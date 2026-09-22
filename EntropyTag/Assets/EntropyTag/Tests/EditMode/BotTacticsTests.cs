using System;
using EntropyTag.Application;
using EntropyTag.Domain;
using NUnit.Framework;

namespace EntropyTag.Tests.EditMode
{
    public sealed class BotTacticsTests
    {
        [TestCase(1, 2)]
        [TestCase(2, 1)]
        public void BothTeamsEngageClosestHostileRatherThanCloserTeammate(int ownTeam, int enemyTeam)
        {
            var opponents = new[]
            {
                Opponent(10, ownTeam, 1d),
                Opponent(20, enemyTeam, 8d),
                Opponent(30, enemyTeam, 4d)
            };

            AssertDecision(
                Decide(Observe(ownTeam, combatReady: true), opponents: opponents),
                BotGoalKind.Engage,
                30);
        }

        [TestCase(1, 2, true, BotGoalKind.Engage)]
        [TestCase(2, 1, true, BotGoalKind.Engage)]
        [TestCase(1, 2, false, BotGoalKind.Pursue)]
        [TestCase(2, 1, false, BotGoalKind.Pursue)]
        public void BothTeamsPreferAnotherBotOverASomewhatCloserHuman(
            int ownTeam, int enemyTeam, bool combatReady, BotGoalKind kind)
        {
            var opponents = new[]
            {
                Opponent(10, enemyTeam, 3d),
                Opponent(20, enemyTeam, 8d, isBot: true),
                Opponent(30, ownTeam, 1d, isBot: true)
            };

            AssertDecision(Decide(Observe(ownTeam, combatReady: combatReady), opponents: opponents), kind, 20);
            Array.Reverse(opponents);
            AssertDecision(Decide(Observe(ownTeam, combatReady: combatReady), opponents: opponents), kind, 20);
        }

        [TestCase(true, BotGoalKind.Engage)]
        [TestCase(false, BotGoalKind.Pursue)]
        public void SubstantiallyCloserHumanStillWinsOpponentSelection(bool combatReady, BotGoalKind kind)
        {
            var opponents = new[]
            {
                Opponent(10, 2, 2d),
                Opponent(20, 2, 10d, isBot: true)
            };

            AssertDecision(Decide(Observe(combatReady: combatReady), opponents: opponents), kind, 10);
        }

        [TestCase(8.999d, 20)]
        [TestCase(9.001d, 10)]
        public void BotPreferenceIsBoundedBySixMetersOfDistance(double botDistance, int target)
        {
            AssertDecision(
                Decide(Observe(combatReady: true), opponents: new[]
                {
                    Opponent(10, 2, 3d),
                    Opponent(20, 2, botDistance, isBot: true)
                }),
                BotGoalKind.Engage, target);
        }

        [TestCase(false, false, true, 8d, 2)]
        [TestCase(true, true, true, 8d, 2)]
        [TestCase(true, false, false, 8d, 2)]
        [TestCase(true, false, true, 11.001d, 2)]
        [TestCase(true, false, true, 8d, 1)]
        public void IneligibleBotNeverDisplacesAHittableHuman(
            bool visible, bool protectedFromHit, bool reachable, double distance, int team)
        {
            AssertDecision(
                Decide(Observe(combatReady: true), opponents: new[]
                {
                    Opponent(10, 2, 9d),
                    Opponent(20, team, distance, visible, protectedFromHit, reachable, isBot: true)
                }),
                BotGoalKind.Engage, 10);
        }

