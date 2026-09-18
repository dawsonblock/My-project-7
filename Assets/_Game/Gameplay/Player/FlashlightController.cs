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
        private bool _bound;

        public bool On { get; private set; }

        private void Awake()
        {
            _input = GetComponentInParent<PlayerInputReader>();
            if (flashlight == null) flashlight = GetComponentInChildren<Light>();
        }

        // Bind + register + subscribe on enable, unwind all three on
        // disable — symmetric, so a disable/enable cycle leaves the
        // participant registered exactly once. Start retries the bind in
        // case GameRoot lagged behind the scene load.
        private void OnEnable() => TryBind();
        private void Start() => TryBind();

        private void TryBind()
        {
            if (_bound || GameRoot.Instance == null) return;
            var services = GameRoot.Instance.Services;
            _settings = services.Get<ISettingsService>();
            if (services.TryGet<ISaveCoordinator>(out var c))
                (_coordinator = c).Register(this);
            if (_input != null) _input.FlashlightPressed += OnFlashlight;
            // Reapply persisted state on every scene entry — flashlight is
            // carried across transitions, not just on save loads.
            SetOn(services.Get<IGameStateService>().State.Player.FlashlightOn);
            _bound = true;
        }

        private void OnDisable()
        {
            _coordinator?.Unregister(this);
            _coordinator = null;
            if (_input != null) _input.FlashlightPressed -= OnFlashlight;
            _bound = false;
        }

        public void CaptureSaveState(GameState state) =>
            state.Player.FlashlightOn = On;

        public void RestoreSaveState(GameState state) =>
            SetOn(state.Player.FlashlightOn);

        private void OnFlashlight()
        {
            // Toggle mode: press inverts. Hold mode is driven by
            // FlashlightHeld in Update — down is on, release is off.
            if (_settings != null && _settings.ToggleFlashlight) SetOn(!On);
        }

        private void Update()
        {
            if (_settings == null || _input == null) return;
            if (!_settings.ToggleFlashlight) SetOn(_input.FlashlightHeld);
        }

        public void SetOn(bool on)
        {
            On = on;
            if (flashlight != null) flashlight.enabled = on;
        }
    }
}
