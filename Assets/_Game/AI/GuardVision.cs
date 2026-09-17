using Escape.Core;
using Escape.Gameplay;
using UnityEngine;

namespace Escape.AI
{
    /// <summary>
    /// Guard eyes: range + FOV + raycast + visibility multiplier. Feeds the
    /// shared detection service and reports sightings to GuardBrain.
    /// </summary>
    public sealed class GuardVision : MonoBehaviour
    {
        [SerializeField] private Transform eye;

        private IDetectionService _detection;
        private Data.StealthTuning _tuning;
        private GuardBrain _brain;
        private Transform _player;
        private PlayerVisibility _visibility;
        private PlayerState _playerState;
        private int _mask;

        /// <summary>Continuous 0..1 perception this frame.</summary>
        public float Perception { get; private set; }
        /// <summary>World position where the player was last clearly seen.</summary>
        public Vector3 LastKnownPosition { get; private set; }
        public float TimeSinceSeen { get; private set; } = float.MaxValue;

        private void Awake()
        {
            _brain = GetComponent<GuardBrain>();
            if (eye == null) eye = transform;
        }

        private void Start()
        {
            var services = GameRoot.Instance.Services;
            _detection = services.Get<IDetectionService>();
            _tuning = services.Get<IContentDatabase>().Tuning;
            _mask = GameLayers.OcclusionMask;
            if (_mask == 0) _mask = Physics.DefaultRaycastLayers;
        }

        private void Update()
        {
            Perception = 0f;
            TimeSinceSeen += Time.deltaTime;
            var player = FindPlayer();
            if (player == null || _playerState.Caught) return;

            Vector3 target = player.position + Vector3.up * 1.4f;
            Vector3 to = target - eye.position;
            float dist = to.magnitude;
            if (dist > _tuning.GuardVisionRange) return;
            if (Vector3.Angle(eye.forward, to) > _tuning.GuardFovDegrees * 0.5f && dist > 1.5f) return;
            if (_playerState.Concealed && dist > 2f && _brain.State != GuardState.Alert) return;

            if (Physics.Raycast(eye.position, to.normalized, out var hit, dist, _mask,
                    QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<PlayerState>() == null)
                    return;

            float visibility = _visibility != null ? _visibility.Current : 1f;
            float proximity = 1f + (1f - dist / _tuning.GuardVisionRange);
            Perception = visibility * proximity;
            LastKnownPosition = player.position;
            TimeSinceSeen = 0f;
            _detection.Submit(name, DetectionType.GuardVision,
                _tuning.GuardDetectionPerSecond * Perception);
        }

        private Transform FindPlayer()
        {
            if (_player != null) return _player;
            var state = FindAnyObjectByType<PlayerState>();
            if (state != null)
            {
                _player = state.transform;
                _playerState = state;
                _visibility = state.GetComponent<PlayerVisibility>();
            }
            return _player;
        }
    }
}
