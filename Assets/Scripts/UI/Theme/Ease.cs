using UnityEngine;

namespace BlackjackGame.UI.Theme
{
    /// <summary>
    /// Easing curves shared by every animated component, so motion across the table has
    /// one character: things decelerate into place (weight), accelerate away (release),
    /// and never move linearly.
    /// </summary>
    public static class Ease
    {
        public static float OutCubic(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u;
        }

        public static float OutQuint(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u * u * u;
        }

        public static float InCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>A small settle past the target and back — used sparingly, for landings.</summary>
        public static float OutBack(float t, float overshoot = 1.2f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        /// <summary>Frame-rate independent exponential approach toward a target.</summary>
        public static float Damp(float current, float target, float speed, float deltaTime) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * deltaTime));

        public static Vector2 Damp(Vector2 current, Vector2 target, float speed, float deltaTime) =>
            Vector2.Lerp(current, target, 1f - Mathf.Exp(-speed * deltaTime));
    }
}
