using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BleedStackDecayModelTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = BleedStackDecayModel.Evaluate(10f, 5f, 2f, 20f, 1f);
            Assert.That(result, Is.EqualTo(13f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float result = BleedStackDecayModel.Evaluate(15f, 10f, 1f, 20f, 1f);
            Assert.That(result, Is.EqualTo(19f)); // (15+10) clamps to 20. Then 20 - (1*1) = 19
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = BleedStackDecayModel.Evaluate(0f, 0f, 0f, 0f, 0f);
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = BleedStackDecayModel.Evaluate(-5f, -2f, -1f, -10f, -0.5f);
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float result = BleedStackDecayModel.Evaluate(float.NaN, float.PositiveInfinity, float.NaN, float.NaN, float.NaN);
            Assert.That(result, Is.EqualTo(0f));
        }
    }
}
