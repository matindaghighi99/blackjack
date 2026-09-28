using UnityEngine;

namespace BlackjackGame.UI.Layout
{
    /// <summary>
    /// Keeps a fixed-design column (the menu's brand and buttons, the store's pack list)
    /// inside its parent: when the parent is shorter or narrower than the design, the column
    /// scales down uniformly instead of clipping — a phone on its side still shows every
    /// control. Never scales up.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ScaleToFit : MonoBehaviour
    {
        [Tooltip("The column's design size, in canvas units.")]
        [SerializeField] private Vector2 _designSize = new Vector2(760f, 1200f);
        [Tooltip("Space kept free above and below (e.g. for a top bar), in canvas units.")]
        [SerializeField] private float _reservedHeight = 220f;

        private RectTransform _rect;
        private Vector2 _lastParent = new Vector2(-1f, -1f);

        private void OnEnable() => Fit(true);

        private void Update() => Fit(false);

        private void Fit(bool force)
        {
            if (_rect == null) _rect = (RectTransform)transform;
            var parent = _rect.parent as RectTransform;
            if (parent == null) return;

            Vector2 size = parent.rect.size;
            if (!force && (size - _lastParent).sqrMagnitude < 0.25f) return;
            _lastParent = size;
            if (size.x <= 1f || size.y <= 1f) return;

            float byHeight = (size.y - _reservedHeight) / Mathf.Max(1f, _designSize.y);
            float byWidth = (size.x - 32f) / Mathf.Max(1f, _designSize.x);
            float scale = Mathf.Clamp(Mathf.Min(byHeight, byWidth), 0.5f, 1f);
            _rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