        [TestCase(true, BotGoalKind.Engage)]
        [TestCase(false, BotGoalKind.Pursue)]
        public void EqualAdjustedOpponentScoresKeepSeededTieBreaking(bool combatReady, BotGoalKind kind)
        {
            var opponents = new[] { Opponent(10, 2, 3d), Opponent(20, 2, 9d, isBot: true) };
            var firstRandom = new SeededRandomSource(12345);
            var secondRandom = new SeededRandomSource(12345);
            int selectedTargets = 0;

            for (int index = 0; index < 64; index++)
            {
                BotDecision first = Decide(Observe(combatReady: combatReady), opponents: opponents, random: firstRandom);
                BotDecision second = Decide(Observe(combatReady: combatReady), opponents: opponents, random: secondRandom);
                AssertDecision(first, kind, second.TargetId);
                selectedTargets |= 1 << (first.TargetId / 10 - 1);
            }

            Assert.That(selectedTargets, Is.EqualTo(3));
        }

        [TestCase(false, false, true)]
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        public void EngagementRequiresVisibilityNoProtectionAndReachability(
            bool visible,
            bool protectedFromHit,
            bool reachable)
        {
            var opponents = new[]
            {
                Opponent(10, 2, 3d, visible, protectedFromHit, reachable)
            };

            AssertDecision(
                Decide(Observe(combatReady: true), new[] { Neutral(1, 2d) }, opponents),
                BotGoalKind.Paint,
                1);
        }

        [TestCase(11d, BotGoalKind.Engage, 10)]
        [TestCase(11.001d, BotGoalKind.Paint, 1)]
        public void EngagementHasAnInclusiveElevenMeterRange(double distance, BotGoalKind kind, int targetId)
        {
            AssertDecision(
                Decide(
                    Observe(combatReady: true),
                    new[] { Neutral(1, 2d) },
                    new[] { Opponent(10, 2, distance) }),
                kind,
                targetId);
        }

        [TestCase(1, 2)]
        [TestCase(2, 1)]
        public void CombatWindowMustBeReadyBeforeEngaging(int ownTeam, int enemyTeam)
        {
            AssertDecision(
                Decide(
                    Observe(ownTeam, combatReady: false),
                    new[] { Neutral(1, 4d) },
                    new[] { Opponent(10, enemyTeam, 1d) }),
                BotGoalKind.Paint,
                1);
        }

        [Test]
        public void IneligibleCloserOpponentsDoNotHideAnEligibleHostile()
        {
            var opponents = new[]
            {
                Opponent(10, 2, 1d, visible: false),
                Opponent(20, 2, 2d, protectedFromHit: true),
                Opponent(30, 2, 3d, reachable: false),
                Opponent(40, 2, 9d)
            };

            AssertDecision(Decide(Observe(combatReady: true), opponents: opponents), BotGoalKind.Engage, 40);
        }

