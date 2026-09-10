using System;
using EntropyTag.Domain;

namespace EntropyTag.Application
{
    public enum MatchSessionState
    {
        Waiting = 0,
        Countdown = 1,
        Active = 2,
        Results = 3
    }

    public interface IMatchArena
    {
        void Reset();

        MatchScoreSnapshot CaptureScore(CircularArenaBoundary boundary);
    }

    public sealed class MatchSession
    {
        private readonly MatchFlowRules rules;
        private readonly IMatchArena arena;
        private MatchClock clock;

        public MatchSession(MatchFlowRules rules, IMatchArena arena)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            clock = new MatchClock(rules.Timing);
        }

        public MatchSessionState State { get; private set; }

        public MatchPhase Phase => clock.Phase;

        public double CountdownRemainingSeconds { get; private set; }

        public double ElapsedSeconds => clock.ElapsedSeconds;

        public double RemainingSeconds => clock.RemainingSeconds;

        public CircularArenaBoundary Boundary => rules.GetBoundary(ElapsedSeconds);

        public MatchResultSnapshot Result { get; private set; }

        public int MatchNumber { get; private set; }

        public event Action<MatchSessionState> StateChanged;

        public event Action<MatchPhase> PhaseChanged;

        public event Action<MatchResultSnapshot> MatchCompleted;

        public void StartMatch()
        {
            if (State != MatchSessionState.Waiting && State != MatchSessionState.Results)
            {
                throw new InvalidOperationException("A match can only start from Waiting or Results.");
            }

            ResetMatch();
        }

        public void RestartMatch()
        {
            ResetMatch();
        }

        public void Advance(double deltaSeconds)
        {
            if (deltaSeconds < 0d || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaSeconds), "Match delta must be finite and non-negative.");
            }

            if (State == MatchSessionState.Waiting || State == MatchSessionState.Results)
            {
                return;
            }

            int advancingMatchNumber = MatchNumber;

            if (State == MatchSessionState.Countdown)
            {
                if (deltaSeconds < CountdownRemainingSeconds)
                {
                    CountdownRemainingSeconds -= deltaSeconds;
                    return;
                }

                deltaSeconds -= CountdownRemainingSeconds;
                CountdownRemainingSeconds = 0d;
                clock.Start();
                State = MatchSessionState.Active;
                StateChanged?.Invoke(State);

                if (MatchNumber != advancingMatchNumber || State != MatchSessionState.Active || Phase != MatchPhase.Opening)
                {
                    return;
                }

                PhaseChanged?.Invoke(Phase);
            }

            // Event listeners may restart; do not spend the old tick's remainder on a new match.
            while (deltaSeconds > 0d && State == MatchSessionState.Active && MatchNumber == advancingMatchNumber)
            {
                MatchPhase previousPhase = Phase;
                double step = Math.Min(deltaSeconds, NextPhaseEndSeconds() - ElapsedSeconds);
                clock.Advance(step);
                deltaSeconds -= step;

                if (Phase == MatchPhase.Complete)
                {
                    CompleteMatch();
                }
                else if (Phase != previousPhase)
                {
                    PhaseChanged?.Invoke(Phase);
                }
            }
        }

        public MatchScoreSnapshot CaptureScore()
        {
            if (State == MatchSessionState.Results)
            {
                return Result.Score;
            }

            return arena.CaptureScore(Boundary) ??
                   throw new InvalidOperationException("The match arena must return a score snapshot.");
        }

        private void ResetMatch()
        {
            int nextMatchNumber = checked(MatchNumber + 1);
            MatchSessionState previousState = State;
            MatchPhase previousPhase = Phase;
            arena.Reset();
            clock = new MatchClock(rules.Timing);
            CountdownRemainingSeconds = rules.CountdownSeconds;
            Result = null;
            MatchNumber = nextMatchNumber;
            State = MatchSessionState.Countdown;

            if (CountdownRemainingSeconds == 0d)
            {
                clock.Start();
                State = MatchSessionState.Active;
            }

            MatchPhase startingPhase = Phase;

            if (State != previousState)
            {
                StateChanged?.Invoke(State);
            }

            if (MatchNumber == nextMatchNumber && Phase == startingPhase && startingPhase != previousPhase)
            {
                PhaseChanged?.Invoke(startingPhase);
            }
        }

        private double NextPhaseEndSeconds()
        {
            switch (Phase)
            {
                case MatchPhase.Opening:
                    return rules.Timing.OpeningEndSeconds;
                case MatchPhase.Contest:
                    return rules.Timing.ContestEndSeconds;
                case MatchPhase.Compression:
                    return rules.Timing.CompressionEndSeconds;
                case MatchPhase.Resolution:
                    return rules.Timing.DurationSeconds;
                default:
                    throw new InvalidOperationException("Only an active phase can advance.");
            }
        }

        private void CompleteMatch()
        {
            var completedResult = new MatchResultSnapshot(MatchNumber, rules.Timing.DurationSeconds, Boundary, CaptureScore());
            Result = completedResult;
            State = MatchSessionState.Results;
            PhaseChanged?.Invoke(Phase);

            if (MatchNumber == completedResult.MatchNumber && State == MatchSessionState.Results)
            {
                StateChanged?.Invoke(State);
            }

            MatchCompleted?.Invoke(completedResult);
        }
    }
}
