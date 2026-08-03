using UnityEngine;

namespace BlackjackGame.Config
{
    /// <summary>
    /// Central, designer-editable configuration. Create one via
    /// Assets ▸ Create ▸ Blackjack ▸ Game Config and wire it into AppManager so no
    /// gameplay value is hardcoded in scripts.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Blackjack/Game Config", order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Table")]
        [Tooltip("Selectable chip denominations for betting.")]
        public int[] ChipDenominations = { 10, 25, 100, 500, 1000 };

        [Min(1)] public int MinBet = 10;
        [Min(1)] public int MaxBet = 10000;

        [Header("Rules")]
        [Tooltip("Which rule variant to load at startup.")]
        public RuleVariant DefaultRuleVariant = RuleVariant.Classic;
    }

    public enum RuleVariant
    {
        Classic,
        European
    }
}
