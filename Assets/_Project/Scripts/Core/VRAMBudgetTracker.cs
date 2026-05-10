using System.Threading;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Register-only estimated VRAM ledger for systems that know their own payload sizes.
    /// </summary>
    public static class VRAMBudgetTracker
    {
        private const int RegistryCapacity = 256;
        private const long WarningThresholdBytes = 1600L * 1024L * 1024L;

        // COLD ALLOC: uint[256] - estimated VRAM owner hashes - owner: VRAMBudgetTracker
        private static readonly uint[] _ownerHashes = new uint[RegistryCapacity];
        // COLD ALLOC: long[256] - estimated VRAM payload bytes - owner: VRAMBudgetTracker
        private static readonly long[] _payloadBytes = new long[RegistryCapacity];

        private static long _estimatedBytes;
        private static int _warningIssued;

        /// <summary>
        /// Current estimated VRAM payload in bytes.
        /// </summary>
        public static long EstimatedVRAMBytes => Interlocked.Read(ref _estimatedBytes);

        /// <summary>
        /// Current estimated VRAM payload in whole megabytes.
        /// </summary>
        public static int EstimatedVRAMMegabytes => (int)(EstimatedVRAMBytes >> 20);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < RegistryCapacity; i++)
            {
                _ownerHashes[i] = 0u;
                _payloadBytes[i] = 0L;
            }

            Interlocked.Exchange(ref _estimatedBytes, 0L);
            Volatile.Write(ref _warningIssued, 0);
        }

        /// <summary>
        /// Registers or updates an owner's estimated VRAM payload.
        /// </summary>
        /// <param name="ownerHash">Stable non-zero owner hash.</param>
        /// <param name="bytes">Estimated payload bytes.</param>
        public static bool RegisterOrUpdate(uint ownerHash, long bytes)
        {
            if (ownerHash == 0u)
                return false;

            long safeBytes = bytes > 0L ? bytes : 0L;
            for (int i = 0; i < RegistryCapacity; i++)
            {
                uint hash = _ownerHashes[i];
                if (hash == ownerHash)
                {
                    long previousBytes = _payloadBytes[i];
                    _payloadBytes[i] = safeBytes;
                    long total = Interlocked.Add(ref _estimatedBytes, safeBytes - previousBytes);
                    CheckWarning(total);
                    return true;
                }

                if (hash != 0u)
                    continue;

                _ownerHashes[i] = ownerHash;
                _payloadBytes[i] = safeBytes;
                CheckWarning(Interlocked.Add(ref _estimatedBytes, safeBytes));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes an owner's estimated VRAM payload.
        /// </summary>
        /// <param name="ownerHash">Stable non-zero owner hash.</param>
        public static bool Unregister(uint ownerHash)
        {
            if (ownerHash == 0u)
                return false;

            for (int i = 0; i < RegistryCapacity; i++)
            {
                if (_ownerHashes[i] != ownerHash)
                    continue;

                long previousBytes = _payloadBytes[i];
                _ownerHashes[i] = 0u;
                _payloadBytes[i] = 0L;
                long total = Interlocked.Add(ref _estimatedBytes, -previousBytes);
                if (total < WarningThresholdBytes)
                    Volatile.Write(ref _warningIssued, 0);

                return true;
            }

            return false;
        }

        private static void CheckWarning(long totalBytes)
        {
            if (totalBytes <= WarningThresholdBytes)
            {
                Volatile.Write(ref _warningIssued, 0);
                return;
            }

            if (Interlocked.CompareExchange(ref _warningIssued, 1, 0) == 0)
                GlobalTelemetryBus.PublishVRAMWarningEvent(totalBytes);
        }
    }
}
