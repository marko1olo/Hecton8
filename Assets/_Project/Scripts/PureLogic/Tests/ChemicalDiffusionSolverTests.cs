using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ChemicalDiffusionSolverTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float[,] grid = new float[3, 3] {
                { 0f, 0f, 0f },
                { 0f, 10f, 0f },
                { 0f, 0f, 0f }
            };

            float[,] result = ChemicalDiffusionSolver.Solve(grid, 0.5f, 1f, 0.0001f, 4f, 1f);

            Assert.That(result[1, 1], Is.LessThan(10f));
            Assert.That(result[0, 1], Is.GreaterThan(0f));
            Assert.That(result[1, 0], Is.GreaterThan(0f));
            Assert.That(result[2, 1], Is.GreaterThan(0f));
            Assert.That(result[1, 2], Is.GreaterThan(0f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float[,] grid = new float[2, 2] {
                { 10f, 10f },
                { 10f, 10f }
            };

            float[,] result = ChemicalDiffusionSolver.Solve(grid, 1f, 1f, 0.0001f, 4f, 1f);

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    Assert.That(result[x, y], Is.EqualTo(10f).Within(0.001f));
                }
            }
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
             float[,] grid = new float[3, 3] {
                { 0f, 0f, 0f },
                { 0f, 5f, 0f },
                { 0f, 0f, 0f }
            };

            float[,] result = ChemicalDiffusionSolver.Solve(grid, 0f, 0f, 0.0001f, 4f, 1f);

            Assert.That(result[1, 1], Is.EqualTo(5f).Within(0.001f));
            Assert.That(result[0, 1], Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float[,] grid = new float[3, 3] {
                { 0f, 0f, 0f },
                { 0f, 10f, 0f },
                { 0f, 0f, 0f }
            };

            float[,] result = ChemicalDiffusionSolver.Solve(grid, -1f, -1f, 0.0001f, 4f, 1f);

            // clamped to 0 diffusion
            Assert.That(result[1, 1], Is.EqualTo(10f).Within(0.001f));
            Assert.That(result[0, 1], Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
             float[,] grid = new float[3, 3] {
                { 0f, 0f, 0f },
                { 0f, 10f, 0f },
                { 0f, 0f, 0f }
            };

            Assert.Throws<ArgumentException>(() => ChemicalDiffusionSolver.Solve(grid, float.NaN, 1f, 0.0001f, 4f, 1f));
            Assert.Throws<ArgumentException>(() => ChemicalDiffusionSolver.Solve(grid, float.PositiveInfinity, 1f, 0.0001f, 4f, 1f));
            Assert.Throws<ArgumentException>(() => ChemicalDiffusionSolver.Solve(grid, 1f, float.NaN, 0.0001f, 4f, 1f));
            Assert.Throws<ArgumentException>(() => ChemicalDiffusionSolver.Solve(grid, 1f, float.PositiveInfinity, 0.0001f, 4f, 1f));
            Assert.Throws<ArgumentNullException>(() => ChemicalDiffusionSolver.Solve(null, 1f, 1f, 0.0001f, 4f, 1f));

            float[,] nanGrid = new float[3, 3] {
                { 0f, 0f, 0f },
                { 0f, float.NaN, 0f },
                { 0f, 0f, 0f }
            };

            float[,] result = ChemicalDiffusionSolver.Solve(nanGrid, 1f, 1f, 0.0001f, 4f, 1f);
            Assert.That(result[1,1], Is.EqualTo(0f).Within(0.001f));
        }
    }
}
