using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for InventoryItemDefragmentationConsolidationCalculator.
    /// Extracted from PlayerInventory.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class InventoryItemDefragmentationConsolidationCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="itemIds">Parameter representing the itemIds (uint[]).</param>
        /// <param name="itemCounts">Parameter representing the itemCounts (int[]).</param>
        /// <param name="maxStackSizes">Parameter representing the maxStackSizes (int[]).</param>
        /// <returns>Returns Calculated slot-index displacements and consolidated stack size changes of type int[].</returns>
        public static int[] Compute(uint[] itemIds, int[] itemCounts, int[] maxStackSizes)
        {
            if (itemIds == null || itemCounts == null || maxStackSizes == null)
            {
                return Array.Empty<int>();
            }

            int count = Math.Min(itemIds.Length, Math.Min(itemCounts.Length, maxStackSizes.Length));

            // Output format constraints: "Calculated slot-index displacements and consolidated stack size changes"
            // Let's pack result as pairs [displacement, countChange] for each initial index.
            // displacement = (new index) - (original index).
            // countChange = (new count) - (original count).
            int[] result = new int[count * 2];

            // We need temporary arrays for pure math calculation to mimic in-place sorting/merging
            // without modifying input parameters, as it must be stateless.
            uint[] workingHashes = new uint[count];
            int[] workingCounts = new int[count];
            int[] originalIndices = new int[count];

            for (int i = 0; i < count; i++)
            {
                workingHashes[i] = itemIds[i];
                workingCounts[i] = Math.Max(0, itemCounts[i]); // Negative inputs clamped to 0
                originalIndices[i] = i;
            }

            // Step 1: Merge Stacks (like InventoryDefragCommand)
            for (int primary = 0; primary < count; primary++)
            {
                uint hash = workingHashes[primary];
                int primaryCount = workingCounts[primary];
                int maxStack = Math.Max(1, maxStackSizes[primary]); // Clamp maxStack to minimum 1

                if (hash == 0 || primaryCount == 0 || maxStack <= 1)
                    continue;

                for (int candidate = primary + 1; candidate < count && primaryCount < maxStack; candidate++)
                {
                    if (workingHashes[candidate] == hash && workingCounts[candidate] > 0)
                    {
                        int candidateMaxStack = Math.Max(1, maxStackSizes[candidate]);
                        if (candidateMaxStack > 1)
                        {
                            int candidateCount = workingCounts[candidate];
                            int transfer = Math.Min(maxStack - primaryCount, candidateCount);

                            if (transfer > 0)
                            {
                                primaryCount += transfer;
                                workingCounts[primary] = primaryCount;
                                workingCounts[candidate] -= transfer;

                                if (workingCounts[candidate] == 0)
                                {
                                    workingHashes[candidate] = 0;
                                }
                            }
                        }
                    }
                }
            }

            // Step 2: Compact Gaps (calculate displacement)
            int writeIndex = 0;
            int[] compactedOriginalIndices = new int[count];
            for (int i = 0; i < count; i++)
            {
                compactedOriginalIndices[i] = -1;
            }

            for (int readIndex = 0; readIndex < count; readIndex++)
            {
                if (workingHashes[readIndex] == 0 || workingCounts[readIndex] == 0)
                    continue;

                compactedOriginalIndices[writeIndex] = originalIndices[readIndex];
                writeIndex++;
            }

            // Generate output array based on displacements and count changes
            for (int originalIdx = 0; originalIdx < count; originalIdx++)
            {
                int newIndex = -1;
                int newCount = 0;

                // Find where the item ended up
                for(int i = 0; i < writeIndex; i++)
                {
                    if (compactedOriginalIndices[i] == originalIdx)
                    {
                        newIndex = i;
                        newCount = workingCounts[originalIdx]; // The new count after merges
                        break;
                    }
                }

                int initialCount = Math.Max(0, itemCounts[originalIdx]);
                int displacement = (newIndex != -1) ? (newIndex - originalIdx) : -originalIdx;
                // If it was merged completely into another slot, its index goes to -1 implicitly or we represent it logically.
                // We'll use displacement = 0 for items that stay, and just their count changes.
                // Or if it disappeared, displacement to 0th but count change handles it.
                // A better displacement metric:
                if (newIndex == -1)
                {
                    displacement = 0;
                }

                int countChange = newCount - initialCount;

                result[originalIdx * 2] = displacement;
                result[originalIdx * 2 + 1] = countChange;
            }

            return result;
        }
    }
}
