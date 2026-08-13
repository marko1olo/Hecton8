#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using Unity.Mathematics;
using Hecton8.Power;

namespace Hecton8.Tests.Power
{
    [TestFixture]
    public class PowerGridAupMathTests
    {
        [Test]
        public void DistanceMeters_ValidInputs_ReturnsExpectedDistance()
        {
            double3 aupA = new double3(10.0, 0.0, 0.0);
            double3 aupB = new double3(0.0, 0.0, 0.0);
            double3 origin = new double3(0.0, 0.0, 0.0);

            float distance = PowerGridAupMath.DistanceMeters(aupA, aupB, origin);

            Assert.AreEqual(10f, distance, 0.001f);
        }

        [Test]
        public void DistanceMeters_SamePoints_ReturnsZero()
        {
            double3 aupA = new double3(10.0, 0.0, 0.0);
            double3 origin = new double3(0.0, 0.0, 0.0);

            float distance = PowerGridAupMath.DistanceMeters(aupA, aupA, origin);

            Assert.AreEqual(0f, distance, 0.0001f);
        }

        [Test]
        public void DistanceMeters_TinyDistance_HandlesZeroSafely()
        {
            double3 aupA = new double3(0.0000001, 0.0, 0.0);
            double3 aupB = new double3(0.0, 0.0, 0.0);
            double3 origin = new double3(0.0, 0.0, 0.0);

            float distance = PowerGridAupMath.DistanceMeters(aupA, aupB, origin);

            Assert.AreEqual(0f, distance, 0.0001f);
        }

        [Test]
        public void DistanceMeters_DifferentOrigin_CalculatesCorrectly()
        {
            double3 aupA = new double3(100.0, 50.0, -25.0);
            double3 aupB = new double3(100.0, 60.0, -25.0);
            double3 origin = new double3(50.0, 10.0, 10.0);

            float distance = PowerGridAupMath.DistanceMeters(aupA, aupB, origin);

            Assert.AreEqual(10f, distance, 0.001f);
        }

        [Test]
        public void DistanceMeters_DiagonalDistance_ReturnsCorrectValue()
        {
            double3 aupA = new double3(3.0, 4.0, 0.0);
            double3 aupB = new double3(0.0, 0.0, 0.0);
            double3 origin = new double3(0.0, 0.0, 0.0);

            float distance = PowerGridAupMath.DistanceMeters(aupA, aupB, origin);

            Assert.AreEqual(5f, distance, 0.001f);
        }

        [Test]
        public void DistanceMeters_LargeValues_ReturnsCorrectDistance()
        {
            double3 aupA = new double3(100000.0, 0.0, 0.0);
            double3 aupB = new double3(-100000.0, 0.0, 0.0);
            double3 origin = new double3(0.0, 0.0, 0.0);

            float distance = PowerGridAupMath.DistanceMeters(aupA, aupB, origin);

            Assert.AreEqual(200000f, distance, 0.001f);
        }

        [Test]
        public void DistanceMeters_AllAxesDifference_CalculatesDistance()
        {
            double3 aupA = new double3(10.0, 20.0, 30.0);
            double3 aupB = new double3(-10.0, -20.0, -30.0);
            double3 origin = new double3(5.0, 5.0, 5.0);

            float distance = PowerGridAupMath.DistanceMeters(aupA, aupB, origin);

            Assert.AreEqual(74.83314f, distance, 0.001f);
        }
    }
}
#endif
