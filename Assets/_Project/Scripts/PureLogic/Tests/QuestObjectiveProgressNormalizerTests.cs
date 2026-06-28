#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class QuestObjectiveProgressNormalizerTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            Assert.AreEqual(0.5f, QuestObjectiveProgressNormalizer.Normalize(5f, 10f, false));
            Assert.AreEqual(0.2f, QuestObjectiveProgressNormalizer.Normalize(20f, 100f, true));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Assert.AreEqual(1.0f, QuestObjectiveProgressNormalizer.Normalize(10f, 10f, false));
            Assert.AreEqual(1.0f, QuestObjectiveProgressNormalizer.Normalize(15f, 10f, false));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Assert.AreEqual(1.0f, QuestObjectiveProgressNormalizer.Normalize(0f, 0f, false));
            Assert.AreEqual(0.0f, QuestObjectiveProgressNormalizer.Normalize(0f, 10f, false));
            Assert.AreEqual(1.0f, QuestObjectiveProgressNormalizer.Normalize(5f, 0f, false));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Assert.AreEqual(0.0f, QuestObjectiveProgressNormalizer.Normalize(-5f, 10f, false));
            Assert.AreEqual(1.0f, QuestObjectiveProgressNormalizer.Normalize(5f, -10f, false));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Assert.AreEqual(0.0f, QuestObjectiveProgressNormalizer.Normalize(float.NaN, 10f, false));
            Assert.AreEqual(0.0f, QuestObjectiveProgressNormalizer.Normalize(5f, float.NaN, false));
            Assert.AreEqual(1.0f, QuestObjectiveProgressNormalizer.Normalize(float.PositiveInfinity, 10f, false));
            Assert.AreEqual(0.0f, QuestObjectiveProgressNormalizer.Normalize(10f, float.PositiveInfinity, false));
        }
    }
}
#endif
