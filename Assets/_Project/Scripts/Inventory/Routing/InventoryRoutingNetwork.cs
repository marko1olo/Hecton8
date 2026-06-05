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
    public struct InventoryQueryResultDTO
    {
        [FieldOffset(0)] public uint ItemHashID;
        [FieldOffset(4)] public int RequestedIndex;
        [FieldOffset(8)] public int Quantity;
        [FieldOffset(12)] public int SlotCount;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public int LastSlotIndex;
        [FieldOffset(24)] private byte _pad0;
        [FieldOffset(25)] private byte _pad1;
        [FieldOffset(26)] private byte _pad2;
        [FieldOffset(27)] private byte _pad3;
        [FieldOffset(28)] private byte _pad4;
        [FieldOffset(29)] private byte _pad5;
        [FieldOffset(30)] private byte _pad6;
        [FieldOffset(31)] private byte _pad7;
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
        [FieldOffset(4)] public uint ItemHashID;
        [FieldOffset(8)] public uint QuantityMoved;
        [FieldOffset(12)] public int SourceSlotIndex;
        [FieldOffset(16)] public int DestinationSlotIndex;
        [FieldOffset(20)] public uint FrameIndex;
        [FieldOffset(24)] public uint Reserved1;
        [FieldOffset(28)] public ushort Reserved0;
        [FieldOffset(30)] public byte Status;
        [FieldOffset(31)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InventoryRoutingTelemetryEntry
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public uint FrameIndex;
        [FieldOffset(12)] public uint ActiveSlots;
        [FieldOffset(16)] public uint EmptySlots;
        [FieldOffset(20)] public uint OrphanedSlots;
        [FieldOffset(24)] public uint QueryCount;
        [FieldOffset(28)] public uint TransactionCount;
        [FieldOffset(32)] public uint ConflictCount;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float QueryTimeEstimateUs;
        [FieldOffset(44)] public float Fragmentation01;
        [FieldOffset(48)] public uint LastItemHash;
        [FieldOffset(52)] public uint LastContainerHashLo;
        [FieldOffset(56)] private byte _pad0;
        [FieldOffset(57)] private byte _pad1;
        [FieldOffset(58)] private byte _pad2;
        [FieldOffset(59)] private byte _pad3;
        [FieldOffset(60)] private byte _pad4;
        [FieldOffset(61)] private byte _pad5;
        [FieldOffset(62)] private byte _pad6;
        [FieldOffset(63)] private byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryRoutingTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public int SliceStart;
        [FieldOffset(8)] public int SliceCount;
        [FieldOffset(12)] public int ActiveSlotCount;
        [FieldOffset(16)] public float MaxQueryRadiusMeters;
        [FieldOffset(20)] public float DecayRateMultiplier;
        [FieldOffset(24)] public int MaxTransactionCASRetries;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct InventoryStackLimitDTO
    {
        [FieldOffset(0)] public uint ItemHashID;
        [FieldOffset(4)] public uint MaxStack;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryContainerRangeDTO
    {
        [FieldOffset(0)] public ulong ContainerHash;
        [FieldOffset(8)] public ulong ContainerAUPHash;
        [FieldOffset(16)] public int SlotStart;
        [FieldOffset(20)] public int SlotCapacity;
        [FieldOffset(24)] public int ActiveSlotCount;
        [FieldOffset(28)] public uint StateFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InventoryAtomicCounter64
    {
        [FieldOffset(0)] public int Quantity;
        [FieldOffset(4)] public int SlotCount;
        [FieldOffset(8)] public uint ItemHashID;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] private byte _pad00;
        [FieldOffset(17)] private byte _pad01;
        [FieldOffset(18)] private byte _pad02;
        [FieldOffset(19)] private byte _pad03;
        [FieldOffset(20)] private byte _pad04;
        [FieldOffset(21)] private byte _pad05;
        [FieldOffset(22)] private byte _pad06;
        [FieldOffset(23)] private byte _pad07;
        [FieldOffset(24)] private byte _pad08;
        [FieldOffset(25)] private byte _pad09;
        [FieldOffset(26)] private byte _pad10;
        [FieldOffset(27)] private byte _pad11;
        [FieldOffset(28)] private byte _pad12;
        [FieldOffset(29)] private byte _pad13;
        [FieldOffset(30)] private byte _pad14;
        [FieldOffset(31)] private byte _pad15;
        [FieldOffset(32)] private byte _pad16;
        [FieldOffset(33)] private byte _pad17;
        [FieldOffset(34)] private byte _pad18;
        [FieldOffset(35)] private byte _pad19;
        [FieldOffset(36)] private byte _pad20;
        [FieldOffset(37)] private byte _pad21;
        [FieldOffset(38)] private byte _pad22;
        [FieldOffset(39)] private byte _pad23;
        [FieldOffset(40)] private byte _pad24;
        [FieldOffset(41)] private byte _pad25;
        [FieldOffset(42)] private byte _pad26;
        [FieldOffset(43)] private byte _pad27;
        [FieldOffset(44)] private byte _pad28;
        [FieldOffset(45)] private byte _pad29;
        [FieldOffset(46)] private byte _pad30;
        [FieldOffset(47)] private byte _pad31;
        [FieldOffset(48)] private byte _pad32;
        [FieldOffset(49)] private byte _pad33;
        [FieldOffset(50)] private byte _pad34;
        [FieldOffset(51)] private byte _pad35;
        [FieldOffset(52)] private byte _pad36;
        [FieldOffset(53)] private byte _pad37;
        [FieldOffset(54)] private byte _pad38;
        [FieldOffset(55)] private byte _pad39;
        [FieldOffset(56)] private byte _pad40;
        [FieldOffset(57)] private byte _pad41;
        [FieldOffset(58)] private byte _pad42;
        [FieldOffset(59)] private byte _pad43;
        [FieldOffset(60)] private byte _pad44;
        [FieldOffset(61)] private byte _pad45;
        [FieldOffset(62)] private byte _pad46;
        [FieldOffset(63)] private byte _pad47;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LogisticsTransferSignal : ISignal
    {
        [FieldOffset(0)] public ulong SourceAUPHash;
        [FieldOffset(8)] public ulong DestinationAUPHash;
        [FieldOffset(16)] public uint TransactionId;
        [FieldOffset(20)] public uint ItemHashID;
        [FieldOffset(24)] public uint Quantity;
        [FieldOffset(28)] public uint FrameIndex;
        [FieldOffset(32)] public float3 VisualMidpoint;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint SourceSlotIndex;
        [FieldOffset(52)] public uint DestinationSlotIndex;
        [FieldOffset(56)] private byte _pad0;
        [FieldOffset(57)] private byte _pad1;
        [FieldOffset(58)] private byte _pad2;
        [FieldOffset(59)] private byte _pad3;
        [FieldOffset(60)] private byte _pad4;
        [FieldOffset(61)] private byte _pad5;
        [FieldOffset(62)] private byte _pad6;
        [FieldOffset(63)] private byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct InventoryRoutingVaultLane
    {
        [FieldOffset(0)]
        public uint BufferID;

        [FieldOffset(4)]
        public uint SystemID;

        [FieldOffset(8)]
        public uint Generation;

        [FieldOffset(12)]
        public uint Flags;

        [FieldOffset(16)]
        public uint ExpectedBufferID;

        [FieldOffset(20)]
        public int Length;

        public void SetHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            BufferID = handle.BufferID;
            SystemID = handle.SystemID;
            Generation = handle.Generation;
            Flags = handle.Flags;
        }

        public VaultGenerationHandle<T> ToHandle<T>() where T : struct
        {
            VaultGenerationHandle<T> handle = default;
            handle.BufferID = BufferID;
            handle.SystemID = SystemID;
            handle.Generation = Generation;
            handle.Flags = Flags;
            return handle;
        }
    }

    public struct InventoryRoutingBufferHandles
    {
        public InventoryRoutingVaultLane Slots;
        public InventoryRoutingVaultLane ActiveSlotCount;
        public InventoryRoutingVaultLane QueryResults;
        public InventoryRoutingVaultLane QueryCounters;
        public InventoryRoutingVaultLane TelemetryRing;
        public InventoryRoutingVaultLane TelemetryCursor;
        public InventoryRoutingVaultLane Tuning;
        public InventoryRoutingVaultLane UiSnapshotA;
        public InventoryRoutingVaultLane UiSnapshotB;
        public InventoryRoutingVaultLane StackLimits;
        public InventoryRoutingVaultLane ContainerRanges;
        public InventoryRoutingVaultLane ContainerRangeCount;
        public InventoryRoutingVaultLane ContainerSyncResult;
    }

    public ref struct InventoryRoutingBuffers
    {
        public NativeArray<InventorySlotDTO> Slots;
        public NativeArray<int> ActiveSlotCount;
        public NativeArray<InventoryQueryResultDTO> QueryResults;
        public NativeArray<InventoryAtomicCounter64> QueryCounters;
        public NativeArray<InventoryRoutingTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<InventoryRoutingTuningDTO> Tuning;
        public NativeArray<InventorySlotDTO> UiSnapshotA;
        public NativeArray<InventorySlotDTO> UiSnapshotB;
        public NativeArray<InventoryStackLimitDTO> StackLimits;
        public NativeArray<InventoryContainerRangeDTO> ContainerRanges;
        public NativeArray<int> ContainerRangeCount;
        public NativeArray<InventoryContainerRangeDTO> ContainerSyncResult;
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
        public const int InventoryContainerRangeDtoSizeBytes = 32;
        public const int DefaultSlotCapacity = 100000;
        public const int DefaultQueryCapacity = 512;
        public const int DefaultContainerRangeCapacity = 2048;
        public const int DefaultContainerSlotCapacity = 64;
        public const int DefaultContainerSlotStride = DefaultContainerSlotCapacity;
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
        public const uint ConditionContainerRangePinned = 1u << 4;
        public const uint TelemetryFlagFatal = 1u << 0;
        public const uint TelemetryFlagFragmented = 1u << 1;
        public const uint TelemetryFlagToasterSliced = 1u << 2;
        public const byte TransactionResultSignalDrop = 1 << 0;
        public const uint ContainerRangeActive = 1u << 0;
        public const uint ContainerRangeSyncFailed = 1u << 1;
        public const uint ContainerRangeCapacityExceeded = 1u << 2;
        public const uint ContainerRangeMutating = 1u << 3;
        public const uint SignalLaneHash = 0x5349314Cu; // SI1L
        public const uint DumpMagic = 0x494E5652u; // INVR
        public const uint DumpVersion = 1u;
        public const string DumpPath = "Docs/AgentLogs/Dump_INVENTORY_ROUTER.bin";
        private const int TelemetryDumpHeaderBytes = 32;

        public const int InventorySlotDTO_ItemHashID = 0;
        public const int InventorySlotDTO_Quantity = 4;
        public const int InventorySlotDTO_ContainerAUPHash = 8;
        public const int InventorySlotDTO_ConditionFlags = 16;
        public const int InventorySlotDTO_ReservedLock = 20;
        public const int InventoryContainerRangeDTO_ContainerHash = 0;
        public const int InventoryContainerRangeDTO_ContainerAUPHash = 8;
        public const int InventoryContainerRangeDTO_SlotStart = 16;
        public const int InventoryContainerRangeDTO_SlotCapacity = 20;
        public const int InventoryContainerRangeDTO_ActiveSlotCount = 24;
        public const int InventoryContainerRangeDTO_StateFlags = 28;

        private const double AupHashInvMeters = 4.0d;
        private const double AupHashMeters = 0.25d;
        private const long AupQuantizedBias = 1L << 20;
        private const long AupQuantizedMin = -(1L << 20);
        private const long AupQuantizedMax = (1L << 20) - 1L;
        private const ulong AupAxisMask = (1UL << 21) - 1UL;
        private const double FloatCastClampMeters = 3.4028234663852886e38d;
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
                   OffsetOf<InventoryQueryResultDTO>(nameof(InventoryQueryResultDTO.ItemHashID)) == 0 &&
                   OffsetOf<InventoryQueryResultDTO>(nameof(InventoryQueryResultDTO.RequestedIndex)) == 4 &&
                   OffsetOf<InventoryQueryResultDTO>(nameof(InventoryQueryResultDTO.Quantity)) == 8 &&
                   OffsetOf<InventoryQueryResultDTO>(nameof(InventoryQueryResultDTO.SlotCount)) == 12 &&
                   OffsetOf<InventoryQueryResultDTO>(nameof(InventoryQueryResultDTO.Flags)) == 16 &&
                   OffsetOf<InventoryQueryResultDTO>(nameof(InventoryQueryResultDTO.LastSlotIndex)) == 20 &&
                   UnsafeUtility.SizeOf<InventoryTransactionRequestDTO>() == InventoryTransactionRequestDtoSizeBytes &&
                   UnsafeUtility.SizeOf<InventoryTransactionResultDTO>() == InventoryTransactionResultDtoSizeBytes &&
                   OffsetOf<InventoryTransactionResultDTO>(nameof(InventoryTransactionResultDTO.TransactionId)) == 0 &&
                   OffsetOf<InventoryTransactionResultDTO>(nameof(InventoryTransactionResultDTO.ItemHashID)) == 4 &&
                   OffsetOf<InventoryTransactionResultDTO>(nameof(InventoryTransactionResultDTO.QuantityMoved)) == 8 &&
                   OffsetOf<InventoryTransactionResultDTO>(nameof(InventoryTransactionResultDTO.SourceSlotIndex)) == 12 &&
                   OffsetOf<InventoryTransactionResultDTO>(nameof(InventoryTransactionResultDTO.DestinationSlotIndex)) == 16 &&
                   OffsetOf<InventoryTransactionResultDTO>(nameof(InventoryTransactionResultDTO.FrameIndex)) == 20 &&
                   OffsetOf<InventoryTransactionResultDTO>(nameof(InventoryTransactionResultDTO.Reserved1)) == 24 &&
                   OffsetOf<InventoryTransactionResultDTO>(nameof(InventoryTransactionResultDTO.Reserved0)) == 28 &&
                   OffsetOf<InventoryTransactionResultDTO>(nameof(InventoryTransactionResultDTO.Status)) == 30 &&
                   OffsetOf<InventoryTransactionResultDTO>(nameof(InventoryTransactionResultDTO.Flags)) == 31 &&
                   UnsafeUtility.SizeOf<InventoryRoutingTelemetryEntry>() == InventoryTelemetryEntrySizeBytes &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.StateHash)) == 0 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.FrameIndex)) == 8 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.ActiveSlots)) == 12 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.EmptySlots)) == 16 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.OrphanedSlots)) == 20 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.QueryCount)) == 24 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.TransactionCount)) == 28 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.ConflictCount)) == 32 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.Flags)) == 36 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.QueryTimeEstimateUs)) == 40 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.Fragmentation01)) == 44 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.LastItemHash)) == 48 &&
                   OffsetOf<InventoryRoutingTelemetryEntry>(nameof(InventoryRoutingTelemetryEntry.LastContainerHashLo)) == 52 &&
                   UnsafeUtility.SizeOf<InventoryRoutingTuningDTO>() == 32 &&
                   UnsafeUtility.SizeOf<InventoryStackLimitDTO>() == 16 &&
                   UnsafeUtility.SizeOf<InventoryContainerRangeDTO>() == InventoryContainerRangeDtoSizeBytes &&
                   OffsetOf<InventoryContainerRangeDTO>(nameof(InventoryContainerRangeDTO.ContainerHash)) == InventoryContainerRangeDTO_ContainerHash &&
                   OffsetOf<InventoryContainerRangeDTO>(nameof(InventoryContainerRangeDTO.ContainerAUPHash)) == InventoryContainerRangeDTO_ContainerAUPHash &&
                   OffsetOf<InventoryContainerRangeDTO>(nameof(InventoryContainerRangeDTO.SlotStart)) == InventoryContainerRangeDTO_SlotStart &&
                   OffsetOf<InventoryContainerRangeDTO>(nameof(InventoryContainerRangeDTO.SlotCapacity)) == InventoryContainerRangeDTO_SlotCapacity &&
                   OffsetOf<InventoryContainerRangeDTO>(nameof(InventoryContainerRangeDTO.ActiveSlotCount)) == InventoryContainerRangeDTO_ActiveSlotCount &&
                   OffsetOf<InventoryContainerRangeDTO>(nameof(InventoryContainerRangeDTO.StateFlags)) == InventoryContainerRangeDTO_StateFlags &&
                   UnsafeUtility.SizeOf<InventoryAtomicCounter64>() == 64 &&
                   OffsetOf<InventoryAtomicCounter64>(nameof(InventoryAtomicCounter64.Quantity)) == 0 &&
                   OffsetOf<InventoryAtomicCounter64>(nameof(InventoryAtomicCounter64.SlotCount)) == 4 &&
                   OffsetOf<InventoryAtomicCounter64>(nameof(InventoryAtomicCounter64.ItemHashID)) == 8 &&
                   OffsetOf<InventoryAtomicCounter64>(nameof(InventoryAtomicCounter64.Flags)) == 12 &&
                   UnsafeUtility.SizeOf<LogisticsTransferSignal>() == 64 &&
                   OffsetOf<LogisticsTransferSignal>(nameof(LogisticsTransferSignal.SourceAUPHash)) == 0 &&
                   OffsetOf<LogisticsTransferSignal>(nameof(LogisticsTransferSignal.DestinationAUPHash)) == 8 &&
                   OffsetOf<LogisticsTransferSignal>(nameof(LogisticsTransferSignal.TransactionId)) == 16 &&
                   OffsetOf<LogisticsTransferSignal>(nameof(LogisticsTransferSignal.ItemHashID)) == 20 &&
                   OffsetOf<LogisticsTransferSignal>(nameof(LogisticsTransferSignal.Quantity)) == 24 &&
                   OffsetOf<LogisticsTransferSignal>(nameof(LogisticsTransferSignal.FrameIndex)) == 28 &&
                   OffsetOf<LogisticsTransferSignal>(nameof(LogisticsTransferSignal.VisualMidpoint)) == 32 &&
                   OffsetOf<LogisticsTransferSignal>(nameof(LogisticsTransferSignal.Flags)) == 44 &&
                   OffsetOf<LogisticsTransferSignal>(nameof(LogisticsTransferSignal.SourceSlotIndex)) == 48 &&
                   OffsetOf<LogisticsTransferSignal>(nameof(LogisticsTransferSignal.DestinationSlotIndex)) == 52;
        }

        public static bool ValidateRuntimeLayoutOrThrow()
        {
            return RuntimeLayoutValid();
        }

        public static InventoryRoutingBufferHandles EnsureBuffers(
            IDataVault vault,
            int slotCapacity = DefaultSlotCapacity,
            int queryCapacity = DefaultQueryCapacity,
            int stackLimitCapacity = StackLimitCapacity,
            int containerRangeCapacity = DefaultContainerRangeCapacity)
        {
            InventoryRoutingBufferHandles handles = default;
            if (vault == null)
                return handles;

            if (!ValidateRuntimeLayoutOrThrow())
                return handles;

            slotCapacity = math.max(1, slotCapacity);
            queryCapacity = math.max(1, queryCapacity);
            stackLimitCapacity = math.max(1, stackLimitCapacity);
            containerRangeCapacity = math.max(1, containerRangeCapacity);

            handles.Slots = AcquireLane<InventorySlotDTO>(
                vault,
                BufferID.ShinobuInventorySlots,
                slotCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            handles.ActiveSlotCount = AcquireLane<int>(
                vault,
                BufferID.ShinobuInventoryActiveSlotCount,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.QueryResults = AcquireLane<InventoryQueryResultDTO>(
                vault,
                BufferID.ShinobuInventoryQueryResults,
                queryCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.QueryCounters = AcquireLane<InventoryAtomicCounter64>(
                vault,
                BufferID.ShinobuInventoryQueryCounters,
                queryCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryRing = AcquireLane<InventoryRoutingTelemetryEntry>(
                vault,
                BufferID.ShinobuInventoryRoutingTelemetry,
                TelemetryCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = AcquireLane<int>(
                vault,
                BufferID.ShinobuInventoryRoutingTelemetryCursor,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.Tuning = AcquireLane<InventoryRoutingTuningDTO>(
                vault,
                BufferID.ShinobuInventoryRoutingTuning,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.UiSnapshotA = AcquireLane<InventorySlotDTO>(
                vault,
                BufferID.ShinobuInventoryUiSnapshotA,
                slotCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            handles.UiSnapshotB = AcquireLane<InventorySlotDTO>(
                vault,
                BufferID.ShinobuInventoryUiSnapshotB,
                slotCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            handles.StackLimits = AcquireLane<InventoryStackLimitDTO>(
                vault,
                BufferID.ShinobuInventoryStackLimits,
                stackLimitCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.ContainerRanges = AcquireLane<InventoryContainerRangeDTO>(
                vault,
                BufferID.ShinobuInventoryContainerRanges,
                containerRangeCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.ContainerRangeCount = AcquireLane<int>(
                vault,
                BufferID.ShinobuInventoryContainerRangeCount,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.ContainerSyncResult = AcquireLane<InventoryContainerRangeDTO>(
                vault,
                BufferID.ShinobuInventoryContainerSyncResult,
                1,
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

            buffers.Slots = OpenLane<InventorySlotDTO>(vault, in handles.Slots);
            buffers.ActiveSlotCount = OpenLane<int>(vault, in handles.ActiveSlotCount);
            buffers.QueryResults = OpenLane<InventoryQueryResultDTO>(vault, in handles.QueryResults);
            buffers.QueryCounters = OpenLane<InventoryAtomicCounter64>(vault, in handles.QueryCounters);
            buffers.TelemetryRing = OpenLane<InventoryRoutingTelemetryEntry>(vault, in handles.TelemetryRing);
            buffers.TelemetryCursor = OpenLane<int>(vault, in handles.TelemetryCursor);
            buffers.Tuning = OpenLane<InventoryRoutingTuningDTO>(vault, in handles.Tuning);
            buffers.UiSnapshotA = OpenLane<InventorySlotDTO>(vault, in handles.UiSnapshotA);
            buffers.UiSnapshotB = OpenLane<InventorySlotDTO>(vault, in handles.UiSnapshotB);
            buffers.StackLimits = OpenLane<InventoryStackLimitDTO>(vault, in handles.StackLimits);
            buffers.ContainerRanges = OpenLane<InventoryContainerRangeDTO>(vault, in handles.ContainerRanges);
            buffers.ContainerRangeCount = OpenLane<int>(vault, in handles.ContainerRangeCount);
            buffers.ContainerSyncResult = OpenLane<InventoryContainerRangeDTO>(vault, in handles.ContainerSyncResult);

            return buffers.Slots.IsCreated &&
                   buffers.ActiveSlotCount.IsCreated &&
                   buffers.QueryResults.IsCreated &&
                   buffers.QueryCounters.IsCreated &&
                   buffers.TelemetryRing.IsCreated &&
                   buffers.TelemetryCursor.IsCreated &&
                   buffers.Tuning.IsCreated &&
                   buffers.UiSnapshotA.IsCreated &&
                   buffers.UiSnapshotB.IsCreated &&
                   buffers.StackLimits.IsCreated &&
                   buffers.ContainerRanges.IsCreated &&
                   buffers.ContainerRangeCount.IsCreated &&
                   buffers.ContainerSyncResult.IsCreated;
        }

        private static InventoryRoutingVaultLane AcquireLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return default;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                owner,
                options);
            uint expectedBufferId = unchecked((uint)(int)bufferId);
            if (handle.BufferID != expectedBufferId || handle.Generation == 0u)
                return default;

            InventoryRoutingVaultLane lane = default;
            lane.SetHandle(in handle);
            lane.ExpectedBufferID = expectedBufferId;
            lane.Length = requiredLength;
            return lane;
        }

        private static NativeArray<T> OpenLane<T>(
            IDataVault vault,
            in InventoryRoutingVaultLane lane) where T : struct
        {
            VaultGenerationHandle<T> handle = lane.ToHandle<T>();
            if (vault == null ||
                lane.ExpectedBufferID == 0u ||
                lane.BufferID != lane.ExpectedBufferID ||
                lane.Generation == 0u ||
                lane.Length <= 0 ||
                !vault.TryResolveHandle(in handle, out NativeArray<T> buffer) ||
                !buffer.IsCreated ||
                buffer.Length < lane.Length)
            {
                return default;
            }

            return buffer;
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

            float weight = SanitizeQualityWeight(globalQualityWeight);
            float eased = weight * weight * (3f - 2f * weight);
            int minBudget = math.max(1, minSlots);
            int maxBudget = math.max(minBudget, maxSlots);
            int budget = (int)math.round(math.lerp(minBudget, maxBudget, eased));
            return math.clamp(budget, 1, totalSlots);
        }

        public static float ResolveCurrentGlobalQualityWeight()
        {
            return SanitizeQualityWeight(HomeostasisBrain.GlobalQualityWeight);
        }

        public static int ResolveNextSliceStart(int previousStart, int processedCount, int totalSlots)
        {
            if (totalSlots <= 0)
                return 0;

            int next = previousStart + math.max(0, processedCount);
            if (next >= totalSlots)
                next %= totalSlots;
            return math.max(0, next);
        }

        public static JobHandle GenerateMockLogisticsNetwork(
            NativeArray<InventorySlotDTO> slots,
            int slotCount,
            double3 originAUP,
            uint seed,
            JobHandle dependency = default)
        {
            int count = slots.IsCreated ? math.clamp(slotCount, 0, slots.Length) : 0;
            if (count == 0)
                return dependency;

            GenerateMockLogisticsNetworkJob job = new GenerateMockLogisticsNetworkJob
            {
                Slots = slots,
                SlotCount = count,
                OriginAUP = originAUP,
                Seed = seed
            };
            return job.Schedule(count, 128, dependency);
        }

        public static JobHandle SchedulePaddedAggregation(
            NativeArray<InventorySlotDTO> slots,
            NativeArray<uint> requestedItemHashIds,
            NativeArray<InventoryAtomicCounter64> counters,
            NativeParallelHashMap<uint, int> totalsByHash,
            NativeArray<InventoryQueryResultDTO> results,
            double3 queryAUP,
            float maxDistanceMeters,
            int slotStart,
            int requestedSlotCount,
            float globalQualityWeight,
            JobHandle dependency = default)
        {
            return SchedulePaddedAggregation(
                slots,
                requestedItemHashIds,
                counters,
                totalsByHash,
                results,
                queryAUP,
                maxDistanceMeters,
                slotStart,
                requestedSlotCount,
                globalQualityWeight,
                clearCountersBeforeAggregation: true,
                dependency);
        }

        public static JobHandle SchedulePaddedAggregation(
            NativeArray<InventorySlotDTO> slots,
            NativeArray<uint> requestedItemHashIds,
            NativeArray<InventoryAtomicCounter64> counters,
            NativeParallelHashMap<uint, int> totalsByHash,
            NativeArray<InventoryQueryResultDTO> results,
            double3 queryAUP,
            float maxDistanceMeters,
            int slotStart,
            int requestedSlotCount,
            float globalQualityWeight,
            bool clearCountersBeforeAggregation,
            JobHandle dependency = default)
        {
            if (!slots.IsCreated || !requestedItemHashIds.IsCreated || !counters.IsCreated)
                return dependency;

            int safeStart = math.clamp(slotStart, 0, slots.Length);
            int remaining = math.max(0, slots.Length - safeStart);
            int requestedCount = requestedSlotCount > 0 ? math.min(requestedSlotCount, remaining) : remaining;
            int sliceCount = math.min(requestedCount, ResolveTimeSliceBatchSize(globalQualityWeight, requestedCount));
            if (sliceCount <= 0)
                return dependency;

            int queryCount = math.min(requestedItemHashIds.Length, counters.Length);
            if (queryCount <= 0)
                return dependency;

            JobHandle aggregateDependency = dependency;
            if (clearCountersBeforeAggregation)
            {
                aggregateDependency = new ClearInventoryAtomicCountersJob
                {
                    Counters = counters,
                    RequestedItemHashIds = requestedItemHashIds
                }.Schedule(queryCount, 32, dependency);
            }

            JobHandle aggregate = new AggregateAvailableResourcesPaddedJob
            {
                Slots = slots,
                RequestedItemHashIds = requestedItemHashIds,
                Counters = counters,
                QueryAUP = queryAUP,
                MaxDistanceMeters = SanitizeNonNegativeFinite(maxDistanceMeters),
                SlotStart = safeStart,
                SlotCount = sliceCount
            }.Schedule(sliceCount, 128, aggregateDependency);

            return new FlushPaddedTotalsToHashMapJob
            {
                Counters = counters,
                TotalsByHash = totalsByHash,
                Results = results
            }.Schedule(aggregate);
        }

        public static JobHandle ScheduleResourceHashIndexLookup(
            NativeArray<InventorySlotDTO> slots,
            NativeArray<int> indexKeys,
            NativeArray<int> indexTotals,
            NativeArray<uint> requestedItemHashIds,
            NativeArray<InventoryQueryResultDTO> results,
            double3 queryAUP,
            float maxDistanceMeters,
            int slotStart,
            int requestedSlotCount,
            float globalQualityWeight,
            JobHandle dependency = default)
        {
            return ScheduleResourceHashIndexLookup(
                slots,
                indexKeys,
                indexTotals,
                requestedItemHashIds,
                results,
                queryAUP,
                maxDistanceMeters,
                slotStart,
                requestedSlotCount,
                globalQualityWeight,
                clearIndexBeforeBuild: true,
                dependency);
        }

        public static JobHandle ScheduleResourceHashIndexLookup(
            NativeArray<InventorySlotDTO> slots,
            NativeArray<int> indexKeys,
            NativeArray<int> indexTotals,
            NativeArray<uint> requestedItemHashIds,
            NativeArray<InventoryQueryResultDTO> results,
            double3 queryAUP,
            float maxDistanceMeters,
            int slotStart,
            int requestedSlotCount,
            float globalQualityWeight,
            bool clearIndexBeforeBuild,
            JobHandle dependency = default)
        {
            if (!slots.IsCreated ||
                !indexKeys.IsCreated ||
                !indexTotals.IsCreated ||
                !requestedItemHashIds.IsCreated ||
                !results.IsCreated)
            {
                return dependency;
            }

            int indexCapacity = math.min(indexKeys.Length, indexTotals.Length);
            if (indexCapacity <= 0)
                return dependency;

            int safeStart = math.clamp(slotStart, 0, slots.Length);
            int remaining = math.max(0, slots.Length - safeStart);
            int requestedCount = requestedSlotCount > 0 ? math.min(requestedSlotCount, remaining) : remaining;
            int sliceCount = math.min(requestedCount, ResolveTimeSliceBatchSize(globalQualityWeight, requestedCount));
            int lookupCount = math.min(requestedItemHashIds.Length, results.Length);
            if (sliceCount <= 0 || lookupCount <= 0)
                return dependency;

            JobHandle buildDependency = dependency;
            if (clearIndexBeforeBuild)
            {
                JobHandle clearKeys = new ClearIntArrayJob { Values = indexKeys }.Schedule(indexKeys.Length, 128, dependency);
                JobHandle clearTotals = new ClearIntArrayJob { Values = indexTotals }.Schedule(indexTotals.Length, 128, dependency);
                buildDependency = JobHandle.CombineDependencies(clearKeys, clearTotals);
            }

            JobHandle build = new BuildResourceHashIndexJob
            {
                Slots = slots,
                IndexKeys = indexKeys,
                IndexTotals = indexTotals,
                QueryAUP = queryAUP,
                MaxDistanceMeters = SanitizeNonNegativeFinite(maxDistanceMeters),
                SlotStart = safeStart,
                SlotCount = sliceCount
            }.Schedule(sliceCount, 128, buildDependency);

            return new LookupResourceHashIndexJob
            {
                IndexKeys = indexKeys,
                IndexTotals = indexTotals,
                RequestedItemHashIds = requestedItemHashIds,
                Results = results
            }.Schedule(lookupCount, 32, build);
        }

        public static JobHandle ScheduleContainerSnapshotPublish(
            NativeArray<InventorySlotDTO> slots,
            NativeArray<InventoryContainerRangeDTO> containerRanges,
            NativeArray<int> containerRangeCount,
            NativeArray<InventoryContainerRangeDTO> containerSyncResult,
            NativeArray<uint> itemHashIds,
            NativeArray<uint> itemQuantities,
            NativeArray<uint> conditionFlags,
            NativeArray<uint> reservedLocks,
            ulong containerHash,
            double3 containerAUP,
            int requestedSlotCapacity = DefaultContainerSlotCapacity,
            ushort defaultQualityMilli = 1000,
            JobHandle dependency = default)
        {
            if (!slots.IsCreated ||
                !containerRanges.IsCreated ||
                !containerRangeCount.IsCreated ||
                !containerSyncResult.IsCreated ||
                !itemHashIds.IsCreated ||
                containerRangeCount.Length == 0 ||
                containerSyncResult.Length == 0 ||
                containerHash == 0UL)
            {
                return dependency;
            }

            int safeSlotCapacity = math.max(1, requestedSlotCapacity);
            return new PublishInventoryContainerSnapshotJob
            {
                Slots = slots,
                ContainerRanges = containerRanges,
                ContainerRangeCount = containerRangeCount,
                ResultRange = containerSyncResult,
                ContainerHash = containerHash,
                ContainerAUPHash = PackAupHash(containerAUP),
                ItemHashIds = itemHashIds,
                ItemQuantities = itemQuantities,
                ConditionFlags = conditionFlags,
                ReservedLocks = reservedLocks,
                SlotCapacity = safeSlotCapacity,
                SlotStride = DefaultContainerSlotStride,
                TotalSlotCapacity = slots.Length,
                DefaultConditionFlags = PackConditionFlags(defaultQualityMilli, ConditionContainerRangePinned)
            }.Schedule(dependency);
        }

        public static JobHandle ScheduleContainerRangeClear(
            NativeArray<InventorySlotDTO> slots,
            NativeArray<InventoryContainerRangeDTO> containerRanges,
            NativeArray<InventoryContainerRangeDTO> containerSyncResult,
            ulong containerHash,
            JobHandle dependency = default)
        {
            if (!slots.IsCreated ||
                !containerRanges.IsCreated ||
                !containerSyncResult.IsCreated ||
                containerSyncResult.Length == 0 ||
                containerHash == 0UL)
            {
                return dependency;
            }

            return new ClearInventoryContainerRangeJob
            {
                Slots = slots,
                ContainerRanges = containerRanges,
                ResultRange = containerSyncResult,
                ContainerHash = containerHash
            }.Schedule(dependency);
        }

        public static JobHandle ScheduleTransactions(
            NativeArray<InventorySlotDTO> slots,
            NativeArray<InventoryTransactionRequestDTO> requests,
            NativeArray<InventoryTransactionResultDTO> results,
            int requestCount,
            bool emitTransferSignals,
            JobHandle dependency = default)
        {
            if (!slots.IsCreated || !requests.IsCreated || !results.IsCreated)
                return dependency;

            global::Hecton8.Core.MpscSignalRingBuffer<LogisticsTransferSignal>.ParallelWriter writer = default;
            NativeArray<int> writerBudget = default;
            int emit = 0;
            if (emitTransferSignals)
            {
                EnsureSignalLane();
                writer = SignalBus<LogisticsTransferSignal>.ParallelWriter;
                writerBudget = SignalBus<LogisticsTransferSignal>.ParallelWriterBudget;
                emit = 1;
            }

            return new InventoryTransactionJob
            {
                Slots = slots,
                Requests = requests,
                Results = results,
                TransferSignalWriter = writer,
                TransferSignalWriterBudget = writerBudget,
                RequestCount = requestCount,
                EmitTransferSignals = emit
            }.Schedule(dependency);
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
            if (!math.isfinite(aup.x) || !math.isfinite(aup.y) || !math.isfinite(aup.z))
            {
                ulong zero = PackAupAxis(0d);
                return zero | (zero << 21) | (zero << 42);
            }

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
            int entrySize = UnsafeUtility.SizeOf<InventoryRoutingTelemetryEntry>();
            if (entrySize <= 0 || telemetry.Length > (int.MaxValue - TelemetryDumpHeaderBytes) / entrySize)
                return;

            int telemetryBytes = telemetry.Length * entrySize;
            int byteCount = TelemetryDumpHeaderBytes + telemetryBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(InventoryRoutingNetwork),
                    "InventoryRoutingTelemetryBlackBoxDumpPayload",
                    NativeArrayOptions.UninitializedMemory);

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                int offset = 0;
                long utcTicks = DateTime.UtcNow.Ticks;
                if (!TryWriteUInt32LittleEndian(destination, byteCount, ref offset, DumpMagic) ||
                    !TryWriteUInt32LittleEndian(destination, byteCount, ref offset, DumpVersion) ||
                    !TryWriteUInt32LittleEndian(destination, byteCount, ref offset, (uint)telemetry.Length) ||
                    !TryWriteUInt32LittleEndian(destination, byteCount, ref offset, (uint)entrySize) ||
                    !TryWriteUInt32LittleEndian(destination, byteCount, ref offset, cursor.IsCreated && cursor.Length > 0 ? unchecked((uint)cursor[0]) : 0u) ||
                    !TryWriteUInt32LittleEndian(destination, byteCount, ref offset, (uint)utcTicks) ||
                    !TryWriteUInt32LittleEndian(destination, byteCount, ref offset, (uint)(utcTicks >> 32)) ||
                    !TryWriteUInt32LittleEndian(destination, byteCount, ref offset, 0u))
                {
                    return;
                }

                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                UnsafeUtility.MemCpy(destination + offset, source, telemetryBytes);
                offset += telemetryBytes;
                if (offset == byteCount)
                    NativeFaultDumpWriter.TryWriteAll(fullPath, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(InventoryRoutingNetwork),
                    "InventoryRoutingTelemetryBlackBoxDumpPayload");
            }
        }

        private static bool TryWriteUInt32LittleEndian(byte* destination, int capacity, ref int offset, uint value)
        {
            if (offset > capacity - 4)
                return false;

            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
            offset += 4;
            return true;
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
        internal static void AtomicAddQuantity(NativeArray<InventoryAtomicCounter64> counters, int index, int delta)
        {
            if (!counters.IsCreated || (uint)index >= (uint)counters.Length || delta == 0)
                return;

            InventoryAtomicCounter64* ptr = (InventoryAtomicCounter64*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(counters);
            Interlocked.Add(ref UnsafeUtility.AsRef<int>(&ptr[index].Quantity), delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AtomicAddSlotCount(NativeArray<InventoryAtomicCounter64> counters, int index, int delta)
        {
            if (!counters.IsCreated || (uint)index >= (uint)counters.Length || delta == 0)
                return;

            InventoryAtomicCounter64* ptr = (InventoryAtomicCounter64*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(counters);
            Interlocked.Add(ref UnsafeUtility.AsRef<int>(&ptr[index].SlotCount), delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool PassesAupGate(ulong containerAupHash, double3 queryAup, float maxDistanceMeters)
        {
            float safeMaxDistance = SanitizeNonNegativeFinite(maxDistanceMeters);
            if (safeMaxDistance <= 0f)
                return true;

            double3 containerAup = DecodeAupHash(containerAupHash);
            double3 deltaDouble = containerAup - queryAup;
            float3 delta = ClampAupDeltaToFloat3(deltaDouble);
            if (!math.all(math.isfinite(delta)))
                return false;

            return math.lengthsq(delta) <= safeMaxDistance * safeMaxDistance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ClampAupDeltaToFloat3(double3 deltaAup)
        {
            float3 result = default;
            result.x = (float)math.clamp(deltaAup.x, -FloatCastClampMeters, FloatCastClampMeters);
            result.y = (float)math.clamp(deltaAup.y, -FloatCastClampMeters, FloatCastClampMeters);
            result.z = (float)math.clamp(deltaAup.z, -FloatCastClampMeters, FloatCastClampMeters);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SanitizeQualityWeight(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SanitizeNonNegativeFinite(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong PackAupAxis(double value)
        {
            if (!math.isfinite(value))
                value = 0d;

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
            uint indexHash = unchecked((uint)index);
            uint itemHash = empty ? 0u : 0x1000u + (((indexHash * 2654435761u) + salt) & 255u);
            uint quantity = empty ? 0u : (uint)(1 + ((index * 17) & 63));
            double3 offset = default;
            offset.x = (index % 317) * 2.0d;
            offset.y = ((index / 317) % 19) * 0.5d;
            offset.z = (index % 91) * -1.75d;

            InventorySlotDTO slot = default;
            slot.ItemHashID = itemHash;
            slot.Quantity = quantity;
            slot.ContainerAUPHash = InventoryRoutingNetwork.PackAupHash(OriginAUP + offset);
            slot.ConditionFlags = InventoryRoutingNetwork.PackConditionFlags(1000, (index & 7) == 0 ? InventoryRoutingNetwork.ConditionPerishable : 0u);
            slot.ReservedLock = 0u;
            Slots[index] = slot;
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
    public unsafe struct BuildResourceHashIndexJob : IJobParallelFor
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

                ref int keyRef = ref UnsafeUtility.AsRef<int>(keys + index);
                int observed = Interlocked.CompareExchange(ref keyRef, 0, 0);
                if (observed == key)
                {
                    InventoryRoutingNetwork.AtomicAdd(IndexTotals, index, quantity);
                    return;
                }

                if (observed != 0)
                    continue;

                int previous = Interlocked.CompareExchange(ref keyRef, key, 0);
                if (previous == 0 || previous == key)
                {
                    InventoryRoutingNetwork.AtomicAdd(IndexTotals, index, quantity);
                    return;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ClearInventoryAtomicCountersJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<InventoryAtomicCounter64> Counters;
        [ReadOnly, NoAlias] public NativeArray<uint> RequestedItemHashIds;

        public void Execute(int index)
        {
            if (!Counters.IsCreated || (uint)index >= (uint)Counters.Length)
                return;

            InventoryAtomicCounter64 counter = default;
            counter.ItemHashID = RequestedItemHashIds.IsCreated && index < RequestedItemHashIds.Length ? RequestedItemHashIds[index] : 0u;
            Counters[index] = counter;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct AggregateAvailableResourcesPaddedJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<InventorySlotDTO> Slots;
        [ReadOnly, NoAlias] public NativeArray<uint> RequestedItemHashIds;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<InventoryAtomicCounter64> Counters;
        public double3 QueryAUP;
        public float MaxDistanceMeters;
        public int SlotStart;
        public int SlotCount;

        public void Execute(int jobIndex)
        {
            int slotIndex = SlotStart + jobIndex;
            if (!Slots.IsCreated ||
                !RequestedItemHashIds.IsCreated ||
                !Counters.IsCreated ||
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
            int limit = math.min(RequestedItemHashIds.Length, Counters.Length);
            for (int requestIndex = 0; requestIndex < limit; requestIndex++)
            {
                if (RequestedItemHashIds[requestIndex] != slot.ItemHashID)
                    continue;

                InventoryRoutingNetwork.AtomicAddQuantity(Counters, requestIndex, quantity);
                InventoryRoutingNetwork.AtomicAddSlotCount(Counters, requestIndex, 1);
                return;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct FlushPaddedTotalsToHashMapJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<InventoryAtomicCounter64> Counters;
        [NoAlias]
        public NativeParallelHashMap<uint, int> TotalsByHash;
        [NoAlias] public NativeArray<InventoryQueryResultDTO> Results;

        public void Execute()
        {
            if (!Counters.IsCreated)
                return;

            if (TotalsByHash.IsCreated)
                TotalsByHash.Clear();

            int resultCount = Results.IsCreated ? Results.Length : 0;
            for (int i = 0; i < Counters.Length; i++)
            {
                InventoryAtomicCounter64 counter = Counters[i];
                if (counter.ItemHashID == 0u)
                    continue;

                int quantity = math.max(0, counter.Quantity);
                if (TotalsByHash.IsCreated && quantity > 0)
                    TotalsByHash.TryAdd(counter.ItemHashID, quantity);

                if (i < resultCount)
                {
                    InventoryQueryResultDTO result = default;
                    result.ItemHashID = counter.ItemHashID;
                    result.RequestedIndex = i;
                    result.Quantity = quantity;
                    result.SlotCount = math.max(0, counter.SlotCount);
                    result.Flags = quantity > 0 ? 1u : 0u;
                    result.LastSlotIndex = -1;
                    Results[i] = result;
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
            InventoryQueryResultDTO result = default;
            result.ItemHashID = hash;
            result.RequestedIndex = index;
            result.Quantity = found ? quantity : 0;
            result.SlotCount = 0;
            result.Flags = found ? 1u : 0u;
            result.LastSlotIndex = -1;
            Results[index] = result;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InventoryTransactionJob : IJob
    {
        [NoAlias] public NativeArray<InventorySlotDTO> Slots;
        [ReadOnly, NoAlias] public NativeArray<InventoryTransactionRequestDTO> Requests;
        [NoAlias] public NativeArray<InventoryTransactionResultDTO> Results;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity's container safety cannot prove this presentation queue writer is memory-disjoint from the slot arrays
        // because the writer is injected as a NativeQueue ParallelWriter. The transaction job only appends 64-byte
        // LogisticsTransferSignal records after a successful two-slot CAS; it never reads or drains the queue and it
        // does not alias Slots, Requests, or Results.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected alternatives: dropping transfer signals would delete the Dear Lie presentation route, and routing
        // through a managed queue would reintroduce GC and main-thread stalls. A Vault scratch array would need a
        // separate atomic cursor and false-sharing padding while still duplicating NativeQueue semantics.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is caller-owned: when EmitTransferSignals is nonzero, TransferSignalWriter must be produced
        // from the configured LogisticsTransferSignal lane for this frame. This job performs enqueue-only writes to
        // that lane; authoritative item state remains in Slots and is protected by ReservedLock CompareExchange.
        [NativeDisableContainerSafetyRestriction]
        public global::Hecton8.Core.MpscSignalRingBuffer<LogisticsTransferSignal>.ParallelWriter TransferSignalWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> TransferSignalWriterBudget;
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
            result = default;
            result.TransactionId = request.TransactionId;
            result.ItemHashID = request.ItemHashID;
            result.SourceSlotIndex = request.SourceSlotIndex;
            result.DestinationSlotIndex = request.DestinationSlotIndex;
            result.FrameIndex = request.FrameIndex;

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
                uint sourcePinned = source.ConditionFlags & InventoryRoutingNetwork.ConditionContainerRangePinned;
                uint destinationPinned = destination.ConditionFlags & InventoryRoutingNetwork.ConditionContainerRangePinned;
                source.Quantity -= request.Quantity;
                if (source.Quantity == 0u)
                {
                    source.ItemHashID = 0u;
                    source.ConditionFlags = sourcePinned;
                }

                if (destination.ItemHashID == 0u)
                {
                    destination.ItemHashID = request.ItemHashID;
                    destination.ConditionFlags = movedConditionFlags | destinationPinned;
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
                    double3 midpointLocalFromSource = (destinationAup - sourceAup) * 0.5d;
                    float3 visualMidpoint = InventoryRoutingNetwork.ClampAupDeltaToFloat3(midpointLocalFromSource);
                    LogisticsTransferSignal signal = default;
                    signal.TransactionId = request.TransactionId;
                    signal.ItemHashID = request.ItemHashID;
                    signal.Quantity = request.Quantity;
                    signal.FrameIndex = request.FrameIndex;
                    signal.SourceAUPHash = source.ContainerAUPHash;
                    signal.DestinationAUPHash = destination.ContainerAUPHash;
                    signal.VisualMidpoint = visualMidpoint;
                    signal.Flags = request.Flags;
                    signal.SourceSlotIndex = unchecked((uint)request.SourceSlotIndex);
                    signal.DestinationSlotIndex = unchecked((uint)request.DestinationSlotIndex);
                    if (!SignalBus<LogisticsTransferSignal>.TryEnqueueBounded(TransferSignalWriter, TransferSignalWriterBudget, signal))
                    {
                        result.Flags |= InventoryRoutingNetwork.TransactionResultSignalDrop;
                    }
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
    public unsafe struct PublishInventoryContainerSnapshotJob : IJob
    {
        [NoAlias] public NativeArray<InventorySlotDTO> Slots;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<InventoryContainerRangeDTO> ContainerRanges;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> ContainerRangeCount;
        [NoAlias] public NativeArray<InventoryContainerRangeDTO> ResultRange;
        [ReadOnly, NoAlias] public NativeArray<uint> ItemHashIds;
        [ReadOnly, NoAlias] public NativeArray<uint> ItemQuantities;
        [ReadOnly, NoAlias] public NativeArray<uint> ConditionFlags;
        [ReadOnly, NoAlias] public NativeArray<uint> ReservedLocks;
        public ulong ContainerHash;
        public ulong ContainerAUPHash;
        public int SlotCapacity;
        public int SlotStride;
        public int TotalSlotCapacity;
        public uint DefaultConditionFlags;

        public void Execute()
        {
            if (!Slots.IsCreated ||
                !ContainerRanges.IsCreated ||
                !ContainerRangeCount.IsCreated ||
                !ResultRange.IsCreated ||
                ContainerRangeCount.Length == 0 ||
                ResultRange.Length == 0 ||
                !ItemHashIds.IsCreated ||
                ContainerHash == 0UL)
            {
                WriteFailure();
                return;
            }

            if (!TryResolveWritableRange(out InventoryContainerRangeDTO range))
                return;

            int slotCapacity = math.min(range.SlotCapacity, Slots.Length - range.SlotStart);
            int active = 0;
            for (int index = 0; index < slotCapacity; index++)
            {
                uint itemHash = index < ItemHashIds.Length ? ItemHashIds[index] : 0u;
                uint quantity = ResolveQuantity(index, itemHash);
                uint reservedLock = ResolveReservedLock(index);
                uint flags = ResolveConditionFlags(index, itemHash, quantity, reservedLock);

                if (itemHash == 0u || quantity == 0u)
                {
                    InventorySlotDTO emptySlot = default;
                    emptySlot.ItemHashID = 0u;
                    emptySlot.Quantity = 0u;
                    emptySlot.ContainerAUPHash = range.ContainerAUPHash;
                    emptySlot.ConditionFlags = InventoryRoutingNetwork.ConditionContainerRangePinned;
                    emptySlot.ReservedLock = 0u;
                    Slots[range.SlotStart + index] = emptySlot;
                    continue;
                }

                InventorySlotDTO slot = default;
                slot.ItemHashID = itemHash;
                slot.Quantity = quantity;
                slot.ContainerAUPHash = range.ContainerAUPHash;
                slot.ConditionFlags = flags;
                slot.ReservedLock = reservedLock;
                Slots[range.SlotStart + index] = slot;
                active++;
            }

            range.ActiveSlotCount = active;
            range.StateFlags = (range.StateFlags | InventoryRoutingNetwork.ContainerRangeActive) &
                ~(InventoryRoutingNetwork.ContainerRangeSyncFailed | InventoryRoutingNetwork.ContainerRangeMutating);
            WriteBackRange(in range);
            ResultRange[0] = range;
        }

        private bool TryResolveWritableRange(out InventoryContainerRangeDTO range)
        {
            range = default;
            int safeSlotCapacity = math.max(1, SlotCapacity);
            int safeSlotStride = math.max(1, SlotStride);
            if (safeSlotCapacity > safeSlotStride)
            {
                WriteFailure(InventoryRoutingNetwork.ContainerRangeCapacityExceeded);
                return false;
            }

            InventoryContainerRangeDTO* ranges =
                (InventoryContainerRangeDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ContainerRanges);
            long hashValue = unchecked((long)ContainerHash);

            for (int index = 0; index < ContainerRanges.Length; index++)
            {
                long* hashPtr = (long*)(&ranges[index].ContainerHash);
                ref long hashRef = ref UnsafeUtility.AsRef<long>(hashPtr);
                long observedHash = Interlocked.CompareExchange(ref hashRef, 0L, 0L);
                if (observedHash != hashValue)
                    continue;

                int* statePtr = (int*)(&ranges[index].StateFlags);
                if (!TryAcquireMutation(statePtr))
                {
                    WriteFailure(InventoryRoutingNetwork.ContainerRangeMutating);
                    return false;
                }

                range = ranges[index];
                if (!RangeFits(in range, safeSlotCapacity, safeSlotStride))
                {
                    range.StateFlags = (range.StateFlags | InventoryRoutingNetwork.ContainerRangeSyncFailed) &
                        ~InventoryRoutingNetwork.ContainerRangeMutating;
                    ranges[index] = range;
                    WriteFailure(InventoryRoutingNetwork.ContainerRangeCapacityExceeded);
                    return false;
                }

                range.ContainerAUPHash = ContainerAUPHash;
                range.StateFlags = (range.StateFlags | InventoryRoutingNetwork.ContainerRangeMutating) &
                    ~InventoryRoutingNetwork.ContainerRangeSyncFailed;
                ranges[index] = range;
                return true;
            }

            for (int index = 0; index < ContainerRanges.Length; index++)
            {
                long* hashPtr = (long*)(&ranges[index].ContainerHash);
                ref long hashRef = ref UnsafeUtility.AsRef<long>(hashPtr);
                if (Interlocked.CompareExchange(ref hashRef, 0L, 0L) != 0L)
                    continue;

                int* statePtr = (int*)(&ranges[index].StateFlags);
                int reservedState = Interlocked.CompareExchange(
                    ref UnsafeUtility.AsRef<int>(statePtr),
                    unchecked((int)InventoryRoutingNetwork.ContainerRangeMutating),
                    0);
                if (reservedState != 0)
                    continue;

                if (Interlocked.CompareExchange(ref hashRef, 0L, 0L) != 0L)
                {
                    Interlocked.Exchange(ref UnsafeUtility.AsRef<int>(statePtr), 0);
                    continue;
                }

                long slotStartLong = (long)index * safeSlotStride;
                if (slotStartLong < 0L || slotStartLong > int.MaxValue || slotStartLong + safeSlotStride > TotalSlotCapacity)
                {
                    Interlocked.Exchange(ref UnsafeUtility.AsRef<int>(statePtr), 0);
                    WriteFailure(InventoryRoutingNetwork.ContainerRangeCapacityExceeded);
                    return false;
                }

                range = default;
                range.ContainerHash = 0UL;
                range.ContainerAUPHash = ContainerAUPHash;
                range.SlotStart = (int)slotStartLong;
                range.SlotCapacity = safeSlotCapacity;
                range.ActiveSlotCount = 0;
                range.StateFlags = InventoryRoutingNetwork.ContainerRangeMutating;
                ranges[index] = range;

                Interlocked.Exchange(ref UnsafeUtility.AsRef<long>(hashPtr), hashValue);
                range.ContainerHash = ContainerHash;
                AtomicMaxRangeCount(index + 1);
                return true;
            }

            WriteFailure(InventoryRoutingNetwork.ContainerRangeCapacityExceeded);
            return false;
        }

        private bool RangeFits(in InventoryContainerRangeDTO range, int safeSlotCapacity, int safeSlotStride)
        {
            return (range.StateFlags & InventoryRoutingNetwork.ContainerRangeActive) != 0 &&
                   range.SlotCapacity >= safeSlotCapacity &&
                   range.SlotStart >= 0 &&
                   range.SlotStart % safeSlotStride == 0 &&
                   (long)range.SlotStart + range.SlotCapacity <= TotalSlotCapacity &&
                   range.SlotStart < Slots.Length;
        }

        private static bool TryAcquireMutation(int* statePtr)
        {
            ref int stateRef = ref UnsafeUtility.AsRef<int>(statePtr);
            while (true)
            {
                int observed = Interlocked.CompareExchange(ref stateRef, 0, 0);
                if ((observed & unchecked((int)InventoryRoutingNetwork.ContainerRangeMutating)) != 0)
                    return false;

                int next = (observed | unchecked((int)InventoryRoutingNetwork.ContainerRangeMutating)) &
                    ~unchecked((int)InventoryRoutingNetwork.ContainerRangeSyncFailed);
                if (Interlocked.CompareExchange(ref stateRef, next, observed) == observed)
                    return true;
            }
        }

        private void AtomicMaxRangeCount(int usedCount)
        {
            if (!ContainerRangeCount.IsCreated || ContainerRangeCount.Length == 0)
                return;

            int* countPtr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ContainerRangeCount);
            ref int countRef = ref UnsafeUtility.AsRef<int>(countPtr);
            while (true)
            {
                int observed = Interlocked.CompareExchange(ref countRef, 0, 0);
                if (observed >= usedCount)
                    return;

                if (Interlocked.CompareExchange(ref countRef, usedCount, observed) == observed)
                    return;
            }
        }

        private uint ResolveQuantity(int index, uint itemHash)
        {
            if (itemHash == 0u)
                return 0u;

            if (!ItemQuantities.IsCreated || index >= ItemQuantities.Length)
                return 1u;

            return ItemQuantities[index];
        }

        private uint ResolveReservedLock(int index)
        {
            return ReservedLocks.IsCreated && index < ReservedLocks.Length ? ReservedLocks[index] : 0u;
        }

        private uint ResolveConditionFlags(int index, uint itemHash, uint quantity, uint reservedLock)
        {
            uint flags = ConditionFlags.IsCreated && index < ConditionFlags.Length && ConditionFlags[index] != 0u
                ? ConditionFlags[index]
                : DefaultConditionFlags;
            flags |= InventoryRoutingNetwork.ConditionContainerRangePinned;
            if (itemHash == 0u || quantity == 0u)
                flags &= InventoryRoutingNetwork.ConditionContainerRangePinned;
            if (reservedLock != 0u)
                flags |= InventoryRoutingNetwork.ConditionLocked;
            return flags;
        }

        private void WriteBackRange(in InventoryContainerRangeDTO range)
        {
            if (!ContainerRanges.IsCreated || range.SlotCapacity <= 0)
                return;

            int rangeIndex = range.SlotStart / InventoryRoutingNetwork.DefaultContainerSlotStride;
            if ((uint)rangeIndex >= (uint)ContainerRanges.Length)
                return;
            if (range.SlotStart != rangeIndex * InventoryRoutingNetwork.DefaultContainerSlotStride)
                return;

            InventoryContainerRangeDTO current = ContainerRanges[rangeIndex];
            if (current.ContainerHash == range.ContainerHash)
                ContainerRanges[rangeIndex] = range;
        }

        private void WriteFailure(uint extraFlags = 0u)
        {
            if (!ResultRange.IsCreated || ResultRange.Length == 0)
                return;

            InventoryContainerRangeDTO failure = default;
            failure.ContainerHash = ContainerHash;
            failure.ContainerAUPHash = ContainerAUPHash;
            failure.SlotStart = -1;
            failure.SlotCapacity = 0;
            failure.ActiveSlotCount = 0;
            failure.StateFlags = InventoryRoutingNetwork.ContainerRangeSyncFailed | extraFlags;
            ResultRange[0] = failure;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ClearInventoryContainerRangeJob : IJob
    {
        [NoAlias] public NativeArray<InventorySlotDTO> Slots;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<InventoryContainerRangeDTO> ContainerRanges;
        [NoAlias] public NativeArray<InventoryContainerRangeDTO> ResultRange;
        public ulong ContainerHash;

        public void Execute()
        {
            if (!Slots.IsCreated ||
                !ContainerRanges.IsCreated ||
                !ResultRange.IsCreated ||
                ResultRange.Length == 0 ||
                ContainerHash == 0UL)
            {
                WriteFailure();
                return;
            }

            InventoryContainerRangeDTO* ranges = (InventoryContainerRangeDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ContainerRanges);
            long expected = unchecked((long)ContainerHash);
            for (int rangeIndex = 0; rangeIndex < ContainerRanges.Length; rangeIndex++)
            {
                long* hashPtr = (long*)(&ranges[rangeIndex].ContainerHash);
                if (Interlocked.CompareExchange(ref UnsafeUtility.AsRef<long>(hashPtr), 0L, 0L) != expected)
                    continue;

                InventoryContainerRangeDTO range = ranges[rangeIndex];
                if ((range.StateFlags & InventoryRoutingNetwork.ContainerRangeActive) == 0 ||
                    (range.StateFlags & InventoryRoutingNetwork.ContainerRangeMutating) != 0)
                {
                    WriteFailure();
                    return;
                }

                long observed = Interlocked.CompareExchange(ref UnsafeUtility.AsRef<long>(hashPtr), 0L, expected);
                if (observed == expected)
                {
                    ClearSlots(in range);
                    ranges[rangeIndex] = default;
                    ResultRange[0] = range;
                    return;
                }
            }

            WriteFailure();
        }

        private void ClearSlots(in InventoryContainerRangeDTO range)
        {
            if (range.SlotStart < 0 || range.SlotCapacity <= 0 || range.SlotStart >= Slots.Length)
                return;

            int slotCount = math.min(range.SlotCapacity, Slots.Length - range.SlotStart);
            for (int index = 0; index < slotCount; index++)
                Slots[range.SlotStart + index] = default;
        }

        private void WriteFailure()
        {
            if (ResultRange.IsCreated && ResultRange.Length > 0)
            {
                InventoryContainerRangeDTO failure = default;
                failure.ContainerHash = ContainerHash;
                failure.SlotStart = -1;
                failure.StateFlags = InventoryRoutingNetwork.ContainerRangeSyncFailed;
                ResultRange[0] = failure;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InventoryRollbackSnapshotJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<InventorySlotDTO> Slots;
        [NoAlias] public NativeArray<byte> DestinationBytes;
        [NoAlias] public NativeArray<int> ResultCounters;
        public int SlotCount;
        public int DestinationByteOffset;

        public void Execute()
        {
            if (!Slots.IsCreated || !DestinationBytes.IsCreated)
                return;

            int count = SlotCount > 0 ? math.min(SlotCount, Slots.Length) : Slots.Length;
            int stride = UnsafeUtility.SizeOf<InventorySlotDTO>();
            int byteCount = count * stride;
            if (count <= 0 ||
                DestinationByteOffset < 0 ||
                DestinationByteOffset + byteCount > DestinationBytes.Length)
            {
                WriteResult(0, 0);
                return;
            }

            void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Slots);
            byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(DestinationBytes) + DestinationByteOffset;
            UnsafeUtility.MemCpy(destination, source, byteCount);
            WriteResult(count, byteCount);
        }

        private void WriteResult(int copiedSlots, int copiedBytes)
        {
            if (!ResultCounters.IsCreated || ResultCounters.Length < 2)
                return;

            ResultCounters[0] = copiedSlots;
            ResultCounters[1] = copiedBytes;
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
            int activeCount = 0;
            for (int read = 0; read < limit; read++)
            {
                InventorySlotDTO slot = Slots[read];
                bool active = slot.ItemHashID != 0u && slot.Quantity != 0u;
                bool pinned = (slot.ConditionFlags & InventoryRoutingNetwork.ConditionContainerRangePinned) != 0u;
                if (pinned)
                {
                    if (active)
                        activeCount++;
                    if (write <= read)
                        write = read + 1;
                    continue;
                }

                if (!active)
                    continue;

                while (write < limit && (Slots[write].ConditionFlags & InventoryRoutingNetwork.ConditionContainerRangePinned) != 0u)
                    write++;

                if (write >= limit)
                    break;

                if (write != read)
                {
                    Slots[write] = slot;
                    Slots[read] = default;
                }

                write++;
                activeCount++;
            }

            InventorySlotDTO empty = default;
            for (int i = write; i < limit; i++)
            {
                if ((Slots[i].ConditionFlags & InventoryRoutingNetwork.ConditionContainerRangePinned) == 0u)
                    Slots[i] = empty;
            }

            if (ActiveSlotCount.IsCreated && ActiveSlotCount.Length > 0)
                ActiveSlotCount[0] = activeCount;
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

            InventoryRoutingTelemetryEntry telemetry = default;
            telemetry.FrameIndex = FrameIndex;
            telemetry.ActiveSlots = active;
            telemetry.EmptySlots = empty;
            telemetry.OrphanedSlots = orphaned;
            telemetry.QueryCount = QueryCount;
            telemetry.TransactionCount = TransactionCount;
            telemetry.ConflictCount = ConflictCount;
            telemetry.Flags = flags;
            telemetry.StateHash = stateHash;
            telemetry.QueryTimeEstimateUs = QueryTimeEstimateUs;
            telemetry.Fragmentation01 = fragmentation;
            telemetry.LastItemHash = lastHash;
            telemetry.LastContainerHashLo = lastContainerLo;
            TelemetryRing[cursor % TelemetryRing.Length] = telemetry;

            if (ActiveSlotCount.IsCreated && ActiveSlotCount.Length > 0)
                ActiveSlotCount[0] = unchecked((int)active);
        }
    }

#if UNITY_EDITOR
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

                InventoryStackLimitDTO stackLimit = default;
                stackLimit.ItemHashID = hash;
                stackLimit.MaxStack = maxStack;
                stackLimit.Flags = flags;
                StackLimits[written++] = stackLimit;
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
#endif
}
