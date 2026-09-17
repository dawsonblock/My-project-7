using Escape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Pause menu: resume, settings, save, quit to menu. Pauses via
    /// timeScale so AI/detection freeze.
    /// </summary>
    public sealed class PauseMenuUI : MonoBehaviour
    {
        private GameObject _root;
        private GameServices _services;
        private Escape.Gameplay.PlayerInputReader _input;
        private SettingsUI _settings;

        public bool IsOpen => _root != null && _root.activeSelf;

        public static PauseMenuUI Create(Transform canvasRoot, SettingsUI settings)
        {
            var go = new GameObject("PauseMenu", typeof(RectTransform), typeof(PauseMenuUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            UiBuilder.Stretch(rt);
            var ui = go.GetComponent<PauseMenuUI>();
            ui._settings = settings;
            ui.Build(rt);
            ui._root = go;
            go.SetActive(false);
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
            Add(panel, "SETTINGS", () => { if (_settings != null) _settings.OpenFrom(this); });
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

        private void Start()
        {
            _services = GameRoot.Instance.Services;
            _input = FindAnyObjectByType<Escape.Gameplay.PlayerInputReader>();
            if (_input != null) _input.PausePressed += Toggle;
        }

        private void OnDestroy()
        {
            if (_input != null) _input.PausePressed -= Toggle;
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            _services ??= GameRoot.Instance.Services;
            _root.SetActive(true);
            Time.timeScale = 0f;
            _services.Get<IInputGate>().PushUi(this);
        }

        public void Close()
        {
            _root.SetActive(false);
            Time.timeScale = 1f;
            _services.Get<IInputGate>().PopUi(this);
            if (_settings != null && _settings.IsOpen) _settings.Close();
        }
    }
}
