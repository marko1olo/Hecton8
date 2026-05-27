using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Inventory
{
    /// <summary>
    /// Zero-GC helpers for hash/count/condition inventory buffers.
    /// </summary>
    public static class InventorySoAUtility
    {
        public enum TransferFailureCode : byte
        {
            None = 0,
            InvalidInput = 1,
            SourceEmpty = 2,
            TargetOccupied = 3,
            WeightLimit = 4,
            VolumeLimit = 5,
            CopyRejected = 6,
            PlacementRejected = 7,
            CraftLocked = 8
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public readonly struct BulkTransferResult
        {
            public readonly TransferFailureCode FailureCode;
            public readonly int MovedSlotCount;
            public readonly int MovedStackCount;
            public readonly float TransferWeightKg;
            public readonly float TransferVolumeLiters;
            public readonly float TargetWeightAfterKg;
            public readonly float TargetVolumeAfterLiters;

            public bool Succeeded => FailureCode == TransferFailureCode.None;

            public BulkTransferResult(
                TransferFailureCode failureCode,
                int movedSlotCount,
                int movedStackCount,
                float transferWeightKg,
                float transferVolumeLiters,
                float targetWeightAfterKg,
                float targetVolumeAfterLiters)
            {
                FailureCode = failureCode;
                MovedSlotCount = movedSlotCount;
                MovedStackCount = movedStackCount;
                TransferWeightKg = transferWeightKg;
                TransferVolumeLiters = transferVolumeLiters;
                TargetWeightAfterKg = targetWeightAfterKg;
                TargetVolumeAfterLiters = targetVolumeAfterLiters;
            }

            public static BulkTransferResult Failed(TransferFailureCode failureCode)
            {
                return new BulkTransferResult(failureCode, 0, 0, 0f, 0f, 0f, 0f);
            }
        }

        public static bool CanCraftFast(ulong currentInventoryMask, ulong recipeMask)
        {
            return recipeMask != 0UL && (currentInventoryMask & recipeMask) == recipeMask;
        }

        public static ushort ResolveStackInsert(ushort currentCount, ushort incomingCount, ushort maxStack, out ushort acceptedCount)
        {
            int capacity = math.max(0, (int)maxStack - currentCount);
            int accepted = math.min(capacity, incomingCount);
            acceptedCount = (ushort)accepted;
            return (ushort)math.min(maxStack, currentCount + accepted);
        }

        public static unsafe bool TryBulkCopyIdenticalItems(
            NativeArray<uint> sourceHashes,
            NativeArray<ushort> sourceCounts,
            NativeArray<float> sourceConditions,
            int sourceStartIndex,
            NativeArray<uint> destinationHashes,
            NativeArray<ushort> destinationCounts,
            NativeArray<float> destinationConditions,
            int destinationStartIndex,
            int itemCount)
        {
            if (!sourceHashes.IsCreated ||
                !sourceCounts.IsCreated ||
                !sourceConditions.IsCreated ||
                !destinationHashes.IsCreated ||
                !destinationCounts.IsCreated ||
                !destinationConditions.IsCreated ||
                sourceStartIndex < 0 ||
                destinationStartIndex < 0 ||
                itemCount <= 0 ||
                sourceStartIndex + itemCount > sourceHashes.Length ||
                sourceStartIndex + itemCount > sourceCounts.Length ||
                sourceStartIndex + itemCount > sourceConditions.Length ||
                destinationStartIndex + itemCount > destinationHashes.Length ||
                destinationStartIndex + itemCount > destinationCounts.Length ||
                destinationStartIndex + itemCount > destinationConditions.Length)
            {
                return false;
            }

            void* sourceHashPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourceHashes) + sourceStartIndex * UnsafeUtility.SizeOf<uint>();
            void* destinationHashPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destinationHashes) + destinationStartIndex * UnsafeUtility.SizeOf<uint>();
            void* sourceCountPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourceCounts) + sourceStartIndex * UnsafeUtility.SizeOf<ushort>();
            void* destinationCountPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destinationCounts) + destinationStartIndex * UnsafeUtility.SizeOf<ushort>();
            void* sourceConditionPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourceConditions) + sourceStartIndex * UnsafeUtility.SizeOf<float>();
            void* destinationConditionPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destinationConditions) + destinationStartIndex * UnsafeUtility.SizeOf<float>();

            long hashBytes = itemCount * UnsafeUtility.SizeOf<uint>();
            long countBytes = itemCount * UnsafeUtility.SizeOf<ushort>();
            long conditionBytes = itemCount * UnsafeUtility.SizeOf<float>();

            if (!UnsafeMemoryCopyGuard.CanCopy(destinationHashPtr, (destinationHashes.Length - destinationStartIndex) * UnsafeUtility.SizeOf<uint>(), sourceHashPtr, hashBytes) ||
                !UnsafeMemoryCopyGuard.CanCopy(destinationCountPtr, (destinationCounts.Length - destinationStartIndex) * UnsafeUtility.SizeOf<ushort>(), sourceCountPtr, countBytes) ||
                !UnsafeMemoryCopyGuard.CanCopy(destinationConditionPtr, (destinationConditions.Length - destinationStartIndex) * UnsafeUtility.SizeOf<float>(), sourceConditionPtr, conditionBytes))
            {
                return false;
            }

            return UnsafeMemoryCopyGuard.TryMemCpy(destinationHashPtr, (destinationHashes.Length - destinationStartIndex) * UnsafeUtility.SizeOf<uint>(), sourceHashPtr, hashBytes) &&
                   UnsafeMemoryCopyGuard.TryMemCpy(destinationCountPtr, (destinationCounts.Length - destinationStartIndex) * UnsafeUtility.SizeOf<ushort>(), sourceCountPtr, countBytes) &&
                   UnsafeMemoryCopyGuard.TryMemCpy(destinationConditionPtr, (destinationConditions.Length - destinationStartIndex) * UnsafeUtility.SizeOf<float>(), sourceConditionPtr, conditionBytes);
        }

        public static unsafe bool TryBulkCopySlice<T>(
            NativeArray<T> source,
            int sourceStartIndex,
            NativeArray<T> destination,
            int destinationStartIndex,
            int elementCount) where T : struct
        {
            if (!source.IsCreated ||
                !destination.IsCreated ||
                sourceStartIndex < 0 ||
                destinationStartIndex < 0 ||
                elementCount <= 0 ||
                sourceStartIndex + elementCount > source.Length ||
                destinationStartIndex + elementCount > destination.Length)
            {
                return false;
            }

            int elementSize = UnsafeUtility.SizeOf<T>();
            long copyBytes = (long)elementCount * elementSize;
            void* sourcePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source) + ((long)sourceStartIndex * elementSize);
            void* destinationPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(destination) + ((long)destinationStartIndex * elementSize);
            long destinationBytes = ((long)destination.Length - destinationStartIndex) * elementSize;
            if (!UnsafeMemoryCopyGuard.CanCopy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(InventorySoAUtility));
                return false;
            }

            return UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
        }

        public static unsafe bool TryClearSlice<T>(NativeArray<T> array, int startIndex, int elementCount) where T : struct
        {
            if (!array.IsCreated ||
                startIndex < 0 ||
                elementCount < 0 ||
                startIndex + elementCount > array.Length)
            {
                return false;
            }

            if (elementCount == 0)
                return true;

            int elementSize = UnsafeUtility.SizeOf<T>();
            void* destinationPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array) + ((long)startIndex * elementSize);
            UnsafeUtility.MemClear(destinationPtr, (long)elementCount * elementSize);
            return true;
        }

        [System.Obsolete("Bulk-transfer validation is owner-phase scalar work; do not schedule same-frame validation jobs.", false)]
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct InventoryTransferValidationJob : IJob
        {
            [ReadOnly] public NativeArray<uint> SourceHashes;
            [ReadOnly] public NativeArray<ushort> SourceCounts;
            [ReadOnly] public NativeArray<float> SourceUnitMassKg;
            [ReadOnly] public NativeArray<float> SourceUnitVolumeM3;
            [ReadOnly] public NativeArray<uint> TargetHashes;
            [ReadOnly] public NativeArray<ushort> TargetCounts;
            public NativeArray<float4> Result;
            public NativeArray<byte> FailureCode;
            public int SourceStartIndex;
            public int TargetStartIndex;
            public int SlotCount;
            public float TargetCurrentWeightKg;
            public float TargetCurrentVolumeLiters;
            public float TargetMaxWeightKg;
            public float TargetMaxVolumeLiters;

            public void Execute()
            {
                if (!SourceHashes.IsCreated ||
                    !SourceCounts.IsCreated ||
                    !SourceUnitMassKg.IsCreated ||
                    !SourceUnitVolumeM3.IsCreated ||
                    !TargetHashes.IsCreated ||
                    !TargetCounts.IsCreated ||
                    !Result.IsCreated ||
                    Result.Length == 0 ||
                    !FailureCode.IsCreated ||
                    FailureCode.Length == 0 ||
                    SourceStartIndex < 0 ||
                    TargetStartIndex < 0 ||
                    SlotCount <= 0 ||
                    SourceStartIndex + SlotCount > SourceHashes.Length ||
                    SourceStartIndex + SlotCount > SourceCounts.Length ||
                    SourceStartIndex + SlotCount > SourceUnitMassKg.Length ||
                    SourceStartIndex + SlotCount > SourceUnitVolumeM3.Length ||
                    TargetStartIndex + SlotCount > TargetHashes.Length ||
                    TargetStartIndex + SlotCount > TargetCounts.Length)
                {
                    SetFailure(TransferFailureCode.InvalidInput);
                    return;
                }

                float transferWeightKg = 0f;
                float transferVolumeLiters = 0f;
                int movedSlotCount = 0;
                int movedStackCount = 0;

                for (int offset = 0; offset < SlotCount; offset++)
                {
                    int sourceIndex = SourceStartIndex + offset;
                    uint hash = SourceHashes[sourceIndex];
                    ushort count = SourceCounts[sourceIndex];
                    if (hash == 0u || count == 0)
                        continue;

                    int targetIndex = TargetStartIndex + offset;
                    if (TargetHashes[targetIndex] != 0u || TargetCounts[targetIndex] != 0)
                    {
                        SetFailure(TransferFailureCode.TargetOccupied);
                        return;
                    }

                    float unitMassKg = math.max(0f, SourceUnitMassKg[sourceIndex]);
                    float unitVolumeLiters = math.max(0f, SourceUnitVolumeM3[sourceIndex]) * 1000f;
                    transferWeightKg += unitMassKg * count;
                    transferVolumeLiters += unitVolumeLiters * count;
                    movedSlotCount++;
                    movedStackCount += count;
                }

                if (movedSlotCount == 0)
                {
                    SetFailure(TransferFailureCode.SourceEmpty);
                    return;
                }

                float nextWeightKg = TargetCurrentWeightKg + transferWeightKg;
                if (TargetMaxWeightKg >= 0f && nextWeightKg > TargetMaxWeightKg)
                {
                    Result[0] = new float4(transferWeightKg, transferVolumeLiters, movedStackCount, movedSlotCount);
                    SetFailure(TransferFailureCode.WeightLimit);
                    return;
                }

                float nextVolumeLiters = TargetCurrentVolumeLiters + transferVolumeLiters;
                if (TargetMaxVolumeLiters >= 0f && nextVolumeLiters > TargetMaxVolumeLiters)
                {
                    Result[0] = new float4(transferWeightKg, transferVolumeLiters, movedStackCount, movedSlotCount);
                    SetFailure(TransferFailureCode.VolumeLimit);
                    return;
                }

                Result[0] = new float4(transferWeightKg, transferVolumeLiters, movedStackCount, movedSlotCount);
                FailureCode[0] = (byte)TransferFailureCode.None;
            }

            private void SetFailure(TransferFailureCode failureCode)
            {
                FailureCode[0] = (byte)failureCode;
            }
        }

        [System.Obsolete("Bulk-transfer compaction is owner-phase scalar work; do not schedule same-frame compaction jobs.", false)]
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct InventoryCompactionJob : IJob
        {
            public NativeArray<uint> ItemHashes;
            public NativeArray<ushort> ItemCounts;
            public NativeArray<float> ItemCondition;
            public NativeArray<ushort> ItemStateFlags;
            public NativeArray<byte> ItemGenetics;
            public NativeArray<ushort> QualityMilli;
            public NativeArray<byte> Durabilities;
            public NativeArray<uint> LastUpdateUnixSeconds;
            public NativeArray<float> UnitMassKg;
            public NativeArray<float> UnitVolumeM3;
            public NativeArray<float> UnitRadiationSv;
            [ReadOnly] public NativeArray<ushort>.ReadOnly MaxStackCounts;
            public NativeArray<int> ResultCount;

            public void Execute()
            {
                int count = ResolveCount();
                for (int primary = 0; primary < count; primary++)
                {
                    uint hash = ItemHashes[primary];
                    ushort primaryCount = ItemCounts[primary];
                    ushort maxStack = ResolveMaxStack(primary);
                    if (hash == 0u || primaryCount == 0 || maxStack <= 1)
                        continue;

                    for (int candidate = primary + 1; candidate < count && primaryCount < maxStack; candidate++)
                    {
                        if (!CanMerge(primary, candidate, hash))
                            continue;

                        ushort candidateCount = ItemCounts[candidate];
                        int capacity = math.max(0, maxStack - primaryCount);
                        int transfer = math.min(capacity, candidateCount);
                        if (transfer <= 0)
                            continue;

                        primaryCount = (ushort)(primaryCount + transfer);
                        ItemCounts[primary] = primaryCount;
                        candidateCount = (ushort)(candidateCount - transfer);
                        ItemCounts[candidate] = candidateCount;
                        ItemCondition[primary] = math.max(ItemCondition[primary], ItemCondition[candidate]);
                        QualityMilli[primary] = (ushort)math.max((int)QualityMilli[primary], (int)QualityMilli[candidate]);
                        Durabilities[primary] = (byte)math.max((int)Durabilities[primary], (int)Durabilities[candidate]);
                        LastUpdateUnixSeconds[primary] = math.max(LastUpdateUnixSeconds[primary], LastUpdateUnixSeconds[candidate]);

                        if (candidateCount == 0)
                            ClearRecord(candidate);
                    }
                }

                int writeIndex = 0;
                for (int readIndex = 0; readIndex < count; readIndex++)
                {
                    if (ItemHashes[readIndex] == 0u || ItemCounts[readIndex] == 0)
                        continue;

                    if (writeIndex != readIndex)
                    {
                        CopyRecord(readIndex, writeIndex);
                        ClearRecord(readIndex);
                    }

                    writeIndex++;
                }

                for (int clearIndex = writeIndex; clearIndex < count; clearIndex++)
                    ClearRecord(clearIndex);

                if (ResultCount.IsCreated && ResultCount.Length > 0)
                    ResultCount[0] = writeIndex;
            }

            private int ResolveCount()
            {
                int count = math.min(ItemHashes.Length, ItemCounts.Length);
                count = math.min(count, ItemCondition.Length);
                count = math.min(count, ItemStateFlags.Length);
                count = math.min(count, ItemGenetics.Length);
                count = math.min(count, QualityMilli.Length);
                count = math.min(count, Durabilities.Length);
                count = math.min(count, LastUpdateUnixSeconds.Length);
                count = math.min(count, UnitMassKg.Length);
                count = math.min(count, UnitVolumeM3.Length);
                count = math.min(count, UnitRadiationSv.Length);
                if (MaxStackCounts.IsCreated)
                    count = math.min(count, MaxStackCounts.Length);
                return count;
            }

            private ushort ResolveMaxStack(int index)
            {
                if (!MaxStackCounts.IsCreated || (uint)index >= (uint)MaxStackCounts.Length)
                    return ushort.MaxValue;

                ushort maxStack = MaxStackCounts[index];
                return maxStack == 0 ? (ushort)1 : maxStack;
            }

            private bool CanMerge(int primary, int candidate, uint hash)
            {
                return ItemHashes[candidate] == hash &&
                       ItemCounts[candidate] > 0 &&
                       ResolveMaxStack(candidate) > 1 &&
                       ItemStateFlags[candidate] == ItemStateFlags[primary] &&
                       ItemGenetics[candidate] == ItemGenetics[primary] &&
                       QualityMilli[candidate] == QualityMilli[primary];
            }

            private void CopyRecord(int source, int destination)
            {
                ItemHashes[destination] = ItemHashes[source];
                ItemCounts[destination] = ItemCounts[source];
                ItemCondition[destination] = math.saturate(ItemCondition[source]);
                ItemStateFlags[destination] = ItemStateFlags[source];
                ItemGenetics[destination] = ItemGenetics[source];
                QualityMilli[destination] = QualityMilli[source];
                Durabilities[destination] = Durabilities[source];
                LastUpdateUnixSeconds[destination] = LastUpdateUnixSeconds[source];
                UnitMassKg[destination] = UnitMassKg[source];
                UnitVolumeM3[destination] = UnitVolumeM3[source];
                UnitRadiationSv[destination] = UnitRadiationSv[source];
            }

            private void ClearRecord(int index)
            {
                ItemHashes[index] = 0u;
                ItemCounts[index] = 0;
                ItemCondition[index] = 0f;
                ItemStateFlags[index] = 0;
                ItemGenetics[index] = 0;
                QualityMilli[index] = 0;
                Durabilities[index] = 0;
                LastUpdateUnixSeconds[index] = 0u;
                UnitMassKg[index] = 0f;
                UnitVolumeM3[index] = 0f;
                UnitRadiationSv[index] = 0f;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct DefragmentJob : IJob
        {
            public NativeArray<uint> ItemHashes;
            public NativeArray<ushort> ItemCounts;
            public NativeArray<float> ItemCondition;
            public NativeArray<int> ResultCount;

            public void Execute()
            {
                int count = math.min(math.min(ItemHashes.Length, ItemCounts.Length), ItemCondition.Length);
                int writeIndex = 0;
                for (int readIndex = 0; readIndex < count; readIndex++)
                {
                    uint hash = ItemHashes[readIndex];
                    ushort itemCount = ItemCounts[readIndex];
                    if (hash == 0u || itemCount == 0)
                        continue;

                    if (writeIndex != readIndex)
                    {
                        ItemHashes[writeIndex] = hash;
                        ItemCounts[writeIndex] = itemCount;
                        ItemCondition[writeIndex] = math.saturate(ItemCondition[readIndex]);
                        ItemHashes[readIndex] = 0u;
                        ItemCounts[readIndex] = 0;
                        ItemCondition[readIndex] = 0f;
                    }

                    writeIndex++;
                }

                for (int clearIndex = writeIndex; clearIndex < count; clearIndex++)
                {
                    ItemHashes[clearIndex] = 0u;
                    ItemCounts[clearIndex] = 0;
                    ItemCondition[clearIndex] = 0f;
                }

                if (ResultCount.IsCreated && ResultCount.Length > 0)
                    ResultCount[0] = writeIndex;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct FrostTickConditionDecayJob : IJob
        {
            [ReadOnly] public NativeArray<uint> PerishableHashes;
            public NativeArray<uint> ItemHashes;
            public NativeArray<float> ItemCondition;
            public float DeltaSeconds;
            public float DecayPerSecond;

            public void Execute()
            {
                if (!ItemHashes.IsCreated ||
                    !ItemCondition.IsCreated ||
                    !PerishableHashes.IsCreated ||
                    PerishableHashes.Length == 0)
                {
                    return;
                }

                int count = math.min(ItemHashes.Length, ItemCondition.Length);
                float decay = math.max(0f, DeltaSeconds) * math.max(0f, DecayPerSecond);
                if (decay <= 0f)
                    return;

                for (int index = 0; index < count; index++)
                {
                    uint hash = ItemHashes[index];
                    if (hash == 0u || !IsPerishable(hash))
                        continue;

                    ItemCondition[index] = math.saturate(ItemCondition[index] - decay);
                }
            }

            private bool IsPerishable(uint hash)
            {
                for (int index = 0; index < PerishableHashes.Length; index++)
                {
                    if (PerishableHashes[index] == hash)
                        return true;
                }

                return false;
            }
        }
    }
}
