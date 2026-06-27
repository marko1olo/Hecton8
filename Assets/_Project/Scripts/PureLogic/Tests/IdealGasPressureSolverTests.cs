using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class IdealGasPressureSolverTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float moles = 100f; // 100 moles
            float tempK = 298.15f; // ~25 C
            float vol = 10f; // 10 m^3
            // P = (nRT)/V = (100 * 8.31446... * 298.15) / 10 = 24789.57

            // Act
            float pressure = IdealGasPressureSolver.Solve(moles, tempK, vol);

            // Assert: Verify expected output behaviour
            float expected = (100f * IdealGasPressureSolver.GasConstant * 298.15f) / 10f;
            Assert.AreEqual(expected, pressure, Tolerance, "Verify standard calculations return expected results.");

            // Double volume: half pressure
            float halfPressure = IdealGasPressureSolver.Solve(moles, tempK, vol * 2f);
            Assert.AreEqual(expected / 2f, halfPressure, Tolerance, "Double volume should halve pressure");

            // Double moles: double pressure
            float doublePressure = IdealGasPressureSolver.Solve(moles * 2f, tempK, vol);
            Assert.AreEqual(expected * 2f, doublePressure, Tolerance, "Double moles should double pressure");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float moles = 0f;
            float tempK = 298.15f;
            float vol = 10f;

            // Act
            float pressure = IdealGasPressureSolver.Solve(moles, tempK, vol);

            // Assert
            Assert.AreEqual(0f, pressure, "Zero moles should result in zero pressure.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero volume)
            float moles = 10f;
            float tempK = 298.15f;
            float vol = 0f;

            // Act
            float pressure = IdealGasPressureSolver.Solve(moles, tempK, vol);

            // Assert
            Assert.AreEqual(0f, pressure, "Verify zero inputs are handled without divide-by-zero or exception.");

            // Very small volume near zero
            float smallVolPressure = IdealGasPressureSolver.Solve(moles, tempK, 1e-7f);
            Assert.AreEqual(0f, smallVolPressure, "Verify very small volume is guarded");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float pressureMolesNeg = IdealGasPressureSolver.Solve(-10f, 298.15f, 10f);
            float pressureTempNeg = IdealGasPressureSolver.Solve(10f, -298.15f, 10f);
            float pressureVolNeg = IdealGasPressureSolver.Solve(10f, 298.15f, -10f);

            // Assert
            Assert.AreEqual(0f, pressureMolesNeg, "Verify negative inputs clamp gracefully or throw.");
            Assert.AreEqual(0f, pressureTempNeg, "Verify negative inputs clamp gracefully or throw.");
            Assert.AreEqual(0f, pressureVolNeg, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float pressureInf = IdealGasPressureSolver.Solve(float.PositiveInfinity, 298.15f, 10f);
            float pressureNaN = IdealGasPressureSolver.Solve(float.NaN, 298.15f, 10f);

            // Large numbers that might overflow
            float pressureLarge = IdealGasPressureSolver.Solve(1e20f, 1e20f, 10f);

            // Assert
            Assert.AreEqual(0f, pressureInf, "Verify robust calculation and overflow protection.");
            Assert.AreEqual(0f, pressureNaN, "Verify robust calculation and overflow protection.");

            // Overflow results in infinity, which we clamp to 0 in our logic (or if it doesn't overflow it's a huge number).
            // Actually float.IsInfinity is checked, so it returns 0.
            Assert.AreEqual(0f, pressureLarge, "Verify overflow protection handles huge results by returning 0.");
        }
    }
}
