using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PoissondiscLandmarkSpacingSolverTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector3 candidateCoord = new Vector3(10f, 0f, 10f);
            Vector3[] existingCoords = new Vector3[] {
                new Vector3(0f, 0f, 0f),
                new Vector3(20f, 0f, 20f)
            };
            float minDistance = 5f;

            // Act
            bool result = PoissondiscLandmarkSpacingSolver.Solve(candidateCoord, existingCoords, minDistance);

            // Assert: Verify expected output behaviour
            Assert.IsTrue(result, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector3 candidateCoord = new Vector3(5f, 0f, 0f);
            Vector3[] existingCoords = new Vector3[] {
                new Vector3(0f, 0f, 0f)
            };
            float minDistance = 6f;

            // Act
            bool result = PoissondiscLandmarkSpacingSolver.Solve(candidateCoord, existingCoords, minDistance);

            // Assert
            Assert.IsFalse(result, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            Vector3 candidateCoord = new Vector3(0f, 0f, 0f);
            Vector3[] existingCoords = new Vector3[] {
                new Vector3(0f, 0f, 0f)
            };
            float minDistance = 1f;

            // Act
            bool result = PoissondiscLandmarkSpacingSolver.Solve(candidateCoord, existingCoords, minDistance);

            // Assert
            Assert.IsFalse(result, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 candidateCoord = new Vector3(-5f, -5f, -5f);
            Vector3[] existingCoords = new Vector3[] {
                new Vector3(-5f, -4f, -5f)
            };
            float minDistance = -10f; // Clamped to 0

            // Act
            bool result = PoissondiscLandmarkSpacingSolver.Solve(candidateCoord, existingCoords, minDistance);

            // Assert
            Assert.IsTrue(result, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 candidateCoord = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3[] existingCoords = new Vector3[] {
                new Vector3(float.MinValue, float.MinValue, float.MinValue)
            };
            float minDistance = 10f;

            // Act
            bool result = PoissondiscLandmarkSpacingSolver.Solve(candidateCoord, existingCoords, minDistance);

            // Assert
            Assert.IsTrue(result, "Verify robust calculation and overflow protection.");
        }

        [Test]
        public void Test_NaNInputs_Case06()
        {
            // Arrange
            Vector3 candidateCoord = new Vector3(float.NaN, 0f, 0f);
            Vector3[] existingCoords = new Vector3[] {
                new Vector3(0f, 0f, 0f)
            };
            float minDistance = 5f;

            // Act
            bool result = PoissondiscLandmarkSpacingSolver.Solve(candidateCoord, existingCoords, minDistance);

            // Assert
            Assert.IsFalse(result, "Verify NaN inputs are handled correctly.");
        }

        [Test]
        public void Test_NullExistingCoords_Case07()
        {
            // Arrange
            Vector3 candidateCoord = new Vector3(0f, 0f, 0f);
            Vector3[] existingCoords = null;
            float minDistance = 5f;

            // Act
            bool result = PoissondiscLandmarkSpacingSolver.Solve(candidateCoord, existingCoords, minDistance);

            // Assert
            Assert.IsTrue(result, "Verify null existing coords array doesn't throw.");
        }
    }
}
