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

        private Vector3 _restPos;
        private Quaternion _restRot;

        private void Awake()
        {
            _restPos = transform.localPosition;
            _restRot = transform.localRotation;
        }

        private void Update()
        {
            float s = Mathf.Sin(Time.time * Speed + Phase);
            transform.localRotation = _restRot * Quaternion.Euler(RotationAmplitude * s);
            if (BobAmplitude > 0f)
                transform.localPosition = _restPos + Vector3.up * (BobAmplitude * s);
        }
    }
}
