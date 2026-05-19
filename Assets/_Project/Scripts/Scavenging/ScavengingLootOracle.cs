using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Generated;
using Hecton8.Core.Memory;
using Hecton8.Inventory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>Visual-only scavenging pickup fake. Size: 80 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct VisualScavengeSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public ulong ResourceNodeHash;
        [FieldOffset(56)] public uint ItemHashID;
        [FieldOffset(60)] public uint OreHash;
        [FieldOffset(64)] public uint Quantity;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public float VfxEmissionMultiplier;
        [FieldOffset(76)] public byte SourceKind;
        [FieldOffset(77)] public byte Flags;
        [FieldOffset(78)] public ushort _pad0;
    }
}

namespace Hecton8.Scavenging
{
    /// <summary>
    /// Deterministic loot oracle constants. Item truth is inventory signal data; visuals are fake.
    /// </summary>
    public static class ScavengingLootOracleConstants
    {
        public const int LootTableEntrySizeBytes = 16;
        public const int HarvestRequestSizeBytes = 128;
        public const int ResolvedYieldSizeBytes = 96;
        public const int InventoryCapacitySizeBytes = 16;
        public const int BiomeModifierSizeBytes = 16;
        public const int TelemetryEntrySizeBytes = 128;
        public const int TelemetryRingCapacity = 300;
        public const int DefaultLootEntryCapacity = 256;
        public const int DefaultRequestCapacity = 64;
        public const int DefaultBiomeModifierCapacity = 128;
        public const int DefaultAuditCapacity = 32;
        public const int DefaultCsvScratchBytes = 64 * 1024;
        public const int SelfAuditRollCount = 10000;
        public const byte ItemSourceKind = 9;
        public const byte VisualSourceKind = 9;
        public const byte HudSeverityWarning = 2;
        public const uint ToolMaskAny = 0u;
        public const uint ToolMaskKnife = 1u << 0;
        public const uint ToolMaskCutter = 1u << 1;
        public const uint ToolMaskDrill = 1u << 2;
        public const uint ToolMaskExtractor = 1u << 3;
        public const uint RequestFlagInventoryFull = 1u << 0;
        public const uint RequestFlagForcedItem = 1u << 1;
        public const uint ResultFlagResolved = 1u << 0;
        public const uint ResultFlagInventoryFull = 1u << 1;
        public const uint ResultFlagNoEligibleEntry = 1u << 2;
        public const uint ResultFlagForcedItem = 1u << 3;
        public const uint InventoryFullMessageHash = 0x4946554Cu; // IFUL
        public const uint VisualScavengeLaneHash = 0x56534356u; // VSCV
        public const uint LootOracleSourceHash = 0x4C4F5243u; // LORC
        public const uint EmergencyTableHash = 0x454D4C54u; // EMLT
        public const uint DefaultSessionSalt = 0x1251255Du;
        public const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_LOOT_ORACLE.bin";

        public static readonly BufferID LootEntriesBufferId = (BufferID)70930;
        public static readonly BufferID HarvestRequestsBufferId = (BufferID)70931;
        public static readonly BufferID ResolvedYieldsBufferId = (BufferID)70932;
        public static readonly BufferID BiomeModifiersBufferId = (BufferID)70933;
        public static readonly BufferID TelemetryRingBufferId = (BufferID)70934;
        public static readonly BufferID DistributionAuditBufferId = (BufferID)70935;
        public static readonly BufferID CsvScratchBufferId = (BufferID)70936;
    }

    /// <summary>Cumulative integer CDF entry. Size: 16 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = ScavengingLootOracleConstants.LootTableEntrySizeBytes)]
    public struct LootTableEntryDTO
    {
        [FieldOffset(0)] public uint ItemHashID;
        [FieldOffset(4)] public uint DropWeight;
        [FieldOffset(8)] public uint ConditionMask;
        [FieldOffset(12)] public uint _pad0;
    }

    /// <summary>Read-only inventory capacity snapshot. Size: 16 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = ScavengingLootOracleConstants.InventoryCapacitySizeBytes)]
    public struct InventoryCapacityDTO
    {
        [FieldOffset(0)] public ushort FreeSlots;
        [FieldOffset(2)] public ushort FreeStackCapacity;
        [FieldOffset(4)] public uint InventoryHash;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint _pad0;
    }

    /// <summary>Biome/item-specific milli-scalar. Size: 16 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = ScavengingLootOracleConstants.BiomeModifierSizeBytes)]
    public struct ScavengingBiomeModifierDTO
    {
        [FieldOffset(0)] public uint BiomeHash;
        [FieldOffset(4)] public uint ItemHashID;
        [FieldOffset(8)] public uint WeightMultiplierMilli;
        [FieldOffset(12)] public uint _pad0;
    }

