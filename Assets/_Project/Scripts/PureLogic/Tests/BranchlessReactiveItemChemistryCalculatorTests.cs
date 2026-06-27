using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BranchlessReactiveItemChemistryCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            uint radioMask = 1u;
            uint flamMask = 2u;
            uint matrix = radioMask | flamMask;

            // Act
            uint result = BranchlessReactiveItemChemistryCalculator.Compute(radioMask, flamMask, matrix);

            // Assert
            Assert.AreEqual(matrix, result, "Radioactive and flammable items should cross-react.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            uint matrix = 3u;

            // Act
            uint result = BranchlessReactiveItemChemistryCalculator.Compute(1u, 1u, matrix);

            // Assert
            Assert.AreEqual(0u, result, "Items with identical single reactive properties should be inert.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            uint matrix = 3u;

            // Act
            uint result1 = BranchlessReactiveItemChemistryCalculator.Compute(0u, 0u, matrix);
            uint result2 = BranchlessReactiveItemChemistryCalculator.Compute(1u, 0u, matrix);

            // Assert
            Assert.AreEqual(0u, result1, "Zero flags should not react.");
            Assert.AreEqual(0u, result2, "One zero flag should not react.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            uint allBits = uint.MaxValue;
            uint matrix = 3u;

            // Act
            uint result1 = BranchlessReactiveItemChemistryCalculator.Compute(allBits, 1u, matrix);
            uint result2 = BranchlessReactiveItemChemistryCalculator.Compute(allBits, allBits, matrix);

            // Assert
            Assert.AreEqual(3u, result1, "All bits flag should react with a single property flag within the matrix.");
            Assert.AreEqual(3u, result2, "Two all-bits flags should react with each other because they trigger multiple matrix properties.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            uint extremeMatrix = 0xFFFFFFFF;

            // Act
            uint result1 = BranchlessReactiveItemChemistryCalculator.Compute(0x000000FF, 0xFF000000, extremeMatrix);
            uint result2 = BranchlessReactiveItemChemistryCalculator.Compute(0x80000000, 0x80000000, extremeMatrix);

            // Assert
            Assert.AreEqual(0xFF0000FF, result1, "Completely orthogonal extreme values should react.");
            Assert.AreEqual(0u, result2, "Identical extreme single-bit values should NOT react.");
        }
    }
}
