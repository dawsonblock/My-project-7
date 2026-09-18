using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Oscillating yaw sweep on the camera head.
    /// </summary>
    public sealed class CameraMotor : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private float centerYaw;

        private Data.StealthTuning _tuning;
        private float _t;

        // Bind on enable; Start retries in case GameRoot lagged the scene
        // load. _tuning doubles as the bound flag — Update guards on it.
        private void OnEnable() => TryBind();

        private void Start()
        {
            TryBind();
            if (head == null) head = transform;
            centerYaw = head.localEulerAngles.y;
        }

        private void TryBind()
        {
            if (_tuning != null || GameRoot.Instance == null) return;
            _tuning = GameRoot.Instance.Services.Get<IContentDatabase>().Tuning;
        }

        private void Update()
        {
            if (_tuning == null || head == null) return;
            _t += Time.deltaTime;
            float phase = Mathf.Sin(_t * Mathf.PI * 2f / _tuning.CameraSweepPeriod);
            var e = head.localEulerAngles;
            e.y = centerYaw + phase * _tuning.CameraSweepDegrees;
            head.localEulerAngles = e;
        }
    }
}
