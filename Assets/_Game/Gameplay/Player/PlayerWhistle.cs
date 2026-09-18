using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Whistle: emits a sharp noise at the player's position to bait a
    /// guard off its patrol. Free but dangerous — it tells the guard
    /// exactly where you were when you made it.
    /// </summary>
    public sealed class PlayerWhistle : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;

        private PlayerInputReader _input;
        private PlayerState _state;
        private IInputGate _gate;
        private INoiseService _noise;
        private Data.StealthTuning _tuning;
        private float _cooldownUntil;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _state = GetComponent<PlayerState>();
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
            _noise = services.Get<INoiseService>();
            _tuning = services.Get<IContentDatabase>().Tuning;
            if (_input != null) _input.WhistlePressed += OnWhistle;
            _bound = true;
        }

        private void OnDisable()
        {
            if (_input != null) _input.WhistlePressed -= OnWhistle;
            _bound = false;
        }

        private void OnWhistle()
        {
            if (_state.Caught || _gate.UiOpen || Time.time < _cooldownUntil) return;
            _cooldownUntil = Time.time + (_tuning != null ? _tuning.WhistleCooldown : 2.5f);
            if (audioSource != null)
            {
                if (audioSource.clip == null)
                    audioSource.clip = Data.ClipLibrary.Get()?.whistle;
                audioSource.Play();
            }
            float radius = _tuning != null ? _tuning.WhistleNoiseRadius : 9f;
            _noise.Emit(new NoiseEvent(transform.position, radius, 1.6f,
                NoiseType.Environmental, this));
        }
    }
}
