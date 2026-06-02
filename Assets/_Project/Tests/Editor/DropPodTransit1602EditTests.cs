namespace Hecton8.Tests.Editor
{
    using Hecton8.Vehicles.DropPod;
    using NUnit.Framework;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using UnityEngine;

    public sealed class DropPodTransit1602EditTests
    {
        [Test]
        public void DropPodSignalPayloadsStaySixteenBytes()
        {
            Assert.AreEqual(16, UnsafeUtility.SizeOf<DropPodCommandSignal>());
            Assert.AreEqual(16, UnsafeUtility.SizeOf<DropPodStatusSignal>());
        }

        [Test]
        public void CubicBezierHitsAuthoredEndpoints()
        {
            for (int i = 0; i < 1000; i++)
            {
                float f = i * 0.03125f;
                Vector3 start = new Vector3(
                    DropPodSplineMath.ApproxSinBhaskara(f) * 2.1f,
                    DropPodSplineMath.ApproxSinBhaskara(f + 0.73f) * 0.8f,
                    DropPodSplineMath.ApproxSinBhaskara(f + 1.41f) * 1.7f);
                Vector3 end = new Vector3(
                    0.35f + DropPodSplineMath.ApproxSinBhaskara(f + 2.3f) * 0.25f,
                    -0.18f,
                    1.05f + DropPodSplineMath.ApproxSinBhaskara(f + 0.19f) * 0.35f);
                Vector3 a = start + Vector3.up * 0.25f + Vector3.right * 0.18f;
                Vector3 b = end + Vector3.up * 0.12f - Vector3.right * 0.12f;

                Assert.That(
                    Vector3.Distance(start, DropPodSplineMath.ResolveBezierPosition(start, a, b, end, 0f)),
                    Is.LessThanOrEqualTo(0.0001f));
                Assert.That(
                    Vector3.Distance(end, DropPodSplineMath.ResolveBezierPosition(start, a, b, end, 1f)),
                    Is.LessThanOrEqualTo(0.0001f));

                Quaternion startRotation = Quaternion.Euler(f * 3.1f, f * 2.2f, f * 0.7f);
                Quaternion endRotation = Quaternion.Euler(3f + f, -11f + f * 0.5f, 0.4f * f);
                Quaternion resolved = DropPodSplineMath.ResolveNlerp(startRotation, endRotation, 1f);
                Assert.That(math.abs(Quaternion.Dot(resolved, endRotation)), Is.GreaterThan(0.9999f));
            }
        }

        [Test]
        public void TransitEaseIsMonotonic()
        {
            float previous = 0f;
            for (int i = 0; i <= 64; i++)
            {
                float current = DropPodSplineMath.ResolveTransitT(i, 64f);
                Assert.GreaterOrEqual(current, previous);
                previous = current;
            }
        }

        [Test]
        public void NlerpRejectsDegenerateQuaternionBlend()
        {
            Quaternion zero = new Quaternion(0f, 0f, 0f, 0f);
            Quaternion resolved = DropPodSplineMath.ResolveNlerp(zero, zero, 0.5f);
            Assert.That(math.abs(Quaternion.Dot(resolved, Quaternion.identity)), Is.GreaterThan(0.9999f));
        }

        [Test]
        public void LocalEulerRejectsNonFiniteAndBoundsHugeFiniteAuthoring()
        {
            Quaternion nonFinite = DropPodSplineMath.ResolveLocalEulerNoAlloc(new Vector3(float.NaN, 0f, 0f));
            Assert.That(math.abs(Quaternion.Dot(nonFinite, Quaternion.identity)), Is.GreaterThan(0.9999f));

            Quaternion hugeFinite = DropPodSplineMath.ResolveLocalEulerNoAlloc(new Vector3(9999f, -9999f, 9999f));
            Quaternion expected = Quaternion.Euler(360f, -360f, 360f);
            Assert.That(math.abs(Quaternion.Dot(hugeFinite, expected)), Is.GreaterThan(0.9999f));

            Quaternion normal = DropPodSplineMath.ResolveLocalEulerNoAlloc(new Vector3(18f, -86f, 0f));
            Assert.That(math.abs(Quaternion.Dot(normal, Quaternion.Euler(18f, -86f, 0f))), Is.GreaterThan(0.9999f));
        }
    }
}
