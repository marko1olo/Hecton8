using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ScannerResolutionDepthCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = ScannerResolutionDepthCalculator.ComputeSqr(100f, 10000f, 10f, 90f);

            Assert.That(result, Is.EqualTo(0.9f * 0.9f).Within(0.0001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float resultEdge = ScannerResolutionDepthCalculator.ComputeSqr(10000f, 10000f, 0f, 100f);

            Assert.That(resultEdge, Is.EqualTo(0f));

            float resultZeroNoise = ScannerResolutionDepthCalculator.ComputeSqr(0f, 10000f, 0f, 10f);
            Assert.That(resultZeroNoise, Is.EqualTo(1f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float resultZeroRange = ScannerResolutionDepthCalculator.ComputeSqr(100f, 0f, 10f, 10f);
            Assert.That(resultZeroRange, Is.EqualTo(0f));

            float resultZeroPowerNoise = ScannerResolutionDepthCalculator.ComputeSqr(100f, 10000f, 0f, 0f);

            Assert.That(resultZeroPowerNoise, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float resultNegDist = ScannerResolutionDepthCalculator.ComputeSqr(-10f, 10000f, 10f, 90f);

            Assert.That(resultNegDist, Is.EqualTo(1f * 0.9f).Within(0.0001f));

            float resultNegNoise = ScannerResolutionDepthCalculator.ComputeSqr(0f, 10000f, -50f, 100f);
            Assert.That(resultNegNoise, Is.EqualTo(1f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float resultNan = ScannerResolutionDepthCalculator.ComputeSqr(float.NaN, 10000f, 10f, 10f);
            Assert.That(resultNan, Is.EqualTo(0f));

            float resultInf = ScannerResolutionDepthCalculator.ComputeSqr(100f, 10000f, float.PositiveInfinity, 10f);

            Assert.That(resultInf, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ClampedBounds_Case06()
        {
            // Negative maxScanRange
            float resultNegRange = ScannerResolutionDepthCalculator.Compute(10f, -50f, 10f, 10f);
            Assert.That(resultNegRange, Is.EqualTo(0f));

            // targetDistance greater than maxScanRange
            float resultOutRange = ScannerResolutionDepthCalculator.Compute(150f, 100f, 10f, 10f);
            Assert.That(resultOutRange, Is.EqualTo(0f));

            // targetDistance exactly equal to maxScanRange
            float resultExactRange = ScannerResolutionDepthCalculator.Compute(100f, 100f, 10f, 10f);
            Assert.That(resultExactRange, Is.EqualTo(0f));

            // negative scanner power
            float resultNegPower = ScannerResolutionDepthCalculator.Compute(10f, 100f, 10f, -50f);
            Assert.That(resultNegPower, Is.EqualTo(0f));
        }
    }
}
