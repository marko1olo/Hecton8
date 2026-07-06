using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class VoronoiBiomeSeedCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector3 worldPos = new Vector3(0, 0, 0);
            Vector3[] biomeSeedPoints = new Vector3[]
            {
                new Vector3(0, 0, 0), // Point A (Distance 0)
                new Vector3(10, 0, 0) // Point B (Distance 10)
            };
            string[] biomeTypes = new string[] { "BiomeA", "BiomeB" };
            float noiseBlend = 1.0f;

            // Act
            string result = VoronoiBiomeSeedCalculator.Compute(worldPos, biomeSeedPoints, biomeTypes, noiseBlend);

            // Assert: Verify expected output behaviour
            Assert.That(result, Is.EqualTo("BiomeA,1.000"));
        }

        [Test]
        public void Test_HappyPath_Midpoint()
        {
            Vector3 worldPos = new Vector3(5, 0, 0);
            Vector3[] biomeSeedPoints = new Vector3[]
            {
                new Vector3(0, 0, 0), // Point A (Distance 5)
                new Vector3(10, 0, 0) // Point B (Distance 5)
            };
            string[] biomeTypes = new string[] { "BiomeA", "BiomeB" };
            float noiseBlend = 1.0f;

            string result = VoronoiBiomeSeedCalculator.Compute(worldPos, biomeSeedPoints, biomeTypes, noiseBlend);
            Assert.That(result, Is.EqualTo("BiomeA,0.500"));
        }

        [Test]
        public void Test_HappyPath_NoNoiseBlend()
        {
            Vector3 worldPos = new Vector3(4, 0, 0);
            Vector3[] biomeSeedPoints = new Vector3[]
            {
                new Vector3(0, 0, 0), // Point A (Distance 4)
                new Vector3(10, 0, 0) // Point B (Distance 6)
            };
            string[] biomeTypes = new string[] { "BiomeA", "BiomeB" };
            float noiseBlend = 0.0f;

            string result = VoronoiBiomeSeedCalculator.Compute(worldPos, biomeSeedPoints, biomeTypes, noiseBlend);
            Assert.That(result, Is.EqualTo("BiomeA,1.000"));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector3 worldPos = new Vector3(0, 0, 0);
            Vector3[] biomeSeedPoints = new Vector3[]
            {
                new Vector3(0, 0, 0)
            };
            string[] biomeTypes = new string[] { "BiomeA" };

            // Act
            string result1 = VoronoiBiomeSeedCalculator.Compute(worldPos, biomeSeedPoints, biomeTypes, -5.0f);
            string result2 = VoronoiBiomeSeedCalculator.Compute(worldPos, biomeSeedPoints, biomeTypes, 5.0f);

            // Assert
            Assert.That(result1, Is.EqualTo("BiomeA,1.000"));
            Assert.That(result2, Is.EqualTo("BiomeA,1.000"));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            Vector3 worldPos = Vector3.Zero;
            Vector3[] biomeSeedPoints = new Vector3[]
            {
                Vector3.Zero,
                Vector3.Zero
            };
            string[] biomeTypes = new string[] { "BiomeA", "BiomeB" };
            float noiseBlend = 0f;

            // Act
            string result = VoronoiBiomeSeedCalculator.Compute(worldPos, biomeSeedPoints, biomeTypes, noiseBlend);

            // Assert
            // When all points are at 0, distances are 0.
            // Our logic: d1=0, d2=0, (d2>0.000001f is false), rawBlend = 1.0f.
            Assert.That(result, Is.EqualTo("BiomeA,1.000"));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 worldPos = new Vector3(-10, -20, -30);
            Vector3[] biomeSeedPoints = new Vector3[]
            {
                new Vector3(-5, -20, -30), // dist = 5
                new Vector3(-20, -20, -30) // dist = 10
            };
            string[] biomeTypes = new string[] { "BiomeA", "BiomeB" };
            float noiseBlend = 1.0f;

            // Act
            string result = VoronoiBiomeSeedCalculator.Compute(worldPos, biomeSeedPoints, biomeTypes, noiseBlend);

            // Assert
            // rawBlend = 0.5 + 0.5 * ((10 - 5) / 10) = 0.75
            Assert.That(result, Is.EqualTo("BiomeA,0.750"));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 worldPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3[] biomeSeedPoints = new Vector3[]
            {
                new Vector3(float.MinValue, float.MinValue, float.MinValue),
                new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)
            };
            string[] biomeTypes = new string[] { "BiomeA", "BiomeB" };
            float noiseBlend = 1.0f;

            // Act
            string result = VoronoiBiomeSeedCalculator.Compute(worldPos, biomeSeedPoints, biomeTypes, noiseBlend);

            // Assert: worldPos is float.MaxValue. Distance to float.MinValue will be Infinity.
            // Distance to float.MaxValue will be 0.
            // So closest should be BiomeB.
            Assert.That(result, Is.EqualTo("BiomeB,1.000"));
        }

        [Test]
        public void Test_Exception_NullArrays()
        {
            Assert.Throws<ArgumentNullException>(() =>
                VoronoiBiomeSeedCalculator.Compute(Vector3.Zero, null, new string[]{"A"}, 0f));

            Assert.Throws<ArgumentNullException>(() =>
                VoronoiBiomeSeedCalculator.Compute(Vector3.Zero, new Vector3[]{Vector3.Zero}, null, 0f));
        }

        [Test]
        public void Test_Exception_EmptyArrays()
        {
            Assert.Throws<ArgumentException>(() =>
                VoronoiBiomeSeedCalculator.Compute(Vector3.Zero, new Vector3[0], new string[]{"A"}, 0f));

            Assert.Throws<ArgumentException>(() =>
                VoronoiBiomeSeedCalculator.Compute(Vector3.Zero, new Vector3[]{Vector3.Zero}, new string[0], 0f));
        }

        [Test]
        public void Test_Exception_MismatchedLengths()
        {
            Assert.Throws<ArgumentException>(() =>
                VoronoiBiomeSeedCalculator.Compute(Vector3.Zero, new Vector3[]{Vector3.Zero, Vector3.One}, new string[]{"A"}, 0f));
        }

        [Test]
        public void Test_Exception_NaNOrInfinity()
        {
            Assert.Throws<ArgumentException>(() =>
                VoronoiBiomeSeedCalculator.Compute(new Vector3(float.NaN, 0, 0), new Vector3[]{Vector3.Zero}, new string[]{"A"}, 0f));

            Assert.Throws<ArgumentException>(() =>
                VoronoiBiomeSeedCalculator.Compute(Vector3.Zero, new Vector3[]{Vector3.Zero}, new string[]{"A"}, float.PositiveInfinity));
        }

        [Test]
        public void Test_Exception_Overflow()
        {
            Vector3 worldPos = Vector3.Zero;
            Vector3[] biomeSeedPoints = new Vector3[] { new Vector3(10, 0, 0) };
            string[] biomeTypes = new string[] { "BiomeA" };

            // distance calculation throws OverflowException, which is caught and returns float.MaxValue, which means it returns BiomeA,1.000
            string result = VoronoiBiomeSeedCalculator.Compute(worldPos, biomeSeedPoints, biomeTypes, 0f, (a, b) => throw new OverflowException());

            Assert.That(result, Is.EqualTo("BiomeA,1.000"));
        }
    }
}
