using System.Collections.Generic;
using Escape.Core;
using Escape.Data;
using NUnit.Framework;
using UnityEngine;

namespace Escape.Tests.EditMode
{
    /// <summary>
    /// Exercises the command → service → state → event pipeline headlessly.
    /// </summary>
    public class CommandAndStateTests
    {
        private CommandJournal _journal;
        private GameCommandDispatcher _dispatcher;
        private GameEventBus _events;
        private TestContent _content;
        private GameStateService _state;
        private ObjectiveService _objectives;
        private InsightService _insights;
        private EvidenceService _evidence;
        private EndingService _endings;
        private WorldService _world;

        [SetUp]
        public void Setup()
        {
            _journal = new CommandJournal(echoToConsole: false);
            _dispatcher = new GameCommandDispatcher(_journal);
            _events = new GameEventBus();
            _content = new TestContent();
            _state = new GameStateService();
            _objectives = new ObjectiveService(_state, _content, _events);
            _insights = new InsightService(_state, _content, _events, _objectives);
            _evidence = new EvidenceService(_state, _content, _events, _insights, _objectives);
            _endings = new EndingService(_state, _content);
            _world = new WorldService(_state, _events, _endings);

            _dispatcher.Register<CollectEvidenceCommand>(_evidence);
            _dispatcher.Register<ActivateObjectiveCommand>(_objectives);
            _dispatcher.Register<CompleteObjectiveCommand>(_objectives);
            _dispatcher.Register<GainInsightCommand>(_insights);
            _dispatcher.Register<UnlockDoorCommand>(_world);
            _dispatcher.Register<SetAlertCommand>(_world);
            _dispatcher.Register<SetLockdownCommand>(_world);
            _dispatcher.Register<StartBroadcastCommand>(_world);
            _dispatcher.Register<CompleteBroadcastCommand>(_world);
        }

        [Test]
        public void CollectEvidence_UpdatesState_AndPublishesEvent()
        {
            EvidenceCollectedEvent? got = null;
            _events.Subscribe<EvidenceCollectedEvent>(e => got = e);
            _dispatcher.Dispatch(new CollectEvidenceCommand("ev_a", "test"));
            Assert.IsTrue(_state.State.CollectedEvidence.Contains("ev_a"));
            Assert.IsTrue(got.HasValue);
        }

        [Test]
        public void CollectEvidence_UnknownId_FailsSafely()
        {
            _dispatcher.Dispatch(new CollectEvidenceCommand("does_not_exist", "test"));
            Assert.IsEmpty(_state.State.CollectedEvidence);
        }

        [Test]
        public void CollectEvidence_IsIdempotent()
        {
            _dispatcher.Dispatch(new CollectEvidenceCommand("ev_a"));
            _dispatcher.Dispatch(new CollectEvidenceCommand("ev_a"));
            Assert.AreEqual(1, _state.State.CollectedEvidence.Count);
        }

        [Test]
        public void Evidence_CompletesEvidenceDrivenObjective()
        {
            _dispatcher.Dispatch(new CollectEvidenceCommand("ev_a"));
            Assert.IsTrue(_state.State.CompletedObjectives.Contains("obj_needs_a"));
        }

        [Test]
        public void Insight_GrantedWhenPairCollected()
        {
            _dispatcher.Dispatch(new CollectEvidenceCommand("ev_a"));
            Assert.IsFalse(_state.State.GainedInsights.Contains("insight_ab"));
            _dispatcher.Dispatch(new CollectEvidenceCommand("ev_b"));
            Assert.IsTrue(_state.State.GainedInsights.Contains("insight_ab"));
        }

        [Test]
        public void Objective_RequiresPrerequisiteObjective()
        {
            // obj_locked requires obj_needs_a; can't activate early
            _dispatcher.Dispatch(new ActivateObjectiveCommand("obj_locked"));
            Assert.IsFalse(_state.State.ActiveObjectives.Contains("obj_locked"));
            _dispatcher.Dispatch(new CollectEvidenceCommand("ev_a")); // completes obj_needs_a → activates obj_locked
            Assert.IsTrue(_state.State.ActiveObjectives.Contains("obj_locked"));
        }

        [Test]
        public void UnlockDoor_PersistsInState()
        {
            _dispatcher.Dispatch(new UnlockDoorCommand("door_x", "test"));
            Assert.IsTrue(_state.State.UnlockedDoors.Contains("door_x"));
        }

