using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class WaterSurfaceTransitionDragCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 entryVelocity = new Vector3(0, -10, 0);
            float surfaceDensity = 1.0f;
            float bodyCrossSection = 1.0f;

            // Act
            Vector3 result = WaterSurfaceTransitionDragCalculator.Compute(entryVelocity, surfaceDensity, bodyCrossSection);

            // Assert
            Assert.That(result.X, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(10f).Within(0.001f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            Vector3 entryVelocity = new Vector3(1, 0, 0);
            float surfaceDensity = 0.0001f;
            float bodyCrossSection = 0.0001f;

            // Act
            Vector3 result = WaterSurfaceTransitionDragCalculator.Compute(entryVelocity, surfaceDensity, bodyCrossSection);

            // Assert
            Assert.That(result.LengthSquared(), Is.LessThan(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            Vector3 entryVelocity = Vector3.Zero;
            float surfaceDensity = 0f;
            float bodyCrossSection = 0f;

            // Act
            Vector3 result = WaterSurfaceTransitionDragCalculator.Compute(entryVelocity, surfaceDensity, bodyCrossSection);

            // Assert
            Assert.That(result, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            Vector3 entryVelocity = new Vector3(10, 0, 0);
            float surfaceDensity = -5f;
            float bodyCrossSection = -2f;

            // Act
            Vector3 result = WaterSurfaceTransitionDragCalculator.Compute(entryVelocity, surfaceDensity, bodyCrossSection);

            // Assert
            Assert.That(result, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange & Act
            Vector3 entryVelocity = new Vector3(float.PositiveInfinity, 0, 0);
            Vector3 result = WaterSurfaceTransitionDragCalculator.Compute(entryVelocity, 1.0f, 1.0f);

            Vector3 entryVelocityNaN = new Vector3(float.NaN, 0, 0);
            Vector3 resultNaN = WaterSurfaceTransitionDragCalculator.Compute(entryVelocityNaN, 1.0f, 1.0f);

            // Assert
            Assert.That(result, Is.EqualTo(Vector3.Zero));
            Assert.That(resultNaN, Is.EqualTo(Vector3.Zero));
        }
    }
}
