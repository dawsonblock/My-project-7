using System;
using System.Collections.Generic;
using System.IO;
using Escape.Data;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>Metadata about a save slot for load-game menus.</summary>
    public sealed class SaveSlotInfo
    {
        public string Slot;
        public bool Exists;
        /// <summary>Parses + validates cleanly (or repairably).</summary>
        public bool Valid;
        /// <summary>A backup exists that can recover a corrupt primary.</summary>
        public bool Recoverable;
        public string SceneId = "";
        public string SceneName = "";
        public string TimestampUtc = "";
        public int EvidenceCount;
        public int ObjectivesCompleted;
    }

    public interface ISaveService
    {
        string[] Slots { get; }
        bool HasSave(string slot);
        string MostRecentSlot();
        SaveSlotInfo GetSlotInfo(string slot);
        bool Save(string slot);
        bool Load(string slot);
        bool Delete(string slot);
    }

    /// <summary>
    /// JSON file saves under persistentDataPath/saves. Save flow:
    /// capture live participant state → canonicalize → envelope →
    /// serialize → tmp → read-back verify → atomic replace with .bak.
    /// Load flow: slot.json → slot.bak → fail, each parsed, migrated,
    /// and validated before touching live state.
    /// </summary>
    public sealed class SaveService : ISaveService,
        IGameCommandHandler<SaveGameCommand>,
        IGameCommandHandler<LoadGameCommand>
    {
        public const string Autosave = "autosave";

        private readonly IGameStateService _state;
        private readonly IContentDatabase _content;
        private readonly IGameEventBus _events;
        private readonly IGameCommandDispatcher _dispatcher;
        private readonly ISaveCoordinator _coordinator;
        private readonly string _dir;

        public string[] Slots { get; } = { Autosave, "slot1", "slot2", "slot3" };

        public SaveService(IGameStateService state, IContentDatabase content,
            IGameEventBus events, IGameCommandDispatcher dispatcher,
            string directory = null, ISaveCoordinator coordinator = null)
        {
            _state = state;
            _content = content;
            _events = events;
            _dispatcher = dispatcher;
            _coordinator = coordinator;
            _dir = directory ?? Path.Combine(Application.persistentDataPath, "saves");
        }

        private string PathFor(string slot) => Path.Combine(_dir, slot + ".json");

        public bool HasSave(string slot) =>
            File.Exists(PathFor(slot)) || File.Exists(SaveFileIO.BakPath(PathFor(slot)));

        public string MostRecentSlot()
        {
            // A backup-only slot is still a recoverable save — HasSave and
            // Load agree, so slot discovery must too. Otherwise the menu
            // disables CONTINUE/LOAD for a save the storage layer can load.
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (var slot in Slots)
            {
                foreach (var p in new[] { PathFor(slot), SaveFileIO.BakPath(PathFor(slot)) })
                {
                    DateTime t;
                    try
                    {
                        if (!File.Exists(p)) continue;
                        t = File.GetLastWriteTimeUtc(p);
                    }
                    catch (Exception) { continue; }
                    if (t > bestTime) { bestTime = t; best = slot; }
                }
            }
            return best;
        }

        public SaveSlotInfo GetSlotInfo(string slot)
        {
            var info = new SaveSlotInfo { Slot = slot };
            var final = PathFor(slot);
            info.Exists = File.Exists(final) || File.Exists(SaveFileIO.BakPath(final));
            if (!info.Exists) return info;

            bool primaryUsable = false;
            foreach (var path in new[] { final, SaveFileIO.BakPath(final) })
            {
                string json;
                try
                {
                    if (!File.Exists(path)) continue;
                    json = File.ReadAllText(path);
                }
                catch (Exception) { continue; } // locked/unreadable file — treat as absent
                if (TryParse(json, out var data, out var result))
                {
                    info.Valid = true;
                    info.SceneId = data.sceneId;
                    info.SceneName = _content.SceneName(data.sceneId) ?? data.sceneId;
                    info.TimestampUtc = data.savedAtUtc;
                    info.EvidenceCount = data.collectedEvidence?.Count ?? 0;
                    info.ObjectivesCompleted = data.completedObjectives?.Count ?? 0;
                    if (path == final) primaryUsable = true;
                    break;
                }
            }
            // Loadable from the backup because the primary is missing *or*
            // corrupt — the menu should still offer recovery.
            info.Recoverable = info.Valid && !primaryUsable;
            return info;
        }

        public bool Save(string slot)
        {
            try
            {
                Directory.CreateDirectory(_dir);
                // Capture volatile runtime state (live pose, flashlight)
                // before snapshotting — GameState lists are already current.
                _coordinator?.CaptureInto(_state.State);
                var data = SaveData.FromState(_state.State);
                var envelope = new SaveEnvelope
                {
                    schemaVersion = SaveData.CurrentVersion,
                    gameVersion = Application.version,
                    timestampUtc = DateTime.UtcNow.ToString("o"),
                    slotId = slot,
                    payload = data
                };
                var json = JsonUtility.ToJson(envelope, true);
                SaveFileIO.AtomicWrite(PathFor(slot), json);
                _events.Publish(new GameSavedEvent(slot));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Save failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Loads state but does not change scene — caller decides.</summary>
        public bool LoadIntoState(string slot, out List<string> errors)
        {
            errors = new List<string>();
            var final = PathFor(slot);
            // Recovery order: primary, then backup.
            foreach (var path in new[] { final, SaveFileIO.BakPath(final) })
            {
                if (!SaveFileIO.TryRead(path, out var json))
                {
                    if (path == final) errors.Add("No save in slot.");
                    continue;
                }
                if (!TryParse(json, out var data, out var result))
                {
                    errors.AddRange(result.Errors);
                    foreach (var w in result.Warnings) errors.Add($"warning: {w}");
                    continue; // fall through to the backup
                }
                if (result.WasRepaired)
                    Debug.LogWarning($"[SaveService] Repaired save '{Path.GetFileName(path)}': " +
                                     string.Join("; ", result.Warnings));
                _state.ReplaceState(data.ToState());
                return true;
            }
            return false;
        }

        /// <summary>
        /// Parse → version-inspect → migrate → validate. Returns false when
        /// the file is unusable; the caller falls back to the .bak.
        /// </summary>
        private bool TryParse(string json, out SaveData data, out SaveValidationResult result)
        {
            data = null;
            result = new SaveValidationResult();
            SaveData raw;
            try
            {
                var probe = JsonUtility.FromJson<SaveEnvelopeProbe>(json);
                if (probe != null && probe.schemaVersion >= 1)
                {
                    // Enveloped format: schemaVersion is authoritative.
                    if (probe.schemaVersion > SaveData.CurrentVersion)
                    {
                        result.Errors.Add($"Save schema {probe.schemaVersion} is newer than supported {SaveData.CurrentVersion}.");
                        return false;
                    }
                    raw = JsonUtility.FromJson<SaveEnvelope>(json)?.payload;
                    if (raw != null) raw.version = probe.schemaVersion;
                }
                else
                {
                    // Legacy v1: bare SaveData, no envelope.
                    raw = JsonUtility.FromJson<SaveData>(json);
                    if (raw != null && raw.version < 1) raw.version = 1;
                }
            }
            catch (Exception e)
            {
                result.Errors.Add($"Unparseable save: {e.Message}");
                return false;
            }

            data = SaveMigrator.MigrateToCurrent(raw);
            if (data == null)
            {
                result.Errors.Add("Save version unsupported.");
                return false;
            }
            result = SaveValidator.Validate(data, _content, WorldManifest.Load());
            return result.IsValid;
        }

        public bool Load(string slot)
        {
            if (!LoadIntoState(slot, out var errors))
            {
                Debug.LogWarning($"[SaveService] Load '{slot}' rejected: {string.Join("; ", errors)}");
                _events.Publish(new SystemMessageEvent("Save could not be loaded."));
                return false;
            }
            _events.Publish(new GameLoadedEvent(slot));
            _dispatcher.Dispatch(new ChangeSceneCommand(_state.State.SceneId));
            return true;
        }

        public bool Delete(string slot)
        {
            var p = PathFor(slot);
            bool any = false;
            foreach (var f in new[] { p, SaveFileIO.BakPath(p), SaveFileIO.TmpPath(p) })
            {
                try
                {
                    if (File.Exists(f)) { File.Delete(f); any = true; }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveService] Could not delete '{f}': {e.Message}");
                }
            }
            return any;
        }

        public void Handle(SaveGameCommand command) => Save(command.Slot);
        public void Handle(LoadGameCommand command) => Load(command.Slot);
    }
}
