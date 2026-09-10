using EntropyTag.Application;
using EntropyTag.Domain;
using NUnit.Framework;

namespace EntropyTag.Tests.EditMode
{
    public sealed class ProjectIdentityTests
    {
        [Test]
        public void DefaultStartupStateMatchesProductIdentity()
        {
            StartupState state = StartupState.CreateDefault();

            Assert.That(state.CompanyName, Is.EqualTo(GameIdentity.CompanyName));
            Assert.That(state.ProductName, Is.EqualTo(GameIdentity.ProductName));
        }

        [Test]
        public void ElementIdentifiersRemainDistinct()
        {
            Assert.That(ElementId.Ice, Is.Not.EqualTo(ElementId.Water));
            Assert.That(ElementId.Water, Is.Not.EqualTo(ElementId.Fire));
            Assert.That(ElementId.Fire, Is.Not.EqualTo(ElementId.Ice));
        }
    }
}
