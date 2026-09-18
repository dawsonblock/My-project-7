using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Mouse/stick look on the camera pivot, plus lean (Q/E roll+offset),
    /// optional head bob, and cursor ownership. Cursor locks only while
    /// gameplay owns input.
    /// </summary>
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private float leanAngle = 12f;
        [SerializeField] private float leanOffset = 0.45f;
        [SerializeField] private float bobFrequency = 9f;
        [SerializeField] private float bobAmplitude = 0.035f;
        /// <summary>Owns yaw — the player root. Pitch/lean stay on this pivot.</summary>
        [SerializeField] private Transform yawRoot;
        [SerializeField] private float controllerDegreesPerSecond = 140f;

        private PlayerInputReader _input;
        private PlayerState _state;
        private CharacterController _cc;
        private ISettingsService _settings;
        private IInputGate _gate;
        private Data.StealthTuning _tuning;
        private bool _bound;

        private float _pitch;
        private float _bobT;
        private float _lean; // -1..1 smoothed
        private Vector3 _rest;

        public Camera Cam { get; private set; }

        private void Awake()
        {
            _input = GetComponentInParent<PlayerInputReader>();
            _state = GetComponentInParent<PlayerState>();
            _cc = GetComponentInParent<CharacterController>();
            Cam = GetComponentInChildren<Camera>();
            _rest = transform.localPosition;
            if (yawRoot == null && _state != null) yawRoot = _state.transform;
        }

        // Bind + subscribe on enable, unwind on disable — symmetric, so a
        // disable/enable cycle leaves the gate subscription intact exactly
        // once. Start retries the bind in case GameRoot lagged scene load.
        private void OnEnable() => TryBind();
        private void Start() => TryBind();

        private void TryBind()
        {
            if (_bound || GameRoot.Instance == null) return;
            var services = GameRoot.Instance.Services;
            _settings = services.Get<ISettingsService>();
            _gate = services.Get<IInputGate>();
            _tuning = services.Get<IContentDatabase>().Tuning;
            _gate.OnUiModeChanged += OnUiModeChanged;
            ApplyCursor();
            if (Cam != null) Cam.fieldOfView = _settings.Fov;
            _bound = true;
        }

        private void OnDisable()
        {
            if (_gate != null) _gate.OnUiModeChanged -= OnUiModeChanged;
            _bound = false;
        }

        private void OnUiModeChanged(bool _) => ApplyCursor();

        private void ApplyCursor()
        {
            bool ui = _gate != null && _gate.UiOpen;
            Cursor.lockState = ui ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = ui;
        }

        public float Pitch => _pitch;
        public void SetPitch(float pitch) => _pitch = pitch;

        private void Update()
        {
            if (_state == null || _settings == null) return;
            if (_state.Caught) return;
            bool ui = _gate != null && _gate.UiOpen;

            if (!ui)
            {
                // Mouse deltas are per-event; stick deflection is a rate and
                // must be integrated over time or look speed varies with fps.
                bool pad = _input.LookFromGamepad;
                float sens = (pad ? _settings.ControllerSensitivity : _settings.MouseSensitivity);
                if (_input.FocusHeld && _tuning != null) sens *= _tuning.FocusSensitivityScale;
                float yInvert = _settings.InvertY ? 1f : -1f;
                Vector2 look = _input.Look;
                if (pad) look *= controllerDegreesPerSecond * Time.deltaTime;
                else look *= 0.08f;

                // Yaw lives on the player root — movement reads the same
                // transform, so forward stays where the camera points.
                if (yawRoot != null) yawRoot.Rotate(Vector3.up, look.x * sens, Space.Self);
                _pitch = Mathf.Clamp(_pitch + look.y * sens * yInvert, -85f, 85f);
            }

            // Focus zoom — RMB eases the FOV in for reading security at range.
            if (Cam != null && _tuning != null)
            {
                float targetFov = _input.FocusHeld && !ui
                    ? _settings.Fov * _tuning.FocusFovScale
                    : _settings.Fov;
                Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, targetFov, Time.deltaTime * 8f);
            }

            float leanTarget = _input.LeanLeft ? -1f : _input.LeanRight ? 1f : 0f;
            _lean = Mathf.MoveTowards(_lean, leanTarget, Time.deltaTime * 6f);

            float speed = _cc != null ? new Vector3(_cc.velocity.x, 0, _cc.velocity.z).magnitude : 0f;
            bool bobOn = _settings.HeadBob && !_settings.ReduceMotion && speed > 0.5f && !ui;
            if (bobOn)
                _bobT += Time.deltaTime * bobFrequency * Mathf.Clamp01(speed / 3f);
            else
                _bobT = Mathf.MoveTowards(_bobT, Mathf.Round(_bobT / Mathf.PI) * Mathf.PI, Time.deltaTime * 6f);
            float bobY = bobOn ? Mathf.Sin(_bobT) * bobAmplitude * Mathf.Clamp01(speed / 3f) : 0f;

            transform.localPosition = _rest + new Vector3(_lean * leanOffset, bobY, 0f);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, -_lean * leanAngle);
        }
    }
}
