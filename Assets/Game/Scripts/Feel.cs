using System;
using System.Collections;
using UnityEngine;

namespace Airships.Game
{
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        public static float OutBack(float t)
        {
            const float overshoot = 1.70158f;
            var inverse = t - 1f;
            return 1f + (overshoot + 1f) * inverse * inverse * inverse + overshoot * inverse * inverse;
        }

        public static IEnumerator Tween(float duration, Action<float> apply, Func<float, float> ease = null, bool unscaled = true)
        {
            ease ??= OutCubic;
            for (var t = 0f; t < 1f; t += (unscaled ? Time.unscaledDeltaTime : Time.deltaTime) / duration)
            {
                apply(ease(t));
                yield return null;
            }
            apply(1f);
        }
    }

    // The only code allowed to change Time.timeScale, so pausing and hit-stop can never fight.
    public static class GameTime
    {
        static bool paused;
        static float hitStopUntil;

        public static bool Paused
        {
            get => paused;
            set
            {
                paused = value;
                Refresh();
            }
        }

        public static float SpeedMultiplier { get; set; } = 1f;

        public static void HitStop(float seconds)
        {
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
            Refresh();
        }

        public static void Tick()
        {
            if (hitStopUntil > 0f && Time.unscaledTime >= hitStopUntil)
            {
                hitStopUntil = 0f;
                Refresh();
            }
        }

        public static void Reset()
        {
            paused = false;
            hitStopUntil = 0f;
            Refresh();
        }

        static void Refresh()
        {
            if (paused) Time.timeScale = 0f;
            else if (hitStopUntil > 0f) Time.timeScale = 0.05f;
            else Time.timeScale = SpeedMultiplier;
        }
    }
}