        [TestCase(1, 2)]
        [TestCase(2, 1)]
        public void EnemyTerritoryIsMoreValuableThanEquidistantNeutral(int ownTeam, int enemyTeam)
        {
            AssertDecision(
                Decide(
                    Observe(ownTeam),
                    new[] { Neutral(1, 4d), Owned(2, enemyTeam, 4d) }),
                BotGoalKind.Contest,
                2);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void MistIsMoreValuableThanEquidistantNeutral(int ownTeam)
        {
            var mist = new BotTerritoryOption(2, TerritoryState.Mist, TerritoryOwner.None, 4d, true, true);

            AssertDecision(
                Decide(Observe(ownTeam), new[] { Neutral(1, 4d), mist }),
                BotGoalKind.Contest,
                2);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void OwnTerritoryNeverBecomesAPaintGoalEvenWhenLosingOrRetained(int ownTeam)
        {
            var territory = new[] { Owned(1, ownTeam, 0d), Neutral(2, 5d) };

            AssertDecision(
                Decide(Observe(ownTeam, remainingSeconds: 10d, losing: true), territory),
                BotGoalKind.Paint,
                2);
            AssertDecision(
                Decide(Observe(ownTeam, losing: true), new[] { Owned(1, ownTeam, 0d) }),
                BotGoalKind.Idle);
        }

        [TestCase(false, BotGoalKind.Paint, 1)]
        [TestCase(true, BotGoalKind.Contest, 2)]
        public void LosingIncreasesContestPreferenceWithoutIgnoringTravel(
            bool losing,
            BotGoalKind kind,
            int targetId)
        {
            AssertDecision(
                Decide(Observe(losing: losing), new[] { Neutral(1, 2d), Owned(2, 2, 30d) }),
                kind,
                targetId);
        }

        [Test]
        public void TravelCostPrefersCloserEquivalentPaint()
        {
            AssertDecision(
                Decide(Observe(), new[] { Neutral(1, 15d), Neutral(2, 2d), Neutral(3, 8d) }),
                BotGoalKind.Paint,
                2);
        }

        [TestCase(80d, BotGoalKind.Contest, 1)]
        [TestCase(42d, BotGoalKind.Paint, 2)]
        [TestCase(20d, BotGoalKind.Paint, 2)]
        [TestCase(1d, BotGoalKind.Paint, 2)]
        public void ClosingStagesStronglyPreferRetainedTerritory(
            double remainingSeconds,
            BotGoalKind kind,
            int targetId)
        {
            var territory = new[]
            {
                Owned(1, 2, 1d, retained: false),
                Neutral(2, 25d, retained: true)
            };

            AssertDecision(Decide(Observe(remainingSeconds: remainingSeconds), territory), kind, targetId);
        }

        [TestCase(true, true, BotGoalKind.ReturnToSafeArea)]
        [TestCase(true, false, BotGoalKind.ReturnToSafeArea)]
        [TestCase(false, true, BotGoalKind.Recover)]
        public void SafetyThenRecoveryOverrideCombatAndTerritory(bool unsafeArea, bool stuck, BotGoalKind expected)
        {
            AssertDecision(
                Decide(
                    Observe(needsSafeArea: unsafeArea, stuck: stuck, combatReady: true),
                    new[] { Neutral(1, 1d) },
                    new[] { Opponent(10, 2, 1d, isBot: true) }),
                expected);
        }

        [Test]
        public void FinalSecondsPrioritizeScoringOverAnAvailableFight()
        {
            AssertDecision(
                Decide(Observe(remainingSeconds: 5d, combatReady: true, losing: true),
                    new[] { Neutral(1, 3d, retained: true) }, new[] { Opponent(10, 2, 2d, isBot: true) }),
                BotGoalKind.Paint, 1);
            AssertDecision(
                Decide(Observe(remainingSeconds: 5d, combatReady: true), opponents: new[] { Opponent(10, 2, 2d) }),
                BotGoalKind.Engage, 10);
        }

        [Test]
        public void RecoveryDoesNotLatchAfterTheMovementAdapterClearsStuck()
        {
            var territory = new[] { Neutral(1, 2d) };
            var random = new SeededRandomSource(123);

            AssertDecision(Decide(Observe(stuck: true), territory, random: random), BotGoalKind.Recover);
            AssertDecision(Decide(Observe(stuck: false), territory, random: random), BotGoalKind.Paint, 1);
        }

        [Test]
        public void EmptyAndEntirelyUnreachableObservationsAreIdle()
        {
            AssertDecision(Decide(Observe()), BotGoalKind.Idle);
            AssertDecision(
                Decide(
                    Observe(combatReady: true),
                    new[] { Neutral(1, 1d, reachable: false) },
                    new[] { Opponent(10, 2, 1d, reachable: false) }),
                BotGoalKind.Idle);
            AssertDecision(
                Decide(Observe(), new[] { default(BotTerritoryOption) }, new[] { default(BotOpponentOption) }),
                BotGoalKind.Idle);
        }

        [TestCase(1, 2)]
        [TestCase(2, 1)]
        public void FallbackPursuesClosestReachableHostileButNeverTeammates(int ownTeam, int enemyTeam)
        {
            var opponents = new[]
            {
                Opponent(10, ownTeam, 1d),
                Opponent(20, enemyTeam, 2d, reachable: false),
                Opponent(30, enemyTeam, 18d),
                Opponent(40, enemyTeam, 12d)
            };

            AssertDecision(
                Decide(Observe(ownTeam), new[] { Owned(1, ownTeam, 0d) }, opponents),
                BotGoalKind.Pursue,
                40);
            AssertDecision(
                Decide(Observe(ownTeam, combatReady: true), opponents: new[] { Opponent(10, ownTeam, 1d) }),
                BotGoalKind.Idle);
        }

        [Test]
        public void NonHittableHostileCanBeApproachedWithoutBeingEngaged()
        {
            AssertDecision(
                Decide(
                    Observe(combatReady: true),
                    opponents: new[] { Opponent(10, 2, 5d, visible: false, protectedFromHit: true) }),
                BotGoalKind.Pursue,
                10);
        }

        [Test]
        public void PursuitIsOnlyAFallbackWhenNoWorthwhileTerritoryIsPaintable()
        {
            var opponents = new[] { Opponent(10, 2, 12d) };

            AssertDecision(
                Decide(Observe(), new[] { Neutral(1, 2d) }, opponents),
                BotGoalKind.Paint,
                1);
            AssertDecision(
                Decide(Observe(), new[] { Neutral(1, 1000d) }, opponents),
                BotGoalKind.Pursue,
                10);
        }

        [TestCase(16d, 100d, BotGoalKind.Pursue)]
        [TestCase(15d, 11d, BotGoalKind.Pursue)]
        [TestCase(15d, 11.001d, BotGoalKind.Idle)]
        [TestCase(1d, 3d, BotGoalKind.Pursue)]
        [TestCase(1d, 3.001d, BotGoalKind.Idle)]
        public void FinalSecondsBoundPursuitDistance(double remainingSeconds, double distance, BotGoalKind kind)
        {
            AssertDecision(
                Decide(
                    Observe(remainingSeconds: remainingSeconds),
                    opponents: new[] { Opponent(10, 2, distance, isBot: true) }),
                kind,
                kind == BotGoalKind.Pursue ? 10 : -1);
        }

        [Test]
        public void ExpiredMatchDoesNotStartCombatOrPainting()
        {
            AssertDecision(
                Decide(
                    Observe(remainingSeconds: 0d, combatReady: true),
                    new[] { Neutral(1, 0d) },
                    new[] { Opponent(10, 2, 0d) }),
                BotGoalKind.Idle);
        }

        [TestCase(BotGoalKind.Paint)]
        [TestCase(BotGoalKind.Contest)]
        [TestCase(BotGoalKind.Pursue)]
        [TestCase(BotGoalKind.Engage)]
        public void SeededTieBreakingReproducesDecisionSequences(BotGoalKind kind)
        {
            bool painting = kind == BotGoalKind.Paint || kind == BotGoalKind.Contest;
            var territory = painting
                ? new[]
                {
                    kind == BotGoalKind.Paint ? Neutral(10, 2d) : Owned(10, 2, 2d),
                    kind == BotGoalKind.Paint ? Neutral(20, 2d) : Owned(20, 2, 2d),
                    kind == BotGoalKind.Paint ? Neutral(30, 2d) : Owned(30, 2, 2d)
                }
                : Array.Empty<BotTerritoryOption>();
            var opponents = painting
                ? Array.Empty<BotOpponentOption>()
                : new[] { Opponent(10, 2, 2d), Opponent(20, 2, 2d), Opponent(30, 2, 2d) };
            BotObservation observation = Observe(combatReady: kind == BotGoalKind.Engage);
            var firstRandom = new SeededRandomSource(12345);
            var secondRandom = new SeededRandomSource(12345);
            int selectedTargets = 0;

            for (int i = 0; i < 64; i++)
            {
                BotDecision first = Decide(observation, territory, opponents, firstRandom);
                BotDecision second = Decide(observation, territory, opponents, secondRandom);
                AssertDecision(first, kind, second.TargetId);
                Assert.That(second.Kind, Is.EqualTo(first.Kind));
                selectedTargets |= 1 << (first.TargetId / 10 - 1);
            }

            Assert.That(selectedTargets, Is.EqualTo(7), "Seeded ties should not always favor list order.");
        }

        [Test]
        public void RandomIsOnlyConsumedForEqualBestCandidates()
        {
            var random = new CountingRandomSource();

            AssertDecision(
                Decide(Observe(), new[] { Neutral(10, 4d), Neutral(20, 2d) }, random: random),
                BotGoalKind.Paint,
                20);
            Assert.That(random.Calls, Is.Zero);
            AssertDecision(
                Decide(Observe(), new[] { Neutral(10, 2d), Neutral(20, 2d) }, random: random),
                BotGoalKind.Paint,
                20);
            Assert.That(random.Calls, Is.EqualTo(1));
        }

        [Test]
        public void OptionsAndObservationsExposeTheirInputs()
        {
            BotTerritoryOption territory = Owned(-2, 2, 7d, reachable: false, retained: false);
            BotOpponentOption opponent = Opponent(0, 2, 9d, visible: false, protectedFromHit: true, reachable: false, isBot: true);
            BotObservation observation = Observe(2, 17d, true, true, true, true);

            Assert.That(territory.Id, Is.EqualTo(-2));
            Assert.That(territory.State, Is.EqualTo(TerritoryState.Fire));
            Assert.That(territory.Owner, Is.EqualTo(TerritoryOwner.ForTeam(new TeamId(2))));
            Assert.That(territory.Distance, Is.EqualTo(7d));
            Assert.That(territory.Reachable, Is.False);
            Assert.That(territory.RetainedByFinalBoundary, Is.False);
            Assert.That(opponent.Id, Is.Zero);
            Assert.That(opponent.Team, Is.EqualTo(new TeamId(2)));
            Assert.That(opponent.Distance, Is.EqualTo(9d));
            Assert.That(opponent.Visible, Is.False);
            Assert.That(opponent.ProtectedFromHit, Is.True);
            Assert.That(opponent.Reachable, Is.False);
            Assert.That(opponent.IsBot, Is.True);
            Assert.That(observation.OwnTeam, Is.EqualTo(new TeamId(2)));
            Assert.That(observation.RemainingSeconds, Is.EqualTo(17d));
            Assert.That(observation.NeedsSafeArea && observation.Stuck && observation.CombatReady && observation.Losing, Is.True);
            AssertDecision(Decide(Observe(), new[] { Neutral(-2, 1d) }), BotGoalKind.Paint, -2);
            AssertDecision(Decide(Observe(), new[] { Neutral(0, 1d) }), BotGoalKind.Paint, 0);
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InputsRejectInvalidDistancesAndRemainingTime(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Neutral(1, value));
            Assert.Throws<ArgumentOutOfRangeException>(() => Opponent(10, 2, value));
            Assert.Throws<ArgumentOutOfRangeException>(() => Observe(remainingSeconds: value));
        }

        [Test]
        public void InputsRejectReservedTargetIdsAndInvalidTeams()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Neutral(-1, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => Opponent(-1, 2, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BotOpponentOption(1, default, 1d, true, false, true, false));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BotObservation(default, 10d, false, false, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => Decide(default));
        }

        [Test]
        public void TerritoryOptionsRequireConsistentStatesAndOwners()
        {
            TerritoryOwner owner = TerritoryOwner.ForTeam(new TeamId(1));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BotTerritoryOption(1, (TerritoryState)99, TerritoryOwner.None, 0d, true, true));
            Assert.Throws<ArgumentException>(
                () => new BotTerritoryOption(1, TerritoryState.Neutral, owner, 0d, true, true));
            Assert.Throws<ArgumentException>(
                () => new BotTerritoryOption(1, TerritoryState.Mist, owner, 0d, true, true));
            Assert.Throws<ArgumentException>(
                () => new BotTerritoryOption(1, TerritoryState.Ice, TerritoryOwner.None, 0d, true, true));
            Assert.Throws<ArgumentException>(
                () => new BotTerritoryOption(1, TerritoryState.Fire, TerritoryOwner.ForTeam(default), 0d, true, true));
        }

        [Test]
        public void DecideRejectsNullCollaboratorsEvenForSafetyOverrides()
        {
            BotObservation observation = Observe(needsSafeArea: true);
            var random = new SeededRandomSource(1);

            Assert.Throws<ArgumentNullException>(
                () => BotTactics.Decide(observation, null, Array.Empty<BotOpponentOption>(), random));
            Assert.Throws<ArgumentNullException>(
                () => BotTactics.Decide(observation, Array.Empty<BotTerritoryOption>(), null, random));
            Assert.Throws<ArgumentNullException>(
                () => BotTactics.Decide(observation, Array.Empty<BotTerritoryOption>(), Array.Empty<BotOpponentOption>(), null));
        }

        [Test]
        public void DecisionsPreserveTheNoTargetSentinelIncludingDefaultValues()
        {
            AssertDecision(default, BotGoalKind.Idle);
            AssertDecision(new BotDecision(BotGoalKind.Idle), BotGoalKind.Idle);
            AssertDecision(new BotDecision(BotGoalKind.ReturnToSafeArea), BotGoalKind.ReturnToSafeArea);
            AssertDecision(new BotDecision(BotGoalKind.Recover), BotGoalKind.Recover);
            Assert.Throws<ArgumentException>(() => new BotDecision(BotGoalKind.Paint));
            Assert.Throws<ArgumentException>(() => new BotDecision(BotGoalKind.Idle, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BotDecision((BotGoalKind)99));
        }

        private static BotObservation Observe(
            int ownTeam = 1,
            double remainingSeconds = 90d,
            bool needsSafeArea = false,
            bool stuck = false,
            bool combatReady = false,
            bool losing = false)
        {
            return new BotObservation(new TeamId(ownTeam), remainingSeconds, needsSafeArea, stuck, combatReady, losing);
        }

        private static BotTerritoryOption Neutral(int id, double distance, bool reachable = true, bool retained = true)
        {
            return new BotTerritoryOption(id, TerritoryState.Neutral, TerritoryOwner.None, distance, reachable, retained);
        }

        private static BotTerritoryOption Owned(
            int id,
            int team,
            double distance,
            bool reachable = true,
            bool retained = true)
        {
            return new BotTerritoryOption(
                id,
                team == 1 ? TerritoryState.Ice : TerritoryState.Fire,
                TerritoryOwner.ForTeam(new TeamId(team)),
                distance,
                reachable,
                retained);
        }

        private static BotOpponentOption Opponent(
            int id,
            int team,
            double distance,
            bool visible = true,
            bool protectedFromHit = false,
            bool reachable = true,
            bool isBot = false)
        {
            return new BotOpponentOption(id, new TeamId(team), distance, visible, protectedFromHit, reachable, isBot);
        }

        private static BotDecision Decide(
            BotObservation observation,
            BotTerritoryOption[] territory = null,
            BotOpponentOption[] opponents = null,
            IRandomSource random = null)
        {
            return BotTactics.Decide(
                observation,
                territory ?? Array.Empty<BotTerritoryOption>(),
                opponents ?? Array.Empty<BotOpponentOption>(),
                random ?? new SeededRandomSource(123));
        }

        private static void AssertDecision(BotDecision decision, BotGoalKind kind, int targetId = -1)
        {
            Assert.That(decision.Kind, Is.EqualTo(kind));
            Assert.That(decision.TargetId, Is.EqualTo(targetId));
        }

        private sealed class CountingRandomSource : IRandomSource
        {
            public int Calls { get; private set; }

            public int NextInt(int maximumExclusive)
            {
                Assert.That(maximumExclusive, Is.GreaterThan(1));
                Calls++;
                return 0;
            }

            public double NextUnit()
            {
                throw new InvalidOperationException("Tie breaking should use integer sampling.");
            }
        }
    }
}
