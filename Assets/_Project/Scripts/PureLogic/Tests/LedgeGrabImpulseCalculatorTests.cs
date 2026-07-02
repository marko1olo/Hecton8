using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class LedgeGrabImpulseCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, -5f, 10f);
            Vector3 norm = new Vector3(0f, 0f, -1f);
            float pullUp = 5f;
            float cancelFrac = 1f;

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert: Vertical velocity fully cancelled on grab (cancelFrac=1 -> Y = 0 + pullUp)
            // Ledge normal is (0, 0, -1). Inward dot is (10*0 + -5*0 + 10*-1) = -10.
            // Since -10 < 0, we subtract normal * dot => (-10) * (0,0,-1) = (0,0,10).
            // So Z becomes 10 - 10 = 0.
            Assert.That(result.X, Is.EqualTo(10f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(5f).Within(0.001f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, -5f, 10f);
            Vector3 norm = new Vector3(0f, 0f, -1f);
            float pullUp = -5f; // should clamp to 0
            float cancelFrac = 1.5f; // should clamp to 1

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert
            Assert.That(result.X, Is.EqualTo(10f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            Vector3 vel = Vector3.Zero;
            Vector3 norm = Vector3.Zero;
            float pullUp = 0f;
            float cancelFrac = 0f;

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert
            Assert.That(result.X, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            Vector3 vel = new Vector3(-10f, -10f, -10f);
            Vector3 norm = new Vector3(0f, 0f, 1f); // wall is facing +Z, we are moving -Z. dot < 0.
            float pullUp = -10f;
            float cancelFrac = -0.5f;

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert
            Assert.That(result.X, Is.EqualTo(-10f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(-10f).Within(0.001f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.001f)); // inward velocity cancelled
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            Vector3 vel = new Vector3(float.MaxValue, float.MinValue, float.NaN);
            Vector3 norm = new Vector3(1f, 0f, 0f);
            float pullUp = float.MaxValue;
            float cancelFrac = 1f;

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert
            Assert.That(result.X, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_Array_Null_ReturnsZeroPullUp()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, -5f, 10f);
            Vector3 norm = new Vector3(0f, 0f, -1f);
            float[] pullUp = null;
            float cancelFrac = 1f;

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert
            Assert.That(result.X, Is.EqualTo(10f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_Array_Empty_ReturnsZeroPullUp()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, -5f, 10f);
            Vector3 norm = new Vector3(0f, 0f, -1f);
            float[] pullUp = new float[0];
            float cancelFrac = 1f;

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert
            Assert.That(result.X, Is.EqualTo(10f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_Array_WithValues_ReturnsSummedPullUp()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, -5f, 10f);
            Vector3 norm = new Vector3(0f, 0f, -1f);
            float[] pullUp = new float[] { 2f, 3f }; // sum is 5f
            float cancelFrac = 1f;

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert
            Assert.That(result.X, Is.EqualTo(10f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(5f).Within(0.001f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.001f));
        }
}
}
