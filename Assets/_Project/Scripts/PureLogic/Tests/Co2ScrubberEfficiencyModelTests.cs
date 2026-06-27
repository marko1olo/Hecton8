using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class Co2ScrubberEfficiencyModelTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float usageHours = 10f;
            float ambientTempCelsius = 20f;
            float maxEfficiency = 1.0f;
            float degradationRate = 0.05f;

            // Act
            float result = Co2ScrubberEfficiencyModel.Evaluate(usageHours, ambientTempCelsius, maxEfficiency, degradationRate);

            // Assert
            Assert.That(result, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float usageHours = 100f; // Excessive usage
            float ambientTempCelsius = 20f;
            float maxEfficiency = 1.0f;
            float degradationRate = 0.05f;

            // Act
            float result = Co2ScrubberEfficiencyModel.Evaluate(usageHours, ambientTempCelsius, maxEfficiency, degradationRate);

            // Assert
            Assert.That(result, Is.EqualTo(0f), "Efficiency should not drop below 0.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float usageHours = 0f;
            float ambientTempCelsius = 0f;
            float maxEfficiency = 0f;
            float degradationRate = 0f;

            // Act
            float result = Co2ScrubberEfficiencyModel.Evaluate(usageHours, ambientTempCelsius, maxEfficiency, degradationRate);

            // Assert
            Assert.That(result, Is.EqualTo(0f), "Should handle zeros gracefully.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float usageHours = -5f;
            float ambientTempCelsius = -10f; // Temp scale = 1.0 since it's < 20C
            float maxEfficiency = -1.0f;
            float degradationRate = -0.5f;

            // Act
            float result = Co2ScrubberEfficiencyModel.Evaluate(usageHours, ambientTempCelsius, maxEfficiency, degradationRate);

            // Assert
            Assert.That(result, Is.EqualTo(0f), "Negative inputs clamped to valid ranges.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float usageHours = float.NaN;
            float ambientTempCelsius = float.PositiveInfinity;
            float maxEfficiency = float.NaN;
            float degradationRate = float.NaN;

            // Act
            float result = Co2ScrubberEfficiencyModel.Evaluate(usageHours, ambientTempCelsius, maxEfficiency, degradationRate);

            // Assert
            Assert.That(float.IsNaN(result), Is.False, "NaN inputs should be guarded against.");
            Assert.That(float.IsInfinity(result), Is.False, "Infinity inputs should be guarded against.");
        }

        [Test]
        public void Test_TemperatureAcceleration_Case06()
        {
            // Arrange
            float usageHours = 10f;
            float maxEfficiency = 1.0f;
            float degradationRate = 0.05f;

            // Act
            float coldResult = Co2ScrubberEfficiencyModel.Evaluate(usageHours, 20f, maxEfficiency, degradationRate); // 1.0 tempScale
            float hotResult = Co2ScrubberEfficiencyModel.Evaluate(usageHours, 40f, maxEfficiency, degradationRate); // 2.0 tempScale

            // Assert
            Assert.That(hotResult, Is.LessThan(coldResult), "High temperatures should accelerate decay.");
        }
    }
}
