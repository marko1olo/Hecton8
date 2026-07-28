using System;
using System.IO;
#if UNITY_EDITOR || UNITY_STANDALONE || HECTON8_MMF_AVAILABLE
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.SaveSystem
{
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct MerkleNodeDTO
    {
        [FieldOffset(0)] public ulong HashLo;
        [FieldOffset(8)] public ulong HashHi;
        [FieldOffset(16)] public uint SectorKey;
        [FieldOffset(20)] public uint ChildMask;
        [FieldOffset(24)] public ulong _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct SectorEntryDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public ulong ByteOffset;
        [FieldOffset(16)] public int CompressedSize;
        [FieldOffset(20)] public int DecompressedSize;
        [FieldOffset(24)] public uint Checksum;
        [FieldOffset(28)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct StateDeltaRecordDTO
    {
        [FieldOffset(0)] public ulong PreviousHashLo;
        [FieldOffset(8)] public ulong PreviousHashHi;
        [FieldOffset(16)] public ulong NewHashLo;
        [FieldOffset(24)] public ulong NewHashHi;
        [FieldOffset(32)] public int SourceOffsetBytes;
        [FieldOffset(36)] public int DataLength;
        [FieldOffset(40)] public int DeltaPayloadOffset;
        [FieldOffset(44)] public int CompressedOffset;
        [FieldOffset(48)] public uint SectorKey;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint Crc32;
        [FieldOffset(60)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct StateLeafDescriptor
    {
        [FieldOffset(0)] public uint SectorKey;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public int SourceOffsetBytes;
        [FieldOffset(12)] public int ByteLength;
        [FieldOffset(16)] public int RecordStrideBytes;
        [FieldOffset(20)] public int TombstoneOffsetBytes;
        [FieldOffset(24)] public uint TombstoneAliveMask;
        [FieldOffset(28)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct Lz4SubBlockHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public int RawBytes;
        [FieldOffset(8)] public int StoredBytes;
        [FieldOffset(12)] public int SourceOffsetBytes;
        [FieldOffset(16)] public uint Crc32;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ushort Version;
        [FieldOffset(26)] public ushort HeaderBytes;
        [FieldOffset(28)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct SaveMerkleWalAppendHeader
    {
        [FieldOffset(0)] public long LogicalOffset;
        [FieldOffset(8)] public long TimestampTicks;
        [FieldOffset(16)] public ulong RootHashLo;
        [FieldOffset(24)] public ulong RootHashHi;
        [FieldOffset(32)] public int RawBytes;
        [FieldOffset(36)] public int StoredBytes;
        [FieldOffset(40)] public uint Magic;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint BlockCount;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public uint RecordCrc32;
        [FieldOffset(60)] public ushort Version;
        [FieldOffset(62)] public ushort HeaderBytes;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct SaveMerkleTelemetryEntry
    {
        [FieldOffset(0)] public ulong RootHashLo;
        [FieldOffset(8)] public ulong RootHashHi;
        [FieldOffset(16)] public int TotalBytesHashed;
        [FieldOffset(20)] public int DeltaBytesGenerated;
        [FieldOffset(24)] public float TreeComputeTimeMs;
        [FieldOffset(28)] public uint Frame;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint ChangedLeaves;
        [FieldOffset(40)] public uint WalBytesWritten;
        [FieldOffset(44)] public uint CrcFailures;
        [FieldOffset(48)] public uint IoFailures;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct SaveMerkleEmergencyHeader64
    {
        [FieldOffset(0)] public ulong TimestampTicks;
        [FieldOffset(8)] public ulong RootHashLo;
        [FieldOffset(16)] public ulong RootHashHi;
        [FieldOffset(24)] public ulong _pad0;
        [FieldOffset(32)] public ulong _pad1;
        [FieldOffset(40)] public uint Magic;
        [FieldOffset(44)] public uint SectorEntryBytes;
        [FieldOffset(48)] public uint MerkleNodeBytes;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint Checksum;
        [FieldOffset(60)] public ushort Version;
        [FieldOffset(62)] public ushort HeaderBytes;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct SaveMerkleRuntimeConfig
    {
        [FieldOffset(0)] public int SubBlockBytes;
        [FieldOffset(4)] public int WalBytesPerSecond;
        [FieldOffset(8)] public int MathLod;
        [FieldOffset(12)] public int CosmeticDropThresholdBytes;
        [FieldOffset(16)] public uint Version;
        [FieldOffset(20)] public uint SchemaHash;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct SaveMerkleEditorSnapshot
    {
        [FieldOffset(0)] public ulong RootHashLo;
        [FieldOffset(8)] public ulong RootHashHi;
        [FieldOffset(16)] public ulong ChangedBranchBits0;
        [FieldOffset(24)] public ulong ChangedBranchBits1;
        [FieldOffset(32)] public ulong ChangedBranchBits2;
        [FieldOffset(40)] public ulong ChangedBranchBits3;
        [FieldOffset(48)] public uint ChangedLeafCount;
        [FieldOffset(52)] public uint LeafCount;
        [FieldOffset(56)] public uint LastChangedSectorKey;
        [FieldOffset(60)] public uint CorruptBlockCount;
        [FieldOffset(64)] public uint StoredBytes;
        [FieldOffset(68)] public uint RawBytes;
        [FieldOffset(72)] public uint SnapshotFlags;
        [FieldOffset(76)] public uint _pad0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal unsafe partial struct MockInventoryData
    {
        [FieldOffset(0)] public uint ItemId;
        [FieldOffset(4)] public uint Count;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint StableSeed;
        [FieldOffset(16)] public fixed byte Payload[112];
    }

    internal ref struct SaveMerkleVaultBufferSet
    {
        public NativeArray<MerkleNodeDTO> CurrentTree;
        public NativeArray<MerkleNodeDTO> PreviousTree;
        public NativeArray<StateLeafDescriptor> LeafDescriptors;
        public NativeArray<StateDeltaRecordDTO> DeltaRecords;
        public NativeArray<byte> DeltaBytes;
        public NativeArray<byte> PrunedDeltaBytes;
        public NativeArray<byte> CompressedBytes;
        public NativeArray<Lz4SubBlockHeader> Lz4BlockHeaders;
        public NativeArray<SaveMerkleTelemetryEntry> TelemetryRing;
        public NativeArray<int> Counters;
        public NativeArray<int> Lz4HashTable;
    }

    internal static unsafe class SaveStateMerkleTree
    {
        internal const int LeafCount = 4096;
        internal const int Fanout = 16;
        internal const int Level1Count = 16;
        internal const int Level2Count = 256;
        internal const int BranchNodeCount = 273;
        internal const int TotalNodeCount = 4369;
        internal const int RootIndex = 0;
        internal const int Level1Offset = 1;
        internal const int Level2Offset = 17;
        internal const int LeafLevelOffset = 273;
        internal const int DefaultSubBlockBytes = 16 * 1024;
        internal const int MaxSubBlockBytes = 32 * 1024;
        internal const int TelemetryRingFrames = 300;
        internal const int HashTableSlots = 4096;
        internal const uint LeafFlagTombstone = 1u;
        internal const uint LeafFlagStableRestState = 1u << 1;
        internal const uint LeafFlagNeedsWake = 1u << 2;
        internal const uint LeafFlagModPayload = 1u << 3;
        internal const uint LeafFlagCosmetic = 1u << 4;
        internal const uint DeltaFlagOverflow = 1u << 31;
        internal const uint ModPayloadSectorPrefix = 0x4D500000u;
        internal const uint Lz4BlockMagic = 0x4C5A3448u; // H4ZL
        internal const uint MerkleWalMagic = 0x4D574838u; // H8WM
        internal const uint EmergencyHeaderMagic = 0x48454354u; // HECT
        internal const ushort MerkleWalVersion = 1;
        internal const ushort Lz4BlockVersion = 1;
        internal const ushort EmergencyHeaderVersion = 0x0009;
        internal const uint Lz4BlockFlagCompressed = 1u;
        internal const uint Lz4BlockFlagRaw = 1u << 1;
        internal const uint Lz4BlockFlagModPayload = 1u << 2;
        internal const uint Lz4BlockFlagRle = 1u << 8;
        internal const uint TelemetryFlagHashOverBudget = 1u << 0;
        internal const uint TelemetryFlagIoException = 1u << 1;
        internal const uint TelemetryFlagCrcFailure = 1u << 2;
        internal const string DefaultMerkleWalFileName = "slot_0.wal";
        internal const string DefaultTelemetryDumpFileName = "Dump_SAVE_SURGEON.bin";

        private const ulong LeafSeed = 0x48485341564C4546UL; // FLEVS HH
        private const ulong NodeSeed = 0x48485341564E4F44UL; // DONVS HH
        private const ulong CommittedTreeSentinel = 0x534156454D524B4CUL; // LKRMEVAS
        private const uint Lz4BlockFlagRleLegacy = 1u << 3;
        private const int CounterRecords = 0;
        private const int CounterBytes = 1;
        private const int CounterChangedLeaves = 2;
        private const int CounterFlags = 3;
        private const int CounterDroppedCosmeticBytes = 4;
        private const int CounterDroppedCosmeticRecords = 5;
        private const int CounterStoredBytes = 8;
        private const int CounterBlockCount = 9;
        private const int CounterRawBytes = 10;
        private const int CounterFailure = 11;
        private const int CounterCapacity = 16;

        private static SaveMerkleEditorSnapshot s_LastEditorSnapshot;
        private static int s_LastEditorSnapshotVersion;

        internal static int RequiredNodeCount => TotalNodeCount;

        internal static SaveMerkleRuntimeConfig BuildDefaultConfig()
        {
            return new SaveMerkleRuntimeConfig
            {
                SubBlockBytes = DefaultSubBlockBytes,
                WalBytesPerSecond = 16 * 1024 * 1024,
                MathLod = 1,
                CosmeticDropThresholdBytes = 512 * 1024,
                Version = 1u,
                SchemaHash = 0x534D524Bu,
                Flags = 0u,
                _pad0 = 0u
            };
        }

        internal static SaveMerkleRuntimeConfig ResolveRuntimeConfigForQuality(
            in SaveMerkleRuntimeConfig baseConfig,
            float globalQualityWeight,
            float systemStress01)
        {
            SaveMerkleRuntimeConfig config = baseConfig;
            float quality = SmoothUnit(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float stress = SmoothUnit(math.isfinite(systemStress01) ? systemStress01 : 0f);
            float retention = math.saturate(quality * (1f - (stress * 0.5f)));
            float survivalPull = SmoothUnit((0.3f - quality) * 3.3333333f);
            float cosmeticFloor = math.lerp(0.03125f, 0.00390625f, survivalPull);
            float cosmeticScale = math.max(cosmeticFloor, retention * retention * math.lerp(0.5f, 1f, quality));
            float subBlockScale = math.lerp(math.lerp(0.25f, 0.125f, survivalPull), 1f, quality);
            float walScale = math.lerp(0.125f, 1f, retention);
            float lodContinuous = math.lerp(0f, 3.999f, quality);

            int baseCosmeticThreshold = math.max(4096, baseConfig.CosmeticDropThresholdBytes);
            int baseSubBlockBytes = math.clamp(
                baseConfig.SubBlockBytes <= 0 ? DefaultSubBlockBytes : baseConfig.SubBlockBytes,
                1024,
                MaxSubBlockBytes);
            int baseWalBytesPerSecond = math.max(1024 * 1024, baseConfig.WalBytesPerSecond);

            config.CosmeticDropThresholdBytes = Align16(math.max(4096, (int)math.round(baseCosmeticThreshold * cosmeticScale)));
            config.SubBlockBytes = Align16(math.clamp((int)math.round(baseSubBlockBytes * subBlockScale), 1024, MaxSubBlockBytes));
            config.WalBytesPerSecond = Align16(math.max(1024 * 1024, (int)math.round(baseWalBytesPerSecond * walScale)));
            config.MathLod = (int)math.clamp(math.floor(lodContinuous), 0f, 3f);
            return config;
        }

        private static float SmoothUnit(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        internal static int ResolveRequiredNodeCount(int leafCount)
        {
            if (leafCount <= 0)
                return 0;

            int nodes = leafCount;
            int level = leafCount;
            while (level > 1)
            {
                level = (level + Fanout - 1) / Fanout;
                nodes += level;
            }

            return nodes;
        }

        internal static int ResolveRequiredSubBlockCount(int sourceBytes, int subBlockBytes)
        {
            int blockSize = math.clamp(subBlockBytes <= 0 ? DefaultSubBlockBytes : subBlockBytes, 1024, MaxSubBlockBytes);
            int bytes = math.max(0, sourceBytes);
            return math.max(1, (bytes + blockSize - 1) / blockSize);
        }

        internal static int ResolveRequiredCompressedCapacity(int sourceBytes, int subBlockBytes)
        {
            int bytes = math.max(0, sourceBytes);
            int blockCount = ResolveRequiredSubBlockCount(bytes, subBlockBytes);
            long headerBytes = (long)blockCount * UnsafeUtility.SizeOf<Lz4SubBlockHeader>();
            long alignmentBytes = (long)blockCount * 16L;
            long lz4WorstCaseBytes = bytes + (bytes / 255) + 16L;
            long required = headerBytes + alignmentBytes + lz4WorstCaseBytes + 256L;
            return required > int.MaxValue ? int.MaxValue : math.max(1024, (int)required);
        }

        internal static bool TryResolveVaultBuffers(
            IDataVault vault,
            int deltaCapacityBytes,
            int compressedCapacityBytes,
            int blockHeaderCapacity,
            out SaveMerkleVaultBufferSet buffers)
        {
            buffers = default;
            if (vault == null)
                return false;

            int safeDeltaBytes = math.max(1024, deltaCapacityBytes);
            int safeCompressedBytes = math.max(
                math.max(1024, compressedCapacityBytes),
                ResolveRequiredCompressedCapacity(safeDeltaBytes, DefaultSubBlockBytes));
            int safeBlockHeaders = math.max(
                math.max(1, blockHeaderCapacity),
                ResolveRequiredSubBlockCount(safeDeltaBytes, DefaultSubBlockBytes));

            bool hasCurrentTree = TryEnsureSaveMerkleVaultBuffer<MerkleNodeDTO>(
                vault,
                BufferID.SaveMerkleNodeFront,
                TotalNodeCount,
                NativeArrayOptions.UninitializedMemory,
                out buffers.CurrentTree);
            bool hasPreviousTree = TryEnsureSaveMerkleVaultBuffer<MerkleNodeDTO>(
                vault,
                BufferID.SaveMerkleNodeBack,
                TotalNodeCount,
                NativeArrayOptions.ClearMemory,
                out buffers.PreviousTree);
            bool hasLeafDescriptors = TryEnsureSaveMerkleVaultBuffer<StateLeafDescriptor>(
                vault,
                BufferID.SaveMerkleLeafDescriptors,
                LeafCount,
                NativeArrayOptions.ClearMemory,
                out buffers.LeafDescriptors);
            bool hasDeltaRecords = TryEnsureSaveMerkleVaultBuffer<StateDeltaRecordDTO>(
                vault,
                BufferID.SaveMerkleDeltaRecords,
                LeafCount,
                NativeArrayOptions.UninitializedMemory,
                out buffers.DeltaRecords);
            bool hasDeltaBytes = TryEnsureSaveMerkleVaultBuffer<byte>(
                vault,
                BufferID.SaveMerkleDeltaBytes,
                safeDeltaBytes,
                NativeArrayOptions.UninitializedMemory,
                out buffers.DeltaBytes);
            bool hasPrunedDeltaBytes = TryEnsureSaveMerkleVaultBuffer<byte>(
                vault,
                BufferID.SaveMerklePrunedDeltaBytes,
                safeDeltaBytes,
                NativeArrayOptions.UninitializedMemory,
                out buffers.PrunedDeltaBytes);
            bool hasCompressedBytes = TryEnsureSaveMerkleVaultBuffer<byte>(
                vault,
                BufferID.SaveMerkleCompressedBytes,
                safeCompressedBytes,
                NativeArrayOptions.UninitializedMemory,
                out buffers.CompressedBytes);
            bool hasLz4BlockHeaders = TryEnsureSaveMerkleVaultBuffer<Lz4SubBlockHeader>(
                vault,
                BufferID.SaveMerkleLz4BlockHeaders,
                safeBlockHeaders,
                NativeArrayOptions.UninitializedMemory,
                out buffers.Lz4BlockHeaders);
            bool hasTelemetryRing = TryEnsureSaveMerkleVaultBuffer<SaveMerkleTelemetryEntry>(
                vault,
                BufferID.SaveMerkleTelemetryRing,
                TelemetryRingFrames,
                NativeArrayOptions.ClearMemory,
                out buffers.TelemetryRing);
            bool hasCounters = TryEnsureSaveMerkleVaultBuffer<int>(
                vault,
                BufferID.SaveMerkleCounters,
                CounterCapacity,
                NativeArrayOptions.ClearMemory,
                out buffers.Counters);
            bool hasLz4HashTable = TryEnsureSaveMerkleVaultBuffer<int>(
                vault,
                BufferID.SaveMerkleLz4HashTable,
                HashTableSlots,
                NativeArrayOptions.UninitializedMemory,
                out buffers.Lz4HashTable);

            return hasCurrentTree &&
                   hasPreviousTree &&
                   hasLeafDescriptors &&
                   hasDeltaRecords &&
                   hasDeltaBytes &&
                   hasPrunedDeltaBytes &&
                   hasCompressedBytes &&
                   hasLz4BlockHeaders &&
                   hasTelemetryRing &&
                   hasCounters &&
                   hasLz4HashTable &&
                   buffers.CurrentTree.IsCreated &&
                   buffers.PreviousTree.IsCreated &&
                   buffers.LeafDescriptors.IsCreated &&
                   buffers.DeltaRecords.IsCreated &&
                   buffers.DeltaBytes.IsCreated &&
                   buffers.PrunedDeltaBytes.IsCreated &&
                   buffers.CompressedBytes.IsCreated &&
                   buffers.Lz4BlockHeaders.IsCreated &&
                   buffers.TelemetryRing.IsCreated &&
                   buffers.Counters.IsCreated &&
                   buffers.Lz4HashTable.IsCreated;
        }

        private static bool TryEnsureSaveMerkleVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.SavePersistence,
                options);

            return handle.BufferID == (uint)bufferId &&
                handle.SystemID == (uint)SystemID.SavePersistence &&
                handle.Generation != 0u &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength;
        }

        internal static int ResolveWalBudgetBytesPerFrame(in SaveMerkleRuntimeConfig config, float deltaTimeSeconds, bool slowMicroSdIo)
        {
            return ResolveWalBudgetBytesPerFrame(config, deltaTimeSeconds, slowMicroSdIo ? 1f : 0f);
        }

        internal static int ResolveWalBudgetBytesPerFrame(in SaveMerkleRuntimeConfig config, float deltaTimeSeconds, float microSdPressure01)
        {
            int bytesPerSecond = math.max(1024, config.WalBytesPerSecond);
            float pressure = SmoothUnit(math.isfinite(microSdPressure01) ? microSdPressure01 : 0f);
            int cappedBytesPerSecond = math.min(bytesPerSecond, 16 * 1024 * 1024);
            bytesPerSecond = Align16((int)math.round(math.lerp(bytesPerSecond, cappedBytesPerSecond, pressure)));

            return math.max(1024, (int)math.floor(bytesPerSecond * math.max(0.001f, deltaTimeSeconds)));
        }

        internal static uint BuildModPayloadSectorKey(ushort localSector)
        {
            return ModPayloadSectorPrefix | localSector;
        }

        internal static bool IsModPayloadSector(uint sectorKey)
        {
            return (sectorKey & 0xFFFF0000u) == ModPayloadSectorPrefix;
        }

        internal static SaveAupLocalOffset32 QuantizeAupForSave(
            double3 absoluteUniverseMeters,
            uint sectorKey,
            int sectorSizeMeters,
            uint flags)
        {
            return SaveDeltaCompression.QuantizeAupLocalOffset32(
                absoluteUniverseMeters,
                sectorKey,
                sectorSizeMeters,
                0u,
                flags);
        }

        internal static SaveAupLocalOffset32 QuantizeAupForSave(
            double3 absoluteUniverseMeters,
            double3 sectorOriginMeters,
            uint sectorKey,
            int sectorSizeMeters,
            uint flags)
        {
            return SaveDeltaCompression.QuantizeAupLocalOffset32(
                absoluteUniverseMeters,
                sectorOriginMeters,
                sectorKey,
                sectorSizeMeters,
                0u,
                flags);
        }

        internal static void GenerateEmergencyMockHeader(void* destination, int destinationCapacity)
        {
            if (destination == null || destinationCapacity < UnsafeUtility.SizeOf<SaveMerkleEmergencyHeader64>())
                return;

            SaveMerkleEmergencyHeader64 header = new SaveMerkleEmergencyHeader64
            {
                Magic = EmergencyHeaderMagic,
                Version = EmergencyHeaderVersion,
                HeaderBytes = (ushort)UnsafeUtility.SizeOf<SaveMerkleEmergencyHeader64>(),
                TimestampTicks = 0UL,
                RootHashLo = 0UL,
                RootHashHi = 0UL,
                SectorEntryBytes = (uint)UnsafeUtility.SizeOf<SectorEntryDTO>(),
                MerkleNodeBytes = (uint)UnsafeUtility.SizeOf<MerkleNodeDTO>(),
                Flags = 1u,
                Checksum = 0u,
                _pad0 = 0UL,
                _pad1 = 0UL
            };

            int headerByteCount = UnsafeUtility.SizeOf<SaveMerkleEmergencyHeader64>();
            Span<byte> headerBytes = stackalloc byte[64];
            if (headerByteCount > headerBytes.Length)
                return;

            fixed (byte* headerPtr = headerBytes)
            {
                WriteEmergencyHeaderLittleEndian(headerPtr, in header);
                header.Checksum = ComputeCrc32(headerPtr, headerByteCount);
                WriteEmergencyHeaderLittleEndian(headerPtr, in header);
                UnsafeUtility.MemCpy(destination, headerPtr, headerByteCount);
            }
        }

        internal static JobHandle ScheduleMerkleBuild(
            NativeArray<byte> sourceBytes,
            NativeArray<StateLeafDescriptor> descriptors,
            NativeArray<MerkleNodeDTO> treeNodes,
            JobHandle dependency)
        {
            JobHandle leaves = new MerkleLeafHashJob
            {
                SourceBytes = sourceBytes,
                Descriptors = descriptors,
                TreeNodes = treeNodes
            }.Schedule(LeafCount, 64, dependency);

            return new MerkleBranchReductionJob
            {
                TreeNodes = treeNodes
            }.Schedule(leaves);
        }

        internal static JobHandle ScheduleDeltaExtraction(
            NativeArray<byte> sourceBytes,
            NativeArray<StateLeafDescriptor> descriptors,
            NativeArray<MerkleNodeDTO> currentTree,
            NativeArray<MerkleNodeDTO> previousTree,
            NativeArray<StateDeltaRecordDTO> deltaRecords,
            NativeArray<byte> deltaBytes,
            NativeArray<int> counters,
            JobHandle dependency)
        {
            return new MerkleChangedLeafExtractionJob
            {
                SourceBytes = sourceBytes,
                Descriptors = descriptors,
                CurrentTree = currentTree,
                PreviousTree = previousTree,
                DeltaRecords = deltaRecords,
                DeltaBytes = deltaBytes,
                Counters = counters
            }.Schedule(dependency);
        }

        internal static JobHandle ScheduleCopyCurrentToPrevious(
            NativeArray<MerkleNodeDTO> currentTree,
            NativeArray<MerkleNodeDTO> previousTree,
            JobHandle dependency)
        {
            return new CopyMerkleTreeJob
            {
                Source = currentTree,
                Destination = previousTree
            }.Schedule(TotalNodeCount, 64, dependency);
        }

        internal static JobHandle ScheduleAcceptCommittedTree(
            NativeArray<MerkleNodeDTO> currentTree,
            NativeArray<MerkleNodeDTO> previousCommittedTree,
            JobHandle dependency)
        {
            return ScheduleCopyCurrentToPrevious(currentTree, previousCommittedTree, dependency);
        }

        internal static JobHandle ScheduleLz4SubBlocks(
            NativeArray<byte> source,
            int sourceLength,
            NativeArray<byte> destination,
            NativeArray<Lz4SubBlockHeader> blockHeaders,
            NativeArray<int> hashTable,
            NativeArray<int> counters,
            SaveMerkleRuntimeConfig config,
            JobHandle dependency)
        {
            return new Lz4SubBlockCompressionJob
            {
                Source = source,
                Destination = destination,
                BlockHeaders = blockHeaders,
                HashTable = hashTable,
                Counters = counters,
                SourceLength = sourceLength,
                SourceLengthCounterIndex = CounterBytes,
                SubBlockBytes = config.SubBlockBytes
            }.Schedule(dependency);
        }

        internal static JobHandle ScheduleCosmeticPayloadPrune(
            NativeArray<byte> sourceDeltaBytes,
            int sourceLength,
            NativeArray<byte> destinationDeltaBytes,
            NativeArray<int> counters,
            SaveMerkleRuntimeConfig config,
            JobHandle dependency)
        {
            return new CosmeticDeltaPayloadPruneJob
            {
                SourceDeltaBytes = sourceDeltaBytes,
                DestinationDeltaBytes = destinationDeltaBytes,
                Counters = counters,
                SourceLength = sourceLength,
                SourceLengthCounterIndex = CounterBytes,
                DropThresholdBytes = config.CosmeticDropThresholdBytes
            }.Schedule(dependency);
        }

        internal static JobHandle ScheduleVaultDeltaWalPipeline(
            NativeArray<byte> sourceBytes,
            SaveMerkleVaultBufferSet buffers,
            SaveMerkleRuntimeConfig config,
            float globalQualityWeight,
            float systemStress01,
            JobHandle dependency)
        {
            SaveMerkleRuntimeConfig resolvedConfig = ResolveRuntimeConfigForQuality(
                config,
                globalQualityWeight,
                systemStress01);
            return ScheduleVaultDeltaWalPipeline(sourceBytes, buffers, resolvedConfig, dependency);
        }

        internal static JobHandle ScheduleVaultDeltaWalPipeline(
            NativeArray<byte> sourceBytes,
            SaveMerkleVaultBufferSet buffers,
            SaveMerkleRuntimeConfig config,
            JobHandle dependency)
        {
            if (!sourceBytes.IsCreated ||
                !buffers.CurrentTree.IsCreated ||
                !buffers.PreviousTree.IsCreated ||
                !buffers.LeafDescriptors.IsCreated ||
                !buffers.DeltaRecords.IsCreated ||
                !buffers.DeltaBytes.IsCreated ||
                !buffers.PrunedDeltaBytes.IsCreated ||
                !buffers.CompressedBytes.IsCreated ||
                !buffers.Lz4BlockHeaders.IsCreated ||
                !buffers.Counters.IsCreated ||
                !buffers.Lz4HashTable.IsCreated)
            {
                return dependency;
            }

            JobHandle baseline = new EnsureCommittedBaselineJob
            {
                TreeNodes = buffers.PreviousTree
            }.Schedule(dependency);
            JobHandle merkle = ScheduleMerkleBuild(
                sourceBytes,
                buffers.LeafDescriptors,
                buffers.CurrentTree,
                baseline);
            JobHandle delta = ScheduleDeltaExtraction(
                sourceBytes,
                buffers.LeafDescriptors,
                buffers.CurrentTree,
                buffers.PreviousTree,
                buffers.DeltaRecords,
                buffers.DeltaBytes,
                buffers.Counters,
                merkle);
            JobHandle prune = ScheduleCosmeticPayloadPrune(
                buffers.DeltaBytes,
                -1,
                buffers.PrunedDeltaBytes,
                buffers.Counters,
                config,
                delta);
            JobHandle lz4 = ScheduleLz4SubBlocks(
                buffers.PrunedDeltaBytes,
                -1,
                buffers.CompressedBytes,
                buffers.Lz4BlockHeaders,
                buffers.Lz4HashTable,
                buffers.Counters,
                config,
                prune);
            return lz4;
        }

        internal static void PublishEditorSnapshot(
            in MerkleNodeDTO root,
            int changedLeafCount,
            uint lastChangedSectorKey,
            int rawBytes,
            int storedBytes,
            int corruptBlockCount)
        {
            PublishEditorSnapshot(
                root,
                changedLeafCount,
                lastChangedSectorKey,
                rawBytes,
                storedBytes,
                corruptBlockCount,
                0UL,
                0UL,
                0UL,
                0UL);
        }

        internal static void PublishEditorSnapshot(
            NativeArray<MerkleNodeDTO> currentTree,
            NativeArray<MerkleNodeDTO> previousTree,
            int changedLeafCount,
            uint lastChangedSectorKey,
            int rawBytes,
            int storedBytes,
            int corruptBlockCount)
        {
            MerkleNodeDTO root = currentTree.IsCreated && currentTree.Length > RootIndex ? currentTree[RootIndex] : default;
            ulong mask0 = 0UL;
            ulong mask1 = 0UL;
            ulong mask2 = 0UL;
            ulong mask3 = 0UL;
            if (currentTree.IsCreated &&
                previousTree.IsCreated &&
                currentTree.Length >= TotalNodeCount &&
                previousTree.Length >= TotalNodeCount)
            {
                for (int i = 0; i < Level2Count; i++)
                {
                    MerkleNodeDTO current = currentTree[Level2Offset + i];
                    MerkleNodeDTO previous = previousTree[Level2Offset + i];
                    if (current.HashLo == previous.HashLo && current.HashHi == previous.HashHi)
                        continue;

                    int lane = i >> 6;
                    ulong bit = 1UL << (i & 63);
                    if (lane == 0)
                        mask0 |= bit;
                    else if (lane == 1)
                        mask1 |= bit;
                    else if (lane == 2)
                        mask2 |= bit;
                    else
                        mask3 |= bit;
                }
            }

            PublishEditorSnapshot(
                root,
                changedLeafCount,
                lastChangedSectorKey,
                rawBytes,
                storedBytes,
                corruptBlockCount,
                mask0,
                mask1,
                mask2,
                mask3);
        }

        private static void PublishEditorSnapshot(
            in MerkleNodeDTO root,
            int changedLeafCount,
            uint lastChangedSectorKey,
            int rawBytes,
            int storedBytes,
            int corruptBlockCount,
            ulong changedBranchBits0,
            ulong changedBranchBits1,
            ulong changedBranchBits2,
            ulong changedBranchBits3)
        {
            s_LastEditorSnapshot = new SaveMerkleEditorSnapshot
            {
                RootHashLo = root.HashLo,
                RootHashHi = root.HashHi,
                ChangedBranchBits0 = changedBranchBits0,
                ChangedBranchBits1 = changedBranchBits1,
                ChangedBranchBits2 = changedBranchBits2,
                ChangedBranchBits3 = changedBranchBits3,
                ChangedLeafCount = (uint)math.max(0, changedLeafCount),
                LeafCount = LeafCount,
                LastChangedSectorKey = lastChangedSectorKey,
                CorruptBlockCount = (uint)math.max(0, corruptBlockCount),
                StoredBytes = (uint)math.max(0, storedBytes),
                RawBytes = (uint)math.max(0, rawBytes),
                SnapshotFlags = (changedBranchBits0 | changedBranchBits1 | changedBranchBits2 | changedBranchBits3) != 0UL ? 1u : 0u,
                _pad0 = 0u
            };

            System.Threading.Volatile.Write(ref s_LastEditorSnapshotVersion, s_LastEditorSnapshotVersion + 1);
        }

        internal static bool TryReadLastEditorSnapshot(out SaveMerkleEditorSnapshot snapshot, out int version)
        {
            version = System.Threading.Volatile.Read(ref s_LastEditorSnapshotVersion);
            snapshot = s_LastEditorSnapshot;
            return version != 0;
        }

        internal static SaveMerkleWalAppendHeader BuildWalHeader(
            ulong rootHashLo,
            ulong rootHashHi,
            uint frame,
            long logicalOffset,
            int rawBytes,
            int storedBytes,
            uint blockCount,
            uint flags,
            long timestampTicks = 0L)
        {
            return new SaveMerkleWalAppendHeader
            {
                Magic = MerkleWalMagic,
                Version = MerkleWalVersion,
                HeaderBytes = (ushort)UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>(),
                Flags = flags,
                BlockCount = blockCount,
                LogicalOffset = logicalOffset,
                RawBytes = rawBytes,
                StoredBytes = storedBytes,
                RootHashLo = rootHashLo,
                RootHashHi = rootHashHi,
                Frame = frame,
                RecordCrc32 = 0u,
                TimestampTicks = timestampTicks < 0L ? 0L : timestampTicks
            };
        }

        internal static bool TryAppendCompressedWalMmf(
            string walPath,
            NativeArray<byte> compressedBytes,
            int byteCount,
            SaveMerkleWalAppendHeader header,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(walPath))
            {
                error = "Merkle WAL path is empty.";
                return false;
            }

            if (!compressedBytes.IsCreated || byteCount <= 0 || byteCount > compressedBytes.Length)
            {
                error = "Merkle WAL payload is empty or out of range.";
                return false;
            }

            try
            {
                string absoluteWalPath = Path.GetFullPath(walPath);
                HectonPersistentPathPolicy.EnsureParentDirectory(absoluteWalPath);
                using FileStream stream = new FileStream(
                    absoluteWalPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite,
                    4096,
                    FileOptions.WriteThrough);

                AsyncWriteManager.InvalidateCachedReadWindows(absoluteWalPath);
                try
                {
                    byte* payload = (byte*)compressedBytes.GetUnsafeReadOnlyPtr();
                    long appendOffset = stream.Length;
                    int headerByteCount = UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>();
                    Span<byte> headerBytes = stackalloc byte[64];
                    if (headerByteCount > headerBytes.Length)
                    {
                        error = "Merkle WAL header exceeds stack serialization budget.";
                        return false;
                    }

                    header.StoredBytes = byteCount;
                    header.LogicalOffset = appendOffset;
                    header.RecordCrc32 = 0u;

                    fixed (byte* headerPtr = headerBytes)
                    {
                        WriteWalAppendHeaderLittleEndian(headerPtr, in header);
                        uint crc = UpdateCrc32(0xFFFFFFFFu, headerPtr, headerByteCount);
                        crc = UpdateCrc32(crc, payload, byteCount);
                        header.RecordCrc32 = FinalizeCrc32(crc);
                        WriteWalAppendHeaderLittleEndian(headerPtr, in header);

                        long appendBytes = headerByteCount + (long)byteCount;
                        long endOffset = appendOffset + appendBytes;

#if UNITY_EDITOR || UNITY_STANDALONE || HECTON8_MMF_AVAILABLE
                        try
                        {
                            stream.SetLength(endOffset);
                            using MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(
                                stream,
                                null,
                                endOffset,
                                MemoryMappedFileAccess.ReadWrite,
                                HandleInheritability.None,
                                true);
                            using MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(
                                appendOffset,
                                appendBytes,
                                MemoryMappedFileAccess.Write);
                            byte* mappedPtr = null;
                            try
                            {
                                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref mappedPtr);
                                byte* target = mappedPtr + (int)accessor.PointerOffset;
                                UnsafeUtility.MemCpy(target, headerPtr, headerByteCount);
                                UnsafeUtility.MemCpy(target + headerByteCount, payload, byteCount);
                                accessor.Flush();
                                stream.Flush(true);
                                return true;
                            }
                            finally
                            {
                                if (mappedPtr != null)
                                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                            }
                        }
                        catch (PlatformNotSupportedException)
                        {
                            stream.SetLength(appendOffset);
                        }
                        catch (Exception)
                        {
                            stream.SetLength(appendOffset);
                        }
#endif

                        stream.Position = appendOffset;
                        stream.Write(headerBytes.Slice(0, headerByteCount));
                        stream.Write(new ReadOnlySpan<byte>(payload, byteCount));
                        stream.Flush(true);
                        return true;
                    }
                }
                finally
                {
                    AsyncWriteManager.InvalidateCachedReadWindows(absoluteWalPath);
                }
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        internal static bool TryValidateWalAndRollback(string walPath, string backupPath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(walPath) || !File.Exists(walPath))
                return true;

            // Validation and rollback must never overlap on the same file. Win32 ReplaceFile refuses a
            // destination that still carries an open handle, so the WAL read stream is scoped to the record
            // scan below and is closed before the .bak is promoted over the primary.
            if (TryValidateWalRecords(walPath, out string corruptionReason))
                return true;

            return TryRestoreBackup(walPath, backupPath, corruptionReason, out error);
        }

        private static bool TryValidateWalRecords(string walPath, out string corruptionReason)
        {
            corruptionReason = string.Empty;
            try
            {
                using FileStream stream = new FileStream(walPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                int headerByteCount = UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>();
                Span<byte> headerBytes = stackalloc byte[64];
                if (headerByteCount > headerBytes.Length)
                {
                    corruptionReason = "Merkle WAL header size exceeds stack parser budget.";
                    return false;
                }

                while (stream.Position < stream.Length)
                {
                    if (!TryReadExact(stream, headerBytes.Slice(0, headerByteCount)))
                    {
                        corruptionReason = "Merkle WAL header truncated.";
                        return false;
                    }

                    SaveMerkleWalAppendHeader header;
                    fixed (byte* headerPtr = headerBytes)
                    {
                        header = ReadWalAppendHeaderLittleEndian(headerPtr);
                    }

                    if (header.Magic != MerkleWalMagic ||
                        header.Version != MerkleWalVersion ||
                        header.HeaderBytes != UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>() ||
                        header.StoredBytes <= 0 ||
                        header.StoredBytes > stream.Length - stream.Position)
                    {
                        corruptionReason = "Merkle WAL header invalid.";
                        return false;
                    }

                    long payloadStart = stream.Position;
                    if (!TryValidateStoredSubBlocks(stream, header.StoredBytes))
                    {
                        if ((header.Flags & LeafFlagModPayload) != 0u)
                        {
                            stream.Position = payloadStart + header.StoredBytes;
                            continue;
                        }

                        corruptionReason = "Merkle WAL LZ4 sub-block CRC failed.";
                        return false;
                    }

                    stream.Position = payloadStart;
                    uint expected = header.RecordCrc32;
                    uint recordCrc;
                    fixed (byte* headerPtr = headerBytes)
                    {
                        WriteUIntLittleEndian(headerPtr, 56, 0u);
                        recordCrc = UpdateCrc32(0xFFFFFFFFu, headerPtr, headerByteCount);
                    }

                    if (!TryUpdateCrcFromStream(stream, header.StoredBytes, ref recordCrc) ||
                        FinalizeCrc32(recordCrc) != expected)
                    {
                        if ((header.Flags & LeafFlagModPayload) != 0u)
                        {
                            stream.Position = payloadStart + header.StoredBytes;
                            continue;
                        }

                        corruptionReason = "Merkle WAL record CRC failed.";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                corruptionReason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        internal static bool TryReplayWalToDeltaArena(
            string walPath,
            NativeArray<byte> destinationDeltaBytes,
            NativeArray<byte> compressedScratchBytes,
            NativeArray<int> counters,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(walPath) || !File.Exists(walPath))
                return true;

            if (!destinationDeltaBytes.IsCreated || !compressedScratchBytes.IsCreated)
            {
                error = "Merkle WAL replay buffers are not allocated.";
                return false;
            }

            try
            {
                using FileStream stream = new FileStream(walPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                int headerByteCount = UnsafeUtility.SizeOf<SaveMerkleWalAppendHeader>();
                Span<byte> headerBytes = stackalloc byte[64];
                if (headerByteCount > headerBytes.Length)
                {
                    error = "Merkle WAL header size exceeds stack parser budget.";
                    return false;
                }

                int write = 0;
                int records = 0;
                int blocks = 0;
                int corruptBlocks = 0;
                int storedBytesTotal = 0;
                byte* scratchPtr = (byte*)compressedScratchBytes.GetUnsafePtr();
                byte* destinationPtr = (byte*)destinationDeltaBytes.GetUnsafePtr();
                if (PointerRangesOverlap(scratchPtr, compressedScratchBytes.Length, destinationPtr, destinationDeltaBytes.Length))
                {
                    error = "Merkle WAL replay destination and scratch buffers overlap.";
                    WriteReplayCounters(counters, 0, 0, 0, 0, 0, failed: true);
                    return false;
                }

                while (stream.Position < stream.Length)
                {
                    if (!TryReadExact(stream, headerBytes.Slice(0, headerByteCount)))
                    {
                        error = "Merkle WAL replay header truncated.";
                        WriteReplayCounters(counters, records, write, blocks, corruptBlocks, storedBytesTotal, failed: true);
                        return false;
                    }

                    SaveMerkleWalAppendHeader header;
                    fixed (byte* headerPtr = headerBytes)
                    {
                        header = ReadWalAppendHeaderLittleEndian(headerPtr);
                    }

                    if (header.Magic != MerkleWalMagic ||
                        header.Version != MerkleWalVersion ||
                        header.HeaderBytes != headerByteCount ||
                        header.RawBytes <= 0 ||
                        header.StoredBytes <= 0 ||
                        header.StoredBytes > compressedScratchBytes.Length ||
                        header.StoredBytes > stream.Length - stream.Position)
                    {
                        error = "Merkle WAL replay header invalid.";
                        WriteReplayCounters(counters, records, write, blocks, corruptBlocks, storedBytesTotal, failed: true);
                        return false;
                    }

                    if (write > destinationDeltaBytes.Length - header.RawBytes)
                    {
                        error = "Merkle WAL replay destination arena overflow.";
                        WriteReplayCounters(counters, records, write, blocks, corruptBlocks, storedBytesTotal, failed: true);
                        return false;
                    }

                    if (!TryReadExact(stream, new Span<byte>(scratchPtr, header.StoredBytes)))
                    {
                        error = "Merkle WAL replay payload truncated.";
                        WriteReplayCounters(counters, records, write, blocks, corruptBlocks, storedBytesTotal, failed: true);
                        return false;
                    }

                    uint expected = header.RecordCrc32;
                    uint recordCrc;
                    fixed (byte* headerPtr = headerBytes)
                    {
                        WriteUIntLittleEndian(headerPtr, 56, 0u);
                        recordCrc = UpdateCrc32(0xFFFFFFFFu, headerPtr, headerByteCount);
                    }

                    recordCrc = UpdateCrc32(recordCrc, scratchPtr, header.StoredBytes);
                    if (FinalizeCrc32(recordCrc) != expected)
                    {
                        corruptBlocks++;
                        if ((header.Flags & LeafFlagModPayload) != 0u)
                            continue;

                        error = "Merkle WAL replay record CRC failed.";
                        WriteReplayCounters(counters, records, write, blocks, corruptBlocks, storedBytesTotal, failed: true);
                        return false;
                    }

                    if (!TryDecodeStoredSubBlocks(
                            scratchPtr,
                            header.StoredBytes,
                            destinationPtr + write,
                            header.RawBytes,
                            out int decodedBlocks))
                    {
                        corruptBlocks++;
                        if ((header.Flags & LeafFlagModPayload) != 0u)
                            continue;

                        error = "Merkle WAL replay sub-block decode failed.";
                        WriteReplayCounters(counters, records, write, blocks, corruptBlocks, storedBytesTotal, failed: true);
                        return false;
                    }

                    write += header.RawBytes;
                    records++;
                    blocks += decodedBlocks;
                    storedBytesTotal += header.StoredBytes;
                }

                WriteReplayCounters(counters, records, write, blocks, corruptBlocks, storedBytesTotal, failed: false);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                WriteReplayCounters(counters, 0, 0, 0, 0, 0, failed: true);
                return false;
            }
        }

        internal static bool TryDumpTelemetry(
            NativeArray<SaveMerkleTelemetryEntry> telemetryRing,
            string dumpPath,
            out string error)
        {
            error = string.Empty;
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
            {
                error = "Merkle telemetry ring is empty.";
                return false;
            }

            if (string.IsNullOrEmpty(dumpPath))
                dumpPath = ResolveDefaultTelemetryDumpPath();

            try
            {
                int bytes = telemetryRing.Length * UnsafeUtility.SizeOf<SaveMerkleTelemetryEntry>();
                byte* ptr = (byte*)telemetryRing.GetUnsafeReadOnlyPtr();
                return NativeFaultDumpWriter.TryWriteAll(dumpPath, new ReadOnlySpan<byte>(ptr, bytes), bytes);
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private static string ResolveDefaultTelemetryDumpPath()
        {
            try
            {
                string root = Directory.GetCurrentDirectory();
                if (!string.IsNullOrEmpty(root))
                {
                    string projectRoot = Path.GetFullPath(root);
                    if (Directory.Exists(Path.Combine(projectRoot, "Assets")) ||
                        Directory.Exists(Path.Combine(projectRoot, "Docs")))
                    {
                        return Path.Combine(projectRoot, "Docs", "AgentLogs", DefaultTelemetryDumpFileName);
                    }
                }
            }
            catch
            {
                // Dump fallback must never become the crash path.
            }

            return HectonPersistentPathPolicy.CombineFile(DefaultTelemetryDumpFileName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ComputeCrc32(byte* data, int byteCount)
        {
            return FinalizeCrc32(UpdateCrc32(0xFFFFFFFFu, data, byteCount));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Hash128(void* ptr, int length, ulong seed, out ulong lo, out ulong hi)
        {
            if (ptr == null || length <= 0)
            {
                lo = 0UL;
                hi = 0UL;
                return;
            }

            lo = MemorySentinelMath.ComputeDeterministicHash64(ptr, length, seed ^ 0x243F6A8885A308D3UL);
            hi = MemorySentinelMath.ComputeDeterministicHash64(ptr, length, seed ^ 0x13198A2E03707344UL);
        }

        private static bool TryRestoreBackup(string walPath, string backupPath, string reason, out string error)
        {
            error = reason + " No .bak available.";
            if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
                return false;

            string restoreTempPath = walPath + ".restore.tmp";
            try
            {
                string absoluteWalPath = Path.GetFullPath(walPath);
                string absoluteBackupPath = Path.GetFullPath(backupPath);

                // Same file as restoreTempPath - File.* and CreateFileW both resolve a relative path against
                // the process CWD - but the native length/flush route needs a rooted path so it can also
                // fsync the parent directory.
                string absoluteRestoreTempPath = absoluteWalPath + ".restore.tmp";
                if (!AsyncWriteManager.TryGetFileLength(absoluteBackupPath, out long backupBytes, out string backupLengthError))
                {
                    error = reason + " .bak length could not be resolved. " + backupLengthError;
                    return false;
                }

                HectonPersistentPathPolicy.EnsureParentDirectory(absoluteWalPath);
                AsyncWriteManager.InvalidateCachedReadWindows(absoluteWalPath);
                try
                {
                    // Stage the .bak into a fresh temp, force it to the platter, prove its length, and only
                    // then replace the primary. Copying the .bak straight over the live WAL truncates the
                    // primary to zero first, so a crash mid-copy destroys the very file the rollback exists
                    // to restore. The temp is deleted before the copy so overwrite: false is a real
                    // assertion that nothing stale is being promoted.
                    DeleteRestoreTempIfExists(restoreTempPath);
                    File.Copy(backupPath, restoreTempPath, false);
                    if (!AsyncWriteManager.TryGetFileLength(absoluteRestoreTempPath, out long stagedBytes, out string stagedLengthError))
                    {
                        error = reason + " Staged .bak length could not be resolved. " + stagedLengthError;
                        return false;
                    }

                    if (stagedBytes != backupBytes)
                    {
                        error = reason + " Staged .bak length mismatch.";
                        return false;
                    }

                    if (!AsyncWriteManager.FlushCriticalSavePath(absoluteRestoreTempPath, stagedBytes, out string stagedFlushError))
                    {
                        error = reason + " Staged .bak flush failed. " + stagedFlushError;
                        return false;
                    }

                    if (!TryPromoteRestoreTemp(restoreTempPath, walPath))
                    {
                        // ReplaceFile refused the hand-off (a filesystem without rename-over support, or a
                        // handle this call does not own). Completing the rollback still beats leaving a
                        // corrupt primary in place, so fall back to a direct overwrite from the .bak; the
                        // length and flush proof below is what actually gates acceptance either way.
                        File.Copy(absoluteBackupPath, absoluteWalPath, true);
                    }
                }
                finally
                {
                    AsyncWriteManager.InvalidateCachedReadWindows(absoluteWalPath);
                    DeleteRestoreTempIfExists(restoreTempPath);
                }

                if (!AsyncWriteManager.TryGetFileLength(absoluteWalPath, out long restoredBytes, out string restoredLengthError))
                {
                    error = reason + " Restored .bak length could not be resolved. " + restoredLengthError;
                    return false;
                }

                if (restoredBytes != backupBytes)
                {
                    error = reason + " Restored .bak length mismatch.";
                    return false;
                }

                if (!AsyncWriteManager.FlushCriticalSavePath(absoluteWalPath, restoredBytes, out string flushError))
                {
                    error = reason + " Restored .bak flush failed. " + flushError;
                    return false;
                }

                error = reason + " Restored .bak.";
            }
            catch (IOException exception)
            {
                error = reason + " .bak restore failed. " + exception.GetType().Name + ": " + exception.Message;
            }
            catch (UnauthorizedAccessException exception)
            {
                error = reason + " .bak restore failed. " + exception.GetType().Name + ": " + exception.Message;
            }
            catch (System.Security.SecurityException exception)
            {
                error = reason + " .bak restore failed. " + exception.GetType().Name + ": " + exception.Message;
            }
            catch (ArgumentException exception)
            {
                error = reason + " .bak restore failed. " + exception.GetType().Name + ": " + exception.Message;
            }
            catch (NotSupportedException exception)
            {
                error = reason + " .bak restore failed. " + exception.GetType().Name + ": " + exception.Message;
            }

            return false;
        }

        /// <summary>
        /// Atomic hand-off of the staged restore temp onto the primary WAL path. ReplaceFile is the only
        /// crash-safe option so it is the only one tried here; a refusal is reported to the caller, which
        /// owns the non-atomic last resort. Win32 ReplaceFile fails if the destination still has an open
        /// handle, which is why the WAL validation stream is closed before any rollback begins.
        /// </summary>
        private static bool TryPromoteRestoreTemp(string restoreTempPath, string walPath)
        {
            try
            {
                if (!File.Exists(walPath))
                {
                    File.Move(restoreTempPath, walPath);
                    return true;
                }

                File.Replace(restoreTempPath, walPath, null, true);
                return true;
            }
            catch (PlatformNotSupportedException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void DeleteRestoreTempIfExists(string restoreTempPath)
        {
            if (string.IsNullOrEmpty(restoreTempPath))
                return;

            try
            {
                if (File.Exists(restoreTempPath))
                    File.Delete(restoreTempPath);
            }
            catch (IOException)
            {
                // A stranded restore temp must never become the crash path; File.Copy below fails loudly
                // instead because it is called with overwrite: false.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static bool TryReadExact(FileStream stream, Span<byte> destination)
        {
            int total = 0;
            while (total < destination.Length)
            {
                int read = stream.Read(destination.Slice(total));
                if (read <= 0)
                    return false;

                total += read;
            }

            return true;
        }

        private static bool TryValidateStoredSubBlocks(FileStream stream, int storedBytes)
        {
            long payloadEnd = stream.Position + storedBytes;
            int headerByteCount = UnsafeUtility.SizeOf<Lz4SubBlockHeader>();
            Span<byte> headerBytes = stackalloc byte[32];
            if (headerByteCount > headerBytes.Length)
                return false;

            while (stream.Position < payloadEnd)
            {
                if (payloadEnd - stream.Position < headerByteCount)
                    return false;

                if (!TryReadExact(stream, headerBytes.Slice(0, headerByteCount)))
                {
                    return false;
                }

                Lz4SubBlockHeader header;
                fixed (byte* headerPtr = headerBytes)
                {
                    header = ReadLz4SubBlockHeaderLittleEndian(headerPtr);
                }

                if (header.Magic != Lz4BlockMagic ||
                    header.Version != Lz4BlockVersion ||
                    header.HeaderBytes != UnsafeUtility.SizeOf<Lz4SubBlockHeader>() ||
                    header.StoredBytes <= 0 ||
                    header.StoredBytes > header.RawBytes ||
                    header.RawBytes <= 0 ||
                    stream.Position + header.StoredBytes > payloadEnd)
                {
                    return false;
                }

                uint storageFlags = header.Flags & (Lz4BlockFlagRaw | Lz4BlockFlagCompressed | Lz4BlockFlagRle | Lz4BlockFlagRleLegacy);
                if (storageFlags != Lz4BlockFlagRaw &&
                    storageFlags != Lz4BlockFlagCompressed &&
                    storageFlags != Lz4BlockFlagRle &&
                    storageFlags != Lz4BlockFlagRleLegacy)
                    return false;

                uint crc = 0xFFFFFFFFu;
                if (!TryUpdateCrcFromStream(stream, header.StoredBytes, ref crc) ||
                    FinalizeCrc32(crc) != header.Crc32)
                {
                    return false;
                }

                long aligned = Align16(stream.Position - (payloadEnd - storedBytes)) + (payloadEnd - storedBytes);
                if (aligned > payloadEnd)
                    return false;

                stream.Position = aligned;
            }

            return stream.Position == payloadEnd;
        }

        private static unsafe bool TryDecodeStoredSubBlocks(
            byte* source,
            int storedBytes,
            byte* destination,
            int expectedRawBytes,
            out int decodedBlocks)
        {
            decodedBlocks = 0;
            if (source == null || destination == null || storedBytes <= 0 || expectedRawBytes <= 0)
                return false;

            int read = 0;
            int rawWrite = 0;
            int headerByteCount = UnsafeUtility.SizeOf<Lz4SubBlockHeader>();
            while (read < storedBytes)
            {
                if (storedBytes - read < headerByteCount)
                    return false;

                Lz4SubBlockHeader header = ReadLz4SubBlockHeaderLittleEndian(source + read);
                read += headerByteCount;
                if (header.Magic != Lz4BlockMagic ||
                    header.Version != Lz4BlockVersion ||
                    header.HeaderBytes != headerByteCount ||
                    header.RawBytes <= 0 ||
                    header.StoredBytes <= 0 ||
                    header.StoredBytes > header.RawBytes ||
                    header.SourceOffsetBytes != rawWrite ||
                    read > storedBytes - header.StoredBytes ||
                    rawWrite > expectedRawBytes - header.RawBytes)
                {
                    return false;
                }

                byte* payload = source + read;
                if (ComputeCrc32(payload, header.StoredBytes) != header.Crc32)
                    return false;

                uint storageFlags = header.Flags & (Lz4BlockFlagRaw | Lz4BlockFlagCompressed | Lz4BlockFlagRle | Lz4BlockFlagRleLegacy);
                byte* blockDestination = destination + header.SourceOffsetBytes;
                if (storageFlags == Lz4BlockFlagRaw)
                {
                    if (header.StoredBytes != header.RawBytes)
                        return false;

                    UnsafeUtility.MemCpy(blockDestination, payload, header.RawBytes);
                }
                else if (storageFlags == Lz4BlockFlagRle || storageFlags == Lz4BlockFlagRleLegacy)
                {
                    if (!TryDecodeRleBlock(payload, header.StoredBytes, blockDestination, header.RawBytes))
                        return false;
                }
                else if (storageFlags == Lz4BlockFlagCompressed)
                {
                    if (!TryDecodeLz4Block(payload, header.StoredBytes, blockDestination, header.RawBytes))
                        return false;
                }
                else
                {
                    return false;
                }

                read = Align16(read + header.StoredBytes);
                if (read > storedBytes)
                    return false;

                rawWrite += header.RawBytes;
                decodedBlocks++;
            }

            return read == storedBytes && rawWrite == expectedRawBytes;
        }

        private static unsafe bool TryDecodeRleBlock(byte* input, int inputBytes, byte* output, int expectedOutputBytes)
        {
            int read = 0;
            int write = 0;
            while (read < inputBytes)
            {
                if (inputBytes - read < 3)
                    return false;

                byte value = input[read++];
                int run = input[read] | (input[read + 1] << 8);
                read += 2;
                if (run <= 0 || write > expectedOutputBytes - run)
                    return false;

                for (int i = 0; i < run; i++)
                    output[write++] = value;
            }

            return read == inputBytes && write == expectedOutputBytes;
        }

        private static unsafe bool TryDecodeLz4Block(byte* input, int inputBytes, byte* output, int expectedOutputBytes)
        {
            int read = 0;
            int write = 0;
            while (read < inputBytes)
            {
                byte token = input[read++];
                int literalLength = token >> 4;
                if (!TryReadLz4Length(input, inputBytes, ref read, ref literalLength))
                    return false;

                if (literalLength < 0 || write > expectedOutputBytes - literalLength || read > inputBytes - literalLength)
                    return false;

                for (int i = 0; i < literalLength; i++)
                    output[write++] = input[read++];

                if (read == inputBytes)
                    break;

                if (inputBytes - read < 2)
                    return false;

                int offset = input[read] | (input[read + 1] << 8);
                read += 2;
                if (offset <= 0 || offset > write)
                    return false;

                int matchLength = token & 0x0F;
                if (!TryReadLz4Length(input, inputBytes, ref read, ref matchLength))
                    return false;

                matchLength += 4;
                if (write > expectedOutputBytes - matchLength)
                    return false;

                int match = write - offset;
                for (int i = 0; i < matchLength; i++)
                    output[write++] = output[match + i];
            }

            return read == inputBytes && write == expectedOutputBytes;
        }

        private static unsafe bool PointerRangesOverlap(byte* left, int leftBytes, byte* right, int rightBytes)
        {
            if (left == null || right == null || leftBytes <= 0 || rightBytes <= 0)
                return false;

            ulong leftStart = (ulong)left;
            ulong rightStart = (ulong)right;
            ulong leftEnd = leftStart + (ulong)leftBytes;
            ulong rightEnd = rightStart + (ulong)rightBytes;
            return leftStart < rightEnd && rightStart < leftEnd;
        }

        private static unsafe bool TryReadLz4Length(byte* input, int inputBytes, ref int read, ref int length)
        {
            if (length < 15)
                return true;

            int value;
            do
            {
                if (read >= inputBytes || length > int.MaxValue - 255)
                    return false;

                value = input[read++];
                length += value;
            }
            while (value == 255);

            return true;
        }

        private static void WriteReplayCounters(
            NativeArray<int> counters,
            int records,
            int bytes,
            int blocks,
            int corruptBlocks,
            int storedBytes,
            bool failed)
        {
            if (!counters.IsCreated)
                return;

            if (counters.Length > CounterRecords)
                counters[CounterRecords] = records;
            if (counters.Length > CounterBytes)
                counters[CounterBytes] = bytes;
            if (counters.Length > CounterBlockCount)
                counters[CounterBlockCount] = blocks;
            if (counters.Length > CounterFailure)
                counters[CounterFailure] = failed ? 1 : 0;
            if (counters.Length > CounterStoredBytes)
                counters[CounterStoredBytes] = storedBytes;
            if (counters.Length > CounterDroppedCosmeticRecords)
                counters[CounterDroppedCosmeticRecords] = corruptBlocks;
        }

        private static bool TryUpdateCrcFromStream(FileStream stream, int byteCount, ref uint crc)
        {
            Span<byte> chunk = stackalloc byte[4096];
            int remaining = byteCount;
            while (remaining > 0)
            {
                int readLength = math.min(remaining, chunk.Length);
                Span<byte> slice = chunk.Slice(0, readLength);
                int total = 0;
                while (total < readLength)
                {
                    int read = stream.Read(slice.Slice(total));
                    if (read <= 0)
                        return false;

                    total += read;
                }

                fixed (byte* ptr = slice)
                {
                    crc = UpdateCrc32(crc, ptr, readLength);
                }

                remaining -= readLength;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteWalAppendHeaderLittleEndian(byte* destination, in SaveMerkleWalAppendHeader header)
        {
            WriteLongLittleEndian(destination, 0, header.LogicalOffset);
            WriteLongLittleEndian(destination, 8, header.TimestampTicks);
            WriteULongLittleEndian(destination, 16, header.RootHashLo);
            WriteULongLittleEndian(destination, 24, header.RootHashHi);
            WriteIntLittleEndian(destination, 32, header.RawBytes);
            WriteIntLittleEndian(destination, 36, header.StoredBytes);
            WriteUIntLittleEndian(destination, 40, header.Magic);
            WriteUIntLittleEndian(destination, 44, header.Flags);
            WriteUIntLittleEndian(destination, 48, header.BlockCount);
            WriteUIntLittleEndian(destination, 52, header.Frame);
            WriteUIntLittleEndian(destination, 56, header.RecordCrc32);
            WriteUShortLittleEndian(destination, 60, header.Version);
            WriteUShortLittleEndian(destination, 62, header.HeaderBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteEmergencyHeaderLittleEndian(byte* destination, in SaveMerkleEmergencyHeader64 header)
        {
            WriteULongLittleEndian(destination, 0, header.TimestampTicks);
            WriteULongLittleEndian(destination, 8, header.RootHashLo);
            WriteULongLittleEndian(destination, 16, header.RootHashHi);
            WriteULongLittleEndian(destination, 24, header._pad0);
            WriteULongLittleEndian(destination, 32, header._pad1);
            WriteUIntLittleEndian(destination, 40, header.Magic);
            WriteUIntLittleEndian(destination, 44, header.SectorEntryBytes);
            WriteUIntLittleEndian(destination, 48, header.MerkleNodeBytes);
            WriteUIntLittleEndian(destination, 52, header.Flags);
            WriteUIntLittleEndian(destination, 56, header.Checksum);
            WriteUShortLittleEndian(destination, 60, header.Version);
            WriteUShortLittleEndian(destination, 62, header.HeaderBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe SaveMerkleWalAppendHeader ReadWalAppendHeaderLittleEndian(byte* source)
        {
            return new SaveMerkleWalAppendHeader
            {
                LogicalOffset = ReadLongLittleEndian(source, 0),
                TimestampTicks = ReadLongLittleEndian(source, 8),
                RootHashLo = ReadULongLittleEndian(source, 16),
                RootHashHi = ReadULongLittleEndian(source, 24),
                RawBytes = ReadIntLittleEndian(source, 32),
                StoredBytes = ReadIntLittleEndian(source, 36),
                Magic = ReadUIntLittleEndian(source, 40),
                Flags = ReadUIntLittleEndian(source, 44),
                BlockCount = ReadUIntLittleEndian(source, 48),
                Frame = ReadUIntLittleEndian(source, 52),
                RecordCrc32 = ReadUIntLittleEndian(source, 56),
                Version = ReadUShortLittleEndian(source, 60),
                HeaderBytes = ReadUShortLittleEndian(source, 62)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteLz4SubBlockHeaderLittleEndian(byte* destination, Lz4SubBlockHeader header)
        {
            WriteUIntLittleEndian(destination, 0, header.Magic);
            WriteIntLittleEndian(destination, 4, header.RawBytes);
            WriteIntLittleEndian(destination, 8, header.StoredBytes);
            WriteIntLittleEndian(destination, 12, header.SourceOffsetBytes);
            WriteUIntLittleEndian(destination, 16, header.Crc32);
            WriteUIntLittleEndian(destination, 20, header.Flags);
            WriteUShortLittleEndian(destination, 24, header.Version);
            WriteUShortLittleEndian(destination, 26, header.HeaderBytes);
            WriteUIntLittleEndian(destination, 28, header._pad0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe Lz4SubBlockHeader ReadLz4SubBlockHeaderLittleEndian(byte* source)
        {
            return new Lz4SubBlockHeader
            {
                Magic = ReadUIntLittleEndian(source, 0),
                RawBytes = ReadIntLittleEndian(source, 4),
                StoredBytes = ReadIntLittleEndian(source, 8),
                SourceOffsetBytes = ReadIntLittleEndian(source, 12),
                Crc32 = ReadUIntLittleEndian(source, 16),
                Flags = ReadUIntLittleEndian(source, 20),
                Version = ReadUShortLittleEndian(source, 24),
                HeaderBytes = ReadUShortLittleEndian(source, 26),
                _pad0 = ReadUIntLittleEndian(source, 28)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteStateDeltaRecordLittleEndian(byte* destination, in StateDeltaRecordDTO record)
        {
            WriteULongLittleEndian(destination, 0, record.PreviousHashLo);
            WriteULongLittleEndian(destination, 8, record.PreviousHashHi);
            WriteULongLittleEndian(destination, 16, record.NewHashLo);
            WriteULongLittleEndian(destination, 24, record.NewHashHi);
            WriteIntLittleEndian(destination, 32, record.SourceOffsetBytes);
            WriteIntLittleEndian(destination, 36, record.DataLength);
            WriteIntLittleEndian(destination, 40, record.DeltaPayloadOffset);
            WriteIntLittleEndian(destination, 44, record.CompressedOffset);
            WriteUIntLittleEndian(destination, 48, record.SectorKey);
            WriteUIntLittleEndian(destination, 52, record.Flags);
            WriteUIntLittleEndian(destination, 56, record.Crc32);
            WriteUIntLittleEndian(destination, 60, record._pad0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe StateDeltaRecordDTO ReadStateDeltaRecordLittleEndian(byte* source)
        {
            return new StateDeltaRecordDTO
            {
                PreviousHashLo = ReadULongLittleEndian(source, 0),
                PreviousHashHi = ReadULongLittleEndian(source, 8),
                NewHashLo = ReadULongLittleEndian(source, 16),
                NewHashHi = ReadULongLittleEndian(source, 24),
                SourceOffsetBytes = ReadIntLittleEndian(source, 32),
                DataLength = ReadIntLittleEndian(source, 36),
                DeltaPayloadOffset = ReadIntLittleEndian(source, 40),
                CompressedOffset = ReadIntLittleEndian(source, 44),
                SectorKey = ReadUIntLittleEndian(source, 48),
                Flags = ReadUIntLittleEndian(source, 52),
                Crc32 = ReadUIntLittleEndian(source, 56),
                _pad0 = ReadUIntLittleEndian(source, 60)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteULongLittleEndian(byte* ptr, int offset, ulong value)
        {
            WriteUIntLittleEndian(ptr, offset, unchecked((uint)value));
            WriteUIntLittleEndian(ptr, offset + 4, unchecked((uint)(value >> 32)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteLongLittleEndian(byte* ptr, int offset, long value)
        {
            WriteULongLittleEndian(ptr, offset, unchecked((ulong)value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteUIntLittleEndian(byte* ptr, int offset, uint value)
        {
            ptr[offset] = unchecked((byte)value);
            ptr[offset + 1] = unchecked((byte)(value >> 8));
            ptr[offset + 2] = unchecked((byte)(value >> 16));
            ptr[offset + 3] = unchecked((byte)(value >> 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteIntLittleEndian(byte* ptr, int offset, int value)
        {
            WriteUIntLittleEndian(ptr, offset, unchecked((uint)value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteUShortLittleEndian(byte* ptr, int offset, ushort value)
        {
            ptr[offset] = unchecked((byte)value);
            ptr[offset + 1] = unchecked((byte)(value >> 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ulong ReadULongLittleEndian(byte* ptr, int offset)
        {
            return ReadUIntLittleEndian(ptr, offset) | ((ulong)ReadUIntLittleEndian(ptr, offset + 4) << 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe long ReadLongLittleEndian(byte* ptr, int offset)
        {
            return unchecked((long)ReadULongLittleEndian(ptr, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint ReadUIntLittleEndian(byte* ptr, int offset)
        {
            return ptr[offset] |
                   ((uint)ptr[offset + 1] << 8) |
                   ((uint)ptr[offset + 2] << 16) |
                   ((uint)ptr[offset + 3] << 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int ReadIntLittleEndian(byte* ptr, int offset)
        {
            return unchecked((int)ReadUIntLittleEndian(ptr, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ushort ReadUShortLittleEndian(byte* ptr, int offset)
        {
            return (ushort)(ptr[offset] | (ptr[offset + 1] << 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Align16(int value)
        {
            return (value + 15) & ~15;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long Align16(long value)
        {
            return (value + 15L) & ~15L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint UpdateCrc32(uint crc, byte* data, int byteCount)
        {
            if (data == null || byteCount <= 0)
                return crc;

            for (int i = 0; i < byteCount; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = 0u - (crc & 1u);
                    crc = (crc >> 1) ^ (0xEDB88320u & mask);
                }
            }

            return crc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FinalizeCrc32(uint crc)
        {
            return ~crc;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct MockInventoryDataGeneratorJob : IJobParallelFor
        {
            [NoAlias]
            public NativeArray<MockInventoryData> Inventory;
            public uint Seed;

            public void Execute(int index)
            {
                if (!Inventory.IsCreated)
                    return;

                uint state = Seed ^ ((uint)index * 0x9E3779B9u);
                MockInventoryData dto = default;
                dto.ItemId = Mix32(state);
                dto.Count = (Mix32(state + 1u) & 0xFFu) + 1u;
                dto.Flags = 1u;
                dto.StableSeed = Mix32(state + 2u);
                byte* payload = dto.Payload;
                for (int i = 0; i < 112; i++)
                {
                    state = Mix32(state + (uint)i + 3u);
                    payload[i] = unchecked((byte)(state >> 24));
                }

                Inventory[index] = dto;
            }

            private static uint Mix32(uint value)
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
        internal struct MockInventoryMutationJob : IJob
        {
            [NoAlias]
            public NativeArray<MockInventoryData> Inventory;
            public int ElementIndex;
            public int DeepByteOffset;
            public uint XorMask;

            public void Execute()
            {
                if (!Inventory.IsCreated || Inventory.Length <= 0)
                    return;

                int index = math.clamp(ElementIndex, 0, Inventory.Length - 1);
                int stride = UnsafeUtility.SizeOf<MockInventoryData>();
                int offset = math.clamp(DeepByteOffset, 16, stride - sizeof(uint)) & ~3;
                byte* basePtr = (byte*)Inventory.GetUnsafePtr();
                uint* target = (uint*)(basePtr + (index * stride) + offset);
                *target ^= XorMask == 0u ? 0xA11CE34u : XorMask;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct MockInventoryLeafDescriptorJob : IJobParallelFor
        {
            [NoAlias]
            public NativeArray<StateLeafDescriptor> Descriptors;
            public int SourceByteLength;
            public int LeafByteStride;
            public uint SectorKeyBase;

            public void Execute(int index)
            {
                if (!Descriptors.IsCreated || index >= Descriptors.Length)
                    return;

                int stride = math.max(1, LeafByteStride);
                int offset = index * stride;
                int byteLength = offset >= SourceByteLength ? 0 : math.min(stride, SourceByteLength - offset);
                Descriptors[index] = new StateLeafDescriptor
                {
                    SectorKey = SectorKeyBase + (uint)index,
                    Flags = 0u,
                    SourceOffsetBytes = offset,
                    ByteLength = byteLength,
                    RecordStrideBytes = UnsafeUtility.SizeOf<MockInventoryData>(),
                    TombstoneOffsetBytes = 8,
                    TombstoneAliveMask = 1u,
                    _pad0 = 0u
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct DearLieDehydrationJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<double3> AbsoluteUniverseMeters;
            [ReadOnly, NoAlias] public NativeArray<double3> SectorOriginMeters;
            [ReadOnly, NoAlias] public NativeArray<uint> SectorKeys;
            [ReadOnly, NoAlias] public NativeArray<byte> MovingFlags;
            [NoAlias]
            public NativeArray<MockStatePayload> OutputPayloads;
            public double3 ReferenceAupMeters;
            public float FarSectorDistanceMeters;
            public int SectorSizeMeters;

            public void Execute(int index)
            {
                if (!AbsoluteUniverseMeters.IsCreated ||
                    !SectorKeys.IsCreated ||
                    !MovingFlags.IsCreated ||
                    !OutputPayloads.IsCreated ||
                    index >= AbsoluteUniverseMeters.Length ||
                    index >= SectorKeys.Length ||
                    index >= MovingFlags.Length ||
                    index >= OutputPayloads.Length)
                {
                    return;
                }

                double3 absolute = AbsoluteUniverseMeters[index];
                if (!IsFinite3(absolute) || !IsFinite3(ReferenceAupMeters))
                {
                    OutputPayloads[index] = default;
                    return;
                }

                double farDistance = math.max(0d, FarSectorDistanceMeters);
                double3 relative = absolute - ReferenceAupMeters;
                bool farSector = farDistance > 0d && math.lengthsq(relative) >= farDistance * farDistance;
                bool moving = MovingFlags[index] != 0;
                uint flags = farSector || !moving ? LeafFlagStableRestState : LeafFlagNeedsWake;
                bool hasSectorOrigin =
                    SectorOriginMeters.IsCreated &&
                    index < SectorOriginMeters.Length &&
                    IsFinite3(SectorOriginMeters[index]);
                OutputPayloads[index] = new MockStatePayload
                {
                    LocalAup = hasSectorOrigin
                        ? QuantizeAupForSave(absolute, SectorOriginMeters[index], SectorKeys[index], SectorSizeMeters, flags)
                        : QuantizeAupForSave(absolute, SectorKeys[index], SectorSizeMeters, flags)
                };
            }

            private static bool IsFinite3(double3 value)
            {
                return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct MerkleLeafHashJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<byte> SourceBytes;
            [ReadOnly, NoAlias] public NativeArray<StateLeafDescriptor> Descriptors;
            [NoAlias]
            public NativeArray<MerkleNodeDTO> TreeNodes;

            public void Execute(int index)
            {
                if (!TreeNodes.IsCreated || TreeNodes.Length < TotalNodeCount)
                    return;

                StateLeafDescriptor descriptor = default;
                descriptor.SectorKey = (uint)index;
                if (Descriptors.IsCreated && index < Descriptors.Length)
                    descriptor = Descriptors[index];

                MerkleNodeDTO node = default;

                if (SourceBytes.IsCreated &&
                    descriptor.ByteLength > 0 &&
                    descriptor.SourceOffsetBytes >= 0 &&
                    descriptor.SourceOffsetBytes <= SourceBytes.Length - descriptor.ByteLength &&
                    (descriptor.Flags & LeafFlagTombstone) == 0u)
                {
                    byte* source = (byte*)SourceBytes.GetUnsafeReadOnlyPtr() + descriptor.SourceOffsetBytes;
                    Hash128(source, descriptor.ByteLength, LeafSeed ^ descriptor.SectorKey, out node.HashLo, out node.HashHi);
                    node.SectorKey = descriptor.SectorKey;
                    node.ChildMask = 1u;
                }

                TreeNodes[LeafLevelOffset + index] = node;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct MerkleBranchReductionJob : IJob
        {
            [NoAlias]
            public NativeArray<MerkleNodeDTO> TreeNodes;

            public void Execute()
            {
                if (!TreeNodes.IsCreated || TreeNodes.Length < TotalNodeCount)
                    return;

                ReduceLevel(LeafLevelOffset, Level2Offset, Level2Count, 2u);
                ReduceLevel(Level2Offset, Level1Offset, Level1Count, 1u);
                ReduceLevel(Level1Offset, RootIndex, 1, 0u);
            }

            private void ReduceLevel(int childOffset, int parentOffset, int parentCount, uint level)
            {
                int nodeBytes = UnsafeUtility.SizeOf<MerkleNodeDTO>();
                byte* treePtr = (byte*)TreeNodes.GetUnsafePtr();
                for (int parent = 0; parent < parentCount; parent++)
                {
                    int childStart = childOffset + (parent * Fanout);
                    uint childMask = 0u;
                    for (int child = 0; child < Fanout; child++)
                    {
                        MerkleNodeDTO childNode = TreeNodes[childStart + child];
                        if ((childNode.HashLo | childNode.HashHi) != 0UL)
                            childMask |= 1u << child;
                    }

                    MerkleNodeDTO node = default;
                    if (childMask != 0u)
                    {
                        node.SectorKey = (uint)parent;
                        node.ChildMask = childMask;
                        Hash128(treePtr + (childStart * nodeBytes), Fanout * nodeBytes, NodeSeed ^ ((ulong)level << 32) ^ (uint)parent, out node.HashLo, out node.HashHi);
                    }

                    TreeNodes[parentOffset + parent] = node;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct MerkleChangedLeafExtractionJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> SourceBytes;
            [ReadOnly, NoAlias] public NativeArray<StateLeafDescriptor> Descriptors;
            [ReadOnly, NoAlias] public NativeArray<MerkleNodeDTO> CurrentTree;
            [ReadOnly, NoAlias] public NativeArray<MerkleNodeDTO> PreviousTree;
            [NoAlias]
            public NativeArray<StateDeltaRecordDTO> DeltaRecords;
            [NoAlias]
            public NativeArray<byte> DeltaBytes;
            [NoAlias]
            public NativeArray<int> Counters;

            public void Execute()
            {
                if (!SourceBytes.IsCreated ||
                    !Descriptors.IsCreated ||
                    !CurrentTree.IsCreated ||
                    !PreviousTree.IsCreated ||
                    !DeltaRecords.IsCreated ||
                    !DeltaBytes.IsCreated ||
                    !Counters.IsCreated ||
                    Counters.Length < 4 ||
                    CurrentTree.Length < TotalNodeCount ||
                    PreviousTree.Length < TotalNodeCount)
                {
                    return;
                }

                Counters[CounterRecords] = 0;
                Counters[CounterBytes] = 0;
                Counters[CounterChangedLeaves] = 0;
                Counters[CounterFlags] = 0;

                MerkleNodeDTO currentRoot = CurrentTree[RootIndex];
                MerkleNodeDTO previousRoot = PreviousTree[RootIndex];
                if (currentRoot.HashLo == previousRoot.HashLo && currentRoot.HashHi == previousRoot.HashHi)
                    return;

                byte* sourcePtr = (byte*)SourceBytes.GetUnsafeReadOnlyPtr();
                byte* deltaPtr = (byte*)DeltaBytes.GetUnsafePtr();
                int headerBytes = UnsafeUtility.SizeOf<StateDeltaRecordDTO>();
                int recordCount = 0;
                int byteCursor = 0;
                int changedLeaves = 0;
                uint flags = 0u;

                for (int i = 0; i < LeafCount && i < Descriptors.Length; i++)
                {
                    MerkleNodeDTO current = CurrentTree[LeafLevelOffset + i];
                    MerkleNodeDTO previous = PreviousTree[LeafLevelOffset + i];
                    if (current.HashLo == previous.HashLo && current.HashHi == previous.HashHi)
                        continue;

                    StateLeafDescriptor descriptor = Descriptors[i];
                    if ((descriptor.Flags & LeafFlagTombstone) != 0u ||
                        descriptor.ByteLength <= 0 ||
                        descriptor.SourceOffsetBytes < 0 ||
                        descriptor.SourceOffsetBytes > SourceBytes.Length - descriptor.ByteLength)
                    {
                        continue;
                    }

                    changedLeaves++;
                    int required = headerBytes + descriptor.ByteLength;
                    if (recordCount >= DeltaRecords.Length || byteCursor > DeltaBytes.Length - required)
                    {
                        flags |= DeltaFlagOverflow;
                        break;
                    }

                    StateDeltaRecordDTO record = new StateDeltaRecordDTO
                    {
                        SectorKey = descriptor.SectorKey,
                        Flags = descriptor.Flags | (IsModPayloadSector(descriptor.SectorKey) ? LeafFlagModPayload : 0u),
                        SourceOffsetBytes = descriptor.SourceOffsetBytes,
                        DataLength = descriptor.ByteLength,
                        DeltaPayloadOffset = byteCursor + headerBytes,
                        CompressedOffset = -1,
                        PreviousHashLo = previous.HashLo,
                        PreviousHashHi = previous.HashHi,
                        NewHashLo = current.HashLo,
                        NewHashHi = current.HashHi,
                        Crc32 = ComputeCrc32(sourcePtr + descriptor.SourceOffsetBytes, descriptor.ByteLength),
                        _pad0 = 0u
                    };

                    DeltaRecords[recordCount] = record;
                    WriteStateDeltaRecordLittleEndian(deltaPtr + byteCursor, record);
                    UnsafeUtility.MemCpy(deltaPtr + byteCursor + headerBytes, sourcePtr + descriptor.SourceOffsetBytes, descriptor.ByteLength);
                    byteCursor += required;
                    recordCount++;
                }

                Counters[CounterRecords] = recordCount;
                Counters[CounterBytes] = byteCursor;
                Counters[CounterChangedLeaves] = changedLeaves;
                Counters[CounterFlags] = unchecked((int)flags);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct CopyMerkleTreeJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<MerkleNodeDTO> Source;
            [NoAlias]
            public NativeArray<MerkleNodeDTO> Destination;

            public void Execute(int index)
            {
                if (!Source.IsCreated || !Destination.IsCreated || index >= Source.Length || index >= Destination.Length)
                    return;

                MerkleNodeDTO node = Source[index];
                if (index == RootIndex)
                    node._pad0 = CommittedTreeSentinel;

                Destination[index] = node;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct EnsureCommittedBaselineJob : IJob
        {
            [NoAlias]
            public NativeArray<MerkleNodeDTO> TreeNodes;

            public void Execute()
            {
                if (!TreeNodes.IsCreated || TreeNodes.Length < TotalNodeCount)
                    return;

                MerkleNodeDTO root = TreeNodes[RootIndex];
                if (root._pad0 == CommittedTreeSentinel)
                    return;

                for (int i = 0; i < TotalNodeCount; i++)
                    TreeNodes[i] = default;

                root = default;
                root._pad0 = CommittedTreeSentinel;
                TreeNodes[RootIndex] = root;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct TombstonePruneJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> SourceRecords;
            [NoAlias]
            public NativeArray<byte> DestinationRecords;
            [NoAlias]
            public NativeArray<int> Counters;
            public int RecordStrideBytes;
            public int AliveFlagOffsetBytes;
            public uint AliveMask;
            public int RecordCount;

            public void Execute()
            {
                if (!SourceRecords.IsCreated ||
                    !DestinationRecords.IsCreated ||
                    !Counters.IsCreated ||
                    Counters.Length <= 0 ||
                    RecordStrideBytes <= 0 ||
                    AliveFlagOffsetBytes < 0 ||
                    AliveFlagOffsetBytes > RecordStrideBytes - sizeof(uint) ||
                    RecordCount <= 0)
                {
                    return;
                }

                byte* source = (byte*)SourceRecords.GetUnsafeReadOnlyPtr();
                byte* destination = (byte*)DestinationRecords.GetUnsafePtr();
                int writeRecord = 0;
                for (int i = 0; i < RecordCount; i++)
                {
                    int sourceOffset = i * RecordStrideBytes;
                    if (sourceOffset < 0 || sourceOffset > SourceRecords.Length - RecordStrideBytes)
                        break;

                    uint flags = *(uint*)(source + sourceOffset + AliveFlagOffsetBytes);
                    if ((flags & AliveMask) == 0u)
                        continue;

                    int destinationOffset = writeRecord * RecordStrideBytes;
                    if (destinationOffset > DestinationRecords.Length - RecordStrideBytes)
                        break;

                    UnsafeUtility.MemCpy(destination + destinationOffset, source + sourceOffset, RecordStrideBytes);
                    writeRecord++;
                }

                Counters[0] = writeRecord;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct Lz4SubBlockCompressionJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> Source;
            [NoAlias]
            public NativeArray<byte> Destination;
            [NoAlias]
            public NativeArray<Lz4SubBlockHeader> BlockHeaders;
            [NoAlias]
            public NativeArray<int> HashTable;
            [NoAlias]
            public NativeArray<int> Counters;
            public int SourceLength;
            public int SourceLengthCounterIndex;
            public int SubBlockBytes;

            public void Execute()
            {
                if (!Source.IsCreated ||
                    !Destination.IsCreated ||
                    !BlockHeaders.IsCreated ||
                    !HashTable.IsCreated ||
                    !Counters.IsCreated ||
                    Counters.Length <= CounterFailure)
                {
                    return;
                }

                int requestedSourceLength = SourceLength;
                if (requestedSourceLength < 0 &&
                    SourceLengthCounterIndex >= 0 &&
                    SourceLengthCounterIndex < Counters.Length)
                {
                    requestedSourceLength = Counters[SourceLengthCounterIndex];
                }

                int sourceLength = math.clamp(requestedSourceLength, 0, Source.Length);
                int blockSize = math.clamp(SubBlockBytes <= 0 ? DefaultSubBlockBytes : SubBlockBytes, 1024, MaxSubBlockBytes);
                Counters[CounterStoredBytes] = 0;
                Counters[CounterBlockCount] = 0;
                Counters[CounterRawBytes] = 0;
                Counters[CounterFailure] = 0;

                int write = 0;
                int blockIndex = 0;
                int rawTotal = 0;
                bool failed = false;
                byte* sourcePtr = (byte*)Source.GetUnsafeReadOnlyPtr();
                byte* destinationPtr = (byte*)Destination.GetUnsafePtr();
                int headerBytes = UnsafeUtility.SizeOf<Lz4SubBlockHeader>();

                for (int blockStart = 0; blockStart < sourceLength; blockStart += blockSize)
                {
                    int rawBytes = math.min(blockSize, sourceLength - blockStart);
                    rawTotal += rawBytes;
                    if (blockIndex >= BlockHeaders.Length || write > Destination.Length - headerBytes)
                    {
                        failed = true;
                        break;
                    }

                    int headerOffset = write;
                    int payloadOffset = headerOffset + headerBytes;
                    if (payloadOffset >= Destination.Length)
                    {
                        failed = true;
                        break;
                    }

                    int destinationCapacity = Destination.Length - payloadOffset;
                    int rleBytes = CompressRleBlock(blockStart, rawBytes, payloadOffset, destinationCapacity);
                    uint storageFlag = Lz4BlockFlagRaw;
                    int storedBytes = rawBytes;
                    bool useRaw = true;

                    if (rleBytes > 0 && rleBytes < rawBytes)
                    {
                        storedBytes = rleBytes;
                        storageFlag = Lz4BlockFlagRle;
                        useRaw = false;
                    }
                    else
                    {
                        ClearHashTable();
                        int compressedBytes = rawBytes >= SaveDeltaCompression.MinimumLz4PayloadBytes
                            ? CompressBlock(blockStart, rawBytes, payloadOffset, destinationCapacity)
                            : -1;

                        useRaw = compressedBytes <= 0 || compressedBytes >= rawBytes;
                        storedBytes = useRaw ? rawBytes : compressedBytes;
                        storageFlag = useRaw ? Lz4BlockFlagRaw : Lz4BlockFlagCompressed;
                    }

                    if (payloadOffset > Destination.Length - storedBytes)
                    {
                        failed = true;
                        break;
                    }

                    if (useRaw)
                        UnsafeUtility.MemCpy(destinationPtr + payloadOffset, sourcePtr + blockStart, rawBytes);

                    uint crc = ComputeCrc32(destinationPtr + payloadOffset, storedBytes);
                    Lz4SubBlockHeader header = new Lz4SubBlockHeader
                    {
                        Magic = Lz4BlockMagic,
                        Version = Lz4BlockVersion,
                        HeaderBytes = (ushort)headerBytes,
                        RawBytes = rawBytes,
                        StoredBytes = storedBytes,
                        SourceOffsetBytes = blockStart,
                        Crc32 = crc,
                        Flags = storageFlag,
                        _pad0 = 0u
                    };

                    BlockHeaders[blockIndex] = header;
                    WriteLz4SubBlockHeaderLittleEndian(destinationPtr + headerOffset, header);
                    write = Align16(payloadOffset + storedBytes);
                    if (write > Destination.Length)
                    {
                        failed = true;
                        break;
                    }

                    for (int pad = payloadOffset + storedBytes; pad < write; pad++)
                        Destination[pad] = 0;

                    blockIndex++;
                }

                Counters[CounterRawBytes] = rawTotal;
                if (failed)
                {
                    Counters[CounterStoredBytes] = 0;
                    Counters[CounterBlockCount] = 0;
                    Counters[CounterFailure] = 1;
                    return;
                }

                Counters[CounterStoredBytes] = write;
                Counters[CounterBlockCount] = blockIndex;
            }

            private void ClearHashTable()
            {
                for (int i = 0; i < HashTable.Length; i++)
                    HashTable[i] = -1;
            }

            private int CompressRleBlock(int sourceOffset, int sourceLength, int destinationOffset, int destinationCapacity)
            {
                int read = 0;
                int write = 0;
                while (read < sourceLength)
                {
                    byte value = Source[sourceOffset + read];
                    int run = 1;
                    while (read + run < sourceLength &&
                           run < ushort.MaxValue &&
                           Source[sourceOffset + read + run] == value)
                    {
                        run++;
                    }

                    if (destinationCapacity - write < 3)
                        return -1;

                    Destination[destinationOffset + write++] = value;
                    ushort run16 = (ushort)run;
                    Destination[destinationOffset + write++] = unchecked((byte)run16);
                    Destination[destinationOffset + write++] = unchecked((byte)(run16 >> 8));
                    read += run;
                }

                return write > 0 && write < sourceLength ? write : -1;
            }

            private int CompressBlock(int sourceOffset, int sourceLength, int destinationOffset, int destinationCapacity)
            {
                int anchor = 0;
                int read = 0;
                int write = 0;
                int lastMatchStart = math.max(0, sourceLength - 12);
                while (read <= lastMatchStart)
                {
                    uint sequence = ReadUInt32(sourceOffset + read);
                    int hash = ResolveLz4Hash(sequence, HashTable.Length);
                    int previous = HashTable[hash];
                    HashTable[hash] = read;

                    if (previous >= 0 &&
                        read - previous <= ushort.MaxValue &&
                        previous + 4 <= sourceLength &&
                        Equals4(sourceOffset + previous, sourceOffset + read))
                    {
                        int matchLength = 4;
                        while (read + matchLength < sourceLength &&
                               Source[sourceOffset + previous + matchLength] == Source[sourceOffset + read + matchLength])
                        {
                            matchLength++;
                        }

                        if (!WriteSequence(sourceOffset, destinationOffset, destinationCapacity, anchor, read, previous, matchLength, ref write))
                            return -1;

                        read += matchLength;
                        anchor = read;
                        continue;
                    }

                    read++;
                }

                if (!WriteLastLiterals(sourceOffset, destinationOffset, destinationCapacity, anchor, sourceLength - anchor, ref write) ||
                    write >= sourceLength)
                {
                    return -1;
                }

                return write;
            }

            private uint ReadUInt32(int offset)
            {
                return Source[offset] |
                       ((uint)Source[offset + 1] << 8) |
                       ((uint)Source[offset + 2] << 16) |
                       ((uint)Source[offset + 3] << 24);
            }

            private bool Equals4(int left, int right)
            {
                return Source[left] == Source[right] &&
                       Source[left + 1] == Source[right + 1] &&
                       Source[left + 2] == Source[right + 2] &&
                       Source[left + 3] == Source[right + 3];
            }

            private static int ResolveLz4Hash(uint sequence, int hashLength)
            {
                uint mixed = sequence * 2654435761u;
                return hashLength <= 1 ? 0 : (int)(mixed % (uint)hashLength);
            }

            private bool WriteSequence(
                int sourceOffset,
                int destinationOffset,
                int destinationCapacity,
                int anchor,
                int read,
                int previous,
                int matchLength,
                ref int write)
            {
                int literalLength = read - anchor;
                int tokenOffset = write++;
                if (tokenOffset >= destinationCapacity)
                    return false;

                byte token = (byte)(math.min(literalLength, 15) << 4);
                if (!WriteLengthExtension(destinationOffset, destinationCapacity, literalLength, ref write))
                    return false;

                if (!CopySource(sourceOffset + anchor, destinationOffset, destinationCapacity, literalLength, ref write))
                    return false;

                int offset = read - previous;
                if (write + 2 > destinationCapacity)
                    return false;

                Destination[destinationOffset + write++] = unchecked((byte)offset);
                Destination[destinationOffset + write++] = unchecked((byte)(offset >> 8));
                int matchCode = matchLength - 4;
                token |= (byte)math.min(matchCode, 15);
                Destination[destinationOffset + tokenOffset] = token;
                return WriteLengthExtension(destinationOffset, destinationCapacity, matchCode, ref write);
            }

            private bool WriteLastLiterals(
                int sourceOffset,
                int destinationOffset,
                int destinationCapacity,
                int start,
                int literalLength,
                ref int write)
            {
                int tokenOffset = write++;
                if (tokenOffset >= destinationCapacity)
                    return false;

                Destination[destinationOffset + tokenOffset] = (byte)(math.min(literalLength, 15) << 4);
                return WriteLengthExtension(destinationOffset, destinationCapacity, literalLength, ref write) &&
                       CopySource(sourceOffset + start, destinationOffset, destinationCapacity, literalLength, ref write);
            }

            private bool WriteLengthExtension(int destinationOffset, int destinationCapacity, int totalLength, ref int write)
            {
                if (totalLength < 15)
                    return true;

                int value = totalLength - 15;
                while (value >= 255)
                {
                    if (write >= destinationCapacity)
                        return false;

                    Destination[destinationOffset + write++] = 255;
                    value -= 255;
                }

                if (write >= destinationCapacity)
                    return false;

                Destination[destinationOffset + write++] = (byte)value;
                return true;
            }

            private bool CopySource(int sourceStart, int destinationOffset, int destinationCapacity, int length, ref int write)
            {
                if (length < 0 || write > destinationCapacity - length)
                    return false;

                for (int i = 0; i < length; i++)
                    Destination[destinationOffset + write++] = Source[sourceStart + i];

                return true;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct CosmeticDeltaPayloadPruneJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<byte> SourceDeltaBytes;
            [NoAlias]
            public NativeArray<byte> DestinationDeltaBytes;
            [NoAlias]
            public NativeArray<int> Counters;
            public int SourceLength;
            public int SourceLengthCounterIndex;
            public int DropThresholdBytes;

            public void Execute()
            {
                if (!SourceDeltaBytes.IsCreated ||
                    !DestinationDeltaBytes.IsCreated ||
                    !Counters.IsCreated ||
                    Counters.Length <= CounterDroppedCosmeticRecords)
                {
                    return;
                }

                int requestedSourceLength = SourceLength;
                if (requestedSourceLength < 0 &&
                    SourceLengthCounterIndex >= 0 &&
                    SourceLengthCounterIndex < Counters.Length)
                {
                    requestedSourceLength = Counters[SourceLengthCounterIndex];
                }

                int sourceLength = math.clamp(requestedSourceLength, 0, SourceDeltaBytes.Length);
                int thresholdBytes = math.max(0, DropThresholdBytes);
                Counters[CounterRecords] = 0;
                Counters[CounterBytes] = 0;
                Counters[CounterChangedLeaves] = 0;
                Counters[CounterFlags] = 0;
                Counters[CounterDroppedCosmeticBytes] = 0;
                Counters[CounterDroppedCosmeticRecords] = 0;

                byte* sourcePtr = (byte*)SourceDeltaBytes.GetUnsafeReadOnlyPtr();
                byte* destinationPtr = (byte*)DestinationDeltaBytes.GetUnsafePtr();
                if (sourceLength <= 0)
                    return;

                int headerBytes = UnsafeUtility.SizeOf<StateDeltaRecordDTO>();
                int read = 0;
                int write = 0;
                int keptRecords = 0;
                int changedRecords = 0;
                int droppedRecords = 0;
                int droppedBytes = 0;
                uint flags = 0u;
                bool shouldDropCosmetics = thresholdBytes > 0 && sourceLength > thresholdBytes;

                while (read < sourceLength)
                {
                    if (sourceLength - read < headerBytes)
                    {
                        flags |= DeltaFlagOverflow;
                        break;
                    }

                    StateDeltaRecordDTO record = ReadStateDeltaRecordLittleEndian(sourcePtr + read);
                    int payloadStart = record.DeltaPayloadOffset;
                    int payloadLength = record.DataLength;
                    if (payloadLength < 0 ||
                        payloadStart < read + headerBytes ||
                        payloadStart > sourceLength - payloadLength)
                    {
                        flags |= DeltaFlagOverflow;
                        break;
                    }

                    int headerGapBytes = payloadStart - read;
                    int recordBytes = headerGapBytes + payloadLength;
                    if (recordBytes < headerBytes || read > sourceLength - recordBytes)
                    {
                        flags |= DeltaFlagOverflow;
                        break;
                    }

                    changedRecords++;
                    bool isCosmetic = (record.Flags & LeafFlagCosmetic) != 0u;
                    if (shouldDropCosmetics && isCosmetic)
                    {
                        droppedRecords++;
                        droppedBytes += recordBytes;
                        read += recordBytes;
                        continue;
                    }

                    if (write > DestinationDeltaBytes.Length - recordBytes)
                    {
                        flags |= DeltaFlagOverflow;
                        break;
                    }

                    int newPayloadStart = write + headerGapBytes;
                    record.DeltaPayloadOffset = newPayloadStart;
                    record.CompressedOffset = -1;
                    WriteStateDeltaRecordLittleEndian(destinationPtr + write, record);
                    if (headerGapBytes > headerBytes)
                    {
                        UnsafeUtility.MemMove(
                            destinationPtr + write + headerBytes,
                            sourcePtr + read + headerBytes,
                            headerGapBytes - headerBytes);
                    }

                    UnsafeUtility.MemMove(
                        destinationPtr + newPayloadStart,
                        sourcePtr + payloadStart,
                        payloadLength);

                    write += recordBytes;
                    keptRecords++;
                    read += recordBytes;
                }

                Counters[CounterRecords] = keptRecords;
                Counters[CounterBytes] = write;
                Counters[CounterChangedLeaves] = changedRecords;
                Counters[CounterFlags] = unchecked((int)flags);
                Counters[CounterDroppedCosmeticBytes] = droppedBytes;
                Counters[CounterDroppedCosmeticRecords] = droppedRecords;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct SaveMerkleTelemetryWriteJob : IJob
        {
            [NoAlias]
            public NativeArray<SaveMerkleTelemetryEntry> TelemetryRing;
            [NoAlias]
            public NativeArray<int> Cursor;
            public uint Frame;
            public int TotalBytesHashed;
            public int DeltaBytesGenerated;
            public float TreeComputeTimeMs;
            public uint ChangedLeaves;
            public MerkleNodeDTO RootNode;
            public uint WalBytesWritten;
            public uint CrcFailures;
            public uint IoFailures;

            public void Execute()
            {
                if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0 || !Cursor.IsCreated || Cursor.Length <= 0)
                    return;

                int writeIndex = math.abs(Cursor[0]) % TelemetryRing.Length;
                uint flags = 0u;
                if (TreeComputeTimeMs > 1f)
                    flags |= TelemetryFlagHashOverBudget;
                if (IoFailures > 0u)
                    flags |= TelemetryFlagIoException;
                if (CrcFailures > 0u)
                    flags |= TelemetryFlagCrcFailure;

                TelemetryRing[writeIndex] = new SaveMerkleTelemetryEntry
                {
                    Frame = Frame,
                    Flags = flags,
                    TotalBytesHashed = TotalBytesHashed,
                    DeltaBytesGenerated = DeltaBytesGenerated,
                    TreeComputeTimeMs = TreeComputeTimeMs,
                    ChangedLeaves = ChangedLeaves,
                    RootHashLo = RootNode.HashLo,
                    RootHashHi = RootNode.HashHi,
                    WalBytesWritten = WalBytesWritten,
                    CrcFailures = CrcFailures,
                    IoFailures = IoFailures,
                    _pad0 = 0u,
                    _pad1 = 0UL
                };

                Cursor[0] = writeIndex + 1;
            }
        }
    
        #region JulesLink_SaveMerkleHashNodeCalculator
        private static void JulesLink_SaveMerkleHashNodeCalculator() { _ = typeof(Hecton8.PureLogic.Systems.SaveMerkleHashNodeCalculator); }
        #endregion
}

    #if UNITY_EDITOR
    internal static unsafe class SaveMerkleCsvOverrideParser
    {
        private const uint SubBlockSizeHash = 0x77168845u;
        private const uint WalBytesPerSecondHash = 0x7E9BC934u;
        private const uint MathLodHash = 0xAFF90B1Fu;
        private const uint DropCosmeticThresholdHash = 0xA685836Au;

        internal static bool TryApplyFile(
            string csvPath,
            NativeArray<byte> scratchBytes,
            ref long observedWriteTicks,
            ref SaveMerkleRuntimeConfig config,
            out int appliedCount,
            out string error)
        {
            appliedCount = 0;
            error = string.Empty;
            if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
                return true;

            try
            {
                long writeTicks = File.GetLastWriteTimeUtc(csvPath).Ticks;
                if (writeTicks == observedWriteTicks)
                    return true;

                if (!scratchBytes.IsCreated)
                {
                    error = "CSV override scratch buffer is not created.";
                    return false;
                }

                using FileStream stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                if (stream.Length > scratchBytes.Length)
                {
                    error = "CSV override file exceeds native scratch capacity.";
                    return false;
                }

                byte* destination = (byte*)scratchBytes.GetUnsafePtr();
                int total = 0;
                while (total < stream.Length)
                {
                    int read = stream.Read(new Span<byte>(destination + total, (int)stream.Length - total));
                    if (read <= 0)
                        break;

                    total += read;
                }

                bool applied = TryApply(destination, total, ref config, out appliedCount);
                observedWriteTicks = writeTicks;
                return applied || appliedCount == 0;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        internal static bool TryApply(byte* data, int byteCount, ref SaveMerkleRuntimeConfig config, out int appliedCount)
        {
            appliedCount = 0;
            if (data == null || byteCount <= 0)
                return false;

            int cursor = 0;
            while (cursor < byteCount)
            {
                int lineStart = cursor;
                while (cursor < byteCount && data[cursor] != (byte)'\n' && data[cursor] != (byte)'\r')
                    cursor++;

                ParseLine(data, lineStart, cursor - lineStart, ref config, ref appliedCount);
                while (cursor < byteCount && (data[cursor] == (byte)'\n' || data[cursor] == (byte)'\r'))
                    cursor++;
            }

            return appliedCount > 0;
        }

        private static void ParseLine(byte* data, int start, int length, ref SaveMerkleRuntimeConfig config, ref int appliedCount)
        {
            if (length <= 0 || data[start] == (byte)'#')
                return;

            int comma = -1;
            for (int i = 0; i < length; i++)
            {
                if (data[start + i] == (byte)',')
                {
                    comma = i;
                    break;
                }
            }

            if (comma <= 0)
                return;

            int keyStart = TrimStart(data, start, comma);
            int keyEnd = TrimEnd(data, keyStart, start + comma);
            int valueStart = TrimStart(data, start + comma + 1, length - comma - 1);
            int valueEnd = TrimEnd(data, valueStart, start + length);
            if (keyEnd <= keyStart || valueEnd <= valueStart)
                return;

            uint keyHash = HashAsciiLower(data + keyStart, keyEnd - keyStart);
            if (!TryParseInt(data + valueStart, valueEnd - valueStart, out int value))
                return;

            switch (keyHash)
            {
                case SubBlockSizeHash:
                    config.SubBlockBytes = math.clamp(value, 1024, SaveStateMerkleTree.MaxSubBlockBytes);
                    appliedCount++;
                    break;
                case WalBytesPerSecondHash:
                    config.WalBytesPerSecond = math.max(1024, value);
                    appliedCount++;
                    break;
                case MathLodHash:
                    config.MathLod = math.clamp(value, 0, 3);
                    appliedCount++;
                    break;
                case DropCosmeticThresholdHash:
                    config.CosmeticDropThresholdBytes = math.max(0, value);
                    appliedCount++;
                    break;
            }
        }

        private static int TrimStart(byte* data, int start, int length)
        {
            int cursor = start;
            int end = start + length;
            while (cursor < end && IsWhitespace(data[cursor]))
                cursor++;
            return cursor;
        }

        private static int TrimEnd(byte* data, int start, int end)
        {
            int cursor = end;
            while (cursor > start && IsWhitespace(data[cursor - 1]))
                cursor--;
            return cursor;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static uint HashAsciiLower(byte* data, int length)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < length; i++)
            {
                byte value = data[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                hash = (hash ^ value) * 16777619u;
            }

            return hash;
        }

        private static bool TryParseInt(byte* data, int length, out int value)
        {
            value = 0;
            if (length <= 0)
                return false;

            int cursor = 0;
            int sign = 1;
            if (data[0] == (byte)'-')
            {
                sign = -1;
                cursor = 1;
            }

            int parsed = 0;
            bool any = false;
            for (; cursor < length; cursor++)
            {
                byte ch = data[cursor];
                if (ch < (byte)'0' || ch > (byte)'9')
                    return false;

                parsed = (parsed * 10) + (ch - (byte)'0');
                any = true;
            }

            value = parsed * sign;
            return any;
        }
    }
    #endif
}
