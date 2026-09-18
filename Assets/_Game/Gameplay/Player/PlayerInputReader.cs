using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape.Gameplay
{
    /// <summary>
    /// Wraps the PlayerInputActions asset. Reads the Player map for gameplay
    /// and exposes one-shot button events. Switches between Player and UI
    /// maps when the input gate reports a UI surface is open.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;

        private InputActionMap _playerMap;
        private InputActionMap _uiMap;
        private InputActionMap _systemMap;
        private InputAction _move, _look, _interact, _sprint, _crouch;
        private InputAction _leanLeft, _leanRight, _flashlight, _board;
        private InputAction _throw, _whistle, _focus;
        private InputAction _pause, _cancel;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        /// <summary>True while the Look action is actuated by a gamepad —
        /// stick input is a rate (deg/s), mouse input is a per-event delta.</summary>
        public bool LookFromGamepad { get; private set; }
        public bool SprintHeld { get; private set; }

        public event System.Action InteractPressed;
        public event System.Action CrouchPressed;
        public event System.Action FlashlightPressed;
        public event System.Action EvidenceBoardPressed;
        /// <summary>Global pause/back (Escape, pad start). Fires even while UI owns input.</summary>
        public event System.Action PausePressed;
        /// <summary>Global cancel (pad east/select). Fires even while UI owns input.</summary>
        public event System.Action CancelPressed;
        public event System.Action ThrowPressed;
        public event System.Action WhistlePressed;
        public bool LeanLeft { get; private set; }
        public bool LeanRight { get; private set; }
        public bool FocusHeld { get; private set; }
        /// <summary>Held state for hold-mode crouch semantics.</summary>
        public bool CrouchHeld { get; private set; }
        /// <summary>Held state for hold-mode flashlight semantics.</summary>
        public bool FlashlightHeld { get; private set; }

        private Escape.Core.IInputGate _gate;

        private void Awake()
        {
            if (actions == null)
            {
                Debug.LogError("[PlayerInputReader] No InputActionAsset assigned — input is dead.", this);
                return;
            }
            _playerMap = actions.FindActionMap("Player", true);
            _uiMap = actions.FindActionMap("UI", true);
            _systemMap = actions.FindActionMap("System", true);
            _move = _playerMap.FindAction("Move", true);
            _look = _playerMap.FindAction("Look", true);
            _interact = _playerMap.FindAction("Interact", true);
            _sprint = _playerMap.FindAction("Sprint", true);
            _crouch = _playerMap.FindAction("Crouch", true);
            _leanLeft = _playerMap.FindAction("LeanLeft", true);
            _leanRight = _playerMap.FindAction("LeanRight", true);
            _flashlight = _playerMap.FindAction("Flashlight", true);
            _board = _playerMap.FindAction("EvidenceBoard", true);
            _throw = _playerMap.FindAction("Throw", true);
            _whistle = _playerMap.FindAction("Whistle", true);
            _focus = _playerMap.FindAction("Focus", true);
            _pause = _systemMap.FindAction("Pause", true);
            _cancel = _systemMap.FindAction("Cancel", true);
        }

        private void OnEnable()
        {
            if (actions == null) return;
            // Enable at the asset level — map-level Enable() can hit a stale
            // map index if the imported asset was reimported mid-session.
            actions.Enable();
            _uiMap.Disable();
            // The System map stays enabled in every mode — pause/back must
            // work while a UI surface owns the Player map.
            _systemMap.Enable();
            _interact.performed += OnInteract;
            _crouch.performed += OnCrouch;
            _flashlight.performed += OnFlashlight;
            _board.performed += OnBoard;
            _pause.performed += OnPause;
            _cancel.performed += OnCancel;
            _throw.performed += OnThrow;
            _whistle.performed += OnWhistle;
            if (Escape.Core.GameRoot.Instance != null)
            {
                _gate = Escape.Core.GameRoot.Instance.Services.Get<Escape.Core.IInputGate>();
                _gate.OnUiModeChanged += OnUiModeChanged;
                OnUiModeChanged(_gate.UiOpen);
            }
        }

        private void OnDisable()
        {
            if (_playerMap == null) return; // Awake bailed — nothing subscribed
            _playerMap.Disable();
            _systemMap.Disable();
            _interact.performed -= OnInteract;
            _crouch.performed -= OnCrouch;
            _flashlight.performed -= OnFlashlight;
            _board.performed -= OnBoard;
            _pause.performed -= OnPause;
            _cancel.performed -= OnCancel;
            _throw.performed -= OnThrow;
            _whistle.performed -= OnWhistle;
            if (_gate != null) _gate.OnUiModeChanged -= OnUiModeChanged;
        }

        private void OnUiModeChanged(bool uiOpen)
        {
            if (uiOpen)
            {
                _playerMap.Disable();
                _uiMap.Enable();
            }
            else
            {
                _uiMap.Disable();
                _playerMap.Enable();
            }
            Move = Vector2.zero;
            Look = Vector2.zero;
        }

        private void Update()
        {
            if (_move == null) return;
            Move = _move.ReadValue<Vector2>();
            Look = _look.ReadValue<Vector2>();
            LookFromGamepad = _look.activeControl?.device is Gamepad;
            SprintHeld = _sprint.IsPressed();
            LeanLeft = _leanLeft.IsPressed();
            LeanRight = _leanRight.IsPressed();
            FocusHeld = _focus.IsPressed();
            CrouchHeld = _crouch.IsPressed();
            FlashlightHeld = _flashlight.IsPressed();
        }

        private void OnInteract(InputAction.CallbackContext _) => InteractPressed?.Invoke();
        private void OnCrouch(InputAction.CallbackContext _) => CrouchPressed?.Invoke();
        private void OnFlashlight(InputAction.CallbackContext _) => FlashlightPressed?.Invoke();
        private void OnBoard(InputAction.CallbackContext _) => EvidenceBoardPressed?.Invoke();
        private void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
        private void OnCancel(InputAction.CallbackContext _) => CancelPressed?.Invoke();
        private void OnThrow(InputAction.CallbackContext _) => ThrowPressed?.Invoke();
        private void OnWhistle(InputAction.CallbackContext _) => WhistlePressed?.Invoke();
    }
}
