using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class CoreTempEquilibriumSolverTests
    {
        private const float DefaultCoolingRate = 0.006f;
        private const float DefaultMinTemp = 20f;
        private const float DefaultMaxTemp = 42f;

        [Test]
        public void Test_Normal_Case01()
        {
            float result = CoreTempEquilibriumSolver.Solve(37f, 20f, 0.5f, 10f, DefaultCoolingRate, DefaultMinTemp, DefaultMaxTemp);
            Assert.That(result, Is.GreaterThan(20f));
            Assert.That(result, Is.LessThan(37f));
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

        /// <summary>
        /// Pins the solver to Newton's law of cooling: core' = core + (ambient - core) * (1 - exp(-k*dt)).
        /// Regression guard for an incomplete Pade range reduction that produced
        /// 1 - exp(-k*dt/4) instead of 1 - exp(-k*dt), understating cooling by ~4x
        /// for small k*dt (0.52 C to 4.19 C absolute error on these vectors).
        /// Bounds are chosen so no result reaches the min/max clamp.
        /// </summary>
        [Test]
        public void Test_MatchesNewtonCoolingLaw_Case06()
        {
            const float core = 37f;
            const float ambient = 25f;
            float[] coolingRates = { 0.006f, 0.06f, 0.6f, 1f, 2f, 4f };

            for (int i = 0; i < coolingRates.Length; i++)
            {
                float kdt = coolingRates[i];
                float expected = core + ((ambient - core) * (1f - (float)Math.Exp(-kdt)));
                float actual = CoreTempEquilibriumSolver.Solve(
                    core, ambient, 0f, 1f, kdt, DefaultMinTemp, DefaultMaxTemp);

                Assert.That(actual, Is.EqualTo(expected).Within(0.01f), "k*dt=" + kdt);
                Assert.That(actual, Is.GreaterThan(DefaultMinTemp), "clamp must not mask the curve");
            }
        }

        /// <summary>
        /// Suit thermal resistance must scale cooling linearly: full resistance freezes
        /// core temperature, half resistance halves the approach toward ambient.
        /// </summary>
        [Test]
        public void Test_SuitResistanceScalesCooling_Case07()
        {
            const float core = 37f;
            const float ambient = 25f;
            const float kdt = 0.5f;

            float unprotected = CoreTempEquilibriumSolver.Solve(
                core, ambient, 0f, 1f, kdt, DefaultMinTemp, DefaultMaxTemp);
            float halfProtected = CoreTempEquilibriumSolver.Solve(
                core, ambient, 0.5f, 1f, kdt, DefaultMinTemp, DefaultMaxTemp);
            float fullyProtected = CoreTempEquilibriumSolver.Solve(
                core, ambient, 1f, 1f, kdt, DefaultMinTemp, DefaultMaxTemp);

            Assert.That(fullyProtected, Is.EqualTo(core).Within(0.0001f));
            float unprotectedDrop = core - unprotected;
            float halfDrop = core - halfProtected;
            Assert.That(halfDrop, Is.EqualTo(unprotectedDrop * 0.5f).Within(0.0001f));
        }

        /// <summary>
        /// Cooling must be monotonic in elapsed time and never overshoot ambient,
        /// which is what a mis-recomposed exponent can do once the exponent grows.
        /// </summary>
        [Test]
        public void Test_MonotonicAndNoOvershoot_Case08()
        {
            const float core = 37f;
            const float ambient = 25f;
            float previous = core;

            for (int step = 1; step <= 40; step++)
            {
                float actual = CoreTempEquilibriumSolver.Solve(
                    core, ambient, 0f, step * 0.25f, DefaultCoolingRate, DefaultMinTemp, DefaultMaxTemp);

                Assert.That(actual, Is.LessThanOrEqualTo(previous + 0.0001f), "must not warm up, step " + step);
                Assert.That(actual, Is.GreaterThanOrEqualTo(ambient - 0.0001f), "must not overshoot ambient, step " + step);
                previous = actual;
            }
        }
    }
}
