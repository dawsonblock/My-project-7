using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Escape.Core
{
    /// <summary>
    /// Implemented in each gameplay scene (by SceneBootstrap) to place the
    /// player at a named spawn point after a load.
    /// </summary>
    public interface IPlayerPlacement
    {
        void PlacePlayer(string spawnId, PlayerSaveState savedPose);
    }

    /// <summary>
    /// Implemented by UI to provide fade transitions. Optional — scene loads
    /// still work without it.
    /// </summary>
    public interface IScreenFader
    {
        IEnumerator FadeOut();
        IEnumerator FadeIn();
    }

    public interface ISceneService
    {
        bool IsTransitioning { get; }
        /// <summary>
        /// Spawn id requested by the in-flight (or most recent) transition.
        /// SceneBootstrap consumes it on arrival: empty means "restore the
        /// saved pose" (save-load path), a name means a spawn point.
        /// </summary>
        string PendingSpawnId { get; }
        /// <summary>
        /// Reads and clears the pending spawn id. SceneBootstrap must consume
        /// it so a later direct scene open can't land on a stale spawn point.
        /// </summary>
        string ConsumePendingSpawnId();
        void LoadScene(string sceneId, string spawnId = "");
    }

    /// <summary>
    /// The only place SceneManager.LoadScene is called. Handles autosave,
    /// fade, load, player placement and the SceneChanged event.
    /// </summary>
    public sealed class SceneService : ISceneService, IGameCommandHandler<ChangeSceneCommand>
    {
        private readonly GameRoot _host;
        private readonly GameServices _services;
        private readonly IGameStateService _state;
        private readonly IContentDatabase _content;
        private readonly IGameEventBus _events;

        public bool IsTransitioning { get; private set; }
        public string PendingSpawnId { get; private set; } = "";

        public string ConsumePendingSpawnId()
        {
            var spawn = PendingSpawnId;
            PendingSpawnId = "";
            return spawn;
        }

        public SceneService(GameRoot host, GameServices services, IGameStateService state,
            IContentDatabase content, IGameEventBus events)
        {
            _host = host;
            _services = services;
            _state = state;
            _content = content;
            _events = events;
        }

        public void LoadScene(string sceneId, string spawnId = "")
        {
            if (IsTransitioning) return;
            var sceneName = _content.SceneName(sceneId);
            if (sceneName == null)
            {
                Debug.LogWarning($"[SceneService] Unknown scene id '{sceneId}'.");
                return;
            }
            PendingSpawnId = spawnId;
            _host.StartCoroutine(LoadRoutine(sceneId, sceneName, spawnId));
        }

        private IEnumerator LoadRoutine(string sceneId, string sceneName, string spawnId)
        {
            IsTransitioning = true;
            // SceneBootstrap publishes SceneReady once the scene is fully
            // initialized (placement, entry objectives, checkpoint). This is
            // the deterministic arrival handshake — never place the player
            // from here, and never rely on a frame yield lining up with the
            // scene's own Start coroutine.
            bool ready = false;
            System.Action<SceneReadyEvent> onReady =
                e => { if (e.SceneId == sceneId) ready = true; };
            _events.Subscribe(onReady);
            try
            {
                if (_services.TryGet<IScreenFader>(out var fader) && fader != null)
                    yield return fader.FadeOut();

                // Synchronous load: the blockout scenes are tiny, and async
                // scene ops can stall when initiated outside the player loop.
                SceneManager.LoadScene(sceneName);

                _state.State.SceneId = sceneId;

                // Bounded wait: a scene without a bootstrap (or a broken one)
                // logs and continues rather than wedging behind a black fade.
                float t = 0f;
                while (!ready && t < 5f)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!ready)
                    Debug.LogWarning($"[SceneService] '{sceneName}' signalled no SceneReady — continuing.");

                _events.Publish(new SceneChangedEvent(sceneId));
                _events.Publish(new ObjectiveUpdatedEvent());

                // Re-fetch: the fader that faded out lived in the old scene and
                // was destroyed by the swap — the new scene registers its own.
                if (_services.TryGet<IScreenFader>(out var fader2) && fader2 != null)
                    yield return fader2.FadeIn();
            }
            finally
            {
                _events.Unsubscribe(onReady);
                IsTransitioning = false;
            }
        }

        public void Handle(ChangeSceneCommand command) => LoadScene(command.SceneId, command.SpawnId);
    }
}
