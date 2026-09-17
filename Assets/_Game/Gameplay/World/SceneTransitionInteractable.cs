using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Doorway/trigger that moves the player to another scene through the
    /// SceneService (autosave → fade → load → spawn).
    /// </summary>
        public sealed class SceneTransitionInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string targetSceneId;
        [SerializeField] private string targetSpawnId = "default";
        [SerializeField] private string prompt = "[E] Continue";
        [SerializeField] private string requiredObjectiveId = "";
        [SerializeField] private string blockedText = "Locked";
        [SerializeField] private string completesObjectiveId = "";

        public InteractionPrompt GetPrompt(PlayerContext player)
        {
            if (!string.IsNullOrEmpty(requiredObjectiveId) &&
                !player.GameState.CompletedObjectives.Contains(requiredObjectiveId))
                return new InteractionPrompt(blockedText, false);
            return new InteractionPrompt(prompt);
        }

        public bool CanInteract(PlayerContext player) =>
            string.IsNullOrEmpty(requiredObjectiveId) ||
            player.GameState.CompletedObjectives.Contains(requiredObjectiveId);

        public void Interact(PlayerContext player)
        {
            // Capture the pose before leaving so autosave stores it.
            var pose = player.GameState.Player;
            pose.Position = player.Transform.position;
            pose.Yaw = player.Transform.eulerAngles.y;
            var look = player.Transform.GetComponentInChildren<PlayerLook>();
            pose.Pitch = look != null ? look.Pitch : 0f;
            if (!string.IsNullOrEmpty(completesObjectiveId))
                player.Dispatcher.Dispatch(new CompleteObjectiveCommand(completesObjectiveId, name));
            player.Dispatcher.Dispatch(new ChangeSceneCommand(targetSceneId, targetSpawnId));
        }
    }
}
