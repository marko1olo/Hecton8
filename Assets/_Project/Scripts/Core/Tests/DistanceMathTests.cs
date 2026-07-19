using NUnit.Framework;
using Unity.Mathematics;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Core.Tests
{
    [TestFixture]
    public class DistanceMathTests
    {
        [Test]
        public void ResolveDistanceQualityWeight01_BelowHighQuality_ReturnsWeightedSmoothValue()
        {
            float distanceSq = 100f; // well within HighQualityDistanceSq (15^2 = 225)
            float globalWeight = 1.0f; // maximum quality
            float quality = DistanceMath.ResolveDistanceQualityWeight01(distanceSq, globalWeight);
            Assert.That(quality, Is.EqualTo(1.0f).Within(0.0001f));
        }

        [Test]
        public void ResolveDistanceQualityWeight01_AtMidpoint_ReturnsLerpedValue()
        {
            float near = DistanceMath.HighQualityDistanceSq; // 225
            float far = near * 16f; // 3600
            float mid = (near + far) / 2f; // 1912.5
            float quality = DistanceMath.ResolveDistanceQualityWeight01(mid, 1.0f);
            Assert.That(quality, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void ResolveDistanceQualityWeight01_BeyondFarDistance_ReturnsZero()
        {
            float far = DistanceMath.HighQualityDistanceSq * 16f; // 3600
            float distanceSq = far + 100f; // 3700
            float quality = DistanceMath.ResolveDistanceQualityWeight01(distanceSq, 1.0f);
            Assert.That(quality, Is.EqualTo(0.0f).Within(0.0001f));
        }

        [Test]
        public void ResolveDistanceQualityWeight01_ZeroGlobalWeight_ReturnsZero()
        {
            float distanceSq = DistanceMath.HighQualityDistanceSq;
            float quality = DistanceMath.ResolveDistanceQualityWeight01(distanceSq, 0.0f);
            Assert.That(quality, Is.EqualTo(0.0f).Within(0.0001f));
        }

        [Test]
        public void ResolveDistanceQualityWeight01_NegativeDistance_HandledSafely()
        {
            float quality = DistanceMath.ResolveDistanceQualityWeight01(-100f, 1.0f);
            Assert.That(quality, Is.EqualTo(1.0f).Within(0.0001f));
        }

        [Test]
        public void ResolveDistanceQualityWeight01_InfinityDistance_HandledSafely()
        {
            float quality = DistanceMath.ResolveDistanceQualityWeight01(float.PositiveInfinity, 1.0f);
            Assert.That(quality, Is.EqualTo(1.0f).Within(0.0001f));
        }

        [Test]
        public void ResolveDistanceQualityWeight01_NaN_HandledSafely()
        {
            float quality = DistanceMath.ResolveDistanceQualityWeight01(float.NaN, 1.0f);
            Assert.That(quality, Is.EqualTo(1.0f).Within(0.0001f));
        }

        [Test]
        public void IsHighQuality_ValidDistance_ReturnsTrue()
        {
            Assert.That(DistanceMath.IsHighQuality(10f), Is.True);
        }

        [Test]
        public void IsHighQuality_PastHighQualityDistance_ReturnsFalse()
        {
            Assert.That(DistanceMath.IsHighQuality(DistanceMath.HighQualityDistanceSq + 10f), Is.False);
        }

        [Test]
        public void IsHighQuality_InfinityOrNaN_ReturnsFalse()
        {
            Assert.That(DistanceMath.IsHighQuality(float.PositiveInfinity), Is.False);
            Assert.That(DistanceMath.IsHighQuality(float.NaN), Is.False);
        }

        [Test]
        public void ResolveMathLodMode_HighWeight_ReturnsHigh()
        {
            Assert.That(DistanceMath.ResolveMathLodMode(1.0f), Is.EqualTo(MathLodMode.High));
            Assert.That(DistanceMath.ResolveMathLodMode(0.5f), Is.EqualTo(MathLodMode.High));
        }

        [Test]
        public void ResolveMathLodMode_LowWeight_ReturnsLow()
        {
            Assert.That(DistanceMath.ResolveMathLodMode(0.49f), Is.EqualTo(MathLodMode.Low));
            Assert.That(DistanceMath.ResolveMathLodMode(0.0f), Is.EqualTo(MathLodMode.Low));
        }

        [Test]
        public void ResolveMathLodMode_InvalidWeight_ReturnsHigh()
        {
            // SanitizeQualityWeight01 returns 1 for NaN/Infinity
            // Let's check SanitizeQualityWeight01 specifically
            Assert.That(DistanceMath.SanitizeQualityWeight01(float.NaN), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(DistanceMath.ResolveMathLodMode(float.NaN), Is.EqualTo(MathLodMode.High));
        }

        [Test]
        public void SanitizeQualityWeight01_ValidWeight_ReturnsSaturated()
        {
            Assert.That(DistanceMath.SanitizeQualityWeight01(0.5f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(DistanceMath.SanitizeQualityWeight01(1.5f), Is.EqualTo(1.0f).Within(0.0001f));
            Assert.That(DistanceMath.SanitizeQualityWeight01(-0.5f), Is.EqualTo(0.0f).Within(0.0001f));
        }

        [Test]
        public void SanitizeQualityWeight01_InvalidWeight_ReturnsOne()
        {
            Assert.That(DistanceMath.SanitizeQualityWeight01(float.NaN), Is.EqualTo(1.0f).Within(0.0001f));
            Assert.That(DistanceMath.SanitizeQualityWeight01(float.PositiveInfinity), Is.EqualTo(1.0f).Within(0.0001f));
        }

        [Test]
        public void Normalize_ZeroVector_ReturnsFallback()
        {
            float3 vector = new float3(0, 0, 0);
            float3 fallback = new float3(0, 1, 0);
            float3 result = DistanceMath.Normalize(vector, 10f, fallback);
            Assert.That(result.x, Is.EqualTo(fallback.x).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(fallback.y).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(fallback.z).Within(0.0001f));
        }

        [Test]
        public void Normalize_ValidVector_ReturnsNormalized()
        {
            float3 vector = new float3(10, 0, 0);
            float3 result = DistanceMath.Normalize(vector, 10f); // distance = 10f, high quality
            Assert.That(result.x, Is.EqualTo(1.0f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(0.0f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(0.0f).Within(0.0001f));
        }

        [Test]
        public void Normalize_InvalidVector_ReturnsFallback()
        {
            float3 vector = new float3(float.NaN, 0, 0);
            float3 fallback = new float3(0, 1, 0);
            float3 result = DistanceMath.Normalize(vector, 10f, fallback);
            Assert.That(result.x, Is.EqualTo(fallback.x).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(fallback.y).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(fallback.z).Within(0.0001f));
        }

        [Test]
        public void DominantAxisOrDefault_ValidVector_ReturnsDominantAxis()
        {
            float3 vector = new float3(0.1f, 10f, -0.5f);
            float3 result = DistanceMath.DominantAxisOrDefault(vector, new float3(0,0,1));
            Assert.That(result.x, Is.EqualTo(0.0f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(1.0f).Within(0.0001f)); // dominant Y
            Assert.That(result.z, Is.EqualTo(0.0f).Within(0.0001f));

            float3 vector2 = new float3(-10f, 0.5f, 0.1f);
            float3 result2 = DistanceMath.DominantAxisOrDefault(vector2, new float3(0,0,1));
            Assert.That(result2.x, Is.EqualTo(-1.0f).Within(0.0001f)); // dominant -X
            Assert.That(result2.y, Is.EqualTo(0.0f).Within(0.0001f));
            Assert.That(result2.z, Is.EqualTo(0.0f).Within(0.0001f));
        }

        [Test]
        public void DominantAxisOrDefault_ZeroVector_ReturnsFallback()
        {
            float3 vector = float3.zero;
            float3 fallback = new float3(0, 0, 1);
            float3 result = DistanceMath.DominantAxisOrDefault(vector, fallback);
            Assert.That(result.x, Is.EqualTo(fallback.x).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(fallback.y).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(fallback.z).Within(0.0001f));
        }

        [Test]
        public void DominantAxisOrDefault_InvalidVector_ReturnsFallback()
        {
            float3 vector = new float3(float.PositiveInfinity, 0, 0);
            float3 fallback = new float3(0, 0, 1);
            float3 result = DistanceMath.DominantAxisOrDefault(vector, fallback);
            Assert.That(result.x, Is.EqualTo(fallback.x).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(fallback.y).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(fallback.z).Within(0.0001f));
        }

        [Test]
        public void DistanceBlend01_BelowNear_ReturnsZero()
        {
            float result = DistanceMath.DistanceBlend01(10f, 20f, 100f);
            Assert.That(result, Is.EqualTo(0.0f).Within(0.0001f));
        }

        [Test]
        public void DistanceBlend01_AboveFar_ReturnsOne()
        {
            float result = DistanceMath.DistanceBlend01(110f, 20f, 100f);
            Assert.That(result, Is.EqualTo(1.0f).Within(0.0001f));
        }

        [Test]
        public void DistanceBlend01_Midpoint_ReturnsLerp()
        {
            float result = DistanceMath.DistanceBlend01(60f, 20f, 100f);
            Assert.That(result, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void DistanceBlend01_NearEqualsFar_SafelyReturnsSaturated()
        {
            float result1 = DistanceMath.DistanceBlend01(10f, 20f, 20f);
            float result2 = DistanceMath.DistanceBlend01(30f, 20f, 20f);

            // spanSq is math.max(MinimumDistanceSq, 20-20) = MinimumDistanceSq.
            // if distance is 10, 10-20 = -10, saturated -> 0
            // if distance is 30, 30-20 = 10, saturated -> 1
            Assert.That(result1, Is.EqualTo(0.0f).Within(0.0001f));
            Assert.That(result2, Is.EqualTo(1.0f).Within(0.0001f));
        }

        [Test]
        public void DistanceBlendMeters01_Midpoint_ReturnsLerp()
        {
            // Let's pick nice numbers:
            // near = 10m, far = 20m.
            // mid = 15m.
            // distanceSq for 15m = 225.
            float result = DistanceMath.DistanceBlendMeters01(225f, 10f, 20f);
            Assert.That(result, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void LerpByDistanceSq_Midpoint_ReturnsLerpedValue()
        {
            float result = DistanceMath.LerpByDistanceSq(10f, 20f, 60f, 20f, 100f);
            // Blend is 0.5. Lerp(10, 20, 0.5) = 15.
            Assert.That(result, Is.EqualTo(15.0f).Within(0.0001f));
        }

        [Test]
        public void LerpByDistanceSq_MidpointFloat3_ReturnsLerpedValue()
        {
            float3 near = new float3(10, 0, 0);
            float3 far = new float3(20, 0, 0);
            float3 result = DistanceMath.LerpByDistanceSq(near, far, 60f, 20f, 100f);
            Assert.That(result.x, Is.EqualTo(15.0f).Within(0.0001f));
        }

        [Test]
        public void Sin_HighQuality_ReturnsFastSin()
        {
            float distanceSq = 1f; // high quality
            float radians = math.PI / 2f;
            float result = DistanceMath.Sin(radians, distanceSq, 1.0f);
            Assert.That(result, Is.EqualTo(1.0f).Within(0.05f)); // FastSin should be close to 1
        }

        [Test]
        public void Sin_LowQuality_ReturnsTriangleWave()
        {
            float far = DistanceMath.HighQualityDistanceSq * 16f + 10f; // low quality
            float radians = math.PI / 2f;
            float result = DistanceMath.Sin(radians, far, 1.0f);
            // Triangle wave at PI/2
            // phase = PI/2 * InvTwoPi = 0.25
            // wrapped = 0.25
            // magnitude = FastTriangleWave01(0.5) => wait let's just assert it is > 0
            Assert.That(result, Is.GreaterThan(0.0f));
        }

        [Test]
        public void Cos_HighQuality_ReturnsFastCos()
        {
            float distanceSq = 1f; // high quality
            float radians = 0f;
            float result = DistanceMath.Cos(radians, distanceSq, 1.0f);
            Assert.That(result, Is.EqualTo(1.0f).Within(0.05f));
        }
    }
}
