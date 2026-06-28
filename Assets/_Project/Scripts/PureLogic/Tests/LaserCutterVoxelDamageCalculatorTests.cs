using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class LaserCutterVoxelDamageCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = LaserCutterVoxelDamageCalculator.Compute(100f, 2f, 10f);
            Assert.That(result, Is.GreaterThan(0f));
            // 100 * (1 / 4) = 25. 25 - 10 = 15.
            Assert.That(result, Is.EqualTo(15f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // hardness exactly absorbs heat
            float result = LaserCutterVoxelDamageCalculator.Compute(40f, 2f, 10f); // 40 / 4 = 10, 10 - 10 = 0
            Assert.That(result, Is.EqualTo(0f));

            // distance < 1 clamps to 1
            float resultClose = LaserCutterVoxelDamageCalculator.Compute(100f, 0.5f, 10f);
            Assert.That(resultClose, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(0f, 10f, 5f), Is.EqualTo(0f));
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(100f, 0f, 5f), Is.EqualTo(95f)); // 100/1 - 5
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(100f, 10f, 0f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(-10f, 5f, 5f), Is.EqualTo(0f));
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(100f, -5f, 5f), Is.EqualTo(0f));
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(100f, 5f, -5f), Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(float.MaxValue, 1f, 0f), Is.GreaterThan(0f));
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(100f, float.MaxValue, 0f), Is.EqualTo(0f));
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(float.NaN, 1f, 1f), Is.EqualTo(0f));
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(10f, float.NaN, 1f), Is.EqualTo(0f));
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(10f, 1f, float.NaN), Is.EqualTo(0f));
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(float.PositiveInfinity, 1f, 1f), Is.EqualTo(0f));
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(10f, float.PositiveInfinity, 1f), Is.EqualTo(0f));
            Assert.That(LaserCutterVoxelDamageCalculator.Compute(10f, 1f, float.PositiveInfinity), Is.EqualTo(0f));
        }
    }
}
