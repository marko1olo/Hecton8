using System.Collections.Generic;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _records.Clear();
        }

        public static void Register(string ownerName, long totalBytes, long budgetBytes)
        {
            if (string.IsNullOrEmpty(ownerName) || budgetBytes <= 0L)
                return;

            int ownerKey = StableHash(ownerName);
            BudgetRecord record = new BudgetRecord
            {
                OwnerName = ownerName,
                TotalBytes = totalBytes < 0L ? 0L : totalBytes,
                BudgetBytes = budgetBytes,
                WarningIssued = false
            };

            if (_records.TryGetValue(ownerKey, out BudgetRecord existing))
                record.WarningIssued = existing.WarningIssued;

            bool exceededBudget = record.TotalBytes > record.BudgetBytes;
            if (exceededBudget && !record.WarningIssued)
            {
                record.WarningIssued = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "[MemoryBudgetTracker] " +
                    ownerName +
                    " exceeded persistent budget. Used=" +
                    FormatBytes(record.TotalBytes) +
                    ", Budget=" +
                    FormatBytes(record.BudgetBytes) +
                    ".");
#endif
            }
            else if (!exceededBudget)
            {
                record.WarningIssued = false;
            }

            _records[ownerKey] = record;
        }

        public static void Unregister(string ownerName)
        {
            if (string.IsNullOrEmpty(ownerName))
                return;

            _records.Remove(StableHash(ownerName));
        }

        public static int ResolveExponentialCapacity(int currentCapacity, int requiredCapacity, int minimumCapacity)
        {
            int resolvedCapacity = Mathf.Max(1, Mathf.Max(currentCapacity, minimumCapacity));
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

        private static string FormatBytes(long bytes)
        {
            const double bytesPerMb = 1024.0 * 1024.0;
            return (bytes / bytesPerMb).ToString("F2") + " MB";
        }
    }
}
