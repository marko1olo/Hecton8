using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PowerLoadBalancerTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float supply = 100f;
            float[] demands = { 20f, 50f, 40f };
            int[] priorities = { 1, 3, 2 }; // Higher number = higher priority

            float[] result = PowerLoadBalancer.Calculate(supply, demands, priorities);

            Assert.That(result[0], Is.EqualTo(10f)); // Index 0 (Priority 1) gets remaining 10f
            Assert.That(result[1], Is.EqualTo(50f)); // Index 1 (Priority 3) gets 50f
            Assert.That(result[2], Is.EqualTo(40f)); // Index 2 (Priority 2) gets 40f
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float supply = 50f;
            float[] demands = { 50f, 50f };
            int[] priorities = { 0, 0 };

            float[] result = PowerLoadBalancer.Calculate(supply, demands, priorities);

            Assert.That(result[0], Is.EqualTo(50f));
            Assert.That(result[1], Is.EqualTo(0f)); // Equal priority falls back to index order.
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float supply = 0f;
            float[] demands = { 10f, 0f };
            int[] priorities = { 0, 0 };

            float[] result = PowerLoadBalancer.Calculate(supply, demands, priorities);

            Assert.That(result[0], Is.EqualTo(0f));
            Assert.That(result[1], Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float supply = -50f;
            float[] demands = { -10f, 20f };
            int[] priorities = { 0, 0 };

            float[] result = PowerLoadBalancer.Calculate(supply, demands, priorities);

            Assert.That(result[0], Is.EqualTo(0f));
            Assert.That(result[1], Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float supply = float.PositiveInfinity;
            float[] demands = { float.MaxValue, float.NaN };
            int[] priorities = { 0, 1 };

            float[] result = PowerLoadBalancer.Calculate(supply, demands, priorities);

            Assert.That(result[0], Is.EqualTo(0f)); // Infinity supply -> 0. MaxValue demand is satisfied by 0 supply
            Assert.That(result[1], Is.EqualTo(0f));
        }

        [Test]
        public void Test_Error_ArgumentNullException()
        {
            float supply = 100f;
            float[] demands = { 20f, 50f };
            int[] priorities = { 1, 3 };

            Assert.Throws<ArgumentNullException>(() => PowerLoadBalancer.Calculate(supply, null, priorities));
            Assert.Throws<ArgumentNullException>(() => PowerLoadBalancer.Calculate(supply, demands, null));
            Assert.Throws<ArgumentNullException>(() => PowerLoadBalancer.Calculate(supply, null, null));
        }

        [Test]
        public void Test_Error_ArgumentException_LengthMismatch()
        {
            float supply = 100f;
            float[] demands = { 20f, 50f };
            int[] priorities = { 1, 3, 2 };

            Assert.Throws<ArgumentException>(() => PowerLoadBalancer.Calculate(supply, demands, priorities));
        }
    }
}
