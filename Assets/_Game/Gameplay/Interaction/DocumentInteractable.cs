using Escape.Core;
using Escape.Data;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Readable document. Opens the document viewer; reading marks the
    /// document read and grants its linked evidence.
    /// </summary>
        public sealed class DocumentInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DocumentDefinition document;

        public InteractionPrompt GetPrompt(PlayerContext player)
        {
            if (document == null) return InteractionPrompt.None;
            bool read = player.GameState.ReadDocuments.Contains(document.Id);
            return new InteractionPrompt(read ? $"[E] Re-read {document.Title}" : $"[E] Read {document.Title}");
        }

        public bool CanInteract(PlayerContext player) => document != null;

        public void Interact(PlayerContext player)
        {
            player.Dispatcher.Dispatch(new ReadDocumentCommand(document.Id));
            if (player.Services.TryGet<IDocumentUI>(out var ui))
                ui.Show(document);
        }
    }
}
