# Architecture

This document explains how the pieces fit together and why, so new systems can be added
without fighting the design.

## Layering

```
            ┌───────────────────────────────────────────────┐
   Unity    │  UI (Screens/Components)                       │  MonoBehaviours
   shell    │  Core: AppManager, GameManager                │  MonoBehaviours
            ├───────────────────────────────────────────────┤
   Pure C#  │  Economy: ChipManager, RewardSystem, Store     │  plain classes
   core     │  Blackjack: Engine, DealerAI, HandEvaluator    │  plain classes
            │  Cards: Card, Deck, Hand · Rules: IRuleSet     │
            │  Player: PlayerData / PlayerProfile            │
            │  Utils: IRandomProvider, MonoSingleton         │
            └───────────────────────────────────────────────┘
```

The **core** (Blackjack, Economy logic, Player data, Rules) has no `UnityEngine`
dependency except where persistence/inspector integration is genuinely needed
(`PlayerProfile`, ScriptableObject configs, `MonoSingleton`). This keeps game rules
headless-testable and portable to a server.

## Key decisions

### 1. Rules as a strategy (`IRuleSet`)
Every variant-specific decision — deck count, blackjack payout, dealer-hits-soft-17,
peek/no-hole-card, doubling and splitting constraints — lives behind `IRuleSet`. The
engine asks the rule set questions (`CanDouble`, `CanSplit`, `DealerHitsSoft17`); it never
hardcodes a variant. Adding "Vegas Downtown" or "Spanish 21" = one new class.

### 2. Engine is a state machine, UI is a projection
`BlackjackEngine` owns round phase (`Idle → PlayerTurn → DealerTurn → Settled`) and emits
events (`OnCardDealt`, `OnPhaseChanged`, `OnRoundSettled`). `GameManager` adapts those to
Unity and applies chip settlement. UI simply renders current state and enables actions
based on the engine's `CanHit` / `CanStand` / `CanDouble` / `CanSplit` flags.

### 3. One economy entry point (`ChipManager`)
All chip mutations funnel through `ChipManager` (`Add`, `TrySpend`, `ApplyNet`) so balance
changes are validated and persisted in one place, and `OnBalanceChanged` keeps every UI
label in sync.

### 4. Injected randomness (`IRandomProvider`)
`Deck` shuffles through an injected RNG. Tests seed it for determinism; production can
later swap in a server-seeded/provably-fair provider without touching the deck.

### 5. Config over constants
`GameConfig` and `EconomyConfig` (ScriptableObjects) hold bet limits, chip denominations,
starting balance, the daily-reward ladder, and store packs — no magic numbers in code.

## Settlement model

Bets are debited up front (`TrySpend`). On settlement `GameManager` returns stake +
winnings for wins/blackjack, returns the stake on a push, returns half on surrender, and
returns nothing on a loss/bust. `HandResult.NetChips` carries the net delta for UI/stats.

## Extending

- **New rule variant:** implement `IRuleSet`, add it to `AppManager.CreateRuleSet()` and
  the `RuleVariant` enum.
- **New reward type:** add data to `EconomyConfig`, logic to `RewardSystem`.
- **Server-authoritative play:** move `BlackjackEngine` behind an API; the client already
  talks to managers, not cards, so the swap is localized.
