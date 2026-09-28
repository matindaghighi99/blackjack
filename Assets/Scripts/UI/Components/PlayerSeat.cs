using BlackjackGame.UI.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// One player hand's place at the table: its cards, its total badge and a thin gold
    /// marker under the hand whose turn it is. When a split adds hands the seats glide to
    /// their new places instead of jumping.
    /// </summary>
    public sealed class PlayerSeat : MonoBehaviour
    {
        [SerializeField] private HandView _hand;
        [SerializeField] private TotalBadge _badge;
        [Tooltip("Hairline under the hand currently being played (split hands only).")]
        [SerializeField] private Image _turnMarker;

        private RectTransform _rect;
        private Vector2 _target;
        private bool _placed;
        private float _marker;
        private float _markerTarget;

        public HandView Hand => _hand;
        public TotalBadge Badge => _badge;

        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        /// <summary>Moves the seat. The first placement (and reduced motion) is instant.</summary>
        public void Place(Vector2 anchoredPosition, bool instant)
        {
            _target = anchoredPosition;
            if (instant || !_placed || MotionPrefs.Reduced) Rect.anchoredPosition = anchoredPosition;
            _placed = true;
        }

        public void SetBadgeOffset(Vector2 offset)
        {
            if (_badge != null) ((RectTransform)_badge.transform).anchoredPosition = offset;
        }

        public void SetMarker(float width, float y)
        {
            if (_turnMarker == null) return;
            RectTransform rt = _turnMarker.rectTransform;
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        /// <summary>Gold label and underline while it is this hand's turn.</summary>
        public void SetTurn(bool isTurn, bool showMarker)
        {
            if (_badge != null) _badge.SetTurn(isTurn);
            _markerTarget = isTurn && showMarker ? 1f : 0f;
            if (MotionPrefs.Reduced) ApplyMarker(_markerTarget);
        }

        /// <summary>Empties the seat and takes it off the table.</summary>
        public void Vacate()
        {
            if (_hand != null) _hand.Clear();
            if (_badge != null) _badge.HideValue();
            _markerTarget = 0f;
            ApplyMarker(0f);
            _placed = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_placed && (Rect.anchoredPosition - _target).sqrMagnitude > 0.01f)
                Rect.anchoredPosition = Ease.Damp(Rect.anchoredPosition, _target, 9f, dt);

            if (!Mathf.Approximately(_marker, _markerTarget))
                ApplyMarker(Mathf.Abs(_marker - _markerTarget) < 0.01f ? _markerTarget : Ease.Damp(_marker, _markerTarget, 12f, dt));
        }

        private void ApplyMarker(float value)
        {
            _marker = value;
            if (_turnMarker == null) return;
            Color c = Palette.Gold;
            c.a = value;
            _turnMarker.color = c;
            _turnMarker.enabled = value > 0.01f;
            _turnMarker.rectTransform.localScale = new Vector3(Mathf.Lerp(0.4f, 1f, value), 1f, 1f);
        }
    }
}
