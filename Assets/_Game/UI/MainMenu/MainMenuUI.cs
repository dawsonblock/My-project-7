using Escape.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Main menu: New Game / Continue / Load / Settings / Credits / Quit.
    /// Builds its own canvas so it lives in MainMenu.unity.
    /// </summary>
    public sealed class MainMenuUI : MonoBehaviour
    {
        private GameServices _services;

        private void Start()
        {
            if (GameRoot.Instance == null)
            {
                Debug.LogError("[MainMenuUI] No GameRoot — load Bootstrap first.");
                return;
            }
            _services = GameRoot.Instance.Services;

            var canvasGo = new GameObject("MainMenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            Build(canvasGo.transform);
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

            var menu = UiBuilder.Panel(root, "Menu",
                new Vector2(0.38f, 0.15f), new Vector2(0.62f, 0.6f), new Color(0, 0, 0, 0));
            menu.GetComponent<Image>().enabled = false;
            UiBuilder.Vertical(menu, 8, new RectOffset(0, 0, 0, 0));

            var saves = _services.Get<ISaveService>();
            var dispatcher = _services.Get<IGameCommandDispatcher>();

            Add(menu, "NEW GAME", () =>
            {
                _services.Get<IGameStateService>().NewGame();
                dispatcher.Dispatch(new ChangeSceneCommand(Escape.Data.SceneId.Dock));
            });

            var continueBtn = Add(menu, "CONTINUE", () =>
                dispatcher.Dispatch(new LoadGameCommand(saves.MostRecentSlot())));
            continueBtn.interactable = saves.MostRecentSlot() != null;

            Add(menu, "LOAD GAME", () =>
                dispatcher.Dispatch(new LoadGameCommand("slot1")));
            Add(menu, "SETTINGS", () => { });
            Add(menu, "CREDITS", () =>
                dispatcher.Dispatch(new ShowSystemMessageCommand("A stealth-investigation prototype.")));
            Add(menu, "QUIT", () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private Button Add(RectTransform parent, string label, UnityEngine.Events.UnityAction act)
        {
            var b = UiBuilder.Button(parent, label, 52);
            b.onClick.AddListener(act);
            return b;
        }
    }
}
