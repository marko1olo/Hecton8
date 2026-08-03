using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for StackMergePriorityCalculator.
    /// Extracted from InventoryRoutingNetwork.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class StackMergePriorityCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="stackCounts">Parameter representing the stackCounts (int[]).</param>
        /// <param name="maxStackSize">Parameter representing the maxStackSize (int).</param>
        /// <param name="quantityToAdd">Parameter representing the quantityToAdd (int).</param>
        /// <returns>Returns newStackCounts, int (remainder) of type int[].</returns>
        public static int[] Compute(int[] stackCounts, int maxStackSize, int quantityToAdd)
        {
            if (stackCounts == null)
            {
                throw new ArgumentNullException(nameof(stackCounts));
            }

            int clampedMaxStack = Math.Max(1, maxStackSize);
            int remainingToAdd = Math.Max(0, quantityToAdd);

            int len = stackCounts.Length;
            int[] result = new int[len + 1];

            // Struct to keep track of original index and count
            var indexedStacks = new StackInfo[len];
            for (int i = 0; i < len; i++)
            {
                // Clamp existing stacks to valid bounds
                int currentCount = Math.Max(0, Math.Min(stackCounts[i], clampedMaxStack));
                indexedStacks[i] = new StackInfo { Index = i, Count = currentCount };
            }

            // Sort by "most-full" first (descending by Count)
            // But we only want to fill stacks that have space, so actually if count == maxStackSize, they take 0.
            // Sorting descending by count puts the most-full stacks at the beginning.
            Array.Sort(indexedStacks, (a, b) => b.Count.CompareTo(a.Count));

            for (int i = 0; i < len; i++)
            {
                int availableSpace = clampedMaxStack - indexedStacks[i].Count;
                if (availableSpace > 0 && remainingToAdd > 0)
                {
                    int amountToFill = Math.Min(availableSpace, remainingToAdd);
                    indexedStacks[i].Count += amountToFill;
                    remainingToAdd -= amountToFill;
                }
            }

            // Restore original order and populate result
            for (int i = 0; i < len; i++)
            {
                int originalIndex = indexedStacks[i].Index;
                result[originalIndex] = indexedStacks[i].Count;
            }

            // The last element is the remainder
            result[len] = remainingToAdd;

            return result;
        }

        private struct StackInfo
        {
            public int Index;
            public int Count;
        }
    }
}
