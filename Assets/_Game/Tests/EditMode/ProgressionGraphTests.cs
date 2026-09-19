using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Escape.Core;
using Escape.Data;
using NUnit.Framework;
using UnityEngine;

namespace Escape.Tests.EditMode
{
    /// <summary>
    /// Graph-level progression validation. Where ProgressionTests pins
    /// individual contracts, this walks the whole shipped chain and proves it
    /// is completable: every scene reachable, every gate satisfiable with
    /// content available at or before it, and a real ending reachable.
    ///
    /// It reads the generated scenes and definitions rather than a hand-kept
    /// model, so a content change that re-opens a softlock fails here instead
    /// of in a playtest.
    /// </summary>
    public class ProgressionGraphTests
    {
        private static readonly string[] CriticalPath =
        {
            SceneId.Dock, SceneId.ServiceEntrance, SceneId.MansionOffice,
            SceneId.SecurityWing, SceneId.BunkerServerRoom, SceneId.BroadcastTower
        };

        private IContentDatabase _content;
        private Dictionary<string, SceneFacts> _scenes;

        [SetUp]
        public void Setup()
        {
            _content = ContentDatabase.Load();
            var guids = GuidToId();
            _scenes = new Dictionary<string, SceneFacts>();
            foreach (var id in CriticalPath) _scenes[id] = SceneFacts.Read(id, guids);
        }

        // ---------- scene graph ----------

        [Test]
        public void EveryTransition_TargetsKnownScene()
        {
            foreach (var scene in _scenes.Values)
                foreach (var gate in scene.Gates)
                    Assert.IsTrue(_content.IsKnownScene(gate.Target),
                        $"{scene.Id}: transition targets unknown scene '{gate.Target}'");
        }

        [Test]
        public void CriticalPath_IsConnectedFromDock()
        {
            var seen = new HashSet<string> { SceneId.Dock };
            var queue = new Queue<string>();
            queue.Enqueue(SceneId.Dock);
            while (queue.Count > 0)
                foreach (var gate in _scenes[queue.Dequeue()].Gates)
                    if (_scenes.ContainsKey(gate.Target) && seen.Add(gate.Target))
                        queue.Enqueue(gate.Target);

            foreach (var id in CriticalPath)
                Assert.IsTrue(seen.Contains(id), $"Scene '{id}' is unreachable from the Dock");
        }

        [Test]
        public void EveryGate_RequirementNamesRealContent()
        {
            foreach (var scene in _scenes.Values)
                foreach (var gate in scene.Gates)
                {
                    if (!string.IsNullOrEmpty(gate.RequiredEvidence))
                        Assert.IsTrue(_content.TryGetEvidence(gate.RequiredEvidence, out _),
                            $"{scene.Id}: gate requires unknown evidence '{gate.RequiredEvidence}'");
                    if (!string.IsNullOrEmpty(gate.RequiredObjective))
                        Assert.IsTrue(_content.TryGetObjective(gate.RequiredObjective, out _),
                            $"{scene.Id}: gate requires unknown objective '{gate.RequiredObjective}'");
                }
        }

        // ---------- content reachability ----------

        [Test]
        public void ObjectiveRequirements_Resolve_And_EvidenceIsGranted()
        {
            var granted = GrantableEvidence();
            foreach (var obj in _content.Objectives)
            {
                foreach (var req in obj.RequiredObjectives)
                    Assert.IsNotNull(req, $"{obj.Id}: null required objective");
                foreach (var req in obj.RequiredEvidence)
                {
                    Assert.IsNotNull(req, $"{obj.Id}: null required evidence");
                    Assert.IsTrue(granted.Contains(req.Id),
                        $"{obj.Id} requires evidence '{req.Id}' that no scene or terminal grants — unobtainable");
                }
            }
        }

