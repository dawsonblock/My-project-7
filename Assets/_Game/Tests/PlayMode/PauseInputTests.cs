using System.Collections;
using Escape.Core;
using Escape.Gameplay;
using Escape.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Escape.Tests.PlayMode
{
    /// <summary>
    /// Verifies the global pause route end-to-end through the real Input
    /// System: a synthesized Escape press must travel Keyboard → System map
    /// → PlayerInputReader.PausePressed → PauseMenuUI, opening and closing
    /// pause and driving timeScale + the input gate stack.
    /// </summary>
    public class PauseInputTests : InputTestFixture
    {
        private GameObject _root;
        private GameObject _player;
        private GameObject _canvasGo;
        private PauseMenuUI _pause;

        public override void Setup()
        {
            base.Setup();
            var keyboard = InputSystem.AddDevice<Keyboard>();

            _root = new GameObject("GameRoot");
            _root.AddComponent<GameRoot>();

            // Build inactive so Awake can't run before `actions` is assigned.
            _player = new GameObject("Player");
            _player.SetActive(false);
            var reader = _player.AddComponent<PlayerInputReader>();
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Game/Gameplay/Player/PlayerInputActions.inputactions");
            var so = new SerializedObject(reader);
            so.FindProperty("actions").objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            _player.SetActive(true);

            _canvasGo = new GameObject("Canvas", typeof(RectTransform));
            _pause = PauseMenuUI.Create(_canvasGo.transform, null);
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            // DestroyImmediate: plain Destroy defers OnDestroy past the next
            // test's Setup, so the new GameRoot would see a live Instance
            // and destroy itself.
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_canvasGo);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Escape_OpensThenCloses_PauseMenu()
        {
            yield return null;
            yield return null;
            var gate = GameRoot.Instance.Services.Get<IInputGate>();

            Press(Keyboard.current.escapeKey);
            yield return null;
            Release(Keyboard.current.escapeKey);
            yield return null;
            Assert.IsTrue(_pause.IsOpen, "Escape should open the pause menu");
            Assert.IsTrue(gate.UiOpen, "Pause should push a UI owner");
            Assert.AreEqual(0f, Time.timeScale, "Pause should freeze time");

            Press(Keyboard.current.escapeKey);
            yield return null;
            Release(Keyboard.current.escapeKey);
            yield return null;
            Assert.IsFalse(_pause.IsOpen, "Second Escape should close the pause menu");
            Assert.IsFalse(gate.UiOpen, "Gate stack should be empty");
            Assert.AreEqual(1f, Time.timeScale, "Time should resume");
        }

        [UnityTest]
        public IEnumerator Escape_WithModalOpen_CancelsTop_NotPause()
        {
            yield return null;
            yield return null;
            var gate = GameRoot.Instance.Services.Get<IInputGate>();
            var modal = new FakeModal();
            gate.PushUi(modal);

            Press(Keyboard.current.escapeKey);
            yield return null;
            Release(Keyboard.current.escapeKey);
            yield return null;

            Assert.AreEqual(1, modal.Cancels, "Escape should cancel the top modal");
            Assert.IsFalse(_pause.IsOpen, "Pause must not open over a modal");
            gate.PopUi(modal);
        }

        [UnityTest]
        public IEnumerator Pause_SelectsControlOnOpen_DeselectsOnClose()
        {
            // Keyboard/gamepad nav needs a selected control to anchor to;
            // closing must drop it so a stale selection can't ghost-submit.
            var es = new GameObject("EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            yield return null;
            yield return null;

            Press(Keyboard.current.escapeKey);
            yield return null;
            Release(Keyboard.current.escapeKey);
            yield return null;

            var sel = EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(sel, "Opening pause should select a control");
            Assert.IsTrue(sel.transform.IsChildOf(_pause.transform),
                "Selected control should be inside the pause menu");

            Press(Keyboard.current.escapeKey);
            yield return null;
            Release(Keyboard.current.escapeKey);
            yield return null;

            Assert.IsNull(EventSystem.current.currentSelectedGameObject,
                "Closing pause should clear the selection");
            Object.DestroyImmediate(es);
        }

        private sealed class FakeModal : ICancelableUi
        {
            public int Cancels;
            public void Cancel() => Cancels++;
        }
    }
}
