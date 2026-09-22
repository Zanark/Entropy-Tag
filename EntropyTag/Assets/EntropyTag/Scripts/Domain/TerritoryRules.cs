using System;
using System.Collections.Generic;

namespace EntropyTag.Domain
{
    public enum TerritoryOwnerMode
    {
        None = 0,
        ApplyingTeam = 1
    }

    public enum BankAwardKind
    {
        None = 0,
        EnemyNeutralized = 1,
        ReactionClaimed = 2
    }

    public readonly struct TeamDefinition
    {
        public TeamDefinition(TeamId id, ElementId element)
        {
            if (element != ElementId.Ice && element != ElementId.Fire)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(element),
                    "The first slice supports only Ice and Fire teams.");
            }

            Id = id;
            Element = element;
        }

        public TeamId Id { get; }

        public ElementId Element { get; }
    }

    public readonly struct TerritoryReactionRule
    {
        public TerritoryReactionRule(
            TerritoryState existingState,
            ElementId appliedElement,
            TerritoryState resultState,
            TerritoryOwnerMode ownerMode,
            BankAwardKind bankAwardKind)
        {
            ExistingState = existingState;
            AppliedElement = appliedElement;
            ResultState = resultState;
            OwnerMode = ownerMode;
            BankAwardKind = bankAwardKind;
        }

        public TerritoryState ExistingState { get; }

        public ElementId AppliedElement { get; }

        public TerritoryState ResultState { get; }

        public TerritoryOwnerMode OwnerMode { get; }

        public BankAwardKind BankAwardKind { get; }
    }

    public sealed class BankAwardPolicy
    {
        public BankAwardPolicy(int enemyNeutralizedAward, int reactionClaimedAward)
        {
            if (enemyNeutralizedAward < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enemyNeutralizedAward),
                    "Enemy-neutralization bank award cannot be negative.");
            }

            if (reactionClaimedAward < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reactionClaimedAward),
                    "Reaction-claim bank award cannot be negative.");
            }

            EnemyNeutralizedAward = enemyNeutralizedAward;
            ReactionClaimedAward = reactionClaimedAward;
        }

        public int EnemyNeutralizedAward { get; }

        public int ReactionClaimedAward { get; }

        public int Calculate(BankAwardKind kind, TerritoryCell existingCell, TeamId applyingTeam)
        {
            switch (kind)
            {
                case BankAwardKind.None:
                    return 0;
                case BankAwardKind.EnemyNeutralized:
                    return existingCell.Owner.HasTeam && existingCell.Owner.TeamId != applyingTeam
                        ? EnemyNeutralizedAward
                        : 0;
                case BankAwardKind.ReactionClaimed:
                    return existingCell.PreviousOwner.HasTeam &&
                           existingCell.PreviousOwner.TeamId != applyingTeam
                        ? ReactionClaimedAward
                        : 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }

    public readonly struct TerritoryResolution
    {
        public TerritoryResolution(bool applied, TerritoryCell cell, int bankAward)
        {
            Applied = applied;
            Cell = cell;
            BankAward = bankAward;
        }

        public bool Applied { get; }

        public TerritoryCell Cell { get; }

        public int BankAward { get; }
    }

    public sealed class TerritoryReactionResolver
    {
        private readonly Dictionary<RuleKey, TerritoryReactionRule> rules;
        private readonly Dictionary<TeamId, ElementId> teamElements;
        private readonly BankAwardPolicy bankAwardPolicy;

        public TerritoryReactionResolver(
            IEnumerable<TeamDefinition> teams,
            IEnumerable<TerritoryReactionRule> reactionRules,
            BankAwardPolicy awardPolicy)
        {
            if (teams == null)
            {
                throw new ArgumentNullException(nameof(teams));
            }

            if (reactionRules == null)
            {
                throw new ArgumentNullException(nameof(reactionRules));
            }

            bankAwardPolicy = awardPolicy ?? throw new ArgumentNullException(nameof(awardPolicy));
            teamElements = new Dictionary<TeamId, ElementId>();

            foreach (TeamDefinition team in teams)
            {
                if (teamElements.ContainsKey(team.Id))
                {
                    throw new ArgumentException($"Duplicate team ID '{team.Id}'.", nameof(teams));
                }

                if (teamElements.ContainsValue(team.Element))
                {
                    throw new ArgumentException(
                        $"Element '{team.Element}' is assigned to more than one first-slice team.",
                        nameof(teams));
                }

                teamElements.Add(team.Id, team.Element);
            }

            if (teamElements.Count != 2 ||
                !teamElements.ContainsValue(ElementId.Ice) ||
                !teamElements.ContainsValue(ElementId.Fire))
            {
                throw new ArgumentException(
                    "First-slice teams must contain exactly one Ice team and one Fire team.",
                    nameof(teams));
            }

            rules = new Dictionary<RuleKey, TerritoryReactionRule>();

            foreach (TerritoryReactionRule rule in reactionRules)
            {
                var key = new RuleKey(rule.ExistingState, rule.AppliedElement);

                if (rules.ContainsKey(key))
                {
                    throw new ArgumentException(
                        $"Duplicate territory rule for {rule.ExistingState} + {rule.AppliedElement}.",
                        nameof(reactionRules));
                }

                ValidateRule(rule);
                rules.Add(key, rule);
            }

            ValidateRequiredCoverage();
        }

        public TerritoryResolution Resolve(
            TerritoryCell existingCell,
            ElementId appliedElement,
            TeamId applyingTeam)
        {
            if (!teamElements.TryGetValue(applyingTeam, out ElementId teamElement))
            {
                throw new ArgumentException($"Unknown applying team '{applyingTeam}'.", nameof(applyingTeam));
            }

            if (teamElement != appliedElement)
            {
                throw new InvalidOperationException(
                    $"Team '{applyingTeam}' uses {teamElement} and cannot apply {appliedElement}.");
            }

            if (!rules.TryGetValue(new RuleKey(existingCell.State, appliedElement), out TerritoryReactionRule rule))
            {
                return new TerritoryResolution(false, existingCell, 0);
            }

            TerritoryOwner owner = rule.OwnerMode == TerritoryOwnerMode.ApplyingTeam
                ? TerritoryOwner.ForTeam(applyingTeam)
                : TerritoryOwner.None;
            TerritoryOwner previousOwner = rule.ResultState == TerritoryState.Mist
                ? existingCell.Owner
                : TerritoryOwner.None;
            var resultCell = new TerritoryCell(rule.ResultState, owner, previousOwner);
            int bankAward = bankAwardPolicy.Calculate(rule.BankAwardKind, existingCell, applyingTeam);
            return new TerritoryResolution(true, resultCell, bankAward);
        }

        public static TerritoryReactionRule[] CreateFirstSliceRules()
        {
            return new[]
            {
                new TerritoryReactionRule(
                    TerritoryState.Neutral,
                    ElementId.Ice,
                    TerritoryState.Ice,
                    TerritoryOwnerMode.ApplyingTeam,
                    BankAwardKind.None),
                new TerritoryReactionRule(
                    TerritoryState.Neutral,
                    ElementId.Fire,
                    TerritoryState.Fire,
                    TerritoryOwnerMode.ApplyingTeam,
                    BankAwardKind.None),
                new TerritoryReactionRule(
                    TerritoryState.Ice,
                    ElementId.Ice,
                    TerritoryState.Ice,
                    TerritoryOwnerMode.ApplyingTeam,
                    BankAwardKind.None),
                new TerritoryReactionRule(
                    TerritoryState.Fire,
                    ElementId.Fire,
                    TerritoryState.Fire,
                    TerritoryOwnerMode.ApplyingTeam,
                    BankAwardKind.None),
                new TerritoryReactionRule(
                    TerritoryState.Ice,
                    ElementId.Fire,
                    TerritoryState.Mist,
                    TerritoryOwnerMode.None,
                    BankAwardKind.EnemyNeutralized),
                new TerritoryReactionRule(
                    TerritoryState.Fire,
                    ElementId.Ice,
                    TerritoryState.Mist,
                    TerritoryOwnerMode.None,
                    BankAwardKind.EnemyNeutralized),
                new TerritoryReactionRule(
                    TerritoryState.Mist,
                    ElementId.Ice,
                    TerritoryState.Ice,
                    TerritoryOwnerMode.ApplyingTeam,
                    BankAwardKind.ReactionClaimed),
                new TerritoryReactionRule(
                    TerritoryState.Mist,
                    ElementId.Fire,
                    TerritoryState.Fire,
                    TerritoryOwnerMode.ApplyingTeam,
                    BankAwardKind.ReactionClaimed)
            };
        }

        private static void ValidateRule(TerritoryReactionRule rule)
        {
            if (rule.AppliedElement != ElementId.Ice && rule.AppliedElement != ElementId.Fire)
            {
                throw new ArgumentException(
                    $"First-slice rule cannot apply unsupported element '{rule.AppliedElement}'.");
            }

            bool resultNeedsOwner =
                rule.ResultState == TerritoryState.Ice || rule.ResultState == TerritoryState.Fire;

            if (resultNeedsOwner != (rule.OwnerMode == TerritoryOwnerMode.ApplyingTeam))
            {
                throw new ArgumentException(
                    $"Rule {rule.ExistingState} + {rule.AppliedElement} has incompatible result ownership.");
            }
        }

        private void ValidateRequiredCoverage()
        {
            TerritoryReactionRule[] required = CreateFirstSliceRules();

            foreach (TerritoryReactionRule rule in required)
            {
                var key = new RuleKey(rule.ExistingState, rule.AppliedElement);

                if (!rules.TryGetValue(key, out TerritoryReactionRule configured))
                {
                    throw new ArgumentException(
                        $"Missing required territory rule for {rule.ExistingState} + {rule.AppliedElement}.");
                }

                if (configured.ResultState != rule.ResultState ||
                    configured.OwnerMode != rule.OwnerMode ||
                    configured.BankAwardKind != rule.BankAwardKind)
                {
                    throw new ArgumentException(
                        $"Territory rule for {rule.ExistingState} + {rule.AppliedElement} does not match the first-slice contract.");
                }
            }
        }

        private readonly struct RuleKey : IEquatable<RuleKey>
        {
            public RuleKey(TerritoryState state, ElementId element)
            {
                State = state;
                Element = element;
            }

            private TerritoryState State { get; }

            private ElementId Element { get; }

            public bool Equals(RuleKey other)
            {
                return State == other.State && Element == other.Element;
            }

            public override bool Equals(object obj)
            {
                return obj is RuleKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)State * 397) ^ (int)Element;
                }
            }
        }
    }
}
