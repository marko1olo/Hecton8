using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class NitrogenNarcosisInputDrifterTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector2 rawInput = new Vector2(0f, 0f);
            float narcosisDepth01 = 1.0f;
            float timeSeconds = 1.0f;
            int seed = 12345;

            // Act
            Vector2 result = NitrogenNarcosisInputDrifter.Calculate(rawInput, narcosisDepth01, timeSeconds, seed);

            // Assert: Verify expected output behaviour
            // Expect some drift to have been applied, so magnitude > 0
            Assert.IsTrue(result.LengthSquared() > 0.0001f, "Drift was not applied for full depth.");
            Assert.IsTrue(Math.Abs(result.X) <= 1.0f, "X should be clamped between -1 and 1");
            Assert.IsTrue(Math.Abs(result.Y) <= 1.0f, "Y should be clamped between -1 and 1");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector2 rawInput = new Vector2(2f, -5f);
            float narcosisDepth01 = 2.0f; // Should clamp to 1
            float timeSeconds = 100f;
            int seed = int.MaxValue;

            // Act
            Vector2 result = NitrogenNarcosisInputDrifter.Calculate(rawInput, narcosisDepth01, timeSeconds, seed);

            // Assert
            Assert.IsTrue(result.X <= 1.0f && result.X >= -1.0f, "Exceeded X boundary bounds");
            Assert.IsTrue(result.Y <= 1.0f && result.Y >= -1.0f, "Exceeded Y boundary bounds");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            Vector2 rawInput = new Vector2(0.5f, 0.5f);
            float narcosisDepth01 = 0f; // 0 depth should return exact same input, just clamped
            float timeSeconds = 0f;
            int seed = 0;

            // Act
            Vector2 result = NitrogenNarcosisInputDrifter.Calculate(rawInput, narcosisDepth01, timeSeconds, seed);

            // Assert
            Assert.AreEqual(0.5f, result.X, 0.0001f, "Zero depth should return unaltered X input");
            Assert.AreEqual(0.5f, result.Y, 0.0001f, "Zero depth should return unaltered Y input");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector2 rawInput = new Vector2(-0.8f, -0.8f);
            float narcosisDepth01 = -1.5f; // Should clamp to 0
            float timeSeconds = -10f;
            int seed = -999;

            // Act
            Vector2 result = NitrogenNarcosisInputDrifter.Calculate(rawInput, narcosisDepth01, timeSeconds, seed);

            // Assert
            // With narcosisDepth01 clamped to 0, no noise is applied, should return exact input.
            Assert.AreEqual(-0.8f, result.X, 0.0001f, "Negative depth should clamp to 0 and not apply drift");
            Assert.AreEqual(-0.8f, result.Y, 0.0001f, "Negative depth should clamp to 0 and not apply drift");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector2 rawInput = new Vector2(float.PositiveInfinity, float.NaN);
            float narcosisDepth01 = float.NegativeInfinity;
            float timeSeconds = float.NaN;
            int seed = 0;

            // Act
            Vector2 result = NitrogenNarcosisInputDrifter.Calculate(rawInput, narcosisDepth01, timeSeconds, seed);

            // Assert
            Assert.AreEqual(1f, result.X, "PositiveInfinity should clamp to 1");
            Assert.AreEqual(0f, result.Y, "NaN should clamp to 0");
        }

        [Test]
        public void Test_SeedDeterminism()
        {
            // Verify that for a fixed state, results are identical
            Vector2 rawInput = new Vector2(0f, 0f);
            float narcosisDepth01 = 1.0f;
            float timeSeconds = 12.3f;
            int seed = 8452;

            Vector2 res1 = NitrogenNarcosisInputDrifter.Calculate(rawInput, narcosisDepth01, timeSeconds, seed);
            Vector2 res2 = NitrogenNarcosisInputDrifter.Calculate(rawInput, narcosisDepth01, timeSeconds, seed);

            Assert.AreEqual(res1.X, res2.X, 0.000001f, "Non-deterministic X result");
            Assert.AreEqual(res1.Y, res2.Y, 0.000001f, "Non-deterministic Y result");
        }
    }
}