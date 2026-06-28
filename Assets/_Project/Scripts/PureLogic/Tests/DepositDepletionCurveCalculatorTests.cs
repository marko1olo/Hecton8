using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class DepositDepletionCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float currentYield = 100f;
            float extractionRate = 5f;
            float depletionExponent = 1f;
            float deltaTime = 1f;

            // Act
            float newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(0f, newYield);

            currentYield = 10f;
            extractionRate = 0.5f;
            depletionExponent = 1f;
            deltaTime = 1f;
            newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);
            Assert.AreEqual(5f, newYield);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs
            float currentYield = 100f;
            float extractionRate = 10f;
            float depletionExponent = 2f;
            float deltaTime = 1f;

            // Act
            float newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);

            // Assert
            Assert.AreEqual(0f, newYield);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            float currentYield = 0f;
            float extractionRate = 10f;
            float depletionExponent = 1f;
            float deltaTime = 1f;

            // Act
            float newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);

            // Assert
            Assert.AreEqual(0f, newYield);

            currentYield = 100f;
            extractionRate = 0f;
            depletionExponent = 1f;
            deltaTime = 1f;
            newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);
            Assert.AreEqual(100f, newYield);

            currentYield = 100f;
            extractionRate = 10f;
            depletionExponent = 0f;
            deltaTime = 1f;
            newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);
            Assert.AreEqual(90f, newYield);

            currentYield = 100f;
            extractionRate = 10f;
            depletionExponent = 1f;
            deltaTime = 0f;
            newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);
            Assert.AreEqual(100f, newYield);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative inputs
            float currentYield = -100f;
            float extractionRate = 10f;
            float depletionExponent = 1f;
            float deltaTime = 1f;

            // Act
            float newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);

            // Assert
            Assert.AreEqual(0f, newYield);

            currentYield = 100f;
            extractionRate = -10f;
            newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);
            Assert.AreEqual(100f, newYield);

            extractionRate = 10f;
            depletionExponent = -1f;
            newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);
            Assert.AreEqual(90f, newYield);

            depletionExponent = 1f;
            deltaTime = -1f;
            newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);
            Assert.AreEqual(100f, newYield);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme values
            float currentYield = float.MaxValue;
            float extractionRate = 10f;
            float depletionExponent = 1f;
            float deltaTime = 1f;

            // Act
            float newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);

            // Assert
            Assert.IsFalse(float.IsNaN(newYield));

            currentYield = 100f;
            extractionRate = float.PositiveInfinity;
            newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);
            Assert.AreEqual(100f, newYield);

            extractionRate = 10f;
            depletionExponent = float.PositiveInfinity;
            newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);
            Assert.AreEqual(90f, newYield);

            depletionExponent = 1f;
            deltaTime = float.PositiveInfinity;
            newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);
            Assert.AreEqual(100f, newYield);

            currentYield = float.NaN;
            newYield = DepositDepletionCurveCalculator.Compute(currentYield, extractionRate, depletionExponent, deltaTime);
            Assert.AreEqual(0f, newYield);
        }
    }
}
