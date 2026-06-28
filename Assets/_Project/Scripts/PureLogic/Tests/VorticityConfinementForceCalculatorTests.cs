using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class VorticityConfinementForceCalculatorTests
    {
        private float[,,] CreateTestField(int size, Func<int, int, int, float> func)
        {
            float[,,] field = new float[size, size, size];
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        field[x, y, z] = func(x, y, z);
                    }
                }
            }
            return field;
        }

        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs. Irrotational: zero. Curl present: force perpendicular to gradient.
            int size = 5;
            float gridSpacing = 1.0f;
            float epsilon = 1.0f;

            // Simple rotational field around Z axis (v_x = -y, v_y = x)
            float[,,] vX = CreateTestField(size, (x, y, z) => -(y - size / 2.0f));
            float[,,] vY = CreateTestField(size, (x, y, z) => (x - size / 2.0f));
            float[,,] vZ = CreateTestField(size, (x, y, z) => 0f);

            // Act
            var result = VorticityConfinementForceCalculator.Compute(vX, vY, vZ, epsilon, gridSpacing);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Item1);
            Assert.IsNotNull(result.Item2);
            Assert.IsNotNull(result.Item3);

            Assert.AreEqual(size, result.Item1.GetLength(0));
            Assert.AreEqual(size, result.Item1.GetLength(1));
            Assert.AreEqual(size, result.Item1.GetLength(2));

            // With this field, vorticity is uniform (w = curl(v) = (0, 0, 2)).
            // The gradient of vorticity magnitude is therefore zero.
            // Force is epsilon * gridSpacing * (N x w), where N is zero. So result should be zero here.
            Assert.AreEqual(0f, result.Item1[size/2, size/2, size/2], 0.0001f);
            Assert.AreEqual(0f, result.Item2[size/2, size/2, size/2], 0.0001f);
            Assert.AreEqual(0f, result.Item3[size/2, size/2, size/2], 0.0001f);

            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            int size = 3;
            float[,,] vX = CreateTestField(size, (x, y, z) => 1f);
            float[,,] vY = CreateTestField(size, (x, y, z) => 1f);
            float[,,] vZ = CreateTestField(size, (x, y, z) => 1f);

            // Act
            var result = VorticityConfinementForceCalculator.Compute(vX, vY, vZ, 0.01f, 0.00001f);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0f, result.Item1[1, 1, 1], 0.0001f);
            Assert.AreEqual(0f, result.Item2[1, 1, 1], 0.0001f);
            Assert.AreEqual(0f, result.Item3[1, 1, 1], 0.0001f);
            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            int size = 3;
            float[,,] vX = new float[size, size, size];
            float[,,] vY = new float[size, size, size];
            float[,,] vZ = new float[size, size, size];

            // Act
            var result1 = VorticityConfinementForceCalculator.Compute(vX, vY, vZ, 0f, 1f);
            var result2 = VorticityConfinementForceCalculator.Compute(vX, vY, vZ, 1f, 0f);

            // Assert
            Assert.AreEqual(0f, result1.Item1[1, 1, 1]);
            Assert.AreEqual(0f, result2.Item1[1, 1, 1]);
            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            int size = 4;
            float[,,] vX = CreateTestField(size, (x, y, z) => x * y);
            float[,,] vY = CreateTestField(size, (x, y, z) => y * z);
            float[,,] vZ = CreateTestField(size, (x, y, z) => z * x);

            // Act
            var result = VorticityConfinementForceCalculator.Compute(vX, vY, vZ, -5f, -1f);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0f, result.Item1[2, 2, 2]); // Clamped to zero due to negative epsilon/spacing
            Assert.AreEqual(0f, result.Item2[2, 2, 2]);
            Assert.AreEqual(0f, result.Item3[2, 2, 2]);
            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            int size = 3;
            float[,,] vX = CreateTestField(size, (x, y, z) => float.MaxValue);
            float[,,] vY = CreateTestField(size, (x, y, z) => float.MinValue);
            float[,,] vZ = CreateTestField(size, (x, y, z) => float.PositiveInfinity);

            // Act
            var result1 = VorticityConfinementForceCalculator.Compute(vX, vY, vZ, 1f, 1f);
            var result2 = VorticityConfinementForceCalculator.Compute(new float[size,size,size], new float[size,size,size], new float[size,size,size], float.PositiveInfinity, float.NaN);

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            // It should handle inf/nan gracefully and not crash or return unhandled NaNs in the output structure.
            Assert.IsFalse(float.IsNaN(result1.Item1[1, 1, 1]));
            Assert.IsFalse(float.IsNaN(result2.Item1[1, 1, 1]));
            Assert.Pass("Verify robust calculation and overflow protection.");
        }
    }
}