        [Test]
        public void TerminalRequirements_Resolve()
        {
            foreach (var term in _content.Terminals)
                foreach (var cmd in term.Commands)
                {
                    foreach (var e in cmd.RequiredEvidence)
                        Assert.IsNotNull(e, $"{term.Id}:{cmd.Command}: null required evidence");
                    foreach (var o in cmd.RequiredObjectives)
                        Assert.IsNotNull(o, $"{term.Id}:{cmd.Command}: null required objective");
                    if (cmd.CompletesObjective != null)
                        Assert.IsTrue(_content.TryGetObjective(cmd.CompletesObjective.Id, out _),
                            $"{term.Id}:{cmd.Command} completes unknown objective '{cmd.CompletesObjective.Id}'");
                }
        }

        [Test]
        public void TerminalUnlockCodes_AreDiscoverableInOrBeforeTheirScene()
        {
            for (int i = 0; i < CriticalPath.Length; i++)
            {
                var facts = _scenes[CriticalPath[i]];
                var docs = new List<string>();
                for (int j = 0; j <= i; j++)
                    foreach (var d in _scenes[CriticalPath[j]].Documents)
                        docs.Add(DefinitionText("Documents", d));

                foreach (var termId in facts.Terminals)
                {
                    if (!_content.TryGetTerminal(termId, out var term)) continue;
                    if (string.IsNullOrEmpty(term.UnlockCode)) continue;
                    bool found = false;
                    foreach (var body in docs)
                        if (body != null && body.Contains(term.UnlockCode)) { found = true; break; }
                    Assert.IsTrue(found,
                        $"Terminal '{termId}' in {facts.Id} needs code '{term.UnlockCode}', " +
                        "but no document at or before that scene reveals it — unpassable gate");
                }
            }
        }

        /// <summary>
        /// Choice-aware reachability, restricted to the failure that actually
        /// strands a player: an item the critical path needs *later* that is
        /// only obtainable up to some scene, where no gate forces the player to
        /// have taken it. The maximal-path walk cannot see this, because it
        /// collects everything by construction.
        ///
        /// For each gate: every required item whose last obtainable scene is at
        /// or before the gate must already be enforced by that point — either
        /// required by a gate the player passed, or completed by a scene entry.
        /// Otherwise the player can walk through and be stuck.
        ///
        /// Optional objectives and endings are deliberately excluded: a secret
        /// ending and a missable collectible are allowed to be missable.
        ///
        /// LIMITATION — this model reads transition requirements, not physical
        /// gating. A scene whose progress is blocked by a locked interior door
        /// (e.g. ServiceEntrance, where the exit sits behind
        /// service_security_door) can be passed in the model but not in the
        /// game, so asserting reachability there would be unsound. Those
        /// scenes are skipped and counted rather than silently mis-asserted.
        /// </summary>
        [Test]
        public void NoIrreversibleEdge_StrandsARequirementNeededLater()
        {
            var required = RequiredByCriticalContent();
            var lastObtainable = new Dictionary<string, int>();
            for (int i = 0; i < CriticalPath.Length; i++)
                foreach (var item in ObtainableAt(i))
                    lastObtainable[item] = i;

            int skipped = 0, checkedGates = 0;
            var enforced = new HashSet<string>();
            for (int i = 0; i < CriticalPath.Length; i++)
            {
                var scene = _scenes[CriticalPath[i]];
                // What the player is forced to hold by the time they stand at
                // this scene's exits.
                foreach (var gate in scene.Gates)
                {
                    if (!string.IsNullOrEmpty(gate.RequiredEvidence)) enforced.Add(gate.RequiredEvidence);
                    if (!string.IsNullOrEmpty(gate.RequiredObjective))
                        AddClosure(gate.RequiredObjective, enforced);
                    if (!string.IsNullOrEmpty(gate.CompletesObjective))
                        AddClosure(gate.CompletesObjective, enforced);
                }
                foreach (var done in scene.BootstrapCompletes)
                    AddClosure(done, enforced);

                if (scene.Gates.Count == 0) continue; // no exit: nothing to pass
                if (scene.HasDoor) { skipped++; continue; } // physically gated: model unsound here
                checkedGates++;

                foreach (var item in required)
                {
                    if (!lastObtainable.TryGetValue(item, out var last) || last > i) continue;
                    Assert.IsTrue(enforced.Contains(item),
                        $"{scene.Id} can be left without '{item}': it is required by the critical " +
                        $"path, last obtainable at {CriticalPath[last]}, and no gate enforces it — " +
                        "a player who skipped it is stranded");
                }
            }

            Assert.Greater(checkedGates, 0, "no scene was reachable to check");
            // Surfaced rather than hidden: if this grows, the model is covering
            // less of the path than the gate list suggests.
            Assert.Less(skipped, CriticalPath.Length,
                "every scene has a door, so this check asserts nothing");
        }

