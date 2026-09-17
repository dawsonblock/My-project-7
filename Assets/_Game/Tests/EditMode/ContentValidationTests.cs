using System.Collections.Generic;
using Escape.Core;
using Escape.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Escape.Tests.EditMode
{
    /// <summary>
    /// Content-integrity tests across every generated definition asset.
    /// These are the tests that keep the imported web data honest.
    /// </summary>
    public class ContentValidationTests
    {
        private static List<T> LoadAll<T>() where T : ScriptableObject
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets(
                         $"t:{typeof(T).Name}", new[] { "Assets/_Game/Resources/Definitions" }))
                list.Add(AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));
            return list;
        }

        [Test]
        public void EvidenceIds_AreUnique_AndNonEmpty()
        {
            var seen = new HashSet<string>();
            foreach (var e in LoadAll<EvidenceDefinition>())
            {
                Assert.IsFalse(string.IsNullOrEmpty(e.Id), "Evidence with empty Id");
                Assert.IsTrue(seen.Add(e.Id), $"Duplicate evidence id '{e.Id}'");
            }
            Assert.Greater(seen.Count, 0, "No evidence definitions imported — run Import Web Content.");
        }

        [Test]
        public void ObjectiveIds_AreUnique_AndNonEmpty()
        {
            var seen = new HashSet<string>();
            foreach (var o in LoadAll<ObjectiveDefinition>())
            {
                Assert.IsFalse(string.IsNullOrEmpty(o.Id));
                Assert.IsTrue(seen.Add(o.Id), $"Duplicate objective id '{o.Id}'");
            }
            Assert.Greater(seen.Count, 0);
        }

        [Test]
        public void TerminalAndEndingIds_AreUnique()
        {
            var t = new HashSet<string>();
            foreach (var x in LoadAll<TerminalDefinition>()) Assert.IsTrue(t.Add(x.Id), $"dup terminal {x.Id}");
            var e = new HashSet<string>();
            foreach (var x in LoadAll<EndingDefinition>()) Assert.IsTrue(e.Add(x.Id), $"dup ending {x.Id}");
        }

        [Test]
        public void EvidenceRelationships_ReferenceValidAssets()
        {
            var all = new HashSet<string>();
            foreach (var e in LoadAll<EvidenceDefinition>()) all.Add(e.Id);
            foreach (var e in LoadAll<EvidenceDefinition>())
            {
                foreach (var c in e.Corroborates)
                    Assert.IsTrue(c != null && all.Contains(c.Id), $"{e.Id} corroborates missing ref");
                foreach (var c in e.Contradicts)
                    Assert.IsTrue(c != null && all.Contains(c.Id), $"{e.Id} contradicts missing ref");
            }
        }

        [Test]
        public void Objectives_HaveNoImpossibleCycles()
        {
            var all = LoadAll<ObjectiveDefinition>();
            var byId = new Dictionary<string, ObjectiveDefinition>();
            foreach (var o in all) byId[o.Id] = o;
            // Missing dependency check — the cycle checker treats unknown
            // ids as leaves, so validate references separately.
            foreach (var o in all)
                foreach (var req in o.RequiredObjectives)
                    Assert.IsTrue(req == null || byId.ContainsKey(req.Id),
                        $"{o.Id} requires missing objective");
            Assert.IsFalse(
                DependencyGraph.HasCycle(
                    byId.Keys,
                    id => byId.TryGetValue(id, out var o)
                        ? Deps(o) : null,
                    out var cycleAt),
                $"Objective dependency cycle at '{cycleAt}'");
        }

        private static IEnumerable<string> Deps(ObjectiveDefinition o)
        {
            foreach (var req in o.RequiredObjectives)
                if (req != null) yield return req.Id;
        }

        // --- DependencyGraph unit tests -----------------------------------

        [Test]
        public void DependencyGraph_Diamond_IsNotCycle()
        {
            //   A → B,C ; B → D ; C → D — shared dep is legal.
            var edges = new Dictionary<string, string[]>
            {
                ["A"] = new[] { "B", "C" },
                ["B"] = new[] { "D" },
                ["C"] = new[] { "D" },
                ["D"] = new string[0]
            };
            Assert.IsFalse(DependencyGraph.HasCycle(
                edges.Keys, id => edges[id], out _));
        }

        [Test]
        public void DependencyGraph_SimpleCycle_Fails()
        {
            var edges = new Dictionary<string, string[]>
            {
                ["A"] = new[] { "B" },
                ["B"] = new[] { "C" },
                ["C"] = new[] { "A" }
            };
            Assert.IsTrue(DependencyGraph.HasCycle(
                edges.Keys, id => edges[id], out var at));
            Assert.IsTrue(at == "A" || at == "B" || at == "C");
        }

        [Test]
        public void DependencyGraph_SelfLoop_Fails()
        {
            var edges = new Dictionary<string, string[]> { ["A"] = new[] { "A" } };
            Assert.IsTrue(DependencyGraph.HasCycle(
                edges.Keys, id => edges[id], out var at));
            Assert.AreEqual("A", at);
        }

        [Test]
        public void TerminalCommands_ReferenceValidTargets()
        {
            var ev = new HashSet<string>();
            foreach (var e in LoadAll<EvidenceDefinition>()) ev.Add(e.Id);
            var obj = new HashSet<string>();
            foreach (var o in LoadAll<ObjectiveDefinition>()) obj.Add(o.Id);
            foreach (var t in LoadAll<TerminalDefinition>())
            {
                Assert.IsFalse(string.IsNullOrEmpty(t.Id));
                foreach (var c in t.Commands)
                {
                    switch (c.Action)
                    {
                        case TerminalActionType.CollectEvidence:
                            Assert.IsTrue(ev.Contains(c.TargetId),
                                $"{t.Id}:{c.Command} targets unknown evidence '{c.TargetId}'");
                            break;
                        case TerminalActionType.CompleteObjective:
                            Assert.IsTrue(obj.Contains(c.TargetId),
                                $"{t.Id}:{c.Command} targets unknown objective '{c.TargetId}'");
                            break;
                    }
                }
            }
        }

        [Test]
        public void AtLeastOneEnding_AlwaysQualifies()
        {
            // ending_bad must have zero requirements so evaluation never
            // returns null.
            var endings = LoadAll<EndingDefinition>();
            bool fallback = false;
            foreach (var e in endings)
                if (e.MinPrimaryEvidence == 0 && e.RequiredEvidence.Length == 0 &&
                    e.RequiredInsights.Length == 0)
                    fallback = true;
            Assert.IsTrue(fallback, "No fallback ending (zero-requirement) exists.");
        }
    }
}
