using Escape.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Composition root for all in-game UI. Builds the canvas and every
    /// screen, then registers the UI facades so gameplay code can reach them
    /// through services without referencing this assembly.
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // A sane reference resolution even before settings bind, so a UI
            // canvas built without a GameRoot still lays out correctly.
            UiBuilder.ApplyLargeUi(_scaler, false);
            gameObject.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            var fader = ScreenFader.Create(transform);
            var hud = HudController.Create(transform);
            var settings = SettingsUI.Create(transform);
            var pause = PauseMenuUI.Create(transform, settings);
            var terminal = TerminalUI.Create(transform);
            var document = DocumentUI.Create(transform);
            var board = EvidenceBoardUI.Create(transform);
            var ending = EndingScreenUI.Create(transform);
            _fader = fader;
            _terminal = terminal;
            _document = document;
            _board = board;
            _ending = ending;

            TryBind();
        }

        // Register facades on enable, unregister on disable — symmetric.
        // Start retries the bind in case GameRoot lagged the scene load.
        private void OnEnable() => TryBind();
        private void Start() => TryBind();

        private void TryBind()
        {
            if (_services != null || GameRoot.Instance == null) return;
            _services = GameRoot.Instance.Services;
            _services.Register<IScreenFader>(_fader);
            _services.Register<ITerminalUI>(_terminal);
            _services.Register<IDocumentUI>(_document);
            _services.Register<IEvidenceBoardUI>(_board);
            _services.Register<IEndingScreenUI>(_ending);

            _settings = _services.Get<ISettingsService>();
            _settings.Changed += ApplyDisplaySettings;
            ApplyDisplaySettings();
        }

        private void ApplyDisplaySettings()
        {
            UiBuilder.ApplyLargeUi(_scaler, _settings != null && _settings.LargeUI);
        }

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private ISettingsService _settings;
        private IScreenFader _fader;
        private ITerminalUI _terminal;
        private IDocumentUI _document;
        private IEvidenceBoardUI _board;
        private IEndingScreenUI _ending;
        private GameServices _services;

        private void OnDisable()
        {
            if (_settings != null) _settings.Changed -= ApplyDisplaySettings;
            if (_services == null) return;
            _services.Unregister<IScreenFader>(_fader);
            _services.Unregister<ITerminalUI>(_terminal);
            _services.Unregister<IDocumentUI>(_document);
            _services.Unregister<IEvidenceBoardUI>(_board);
            _services.Unregister<IEndingScreenUI>(_ending);
            _services = null;
        }
    }
}
