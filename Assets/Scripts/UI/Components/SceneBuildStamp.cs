using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Records which version of <c>Blackjack ▸ Build UI Scenes</c> produced a scene.
    ///
    /// The scenes are generated from code, so a scene saved by an older generator can be
    /// missing objects the current scripts expect. The editor compares this stamp on load
    /// and offers to rebuild; at runtime a mismatch is reported instead of failing quietly.
    /// </summary>
    public sealed class SceneBuildStamp : MonoBehaviour
    {
        /// <summary>Bump whenever the generated scene structure changes.</summary>
        public const int CurrentVersion = 2;

        [SerializeField] private int _sceneBuildVersion;

        public int Version => _sceneBuildVersion;

        private void Awake()
        {
            if (_sceneBuildVersion != CurrentVersion)
            {
                Debug.LogWarning(
                    $"[SceneBuildStamp] '{gameObject.scene.name}' was built by scene layout v{_sceneBuildVersion}, " +
                    $"scripts expect v{CurrentVersion}. Run Blackjack ▸ Build UI Scenes.");
            }
        }
    }
}
