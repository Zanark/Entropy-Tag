using System;

namespace EntropyTag.Domain
{
    public sealed class DomainRulesConfig
    {
        public DomainRulesConfig(
            TeamDefinition iceTeam,
            TeamDefinition fireTeam,
            TerritoryReactionRule[] territoryRules,
            BankAwardPolicy bankPolicy,
            MatchTiming matchTiming)
        {
            if (iceTeam.Element != ElementId.Ice)
            {
                throw new ArgumentException("Ice team definition must use the Ice element.", nameof(iceTeam));
            }

            if (fireTeam.Element != ElementId.Fire)
            {
                throw new ArgumentException("Fire team definition must use the Fire element.", nameof(fireTeam));
            }

            if (iceTeam.Id == fireTeam.Id)
            {
                throw new ArgumentException("Ice and Fire teams must have distinct IDs.");
            }

            if (territoryRules == null || territoryRules.Length == 0)
            {
                throw new ArgumentException("At least one territory reaction rule is required.", nameof(territoryRules));
            }

            IceTeam = iceTeam;
            FireTeam = fireTeam;
            TerritoryRules = (TerritoryReactionRule[])territoryRules.Clone();
            BankPolicy = bankPolicy ?? throw new ArgumentNullException(nameof(bankPolicy));
            MatchTiming = matchTiming ?? throw new ArgumentNullException(nameof(matchTiming));
            Resolver = new TerritoryReactionResolver(
                new[] { IceTeam, FireTeam },
                TerritoryRules,
                BankPolicy);
        }

        public TeamDefinition IceTeam { get; }

        public TeamDefinition FireTeam { get; }

        public TerritoryReactionRule[] TerritoryRules { get; }

        public BankAwardPolicy BankPolicy { get; }

        public MatchTiming MatchTiming { get; }

        public TerritoryReactionResolver Resolver { get; }

        public static DomainRulesConfig CreateFirstSlice()
        {
            return new DomainRulesConfig(
                new TeamDefinition(new TeamId(1), ElementId.Ice),
                new TeamDefinition(new TeamId(2), ElementId.Fire),
                TerritoryReactionResolver.CreateFirstSliceRules(),
                new BankAwardPolicy(enemyNeutralizedAward: 2, reactionClaimedAward: 1),
                MatchTiming.CreateFirstSlice());
        }
    }
}
