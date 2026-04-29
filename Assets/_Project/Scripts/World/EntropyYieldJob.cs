using System.Runtime.InteropServices;
using Hecton8.Scavenging;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct DestroyedOrganicEvent
    {
        public float3 Position;
        public float ToolPower;
        public float ParentMassKg;
        public float Damage01;
        public uint InstanceUid;
        public int TemplateIndex;
        public int MaterialClassId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct ItemDropData
    {
        public float3 Position;
        public int ItemHashId;
        public ushort Quantity;
        public half QualityFactor;
        public byte MaterialClassId;
        public byte RarityTier;
        public ushort Reserved0;
        public uint SourceInstanceUid;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct EntropyYieldMaterialLutEntry
    {
        public float DensityKgPerM3;
        public float UnitItemMassKg;
        public float MinimumRecovery;
        public float QualityBias;
    }

    /// <summary>
    /// Burst deterministic flora yield generation. One stack-oriented drop record per destroyed instance.
    /// </summary>
    [BurstCompile]
    internal struct EntropyYieldJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<DestroyedOrganicEvent> Events;
        [ReadOnly] public NativeArray<HarvestableTemplate.RuntimeDescriptor> TemplateDescriptors;
        [ReadOnly] public NativeArray<HarvestableTemplate.LootRuntimeEntry> LootEntries;
        [ReadOnly] public NativeArray<EntropyYieldMaterialLutEntry> MaterialLut;
        public NativeQueue<ItemDropData>.ParallelWriter DropWriter;
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
            HarvestableTemplate.LootRuntimeEntry resolvedLoot = LootEntries[lootStart];
            for (int lootIndex = 0; lootIndex < lootCount; lootIndex++)
            {
                HarvestableTemplate.LootRuntimeEntry candidate = LootEntries[lootStart + lootIndex];
                runningWeight += math.max(1, candidate.Weight);
                if (weightedPick < runningWeight)
                {
                    resolvedLoot = candidate;
                    break;
                }
            }

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

            DropWriter.Enqueue(new ItemDropData
            {
                Position = organicEvent.Position,
                ItemHashId = resolvedLoot.ItemHashId,
                Quantity = (ushort)math.clamp(finalQuantity, 1, ushort.MaxValue),
                QualityFactor = (half)quality01,
                MaterialClassId = (byte)organicEvent.MaterialClassId,
                RarityTier = rarityTier,
                Reserved0 = 0,
                SourceInstanceUid = organicEvent.InstanceUid
            });
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
