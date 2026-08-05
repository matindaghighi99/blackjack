using System;
using BlackjackGame.Core;
using BlackjackGame.Economy;
using BlackjackGame.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Screens
{
    /// <summary>
    /// Main menu: Play, Store and Rewards. Wire the buttons and labels in the inspector.
    /// Deliberately thin — it only routes intent to the managers.
    /// </summary>
    public sealed class MainMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _storeButton;
        [SerializeField] private Button _rewardsButton;

        [Header("Quick Actions (top bar)")]
        [SerializeField] private Button _giftButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _trophyButton;
        [SerializeField] private SettingsPanel _settingsPanel;
        [SerializeField] private StatsPanel _statsPanel;

        [Header("Labels")]
        [SerializeField] private TMP_Text _balanceLabel;
        [SerializeField] private TMP_Text _rewardStatusLabel;

        [Header("Juice")]
        [Tooltip("Rolls the balance up instead of snapping it.")]
        [SerializeField] private CountRollup _balanceRollup;
        [Tooltip("Slams the reward status line in when a claim lands.")]
        [SerializeField] private LabelPunch _rewardPunch;

        private void Start()
        {
            if (_playButton != null) _playButton.onClick.AddListener(() => Load(SceneNames.Game));
            if (_storeButton != null) _storeButton.onClick.AddListener(() => Load(SceneNames.Store));
            if (_rewardsButton != null) _rewardsButton.onClick.AddListener(ClaimDailyReward);

            // The top-bar gift icon is a shortcut to the same claim flow as the big
            // DAILY REWARDS row — no separate logic to keep in sync.
            if (_giftButton != null) _giftButton.onClick.AddListener(ClaimDailyReward);
            if (_settingsButton != null && _settingsPanel != null)
                _settingsButton.onClick.AddListener(_settingsPanel.Show);
            if (_trophyButton != null && _statsPanel != null)
                _trophyButton.onClick.AddListener(_statsPanel.Show);

            RefreshBalance();
            RefreshRewardStatus();

            if (AppManager.Exists)
                AppManager.Instance.Chips.OnBalanceChanged += _ => RefreshBalance();
        }

        private void ClaimDailyReward()
        {
            if (!AppManager.Exists) return;

            DailyRewardResult result = AppManager.Instance.Rewards.TryClaim(DateTime.UtcNow);
            if (_rewardStatusLabel != null)
            {
                _rewardStatusLabel.text = result.Success
                    ? $"+{result.ChipsAwarded:N0} chips! Streak: {result.NewStreak}"
                    : $"Next reward in {result.TimeUntilNext.Hours}h {result.TimeUntilNext.Minutes}m";
            }

            // Gold slam on a successful claim; a flat grey nudge when it's not ready yet.
            if (_rewardPunch != null)
            {
                _rewardPunch.Play(
                    result.Success ? new Color(1f, 0.86f, 0.35f) : new Color(0.8f, 0.8f, 0.8f),
                    result.Success ? 1f : 0.3f);
            }

            RefreshBalance();
        }

        private void RefreshBalance()
        {
            if (!AppManager.Exists) return;

            // The rollup owns the label's text when present, so don't write both.
            if (_balanceRollup != null) _balanceRollup.SetValue(AppManager.Instance.Chips.Balance);
            else if (_balanceLabel != null) _balanceLabel.text = $"{AppManager.Instance.Chips.Balance:N0}";
        }

        private void RefreshRewardStatus()
        {
            if (_rewardStatusLabel == null || !AppManager.Exists) return;
            bool available = AppManager.Instance.Rewards.IsRewardAvailable(DateTime.UtcNow);
            _rewardStatusLabel.text = available ? "Daily reward ready!" : "Come back later for your reward.";
        }

        private void Load(string scene)
        {
            if (GameManager.Exists) GameManager.Instance.GoToScene(scene);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }
    }
}
