using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SpawnCooldownGateTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            bool canSpawn = SpawnCooldownGate.EvaluateGate(10f, 20f, 5f, 1f, 2f);
            Assert.IsTrue(canSpawn, "Should spawn since 10s elapsed and effective cooldown is 7s.");
            bool cannotSpawn = SpawnCooldownGate.EvaluateGate(10f, 15f, 5f, 1f, 2f);
            Assert.IsFalse(cannotSpawn, "Should not spawn since only 5s elapsed and effective cooldown is 7s.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            bool exactlyOnBoundary = SpawnCooldownGate.EvaluateGate(10f, 17f, 5f, 1f, 2f);
            Assert.IsTrue(exactlyOnBoundary, "Should spawn when exactly at effective cooldown (7s).");
            bool barelyUnder = SpawnCooldownGate.EvaluateGate(10f, 16.99f, 5f, 1f, 2f);
            Assert.IsFalse(barelyUnder, "Should not spawn slightly under effective cooldown.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            bool zeros = SpawnCooldownGate.EvaluateGate(0f, 0f, 0f, 0f, 0f);
            Assert.IsTrue(zeros, "Zero inputs should result in 0 effective cooldown and 0 elapsed, which is >= 0, returning true.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            bool negative = SpawnCooldownGate.EvaluateGate(-5f, -1f, -10f, -2f, -3f);
            Assert.IsTrue(negative, "Negative inputs clamped to 0 should result in true.");

            bool negativeTimeDiff = SpawnCooldownGate.EvaluateGate(20f, 10f, 5f, 0f, 0f);
            Assert.IsFalse(negativeTimeDiff, "Time going backwards should clamp to 0 elapsed, returning false if cooldown > 0.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            bool nanTest = SpawnCooldownGate.EvaluateGate(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.IsTrue(nanTest, "NaN inputs should be sanitized to 0, resulting in true.");

            bool infinityTest = SpawnCooldownGate.EvaluateGate(0f, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);
            Assert.IsFalse(infinityTest, "Max value for cooldown components should prevent spawning.");
        }
    }
}
