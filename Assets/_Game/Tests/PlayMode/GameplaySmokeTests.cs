using System.Collections;
using Escape.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Escape.Tests.PlayMode
{
    /// <summary>
    /// PlayMode smoke tests: real services wired through a real GameRoot.
    /// </summary>
    public class GameplaySmokeTests
    {
        /// <summary>Fixed frame time, so the detection ramp is deterministic.</summary>
        private const float FixedStep = 1f / 30f;

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
            Time.captureDeltaTime = 0f;
            // Immediate: deferred Destroy can let the next test's GameRoot
            // see a live Instance and destroy itself in Awake.
            Object.DestroyImmediate(_root);
        }

        [UnityTest]
        public IEnumerator GameRoot_RegistersAllServices()
        {
            yield return null; // Awake ran on AddComponent
            var s = GameRoot.Instance.Services;
            Assert.IsNotNull(s.Get<IGameStateService>());
            Assert.IsNotNull(s.Get<IGameCommandDispatcher>());
            Assert.IsNotNull(s.Get<IGameEventBus>());
            Assert.IsNotNull(s.Get<IContentDatabase>());
            Assert.IsNotNull(s.Get<IDetectionService>());
            Assert.IsNotNull(s.Get<INoiseService>());
            Assert.IsNotNull(s.Get<ISaveService>());
            Assert.IsNotNull(s.Get<ISceneService>());
            Assert.IsNotNull(s.Get<IInputGate>());
            Assert.IsNotNull(s.Get<ISaveCoordinator>());
        }

        /// <summary>
        /// Sensors submit once per frame while they perceive the player, and
        /// GameRoot integrates with Time.deltaTime. A headless batchmode loop
        /// runs thousands of frames per second, so the submitted per-second
        /// rate is multiplied by a ~1e-4s delta and the meter barely moves —
        /// the test measured the host's frame rate, not the detection ramp.
        /// Pinning the step makes the ramp and the decay both exact.
        /// </summary>
        [UnityTest]
        public IEnumerator Detection_RisesAndDecays()
        {
            yield return null;
            Time.captureDeltaTime = FixedStep;
            var det = GameRoot.Instance.Services.Get<IDetectionService>();
            // Sensors submit once per frame while they perceive the player.
            for (int i = 0; i < 30; i++)
            {
                det.Submit("test_cam", DetectionType.Camera, 60f);
                yield return null;
            }
            Assert.Greater(det.Detection, 10f);
            float peak = det.Detection;
            yield return new WaitForSeconds(1.5f);
            Assert.Less(det.Detection, peak);
        }
    }
}
