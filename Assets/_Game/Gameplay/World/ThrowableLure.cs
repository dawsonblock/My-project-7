using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// A thrown bottle/brick. Flies under physics, emits a loud
    /// NoiseType.ThrownObject event on first impact, then becomes a
    /// pickup so the lure can be recovered and reused.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class ThrowableLure : MonoBehaviour, IInteractable, IWorldObject
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private string lureId = "";
        [SerializeField] private float minImpactSpeed = 1.2f;
        [SerializeField] private float maxImpactSounds = 2f;

        private INoiseService _noise;
        private Data.StealthTuning _tuning;
        private IWorldService _world;
        private Rigidbody _rb;
        private bool _impacted;
        private int _impacts;

        public string Id => lureId;

        private void Awake() => _rb = GetComponent<Rigidbody>();

        // Bind + register on enable, unregister on disable — symmetric, so a
        // disable/enable cycle leaves the lure registered exactly once.
        // Start retries the bind in case GameRoot lagged behind scene load.
        private void OnEnable() => TryBind();
        private void Start() => TryBind();

        private void TryBind()
        {
            if (_bound || GameRoot.Instance == null) return;
            var services = GameRoot.Instance.Services;
            _noise = services.Get<INoiseService>();
            _tuning = services.Get<IContentDatabase>().Tuning;
            // Only scene-placed lures (those with an id) register with the
            // world — thrown instances are transient.
            if (!string.IsNullOrEmpty(lureId))
            {
                _world = services.Get<IWorldService>();
                _world.Register(this);
            }
            _bound = true;
        }

        private bool _bound;

        private void OnDisable()
        {
            _world?.Unregister(this);
            _world = null;
            _bound = false;
        }

        public void RestoreFromState(GameState state)
        {
            if (!string.IsNullOrEmpty(lureId) && state.CollectedLures.Contains(lureId))
                gameObject.SetActive(false);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.magnitude < minImpactSpeed) return;
            _impacted = true;
            if (audioSource != null)
            {
                var clip = Data.ClipLibrary.Pick(Data.ClipLibrary.Get()?.glassImpacts);
                if (clip != null) audioSource.PlayOneShot(clip);
                else audioSource.Play();
            }
            if (_impacts >= maxImpactSounds) return;
            _impacts++;
            float radius = _tuning != null ? _tuning.ThrownNoiseRadius : 14f;
            _noise?.Emit(new NoiseEvent(transform.position, radius, 2.2f,
                NoiseType.ThrownObject, this));
        }

        // Recoverable once it has landed (impacted) or simply come to rest —
        // scene-placed pickups never take a hard impact.
        private bool Recoverable =>
            _impacted || (_rb != null && _rb.linearVelocity.sqrMagnitude < 0.25f);

        private bool AlreadyCollected(PlayerContext player) =>
            !string.IsNullOrEmpty(lureId) && player.GameState.CollectedLures.Contains(lureId);

        public InteractionPrompt GetPrompt(PlayerContext player)
        {
            if (AlreadyCollected(player)) return InteractionPrompt.None;
            if (!Recoverable) return InteractionPrompt.None;
            if (player.GameState.Lures >= (_tuning != null ? _tuning.MaxLures : 6))
                return new InteractionPrompt("Lure — pockets full", false);
            return new InteractionPrompt("[E] Pick up lure");
        }

        public bool CanInteract(PlayerContext player) =>
            !AlreadyCollected(player) && Recoverable
            && player.GameState.Lures < (_tuning != null ? _tuning.MaxLures : 6);

        public void Interact(PlayerContext player)
        {
            if (!string.IsNullOrEmpty(lureId))
                player.GameState.CollectedLures.Add(lureId);
            player.GameState.Lures++;
            if (player.Services.TryGet<IAudioService>(out var audio))
                audio.Play2D(Data.ClipLibrary.Get()?.pickup, 0.8f);
            Destroy(gameObject);
        }
    }
}
