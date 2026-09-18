using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Grounded stealth-horror movement: accel/decel, crouch, sprint,
    /// gravity. Deliberately not an arena-shooter feel.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private CharacterController _cc;
        private PlayerInputReader _input;
        private PlayerState _state;
        private PlayerLook _look;
        private Data.StealthTuning _tuning;
        private ISettingsService _settings;
        private IInputGate _gate;

        private Vector3 _velocity;
        private float _vertical;
        private bool _crouchToggled;
        private bool _sprintToggled;
        private float _standHeight;
        private Vector3 _standCenter;
        private bool _bound;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            _state = GetComponent<PlayerState>();
            _look = GetComponentInChildren<PlayerLook>();
            _standHeight = _cc.height;
            _standCenter = _cc.center;
        }

        // Bind + subscribe on enable, unwind on disable — symmetric, so a
        // disable/enable cycle leaves the input subscription intact exactly
        // once. Start retries the bind in case GameRoot lagged scene load.
        private void OnEnable() => TryBind();
        private void Start() => TryBind();

        private void TryBind()
        {
            if (_bound || GameRoot.Instance == null) return;
            var services = GameRoot.Instance.Services;
            _tuning = services.Get<IContentDatabase>().Tuning;
            _settings = services.Get<ISettingsService>();
            _gate = services.Get<IInputGate>();
            if (_input != null) _input.CrouchPressed += OnCrouchPressed;
            _bound = true;
        }

        private void OnDisable()
        {
            if (_input != null) _input.CrouchPressed -= OnCrouchPressed;
            _bound = false;
        }

        private void OnCrouchPressed()
        {
            // Toggle mode: press flips. Hold mode is driven by CrouchHeld
            // in Update — press-down crouches, release stands.
            if (_settings.ToggleCrouch) _crouchToggled = !_crouchToggled;
        }

        private void Update()
        {
            if (_gate == null || _tuning == null || _settings == null) return;
            if (_state.Caught || _gate.UiOpen)
            {
                _velocity = Vector3.zero;
                ApplyGravity();
                return;
            }

            bool crouch = _settings.ToggleCrouch ? _crouchToggled : _input.CrouchHeld;
            _state.Crouching = crouch;

            bool sprint = _settings.ToggleSprint ? _sprintToggled : _input.SprintHeld;
            if (_settings.ToggleSprint && _input.SprintHeld && !_sprintWasHeld) _sprintToggled = !_sprintToggled;
            _sprintWasHeld = _input.SprintHeld;
            TickStamina(sprint);
            _state.Sprinting = sprint && !crouch && !_state.Exhausted
                && _input.Move.sqrMagnitude > 0.01f;

            float speed = crouch ? _tuning.CrouchSpeed
                : _state.Sprinting ? _tuning.SprintSpeed
                : _tuning.WalkSpeed;

            Vector3 wish = transform.TransformDirection(
                new Vector3(_input.Move.x, 0f, _input.Move.y)).normalized * speed;
            float rate = wish.sqrMagnitude > 0.01f ? _tuning.Acceleration : _tuning.Deceleration;
            _velocity = Vector3.MoveTowards(_velocity, wish, rate * Time.deltaTime);

            ApplyGravity();
            _cc.Move((_velocity + Vector3.up * _vertical) * Time.deltaTime);

            // Height follows crouch state.
            float targetH = crouch ? _standHeight * 0.55f : _standHeight;
            _cc.height = Mathf.Lerp(_cc.height, targetH, Time.deltaTime * 10f);
            _cc.center = _standCenter * (_cc.height / _standHeight);
        }

        private bool _sprintWasHeld;
        private float _sprintStoppedAt = -10f;

        private void TickStamina(bool sprintRequested)
        {
            bool moving = _input.Move.sqrMagnitude > 0.01f;
            bool wantsSprint = sprintRequested && moving && !_state.Crouching;

            if (wantsSprint && !_state.Exhausted)
            {
                _state.Stamina -= _tuning.SprintStaminaDrain * Time.deltaTime;
                _sprintStoppedAt = Time.time;
                if (_state.Stamina <= 0f)
                {
                    _state.Stamina = 0f;
                    _state.Exhausted = true;
                }
            }
            else if (Time.time - _sprintStoppedAt > _tuning.StaminaRegenDelay)
            {
                _state.Stamina = Mathf.Min(_tuning.MaxStamina,
                    _state.Stamina + _tuning.StaminaRegenRate * Time.deltaTime);
            }

            if (_state.Exhausted && _state.Stamina >= _tuning.StaminaResumeThreshold)
                _state.Exhausted = false;
        }

        private void ApplyGravity()
        {
            if (_cc.isGrounded && _vertical < 0f) _vertical = -2f;
            _vertical += _tuning != null ? _tuning.Gravity * Time.deltaTime : -18f * Time.deltaTime;
        }

        public void Teleport(Vector3 position, float yaw, float pitch = 0f)
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            _cc.enabled = true;
            _velocity = Vector3.zero;
            if (_look != null) _look.SetPitch(pitch);
        }
    }
}
