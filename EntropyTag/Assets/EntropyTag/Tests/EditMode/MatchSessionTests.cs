using System;
using System.Collections.Generic;
using EntropyTag.Application;
using EntropyTag.Domain;
using NUnit.Framework;

namespace EntropyTag.Tests.EditMode
{
    public sealed class MatchSessionTests
    {
        private FakeArena arena;
        private MatchSession session;

        [SetUp]
        public void SetUp()
        {
            arena = new FakeArena();
            session = new MatchSession(MatchFlowRules.CreateFirstSlice(), arena);
        }

        [Test]
        public void WaitingDoesNotResetCaptureOrAdvanceTheArena()
        {
            session.Advance(double.MaxValue);

            Assert.That(session.State, Is.EqualTo(MatchSessionState.Waiting));
            Assert.That(session.Phase, Is.EqualTo(MatchPhase.Waiting));
            Assert.That(session.CountdownRemainingSeconds, Is.Zero);
            Assert.That(session.ElapsedSeconds, Is.Zero);
            Assert.That(session.RemainingSeconds, Is.EqualTo(120d));
            Assert.That(session.Boundary.Radius, Is.EqualTo(20d));
            Assert.That(session.Result, Is.Null);
            Assert.That(session.MatchNumber, Is.Zero);
            Assert.That(arena.ResetCount, Is.Zero);
            Assert.That(arena.CaptureCount, Is.Zero);
        }

        [Test]
        public void StartResetsFreeplayAndEntersCountdownWithoutConsumingMatchTime()
        {
            PaintFireReaction();

            session.StartMatch();

            AssertFreshMatch(1);
            Assert.That(arena.ResetCount, Is.EqualTo(1));
            Assert.That(arena.CaptureCount, Is.Zero);
            session.Advance(2d);
            Assert.That(session.State, Is.EqualTo(MatchSessionState.Countdown));
            Assert.That(session.Phase, Is.EqualTo(MatchPhase.Waiting));
            Assert.That(session.CountdownRemainingSeconds, Is.EqualTo(1d));
            Assert.That(session.ElapsedSeconds, Is.Zero);
            Assert.That(session.RemainingSeconds, Is.EqualTo(120d));
        }

        [TestCase(3d, 0d)]
        [TestCase(7d, 4d)]
        [TestCase(27d, 24d)]
        public void CountdownConsumesOverflowExactly(double delta, double expectedElapsed)
        {
            session.StartMatch();
            session.Advance(delta);

            Assert.That(session.State, Is.EqualTo(MatchSessionState.Active));
            Assert.That(session.CountdownRemainingSeconds, Is.Zero);
            Assert.That(session.ElapsedSeconds, Is.EqualTo(expectedElapsed));
            Assert.That(session.RemainingSeconds, Is.EqualTo(120d - expectedElapsed));
            Assert.That(session.Phase, Is.EqualTo(expectedElapsed < 24d ? MatchPhase.Opening : MatchPhase.Contest));
        }

        [Test]
        public void CountdownOverflowWorksAfterPartialCountdownTicks()
        {
            session.StartMatch();
            session.Advance(2d);
            session.Advance(5d);

            Assert.That(session.ElapsedSeconds, Is.EqualTo(4d));
            Assert.That(session.RemainingSeconds, Is.EqualTo(116d));
            Assert.That(session.Phase, Is.EqualTo(MatchPhase.Opening));
        }

        [Test]
        public void ZeroCountdownActivatesImmediatelyWithoutAnIntermediateCountdownEvent()
        {
            session = new MatchSession(
                new MatchFlowRules(MatchTiming.CreateFirstSlice(), 0d, 0d, 0d, 20d, 8d), arena);
            List<string> events = RecordEvents(session);

            session.StartMatch();
            session.Advance(0d);

            Assert.That(session.State, Is.EqualTo(MatchSessionState.Active));
            Assert.That(session.Phase, Is.EqualTo(MatchPhase.Opening));
            Assert.That(session.ElapsedSeconds, Is.Zero);
            Assert.That(session.RemainingSeconds, Is.EqualTo(120d));
            Assert.That(arena.ResetCount, Is.EqualTo(1));
            Assert.That(events, Is.EqualTo(new[] { "State:Active", "Phase:Opening" }));
        }

