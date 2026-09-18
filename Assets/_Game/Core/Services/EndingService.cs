using System.Collections.Generic;
using Escape.Data;
using UnityEngine;

namespace Escape.Core
{
    /// <summary>
    /// Deterministic result of a broadcast. UI shows this breakdown so the
    /// ending explains itself: what was transmitted, corroborated, missing.
    /// </summary>
    public sealed class EndingResult
    {
        public EndingDefinition Ending;
        public List<string> TransmittedEvidence = new List<string>();
        public List<string> CorroboratedClaims = new List<string>();
        public List<string> MissingEvidence = new List<string>();
        public List<string> UnresolvedContradictions = new List<string>();
        public List<string> GainedInsights = new List<string>();
    }

    /// <summary>
    /// Pure, deterministic evaluator. Picks the highest-priority ending whose
    /// requirements the GameState satisfies.
    /// </summary>
    public static class EndingEvaluator
    {
        public static EndingResult Evaluate(GameState state, IContentDatabase content)
        {
            var result = new EndingResult();
            // Don't trust collection hygiene — a corrupted save must not
            // double-count evidence or insights toward endings.
            var collected = new HashSet<string>(
                state.CollectedEvidence ?? new List<string>(), System.StringComparer.Ordinal);
            var insights = new HashSet<string>(
                state.GainedInsights ?? new List<string>(), System.StringComparer.Ordinal);
            result.GainedInsights.AddRange(insights);

            int primaryCount = 0;
            foreach (var id in collected)
            {
                if (!content.TryGetEvidence(id, out var def)) continue;
                result.TransmittedEvidence.Add(def.Title);
                if (def.Category == EvidenceCategory.Primary) primaryCount++;

                foreach (var c in def.Corroborates)
                    if (c != null && collected.Contains(c.Id) &&
                        !result.CorroboratedClaims.Contains(c.Title))
                        result.CorroboratedClaims.Add(c.Title);
                foreach (var c in def.Contradicts)
                    if (c != null && !collected.Contains(c.Id) &&
                        !result.UnresolvedContradictions.Contains(c.Title))
                        result.UnresolvedContradictions.Add(c.Title);
            }

            foreach (var def in content.Evidence)
                if (def.RequiredForBroadcast && !collected.Contains(def.Id))
                    result.MissingEvidence.Add(def.Title);

            EndingDefinition best = null;
            foreach (var ending in content.Endings)
            {
                if (primaryCount < ending.MinPrimaryEvidence) continue;
                bool ok = true;
                foreach (var req in ending.RequiredEvidence)
                    if (req != null && !collected.Contains(req.Id)) { ok = false; break; }
                if (!ok) continue;
                foreach (var req in ending.RequiredInsights)
                    if (req != null && !insights.Contains(req.Id)) { ok = false; break; }
                if (!ok) continue;
                if (best == null || ending.Priority > best.Priority) best = ending;
            }

            result.Ending = best;
            return result;
        }
    }

    public sealed class EndingService
    {
        private readonly IGameStateService _state;
        private readonly IContentDatabase _content;

        public EndingService(IGameStateService state, IContentDatabase content)
        {
            _state = state;
            _content = content;
        }

        public EndingResult Evaluate() => EndingEvaluator.Evaluate(_state.State, _content);
    }
}
