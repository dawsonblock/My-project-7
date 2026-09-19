using Escape.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Main menu: Continue / New Game / Load / Settings / Credits / Quit.
    /// Builds its own canvas in MainMenu.unity. Sub-panels (load slots,
    /// new-game confirm, credits) swap in over the button column; Escape
    /// backs out to the main column.
    /// </summary>
    public sealed class MainMenuUI : MonoBehaviour
    {
        private GameServices _services;
        private ISaveService _saves;
        private ISettingsService _settings;
        private IGameCommandDispatcher _dispatcher;
        private RectTransform _menuPanel;
        private RectTransform _loadPanel, _confirmPanel, _creditsPanel;
        private SettingsUI _settingsUi;
        private Canvas _canvas;
        private CanvasScaler _scaler;

        private void Start()
        {
            if (GameRoot.Instance == null)
            {
                Debug.LogError("[MainMenuUI] No GameRoot — load Bootstrap first.");
                return;
            }
            _services = GameRoot.Instance.Services;
            _saves = _services.Get<ISaveService>();
            _dispatcher = _services.Get<IGameCommandDispatcher>();
            _settings = _services.Get<ISettingsService>();

            var canvasGo = new GameObject("MainMenuCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _scaler = canvasGo.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            // The shared settings panel works outside the pause menu too.
            _settingsUi = SettingsUI.Create(canvasGo.transform);

            // Large UI has to follow the setting, not be sampled once: this
            // menu is where the player turns it on, so a one-shot read meant
            // the change only appeared after leaving and re-entering.
            _settings.Changed += ApplyDisplaySettings;
            ApplyDisplaySettings();

            Build(canvasGo.transform);
        }

        private void ApplyDisplaySettings() => UiBuilder.ApplyLargeUi(_scaler, _settings.LargeUI);

        private void OnDestroy()
        {
            if (_settings != null) _settings.Changed -= ApplyDisplaySettings;
        }

        private void Build(Transform root)
        {
            UiBuilder.Panel(root, "Bg", Vector2.zero, Vector2.one, new Color(0.01f, 0.02f, 0.02f, 1f));
            var title = UiBuilder.Text(root, "Title", "ESCAPE THE ELITES", 54, UiBuilder.Accent, TextAlignmentOptions.Center);
            UiBuilder.SetAnchored((RectTransform)title.transform,
                new Vector2(0.2f, 0.72f), new Vector2(0.8f, 0.85f), Vector2.zero, Vector2.zero);
            var sub = UiBuilder.Text(root, "Sub", "— THE BROADCAST —", 22, UiBuilder.TextDim, TextAlignmentOptions.Center);
            UiBuilder.SetAnchored((RectTransform)sub.transform,
                new Vector2(0.2f, 0.66f), new Vector2(0.8f, 0.73f), Vector2.zero, Vector2.zero);

            _menuPanel = UiBuilder.Panel(root, "Menu",
                new Vector2(0.38f, 0.15f), new Vector2(0.62f, 0.6f), new Color(0, 0, 0, 0));
            _menuPanel.GetComponent<Image>().enabled = false;
            UiBuilder.Vertical(_menuPanel, 8, new RectOffset(0, 0, 0, 0));

            var newest = _saves.MostRecentSlot();
            var continueBtn = Add(_menuPanel, "CONTINUE",
                () => _dispatcher.Dispatch(new LoadGameCommand(newest)));
            continueBtn.interactable = newest != null;

            Add(_menuPanel, "NEW GAME", OnNewGame);
            Add(_menuPanel, "LOAD GAME", ShowLoadPanel).interactable = newest != null;
            // Naming the menu column as the focus anchor: closing settings
            // without one left a controller/keyboard user with no selection.
            Add(_menuPanel, "SETTINGS", () => _settingsUi.OpenFrom(_menuPanel));
            Add(_menuPanel, "CREDITS", ShowCredits);
            Add(_menuPanel, "QUIT", () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });

            // Gamepad/keyboard navigation lands on the first usable button.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(
                    (newest != null ? continueBtn : _menuPanel.GetComponentInChildren<Button>()).gameObject);
        }

        private void Update()
        {
            // No PlayerInputReader in this scene — Escape backs out of the
            // load/confirm/credits panels to the main column.
            if (UnityEngine.InputSystem.Keyboard.current == null ||
                !UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                return;
            foreach (var p in new[] { _loadPanel, _confirmPanel, _creditsPanel })
                if (p != null && p.gameObject.activeSelf) { Back(p); return; }
        }

        private Button Add(RectTransform parent, string label, UnityEngine.Events.UnityAction act)
        {
            var b = UiBuilder.Button(parent, label, 52);
            b.onClick.AddListener(act);
            return b;
        }

        private void Swap(RectTransform panel)
        {
            _menuPanel.gameObject.SetActive(false);
            panel.gameObject.SetActive(true);
            var first = panel.GetComponentInChildren<Button>();
            if (first != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private void Back(RectTransform panel)
        {
            panel.gameObject.SetActive(false);
            _menuPanel.gameObject.SetActive(true);
            var first = _menuPanel.GetComponentInChildren<Button>();
            if (first != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        // --- New game -----------------------------------------------------

        private void OnNewGame()
        {
            if (_saves.MostRecentSlot() == null) { StartNewGame(); return; }
            if (_confirmPanel == null) _confirmPanel = BuildConfirm();
            Swap(_confirmPanel);
        }

        private void StartNewGame()
        {
            _services.Get<IGameStateService>().NewGame();
            _dispatcher.Dispatch(new ChangeSceneCommand(Escape.Data.SceneId.Dock));
        }

        private RectTransform BuildConfirm()
        {
            var go = UiBuilder.Panel(_canvas.transform, "ConfirmNewGame",
                new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.6f), UiBuilder.PanelBg);
            UiBuilder.Vertical(go, 12, new RectOffset(28, 28, 28, 28));
            UiBuilder.Text(go, "T", "START NEW GAME?", 26, Color.white, TextAlignmentOptions.Center);
            UiBuilder.Text(go, "D", "Existing autosave progress may be overwritten.",
                16, UiBuilder.TextDim, TextAlignmentOptions.Center);
            var yes = UiBuilder.Button(go, "START NEW GAME", 46);
            yes.onClick.AddListener(StartNewGame);
            var no = UiBuilder.Button(go, "CANCEL", 46);
            no.onClick.AddListener(() => Back(_confirmPanel));
            go.gameObject.SetActive(false);
            return go;
        }

        // --- Load game ----------------------------------------------------

        private void ShowLoadPanel()
        {
            if (_loadPanel == null) _loadPanel = BuildLoadPanel();
            else RefreshLoadPanel();
            Swap(_loadPanel);
        }

        private RectTransform BuildLoadPanel()
        {
            var go = UiBuilder.Panel(_canvas.transform, "LoadPanel",
                new Vector2(0.22f, 0.2f), new Vector2(0.78f, 0.75f), UiBuilder.PanelBg);
            UiBuilder.Vertical(go, 8, new RectOffset(24, 24, 24, 24));
            UiBuilder.Text(go, "T", "LOAD GAME", 26, Color.white, TextAlignmentOptions.Center);
            foreach (var slot in _saves.Slots)
            {
                var info = _saves.GetSlotInfo(slot);
                var b = UiBuilder.Button(go, SlotLabel(slot, info), 46);
                b.interactable = info.Valid;
                var s = slot;
                b.onClick.AddListener(() => _dispatcher.Dispatch(new LoadGameCommand(s)));
            }
            var back = UiBuilder.Button(go, "BACK", 46);
            back.onClick.AddListener(() => Back(_loadPanel));
            go.gameObject.SetActive(false);
            return go;
        }

        private void RefreshLoadPanel()
        {
            // Rebuild row labels — save state may have changed since last open.
            var buttons = _loadPanel.GetComponentsInChildren<Button>(true);
            int i = 0;
            foreach (var slot in _saves.Slots)
            {
                if (i >= buttons.Length - 1) break; // last button is BACK
                var info = _saves.GetSlotInfo(slot);
                buttons[i].interactable = info.Valid;
                var t = buttons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (t != null) t.text = SlotLabel(slot, info);
                i++;
            }
        }

        private string SlotLabel(string slot, SaveSlotInfo info)
        {
            var name = slot == SaveService.Autosave ? "AUTOSAVE" : slot.ToUpperInvariant();
            if (!info.Exists) return $"{name}  —  EMPTY";
            if (!info.Valid) return $"{name}  —  CORRUPTED";
            var stamp = System.DateTime.TryParse(info.TimestampUtc, out var dt)
                ? dt.ToLocalTime().ToString("MMM d, HH:mm") : "?";
            var tag = info.Recoverable ? " (recovered)" : "";
            return $"{name}{tag}  —  {info.SceneName}  —  " +
                   $"{info.EvidenceCount} evidence, {info.ObjectivesCompleted} objectives  —  {stamp}";
        }

        // --- Credits -------------------------------------------------------

        private void ShowCredits()
        {
            if (_creditsPanel == null) _creditsPanel = BuildCredits();
            Swap(_creditsPanel);
        }

        private RectTransform BuildCredits()
        {
            var go = UiBuilder.Panel(_canvas.transform, "Credits",
                new Vector2(0.3f, 0.25f), new Vector2(0.7f, 0.7f), UiBuilder.PanelBg);
            UiBuilder.Vertical(go, 10, new RectOffset(28, 28, 28, 28));
            UiBuilder.Text(go, "T", "CREDITS", 26, Color.white, TextAlignmentOptions.Center);
            UiBuilder.Text(go, "B",
                "ESCAPE THE ELITES: THE BROADCAST\n\n" +
                "An investigative stealth game.\n\n" +
                "Audio — Kenney Impact Sounds, Interface Sounds, UI Audio (CC0)\n" +
                "Textures — ambientCG (CC0)\n\n" +
                "See Assets/_Game/THIRD_PARTY_NOTICES.md",
                15, UiBuilder.TextDim, TextAlignmentOptions.Top);
            var back = UiBuilder.Button(go, "BACK", 46);
            back.onClick.AddListener(() => Back(_creditsPanel));
            go.gameObject.SetActive(false);
            return go;
        }
    }
}
