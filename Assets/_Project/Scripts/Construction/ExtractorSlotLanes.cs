using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    /// <summary>
    /// Slot arithmetic for the autonomous-extractor accumulation lanes.
    /// The extractor runtime keys cycle timer, buffered item hash, buffered unit count, and completed cycle
    /// count by registry slot index, so a slot relocation must carry the whole row with it and a slot claim
    /// must start from a zeroed row. Without both rules a registry compaction deletes one extractor's
    /// buffered yield and hands the abandoned tally to whichever module claims the freed slot next.
    /// </summary>
    public static class ExtractorSlotLanes
    {
        /// <summary>Row count the four lanes can address together, or zero when any lane is missing.</summary>
        public static int ResolveUsableRowCount(
            NativeArray<float> cycleTimers,
            NativeArray<int> bufferedItemHashIds,
            NativeArray<int> bufferedUnitCounts,
            NativeArray<int> completedCycleCounts)
        {
            if (!cycleTimers.IsCreated ||
                !bufferedItemHashIds.IsCreated ||
                !bufferedUnitCounts.IsCreated ||
                !completedCycleCounts.IsCreated)
            {
                return 0;
            }

            int rowCount = math.min(cycleTimers.Length, bufferedItemHashIds.Length);
            rowCount = math.min(rowCount, bufferedUnitCounts.Length);
            rowCount = math.min(rowCount, completedCycleCounts.Length);
            return math.max(0, rowCount);
        }

        /// <summary>
        /// Carries one extractor's accumulated row from <paramref name="sourceIndex"/> to
        /// <paramref name="destinationIndex"/> and zeroes the vacated row so nothing inherits it.
        /// A non-finite or negative accumulated cycle time is corruption, not a schedule, so the timer is
        /// clamped through the <see cref="MathGuard"/> owner rather than a local copy of the same rule;
        /// buffered and completed tallies are physical unit counts, so negatives are clamped away too.
        /// </summary>
        public static bool TryMoveRow(
            NativeArray<float> cycleTimers,
            NativeArray<int> bufferedItemHashIds,
            NativeArray<int> bufferedUnitCounts,
            NativeArray<int> completedCycleCounts,
            int sourceIndex,
            int destinationIndex)
        {
            int rowCount = ResolveUsableRowCount(cycleTimers, bufferedItemHashIds, bufferedUnitCounts, completedCycleCounts);
            if ((uint)sourceIndex >= (uint)rowCount || (uint)destinationIndex >= (uint)rowCount)
                return false;

            if (sourceIndex == destinationIndex)
                return true;

            cycleTimers[destinationIndex] = MathGuard.SanitizeNonNegative(cycleTimers[sourceIndex]);
            bufferedItemHashIds[destinationIndex] = bufferedItemHashIds[sourceIndex];
            bufferedUnitCounts[destinationIndex] = math.max(0, bufferedUnitCounts[sourceIndex]);
            completedCycleCounts[destinationIndex] = math.max(0, completedCycleCounts[sourceIndex]);

            return TryClearRow(cycleTimers, bufferedItemHashIds, bufferedUnitCounts, completedCycleCounts, sourceIndex);
        }

        /// <summary>
        /// Zeroes one slot row so a claimed slot cannot inherit an abandoned extraction buffer. This is the
        /// only place a zeroed extractor row is defined: slot claim, module unregistration, and the
        /// job-completion pass over a vacated slot all route here, so a fifth lane is added once, not three
        /// times. Returns false when the index is outside the shortest lane.
        /// </summary>
        public static bool TryClearRow(
            NativeArray<float> cycleTimers,
            NativeArray<int> bufferedItemHashIds,
            NativeArray<int> bufferedUnitCounts,
            NativeArray<int> completedCycleCounts,
            int index)
        {
            int rowCount = ResolveUsableRowCount(cycleTimers, bufferedItemHashIds, bufferedUnitCounts, completedCycleCounts);
            if ((uint)index >= (uint)rowCount)
                return false;

            cycleTimers[index] = 0f;
            bufferedItemHashIds[index] = 0;
            bufferedUnitCounts[index] = 0;
            completedCycleCounts[index] = 0;
            return true;
        }
    }
}
