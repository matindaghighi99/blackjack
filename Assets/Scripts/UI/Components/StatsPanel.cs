using BlackjackGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Modal stats panel opened from the trophy icon on any screen. There is no server
    /// leaderboard, so this reflects the player's own local <c>PlayerData</c> — level,
    /// hands played, win rate, blackjacks, and daily-reward streak.
    /// </summary>
    public sealed class StatsPanel : MonoBehaviour
    {
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private TMP_Text _handsLabel;
        [SerializeField] private TMP_Text _winRateLabel;
        [SerializeField] private TMP_Text _blackjacksLabel;
        [SerializeField] private TMP_Text _streakLabel;

        private void Awake()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        public void Show()
        {
            Refresh();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void Refresh()
        {
            if (!AppManager.Exists) return;
            var data = AppManager.Instance.Profile.Data;

            if (_levelLabel != null) _levelLabel.text = $"Level {data.Level}";
            if (_handsLabel != null) _handsLabel.text = $"Hands played: {data.HandsPlayed:N0}";
            if (_winRateLabel != null) _winRateLabel.text = $"Win rate: {data.WinRate * 100f:0.#}%";
            if (_blackjacksLabel != null) _blackjacksLabel.text = $"Blackjacks: {data.Blackjacks:N0}";
            if (_streakLabel != null) _streakLabel.text = $"Daily streak: {data.DailyStreak}";
        }
    }
}
