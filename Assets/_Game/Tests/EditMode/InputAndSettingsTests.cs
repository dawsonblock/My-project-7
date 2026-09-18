using Escape.Core;
using NUnit.Framework;
using UnityEngine;

namespace Escape.Tests.EditMode
{
    /// <summary>
    /// Input-gate modal stack and settings cache semantics.
    /// </summary>
    public class InputAndSettingsTests
    {
        private sealed class FakeModal : ICancelableUi
        {
            public int Cancels;
            public void Cancel() => Cancels++;
        }

        [Test]
        public void InputGate_CancelTop_Routes_To_Topmost()
        {
            var gate = new InputGate();
            var bottom = new FakeModal();
            var top = new FakeModal();
            gate.PushUi(bottom);
            gate.PushUi(top);

            gate.CancelTop();
            Assert.AreEqual(0, bottom.Cancels);
            Assert.AreEqual(1, top.Cancels);

            gate.PopUi(top);
            gate.CancelTop();
            Assert.AreEqual(1, bottom.Cancels);
        }

        [Test]
        public void InputGate_CancelTop_Ignores_NonCancelable()
        {
            var gate = new InputGate();
            var plain = new object();
            var cancelable = new FakeModal();
            gate.PushUi(cancelable);
            gate.PushUi(plain); // non-cancelable on top

            gate.CancelTop();
            Assert.AreEqual(0, cancelable.Cancels, "Non-cancelable top must swallow cancel");
        }

        [Test]
        public void InputGate_UiOpen_Follows_Stack()
        {
            var gate = new InputGate();
            var a = new object(); var b = new object();
            Assert.IsFalse(gate.UiOpen);
            gate.PushUi(a); gate.PushUi(b);
            Assert.IsTrue(gate.UiOpen);
            Assert.AreSame(b, gate.TopOwner);
            gate.PopUi(b);
            Assert.IsTrue(gate.UiOpen);
            gate.PopUi(a);
            Assert.IsFalse(gate.UiOpen);
            Assert.IsNull(gate.TopOwner);
        }

        [Test]
        public void Settings_Cache_ServesReads_WithoutPlayerPrefs()
        {
            var s = new SettingsService();
            s.MouseSensitivity = 2.5f;
            // Reads must come from the in-memory model — wiping PlayerPrefs
            // must not change runtime behavior until persisted+reloaded.
            PlayerPrefs.DeleteKey("set_mouse_sens");
            Assert.AreEqual(2.5f, s.MouseSensitivity);
        }

        [Test]
        public void Settings_Persist_Writes_OnlyOnFlush()
        {
            var s = new SettingsService();
            s.ToggleCrouch = true;
            s.MasterVolume = 0.4f;
            s.Persist();
            Assert.AreEqual(1, PlayerPrefs.GetInt("set_toggle_crouch"));
            Assert.AreEqual(0.4f, PlayerPrefs.GetFloat("set_vol_master"), 0.001f);
            s.ToggleCrouch = false;
            s.MasterVolume = 1f;
            s.Persist();
        }
    }
}
