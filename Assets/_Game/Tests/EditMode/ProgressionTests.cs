using System.Collections.Generic;
using System.IO;
using Escape.Core;
using Escape.Data;
using NUnit.Framework;
using UnityEngine;

namespace Escape.Tests.EditMode
{
    /// <summary>
    /// Critical-path invariants: the checks that would have caught the
    /// irreversible softlocks (missable broadcast key, skippable signal
    /// routing) and the evidence-implies-completion semantic bug.
    /// These test game-level correctness, not single components.
    /// </summary>
    public class ProgressionTests
    {
        // ---------- objective semantics ----------

        [Test]
        public void ExplicitObjective_DoesNotComplete_FromEvidenceAlone()
        {
            var content = new TestContent();
            var events = new GameEventBus();
            var state = new GameStateService();
            var objectives = new ObjectiveService(state, content, events);
            var insights = new InsightService(state, content, events, objectives);
            var evidence = new EvidenceService(state, content, events, insights, objectives);
            var dispatcher = new GameCommandDispatcher(new CommandJournal(false));
            dispatcher.Register<CollectEvidenceCommand>(evidence);
            dispatcher.Register<CompleteObjectiveCommand>(objectives);

            dispatcher.Dispatch(new CollectEvidenceCommand("ev_key", "test"));

            var s = state.State;
            Assert.IsTrue(s.ActiveObjectives.Contains("obj_transmit"),
                "Explicit objective should activate when its prerequisites are met");
            Assert.IsFalse(s.CompletedObjectives.Contains("obj_transmit"),
                "Explicit objective completed from evidence alone — semantic regression");

            // A bare command must not finish it either: it belongs to an action.
            dispatcher.Dispatch(new CompleteObjectiveCommand("obj_transmit", "test"));
            Assert.IsFalse(s.CompletedObjectives.Contains("obj_transmit"),
                "An Explicit objective was completed by a bare command, not its action");

            objectives.CompleteFromAction("obj_transmit", "test");
            Assert.IsTrue(s.CompletedObjectives.Contains("obj_transmit"));
        }

        [Test]
        public void EvidenceObjective_StillCompletes_FromEvidence()
        {
            var content = new TestContent();
            var events = new GameEventBus();
            var state = new GameStateService();
            var objectives = new ObjectiveService(state, content, events);
            var insights = new InsightService(state, content, events, objectives);
            var evidence = new EvidenceService(state, content, events, insights, objectives);
            var dispatcher = new GameCommandDispatcher(new CommandJournal(false));
            dispatcher.Register<CollectEvidenceCommand>(evidence);

            dispatcher.Dispatch(new CollectEvidenceCommand("ev_key", "test"));
            Assert.IsTrue(state.State.CompletedObjectives.Contains("obj_pickup"),
                "Evidence-mode objective must still auto-complete");
        }

        // ---------- real content: the broadcast chain ----------

        [Test]
        public void BroadcastTruth_RequiresTransmission_NotJustKey()
        {
            var content = ContentDatabase.Load();
            var state = new GameStateService();
            var events = new GameEventBus();
            var objectives = new ObjectiveService(state, content, events);
            var insights = new InsightService(state, content, events, objectives);
            var evidence = new EvidenceService(state, content, events, insights, objectives);
            var dispatcher = new GameCommandDispatcher(new CommandJournal(false));
            dispatcher.Register<CollectEvidenceCommand>(evidence);
            dispatcher.Register<CompleteObjectiveCommand>(objectives);

            var s = state.State;
            // Drive the real prerequisite chain: routing objective done,
            // key in hand — the state a player reaches mid-tower.
            s.CompletedObjectives.Add("download_archive");
            dispatcher.Dispatch(new CompleteObjectiveCommand("route_broadcast", "test"));
            dispatcher.Dispatch(new CollectEvidenceCommand("broadcast_key_001", "test"));

            Assert.IsFalse(s.CompletedObjectives.Contains("broadcast_truth"),
                "broadcast_truth completed because the key was held — the pre-fix bug");
        }

