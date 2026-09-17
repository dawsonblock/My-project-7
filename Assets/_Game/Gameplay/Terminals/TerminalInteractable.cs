using Escape.Core;
using Escape.Data;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Opens the terminal UI. The terminal itself is a dumb screen — all
    /// actions flow through the command dispatcher via TerminalUI.
    /// </summary>
        public sealed class TerminalInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private TerminalDefinition terminal;

        public TerminalDefinition Terminal => terminal;

        public InteractionPrompt GetPrompt(PlayerContext player)
        {
            if (terminal == null) return InteractionPrompt.None;
            if (terminal.RequiredObjective != null &&
                !player.GameState.CompletedObjectives.Contains(terminal.RequiredObjective.Id))
                return new InteractionPrompt($"{terminal.DisplayName} — offline", false);
            return new InteractionPrompt($"[E] Access {terminal.DisplayName}");
        }

        public bool CanInteract(PlayerContext player)
        {
            if (terminal == null) return false;
            return terminal.RequiredObjective == null ||
                   player.GameState.CompletedObjectives.Contains(terminal.RequiredObjective.Id);
        }

        public void Interact(PlayerContext player)
        {
            if (player.Services.TryGet<ITerminalUI>(out var ui))
                ui.Open(terminal);
        }
    }
}
