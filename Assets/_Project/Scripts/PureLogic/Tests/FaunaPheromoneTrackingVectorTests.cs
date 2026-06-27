using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FaunaPheromoneTrackingVectorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 faunaPos = new Vector3(0, 0, 0);
            Vector3[] coords = new Vector3[]
            {
                new Vector3(10, 0, 0),
                new Vector3(0, 10, 0),
                new Vector3(0, 0, 10)
            };
            float[] strengths = new float[] { 1f, 5f, 2f };

            // Act
            Vector3 result = FaunaPheromoneTrackingVector.Calculate(faunaPos, coords, strengths);

            // Assert
            Assert.That(result, Is.EqualTo(new Vector3(0, 1, 0)));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            Vector3 faunaPos = new Vector3(1, 1, 1);
            Vector3[] coords = new Vector3[]
            {
                new Vector3(2, 1, 1),
                new Vector3(1, 1, 1) // On top of fauna, highest strength but diff is zero
            };
            float[] strengths = new float[] { 1f, 10f };

            // Act
            Vector3 result = FaunaPheromoneTrackingVector.Calculate(faunaPos, coords, strengths);

            // Assert: The highest strength is on top of fauna, so diff is zero, which means returns Vector3.Zero
            Assert.That(result, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            Vector3 faunaPos = Vector3.Zero;
            Vector3[] coords = new Vector3[0];
            float[] strengths = new float[0];

            // Act
            Vector3 result1 = FaunaPheromoneTrackingVector.Calculate(faunaPos, coords, strengths);
            Vector3 result2 = FaunaPheromoneTrackingVector.Calculate(faunaPos, null!, null!);

            // Assert
            Assert.That(result1, Is.EqualTo(Vector3.Zero));
            Assert.That(result2, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            Vector3 faunaPos = new Vector3(0, 0, 0);
            Vector3[] coords = new Vector3[]
            {
                new Vector3(10, 0, 0),
                new Vector3(0, 10, 0)
            };
            float[] strengths = new float[] { -5f, -10f }; // All negative strengths

            // Act
            Vector3 result = FaunaPheromoneTrackingVector.Calculate(faunaPos, coords, strengths);

            // Assert: Should return Vector3.Zero since no positive valid strength found
            Assert.That(result, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            Vector3 faunaPos = new Vector3(float.NaN, 0, 0);
            Vector3[] coords = new Vector3[]
            {
                new Vector3(float.PositiveInfinity, 0, 0),
                new Vector3(10, 0, 0)
            };
            float[] strengths = new float[] { 100f, 5f };

            // Act
            Vector3 result = FaunaPheromoneTrackingVector.Calculate(faunaPos, coords, strengths);

            // Assert: NaN fauna pos should return zero immediately
            Assert.That(result, Is.EqualTo(Vector3.Zero));

            // Arrange
            Vector3 faunaPos2 = Vector3.Zero;

            // Act
            Vector3 result2 = FaunaPheromoneTrackingVector.Calculate(faunaPos2, coords, strengths);

            // Assert: Infinity coord should be ignored, falls back to 5f strength coord
            Assert.That(result2, Is.EqualTo(new Vector3(1, 0, 0)));
        }
    }
}
