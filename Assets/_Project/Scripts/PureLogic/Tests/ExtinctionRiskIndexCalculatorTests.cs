using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ExtinctionRiskIndexCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            // Act: Pop above viable, good habitat: low risk.
            float risk = ExtinctionRiskIndexCalculator.Compute(100f, 50f, 1.0f, 0.0f);
            // Assert: Verify expected output behaviour
            Assert.That(risk, Is.EqualTo(0.0f).Within(0.0001f));

            // Pop below viable: high risk.
            float risk2 = ExtinctionRiskIndexCalculator.Compute(25f, 50f, 1.0f, 0.0f);
            Assert.That(risk2, Is.EqualTo(0.5f).Within(0.0001f));

            // Predation alone can elevate.
            float risk3 = ExtinctionRiskIndexCalculator.Compute(100f, 50f, 1.0f, 0.5f);
            Assert.That(risk3, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Act
            float risk = ExtinctionRiskIndexCalculator.Compute(50f, 50f, -0.5f, 1.5f);
            // Assert
            Assert.That(risk, Is.EqualTo(1.0f).Within(0.0001f));

            float risk2 = ExtinctionRiskIndexCalculator.Compute(50f, 50f, 1.5f, -0.5f);
            Assert.That(risk2, Is.EqualTo(0.0f).Within(0.0001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float risk = ExtinctionRiskIndexCalculator.Compute(0f, 0f, 0f, 0f);
            // Assert
            Assert.That(risk, Is.EqualTo(1.0f).Within(0.0001f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float risk = ExtinctionRiskIndexCalculator.Compute(-10f, -50f, -1.0f, -0.5f);
            // Assert
            Assert.That(risk, Is.EqualTo(1.0f).Within(0.0001f));

            float risk2 = ExtinctionRiskIndexCalculator.Compute(-10f, 50f, 1.0f, 0.0f);
            Assert.That(risk2, Is.EqualTo(1.0f).Within(0.0001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float risk1 = ExtinctionRiskIndexCalculator.Compute(float.PositiveInfinity, 50f, 1f, 0f);
            // Assert
            Assert.That(risk1, Is.EqualTo(0f).Within(0.0001f));

            float riskNaN = ExtinctionRiskIndexCalculator.Compute(float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.That(riskNaN, Is.EqualTo(1.0f).Within(0.0001f));

            float riskInf = ExtinctionRiskIndexCalculator.Compute(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Assert.That(riskInf, Is.EqualTo(1.0f).Within(0.0001f));
        }
    }
}