        /// <summary>
        /// Authored persistent ids must be unique across the shipped scenes.
        /// The runtime registry keys by id, so a duplicate would make one
        /// object's state unreachable — and the id sets a save carries are
        /// only meaningful if the ids are.
        /// </summary>
        [Test]
        public void AuthoredWorldObjectIds_AreUniqueAcrossScenes()
        {
            var seen = new Dictionary<string, string>(); // id -> scene
            foreach (var sceneId in CriticalPath)
            {
                var path = Path.Combine(Application.dataPath, "_Game/Scenes", FileName(sceneId) + ".unity");
                var text = File.ReadAllText(path);
                foreach (var id in AuthoredIds(text))
                {
                    Assert.IsFalse(seen.TryGetValue(id, out var other),
                        $"'{id}' is authored in both {other} and {sceneId} — the runtime registry " +
                        "keys by id and would silently drop one");
                    seen[id] = sceneId;
                }
            }
            Assert.Greater(seen.Count, 0, "no authored world ids were found to check");
        }

        /// <summary>
        /// Ids of scene-authored persistent objects. Only the fields that
        /// define identity count: DoorController/SecurityCamera `id` and
        /// ThrowableLure `lureId`. `sourceId` is a cross-reference on a sibling
        /// component carrying the same string, not a second identity.
        /// </summary>
        private static IEnumerable<string> AuthoredIds(string sceneText)
        {
            foreach (Match m in Regex.Matches(sceneText,
                         @"propertyPath: (?:id|lureId)\r?\n\s*value: (\S+)"))
            {
                var id = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(id)) yield return id;
            }
        }

        /// <summary>Items the non-optional critical path depends on, transitively.</summary>
        private HashSet<string> RequiredByCriticalContent()
        {
            var required = new HashSet<string>();
            foreach (var obj in _content.Objectives)
                if (!obj.Optional) AddClosure(obj.Id, required);
            foreach (var gate in _scenes.Values.SelectMany(s => s.Gates))
            {
                if (!string.IsNullOrEmpty(gate.RequiredEvidence)) required.Add(gate.RequiredEvidence);
                if (!string.IsNullOrEmpty(gate.RequiredObjective)) AddClosure(gate.RequiredObjective, required);
            }
            return required;
        }

        /// <summary>Objective plus everything it depends on, transitively.</summary>
        private void AddClosure(string objectiveId, HashSet<string> into)
        {
            if (!into.Add(objectiveId)) return;
            if (!_content.TryGetObjective(objectiveId, out var def)) return;
            foreach (var e in def.RequiredEvidence)
                if (e != null) into.Add(e.Id);
            foreach (var o in def.RequiredObjectives)
                if (o != null) AddClosure(o.Id, into);
        }

        /// <summary>Everything a player could pick up or finish in one scene.</summary>
        private HashSet<string> ObtainableAt(int sceneIndex)
        {
            var facts = _scenes[CriticalPath[sceneIndex]];
            var items = new HashSet<string>();
            foreach (var ev in facts.Evidence) items.Add(ev);
            foreach (var docId in facts.Documents)
                if (_content.TryGetDocument(docId, out var doc) && doc.GrantsEvidence != null)
                    items.Add(doc.GrantsEvidence.Id);
            foreach (var termId in facts.Terminals)
            {
                if (!_content.TryGetTerminal(termId, out var term)) continue;
                foreach (var cmd in term.Commands)
                {
                    if (cmd.Action == TerminalActionType.CollectEvidence && !string.IsNullOrEmpty(cmd.TargetId))
                        items.Add(cmd.TargetId);
                    if (cmd.CompletesObjective != null) items.Add(cmd.CompletesObjective.Id);
                }
            }
            foreach (var done in facts.BootstrapCompletes) items.Add(done);
            return items;
        }

