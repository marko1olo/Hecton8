using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FabricationCraftTimeModifierTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = FabricationCraftTimeModifier.Calculate(10f, 0.5f, 0.5f, 2f, 1f, 0.5f, 0.5f, 0f, 0.99f, 0.5f);
            Assert.That(result, Is.EqualTo(10f * 1.5f * 0.75f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float result = FabricationCraftTimeModifier.Calculate(10f, 1f, 1f, 1f, 2f, 0.5f, 0.5f, 0f, 0.99f, 0.5f);
            Assert.That(result, Is.EqualTo(10f * 1f * 0.5f).Within(0.001f));

            float result2 = FabricationCraftTimeModifier.Calculate(0.1f, 1f, 1f, 1f, 2f, 0.5f, 0.5f, 0f, 0.99f, 0.5f);
            Assert.That(result2, Is.EqualTo(2f).Within(0.001f)); // hits min time floor
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = FabricationCraftTimeModifier.Calculate(0f, 0f, 0f, 0f, 1f, 0.5f, 0.5f, 0f, 0.99f, 0.5f);
            Assert.That(result, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = FabricationCraftTimeModifier.Calculate(-5f, -1f, -1f, -2f, -1f, 0.5f, 0.5f, 0.001f, 0.99f, 0.5f);
            Assert.That(result, Is.EqualTo(0.001f).Within(0.001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float result = FabricationCraftTimeModifier.Calculate(float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.NaN, 1f, 0.5f, 0.5f, 0f, 0.99f, 0.5f);
            Assert.That(result, Is.EqualTo(1f).Within(0.001f));

            float result2 = FabricationCraftTimeModifier.Calculate(1e10f, 1f, 1f, 1e5f, 1f, 0.5f, 0.5f, 0f, 0.99f, 0.5f);
            Assert.That(result2, Is.GreaterThan(1e14f));
        }
    }
}
