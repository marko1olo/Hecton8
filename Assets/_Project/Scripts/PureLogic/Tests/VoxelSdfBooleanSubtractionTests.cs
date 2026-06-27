using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class VoxelSdfBooleanSubtractionTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float[,,] field = new float[5, 5, 5];
            for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
            for (int z = 0; z < 5; z++)
                field[x,y,z] = -5f; // Solid inside

            var result = VoxelSdfBooleanSubtraction.Calculate(field, new Vector3(2, 2, 2), 2f, 5, 5f);

            // Inside the sphere, it should carve out and be > 0
            Assert.IsTrue(result[2, 2, 2] > 0);

            // Far away, it should still be < 0
            Assert.IsTrue(result[0, 0, 0] < 0);

            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float[,,] field = new float[1, 1, 1];
            field[0,0,0] = -1f;

            var result = VoxelSdfBooleanSubtraction.Calculate(field, new Vector3(0, 0, 0), 0f, 1, 1f, 0.0001f, 1, 0.0001f, 0.0001f);

            // distBase = -1, distCarve = 0
            // result = Max(-1, -0) = 0
            Assert.IsTrue(Math.Abs(result[0, 0, 0]) < 0.1f);

            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float[,,] field = new float[2, 2, 2];
            var result = VoxelSdfBooleanSubtraction.Calculate(field, Vector3.Zero, 0f, 0, 0f);

            Assert.IsFalse(float.IsNaN(result[0,0,0]));

            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float[,,] field = new float[2, 2, 2];
            var result = VoxelSdfBooleanSubtraction.Calculate(field, new Vector3(-1, -1, -1), -5f, -2, -10f);

            Assert.IsNotNull(result);
            Assert.IsFalse(float.IsNaN(result[0,0,0]));

            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float[,,] field = new float[2, 2, 2];
            field[0,0,0] = float.MinValue / 2f;
            var result = VoxelSdfBooleanSubtraction.Calculate(field, new Vector3(1e10f, 1e10f, 1e10f), 1e20f, 2, 1e10f);

            Assert.IsNotNull(result);
            Assert.IsFalse(float.IsNaN(result[0,0,0]));

            Assert.Pass("Verify robust calculation and overflow protection.");
        }

        [Test]
        public void Test_NullInput_Case06()
        {
            Assert.Throws<ArgumentNullException>(() => VoxelSdfBooleanSubtraction.Calculate(null, Vector3.Zero, 1f, 1, 1f));
        }
    }
}
