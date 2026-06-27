using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class CoreTempEquilibriumSolverTests
    {
        private const float DefaultCoolingRate = 0.006f;
        private const float DefaultMinTemp = 20f;
        private const float DefaultMaxTemp = 43f;

        [Test]
        public void Test_HappyPath_Case01()
        {
            // Core == ambient: no change
            float result1 = CoreTempEquilibriumSolver.Solve(37f, 37f, 0f, 10f, DefaultCoolingRate, DefaultMinTemp, DefaultMaxTemp);
            Assert.That(result1, Is.EqualTo(37f).Within(0.001f));

            // Perfect suit: near zero drift
            float result2 = CoreTempEquilibriumSolver.Solve(37f, 2f, 1f, 10f, DefaultCoolingRate, DefaultMinTemp, DefaultMaxTemp);
            Assert.That(result2, Is.EqualTo(37f).Within(0.001f));

            // Exposed to 2C water: rapid drop
            float result3 = CoreTempEquilibriumSolver.Solve(37f, 2f, 0f, 600f, DefaultCoolingRate, DefaultMinTemp, DefaultMaxTemp);
            Assert.That(result3, Is.LessThan(37f));
            Assert.That(result3, Is.GreaterThanOrEqualTo(DefaultMinTemp));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float result1 = CoreTempEquilibriumSolver.Solve(10f, 10f, 0f, 1f, DefaultCoolingRate, DefaultMinTemp, DefaultMaxTemp);
            Assert.That(result1, Is.EqualTo(DefaultMinTemp)); // clamped min

            float result2 = CoreTempEquilibriumSolver.Solve(50f, 50f, 0f, 1f, DefaultCoolingRate, DefaultMinTemp, DefaultMaxTemp);
            Assert.That(result2, Is.EqualTo(DefaultMaxTemp)); // clamped max
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = CoreTempEquilibriumSolver.Solve(0f, 0f, 0f, 0f, 0f, DefaultMinTemp, DefaultMaxTemp);
            Assert.That(result, Is.EqualTo(DefaultMinTemp));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = CoreTempEquilibriumSolver.Solve(37f, 37f, -1f, -10f, -0.006f, DefaultMinTemp, DefaultMaxTemp);
            Assert.That(result, Is.EqualTo(37f).Within(0.001f)); // suit clamped to 0, dt clamped to 0 -> no change
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float result1 = CoreTempEquilibriumSolver.Solve(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, DefaultMinTemp, DefaultMaxTemp);
            Assert.That(result1, Is.EqualTo(37f).Within(0.001f)); // safe dt fallback 0 -> no change

            float result2 = CoreTempEquilibriumSolver.Solve(37f, float.PositiveInfinity, 0f, 10f, DefaultCoolingRate, DefaultMinTemp, DefaultMaxTemp);
            Assert.That(result2, Is.LessThan(37f)); // safe ambient fallback 4f -> temp drops
        }
    }
}
