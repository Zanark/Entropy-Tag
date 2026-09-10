using EntropyTag.Domain;
using NUnit.Framework;

namespace EntropyTag.Tests.EditMode
{
    public sealed class TerritoryReactionStatsTests
    {
        private DomainRulesConfig config;
        private TerritoryField field;
        private readonly TerritoryCoordinate first = new TerritoryCoordinate(0, 0);
        private readonly TerritoryCoordinate second = new TerritoryCoordinate(1, 0);

        [SetUp]
        public void SetUp()
        {
            config = DomainRulesConfig.CreateFirstSlice();
            field = new TerritoryField(2, 1, config.Resolver);
        }

        [Test]
        public void LegacyAndDefaultStampResultsHaveZeroReactionCounters()
        {
            var legacy = new StampResult(4, 2, 3);
            StampResult empty = default;
            var extended = new StampResult(5, 4, 6, 2, 1);

            Assert.That(legacy.AttemptedCells, Is.EqualTo(4));
            Assert.That(legacy.ChangedCells, Is.EqualTo(2));
            Assert.That(legacy.BankAward, Is.EqualTo(3));
            Assert.That(legacy.MistCreatedCells, Is.Zero);
            Assert.That(legacy.MistClaimedCells, Is.Zero);
            Assert.That(empty.AttemptedCells, Is.Zero);
            Assert.That(empty.ChangedCells, Is.Zero);
            Assert.That(empty.BankAward, Is.Zero);
            Assert.That(empty.MistCreatedCells, Is.Zero);
            Assert.That(empty.MistClaimedCells, Is.Zero);
            Assert.That(extended.MistCreatedCells, Is.EqualTo(2));
            Assert.That(extended.MistClaimedCells, Is.EqualTo(1));
        }

        [TestCase(ElementId.Ice, ElementId.Fire)]
        [TestCase(ElementId.Fire, ElementId.Ice)]
        public void ReactionsCountActualUniqueTransitionsSeparatelyFromNeutralPainting(
            ElementId original, ElementId opponent)
        {
            StampResult paint = Stamp(original, first);
            StampResult sameElement = Stamp(original, first, first);
            StampResult create = Stamp(opponent, first, first, second, second);
            StampResult claim = Stamp(opponent, first, first);
            StampResult repeat = Stamp(opponent, first, first);

            Assert.That(paint.MistCreatedCells, Is.Zero);
            Assert.That(paint.MistClaimedCells, Is.Zero);
            Assert.That(sameElement.ChangedCells, Is.Zero);
            Assert.That(sameElement.MistCreatedCells, Is.Zero);
            Assert.That(sameElement.MistClaimedCells, Is.Zero);
            Assert.That(create.AttemptedCells, Is.EqualTo(4));
            Assert.That(create.ChangedCells, Is.EqualTo(2));
            Assert.That(create.MistCreatedCells, Is.EqualTo(1));
            Assert.That(create.MistClaimedCells, Is.Zero);
            Assert.That(create.BankAward, Is.EqualTo(2));
            Assert.That(claim.AttemptedCells, Is.EqualTo(2));
            Assert.That(claim.ChangedCells, Is.EqualTo(1));
            Assert.That(claim.MistCreatedCells, Is.Zero);
            Assert.That(claim.MistClaimedCells, Is.EqualTo(1));
            Assert.That(claim.BankAward, Is.EqualTo(1));
            Assert.That(repeat.ChangedCells, Is.Zero);
            Assert.That(repeat.BankAward, Is.Zero);
            Assert.That(repeat.MistCreatedCells, Is.Zero);
            Assert.That(repeat.MistClaimedCells, Is.Zero);
        }

        [TestCase(ElementId.Ice, ElementId.Fire)]
        [TestCase(ElementId.Fire, ElementId.Ice)]
        public void OwnProvenanceClaimCountsEvenWhenBankIsZero(ElementId original, ElementId opponent)
        {
            Stamp(original, first);
            Stamp(opponent, first);

            Assert.That(field.GetCell(first).State, Is.EqualTo(TerritoryState.Mist));
            Assert.That(field.GetCell(first).Owner.HasTeam, Is.False);
            Assert.That(field.GetCell(first).PreviousOwner.TeamId, Is.EqualTo(Team(original)));

            StampResult claim = Stamp(original, first, first);

            Assert.That(claim.MistCreatedCells, Is.Zero);
            Assert.That(claim.MistClaimedCells, Is.EqualTo(1));
            Assert.That(claim.BankAward, Is.Zero);
            Assert.That(field.GetCell(first).Owner.TeamId, Is.EqualTo(Team(original)));
            Assert.That(field.GetCell(first).PreviousOwner.HasTeam, Is.False);
        }

        [Test]
        public void ResetClearsProvenanceAndLaterStampsDoNotInheritReactionCounts()
        {
            Stamp(ElementId.Ice, first, second);
            StampResult beforeReset = Stamp(ElementId.Fire, first, second, first, second);
            Assert.That(beforeReset.MistCreatedCells, Is.EqualTo(2));

            field.Reset();

            Assert.That(field.GetCell(first), Is.EqualTo(TerritoryCell.Neutral));
            Assert.That(field.GetCell(second), Is.EqualTo(TerritoryCell.Neutral));
            StampResult fresh = Stamp(ElementId.Fire, first, first, second, second);
            Assert.That(fresh.ChangedCells, Is.EqualTo(2));
            Assert.That(fresh.BankAward, Is.Zero);
            Assert.That(fresh.MistCreatedCells, Is.Zero);
            Assert.That(fresh.MistClaimedCells, Is.Zero);
            Assert.That(beforeReset.MistCreatedCells, Is.EqualTo(2));
        }

        private StampResult Stamp(ElementId element, params TerritoryCoordinate[] coordinates)
        {
            return field.ApplyStamp(coordinates, element, Team(element));
        }

        private TeamId Team(ElementId element)
        {
            return element == ElementId.Ice ? config.IceTeam.Id : config.FireTeam.Id;
        }
    }
}
