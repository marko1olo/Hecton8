using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BiomeDiscoveryBitmaskTrackerTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            uint currentMask = 0;
            int biomeIndex = 5;

            // Act
            uint result = BiomeDiscoveryBitmaskTracker.Calculate(currentMask, biomeIndex);

            // Assert: Verify expected output behaviour
            // 1u << 5 is 32.
            Assert.AreEqual(32u, result);

            // Additional happy path test
            currentMask = 32u;
            result = BiomeDiscoveryBitmaskTracker.Calculate(currentMask, 2);
            // 32 | (1 << 2) = 32 | 4 = 36
            Assert.AreEqual(36u, result);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            uint currentMask = 0;

            // Act
            uint resultMin = BiomeDiscoveryBitmaskTracker.Calculate(currentMask, 0);
            uint resultMax = BiomeDiscoveryBitmaskTracker.Calculate(currentMask, 31);

            // Assert
            Assert.AreEqual(1u, resultMin); // 1 << 0
            Assert.AreEqual(2147483648u, resultMax); // 1 << 31
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            uint currentMask = 0;
            int biomeIndex = 0;

            // Act
            uint result = BiomeDiscoveryBitmaskTracker.Calculate(currentMask, biomeIndex);

            // Assert
            Assert.AreEqual(1u, result);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            uint currentMask = 0;
            int biomeIndex = -5;

            // Act
            uint result = BiomeDiscoveryBitmaskTracker.Calculate(currentMask, biomeIndex);

            // Assert
            // Negative value should be clamped to 0. So 1 << 0 = 1.
            Assert.AreEqual(1u, result);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            uint currentMask = 0;
            int biomeIndex = int.MaxValue;

            // Act
            uint result = BiomeDiscoveryBitmaskTracker.Calculate(currentMask, biomeIndex);

            // Assert
            // Extreme value should be clamped to 31. So 1 << 31.
            Assert.AreEqual(2147483648u, result);
        }
    }
}
