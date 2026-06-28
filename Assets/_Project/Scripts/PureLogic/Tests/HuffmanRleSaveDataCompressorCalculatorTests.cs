using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class HuffmanRleSaveDataCompressorCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs (e.g. AAABBBCC)
            byte[] uncompressedData = new byte[] { 65, 65, 65, 66, 66, 66, 67, 67 };

            // Expected output:
            // 65, 3, 0 (A x 3)
            // 66, 3, 0 (B x 3)
            // 67, 2, 0 (C x 2)
            byte[] expectedData = new byte[] { 65, 3, 0, 66, 3, 0, 67, 2, 0 };

            // Act
            byte[] result = HuffmanRleSaveDataCompressorCalculator.Compute(uncompressedData);

            // Assert: Verify expected output behaviour
            Assert.That(result, Is.EqualTo(expectedData));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // ushort.MaxValue is 65535.
            // If we have an array of 65536 identical bytes, it should split into two runs.
            // Run 1: length 65535 (0xFFFF)
            // Run 2: length 1 (0x0001)
            byte[] uncompressedData = new byte[65536];
            Array.Fill(uncompressedData, (byte)42);

            byte[] expectedData = new byte[] {
                42, 255, 255, // 42 x 65535 (255 is 0xFF)
                42, 1, 0      // 42 x 1
            };

            // Act
            byte[] result = HuffmanRleSaveDataCompressorCalculator.Compute(uncompressedData);

            // Assert
            Assert.That(result, Is.EqualTo(expectedData));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            byte[] uncompressedDataNull = null;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            byte[] uncompressedDataEmpty = Array.Empty<byte>();

            // Act
#pragma warning disable CS8604 // Possible null reference argument.
            byte[] resultNull = HuffmanRleSaveDataCompressorCalculator.Compute(uncompressedDataNull);
#pragma warning restore CS8604 // Possible null reference argument.
            byte[] resultEmpty = HuffmanRleSaveDataCompressorCalculator.Compute(uncompressedDataEmpty);

            // Assert
            Assert.That(resultNull, Is.Not.Null);
            Assert.That(resultNull.Length, Is.EqualTo(0));

            Assert.That(resultEmpty, Is.Not.Null);
            Assert.That(resultEmpty.Length, Is.EqualTo(0));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // For byte arrays, negative values are not possible directly in the array, but we can test
            // byte boundaries like 0 and 255 to ensure they are compressed correctly.
            byte[] uncompressedData = new byte[] { 0, 0, 255, 255, 255, 0, 255 };

            byte[] expectedData = new byte[] {
                0, 2, 0,
                255, 3, 0,
                0, 1, 0,
                255, 1, 0
            };

            // Act
            byte[] result = HuffmanRleSaveDataCompressorCalculator.Compute(uncompressedData);

            // Assert
            Assert.That(result, Is.EqualTo(expectedData));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Test a massive array of alternating single bytes (worst case scenario for RLE)
            int size = 10000;
            byte[] uncompressedData = new byte[size];
            for (int i = 0; i < size; i++)
            {
                uncompressedData[i] = (byte)(i % 256);
            }

            // In the worst case, each byte takes 3 bytes to encode (value, 1, 0)
            int expectedSize = size * 3;

            // Act
            byte[] result = HuffmanRleSaveDataCompressorCalculator.Compute(uncompressedData);

            // Assert
            Assert.That(result.Length, Is.EqualTo(expectedSize));
            Assert.That(result[0], Is.EqualTo(0));
            Assert.That(result[1], Is.EqualTo(1));
            Assert.That(result[2], Is.EqualTo(0));

            Assert.That(result[3], Is.EqualTo(1));
            Assert.That(result[4], Is.EqualTo(1));
            Assert.That(result[5], Is.EqualTo(0));
        }

        [Test]
        public void Test_VerifyCompressionRatio()
        {
            // Arrange: Verify massive arrays of identical bytes compress to < 1% size. Lossless recovery.
            int massiveSize = 1024 * 1024; // 1 MB
            byte[] uncompressedData = new byte[massiveSize];
            Array.Fill(uncompressedData, (byte)99);

            // Act
            byte[] result = HuffmanRleSaveDataCompressorCalculator.Compute(uncompressedData);

            // Assert
            // Compression ratio should be extremely small
            double compressionRatio = (double)result.Length / uncompressedData.Length;
            Assert.That(compressionRatio, Is.LessThan(0.01));

            // Expected output:
            // 1MB = 1048576 bytes
            // 1048576 / 65535 = 16 runs of 65535
            // 1048576 % 65535 = 1 run of 16
            // Total 17 runs -> 17 * 3 = 51 bytes
            Assert.That(result.Length, Is.EqualTo(51));
        }
    }
}