    /// <summary>Harvest request passed into Burst loot resolution. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = ScavengingLootOracleConstants.HarvestRequestSizeBytes)]
    public struct ScavengingHarvestRequestDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePosition NodeAup;
        [FieldOffset(48)] public ulong SessionID;
        [FieldOffset(56)] public ulong ResourceNodeHash;
        [FieldOffset(64)] public uint OreHash;
        [FieldOffset(68)] public uint ToolHashID;
        [FieldOffset(72)] public uint BiomeHash;
        [FieldOffset(76)] public uint TableHash;
        [FieldOffset(80)] public uint TableVersion;
        [FieldOffset(84)] public uint RollIndex;
        [FieldOffset(88)] public uint LootStartIndex;
        [FieldOffset(92)] public uint LootEntryCount;
        [FieldOffset(96)] public uint QuantityMin;
        [FieldOffset(100)] public uint QuantityMax;
        [FieldOffset(104)] public uint ForcedItemHashID;
        [FieldOffset(108)] public float GlobalQualityWeight;
        [FieldOffset(112)] public InventoryCapacityDTO Capacity;
    }

    /// <summary>Resolved deterministic yield. Size: 96 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = ScavengingLootOracleConstants.ResolvedYieldSizeBytes)]
    public struct ScavengingResolvedYieldDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePosition NodeAup;
        [FieldOffset(48)] public ulong ResourceNodeHash;
        [FieldOffset(56)] public uint ItemHashID;
        [FieldOffset(60)] public uint OreHash;
        [FieldOffset(64)] public uint Quantity;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public float VfxEmissionMultiplier;
        [FieldOffset(76)] public uint Roll;
        [FieldOffset(80)] public uint TotalWeight;
        [FieldOffset(84)] public ushort DepletionWordIndex;
        [FieldOffset(86)] public byte SourceKind;
        [FieldOffset(87)] public byte Flags;
        [FieldOffset(88)] public uint TableHash;
        [FieldOffset(92)] public uint RequestId;
    }

    /// <summary>Scavenging black-box ring entry. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = ScavengingLootOracleConstants.TelemetryEntrySizeBytes)]
    public struct ScavengingTelemetryEntry
    {
        [FieldOffset(0)] public AbsoluteUniversePosition NodeAup;
        [FieldOffset(48)] public ulong ResourceNodeHash;
        [FieldOffset(56)] public uint SelectedItemHashID;
        [FieldOffset(60)] public uint OreHash;
        [FieldOffset(64)] public uint Frame;
        [FieldOffset(68)] public uint TotalWeight;
        [FieldOffset(72)] public uint Roll;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public uint EstimatedCpuMicroseconds;
        [FieldOffset(84)] public uint TableHash;
        [FieldOffset(88)] public uint RequestId;
        [FieldOffset(92)] public float GlobalQualityWeight;
        [FieldOffset(96)] public uint DepletionWordIndex;
        [FieldOffset(100)] public uint DistributionBucket;
        [FieldOffset(104)] public ulong DepletionMask;
        [FieldOffset(112)] public ulong _pad0;
        [FieldOffset(120)] public ulong _pad1;
    }

    public struct ScavengingLootOracleVaultViews
    {
        public NativeArray<LootTableEntryDTO> LootEntries;
        public NativeArray<ScavengingHarvestRequestDTO> Requests;
        public NativeArray<ScavengingResolvedYieldDTO> ResolvedYields;
        public NativeArray<ScavengingBiomeModifierDTO> BiomeModifiers;
        public NativeArray<ScavengingTelemetryEntry> TelemetryRing;
        public NativeArray<uint> DistributionAudit;
        public NativeArray<byte> CsvScratch;

        public bool HasAllBuffers()
        {
            return LootEntries.IsCreated &&
                   Requests.IsCreated &&
                   ResolvedYields.IsCreated &&
                   BiomeModifiers.IsCreated &&
                   TelemetryRing.IsCreated &&
                   DistributionAudit.IsCreated &&
                   CsvScratch.IsCreated;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateEmergencyMockLootTablesJob : IJob
    {
        [NoAlias] public NativeArray<LootTableEntryDTO> LootEntries;

        public void Execute()
        {
            if (!LootEntries.IsCreated || LootEntries.Length < 4)
                return;

            LootEntries[0] = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.TitaniumScrapHash,
                DropWeight = 55u,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskAny,
                _pad0 = 0u
            };
            LootEntries[1] = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.CopperOreHash,
                DropWeight = 82u,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskAny,
                _pad0 = 0u
            };
            LootEntries[2] = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.SulfurClumpsHash,
                DropWeight = 94u,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskCutter | ScavengingLootOracleConstants.ToolMaskDrill | ScavengingLootOracleConstants.ToolMaskExtractor,
                _pad0 = 0u
            };
            LootEntries[3] = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.AbyssalCrystalHash,
                DropWeight = 100u,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskDrill | ScavengingLootOracleConstants.ToolMaskExtractor,
                _pad0 = 0u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockHarvestRequestJob : IJob
    {
        [NoAlias] public NativeArray<ScavengingHarvestRequestDTO> Requests;
        public uint RequestCount;
        public uint Frame;
        public ulong SessionID;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!Requests.IsCreated)
                return;

            int count = (int)math.min(RequestCount, (uint)Requests.Length);
            for (int i = 0; i < count; i++)
            {
                AbsoluteUniversePosition aup = default;
                aup.GridX = 12L + i;
                aup.GridY = -3L;
                aup.GridZ = 44L;
                aup.LocalX = 0.125f * i;
                aup.LocalY = 0.25f;
                aup.LocalZ = -0.375f;

                Requests[i] = new ScavengingHarvestRequestDTO
                {
                    NodeAup = aup,
                    SessionID = SessionID,
                    ResourceNodeHash = 0UL,
                    OreHash = H8Hashes.Items.TitaniumScrapHash,
                    ToolHashID = ScavengingLootOracleConstants.ToolMaskDrill,
                    BiomeHash = 0u,
                    TableHash = ScavengingLootOracleConstants.EmergencyTableHash,
                    TableVersion = 1u,
                    RollIndex = unchecked((uint)i),
                    LootStartIndex = 0u,
                    LootEntryCount = 4u,
                    QuantityMin = 1u,
                    QuantityMax = 1u,
                    ForcedItemHashID = 0u,
                    GlobalQualityWeight = math.saturate(GlobalQualityWeight),
                    Capacity = new InventoryCapacityDTO
                    {
                        FreeSlots = 1,
                        FreeStackCapacity = 1,
                        InventoryHash = 0u,
                        Flags = 0u,
                        _pad0 = Frame
                    }
                };
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct LootResolutionJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ScavengingHarvestRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<LootTableEntryDTO> LootEntries;
        [ReadOnly, NoAlias] public NativeArray<ScavengingBiomeModifierDTO> BiomeModifiers;
        [NoAlias] public NativeArray<ScavengingResolvedYieldDTO> ResolvedYields;
        [NoAlias] public NativeArray<ScavengingTelemetryEntry> TelemetryRing;
        public int RequestCount;
        public uint Frame;
        public int TelemetryCursor;

        public void Execute()
        {
            if (!Requests.IsCreated || !ResolvedYields.IsCreated)
                return;

            int count = math.min(RequestCount, math.min(Requests.Length, ResolvedYields.Length));
            for (int i = 0; i < count; i++)
            {
                ScavengingHarvestRequestDTO request = Requests[i];
                ScavengingResolvedYieldDTO result = ResolveRequest(in request, i);
                ResolvedYields[i] = result;
                WriteTelemetry(in request, in result, i);
            }
        }

        private ScavengingResolvedYieldDTO ResolveRequest(in ScavengingHarvestRequestDTO request, int requestIndex)
        {
            float quality = math.saturate(math.select(1f, request.GlobalQualityWeight, math.isfinite(request.GlobalQualityWeight)));
            ulong resourceHash = request.ResourceNodeHash != 0UL
                ? request.ResourceNodeHash
                : BuildResourceNodeHash(in request.NodeAup, request.OreHash);
            ushort wordIndex = unchecked((ushort)((resourceHash >> 6) & 0xFFFFUL));
            ScavengingResolvedYieldDTO result = new ScavengingResolvedYieldDTO
            {
                NodeAup = request.NodeAup,
                ResourceNodeHash = resourceHash,
                ItemHashID = 0u,
                OreHash = request.OreHash,
                Quantity = 0u,
                Frame = Frame,
                VfxEmissionMultiplier = math.lerp(0.1f, 1.0f, quality),
                Roll = 0u,
                TotalWeight = 0u,
                DepletionWordIndex = wordIndex,
                SourceKind = ScavengingLootOracleConstants.ItemSourceKind,
                Flags = 0,
                TableHash = request.TableHash,
                RequestId = unchecked((uint)requestIndex)
            };

            if ((request.Capacity.Flags & ScavengingLootOracleConstants.RequestFlagInventoryFull) != 0u ||
                (request.Capacity.FreeSlots == 0 && request.Capacity.FreeStackCapacity == 0))
            {
                result.Flags = (byte)ScavengingLootOracleConstants.ResultFlagInventoryFull;
                return result;
            }

            uint quantityMin = math.max(1u, request.QuantityMin);
            uint quantityMax = math.max(quantityMin, request.QuantityMax);
            uint seed = BuildDeterministicSeed(in request, resourceHash);
            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(seed | 1u);
            uint rollBits = random.NextUInt();
            uint quantityRange = quantityMax - quantityMin + 1u;
            uint quantity = quantityMin + MapUIntToRange(random.NextUInt(), quantityRange);

            if (request.ForcedItemHashID != 0u || (request.Capacity.Flags & ScavengingLootOracleConstants.RequestFlagForcedItem) != 0u)
            {
                result.ItemHashID = request.ForcedItemHashID != 0u ? request.ForcedItemHashID : request.OreHash;
                result.Quantity = quantity;
                result.Roll = rollBits;
                result.TotalWeight = 1u;
                result.Flags = (byte)(ScavengingLootOracleConstants.ResultFlagResolved | ScavengingLootOracleConstants.ResultFlagForcedItem);
                return result;
            }

            uint start = math.min(request.LootStartIndex, (uint)LootEntries.Length);
            uint requestedCount = request.LootEntryCount != 0u ? request.LootEntryCount : (uint)LootEntries.Length;
            uint entryCount = math.min(requestedCount, (uint)LootEntries.Length - start);
            if (entryCount == 0u)
            {
                result.Flags = (byte)ScavengingLootOracleConstants.ResultFlagNoEligibleEntry;
                return result;
            }

            bool hasBiomeModifier = HasBiomeModifier(request.BiomeHash);
            if (!hasBiomeModifier)
            {
                uint totalWeight = ResolveBaseTotalWeight(start, entryCount, request.ToolHashID);
                if (totalWeight == 0u)
                {
                    result.Flags = (byte)ScavengingLootOracleConstants.ResultFlagNoEligibleEntry;
                    return result;
                }

                uint threshold = MapUIntToRange(rollBits, totalWeight);
                uint selectedIndex = BinarySearchEligible(start, entryCount, request.ToolHashID, threshold);
                LootTableEntryDTO selected = LootEntries[(int)selectedIndex];
                result.ItemHashID = selected.ItemHashID;
                result.Quantity = quantity;
                result.Roll = threshold;
                result.TotalWeight = totalWeight;
                result.Flags = (byte)ScavengingLootOracleConstants.ResultFlagResolved;
                return result;
            }

            uint modifiedTotal = ResolveModifiedTotalWeight(start, entryCount, request.ToolHashID, request.BiomeHash);
            if (modifiedTotal == 0u)
            {
                result.Flags = (byte)ScavengingLootOracleConstants.ResultFlagNoEligibleEntry;
                return result;
            }

            uint modifiedThreshold = MapUIntToRange(rollBits, modifiedTotal);
            uint itemHash = SelectModifiedItem(start, entryCount, request.ToolHashID, request.BiomeHash, modifiedThreshold);
            result.ItemHashID = itemHash;
            result.Quantity = quantity;
            result.Roll = modifiedThreshold;
            result.TotalWeight = modifiedTotal;
            result.Flags = itemHash != 0u
                ? (byte)ScavengingLootOracleConstants.ResultFlagResolved
                : (byte)ScavengingLootOracleConstants.ResultFlagNoEligibleEntry;
            return result;
        }

        private void WriteTelemetry(in ScavengingHarvestRequestDTO request, in ScavengingResolvedYieldDTO result, int index)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length == 0)
                return;

            int slot = PositiveMod(TelemetryCursor + index, TelemetryRing.Length);
            TelemetryRing[slot] = new ScavengingTelemetryEntry
            {
                NodeAup = result.NodeAup,
                ResourceNodeHash = result.ResourceNodeHash,
                SelectedItemHashID = result.ItemHashID,
                OreHash = result.OreHash,
                Frame = Frame,
                TotalWeight = result.TotalWeight,
                Roll = result.Roll,
                Flags = result.Flags,
                EstimatedCpuMicroseconds = 5u,
                TableHash = request.TableHash,
                RequestId = result.RequestId,
                GlobalQualityWeight = math.saturate(request.GlobalQualityWeight),
                DepletionWordIndex = result.DepletionWordIndex,
                DistributionBucket = result.TotalWeight > 0u ? result.Roll : 0u,
                DepletionMask = 1UL << (int)(result.ResourceNodeHash & 63UL),
                _pad0 = 0UL,
                _pad1 = 0UL
            };
        }

        private uint ResolveBaseTotalWeight(uint start, uint entryCount, uint toolMask)
        {
            uint previousCdf = 0u;
            uint total = 0u;
            for (uint i = 0u; i < entryCount; i++)
            {
                LootTableEntryDTO entry = LootEntries[(int)(start + i)];
                uint weight = entry.DropWeight > previousCdf ? entry.DropWeight - previousCdf : 0u;
                previousCdf = math.max(previousCdf, entry.DropWeight);
                total += PassesToolMask(entry.ConditionMask, toolMask) ? weight : 0u;
            }

            return total;
        }

        private uint BinarySearchEligible(uint start, uint entryCount, uint toolMask, uint threshold)
        {
            uint low = 0u;
            uint high = entryCount;
            while (low < high)
            {
                uint mid = low + ((high - low) >> 1);
                uint prefix = ResolveBasePrefixWeight(start, mid + 1u, toolMask);
                if (threshold < prefix)
                    high = mid;
                else
                    low = mid + 1u;
            }

            return start + math.min(low, entryCount - 1u);
        }

        private uint ResolveBasePrefixWeight(uint start, uint count, uint toolMask)
        {
            uint previousCdf = 0u;
            uint total = 0u;
            for (uint i = 0u; i < count; i++)
            {
                LootTableEntryDTO entry = LootEntries[(int)(start + i)];
                uint weight = entry.DropWeight > previousCdf ? entry.DropWeight - previousCdf : 0u;
                previousCdf = math.max(previousCdf, entry.DropWeight);
                total += PassesToolMask(entry.ConditionMask, toolMask) ? weight : 0u;
            }

            return total;
        }

        private uint ResolveModifiedTotalWeight(uint start, uint entryCount, uint toolMask, uint biomeHash)
        {
            uint previousCdf = 0u;
            ulong total = 0UL;
            for (uint i = 0u; i < entryCount; i++)
            {
                LootTableEntryDTO entry = LootEntries[(int)(start + i)];
                uint weight = entry.DropWeight > previousCdf ? entry.DropWeight - previousCdf : 0u;
                previousCdf = math.max(previousCdf, entry.DropWeight);
                if (!PassesToolMask(entry.ConditionMask, toolMask))
                    continue;

                uint multiplier = ResolveBiomeMultiplierMilli(biomeHash, entry.ItemHashID);
                total += ((ulong)weight * multiplier + 999UL) / 1000UL;
            }

            return (uint)math.min(total, (ulong)uint.MaxValue);
        }

        private uint SelectModifiedItem(uint start, uint entryCount, uint toolMask, uint biomeHash, uint threshold)
        {
            uint previousCdf = 0u;
            ulong cumulative = 0UL;
            for (uint i = 0u; i < entryCount; i++)
            {
                LootTableEntryDTO entry = LootEntries[(int)(start + i)];
                uint weight = entry.DropWeight > previousCdf ? entry.DropWeight - previousCdf : 0u;
                previousCdf = math.max(previousCdf, entry.DropWeight);
                if (!PassesToolMask(entry.ConditionMask, toolMask))
                    continue;

                uint multiplier = ResolveBiomeMultiplierMilli(biomeHash, entry.ItemHashID);
                cumulative += ((ulong)weight * multiplier + 999UL) / 1000UL;
                if (threshold < cumulative)
                    return entry.ItemHashID;
            }

            return 0u;
        }

        private bool HasBiomeModifier(uint biomeHash)
        {
            if (biomeHash == 0u || !BiomeModifiers.IsCreated)
                return false;

            for (int i = 0; i < BiomeModifiers.Length; i++)
            {
                ScavengingBiomeModifierDTO modifier = BiomeModifiers[i];
                if (modifier.BiomeHash == biomeHash && modifier.WeightMultiplierMilli != 0u)
                    return true;
            }

            return false;
        }

        private uint ResolveBiomeMultiplierMilli(uint biomeHash, uint itemHash)
        {
            if (biomeHash == 0u || itemHash == 0u || !BiomeModifiers.IsCreated)
                return 1000u;

            for (int i = 0; i < BiomeModifiers.Length; i++)
            {
                ScavengingBiomeModifierDTO modifier = BiomeModifiers[i];
                if (modifier.BiomeHash == biomeHash && modifier.ItemHashID == itemHash && modifier.WeightMultiplierMilli != 0u)
                    return modifier.WeightMultiplierMilli;
            }

            return 1000u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PassesToolMask(uint entryMask, uint toolMask)
        {
            return entryMask == ScavengingLootOracleConstants.ToolMaskAny || (entryMask & toolMask) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MapUIntToRange(uint value, uint range)
        {
            return range <= 1u ? 0u : (uint)(((ulong)value * range) >> 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BuildDeterministicSeed(in ScavengingHarvestRequestDTO request, ulong resourceHash)
        {
            ulong x = (ulong)request.NodeAup.GridX;
            ulong y = (ulong)request.NodeAup.GridY;
            ulong z = (ulong)request.NodeAup.GridZ;
            ulong lx = math.asuint(request.NodeAup.LocalX);
            ulong ly = math.asuint(request.NodeAup.LocalY);
            ulong lz = math.asuint(request.NodeAup.LocalZ);
            ulong mixed =
                x * 0x9E3779B185EBCA87UL ^
                y * 0xC2B2AE3D27D4EB4FUL ^
                z * 0x165667B19E3779F9UL ^
                lx * 0xD6E8FEB86659FD93UL ^
                ly * 0xA5A3564E27F2C19DUL ^
                lz * 0x9E3779B97F4A7C15UL ^
                request.SessionID ^
                ((ulong)request.TableVersion << 32) ^
                request.RollIndex ^
                resourceHash;

            mixed ^= mixed >> 33;
            mixed *= 0xFF51AFD7ED558CCDUL;
            mixed ^= mixed >> 33;
            mixed *= 0xC4CEB9FE1A85EC53UL;
            mixed ^= mixed >> 33;
            return (uint)(mixed ^ (mixed >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BuildResourceNodeHash(in AbsoluteUniversePosition aup, uint typeHash)
        {
            ulong mixed =
                ((ulong)aup.GridX * 0x9E3779B185EBCA87UL) ^
                ((ulong)aup.GridY * 0xC2B2AE3D27D4EB4FUL) ^
                ((ulong)aup.GridZ * 0x165667B19E3779F9UL) ^
                ((ulong)math.asuint(aup.LocalX) << 32) ^
                ((ulong)math.asuint(aup.LocalY) << 1) ^
                math.asuint(aup.LocalZ) ^
                typeHash;

            mixed ^= mixed >> 30;
            mixed *= 0xBF58476D1CE4E5B9UL;
            mixed ^= mixed >> 27;
            mixed *= 0x94D049BB133111EBUL;
            mixed ^= mixed >> 31;
            return mixed != 0UL ? mixed : 0xA125125UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PositiveMod(int value, int divisor)
        {
            int mod = value % divisor;
            return mod < 0 ? mod + divisor : mod;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct PublishLootYieldsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ScavengingResolvedYieldDTO> ResolvedYields;
        public int YieldCount;
        public NativeQueue<ItemAcquiredSignal>.ParallelWriter ItemWriter;
        public NativeQueue<VisualScavengeSignal>.ParallelWriter VisualWriter;
        public NativeQueue<ResourceDepletionDeltaSignal>.ParallelWriter DepletionWriter;
        public NativeQueue<HUDNotificationSignal>.ParallelWriter HudWriter;

        public void Execute()
        {
            if (!ResolvedYields.IsCreated)
                return;

            int count = math.min(YieldCount, ResolvedYields.Length);
            for (int i = 0; i < count; i++)
            {
                ScavengingResolvedYieldDTO yield = ResolvedYields[i];
                if (((uint)yield.Flags & ScavengingLootOracleConstants.ResultFlagInventoryFull) != 0u)
                {
                    HudWriter.Enqueue(new HUDNotificationSignal
                    {
                        MessageHash = ScavengingLootOracleConstants.InventoryFullMessageHash,
                        ContextHash = yield.OreHash,
                        SourceId = ScavengingLootOracleConstants.LootOracleSourceHash,
                        Frame = yield.Frame,
                        Severity = ScavengingLootOracleConstants.HudSeverityWarning,
                        Flags = 0
                    });
                    continue;
                }

                if (((uint)yield.Flags & ScavengingLootOracleConstants.ResultFlagResolved) == 0u || yield.ItemHashID == 0u || yield.Quantity == 0u)
                    continue;

                ushort signalQuantity = (ushort)math.min(yield.Quantity, (uint)ushort.MaxValue);
                ItemWriter.Enqueue(new ItemAcquiredSignal
                {
                    PositionAup = yield.NodeAup,
                    ItemHash = yield.ItemHashID,
                    OreHash = yield.OreHash,
                    Quantity = signalQuantity,
                    SourceKind = ScavengingLootOracleConstants.ItemSourceKind,
                    Flags = 0,
                    Frame = yield.Frame
                });

                VisualWriter.Enqueue(new VisualScavengeSignal
                {
                    PositionAup = yield.NodeAup,
                    ResourceNodeHash = yield.ResourceNodeHash,
                    ItemHashID = yield.ItemHashID,
                    OreHash = yield.OreHash,
                    Quantity = yield.Quantity,
                    Frame = yield.Frame,
                    VfxEmissionMultiplier = yield.VfxEmissionMultiplier,
                    SourceKind = ScavengingLootOracleConstants.VisualSourceKind,
                    Flags = 0,
                    _pad0 = 0
                });

                DepletionWriter.Enqueue(new ResourceDepletionDeltaSignal
                {
                    SectorHash = unchecked((long)(yield.ResourceNodeHash ^ (yield.ResourceNodeHash >> 32))),
                    DepletionMask = 1UL << (int)(yield.ResourceNodeHash & 63UL),
                    OreHash = yield.OreHash,
                    Frame = yield.Frame,
                    WordIndex = yield.DepletionWordIndex,
                    Operation = 1,
                    Flags = 0
                });
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ScavengingLootOracleSelfAuditJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<LootTableEntryDTO> LootEntries;
        [NoAlias] public NativeArray<uint> DistributionAudit;
        public uint RollCount;
        public ulong SessionID;

        public void Execute()
        {
            if (!LootEntries.IsCreated || !DistributionAudit.IsCreated)
                return;

            for (int i = 0; i < DistributionAudit.Length; i++)
                DistributionAudit[i] = 0u;

            uint entryCount = (uint)math.min(LootEntries.Length, DistributionAudit.Length);
            if (entryCount == 0u)
                return;

            uint total = LootEntries[(int)(entryCount - 1u)].DropWeight;
            if (total == 0u)
                return;

            uint rolls = math.max(1u, RollCount);
            for (uint i = 0u; i < rolls; i++)
            {
                AbsoluteUniversePosition aup = default;
                aup.GridX = 31L;
                aup.GridY = (long)i;
                aup.GridZ = -17L;
                aup.LocalX = (i & 255u) * 0.00390625f;
                aup.LocalY = ((i >> 8) & 255u) * 0.00390625f;
                aup.LocalZ = 0.5f;
                ulong resourceHash = LootResolutionJob.BuildResourceNodeHash(in aup, ScavengingLootOracleConstants.EmergencyTableHash);
                uint seed = (uint)(resourceHash ^ (resourceHash >> 32) ^ SessionID ^ i);
                Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(seed | 1u);
                uint threshold = (uint)(((ulong)random.NextUInt() * total) >> 32);
                uint selected = 0u;
                for (uint entry = 0u; entry < entryCount; entry++)
                {
                    if (threshold < LootEntries[(int)entry].DropWeight)
                    {
                        selected = entry;
                        break;
                    }
                }

                DistributionAudit[(int)selected] = DistributionAudit[(int)selected] + 1u;
            }
        }
    }

    public static class ScavengingLootOracleCsvParser
    {
        public static int ParseLootDistributionCsvBytes(NativeArray<byte> csvBytes, NativeArray<LootTableEntryDTO> destination)
        {
            if (!csvBytes.IsCreated || !destination.IsCreated)
                return 0;

            int cursor = 0;
            int count = 0;
            uint cumulative = 0u;
            while (cursor < csvBytes.Length && count < destination.Length)
            {
                SkipLineBreaks(csvBytes, ref cursor);
                if (cursor >= csvBytes.Length)
                    break;

                int itemStart = cursor;
                SkipUntilDelimiter(csvBytes, ref cursor);
                int itemEnd = cursor;
                if (cursor < csvBytes.Length && csvBytes[cursor] == ',')
                    cursor++;

                uint itemHash = ParseUnsignedToken(csvBytes, itemStart, itemEnd, out bool itemWasNumeric);
                if (!itemWasNumeric)
                    itemHash = HashTokenFnv1a(csvBytes, itemStart, itemEnd);

                int weightStart = cursor;
                SkipUntilDelimiter(csvBytes, ref cursor);
                int weightEnd = cursor;
                if (cursor < csvBytes.Length && csvBytes[cursor] == ',')
                    cursor++;

                uint weight = ParseUnsignedToken(csvBytes, weightStart, weightEnd, out bool weightWasNumeric);
                if (!weightWasNumeric || weight == 0u)
                {
                    SkipCurrentLine(csvBytes, ref cursor);
                    continue;
                }

                int maskStart = cursor;
                SkipUntilLineEnd(csvBytes, ref cursor);
                uint conditionMask = ParseUnsignedToken(csvBytes, maskStart, cursor, out bool maskWasNumeric);
                if (!maskWasNumeric)
                    conditionMask = ScavengingLootOracleConstants.ToolMaskAny;

                cumulative += weight;
                destination[count] = new LootTableEntryDTO
                {
                    ItemHashID = itemHash,
                    DropWeight = cumulative,
                    ConditionMask = conditionMask,
                    _pad0 = 0u
                };
                count++;
            }

            return count;
        }

        private static void SkipLineBreaks(NativeArray<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && (bytes[cursor] == '\r' || bytes[cursor] == '\n'))
                cursor++;
        }

        private static void SkipUntilDelimiter(NativeArray<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && bytes[cursor] != ',' && bytes[cursor] != '\r' && bytes[cursor] != '\n')
                cursor++;
        }

        private static void SkipUntilLineEnd(NativeArray<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && bytes[cursor] != '\r' && bytes[cursor] != '\n')
                cursor++;
        }

        private static void SkipCurrentLine(NativeArray<byte> bytes, ref int cursor)
        {
            SkipUntilLineEnd(bytes, ref cursor);
            SkipLineBreaks(bytes, ref cursor);
        }

        private static uint ParseUnsignedToken(NativeArray<byte> bytes, int start, int end, out bool parsed)
        {
            parsed = false;
            uint value = 0u;
            for (int i = start; i < end; i++)
            {
                byte b = bytes[i];
                if (b == ' ' || b == '\t' || b == '"')
                    continue;

                if (b < '0' || b > '9')
                    return 0u;

                parsed = true;
                value = value * 10u + (uint)(b - '0');
            }

            return value;
        }

        private static uint HashTokenFnv1a(NativeArray<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte b = bytes[i];
                if (b == ' ' || b == '\t' || b == '"')
                    continue;

                hash ^= b;
                hash *= 16777619u;
            }

            return hash;
        }
    }

    public sealed class ScavengingLootOracleRuntime : MonoBehaviour, ILateFrameTickable
    {
        private static ScavengingLootOracleRuntime _host;
        private static bool _signalLanesConfigured;
        private static bool _staticReset;

        private GlobalDataVault _vault;
        private VaultBufferHandle<LootTableEntryDTO> _lootEntriesHandle;
        private VaultBufferHandle<ScavengingHarvestRequestDTO> _requestsHandle;
        private VaultBufferHandle<ScavengingResolvedYieldDTO> _resolvedYieldsHandle;
        private VaultBufferHandle<ScavengingBiomeModifierDTO> _biomeModifiersHandle;
        private VaultBufferHandle<ScavengingTelemetryEntry> _telemetryRingHandle;
        private VaultBufferHandle<uint> _distributionAuditHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private int _queuedCount;
        private int _telemetryCursor;
        private uint _requestSequence;
        private uint _activeBiomeHash;
        private bool _vaultReady;
        private bool _emergencyTableGenerated;
        private bool _registeredLateFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _host = null;
            _signalLanesConfigured = false;
            _staticReset = true;
        }

        public static bool TryQueueResourceNodeLoot(
            in AbsoluteUniversePosition nodeAup,
            uint oreHash,
            uint forcedItemHash,
            uint quantity,
            uint toolMask,
            bool inventoryCapacityAvailable)
        {
            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.EnsureVault())
                return false;

            ScavengingLootOracleVaultViews views = host.ResolveViews();
            if (!views.HasAllBuffers() || host._queuedCount >= views.Requests.Length)
                return false;

            bool full = !inventoryCapacityAvailable;
            int slot = host._queuedCount++;
            float quality = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            uint frame = unchecked((uint)Time.frameCount);
            uint clampedQuantity = math.max(1u, quantity);
            uint requestId = ++host._requestSequence;
            views.Requests[slot] = new ScavengingHarvestRequestDTO
            {
                NodeAup = nodeAup,
                SessionID = ResolveSessionId(),
                ResourceNodeHash = 0UL,
                OreHash = oreHash != 0u ? oreHash : forcedItemHash,
                ToolHashID = toolMask != 0u ? toolMask : ScavengingLootOracleConstants.ToolMaskAny,
                BiomeHash = host._activeBiomeHash,
                TableHash = ScavengingLootOracleConstants.EmergencyTableHash,
                TableVersion = 1u,
                RollIndex = requestId,
                LootStartIndex = 0u,
                LootEntryCount = 4u,
                QuantityMin = clampedQuantity,
                QuantityMax = clampedQuantity,
                ForcedItemHashID = forcedItemHash,
                GlobalQualityWeight = quality,
                Capacity = new InventoryCapacityDTO
                {
                    FreeSlots = full ? (ushort)0 : (ushort)1,
                    FreeStackCapacity = full ? (ushort)0 : (ushort)1,
                    InventoryHash = 0u,
                    Flags = full ? ScavengingLootOracleConstants.RequestFlagInventoryFull : 0u,
                    _pad0 = frame
                }
            };

            return !full;
        }

        public static bool TryValidateLootTableEntryLayout(out string failure)
        {
            failure = null;
            if (UnsafeUtility.SizeOf<LootTableEntryDTO>() != ScavengingLootOracleConstants.LootTableEntrySizeBytes)
            {
                failure = "LootTableEntryDTO size mismatch";
                return false;
            }

            if (UnsafeUtility.GetFieldOffset(typeof(LootTableEntryDTO).GetField(nameof(LootTableEntryDTO.ItemHashID))) != 0 ||
                UnsafeUtility.GetFieldOffset(typeof(LootTableEntryDTO).GetField(nameof(LootTableEntryDTO.DropWeight))) != 4 ||
                UnsafeUtility.GetFieldOffset(typeof(LootTableEntryDTO).GetField(nameof(LootTableEntryDTO.ConditionMask))) != 8 ||
                UnsafeUtility.GetFieldOffset(typeof(LootTableEntryDTO).GetField(nameof(LootTableEntryDTO._pad0))) != 12)
            {
                failure = "LootTableEntryDTO field offset mismatch";
                return false;
            }

            return true;
        }

        public static bool GenerateEmergencyMockLootTables()
        {
            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.EnsureVault())
                return false;

            JobHandle handle = host.EnsureEmergencyLootTableJob(default);
            handle.Complete();
            return true;
        }

        public static bool TryIngestLootDistributionCsvBytes(NativeArray<byte> csvBytes, out int entryCount)
        {
            entryCount = 0;
            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.EnsureVault())
                return false;

            NativeArray<LootTableEntryDTO> entries = host._lootEntriesHandle.Resolve(host._vault);
            if (!entries.IsCreated)
                return false;

            entryCount = ScavengingLootOracleCsvParser.ParseLootDistributionCsvBytes(csvBytes, entries);
            host._emergencyTableGenerated = entryCount > 0;
            return entryCount > 0;
        }

        public static bool TryDumpTelemetryRing()
        {
            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.EnsureVault())
                return false;

            NativeArray<ScavengingTelemetryEntry> ring = host._telemetryRingHandle.Resolve(host._vault);
            if (!ring.IsCreated)
                return false;

            string path = Path.Combine(Application.dataPath, "..", ScavengingLootOracleConstants.TelemetryDumpRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(ring.Length);
                for (int i = 0; i < ring.Length; i++)
                {
                    ScavengingTelemetryEntry entry = ring[i];
                    writer.Write(entry.NodeAup.GridX);
                    writer.Write(entry.NodeAup.GridY);
                    writer.Write(entry.NodeAup.GridZ);
                    writer.Write(entry.NodeAup.LocalX);
                    writer.Write(entry.NodeAup.LocalY);
                    writer.Write(entry.NodeAup.LocalZ);
                    writer.Write(entry.ResourceNodeHash);
                    writer.Write(entry.SelectedItemHashID);
                    writer.Write(entry.OreHash);
                    writer.Write(entry.Frame);
                    writer.Write(entry.TotalWeight);
                    writer.Write(entry.Roll);
                    writer.Write(entry.Flags);
                    writer.Write(entry.EstimatedCpuMicroseconds);
                    writer.Write(entry.TableHash);
                    writer.Write(entry.RequestId);
                    writer.Write(entry.GlobalQualityWeight);
                    writer.Write(entry.DepletionWordIndex);
                    writer.Write(entry.DistributionBucket);
                    writer.Write(entry.DepletionMask);
                }
            }

            return true;
        }

        public static bool TryRunDistributionSelfAudit(out NativeArray<uint> auditCounts)
        {
            auditCounts = default;
            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.EnsureVault())
                return false;

            ScavengingLootOracleVaultViews views = host.ResolveViews();
            if (!views.HasAllBuffers())
                return false;

            JobHandle dependency = host.EnsureEmergencyLootTableJob(default);
            ScavengingLootOracleSelfAuditJob auditJob = new ScavengingLootOracleSelfAuditJob
            {
                LootEntries = views.LootEntries,
                DistributionAudit = views.DistributionAudit,
                RollCount = ScavengingLootOracleConstants.SelfAuditRollCount,
                SessionID = ResolveSessionId()
            };
            JobHandle handle = auditJob.Schedule(dependency);
            handle.Complete();
            auditCounts = views.DistributionAudit;
            return true;
        }

#if UNITY_EDITOR
        internal static void DrawHighestProbabilityGizmo(ResourceNode node, Vector3 position)
        {
            if (node == null)
                return;

            uint itemHash = H8Hashes.Items.TitaniumScrapHash;
            ScavengingLootOracleRuntime host = Application.isPlaying ? EnsureHost() : null;
            if (host != null && host.EnsureVault())
            {
                NativeArray<LootTableEntryDTO> entries = host._lootEntriesHandle.Resolve(host._vault);
                if (entries.IsCreated && entries.Length > 0)
                {
                    uint previous = 0u;
                    uint bestWeight = 0u;
                    for (int i = 0; i < math.min(entries.Length, 4); i++)
                    {
                        LootTableEntryDTO entry = entries[i];
                        uint weight = entry.DropWeight > previous ? entry.DropWeight - previous : 0u;
                        previous = math.max(previous, entry.DropWeight);
                        if (weight > bestWeight)
                        {
                            bestWeight = weight;
                            itemHash = entry.ItemHashID;
                        }
                    }
                }
            }

            UnityEditor.Handles.Label(position + Vector3.up * 0.65f, $"Loot 0x{itemHash:X8}");
        }
#endif

        private void OnEnable()
        {
            ConfigureSignalLanes();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
        }

        public void LateFrameTick()
        {
            DrainBiomeSignals();
            if (_queuedCount <= 0)
                return;

            if (!EnsureVault())
            {
                _queuedCount = 0;
                return;
            }

            int count = _queuedCount;
            _queuedCount = 0;
            ScavengingLootOracleVaultViews views = ResolveViews();
            if (!views.HasAllBuffers())
                return;

            JobHandle dependency = EnsureEmergencyLootTableJob(default);
            LootResolutionJob resolveJob = new LootResolutionJob
            {
                Requests = views.Requests,
                LootEntries = views.LootEntries,
                BiomeModifiers = views.BiomeModifiers,
                ResolvedYields = views.ResolvedYields,
                TelemetryRing = views.TelemetryRing,
                RequestCount = count,
                Frame = unchecked((uint)Time.frameCount),
                TelemetryCursor = _telemetryCursor
            };
            JobHandle resolveHandle = resolveJob.Schedule(dependency);

            PublishLootYieldsJob publishJob = new PublishLootYieldsJob
            {
                ResolvedYields = views.ResolvedYields,
                YieldCount = count,
                ItemWriter = SignalBus<ItemAcquiredSignal>.ParallelWriter,
                VisualWriter = SignalBus<VisualScavengeSignal>.ParallelWriter,
                DepletionWriter = SignalBus<ResourceDepletionDeltaSignal>.ParallelWriter,
                HudWriter = SignalBus<HUDNotificationSignal>.ParallelWriter
            };

            JobHandle publishHandle = publishJob.Schedule(resolveHandle);
            publishHandle.Complete();
            _telemetryCursor = (_telemetryCursor + count) % ScavengingLootOracleConstants.TelemetryRingCapacity;
        }

        private static ScavengingLootOracleRuntime EnsureHost()
        {
            ConfigureSignalLanes();
            if (_host != null)
                return _host;

            GameObject hostObject = new GameObject("ScavengingLootOracleRuntime"); // COLD ALLOC: GameObject[1] - dispatcher bridge for Burst loot signal completion - owner: SHINOBU_125
            hostObject.hideFlags = HideFlags.HideAndDontSave;
            _host = hostObject.AddComponent<ScavengingLootOracleRuntime>(); // COLD ALLOC: MonoBehaviour[1] - late-frame job owner - owner: SHINOBU_125
            DontDestroyOnLoad(hostObject);
            return _host;
        }

        private static void ConfigureSignalLanes()
        {
            if (_signalLanesConfigured && _staticReset)
                return;

            SignalBus<VisualScavengeSignal>.Configure(
                expectedCapacity: 128,
                maxFrameSignals: 512,
                lowTierFrameSignals: 64,
                laneHash: ScavengingLootOracleConstants.VisualScavengeLaneHash);
            SignalBus<VisualScavengeSignal>.EnsureInitialized();
            _signalLanesConfigured = true;
            _staticReset = true;
        }

        private bool EnsureVault()
        {
            if (_vaultReady && _vault != null)
                return true;

            if (!GlobalDataVault.TryGetLatestCreated(out _vault) || _vault == null)
                return false;

            _lootEntriesHandle = _vault.GetBufferHandle<LootTableEntryDTO>(
                ScavengingLootOracleConstants.LootEntriesBufferId,
                ScavengingLootOracleConstants.DefaultLootEntryCapacity,
                SystemID.GameplayLoot,
                NativeArrayOptions.UninitializedMemory);
            _requestsHandle = _vault.GetBufferHandle<ScavengingHarvestRequestDTO>(
                ScavengingLootOracleConstants.HarvestRequestsBufferId,
                ScavengingLootOracleConstants.DefaultRequestCapacity,
                SystemID.GameplayLoot,
                NativeArrayOptions.UninitializedMemory);
            _resolvedYieldsHandle = _vault.GetBufferHandle<ScavengingResolvedYieldDTO>(
                ScavengingLootOracleConstants.ResolvedYieldsBufferId,
                ScavengingLootOracleConstants.DefaultRequestCapacity,
                SystemID.GameplayLoot,
                NativeArrayOptions.UninitializedMemory);
            _biomeModifiersHandle = _vault.GetBufferHandle<ScavengingBiomeModifierDTO>(
                ScavengingLootOracleConstants.BiomeModifiersBufferId,
                ScavengingLootOracleConstants.DefaultBiomeModifierCapacity,
                SystemID.GameplayLoot,
                NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = _vault.GetBufferHandle<ScavengingTelemetryEntry>(
                ScavengingLootOracleConstants.TelemetryRingBufferId,
                ScavengingLootOracleConstants.TelemetryRingCapacity,
                SystemID.GameplayLoot,
                NativeArrayOptions.UninitializedMemory);
            _distributionAuditHandle = _vault.GetBufferHandle<uint>(
                ScavengingLootOracleConstants.DistributionAuditBufferId,
                ScavengingLootOracleConstants.DefaultAuditCapacity,
                SystemID.GameplayLoot,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = _vault.GetBufferHandle<byte>(
                ScavengingLootOracleConstants.CsvScratchBufferId,
                ScavengingLootOracleConstants.DefaultCsvScratchBytes,
                SystemID.GameplayLoot,
                NativeArrayOptions.UninitializedMemory);

            _vaultReady = ResolveViews().HasAllBuffers();
            return _vaultReady;
        }

        private ScavengingLootOracleVaultViews ResolveViews()
        {
            return new ScavengingLootOracleVaultViews
            {
                LootEntries = _lootEntriesHandle.Resolve(_vault),
                Requests = _requestsHandle.Resolve(_vault),
                ResolvedYields = _resolvedYieldsHandle.Resolve(_vault),
                BiomeModifiers = _biomeModifiersHandle.Resolve(_vault),
                TelemetryRing = _telemetryRingHandle.Resolve(_vault),
                DistributionAudit = _distributionAuditHandle.Resolve(_vault),
                CsvScratch = _csvScratchHandle.Resolve(_vault)
            };
        }

        private JobHandle EnsureEmergencyLootTableJob(JobHandle dependency)
        {
            if (_emergencyTableGenerated)
                return dependency;

            NativeArray<LootTableEntryDTO> entries = _lootEntriesHandle.Resolve(_vault);
            if (!entries.IsCreated)
                return dependency;

            GenerateEmergencyMockLootTablesJob job = new GenerateEmergencyMockLootTablesJob
            {
                LootEntries = entries
            };
            _emergencyTableGenerated = true;
            return job.Schedule(dependency);
        }

        private void DrainBiomeSignals()
        {
            ReadOnlySpan<BiomeChangedSignal> signals = SignalBus<BiomeChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                if (signals[i].CurrentBiomeHash != 0u)
                    _activeBiomeHash = signals[i].CurrentBiomeHash;
            }
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Core);
            _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.Core).Contains(this);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
            _registeredLateFrame = false;
        }

        private static ulong ResolveSessionId()
        {
            IWorldSeedProvider provider = GlobalRegistry.WorldSeedProvider;
            uint worldSeed = provider != null && provider.IsInitialized
                ? unchecked((uint)provider.RuntimeWorldSeed)
                : ScavengingLootOracleConstants.DefaultSessionSalt;
            return ((ulong)worldSeed << 32) | ScavengingLootOracleConstants.DefaultSessionSalt;
        }
    }
}

