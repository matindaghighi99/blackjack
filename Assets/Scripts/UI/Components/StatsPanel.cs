using BlackjackGame.Core;
using BlackjackGame.UI.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Modal stats panel opened from the trophy icon on any screen. There is no server
    /// leaderboard, so this reflects the player's own local <c>PlayerData</c> — level,
    /// hands played, win rate, blackjacks, and daily-reward streak. Each field is the value
    /// half of a caption/value row; the captions are static text in the scene.
    /// </summary>
    public sealed class StatsPanel : MonoBehaviour
    {
        [Tooltip("The dimmed layer behind the frame; tapping it dismisses the panel.")]
        [SerializeField] private Button _backdropButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private TMP_Text _handsLabel;
        [SerializeField] private TMP_Text _winRateLabel;
        [SerializeField] private TMP_Text _blackjacksLabel;
        [SerializeField] private TMP_Text _streakLabel;

        private void Awake()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            // Tapping outside the frame closes the panel — the gesture everyone tries first.
            if (_backdropButton != null) _backdropButton.onClick.AddListener(Hide);
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

            var culture = TableText.Culture;
            if (_levelLabel != null) _levelLabel.text = data.Level.ToString(culture);
            if (_handsLabel != null) _handsLabel.text = TableText.Chips(data.HandsPlayed);
            if (_winRateLabel != null) _winRateLabel.text = (data.WinRate * 100f).ToString("0.#", culture) + "%";
            if (_blackjacksLabel != null) _blackjacksLabel.text = TableText.Chips(data.Blackjacks);
            if (_streakLabel != null)
                _streakLabel.text = data.DailyStreak == 1 ? "1 day" : $"{data.DailyStreak.ToString(culture)} days";
        }
    }
}
