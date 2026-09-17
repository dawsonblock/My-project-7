using UnityEngine;

namespace Escape.Gameplay
{
    public readonly struct InteractionPrompt
    {
        public readonly string Label;
        public readonly bool Enabled;

        public InteractionPrompt(string label, bool enabled = true)
        {
            Label = label;
            Enabled = enabled;
        }

        public static InteractionPrompt None => new InteractionPrompt("", false);
    }

    /// <summary>
    /// Anything the player can aim at and use. PlayerInteraction raycasts the
    /// Interactable layer and calls these.
    /// </summary>
    public interface IInteractable
    {
        InteractionPrompt GetPrompt(PlayerContext player);
        bool CanInteract(PlayerContext player);
        void Interact(PlayerContext player);
    }

    /// <summary>
    /// Runtime context handed to interactables. Avoids every interactable
    /// hunting for services and player components itself.
    /// </summary>
    public sealed class PlayerContext
    {
        public Transform Transform;
        public Camera Camera;
        public PlayerState State;
        public Escape.Core.GameServices Services;

        public Escape.Core.GameState GameState =>
            Services.Get<Escape.Core.IGameStateService>().State;

        public Escape.Core.IGameCommandDispatcher Dispatcher =>
            Services.Get<Escape.Core.IGameCommandDispatcher>();
    }
}
