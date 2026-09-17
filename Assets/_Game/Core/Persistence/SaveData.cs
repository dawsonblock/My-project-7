using System;
using System.Collections.Generic;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// Versioned on-disk save format. GameState is the live runtime form;
    /// SaveData is the serialized form that migrations evolve over time.
    /// v1 → v2: wrapped in SaveEnvelope; player pose became explicit
    /// yaw/pitch instead of root eulerAngles.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public string sceneId = "dock";
        public PlayerSaveState player = new PlayerSaveState();
        public List<string> collectedEvidence = new List<string>();
        public List<string> readDocuments = new List<string>();
        public List<string> completedObjectives = new List<string>();
        public List<string> activeObjectives = new List<string>();
        public List<string> gainedInsights = new List<string>();
        public List<string> unlockedDoors = new List<string>();
        public List<string> disabledCameras = new List<string>();
        public List<string> unlockedTerminals = new List<string>();
        public List<string> usedTerminalCommands = new List<string>();
        public bool alert;
        public bool lockdown;
        public bool broadcastStarted;
        public bool broadcastCompleted;
        public string endingId = "";
        public float detection;
        public int lures = 3;
        public List<string> collectedLures = new List<string>();
        public string savedAtUtc = "";

        /// <summary>
        /// Snapshot of live state. Every id list is canonicalized on the way
        /// out: no nulls, no empties, no duplicates, ordinal-sorted — so two
        /// identical states produce identical files.
        /// </summary>
        public static SaveData FromState(GameState s)
        {
            var data = new SaveData
            {
                version = CurrentVersion,
                sceneId = s.SceneId,
                player = s.Player,
                collectedEvidence = new List<string>(s.CollectedEvidence),
                readDocuments = new List<string>(s.ReadDocuments),
                completedObjectives = new List<string>(s.CompletedObjectives),
                activeObjectives = new List<string>(s.ActiveObjectives),
                gainedInsights = new List<string>(s.GainedInsights),
                unlockedDoors = new List<string>(s.UnlockedDoors),
                disabledCameras = new List<string>(s.DisabledCameras),
                unlockedTerminals = new List<string>(s.UnlockedTerminals),
                usedTerminalCommands = new List<string>(s.UsedTerminalCommands),
                alert = s.Alert,
                lockdown = s.Lockdown,
                broadcastStarted = s.BroadcastStarted,
                broadcastCompleted = s.BroadcastCompleted,
                endingId = s.EndingId ?? "",
                detection = s.Detection,
                lures = s.Lures,
                collectedLures = new List<string>(s.CollectedLures),
                savedAtUtc = DateTime.UtcNow.ToString("o")
            };
            data.Canonicalize();
            return data;
        }

        /// <summary>Applies set semantics to every id list, in place.</summary>
        public void Canonicalize()
        {
            Canonicalize(collectedEvidence);
            Canonicalize(readDocuments);
            Canonicalize(completedObjectives);
            Canonicalize(activeObjectives);
            Canonicalize(gainedInsights);
            Canonicalize(unlockedDoors);
            Canonicalize(disabledCameras);
            Canonicalize(unlockedTerminals);
            Canonicalize(usedTerminalCommands);
            Canonicalize(collectedLures);
        }

        private static void Canonicalize(List<string> ids)
        {
            if (ids == null) return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = ids.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(ids[i]) || !seen.Add(ids[i]))
                    ids.RemoveAt(i);
            ids.Sort(StringComparer.Ordinal);
        }

        public GameState ToState()
        {
            Canonicalize(); // defense in depth — don't trust the validator alone
            return new GameState
            {
                SceneId = sceneId,
                Player = player ?? new PlayerSaveState(),
                CollectedEvidence = new List<string>(collectedEvidence ?? new List<string>()),
                ReadDocuments = new List<string>(readDocuments ?? new List<string>()),
                CompletedObjectives = new List<string>(completedObjectives ?? new List<string>()),
                ActiveObjectives = new List<string>(activeObjectives ?? new List<string>()),
                GainedInsights = new List<string>(gainedInsights ?? new List<string>()),
                UnlockedDoors = new List<string>(unlockedDoors ?? new List<string>()),
                DisabledCameras = new List<string>(disabledCameras ?? new List<string>()),
                UnlockedTerminals = new List<string>(unlockedTerminals ?? new List<string>()),
                UsedTerminalCommands = new List<string>(usedTerminalCommands ?? new List<string>()),
                Alert = alert,
                Lockdown = lockdown,
                BroadcastStarted = broadcastStarted,
                BroadcastCompleted = broadcastCompleted,
                EndingId = endingId ?? "",
                Detection = detection,
                Lures = lures,
                CollectedLures = new List<string>(collectedLures ?? new List<string>())
            };
        }
    }

    /// <summary>
    /// One step of save migration. Chain versions: v1 → v2, v2 → v3, ...
    /// Never edit a released schema — add a step instead.
    /// </summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }
        SaveData Migrate(SaveData data);
    }

    public static class SaveMigrator
    {
        private static readonly List<ISaveMigration> Migrations = new List<ISaveMigration>
        {
            new SaveMigrationV1ToV2()
        };

        public static SaveData MigrateToCurrent(SaveData data)
        {
            int guard = 0;
            while (data != null && data.version < SaveData.CurrentVersion && guard++ < 32)
            {
                var step = Migrations.Find(m => m.FromVersion == data.version);
                if (step == null) return null; // unsupported version
                data = step.Migrate(data);
            }
            return data != null && data.version == SaveData.CurrentVersion ? data : null;
        }
    }
}
