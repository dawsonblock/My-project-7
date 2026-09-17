using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Unity serializes scene YAML documents in local-fileID order, and those
    /// ids are assigned randomly per save — so byte-identical generation still
    /// produces a shuffled file. This pass makes output canonical:
    ///
    ///   1. Every document gets a signature from its class, stripped-flag and
    ///      body with local fileID refs blanked.
    ///   2. Signatures are iteratively refined by folding in the signatures of
    ///      each document's local ref targets (WL-style), so docs that differ
    ///      only in who they point at — e.g. stripped Transforms of sibling
    ///      prefab instances — separate correctly.
    ///   3. Documents are emitted sorted by final signature; local ids become
    ///      the sorted rank. External refs (which carry a guid) are untouched.
    ///
    /// Deterministic generation then yields a byte-identical file. Residual
    /// risk: truly automorphic doc sets (identical content AND identical
    /// resolved refs) sort by input order — generated content uses unique
    /// names/ids, so this does not occur in practice.
    /// </summary>
    public static class SceneYamlNormalizer
    {
        private static readonly Regex DocHeader =
            new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$", RegexOptions.Multiline);
        private static readonly Regex LocalRef =
            new Regex(@"\{fileID: (-?\d+)\}");

        private sealed class Doc
        {
            public string ClassId;
            public long OldId;
            public bool Stripped;
            public string Body;          // verbatim text after the header line
            public long[] Refs;          // ordered local fileID targets in Body
            public string Sig0;          // own-content signature
            public string Sig;           // refined signature
            public int InputIndex;
        }

        public static void NormalizeDirectory(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var path in Directory.GetFiles(dir, "*.unity"))
                NormalizeFile(path);
        }

        public static void NormalizeFile(string path)
        {
            var text = File.ReadAllText(path);
            if (!text.StartsWith("%YAML")) return; // binary — skip

            var matches = DocHeader.Matches(text);
            if (matches.Count == 0) return;

            var prefix = text.Substring(0, matches[0].Index);
            var docs = new List<Doc>(matches.Count);
            for (var i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                var bodyStart = m.Index + m.Length;
                var bodyEnd = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                var body = text.Substring(bodyStart, bodyEnd - bodyStart);
                docs.Add(new Doc
                {
                    ClassId = m.Groups[1].Value,
                    OldId = long.Parse(m.Groups[2].Value),
                    Stripped = m.Groups[3].Success,
                    Body = body,
                    Refs = LocalRef.Matches(body).Cast<Match>()
                        .Select(r => long.Parse(r.Groups[1].Value)).ToArray(),
                    InputIndex = i,
                });
            }

            var byOldId = docs.ToDictionary(d => d.OldId);
            var blanked = docs.Select(d => LocalRef.Replace(d.Body, "{fileID: *}")).ToArray();
            for (var i = 0; i < docs.Count; i++)
                docs[i].Sig0 = docs[i].Sig = Hash(docs[i].ClassId + "|" + docs[i].Stripped + "|" + blanked[i]);

            // Refine until the partition is stable (bounded).
            for (var iter = 0; iter < 16; iter++)
            {
                var changed = false;
                foreach (var d in docs)
                {
                    var sb = new StringBuilder(d.Sig0);
                    foreach (var r in d.Refs)
                    {
                        sb.Append('|');
                        sb.Append(byOldId.TryGetValue(r, out var t) ? t.Sig : "ext");
                    }
                    var next = Hash(sb.ToString());
                    if (next != d.Sig) { d.Sig = next; changed = true; }
                }
                if (!changed) break;
            }

            var ordered = docs.OrderBy(d => d.Sig, StringComparer.Ordinal).ToList();
            var newId = new Dictionary<long, long>(docs.Count);
            for (var i = 0; i < ordered.Count; i++)
                newId[ordered[i].OldId] = i + 1;

            var outSb = new StringBuilder(text.Length + 64);
            outSb.Append(prefix);
            foreach (var d in ordered)
            {
                outSb.Append("--- !u!").Append(d.ClassId).Append(" &").Append(newId[d.OldId]);
                if (d.Stripped) outSb.Append(" stripped");
                outSb.Append('\n');
                outSb.Append(LocalRef.Replace(d.Body, m =>
                    newId.TryGetValue(long.Parse(m.Groups[1].Value), out var nid)
                        ? "{fileID: " + nid + "}"
                        : m.Value));
            }

            File.WriteAllText(path, outSb.ToString());
            Debug.Log($"[SceneYamlNormalizer] Normalized {Path.GetFileName(path)} ({docs.Count} objects)");
        }

        private static string Hash(string s)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));
        }
    }
}
