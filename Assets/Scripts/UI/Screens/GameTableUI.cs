using System;
using System.Collections;
using System.Collections.Generic;
using BlackjackGame.Blackjack;
using BlackjackGame.Blackjack.Cards;
using BlackjackGame.Blackjack.Rules;
using BlackjackGame.Core;
using BlackjackGame.Economy;
using BlackjackGame.UI.Components;
using BlackjackGame.UI.Feedback;
using BlackjackGame.UI.Interaction;
using BlackjackGame.UI.Layout;
using BlackjackGame.UI.Presentation;
using BlackjackGame.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Screens
{
    /// <summary>
    /// The blackjack table: betting, the round, and the result — a projection of the engine.
    ///
    /// The screen moves through four states. <b>Betting</b>: chips are thrown onto the felt
    /// and the stake accumulates there (tap the stake, or CLEAR, to take it back; the last
    /// stake is re-placed after each round so DEAL alone repeats it). <b>Dealing</b>: the
    /// opening cards fly from the shoe; actions are shown but held until they land.
    /// <b>PlayerTurn</b>: the engine's CanHit / CanStand / CanDouble / CanSplit /
    /// CanSurrender flags decide what is enabled. <b>Settling</b>: the engine has already
    /// resolved the round, but the table tells it in order — hole card, each dealer draw,
    /// verdict, chips — so the answer never arrives before the story.
    ///
    /// Nothing here decides a rule or an amount. Totals come from the engine's hands,
    /// outcomes and nets from its HandResults, balances from ChipManager (via GameManager).
    /// </summary>
    public sealed class GameTableUI : MonoBehaviour
    {
        private enum TableState { Betting, Dealing, PlayerTurn, Settling }

        [Header("Layout")]
        [SerializeField] private TableLayoutDriver _layout;

        [Header("Betting")]
        [Tooltip("Chip rack, smallest to largest. Values come from GameConfig.ChipDenominations.")]
        [SerializeField] private ChipButton[] _chipButtons;
        [SerializeField] private ChipSpriteLibrary _chipLibrary;
        [SerializeField] private Button _dealButton;
        [SerializeField] private Button _clearButton;
        [Tooltip("The stake on the felt — grows as chips are tapped, settles with the round.")]
        [SerializeField] private BetChipView _betChip;
        [Tooltip("Button over the stake so the resting stack can be tapped clear.")]
        [SerializeField] private Button _betChipButton;

        [Header("Control rows")]
        [Tooltip("Chip rack + DEAL — shown between rounds.")]
        [SerializeField] private CanvasGroup _betRow;
        [Tooltip("Hit/Stand/Double/Split — shown while a round is live.")]
        [SerializeField] private CanvasGroup _actionRow;

        [Header("Actions")]
        [SerializeField] private Button _hitButton;
        [SerializeField] private Button _standButton;
        [SerializeField] private Button _doubleButton;
        [SerializeField] private Button _splitButton;
        [SerializeField] private Button _surrenderButton;
        [Tooltip("The extra stake a double would commit, shown under DOUBLE.")]
        [SerializeField] private TMP_Text _doubleCost;
        [SerializeField] private TMP_Text _splitCost;

        [Header("Hands")]
        [SerializeField] private HandView _dealerHandView;
        [SerializeField] private TotalBadge _dealerBadge;
        [Tooltip("Player hand seats. One is used per hand; splits use up to all of them.")]
        [SerializeField] private PlayerSeat[] _seats;
        [SerializeField] private RectTransform _shoe;
        [SerializeField] private RectTransform _discard;

        [Header("Felt")]
        [SerializeField] private CanvasGroup _feltPrint;
        [SerializeField] private TMP_Text _feltPayout;
        [SerializeField] private TMP_Text _feltDealerRule;
        [SerializeField] private TMP_Text _sessionLabel;
        [SerializeField] private ResultBanner _banner;

        [Header("Readouts")]
        [SerializeField] private TMP_Text _balanceLabel;
        [Tooltip("Rolls the balance instead of snapping it.")]
        [SerializeField] private CountRollup _balanceRollup;
        [SerializeField] private TMP_Text _betLabel;
        [SerializeField] private TMP_Text _lastLabel;
        [Tooltip("What is happening and what to do next — on the rail, above the controls.")]
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private LabelPunch _statusPunch;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;
        [Tooltip("The + by the balance — shortcut to the chip store.")]
        [SerializeField] private Button _addChipsButton;
        [Tooltip("Pulses on the + while the player is out of chips.")]
        [SerializeField] private PulsingDot _addChipsDot;

        [Header("Quick actions")]
        [SerializeField] private Button _giftButton;
        [SerializeField] private PulsingDot _giftDot;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _statsButton;
        [SerializeField] private SettingsPanel _settingsPanel;
        [SerializeField] private StatsPanel _statsPanel;

        [Header("Keyboard")]
        [SerializeField] private KeyboardNavigator _navigator;

        [Header("Pacing")]
        [Tooltip("Pause after the hole card turns, before the dealer draws.")]
        [SerializeField] private float _revealPause = 0.5f;
        [Tooltip("Pause between successive dealer draws.")]
        [SerializeField] private float _drawPause = 0.42f;
        [Tooltip("Minimum time the verdict holds before the bet row returns.")]
        [SerializeField] private float _resultHold = 0.9f;
        [SerializeField] private float _rowFade = 0.22f;

        private GameManager _game;
        private IRuleSet _rules;
        private TableState _state = TableState.Betting;

        /// <summary>The engine instance the views were last drawn for; a change means "new round".</summary>
        private BlackjackEngine _renderedEngine;
        /// <summary>Results delivered by <see cref="GameManager.OnRoundComplete"/> for the current round.</summary>
        private IReadOnlyList<HandResult> _lastResults;

        private int[] _denominations = Array.Empty<int>();
        private int _currentBet;
        private int _lastBet;
        private int _selectedChip = -1;

        private readonly Dictionary<Hand, PlayerSeat> _seatOf = new Dictionary<Hand, PlayerSeat>();
        private int _seatCount = -1;

        // The displayed balance is held while chips are on the felt: the engine settles
        // instantly, but the balance should only move when the chips physically arrive.
        private bool _trackingRound;
        private long _roundLow;
        private bool _holdBalance;

        private bool _hasLast;
        private long _lastNet;

        private float _betRowAlpha;
        private float _actionRowAlpha;
        private float _feltAlpha = 1f;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Start()
        {
            _game = GameManager.Instance;

            Wire(_dealButton, OnDeal);
            Wire(_clearButton, ClearBet);
            Wire(_betChipButton, ClearBet);
            Wire(_hitButton, OnHit);
            Wire(_standButton, OnStand);
            Wire(_doubleButton, OnDouble);
            Wire(_splitButton, OnSplit);
            Wire(_surrenderButton, OnSurrender);
            Wire(_backButton, () => Navigate(SceneNames.MainMenu));
            Wire(_addChipsButton, () => Navigate(SceneNames.Store));
            Wire(_giftButton, ClaimGift);
            if (_settingsButton != null && _settingsPanel != null) _settingsButton.onClick.AddListener(_settingsPanel.Show);
            if (_statsButton != null && _statsPanel != null) _statsButton.onClick.AddListener(_statsPanel.Show);

            if (_chipButtons != null)
            {
                for (int i = 0; i < _chipButtons.Length; i++)
                {
                    int index = i; // avoid closure capture bug
                    if (_chipButtons[i] != null) _chipButtons[i].Button.onClick.AddListener(() => OnChipTapped(index));
                }
            }

            foreach (PlayerSeat seat in Seats) seat.Vacate();

            if (_game == null)
            {
                // Opened without the app booted (Game scene played directly). Say so instead
                // of showing a dead table.
                SetStatus("START FROM THE MAIN MENU");
                SetRow(_betRow, false, true);
                SetRow(_actionRow, false, true);
                return;
            }

            _game.OnRoundComplete += HandleRoundComplete;
            _game.OnBalanceChanged += HandleBalanceChanged;
            if (AppManager.Exists && AppManager.Instance.Profile != null)
                AppManager.Instance.Profile.OnChanged += HandleProfileChanged;

            ConfigureFromConfig();

            if (_layout != null)
            {
                _layout.Changed += OnLayoutChanged;
                _layout.ApplyNow();
            }

            if (_balanceRollup != null) _balanceRollup.SnapTo(_game.Balance);
            RefreshBalance();
            EnterBetting(afterRound: false, instant: true);
            RefreshGiftDot();
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.OnRoundComplete -= HandleRoundComplete;
                _game.OnBalanceChanged -= HandleBalanceChanged;
            }
            if (AppManager.Exists && AppManager.Instance.Profile != null)
                AppManager.Instance.Profile.OnChanged -= HandleProfileChanged;
            if (_layout != null) _layout.Changed -= OnLayoutChanged;
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private IEnumerable<PlayerSeat> Seats
        {
            get
            {
                if (_seats == null) yield break;
                foreach (PlayerSeat s in _seats)
                    if (s != null) yield return s;
            }
        }

        /// <summary>Chip values, limits and rule wording, all from config and the rule set.</summary>
        private void ConfigureFromConfig()
        {
            var values = new List<int>();
            if (AppManager.Exists && AppManager.Instance.GameConfig != null &&
                AppManager.Instance.GameConfig.ChipDenominations != null)
            {
                foreach (int v in AppManager.Instance.GameConfig.ChipDenominations)
                    if (v > 0 && !values.Contains(v)) values.Add(v);
            }
            values.Sort();
            int rack = _chipButtons != null ? _chipButtons.Length : 0;
            // A short rack keeps the smallest chips, so the table minimum stays reachable.
            if (values.Count > rack) values.RemoveRange(rack, values.Count - rack);
            _denominations = values.ToArray();

            for (int i = 0; i < rack; i++)
            {
                if (_chipButtons[i] == null) continue;
                bool used = i < _denominations.Length;
                if (used) _chipButtons[i].Bind(_denominations[i], _chipLibrary);
                _chipButtons[i].gameObject.SetActive(used);
            }
            if (_layout != null) _layout.SetVisibleChipCount(_denominations.Length);
            if (_betChip != null) _betChip.SetDenominations(_denominations);

            _rules = AppManager.Exists ? AppManager.Instance.CreateRuleSet() : null;
            if (_feltPayout != null) _feltPayout.text = TableText.PayoutLine(_rules);
            if (_feltDealerRule != null) _feltDealerRule.text = TableText.DealerRuleLine(_rules);
            if (_sessionLabel != null) _sessionLabel.text = TableText.SessionLine(_rules, MinBet, TableMax);
        }

        // =====================================================================
        //  Layout
        // =====================================================================

        private void OnLayoutChanged(TableLayout layout)
        {
            Vector3 shoe = _shoe != null ? _shoe.position : transform.position;
            Vector3 discard = _discard != null ? _discard.position : transform.position;
            if (_dealerHandView != null)
            {
                _dealerHandView.SetDealOrigin(shoe);
                _dealerHandView.SetDiscardPoint(discard);
            }
            foreach (PlayerSeat seat in Seats)
            {
                seat.Hand.SetDealOrigin(shoe);
                seat.Hand.SetDiscardPoint(discard);
            }

            PlaceSeats(Mathf.Max(1, CurrentHandCount), instant: true);

            if (_betChip != null && _balanceLabel != null && _dealerHandView != null)
                _betChip.SetHomes(_balanceLabel.transform.position, _dealerHandView.transform.position);
            if (_statusPunch != null) _statusPunch.Rehome();
        }

        private int CurrentHandCount =>
            _game != null && _game.Engine != null && _renderedEngine == _game.Engine ? _game.Engine.PlayerHands.Count : 1;

        /// <summary>Positions the seats (and the betting spot) for <paramref name="count"/> hands.</summary>
        private void PlaceSeats(int count, bool instant)
        {
            if (_layout == null || _layout.Current == null) return;
            TableLayout l = _layout.Current;
            SeatLayout seats = l.Seats(count);
            _seatCount = count;

            BlackjackEngine engine = _renderedEngine;
            IReadOnlyList<Hand> hands = engine != null ? engine.PlayerHands : null;

            // Seats follow the engine's hand order; a seat keeps its hand for the whole round.
            for (int i = 0; i < seats.Xs.Length; i++)
            {
                PlayerSeat seat = null;
                if (hands != null && i < hands.Count) _seatOf.TryGetValue(hands[i], out seat);
                if (seat == null && i == 0 && _seats != null && _seats.Length > 0) seat = _seats[0];
                if (seat == null) continue;

                seat.Place(TableLayoutDriver.ToAnchored(new LayoutPoint(seats.Xs[i], seats.Y)), instant);
                float h = seats.CardHeight;
                seat.Hand.SetLayout(new Vector2(h * TableLayout.CardAspect, h), seats.MaxSpan);
                seat.SetBadgeOffset(new Vector2(seats.BadgeOffset.X, -seats.BadgeOffset.Y));
                seat.SetMarker(h * TableLayout.CardAspect * 1.3f, -(h / 2f + 16f));
            }

            _layout.PlaceBetSpot(seats.Bet, instant);
        }

        // =====================================================================
        //  Betting
        // =====================================================================

        private int MinBet =>
            AppManager.Exists && AppManager.Instance.GameConfig != null ? AppManager.Instance.GameConfig.MinBet : 1;

        private int TableMax =>
            AppManager.Exists && AppManager.Instance.GameConfig != null ? AppManager.Instance.GameConfig.MaxBet : int.MaxValue;

        /// <summary>Highest stake the player can legally place *and* afford.</summary>
        private int MaxBet => (int)Math.Min(TableMax, Math.Max(0L, _game != null ? _game.Balance : 0L));

        private bool Broke => MaxBet < MinBet;

        private void OnChipTapped(int index)
        {
            if (_state != TableState.Betting || index < 0 || index >= _denominations.Length) return;

            int value = _denominations[index];
            int next = Mathf.Min(_currentBet + value, MaxBet);
            if (next <= _currentBet)
            {
                NotEnoughChips();
                return;
            }

            BeginBetting();
            _currentBet = next;
            SelectChip(index);

            if (_betChip != null)
            {
                Vector3 from = _chipButtons[index] != null ? _chipButtons[index].transform.position : _betChip.transform.position;
                _betChip.AddChip(_currentBet, value, from);
            }
            RefreshBetting();
        }

        private void ClearBet()
        {
            if (_state != TableState.Betting || _currentBet == 0) return;
            BeginBetting();
            _currentBet = 0;
            if (_betChip != null) _betChip.ClearToBalance();
            UiCues.Raise(UiCue.ChipClear);
            RefreshBetting();
        }

        /// <summary>The first touch of a new bet retires the last round's verdict.</summary>
        private void BeginBetting()
        {
            if (_banner != null) _banner.Hide();
        }

        private void SelectChip(int index)
        {
            _selectedChip = index;
            if (_chipButtons == null) return;
            for (int i = 0; i < _chipButtons.Length; i++)
                if (_chipButtons[i] != null) _chipButtons[i].SetSelected(i == index);
        }

        private void NotEnoughChips()
        {
            UiCues.Raise(UiCue.Denied);
            SetStatus(Broke ? TableText.StatusOutOfChips : TableText.StatusNotEnough, Palette.Loss);
        }

        /// <summary>Re-places the last stake (clamped to what's affordable) after a round.</summary>
        private void PrefillRebet()
        {
            _currentBet = Mathf.Clamp(_lastBet, 0, MaxBet);
            if (_currentBet < MinBet) _currentBet = 0;
            if (_betChip == null) return;

            if (_currentBet > 0)
            {
                List<int> chips = TableText.Breakdown(_currentBet, _denominations, 1);
                int top = chips.Count > 0 ? chips[0] : (_denominations.Length > 0 ? _denominations[0] : _currentBet);
                Vector3 from = _balanceLabel != null ? _balanceLabel.transform.position : _betChip.transform.position;
                _betChip.AddChip(_currentBet, top, from);
            }
            else
            {
                _betChip.Hide();
            }
        }

        private void RefreshBetting()
        {
            bool broke = Broke;
            if (_dealButton != null) _dealButton.interactable = !broke && _currentBet >= MinBet;
            if (_clearButton != null) _clearButton.interactable = _currentBet > 0;
            if (_betChipButton != null) _betChipButton.interactable = _currentBet > 0;
            if (_chipButtons != null)
            {
                for (int i = 0; i < _chipButtons.Length; i++)
                    if (_chipButtons[i] != null) _chipButtons[i].Button.interactable = !broke && _currentBet < MaxBet;
            }
            if (_giftButton != null) _giftButton.interactable = true;
            SetBetReadout(_currentBet);

            if (_addChipsDot != null)
            {
                if (broke) _addChipsDot.Show();
                else _addChipsDot.Hide();
            }

            if (broke) SetStatus(TableText.StatusOutOfChips);
            else if (_currentBet >= MinBet) SetStatus(TableText.StatusReady);
            else SetStatus($"{TableText.StatusPlaceBet}  ·  {TableText.Limits(MinBet, TableMax)}");
        }

        // =====================================================================
        //  The round
        // =====================================================================

        private void OnDeal()
        {
            if (_state != TableState.Betting || _game == null || Broke || _currentBet < MinBet) return;

            if (_banner != null) _banner.Hide();
            CollectTable();

            // Reset BEFORE dealing: an instant blackjack settles inside PlaceBetAndDeal, and
            // its results event must land on a clean slate rather than be wiped afterwards.
            _lastResults = null;
            _trackingRound = true;
            _roundLow = _game.Balance;
            _holdBalance = true;

            if (!_game.PlaceBetAndDeal(_currentBet))
            {
                _trackingRound = false;
                _holdBalance = false;
                RefreshBalance();
                NotEnoughChips();
                return;
            }

            _lastBet = _currentBet;
            if (_giftButton != null) _giftButton.interactable = false;
            UiCues.Raise(UiCue.ChipPlace);
            if (_betChip != null) _betChip.Punch();
            if (_betChipButton != null) _betChipButton.interactable = false;
            ShowRoundBalance();

            BlackjackEngine engine = _game.Engine;
            SyncNewRound(engine);
            RenderPlayers(engine);
            RenderDealerConcealed(engine);
            UpdateStake(engine);

            _state = TableState.Dealing;
            SetRow(_betRow, false);
            SetRow(_actionRow, true);
            SetActionsInteractable(false);
            SetStatus(TableText.StatusDealing);
            StartCoroutine(DealFlow(engine));
        }

        private IEnumerator DealFlow(BlackjackEngine engine)
        {
            yield return WaitForCards();

            if (engine.Phase == RoundPhase.Settled)
            {
                StartCoroutine(SettleSequence(engine));
                yield break;
            }

            _state = TableState.PlayerTurn;
            RefreshTurn(engine);
        }

        private void OnHit() => Act(() => _game.Hit());

        private void OnStand() => Act(() => _game.Stand());

        private void OnSurrender() => Act(() => _game.Surrender());

        private void OnDouble()
        {
            Act(() => _game.DoubleDown(), stakeFromBalance: true);
        }

        private void OnSplit()
        {
            Act(() => _game.Split(), stakeFromBalance: true);
        }

        /// <summary>
        /// Runs one player action through GameManager, then projects the result. A double or
        /// split that went through throws the extra stake from the balance onto the felt.
        /// </summary>
        private void Act(Action action, bool stakeFromBalance = false)
        {
            if (_state != TableState.PlayerTurn || _game == null || _game.Engine == null) return;
            if (CardsInFlight()) return;

            BlackjackEngine engine = _game.Engine;
            long stakeBefore = TotalStake(engine);
            action();

            long stakeAfter = TotalStake(engine);
            if (stakeFromBalance && stakeAfter > stakeBefore && _betChip != null)
            {
                List<int> chips = TableText.Breakdown(stakeAfter - stakeBefore, _denominations, 1);
                int chip = chips.Count > 0 ? chips[0] : 0;
                Vector3 from = _balanceLabel != null ? _balanceLabel.transform.position : _betChip.transform.position;
                _betChip.AddChip(stakeAfter, chip, from);
            }
            ShowRoundBalance();

            RenderPlayers(engine);
            if (engine.Phase == RoundPhase.Settled)
            {
                StartCoroutine(SettleSequence(engine));
                return;
            }
            RenderDealerConcealed(engine);
            UpdateStake(engine);
            RefreshTurn(engine);
        }

        private void Update()
        {
            // Actions stay held while a card is still in the air, so a double-tap on HIT
            // can't draw a second card before the first has even been seen.
            if (_state == TableState.PlayerTurn && _game != null && _game.Engine != null)
                RefreshActions(_game.Engine);

            FadeRows();
            FadeFelt();
        }

        private void RefreshTurn(BlackjackEngine engine)
        {
            RefreshActions(engine);
            IReadOnlyList<Hand> hands = engine.PlayerHands;
            bool split = hands.Count > 1;
            int active = IndexOf(engine, engine.ActiveHand);
            SetStatus(split ? TableText.HandOf(active, hands.Count) : TableText.StatusYourMove);

            if (_navigator != null) _navigator.Preferred = _hitButton;
        }

        private void RefreshActions(BlackjackEngine engine)
        {
            bool playing = _state == TableState.PlayerTurn && engine.Phase == RoundPhase.PlayerTurn && !CardsInFlight();
            Hand active = engine.ActiveHand;
            bool canAffordExtra = active != null && _game.Balance >= active.Bet;

            SetInteractable(_hitButton, playing && engine.CanHit);
            SetInteractable(_standButton, playing && engine.CanStand);
            SetInteractable(_doubleButton, playing && engine.CanDouble && canAffordExtra);
            SetInteractable(_splitButton, playing && engine.CanSplit && canAffordExtra);
            SetInteractable(_surrenderButton, playing && engine.CanSurrender);

            bool surrender = engine.Phase == RoundPhase.PlayerTurn && engine.CanSurrender;
            if (_surrenderButton != null && _surrenderButton.gameObject.activeSelf != surrender)
            {
                _surrenderButton.gameObject.SetActive(surrender);
                if (_layout != null) _layout.SetSurrenderVisible(surrender);
            }

            string cost = active != null ? TableText.ExtraStake(active.Bet) : "";
            if (_doubleCost != null && _doubleCost.text != cost) _doubleCost.text = cost;
            if (_splitCost != null && _splitCost.text != cost) _splitCost.text = cost;
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null && button.interactable != value) button.interactable = value;
        }

        private void SetActionsInteractable(bool value)
        {
            SetInteractable(_hitButton, value);
            SetInteractable(_standButton, value);
            SetInteractable(_doubleButton, value);
            SetInteractable(_splitButton, value);
            SetInteractable(_surrenderButton, value);
        }

        private void HandleRoundComplete(IReadOnlyList<HandResult> results) => _lastResults = results;

        // =====================================================================
        //  Rendering
        // =====================================================================

        /// <summary>Resets the hand bookkeeping when a fresh engine appears.</summary>
        private void SyncNewRound(BlackjackEngine engine)
        {
            if (ReferenceEquals(engine, _renderedEngine)) return;
            _renderedEngine = engine;
            _seatOf.Clear();
            // Seats stay active: last round's cards may still be sliding to the discard.
            for (int i = 0; _seats != null && i < _seats.Length; i++)
            {
                if (_seats[i] == null) continue;
                if (_seats[i].Badge != null) _seats[i].Badge.HideValue();
                _seats[i].SetTurn(false, false);
            }
        }

        /// <summary>Sends the last round's cards to the discard side before a new deal.</summary>
        private void CollectTable()
        {
            if (_dealerHandView != null) _dealerHandView.Collect();
            foreach (PlayerSeat seat in Seats)
            {
                seat.Hand.Collect();
                if (seat.Badge != null) seat.Badge.HideValue();
                seat.SetTurn(false, false);
            }
            if (_dealerBadge != null) _dealerBadge.HideValue();
            if (_layout != null) _layout.SetDealerHasCards(false);
        }

        private void RenderPlayers(BlackjackEngine engine)
        {
            IReadOnlyList<Hand> hands = engine.PlayerHands;
            if (hands.Count == 0) return;

            // Give every hand a seat. A hand without one was just split off its left
            // neighbour: that hand's second card travels across rather than re-dealing.
            for (int i = 0; i < hands.Count; i++)
            {
                if (_seatOf.ContainsKey(hands[i])) continue;
                PlayerSeat seat = FreeSeat();
                if (seat == null) continue;
                _seatOf[hands[i]] = seat;
                seat.gameObject.SetActive(true);

                if (i > 0 && _seatOf.TryGetValue(hands[i - 1], out PlayerSeat source) &&
                    source.Hand.VisibleCardCount >= 2)
                {
                    if (source.Hand.TryGetCardWorldPosition(1, out Vector3 moved)) seat.Hand.DealNextFrom(moved);
                    source.Hand.Truncate(1);
                }
            }

            if (hands.Count != _seatCount) PlaceSeats(hands.Count, instant: false);

            bool playing = engine.Phase == RoundPhase.PlayerTurn;
            bool split = hands.Count > 1;
            Hand activeHand = engine.ActiveHand;
            for (int i = 0; i < hands.Count; i++)
            {
                if (!_seatOf.TryGetValue(hands[i], out PlayerSeat seat)) continue;
                seat.Hand.Render(hands[i].Cards);
                if (seat.Badge != null) seat.Badge.SetLabel(split ? $"HAND {i + 1}" : "YOU");
                if (seat.Badge != null) seat.Badge.ShowTotal(TableText.ForHand(hands[i]));
                bool isActive = playing && ReferenceEquals(hands[i], activeHand);
                seat.Hand.SetEmphasis(!split || !playing || isActive);
                seat.SetTurn(isActive, split);
            }
        }

        private PlayerSeat FreeSeat()
        {
            foreach (PlayerSeat seat in Seats)
                if (!_seatOf.ContainsValue(seat)) return seat;
            return null;
        }

        private void RenderDealerConcealed(BlackjackEngine engine)
        {
            IReadOnlyList<Card> cards = engine.DealerHand.Cards;
            bool conceal = cards.Count > 1;
            if (_dealerHandView != null) _dealerHandView.Render(cards, conceal ? 1 : -1);
            if (_layout != null) _layout.SetDealerHasCards(cards.Count > 0);
            if (_dealerBadge != null)
            {
                _dealerBadge.SetLabel("DEALER");
                _dealerBadge.SetTurn(false);
                _dealerBadge.ShowTotal(TableText.ForCards(Visible(cards, 1)));
            }
        }

        /// <summary>Draws the first <paramref name="revealed"/> dealer cards face up.</summary>
        private List<Card> RenderDealer(BlackjackEngine engine, int revealed)
        {
            IReadOnlyList<Card> cards = engine.DealerHand.Cards;
            List<Card> shown = Visible(cards, Mathf.Clamp(revealed, 0, cards.Count));
            if (_dealerHandView != null) _dealerHandView.Render(shown, -1);
            if (_layout != null) _layout.SetDealerHasCards(shown.Count > 0);
            if (_dealerBadge != null) _dealerBadge.ShowTotal(TableText.ForCards(shown));
            return shown;
        }

        private static List<Card> Visible(IReadOnlyList<Card> cards, int count)
        {
            var visible = new List<Card>(count);
            for (int i = 0; i < count && i < cards.Count; i++) visible.Add(cards[i]);
            return visible;
        }

        private static int IndexOf(BlackjackEngine engine, Hand hand)
        {
            if (hand == null) return 0;
            for (int i = 0; i < engine.PlayerHands.Count; i++)
                if (ReferenceEquals(engine.PlayerHands[i], hand)) return i;
            return 0;
        }

        private static long TotalStake(BlackjackEngine engine)
        {
            long staked = 0;
            foreach (Hand h in engine.PlayerHands) staked += h.Bet;
            return staked;
        }

        /// <summary>Doubling and splitting change what is at risk: total it from the hands.</summary>
        private void UpdateStake(BlackjackEngine engine) => SetBetReadout(TotalStake(engine));

        // =====================================================================
        //  Settle sequence
        // =====================================================================

        /// <summary>
        /// The round's third act, in order: the last player card lands, the hole card turns,
        /// the dealer draws one card at a time, the verdict lands, the chips move — and only
        /// then does the bet row return with the stake re-placed.
        /// </summary>
        private IEnumerator SettleSequence(BlackjackEngine engine)
        {
            _state = TableState.Settling;
            SetActionsInteractable(false);
            UpdateStake(engine);
            if (_navigator != null) _navigator.Preferred = null;

            // Let in-flight cards (a double's third card, a split re-deal) land.
            yield return WaitForCards();

            foreach (PlayerSeat seat in Seats)
            {
                seat.SetTurn(false, false);
                seat.Hand.SetEmphasis(true);
            }
            if (_dealerBadge != null) _dealerBadge.SetTurn(true);
            SetStatus(TableText.StatusDealerTurn);

            // Turn the hole card.
            List<Card> shown = RenderDealer(engine, Mathf.Min(2, engine.DealerHand.Cards.Count));
            yield return WaitForCards();
            yield return new WaitForSeconds(MotionPrefs.Duration(_revealPause));

            // Draw the rest, one by one.
            for (int reveal = 3; reveal <= engine.DealerHand.Cards.Count; reveal++)
            {
                shown = RenderDealer(engine, reveal);
                yield return WaitForCards();
                yield return new WaitForSeconds(MotionPrefs.Duration(_drawPause));
            }

            if (_dealerBadge != null) _dealerBadge.SetTurn(false);
            SetStatus(TableText.DealerStatus(shown, finished: true));

            float chipsHome = ShowOutcome(engine);
            float wait = Mathf.Max(chipsHome, MotionPrefs.Duration(_resultHold));

            yield return new WaitForSeconds(chipsHome);
            // The chips have arrived: now the balance moves.
            _trackingRound = false;
            _holdBalance = false;
            RefreshBalance();

            yield return new WaitForSeconds(Mathf.Max(0f, wait - chipsHome));
            EnterBetting(afterRound: true, instant: false);
        }

        private IEnumerator WaitForCards()
        {
            // Frame budget so a stuck animation can never soft-lock the table.
            for (int frame = 0; frame < 900 && CardsInFlight(); frame++) yield return null;
        }

        private bool CardsInFlight()
        {
            if (_dealerHandView != null && _dealerHandView.IsAnimating) return true;
            for (int i = 0; _seats != null && i < _seats.Length; i++)
            {
                PlayerSeat seat = _seats[i];
                if (seat != null && seat.gameObject.activeSelf && seat.Hand.IsAnimating) return true;
            }
            return false;
        }

        /// <summary>Verdict, per-hand results, and the chips. Returns seconds until the chips land.</summary>
        private float ShowOutcome(BlackjackEngine engine)
        {
            IReadOnlyList<HandResult> results = _lastResults;
            if (results == null || results.Count == 0)
            {
                // The results event never arrived (shouldn't happen): stay quiet rather than lie.
                if (_betChip != null) _betChip.Hide();
                return 0f;
            }

            RoundSummary summary = TableText.Summarize(results, engine.DealerHand, engine.Rules);
            if (_banner != null) _banner.Show(summary);
            _hasLast = true;
            _lastNet = summary.Net;
            SetLastReadout();
            RaiseOutcomeCue(summary, results);

            bool split = results.Count > 1;
            long winnings = 0;
            foreach (HandResult r in results)
            {
                if (_seatOf.TryGetValue(r.Hand, out PlayerSeat seat))
                {
                    ResultTone tone = TableText.ToneOf(r.Outcome);
                    if (tone == ResultTone.Blackjack)
                    {
                        seat.Hand.SetGlow(Palette.Gold, 0.7f);
                        seat.Hand.PulseGlow();
                    }
                    else if (tone == ResultTone.Win)
                    {
                        seat.Hand.SetGlow(Palette.Win, 0.42f);
                    }
                    else if (tone == ResultTone.Loss)
                    {
                        seat.Hand.SetOutcomeDim(true);
                    }
                    if (split && seat.Badge != null) seat.Badge.ShowResult(TableText.HandResultTag(r), tone);
                }
                if (r.NetChips > 0) winnings += r.NetChips;
            }

            // How much comes back is observed, not recomputed: GameManager has already
            // credited the round, so it is the balance now minus the low point after the
            // stakes went out. Of that, the engine's positive nets are the winnings and the
            // rest is returned stake.
            long credited = Math.Max(0L, _game.Balance - _roundLow);
            long returned = Math.Max(0L, credited - winnings);
            if (credited < winnings) winnings = credited;

            if (_betChip == null) return 0f;
            if (returned > 0 && returned != _betChip.Amount) _betChip.SetAmount(returned);
            BetChipView.SettleKind kind = winnings > 0
                ? BetChipView.SettleKind.Win
                : returned > 0 ? BetChipView.SettleKind.Return : BetChipView.SettleKind.Lose;
            return _betChip.Settle(kind, winnings);
        }

        private static void RaiseOutcomeCue(RoundSummary summary, IReadOnlyList<HandResult> results)
        {
            bool bust = results.Count == 1 && results[0].Outcome == HandOutcome.PlayerBust;
            switch (summary.Tone)
            {
                case ResultTone.Blackjack: UiCues.Raise(UiCue.Blackjack); break;
                case ResultTone.Win: UiCues.Raise(UiCue.Win); break;
                case ResultTone.Push: UiCues.Raise(UiCue.Push); break;
                default: UiCues.Raise(bust ? UiCue.Bust : UiCue.Loss); break;
            }
        }

        /// <summary>Back to betting: bet row up, stake re-placed, focus on DEAL.</summary>
        private void EnterBetting(bool afterRound, bool instant)
        {
            _state = TableState.Betting;
            SetRow(_actionRow, false, instant);
            SetRow(_betRow, true, instant);
            if (_dealerBadge != null) _dealerBadge.SetTurn(false);
            if (_dealerBadge != null && (_renderedEngine == null || !afterRound))
            {
                _dealerBadge.SetLabel("DEALER");
                _dealerBadge.HideValue();
            }

            if (afterRound) PrefillRebet();
            RefreshBetting();
            RefreshGiftDot();
            if (_navigator != null) _navigator.Preferred = _dealButton;
        }

        // =====================================================================
        //  Readouts
        // =====================================================================

        private void HandleBalanceChanged(long balance)
        {
            if (_trackingRound) _roundLow = Math.Min(_roundLow, balance);
            if (!_holdBalance) RefreshBalance();
        }

        private void HandleProfileChanged(Player.PlayerData _)
        {
            // Resets and reward claims change the balance without a chip event.
            if (!_holdBalance) RefreshBalance();
        }

        /// <summary>During a round, the balance shows what is left after the stakes went out.</summary>
        private void ShowRoundBalance()
        {
            if (!_trackingRound) return;
            if (_balanceRollup != null) _balanceRollup.SetValue(_roundLow);
            if (_balanceRollup == null && _balanceLabel != null) _balanceLabel.text = TableText.Chips(_roundLow);
        }

        private void RefreshBalance()
        {
            if (_game == null) return;
            // The rollup owns the label's text when present, so don't write both.
            if (_balanceRollup != null) _balanceRollup.SetValue(_game.Balance);
            else if (_balanceLabel != null) _balanceLabel.text = TableText.Chips(_game.Balance);
        }

        private void SetBetReadout(long amount)
        {
            if (_betLabel != null) _betLabel.text = amount > 0 ? TableText.Chips(amount) : "—";
        }

        private void SetLastReadout()
        {
            if (_lastLabel == null) return;
            if (!_hasLast)
            {
                _lastLabel.text = "—";
                _lastLabel.color = Palette.Muted;
                return;
            }
            _lastLabel.text = TableText.Signed(_lastNet);
            _lastLabel.color = _lastNet > 0 ? Palette.Win : _lastNet < 0 ? Palette.Loss : Palette.Ivory;
        }

        private void SetStatus(string text, Color? tint = null)
        {
            if (_statusLabel == null) return;
            if (_statusLabel.text == text && !tint.HasValue) return;
            _statusLabel.text = text;
            if (_statusPunch != null) _statusPunch.Play(tint, tint.HasValue ? 1f : 0.5f);
        }

        // =====================================================================
        //  Daily reward shortcut
        // =====================================================================

        /// <summary>Same claim flow as the Main Menu's daily-reward button; the result shows
        /// on the status line.</summary>
        private void ClaimGift()
        {
            if (!AppManager.Exists || AppManager.Instance.Rewards == null) return;

            DailyRewardResult result = AppManager.Instance.Rewards.TryClaim(DateTime.UtcNow);
            if (result.Success)
            {
                SetStatus($"+{TableText.Chips(result.ChipsAwarded)} CHIPS  ·  DAY {result.NewStreak} STREAK", Palette.Gold);
                UiCues.Raise(UiCue.ChipCollect);
            }
            else
            {
                TimeSpan wait = result.TimeUntilNext;
                SetStatus($"NEXT REWARD IN {(int)wait.TotalHours}H {wait.Minutes:00}M");
            }

            RefreshGiftDot();
            if (!_holdBalance) RefreshBalance();
            if (_state == TableState.Betting) RefreshBettingControlsOnly();
        }

        /// <summary>Re-evaluates the betting controls without touching the status line.</summary>
        private void RefreshBettingControlsOnly()
        {
            bool broke = Broke;
            if (_dealButton != null) _dealButton.interactable = !broke && _currentBet >= MinBet;
            if (_chipButtons != null)
            {
                for (int i = 0; i < _chipButtons.Length; i++)
                    if (_chipButtons[i] != null) _chipButtons[i].Button.interactable = !broke && _currentBet < MaxBet;
            }
        }

        private void RefreshGiftDot()
        {
            if (_giftDot == null || !AppManager.Exists || AppManager.Instance.Rewards == null) return;
            if (AppManager.Instance.Rewards.IsRewardAvailable(DateTime.UtcNow)) _giftDot.Show();
            else _giftDot.Hide();
        }

        private void Navigate(string scene)
        {
            if (_game != null) _game.GoToScene(scene);
            else SceneFader.TransitionTo(scene);
        }

        // =====================================================================
        //  Rows & felt
        // =====================================================================

        /// <summary>
        /// Shows or hides a control row. Rows cross-fade; a hidden row is also deactivated so
        /// it can neither be clicked nor reached by keyboard navigation.
        /// </summary>
        private void SetRow(CanvasGroup group, bool visible, bool instant = false)
        {
            if (group == null) return;
            group.interactable = visible;
            // An incoming row only takes taps once it can actually be seen (see Rising).
            group.blocksRaycasts = visible && (instant || MotionPrefs.Reduced || group.alpha > 0.5f);
            if (visible && !group.gameObject.activeSelf) group.gameObject.SetActive(true);
            if (instant || MotionPrefs.Reduced)
            {
                group.alpha = visible ? 1f : 0f;
                group.blocksRaycasts = visible;
                if (!visible) group.gameObject.SetActive(false);
            }
            if (group == _betRow) _betRowAlpha = visible ? 1f : 0f;
            else if (group == _actionRow) _actionRowAlpha = visible ? 1f : 0f;
        }

        /// <summary>
        /// Rows share the dock, so they swap in sequence — the outgoing row fades out, then
        /// the incoming one fades in — rather than cross-fading chips over buttons.
        /// </summary>
        private void FadeRows()
        {
            float step = Time.deltaTime / Mathf.Max(0.01f, MotionPrefs.Duration(_rowFade));
            bool outgoing = Fading(_betRow, _betRowAlpha, step) | Fading(_actionRow, _actionRowAlpha, step);
            if (outgoing) return;
            Rising(_betRow, _betRowAlpha, step);
            Rising(_actionRow, _actionRowAlpha, step);
        }

        private static bool Fading(CanvasGroup group, float target, float step)
        {
            if (group == null || target > 0f || !group.gameObject.activeSelf) return false;
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, step);
            if (group.alpha <= 0f) group.gameObject.SetActive(false);
            return group.gameObject.activeSelf;
        }

        private static void Rising(CanvasGroup group, float target, float step)
        {
            if (group == null || target <= 0f || !group.gameObject.activeSelf) return;
            group.alpha = Mathf.MoveTowards(group.alpha, 1f, step);
            if (!group.blocksRaycasts && group.alpha > 0.5f) group.blocksRaycasts = true;
        }

        /// <summary>The felt print steps back while the verdict is showing.</summary>
        private void FadeFelt()
        {
            if (_feltPrint == null) return;
            float target = _banner != null && _banner.Visible ? 0f : 1f;
            if (Mathf.Approximately(_feltAlpha, target)) return;
            _feltAlpha = Mathf.MoveTowards(_feltAlpha, target, Time.deltaTime / Mathf.Max(0.01f, MotionPrefs.Duration(0.3f)));
            _feltPrint.alpha = _feltAlpha;
        }
    }
}
