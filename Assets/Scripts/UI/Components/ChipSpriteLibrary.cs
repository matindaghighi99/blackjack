using System;
using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Chip artwork per denomination: a top-down face for the chip rack and a side view for
    /// stacks on the felt. The value itself is drawn live (TextMeshPro), so the art never
    /// disagrees with <c>GameConfig.ChipDenominations</c>; a denomination without art of its
    /// own borrows the nearest lower colourway.
    ///
    /// Populated automatically by <c>Blackjack ▸ Build UI Scenes</c> from Assets/Art/Chips.
    /// </summary>
    [CreateAssetMenu(fileName = "ChipSpriteLibrary", menuName = "Blackjack/Chip Sprite Library", order = 3)]
    public sealed class ChipSpriteLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public int Value;
            public Sprite Face;
            public Sprite Side;
            [Tooltip("Light chips (ivory, ochre, pearl) carry their value in dark ink.")]
            public bool DarkLabel;
        }

        [Tooltip("One entry per colourway, any order.")]
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        [Tooltip("Soft contact shadow placed under a chip or stack.")]
        [SerializeField] private Sprite _shadow;

        public Sprite Shadow => _shadow;

        public bool IsComplete
        {
            get
            {
                if (_shadow == null || _entries == null || _entries.Length == 0) return false;
                foreach (Entry e in _entries)
                    if (e.Face == null || e.Side == null) return false;
                return true;
            }
        }

        /// <summary>The colourway for a value: exact match, else the nearest lower one, else the smallest.</summary>
        public Entry Get(int value)
        {
            Entry best = default;
            bool found = false;
            Entry smallest = default;
            bool haveSmallest = false;

            if (_entries != null)
            {
                foreach (Entry e in _entries)
                {
                    if (e.Face == null) continue;
                    if (e.Value == value) return e;
                    if (e.Value < value && (!found || e.Value > best.Value))
                    {
                        best = e;
                        found = true;
                    }
                    if (!haveSmallest || e.Value < smallest.Value)
                    {
                        smallest = e;
                        haveSmallest = true;
                    }
                }
            }
            return found ? best : smallest;
        }
    }
}
