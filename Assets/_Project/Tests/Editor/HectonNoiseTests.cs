using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonNoiseTests
    {
        [Test]
        public void SampleLUT_WithValidT_ReturnsInterpolatedValue()
        {
            // Arrange
            NativeArray<float> lut = new NativeArray<float>(5, Allocator.Temp);
            lut[0] = 0f;
            lut[1] = 10f;
            lut[2] = 20f;
            lut[3] = 30f;
            lut[4] = 40f;

            try
            {
                // Act
                float val0 = HectonNoise.SampleLUT(lut, 0f);
                float val1 = HectonNoise.SampleLUT(lut, 0.25f);
                float val2 = HectonNoise.SampleLUT(lut, 0.5f);
                float val3 = HectonNoise.SampleLUT(lut, 0.75f);
                float val4 = HectonNoise.SampleLUT(lut, 1f);

                float valMid = HectonNoise.SampleLUT(lut, 0.125f); // Halfway between 0 and 0.25 (idx 0 and 1)

                // Assert
                Assert.AreEqual(0f, val0);
                Assert.AreEqual(10f, val1);
                Assert.AreEqual(20f, val2);
                Assert.AreEqual(30f, val3);
                Assert.AreEqual(40f, val4);

                Assert.AreEqual(5f, valMid);
            }
            finally
            {
                lut.Dispose();
            }
        }

        [Test]
        public void SampleLUT_WithOutOfBoundsT_ClampsToRange()
        {
            // Arrange
            NativeArray<float> lut = new NativeArray<float>(2, Allocator.Temp);
            lut[0] = 5f;
            lut[1] = 15f;

            try
            {
                // Act
                float valUnder = HectonNoise.SampleLUT(lut, -0.5f);
                float valOver = HectonNoise.SampleLUT(lut, 1.5f);

                // Assert
                Assert.AreEqual(5f, valUnder);
                Assert.AreEqual(15f, valOver);
            }
            finally
            {
                lut.Dispose();
            }
        }

        [Test]
        public void SampleLUTReadOnly_WithValidT_ReturnsInterpolatedValue()
        {
            // Arrange
            NativeArray<float> lut = new NativeArray<float>(5, Allocator.Temp);
            lut[0] = 0f;
            lut[1] = 10f;
            lut[2] = 20f;
            lut[3] = 30f;
            lut[4] = 40f;

            try
            {
                var readOnlyLut = lut.AsReadOnly();

                // Act
                float val0 = HectonNoise.SampleLUT(readOnlyLut, 0f);
                float valMid = HectonNoise.SampleLUT(readOnlyLut, 0.125f);
                float val4 = HectonNoise.SampleLUT(readOnlyLut, 1f);

                // Assert
                Assert.AreEqual(0f, val0);
                Assert.AreEqual(5f, valMid);
                Assert.AreEqual(40f, val4);
            }
            finally
            {
                lut.Dispose();
            }
        }

        [Test]
        public void Fractal2D_WithDeterministicSeed_ReturnsConsistentResults()
        {
            // Arrange
            NoiseData nd = new NoiseData
            {
                seed = 12345,
                octaves = 3,
                scale = 0.01f,
                persistence = 0.5f,
                lacunarity = 2f,
                offset = new float3(0f, 0f, 0f)
            };

            // Act
            float val1 = HectonNoise.Fractal2D(10f, 20f, nd);
            float val2 = HectonNoise.Fractal2D(10f, 20f, nd);
            float valDifferentPos = HectonNoise.Fractal2D(15f, 20f, nd);

            // Assert
            Assert.AreEqual(val1, val2, "Same input should produce same output");
            Assert.AreNotEqual(val1, valDifferentPos, "Different positions should produce different output");
            Assert.IsTrue(val1 >= -1f && val1 <= 1f, "Noise should be bounded");
        }

        [Test]
        public void Fractal3D_WithDeterministicSeed_ReturnsConsistentResults()
        {
            // Arrange
            NoiseData nd = new NoiseData
            {
                seed = 54321,
                octaves = 2,
                scale = 0.05f,
                persistence = 0.5f,
                lacunarity = 2f,
                offset = new float3(1f, 2f, 3f)
            };

            // Act
            float val1 = HectonNoise.Fractal3D(10f, 20f, 30f, nd, 0f);
            float val2 = HectonNoise.Fractal3D(10f, 20f, 30f, nd, 0f);
            float valDifferentPos = HectonNoise.Fractal3D(10f, 25f, 30f, nd, 0f);
            float valDifferentChanOff = HectonNoise.Fractal3D(10f, 20f, 30f, nd, 100f);

            // Assert
            Assert.AreEqual(val1, val2, "Same input should produce same output");
            Assert.AreNotEqual(val1, valDifferentPos, "Different positions should produce different output");
            Assert.AreNotEqual(val1, valDifferentChanOff, "Different channel offset should produce different output");
            Assert.IsTrue(val1 >= -1f && val1 <= 1f, "Noise should be bounded");
        }
    }
}
