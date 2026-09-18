using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Real line-of-sight detection: range test → FOV test → physics raycast
    /// against world geometry → visibility multiplier → detection
    /// contribution submitted to the shared DetectionService.
    /// </summary>
    public sealed class CameraSensor : MonoBehaviour
    {
        [SerializeField] private Transform eye;
        [SerializeField] private string sourceId;

        private IDetectionService _detection;
        private Data.StealthTuning _tuning;
        private Transform _player;
        private PlayerVisibility _visibility;
        private PlayerState _playerState;
        private int _occlusionMask;
        private bool _bound;

        /// <summary>0..1 — how strongly the player is currently perceived.</summary>
        public float Perception { get; private set; }

        private void Awake()
        {
            if (string.IsNullOrEmpty(sourceId)) sourceId = name;
            if (eye == null) eye = transform;
        }

        // Bind on enable; Start retries in case GameRoot lagged the scene
        // load. Unbound sensor contributes nothing rather than NRE-ing.
        private void OnEnable() => TryBind();
        private void Start() => TryBind();

        private void TryBind()
        {
            if (_bound || GameRoot.Instance == null) return;
            var services = GameRoot.Instance.Services;
            _detection = services.Get<IDetectionService>();
            _tuning = services.Get<IContentDatabase>().Tuning;
            _occlusionMask = GameLayers.OcclusionMask;
            if (_occlusionMask == 0) _occlusionMask = Physics.DefaultRaycastLayers;
            _bound = true;
        }

        private void OnDisable() => _bound = false;

        private void Update()
        {
            Perception = 0f;
            if (!_bound) return;
            var player = FindPlayer();
            if (player == null) return;

            Vector3 target = player.position + Vector3.up * 1.2f; // chest height
            Vector3 to = target - eye.position;
            float dist = to.magnitude;
            if (dist > _tuning.CameraRange) return;

            if (Vector3.Angle(eye.forward, to) > _tuning.CameraFovDegrees * 0.5f) return;

            // Concealed players in hiding zones are only seen at arm's length.
            if (_playerState != null && _playerState.Concealed && dist > 2f) return;

            if (Physics.Raycast(eye.position, to.normalized, out var hit, dist, _occlusionMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<PlayerState>() == null)
                    return; // wall/door blocks line of sight
            }

            float visibility = _visibility != null ? _visibility.Current : 1f;
            // Closer = faster detection.
            float proximity = 1f + (1f - dist / _tuning.CameraRange) * 0.8f;
            Perception = visibility * proximity;
            _detection.Submit(sourceId, DetectionType.Camera,
                _tuning.CameraDetectionPerSecond * Perception);
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

        private void OnDrawGizmosSelected()
        {
            var e = eye != null ? eye : transform;
            var tuning = GameRoot.Instance != null
                ? GameRoot.Instance.Services.Get<IContentDatabase>().Tuning : null;
            float range = tuning != null ? tuning.CameraRange : 14f;
            float fov = tuning != null ? tuning.CameraFovDegrees : 62f;
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.9f);
            Vector3 left = Quaternion.Euler(0, -fov * 0.5f, 0) * e.forward;
            Vector3 right = Quaternion.Euler(0, fov * 0.5f, 0) * e.forward;
            Gizmos.DrawLine(e.position, e.position + left * range);
            Gizmos.DrawLine(e.position, e.position + right * range);
            Gizmos.DrawLine(e.position, e.position + e.forward * range);
        }
    }
}
