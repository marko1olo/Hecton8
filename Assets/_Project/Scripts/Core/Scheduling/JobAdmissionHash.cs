using System;
using System.Runtime.CompilerServices;

namespace Hecton8.Core.Scheduling
{
    /// <summary>
    /// FNV1a job type hash helpers. Generic static storage makes the type-name walk cold per job struct.
    /// </summary>
    public static class JobAdmissionHash<TJob>
        where TJob : struct
    {
        /// <summary>Stable 32-bit FNV1a hash of the job struct type name.</summary>
        public static readonly uint Value = ComputeTypeHash();

        private static uint ComputeTypeHash()
        {
            string typeName = typeof(TJob).FullName;
            return JobAdmissionHash.ComputeFnv1a(typeName);
        }
    }

    /// <summary>
    /// FNV1a hash implementation shared by diagnostics and wrappers.
    /// </summary>
    public static class JobAdmissionHash
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>Computes a stable FNV1a hash for a managed type name on cold paths.</summary>
        /// <param name="text">Type name.</param>
        /// <returns>Non-zero hash.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeFnv1a(string text)
        {
            uint hash = FnvOffset;
            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= FnvPrime;
                }
            }

            return hash == 0u ? 1u : hash;
        }
    }
}
