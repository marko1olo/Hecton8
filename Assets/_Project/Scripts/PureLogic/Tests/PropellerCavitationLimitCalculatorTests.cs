using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PropellerCavitationLimitCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs (Low RPM, full efficiency)
            float propRPM = 100f;
            float depthMeters = 50f;
            float waterTemp = 10f;
            float propDiameter = 2f;

            // Act
            float efficiency = PropellerCavitationLimitCalculator.Compute(propRPM, depthMeters, waterTemp, propDiameter);

            // Assert
            Assert.AreEqual(1.0f, efficiency, 0.01f, "Low RPM should yield full efficiency without cavitation.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (High RPM, shallow depth)
            float propRPM = 5000f; // Extremely high RPM
            float depthMeters = 5f; // Shallow depth
            float waterTemp = 15f;
            float propDiameter = 2f;

            // Act
            float efficiency = PropellerCavitationLimitCalculator.Compute(propRPM, depthMeters, waterTemp, propDiameter);

            // Assert
            Assert.Less(efficiency, 1.0f, "High RPM at shallow depth should cause cavitation and drop efficiency.");
            Assert.GreaterOrEqual(efficiency, 0.1f, "Efficiency should not drop below the minimum clamp.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float propRPM = 0f;
            float depthMeters = 0f;
            float waterTemp = 0f;
            float propDiameter = 0f;

            // Act
            float efficiency = PropellerCavitationLimitCalculator.Compute(propRPM, depthMeters, waterTemp, propDiameter);

            // Assert
            Assert.AreEqual(1.0f, efficiency, 0.01f, "Zero RPM should yield full efficiency.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float propRPM = -100f;
            float depthMeters = -50f;
            float waterTemp = -10f;
            float propDiameter = -2f;

            // Act
            float efficiency = PropellerCavitationLimitCalculator.Compute(propRPM, depthMeters, waterTemp, propDiameter);

            // Assert
            Assert.AreEqual(1.0f, efficiency, 0.01f, "Negative RPM should be clamped/handled and return full efficiency.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float propRPM = float.MaxValue;
            float depthMeters = float.MaxValue;
            float waterTemp = float.MaxValue;
            float propDiameter = float.MaxValue;

            // Act
            float efficiency = PropellerCavitationLimitCalculator.Compute(propRPM, depthMeters, waterTemp, propDiameter);

            // Assert
            Assert.IsTrue(float.IsFinite(efficiency), "Efficiency calculation must not overflow or result in NaN/Infinity.");
            Assert.GreaterOrEqual(efficiency, 0.1f, "Extreme inputs should be safely clamped to minimum efficiency.");
            Assert.LessOrEqual(efficiency, 1.0f, "Extreme inputs should be safely clamped to maximum efficiency.");
        }
    }
}
