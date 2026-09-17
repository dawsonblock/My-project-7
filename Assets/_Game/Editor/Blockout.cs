using System;
using Escape.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Escape.EditorTools
{
    /// <summary>
    /// Shared construction helpers for generated scenes and prefabs:
    /// materials, primitives, serialized-field wiring.
    /// </summary>
    public static class Blockout
    {
        public const string MatDir = "Assets/_Game/Art/Materials";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        public static Material Mat(string name, Color color, float metallic = 0f,
            float smoothness = 0.35f, bool emissive = false)
        {
            EnsureFolder(MatDir);
            var path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 2.5f);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static Material VisionConeMat()
        {
            EnsureFolder(MatDir);
            var path = $"{MatDir}/MAT_VisionCone.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", new Color(1f, 0.6f, 0.2f, 0.12f));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size,
            Material mat = null, string layer = null, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!string.IsNullOrEmpty(layer))
            {
                int l = LayerMask.NameToLayer(layer);
                if (l >= 0) go.layer = l;
            }
            return go;
        }

        /// <summary>Set a [SerializeField] field via SerializedObject.</summary>
        public static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[Blockout] No field '{field}' on {target.GetType().Name}");
                return;
            }
            switch (value)
            {
                case Object o: p.objectReferenceValue = o; break;
                case string s: p.stringValue = s; break;
                case bool b: p.boolValue = b; break;
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case Vector3 v: p.vector3Value = v; break;
                case Enum e: p.intValue = Convert.ToInt32(e); break;
                case null: p.objectReferenceValue = null; break;
                default: Debug.LogWarning($"[Blockout] Unhandled value type {value.GetType().Name}"); break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static T LoadAsset<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);

        public static T LoadDefinition<T>(string prefix, string id) where T : ScriptableObject
        {
            var typeName = typeof(T).Name;
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeName}", new[] { "Assets/_Game/Resources/Definitions" }))
            {
                var so = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                var f = typeof(T).GetField("Id");
                if (so != null && (string)f.GetValue(so) == id) return so;
            }
            Debug.LogWarning($"[Blockout] Definition not found: {typeName} '{id}' — run Import Web Content first.");
            return null;
        }
    }
}
