# Architecture

This document explains how the pieces fit together and why, so new systems can be added
without fighting the design.

## Layering

```
            ┌───────────────────────────────────────────────┐
   Unity    │  UI: Screens, Components, Layout driver        │  MonoBehaviours
   shell    │      TableText · TableLayout (pure, tested)    │  plain classes
            │  Core: AppManager, GameManager                │  MonoBehaviours
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

## Presentation layer (`Assets/Scripts/UI`)

The UI shows the engine's state; it has no rules of its own. Everything it prints or
animates comes from the engine's hands and `HandResult`s, `IRuleSet`, `GameConfig` and
`ChipManager`.

### Pacing, not deciding
`BlackjackEngine` resolves a round synchronously. Once the player's last action
returns, the dealer has drawn and every hand is settled. `GameTableUI` then tells the
story in order: hole-card flip, each dealer draw, the verdict banner, and the chips paid,
returned or swept. The screen has its own four states (`Betting → Dealing → PlayerTurn →
Settling`). They gate input while cards are in flight; they never alter the round.
Actions are enabled strictly from the engine's `CanHit` / `CanStand` / `CanDouble` /
`CanSplit` / `CanSurrender`.

### Observing the balance instead of recomputing payouts
The balance readout stays at its lowest in-round value while chips are on the felt,
then rolls up when the chips land. The chip animation needs two numbers: how much came
back and how much of that was winnings. Rather than recomputing the payout, the table
watches `ChipManager.OnBalanceChanged`, which `GameManager` relays:

```
credited  = balance after settlement − lowest balance seen this round
winnings  = Σ max(0, HandResult.NetChips)
returned  = credited − winnings
```

If `GameManager`'s settlement changes, the table follows it automatically.

### Pure, tested helpers
- **`TableText`** formats everything the table says: hand totals ("7 / 17", "BUST",
  "BLACKJACK"), the round verdict and its one-line reason, signed amounts (with a true
  minus sign), chip labels (1K, 2.5K), and the felt print ("BLACKJACK PAYS 3 TO 2",
  "Dealer hits soft 17"). All of it comes from `IRuleSet` and the results. No Unity
  dependency.
- **`TableLayout`** is the responsive geometry, in safe-area units: HUD, dealer and
  player rows, split seats, bet spot, shoe, rail and control dock, for portrait and
  landscape. `TableLayoutDriver` only applies it to `RectTransform`s. The EditMode tests
  sweep aspect ratios from 21:9 to 20:9 phones with insets.

### Motion, input and sound hooks
- **`Theme/`** — `Palette` holds the colour tokens (sprites are drawn white and tinted),
  `Ease` the curves, and `MotionPrefs` the *Reduce Motion* setting. That setting
  shortens durations and removes the travel.
- **`Interaction/`** — `KeyboardNavigator` handles Tab order, modal scoping, Esc to
  close and focus recovery. `InputModality` shows focus rings only for keyboard users.
  `TableShortcuts` maps H/S/D/P/R, 1–9, Backspace and Space/Enter to the same buttons a
  tap would press, so the engine sees identical calls.
- **`Feedback/UiCues`** — a static event hub raised at each audible moment (card deal,
  flip, chip place/collect, win, bust…). No audio ships. A sound player only has to
  subscribe to `UiCues.Raised`.

### Scenes are generated
`Assets/Editor/SceneBootstrap.cs` builds and wires `MainMenu`, `Game` and `Store`.
**Blackjack ▸ Build UI Scenes** runs it, and so does `BuildAllFromCommandLine` for CI.
Each scene carries a `SceneBuildStamp`. When the builder's version is newer, the editor
offers a rebuild, and **Verify Scene Wiring** reports any missing reference.

## Extending

- **New rule variant:** implement `IRuleSet`, add it to `AppManager.CreateRuleSet()` and
  the `RuleVariant` enum.
- **New reward type:** add data to `EconomyConfig`, logic to `RewardSystem`.
- **Server-authoritative play:** move `BlackjackEngine` behind an API; the client already
  talks to managers, not cards, so the swap is localized.
- **Sound:** subscribe to `UiCues.Raised` and map each `UiCue` to a clip (respect the
  existing sound setting in `SettingsPanel`).
- **New table copy or layout:** edit `TableText` / `TableLayout`, then run the EditMode
  tests. `art-source/preview_premium_table.py` mirrors the layout for quick visual checks
  without Unity.
