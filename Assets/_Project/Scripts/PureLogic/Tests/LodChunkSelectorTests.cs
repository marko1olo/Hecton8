using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class LodChunkSelectorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float[] thresholds = new float[] { 100f, 200f, 300f };
            int maxLod = 3;

            // Act
            int lodAt50 = LodChunkSelector.Calculate(50f, thresholds, maxLod);
            int lodAt150 = LodChunkSelector.Calculate(150f, thresholds, maxLod);
            int lodAt250 = LodChunkSelector.Calculate(250f, thresholds, maxLod);
            int lodAt350 = LodChunkSelector.Calculate(350f, thresholds, maxLod);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(0, lodAt50);
            Assert.AreEqual(1, lodAt150);
            Assert.AreEqual(2, lodAt250);
            Assert.AreEqual(3, lodAt350);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float[] thresholds = new float[] { 100f };
            int maxLod = 1;

            // Act
            int lodAtExactly100 = LodChunkSelector.Calculate(100f, thresholds, maxLod);
            int lodPastMax = LodChunkSelector.Calculate(500f, thresholds, maxLod);

            // maxLod clamp check
            float[] manyThresholds = new float[] { 100f, 200f, 300f, 400f };
            int lodClamp = LodChunkSelector.Calculate(500f, manyThresholds, 2);

            // Assert
            Assert.AreEqual(0, lodAtExactly100);
            Assert.AreEqual(1, lodPastMax);
            Assert.AreEqual(2, lodClamp);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float[] emptyThresholds = new float[0];
            float[] thresholds = new float[] { 100f };

            // Act
            int lodZeroDistance = LodChunkSelector.Calculate(0f, thresholds, 1);
            int lodEmptyThresholds = LodChunkSelector.Calculate(150f, emptyThresholds, 1);
            int lodNullThresholds = LodChunkSelector.Calculate(150f, null, 1);
            int lodNegativeMaxLod = LodChunkSelector.Calculate(150f, thresholds, -1);

            // Assert
            Assert.AreEqual(0, lodZeroDistance);
            Assert.AreEqual(0, lodEmptyThresholds);
            Assert.AreEqual(0, lodNullThresholds);
            Assert.AreEqual(0, lodNegativeMaxLod);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float[] thresholds = new float[] { 100f };

            // Act
            int lodNegativeDistance = LodChunkSelector.Calculate(-50f, thresholds, 1);

            // Assert
            Assert.AreEqual(0, lodNegativeDistance);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float[] thresholds = new float[] { 100f, 1000f, 10000f };

            // Act
            int lodInfinity = LodChunkSelector.Calculate(float.PositiveInfinity, thresholds, 3);
            int lodNaN = LodChunkSelector.Calculate(float.NaN, thresholds, 3);
            int lodMaxVal = LodChunkSelector.Calculate(float.MaxValue, thresholds, 3);

            // Assert
            Assert.AreEqual(0, lodInfinity);
            Assert.AreEqual(0, lodNaN);
            Assert.AreEqual(3, lodMaxVal);
        }

        [Test]
        public void Test_InvalidThresholds_Case06()
        {
            // Arrange: Setup invalid elements within thresholds array
            float[] thresholdsWithNegative = new float[] { -100f, 100f };
            float[] thresholdsWithNaN = new float[] { float.NaN, 100f };
            float[] thresholdsUnordered = new float[] { 300f, 100f, 200f };

            // Act
            int lodNegativeThreshold = LodChunkSelector.Calculate(50f, thresholdsWithNegative, 3);
            int lodNaNThreshold = LodChunkSelector.Calculate(150f, thresholdsWithNaN, 3);
            int lodUnorderedThreshold = LodChunkSelector.Calculate(150f, thresholdsUnordered, 3);

            // Assert
            // distanceFromCamera (50) > -100 (yes -> +1), > 100 (no -> break) => lod = 1
            Assert.AreEqual(1, lodNegativeThreshold);

            // distanceFromCamera (150) > NaN is false, so it breaks => lod = 0
            Assert.AreEqual(0, lodNaNThreshold);

            // distanceFromCamera (150) > 300 (no -> break) => breaks at i=0 => lod = 0
            Assert.AreEqual(0, lodUnorderedThreshold);
        }
    }
}