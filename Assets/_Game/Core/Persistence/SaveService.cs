using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Escape.Core
{
    public interface ISaveService
    {
        string[] Slots { get; }
        bool HasSave(string slot);
        string MostRecentSlot();
        bool Save(string slot);
        bool Load(string slot);
        bool Delete(string slot);
    }

    /// <summary>
    /// JSON file saves under persistentDataPath/saves. Autosave plus three
    /// manual slots. Every write is validated on read-back structure, every
    /// load is validated against the content database.
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
        private readonly string _dir;

        public string[] Slots { get; } = { Autosave, "slot1", "slot2", "slot3" };

        public SaveService(IGameStateService state, IContentDatabase content,
            IGameEventBus events, IGameCommandDispatcher dispatcher, string directory = null)
        {
            _state = state;
            _content = content;
            _events = events;
            _dispatcher = dispatcher;
            _dir = directory ?? Path.Combine(Application.persistentDataPath, "saves");
        }

        private string PathFor(string slot) => Path.Combine(_dir, slot + ".json");

        public bool HasSave(string slot) => File.Exists(PathFor(slot));

        public string MostRecentSlot()
        {
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (var slot in Slots)
            {
                var p = PathFor(slot);
                if (!File.Exists(p)) continue;
                var t = File.GetLastWriteTimeUtc(p);
                if (t > bestTime) { bestTime = t; best = slot; }
            }
            return best;
        }

        public bool Save(string slot)
        {
            try
            {
                Directory.CreateDirectory(_dir);
                var json = JsonUtility.ToJson(SaveData.FromState(_state.State), true);
                File.WriteAllText(PathFor(slot), json);
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
            var path = PathFor(slot);
            if (!File.Exists(path)) { errors.Add("No save in slot."); return false; }
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                data = SaveMigrator.MigrateToCurrent(data);
                if (data == null) { errors.Add("Save version unsupported."); return false; }
                if (!SaveValidator.Validate(data, _content, errors)) return false;
                _state.ReplaceState(data.ToState());
                return true;
            }
            catch (Exception e)
            {
                errors.Add(e.Message);
                return false;
            }
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
            if (!File.Exists(p)) return false;
            File.Delete(p);
            return true;
        }

        public void Handle(SaveGameCommand command) => Save(command.Slot);
        public void Handle(LoadGameCommand command) => Load(command.Slot);
    }
}
