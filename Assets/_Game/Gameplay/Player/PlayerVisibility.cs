using System.Collections.Generic;
using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Computes the player's visibility multiplier from lighting zones,
    /// crouching, flashlight and hiding spots. Sensors multiply their
    /// detection rate by this value. Approximate by design — no per-pixel
    /// light sampling in the first version.
    /// </summary>
    public sealed class PlayerVisibility : MonoBehaviour
    {
        public enum Exposure { Bright, Normal, Dim, Dark }

        private static readonly List<PlayerVisibility> _all = new List<PlayerVisibility>();

        private PlayerState _state;
        private FlashlightController _flashlight;
        private Data.StealthTuning _tuning;
        private readonly HashSet<LightingZone> _zones = new HashSet<LightingZone>();

        public float Current { get; private set; } = 1f;

        private void Awake()
        {
            _state = GetComponent<PlayerState>();
            _flashlight = GetComponentInChildren<FlashlightController>();
        }

        private void Start()
        {
            _tuning = GameRoot.Instance.Services.Get<IContentDatabase>().Tuning;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public void EnterZone(LightingZone zone) => _zones.Add(zone);
        public void ExitZone(LightingZone zone) => _zones.Remove(zone);

        private void Update()
        {
            if (_tuning == null || _state == null) return;
            float v = _tuning.NormalVisibility;

            // Darkest lighting zone wins.
            foreach (var z in _zones)
            {
                float zv = z.ExposureLevel switch
                {
                    Exposure.Bright => _tuning.BrightVisibility,
                    Exposure.Dim => _tuning.DimVisibility,
                    Exposure.Dark => _tuning.DarkVisibility,
                    _ => _tuning.NormalVisibility
                };
                v = Mathf.Min(v, zv);
            }

            if (_state.Crouching) v *= _tuning.CrouchVisibilityBonus;
            if (_flashlight != null && _flashlight.On) v *= _tuning.FlashlightVisibilityPenalty;
            if (_state.Concealed) v = Mathf.Min(v, _tuning.HidingVisibility);

            Current = v;
            _state.VisibilityMultiplier = v;
        }
    }
}
