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

        [BurstCompile]
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

        [BurstCompile]
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
