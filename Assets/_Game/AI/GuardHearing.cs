using Escape.Core;
using UnityEngine;

namespace Escape.AI
{
    /// <summary>
    /// Subscribes to the global NoiseService and forwards stimuli the guard
    /// can hear to the brain. Hearing never directly detects the player —
    /// it produces investigation targets.
    /// </summary>
    public sealed class GuardHearing : MonoBehaviour
    {
        private GuardBrain _brain;
        private INoiseService _noise;
        private Data.StealthTuning _tuning;

        private void Awake() => _brain = GetComponent<GuardBrain>();

        private void OnEnable()
        {
            if (GameRoot.Instance == null) return;
            _noise = GameRoot.Instance.Services.Get<INoiseService>();
            _tuning = GameRoot.Instance.Services.Get<IContentDatabase>().Tuning;
            _noise.OnNoise += OnNoise;
        }

        private void OnDisable()
        {
            if (_noise != null) _noise.OnNoise -= OnNoise;
        }

        private void OnNoise(NoiseEvent n)
        {
            if (n.Source == (Object)this || n.Source == (Object)_brain) return;
            float radius = n.Radius * (_tuning != null ? _tuning.GuardHearingRadiusMultiplier : 1f);
            float dist = Vector3.Distance(transform.position, n.Position);
            if (dist > radius) return;
            float strength = n.Strength * (1f - dist / radius);
            _brain.HearNoise(n.Position, strength, n.Type);
        }
    }
}
