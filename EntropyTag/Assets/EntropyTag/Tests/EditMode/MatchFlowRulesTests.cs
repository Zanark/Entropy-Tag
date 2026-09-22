using System;
using EntropyTag.Domain;
using NUnit.Framework;

namespace EntropyTag.Tests.EditMode
{
    public sealed class MatchFlowRulesTests
    {
        [Test]
        public void FirstSliceUsesAuthoredTwoMinuteDefaults()
        {
            MatchFlowRules rules = MatchFlowRules.CreateFirstSlice();

            Assert.That(rules.CountdownSeconds, Is.EqualTo(3d));
            Assert.That(rules.Timing.DurationSeconds, Is.EqualTo(120d));
            Assert.That(rules.Timing.OpeningEndSeconds, Is.EqualTo(24d));
            Assert.That(rules.Timing.ContestEndSeconds, Is.EqualTo(78d));
            Assert.That(rules.Timing.CompressionEndSeconds, Is.EqualTo(115d));
            Assert.That(rules.InitialBoundary.CenterX, Is.Zero);
            Assert.That(rules.InitialBoundary.CenterZ, Is.Zero);
            Assert.That(rules.InitialBoundary.Radius, Is.EqualTo(20d));
            Assert.That(rules.FinalRadius, Is.EqualTo(8d));
        }

        [TestCase(0d, 20d)]
        [TestCase(24d, 20d)]
        [TestCase(77.999d, 20d)]
        [TestCase(78d, 20d)]
        [TestCase(96.5d, 14d)]
        [TestCase(115d, 8d)]
        [TestCase(120d, 8d)]
        [TestCase(1000d, 8d)]
        [TestCase(double.MaxValue, 8d)]
        public void BoundaryShrinksOnlyDuringCompression(double elapsed, double expectedRadius)
        {
            CircularArenaBoundary boundary = MatchFlowRules.CreateFirstSlice().GetBoundary(elapsed);

            Assert.That(boundary.Radius, Is.EqualTo(expectedRadius));
            Assert.That(boundary.CenterX, Is.Zero);
            Assert.That(boundary.CenterZ, Is.Zero);
        }

        [Test]
        public void BoundaryUsesConfiguredCenterAndThresholds()
        {
            var rules = new MatchFlowRules(new MatchTiming(10d, 2d, 4d, 8d), 0d, 3d, -2d, 9d, 3d);

            Assert.That(rules.GetBoundary(4d).Radius, Is.EqualTo(9d));
            Assert.That(rules.GetBoundary(6d).Radius, Is.EqualTo(6d));
            Assert.That(rules.GetBoundary(8d).Radius, Is.EqualTo(3d));
            Assert.That(rules.GetBoundary(10d).CenterX, Is.EqualTo(3d));
            Assert.That(rules.GetBoundary(10d).CenterZ, Is.EqualTo(-2d));
        }

        [Test]
        public void EqualRadiiAndZeroCountdownAreValid()
        {
            var rules = new MatchFlowRules(MatchTiming.CreateFirstSlice(), 0d, 0d, 0d, 8d, 8d);

            Assert.That(rules.CountdownSeconds, Is.Zero);
            Assert.That(rules.GetBoundary(96.5d).Radius, Is.EqualTo(8d));
            Assert.That(rules.GetBoundary(120d).Radius, Is.EqualTo(8d));
        }

        [TestCase(0d, 0d, true)]
        [TestCase(5d, 0d, true)]
        [TestCase(3d, 4d, true)]
        [TestCase(1d, 1d, true)]
        [TestCase(5.001d, 0d, false)]
        [TestCase(3.001d, 4d, false)]
        [TestCase(5d, 5d, false)]
        public void CircleIncludesItsEdgeAndIsSymmetric(double x, double z, bool expected)
        {
            var boundary = new CircularArenaBoundary(7d, -3d, 5d);

            foreach (int signX in new[] { -1, 1 })
            {
                foreach (int signZ in new[] { -1, 1 })
                {
                    Assert.That(boundary.Contains(7d + signX * x, -3d + signZ * z), Is.EqualTo(expected));
                    Assert.That(boundary.Contains(7d + signX * z, -3d + signZ * x), Is.EqualTo(expected));
                }
            }
        }

        [Test]
        public void CircleHandlesFiniteExtremeRadiiWithoutSquaredDistanceOverflow()
        {
            var large = new CircularArenaBoundary(0d, 0d, double.MaxValue);
            var small = new CircularArenaBoundary(0d, 0d, 1e-200d);

            Assert.That(large.Contains(double.MaxValue, 0d), Is.True);
            Assert.That(large.Contains(double.MaxValue, double.MaxValue), Is.False);
            Assert.That(small.Contains(1e-200d, 0d), Is.True);
            Assert.That(small.Contains(1e-200d, 1e-200d), Is.False);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void BoundariesRejectNonfiniteCoordinatesAndRadii(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CircularArenaBoundary(value, 0d, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CircularArenaBoundary(0d, value, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CircularArenaBoundary(0d, 0d, value));
            var boundary = new CircularArenaBoundary(0d, 0d, 1d);
            Assert.Throws<ArgumentOutOfRangeException>(() => boundary.Contains(value, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => boundary.Contains(0d, value));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        public void BoundaryRejectsNonpositiveRadius(double radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CircularArenaBoundary(0d, 0d, radius));
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void FlowRulesRejectInvalidElapsedTimeAndCountdown(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchFlowRules.CreateFirstSlice().GetBoundary(value));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MatchFlowRules(MatchTiming.CreateFirstSlice(), value, 0d, 0d, 20d, 8d));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(21d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void FlowRulesRejectInvalidFinalRadius(double radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MatchFlowRules(MatchTiming.CreateFirstSlice(), 3d, 0d, 0d, 20d, radius));
        }

        [Test]
        public void FlowRulesValidateTimingAndInitialBoundary()
        {
            Assert.Throws<ArgumentNullException>(() => new MatchFlowRules(null, 3d, 0d, 0d, 20d, 8d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MatchFlowRules(MatchTiming.CreateFirstSlice(), 3d, double.NaN, 0d, 20d, 8d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MatchFlowRules(MatchTiming.CreateFirstSlice(), 3d, 0d, double.PositiveInfinity, 20d, 8d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MatchFlowRules(MatchTiming.CreateFirstSlice(), 3d, 0d, 0d, double.NaN, 8d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MatchFlowRules(MatchTiming.CreateFirstSlice(), 3d, 0d, 0d, double.PositiveInfinity, 8d));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void MatchTimingRejectsEachNonfiniteThreshold(double value)
        {
            Assert.Throws<ArgumentException>(() => new MatchTiming(120d, value, 78d, 115d));
            Assert.Throws<ArgumentException>(() => new MatchTiming(120d, 24d, value, 115d));
            Assert.Throws<ArgumentException>(() => new MatchTiming(120d, 24d, 78d, value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchTiming(value, 24d, 78d, 115d));
        }
    }
}
