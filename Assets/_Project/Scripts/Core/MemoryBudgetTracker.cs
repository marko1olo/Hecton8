using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Cold-path tracker for persistent native allocations that enforces owner budgets and warns on overrun.
    /// </summary>
    internal static class MemoryBudgetTracker
    {
        private struct BudgetRecord
        {
            public string OwnerName;
            public long TotalBytes;
            public long BudgetBytes;
            public bool WarningIssued;
        }

        // COLD ALLOC: Dictionary<int,BudgetRecord>[32] - persistent native allocation budget registry - owner: MemoryBudgetTracker
        private static readonly Dictionary<int, BudgetRecord> _records = new Dictionary<int, BudgetRecord>(32);
        private const int OwnerCollisionProbeLimit = 16;
        private const int GateSpinLimit = 128;
        private static int _recordGate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _recordGate = 0;
            _records.Clear();
        }

        public static void Register(string ownerName, long totalBytes, long budgetBytes)
        {
            if (string.IsNullOrEmpty(ownerName) || budgetBytes <= 0L)
                return;

            int ownerKey = StableHash(ownerName);
            bool shouldWarn = false;
            BudgetRecord record = new BudgetRecord
            {
                OwnerName = ownerName,
                TotalBytes = totalBytes < 0L ? 0L : totalBytes,
                BudgetBytes = budgetBytes,
                WarningIssued = false
            };

            if (!TryEnterRecordGate())
                return;

            try
            {
                if (!TryResolveRecordSlotLocked(ownerName, ownerKey, out ownerKey, out BudgetRecord existing, out bool hasExisting))
                    return;

                if (hasExisting)
                    record.WarningIssued = existing.WarningIssued;

                bool exceededBudget = record.TotalBytes > record.BudgetBytes;
                if (exceededBudget && !record.WarningIssued)
                {
                    record.WarningIssued = true;
                    shouldWarn = true;
                }
                else if (!exceededBudget)
                {
                    record.WarningIssued = false;
                }

                _records[ownerKey] = record;
            }
            finally
            {
                ExitRecordGate();
            }

            if (shouldWarn)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[MemoryBudgetTracker] Persistent native budget exceeded.");
#endif
            }
        }

        public static void Unregister(string ownerName)
        {
            if (string.IsNullOrEmpty(ownerName))
                return;

            if (!TryEnterRecordGate())
                return;

            try
            {
                int ownerKey = StableHash(ownerName);
                if (TryResolveRecordSlotLocked(ownerName, ownerKey, out ownerKey, out _, out bool hasExisting) && hasExisting)
                    _records.Remove(ownerKey);
            }
            finally
            {
                ExitRecordGate();
            }
        }

        public static int ResolveExponentialCapacity(int currentCapacity, int requiredCapacity, int minimumCapacity)
        {
            int resolvedCapacity = math.max(1, math.max(currentCapacity, minimumCapacity));
            int growthWatchdog = 32;
            while (resolvedCapacity < requiredCapacity && growthWatchdog-- > 0)
            {
                int nextCapacity = resolvedCapacity << 1;
                if (nextCapacity <= 0)
                    return requiredCapacity;

                resolvedCapacity = nextCapacity;
            }

            if (growthWatchdog <= 0)
                return requiredCapacity;

            return resolvedCapacity;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                const int fnvOffset = unchecked((int)2166136261u);
                const int fnvPrime = 16777619;
                int hash = fnvOffset;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash ^ value[i]) * fnvPrime;

                return hash;
            }
        }

        private static bool TryResolveRecordSlotLocked(
            string ownerName,
            int baseKey,
            out int ownerKey,
            out BudgetRecord existing,
            out bool hasExisting)
        {
            int firstFreeKey = 0;
            bool hasFreeKey = false;

            for (int probe = 0; probe <= OwnerCollisionProbeLimit; probe++)
            {
                int candidateKey = ResolveProbeKey(baseKey, probe);
                if (_records.TryGetValue(candidateKey, out existing))
                {
                    if (string.Equals(existing.OwnerName, ownerName, StringComparison.Ordinal))
                    {
                        ownerKey = candidateKey;
                        hasExisting = true;
                        return true;
                    }

                    continue;
                }

                if (!hasFreeKey)
                {
                    firstFreeKey = candidateKey;
                    hasFreeKey = true;
                }
            }

            if (hasFreeKey)
            {
                ownerKey = firstFreeKey;
                existing = default;
                hasExisting = false;
                return true;
            }

            ownerKey = 0;
            existing = default;
            hasExisting = false;
            return false;
        }

        private static int ResolveProbeKey(int baseKey, int probe)
        {
            if (probe <= 0)
                return baseKey;

            const int probeStep = unchecked((int)0x9E3779B9u);
            return unchecked(baseKey + (probe * probeStep));
        }

        private static bool TryEnterRecordGate()
        {
            for (int spin = 0; spin < GateSpinLimit; spin++)
            {
                if (Interlocked.CompareExchange(ref _recordGate, 1, 0) == 0)
                    return true;

                Thread.SpinWait(1 + spin);
            }

            return false;
        }

        private static void ExitRecordGate()
        {
            Volatile.Write(ref _recordGate, 0);
        }
    }
}
