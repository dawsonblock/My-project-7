using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Applies AmbientCG PBR maps to the generated blockout materials.
    /// Runs after scene building (mats must exist first); the .mat assets
    /// are shared so every scene picks the textures up without a rebuild.
    /// Roughness/metalness maps are skipped — URP reads smoothness from the
    /// metallic-gloss alpha, and these sources ship separate roughness maps
    /// that would need channel repacking. Scalar values stay authoritative.
    /// </summary>
    public static class MaterialLibrary
    {
        private const string TexRoot = "Assets/_Game/Art/Textures";

        [MenuItem("Tools/Escape the Elites/Apply PBR Textures")]
        public static void ApplyAll()
        {
            Wire("MAT_Concrete", "Concrete034", 5f, new Color(0.72f, 0.72f, 0.75f));
            Wire("MAT_Concrete_Dark", "Concrete034", 5f, new Color(0.38f, 0.39f, 0.44f));
            Wire("MAT_Metal_Dark", "CorrugatedSteel009", 3f, new Color(0.5f, 0.54f, 0.58f));
            Wire("MAT_Locker", "CorrugatedSteel009", 2f, new Color(0.45f, 0.58f, 0.5f));
            Wire("MAT_Fence", "CorrugatedSteel009", 4f, new Color(0.5f, 0.52f, 0.55f));
            Wire("MAT_Wood", "WoodFloor043", 3f, new Color(0.7f, 0.55f, 0.4f));
            Wire("MAT_Crate", "WoodFloor043", 1f, new Color(0.8f, 0.66f, 0.48f));
            Wire("MAT_DeskTop", "WoodFloor043", 1f, new Color(0.6f, 0.45f, 0.32f));
            Wire("MAT_Carpet", "Carpet016", 4f, new Color(0.5f, 0.33f, 0.28f));
            AssetDatabase.SaveAssets();
            Debug.Log("[MaterialLibrary] PBR textures applied.");
        }

        private static void Wire(string matName, string texSet, float tiling, Color tint)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{Blockout.MatDir}/{matName}.mat");
            if (mat == null) return;
            var dir = $"{TexRoot}/{texSet}";
            var color = LoadTex($"{dir}/{texSet}_1K-JPG_Color.jpg");
            var normal = LoadNormal($"{dir}/{texSet}_1K-JPG_NormalGL.jpg");
            var ao = LoadTex($"{dir}/{texSet}_1K-JPG_AmbientOcclusion.jpg");

            mat.SetColor("_BaseColor", tint);
            if (color != null)
            {
                mat.SetTexture("_BaseMap", color);
                mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            }
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                mat.EnableKeyword("_NORMALMAP");
            }
            if (ao != null) mat.SetTexture("_OcclusionMap", ao);
            EditorUtility.SetDirty(mat);
        }

        private static Texture2D LoadTex(string path) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        private static Texture2D LoadNormal(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter imp &&
                imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
