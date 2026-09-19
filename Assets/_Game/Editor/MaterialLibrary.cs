using System.IO;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Applies AmbientCG PBR maps to the generated blockout materials.
    /// Runs after scene building (mats must exist first); the .mat assets
    /// are shared so every scene picks the textures up without a rebuild.
    ///
    /// Roughness/metalness are repacked into URP's metallic-smoothness map
    /// (R = metallic, A = 1 - roughness) and cached next to the sources, so
    /// surfaces get real metal/rough response instead of one scalar.
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
            Wire("MAT_Paving", "PavingStones128", 6f, new Color(0.62f, 0.63f, 0.66f));

            // Structural materials that were still flat colour.
            Wire("MAT_Metal_Pipe", "CorrugatedSteel009", 2f, new Color(0.5f, 0.53f, 0.57f));
            Wire("MAT_Metal_Grate", "CorrugatedSteel009", 4f, new Color(0.42f, 0.45f, 0.48f));
            Wire("MAT_Rust", "CorrugatedSteel009", 2f, new Color(0.62f, 0.34f, 0.18f));
            Wire("MAT_Wood_Planks", "WoodFloor043", 2f, new Color(0.66f, 0.5f, 0.36f));
            Wire("MAT_Carpet_Rug", "Carpet016", 3f, new Color(0.55f, 0.36f, 0.3f));

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
            WireMetallicSmoothness(mat, texSet, tiling);
            EditorUtility.SetDirty(mat);
        }

        /// <summary>
        /// Wires URP's metallic-smoothness map. The sources ship roughness and
        /// metalness as separate greyscale maps; URP samples metallic from R
        /// and smoothness from A, so they have to be combined into one texture.
        /// </summary>
        private static void WireMetallicSmoothness(Material mat, string texSet, float tiling)
        {
            var dir = $"{TexRoot}/{texSet}";
            var outPath = $"{dir}/{texSet}_1K-JPG_MetallicSmoothness.png";
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            if (ms == null)
                ms = BuildMetallicSmoothness(
                    $"{dir}/{texSet}_1K-JPG_Metalness.jpg",
                    $"{dir}/{texSet}_1K-JPG_Roughness.jpg", outPath);
            if (ms == null) return;

            mat.SetTexture("_MetallicGlossMap", ms);
            mat.SetTextureScale("_MetallicGlossMap", new Vector2(tiling, tiling));
            // URP computes metallic = _Metallic * map.r and
            // smoothness = _Smoothness * map.a, so both scalars have to be 1
            // or they scale the maps back down and the pass does nothing.
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Smoothness", 1f);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        }

        /// <summary>
        /// Builds the packed map from the source JPGs, or returns the cached
        /// one. Decoding is CPU-side (LoadImage) so the result is bit-stable —
        /// the determinism gate diffs these files.
        ///
        /// Metalness is optional: concrete, carpet and paving are dielectrics
        /// and the sources ship no metalness map for them. Roughness alone
        /// still drives the specular response, which is the point of the pass.
        /// </summary>
        private static Texture2D BuildMetallicSmoothness(string metalPath, string roughPath, string outPath)
        {
            if (!File.Exists(roughPath)) return null;

            var rough = Decode(roughPath);
            if (rough == null) return null;

            var metal = File.Exists(metalPath) ? Decode(metalPath) : null;
            if (metal != null && (metal.width != rough.width || metal.height != rough.height))
            {
                Object.DestroyImmediate(metal);
                metal = null;
            }

            int w = rough.width;
            int h = rough.height;
            var rp = rough.GetPixels();
            var mp = metal != null ? metal.GetPixels() : null;
            var packed = new Color[w * h];
            for (int i = 0; i < packed.Length; i++)
            {
                float m = mp != null ? mp[i].r : 0f;
                packed[i] = new Color(m, m, m, 1f - rp[i].r);
            }

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, true, true);
            outTex.SetPixels(packed);
            outTex.Apply();
            File.WriteAllBytes(outPath, outTex.EncodeToPNG());

            Object.DestroyImmediate(metal);
            Object.DestroyImmediate(rough);
            Object.DestroyImmediate(outTex);

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(outPath) is TextureImporter imp)
            {
                // Metallic/smoothness are data, not colour — no sRGB decode.
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = false;
                imp.alphaIsTransparency = false;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        /// <summary>Readable CPU copy of an imported texture, without touching its importer.</summary>
        private static Texture2D Decode(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(tex);
                return null;
            }
            return tex;
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
