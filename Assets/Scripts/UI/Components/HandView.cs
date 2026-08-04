using System.Collections.Generic;
using BlackjackGame.Blackjack.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Renders a hand as overlapping, slightly fanned card sprites.
    ///
    /// Card objects are pooled and reused rather than destroyed between rounds — a hand
    /// changes several times per round and churning GameObjects would generate needless
    /// garbage on mobile. Purely presentational: it reads the engine's cards and never
    /// mutates them.
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

        [Tooltip("Horizontal step between cards, as a fraction of card width. " +
                 "Below 1 the cards overlap.")]
        [Range(0.30f, 1.20f)]
        [SerializeField] private float _spacing = 0.68f;

        [Tooltip("Total fan spread across the hand, in degrees.")]
        [Range(0f, 20f)]
        [SerializeField] private float _fanDegrees = 5f;

        [Header("Depth")]
        [Tooltip("Drop shadow behind each card. Leave empty for no shadow.")]
        [SerializeField] private Sprite _shadowSprite;
        [SerializeField] private Vector2 _shadowOffset = new Vector2(6f, -10f);
        [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0f, 0.45f);
        [Tooltip("Extra size the shadow adds around the card.")]
        [SerializeField] private float _shadowSpread = 14f;

        [Tooltip("Random tilt per card, in degrees, so a hand doesn't look mechanical.")]
        [Range(0f, 8f)]
        [SerializeField] private float _jitterDegrees = 2.2f;

        [Header("Motion")]
        [Tooltip("Where new cards fly in from, relative to the hand — the dealing shoe.")]
        [SerializeField] private Vector2 _dealFrom = new Vector2(520f, 620f);
        [Tooltip("Seconds for a card to travel from the shoe to its place.")]
        [SerializeField] private float _dealDuration = 0.26f;
        [Tooltip("Delay between consecutive cards in the same deal.")]
        [SerializeField] private float _dealStagger = 0.09f;
        [Tooltip("Seconds for a card to turn over when it is revealed.")]
        [SerializeField] private float _flipDuration = 0.24f;

        private readonly List<Image> _pool = new List<Image>();
        private readonly List<Image> _shadows = new List<Image>();
        private readonly List<CardMotion> _motion = new List<CardMotion>();

        /// <summary>Per-card animation state. Driven from Update, not coroutines.</summary>
        private sealed class CardMotion
        {
            public Vector2 FromPos, ToPos;
            public float FromRot, ToRot;
            /// <summary>Seconds until this card starts moving.</summary>
            public float Delay;
            /// <summary>0..1 along the deal; 1 means settled.</summary>
            public float Travel = 1f;

            /// <summary>0..1 through a flip; 1 means no flip in progress.</summary>
            public float Flip = 1f;
            public Sprite FlipTo;
            public bool FlipSwapped;

            public bool Busy => Travel < 1f || Flip < 1f;
        }

        /// <summary>True while any card is still moving or turning over.</summary>
        public bool IsAnimating
        {
            get
            {
                // Only active cards count. A hidden card's motion state is stale by
                // definition, and letting it register here leaves IsAnimating stuck true.
                for (int i = 0; i < _motion.Count; i++)
                    if (_motion[i] != null && _motion[i].Busy && _pool[i].gameObject.activeSelf)
                        return true;
                return false;
            }
        }

        /// <summary>How many cards are currently displayed. Handy for tests.</summary>
        public int VisibleCardCount { get; private set; }

        /// <summary>Hides every card without destroying the pool.</summary>
        public void Clear()
        {
            foreach (Image card in _pool)
                if (card != null) card.gameObject.SetActive(false);
            foreach (Image shadow in _shadows)
                if (shadow != null) shadow.gameObject.SetActive(false);
            foreach (CardMotion m in _motion)
            {
                if (m == null) continue;
                m.Travel = 1f;
                m.Flip = 1f;
            }
            VisibleCardCount = 0;
        }

        /// <summary>
        /// Draws <paramref name="cards"/> left to right.
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

            int count = cards?.Count ?? 0;
            EnsurePool(count);

            float step = _cardSize.x * _spacing;
            float span = count > 1 ? step * (count - 1) : 0f;

            for (int i = 0; i < _pool.Count; i++)
            {
                Image card = _pool[i];
                Image shadow = _shadows[i];
                CardMotion motion = _motion[i];

                if (i >= count)
                {
                    // Reset motion as well as hiding: Update skips inactive cards, so a
                    // card hidden mid-deal would keep Travel < 1 for ever.
                    motion.Travel = 1f;
                    motion.Flip = 1f;
                    card.gameObject.SetActive(false);
                    if (shadow != null) shadow.gameObject.SetActive(false);
                    continue;
                }

                bool faceDown = faceDownFrom >= 0 && i >= faceDownFrom;
                Sprite wanted = faceDown ? _library.Back : _library.GetFace(cards[i]);

                bool isNew = !card.gameObject.activeSelf;
                card.gameObject.SetActive(true);

                var rect = (RectTransform)card.transform;
                rect.sizeDelta = _cardSize;

                // Fan from -half to +half across the hand, plus a deterministic per-slot
                // tilt. Deterministic matters: a card must not jump when the hand
                // re-renders, which happens on every Refresh.
                float t = count > 1 ? (i / (float)(count - 1)) - 0.5f : 0f;
                float jitter = _jitterDegrees * Mathf.Sin(i * 12.9898f);

                motion.ToPos = new Vector2(-span * 0.5f + step * i, 0f);
                motion.ToRot = -t * _fanDegrees + jitter;

                if (isNew)
                {
                    // Fresh card: fly it in from the shoe, staggered behind the ones
                    // already on their way.
                    motion.FromPos = _dealFrom;
                    motion.FromRot = motion.ToRot + 18f;
                    motion.Delay = CountPending() * _dealStagger;
                    motion.Travel = 0f;
                    card.sprite = wanted;
                    ApplyTransform(i, 0f);
                }
                else if (motion.Travel >= 1f)
                {
                    // Already settled — just keep it in place unless the hand reflowed.
                    ApplyTransform(i, 1f);
                }

                // Turn the card over rather than swapping the sprite outright. Guard
                // against restarting a flip that is already heading for the same face —
                // Render runs on every Refresh, which would otherwise reset it each time.
                bool alreadyFlippingToWanted = motion.Flip < 1f && motion.FlipTo == wanted;
                if (card.sprite != wanted && !isNew && !alreadyFlippingToWanted)
                {
                    motion.Flip = 0f;
                    motion.FlipTo = wanted;
                    motion.FlipSwapped = false;
                }

                if (shadow != null) shadow.gameObject.SetActive(true);
                if (shadow != null) ((RectTransform)shadow.transform).SetSiblingIndex(i);
                rect.SetSiblingIndex(_pool.Count + i);
            }

            VisibleCardCount = count;
        }

        private int CountPending()
        {
            int n = 0;
            foreach (CardMotion m in _motion)
                if (m != null && m.Travel < 1f) n++;
            return n;
        }

        private void Update()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                CardMotion m = _motion[i];
                if (m == null || !_pool[i].gameObject.activeSelf) continue;

                if (m.Travel < 1f)
                {
                    if (m.Delay > 0f) m.Delay -= Time.deltaTime;
                    else m.Travel = Mathf.Min(1f, m.Travel + Time.deltaTime / Mathf.Max(0.01f, _dealDuration));
                    ApplyTransform(i, m.Travel);
                }

                if (m.Flip < 1f)
                {
                    m.Flip = Mathf.Min(1f, m.Flip + Time.deltaTime / Mathf.Max(0.01f, _flipDuration));

                    // Swap the face at the halfway point, when the card is edge-on.
                    if (!m.FlipSwapped && m.Flip >= 0.5f)
                    {
                        _pool[i].sprite = m.FlipTo;
                        m.FlipSwapped = true;
                    }

                    float scaleX = Mathf.Abs(Mathf.Cos(m.Flip * Mathf.PI));
                    Vector3 s = _pool[i].transform.localScale;
                    _pool[i].transform.localScale = new Vector3(Mathf.Max(0.02f, scaleX), s.y, 1f);

                    if (m.Flip >= 1f)
                        _pool[i].transform.localScale = Vector3.one;
                }
            }
        }

        /// <summary>Places card <paramref name="i"/> a fraction <paramref name="k"/> along its deal.</summary>
        private void ApplyTransform(int i, float k)
        {
            CardMotion m = _motion[i];
            // Ease-out: cards decelerate into place, which reads as weight.
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f);

            var rect = (RectTransform)_pool[i].transform;
            rect.anchoredPosition = Vector2.LerpUnclamped(m.FromPos, m.ToPos, e);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(m.FromRot, m.ToRot, e));

            Image shadow = _shadows[i];
            if (shadow == null) return;
            var srect = (RectTransform)shadow.transform;
            srect.sizeDelta = _cardSize + Vector2.one * _shadowSpread;
            srect.anchoredPosition = rect.anchoredPosition + _shadowOffset;
            srect.localRotation = rect.localRotation;
        }

        private void EnsurePool(int required)
        {
            while (_pool.Count < required)
            {
                int index = _pool.Count;

                Image shadow = null;
                if (_shadowSprite != null)
                {
                    var shadowGo = new GameObject($"CardShadow_{index:00}", typeof(RectTransform));
                    shadowGo.transform.SetParent(_cardRoot, false);
                    shadow = shadowGo.AddComponent<Image>();
                    shadow.sprite = _shadowSprite;
                    shadow.color = _shadowColor;
                    shadow.raycastTarget = false;
                }
                _shadows.Add(shadow);

                // worldPositionStays:false — the default overload keeps world position,
                // which drags the canvas scale into the child's local transform.
                Image card = Instantiate(_cardPrefab, _cardRoot, false);
                card.name = $"Card_{index:00}";
                card.raycastTarget = false;
                card.transform.localScale = Vector3.one;
                card.gameObject.SetActive(false); // Render() activates it and deals it in
                _pool.Add(card);
                _motion.Add(new CardMotion());
            }
        }
    }
}
