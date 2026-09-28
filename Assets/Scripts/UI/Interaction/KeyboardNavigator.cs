using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackjackGame.UI.Interaction
{
    /// <summary>
    /// Keyboard access for a screen, layered on UGUI's own navigation:
    ///
    /// * Tab / Shift+Tab cycle through every usable control in reading order (UGUI has no
    ///   Tab support of its own); arrow keys keep using UGUI's spatial navigation.
    /// * While a modal panel is open, focus stays inside it and Escape closes it.
    /// * If the focused control disappears (a control row swaps out mid-round), focus moves
    ///   to the control the screen marked as <see cref="Preferred"/> rather than being lost.
    /// * Tracks keyboard vs pointer use so focus rings only show for keyboard users.
    /// </summary>
    public sealed class KeyboardNavigator : MonoBehaviour
    {
        [Tooltip("Modal roots (settings, stats…). While one is active, Tab stays inside it and Escape closes it.")]
        [SerializeField] private GameObject[] _modals;
        [Tooltip("Focused when keyboard navigation starts and nothing better is available.")]
        [SerializeField] private Selectable _defaultSelection;

        private static Selectable[] _buffer = new Selectable[64];
        private readonly List<Selectable> _ordered = new List<Selectable>();
        private Vector3 _lastMouse;

        /// <summary>The control that should take focus when the current one goes away.</summary>
        public Selectable Preferred { get; set; }

        /// <summary>The open modal panel, if any.</summary>
        public GameObject ActiveModal
        {
            get
            {
                if (_modals == null) return null;
                foreach (GameObject m in _modals)
                    if (m != null && m.activeInHierarchy) return m;
                return null;
            }
        }

        private void Start() => _lastMouse = UnityEngine.Input.mousePosition;

        private void Update()
        {
            TrackModality();

            EventSystem es = EventSystem.current;
            if (es == null) return;
            GameObject modal = ActiveModal;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) && modal != null)
            {
                modal.SetActive(false);
                es.SetSelectedGameObject(null);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                bool back = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
                Cycle(es, modal, back ? -1 : 1);
                return;
            }

            if (!InputModality.Keyboard) return;

            // Keep focus on something real: a hidden or disabled control can't be activated.
            GameObject current = es.currentSelectedGameObject;
            Selectable selected = current != null ? current.GetComponent<Selectable>() : null;
            if (selected == null || !IsUsable(selected, modal))
            {
                Selectable next = Fallback(modal);
                if (next != null && (selected == null || next != selected)) es.SetSelectedGameObject(next.gameObject);
            }
        }

        private void TrackModality()
        {
            Vector3 mouse = UnityEngine.Input.mousePosition;
            if (UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetMouseButtonDown(1) ||
                UnityEngine.Input.touchCount > 0 || (mouse - _lastMouse).sqrMagnitude > 16f)
            {
                InputModality.NotePointer();
            }
            _lastMouse = mouse;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab) ||
                UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) || UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) ||
                UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                InputModality.NoteKeyboard();
            }
        }

        private void Cycle(EventSystem es, GameObject modal, int direction)
        {
            Collect(modal);
            if (_ordered.Count == 0) return;

            GameObject current = es.currentSelectedGameObject;
            int index = -1;
            for (int i = 0; i < _ordered.Count; i++)
                if (current != null && _ordered[i].gameObject == current) index = i;

            int next = index < 0
                ? (direction > 0 ? 0 : _ordered.Count - 1)
                : (index + direction + _ordered.Count) % _ordered.Count;
            es.SetSelectedGameObject(_ordered[next].gameObject);
        }

        private Selectable Fallback(GameObject modal)
        {
            if (modal == null)
            {
                if (IsUsable(Preferred, null)) return Preferred;
                if (IsUsable(_defaultSelection, null)) return _defaultSelection;
            }
            Collect(modal);
            return _ordered.Count > 0 ? _ordered[0] : null;
        }

        /// <summary>Usable controls in scope, top-to-bottom then left-to-right.</summary>
        private void Collect(GameObject modal)
        {
            _ordered.Clear();
            int count = Selectable.allSelectableCount;
            if (_buffer.Length < count) _buffer = new Selectable[count * 2];
            int n = Selectable.AllSelectablesNoAlloc(_buffer);
            for (int i = 0; i < n; i++)
            {
                Selectable s = _buffer[i];
                if (IsUsable(s, modal)) _ordered.Add(s);
            }
            _ordered.Sort(ReadingOrder);
        }

        private static int ReadingOrder(Selectable a, Selectable b)
        {
            Vector3 pa = a.transform.position;
            Vector3 pb = b.transform.position;
            // Same row if the centres are close vertically (world units scale with the
            // canvas, so compare against each control's own height).
            float tolerance = ((RectTransform)a.transform).rect.height * a.transform.lossyScale.y * 0.5f;
            if (Mathf.Abs(pa.y - pb.y) > tolerance) return pb.y.CompareTo(pa.y);
            return pa.x.CompareTo(pb.x);
        }

        private static bool IsUsable(Selectable s, GameObject modal)
        {
            if (s == null || !s.isActiveAndEnabled || !s.IsInteractable()) return false;
            return modal == null || s.transform.IsChildOf(modal.transform);
        }
    }
}
