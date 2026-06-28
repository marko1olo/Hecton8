using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class StormWaveHeightBeaufortCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float beaufortNumber = 7f;
            float fetchDistanceKm = 100f;
            float windDurationHours = 12f;

            // Act
            float result = StormWaveHeightBeaufortCalculator.Compute(beaufortNumber, fetchDistanceKm, windDurationHours);

            // Assert
            Assert.IsTrue(result > 0f);
            Assert.IsTrue(result < 10f); // Roughly 4-5m for Beaufort 7
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float minBeaufort = 0f;
            float maxBeaufort = 12f;
            float minFetch = 0f;
            float maxFetch = 1000f;
            float minDuration = 0f;
            float maxDuration = 48f;

            // Act
            float minResult = StormWaveHeightBeaufortCalculator.Compute(minBeaufort, minFetch, minDuration);
            float maxResult = StormWaveHeightBeaufortCalculator.Compute(maxBeaufort, maxFetch, maxDuration);

            // Assert
            Assert.AreEqual(0f, minResult);
            Assert.IsTrue(maxResult > 10f); // High waves for hurricane
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float beaufortNumber = 0f;
            float fetchDistanceKm = 0f;
            float windDurationHours = 0f;

            // Act
            float result = StormWaveHeightBeaufortCalculator.Compute(beaufortNumber, fetchDistanceKm, windDurationHours);

            // Assert
            Assert.AreEqual(0f, result);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float beaufortNumber = -5f;
            float fetchDistanceKm = -100f;
            float windDurationHours = -10f;

            // Act
            float result = StormWaveHeightBeaufortCalculator.Compute(beaufortNumber, fetchDistanceKm, windDurationHours);

            // Assert
            Assert.AreEqual(0f, result);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float beaufortNumber = float.MaxValue;
            float fetchDistanceKm = float.MaxValue;
            float windDurationHours = float.MaxValue;

            // Act
            float result = StormWaveHeightBeaufortCalculator.Compute(beaufortNumber, fetchDistanceKm, windDurationHours);

            // Assert
            // It clamps beaufort to 12. fetch/duration Exp functions handle large values without crashing
            Assert.IsTrue(!float.IsNaN(result));
            Assert.IsTrue(!float.IsInfinity(result));
        }

        [Test]
        public void Test_ExtremeInputs_Case06_NaN_Infinity()
        {
            // Arrange
            float beaufortNumber = float.NaN;
            float fetchDistanceKm = float.PositiveInfinity;
            float windDurationHours = float.NegativeInfinity;

            // Act
            float result = StormWaveHeightBeaufortCalculator.Compute(beaufortNumber, fetchDistanceKm, windDurationHours);

            // Assert
            Assert.AreEqual(0f, result);
        }
    }
}
