using BlackjackGame.Core;
using TMPro;
using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Drop-in reusable label that always shows the live chip balance. Attach to any TextMeshPro label
    /// and it self-subscribes to balance changes — reused across menu, table and store.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class ChipBalanceView : MonoBehaviour
    {
        [SerializeField] private string _format = "{0:N0}";
        private TMP_Text _label;

        private void Awake() => _label = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            if (!AppManager.Exists) return;
            AppManager.Instance.Chips.OnBalanceChanged += Render;
            Render(AppManager.Instance.Chips.Balance);
        }

        private void OnDisable()
        {
            if (AppManager.Exists)
                AppManager.Instance.Chips.OnBalanceChanged -= Render;
        }

        private void Render(long balance) => _label.text = string.Format(_format, balance);
    }
}
