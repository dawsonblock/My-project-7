using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Emits footstep NoiseEvents on a stride timer. Radius scales with
    /// movement mode and the surface under the player (SurfaceNoise
    /// component or physic material name).
    /// </summary>
    public sealed class PlayerNoiseEmitter : MonoBehaviour
    {
        private PlayerState _state;
        private PlayerMovement _movement;
        private CharacterController _cc;
        private INoiseService _noise;
        private Data.StealthTuning _tuning;
        private IInputGate _gate;
        private AudioSource _steps;

        private float _strideT;
        private float _strideInterval = 0.55f;

        private void Awake()
        {
            _state = GetComponent<PlayerState>();
            _movement = GetComponent<PlayerMovement>();
            _cc = GetComponent<CharacterController>();
            _steps = gameObject.AddComponent<AudioSource>();
            _steps.spatialBlend = 0f;
            _steps.playOnAwake = false;
        }

        private bool _bound;

        // Bind on enable; Start retries in case GameRoot lagged the scene
        // load. Update already guards on the bound services.
        private void OnEnable() => TryBind();
        private void Start() => TryBind();

        private void TryBind()
        {
            if (_bound || GameRoot.Instance == null) return;
            var services = GameRoot.Instance.Services;
            _noise = services.Get<INoiseService>();
            _tuning = services.Get<IContentDatabase>().Tuning;
            _gate = services.Get<IInputGate>();
            _bound = true;
        }

        private void OnDisable() => _bound = false;

        private void Update()
        {
            if (_gate == null || _tuning == null) return;
            if (_state.Caught || _gate.UiOpen || !_cc.isGrounded) return;
            float speed = new Vector3(_cc.velocity.x, 0, _cc.velocity.z).magnitude;
            if (speed < 0.3f) { _strideT = 0f; return; }

            _strideInterval = _state.Sprinting ? 0.32f : _state.Crouching ? 0.75f : 0.55f;
            _strideT += Time.deltaTime;
            if (_strideT < _strideInterval) return;
            _strideT = 0f;

            float moveMul = _state.Sprinting ? _tuning.SprintNoiseMultiplier
                : _state.Crouching ? _tuning.CrouchNoiseMultiplier
                : _tuning.WalkNoiseMultiplier;
            SampleSurface(out float surface, out var category);
            float radius = _tuning.FootstepBaseRadius * moveMul * surface;

            _noise.Emit(new NoiseEvent(transform.position, radius, moveMul,
                _state.Sprinting ? NoiseType.Sprint : NoiseType.Footstep, this));
            PlayStep(category);
        }

        private void PlayStep(SurfaceNoise.Category category)
        {
            var lib = Data.ClipLibrary.Get();
            if (lib == null || _steps == null) return;
            var set = category switch
            {
                SurfaceNoise.Category.Carpet => lib.stepsCarpet,
                SurfaceNoise.Category.Wood => lib.stepsWood,
                SurfaceNoise.Category.Metal => lib.stepsMetal,
                _ => lib.stepsConcrete,
            };
            var clip = Data.ClipLibrary.Pick(set);
            if (clip == null) return;
            _steps.pitch = Random.Range(0.9f, 1.1f);
            _steps.PlayOneShot(clip,
                _state.Crouching ? 0.1f : _state.Sprinting ? 0.5f : 0.28f);
        }

        private void SampleSurface(out float multiplier, out SurfaceNoise.Category category)
        {
            multiplier = 1f;
            category = SurfaceNoise.Category.Concrete;
            int mask = GameLayers.Player < 0 ? Physics.DefaultRaycastLayers
                : Physics.DefaultRaycastLayers & ~(1 << GameLayers.Player);
            if (!Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down,
                    out var hit, 2f, mask, QueryTriggerInteraction.Ignore))
                return;
            var surf = hit.collider.GetComponent<SurfaceNoise>();
            var mat = hit.collider.sharedMaterial;
            string name = mat != null ? mat.name : "";
            // Blockout colliders carry no PhysicMaterial — the render
            // material name (MAT_Concrete, MAT_Carpet, ...) is the signal.
            if (string.IsNullOrEmpty(name))
            {
                var rend = hit.collider.GetComponent<MeshRenderer>();
                if (rend != null && rend.sharedMaterial != null)
                    name = rend.sharedMaterial.name;
            }
            multiplier = surf != null ? surf.Multiplier : SurfaceNoise.MultiplierForName(name);
            category = SurfaceNoise.CategoryForName(name);
        }
    }
}
