using System;

namespace Airships.Core
{
    // The deck battle's musical grid, with no Unity dependency. Times are seconds since the battle song's
    // first sample. A sequence is one turn's worth of beats: three seconds in the deck battle songs, each
    // opening on a crash and closing on a drum pickup.
    public class BattleBeat
    {
        // How far past a beat still counts as on it. A frame or two late shouldn't cost a whole beat's wait.
        public const double Grace = 0.12;

        public double BeatSeconds { get; }
        public int SequenceBeats { get; }
        public double SequenceSeconds => BeatSeconds * SequenceBeats;

        public BattleBeat(double beatSeconds, int sequenceBeats)
        {
            if (beatSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(beatSeconds));
            if (sequenceBeats <= 0) throw new ArgumentOutOfRangeException(nameof(sequenceBeats));
            BeatSeconds = beatSeconds;
            SequenceBeats = sequenceBeats;
        }

        public static BattleBeat From(TimingDef timing) => new BattleBeat(timing.beatSeconds, timing.sequenceBeats);

        // The first moment on a grid of this many beats at or after now, allowing the grace.
        public double Next(double now, int beats = 1)
        {
            var step = BeatSeconds * Math.Max(1, beats);
            return Math.Max(0, Math.Ceiling((now - Grace) / step)) * step;
        }

        // The next beat strictly ahead of now, for an action that must start on the music but can't start in the past.
        public double NextAhead(double now, int beats = 1)
        {
            var step = BeatSeconds * Math.Max(1, beats);
            return (Math.Floor(now / step) + 1) * step;
        }

        public double NextSequence(double now) => Next(now, SequenceBeats);

        public long BeatIndex(double now) => (long)Math.Floor(now / BeatSeconds);

        // Zero on a sequence's opening beat, up to SequenceBeats - 1 on its last.
        public int BeatInSequence(double now)
        {
            var index = BeatIndex(now) % SequenceBeats;
            return (int)(index < 0 ? index + SequenceBeats : index);
        }

        // From 1 just as a beat lands to 0 just before the next one, for pulsing things in time.
        public float Pulse(double now)
        {
            var phase = now / BeatSeconds - Math.Floor(now / BeatSeconds);
            return (float)(1 - phase);
        }

        // Whether a time sits on the grid of this many beats per step, within a small tolerance.
        public bool OnGrid(double seconds, double stepBeats)
        {
            var steps = seconds / (BeatSeconds * stepBeats);
            return Math.Abs(steps - Math.Round(steps)) < 0.01;
        }
    }
}
