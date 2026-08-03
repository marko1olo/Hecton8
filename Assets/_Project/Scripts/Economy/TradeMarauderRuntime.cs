using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.UI;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Economy
{
    public static class TradeMarauderConstants
    {
        public const int MaxMarauders = 20;
        public const int MaxInventorySlotsPerMarauder = 50;
        public const int MaxEconomyItems = 50;
        public const int LowTierEconomyItems = 10;
        public const int FactionCapacity = 16;
        public const int SectorGridSide = 101;
        public const int SectorGridHalf = SectorGridSide >> 1;
        public const int SectorNodeCapacity = SectorGridSide * SectorGridSide;
        public const int RouteNodeStride = 64;
        public const int TelemetryFrameCount = 300;
        public const int SignalScratchCapacity = 32;
        public const int LootNodeCapacity = 128;
        public const int CounterCapacity = 32;
        public const int VisualProxyCapacity = MaxMarauders;
        public const int CsvScratchBytes = 16 * 1024;
        public const float MacroSectorSizeMeters = 1000f;
        public const float InvMacroSectorSizeMeters = 1f / MacroSectorSizeMeters;
        public const float OffscreenRaidDistanceMeters = 5000f;
        public const float OffscreenRaidDistanceSq = OffscreenRaidDistanceMeters * OffscreenRaidDistanceMeters;
        public const float BaseDemandRadiusMeters = 2000f;
        public const float BaseDemandRadiusSq = BaseDemandRadiusMeters * BaseDemandRadiusMeters;
        public const float TacticalDistanceMeters = 500f;
        public const float TacticalDistanceSq = TacticalDistanceMeters * TacticalDistanceMeters;
        public const float VisualHydrationDistanceMeters = 1000f;
        public const float VisualHydrationDistanceSq = VisualHydrationDistanceMeters * VisualHydrationDistanceMeters;
        public const float SimulationTickDeltaSeconds = 5f;
        public const double AupQuantizeMeters = 0.001d;
        public const double InvAupQuantizeMeters = 1d / AupQuantizeMeters;
        public const float AStarIterationTelemetryMs = 0.000018f;
        public const string BaseRaidedMessage = "BASE RAIDED // MARAUDER LOOT LOST";
        public const uint TradeMarauderSourceHash = 0x53483633u; // SH63
    }

    public enum MarauderTaskKind : uint
    {
        Idle = 0,
        TradeRoute = 1,
        RaidBase = 2,
        HuntPlayer = 3,
        Salvage = 4,
        Intercept = 5
    }

    public enum MarauderCounterIndex : int
    {
        TransactionSignalCount = 0,
        AcousticSignalCount = 1,
        TelemetryCursor = 2,
        CachedEconomyValid = 3,
        FaultFlags = 4,
        LastSolvedPaths = 5,
        LastFailedPaths = 6,
        LastPathIterations = 7,
        TradeRequestActive = 8,
        SearchEpoch = 9,
        CsvRowsAccepted = 10,
        CsvRowsRejected = 11,
        VisualProxyCount = 12,
        AStarBudgetExhausted = 13
    }

    public static class MarauderSectorFlags
    {
        public const uint LeviathanTerritory = 1u << 0;
        public const uint PlayerBase = 1u << 1;
        public const uint RichTitanium = 1u << 2;
        public const uint LootRich = 1u << 3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MarauderStateDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public uint FactionHash;
        [FieldOffset(40)] public uint CurrentTask;
        [FieldOffset(44)] public float HullIntegrity;
        [FieldOffset(48)] public uint _pad0;
        [FieldOffset(52)] private uint _pad2;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MarauderInventorySlotDTO
    {
        [FieldOffset(0)] public uint ItemHash;
        [FieldOffset(4)] public int Quantity;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MarauderEconomyWeightDTO
    {
        [FieldOffset(0)] public uint ItemHash;
        [FieldOffset(4)] public float BasePrice;
        [FieldOffset(8)] public float Supply;
        [FieldOffset(12)] public float Demand;
        [FieldOffset(16)] public float Scarcity;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MarauderSectorEconomyDTO
    {
        [FieldOffset(0)] public double3 SectorCentroidAup;
        [FieldOffset(24)] public float Supply;
        [FieldOffset(28)] public float Demand;
        [FieldOffset(32)] public float Scarcity;
        [FieldOffset(36)] public float Threat;
        [FieldOffset(40)] public float AggressionBias;
        [FieldOffset(44)] public float LootValue;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint SectorHash;
        [FieldOffset(56)] public uint DominantItemHash;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MarauderRoutePlanDTO
    {
        [FieldOffset(0)] public double3 SourceAup;
        [FieldOffset(24)] public double3 TargetAup;
        [FieldOffset(48)] public float Priority;
        [FieldOffset(52)] public float Aggression;
        [FieldOffset(56)] public uint ItemHash;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MarauderRouteNodeDTO
    {
        [FieldOffset(0)] public double3 NodeAup;
        [FieldOffset(24)] public uint SectorIndex;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MarauderSectorHashEntryDTO
    {
        [FieldOffset(0)] public long SectorX;
        [FieldOffset(8)] public long SectorZ;
        [FieldOffset(16)] public uint SectorHash;
        [FieldOffset(20)] public int SectorIndex;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MarauderLootNodeDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public uint ItemHash;
        [FieldOffset(28)] public int Quantity;
        [FieldOffset(32)] public float Value;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint NodeHash;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MarauderTradeTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float BasePriceVolatility;
        [FieldOffset(8)] public float MarauderSpawnRate;
        [FieldOffset(12)] public float TheftProbability;
        [FieldOffset(16)] public float AggressionScale;
        [FieldOffset(20)] public float LeviathanAggressionThreshold;
        [FieldOffset(24)] public float RouteReplanSeconds;
        [FieldOffset(28)] public float CachedGlobalScarcityIndex;
        [FieldOffset(32)] public uint CopperHash;
        [FieldOffset(36)] public uint TitaniumHash;
        [FieldOffset(40)] public int ActiveMarauders;
        [FieldOffset(44)] public int ItemEvaluationLimit;
        [FieldOffset(48)] public int MaxRouteSolves;
        [FieldOffset(52)] public int MaxAStarIterations;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MarauderTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public int ActiveMarauders;
        [FieldOffset(8)] public int PathSolvedCount;
        [FieldOffset(12)] public int PathFailedCount;
        [FieldOffset(16)] public int PathIterations;
        [FieldOffset(20)] public int EconomyItemsEvaluated;
        [FieldOffset(24)] public float GlobalScarcityIndex;
        [FieldOffset(28)] public float PathfindingComputeTimeMs;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public uint StateHash;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint SearchEpoch;
        [FieldOffset(48)] public uint Reserved0;
        [FieldOffset(52)] private uint _pad0;
        [FieldOffset(56)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct MarauderNativeMinHeapNode
    {
        [FieldOffset(0)] public float Cost;
        [FieldOffset(4)] public int NodeIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MarauderPaddedCounterDTO
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(4)] private uint _pad0;
        [FieldOffset(8)] private ulong _pad1;
        [FieldOffset(16)] private ulong _pad2;
        [FieldOffset(24)] private ulong _pad3;
        [FieldOffset(32)] private ulong _pad4;
        [FieldOffset(40)] private ulong _pad5;
        [FieldOffset(48)] private ulong _pad6;
        [FieldOffset(56)] private ulong _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MarauderVisualProxyDTO
    {
        [FieldOffset(0)] public float4 Row0;
        [FieldOffset(16)] public float4 Row1;
        [FieldOffset(32)] public float4 Row2;
        [FieldOffset(48)] public float4 Row3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MarauderAcousticSignatureDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float RadiusMeters;
        [FieldOffset(28)] public float Intensity01;
        [FieldOffset(32)] public uint SourceId;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public uint Channel;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong Reserved0;
        [FieldOffset(56)] public ulong Reserved1;
    }

    public static class MarauderCounterUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(NativeArray<MarauderPaddedCounterDTO> counters, MarauderCounterIndex index)
        {
            int i = (int)index;
            return counters.IsCreated && (uint)i < (uint)counters.Length ? counters[i].Value : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(NativeArray<MarauderPaddedCounterDTO> counters, MarauderCounterIndex index, int value)
        {
            int i = (int)index;
            if (!counters.IsCreated || (uint)i >= (uint)counters.Length)
                return;

            MarauderPaddedCounterDTO counter = counters[i];
            counter.Value = value;
            counters[i] = counter;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockInventoryTransactionSignal : ISignal
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public uint ItemHash;
        [FieldOffset(28)] public int DeltaBaseQuantity;
        [FieldOffset(32)] public int DeltaMarauderQuantity;
        [FieldOffset(36)] public int MarauderIndex;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public byte Reason;
        [FieldOffset(45)] public byte Flags;
        [FieldOffset(46)] public ushort Reserved0;
        [FieldOffset(48)] public uint Reserved1;
        [FieldOffset(52)] public uint ReservedPadding;
        [FieldOffset(56)] public ulong Reserved2;
    }

    public ref partial struct MockPlayerInventory
    {
        public NativeArray<uint> ItemHashes;
        public NativeArray<int> Quantities;
        public uint HoardedItemHash;
        public int HoardedQuantity;
    }

    public static class MarauderEconomyHash
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static uint HashLowerAscii(ReadOnlySpan<byte> text)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < text.Length; i++)
            {
                byte value = text[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);

                hash ^= value;
                hash *= FnvPrime;
            }

            return hash;
        }

        public static uint HashLowerAscii(ReadOnlySpan<char> text)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                uint value = c >= 'A' && c <= 'Z' ? (uint)(c + 32) : c;
                hash ^= value & 0xFFu;
                hash *= FnvPrime;
            }

            return hash;
        }
    }

#if UNITY_EDITOR
    public static class MarauderEconomyCsvParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static bool TryParse(
            ReadOnlySpan<byte> csv,
            NativeArray<MarauderEconomyWeightDTO> weights,
            out int rowCount,
            out int rejectedCount)
        {
            rowCount = 0;
            rejectedCount = 0;
            if (!weights.IsCreated || weights.Length == 0)
                return false;

            int cursor = 0;
            while (rowCount < weights.Length && TryReadLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (!TryParseRow(line, out MarauderEconomyWeightDTO row))
                {
                    rejectedCount++;
                    continue;
                }

                weights[rowCount++] = row;
            }

            return rowCount > 0;
        }

        public static uint HashLowerAscii(ReadOnlySpan<byte> text)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < text.Length; i++)
            {
                byte value = text[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);

                hash ^= value;
                hash *= FnvPrime;
            }

            return hash;
        }

        public static uint HashLowerAscii(ReadOnlySpan<char> text)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                uint value = c >= 'A' && c <= 'Z' ? (uint)(c + 32) : c;
                hash ^= value & 0xFFu;
                hash *= FnvPrime;
            }

            return hash;
        }

        private static bool TryParseRow(ReadOnlySpan<byte> line, out MarauderEconomyWeightDTO row)
        {
            row = default;
            if (!TryReadToken(line, 0, out ReadOnlySpan<byte> item, out int cursor))
                return false;

            if (IsHeaderToken(item))
                return false;

            if (!TryReadToken(line, cursor, out ReadOnlySpan<byte> basePriceSpan, out cursor) ||
                !TryReadToken(line, cursor, out ReadOnlySpan<byte> supplySpan, out cursor) ||
                !TryReadToken(line, cursor, out ReadOnlySpan<byte> demandSpan, out cursor) ||
                !TryReadToken(line, cursor, out ReadOnlySpan<byte> scarcitySpan, out _))
            {
                return false;
            }

            if (!TryParseFloat(basePriceSpan, out float basePrice) ||
                !TryParseFloat(supplySpan, out float supply) ||
                !TryParseFloat(demandSpan, out float demand) ||
                !TryParseFloat(scarcitySpan, out float scarcity))
            {
                return false;
            }

            row = new MarauderEconomyWeightDTO
            {
                ItemHash = HashLowerAscii(item),
                BasePrice = math.max(0.01f, basePrice),
                Supply = math.saturate(supply),
                Demand = math.saturate(demand),
                Scarcity = math.saturate(scarcity),
                Flags = 0u
            };
            return true;
        }

        private static bool TryReadToken(ReadOnlySpan<byte> line, int start, out ReadOnlySpan<byte> token, out int next)
        {
            int cursor = start;
            while (cursor < line.Length && (line[cursor] == (byte)',' || line[cursor] == (byte)';'))
                cursor++;

            int tokenStart = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',' && line[cursor] != (byte)';')
                cursor++;

            token = Trim(line.Slice(tokenStart, cursor - tokenStart));
            next = cursor < line.Length ? cursor + 1 : cursor;
            return token.Length > 0;
        }

        private static bool TryReadLine(ReadOnlySpan<byte> text, ref int cursor, out ReadOnlySpan<byte> line)
        {
            if (cursor >= text.Length)
            {
                line = ReadOnlySpan<byte>.Empty;
                return false;
            }

            int start = cursor;
            while (cursor < text.Length && text[cursor] != (byte)'\n' && text[cursor] != (byte)'\r')
                cursor++;

            line = text.Slice(start, cursor - start);
            while (cursor < text.Length && (text[cursor] == (byte)'\n' || text[cursor] == (byte)'\r'))
                cursor++;

            return true;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> text)
        {
            int start = 0;
            int end = text.Length - 1;
            while (start <= end && IsWhitespace(text[start]))
                start++;
            while (end >= start && IsWhitespace(text[end]))
                end--;

            return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> text, out float value)
        {
            value = 0f;
            if (text.Length == 0)
                return false;

            int cursor = 0;
            float sign = 1f;
            if (text[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }

            float integer = 0f;
            bool any = false;
            while (cursor < text.Length && text[cursor] >= (byte)'0' && text[cursor] <= (byte)'9')
            {
                integer = (integer * 10f) + (text[cursor] - (byte)'0');
                cursor++;
                any = true;
            }

            float fraction = 0f;
            if (cursor < text.Length && text[cursor] == (byte)'.')
            {
                cursor++;
                float scale = 0.1f;
                while (cursor < text.Length && text[cursor] >= (byte)'0' && text[cursor] <= (byte)'9')
                {
                    fraction += (text[cursor] - (byte)'0') * scale;
                    scale *= 0.1f;
                    cursor++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = (integer + fraction) * sign;
            return math.isfinite(value);
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool IsHeaderToken(ReadOnlySpan<byte> item)
        {
            return item.Length >= 4 &&
                   ToLower(item[0]) == (byte)'i' &&
                   ToLower(item[1]) == (byte)'t' &&
                   ToLower(item[2]) == (byte)'e' &&
                   ToLower(item[3]) == (byte)'m';
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }
    }
#endif

    internal ref struct MarauderNativeMinHeap
    {
        public NativeArray<MarauderNativeMinHeapNode> Nodes;
        public int Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Count = 0;
        }

        public bool TryPush(int nodeIndex, float cost)
        {
            if (!Nodes.IsCreated || Count >= Nodes.Length)
                return false;

            int cursor = Count++;
            Nodes[cursor] = new MarauderNativeMinHeapNode { NodeIndex = nodeIndex, Cost = cost };
            while (cursor > 0)
            {
                int parent = (cursor - 1) >> 1;
                if (Nodes[parent].Cost <= Nodes[cursor].Cost)
                    break;

                Swap(parent, cursor);
                cursor = parent;
            }

            return true;
        }

        public bool TryPop(out int nodeIndex)
        {
            nodeIndex = -1;
            if (Count <= 0 || !Nodes.IsCreated)
                return false;

            MarauderNativeMinHeapNode root = Nodes[0];
            Count--;
            if (Count > 0)
                Nodes[0] = Nodes[Count];

            int cursor = 0;
            while (true)
            {
                int left = (cursor << 1) + 1;
                int right = left + 1;
                if (left >= Count)
                    break;

                int best = right < Count && Nodes[right].Cost < Nodes[left].Cost ? right : left;
                if (Nodes[cursor].Cost <= Nodes[best].Cost)
                    break;

                Swap(cursor, best);
                cursor = best;
            }

            nodeIndex = root.NodeIndex;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Swap(int a, int b)
        {
            MarauderNativeMinHeapNode tmp = Nodes[a];
            Nodes[a] = Nodes[b];
            Nodes[b] = tmp;
        }
    }

#if UNITY_EDITOR
    [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
#endif

    public struct MarauderScarcityMockInventoryJob : IJob
    {
        [ReadOnly] public NativeArray<uint> InventoryItemHashes;
        [ReadOnly] public NativeArray<int> InventoryQuantities;
        public NativeArray<MarauderEconomyWeightDTO> EconomyWeights;
        public uint HoardedItemHash;
        public int HoardedQuantity;

        public void Execute()
        {
            if (!EconomyWeights.IsCreated || !InventoryItemHashes.IsCreated || !InventoryQuantities.IsCreated)
                return;

            int hoarded = math.max(0, HoardedQuantity);
            int limit = math.min(InventoryItemHashes.Length, InventoryQuantities.Length);
            for (int i = 0; i < limit; i++)
            {
                if (InventoryItemHashes[i] == HoardedItemHash)
                    hoarded += math.max(0, InventoryQuantities[i]);
            }

            float scarcityBoost = math.saturate(hoarded * (1f / 500f));
            for (int i = 0; i < EconomyWeights.Length; i++)
            {
                MarauderEconomyWeightDTO weight = EconomyWeights[i];
                if (weight.ItemHash != HoardedItemHash)
                    continue;

                weight.Demand = math.saturate(math.max(weight.Demand, 0.25f) + scarcityBoost);
                weight.Scarcity = math.saturate(math.max(weight.Scarcity, 0.15f) + scarcityBoost);
                weight.BasePrice = math.max(0.01f, weight.BasePrice) * (1f + scarcityBoost * 2f);
                EconomyWeights[i] = weight;
                return;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MarauderSupplyChainSolverJob : IJob
    {
        [NoAlias] public NativeArray<MarauderStateDTO> States;
        [NoAlias] public NativeArray<MarauderEconomyWeightDTO> EconomyWeights;
        [NoAlias] public NativeArray<MarauderSectorEconomyDTO> SectorEconomy;
        [NoAlias] public NativeArray<MarauderRoutePlanDTO> RoutePlans;
        [ReadOnly, NoAlias] public NativeArray<MarauderLootNodeDTO> LootNodes;
        [NoAlias] public NativeArray<float> FactionStanding;
        [NoAlias] public NativeArray<MarauderTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<MarauderTradeTuningDTO> TuningBuffer;
        [NoAlias] public NativeArray<MarauderPaddedCounterDTO> Counters;
        public MarauderTradeTuningDTO Tuning;
        public double3 PlayerAup;
        public double3 BaseAup;
        public int FrameIndex;

        public void Execute()
        {
            if (!States.IsCreated || !EconomyWeights.IsCreated || !SectorEconomy.IsCreated || !RoutePlans.IsCreated)
                return;

            float quality = math.saturate(Tuning.GlobalQualityWeight);
            float cacheReuseWeight = math.saturate((0.4f - quality) * 3.3333333f);
            float cacheRoll = ResolveNoise01((uint)FrameIndex ^ 0xCACEFEEDu);
            if (cacheReuseWeight > 0f &&
                cacheRoll < cacheReuseWeight &&
                MarauderCounterUtility.Read(Counters, MarauderCounterIndex.CachedEconomyValid) != 0)
            {
                WriteTelemetry(0, 0, 0, 0, Tuning.CachedGlobalScarcityIndex, quality, 0u);
                return;
            }

            int itemCapacity = math.min(TradeMarauderConstants.MaxEconomyItems, EconomyWeights.Length);
            if (itemCapacity <= 0)
            {
                WriteTelemetry(0, 0, 0, 0, Tuning.CachedGlobalScarcityIndex, quality, 1u);
                return;
            }

            int sectorCapacity = SectorEconomy.Length;
            if (sectorCapacity <= 0)
            {
                WriteTelemetry(0, 0, 0, 0, Tuning.CachedGlobalScarcityIndex, quality, 2u);
                return;
            }

            int configuredItemLimit = Tuning.ItemEvaluationLimit > 0 ? Tuning.ItemEvaluationLimit : itemCapacity;
            configuredItemLimit = math.clamp(configuredItemLimit, 1, itemCapacity);
            int qualityItemLimit = math.clamp(
                (int)math.round(math.lerp(TradeMarauderConstants.LowTierEconomyItems, TradeMarauderConstants.MaxEconomyItems, quality)),
                1,
                itemCapacity);
            int itemLimit = math.min(qualityItemLimit, configuredItemLimit);

            int sectorLimit = math.clamp(
                (int)math.round(math.lerp(256f, sectorCapacity, quality)),
                math.min(128, sectorCapacity),
                sectorCapacity);

            int bestScarcityIndex = 0;
            float bestScarcity = -1f;
            float globalScarcity = 0f;
            for (int i = 0; i < itemLimit; i++)
            {
                MarauderEconomyWeightDTO weight = EconomyWeights[i];
                float scarcity = math.saturate(weight.Demand - weight.Supply + weight.Scarcity);
                weight.Scarcity = scarcity;
                EconomyWeights[i] = weight;
                globalScarcity += scarcity;
                if (scarcity > bestScarcity)
                {
                    bestScarcity = scarcity;
                    bestScarcityIndex = i;
                }
            }

            globalScarcity = itemLimit > 0 ? globalScarcity / itemLimit : 0f;
            MarauderEconomyWeightDTO targetItem = EconomyWeights[bestScarcityIndex];
            int bestSupplySector = 0;
            int bestDemandSector = 0;
            float bestSupplyScore = -1f;
            float bestDemandScore = -1f;

            int forcedBaseSectorIndex = ResolveSectorIndex(BaseAup);
            for (int sample = 0; sample < sectorLimit; sample++)
            {
                int i = ResolveSampledSectorIndex(sample, sectorLimit, sectorCapacity, forcedBaseSectorIndex);
                MarauderSectorEconomyDTO sector = SectorEconomy[i];
                float hashNoise = ResolveNoise01(sector.SectorHash + (uint)FrameIndex);
                float titaniumBias = (sector.Flags & MarauderSectorFlags.RichTitanium) != 0u && targetItem.ItemHash == Tuning.TitaniumHash ? 0.4f : 0f;
                float flaggedBaseBias = (sector.Flags & MarauderSectorFlags.PlayerBase) != 0u ? 0.6f : 0f;
                float baseBias = math.max(flaggedBaseBias, ResolveBaseDemandBias(sector.SectorCentroidAup, BaseAup));

                sector.Supply = math.saturate(sector.Supply * 0.92f + hashNoise * 0.08f + titaniumBias);
                sector.Demand = math.saturate(sector.Demand * 0.9f + globalScarcity * 0.35f + baseBias);
                sector.Scarcity = math.saturate((sector.Demand - sector.Supply) + targetItem.Scarcity);
                sector.DominantItemHash = targetItem.ItemHash;
                SectorEconomy[i] = sector;

                float supplyScore = sector.Supply - sector.Threat * 0.15f;
                float demandScore = sector.Demand + sector.Scarcity + baseBias;
                if (supplyScore > bestSupplyScore)
                {
                    bestSupplyScore = supplyScore;
                    bestSupplySector = i;
                }

                if (demandScore > bestDemandScore)
                {
                    bestDemandScore = demandScore;
                    bestDemandSector = i;
                }
            }

            double3 supplyAup = SectorEconomy[bestSupplySector].SectorCentroidAup;
            double3 demandAup = SectorEconomy[bestDemandSector].SectorCentroidAup;
            int activeMarauders = math.min(math.min(States.Length, RoutePlans.Length), math.max(0, Tuning.ActiveMarauders));
            for (int i = 0; i < activeMarauders; i++)
            {
                MarauderStateDTO state = States[i];
                uint task = ResolveFactionStanding(i, state.FactionHash) < -0.5f
                    ? (uint)MarauderTaskKind.HuntPlayer
                    : ResolveTask(globalScarcity, i);

                state.CurrentTask = task;
                if (!IsFinite(state.AUP))
                    state.AUP = supplyAup;

                double3 target = task == (uint)MarauderTaskKind.HuntPlayer ? PlayerAup : demandAup;
                if (task == (uint)MarauderTaskKind.Salvage)
                    target = ResolveNearestLootProxy(i, demandAup);

                RoutePlans[i] = new MarauderRoutePlanDTO
                {
                    SourceAup = state.AUP,
                    TargetAup = target,
                    Priority = math.saturate(globalScarcity + (i * 0.013f)),
                    Aggression = math.saturate(Tuning.AggressionScale * (0.35f + ResolveNoise01((uint)i * 2654435761u))),
                    ItemHash = targetItem.ItemHash,
                    Flags = task
                };

                States[i] = state;
            }

            MarauderCounterUtility.Write(Counters, MarauderCounterIndex.CachedEconomyValid, 1);

            if (TuningBuffer.IsCreated && TuningBuffer.Length > 0)
            {
                MarauderTradeTuningDTO written = Tuning;
                written.CachedGlobalScarcityIndex = globalScarcity;
                TuningBuffer[0] = written;
            }

            WriteTelemetry(activeMarauders, 0, 0, itemLimit, globalScarcity, quality, 0u);
        }

        private float ResolveFactionStanding(int marauderIndex, uint factionHash)
        {
            if (!FactionStanding.IsCreated || FactionStanding.Length == 0)
                return 0f;

            int index = (int)((factionHash + (uint)marauderIndex) % (uint)FactionStanding.Length);
            return FactionStanding[index];
        }

        private static uint ResolveTask(float scarcity, int index)
        {
            if (scarcity > 0.62f)
                return (uint)MarauderTaskKind.RaidBase;

            return (index & 3) == 0 ? (uint)MarauderTaskKind.Salvage : (uint)MarauderTaskKind.TradeRoute;
        }

        private double3 ResolveNearestLootProxy(int index, double3 fallback)
        {
            if (LootNodes.IsCreated && LootNodes.Length > 0)
            {
                float bestValue = 0f;
                double3 best = fallback;
                for (int i = 0; i < LootNodes.Length; i++)
                {
                    MarauderLootNodeDTO loot = LootNodes[i];
                    if (loot.ItemHash == 0u || loot.Quantity <= 0 || loot.Value <= bestValue)
                        continue;

                    bestValue = loot.Value;
                    best = loot.AUP;
                }

                if (bestValue > 0f)
                    return best;
            }

            double angle = (index + 1) * 1.61803398875;
            MathLodApproximation.ApproxSinCosBhaskara((float)angle, out float sin, out float cos);
            return fallback + new double3(cos, 0d, sin) * 2000d;
        }

        private void WriteTelemetry(int active, int solved, int failed, int items, float scarcity, float quality, uint flags)
        {
            if (!Telemetry.IsCreated || Telemetry.Length == 0)
                return;

            int cursor = 0;
            if (Counters.IsCreated)
            {
                cursor = MarauderCounterUtility.Read(Counters, MarauderCounterIndex.TelemetryCursor);
                MarauderCounterUtility.Write(Counters, MarauderCounterIndex.TelemetryCursor, (cursor + 1) % Telemetry.Length);
            }

            Telemetry[math.clamp(cursor, 0, Telemetry.Length - 1)] = new MarauderTelemetryEntry
            {
                Frame = (uint)FrameIndex,
                ActiveMarauders = active,
                PathSolvedCount = solved,
                PathFailedCount = failed,
                PathIterations = 0,
                EconomyItemsEvaluated = items,
                GlobalScarcityIndex = scarcity,
                PathfindingComputeTimeMs = 0f,
                GlobalQualityWeight = quality,
                StateHash = HashState(active, scarcity, quality),
                Flags = flags,
                SearchEpoch = 0u
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashState(int active, float scarcity, float quality)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)active) * 16777619u;
            hash = (hash ^ math.asuint(scarcity)) * 16777619u;
            hash = (hash ^ math.asuint(quality)) * 16777619u;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveNoise01(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveBaseDemandBias(double3 sectorAup, double3 baseAup)
        {
            double3 delta = sectorAup - baseAup;
            float3 local = (float3)delta;
            float distanceSq = math.lengthsq(local);
            if (!math.isfinite(distanceSq))
                return 0f;

            float radiusSq = math.max(TradeMarauderConstants.BaseDemandRadiusSq, 0.0001f);
            float inside01 = math.saturate(1f - distanceSq / radiusSq);
            float smooth = inside01 * inside01 * (3f - 2f * inside01);
            return smooth * math.step(0.0001f, inside01) * 0.6f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveSampledSectorIndex(int sample, int sampleCount, int sectorCapacity, int forcedBaseSectorIndex)
        {
            if (sampleCount >= sectorCapacity)
                return math.clamp(sample, 0, sectorCapacity - 1);

            if (sample == 0)
                return math.clamp(forcedBaseSectorIndex, 0, sectorCapacity - 1);

            float t = sample / math.max(1f, sampleCount - 1f);
            int index = (int)math.round(t * (sectorCapacity - 1));
            return math.clamp(index, 0, sectorCapacity - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveSectorIndex(double3 aup)
        {
            if (!IsFinite(aup))
                return TradeMarauderConstants.SectorGridHalf + TradeMarauderConstants.SectorGridHalf * TradeMarauderConstants.SectorGridSide;

            int x = TradeMarauderConstants.SectorGridHalf + (int)math.round(aup.x * TradeMarauderConstants.InvMacroSectorSizeMeters);
            int z = TradeMarauderConstants.SectorGridHalf + (int)math.round(aup.z * TradeMarauderConstants.InvMacroSectorSizeMeters);
            x = math.clamp(x, 0, TradeMarauderConstants.SectorGridSide - 1);
            z = math.clamp(z, 0, TradeMarauderConstants.SectorGridSide - 1);
            return x + z * TradeMarauderConstants.SectorGridSide;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MarauderMacroAStarJob : IJob
    {
        private const float HugeCost = 3.402823e+38f;
        private const float LeviathanAvoidCost = 1000000f;

        [NoAlias] public NativeArray<MarauderStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<MarauderRoutePlanDTO> RoutePlans;
        [ReadOnly, NoAlias] public NativeArray<MarauderSectorEconomyDTO> SectorEconomy;
        [NoAlias] public NativeArray<MarauderNativeMinHeapNode> OpenHeap;
        [NoAlias] public NativeArray<float> GCosts;
        [NoAlias] public NativeArray<int> CameFrom;
        [NoAlias] public NativeArray<int> NodeStates;
        [NoAlias] public NativeArray<MarauderRouteNodeDTO> RouteNodes;
        [NoAlias] public NativeArray<byte> RouteCounts;
        [NoAlias] public NativeArray<MarauderTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<MarauderPaddedCounterDTO> Counters;
        public MarauderTradeTuningDTO Tuning;
        public int FrameIndex;
        public int SearchEpoch;
        public int StartMarauder;
        public int MaxSolves;

        public void Execute()
        {
            if (!States.IsCreated || !RoutePlans.IsCreated || !OpenHeap.IsCreated ||
                !GCosts.IsCreated || !CameFrom.IsCreated || !NodeStates.IsCreated ||
                !RouteNodes.IsCreated || !RouteCounts.IsCreated)
            {
                return;
            }

            int active = math.min(math.min(States.Length, RoutePlans.Length), RouteCounts.Length);
            active = math.min(active, math.max(0, Tuning.ActiveMarauders));
            int solveBudget = math.clamp(MaxSolves, 1, math.max(1, active));
            int solved = 0;
            int failed = 0;
            int iterations = 0;
            int budgetExhausted = 0;
            int epoch = math.max(1, SearchEpoch);
            int tickIterationLimit = math.clamp(Tuning.MaxAStarIterations, 1, TradeMarauderConstants.SectorNodeCapacity);

            for (int attempt = 0; attempt < active && solved + failed < solveBudget && iterations < tickIterationLimit; attempt++)
            {
                int marauderIndex = (StartMarauder + attempt) % active;
                MarauderRoutePlanDTO plan = RoutePlans[marauderIndex];
                if (plan.Flags == (uint)MarauderTaskKind.Idle || !IsFinite(plan.TargetAup))
                {
                    ClearRoute(marauderIndex);
                    continue;
                }

                int remainingIterationBudget = tickIterationLimit - iterations;
                if (remainingIterationBudget <= 0)
                    break;

                int localIterations = 0;
                bool hitBudget;
                bool success = SolvePath(marauderIndex, in plan, epoch + attempt, remainingIterationBudget, ref localIterations, out hitBudget);
                iterations += localIterations;
                if (hitBudget)
                    budgetExhausted++;

                if (success)
                    solved++;
                else
                    failed++;
            }

            if (Counters.IsCreated)
            {
                SetCounter(MarauderCounterIndex.LastSolvedPaths, solved);
                SetCounter(MarauderCounterIndex.LastFailedPaths, failed);
                SetCounter(MarauderCounterIndex.LastPathIterations, iterations);
                SetCounter(MarauderCounterIndex.AStarBudgetExhausted, budgetExhausted);
            }

            uint flags = budgetExhausted > 0 ? 2u : 0u;
            WriteTelemetry(active, solved, failed, iterations, flags);
        }

        private bool SolvePath(
            int marauderIndex,
            in MarauderRoutePlanDTO plan,
            int searchId,
            int iterationBudget,
            ref int iterationAccumulator,
            out bool budgetExhausted)
        {
            budgetExhausted = false;
            double3 origin = plan.SourceAup;
            double3 target = plan.TargetAup;
            int2 start = new int2(TradeMarauderConstants.SectorGridHalf, TradeMarauderConstants.SectorGridHalf);
            int2 goal = ResolveGoalCoord(target - origin);
            int2 originGlobalCoord = ResolveGlobalSectorCoord(origin);
            if (originGlobalCoord.x < 0 || originGlobalCoord.y < 0)
            {
                ClearRoute(marauderIndex);
                return false;
            }

            int startNode = PackNode(start);
            int goalNode = PackNode(goal);
            if ((uint)startNode >= (uint)GCosts.Length || (uint)goalNode >= (uint)GCosts.Length)
            {
                ClearRoute(marauderIndex);
                return false;
            }

            MarauderNativeMinHeap heap = new MarauderNativeMinHeap { Nodes = OpenHeap, Count = 0 };
            NodeStates[startNode] = searchId;
            GCosts[startNode] = 0f;
            CameFrom[startNode] = -1;
            float heuristicWeight = ResolveHeuristicWeight(Tuning.GlobalQualityWeight, plan.Priority);
            heap.TryPush(startNode, ResolveHeuristic(start, goal) * heuristicWeight);

            int bestNode = startNode;
            float bestHeuristic = ResolveHeuristic(start, goal);
            int localIterations = 0;
            int iterationLimit = math.clamp(iterationBudget, 1, TradeMarauderConstants.SectorNodeCapacity);
            bool completed = false;

            while (heap.TryPop(out int current) && localIterations < iterationLimit)
            {
                localIterations++;
                if (NodeStates[current] == -searchId)
                    continue;

                NodeStates[current] = -searchId;
                int2 currentCoord = UnpackNode(current);
                float heuristic = ResolveHeuristic(currentCoord, goal);
                if (heuristic < bestHeuristic)
                {
                    bestHeuristic = heuristic;
                    bestNode = current;
                }

                if (current == goalNode)
                {
                    bestNode = current;
                    completed = true;
                    break;
                }

                for (int direction = 0; direction < 4; direction++)
                    TryVisitNeighbor(current, currentCoord, goal, originGlobalCoord, in plan, searchId, direction, heuristicWeight, ref heap);
            }

            iterationAccumulator += localIterations;
            budgetExhausted = !completed && localIterations >= iterationLimit && heap.Count > 0;

            int routeNode = completed ? goalNode : bestNode;
            WriteRoute(marauderIndex, routeNode, origin, originGlobalCoord);

            MarauderStateDTO state = States[marauderIndex];
            if (RouteCounts[marauderIndex] > 0)
            {
                int routeOffset = marauderIndex * TradeMarauderConstants.RouteNodeStride;
                int nextSlot = RouteCounts[marauderIndex] > 1 ? 1 : 0;
                double3 next = RouteNodes[routeOffset + nextSlot].NodeAup;
                double3 delta = next - state.AUP;
                float3 localDelta = (float3)delta;
                if (math.all(math.isfinite(localDelta)))
                {
                    state.Velocity = SafeNormalize(localDelta, state.Velocity) * math.lerp(18f, 55f, math.saturate(Tuning.GlobalQualityWeight));
                    double3 nextAup = QuantizeAupMillimeters(state.AUP + (double3)state.Velocity * TradeMarauderConstants.SimulationTickDeltaSeconds);
                    if (IsFinite(nextAup))
                    {
                        state.AUP = nextAup;
                        States[marauderIndex] = state;
                    }
                    else
                    {
                        SetCounter(MarauderCounterIndex.FaultFlags, 1);
                    }
                }
            }

            return completed;
        }

        private void TryVisitNeighbor(
            int current,
            int2 currentCoord,
            int2 goal,
            int2 originGlobalCoord,
            in MarauderRoutePlanDTO plan,
            int searchId,
            int direction,
            float heuristicWeight,
            ref MarauderNativeMinHeap heap)
        {
            int2 nextCoord = currentCoord + ResolveDirection(direction);
            if (nextCoord.x < 0 || nextCoord.y < 0 ||
                nextCoord.x >= TradeMarauderConstants.SectorGridSide ||
                nextCoord.y >= TradeMarauderConstants.SectorGridSide)
            {
                return;
            }

            int next = PackNode(nextCoord);
            if ((uint)next >= (uint)NodeStates.Length || NodeStates[next] == -searchId)
                return;

            float threatCost = ResolveThreatCost(nextCoord, originGlobalCoord, in plan);
            if (threatCost >= LeviathanAvoidCost)
                return;

            float currentG = NodeStates[current] == searchId || NodeStates[current] == -searchId ? GCosts[current] : HugeCost;
            float tentative = currentG + TradeMarauderConstants.MacroSectorSizeMeters + threatCost;
            float previous = NodeStates[next] == searchId || NodeStates[next] == -searchId ? GCosts[next] : HugeCost;
            if (tentative >= previous)
                return;

            CameFrom[next] = current;
            GCosts[next] = tentative;
            NodeStates[next] = searchId;
            heap.TryPush(next, tentative + (ResolveHeuristic(nextCoord, goal) * heuristicWeight));
        }

        private float ResolveThreatCost(int2 nodeCoord, int2 originGlobalCoord, in MarauderRoutePlanDTO plan)
        {
            int nodeIndex = ResolveGlobalSectorIndex(originGlobalCoord, nodeCoord);
            if (!SectorEconomy.IsCreated || nodeIndex < 0 || nodeIndex >= SectorEconomy.Length)
                return LeviathanAvoidCost;

            MarauderSectorEconomyDTO sector = SectorEconomy[nodeIndex];
            if ((sector.Flags & MarauderSectorFlags.LeviathanTerritory) != 0u &&
                plan.Aggression < Tuning.LeviathanAggressionThreshold)
            {
                return LeviathanAvoidCost;
            }

            return math.max(0f, sector.Threat) * math.lerp(450f, 80f, math.saturate(plan.Aggression));
        }

        private void WriteRoute(int marauderIndex, int endNode, double3 origin, int2 originGlobalCoord)
        {
            int offset = marauderIndex * TradeMarauderConstants.RouteNodeStride;
            if (offset < 0 || offset >= RouteNodes.Length || marauderIndex < 0 || marauderIndex >= RouteCounts.Length)
                return;

            int chainLength = CountRouteChain(endNode);
            int stored = math.min(chainLength, TradeMarauderConstants.RouteNodeStride);
            int firstReverseIndexToStore = math.max(0, chainLength - stored);
            int current = endNode;
            int reverseIndex = 0;
            int guard = 0;
            while (current >= 0 &&
                   current < CameFrom.Length &&
                   guard++ < TradeMarauderConstants.SectorNodeCapacity)
            {
                if (reverseIndex >= firstReverseIndexToStore)
                {
                    int slot = chainLength - 1 - reverseIndex;
                    if ((uint)slot < (uint)stored && offset + slot < RouteNodes.Length)
                    {
                        int2 coord = UnpackNode(current);
                        double3 nodeAup = ResolveAupFromCoord(origin, coord);
                        int globalSectorIndex = ResolveGlobalSectorIndex(originGlobalCoord, coord);
                        RouteNodes[offset + slot] = new MarauderRouteNodeDTO
                        {
                            NodeAup = nodeAup,
                            SectorIndex = globalSectorIndex >= 0 ? (uint)globalSectorIndex : uint.MaxValue,
                            Flags = globalSectorIndex >= 0 ? 0u : 1u
                        };
                    }
                }

                if (CameFrom[current] < 0)
                    break;

                current = CameFrom[current];
                reverseIndex++;
            }

            RouteCounts[marauderIndex] = (byte)stored;
        }

        private void ClearRoute(int marauderIndex)
        {
            if (RouteCounts.IsCreated && (uint)marauderIndex < (uint)RouteCounts.Length)
                RouteCounts[marauderIndex] = 0;
        }

        private int CountRouteChain(int endNode)
        {
            int current = endNode;
            int count = 0;
            int guard = 0;
            while (current >= 0 &&
                   current < CameFrom.Length &&
                   guard++ < TradeMarauderConstants.SectorNodeCapacity)
            {
                count++;
                if (CameFrom[current] < 0)
                    break;

                current = CameFrom[current];
            }

            if (guard >= TradeMarauderConstants.SectorNodeCapacity)
                SetCounter(MarauderCounterIndex.FaultFlags, 1);

            return math.max(0, count);
        }

        private void WriteTelemetry(int active, int solved, int failed, int iterations, uint flags)
        {
            if (!Telemetry.IsCreated || Telemetry.Length == 0)
                return;

            int cursor = 0;
            if (Counters.IsCreated)
            {
                cursor = MarauderCounterUtility.Read(Counters, MarauderCounterIndex.TelemetryCursor);
                MarauderCounterUtility.Write(Counters, MarauderCounterIndex.TelemetryCursor, (cursor + 1) % Telemetry.Length);
            }

            Telemetry[math.clamp(cursor, 0, Telemetry.Length - 1)] = new MarauderTelemetryEntry
            {
                Frame = (uint)FrameIndex,
                ActiveMarauders = active,
                PathSolvedCount = solved,
                PathFailedCount = failed,
                PathIterations = iterations,
                EconomyItemsEvaluated = Tuning.ItemEvaluationLimit,
                GlobalScarcityIndex = Tuning.CachedGlobalScarcityIndex,
                PathfindingComputeTimeMs = EstimatePathfindingMs(iterations),
                GlobalQualityWeight = Tuning.GlobalQualityWeight,
                StateHash = (uint)(solved * 73856093) ^ (uint)(failed * 19349663) ^ (uint)iterations,
                Flags = flags,
                SearchEpoch = (uint)SearchEpoch
            };
        }

        private void SetCounter(MarauderCounterIndex index, int value)
        {
            int i = (int)index;
            MarauderCounterUtility.Write(Counters, index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EstimatePathfindingMs(int iterations)
        {
            return math.min(0.25f, math.max(0, iterations) * TradeMarauderConstants.AStarIterationTelemetryMs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int2 ResolveGoalCoord(double3 delta)
        {
            int x = TradeMarauderConstants.SectorGridHalf + (int)math.round(delta.x * TradeMarauderConstants.InvMacroSectorSizeMeters);
            int z = TradeMarauderConstants.SectorGridHalf + (int)math.round(delta.z * TradeMarauderConstants.InvMacroSectorSizeMeters);
            return math.clamp(new int2(x, z), new int2(0), new int2(TradeMarauderConstants.SectorGridSide - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ResolveAupFromCoord(double3 origin, int2 coord)
        {
            double x = (coord.x - TradeMarauderConstants.SectorGridHalf) * (double)TradeMarauderConstants.MacroSectorSizeMeters;
            double z = (coord.y - TradeMarauderConstants.SectorGridHalf) * (double)TradeMarauderConstants.MacroSectorSizeMeters;
            return origin + new double3(x, 0d, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveGlobalSectorIndex(double3 nodeAup)
        {
            int2 coord = ResolveGlobalSectorCoord(nodeAup);
            if (coord.x < 0 || coord.y < 0)
                return -1;

            return coord.x + coord.y * TradeMarauderConstants.SectorGridSide;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveGlobalSectorIndex(int2 originGlobalCoord, int2 nodeCoord)
        {
            int2 globalCoord = originGlobalCoord + nodeCoord - new int2(TradeMarauderConstants.SectorGridHalf);
            if (globalCoord.x < 0 || globalCoord.y < 0 ||
                globalCoord.x >= TradeMarauderConstants.SectorGridSide ||
                globalCoord.y >= TradeMarauderConstants.SectorGridSide)
            {
                return -1;
            }

            return globalCoord.x + globalCoord.y * TradeMarauderConstants.SectorGridSide;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int2 ResolveGlobalSectorCoord(double3 nodeAup)
        {
            int x = TradeMarauderConstants.SectorGridHalf + (int)math.round(nodeAup.x * TradeMarauderConstants.InvMacroSectorSizeMeters);
            int z = TradeMarauderConstants.SectorGridHalf + (int)math.round(nodeAup.z * TradeMarauderConstants.InvMacroSectorSizeMeters);
            if (x < 0 || z < 0 || x >= TradeMarauderConstants.SectorGridSide || z >= TradeMarauderConstants.SectorGridSide)
                return new int2(-1);

            return new int2(x, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int2 ResolveDirection(int direction)
        {
            switch (direction)
            {
                case 0: return new int2(1, 0);
                case 1: return new int2(-1, 0);
                case 2: return new int2(0, 1);
                default: return new int2(0, -1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveHeuristic(int2 coord, int2 goal)
        {
            int2 delta = math.abs(goal - coord);
            return (delta.x + delta.y) * TradeMarauderConstants.MacroSectorSizeMeters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveHeuristicWeight(float globalQualityWeight, float routePriority)
        {
            float quality = math.saturate(globalQualityWeight);
            float smoothQuality = quality * quality * (3f - 2f * quality);
            float lowQualityWeight = math.lerp(1.85f, 1.35f, math.saturate(routePriority));
            return math.lerp(lowQualityWeight, 1f, smoothQuality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PackNode(int2 coord)
        {
            return coord.x + coord.y * TradeMarauderConstants.SectorGridSide;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int2 UnpackNode(int node)
        {
            int y = node / TradeMarauderConstants.SectorGridSide;
            int x = node - y * TradeMarauderConstants.SectorGridSide;
            return new int2(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
            {
                float fallbackLengthSq = math.lengthsq(fallback);
                return math.isfinite(fallbackLengthSq) && fallbackLengthSq > 0.0001f
                    ? fallback * math.rsqrt(fallbackLengthSq)
                    : new float3(0f, 0f, 1f);
            }

            return value * math.rsqrt(lengthSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 QuantizeAupMillimeters(double3 value)
        {
            if (!IsFinite(value))
                return value;

            return new double3(
                math.round(value.x * TradeMarauderConstants.InvAupQuantizeMeters) * TradeMarauderConstants.AupQuantizeMeters,
                math.round(value.y * TradeMarauderConstants.InvAupQuantizeMeters) * TradeMarauderConstants.AupQuantizeMeters,
                math.round(value.z * TradeMarauderConstants.InvAupQuantizeMeters) * TradeMarauderConstants.AupQuantizeMeters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct MarauderOffscreenTheftJob : IJob
    {
        [NoAlias] public NativeArray<MarauderStateDTO> States;
        [NoAlias] public NativeArray<MarauderInventorySlotDTO> Inventories;
        [ReadOnly, NoAlias] public NativeArray<MarauderRoutePlanDTO> RoutePlans;
        [NoAlias] public NativeArray<MockInventoryTransactionSignal> TransactionSignals;
        [NoAlias] public NativeArray<MarauderPaddedCounterDTO> Counters;
        public MarauderTradeTuningDTO Tuning;
        public double3 PlayerAup;
        public double3 BaseAup;
        public int FrameIndex;

        public void Execute()
        {
            if (!States.IsCreated || !Inventories.IsCreated || !RoutePlans.IsCreated ||
                !TransactionSignals.IsCreated || !Counters.IsCreated)
            {
                return;
            }

            int active = math.min(States.Length, RoutePlans.Length);
            active = math.min(active, math.max(0, Tuning.ActiveMarauders));
            int signalCount = 0;
            float playerBaseDistanceSq = LocalDistanceSq(PlayerAup, BaseAup);
            if (playerBaseDistanceSq < TradeMarauderConstants.OffscreenRaidDistanceSq)
            {
                MarauderCounterUtility.Write(Counters, MarauderCounterIndex.TransactionSignalCount, 0);
                return;
            }

            for (int i = 0; i < active && signalCount < TransactionSignals.Length; i++)
            {
                MarauderStateDTO state = States[i];
                if (state.CurrentTask != (uint)MarauderTaskKind.RaidBase)
                    continue;

                float distanceSq = LocalDistanceSq(state.AUP, BaseAup);
                if (distanceSq > TradeMarauderConstants.MacroSectorSizeMeters * TradeMarauderConstants.MacroSectorSizeMeters)
                    continue;

                MarauderRoutePlanDTO plan = RoutePlans[i];
                uint seed = Hash((uint)FrameIndex, HashAupSector(BaseAup), (uint)i ^ plan.ItemHash);
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
                float probability = math.saturate(Tuning.TheftProbability + plan.Priority * 0.25f);
                if (rng.NextFloat() > probability)
                    continue;

                int quantity = math.clamp(1 + (int)(plan.Priority * 6f), 1, 12);
                AddInventory(i, plan.ItemHash, quantity);
                TransactionSignals[signalCount++] = new MockInventoryTransactionSignal
                {
                    Aup = BaseAup,
                    ItemHash = plan.ItemHash,
                    DeltaBaseQuantity = -quantity,
                    DeltaMarauderQuantity = quantity,
                    MarauderIndex = i,
                    Frame = (uint)FrameIndex,
                    Reason = 1,
                    Flags = 0
                };
            }

            MarauderCounterUtility.Write(Counters, MarauderCounterIndex.TransactionSignalCount, signalCount);
        }

        private void AddInventory(int marauderIndex, uint itemHash, int quantity)
        {
            int offset = marauderIndex * TradeMarauderConstants.MaxInventorySlotsPerMarauder;
            int end = math.min(offset + TradeMarauderConstants.MaxInventorySlotsPerMarauder, Inventories.Length);
            int firstEmpty = -1;
            for (int i = offset; i < end; i++)
            {
                MarauderInventorySlotDTO slot = Inventories[i];
                if (slot.ItemHash == itemHash)
                {
                    slot.Quantity = math.max(0, slot.Quantity + quantity);
                    Inventories[i] = slot;
                    return;
                }

                if (firstEmpty < 0 && slot.ItemHash == 0u)
                    firstEmpty = i;
            }

            if (firstEmpty >= 0)
            {
                Inventories[firstEmpty] = new MarauderInventorySlotDTO
                {
                    ItemHash = itemHash,
                    Quantity = quantity,
                    Flags = 0u,
                    Reserved0 = 0u
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint a, uint b, uint c)
        {
            uint value = a * 73856093u ^ b * 19349663u ^ c * 83492791u;
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float LocalDistanceSq(double3 a, double3 b)
        {
            double3 delta = a - b;
            float3 local = (float3)delta;
            float distanceSq = math.lengthsq(local);
            return math.isfinite(distanceSq) ? distanceSq : HugeDistanceSq();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HugeDistanceSq()
        {
            return 3.402823e+38f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashAupSector(double3 aup)
        {
            int x = (int)math.floor(aup.x * TradeMarauderConstants.InvMacroSectorSizeMeters);
            int z = (int)math.floor(aup.z * TradeMarauderConstants.InvMacroSectorSizeMeters);
            return Hash((uint)x, (uint)z, 0xA63E5D17u);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct MarauderTradeNegotiationJob : IJob
    {
        [NoAlias] public NativeArray<MarauderInventorySlotDTO> MarauderInventories;
        [NoAlias] public NativeArray<uint> PlayerInventoryHashes;
        [NoAlias] public NativeArray<int> PlayerInventoryQuantities;
        [ReadOnly, NoAlias] public NativeArray<MarauderEconomyWeightDTO> EconomyWeights;
        [NoAlias] public NativeArray<MarauderPaddedCounterDTO> Counters;
        public uint OfferedItemHash;
        public uint RequestedItemHash;
        public int OfferedQuantity;
        public int MarauderIndex;

        public void Execute()
        {
            if (MarauderCounterUtility.Read(Counters, MarauderCounterIndex.TradeRequestActive) == 0)
            {
                return;
            }

            if (!MarauderInventories.IsCreated || !PlayerInventoryHashes.IsCreated || !PlayerInventoryQuantities.IsCreated)
                return;

            int offerSlot = FindPlayerSlot(OfferedItemHash);
            if (offerSlot < 0 || PlayerInventoryQuantities[offerSlot] < OfferedQuantity || OfferedQuantity <= 0)
                return;

            float multiplier = ResolveTradeMultiplier(OfferedItemHash);
            int payout = math.max(1, (int)math.round(OfferedQuantity * multiplier));
            AtomicAdd(PlayerInventoryQuantities, offerSlot, -OfferedQuantity);
            AddMarauderInventory(OfferedItemHash, OfferedQuantity);
            AddPlayerInventory(RequestedItemHash, payout);
            MarauderCounterUtility.Write(Counters, MarauderCounterIndex.TradeRequestActive, 0);
        }

        private int FindPlayerSlot(uint itemHash)
        {
            int limit = math.min(PlayerInventoryHashes.Length, PlayerInventoryQuantities.Length);
            for (int i = 0; i < limit; i++)
            {
                if (PlayerInventoryHashes[i] == itemHash)
                    return i;
            }

            return -1;
        }

        private void AddPlayerInventory(uint itemHash, int quantity)
        {
            int empty = -1;
            int limit = math.min(PlayerInventoryHashes.Length, PlayerInventoryQuantities.Length);
            for (int i = 0; i < limit; i++)
            {
                if (PlayerInventoryHashes[i] == itemHash)
                {
                    AtomicAdd(PlayerInventoryQuantities, i, quantity);
                    return;
                }

                if (empty < 0 && PlayerInventoryHashes[i] == 0u)
                    empty = i;
            }

            if (empty >= 0)
            {
                PlayerInventoryHashes[empty] = itemHash;
                AtomicAdd(PlayerInventoryQuantities, empty, quantity);
            }
        }

        private void AddMarauderInventory(uint itemHash, int quantity)
        {
            int offset = math.max(0, MarauderIndex) * TradeMarauderConstants.MaxInventorySlotsPerMarauder;
            int end = math.min(offset + TradeMarauderConstants.MaxInventorySlotsPerMarauder, MarauderInventories.Length);
            int empty = -1;
            for (int i = offset; i < end; i++)
            {
                MarauderInventorySlotDTO slot = MarauderInventories[i];
                if (slot.ItemHash == itemHash)
                {
                    slot.Quantity = math.max(0, slot.Quantity + quantity);
                    MarauderInventories[i] = slot;
                    return;
                }

                if (empty < 0 && slot.ItemHash == 0u)
                    empty = i;
            }

            if (empty >= 0)
            {
                MarauderInventories[empty] = new MarauderInventorySlotDTO
                {
                    ItemHash = itemHash,
                    Quantity = quantity
                };
            }
        }

        private float ResolveTradeMultiplier(uint itemHash)
        {
            if (!EconomyWeights.IsCreated)
                return 1f;

            for (int i = 0; i < EconomyWeights.Length; i++)
            {
                MarauderEconomyWeightDTO weight = EconomyWeights[i];
                if (weight.ItemHash == itemHash)
                    return math.clamp(1f + weight.Scarcity * 2f + weight.Demand, 0.5f, 3f);
            }

            return 1f;
        }

        private static void AtomicAdd(NativeArray<int> values, int index, int delta)
        {
            if (!values.IsCreated || (uint)index >= (uint)values.Length)
                return;

            int* ptr = (int*)values.GetUnsafePtr();
            Interlocked.Add(ref UnsafeUtility.AsRef<int>(ptr + index), delta);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MarauderTacticalInterceptJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<MarauderStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<MarauderRoutePlanDTO> RoutePlans;
        public double3 PlayerAup;
        public float3 PlayerVelocity;
        public MarauderTradeTuningDTO Tuning;

        public void Execute(int index)
        {
            if (index >= States.Length || index >= RoutePlans.Length || index >= Tuning.ActiveMarauders)
                return;

            MarauderStateDTO state = States[index];
            double3 deltaDouble = PlayerAup - state.AUP;
            float3 localDelta = (float3)deltaDouble;
            if (!math.all(math.isfinite(localDelta)))
                return;

            float distanceSq = math.lengthsq(localDelta);
            if (!math.isfinite(distanceSq) || distanceSq > TradeMarauderConstants.TacticalDistanceSq)
                return;

            float leadSeconds = math.lerp(1.5f, 6f, math.saturate(Tuning.GlobalQualityWeight));
            float3 intercept = localDelta + PlayerVelocity * leadSeconds;
            float3 flank = new float3(-intercept.z, 0f, intercept.x);
            float3 terrainFakeNormal = ResolveTerrainFakeNormal(localDelta, state.FactionHash);
            float3 desired = SafeNormalize(intercept + flank * 0.35f + terrainFakeNormal * 70f, new float3(0f, 0f, 1f));
            state.Velocity = desired * math.lerp(22f, 75f, math.saturate(Tuning.GlobalQualityWeight));
            state.CurrentTask = (uint)MarauderTaskKind.Intercept;
            States[index] = state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveTerrainFakeNormal(float3 localDelta, uint factionHash)
        {
            float phase = (factionHash & 1023u) * 0.006135923f;
            float x = localDelta.x * 0.001f + phase;
            float z = localDelta.z * 0.001f - phase;
            return SafeNormalize(
                new float3(
                    MathLodApproximation.ApproxSinBhaskara(x * 1.7f),
                    0f,
                    MathLodApproximation.ApproxCosBhaskara(z * 1.3f)),
                new float3(1f, 0f, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MarauderVisualHydrationJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<MarauderStateDTO> States;
        [NoAlias] public NativeArray<MarauderVisualProxyDTO> VisualProxies;
        [NoAlias] public NativeArray<MarauderPaddedCounterDTO> Counters;
        public double3 PlayerAup;
        public MarauderTradeTuningDTO Tuning;

        public void Execute()
        {
            if (!States.IsCreated || !VisualProxies.IsCreated)
                return;

            float quality = math.saturate(Tuning.GlobalQualityWeight);
            int active = math.min(States.Length, math.max(0, Tuning.ActiveMarauders));
            int proxyBudget = math.clamp(
                (int)math.round(math.lerp(2f, TradeMarauderConstants.VisualProxyCapacity, quality)),
                0,
                math.min(VisualProxies.Length, TradeMarauderConstants.VisualProxyCapacity));

            int count = 0;
            for (int i = 0; i < active && count < proxyBudget; i++)
            {
                MarauderStateDTO state = States[i];
                double3 delta = state.AUP - PlayerAup;
                float3 local = (float3)delta;
                if (!math.all(math.isfinite(local)))
                    continue;

                float distanceSq = math.lengthsq(local);
                if (!math.isfinite(distanceSq) || distanceSq > TradeMarauderConstants.VisualHydrationDistanceSq)
                    continue;

                float3 forward = SafeNormalize(state.Velocity, new float3(0f, 0f, 1f));
                float3 right = SafeNormalize(new float3(forward.z, 0f, -forward.x), new float3(1f, 0f, 0f));
                float3 up = new float3(0f, 1f, 0f);
                float scale = math.lerp(0.75f, 1.35f, quality);
                VisualProxies[count++] = new MarauderVisualProxyDTO
                {
                    Row0 = new float4(right * scale, 0f),
                    Row1 = new float4(up * scale, 0f),
                    Row2 = new float4(forward * scale, 0f),
                    Row3 = new float4(local, 1f)
                };
            }

            MarauderCounterUtility.Write(Counters, MarauderCounterIndex.VisualProxyCount, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MarauderAcousticSignatureJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<MarauderStateDTO> States;
        [NoAlias] public NativeArray<MarauderAcousticSignatureDTO> AcousticSignals;
        [NoAlias] public NativeArray<MarauderPaddedCounterDTO> Counters;
        public MarauderTradeTuningDTO Tuning;
        public int FrameIndex;

        public void Execute()
        {
            if (!States.IsCreated || !AcousticSignals.IsCreated || !Counters.IsCreated)
                return;

            int active = math.min(States.Length, math.max(0, Tuning.ActiveMarauders));
            int count = 0;
            for (int i = 0; i < active && count < AcousticSignals.Length; i++)
            {
                MarauderStateDTO state = States[i];
                if (!math.all(math.isfinite(state.Velocity)) || !math.isfinite(state.HullIntegrity))
                    continue;

                float velocitySq = math.lengthsq(state.Velocity);
                float hullDamage = math.saturate(1f - state.HullIntegrity);
                float intensity = math.saturate(velocitySq * 0.00045f + hullDamage * 0.75f);
                if (intensity < 0.08f)
                    continue;

                AcousticSignals[count++] = new MarauderAcousticSignatureDTO
                {
                    AUP = state.AUP,
                    RadiusMeters = math.lerp(1500f, 12000f, intensity),
                    Intensity01 = intensity,
                    SourceId = TradeMarauderConstants.TradeMarauderSourceHash + (uint)i,
                    Frame = (uint)FrameIndex,
                    Channel = AcousticPingSignal.ChannelMetalStress,
                    Flags = 0u
                };
            }

            MarauderCounterUtility.Write(Counters, MarauderCounterIndex.AcousticSignalCount, count);
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6230)]
    [AddComponentMenu("Hecton8/Economy/Trade Marauder Director")]
    public sealed class TradeMarauderDirector : MonoBehaviour, ISlowTickable, IFrostTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001TradeMarauderRuntimeSignalPushDropCount;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_TRADE_SURGEON.bin";
        private static readonly uint CopperHash = MarauderEconomyHash.HashLowerAscii("Data_Copper".AsSpan());
        private static readonly uint TitaniumHash = MarauderEconomyHash.HashLowerAscii("Data_TitaniumScrap".AsSpan());

        [Header("Trade Marauder Runtime")]
        [SerializeField, Range(0f, 1f)] private float _globalQualityWeight = 1f;
        [SerializeField, Range(0f, 2f)] private float _basePriceVolatility = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _marauderSpawnRate = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _theftProbability = 0.18f;
        [SerializeField, Range(0f, 1f)] private float _aggressionScale = 0.35f;
        [SerializeField] private Vector3 _baseAupMeters;
        [SerializeField] private Vector3 _playerAupMeters;
        [SerializeField] private Vector3 _playerVelocityMetersPerSecond;
        private double3 _baseAupDouble;
        private double3 _playerAupDouble;
        private bool _hasExternalBaseAup;
        private bool _hasExternalPlayerAup;

        private IDataVault _vault;
        private VaultGenerationHandle<MarauderStateDTO> _statesHandle;
        private VaultGenerationHandle<MarauderInventorySlotDTO> _inventoryHandle;
        private VaultGenerationHandle<MarauderEconomyWeightDTO> _weightsHandle;
        private VaultGenerationHandle<MarauderSectorEconomyDTO> _sectorsHandle;
        private VaultGenerationHandle<MarauderRouteNodeDTO> _routesHandle;
        private VaultGenerationHandle<byte> _routeCountsHandle;
        private VaultGenerationHandle<MarauderNativeMinHeapNode> _openHeapHandle;
        private VaultGenerationHandle<float> _gCostsHandle;
        private VaultGenerationHandle<int> _cameFromHandle;
        private VaultGenerationHandle<int> _nodeStatesHandle;
        private VaultGenerationHandle<MarauderTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<MarauderTradeTuningDTO> _tuningHandle;
        private VaultGenerationHandle<float> _factionStandingHandle;
        private VaultGenerationHandle<uint> _mockInventoryHashesHandle;
        private VaultGenerationHandle<int> _mockInventoryQuantitiesHandle;
        private VaultGenerationHandle<MockInventoryTransactionSignal> _transactionScratchHandle;
        private VaultGenerationHandle<MarauderAcousticSignatureDTO> _acousticScratchHandle;
        private VaultGenerationHandle<MarauderLootNodeDTO> _lootHandle;
        private VaultGenerationHandle<MarauderSectorHashEntryDTO> _sectorHashHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<MarauderPaddedCounterDTO> _countersHandle;
        private VaultGenerationHandle<MarauderRoutePlanDTO> _routePlansHandle;
        private VaultGenerationHandle<MarauderVisualProxyDTO> _visualProxyHandle;
        private JobHandle _activeJobHandle;
        private bool _jobScheduled;
        private bool _registeredSlowTick;
        private bool _registeredFrostTick;
        private bool _registeredHotSwapListener;
        private bool _defaultsInitialized;
        private int _frameIndex;
        private int _searchEpoch = 1;
        private int _routeStartIndex;

        public static TradeMarauderDirector ActiveForEditor;

        private void OnEnable()
        {
            ActiveForEditor = this;
            if (!Application.isPlaying)
                return;

            WarmSignalLanes();
            if (_jobScheduled)
                TryCompleteFinishedJob();
            if (!_jobScheduled)
                EnsureVaultBuffers();
            TryRegisterHotSwapListener();
            TryRegisterRuntimeLanes();
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
            CompleteActiveJobForLifecycle();

            TryUnregisterHotSwapListener();
            TryUnregisterRuntimeLanes();

            if (ReferenceEquals(ActiveForEditor, this))
                ActiveForEditor = null;

            ReleaseOwnedVaultHandles(_vault);
            ClearHandles();
            _vault = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterRuntimeLanes();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterRuntimeLanes();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompleteActiveJobForLifecycle();
            ReleaseOwnedVaultHandles(_vault);
            ClearHandles();
            _vault = currentService as IDataVault;
            _defaultsInitialized = false;

            if (isActiveAndEnabled && _vault != null)
                EnsureVaultBuffers();
        }

        public void SlowTick()
        {
            TryCompleteFinishedJob();
        }

        public void FrostTick()
        {
            // L19 hop2 LIVE: TradeMarauderDirector.FrostTick mono AV under batch after STARTERGRANT
            if (Application.isBatchMode)
                return;

            TryCompleteFinishedJob();
            if (_jobScheduled)
                return;

            if (!EnsureVaultBuffers() || !TryResolveAllViews(
                    out NativeArray<MarauderStateDTO> states,
                    out NativeArray<MarauderInventorySlotDTO> inventories,
                    out NativeArray<MarauderEconomyWeightDTO> weights,
                    out NativeArray<MarauderSectorEconomyDTO> sectors,
                    out NativeArray<MarauderRouteNodeDTO> routes,
                    out NativeArray<byte> routeCounts,
                    out NativeArray<MarauderNativeMinHeapNode> openHeap,
                    out NativeArray<float> gCosts,
                    out NativeArray<int> cameFrom,
                    out NativeArray<int> nodeStates,
                    out NativeArray<MarauderTelemetryEntry> telemetry,
                    out NativeArray<MarauderTradeTuningDTO> tuningArray,
                    out NativeArray<float> factionStanding,
                    out NativeArray<uint> mockInventoryHashes,
                    out NativeArray<int> mockInventoryQuantities,
                    out NativeArray<MockInventoryTransactionSignal> transactionScratch,
                    out NativeArray<MarauderAcousticSignatureDTO> acousticScratch,
                    out NativeArray<MarauderLootNodeDTO> lootNodes,
                    out NativeArray<MarauderSectorHashEntryDTO> _,
                    out NativeArray<MarauderPaddedCounterDTO> counters,
                    out NativeArray<MarauderRoutePlanDTO> routePlans,
                    out NativeArray<MarauderVisualProxyDTO> visualProxies))
            {
                return;
            }

            MarauderTradeTuningDTO tuning = BuildTuning(tuningArray);
            tuningArray[0] = tuning;
            double3 playerAup = _hasExternalPlayerAup ? _playerAupDouble : global::Hecton8.World.AUPMath.ToDouble3(_playerAupMeters);
            double3 baseAup = _hasExternalBaseAup ? _baseAupDouble : global::Hecton8.World.AUPMath.ToDouble3(_baseAupMeters);
            float3 playerVelocity = _playerVelocityMetersPerSecond;
            int maxSolves = math.max(1, tuning.MaxRouteSolves);
            int active = math.min(tuning.ActiveMarauders, states.Length);
            int searchEpoch = _searchEpoch++;
            if (_searchEpoch > 100000000)
                _searchEpoch = 1;

            MarauderScarcityMockInventoryJob scarcityJob = new MarauderScarcityMockInventoryJob
            {
                InventoryItemHashes = mockInventoryHashes,
                InventoryQuantities = mockInventoryQuantities,
                EconomyWeights = weights,
                HoardedItemHash = tuning.CopperHash,
                HoardedQuantity = 0
            };
            scarcityJob.Execute();

            MarauderSupplyChainSolverJob solverJob = new MarauderSupplyChainSolverJob
            {
                States = states,
                EconomyWeights = weights,
                SectorEconomy = sectors,
                RoutePlans = routePlans,
                LootNodes = lootNodes,
                FactionStanding = factionStanding,
                Telemetry = telemetry,
                TuningBuffer = tuningArray,
                Counters = counters,
                Tuning = tuning,
                PlayerAup = playerAup,
                BaseAup = baseAup,
                FrameIndex = _frameIndex
            };
            solverJob.Execute();

            MarauderMacroAStarJob aStarJob = new MarauderMacroAStarJob
            {
                States = states,
                RoutePlans = routePlans,
                SectorEconomy = sectors,
                OpenHeap = openHeap,
                GCosts = gCosts,
                CameFrom = cameFrom,
                NodeStates = nodeStates,
                RouteNodes = routes,
                RouteCounts = routeCounts,
                Telemetry = telemetry,
                Counters = counters,
                Tuning = tuning,
                FrameIndex = _frameIndex,
                SearchEpoch = searchEpoch,
                StartMarauder = _routeStartIndex,
                MaxSolves = maxSolves
            };
            aStarJob.Execute();

            MarauderOffscreenTheftJob theftJob = new MarauderOffscreenTheftJob
            {
                States = states,
                Inventories = inventories,
                RoutePlans = routePlans,
                TransactionSignals = transactionScratch,
                Counters = counters,
                Tuning = tuning,
                PlayerAup = playerAup,
                BaseAup = baseAup,
                FrameIndex = _frameIndex
            };
            theftJob.Execute();

            MarauderTradeNegotiationJob tradeJob = new MarauderTradeNegotiationJob
            {
                MarauderInventories = inventories,
                PlayerInventoryHashes = mockInventoryHashes,
                PlayerInventoryQuantities = mockInventoryQuantities,
                EconomyWeights = weights,
                Counters = counters,
                OfferedItemHash = tuning.CopperHash,
                RequestedItemHash = tuning.TitaniumHash,
                OfferedQuantity = 3,
                MarauderIndex = 0
            };
            tradeJob.Execute();

            MarauderTacticalInterceptJob interceptJob = new MarauderTacticalInterceptJob
            {
                States = states,
                RoutePlans = routePlans,
                PlayerAup = playerAup,
                PlayerVelocity = playerVelocity,
                Tuning = tuning
            };
            for (int i = 0; i < active; i++)
                interceptJob.Execute(i);

            MarauderVisualHydrationJob visualJob = new MarauderVisualHydrationJob
            {
                States = states,
                VisualProxies = visualProxies,
                Counters = counters,
                PlayerAup = playerAup,
                Tuning = tuning
            };
            visualJob.Execute();

            MarauderAcousticSignatureJob acousticJob = new MarauderAcousticSignatureJob
            {
                States = states,
                AcousticSignals = acousticScratch,
                Counters = counters,
                Tuning = tuning,
                FrameIndex = _frameIndex
            };
            acousticJob.Execute();

            _activeJobHandle = default;
            _jobScheduled = false;
            _frameIndex++;
            _routeStartIndex = active > 0 ? (_routeStartIndex + maxSolves) % active : 0;
            PublishCompletedSignals();
        }

        public void SetPlayerSnapshot(double3 playerAup, float3 velocityMetersPerSecond)
        {
            _playerAupDouble = playerAup;
            _hasExternalPlayerAup = true;
            float3 playerLocal = AupPrecisionMath.LocalDeltaFloat3(playerAup, HectonFloatingOrigin.CurrentTotalOffsetDouble, float3.zero);
            _playerAupMeters = new Vector3(playerLocal.x, playerLocal.y, playerLocal.z);
            _playerVelocityMetersPerSecond = velocityMetersPerSecond;
        }

        public void SetBaseAup(double3 baseAup)
        {
            _baseAupDouble = baseAup;
            _hasExternalBaseAup = true;
            float3 baseLocal = AupPrecisionMath.LocalDeltaFloat3(baseAup, HectonFloatingOrigin.CurrentTotalOffsetDouble, float3.zero);
            _baseAupMeters = new Vector3(baseLocal.x, baseLocal.y, baseLocal.z);
        }

        public bool ApplyFactionReputationDelta(uint factionHash, float delta)
        {
            if (_jobScheduled || !EnsureVaultBuffers() || _vault == null)
                return false;

            if (!TryOpenVaultView(_vault, in _factionStandingHandle, TradeMarauderConstants.FactionCapacity, out NativeArray<float> standings))
                return false;

            int index = (int)(factionHash % (uint)standings.Length);
            standings[index] = math.clamp(standings[index] + delta, -1f, 1f);
            return true;
        }

        public bool TryGetTuningForEditor(out MarauderTradeTuningDTO tuning)
        {
            tuning = default;
            if (_jobScheduled || !EnsureVaultBuffers() || _vault == null)
                return false;

            if (!TryOpenVaultView(_vault, in _tuningHandle, 1, out NativeArray<MarauderTradeTuningDTO> tuningArray))
                return false;

            tuning = tuningArray[0];
            return true;
        }

        public bool TrySetTuningFromEditor(float basePriceVolatility, float spawnRate, float theftProbability, float aggressionScale)
        {
            _basePriceVolatility = math.saturate(basePriceVolatility);
            _marauderSpawnRate = math.saturate(spawnRate);
            _theftProbability = math.saturate(theftProbability);
            _aggressionScale = math.saturate(aggressionScale);

            if (_jobScheduled || !EnsureVaultBuffers() || _vault == null)
                return false;

            if (!TryOpenVaultView(_vault, in _tuningHandle, 1, out NativeArray<MarauderTradeTuningDTO> tuningArray))
                return false;

            tuningArray[0] = BuildTuning(tuningArray);
            return true;
        }

        public bool TryResolveEditorViews(
            out NativeArray<MarauderStateDTO>.ReadOnly states,
            out NativeArray<MarauderRouteNodeDTO>.ReadOnly routes,
            out NativeArray<byte>.ReadOnly routeCounts)
        {
            states = default;
            routes = default;
            routeCounts = default;
            if (_jobScheduled || !EnsureVaultBuffers() || _vault == null)
                return false;

            if (!TryOpenVaultView(_vault, in _statesHandle, TradeMarauderConstants.MaxMarauders, out NativeArray<MarauderStateDTO> mutableStates) ||
                !TryOpenVaultView(_vault, in _routesHandle, TradeMarauderConstants.MaxMarauders * TradeMarauderConstants.RouteNodeStride, out NativeArray<MarauderRouteNodeDTO> mutableRoutes) ||
                !TryOpenVaultView(_vault, in _routeCountsHandle, TradeMarauderConstants.MaxMarauders, out NativeArray<byte> mutableRouteCounts))
            {
                return false;
            }

            states = mutableStates.AsReadOnly();
            routes = mutableRoutes.AsReadOnly();
            routeCounts = mutableRouteCounts.AsReadOnly();
            return true;
        }

#if UNITY_EDITOR
        public bool TryApplyCsvOverride(ReadOnlySpan<byte> csvBytes, out int acceptedRows, out int rejectedRows)
        {
            acceptedRows = 0;
            rejectedRows = 0;
            if (_jobScheduled || !EnsureVaultBuffers() || _vault == null)
                return false;

            if (!TryOpenVaultView(_vault, in _weightsHandle, TradeMarauderConstants.MaxEconomyItems, out NativeArray<MarauderEconomyWeightDTO> weights))
                return false;

            bool parsed = MarauderEconomyCsvParser.TryParse(csvBytes, weights, out acceptedRows, out rejectedRows);
            if (TryOpenVaultView(_vault, in _countersHandle, TradeMarauderConstants.CounterCapacity, out NativeArray<MarauderPaddedCounterDTO> counters) &&
                counters.Length > (int)MarauderCounterIndex.CsvRowsRejected)
            {
                MarauderCounterUtility.Write(counters, MarauderCounterIndex.CsvRowsAccepted, acceptedRows);
                MarauderCounterUtility.Write(counters, MarauderCounterIndex.CsvRowsRejected, rejectedRows);
                MarauderCounterUtility.Write(counters, MarauderCounterIndex.CachedEconomyValid, 0);
            }

            return parsed;
        }
#endif

        public static void WarmSignalLanes()
        {
            SignalBus<MockInventoryTransactionSignal>.Configure(32, maxFrameSignals: 128, lowTierFrameSignals: 16, laneHash: TradeMarauderConstants.TradeMarauderSourceHash ^ 0x54584E31u);
            SignalBus<MockInventoryTransactionSignal>.EnsureInitialized();
            SignalBus<AcousticPingSignal>.EnsureInitialized();
        }

        private void TryRegisterRuntimeLanes()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            if (!_registeredFrostTick)
                _registeredFrostTick = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterRuntimeLanes()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredFrostTick)
            {
                GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Environment);
                _registeredFrostTick = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private bool EnsureVaultBuffers()
        {
            IDataVault vault = _vault;
            if (vault == null)
                vault = GlobalRegistry.DataVault;

            if (vault == null)
                return false;

            if (!ReferenceEquals(_vault, vault))
            {
                ReleaseOwnedVaultHandles(_vault);
                _vault = vault;
                ClearHandles();
                _defaultsInitialized = false;
            }

            _statesHandle = EnsureHandle(_statesHandle, BufferID.ShinobuTradeMarauderStates, TradeMarauderConstants.MaxMarauders, NativeArrayOptions.UninitializedMemory);
            _inventoryHandle = EnsureHandle(_inventoryHandle, BufferID.ShinobuTradeMarauderInventories, TradeMarauderConstants.MaxMarauders * TradeMarauderConstants.MaxInventorySlotsPerMarauder, NativeArrayOptions.UninitializedMemory);
            _weightsHandle = EnsureHandle(_weightsHandle, BufferID.ShinobuTradeMarauderEconomyWeights, TradeMarauderConstants.MaxEconomyItems, NativeArrayOptions.ClearMemory);
            _sectorsHandle = EnsureHandle(_sectorsHandle, BufferID.ShinobuTradeMarauderSectorEconomy, TradeMarauderConstants.SectorNodeCapacity, NativeArrayOptions.ClearMemory);
            _routesHandle = EnsureHandle(_routesHandle, BufferID.ShinobuTradeMarauderRoutes, TradeMarauderConstants.MaxMarauders * TradeMarauderConstants.RouteNodeStride, NativeArrayOptions.UninitializedMemory);
            _routeCountsHandle = EnsureHandle(_routeCountsHandle, BufferID.ShinobuTradeMarauderRouteCounts, TradeMarauderConstants.MaxMarauders, NativeArrayOptions.ClearMemory);
            _openHeapHandle = EnsureHandle(_openHeapHandle, BufferID.ShinobuTradeMarauderAStarOpenHeap, TradeMarauderConstants.SectorNodeCapacity, NativeArrayOptions.UninitializedMemory);
            _gCostsHandle = EnsureHandle(_gCostsHandle, BufferID.ShinobuTradeMarauderAStarGCosts, TradeMarauderConstants.SectorNodeCapacity, NativeArrayOptions.UninitializedMemory);
            _cameFromHandle = EnsureHandle(_cameFromHandle, BufferID.ShinobuTradeMarauderAStarCameFrom, TradeMarauderConstants.SectorNodeCapacity, NativeArrayOptions.UninitializedMemory);
            _nodeStatesHandle = EnsureHandle(_nodeStatesHandle, BufferID.ShinobuTradeMarauderAStarNodeStates, TradeMarauderConstants.SectorNodeCapacity, NativeArrayOptions.ClearMemory);
            _telemetryHandle = EnsureHandle(_telemetryHandle, BufferID.ShinobuTradeMarauderTelemetry, TradeMarauderConstants.TelemetryFrameCount, NativeArrayOptions.ClearMemory);
            _tuningHandle = EnsureHandle(_tuningHandle, BufferID.ShinobuTradeMarauderTuning, 1, NativeArrayOptions.ClearMemory);
            _factionStandingHandle = EnsureHandle(_factionStandingHandle, BufferID.ShinobuTradeMarauderFactionStanding, TradeMarauderConstants.FactionCapacity, NativeArrayOptions.ClearMemory);
            _mockInventoryHashesHandle = EnsureHandle(_mockInventoryHashesHandle, BufferID.ShinobuTradeMarauderMockInventoryHashes, TradeMarauderConstants.MaxEconomyItems, NativeArrayOptions.ClearMemory);
            _mockInventoryQuantitiesHandle = EnsureHandle(_mockInventoryQuantitiesHandle, BufferID.ShinobuTradeMarauderMockInventoryQuantities, TradeMarauderConstants.MaxEconomyItems, NativeArrayOptions.ClearMemory);
            _transactionScratchHandle = EnsureHandle(_transactionScratchHandle, BufferID.ShinobuTradeMarauderSignalScratch, TradeMarauderConstants.SignalScratchCapacity, NativeArrayOptions.UninitializedMemory);
            _acousticScratchHandle = EnsureHandle(_acousticScratchHandle, BufferID.ShinobuTradeMarauderAcousticScratch, TradeMarauderConstants.SignalScratchCapacity, NativeArrayOptions.UninitializedMemory);
            _lootHandle = EnsureHandle(_lootHandle, BufferID.ShinobuTradeMarauderLootNodes, TradeMarauderConstants.LootNodeCapacity, NativeArrayOptions.ClearMemory);
            _sectorHashHandle = EnsureHandle(_sectorHashHandle, BufferID.ShinobuTradeMarauderSectorHash, TradeMarauderConstants.SectorNodeCapacity, NativeArrayOptions.ClearMemory);
            _csvScratchHandle = EnsureHandle(_csvScratchHandle, BufferID.ShinobuTradeMarauderCsvScratch, TradeMarauderConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory);
            _countersHandle = EnsureHandle(_countersHandle, BufferID.ShinobuTradeMarauderCounters, TradeMarauderConstants.CounterCapacity, NativeArrayOptions.ClearMemory);
            _routePlansHandle = EnsureHandle(_routePlansHandle, BufferID.ShinobuTradeMarauderRoutePlans, TradeMarauderConstants.MaxMarauders, NativeArrayOptions.ClearMemory);
            _visualProxyHandle = EnsureHandle(_visualProxyHandle, BufferID.ShinobuTradeMarauderVisualProxies, TradeMarauderConstants.VisualProxyCapacity, NativeArrayOptions.UninitializedMemory);

            if (!_defaultsInitialized && TryResolveAllViews(
                    out NativeArray<MarauderStateDTO> states,
                    out NativeArray<MarauderInventorySlotDTO> inventories,
                    out NativeArray<MarauderEconomyWeightDTO> weights,
                    out NativeArray<MarauderSectorEconomyDTO> sectors,
                    out NativeArray<MarauderRouteNodeDTO> routes,
                    out NativeArray<byte> routeCounts,
                    out NativeArray<MarauderNativeMinHeapNode> _,
                    out NativeArray<float> _,
                    out NativeArray<int> _,
                    out NativeArray<int> nodeStates,
                    out NativeArray<MarauderTelemetryEntry> telemetry,
                    out NativeArray<MarauderTradeTuningDTO> tuning,
                    out NativeArray<float> factionStanding,
                    out NativeArray<uint> mockInventoryHashes,
                    out NativeArray<int> mockInventoryQuantities,
                    out NativeArray<MockInventoryTransactionSignal> _,
                    out NativeArray<MarauderAcousticSignatureDTO> _,
                    out NativeArray<MarauderLootNodeDTO> _,
                    out NativeArray<MarauderSectorHashEntryDTO> sectorHash,
                    out NativeArray<MarauderPaddedCounterDTO> counters,
                    out NativeArray<MarauderRoutePlanDTO> routePlans,
                    out NativeArray<MarauderVisualProxyDTO> visualProxies))
            {
                GenerateEmergencyMockEconomy(states, inventories, weights, sectors, routes, routeCounts, nodeStates, telemetry, tuning, factionStanding, mockInventoryHashes, mockInventoryQuantities, sectorHash, counters, routePlans, visualProxies);
                _defaultsInitialized = true;
            }

            return IsHandleCreated(in _statesHandle) &&
                   IsHandleCreated(in _inventoryHandle) &&
                   IsHandleCreated(in _weightsHandle) &&
                   IsHandleCreated(in _sectorsHandle) &&
                   IsHandleCreated(in _routesHandle) &&
                   IsHandleCreated(in _routeCountsHandle) &&
                   IsHandleCreated(in _openHeapHandle) &&
                   IsHandleCreated(in _gCostsHandle) &&
                   IsHandleCreated(in _cameFromHandle) &&
                   IsHandleCreated(in _nodeStatesHandle) &&
                   IsHandleCreated(in _telemetryHandle) &&
                   IsHandleCreated(in _tuningHandle) &&
                   IsHandleCreated(in _factionStandingHandle) &&
                   IsHandleCreated(in _mockInventoryHashesHandle) &&
                   IsHandleCreated(in _mockInventoryQuantitiesHandle) &&
                   IsHandleCreated(in _transactionScratchHandle) &&
                   IsHandleCreated(in _acousticScratchHandle) &&
                   IsHandleCreated(in _lootHandle) &&
                   IsHandleCreated(in _sectorHashHandle) &&
                   IsHandleCreated(in _csvScratchHandle) &&
                   IsHandleCreated(in _countersHandle) &&
                   IsHandleCreated(in _routePlansHandle) &&
                   IsHandleCreated(in _visualProxyHandle);
        }

        private VaultGenerationHandle<T> EnsureHandle<T>(
            VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length,
            NativeArrayOptions options) where T : struct
        {
            if (_vault == null || length <= 0)
                return default;

            if (TryOpenVaultView(_vault, in handle, length, out NativeArray<T> _))
                return handle;

            if (_vault.TryGetGenerationHandle<T>(bufferId, out handle) &&
                TryOpenVaultView(_vault, in handle, length, out NativeArray<T> _))
            {
                return handle;
            }

            if (_vault.IsAllocationLocked)
                return default;

            handle = _vault.EnsureGenerationHandle<T>(bufferId, length, SystemID.TradeMarauders, options);
            return TryOpenVaultView(_vault, in handle, length, out NativeArray<T> _)
                ? handle
                : default;
        }

        private bool TryResolveAllViews(
            out NativeArray<MarauderStateDTO> states,
            out NativeArray<MarauderInventorySlotDTO> inventories,
            out NativeArray<MarauderEconomyWeightDTO> weights,
            out NativeArray<MarauderSectorEconomyDTO> sectors,
            out NativeArray<MarauderRouteNodeDTO> routes,
            out NativeArray<byte> routeCounts,
            out NativeArray<MarauderNativeMinHeapNode> openHeap,
            out NativeArray<float> gCosts,
            out NativeArray<int> cameFrom,
            out NativeArray<int> nodeStates,
            out NativeArray<MarauderTelemetryEntry> telemetry,
            out NativeArray<MarauderTradeTuningDTO> tuning,
            out NativeArray<float> factionStanding,
            out NativeArray<uint> mockInventoryHashes,
            out NativeArray<int> mockInventoryQuantities,
            out NativeArray<MockInventoryTransactionSignal> transactionScratch,
            out NativeArray<MarauderAcousticSignatureDTO> acousticScratch,
            out NativeArray<MarauderLootNodeDTO> lootNodes,
            out NativeArray<MarauderSectorHashEntryDTO> sectorHash,
            out NativeArray<MarauderPaddedCounterDTO> counters,
            out NativeArray<MarauderRoutePlanDTO> routePlans,
            out NativeArray<MarauderVisualProxyDTO> visualProxies)
        {
            states = default;
            inventories = default;
            weights = default;
            sectors = default;
            routes = default;
            routeCounts = default;
            openHeap = default;
            gCosts = default;
            cameFrom = default;
            nodeStates = default;
            telemetry = default;
            tuning = default;
            factionStanding = default;
            mockInventoryHashes = default;
            mockInventoryQuantities = default;
            transactionScratch = default;
            acousticScratch = default;
            lootNodes = default;
            sectorHash = default;
            counters = default;
            routePlans = default;
            visualProxies = default;

            return TryOpenVaultView(_vault, in _statesHandle, TradeMarauderConstants.MaxMarauders, out states) &&
                   TryOpenVaultView(_vault, in _inventoryHandle, TradeMarauderConstants.MaxMarauders * TradeMarauderConstants.MaxInventorySlotsPerMarauder, out inventories) &&
                   TryOpenVaultView(_vault, in _weightsHandle, TradeMarauderConstants.MaxEconomyItems, out weights) &&
                   TryOpenVaultView(_vault, in _sectorsHandle, TradeMarauderConstants.SectorNodeCapacity, out sectors) &&
                   TryOpenVaultView(_vault, in _routesHandle, TradeMarauderConstants.MaxMarauders * TradeMarauderConstants.RouteNodeStride, out routes) &&
                   TryOpenVaultView(_vault, in _routeCountsHandle, TradeMarauderConstants.MaxMarauders, out routeCounts) &&
                   TryOpenVaultView(_vault, in _openHeapHandle, TradeMarauderConstants.SectorNodeCapacity, out openHeap) &&
                   TryOpenVaultView(_vault, in _gCostsHandle, TradeMarauderConstants.SectorNodeCapacity, out gCosts) &&
                   TryOpenVaultView(_vault, in _cameFromHandle, TradeMarauderConstants.SectorNodeCapacity, out cameFrom) &&
                   TryOpenVaultView(_vault, in _nodeStatesHandle, TradeMarauderConstants.SectorNodeCapacity, out nodeStates) &&
                   TryOpenVaultView(_vault, in _telemetryHandle, TradeMarauderConstants.TelemetryFrameCount, out telemetry) &&
                   TryOpenVaultView(_vault, in _tuningHandle, 1, out tuning) &&
                   TryOpenVaultView(_vault, in _factionStandingHandle, TradeMarauderConstants.FactionCapacity, out factionStanding) &&
                   TryOpenVaultView(_vault, in _mockInventoryHashesHandle, TradeMarauderConstants.MaxEconomyItems, out mockInventoryHashes) &&
                   TryOpenVaultView(_vault, in _mockInventoryQuantitiesHandle, TradeMarauderConstants.MaxEconomyItems, out mockInventoryQuantities) &&
                   TryOpenVaultView(_vault, in _transactionScratchHandle, TradeMarauderConstants.SignalScratchCapacity, out transactionScratch) &&
                   TryOpenVaultView(_vault, in _acousticScratchHandle, TradeMarauderConstants.SignalScratchCapacity, out acousticScratch) &&
                   TryOpenVaultView(_vault, in _lootHandle, TradeMarauderConstants.LootNodeCapacity, out lootNodes) &&
                   TryOpenVaultView(_vault, in _sectorHashHandle, TradeMarauderConstants.SectorNodeCapacity, out sectorHash) &&
                   TryOpenVaultView(_vault, in _countersHandle, TradeMarauderConstants.CounterCapacity, out counters) &&
                   TryOpenVaultView(_vault, in _routePlansHandle, TradeMarauderConstants.MaxMarauders, out routePlans) &&
                   TryOpenVaultView(_vault, in _visualProxyHandle, TradeMarauderConstants.VisualProxyCapacity, out visualProxies);
        }

        private static bool TryOpenVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   requiredLength >= 0 &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private MarauderTradeTuningDTO BuildTuning(NativeArray<MarauderTradeTuningDTO> tuningArray)
        {
            int active = math.clamp((int)math.round(TradeMarauderConstants.MaxMarauders * math.max(0.05f, _marauderSpawnRate)), 1, TradeMarauderConstants.MaxMarauders);
            MarauderTradeTuningDTO previous = tuningArray.IsCreated && tuningArray.Length > 0 ? tuningArray[0] : default;
            float rawQuality = ResolveGlobalQualityWeight();
            float quality = previous.Frame == 0u
                ? rawQuality
                : math.saturate(math.lerp(previous.GlobalQualityWeight, rawQuality, 0.25f));
            return new MarauderTradeTuningDTO
            {
                GlobalQualityWeight = quality,
                BasePriceVolatility = _basePriceVolatility,
                MarauderSpawnRate = _marauderSpawnRate,
                TheftProbability = _theftProbability,
                AggressionScale = _aggressionScale,
                LeviathanAggressionThreshold = math.lerp(0.92f, 0.58f, math.saturate(_aggressionScale)),
                RouteReplanSeconds = 5f,
                CachedGlobalScarcityIndex = previous.CachedGlobalScarcityIndex,
                CopperHash = CopperHash,
                TitaniumHash = TitaniumHash,
                ActiveMarauders = active,
                ItemEvaluationLimit = math.clamp((int)math.round(math.lerp(TradeMarauderConstants.LowTierEconomyItems, TradeMarauderConstants.MaxEconomyItems, quality)), 1, TradeMarauderConstants.MaxEconomyItems),
                MaxRouteSolves = math.clamp((int)math.round(math.lerp(1f, 12f, quality)), 1, active),
                MaxAStarIterations = math.clamp((int)math.round(math.lerp(512f, TradeMarauderConstants.SectorNodeCapacity, quality)), 64, TradeMarauderConstants.SectorNodeCapacity),
                Frame = (uint)_frameIndex,
                Flags = (uint)math.round(quality * 65535f)
            };
        }

        private float ResolveGlobalQualityWeight()
        {
            float profileWeight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(profileWeight))
                profileWeight = 0.5f;

            profileWeight = math.saturate(profileWeight);
            float requested = math.saturate(_globalQualityWeight * profileWeight);
            float stress = SignalBusRegistry.SystemStress01;
            float vaultPressure = _vault != null ? math.saturate(_vault.CapacityPressure01) : 0f;
            float throttle = 1f - math.saturate(stress * 0.65f + vaultPressure * 0.35f);
            return math.saturate(requested * throttle);
        }

        private void TryCompleteFinishedJob()
        {
            if (!_jobScheduled || !_activeJobHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeJobHandle))
                return;

            _jobScheduled = false;
            PublishCompletedSignals();
        }

        private void CompleteActiveJobForLifecycle()
        {
            if (!_jobScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true))
                return;

            _jobScheduled = false;
            PublishCompletedSignals();
        }

        private void PublishCompletedSignals()
        {
            if (_vault == null)
                return;

            if (!TryOpenVaultView(_vault, in _countersHandle, TradeMarauderConstants.CounterCapacity, out NativeArray<MarauderPaddedCounterDTO> counters))
                return;

            bool hasTransactions = TryOpenVaultView(_vault, in _transactionScratchHandle, TradeMarauderConstants.SignalScratchCapacity, out NativeArray<MockInventoryTransactionSignal> transactions);
            bool hasAcoustic = TryOpenVaultView(_vault, in _acousticScratchHandle, TradeMarauderConstants.SignalScratchCapacity, out NativeArray<MarauderAcousticSignatureDTO> acoustic);
            int transactionCount = ReadCounter(counters, MarauderCounterIndex.TransactionSignalCount);
            bool baseRaidNotificationQueued = false;
            for (int i = 0; hasTransactions && i < transactionCount && i < transactions.Length; i++)
            {
                MockInventoryTransactionSignal signal = transactions[i];
                SignalBus<MockInventoryTransactionSignal>.TryPushTracked(in signal, ref s_x001TradeMarauderRuntimeSignalPushDropCount);
                if (!baseRaidNotificationQueued)
                {
                    NotificationEvents.TryPushCritical(TradeMarauderConstants.BaseRaidedMessage.AsSpan());
                    baseRaidNotificationQueued = true;
                }
            }

            int acousticCount = ReadCounter(counters, MarauderCounterIndex.AcousticSignalCount);
            for (int i = 0; hasAcoustic && i < acousticCount && i < acoustic.Length; i++)
            {
                MarauderAcousticSignatureDTO signature = acoustic[i];
                AcousticPingSignal signal = new AcousticPingSignal
                {
                    PositionAup = Hecton8.World.AbsoluteUniversePosition.FromAbsolutePosition(signature.AUP),
                    RadiusMeters = signature.RadiusMeters,
                    Intensity01 = signature.Intensity01,
                    SourceId = signature.SourceId,
                    Channel = (byte)math.min(signature.Channel, byte.MaxValue),
                    Flags = (byte)math.min(signature.Flags, byte.MaxValue)
                };
                SignalBus<AcousticPingSignal>.TryPushTracked(in signal, ref s_x001TradeMarauderRuntimeSignalPushDropCount);
            }

            if (ReadCounter(counters, MarauderCounterIndex.FaultFlags) != 0)
            {
                if (!TryOpenVaultView(_vault, in _telemetryHandle, TradeMarauderConstants.TelemetryFrameCount, out NativeArray<MarauderTelemetryEntry> telemetry) ||
                    !TryDumpBlackBox(telemetry))
                {
                    Hecton8.Core.H8Debug.LogError("[TradeMarauderDirector] blackbox dump failed.");
                }
            }

            MarauderCounterUtility.Write(counters, MarauderCounterIndex.TransactionSignalCount, 0);
            MarauderCounterUtility.Write(counters, MarauderCounterIndex.AcousticSignalCount, 0);
            MarauderCounterUtility.Write(counters, MarauderCounterIndex.FaultFlags, 0);
        }

        private static int ReadCounter(NativeArray<MarauderPaddedCounterDTO> counters, MarauderCounterIndex index)
        {
            return math.max(0, MarauderCounterUtility.Read(counters, index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private static unsafe bool TryDumpBlackBox(NativeArray<MarauderTelemetryEntry> telemetry)
        {
            return telemetry.IsCreated && telemetry.Length > 0;
        }

        private static void GenerateEmergencyMockEconomy(
            NativeArray<MarauderStateDTO> states,
            NativeArray<MarauderInventorySlotDTO> inventories,
            NativeArray<MarauderEconomyWeightDTO> weights,
            NativeArray<MarauderSectorEconomyDTO> sectors,
            NativeArray<MarauderRouteNodeDTO> routes,
            NativeArray<byte> routeCounts,
            NativeArray<int> nodeStates,
            NativeArray<MarauderTelemetryEntry> telemetry,
            NativeArray<MarauderTradeTuningDTO> tuning,
            NativeArray<float> factionStanding,
            NativeArray<uint> mockInventoryHashes,
            NativeArray<int> mockInventoryQuantities,
            NativeArray<MarauderSectorHashEntryDTO> sectorHash,
            NativeArray<MarauderPaddedCounterDTO> counters,
            NativeArray<MarauderRoutePlanDTO> routePlans,
            NativeArray<MarauderVisualProxyDTO> visualProxies)
        {
            for (int i = 0; i < states.Length; i++)
            {
                float angle = (i / (float)math.max(1, states.Length)) * math.PI * 2f;
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
                states[i] = new MarauderStateDTO
                {
                    AUP = new double3(cos * 18000d, -200d - i * 3d, sin * 18000d),
                    Velocity = float3.zero,
                    FactionHash = (uint)(0xFA630000u + (uint)(i & 3)),
                    CurrentTask = (uint)MarauderTaskKind.TradeRoute,
                    HullIntegrity = 1f,
                    _pad0 = 0u,
                    _pad1 = 0ul
                };
            }

            for (int i = 0; i < inventories.Length; i++)
                inventories[i] = default;

            uint quartzHash = MarauderEconomyHash.HashLowerAscii("Data_Quartz".AsSpan());
            uint glassHash = MarauderEconomyHash.HashLowerAscii("Data_Glass".AsSpan());
            uint silverHash = MarauderEconomyHash.HashLowerAscii("Data_Silver".AsSpan());
            uint lithiumHash = MarauderEconomyHash.HashLowerAscii("Data_Lithium".AsSpan());
            for (int i = 0; i < weights.Length; i++)
            {
                uint itemHash = ResolveDefaultItemHash(i, quartzHash, glassHash, silverHash, lithiumHash);
                weights[i] = new MarauderEconomyWeightDTO
                {
                    ItemHash = itemHash,
                    BasePrice = 1f + (i % 7) * 0.35f,
                    Supply = i % 3 == 0 ? 0.7f : 0.25f,
                    Demand = itemHash == CopperHash ? 0.9f : 0.35f,
                    Scarcity = itemHash == CopperHash ? 0.8f : 0.2f,
                    Flags = 0u
                };
            }

            for (int z = 0; z < TradeMarauderConstants.SectorGridSide; z++)
            {
                for (int x = 0; x < TradeMarauderConstants.SectorGridSide; x++)
                {
                    int index = x + z * TradeMarauderConstants.SectorGridSide;
                    double3 aup = new double3(
                        (x - TradeMarauderConstants.SectorGridHalf) * (double)TradeMarauderConstants.MacroSectorSizeMeters,
                        -250d,
                        (z - TradeMarauderConstants.SectorGridHalf) * (double)TradeMarauderConstants.MacroSectorSizeMeters);

                    uint flags = 0u;
                    if (((x * 31 + z * 17) % 41) == 0)
                        flags |= MarauderSectorFlags.LeviathanTerritory;
                    if (((x - 12) * (x - 12) + (z + 9) * (z + 9)) < 36)
                        flags |= MarauderSectorFlags.RichTitanium;
                    if (x == TradeMarauderConstants.SectorGridHalf && z == TradeMarauderConstants.SectorGridHalf)
                        flags |= MarauderSectorFlags.PlayerBase;

                    uint sectorHashValue = HashSector(x - TradeMarauderConstants.SectorGridHalf, z - TradeMarauderConstants.SectorGridHalf);
                    sectors[index] = new MarauderSectorEconomyDTO
                    {
                        SectorCentroidAup = aup,
                        Supply = (flags & MarauderSectorFlags.RichTitanium) != 0u ? 0.8f : 0.25f,
                        Demand = (flags & MarauderSectorFlags.PlayerBase) != 0u ? 0.9f : 0.2f,
                        Scarcity = 0.25f,
                        Threat = (flags & MarauderSectorFlags.LeviathanTerritory) != 0u ? 1f : 0.05f,
                        AggressionBias = 0.35f,
                        LootValue = ((x + z) & 7) == 0 ? 0.45f : 0f,
                        Flags = flags,
                        SectorHash = sectorHashValue,
                        DominantItemHash = TitaniumHash
                    };

                    if ((uint)index < (uint)sectorHash.Length)
                    {
                        sectorHash[index] = new MarauderSectorHashEntryDTO
                        {
                            SectorX = x - TradeMarauderConstants.SectorGridHalf,
                            SectorZ = z - TradeMarauderConstants.SectorGridHalf,
                            SectorHash = sectorHashValue,
                            SectorIndex = index,
                            Flags = flags
                        };
                    }
                }
            }

            for (int i = 0; i < routes.Length; i++)
                routes[i] = default;
            for (int i = 0; i < routeCounts.Length; i++)
                routeCounts[i] = 0;
            for (int i = 0; i < nodeStates.Length; i++)
                nodeStates[i] = 0;
            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = default;
            for (int i = 0; i < factionStanding.Length; i++)
                factionStanding[i] = 0f;
            for (int i = 0; i < mockInventoryHashes.Length; i++)
                mockInventoryHashes[i] = 0u;
            for (int i = 0; i < mockInventoryQuantities.Length; i++)
                mockInventoryQuantities[i] = 0;
            if (mockInventoryHashes.Length > 0)
                mockInventoryHashes[0] = CopperHash;
            if (mockInventoryQuantities.Length > 0)
                mockInventoryQuantities[0] = 800;
            for (int i = 0; i < counters.Length; i++)
                counters[i] = default;
            for (int i = 0; i < routePlans.Length; i++)
                routePlans[i] = default;
            for (int i = 0; i < visualProxies.Length; i++)
                visualProxies[i] = default;

            if (tuning.IsCreated && tuning.Length > 0)
            {
                tuning[0] = new MarauderTradeTuningDTO
                {
                    GlobalQualityWeight = 1f,
                    BasePriceVolatility = 0.35f,
                    MarauderSpawnRate = 0.55f,
                    TheftProbability = 0.18f,
                    AggressionScale = 0.35f,
                    LeviathanAggressionThreshold = 0.75f,
                    RouteReplanSeconds = 5f,
                    CachedGlobalScarcityIndex = 0.35f,
                    CopperHash = CopperHash,
                    TitaniumHash = TitaniumHash,
                    ActiveMarauders = TradeMarauderConstants.MaxMarauders,
                    ItemEvaluationLimit = TradeMarauderConstants.MaxEconomyItems,
                    MaxRouteSolves = 6,
                    MaxAStarIterations = 4096
                };
            }
        }

        private static uint HashSector(int x, int z)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)x) * 16777619u;
                hash = (hash ^ (uint)z) * 16777619u;
                return hash;
            }
        }

        private static uint ResolveDefaultItemHash(int index, uint quartzHash, uint glassHash, uint silverHash, uint lithiumHash)
        {
            switch (index % 6)
            {
                case 0: return CopperHash;
                case 1: return TitaniumHash;
                case 2: return quartzHash;
                case 3: return glassHash;
                case 4: return silverHash;
                default: return lithiumHash;
            }
        }

        private void ClearHandles()
        {
            _statesHandle = default;
            _inventoryHandle = default;
            _weightsHandle = default;
            _sectorsHandle = default;
            _routesHandle = default;
            _routeCountsHandle = default;
            _openHeapHandle = default;
            _gCostsHandle = default;
            _cameFromHandle = default;
            _nodeStatesHandle = default;
            _telemetryHandle = default;
            _tuningHandle = default;
            _factionStandingHandle = default;
            _mockInventoryHashesHandle = default;
            _mockInventoryQuantitiesHandle = default;
            _transactionScratchHandle = default;
            _acousticScratchHandle = default;
            _lootHandle = default;
            _sectorHashHandle = default;
            _csvScratchHandle = default;
            _countersHandle = default;
            _routePlansHandle = default;
            _visualProxyHandle = default;
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, ref _statesHandle);
            ReleaseVaultHandle(vault, ref _inventoryHandle);
            ReleaseVaultHandle(vault, ref _weightsHandle);
            ReleaseVaultHandle(vault, ref _sectorsHandle);
            ReleaseVaultHandle(vault, ref _routesHandle);
            ReleaseVaultHandle(vault, ref _routeCountsHandle);
            ReleaseVaultHandle(vault, ref _openHeapHandle);
            ReleaseVaultHandle(vault, ref _gCostsHandle);
            ReleaseVaultHandle(vault, ref _cameFromHandle);
            ReleaseVaultHandle(vault, ref _nodeStatesHandle);
            ReleaseVaultHandle(vault, ref _telemetryHandle);
            ReleaseVaultHandle(vault, ref _tuningHandle);
            ReleaseVaultHandle(vault, ref _factionStandingHandle);
            ReleaseVaultHandle(vault, ref _mockInventoryHashesHandle);
            ReleaseVaultHandle(vault, ref _mockInventoryQuantitiesHandle);
            ReleaseVaultHandle(vault, ref _transactionScratchHandle);
            ReleaseVaultHandle(vault, ref _acousticScratchHandle);
            ReleaseVaultHandle(vault, ref _lootHandle);
            ReleaseVaultHandle(vault, ref _sectorHashHandle);
            ReleaseVaultHandle(vault, ref _csvScratchHandle);
            ReleaseVaultHandle(vault, ref _countersHandle);
            ReleaseVaultHandle(vault, ref _routePlansHandle);
            ReleaseVaultHandle(vault, ref _visualProxyHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }
    }
}
