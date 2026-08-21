using System;

namespace EntropyTag.Domain
{
    public enum MatchPhase
    {
        Waiting = 0,
        Opening = 1,
        Contest = 2,
        Compression = 3,
        Resolution = 4,
        Complete = 5
    }

    public sealed class MatchTiming
    {
        public MatchTiming(
            double durationSeconds,
            double openingEndSeconds,
            double contestEndSeconds,
            double compressionEndSeconds)
        {
            if (durationSeconds <= 0d ||
                double.IsNaN(durationSeconds) ||
                double.IsInfinity(durationSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    "Match duration must be finite and greater than zero.");
            }

            if (openingEndSeconds <= 0d ||
                contestEndSeconds <= openingEndSeconds ||
                compressionEndSeconds <= contestEndSeconds ||
                compressionEndSeconds >= durationSeconds)
            {
                throw new ArgumentException(
                    "Phase thresholds must satisfy 0 < opening < contest < compression < duration.");
            }

            DurationSeconds = durationSeconds;
            OpeningEndSeconds = openingEndSeconds;
            ContestEndSeconds = contestEndSeconds;
            CompressionEndSeconds = compressionEndSeconds;
        }

        public double DurationSeconds { get; }

        public double OpeningEndSeconds { get; }

        public double ContestEndSeconds { get; }

        public double CompressionEndSeconds { get; }

        public static MatchTiming CreateFirstSlice()
        {
            return new MatchTiming(120d, 24d, 78d, 115d);
        }
    }

    public sealed class MatchClock
    {
        private readonly MatchTiming timing;

        public MatchClock(MatchTiming matchTiming)
        {
            timing = matchTiming ?? throw new ArgumentNullException(nameof(matchTiming));
            Phase = MatchPhase.Waiting;
        }

        public double ElapsedSeconds { get; private set; }

        public double RemainingSeconds => Math.Max(0d, timing.DurationSeconds - ElapsedSeconds);

        public MatchPhase Phase { get; private set; }

        public void Start()
        {
            if (Phase != MatchPhase.Waiting)
            {
                throw new InvalidOperationException("Match clock can only be started from Waiting.");
            }

            Phase = MatchPhase.Opening;
        }

        public void Advance(double deltaSeconds)
        {
            if (Phase == MatchPhase.Waiting)
            {
                throw new InvalidOperationException("Match clock must be started before it can advance.");
            }

            if (deltaSeconds < 0d || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaSeconds),
                    "Match delta must be finite and non-negative.");
            }

            if (Phase == MatchPhase.Complete)
            {
                return;
            }

            ElapsedSeconds = Math.Min(timing.DurationSeconds, ElapsedSeconds + deltaSeconds);
            Phase = ResolvePhase(ElapsedSeconds);
        }

        private MatchPhase ResolvePhase(double elapsed)
        {
            if (elapsed >= timing.DurationSeconds)
            {
                return MatchPhase.Complete;
            }

            if (elapsed >= timing.CompressionEndSeconds)
            {
                return MatchPhase.Resolution;
            }

            if (elapsed >= timing.ContestEndSeconds)
            {
                return MatchPhase.Compression;
            }

            if (elapsed >= timing.OpeningEndSeconds)
            {
                return MatchPhase.Contest;
            }

            return MatchPhase.Opening;
        }
    }
}
