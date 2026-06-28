using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SaveDeltaVoxelStatePackingCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            byte[] original = new byte[] { 1, 2, 3, 4, 5 };
            byte[] modified = new byte[] { 1, 9, 3, 4, 8 };

            // Act
            byte[] delta = SaveDeltaVoxelStatePackingCalculator.Compute(original, modified);

            // Assert: Verify expected output behaviour
            // Expecting 2 changes: index 1 and index 4. Each change is 5 bytes. Total 10 bytes.
            Assert.That(delta.Length, Is.EqualTo(10));

            // First change: index 1, value 9
            Assert.That(delta[0], Is.EqualTo(1));
            Assert.That(delta[1], Is.EqualTo(0));
            Assert.That(delta[2], Is.EqualTo(0));
            Assert.That(delta[3], Is.EqualTo(0));
            Assert.That(delta[4], Is.EqualTo(9));

            // Second change: index 4, value 8
            Assert.That(delta[5], Is.EqualTo(4));
            Assert.That(delta[6], Is.EqualTo(0));
            Assert.That(delta[7], Is.EqualTo(0));
            Assert.That(delta[8], Is.EqualTo(0));
            Assert.That(delta[9], Is.EqualTo(8));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // original equals modified
            byte[] original = new byte[] { 10, 20, 30 };
            byte[] modified = new byte[] { 10, 20, 30 };

            // Act
            byte[] delta = SaveDeltaVoxelStatePackingCalculator.Compute(original, modified);

            // Assert
            Assert.That(delta.Length, Is.EqualTo(0));
            Assert.That(delta, Is.EqualTo(Array.Empty<byte>()));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Empty arrays
            byte[] original = new byte[0];
            byte[] modified = new byte[0];

            // Act
            byte[] delta = SaveDeltaVoxelStatePackingCalculator.Compute(original, modified);

            // Assert
            Assert.That(delta.Length, Is.EqualTo(0));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Mismatched lengths
            byte[] original = new byte[] { 1, 2 };
            byte[] modified = new byte[] { 1, 2, 3 };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => SaveDeltaVoxelStatePackingCalculator.Compute(original, modified));
            Assert.Throws<ArgumentNullException>(() => SaveDeltaVoxelStatePackingCalculator.Compute(null, modified));
            Assert.Throws<ArgumentNullException>(() => SaveDeltaVoxelStatePackingCalculator.Compute(original, null));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // All values changed
            int size = 1000;
            byte[] original = new byte[size];
            byte[] modified = new byte[size];
            for (int i = 0; i < size; i++)
            {
                original[i] = 0;
                modified[i] = 255;
            }

            // Act
            byte[] delta = SaveDeltaVoxelStatePackingCalculator.Compute(original, modified);

            // Assert
            Assert.That(delta.Length, Is.EqualTo(size * 5));
            // Check the last one
            int lastChangeIndex = (size - 1) * 5;
            int expectedIndex = size - 1; // 999

            Assert.That(delta[lastChangeIndex], Is.EqualTo(expectedIndex & 0xFF));
            Assert.That(delta[lastChangeIndex + 1], Is.EqualTo((expectedIndex >> 8) & 0xFF));
            Assert.That(delta[lastChangeIndex + 4], Is.EqualTo(255));
        }
    }
}
