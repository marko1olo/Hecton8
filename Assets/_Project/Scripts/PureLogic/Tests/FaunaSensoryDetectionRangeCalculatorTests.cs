using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FaunaSensoryDetectionRangeCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector3 predatorPos = new Vector3(0, 0, 0);
            Vector3 preyPosInVisual = new Vector3(0, 0, 40); // Within baseVisualRange (50) and low turbidity
            Vector3 preyPosOutOfVisual = new Vector3(0, 0, 60); // Out of baseVisualRange

            float lowTurbidity = 0f; // No visual reduction
            float lowSpeed = 0f; // No hearing reduction

            // Act
            bool detectedInVisual = FaunaSensoryDetectionRangeCalculator.Compute(predatorPos, preyPosInVisual, lowTurbidity, lowSpeed);
            bool detectedOutOfVisual = FaunaSensoryDetectionRangeCalculator.Compute(predatorPos, preyPosOutOfVisual, lowTurbidity, lowSpeed);

            // Assert: Verify expected output behaviour
            Assert.That(detectedInVisual, Is.True);
            Assert.That(detectedOutOfVisual, Is.False);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector3 predatorPos = new Vector3(0, 0, 0);

            // At high speed, hearing range increases up to maxHearingRange (100)
            // speedHearingScale is 2, so 50 speed gets us 100 range.
            float highSpeed = 50f;
            Vector3 preyPosAtBoundary = new Vector3(0, 0, 100);
            Vector3 preyPosJustOutside = new Vector3(0, 0, 100.1f);
            float highTurbidity = 10f; // Visual range basically 0

            // Act
            bool detectedAtBoundary = FaunaSensoryDetectionRangeCalculator.Compute(predatorPos, preyPosAtBoundary, highTurbidity, highSpeed);
            bool detectedJustOutside = FaunaSensoryDetectionRangeCalculator.Compute(predatorPos, preyPosJustOutside, highTurbidity, highSpeed);

            // Assert
            Assert.That(detectedAtBoundary, Is.True);
            Assert.That(detectedJustOutside, Is.False);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            Vector3 zeroPos = Vector3.Zero;
            float zeroTurbidity = 0f;
            float zeroSpeed = 0f;

            // Act
            bool result = FaunaSensoryDetectionRangeCalculator.Compute(zeroPos, zeroPos, zeroTurbidity, zeroSpeed);

            // Assert
            Assert.That(result, Is.True, "Should definitely detect prey at distance 0.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 predatorPos = new Vector3(0, 0, 0);
            Vector3 preyPos = new Vector3(0, 0, 45); // Within visual range if negative turbidity clamped to 0

            float negativeTurbidity = -5f;
            float negativeSpeed = -10f;

            // Act
            bool result = FaunaSensoryDetectionRangeCalculator.Compute(predatorPos, preyPos, negativeTurbidity, negativeSpeed);

            // Assert
            // Visual range should be 50 * e^0 = 50
            Assert.That(result, Is.True);

            // Check that negative speed clamps to 0 (no hearing range)
            Vector3 preyPosOutOfVisual = new Vector3(0, 0, 55);
            bool resultOutOfVisual = FaunaSensoryDetectionRangeCalculator.Compute(predatorPos, preyPosOutOfVisual, 0f, negativeSpeed);
            Assert.That(resultOutOfVisual, Is.False);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 predatorPos = new Vector3(0, 0, 0);
            Vector3 preyPos = new Vector3(0, 0, 150); // Far away

            // Infinity speed should clamp to 0 or safe values (our code sets inf to 0)
            float infSpeed = float.PositiveInfinity;
            float nanTurbidity = float.NaN;

            // Act
            bool resultInfSpeed = FaunaSensoryDetectionRangeCalculator.Compute(predatorPos, preyPos, 0f, infSpeed);

            // With NaN turbidity, visual range should reset to 0 (safe fallback) or full
            // Our code resets to 0 turbidity => base visual range 50
            bool resultNaNVisual = FaunaSensoryDetectionRangeCalculator.Compute(predatorPos, new Vector3(0, 0, 45), nanTurbidity, 0f);

            // Assert
            Assert.That(resultInfSpeed, Is.False, "Infinity speed should be guarded and not grant infinite hearing.");
            Assert.That(resultNaNVisual, Is.True, "NaN turbidity should be guarded and fallback to 0 (clear water).");
        }
    }
}
