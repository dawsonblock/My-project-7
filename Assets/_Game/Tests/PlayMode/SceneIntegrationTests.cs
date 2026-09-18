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
