using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackjackGame.UI.Interaction
{
    /// <summary>
    /// Desktop shortcuts for the table: H hit · S stand · D double · P split · R surrender ·
    /// 1–9 chips · Backspace clear · Space/Enter deal.
    ///
    /// Every shortcut is routed through the very same Button a tap would press, and only
    /// when that button is visible and interactable — so a key can never do anything the
    /// screen would not let a finger do at that moment.
    ///
    /// Space and Enter are also Unity's Submit keys; they only deal when no control has
    /// focus, otherwise Submit already presses the focused control.
    /// </summary>
    public sealed class TableShortcuts : MonoBehaviour
    {
        [SerializeField] private KeyboardNavigator _navigator;
        [SerializeField] private Button _deal;
        [SerializeField] private Button _hit;
        [SerializeField] private Button _stand;
        [SerializeField] private Button _double;
        [SerializeField] private Button _split;
        [SerializeField] private Button _surrender;
        [SerializeField] private Button _clear;
        [Tooltip("Chip rack buttons, smallest to largest (keys 1–9).")]
        [SerializeField] private Button[] _chips;

        private void Update()
        {
            if (!UnityEngine.Input.anyKeyDown) return;
            if (_navigator != null && _navigator.ActiveModal != null) return;

            if (Down(KeyCode.H)) Press(_hit);
            else if (Down(KeyCode.S)) Press(_stand);
            else if (Down(KeyCode.D)) Press(_double);
            else if (Down(KeyCode.P)) Press(_split);
            else if (Down(KeyCode.R)) Press(_surrender);
            else if (Down(KeyCode.Backspace) || Down(KeyCode.Delete)) Press(_clear);
            else if (Down(KeyCode.Space) || Down(KeyCode.Return) || Down(KeyCode.KeypadEnter))
            {
                EventSystem es = EventSystem.current;
                if (es == null || es.currentSelectedGameObject == null) Press(_deal);
            }
            else if (_chips != null)
            {
                for (int i = 0; i < _chips.Length && i < 9; i++)
                {
                    if (Down(KeyCode.Alpha1 + i) || Down(KeyCode.Keypad1 + i))
                    {
                        Press(_chips[i]);
                        break;
                    }
                }
            }
        }

        private static bool Down(KeyCode key) => UnityEngine.Input.GetKeyDown(key);

        private static void Press(Button button)
        {
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable()) return;
            button.onClick.Invoke();
        }
    }
}
