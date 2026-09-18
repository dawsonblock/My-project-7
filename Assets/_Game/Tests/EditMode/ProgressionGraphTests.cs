using System.Collections.Generic;
using System.IO;
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
                var world = new WorldService(_state, events, new EndingService(_state, content));

                _dispatcher = new GameCommandDispatcher(new CommandJournal(false));
                _dispatcher.Register<CollectEvidenceCommand>(evidence);
                _dispatcher.Register<CompleteObjectiveCommand>(_objectives);
                _dispatcher.Register<UnlockDoorCommand>(world);
                _dispatcher.Register<DisableCameraCommand>(world);
                _dispatcher.Register<SetLockdownCommand>(world);
                _dispatcher.Register<StartBroadcastCommand>(world);
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
                // completing it is what sets the ending.
                if (State.BroadcastStarted && !State.BroadcastCompleted)
                {
                    _dispatcher.Dispatch(new CompleteBroadcastCommand());
                    Complete(_consoleObjective);
                }
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
                        _dispatcher.Dispatch(new StartBroadcastCommand(term.Id)); break;
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

            public sealed class Gate
            {
                public string Target, RequiredEvidence, RequiredObjective, CompletesObjective;
            }

            public static SceneFacts Read(string sceneId, Dictionary<string, string> guids)
            {
                var f = new SceneFacts { Id = sceneId };
                var path = Path.Combine(Application.dataPath, "_Game/Scenes", FileName(sceneId) + ".unity");
                var text = File.ReadAllText(path);

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