        // ---------- the end-to-end walk ----------

        [Test]
        public void EverySceneGate_IsSatisfiable_ByTheTimeThePlayerArrives()
        {
            var sim = new Progression(_content, _scenes);
            sim.Walk(CriticalPath);

            for (int i = 0; i < CriticalPath.Length; i++)
            {
                var scene = _scenes[CriticalPath[i]];
                foreach (var gate in scene.Gates)
                {
                    if (!string.IsNullOrEmpty(gate.RequiredEvidence))
                        Assert.IsTrue(sim.EvidenceAfterScene[i].Contains(gate.RequiredEvidence),
                            $"{scene.Id} gate needs evidence '{gate.RequiredEvidence}' that is not " +
                            "obtainable at or before this scene — softlock");
                    if (!string.IsNullOrEmpty(gate.RequiredObjective))
                        Assert.IsTrue(sim.CompletedAfterScene[i].Contains(gate.RequiredObjective),
                            $"{scene.Id} gate needs objective '{gate.RequiredObjective}' that is not " +
                            "completable at or before this scene — softlock");
                }
            }
        }

        [Test]
        public void CriticalPath_CompletesBroadcastTruth_AndTransmits()
        {
            var sim = new Progression(_content, _scenes);
            sim.Walk(CriticalPath);

            Assert.IsTrue(sim.State.CompletedObjectives.Contains("broadcast_truth"),
                "broadcast_truth did not complete — the transmission is unreachable");
            Assert.IsTrue(sim.State.BroadcastCompleted,
                "BroadcastCompleted never set on the critical path");
        }

        [Test]
        public void CriticalPath_CompletesEveryRequiredObjective()
        {
            var sim = new Progression(_content, _scenes);
            sim.Walk(CriticalPath);

            foreach (var obj in _content.Objectives)
            {
                if (obj.Optional) continue;
                Assert.IsTrue(sim.State.CompletedObjectives.Contains(obj.Id),
                    $"Required objective '{obj.Id}' is not completable on the critical path");
            }
        }

        [Test]
        public void CriticalPath_EarnsBetterThanTheFallbackEnding()
        {
            var sim = new Progression(_content, _scenes);
            sim.Walk(CriticalPath);

            var result = EndingEvaluator.Evaluate(sim.State, _content);
            Assert.IsNotNull(result.Ending,
                "No ending qualified at all — the fallback should always match");
            Assert.Greater(result.Ending.Priority, 0,
                $"Critical path produced only the priority-0 fallback ending '{result.Ending.Id}' — " +
                "the evidence gathered along the way is not counting toward the ending");
            Assert.IsNotEmpty(sim.State.EndingId);
        }

        // ---------- simulation ----------

        /// <summary>
        /// Replays the critical path through the real services: collect what
        /// each scene places, run every terminal command whose requirements
        /// are met, let evidence-mode objectives auto-complete, then take the
        /// scene's exit gate. Records per-scene snapshots so gate
        /// satisfiability can be asserted at the point of arrival.
        /// </summary>
        private sealed class Progression
        {
            private readonly IContentDatabase _content;
            private readonly Dictionary<string, SceneFacts> _scenes;
            private readonly GameStateService _state = new GameStateService();
            private readonly GameCommandDispatcher _dispatcher;
            private readonly ObjectiveService _objectives;
            private readonly string _consoleObjective;

            public readonly List<HashSet<string>> CompletedAfterScene = new List<HashSet<string>>();
            public readonly List<HashSet<string>> EvidenceAfterScene = new List<HashSet<string>>();

            public GameState State => _state.State;

