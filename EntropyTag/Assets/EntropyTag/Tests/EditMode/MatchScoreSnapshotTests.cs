using System;
using System.Linq;
using EntropyTag.Application;
using EntropyTag.Domain;
using NUnit.Framework;

namespace EntropyTag.Tests.EditMode
{
    public sealed class MatchScoreSnapshotTests
    {
        [Test]
        public void ApplicationAssemblyHasNoUnityEngineReference()
        {
            string[] references = typeof(MatchSession).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name).ToArray();

            Assert.That(references, Does.Not.Contain("UnityEngine"));
            Assert.That(references.Any(reference => reference.StartsWith("UnityEngine.")), Is.False);
        }

        [TestCase(2, 1, 0, 1000, MatchOutcomeKind.Winner, 1)]
        [TestCase(1, 2, 1000, 0, MatchOutcomeKind.Winner, 2)]
        [TestCase(1, 1, 0, 1000, MatchOutcomeKind.Tie, 0)]
        [TestCase(1, 1, 1000, 0, MatchOutcomeKind.Tie, 0)]
        [TestCase(0, 0, 1000, 1, MatchOutcomeKind.ZeroOwnership, 0)]
        public void TerritoryAloneDeterminesOutcome(
            int ice, int fire, int iceBank, int fireBank, MatchOutcomeKind kind, int winner)
        {
            var score = new MatchScoreSnapshot(Coverage(ice + fire + 2, 1, 1, ice, fire), iceBank, fireBank);

            Assert.That(score.Outcome.Kind, Is.EqualTo(kind));
            Assert.That(score.Outcome.Winner.HasTeam, Is.EqualTo(winner != 0));
            Assert.That(score.Outcome.Winner.TeamId.Value, Is.EqualTo(winner));
            Assert.That(score.IceBank, Is.EqualTo(iceBank));
            Assert.That(score.FireBank, Is.EqualTo(fireBank));
        }

        [Test]
        public void ZeroEligibleCellsHaveZeroOwnershipAndPercentagesDespiteBankAndReactions()
        {
            var score = new MatchScoreSnapshot(Coverage(0, 0, 0, 0, 0), 100, 1, 9, 8, 7, 6);

            Assert.That(score.TotalCells, Is.Zero);
            Assert.That(score.NeutralCells, Is.Zero);
            Assert.That(score.MistCells, Is.Zero);
            Assert.That(score.IceCoverage.OwnedCells, Is.Zero);
            Assert.That(score.FireCoverage.OwnedCells, Is.Zero);
            Assert.That(score.IceCoverage.Percentage, Is.Zero);
            Assert.That(score.FireCoverage.Percentage, Is.Zero);
            Assert.That(score.Outcome.Kind, Is.EqualTo(MatchOutcomeKind.ZeroOwnership));
        }

        [Test]
        public void SnapshotCopiesCoverageAndAllReactionCounters()
        {
            CoverageSnapshot coverage = Coverage(10, 1, 2, 3, 4);
            var score = new MatchScoreSnapshot(coverage, 5, 6, 7, 8, 9, 10);
            coverage.Teams[0] = new TeamCoverage(new TeamId(1), 10, 10);
            coverage.Teams[1] = new TeamCoverage(new TeamId(2), 0, 10);

            Assert.That(score.TotalCells, Is.EqualTo(10));
            Assert.That(score.NeutralCells, Is.EqualTo(1));
            Assert.That(score.MistCells, Is.EqualTo(2));
            Assert.That(score.IceCoverage.TeamId, Is.EqualTo(new TeamId(1)));
            Assert.That(score.FireCoverage.TeamId, Is.EqualTo(new TeamId(2)));
            Assert.That(score.IceCoverage.OwnedCells, Is.EqualTo(3));
            Assert.That(score.FireCoverage.OwnedCells, Is.EqualTo(4));
            Assert.That(score.IceCoverage.Percentage, Is.EqualTo(30d));
            Assert.That(score.FireCoverage.Percentage, Is.EqualTo(40d));
            Assert.That(score.IceBank, Is.EqualTo(5));
            Assert.That(score.FireBank, Is.EqualTo(6));
            Assert.That(score.IceMistCreatedCells, Is.EqualTo(7));
            Assert.That(score.FireMistCreatedCells, Is.EqualTo(8));
            Assert.That(score.IceMistClaimedCells, Is.EqualTo(9));
            Assert.That(score.FireMistClaimedCells, Is.EqualTo(10));
            Assert.That(score.Outcome.Winner.TeamId, Is.EqualTo(new TeamId(2)));
        }

