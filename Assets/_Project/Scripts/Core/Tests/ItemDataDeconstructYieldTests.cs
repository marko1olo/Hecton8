using NUnit.Framework;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Tests
{
    [TestFixture]
    public class ItemDataDeconstructYieldTests
    {
        [Test]
        public void Test_DeterministicYield_RegularRange()
        {
            var entry = new DeconstructYieldEntry
            {
                minYield = 2,
                maxYield = 6
            };

            // 2 + ((6 - 2) >> 1) = 2 + (4 >> 1) = 2 + 2 = 4
            Assert.That(entry.ResolveDeterministicAmount(), Is.EqualTo(4));
        }

        [Test]
        public void Test_DeterministicYield_OddRange()
        {
            var entry = new DeconstructYieldEntry
            {
                minYield = 1,
                maxYield = 4
            };

            // 1 + ((4 - 1) >> 1) = 1 + (3 >> 1) = 1 + 1 = 2
            Assert.That(entry.ResolveDeterministicAmount(), Is.EqualTo(2));
        }

        [Test]
        public void Test_DeterministicYield_SameMinMax()
        {
            var entry = new DeconstructYieldEntry
            {
                minYield = 5,
                maxYield = 5
            };

            Assert.That(entry.ResolveDeterministicAmount(), Is.EqualTo(5));
        }

        [Test]
        public void Test_DeterministicYield_NegativeMin()
        {
            var entry = new DeconstructYieldEntry
            {
                minYield = -2,
                maxYield = 4
            };

            // min is clamped to 0
            // 0 + ((4 - 0) >> 1) = 2
            Assert.That(entry.ResolveDeterministicAmount(), Is.EqualTo(2));
        }

        [Test]
        public void Test_DeterministicYield_MaxLessThanMin()
        {
            var entry = new DeconstructYieldEntry
            {
                minYield = 5,
                maxYield = 2
            };

            // max is clamped to min (5)
            // 5 + ((5 - 5) >> 1) = 5
            Assert.That(entry.ResolveDeterministicAmount(), Is.EqualTo(5));
        }
    }
}
