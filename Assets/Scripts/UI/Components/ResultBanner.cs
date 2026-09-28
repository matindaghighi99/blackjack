using BlackjackGame.UI.Presentation;
using BlackjackGame.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// The round's verdict, set across the felt between the two hands: a dark band with
    /// hairline rules, the headline in the display serif, the amount, and the reason.
    ///
    /// It arrives rather than pops: the band fades up, the rules draw outward from the
    /// centre and the headline's letter-spacing tightens into place. A blackjack adds one
    /// slow sweep of light across the headline — rewarding, never a firework. It is not a
    /// modal; it stays until the next bet starts and never blocks a tap.
    /// </summary>
    public sealed class ResultBanner : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _content;
        [SerializeField] private Image _ruleTop;
        [SerializeField] private Image _ruleBottom;
        [SerializeField] private TMP_Text _headline;
        [SerializeField] private TMP_Text _amount;
        [SerializeField] private TMP_Text _detail;
        [Tooltip("Soft light swept across the headline on a blackjack.")]
        [SerializeField] private Image _sheen;

        [Header("Motion")]
        [SerializeField] private float _inDuration = 0.6f;
        [SerializeField] private float _outDuration = 0.25f;
        [SerializeField] private float _headlineSpacingFrom = 24f;
        [SerializeField] private float _headlineSpacing = 10f;

        private float _t = 1f;
        private float _out = 1f;
        private bool _visible;
        private bool _sweep;
        private float _sheenT = 1f;
        private Color _ruleColor = Palette.Gold;
        private float _ruleAlpha = 0.8f;

        public bool Visible => _visible;

        // No Awake reset on purpose: the scene ships this object inactive, and Show()
        // activating it would run Awake mid-Show — a reset there would hide it again.

        /// <summary>Scales the banner's type to the height the layout can spare.</summary>
        public void SetScale(float scale)
        {
            if (_content != null) _content.localScale = new Vector3(scale, scale, 1f);
        }

        public void Show(RoundSummary summary)
        {
            gameObject.SetActive(true);
            Color accent;
            Color headline = Palette.Ivory;
            switch (summary.Tone)
            {
                case ResultTone.Blackjack:
                    accent = Palette.Gold;
                    headline = Palette.Gold;
                    break;
                case ResultTone.Win: accent = Palette.Win; break;
                case ResultTone.Loss: accent = Palette.Loss; break;
                default: accent = Palette.Push; break;
            }

            if (_headline != null)
            {
                _headline.text = summary.Headline;
                _headline.color = headline;
            }
            if (_amount != null)
            {
                _amount.text = summary.Amount;
                _amount.color = accent;
                _amount.gameObject.SetActive(!string.IsNullOrEmpty(summary.Amount));
            }
            if (_detail != null)
            {
                _detail.text = summary.Detail;
                _detail.gameObject.SetActive(!string.IsNullOrEmpty(summary.Detail));
            }

            _ruleColor = accent;
            _ruleAlpha = summary.Tone == ResultTone.Push ? 0.4f : 0.8f;
            _sweep = summary.Tone == ResultTone.Blackjack && !MotionPrefs.Reduced;
            _sheenT = _sweep ? 0f : 1f;
            _visible = true;
            _out = 1f;
            _t = 0f;
            Apply();
        }

        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            _out = 0f;
        }

        public void HideNow()
        {
            _visible = false;
            _t = 1f;
            _out = 1f;
            if (_group != null) _group.alpha = 0f;
            if (_sheen != null) _sheen.enabled = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_visible)
            {
                if (_t < 1f)
                {
                    _t = Mathf.Min(1f, _t + dt / Mathf.Max(0.01f, MotionPrefs.Duration(_inDuration)));
                    Apply();
                }
                if (_sheenT < 1f)
                {
                    // The sweep waits until the headline has landed.
                    if (_t > 0.55f) _sheenT = Mathf.Min(1f, _sheenT + dt / 1.1f);
                    ApplySheen();
                }
            }
            else if (_out < 1f)
            {
                _out = Mathf.Min(1f, _out + dt / Mathf.Max(0.01f, MotionPrefs.Duration(_outDuration)));
                if (_group != null) _group.alpha = 1f - Ease.OutCubic(_out);
                if (_out >= 1f) HideNow();
            }
        }

        private void Apply()
        {
            float e = Ease.OutCubic(_t);
            if (_group != null) _group.alpha = Mathf.Clamp01(_t * 2.2f);

            if (_headline != null)
            {
                _headline.characterSpacing = MotionPrefs.Reduced
                    ? _headlineSpacing
                    : Mathf.Lerp(_headlineSpacingFrom, _headlineSpacing, e);
                _headline.rectTransform.anchoredPosition = new Vector2(
                    _headline.rectTransform.anchoredPosition.x, HeadlineY + MotionPrefs.Amount(10f) * (1f - e));
            }

            float rules = MotionPrefs.Reduced ? 1f : Ease.OutCubic(Mathf.Clamp01((_t - 0.1f) / 0.7f));
            SetRule(_ruleTop, rules);
            SetRule(_ruleBottom, rules);
        }

        private float _headlineY = float.NaN;
        private float HeadlineY
        {
            get
            {
                if (float.IsNaN(_headlineY) && _headline != null)
                    _headlineY = _headline.rectTransform.anchoredPosition.y;
                return float.IsNaN(_headlineY) ? 0f : _headlineY;
            }
        }

        private void SetRule(Image rule, float progress)
        {
            if (rule == null) return;
            rule.rectTransform.localScale = new Vector3(Mathf.Max(0.001f, progress), 1f, 1f);
            Color c = _ruleColor;
            c.a = _ruleAlpha;
            rule.color = c;
        }

        private void ApplySheen()
        {
            if (_sheen == null) return;
            bool on = _sheenT > 0f && _sheenT < 1f;
            _sheen.enabled = on;
            if (!on) return;
            float w = _content != null ? ((RectTransform)_content.parent).rect.width : 800f;
            float x = Mathf.Lerp(-w * 0.4f, w * 0.4f, Ease.InOutCubic(_sheenT));
            _sheen.rectTransform.anchoredPosition = new Vector2(x, _sheen.rectTransform.anchoredPosition.y);
            Color c = Palette.GoldLight;
            c.a = 0.34f * Mathf.Sin(_sheenT * Mathf.PI);
            _sheen.color = c;
        }
    }
}
