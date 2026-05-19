using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Inventory
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventorySlotDTO
    {
        [FieldOffset(0)] public uint ItemHashID;
        [FieldOffset(4)] public uint Quantity;
        [FieldOffset(8)] public ulong ContainerAUPHash;
        [FieldOffset(16)] public uint ConditionFlags;
        [FieldOffset(20)] public uint ReservedLock;
        [FieldOffset(24)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryQueryResultDTO
    {
        [FieldOffset(0)] public uint ItemHashID;
        [FieldOffset(4)] public int RequestedIndex;
        [FieldOffset(8)] public int Quantity;
        [FieldOffset(12)] public int SlotCount;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public int LastSlotIndex;
        [FieldOffset(24)] public ulong Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryTransactionRequestDTO
    {
        [FieldOffset(0)] public int SourceSlotIndex;
        [FieldOffset(4)] public int DestinationSlotIndex;
        [FieldOffset(8)] public uint ItemHashID;
        [FieldOffset(12)] public uint Quantity;
        [FieldOffset(16)] public uint TransactionId;
        [FieldOffset(20)] public uint ActorHash;
        [FieldOffset(24)] public uint FrameIndex;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryTransactionResultDTO
    {
        [FieldOffset(0)] public uint TransactionId;
        [FieldOffset(4)] public byte Status;
        [FieldOffset(5)] public byte Flags;
        [FieldOffset(6)] public ushort Reserved0;
        [FieldOffset(8)] public uint ItemHashID;
        [FieldOffset(12)] public uint QuantityMoved;
        [FieldOffset(16)] public int SourceSlotIndex;
        [FieldOffset(20)] public int DestinationSlotIndex;
        [FieldOffset(24)] public uint FrameIndex;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InventoryRoutingTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint ActiveSlots;
        [FieldOffset(8)] public uint EmptySlots;
        [FieldOffset(12)] public uint OrphanedSlots;
        [FieldOffset(16)] public uint QueryCount;
        [FieldOffset(20)] public uint TransactionCount;
        [FieldOffset(24)] public uint ConflictCount;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public ulong StateHash;
        [FieldOffset(40)] public float QueryTimeEstimateUs;
        [FieldOffset(44)] public float Fragmentation01;
        [FieldOffset(48)] public uint LastItemHash;
        [FieldOffset(52)] public uint LastContainerHashLo;
        [FieldOffset(56)] public ulong Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryRoutingTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public int SliceStart;
        [FieldOffset(8)] public int SliceCount;
        [FieldOffset(12)] public int ActiveSlotCount;
        [FieldOffset(16)] public float MaxQueryRadiusMeters;
        [FieldOffset(20)] public int MaxTransactionCASRetries;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct InventoryStackLimitDTO
    {
        [FieldOffset(0)] public uint ItemHashID;
        [FieldOffset(4)] public uint MaxStack;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LogisticsTransferSignal : ISignal
    {
        [FieldOffset(0)] public uint TransactionId;
        [FieldOffset(4)] public uint ItemHashID;
        [FieldOffset(8)] public uint Quantity;
        [FieldOffset(12)] public uint FrameIndex;
        [FieldOffset(16)] public ulong SourceAUPHash;
        [FieldOffset(24)] public ulong DestinationAUPHash;
        [FieldOffset(32)] public float3 VisualMidpoint;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint SourceSlotIndex;
        [FieldOffset(52)] public uint DestinationSlotIndex;
        [FieldOffset(56)] public ulong Reserved0;
    }

    public struct InventoryRoutingBufferHandles
    {
        public VaultBufferHandle<InventorySlotDTO> Slots;
        public VaultBufferHandle<int> ActiveSlotCount;
        public VaultBufferHandle<InventoryQueryResultDTO> QueryResults;
        public VaultBufferHandle<InventoryRoutingTelemetryEntry> TelemetryRing;
        public VaultBufferHandle<int> TelemetryCursor;
        public VaultBufferHandle<InventoryRoutingTuningDTO> Tuning;
        public VaultBufferHandle<InventorySlotDTO> UiSnapshotA;
        public VaultBufferHandle<InventorySlotDTO> UiSnapshotB;
        public VaultBufferHandle<InventoryStackLimitDTO> StackLimits;
    }

    public struct InventoryRoutingBuffers
    {
        public NativeArray<InventorySlotDTO> Slots;
        public NativeArray<int> ActiveSlotCount;
        public NativeArray<InventoryQueryResultDTO> QueryResults;
        public NativeArray<InventoryRoutingTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<InventoryRoutingTuningDTO> Tuning;
        public NativeArray<InventorySlotDTO> UiSnapshotA;
        public NativeArray<InventorySlotDTO> UiSnapshotB;
        public NativeArray<InventoryStackLimitDTO> StackLimits;
    }

    public enum InventoryTransactionStatus : byte
    {
        Success = 0,
        InvalidInput = 1,
        SourceEmpty = 2,
        ItemMismatch = 3,
        InsufficientQuantity = 4,
        DestinationOccupied = 5,
        DestinationOverflow = 6,
        AtomicConflict = 7
    }

    public static unsafe class InventoryRoutingNetwork
    {
        public const int InventorySlotDtoSizeBytes = 32;
        public const int InventoryQueryResultDtoSizeBytes = 32;
        public const int InventoryTransactionRequestDtoSizeBytes = 32;
        public const int InventoryTransactionResultDtoSizeBytes = 32;
        public const int InventoryTelemetryEntrySizeBytes = 64;
        public const int DefaultSlotCapacity = 100000;
        public const int DefaultQueryCapacity = 512;
        public const int TelemetryCapacity = 300;
        public const int StackLimitCapacity = 4096;
        public const int DefaultMinSliceSlots = 1024;
        public const int DefaultMaxSliceSlots = 65536;
        public const int DefaultIndexProbeLimit = 32;
        public const int ConditionQualityShift = 16;
        public const uint ConditionLowMask = 0x0000FFFFu;
        public const uint ConditionQualityMask = 0xFFFF0000u;
        public const uint ConditionPerishable = 1u << 0;
        public const uint ConditionDegraded = 1u << 1;
        public const uint ConditionOrphaned = 1u << 2;
        public const uint ConditionLocked = 1u << 3;
        public const uint TelemetryFlagFatal = 1u << 0;
        public const uint TelemetryFlagFragmented = 1u << 1;
        public const uint TelemetryFlagToasterSliced = 1u << 2;
        public const uint SignalLaneHash = 0x5349314Cu; // SI1L
        public const uint DumpMagic = 0x494E5652u; // INVR
        public const uint DumpVersion = 1u;
        public const string DumpPath = "Docs/AgentLogs/Dump_INVENTORY_ROUTER.bin";

        public const int InventorySlotDTO_ItemHashID = 0;
        public const int InventorySlotDTO_Quantity = 4;
        public const int InventorySlotDTO_ContainerAUPHash = 8;
        public const int InventorySlotDTO_ConditionFlags = 16;
        public const int InventorySlotDTO_ReservedLock = 20;

        private const double AupHashInvMeters = 4.0d;
        private const double AupHashMeters = 0.25d;
        private const long AupQuantizedBias = 1L << 20;
        private const long AupQuantizedMin = -(1L << 20);
        private const long AupQuantizedMax = (1L << 20) - 1L;
        private const ulong AupAxisMask = (1UL << 21) - 1UL;
        internal const uint FnvOffset = 2166136261u;
        internal const uint FnvPrime = 16777619u;

        public static bool RuntimeLayoutValid()
        {
            return UnsafeUtility.SizeOf<InventorySlotDTO>() == InventorySlotDtoSizeBytes &&
                   OffsetOf<InventorySlotDTO>(nameof(InventorySlotDTO.ItemHashID)) == InventorySlotDTO_ItemHashID &&
                   OffsetOf<InventorySlotDTO>(nameof(InventorySlotDTO.Quantity)) == InventorySlotDTO_Quantity &&
                   OffsetOf<InventorySlotDTO>(nameof(InventorySlotDTO.ContainerAUPHash)) == InventorySlotDTO_ContainerAUPHash &&
                   OffsetOf<InventorySlotDTO>(nameof(InventorySlotDTO.ConditionFlags)) == InventorySlotDTO_ConditionFlags &&
                   OffsetOf<InventorySlotDTO>(nameof(InventorySlotDTO.ReservedLock)) == InventorySlotDTO_ReservedLock &&
                   UnsafeUtility.SizeOf<InventoryQueryResultDTO>() == InventoryQueryResultDtoSizeBytes &&
                   UnsafeUtility.SizeOf<InventoryTransactionRequestDTO>() == InventoryTransactionRequestDtoSizeBytes &&
                   UnsafeUtility.SizeOf<InventoryTransactionResultDTO>() == InventoryTransactionResultDtoSizeBytes &&
                   UnsafeUtility.SizeOf<InventoryRoutingTelemetryEntry>() == InventoryTelemetryEntrySizeBytes &&
                   UnsafeUtility.SizeOf<InventoryRoutingTuningDTO>() == 32 &&
                   UnsafeUtility.SizeOf<InventoryStackLimitDTO>() == 16 &&
                   UnsafeUtility.SizeOf<LogisticsTransferSignal>() == 64;
        }

        public static void ValidateRuntimeLayoutOrThrow()
        {
            if (!RuntimeLayoutValid())
                throw new InvalidOperationException("InventoryRoutingNetwork DTO layout mismatch.");
        }

        public static InventoryRoutingBufferHandles EnsureBuffers(
            IDataVault vault,
            int slotCapacity = DefaultSlotCapacity,
            int queryCapacity = DefaultQueryCapacity,
            int stackLimitCapacity = StackLimitCapacity)
        {
            InventoryRoutingBufferHandles handles = default;
            if (vault == null)
                return handles;

            ValidateRuntimeLayoutOrThrow();
            slotCapacity = math.max(1, slotCapacity);
            queryCapacity = math.max(1, queryCapacity);
            stackLimitCapacity = math.max(1, stackLimitCapacity);

            handles.Slots = vault.GetBufferHandle<InventorySlotDTO>(
                BufferID.ShinobuInventorySlots,
                slotCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            handles.ActiveSlotCount = vault.GetBufferHandle<int>(
                BufferID.ShinobuInventoryActiveSlotCount,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.QueryResults = vault.GetBufferHandle<InventoryQueryResultDTO>(
                BufferID.ShinobuInventoryQueryResults,
                queryCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryRing = vault.GetBufferHandle<InventoryRoutingTelemetryEntry>(
                BufferID.ShinobuInventoryRoutingTelemetry,
                TelemetryCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.GetBufferHandle<int>(
                BufferID.ShinobuInventoryDumpScratch,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.Tuning = vault.GetBufferHandle<InventoryRoutingTuningDTO>(
                BufferID.ShinobuInventoryRoutingTuning,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.UiSnapshotA = vault.GetBufferHandle<InventorySlotDTO>(
                BufferID.ShinobuInventoryUiSnapshotA,
                slotCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            handles.UiSnapshotB = vault.GetBufferHandle<InventorySlotDTO>(
                BufferID.ShinobuInventoryUiSnapshotB,
                slotCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            handles.StackLimits = vault.GetBufferHandle<InventoryStackLimitDTO>(
                BufferID.ShinobuInventoryStackLimits,
                stackLimitCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);

            EnsureSignalLane();
            return handles;
        }

        public static bool TryResolveBuffers(
            IDataVault vault,
            ref InventoryRoutingBufferHandles handles,
            out InventoryRoutingBuffers buffers)
        {
            buffers = default;
            if (vault == null)
                return false;

            buffers.Slots = handles.Slots.Resolve(vault);
            buffers.ActiveSlotCount = handles.ActiveSlotCount.Resolve(vault);
            buffers.QueryResults = handles.QueryResults.Resolve(vault);
            buffers.TelemetryRing = handles.TelemetryRing.Resolve(vault);
            buffers.TelemetryCursor = handles.TelemetryCursor.Resolve(vault);
            buffers.Tuning = handles.Tuning.Resolve(vault);
            buffers.UiSnapshotA = handles.UiSnapshotA.Resolve(vault);
            buffers.UiSnapshotB = handles.UiSnapshotB.Resolve(vault);
            buffers.StackLimits = handles.StackLimits.Resolve(vault);

            return buffers.Slots.IsCreated &&
                   buffers.ActiveSlotCount.IsCreated &&
                   buffers.QueryResults.IsCreated &&
                   buffers.TelemetryRing.IsCreated &&
                   buffers.TelemetryCursor.IsCreated &&
                   buffers.Tuning.IsCreated &&
                   buffers.UiSnapshotA.IsCreated &&
                   buffers.UiSnapshotB.IsCreated &&
                   buffers.StackLimits.IsCreated;
        }

        public static void EnsureSignalLane()
        {
            SignalBus<LogisticsTransferSignal>.Configure(
                128,
                maxFrameSignals: 512,
                lowTierFrameSignals: 32,
                laneHash: SignalLaneHash);
            SignalBus<LogisticsTransferSignal>.EnsureInitialized();
        }

        public static int ResolveTimeSliceBatchSize(float globalQualityWeight, int totalSlots, int minSlots = DefaultMinSliceSlots, int maxSlots = DefaultMaxSliceSlots)
        {
            if (totalSlots <= 0)
                return 0;

            float weight = math.saturate(globalQualityWeight);
            float eased = weight * weight * (3f - 2f * weight);
            int minBudget = math.max(1, minSlots);
            int maxBudget = math.max(minBudget, maxSlots);
            int budget = (int)math.round(math.lerp(minBudget, maxBudget, eased));
            return math.clamp(budget, 1, totalSlots);
        }

        public static uint PackConditionFlags(ushort qualityMilli, uint lowFlags)
        {
            uint quality = (uint)math.min((int)qualityMilli, 1000);
            return (quality << ConditionQualityShift) | (lowFlags & ConditionLowMask);
        }

        public static ushort ReadConditionQualityMilli(uint conditionFlags)
        {
            return (ushort)((conditionFlags & ConditionQualityMask) >> ConditionQualityShift);
        }

        public static ulong PackAupHash(double3 aup)
        {
            ulong x = PackAupAxis(aup.x);
            ulong y = PackAupAxis(aup.y);
            ulong z = PackAupAxis(aup.z);
            return x | (y << 21) | (z << 42);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 DecodeAupHash(ulong hash)
        {
            long x = (long)(hash & AupAxisMask) - AupQuantizedBias;
            long y = (long)((hash >> 21) & AupAxisMask) - AupQuantizedBias;
            long z = (long)((hash >> 42) & AupAxisMask) - AupQuantizedBias;
            return new double3(x * AupHashMeters, y * AupHashMeters, z * AupHashMeters);
        }

        public static float ComputeFragmentation01(NativeArray<InventorySlotDTO> slots, int activeHint)
        {
            if (!slots.IsCreated || slots.Length == 0)
                return 0f;

            int scanCount = activeHint > 0 ? math.min(activeHint, slots.Length) : slots.Length;
            int holesBeforeLastActive = 0;
            bool seenEmpty = false;
            for (int i = 0; i < scanCount; i++)
            {
                InventorySlotDTO slot = slots[i];
                bool active = slot.ItemHashID != 0u && slot.Quantity != 0u;
                if (!active)
                {
                    seenEmpty = true;
                    continue;
                }

                if (seenEmpty)
                    holesBeforeLastActive++;
            }

            return scanCount > 0 ? math.saturate(holesBeforeLastActive / (float)scanCount) : 0f;
        }

        public static ulong ComputeStateHash(NativeArray<InventorySlotDTO> slots, int activeHint)
        {
            if (!slots.IsCreated)
                return 0UL;

            int count = activeHint > 0 ? math.min(activeHint, slots.Length) : slots.Length;
            ulong hash = 1469598103934665603UL;
            for (int i = 0; i < count; i++)
            {
                InventorySlotDTO slot = slots[i];
                hash ^= slot.ItemHashID;
                hash *= 1099511628211UL;
                hash ^= slot.Quantity;
                hash *= 1099511628211UL;
                hash ^= slot.ContainerAUPHash;
                hash *= 1099511628211UL;
                hash ^= slot.ConditionFlags;
                hash *= 1099511628211UL;
            }

            return hash;
        }

        public static bool TryLookupIndexedTotal(
            NativeArray<int> indexKeys,
            NativeArray<int> indexTotals,
            uint itemHashId,
            out int quantity)
        {
            quantity = 0;
            if (!indexKeys.IsCreated || !indexTotals.IsCreated || itemHashId == 0u || indexKeys.Length == 0 || indexTotals.Length < indexKeys.Length)
                return false;

            int capacity = indexKeys.Length;
            int start = HashToIndex(itemHashId, capacity);
            int key = unchecked((int)itemHashId);
            for (int probe = 0; probe < math.min(capacity, DefaultIndexProbeLimit); probe++)
            {
                int index = start + probe;
                if (index >= capacity)
                    index -= capacity;

                int observed = indexKeys[index];
                if (observed == key)
                {
                    quantity = math.max(0, indexTotals[index]);
                    return true;
                }

                if (observed == 0)
                    return false;
            }

            return false;
        }

        public static void WriteTelemetryDump(string relativePath, NativeArray<InventoryRoutingTelemetryEntry> telemetry, NativeArray<int> cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return;

            string path = string.IsNullOrWhiteSpace(relativePath) ? DumpPath : relativePath;
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            uint[] header =
            {
                DumpMagic,
                DumpVersion,
                (uint)telemetry.Length,
                (uint)UnsafeUtility.SizeOf<InventoryRoutingTelemetryEntry>(),
                cursor.IsCreated && cursor.Length > 0 ? unchecked((uint)cursor[0]) : 0u,
                (uint)DateTime.UtcNow.Ticks,
                (uint)(DateTime.UtcNow.Ticks >> 32),
                0u
            };

            fixed (uint* headerPtr = header)
            {
                stream.Write(new ReadOnlySpan<byte>(headerPtr, header.Length * sizeof(uint)));
            }

            void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            int byteCount = telemetry.Length * UnsafeUtility.SizeOf<InventoryRoutingTelemetryEntry>();
            stream.Write(new ReadOnlySpan<byte>(source, byteCount));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int HashToIndex(uint hash, int capacity)
        {
            if (capacity <= 1)
                return 0;

            uint mixed = hash;
            mixed ^= mixed >> 16;
            mixed *= 0x7FEB352Du;
            mixed ^= mixed >> 15;
            mixed *= 0x846CA68Bu;
            mixed ^= mixed >> 16;
            return (int)(mixed % (uint)capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AtomicAdd(NativeArray<int> values, int index, int delta)
        {
            if (!values.IsCreated || (uint)index >= (uint)values.Length || delta == 0)
                return;

            int* ptr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(values);
            Interlocked.Add(ref UnsafeUtility.AsRef<int>(ptr + index), delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool PassesAupGate(ulong containerAupHash, double3 queryAup, float maxDistanceMeters)
        {
            if (maxDistanceMeters <= 0f)
                return true;

            double3 containerAup = DecodeAupHash(containerAupHash);
            double3 deltaDouble = containerAup - queryAup;
            float3 delta = new float3((float)deltaDouble.x, (float)deltaDouble.y, (float)deltaDouble.z);
            return math.lengthsq(delta) <= maxDistanceMeters * maxDistanceMeters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong PackAupAxis(double value)
        {
            long quantized = (long)math.round(value * AupHashInvMeters);
            if (quantized < AupQuantizedMin)
                quantized = AupQuantizedMin;
            if (quantized > AupQuantizedMax)
                quantized = AupQuantizedMax;

            return (ulong)(quantized + AupQuantizedBias) & AupAxisMask;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockLogisticsNetworkJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<InventorySlotDTO> Slots;
        public int SlotCount;
        public double3 OriginAUP;
        public uint Seed;

        public void Execute(int index)
        {
            if (!Slots.IsCreated || (uint)index >= (uint)Slots.Length || index >= SlotCount)
                return;

            uint salt = Seed == 0u ? 0x5348494Eu : Seed;
            bool empty = (index % 13) == 0;
            uint itemHash = empty ? 0u : 0x1000u + (uint)((index * 2654435761u + salt) & 255u);
            uint quantity = empty ? 0u : (uint)(1 + ((index * 17) & 63));
            double3 offset = new double3((index % 317) * 2.0d, ((index / 317) % 19) * 0.5d, (index % 91) * -1.75d);

            Slots[index] = new InventorySlotDTO
            {
                ItemHashID = itemHash,
                Quantity = quantity,
                ContainerAUPHash = InventoryRoutingNetwork.PackAupHash(OriginAUP + offset),
                ConditionFlags = InventoryRoutingNetwork.PackConditionFlags(1000, (index & 7) == 0 ? InventoryRoutingNetwork.ConditionPerishable : 0u),
                ReservedLock = 0u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ZeroInitializeInventorySlotsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<InventorySlotDTO> Slots;
        public int SlotCount;

        public void Execute(int index)
        {
            if (!Slots.IsCreated || (uint)index >= (uint)Slots.Length || index >= SlotCount)
                return;

            InventorySlotDTO slot = Slots[index];
            slot.ItemHashID = 0u;
            slot.Quantity = 0u;
            slot.ConditionFlags = 0u;
            slot.ReservedLock = 0u;
            Slots[index] = slot;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ClearIntArrayJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<int> Values;

        public void Execute(int index)
        {
            if (Values.IsCreated && (uint)index < (uint)Values.Length)
                Values[index] = 0;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct AggregateAvailableResourcesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<InventorySlotDTO> Slots;
        [ReadOnly, NoAlias] public NativeArray<uint> RequestedItemHashIds;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> RequestedTotals;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> RequestedSlotCounts;
        public double3 QueryAUP;
        public float MaxDistanceMeters;
        public int SlotStart;
        public int SlotCount;

        public void Execute(int jobIndex)
        {
            int slotIndex = SlotStart + jobIndex;
            if (!Slots.IsCreated ||
                !RequestedItemHashIds.IsCreated ||
                !RequestedTotals.IsCreated ||
                (uint)slotIndex >= (uint)Slots.Length ||
                jobIndex >= SlotCount)
            {
                return;
            }

            InventorySlotDTO slot = Slots[slotIndex];
            if (slot.ItemHashID == 0u || slot.Quantity == 0u || slot.ReservedLock != 0u)
                return;
            if (!InventoryRoutingNetwork.PassesAupGate(slot.ContainerAUPHash, QueryAUP, MaxDistanceMeters))
                return;

            int quantity = slot.Quantity > int.MaxValue ? int.MaxValue : (int)slot.Quantity;
            for (int requestIndex = 0; requestIndex < RequestedItemHashIds.Length && requestIndex < RequestedTotals.Length; requestIndex++)
            {
                if (RequestedItemHashIds[requestIndex] != slot.ItemHashID)
                    continue;

                InventoryRoutingNetwork.AtomicAdd(RequestedTotals, requestIndex, quantity);
                InventoryRoutingNetwork.AtomicAdd(RequestedSlotCounts, requestIndex, 1);
                return;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildResourceHashIndexJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<InventorySlotDTO> Slots;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> IndexKeys;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> IndexTotals;
        public double3 QueryAUP;
        public float MaxDistanceMeters;
        public int SlotStart;
        public int SlotCount;

        public void Execute(int jobIndex)
        {
            int slotIndex = SlotStart + jobIndex;
            if (!Slots.IsCreated ||
                !IndexKeys.IsCreated ||
                !IndexTotals.IsCreated ||
                IndexTotals.Length < IndexKeys.Length ||
                (uint)slotIndex >= (uint)Slots.Length ||
                jobIndex >= SlotCount)
            {
                return;
            }

            InventorySlotDTO slot = Slots[slotIndex];
            if (slot.ItemHashID == 0u || slot.Quantity == 0u || slot.ReservedLock != 0u)
                return;
            if (!InventoryRoutingNetwork.PassesAupGate(slot.ContainerAUPHash, QueryAUP, MaxDistanceMeters))
                return;

            int capacity = IndexKeys.Length;
            int key = unchecked((int)slot.ItemHashID);
            int start = InventoryRoutingNetwork.HashToIndex(slot.ItemHashID, capacity);
            int quantity = slot.Quantity > int.MaxValue ? int.MaxValue : (int)slot.Quantity;
            int* keys = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(IndexKeys);

            for (int probe = 0; probe < math.min(capacity, InventoryRoutingNetwork.DefaultIndexProbeLimit); probe++)
            {
                int index = start + probe;
                if (index >= capacity)
                    index -= capacity;

                int observed = Volatile.Read(ref UnsafeUtility.AsRef<int>(keys + index));
                if (observed == key)
                {
                    InventoryRoutingNetwork.AtomicAdd(IndexTotals, index, quantity);
                    return;
                }

                if (observed != 0)
                    continue;

                int previous = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(keys + index), key, 0);
                if (previous == 0 || previous == key)
                {
                    InventoryRoutingNetwork.AtomicAdd(IndexTotals, index, quantity);
                    return;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct LookupResourceHashIndexJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<int> IndexKeys;
        [ReadOnly, NoAlias] public NativeArray<int> IndexTotals;
        [ReadOnly, NoAlias] public NativeArray<uint> RequestedItemHashIds;
        [NoAlias] public NativeArray<InventoryQueryResultDTO> Results;

        public void Execute(int index)
        {
            if (!RequestedItemHashIds.IsCreated ||
                !Results.IsCreated ||
                (uint)index >= (uint)RequestedItemHashIds.Length ||
                (uint)index >= (uint)Results.Length)
            {
                return;
            }

            uint hash = RequestedItemHashIds[index];
            int quantity;
            bool found = InventoryRoutingNetwork.TryLookupIndexedTotal(IndexKeys, IndexTotals, hash, out quantity);
            Results[index] = new InventoryQueryResultDTO
            {
                ItemHashID = hash,
                RequestedIndex = index,
                Quantity = found ? quantity : 0,
                SlotCount = 0,
                Flags = found ? 1u : 0u,
                LastSlotIndex = -1
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InventoryTransactionJob : IJob
    {
        [NoAlias] public NativeArray<InventorySlotDTO> Slots;
        [ReadOnly, NoAlias] public NativeArray<InventoryTransactionRequestDTO> Requests;
        [NoAlias] public NativeArray<InventoryTransactionResultDTO> Results;
        public NativeQueue<LogisticsTransferSignal>.ParallelWriter TransferSignalWriter;
        public int RequestCount;
        public int EmitTransferSignals;

        public void Execute()
        {
            if (!Slots.IsCreated || !Requests.IsCreated || !Results.IsCreated)
                return;

            InventorySlotDTO* slots = (InventorySlotDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Slots);
            InventoryTransactionResultDTO* results = (InventoryTransactionResultDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Results);
            int count = math.min(RequestCount, math.min(Requests.Length, Results.Length));
            for (int i = 0; i < count; i++)
                ExecuteRequest(slots, Requests[i], ref UnsafeUtility.AsRef<InventoryTransactionResultDTO>(results + i));
        }

        private void ExecuteRequest(InventorySlotDTO* slots, InventoryTransactionRequestDTO request, ref InventoryTransactionResultDTO result)
        {
            result = new InventoryTransactionResultDTO
            {
                TransactionId = request.TransactionId,
                ItemHashID = request.ItemHashID,
                SourceSlotIndex = request.SourceSlotIndex,
                DestinationSlotIndex = request.DestinationSlotIndex,
                FrameIndex = request.FrameIndex
            };

            if ((uint)request.SourceSlotIndex >= (uint)Slots.Length ||
                (uint)request.DestinationSlotIndex >= (uint)Slots.Length ||
                request.ItemHashID == 0u ||
                request.Quantity == 0u ||
                request.SourceSlotIndex == request.DestinationSlotIndex)
            {
                result.Status = (byte)InventoryTransactionStatus.InvalidInput;
                return;
            }

            int first = math.min(request.SourceSlotIndex, request.DestinationSlotIndex);
            int second = math.max(request.SourceSlotIndex, request.DestinationSlotIndex);
            int lockValue = unchecked((int)(request.TransactionId == 0u ? 0x53480001u : request.TransactionId));

            if (!TryAcquire(slots, first, lockValue))
            {
                result.Status = (byte)InventoryTransactionStatus.AtomicConflict;
                return;
            }

            bool secondAcquired = TryAcquire(slots, second, lockValue);
            if (!secondAcquired)
            {
                Release(slots, first);
                result.Status = (byte)InventoryTransactionStatus.AtomicConflict;
                return;
            }

            InventorySlotDTO source = slots[request.SourceSlotIndex];
            InventorySlotDTO destination = slots[request.DestinationSlotIndex];
            if (source.ItemHashID == 0u || source.Quantity == 0u)
            {
                result.Status = (byte)InventoryTransactionStatus.SourceEmpty;
            }
            else if (source.ItemHashID != request.ItemHashID)
            {
                result.Status = (byte)InventoryTransactionStatus.ItemMismatch;
            }
            else if (source.Quantity < request.Quantity)
            {
                result.Status = (byte)InventoryTransactionStatus.InsufficientQuantity;
            }
            else if (destination.ItemHashID != 0u && destination.ItemHashID != request.ItemHashID)
            {
                result.Status = (byte)InventoryTransactionStatus.DestinationOccupied;
            }
            else if (uint.MaxValue - destination.Quantity < request.Quantity)
            {
                result.Status = (byte)InventoryTransactionStatus.DestinationOverflow;
            }
            else
            {
                uint movedConditionFlags = source.ConditionFlags;
                source.Quantity -= request.Quantity;
                if (source.Quantity == 0u)
                {
                    source.ItemHashID = 0u;
                    source.ConditionFlags = 0u;
                }

                if (destination.ItemHashID == 0u)
                {
                    destination.ItemHashID = request.ItemHashID;
                    destination.ConditionFlags = movedConditionFlags;
                    if (destination.ContainerAUPHash == 0UL)
                        destination.ContainerAUPHash = source.ContainerAUPHash;
                }

                destination.Quantity += request.Quantity;
                result.Status = (byte)InventoryTransactionStatus.Success;
                result.QuantityMoved = request.Quantity;

                if (EmitTransferSignals != 0)
                {
                    double3 sourceAup = InventoryRoutingNetwork.DecodeAupHash(source.ContainerAUPHash);
                    double3 destinationAup = InventoryRoutingNetwork.DecodeAupHash(destination.ContainerAUPHash);
                    double3 midpoint = (sourceAup + destinationAup) * 0.5d;
                    TransferSignalWriter.Enqueue(new LogisticsTransferSignal
                    {
                        TransactionId = request.TransactionId,
                        ItemHashID = request.ItemHashID,
                        Quantity = request.Quantity,
                        FrameIndex = request.FrameIndex,
                        SourceAUPHash = source.ContainerAUPHash,
                        DestinationAUPHash = destination.ContainerAUPHash,
                        VisualMidpoint = new float3((float)midpoint.x, (float)midpoint.y, (float)midpoint.z),
                        Flags = request.Flags,
                        SourceSlotIndex = unchecked((uint)request.SourceSlotIndex),
                        DestinationSlotIndex = unchecked((uint)request.DestinationSlotIndex)
                    });
                }

                slots[request.SourceSlotIndex] = source;
                slots[request.DestinationSlotIndex] = destination;
            }

            Release(slots, second);
            Release(slots, first);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryAcquire(InventorySlotDTO* slots, int index, int lockValue)
        {
            int* lockPtr = (int*)UnsafeUtility.AddressOf(ref slots[index].ReservedLock);
            return Interlocked.CompareExchange(ref UnsafeUtility.AsRef<int>(lockPtr), lockValue, 0) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Release(InventorySlotDTO* slots, int index)
        {
            int* lockPtr = (int*)UnsafeUtility.AddressOf(ref slots[index].ReservedLock);
            Interlocked.Exchange(ref UnsafeUtility.AsRef<int>(lockPtr), 0);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct TickInventoryDecayJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<InventorySlotDTO> Slots;
        public ushort DecayMilli;
        public int SlotStart;
        public int SlotCount;

        public void Execute(int jobIndex)
        {
            int slotIndex = SlotStart + jobIndex;
            if (!Slots.IsCreated || (uint)slotIndex >= (uint)Slots.Length || jobIndex >= SlotCount)
                return;

            InventorySlotDTO slot = Slots[slotIndex];
            if (slot.ItemHashID == 0u || slot.Quantity == 0u || (slot.ConditionFlags & InventoryRoutingNetwork.ConditionPerishable) == 0u)
                return;

            uint flags = slot.ConditionFlags & InventoryRoutingNetwork.ConditionLowMask;
            uint quality = (slot.ConditionFlags & InventoryRoutingNetwork.ConditionQualityMask) >> InventoryRoutingNetwork.ConditionQualityShift;
            uint decay = math.max(1u, DecayMilli);
            quality = quality > decay ? quality - decay : 0u;
            if (quality == 0u)
                flags |= InventoryRoutingNetwork.ConditionDegraded;

            slot.ConditionFlags = (quality << InventoryRoutingNetwork.ConditionQualityShift) | flags;
            Slots[slotIndex] = slot;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct PublishInventorySnapshotJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<InventorySlotDTO> SourceSlots;
        [NoAlias] public NativeArray<InventorySlotDTO> SnapshotSlots;
        public int SlotCount;

        public void Execute(int index)
        {
            if (!SourceSlots.IsCreated || !SnapshotSlots.IsCreated || (uint)index >= (uint)SnapshotSlots.Length)
                return;

            SnapshotSlots[index] = index < SlotCount && index < SourceSlots.Length ? SourceSlots[index] : default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CompactInventoryArrayJob : IJob
    {
        [NoAlias] public NativeArray<InventorySlotDTO> Slots;
        [NoAlias] public NativeArray<int> ActiveSlotCount;
        public int SlotLimit;

        public void Execute()
        {
            if (!Slots.IsCreated)
                return;

            int limit = SlotLimit > 0 ? math.min(SlotLimit, Slots.Length) : Slots.Length;
            int write = 0;
            for (int read = 0; read < limit; read++)
            {
                InventorySlotDTO slot = Slots[read];
                bool active = slot.ItemHashID != 0u && slot.Quantity != 0u;
                if (!active)
                    continue;

                if (write != read)
                    Slots[write] = slot;
                write++;
            }

            InventorySlotDTO empty = default;
            for (int i = write; i < limit; i++)
                Slots[i] = empty;

            if (ActiveSlotCount.IsCreated && ActiveSlotCount.Length > 0)
                ActiveSlotCount[0] = write;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordInventoryTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<InventorySlotDTO> Slots;
        [NoAlias] public NativeArray<InventoryRoutingTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        [NoAlias] public NativeArray<int> ActiveSlotCount;
        public uint FrameIndex;
        public uint QueryCount;
        public uint TransactionCount;
        public uint ConflictCount;
        public float QueryTimeEstimateUs;

        public void Execute()
        {
            if (!Slots.IsCreated || !TelemetryRing.IsCreated || TelemetryRing.Length == 0)
                return;

            uint active = 0u;
            uint empty = 0u;
            uint orphaned = 0u;
            uint flags = 0u;
            ulong stateHash = 1469598103934665603UL;
            uint lastHash = 0u;
            uint lastContainerLo = 0u;
            bool seenEmpty = false;
            for (int i = 0; i < Slots.Length; i++)
            {
                InventorySlotDTO slot = Slots[i];
                bool hasHash = slot.ItemHashID != 0u;
                bool hasQuantity = slot.Quantity != 0u;
                if (!hasHash && !hasQuantity)
                {
                    empty++;
                    seenEmpty = true;
                    continue;
                }

                if (hasHash != hasQuantity)
                    flags |= InventoryRoutingNetwork.TelemetryFlagFatal;
                if (seenEmpty)
                    orphaned++;
                if ((slot.ConditionFlags & InventoryRoutingNetwork.ConditionOrphaned) != 0u)
                    orphaned++;

                active++;
                lastHash = slot.ItemHashID;
                lastContainerLo = unchecked((uint)slot.ContainerAUPHash);
                stateHash ^= slot.ItemHashID;
                stateHash *= 1099511628211UL;
                stateHash ^= slot.Quantity;
                stateHash *= 1099511628211UL;
                stateHash ^= slot.ContainerAUPHash;
                stateHash *= 1099511628211UL;
                stateHash ^= slot.ConditionFlags;
                stateHash *= 1099511628211UL;
            }

            float fragmentation = Slots.Length > 0 ? math.saturate(orphaned / (float)Slots.Length) : 0f;
            if (fragmentation > 0.05f)
                flags |= InventoryRoutingNetwork.TelemetryFlagFragmented;

            int cursor = 0;
            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
            {
                cursor = TelemetryCursor[0];
                TelemetryCursor[0] = (cursor + 1) % TelemetryRing.Length;
            }

            TelemetryRing[cursor % TelemetryRing.Length] = new InventoryRoutingTelemetryEntry
            {
                FrameIndex = FrameIndex,
                ActiveSlots = active,
                EmptySlots = empty,
                OrphanedSlots = orphaned,
                QueryCount = QueryCount,
                TransactionCount = TransactionCount,
                ConflictCount = ConflictCount,
                Flags = flags,
                StateHash = stateHash,
                QueryTimeEstimateUs = QueryTimeEstimateUs,
                Fragmentation01 = fragmentation,
                LastItemHash = lastHash,
                LastContainerHashLo = lastContainerLo
            };

            if (ActiveSlotCount.IsCreated && ActiveSlotCount.Length > 0)
                ActiveSlotCount[0] = unchecked((int)active);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CsvItemLimitsIngestJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<byte> CsvUtf8;
        [NoAlias] public NativeArray<InventoryStackLimitDTO> StackLimits;
        [NoAlias] public NativeArray<int> ResultCounters;
        public int ByteCount;

        public void Execute()
        {
            if (!CsvUtf8.IsCreated || !StackLimits.IsCreated || StackLimits.Length == 0)
                return;

            int length = math.min(ByteCount > 0 ? ByteCount : CsvUtf8.Length, CsvUtf8.Length);
            int offset = 0;
            int written = 0;
            int rejected = 0;

            while (offset < length && written < StackLimits.Length)
            {
                SkipLineWhitespace(ref offset, length);
                if (offset >= length)
                    break;

                if (CsvUtf8[offset] == (byte)'#')
                {
                    SkipLine(ref offset, length);
                    continue;
                }

                uint hash = ParseHashToken(ref offset, length);
                SkipDelimiter(ref offset, length);
                uint maxStack = ParseUInt(ref offset, length);
                SkipDelimiter(ref offset, length);
                uint flags = ParseUInt(ref offset, length);
                SkipLine(ref offset, length);

                if (hash == 0u || maxStack == 0u)
                {
                    rejected++;
                    continue;
                }

                StackLimits[written++] = new InventoryStackLimitDTO
                {
                    ItemHashID = hash,
                    MaxStack = maxStack,
                    Flags = flags
                };
            }

            if (ResultCounters.IsCreated && ResultCounters.Length >= 2)
            {
                ResultCounters[0] = written;
                ResultCounters[1] = rejected;
            }
        }

        private void SkipLineWhitespace(ref int offset, int length)
        {
            while (offset < length)
            {
                byte b = CsvUtf8[offset];
                if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\r' && b != (byte)'\n')
                    return;
                offset++;
            }
        }

        private void SkipDelimiter(ref int offset, int length)
        {
            while (offset < length)
            {
                byte b = CsvUtf8[offset];
                if (b == (byte)',' || b == (byte)';' || b == (byte)'\t')
                {
                    offset++;
                    return;
                }

                if (b == (byte)' ' || b == (byte)'\r')
                {
                    offset++;
                    continue;
                }

                return;
            }
        }

        private void SkipLine(ref int offset, int length)
        {
            while (offset < length && CsvUtf8[offset] != (byte)'\n')
                offset++;
            if (offset < length)
                offset++;
        }

        private uint ParseHashToken(ref int offset, int length)
        {
            int start = offset;
            uint numeric = ParseUInt(ref offset, length);
            if (offset > start)
                return numeric;

            uint hash = InventoryRoutingNetwork.FnvOffset;
            while (offset < length)
            {
                byte b = CsvUtf8[offset];
                if (b == (byte)',' || b == (byte)';' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n')
                    break;
                if (b != (byte)' ')
                {
                    hash ^= b;
                    hash *= InventoryRoutingNetwork.FnvPrime;
                }
                offset++;
            }

            return hash == 0u ? 1u : hash;
        }

        private uint ParseUInt(ref int offset, int length)
        {
            while (offset < length && (CsvUtf8[offset] == (byte)' ' || CsvUtf8[offset] == (byte)'\t'))
                offset++;

            bool hex = offset + 1 < length &&
                       CsvUtf8[offset] == (byte)'0' &&
                       (CsvUtf8[offset + 1] == (byte)'x' || CsvUtf8[offset + 1] == (byte)'X');
            if (hex)
                offset += 2;

            uint value = 0u;
            bool parsed = false;
            while (offset < length)
            {
                byte b = CsvUtf8[offset];
                uint digit;
                if (b >= (byte)'0' && b <= (byte)'9')
                    digit = (uint)(b - (byte)'0');
                else if (hex && b >= (byte)'a' && b <= (byte)'f')
                    digit = (uint)(10 + b - (byte)'a');
                else if (hex && b >= (byte)'A' && b <= (byte)'F')
                    digit = (uint)(10 + b - (byte)'A');
                else
                    break;

                value = hex ? (value << 4) + digit : value * 10u + digit;
                parsed = true;
                offset++;
            }

            return parsed ? value : 0u;
        }
    }
}
