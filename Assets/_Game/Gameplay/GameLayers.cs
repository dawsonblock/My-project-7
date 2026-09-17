using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Named layers configured by the project bootstrapper in TagManager.
    /// </summary>
    public static class GameLayers
    {
        public const string WorldGeometryName = "WorldGeometry";
        public const string InteractableName = "Interactable";
        public const string PlayerName = "Player";
        public const string GuardName = "Guard";
        public const string SecurityName = "Security";
        public const string HidingZoneName = "HidingZone";
        public const string LightingZoneName = "LightingZone";

        public static int WorldGeometry => LayerMask.NameToLayer(WorldGeometryName);
        public static int Interactable => LayerMask.NameToLayer(InteractableName);
        public static int Player => LayerMask.NameToLayer(PlayerName);
        public static int Guard => LayerMask.NameToLayer(GuardName);
        public static int Security => LayerMask.NameToLayer(SecurityName);
        public static int HidingZone => LayerMask.NameToLayer(HidingZoneName);
        public static int LightingZone => LayerMask.NameToLayer(LightingZoneName);

        public static int InteractableMask => Mask(Interactable);
        public static int OcclusionMask => Mask(WorldGeometry) | Mask(Interactable);

        private static int Mask(int layer) => layer < 0 ? 0 : 1 << layer;
    }
}
