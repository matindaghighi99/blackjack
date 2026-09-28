using BlackjackGame.UI.Feedback;
using BlackjackGame.UI.Interaction;
using BlackjackGame.UI.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// The tactile layer every button shares, so all controls respond the same way:
    ///
    /// * hover — a slight lift and a faint brightening overlay;
    /// * press — a small scale-down, released with a gentle settle;
    /// * disabled — dimmed, and deaf to hover, so it never looks clickable;
    /// * keyboard focus — a gold ring, shown only when navigating by keyboard;
    /// * primary — an optional slow breathing glow for the one action that matters now.
    ///
    /// Unity's ColorBlock can only tint; this owns scale, opacity and the extra graphics.
    /// Buttons using it should have their Selectable transition set to None.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonJuice : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        [Header("Scale response")]
        [SerializeField] private float _hoverScale = 1.02f;
        [SerializeField] private float _pressScale = 0.965f;
        [Tooltip("How fast the button chases its target scale. Higher is snappier.")]
        [SerializeField] private float _responseSpeed = 18f;
        [Tooltip("What scales. Defaults to this button; point it at a child to keep the hit area still.")]
        [SerializeField] private RectTransform _visual;

        [Header("Hover")]
        [Tooltip("White overlay faded in on hover.")]
        [SerializeField] private Graphic _highlight;
        [SerializeField] private float _highlightAlpha = 0.07f;

        [Header("Focus")]
        [Tooltip("Ring shown while the button has keyboard focus.")]
        [SerializeField] private Graphic _focusRing;

        [Header("Primary glow")]
        [Tooltip("Optional soft glow behind the button that breathes while it is usable.")]
        [SerializeField] private Graphic _glow;
        [SerializeField] private float _glowMinAlpha = 0.05f;
        [SerializeField] private float _glowMaxAlpha = 0.22f;
        [SerializeField] private float _glowSpeed = 1.6f;

        [Header("Disabled")]
        [SerializeField] private float _disabledAlpha = 0.36f;

        private Button _button;
        private CanvasGroup _group;
        private bool _hovered;
        private bool _pressed;
        private bool _selected;
        private float _settle;
        private float _hover;
        private float _focus;

        public Button Button => _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_visual == null) _visual = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _button.onClick.AddListener(() => UiCues.Raise(UiCue.ButtonClick));
            Snap();
        }

        private void OnEnable() => Snap();

        private void OnDisable()
        {
            // A button hidden mid-press must not come back squashed.
            _hovered = false;
            _pressed = false;
            _settle = 0f;
            if (_visual != null) _visual.localScale = Vector3.one;
        }

        private void Snap()
        {
            if (_button == null) return;
            bool usable = _button.interactable;
            if (_group != null) _group.alpha = usable ? 1f : _disabledAlpha;
            _hover = 0f;
            _focus = FocusVisible ? 1f : 0f;
            SetAlpha(_highlight, 0f);
            SetAlpha(_focusRing, _focus);
            if (!usable) SetAlpha(_glow, 0f);
        }

        private bool FocusVisible => _selected && InputModality.Keyboard && _button != null && _button.IsInteractable();

        private void Update()
        {
            if (_visual == null || _button == null) return;

            float dt = Time.unscaledDeltaTime;
            bool usable = _button.IsInteractable();
            bool reduced = MotionPrefs.Reduced;

            float target = 1f;
            if (usable)
            {
                if (_pressed) target = _pressScale;
                else if (_hovered) target = _hoverScale;
            }
            if (_settle > 0f)
            {
                _settle = Mathf.Max(0f, _settle - dt / 0.22f);
                target += 0.02f * Mathf.Sin(_settle * Mathf.PI);
            }
            if (reduced) target = 1f;

            float s = Ease.Damp(_visual.localScale.x, target, _responseSpeed, dt);
            if (Mathf.Abs(s - target) < 0.0005f) s = target;
            _visual.localScale = new Vector3(s, s, 1f);

            if (_group != null)
                _group.alpha = Ease.Damp(_group.alpha, usable ? 1f : _disabledAlpha, 16f, dt);

            _hover = Ease.Damp(_hover, usable && _hovered && !_pressed ? 1f : 0f, 16f, dt);
            SetAlpha(_highlight, _hover * _highlightAlpha);

            _focus = Ease.Damp(_focus, FocusVisible ? 1f : 0f, 18f, dt);
            SetAlpha(_focusRing, _focus);

            if (_glow != null)
            {
                float wave = reduced ? 0.5f : (Mathf.Sin(Time.unscaledTime * _glowSpeed) + 1f) * 0.5f;
                SetAlpha(_glow, usable ? Mathf.Lerp(_glowMinAlpha, _glowMaxAlpha, wave) : 0f);
            }
        }

        private static void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            Color c = g.color;
            if (Mathf.Abs(c.a - a) < 0.001f) return;
            c.a = a;
            g.color = c;
            bool on = a > 0.002f;
            if (g.enabled != on) g.enabled = on;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            InputModality.NotePointer();
            if (_button.IsInteractable()) _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_pressed && _button.IsInteractable()) _settle = 1f;
            _pressed = false;
        }

        public void OnSelect(BaseEventData eventData) => _selected = true;

        public void OnDeselect(BaseEventData eventData) => _selected = false;
    }
}
