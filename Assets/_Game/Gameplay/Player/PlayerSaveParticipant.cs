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
        }

        // Registration is symmetric with OnDisable — a disable/enable cycle
        // must leave the participant registered exactly once. Start retries
        // the bind in case GameRoot lagged behind the scene load.
        private void OnEnable() => TryRegister();
        private void Start() => TryRegister();

        private void TryRegister()
        {
            if (_coordinator != null || GameRoot.Instance == null) return;
            if (GameRoot.Instance.Services.TryGet<ISaveCoordinator>(out var c))
                (_coordinator = c).Register(this);
        }

        private void OnDisable()
        {
            _coordinator?.Unregister(this);
            _coordinator = null;
        }

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
