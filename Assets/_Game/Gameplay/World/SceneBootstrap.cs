using System.Collections;
using Escape.Core;
using UnityEngine;

namespace Escape.Gameplay
{
    /// <summary>
    /// Per-scene composition hook. Registers the scene's player-placement
    /// service, hands the player its PlayerContext pieces, and triggers the
    /// entry autosave / first objective activation.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class SceneBootstrap : MonoBehaviour, IPlayerPlacement
    {
        [SerializeField] private string sceneId;
        [SerializeField] private string firstObjectiveId = "";
        [SerializeField] private string completesObjectiveId = "";
        [SerializeField] private Transform playerRoot;

        private GameServices _services;

        private IEnumerator Start()
        {
            // Wait for GameRoot — it persists across scene loads, but a scene
            // opened directly in the editor may need a few frames for it to
            // appear. Bounded so a missing root can't wedge the coroutine.
            int waited = 0;
            while (GameRoot.Instance == null && waited++ < 120)
                yield return null;
            if (GameRoot.Instance == null)
            {
                Debug.LogError("[SceneBootstrap] No GameRoot — open Bootstrap scene first.");
                yield break;
            }
            _services = GameRoot.Instance.Services;
            _services.Register<IPlayerPlacement>(this);

            var state = _services.Get<IGameStateService>();
            state.State.SceneId = sceneId;

            if (playerRoot == null)
                playerRoot = FindAnyObjectByType<PlayerState>()?.transform;

            // The transition decides placement: a named spawn means "arrive at
            // that point", empty means "restore the saved pose" (save loads).
            var saved = state.State.Player;
            var spawn = _services.Get<ISceneService>().ConsumePendingSpawnId();
            PlacePlayer(string.IsNullOrEmpty(spawn) ? "" : spawn,
                string.IsNullOrEmpty(spawn) ? saved : null);

            var dispatcher = _services.Get<IGameCommandDispatcher>();
            if (!string.IsNullOrEmpty(completesObjectiveId))
                dispatcher.Dispatch(new CompleteObjectiveCommand(completesObjectiveId, sceneId));
            if (!string.IsNullOrEmpty(firstObjectiveId))
                dispatcher.Dispatch(new ActivateObjectiveCommand(firstObjectiveId));

            // Arrival checkpoint — after placement, so the autosave records
            // the spawn pose rather than the previous scene's position.
            if (sceneId != Data.SceneId.MainMenu && sceneId != Data.SceneId.Bootstrap
                && _services.TryGet<ISaveService>(out var saves))
                saves.Save(SaveService.Autosave);

            // Deterministic handoff: SceneService blocks the transition on
            // this signal before publishing SceneChanged and fading in.
            if (_services.TryGet<IGameEventBus>(out var events))
                events.Publish(new SceneReadyEvent(sceneId));
        }

        // Start registers on the first run; a later disable/enable cycle must
        // restore the registration without re-running Start.
        private void OnEnable()
        {
            if (_services != null)
                _services.Register<IPlayerPlacement>(this);
        }

        private void OnDisable()
        {
            if (_services != null)
                _services.Unregister<IPlayerPlacement>(this);
        }

        public void PlacePlayer(string spawnId, PlayerSaveState savedPose)
        {
            if (playerRoot == null) return;
            var movement = playerRoot.GetComponent<PlayerMovement>();

            // A load (empty spawn id) restores the saved pose; scene
            // transitions always land on a named spawn point.
            if (string.IsNullOrEmpty(spawnId) && savedPose != null &&
                savedPose.Position != Vector3.zero)
            {
                // Load path — save participants restore volatile state
                // (pose, flashlight). Spawn-point entries skip this.
                if (_services.TryGet<ISaveCoordinator>(out var coord))
                    coord.RestoreFrom(_services.Get<IGameStateService>().State);
                else
                    movement?.Teleport(savedPose.Position, savedPose.Yaw, savedPose.Pitch);
                WritePose();
                return;
            }

            PlayerSpawnPoint target = null;
            var points = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            foreach (var p in points)
                if (p.SpawnId == (string.IsNullOrEmpty(spawnId) ? "default" : spawnId))
                { target = p; break; }
            if (target == null && points.Length > 0) target = points[0];

            if (target != null)
                movement?.Teleport(target.transform.position, target.transform.eulerAngles.y, 0f);
            WritePose();
        }

        private void WritePose()
        {
            if (_services == null || playerRoot == null) return;
            var pose = _services.Get<IGameStateService>().State.Player;
            pose.Position = playerRoot.position;
            pose.Yaw = playerRoot.eulerAngles.y;
            var look = playerRoot.GetComponentInChildren<PlayerLook>();
            pose.Pitch = look != null ? look.Pitch : 0f;
        }
    }
}
