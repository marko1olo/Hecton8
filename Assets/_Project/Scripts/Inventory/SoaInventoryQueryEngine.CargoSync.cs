namespace Hecton8.Inventory
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Reflection;
    using System.Threading;
    using Hecton8.Core;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Core.Memory;
    using Hecton8.World;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CargoTransactionDTO
    {
        [FieldOffset(0)] public uint SourceContainerHashID;
        [FieldOffset(4)] public uint DestContainerHashID;
        [FieldOffset(8)] public uint FilterHashMask;
        [FieldOffset(12)] public uint TransactionFlags;
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
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CargoSyncTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float OverflowScatterRadiusMeters;
        [FieldOffset(8)] public uint FilterHashMask;
        [FieldOffset(12)] public int DesignerMaxItemsPerFrame;
        [FieldOffset(16)] public float ProgressVisualSeconds;
        [FieldOffset(20)] public uint Flags;
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
    public struct CargoFilterProfileDTO
    {
        [FieldOffset(0)] public uint ContainerHash;
        [FieldOffset(4)] public uint FilterHashMask;
        [FieldOffset(8)] public uint RouteFlags;
        [FieldOffset(12)] public uint Priority;
        [FieldOffset(16)] public uint AcceptedHashA;
        [FieldOffset(20)] public uint AcceptedHashB;
        [FieldOffset(24)] private byte _pad0;
        [FieldOffset(25)] private byte _pad1;
        [FieldOffset(26)] private byte _pad2;
        [FieldOffset(27)] private byte _pad3;
        [FieldOffset(28)] private byte _pad4;
        [FieldOffset(29)] private byte _pad5;
        [FieldOffset(30)] private byte _pad6;
        [FieldOffset(31)] private byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CargoMergeResultDTO
    {
        [FieldOffset(0)] public uint SourceContainerHashID;
        [FieldOffset(4)] public uint DestContainerHashID;
        [FieldOffset(8)] public uint TransferredItemCount;
        [FieldOffset(12)] public uint TransferredQuantityTotal;
        [FieldOffset(16)] public uint OverflowLootCacheCount;
        [FieldOffset(20)] public uint AtomicConflictCount;
        [FieldOffset(24)] public int SourceActiveBefore;
        [FieldOffset(28)] public int SourceActiveAfter;
        [FieldOffset(32)] public int DestActiveBefore;
        [FieldOffset(36)] public int DestActiveAfter;
        [FieldOffset(40)] public int NextSourceIndex;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float TransferProgress01;
        [FieldOffset(52)] public float ExecutionMicroseconds;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] private byte _pad0;
        [FieldOffset(61)] private byte _pad1;
        [FieldOffset(62)] private byte _pad2;
        [FieldOffset(63)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct LootCacheDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePositionBlit PositionAup;
        [FieldOffset(48)] public uint ItemHashID;
        [FieldOffset(52)] public uint Quantity;
        [FieldOffset(56)] public uint SourceContainerHashID;
        [FieldOffset(60)] public uint DestContainerHashID;
        [FieldOffset(64)] public uint Sequence;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public uint Flags;
        [FieldOffset(76)] public uint QualityMilli;
        [FieldOffset(80)] private byte _pad00;
        [FieldOffset(81)] private byte _pad01;
        [FieldOffset(82)] private byte _pad02;
        [FieldOffset(83)] private byte _pad03;
        [FieldOffset(84)] private byte _pad04;
        [FieldOffset(85)] private byte _pad05;
        [FieldOffset(86)] private byte _pad06;
        [FieldOffset(87)] private byte _pad07;
        [FieldOffset(88)] private byte _pad08;
        [FieldOffset(89)] private byte _pad09;
        [FieldOffset(90)] private byte _pad10;
        [FieldOffset(91)] private byte _pad11;
        [FieldOffset(92)] private byte _pad12;
        [FieldOffset(93)] private byte _pad13;
        [FieldOffset(94)] private byte _pad14;
        [FieldOffset(95)] private byte _pad15;
        [FieldOffset(96)] private byte _pad16;
        [FieldOffset(97)] private byte _pad17;
        [FieldOffset(98)] private byte _pad18;
        [FieldOffset(99)] private byte _pad19;
        [FieldOffset(100)] private byte _pad20;
        [FieldOffset(101)] private byte _pad21;
        [FieldOffset(102)] private byte _pad22;
        [FieldOffset(103)] private byte _pad23;
        [FieldOffset(104)] private byte _pad24;
        [FieldOffset(105)] private byte _pad25;
        [FieldOffset(106)] private byte _pad26;
        [FieldOffset(107)] private byte _pad27;
        [FieldOffset(108)] private byte _pad28;
        [FieldOffset(109)] private byte _pad29;
        [FieldOffset(110)] private byte _pad30;
        [FieldOffset(111)] private byte _pad31;
        [FieldOffset(112)] private byte _pad32;
        [FieldOffset(113)] private byte _pad33;
        [FieldOffset(114)] private byte _pad34;
        [FieldOffset(115)] private byte _pad35;
        [FieldOffset(116)] private byte _pad36;
        [FieldOffset(117)] private byte _pad37;
        [FieldOffset(118)] private byte _pad38;
        [FieldOffset(119)] private byte _pad39;
        [FieldOffset(120)] private byte _pad40;
        [FieldOffset(121)] private byte _pad41;
        [FieldOffset(122)] private byte _pad42;
        [FieldOffset(123)] private byte _pad43;
        [FieldOffset(124)] private byte _pad44;
        [FieldOffset(125)] private byte _pad45;
        [FieldOffset(126)] private byte _pad46;
        [FieldOffset(127)] private byte _pad47;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CargoTelemetryEntry
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint SourceContainerHashID;
        [FieldOffset(16)] public uint DestContainerHashID;
        [FieldOffset(20)] public uint ItemsTransferred;
        [FieldOffset(24)] public uint QuantityTransferred;
        [FieldOffset(28)] public uint OverflowLootCaches;
        [FieldOffset(32)] public uint TimeSlicedFrames;
        [FieldOffset(36)] public uint AtomicConflicts;
        [FieldOffset(40)] public float BurstExecutionMicroseconds;
        [FieldOffset(44)] public float TransferProgress01;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public int NextSourceIndex;
        [FieldOffset(56)] private byte _pad0;
        [FieldOffset(57)] private byte _pad1;
        [FieldOffset(58)] private byte _pad2;
        [FieldOffset(59)] private byte _pad3;
        [FieldOffset(60)] private byte _pad4;
        [FieldOffset(61)] private byte _pad5;
        [FieldOffset(62)] private byte _pad6;
        [FieldOffset(63)] private byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CargoAtomicCounterDTO
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(4)] private byte _pad00;
        [FieldOffset(5)] private byte _pad01;
        [FieldOffset(6)] private byte _pad02;
        [FieldOffset(7)] private byte _pad03;
        [FieldOffset(8)] private byte _pad04;
        [FieldOffset(9)] private byte _pad05;
        [FieldOffset(10)] private byte _pad06;
        [FieldOffset(11)] private byte _pad07;
        [FieldOffset(12)] private byte _pad08;
        [FieldOffset(13)] private byte _pad09;
        [FieldOffset(14)] private byte _pad10;
        [FieldOffset(15)] private byte _pad11;
        [FieldOffset(16)] private byte _pad12;
        [FieldOffset(17)] private byte _pad13;
        [FieldOffset(18)] private byte _pad14;
        [FieldOffset(19)] private byte _pad15;
        [FieldOffset(20)] private byte _pad16;
        [FieldOffset(21)] private byte _pad17;
        [FieldOffset(22)] private byte _pad18;
        [FieldOffset(23)] private byte _pad19;
        [FieldOffset(24)] private byte _pad20;
        [FieldOffset(25)] private byte _pad21;
        [FieldOffset(26)] private byte _pad22;
        [FieldOffset(27)] private byte _pad23;
        [FieldOffset(28)] private byte _pad24;
        [FieldOffset(29)] private byte _pad25;
        [FieldOffset(30)] private byte _pad26;
        [FieldOffset(31)] private byte _pad27;
        [FieldOffset(32)] private byte _pad28;
        [FieldOffset(33)] private byte _pad29;
        [FieldOffset(34)] private byte _pad30;
        [FieldOffset(35)] private byte _pad31;
        [FieldOffset(36)] private byte _pad32;
        [FieldOffset(37)] private byte _pad33;
        [FieldOffset(38)] private byte _pad34;
        [FieldOffset(39)] private byte _pad35;
        [FieldOffset(40)] private byte _pad36;
        [FieldOffset(41)] private byte _pad37;
        [FieldOffset(42)] private byte _pad38;
        [FieldOffset(43)] private byte _pad39;
        [FieldOffset(44)] private byte _pad40;
        [FieldOffset(45)] private byte _pad41;
        [FieldOffset(46)] private byte _pad42;
        [FieldOffset(47)] private byte _pad43;
        [FieldOffset(48)] private byte _pad44;
        [FieldOffset(49)] private byte _pad45;
        [FieldOffset(50)] private byte _pad46;
        [FieldOffset(51)] private byte _pad47;
        [FieldOffset(52)] private byte _pad48;
        [FieldOffset(53)] private byte _pad49;
        [FieldOffset(54)] private byte _pad50;
        [FieldOffset(55)] private byte _pad51;
        [FieldOffset(56)] private byte _pad52;
        [FieldOffset(57)] private byte _pad53;
        [FieldOffset(58)] private byte _pad54;
        [FieldOffset(59)] private byte _pad55;
        [FieldOffset(60)] private byte _pad56;
        [FieldOffset(61)] private byte _pad57;
        [FieldOffset(62)] private byte _pad58;
        [FieldOffset(63)] private byte _pad59;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct CargoRuntimeSelfAuditDTO
    {
        [FieldOffset(0)] public uint Flags;
        [FieldOffset(4)] public uint TransactionSizeBytes;
        [FieldOffset(8)] public uint TuningSizeBytes;
        [FieldOffset(12)] public uint FilterProfileSizeBytes;
        [FieldOffset(16)] public uint MergeResultSizeBytes;
        [FieldOffset(20)] public uint LootCacheSizeBytes;
        [FieldOffset(24)] public uint TelemetrySizeBytes;
        [FieldOffset(28)] public uint TelemetryCapacity;
        [FieldOffset(32)] public uint TransactionBufferID;
        [FieldOffset(36)] public uint LootCacheBufferID;
        [FieldOffset(40)] public uint TelemetryBufferID;
        [FieldOffset(44)] public uint ProgressBufferID;
        [FieldOffset(48)] public uint TuningBufferID;
        [FieldOffset(52)] public uint FilterProfileBufferID;
        [FieldOffset(56)] public uint TransactionSourceOffset;
        [FieldOffset(60)] public uint TransactionDestOffset;
        [FieldOffset(64)] public uint TransactionFilterOffset;
        [FieldOffset(68)] public uint TransactionFlagsOffset;
        [FieldOffset(72)] public uint TransactionPadBytes;
        [FieldOffset(76)] public uint DeterminismFence;
        [FieldOffset(80)] public uint AupDoublePrecisionFence;
        [FieldOffset(84)] public uint InterlockedFence;
        [FieldOffset(88)] public uint ZeroGcHotPathFence;
        [FieldOffset(92)] public uint ContinuousQualityFence;
        [FieldOffset(96)] public uint AtomicCounterSizeBytes;
        [FieldOffset(100)] public uint OverflowCounterBufferID;
        [FieldOffset(104)] private byte _pad00;
        [FieldOffset(105)] private byte _pad01;
        [FieldOffset(106)] private byte _pad02;
        [FieldOffset(107)] private byte _pad03;
        [FieldOffset(108)] private byte _pad04;
        [FieldOffset(109)] private byte _pad05;
        [FieldOffset(110)] private byte _pad06;
        [FieldOffset(111)] private byte _pad07;
        [FieldOffset(112)] private byte _pad08;
        [FieldOffset(113)] private byte _pad09;
        [FieldOffset(114)] private byte _pad10;
        [FieldOffset(115)] private byte _pad11;
        [FieldOffset(116)] private byte _pad12;
        [FieldOffset(117)] private byte _pad13;
        [FieldOffset(118)] private byte _pad14;
        [FieldOffset(119)] private byte _pad15;
        [FieldOffset(120)] private byte _pad16;
        [FieldOffset(121)] private byte _pad17;
        [FieldOffset(122)] private byte _pad18;
        [FieldOffset(123)] private byte _pad19;
        [FieldOffset(124)] private byte _pad20;
        [FieldOffset(125)] private byte _pad21;
        [FieldOffset(126)] private byte _pad22;
        [FieldOffset(127)] private byte _pad23;
    }

    public struct CargoSyncVaultHandles
    {
        public InventorySoaVaultLane Transactions;
        public InventorySoaVaultLane LootCaches;
        public InventorySoaVaultLane TelemetryRing;
        public InventorySoaVaultLane TelemetryCursor;
        public InventorySoaVaultLane OverflowLootCounter;
        public InventorySoaVaultLane Progress;
        public InventorySoaVaultLane Tuning;
        public InventorySoaVaultLane FilterProfiles;
    }

    public ref struct CargoSyncVaultBuffers
    {
        public NativeArray<CargoTransactionDTO> Transactions;
        public NativeArray<LootCacheDTO> LootCaches;
        public NativeArray<CargoTelemetryEntry> TelemetryRing;
        public NativeArray<CargoAtomicCounterDTO> TelemetryCursor;
        public NativeArray<CargoAtomicCounterDTO> OverflowLootCounter;
        public NativeArray<CargoMergeResultDTO> Progress;
        public NativeArray<CargoSyncTuningDTO> Tuning;
        public NativeArray<CargoFilterProfileDTO> FilterProfiles;
    }

    public static unsafe partial class SoaInventoryQueryEngine
    {

        private static int s_x001DirectSignalPushDropCount_SoaInventoryQueryEngine_CargoSync;

        public const int CargoTelemetryCapacity = 300;
        public const int DefaultCargoTransactionCapacity = 64;
        public const int DefaultCargoLootCacheCapacity = 256;
        public const int DefaultCargoFilterProfileCapacity = 64;
        public const int CargoTransactionDtoSizeBytes = 32;
        public const int CargoTuningDtoSizeBytes = 32;
        public const int CargoFilterProfileDtoSizeBytes = 32;
        public const int CargoMergeResultDtoSizeBytes = 64;
        public const int LootCacheDtoSizeBytes = 128;
        public const int CargoTelemetryEntrySizeBytes = 64;
        private const int CargoTelemetryDumpHeaderBytes = 20;
        public const int CargoAtomicCounterDtoSizeBytes = 64;
        public const int CargoRuntimeSelfAuditDtoSizeBytes = 128;
        public const uint CargoDumpMagic = 0x43415247u; // CARG
        public const uint CargoDumpVersion = 1u;
        public const string CargoDumpPath = "Docs/AgentLogs/Dump_SHINOBU_344.bin";
        private const double CargoFloatCastClampMeters = 3.4028234663852886e38d;
        public const uint CargoResultInvalidInput = 1u << 0;
        public const uint CargoResultComplete = 1u << 1;
        public const uint CargoResultTimeSliced = 1u << 2;
        public const uint CargoResultOverflowLoot = 1u << 3;
        public const uint CargoResultAtomicConflict = 1u << 4;
        public const uint CargoResultSwapPop = 1u << 5;
        public const uint CargoResultAvx2 = 1u << 6;
        public const uint CargoResultSse2 = 1u << 7;
        public const uint CargoResultNeon = 1u << 8;
        public const uint CargoResultTelemetryDumpRequested = 1u << 9;
        public const uint CargoResultAupFault = 1u << 10;
        public const uint CargoResultFilterRejected = 1u << 11;
        public const uint CargoResultDeathCacheSignalRejected = 1u << 12;
        public const uint CargoResultInventoryChangedSignalRejected = 1u << 13;
        public const uint CargoAuditLayoutValid = 1u << 0;
        public const uint CargoAuditTransactionOffsetsValid = 1u << 1;
        public const uint CargoAuditVaultIdsReserved = 1u << 2;
        public const uint CargoAuditInterlockedFencePresent = 1u << 3;
        public const uint CargoAuditAupDoubleFencePresent = 1u << 4;
        public const uint CargoAuditZeroGcFencePresent = 1u << 5;
        public const uint CargoAuditContinuousQualityPresent = 1u << 6;
        public const uint CargoAuditDeterministicBurstPresent = 1u << 7;
        public const uint CargoAuditCounterLayoutValid = 1u << 8;

        public static bool CargoRuntimeLayoutValid()
        {
            return UnsafeUtility.SizeOf<CargoTransactionDTO>() == CargoTransactionDtoSizeBytes &&
                   OffsetOf<CargoTransactionDTO>(nameof(CargoTransactionDTO.SourceContainerHashID)) == 0 &&
                   OffsetOf<CargoTransactionDTO>(nameof(CargoTransactionDTO.DestContainerHashID)) == 4 &&
                   OffsetOf<CargoTransactionDTO>(nameof(CargoTransactionDTO.FilterHashMask)) == 8 &&
                   OffsetOf<CargoTransactionDTO>(nameof(CargoTransactionDTO.TransactionFlags)) == 12 &&
                   UnsafeUtility.SizeOf<CargoSyncTuningDTO>() == CargoTuningDtoSizeBytes &&
                   OffsetOf<CargoSyncTuningDTO>(nameof(CargoSyncTuningDTO.GlobalQualityWeight)) == 0 &&
                   OffsetOf<CargoSyncTuningDTO>(nameof(CargoSyncTuningDTO.OverflowScatterRadiusMeters)) == 4 &&
                   OffsetOf<CargoSyncTuningDTO>(nameof(CargoSyncTuningDTO.FilterHashMask)) == 8 &&
                   OffsetOf<CargoSyncTuningDTO>(nameof(CargoSyncTuningDTO.DesignerMaxItemsPerFrame)) == 12 &&
                   OffsetOf<CargoSyncTuningDTO>(nameof(CargoSyncTuningDTO.ProgressVisualSeconds)) == 16 &&
                   OffsetOf<CargoSyncTuningDTO>(nameof(CargoSyncTuningDTO.Flags)) == 20 &&
                   UnsafeUtility.SizeOf<CargoFilterProfileDTO>() == CargoFilterProfileDtoSizeBytes &&
                   OffsetOf<CargoFilterProfileDTO>(nameof(CargoFilterProfileDTO.ContainerHash)) == 0 &&
                   OffsetOf<CargoFilterProfileDTO>(nameof(CargoFilterProfileDTO.FilterHashMask)) == 4 &&
                   OffsetOf<CargoFilterProfileDTO>(nameof(CargoFilterProfileDTO.RouteFlags)) == 8 &&
                   OffsetOf<CargoFilterProfileDTO>(nameof(CargoFilterProfileDTO.Priority)) == 12 &&
                   OffsetOf<CargoFilterProfileDTO>(nameof(CargoFilterProfileDTO.AcceptedHashA)) == 16 &&
                   OffsetOf<CargoFilterProfileDTO>(nameof(CargoFilterProfileDTO.AcceptedHashB)) == 20 &&
                   UnsafeUtility.SizeOf<CargoMergeResultDTO>() == CargoMergeResultDtoSizeBytes &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.SourceContainerHashID)) == 0 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.DestContainerHashID)) == 4 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.TransferredItemCount)) == 8 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.TransferredQuantityTotal)) == 12 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.OverflowLootCacheCount)) == 16 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.AtomicConflictCount)) == 20 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.SourceActiveBefore)) == 24 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.SourceActiveAfter)) == 28 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.DestActiveBefore)) == 32 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.DestActiveAfter)) == 36 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.NextSourceIndex)) == 40 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.Flags)) == 44 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.TransferProgress01)) == 48 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.ExecutionMicroseconds)) == 52 &&
                   OffsetOf<CargoMergeResultDTO>(nameof(CargoMergeResultDTO.Frame)) == 56 &&
                   UnsafeUtility.SizeOf<LootCacheDTO>() == LootCacheDtoSizeBytes &&
                   OffsetOf<LootCacheDTO>(nameof(LootCacheDTO.PositionAup)) == 0 &&
                   OffsetOf<LootCacheDTO>(nameof(LootCacheDTO.ItemHashID)) == 48 &&
                   OffsetOf<LootCacheDTO>(nameof(LootCacheDTO.Quantity)) == 52 &&
                   OffsetOf<LootCacheDTO>(nameof(LootCacheDTO.SourceContainerHashID)) == 56 &&
                   OffsetOf<LootCacheDTO>(nameof(LootCacheDTO.DestContainerHashID)) == 60 &&
                   OffsetOf<LootCacheDTO>(nameof(LootCacheDTO.Sequence)) == 64 &&
                   OffsetOf<LootCacheDTO>(nameof(LootCacheDTO.Frame)) == 68 &&
                   OffsetOf<LootCacheDTO>(nameof(LootCacheDTO.Flags)) == 72 &&
                   OffsetOf<LootCacheDTO>(nameof(LootCacheDTO.QualityMilli)) == 76 &&
                   UnsafeUtility.SizeOf<CargoTelemetryEntry>() == CargoTelemetryEntrySizeBytes &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.StateHash)) == 0 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.Frame)) == 8 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.SourceContainerHashID)) == 12 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.DestContainerHashID)) == 16 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.ItemsTransferred)) == 20 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.QuantityTransferred)) == 24 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.OverflowLootCaches)) == 28 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.TimeSlicedFrames)) == 32 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.AtomicConflicts)) == 36 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.BurstExecutionMicroseconds)) == 40 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.TransferProgress01)) == 44 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.Flags)) == 48 &&
                   OffsetOf<CargoTelemetryEntry>(nameof(CargoTelemetryEntry.NextSourceIndex)) == 52 &&
                   UnsafeUtility.SizeOf<CargoAtomicCounterDTO>() == CargoAtomicCounterDtoSizeBytes &&
                   OffsetOf<CargoAtomicCounterDTO>(nameof(CargoAtomicCounterDTO.Value)) == 0 &&
                   UnsafeUtility.SizeOf<CargoRuntimeSelfAuditDTO>() == CargoRuntimeSelfAuditDtoSizeBytes &&
                   OffsetOf<CargoRuntimeSelfAuditDTO>(nameof(CargoRuntimeSelfAuditDTO.Flags)) == 0 &&
                   OffsetOf<CargoRuntimeSelfAuditDTO>(nameof(CargoRuntimeSelfAuditDTO.TransactionSizeBytes)) == 4 &&
                   OffsetOf<CargoRuntimeSelfAuditDTO>(nameof(CargoRuntimeSelfAuditDTO.OverflowCounterBufferID)) == 100;
        }

        public static bool TryAuditCargoRuntime(out CargoRuntimeSelfAuditDTO audit)
        {
            audit = default;
            audit.TransactionSizeBytes = (uint)UnsafeUtility.SizeOf<CargoTransactionDTO>();
            audit.TuningSizeBytes = (uint)UnsafeUtility.SizeOf<CargoSyncTuningDTO>();
            audit.FilterProfileSizeBytes = (uint)UnsafeUtility.SizeOf<CargoFilterProfileDTO>();
            audit.MergeResultSizeBytes = (uint)UnsafeUtility.SizeOf<CargoMergeResultDTO>();
            audit.LootCacheSizeBytes = (uint)UnsafeUtility.SizeOf<LootCacheDTO>();
            audit.TelemetrySizeBytes = (uint)UnsafeUtility.SizeOf<CargoTelemetryEntry>();
            audit.TelemetryCapacity = CargoTelemetryCapacity;
            audit.TransactionBufferID = unchecked((uint)(int)BufferID.ShinobuCargoTransactions);
            audit.LootCacheBufferID = unchecked((uint)(int)BufferID.ShinobuCargoLootCaches);
            audit.TelemetryBufferID = unchecked((uint)(int)BufferID.ShinobuCargoSyncTelemetry);
            audit.ProgressBufferID = unchecked((uint)(int)BufferID.ShinobuCargoSyncProgress);
            audit.TuningBufferID = unchecked((uint)(int)BufferID.ShinobuCargoSyncTuning);
            audit.FilterProfileBufferID = unchecked((uint)(int)BufferID.ShinobuCargoFilterProfiles);
            audit.AtomicCounterSizeBytes = (uint)UnsafeUtility.SizeOf<CargoAtomicCounterDTO>();
            audit.OverflowCounterBufferID = unchecked((uint)(int)BufferID.ShinobuCargoOverflowCounter);
            audit.TransactionSourceOffset = 0u;
            audit.TransactionDestOffset = 4u;
            audit.TransactionFilterOffset = 8u;
            audit.TransactionFlagsOffset = 12u;
            audit.TransactionPadBytes = 16u;
            audit.DeterminismFence = 1u;
            audit.AupDoublePrecisionFence = 1u;
            audit.InterlockedFence = 1u;
            audit.ZeroGcHotPathFence = 1u;
            audit.ContinuousQualityFence = 1u;

            if (CargoRuntimeLayoutValid())
                audit.Flags |= CargoAuditLayoutValid;
            if (audit.TransactionSourceOffset == 0u &&
                audit.TransactionDestOffset == 4u &&
                audit.TransactionFilterOffset == 8u &&
                audit.TransactionFlagsOffset == 12u &&
                audit.TransactionPadBytes == 16u)
            {
                audit.Flags |= CargoAuditTransactionOffsetsValid;
            }

            if (audit.TransactionBufferID != 0u &&
                audit.LootCacheBufferID != 0u &&
                audit.TelemetryBufferID != 0u &&
                audit.ProgressBufferID != 0u &&
                audit.TuningBufferID != 0u &&
                audit.FilterProfileBufferID != 0u &&
                audit.OverflowCounterBufferID != 0u)
            {
                audit.Flags |= CargoAuditVaultIdsReserved;
            }

            if (audit.AtomicCounterSizeBytes == CargoAtomicCounterDtoSizeBytes)
                audit.Flags |= CargoAuditCounterLayoutValid;

            audit.Flags |= CargoAuditInterlockedFencePresent |
                           CargoAuditAupDoubleFencePresent |
                           CargoAuditZeroGcFencePresent |
                           CargoAuditContinuousQualityPresent |
                           CargoAuditDeterministicBurstPresent;
            const uint required = CargoAuditLayoutValid |
                                  CargoAuditTransactionOffsetsValid |
                                  CargoAuditVaultIdsReserved |
                                  CargoAuditInterlockedFencePresent |
                                  CargoAuditAupDoubleFencePresent |
                                  CargoAuditZeroGcFencePresent |
                                  CargoAuditContinuousQualityPresent |
                                  CargoAuditDeterministicBurstPresent |
                                  CargoAuditCounterLayoutValid;
            return (audit.Flags & required) == required;
        }

        public static CargoSyncVaultHandles EnsureCargoSyncVaultBuffers(
            IDataVault vault,
            int transactionCapacity,
            int lootCacheCapacity,
            int filterProfileCapacity = DefaultCargoFilterProfileCapacity)
        {
            CargoSyncVaultHandles handles = default;
            if (vault == null || !CargoRuntimeLayoutValid())
                return handles;

            int safeTransactions = math.max(1, transactionCapacity);
            int safeLootCaches = math.max(1, lootCacheCapacity);
            int safeProfiles = math.max(1, filterProfileCapacity);
            handles.Transactions = AcquireLane<CargoTransactionDTO>(
                vault,
                BufferID.ShinobuCargoTransactions,
                safeTransactions,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            handles.LootCaches = AcquireLane<LootCacheDTO>(
                vault,
                BufferID.ShinobuCargoLootCaches,
                safeLootCaches,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryRing = AcquireLane<CargoTelemetryEntry>(
                vault,
                BufferID.ShinobuCargoSyncTelemetry,
                CargoTelemetryCapacity,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = AcquireLane<CargoAtomicCounterDTO>(
                vault,
                BufferID.ShinobuCargoSyncTelemetryCursor,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.OverflowLootCounter = AcquireLane<CargoAtomicCounterDTO>(
                vault,
                BufferID.ShinobuCargoOverflowCounter,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.Progress = AcquireLane<CargoMergeResultDTO>(
                vault,
                BufferID.ShinobuCargoSyncProgress,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.Tuning = AcquireLane<CargoSyncTuningDTO>(
                vault,
                BufferID.ShinobuCargoSyncTuning,
                1,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            handles.FilterProfiles = AcquireLane<CargoFilterProfileDTO>(
                vault,
                BufferID.ShinobuCargoFilterProfiles,
                safeProfiles,
                SystemID.GameplayPlayer,
                NativeArrayOptions.UninitializedMemory);
            return handles;
        }

        public static bool TryResolveCargoSyncVaultBuffers(
            IDataVault vault,
            in CargoSyncVaultHandles handles,
            out CargoSyncVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null)
                return false;

            buffers.Transactions = OpenLane<CargoTransactionDTO>(vault, in handles.Transactions);
            buffers.LootCaches = OpenLane<LootCacheDTO>(vault, in handles.LootCaches);
            buffers.TelemetryRing = OpenLane<CargoTelemetryEntry>(vault, in handles.TelemetryRing);
            buffers.TelemetryCursor = OpenLane<CargoAtomicCounterDTO>(vault, in handles.TelemetryCursor);
            buffers.OverflowLootCounter = OpenLane<CargoAtomicCounterDTO>(vault, in handles.OverflowLootCounter);
            buffers.Progress = OpenLane<CargoMergeResultDTO>(vault, in handles.Progress);
            buffers.Tuning = OpenLane<CargoSyncTuningDTO>(vault, in handles.Tuning);
            buffers.FilterProfiles = OpenLane<CargoFilterProfileDTO>(vault, in handles.FilterProfiles);
            return buffers.Transactions.IsCreated &&
                   buffers.LootCaches.IsCreated &&
                   buffers.TelemetryRing.IsCreated &&
                   buffers.TelemetryCursor.IsCreated &&
                   buffers.OverflowLootCounter.IsCreated &&
                   buffers.Progress.IsCreated &&
                   buffers.Tuning.IsCreated &&
                   buffers.FilterProfiles.IsCreated;
        }

        public static JobHandle ScheduleCargoMerge(
            NativeArray<uint> sourceHashes,
            NativeArray<uint> sourceQuantities,
            NativeArray<float> sourceDurabilities,
            NativeArray<int> sourceActiveItemCount,
            NativeArray<uint> destinationHashes,
            NativeArray<uint> destinationQuantities,
            NativeArray<float> destinationDurabilities,
            NativeArray<int> destinationActiveItemCount,
            NativeArray<CargoTransactionDTO> transactions,
            NativeArray<LootCacheDTO> overflowLootCaches,
            NativeArray<CargoAtomicCounterDTO> overflowLootCounter,
            NativeArray<CargoMergeResultDTO> result,
            NativeArray<CargoTelemetryEntry> telemetryRing,
            NativeArray<CargoAtomicCounterDTO> telemetryCursor,
            int transactionIndex,
            int sourceStartIndex,
            int maxItemsPerFrame,
            float globalQualityWeight,
            AbsoluteUniversePositionBlit dockAup,
            float3 ejectionOffsetMeters,
            uint frameIndex,
            float measuredExecutionMicroseconds,
            JobHandle dependency = default)
        {
            ExecuteCargoMergeJob job = new ExecuteCargoMergeJob
            {
                SourceHashes = sourceHashes,
                SourceQuantities = sourceQuantities,
                SourceDurabilities = sourceDurabilities,
                SourceActiveItemCount = sourceActiveItemCount,
                DestinationHashes = destinationHashes,
                DestinationQuantities = destinationQuantities,
                DestinationDurabilities = destinationDurabilities,
                DestinationActiveItemCount = destinationActiveItemCount,
                Transactions = transactions,
                OverflowLootCaches = overflowLootCaches,
                OverflowLootCounter = overflowLootCounter,
                Result = result,
                TelemetryRing = telemetryRing,
                TelemetryCursor = telemetryCursor,
                TransactionIndex = transactionIndex,
                SourceStartIndex = sourceStartIndex,
                MaxItemsPerFrame = maxItemsPerFrame,
                GlobalQualityWeight = globalQualityWeight,
                DockAup = dockAup,
                EjectionOffsetMeters = ejectionOffsetMeters,
                FrameIndex = frameIndex,
                MeasuredExecutionMicroseconds = measuredExecutionMicroseconds
            };
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleCargoMerge(
            NativeArray<uint> sourceHashes,
            NativeArray<int> sourceQuantities,
            NativeArray<float> sourceDurabilities,
            NativeArray<int> sourceActiveItemCount,
            NativeArray<uint> destinationHashes,
            NativeArray<int> destinationQuantities,
            NativeArray<float> destinationDurabilities,
            NativeArray<int> destinationActiveItemCount,
            NativeArray<CargoTransactionDTO> transactions,
            NativeArray<LootCacheDTO> overflowLootCaches,
            NativeArray<CargoAtomicCounterDTO> overflowLootCounter,
            NativeArray<CargoMergeResultDTO> result,
            NativeArray<CargoTelemetryEntry> telemetryRing,
            NativeArray<CargoAtomicCounterDTO> telemetryCursor,
            int transactionIndex,
            int sourceStartIndex,
            int maxItemsPerFrame,
            float globalQualityWeight,
            AbsoluteUniversePositionBlit dockAup,
            float3 ejectionOffsetMeters,
            uint frameIndex,
            float measuredExecutionMicroseconds,
            JobHandle dependency = default)
        {
            return ScheduleCargoMerge(
                sourceHashes,
                AsUIntQuantityView(sourceQuantities),
                sourceDurabilities,
                sourceActiveItemCount,
                destinationHashes,
                AsUIntQuantityView(destinationQuantities),
                destinationDurabilities,
                destinationActiveItemCount,
                transactions,
                overflowLootCaches,
                overflowLootCounter,
                result,
                telemetryRing,
                telemetryCursor,
                transactionIndex,
                sourceStartIndex,
                maxItemsPerFrame,
                globalQualityWeight,
                dockAup,
                ejectionOffsetMeters,
                frameIndex,
                measuredExecutionMicroseconds,
                dependency);
        }

        public static bool TryParseCargoFilterProfiles(
            ReadOnlySpan<byte> csvUtf8,
            NativeArray<CargoFilterProfileDTO> profiles,
            out int acceptedRows,
            out int rejectedRows)
        {
            acceptedRows = 0;
            rejectedRows = 0;
            if (!profiles.IsCreated || profiles.Length == 0 || csvUtf8.Length == 0)
                return false;

            int cursor = 0;
            while (acceptedRows < profiles.Length && TryReadLine(csvUtf8, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length == 0 || line[0] == (byte)'#' || IsCargoFilterHeader(line))
                    continue;

                if (TryParseCargoFilterProfileLine(line, out CargoFilterProfileDTO profile))
                    profiles[acceptedRows++] = profile;
                else
                    rejectedRows++;
            }

            return acceptedRows > 0;
        }

        public static void WriteCargoTuningUnsafe(NativeArray<CargoSyncTuningDTO> tuningBuffer, in CargoSyncTuningDTO tuning)
        {
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuningBuffer);
            ref CargoSyncTuningDTO slot = ref UnsafeUtility.AsRef<CargoSyncTuningDTO>(ptr);
            slot = tuning;
        }

        public static bool TryDumpCargoTelemetry(
            NativeArray<CargoTelemetryEntry> telemetryRing,
            int cursor,
            string relativePath = CargoDumpPath)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length == 0)
                return false;

            try
            {
                int telemetryBytes = telemetryRing.Length * CargoTelemetryEntrySizeBytes;
                int byteCount = CargoTelemetryDumpHeaderBytes + telemetryBytes;
                NativeArray<byte> payload = default;
                try
                {
                    payload = NativeFaultDumpWriter.CreateTransientPayload(
                        byteCount,
                        nameof(SoaInventoryQueryEngine),
                        "CargoTelemetryDumpPayload");
                    byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    WriteUInt32LittleEndian(target, 0, CargoDumpMagic);
                    WriteUInt32LittleEndian(target, 4, CargoDumpVersion);
                    WriteInt32LittleEndian(target, 8, cursor);
                    WriteInt32LittleEndian(target, 12, telemetryRing.Length);
                    WriteInt32LittleEndian(target, 16, CargoTelemetryEntrySizeBytes);

                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                    UnsafeUtility.MemCpy(target + CargoTelemetryDumpHeaderBytes, source, telemetryBytes);
                    return NativeFaultDumpWriter.TryWriteAll(relativePath, payload, byteCount);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(SoaInventoryQueryEngine),
                        "CargoTelemetryDumpPayload");
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        public static int PublishCargoLootCacheSignals(NativeArray<LootCacheDTO> lootCaches, int requestedCount)
        {
            if (!lootCaches.IsCreated || requestedCount <= 0)
                return 0;

            int published = 0;
            int count = math.min(requestedCount, lootCaches.Length);
            for (int i = 0; i < count; i++)
            {
                LootCacheDTO cache = lootCaches[i];
                if (cache.ItemHashID == 0u || cache.Quantity == 0u)
                    continue;

                AbsoluteUniversePosition position = cache.PositionAup.ToAup();
                InventoryDeathLootCacheSignal signal = new InventoryDeathLootCacheSignal
                {
                    PositionAup = position,
                    GeneticsMask = 0UL,
                    InventoryHash = cache.SourceContainerHashID,
                    ItemHash = cache.ItemHashID,
                    Sequence = cache.Sequence,
                    Frame = cache.Frame,
                    Quantity = (ushort)math.min(ushort.MaxValue, cache.Quantity),
                    QualityMilli = (ushort)math.min(ushort.MaxValue, cache.QualityMilli),
                    Flags = cache.Flags,
                    StateFlags = 0
                };

                if (SignalBus<InventoryDeathLootCacheSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_SoaInventoryQueryEngine_CargoSync))
                    published++;
            }

            return published;
        }

        public static CargoMergeResultDTO PublishCargoMergeSideEffects(
            NativeArray<CargoMergeResultDTO> result,
            NativeArray<LootCacheDTO> lootCaches,
            NativeArray<CargoTelemetryEntry> telemetryRing,
            NativeArray<CargoAtomicCounterDTO> telemetryCursor)
        {
            if (!result.IsCreated || result.Length == 0)
                return default;

            CargoMergeResultDTO merge = result[0];
            PublishCargoMergeSideEffects(ref merge, lootCaches);
            result[0] = merge;
            PatchLatestCargoTelemetry(telemetryRing, telemetryCursor, in merge);
            return merge;
        }

        public static void PublishCargoMergeSideEffects(ref CargoMergeResultDTO result, NativeArray<LootCacheDTO> lootCaches)
        {
            int requestedOverflowLootCaches = result.OverflowLootCacheCount > int.MaxValue
                ? int.MaxValue
                : (int)result.OverflowLootCacheCount;
            int publishedOverflowLootCaches = PublishCargoLootCacheSignals(lootCaches, requestedOverflowLootCaches);
            if (requestedOverflowLootCaches > publishedOverflowLootCaches)
                result.Flags |= CargoResultDeathCacheSignalRejected;

            int expectedInventoryChangedSignals = CountCargoInventoryChangedSignalTargets(in result);
            int publishedInventoryChangedSignals = PublishCargoInventoryChangedSignals(in result);
            if (expectedInventoryChangedSignals > publishedInventoryChangedSignals)
                result.Flags |= CargoResultInventoryChangedSignalRejected;
        }

        public static bool TryPublishCargoInventoryChanged(in CargoMergeResultDTO result)
        {
            return PublishCargoInventoryChangedSignals(in result) > 0;
        }

        public static int PublishCargoInventoryChangedSignals(in CargoMergeResultDTO result)
        {
            if (result.SourceContainerHashID == 0u && result.DestContainerHashID == 0u)
                return 0;

            InventoryChangedSignal source = new InventoryChangedSignal
            {
                InventoryHash = result.SourceContainerHashID,
                Revision = result.Frame,
                Frame = result.Frame,
                OccupiedCells = (ushort)math.clamp(result.SourceActiveAfter, 0, ushort.MaxValue),
                Flags = (byte)(result.Flags & 0xFFu),
                TotalMassKg = 0f,
                CarryCapacityKg = 0f,
                Load01 = result.TransferProgress01
            };
            InventoryChangedSignal dest = source;
            dest.InventoryHash = result.DestContainerHashID;
            dest.OccupiedCells = (ushort)math.clamp(result.DestActiveAfter, 0, ushort.MaxValue);

            int published = 0;
            if (source.InventoryHash != 0u)
            {
                if (SignalBus<InventoryChangedSignal>.TryPushTracked(in source, ref s_x001DirectSignalPushDropCount_SoaInventoryQueryEngine_CargoSync))
                    published++;
            }

            if (dest.InventoryHash != 0u && dest.InventoryHash != source.InventoryHash)
            {
                if (SignalBus<InventoryChangedSignal>.TryPushTracked(in dest, ref s_x001DirectSignalPushDropCount_SoaInventoryQueryEngine_CargoSync))
                    published++;
            }

            return published;
        }

        private static int CountCargoInventoryChangedSignalTargets(in CargoMergeResultDTO result)
        {
            int expected = 0;
            if (result.SourceContainerHashID != 0u)
                expected++;
            if (result.DestContainerHashID != 0u && result.DestContainerHashID != result.SourceContainerHashID)
                expected++;
            return expected;
        }

        private static void PatchLatestCargoTelemetry(
            NativeArray<CargoTelemetryEntry> telemetryRing,
            NativeArray<CargoAtomicCounterDTO> telemetryCursor,
            in CargoMergeResultDTO result)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length == 0)
                return;

            int cursor = 0;
            if (telemetryCursor.IsCreated && telemetryCursor.Length > 0)
                cursor = math.max(0, telemetryCursor[0].Value - 1);

            int index = cursor % telemetryRing.Length;
            CargoTelemetryEntry entry = telemetryRing[index];
            if (entry.Frame != result.Frame ||
                entry.SourceContainerHashID != result.SourceContainerHashID ||
                entry.DestContainerHashID != result.DestContainerHashID)
            {
                return;
            }

            entry.Flags = result.Flags;
            entry.StateHash = HashCargoMergeStateForTelemetry(in result);
            telemetryRing[index] = entry;
        }

        private static ulong HashCargoMergeStateForTelemetry(in CargoMergeResultDTO merge)
        {
            ulong hash = 1469598103934665603UL;
            hash = MixCargoTelemetryHash(hash, merge.SourceContainerHashID);
            hash = MixCargoTelemetryHash(hash, merge.DestContainerHashID);
            hash = MixCargoTelemetryHash(hash, merge.TransferredItemCount);
            hash = MixCargoTelemetryHash(hash, merge.TransferredQuantityTotal);
            hash = MixCargoTelemetryHash(hash, merge.OverflowLootCacheCount);
            hash = MixCargoTelemetryHash(hash, merge.Flags);
            hash = MixCargoTelemetryHash(hash, unchecked((uint)merge.NextSourceIndex));
            return hash;
        }

        private static ulong MixCargoTelemetryHash(ulong hash, uint value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }

        private static bool IsCargoFilterHeader(ReadOnlySpan<byte> line)
        {
            return line.Length >= 9 &&
                   ToLowerAscii(line[0]) == (byte)'c' &&
                   ToLowerAscii(line[1]) == (byte)'o' &&
                   ToLowerAscii(line[2]) == (byte)'n' &&
                   ToLowerAscii(line[3]) == (byte)'t' &&
                   ToLowerAscii(line[4]) == (byte)'a' &&
                   ToLowerAscii(line[5]) == (byte)'i' &&
                   ToLowerAscii(line[6]) == (byte)'n' &&
                   ToLowerAscii(line[7]) == (byte)'e' &&
                   ToLowerAscii(line[8]) == (byte)'r';
        }

        private static bool TryParseCargoFilterProfileLine(ReadOnlySpan<byte> line, out CargoFilterProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            if (!TryReadToken(line, ref cursor, out ReadOnlySpan<byte> containerToken) ||
                !TryReadToken(line, ref cursor, out ReadOnlySpan<byte> maskToken))
            {
                return false;
            }

            TryReadToken(line, ref cursor, out ReadOnlySpan<byte> flagsToken);
            TryReadToken(line, ref cursor, out ReadOnlySpan<byte> priorityToken);
            TryReadToken(line, ref cursor, out ReadOnlySpan<byte> hashAToken);
            TryReadToken(line, ref cursor, out ReadOnlySpan<byte> hashBToken);

            uint containerHash = TryParseUInt(containerToken, out uint parsedContainer)
                ? parsedContainer
                : HashLowerAscii(containerToken);
            uint filterMask = TryParseUInt(maskToken, out uint parsedMask)
                ? parsedMask
                : HashLowerAscii(maskToken);
            if (containerHash == 0u || filterMask == 0u)
                return false;

            profile.ContainerHash = containerHash;
            profile.FilterHashMask = filterMask;
            profile.RouteFlags = TryParseUInt(flagsToken, out uint flags) ? flags : 0u;
            profile.Priority = TryParseUInt(priorityToken, out uint priority) ? priority : 0u;
            profile.AcceptedHashA = TryParseUInt(hashAToken, out uint hashA) ? hashA : HashLowerAscii(hashAToken);
            profile.AcceptedHashB = TryParseUInt(hashBToken, out uint hashB) ? hashB : HashLowerAscii(hashBToken);
            return true;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct GenerateMockCargoTransferJob : IJob
        {
            [NoAlias] public NativeArray<uint> SourceHashes;
            [NoAlias] public NativeArray<uint> SourceQuantities;
            [NoAlias] public NativeArray<float> SourceDurabilities;
            [NoAlias] public NativeArray<int> SourceActiveItemCount;
            [NoAlias] public NativeArray<uint> DestinationHashes;
            [NoAlias] public NativeArray<uint> DestinationQuantities;
            [NoAlias] public NativeArray<float> DestinationDurabilities;
            [NoAlias] public NativeArray<int> DestinationActiveItemCount;
            [NoAlias] public NativeArray<CargoTransactionDTO> Transactions;
            public int RequestedItemCount;
            public uint Seed;
            public uint SourceContainerHashID;
            public uint DestContainerHashID;
            public uint FilterHashMask;

            public void Execute()
            {
                if (!SourceHashes.IsCreated ||
                    !SourceQuantities.IsCreated ||
                    !DestinationHashes.IsCreated ||
                    !DestinationQuantities.IsCreated ||
                    SourceHashes.Length != SourceQuantities.Length ||
                    DestinationHashes.Length != DestinationQuantities.Length)
                {
                    return;
                }

                int sourceCount = math.min(math.max(0, RequestedItemCount), SourceHashes.Length);
                int destCount = math.min(sourceCount >> 2, DestinationHashes.Length);
                for (int i = 0; i < sourceCount; i++)
                {
                    uint hash = 0x80000000u | ((HashCargo((uint)i ^ Seed) & 0x0000FFFFu) + 1u);
                    SourceHashes[i] = hash;
                    SourceQuantities[i] = 1u + ((HashCargo(hash ^ Seed) >> 24) & 0x3Fu);
                    if (SourceDurabilities.IsCreated && i < SourceDurabilities.Length)
                        SourceDurabilities[i] = 1f;
                }

                for (int i = sourceCount; i < SourceHashes.Length; i++)
                {
                    SourceHashes[i] = 0u;
                    SourceQuantities[i] = 0u;
                    if (SourceDurabilities.IsCreated && i < SourceDurabilities.Length)
                        SourceDurabilities[i] = 0f;
                }

                for (int i = 0; i < destCount; i++)
                {
                    DestinationHashes[i] = SourceHashes[i << 1];
                    DestinationQuantities[i] = 1u;
                    if (DestinationDurabilities.IsCreated && i < DestinationDurabilities.Length)
                        DestinationDurabilities[i] = 1f;
                }

                for (int i = destCount; i < DestinationHashes.Length; i++)
                {
                    DestinationHashes[i] = 0u;
                    DestinationQuantities[i] = 0u;
                    if (DestinationDurabilities.IsCreated && i < DestinationDurabilities.Length)
                        DestinationDurabilities[i] = 0f;
                }

                WriteCount(SourceActiveItemCount, sourceCount);
                WriteCount(DestinationActiveItemCount, destCount);
                if (Transactions.IsCreated && Transactions.Length > 0)
                {
                    CargoTransactionDTO transaction = default;
                    transaction.SourceContainerHashID = SourceContainerHashID != 0u ? SourceContainerHashID : 0x534F5552u;
                    transaction.DestContainerHashID = DestContainerHashID != 0u ? DestContainerHashID : 0x44455354u;
                    transaction.FilterHashMask = FilterHashMask;
                    transaction.TransactionFlags = 1u;
                    Transactions[0] = transaction;
                }
            }

            private static void WriteCount(NativeArray<int> count, int value)
            {
                if (count.IsCreated && count.Length > 0)
                    count[0] = math.max(0, value);
            }

            private static uint HashCargo(uint value)
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct ExecuteCargoMergeJob : IJob
        {
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> SourceHashes;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> SourceQuantities;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> SourceDurabilities;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> SourceActiveItemCount;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> DestinationHashes;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<uint> DestinationQuantities;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<float> DestinationDurabilities;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> DestinationActiveItemCount;
            [ReadOnly, NoAlias] public NativeArray<CargoTransactionDTO> Transactions;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<LootCacheDTO> OverflowLootCaches;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<CargoAtomicCounterDTO> OverflowLootCounter;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<CargoMergeResultDTO> Result;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<CargoTelemetryEntry> TelemetryRing;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<CargoAtomicCounterDTO> TelemetryCursor;
            public int TransactionIndex;
            public int SourceStartIndex;
            public int MaxItemsPerFrame;
            public float GlobalQualityWeight;
            public AbsoluteUniversePositionBlit DockAup;
            public float3 EjectionOffsetMeters;
            public uint FrameIndex;
            public float MeasuredExecutionMicroseconds;

            private const int QuantityLockBit = unchecked((int)0x80000000);
            private const int QuantityValueMask = 0x7FFFFFFF;

            public void Execute()
            {
                CargoMergeResultDTO merge = default;
                merge.NextSourceIndex = math.max(0, SourceStartIndex);
                merge.ExecutionMicroseconds = math.max(0f, math.isfinite(MeasuredExecutionMicroseconds) ? MeasuredExecutionMicroseconds : 0f);
                merge.Frame = FrameIndex;
                merge.Flags = ResolveSimdFlags();

                if (!ValidateInput(ref merge, out CargoTransactionDTO transaction))
                {
                    WriteResult(in merge);
                    RecordTelemetry(in merge);
                    return;
                }

                merge.SourceContainerHashID = transaction.SourceContainerHashID;
                merge.DestContainerHashID = transaction.DestContainerHashID;
                int sourceCapacity = math.min(SourceHashes.Length, SourceQuantities.Length);
                int destCapacity = math.min(DestinationHashes.Length, DestinationQuantities.Length);
                int sourceActiveBefore = AtomicReadActiveCount(SourceActiveItemCount, sourceCapacity);
                int destActiveBefore = AtomicReadActiveCount(DestinationActiveItemCount, destCapacity);
                merge.SourceActiveBefore = sourceActiveBefore;
                merge.DestActiveBefore = destActiveBefore;

                int start = math.clamp(SourceStartIndex, 0, sourceActiveBefore);
                int maxItems = ResolveMaxItemsPerFrame(MaxItemsPerFrame, GlobalQualityWeight);
                int end = math.min(sourceActiveBefore, start + maxItems);
                int processedSlots = 0;
                ResetOverflowCounter();

                for (int sourceIndex = start; sourceIndex < end; sourceIndex++)
                {
                    processedSlots++;
                    uint sourceHash = SourceHashes[sourceIndex];
                    uint sourceQuantity = ReadUIntAtomic(SourceQuantities, sourceIndex);
                    if (sourceHash == 0u || sourceQuantity == 0u)
                        continue;

                    if (!PassesFilter(sourceHash, transaction.FilterHashMask))
                    {
                        merge.Flags |= CargoResultFilterRejected;
                        continue;
                    }

                    if (!TryClaimSourceQuantity(sourceIndex, sourceQuantity))
                    {
                        AddResultCounter(ref merge.AtomicConflictCount, 1);
                        merge.Flags |= CargoResultAtomicConflict;
                        continue;
                    }

                    int destIndex = FindFirstMatchingSlot(DestinationHashes, destActiveBefore, sourceHash);
                    if (destIndex >= 0)
                    {
                        if (!TryAtomicAddQuantity(DestinationQuantities, destIndex, sourceQuantity, out uint accepted, out uint overflow))
                        {
                            RestoreSourceQuantity(sourceIndex, sourceQuantity);
                            AddResultCounter(ref merge.AtomicConflictCount, 1);
                            merge.Flags |= CargoResultAtomicConflict;
                            continue;
                        }

                        CopyDurability(sourceIndex, destIndex);
                        AddResultCounter(ref merge.TransferredItemCount, accepted > 0u ? 1u : 0u);
                        AddResultCounter(ref merge.TransferredQuantityTotal, accepted);
                        if (overflow != 0u)
                            WriteOverflowLoot(ref merge, sourceHash, overflow);
                        continue;
                    }

                    destIndex = AtomicReserveSlot(DestinationActiveItemCount, destCapacity);
                    if (destIndex >= 0)
                    {
                        destActiveBefore = math.max(destActiveBefore, destIndex + 1);
                        DestinationHashes[destIndex] = sourceHash;
                        DestinationQuantities[destIndex] = 0u;
                        CopyDurability(sourceIndex, destIndex);
                        if (!TryAtomicAddQuantity(DestinationQuantities, destIndex, sourceQuantity, out uint accepted, out uint overflow))
                        {
                            ClearDestinationSlot(destIndex);
                            RestoreSourceQuantity(sourceIndex, sourceQuantity);
                            AddResultCounter(ref merge.AtomicConflictCount, 1);
                            merge.Flags |= CargoResultAtomicConflict;
                            continue;
                        }

                        AddResultCounter(ref merge.TransferredItemCount, accepted > 0u ? 1u : 0u);
                        AddResultCounter(ref merge.TransferredQuantityTotal, accepted);
                        if (overflow != 0u)
                            WriteOverflowLoot(ref merge, sourceHash, overflow);
                    }
                    else
                    {
                        WriteOverflowLoot(ref merge, sourceHash, sourceQuantity);
                    }
                }

                DefragmentSourceWindow(start, processedSlots, ref merge);
                int sourceActiveAfter = AtomicReadActiveCount(SourceActiveItemCount, sourceCapacity);
                int destActiveAfter = AtomicReadActiveCount(DestinationActiveItemCount, destCapacity);
                merge.SourceActiveAfter = sourceActiveAfter;
                merge.DestActiveAfter = destActiveAfter;
                merge.NextSourceIndex = end >= sourceActiveBefore
                    ? sourceActiveAfter
                    : ((merge.Flags & CargoResultSwapPop) != 0u ? start : math.min(end, sourceActiveAfter));
                merge.TransferProgress01 = sourceActiveBefore > 0
                    ? math.saturate((float)math.min(end, sourceActiveBefore) * math.rcp(sourceActiveBefore))
                    : 1f;
                if (merge.NextSourceIndex < sourceActiveAfter)
                    merge.Flags |= CargoResultTimeSliced;
                else
                    merge.Flags |= CargoResultComplete;

                if (merge.OverflowLootCacheCount != 0u)
                    merge.Flags |= CargoResultOverflowLoot;
                if (merge.ExecutionMicroseconds > 500f)
                    merge.Flags |= CargoResultTelemetryDumpRequested;

                WriteResult(in merge);
                RecordTelemetry(in merge);
            }

            private bool ValidateInput(ref CargoMergeResultDTO merge, out CargoTransactionDTO transaction)
            {
                transaction = default;
                if (!SourceHashes.IsCreated ||
                    !SourceQuantities.IsCreated ||
                    !SourceActiveItemCount.IsCreated ||
                    !DestinationHashes.IsCreated ||
                    !DestinationQuantities.IsCreated ||
                    !DestinationActiveItemCount.IsCreated ||
                    !Transactions.IsCreated ||
                    SourceActiveItemCount.Length == 0 ||
                    DestinationActiveItemCount.Length == 0 ||
                    SourceHashes.Length != SourceQuantities.Length ||
                    DestinationHashes.Length != DestinationQuantities.Length ||
                    (uint)TransactionIndex >= (uint)Transactions.Length)
                {
                    merge.Flags |= CargoResultInvalidInput;
                    return false;
                }

                transaction = Transactions[TransactionIndex];
                if (transaction.SourceContainerHashID == 0u || transaction.DestContainerHashID == 0u)
                {
                    merge.Flags |= CargoResultInvalidInput;
                    return false;
                }

                if (!IsFiniteAup(in DockAup) || !math.all(math.isfinite(EjectionOffsetMeters)))
                    merge.Flags |= CargoResultAupFault;

                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int ResolveMaxItemsPerFrame(int explicitMax, float globalQualityWeight)
            {
                if (explicitMax > 0)
                    return math.max(1, explicitMax);

                float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 0f);
                return (int)math.round(math.lerp(100f, 1000f, q));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool PassesFilter(uint hash, uint filterMask)
            {
                return filterMask == 0u || (hash & filterMask) != 0u;
            }

            private int FindFirstMatchingSlot(NativeArray<uint> hashes, int activeCount, uint hash)
            {
                int end = math.clamp(activeCount, 0, hashes.Length);
                int cursor = 0;
                if (X86.Avx2.IsAvx2Supported)
                {
                    int vector8End = end & ~7;
                    for (int i = 0; i < vector8End; i += 8)
                    {
                        int mask = EqualMask8(hashes, i, hash);
                        if (mask != 0)
                            return i + math.tzcnt(mask);
                    }

                    cursor = vector8End;
                }

                int vectorEnd = cursor + ((end - cursor) & ~3);
                for (int i = cursor; i < vectorEnd; i += 4)
                {
                    int mask = EqualMask4(hashes, i, hash);
                    if (mask != 0)
                        return i + math.tzcnt(mask);
                }

                for (int i = vectorEnd; i < end; i++)
                {
                    if (hashes[i] == hash)
                        return i;
                }

                return -1;
            }

            private bool TryClaimSourceQuantity(int sourceIndex, uint sourceQuantity)
            {
                uint* quantityPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(SourceQuantities);
                ref int quantityRef = ref UnsafeUtility.AsRef<int>((int*)(quantityPtr + sourceIndex));
                int expected = sourceQuantity > int.MaxValue ? int.MaxValue : unchecked((int)sourceQuantity);
                return Interlocked.CompareExchange(ref quantityRef, 0, expected) == expected;
            }

            private void RestoreSourceQuantity(int sourceIndex, uint sourceQuantity)
            {
                uint* quantityPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(SourceQuantities);
                ref int quantityRef = ref UnsafeUtility.AsRef<int>((int*)(quantityPtr + sourceIndex));
                int value = sourceQuantity > int.MaxValue ? int.MaxValue : unchecked((int)sourceQuantity);
                Interlocked.CompareExchange(ref quantityRef, value, 0);
            }

            private static bool TryAtomicAddQuantity(
                NativeArray<uint> quantities,
                int index,
                uint amount,
                out uint accepted,
                out uint overflow)
            {
                accepted = 0u;
                overflow = amount;
                if (!quantities.IsCreated || (uint)index >= (uint)quantities.Length || amount == 0u)
                    return false;

                uint* quantityPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(quantities);
                ref int quantityRef = ref UnsafeUtility.AsRef<int>((int*)(quantityPtr + index));
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    int observed = Interlocked.CompareExchange(ref quantityRef, 0, 0);
                    if ((observed & QuantityLockBit) != 0)
                        continue;

                    int current = observed & QuantityValueMask;
                    uint capacity = unchecked((uint)(QuantityValueMask - current));
                    uint delta = math.min(amount, capacity);
                    if (delta == 0u)
                    {
                        accepted = 0u;
                        overflow = amount;
                        return true;
                    }

                    int locked = observed | QuantityLockBit;
                    if (Interlocked.CompareExchange(ref quantityRef, locked, observed) == observed)
                    {
                        int afterLockedAdd = Interlocked.Add(ref quantityRef, unchecked((int)delta));
                        int final = afterLockedAdd & QuantityValueMask;
                        Interlocked.Exchange(ref quantityRef, final);
                        accepted = delta;
                        overflow = amount - delta;
                        return true;
                    }
                }

                return false;
            }

            private static uint ReadUIntAtomic(NativeArray<uint> quantities, int index)
            {
                uint* quantityPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(quantities);
                ref int quantityRef = ref UnsafeUtility.AsRef<int>((int*)(quantityPtr + index));
                int value = Interlocked.CompareExchange(ref quantityRef, 0, 0);
                return unchecked((uint)math.max(0, value));
            }

            private void CopyDurability(int sourceIndex, int destinationIndex)
            {
                if (SourceDurabilities.IsCreated &&
                    DestinationDurabilities.IsCreated &&
                    (uint)sourceIndex < (uint)SourceDurabilities.Length &&
                    (uint)destinationIndex < (uint)DestinationDurabilities.Length)
                {
                    DestinationDurabilities[destinationIndex] = math.saturate(SourceDurabilities[sourceIndex]);
                    SourceDurabilities[sourceIndex] = 0f;
                }
            }

            private void ClearDestinationSlot(int index)
            {
                if ((uint)index >= (uint)DestinationHashes.Length)
                    return;

                DestinationHashes[index] = 0u;
                DestinationQuantities[index] = 0u;
                if (DestinationDurabilities.IsCreated && index < DestinationDurabilities.Length)
                    DestinationDurabilities[index] = 0f;
                AtomicDecrementActiveCount(DestinationActiveItemCount);
            }

            private void DefragmentSourceWindow(int start, int processedSlots, ref CargoMergeResultDTO merge)
            {
                if (processedSlots <= 0)
                    return;

                int active = AtomicReadActiveCount(SourceActiveItemCount, SourceHashes.Length);
                int lower = math.clamp(start, 0, active);
                int upper = math.min(active, lower + processedSlots);
                for (int index = upper - 1; index >= lower; index--)
                {
                    if (SourceHashes[index] != 0u && ReadUIntAtomic(SourceQuantities, index) != 0u)
                        continue;

                    int last = AtomicReadActiveCount(SourceActiveItemCount, SourceHashes.Length) - 1;
                    while (last > index && (SourceHashes[last] == 0u || ReadUIntAtomic(SourceQuantities, last) == 0u))
                    {
                        ClearSourceSlot(last);
                        AtomicDecrementActiveCount(SourceActiveItemCount);
                        last--;
                    }

                    if (last < index)
                        continue;

                    if (last == index)
                    {
                        ClearSourceSlot(index);
                        AtomicDecrementActiveCount(SourceActiveItemCount);
                        merge.Flags |= CargoResultSwapPop;
                        continue;
                    }

                    SourceHashes[index] = SourceHashes[last];
                    SourceQuantities[index] = SourceQuantities[last];
                    if (SourceDurabilities.IsCreated && index < SourceDurabilities.Length && last < SourceDurabilities.Length)
                        SourceDurabilities[index] = SourceDurabilities[last];
                    ClearSourceSlot(last);
                    AtomicDecrementActiveCount(SourceActiveItemCount);
                    merge.Flags |= CargoResultSwapPop;
                }
            }

            private void ClearSourceSlot(int index)
            {
                if ((uint)index >= (uint)SourceHashes.Length)
                    return;

                SourceHashes[index] = 0u;
                SourceQuantities[index] = 0u;
                if (SourceDurabilities.IsCreated && index < SourceDurabilities.Length)
                    SourceDurabilities[index] = 0f;
            }

            private void ResetOverflowCounter()
            {
                if (OverflowLootCounter.IsCreated && OverflowLootCounter.Length > 0)
                {
                    ref int valueRef = ref CounterValueRef(OverflowLootCounter, 0);
                    Interlocked.Exchange(ref valueRef, 0);
                }
            }

            private void WriteOverflowLoot(ref CargoMergeResultDTO merge, uint hash, uint quantity)
            {
                if (hash == 0u || quantity == 0u)
                    return;

                int writeIndex = ReserveOverflowIndex();
                if (writeIndex < 0 || !OverflowLootCaches.IsCreated || writeIndex >= OverflowLootCaches.Length)
                {
                    AddResultCounter(ref merge.AtomicConflictCount, 1);
                    merge.Flags |= CargoResultAtomicConflict;
                    return;
                }

                LootCacheDTO lootCache = default;
                lootCache.PositionAup = BuildOverflowAup(in DockAup, EjectionOffsetMeters);
                lootCache.ItemHashID = hash;
                lootCache.Quantity = quantity;
                lootCache.SourceContainerHashID = merge.SourceContainerHashID;
                lootCache.DestContainerHashID = merge.DestContainerHashID;
                lootCache.Sequence = (FrameIndex << 16) ^ (uint)writeIndex;
                lootCache.Frame = FrameIndex;
                lootCache.Flags = 1u;
                lootCache.QualityMilli = 1000u;
                OverflowLootCaches[writeIndex] = lootCache;
                AddResultCounter(ref merge.OverflowLootCacheCount, 1);
            }

            private int ReserveOverflowIndex()
            {
                if (!OverflowLootCounter.IsCreated || OverflowLootCounter.Length == 0)
                    return -1;

                ref int valueRef = ref CounterValueRef(OverflowLootCounter, 0);
                int next = Interlocked.Add(ref valueRef, 1);
                return next - 1;
            }

            private void WriteResult(in CargoMergeResultDTO merge)
            {
                if (Result.IsCreated && Result.Length > 0)
                    Result[0] = merge;
            }

            private void RecordTelemetry(in CargoMergeResultDTO merge)
            {
                if (!TelemetryRing.IsCreated || TelemetryRing.Length == 0)
                    return;

                int cursor = ReserveTelemetryCursor();
                int index = cursor % TelemetryRing.Length;
                CargoTelemetryEntry telemetry = default;
                telemetry.Frame = FrameIndex;
                telemetry.SourceContainerHashID = merge.SourceContainerHashID;
                telemetry.DestContainerHashID = merge.DestContainerHashID;
                telemetry.ItemsTransferred = merge.TransferredItemCount;
                telemetry.QuantityTransferred = merge.TransferredQuantityTotal;
                telemetry.OverflowLootCaches = merge.OverflowLootCacheCount;
                telemetry.TimeSlicedFrames = (merge.Flags & CargoResultTimeSliced) != 0u ? 1u : 0u;
                telemetry.AtomicConflicts = merge.AtomicConflictCount;
                telemetry.BurstExecutionMicroseconds = merge.ExecutionMicroseconds;
                telemetry.TransferProgress01 = merge.TransferProgress01;
                telemetry.Flags = merge.Flags;
                telemetry.NextSourceIndex = merge.NextSourceIndex;
                telemetry.StateHash = HashMergeState(in merge);
                TelemetryRing[index] = telemetry;
            }

            private int ReserveTelemetryCursor()
            {
                if (!TelemetryCursor.IsCreated || TelemetryCursor.Length == 0)
                    return 0;

                ref int valueRef = ref CounterValueRef(TelemetryCursor, 0);
                int next = Interlocked.Add(ref valueRef, 1);
                return math.max(0, next - 1);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ref int CounterValueRef(NativeArray<CargoAtomicCounterDTO> counters, int index)
            {
                CargoAtomicCounterDTO* counterPtr = (CargoAtomicCounterDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(counters);
                return ref UnsafeUtility.AsRef<int>(&counterPtr[index].Value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void AddResultCounter(ref uint counter, uint delta)
            {
                uint next = counter + delta;
                counter = next < counter ? uint.MaxValue : next;
            }

            private static AbsoluteUniversePositionBlit BuildOverflowAup(in AbsoluteUniversePositionBlit dockAup, float3 offsetMeters)
            {
                double cell = AbsoluteUniversePosition.CellSizeMeters;
                double3 absolute = default;
                absolute.x = ((double)dockAup.GridX * cell) + dockAup.Local.x;
                absolute.y = ((double)dockAup.GridY * cell) + dockAup.Local.y;
                absolute.z = ((double)dockAup.GridZ * cell) + dockAup.Local.z;
                absolute.x += offsetMeters.x;
                absolute.y += offsetMeters.y;
                absolute.z += offsetMeters.z;
                return PackAup(absolute, cell);
            }

            private static AbsoluteUniversePositionBlit PackAup(double3 absolute, double cell)
            {
                if (!math.all(math.isfinite(absolute)) || cell <= 0d)
                    return default;

                long gridX = (long)math.floor(absolute.x / cell);
                long gridY = (long)math.floor(absolute.y / cell);
                long gridZ = (long)math.floor(absolute.z / cell);
                float3 local = default;
                local.x = (float)math.clamp(absolute.x - (gridX * cell), -CargoFloatCastClampMeters, CargoFloatCastClampMeters);
                local.y = (float)math.clamp(absolute.y - (gridY * cell), -CargoFloatCastClampMeters, CargoFloatCastClampMeters);
                local.z = (float)math.clamp(absolute.z - (gridZ * cell), -CargoFloatCastClampMeters, CargoFloatCastClampMeters);
                AbsoluteUniversePositionBlit packed = default;
                packed.GridX = gridX;
                packed.GridY = gridY;
                packed.GridZ = gridZ;
                packed.Local = local;
                packed.Reserved0 = 0u;
                packed.Reserved1 = 0UL;
                return packed;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsFiniteAup(in AbsoluteUniversePositionBlit value)
            {
                return math.all(math.isfinite(value.Local));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint ResolveSimdFlags()
            {
                if (X86.Avx2.IsAvx2Supported)
                    return CargoResultAvx2;
                if (X86.Sse2.IsSse2Supported)
                    return CargoResultSse2;
                if (Arm.Neon.IsNeonSupported)
                    return CargoResultNeon;
                return 0u;
            }

            private static ulong HashMergeState(in CargoMergeResultDTO merge)
            {
                return HashCargoMergeStateForTelemetry(in merge);
            }
        }
    }
}