        [Test]
        public void Commands_AreJournaled()
        {
            _dispatcher.Dispatch(new CollectEvidenceCommand("ev_a"));
            _dispatcher.Dispatch(new UnlockDoorCommand("door_x", "t"));
            var dump = _journal.Dump();
            StringAssert.Contains("COLLECT_EVIDENCE ev_a", dump);
            StringAssert.Contains("UNLOCK_DOOR door_x", dump);
        }

        [Test]
        public void Ending_Evaluation_IsDeterministic()
        {
            _dispatcher.Dispatch(new CollectEvidenceCommand("ev_a"));
            _dispatcher.Dispatch(new CollectEvidenceCommand("ev_b"));
            var r1 = _endings.Evaluate();
            var r2 = _endings.Evaluate();
            Assert.AreEqual(r1.Ending.Id, r2.Ending.Id);
            Assert.AreEqual("ending_best", r1.Ending.Id);
        }

        [Test]
        public void Ending_Fallback_WhenNothingCollected()
        {
            var r = _endings.Evaluate();
            Assert.AreEqual("ending_bad", r.Ending.Id);
        }

        // ---------- in-memory content ----------

        private class TestContent : IContentDatabase
        {
            public StealthTuning Tuning { get; } = ScriptableObject.CreateInstance<StealthTuning>();
            private readonly Dictionary<string, EvidenceDefinition> _ev = new();
            private readonly Dictionary<string, ObjectiveDefinition> _ob = new();
            private readonly Dictionary<string, InsightDefinition> _ins = new();
            private readonly Dictionary<string, EndingDefinition> _end = new();

            public TestContent()
            {
                var a = ScriptableObject.CreateInstance<EvidenceDefinition>();
                a.Id = "ev_a"; a.Title = "A"; a.Category = EvidenceCategory.Primary;
                var b = ScriptableObject.CreateInstance<EvidenceDefinition>();
                b.Id = "ev_b"; b.Title = "B"; b.Category = EvidenceCategory.Primary;
                _ev["ev_a"] = a; _ev["ev_b"] = b;

                var oa = ScriptableObject.CreateInstance<ObjectiveDefinition>();
                oa.Id = "obj_needs_a"; oa.Title = "Needs A";
                oa.RequiredEvidence = new[] { a };
                var ol = ScriptableObject.CreateInstance<ObjectiveDefinition>();
                ol.Id = "obj_locked"; ol.Title = "Locked";
                ol.RequiredObjectives = new[] { oa };
                _ob["obj_needs_a"] = oa; _ob["obj_locked"] = ol;

                var ins = ScriptableObject.CreateInstance<InsightDefinition>();
                ins.Id = "insight_ab"; ins.Title = "AB";
                ins.RequiredEvidence = new[] { a, b };
                _ins["insight_ab"] = ins;

                var bad = ScriptableObject.CreateInstance<EndingDefinition>();
                bad.Id = "ending_bad"; bad.Priority = 0;
                var best = ScriptableObject.CreateInstance<EndingDefinition>();
                best.Id = "ending_best"; best.Priority = 10;
                best.MinPrimaryEvidence = 2;
                best.RequiredInsights = new[] { ins };
                _end["ending_bad"] = bad; _end["ending_best"] = best;
            }

            public IReadOnlyCollection<EvidenceDefinition> Evidence => _ev.Values;
            public IReadOnlyCollection<ObjectiveDefinition> Objectives => _ob.Values;
            public IReadOnlyCollection<TerminalDefinition> Terminals => new List<TerminalDefinition>();
            public IReadOnlyCollection<EndingDefinition> Endings => _end.Values;
            public IReadOnlyCollection<InsightDefinition> Insights => _ins.Values;
            public IReadOnlyCollection<DocumentDefinition> Documents => new List<DocumentDefinition>();
            public bool TryGetEvidence(string id, out EvidenceDefinition d) => _ev.TryGetValue(id, out d);
            public bool TryGetObjective(string id, out ObjectiveDefinition d) => _ob.TryGetValue(id, out d);
            public bool TryGetTerminal(string id, out TerminalDefinition d) { d = null; return false; }
            public bool TryGetEnding(string id, out EndingDefinition d) => _end.TryGetValue(id, out d);
            public bool TryGetInsight(string id, out InsightDefinition d) => _ins.TryGetValue(id, out d);
            public bool TryGetDocument(string id, out DocumentDefinition d) { d = null; return false; }
            public bool IsKnownScene(string id) => id == "dock";
            public string SceneName(string id) => id == "dock" ? "Dock" : null;
        }
    }
}
