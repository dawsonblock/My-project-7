using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Deterministic ambient transform motion for set dressing: pendulum sway
    /// (hanging chains, clock pendulums, tarp edges) and slow vertical bob
    /// (water plane, floating debris). Position/rotation oscillate around the
    /// captured rest pose; phase offsets keep neighbours out of sync.
    /// </summary>
    public sealed class AmbientMotion : MonoBehaviour
    {
        public Vector3 RotationAmplitude = Vector3.zero;
        public float BobAmplitude;
        public float Speed = 1f;
        public float Phase;

        /// <summary>Continuous rotation (deg/s) — fans, dishes. Overrides sway.</summary>
        public Vector3 SpinAxis = Vector3.zero;
        public float SpinSpeed;

        private Vector3 _restPos;
        private Quaternion _restRot;

        private void Awake()
        {
            _restPos = transform.localPosition;
            _restRot = transform.localRotation;
        }

        private void Update()
        {
            if (SpinSpeed != 0f)
            {
                transform.localRotation = _restRot *
                    Quaternion.AngleAxis(Time.time * SpinSpeed + Phase, SpinAxis.normalized);
            }
            else
            {
                float s = Mathf.Sin(Time.time * Speed + Phase);
                transform.localRotation = _restRot * Quaternion.Euler(RotationAmplitude * s);
            }
            if (BobAmplitude > 0f)
            {
                float b = Mathf.Sin(Time.time * Speed + Phase);
                transform.localPosition = _restPos + Vector3.up * (BobAmplitude * b);
            }
        }
    }
}
