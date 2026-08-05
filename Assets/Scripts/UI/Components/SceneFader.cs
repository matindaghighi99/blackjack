using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// A full-screen black veil that fades the scene in on load and out again before a
    /// scene change, replacing the hard cut between Menu, Game and Store.
    ///
    /// One instance lives at the top of each scene's canvas. Callers go through the
    /// static <see cref="TransitionTo"/>, which quietly degrades to a plain
    /// <c>SceneManager.LoadScene</c> when no fader is present (tests, batch mode).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class SceneFader : MonoBehaviour
    {
        [Tooltip("Seconds for the fade-in when a scene starts.")]
        [SerializeField] private float _fadeInDuration = 0.28f;
        [Tooltip("Seconds for the fade-out before the next scene loads.")]
        [SerializeField] private float _fadeOutDuration = 0.22f;

        private static SceneFader _active;

        private Image _veil;
        private bool _leaving;

        private void Awake()
        {
            _active = this;
            _veil = GetComponent<Image>();

            // Start opaque and let Start() fade in, so the first visible frame of a
            // scene is never a half-built layout.
            _veil.color = new Color(0f, 0f, 0f, 1f);
            _veil.raycastTarget = true;
        }

        private void Start() => StartCoroutine(FadeIn());

        private void OnDestroy()
        {
            if (_active == this) _active = null;
        }

        /// <summary>
        /// Fades to black, then loads <paramref name="sceneName"/>. Falls back to an
        /// immediate load when no fader exists in the scene.
        /// </summary>
        public static void TransitionTo(string sceneName)
        {
            if (_active != null && _active.isActiveAndEnabled)
                _active.StartCoroutine(_active.FadeOutAndLoad(sceneName));
            else
                SceneManager.LoadScene(sceneName);
        }

        private IEnumerator FadeIn()
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _fadeInDuration);
                SetAlpha(1f - Mathf.Clamp01(t));
                yield return null;
            }

            SetAlpha(0f);
            // Fully faded in: stop eating clicks.
            _veil.raycastTarget = false;
        }

        private IEnumerator FadeOutAndLoad(string sceneName)
        {
            if (_leaving) yield break; // double-taps must not queue two loads
            _leaving = true;

            _veil.raycastTarget = true; // freeze input while the lights go down
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _fadeOutDuration);
                SetAlpha(Mathf.Clamp01(t));
                yield return null;
            }

            SceneManager.LoadScene(sceneName);
        }

        private void SetAlpha(float a)
        {
            if (_veil != null) _veil.color = new Color(0f, 0f, 0f, a);
        }
    }
}
