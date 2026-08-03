using System;
using BlackjackGame.Player;

namespace BlackjackGame.Economy
{
    /// <summary>
    /// Single source of truth for the player's virtual chip balance. All chip mutations
    /// (bets, payouts, rewards, purchases) funnel through here so balance changes are
    /// centralised, validated, and persisted.
    ///
    /// Virtual chips only — no cash value, no payouts. Social-casino model.
    /// </summary>
    public sealed class ChipManager
    {
        private readonly PlayerProfile _profile;

        /// <summary>Fired whenever the balance changes, with the new total.</summary>
        public event Action<long> OnBalanceChanged;

        public ChipManager(PlayerProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public long Balance => _profile.Data.Chips;

        public bool CanAfford(long amount) => amount >= 0 && Balance >= amount;

        /// <summary>Credits chips (winnings, rewards, purchases). Ignores non-positive amounts.</summary>
        public void Add(long amount)
        {
            if (amount <= 0) return;
            _profile.Data.Chips += amount;
            Persist();
        }

        /// <summary>
        /// Debits chips (placing a bet). Returns false and changes nothing if the player
        /// cannot afford it — callers must check the result before committing to a bet.
        /// </summary>
        public bool TrySpend(long amount)
        {
            if (amount <= 0 || !CanAfford(amount)) return false;
            _profile.Data.Chips -= amount;
            Persist();
            return true;
        }

        /// <summary>Applies a net round result (positive = won, negative = lost stake already debited).</summary>
        public void ApplyNet(long net)
        {
            _profile.Data.Chips += net;
            if (_profile.Data.Chips < 0) _profile.Data.Chips = 0;
            Persist();
        }

        private void Persist()
        {
            _profile.Save();
            OnBalanceChanged?.Invoke(Balance);
        }
    }
}
