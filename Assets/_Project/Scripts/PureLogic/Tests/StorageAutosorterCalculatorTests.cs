using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class StorageAutosorterCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            int[] itemCategories = { 1, 1, 2 };
            int[] itemWidths = { 2, 1, 2 };
            int[] itemHeights = { 2, 1, 1 };
            int gridWidth = 3;
            int gridHeight = 3;

            // Act
            int[] result = StorageAutosorterCalculator.Compute(itemCategories, itemWidths, itemHeights, gridWidth, gridHeight);

            // Assert
            // Item 0 (cat 1, 2x2) -> index 0 (0,0)
            // Item 1 (cat 1, 1x1) -> index 2 (2,0)
            // Item 2 (cat 2, 2x1) -> index 6 (0,2) or (2,1) or something based on packing
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(3));
            Assert.That(result[0], Is.EqualTo(0));
            Assert.That(result[1], Is.EqualTo(2)); // Place at (2,0) since (0,0) to (1,1) is occupied
            Assert.That(result[2], Is.Not.EqualTo(-1));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            int[] itemCategories = { 1 };
            int[] itemWidths = { 3 };
            int[] itemHeights = { 3 };
            int gridWidth = 2;
            int gridHeight = 2;

            // Act
            int[] result = StorageAutosorterCalculator.Compute(itemCategories, itemWidths, itemHeights, gridWidth, gridHeight);

            // Assert
            Assert.That(result[0], Is.EqualTo(-1), "Item too big should return -1");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            int[] itemCategories = { 1 };
            int[] itemWidths = { 1 };
            int[] itemHeights = { 1 };
            int gridWidth = 0;
            int gridHeight = 0;

            // Act
            int[] result = StorageAutosorterCalculator.Compute(itemCategories, itemWidths, itemHeights, gridWidth, gridHeight);

            // Assert
            Assert.That(result[0], Is.EqualTo(-1));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            int[] itemCategories = { 1, 1 };
            int[] itemWidths = { -1, 1 };
            int[] itemHeights = { -1, 1 };
            int gridWidth = 2;
            int gridHeight = 2;

            // Act
            int[] result = StorageAutosorterCalculator.Compute(itemCategories, itemWidths, itemHeights, gridWidth, gridHeight);

            // Assert
            Assert.That(result[0], Is.EqualTo(-1));
            Assert.That(result[1], Is.EqualTo(0));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            int[] itemCategories = { 1, 2 };
            int[] itemWidths = { 10000, 10000 };
            int[] itemHeights = { 10000, 10000 };
            int gridWidth = 200000;
            int gridHeight = 200000;

            // Act
            int[] result = StorageAutosorterCalculator.Compute(itemCategories, itemWidths, itemHeights, gridWidth, gridHeight);

            // Assert
            Assert.That(result[0], Is.EqualTo(-1));
            Assert.That(result[1], Is.EqualTo(-1));
        }

        [Test]
        public void Test_MalformedInputs_Case06()
        {
            int[] cat = { 1, 2 };
            int[] w = { 1 };
            int[] h = { 1 };
            Assert.Throws<ArgumentException>(() => StorageAutosorterCalculator.Compute(cat, w, h, 2, 2));
        }
    }
}
