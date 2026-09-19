using System.Collections;
using Escape.Core;
using Escape.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Escape.Tests.PlayMode
{
    /// <summary>
    /// Qualifies the movement half of the control chain end to end:
    /// keyboard → PlayerInputReader.Move → PlayerMovement → CharacterController.
    /// PlayerLookTests covers the look half. The yaw test is the regression
    /// guard for movement reading the wrong transform.
    ///
    /// Every test pins <see cref="Time.captureDeltaTime"/>. A headless
    /// batchmode loop runs thousands of frames per second, so Time.deltaTime is
    /// ~1e-4s and acceleration * dt never accumulates into measurable
    /// displacement — these tests used to fail on the environment's frame rate
    /// rather than on the behaviour they exist to qualify.
    /// </summary>
    public class PlayerMovementTests : InputTestFixture
    {
        /// <summary>Fixed frame time, so distance assertions are exact.</summary>
        private const float FixedStep = 1f / 30f;

        private GameObject _root;
        private GameObject _player;
        private PlayerMovement _movement;
        private Keyboard _kb;

        public override void Setup()
        {
            base.Setup();
            _kb = InputSystem.AddDevice<Keyboard>();

            _root = new GameObject("GameRoot");
            _root.AddComponent<GameRoot>();

            // Build inactive so Awake/OnEnable can't run before `actions` is
            // assigned (same pattern as PauseInputTests).
            _player = new GameObject("Player");
            _player.SetActive(false);
            _player.AddComponent<PlayerState>();
            _player.AddComponent<CharacterController>();
            _movement = _player.AddComponent<PlayerMovement>();
            var reader = _player.AddComponent<PlayerInputReader>();
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Game/Gameplay/Player/PlayerInputActions.inputactions");
            var so = new SerializedObject(reader);
            so.FindProperty("actions").objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(_player.transform, false);
            pivot.AddComponent<PlayerLook>();
            var camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(pivot.transform, false);
            camGo.AddComponent<Camera>();

            _player.SetActive(true);
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = 0f;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_player != null) Object.DestroyImmediate(_player);
            base.TearDown();
        }

        /// <summary>
        /// A movement input source with no device behind it. Proves the
        /// movement rules can be driven exactly, independent of the Input
        /// System's device pump and of the Game view's focus.
        /// </summary>
        private sealed class FakeMoveInput : IPlayerMoveInput
        {
            public Vector2 Move { get; set; }
            public bool SprintHeld { get; set; }
            public bool CrouchHeld { get; set; }
            public event System.Action CrouchPressed;
            public void PressCrouch() => CrouchPressed?.Invoke();
        }

        private IInputGate Gate => GameRoot.Instance.Services.Get<IInputGate>();

        /// <summary>Displacement since `from`, flattened to the ground plane.</summary>
        private Vector3 HorizontalDrift(Vector3 from) =>
            new Vector3(_player.transform.position.x - from.x, 0f,
                        _player.transform.position.z - from.z);

        private IEnumerator HoldForward(int frames)
        {
            Press(_kb.wKey);
            for (int i = 0; i < frames; i++) yield return null;
            Release(_kb.wKey);
        }

        [UnityTest]
        public IEnumerator ForwardInput_MovesThePlayer()
        {
            yield return null;
            Time.captureDeltaTime = FixedStep;
            var start = _player.transform.position;

            yield return HoldForward(30);

            Assert.Greater(HorizontalDrift(start).magnitude, 0.3f,
                "Holding W must move the player");
        }

        [UnityTest]
        public IEnumerator Movement_FollowsPlayerYaw()
        {
            yield return null;
            Time.captureDeltaTime = FixedStep;
            _player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            yield return null;
            var start = _player.transform.position;

            yield return HoldForward(30);

            var moved = HorizontalDrift(start);
            Assert.Greater(moved.x, 0.3f,
                "At yaw 90 the player's forward is +X, so W must travel along +X");
            Assert.Less(Mathf.Abs(moved.z), Mathf.Abs(moved.x),
                "Movement must follow the player's yaw, not world Z");
        }

        [UnityTest]
        public IEnumerator UiOpen_SuppressesMovement()
        {
            yield return null;
            // Fixed steps matter here too: without them nothing moves anyway,
            // so the test would pass for the wrong reason.
            Time.captureDeltaTime = FixedStep;
            var owner = new object();
            Gate.PushUi(owner);
            yield return null;
            var start = _player.transform.position;

            yield return HoldForward(20);

            Assert.Less(HorizontalDrift(start).magnitude, 0.05f,
                "Gameplay movement must not apply while a UI surface owns input");
            Gate.PopUi(owner);
        }

        /// <summary>
        /// The movement rules driven through the injected source rather than
        /// through key presses. Crouch is a clean probe: it selects a different
        /// speed from the same tuning, so the distance over an identical number
        /// of fixed steps must differ.
        /// </summary>
        [UnityTest]
        public IEnumerator CrouchInput_SlowsMovement_ThroughTheInjectedSource()
        {
            yield return null;
            Time.captureDeltaTime = FixedStep;
            var fake = new FakeMoveInput { Move = Vector2.up };
            _movement.SetInputSource(fake);

            var walkStart = _player.transform.position;
            for (int i = 0; i < 30; i++) yield return null;
            float walked = HorizontalDrift(walkStart).magnitude;

            fake.CrouchHeld = true;
            var crouchStart = _player.transform.position;
            for (int i = 0; i < 30; i++) yield return null;
            float crouched = HorizontalDrift(crouchStart).magnitude;

            Assert.Greater(walked, 0.5f,
                "The injected source must actually drive movement, or this proves nothing");
            Assert.Less(crouched, walked,
                "Crouch input must reduce the distance travelled over the same fixed steps");
        }
    }
}
