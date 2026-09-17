namespace Escape.Core
{
    /// <summary>
    /// Turns collected evidence combinations into insights — the
    /// investigation layer that changes interpretation, not just inventory.
    /// </summary>
    public sealed class InsightService : IGameCommandHandler<GainInsightCommand>
    {
        private readonly IGameStateService _state;
        private readonly IContentDatabase _content;
        private readonly IGameEventBus _events;
        private readonly ObjectiveService _objectives;

        public InsightService(IGameStateService state, IContentDatabase content,
            IGameEventBus events, ObjectiveService objectives)
        {
            _state = state;
            _content = content;
            _events = events;
            _objectives = objectives;
        }

        public void EvaluateNewEvidence(string evidenceId)
        {
            var s = _state.State;
            foreach (var insight in _content.Insights)
            {
                if (s.GainedInsights.Contains(insight.Id)) continue;
                bool referencesNew = false;
                bool all = true;
                foreach (var req in insight.RequiredEvidence)
                {
                    if (req == null) continue;
                    if (req.Id == evidenceId) referencesNew = true;
                    if (!s.CollectedEvidence.Contains(req.Id)) { all = false; break; }
                }
                if (all && referencesNew) Grant(insight.Id);
            }
        }

        public bool Grant(string insightId)
        {
            var s = _state.State;
            if (!_content.TryGetInsight(insightId, out var def)) return false;
            if (s.GainedInsights.Contains(insightId)) return false;
            s.GainedInsights.Add(insightId);
            _events.Publish(new InsightGainedEvent(insightId));
            _events.Publish(new SystemMessageEvent($"Insight: {def.Title}"));
            if (def.UnlocksObjective != null)
                _objectives.Activate(def.UnlocksObjective.Id);
            return true;
        }

        public void Handle(GainInsightCommand command) => Grant(command.InsightId);
    }
}