            public Progression(IContentDatabase content, Dictionary<string, SceneFacts> scenes)
            {
                _content = content;
                _scenes = scenes;
                _consoleObjective = BroadcastConsoleObjective();

                var events = new GameEventBus();
                _objectives = new ObjectiveService(_state, content, events);
                var insights = new InsightService(_state, content, events, _objectives);
                var evidence = new EvidenceService(_state, content, events, insights, _objectives);
                var world = new WorldService(_state, events, new EndingService(_state, content), _objectives);

                _dispatcher = new GameCommandDispatcher(new CommandJournal(false));
                _dispatcher.Register<CollectEvidenceCommand>(evidence);
                _dispatcher.Register<CompleteObjectiveCommand>(_objectives);
                _dispatcher.Register<UnlockDoorCommand>(world);
                _dispatcher.Register<DisableCameraCommand>(world);
                _dispatcher.Register<SetLockdownCommand>(world);
                _dispatcher.Register<RouteBroadcastCommand>(world);
                _dispatcher.Register<CompleteBroadcastCommand>(world);
            }

            public void Walk(IEnumerable<string> path)
            {
                foreach (var sceneId in path)
                {
                    var facts = _scenes[sceneId];
                    foreach (var ev in facts.Evidence) Collect(ev);
                    foreach (var docId in facts.Documents) CollectFromDocument(docId);
                    foreach (var o in facts.BootstrapCompletes) Complete(o);
                    _objectives.EvaluateProgress();

                    RunTerminals(facts);
                    _objectives.EvaluateProgress();

                    CompletedAfterScene.Add(new HashSet<string>(State.CompletedObjectives));
                    EvidenceAfterScene.Add(new HashSet<string>(State.CollectedEvidence));

                    foreach (var gate in facts.Gates)
                        if (GateOpen(gate)) Complete(gate.CompletesObjective);
                }

                // The relay console: transmitting needs the routed signal, and
                // the domain completes the transmission objective itself.
                if (State.BroadcastStarted && !State.BroadcastCompleted)
                    _dispatcher.Dispatch(new CompleteBroadcastCommand(_consoleObjective));
            }

            private void RunTerminals(SceneFacts facts)
            {
                foreach (var termId in facts.Terminals)
                {
                    if (!_content.TryGetTerminal(termId, out var term)) continue;
                    if (term.RequiredObjective != null &&
                        !State.CompletedObjectives.Contains(term.RequiredObjective.Id)) continue;

                    var ran = new HashSet<string>();
                    bool changed = true;
                    int guard = 0;
                    while (changed && guard++ < 8)
                    {
                        changed = false;
                        foreach (var cmd in term.Commands)
                        {
                            var key = termId + ":" + cmd.Command;
                            if (ran.Contains(key) || !RequirementsMet(cmd)) continue;
                            Apply(term, cmd);
                            ran.Add(key);
                            changed = true;
                            _objectives.EvaluateProgress();
                        }
                    }
                }
            }

            private bool RequirementsMet(TerminalCommandDefinition cmd)
            {
                foreach (var e in cmd.RequiredEvidence)
                    if (e != null && !State.CollectedEvidence.Contains(e.Id)) return false;
                foreach (var o in cmd.RequiredObjectives)
                    if (o != null && !State.CompletedObjectives.Contains(o.Id)) return false;
                foreach (var i in cmd.RequiredInsights)
                    if (i != null && !State.GainedInsights.Contains(i.Id)) return false;
                return true;
            }

            private void Apply(TerminalDefinition term, TerminalCommandDefinition cmd)
            {
                switch (cmd.Action)
                {
                    case TerminalActionType.CollectEvidence: Collect(cmd.TargetId); break;
                    case TerminalActionType.CompleteObjective: Complete(cmd.TargetId); break;
                    case TerminalActionType.UnlockDoor:
                        _dispatcher.Dispatch(new UnlockDoorCommand(cmd.TargetId, term.Id)); break;
                    case TerminalActionType.DisableCamera:
                        _dispatcher.Dispatch(new DisableCameraCommand(cmd.TargetId, term.Id)); break;
                    case TerminalActionType.SetLockdown:
                        _dispatcher.Dispatch(new SetLockdownCommand(true, term.Id)); break;
                    case TerminalActionType.StartBroadcast:
                        _dispatcher.Dispatch(new RouteBroadcastCommand(
                            cmd.CompletesObjective != null ? cmd.CompletesObjective.Id : "",
                            term.Id)); break;
                }
                if (cmd.CompletesObjective != null) Complete(cmd.CompletesObjective.Id);
            }

