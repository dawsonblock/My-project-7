using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Per-collider footstep noise multiplier. Falls back to physic-material
    /// name matching when no component is present.
    /// </summary>
    public sealed class SurfaceNoise : MonoBehaviour
    {
        [Range(0.1f, 3f)]
        public float Multiplier = 1f;

        public enum Category { Concrete, Carpet, Wood, Metal, Water, Glass }

        public static float MultiplierForName(string materialName)
        {
            switch (CategoryForName(materialName))
            {
                case Category.Carpet: return 0.45f;
                case Category.Wood: return 0.8f;
                case Category.Metal: return 1.5f;
                case Category.Water: return 1.8f;
                case Category.Glass: return 2.2f;
                default: return 1.0f; // concrete and default
            }
        }

        public static Category CategoryForName(string materialName)
        {
            var n = materialName.ToLowerInvariant();
            if (n.Contains("carpet")) return Category.Carpet;
            if (n.Contains("wood") || n.Contains("timber") || n.Contains("crate") || n.Contains("desk")) return Category.Wood;
            if (n.Contains("metal") || n.Contains("steel") || n.Contains("locker") || n.Contains("rack")) return Category.Metal;
            if (n.Contains("water")) return Category.Water;
            if (n.Contains("glass")) return Category.Glass;
            return Category.Concrete;
        }
    }
}
