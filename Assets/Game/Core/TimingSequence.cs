using System;
using System.Collections.Generic;

namespace Airships.Core
{
    // One step in a timed pattern. Its ring appears Ring seconds before the moment At, so a short Ring
    // is a fast target. X and Y place it on screen, from 0 to 1 across the prompt area.
    [Serializable]
    public class TimingBeat
    {
        public float at;
        public float ring;
        public float x;
        public float y;
        public string kind; // "tap" (the default), "swipe" or "drag".
        public string dir; // Swipes only: "up", "down", "left" or "right".
        public float endX; // Drags only: where the marker travels to.
        public float endY;
        public float duration; // Drags only: seconds the marker takes to travel.

        public bool IsSwipe => kind == "swipe";
        public bool IsDrag => kind == "drag";
        public float End => IsDrag ? at + duration : at;
        public float DirX => dir == "left" ? -1f : dir == "right" ? 1f : 0f;
        public float DirY => dir == "down" ? -1f : dir == "up" ? 1f : 0f;
    }

    [Serializable]
    public class TimingPattern
    {
        public string id;
        public string role; // "attack", "defend" or "any".
        public int minTier;
        public TimingBeat[] beats;

        public float Length => beats[beats.Length - 1].End;
    }

    // Grades one play of a pattern from pointer input, with no dependency on Unity. Pointer positions
    // are in the prompt area's own units, from (0, 0) at the bottom left to (width, height).
    //
    // Tap: press on the target as its ring closes.
    // Swipe: press on the target, then flick the shown way so the flick finishes as the ring closes.
    // Drag: press as the ring closes, then keep the finger on the marker until it reaches the end.
    //
    // Every step must succeed, in order. One failure fails the pattern, and the weakest step sets the grade.
    public class TimingSequence
    {
        // Presses this far ahead of the first step are ignored, so a double-tap on a menu button doesn't
        // fail the pattern. Once it's underway, every press counts.
        const float IgnoreBeforeFirst = 0.4f;
        const float EarliestSwipePress = 0.35f;
        const float DragLiftGrace = 0.9f;

        readonly TimingDef def;
        readonly float width;
        readonly float height;
        TimingGrade weakest = TimingGrade.Perfect;
        TimingGrade dragGrade;
        float pointerX;
        float pointerY;
        float pressX;
        float pressY;

        public TimingPattern Pattern { get; }
        public float Elapsed { get; private set; }
        public int NextBeat { get; private set; }
        public bool Done { get; private set; }
        public TimingGrade Grade { get; private set; } = TimingGrade.Miss;
        public TimingGrade LastStepGrade { get; private set; } = TimingGrade.Miss;
        public bool IsDragging { get; private set; }
        public bool IsSwipeArmed { get; private set; }

        public TimingSequence(TimingPattern pattern, TimingDef def, float width, float height)
        {
            Pattern = pattern;
            this.def = def;
            this.width = width;
            this.height = height;
        }

        TimingBeat Current => Pattern.beats[NextBeat];

        public void Advance(float seconds)
        {
            if (Done) return;
            Elapsed += seconds;
            var beat = Current;

            if (IsDragging)
            {
                var progress = DragProgress(beat);
                MarkerPosition(beat, progress, out var markerX, out var markerY);
                if (Distance(pointerX, pointerY, markerX, markerY) > def.dragTolerance) Finish(TimingGrade.Miss);
                else if (progress >= 1f) CompleteStep(dragGrade);
                return;
            }

            if (Elapsed > beat.at + def.goodWindow) Finish(TimingGrade.Miss);
        }

        public void PointerDown(float x, float y)
        {
            if (Done || IsDragging) return;
            var beat = Current;
            if (NextBeat == 0 && Elapsed < beat.at - IgnoreBeforeFirst) return;

            pointerX = x;
            pointerY = y;
            if (Distance(x, y, beat.x * width, beat.y * height) > def.targetRadius)
            {
                Finish(TimingGrade.Miss);
                return;
            }

            if (beat.IsSwipe)
            {
                if (Elapsed < beat.at - EarliestSwipePress)
                {
                    Finish(TimingGrade.Miss);
                    return;
                }
                IsSwipeArmed = true;
                pressX = x;
                pressY = y;
                return;
            }

            var grade = TimingOf(beat);
            if (grade == TimingGrade.Miss)
            {
                Finish(TimingGrade.Miss);
            }
            else if (beat.IsDrag)
            {
                IsDragging = true;
                dragGrade = grade;
            }
            else
            {
                CompleteStep(grade);
            }
        }

        public void PointerMove(float x, float y)
        {
            if (Done) return;
            pointerX = x;
            pointerY = y;
            if (!IsSwipeArmed) return;

            var dx = x - pressX;
            var dy = y - pressY;
            var travelled = (float)Math.Sqrt(dx * dx + dy * dy);
            if (travelled < def.swipeDistance) return;

            var beat = Current;
            IsSwipeArmed = false;
            var cosine = (dx * beat.DirX + dy * beat.DirY) / travelled;
            var allowed = (float)Math.Cos(def.swipeAngleDegrees * Math.PI / 180.0);
            var grade = cosine >= allowed ? TimingOf(beat) : TimingGrade.Miss;
            if (grade == TimingGrade.Miss) Finish(TimingGrade.Miss);
            else CompleteStep(grade);
        }

        public void PointerUp()
        {
            if (Done) return;
            if (IsSwipeArmed)
            {
                Finish(TimingGrade.Miss);
            }
            else if (IsDragging)
            {
                if (DragProgress(Current) >= DragLiftGrace) CompleteStep(dragGrade);
                else Finish(TimingGrade.Miss);
            }
        }

        // Where a drag's marker is right now, in the prompt area's units.
        public void CurrentMarker(out float x, out float y)
        {
            var beat = Current;
            MarkerPosition(beat, DragProgress(beat), out x, out y);
        }

        float DragProgress(TimingBeat beat)
        {
            var progress = (Elapsed - beat.at) / Math.Max(0.01f, beat.duration);
            return progress < 0f ? 0f : progress > 1f ? 1f : progress;
        }

        void MarkerPosition(TimingBeat beat, float progress, out float x, out float y)
        {
            x = (beat.x + (beat.endX - beat.x) * progress) * width;
            y = (beat.y + (beat.endY - beat.y) * progress) * height;
        }

        TimingGrade TimingOf(TimingBeat beat)
        {
            var error = Math.Abs(Elapsed - beat.at);
            return error <= def.perfectWindow ? TimingGrade.Perfect : error <= def.goodWindow ? TimingGrade.Good : TimingGrade.Miss;
        }

        void CompleteStep(TimingGrade grade)
        {
            LastStepGrade = grade;
            if (grade < weakest) weakest = grade;
            IsDragging = false;
            IsSwipeArmed = false;
            NextBeat++;
            if (NextBeat >= Pattern.beats.Length) Finish(weakest);
        }

        void Finish(TimingGrade grade)
        {
            Done = true;
            Grade = grade;
            IsDragging = false;
            IsSwipeArmed = false;
        }

        static float Distance(float ax, float ay, float bx, float by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // Picks a pattern allowed at this tier and for this role, avoiding an immediate repeat.
        public static TimingPattern Pick(TimingDef def, int tier, bool attacking, GameRandom random, string previousId)
        {
            var role = attacking ? "attack" : "defend";
            var options = new List<TimingPattern>();
            foreach (var pattern in def.patterns)
            {
                if (pattern.minTier > tier || (pattern.role != role && pattern.role != "any") || pattern.id == previousId) continue;
                options.Add(pattern);
            }
            return options[random.Range(0, options.Count)];
        }
    }
}
