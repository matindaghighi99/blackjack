using TMPro;
using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// The stake on the table, as a chip that actually travels.
    ///
    /// Placing a bet throws the chip from the balance pill down to the betting spot;
    /// winning sends it back up to the pill; losing slides it away to the dealer. Chips
    /// moving between the player's balance and the table is the clearest way to show
    /// where the money went — a number changing in a corner is not.
    ///
    /// The chip's denomination is drawn live rather than baked into the sprite, so it
    /// always shows the real stake (including after a double or a split).
    /// </summary>
    public sealed class BetChipView : MonoBehaviour
    {
        private enum Mode { Hidden, Placing, Resting, Returning, Losing }

        [Tooltip("Draws the stake on the chip face. The sprite's own number is painted out.")]
        [SerializeField] private TMP_Text _label;

        [Header("Flight")]
        [Tooltip("Where the chip flies from and back to — the balance pill, in canvas units.")]
        [SerializeField] private Vector2 _flyFrom = new Vector2(-236f, 830f);
        [Tooltip("Where a lost chip is swept off to — the dealer's side of the table.")]
        [SerializeField] private Vector2 _loseTo = new Vector2(120f, 520f);
        [SerializeField] private float _placeDuration = 0.46f;
        [SerializeField] private float _returnDuration = 0.5f;

        [Tooltip("Sideways bow of the flight path, in canvas units.")]
        [SerializeField] private float _arc = 190f;
        [Tooltip("Degrees the chip spins through while travelling.")]
        [SerializeField] private float _spin = 260f;
        [Tooltip("Scale the chip starts at, as if thrown from a distance.")]
        [SerializeField] private float _flyScale = 0.45f;
        [Tooltip("How far past full size the chip swells as it lands, before settling.")]
        [SerializeField] private float _landOvershoot = 0.16f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _rest;
        private Mode _mode = Mode.Hidden;
        private float _t;
        private float _land;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            // The betting spot is wherever the scene placed this object; it is serialized
            // by the time Awake runs, so reading it here is safe.
            _rest = _rect.anchoredPosition;

            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            Hide();
        }

        /// <summary>Hides the chip immediately, with no animation.</summary>
        public void Hide()
        {
            _mode = Mode.Hidden;
            _t = 0f;
            _land = 0f;
            if (_group != null) _group.alpha = 0f;
            if (_rect != null)
            {
                _rect.anchoredPosition = _rest;
                _rect.localScale = Vector3.one;
                _rect.localRotation = Quaternion.identity;
            }
        }

        /// <summary>Throws the chip from the balance onto the table.</summary>
        public void PlaceBet(long amount)
        {
            SetAmount(amount);
            _mode = Mode.Placing;
            _t = 0f;
            _land = 0f;
            if (_group != null) _group.alpha = 1f;
            Apply();
        }

        /// <summary>Updates the number on the chip without re-throwing it.</summary>
        public void SetAmount(long amount)
        {
            if (_label != null) _label.text = amount.ToString("N0");
        }

        /// <summary>
        /// Settles the round: a won stake flies back to the balance, a lost one is swept
        /// away toward the dealer. A push is treated as a return — the stake does come back.
        /// </summary>
        public void Settle(bool playerKeepsStake)
        {
            if (_mode == Mode.Hidden) return;
            _mode = playerKeepsStake ? Mode.Returning : Mode.Losing;
            _t = 0f;
        }

        private void Update()
        {
            switch (_mode)
            {
                case Mode.Hidden:
                    return;

                case Mode.Placing:
                    _t = Mathf.Min(1f, _t + Time.deltaTime / Mathf.Max(0.01f, _placeDuration));
                    if (_t >= 1f)
                    {
                        _mode = Mode.Resting;
                        _land = 1f;
                    }
                    Apply();
                    return;

                case Mode.Resting:
                    if (_land <= 0f) return;
                    _land = Mathf.Max(0f, _land - Time.deltaTime / 0.3f);
                    Apply();
                    return;

                default:
                    _t = Mathf.Min(1f, _t + Time.deltaTime / Mathf.Max(0.01f, _returnDuration));
                    Apply();
                    if (_t >= 1f) Hide();
                    return;
            }
        }

        private void Apply()
        {
            if (_rect == null) return;

            bool outbound = _mode == Mode.Placing;
            Vector2 from = outbound ? _flyFrom : _rest;
            Vector2 to = outbound ? _rest : (_mode == Mode.Losing ? _loseTo : _flyFrom);

            // Ease-out on the way in so the chip decelerates onto its spot; ease-in on the
            // way out so it accelerates away, which reads as being swept rather than placed.
            float e = outbound
                ? 1f - Mathf.Pow(1f - _t, 3f)
                : _t * _t;

            Vector2 straight = Vector2.LerpUnclamped(from, to, e);
            Vector2 travel = to - from;
            Vector2 perpendicular = new Vector2(-travel.y, travel.x).normalized;
            _rect.anchoredPosition = straight + perpendicular * (_arc * Mathf.Sin(e * Mathf.PI));

            _rect.localRotation = Quaternion.Euler(0f, 0f, _spin * (outbound ? 1f - e : e));

            float scale = outbound
                ? Mathf.LerpUnclamped(_flyScale, 1f, e)
                : Mathf.LerpUnclamped(1f, _flyScale, e);
            if (_land > 0f) scale += _landOvershoot * _land * Mathf.Sin(_land * Mathf.PI);
            _rect.localScale = new Vector3(scale, scale, 1f);

            // Fade only on the way out; the chip arrives fully solid.
            if (_group != null && !outbound) _group.alpha = 1f - Mathf.Clamp01(e * 1.15f);
        }
    }
}
