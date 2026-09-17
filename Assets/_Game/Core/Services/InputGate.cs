using System;
using System.Collections.Generic;

namespace Escape.Core
{
    /// <summary>
    /// Tracks whether a UI surface (terminal, board, pause, document) owns
    /// input. Gameplay systems check this instead of reaching into UI code.
    /// </summary>
    public interface IInputGate
    {
        bool UiOpen { get; }
        event Action<bool> OnUiModeChanged;
        void PushUi(object owner);
        void PopUi(object owner);
    }

    public sealed class InputGate : IInputGate
    {
        private readonly List<object> _owners = new List<object>();
        public bool UiOpen => _owners.Count > 0;
        public event Action<bool> OnUiModeChanged;

        public void PushUi(object owner)
        {
            if (owner == null || _owners.Contains(owner)) return;
            bool was = UiOpen;
            _owners.Add(owner);
            if (!was) OnUiModeChanged?.Invoke(true);
        }

        public void PopUi(object owner)
        {
            bool was = UiOpen;
            _owners.Remove(owner);
            if (was && !UiOpen) OnUiModeChanged?.Invoke(false);
        }
    }
}
