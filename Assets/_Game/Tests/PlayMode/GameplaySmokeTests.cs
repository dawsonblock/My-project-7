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
            Object.Destroy(_root);
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
        }

        [UnityTest]
        public IEnumerator Detection_RisesAndDecays()
        {
            yield return null;
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
