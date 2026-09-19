using Escape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Pause menu: resume, settings, save, quit to menu. Pauses via
    /// timeScale so AI/detection freeze. Owns the global pause/back route:
    /// Escape/pad-start opens pause over gameplay, and backs out of whatever
    /// modal is on top (including pause itself).
    /// </summary>
    public sealed class PauseMenuUI : MonoBehaviour, ICancelableUi
    {
        private GameObject _root;
        private GameServices _services;
        private Escape.Gameplay.PlayerInputReader _input;
        private SettingsUI _settings;

        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>
        /// Where focus should return after a modal opened from here closes.
        /// The panel, not the host: the host GameObject deliberately stays
        /// active while closed (so Update can keep retrying the input bind),
        /// so handing out the host would make "pause is closed" look open.
        /// </summary>
        public Transform FocusReturnTarget => _root != null ? _root.transform : transform;

        public static PauseMenuUI Create(Transform canvasRoot, SettingsUI settings)
        {
            // The component host stays active so Update() can keep retrying
            // the input-reader bind; only the visual panel toggles.
            var go = new GameObject("PauseMenu", typeof(RectTransform), typeof(PauseMenuUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            UiBuilder.Stretch(rt);
            var ui = go.GetComponent<PauseMenuUI>();
            ui._settings = settings;
            var panelGo = new GameObject("Panel", typeof(RectTransform));
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.SetParent(rt, false);
            UiBuilder.Stretch(panelRt);
            ui.Build(panelRt);
            ui._root = panelGo;
            panelGo.SetActive(false);
            return ui;
        }

        private void Build(RectTransform root)
        {
            var dim = UiBuilder.Panel(root, "Dim", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.6f));
            var panel = UiBuilder.Panel(root, "Panel",
                new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.8f), UiBuilder.PanelBg);
            UiBuilder.Vertical(panel, 10, new RectOffset(30, 30, 30, 30));

            var t = UiBuilder.Text(panel, "T", "PAUSED", 30, Color.white, TMPro.TextAlignmentOptions.Center);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;

            Add(panel, "RESUME", () => Close());
            Add(panel, "SAVE GAME", () =>
                _services.Get<IGameCommandDispatcher>().Dispatch(new SaveGameCommand("slot1")));
            Add(panel, "SETTINGS", () => { if (_settings != null) _settings.OpenFrom(FocusReturnTarget); });
            Add(panel, "QUIT TO MENU", () =>
            {
                Time.timeScale = 1f;
                Close();
                _services.Get<IGameCommandDispatcher>()
                    .Dispatch(new ChangeSceneCommand(Escape.Data.SceneId.MainMenu));
            });
        }

        private void Add(RectTransform parent, string label, UnityEngine.Events.UnityAction act)
        {
            var b = UiBuilder.Button(parent, label, 46);
            b.onClick.AddListener(act);
        }

        // Bind on enable; Start retries in case GameRoot lagged the scene
        // load — the menu must never NRE on a missing service root.
        private void OnEnable() => TryBindServices();

        private void Start()
        {
            TryBindServices();
            TryBindInput();
        }

        private void TryBindServices()
        {
            if (_services == null && GameRoot.Instance != null)
                _services = GameRoot.Instance.Services;
        }

        // The player prefab can spawn after the UI is built, so keep
        // retrying the reader lookup until it exists — otherwise global
        // pause/back events would be silently unbound for the whole scene.
        private void Update()
        {
            if (_services == null) TryBindServices();
            if (_input == null) TryBindInput();
        }

        private void TryBindInput()
        {
            var reader = FindAnyObjectByType<Escape.Gameplay.PlayerInputReader>();
            if (reader == null) return;
            _input = reader;
            _input.PausePressed += OnGlobalPause;
            _input.CancelPressed += OnGlobalCancel;
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.PausePressed -= OnGlobalPause;
                _input.CancelPressed -= OnGlobalCancel;
            }
        }

        /// <summary>
        /// Global pause/back semantics: nothing open → open pause; pause or
        /// any modal open → back out of the topmost modal. Keeps Escape as
        /// "back" everywhere instead of stacking pause over terminals.
        /// </summary>
        private void OnGlobalPause()
        {
            TryBindServices();
            if (_services == null) return;
            var gate = _services.Get<IInputGate>();
            if (!gate.UiOpen) Open();
            else gate.CancelTop();
        }

        private void OnGlobalCancel()
        {
            TryBindServices();
            if (_services == null) return;
            _services.Get<IInputGate>().CancelTop();
        }

        /// <summary>ICancelableUi — invoked when pause is the top modal.</summary>
        public void Cancel() => Close();

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            TryBindServices();
            if (_services == null) return;
            _root.SetActive(true);
            Time.timeScale = 0f;
            _services.Get<IInputGate>().PushUi(this);
            UiBuilder.SelectFirst(_root.transform);
        }

        public void Close()
        {
            UiBuilder.Deselect();
            _root.SetActive(false);
            Time.timeScale = 1f;
            if (_services != null) _services.Get<IInputGate>().PopUi(this);
            if (_settings != null && _settings.IsOpen) _settings.Close();
        }
    }
}
