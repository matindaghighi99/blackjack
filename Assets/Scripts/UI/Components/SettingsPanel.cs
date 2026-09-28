using BlackjackGame.Core;
using BlackjackGame.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Modal settings panel opened from the gear icon on any screen. Self-contained:
    /// toggles its own GameObject's active state, so Show()/Hide() are all a caller
    /// needs to wire up.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private const string MutedPrefKey = "audio_muted";

        [Tooltip("The dimmed layer behind the frame; tapping it dismisses the panel.")]
        [SerializeField] private Button _backdropButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _muteButton;
        [SerializeField] private TMP_Text _muteLabel;
        [Tooltip("Toggles MotionPrefs.Reduced — shorter, travel-free animation everywhere.")]
        [SerializeField] private Button _motionButton;
        [SerializeField] private TMP_Text _motionLabel;
        [SerializeField] private Button _resetButton;
        [SerializeField] private TMP_Text _resetLabel;

        /// <summary>Armed by a first tap on Reset; a second tap while armed executes it.
        /// Guards against wiping a save from a single mis-tap.</summary>
        private bool _resetConfirmArmed;

        private void Awake()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            // Tapping outside the frame closes the panel — the gesture everyone tries first.
            if (_backdropButton != null) _backdropButton.onClick.AddListener(Hide);
            if (_muteButton != null) _muteButton.onClick.AddListener(ToggleMute);
            if (_motionButton != null) _motionButton.onClick.AddListener(ToggleMotion);
            if (_resetButton != null) _resetButton.onClick.AddListener(OnResetClicked);

            ApplyMuteState(PlayerPrefs.GetInt(MutedPrefKey, 0) == 1);
            RefreshMotionLabel();
        }

        public void Show()
        {
            DisarmReset();
            RefreshMotionLabel();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void ToggleMute()
        {
            bool currentlyMuted = PlayerPrefs.GetInt(MutedPrefKey, 0) == 1;
            ApplyMuteState(!currentlyMuted);
        }

        private void ApplyMuteState(bool muted)
        {
            PlayerPrefs.SetInt(MutedPrefKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = muted ? 0f : 1f;
            if (_muteLabel != null) _muteLabel.text = muted ? "SOUND  ·  OFF" : "SOUND  ·  ON";
        }

        private void ToggleMotion()
        {
            MotionPrefs.Reduced = !MotionPrefs.Reduced;
            RefreshMotionLabel();
        }

        private void RefreshMotionLabel()
        {
            if (_motionLabel != null)
                _motionLabel.text = MotionPrefs.Reduced ? "REDUCE MOTION  ·  ON" : "REDUCE MOTION  ·  OFF";
        }

        private void OnResetClicked()
        {
            if (!_resetConfirmArmed)
            {
                _resetConfirmArmed = true;
                if (_resetLabel != null) _resetLabel.text = "TAP AGAIN TO CONFIRM";
                return;
            }

            DisarmReset();

            if (!AppManager.Exists) return;
            AppManager.Instance.Profile.ResetProgress(AppManager.Instance.EconomyConfig.StartingChips);
            Hide();
        }

        private void DisarmReset()
        {
            _resetConfirmArmed = false;
            if (_resetLabel != null) _resetLabel.text = "RESET PROGRESS";
        }
    }
}
