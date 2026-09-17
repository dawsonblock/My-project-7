using System.Collections.Generic;
using System.Text;
using Escape.Core;
using Escape.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Terminal screen: code entry → command list → output log. Every
    /// command checks requirements and dispatches through the command
    /// dispatcher; this view never mutates game state.
    /// </summary>
    public sealed class TerminalUI : MonoBehaviour, ITerminalUI
    {
        private GameObject _root;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _output;
        private RectTransform _buttonList;
        private RectTransform _codeRow;
        private TMP_InputField _codeField;
        private TextMeshProUGUI _codeError;

        private GameServices _services;
        private TerminalDefinition _terminal;
        private bool _unlocked;
        private readonly StringBuilder _log = new StringBuilder();
        private readonly List<GameObject> _buttons = new List<GameObject>();

        public bool IsOpen => _root != null && _root.activeSelf;

        public static TerminalUI Create(Transform canvasRoot)
        {
            var go = new GameObject("TerminalUI", typeof(RectTransform), typeof(TerminalUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            UiBuilder.Stretch(rt);
            var ui = go.GetComponent<TerminalUI>();
            ui.Build(rt);
            go.SetActive(false);
            ui._root = go;
            return ui;
        }

        private void Build(RectTransform root)
        {
            var outer = UiBuilder.Panel(root, "Bg", new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.9f), UiBuilder.PanelBg);
            var layout = UiBuilder.Vertical(outer, 8, new RectOffset(24, 24, 20, 20));

            _title = UiBuilder.Text(outer, "Title", "TERMINAL", 26, UiBuilder.Accent);
            _title.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;

            var codeGo = new GameObject("CodeRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _codeRow = (RectTransform)codeGo.transform;
            _codeRow.SetParent(outer, false);
            var hl = codeGo.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 10;
            hl.childForceExpandHeight = false;
            _codeRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;

            var prompt = UiBuilder.Text(_codeRow, "Prompt", "ACCESS CODE:", 18, UiBuilder.TextDim);
            prompt.gameObject.AddComponent<LayoutElement>().preferredWidth = 160;

            var fieldGo = new GameObject("CodeField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            fieldGo.transform.SetParent(_codeRow, false);
            fieldGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            fieldGo.AddComponent<LayoutElement>().preferredWidth = 220;
            var fieldText = UiBuilder.Text(fieldGo.transform, "Text", "", 20, Color.white);
            fieldText.textWrappingMode = TextWrappingModes.NoWrap;
            var fieldArea = new GameObject("Area", typeof(RectTransform), typeof(RectMask2D));
            var areaRt = (RectTransform)fieldArea.transform;
            areaRt.SetParent(fieldGo.transform, false);
            UiBuilder.Stretch(areaRt);
            fieldText.transform.SetParent(areaRt, false);
            _codeField = fieldGo.GetComponent<TMP_InputField>();
            _codeField.textViewport = areaRt;
            _codeField.textComponent = fieldText;
            _codeField.characterValidation = TMP_InputField.CharacterValidation.Integer;

            var submit = UiBuilder.Button(_codeRow, "ENTER");
            submit.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;
            submit.onClick.AddListener(SubmitCode);

            _codeError = UiBuilder.Text(outer, "CodeError", "", 14, UiBuilder.Warn);
            _codeError.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            var scrollHost = new GameObject("CommandHost", typeof(RectTransform));
            var shRt = (RectTransform)scrollHost.transform;
            shRt.SetParent(outer, false);
            shRt.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            var scroll = UiBuilder.Scroll(shRt, out _buttonList);
            UiBuilder.Vertical(_buttonList, 6, new RectOffset(0, 0, 4, 4));

            var outGo = new GameObject("Output", typeof(RectTransform), typeof(Image));
            var outRt = (RectTransform)outGo.transform;
            outRt.SetParent(outer, false);
            outGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            outGo.AddComponent<LayoutElement>().preferredHeight = 150;
            _output = UiBuilder.Text(outRt, "Text", "", 15, UiBuilder.Accent);
            ((RectTransform)_output.transform).offsetMin = new Vector2(12, 8);
            ((RectTransform)_output.transform).offsetMax = new Vector2(-12, -8);
            _output.alignment = TextAlignmentOptions.TopLeft;

            var close = UiBuilder.Button(outer, "DISCONNECT  [Esc]");
            close.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            close.onClick.AddListener(Close);
        }

        private void Start()
        {
            _services = GameRoot.Instance.Services;
        }

        public void Open(TerminalDefinition terminal)
        {
            _terminal = terminal;
            _services ??= GameRoot.Instance.Services;
            var state = _services.Get<IGameStateService>().State;
            _unlocked = string.IsNullOrEmpty(terminal.UnlockCode) ||
                        state.UnlockedTerminals.Contains(terminal.Id);
            _title.text = terminal.DisplayName;
            _log.Clear();
            AppendLine(_unlocked
                ? "SESSION OPEN — commands available"
                : "LOCKED — enter access code");
            Rebuild();
            _root.SetActive(true);
            Sfx(Data.ClipLibrary.Get()?.uiHover);
            _services.Get<IInputGate>().PushUi(this);
        }

        public void Close()
        {
            _root.SetActive(false);
            Sfx(Data.ClipLibrary.Get()?.uiBack);
            _services.Get<IInputGate>().PopUi(this);
        }

        private void Sfx(AudioClip clip, float volume = 0.7f)
        {
            if (clip == null || _services == null) return;
            _services.Get<IAudioService>().Play2D(clip, volume);
        }

        private void SubmitCode()
        {
            if (_terminal == null) return;
            var lib = Data.ClipLibrary.Get();
            if (_codeField.text == _terminal.UnlockCode)
            {
                _services.Get<IGameCommandDispatcher>().Dispatch(new UnlockTerminalCommand(_terminal.Id));
                _unlocked = true;
                _codeError.text = "";
                AppendLine("ACCESS GRANTED");
                Sfx(lib?.uiConfirm);
                Rebuild();
            }
            else
            {
                _codeError.text = "INVALID CODE";
                AppendLine("ACCESS DENIED");
                Sfx(lib?.uiError);
            }
            _codeField.text = "";
        }

        private void Rebuild()
        {
            foreach (var b in _buttons) Destroy(b);
            _buttons.Clear();
            _codeRow.gameObject.SetActive(!_unlocked);
            _codeError.gameObject.SetActive(!_unlocked);
            if (!_unlocked) return;

            var state = _services.Get<IGameStateService>().State;
            foreach (var cmd in _terminal.Commands)
            {
                bool met = RequirementsMet(cmd, state);
                if (cmd.HideUntilUnlocked && !met) continue;
                var btn = UiBuilder.Button(_buttonList, cmd.Label);
                btn.interactable = met;
                var captured = cmd;
                btn.onClick.AddListener(() => RunCommand(captured));
                _buttons.Add(btn.gameObject);
            }
        }

        private bool RequirementsMet(TerminalCommandDefinition cmd, GameState state)
        {
            foreach (var e in cmd.RequiredEvidence)
                if (e != null && !state.CollectedEvidence.Contains(e.Id)) return false;
            foreach (var o in cmd.RequiredObjectives)
                if (o != null && !state.CompletedObjectives.Contains(o.Id)) return false;
            foreach (var i in cmd.RequiredInsights)
                if (i != null && !state.GainedInsights.Contains(i.Id)) return false;
            return true;
        }

        private void RunCommand(TerminalCommandDefinition cmd)
        {
            var dispatcher = _services.Get<IGameCommandDispatcher>();
            dispatcher.Dispatch(new RecordTerminalUseCommand(_terminal.Id, cmd.Command));

            switch (cmd.Action)
            {
                case TerminalActionType.CollectEvidence:
                    dispatcher.Dispatch(new CollectEvidenceCommand(cmd.TargetId, _terminal.Id));
                    break;
                case TerminalActionType.CompleteObjective:
                    dispatcher.Dispatch(new CompleteObjectiveCommand(cmd.TargetId, _terminal.Id));
                    break;
                case TerminalActionType.UnlockDoor:
                    dispatcher.Dispatch(new UnlockDoorCommand(cmd.TargetId, _terminal.Id));
                    break;
                case TerminalActionType.DisableCamera:
                    dispatcher.Dispatch(new DisableCameraCommand(cmd.TargetId, _terminal.Id));
                    break;
                case TerminalActionType.SetLockdown:
                    dispatcher.Dispatch(new SetLockdownCommand(true, _terminal.Id));
                    break;
                case TerminalActionType.StartBroadcast:
                    dispatcher.Dispatch(new StartBroadcastCommand(_terminal.Id));
                    break;
                case TerminalActionType.ListStatus:
                    AppendLine(StatusText());
                    return;
                case TerminalActionType.ListDoors:
                    AppendLine(DoorsText());
                    return;
                case TerminalActionType.ListCameras:
                    AppendLine(CamerasText());
                    return;
            }
            AppendLine("> " + cmd.Command + "\n" + (string.IsNullOrEmpty(cmd.SuccessText) ? "OK" : cmd.SuccessText));
            if (cmd.CompletesObjective != null)
                dispatcher.Dispatch(new CompleteObjectiveCommand(cmd.CompletesObjective.Id, _terminal.Id));
            Rebuild();
        }

        private string StatusText()
        {
            var s = _services.Get<IGameStateService>().State;
            var sb = new StringBuilder("> STATUS\n");
            sb.AppendLine($"DOORS UNLOCKED: {s.UnlockedDoors.Count}");
            sb.AppendLine($"CAMERAS DOWN: {s.DisabledCameras.Count}");
            sb.AppendLine($"ALERT: {(s.Alert ? "ACTIVE" : "NOMINAL")}");
            sb.AppendLine($"LOCKDOWN: {(s.Lockdown ? "YES" : "NO")}");
            return sb.ToString();
        }

        private string DoorsText()
        {
            var s = _services.Get<IGameStateService>().State;
            var sb = new StringBuilder("> DOORS\n");
            foreach (var d in _terminal.AssociatedDoorIds)
                sb.AppendLine($"{d} — {(s.UnlockedDoors.Contains(d) ? "UNLOCKED" : "LOCKED")}");
            return sb.ToString();
        }

        private string CamerasText()
        {
            var s = _services.Get<IGameStateService>().State;
            var sb = new StringBuilder("> CAMERAS\n");
            foreach (var c in _terminal.AssociatedCameraIds)
                sb.AppendLine($"{c} — {(s.DisabledCameras.Contains(c) ? "OFFLINE" : "ACTIVE")}");
            return sb.ToString();
        }

        private void AppendLine(string text)
        {
            _log.AppendLine(text);
            if (_log.Length > 4000) _log.Remove(0, _log.Length - 4000);
            _output.text = _log.ToString();
        }

        private void Update()
        {
            if (IsOpen && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }
    }
}
