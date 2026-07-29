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
            // Arrange: Setup extreme or infinity values.
            //
            // These four were previously declared and then IGNORED - the Act block passed 10f/-10f inline
            // instead of maxGainDB/minGainDB - so a test whose assertions claim to "verify robust
            // calculation and overflow protection" never sent an overflow-capable value to either gain
            // clamp. That is what the CS0219 unused-local warnings on these lines were actually telling
            // anyone who read them, in a project where five of those warnings appear in every single
            // compile-gate run.
            float measuredLUFS = float.NaN;
            float targetLUFS = float.PositiveInfinity;
            float maxGainDB = float.MaxValue;
            float minGainDB = float.MinValue;

            // Act
            float resultNaN = LufsNormalizationCalculator.Compute(measuredLUFS, -14f, maxGainDB, minGainDB);
            float resultInf = LufsNormalizationCalculator.Compute(-14f, targetLUFS, maxGainDB, minGainDB);

            // Extreme but FINITE clamp bounds, with finite inputs, so execution reaches the clamp instead of
            // short-circuiting on the NaN/Infinity guard at LufsNormalizationCalculator.cs:22-27. Neither
            // case above gets that far - which is why "overflow protection" was previously unexercised.
            float resultExtremeClamp = LufsNormalizationCalculator.Compute(-20f, -14f, float.MaxValue, float.MinValue);

            // Assert
            Assert.AreEqual(0f, resultNaN, Tolerance, "Verify robust calculation and overflow protection (NaN).");
            Assert.AreEqual(0f, resultInf, Tolerance, "Verify robust calculation and overflow protection (Inf).");
            Assert.AreEqual(6f, resultExtremeClamp, Tolerance,
                "Gain of 6 dB must survive clamping against float.MaxValue/float.MinValue bounds.");
        }
    }
}
