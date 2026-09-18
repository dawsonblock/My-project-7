using System.Collections.Generic;
using Escape.Core;
using Escape.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Evidence board: collected evidence list, detail pane, corroboration /
    /// contradiction relationships, insights. Predefined relationships only
    /// — no free-form graph editor.
    /// </summary>
    public sealed class EvidenceBoardUI : MonoBehaviour, IEvidenceBoardUI, ICancelableUi
    {
        private GameObject _root;
        private RectTransform _list;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailBody;
        private TextMeshProUGUI _relations;
        private TextMeshProUGUI _insights;

        private GameServices _services;
        private PlayerInputReaderRef _input;
        private readonly List<GameObject> _items = new List<GameObject>();

        public bool IsOpen => _root != null && _root.activeSelf;

        private struct PlayerInputReaderRef
        {
            public Escape.Gameplay.PlayerInputReader Reader;
        }

        public static EvidenceBoardUI Create(Transform canvasRoot)
        {
            var go = new GameObject("EvidenceBoard", typeof(RectTransform), typeof(EvidenceBoardUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            UiBuilder.Stretch(rt);
            var ui = go.GetComponent<EvidenceBoardUI>();
            // Host stays active so Update() can retry the reader bind;
            // only the visual panel toggles.
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
            var panel = UiBuilder.Panel(root, "Board",
                new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), UiBuilder.PanelBg);

            var columns = new GameObject("Columns", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var crt = (RectTransform)columns.transform;
            crt.SetParent(panel, false);
            UiBuilder.Stretch(crt);
            var hl = columns.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 16;
            hl.padding = new RectOffset(24, 24, 24, 24);

            // Left: evidence list
            var left = new GameObject("List", typeof(RectTransform));
            left.transform.SetParent(crt, false);
            left.AddComponent<LayoutElement>().preferredWidth = 320;
            UiBuilder.Scroll(left.transform, out _list);
            UiBuilder.Vertical(_list, 4, new RectOffset(4, 4, 4, 4));

            // Middle: detail + relationships
            var mid = new GameObject("Detail", typeof(RectTransform));
            mid.transform.SetParent(crt, false);
            mid.AddComponent<LayoutElement>().flexibleWidth = 1;
            UiBuilder.Vertical(mid.transform, 10, new RectOffset(0, 0, 0, 0));

            _detailTitle = UiBuilder.Text(mid.transform, "DT", "Select evidence", 24, Color.white);
            _detailTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            _detailBody = UiBuilder.Text(mid.transform, "DB", "", 17, UiBuilder.TextDim);
            _detailBody.gameObject.AddComponent<LayoutElement>().preferredHeight = 160;
            _detailBody.alignment = TextAlignmentOptions.TopLeft;
            _relations = UiBuilder.Text(mid.transform, "Rel", "", 16, UiBuilder.Accent);
            _relations.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            _relations.alignment = TextAlignmentOptions.TopLeft;

            // Right: insights
            var right = new GameObject("Insights", typeof(RectTransform));
            right.transform.SetParent(crt, false);
            right.AddComponent<LayoutElement>().preferredWidth = 300;
            UiBuilder.Vertical(right.transform, 8, new RectOffset(0, 0, 0, 0));
            var ih = UiBuilder.Text(right.transform, "IH", "INSIGHTS", 20, UiBuilder.Accent);
            ih.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            _insights = UiBuilder.Text(right.transform, "I", "—", 15, UiBuilder.TextDim);
            _insights.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            _insights.alignment = TextAlignmentOptions.TopLeft;

            var close = UiBuilder.Button(panel, "CLOSE  [Tab]", 36);
            var closeRt = (RectTransform)close.transform;
            closeRt.anchorMin = new Vector2(0.4f, 0);
            closeRt.anchorMax = new Vector2(0.6f, 0);
            closeRt.offsetMin = new Vector2(0, 6);
            closeRt.offsetMax = new Vector2(0, 42);
            close.onClick.AddListener(Close);
        }

        private void Start()
        {
            TryBindServices();
            TryBindInput();
        }

        private void OnEnable()
        {
            TryBindServices();
            if (_input.Reader == null) TryBindInput();
        }

        private void TryBindServices()
        {
            if (_services == null && GameRoot.Instance != null)
                _services = GameRoot.Instance.Services;
        }

        private void TryBindInput()
        {
            if (_input.Reader != null) return;
            var reader = FindAnyObjectByType<Escape.Gameplay.PlayerInputReader>();
            if (reader == null) return;
            _input = new PlayerInputReaderRef { Reader = reader };
            _input.Reader.EvidenceBoardPressed += Toggle;
        }

        private void OnDestroy()
        {
            if (_input.Reader != null)
                _input.Reader.EvidenceBoardPressed -= Toggle;
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else OpenBoard();
        }

        private void OpenBoard()
        {
            TryBindServices();
            if (_services == null) return;
            Rebuild();
            _root.SetActive(true);
            _services.Get<IInputGate>().PushUi(this);
            UiBuilder.SelectFirst(_root.transform);
        }

        public void Cancel() => Close();

        public void Close()
        {
            UiBuilder.Deselect();
            _root.SetActive(false);
            if (_services != null) _services.Get<IInputGate>().PopUi(this);
        }

        private void Rebuild()
        {
            foreach (var i in _items) Destroy(i);
            _items.Clear();
            var state = _services.Get<IGameStateService>().State;
            var content = _services.Get<IContentDatabase>();

            foreach (var id in state.CollectedEvidence)
            {
                if (!content.TryGetEvidence(id, out var def)) continue;
                var btn = UiBuilder.Button(_list, $"{CategoryTag(def.Category)} {def.Title}", 36);
                var captured = def;
                btn.onClick.AddListener(() => ShowDetail(captured));
                _items.Add(btn.gameObject);
            }

            _insights.text = state.GainedInsights.Count == 0 ? "—" : "";
            foreach (var id in state.GainedInsights)
                if (content.TryGetInsight(id, out var ins))
                    _insights.text += $"◆ {ins.Title}\n  {ins.Description}\n\n";

            if (_items.Count > 0 && content.TryGetEvidence(state.CollectedEvidence[^1], out var last))
                ShowDetail(last);
        }

        private void ShowDetail(EvidenceDefinition def)
        {
            var state = _services.Get<IGameStateService>().State;
            _detailTitle.text = def.Title;
            _detailBody.text = def.Description;

            var rel = new System.Text.StringBuilder();
            foreach (var c in def.Corroborates)
            {
                if (c == null) continue;
                bool held = state.CollectedEvidence.Contains(c.Id);
                rel.AppendLine(held
                    ? $"▲ corroborates: {c.Title} (collected)"
                    : $"△ corroborates: ??? (not yet found)");
            }
            foreach (var c in def.Contradicts)
            {
                if (c == null) continue;
                bool held = state.CollectedEvidence.Contains(c.Id);
                rel.AppendLine(held
                    ? $"✖ contradicted by: {c.Title} (collected)"
                    : $"✖ contradicted by: ??? (unresolved)");
            }
            if (def.RequiredForBroadcast)
                rel.AppendLine("■ required for broadcast");
            _relations.text = rel.ToString();
        }

        private static string CategoryTag(EvidenceCategory c) => c switch
        {
            EvidenceCategory.Primary => "◼",
            EvidenceCategory.Corroborating => "▲",
            EvidenceCategory.Contradictory => "✖",
            EvidenceCategory.Operational => "⚙",
            EvidenceCategory.Hidden => "?",
            _ => "•"
        };

        private void Update()
        {
            // The player prefab can spawn after the UI is built; retry until
            // the reader exists or EvidenceBoard presses are silently lost.
            if (_input.Reader == null) TryBindInput();
        }
    }
}
