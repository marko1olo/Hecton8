using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Scavenging;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DestroyedOrganicEvent
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 NavObstacleCenter;
        [FieldOffset(24)] public float3 NavObstacleExtents;
        [FieldOffset(36)] public float ToolPower;
        [FieldOffset(40)] public float ParentMassKg;
        [FieldOffset(44)] public float Damage01;
        [FieldOffset(48)] public uint InstanceUid;
        [FieldOffset(52)] public int TemplateIndex;
        [FieldOffset(56)] public int MaterialClassId;
        [FieldOffset(60)] private byte _pad0;
        [FieldOffset(61)] private byte _pad1;
        [FieldOffset(62)] private byte _pad2;
        [FieldOffset(63)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct ItemDropData
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public int ItemHashId;
        [FieldOffset(16)] public uint SourceInstanceUid;
        [FieldOffset(20)] public ushort Quantity;
        [FieldOffset(22)] public half QualityFactor;
        [FieldOffset(24)] public byte MaterialClassId;
        [FieldOffset(25)] public byte RarityTier;
        [FieldOffset(26)] private byte _pad0;
        [FieldOffset(27)] private byte _pad1;
        [FieldOffset(28)] private byte _pad2;
        [FieldOffset(29)] private byte _pad3;
        [FieldOffset(30)] private byte _pad4;
        [FieldOffset(31)] private byte _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct EntropyYieldMaterialLutEntry
    {
        [FieldOffset(0)] public float DensityKgPerM3;
        [FieldOffset(4)] public float UnitItemMassKg;
        [FieldOffset(8)] public float MinimumRecovery;
        [FieldOffset(12)] public float QualityBias;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct FractionalDrillingYieldSample
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float ToolPower;
        [FieldOffset(16)] public float NodeHardness;
        [FieldOffset(20)] public float DeltaSeconds;
        [FieldOffset(24)] public float UnitItemMassKg;
        [FieldOffset(28)] public float FractionalMassRemainderKg;
        [FieldOffset(32)] public int ItemHashId;
        [FieldOffset(36)] public uint SourceInstanceUid;
        [FieldOffset(40)] private byte _pad0;
        [FieldOffset(41)] private byte _pad1;
        [FieldOffset(42)] private byte _pad2;
        [FieldOffset(43)] private byte _pad3;
        [FieldOffset(44)] private byte _pad4;
        [FieldOffset(45)] private byte _pad5;
        [FieldOffset(46)] private byte _pad6;
        [FieldOffset(47)] private byte _pad7;
        [FieldOffset(48)] private byte _pad8;
        [FieldOffset(49)] private byte _pad9;
        [FieldOffset(50)] private byte _pad10;
        [FieldOffset(51)] private byte _pad11;
        [FieldOffset(52)] private byte _pad12;
        [FieldOffset(53)] private byte _pad13;
        [FieldOffset(54)] private byte _pad14;
        [FieldOffset(55)] private byte _pad15;
        [FieldOffset(56)] private byte _pad16;
        [FieldOffset(57)] private byte _pad17;
        [FieldOffset(58)] private byte _pad18;
        [FieldOffset(59)] private byte _pad19;
        [FieldOffset(60)] private byte _pad20;
        [FieldOffset(61)] private byte _pad21;
        [FieldOffset(62)] private byte _pad22;
        [FieldOffset(63)] private byte _pad23;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct FractionalDrillingYieldResult
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float FractionalMassRemainderKg;
        [FieldOffset(16)] public int WholeItemCount;
        [FieldOffset(20)] public int ItemHashId;
        [FieldOffset(24)] public uint SourceInstanceUid;
        [FieldOffset(28)] private byte _pad0;
        [FieldOffset(29)] private byte _pad1;
        [FieldOffset(30)] private byte _pad2;
        [FieldOffset(31)] private byte _pad3;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct FractionalDrillingYieldJob : IJobParallelFor
    {
        private const int GramsPerKilogram = 1000;
        private const float KilogramsPerGram = 0.001f;

        [NoAlias] [ReadOnly] public NativeArray<FractionalDrillingYieldSample> Samples;
        [NoAlias] public NativeArray<FractionalDrillingYieldResult> Results;
        public int SampleCount;

        public void Execute(int index)
        {
            if (index >= SampleCount || !Samples.IsCreated || !Results.IsCreated)
                return;

            FractionalDrillingYieldSample sample = Samples[index];
            float unitItemMassKg = math.max(0.01f, sample.UnitItemMassKg);
            float extractedMassKg = math.max(0f, sample.ToolPower) *
                                    math.max(0.01f, sample.NodeHardness) *
                                    math.max(0f, sample.DeltaSeconds);
            long unitItemMassGrams = UnitKilogramsToGrams(unitItemMassKg);
            long remainderGrams = KilogramsToGrams(sample.FractionalMassRemainderKg);
            long extractedGrams = KilogramsToGrams(extractedMassKg);
            long availableGrams = remainderGrams + extractedGrams;
            long wholeItemCountLong = unitItemMassGrams > 0L ? availableGrams / unitItemMassGrams : 0L;
            if (wholeItemCountLong > int.MaxValue)
                wholeItemCountLong = int.MaxValue;

            long consumedGrams = wholeItemCountLong * unitItemMassGrams;
            long remainingGrams = availableGrams > consumedGrams ? availableGrams - consumedGrams : 0L;

            FractionalDrillingYieldResult result = default;
            result.Position = sample.Position;
            result.FractionalMassRemainderKg = remainingGrams * KilogramsPerGram;
            result.WholeItemCount = (int)wholeItemCountLong;
            result.ItemHashId = sample.ItemHashId;
            result.SourceInstanceUid = sample.SourceInstanceUid;
            Results[index] = result;
        }

        private static long KilogramsToGrams(float kilograms)
        {
            if (!math.isfinite(kilograms) || kilograms <= 0f)
                return 0L;

            long grams = (long)math.round(kilograms * GramsPerKilogram);
            return grams > 0L ? grams : 0L;
        }

        private static long UnitKilogramsToGrams(float kilograms)
        {
            long grams = KilogramsToGrams(kilograms);
            return grams > 0L ? grams : 1L;
        }
    }

    /// <summary>
    /// Burst deterministic flora yield generation. One stack-oriented drop record per destroyed instance.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct EntropyYieldJob : IJobParallelFor
    {
        [NoAlias] [ReadOnly] public NativeArray<DestroyedOrganicEvent> Events;
        [NoAlias] [ReadOnly] public NativeArray<HarvestableTemplate.RuntimeDescriptor> TemplateDescriptors;
        [NoAlias] [ReadOnly] public NativeArray<HarvestableTemplate.LootRuntimeEntry> LootEntries;
        [NoAlias] [ReadOnly] public NativeArray<EntropyYieldMaterialLutEntry> MaterialLut;

        // SAFETY: all workers write through a single atomic budget claim; no worker writes DropOutput
        // before Interlocked.Decrement returns a unique slot, and bounds are checked before the store.
        //
        // SAFETY: DropOutput and DropBudget are Vault-pinned only inside the LateFrame batch window.
        // DestructibleOrganicManager invokes Execute directly for the bounded yield slice before
        // releasing the lock, so compaction cannot relocate either buffer while this solver owns a view.
        //
        // SAFETY: the result order is intentionally unstable. The downstream drain treats the buffer as
        // a bounded unordered drop batch, which avoids a serial merge and keeps overflow fail-closed.
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<ItemDropData> DropOutput;
        [NativeDisableParallelForRestriction] public NativeArray<int> DropBudget;
        public int EventCount;

        public void Execute(int index)
        {
            if (index >= EventCount ||
                !Events.IsCreated ||
                !TemplateDescriptors.IsCreated ||
                !LootEntries.IsCreated ||
                !MaterialLut.IsCreated)
            {
                return;
            }

            DestroyedOrganicEvent organicEvent = Events[index];
            if (organicEvent.TemplateIndex < 0 || organicEvent.TemplateIndex >= TemplateDescriptors.Length)
                return;

            HarvestableTemplate.RuntimeDescriptor descriptor = TemplateDescriptors[organicEvent.TemplateIndex];
            if (descriptor.LootCount <= 0)
                return;

            int materialIndex = math.clamp(organicEvent.MaterialClassId, 0, MaterialLut.Length - 1);
            EntropyYieldMaterialLutEntry materialEntry = MaterialLut[materialIndex];
            float densityKgPerM3 = math.max(0.01f, materialEntry.DensityKgPerM3);
            float unitItemMassKg = math.max(0.01f, materialEntry.UnitItemMassKg);
            float toolPower01 = math.saturate(organicEvent.ToolPower);
            float recoveredMassKg = math.max(0.05f, organicEvent.ParentMassKg) *
                math.lerp(math.saturate(materialEntry.MinimumRecovery), 1f, toolPower01);
            float yieldVolumeM3 = recoveredMassKg / densityKgPerM3;

            int lootStart = math.max(0, descriptor.LootStartIndex);
            int lootCount = math.min((int)descriptor.LootCount, LootEntries.Length - lootStart);
            if (lootCount <= 0)
                return;

            uint rng = organicEvent.InstanceUid ^ (uint)descriptor.StableHashId ^ 0x9E3779B9u;
            int totalWeight = 0;
            for (int lootIndex = 0; lootIndex < lootCount; lootIndex++)
                totalWeight += math.max(1, LootEntries[lootStart + lootIndex].Weight);

            if (totalWeight <= 0)
                return;

            int weightedPick = (int)math.floor(Next01(ref rng) * totalWeight);
            int runningWeight = 0;
            int resolvedLootIndex = lootStart;
            int resolvedLootSet = 0;
            for (int lootIndex = 0; lootIndex < lootCount; lootIndex++)
            {
                int candidateIndex = lootStart + lootIndex;
                runningWeight += math.max(1, LootEntries[candidateIndex].Weight);
                bool selectCandidate = resolvedLootSet == 0 & weightedPick < runningWeight;
                resolvedLootIndex = math.select(resolvedLootIndex, candidateIndex, selectCandidate);
                resolvedLootSet = math.select(resolvedLootSet, 1, selectCandidate);
            }

            HarvestableTemplate.LootRuntimeEntry resolvedLoot = LootEntries[resolvedLootIndex];
            int authoredMin = math.max(1, resolvedLoot.MinimumAmount);
            int authoredMax = math.max(authoredMin, resolvedLoot.MaximumAmount);
            int authoredQuantity = authoredMin;
            if (authoredMax > authoredMin)
            {
                authoredQuantity += (int)math.floor((authoredMax - authoredMin + 1) * Next01(ref rng));
                authoredQuantity = math.clamp(authoredQuantity, authoredMin, authoredMax);
            }

            float quality01 = math.saturate(
                Next01(ref rng) * (0.55f + math.saturate(materialEntry.QualityBias)) +
                toolPower01 * 0.25f +
                math.saturate(organicEvent.Damage01) * 0.20f);
            byte rarityTier = ResolveRarityTier(quality01);
            int massQuantity = math.max(1, (int)math.floor(recoveredMassKg / unitItemMassKg));
            int volumeBonus = (int)math.floor(yieldVolumeM3 * 0.35f);
            int rarityBonus = rarityTier >= 3 ? 2 : rarityTier;
            int finalQuantity = authoredQuantity + math.max(0, massQuantity - 1) + volumeBonus + rarityBonus;

            ItemDropData drop = default;
            drop.Position = organicEvent.Position;
            drop.ItemHashId = resolvedLoot.ItemHashId;
            drop.Quantity = (ushort)math.clamp(finalQuantity, 1, ushort.MaxValue);
            drop.QualityFactor = (half)quality01;
            drop.MaterialClassId = (byte)organicEvent.MaterialClassId;
            drop.RarityTier = rarityTier;
            drop.SourceInstanceUid = organicEvent.InstanceUid;
            TryWriteBounded(DropOutput, DropBudget, drop);
        }

        private static unsafe bool TryWriteBounded(
            NativeArray<ItemDropData> output,
            NativeArray<int> writerBudget,
            ItemDropData drop)
        {
            const int remainingIndex = 0;
            const int droppedIndex = 1;
            const int budgetLength = 2;
            if (!output.IsCreated || !writerBudget.IsCreated || writerBudget.Length < budgetLength)
                return false;

            int* budget = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(writerBudget);
            int remainingAfterClaim = Interlocked.Decrement(ref budget[remainingIndex]);
            if (remainingAfterClaim < 0 || remainingAfterClaim >= output.Length)
            {
                Interlocked.Increment(ref budget[droppedIndex]);
                return false;
            }

            int writeIndex = output.Length - 1 - remainingAfterClaim;
            if ((uint)writeIndex >= (uint)output.Length)
            {
                Interlocked.Increment(ref budget[droppedIndex]);
                return false;
            }

            output[writeIndex] = drop;
            return true;
        }

        private static byte ResolveRarityTier(float quality01)
        {
            if (quality01 >= 0.92f)
                return 3;

            if (quality01 >= 0.72f)
                return 2;

            if (quality01 >= 0.42f)
                return 1;

            return 0;
        }

        private static float Next01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }
}
