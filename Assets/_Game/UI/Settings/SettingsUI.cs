using Escape.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Settings panel — accessibility options from day one. Works both from
    /// pause menu and main menu.
    /// </summary>
    public sealed class SettingsUI : MonoBehaviour, ICancelableUi
    {
        private GameObject _root;
        private ISettingsService _settings;
        private GameServices _services;
        private Transform _returnFocus;

        public bool IsOpen => _root != null && _root.activeSelf;

        public static SettingsUI Create(Transform canvasRoot)
        {
            var go = new GameObject("SettingsUI", typeof(RectTransform), typeof(SettingsUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            UiBuilder.Stretch(rt);
            var ui = go.GetComponent<SettingsUI>();
            ui.Build(rt);
            ui._root = go;
            go.SetActive(false);
            return ui;
        }

        private void Build(RectTransform root)
        {
            var panel = UiBuilder.Panel(root, "Panel",
                new Vector2(0.28f, 0.1f), new Vector2(0.72f, 0.92f), UiBuilder.PanelBg);
            UiBuilder.Scroll(panel, out var content);
            UiBuilder.Vertical(content, 8, new RectOffset(30, 30, 24, 24));

            var t = UiBuilder.Text(content, "T", "SETTINGS", 26, Color.white);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
        }

        private bool _populated;

        private void Start() => TryBind();

        // Bind + populate on enable — during Create() the hierarchy isn't
        // built yet, so populate waits for the first real open.
        private void OnEnable()
        {
            TryBind();
            if (_services != null && _root != null && !_populated)
            {
                Populate();
                _populated = true;
            }
        }

        private void TryBind()
        {
            if (_services == null && GameRoot.Instance != null)
            {
                _services = GameRoot.Instance.Services;
                _settings = _services.Get<ISettingsService>();
            }
        }

        private void Populate()
        {
            var content = _root.GetComponentInChildren<ScrollRect>().content;
            Slider(content, "Mouse Sensitivity", 0.2f, 3f, () => _settings.MouseSensitivity, v => _settings.MouseSensitivity = v);
            Slider(content, "Controller Sensitivity", 0.2f, 3f, () => _settings.ControllerSensitivity, v => _settings.ControllerSensitivity = v);
            Slider(content, "Field of View", 50f, 100f, () => _settings.Fov, v =>
            {
                _settings.Fov = v;
                if (Camera.main != null) Camera.main.fieldOfView = v;
            });
            Toggle(content, "Invert Y", () => _settings.InvertY, v => _settings.InvertY = v);
            Toggle(content, "Head Bob", () => _settings.HeadBob, v => _settings.HeadBob = v);
            Toggle(content, "Reduce Motion", () => _settings.ReduceMotion, v => _settings.ReduceMotion = v);
            Toggle(content, "Toggle Sprint", () => _settings.ToggleSprint, v => _settings.ToggleSprint = v);
            Toggle(content, "Toggle Crouch", () => _settings.ToggleCrouch, v => _settings.ToggleCrouch = v);
            Toggle(content, "Toggle Flashlight", () => _settings.ToggleFlashlight, v => _settings.ToggleFlashlight = v);
            Toggle(content, "Large UI", () => _settings.LargeUI, v => _settings.LargeUI = v);
            Slider(content, "Master Volume", 0f, 1f, () => _settings.MasterVolume, v => _settings.MasterVolume = v);

            // Deliberately not exposed yet, because nothing reads them:
            // Subtitles, High Contrast and the per-bus volume sliders. The
            // model still carries them (persisted, and the audio pass will
            // consume Music/Ambience/SFX once AudioService has mixer
            // snapshots) — showing a control that does nothing is worse than
            // showing none.

            var back = UiBuilder.Button(content, "BACK", 44);
            back.onClick.AddListener(Close);
        }

        private void Slider(Transform parent, string label, float min, float max,
            System.Func<float> get, System.Action<float> set)
        {
            var row = new GameObject("Row_" + label, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = 36;
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 12;
            var l = UiBuilder.Text(row.transform, "L", label, 16, UiBuilder.TextDim);
            l.gameObject.AddComponent<LayoutElement>().preferredWidth = 180;
            var sGo = new GameObject("S", typeof(RectTransform), typeof(Slider));
            sGo.transform.SetParent(row.transform, false);
            sGo.AddComponent<LayoutElement>().flexibleWidth = 1;
            var s = sGo.GetComponent<Slider>();
            s.minValue = min;
            s.maxValue = max;
            s.value = get();

            // Conventional slider structure: a background, a fill area whose
            // fill the Slider drives by anchors, and a handle. Without
            // fillRect/handleRect the widget changes value but shows nothing.
            var bg = sGo.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.12f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sGo.transform, false);
            var fa = (RectTransform)fillArea.transform;
            fa.anchorMin = new Vector2(0f, 0.35f);
            fa.anchorMax = new Vector2(1f, 0.65f);
            fa.offsetMin = new Vector2(6f, 0f);
            fa.offsetMax = new Vector2(-6f, 0f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            var fr = (RectTransform)fillGo.transform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.pivot = new Vector2(0f, 0.5f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            fillGo.GetComponent<Image>().color = UiBuilder.Accent;
            s.fillRect = fr;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sGo.transform, false);
            var ha = (RectTransform)handleArea.transform;
            ha.anchorMin = Vector2.zero;
            ha.anchorMax = Vector2.one;
            ha.offsetMin = new Vector2(6f, 0f);
            ha.offsetMax = new Vector2(-6f, 0f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleArea.transform, false);
            var hr = (RectTransform)handleGo.transform;
            hr.anchorMin = new Vector2(0f, 0f);
            hr.anchorMax = new Vector2(0f, 1f);
            hr.sizeDelta = new Vector2(12f, 0f);
            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = Color.white;
            s.handleRect = hr;
            s.targetGraphic = handleImg;

            s.onValueChanged.AddListener(v => set(v));
        }

        private void Toggle(Transform parent, string label, System.Func<bool> get, System.Action<bool> set)
        {
            var row = new GameObject("Row_" + label, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = 36;
            var l = UiBuilder.Text(row.transform, "L", label, 16, UiBuilder.TextDim);
            l.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var tGo = new GameObject("T", typeof(RectTransform), typeof(Image), typeof(Toggle));
            tGo.transform.SetParent(row.transform, false);
            tGo.AddComponent<LayoutElement>().preferredWidth = 30;
            var t = tGo.GetComponent<Toggle>();
            var img = tGo.GetComponent<Image>();
            img.color = get() ? UiBuilder.Accent : new Color(0.1f, 0.1f, 0.1f);
            t.isOn = get();
            t.onValueChanged.AddListener(v =>
            {
                set(v);
                img.color = v ? UiBuilder.Accent : new Color(0.1f, 0.1f, 0.1f);
            });
        }

        /// <summary>
        /// Opens the panel, remembering where focus should land when it closes.
        ///
        /// Generic on purpose. This used to take a PauseMenuUI specifically,
        /// which meant the main menu — the other surface that opens settings —
        /// closed back to no selection at all, stranding controller and
        /// keyboard users. Any caller can now name its own focus anchor.
        /// </summary>
        public void OpenFrom(Transform returnFocus)
        {
            _returnFocus = returnFocus;
            Open();
        }

        public void Open()
        {
            TryBind();
            if (_services == null) return;
            _root.SetActive(true);
            _services.Get<IInputGate>().PushUi(this);
            UiBuilder.SelectFirst(_root.transform);
        }

        private void Update()
        {
            // Escape backs out even in scenes with no PlayerInputReader
            // (main menu). Close() is idempotent if the input gate already
            // routed the same key press through CancelTop.
            if (IsOpen && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        public void Cancel() => Close();

        public void Close()
        {
            _root.SetActive(false);
            _settings?.Persist(); // flush deferred writes when the menu closes
            if (_services != null) _services.Get<IInputGate>().PopUi(this);

            // Hand focus back to whatever opened us, so a controller/keyboard
            // user is not dropped back into the menu with nothing selected.
            // A target that has since been hidden is no anchor at all — fall
            // back to clearing the selection rather than selecting something
            // the player cannot see.
            if (_returnFocus != null && _returnFocus.gameObject.activeInHierarchy)
                UiBuilder.SelectFirst(_returnFocus);
            else
                UiBuilder.Deselect();
        }
    }
}