        [TestCase(MatchSessionState.Countdown)]
        [TestCase(MatchSessionState.Active)]
        public void StartRejectsAnInProgressMatchWithoutResettingIt(MatchSessionState state)
        {
            ReachState(state);
            double elapsed = session.ElapsedSeconds;
            double countdown = session.CountdownRemainingSeconds;

            Assert.Throws<InvalidOperationException>(() => session.StartMatch());

            Assert.That(session.State, Is.EqualTo(state));
            Assert.That(session.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(session.CountdownRemainingSeconds, Is.EqualTo(countdown));
            Assert.That(session.MatchNumber, Is.EqualTo(1));
            Assert.That(arena.ResetCount, Is.EqualTo(1));
        }

        [Test]
        public void LargeTickEmitsEveryPhaseAtItsThresholdAndCompletesExactlyOnce()
        {
            List<string> events = RecordEvents(session);
            var phaseTimes = new List<double>();
            var phaseRadii = new List<double>();
            session.PhaseChanged += phase =>
            {
                phaseTimes.Add(session.ElapsedSeconds);
                phaseRadii.Add(session.Boundary.Radius);
            };
            session.MatchCompleted += result =>
            {
                Assert.That(session.State, Is.EqualTo(MatchSessionState.Results));
                Assert.That(session.Phase, Is.EqualTo(MatchPhase.Complete));
                Assert.That(session.Result, Is.SameAs(result));
                Assert.That(session.CaptureScore(), Is.SameAs(result.Score));
            };
            arena.OnCapture = () =>
            {
                Assert.That(session.ElapsedSeconds, Is.EqualTo(120d));
                Assert.That(session.RemainingSeconds, Is.Zero);
                Assert.That(session.Boundary.Radius, Is.EqualTo(8d));
            };

            session.StartMatch();
            session.Advance(double.MaxValue);
            MatchResultSnapshot result = session.Result;
            session.Advance(0d);
            session.Advance(120d);
            session.Advance(double.MaxValue);

            Assert.That(events, Is.EqualTo(new[]
            {
                "State:Countdown", "State:Active", "Phase:Opening", "Phase:Contest",
                "Phase:Compression", "Phase:Resolution", "Phase:Complete", "State:Results", "Completed:1"
            }));
            Assert.That(phaseTimes, Is.EqualTo(new[] { 0d, 24d, 78d, 115d, 120d }));
            Assert.That(phaseRadii, Is.EqualTo(new[] { 20d, 20d, 20d, 8d, 8d }));
            Assert.That(arena.CaptureCount, Is.EqualTo(1));
            Assert.That(arena.ResetCount, Is.EqualTo(1));
            Assert.That(arena.LastBoundary.Radius, Is.EqualTo(8d));
            Assert.That(result, Is.SameAs(session.Result));
            Assert.That(result.MatchNumber, Is.EqualTo(1));
            Assert.That(result.DurationSeconds, Is.EqualTo(120d));
            Assert.That(result.Boundary.Radius, Is.EqualTo(8d));
            Assert.That(result.Outcome.Kind, Is.EqualTo(MatchOutcomeKind.ZeroOwnership));
        }

        [Test]
        public void SplitAndSingleTicksProduceTheSameLifecycle()
        {
            var other = new MatchSession(MatchFlowRules.CreateFirstSlice(), new FakeArena());
            List<string> singleEvents = RecordEvents(session);
            List<string> splitEvents = RecordEvents(other);
            session.StartMatch();
            other.StartMatch();

            session.Advance(123d);
            foreach (double delta in new[] { 1d, 2d, 24d, 54d, 18.5d, 18.5d, 5d })
            {
                other.Advance(delta);
            }

            Assert.That(splitEvents, Is.EqualTo(singleEvents));
            Assert.That(other.State, Is.EqualTo(session.State));
            Assert.That(other.ElapsedSeconds, Is.EqualTo(session.ElapsedSeconds));
            Assert.That(other.Result.Boundary.Radius, Is.EqualTo(session.Result.Boundary.Radius));
            Assert.That(other.Result.Outcome.Kind, Is.EqualTo(session.Result.Outcome.Kind));
        }

        [Test]
        public void SessionUsesConfiguredDurationThresholdsAndFinalBoundary()
        {
            session = new MatchSession(
                new MatchFlowRules(new MatchTiming(10d, 2d, 4d, 8d), 1d, 3d, -2d, 9d, 3d), arena);
            var phaseTimes = new List<double>();
            session.PhaseChanged += phase => phaseTimes.Add(session.ElapsedSeconds);

            session.StartMatch();
            Assert.That(session.RemainingSeconds, Is.EqualTo(10d));
            session.Advance(11d);

            Assert.That(phaseTimes, Is.EqualTo(new[] { 0d, 2d, 4d, 8d, 10d }));
            Assert.That(session.Result.DurationSeconds, Is.EqualTo(10d));
            Assert.That(session.Result.Boundary.CenterX, Is.EqualTo(3d));
            Assert.That(session.Result.Boundary.CenterZ, Is.EqualTo(-2d));
            Assert.That(session.Result.Boundary.Radius, Is.EqualTo(3d));
            Assert.That(arena.CaptureCount, Is.EqualTo(1));
        }

        [Test]
        public void AdvancingFromCountdownStateListenerDoesNotRepeatCompletePhase()
        {
            var phases = new List<MatchPhase>();
            session.PhaseChanged += phases.Add;
            session.StateChanged += state =>
            {
                if (state == MatchSessionState.Countdown)
                {
                    session.Advance(123d);
                }
            };

            session.StartMatch();

            Assert.That(phases, Is.EqualTo(new[]
            {
                MatchPhase.Opening, MatchPhase.Contest, MatchPhase.Compression,
                MatchPhase.Resolution, MatchPhase.Complete
            }));
            Assert.That(session.State, Is.EqualTo(MatchSessionState.Results));
            Assert.That(arena.CaptureCount, Is.EqualTo(1));
        }

        [Test]
        public void CompletionListenerCanRematchWithoutConsumingOldTickOverflow()
        {
            session = new MatchSession(
                new MatchFlowRules(MatchTiming.CreateFirstSlice(), 0d, 0d, 0d, 20d, 8d), arena);
            var completed = new List<MatchResultSnapshot>();
            session.MatchCompleted += result =>
            {
                completed.Add(result);
                if (result.MatchNumber == 1)
                {
                    session.StartMatch();
                }
            };

            session.StartMatch();
            session.Advance(double.MaxValue);

            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(completed[0].MatchNumber, Is.EqualTo(1));
            Assert.That(session.MatchNumber, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(MatchSessionState.Active));
            Assert.That(session.Phase, Is.EqualTo(MatchPhase.Opening));
            Assert.That(session.ElapsedSeconds, Is.Zero);
            Assert.That(session.Result, Is.Null);
            Assert.That(arena.ResetCount, Is.EqualTo(2));
            Assert.That(arena.CaptureCount, Is.EqualTo(1));
        }

        [Test]
        public void ResultsStateListenerCanRematchWithoutLosingTheCompletedSnapshot()
        {
            MatchResultSnapshot completed = null;
            session.StateChanged += state =>
            {
                if (state == MatchSessionState.Results)
                {
                    session.StartMatch();
                }
            };
            session.MatchCompleted += result => completed = result;

            session.StartMatch();
            session.Advance(123d);

            Assert.That(completed, Is.Not.Null);
            Assert.That(completed.MatchNumber, Is.EqualTo(1));
            Assert.That(completed.DurationSeconds, Is.EqualTo(120d));
            AssertFreshMatch(2);
            Assert.That(arena.CaptureCount, Is.EqualTo(1));
        }

        [Test]
        public void PhaseListenerRestartStopsAdvancingTheAbandonedMatch()
        {
            session = new MatchSession(
                new MatchFlowRules(MatchTiming.CreateFirstSlice(), 0d, 0d, 0d, 20d, 8d), arena);
            session.PhaseChanged += phase =>
            {
                if (phase == MatchPhase.Compression && session.MatchNumber == 1)
                {
                    session.RestartMatch();
                }
            };

            session.StartMatch();
            session.Advance(120d);

            Assert.That(session.MatchNumber, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(MatchSessionState.Active));
            Assert.That(session.Phase, Is.EqualTo(MatchPhase.Opening));
            Assert.That(session.ElapsedSeconds, Is.Zero);
            Assert.That(arena.ResetCount, Is.EqualTo(2));
            Assert.That(arena.CaptureCount, Is.Zero);
        }

        [TestCase(MatchSessionState.Waiting)]
        [TestCase(MatchSessionState.Countdown)]
        [TestCase(MatchSessionState.Active)]
        [TestCase(MatchSessionState.Results)]
        public void RestartIsAllowedFromEveryStateAndClearsPreviousMatchData(MatchSessionState state)
        {
            ReachState(state);
            PaintFireReaction();
            int priorNumber = session.MatchNumber;
            int priorResets = arena.ResetCount;
            int completions = 0;
            session.MatchCompleted += result => completions++;

            session.RestartMatch();

            AssertFreshMatch(priorNumber + 1);
            Assert.That(arena.ResetCount, Is.EqualTo(priorResets + 1));
            Assert.That(completions, Is.Zero);
        }

        [Test]
        public void RestartFromActiveNotifiesCountdownAndWaitingPhase()
        {
            ReachState(MatchSessionState.Active);
            session.Advance(80d);
            List<string> events = RecordEvents(session);

            session.RestartMatch();

            Assert.That(events, Is.EqualTo(new[] { "State:Countdown", "Phase:Waiting" }));
            AssertFreshMatch(2);
        }

        [Test]
        public void TenConsecutiveTwoMinuteMatchesResetExactlyOnceAndNeverAccumulateOldStats()
        {
            var results = new List<MatchResultSnapshot>();
            session.MatchCompleted += results.Add;

            for (int matchNumber = 1; matchNumber <= 10; matchNumber++)
            {
                session.StartMatch();
                AssertFreshMatch(matchNumber);
                session.Advance(3d);
                PaintFireReaction();
                session.Advance(120d);

                Assert.That(session.State, Is.EqualTo(MatchSessionState.Results));
                Assert.That(session.ElapsedSeconds, Is.EqualTo(120d));
                Assert.That(session.Result.MatchNumber, Is.EqualTo(matchNumber));
                Assert.That(session.Result.Score.FireCoverage.OwnedCells, Is.EqualTo(1));
                Assert.That(session.Result.Score.FireBank, Is.EqualTo(3));
                Assert.That(session.Result.Score.FireMistCreatedCells, Is.EqualTo(1));
                Assert.That(session.Result.Score.FireMistClaimedCells, Is.EqualTo(1));
                Assert.That(session.Result.Score.IceBank, Is.Zero);
                Assert.That(arena.ResetCount, Is.EqualTo(matchNumber));
                Assert.That(arena.CaptureCount, Is.EqualTo(matchNumber));
                Assert.That(results.Count, Is.EqualTo(matchNumber));
            }

            for (int index = 0; index < results.Count; index++)
            {
                Assert.That(results[index].MatchNumber, Is.EqualTo(index + 1));
                Assert.That(results[index].Score.FireBank, Is.EqualTo(3));
                Assert.That(results[index].Score.FireCoverage.OwnedCells, Is.EqualTo(1));
                Assert.That(results[index].Outcome.Winner.TeamId.Value, Is.EqualTo(2));
            }
        }

        [Test]
        public void CompletedResultRemainsFrozenAfterSourceMutationArenaResetAndRematch()
        {
            session.StartMatch();
            session.Advance(3d);
            PaintFireReaction();
            session.Advance(120d);
            MatchResultSnapshot first = session.Result;
            CoverageSnapshot source = arena.LastCoverage;
            source.Teams[0] = new TeamCoverage(new TeamId(1), 4, 4);
            source.Teams[1] = new TeamCoverage(new TeamId(2), 0, 4);
            arena.Reset();
            arena.Stamp(ElementId.Ice, new TerritoryCoordinate(0, 0));

            Assert.That(session.CaptureScore(), Is.SameAs(first.Score));
            Assert.That(session.CaptureScore(), Is.SameAs(first.Score));
            Assert.That(arena.CaptureCount, Is.EqualTo(1));
            Assert.That(first.Score.FireBank, Is.EqualTo(3));
            Assert.That(first.Score.FireCoverage.OwnedCells, Is.EqualTo(1));
            Assert.That(first.Score.FireMistCreatedCells, Is.EqualTo(1));
            Assert.That(first.Score.FireMistClaimedCells, Is.EqualTo(1));
            Assert.That(first.Outcome.Winner.TeamId.Value, Is.EqualTo(2));

            session.StartMatch();
            Assert.That(session.Result, Is.Null);
            session.Advance(3d);
            arena.Stamp(ElementId.Ice, new TerritoryCoordinate(1, 1));
            session.Advance(120d);

            Assert.That(session.Result, Is.Not.SameAs(first));
            Assert.That(session.Result.MatchNumber, Is.EqualTo(2));
            Assert.That(session.Result.Outcome.Winner.TeamId.Value, Is.EqualTo(1));
            Assert.That(first.MatchNumber, Is.EqualTo(1));
            Assert.That(first.Outcome.Winner.TeamId.Value, Is.EqualTo(2));
            Assert.That(first.Score.FireBank, Is.EqualTo(3));
        }

        [Test]
        public void LiveCaptureAlwaysUsesCurrentBoundaryAndDoesNotFreezeUntilCompletion()
        {
            MatchScoreSnapshot waiting = session.CaptureScore();
            Assert.That(arena.LastBoundary.Radius, Is.EqualTo(20d));
            session.StartMatch();
            session.Advance(99.5d);
            MatchScoreSnapshot compression = session.CaptureScore();
            Assert.That(session.ElapsedSeconds, Is.EqualTo(96.5d));
            Assert.That(arena.LastBoundary.Radius, Is.EqualTo(14d));
            arena.Stamp(ElementId.Ice, new TerritoryCoordinate(0, 0));
            MatchScoreSnapshot painted = session.CaptureScore();

            Assert.That(painted.IceCoverage.OwnedCells, Is.EqualTo(1));
            Assert.That(waiting.IceCoverage.OwnedCells, Is.Zero);
            Assert.That(compression.IceCoverage.OwnedCells, Is.Zero);
            Assert.That(painted, Is.Not.SameAs(compression));
            Assert.That(session.Result, Is.Null);
            Assert.That(arena.CaptureCount, Is.EqualTo(3));
        }

        [Test]
        public void FinalSnapshotIncludesOwnProvenanceClaimsWithoutAwardingBank()
        {
            session.StartMatch();
            session.Advance(3d);
            var coordinate = new TerritoryCoordinate(0, 0);
            arena.Stamp(ElementId.Ice, coordinate);
            arena.Stamp(ElementId.Fire, coordinate, coordinate);
            arena.Stamp(ElementId.Ice, coordinate, coordinate);
            session.Advance(120d);

            Assert.That(session.Result.Score.IceMistClaimedCells, Is.EqualTo(1));
            Assert.That(session.Result.Score.IceBank, Is.Zero);
            Assert.That(session.Result.Score.FireMistCreatedCells, Is.EqualTo(1));
            Assert.That(session.Result.Score.FireBank, Is.EqualTo(2));
            Assert.That(session.Result.Outcome.Winner.TeamId.Value, Is.EqualTo(1));
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidDeltaIsRejectedInEveryState(double delta)
        {
            foreach (MatchSessionState state in new[]
                     {
                         MatchSessionState.Waiting, MatchSessionState.Countdown,
                         MatchSessionState.Active, MatchSessionState.Results
                     })
            {
                SetUp();
                ReachState(state);
                double elapsed = session.ElapsedSeconds;
                int captures = arena.CaptureCount;
                Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(delta));
                Assert.That(session.State, Is.EqualTo(state));
                Assert.That(session.ElapsedSeconds, Is.EqualTo(elapsed));
                Assert.That(arena.CaptureCount, Is.EqualTo(captures));
            }
        }

        [Test]
        public void NullDependenciesAndNullArenaScoresFailExplicitly()
        {
            Assert.Throws<ArgumentNullException>(() => new MatchSession(null, arena));
            Assert.Throws<ArgumentNullException>(() => new MatchSession(MatchFlowRules.CreateFirstSlice(), null));
            arena.ReturnNullScore = true;
            Assert.Throws<InvalidOperationException>(() => session.CaptureScore());
        }

        private void AssertFreshMatch(int number)
        {
            Assert.That(session.MatchNumber, Is.EqualTo(number));
            Assert.That(session.State, Is.EqualTo(MatchSessionState.Countdown));
            Assert.That(session.Phase, Is.EqualTo(MatchPhase.Waiting));
            Assert.That(session.CountdownRemainingSeconds, Is.EqualTo(3d));
            Assert.That(session.ElapsedSeconds, Is.Zero);
            Assert.That(session.RemainingSeconds, Is.EqualTo(120d));
            Assert.That(session.Boundary.Radius, Is.EqualTo(20d));
            Assert.That(session.Result, Is.Null);
            Assert.That(arena.IceBank + arena.FireBank, Is.Zero);
            Assert.That(arena.IceMistCreated + arena.FireMistCreated, Is.Zero);
            Assert.That(arena.IceMistClaimed + arena.FireMistClaimed, Is.Zero);
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    Assert.That(arena.Field.GetCell(new TerritoryCoordinate(x, y)), Is.EqualTo(TerritoryCell.Neutral));
                }
            }
        }

