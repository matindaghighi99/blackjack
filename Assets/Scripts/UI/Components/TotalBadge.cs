using BlackjackGame.UI.Presentation;
using BlackjackGame.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// A seat's header: a small tracked label (DEALER, YOU, HAND 2) beside a pill carrying
    /// the hand total. The label turns gold while it is that seat's turn, so "whose turn is
    /// it?" is answered at the hand itself; the pill takes the total's tone (gold for 21 and
    /// blackjack, coral for a bust) and gives a small pop when the number changes.
    /// </summary>
    public sealed class TotalBadge : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _pill;
        [SerializeField] private TMP_Text _value;

        [Header("Metrics")]
        [SerializeField] private float _gap = 16f;
        [SerializeField] private float _pillPadding = 34f;
        [SerializeField] private float _minPillWidth = 64f;

        private string _shown = "";
        private float _pop;
        private bool _hasValue;

        private void Awake() => Layout();

        public void SetLabel(string text)
        {
            if (_label == null) return;
            _label.text = text ?? "";
            _label.gameObject.SetActive(!string.IsNullOrEmpty(text));
            Layout();
        }

        /// <summary>Gold label while it is this seat's turn to act.</summary>
        public void SetTurn(bool turn)
        {
            if (_label != null) _label.color = turn ? Palette.Gold : Palette.Muted;
        }

        /// <summary>Shows a hand total.</summary>
        public void ShowTotal(TotalDisplay total)
        {
            if (string.IsNullOrEmpty(total.Text))
            {
                HideValue();
                return;
            }

            Color fill, ink = Palette.Ink;
            switch (total.Tone)
            {
                case TotalTone.Blackjack: fill = Palette.Gold; break;
                case TotalTone.TwentyOne: fill = Palette.GoldLight; break;
                case TotalTone.Bust: fill = Palette.Loss; break;
                default: fill = Palette.Ivory; break;
            }
            Show(total.Text, fill, ink);
        }

        /// <summary>Shows a settled split hand's result in place of its total.</summary>
        public void ShowResult(string text, ResultTone tone)
        {
            Color fill;
            switch (tone)
            {
                case ResultTone.Blackjack: fill = Palette.Gold; break;
                case ResultTone.Win: fill = Palette.Win; break;
                case ResultTone.Loss: fill = Palette.Loss; break;
                default: fill = Palette.Push; break;
            }
            Show(text, fill, Palette.Ink);
        }

        public void HideValue()
        {
            _hasValue = false;
            _shown = "";
            if (_pill != null) _pill.gameObject.SetActive(false);
            Layout();
        }

        private void Show(string text, Color fill, Color ink)
        {
            _hasValue = true;
            if (_pill != null)
            {
                _pill.gameObject.SetActive(true);
                _pill.color = fill;
            }
            if (_value != null)
            {
                _value.text = text;
                _value.color = ink;
            }
            if (text != _shown && !string.IsNullOrEmpty(_shown) && !MotionPrefs.Reduced) _pop = 1f;
            _shown = text;
            Layout();
        }

        /// <summary>Centres label + pill as one unit on this object's position.</summary>
        private void Layout()
        {
            bool labelOn = _label != null && _label.gameObject.activeSelf && !string.IsNullOrEmpty(_label.text);
            float labelWidth = labelOn ? _label.GetPreferredValues(_label.text).x : 0f;
            float pillWidth = 0f;
            if (_hasValue && _value != null && _pill != null)
                pillWidth = Mathf.Max(_minPillWidth, _value.GetPreferredValues(_value.text).x + _pillPadding);

            float gap = labelOn && pillWidth > 0f ? _gap : 0f;
            float total = labelWidth + gap + pillWidth;
            float left = -total / 2f;

            if (labelOn)
            {
                RectTransform lr = _label.rectTransform;
                lr.sizeDelta = new Vector2(labelWidth + 4f, lr.sizeDelta.y);
                lr.anchoredPosition = new Vector2(left + labelWidth / 2f, lr.anchoredPosition.y);
            }
            if (pillWidth > 0f)
            {
                RectTransform pr = _pill.rectTransform;
                pr.sizeDelta = new Vector2(pillWidth, pr.sizeDelta.y);
                pr.anchoredPosition = new Vector2(left + labelWidth + gap + pillWidth / 2f, pr.anchoredPosition.y);
            }
        }

        private void Update()
        {
            if (_pop <= 0f || _pill == null) return;
            _pop = Mathf.Max(0f, _pop - Time.deltaTime / 0.26f);
            float s = 1f + 0.09f * Mathf.Sin(_pop * Mathf.PI) * _pop;
            _pill.rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }
}
