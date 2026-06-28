using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FluidAdvectionStepCalculatorTests
    {
        private float[] _velX;
        private float[] _velY;
        private float[] _velZ;

        [SetUp]
        public void Setup()
        {
            _velX = new float[27];
            _velY = new float[27];
            _velZ = new float[27];

            for (int i = 0; i < 27; i++)
            {
                _velX[i] = 1f;
                _velY[i] = 2f;
                _velZ[i] = -1f;
            }
        }

        [Test]
        public void Test_HappyPath_Case01()
        {
            _velX[0] = 5f;
            _velX[1] = 10f;

            _velX[0] = -1f;
            _velY[0] = 0f;
            _velZ[0] = 0f;

            float result = FluidAdvectionStepCalculator.Compute(_velX, _velY, _velZ, 0, 0, 0, 1f, 1f);

            Assert.AreEqual(10f, result, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            _velX[0] = -100f;
            _velY[0] = 0f;
            _velZ[0] = 0f;

            _velX[2] = 42f;

            float result = FluidAdvectionStepCalculator.Compute(_velX, _velY, _velZ, 0, 0, 0, 1f, 1f);

            Assert.AreEqual(42f, result, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            _velX[0] = 0f;
            _velY[0] = 0f;
            _velZ[0] = 0f;
            float resultZeroVel = FluidAdvectionStepCalculator.Compute(_velX, _velY, _velZ, 0, 0, 0, 0f, 1f);
            Assert.AreEqual(0f, resultZeroVel, 0.001f);

            float resultZeroDuration = FluidAdvectionStepCalculator.Compute(_velX, _velY, _velZ, 1, 1, 1, 0f, 1f);
            Assert.AreEqual(1f, resultZeroDuration, 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float resultNegTime = FluidAdvectionStepCalculator.Compute(_velX, _velY, _velZ, 1, 1, 1, -5f, 1f);
            float resultOOB = FluidAdvectionStepCalculator.Compute(_velX, _velY, _velZ, -1, 1, 1, 1f, 1f);

            Assert.AreEqual(1f, resultNegTime, 0.001f);
            Assert.AreEqual(0f, resultOOB);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            _velX[0] = float.PositiveInfinity;
            float resultInfVel = FluidAdvectionStepCalculator.Compute(_velX, _velY, _velZ, 0, 0, 0, 1f, 1f);
            float resultInfTime = FluidAdvectionStepCalculator.Compute(_velX, _velY, _velZ, 1, 1, 1, float.PositiveInfinity, 1f);
            float resultZeroGrid = FluidAdvectionStepCalculator.Compute(_velX, _velY, _velZ, 1, 1, 1, 1f, 0f);

            Assert.AreEqual(0f, resultInfVel);
            Assert.AreEqual(0f, resultInfTime);
            Assert.AreEqual(0f, resultZeroGrid);
        }
    }
}
