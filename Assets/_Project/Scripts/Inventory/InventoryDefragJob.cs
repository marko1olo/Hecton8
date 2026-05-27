using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Inventory.Algorithms
{
    /// <summary>
    /// Result slots written by <see cref="InventoryDefragCommand"/>.
    /// </summary>
    public static class InventoryDefragResultSlots
    {
        public const int OccupiedCount = 0;
        public const int MergeOperations = 1;
        public const int ShiftedRecords = 2;
        public const int Flags = 3;
        public const int RequiredLength = 4;
    }

    /// <summary>
    /// Sorts and compacts inventory SOA buffers in-place without managed or native allocations.
    /// </summary>
    /// <remarks>
    /// Sort order: non-empty first, category ascending, hash ascending, count descending.
    /// Optional arrays are shifted and swapped only when created and long enough for <see cref="SlotCount"/>.
    /// </remarks>
    public ref struct InventoryDefragCommand
    {
        public NativeArray<int> ItemHashes;
        public NativeArray<ushort> ItemCounts;
        public NativeArray<byte> ItemCategories;
        public NativeArray<ushort> MaxStackSizes;
        public NativeArray<byte> ItemRarities;
        public NativeArray<byte> ItemWidths;
        public NativeArray<byte> ItemHeights;
        public NativeArray<byte> ItemFlags;
        public NativeArray<ushort> ItemStateFlags;
        public NativeArray<byte> ItemGenetics;
        public NativeArray<ushort> QualityMilli;
        public NativeArray<byte> Durabilities;
        public NativeArray<uint> LastUpdateUnixSeconds;
        public NativeArray<float> UnitMassKg;
        public NativeArray<float> UnitVolumeM3;
        public NativeArray<float> UnitRadiationSv;
        public NativeArray<int> Result;
        public int SlotCount;

        public void Execute()
        {
            int count = ResolveCount();
            if (count <= 0)
            {
                WriteResult(0, 0, 0, 1);
                return;
            }

            int mergeOperations = MergeStacks(count);
            int shiftedRecords = 0;
            int occupiedCount = CompactGaps(count, ref shiftedRecords);
            InsertionSort(occupiedCount);
            WriteResult(occupiedCount, mergeOperations, shiftedRecords, 0);
        }

        private int ResolveCount()
        {
            if (!ItemHashes.IsCreated || !ItemCounts.IsCreated)
                return 0;

            int count = math.min(ItemHashes.Length, ItemCounts.Length);
            if (SlotCount > 0)
                count = math.min(count, SlotCount);

            count = ClampOptional(count, ItemCategories);
            count = ClampOptional(count, MaxStackSizes);
            count = ClampOptional(count, ItemRarities);
            count = ClampOptional(count, ItemWidths);
            count = ClampOptional(count, ItemHeights);
            count = ClampOptional(count, ItemFlags);
            count = ClampOptional(count, ItemStateFlags);
            count = ClampOptional(count, ItemGenetics);
            count = ClampOptional(count, QualityMilli);
            count = ClampOptional(count, Durabilities);
            count = ClampOptional(count, LastUpdateUnixSeconds);
            count = ClampOptional(count, UnitMassKg);
            count = ClampOptional(count, UnitVolumeM3);
            count = ClampOptional(count, UnitRadiationSv);
            return count;
        }

        private static int ClampOptional<T>(int count, NativeArray<T> optional)
            where T : struct
        {
            return optional.IsCreated ? math.min(count, optional.Length) : count;
        }

        private int MergeStacks(int count)
        {
            int mergeOperations = 0;
            for (int primary = 0; primary < count; primary++)
            {
                int hash = ItemHashes[primary];
                ushort primaryCount = ItemCounts[primary];
                ushort maxStack = ResolveMaxStack(primary);
                if (hash == 0 || primaryCount == 0 || maxStack <= 1)
                    continue;

                for (int candidate = primary + 1; candidate < count && primaryCount < maxStack; candidate++)
                {
                    if (!CanMerge(primary, candidate, hash))
                        continue;

                    ushort candidateCount = ItemCounts[candidate];
                    int transfer = math.min(maxStack - primaryCount, candidateCount);
                    if (transfer <= 0)
                        continue;

                    primaryCount = (ushort)(primaryCount + transfer);
                    ItemCounts[primary] = primaryCount;
                    candidateCount = (ushort)(candidateCount - transfer);
                    ItemCounts[candidate] = candidateCount;
                    mergeOperations++;

                    if (candidateCount == 0)
                        ClearRecord(candidate);
                }
            }

            return mergeOperations;
        }

        private bool CanMerge(int primary, int candidate, int hash)
        {
            return ItemHashes[candidate] == hash &&
                   ItemCounts[candidate] > 0 &&
                   ResolveMaxStack(candidate) > 1 &&
                   AreEqual(ItemCategories, primary, candidate) &&
                   AreEqual(ItemStateFlags, primary, candidate) &&
                   AreEqual(ItemGenetics, primary, candidate) &&
                   AreEqual(QualityMilli, primary, candidate);
        }

        private static bool AreEqual(NativeArray<byte> values, int a, int b)
        {
            return !values.IsCreated || values[a] == values[b];
        }

        private static bool AreEqual(NativeArray<ushort> values, int a, int b)
        {
            return !values.IsCreated || values[a] == values[b];
        }

        private ushort ResolveMaxStack(int index)
        {
            if (!MaxStackSizes.IsCreated)
                return ushort.MaxValue;

            ushort maxStack = MaxStackSizes[index];
            return maxStack == 0 ? (ushort)1 : maxStack;
        }

        private int CompactGaps(int count, ref int shiftedRecords)
        {
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < count; readIndex++)
            {
                if (ItemHashes[readIndex] == 0 || ItemCounts[readIndex] == 0)
                    continue;

                if (writeIndex != readIndex)
                {
                    CopyRecord(readIndex, writeIndex);
                    ClearRecord(readIndex);
                    shiftedRecords++;
                }

                writeIndex++;
            }

            for (int clearIndex = writeIndex; clearIndex < count; clearIndex++)
                ClearRecord(clearIndex);

            return writeIndex;
        }

        private void InsertionSort(int count)
        {
            for (int index = 1; index < count; index++)
            {
                InventoryDefragRecord current = ReadRecord(index);
                int scan = index - 1;
                while (scan >= 0 && Compare(in current, scan) < 0)
                {
                    CopyRecord(scan, scan + 1);
                    scan--;
                }

                WriteRecord(scan + 1, in current);
            }
        }

        private int Compare(in InventoryDefragRecord candidate, int existingIndex)
        {
            int existingHash = ItemHashes[existingIndex];
            bool candidateEmpty = candidate.Hash == 0 || candidate.Count == 0;
            bool existingEmpty = existingHash == 0 || ItemCounts[existingIndex] == 0;
            if (candidateEmpty != existingEmpty)
                return candidateEmpty ? 1 : -1;
            if (candidateEmpty)
                return 0;

            byte existingCategory = ReadOptional(ItemCategories, existingIndex);
            if (candidate.Category != existingCategory)
                return candidate.Category < existingCategory ? -1 : 1;

            if (candidate.Hash != existingHash)
                return candidate.Hash < existingHash ? -1 : 1;

            ushort existingCount = ItemCounts[existingIndex];
            if (candidate.Count != existingCount)
                return candidate.Count > existingCount ? -1 : 1;

            return 0;
        }

        private InventoryDefragRecord ReadRecord(int index)
        {
            return new InventoryDefragRecord
            {
                Hash = ItemHashes[index],
                Count = ItemCounts[index],
                Category = ReadOptional(ItemCategories, index),
                MaxStack = ReadOptional(MaxStackSizes, index),
                Rarity = ReadOptional(ItemRarities, index),
                Width = ReadOptional(ItemWidths, index),
                Height = ReadOptional(ItemHeights, index),
                Flags = ReadOptional(ItemFlags, index),
                StateFlags = ReadOptional(ItemStateFlags, index),
                Genetics = ReadOptional(ItemGenetics, index),
                Quality = ReadOptional(QualityMilli, index),
                Durability = ReadOptional(Durabilities, index),
                LastUpdateUnixSeconds = ReadOptional(LastUpdateUnixSeconds, index),
                UnitMassKg = ReadOptional(UnitMassKg, index),
                UnitVolumeM3 = ReadOptional(UnitVolumeM3, index),
                UnitRadiationSv = ReadOptional(UnitRadiationSv, index)
            };
        }

        private void WriteRecord(int index, in InventoryDefragRecord record)
        {
            ItemHashes[index] = record.Hash;
            ItemCounts[index] = record.Count;
            WriteOptional(ItemCategories, index, record.Category);
            WriteOptional(MaxStackSizes, index, record.MaxStack);
            WriteOptional(ItemRarities, index, record.Rarity);
            WriteOptional(ItemWidths, index, record.Width);
            WriteOptional(ItemHeights, index, record.Height);
            WriteOptional(ItemFlags, index, record.Flags);
            WriteOptional(ItemStateFlags, index, record.StateFlags);
            WriteOptional(ItemGenetics, index, record.Genetics);
            WriteOptional(QualityMilli, index, record.Quality);
            WriteOptional(Durabilities, index, record.Durability);
            WriteOptional(LastUpdateUnixSeconds, index, record.LastUpdateUnixSeconds);
            WriteOptional(UnitMassKg, index, record.UnitMassKg);
            WriteOptional(UnitVolumeM3, index, record.UnitVolumeM3);
            WriteOptional(UnitRadiationSv, index, record.UnitRadiationSv);
        }

        private void CopyRecord(int source, int destination)
        {
            ItemHashes[destination] = ItemHashes[source];
            ItemCounts[destination] = ItemCounts[source];
            CopyOptional(ItemCategories, source, destination);
            CopyOptional(MaxStackSizes, source, destination);
            CopyOptional(ItemRarities, source, destination);
            CopyOptional(ItemWidths, source, destination);
            CopyOptional(ItemHeights, source, destination);
            CopyOptional(ItemFlags, source, destination);
            CopyOptional(ItemStateFlags, source, destination);
            CopyOptional(ItemGenetics, source, destination);
            CopyOptional(QualityMilli, source, destination);
            CopyOptional(Durabilities, source, destination);
            CopyOptional(LastUpdateUnixSeconds, source, destination);
            CopyOptional(UnitMassKg, source, destination);
            CopyOptional(UnitVolumeM3, source, destination);
            CopyOptional(UnitRadiationSv, source, destination);
        }

        private void ClearRecord(int index)
        {
            ItemHashes[index] = 0;
            ItemCounts[index] = 0;
            ClearOptional(ItemCategories, index);
            ClearOptional(MaxStackSizes, index);
            ClearOptional(ItemRarities, index);
            ClearOptional(ItemWidths, index);
            ClearOptional(ItemHeights, index);
            ClearOptional(ItemFlags, index);
            ClearOptional(ItemStateFlags, index);
            ClearOptional(ItemGenetics, index);
            ClearOptional(QualityMilli, index);
            ClearOptional(Durabilities, index);
            ClearOptional(LastUpdateUnixSeconds, index);
            ClearOptional(UnitMassKg, index);
            ClearOptional(UnitVolumeM3, index);
            ClearOptional(UnitRadiationSv, index);
        }

        private static T ReadOptional<T>(NativeArray<T> values, int index)
            where T : struct
        {
            return values.IsCreated ? values[index] : default;
        }

        private static void WriteOptional<T>(NativeArray<T> values, int index, T value)
            where T : struct
        {
            if (values.IsCreated)
                values[index] = value;
        }

        private static void CopyOptional<T>(NativeArray<T> values, int source, int destination)
            where T : struct
        {
            if (values.IsCreated)
                values[destination] = values[source];
        }

        private static void ClearOptional<T>(NativeArray<T> values, int index)
            where T : struct
        {
            if (values.IsCreated)
                values[index] = default;
        }

        private void WriteResult(int occupiedCount, int mergeOperations, int shiftedRecords, int flags)
        {
            if (!Result.IsCreated || Result.Length < InventoryDefragResultSlots.RequiredLength)
                return;

            Result[InventoryDefragResultSlots.OccupiedCount] = occupiedCount;
            Result[InventoryDefragResultSlots.MergeOperations] = mergeOperations;
            Result[InventoryDefragResultSlots.ShiftedRecords] = shiftedRecords;
            Result[InventoryDefragResultSlots.Flags] = flags;
        }

        private struct InventoryDefragRecord
        {
            public int Hash;
            public ushort Count;
            public byte Category;
            public ushort MaxStack;
            public byte Rarity;
            public byte Width;
            public byte Height;
            public byte Flags;
            public ushort StateFlags;
            public byte Genetics;
            public ushort Quality;
            public byte Durability;
            public uint LastUpdateUnixSeconds;
            public float UnitMassKg;
            public float UnitVolumeM3;
            public float UnitRadiationSv;
        }
    }

    /// <summary>
    /// Legacy Burst wrapper for amortized dispatcher-owned defrag windows. Player inventory sort uses
    /// <see cref="InventoryDefragCommand"/> directly to avoid same-frame schedule/readback.
    /// </summary>
    [System.Obsolete("Use InventoryDefragCommand.Execute() in the owner phase unless a dispatcher-owned async window is proven.", false)]
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct InventoryDefragJob : IJob
    {
        public NativeArray<int> ItemHashes;
        public NativeArray<ushort> ItemCounts;
        public NativeArray<byte> ItemCategories;
        public NativeArray<ushort> MaxStackSizes;
        public NativeArray<byte> ItemRarities;
        public NativeArray<byte> ItemWidths;
        public NativeArray<byte> ItemHeights;
        public NativeArray<byte> ItemFlags;
        public NativeArray<ushort> ItemStateFlags;
        public NativeArray<byte> ItemGenetics;
        public NativeArray<ushort> QualityMilli;
        public NativeArray<byte> Durabilities;
        public NativeArray<uint> LastUpdateUnixSeconds;
        public NativeArray<float> UnitMassKg;
        public NativeArray<float> UnitVolumeM3;
        public NativeArray<float> UnitRadiationSv;
        public NativeArray<int> Result;
        public int SlotCount;

        public void Execute()
        {
            InventoryDefragCommand command = new InventoryDefragCommand
            {
                ItemHashes = ItemHashes,
                ItemCounts = ItemCounts,
                ItemCategories = ItemCategories,
                MaxStackSizes = MaxStackSizes,
                ItemRarities = ItemRarities,
                ItemWidths = ItemWidths,
                ItemHeights = ItemHeights,
                ItemFlags = ItemFlags,
                ItemStateFlags = ItemStateFlags,
                ItemGenetics = ItemGenetics,
                QualityMilli = QualityMilli,
                Durabilities = Durabilities,
                LastUpdateUnixSeconds = LastUpdateUnixSeconds,
                UnitMassKg = UnitMassKg,
                UnitVolumeM3 = UnitVolumeM3,
                UnitRadiationSv = UnitRadiationSv,
                Result = Result,
                SlotCount = SlotCount
            };
            command.Execute();
        }
    }
}
