using BlackjackGame.UI.Theme;
using TMPro;
using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Makes a status line arrive instead of blinking in: it rises a few units, fades up,
    /// settles from a hair larger than its resting size, and briefly carries a tint.
    ///
    /// Deliberately understated — a message changing should be noticed, not shouted. The
    /// API (and its <c>shake</c> parameter, now read as "emphasis") is kept so existing
    /// callers work unchanged.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LabelPunch : MonoBehaviour
    {
        [Tooltip("Starting scale multiplier.")]
        [SerializeField] private float _startScale = 1.06f;
        [Tooltip("Seconds for the reveal to resolve.")]
        [SerializeField] private float _duration = 0.45f;
        [Tooltip("How far the label rises into place, in canvas units.")]
        [SerializeField] private float _rise = 10f;
        [Tooltip("Seconds the tint holds before easing back to the label's own colour.")]
        [SerializeField] private float _tintHold = 1.2f;

        private TMP_Text _label;
        private RectTransform _rect;
        private Vector2 _homePosition;
        private Color _baseColor;
        private bool _homeCaptured;

        private float _t = 1f;
        private float _emphasis = 1f;
        private Color _tint;
        private bool _tinted;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _rect = (RectTransform)transform;
            _baseColor = _label.color;
        }

        private void OnEnable() => CaptureHome();

        /// <summary>
        /// Home position is captured lazily rather than in Awake: layout code positions
        /// these labels after construction, so reading it too early stores the wrong spot.
        /// </summary>
        private void CaptureHome()
        {
            if (_homeCaptured || _rect == null) return;
            _homePosition = _rect.anchoredPosition;
            _homeCaptured = true;
        }

        /// <summary>Re-reads the resting position after a layout change.</summary>
        public void Rehome()
        {
            _homeCaptured = false;
            CaptureHome();
        }

        /// <summary>Plays the reveal. Pass a colour to tint the label as it lands.</summary>
        public void Play(Color? tint = null, float shake = 1f)
        {
            CaptureHome();
            _t = 0f;
            _emphasis = Mathf.Clamp(shake, 0.35f, 1.5f);
            _tinted = tint.HasValue;
            if (tint.HasValue) _tint = tint.Value;
        }

        /// <summary>Cancels any reveal in progress and restores the resting look.</summary>
        public void ResetNow()
        {
            _t = 1f;
            if (_rect == null) return;
            CaptureHome();
            _rect.localScale = Vector3.one;
            _rect.anchoredPosition = _homePosition;
            if (_label != null)
            {
                _label.color = _baseColor;
                _label.alpha = 1f;
            }
        }

        private void Update()
        {
            if (_rect == null || _t >= 1f) return;

            float total = _duration + _tintHold;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / Mathf.Max(0.01f, MotionPrefs.Duration(total)));
            float k = Mathf.Clamp01(_t * total / _duration);
            float e = Ease.OutCubic(k);

            float s = Mathf.LerpUnclamped(1f + (_startScale - 1f) * _emphasis, 1f, e);
            if (MotionPrefs.Reduced) s = 1f;
            _rect.localScale = new Vector3(s, s, 1f);
            _rect.anchoredPosition = _homePosition - new Vector2(0f, MotionPrefs.Amount(_rise) * (1f - e));

            if (_label != null)
            {
                if (_tinted)
                {
                    float hold = Mathf.Clamp01((_t * total - _duration) / Mathf.Max(0.01f, _tintHold));
                    _label.color = Color.Lerp(_tint, _baseColor, Ease.InOutCubic(hold));
                }
                // After the colour, which carries its own alpha.
                _label.alpha = Mathf.Clamp01(k * 1.8f);
            }

            if (_t >= 1f) ResetNow();
        }
    }
}
