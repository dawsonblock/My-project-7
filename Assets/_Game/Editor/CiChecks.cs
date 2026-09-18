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

        [MenuItem("Tools/Escape the Elites/Verify Deterministic Generation")]
        public static void VerifyDeterministicGeneration()
        {
            BuildAll.Run();
            var first = Snapshot();
            BuildAll.Run();
            var second = Snapshot();

            var diffs = new List<string>();
            foreach (var kv in first)
            {
                if (!second.TryGetValue(kv.Key, out var h))
                    diffs.Add($"missing after rebuild: {kv.Key}");
                else if (h != kv.Value)
                    diffs.Add($"content churn: {kv.Key}");
            }
            foreach (var kv in second)
                if (!first.ContainsKey(kv.Key))
                    diffs.Add($"new file on rebuild: {kv.Key}");

            if (diffs.Count > 0)
                throw new Exception(
                    "Generation is not deterministic:\n  " + string.Join("\n  ", diffs));

            Debug.Log($"[CiChecks] Deterministic generation verified ({first.Count} files).");
        }

        private static Dictionary<string, string> Snapshot()
        {
            var map = new Dictionary<string, string>();
            using (var sha = SHA256.Create())
            {
                foreach (var dir in GeneratedDirs)
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
