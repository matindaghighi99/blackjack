using BlackjackGame.Blackjack.Rules;
using BlackjackGame.Config;
using BlackjackGame.Economy;
using BlackjackGame.Economy.IAP;
using BlackjackGame.Player;
using BlackjackGame.Utils;
using UnityEngine;

namespace BlackjackGame.Core
{
    /// <summary>
    /// Application composition root. Lives for the whole app lifetime, owns the shared
    /// services (profile, economy, config) and hands them to whichever scene is active.
    /// This is where dependencies are wired once, keeping everything else decoupled.
    ///
    /// Place a single AppManager in the MainMenu scene and assign the config assets.
    /// </summary>
    public sealed class AppManager : MonoSingleton<AppManager>
    {
        [Header("Configuration Assets")]
        [SerializeField] private GameConfig _gameConfig;
        [SerializeField] private EconomyConfig _economyConfig;

        // Shared services (constructed once at boot).
        public GameConfig GameConfig => _gameConfig;
        public EconomyConfig EconomyConfig => _economyConfig;
        public PlayerProfile Profile { get; private set; }
        public ChipManager Chips { get; private set; }
        public RewardSystem Rewards { get; private set; }
        public StoreManager Store { get; private set; }

        protected override void OnSingletonAwake()
        {
            Bootstrap();
        }

        private void Bootstrap()
        {
            if (_economyConfig == null || _gameConfig == null)
            {
                Debug.LogError("[AppManager] Config assets not assigned. Assign GameConfig & EconomyConfig in the inspector.");
                return;
            }

            Profile = PlayerProfile.LoadOrCreate(_economyConfig.StartingChips);
            Chips = new ChipManager(Profile);
            Rewards = new RewardSystem(_economyConfig, Profile, Chips);
            Store = new StoreManager(_economyConfig, Chips, CreatePurchaseService());

            Debug.Log($"[AppManager] Booted. Player {Profile.Data.DisplayName} with {Chips.Balance:N0} chips.");
        }

        /// <summary>Factory for the configured rule set. Extend as new variants are added.</summary>
        public IRuleSet CreateRuleSet()
        {
            return _gameConfig.DefaultRuleVariant switch
            {
                RuleVariant.European => new EuropeanRules(),
                _ => new ClassicRules()
            };
        }

        /// <summary>
        /// Chooses the billing backend: real Unity IAP on device (when the package is
        /// installed), a mock everywhere else (editor / no package) so the store is always
        /// testable. Receipts are validated server-side unless disabled in EconomyConfig.
        /// </summary>
        private IPurchaseService CreatePurchaseService()
        {
#if UNITY_PURCHASING && !UNITY_EDITOR
            IReceiptValidator validator = _economyConfig.UseServerReceiptValidation
                ? new BackendReceiptValidator(_economyConfig.BackendBaseUrl, () => Profile.Data.PlayerId)
                : (IReceiptValidator)new NoOpReceiptValidator();
            return new UnityIapService(validator);
#else
            return new MockPurchaseService();
#endif
        }

        protected override void OnDestroy()
        {
            Store?.Dispose();
            base.OnDestroy();
        }
    }
}
