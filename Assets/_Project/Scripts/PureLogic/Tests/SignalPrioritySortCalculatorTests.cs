using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SignalPrioritySortCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Higher priority wins
            Assert.That(SignalPrioritySortCalculator.Compute(10, 100L, 5, 100L), Is.EqualTo(-1));
            Assert.That(SignalPrioritySortCalculator.Compute(5, 100L, 10, 100L), Is.EqualTo(1));

            // Same priority, earlier timestamp wins
            Assert.That(SignalPrioritySortCalculator.Compute(10, 100L, 10, 200L), Is.EqualTo(-1));
            Assert.That(SignalPrioritySortCalculator.Compute(10, 200L, 10, 100L), Is.EqualTo(1));

            // Same priority, same timestamp
            Assert.That(SignalPrioritySortCalculator.Compute(10, 100L, 10, 100L), Is.EqualTo(0));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Testing boundary integers and longs
            Assert.That(SignalPrioritySortCalculator.Compute(int.MaxValue, long.MaxValue, int.MaxValue, long.MaxValue), Is.EqualTo(0));
            Assert.That(SignalPrioritySortCalculator.Compute(int.MinValue, long.MinValue, int.MinValue, long.MinValue), Is.EqualTo(0));

            Assert.That(SignalPrioritySortCalculator.Compute(int.MaxValue, long.MaxValue, int.MinValue, long.MaxValue), Is.EqualTo(-1));
            Assert.That(SignalPrioritySortCalculator.Compute(int.MinValue, long.MaxValue, int.MaxValue, long.MaxValue), Is.EqualTo(1));

            Assert.That(SignalPrioritySortCalculator.Compute(0, long.MinValue, 0, long.MaxValue), Is.EqualTo(-1));
            Assert.That(SignalPrioritySortCalculator.Compute(0, long.MaxValue, 0, long.MinValue), Is.EqualTo(1));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Assert.That(SignalPrioritySortCalculator.Compute(0, 0L, 0, 0L), Is.EqualTo(0));
            Assert.That(SignalPrioritySortCalculator.Compute(1, 0L, 0, 0L), Is.EqualTo(-1));
            Assert.That(SignalPrioritySortCalculator.Compute(0, 0L, 1, 0L), Is.EqualTo(1));
            Assert.That(SignalPrioritySortCalculator.Compute(0, 0L, 0, 1L), Is.EqualTo(-1));
            Assert.That(SignalPrioritySortCalculator.Compute(0, 1L, 0, 0L), Is.EqualTo(1));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Assert.That(SignalPrioritySortCalculator.Compute(-5, -100L, -5, -100L), Is.EqualTo(0));
            Assert.That(SignalPrioritySortCalculator.Compute(-5, -200L, -5, -100L), Is.EqualTo(-1)); // -200 is earlier than -100
            Assert.That(SignalPrioritySortCalculator.Compute(-5, -100L, -5, -200L), Is.EqualTo(1));
            Assert.That(SignalPrioritySortCalculator.Compute(-1, -100L, -5, -100L), Is.EqualTo(-1));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Assert.That(SignalPrioritySortCalculator.Compute(int.MaxValue, long.MinValue, int.MinValue, long.MaxValue), Is.EqualTo(-1));
            Assert.That(SignalPrioritySortCalculator.Compute(int.MinValue, long.MaxValue, int.MaxValue, long.MinValue), Is.EqualTo(1));
        }
    }
}
