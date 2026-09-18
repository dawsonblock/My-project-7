using System.Text;
using Escape.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Ending screen shows the evaluation breakdown — evidence transmitted,
    /// corroborated claims, what was missing — not just a title card.
    /// </summary>
    public sealed class EndingScreenUI : MonoBehaviour, IEndingScreenUI
    {
        private GameObject _root;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private GameServices _services;
        private IGameEventBus _events;

        public static EndingScreenUI Create(Transform canvasRoot)
        {
            var go = new GameObject("EndingScreen", typeof(RectTransform), typeof(EndingScreenUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            UiBuilder.Stretch(rt);
            var ui = go.GetComponent<EndingScreenUI>();
            ui.Build(rt);
            ui._root = go;
            go.SetActive(false);
            return ui;
        }

        private void Build(RectTransform root)
        {
            var panel = UiBuilder.Panel(root, "Panel", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.96f));
            UiBuilder.Vertical(panel, 14, new RectOffset(80, 80, 60, 60));

            _title = UiBuilder.Text(panel, "T", "", 40, UiBuilder.Accent, TextAlignmentOptions.Center);
            _title.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;

            var host = new GameObject("BodyHost", typeof(RectTransform));
            host.transform.SetParent(panel, false);
            host.AddComponent<LayoutElement>().flexibleHeight = 1;
            UiBuilder.Scroll(host.transform, out var content);
            _body = UiBuilder.Text(content, "B", "", 18, Color.white);
            _body.alignment = TextAlignmentOptions.TopLeft;

            var btn = UiBuilder.Button(panel, "RETURN TO MENU", 48);
            btn.onClick.AddListener(() =>
            {
                Close();
                _services.Get<IGameCommandDispatcher>()
                    .Dispatch(new ChangeSceneCommand(Escape.Data.SceneId.MainMenu));
            });
        }

        private void Start()
        {
            _services = GameRoot.Instance.Services;
            _events = _services.Get<IGameEventBus>();
            _events.Subscribe<BroadcastCompletedEvent>(OnBroadcast);
        }

        private void OnDestroy()
        {
            _events?.Unsubscribe<BroadcastCompletedEvent>(OnBroadcast);
        }

        private void OnBroadcast(BroadcastCompletedEvent e)
        {
            var endings = _services.Get<EndingService>();
            Show(endings.Evaluate());
        }

        public void Show(EndingResult result)
        {
            _services ??= GameRoot.Instance.Services;
            _title.text = result.Ending != null ? result.Ending.Title : "SIGNAL LOST";

            var sb = new StringBuilder();
            sb.AppendLine(result.Ending != null ? result.Ending.Description : "The transmission died.");
            sb.AppendLine();
            sb.AppendLine("— EVIDENCE TRANSMITTED —");
            foreach (var e in result.TransmittedEvidence) sb.AppendLine($"  ◼ {e}");
            if (result.CorroboratedClaims.Count > 0)
            {
                sb.AppendLine("— CORROBORATED —");
                foreach (var c in result.CorroboratedClaims) sb.AppendLine($"  ▲ {c}");
            }
            if (result.GainedInsights.Count > 0)
            {
                sb.AppendLine("— INSIGHTS —");
                var content = _services.Get<IContentDatabase>();
                foreach (var i in result.GainedInsights)
                    if (content.TryGetInsight(i, out var ins)) sb.AppendLine($"  ◆ {ins.Title}");
            }
            if (result.MissingEvidence.Count > 0)
            {
                sb.AppendLine("— MISSING —");
                foreach (var m in result.MissingEvidence) sb.AppendLine($"  □ {m}");
            }
            if (result.UnresolvedContradictions.Count > 0)
            {
                sb.AppendLine("— UNRESOLVED —");
                foreach (var u in result.UnresolvedContradictions) sb.AppendLine($"  ✖ {u}");
            }
            _body.text = sb.ToString();

            _root.SetActive(true);
            _services.Get<IInputGate>().PushUi(this);
            UiBuilder.SelectFirst(_root.transform);
        }

        public void Close()
        {
            UiBuilder.Deselect();
            _root.SetActive(false);
            _services.Get<IInputGate>().PopUi(this);
        }
    }
}
