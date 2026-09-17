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
        private InputAction _move, _look, _interact, _sprint, _crouch;
        private InputAction _leanLeft, _leanRight, _flashlight, _board, _pause;
        private InputAction _throw, _whistle, _focus;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool SprintHeld { get; private set; }

        public event System.Action InteractPressed;
        public event System.Action CrouchPressed;
        public event System.Action FlashlightPressed;
        public event System.Action EvidenceBoardPressed;
        public event System.Action PausePressed;
        public event System.Action ThrowPressed;
        public event System.Action WhistlePressed;
        public bool LeanLeft { get; private set; }
        public bool LeanRight { get; private set; }
        public bool FocusHeld { get; private set; }

        private Escape.Core.IInputGate _gate;

        private void Awake()
        {
            _playerMap = actions.FindActionMap("Player", true);
            _uiMap = actions.FindActionMap("UI", true);
            _move = _playerMap.FindAction("Move", true);
            _look = _playerMap.FindAction("Look", true);
            _interact = _playerMap.FindAction("Interact", true);
            _sprint = _playerMap.FindAction("Sprint", true);
            _crouch = _playerMap.FindAction("Crouch", true);
            _leanLeft = _playerMap.FindAction("LeanLeft", true);
            _leanRight = _playerMap.FindAction("LeanRight", true);
            _flashlight = _playerMap.FindAction("Flashlight", true);
            _board = _playerMap.FindAction("EvidenceBoard", true);
            _pause = _playerMap.FindAction("Pause", true);
            _throw = _playerMap.FindAction("Throw", true);
            _whistle = _playerMap.FindAction("Whistle", true);
            _focus = _playerMap.FindAction("Focus", true);
        }

        private void OnEnable()
        {
            // Enable at the asset level — map-level Enable() can hit a stale
            // map index if the imported asset was reimported mid-session.
            actions.Enable();
            _uiMap.Disable();
            _interact.performed += OnInteract;
            _crouch.performed += OnCrouch;
            _flashlight.performed += OnFlashlight;
            _board.performed += OnBoard;
            _pause.performed += OnPause;
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
            _playerMap.Disable();
            _interact.performed -= OnInteract;
            _crouch.performed -= OnCrouch;
            _flashlight.performed -= OnFlashlight;
            _board.performed -= OnBoard;
            _pause.performed -= OnPause;
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
            Move = _move.ReadValue<Vector2>();
            Look = _look.ReadValue<Vector2>();
            SprintHeld = _sprint.IsPressed();
            LeanLeft = _leanLeft.IsPressed();
            LeanRight = _leanRight.IsPressed();
            FocusHeld = _focus.IsPressed();
        }

        private void OnInteract(InputAction.CallbackContext _) => InteractPressed?.Invoke();
        private void OnCrouch(InputAction.CallbackContext _) => CrouchPressed?.Invoke();
        private void OnFlashlight(InputAction.CallbackContext _) => FlashlightPressed?.Invoke();
        private void OnBoard(InputAction.CallbackContext _) => EvidenceBoardPressed?.Invoke();
        private void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
        private void OnThrow(InputAction.CallbackContext _) => ThrowPressed?.Invoke();
        private void OnWhistle(InputAction.CallbackContext _) => WhistlePressed?.Invoke();
    }
}
