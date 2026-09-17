using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Throws a ThrowableLure along the camera aim when the Throw action
    /// fires and the player still carries lures (GameState.Lures).
    /// </summary>
    public sealed class PlayerThrower : MonoBehaviour
    {
        [SerializeField] private GameObject lurePrefab;
        [SerializeField] private AudioSource throwAudio;

        private PlayerInputReader _input;
        private PlayerState _state;
        private PlayerLook _look;
        private IInputGate _gate;
        private IGameStateService _gameState;
        private IGameEventBus _events;
        private INoiseService _noise;
        private Data.StealthTuning _tuning;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _state = GetComponent<PlayerState>();
            _look = GetComponentInChildren<PlayerLook>();
            if (throwAudio == null)
            {
                throwAudio = gameObject.AddComponent<AudioSource>();
                throwAudio.spatialBlend = 0f;
                throwAudio.playOnAwake = false;
            }
        }

        private void Start()
        {
            var services = GameRoot.Instance.Services;
            _gate = services.Get<IInputGate>();
            _gameState = services.Get<IGameStateService>();
            _events = services.Get<IGameEventBus>();
            _noise = services.Get<INoiseService>();
            _tuning = services.Get<IContentDatabase>().Tuning;
            _input.ThrowPressed += OnThrow;
        }

        private void OnDestroy()
        {
            if (_input != null) _input.ThrowPressed -= OnThrow;
        }

        private void OnThrow()
        {
            if (_state.Caught || _gate.UiOpen || lurePrefab == null) return;
            if (_gameState.State.Lures <= 0)
            {
                _events.Publish(new SystemMessageEvent("No lures left", 2f));
                return;
            }

            _gameState.State.Lures--;
            if (throwAudio != null)
            {
                if (throwAudio.clip == null)
                    throwAudio.clip = Data.ClipLibrary.Get()?.throwWhoosh;
                throwAudio.Play();
            }

            var cam = _look != null ? _look.Cam : GetComponentInChildren<Camera>();
            var dir = cam != null ? cam.transform.forward : transform.forward;
            var pos = (cam != null ? cam.transform.position : transform.position + Vector3.up * 1.5f)
                      + dir * 0.5f;

            var go = Instantiate(lurePrefab, pos, Quaternion.identity);
            var rb = go.GetComponent<Rigidbody>();
            rb.linearVelocity = dir * _tuning.ThrowForce + Vector3.up * _tuning.ThrowArc;
            rb.angularVelocity = Random.insideUnitSphere * 6f;

            // The throw itself is quiet; the impact is loud.
            _noise.Emit(new NoiseEvent(pos, 3f, 0.6f, NoiseType.Footstep, this));
        }
    }
}