        [Test]
        public void OfficeTerminal_Broadcast_IsGated_AndCompletesRouting()
        {
            var content = ContentDatabase.Load();
            Assert.IsTrue(content.TryGetTerminal("office_terminal", out var term));
            TerminalCommandDefinition broadcast = null;
            foreach (var c in term.Commands)
                if (c.Command == "BROADCAST") broadcast = c;
            Assert.IsNotNull(broadcast, "Office terminal lost its BROADCAST command");

            bool needsArchive = false;
            foreach (var o in broadcast.RequiredObjectives)
                if (o != null && o.Id == "download_archive") needsArchive = true;
            Assert.IsTrue(needsArchive,
                "BROADCAST must require download_archive — the routing step is ordered after the pull");

            bool needsKey = false;
            foreach (var e in broadcast.RequiredEvidence)
                if (e != null && e.Id == "broadcast_key_001") needsKey = true;
            Assert.IsTrue(needsKey, "BROADCAST must require the broadcast key");

            Assert.IsNotNull(broadcast.CompletesObjective,
                "BROADCAST must complete an objective");
            Assert.AreEqual("route_broadcast", broadcast.CompletesObjective.Id,
                "BROADCAST must complete route_broadcast — that is what the office exit checks");
        }

        [Test]
        public void BroadcastObjectives_AreExplicit_AndOrdered()
        {
            var content = ContentDatabase.Load();
            Assert.IsTrue(content.TryGetObjective("route_broadcast", out var route));
            Assert.IsTrue(content.TryGetObjective("broadcast_truth", out var truth));

            Assert.AreEqual(ObjectiveCompletionMode.Explicit, route.Completion,
                "route_broadcast must not complete from evidence");
            Assert.AreEqual(ObjectiveCompletionMode.Explicit, truth.Completion,
                "broadcast_truth must not complete from evidence");

            bool truthNeedsRoute = false;
            foreach (var o in truth.RequiredObjectives)
                if (o != null && o.Id == "route_broadcast") truthNeedsRoute = true;
            Assert.IsTrue(truthNeedsRoute,
                "broadcast_truth must chain after route_broadcast");
        }

        // ---------- generated scenes: the irreversible-exit guards ----------

        [Test]
        public void DockExit_RequiresBroadcastKey()
        {
            // The Dock → ServiceEntrance transition is the point of no
            // return for the broadcast key; it must refuse an empty hand.
            var doc = SceneDoc("Dock", "targetSceneId: service_entrance");
            StringAssert.Contains("requiredEvidenceId: broadcast_key_001", doc,
                "Dock exit no longer requires the broadcast key — softlock regression");
        }

        [Test]
        public void OfficeExit_RequiresRoutedBroadcast()
        {
            // MansionOffice → SecurityWing must not open on the archive pull
            // alone; the signal has to be routed (route_broadcast implies
            // download_archive because the terminal command is gated on it).
            var doc = SceneDoc("MansionOffice", "targetSceneId: security_wing");
            StringAssert.Contains("requiredObjectiveId: route_broadcast", doc,
                "Office exit no longer requires route_broadcast — softlock regression");
        }

        /// <summary>Returns the scene YAML document containing `marker`.</summary>
        private static string SceneDoc(string sceneName, string marker)
        {
            var path = Path.Combine(Application.dataPath, "_Game/Scenes", sceneName + ".unity");
            Assert.IsTrue(File.Exists(path), $"Scene missing: {path}");
            foreach (var doc in File.ReadAllText(path).Split(new[] { "--- !u!" },
                         System.StringSplitOptions.RemoveEmptyEntries))
                if (doc.Contains(marker)) return doc;
            Assert.Fail($"No document in {sceneName}.unity contains '{marker}'");
            return null;
        }

        // ---------- save invariants ----------

        [Test]
        public void MostRecentSlot_Finds_BackupOnly_Save()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ete_prog_saves_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var state = new GameStateService();
                state.State.SceneId = "dock";
                var saves = new SaveService(state, new TestContent(), new GameEventBus(),
                    new GameCommandDispatcher(new CommandJournal(false)), dir);
                Assert.IsTrue(saves.Save("slot1"));

                var primary = Path.Combine(dir, "slot1.json");
                File.Copy(primary, primary + ".bak");
                File.Delete(primary);

