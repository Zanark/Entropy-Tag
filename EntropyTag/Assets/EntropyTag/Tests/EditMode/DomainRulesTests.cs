using System;
using System.Linq;
using EntropyTag.Domain;
using NUnit.Framework;

namespace EntropyTag.Tests.EditMode
{
    public sealed class DomainRulesTests
    {
        private DomainRulesConfig config;

        [SetUp]
        public void SetUp()
        {
            config = DomainRulesConfig.CreateFirstSlice();
        }

        [Test]
        public void DomainAssemblyHasNoUnityEngineReference()
        {
            string[] references = typeof(DomainRulesConfig).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("UnityEngine"));
            Assert.That(references.Any(reference => reference.StartsWith("UnityEngine.")), Is.False);
        }

        [Test]
        public void IdentifiersRejectNonPositiveValues()
        {
            Assert.That(
                () => new TeamId(0),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains("greater than zero"));
            Assert.That(
                () => new PlayerId(-1),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains("greater than zero"));
        }

        [TestCase(TerritoryState.Neutral, ElementId.Ice, TerritoryState.Ice)]
        [TestCase(TerritoryState.Neutral, ElementId.Fire, TerritoryState.Fire)]
        [TestCase(TerritoryState.Ice, ElementId.Ice, TerritoryState.Ice)]
        [TestCase(TerritoryState.Fire, ElementId.Fire, TerritoryState.Fire)]
        [TestCase(TerritoryState.Ice, ElementId.Fire, TerritoryState.Mist)]
        [TestCase(TerritoryState.Fire, ElementId.Ice, TerritoryState.Mist)]
        [TestCase(TerritoryState.Mist, ElementId.Ice, TerritoryState.Ice)]
        [TestCase(TerritoryState.Mist, ElementId.Fire, TerritoryState.Fire)]
        public void FirstSliceConversionsProduceExpectedState(
            TerritoryState existingState,
            ElementId appliedElement,
            TerritoryState expectedState)
        {
            TeamDefinition applyingTeam = GetTeam(appliedElement);
            TerritoryCell existing = CreateCell(existingState);

            TerritoryResolution result =
                config.Resolver.Resolve(existing, appliedElement, applyingTeam.Id);

            Assert.That(result.Applied, Is.True);
            Assert.That(result.Cell.State, Is.EqualTo(expectedState));

            if (expectedState == TerritoryState.Ice || expectedState == TerritoryState.Fire)
            {
                Assert.That(result.Cell.Owner, Is.EqualTo(TerritoryOwner.ForTeam(applyingTeam.Id)));
            }
            else
            {
                Assert.That(result.Cell.Owner, Is.EqualTo(TerritoryOwner.None));
            }
        }

        [Test]
        public void TeamCannotApplyUnsupportedOrMismatchedElement()
        {
            Assert.That(
                () => config.Resolver.Resolve(TerritoryCell.Neutral, ElementId.Water, config.IceTeam.Id),
                Throws.InvalidOperationException.With.Message.Contains("cannot apply Water"));
            Assert.That(
                () => config.Resolver.Resolve(TerritoryCell.Neutral, ElementId.Fire, config.IceTeam.Id),
                Throws.InvalidOperationException.With.Message.Contains("cannot apply Fire"));
        }

        [Test]
        public void EnemyNeutralizationAndReactionClaimAwardBank()
        {
            TerritoryResolution icePaint = config.Resolver.Resolve(
                TerritoryCell.Neutral,
                ElementId.Ice,
                config.IceTeam.Id);
            TerritoryResolution neutralize = config.Resolver.Resolve(
                icePaint.Cell,
                ElementId.Fire,
                config.FireTeam.Id);
            TerritoryResolution claim = config.Resolver.Resolve(
                neutralize.Cell,
                ElementId.Fire,
                config.FireTeam.Id);

            Assert.That(neutralize.Cell.State, Is.EqualTo(TerritoryState.Mist));
            Assert.That(neutralize.Cell.PreviousOwner.TeamId, Is.EqualTo(config.IceTeam.Id));
            Assert.That(neutralize.BankAward, Is.EqualTo(2));
            Assert.That(claim.Cell.State, Is.EqualTo(TerritoryState.Fire));
            Assert.That(claim.BankAward, Is.EqualTo(1));
        }

