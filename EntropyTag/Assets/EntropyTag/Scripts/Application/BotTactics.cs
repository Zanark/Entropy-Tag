using System;
using System.Collections.Generic;
using EntropyTag.Domain;

namespace EntropyTag.Application
{
    public enum BotGoalKind
    {
        Idle = 0,
        Paint = 1,
        Contest = 2,
        Pursue = 3,
        Engage = 4,
        ReturnToSafeArea = 5,
        Recover = 6
    }

    public readonly struct BotTerritoryOption
    {
        public BotTerritoryOption(
            int id,
            TerritoryState state,
            TerritoryOwner owner,
            double distance,
            bool reachable,
            bool retainedByFinalBoundary)
        {
            BotTactics.RequireTargetId(id, nameof(id));
            BotTactics.RequireNonNegativeFinite(distance, nameof(distance));

            if (state < TerritoryState.Neutral || state > TerritoryState.Mist)
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            bool requiresOwner = state == TerritoryState.Ice || state == TerritoryState.Fire;

            if (owner.HasTeam != requiresOwner || (owner.HasTeam && owner.TeamId.Value <= 0))
            {
                throw new ArgumentException("Only Ice and Fire territory must have a valid owner.", nameof(owner));
            }

            Id = id;
            State = state;
            Owner = owner;
            Distance = distance;
            Reachable = reachable;
            RetainedByFinalBoundary = retainedByFinalBoundary;
        }

        public int Id { get; }

        public TerritoryState State { get; }

        public TerritoryOwner Owner { get; }

        public double Distance { get; }

        public bool Reachable { get; }

        public bool RetainedByFinalBoundary { get; }
    }

    public readonly struct BotOpponentOption
    {
        public BotOpponentOption(
            int id,
            TeamId team,
            double distance,
            bool visible,
            bool protectedFromHit,
            bool reachable,
            bool isBot)
        {
            BotTactics.RequireTargetId(id, nameof(id));
            BotTactics.RequireTeam(team, nameof(team));
            BotTactics.RequireNonNegativeFinite(distance, nameof(distance));
            Id = id;
            Team = team;
            Distance = distance;
            Visible = visible;
            ProtectedFromHit = protectedFromHit;
            Reachable = reachable;
            IsBot = isBot;
        }

        public int Id { get; }

        public TeamId Team { get; }

        public double Distance { get; }

        public bool Visible { get; }

        public bool ProtectedFromHit { get; }

        public bool Reachable { get; }

        public bool IsBot { get; }
    }

    public readonly struct BotObservation
    {
        public BotObservation(
            TeamId ownTeam,
            double remainingSeconds,
            bool needsSafeArea,
            bool stuck,
            bool combatReady,
            bool losing)
        {
            BotTactics.RequireTeam(ownTeam, nameof(ownTeam));
            BotTactics.RequireNonNegativeFinite(remainingSeconds, nameof(remainingSeconds));
            OwnTeam = ownTeam;
            RemainingSeconds = remainingSeconds;
            NeedsSafeArea = needsSafeArea;
            Stuck = stuck;
            CombatReady = combatReady;
            Losing = losing;
        }

        public TeamId OwnTeam { get; }

        public double RemainingSeconds { get; }

        public bool NeedsSafeArea { get; }

        public bool Stuck { get; }

        public bool CombatReady { get; }

        public bool Losing { get; }
    }

    public readonly struct BotDecision
    {
        private readonly int targetId;

        public BotDecision(BotGoalKind kind, int targetId = -1)
        {
            if (kind < BotGoalKind.Idle || kind > BotGoalKind.Recover)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            bool requiresTarget = kind == BotGoalKind.Paint || kind == BotGoalKind.Contest ||
                                  kind == BotGoalKind.Pursue || kind == BotGoalKind.Engage;

            if (requiresTarget == (targetId == -1))
            {
                throw new ArgumentException("Only Paint, Contest, Pursue, and Engage require a target.", nameof(targetId));
            }

            Kind = kind;
            this.targetId = targetId;
        }

        public BotGoalKind Kind { get; }

        public int TargetId => Kind == BotGoalKind.Idle ? -1 : targetId;
    }

    public static class BotTactics
    {
        private const double EngagementRange = 11d;
        private const double BotOpponentDistanceBonus = 6d;
        private const double ClosingSeconds = 42d;
        private const double FinalSeconds = 15d;

