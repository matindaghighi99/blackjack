using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Shrinks this RectTransform to the device's safe area, so notches, punch-holes
    /// and the home-indicator strip never sit on top of live UI. The background stays
    /// full-bleed outside this rect; everything interactive lives inside it.
    ///
    /// Re-applies itself when the safe area changes (rotation, foldables, iPad stage
    /// resizing) rather than only once on load.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _applied = new Rect(-1f, -1f, -1f, -1f);

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            // Cheap comparison per frame; Screen.safeArea only changes on rare events.
            if (Screen.safeArea != _applied) Apply();
        }

        private void Apply()
        {
            Rect area = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
            _applied = area;
        }
    }
}
