using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Shakes an entire canvas layer for impact moments — a blackjack, a bust, a big win.
    /// Attach to a RectTransform that parents the screen content (not the Canvas itself,
    /// which a ScreenSpaceOverlay canvas will keep re-centring).
    ///
    /// Uses summed sine waves at incommensurate frequencies rather than Random: the motion
    /// is smooth and repeatable, where per-frame randomness reads as jitter and can spike.
    /// </summary>
    public sealed class ScreenShake : MonoBehaviour
    {
        [SerializeField] private float _duration = 0.4f;
        [SerializeField] private float _amplitude = 18f;
        [SerializeField] private float _rotationAmplitude = 1.1f;

        private RectTransform _rect;
        private Vector2 _home;
        private float _t = 1f;
        private float _scale;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _home = _rect.anchoredPosition;
        }

        /// <summary>Starts a shake. <paramref name="strength"/> scales the configured amplitude.</summary>
        public void Shake(float strength = 1f)
        {
            _scale = Mathf.Max(0f, strength);
            _t = 0f;
        }

        private void Update()
        {
            if (_rect == null || _t >= 1f) return;

            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / Mathf.Max(0.01f, _duration));

            // Quadratic decay: hits hard, dies away fast, never lingers.
            float decay = (1f - _t) * (1f - _t) * _scale;
            float time = _t * _duration;

            float x = (Mathf.Sin(time * 47f) + Mathf.Sin(time * 31f) * 0.6f) * _amplitude * decay;
            float y = (Mathf.Sin(time * 39f) + Mathf.Sin(time * 23f) * 0.6f) * _amplitude * 0.7f * decay;
            float rot = Mathf.Sin(time * 43f) * _rotationAmplitude * decay;

            _rect.anchoredPosition = _home + new Vector2(x, y);
            _rect.localRotation = Quaternion.Euler(0f, 0f, rot);

            if (_t >= 1f)
            {
                _rect.anchoredPosition = _home;
                _rect.localRotation = Quaternion.identity;
            }
        }
    }
}
