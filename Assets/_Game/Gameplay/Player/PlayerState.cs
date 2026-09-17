using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Shared player flags read by movement, visibility, noise and AI.
    /// </summary>
    public sealed class PlayerState : MonoBehaviour
    {
        public bool Crouching { get; set; }
        public bool Sprinting { get; set; }
        public float Stamina { get; set; } = 100f;
        public bool Exhausted { get; set; }
        public bool Concealed { get; set; }      // inside a hiding spot
        public bool Caught { get; set; }
        public float VisibilityMultiplier { get; set; } = 1f;
        public Escape.Gameplay.HidingZone CurrentHidingZone { get; set; }
    }
}
