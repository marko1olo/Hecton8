using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;
using System.Linq;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SaveMerkleHashNodeCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            byte[] leftChild = new byte[] { 1, 2, 3, 4 };
            byte[] rightChild = new byte[] { 5, 6, 7, 8 };

            // Act
            byte[] result1 = SaveMerkleHashNodeCalculator.Compute(leftChild, rightChild);
            byte[] result2 = SaveMerkleHashNodeCalculator.Compute(leftChild, rightChild);

            // Assert: Verify expected output behaviour
            Assert.IsNotNull(result1);
            Assert.AreEqual(32, result1.Length);
            Assert.IsTrue(result1.SequenceEqual(result2), "Identical inputs should produce identical hashes.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            byte[] emptyLeft = new byte[0];
            byte[] emptyRight = new byte[0];

            // Act
            byte[] result = SaveMerkleHashNodeCalculator.Compute(emptyLeft, emptyRight);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(32, result.Length);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            byte[] nullLeft = null;
            byte[] nullRight = null;

            // Act
            byte[] result = SaveMerkleHashNodeCalculator.Compute(nullLeft, nullRight);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(32, result.Length);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // We use a large input as "out-of-range" for this test, simulating large blobs
            byte[] largeLeft = new byte[100000];
            byte[] largeRight = new byte[100000];
            new Random(1).NextBytes(largeLeft);
            new Random(2).NextBytes(largeRight);

            // Act
            byte[] result = SaveMerkleHashNodeCalculator.Compute(largeLeft, largeRight);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(32, result.Length);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            byte[] normal = new byte[] { 1, 2, 3 };

            // Act
            byte[] resultNullLeft = SaveMerkleHashNodeCalculator.Compute(null, normal);
            byte[] resultNullRight = SaveMerkleHashNodeCalculator.Compute(normal, null);

            // Assert
            Assert.IsNotNull(resultNullLeft);
            Assert.AreEqual(32, resultNullLeft.Length);

            Assert.IsNotNull(resultNullRight);
            Assert.AreEqual(32, resultNullRight.Length);

            Assert.IsFalse(resultNullLeft.SequenceEqual(resultNullRight), "Hash should be order-dependent.");
        }
    }
}
