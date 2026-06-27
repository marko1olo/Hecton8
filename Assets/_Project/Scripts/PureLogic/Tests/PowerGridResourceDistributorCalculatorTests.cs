using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PowerGridResourceDistributorCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float generatedPower = 100f;
            float[] nodeDemands = { 20f, 50f, 40f };
            int[] nodePriorities = { 1, 3, 2 }; // highest priority is 3 (index 1), then 2 (index 2), then 1 (index 0)

            float[] result = PowerGridResourceDistributorCalculator.Compute(generatedPower, nodeDemands, nodePriorities);

            // Index 1 gets 50 (rem: 50)
            // Index 2 gets 40 (rem: 10)
            // Index 0 gets 10 (rem: 0)
            Assert.That(result[0], Is.EqualTo(10f));
            Assert.That(result[1], Is.EqualTo(50f));
            Assert.That(result[2], Is.EqualTo(40f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float generatedPower = 50f;
            float[] nodeDemands = { 20f, 50f, 40f };
            int[] nodePriorities = { 1, 3, 2 };

            float[] result = PowerGridResourceDistributorCalculator.Compute(generatedPower, nodeDemands, nodePriorities);

            // Index 1 gets 50 (rem: 0)
            // Index 2 gets 0
            // Index 0 gets 0
            Assert.That(result[0], Is.EqualTo(0f));
            Assert.That(result[1], Is.EqualTo(50f));
            Assert.That(result[2], Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float generatedPower = 0f;
            float[] nodeDemands = { 20f, 50f, 40f };
            int[] nodePriorities = { 1, 3, 2 };

            float[] result = PowerGridResourceDistributorCalculator.Compute(generatedPower, nodeDemands, nodePriorities);

            Assert.That(result[0], Is.EqualTo(0f));
            Assert.That(result[1], Is.EqualTo(0f));
            Assert.That(result[2], Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float generatedPower = -100f; // negative power -> 0
            float[] nodeDemands = { -20f, 50f, -40f }; // negative demands -> 0
            int[] nodePriorities = { 1, 3, 2 };

            float[] result = PowerGridResourceDistributorCalculator.Compute(generatedPower, nodeDemands, nodePriorities);

            Assert.That(result[0], Is.EqualTo(0f));
            Assert.That(result[1], Is.EqualTo(0f));
            Assert.That(result[2], Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float generatedPower = float.NaN;
            float[] nodeDemands = { float.PositiveInfinity, float.NaN, float.NegativeInfinity };
            int[] nodePriorities = { 1, 3, 2 };

            float[] result = PowerGridResourceDistributorCalculator.Compute(generatedPower, nodeDemands, nodePriorities);

            Assert.That(result[0], Is.EqualTo(0f));
            Assert.That(result[1], Is.EqualTo(0f));
            Assert.That(result[2], Is.EqualTo(0f));
        }

        [Test]
        public void Test_Validation_Case06()
        {
            Assert.Throws<ArgumentNullException>(() => PowerGridResourceDistributorCalculator.Compute(100f, null!, new int[] { 1 }));
            Assert.Throws<ArgumentNullException>(() => PowerGridResourceDistributorCalculator.Compute(100f, new float[] { 10f }, null!));
            Assert.Throws<ArgumentException>(() => PowerGridResourceDistributorCalculator.Compute(100f, new float[] { 10f, 20f }, new int[] { 1 }));
        }

        [Test]
        public void Test_EqualPriorities_Case07()
        {
            float generatedPower = 30f;
            float[] nodeDemands = { 20f, 20f };
            int[] nodePriorities = { 1, 1 };

            float[] result = PowerGridResourceDistributorCalculator.Compute(generatedPower, nodeDemands, nodePriorities);

            Assert.That(result[0] + result[1], Is.EqualTo(30f));
        }
    }
}