            private bool GateOpen(SceneFacts.Gate gate)
            {
                if (!string.IsNullOrEmpty(gate.RequiredEvidence) &&
                    !State.CollectedEvidence.Contains(gate.RequiredEvidence)) return false;
                if (!string.IsNullOrEmpty(gate.RequiredObjective) &&
                    !State.CompletedObjectives.Contains(gate.RequiredObjective)) return false;
                return true;
            }

            private void Collect(string id)
            {
                if (!string.IsNullOrEmpty(id))
                    _dispatcher.Dispatch(new CollectEvidenceCommand(id, "sim"));
            }

            /// <summary>Reading a document grants its linked evidence.</summary>
            private void CollectFromDocument(string documentId)
            {
                if (_content.TryGetDocument(documentId, out var doc) && doc.GrantsEvidence != null)
                    Collect(doc.GrantsEvidence.Id);
            }

            private void Complete(string id)
            {
                if (!string.IsNullOrEmpty(id))
                    _dispatcher.Dispatch(new CompleteObjectiveCommand(id, "sim"));
            }

            /// <summary>The objective the tower console completes, read from the prefab.</summary>
            private static string BroadcastConsoleObjective()
            {
                var path = Path.Combine(Application.dataPath,
                    "_Game/Prefabs/Gameplay/BroadcastConsole.prefab");
                if (!File.Exists(path)) return "";
                var m = Regex.Match(File.ReadAllText(path), @"^\s*completesObjectiveId:\s*(\S+)\s*$",
                    RegexOptions.Multiline);
                return m.Success ? m.Groups[1].Value : "";
            }
        }

        // ---------- generated-content readers ----------

        private sealed class SceneFacts
        {
            public string Id;
            public readonly List<string> Evidence = new List<string>();
            public readonly List<string> Terminals = new List<string>();
            public readonly List<string> Documents = new List<string>();
            public readonly List<string> BootstrapCompletes = new List<string>();
            public readonly List<Gate> Gates = new List<Gate>();
            /// <summary>
            /// The scene contains a DoorController, so progress may be blocked
            /// physically rather than by a transition requirement — the static
            /// model cannot reason about reachability there.
            /// </summary>
            public bool HasDoor;

            public sealed class Gate
            {
                public string Target, RequiredEvidence, RequiredObjective, CompletesObjective;
            }

            public static SceneFacts Read(string sceneId, Dictionary<string, string> guids)
            {
                var f = new SceneFacts { Id = sceneId };
                var path = Path.Combine(Application.dataPath, "_Game/Scenes", FileName(sceneId) + ".unity");
                var text = File.ReadAllText(path);

                // Doors are scene-authored and block progress physically;
                // DoorController is the only component setting `requirement`.
                f.HasDoor = text.Contains("propertyPath: requirement");

                // Interactables are prefab instances — their definition
                // references live in m_Modifications as objectReference guids.
                var refs = new Regex(
                    @"propertyPath: (evidence|terminal|document)\s*\r?\n\s*value:.*\r?\n\s*objectReference: \{fileID: \d+, guid: ([0-9a-f]+)");
                foreach (Match m in refs.Matches(text))
                {
                    if (!guids.TryGetValue(m.Groups[2].Value, out var id)) continue;
                    switch (m.Groups[1].Value)
                    {
                        case "evidence": f.Evidence.Add(id); break;
                        case "terminal": f.Terminals.Add(id); break;
                        case "document": f.Documents.Add(id); break;
                    }
                }

                foreach (var doc in text.Split(new[] { "--- !u!" }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    if (doc.Contains("targetSceneId:"))
                        f.Gates.Add(new Gate
                        {
                            Target = Field(doc, "targetSceneId"),
                            RequiredEvidence = Field(doc, "requiredEvidenceId"),
                            RequiredObjective = Field(doc, "requiredObjectiveId"),
                            CompletesObjective = Field(doc, "completesObjectiveId")
                        });
                    else if (doc.Contains("firstObjectiveId:") || doc.Contains("completesObjectiveId:"))
                    {
                        foreach (var v in new[] { Field(doc, "completesObjectiveId"), Field(doc, "firstObjectiveId") })
                            if (!string.IsNullOrEmpty(v)) f.BootstrapCompletes.Add(v);
                    }
                }
                return f;
            }

            private static string Field(string doc, string key)
            {
                foreach (var line in doc.Split('\n'))
                {
                    var t = line.Trim();
                    if (t.StartsWith(key + ":")) return t.Substring(key.Length + 1).Trim();
                }
                return "";
            }
        }

