using System.Collections;
using System.Collections.Generic;
using Escape.Core;
using Escape.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Restrained HUD: interaction prompt, objective, detection meter,
    /// hidden indicator, notifications. Purely a view — listens to events,
    /// owns no state.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        private TextMeshProUGUI _prompt;
        private TextMeshProUGUI _objective;
        private TextMeshProUGUI _hidden;
        private TextMeshProUGUI _notify;
        private TextMeshProUGUI _lures;
        private TextMeshProUGUI _alert;
        private Image _detectFill;
        private Image _detectBg;
        private Image _staminaBg;
        private Image _staminaFill;

        private PlayerInteraction _interaction;
        private IGameEventBus _events;
        private IDetectionService _detection;
        private IObjectiveService _objectives;
        private IGameStateService _gameState;
        private IAudioService _audio;

        private readonly Queue<(string msg, float until)> _messages = new Queue<(string, float)>();
        private float _notifyUntil;
        private bool _wasAlerted;

        public static HudController Create(Transform canvasRoot)
        {
            var go = new GameObject("HUD", typeof(RectTransform), typeof(HudController));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            UiBuilder.Stretch(rt);
            var hud = go.GetComponent<HudController>();
            hud.Build(rt);
            return hud;
        }

        private void Build(RectTransform root)
        {
            _prompt = UiBuilder.Text(root, "Prompt", "", 22, Color.white, TextAlignmentOptions.Center);
            UiBuilder.SetAnchored((RectTransform)_prompt.transform,
                new Vector2(0.3f, 0.4f), new Vector2(0.7f, 0.47f), Vector2.zero, Vector2.zero);

            _objective = UiBuilder.Text(root, "Objective", "", 18, UiBuilder.TextDim, TextAlignmentOptions.TopLeft);
            UiBuilder.SetAnchored((RectTransform)_objective.transform,
                new Vector2(0.02f, 0.92f), new Vector2(0.5f, 0.99f), Vector2.zero, Vector2.zero);

            _hidden = UiBuilder.Text(root, "Hidden", "HIDDEN", 16, UiBuilder.Accent, TextAlignmentOptions.TopRight);
            UiBuilder.SetAnchored((RectTransform)_hidden.transform,
                new Vector2(0.85f, 0.92f), new Vector2(0.98f, 0.99f), Vector2.zero, Vector2.zero);

            _notify = UiBuilder.Text(root, "Notify", "", 18, Color.white, TextAlignmentOptions.Center);
            UiBuilder.SetAnchored((RectTransform)_notify.transform,
                new Vector2(0.2f, 0.78f), new Vector2(0.8f, 0.88f), Vector2.zero, Vector2.zero);

            _alert = UiBuilder.Text(root, "Alert", "", 20, new Color(1f, 0.25f, 0.15f),
                TextAlignmentOptions.Top);
            UiBuilder.SetAnchored((RectTransform)_alert.transform,
                new Vector2(0.3f, 0.9f), new Vector2(0.7f, 0.98f), Vector2.zero, Vector2.zero);

            _lures = UiBuilder.Text(root, "Lures", "", 16, UiBuilder.TextDim,
                TextAlignmentOptions.BottomLeft);
            UiBuilder.SetAnchored((RectTransform)_lures.transform,
                new Vector2(0.02f, 0.02f), new Vector2(0.25f, 0.07f), Vector2.zero, Vector2.zero);

            var detBg = UiBuilder.Panel(root, "DetectBg",
                new Vector2(0.35f, 0.05f), new Vector2(0.65f, 0.065f), new Color(0, 0, 0, 0.6f));
            _detectBg = detBg.GetComponent<Image>();
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            _detectFill = fillGo.GetComponent<Image>();
            var frt = (RectTransform)fillGo.transform;
            frt.SetParent(detBg, false);
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.pivot = new Vector2(0, 0.5f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            _detectFill.color = UiBuilder.Warn;
            _detectFill.type = Image.Type.Filled;
            _detectFill.fillMethod = Image.FillMethod.Horizontal;
            _detectFill.fillAmount = 0f;

            var stamBg = UiBuilder.Panel(root, "StaminaBg",
                new Vector2(0.35f, 0.025f), new Vector2(0.65f, 0.038f), new Color(0, 0, 0, 0.6f));
            _staminaBg = stamBg.GetComponent<Image>();
            var sfillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            _staminaFill = sfillGo.GetComponent<Image>();
            var srt = (RectTransform)sfillGo.transform;
            srt.SetParent(stamBg, false);
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.pivot = new Vector2(0, 0.5f);
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            _staminaFill.color = UiBuilder.Accent;
            _staminaFill.type = Image.Type.Filled;
            _staminaFill.fillMethod = Image.FillMethod.Horizontal;
            _staminaFill.fillAmount = 1f;
        }

        private void Start()
        {
            var services = GameRoot.Instance.Services;
            _events = services.Get<IGameEventBus>();
            _detection = services.Get<IDetectionService>();
            _objectives = services.Get<IObjectiveService>();
            _gameState = services.Get<IGameStateService>();
            services.TryGet<IAudioService>(out _audio);
            _interaction = FindAnyObjectByType<PlayerInteraction>();

            _events.Subscribe<SystemMessageEvent>(OnSystemMessage);
            _events.Subscribe<ObjectiveUpdatedEvent>(OnObjectives);
            RefreshObjective();
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.Unsubscribe<SystemMessageEvent>(OnSystemMessage);
            _events.Unsubscribe<ObjectiveUpdatedEvent>(OnObjectives);
        }

        private void OnObjectives(ObjectiveUpdatedEvent _) => RefreshObjective();

        private void RefreshObjective()
        {
            var title = _objectives.CurrentObjectiveTitle;
            _objective.text = string.IsNullOrEmpty(title) ? "" : $"▸ {title}";
        }

        private void OnSystemMessage(SystemMessageEvent e)
        {
            _notify.text = e.Message;
            _notifyUntil = Time.unscaledTime + e.Duration;
        }

        private void Update()
        {
            if (_detection == null || _gameState == null) return;
            if (_interaction != null)
            {
                var p = _interaction.CurrentPrompt;
                _prompt.text = p.Label;
            }
            if (Time.unscaledTime > _notifyUntil) _notify.text = "";

            float d = _detection.Detection / 100f;
            _detectFill.fillAmount = d;
            _detectBg.enabled = d > 0.01f;
            _detectFill.enabled = d > 0.01f;
            _detectFill.color = _detection.Level >= DetectionLevel.Imminent
                ? new Color(1f, 0.15f, 0.1f) : UiBuilder.Warn;

            var state = _interaction != null ? _interaction.Context?.State : null;
            _hidden.enabled = state != null && state.Concealed;

            _lures.text = $"LURES ×{_gameState.State.Lures}   [G] throw   [T] whistle";

            float stamina = state != null ? state.Stamina : 100f;
            bool showStamina = stamina < 99.5f;
            _staminaBg.enabled = showStamina;
            _staminaFill.enabled = showStamina;
            if (showStamina)
            {
                _staminaFill.fillAmount = stamina / 100f;
                _staminaFill.color = state.Exhausted ? new Color(1f, 0.25f, 0.15f) : UiBuilder.Accent;
            }

            var gs = _gameState.State;
            _alert.text = gs.Lockdown ? "— LOCKDOWN —" : gs.Alert ? "— SECURITY ALERT —" : "";
            if ((gs.Alert || gs.Lockdown) && !_wasAlerted)
            {
                var sting = Data.ClipLibrary.Get()?.alertSting;
                if (sting != null) _audio?.Play2D(sting, 0.9f);
            }
            _wasAlerted = gs.Alert || gs.Lockdown;
        }
    }
}
