using UnityEngine;

namespace Airships.Game
{
    // Walks when it's moving, and stands idle when it isn't, wherever it's being moved from.
    public class CompanionGait : MonoBehaviour
    {
        const float WalkSpeed = 0.2f; // Metres per second before the legs start.
        const float StopSeconds = 0.12f; // A brief pause in movement doesn't stop the walk.

        public KitCharacter Animation;
        public bool Busy; // A one-shot is playing, so walking and standing wait.

        Vector3 last;
        bool walking;
        bool started;
        float still;

        void LateUpdate()
        {
            if (Animation == null) return;
            var delta = Time.deltaTime > 0f ? Vector3.Distance(transform.position, last) / Time.deltaTime : 0f;
            last = transform.position;
            if (!started)
            {
                started = true;
                Animation.Loop("Idle");
                return;
            }
            if (Busy) return;

            still = delta > WalkSpeed ? 0f : still + Time.deltaTime;
            var shouldWalk = still < StopSeconds;
            if (shouldWalk == walking) return;
            walking = shouldWalk;
            Animation.Loop(walking ? "Walk" : "Idle");
        }
    }
}
