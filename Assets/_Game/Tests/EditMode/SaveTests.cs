using System.Collections.Generic;
using System.IO;
using Escape.Core;
using Escape.Data;
using NUnit.Framework;
using UnityEngine;

namespace Escape.Tests.EditMode
{
    public class SaveTests
    {
        private string _dir;
        private GameStateService _state;
        private SaveService _saves;
        private TestContent _content;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ete_test_saves_" + System.Guid.NewGuid().ToString("N"));
            _state = new GameStateService();
            _content = new TestContent();
            var events = new GameEventBus();
            var dispatcher = new GameCommandDispatcher(new CommandJournal(false));
            _saves = new SaveService(_state, _content, events, dispatcher, _dir);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void SaveLoad_RoundTrips()
        {
            _state.State.CollectedEvidence.Add("ev_a");
            _state.State.UnlockedDoors.Add("door_x");
            _state.State.SceneId = "dock";
            _state.State.Player.Position = new Vector3(1, 2, 3);
            Assert.IsTrue(_saves.Save("slot1"));

            _state.NewGame();
            Assert.IsTrue(_saves.LoadIntoState("slot1", out var errors), string.Join(";", errors));
            Assert.IsTrue(_state.State.CollectedEvidence.Contains("ev_a"));
            Assert.IsTrue(_state.State.UnlockedDoors.Contains("door_x"));
            Assert.AreEqual(new Vector3(1, 2, 3), _state.State.Player.Position);
        }

        [Test]
        public void SaveLoad_RoundTrips_LureState()
        {
            _state.State.SceneId = "dock";
            _state.State.Lures = 5;
            _state.State.CollectedLures.Add("lure_dock_0");
            Assert.IsTrue(_saves.Save("slot1"));

            _state.NewGame();
            Assert.AreEqual(3, _state.State.Lures); // default
            Assert.IsTrue(_saves.LoadIntoState("slot1", out var errors), string.Join(";", errors));
            Assert.AreEqual(5, _state.State.Lures);
            Assert.IsTrue(_state.State.CollectedLures.Contains("lure_dock_0"));
        }

        [Test]
        public void Save_Rejects_UnknownScene()
        {
            _state.State.SceneId = "nowhere";
            _saves.Save("slot1");
            _state.NewGame();
            Assert.IsFalse(_saves.LoadIntoState("slot1", out var errors));
            StringAssert.Contains("scene", string.Join(";", errors).ToLower());
        }

        [Test]
        public void Save_Drops_UnknownEvidenceIds()
        {
            _state.State.SceneId = "dock";
            _state.State.CollectedEvidence.Add("ev_a");
            _state.State.CollectedEvidence.Add("bogus_evidence");
            _saves.Save("slot1");
            _state.NewGame();
            Assert.IsTrue(_saves.LoadIntoState("slot1", out _));
            Assert.IsTrue(_state.State.CollectedEvidence.Contains("ev_a"));
            Assert.IsFalse(_state.State.CollectedEvidence.Contains("bogus_evidence"));
        }

        [Test]
        public void Save_Rejects_NonFinitePosition()
        {
            _state.State.SceneId = "dock";
            _state.State.Player.Position = new Vector3(float.NaN, 0, 0);
            // NaN won't survive JSON; write raw to simulate corruption.
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "slot1.json"),
                "{\"version\":1,\"sceneId\":\"dock\",\"player\":{\"Position\":{\"x\":1e40,\"y\":0,\"z\":0}}}");
            _state.NewGame();
            Assert.IsFalse(_saves.LoadIntoState("slot1", out _));
        }

        [Test]
        public void Migrator_NoMigrations_KeepsV1()
        {
            var data = new SaveData { version = 1, sceneId = "dock" };
            Assert.IsNotNull(SaveMigrator.MigrateToCurrent(data));
        }

        private class TestContent : IContentDatabase
        {
            public StealthTuning Tuning => ScriptableObject.CreateInstance<StealthTuning>();
            public IReadOnlyCollection<EvidenceDefinition> Evidence => new List<EvidenceDefinition>();
            public IReadOnlyCollection<ObjectiveDefinition> Objectives => new List<ObjectiveDefinition>();
            public IReadOnlyCollection<TerminalDefinition> Terminals => new List<TerminalDefinition>();
            public IReadOnlyCollection<EndingDefinition> Endings => new List<EndingDefinition>();
            public IReadOnlyCollection<InsightDefinition> Insights => new List<InsightDefinition>();
            public IReadOnlyCollection<DocumentDefinition> Documents => new List<DocumentDefinition>();
            public bool TryGetEvidence(string id, out EvidenceDefinition d)
            {
                d = id == "ev_a" ? ScriptableObject.CreateInstance<EvidenceDefinition>() : null;
                return d != null;
            }
            public bool TryGetObjective(string id, out ObjectiveDefinition d) { d = null; return false; }
            public bool TryGetTerminal(string id, out TerminalDefinition d) { d = null; return false; }
            public bool TryGetEnding(string id, out EndingDefinition d) { d = null; return false; }
            public bool TryGetInsight(string id, out InsightDefinition d) { d = null; return false; }
            public bool TryGetDocument(string id, out DocumentDefinition d) { d = null; return false; }
            public bool IsKnownScene(string id) => id == "dock";
            public string SceneName(string id) => "Dock";
        }
    }
}
