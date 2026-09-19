using System.Collections.Generic;
using UnityEngine;

namespace Escape.Data
{
    /// <summary>
    /// Generated inventory of the ids that scene-authored persistent objects
    /// use — doors, cameras, lures and terminal commands. A save carries those
    /// ids, so the validator can only tell a real id from a fabricated one if
    /// it knows the shipped set. Written by WorldManifestBuilder during
    /// BuildAll; absent in a project that has never generated.
    /// </summary>
    public sealed class WorldManifest : ScriptableObject
    {
        public string[] doors = new string[0];
        public string[] cameras = new string[0];
        public string[] lures = new string[0];
        /// <summary>Entries are "terminalId:COMMAND", matching save keys.</summary>
        public string[] terminalCommands = new string[0];

        private static WorldManifest _cached;
        private static bool _probed;

        /// <summary>The shipped manifest, or null when the project has none.</summary>
        public static WorldManifest Load()
        {
            if (_probed) return _cached;
            _probed = true;
            _cached = Resources.Load<WorldManifest>("Definitions/WorldManifest");
            return _cached;
        }

        /// <summary>Drops the cache — for tests and after regeneration.</summary>
        public static void ClearCache()
        {
            _cached = null;
            _probed = false;
        }

        public bool IsKnownDoor(string id) => Contains(doors, id);
        public bool IsKnownCamera(string id) => Contains(cameras, id);
        public bool IsKnownLure(string id) => Contains(lures, id);
        public bool IsKnownTerminalCommand(string id) => Contains(terminalCommands, id);

        private static bool Contains(string[] set, string id)
        {
            if (set == null || string.IsNullOrEmpty(id)) return false;
            foreach (var entry in set)
                if (entry == id) return true;
            return false;
        }

        /// <summary>Total ids known, for diagnostics and tests.</summary>
        public int Count =>
            (doors?.Length ?? 0) + (cameras?.Length ?? 0) +
            (lures?.Length ?? 0) + (terminalCommands?.Length ?? 0);

        public static WorldManifest From(IEnumerable<string> doorIds,
            IEnumerable<string> cameraIds, IEnumerable<string> lureIds,
            IEnumerable<string> terminalCommands)
        {
            var m = CreateInstance<WorldManifest>();
            m.doors = new List<string>(doorIds).ToArray();
            m.cameras = new List<string>(cameraIds).ToArray();
            m.lures = new List<string>(lureIds).ToArray();
            m.terminalCommands = new List<string>(terminalCommands).ToArray();
            return m;
        }
    }
}
