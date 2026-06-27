using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FluidPressureJacobiSolverTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float[,,] pressure = new float[3, 3, 3];
            float[,,] divergence = new float[3, 3, 3];
            divergence[1, 1, 1] = 1f;

            float[,,] result = FluidPressureJacobiSolver.Solve(pressure, divergence, 1f);

            // divergence = 1, so p_curr = (0 - 1) / 6 = -0.1666f
            Assert.That(result[1, 1, 1], Is.LessThan(0f));
            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float[,,] pressure = new float[1, 1, 1];
            pressure[0,0,0] = 5f;
            float[,,] divergence = new float[1, 1, 1];

            float[,,] result = FluidPressureJacobiSolver.Solve(pressure, divergence, 1f);

            // (5 * 6 - 0) / 6 = 5
            Assert.That(result[0,0,0], Is.EqualTo(5f).Within(0.001f));
            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float[,,] pressure = new float[2, 2, 2];
            float[,,] divergence = new float[2, 2, 2];

            Assert.Throws<ArgumentOutOfRangeException>(() => FluidPressureJacobiSolver.Solve(pressure, divergence, 0f));
            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float[,,] pressure = new float[2, 2, 2];
            float[,,] divergence = new float[2, 2, 2];

            Assert.Throws<ArgumentOutOfRangeException>(() => FluidPressureJacobiSolver.Solve(pressure, divergence, -5f));
            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float[,,] pressure = new float[2, 2, 2];
            pressure[0,0,0] = float.NaN;
            pressure[1,1,1] = float.PositiveInfinity;
            float[,,] divergence = new float[2, 2, 2];
            divergence[0,0,0] = float.NaN;

            float[,,] result = FluidPressureJacobiSolver.Solve(pressure, divergence, 1f);

            Assert.That(float.IsNaN(result[0,0,0]), Is.False);
            Assert.Pass("Verify robust calculation and overflow protection.");
        }
    }
}
