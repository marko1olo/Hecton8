using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Data;
using Hecton8.Core.Generated;
using Hecton8.Core.Memory;
using Hecton8.UI;
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
        public const string InventoryFullMessage = "SCAVENGE BLOCKED // INVENTORY FULL";
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
        public const uint VisualScavengeLaneHash = 0x56534356u; // VSCV
        public const uint LootOracleSourceHash = 0x4C4F5243u; // LORC
        public const uint EmergencyTableHash = 0x454D4C54u; // EMLT
        public const uint MonolithTableVersion = 0x4838444Du; // H8DM
        public const uint CsvImportTableHash = 0x43535654u; // CSVT
        public const uint CsvImportTableVersion = 0x43535631u; // CSV1
        public const uint EditorTuningTableHash = 0x4544544Eu; // EDTN
        public const uint EditorTuningTableVersion = 0x45445431u; // EDT1
        public const uint EditorPreviewBiomeHash = 0x45504249u; // EPBI
        public const uint DefaultSessionSalt = 0x1251255Du;
        public const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_LOOT_ORACLE.bin";

        public static readonly BufferID LootEntriesBufferId = BufferID.ScavengingLootOracleRuntime_LootEntriesBufferId;
        public static readonly BufferID BiomeModifiersBufferId = BufferID.ScavengingLootOracleRuntime_BiomeModifiersBufferId;
        public static readonly BufferID DistributionAuditBufferId = BufferID.ScavengingLootOracleRuntime_DistributionAuditBufferId;
        public static readonly BufferID CsvScratchBufferId = BufferID.ScavengingLootOracleRuntime_CsvScratchBufferId;
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateEmergencyMockLootTablesJob : IJob
    {
        [NoAlias] public NativeArray<LootTableEntryDTO> LootEntries;

        public void Execute()
        {
            WriteEmergencyLootTable(LootEntries);
        }

        internal static void WriteEmergencyLootTable(NativeArray<LootTableEntryDTO> lootEntries)
        {
            if (!lootEntries.IsCreated || lootEntries.Length < 4)
                return;

            lootEntries[0] = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.TitaniumScrapHash,
                DropWeight = 55u,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskAny,
                _pad0 = 0u
            };
            lootEntries[1] = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.CopperOreHash,
                DropWeight = 82u,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskAny,
                _pad0 = 0u
            };
            lootEntries[2] = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.SulfurClumpsHash,
                DropWeight = 94u,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskCutter | ScavengingLootOracleConstants.ToolMaskDrill | ScavengingLootOracleConstants.ToolMaskExtractor,
                _pad0 = 0u
            };
            lootEntries[3] = new LootTableEntryDTO
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
        [ReadOnly, NoAlias] public NativeArray<LootTableEntryDTO>.ReadOnly LootEntries;
        [ReadOnly, NoAlias] public NativeArray<ScavengingBiomeModifierDTO>.ReadOnly BiomeModifiers;
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

            int slot = WrapTelemetrySlotNoModulo(TelemetryCursor + index, TelemetryRing.Length);
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
        private static int WrapTelemetrySlotNoModulo(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = math.max(0, value);
            wrapped -= math.select(0, safeCapacity, wrapped >= safeCapacity);
            return wrapped;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ScavengingLootOracleSelfAuditJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<LootTableEntryDTO>.ReadOnly LootEntries;
        [ReadOnly, NoAlias] public NativeArray<ScavengingBiomeModifierDTO>.ReadOnly BiomeModifiers;
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

#if UNITY_EDITOR
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
#endif

    public sealed class ScavengingLootOracleRuntime : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        private static ScavengingLootOracleRuntime _host;
        private static bool _signalLanesConfigured;
        private static bool _staticReset;
        private static bool _coldHostCleanupAllowed;

        private const string HostObjectName = "ScavengingLootOracleRuntime";
        private const SystemID OwnerSystem = SystemID.GameplayLoot;
        private const uint SimulationSystemHash = ScavengingLootOracleConstants.LootOracleSourceHash;
        private const uint PostSimulationSystemHash = ScavengingLootOracleConstants.LootOracleSourceHash ^ 0x504F5354u; // POST
        private const uint VisualSyncSystemHash = ScavengingLootOracleConstants.LootOracleSourceHash ^ 0x5653594Eu; // VSYN

        private struct SimulationNativeScratch
        {
            public NativeArray<ScavengingHarvestRequestDTO> Requests;
            public NativeArray<ScavengingResolvedYieldDTO> ResolvedYields;
            public NativeArray<VisualScavengeSignal> VisualSignals;
            public NativeArray<ScavengingTelemetryEntry> TelemetryRing;
            public NativeArray<uint> DistributionAudit;
            public NativeArray<LootTableEntryDTO> LootEntryScratch;

            public bool IsReady()
            {
                return Requests.IsCreated &&
                       ResolvedYields.IsCreated &&
                       VisualSignals.IsCreated &&
                       TelemetryRing.IsCreated &&
                       DistributionAudit.IsCreated &&
                       LootEntryScratch.IsCreated &&
                       Requests.Length == ScavengingLootOracleConstants.DefaultRequestCapacity &&
                       ResolvedYields.Length == ScavengingLootOracleConstants.DefaultRequestCapacity &&
                       VisualSignals.Length == ScavengingLootOracleConstants.DefaultRequestCapacity &&
                       TelemetryRing.Length == ScavengingLootOracleConstants.TelemetryRingCapacity &&
                       DistributionAudit.Length == ScavengingLootOracleConstants.DefaultAuditCapacity &&
                       LootEntryScratch.Length == ScavengingLootOracleConstants.DefaultLootEntryCapacity;
            }

            public void Ensure()
            {
                if (IsReady())
                    return;

                Dispose();
                try
                {
                    Requests = AllocateNativeArray<ScavengingHarvestRequestDTO>(
                        ScavengingLootOracleConstants.DefaultRequestCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        nameof(Requests));
                    ResolvedYields = AllocateNativeArray<ScavengingResolvedYieldDTO>(
                        ScavengingLootOracleConstants.DefaultRequestCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        nameof(ResolvedYields));
                    VisualSignals = AllocateNativeArray<VisualScavengeSignal>(
                        ScavengingLootOracleConstants.DefaultRequestCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        nameof(VisualSignals));
                    TelemetryRing = AllocateNativeArray<ScavengingTelemetryEntry>(
                        ScavengingLootOracleConstants.TelemetryRingCapacity,
                        NativeArrayOptions.ClearMemory,
                        nameof(TelemetryRing));
                    DistributionAudit = AllocateNativeArray<uint>(
                        ScavengingLootOracleConstants.DefaultAuditCapacity,
                        NativeArrayOptions.ClearMemory,
                        nameof(DistributionAudit));
                    LootEntryScratch = AllocateNativeArray<LootTableEntryDTO>(
                        ScavengingLootOracleConstants.DefaultLootEntryCapacity,
                        NativeArrayOptions.UninitializedMemory,
                        nameof(LootEntryScratch));
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                DisposeNativeArray(ref Requests);
                DisposeNativeArray(ref ResolvedYields);
                DisposeNativeArray(ref VisualSignals);
                DisposeNativeArray(ref TelemetryRing);
                DisposeNativeArray(ref DistributionAudit);
                DisposeNativeArray(ref LootEntryScratch);
            }

            private static NativeArray<T> AllocateNativeArray<T>(int length, NativeArrayOptions options, string label) where T : struct
            {
                NativeArray<T> array = H8Memory.Allocate<T>(length, OwnerSystem, Allocator.Persistent, options);
                if (!array.IsCreated)
                    throw new InvalidOperationException($"{nameof(ScavengingLootOracleRuntime)} native allocation failed for {label}.");

                return array;
            }

            private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
            {
                H8Memory.Release(ref array, OwnerSystem);
            }
        }

        private IDataVault _vault;
        private IWorldSeedProvider _worldSeedProvider;
        private ulong _sessionId;
        private SimulationNativeScratch _nativeScratch;
        private VaultGenerationHandle<LootTableEntryDTO> _lootEntriesHandle;
        private VaultGenerationHandle<ScavengingBiomeModifierDTO> _biomeModifiersHandle;
        private VaultGenerationHandle<uint> _distributionAuditHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private int _queuedCount;
        private int _telemetryCursor;
        private JobHandle _pendingPublishHandle;
        private int _pendingPublishCount;
        private bool _publishPending;
        private int _pendingVisualSignalCount;
        private bool _visualPublishPending;
        private int _signalDropCount;
        private uint _simulationFrameCounter;
        private int _activeLootEntryCount;
        private int _activeBiomeModifierCount;
        private uint _activeBiomeHash;
        private bool _vaultReady;
        private bool _emergencyTableGenerated;
        private bool _lootTableHydrated;
        private uint _activeLootTableHash;
        private uint _activeLootTableVersion;
        private bool _registeredSimulationDispatcher;
        private bool _registeredPostSimulationDispatcher;
        private bool _registeredVisualSyncDispatcher;
        private bool _registeredHotSwap;
        private SimulationPhaseSystem _simulationPhase;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private VisualSyncPhaseSystem _visualSyncPhase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _coldHostCleanupAllowed = true;
            try
            {
                DestroyUnboundHostObjectsCold();
                _host = null;
                _signalLanesConfigured = false;
                _staticReset = true;
            }
            finally
            {
                _coldHostCleanupAllowed = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapHostAfterSceneLoad()
        {
            if (Application.isPlaying)
                ConfigureSignalLanes();
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
            ScavengingLootOracleRuntime host = TryGetPreparedHostForHot();
            if (host == null)
                return false;

            if (host._publishPending)
                return false;

            if (!host._nativeScratch.Requests.IsCreated)
            {
                return false;
            }

            NativeArray<ScavengingHarvestRequestDTO> requests = host._nativeScratch.Requests;
            if (!requests.IsCreated || host._queuedCount >= requests.Length)
                return false;

            bool full = !inventoryCapacityAvailable;
            int slot = host._queuedCount++;
            float quality = ScavengingLootOracleMath.SanitizeQualityWeight(HomeostasisBrain.GlobalQualityWeight);
            uint frame = host.PeekNextSimulationFrame();
            uint clampedQuantity = ClampItemSignalQuantity(quantity);
            uint quantityClampFlag = clampedQuantity != quantity
                ? ScavengingLootOracleConstants.RequestFlagQuantityClamped
                : 0u;
            uint lootEntryCount = host._activeLootEntryCount > 0
                ? (uint)math.min(host._activeLootEntryCount, ScavengingLootOracleConstants.DefaultLootEntryCapacity)
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
            request.SessionID = host.ResolveCachedSessionId();
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
            requests[slot] = request;

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
            if (!IsColdManualOracleOperationAllowed())
                return false;

            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.PrepareVaultCold())
                return false;

            JobHandle handle = host.EnsureEmergencyLootTableJob(default);
            // COLD SYNC JOB: manual/editor fallback generation must finish before the caller inspects the table.
            ForceCompleteColdJobInPostSimulationWindow(ref handle);
            return true;
        }

#if UNITY_EDITOR
        public static bool TryIngestLootDistributionCsvBytes(NativeArray<byte> csvBytes, out int entryCount)
        {
            entryCount = 0;
            if (!IsColdManualOracleOperationAllowed())
                return false;

            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.PrepareVaultCold())
                return false;

            NativeArray<LootTableEntryDTO> scratchEntries = host._nativeScratch.LootEntryScratch;
            if (!scratchEntries.IsCreated ||
                scratchEntries.Length < ScavengingLootOracleConstants.DefaultLootEntryCapacity)
                return false;

            entryCount = ScavengingLootOracleCsvParser.ParseLootDistributionCsvBytes(csvBytes, scratchEntries);
            if (entryCount <= 0)
                return false;

            if (!TryAcquireScavengingVaultBuffer(
                    host._vault,
                    ref host._lootEntriesHandle,
                    ScavengingLootOracleConstants.LootEntriesBufferId,
                    ScavengingLootOracleConstants.DefaultLootEntryCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<LootTableEntryDTO> entries))
                return false;

            try
            {
                int count = math.min(entryCount, math.min(entries.Length, scratchEntries.Length));
                for (int i = 0; i < count; i++)
                    entries[i] = scratchEntries[i];
                entryCount = count;
            }
            finally
            {
                host._vault.ReleaseWriteLock(in host._lootEntriesHandle, OwnerSystem);
            }

            if (entryCount <= 0)
                return false;

            host._activeLootEntryCount = entryCount;
            host._activeLootTableHash = ScavengingLootOracleConstants.CsvImportTableHash;
            host._activeLootTableVersion = ScavengingLootOracleConstants.CsvImportTableVersion;
            host._lootTableHydrated = true;
            host._emergencyTableGenerated = false;
            return true;
        }
#endif

#if UNITY_EDITOR
        public static bool TryApplyEditorTuning(float biomeScalar, float toolYieldBonus, float rareDropRate, out int entryCount, out int modifierCount)
        {
            entryCount = 0;
            modifierCount = 0;
            if (!IsColdManualOracleOperationAllowed())
                return false;

            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.PrepareVaultCold())
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
            LootTableEntryDTO loot0 = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.TitaniumScrapHash,
                DropWeight = cdf,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskAny,
                _pad0 = 0u
            };
            cdf = AddClampedCdf(cdf, copperWeight);
            LootTableEntryDTO loot1 = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.CopperOreHash,
                DropWeight = cdf,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskAny,
                _pad0 = 0u
            };
            cdf = AddClampedCdf(cdf, sulfurWeight);
            LootTableEntryDTO loot2 = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.SulfurClumpsHash,
                DropWeight = cdf,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskCutter | ScavengingLootOracleConstants.ToolMaskDrill | ScavengingLootOracleConstants.ToolMaskExtractor,
                _pad0 = 0u
            };
            cdf = AddClampedCdf(cdf, abyssalWeight);
            LootTableEntryDTO loot3 = new LootTableEntryDTO
            {
                ItemHashID = H8Hashes.Items.AbyssalCrystalHash,
                DropWeight = cdf,
                ConditionMask = ScavengingLootOracleConstants.ToolMaskDrill | ScavengingLootOracleConstants.ToolMaskExtractor,
                _pad0 = 0u
            };

            uint targetBiomeHash = host._activeBiomeHash != 0u
                ? host._activeBiomeHash
                : ScavengingLootOracleConstants.EditorPreviewBiomeHash;

            uint biomeMultiplierMilli = ToEditorMilli(safeBiomeScalar);
            ScavengingBiomeModifierDTO biome0 = new ScavengingBiomeModifierDTO
            {
                BiomeHash = targetBiomeHash,
                ItemHashID = H8Hashes.Items.SulfurClumpsHash,
                WeightMultiplierMilli = biomeMultiplierMilli,
                _pad0 = 0u
            };
            ScavengingBiomeModifierDTO biome1 = new ScavengingBiomeModifierDTO
            {
                BiomeHash = targetBiomeHash,
                ItemHashID = H8Hashes.Items.AbyssalCrystalHash,
                WeightMultiplierMilli = biomeMultiplierMilli,
                _pad0 = 0u
            };

            if (!TryAcquireScavengingVaultBuffer(
                    host._vault,
                    ref host._lootEntriesHandle,
                    ScavengingLootOracleConstants.LootEntriesBufferId,
                    ScavengingLootOracleConstants.DefaultLootEntryCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<LootTableEntryDTO> lootEntries))
                return false;

            try
            {
                if (lootEntries.Length < 4)
                    return false;

                lootEntries[0] = loot0;
                lootEntries[1] = loot1;
                lootEntries[2] = loot2;
                lootEntries[3] = loot3;
            }
            finally
            {
                host._vault.ReleaseWriteLock(in host._lootEntriesHandle, OwnerSystem);
            }

            if (!TryAcquireScavengingVaultBuffer(
                    host._vault,
                    ref host._biomeModifiersHandle,
                    ScavengingLootOracleConstants.BiomeModifiersBufferId,
                    ScavengingLootOracleConstants.DefaultBiomeModifierCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<ScavengingBiomeModifierDTO> biomeModifiers))
                return false;

            try
            {
                if (biomeModifiers.Length < 2)
                    return false;

                biomeModifiers[0] = biome0;
                biomeModifiers[1] = biome1;
            }
            finally
            {
                host._vault.ReleaseWriteLock(in host._biomeModifiersHandle, OwnerSystem);
            }

            if (host._activeBiomeHash == 0u)
                host._activeBiomeHash = targetBiomeHash;
            host._activeLootEntryCount = 4;
            host._activeBiomeModifierCount = 2;
            host._activeLootTableHash = ScavengingLootOracleConstants.EditorTuningTableHash;
            host._activeLootTableVersion = ScavengingLootOracleConstants.EditorTuningTableVersion;
            host._lootTableHydrated = true;
            host._emergencyTableGenerated = false;
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
            if (host == null || !host.IsVaultReadyForHot())
                return false;

            NativeArray<ScavengingTelemetryEntry> ring = host._nativeScratch.TelemetryRing;
            if (!ring.IsCreated)
                return false;

            const int HeaderBytes = 4;
            const int RowBytes = 100;
            int totalBytes = HeaderBytes + ring.Length * RowBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                totalBytes,
                nameof(ScavengingLootOracleRuntime),
                "scavengingLootOracleTelemetryDumpPayload");
            try
            {
                WriteInt32LittleEndian(payload, 0, ring.Length);
                for (int i = 0; i < ring.Length; i++)
                {
                    ScavengingTelemetryEntry entry = ring[i];
                    int offset = HeaderBytes + i * RowBytes;
                    WriteAupCompact36(payload, offset, entry.NodeAup);
                    WriteUInt64LittleEndian(payload, offset + 36, entry.ResourceNodeHash);
                    WriteUInt32LittleEndian(payload, offset + 44, entry.SelectedItemHashID);
                    WriteUInt32LittleEndian(payload, offset + 48, entry.OreHash);
                    WriteUInt32LittleEndian(payload, offset + 52, entry.Frame);
                    WriteUInt32LittleEndian(payload, offset + 56, entry.TotalWeight);
                    WriteUInt32LittleEndian(payload, offset + 60, entry.Roll);
                    WriteUInt32LittleEndian(payload, offset + 64, entry.Flags);
                    WriteUInt32LittleEndian(payload, offset + 68, entry.EstimatedCpuMicroseconds);
                    WriteUInt32LittleEndian(payload, offset + 72, entry.TableHash);
                    WriteUInt32LittleEndian(payload, offset + 76, entry.RequestId);
                    WriteFloat32LittleEndian(payload, offset + 80, entry.GlobalQualityWeight);
                    WriteUInt32LittleEndian(payload, offset + 84, entry.DepletionWordIndex);
                    WriteUInt32LittleEndian(payload, offset + 88, entry.DistributionBucket);
                    WriteUInt64LittleEndian(payload, offset + 92, entry.DepletionMask);
                }

                return NativeFaultDumpWriter.TryWriteAll(
                    ScavengingLootOracleConstants.TelemetryDumpRelativePath,
                    payload,
                    totalBytes);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ScavengingLootOracleRuntime),
                    "scavengingLootOracleTelemetryDumpPayload");
            }
        }

        private static void WriteAupCompact36(NativeArray<byte> payload, int offset, AbsoluteUniversePosition aup)
        {
            WriteInt64LittleEndian(payload, offset, aup.GridX);
            WriteInt64LittleEndian(payload, offset + 8, aup.GridY);
            WriteInt64LittleEndian(payload, offset + 16, aup.GridZ);
            WriteFloat32LittleEndian(payload, offset + 24, aup.LocalX);
            WriteFloat32LittleEndian(payload, offset + 28, aup.LocalY);
            WriteFloat32LittleEndian(payload, offset + 32, aup.LocalZ);
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> payload, int offset, float value)
        {
            WriteUInt32LittleEndian(payload, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, int offset, int value)
        {
            WriteUInt32LittleEndian(payload, offset, unchecked((uint)value));
        }

        private static void WriteInt64LittleEndian(NativeArray<byte> payload, int offset, long value)
        {
            WriteUInt64LittleEndian(payload, offset, unchecked((ulong)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, int offset, uint value)
        {
            payload[offset] = (byte)value;
            payload[offset + 1] = (byte)(value >> 8);
            payload[offset + 2] = (byte)(value >> 16);
            payload[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> payload, int offset, ulong value)
        {
            payload[offset] = (byte)value;
            payload[offset + 1] = (byte)(value >> 8);
            payload[offset + 2] = (byte)(value >> 16);
            payload[offset + 3] = (byte)(value >> 24);
            payload[offset + 4] = (byte)(value >> 32);
            payload[offset + 5] = (byte)(value >> 40);
            payload[offset + 6] = (byte)(value >> 48);
            payload[offset + 7] = (byte)(value >> 56);
        }

        public static bool TryRunDistributionSelfAudit(out NativeArray<uint>.ReadOnly auditCounts)
        {
            auditCounts = default;
            if (!IsColdManualOracleOperationAllowed())
                return false;

            ScavengingLootOracleRuntime host = EnsureHost();
            if (host == null || !host.PrepareVaultCold())
                return false;

            JobHandle dependency = host.EnsureLootTableJob(default);
            NativeArray<uint> auditScratch = host._nativeScratch.DistributionAudit;
            if (!host.TryResolveHotLootReadViews(out NativeArray<LootTableEntryDTO>.ReadOnly lootEntries, out NativeArray<ScavengingBiomeModifierDTO>.ReadOnly biomeModifiers) ||
                !auditScratch.IsCreated ||
                auditScratch.Length < ScavengingLootOracleConstants.DefaultAuditCapacity)
                return false;

            ScavengingLootOracleSelfAuditJob auditJob = new ScavengingLootOracleSelfAuditJob
            {
                LootEntries = lootEntries,
                BiomeModifiers = biomeModifiers,
                DistributionAudit = auditScratch,
                RollCount = ScavengingLootOracleConstants.SelfAuditRollCount,
                EntryCount = host._activeLootEntryCount > 0 ? (uint)host._activeLootEntryCount : 0u,
                BiomeModifierCount = host._activeBiomeModifierCount,
                ToolHashID = ScavengingLootOracleConstants.ToolMaskKnife |
                             ScavengingLootOracleConstants.ToolMaskCutter |
                             ScavengingLootOracleConstants.ToolMaskDrill |
                             ScavengingLootOracleConstants.ToolMaskExtractor,
                BiomeHash = host._activeBiomeHash,
                SessionID = host.ResolveCachedSessionId()
            };
            JobHandle handle = auditJob.Schedule(dependency);
            // COLD SYNC JOB: editor self-audit runs into owner scratch first; Vault copy below is a short lock.
            ForceCompleteColdJobInPostSimulationWindow(ref handle);

            if (!TryAcquireScavengingVaultBuffer(
                    host._vault,
                    ref host._distributionAuditHandle,
                    ScavengingLootOracleConstants.DistributionAuditBufferId,
                    ScavengingLootOracleConstants.DefaultAuditCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<uint> distributionAudit))
                return false;

            try
            {
                int count = math.min(distributionAudit.Length, auditScratch.Length);
                for (int i = 0; i < count; i++)
                    distributionAudit[i] = auditScratch[i];
            }
            finally
            {
                host._vault.ReleaseWriteLock(in host._distributionAuditHandle, OwnerSystem);
            }

            auditCounts = auditScratch.AsReadOnly();
            return true;
        }

#if UNITY_EDITOR
        internal static void DrawHighestProbabilityGizmo(ResourceNode node, Vector3 position)
        {
            if (node == null)
                return;

            uint itemHash = H8Hashes.Items.TitaniumScrapHash;
            ScavengingLootOracleRuntime host = IsColdManualOracleOperationAllowed() ? EnsureHost() : null;
            if (host != null && host.PrepareVaultCold())
            {
                JobHandle dependency = host.EnsureLootTableJob(default);
                // COLD SYNC JOB: editor gizmo needs the preview table before reading Vault rows.
                ForceCompleteColdJobInPostSimulationWindow(ref dependency);
                if (TryReadScavengingVaultBuffer(host._vault, in host._lootEntriesHandle, ScavengingLootOracleConstants.LootEntriesBufferId, ScavengingLootOracleConstants.DefaultLootEntryCapacity, out NativeArray<LootTableEntryDTO>.ReadOnly entries))
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
            if (!TryBindAuthoredHostCold())
                return;

            ConfigureSignalLanes();
            CacheVaultCold();
            PrepareVaultCold();
            TryRegisterHotSwapListener();
            TryRegisterDispatcherPhases();
        }

        private void OnDisable()
        {
            ShutdownForLifecycle();
        }

        private void OnDestroy()
        {
            ShutdownForLifecycle();
        }

        private void ShutdownForLifecycle()
        {
            ForceCompletePendingPublishForLifecycle();
            TryUnregisterDispatcherPhases();
            TryUnregisterHotSwapListener();
            ReleaseVaultBinding();
            if (ReferenceEquals(_host, this))
                _host = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    ForceCompletePendingPublishForLifecycle();
                    IDataVault previousVault = previousService as IDataVault;
                    if (previousVault == null)
                        previousVault = _vault;

                    if (!ReleaseScavengingVaultHandles(previousVault))
                    {
                        _vaultReady = false;
                        break;
                    }

                    _vault = currentService as IDataVault;
                    InvalidateVaultHandles();
                    PrepareVaultCold();
                    break;
                case GlobalRegistryServiceSlot.WorldSeedProvider:
                    _worldSeedProvider = currentService as IWorldSeedProvider;
                    _sessionId = BuildSessionId(_worldSeedProvider);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterDispatcherPhases();
                    if (currentService != null)
                        TryRegisterDispatcherPhases();
                    break;
            }
        }

        private JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            DrainBiomeSignals();
            if (_publishPending)
                return dependsOn;

            if (_queuedCount <= 0)
                return dependsOn;

            if (!IsVaultReadyForHot())
            {
                if (_vault != null && _vault.IsCompactionFenceActive)
                    return dependsOn;

                _queuedCount = 0;
                return dependsOn;
            }

            if (!_lootTableHydrated)
            {
                _queuedCount = 0;
                return dependsOn;
            }

            int count = _queuedCount;

            if (!TryResolveHotLootReadViews(
                    out NativeArray<LootTableEntryDTO>.ReadOnly lootEntries,
                    out NativeArray<ScavengingBiomeModifierDTO>.ReadOnly biomeModifiers) ||
                !_nativeScratch.Requests.IsCreated ||
                !_nativeScratch.ResolvedYields.IsCreated ||
                !_nativeScratch.TelemetryRing.IsCreated)
            {
                if (_vault != null && _vault.IsCompactionFenceActive)
                    return dependsOn;

                _queuedCount = 0;
                return dependsOn;
            }

            _queuedCount = 0;

            LootResolutionJob resolveJob = default;
            resolveJob.Requests = _nativeScratch.Requests;
            resolveJob.LootEntries = lootEntries;
            resolveJob.BiomeModifiers = biomeModifiers;
            resolveJob.ResolvedYields = _nativeScratch.ResolvedYields;
            resolveJob.TelemetryRing = _nativeScratch.TelemetryRing;
            resolveJob.RequestCount = count;
            resolveJob.BiomeModifierCount = _activeBiomeModifierCount;
            resolveJob.Frame = AdvanceSimulationFrame();
            resolveJob.TelemetryCursor = _telemetryCursor;
            JobHandle resolveHandle = resolveJob.Schedule(dependsOn);

            _pendingPublishHandle = resolveHandle;
            _pendingPublishCount = count;
            _publishPending = true;
            H8Memory.RegisterActiveJob(SystemID.GameplayLoot, _pendingPublishHandle);
            return resolveHandle;
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            TryCompletePendingPublish(forceComplete: false);
        }

        private bool TryCompletePendingPublish(bool forceComplete)
        {
            if (!_publishPending)
                return true;

            if (!DispatcherJobFence.TryComplete(ref _pendingPublishHandle, forceComplete))
                return false;

            NativeArray<ScavengingResolvedYieldDTO> resolvedYields = _nativeScratch.ResolvedYields;
            if (resolvedYields.IsCreated)
                PublishResolvedTruthAndQueueVisuals(resolvedYields, _pendingPublishCount, forceComplete);

            _publishPending = false;
            int nextTelemetryCursor = _telemetryCursor + _pendingPublishCount;
            nextTelemetryCursor -= math.select(0, ScavengingLootOracleConstants.TelemetryRingCapacity, nextTelemetryCursor >= ScavengingLootOracleConstants.TelemetryRingCapacity);
            _telemetryCursor = nextTelemetryCursor;
            _pendingPublishCount = 0;
            H8Memory.RegisterActiveJob(SystemID.GameplayLoot, default);
            return true;
        }

        private void PublishResolvedTruthAndQueueVisuals(NativeArray<ScavengingResolvedYieldDTO> resolvedYields, int yieldCount, bool discardVisuals)
        {
            int count = math.min(yieldCount, resolvedYields.Length);
            NativeArray<VisualScavengeSignal> visualSignals = _nativeScratch.VisualSignals;
            int visualCount = !discardVisuals && _visualPublishPending && visualSignals.IsCreated
                ? math.min(_pendingVisualSignalCount, visualSignals.Length)
                : 0;
            for (int i = 0; i < count; i++)
            {
                ScavengingResolvedYieldDTO yield = resolvedYields[i];
                if (((uint)yield.Flags & ScavengingLootOracleConstants.ResultFlagInventoryFull) != 0u)
                {
                    NotificationEvents.TryPushWarning(ScavengingLootOracleConstants.InventoryFullMessage.AsSpan());
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
                SignalBus<ItemAcquiredSignal>.TryPushTracked(in itemSignal, ref _signalDropCount);

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
                if (!discardVisuals && visualSignals.IsCreated && visualCount < visualSignals.Length)
                {
                    visualSignals[visualCount] = visualSignal;
                    visualCount++;
                }
                else if (!discardVisuals)
                {
                    _signalDropCount++;
                }

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
                SignalBus<ResourceDepletionDeltaSignal>.TryPushTracked(in depletionSignal, ref _signalDropCount);
            }

            if (discardVisuals || visualCount <= 0)
            {
                _pendingVisualSignalCount = 0;
                _visualPublishPending = false;
                return;
            }

            _pendingVisualSignalCount = visualCount;
            _visualPublishPending = true;
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            PublishQueuedVisualSignalsVisualSync();
        }

        private void PublishQueuedVisualSignalsVisualSync()
        {
            if (!_visualPublishPending)
                return;

            NativeArray<VisualScavengeSignal> visualSignals = _nativeScratch.VisualSignals;
            int count = visualSignals.IsCreated
                ? math.min(_pendingVisualSignalCount, visualSignals.Length)
                : 0;
            for (int i = 0; i < count; i++)
            {
                VisualScavengeSignal visualSignal = visualSignals[i];
                SignalBus<VisualScavengeSignal>.TryPushTracked(in visualSignal, ref _signalDropCount);
            }

            _pendingVisualSignalCount = 0;
            _visualPublishPending = false;
        }

        private void ClearQueuedVisualSignalsForLifecycle()
        {
            _pendingVisualSignalCount = 0;
            _visualPublishPending = false;
        }

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

        private static ScavengingLootOracleRuntime EnsureHost()
        {
            return _host;
        }

        private static bool IsColdManualOracleOperationAllowed()
        {
            return !Application.isPlaying;
        }

        private static void DestroyUnboundHostObjectsCold()
        {
            if (!_coldHostCleanupAllowed || Application.isPlaying)
                return;

#if UNITY_EDITOR
            GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None); // COLD ALLOC: reload cleanup scan for HideAndDontSave orphan hosts.
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null ||
                    !string.Equals(candidate.name, HostObjectName, StringComparison.Ordinal) ||
                    candidate.GetComponent<ScavengingLootOracleRuntime>() != null)
                {
                    continue;
                }

#if UNITY_EDITOR
                DestroyImmediate(candidate);
#else
                Destroy(candidate);
#endif
            }
#endif
        }

        private bool TryBindAuthoredHostCold()
        {
            if (_host == null || ReferenceEquals(_host, this))
            {
                _host = this;
                return true;
            }

            enabled = false;
            return false;
        }

        private static ScavengingLootOracleRuntime TryGetPreparedHostForHot()
        {
            ScavengingLootOracleRuntime host = _host;
            return host != null && host.IsVaultReadyForHot()
                ? host
                : null;
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
            SignalBus<ItemAcquiredSignal>.EnsureInitialized();
            SignalBus<ResourceDepletionDeltaSignal>.EnsureInitialized();
            _signalLanesConfigured = true;
            _staticReset = true;
        }

        private bool PrepareVaultCold()
        {
            CacheSessionIdCold();
            if (!EnsureVaultCold())
                return false;

            if (!EnsureSimulationNativeScratchCold())
                return false;

            PrepareLootTableCold();
            return _lootTableHydrated;
        }

        private bool IsVaultReadyForHot()
        {
            return _vaultReady &&
                   _vault != null &&
                   !_vault.IsCompactionFenceActive &&
                   _lootTableHydrated;
        }

        private bool EnsureVaultCold()
        {
            if (_vaultReady && _vault != null)
                return true;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            _vaultReady =
                EnsureScavengingVaultBuffer(vault, ref _lootEntriesHandle, ScavengingLootOracleConstants.LootEntriesBufferId, ScavengingLootOracleConstants.DefaultLootEntryCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureScavengingVaultBuffer(vault, ref _biomeModifiersHandle, ScavengingLootOracleConstants.BiomeModifiersBufferId, ScavengingLootOracleConstants.DefaultBiomeModifierCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureScavengingVaultBuffer(vault, ref _distributionAuditHandle, ScavengingLootOracleConstants.DistributionAuditBufferId, ScavengingLootOracleConstants.DefaultAuditCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureScavengingVaultBuffer(vault, ref _csvScratchHandle, ScavengingLootOracleConstants.CsvScratchBufferId, ScavengingLootOracleConstants.DefaultCsvScratchBytes, NativeArrayOptions.UninitializedMemory, out _);
            if (!_vaultReady)
            {
                if (ReleaseScavengingVaultHandles(vault))
                    InvalidateVaultHandles();
            }

            return _vaultReady;
        }

        private bool TryResolveHotLootReadViews(
            out NativeArray<LootTableEntryDTO>.ReadOnly lootEntries,
            out NativeArray<ScavengingBiomeModifierDTO>.ReadOnly biomeModifiers)
        {
            lootEntries = default;
            biomeModifiers = default;
            if (_vault == null || !_vaultReady || !_lootTableHydrated || _vault.IsCompactionFenceActive)
                return false;

            return TryReadScavengingVaultBuffer(
                       _vault,
                       in _lootEntriesHandle,
                       ScavengingLootOracleConstants.LootEntriesBufferId,
                       ScavengingLootOracleConstants.DefaultLootEntryCapacity,
                       out lootEntries) &&
                   TryReadScavengingVaultBuffer(
                       _vault,
                       in _biomeModifiersHandle,
                       ScavengingLootOracleConstants.BiomeModifiersBufferId,
                       ScavengingLootOracleConstants.DefaultBiomeModifierCapacity,
                       out biomeModifiers);
        }

        private JobHandle EnsureEmergencyLootTableJob(JobHandle dependency)
        {
            if (_emergencyTableGenerated)
                return dependency;

            if (!TryAcquireScavengingVaultBuffer(
                    _vault,
                    ref _lootEntriesHandle,
                    ScavengingLootOracleConstants.LootEntriesBufferId,
                    ScavengingLootOracleConstants.DefaultLootEntryCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<LootTableEntryDTO> entries))
                return dependency;

            try
            {
                GenerateEmergencyMockLootTablesJob.WriteEmergencyLootTable(entries);
            }
            finally
            {
                _vault.ReleaseWriteLock(in _lootEntriesHandle, OwnerSystem);
            }

            _emergencyTableGenerated = true;
            _lootTableHydrated = true;
            _activeLootEntryCount = 4;
            _activeLootTableHash = ScavengingLootOracleConstants.EmergencyTableHash;
            _activeLootTableVersion = 1u;
            return dependency;
        }

        private void TryPrimeMonolithLootTable()
        {
            if (!_lootTableHydrated)
                TryImportLootCdfFromDataMonolith();
        }

        private void PrepareLootTableCold()
        {
            if (_lootTableHydrated)
                return;

            TryPrimeMonolithLootTable();
            if (_lootTableHydrated)
                return;

#if UNITY_EDITOR
            JobHandle handle = EnsureEmergencyLootTableJob(default);
            ForceCompleteColdJobInPostSimulationWindow(ref handle);
#else
            _lootTableHydrated = true;
            _activeLootEntryCount = 0;
            _activeLootTableHash = 0u;
            _activeLootTableVersion = 0u;
#endif
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
            if (!H8StaticDataArena.TryGetSectionSpan(H8DataSectionId.LootCdf, out ReadOnlySpan<H8LootCdfRecord> records) ||
                records.Length <= 0)
            {
                return false;
            }

            uint tableHash = records[0].TableHash;
            if (tableHash == 0u)
                return false;

            int recordCount = 0;
            int recordCapacity = math.min(ScavengingLootOracleConstants.DefaultLootEntryCapacity, records.Length);
            for (int i = 0; i < recordCapacity; i++)
            {
                if (records[i].TableHash != tableHash)
                    break;

                recordCount++;
            }

            if (recordCount <= 0)
                return false;

            if (!TryAcquireScavengingVaultBuffer(
                    _vault,
                    ref _lootEntriesHandle,
                    ScavengingLootOracleConstants.LootEntriesBufferId,
                    ScavengingLootOracleConstants.DefaultLootEntryCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<LootTableEntryDTO> entries))
                return false;

            int count = 0;
            try
            {
                int maxCount = math.min(entries.Length, recordCount);
                for (int i = 0; i < maxCount; i++)
                {
                    H8LootCdfRecord source = records[i];
                    entries[count++] = new LootTableEntryDTO
                    {
                        ItemHashID = source.ItemHash,
                        DropWeight = source.CumulativeWeight,
                        ConditionMask = ScavengingLootOracleConstants.ToolMaskAny,
                        _pad0 = source.TotalWeight
                    };
                }
            }
            finally
            {
                _vault.ReleaseWriteLock(in _lootEntriesHandle, OwnerSystem);
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

        private void TryRegisterDispatcherPhases()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (_simulationPhase == null)
                _simulationPhase = new SimulationPhaseSystem(this);
            if (_postSimulationPhase == null)
                _postSimulationPhase = new PostSimulationPhaseSystem(this);
            if (_visualSyncPhase == null)
                _visualSyncPhase = new VisualSyncPhaseSystem(this);

            if (!_registeredSimulationDispatcher)
                _registeredSimulationDispatcher = GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase);

            if (!_registeredSimulationDispatcher)
                return;

            if (!_registeredPostSimulationDispatcher)
            {
                if (GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
                {
                    _registeredPostSimulationDispatcher = true;
                }
                else
                {
                    GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                    _registeredSimulationDispatcher = false;
                    return;
                }
            }

            if (_registeredVisualSyncDispatcher)
                return;

            if (GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase))
            {
                _registeredVisualSyncDispatcher = true;
                return;
            }

            GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
            _registeredPostSimulationDispatcher = false;
            GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
            _registeredSimulationDispatcher = false;
        }

        private void TryUnregisterDispatcherPhases()
        {
            if (_registeredVisualSyncDispatcher)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncPhase);
                _registeredVisualSyncDispatcher = false;
            }

            if (_registeredPostSimulationDispatcher)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulationDispatcher = false;
            }

            if (_registeredSimulationDispatcher)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                _registeredSimulationDispatcher = false;
            }
        }

        private bool ForceCompletePendingPublishForLifecycle()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                bool completed = TryCompletePendingPublish(forceComplete: true);
                ClearQueuedVisualSignalsForLifecycle();
                return completed;
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private static void ForceCompleteColdJobInPostSimulationWindow(ref JobHandle handle)
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private void CacheVaultCold()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (ReferenceEquals(_vault, vault))
                return;

            ForceCompletePendingPublishForLifecycle();
            if (!ReleaseScavengingVaultHandles(_vault))
            {
                _vaultReady = false;
                return;
            }

            _vault = vault;
            InvalidateVaultHandles();
        }

        private void CacheSessionIdCold()
        {
            _worldSeedProvider = GlobalRegistry.WorldSeedProvider;
            _sessionId = BuildSessionId(_worldSeedProvider);
        }

        private void ReleaseVaultBinding()
        {
            ForceCompletePendingPublishForLifecycle();
            bool released = ReleaseScavengingVaultHandles(_vault);
            DisposeSimulationNativeScratch();
            if (released)
            {
                _vault = null;
                InvalidateVaultHandles();
                return;
            }

            _vaultReady = false;
        }

        private bool EnsureSimulationNativeScratchCold()
        {
            if (_nativeScratch.IsReady())
                return true;

            _nativeScratch.Ensure();
            return _nativeScratch.IsReady();
        }

        private void DisposeSimulationNativeScratch()
        {
            _nativeScratch.Dispose();
        }

        private void InvalidateVaultHandles()
        {
            _lootEntriesHandle = default;
            _biomeModifiersHandle = default;
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

        private bool ReleaseScavengingVaultHandles(IDataVault vault)
        {
            bool released = true;
            released &= ReleaseScavengingVaultHandle(vault, ref _lootEntriesHandle, ScavengingLootOracleConstants.LootEntriesBufferId);
            released &= ReleaseScavengingVaultHandle(vault, ref _biomeModifiersHandle, ScavengingLootOracleConstants.BiomeModifiersBufferId);
            released &= ReleaseScavengingVaultHandle(vault, ref _distributionAuditHandle, ScavengingLootOracleConstants.DistributionAuditBufferId);
            released &= ReleaseScavengingVaultHandle(vault, ref _csvScratchHandle, ScavengingLootOracleConstants.CsvScratchBufferId);
            return released;
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
            if (TryResolveScavengingVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || vault.IsAllocationLocked || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystem, options);
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
                !vault.IsCompactionFenceActive &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (vault.IsCompactionFenceActive)
            {
                buffer = default;
                return false;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsScavengingVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                vault.IsCompactionFenceActive ||
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
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault != null &&
                !vault.IsCompactionFenceActive &&
                requiredLength > 0 &&
                IsScavengingVaultHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out buffer) &&
                !vault.IsCompactionFenceActive &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            buffer = default;
            return false;
        }

        private static bool TryAcquireScavengingVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || vault.IsCompactionFenceActive)
                return false;

            if (!IsScavengingVaultHandle(in handle, bufferId))
            {
                if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                    !IsScavengingVaultHandle(in handle, bufferId))
                {
                    if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                    {
                        handle = default;
                        return false;
                    }

                    handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystem, options);
                }
            }

            if (!IsScavengingVaultHandle(in handle, bufferId) ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystem, out buffer))
            {
                buffer = default;
                return false;
            }

            if (!vault.IsCompactionFenceActive &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            vault.ReleaseWriteLock(in handle, OwnerSystem);
            buffer = default;
            return false;
        }

        private static bool IsScavengingVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private static bool ReleaseScavengingVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault == null || !IsScavengingVaultHandle(in handle, bufferId))
            {
                handle = default;
                return true;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            if (!vault.ReleaseBuffer(in handle) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated)
            {
                return false;
            }

            handle = default;
            return true;
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

        private sealed class SimulationPhaseSystem : IDispatcherSystem, IDispatcherFenceDomainProvider
        {
            private readonly ScavengingLootOracleRuntime _owner;

            public SimulationPhaseSystem(ScavengingLootOracleRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => SimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.Simulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public DispatcherFenceDomain GetFenceDomain() => DispatcherFenceDomain.Simulation;

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return _owner != null
                    ? _owner.ScheduleSimulation(in timing, in context, dependsOn)
                    : dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
            }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly ScavengingLootOracleRuntime _owner;

            public PostSimulationPhaseSystem(ScavengingLootOracleRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => PostSimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner?.PostSimulationTick(in timing);
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
            }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private readonly ScavengingLootOracleRuntime _owner;

            public VisualSyncPhaseSystem(ScavengingLootOracleRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => VisualSyncSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.VisualSync;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
                _owner?.VisualSyncTick(in timing);
            }
        }

        private ulong ResolveCachedSessionId()
        {
            if (_sessionId == 0UL)
                _sessionId = BuildSessionId(_worldSeedProvider);

            return _sessionId;
        }

        private static ulong BuildSessionId(IWorldSeedProvider provider)
        {
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

            if (!ScavengingLootOracleRuntime.TryRunDistributionSelfAudit(out NativeArray<uint>.ReadOnly counts) || !counts.IsCreated)
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
