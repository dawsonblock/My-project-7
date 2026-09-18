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
    ///   2. Docs are iteratively re-partitioned by (own signature, sorted
    ///      ranks of local ref targets) — WL-style refinement using RANKS,
    ///      not hash-chaining. Rank refinement is synchronous and order-free,
    ///      so it reaches the same coarsest partition on cyclic graphs
    ///      (Transform parent/child refs) regardless of input doc order.
    ///   3. Documents are emitted sorted by final rank; local ids become the
    ///      sorted position. External refs (which carry a guid) are untouched.
    ///
    /// Deterministic generation then yields a byte-identical file. Residual
    /// risk: truly automorphic docs (identical content AND identical resolved
    /// neighborhoods) order by input position — generated content uses unique
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
            public string Body;
            public long[] Refs;          // ordered local fileID targets in Body
            public string OwnSig;        // content signature, refs blanked
            public int Rank;
        }

        public static void NormalizeDirectory(string dir, string pattern = "*.unity")
        {
            if (!Directory.Exists(dir)) return;
            foreach (var path in Directory.GetFiles(dir, pattern, SearchOption.AllDirectories))
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
                });
            }

            var byOldId = docs.ToDictionary(d => d.OldId);
            foreach (var d in docs)
            {
                var blanked = LocalRef.Replace(d.Body, "{fileID: *}");
                d.OwnSig = Hash(d.ClassId + "|" + d.Stripped + "|" + blanked);
            }

            // Rank-based refinement: partition by (OwnSig, target ranks) until
            // the partition is stable. Ranks are canonical (sorted unique
            // keys), so the fixpoint is independent of document input order.
            AssignRanks(docs, d => d.OwnSig);
            for (var iter = 0; iter < 64; iter++)
            {
                var snapshot = docs.ToDictionary(d => d, d => d.Rank);
                AssignRanks(docs, d =>
                {
                    var sb = new StringBuilder(d.OwnSig);
                    foreach (var r in d.Refs)
                        sb.Append('|').Append(
                            byOldId.TryGetValue(r, out var t) ? snapshot[t] : -1);
                    return sb.ToString();
                });
                if (docs.All(d => snapshot[d] == d.Rank))
                    break;
            }

            var ordered = docs.OrderBy(d => d.Rank)
                .ThenBy(d => d.OwnSig, StringComparer.Ordinal).ToList();

            // Stripped-object ids are NOT free: Unity requires them to be
            // (prefabInstanceFileID + k) for the k-th stripped doc of that
            // instance, or prefab resolution fails on load. Assign a
            // PrefabInstance's id, then its stripped docs contiguously.
            var strippedOf = new Dictionary<long, List<Doc>>();
            foreach (var d in docs)
            {
                if (!d.Stripped) continue;
                var m = Regex.Match(d.Body, @"m_PrefabInstance: \{fileID: (-?\d+)\}");
                if (m.Success)
                {
                    var inst = long.Parse(m.Groups[1].Value);
                    if (!strippedOf.TryGetValue(inst, out var l))
                        strippedOf[inst] = l = new List<Doc>();
                    l.Add(d);
                }
            }
            foreach (var l in strippedOf.Values)
                l.Sort((a, b) => string.CompareOrdinal(a.ClassId + a.OwnSig, b.ClassId + b.OwnSig));

            var newId = new Dictionary<long, long>(docs.Count);
            var next = 1L;
            foreach (var d in ordered)
            {
                if (d.Stripped) continue;
                newId[d.OldId] = next++;
                if (d.ClassId == "1001" && strippedOf.TryGetValue(d.OldId, out var kids))
                    foreach (var k in kids) newId[k.OldId] = next++;
            }
            foreach (var d in docs)
                if (!newId.ContainsKey(d.OldId)) newId[d.OldId] = next++;

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

        /// <summary>Assigns canonical ranks: docs sorted by key get ranks
        /// 0..k-1; equal keys share a rank. Returns the distinct-key count.</summary>
        private static int AssignRanks(List<Doc> docs, Func<Doc, string> key)
        {
            var keyed = docs.Select(d => (d, k: key(d))).ToList();
            var distinct = keyed.Select(x => x.k).Distinct()
                .OrderBy(k => k, StringComparer.Ordinal).ToList();
            var rankOf = new Dictionary<string, int>(distinct.Count);
            for (var i = 0; i < distinct.Count; i++) rankOf[distinct[i]] = i;
            foreach (var (d, k) in keyed) d.Rank = rankOf[k];
            return distinct.Count;
        }

        private static string Hash(string s)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));
        }
    }
}
