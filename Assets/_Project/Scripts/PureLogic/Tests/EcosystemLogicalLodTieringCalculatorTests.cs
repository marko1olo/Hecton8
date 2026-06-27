using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class EcosystemLogicalLodTieringCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float distanceSq = 100f;
            float zone1RadiusSq = 400f;
            float zone2RadiusSq = 1000f;
            float qualityWeight = 0f;

            // Act
            int tier = EcosystemLogicalLodTieringCalculator.Compute(distanceSq, zone1RadiusSq, zone2RadiusSq, qualityWeight);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(0, tier); // distanceSq (100) < zone1RadiusSq (400) -> FullSim (0)

            // test zone 2
            tier = EcosystemLogicalLodTieringCalculator.Compute(600f, zone1RadiusSq, zone2RadiusSq, qualityWeight);
            Assert.AreEqual(1, tier);

            // test zone 3
            tier = EcosystemLogicalLodTieringCalculator.Compute(1200f, zone1RadiusSq, zone2RadiusSq, qualityWeight);
            Assert.AreEqual(2, tier);

            // Verify standard calculations return expected results.
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float zone1RadiusSq = 400f;
            float zone2RadiusSq = 1000f;
            float qualityWeight = 1f;

            // Expanded zone 1: 400 * (1 + 1) = 800

            // Act & Assert
            int tier = EcosystemLogicalLodTieringCalculator.Compute(800f, zone1RadiusSq, zone2RadiusSq, qualityWeight);
            Assert.AreEqual(1, tier); // 800 is not < 800, so it falls to zone2 check -> DataOnly

            tier = EcosystemLogicalLodTieringCalculator.Compute(799.9f, zone1RadiusSq, zone2RadiusSq, qualityWeight);
            Assert.AreEqual(0, tier); // FullSim

            tier = EcosystemLogicalLodTieringCalculator.Compute(1000f, zone1RadiusSq, zone2RadiusSq, qualityWeight);
            Assert.AreEqual(1, tier); // <= 1000 -> DataOnly

            tier = EcosystemLogicalLodTieringCalculator.Compute(1000.1f, zone1RadiusSq, zone2RadiusSq, qualityWeight);
            Assert.AreEqual(2, tier); // Hibernating
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            int tier = EcosystemLogicalLodTieringCalculator.Compute(0f, 0f, 0f, 0f);

            // Assert
            Assert.AreEqual(1, tier); // distanceSq (0) not < zone1 (0), but <= zone2 (0), so 1

            tier = EcosystemLogicalLodTieringCalculator.Compute(1f, 0f, 0f, 0f);
            Assert.AreEqual(2, tier); // > 0, so 2
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            int tier = EcosystemLogicalLodTieringCalculator.Compute(-10f, -20f, -30f, -5f);

            // Assert: Should clamp to 0s
            // same as 0, 0, 0, 0
            Assert.AreEqual(1, tier);

            tier = EcosystemLogicalLodTieringCalculator.Compute(-10f, 10f, 20f, 0f);
            // distanceSq becomes 0. 0 < 10 -> 0
            Assert.AreEqual(0, tier);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            int tier = EcosystemLogicalLodTieringCalculator.Compute(float.PositiveInfinity, 10f, 20f, float.NaN);

            // Assert: Infinite distance should return Hibernating (2)
            Assert.AreEqual(2, tier);

            tier = EcosystemLogicalLodTieringCalculator.Compute(float.NaN, float.PositiveInfinity, float.NaN, float.PositiveInfinity);
            // 0, 0, 0, 0
            Assert.AreEqual(1, tier);
        }
    }
}
