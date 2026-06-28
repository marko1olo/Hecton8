using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class InventoryItemDefragmentationConsolidationCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            uint[] ids = { 101, 101, 102, 101 };
            int[] counts = { 5, 5, 20, 2 };
            int[] maxStacks = { 10, 10, 50, 10 };

            // Act
            int[] result = InventoryItemDefragmentationConsolidationCalculator.Compute(ids, counts, maxStacks);

            // Assert: Verify expected output behaviour
            // Slot 0 (101): count 5 + 5 from Slot 1 => 10. Change: +5. Displacement: 0
            // Slot 1 (101): count 5 - 5 to Slot 0 => 0. Change: -5. Displacement: 0 (removed)
            // Slot 2 (102): count 20. Stays same. Gap compaction shifts it to Slot 1. Displacement: 1 - 2 = -1. Change: 0.
            // Slot 3 (101): count 2. Stays same. Gap compaction shifts it to Slot 2. Displacement: 2 - 3 = -1. Change: 0.
            Assert.That(result.Length, Is.EqualTo(8));

            Assert.That(result[0], Is.EqualTo(0)); // Disp Slot 0
            Assert.That(result[1], Is.EqualTo(5)); // Count change Slot 0

            Assert.That(result[2], Is.EqualTo(0)); // Disp Slot 1
            Assert.That(result[3], Is.EqualTo(-5)); // Count change Slot 1

            Assert.That(result[4], Is.EqualTo(-1)); // Disp Slot 2
            Assert.That(result[5], Is.EqualTo(0)); // Count change Slot 2

            Assert.That(result[6], Is.EqualTo(-1)); // Disp Slot 3
            Assert.That(result[7], Is.EqualTo(0)); // Count change Slot 3

            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            uint[] ids = { 101, 101 };
            int[] counts = { 10, 5 };
            int[] maxStacks = { 10, 10 }; // First stack is already full

            // Act
            int[] result = InventoryItemDefragmentationConsolidationCalculator.Compute(ids, counts, maxStacks);

            // Assert
            Assert.That(result[1], Is.EqualTo(0)); // No change in slot 0 count
            Assert.That(result[3], Is.EqualTo(0)); // No change in slot 1 count

            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            uint[] ids = { 0, 101, 0, 101 };
            int[] counts = { 0, 5, 0, 5 };
            int[] maxStacks = { 10, 10, 10, 10 };

            // Act
            int[] result = InventoryItemDefragmentationConsolidationCalculator.Compute(ids, counts, maxStacks);

            // Assert
            Assert.That(result[2], Is.EqualTo(-1)); // Disp Slot 1
            Assert.That(result[3], Is.EqualTo(5)); // Change Slot 1

            Assert.That(result[7], Is.EqualTo(-5)); // Change Slot 3

            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            uint[] ids = { 101, 101 };
            int[] counts = { -5, 10 }; // Negative count
            int[] maxStacks = { 0, -5 }; // Invalid max stacks

            // Act
            int[] result = InventoryItemDefragmentationConsolidationCalculator.Compute(ids, counts, maxStacks);

            // Assert
            Assert.That(result[1], Is.EqualTo(0)); // Change for slot 0 (0-0=0)
            Assert.That(result[2], Is.EqualTo(-1)); // Disp for slot 1
            Assert.That(result[3], Is.EqualTo(0)); // Change for slot 1

            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            uint[] ids = { 101, 101 };
            int[] counts = { int.MaxValue - 100, 200 };
            int[] maxStacks = { int.MaxValue, int.MaxValue };

            // Act
            int[] result = InventoryItemDefragmentationConsolidationCalculator.Compute(ids, counts, maxStacks);

            // Assert
            Assert.That(result[1], Is.EqualTo(100)); // Slot 0 gained 100
            Assert.That(result[3], Is.EqualTo(-100)); // Slot 1 lost 100

            Assert.Pass("Verify robust calculation and overflow protection.");
        }
    }
}
