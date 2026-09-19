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
        /// full completion requirements hold. Callers that mutate state on
        /// the strength of an objective must ask this first.
        /// </summary>
        bool CanComplete(string objectiveId);
        void EvaluateProgress();
        string CurrentObjectiveTitle { get; }
    }

    /// <summary>
    /// Objectives activate when their required objectives are complete, and
    /// complete when their full completion requirements hold — the required
    /// objectives *and* the required evidence the content declares.
    ///
    /// Two predicates, deliberately not one. "What makes an objective
    /// available" (activation) is a weaker claim than "what finishes it"
    /// (completion), and conflating them is how a caller ends up able to
    /// finish something the player has not earned. For an Explicit
    /// objective the completion requirements are also the action's
    /// authorization: the domain action that owns it (routing a broadcast,
    /// transmitting) asks CanComplete before mutating state, so the
    /// requirement is enforced by the authority rather than by whichever
    /// terminal surface happened to offer the button.
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
        /// Activation gate: only the prerequisite objectives. An objective
        /// becomes available as soon as the chain that leads to it is done;
        /// the evidence it needs is what finishes it, not what reveals it.
        /// </summary>
        public bool ActivationRequirementsMet(ObjectiveDefinition def)
        {
            var s = _state.State;
            foreach (var req in def.RequiredObjectives)
                if (req != null && !s.CompletedObjectives.Contains(req.Id)) return false;
            return true;
        }

        /// <summary>
        /// Completion gate: prerequisite objectives *and* every piece of
        /// evidence the objective declares. This is the predicate the domain
        /// actions and the command layer both use, so a requirement can no
        /// longer be enforced only by the surface that offers the action.
        /// </summary>
        public bool CompletionRequirementsMet(ObjectiveDefinition def)
        {
            if (!ActivationRequirementsMet(def)) return false;
            var s = _state.State;
            foreach (var ev in def.RequiredEvidence)
                if (ev != null && !s.CollectedEvidence.Contains(ev.Id)) return false;
            return true;
        }

        public bool CanComplete(string objectiveId)
        {
            if (!_content.TryGetObjective(objectiveId, out var def)) return false;
            if (_state.State.CompletedObjectives.Contains(objectiveId)) return false;
            return CompletionRequirementsMet(def);
        }

        public bool Activate(string objectiveId)
        {
            var s = _state.State;
            if (!_content.TryGetObjective(objectiveId, out var def)) return false;
            if (s.CompletedObjectives.Contains(objectiveId) || s.ActiveObjectives.Contains(objectiveId)) return false;
            if (!ActivationRequirementsMet(def)) return false;
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
            // Both completion paths — the bare command and the domain action —
            // land here, so neither can finish an objective whose declared
            // requirements are unmet.
            if (!CompletionRequirementsMet(def)) return false;
            s.ActiveObjectives.Remove(objectiveId);
            s.CompletedObjectives.Add(objectiveId);
            _events.Publish(new ObjectiveCompletedEvent(objectiveId));
            _events.Publish(new SystemMessageEvent($"Objective complete: {def.Title}"));
            _events.Publish(new ObjectiveUpdatedEvent());

            // Activate any objectives that are now unblocked.
            foreach (var candidate in _content.Objectives)
                if (ActivationRequirementsMet(candidate)) Activate(candidate.Id);
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
                    if (!s.ActiveObjectives.Contains(def.Id) && ActivationRequirementsMet(def))
                    {
                        Activate(def.Id);
                        changed = true;
                    }
                }
                foreach (var def in _content.Objectives)
                {
                    if (s.CompletedObjectives.Contains(def.Id) || !s.ActiveObjectives.Contains(def.Id)) continue;
                    // Explicit objectives finish only through their action —
                    // required evidence authorizes that action, it never
                    // auto-completes the objective.
                    if (def.Completion != ObjectiveCompletionMode.Evidence) continue;
                    if (def.RequiredEvidence.Length == 0) continue;
                    if (CompleteInternal(def.Id, "evidence")) changed = true;
                }
            }
        }

        public void Handle(ActivateObjectiveCommand command) => Activate(command.ObjectiveId);
        public void Handle(CompleteObjectiveCommand command) => Complete(command.ObjectiveId, command.SourceId);
    }
}
