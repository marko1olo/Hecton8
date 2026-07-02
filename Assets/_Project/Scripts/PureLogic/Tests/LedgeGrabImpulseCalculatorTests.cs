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
        public void Test_CancelFraction_Clamp_LowerBound()
        {
            // Arrange
            Vector3 vel = new Vector3(0f, 10f, 0f);
            Vector3 norm = new Vector3(0f, 0f, -1f);
            float pullUp = 0f;
            float cancelFrac = -0.5f; // should clamp to 0

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert
            // Y velocity should be multiplied by (1 - 0) = 1, so it remains 10.
            Assert.That(result.Y, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void Test_CancelFraction_Clamp_UpperBound()
        {
            // Arrange
            Vector3 vel = new Vector3(0f, 10f, 0f);
            Vector3 norm = new Vector3(0f, 0f, -1f);
            float pullUp = 0f;
            float cancelFrac = 2.0f; // should clamp to 1

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert
            // Y velocity should be multiplied by (1 - 1) = 0, so it becomes 0.
            Assert.That(result.Y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_PullUpForce_MinBound()
        {
            // Arrange
            Vector3 vel = new Vector3(0f, 0f, 0f);
            Vector3 norm = new Vector3(0f, 0f, -1f);
            float pullUp = -10f; // should clamp to 0
            float cancelFrac = 0f;

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert
            // Y velocity should have 0 pullUpForce added.
            Assert.That(result.Y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_LedgeNormal_FallbackToUnitY()
        {
            // Arrange
            Vector3 vel = new Vector3(0f, -10f, 0f);
            // Length squared < 0.0001f (0.01^2 = 0.0001)
            Vector3 norm = new Vector3(0f, 0.005f, 0f);
            float pullUp = 0f;
            float cancelFrac = 0f;

            // Act
            Vector3 result = LedgeGrabImpulseCalculator.Compute(vel, norm, pullUp, cancelFrac);

            // Assert
            // Fallback normal is UnitY (0, 1, 0).
            // Inward dot is Dot((0, -10, 0), (0, 1, 0)) = -10.
            // Since -10 < 0, we subtract UnitY * -10 from velocity -> (0, -10, 0) - (0, -10, 0) = (0, 0, 0).
            Assert.That(result.Y, Is.EqualTo(0f).Within(0.001f));
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
