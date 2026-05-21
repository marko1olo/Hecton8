using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Data;
using Hecton8.Core.Generated;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

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
        public const byte ItemSourceKind = ItemAcquiredSignalSourceKinds.ScavengingLootOracle;
        public const byte VisualSourceKind = ItemAcquiredSignalSourceKinds.ScavengingLootOracle;
        public const byte HudSeverityWarning = 2;
        public const byte ItemSignalFlagQuantityClamped = 1 << 0;
        public const uint ItemSignalMaxQuantity = ushort.MaxValue;
        public const uint ToolMaskAny = 0u;
        public const uint ToolMaskKnife = 1u << 0;
        public const uint ToolMaskCutter = 1u << 1;
        public const uint ToolMaskDrill = 1u << 2;
        public const uint ToolMaskExtractor = 1u << 3;
        public const uint RequestFlagInventoryFull = 1u << 0;
        public const uint RequestFlagForcedItem = 1u << 1;
        public const uint RequestFlagSuppressDepletionDelta = 1u << 2;
        public const uint RequestFlagQuantityClamped = 1u << 3;
        public const uint ResultFlagResolved = 1u << 0;
        public const uint ResultFlagInventoryFull = 1u << 1;
        public const uint ResultFlagNoEligibleEntry = 1u << 2;
        public const uint ResultFlagForcedItem = 1u << 3;
        public const uint ResultFlagSuppressDepletionDelta = 1u << 4;
        public const uint ResultFlagQuantityClamped = 1u << 5;
        public const uint InventoryFullMessageHash = 0x4946554Cu; // IFUL
        public const uint VisualScavengeLaneHash = 0x56534356u; // VSCV
        public const uint LootOracleSourceHash = 0x4C4F5243u; // LORC
        public const uint EmergencyTableHash = 0x454D4C54u; // EMLT
        public const uint MonolithTableVersion = 0x4838444Du; // H8DM
        public const uint EditorPreviewBiomeHash = 0x45504249u; // EPBI
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

    internal static class ScavengingLootOracleMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeQualityWeight(float value)
        {
            return math.saturate(math.select(0f, value, math.isfinite(value)));
        }
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
                    GlobalQualityWeight = ScavengingLootOracleMath.SanitizeQualityWeight(GlobalQualityWeight),
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
        public int BiomeModifierCount;
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
            float quality = ScavengingLootOracleMath.SanitizeQualityWeight(request.GlobalQualityWeight);
            ulong resourceHash = request.ResourceNodeHash != 0UL
                ? request.ResourceNodeHash
                : BuildResourceNodeHash(in request.NodeAup, request.OreHash);
            ushort wordIndex = unchecked((ushort)((resourceHash >> 6) & 0xFFFFUL));
            ScavengingResolvedYieldDTO result = default;
            result.NodeAup = request.NodeAup;
            result.ResourceNodeHash = resourceHash;
            result.ItemHashID = 0u;
            result.OreHash = request.OreHash;
            result.Quantity = 0u;
            result.Frame = Frame;
            result.VfxEmissionMultiplier = math.lerp(0.1f, 1.0f, quality);
            result.Roll = 0u;
            result.TotalWeight = 0u;
            result.DepletionWordIndex = wordIndex;
            result.SourceKind = ScavengingLootOracleConstants.ItemSourceKind;
            result.Flags = 0;
            result.TableHash = request.TableHash;
            result.RequestId = unchecked((uint)requestIndex);

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
                result.Flags = BuildResultFlags(
                    in request,
                    ScavengingLootOracleConstants.ResultFlagResolved | ScavengingLootOracleConstants.ResultFlagForcedItem);
                return result;
            }

            if (request.TableHash == 0u && request.LootEntryCount == 0u)
            {
                result.Flags = (byte)ScavengingLootOracleConstants.ResultFlagNoEligibleEntry;
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
                uint totalWeight = ResolveBaseTotalWeight(start, entryCount, request.ToolHashID, out bool canUseRawCdf);
                if (totalWeight == 0u)
                {
                    result.Flags = (byte)ScavengingLootOracleConstants.ResultFlagNoEligibleEntry;
                    return result;
                }

                uint threshold = MapUIntToRange(rollBits, totalWeight);
                uint selectedIndex = canUseRawCdf
                    ? BinarySearchRawCdf(start, entryCount, threshold)
                    : SelectBaseItemLinear(start, entryCount, request.ToolHashID, threshold);
                LootTableEntryDTO selected = LootEntries[(int)selectedIndex];
                result.ItemHashID = selected.ItemHashID;
                result.Quantity = quantity;
                result.Roll = threshold;
                result.TotalWeight = totalWeight;
                result.Flags = BuildResultFlags(in request, ScavengingLootOracleConstants.ResultFlagResolved);
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
                ? BuildResultFlags(in request, ScavengingLootOracleConstants.ResultFlagResolved)
                : (byte)ScavengingLootOracleConstants.ResultFlagNoEligibleEntry;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte BuildResultFlags(in ScavengingHarvestRequestDTO request, uint baseFlags)
        {
            uint flags = baseFlags;
            if ((request.Capacity.Flags & ScavengingLootOracleConstants.RequestFlagSuppressDepletionDelta) != 0u)
                flags |= ScavengingLootOracleConstants.ResultFlagSuppressDepletionDelta;
            if ((request.Capacity.Flags & ScavengingLootOracleConstants.RequestFlagQuantityClamped) != 0u)
                flags |= ScavengingLootOracleConstants.ResultFlagQuantityClamped;

            return (byte)flags;
        }

        private void WriteTelemetry(in ScavengingHarvestRequestDTO request, in ScavengingResolvedYieldDTO result, int index)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length == 0)
                return;

            int slot = PositiveMod(TelemetryCursor + index, TelemetryRing.Length);
            ScavengingTelemetryEntry telemetry = default;
            telemetry.NodeAup = result.NodeAup;
            telemetry.ResourceNodeHash = result.ResourceNodeHash;
            telemetry.SelectedItemHashID = result.ItemHashID;
            telemetry.OreHash = result.OreHash;
            telemetry.Frame = Frame;
            telemetry.TotalWeight = result.TotalWeight;
            telemetry.Roll = result.Roll;
            telemetry.Flags = result.Flags;
            telemetry.EstimatedCpuMicroseconds = 5u;
            telemetry.TableHash = request.TableHash;
            telemetry.RequestId = result.RequestId;
            telemetry.GlobalQualityWeight = ScavengingLootOracleMath.SanitizeQualityWeight(request.GlobalQualityWeight);
            telemetry.DepletionWordIndex = result.DepletionWordIndex;
            telemetry.DistributionBucket = result.TotalWeight > 0u ? result.Roll : 0u;
            telemetry.DepletionMask = 1UL << (int)(result.ResourceNodeHash & 63UL);
            telemetry._pad0 = 0UL;
            telemetry._pad1 = 0UL;
            TelemetryRing[slot] = telemetry;
        }

        private uint ResolveBaseTotalWeight(uint start, uint entryCount, uint toolMask, out bool canUseRawCdf)
        {
            uint previousCdf = 0u;
            uint total = 0u;
            bool allEntriesEligible = true;
            bool cdfMonotonic = true;
            for (uint i = 0u; i < entryCount; i++)
            {
                LootTableEntryDTO entry = LootEntries[(int)(start + i)];
                bool eligible = PassesToolMask(entry.ConditionMask, toolMask);
                cdfMonotonic &= entry.DropWeight >= previousCdf;
                uint weight = entry.DropWeight > previousCdf ? entry.DropWeight - previousCdf : 0u;
                previousCdf = math.max(previousCdf, entry.DropWeight);
                allEntriesEligible &= eligible;
                total += eligible ? weight : 0u;
            }

            canUseRawCdf = allEntriesEligible && cdfMonotonic;
            return total;
        }

        private uint BinarySearchRawCdf(uint start, uint entryCount, uint threshold)
        {
            uint low = 0u;
            uint high = entryCount;
            while (low < high)
            {
                uint mid = low + ((high - low) >> 1);
                uint prefix = LootEntries[(int)(start + mid)].DropWeight;
                if (threshold < prefix)
                    high = mid;
                else
                    low = mid + 1u;
            }

            return start + math.min(low, entryCount - 1u);
        }

        private uint SelectBaseItemLinear(uint start, uint entryCount, uint toolMask, uint threshold)
        {
            uint previousCdf = 0u;
            uint cumulative = 0u;
            uint fallback = start;
            for (uint i = 0u; i < entryCount; i++)
            {
                LootTableEntryDTO entry = LootEntries[(int)(start + i)];
                uint weight = entry.DropWeight > previousCdf ? entry.DropWeight - previousCdf : 0u;
                previousCdf = math.max(previousCdf, entry.DropWeight);
                if (!PassesToolMask(entry.ConditionMask, toolMask))
                    continue;

                fallback = start + i;
                cumulative += weight;
                if (threshold < cumulative)
                    return fallback;
            }

            return fallback;
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
            int count = math.min(math.max(0, BiomeModifierCount), BiomeModifiers.IsCreated ? BiomeModifiers.Length : 0);
            if (biomeHash == 0u || count <= 0)
                return false;

            for (int i = 0; i < count; i++)
            {
                ScavengingBiomeModifierDTO modifier = BiomeModifiers[i];
                if (modifier.BiomeHash == biomeHash && modifier.WeightMultiplierMilli != 0u)
                    return true;
            }

            return false;
        }

        private uint ResolveBiomeMultiplierMilli(uint biomeHash, uint itemHash)
        {
            int count = math.min(math.max(0, BiomeModifierCount), BiomeModifiers.IsCreated ? BiomeModifiers.Length : 0);
            if (biomeHash == 0u || itemHash == 0u || count <= 0)
                return 1000u;

            for (int i = 0; i < count; i++)
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
            ulong lx = QuantizeLocalMillimetersForHash(request.NodeAup.LocalX);
            ulong ly = QuantizeLocalMillimetersForHash(request.NodeAup.LocalY);
            ulong lz = QuantizeLocalMillimetersForHash(request.NodeAup.LocalZ);
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
                (QuantizeLocalMillimetersForHash(aup.LocalX) << 32) ^
                (QuantizeLocalMillimetersForHash(aup.LocalY) << 1) ^
                QuantizeLocalMillimetersForHash(aup.LocalZ) ^
                typeHash;

            mixed ^= mixed >> 30;
            mixed *= 0xBF58476D1CE4E5B9UL;
            mixed ^= mixed >> 27;
            mixed *= 0x94D049BB133111EBUL;
            mixed ^= mixed >> 31;
            return mixed != 0UL ? mixed : 0xA125125UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong QuantizeLocalMillimetersForHash(float value)
        {
            float finite = math.select(0f, value, math.isfinite(value));
            float clamped = math.clamp(finite, 0f, (float)AbsoluteUniversePosition.CellSizeMeters);
            return (ulong)(long)math.round(clamped * 1000f);
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
                    HUDNotificationSignal hudSignal = default;
                    hudSignal.MessageHash = ScavengingLootOracleConstants.InventoryFullMessageHash;
                    hudSignal.ContextHash = yield.OreHash;
                    hudSignal.SourceId = ScavengingLootOracleConstants.LootOracleSourceHash;
                    hudSignal.Frame = yield.Frame;
                    hudSignal.Severity = ScavengingLootOracleConstants.HudSeverityWarning;
                    hudSignal.Flags = 0;
                    HudWriter.Enqueue(hudSignal);
                    continue;
                }

                if (((uint)yield.Flags & ScavengingLootOracleConstants.ResultFlagResolved) == 0u || yield.ItemHashID == 0u || yield.Quantity == 0u)
                    continue;

                ushort signalQuantity = (ushort)math.min(yield.Quantity, ScavengingLootOracleConstants.ItemSignalMaxQuantity);
                ItemAcquiredSignal itemSignal = default;
                itemSignal.PositionAup = yield.NodeAup;
                itemSignal.ItemHash = yield.ItemHashID;
                itemSignal.OreHash = yield.OreHash;
                itemSignal.Quantity = signalQuantity;
                itemSignal.SourceKind = ScavengingLootOracleConstants.ItemSourceKind;
                itemSignal.Flags = (byte)(((uint)yield.Flags & ScavengingLootOracleConstants.ResultFlagQuantityClamped) != 0u
                    ? ScavengingLootOracleConstants.ItemSignalFlagQuantityClamped
                    : 0);
                itemSignal.Frame = yield.Frame;
                ItemWriter.Enqueue(itemSignal);

                VisualScavengeSignal visualSignal = default;
                visualSignal.PositionAup = ToVisualSignalAup(in yield.NodeAup);
                visualSignal.ResourceNodeHash = yield.ResourceNodeHash;
                visualSignal.ItemHashID = yield.ItemHashID;
                visualSignal.OreHash = yield.OreHash;
                visualSignal.Quantity = yield.Quantity;
                visualSignal.Frame = yield.Frame;
                visualSignal.VfxEmissionMultiplier = yield.VfxEmissionMultiplier;
                visualSignal.SourceKind = ScavengingLootOracleConstants.VisualSourceKind;
                visualSignal.Flags = yield.Flags;
                visualSignal._pad0 = 0;
                VisualWriter.Enqueue(visualSignal);

                if (((uint)yield.Flags & ScavengingLootOracleConstants.ResultFlagSuppressDepletionDelta) != 0u)
                    continue;

                ResourceDepletionDeltaSignal depletionSignal = default;
                depletionSignal.SectorHash = unchecked((long)(yield.ResourceNodeHash ^ (yield.ResourceNodeHash >> 32)));
                depletionSignal.DepletionMask = 1UL << (int)(yield.ResourceNodeHash & 63UL);
                depletionSignal.OreHash = yield.OreHash;
                depletionSignal.Frame = yield.Frame;
                depletionSignal.WordIndex = yield.DepletionWordIndex;
                depletionSignal.Operation = 1;
                depletionSignal.Flags = 0;
                DepletionWriter.Enqueue(depletionSignal);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VisualScavengeAup48 ToVisualSignalAup(in AbsoluteUniversePosition aup)
        {
            VisualScavengeAup48 visualAup = default;
            visualAup.GridX = aup.GridX;
            visualAup.GridY = aup.GridY;
            visualAup.GridZ = aup.GridZ;
            visualAup.LocalX = aup.LocalX;
            visualAup.LocalY = aup.LocalY;
            visualAup.LocalZ = aup.LocalZ;
            visualAup._pad0 = 0f;
            visualAup._pad1 = 0UL;
            return visualAup;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ScavengingLootOracleSelfAuditJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<LootTableEntryDTO> LootEntries;
        [ReadOnly, NoAlias] public NativeArray<ScavengingBiomeModifierDTO> BiomeModifiers;
        [NoAlias] public NativeArray<uint> DistributionAudit;
        public uint RollCount;
        public uint EntryCount;
        public int BiomeModifierCount;
        public uint ToolHashID;
        public uint BiomeHash;
        public ulong SessionID;

        public void Execute()
        {
            if (!LootEntries.IsCreated || !DistributionAudit.IsCreated)
                return;

            for (int i = 0; i < DistributionAudit.Length; i++)
                DistributionAudit[i] = 0u;

            uint requestedCount = EntryCount != 0u ? EntryCount : 4u;
            uint entryCount = math.min(requestedCount, (uint)math.min(LootEntries.Length, DistributionAudit.Length));
            if (entryCount == 0u)
                return;

            uint total = ResolveAuditTotalWeight(entryCount);
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
                uint selected = SelectAuditEntry(entryCount, threshold);

                DistributionAudit[(int)selected] = DistributionAudit[(int)selected] + 1u;
            }
        }

        private uint ResolveAuditTotalWeight(uint entryCount)
        {
            uint previousCdf = 0u;
            ulong total = 0UL;
            for (uint i = 0u; i < entryCount; i++)
            {
                LootTableEntryDTO entry = LootEntries[(int)i];
                uint weight = entry.DropWeight > previousCdf ? entry.DropWeight - previousCdf : 0u;
                previousCdf = math.max(previousCdf, entry.DropWeight);
                if (!PassesToolMask(entry.ConditionMask))
                    continue;

                uint multiplier = ResolveBiomeMultiplierMilli(BiomeHash, entry.ItemHashID);
                total += ((ulong)weight * multiplier + 999UL) / 1000UL;
            }

            return (uint)math.min(total, (ulong)uint.MaxValue);
        }

        private uint SelectAuditEntry(uint entryCount, uint threshold)
        {
            uint previousCdf = 0u;
            ulong cumulative = 0UL;
            for (uint i = 0u; i < entryCount; i++)
            {
                LootTableEntryDTO entry = LootEntries[(int)i];
                uint weight = entry.DropWeight > previousCdf ? entry.DropWeight - previousCdf : 0u;
                previousCdf = math.max(previousCdf, entry.DropWeight);
                if (!PassesToolMask(entry.ConditionMask))
                    continue;

                uint multiplier = ResolveBiomeMultiplierMilli(BiomeHash, entry.ItemHashID);
                cumulative += ((ulong)weight * multiplier + 999UL) / 1000UL;
                if (threshold < cumulative)
                    return i;
            }

            return 0u;
        }

        private bool PassesToolMask(uint entryMask)
        {
            return entryMask == ScavengingLootOracleConstants.ToolMaskAny || (entryMask & ToolHashID) != 0u;
        }

        private uint ResolveBiomeMultiplierMilli(uint biomeHash, uint itemHash)
        {
            int count = math.min(math.max(0, BiomeModifierCount), BiomeModifiers.IsCreated ? BiomeModifiers.Length : 0);
            if (biomeHash == 0u || itemHash == 0u || count <= 0)
                return 1000u;

            for (int i = 0; i < count; i++)
            {
                ScavengingBiomeModifierDTO modifier = BiomeModifiers[i];
                if (modifier.BiomeHash == biomeHash && modifier.ItemHashID == itemHash && modifier.WeightMultiplierMilli != 0u)
                    return modifier.WeightMultiplierMilli;
            }

            return 1000u;
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

                cumulative = AddSaturating(cumulative, weight);
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
                uint digit = (uint)(b - '0');
                if (value > (uint.MaxValue - digit) / 10u)
                {
                    parsed = false;
                    return 0u;
                }

                value = value * 10u + digit;
            }

            return value;
        }

        private static uint AddSaturating(uint current, uint delta)
        {
            ulong next = (ulong)current + delta;
            return next > uint.MaxValue ? uint.MaxValue : (uint)next;
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

    public sealed class ScavengingLootOracleRuntime : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static ScavengingLootOracleRuntime _host;
        private static bool _signalLanesConfigured;
        private static bool _staticReset;

        private const SystemID OwnerSystem = SystemID.GameplayLoot;

        private IDataVault _vault;
        private VaultGenerationHandle<LootTableEntryDTO> _lootEntriesHandle;
        private VaultGenerationHandle<ScavengingHarvestRequestDTO> _requestsHandle;
        private VaultGenerationHandle<ScavengingResolvedYieldDTO> _resolvedYieldsHandle;
        private VaultGenerationHandle<ScavengingBiomeModifierDTO> _biomeModifiersHandle;
        private VaultGenerationHandle<ScavengingTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<uint> _distributionAuditHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private int _queuedCount;
        private int _telemetryCursor;
        private JobHandle _pendingPublishHandle;
        private int _pendingPublishCount;
        private bool _publishPending;
        private uint _simulationFrameCounter;
        private int _activeLootEntryCount;
        private int _activeBiomeModifierCount;
        private uint _activeBiomeHash;
        private bool _vaultReady;
        private bool _emergencyTableGenerated;
        private bool _lootTableHydrated;
        private uint _activeLootTableHash;
        private uint _activeLootTableVersion;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _host = null;
            _signalLanesConfigured = false;
            _staticReset = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapHostAfterSceneLoad()
        {
            if (Application.isPlaying)
                EnsureHost();
        }

        public static bool TryQueueResourceNodeLoot(
            in AbsoluteUniversePosition nodeAup,
            uint oreHash,
            uint forcedItemHash,
            uint quantity,
            uint toolMask,
            bool inventoryCapacityAvailable,
            bool emitDepletionDelta = true)
        {
            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.EnsureVault())
                return false;

            ScavengingLootOracleVaultViews views = host.ResolveViews();
            if (!views.HasAllBuffers() || host._queuedCount >= views.Requests.Length)
                return false;

            host.TryPrimeMonolithLootTable();
            bool full = !inventoryCapacityAvailable;
            int slot = host._queuedCount++;
            float quality = ScavengingLootOracleMath.SanitizeQualityWeight(HomeostasisBrain.GlobalQualityWeight);
            uint frame = host.PeekNextSimulationFrame();
            uint clampedQuantity = ClampItemSignalQuantity(quantity);
            uint quantityClampFlag = clampedQuantity != quantity
                ? ScavengingLootOracleConstants.RequestFlagQuantityClamped
                : 0u;
            uint lootEntryCount = host._activeLootEntryCount > 0
                ? (uint)math.min(host._activeLootEntryCount, views.LootEntries.Length)
                : 0u;
            InventoryCapacityDTO capacity = default;
            capacity.FreeSlots = full ? (ushort)0 : (ushort)1;
            capacity.FreeStackCapacity = full ? (ushort)0 : (ushort)1;
            capacity.InventoryHash = 0u;
            capacity.Flags = (full ? ScavengingLootOracleConstants.RequestFlagInventoryFull : 0u) |
                             (forcedItemHash != 0u ? ScavengingLootOracleConstants.RequestFlagForcedItem : 0u) |
                             (!emitDepletionDelta ? ScavengingLootOracleConstants.RequestFlagSuppressDepletionDelta : 0u) |
                             quantityClampFlag;
            capacity._pad0 = frame;

            ScavengingHarvestRequestDTO request = default;
            request.NodeAup = nodeAup;
            request.SessionID = ResolveSessionId();
            request.ResourceNodeHash = 0UL;
            request.OreHash = oreHash != 0u ? oreHash : forcedItemHash;
            request.ToolHashID = toolMask != 0u ? toolMask : ScavengingLootOracleConstants.ToolMaskAny;
            request.BiomeHash = host._activeBiomeHash;
            request.TableHash = host._activeLootTableHash;
            request.TableVersion = host._activeLootTableVersion;
            request.RollIndex = 0u;
            request.LootStartIndex = 0u;
            request.LootEntryCount = lootEntryCount;
            request.QuantityMin = clampedQuantity;
            request.QuantityMax = clampedQuantity;
            request.ForcedItemHashID = forcedItemHash;
            request.GlobalQualityWeight = quality;
            request.Capacity = capacity;
            views.Requests[slot] = request;

            return !full;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ClampItemSignalQuantity(uint quantity)
        {
            return math.min(math.max(1u, quantity), ScavengingLootOracleConstants.ItemSignalMaxQuantity);
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
            // COLD SYNC JOB: manual/editor fallback generation must finish before the caller inspects the table.
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            return true;
        }

        public static bool TryIngestLootDistributionCsvBytes(NativeArray<byte> csvBytes, out int entryCount)
        {
            entryCount = 0;
            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.EnsureVault())
                return false;

            if (!TryResolveScavengingVaultBuffer(host._vault, ref host._lootEntriesHandle, ScavengingLootOracleConstants.LootEntriesBufferId, ScavengingLootOracleConstants.DefaultLootEntryCapacity, out NativeArray<LootTableEntryDTO> entries))
                return false;

            entryCount = ScavengingLootOracleCsvParser.ParseLootDistributionCsvBytes(csvBytes, entries);
            host._activeLootEntryCount = entryCount;
            host._emergencyTableGenerated = entryCount > 0;
            return entryCount > 0;
        }

