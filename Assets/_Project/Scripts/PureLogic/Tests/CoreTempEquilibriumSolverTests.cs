using NUnit.Framework;
using System;
using System.Numerics;
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
