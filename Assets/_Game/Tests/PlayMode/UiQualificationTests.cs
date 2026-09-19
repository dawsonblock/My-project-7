using System.Collections;
using Escape.Core;
using Escape.Data;
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
            PlayerPrefs.DeleteKey("set_large_ui");
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

        [UnityTest]
        public IEnumerator Settings_Close_ReturnsFocusToItsOpener()
        {
            var es = new GameObject("EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            var settings = SettingsUI.Create(_canvasGo.transform);
            var pause = PauseMenuUI.Create(_canvasGo.transform, settings);
            yield return null;
            yield return null;

            pause.Open();
            yield return null;
            settings.OpenFrom(pause.FocusReturnTarget);
            yield return null;
            settings.Close();
            yield return null;

            Assert.IsNotNull(EventSystem.current.currentSelectedGameObject,
                "Closing settings must return focus to the menu that opened it, not clear it — " +
                "a controller user would otherwise be stranded with no selection");
            pause.Close();
            Object.DestroyImmediate(es);
        }

        /// <summary>
        /// The focus anchor is any transform, not specifically a pause menu.
        /// The main menu opens settings too, and it used to close back to no
        /// selection at all because it could not name itself as the anchor.
        /// </summary>
        [UnityTest]
        public IEnumerator Settings_Close_ReturnsFocus_ToAnyNamedAnchor()
        {
            var es = new GameObject("EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            var settings = SettingsUI.Create(_canvasGo.transform);
            var anchor = UiBuilder.Panel(_canvasGo.transform, "Anchor",
                new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f), UiBuilder.PanelBg);
            UiBuilder.Vertical(anchor, 8, new RectOffset(0, 0, 0, 0));
            UiBuilder.Button(anchor, "FIRST", 44);
            yield return null;
            yield return null;

            settings.OpenFrom(anchor);
            yield return null;
            settings.Close();
            yield return null;

            Assert.IsNotNull(EventSystem.current.currentSelectedGameObject,
                "Closing settings must restore focus to the anchor it was opened from");
            Assert.AreEqual(anchor, EventSystem.current.currentSelectedGameObject.transform.parent,
                "focus must land inside the named anchor, not on some other surface");
            Object.DestroyImmediate(es);
        }

        /// <summary>
        /// Large UI has to follow the setting on the surface that exposes it —
        /// the main menu is where the player turns it on, and a one-shot read
        /// in Start meant the change only showed after leaving and re-entering.
        ///
        /// It is expressed as a reference resolution rather than a directly
        /// assigned canvas scale factor, because CanvasScaler recomputes the
        /// scale factor from the reference resolution every frame: a value
        /// written straight onto the canvas is discarded on the next resize,
        /// and a hard 1.25 is only "25% larger" at the reference resolution.
        /// </summary>
        [UnityTest]
        public IEnumerator LargeUi_IsReactive_OnTheMainMenu()
        {
            var settings = GameRoot.Instance.Services.Get<ISettingsService>();
            var original = settings.LargeUI;
            // MainMenuUI builds its canvas as a root object, so a canvas left
            // behind by another test's scene load would win GameObject.Find.
            // Clear any stale one first — this test must look at its own menu.
            var stale = GameObject.Find("MainMenuCanvas");
            if (stale != null) Object.DestroyImmediate(stale);

            var host = new GameObject("MainMenuHost", typeof(RectTransform), typeof(MainMenuUI));
            yield return null;
            yield return null;

            var canvasGo = GameObject.Find("MainMenuCanvas");
            Assert.IsNotNull(canvasGo, "MainMenuUI did not build its canvas");
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            Assert.IsNotNull(scaler, "MainMenuUI's canvas has no CanvasScaler");

            settings.LargeUI = false;
            var normal = scaler.referenceResolution;
            settings.LargeUI = true;
            var large = scaler.referenceResolution;

            Assert.Less(large.x, normal.x,
                "Large UI was toggled on the main menu and the menu's own canvas did not follow");
            Assert.AreEqual(1.25f, normal.x / large.x, 0.001f,
                "Large UI must be exactly 25% larger than the normal UI at any screen size");

            settings.LargeUI = original;
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(canvasGo);
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
