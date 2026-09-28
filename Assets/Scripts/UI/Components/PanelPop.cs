using BlackjackGame.UI.Theme;
using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Eases a modal panel in on enable — from a hair smaller, fading up — so it arrives
    /// as a deliberate piece of UI instead of blinking into existence. No bounce: a
    /// settings sheet is not a celebration.
    ///
    /// Purely cosmetic — it never changes activeSelf, so panel visibility logic stays
    /// wherever it already lives.
    /// </summary>
    public sealed class PanelPop : MonoBehaviour
    {
        [SerializeField] private float _startScale = 0.96f;
        [SerializeField] private float _duration = 0.3f;
        [SerializeField] private float _overshoot = 0f;
        [Tooltip("Optional group faded in alongside the scale.")]
        [SerializeField] private CanvasGroup _fadeGroup;

        private RectTransform _rect;
        private float _t = 1f;

        private void Awake() => _rect = (RectTransform)transform;

        private void OnEnable()
        {
            _t = 0f;
            Apply();
        }

        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / Mathf.Max(0.01f, MotionPrefs.Duration(_duration)));
            Apply();
        }

        private void Apply()
        {
            if (_rect == null) return;

            float e = 1f - Mathf.Pow(1f - _t, 3f);
            float bounce = _overshoot * Mathf.Sin(_t * Mathf.PI * 2f) * (1f - _t);
            float s = MotionPrefs.Reduced ? 1f : Mathf.LerpUnclamped(_startScale, 1f, e) + bounce;
            _rect.localScale = new Vector3(s, s, 1f);

            if (_fadeGroup != null) _fadeGroup.alpha = Mathf.Clamp01(_t * 1.6f);

            if (_t >= 1f)
            {
                _rect.localScale = Vector3.one;
                if (_fadeGroup != null) _fadeGroup.alpha = 1f;
            }
        }
    }
}
