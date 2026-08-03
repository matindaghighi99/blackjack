using BlackjackGame.Config;
using BlackjackGame.Core;
using BlackjackGame.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Screens
{
    /// <summary>
    /// Mock store screen. Spawns a button per chip pack from EconomyConfig. Purchases are
    /// simulated (no real billing) — see <see cref="StoreManager"/> for the TODO to wire
    /// real IAP before release.
    /// </summary>
    public sealed class StoreUI : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Parent transform (e.g. a Vertical Layout Group) that pack buttons are added to.")]
        [SerializeField] private Transform _packListRoot;
        [Tooltip("A Button prefab with a child Text used as the row template.")]
        [SerializeField] private Button _packButtonPrefab;

        [Header("Display")]
        [SerializeField] private Text _balanceLabel;
        [SerializeField] private Text _statusLabel;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        private void Start()
        {
            if (_backButton != null)
                _backButton.onClick.AddListener(() =>
                    UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.MainMenu));

            BuildPackList();
            RefreshBalance();

            if (AppManager.Exists)
                AppManager.Instance.Chips.OnBalanceChanged += _ => RefreshBalance();
        }

        private void BuildPackList()
        {
            if (!AppManager.Exists || _packListRoot == null || _packButtonPrefab == null) return;

            foreach (ChipPack pack in AppManager.Instance.Store.AvailablePacks)
            {
                ChipPack captured = pack; // avoid closure capture bug
                Button button = Instantiate(_packButtonPrefab, _packListRoot);
                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    int total = captured.ChipAmount + captured.BonusChips;
                    string bonus = captured.BonusChips > 0 ? $" (+{captured.BonusChips:N0} bonus)" : "";
                    label.text = $"{captured.DisplayName} — {total:N0} chips{bonus}   {captured.PriceLabel}";
                }
                button.onClick.AddListener(() => Purchase(captured.Id));
            }
        }

        private void Purchase(string packId)
        {
            PurchaseResult result = AppManager.Instance.Store.PurchasePack(packId);
            if (_statusLabel != null) _statusLabel.text = result.Message;
            RefreshBalance();
        }

        private void RefreshBalance()
        {
            if (_balanceLabel != null && AppManager.Exists)
                _balanceLabel.text = $"Balance: {AppManager.Instance.Chips.Balance:N0}";
        }
    }
}