#if UNITY_EDITOR
        public static bool TryApplyEditorTuning(float biomeScalar, float toolYieldBonus, float rareDropRate, out int entryCount, out int modifierCount)
        {
            entryCount = 0;
            modifierCount = 0;
            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.EnsureVault())
                return false;

            ScavengingLootOracleVaultViews views = host.ResolveViews();
            if (!views.HasAllBuffers() || views.LootEntries.Length < 4 || views.BiomeModifiers.Length < 2)
                return false;

            float safeBiomeScalar = math.clamp(math.select(1f, biomeScalar, math.isfinite(biomeScalar)), 0.1f, 3.0f);
            float safeToolBonus = math.clamp(math.select(1f, toolYieldBonus, math.isfinite(toolYieldBonus)), 0.1f, 3.0f);
            float safeRareRate = math.saturate(math.select(0.06f, rareDropRate, math.isfinite(rareDropRate)));
            float rareCurve = safeRareRate * safeRareRate * (3f - (2f * safeRareRate));
            float rareMultiplier = math.lerp(0.05f, 8.0f, rareCurve);

            uint titaniumWeight = ToEditorWeight(55f);
            uint copperWeight = ToEditorWeight(27f);
            uint sulfurWeight = ToEditorWeight(12f * safeToolBonus);
            uint abyssalWeight = ToEditorWeight(6f * safeToolBonus * rareMultiplier);
            uint cdf = titaniumWeight;
            views.LootEntries[0] = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.TitaniumScrapHash,
                DropWeight = cdf,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskAny,
                _pad0 = 0u
            };
            cdf = AddClampedCdf(cdf, copperWeight);
            views.LootEntries[1] = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.CopperOreHash,
                DropWeight = cdf,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskAny,
                _pad0 = 0u
            };
            cdf = AddClampedCdf(cdf, sulfurWeight);
            views.LootEntries[2] = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.SulfurClumpsHash,
                DropWeight = cdf,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskCutter | ScavengingLootOracleConstants.ToolMaskDrill | ScavengingLootOracleConstants.ToolMaskExtractor,
                _pad0 = 0u
            };
            cdf = AddClampedCdf(cdf, abyssalWeight);
            views.LootEntries[3] = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.AbyssalCrystalHash,
                DropWeight = cdf,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskDrill | ScavengingLootOracleConstants.ToolMaskExtractor,
                _pad0 = 0u
            };

            uint targetBiomeHash = host._activeBiomeHash != 0u
                ? host._activeBiomeHash
                : ScavengingLootOracleConstants.EditorPreviewBiomeHash;
            if (host._activeBiomeHash == 0u)
                host._activeBiomeHash = targetBiomeHash;

            uint biomeMultiplierMilli = ToEditorMilli(safeBiomeScalar);
            views.BiomeModifiers[0] = new ScavengingBiomeModifierDTO
            {
                BiomeHash = targetBiomeHash,
                ItemHashID = H8Hashes.Items.SulfurClumpsHash,
                WeightMultiplierMilli = biomeMultiplierMilli,
                _pad0 = 0u
            };
            views.BiomeModifiers[1] = new ScavengingBiomeModifierDTO
            {
                BiomeHash = targetBiomeHash,
                ItemHashID = H8Hashes.Items.AbyssalCrystalHash,
                WeightMultiplierMilli = biomeMultiplierMilli,
                _pad0 = 0u
            };

            host._activeLootEntryCount = 4;
            host._activeBiomeModifierCount = 2;
            host._emergencyTableGenerated = true;
            entryCount = host._activeLootEntryCount;
            modifierCount = host._activeBiomeModifierCount;
            return true;
        }

        private static uint ToEditorMilli(float value)
        {
            return (uint)math.clamp((int)math.round(value * 1000f), 1, 10000);
        }

        private static uint ToEditorWeight(float value)
        {
            return (uint)math.clamp((int)math.round(value * 100f), 1, 1000000);
        }

        private static uint AddClampedCdf(uint current, uint delta)
        {
            ulong next = (ulong)current + delta;
            return next > uint.MaxValue ? uint.MaxValue : (uint)next;
        }
