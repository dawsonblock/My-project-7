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
    /// PlayerLook transform ownership and device semantics: yaw lives on the
    /// player root, pitch/lean on the camera pivot; mouse deltas apply per
    /// event while stick deflection integrates degrees/second over dt. These
    /// pin the fix for the old bug where pitch's localRotation write erased
    /// the yaw applied to the same transform every frame.
    /// </summary>
    public class PlayerLookTests : InputTestFixture
    {
        private GameObject _root;
        private GameObject _player;
        private Transform _pivot;
        private PlayerLook _look;
        private PlayerInputReader _reader;
        private Mouse _mouse;
        private Gamepad _pad;

        private float Yaw => _player.transform.eulerAngles.y;
        private ISettingsService Settings =>
            GameRoot.Instance.Services.Get<ISettingsService>();

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _pad = InputSystem.AddDevice<Gamepad>();

            _root = new GameObject("GameRoot");
            _root.AddComponent<GameRoot>();

            // Build inactive so Awake/OnEnable can't run before `actions` is
            // assigned (same pattern as PauseInputTests).
            _player = new GameObject("Player");
            _player.SetActive(false);
            _player.AddComponent<PlayerState>();
            _player.AddComponent<CharacterController>();
            _reader = _player.AddComponent<PlayerInputReader>();
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Game/Gameplay/Player/PlayerInputActions.inputactions");
            var so = new SerializedObject(_reader);
            so.FindProperty("actions").objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();

            var pivotGo = new GameObject("CameraPivot");
            pivotGo.transform.SetParent(_player.transform, false);
            _pivot = pivotGo.transform;
            _look = pivotGo.AddComponent<PlayerLook>();
            var camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(_pivot, false);
            camGo.AddComponent<Camera>();

            _player.SetActive(true);
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_player);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator MouseLook_ChangesPlayerYaw()
        {
            yield return null;
            Set(_mouse.delta, new Vector2(10f, 0f));
            yield return null;
            yield return null;

            Assert.Greater(Yaw, 0.01f,
                "Horizontal mouse delta should rotate the player root");
        }

        [UnityTest]
        public IEnumerator MouseLook_UsesMouseSensitivity()
        {
            yield return null;
            Settings.MouseSensitivity = 3f;
            Set(_mouse.delta, new Vector2(10f, 0f));
            yield return null;
            yield return null;

            // 10 * 0.08 * 3 = 2.4 degrees on the root.
            Assert.AreEqual(2.4f, Yaw, 0.05f);
        }

        [UnityTest]
        public IEnumerator Yaw_PersistsAcrossFrames()
        {
            yield return null;
            Set(_mouse.delta, new Vector2(10f, 0f));
            yield return null;
            float yaw = Yaw;
            Assert.Greater(yaw, 0.01f, "Precondition: yaw applied");

            // Mouse delta self-resets; the old bug overwrote localRotation
            // with pitch-only and would zero this on the next frame.
            for (int i = 0; i < 3; i++) yield return null;

            Assert.AreEqual(yaw, Yaw, 0.001f,
                "Yaw must survive frames where pitch/lean rewrite the pivot");
        }

        [UnityTest]
        public IEnumerator VerticalLook_ChangesPitch_NotYaw()
        {
            yield return null;
            Set(_mouse.delta, new Vector2(0f, -10f));
            yield return null;
            yield return null;

            Assert.AreNotEqual(0f, _look.Pitch,
                "Vertical mouse delta should drive pitch");
            Assert.AreEqual(0f, Yaw, 0.0001f,
                "Vertical input must not rotate the player root");
        }

        [UnityTest]
        public IEnumerator Pitch_ClampsToLimits()
        {
            yield return null;
            for (int i = 0; i < 5; i++)
            {
                Set(_mouse.delta, new Vector2(0f, -1000f));
                yield return null;
            }

            Assert.AreEqual(85f, _look.Pitch, 0.001f);
        }

        [UnityTest]
        public IEnumerator InvertY_FlipsPitch()
        {
            yield return null;
            Settings.InvertY = false;
            Set(_mouse.delta, new Vector2(0f, -10f));
            yield return null;
            yield return null;
            float normal = _look.Pitch;

            _look.SetPitch(0f);
            Settings.InvertY = true;
            Set(_mouse.delta, new Vector2(0f, -10f));
            yield return null;
            yield return null;
            float inverted = _look.Pitch;

            Assert.AreEqual(-normal, inverted, 0.05f,
                "InvertY should flip the pitch direction");
        }

        [UnityTest]
        public IEnumerator GamepadLook_UsesControllerSensitivity()
        {
            yield return null;
            Settings.ControllerSensitivity = 2f;
            Time.captureDeltaTime = 1f / 60f;

            Set(_pad.rightStick, Vector2.right);
            yield return null; // one application frame
            Set(_pad.rightStick, Vector2.zero);
            float yaw = Yaw;

            // 1.0 deflection * 140 deg/s * (1/60)s * sens 2 = 4.667 deg.
            Assert.AreEqual(140f * (1f / 60f) * 2f, yaw, 0.05f);
        }

        [UnityTest]
        public IEnumerator GamepadLook_IsFrameRateIndependent()
        {
            yield return null;
            Settings.ControllerSensitivity = 1f;

            Time.captureDeltaTime = 1f / 30f;
            Set(_pad.rightStick, Vector2.right);
            for (int i = 0; i < 30; i++) yield return null;
            float yaw30 = Yaw;

            _player.transform.rotation = Quaternion.identity;

            Time.captureDeltaTime = 1f / 120f;
            for (int i = 0; i < 120; i++) yield return null;
            Set(_pad.rightStick, Vector2.zero);
            float yaw120 = Yaw;

            // One simulated second of full deflection at either rate must
            // integrate to the same ~140 degrees.
            Assert.AreEqual(yaw30, yaw120, yaw30 * 0.02f,
                "Stick look integrates deg/s over dt — same turn per second");
        }

        [UnityTest]
        public IEnumerator LookFromGamepad_TracksActuatingDevice()
        {
            yield return null;
            Set(_pad.rightStick, Vector2.right);
            yield return null;
            Assert.IsTrue(_reader.LookFromGamepad);

            Set(_pad.rightStick, Vector2.zero);
            Set(_mouse.delta, new Vector2(5f, 0f));
            yield return null;
            Assert.IsFalse(_reader.LookFromGamepad);
        }

        [UnityTest]
        public IEnumerator UiOpen_DisablesLook()
        {
            yield return null;
            var gate = GameRoot.Instance.Services.Get<IInputGate>();
            var owner = new object();
            gate.PushUi(owner);
            yield return null;

            Set(_mouse.delta, new Vector2(10f, 0f));
            yield return null;
            yield return null;

            Assert.AreEqual(0f, Yaw, 0.0001f,
                "Look must not apply while a UI surface owns input");
            gate.PopUi(owner);
        }
    }
}