        private void ReachState(MatchSessionState state)
        {
            if (state == MatchSessionState.Waiting)
            {
                return;
            }

            session.StartMatch();
            if (state == MatchSessionState.Active || state == MatchSessionState.Results)
            {
                session.Advance(state == MatchSessionState.Results ? 123d : 3d);
            }
        }

        private void PaintFireReaction()
        {
            var coordinate = new TerritoryCoordinate(0, 0);
            arena.Stamp(ElementId.Ice, coordinate);
            arena.Stamp(ElementId.Fire, coordinate, coordinate);
            arena.Stamp(ElementId.Fire, coordinate, coordinate);
        }

        private static List<string> RecordEvents(MatchSession match)
        {
            var events = new List<string>();
            match.StateChanged += state => events.Add("State:" + state);
            match.PhaseChanged += phase => events.Add("Phase:" + phase);
            match.MatchCompleted += result => events.Add("Completed:" + result.MatchNumber);
            return events;
        }

        private sealed class FakeArena : IMatchArena
        {
            private readonly DomainRulesConfig config = DomainRulesConfig.CreateFirstSlice();

            public FakeArena()
            {
                Field = new TerritoryField(2, 2, config.Resolver);
            }

            public TerritoryField Field { get; }
            public int ResetCount { get; private set; }
            public int CaptureCount { get; private set; }
            public int IceBank { get; private set; }
            public int FireBank { get; private set; }
            public int IceMistCreated { get; private set; }
            public int FireMistCreated { get; private set; }
            public int IceMistClaimed { get; private set; }
            public int FireMistClaimed { get; private set; }
            public CircularArenaBoundary LastBoundary { get; private set; }
            public CoverageSnapshot LastCoverage { get; private set; }
            public Action OnCapture { get; set; }
            public bool ReturnNullScore { get; set; }

