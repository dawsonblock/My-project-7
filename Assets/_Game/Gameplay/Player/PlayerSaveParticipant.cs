using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Captures the live player pose (position + yaw from the root,
    /// pitch from PlayerLook) into GameState at save time, and restores
    /// it on load. Added to the Player prefab by PrefabFactory.
    /// </summary>
    public sealed class PlayerSaveParticipant : MonoBehaviour, ISaveParticipant
    {
        private PlayerMovement _movement;
        private PlayerLook _look;
        private ISaveCoordinator _coordinator;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _look = GetComponentInChildren<PlayerLook>();
            // Register in Awake — SceneService places the player one frame
            // after LoadScene, and Start ordering relative to that coroutine
            // isn't guaranteed. GameRoot persists across scenes.
            TryRegister();
        }

        private void Start() => TryRegister();

        private void TryRegister()
        {
            if (_coordinator != null || GameRoot.Instance == null) return;
            if (GameRoot.Instance.Services.TryGet<ISaveCoordinator>(out var c))
                (_coordinator = c).Register(this);
        }

        private void OnDisable() => _coordinator?.Unregister(this);

        public void CaptureSaveState(GameState state)
        {
            var p = state.Player;
            p.Position = transform.position;
            p.Yaw = transform.eulerAngles.y;
            p.Pitch = _look != null ? _look.Pitch : 0f;
        }

        public void RestoreSaveState(GameState state)
        {
            var p = state.Player;
            if (_movement != null) _movement.Teleport(p.Position, p.Yaw, p.Pitch);
        }
    }
}
