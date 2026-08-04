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

        private readonly List<Image> _pool = new List<Image>();

        /// <summary>How many cards are currently displayed. Handy for tests.</summary>
        public int VisibleCardCount { get; private set; }

        /// <summary>Hides every card without destroying the pool.</summary>
        public void Clear()
        {
            foreach (Image card in _pool)
                if (card != null) card.gameObject.SetActive(false);
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
                if (i >= count)
                {
                    card.gameObject.SetActive(false);
                    continue;
                }

                bool faceDown = faceDownFrom >= 0 && i >= faceDownFrom;
                card.gameObject.SetActive(true);
                card.sprite = faceDown ? _library.Back : _library.GetFace(cards[i]);

                var rect = (RectTransform)card.transform;
                rect.sizeDelta = _cardSize;
                rect.anchoredPosition = new Vector2(-span * 0.5f + step * i, 0f);

                // Fan from -half to +half across the hand.
                float t = count > 1 ? (i / (float)(count - 1)) - 0.5f : 0f;
                rect.localRotation = Quaternion.Euler(0f, 0f, -t * _fanDegrees);
                rect.SetSiblingIndex(i);
            }

            VisibleCardCount = count;
        }

        private void EnsurePool(int required)
        {
            while (_pool.Count < required)
            {
                // worldPositionStays:false — the default overload keeps world position,
                // which drags the canvas scale into the child's local transform.
                Image card = Instantiate(_cardPrefab, _cardRoot, false);
                card.name = $"Card_{_pool.Count:00}";
                card.raycastTarget = false;
                card.transform.localScale = Vector3.one;
                _pool.Add(card);
            }
        }
    }
}
