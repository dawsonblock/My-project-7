using Escape.Gameplay;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Ensures project tags/layers exist for the game's interaction,
    /// occlusion and stealth queries.
    /// </summary>
    public static class ProjectSetup
    {
        private static readonly string[] Layers =
        {
            GameLayers.WorldGeometryName,
            GameLayers.InteractableName,
            GameLayers.PlayerName,
            GameLayers.GuardName,
            GameLayers.SecurityName,
            GameLayers.HidingZoneName,
            GameLayers.LightingZoneName
        };

        [MenuItem("Tools/Escape the Elites/Setup Layers")]
        public static void EnsureLayers()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");
            foreach (var layer in Layers)
            {
                if (LayerMask.NameToLayer(layer) >= 0) continue;
                for (int i = 8; i < layersProp.arraySize; i++)
                {
                    var slot = layersProp.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.stringValue))
                    {
                        slot.stringValue = layer;
                        break;
                    }
                }
            }
            tagManager.ApplyModifiedProperties();
            foreach (var layer in Layers)
                if (LayerMask.NameToLayer(layer) < 0)
                    Debug.LogWarning($"[ProjectSetup] Could not create layer '{layer}'.");
            Debug.Log("[ProjectSetup] Layers ensured.");
        }
    }
}
