using Escape.Data;
using UnityEngine;

namespace Escape.Core
{
    public interface IObjectiveService
    {
        bool Activate(string objectiveId);
        bool Complete(string objectiveId, string sourceId = "");
        void EvaluateProgress();
        string CurrentObjectiveTitle { get; }
    }

    /// <summary>
    /// Objectives activate when their required objectives are complete and
    /// complete when their required evidence is collected (or via explicit
    /// command for terminal/action-driven objectives).
    /// </summary>
    public sealed class ObjectiveService : IObjectiveService,
        IGameCommandHandler<ActivateObjectiveCommand>,
        IGameCommandHandler<CompleteObjectiveCommand>
    {
        private readonly IGameStateService _state;
        private readonly IContentDatabase _content;
        private readonly IGameEventBus _events;

        public ObjectiveService(IGameStateService state, IContentDatabase content, IGameEventBus events)
        {
            _state = state;
            _content = content;
            _events = events;
        }

        public string CurrentObjectiveTitle
        {
            get
            {
                var active = _state.State.ActiveObjectives;
                for (int i = active.Count - 1; i >= 0; i--)
                    if (_content.TryGetObjective(active[i], out var def)) return def.Title;
                return string.Empty;
            }
        }

        public bool RequirementsMet(ObjectiveDefinition def)
        {
            var s = _state.State;
            foreach (var req in def.RequiredObjectives)
                if (req != null && !s.CompletedObjectives.Contains(req.Id)) return false;
            return true;
        }

        public bool Activate(string objectiveId)
        {
            var s = _state.State;
            if (!_content.TryGetObjective(objectiveId, out var def)) return false;
            if (s.CompletedObjectives.Contains(objectiveId) || s.ActiveObjectives.Contains(objectiveId)) return false;
            if (!RequirementsMet(def)) return false;
            s.ActiveObjectives.Add(objectiveId);
            _events.Publish(new ObjectiveActivatedEvent(objectiveId));
            _events.Publish(new ObjectiveUpdatedEvent());
            return true;
        }

        public bool Complete(string objectiveId, string sourceId = "")
        {
            var s = _state.State;
            if (!_content.TryGetObjective(objectiveId, out var def)) return false;
            if (s.CompletedObjectives.Contains(objectiveId)) return false;
            s.ActiveObjectives.Remove(objectiveId);
            s.CompletedObjectives.Add(objectiveId);
            _events.Publish(new ObjectiveCompletedEvent(objectiveId));
            _events.Publish(new SystemMessageEvent($"Objective complete: {def.Title}"));
            _events.Publish(new ObjectiveUpdatedEvent());

            // Activate any objectives that are now unblocked.
            foreach (var candidate in _content.Objectives)
                if (RequirementsMet(candidate)) Activate(candidate.Id);
            return true;
        }

        /// <summary>
        /// Completes evidence-driven objectives and activates newly unblocked ones.
        /// Runs to a fixpoint: the content list has no dependency ordering
        /// guarantee, so a completion mid-pass can unblock objectives that were
        /// already iterated past.
        /// </summary>
        public void EvaluateProgress()
        {
            var s = _state.State;
            bool changed = true;
            int guard = 0;
            while (changed && guard++ < 32)
            {
                changed = false;
                foreach (var def in _content.Objectives)
                {
                    if (s.CompletedObjectives.Contains(def.Id)) continue;
                    if (!s.ActiveObjectives.Contains(def.Id) && RequirementsMet(def))
                    {
                        Activate(def.Id);
                        changed = true;
                    }
                }
                foreach (var def in _content.Objectives)
                {
                    if (s.CompletedObjectives.Contains(def.Id) || !s.ActiveObjectives.Contains(def.Id)) continue;
                    if (def.RequiredEvidence.Length == 0) continue;
                    bool all = true;
                    foreach (var ev in def.RequiredEvidence)
                        if (ev != null && !s.CollectedEvidence.Contains(ev.Id)) { all = false; break; }
                    if (all)
                    {
                        Complete(def.Id, "evidence");
                        changed = true;
                    }
                }
            }
        }

        public void Handle(ActivateObjectiveCommand command) => Activate(command.ObjectiveId);
        public void Handle(CompleteObjectiveCommand command) => Complete(command.ObjectiveId, command.SourceId);
    }
}