        [Test]
        public void RecoveringOwnMistDoesNotAwardBank()
        {
            TerritoryCell mist = new TerritoryCell(
                TerritoryState.Mist,
                TerritoryOwner.None,
                TerritoryOwner.ForTeam(config.IceTeam.Id));

            TerritoryResolution result =
                config.Resolver.Resolve(mist, ElementId.Ice, config.IceTeam.Id);

            Assert.That(result.Cell.State, Is.EqualTo(TerritoryState.Ice));
            Assert.That(result.BankAward, Is.Zero);
        }

        [Test]
        public void FixedStampSequenceProducesDeterministicCoverageAndBank()
        {
            var first = ExecuteFixedSequence();
            var second = ExecuteFixedSequence();

            Assert.That(second.IceOwned, Is.EqualTo(first.IceOwned));
            Assert.That(second.FireOwned, Is.EqualTo(first.FireOwned));
            Assert.That(second.Neutral, Is.EqualTo(first.Neutral));
            Assert.That(second.Mist, Is.EqualTo(first.Mist));
            Assert.That(second.FireBank, Is.EqualTo(first.FireBank));
            Assert.That(first.IceOwned, Is.EqualTo(1));
            Assert.That(first.FireOwned, Is.EqualTo(2));
            Assert.That(first.Neutral, Is.EqualTo(1));
            Assert.That(first.Mist, Is.Zero);
            Assert.That(first.FireBank, Is.EqualTo(3));
        }

        [Test]
        public void StampDeduplicatesCoordinatesAndResetClearsState()
        {
            var field = new TerritoryField(2, 2, config.Resolver);
            var coordinate = new TerritoryCoordinate(0, 0);

            StampResult result = field.ApplyStamp(
                new[] { coordinate, coordinate },
                ElementId.Ice,
                config.IceTeam.Id);

            Assert.That(result.AttemptedCells, Is.EqualTo(2));
            Assert.That(result.ChangedCells, Is.EqualTo(1));
            Assert.That(field.GetCell(coordinate).State, Is.EqualTo(TerritoryState.Ice));

            field.Reset();

            Assert.That(field.GetCell(coordinate), Is.EqualTo(TerritoryCell.Neutral));
        }

        [Test]
        public void TerritoryFieldRejectsOutOfBoundsCoordinates()
        {
            var field = new TerritoryField(2, 2, config.Resolver);

            Assert.That(
                () => field.GetCell(new TerritoryCoordinate(2, 0)),
                Throws.TypeOf<ArgumentOutOfRangeException>().With.Message.Contains("outside 2x2"));
        }

        [Test]
        public void MatchOutcomeHandlesWinnerTieAndZeroOwnership()
        {
            var zeroField = new TerritoryField(2, 1, config.Resolver);
            CoverageSnapshot zeroCoverage = CalculateCoverage(zeroField);
            Assert.That(MatchOutcome.Resolve(zeroCoverage).Kind, Is.EqualTo(MatchOutcomeKind.ZeroOwnership));

            zeroField.ApplyStamp(
                new[] { new TerritoryCoordinate(0, 0) },
                ElementId.Ice,
                config.IceTeam.Id);
            zeroField.ApplyStamp(
                new[] { new TerritoryCoordinate(1, 0) },
                ElementId.Fire,
                config.FireTeam.Id);
            CoverageSnapshot tieCoverage = CalculateCoverage(zeroField);
            Assert.That(MatchOutcome.Resolve(tieCoverage).Kind, Is.EqualTo(MatchOutcomeKind.Tie));

            var winnerField = new TerritoryField(2, 1, config.Resolver);
            winnerField.ApplyStamp(
                new[] { new TerritoryCoordinate(0, 0), new TerritoryCoordinate(1, 0) },
                ElementId.Fire,
                config.FireTeam.Id);
            MatchOutcome winner = MatchOutcome.Resolve(CalculateCoverage(winnerField));
            Assert.That(winner.Kind, Is.EqualTo(MatchOutcomeKind.Winner));
            Assert.That(winner.Winner.TeamId, Is.EqualTo(config.FireTeam.Id));
        }

