using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Tactile feedback for a UI button: it grows slightly on hover, snaps down when
    /// pressed, and springs back on release. Unity's built-in ColorBlock only tints —
    /// scale is what actually reads as "pressed" on a phone, where there is no cursor
    /// to show hover state.
    ///
    /// Also drives an optional glow graphic that breathes while the button is
    /// interactable, so the legal moves in a round advertise themselves.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonJuice : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Scale response")]
        [SerializeField] private float _hoverScale = 1.04f;
        [SerializeField] private float _pressScale = 0.93f;
        [Tooltip("How fast the button chases its target scale. Higher is snappier.")]
        [SerializeField] private float _responseSpeed = 14f;

        [Header("Enabled glow")]
        [Tooltip("Optional graphic behind the button that pulses while it is usable.")]
        [SerializeField] private Graphic _glow;
        [SerializeField] private float _glowMinAlpha = 0.15f;
        [SerializeField] private float _glowMaxAlpha = 0.55f;
        [SerializeField] private float _glowSpeed = 2.6f;

        [Header("Disabled state")]
        [Tooltip("Scale applied while the button is non-interactable, so dead controls visibly recede.")]
        [SerializeField] private float _disabledScale = 0.94f;

        private Button _button;
        private RectTransform _rect;
        private bool _hovered;
        private bool _pressed;

        /// <summary>Extra one-shot punch, decaying to zero. Lets code kick the button.</summary>
        private float _punch;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _rect = (RectTransform)transform;
        }

        private void OnDisable()
        {
            // Reset so a button hidden mid-press doesn't come back squashed.
            _hovered = false;
            _pressed = false;
            _punch = 0f;
            if (_rect != null) _rect.localScale = Vector3.one;
        }

        /// <summary>Kicks the button outward briefly — call when its action fires.</summary>
        public void Punch(float amount = 0.12f) => _punch = amount;

        private void Update()
        {
            if (_rect == null || _button == null) return;

            bool usable = _button.interactable;

            float target = 1f;
            if (!usable) target = _disabledScale;
            else if (_pressed) target = _pressScale;
            else if (_hovered) target = _hoverScale;
            target += _punch;

            if (_punch > 0f) _punch = Mathf.Max(0f, _punch - Time.unscaledDeltaTime * 0.6f);

            // Unscaled time: menus and pause states should still feel alive.
            float k = 1f - Mathf.Exp(-_responseSpeed * Time.unscaledDeltaTime);
            float s = Mathf.Lerp(_rect.localScale.x, target, k);
            _rect.localScale = new Vector3(s, s, 1f);

            if (_glow == null) return;

            if (!usable)
            {
                SetGlowAlpha(0f);
                return;
            }

            float wave = (Mathf.Sin(Time.unscaledTime * _glowSpeed) + 1f) * 0.5f;
            SetGlowAlpha(Mathf.Lerp(_glowMinAlpha, _glowMaxAlpha, wave));
        }

        private void SetGlowAlpha(float a)
        {
            Color c = _glow.color;
            if (Mathf.Approximately(c.a, a)) return;
            c.a = a;
            _glow.color = c;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData) { _hovered = false; _pressed = false; }
        public void OnPointerDown(PointerEventData eventData) { if (_button.interactable) _pressed = true; }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_pressed && _button.interactable) Punch();
            _pressed = false;
        }
    }
}
