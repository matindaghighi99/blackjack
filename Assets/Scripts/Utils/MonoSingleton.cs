using UnityEngine;

namespace BlackjackGame.Utils
{
    /// <summary>
    /// Reusable base class for persistent manager singletons (GameManager, ChipManager…).
    /// Survives scene loads and guards against duplicates.
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;
        public static T Instance => _instance;
        public static bool Exists => _instance != null;

        /// <summary>Set false in a subclass if it should not persist across scenes.</summary>
        protected virtual bool Persistent => true;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;
            if (Persistent)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            OnSingletonAwake();
        }

        /// <summary>Override for one-time initialisation instead of Awake().</summary>
        protected virtual void OnSingletonAwake() { }

        protected virtual void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
