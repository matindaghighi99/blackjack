using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// A small notification dot that breathes to draw the eye — pinned to the gift
    /// button when the daily reward is claimable. Show/Hide is all callers need;
    /// the pulse runs itself while visible.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class PulsingDot : MonoBehaviour
    {
        [Tooltip("Scale swing of the breath, as a fraction of resting size.")]
        [SerializeField] private float _pulseAmount = 0.18f;
        [SerializeField] private float _pulseSpeed = 3.4f;

        private Image _image;

        private void Awake() => _image = GetComponent<Image>();

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        private void OnEnable() => transform.localScale = Vector3.one;

        private void Update()
        {
            float s = 1f + _pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _pulseSpeed));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