        private static string FileName(string sceneId)
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

        /// <summary>Every evidence id the shipped content can actually hand the player.</summary>
        private HashSet<string> GrantableEvidence()
        {
            var granted = new HashSet<string>();
            foreach (var scene in _scenes.Values)
                foreach (var e in scene.Evidence) granted.Add(e);
            foreach (var term in _content.Terminals)
                foreach (var cmd in term.Commands)
                    if (cmd.Action == TerminalActionType.CollectEvidence && !string.IsNullOrEmpty(cmd.TargetId))
                        granted.Add(cmd.TargetId);
            foreach (var doc in _content.Documents)
                if (doc.GrantsEvidence != null) granted.Add(doc.GrantsEvidence.Id);
            return granted;
        }

        private static Dictionary<string, string> _guidToId;

        /// <summary>guid → definition Id, read straight from the asset meta files.</summary>
        private static Dictionary<string, string> GuidToId()
        {
            if (_guidToId != null) return _guidToId;
            _guidToId = new Dictionary<string, string>();
            var root = Path.Combine(Application.dataPath, "_Game/Resources/Definitions");
            foreach (var meta in Directory.GetFiles(root, "*.asset.meta", SearchOption.AllDirectories))
            {
                var g = Regex.Match(File.ReadAllText(meta), @"guid:\s*([0-9a-f]+)");
                if (!g.Success) continue;
                var asset = meta.Substring(0, meta.Length - ".meta".Length);
                var id = Regex.Match(File.ReadAllText(asset), @"^\s*Id:\s*(.*)$", RegexOptions.Multiline);
                _guidToId[g.Groups[1].Value] = id.Success
                    ? id.Groups[1].Value.Trim()
                    : Path.GetFileNameWithoutExtension(asset);
            }
            return _guidToId;
        }

        private static readonly Dictionary<string, Dictionary<string, string>> _idToPath =
            new Dictionary<string, Dictionary<string, string>>();

        /// <summary>Definition Id → asset path, for one Definitions subfolder.</summary>
        private static Dictionary<string, string> IdToPath(string folder)
        {
            if (_idToPath.TryGetValue(folder, out var cached)) return cached;
            var map = new Dictionary<string, string>();
            var dir = Path.Combine(Application.dataPath, "_Game/Resources/Definitions", folder);
            if (Directory.Exists(dir))
                foreach (var asset in Directory.GetFiles(dir, "*.asset"))
                {
                    var id = Regex.Match(File.ReadAllText(asset), @"^\s*Id:\s*(.*)$", RegexOptions.Multiline);
                    if (id.Success) map[id.Groups[1].Value.Trim()] = asset;
                }
            _idToPath[folder] = map;
            return map;
        }

        private static string DefinitionText(string folder, string id)
        {
            return IdToPath(folder).TryGetValue(id, out var path) && File.Exists(path)
                ? File.ReadAllText(path)
                : null;
        }
    }
}
