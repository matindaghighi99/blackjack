using System;
using System.Collections.Generic;
using BlackjackGame.Blackjack;
using BlackjackGame.Economy;
using BlackjackGame.Player;
using BlackjackGame.UI.Components;
using BlackjackGame.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackjackGame.Core
{
    /// <summary>Named scenes, kept in one place to avoid magic strings.</summary>
    public static class SceneNames
    {
        public const string MainMenu = "MainMenu";
        public const string Game = "Game";
        public const string Store = "Store";
    }

    /// <summary>
    /// Orchestrates a single blackjack table: takes the player's bet, runs a round via
    /// <see cref="BlackjackEngine"/>, and settles chips against <see cref="ChipManager"/>.
    /// The engine stays pure; this class is the Unity-facing coordinator the table UI talks to.
    /// </summary>
    public sealed class GameManager : MonoSingleton<GameManager>
    {
        protected override bool Persistent => false; // one per Game scene

        private BlackjackEngine _engine;
        private ChipManager _chips;
        private PlayerProfile _profile;
        private int _currentBet;

        /// <summary>Raised when a round is fully settled, with per-hand results.</summary>
        public event Action<IReadOnlyList<HandResult>> OnRoundComplete;
        public event Action<long> OnBalanceChanged;

        public BlackjackEngine Engine => _engine;
        public long Balance => _chips?.Balance ?? 0;

        protected override void OnSingletonAwake()
        {
            if (!AppManager.Exists)
            {
                Debug.LogError("[GameManager] AppManager missing. Start from the MainMenu scene.");
                return;
            }

            _chips = AppManager.Instance.Chips;
            _profile = AppManager.Instance.Profile;
            _chips.OnBalanceChanged += HandleBalanceChanged;
        }

        /// <summary>Places a bet (debiting chips) and deals a new round. Returns false if unaffordable.</summary>
        public bool PlaceBetAndDeal(int bet)
        {
            if (_engine != null && _engine.Phase != RoundPhase.Settled && _engine.Phase != RoundPhase.Idle)
            {
                Debug.LogWarning("[GameManager] Round already in progress.");
                return false;
            }

            if (!_chips.TrySpend(bet))
            {
                Debug.Log("[GameManager] Not enough chips for this bet.");
                return false;
            }

            _currentBet = bet;
            _engine = new BlackjackEngine(AppManager.Instance.CreateRuleSet());
            _engine.OnRoundSettled += HandleRoundSettled;
            _engine.StartRound(bet);
            return true;
        }

        // ---- Player action pass-throughs (UI calls these) ----
        public void Hit() => _engine?.Hit();
        public void Stand() => _engine?.Stand();
        public void DoubleDown()
        {
            // Doubling requires an extra bet to be reserved from the balance.
            if (_engine != null && _engine.CanDouble && _chips.TrySpend(_currentBet))
                _engine.DoubleDown();
        }
        public void Split()
        {
            if (_engine != null && _engine.CanSplit && _chips.TrySpend(_currentBet))
                _engine.Split();
        }
        public void Surrender() => _engine?.Surrender();

        private void HandleRoundSettled(IReadOnlyList<HandResult> results)
        {
            long net = 0;
            bool anyWin = false;
            bool anyBlackjack = false;

            foreach (var r in results)
            {
                // Return the original stake on non-losses, then apply the net delta.
                switch (r.Outcome)
                {
                    case HandOutcome.PlayerBust:
                    case HandOutcome.DealerWin:
                        // Stake already lost at bet time; nothing returned.
                        break;
                    case HandOutcome.Surrendered:
                        _chips.Add(r.Hand.Bet / 2); // half stake back
                        break;
                    case HandOutcome.Push:
                        _chips.Add(r.Hand.Bet); // stake returned
                        break;
                    default: // wins & blackjack
                        _chips.Add(r.Hand.Bet + r.NetChips); // stake back + winnings
                        anyWin = true;
                        break;
                }

                if (r.Outcome == HandOutcome.PlayerBlackjack) anyBlackjack = true;
                net += r.NetChips;
            }

            _profile.RecordRound(anyWin, anyBlackjack);
            OnRoundComplete?.Invoke(results);
        }

        private void HandleBalanceChanged(long balance) => OnBalanceChanged?.Invoke(balance);

        /// <summary>Scene changes go through the fader so screens hand over with a
        /// dip to black instead of a hard cut. Falls back to a direct load when no
        /// fader exists (tests, batch mode).</summary>
        public void GoToScene(string sceneName) => SceneFader.TransitionTo(sceneName);

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_chips != null) _chips.OnBalanceChanged -= HandleBalanceChanged;
            if (_engine != null) _engine.OnRoundSettled -= HandleRoundSettled;
        }
    }
}
