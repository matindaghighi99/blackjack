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
    ///
    /// The daily-reward state is live: a pulsing dot on the gift button while a claim is
    /// waiting, and a countdown that ticks by the second when it isn't — a menu that
    /// says "come back later" without saying when is just a locked door.
    /// </summary>
    public sealed class MainMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _storeButton;
        [SerializeField] private Button _rewardsButton;
        [Tooltip("The + on the balance pill — shortcut to the chip store.")]
        [SerializeField] private Button _addChipsButton;

        [Header("Quick Actions (top bar)")]
        [SerializeField] private Button _giftButton;
        [SerializeField] private PulsingDot _giftDot;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _trophyButton;
        [SerializeField] private SettingsPanel _settingsPanel;
        [SerializeField] private StatsPanel _statsPanel;

        [Header("Labels")]
        [SerializeField] private TMP_Text _balanceLabel;
        [SerializeField] private TMP_Text _rewardStatusLabel;
        [Tooltip("The subtitle inside the DAILY REWARDS row; mirrors the reward state.")]
        [SerializeField] private TMP_Text _rewardsRowSubtitle;

        [Header("Juice")]
        [Tooltip("Rolls the balance up instead of snapping it.")]
        [SerializeField] private CountRollup _balanceRollup;
        [Tooltip("Slams the reward status line in when a claim lands.")]
        [SerializeField] private LabelPunch _rewardPunch;

        /// <summary>Countdown refresh bookkeeping — update once a second, not once a frame.</summary>
        private float _nextStatusRefresh;

        private void Start()
        {
            if (_playButton != null) _playButton.onClick.AddListener(() => Load(SceneNames.Game));
            if (_storeButton != null) _storeButton.onClick.AddListener(() => Load(SceneNames.Store));
            if (_addChipsButton != null) _addChipsButton.onClick.AddListener(() => Load(SceneNames.Store));
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

        private void Update()
        {
            // Tick the countdown / readiness once a second. Cheap, and it means the
            // reward flipping to "ready" while the menu is open is actually seen.
            if (Time.unscaledTime < _nextStatusRefresh) return;
            _nextStatusRefresh = Time.unscaledTime + 1f;
            RefreshRewardStatus();
        }

        private void ClaimDailyReward()
        {
            if (!AppManager.Exists) return;

            DailyRewardResult result = AppManager.Instance.Rewards.TryClaim(DateTime.UtcNow);
            if (_rewardStatusLabel != null)
            {
                _rewardStatusLabel.text = result.Success
                    ? $"+{result.ChipsAwarded:N0} chips! Streak: {result.NewStreak}"
                    : NextRewardText(result.TimeUntilNext);
            }

            // Gold slam on a successful claim; a flat grey nudge when it's not ready yet.
            if (_rewardPunch != null)
            {
                _rewardPunch.Play(
                    result.Success ? new Color(1f, 0.86f, 0.35f) : new Color(0.8f, 0.8f, 0.8f),
                    result.Success ? 1f : 0.3f);
            }

            RefreshBalance();
            RefreshRewardWidgets(AppManager.Instance.Rewards.IsRewardAvailable(DateTime.UtcNow));

            // Hold the claim message on screen; the ticking countdown would overwrite it
            // within a second otherwise.
            _nextStatusRefresh = Time.unscaledTime + 3.5f;
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
            if (!AppManager.Exists) return;

            bool available = AppManager.Instance.Rewards.IsRewardAvailable(DateTime.UtcNow);

            if (_rewardStatusLabel != null)
            {
                if (available)
                {
                    _rewardStatusLabel.text = "Daily reward ready!";
                }
                else
                {
                    TimeSpan wait = AppManager.Instance.Rewards.TimeUntilNext(DateTime.UtcNow);
                    _rewardStatusLabel.text = NextRewardText(wait);
                }
            }

            RefreshRewardWidgets(available);
        }

        private void RefreshRewardWidgets(bool available)
        {
            if (_giftDot != null)
            {
                if (available) _giftDot.Show();
                else _giftDot.Hide();
            }

            if (_rewardsRowSubtitle != null)
                _rewardsRowSubtitle.text = available ? "READY TO CLAIM!" : "COME BACK TOMORROW";
        }

        private static string NextRewardText(TimeSpan wait)
        {
            if (wait.TotalMinutes < 1) return "Reward ready in under a minute";
            return wait.TotalHours >= 1
                ? $"Ready in {(int)wait.TotalHours}h {wait.Minutes}m"
                : $"Ready in {wait.Minutes}m";
        }

        private void Load(string scene)
        {
            if (GameManager.Exists) GameManager.Instance.GoToScene(scene);
            else SceneFader.TransitionTo(scene);
        }
    }
}
