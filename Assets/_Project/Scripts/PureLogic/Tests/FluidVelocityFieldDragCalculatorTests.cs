using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FluidVelocityFieldDragCalculatorTests
    {
        private const float Tolerance = 1e-4f;
        private const float Epsilon = 1e-6f;

        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector3 hullVelocity = new Vector3(10f, 0f, 0f);
            Vector3 currentVelocity = new Vector3(5f, 0f, 0f);
            float dragCoefficient = 1.2f;
            float frontalArea = 2.0f;

            // Act
            Vector3 drag = FluidVelocityFieldDragCalculator.Compute(hullVelocity, currentVelocity, dragCoefficient, frontalArea, Epsilon);

            // Assert: Verify expected output behaviour
            // relative = 10 - 5 = 5
            // direction = (1, 0, 0)
            // speedSquared = 25
            // force = -1 * (1, 0, 0) * 25 * 1.2 * 2.0 = (-60, 0, 0)
            Assert.AreEqual(-60.0f, drag.X, Tolerance);
            Assert.AreEqual(0f, drag.Y, Tolerance);
            Assert.AreEqual(0f, drag.Z, Tolerance);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Hull matching current velocity exactly must experience 0 drag.
            Vector3 hullVelocity = new Vector3(5f, -2f, 10f);
            Vector3 currentVelocity = new Vector3(5f, -2f, 10f);
            float dragCoefficient = 1.5f;
            float frontalArea = 3.0f;

            // Act
            Vector3 drag = FluidVelocityFieldDragCalculator.Compute(hullVelocity, currentVelocity, dragCoefficient, frontalArea, Epsilon);

            // Assert
            Assert.AreEqual(0f, drag.X, Tolerance);
            Assert.AreEqual(0f, drag.Y, Tolerance);
            Assert.AreEqual(0f, drag.Z, Tolerance);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            Vector3 hullVelocity = Vector3.Zero;
            Vector3 currentVelocity = Vector3.Zero;
            float dragCoefficient = 0f;
            float frontalArea = 0f;

            // Act
            Vector3 drag = FluidVelocityFieldDragCalculator.Compute(hullVelocity, currentVelocity, dragCoefficient, frontalArea, Epsilon);

            // Assert
            Assert.AreEqual(0f, drag.X, Tolerance);
            Assert.AreEqual(0f, drag.Y, Tolerance);
            Assert.AreEqual(0f, drag.Z, Tolerance);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 hullVelocity = new Vector3(10f, 0f, 0f);
            Vector3 currentVelocity = new Vector3(0f, 0f, 0f);
            float dragCoefficient = -1.5f; // Should be clamped to 0
            float frontalArea = -2.0f; // Should be clamped to 0

            // Act
            Vector3 drag = FluidVelocityFieldDragCalculator.Compute(hullVelocity, currentVelocity, dragCoefficient, frontalArea, Epsilon);

            // Assert
            Assert.AreEqual(0f, drag.X, Tolerance);
            Assert.AreEqual(0f, drag.Y, Tolerance);
            Assert.AreEqual(0f, drag.Z, Tolerance);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 hullVelocity = new Vector3(float.PositiveInfinity, float.NaN, 0f);
            Vector3 currentVelocity = new Vector3(0f, float.NegativeInfinity, 0f);
            float dragCoefficient = float.NaN;
            float frontalArea = float.PositiveInfinity;

            // Act
            Vector3 drag = FluidVelocityFieldDragCalculator.Compute(hullVelocity, currentVelocity, dragCoefficient, frontalArea, Epsilon);

            // Assert
            // Infinity/NaN values should be clamped or return Zero
            Assert.AreEqual(0f, drag.X, Tolerance);
            Assert.AreEqual(0f, drag.Y, Tolerance);
            Assert.AreEqual(0f, drag.Z, Tolerance);

            // Large standard arithmetic check
            hullVelocity = new Vector3(1000f, 0f, 0f);
            currentVelocity = new Vector3(-1000f, 0f, 0f); // Opposing current = 2000 relative
            dragCoefficient = 10f;
            frontalArea = 100f;
            Vector3 extremeDrag = FluidVelocityFieldDragCalculator.Compute(hullVelocity, currentVelocity, dragCoefficient, frontalArea, Epsilon);

            // relative = 2000
            // speedSquared = 4,000,000
            // drag = -(4,000,000) * 10 * 100 = -4,000,000,000
            Assert.IsTrue(extremeDrag.X < -1000000.0f);
            Assert.AreEqual(0f, extremeDrag.Y, Tolerance);
            Assert.AreEqual(0f, extremeDrag.Z, Tolerance);
        }
    }
}
