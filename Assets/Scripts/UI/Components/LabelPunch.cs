using TMPro;
using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Makes a text label announce itself: it slams in oversized, overshoots, then settles,
    /// optionally tinted and shivering. Used for the round outcome ("Blackjack!", "Bust"),
    /// where the difference between a line of text appearing and a line of text *landing*
    /// is most of the felt drama.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LabelPunch : MonoBehaviour
    {
        [Tooltip("Starting scale multiplier for the slam.")]
        [SerializeField] private float _startScale = 2.1f;
        [Tooltip("Seconds for the slam to resolve.")]
        [SerializeField] private float _duration = 0.5f;
        [Tooltip("How far past 1.0 the overshoot bounces before settling.")]
        [SerializeField] private float _overshoot = 0.16f;

        [Header("Shake")]
        [Tooltip("Peak positional shiver, in canvas units.")]
        [SerializeField] private float _shakeAmplitude = 9f;
        [SerializeField] private float _shakeFrequency = 46f;

        private TMP_Text _label;
        private RectTransform _rect;
        private Vector2 _homePosition;
        private Color _baseColor;
        private bool _homeCaptured;

        private float _t = 1f;
        private float _shakeScale;
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
        /// Home position is captured lazily rather than in Awake: SceneBootstrap positions
        /// these labels after construction, so reading it too early stores (0,0) and the
        /// label would snap to the middle of the canvas after its first punch.
        /// </summary>
        private void CaptureHome()
        {
            if (_homeCaptured || _rect == null) return;
            _homePosition = _rect.anchoredPosition;
            _homeCaptured = true;
        }

        /// <summary>Plays the slam. Pass a colour to tint the label as it lands.</summary>
        public void Play(Color? tint = null, float shake = 1f)
        {
            CaptureHome();
            _t = 0f;
            _shakeScale = Mathf.Max(0f, shake);
            _tinted = tint.HasValue;
            if (tint.HasValue) _tint = tint.Value;
        }

        /// <summary>Cancels any punch in progress and restores the resting look.</summary>
        public void ResetNow()
        {
            _t = 1f;
            if (_rect == null) return;
            CaptureHome();
            _rect.localScale = Vector3.one;
            _rect.anchoredPosition = _homePosition;
            if (_label != null) _label.color = _baseColor;
        }

        private void Update()
        {
            if (_rect == null || _t >= 1f) return;

            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / Mathf.Max(0.01f, _duration));

            float e = 1f - Mathf.Pow(1f - _t, 3f);

            // Ease down from the oversized start, then add a decaying sine bounce so the
            // label springs rather than merely shrinking.
            float baseScale = Mathf.LerpUnclamped(_startScale, 1f, e);
            float bounce = _overshoot * Mathf.Sin(_t * Mathf.PI * 2f) * (1f - _t);
            float s = baseScale + bounce;
            _rect.localScale = new Vector3(s, s, 1f);

            float decay = (1f - _t) * (1f - _t);
            float offset = Mathf.Sin(_t * _shakeFrequency) * _shakeAmplitude * decay * _shakeScale;
            _rect.anchoredPosition = _homePosition + new Vector2(offset, 0f);

            if (_label != null && _tinted)
                _label.color = Color.Lerp(_baseColor, _tint, decay);

            if (_t >= 1f) ResetNow();
        }
    }
}
