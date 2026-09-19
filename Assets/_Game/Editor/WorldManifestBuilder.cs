using System.Collections.Generic;
using System.IO;
using System.Linq;
using Escape.Data;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Writes the generated world manifest — the set of ids scene-authored
    /// persistent objects use, so SaveValidator can tell a real id from a
    /// fabricated one in a save.
    ///
    /// Door/camera/lure ids come from SceneFactory as it generates (a door and
    /// a camera both serialize a plain `id`, so scene YAML cannot separate
    /// them). Terminal command keys come from the content definitions.
    /// </summary>
    public static class WorldManifestBuilder
    {
        public const string Path = "Assets/_Game/Resources/Definitions/WorldManifest.asset";

        [MenuItem("Tools/Escape the Elites/Build World Manifest")]
        public static void Build()
        {
            var terminals = Resources.LoadAll<TerminalDefinition>("Definitions");
            var commandKeys = new SortedSet<string>();
            foreach (var terminal in terminals)
                foreach (var cmd in terminal.Commands)
                    if (!string.IsNullOrEmpty(cmd.Command))
                        commandKeys.Add($"{terminal.Id}:{cmd.Command}");

            var manifest = AssetDatabase.LoadAssetAtPath<WorldManifest>(Path);
            if (manifest == null)
            {
                Blockout.EnsureFolder(System.IO.Path.GetDirectoryName(Path).Replace('\\', '/'));
                manifest = ScriptableObject.CreateInstance<WorldManifest>();
                AssetDatabase.CreateAsset(manifest, Path);
            }

            manifest.doors = SceneFactory.DoorIds.ToArray();
            manifest.cameras = SceneFactory.CameraIds.ToArray();
            manifest.lures = SceneFactory.LureIds.ToArray();
            manifest.terminalCommands = commandKeys.ToArray();

            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            WorldManifest.ClearCache();

            Debug.Log($"[WorldManifestBuilder] {manifest.Count} ids " +
                      $"({manifest.doors.Length} doors, {manifest.cameras.Length} cameras, " +
                      $"{manifest.lures.Length} lures, {manifest.terminalCommands.Length} terminal commands).");
        }
    }
}
