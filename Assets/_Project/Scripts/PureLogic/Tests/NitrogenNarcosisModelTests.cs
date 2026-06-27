using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class NitrogenNarcosisModelTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float depth = 40f;
            float time = 60f;
            float onset = 10f;
            float maxImp = 1f;

            // Act
            float result1 = NitrogenNarcosisModel.Evaluate(depth, time, onset, maxImp);
            float result2 = NitrogenNarcosisModel.Evaluate(depth, time * 2, onset, maxImp);

            // Assert
            Assert.That(result1, Is.GreaterThan(0f));
            Assert.That(result2, Is.GreaterThan(result1).Or.EqualTo(maxImp));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange & Act
            float atOnset = NitrogenNarcosisModel.Evaluate(30f, 60f, 30f, 1f);
            float maxClamped = NitrogenNarcosisModel.Evaluate(1000f, 3600f, 30f, 0.001f);

            // Assert
            Assert.That(atOnset, Is.EqualTo(0f), "At onset depth, impairment should be 0.");
            Assert.That(maxClamped, Is.EqualTo(0.001f), "Should clamp to maxImpairment.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange & Act
            float zeroDepth = NitrogenNarcosisModel.Evaluate(0f, 60f, 30f, 1f);
            float zeroTime = NitrogenNarcosisModel.Evaluate(40f, 0f, 30f, 1f);
            float zeroMax = NitrogenNarcosisModel.Evaluate(40f, 60f, 30f, 0f);

            // Assert
            Assert.That(zeroDepth, Is.EqualTo(0f));
            Assert.That(zeroTime, Is.EqualTo(0f));
            Assert.That(zeroMax, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange & Act
            float negDepth = NitrogenNarcosisModel.Evaluate(-10f, 60f, 30f, 1f);
            float negTime = NitrogenNarcosisModel.Evaluate(40f, -60f, 30f, 1f);
            float negMax = NitrogenNarcosisModel.Evaluate(40f, 60f, 30f, -1f);

            // Assert
            Assert.That(negDepth, Is.EqualTo(0f));
            Assert.That(negTime, Is.EqualTo(0f));
            Assert.That(negMax, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange & Act
            float nanDepth = NitrogenNarcosisModel.Evaluate(float.NaN, 60f, 30f, 1f);
            float infDepth = NitrogenNarcosisModel.Evaluate(float.PositiveInfinity, 60f, 30f, 1f);
            float infTime = NitrogenNarcosisModel.Evaluate(40f, float.PositiveInfinity, 30f, 0.8f);

            // Assert
            Assert.That(nanDepth, Is.EqualTo(0f));
            Assert.That(infDepth, Is.EqualTo(1f));
            Assert.That(infTime, Is.EqualTo(0.8f));
        }
    }
}
