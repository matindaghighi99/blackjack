using System;
using System.Collections.Generic;
using BlackjackGame.Config;
using BlackjackGame.Core;
using BlackjackGame.Economy;
using BlackjackGame.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Screens
{
    /// <summary>
    /// Store screen. Spawns a button per chip pack from EconomyConfig and drives the real
    /// IAP flow via <see cref="StoreManager"/>. Purchases are asynchronous: tapping a pack
    /// kicks off the platform billing flow and the result arrives on
    /// <see cref="StoreManager.OnPurchaseCompleted"/> (chips are only granted after the
    /// receipt is validated).
    /// </summary>
    public sealed class StoreUI : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Parent transform (e.g. a Vertical Layout Group) that pack rows are added to.")]
        [SerializeField] private Transform _packListRoot;
        [Tooltip("Row template: artwork, amount, bonus and price.")]
        [SerializeField] private StorePackRow _packRowPrefab;
        [Tooltip("Chip artwork per pack, in EconomyConfig order. Reused if there are fewer.")]
        [SerializeField] private Sprite[] _packArtwork;

        [Header("Display")]
        [SerializeField] private TMP_Text _balanceLabel;
        [SerializeField] private TMP_Text _statusLabel;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        [Header("Quick Actions (top bar)")]
        [SerializeField] private Button _giftButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _trophyButton;
        [SerializeField] private SettingsPanel _settingsPanel;
        [SerializeField] private StatsPanel _statsPanel;

        [Header("Juice")]
        [Tooltip("Rolls the balance up instead of snapping it — the payoff for a purchase.")]
        [SerializeField] private CountRollup _balanceRollup;
        [Tooltip("Slams the status line in on a completed purchase or reward claim.")]
        [SerializeField] private LabelPunch _statusPunch;

        private StoreManager _store;

        private void Start()
        {
            if (_backButton != null)
                _backButton.onClick.AddListener(() =>
                    UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.MainMenu));

            if (_giftButton != null) _giftButton.onClick.AddListener(ClaimGift);
            if (_settingsButton != null && _settingsPanel != null)
                _settingsButton.onClick.AddListener(_settingsPanel.Show);
            if (_trophyButton != null && _statsPanel != null)
                _trophyButton.onClick.AddListener(_statsPanel.Show);

            if (!AppManager.Exists)
            {
                SetStatus("Store unavailable (start from Main Menu).");
                return;
            }

            _store = AppManager.Instance.Store;
            _store.OnPurchaseCompleted += HandlePurchaseCompleted;
            _store.OnStoreReady += HandleStoreReady;
            AppManager.Instance.Chips.OnBalanceChanged += _ => RefreshBalance();

            BuildPackList();
            RefreshBalance();
            SetStatus(_store.IsReady ? "" : "Connecting to store…");
        }

        private void OnDestroy()
        {
            if (_store != null)
            {
                _store.OnPurchaseCompleted -= HandlePurchaseCompleted;
                _store.OnStoreReady -= HandleStoreReady;
            }
        }

        private void BuildPackList()
        {
            if (_packListRoot == null || _packRowPrefab == null) return;

            int bestValue = BestValueIndex(_store.AvailablePacks);

            for (int i = 0; i < _store.AvailablePacks.Count; i++)
            {
                ChipPack captured = _store.AvailablePacks[i]; // avoid closure capture bug
                StorePackRow row = Instantiate(_packRowPrefab, _packListRoot, false);

                Sprite art = _packArtwork != null && _packArtwork.Length > 0
                    ? _packArtwork[i % _packArtwork.Length]
                    : null;

                row.Bind(captured, art, i == bestValue);
                if (row.Button != null) row.Button.onClick.AddListener(() => Purchase(captured.Id));
            }
        }

        /// <summary>
        /// The pack giving the most bonus per chip bought. Derived rather than hard-coded
        /// so the badge follows EconomyConfig if the packs are ever retuned.
        /// </summary>
        private static int BestValueIndex(IReadOnlyList<ChipPack> packs)
        {
            int best = -1;
            float bestRatio = 0f;
            for (int i = 0; i < packs.Count; i++)
            {
                if (packs[i].ChipAmount <= 0 || packs[i].BonusChips <= 0) continue;
                float ratio = packs[i].BonusChips / (float)packs[i].ChipAmount;
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Same claim flow as the Main Menu's daily-reward button, surfaced here
        /// as a quick action.</summary>
        private void ClaimGift()
        {
            if (!AppManager.Exists) return;

            DailyRewardResult result = AppManager.Instance.Rewards.TryClaim(DateTime.UtcNow);
            SetStatus(result.Success
                ? $"+{result.ChipsAwarded:N0} chips! Streak: {result.NewStreak}"
                : $"Next reward in {result.TimeUntilNext.Hours}h {result.TimeUntilNext.Minutes}m");
            RefreshBalance();
        }

        private void Purchase(string packId)
        {
            SetStatus("Processing purchase…");
            _store.PurchasePack(packId); // result arrives via OnPurchaseCompleted
        }

        private void HandlePurchaseCompleted(PurchaseResult result)
        {
            SetStatus(result.Message);
            if (_statusPunch != null)
            {
                _statusPunch.Play(
                    result.Success ? new Color(0.42f, 1f, 0.55f) : new Color(1f, 0.42f, 0.42f),
                    result.Success ? 1f : 0.4f);
            }
            RefreshBalance();
        }

        private void HandleStoreReady() => SetStatus("");

        private void RefreshBalance()
        {
            if (!AppManager.Exists) return;

            // The rollup owns the label's text when present, so don't write both.
            if (_balanceRollup != null) _balanceRollup.SetValue(AppManager.Instance.Chips.Balance);
            else if (_balanceLabel != null)
                _balanceLabel.text = $"{AppManager.Instance.Chips.Balance:N0}";
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null) _statusLabel.text = message;
        }
    }
}
