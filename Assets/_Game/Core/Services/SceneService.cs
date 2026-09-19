using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Escape.Core
{
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
    /// The only place SceneManager.LoadScene is called. Owns the transition
    /// state machine: fade, load, the SceneReady handshake, SceneChanged, and
    /// what happens when a scene never becomes ready. Player placement and
    /// autosave belong to SceneBootstrap — by the time this service publishes
    /// SceneChanged, the scene has already done its own arrival work.
    /// </summary>
    public sealed class SceneService : ISceneService, IGameCommandHandler<ChangeSceneCommand>
    {
        private readonly GameRoot _host;
        private readonly GameServices _services;
        private readonly IGameStateService _state;
        private readonly IContentDatabase _content;
        private readonly IGameEventBus _events;

        /// <summary>
        /// Last scene this service successfully arrived in. The rollback
        /// target when a later transition fails its readiness contract.
        /// </summary>
        private string _lastReadySceneId = "";

        public bool IsTransitioning { get; private set; }
        public string PendingSpawnId { get; private set; } = "";

        /// <summary>
        /// How long to wait for a scene's SceneReady before declaring the
        /// transition failed. Settable so tests can exercise the failure path
        /// without waiting the full window.
        /// </summary>
        public float ReadyTimeoutSeconds { get; set; } = 5f;

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
            try
            {
                bool ready = false;
                yield return LoadAndAwaitReady(sceneId, sceneName, r => ready = r);

                if (!ready)
                {
                    // The destination loaded but never initialized, so state
                    // already points at a scene that is not trustworthy.
                    Debug.LogError($"[SceneService] '{sceneName}' never signalled " +
                                   "SceneReady — transition failed, SceneChanged withheld.");
                    _events.Publish(new SceneTransitionFailedEvent(sceneId));

                    yield return Recover(sceneId);
                    yield break;
                }

                _lastReadySceneId = sceneId;
                _events.Publish(new SceneChangedEvent(sceneId));
                _events.Publish(new ObjectiveUpdatedEvent());

                // Re-fetch: the fader that faded out lived in the old scene and
                // was destroyed by the swap — the new scene registers its own.
                if (_services.TryGet<IScreenFader>(out var fader2) && fader2 != null)
                    yield return fader2.FadeIn();
            }
            finally
            {
                IsTransitioning = false;
            }
        }

        /// <summary>
        /// Loads one scene and waits for its readiness handshake. SceneBootstrap
        /// publishes SceneReady once the scene is fully initialized (placement,
        /// entry objectives, checkpoint) — never place the player from here, and
        /// never rely on a frame yield lining up with the scene's own Start
        /// coroutine. Reports the outcome instead of returning it, because
        /// coroutines cannot have out parameters.
        /// </summary>
        private IEnumerator LoadAndAwaitReady(string sceneId, string sceneName,
            System.Action<bool> report)
        {
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

                float t = 0f;
                while (!ready && t < ReadyTimeoutSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                report(ready);
            }
            finally
            {
                _events.Unsubscribe(onReady);
            }
        }

        /// <summary>
        /// Recovery policy for a failed arrival. A readiness timeout is not
        /// transactional — SceneManager.LoadScene has already replaced the old
        /// scene and state.SceneId already points at the new one — so "fail
        /// closed" has to mean "put the session somewhere trustworthy", not
        /// "fade the broken scene back in and hope". Roll back to the last
        /// scene that did become ready; if there is none (or it is the scene
        /// that just failed), fall back to the main menu. If that also fails to
        /// become ready, report the transition as fatal rather than pretending
        /// there is a session to continue.
        ///
        /// Deliberately does not recurse into LoadRoutine: a recovery that can
        /// itself trigger recovery is how a failed load becomes a loop.
        /// </summary>
        private IEnumerator Recover(string failedSceneId)
        {
            var target = RecoveryTargetFor(failedSceneId);
            var targetName = target == null ? null : _content.SceneName(target);
            if (targetName == null)
            {
                Debug.LogError($"[SceneService] '{failedSceneId}' failed readiness and there is " +
                               "no recovery scene to fall back to — the session has no trusted scene.");
                _events.Publish(new SceneTransitionFatalEvent(failedSceneId));
                yield break;
            }

            bool recovered = false;
            // Clear the spawn request before rolling back. LoadScene stored the
            // FAILED scene's spawn id, and SceneBootstrap treats any non-empty
            // spawn id as "arrive at this named point" — so without this the
            // recovery scene would consume the failed scene's spawn and
            // teleport the player to an arbitrary point in it, instead of
            // restoring where they actually were. Empty means "restore the
            // saved pose", which is what returning to a known-good scene means.
            // Removing this line fails SceneWithoutReady_RollsBackToTheLastReadyScene.
            PendingSpawnId = "";
            yield return LoadAndAwaitReady(target, targetName, r => recovered = r);

            if (!recovered)
            {
                Debug.LogError($"[SceneService] Recovery into '{targetName}' also failed readiness — " +
                               "the session has no trusted scene.");
                _events.Publish(new SceneTransitionFatalEvent(failedSceneId));
                yield break;
            }

            _lastReadySceneId = target;
            Debug.LogWarning($"[SceneService] Recovered from '{failedSceneId}' by returning to " +
                             $"'{targetName}'.");
            _events.Publish(new SceneChangedEvent(target));
            _events.Publish(new ObjectiveUpdatedEvent());

            if (_services.TryGet<IScreenFader>(out var fader) && fader != null)
                yield return fader.FadeIn();
        }

        /// <summary>
        /// Where to send the session after a failed arrival. Null when there is
        /// nowhere distinct to go, which is what makes the failure fatal rather
        /// than a retry loop.
        /// </summary>
        private string RecoveryTargetFor(string failedSceneId)
        {
            if (!string.IsNullOrEmpty(_lastReadySceneId) && _lastReadySceneId != failedSceneId)
                return _lastReadySceneId;
            if (failedSceneId != Data.SceneId.MainMenu)
                return Data.SceneId.MainMenu;
            return null;
        }

        public void Handle(ChangeSceneCommand command) => LoadScene(command.SceneId, command.SpawnId);
    }
}