#if UNITY_EDITOR
namespace Hecton8.Scavenging.Editor
{
    using Unity.Collections;
    using UnityEditor;
    using UnityEngine.UIElements;

    public sealed class ProceduralLootTunerWindow : EditorWindow
    {
        private Label _layoutLabel;
        private Label _auditLabel;
        private Slider _biomeScalarSlider;
        private Slider _toolBonusSlider;
        private Slider _rareDropSlider;

        [MenuItem("Hecton8/Scavenging/Procedural Loot Tuner")]
        public static void Open()
        {
            GetWindow<ProceduralLootTunerWindow>("Procedural Loot Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            _layoutLabel = new Label();
            _auditLabel = new Label();
            _biomeScalarSlider = new Slider("Biome Modifier", 0.1f, 3.0f) { value = 1.0f };
            _toolBonusSlider = new Slider("Tool Yield Bonus", 0.1f, 3.0f) { value = 1.0f };
            _rareDropSlider = new Slider("Rare Drop Rate", 0.0f, 1.0f) { value = 0.06f };
            root.Add(_layoutLabel);
            root.Add(_auditLabel);
            root.Add(_biomeScalarSlider);
            root.Add(_toolBonusSlider);
            root.Add(_rareDropSlider);
            root.Add(new Button(RunAudit) { text = "Run 10k Audit" });
            root.Add(new Button(LoadCsv) { text = "Load loot_distribution_tables.csv" });
            RefreshLayoutLabel();
        }

        private void OnInspectorUpdate()
        {
            RefreshLayoutLabel();
        }

        private void RefreshLayoutLabel()
        {
            if (_layoutLabel == null)
                return;

            bool valid = ScavengingLootOracleRuntime.TryValidateLootTableEntryLayout(out string failure);
            _layoutLabel.text = valid
                ? "LootTableEntryDTO: size 16, offsets 0/4/8/12"
                : failure;
        }

        private void RunAudit()
        {
            if (_auditLabel == null)
                return;

            if (!ScavengingLootOracleRuntime.TryRunDistributionSelfAudit(out NativeArray<uint> counts) || !counts.IsCreated)
            {
                _auditLabel.text = "Audit unavailable: Vault not created.";
                return;
            }

            uint c0 = counts.Length > 0 ? counts[0] : 0u;
            uint c1 = counts.Length > 1 ? counts[1] : 0u;
            uint c2 = counts.Length > 2 ? counts[2] : 0u;
            uint c3 = counts.Length > 3 ? counts[3] : 0u;
            _auditLabel.text = $"10k: {c0}/{c1}/{c2}/{c3}";
        }

        private void LoadCsv()
        {
            if (_auditLabel == null)
                return;

            string path = EditorUtility.OpenFilePanel("loot_distribution_tables.csv", Application.dataPath, "csv");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            byte[] bytes = File.ReadAllBytes(path);
            using (NativeArray<byte> nativeBytes = new NativeArray<byte>(bytes.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                for (int i = 0; i < bytes.Length; i++)
                    nativeBytes[i] = bytes[i];

                _auditLabel.text = ScavengingLootOracleRuntime.TryIngestLootDistributionCsvBytes(nativeBytes, out int entryCount)
                    ? $"CSV entries loaded: {entryCount}"
                    : "CSV ingest failed: Vault unavailable or no entries.";
            }
        }
    }
}
#endif