#endif

        public static bool TryDumpTelemetryRing()
        {
            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.EnsureVault())
                return false;

            if (!TryReadScavengingVaultBuffer(host._vault, in host._telemetryRingHandle, ScavengingLootOracleConstants.TelemetryRingBufferId, ScavengingLootOracleConstants.TelemetryRingCapacity, out NativeArray<ScavengingTelemetryEntry> ring))
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

            JobHandle dependency = host.EnsureLootTableJob(default);
            ScavengingLootOracleSelfAuditJob auditJob = new ScavengingLootOracleSelfAuditJob
            {
                LootEntries = views.LootEntries,
                BiomeModifiers = views.BiomeModifiers,
                DistributionAudit = views.DistributionAudit,
                RollCount = ScavengingLootOracleConstants.SelfAuditRollCount,
                EntryCount = host._activeLootEntryCount > 0 ? (uint)host._activeLootEntryCount : 0u,
                BiomeModifierCount = host._activeBiomeModifierCount,
                ToolHashID = ScavengingLootOracleConstants.ToolMaskKnife |
                             ScavengingLootOracleConstants.ToolMaskCutter |
                             ScavengingLootOracleConstants.ToolMaskDrill |
                             ScavengingLootOracleConstants.ToolMaskExtractor,
                BiomeHash = host._activeBiomeHash,
                SessionID = ResolveSessionId()
            };
            JobHandle handle = auditJob.Schedule(dependency);
            // COLD SYNC JOB: editor self-audit returns the Vault audit buffer to the inspector button.
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
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
                JobHandle dependency = host.EnsureLootTableJob(default);
                // COLD SYNC JOB: editor gizmo needs the preview table before reading Vault rows.
                DispatcherJobFence.TryComplete(ref dependency, forceComplete: true);
                if (TryReadScavengingVaultBuffer(host._vault, in host._lootEntriesHandle, ScavengingLootOracleConstants.LootEntriesBufferId, ScavengingLootOracleConstants.DefaultLootEntryCapacity, out NativeArray<LootTableEntryDTO> entries))
                {
                    int count = host._activeLootEntryCount > 0 ? math.min(host._activeLootEntryCount, entries.Length) : 0;
                    if (count > 0)
                    {
                        uint previous = 0u;
                        uint bestWeight = 0u;
                        for (int i = 0; i < math.min(count, 4); i++)
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
            }

            UnityEditor.Handles.Label(position + Vector3.up * 0.65f, $"Loot 0x{itemHash:X8}");
        }
