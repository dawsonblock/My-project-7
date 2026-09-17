using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    public sealed class FlashlightController : MonoBehaviour
    {
        [SerializeField] private Light flashlight;

        private PlayerInputReader _input;
        private ISettingsService _settings;

        public bool On { get; private set; }

        private void Awake()
        {
            _input = GetComponentInParent<PlayerInputReader>();
            if (flashlight == null) flashlight = GetComponentInChildren<Light>();
        }

        private void Start()
        {
            _settings = GameRoot.Instance.Services.Get<ISettingsService>();
            _input.FlashlightPressed += OnFlashlight;
            SetOn(false);
        }

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
