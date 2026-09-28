using System;
using UnityEngine;

namespace BlackjackGame.UI.Theme
{
    /// <summary>
    /// The player's reduced-motion preference (Settings ▸ Reduce Motion).
    ///
    /// Unity has no cross-platform hook for the OS accessibility setting, so it is an
    /// in-game toggle. Every animated component reads it: travel, spin, arcs and swells are
    /// dropped and durations shortened, but short fades remain so state changes still read.
    /// </summary>
    public static class MotionPrefs
    {
        private const string PrefKey = "reduced_motion";

        private static bool? _reduced;

        /// <summary>Raised when the preference is toggled, with the new value.</summary>
        public static event Action<bool> Changed;

        public static bool Reduced
        {
            get
            {
                if (!_reduced.HasValue) _reduced = PlayerPrefs.GetInt(PrefKey, 0) == 1;
                return _reduced.Value;
            }
            set
            {
                if (_reduced.HasValue && _reduced.Value == value) return;
                _reduced = value;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke(value);
            }
        }

        /// <summary>Scales an animation duration for the current preference.</summary>
        public static float Duration(float seconds) => Reduced ? seconds * 0.35f : seconds;

        /// <summary>Scales a travel distance / overshoot / swell: none at all when reduced.</summary>
        public static float Amount(float value) => Reduced ? 0f : value;
    }
}
