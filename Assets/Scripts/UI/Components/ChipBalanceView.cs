using BlackjackGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Drop-in reusable label that always shows the live chip balance. Attach to any Text
    /// and it self-subscribes to balance changes — reused across menu, table and store.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class ChipBalanceView : MonoBehaviour
    {
        [SerializeField] private string _format = "{0:N0}";
        private Text _label;

        private void Awake() => _label = GetComponent<Text>();

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
