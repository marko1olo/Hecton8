using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SaveDataBinaryChecksumCalculator.
    /// Extracted from SaveBinaryPayloadCodec.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SaveDataBinaryChecksumCalculator
    {
        private const uint AdlerModulus = 65521u;
        private const uint AdlerInitialA = 1u;
        private const uint AdlerInitialB = 0u;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="data">Parameter representing the data (byte[]).</param>
        /// <returns>Returns 32-bit checksum hash of type uint.</returns>
        public static uint Compute(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return (AdlerInitialB << 16) | AdlerInitialA;
            }

            uint a = AdlerInitialA;
            uint b = AdlerInitialB;

            // Simple Adler32 implementation
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % AdlerModulus;
                b = (b + a) % AdlerModulus;
            }

            return (b << 16) | a;
        }
    }
}