        public static BotDecision Decide(
            BotObservation observation,
            IReadOnlyList<BotTerritoryOption> territory,
            IReadOnlyList<BotOpponentOption> opponents,
            IRandomSource random)
        {
            RequireTeam(observation.OwnTeam, nameof(observation));

            if (territory == null)
            {
                throw new ArgumentNullException(nameof(territory));
            }

            if (opponents == null)
            {
                throw new ArgumentNullException(nameof(opponents));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (observation.NeedsSafeArea)
            {
                return new BotDecision(BotGoalKind.ReturnToSafeArea);
            }

            if (observation.Stuck)
            {
                // The movement adapter executes one bounded recovery action and re-observes.
                return new BotDecision(BotGoalKind.Recover);
            }

            if (observation.RemainingSeconds == 0d)
            {
                return new BotDecision(BotGoalKind.Idle);
            }

            int engagementId = observation.CombatReady
                ? FindOpponent(observation, opponents, random, EngagementRange, true)
                : -1;
            if (engagementId != -1 && observation.RemainingSeconds > FinalSeconds)
            {
                return new BotDecision(BotGoalKind.Engage, engagementId);
            }

            double bestScore = double.NegativeInfinity;
            int tieCount = 0;
            BotDecision bestTerritory = default;

            for (int i = 0; i < territory.Count; i++)
            {
                BotTerritoryOption option = territory[i];

                if (!option.Reachable || (option.Owner.HasTeam && option.Owner.TeamId == observation.OwnTeam))
                {
                    continue;
                }

                bool enemyOwned = option.Owner.HasTeam;
                bool contested = enemyOwned || option.State == TerritoryState.Mist;
                double score = enemyOwned ? 34d : contested ? 32d : 20d;

                if (observation.Losing && contested)
                {
                    score += enemyOwned ? 18d : 6d;
                }

                // Compression begins with 42 seconds left in the authored two-minute match.
                // Retained ownership matters more than gains that the final circle will erase.
                if (observation.RemainingSeconds <= ClosingSeconds)
                {
                    score += option.RetainedByFinalBoundary ? 80d : -20d;
                }

                score -= option.Distance;

                if (score > 0d && Prefer(score, ref bestScore, ref tieCount, random))
                {
                    bestTerritory = new BotDecision(contested ? BotGoalKind.Contest : BotGoalKind.Paint, option.Id);
                }
            }

            if (bestTerritory.Kind != BotGoalKind.Idle)
            {
                return bestTerritory;
            }

            if (engagementId != -1)
            {
                return new BotDecision(BotGoalKind.Engage, engagementId);
            }

            double pursuitRange = observation.RemainingSeconds <= FinalSeconds
                ? Math.Min(EngagementRange, observation.RemainingSeconds * 3d)
                : double.MaxValue;
            int pursuitId = FindOpponent(observation, opponents, random, pursuitRange, false);
            return pursuitId == -1
                ? new BotDecision(BotGoalKind.Idle)
                : new BotDecision(BotGoalKind.Pursue, pursuitId);
        }

        private static int FindOpponent(
            BotObservation observation,
            IReadOnlyList<BotOpponentOption> opponents,
            IRandomSource random,
            double maximumDistance,
            bool requireHittable)
        {
            int targetId = -1;
            double bestScore = double.NegativeInfinity;
            int tieCount = 0;

            for (int i = 0; i < opponents.Count; i++)
            {
                BotOpponentOption opponent = opponents[i];

                if (!opponent.Reachable || opponent.Team == observation.OwnTeam ||
                    opponent.Distance > maximumDistance ||
                    (requireHittable && (!opponent.Visible || opponent.ProtectedFromHit)))
                {
                    continue;
                }

                double score = (opponent.IsBot ? BotOpponentDistanceBonus : 0d) - opponent.Distance;
                if (Prefer(score, ref bestScore, ref tieCount, random))
                {
                    targetId = opponent.Id;
                }
            }

            return targetId;
        }

        private static bool Prefer(double score, ref double bestScore, ref int tieCount, IRandomSource random)
        {
            if (score > bestScore)
            {
                bestScore = score;
                tieCount = 1;
                return true;
            }

            // Reservoir sampling breaks equal-score ties without allocating a candidate list.
            return score == bestScore && random.NextInt(++tieCount) == 0;
        }

        internal static void RequireTargetId(int id, string parameterName)
        {
            if (id == -1)
            {
                throw new ArgumentOutOfRangeException(parameterName, "-1 is reserved for decisions without a target.");
            }
        }

        internal static void RequireTeam(TeamId team, string parameterName)
        {
            if (team.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "A valid team is required.");
            }
        }

        internal static void RequireNonNegativeFinite(double value, string parameterName)
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and nonnegative.");
            }
        }
    }
}
