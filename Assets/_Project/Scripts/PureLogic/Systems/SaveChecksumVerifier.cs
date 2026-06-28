using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SaveChecksumVerifier.
    /// Extracted from SaveBinaryStorage.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SaveChecksumVerifier
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="payload">Parameter representing the payload (byte[]).</param>
        /// <param name="storedChecksum">Parameter representing the storedChecksum (uint).</param>
        /// <param name="hashSeed">The initial hash seed.</param>
        /// <param name="hashPrime1">The prime number multiplier used per byte.</param>
        /// <param name="hashPrime2">The first avalanche multiplier.</param>
        /// <param name="hashPrime3">The second avalanche multiplier.</param>
        /// <param name="shift1">The shift amount used to mix length into the initial hash, and for folding.</param>
        /// <param name="shift2">The shift amount used during the avalanche phase.</param>
        /// <param name="zeroFallback">The fallback hash value if the result is zero.</param>
        /// <returns>Returns isValid, uint (computedChecksum) of type bool.</returns>
        public static bool Verify(
            byte[] payload,
            uint storedChecksum,
            ulong hashSeed,
            ulong hashPrime1,
            ulong hashPrime2,
            ulong hashPrime3,
            int shift1,
            int shift2,
            ulong zeroFallback)
        {
            if (payload == null || payload.Length == 0)
                return storedChecksum == 0u;

            ulong hash = hashSeed ^ ((ulong)(uint)payload.Length << shift1);
            for (int i = 0; i < payload.Length; i++)
            {
                hash ^= payload[i];
                hash *= hashPrime1;
            }

            hash ^= hash >> shift2;
            hash *= hashPrime2;
            hash ^= hash >> shift2;
            hash *= hashPrime3;
            hash ^= hash >> shift2;

            if (hash == 0UL)
                hash = zeroFallback;

            uint computedChecksum = (uint)hash ^ (uint)(hash >> shift1);
            return computedChecksum == storedChecksum;
        }
    }
}
