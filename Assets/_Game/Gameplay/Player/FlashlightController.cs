using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    public sealed class FlashlightController : MonoBehaviour, ISaveParticipant
    {
        [SerializeField] private Light flashlight;

        private PlayerInputReader _input;
        private ISettingsService _settings;
        private ISaveCoordinator _coordinator;

        public bool On { get; private set; }

        private void Awake()
        {
            _input = GetComponentInParent<PlayerInputReader>();
            if (flashlight == null) flashlight = GetComponentInChildren<Light>();
        }

        private void Start()
        {
            var services = GameRoot.Instance.Services;
            _settings = services.Get<ISettingsService>();
            if (services.TryGet<ISaveCoordinator>(out _coordinator))
                _coordinator.Register(this);
            _input.FlashlightPressed += OnFlashlight;
            // Reapply persisted state on every scene entry — flashlight is
            // carried across transitions, not just on save loads.
            SetOn(services.Get<IGameStateService>().State.Player.FlashlightOn);
        }

        private void OnDisable() => _coordinator?.Unregister(this);

        public void CaptureSaveState(GameState state) =>
            state.Player.FlashlightOn = On;

        public void RestoreSaveState(GameState state) =>
            SetOn(state.Player.FlashlightOn);

        private void OnDestroy()
        {
            if (_input != null) _input.FlashlightPressed -= OnFlashlight;
        }

        private void OnFlashlight()
        {
            if (!_settings.ToggleFlashlight) { SetOn(!On); return; }
            SetOn(!On);
        }

        public void SetOn(bool on)
        {
            On = on;
            if (flashlight != null) flashlight.enabled = on;
        }
    }
}
