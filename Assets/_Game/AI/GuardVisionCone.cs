using Escape.Core;
using UnityEngine;

namespace Escape.AI
{
    /// <summary>
    /// Renders the guard's field of view as a flat fan on the floor, so
    /// patrol routes are readable at a glance. Tinted by brain state:
    /// calm amber, searching orange, alert red.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GuardVisionCone : MonoBehaviour
    {
        [SerializeField] private int segments = 20;
        [SerializeField] private GuardBrain brain;
        [SerializeField] private float height = 0.06f;
        [SerializeField] private float rangeScale = 0.55f;
        [SerializeField] private Color patrolColor = new Color(1f, 0.75f, 0.25f, 0.10f);
        [SerializeField] private Color searchColor = new Color(1f, 0.45f, 0.1f, 0.16f);
        [SerializeField] private Color alertColor = new Color(1f, 0.1f, 0.1f, 0.28f);

        private Data.StealthTuning _tuning;
        private MeshFilter _mf;
        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private float _builtRange = -1f, _builtFov = -1f;

        private void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            if (brain == null) brain = GetComponentInParent<GuardBrain>();
        }

        private void Start()
        {
            _tuning = GameRoot.Instance.Services.Get<IContentDatabase>().Tuning;
            if (_mr.sharedMaterial == null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = 3000;
                _mr.sharedMaterial = mat;
            }
        }

        private void LateUpdate()
        {
            if (_tuning == null || brain == null) return;

            float range = _tuning.GuardVisionRange * rangeScale;
            float fov = _tuning.GuardFovDegrees;
            if (_builtRange != range || _builtFov != fov) Rebuild(range, fov);

            // Keep the fan flat on the floor even as the guard's head bobs.
            transform.position = new Vector3(brain.transform.position.x, height,
                brain.transform.position.z);
            transform.rotation = Quaternion.Euler(0f, brain.transform.eulerAngles.y, 0f);

            var c = brain.State == GuardState.Alert ? alertColor
                : brain.State == GuardState.Investigate || brain.State == GuardState.Search
                    ? searchColor
                    : patrolColor;
            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetColor("_BaseColor", c);
            _mr.SetPropertyBlock(_mpb);
        }

        private void Rebuild(float range, float fovDeg)
        {
            _builtRange = range;
            _builtFov = fovDeg;
            var mesh = new Mesh { name = "GuardVisionCone" };
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            float half = fovDeg * 0.5f;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(-half, half, (float)i / segments);
                verts[i + 1] = Quaternion.Euler(0, a, 0) * Vector3.forward * range;
            }
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            if (_mf.sharedMesh != null) Destroy(_mf.sharedMesh);
            _mf.sharedMesh = mesh;
        }
    }
}
