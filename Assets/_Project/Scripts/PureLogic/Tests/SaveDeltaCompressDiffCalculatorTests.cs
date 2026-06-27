using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SaveDeltaCompressDiffCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            byte[] baseSnapshot = new byte[] { 0, 1, 2, 3, 4, 5 };

            // Identical
            var identicalPatches = SaveDeltaCompressDiffCalculator.Compute(baseSnapshot, baseSnapshot);
            Assert.AreEqual(0, identicalPatches.Length, "Identical snapshots should return zero patches.");

            // One byte changed
            byte[] oneByteChanged = new byte[] { 0, 1, 9, 3, 4, 5 };
            var onePatch = SaveDeltaCompressDiffCalculator.Compute(baseSnapshot, oneByteChanged);
            Assert.AreEqual(1, onePatch.Length, "One byte changed should return one patch.");
            Assert.AreEqual(2, onePatch[0].offset);
            Assert.AreEqual(1, onePatch[0].length);
            Assert.AreEqual(new byte[] { 9 }, onePatch[0].patchData);

            // Multiple changes
            byte[] multipleBytesChanged = new byte[] { 0, 9, 8, 3, 4, 7 };
            var multiPatch = SaveDeltaCompressDiffCalculator.Compute(baseSnapshot, multipleBytesChanged);
            Assert.AreEqual(2, multiPatch.Length, "Multiple non-contiguous changes should return multiple patches.");
            Assert.AreEqual(1, multiPatch[0].offset);
            Assert.AreEqual(2, multiPatch[0].length);
            Assert.AreEqual(new byte[] { 9, 8 }, multiPatch[0].patchData);
            Assert.AreEqual(5, multiPatch[1].offset);
            Assert.AreEqual(1, multiPatch[1].length);
            Assert.AreEqual(new byte[] { 7 }, multiPatch[1].patchData);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            byte[] baseSnapshot = new byte[] { 0, 1, 2, 3 };
            byte[] newLarger = new byte[] { 0, 1, 2, 3, 4, 5 };

            // Act
            var largerPatches = SaveDeltaCompressDiffCalculator.Compute(baseSnapshot, newLarger);

            // Assert
            Assert.AreEqual(1, largerPatches.Length, "New larger snapshot should return an appended patch.");
            Assert.AreEqual(4, largerPatches[0].offset);
            Assert.AreEqual(2, largerPatches[0].length);
            Assert.AreEqual(new byte[] { 4, 5 }, largerPatches[0].patchData);

            byte[] baseLarger = new byte[] { 0, 1, 2, 3, 4, 5 };
            byte[] newSmaller = new byte[] { 0, 1, 2, 3 };
            var smallerPatches = SaveDeltaCompressDiffCalculator.Compute(baseLarger, newSmaller);
            Assert.AreEqual(0, smallerPatches.Length, "New smaller snapshot (with identical prefix) should return no patches, simulating truncation.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            byte[] emptyBase = Array.Empty<byte>();
            byte[] emptyNew = Array.Empty<byte>();

            // Act
            var patches = SaveDeltaCompressDiffCalculator.Compute(emptyBase, emptyNew);

            // Assert
            Assert.AreEqual(0, patches.Length, "Verify zero inputs are handled without divide-by-zero or exception.");

            var emptyBaseToNew = SaveDeltaCompressDiffCalculator.Compute(emptyBase, new byte[] { 1, 2, 3 });
            Assert.AreEqual(1, emptyBaseToNew.Length);
            Assert.AreEqual(0, emptyBaseToNew[0].offset);
            Assert.AreEqual(3, emptyBaseToNew[0].length);
            Assert.AreEqual(new byte[] { 1, 2, 3 }, emptyBaseToNew[0].patchData);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // We use null since arrays don't have negative sizes

            // Act
            var patches1 = SaveDeltaCompressDiffCalculator.Compute(null, new byte[] { 1, 2, 3 });
            var patches2 = SaveDeltaCompressDiffCalculator.Compute(new byte[] { 1, 2, 3 }, null);
            var patches3 = SaveDeltaCompressDiffCalculator.Compute(null, null);

            // Assert
            Assert.AreEqual(1, patches1.Length, "Null base snapshot should be treated as empty array.");
            Assert.AreEqual(new byte[] { 1, 2, 3 }, patches1[0].patchData);

            Assert.AreEqual(0, patches2.Length, "Null new snapshot should return empty patches.");
            Assert.AreEqual(0, patches3.Length, "Both null should return empty patches.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            byte[] largeBase = new byte[100000];
            byte[] largeNew = new byte[100000];

            for (int i = 0; i < largeBase.Length; i++)
            {
                largeBase[i] = (byte)(i % 256);
                largeNew[i] = (byte)(i % 256);
            }

            largeNew[99999] = 255;
            largeNew[50000] = 255;

            // Act
            var patches = SaveDeltaCompressDiffCalculator.Compute(largeBase, largeNew);

            // Assert
            Assert.AreEqual(2, patches.Length, "Verify robust calculation and overflow protection.");
            Assert.AreEqual(50000, patches[0].offset);
            Assert.AreEqual(1, patches[0].length);
            Assert.AreEqual(new byte[] { 255 }, patches[0].patchData);

            Assert.AreEqual(99999, patches[1].offset);
            Assert.AreEqual(1, patches[1].length);
            Assert.AreEqual(new byte[] { 255 }, patches[1].patchData);
        }
    }
}
