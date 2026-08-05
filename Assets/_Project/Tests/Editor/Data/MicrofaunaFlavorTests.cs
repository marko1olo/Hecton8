using NUnit.Framework;
using Hecton8;

namespace Hecton8.Tests.Data
{
    [TestFixture]
    public class MicrofaunaFlavorTests
    {
        [Test]
        public void MicrofaunaFlavor_Initialization_SetsValuesCorrectly()
        {
            var flavor = new MicrofaunaFlavor
            {
                id = "test_flavor",
                spawnWeight = 0.75f,
                maxDepth = 500f
            };

            Assert.That(flavor.id, Is.EqualTo("test_flavor"));
            Assert.That(flavor.spawnWeight, Is.EqualTo(0.75f));
            Assert.That(flavor.maxDepth, Is.EqualTo(500f));
        }
    }
}
