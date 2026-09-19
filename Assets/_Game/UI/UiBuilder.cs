using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Programmatic uGUI construction helpers so screens need no prefab
    /// wiring during prototyping.
    /// </summary>
    public static class UiBuilder
    {
        public static readonly Color PanelBg = new Color(0.02f, 0.05f, 0.04f, 0.92f);
        public static readonly Color Accent = new Color(0.2f, 0.9f, 0.5f);
        public static readonly Color Warn = new Color(1f, 0.45f, 0.2f);
        public static readonly Color TextDim = new Color(0.6f, 0.75f, 0.65f);

        private static readonly Vector2 NormalReferenceResolution = new Vector2(1920f, 1080f);
        /// <summary>25% larger than the normal reference, for the Large UI setting.</summary>
        private static readonly Vector2 LargeReferenceResolution = new Vector2(1536f, 864f);

        /// <summary>
        /// Applies the "Large UI" accessibility setting to a canvas.
        ///
        /// Adjusts the scaler's reference resolution rather than writing
        /// <c>canvas.scaleFactor</c>. CanvasScaler in ScaleWithScreenSize mode
        /// recomputes and rewrites the canvas scale factor itself, so a value
        /// assigned directly survives only until the next resize — and it was
        /// also resolution-dependent: a hard 1.25 is not "25% larger than this
        /// screen's normal UI" anywhere but the reference resolution.
        /// </summary>
        public static void ApplyLargeUi(CanvasScaler scaler, bool large)
        {
            if (scaler == null) return;
            scaler.referenceResolution = large ? LargeReferenceResolution : NormalReferenceResolution;
        }

        public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = bg;
            return rt;
        }

        public static RectTransform SubPanel(Transform parent, string name, RectOffset padding, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var img = go.GetComponent<Image>();
            img.color = bg;
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string content,
            float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public static Button Button(Transform parent, string label, float height = 42f)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ButtonSfx));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(0, height);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.08f, 0.14f, 0.1f, 0.95f);
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.15f, 0.3f, 0.2f, 1f);
            colors.pressedColor = new Color(0.2f, 0.5f, 0.3f, 1f);
            btn.colors = colors;
            var t = Text(rt, "Label", label, 20, Color.white, TextAlignmentOptions.Center);
            return btn;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static void SetAnchored(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        public static VerticalLayoutGroup Vertical(Transform parent, float spacing, RectOffset padding)
        {
            var v = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childControlWidth = true;
            v.childControlHeight = true;
            return v;
        }

        public static ScrollRect Scroll(Transform parent, out RectTransform content)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.SetParent(parent, false);
            Stretch(scrollRt);

            var viewGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewRt = (RectTransform)viewGo.transform;
            viewRt.SetParent(scrollRt, false);
            Stretch(viewRt);
            viewGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            viewGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter));
            content = (RectTransform)contentGo.transform;
            content.SetParent(viewRt, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var fit = contentGo.GetComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewRt;
            scroll.content = content;
            scroll.horizontal = false;
            return scroll;
        }

        /// <summary>
        /// Anchors keyboard/gamepad navigation: selects the first
        /// interactable Selectable under root so arrow keys have somewhere
        /// to start from. Call after the panel is active.
        /// </summary>
        public static void SelectFirst(Transform root)
        {
            if (EventSystem.current == null) return;
            foreach (var s in root.GetComponentsInChildren<Selectable>(false))
            {
                if (!s.interactable || !s.IsActive()) continue;
                EventSystem.current.SetSelectedGameObject(s.gameObject);
                return;
            }
        }

        /// <summary>
        /// Drops any selection so a stale selected control can't take a
        /// submit input after its screen closes.
        /// </summary>
        public static void Deselect()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
