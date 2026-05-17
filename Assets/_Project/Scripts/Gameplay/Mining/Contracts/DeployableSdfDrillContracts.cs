using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Gameplay.Mining.Contracts
{
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
        LowTierSdfSkipped = 1 << 4,
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 68)]
    public struct DeployableSdfDrillExtractionInput
    {
        public long GridX;
        public long GridY;
        public long GridZ;
        public float LocalX;
        public float LocalY;
        public float LocalZ;
        public double ElapsedSeconds;
        public float CycleSeconds;
        public uint DrillSeed;
        public uint SectorHash;
        public int BiomeId;
        public ushort MaxCycles;
        public ushort QuantityPerCycle;
        public byte SlotCount;
        public byte MathLod;
        public ushort Flags;
    }

    /// <summary>
    /// Blittable extraction result. Slot deltas mirror the exact inventory lanes mutated by the job.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 60)]
    public struct DeployableSdfDrillExtractionResult
    {
        public uint NewSeed;
        public uint LastItemHash;
        public uint LastOreHash;
        public ushort CyclesProcessed;
        public ushort TotalQuantity;
        public ushort LastSlotIndex;
        public ushort Flags;
        public ushort Slot0Delta;
        public ushort Slot1Delta;
        public ushort Slot2Delta;
        public ushort Slot3Delta;
        public uint Slot0ItemHash;
        public uint Slot1ItemHash;
        public uint Slot2ItemHash;
        public uint Slot3ItemHash;
        public uint Slot0OreHash;
        public uint Slot1OreHash;
        public uint Slot2OreHash;
        public uint Slot3OreHash;
    }

    /// <summary>
    /// Macro database record used to dehydrate and rehydrate unloaded drills.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 110)]
    public struct DeployableSdfDrillMacroRecord
    {
        public long GridX;
        public long GridY;
        public long GridZ;
        public float LocalX;
        public float LocalY;
        public float LocalZ;
        public double LastUnscaledTimeSeconds;
        public uint DrillSeed;
        public uint SectorHash;
        public float Health;
        public ushort Flags;
        public ushort Slot0Quantity;
        public ushort Slot1Quantity;
        public ushort Slot2Quantity;
        public ushort Slot3Quantity;
        public ushort Slot0Capacity;
        public ushort Slot1Capacity;
        public ushort Slot2Capacity;
        public ushort Slot3Capacity;
        public uint Slot0ItemHash;
        public uint Slot1ItemHash;
        public uint Slot2ItemHash;
        public uint Slot3ItemHash;
        public uint Slot0OreHash;
        public uint Slot1OreHash;
        public uint Slot2OreHash;
        public uint Slot3OreHash;
        public uint OresExtracted;
    }

    /// <summary>
    /// Fixed-size blackbox entry written by the drill telemetry ring.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 56)]
    public struct DeployableSdfDrillTelemetryEntry
    {
        public long GridX;
        public long GridY;
        public long GridZ;
        public float LocalX;
        public float LocalY;
        public float LocalZ;
        public uint Frame;
        public uint ActiveDrills;
        public uint OresExtracted;
        public ushort FillPermille;
        public ushort HealthPermille;
        public ushort Flags;
        public ushort JobCycles;
    }

    /// <summary>
    /// Deterministic Burst LCG extraction job for one drill.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DeployableSdfDrillExtractionJob : IJob
    {
        public DeployableSdfDrillExtractionInput Input;
        public NativeSlice<ushort> Quantities;
        [ReadOnly] public NativeSlice<ushort> Capacities;
        [ReadOnly] public NativeSlice<uint> ItemHashes;
        [ReadOnly] public NativeSlice<uint> OreHashes;
        public NativeSlice<DeployableSdfDrillExtractionResult> Result;

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
