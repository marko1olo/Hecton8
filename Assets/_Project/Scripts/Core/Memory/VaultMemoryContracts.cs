using System;
using System.Runtime.CompilerServices;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Runtime memory layout profile imported from legacy binary files, CSV overrides, or mock fallback. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VaultMemoryLayoutConfig
    {
        [FieldOffset(0)] public long ArenaLimitBytes;
        [FieldOffset(8)] public int BufferCapacity;
        [FieldOffset(12)] public int HotEntityCapacity;
        [FieldOffset(16)] public int ColdEntityCapacity;
        [FieldOffset(20)] public int BucketCapacity;
        [FieldOffset(24)] public uint SourceHash;
        [FieldOffset(28)] public uint Version;
        [FieldOffset(32)] public byte ScalabilityProfile;
        [FieldOffset(33)] public byte Flags;
        [FieldOffset(34)] private ushort _pad0;
        [FieldOffset(36)] public float StrideAggressiveness;
        [FieldOffset(40)] private ulong _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;
    }

    /// <summary>
    /// Absolute universe position with 64-bit authority. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct VaultAup64
    {
        [FieldOffset(0)] public long SectorX;
        [FieldOffset(8)] public long SectorY;
        [FieldOffset(16)] public long SectorZ;
        [FieldOffset(24)] public double LocalX;
        [FieldOffset(32)] public double LocalY;
        [FieldOffset(40)] public double LocalZ;
    }

    /// <summary>
    /// Rollback-friendly AUP authority split into 64-bit sectors and 32-bit local offsets. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VaultAupSectorLocal32
    {
        [FieldOffset(0)] public long SectorX;
        [FieldOffset(8)] public long SectorY;
        [FieldOffset(16)] public long SectorZ;
        [FieldOffset(24)] public float3 LocalOffset;
        [FieldOffset(36)] public uint EntityId;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint ShiftFrameId;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    /// <summary>
    /// Hot per-frame entity stream. No display/lore data. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VaultHotEntityData
    {
        [FieldOffset(0)] public float4 Rotation;
        [FieldOffset(16)] public float3 LocalPosition;
        [FieldOffset(28)] public float3 Velocity;
        [FieldOffset(40)] public uint EntityId;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint ShiftFrameId;
        [FieldOffset(52)] public byte SimulationBucket;
        [FieldOffset(53)] public byte LodTier;
        [FieldOffset(54)] private ushort _pad0;
        [FieldOffset(56)] private uint _pad1;
        [FieldOffset(60)] private uint _pad2;
    }

    /// <summary>
    /// Cold entity metadata stream. Read outside tight simulation loops. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VaultColdEntityData
    {
        [FieldOffset(0)] public ulong DisplayNameHash;
        [FieldOffset(8)] public ulong FactionMask;
        [FieldOffset(16)] public uint EntityId;
        [FieldOffset(20)] public uint ArchetypeHash;
        [FieldOffset(24)] public uint PrefabHash;
        [FieldOffset(28)] public int MaxHealth;
        [FieldOffset(32)] public int MaxEnergy;
        [FieldOffset(36)] public ushort Flags;
        [FieldOffset(38)] public ushort MaterialSet;
        [FieldOffset(40)] private uint _pad0;
        [FieldOffset(44)] private uint _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    /// <summary>
    /// Descriptor record for static transform matrices using the Dear Lie protocol. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VaultTransformAlias
    {
        [FieldOffset(0)] public uint MatrixBufferId;
        [FieldOffset(4)] public uint MatrixOffsetBytes;
        [FieldOffset(8)] public uint MatrixGeneration;
        [FieldOffset(12)] public uint TransformHash;
        [FieldOffset(16)] public uint EntityId;
        [FieldOffset(20)] public byte Flags;
        [FieldOffset(21)] private byte _pad0;
        [FieldOffset(22)] private ushort _pad1;
        [FieldOffset(24)] private ulong _pad3;
    }

    /// <summary>
    /// SHINOBU_100 vault sovereignty heartbeat. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VaultSovereigntyTelemetryEntry
    {
        [FieldOffset(0)] public long TotalVaultBytes;
        [FieldOffset(8)] public long ArenaBytes;
        [FieldOffset(16)] public int ActiveBufferCount;
        [FieldOffset(20)] public int GenerationMisses;
        [FieldOffset(24)] public int StrideMultiplier;
        [FieldOffset(28)] public float MaxMemoryJobUs;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint VaultGenerationId;
        [FieldOffset(40)] public uint BufferId;
        [FieldOffset(44)] public uint StateHash;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] private ulong _pad0;
    }

    /// <summary>
    /// Memory-local relocation record consumed by the Core dispatcher. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VaultMemoryAddressShiftRecord
    {
        public const byte FlagMemMove = 1 << 0;
        public const byte FlagFenceProtected = 1 << 1;
        public const byte FlagSwapPopIndexMove = 1 << 2;

        [FieldOffset(0)] public long OldOffsetBytes;
        [FieldOffset(8)] public long NewOffsetBytes;
        [FieldOffset(16)] public int BufferId;
        [FieldOffset(20)] public int ByteLength;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(28)] public byte Flags;
        [FieldOffset(29)] public byte SystemId;
        [FieldOffset(30)] private ushort _pad0;
        [FieldOffset(32)] public int OldIndex;
        [FieldOffset(36)] public int NewIndex;
        [FieldOffset(40)] public uint MovedEntityId;
        [FieldOffset(44)] public uint SourceFrame;
        [FieldOffset(48)] public uint SourceHash;
        [FieldOffset(52)] public uint CompactedCount;
        [FieldOffset(56)] private ulong _pad1;
    }

    public static class VaultSovereigntyTelemetry
    {
        public const int Capacity = 300;
        public const uint FaultFlag = 1u;
        public const uint PhysicsSourceHash = 0x53483130u; // SH10
        private const ulong DumpMagic = 0x3030315F55424F53UL; // SOBU_100 low-endian marker
        private const int DumpVersion = 1;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_100.bin";

        private static VaultGenerationHandle<VaultSovereigntyTelemetryEntry> _ringHandle;
        private static IDataVault _ringVault;
        private static int _cursor;

        public static bool TryRecord(
            IDataVault vault,
            uint frame,
            int generationMisses,
            int strideMultiplier,
            float maxMemoryJobUs,
            float globalQualityWeight,
            uint sourceHash,
            uint flags)
        {
            if (vault == null)
                return false;

            if (!EnsureRing(vault))
                return false;

            if (!TryResolveRing(vault, out NativeArray<VaultSovereigntyTelemetryEntry> ring) ||
                ring.Length == 0)
            {
                return false;
            }

            int cursor = _cursor;
            if ((uint)cursor >= (uint)ring.Length)
                cursor = 0;

            VaultSovereigntyTelemetryEntry entry = default;
            entry.TotalVaultBytes = vault.AllocatedBytes;
            entry.ArenaBytes = vault.ArenaBytes;
            entry.ActiveBufferCount = vault.MemoryBlockSnapshotCount;
            entry.GenerationMisses = math.max(0, generationMisses);
            entry.StrideMultiplier = math.clamp(strideMultiplier, 1, 16);
            entry.MaxMemoryJobUs = math.max(0f, math.isfinite(maxMemoryJobUs) ? maxMemoryJobUs : 0f);
            entry.Frame = frame;
            entry.VaultGenerationId = vault.VaultGenerationID;
            entry.BufferId = (uint)BufferID.VaultSovereigntyTelemetryRing;
            entry.StateHash = HashTelemetry(entry.TotalVaultBytes, entry.ArenaBytes, sourceHash, frame);
            entry.GlobalQualityWeight = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            entry.Flags = flags;
            ring[cursor] = entry;

            cursor++;
            if (cursor >= ring.Length)
                cursor = 0;
            _cursor = cursor;
            return true;
        }

        public static bool EnsureRing(IDataVault vault)
        {
            if (vault == null)
                return false;
            if (!ReferenceEquals(_ringVault, vault))
            {
                _ringHandle = default;
                _cursor = 0;
                _ringVault = vault;
            }

            if (TryResolveRing(vault, out NativeArray<VaultSovereigntyTelemetryEntry> existingRing) &&
                existingRing.Length >= Capacity)
            {
                return true;
            }
            if (vault.IsAllocationLocked)
                return false;

            _ringHandle = vault.EnsureGenerationHandle<VaultSovereigntyTelemetryEntry>(
                BufferID.VaultSovereigntyTelemetryRing,
                Capacity,
                SystemID.CoreDataVault,
                NativeArrayOptions.ClearMemory);
            return TryResolveRing(vault, out NativeArray<VaultSovereigntyTelemetryEntry> ring) &&
                   ring.Length >= Capacity;
        }

        private static bool TryResolveRing(IDataVault vault, out NativeArray<VaultSovereigntyTelemetryEntry> ring)
        {
            ring = default;
            return vault != null &&
                   _ringHandle.BufferID != 0u &&
                   vault.TryResolveHandle(in _ringHandle, out ring) &&
                   ring.IsCreated;
        }

        private static bool TryReadRing(IDataVault vault, out NativeArray<VaultSovereigntyTelemetryEntry>.ReadOnly ring)
        {
            ring = default;
            if (vault == null)
                return false;

            VaultGenerationHandle<VaultSovereigntyTelemetryEntry> handle = _ringHandle;
            if (handle.BufferID == 0u &&
                !vault.TryGetGenerationHandle(BufferID.VaultSovereigntyTelemetryRing, out handle))
            {
                return false;
            }

            return handle.BufferID != 0u &&
                   vault.TryReadOnlyHandle(in handle, out ring) &&
                   ring.IsCreated;
        }

        public static bool TryDump(IDataVault vault, string projectRoot)
        {
            if (!TryReadRing(vault, out NativeArray<VaultSovereigntyTelemetryEntry>.ReadOnly ring) ||
                ring.Length == 0 ||
                string.IsNullOrEmpty(projectRoot))
            {
                return false;
            }

            string path = Path.Combine(projectRoot, DumpRelativePath);
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    writer.Write(DumpMagic);
                    writer.Write(DumpVersion);
                    writer.Write(ring.Length);
                    writer.Write(UnsafeUtility.SizeOf<VaultSovereigntyTelemetryEntry>());
                    int start = _cursor;
                    for (int i = 0; i < ring.Length; i++)
                    {
                        int index = start + i;
                        if (index >= ring.Length)
                            index -= ring.Length;

                        VaultSovereigntyTelemetryEntry entry = ring[index];
                        writer.Write(entry.TotalVaultBytes);
                        writer.Write(entry.ArenaBytes);
                        writer.Write(entry.ActiveBufferCount);
                        writer.Write(entry.GenerationMisses);
                        writer.Write(entry.StrideMultiplier);
                        writer.Write(entry.MaxMemoryJobUs);
                        writer.Write(entry.Frame);
                        writer.Write(entry.VaultGenerationId);
                        writer.Write(entry.BufferId);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.GlobalQualityWeight);
                        writer.Write(entry.Flags);
                        writer.Write(0UL);
                    }
                }

                return true;
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

        private static uint HashTelemetry(long totalBytes, long arenaBytes, uint sourceHash, uint frame)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)totalBytes) * 16777619u;
            hash = (hash ^ (uint)(totalBytes >> 32)) * 16777619u;
            hash = (hash ^ (uint)arenaBytes) * 16777619u;
            hash = (hash ^ (uint)(arenaBytes >> 32)) * 16777619u;
            hash = (hash ^ sourceHash) * 16777619u;
            hash = (hash ^ frame) * 16777619u;
            return hash;
        }
    }

    /// <summary>
    /// Immutable byte-size and alignment contract for vault-owned buffers. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public readonly struct VaultBufferContract
    {
        public const int LayoutConfigSizeBytes = 64;
        public const int Aup64SizeBytes = 48;
        public const int AupSectorLocal32SizeBytes = 64;
        public const int HotEntitySizeBytes = 64;
        public const int ColdEntitySizeBytes = 64;
        public const int TransformAliasSizeBytes = 32;
        public const int AddressShiftRecordSizeBytes = 64;
        public const int RequiredAlignmentBytes = 8;
        public const int CacheLineBytes = 64;
        public const double AupSectorSizeMeters = HectonPhysicsContract.AupSectorSizeMetersDouble;
        public const int LayoutConfigBufferId = (int)BufferID.VaultMemoryLayoutConfig;
        public const int HotEntityBufferId = (int)BufferID.VaultHotEntityData;
        public const int ColdEntityBufferId = (int)BufferID.VaultColdEntityData;
        public const int Aup64BufferId = (int)BufferID.VaultAup64;
        public const int AupSectorLocal32BufferId = (int)BufferID.VaultAupSectorLocal32;
        public const int EntityBucketMapBufferId = (int)BufferID.VaultEntityBucketMap;
        public const int SharedTransformMatricesBufferId = (int)BufferID.VaultSharedTransformMatrices;
        public const int TelemetryRingBufferId = (int)BufferID.VaultSovereigntyTelemetryRing;
        public const int AcousticEchoPendingTapsBufferId = (int)BufferID.AcousticEchoPendingTaps;
        public const int CsvScratchBufferId = (int)BufferID.VaultMemoryProfileCsvScratch;
        public const int ActiveEntityCountBufferId = (int)BufferID.VaultSovereigntyActiveEntityCount;
        public const int AddressShiftRecordsBufferId = (int)BufferID.VaultMemoryAddressShiftRecords;
        public const int AddressShiftCountBufferId = (int)BufferID.VaultMemoryAddressShiftCount;
        public const int OwnedBufferCount = 16;
        public const int MinBufferId = LayoutConfigBufferId;
        // SHINOBU owns 550-559 plus 636-641. WristHud/Flora own the intervening enum values.
        public const int MaxBufferId = AddressShiftCountBufferId;

        public const int LayoutConfigArenaLimitOffset = 0;
        public const int LayoutConfigBufferCapacityOffset = 8;
        public const int LayoutConfigHotEntityCapacityOffset = 12;
        public const int LayoutConfigColdEntityCapacityOffset = 16;
        public const int LayoutConfigBucketCapacityOffset = 20;
        public const int LayoutConfigSourceHashOffset = 24;
        public const int LayoutConfigVersionOffset = 28;
        public const int LayoutConfigScalabilityProfileOffset = 32;
        public const int LayoutConfigFlagsOffset = 33;
        public const int LayoutConfigStrideAggressivenessOffset = 36;

        public const int AupSectorXOffset = 0;
        public const int AupSectorYOffset = 8;
        public const int AupSectorZOffset = 16;
        public const int AupLocalXOffset = 24;
        public const int AupLocalYOffset = 32;
        public const int AupLocalZOffset = 40;
        public const int Aup32SectorXOffset = 0;
        public const int Aup32SectorYOffset = 8;
        public const int Aup32SectorZOffset = 16;
        public const int Aup32LocalOffset = 24;
        public const int Aup32EntityIdOffset = 36;
        public const int Aup32FlagsOffset = 40;
        public const int Aup32ShiftFrameIdOffset = 44;

        public const int HotRotationOffset = 0;
        public const int HotLocalPositionOffset = 16;
        public const int HotVelocityOffset = 28;
        public const int HotEntityIdOffset = 40;
        public const int HotFlagsOffset = 44;
        public const int HotShiftFrameIdOffset = 48;
        public const int HotSimulationBucketOffset = 52;
        public const int HotLodTierOffset = 53;

        public const int ColdDisplayNameHashOffset = 0;
        public const int ColdFactionMaskOffset = 8;
        public const int ColdEntityIdOffset = 16;
        public const int ColdArchetypeHashOffset = 20;
        public const int ColdPrefabHashOffset = 24;
        public const int ColdMaxHealthOffset = 28;
        public const int ColdMaxEnergyOffset = 32;
        public const int ColdFlagsOffset = 36;
        public const int ColdMaterialSetOffset = 38;

        public const int TransformAliasMatrixBufferIdOffset = 0;
        public const int TransformAliasMatrixOffsetBytesOffset = 4;
        public const int TransformAliasMatrixGenerationOffset = 8;
        public const int TransformAliasTransformHashOffset = 12;
        public const int TransformAliasEntityIdOffset = 16;
        public const int TransformAliasFlagsOffset = 20;

        [FieldOffset(0)] public readonly int LayoutConfigSize;
        [FieldOffset(4)] public readonly int Aup64Size;
        [FieldOffset(8)] public readonly int AupSectorLocal32Size;
        [FieldOffset(12)] public readonly int HotEntitySize;
        [FieldOffset(16)] public readonly int ColdEntitySize;
        [FieldOffset(20)] public readonly int TransformAliasSize;
        [FieldOffset(24)] public readonly int RequiredAlignment;
        [FieldOffset(28)] public readonly int CacheLineSize;
        [FieldOffset(32)] public readonly int MinEnumValue;
        [FieldOffset(36)] public readonly int MaxEnumValue;
        [FieldOffset(40)] private readonly uint _pad0;
        [FieldOffset(44)] private readonly uint _pad1;
        [FieldOffset(48)] private readonly ulong _pad2;
        [FieldOffset(56)] private readonly ulong _pad3;

        /// <summary>Creates the compile-time layout contract instance.</summary>
        public VaultBufferContract(byte _)
        {
            LayoutConfigSize = LayoutConfigSizeBytes;
            Aup64Size = Aup64SizeBytes;
            AupSectorLocal32Size = AupSectorLocal32SizeBytes;
            HotEntitySize = HotEntitySizeBytes;
            ColdEntitySize = ColdEntitySizeBytes;
            TransformAliasSize = TransformAliasSizeBytes;
            RequiredAlignment = RequiredAlignmentBytes;
            CacheLineSize = CacheLineBytes;
            MinEnumValue = MinBufferId;
            MaxEnumValue = MaxBufferId;
            _pad0 = 0u;
            _pad1 = 0u;
            _pad2 = 0UL;
            _pad3 = 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool OwnsBufferId(BufferID bufferId)
        {
            switch (bufferId)
            {
                case BufferID.VaultMemoryLayoutConfig:
                case BufferID.VaultHotEntityData:
                case BufferID.VaultColdEntityData:
                case BufferID.VaultAup64:
                case BufferID.VaultEntityBucketMap:
                case BufferID.VaultSharedTransformMatrices:
                case BufferID.VehicleMotorSubmarineStates:
                case BufferID.VaultSovereigntyTelemetryRing:
                case BufferID.AcousticEchoPendingTaps:
                case BufferID.VaultAupSectorLocal32:
                case BufferID.VaultSovereigntyActiveEntityCount:
                case BufferID.VaultMemoryProfileCsvScratch:
                case BufferID.VaultMemoryAddressShiftRecords:
                case BufferID.VaultMemoryAddressShiftCount:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Allocation-free math helpers for vault memory records.
    /// </summary>
    public static class VaultMemoryMath
    {
        public const double AupSectorSizeMeters = HectonPhysicsContract.AupSectorSizeMetersDouble;
        private const double MaxLocalFloatDowncastMeters = HectonPhysicsContract.AupMaxFloatSafeMeters;

        /// <summary>Builds a 64-bucket simulation id from AUP sector and local coordinates.</summary>
        public static byte ResolveSimulationBucket(in VaultAup64 aup)
        {
            ulong hash = 1469598103934665603UL;
            hash = Mix(hash, unchecked((ulong)aup.SectorX));
            hash = Mix(hash, unchecked((ulong)aup.SectorY));
            hash = Mix(hash, unchecked((ulong)aup.SectorZ));
            hash = Mix(hash, unchecked((ulong)math.aslong((double)aup.LocalX)));
            hash = Mix(hash, unchecked((ulong)math.aslong((double)aup.LocalY)));
            hash = Mix(hash, unchecked((ulong)math.aslong((double)aup.LocalZ)));
            return (byte)(hash & 63UL);
        }

        /// <summary>Builds a safe mock config aligned to cache and ARM64 rules.</summary>
        public static VaultMemoryLayoutConfig BuildMockConfig(byte scalabilityProfile)
        {
            VaultMemoryLayoutConfig config = default;
            config.ArenaLimitBytes = GlobalDataVault.ResolveArenaCapacityLimit(scalabilityProfile);
            config.BufferCapacity = 512;
            config.HotEntityCapacity = 1024;
            config.ColdEntityCapacity = 1024;
            config.BucketCapacity = 64;
            config.SourceHash = 0x4D4F434Bu; // MOCK
            config.Version = 1u;
            config.ScalabilityProfile = scalabilityProfile;
            config.Flags = 1;
            float profile01 = GlobalDataVault.DecodeScalabilityProfile01(scalabilityProfile);
            float curve01 = profile01 * profile01 * (3f - (2f * profile01));
            config.StrideAggressiveness = math.lerp(0.75f, 0.25f, curve01);
            return config;
        }

        /// <summary>Resolves camera-relative meters from sector/local AUP without casting absolute coordinates to float.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ResolveCameraRelativeLocal(in VaultAup64 entity, in VaultAup64 camera)
        {
            double3 delta = ResolveCameraRelativeDeltaMeters(in entity, in camera);
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            double x = math.clamp(delta.x, -MaxLocalFloatDowncastMeters, MaxLocalFloatDowncastMeters);
            double y = math.clamp(delta.y, -MaxLocalFloatDowncastMeters, MaxLocalFloatDowncastMeters);
            double z = math.clamp(delta.z, -MaxLocalFloatDowncastMeters, MaxLocalFloatDowncastMeters);
            float3 local = new float3((float)x, (float)y, (float)z);
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }

        /// <summary>Resolves camera-relative double-precision meters from sector/local AUP authority.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 ResolveCameraRelativeDeltaMeters(in VaultAup64 entity, in VaultAup64 camera)
        {
            return new double3(
                ResolveAxisDeltaMeters(entity.SectorX, camera.SectorX, entity.LocalX, camera.LocalX),
                ResolveAxisDeltaMeters(entity.SectorY, camera.SectorY, entity.LocalY, camera.LocalY),
                ResolveAxisDeltaMeters(entity.SectorZ, camera.SectorZ, entity.LocalZ, camera.LocalZ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ResolveAxisDeltaMeters(long entitySector, long cameraSector, double entityLocal, double cameraLocal)
        {
            double sectorDelta = (double)entitySector - cameraSector;
            return (sectorDelta * AupSectorSizeMeters) + (entityLocal - cameraLocal);
        }

        private static ulong Mix(ulong hash, ulong value)
        {
            hash ^= value;
            return hash * 1099511628211UL;
        }
    }

    /// <summary>
    /// Converts 64-bit AUP authority to hot local float positions for downstream SIMD jobs.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct VaultAupLocalOffsetResolverJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<VaultAup64> EntityAups;
        [NoAlias] public NativeArray<VaultHotEntityData> HotEntities;
        public VaultAup64 CameraAup;
        public uint ShiftFrameId;

        /// <inheritdoc />
        public void Execute(int index)
        {
            if ((uint)index >= (uint)EntityAups.Length || (uint)index >= (uint)HotEntities.Length)
                return;

            void* aupPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(EntityAups);
            void* hotPtr = NativeArrayUnsafeUtility.GetUnsafePtr(HotEntities);
            ref readonly VaultAup64 entity = ref UnsafeUtility.AsRef<VaultAup64>(
                (byte*)aupPtr + (index * UnsafeUtility.SizeOf<VaultAup64>()));
            ref VaultHotEntityData hot = ref UnsafeUtility.AsRef<VaultHotEntityData>(
                (byte*)hotPtr + (index * UnsafeUtility.SizeOf<VaultHotEntityData>()));
            float3 local = VaultMemoryMath.ResolveCameraRelativeLocal(in entity, in CameraAup);

            hot.LocalPosition = local;
            hot.ShiftFrameId = ShiftFrameId;
            hot.SimulationBucket = VaultMemoryMath.ResolveSimulationBucket(in entity);
        }
    }

    /// <summary>
    /// FrostTick maintenance result for SHINOBU_100 memory sovereignty. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VaultSovereigntyMaintenanceStats
    {
        [FieldOffset(0)] public int AupRowsVisited;
        [FieldOffset(4)] public int SweepRowsVisited;
        [FieldOffset(8)] public int ActiveCount;
        [FieldOffset(12)] public int ScanBudget;
        [FieldOffset(16)] public float MaxJobUs;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private ulong _pad0;
    }

    /// <summary>
    /// Core PRE_SIMULATION FrostTick maintenance for AUP sector wrapping and O(1) swap-pop compaction.
    /// </summary>
    public static class VaultSovereigntyMaintenance
    {
        public const uint SourceHash = 0x53483130u; // SH10
        public const int DefaultHotEntityCapacity = 1024;
        private const int MinimumSweepRows = 64;
        private const uint FlagAupWrapped = 1u << 0;
        private const uint FlagSweepExecuted = 1u << 1;
        private const uint FlagCompleted = 1u << 2;

        public static bool PrewarmBuffers(IDataVault vault, int hotEntityCapacity)
        {
            if (vault == null || vault.IsAllocationLocked)
                return false;

            int capacity = ResolvePrewarmCapacity(hotEntityCapacity);
            bool ok = VaultSovereigntyTelemetry.EnsureRing(vault);
            bool hasActiveCount = TryEnsureCoreVaultBuffer(
                vault,
                BufferID.VaultSovereigntyActiveEntityCount,
                1,
                NativeArrayOptions.ClearMemory,
                out NativeArray<int> activeCount);
            bool hasShiftCount = TryEnsureCoreVaultBuffer(
                vault,
                BufferID.VaultMemoryAddressShiftCount,
                1,
                NativeArrayOptions.ClearMemory,
                out NativeArray<int> shiftCount);
            bool hasShiftRecords = TryEnsureCoreVaultBuffer(
                vault,
                BufferID.VaultMemoryAddressShiftRecords,
                capacity,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<VaultMemoryAddressShiftRecord> shiftRecords);
            bool hasCsvScratch = TryEnsureCoreVaultBuffer(
                vault,
                BufferID.VaultMemoryProfileCsvScratch,
                VaultLegacyBinaryArchaeology.CsvScratchBytes,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<byte> csvScratch);

            int aupCapacity = capacity;
            if (TryReadCoreVaultBuffer(vault, BufferID.VaultAup64, 1, out NativeArray<VaultAup64>.ReadOnly aups))
                aupCapacity = math.max(aupCapacity, aups.Length);
            bool hasSectorLocal = TryEnsureCoreVaultBuffer(
                vault,
                BufferID.VaultAupSectorLocal32,
                aupCapacity,
                NativeArrayOptions.UninitializedMemory,
                out NativeArray<VaultAupSectorLocal32> sectorLocal);

            return ok &&
                hasActiveCount &&
                hasShiftCount &&
                hasShiftRecords &&
                hasCsvScratch &&
                hasSectorLocal &&
                activeCount.IsCreated &&
                shiftCount.IsCreated &&
                shiftRecords.IsCreated &&
                csvScratch.IsCreated &&
                sectorLocal.IsCreated;
        }

        public static VaultSovereigntyMaintenanceStats RunPreSimulationFrost(
            IDataVault vault,
            float globalQualityWeight,
            uint frame)
        {
            VaultSovereigntyMaintenanceStats stats = default;
            if (vault == null)
                return stats;

            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float strideAggressiveness = ResolveStrideAggressiveness(vault);
            bool executed = false;

            NativeArray<VaultHotEntityData> hotEntities = default;
            TryResolveCoreVaultBuffer(vault, BufferID.VaultHotEntityData, 1, out hotEntities);

            if (TryResolveCoreVaultBuffer(vault, BufferID.VaultAup64, 1, out NativeArray<VaultAup64> aups) &&
                aups.IsCreated &&
                hotEntities.IsCreated)
            {
                int count = math.min(aups.Length, hotEntities.Length);
                if (count > 0)
                {
                    bool hasSectorLocal = TryEnsureCoreVaultBuffer(
                        vault,
                        BufferID.VaultAupSectorLocal32,
                        count,
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<VaultAupSectorLocal32> sectorLocal);
                    if (hasSectorLocal && sectorLocal.IsCreated)
                    {
                        VaultAupPrecisionDeltaCompactionJob job = new VaultAupPrecisionDeltaCompactionJob
                        {
                            Aups = aups,
                            SectorLocal32 = sectorLocal,
                            HotEntities = hotEntities,
                            SectorSizeMeters = HectonPhysicsContract.AupSectorSizeMetersFloat,
                            Frame = frame
                        };
                        for (int index = 0; index < count; index++)
                            job.Execute(index);
                        executed = true;
                        stats.AupRowsVisited = count;
                        stats.Flags |= FlagAupWrapped;
                    }
                }
            }

            if (hotEntities.IsCreated && hotEntities.Length > 0)
            {
                bool hasActiveCount = TryEnsureCoreVaultBuffer(
                    vault,
                    BufferID.VaultSovereigntyActiveEntityCount,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<int> activeCount);
                if (hasActiveCount && activeCount.IsCreated)
                {
                    int active = activeCount[0];
                    if (active <= 0 || active > hotEntities.Length)
                        activeCount[0] = hotEntities.Length;

                    int budget = ResolveSweepBudget(activeCount[0], hotEntities.Length, quality, strideAggressiveness);
                    stats.ScanBudget = budget;
                    TryEnsureCoreVaultBuffer(
                        vault,
                        BufferID.VaultMemoryAddressShiftRecords,
                        math.max(1, budget),
                        NativeArrayOptions.UninitializedMemory,
                        out NativeArray<VaultMemoryAddressShiftRecord> shiftRecords);
                    TryEnsureCoreVaultBuffer(
                        vault,
                        BufferID.VaultMemoryAddressShiftCount,
                        1,
                        NativeArrayOptions.ClearMemory,
                        out NativeArray<int> shiftCount);
                    if (shiftCount.IsCreated && shiftCount.Length > 0)
                        shiftCount[0] = 0;

                    VaultOrphanedPointerSweepJob job = new VaultOrphanedPointerSweepJob
                    {
                        HotEntities = hotEntities,
                        Aups = ResolveOptionalAup64(vault),
                        SectorLocal32 = ResolveOptionalSectorLocal32(vault),
                        ActiveCount = activeCount,
                        ShiftRecords = shiftRecords,
                        ShiftCount = shiftCount,
                        MaxScanCount = budget,
                        BufferId = BufferID.VaultHotEntityData,
                        Frame = frame,
                        SourceHash = SourceHash,
                        SystemId = (byte)SystemID.CoreDataVault
                    };
                    job.Execute();
                    executed = true;
                    stats.Flags |= FlagSweepExecuted;
                }
            }

            if (executed)
                stats.Flags |= FlagCompleted;

            if (TryReadCoreVaultBuffer(vault, BufferID.VaultSovereigntyActiveEntityCount, 1, out NativeArray<int>.ReadOnly resolvedCount) &&
                resolvedCount.Length > 0)
            {
                stats.ActiveCount = resolvedCount[0];
            }

            double elapsedUs =
                (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000000.0d /
                System.Diagnostics.Stopwatch.Frequency;
            stats.MaxJobUs = (float)math.max(0.0d, elapsedUs);
            return stats;
        }

        private static NativeArray<VaultAup64> ResolveOptionalAup64(IDataVault vault)
        {
            return TryResolveCoreVaultBuffer(vault, BufferID.VaultAup64, 1, out NativeArray<VaultAup64> aups) ? aups : default;
        }

        private static NativeArray<VaultAupSectorLocal32> ResolveOptionalSectorLocal32(IDataVault vault)
        {
            return TryResolveCoreVaultBuffer(vault, BufferID.VaultAupSectorLocal32, 1, out NativeArray<VaultAupSectorLocal32> local32) ? local32 : default;
        }

        private static float ResolveStrideAggressiveness(IDataVault vault)
        {
            if (TryReadCoreVaultBuffer(vault, BufferID.VaultMemoryLayoutConfig, 1, out NativeArray<VaultMemoryLayoutConfig>.ReadOnly configs) &&
                configs.Length > 0)
            {
                float value = configs[0].StrideAggressiveness;
                return math.saturate(math.isfinite(value) ? value : 0.35f);
            }

            return 0.35f;
        }

        private static bool TryEnsureCoreVaultBuffer<T>(
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
                SystemID.CoreDataVault,
                options);

            return IsCoreVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength;
        }

        private static bool TryResolveCoreVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                vault.IsCompactionFenceActive ||
                !vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                !IsCoreVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out NativeArray<T> resolved) ||
                !resolved.IsCreated ||
                resolved.Length < requiredLength)
            {
                return false;
            }

            buffer = resolved;
            return true;
        }

        private static bool TryReadCoreVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                vault.IsCompactionFenceActive ||
                !vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                !IsCoreVaultHandle(in handle, bufferId) ||
                !vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) ||
                resolved.Length < requiredLength)
            {
                return false;
            }

            buffer = resolved;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCoreVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                handle.SystemID == (uint)SystemID.CoreDataVault &&
                handle.Generation != 0u;
        }

        private static int ResolvePrewarmCapacity(int hotEntityCapacity)
        {
            int requested = hotEntityCapacity > 0 ? hotEntityCapacity : DefaultHotEntityCapacity;
            return math.clamp(requested, MinimumSweepRows, 1048576);
        }

        private static int ResolveSweepBudget(int activeCount, int capacity, float quality, float strideAggressiveness)
        {
            int count = math.clamp(activeCount, 0, math.max(0, capacity));
            if (count <= 0)
                return 0;

            float dampedQuality = math.saturate(quality * math.lerp(1f, quality, math.saturate(strideAggressiveness)));
            float curved = dampedQuality * dampedQuality * (3f - (2f * dampedQuality));
            float minimum = math.min(count, MinimumSweepRows);
            return math.clamp((int)math.ceil(math.lerp(minimum, count, curved)), 1, count);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct VaultAupPrecisionDeltaCompactionJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<VaultAup64> Aups;
        [NoAlias] public NativeArray<VaultAupSectorLocal32> SectorLocal32;
        [NoAlias] public NativeArray<VaultHotEntityData> HotEntities;
        public float SectorSizeMeters;
        public uint Frame;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Aups.Length)
                return;

            float sectorSize = math.max(1f, math.isfinite(SectorSizeMeters) ? SectorSizeMeters : HectonPhysicsContract.AupSectorSizeMetersFloat);
            VaultAup64 aup = Aups[index];
            WrapAxis(ref aup.SectorX, ref aup.LocalX, sectorSize);
            WrapAxis(ref aup.SectorY, ref aup.LocalY, sectorSize);
            WrapAxis(ref aup.SectorZ, ref aup.LocalZ, sectorSize);

            if (!IsFinite(in aup))
                aup = default;

            Aups[index] = aup;
            float3 local = new float3((float)aup.LocalX, (float)aup.LocalY, (float)aup.LocalZ);
            if (!math.all(math.isfinite(local)))
                local = float3.zero;

            uint entityId = 0u;
            if (HotEntities.IsCreated && (uint)index < (uint)HotEntities.Length)
            {
                VaultHotEntityData hot = HotEntities[index];
                entityId = hot.EntityId;
                hot.LocalPosition = local;
                hot.ShiftFrameId = Frame;
                hot.SimulationBucket = VaultMemoryMath.ResolveSimulationBucket(in aup);
                HotEntities[index] = hot;
            }

            if (SectorLocal32.IsCreated && (uint)index < (uint)SectorLocal32.Length)
            {
                VaultAupSectorLocal32 split = SectorLocal32[index];
                split.SectorX = aup.SectorX;
                split.SectorY = aup.SectorY;
                split.SectorZ = aup.SectorZ;
                split.LocalOffset = local;
                split.EntityId = entityId;
                split.ShiftFrameId = Frame;
                SectorLocal32[index] = split;
            }
        }

        private static void WrapAxis(ref long sector, ref double local, float sectorSize)
        {
            if (!math.isfinite(local))
            {
                local = 0.0d;
                return;
            }

            if (local >= 0.0d && local < sectorSize)
                return;

            double shift = math.floor(local / sectorSize);
            if (!math.isfinite(shift))
            {
                local = 0.0d;
                return;
            }

            long sectorDelta = (long)shift;
            sector += sectorDelta;
            local -= sectorDelta * (double)sectorSize;
            if (local < 0.0d)
            {
                sector -= 1L;
                local += sectorSize;
            }
            else if (local >= sectorSize)
            {
                sector += 1L;
                local -= sectorSize;
            }
        }

        private static bool IsFinite(in VaultAup64 aup)
        {
            return math.isfinite(aup.LocalX) &&
                math.isfinite(aup.LocalY) &&
                math.isfinite(aup.LocalZ);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct VaultOrphanedPointerSweepJob : IJob
    {
        [NoAlias] public NativeArray<VaultHotEntityData> HotEntities;
        [NoAlias] public NativeArray<VaultAup64> Aups;
        [NoAlias] public NativeArray<VaultAupSectorLocal32> SectorLocal32;
        [NoAlias] public NativeArray<int> ActiveCount;
        [NoAlias] public NativeArray<VaultMemoryAddressShiftRecord> ShiftRecords;
        [NoAlias] public NativeArray<int> ShiftCount;
        public int MaxScanCount;
        public BufferID BufferId;
        public uint Frame;
        public uint SourceHash;
        public byte SystemId;

        public void Execute()
        {
            if (!HotEntities.IsCreated || !ActiveCount.IsCreated || ActiveCount.Length == 0)
                return;

            int count = math.clamp(ActiveCount[0], 0, HotEntities.Length);
            int budget = math.clamp(MaxScanCount, 0, count);
            int processed = 0;
            int index = 0;
            while (index < count && processed < budget)
            {
                VaultHotEntityData hot = HotEntities[index];
                if (IsAlive(in hot))
                {
                    index++;
                    processed++;
                    continue;
                }

                int last = count - 1;
                if (index != last)
                {
                    VaultHotEntityData moved = HotEntities[last];
                    HotEntities[index] = moved;
                    MoveOptionalAup(last, index);
                    ClearSlot(last);
                    PublishShift(last, index, in moved, count - 1);
                }
                else
                {
                    ClearSlot(last);
                }

                count--;
                processed++;
            }

            ActiveCount[0] = count;
        }

        private static bool IsAlive(in VaultHotEntityData hot)
        {
            return hot.EntityId != 0u &&
                math.all(math.isfinite(hot.LocalPosition)) &&
                math.all(math.isfinite(hot.Velocity));
        }

        private void ClearSlot(int slot)
        {
            if ((uint)slot < (uint)HotEntities.Length)
                HotEntities[slot] = default;
            if (Aups.IsCreated && (uint)slot < (uint)Aups.Length)
                Aups[slot] = default;
            if (SectorLocal32.IsCreated && (uint)slot < (uint)SectorLocal32.Length)
                SectorLocal32[slot] = default;
        }

        private void MoveOptionalAup(int from, int to)
        {
            if (Aups.IsCreated && (uint)from < (uint)Aups.Length && (uint)to < (uint)Aups.Length)
                Aups[to] = Aups[from];
            if (SectorLocal32.IsCreated && (uint)from < (uint)SectorLocal32.Length && (uint)to < (uint)SectorLocal32.Length)
                SectorLocal32[to] = SectorLocal32[from];
        }

        private void PublishShift(int oldIndex, int newIndex, in VaultHotEntityData moved, int compactedCount)
        {
            if (moved.EntityId == 0u ||
                !ShiftRecords.IsCreated ||
                !ShiftCount.IsCreated ||
                ShiftCount.Length == 0)
                return;

            int writeIndex = ShiftCount[0];
            if ((uint)writeIndex >= (uint)ShiftRecords.Length)
                return;

            VaultMemoryAddressShiftRecord record = default;
            record.BufferId = (int)BufferId;
            record.ByteLength = UnsafeUtility.SizeOf<VaultHotEntityData>();
            record.Flags = VaultMemoryAddressShiftRecord.FlagSwapPopIndexMove;
            record.SystemId = SystemId;
            record.OldIndex = oldIndex;
            record.NewIndex = newIndex;
            record.MovedEntityId = moved.EntityId;
            record.SourceFrame = Frame;
            record.SourceHash = SourceHash;
            record.CompactedCount = (uint)math.max(0, compactedCount);
            ShiftRecords[writeIndex] = record;
            ShiftCount[0] = writeIndex + 1;
        }
    }

    /// <summary>
    /// Human-authored memory sizing facade consumed by bootstrap before GlobalDataVault creation.
    /// </summary>
    [CreateAssetMenu(fileName = "VaultConfigurationAsset", menuName = "Hecton8/Core/Vault Configuration")]
    public sealed class VaultConfigurationAsset : ScriptableObject
    {
        [Header("Vault Limits")]
        [Tooltip("Minimum-quality vault arena limit in bytes.")]
        [SerializeField, FormerlySerializedAs("lowArenaLimitBytes")] private long minimumQualityArenaLimitBytes = GlobalDataVault.MinimumQualityArenaLimitBytes;
        [Tooltip("Maximum-quality vault arena limit in bytes.")]
        [SerializeField, FormerlySerializedAs("highArenaLimitBytes")] private long maximumQualityArenaLimitBytes = GlobalDataVault.MaximumQualityArenaLimitBytes;
        [Tooltip("GlobalDataVault buffer table capacity.")]
        [SerializeField, Range(128, 32768)] private int bufferCapacity = 512;

        [Header("Entity Streams")]
        [Tooltip("Maximum hot entity records.")]
        [SerializeField, Range(64, 1048576)] private int hotEntityCapacity = 1024;
        [Tooltip("Maximum cold entity records.")]
        [SerializeField, Range(64, 1048576)] private int coldEntityCapacity = 1024;
        [Tooltip("Simulation bucket count. Runtime clamps to 64.")]
        [SerializeField, Range(1, 64)] private int bucketCapacity = 64;
        [Tooltip("Designer-authored multiplier for quality-driven memory maintenance striding.")]
        [SerializeField, Range(0f, 1f)] private float strideAggressiveness = 0.35f;

        /// <summary>Resolves the arena limit for the active scalability profile.</summary>
        public long ResolveArenaLimitBytes(byte scalabilityProfile)
        {
            long minimum = minimumQualityArenaLimitBytes > 0L
                ? minimumQualityArenaLimitBytes
                : GlobalDataVault.ResolveArenaCapacityLimit(0);
            long maximum = maximumQualityArenaLimitBytes > 0L
                ? maximumQualityArenaLimitBytes
                : GlobalDataVault.ResolveArenaCapacityLimit(3);
            if (maximum < minimum)
                maximum = minimum;

            float profile01 = GlobalDataVault.DecodeScalabilityProfile01(scalabilityProfile);
            float curve01 = profile01 * profile01 * (3f - (2f * profile01));
            return (long)math.round(minimum + ((double)(maximum - minimum) * curve01));
        }

        /// <summary>Builds the runtime layout config written into the vault.</summary>
        public VaultMemoryLayoutConfig BuildRuntimeConfig(byte scalabilityProfile)
        {
            VaultMemoryLayoutConfig config = default;
            config.ArenaLimitBytes = ResolveArenaLimitBytes(scalabilityProfile);
            config.BufferCapacity = math.clamp(bufferCapacity, 128, 32768);
            config.HotEntityCapacity = math.max(64, hotEntityCapacity);
            config.ColdEntityCapacity = math.max(64, coldEntityCapacity);
            config.BucketCapacity = math.clamp(bucketCapacity, 1, 64);
            config.SourceHash = 0x5641554Cu; // VAUL
            config.Version = 1u;
            config.ScalabilityProfile = scalabilityProfile;
            config.StrideAggressiveness = math.saturate(strideAggressiveness);
            return config;
        }
    }
}
