using BlackjackGame.Blackjack.Cards;
using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Maps a <see cref="Card"/> to its sprite. Kept as an asset rather than a
    /// <c>Resources.Load</c> lookup so the references are explicit, survive renames, and
    /// get stripped correctly by the build pipeline.
    ///
    /// Populated automatically by <c>Blackjack ▸ Build UI Scenes</c> from Assets/Art/Cards.
    /// </summary>
    [CreateAssetMenu(fileName = "CardSpriteLibrary", menuName = "Blackjack/Card Sprite Library", order = 2)]
    public sealed class CardSpriteLibrary : ScriptableObject
    {
        /// <summary>Number of distinct faces in a French deck.</summary>
        public const int FaceCount = 52;

        [Tooltip("52 faces indexed by (int)Suit * 13 + ((int)Rank - 1).")]
        [SerializeField] private Sprite[] _faces = new Sprite[FaceCount];

        [SerializeField] private Sprite _back;

        public Sprite Back => _back;

        /// <summary>True when every face and the back are assigned.</summary>
        public bool IsComplete
        {
            get
            {
                if (_back == null || _faces == null || _faces.Length != FaceCount) return false;
                foreach (Sprite s in _faces)
                    if (s == null) return false;
                return true;
            }
        }

        /// <summary>Returns the face for a card, falling back to the back sprite if unset.</summary>
        public Sprite GetFace(Card card)
        {
            int index = IndexOf(card);
            if (_faces == null || index < 0 || index >= _faces.Length) return _back;
            return _faces[index] != null ? _faces[index] : _back;
        }

        /// <summary>Deterministic slot for a card. Matches the generator's file naming.</summary>
        public static int IndexOf(Card card) => ((int)card.Suit * 13) + ((int)card.Rank - 1);
    }
}
