using System.Collections;
using Escape.Core;
using Escape.Data;
using Escape.Gameplay;
using Escape.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Escape.Tests.PlayMode
{
    /// <summary>
    /// UI qualification: every modal surface must push/pop the input gate
    /// symmetrically, keep its host active while visually closed (lazy
    /// binding and the Update retry depend on it), survive a missing
    /// GameRoot, and still receive global input when the player prefab
    /// spawns after the UI was built.
    /// </summary>
    public class UiQualificationTests : InputTestFixture
    {
        private GameObject _root;
        private GameObject _canvasGo;
        private Keyboard _kb;

        public override void Setup()
        {
            base.Setup();
            _kb = InputSystem.AddDevice<Keyboard>();
            _root = new GameObject("GameRoot");
            _root.AddComponent<GameRoot>();
            _canvasGo = new GameObject("Canvas", typeof(RectTransform));
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            // Settings tests write real PlayerPrefs keys.
            PlayerPrefs.DeleteKey("set_mouse_sens");
            if (_root != null) Object.DestroyImmediate(_root);
            if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
            base.TearDown();
        }

        private IInputGate Gate => GameRoot.Instance.Services.Get<IInputGate>();

        // ---------- settings ----------

        [UnityTest]
        public IEnumerator Settings_OpenAndClose_PushesAndPopsTheGate()
        {
            var ui = SettingsUI.Create(_canvasGo.transform);
            yield return null;

            ui.Open();
            Assert.IsTrue(ui.IsOpen, "Settings should report open");
            Assert.IsTrue(Gate.UiOpen, "Opening settings must suppress gameplay input");

            ui.Open(); // idempotent — PushUi dedupes by owner
            ui.Close();
            Assert.IsFalse(ui.IsOpen);
            Assert.IsFalse(Gate.UiOpen, "Closing settings must release input ownership");
        }

        [UnityTest]
        public IEnumerator Settings_Close_FlushesPendingWrites()
        {
            var ui = SettingsUI.Create(_canvasGo.transform);
            yield return null;
            var settings = GameRoot.Instance.Services.Get<ISettingsService>();

            ui.Open();
            settings.MouseSensitivity = 3.7f;
            ui.Close();

            Assert.AreEqual(3.7f, PlayerPrefs.GetFloat("set_mouse_sens", -1f), 0.001f,
                "Closing settings must flush deferred writes");
        }

        [UnityTest]
        public IEnumerator Settings_WithoutGameRoot_IsSafeToOpenAndClose()
        {
            // Main-menu case: no GameRoot exists yet.
            Object.DestroyImmediate(_root);
            _root = null;
            yield return null;

            var ui = SettingsUI.Create(_canvasGo.transform);
            yield return null;

            ui.Open();
            Assert.IsFalse(ui.IsOpen, "Open must no-op without services rather than throw");
            ui.Close();
            Assert.IsFalse(ui.IsOpen);
        }

        // ---------- evidence board ----------

        [UnityTest]
        public IEnumerator EvidenceBoard_HostStaysActive_WhileClosed()
        {
            var board = EvidenceBoardUI.Create(_canvasGo.transform);
            yield return null;

            Assert.IsFalse(board.IsOpen, "Board starts closed");
            Assert.IsTrue(board.gameObject.activeInHierarchy,
                "Board host must stay active while closed — its Update retry and lazy " +
                "input binding stop running if the host is disabled");
        }

        // ---------- documents ----------

        [UnityTest]
        public IEnumerator Document_ShowAndCancel_TogglesGate()
        {
            var ui = DocumentUI.Create(_canvasGo.transform);
            yield return null;

            var doc = ScriptableObject.CreateInstance<DocumentDefinition>();
            doc.Id = "doc_qualification";
            doc.Title = "Incident Report";
            doc.Body = "Body text";

            ui.Show(doc);
            Assert.IsTrue(Gate.UiOpen, "A readable document is modal");

            Press(_kb.escapeKey);
            yield return null;
            Release(_kb.escapeKey);
            yield return null;

            Assert.IsFalse(Gate.UiOpen, "Escape must dismiss the document");
            Object.DestroyImmediate(doc);
        }
    }
}

namespace Escape.Tests.PlayMode
{
    /// <summary>
    /// The evidence board is built before the player prefab exists — the
    /// ordering that silently swallowed the EvidenceBoard key before the
    /// retry bind. Kept in its own fixture because the player must be created
    /// during Setup: enabling the shared action asset mid-test trips an
    /// Input System internal fault (InputManagerStateMonitors), which is
    /// unrelated to the behaviour under test.
    /// </summary>
    public class EvidenceBoardLateBindTests : InputTestFixture
    {
        private GameObject _root;
        private GameObject _canvasGo;
        private GameObject _player;
        private Keyboard _kb;
        private EvidenceBoardUI _board;

        public override void Setup()
        {
            base.Setup();
            _kb = InputSystem.AddDevice<Keyboard>();

            _root = new GameObject("GameRoot");
            _root.AddComponent<GameRoot>();
            _canvasGo = new GameObject("Canvas", typeof(RectTransform));

            // UI first: its OnEnable runs with no player in the scene.
            _board = EvidenceBoardUI.Create(_canvasGo.transform);
            Assert.IsNull(Object.FindAnyObjectByType<PlayerInputReader>(),
                "Precondition: the board was built before any player exists");

            // Then the player prefab stand-in, with a real actions asset.
            _player = new GameObject("Player");
            _player.SetActive(false);
            var reader = _player.AddComponent<PlayerInputReader>();
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Game/Gameplay/Player/PlayerInputActions.inputactions");
            var so = new SerializedObject(reader);
            so.FindProperty("actions").objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            _player.SetActive(true);
        }

        public override void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
            if (_player != null) Object.DestroyImmediate(_player);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator TabOpensBoard_BoundToAPlayerThatSpawnedAfterIt()
        {
            yield return null;
            yield return null; // let the board's Update retry find the reader

            Press(_kb.tabKey);
            yield return null;
            Release(_kb.tabKey);
            yield return null;

            Assert.IsTrue(_board.IsOpen,
                "Tab must open the board even though the player spawned after it was built");
            Assert.IsTrue(_root.GetComponent<GameRoot>().Services.Get<IInputGate>().UiOpen,
                "Opening the board must suppress gameplay input");

            _board.Toggle();
            Assert.IsFalse(_board.IsOpen);
            Assert.IsFalse(_root.GetComponent<GameRoot>().Services.Get<IInputGate>().UiOpen,
                "Closing the board must release input ownership");
        }
    }
}
