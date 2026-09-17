using UnityEngine;

namespace Escape.Gameplay
{
        public sealed class DoorInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DoorController door;
        [SerializeField] private string label = "Door";

        private void Awake()
        {
            if (door == null) door = GetComponentInParent<DoorController>();
        }

        public InteractionPrompt GetPrompt(PlayerContext player)
        {
            if (door.Unlocked || door.RequirementMet)
                return new InteractionPrompt(door.State == DoorState.Open ? $"[E] Close {label}" : $"[E] Open {label}");
            return new InteractionPrompt($"{door.LockedText}", false);
        }

        public bool CanInteract(PlayerContext player) => door.Unlocked || door.RequirementMet;

        public void Interact(PlayerContext player) => door.TryOpenByPlayer(player);
    }
}
