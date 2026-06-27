using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class CableConstraintSatisfierTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector3 posA = new Vector3(0, 0, 0);
            Vector3 posB = new Vector3(2, 0, 0); // delta = (2,0,0), len = 2
            float restLength = 1f; // error = 2 - 1 = 1
            float stiffness = 1f;

            // Act
            Vector3 correction = CableConstraintSatisfier.Calculate(posA, posB, restLength, stiffness);

            // Expected correction calculation:
            // error = 1, invLen = 0.5, stiffness = 1, delta = (2,0,0)
            // correctionFactor = 1 * 0.5 * 1 * 0.5 = 0.25
            // correction = (2,0,0) * 0.25 = (0.5, 0, 0)

            // Assert: Verify expected output behaviour
            Assert.AreEqual(0.5f, correction.X, 0.001f);
            Assert.AreEqual(0f, correction.Y, 0.001f);
            Assert.AreEqual(0f, correction.Z, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector3 posA = new Vector3(0, 0, 0);
            Vector3 posB = new Vector3(0.0000001f, 0, 0); // lenSq < 1e-6
            float restLength = 1f;
            float stiffness = 1f;

            // Act
            Vector3 correction = CableConstraintSatisfier.Calculate(posA, posB, restLength, stiffness);

            // Assert
            Assert.AreEqual(Vector3.Zero, correction, "Correction should be zero when distance squared is below threshold.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            Vector3 posA = Vector3.Zero;
            Vector3 posB = Vector3.Zero;
            float restLength = 0f;
            float stiffness = 0f;

            // Act
            Vector3 correction = CableConstraintSatisfier.Calculate(posA, posB, restLength, stiffness);

            // Assert
            Assert.AreEqual(Vector3.Zero, correction, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 posA = new Vector3(0, 0, 0);
            Vector3 posB = new Vector3(1, 0, 0);
            float restLength = -5f; // Should be clamped to MinConstraintLength (1e-3f)
            float stiffness = -2f; // Should be clamped to 0f

            // Act
            Vector3 correction = CableConstraintSatisfier.Calculate(posA, posB, restLength, stiffness);

            // Assert
            // Because stiffness is clamped to 0, correction should be zero.
            Assert.AreEqual(Vector3.Zero, correction, "Verify negative inputs clamp gracefully or return zero.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 posA = new Vector3(float.NaN, 0, 0);
            Vector3 posB = new Vector3(1, 0, 0);
            float restLength = 1f;
            float stiffness = 1f;

            // Act
            Vector3 correctionNaN = CableConstraintSatisfier.Calculate(posA, posB, restLength, stiffness);

            // Assert
            Assert.AreEqual(Vector3.Zero, correctionNaN, "Verify robust calculation and overflow protection handles NaN.");

            // Arrange infinite
            Vector3 posAInf = new Vector3(float.PositiveInfinity, 0, 0);
            Vector3 correctionInf = CableConstraintSatisfier.Calculate(posAInf, posB, restLength, stiffness);

            Assert.AreEqual(Vector3.Zero, correctionInf, "Verify robust calculation handles Infinity.");
        }

        [Test]
        public void Test_Compressed_Case06()
        {
            // Arrange: Setup compressed inputs (pushed apart)
            Vector3 posA = new Vector3(0, 0, 0);
            Vector3 posB = new Vector3(1, 0, 0); // delta = (1,0,0), len = 1
            float restLength = 2f; // error = 1 - 2 = -1
            float stiffness = 1f;

            // Act
            Vector3 correction = CableConstraintSatisfier.Calculate(posA, posB, restLength, stiffness);

            // Expected correction calculation:
            // error = -1, invLen = 1, stiffness = 1, delta = (1,0,0)
            // correctionFactor = -1 * 1 * 1 * 0.5 = -0.5
            // correction = (1,0,0) * -0.5 = (-0.5, 0, 0)

            // newPosA = posA + correction = (-0.5, 0, 0)
            // newPosB = posB - correction = (1.5, 0, 0)
            // distance becomes 2.

            // Assert
            Assert.AreEqual(-0.5f, correction.X, 0.001f);
            Assert.AreEqual(0f, correction.Y, 0.001f);
            Assert.AreEqual(0f, correction.Z, 0.001f);
        }

        [Test]
        public void Test_AtRestLength_Case07()
        {
            // Arrange: Setup inputs exactly at rest length
            Vector3 posA = new Vector3(0, 0, 0);
            Vector3 posB = new Vector3(1, 0, 0); // len = 1
            float restLength = 1f; // error = 1 - 1 = 0
            float stiffness = 1f;

            // Act
            Vector3 correction = CableConstraintSatisfier.Calculate(posA, posB, restLength, stiffness);

            // Assert
            Assert.AreEqual(Vector3.Zero, correction, "No correction expected at rest length.");
        }
    }
}
