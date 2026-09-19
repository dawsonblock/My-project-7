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

        [Test]
        public void RealContent_MainObjectiveChain_IsCompletable()
        {
            // Simulates a full run through the shipped content graph:
            // traversal objectives fired the way SceneBootstrap fires them,
            // every evidence pickup, then the real terminal/console actions.
            // If any main-chain objective is completable by nothing, this fails.
            var content = ContentDatabase.Load();
            var events = new GameEventBus();
            var state = new GameStateService();
            var objectives = new ObjectiveService(state, content, events);
            var insights = new InsightService(state, content, events, objectives);
            var evidence = new EvidenceService(state, content, events, insights, objectives);
            var endings = new EndingService(state, content);
            var world = new WorldService(state, events, endings, objectives);
            var dispatcher = new GameCommandDispatcher(new CommandJournal(echoToConsole: false));
            dispatcher.Register<CollectEvidenceCommand>(evidence);
            dispatcher.Register<ActivateObjectiveCommand>(objectives);
            dispatcher.Register<CompleteObjectiveCommand>(objectives);
            dispatcher.Register<GainInsightCommand>(insights);
            dispatcher.Register<RouteBroadcastCommand>(world);
            dispatcher.Register<CompleteBroadcastCommand>(world);

            // Scene-entry objectives, in dependency order — completion now
            // enforces that an objective's prerequisites are already complete.
            dispatcher.Dispatch(new CompleteObjectiveCommand("reach_compound", "test"));
            dispatcher.Dispatch(new CompleteObjectiveCommand("infiltrate_service", "test"));

            foreach (var ev in content.Evidence)
                dispatcher.Dispatch(new CollectEvidenceCommand(ev.Id, "test"));

            var s = state.State;

            // Evidence alone must NOT finish the explicit objectives —
            // routing the signal and transmitting are actions, not pickups.
            Assert.IsFalse(s.CompletedObjectives.Contains("route_broadcast"),
                "route_broadcast completed without the terminal action");
            Assert.IsFalse(s.CompletedObjectives.Contains("broadcast_truth"),
                "broadcast_truth completed without an actual broadcast");

            // The interior door is released by the service terminal; completing
            // it settles the rest of the evidence chain behind it.
            dispatcher.Dispatch(new CompleteObjectiveCommand("unlock_service_door", "service_terminal"));
            Assert.IsTrue(s.CompletedObjectives.Contains("search_office"),
                "unlocking the door did not settle search_office");
            Assert.IsTrue(s.CompletedObjectives.Contains("download_archive"),
                "the archive pull did not settle behind search_office");

            // Office terminal BROADCAST routes the signal: the domain validates
            // the routing objective's prerequisites and completes it itself.
            dispatcher.Dispatch(new RouteBroadcastCommand("route_broadcast", "office_terminal"));
            Assert.IsTrue(s.BroadcastStarted, "Terminal BROADCAST did not set BroadcastStarted");
            Assert.IsTrue(s.CompletedObjectives.Contains("route_broadcast"));

            // Scene entries that depend on the archive pull.
            dispatcher.Dispatch(new CompleteObjectiveCommand("reach_bunker", "test"));
            dispatcher.Dispatch(new CompleteObjectiveCommand("reach_tower", "test"));

            // Tower relay console: the transmission completes its own objective.
            dispatcher.Dispatch(new CompleteBroadcastCommand("broadcast_truth"));

            foreach (var id in new[] { "reach_compound", "infiltrate_service",
                     "find_keycard", "unlock_service_door", "search_office",
                     "corroborate_story", "download_archive", "route_broadcast",
                     "broadcast_truth", "reach_bunker", "reach_tower" })
                Assert.IsTrue(s.CompletedObjectives.Contains(id),
                    $"Main-chain objective '{id}' did not complete");

            foreach (var ins in content.Insights)
                Assert.IsTrue(s.GainedInsights.Contains(ins.Id),
                    $"Insight '{ins.Id}' did not form from full evidence collection");
        }
    }
}
