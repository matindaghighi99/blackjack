using System;
using BlackjackGame.Core;
using BlackjackGame.Economy;
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

        [Header("Labels")]
        [SerializeField] private TMP_Text _balanceLabel;
        [SerializeField] private TMP_Text _rewardStatusLabel;

        private void Start()
        {
            if (_playButton != null) _playButton.onClick.AddListener(() => Load(SceneNames.Game));
            if (_storeButton != null) _storeButton.onClick.AddListener(() => Load(SceneNames.Store));
            if (_rewardsButton != null) _rewardsButton.onClick.AddListener(ClaimDailyReward);

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
            RefreshBalance();
        }

        private void RefreshBalance()
        {
            if (_balanceLabel != null && AppManager.Exists)
                _balanceLabel.text = $"{AppManager.Instance.Chips.Balance:N0}";
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
