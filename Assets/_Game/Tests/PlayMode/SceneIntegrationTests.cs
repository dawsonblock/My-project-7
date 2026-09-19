using System.Collections;
using Escape.Core;
using Escape.Data;
using Escape.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Escape.Tests.PlayMode
{
    /// <summary>
    /// Integration tests against the real generated scenes: scene loads
    /// through SceneService, bootstrap wiring, spawn placement, and a
    /// save/load round-trip through the coordinator + participants.
    /// </summary>
    public class SceneIntegrationTests
    {
        private const string TestSlot = "itest_slot";

        private GameObject _root;

        [SetUp]
        public void Setup()
        {
            _root = new GameObject("GameRoot");
            _root.AddComponent<GameRoot>();
        }

        [TearDown]
        public void Teardown()
        {
            if (GameRoot.Instance != null)
                GameRoot.Instance.Services.Get<ISaveService>()?.Delete(TestSlot);
            Object.DestroyImmediate(_root);
        }

        [UnityTest]
        public IEnumerator Dock_Loads_AndBootstraps()
        {
            yield return null;
            var scenes = GameRoot.Instance.Services.Get<ISceneService>();
            scenes.LoadScene(SceneId.Dock, "default");

            yield return WaitUntil(() => !scenes.IsTransitioning, 10f);
            yield return null; // SceneBootstrap.Start
            yield return null;

            var bootstrap = Object.FindAnyObjectByType<SceneBootstrap>();
            Assert.IsNotNull(bootstrap, "Dock scene has no SceneBootstrap");

            var state = GameRoot.Instance.Services.Get<IGameStateService>();
            Assert.AreEqual(SceneId.Dock, state.State.SceneId);

            var player = Object.FindAnyObjectByType<PlayerState>();
            Assert.IsNotNull(player, "No player spawned in Dock");

            // Arrival autosave must exist after bootstrap.
            var saves = GameRoot.Instance.Services.Get<ISaveService>();
            Assert.IsTrue(saves.HasSave(SaveService.Autosave),
                "Bootstrap did not write the arrival autosave");
        }

        [UnityTest]
        public IEnumerator Save_Load_RoundTrip_RestoresLivePose()
        {
            yield return null;
            var services = GameRoot.Instance.Services;
            services.Get<ISceneService>().LoadScene(SceneId.Dock, "default");
            yield return WaitUntil(
                () => !services.Get<ISceneService>().IsTransitioning, 10f);
            yield return null;
            yield return null;

            var player = Object.FindAnyObjectByType<PlayerState>();
            Assert.IsNotNull(player);
            var movement = player.GetComponent<PlayerMovement>();
            var saves = services.Get<ISaveService>();
            var state = services.Get<IGameStateService>();

            // Pose A → save.
            var posA = new Vector3(3f, 0f, -4f);
            movement.Teleport(posA, 120f, -15f);
            yield return null;
            Assert.IsTrue(saves.Save(TestSlot), "Save failed");

            // Load triggers a full scene reload; the player object is
            // recreated and the coordinator restores the saved pose.
            Assert.IsTrue(saves.Load(TestSlot), "Load failed");
            yield return WaitUntil(
                () => !services.Get<ISceneService>().IsTransitioning, 10f);
            yield return null; // SceneBootstrap.Start + placement
            yield return null;

            player = Object.FindAnyObjectByType<PlayerState>();
            Assert.IsNotNull(player, "Player missing after scene reload");
            var p = player.transform.position;
            Assert.Less(Vector3.Distance(p, posA), 0.5f,
                $"Pose not restored after load: {p}");
            Assert.AreEqual(posA, state.State.Player.Position);
        }

        /// <summary>
        /// SceneReady is a commit barrier, not a best-effort wait. If a scene
        /// never signals readiness inside the window, the transition must be
        /// reported as failed rather than published as a successful arrival.
        /// A zero-length window makes that deterministic: the readiness signal
        /// arrives from the bootstrap coroutine on a later frame, so it can
        /// never land inside the window.
        /// </summary>
        [UnityTest]
        public IEnumerator SceneWithoutReady_FailsClosed_AndWithholdsSceneChanged()
        {
            yield return null;
            var services = GameRoot.Instance.Services;
            var scenes = services.Get<ISceneService>() as SceneService;
            var events = services.Get<IGameEventBus>();
            Assert.IsNotNull(scenes, "ISceneService is not a SceneService");

            bool failed = false, changed = false;
            System.Action<SceneTransitionFailedEvent> onFail = _ => failed = true;
            System.Action<SceneChangedEvent> onChange = _ => changed = true;
            events.Subscribe(onFail);
            events.Subscribe(onChange);
            try
            {
                // The failure path logs an error by design; declare it so the
                // test framework does not treat it as an unhandled error.
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                    "never signalled SceneReady"));
                scenes.ReadyTimeoutSeconds = 0f;
                scenes.LoadScene(SceneId.Dock, "default");
                yield return WaitUntil(() => !scenes.IsTransitioning, 10f);
            }
            finally
            {
                events.Unsubscribe(onFail);
                events.Unsubscribe(onChange);
                scenes.ReadyTimeoutSeconds = 5f;
            }

            Assert.IsTrue(failed,
                "A scene that never signalled SceneReady must report a failed transition");
            Assert.IsFalse(changed,
                "A failed transition must not publish SceneChanged as if it succeeded");
        }

        private static IEnumerator WaitUntil(System.Func<bool> pred, float timeout)
        {
            var t = 0f;
            while (!pred() && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(pred(), "Timed out waiting for condition");
        }
    }
}