        [Test]
        public void MatchClockUsesTwoMinuteDeterministicPhases()
        {
            var clock = new MatchClock(config.MatchTiming);

            Assert.That(clock.Phase, Is.EqualTo(MatchPhase.Waiting));
            Assert.That(() => clock.Advance(1d), Throws.InvalidOperationException);

            clock.Start();
            Assert.That(clock.Phase, Is.EqualTo(MatchPhase.Opening));
            clock.Advance(24d);
            Assert.That(clock.Phase, Is.EqualTo(MatchPhase.Contest));
            clock.Advance(54d);
            Assert.That(clock.Phase, Is.EqualTo(MatchPhase.Compression));
            clock.Advance(37d);
            Assert.That(clock.Phase, Is.EqualTo(MatchPhase.Resolution));
            clock.Advance(5d);

            Assert.That(clock.Phase, Is.EqualTo(MatchPhase.Complete));
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(120d));
            Assert.That(clock.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void SeededRandomSourceRepeatsSequence()
        {
            var first = new SeededRandomSource(12345);
            var second = new SeededRandomSource(12345);

            for (int index = 0; index < 32; index++)
            {
                Assert.That(second.NextInt(1000), Is.EqualTo(first.NextInt(1000)));
                Assert.That(second.NextUnit(), Is.EqualTo(first.NextUnit()));
            }
        }

        [Test]
        public void InvalidConfigurationFailsWithActionableMessage()
        {
            TerritoryReactionRule[] rules = TerritoryReactionResolver.CreateFirstSliceRules();
            rules[0] = new TerritoryReactionRule(
                TerritoryState.Neutral,
                ElementId.Ice,
                TerritoryState.Fire,
                TerritoryOwnerMode.ApplyingTeam,
                BankAwardKind.None);

            Assert.That(
                () => new DomainRulesConfig(
                    config.IceTeam,
                    config.FireTeam,
                    rules,
                    config.BankPolicy,
                    config.MatchTiming),
                Throws.ArgumentException.With.Message.Contains("does not match the first-slice contract"));
            Assert.That(
                () => new MatchTiming(120d, 30d, 20d, 110d),
                Throws.ArgumentException.With.Message.Contains("0 < opening < contest < compression < duration"));
        }

        private TeamDefinition GetTeam(ElementId element)
        {
            return element == ElementId.Ice ? config.IceTeam : config.FireTeam;
        }

        private TerritoryCell CreateCell(TerritoryState state)
        {
            switch (state)
            {
                case TerritoryState.Neutral:
                    return TerritoryCell.Neutral;
                case TerritoryState.Ice:
                    return new TerritoryCell(
                        TerritoryState.Ice,
                        TerritoryOwner.ForTeam(config.IceTeam.Id),
                        TerritoryOwner.None);
                case TerritoryState.Fire:
                    return new TerritoryCell(
                        TerritoryState.Fire,
                        TerritoryOwner.ForTeam(config.FireTeam.Id),
                        TerritoryOwner.None);
                case TerritoryState.Mist:
                    return new TerritoryCell(
                        TerritoryState.Mist,
                        TerritoryOwner.None,
                        TerritoryOwner.ForTeam(config.FireTeam.Id));
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private SequenceEvidence ExecuteFixedSequence()
        {
            var field = new TerritoryField(2, 2, config.Resolver);
            field.ApplyStamp(
                new[] { new TerritoryCoordinate(0, 0), new TerritoryCoordinate(1, 0) },
                ElementId.Ice,
                config.IceTeam.Id);
            StampResult neutralize = field.ApplyStamp(
                new[] { new TerritoryCoordinate(1, 0), new TerritoryCoordinate(0, 1) },
                ElementId.Fire,
                config.FireTeam.Id);
            StampResult claim = field.ApplyStamp(
                new[] { new TerritoryCoordinate(1, 0) },
                ElementId.Fire,
                config.FireTeam.Id);
            CoverageSnapshot coverage = CalculateCoverage(field);

            return new SequenceEvidence(
                coverage.GetTeam(config.IceTeam.Id).OwnedCells,
                coverage.GetTeam(config.FireTeam.Id).OwnedCells,
                coverage.NeutralCells,
                coverage.MistCells,
                neutralize.BankAward + claim.BankAward);
        }

        private CoverageSnapshot CalculateCoverage(TerritoryField field)
        {
            return TerritoryCoverageCalculator.Calculate(
                field,
                new[] { config.IceTeam.Id, config.FireTeam.Id });
        }

        private readonly struct SequenceEvidence
        {
            public SequenceEvidence(
                int iceOwned,
                int fireOwned,
                int neutral,
                int mist,
                int fireBank)
            {
                IceOwned = iceOwned;
                FireOwned = fireOwned;
                Neutral = neutral;
                Mist = mist;
                FireBank = fireBank;
            }

            public int IceOwned { get; }

            public int FireOwned { get; }

            public int Neutral { get; }

            public int Mist { get; }

            public int FireBank { get; }
        }
    }
}
