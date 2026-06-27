using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class AtmosphericRoomGasDiffusionCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float roomAO2 = 100f;
            float roomBO2 = 50f;
            float roomACO2 = 10f;
            float roomBCO2 = 2f;
            float doorAreaM2 = 2f;
            float deltaTime = 1f;
            float conductance = 0.045f;
            float maxRatio = 0.5f;

            // Act
            Vector2 result = AtmosphericRoomGasDiffusionCalculator.Compute(roomAO2, roomBO2, roomACO2, roomBCO2, doorAreaM2, deltaTime, conductance, maxRatio);

            // Assert: Verify expected output behaviour
            // Exchange = (100 - 50) * 2 * 1 * 0.045 = 50 * 0.09 = 4.5
            Assert.That(result.X, Is.EqualTo(4.5f).Within(0.001f));
            // Exchange = (10 - 2) * 2 * 1 * 0.045 = 8 * 0.09 = 0.72
            Assert.That(result.Y, Is.EqualTo(0.72f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Test that extremely high delta time or door area doesn't over-transfer.
            // Half difference limit should clamp it.
            float roomAO2 = 100f;
            float roomBO2 = 0f;
            float roomACO2 = 100f;
            float roomBCO2 = 0f;
            float doorAreaM2 = 1000f;
            float deltaTime = 1000f;
            float conductance = 0.045f;
            float maxRatio = 0.5f;

            // Act
            Vector2 result = AtmosphericRoomGasDiffusionCalculator.Compute(roomAO2, roomBO2, roomACO2, roomBCO2, doorAreaM2, deltaTime, conductance, maxRatio);

            // Assert
            // Diff is 100. Max transfer is 50.
            Assert.That(result.X, Is.EqualTo(50f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            Vector2 result1 = AtmosphericRoomGasDiffusionCalculator.Compute(100f, 50f, 10f, 2f, 0f, 1f, 0.045f, 0.5f);
            Vector2 result2 = AtmosphericRoomGasDiffusionCalculator.Compute(100f, 50f, 10f, 2f, 2f, 0f, 0.045f, 0.5f);
            Vector2 result3 = AtmosphericRoomGasDiffusionCalculator.Compute(50f, 50f, 10f, 10f, 2f, 1f, 0.045f, 0.5f);
            Vector2 result4 = AtmosphericRoomGasDiffusionCalculator.Compute(100f, 50f, 10f, 2f, 2f, 1f, 0f, 0.5f);
            Vector2 result5 = AtmosphericRoomGasDiffusionCalculator.Compute(100f, 50f, 10f, 2f, 2f, 1f, 0.045f, 0f);

            // Assert
            Assert.That(result1.X, Is.EqualTo(0f));
            Assert.That(result1.Y, Is.EqualTo(0f));

            Assert.That(result2.X, Is.EqualTo(0f));
            Assert.That(result2.Y, Is.EqualTo(0f));

            Assert.That(result3.X, Is.EqualTo(0f));
            Assert.That(result3.Y, Is.EqualTo(0f));

            Assert.That(result4.X, Is.EqualTo(0f));
            Assert.That(result4.Y, Is.EqualTo(0f));

            Assert.That(result5.X, Is.EqualTo(0f));
            Assert.That(result5.Y, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Negative door area or delta time should result in 0 transfer.
            // Act
            Vector2 result1 = AtmosphericRoomGasDiffusionCalculator.Compute(100f, 50f, 10f, 2f, -5f, 1f, 0.045f, 0.5f);
            Vector2 result2 = AtmosphericRoomGasDiffusionCalculator.Compute(100f, 50f, 10f, 2f, 2f, -1f, 0.045f, 0.5f);

            // Negative gas concentration should be clamped to 0
            Vector2 result3 = AtmosphericRoomGasDiffusionCalculator.Compute(-100f, 50f, -10f, 2f, 2f, 1f, 0.045f, 0.5f);

            // Negative constants should be clamped to 0
            Vector2 result4 = AtmosphericRoomGasDiffusionCalculator.Compute(100f, 50f, 10f, 2f, 2f, 1f, -0.045f, -0.5f);

            // Assert
            Assert.That(result1.X, Is.EqualTo(0f));
            Assert.That(result1.Y, Is.EqualTo(0f));

            Assert.That(result2.X, Is.EqualTo(0f));
            Assert.That(result2.Y, Is.EqualTo(0f));

            // For result3: roomAO2 becomes 0, roomBO2 is 50. diff is -50. Max transfer is 25.
            // Transfer = (0 - 50) * 2 * 1 * 0.045 = -4.5
            Assert.That(result3.X, Is.EqualTo(-4.5f).Within(0.001f));
            // CO2: roomA becomes 0, roomB is 2. Diff is -2. Max transfer is 1.
            // Transfer = (0 - 2) * 2 * 1 * 0.045 = -0.18
            Assert.That(result3.Y, Is.EqualTo(-0.18f).Within(0.001f));

            Assert.That(result4.X, Is.EqualTo(0f));
            Assert.That(result4.Y, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            Vector2 result1 = AtmosphericRoomGasDiffusionCalculator.Compute(float.PositiveInfinity, 50f, 10f, 2f, 2f, 1f, 0.045f, 0.5f);
            Vector2 result2 = AtmosphericRoomGasDiffusionCalculator.Compute(100f, 50f, float.NaN, 2f, 2f, 1f, 0.045f, 0.5f);
            Vector2 result3 = AtmosphericRoomGasDiffusionCalculator.Compute(100f, 50f, 10f, 2f, 2f, 1f, float.PositiveInfinity, 0.5f);

            // Assert
            // Infinity/NaN clamped to 0
            // result1 O2: (0 - 50) * 0.09 = -4.5
            Assert.That(result1.X, Is.EqualTo(-4.5f).Within(0.001f));

            // result2 CO2: (0 - 2) * 0.09 = -0.18
            Assert.That(result2.Y, Is.EqualTo(-0.18f).Within(0.001f));

            // result3 conductance is Infinity, clamps to 0
            Assert.That(result3.X, Is.EqualTo(0f));
        }
    }
}
