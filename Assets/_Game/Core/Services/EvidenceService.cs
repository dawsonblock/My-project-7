using UnityEngine;

namespace Escape.Core
{
    public interface IEvidenceService
    {
        bool Collect(string evidenceId, string sourceId = "");
        bool Read(string documentId);
        bool IsCollected(string evidenceId);
    }

    /// <summary>
    /// Validates evidence ids against the content database, mutates state,
    /// then cascades: event → insight check → objective progress check.
    /// Unknown ids fail safely.
    /// </summary>
    public sealed class EvidenceService : IEvidenceService,
        IGameCommandHandler<CollectEvidenceCommand>,
        IGameCommandHandler<ReadDocumentCommand>
    {
        private readonly IGameStateService _state;
        private readonly IContentDatabase _content;
        private readonly IGameEventBus _events;
        private readonly InsightService _insights;
        private readonly ObjectiveService _objectives;

        public EvidenceService(IGameStateService state, IContentDatabase content,
            IGameEventBus events, InsightService insights, ObjectiveService objectives)
        {
            _state = state;
            _content = content;
            _events = events;
            _insights = insights;
            _objectives = objectives;
        }

        public bool IsCollected(string evidenceId) => _state.State.CollectedEvidence.Contains(evidenceId);

        public bool Collect(string evidenceId, string sourceId = "")
        {
            if (!_content.TryGetEvidence(evidenceId, out var def))
            {
                Debug.LogWarning($"[EvidenceService] Unknown evidence id '{evidenceId}' from '{sourceId}'. Ignored.");
                return false;
            }
            if (_state.State.CollectedEvidence.Contains(evidenceId)) return false;

            _state.State.CollectedEvidence.Add(evidenceId);
            _events.Publish(new EvidenceCollectedEvent(evidenceId));
            _events.Publish(new SystemMessageEvent($"Evidence acquired: {def.Title}"));

            _insights.EvaluateNewEvidence(evidenceId);
            _objectives.EvaluateProgress();
            return true;
        }

        public bool Read(string documentId)
        {
            if (!_content.TryGetDocument(documentId, out var doc))
            {
                Debug.LogWarning($"[EvidenceService] Unknown document id '{documentId}'. Ignored.");
                return false;
            }
            if (!_state.State.ReadDocuments.Contains(documentId))
                _state.State.ReadDocuments.Add(documentId);
            _events.Publish(new DocumentReadEvent(documentId));
            if (doc.GrantsEvidence != null)
                Collect(doc.GrantsEvidence.Id, documentId);
            return true;
        }

        public void Handle(CollectEvidenceCommand command) => Collect(command.EvidenceId, command.SourceId);
        public void Handle(ReadDocumentCommand command) => Read(command.DocumentId);
    }
}
