using System;
using EntropyTag.Domain;

namespace EntropyTag.Application
{
    public sealed class MatchScoreSnapshot
    {
        public MatchScoreSnapshot(
            CoverageSnapshot coverage,
            int iceBank,
            int fireBank,
            int iceMistCreatedCells = 0,
            int fireMistCreatedCells = 0,
            int iceMistClaimedCells = 0,
            int fireMistClaimedCells = 0)
        {
            if (coverage == null)
            {
                throw new ArgumentNullException(nameof(coverage));
            }

            RequireNonNegative(coverage.TotalCells, nameof(coverage.TotalCells));
            RequireNonNegative(coverage.NeutralCells, nameof(coverage.NeutralCells));
            RequireNonNegative(coverage.MistCells, nameof(coverage.MistCells));
            RequireNonNegative(iceBank, nameof(iceBank));
            RequireNonNegative(fireBank, nameof(fireBank));
            RequireNonNegative(iceMistCreatedCells, nameof(iceMistCreatedCells));
            RequireNonNegative(fireMistCreatedCells, nameof(fireMistCreatedCells));
            RequireNonNegative(iceMistClaimedCells, nameof(iceMistClaimedCells));
            RequireNonNegative(fireMistClaimedCells, nameof(fireMistClaimedCells));

            if (coverage.Teams.Length != 2)
            {
                throw new ArgumentException("Match coverage must contain exactly teams 1 (Ice) and 2 (Fire).", nameof(coverage));
            }

            TeamCoverage iceCoverage = default;
            TeamCoverage fireCoverage = default;
            bool hasIce = false;
            bool hasFire = false;

            foreach (TeamCoverage team in coverage.Teams)
            {
                if (team.TotalCells != coverage.TotalCells || team.OwnedCells < 0 || team.OwnedCells > coverage.TotalCells)
                {
                    throw new ArgumentException("Team coverage must use the match total and valid ownership counts.", nameof(coverage));
                }

                if (team.TeamId.Value == 1 && !hasIce)
                {
                    iceCoverage = team;
                    hasIce = true;
                }
                else if (team.TeamId.Value == 2 && !hasFire)
                {
                    fireCoverage = team;
                    hasFire = true;
                }
                else
                {
                    throw new ArgumentException("Match coverage must contain exactly teams 1 (Ice) and 2 (Fire).", nameof(coverage));
                }
            }

            long accountedCells = (long)coverage.NeutralCells + coverage.MistCells +
                                  iceCoverage.OwnedCells + fireCoverage.OwnedCells;

            if (accountedCells != coverage.TotalCells)
            {
                throw new ArgumentException("Neutral, Mist, Ice, and Fire counts must sum to the match total.", nameof(coverage));
            }

            TotalCells = coverage.TotalCells;
            NeutralCells = coverage.NeutralCells;
            MistCells = coverage.MistCells;
            IceCoverage = iceCoverage;
            FireCoverage = fireCoverage;
            IceBank = iceBank;
            FireBank = fireBank;
            IceMistCreatedCells = iceMistCreatedCells;
            FireMistCreatedCells = fireMistCreatedCells;
            IceMistClaimedCells = iceMistClaimedCells;
            FireMistClaimedCells = fireMistClaimedCells;
            Outcome = MatchOutcome.Resolve(new CoverageSnapshot(
                TotalCells, NeutralCells, MistCells, new[] { IceCoverage, FireCoverage }));
        }

        public int TotalCells { get; }

        public int NeutralCells { get; }

        public int MistCells { get; }

        public TeamCoverage IceCoverage { get; }

        public TeamCoverage FireCoverage { get; }

        public int IceBank { get; }

        public int FireBank { get; }

        public int IceMistCreatedCells { get; }

        public int FireMistCreatedCells { get; }

        public int IceMistClaimedCells { get; }

        public int FireMistClaimedCells { get; }

        public MatchOutcome Outcome { get; }

        private static void RequireNonNegative(int value, string parameterName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Match score counts cannot be negative.");
            }
        }
    }

    public sealed class MatchResultSnapshot
    {
        internal MatchResultSnapshot(
            int matchNumber,
            double durationSeconds,
            CircularArenaBoundary boundary,
            MatchScoreSnapshot score)
        {
            MatchNumber = matchNumber;
            DurationSeconds = durationSeconds;
            Boundary = boundary;
            Score = score ?? throw new ArgumentNullException(nameof(score));
        }

        public int MatchNumber { get; }

        public double DurationSeconds { get; }

        public CircularArenaBoundary Boundary { get; }

        public MatchScoreSnapshot Score { get; }

        public MatchOutcome Outcome => Score.Outcome;
    }
}
