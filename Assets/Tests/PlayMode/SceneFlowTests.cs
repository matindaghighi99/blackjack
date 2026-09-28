using System;
using System.Collections;
using BlackjackGame.Blackjack;
using BlackjackGame.Core;
using BlackjackGame.UI.Components;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BlackjackGame.PlayTests
{
    /// <summary>
    /// Play-mode smoke tests: the automated equivalent of "press Play from MainMenu, bet a
    /// chip, deal, hit/stand, watch the round settle and the chips move". They drive the
    /// real scenes and real UI (via <c>onClick.Invoke()</c> on the same buttons a finger
    /// would press), so a broken serialized reference or a missing scene fails the run.
    ///
    /// The scenes must exist and be registered in Build Settings — run
    /// <b>Blackjack ▸ Build UI Scenes</b> first.
    /// </summary>
    public class SceneFlowTests
    {
        private const int TestChip = 100;

        // -----------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------

        private static IEnumerator LoadScene(string name)
        {
            SceneManager.LoadScene(name);
            yield return null; // scene activates
            yield return null; // Awake/Start have run
        }

        private static T FindUI<T>(string gameObjectName) where T : Component
        {
            GameObject go = GameObject.Find(gameObjectName);
            Assert.IsNotNull(go, $"GameObject '{gameObjectName}' not found (or inactive) in the active scene.");
            var component = go.GetComponent<T>();
            Assert.IsNotNull(component, $"'{gameObjectName}' has no {typeof(T).Name}.");
            return component;
        }

        /// <summary>Waits for a condition with a frame budget, so a stuck UI fails instead of hanging.</summary>
        private static IEnumerator WaitUntil(Func<bool> condition, string what, int frames = 1200)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                if (condition()) yield break;
                yield return null;
            }
            Assert.Fail($"Timed out waiting for: {what}");
        }

        /// <summary>
        /// The Nth card image inside a HandView, found by name. HandView also parents a
        /// shadow per card, so positional indexing would pick up shadows.
        /// </summary>
        private static Image CardAt(HandView view, int index)
        {
            string wanted = $"Card_{index:00}";
            foreach (Image image in view.GetComponentsInChildren<Image>())
                if (image.name == wanted) return image;
            Assert.Fail($"No '{wanted}' under {view.name}.");
            return null;
        }

        private static bool Usable(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) return false;
            var button = go.GetComponent<Button>();
            return button != null && button.isActiveAndEnabled && button.IsInteractable();
        }

        /// <summary>Boots the app the way a player would, and guarantees a spendable balance.</summary>
        private static IEnumerator BootFromMainMenu()
        {
            yield return LoadScene(SceneNames.MainMenu);

            Assert.IsTrue(AppManager.Exists,
                "AppManager did not boot. Is it in MainMenu with both config assets assigned?");
            Assert.IsNotNull(AppManager.Instance.Chips, "AppManager.Chips is null — Bootstrap() bailed out.");

            if (AppManager.Instance.Chips.Balance < TestChip * 20)
                AppManager.Instance.Chips.Add(TestChip * 50);
        }

        // -----------------------------------------------------------------
        //  Tests
        // -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator MainMenu_BootsAppManagerAndShowsBalance()
        {
            yield return BootFromMainMenu();

            Assert.IsNotNull(AppManager.Instance.GameConfig, "GameConfig not assigned on AppManager.");
            Assert.IsNotNull(AppManager.Instance.EconomyConfig, "EconomyConfig not assigned on AppManager.");
            Assert.IsNotNull(AppManager.Instance.Rewards);
            Assert.IsNotNull(AppManager.Instance.Store);

            Assert.IsNotEmpty(FindUI<TMP_Text>("BalanceLabel").text, "Main menu balance label was never populated.");

            foreach (string name in new[] { "PlayButton", "StoreButton", "RewardsButton" })
            {
                Button button = FindUI<Button>(name);
                Assert.IsTrue(button.interactable, $"{name} is not interactable.");
            }

            // Claiming the daily reward must always leave a message on the status label.
            FindUI<Button>("RewardsButton").onClick.Invoke();
            yield return null;
            Assert.IsNotEmpty(FindUI<TMP_Text>("RewardStatusLabel").text, "Reward status label was not updated after claiming.");
        }

        [UnityTest]
        public IEnumerator GameScene_BetDealPlaySettle_MovesChipBalance()
        {
            yield return BootFromMainMenu();
            var chips = AppManager.Instance.Chips;

            yield return LoadScene(SceneNames.Game);
            Assert.IsTrue(GameManager.Exists, "GameManager missing from the Game scene.");
            GameManager game = GameManager.Instance;

            // Chip-first betting: nothing is staked until a chip is tapped.
            Assert.IsFalse(Usable("DealButton"), "DEAL must be disabled before any bet is placed.");
            FindUI<Button>($"Chip{TestChip}").onClick.Invoke();
            yield return null;
            Assert.AreEqual("100", FindUI<TMP_Text>("BetLabel").text, "The BET readout should show the stake.");
            Assert.IsTrue(Usable("DealButton"), "DEAL should enable once the stake meets the table minimum.");

            long balanceBeforeDeal = chips.Balance;
            FindUI<Button>("DealButton").onClick.Invoke();
            yield return null;

            Assert.IsNotNull(game.Engine, "Deal did not start a round.");
            Assert.AreEqual(1, game.Engine.PlayerHands.Count);
            Assert.AreEqual(2, game.Engine.PlayerHands[0].Cards.Count, "Player should hold two cards.");

            var dealerCards = FindUI<HandView>("DealerHandView");
            var playerCards = FindUI<HandView>("PlayerHandView");
            yield return WaitUntil(() => !dealerCards.IsAnimating && !playerCards.IsAnimating, "the opening deal to land");
            Assert.AreEqual(2, playerCards.VisibleCardCount, "Player's cards were not rendered.");

            if (game.Engine.Phase == RoundPhase.PlayerTurn)
            {
                Assert.AreEqual(balanceBeforeDeal - TestChip, chips.Balance, "Placing a bet should debit exactly the bet.");

                // The hole card stays face down while the player acts.
                if (game.Engine.DealerHand.Cards.Count > 1)
                    StringAssert.Contains("Back", CardAt(dealerCards, 1).sprite.name,
                        "Dealer's hole card should be face down during the player's turn.");

                yield return WaitUntil(() => Usable("StandButton"), "actions to unlock after the deal");

                if (Usable("HitButton") && game.Engine.PlayerHands[0].Value < 12)
                {
                    int before = game.Engine.PlayerHands[0].Cards.Count;
                    FindUI<Button>("HitButton").onClick.Invoke();
                    yield return null;
                    Assert.AreEqual(before + 1, game.Engine.PlayerHands[0].Cards.Count, "Hit drew no card.");
                }

                // Stand through whatever is left (split hands included); buttons are looked up
                // each time because the action row hides the moment the round settles.
                int guard = 0;
                while (game.Engine.Phase == RoundPhase.PlayerTurn && guard++ < 50)
                {
                    yield return WaitUntil(() => Usable("StandButton") || game.Engine.Phase != RoundPhase.PlayerTurn,
                        "STAND to be available");
                    if (Usable("StandButton")) FindUI<Button>("StandButton").onClick.Invoke();
                    yield return null;
                }
            }

            Assert.AreEqual(RoundPhase.Settled, game.Engine.Phase, "Round never settled.");

            // The table tells the story in order; when it's done the bet row comes back.
            yield return WaitUntil(() => GameObject.Find("BetRow") != null && Usable("DealButton"),
                "the settle sequence to finish and betting to reopen", 2400);

            var balanceLabel = FindUI<TMP_Text>("BalanceLabel");
            yield return WaitUntil(() => !balanceLabel.GetComponent<CountRollup>().IsRolling, "the balance to finish rolling");
            Assert.IsTrue(long.TryParse(balanceLabel.text.Replace(",", ""), out long shown),
                $"Table balance label should be a plain number, was '{balanceLabel.text}'.");
            // The re-bet is placed back on the felt from the balance display only visually;
            // the label shows the real balance.
            Assert.AreEqual(chips.Balance, shown, "Balance label is out of sync with ChipManager.");

            // Once settled the dealer's hand is fully revealed.
            Assert.AreEqual(game.Engine.DealerHand.Cards.Count, dealerCards.VisibleCardCount);
            for (int i = 0; i < dealerCards.VisibleCardCount; i++)
                Assert.IsFalse(CardAt(dealerCards, i).sprite.name.Contains("Back"),
                    "Dealer still has a face-down card after the round settled.");

            Assert.IsNotEmpty(FindUI<TMP_Text>("Headline").text, "No verdict was shown.");
            Assert.IsNotEmpty(FindUI<TMP_Text>("StatusLabel").text, "The status line went blank.");
        }

        [UnityTest]
        public IEnumerator StoreScene_ListsPacks_AndMockPurchaseGrantsChips()
        {
            yield return BootFromMainMenu();
            var chips = AppManager.Instance.Chips;
            int expectedPacks = AppManager.Instance.EconomyConfig.ChipPacks.Length;

            yield return LoadScene(SceneNames.Store);

            GameObject packList = GameObject.Find("PackList");
            Assert.IsNotNull(packList, "Store scene has no PackList container.");
            Assert.AreEqual(expectedPacks, packList.transform.childCount,
                "StoreUI should spawn one row per chip pack from EconomyConfig.");

            long before = chips.Balance;
            var firstPack = packList.transform.GetChild(0).GetComponent<Button>();
            Assert.IsNotNull(firstPack, "Pack row is not a Button — check the StorePackRow prefab.");

            firstPack.onClick.Invoke();
            yield return null;

            // In the editor AppManager selects MockPurchaseService, which resolves synchronously.
            Assert.Greater(chips.Balance, before, "Mock purchase did not grant chips.");
            Assert.IsNotEmpty(FindUI<TMP_Text>("StatusLabel").text, "Store status label not updated.");
        }
    }
}
