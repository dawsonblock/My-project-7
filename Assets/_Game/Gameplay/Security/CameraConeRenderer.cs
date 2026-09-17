using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Renders the camera's visible detection cone as a translucent mesh fan
    /// generated at runtime. Tints red as detection climbs.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CameraConeRenderer : MonoBehaviour
    {
        [SerializeField] private int segments = 24;
        [SerializeField] private CameraSensor sensor;
        [SerializeField] private Color idleColor = new Color(1f, 0.6f, 0.2f, 0.12f);
        [SerializeField] private Color alertColor = new Color(1f, 0.1f, 0.1f, 0.25f);
        [SerializeField] private Color disabledColor = new Color(0.2f, 0.6f, 1f, 0.05f);

        private Data.StealthTuning _tuning;
        private MeshFilter _mf;
        private MeshRenderer _mr;
        private bool _disabled;
        private float _builtRange = -1f, _builtFov = -1f;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            if (sensor == null) sensor = GetComponentInParent<CameraSensor>();
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

        public void SetDisabled(bool disabled) => _disabled = disabled;

        private void LateUpdate()
        {
            if (_tuning == null) return;
            if (_builtRange != _tuning.CameraRange || _builtFov != _tuning.CameraFovDegrees)
                Rebuild(_tuning.CameraRange, _tuning.CameraFovDegrees);

            float per = sensor != null && sensor.enabled ? sensor.Perception : 0f;
            var c = _disabled ? disabledColor : Color.Lerp(idleColor, alertColor, Mathf.Clamp01(per));
            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetColor("_BaseColor", c);
            _mr.SetPropertyBlock(_mpb);
        }

        private void Rebuild(float range, float fovDeg)
        {
            _builtRange = range;
            _builtFov = fovDeg;
            var mesh = new Mesh { name = "VisionCone" };
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
