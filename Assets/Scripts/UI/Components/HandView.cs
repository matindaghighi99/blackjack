using System.Collections.Generic;
using BlackjackGame.Blackjack.Cards;
using BlackjackGame.UI.Feedback;
using BlackjackGame.UI.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Renders one hand as a gently fanned, overlapping row of cards, and animates every
    /// change to it the way a dealer would make it:
    ///
    /// * New cards leave the shoe face down, travel in a shallow arc and turn face up in
    ///   flight, settling onto the felt with a small lift-and-drop.
    /// * Reveals (the hole card) are a real 3D turn about the card's vertical axis — the
    ///   canvas renders through a perspective camera, so the card foreshortens as it turns.
    /// * A hand that gains cards re-centres by easing, never by snapping.
    /// * At the next deal, the old hand is collected to the discard side rather than
    ///   vanishing.
    ///
    /// Purely presentational: it reads the engine's cards and never mutates them. Card
    /// objects are pooled — a hand changes many times a round and churning GameObjects
    /// would generate garbage on mobile.
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private CardSpriteLibrary _library;
        [Tooltip("Image prefab used as the card template.")]
        [SerializeField] private Image _cardPrefab;
        [Tooltip("Parent the spawned cards are laid out under.")]
        [SerializeField] private RectTransform _cardRoot;

        [Header("Layout")]
        [SerializeField] private Vector2 _cardSize = new Vector2(150f, 210f);

        [Tooltip("Horizontal step between cards, as a fraction of card width. Below 1 the cards overlap.")]
        [Range(0.30f, 1.20f)]
        [SerializeField] private float _spacing = 0.62f;

        [Tooltip("Widest the hand may spread, in canvas units, centre-to-centre between the " +
                 "first and last card. Cards tighten their overlap rather than growing past it. " +
                 "0 disables the cap.")]
        [SerializeField] private float _maxSpan = 420f;

        [Tooltip("Total fan spread across the hand, in degrees.")]
        [Range(0f, 20f)]
        [SerializeField] private float _fanDegrees = 4f;

        [Tooltip("Deterministic tilt per card, in degrees, so a hand doesn't look stamped out.")]
        [Range(0f, 6f)]
        [SerializeField] private float _jitterDegrees = 1.6f;

        [Header("Depth")]
        [Tooltip("Soft shadow behind each card. Leave empty for no shadow.")]
        [SerializeField] private Sprite _shadowSprite;
        [SerializeField] private Vector2 _shadowOffset = new Vector2(4f, -9f);
        [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0f, 0.55f);
        [Tooltip("Extra size the shadow adds around the card (the sprite's blur margin).")]
        [SerializeField] private float _shadowSpread = 44f;

        [Header("Deal")]
        [Tooltip("Fallback deal origin, local to the card root, used until SetDealOrigin is called.")]
        [SerializeField] private Vector2 _dealFrom = new Vector2(420f, 520f);
        [Tooltip("Seconds for a card to travel from the shoe to its place.")]
        [SerializeField] private float _dealDuration = 0.46f;
        [Tooltip("Delay between consecutive cards arriving in the same render.")]
        [SerializeField] private float _dealStagger = 0.16f;
        [Tooltip("Sideways bow of the flight path, in canvas units. A card skates in a shallow curve.")]
        [SerializeField] private float _dealArc = 46f;
        [Tooltip("Extra degrees of rotation shed during the flight.")]
        [SerializeField] private float _dealSpin = 22f;
        [Tooltip("How far the card rises toward the viewer mid-flight, as a scale fraction.")]
        [SerializeField] private float _dealLift = 0.06f;
        [Tooltip("Scale the card starts at in the shoe — it sits further from the viewer.")]
        [SerializeField] private float _dealStartScale = 0.82f;

        [Header("Flip")]
        [Tooltip("Seconds for a card to turn over.")]
        [SerializeField] private float _flipDuration = 0.42f;
        [Tooltip("How far a turning card lifts toward the viewer, as a scale fraction.")]
        [SerializeField] private float _flipLift = 0.07f;
        [Tooltip("Multiplier on flip duration for the dealer's hole card — the moment the round turns.")]
        [SerializeField] private float _holeCardFlipSlowdown = 1.3f;

        [Header("Collect")]
        [Tooltip("Fallback collect point, local to the card root, used until SetDiscardPoint is called.")]
        [SerializeField] private Vector2 _discardTo = new Vector2(-520f, 560f);
        [SerializeField] private float _collectDuration = 0.36f;

        [Header("Outcome glow")]
        [Tooltip("Soft light behind the hand, tinted and faded in when the hand wins.")]
        [SerializeField] private Image _glow;

        // ---- runtime state ------------------------------------------------------

        private sealed class CardSlot
        {
            public Image Face;
            public Image Shadow;
            public RectTransform Rect;
            public RectTransform ShadowRect;

            /// <summary>The sprite this card ends on (face, or back for a hole card).</summary>
            public Sprite Target;

            public Vector2 From, To;
            public float FromRot, ToRot;
            public float Delay;
            /// <summary>0..1 along the deal; 1 means settled.</summary>
            public float Travel = 1f;
            public float Arc;
            public float Spin;
            public bool FlipsInFlight;
            public bool DealCueRaised;

            /// <summary>0..1 through a reveal; 1 means no reveal in progress.</summary>
            public float Flip = 1f;
            public float FlipDuration;
            public Sprite FlipFrom;

            /// <summary>Decaying 0..1 settle after touchdown.</summary>
            public float Land;

            public float LeaveT;
            public Vector2 LeaveFrom, LeaveTo;
            public float LeaveRot;

            public bool Busy => Travel < 1f || Flip < 1f;
        }

        private readonly List<CardSlot> _slots = new List<CardSlot>();
        private readonly List<CardSlot> _leaving = new List<CardSlot>();
        private readonly List<CardSlot> _free = new List<CardSlot>();

        private RectTransform _shadowLayer;
        private RectTransform _faceLayer;

        private bool _hasDealOrigin;
        private Vector3 _dealOriginWorld;
        private bool _hasDiscard;
        private Vector3 _discardWorld;
        private bool _hasTransfer;
        private Vector3 _transferWorld;

        // Emphasis (split hands) and outcome dimming, eased rather than snapped.
        private float _scaleTarget = 1f;
        private float _scale = 1f;
        private float _dimTarget = 1f;
        private float _dim = 1f;
        private bool _emphasised = true;
        private bool _outcomeDimmed;

        private Color _glowColor = Color.white;
        private float _glowTarget;
        private float _glowAlpha;
        private float _glowPulse;

        /// <summary>True while any card is still travelling or turning over.</summary>
        public bool IsAnimating
        {
            get
            {
                foreach (CardSlot s in _slots)
                    if (s.Busy) return true;
                return false;
            }
        }

        /// <summary>How many cards are currently displayed (collected cards excluded).</summary>
        public int VisibleCardCount => _slots.Count;

        public Vector2 CardSize => _cardSize;

        /// <summary>Width the hand currently occupies, outer card edge to outer card edge.</summary>
        public float Width => _slots.Count == 0 ? 0f : Span(_slots.Count) + _cardSize.x;

        private void Awake() => EnsureLayers();

        private void EnsureLayers()
        {
            if (_shadowLayer != null || _cardRoot == null) return;
            _shadowLayer = NewLayer("Shadows");
            _faceLayer = NewLayer("Faces");
        }

        private RectTransform NewLayer(string layerName)
        {
            var go = new GameObject(layerName, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_cardRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        // =====================================================================
        //  Configuration
        // =====================================================================

        /// <summary>Card size and fan cap, from the responsive layout. Settled cards ease to fit.</summary>
        public void SetLayout(Vector2 cardSize, float maxSpan)
        {
            _cardSize = cardSize;
            _maxSpan = maxSpan;
            Reflow();
        }

        /// <summary>Where new cards come from — the shoe's real position on this screen.</summary>
        public void SetDealOrigin(Vector3 worldPosition)
        {
            _hasDealOrigin = true;
            _dealOriginWorld = worldPosition;
        }

        /// <summary>Where collected cards go at the next deal.</summary>
        public void SetDiscardPoint(Vector3 worldPosition)
        {
            _hasDiscard = true;
            _discardWorld = worldPosition;
        }

        /// <summary>
        /// The next new card flies in from <paramref name="worldPosition"/> already face up,
        /// instead of from the shoe — a split card moving across from its old hand.
        /// </summary>
        public void DealNextFrom(Vector3 worldPosition)
        {
            _hasTransfer = true;
            _transferWorld = worldPosition;
        }

        /// <summary>World position of a displayed card, e.g. the source of a split transfer.</summary>
        public bool TryGetCardWorldPosition(int index, out Vector3 position)
        {
            if (index >= 0 && index < _slots.Count)
            {
                position = _slots[index].Rect.position;
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>
        /// Marks this view as the hand in play (full size and colour) or one waiting its
        /// turn (slightly smaller and dimmed). <paramref name="instant"/> skips the ease.
        /// </summary>
        public void SetEmphasis(bool active, bool instant = false)
        {
            _emphasised = active;
            UpdateTargets();
            if (instant) SnapEmphasis();
        }

        /// <summary>Dims a settled losing hand so the eye goes to what won.</summary>
        public void SetOutcomeDim(bool dimmed)
        {
            _outcomeDimmed = dimmed;
            UpdateTargets();
        }

        /// <summary>Fades a soft light in behind the hand (strength 0 turns it off).</summary>
        public void SetGlow(Color color, float strength)
        {
            _glowColor = color;
            _glowTarget = Mathf.Clamp01(strength);
            if (MotionPrefs.Reduced) _glowAlpha = _glowTarget;
        }

        /// <summary>A single swell of the glow — the blackjack moment.</summary>
        public void PulseGlow(float amount = 0.35f)
        {
            if (!MotionPrefs.Reduced) _glowPulse = amount;
        }

        private void UpdateTargets()
        {
            _scaleTarget = _emphasised ? 1f : 0.92f;
            _dimTarget = (_emphasised ? 1f : 0.8f) * (_outcomeDimmed ? 0.62f : 1f);
            if (MotionPrefs.Reduced) SnapEmphasis();
        }

        private void SnapEmphasis()
        {
            _scale = _scaleTarget;
            _dim = _dimTarget;
            ApplyEmphasis();
        }

        // =====================================================================
        //  Clearing
        // =====================================================================

        /// <summary>Hides every card immediately, with no animation.</summary>
        public void Clear()
        {
            for (int i = _slots.Count - 1; i >= 0; i--) Release(_slots[i]);
            _slots.Clear();
            foreach (CardSlot s in _leaving) Release(s);
            _leaving.Clear();
            ResetOutcome();
        }

        /// <summary>
        /// Sweeps the displayed cards off to the discard side. The view is empty (and
        /// immediately reusable) as soon as this returns; the old cards animate out on
        /// their own and are recycled when they land.
        /// </summary>
        public void Collect()
        {
            if (_slots.Count == 0)
            {
                ResetOutcome();
                return;
            }

            if (MotionPrefs.Reduced)
            {
                Clear();
                return;
            }

            Vector2 to = _hasDiscard ? (Vector2)_cardRoot.InverseTransformPoint(_discardWorld) : _discardTo;
            for (int i = 0; i < _slots.Count; i++)
            {
                CardSlot s = _slots[i];
                s.LeaveFrom = s.Rect.anchoredPosition;
                s.LeaveTo = to + new Vector2(i * 3f, -i * 2f);
                s.LeaveRot = s.Rect.localEulerAngles.z;
                s.LeaveT = 0f;
                s.Travel = 1f;
                s.Flip = 1f;
                s.Face.enabled = true;
                s.Face.name = "Card_Leaving";
                _leaving.Add(s);
            }
            _slots.Clear();
            ResetOutcome();
        }

        /// <summary>Drops cards past <paramref name="count"/> instantly — used when a split
        /// takes the second card of a pair away to its own hand.</summary>
        public void Truncate(int count)
        {
            for (int i = _slots.Count - 1; i >= Mathf.Max(0, count); i--)
            {
                Release(_slots[i]);
                _slots.RemoveAt(i);
            }
            Reflow();
        }

        private void ResetOutcome()
        {
            _outcomeDimmed = false;
            _glowTarget = 0f;
            _glowPulse = 0f;
            UpdateTargets();
        }

        // =====================================================================
        //  Rendering
        // =====================================================================

        /// <summary>
        /// Draws <paramref name="cards"/> left to right, dealing in any that are new and
        /// turning over any whose face changed.
        /// </summary>
        /// <param name="cards">The hand to display.</param>
        /// <param name="faceDownFrom">
        /// Index from which cards are drawn face down (the dealer's hole card during the
        /// player's turn). Negative means every card is face up.
        /// </param>
        public void Render(IReadOnlyList<Card> cards, int faceDownFrom = -1)
        {
            if (_cardRoot == null || _cardPrefab == null || _library == null)
            {
                Debug.LogWarning($"[HandView] '{name}' is not fully wired; nothing to draw.");
                return;
            }
            EnsureLayers();

            int count = cards?.Count ?? 0;

            // Only the split path removes cards; anything else asking for fewer is a new
            // hand being drawn over an old one, so drop the extras outright.
            if (count < _slots.Count) Truncate(count);

            bool reduced = MotionPrefs.Reduced;
            int pending = 0;
            foreach (CardSlot s in _slots)
                if (s.Travel < 1f) pending++;

            for (int i = 0; i < count; i++)
            {
                bool faceDown = faceDownFrom >= 0 && i >= faceDownFrom;
                Sprite wanted = faceDown ? _library.Back : _library.GetFace(cards[i]);

                if (i >= _slots.Count)
                {
                    CardSlot slot = Acquire();
                    slot.Face.name = $"Card_{i:00}";
                    _slots.Add(slot);
                    StartDeal(slot, i, count, wanted, faceDown, pending++, reduced);
                }
                else
                {
                    CardSlot slot = _slots[i];
                    slot.Face.name = $"Card_{i:00}";
                    if (slot.Target != wanted) StartFlip(slot, wanted, reduced);
                }
            }

            Reflow();
            Restack();
            ApplyEmphasis();
        }

        private void StartDeal(CardSlot slot, int index, int count, Sprite wanted, bool faceDown,
            int queuePosition, bool reduced)
        {
            bool transfer = _hasTransfer;
            Vector2 origin = transfer
                ? (Vector2)_cardRoot.InverseTransformPoint(_transferWorld)
                : _hasDealOrigin ? (Vector2)_cardRoot.InverseTransformPoint(_dealOriginWorld) : _dealFrom;
            _hasTransfer = false;

            slot.Target = wanted;
            slot.From = origin;
            slot.To = SlotPosition(index, count);
            slot.ToRot = SlotRotation(index, count);
            slot.FromRot = slot.ToRot + (transfer ? 0f : 14f);
            slot.Travel = 0f;
            slot.Land = 0f;
            slot.Flip = 1f;
            slot.DealCueRaised = false;
            // A transferred split card is already on the table: it moves at once, face up.
            slot.Delay = transfer ? 0f : queuePosition * MotionPrefs.Duration(_dealStagger);

            float side = index % 2 == 0 ? 1f : -1f;
            slot.Arc = reduced || transfer ? 0f : _dealArc * side;
            slot.Spin = reduced || transfer ? 0f : _dealSpin * side;
            slot.FlipsInFlight = !reduced && !transfer && !faceDown;

            slot.Face.sprite = slot.FlipsInFlight ? _library.Back : wanted;
            // A queued card waits in the shoe unseen until its flight begins.
            bool visible = slot.Delay <= 0f;
            slot.Face.enabled = visible;
            if (slot.Shadow != null) slot.Shadow.enabled = visible;
            if (visible) RaiseDealCue(slot);
            ApplyFlight(slot);
        }

        private void StartFlip(CardSlot slot, Sprite wanted, bool reduced)
        {
            bool reveal = slot.Target == _library.Back;
            slot.FlipFrom = slot.Target;
            slot.Target = wanted;

            if (slot.Travel < 1f)
            {
                // Still in the air: it will simply land showing the new face.
                if (!slot.FlipsInFlight) slot.Face.sprite = wanted;
                return;
            }

            slot.Flip = 0f;
            slot.FlipDuration = MotionPrefs.Duration(_flipDuration * (reveal ? _holeCardFlipSlowdown : 1f));
            UiCues.Raise(UiCue.CardFlip);
        }

        private void RaiseDealCue(CardSlot slot)
        {
            if (slot.DealCueRaised) return;
            slot.DealCueRaised = true;
            UiCues.Raise(UiCue.CardDeal);
        }

        // =====================================================================
        //  Layout
        // =====================================================================

        private float Step(int count)
        {
            float step = _cardSize.x * _spacing;
            if (count > 1 && _maxSpan > 0f && step * (count - 1) > _maxSpan)
                step = _maxSpan / (count - 1);
            return step;
        }

        private float Span(int count) => count > 1 ? Step(count) * (count - 1) : 0f;

        private Vector2 SlotPosition(int i, int count) =>
            new Vector2(-Span(count) * 0.5f + Step(count) * i, 0f);

        private float SlotRotation(int i, int count)
        {
            // Fan from +half to -half across the hand plus a deterministic per-slot tilt.
            // Deterministic matters: a card must not twitch when the hand re-renders.
            float t = count > 1 ? i / (float)(count - 1) - 0.5f : 0f;
            return -t * _fanDegrees + _jitterDegrees * Mathf.Sin(i * 12.9898f);
        }

        /// <summary>Retargets every card for the current count and size; settled cards ease there.</summary>
        private void Reflow()
        {
            int count = _slots.Count;
            for (int i = 0; i < count; i++)
            {
                CardSlot s = _slots[i];
                s.To = SlotPosition(i, count);
                s.ToRot = SlotRotation(i, count);
                s.Rect.sizeDelta = _cardSize;
                if (s.ShadowRect != null) s.ShadowRect.sizeDelta = _cardSize + Vector2.one * _shadowSpread;
            }
        }

        /// <summary>Later cards draw over earlier ones; collected cards go underneath everything.</summary>
        private void Restack()
        {
            foreach (CardSlot s in _leaving)
            {
                if (s.ShadowRect != null) s.ShadowRect.SetAsFirstSibling();
                s.Rect.SetAsFirstSibling();
            }
            foreach (CardSlot s in _slots)
            {
                if (s.ShadowRect != null) s.ShadowRect.SetAsLastSibling();
                s.Rect.SetAsLastSibling();
            }
        }

        // =====================================================================
        //  Animation
        // =====================================================================

        private void Update()
        {
            float dt = Time.deltaTime;

            if (!Mathf.Approximately(_scale, _scaleTarget) || !Mathf.Approximately(_dim, _dimTarget))
            {
                _scale = Ease.Damp(_scale, _scaleTarget, 9f, dt);
                _dim = Ease.Damp(_dim, _dimTarget, 9f, dt);
                if (Mathf.Abs(_scale - _scaleTarget) < 0.001f) _scale = _scaleTarget;
                if (Mathf.Abs(_dim - _dimTarget) < 0.001f) _dim = _dimTarget;
                ApplyEmphasis();
            }

            UpdateGlow(dt);

            foreach (CardSlot s in _slots)
            {
                if (s.Travel < 1f)
                {
                    if (s.Delay > 0f)
                    {
                        s.Delay -= dt;
                        if (s.Delay > 0f) continue;
                        s.Face.enabled = true;
                        if (s.Shadow != null) s.Shadow.enabled = true;
                        RaiseDealCue(s);
                    }

                    s.Travel = Mathf.Min(1f, s.Travel + dt / Mathf.Max(0.01f, MotionPrefs.Duration(_dealDuration)));
                    if (s.Travel >= 1f)
                    {
                        s.Land = MotionPrefs.Reduced ? 0f : 1f;
                        s.Face.sprite = s.Target;
                    }
                    ApplyFlight(s);
                }
                else if (s.Flip < 1f)
                {
                    s.Flip = Mathf.Min(1f, s.Flip + dt / Mathf.Max(0.01f, s.FlipDuration));
                    ApplyFlip(s);
                }
                else
                {
                    if (s.Land > 0f) s.Land = Mathf.Max(0f, s.Land - dt / 0.26f);
                    // Settled: ease toward the slot so reflows (a card added, a resize) glide.
                    Vector2 p = s.Rect.anchoredPosition;
                    if ((p - s.To).sqrMagnitude > 0.01f || s.Land > 0f)
                        ApplyRest(s, dt);
                }
            }

            for (int i = _leaving.Count - 1; i >= 0; i--)
            {
                CardSlot s = _leaving[i];
                s.LeaveT = Mathf.Min(1f, s.LeaveT + dt / Mathf.Max(0.01f, _collectDuration));
                float e = Ease.InOutCubic(s.LeaveT);
                s.Rect.anchoredPosition = Vector2.LerpUnclamped(s.LeaveFrom, s.LeaveTo, e);
                s.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(s.LeaveRot, s.LeaveRot + 24f, e));
                float scale = Mathf.Lerp(1f, _dealStartScale, e);
                s.Rect.localScale = new Vector3(scale, scale, 1f);
                float alpha = 1f - Mathf.Clamp01((s.LeaveT - 0.45f) / 0.55f);
                SetAlpha(s, alpha);
                if (s.ShadowRect != null)
                {
                    s.ShadowRect.anchoredPosition = s.Rect.anchoredPosition + _shadowOffset;
                    s.ShadowRect.localRotation = s.Rect.localRotation;
                    s.ShadowRect.localScale = s.Rect.localScale;
                }
                if (s.LeaveT >= 1f)
                {
                    _leaving.RemoveAt(i);
                    Release(s);
                }
            }
        }

        /// <summary>Places a card a fraction of the way along its flight from the shoe.</summary>
        private void ApplyFlight(CardSlot s)
        {
            float e = Ease.OutCubic(s.Travel);

            // Straight line, bowed sideways: sin() is zero at both ends, so the card leaves
            // the shoe and lands on its mark exactly while curving in between.
            Vector2 travel = s.To - s.From;
            Vector2 perpendicular = travel.sqrMagnitude > 0.01f ? new Vector2(-travel.y, travel.x).normalized : Vector2.zero;
            float bow = Mathf.Sin(e * Mathf.PI);
            s.Rect.anchoredPosition = Vector2.LerpUnclamped(s.From, s.To, e) + perpendicular * (s.Arc * bow);

            float z = Mathf.LerpUnclamped(s.FromRot, s.ToRot, e) + s.Spin * (1f - e);

            // Face-up cards turn over during the flight, finishing well before touchdown.
            float yaw = 0f;
            if (s.FlipsInFlight)
            {
                float turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.78f, s.Travel));
                yaw = 180f * (1f - turn);
                s.Face.sprite = yaw > 90f ? _library.Back : s.Target;
                if (yaw > 90f) yaw -= 180f; // keep the back readable rather than mirrored
            }
            s.Rect.localRotation = Quaternion.Euler(0f, yaw, z);

            float lift = MotionPrefs.Amount(_dealLift) * bow;
            float scale = Mathf.LerpUnclamped(MotionPrefs.Reduced ? 1f : _dealStartScale, 1f, e) + lift;
            s.Rect.localScale = new Vector3(scale, scale, 1f);

            float height = bow * 0.8f + (1f - e) * 0.6f;
            ApplyShadow(s, height, scale);
        }

        /// <summary>A reveal: the card turns about its vertical axis, lifting as it goes.</summary>
        private void ApplyFlip(CardSlot s)
        {
            float t = Mathf.SmoothStep(0f, 1f, s.Flip);
            float yaw = 180f * (1f - t);
            bool showingOld = yaw > 90f;
            s.Face.sprite = showingOld ? s.FlipFrom : s.Target;
            if (showingOld) yaw -= 180f;

            s.Rect.anchoredPosition = s.To;
            s.Rect.localRotation = Quaternion.Euler(0f, yaw, s.ToRot);
            float lift = MotionPrefs.Amount(_flipLift) * Mathf.Sin(s.Flip * Mathf.PI);
            float scale = 1f + lift;
            s.Rect.localScale = new Vector3(scale, scale, 1f);
            ApplyShadow(s, lift / Mathf.Max(0.001f, _flipLift), scale);

            if (s.Flip >= 1f)
            {
                s.Face.sprite = s.Target;
                s.Rect.localRotation = Quaternion.Euler(0f, 0f, s.ToRot);
            }
        }

        private void ApplyRest(CardSlot s, float dt)
        {
            Vector2 p = MotionPrefs.Reduced ? s.To : Ease.Damp(s.Rect.anchoredPosition, s.To, 12f, dt);
            if ((p - s.To).sqrMagnitude < 0.01f) p = s.To;
            s.Rect.anchoredPosition = p;
            float z = Mathf.LerpAngle(s.Rect.localEulerAngles.z, s.ToRot, MotionPrefs.Reduced ? 1f : 1f - Mathf.Exp(-12f * dt));
            s.Rect.localRotation = Quaternion.Euler(0f, 0f, z);

            // Touchdown: a tiny press into the felt and back, which reads as weight.
            float settle = s.Land > 0f ? -0.018f * Mathf.Sin(s.Land * Mathf.PI) : 0f;
            float scale = 1f + settle;
            s.Rect.localScale = new Vector3(scale, scale, 1f);
            ApplyShadow(s, 0f, scale);
        }

        private void ApplyShadow(CardSlot s, float height, float scale)
        {
            if (s.ShadowRect == null) return;
            // The shadow drifts away while the card is airborne and tucks in as it lands —
            // that separation is what sells the height.
            s.ShadowRect.anchoredPosition = s.Rect.anchoredPosition + _shadowOffset * (1f + height * 2.2f);
            s.ShadowRect.localRotation = Quaternion.Euler(0f, 0f, s.Rect.localEulerAngles.z);
            s.ShadowRect.localScale = new Vector3(scale, scale, 1f);
            Color c = _shadowColor;
            c.a *= Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(height)) * Mathf.Lerp(0.5f, 1f, _dim);
            s.Shadow.color = c;
        }

        private void ApplyEmphasis()
        {
            if (_cardRoot != null) _cardRoot.localScale = new Vector3(_scale, _scale, 1f);
            var tint = new Color(_dim, _dim, _dim, 1f);
            foreach (CardSlot s in _slots)
            {
                s.Face.color = tint;
                if (s.Shadow != null)
                {
                    Color c = _shadowColor;
                    c.a *= Mathf.Lerp(0.5f, 1f, _dim);
                    s.Shadow.color = c;
                }
            }
        }

        private void UpdateGlow(float dt)
        {
            if (_glow == null) return;

            _glowAlpha = MotionPrefs.Reduced ? _glowTarget : Ease.Damp(_glowAlpha, _glowTarget, 5f, dt);
            if (_glowPulse > 0f) _glowPulse = Mathf.Max(0f, _glowPulse - dt * 0.45f);
            float a = Mathf.Clamp01(_glowAlpha + _glowPulse * Mathf.Sin(_glowPulse / 0.35f * Mathf.PI));

            bool on = a > 0.002f && _slots.Count > 0;
            if (_glow.enabled != on) _glow.enabled = on;
            if (!on) return;

            Color c = _glowColor;
            c.a = a;
            _glow.color = c;
            // The light pools around the whole hand, however many cards it holds.
            var size = new Vector2((Width + _cardSize.x) * 1.6f, _cardSize.y * 2.4f);
            _glow.rectTransform.sizeDelta = size * _scale;
        }

        private static void SetAlpha(CardSlot s, float alpha)
        {
            Color c = s.Face.color;
            c.a = alpha;
            s.Face.color = c;
            if (s.Shadow != null)
            {
                Color sc = s.Shadow.color;
                sc.a = Mathf.Min(sc.a, alpha * 0.5f);
                s.Shadow.color = sc;
            }
        }

        // =====================================================================
        //  Pool
        // =====================================================================

        private CardSlot Acquire()
        {
            CardSlot slot;
            if (_free.Count > 0)
            {
                slot = _free[_free.Count - 1];
                _free.RemoveAt(_free.Count - 1);
            }
            else
            {
                slot = new CardSlot();
                if (_shadowSprite != null)
                {
                    var shadowGo = new GameObject("CardShadow", typeof(RectTransform));
                    shadowGo.transform.SetParent(_shadowLayer, false);
                    slot.Shadow = shadowGo.AddComponent<Image>();
                    slot.Shadow.sprite = _shadowSprite;
                    slot.Shadow.type = Image.Type.Sliced;
                    slot.Shadow.raycastTarget = false;
                    slot.ShadowRect = (RectTransform)shadowGo.transform;
                }

                // worldPositionStays:false — the default keeps world position, which drags
                // the canvas scale into the child's local transform.
                slot.Face = Instantiate(_cardPrefab, _faceLayer, false);
                slot.Face.raycastTarget = false;
                slot.Face.preserveAspect = false;
                slot.Rect = (RectTransform)slot.Face.transform;
            }

            slot.Face.gameObject.SetActive(true);
            slot.Face.color = new Color(_dim, _dim, _dim, 1f);
            slot.Rect.localScale = Vector3.one;
            slot.Rect.sizeDelta = _cardSize;
            if (slot.Shadow != null)
            {
                slot.Shadow.gameObject.SetActive(true);
                slot.ShadowRect.sizeDelta = _cardSize + Vector2.one * _shadowSpread;
            }
            return slot;
        }

        private void Release(CardSlot slot)
        {
            slot.Travel = 1f;
            slot.Flip = 1f;
            slot.Land = 0f;
            slot.Delay = 0f;
            slot.Target = null;
            if (slot.Face != null) slot.Face.gameObject.SetActive(false);
            if (slot.Shadow != null) slot.Shadow.gameObject.SetActive(false);
            if (!_free.Contains(slot)) _free.Add(slot);
        }
    }
}
