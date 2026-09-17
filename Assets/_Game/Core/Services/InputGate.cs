using System;
using System.Collections.Generic;

namespace Escape.Core
{
    /// <summary>
    /// Implemented by UI surfaces that can be dismissed with the global
    /// back key (Escape / gamepad east). Only the topmost modal receives it.
    /// </summary>
    public interface ICancelableUi
    {
        void Cancel();
    }

    /// <summary>
    /// Modal ownership. A push-ordered stack of UI owners: gameplay input
    /// is suppressed while any owner is present, and Cancel/Pause-back is
    /// routed only to the topmost owner — so a settings panel over the
    /// pause menu can't both close at once.
    /// </summary>
    public interface IInputGate
    {
        bool UiOpen { get; }
        /// <summary>Topmost pushed owner, or null when gameplay owns input.</summary>
        object TopOwner { get; }
        event Action<bool> OnUiModeChanged;
        void PushUi(object owner);
        void PopUi(object owner);
        /// <summary>Deliver a back/cancel press to the top modal, if cancelable.</summary>
        void CancelTop();
    }

    public sealed class InputGate : IInputGate
    {
        private readonly List<object> _owners = new List<object>();
        public bool UiOpen => _owners.Count > 0;
        public object TopOwner => _owners.Count > 0 ? _owners[_owners.Count - 1] : null;
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

        public void CancelTop()
        {
            if (TopOwner is ICancelableUi cancelable) cancelable.Cancel();
        }
    }
}
