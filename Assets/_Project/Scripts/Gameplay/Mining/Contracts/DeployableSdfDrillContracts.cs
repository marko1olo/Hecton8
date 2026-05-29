using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Gameplay.Mining.Contracts
{
    internal static class DeployableSdfDrillLayout
    {
        internal const int ExtractionInputStrideBytes = 128;
        internal const int ExtractionResultStrideBytes = 64;
        internal const int MacroRecordStrideBytes = 128;
        internal const int TelemetryEntryStrideBytes = 64;
    }

    /// <summary>
    /// Runtime state bits for a deployable SDF drill.
    /// </summary>
    [Flags]
    public enum DeployableSdfDrillFlags : ushort
    {
        None = 0,
        Active = 1 << 0,
        DormantNoPower = 1 << 1,
        Broken = 1 << 2,
        InventoryFull = 1 << 3,
        SdfVisualDeferred = 1 << 4,
        MacroResident = 1 << 5,
        Snapped = 1 << 6,
        FaultDumped = 1 << 7
    }

    /// <summary>
    /// Math and presentation budget selected for drill extraction.
    /// </summary>
    public enum DeployableSdfDrillMathLod : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Blittable input consumed by the Burst extraction job.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DeployableSdfDrillLayout.ExtractionInputStrideBytes)]
    public struct DeployableSdfDrillExtractionInput
    {
        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public double ElapsedSeconds;
        [FieldOffset(32)] public float LocalX;
        [FieldOffset(36)] public float LocalY;
        [FieldOffset(40)] public float LocalZ;
        [FieldOffset(44)] public float CycleSeconds;
        [FieldOffset(48)] public uint DrillSeed;
        [FieldOffset(52)] public uint SectorHash;
        [FieldOffset(56)] public int BiomeId;
        [FieldOffset(60)] public ushort MaxCycles;
        [FieldOffset(62)] public ushort QuantityPerCycle;
        [FieldOffset(64)] public byte SlotCount;
        [FieldOffset(65)] public byte MathLod;
        [FieldOffset(66)] public ushort Flags;
        [FieldOffset(68)] private uint _pad0;
        [FieldOffset(72)] private ulong _pad1;
        [FieldOffset(80)] private ulong _pad2;
        [FieldOffset(88)] private ulong _pad3;
        [FieldOffset(96)] private ulong _pad4;
        [FieldOffset(104)] private ulong _pad5;
        [FieldOffset(112)] private ulong _pad6;
        [FieldOffset(120)] private ulong _pad7;
    }

    /// <summary>
    /// Blittable extraction result. Slot deltas mirror the exact inventory lanes mutated by the job.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DeployableSdfDrillLayout.ExtractionResultStrideBytes)]
    public struct DeployableSdfDrillExtractionResult
    {
        [FieldOffset(0)] public uint NewSeed;
        [FieldOffset(4)] public uint LastItemHash;
        [FieldOffset(8)] public uint LastOreHash;
        [FieldOffset(12)] public uint Slot0ItemHash;
        [FieldOffset(16)] public uint Slot1ItemHash;
        [FieldOffset(20)] public uint Slot2ItemHash;
        [FieldOffset(24)] public uint Slot3ItemHash;
        [FieldOffset(28)] public uint Slot0OreHash;
        [FieldOffset(32)] public uint Slot1OreHash;
        [FieldOffset(36)] public uint Slot2OreHash;
        [FieldOffset(40)] public uint Slot3OreHash;
        [FieldOffset(44)] public ushort CyclesProcessed;
        [FieldOffset(46)] public ushort TotalQuantity;
        [FieldOffset(48)] public ushort LastSlotIndex;
        [FieldOffset(50)] public ushort Flags;
        [FieldOffset(52)] public ushort Slot0Delta;
        [FieldOffset(54)] public ushort Slot1Delta;
        [FieldOffset(56)] public ushort Slot2Delta;
        [FieldOffset(58)] public ushort Slot3Delta;
        [FieldOffset(60)] private uint _pad0;
    }

    /// <summary>
    /// Macro database record used to dehydrate and rehydrate unloaded drills.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DeployableSdfDrillLayout.MacroRecordStrideBytes)]
    public struct DeployableSdfDrillMacroRecord
    {
        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public double LastUnscaledTimeSeconds;
        [FieldOffset(32)] public float LocalX;
        [FieldOffset(36)] public float LocalY;
        [FieldOffset(40)] public float LocalZ;
        [FieldOffset(44)] public uint DrillSeed;
        [FieldOffset(48)] public uint SectorHash;
        [FieldOffset(52)] public float Health;
        [FieldOffset(56)] public uint OresExtracted;
        [FieldOffset(60)] public ushort Flags;
        [FieldOffset(62)] public ushort Slot0Quantity;
        [FieldOffset(64)] public ushort Slot1Quantity;
        [FieldOffset(66)] public ushort Slot2Quantity;
        [FieldOffset(68)] public ushort Slot3Quantity;
        [FieldOffset(70)] public ushort Slot0Capacity;
        [FieldOffset(72)] public ushort Slot1Capacity;
        [FieldOffset(74)] public ushort Slot2Capacity;
        [FieldOffset(76)] public ushort Slot3Capacity;
        [FieldOffset(78)] private ushort _pad0;
        [FieldOffset(80)] public uint Slot0ItemHash;
        [FieldOffset(84)] public uint Slot1ItemHash;
        [FieldOffset(88)] public uint Slot2ItemHash;
        [FieldOffset(92)] public uint Slot3ItemHash;
        [FieldOffset(96)] public uint Slot0OreHash;
        [FieldOffset(100)] public uint Slot1OreHash;
        [FieldOffset(104)] public uint Slot2OreHash;
        [FieldOffset(108)] public uint Slot3OreHash;
        [FieldOffset(112)] private ulong _pad1;
        [FieldOffset(120)] private ulong _pad2;
    }

    /// <summary>
    /// Fixed-size blackbox entry written by the drill telemetry ring.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DeployableSdfDrillLayout.TelemetryEntryStrideBytes)]
    public struct DeployableSdfDrillTelemetryEntry
    {
        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public float LocalX;
        [FieldOffset(28)] public float LocalY;
        [FieldOffset(32)] public float LocalZ;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public uint ActiveDrills;
        [FieldOffset(44)] public uint OresExtracted;
        [FieldOffset(48)] public ushort FillPermille;
        [FieldOffset(50)] public ushort HealthPermille;
        [FieldOffset(52)] public ushort Flags;
        [FieldOffset(54)] public ushort JobCycles;
        [FieldOffset(56)] private ulong _pad0;
    }

    /// <summary>
    /// Deterministic Burst LCG extraction job for one drill.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DeployableSdfDrillExtractionJob : IJob
    {
        public DeployableSdfDrillExtractionInput Input;
        [NoAlias] public NativeSlice<ushort> Quantities;
        [ReadOnly, NoAlias] public NativeSlice<ushort> Capacities;
        [ReadOnly, NoAlias] public NativeSlice<uint> ItemHashes;
        [ReadOnly, NoAlias] public NativeSlice<uint> OreHashes;
        [NoAlias] public NativeSlice<DeployableSdfDrillExtractionResult> Result;

        public void Execute()
        {
            DeployableSdfDrillExtractionResult result = default;
            uint seed = DeployableSdfDrillMath.Mix(Input.DrillSeed, Input.SectorHash ^ (uint)Input.BiomeId);
            result.NewSeed = seed;

            if (Quantities.Length <= 0 || Capacities.Length <= 0 || ItemHashes.Length <= 0 || OreHashes.Length <= 0 ||
                Result.Length <= 0)
            {
                return;
            }

            int slotCount = math.min(Input.SlotCount, Quantities.Length);
            slotCount = math.min(slotCount, Capacities.Length);
            slotCount = math.min(slotCount, ItemHashes.Length);
            slotCount = math.min(slotCount, OreHashes.Length);
            if (slotCount <= 0)
            {
                Result[0] = result;
                return;
            }

            int cycles = DeployableSdfDrillMath.ResolveCycleCount(Input.ElapsedSeconds, Input.CycleSeconds, Input.MaxCycles);
            if (cycles <= 0)
            {
                Result[0] = result;
                return;
            }

            ushort totalQuantity = 0;
            ushort consumedCycles = 0;
            ushort lastSlotIndex = ushort.MaxValue;
            uint lastItemHash = 0u;
            uint lastOreHash = 0u;
            ushort flags = 0;
            if (IsInventoryFull(slotCount, Quantities, Capacities))
            {
                result.Flags = (ushort)DeployableSdfDrillFlags.InventoryFull;
                result.NewSeed = seed;
                Result[0] = result;
                return;
            }

            for (int cycle = 0; cycle < cycles; cycle++)
            {
                consumedCycles++;
                seed = DeployableSdfDrillMath.NextLcg(seed + (uint)cycle + 1u);
                int slotIndex = DeployableSdfDrillMath.ResolveOreSlot(seed, Input.BiomeId, slotCount);
                ushort capacity = Capacities[slotIndex];
                ushort current = Quantities[slotIndex];
                if (capacity <= current)
                    continue;

                ushort free = (ushort)(capacity - current);
                ushort request = Input.QuantityPerCycle == 0 ? (ushort)1 : Input.QuantityPerCycle;
                ushort grant = request <= free ? request : free;
                Quantities[slotIndex] = (ushort)(current + grant);

                totalQuantity = (ushort)math.min(ushort.MaxValue, (int)totalQuantity + grant);
                lastSlotIndex = (ushort)slotIndex;
                lastItemHash = ItemHashes[slotIndex];
                lastOreHash = DeployableSdfDrillMath.ResolveOreHash(OreHashes[slotIndex]);
                AccumulateSlotDelta(ref result, slotIndex, grant, lastItemHash, lastOreHash);
            }

            if (IsInventoryFull(slotCount, Quantities, Capacities))
                flags = (ushort)(flags | (ushort)DeployableSdfDrillFlags.InventoryFull);

            result.NewSeed = seed;
            result.LastItemHash = lastItemHash;
            result.LastOreHash = lastOreHash;
            result.CyclesProcessed = consumedCycles;
            result.TotalQuantity = totalQuantity;
            result.LastSlotIndex = lastSlotIndex;
            result.Flags = flags;
            Result[0] = result;
        }

        private static bool IsInventoryFull(
            int slotCount,
            NativeSlice<ushort> quantities,
            NativeSlice<ushort> capacities)
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (capacities[i] > quantities[i])
                    return false;
            }

            return true;
        }

        private static void AccumulateSlotDelta(
            ref DeployableSdfDrillExtractionResult result,
            int slotIndex,
            ushort grant,
            uint itemHash,
            uint oreHash)
        {
            switch (slotIndex)
            {
                case 0:
                    result.Slot0Delta = AddSaturating(result.Slot0Delta, grant);
                    result.Slot0ItemHash = itemHash;
                    result.Slot0OreHash = oreHash;
                    break;
                case 1:
                    result.Slot1Delta = AddSaturating(result.Slot1Delta, grant);
                    result.Slot1ItemHash = itemHash;
                    result.Slot1OreHash = oreHash;
                    break;
                case 2:
                    result.Slot2Delta = AddSaturating(result.Slot2Delta, grant);
                    result.Slot2ItemHash = itemHash;
                    result.Slot2OreHash = oreHash;
                    break;
                default:
                    result.Slot3Delta = AddSaturating(result.Slot3Delta, grant);
                    result.Slot3ItemHash = itemHash;
                    result.Slot3OreHash = oreHash;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort AddSaturating(ushort current, ushort delta)
        {
            return (ushort)math.min(ushort.MaxValue, (int)current + delta);
        }
    }

    /// <summary>
    /// Burst-compatible hashing and cycle-count helpers for deployable drill extraction.
    /// </summary>
    public static class DeployableSdfDrillMath
    {
        public const uint DefaultOreHash = 0xA826F165u;
        public const uint DefaultItemHash = 0x5B5A0D13u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint a, uint b)
        {
            uint h = a ^ 0x9E3779B9u;
            h ^= b + 0x85EBCA6Bu + (h << 6) + (h >> 2);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h == 0u ? 0xA341316Cu : h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint NextLcg(uint state)
        {
            return (state * 1664525u) + 1013904223u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ResolveSectorHash(long gridX, long gridY, long gridZ, float localX, float localY, float localZ)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, (uint)gridX);
            hash = Mix(hash, (uint)(gridX >> 32));
            hash = Mix(hash, (uint)gridY);
            hash = Mix(hash, (uint)(gridY >> 32));
            hash = Mix(hash, (uint)gridZ);
            hash = Mix(hash, (uint)(gridZ >> 32));
            hash = Mix(hash, (uint)math.floor(localX * 0.125f));
            hash = Mix(hash, (uint)math.floor(localY * 0.125f));
            hash = Mix(hash, (uint)math.floor(localZ * 0.125f));
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveCycleCount(double elapsedSeconds, float cycleSeconds, ushort maxCycles)
        {
            if (elapsedSeconds < cycleSeconds || cycleSeconds <= 0.001f || maxCycles == 0)
                return 0;

            double cycles = math.floor(elapsedSeconds * math.rcp((double)cycleSeconds));
            return (int)math.clamp(cycles, 0.0, maxCycles);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveOreSlot(uint seed, int biomeId, int slotCount)
        {
            int safeSlotCount = math.max(1, slotCount);
            uint mixed = Mix(seed, (uint)biomeId);
            return (int)(mixed % (uint)safeSlotCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ResolveOreHash(uint configuredOreHash)
        {
            return configuredOreHash != 0u ? configuredOreHash : DefaultOreHash;
        }
    }
}
