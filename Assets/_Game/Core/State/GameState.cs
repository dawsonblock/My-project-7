using System;
using System.Collections.Generic;
using UnityEngine;

namespace Escape.Core
{
    [Serializable]
    public sealed class PlayerSaveState
    {
        /// <summary>
        /// True once a pose has actually been captured. Without it, a player
        /// legitimately saved at the world origin is indistinguishable from a
        /// save that never recorded a pose.
        /// </summary>
        public bool HasPose;
        public Vector3 Position;
        /// <summary>Player root yaw, degrees.</summary>
        public float Yaw;
        /// <summary>Camera pitch, degrees. Lives on PlayerLook, not the root.</summary>
        public float Pitch;
        public bool FlashlightOn;
        /// <summary>Legacy v1 field — read by SaveMigrationV1ToV2 only.</summary>
        public Vector3 EulerRotation;
    }

    public enum DetectionLevel
    {
        Hidden,
        Safe,
        Suspicious,
        Imminent,
        Detected
    }

    /// <summary>
    /// Live runtime state. Definitions (ScriptableObjects) are immutable
    /// design-time data; this is the only thing that changes while playing.
    /// Never stored in a ScriptableObject.
    /// </summary>
    [Serializable]
    public sealed class GameState
    {
        public string SceneId = Escape.Data.SceneId.Dock;
        public List<string> CollectedEvidence = new List<string>();
        public List<string> ReadDocuments = new List<string>();
        public List<string> CompletedObjectives = new List<string>();
        public List<string> ActiveObjectives = new List<string>();
        public List<string> GainedInsights = new List<string>();
        public List<string> UnlockedDoors = new List<string>();
        public List<string> DisabledCameras = new List<string>();
        public List<string> UnlockedTerminals = new List<string>();
        public List<string> UsedTerminalCommands = new List<string>();
        public bool Alert;
        public bool Lockdown;
        public bool BroadcastStarted;
        public bool BroadcastCompleted;
        public string EndingId;
        public float Detection;
        public int Lures = 3;
        public List<string> CollectedLures = new List<string>();
        public PlayerSaveState Player = new PlayerSaveState();

        public GameState Clone()
        {
            var json = JsonUtility.ToJson(this);
            return JsonUtility.FromJson<GameState>(json);
        }
    }
}
