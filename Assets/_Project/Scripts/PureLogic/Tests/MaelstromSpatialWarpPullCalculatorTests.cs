using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class MaelstromSpatialWarpPullCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 objectPos = new Vector3(10f, 0f, 0f);
            Vector3 corePos = new Vector3(0f, 0f, 0f);
            float coreRadius = 10f;
            float warpStrength = 5f;

            // Act
            Vector3 result = MaelstromSpatialWarpPullCalculator.Compute(objectPos, corePos, coreRadius, warpStrength);

            // Assert
            Assert.IsTrue(result.Length() > 0, "Pull should be greater than zero for valid distance");
            Assert.AreEqual(-1f, Vector3.Normalize(result).X, 0.01f, "Pull direction should be towards core");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector3 corePos = new Vector3(0f, 0f, 0f);
            float coreRadius = 10f;
            float warpStrength = 5f;

            // Act: exactly on the boundary (2x coreRadius)
            Vector3 exactlyOnBoundary = new Vector3(20f, 0f, 0f);
            Vector3 justOutside = new Vector3(20.1f, 0f, 0f);
            Vector3 justInside = new Vector3(19.9f, 0f, 0f);

            Vector3 resultOn = MaelstromSpatialWarpPullCalculator.Compute(exactlyOnBoundary, corePos, coreRadius, warpStrength);
            Vector3 resultOutside = MaelstromSpatialWarpPullCalculator.Compute(justOutside, corePos, coreRadius, warpStrength);
            Vector3 resultInside = MaelstromSpatialWarpPullCalculator.Compute(justInside, corePos, coreRadius, warpStrength);

            // Assert
            Assert.AreEqual(Vector3.Zero, resultOn, "Exactly at 2x radius should be zero");
            Assert.AreEqual(Vector3.Zero, resultOutside, "Outside 2x radius should be zero");
            Assert.IsTrue(resultInside.Length() > 0, "Just inside 2x radius should have pull");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            Vector3 objectPos = new Vector3(0f, 0f, 0f);
            Vector3 corePos = new Vector3(0f, 0f, 0f);
            float coreRadius = 0f;
            float warpStrength = 0f;

            // Act
            Vector3 result1 = MaelstromSpatialWarpPullCalculator.Compute(objectPos, corePos, 10f, 5f);
            Vector3 result2 = MaelstromSpatialWarpPullCalculator.Compute(new Vector3(5f, 0f, 0f), corePos, coreRadius, 5f);
            Vector3 result3 = MaelstromSpatialWarpPullCalculator.Compute(new Vector3(5f, 0f, 0f), corePos, 10f, warpStrength);

            // Assert
            Assert.AreEqual(Vector3.Zero, result1, "Same position should return zero");
            Assert.AreEqual(Vector3.Zero, result2, "Zero radius should return zero");
            Assert.AreEqual(Vector3.Zero, result3, "Zero strength should return zero");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 objectPos = new Vector3(5f, 0f, 0f);
            Vector3 corePos = new Vector3(0f, 0f, 0f);

            // Act
            Vector3 result1 = MaelstromSpatialWarpPullCalculator.Compute(objectPos, corePos, -10f, 5f);
            Vector3 result2 = MaelstromSpatialWarpPullCalculator.Compute(objectPos, corePos, 10f, -5f);

            // Assert
            Assert.AreEqual(Vector3.Zero, result1, "Negative radius should return zero");
            Assert.AreEqual(Vector3.Zero, result2, "Negative strength should return zero");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 objectPos = new Vector3(float.PositiveInfinity, 0f, 0f);
            Vector3 corePos = new Vector3(0f, 0f, 0f);

            // Act
            Vector3 result1 = MaelstromSpatialWarpPullCalculator.Compute(objectPos, corePos, 10f, 5f);
            Vector3 result2 = MaelstromSpatialWarpPullCalculator.Compute(new Vector3(100f, 0f, 0f), corePos, float.NaN, 5f);
            Vector3 result3 = MaelstromSpatialWarpPullCalculator.Compute(new Vector3(100f, 0f, 0f), corePos, 10f, float.NaN);

            // Assert
            Assert.AreEqual(Vector3.Zero, result1, "Infinity position should return zero");
            Assert.AreEqual(Vector3.Zero, result2, "NaN radius should return zero");
            Assert.AreEqual(Vector3.Zero, result3, "NaN strength should return zero");
        }
    }
}
