using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Data
{
    /// <summary>
    /// Burst job that unpacks creature genome records from the monolithic AoS blob into SoA arrays.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct H8CreatureSoAReconstructJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<byte> Blob;
        public int CreatureSectionOffsetBytes;
        public int CreatureCount;

        [WriteOnly, NoAlias] public NativeArray<float> Aggressions;
        [WriteOnly, NoAlias] public NativeArray<float> Metabolisms;
        [WriteOnly, NoAlias] public NativeArray<float> HealthCaps;
        [WriteOnly, NoAlias] public NativeArray<float> CruiseSpeeds;
        [WriteOnly, NoAlias] public NativeArray<float> BurstSpeeds;
        [WriteOnly, NoAlias] public NativeArray<uint> MateMasks;

        /// <summary>
        /// Unpacks one creature record into parallel arrays.
        /// </summary>
        /// <param name="index">Record index.</param>
        public void Execute(int index)
        {
            if ((uint)index >= (uint)CreatureCount)
                return;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Blob);
            byte* recordPtr = basePtr + CreatureSectionOffsetBytes + (index * H8DataLayoutConstants.CreatureTraitRecordSize);
            H8CreatureTraitRecord record = UnsafeUtility.ReadArrayElement<H8CreatureTraitRecord>(recordPtr, 0);

            Aggressions[index] = math.isfinite(record.Genome.Aggression) ? record.Genome.Aggression : 0f;
            Metabolisms[index] = math.isfinite(record.Genome.Metabolism) ? record.Genome.Metabolism : 1f;
            HealthCaps[index] = math.isfinite(record.Genome.MaxHealth) ? record.Genome.MaxHealth : 1f;
            CruiseSpeeds[index] = math.isfinite(record.Genome.CruiseSpeed) ? record.Genome.CruiseSpeed : 0f;
            BurstSpeeds[index] = math.isfinite(record.Genome.BurstSpeed) ? record.Genome.BurstSpeed : 0f;
            MateMasks[index] = record.MateMask;
        }
    }

    /// <summary>
    /// Burst job that unpacks item records from the monolithic AoS blob into cache-linear SoA arrays.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct H8ItemSoAReconstructJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<byte> Blob;
        public int ItemSectionOffsetBytes;
        public int ItemCount;

        [WriteOnly, NoAlias] public NativeArray<uint> HashIds;
        [WriteOnly, NoAlias] public NativeArray<uint> CategoryHashes;
        [WriteOnly, NoAlias] public NativeArray<ushort> MaxStacks;
        [WriteOnly, NoAlias] public NativeArray<ulong> RecipeMask0;
        [WriteOnly, NoAlias] public NativeArray<ulong> RecipeMask1;
        [WriteOnly, NoAlias] public NativeArray<float> MassKg;
        [WriteOnly, NoAlias] public NativeArray<float> VolumeM3;
        [WriteOnly, NoAlias] public NativeArray<float> BaseQualities;
        [WriteOnly, NoAlias] public NativeArray<float> HeatCapacities;

        /// <summary>
        /// Unpacks one item record into parallel arrays.
        /// </summary>
        /// <param name="index">Record index.</param>
        public void Execute(int index)
        {
            if ((uint)index >= (uint)ItemCount)
                return;

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Blob);
            byte* recordPtr = basePtr + ItemSectionOffsetBytes + (index * H8DataLayoutConstants.ItemRecordSize);
            H8ItemRecord record = UnsafeUtility.ReadArrayElement<H8ItemRecord>(recordPtr, 0);

            HashIds[index] = record.HashId;
            CategoryHashes[index] = record.CategoryHash;
            MaxStacks[index] = record.MaxStack;
            RecipeMask0[index] = record.RecipeMask0;
            RecipeMask1[index] = record.RecipeMask1;
            MassKg[index] = math.isfinite(record.MassKg) ? record.MassKg : 0f;
            VolumeM3[index] = math.isfinite(record.VolumeM3) ? record.VolumeM3 : 0f;
            BaseQualities[index] = math.isfinite(record.BaseQuality) ? record.BaseQuality : 1f;
            HeatCapacities[index] = math.isfinite(record.HeatCapacity) ? record.HeatCapacity : 0f;
        }
    }
}
