using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class GasMixturePartialPressureCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float[] fractions = { 0.5f, 0.5f };
            float[] result = GasMixturePartialPressureCalculator.Compute(100f, fractions);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(50f, result[0], 0.001f);
            Assert.AreEqual(50f, result[1], 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float[] fractions = { 1.0f };
            float[] result = GasMixturePartialPressureCalculator.Compute(100f, fractions);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(100f, result[0], 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float[] fractions = { 0f, 0f };
            float[] result = GasMixturePartialPressureCalculator.Compute(100f, fractions);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(0f, result[0], 0.001f);
            Assert.AreEqual(0f, result[1], 0.001f);

            float[] fractions2 = { 0.5f, 0.5f };
            float[] result2 = GasMixturePartialPressureCalculator.Compute(0f, fractions2);
            Assert.AreEqual(2, result2.Length);
            Assert.AreEqual(0f, result2[0], 0.001f);
            Assert.AreEqual(0f, result2[1], 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float[] fractions = { -0.5f, -0.5f };
            float[] result = GasMixturePartialPressureCalculator.Compute(100f, fractions);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(0f, result[0], 0.001f);
            Assert.AreEqual(0f, result[1], 0.001f);

            float[] fractions2 = { 0.5f, 0.5f };
            float[] result2 = GasMixturePartialPressureCalculator.Compute(-100f, fractions2);
            Assert.AreEqual(2, result2.Length);
            Assert.AreEqual(0f, result2[0], 0.001f);
            Assert.AreEqual(0f, result2[1], 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float[] fractions = { float.PositiveInfinity, float.NaN };
            float[] result = GasMixturePartialPressureCalculator.Compute(100f, fractions);
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(0f, result[0], 0.001f);
            Assert.AreEqual(0f, result[1], 0.001f);

            float[] fractions2 = { 0.5f, 0.5f };
            float[] result2 = GasMixturePartialPressureCalculator.Compute(float.PositiveInfinity, fractions2);
            Assert.AreEqual(2, result2.Length);
            Assert.AreEqual(0f, result2[0], 0.001f);
            Assert.AreEqual(0f, result2[1], 0.001f);
        }
    }
}
