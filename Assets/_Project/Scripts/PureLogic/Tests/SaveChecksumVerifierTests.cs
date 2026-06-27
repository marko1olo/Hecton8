#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SaveChecksumVerifierTests
    {
        private const ulong DefaultHashSeed = 14695981039346656037UL;
        private const ulong DefaultHashPrime1 = 1099511628211UL;
        private const ulong DefaultHashPrime2 = 0xff51afd7ed558ccdUL;
        private const ulong DefaultHashPrime3 = 0xc4ceb9fe1a85ec53UL;
        private const int DefaultShift1 = 32;
        private const int DefaultShift2 = 33;
        private const ulong DefaultZeroFallback = 1UL;

        private uint ComputeExpectedChecksum(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return 0u;

            ulong expectedHash = DefaultHashSeed ^ ((ulong)(uint)payload.Length << DefaultShift1);
            for (int i = 0; i < payload.Length; i++)
            {
                expectedHash ^= payload[i];
                expectedHash *= DefaultHashPrime1;
            }
            expectedHash ^= expectedHash >> DefaultShift2;
            expectedHash *= DefaultHashPrime2;
            expectedHash ^= expectedHash >> DefaultShift2;
            expectedHash *= DefaultHashPrime3;
            expectedHash ^= expectedHash >> DefaultShift2;
            if (expectedHash == 0UL) expectedHash = DefaultZeroFallback;

            return (uint)expectedHash ^ (uint)(expectedHash >> DefaultShift1);
        }

        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            byte[] payload = new byte[] { 1, 2, 3, 4, 5 };
            uint expectedChecksum = ComputeExpectedChecksum(payload);

            // Act
            bool result = SaveChecksumVerifier.Verify(
                payload,
                expectedChecksum,
                DefaultHashSeed,
                DefaultHashPrime1,
                DefaultHashPrime2,
                DefaultHashPrime3,
                DefaultShift1,
                DefaultShift2,
                DefaultZeroFallback);

            // Assert: Verify expected output behaviour
            Assert.IsTrue(result, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            byte[] payload = new byte[] { 0, 255, 127, 128 }; // Boundary byte values
            uint expectedChecksum = ComputeExpectedChecksum(payload);

            // Act
            bool result = SaveChecksumVerifier.Verify(
                payload,
                expectedChecksum,
                DefaultHashSeed,
                DefaultHashPrime1,
                DefaultHashPrime2,
                DefaultHashPrime3,
                DefaultShift1,
                DefaultShift2,
                DefaultZeroFallback);

            // Assert
            Assert.IsTrue(result, "Verify boundary byte values process correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            byte[] payload = new byte[0];
            uint expectedChecksum = 0u; // Empty array should result in 0 checksum validation

            // Act
            bool result = SaveChecksumVerifier.Verify(
                payload,
                expectedChecksum,
                DefaultHashSeed,
                DefaultHashPrime1,
                DefaultHashPrime2,
                DefaultHashPrime3,
                DefaultShift1,
                DefaultShift2,
                DefaultZeroFallback);

            // Assert
            Assert.IsTrue(result, "Verify zero length inputs are handled correctly.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // For byte[], out of bounds means null. We can test null payload here.
            byte[] payload = null;
            uint expectedChecksum = 0u;

            // Act
            bool result = SaveChecksumVerifier.Verify(
                payload,
                expectedChecksum,
                DefaultHashSeed,
                DefaultHashPrime1,
                DefaultHashPrime2,
                DefaultHashPrime3,
                DefaultShift1,
                DefaultShift2,
                DefaultZeroFallback);

            // Assert
            Assert.IsTrue(result, "Verify null payload gracefully returns true for expected zero checksum.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Extremely large array (simulated by random array with size like 100000)
            int size = 100000;
            byte[] payload = new byte[size];
            for(int i = 0; i < size; i++)
            {
                payload[i] = (byte)(i % 256);
            }

            uint expectedChecksum = ComputeExpectedChecksum(payload);

            // Act
            bool result = SaveChecksumVerifier.Verify(
                payload,
                expectedChecksum,
                DefaultHashSeed,
                DefaultHashPrime1,
                DefaultHashPrime2,
                DefaultHashPrime3,
                DefaultShift1,
                DefaultShift2,
                DefaultZeroFallback);

            // Assert
            Assert.IsTrue(result, "Verify robust calculation for large arrays.");

            // Flipped bit invalidation
            payload[0] = 255;
            bool failedResult = SaveChecksumVerifier.Verify(
                payload,
                expectedChecksum,
                DefaultHashSeed,
                DefaultHashPrime1,
                DefaultHashPrime2,
                DefaultHashPrime3,
                DefaultShift1,
                DefaultShift2,
                DefaultZeroFallback);
            Assert.IsFalse(failedResult, "Flipped bit should invalidate checksum.");
        }
    }
}
#endif