                Assert.IsTrue(saves.HasSave("slot1"), "Backup-only slot not seen by HasSave");
                Assert.AreEqual("slot1", saves.MostRecentSlot(),
                    "MostRecentSlot ignored a backup-only save — menu would hide a recoverable game");
                Assert.IsTrue(saves.LoadIntoState("slot1", out var errors), string.Join(";", errors));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void SlotInfo_Marks_MissingPrimary_AsRecoverable()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ete_slotinfo_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var state = new GameStateService();
                state.State.SceneId = "dock";
                var saves = new SaveService(state, new TestContent(), new GameEventBus(),
                    new GameCommandDispatcher(new CommandJournal(false)), dir);
                Assert.IsTrue(saves.Save("slot1"));

                var primary = Path.Combine(dir, "slot1.json");
                File.Copy(primary, primary + ".bak");
                File.Delete(primary); // primary missing, backup valid

                var info = saves.GetSlotInfo("slot1");
                Assert.IsTrue(info.Exists);
                Assert.IsTrue(info.Valid, "A valid backup should make the slot loadable");
                Assert.IsTrue(info.Recoverable,
                    "A missing primary with a valid backup is recoverable — the menu must not hide it");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void Validator_Repairs_ActiveCompletedOverlap()
        {
            var data = new SaveData
            {
                sceneId = "dock",
                completedObjectives = new List<string> { "obj_pickup" },
                activeObjectives = new List<string> { "obj_pickup", "obj_transmit" }
            };
            var r = SaveValidator.Validate(data, new TestContent());
            Assert.IsTrue(r.IsValid, string.Join(";", r.Errors));
            Assert.IsTrue(r.WasRepaired);
            Assert.IsFalse(data.activeObjectives.Contains("obj_pickup"),
                "Objective stayed in both active and completed");
            Assert.IsTrue(data.activeObjectives.Contains("obj_transmit"));
        }

        [Test]
        public void Validator_Repairs_BroadcastFlag_Inconsistency()
        {
            var data = new SaveData
            {
                sceneId = "dock",
                broadcastCompleted = true,
                broadcastStarted = false
            };
            var r = SaveValidator.Validate(data, new TestContent());
            Assert.IsTrue(r.IsValid, string.Join(";", r.Errors));
            Assert.IsTrue(data.broadcastStarted,
                "BroadcastCompleted without BroadcastStarted not repaired");
        }

        [Test]
        public void Validator_Clamps_NegativeLures()
        {
            var data = new SaveData { sceneId = "dock", lures = -3 };
            var r = SaveValidator.Validate(data, new TestContent());
            Assert.IsTrue(r.IsValid, string.Join(";", r.Errors));
            Assert.AreEqual(0, data.lures);
        }

        [Test]
        public void Validator_Drops_UnknownEnding()
        {
            var data = new SaveData { sceneId = "dock", endingId = "ending_bogus" };
            var r = SaveValidator.Validate(data, new TestContent());
            Assert.IsTrue(r.IsValid, string.Join(";", r.Errors));
            Assert.AreEqual("", data.endingId);
        }

        // ---------- domain invariants: the command layer enforces its own contract ----------

        /// <summary>Real content, wired the way GameRoot wires it.</summary>
        private static (GameStateService state, GameCommandDispatcher dispatcher) RealRig()
        {
            var content = ContentDatabase.Load();
            var state = new GameStateService();
            var events = new GameEventBus();
            var objectives = new ObjectiveService(state, content, events);
            var insights = new InsightService(state, content, events, objectives);
            var evidence = new EvidenceService(state, content, events, insights, objectives);
            var endings = new EndingService(state, content);
            var world = new WorldService(state, events, endings, objectives);
            var dispatcher = new GameCommandDispatcher(new CommandJournal(false));
            dispatcher.Register<CollectEvidenceCommand>(evidence);
            dispatcher.Register<CompleteObjectiveCommand>(objectives);
            dispatcher.Register<RouteBroadcastCommand>(world);
            dispatcher.Register<CompleteBroadcastCommand>(world);
            return (state, dispatcher);
        }

        [Test]
        public void Transmission_IsRefused_UntilTheRelayIsRouted()
        {
            var (state, dispatcher) = RealRig();
            // A corrupted save or a future caller could present this state:
            // the routing objective recorded complete, but no routed relay.
            state.State.CompletedObjectives.Add("download_archive");
            state.State.CompletedObjectives.Add("route_broadcast");

            dispatcher.Dispatch(new CompleteBroadcastCommand("broadcast_truth"));

            Assert.IsFalse(state.State.BroadcastCompleted,
                "The domain transmitted from an unrouted relay");
            Assert.IsFalse(state.State.CompletedObjectives.Contains("broadcast_truth"));
        }

