using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Trigger volume marking a region's light level. PlayerVisibility
    /// samples these to pick its exposure multiplier.
    /// </summary>
        public sealed class LightingZone : MonoBehaviour
    {
        public PlayerVisibility.Exposure ExposureLevel = PlayerVisibility.Exposure.Normal;

        private void Reset()
        {
            var c = GetComponent<Collider>();
            c.isTrigger = true;
            gameObject.layer = GameLayers.LightingZone;
        }

        private void OnTriggerEnter(Collider other)
        {
            other.GetComponentInParent<PlayerVisibility>()?.EnterZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            other.GetComponentInParent<PlayerVisibility>()?.ExitZone(this);
        }
    }
}