        [Test]
        public void TeamArrayOrderDoesNotAffectIdentity()
        {
            var coverage = new CoverageSnapshot(3, 0, 0, new[]
            {
                new TeamCoverage(new TeamId(2), 2, 3),
                new TeamCoverage(new TeamId(1), 1, 3)
            });
            var score = new MatchScoreSnapshot(coverage, 0, 0);

            Assert.That(score.IceCoverage.OwnedCells, Is.EqualTo(1));
            Assert.That(score.FireCoverage.OwnedCells, Is.EqualTo(2));
            Assert.That(score.Outcome.Winner.TeamId.Value, Is.EqualTo(2));
            Assert.That(score.IceMistCreatedCells, Is.Zero);
            Assert.That(score.FireMistCreatedCells, Is.Zero);
            Assert.That(score.IceMistClaimedCells, Is.Zero);
            Assert.That(score.FireMistClaimedCells, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void EveryBankAndReactionCounterRejectsNegativeValues(int index)
        {
            int[] counters = new int[6];
            counters[index] = -1;

            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchScoreSnapshot(
                Coverage(1, 1, 0, 0, 0),
                counters[0], counters[1], counters[2], counters[3], counters[4], counters[5]));
        }

        [TestCase(-1, 0, 0, 0, 0)]
        [TestCase(1, -1, 0, 1, 1)]
        [TestCase(1, 0, -1, 1, 1)]
        [TestCase(1, 0, 0, -1, 2)]
        [TestCase(1, 0, 0, 2, -1)]
        [TestCase(1, 0, 0, 2, 0)]
        [TestCase(1, 0, 0, 0, 2)]
        [TestCase(1, 0, 0, 0, 0)]
        [TestCase(1, 1, 1, 0, 0)]
        [TestCase(0, 0, 0, 1, 0)]
        [TestCase(int.MaxValue, int.MaxValue, int.MaxValue, 0, 0)]
        public void InvalidCoverageCountsAreRejected(int total, int neutral, int mist, int ice, int fire)
        {
            Assert.That(() => new MatchScoreSnapshot(Coverage(total, neutral, mist, ice, fire), 0, 0),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void NullMissingDuplicateUnknownAndInconsistentTeamCoverageAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new MatchScoreSnapshot(null, 0, 0));
            TeamCoverage ice = new TeamCoverage(new TeamId(1), 0, 1);
            TeamCoverage fire = new TeamCoverage(new TeamId(2), 0, 1);
            TeamCoverage[][] invalidTeams =
            {
                Array.Empty<TeamCoverage>(),
                new[] { ice },
                new[] { ice, fire, new TeamCoverage(new TeamId(3), 0, 1) },
                new[] { ice, ice },
                new[] { fire, fire },
                new[] { ice, new TeamCoverage(new TeamId(3), 0, 1) },
                new[] { ice, new TeamCoverage(default, 0, 1) },
                new[] { ice, new TeamCoverage(new TeamId(2), 0, 2) }
            };

            foreach (TeamCoverage[] teams in invalidTeams)
            {
                Assert.Throws<ArgumentException>(
                    () => new MatchScoreSnapshot(new CoverageSnapshot(1, 1, 0, teams), 0, 0));
            }
        }

        private static CoverageSnapshot Coverage(int total, int neutral, int mist, int ice, int fire)
        {
            return new CoverageSnapshot(total, neutral, mist, new[]
            {
                new TeamCoverage(new TeamId(1), ice, total),
                new TeamCoverage(new TeamId(2), fire, total)
            });
        }
    }
}
