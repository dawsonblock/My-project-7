using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Deterministic light animation. Flicker = perlin-noise intensity jitter
    /// (fluorescent hum); Pulse = smooth sine swell (warning beacons);
    /// Buzz = flicker plus occasional hard dropouts (dying tube).
    /// Driven purely by Time.time + serialized seed — no RNG state, so the
    /// same scene always animates identically.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightFlicker : MonoBehaviour
    {
        public enum Mode { Flicker, Pulse, Buzz }

        public Mode FlickerMode = Mode.Flicker;
        [Range(0f, 1f)] public float Depth = 0.35f;
        public float Speed = 9f;
        public float Seed = 1f;

        private Light _light;
        private float _base;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
        }

        private void Update()
        {
            float t = Time.time * Speed + Seed * 97.31f;
            float k;
            switch (FlickerMode)
            {
                case Mode.Pulse:
                    k = 0.5f + 0.5f * Mathf.Sin(t);
                    break;
                case Mode.Buzz:
                    k = Mathf.PerlinNoise(t, Seed);
                    // Dropout: when a slow second perlin dips, the tube cuts out.
                    if (Mathf.PerlinNoise(Seed * 3.7f, t * 0.13f) < 0.18f) k *= 0.05f;
                    break;
                default:
                    k = Mathf.PerlinNoise(t, Seed);
                    break;
            }
            _light.intensity = _base * Mathf.Lerp(1f - Depth, 1f, k);
        }
    }
}
