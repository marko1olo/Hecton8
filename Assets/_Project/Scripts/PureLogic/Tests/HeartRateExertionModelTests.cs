using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class HeartRateExertionModelTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float currentHR = 80f;
            float exertion = 0.5f;
            float stress = 0.0f;
            float resting = 60f;
            float max = 180f;

            // Target is 120. Diff = +40. adaptationSpeed=1f, dt=0.5f => change = +20
            float result = HeartRateExertionModel.Evaluate(currentHR, exertion, stress, resting, max, 1f, 0.5f);

            Assert.That(result, Is.EqualTo(100f).Within(0.01f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float currentHR = 180f;
            float exertion = 1.0f;
            float stress = 1.0f;
            float resting = 60f;
            float max = 180f;

            // Target is 180.
            float result = HeartRateExertionModel.Evaluate(currentHR, exertion, stress, resting, max, 1f, 0.1f);

            Assert.That(result, Is.EqualTo(180f).Within(0.01f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float currentHR = 60f;
            float exertion = 0f;
            float stress = 0f;
            float resting = 60f;
            float max = 180f;

            float result = HeartRateExertionModel.Evaluate(currentHR, exertion, stress, resting, max, 0f, 0f);

            Assert.That(result, Is.EqualTo(60f).Within(0.01f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Should throw ArgumentOutOfRangeException
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                HeartRateExertionModel.Evaluate(60f, -0.5f, -0.5f, -60f, 180f, 1f, 0.1f);
            });
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                HeartRateExertionModel.Evaluate(float.NaN, float.PositiveInfinity, float.NaN, float.NaN, float.PositiveInfinity, float.PositiveInfinity, float.NaN);
            });
        }
    }
}
