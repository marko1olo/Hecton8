using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class RadiationDoseAccumulatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float currentDose = 10f;
            float exposureRate = 7200f; // 2 Sv per sec -> 7200 Sv per hour
            float recoveryRate = 0f;
            float dt = 1f;

            // Act
            float newDose = RadiationDoseAccumulator.Calculate(currentDose, exposureRate, recoveryRate, dt);

            // Assert
            Assert.AreEqual(12f, newDose, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float currentDose = 5f;
            float exposureRate = 0f;
            float recoveryRate = 3600f; // -1 Sv per sec
            float dt = 10f;

            // Act
            float newDose = RadiationDoseAccumulator.Calculate(currentDose, exposureRate, recoveryRate, dt);

            // Assert
            Assert.AreEqual(0f, newDose, "Dose should be clamped to 0.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float currentDose = 0f;
            float exposureRate = 0f;
            float recoveryRate = 0f;
            float dt = 0f;

            // Act
            float newDose = RadiationDoseAccumulator.Calculate(currentDose, exposureRate, recoveryRate, dt);

            // Assert
            Assert.AreEqual(0f, newDose, "All zeros should result in 0.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float currentDose = -10f;
            float exposureRate = -5f;
            float recoveryRate = -2f;
            float dt = -1f;

            // Act
            float newDose = RadiationDoseAccumulator.Calculate(currentDose, exposureRate, recoveryRate, dt);

            // Assert
            Assert.AreEqual(0f, newDose, "Negative inputs should be handled safely and clamped.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float currentDose = 1f;
            float exposureRate = float.PositiveInfinity;
            float recoveryRate = float.NaN;
            float dt = 1f;

            // Act
            float newDose = RadiationDoseAccumulator.Calculate(currentDose, exposureRate, recoveryRate, dt);

            // Assert
            Assert.AreEqual(1f, newDose, "Extreme or invalid inputs should default to 0 for accumulation.");
        }

        [Test]
        public void Test_EqualRates_Stable()
        {
            // Arrange
            float currentDose = 10f;
            float exposureRate = 3600f;
            float recoveryRate = 3600f;
            float dt = 5f;

            // Act
            float newDose = RadiationDoseAccumulator.Calculate(currentDose, exposureRate, recoveryRate, dt);

            // Assert
            Assert.AreEqual(10f, newDose, 0.001f, "Equal rates should result in stable dose.");
        }
    }
}
