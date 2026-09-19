using Escape.Data;
using UnityEngine;

namespace Escape.Core
{
    public interface IObjectiveService
    {
        bool Activate(string objectiveId);
        bool Complete(string objectiveId, string sourceId = "");
        /// <summary>
        /// Completes an objective on behalf of the domain action that owns it
        /// (routing a broadcast, transmitting). Explicit objectives are only
        /// completable this way — a bare command is refused, so "an action,
        /// not a pickup" is enforced rather than conventional.
        /// </summary>
        bool CompleteFromAction(string objectiveId, string sourceId = "");
        /// <summary>
        /// True when the objective exists, is not already complete, and its
        /// prerequisite objectives are complete. Callers that mutate state on
        /// the strength of an objective must ask this first.
        /// </summary>
        bool CanComplete(string objectiveId);
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

        /// <summary>
        /// RequiredObjectives gate both activation and completion — an
        /// objective cannot finish before the objectives it depends on.
        /// RequiredEvidence is completion criteria for Evidence-mode
        /// objectives only; on an Explicit objective it gates the surface that
        /// offers the action (a terminal command, a console), not completion.
        /// </summary>
        public bool RequirementsMet(ObjectiveDefinition def)
        {
            var s = _state.State;
            foreach (var req in def.RequiredObjectives)
                if (req != null && !s.CompletedObjectives.Contains(req.Id)) return false;
            return true;
        }

        public bool CanComplete(string objectiveId)
        {
            if (!_content.TryGetObjective(objectiveId, out var def)) return false;
            if (_state.State.CompletedObjectives.Contains(objectiveId)) return false;
            return RequirementsMet(def);
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
            if (!_content.TryGetObjective(objectiveId, out var def)) return false;
            // An Explicit objective belongs to a domain action. Letting a bare
            // CompleteObjectiveCommand finish it would make the "action, not a
            // pickup" contract a convention that any caller could ignore.
            if (def.Completion == ObjectiveCompletionMode.Explicit) return false;
            return CompleteFromAction(objectiveId, sourceId);
        }

        public bool CompleteFromAction(string objectiveId, string sourceId = "")
        {
            if (!CompleteInternal(objectiveId, sourceId)) return false;
            // Completing one objective can make an Evidence-mode objective
            // eligible, so settle the whole graph before returning — otherwise
            // a completion-driven unlock would stall until the next pickup.
            EvaluateProgress();
            return true;
        }

        private bool CompleteInternal(string objectiveId, string sourceId)
        {
            var s = _state.State;
            if (!_content.TryGetObjective(objectiveId, out var def)) return false;
            if (s.CompletedObjectives.Contains(objectiveId)) return false;
            if (!RequirementsMet(def)) return false;
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
                    // Explicit objectives finish only through commands —
                    // required evidence gates availability, not completion.
                    if (def.Completion != ObjectiveCompletionMode.Evidence) continue;
                    if (def.RequiredEvidence.Length == 0) continue;
                    bool all = true;
                    foreach (var ev in def.RequiredEvidence)
                        if (ev != null && !s.CollectedEvidence.Contains(ev.Id)) { all = false; break; }
                    if (all)
                    {
                        CompleteInternal(def.Id, "evidence");
                        changed = true;
                    }
                }
            }
        }

        public void Handle(ActivateObjectiveCommand command) => Activate(command.ObjectiveId);
        public void Handle(CompleteObjectiveCommand command) => Complete(command.ObjectiveId, command.SourceId);
    }
}
