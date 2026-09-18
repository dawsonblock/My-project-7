using Escape.Core;
using Escape.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Readable document viewer. Esc/E closes.
    /// </summary>
    public sealed class DocumentUI : MonoBehaviour, IDocumentUI, ICancelableUi
    {
        private GameObject _root;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private GameServices _services;

        public static DocumentUI Create(Transform canvasRoot)
        {
            var go = new GameObject("DocumentUI", typeof(RectTransform), typeof(DocumentUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            UiBuilder.Stretch(rt);
            var ui = go.GetComponent<DocumentUI>();
            ui.Build(rt);
            ui._root = go;
            go.SetActive(false);
            return ui;
        }

        private void Build(RectTransform root)
        {
            var panel = UiBuilder.Panel(root, "Paper",
                new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.9f), new Color(0.92f, 0.9f, 0.8f, 0.98f));
            UiBuilder.Vertical(panel, 12, new RectOffset(40, 40, 32, 32));

            _title = UiBuilder.Text(panel, "Title", "", 26, new Color(0.1f, 0.1f, 0.1f));
            _title.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;

            var bodyHost = new GameObject("BodyHost", typeof(RectTransform));
            bodyHost.transform.SetParent(panel, false);
            bodyHost.AddComponent<LayoutElement>().flexibleHeight = 1;
            UiBuilder.Scroll(bodyHost.transform, out var content);
            _body = UiBuilder.Text(content, "Body", "", 18, new Color(0.12f, 0.12f, 0.1f));
            _body.alignment = TextAlignmentOptions.TopLeft;

            var close = UiBuilder.Button(panel, "PUT DOWN  [E]");
            close.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            close.onClick.AddListener(Close);
        }

        private void Start() => TryBindServices();
        private void OnEnable() => TryBindServices();

        private void TryBindServices()
        {
            if (_services == null && GameRoot.Instance != null)
                _services = GameRoot.Instance.Services;
        }

        public void Show(DocumentDefinition doc)
        {
            TryBindServices();
            if (_services == null) return;
            _title.text = doc.Title;
            _body.text = doc.Body;
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

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (_root.activeSelf && kb != null &&
                (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
                Close();
        }
    }
}