#endif

        private void OnEnable()
        {
            ConfigureSignalLanes();
            CacheVaultCold();
            TryRegisterHotSwapListener();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            TryCompletePendingPublish(forceComplete: true);
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
            ReleaseVaultBinding();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    TryCompletePendingPublish(forceComplete: true);
                    IDataVault previousVault = previousService as IDataVault;
                    if (previousVault == null)
                        previousVault = _vault;

                    ReleaseScavengingVaultHandles(previousVault);
                    _vault = currentService as IDataVault;
                    InvalidateVaultHandles();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterLateFrame();
                    TryRegisterLateFrame();
                    break;
            }
        }

        public void LateFrameTick()
        {
            DrainBiomeSignals();
            if (!TryCompletePendingPublish(forceComplete: false))
                return;

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

            JobHandle dependency = EnsureLootTableJob(default);
            LootResolutionJob resolveJob = default;
            resolveJob.Requests = views.Requests;
            resolveJob.LootEntries = views.LootEntries;
            resolveJob.BiomeModifiers = views.BiomeModifiers;
            resolveJob.ResolvedYields = views.ResolvedYields;
            resolveJob.TelemetryRing = views.TelemetryRing;
            resolveJob.RequestCount = count;
            resolveJob.BiomeModifierCount = _activeBiomeModifierCount;
            resolveJob.Frame = AdvanceSimulationFrame();
            resolveJob.TelemetryCursor = _telemetryCursor;
            JobHandle resolveHandle = resolveJob.Schedule(dependency);

            PublishLootYieldsJob publishJob = default;
            publishJob.ResolvedYields = views.ResolvedYields;
            publishJob.YieldCount = count;
            publishJob.ItemWriter = SignalBus<ItemAcquiredSignal>.ParallelWriter;
            publishJob.VisualWriter = SignalBus<VisualScavengeSignal>.ParallelWriter;
            publishJob.DepletionWriter = SignalBus<ResourceDepletionDeltaSignal>.ParallelWriter;
            publishJob.HudWriter = SignalBus<HUDNotificationSignal>.ParallelWriter;

            JobHandle publishHandle = publishJob.Schedule(resolveHandle);
            _pendingPublishHandle = publishHandle;
            _pendingPublishCount = count;
            _publishPending = true;
            H8Memory.RegisterActiveJob(SystemID.GameplayLoot, _pendingPublishHandle);
        }

        private bool TryCompletePendingPublish(bool forceComplete)
        {
            if (!_publishPending)
                return true;

            if (!DispatcherJobFence.TryComplete(ref _pendingPublishHandle, forceComplete))
                return false;

            _publishPending = false;
            _telemetryCursor = (_telemetryCursor + _pendingPublishCount) % ScavengingLootOracleConstants.TelemetryRingCapacity;
            _pendingPublishCount = 0;
            H8Memory.RegisterActiveJob(SystemID.GameplayLoot, default);
            return true;
        }

        private static ScavengingLootOracleRuntime EnsureHost()
        {
            ConfigureSignalLanes();
            if (_host != null)
            {
                _host.TryRegisterHotSwapListener();
                _host.TryRegisterLateFrame();
                return _host;
            }

            GameObject hostObject = new GameObject("ScavengingLootOracleRuntime"); // COLD ALLOC: GameObject[1] - scene-local dispatcher bridge for Burst loot signal completion - owner: SHINOBU_125
            hostObject.hideFlags = HideFlags.HideAndDontSave;
            _host = hostObject.AddComponent<ScavengingLootOracleRuntime>(); // COLD ALLOC: MonoBehaviour[1] - late-frame job owner - owner: SHINOBU_125
            return _host;
        }

        private uint PeekNextSimulationFrame()
        {
            uint next = _simulationFrameCounter + 1u;
            return next != 0u ? next : 1u;
        }

        private uint AdvanceSimulationFrame()
        {
            _simulationFrameCounter++;
            if (_simulationFrameCounter == 0u)
                _simulationFrameCounter = 1u;

            return _simulationFrameCounter;
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

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            _vaultReady =
                EnsureScavengingVaultBuffer(vault, ref _lootEntriesHandle, ScavengingLootOracleConstants.LootEntriesBufferId, ScavengingLootOracleConstants.DefaultLootEntryCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureScavengingVaultBuffer(vault, ref _requestsHandle, ScavengingLootOracleConstants.HarvestRequestsBufferId, ScavengingLootOracleConstants.DefaultRequestCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureScavengingVaultBuffer(vault, ref _resolvedYieldsHandle, ScavengingLootOracleConstants.ResolvedYieldsBufferId, ScavengingLootOracleConstants.DefaultRequestCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureScavengingVaultBuffer(vault, ref _biomeModifiersHandle, ScavengingLootOracleConstants.BiomeModifiersBufferId, ScavengingLootOracleConstants.DefaultBiomeModifierCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureScavengingVaultBuffer(vault, ref _telemetryRingHandle, ScavengingLootOracleConstants.TelemetryRingBufferId, ScavengingLootOracleConstants.TelemetryRingCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureScavengingVaultBuffer(vault, ref _distributionAuditHandle, ScavengingLootOracleConstants.DistributionAuditBufferId, ScavengingLootOracleConstants.DefaultAuditCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureScavengingVaultBuffer(vault, ref _csvScratchHandle, ScavengingLootOracleConstants.CsvScratchBufferId, ScavengingLootOracleConstants.DefaultCsvScratchBytes, NativeArrayOptions.UninitializedMemory, out _);
            if (!_vaultReady)
                ReleaseScavengingVaultHandles(vault);

            return _vaultReady;
        }

        private ScavengingLootOracleVaultViews ResolveViews()
        {
            ScavengingLootOracleVaultViews views = default;
            if (_vault == null || !_vaultReady)
                return views;

            TryResolveScavengingVaultBuffer(_vault, ref _lootEntriesHandle, ScavengingLootOracleConstants.LootEntriesBufferId, ScavengingLootOracleConstants.DefaultLootEntryCapacity, out views.LootEntries);
            TryResolveScavengingVaultBuffer(_vault, ref _requestsHandle, ScavengingLootOracleConstants.HarvestRequestsBufferId, ScavengingLootOracleConstants.DefaultRequestCapacity, out views.Requests);
            TryResolveScavengingVaultBuffer(_vault, ref _resolvedYieldsHandle, ScavengingLootOracleConstants.ResolvedYieldsBufferId, ScavengingLootOracleConstants.DefaultRequestCapacity, out views.ResolvedYields);
            TryResolveScavengingVaultBuffer(_vault, ref _biomeModifiersHandle, ScavengingLootOracleConstants.BiomeModifiersBufferId, ScavengingLootOracleConstants.DefaultBiomeModifierCapacity, out views.BiomeModifiers);
            TryResolveScavengingVaultBuffer(_vault, ref _telemetryRingHandle, ScavengingLootOracleConstants.TelemetryRingBufferId, ScavengingLootOracleConstants.TelemetryRingCapacity, out views.TelemetryRing);
            TryResolveScavengingVaultBuffer(_vault, ref _distributionAuditHandle, ScavengingLootOracleConstants.DistributionAuditBufferId, ScavengingLootOracleConstants.DefaultAuditCapacity, out views.DistributionAudit);
            TryResolveScavengingVaultBuffer(_vault, ref _csvScratchHandle, ScavengingLootOracleConstants.CsvScratchBufferId, ScavengingLootOracleConstants.DefaultCsvScratchBytes, out views.CsvScratch);
            return views;
        }

        private JobHandle EnsureEmergencyLootTableJob(JobHandle dependency)
        {
            if (_emergencyTableGenerated)
                return dependency;

            if (!TryResolveScavengingVaultBuffer(_vault, ref _lootEntriesHandle, ScavengingLootOracleConstants.LootEntriesBufferId, ScavengingLootOracleConstants.DefaultLootEntryCapacity, out NativeArray<LootTableEntryDTO> entries))
                return dependency;

            GenerateEmergencyMockLootTablesJob job = new GenerateEmergencyMockLootTablesJob
            {
                LootEntries = entries
            };
            _emergencyTableGenerated = true;
            _lootTableHydrated = true;
            _activeLootEntryCount = math.min(4, entries.Length);
            _activeLootTableHash = ScavengingLootOracleConstants.EmergencyTableHash;
            _activeLootTableVersion = 1u;
            return job.Schedule(dependency);
        }

        private void TryPrimeMonolithLootTable()
        {
            if (!_lootTableHydrated)
                TryImportLootCdfFromDataMonolith();
        }

        private JobHandle EnsureLootTableJob(JobHandle dependency)
        {
            if (_lootTableHydrated)
                return dependency;

            if (TryImportLootCdfFromDataMonolith())
                return dependency;

#if UNITY_EDITOR
            return EnsureEmergencyLootTableJob(dependency);
#else
            _lootTableHydrated = true;
            _activeLootEntryCount = 0;
            _activeLootTableHash = 0u;
            _activeLootTableVersion = 0u;
            return dependency;
#endif
        }

        private bool TryImportLootCdfFromDataMonolith()
        {
            if (!TryResolveScavengingVaultBuffer(_vault, ref _lootEntriesHandle, ScavengingLootOracleConstants.LootEntriesBufferId, ScavengingLootOracleConstants.DefaultLootEntryCapacity, out NativeArray<LootTableEntryDTO> entries) ||
                !H8StaticDataArena.TryGetSectionSpan(H8DataSectionId.LootCdf, out ReadOnlySpan<H8LootCdfRecord> records) ||
                records.Length <= 0)
            {
                return false;
            }

            uint tableHash = records[0].TableHash;
            if (tableHash == 0u)
                return false;

            int count = 0;
            int maxCount = math.min(entries.Length, records.Length);
            for (int i = 0; i < maxCount; i++)
            {
                H8LootCdfRecord source = records[i];
                if (source.TableHash != tableHash)
                    break;

                entries[count++] = new LootTableEntryDTO
                {
                    ItemHashID = source.ItemHash,
                    DropWeight = source.CumulativeWeight,
                    ConditionMask = ScavengingLootOracleConstants.ToolMaskAny,
                    _pad0 = source.TotalWeight
                };
            }

            if (count <= 0)
                return false;

            _activeLootEntryCount = count;
            _activeLootTableHash = tableHash;
            _activeLootTableVersion = ScavengingLootOracleConstants.MonolithTableVersion;
            _lootTableHydrated = true;
            _emergencyTableGenerated = false;
            return true;
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

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
            _registeredLateFrame = false;
        }

        private void CacheVaultCold()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (ReferenceEquals(_vault, vault))
                return;

            TryCompletePendingPublish(forceComplete: true);
            ReleaseScavengingVaultHandles(_vault);
            _vault = vault;
            InvalidateVaultHandles();
        }

        private void ReleaseVaultBinding()
        {
            TryCompletePendingPublish(forceComplete: true);
            ReleaseScavengingVaultHandles(_vault);
            _vault = null;
            InvalidateVaultHandles();
        }

        private void InvalidateVaultHandles()
        {
            _lootEntriesHandle = default;
            _requestsHandle = default;
            _resolvedYieldsHandle = default;
            _biomeModifiersHandle = default;
            _telemetryRingHandle = default;
            _distributionAuditHandle = default;
            _csvScratchHandle = default;
            _vaultReady = false;
            _emergencyTableGenerated = false;
            _lootTableHydrated = false;
            _activeLootEntryCount = 0;
            _activeBiomeModifierCount = 0;
            _activeLootTableHash = 0u;
            _activeLootTableVersion = 0u;
        }

        private void ReleaseScavengingVaultHandles(IDataVault vault)
        {
            ReleaseScavengingVaultHandle(vault, ref _lootEntriesHandle, ScavengingLootOracleConstants.LootEntriesBufferId);
            ReleaseScavengingVaultHandle(vault, ref _requestsHandle, ScavengingLootOracleConstants.HarvestRequestsBufferId);
            ReleaseScavengingVaultHandle(vault, ref _resolvedYieldsHandle, ScavengingLootOracleConstants.ResolvedYieldsBufferId);
            ReleaseScavengingVaultHandle(vault, ref _biomeModifiersHandle, ScavengingLootOracleConstants.BiomeModifiersBufferId);
            ReleaseScavengingVaultHandle(vault, ref _telemetryRingHandle, ScavengingLootOracleConstants.TelemetryRingBufferId);
            ReleaseScavengingVaultHandle(vault, ref _distributionAuditHandle, ScavengingLootOracleConstants.DistributionAuditBufferId);
            ReleaseScavengingVaultHandle(vault, ref _csvScratchHandle, ScavengingLootOracleConstants.CsvScratchBufferId);
        }

        private static bool EnsureScavengingVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (TryResolveScavengingVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, OwnerSystem, options);
            return TryResolveScavengingVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryResolveScavengingVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (IsScavengingVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsScavengingVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryReadScavengingVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsScavengingVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsScavengingVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private static void ReleaseScavengingVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsScavengingVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
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
            root.Add(new Button(ApplyTuning) { text = "Apply Vault Tuning" });
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

        private void ApplyTuning()
        {
            if (_auditLabel == null)
                return;

            bool applied = ScavengingLootOracleRuntime.TryApplyEditorTuning(
                _biomeScalarSlider.value,
                _toolBonusSlider.value,
                _rareDropSlider.value,
                out int entryCount,
                out int modifierCount);
            _auditLabel.text = applied
                ? $"Tuned Vault: entries {entryCount}, biome modifiers {modifierCount}"
                : "Tuning failed: Vault unavailable.";
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

        private unsafe void LoadCsv()
        {
            if (_auditLabel == null)
                return;

            string path = EditorUtility.OpenFilePanel("loot_distribution_tables.csv", Application.dataPath, "csv");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            FileInfo info = new FileInfo(path);
            if (info.Length <= 0L || info.Length > int.MaxValue)
            {
                _auditLabel.text = "CSV ingest failed: invalid byte length.";
                return;
            }

            using (NativeArray<byte> nativeBytes = new NativeArray<byte>((int)info.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(nativeBytes);
                int total = 0;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    Span<byte> span = new Span<byte>(destination, nativeBytes.Length);
                    while (total < nativeBytes.Length)
                    {
                        int read = stream.Read(span.Slice(total));
                        if (read <= 0)
                            break;

                        total += read;
                    }
                }

                if (total != nativeBytes.Length)
                {
                    _auditLabel.text = "CSV ingest failed: incomplete file read.";
                    return;
                }

                _auditLabel.text = ScavengingLootOracleRuntime.TryIngestLootDistributionCsvBytes(nativeBytes, out int entryCount)
                    ? $"CSV entries loaded: {entryCount}"
                    : "CSV ingest failed: Vault unavailable or no entries.";
            }
        }
    }
}
#endif
