using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class StackMergePriorityCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            int[] stackCounts = new int[] { 10, 50, 20, 90 };
            int maxStackSize = 100;
            int quantityToAdd = 130; // 90 gets 10, 50 gets 50, 20 gets 70 (wait, remaining is 130 - 10 - 50 = 70. 20 gets 70 to become 90).

            // Act
            int[] result = StackMergePriorityCalculator.Compute(stackCounts, maxStackSize, quantityToAdd);

            // Assert
            Assert.AreEqual(5, result.Length);
            // 90 gets 10 -> 100
            // 50 gets 50 -> 100
            // 20 gets 70 -> 90
            // 10 gets 0 -> 10
            Assert.AreEqual(10, result[0]);
            Assert.AreEqual(100, result[1]);
            Assert.AreEqual(90, result[2]);
            Assert.AreEqual(100, result[3]);
            Assert.AreEqual(0, result[4]); // remainder
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            int[] stackCounts = new int[] { 100, 100 };
            int maxStackSize = 100;
            int quantityToAdd = 50;

            // Act
            int[] result = StackMergePriorityCalculator.Compute(stackCounts, maxStackSize, quantityToAdd);

            // Assert
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(100, result[0]);
            Assert.AreEqual(100, result[1]);
            Assert.AreEqual(50, result[2]); // All 50 goes to remainder
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            int[] stackCounts = new int[] { 0, 0 };
            int maxStackSize = 0; // Should be clamped to 1
            int quantityToAdd = 0;

            // Act
            int[] result = StackMergePriorityCalculator.Compute(stackCounts, maxStackSize, quantityToAdd);

            // Assert
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(0, result[0]);
            Assert.AreEqual(0, result[1]);
            Assert.AreEqual(0, result[2]); // Remainder 0
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            int[] stackCounts = new int[] { -10, 150 }; // Clamped to 0 and maxStackSize respectively
            int maxStackSize = 100;
            int quantityToAdd = -50; // Clamped to 0

            // Act
            int[] result = StackMergePriorityCalculator.Compute(stackCounts, maxStackSize, quantityToAdd);

            // Assert
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(0, result[0]); // -10 clamped to 0
            Assert.AreEqual(100, result[1]); // 150 clamped to 100
            Assert.AreEqual(0, result[2]); // Remainder 0
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            int[] stackCounts = new int[] { int.MaxValue - 100, 0 };
            int maxStackSize = int.MaxValue;
            int quantityToAdd = 200;

            // Act
            int[] result = StackMergePriorityCalculator.Compute(stackCounts, maxStackSize, quantityToAdd);

            // Assert
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(int.MaxValue, result[0]);
            Assert.AreEqual(100, result[1]); // The remaining 100 goes to the next empty stack
            Assert.AreEqual(0, result[2]); // Remainder 0

            Assert.Throws<ArgumentNullException>(() => { StackMergePriorityCalculator.Compute(null, 10, 10); });
        }
    }
}
