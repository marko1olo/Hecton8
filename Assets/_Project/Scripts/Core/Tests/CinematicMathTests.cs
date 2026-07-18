using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Core.Tests
{
    [TestFixture]
    public class CinematicMathTests
    {
        // 1. ApproximateLength
        [Test]
        public void ApproximateLength_ValidInput_ReturnsExpectedApprox()
        {
            Assert.That(CinematicMath.ApproximateLength(new float3(1, 0, 0)), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void ApproximateLength_ZeroInput_ReturnsZero()
        {
            Assert.That(CinematicMath.ApproximateLength(float3.zero), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ApproximateLength_NegativeInput_ReturnsExpectedApprox()
        {
            Assert.That(CinematicMath.ApproximateLength(new float3(-1, -2, -3)), Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void ApproximateLength_InfinityInput_ReturnsZero()
        {
            Assert.That(CinematicMath.ApproximateLength(new float3(float.PositiveInfinity, 0, 0)), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ApproximateLength_NaNInput_ReturnsZero()
        {
            Assert.That(CinematicMath.ApproximateLength(new float3(float.NaN, 0, 0)), Is.EqualTo(0f).Within(0.001f));
        }

        // 2. FastTriangleWave01
        [Test]
        public void FastTriangleWave01_StandardPhases_ReturnsExpected()
        {
            Assert.That(CinematicMath.FastTriangleWave01(0.25f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(CinematicMath.FastTriangleWave01(0.5f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastTriangleWave01_ZeroInput_ReturnsZero()
        {
            Assert.That(CinematicMath.FastTriangleWave01(0f), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FastTriangleWave01_NegativeInput_ReturnsExpected()
        {
            Assert.That(CinematicMath.FastTriangleWave01(-0.25f), Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void FastTriangleWave01_InfinityInput_ReturnsZero()
        {
            Assert.That(CinematicMath.FastTriangleWave01(float.PositiveInfinity), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FastTriangleWave01_NaNInput_ReturnsZero()
        {
            Assert.That(CinematicMath.FastTriangleWave01(float.NaN), Is.EqualTo(0f).Within(0.001f));
        }

        // 3. FastTriangleWaveSigned
        [Test]
        public void FastTriangleWaveSigned_StandardPhases_ReturnsExpected()
        {
            Assert.That(CinematicMath.FastTriangleWaveSigned(0.25f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(CinematicMath.FastTriangleWaveSigned(0.5f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastTriangleWaveSigned_ZeroInput_ReturnsNegativeOne()
        {
            Assert.That(CinematicMath.FastTriangleWaveSigned(0f), Is.EqualTo(-1f).Within(0.001f));
        }

        [Test]
        public void FastTriangleWaveSigned_NegativeInput_ReturnsExpected()
        {
            Assert.That(CinematicMath.FastTriangleWaveSigned(-0.25f), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FastTriangleWaveSigned_InfinityInput_ReturnsNegativeOne()
        {
            Assert.That(CinematicMath.FastTriangleWaveSigned(float.PositiveInfinity), Is.EqualTo(-1f).Within(0.001f));
        }

        [Test]
        public void FastTriangleWaveSigned_NaNInput_ReturnsNegativeOne()
        {
            Assert.That(CinematicMath.FastTriangleWaveSigned(float.NaN), Is.EqualTo(-1f).Within(0.001f));
        }

        // 4. FastSin
        [Test]
        public void FastSin_StandardAngles_ReturnsExpected()
        {
            Assert.That(CinematicMath.FastSin(math.PI / 2f), Is.EqualTo(1f).Within(0.05f));
        }

        [Test]
        public void FastSin_ZeroInput_ReturnsZero()
        {
            Assert.That(CinematicMath.FastSin(0f), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FastSin_NegativeInput_ReturnsExpected()
        {
            Assert.That(CinematicMath.FastSin(-math.PI / 2f), Is.EqualTo(-1f).Within(0.05f));
        }

        [Test]
        public void FastSin_InfinityInput_ReturnsZero()
        {
            Assert.That(CinematicMath.FastSin(float.PositiveInfinity), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FastSin_NaNInput_ReturnsZero()
        {
            Assert.That(CinematicMath.FastSin(float.NaN), Is.EqualTo(0f).Within(0.001f));
        }

        // 5. FastCos
        [Test]
        public void FastCos_StandardAngles_ReturnsExpected()
        {
            Assert.That(CinematicMath.FastCos(math.PI), Is.EqualTo(-1f).Within(0.05f));
        }

        [Test]
        public void FastCos_ZeroInput_ReturnsOne()
        {
            Assert.That(CinematicMath.FastCos(0f), Is.EqualTo(1f).Within(0.05f));
        }

        [Test]
        public void FastCos_NegativeInput_ReturnsExpected()
        {
            Assert.That(CinematicMath.FastCos(-math.PI), Is.EqualTo(-1f).Within(0.05f));
        }

        [Test]
        public void FastCos_InfinityInput_ReturnsZero()
        {
            Assert.That(CinematicMath.FastCos(float.PositiveInfinity), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FastCos_NaNInput_ReturnsZero()
        {
            Assert.That(CinematicMath.FastCos(float.NaN), Is.EqualTo(0f).Within(0.001f));
        }

        // 6. FastYawQuaternion
        [Test]
        public void FastYawQuaternion_StandardAngles_ReturnsExpected()
        {
            quaternion q = CinematicMath.FastYawQuaternion(math.PI);
            Assert.That(q.value.y, Is.EqualTo(1f).Within(0.05f));
        }

        [Test]
        public void FastYawQuaternion_ZeroInput_ReturnsIdentity()
        {
            quaternion q = CinematicMath.FastYawQuaternion(0f);
            Assert.That(q.value.w, Is.EqualTo(1f).Within(0.001f));
            Assert.That(q.value.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FastYawQuaternion_NegativeInput_ReturnsExpected()
        {
            quaternion q = CinematicMath.FastYawQuaternion(-math.PI);
            Assert.That(q.value.y, Is.EqualTo(-1f).Within(0.05f));
        }

        [Test]
        public void FastYawQuaternion_InfinityInput_ReturnsIdentity()
        {
            quaternion q = CinematicMath.FastYawQuaternion(float.PositiveInfinity);
            Assert.That(q.value.w, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastYawQuaternion_NaNInput_ReturnsIdentity()
        {
            quaternion q = CinematicMath.FastYawQuaternion(float.NaN);
            Assert.That(q.value.w, Is.EqualTo(1f).Within(0.001f));
        }

        // 7. NormalizeQuaternionOrIdentity (quaternion)
        [Test]
        public void NormalizeQuaternionOrIdentity_Valid_Normalizes()
        {
            quaternion q = new quaternion(0, 2, 0, 0);
            quaternion n = CinematicMath.NormalizeQuaternionOrIdentity(q);
            Assert.That(n.value.y, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void NormalizeQuaternionOrIdentity_ZeroLength_ReturnsIdentity()
        {
            quaternion q = new quaternion(0, 0, 0, 0);
            quaternion n = CinematicMath.NormalizeQuaternionOrIdentity(q);
            Assert.That(n.value.w, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void NormalizeQuaternionOrIdentity_NegativeValues_Normalizes()
        {
            quaternion q = new quaternion(0, -2, 0, 0);
            quaternion n = CinematicMath.NormalizeQuaternionOrIdentity(q);
            Assert.That(n.value.y, Is.EqualTo(-1f).Within(0.001f));
        }

        [Test]
        public void NormalizeQuaternionOrIdentity_Infinity_ReturnsIdentity()
        {
            quaternion q = new quaternion(0, float.PositiveInfinity, 0, 0);
            quaternion n = CinematicMath.NormalizeQuaternionOrIdentity(q);
            Assert.That(n.value.w, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void NormalizeQuaternionOrIdentity_NaN_ReturnsIdentity()
        {
            quaternion q = new quaternion(0, float.NaN, 0, 0);
            quaternion n = CinematicMath.NormalizeQuaternionOrIdentity(q);
            Assert.That(n.value.w, Is.EqualTo(1f).Within(0.001f));
        }

        // 8. FastNlerp (quaternion, quaternion, float)
        [Test]
        public void FastNlerp_Math_Halfway_Blends()
        {
            quaternion a = quaternion.identity;
            quaternion b = new quaternion(0, 1, 0, 0);
            quaternion res = CinematicMath.FastNlerp(a, b, 0.5f);
            Assert.That(res.value.w, Is.EqualTo(0.707f).Within(0.01f));
            Assert.That(res.value.y, Is.EqualTo(0.707f).Within(0.01f));
        }

        [Test]
        public void FastNlerp_Math_ZeroT_ReturnsA()
        {
            quaternion a = quaternion.identity;
            quaternion b = new quaternion(0, 1, 0, 0);
            quaternion res = CinematicMath.FastNlerp(a, b, 0f);
            Assert.That(res.value.w, Is.EqualTo(1f).Within(0.001f));
            Assert.That(res.value.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FastNlerp_Math_NegativeT_ReturnsA()
        {
            quaternion a = quaternion.identity;
            quaternion b = new quaternion(0, 1, 0, 0);
            quaternion res = CinematicMath.FastNlerp(a, b, -1f);
            Assert.That(res.value.w, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastNlerp_Math_InfinityT_ReturnsA()
        {
            quaternion a = quaternion.identity;
            quaternion b = new quaternion(0, 1, 0, 0);
            quaternion res = CinematicMath.FastNlerp(a, b, float.PositiveInfinity);
            Assert.That(res.value.w, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastNlerp_Math_NaNT_ReturnsA()
        {
            quaternion a = quaternion.identity;
            quaternion b = new quaternion(0, 1, 0, 0);
            quaternion res = CinematicMath.FastNlerp(a, b, float.NaN);
            Assert.That(res.value.w, Is.EqualTo(1f).Within(0.001f));
        }

        // 9. FastNlerp (Quaternion, Vector3, float, Vector3)
        [Test]
        public void FastNlerp_UnityDir_Valid_Blends()
        {
            Quaternion a = Quaternion.identity;
            Vector3 dir = Vector3.left;
            Quaternion res = CinematicMath.FastNlerp(a, dir, 0.5f, Vector3.up);
            Assert.That(res.y, Is.EqualTo(-0.382f).Within(0.05f)); // look left is euler 0, 270, 0 -> q=(0, -0.707, 0, 0.707). Mid is approx (0, -0.382, 0, 0.923)
        }

        [Test]
        public void FastNlerp_UnityDir_ZeroT_ReturnsA()
        {
            Quaternion a = Quaternion.identity;
            Vector3 dir = Vector3.left;
            Quaternion res = CinematicMath.FastNlerp(a, dir, 0f, Vector3.up);
            Assert.That(res.w, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastNlerp_UnityDir_NegativeT_ReturnsA()
        {
            Quaternion a = Quaternion.identity;
            Vector3 dir = Vector3.left;
            Quaternion res = CinematicMath.FastNlerp(a, dir, -0.5f, Vector3.up);
            Assert.That(res.w, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastNlerp_UnityDir_InfinityT_ReturnsA()
        {
            Quaternion a = Quaternion.identity;
            Vector3 dir = Vector3.left;
            Quaternion res = CinematicMath.FastNlerp(a, dir, float.PositiveInfinity, Vector3.up);
            Assert.That(res.w, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastNlerp_UnityDir_ZeroDir_Fallback()
        {
            Quaternion a = Quaternion.identity;
            Vector3 dir = Vector3.zero;
            Quaternion res = CinematicMath.FastNlerp(a, dir, 1f, Vector3.up);
            Assert.That(res.w, Is.EqualTo(1f).Within(0.001f));
        }

        // 10. FastNlerp (Quaternion, Quaternion, float)
        [Test]
        public void FastNlerp_Unity_Halfway_Blends()
        {
            Quaternion a = Quaternion.identity;
            Quaternion b = new Quaternion(0, 1, 0, 0);
            Quaternion res = CinematicMath.FastNlerp(a, b, 0.5f);
            Assert.That(res.w, Is.EqualTo(0.707f).Within(0.01f));
            Assert.That(res.y, Is.EqualTo(0.707f).Within(0.01f));
        }

        [Test]
        public void FastNlerp_Unity_ZeroT_ReturnsA()
        {
            Quaternion a = Quaternion.identity;
            Quaternion b = new Quaternion(0, 1, 0, 0);
            Quaternion res = CinematicMath.FastNlerp(a, b, 0f);
            Assert.That(res.w, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastNlerp_Unity_NegativeT_ReturnsA()
        {
            Quaternion a = Quaternion.identity;
            Quaternion b = new Quaternion(0, 1, 0, 0);
            Quaternion res = CinematicMath.FastNlerp(a, b, -0.5f);
            Assert.That(res.w, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastNlerp_Unity_InfinityT_ReturnsA()
        {
            Quaternion a = Quaternion.identity;
            Quaternion b = new Quaternion(0, 1, 0, 0);
            Quaternion res = CinematicMath.FastNlerp(a, b, float.PositiveInfinity);
            Assert.That(res.w, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FastNlerp_Unity_NaNT_ReturnsA()
        {
            Quaternion a = Quaternion.identity;
            Quaternion b = new Quaternion(0, 1, 0, 0);
            Quaternion res = CinematicMath.FastNlerp(a, b, float.NaN);
            Assert.That(res.w, Is.EqualTo(1f).Within(0.001f));
        }
    }
}