        [Test]
        public void Routing_IsRefused_UntilItsPrerequisitesAreMet()
        {
            var (state, dispatcher) = RealRig();

            dispatcher.Dispatch(new RouteBroadcastCommand("route_broadcast", "test"));

            Assert.IsFalse(state.State.BroadcastStarted,
                "Routing must validate the routing objective's prerequisites, not trust the caller");
            Assert.IsFalse(state.State.CompletedObjectives.Contains("route_broadcast"));
        }

        [Test]
        public void ObjectiveCompletion_IsRefused_BeforeItsPrerequisites()
        {
            var content = ContentDatabase.Load();
            var state = new GameStateService();
            var events = new GameEventBus();
            var objectives = new ObjectiveService(state, content, events);

            // route_broadcast is Explicit, so this also has to go through the
            // action path — the point here is the prerequisite gate.
            objectives.CompleteFromAction("route_broadcast", "test");
            Assert.IsFalse(state.State.CompletedObjectives.Contains("route_broadcast"),
                "An objective completed before its prerequisite objectives");

            state.State.CompletedObjectives.Add("download_archive");
            objectives.CompleteFromAction("route_broadcast", "test");
            Assert.IsTrue(state.State.CompletedObjectives.Contains("route_broadcast"),
                "The objective should complete once its prerequisites hold");
        }

        [Test]
        public void Domain_CompletesTheTransmissionObjective_WithoutOutsideSequencing()
        {
            var (state, dispatcher) = RealRig();
            var s = state.State;
            s.CollectedEvidence.Add("broadcast_key_001");
            s.CompletedObjectives.Add("download_archive");

            dispatcher.Dispatch(new RouteBroadcastCommand("route_broadcast", "office_terminal"));
            Assert.IsTrue(s.BroadcastStarted, "Routing should start the relay");

            // Only the transmission command — nothing dispatches
            // CompleteObjectiveCommand for broadcast_truth.
            dispatcher.Dispatch(new CompleteBroadcastCommand("broadcast_truth"));

            Assert.IsTrue(s.CompletedObjectives.Contains("broadcast_truth"),
                "The domain must complete the transmission objective itself");
            Assert.IsTrue(s.BroadcastCompleted);
            Assert.IsNotEmpty(s.EndingId, "The transmission must evaluate an ending");
        }

        // ---------- save schema v3: pose validity is explicit ----------

        [Test]
        public void PoseAtWorldOrigin_IsARecordedPose()
        {
            // The v2 rule inferred "no pose" from a zero position, so a player
            // saved at the origin was dropped on a spawn point instead.
            var atOrigin = new PlayerSaveState { HasPose = true, Position = Vector3.zero };
            Assert.IsTrue(Escape.Gameplay.SceneBootstrap.ShouldRestorePose("", atOrigin),
                "A save at the world origin must be restored, not replaced by a spawn point");

            Assert.IsFalse(Escape.Gameplay.SceneBootstrap.ShouldRestorePose("", new PlayerSaveState()),
                "A save that never recorded a pose must take the spawn point");

            Assert.IsFalse(Escape.Gameplay.SceneBootstrap.ShouldRestorePose("default", atOrigin),
                "A named spawn always wins over the saved pose");
        }

        [Test]
        public void Migration_V2ToV3_RecordsPoseValidity()
        {
            var withPose = SaveMigrator.MigrateToCurrent(new SaveData
            {
                version = 2,
                sceneId = "dock",
                player = new PlayerSaveState { Position = new Vector3(2f, 0f, 3f) }
            });
            Assert.IsNotNull(withPose);
            Assert.AreEqual(SaveData.CurrentVersion, withPose.version);
            Assert.IsTrue(withPose.player.HasPose,
                "A v2 save with a position should migrate to a recorded pose");

            var withoutPose = SaveMigrator.MigrateToCurrent(new SaveData
            {
                version = 2,
                sceneId = "dock",
                player = new PlayerSaveState()
            });
            Assert.IsNotNull(withoutPose);
            Assert.IsFalse(withoutPose.player.HasPose,
                "A v2 save with no position stays 'no pose' under the old inference");
        }

