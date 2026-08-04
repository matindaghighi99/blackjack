using BlackjackGame.Config;
using BlackjackGame.Core;
using BlackjackGame.Economy;
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
        [Tooltip("Parent transform (e.g. a Vertical Layout Group) that pack buttons are added to.")]
        [SerializeField] private Transform _packListRoot;
        [Tooltip("A Button prefab with a child Text used as the row template.")]
        [SerializeField] private Button _packButtonPrefab;

        [Header("Display")]
        [SerializeField] private Text _balanceLabel;
        [SerializeField] private Text _statusLabel;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        private StoreManager _store;

        private void Start()
        {
            if (_backButton != null)
                _backButton.onClick.AddListener(() =>
                    UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.MainMenu));

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
            if (_packListRoot == null || _packButtonPrefab == null) return;

            foreach (ChipPack pack in _store.AvailablePacks)
            {
                ChipPack captured = pack; // avoid closure capture bug
                Button button = Instantiate(_packButtonPrefab, _packListRoot, false);
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
            SetStatus("Processing purchase…");
            _store.PurchasePack(packId); // result arrives via OnPurchaseCompleted
        }

        private void HandlePurchaseCompleted(PurchaseResult result)
        {
            SetStatus(result.Message);
            RefreshBalance();
        }

        private void HandleStoreReady() => SetStatus("");

        private void RefreshBalance()
        {
            if (_balanceLabel != null && AppManager.Exists)
                _balanceLabel.text = $"Balance: {AppManager.Instance.Chips.Balance:N0}";
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null) _statusLabel.text = message;
        }
    }
}
