using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Camera-center raycast against the Interactable layer. Exposes the
    /// current target's prompt for the HUD and fires Interact on keypress.
    /// </summary>
    public sealed class PlayerInteraction : MonoBehaviour
    {
        private PlayerInputReader _input;
        private PlayerLook _look;
        private PlayerState _state;
        private PlayerContext _context;
        private IInputGate _gate;
        private float _distance = 2.6f;

        public IInteractable Current { get; private set; }
        public InteractionPrompt CurrentPrompt { get; private set; } = InteractionPrompt.None;
        public PlayerContext Context => _context;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _state = GetComponent<PlayerState>();
            _look = GetComponentInChildren<PlayerLook>();
        }

        private bool _bound;

        // Bind + subscribe on enable, unwind on disable — symmetric, so a
        // disable/enable cycle leaves the input subscription intact exactly
        // once. Start retries the bind in case GameRoot lagged scene load.
        private void OnEnable() => TryBind();
        private void Start() => TryBind();

        private void TryBind()
        {
            if (_bound || GameRoot.Instance == null) return;
            var services = GameRoot.Instance.Services;
            _gate = services.Get<IInputGate>();
            _distance = services.Get<IContentDatabase>().Tuning.InteractDistance;
            _context = new PlayerContext
            {
                Transform = transform,
                Camera = _look != null ? _look.Cam : GetComponentInChildren<Camera>(),
                State = _state,
                Services = services
            };
            if (_input != null) _input.InteractPressed += OnInteract;
            _bound = true;
        }

        private void OnDisable()
        {
            if (_input != null) _input.InteractPressed -= OnInteract;
            _bound = false;
        }

        private void Update()
        {
            if (_gate == null || _context == null) return;
            if (_state.Caught || _gate.UiOpen || _context.Camera == null)
            {
                Current = null;
                CurrentPrompt = InteractionPrompt.None;
                return;
            }

            var ray = new Ray(_context.Camera.transform.position, _context.Camera.transform.forward);
            IInteractable found = null;
            if (Physics.Raycast(ray, out var hit, _distance, GameLayers.InteractableMask, QueryTriggerInteraction.Collide))
                found = hit.collider.GetComponentInParent<IInteractable>();

            Current = found;
            CurrentPrompt = found != null ? found.GetPrompt(_context) : InteractionPrompt.None;
        }

        private void OnInteract()
        {
            if (Current != null && CurrentPrompt.Enabled && Current.CanInteract(_context))
                Current.Interact(_context);
        }
    }
}
