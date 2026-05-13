namespace Hecton8.Inventory.Corrosion
{
    using Hecton8.Inventory.Corrosion.Contracts;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;

    [BurstCompile]
    public struct ItemSalinityCorrosionJob : IJob
    {
        [ReadOnly] public NativeArray<uint>.ReadOnly ItemHashes;
        [ReadOnly] public NativeArray<ushort> StackCounts;
        public NativeArray<float> ItemDurability;
        public NativeArray<byte> DurabilityBytes;
        public NativeArray<ushort> QualityMilli;
        public NativeArray<ushort> ItemStateFlags;
        public NativeArray<int> Result;
        public NativeArray<uint> BrokenItemHashes;
        public ulong CurrentInventoryMask;
        public float SalinityFactor;
        public float DegradationRate;
        public ushort DegradedMask;
        public ushort RustedMask;
        public ushort BrokenMask;
        public ushort DegradedThresholdMilli;

        public void Execute()
        {
            ClearResult();

            int count = math.min(
                math.min(math.min(ItemHashes.Length, StackCounts.Length), math.min(ItemDurability.Length, DurabilityBytes.Length)),
                math.min(QualityMilli.Length, ItemStateFlags.Length));
            if (count <= 0)
                return;

            float salinity = math.saturate(SalinityFactor);
            float tickDegradation = math.max(0f, DegradationRate) * salinity;
            float totalDurability = 0f;
            int equippedCount = 0;
            int scannedCount = 0;
            int changedCount = 0;
            int brokenCount = 0;

            for (int slot = 0; slot < count; slot++)
            {
                uint hash = ItemHashes[slot];
                if (hash == 0u || StackCounts[slot] == 0)
                    continue;

                scannedCount++;
                ulong materialBit = ItemCorrosionMath.ResolveInventoryMaterialBit(hash);
                if ((CurrentInventoryMask & materialBit) == 0UL)
                    continue;

                float currentDurability = math.saturate(ItemDurability[slot]);
                if (!math.isfinite(currentDurability))
                    currentDurability = 0f;

                ushort flags = ItemStateFlags[slot];
                if ((flags & BrokenMask) != 0)
                {
                    totalDurability += 0f;
                    equippedCount++;
                    continue;
                }

                float nextDurability = currentDurability;
                if (tickDegradation > 0f)
                {
                    nextDurability = math.saturate(currentDurability - tickDegradation);
                    if (salinity > 0f)
                        flags = (ushort)(flags | RustedMask);
                }

                ushort nextQualityMilli = (ushort)math.clamp((int)math.round(nextDurability * 1000f), 0, 1000);
                byte nextDurabilityByte = (byte)math.clamp((int)math.round(nextDurability * 100f), 0, 100);

                if (nextQualityMilli < DegradedThresholdMilli)
                    flags = (ushort)(flags | DegradedMask);

                if (nextDurabilityByte == 0)
                {
                    flags = (ushort)(flags | BrokenMask | DegradedMask);
                    if (currentDurability > 0f && brokenCount < BrokenItemHashes.Length)
                        BrokenItemHashes[brokenCount++] = hash;
                }

                if (nextDurability != currentDurability ||
                    nextDurabilityByte != DurabilityBytes[slot] ||
                    nextQualityMilli != QualityMilli[slot] ||
                    flags != ItemStateFlags[slot])
                {
                    ItemDurability[slot] = nextDurability;
                    DurabilityBytes[slot] = nextDurabilityByte;
                    QualityMilli[slot] = nextQualityMilli;
                    ItemStateFlags[slot] = flags;
                    changedCount++;
                }

                totalDurability += nextDurability;
                equippedCount++;
            }

            WriteResult(InventoryCorrosionConstants.ResultChangedCount, changedCount);
            WriteResult(InventoryCorrosionConstants.ResultBrokenCount, brokenCount);
            WriteResult(InventoryCorrosionConstants.ResultAverageDurabilityMilli, equippedCount > 0 ? (int)math.round((totalDurability / equippedCount) * 1000f) : 1000);
            WriteResult(InventoryCorrosionConstants.ResultEquippedCount, equippedCount);
            WriteResult(InventoryCorrosionConstants.ResultScannedCount, scannedCount);
        }

        private void ClearResult()
        {
            int count = Result.IsCreated ? math.min(Result.Length, InventoryCorrosionConstants.ResultRequiredLength) : 0;
            for (int i = 0; i < count; i++)
                Result[i] = 0;

            int brokenCount = BrokenItemHashes.IsCreated ? BrokenItemHashes.Length : 0;
            for (int i = 0; i < brokenCount; i++)
                BrokenItemHashes[i] = 0u;
        }

        private void WriteResult(int index, int value)
        {
            if (Result.IsCreated && (uint)index < (uint)Result.Length)
                Result[index] = value;
        }
    }
}
