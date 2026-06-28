using NUnit.Framework;
using System;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SymbiosisBenefitMatrixCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Mutualism()
        {
            // Arrange: Setup standard test inputs (Isolated: mutualist pair)
            float[] pops = { 10f, 20f };
            float[,] interaction = {
                { 0.1f, 0.5f }, // Species 0 benefits from Species 1
                { 0.5f, 0.2f }  // Species 1 benefits from Species 0
            };

            // Act
            float[] result = SymbiosisBenefitMatrixCalculator.Compute(pops, interaction);

            // Assert: Verify expected output behaviour
            // Sp 0: (0.1 * 10 * 10) + (0.5 * 20 * 10) = 10 + 100 = 110
            Assert.AreEqual(110f, result[0], 0.001f);
            // Sp 1: (0.5 * 10 * 20) + (0.2 * 20 * 20) = 100 + 80 = 180
            Assert.AreEqual(180f, result[1], 0.001f);
        }

        [Test]
        public void Test_HappyPath_Parasitism()
        {
            // Arrange: Setup standard test inputs (Parasite-host: parasite gains, host loses)
            float[] pops = { 50f, 10f }; // 0: Host, 1: Parasite
            float[,] interaction = {
                { 0.1f, -0.5f }, // Host harmed by Parasite
                { 0.5f, 0.0f }   // Parasite benefits from Host
            };

            // Act
            float[] result = SymbiosisBenefitMatrixCalculator.Compute(pops, interaction);

            // Assert
            // Host: (0.1 * 50 * 50) + (-0.5 * 10 * 50) = 250 - 250 = 0
            Assert.AreEqual(0f, result[0], 0.001f);
            // Parasite: (0.5 * 50 * 10) + (0.0 * 10 * 10) = 250 + 0 = 250
            Assert.AreEqual(250f, result[1], 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (Single species, empty)
            float[] emptyPops = new float[0];
            float[,] emptyMatrix = new float[0, 0];

            // Act
            float[] emptyResult = SymbiosisBenefitMatrixCalculator.Compute(emptyPops, emptyMatrix);

            // Assert
            Assert.AreEqual(0, emptyResult.Length);

            // Single species
            float[] singlePops = { 10f };
            float[,] singleMatrix = { { 0.5f } };
            float[] singleResult = SymbiosisBenefitMatrixCalculator.Compute(singlePops, singleMatrix);
            Assert.AreEqual(50f, singleResult[0], 0.001f); // 0.5 * 10 * 10 = 50
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            float[] pops = { 0f, 10f };
            float[,] interaction = {
                { 0.5f, 0.5f },
                { 0.5f, 0.5f }
            };

            // Act
            float[] result = SymbiosisBenefitMatrixCalculator.Compute(pops, interaction);

            // Assert
            Assert.AreEqual(0f, result[0]); // Pop is 0, benefit should be 0
            Assert.AreEqual(50f, result[1], 0.001f); // 0.5*0*10 + 0.5*10*10 = 50
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float[] pops = { -10f, 20f }; // Negative population should be clamped to 0
            float[,] interaction = {
                { 0.5f, 0.5f },
                { 0.5f, 0.5f }
            };

            // Act
            float[] result = SymbiosisBenefitMatrixCalculator.Compute(pops, interaction);

            // Assert
            Assert.AreEqual(0f, result[0]); // Pop clamped to 0
            Assert.AreEqual(200f, result[1], 0.001f); // 0.5*0*20 + 0.5*20*20 = 200
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float[] pops = { float.PositiveInfinity, float.NaN, 10f };
            float[,] interaction = {
                { 1f, 1f, 1f },
                { 1f, float.PositiveInfinity, 1f },
                { 1f, float.NaN, 1f }
            };

            // Act
            float[] result = SymbiosisBenefitMatrixCalculator.Compute(pops, interaction);

            // Assert
            // Infinity/NaN populations clamped to 0
            Assert.AreEqual(0f, result[0]);
            Assert.AreEqual(0f, result[1]);
            // Sp 2: interaction with 0 and 1 are 0, interaction with 2 is 1. (1 * 10 * 10 = 100)
            Assert.AreEqual(100f, result[2], 0.001f);
        }

        [Test]
        public void Test_InvalidDimensions_ThrowsArgumentException()
        {
            // Arrange
            float[] pops = { 10f, 20f };
            float[,] interaction = {
                { 1f, 1f, 1f },
                { 1f, 1f, 1f }
            }; // 2x3 matrix for 2 populations

            // Act & Assert
            Assert.Throws<ArgumentException>(() => SymbiosisBenefitMatrixCalculator.Compute(pops, interaction));

            float[,] nonSquare = {
                { 1f, 1f },
                { 1f, 1f },
                { 1f, 1f }
            }; // 3x2 matrix
            Assert.Throws<ArgumentException>(() => SymbiosisBenefitMatrixCalculator.Compute(pops, nonSquare));
        }

        [Test]
        public void Test_NullInputs_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SymbiosisBenefitMatrixCalculator.Compute(null, new float[2,2]));
            Assert.Throws<ArgumentNullException>(() => SymbiosisBenefitMatrixCalculator.Compute(new float[2], null));
        }
    }
}
