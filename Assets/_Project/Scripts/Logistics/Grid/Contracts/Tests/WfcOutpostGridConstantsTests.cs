using NUnit.Framework;
using Unity.Mathematics;
using Hecton8.Logistics.Grid.Contracts;

namespace Hecton8.Logistics.Grid.Contracts.Tests
{
    public class WfcOutpostGridConstantsTests
    {
        [Test]
        public void Flatten_ReturnsCorrect1DIndex()
        {
            var dimensions = new int3(10, 5, 10);

            // x + dimensions.x * (z + dimensions.z * y)
            Assert.That(WfcOutpostGridConstants.Flatten(1, 0, 0, dimensions), Is.EqualTo(1));
            Assert.That(WfcOutpostGridConstants.Flatten(0, 1, 0, dimensions), Is.EqualTo(100));
            Assert.That(WfcOutpostGridConstants.Flatten(0, 0, 1, dimensions), Is.EqualTo(10));
            Assert.That(WfcOutpostGridConstants.Flatten(5, 2, 7, dimensions), Is.EqualTo(275));
            Assert.That(WfcOutpostGridConstants.Flatten(9, 4, 9, dimensions), Is.EqualTo(499));
        }

        [Test]
        public void Flatten_HandlesZeroDimensions()
        {
            var dimensions = new int3(0, 0, 0);
            Assert.That(WfcOutpostGridConstants.Flatten(5, 2, 7, dimensions), Is.EqualTo(5));
        }

        [Test]
        public void Flatten_HandlesNegativeInputs()
        {
            var dimensions = new int3(10, 5, 10);
            Assert.That(WfcOutpostGridConstants.Flatten(-1, -1, -1, dimensions), Is.EqualTo(-111));
        }
    }
}
