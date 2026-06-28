using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class HarpoonTensionForceCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float currentLength = 10f;
            float restLength = 5f;
            float stiffness = 100f;
            float dampingCoeff = 10f;
            float extensionVelocity = 2f;

            // Act
            float result = HarpoonTensionForceCalculator.Compute(currentLength, restLength, stiffness, dampingCoeff, extensionVelocity);

            // Assert: Verify expected output behaviour
            // stretch = 10 - 5 = 5
            // force = (5 * 100) + (2 * 10) = 500 + 20 = 520
            Assert.AreEqual(520f, result, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float currentLength = 5f;
            float restLength = 5f; // exactly at rest length
            float stiffness = 100f;
            float dampingCoeff = 10f;
            float extensionVelocity = 2f;

            // Act
            float result1 = HarpoonTensionForceCalculator.Compute(currentLength, restLength, stiffness, dampingCoeff, extensionVelocity);
            float result2 = HarpoonTensionForceCalculator.Compute(4f, 5f, stiffness, dampingCoeff, extensionVelocity); // below rest length

            // Assert
            Assert.AreEqual(0f, result1, "Tension should be zero at exactly rest length.");
            Assert.AreEqual(0f, result2, "Tension should be zero when current length is below rest length.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float currentLength = 0f;
            float restLength = 0f;
            float stiffness = 0f;
            float dampingCoeff = 0f;
            float extensionVelocity = 0f;

            // Act
            float result = HarpoonTensionForceCalculator.Compute(currentLength, restLength, stiffness, dampingCoeff, extensionVelocity);

            // Assert
            Assert.AreEqual(0f, result, "Tension should handle all zeros safely.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float currentLength = -10f; // negative current length
            float restLength = -5f; // negative rest length
            float stiffness = -100f; // negative stiffness
            float dampingCoeff = -10f; // negative damping
            float extensionVelocity = -2f; // negative velocity

            // Act
            float result = HarpoonTensionForceCalculator.Compute(currentLength, restLength, stiffness, dampingCoeff, extensionVelocity);

            // Assert
            Assert.AreEqual(0f, result, "Tension should clamp negative values gracefully.");

            // Additional test for negative velocity reducing total force but not below 0
            float result2 = HarpoonTensionForceCalculator.Compute(10f, 5f, 10f, 10f, -100f);
            Assert.AreEqual(0f, result2, "Total force should not go below 0 due to negative velocity and damping.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float resultNaN = HarpoonTensionForceCalculator.Compute(float.NaN, 5f, 100f, 10f, 2f);
            float resultInfinity = HarpoonTensionForceCalculator.Compute(10f, float.PositiveInfinity, 100f, 10f, 2f);
            float resultMax = HarpoonTensionForceCalculator.Compute(float.MaxValue, 0f, 1f, 0f, 0f);

            // Act & Assert
            Assert.AreEqual(0f, resultNaN, "Should return 0 on NaN input.");
            Assert.AreEqual(0f, resultInfinity, "Should return 0 on Infinity input.");
            // We just ensure it doesn't crash on MaxValue, it might be Infinity depending on float precision
            Assert.IsTrue(!float.IsNaN(resultMax), "Large values should compute robustly.");
        }
    }
}
