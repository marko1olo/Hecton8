using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class LufsNormalizationCalculatorTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float measuredLUFS = -14f;
            float targetLUFS = -14f;
            float maxGainDB = 10f;
            float minGainDB = -10f;

            // Act
            float result = LufsNormalizationCalculator.Compute(measuredLUFS, targetLUFS, maxGainDB, minGainDB);

            // Assert: Verify expected output behaviour
            // measured == target: 0 dB.
            Assert.AreEqual(0f, result, Tolerance, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float measuredLUFS_quiet = -20f;
            float targetLUFS = -14f;
            float maxGainDB = 5f;
            float minGainDB = -10f;

            // Act
            float resultQuiet = LufsNormalizationCalculator.Compute(measuredLUFS_quiet, targetLUFS, maxGainDB, minGainDB);

            // Loud case
            float measuredLUFS_loud = -5f;
            float resultLoud = LufsNormalizationCalculator.Compute(measuredLUFS_loud, targetLUFS, maxGainDB, minGainDB);

            // Assert
            // Quiet: positive gain up to max
            Assert.AreEqual(5f, resultQuiet, Tolerance, "Verify boundary constraints clamp correctly for max.");
            // Loud: negative, clamped
            Assert.AreEqual(-9f, resultLoud, Tolerance, "Verify boundary constraints clamp correctly for negative.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float measuredLUFS = 0f;
            float targetLUFS = 0f;
            float maxGainDB = 0f;
            float minGainDB = 0f;

            // Act
            float result = LufsNormalizationCalculator.Compute(measuredLUFS, targetLUFS, maxGainDB, minGainDB);

            // Assert
            Assert.AreEqual(0f, result, Tolerance, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float measuredLUFS = -5f;
            float targetLUFS = -14f;
            float maxGainDB = 10f;
            float minGainDB = -5f;

            // Act
            float result = LufsNormalizationCalculator.Compute(measuredLUFS, targetLUFS, maxGainDB, minGainDB);

            // Assert
            Assert.AreEqual(-5f, result, Tolerance, "Verify negative inputs clamp gracefully or throw.");

            // crossed ranges
            float resultCrossed = LufsNormalizationCalculator.Compute(-20f, -14f, -10f, 10f);
            Assert.AreEqual(10f, resultCrossed, Tolerance);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float measuredLUFS = float.NaN;
            float targetLUFS = float.PositiveInfinity;
            float maxGainDB = float.MaxValue;
            float minGainDB = float.MinValue;

            // Act
            float resultNaN = LufsNormalizationCalculator.Compute(float.NaN, -14f, 10f, -10f);
            float resultInf = LufsNormalizationCalculator.Compute(-14f, float.PositiveInfinity, 10f, -10f);

            // Assert
            Assert.AreEqual(0f, resultNaN, Tolerance, "Verify robust calculation and overflow protection (NaN).");
            Assert.AreEqual(0f, resultInf, Tolerance, "Verify robust calculation and overflow protection (Inf).");
        }
    }
}
