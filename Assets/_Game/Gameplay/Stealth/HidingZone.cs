using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Trigger volume — closet, alcove, server rack shadow. Not full
    /// immunity: a guard in Alert inside the same zone still finds you.
    /// </summary>
        public sealed class HidingZone : MonoBehaviour
    {
        [Range(0.05f, 1f)]
        public float VisibilityOverride = 0.25f;

        public bool PlayerInside { get; private set; }

        private void Reset()
        {
            var c = GetComponent<Collider>();
            c.isTrigger = true;
            gameObject.layer = GameLayers.HidingZone;
        }

        private void OnTriggerEnter(Collider other)
        {
            var state = other.GetComponentInParent<PlayerState>();
            if (state == null) return;
            state.Concealed = true;
            state.CurrentHidingZone = this;
            PlayerInside = true;
        }

        private void OnTriggerExit(Collider other)
        {
            var state = other.GetComponentInParent<PlayerState>();
            if (state == null) return;
            state.Concealed = false;
            if (state.CurrentHidingZone == this) state.CurrentHidingZone = null;
            PlayerInside = false;
        }
    }
}
