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
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
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

            _services = GameRoot.Instance.Services;
            _services.Register<IScreenFader>(_fader = fader);
            _services.Register<ITerminalUI>(_terminal = terminal);
            _services.Register<IDocumentUI>(_document = document);
            _services.Register<IEvidenceBoardUI>(_board = board);
            _services.Register<IEndingScreenUI>(_ending = ending);
        }

        private IScreenFader _fader;
        private ITerminalUI _terminal;
        private IDocumentUI _document;
        private IEvidenceBoardUI _board;
        private IEndingScreenUI _ending;
        private GameServices _services;

        private void OnDestroy()
        {
            if (_services == null) return;
            _services.Unregister<IScreenFader>(_fader);
            _services.Unregister<ITerminalUI>(_terminal);
            _services.Unregister<IDocumentUI>(_document);
            _services.Unregister<IEvidenceBoardUI>(_board);
            _services.Unregister<IEndingScreenUI>(_ending);
        }
    }
}
