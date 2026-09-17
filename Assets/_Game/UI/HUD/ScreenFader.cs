using System.Collections;
using Escape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.UI
{
    /// <summary>
    /// Full-screen fade used by scene transitions and the caught sequence.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour, IScreenFader
    {
        [SerializeField] private Image image;
        [SerializeField] private float duration = 0.5f;

        public static ScreenFader Create(Transform canvasRoot)
        {
            var go = new GameObject("ScreenFader", typeof(RectTransform), typeof(Image), typeof(ScreenFader));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRoot, false);
            UiBuilder.Stretch(rt);
            var fader = go.GetComponent<ScreenFader>();
            fader.image = go.GetComponent<Image>();
            fader.image.color = new Color(0, 0, 0, 1f);
            fader.image.raycastTarget = false;
            return fader;
        }

        private void Start() => StartCoroutine(FadeIn());

        public IEnumerator FadeOut()
        {
            image.raycastTarget = true;
            yield return Fade(1f);
        }

        public IEnumerator FadeIn()
        {
            yield return Fade(0f);
            image.raycastTarget = false;
        }

        private IEnumerator Fade(float target)
        {
            float start = image.color.a;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                var c = image.color;
                c.a = Mathf.Lerp(start, target, t);
                image.color = c;
                yield return null;
            }
        }
    }
}
