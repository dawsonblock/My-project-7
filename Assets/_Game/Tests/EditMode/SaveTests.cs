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
            // A real shipped door id: the validator now checks scene-authored
            // ids against the generated world manifest, so a fabricated one is
            // dropped (covered by ProgressionTests).
            _state.State.UnlockedDoors.Add("service_security_door");
            _state.State.SceneId = "dock";
            _state.State.Player.Position = new Vector3(1, 2, 3);
            Assert.IsTrue(_saves.Save("slot1"));

            _state.NewGame();
            Assert.IsTrue(_saves.LoadIntoState("slot1", out var errors), string.Join(";", errors));
            Assert.IsTrue(_state.State.CollectedEvidence.Contains("ev_a"));
            Assert.IsTrue(_state.State.UnlockedDoors.Contains("service_security_door"));
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
        public void Migrator_V1_To_V2_SplitsPose()
        {
            var data = new SaveData
            {
                version = 1,
                sceneId = "dock",
                player = new PlayerSaveState { EulerRotation = new Vector3(30, 90, 0) }
            };
            var migrated = SaveMigrator.MigrateToCurrent(data);
            Assert.IsNotNull(migrated);
            Assert.AreEqual(SaveData.CurrentVersion, migrated.version);
            Assert.AreEqual(90f, migrated.player.Yaw);
            Assert.AreEqual(30f, migrated.player.Pitch);
        }

        [Test]
        public void Save_RoundTrips_Yaw_Pitch_Flashlight()
        {
            _state.State.SceneId = "dock";
            _state.State.Player.Position = new Vector3(4, 0, 7);
            _state.State.Player.Yaw = 135f;
            _state.State.Player.Pitch = -20f;
            _state.State.Player.FlashlightOn = true;
            Assert.IsTrue(_saves.Save("slot1"));

            _state.NewGame();
            Assert.IsTrue(_saves.LoadIntoState("slot1", out var errors), string.Join(";", errors));
            Assert.AreEqual(135f, _state.State.Player.Yaw);
            Assert.AreEqual(-20f, _state.State.Player.Pitch);
            Assert.IsTrue(_state.State.Player.FlashlightOn);
        }

        [Test]
        public void Save_CorruptPrimary_FallsBack_ToBackup()
        {
            _state.State.SceneId = "dock";
            _state.State.CollectedEvidence.Add("ev_a");
            Assert.IsTrue(_saves.Save("slot1"));
            // Second save moves the first (ev_a) file to slot1.json.bak.
            _state.State.CollectedEvidence.Clear();
            Assert.IsTrue(_saves.Save("slot1"));
            File.WriteAllText(Path.Combine(_dir, "slot1.json"), "garbage{{{");
            _state.NewGame();
            Assert.IsTrue(_saves.LoadIntoState("slot1", out var errors), string.Join(";", errors));
            Assert.IsTrue(_state.State.CollectedEvidence.Contains("ev_a"),
                "Backup should restore the previous good save");
        }

        [Test]
        public void Save_CorruptPrimary_AndCorruptBackup_Fails()
        {
            _state.State.SceneId = "dock";
            Assert.IsTrue(_saves.Save("slot1"));
            File.WriteAllText(Path.Combine(_dir, "slot1.json"), "garbage");
            File.WriteAllText(Path.Combine(_dir, "slot1.json.bak"), "also garbage");
            Assert.IsFalse(_saves.LoadIntoState("slot1", out _));
        }

        [Test]
        public void Save_Rejects_FutureSchema()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "slot1.json"),
                "{\"schemaVersion\":99,\"payload\":{\"sceneId\":\"dock\"}}");
            Assert.IsFalse(_saves.LoadIntoState("slot1", out var errors));
            StringAssert.Contains("newer", string.Join(";", errors));
        }

        [Test]
        public void Save_Legacy_V1_File_Loads()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "slot1.json"),
                "{\"version\":1,\"sceneId\":\"dock\",\"player\":{\"Position\":{\"x\":1,\"y\":2,\"z\":3},\"EulerRotation\":{\"x\":15,\"y\":45,\"z\":0},\"FlashlightOn\":true},\"collectedEvidence\":[\"ev_a\"]}");
            Assert.IsTrue(_saves.LoadIntoState("slot1", out var errors), string.Join(";", errors));
            Assert.AreEqual(45f, _state.State.Player.Yaw);
            Assert.AreEqual(15f, _state.State.Player.Pitch);
            Assert.IsTrue(_state.State.Player.FlashlightOn);
            Assert.IsTrue(_state.State.CollectedEvidence.Contains("ev_a"));
        }

        [Test]
        public void Save_Canonicalizes_Duplicates_And_Empties()
        {
            _state.State.SceneId = "dock";
            _state.State.CollectedEvidence.Add("ev_a");
            _state.State.CollectedEvidence.Add("ev_a"); // duplicate
            _state.State.UnlockedDoors.Add("door_b");
            _state.State.UnlockedDoors.Add("door_a");
            _state.State.UnlockedDoors.Add("");      // empty
            _state.State.UnlockedDoors.Add(null);    // null
            Assert.IsTrue(_saves.Save("slot1"));

            // The file itself must be canonical — sorted, deduped.
            var env = JsonUtility.FromJson<SaveEnvelope>(
                File.ReadAllText(Path.Combine(_dir, "slot1.json")));
            Assert.AreEqual(1, env.payload.collectedEvidence.Count);
            Assert.AreEqual(new List<string> { "door_a", "door_b" }, env.payload.unlockedDoors);
        }

        [Test]
        public void Save_Rejects_SlotIdsThatArePaths()
        {
            // A slot id becomes a file name. The API must not accept a path,
            // even though ordinary gameplay only ever passes the shipped slots.
            foreach (var slot in new[] { "../escape", "..", "a/b", "/etc/passwd", "", "slot 1" })
                Assert.IsFalse(SaveService.IsValidSlot(slot),
                    $"'{slot}' must not be accepted as a slot id");

            foreach (var slot in new[] { "autosave", "slot1", "slot_2", "smoke_slot", "itest-slot" })
                Assert.IsTrue(SaveService.IsValidSlot(slot),
                    $"'{slot}' is a legitimate slot id and must be accepted");
        }

        [Test]
        public void Save_Refuses_AnInvalidSlot_WithoutTouchingDisk()
        {
            _state.State.SceneId = "dock";
            Assert.IsFalse(_saves.Save("../escape"), "an invalid slot must not be written");
            Assert.IsFalse(_saves.HasSave("../escape"));
            Assert.IsFalse(_saves.Delete("../escape"));
            Assert.IsFalse(_saves.LoadIntoState("../escape", out var errors));
            StringAssert.Contains("Invalid save slot", string.Join(";", errors));
            Assert.IsFalse(Directory.Exists(Path.Combine(Path.GetTempPath(), "escape")),
                "an invalid slot must not escape the saves directory");
        }

        [Test]
        public void SaveCoordinator_Captures_Participant_State()
        {
            var coordinator = new SaveCoordinator();
            var saves = new SaveService(_state, _content, new GameEventBus(),
                new GameCommandDispatcher(new CommandJournal(false)), _dir, coordinator);
            var fake = new FakeParticipant();
            coordinator.Register(fake);
            _state.State.SceneId = "dock";
            Assert.IsTrue(saves.Save("slot1"));
            Assert.IsTrue(fake.Captured, "Save must capture participant state");
        }

        private sealed class FakeParticipant : ISaveParticipant
        {
            public bool Captured;
            public void CaptureSaveState(GameState s) => Captured = true;
            public void RestoreSaveState(GameState s) { }
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
