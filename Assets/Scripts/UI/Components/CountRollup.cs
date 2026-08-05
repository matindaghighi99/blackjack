using System.Globalization;
using TMPro;
using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Rolls a number up (or down) to its new value instead of snapping, and flashes the
    /// label while it moves. A balance that visibly climbs after a win is the single
    /// cheapest piece of reward feedback in a casino game.
    ///
    /// Drive it with <see cref="SetValue"/>; it owns the label's text from then on.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class CountRollup : MonoBehaviour
    {
        [Tooltip("Standard or custom numeric format applied to the displayed value.")]
        [SerializeField] private string _format = "N0";

        [Tooltip("Seconds the roll takes, regardless of how large the jump is.")]
        [SerializeField] private float _duration = 0.55f;

        [Header("Flash")]
        [Tooltip("Colour mixed in while the value is climbing. Alpha is ignored.")]
        [SerializeField] private Color _gainFlash = new Color(0.45f, 1f, 0.55f);
        [Tooltip("Colour mixed in while the value is falling.")]
        [SerializeField] private Color _lossFlash = new Color(1f, 0.42f, 0.42f);
        [SerializeField] private float _punchScale = 0.18f;

        private TMP_Text _label;
        private Color _baseColor;

        private double _shown;
        private double _from;
        private double _target;
        private float _t = 1f;
        private bool _rising;
        private bool _initialised;

        /// <summary>
        /// True while the number is still travelling toward its target. Tests (and any
        /// code that needs to read the settled figure) can wait on this rather than
        /// racing the animation.
        /// </summary>
        public bool IsRolling => _t < 1f;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _baseColor = _label.color;
        }

        /// <summary>
        /// Sets the value. The first call snaps (no animation on scene load); later calls roll.
        /// </summary>
        public void SetValue(double value)
        {
            if (!_initialised)
            {
                _initialised = true;
                _shown = _from = _target = value;
                _t = 1f;
                Render();
                return;
            }

            if (System.Math.Abs(value - _target) < 0.0001) return;

            _from = _shown;
            _target = value;
            _rising = _target > _from;
            _t = 0f;
        }

        /// <summary>Forces the display to a value with no animation.</summary>
        public void SnapTo(double value)
        {
            _initialised = true;
            _shown = _from = _target = value;
            _t = 1f;
            if (_label != null)
            {
                _label.color = _baseColor;
                _label.transform.localScale = Vector3.one;
            }
            Render();
        }

        private void Update()
        {
            if (_label == null || _t >= 1f) return;

            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / Mathf.Max(0.01f, _duration));

            // Ease-out so the number decelerates into its final value.
            float e = 1f - Mathf.Pow(1f - _t, 3f);
            _shown = _from + (_target - _from) * e;
            Render();

            // Flash and swell hardest at the start, settling back to normal.
            float intensity = 1f - _t;
            _label.color = Color.Lerp(_baseColor, _rising ? _gainFlash : _lossFlash, intensity * 0.85f);
            float s = 1f + _punchScale * intensity;
            _label.transform.localScale = new Vector3(s, s, 1f);

            if (_t >= 1f)
            {
                _shown = _target;
                Render();
                _label.color = _baseColor;
                _label.transform.localScale = Vector3.one;
            }
        }

        private void Render()
        {
            if (_label == null) return;
            _label.text = System.Math.Round(_shown).ToString(_format, CultureInfo.CurrentCulture);
        }
    }
}
