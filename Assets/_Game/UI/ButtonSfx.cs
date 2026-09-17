using Escape.Core;
using Escape.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Plays the library click when the sibling Button fires. Attached by
    /// UiBuilder.Button so every generated UI button gets audio for free.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonSfx : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            var lib = ClipLibrary.Get();
            if (lib == null || lib.uiClick == null) return;
            if (GameRoot.Instance == null) return;
            GameRoot.Instance.Services.Get<IAudioService>().Play2D(lib.uiClick, 0.6f);
        }
    }
}
