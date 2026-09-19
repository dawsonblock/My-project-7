using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// CI entry points. VerifyDeterministicGeneration runs the full content
    /// pipeline twice and throws if any generated file differs — usable via
    /// -executeMethod locally or as a buildMethod in CI.
    /// </summary>
    public static class CiChecks
    {
        private static readonly string[] GeneratedDirs =
        {
            "Assets/_Game/Scenes",
            "Assets/_Game/Prefabs",
        };

        /// <summary>
        /// Definition assets that WebDataImporter regenerates from the frozen
        /// JSON in MigrationReference/. Editing one of these by hand is
        /// silently reverted by the next BuildAll — and the determinism gate
        /// still passes, because both of its runs agree on the JSON-derived
        /// result. So drift is treated as a build failure, not a surprise.
        /// </summary>
        private static readonly string[] DefinitionDirs =
        {
            "Assets/_Game/Resources/Definitions",
        };

        [MenuItem("Tools/Escape the Elites/Verify Deterministic Generation")]
        public static void VerifyDeterministicGeneration()
        {
            BuildAll.Run();
            var first = Snapshot(GeneratedDirs);
            BuildAll.Run();
            var second = Snapshot(GeneratedDirs);

            var diffs = DescribeDiffs(first, second, "rebuild");
            if (diffs.Count > 0)
                throw new Exception(
                    "Generation is not deterministic:\n  " + string.Join("\n  ", diffs));

            Debug.Log($"[CiChecks] Deterministic generation verified ({first.Count} files).");
        }

        /// <summary>
        /// Fails if the tracked definition assets differ from what the frozen
        /// JSON produces — i.e. if someone hand-edited a generated .asset, or
        /// changed the importer without reimporting. Reimporting is the fix,
        /// so this check performs it: the tree is left canonical and the run
        /// fails with the list of files that were out of sync.
        /// </summary>
        [MenuItem("Tools/Escape the Elites/Verify Generated Content Matches Source")]
        public static void VerifyGeneratedContentMatchesSource()
        {
            var before = Snapshot(DefinitionDirs);
            WebDataImporter.ImportAll();
            AssetDatabase.SaveAssets();
            var after = Snapshot(DefinitionDirs);

            var diffs = DescribeDiffs(before, after, "reimport");
            if (diffs.Count > 0)
                throw new Exception(
                    "Generated definitions do not match MigrationReference. Edit the JSON in " +
                    "MigrationReference/ rather than the .asset; the definitions have now been " +
                    "reimported to match. Files that changed:\n  " +
                    string.Join("\n  ", diffs) +
                    "\n\nIf one of those files is NEW, a definition asset was missing and the " +
                    "importer recreated it with a fresh GUID — scenes and prefabs still reference " +
                    "the old GUID, so restore the deleted asset (git checkout) and fix the " +
                    "references before committing anything.");

            Debug.Log($"[CiChecks] Generated content matches its source ({before.Count} files).");
        }

        /// <summary>Files present in one snapshot but not the other, or changed.</summary>
        private static List<string> DescribeDiffs(Dictionary<string, string> before,
            Dictionary<string, string> after, string action)
        {
            var diffs = new List<string>();
            foreach (var kv in before)
            {
                if (!after.TryGetValue(kv.Key, out var h))
                    diffs.Add($"missing after {action}: {kv.Key}");
                else if (h != kv.Value)
                    diffs.Add($"content churn: {kv.Key}");
            }
            foreach (var kv in after)
                if (!before.ContainsKey(kv.Key))
                    diffs.Add($"new on {action}: {kv.Key}");
            return diffs;
        }

        private static Dictionary<string, string> Snapshot(string[] dirs)
        {
            var map = new Dictionary<string, string>();
            using (var sha = SHA256.Create())
            {
                foreach (var dir in dirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var path in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                    {
                        if (path.EndsWith(".meta")) continue;
                        map[path] = Convert.ToBase64String(
                            sha.ComputeHash(File.ReadAllBytes(path)));
                    }
                }
            }
            return map;
        }
    }
}
