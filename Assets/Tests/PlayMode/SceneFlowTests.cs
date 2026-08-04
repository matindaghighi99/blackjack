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
    /// Play-mode smoke tests: these are the automated equivalent of "press Play from
    /// MainMenu, deal a round, hit/stand, watch the chip balance move". They drive the
    /// real scenes and real UI components (via <c>onClick.Invoke()</c>), so a broken
    /// serialized reference or a missing scene in Build Settings fails the run.
    ///
    /// The scenes must exist and be registered in Build Settings — run
    /// <b>Blackjack ▸ Build UI Scenes</b> first.
    /// </summary>
    public class SceneFlowTests
    {
        private const int TestBet = 100;

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
            Assert.IsNotNull(go, $"GameObject '{gameObjectName}' not found in the active scene.");
            var component = go.GetComponent<T>();
            Assert.IsNotNull(component, $"'{gameObjectName}' has no {typeof(T).Name}.");
            return component;
        }

        /// <summary>Boots the app the way a player would, and guarantees a spendable balance.</summary>
        private static IEnumerator BootFromMainMenu()
        {
            yield return LoadScene(SceneNames.MainMenu);

            Assert.IsTrue(AppManager.Exists,
                "AppManager did not boot. Is it in MainMenu with both config assets assigned?");
            Assert.IsNotNull(AppManager.Instance.Chips, "AppManager.Chips is null — Bootstrap() bailed out.");

            if (AppManager.Instance.Chips.Balance < TestBet * 10)
                AppManager.Instance.Chips.Add(TestBet * 50);
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

            var balanceLabel = FindUI<TMP_Text>("BalanceLabel");
            Assert.IsNotEmpty(balanceLabel.text, "Main menu balance label was never populated.");

            // The three routing buttons exist, are wired into the scene and are clickable.
            foreach (string name in new[] { "PlayButton", "StoreButton", "RewardsButton" })
            {
                Button button = FindUI<Button>(name);
                Assert.IsTrue(button.interactable, $"{name} is not interactable.");
                Assert.IsNotNull(button.onClick, $"{name} has no onClick event.");
            }

            // Claiming the daily reward must always leave a message on the status label.
            FindUI<Button>("RewardsButton").onClick.Invoke();
            yield return null;
            Assert.IsNotEmpty(FindUI<TMP_Text>("RewardStatusLabel").text,
                "Reward status label was not updated after claiming.");
        }

        [UnityTest]
        public IEnumerator GameScene_DealHitStand_MovesChipBalance()
        {
            yield return BootFromMainMenu();
            var chips = AppManager.Instance.Chips;

            yield return LoadScene(SceneNames.Game);
            Assert.IsTrue(GameManager.Exists, "GameManager missing from the Game scene.");
            GameManager game = GameManager.Instance;

            var betInput = FindUI<TMP_InputField>("BetInput");
            Assert.AreEqual(TestBet.ToString(), betInput.text,
                "Bet field should be pre-filled so Deal works with no typing.");

            long balanceBeforeDeal = chips.Balance;
            FindUI<Button>("DealButton").onClick.Invoke();
            yield return null;

            Assert.IsNotNull(game.Engine, "Deal did not start a round.");
            Assert.AreEqual(1, game.Engine.PlayerHands.Count);
            Assert.AreEqual(2, game.Engine.PlayerHands[0].Cards.Count, "Player should hold two cards.");
            Assert.GreaterOrEqual(game.Engine.DealerHand.Cards.Count, 1);

            // Cards must actually be drawn, not just held in the engine.
            var dealerCards = FindUI<HandView>("DealerHandView");
            var playerCards = FindUI<HandView>("PlayerHandView");
            Assert.AreEqual(2, playerCards.VisibleCardCount, "Player's cards were not rendered.");
            Assert.AreEqual(game.Engine.DealerHand.Cards.Count, dealerCards.VisibleCardCount,
                "Dealer's cards were not rendered.");

            if (game.Engine.Phase == RoundPhase.PlayerTurn)
            {
                // The hole card must stay face down, and the label must not leak the total.
                if (game.Engine.DealerHand.Cards.Count > 1)
                {
                    Image[] shown = dealerCards.GetComponentsInChildren<Image>();
                    Assert.GreaterOrEqual(shown.Length, 2);
                    Assert.IsNotNull(shown[1].sprite, "Hole card has no sprite.");
                    StringAssert.Contains("Back", shown[1].sprite.name,
                        "Dealer's hole card should be face down during the player's turn.");
                    StringAssert.Contains("?", FindUI<TMP_Text>("DealerHandLabel").text,
                        "Dealer label leaks the hidden card's value.");
                }

                // Stake debited, round in progress.
                Assert.AreEqual(balanceBeforeDeal - TestBet, chips.Balance,
                    "Placing a bet should debit exactly the bet amount.");

                if (game.Engine.CanHit)
                {
                    int before = game.Engine.PlayerHands[0].Cards.Count;
                    FindUI<Button>("HitButton").onClick.Invoke();
                    yield return null;
                    Assert.AreEqual(before + 1, game.Engine.PlayerHands[0].Cards.Count, "Hit drew no card.");
                }

                Button stand = FindUI<Button>("StandButton");
                int guard = 0;
                while (game.Engine.Phase == RoundPhase.PlayerTurn && guard++ < 25)
                {
                    stand.onClick.Invoke();
                    yield return null;
                }
            }
            else
            {
                // Natural blackjack settled instantly — balance already reflects the payout.
                Assert.AreNotEqual(balanceBeforeDeal, chips.Balance);
            }

            Assert.AreEqual(RoundPhase.Settled, game.Engine.Phase, "Round never settled.");

            var balanceLabel = FindUI<TMP_Text>("BalanceLabel");
            StringAssert.StartsWith("Chips:", balanceLabel.text, "Table balance label not refreshed.");
            Assert.IsNotEmpty(FindUI<TMP_Text>("DealerHandLabel").text);
            Assert.IsNotEmpty(FindUI<TMP_Text>("PlayerHandLabel").text);

            // Once settled the dealer's hand is fully revealed.
            Assert.AreEqual(game.Engine.DealerHand.Cards.Count, dealerCards.VisibleCardCount);
            foreach (Image card in dealerCards.GetComponentsInChildren<Image>())
                Assert.IsFalse(card.sprite != null && card.sprite.name.Contains("Back"),
                    "Dealer still has a face-down card after the round settled.");
            StringAssert.DoesNotContain("?", FindUI<TMP_Text>("DealerHandLabel").text);
            Assert.IsNotEmpty(FindUI<TMP_Text>("OutcomeLabel").text, "No outcome was shown.");
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
            Assert.IsNotNull(firstPack, "Pack row is not a Button — check the PackButton prefab.");

            firstPack.onClick.Invoke();
            yield return null;

            // In the editor AppManager selects MockPurchaseService, which resolves synchronously.
            Assert.Greater(chips.Balance, before, "Mock purchase did not grant chips.");
            Assert.IsNotEmpty(FindUI<TMP_Text>("StatusLabel").text, "Store status label not updated.");
        }
    }
}
