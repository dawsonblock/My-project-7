using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Unity assigns fresh random local fileIDs to scene objects on every
    /// SaveScene call, so byte-identical generation still produces a diff.
    /// This pass renumbers local fileIDs sequentially in document order —
    /// deterministic output for deterministic generation. References to
    /// external assets (which carry a guid) are left untouched.
    /// </summary>
    public static class SceneYamlNormalizer
    {
        private static readonly Regex Header =
            new Regex(@"^(--- !u!\d+ &)(-?\d+)", RegexOptions.Multiline);
        private static readonly Regex LocalRef =
            new Regex(@"\{fileID: (-?\d+)\}");

        public static void NormalizeDirectory(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var path in Directory.GetFiles(dir, "*.unity"))
                NormalizeFile(path);
        }

        public static void NormalizeFile(string path)
        {
            var text = File.ReadAllText(path);
            if (!text.StartsWith("%YAML")) return; // binary file — skip

            // First pass: assign new sequential ids in document order.
            var map = new Dictionary<long, long>();
            long next = 1;
            foreach (Match m in Header.Matches(text))
            {
                var id = long.Parse(m.Groups[2].Value);
                if (!map.ContainsKey(id)) map[id] = next++;
            }

            // Rewrite document headers.
            text = Header.Replace(text, m =>
                m.Groups[1].Value + map[long.Parse(m.Groups[2].Value)]);

            // Rewrite bare local references ({fileID: N} with no guid).
            text = LocalRef.Replace(text, m =>
            {
                var id = long.Parse(m.Groups[1].Value);
                return map.TryGetValue(id, out var nid)
                    ? "{fileID: " + nid + "}"
                    : m.Value;
            });

            File.WriteAllText(path, text);
            Debug.Log($"[SceneYamlNormalizer] Normalized {Path.GetFileName(path)} ({map.Count} objects)");
        }
    }
}
