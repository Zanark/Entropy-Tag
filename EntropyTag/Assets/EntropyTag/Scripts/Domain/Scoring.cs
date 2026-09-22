using System;
using System.Collections.Generic;

namespace EntropyTag.Domain
{
    public readonly struct TeamCoverage
    {
        public TeamCoverage(TeamId teamId, int ownedCells, int totalCells)
        {
            TeamId = teamId;
            OwnedCells = ownedCells;
            TotalCells = totalCells;
        }

        public TeamId TeamId { get; }

        public int OwnedCells { get; }

        public int TotalCells { get; }

        public double Percentage => TotalCells == 0 ? 0d : OwnedCells * 100d / TotalCells;
    }

    public sealed class CoverageSnapshot
    {
        public CoverageSnapshot(int totalCells, int neutralCells, int mistCells, TeamCoverage[] teams)
        {
            TotalCells = totalCells;
            NeutralCells = neutralCells;
            MistCells = mistCells;
            Teams = teams ?? throw new ArgumentNullException(nameof(teams));
        }

        public int TotalCells { get; }

        public int NeutralCells { get; }

        public int MistCells { get; }

        public TeamCoverage[] Teams { get; }

        public TeamCoverage GetTeam(TeamId teamId)
        {
            foreach (TeamCoverage coverage in Teams)
            {
                if (coverage.TeamId == teamId)
                {
                    return coverage;
                }
            }

            throw new ArgumentException($"Coverage does not contain team '{teamId}'.", nameof(teamId));
        }
    }

    public static class TerritoryCoverageCalculator
    {
        public static CoverageSnapshot Calculate(
            IReadOnlyTerritoryField field,
            IEnumerable<TeamId> teamIds)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            if (teamIds == null)
            {
                throw new ArgumentNullException(nameof(teamIds));
            }

            var counts = new Dictionary<TeamId, int>();

            foreach (TeamId teamId in teamIds)
            {
                if (counts.ContainsKey(teamId))
                {
                    throw new ArgumentException($"Duplicate scoring team '{teamId}'.", nameof(teamIds));
                }

                counts.Add(teamId, 0);
            }

            if (counts.Count == 0)
            {
                throw new ArgumentException("At least one scoring team is required.", nameof(teamIds));
            }

            int neutral = 0;
            int mist = 0;

            for (int y = 0; y < field.Height; y++)
            {
                for (int x = 0; x < field.Width; x++)
                {
                    TerritoryCell cell = field.GetCell(new TerritoryCoordinate(x, y));

                    if (cell.State == TerritoryState.Neutral)
                    {
                        neutral++;
                    }
                    else if (cell.State == TerritoryState.Mist)
                    {
                        mist++;
                    }
                    else if (cell.Owner.HasTeam && counts.ContainsKey(cell.Owner.TeamId))
                    {
                        counts[cell.Owner.TeamId]++;
                    }
                }
            }

            var teams = new TeamCoverage[counts.Count];
            int index = 0;

            foreach (KeyValuePair<TeamId, int> pair in counts)
            {
                teams[index++] = new TeamCoverage(pair.Key, pair.Value, field.CellCount);
            }

            return new CoverageSnapshot(field.CellCount, neutral, mist, teams);
        }
    }

    public enum MatchOutcomeKind
    {
        Winner = 0,
        Tie = 1,
        ZeroOwnership = 2
    }

    public readonly struct MatchOutcome
    {
        private MatchOutcome(MatchOutcomeKind kind, TerritoryOwner winner)
        {
            Kind = kind;
            Winner = winner;
        }

        public MatchOutcomeKind Kind { get; }

        public TerritoryOwner Winner { get; }

        public static MatchOutcome Resolve(CoverageSnapshot coverage)
        {
            if (coverage == null)
            {
                throw new ArgumentNullException(nameof(coverage));
            }

            int highest = 0;
            TeamId winner = default;
            bool hasWinner = false;
            bool tied = false;

            foreach (TeamCoverage team in coverage.Teams)
            {
                if (team.OwnedCells > highest)
                {
                    highest = team.OwnedCells;
                    winner = team.TeamId;
                    hasWinner = true;
                    tied = false;
                }
                else if (team.OwnedCells == highest && team.OwnedCells > 0)
                {
                    tied = true;
                }
            }

            if (!hasWinner)
            {
                return new MatchOutcome(MatchOutcomeKind.ZeroOwnership, TerritoryOwner.None);
            }

            if (tied)
            {
                return new MatchOutcome(MatchOutcomeKind.Tie, TerritoryOwner.None);
            }

            return new MatchOutcome(MatchOutcomeKind.Winner, TerritoryOwner.ForTeam(winner));
        }
    }
}
