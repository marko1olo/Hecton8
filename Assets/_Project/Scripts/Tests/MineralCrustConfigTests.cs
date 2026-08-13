using NUnit.Framework;
using Hecton8.Caves;

namespace Hecton8.Tests.Caves
{
    [TestFixture]
    public class MineralCrustConfigTests
    {
        [Test]
        public void DefaultConstructor_HasExpectedValues()
        {
            var config = new MineralCrustConfig();

            Assert.IsTrue(config.enabled);
            Assert.That(config.coverage, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(config.noiseScale, Is.EqualTo(10f).Within(0.001f));
        }
    }
}
