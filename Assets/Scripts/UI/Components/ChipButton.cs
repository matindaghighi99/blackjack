using BlackjackGame.UI.Presentation;
using BlackjackGame.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// One chip in the rack. Shows its denomination on the chip face and, when it was the
    /// last chip used, rises slightly inside a thin gold ring — the selected state that
    /// tells the player which chip another tap on the stake will repeat.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ChipButton : MonoBehaviour
    {
        [SerializeField] private Image _face;
        [SerializeField] private TMP_Text _label;
        [Tooltip("Thin ring shown around the selected chip.")]
        [SerializeField] private Image _ring;
        [Tooltip("The visual root that rises when selected (face, label and ring).")]
        [SerializeField] private RectTransform _visual;
        [SerializeField] private float _selectedLift = 9f;

        private Button _button;
        private bool _selected;
        private float _lift;

        public int Value { get; private set; }

        public Button Button => _button != null ? _button : _button = GetComponent<Button>();

        private void Awake()
        {
            _button = GetComponent<Button>();
            Apply(0f);
        }

        /// <summary>Shows <paramref name="value"/> using its colourway from the library.</summary>
        public void Bind(int value, ChipSpriteLibrary library)
        {
            Value = value;
            if (library != null)
            {
                ChipSpriteLibrary.Entry entry = library.Get(value);
                if (_face != null && entry.Face != null) _face.sprite = entry.Face;
                if (_label != null) _label.color = entry.DarkLabel ? Palette.Ink : Palette.Ivory;
            }
            if (_label != null) _label.text = TableText.ChipLabel(value);
            name = $"Chip{value}";
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (MotionPrefs.Reduced) Apply(Target);
        }

        private float Target => _selected && Button.interactable ? 1f : 0f;

        private void Update()
        {
            float target = Target;
            if (Mathf.Approximately(_lift, target)) return;
            float next = MotionPrefs.Reduced ? target : Ease.Damp(_lift, target, 14f, Time.unscaledDeltaTime);
            if (Mathf.Abs(next - target) < 0.002f) next = target;
            Apply(next);
        }

        private void Apply(float lift)
        {
            _lift = lift;
            if (_visual != null) _visual.anchoredPosition = new Vector2(0f, _selectedLift * lift);
            if (_ring != null)
            {
                Color c = Palette.Gold;
                c.a = 0.95f * lift;
                _ring.color = c;
                _ring.enabled = lift > 0.01f;
            }
        }
    }
}
