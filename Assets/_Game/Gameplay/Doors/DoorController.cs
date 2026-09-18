using System.Collections;
using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    public enum DoorState { Closed, Opening, Open, Closing, Locked, Disabled }
    public enum DoorRequirementKind { None, Evidence, Objective, Insight, RemoteOnly }

    /// <summary>
    /// Sliding door with access requirements checked against GameState.
    /// Registers with the WorldService so terminal commands can reach it and
    /// so unlocked state persists across scene reloads.
    /// </summary>
    public sealed class DoorController : MonoBehaviour, IDoorObject
    {
        [SerializeField] private string id;
        [SerializeField] private Transform panel;
        [SerializeField] private Vector3 openOffset = new Vector3(0, 0, 0);
        [SerializeField] private float slideDuration = 0.7f;
        [SerializeField] private DoorRequirementKind requirement = DoorRequirementKind.None;
        [SerializeField] private string requirementId = "";
        [SerializeField] private string lockedText = "Locked";
        [SerializeField] private bool startOpen;
        [SerializeField] private AudioSource audioSource;

        private IGameStateService _state;
        private IGameCommandDispatcher _dispatcher;
        private INoiseService _noise;
        private Data.StealthTuning _tuning;
        private IWorldService _world;
        private Vector3 _closedPos;
        private Coroutine _anim;

        public string Id => id;
        public DoorState State { get; private set; } = DoorState.Closed;
        public string LockedText => lockedText;

        public bool Unlocked =>
            State == DoorState.Open || State == DoorState.Opening ||
            (_state != null && _state.State.UnlockedDoors.Contains(id));

        public bool RequirementMet
        {
            get
            {
                if (_state == null) return false; // unbound — fail closed
                var s = _state.State;
                switch (requirement)
                {
                    case DoorRequirementKind.Evidence: return s.CollectedEvidence.Contains(requirementId);
                    case DoorRequirementKind.Objective: return s.CompletedObjectives.Contains(requirementId);
                    case DoorRequirementKind.Insight: return s.GainedInsights.Contains(requirementId);
                    case DoorRequirementKind.RemoteOnly: return false; // terminal unlock only
                    default: return true;
                }
            }
        }

        private void Awake()
        {
            _closedPos = panel != null ? panel.localPosition : Vector3.zero;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource != null && audioSource.clip == null)
                audioSource.clip = Data.ClipLibrary.Get()?.doorClunk;
        }

        // Bind + register on enable, unregister on disable — symmetric, so a
        // disable/enable cycle leaves the door registered exactly once.
        // Start retries the bind in case GameRoot lagged behind scene load.
        private void OnEnable() => TryBind();
        private void Start() => TryBind();

        private void TryBind()
        {
            if (_world != null || GameRoot.Instance == null) return;
            var services = GameRoot.Instance.Services;
            _state = services.Get<IGameStateService>();
            _dispatcher = services.Get<IGameCommandDispatcher>();
            _noise = services.Get<INoiseService>();
            _tuning = services.Get<IContentDatabase>().Tuning;
            _world = services.Get<IWorldService>();
            _world.Register(this);
        }

        private void OnDisable()
        {
            _world?.Unregister(this);
            _world = null;
        }

        public void RestoreFromState(GameState state)
        {
            if (state.UnlockedDoors.Contains(id))
            {
                State = DoorState.Open;
                SnapOpen();
            }
            else if (startOpen)
            {
                State = DoorState.Open;
                SnapOpen();
            }
            else if (!RequirementMet)
            {
                State = DoorState.Locked;
            }
        }

        private void SnapOpen()
        {
            if (panel != null) panel.localPosition = _closedPos + openOffset;
            SetObstacle(false);
        }

        /// <summary>Called by player interact or by UnlockDoorCommand via WorldService.</summary>
        public void Unlock(string sourceId)
        {
            if (State == DoorState.Open || State == DoorState.Opening) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Animate(true));
            EmitNoise();
        }

        /// <summary>Player pushed the door: opens if unlocked/requirement met.</summary>
        public void TryOpenByPlayer(PlayerContext player)
        {
            if (!Unlocked && !RequirementMet) return; // interactable handles locked prompt
            if (!Unlocked)
                _dispatcher.Dispatch(new UnlockDoorCommand(id, player.Transform.name));
            else
            {
                if (_anim != null) StopCoroutine(_anim);
                _anim = StartCoroutine(Animate(State != DoorState.Open && State != DoorState.Opening));
                EmitNoise();
            }
        }

        public void Open() => Unlock("system");

        private IEnumerator Animate(bool opening)
        {
            State = opening ? DoorState.Opening : DoorState.Closing;
            if (audioSource != null) audioSource.Play();
            Vector3 from = panel.localPosition;
            Vector3 to = opening ? _closedPos + openOffset : _closedPos;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / slideDuration;
                panel.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            panel.localPosition = to;
            State = opening ? DoorState.Open : (RequirementMet || Unlocked ? DoorState.Closed : DoorState.Locked);
            SetObstacle(!opening);
            GameRoot.Instance.Services.Get<IGameEventBus>().Publish(new DoorStateChangedEvent(id));
        }

        private void SetObstacle(bool blocking)
        {
            var obstacle = GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = blocking;
        }

        private void EmitNoise()
        {
            _noise?.Emit(new NoiseEvent(transform.position,
                _tuning != null ? _tuning.DoorNoiseRadius : 7f, 1f, NoiseType.Door, this));
        }

        private void OnDrawGizmosSelected()
        {
            if (panel != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(panel.TransformPoint(_closedPos + openOffset), panel.lossyScale);
            }
        }
    }
}
