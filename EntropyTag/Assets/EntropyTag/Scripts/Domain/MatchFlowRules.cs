using System;

namespace EntropyTag.Domain
{
    public readonly struct CircularArenaBoundary
    {
        public CircularArenaBoundary(double centerX, double centerZ, double radius)
        {
            RequireFinite(centerX, nameof(centerX));
            RequireFinite(centerZ, nameof(centerZ));
            RequireFinite(radius, nameof(radius));

            if (radius <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Arena radius must be greater than zero.");
            }

            CenterX = centerX;
            CenterZ = centerZ;
            Radius = radius;
        }

        public double CenterX { get; }

        public double CenterZ { get; }

        public double Radius { get; }

        public bool Contains(double x, double z)
        {
            RequireFinite(x, nameof(x));
            RequireFinite(z, nameof(z));

            // Normalize before squaring to avoid radius overflow/underflow; tolerate edge rounding.
            double normalizedX = (x - CenterX) / Radius;
            double normalizedZ = (z - CenterZ) / Radius;
            return normalizedX * normalizedX + normalizedZ * normalizedZ <= 1d + 1e-12d;
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Arena coordinates and radius must be finite.");
            }
        }
    }

    public sealed class MatchFlowRules
    {
        public MatchFlowRules(
            MatchTiming timing,
            double countdownSeconds,
            double centerX,
            double centerZ,
            double initialRadius,
            double finalRadius)
        {
            Timing = timing ?? throw new ArgumentNullException(nameof(timing));

            if (countdownSeconds < 0d || double.IsNaN(countdownSeconds) || double.IsInfinity(countdownSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(countdownSeconds), "Countdown must be finite and non-negative.");
            }

            if (finalRadius <= 0d ||
                double.IsNaN(finalRadius) ||
                double.IsInfinity(finalRadius) ||
                finalRadius > initialRadius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(finalRadius), "Final radius must be finite, positive, and no greater than the initial radius.");
            }

            InitialBoundary = new CircularArenaBoundary(centerX, centerZ, initialRadius);
            CountdownSeconds = countdownSeconds;
            FinalRadius = finalRadius;
        }

        public MatchTiming Timing { get; }

        public double CountdownSeconds { get; }

        public CircularArenaBoundary InitialBoundary { get; }

        public double FinalRadius { get; }

        public CircularArenaBoundary GetBoundary(double elapsedSeconds)
        {
            if (elapsedSeconds < 0d || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedSeconds), "Elapsed time must be finite and non-negative.");
            }

            if (elapsedSeconds <= Timing.ContestEndSeconds)
            {
                return InitialBoundary;
            }

            double radius = FinalRadius;

            if (elapsedSeconds < Timing.CompressionEndSeconds)
            {
                double progress = (elapsedSeconds - Timing.ContestEndSeconds) /
                                  (Timing.CompressionEndSeconds - Timing.ContestEndSeconds);
                radius = Math.Max(
                    FinalRadius, InitialBoundary.Radius + (FinalRadius - InitialBoundary.Radius) * progress);
            }

            return new CircularArenaBoundary(InitialBoundary.CenterX, InitialBoundary.CenterZ, radius);
        }

        public static MatchFlowRules CreateFirstSlice()
        {
            return new MatchFlowRules(MatchTiming.CreateFirstSlice(), 3d, 0d, 0d, 20d, 8d);
        }
    }
}