            public void Reset()
            {
                ResetCount++;
                Field.Reset();
                IceBank = 0;
                FireBank = 0;
                IceMistCreated = 0;
                FireMistCreated = 0;
                IceMistClaimed = 0;
                FireMistClaimed = 0;
            }

            public void Stamp(ElementId element, params TerritoryCoordinate[] coordinates)
            {
                bool ice = element == ElementId.Ice;
                StampResult result = Field.ApplyStamp(coordinates, element, ice ? config.IceTeam.Id : config.FireTeam.Id);
                if (ice)
                {
                    IceBank += result.BankAward;
                    IceMistCreated += result.MistCreatedCells;
                    IceMistClaimed += result.MistClaimedCells;
                }
                else
                {
                    FireBank += result.BankAward;
                    FireMistCreated += result.MistCreatedCells;
                    FireMistClaimed += result.MistClaimedCells;
                }
            }

            public MatchScoreSnapshot CaptureScore(CircularArenaBoundary boundary)
            {
                CaptureCount++;
                LastBoundary = boundary;
                OnCapture?.Invoke();
                if (ReturnNullScore)
                {
                    return null;
                }

                LastCoverage = TerritoryCoverageCalculator.Calculate(Field, new[] { config.IceTeam.Id, config.FireTeam.Id });
                return new MatchScoreSnapshot(
                    LastCoverage, IceBank, FireBank, IceMistCreated, FireMistCreated, IceMistClaimed, FireMistClaimed);
            }
        }
    }
}