        [Test]
        public void Validator_Repairs_PoseWithoutTheFlag()
        {
            var data = new SaveData
            {
                sceneId = "dock",
                player = new PlayerSaveState { HasPose = false, Position = new Vector3(1f, 0f, 1f) }
            };
            var r = SaveValidator.Validate(data, new TestContent());
            Assert.IsTrue(r.IsValid, string.Join(";", r.Errors));
            Assert.IsTrue(data.player.HasPose,
                "A position without HasPose should be repaired to a recorded pose");
        }

        // ---------- save invariants: broadcast coherence ----------

        [Test]
        public void Validator_Repairs_RoutingObjective_MissingBehindBroadcastFlag()
        {
            var data = new SaveData { sceneId = "dock", broadcastStarted = true };
            var r = SaveValidator.Validate(data, ContentDatabase.Load());
            Assert.IsTrue(r.IsValid, string.Join(";", r.Errors));
            Assert.IsTrue(data.completedObjectives.Contains("route_broadcast"),
                "BroadcastStarted implies the signal was routed — the objective should be restored");
        }

        [Test]
        public void Validator_Repairs_TransmissionObjective_MissingBehindCompletedFlag()
        {
            var data = new SaveData { sceneId = "dock", broadcastCompleted = true };
            var r = SaveValidator.Validate(data, ContentDatabase.Load());
            Assert.IsTrue(r.IsValid, string.Join(";", r.Errors));
            Assert.IsTrue(data.completedObjectives.Contains("route_broadcast"));
            Assert.IsTrue(data.completedObjectives.Contains("broadcast_truth"),
                "BroadcastCompleted implies an actual transmission");
        }

        [Test]
        public void Validator_DropsEnding_WithoutACompletedBroadcast()
        {
            var data = new SaveData { sceneId = "dock", endingId = "ending_bad" };
            var r = SaveValidator.Validate(data, ContentDatabase.Load());
            Assert.IsTrue(r.IsValid, string.Join(";", r.Errors));
            Assert.AreEqual("", data.endingId,
                "An ending without a transmission is not reachable and must be dropped");
        }

        [Test]
        public void Validator_KeepsEnding_WhenTheBroadcastCompleted()
        {
            var data = new SaveData
            {
                sceneId = "dock",
                broadcastStarted = true,
                broadcastCompleted = true,
                endingId = "ending_bad"
            };
            var r = SaveValidator.Validate(data, ContentDatabase.Load());
            Assert.IsTrue(r.IsValid, string.Join(";", r.Errors));
            Assert.AreEqual("ending_bad", data.endingId);
        }

        // ---------- world manifest: scene-authored ids are verifiable ----------

        [Test]
        public void Manifest_CoversEveryAuthoredWorldId()
        {
            var manifest = WorldManifest.Load();
            Assert.IsNotNull(manifest,
                "No world manifest — run Tools > Escape the Elites > Build All");

            var known = new HashSet<string>(manifest.doors ?? new string[0]);
            known.UnionWith(manifest.cameras ?? new string[0]);
            known.UnionWith(manifest.lures ?? new string[0]);

            int found = 0;
            foreach (var sceneId in new[] { SceneId.Dock, SceneId.ServiceEntrance,
                     SceneId.MansionOffice, SceneId.SecurityWing,
                     SceneId.BunkerServerRoom, SceneId.BroadcastTower })
            {
                var path = Path.Combine(Application.dataPath, "_Game/Scenes",
                    SceneName(sceneId) + ".unity");
                if (!File.Exists(path)) continue;
                var text = File.ReadAllText(path);
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(text,
                             @"propertyPath: (?:id|lureId)\r?\n\s*value: (\S+)"))
                {
                    var id = m.Groups[1].Value.Trim();
                    found++;
                    Assert.IsTrue(known.Contains(id),
                        $"'{id}' is authored in {SceneName(sceneId)} but missing from the world " +
                        "manifest — a save carrying it would be rejected as unknown");
                }
            }
            Assert.Greater(found, 0, "no authored world ids were found to check");
        }

