using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class CoreTempEquilibriumSolverTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float initialCore = 37f;
            float ambient = 2f;
            float suitRes = 0f;
            float dt = 0.2f;

            // Act
            float result = CoreTempEquilibriumSolver.Solve(initialCore, ambient, suitRes, dt);

            // Assert
            Assert.That(result, Is.LessThan(initialCore));
            Assert.That(result, Is.GreaterThan(ambient));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float minClampCore = 10f;
            float ambient = 10f;
            float suitRes = 0f;
            float dt = 0.1f;

            // Act
            float result1 = CoreTempEquilibriumSolver.Solve(minClampCore, ambient, suitRes, dt);
            float result2 = CoreTempEquilibriumSolver.Solve(50f, 50f, 0f, 0.1f);

            // Assert
            Assert.That(result1, Is.EqualTo(20f)); // MinCoreTemp
            Assert.That(result2, Is.EqualTo(43f)); // MaxCoreTemp
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float initialCore = 37f;

            // Act
            float result = CoreTempEquilibriumSolver.Solve(initialCore, 0f, 0f, 0f);

            // Assert
            Assert.That(result, Is.EqualTo(37f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float initialCore = 37f;
            float dt = -5f;

            // Act
            float result = CoreTempEquilibriumSolver.Solve(initialCore, -5f, -1f, dt);

            // Assert
            Assert.That(result, Is.EqualTo(37f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float initialCore = float.NaN;

            // Act
            float result1 = CoreTempEquilibriumSolver.Solve(initialCore, 20f, 0f, 0.1f);
            float result2 = CoreTempEquilibriumSolver.Solve(37f, float.NaN, 0f, 0.1f);
            float result3 = CoreTempEquilibriumSolver.Solve(37f, 20f, float.PositiveInfinity, 0.1f);

            // Assert
            // initialCore is NaN -> falls back to 37. Ambient is 20, cooling applies -> < 37
            Assert.That(result1, Is.LessThan(37f));

            // ambient is NaN -> falls back to 4. initial core 37, cooling applies -> < 37
            Assert.That(result2, Is.LessThan(37f));

            // suit is Infinity -> falls back to 0. core 37, ambient 20, cooling applies -> < 37
            Assert.That(result3, Is.LessThan(37f));
        }
    }
}
