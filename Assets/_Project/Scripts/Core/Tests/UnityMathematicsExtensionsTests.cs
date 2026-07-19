using NUnit.Framework;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Tests
{
    [TestFixture]
    public class UnityMathematicsExtensionsTests
    {
        // ==========================================
        // Vector3 -> float3 Tests
        // ==========================================

        [Test]
        public void ToFloat3_Vector3_HappyPath()
        {
            Vector3 input = new Vector3(1.5f, 2.75f, 3.125f);
            float3 result = UnityMathematicsExtensions.ToFloat3(input);

            Assert.That(result.x, Is.EqualTo(1.5f));
            Assert.That(result.y, Is.EqualTo(2.75f));
            Assert.That(result.z, Is.EqualTo(3.125f));
        }

        [Test]
        public void ToFloat3_Vector3_ZeroInputs()
        {
            Vector3 input = Vector3.zero;
            float3 result = UnityMathematicsExtensions.ToFloat3(input);

            Assert.That(result.x, Is.EqualTo(0f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(0f));
        }

        [Test]
        public void ToFloat3_Vector3_NegativeInputs()
        {
            Vector3 input = new Vector3(-1.1f, -2.2f, -3.3f);
            float3 result = UnityMathematicsExtensions.ToFloat3(input);

            Assert.That(result.x, Is.EqualTo(-1.1f));
            Assert.That(result.y, Is.EqualTo(-2.2f));
            Assert.That(result.z, Is.EqualTo(-3.3f));
        }

        [Test]
        public void ToFloat3_Vector3_Extremes()
        {
            Vector3 input = new Vector3(float.MinValue, float.MaxValue, float.Epsilon);
            float3 result = UnityMathematicsExtensions.ToFloat3(input);

            Assert.That(result.x, Is.EqualTo(float.MinValue));
            Assert.That(result.y, Is.EqualTo(float.MaxValue));
            Assert.That(result.z, Is.EqualTo(float.Epsilon));
        }

        [Test]
        public void ToFloat3_Vector3_InfinityAndNaN()
        {
            Vector3 input = new Vector3(float.PositiveInfinity, float.NegativeInfinity, float.NaN);
            float3 result = UnityMathematicsExtensions.ToFloat3(input);

            Assert.That(result.x, Is.EqualTo(float.PositiveInfinity));
            Assert.That(result.y, Is.EqualTo(float.NegativeInfinity));
            Assert.That(float.IsNaN(result.z), Is.True);
        }

        // ==========================================
        // double3 -> float3 Tests
        // ==========================================

        [Test]
        public void ToFloat3_Double3_HappyPath()
        {
            double3 input = new double3(1.5, 2.75, 3.125);
            float3 result = UnityMathematicsExtensions.ToFloat3(input);

            Assert.That(result.x, Is.EqualTo(1.5f));
            Assert.That(result.y, Is.EqualTo(2.75f));
            Assert.That(result.z, Is.EqualTo(3.125f));
        }

        [Test]
        public void ToFloat3_Double3_ZeroInputs()
        {
            double3 input = new double3(0.0, 0.0, 0.0);
            float3 result = UnityMathematicsExtensions.ToFloat3(input);

            Assert.That(result.x, Is.EqualTo(0f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(0f));
        }

        [Test]
        public void ToFloat3_Double3_NegativeInputs()
        {
            double3 input = new double3(-1.1, -2.2, -3.3);
            float3 result = UnityMathematicsExtensions.ToFloat3(input);

            // Need to account for potential precision loss in conversion from double to float,
            // but for exact representable floats or very close approximations, this should match.
            Assert.That(result.x, Is.EqualTo((float)-1.1));
            Assert.That(result.y, Is.EqualTo((float)-2.2));
            Assert.That(result.z, Is.EqualTo((float)-3.3));
        }

        [Test]
        public void ToFloat3_Double3_Extremes()
        {
            // Note: float.MaxValue and float.MinValue are well within double range.
            // Using values that fit in float to ensure round-trip comparison makes sense,
            // or values larger than float to test infinity conversion.
            double3 input = new double3(double.MaxValue, double.MinValue, double.Epsilon);
            float3 result = UnityMathematicsExtensions.ToFloat3(input);

            // double.MaxValue -> float.PositiveInfinity
            // double.MinValue -> float.NegativeInfinity
            // double.Epsilon -> 0f (underflow for float)
            Assert.That(result.x, Is.EqualTo(float.PositiveInfinity));
            Assert.That(result.y, Is.EqualTo(float.NegativeInfinity));
            Assert.That(result.z, Is.EqualTo(0f));
        }

        [Test]
        public void ToFloat3_Double3_InfinityAndNaN()
        {
            double3 input = new double3(double.PositiveInfinity, double.NegativeInfinity, double.NaN);
            float3 result = UnityMathematicsExtensions.ToFloat3(input);

            Assert.That(result.x, Is.EqualTo(float.PositiveInfinity));
            Assert.That(result.y, Is.EqualTo(float.NegativeInfinity));
            Assert.That(float.IsNaN(result.z), Is.True);
        }
    }
}