        [Test]
        public void Validator_DropsUnknownDoorId_WhenManifestIsPresent()
        {
            var manifest = WorldManifest.Load();
            Assert.IsNotNull(manifest, "No world manifest — run Build All");

            var data = new SaveData { sceneId = "dock", unlockedDoors = new List<string> { "door_fabricated" } };
            var r = SaveValidator.Validate(data, ContentDatabase.Load(), manifest);
            Assert.IsTrue(r.IsValid, string.Join(";", r.Errors));
            Assert.IsFalse(data.unlockedDoors.Contains("door_fabricated"),
                "A door id that exists in no shipped scene must be dropped when a manifest is available");
        }

        [Test]
        public void Validator_KeepsSceneIds_WhenNoManifestIsAvailable()
        {
            // Degrade, don't reject: a project that has never generated has no
            // manifest, and the validator must not throw away every id.
            var data = new SaveData { sceneId = "dock", unlockedDoors = new List<string> { "door_fabricated" } };
            var r = SaveValidator.Validate(data, ContentDatabase.Load(), null);
            Assert.IsTrue(r.IsValid, string.Join(";", r.Errors));
            Assert.IsTrue(data.unlockedDoors.Contains("door_fabricated"));
        }

        private static string SceneName(string sceneId)
        {
            switch (sceneId)
            {
                case SceneId.Dock: return "Dock";
                case SceneId.ServiceEntrance: return "ServiceEntrance";
                case SceneId.MansionOffice: return "MansionOffice";
                case SceneId.SecurityWing: return "SecurityWing";
                case SceneId.BunkerServerRoom: return "BunkerServerRoom";
                case SceneId.BroadcastTower: return "BroadcastTower";
                default: return sceneId;
            }
        }

        // ---------- in-memory content ----------

        private sealed class TestContent : IContentDatabase
        {
            public StealthTuning Tuning { get; } = ScriptableObject.CreateInstance<StealthTuning>();
            private readonly Dictionary<string, EvidenceDefinition> _ev = new();
            private readonly Dictionary<string, ObjectiveDefinition> _ob = new();

            public TestContent()
            {
                var key = ScriptableObject.CreateInstance<EvidenceDefinition>();
                key.Id = "ev_key"; key.Title = "Key";
                _ev["ev_key"] = key;

                var pickup = ScriptableObject.CreateInstance<ObjectiveDefinition>();
                pickup.Id = "obj_pickup"; pickup.Title = "Pick up";
                pickup.RequiredEvidence = new[] { key };
                pickup.Completion = ObjectiveCompletionMode.Evidence;

                var transmit = ScriptableObject.CreateInstance<ObjectiveDefinition>();
                transmit.Id = "obj_transmit"; transmit.Title = "Transmit";
                transmit.RequiredEvidence = new[] { key };
                transmit.Completion = ObjectiveCompletionMode.Explicit;

                _ob["obj_pickup"] = pickup;
                _ob["obj_transmit"] = transmit;
            }

            public IReadOnlyCollection<EvidenceDefinition> Evidence => _ev.Values;
            public IReadOnlyCollection<ObjectiveDefinition> Objectives => _ob.Values;
            public IReadOnlyCollection<TerminalDefinition> Terminals => new List<TerminalDefinition>();
            public IReadOnlyCollection<EndingDefinition> Endings => new List<EndingDefinition>();
            public IReadOnlyCollection<InsightDefinition> Insights => new List<InsightDefinition>();
            public IReadOnlyCollection<DocumentDefinition> Documents => new List<DocumentDefinition>();
            public bool TryGetEvidence(string id, out EvidenceDefinition d) => _ev.TryGetValue(id, out d);
            public bool TryGetObjective(string id, out ObjectiveDefinition d) => _ob.TryGetValue(id, out d);
            public bool TryGetTerminal(string id, out TerminalDefinition d) { d = null; return false; }
            public bool TryGetEnding(string id, out EndingDefinition d) { d = null; return false; }
            public bool TryGetInsight(string id, out InsightDefinition d) { d = null; return false; }
            public bool TryGetDocument(string id, out DocumentDefinition d) { d = null; return false; }
            public bool IsKnownScene(string id) => id == "dock";
            public string SceneName(string id) => id == "dock" ? "Dock" : null;
        }
    }
}
