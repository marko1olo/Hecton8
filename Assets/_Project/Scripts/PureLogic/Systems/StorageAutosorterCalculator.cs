using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for StorageAutosorterCalculator.
    /// Extracted from PDAInventoryTab.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class StorageAutosorterCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='itemCategories'>Parameter representing the itemCategories (int[]).</param>
        /// <param name='itemWidths'>Parameter representing the itemWidths (int[]).</param>
        /// <param name='itemHeights'>Parameter representing the itemHeights (int[]).</param>
        /// <param name='gridWidth'>Parameter representing the gridWidth (int).</param>
        /// <param name='gridHeight'>Parameter representing the gridHeight (int).</param>
        /// <returns>Returns 1D flattened grid index coordinates for each item, or -1 if no fit of type int[].</returns>
        public static int[] Compute(int[] itemCategories, int[] itemWidths, int[] itemHeights, int gridWidth, int gridHeight)
        {
            if (itemCategories == null || itemWidths == null || itemHeights == null)
            {
                return Array.Empty<int>();
            }

            int count = itemCategories.Length;
            if (count == 0)
            {
                return Array.Empty<int>();
            }

            if (itemWidths.Length != count || itemHeights.Length != count)
            {
                throw new ArgumentException("Input arrays must have the same length.");
            }

            if (gridWidth <= 0 || gridHeight <= 0)
            {
                int[] invalid = new int[count];
                for (int i = 0; i < count; i++) invalid[i] = -1;
                return invalid;
            }

            // Protect against integer overflow for grid size allocation
            long gridArea = (long)gridWidth * gridHeight;
            if (gridArea > int.MaxValue)
            {
                int[] invalid = new int[count];
                for (int i = 0; i < count; i++) invalid[i] = -1;
                return invalid;
            }

            int[] result = new int[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = -1; // Default to no fit
            }

            // Create index array to sort by categories
            int[] indices = new int[count];
            for (int i = 0; i < count; i++)
            {
                indices[i] = i;
            }

            // Sort indices by category (ascending), then area (descending)
            Array.Sort(indices, (a, b) =>
            {
                int catCompare = itemCategories[a].CompareTo(itemCategories[b]);
                if (catCompare != 0) return catCompare;

                // Protect against overflow here too
                long areaA = (long)itemWidths[a] * itemHeights[a];
                long areaB = (long)itemWidths[b] * itemHeights[b];
                return areaB.CompareTo(areaA); // Descending area
            });

            bool[] occupied = new bool[gridWidth * gridHeight];

            // Bin packing: Top-Left to Bottom-Right first fit
            for (int i = 0; i < count; i++)
            {
                int idx = indices[i];
                int w = itemWidths[idx];
                int h = itemHeights[idx];

                if (w <= 0 || h <= 0 || w > gridWidth || h > gridHeight)
                {
                    continue; // Skip invalid or oversized items, remains -1
                }

                bool placed = false;
                for (int y = 0; y <= gridHeight - h && !placed; y++)
                {
                    for (int x = 0; x <= gridWidth - w && !placed; x++)
                    {
                        // Check if space is free
                        bool canFit = true;
                        for (int dy = 0; dy < h; dy++)
                        {
                            for (int dx = 0; dx < w; dx++)
                            {
                                if (occupied[(y + dy) * gridWidth + (x + dx)])
                                {
                                    canFit = false;
                                    break;
                                }
                            }
                            if (!canFit) break;
                        }

                        if (canFit)
                        {
                            // Place it
                            for (int dy = 0; dy < h; dy++)
                            {
                                for (int dx = 0; dx < w; dx++)
                                {
                                    occupied[(y + dy) * gridWidth + (x + dx)] = true;
                                }
                            }

                            result[idx] = y * gridWidth + x;
                            placed = true;
                        }
                    }
                }
            }

            return result;
        }
    }
}
