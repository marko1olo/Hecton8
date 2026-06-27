using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class UnderwaterFogDensityCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            // Act
            float densityOcean = UnderwaterFogDensityCalculator.Compute("open ocean", 10f, 0.05f, 1.0f);
            float densityKelp = UnderwaterFogDensityCalculator.Compute("kelp forest", 10f, 0.05f, 1.0f);
            float densityBrine = UnderwaterFogDensityCalculator.Compute("brine pool", 10f, 0.05f, 1.0f);

            // Assert: Verify expected output behaviour
            Assert.IsTrue(densityOcean > 0f);
            Assert.IsTrue(densityKelp > densityOcean);
            Assert.IsTrue(densityBrine > densityKelp);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Act
            float density = UnderwaterFogDensityCalculator.Compute("brine pool", 1000f, 10f, 10f);

            // Assert
            Assert.IsTrue(density > 0f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float density = UnderwaterFogDensityCalculator.Compute("open ocean", 0f, 0f, 0f);

            // Assert
            Assert.AreEqual(0f, density, 0.0001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float density = UnderwaterFogDensityCalculator.Compute("open ocean", -10f, -0.05f, -1.0f);

            // Assert
            Assert.AreEqual(0f, density, 0.0001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float densityNaN = UnderwaterFogDensityCalculator.Compute("open ocean", float.NaN, float.NaN, float.NaN);
            float densityInf = UnderwaterFogDensityCalculator.Compute("open ocean", float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

            // Assert
            Assert.AreEqual(0f, densityNaN, 0.0001f);
            Assert.AreEqual(0f, densityInf, 0.0001f);
        }
    }
}
