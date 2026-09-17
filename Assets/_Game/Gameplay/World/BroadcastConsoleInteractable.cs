using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// End-of-slice relay console. Requires the signal to have been routed
    /// by a terminal BROADCAST command, then transmitting evaluates evidence
    /// and produces the ending.
    /// </summary>
        public sealed class BroadcastConsoleInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string label = "TRANSMIT";

        public InteractionPrompt GetPrompt(PlayerContext player)
        {
            var s = player.GameState;
            if (s.BroadcastCompleted) return InteractionPrompt.None;
            if (!s.BroadcastStarted)
                return new InteractionPrompt("Relay cold — route the signal at a security node first", false);
            return new InteractionPrompt($"[E] {label} — broadcast the evidence");
        }

        public bool CanInteract(PlayerContext player) =>
            player.GameState.BroadcastStarted && !player.GameState.BroadcastCompleted;

        public void Interact(PlayerContext player)
        {
            player.Dispatcher.Dispatch(new SetAlertCommand(true, "broadcast"));
            player.Dispatcher.Dispatch(new CompleteBroadcastCommand());
        }
    }
}
