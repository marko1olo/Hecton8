using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Memory
{
    /// <summary>
    /// Runtime memory layout profile imported from legacy binary files, CSV overrides, or mock fallback. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VaultMemoryLayoutConfig
    {
        public long ArenaLimitBytes;
        public int BufferCapacity;
        public int HotEntityCapacity;
        public int ColdEntityCapacity;
        public int BucketCapacity;
        public uint SourceHash;
        public uint Version;
        public byte ScalabilityProfile;
        public byte Flags;
        private ushort _pad0;
        private uint _pad1;
        private long _pad2;
        private long _pad3;
        private long _pad4;
    }

    /// <summary>
    /// Absolute universe position with 64-bit authority. Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct VaultAup64
    {
        public long SectorX;
        public long SectorY;
        public long SectorZ;
        public double LocalX;
        public double LocalY;
        public double LocalZ;
    }

    /// <summary>
    /// Hot per-frame entity stream. No display/lore data. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VaultHotEntityData
    {
        public float4 Rotation;
        public float3 LocalPosition;
        public float3 Velocity;
        public uint EntityId;
        public uint Flags;
        public uint ShiftFrameId;
        public byte SimulationBucket;
        public byte LodTier;
        private ushort _pad0;
        private uint _pad1;
        private uint _pad2;
    }

    /// <summary>
    /// Cold entity metadata stream. Read outside tight simulation loops. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VaultColdEntityData
    {
        public ulong DisplayNameHash;
        public ulong FactionMask;
        public uint EntityId;
        public uint ArchetypeHash;
        public uint PrefabHash;
        public int MaxHealth;
        public int MaxEnergy;
        public ushort Flags;
        public ushort MaterialSet;
        private uint _pad0;
        private uint _pad1;
        private long _pad2;
        private long _pad3;
    }

    /// <summary>
    /// Pointer-alias record for static transforms using the Dear Lie protocol. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VaultTransformAlias
    {
        public long MatrixPointer;
        public uint TransformHash;
        public uint EntityId;
        public byte Flags;
        private byte _pad0;
        private ushort _pad1;
        private long _pad2;
    }

    /// <summary>
    /// Immutable byte-size and alignment contract for vault-owned buffers. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public readonly struct VaultBufferContract
    {
        public const int LayoutConfigSizeBytes = 64;
        public const int Aup64SizeBytes = 48;
        public const int HotEntitySizeBytes = 64;
        public const int ColdEntitySizeBytes = 64;
        public const int TransformAliasSizeBytes = 32;
        public const int RequiredAlignmentBytes = 8;
        public const int CacheLineBytes = 64;
        public const double AupSectorSizeMeters = HectonPhysicsContract.AupSectorSizeMetersDouble;
        public const int LayoutConfigBufferId = (int)BufferID.VaultMemoryLayoutConfig;
        public const int HotEntityBufferId = (int)BufferID.VaultHotEntityData;
        public const int ColdEntityBufferId = (int)BufferID.VaultColdEntityData;
        public const int Aup64BufferId = (int)BufferID.VaultAup64;
        public const int EntityBucketMapBufferId = (int)BufferID.VaultEntityBucketMap;
        public const int SharedTransformMatricesBufferId = (int)BufferID.VaultSharedTransformMatrices;
        public const int OwnedBufferCount = 6;
        public const int MinBufferId = LayoutConfigBufferId;
        // SHINOBU owns only the contiguous vault memory range 550-555. Peer enum high-water marks are not part of this ABI.
        public const int MaxBufferId = MinBufferId + OwnedBufferCount - 1;

        public const int LayoutConfigArenaLimitOffset = 0;
        public const int LayoutConfigBufferCapacityOffset = 8;
        public const int LayoutConfigHotEntityCapacityOffset = 12;
        public const int LayoutConfigColdEntityCapacityOffset = 16;
        public const int LayoutConfigBucketCapacityOffset = 20;
        public const int LayoutConfigSourceHashOffset = 24;
        public const int LayoutConfigVersionOffset = 28;
        public const int LayoutConfigScalabilityProfileOffset = 32;
        public const int LayoutConfigFlagsOffset = 33;

        public const int AupSectorXOffset = 0;
        public const int AupSectorYOffset = 8;
        public const int AupSectorZOffset = 16;
        public const int AupLocalXOffset = 24;
        public const int AupLocalYOffset = 32;
        public const int AupLocalZOffset = 40;

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

        public const int TransformAliasMatrixPointerOffset = 0;
        public const int TransformAliasTransformHashOffset = 8;
        public const int TransformAliasEntityIdOffset = 12;
        public const int TransformAliasFlagsOffset = 16;

        public readonly int LayoutConfigSize;
        public readonly int Aup64Size;
        public readonly int HotEntitySize;
        public readonly int ColdEntitySize;
        public readonly int TransformAliasSize;
        public readonly int RequiredAlignment;
        public readonly int CacheLineSize;
        public readonly int MinEnumValue;
        public readonly int MaxEnumValue;
        private readonly int _pad0;
        private readonly long _pad1;
        private readonly long _pad2;
        private readonly long _pad3;

        /// <summary>Creates the compile-time layout contract instance.</summary>
        public VaultBufferContract(byte _)
        {
            LayoutConfigSize = LayoutConfigSizeBytes;
            Aup64Size = Aup64SizeBytes;
            HotEntitySize = HotEntitySizeBytes;
            ColdEntitySize = ColdEntitySizeBytes;
            TransformAliasSize = TransformAliasSizeBytes;
            RequiredAlignment = RequiredAlignmentBytes;
            CacheLineSize = CacheLineBytes;
            MinEnumValue = MinBufferId;
            MaxEnumValue = MaxBufferId;
            _pad0 = 0;
            _pad1 = 0L;
            _pad2 = 0L;
            _pad3 = 0L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool OwnsBufferId(BufferID bufferId)
        {
            int id = (int)bufferId;
            return (uint)(id - MinBufferId) < OwnedBufferCount;
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
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct VaultAupLocalOffsetResolverJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<VaultAup64> EntityAups;
        public NativeArray<VaultHotEntityData> HotEntities;
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
    /// Human-authored memory sizing facade consumed by bootstrap before GlobalDataVault creation.
    /// </summary>
    [CreateAssetMenu(fileName = "VaultConfigurationAsset", menuName = "Hecton8/Core/Vault Configuration")]
    public sealed class VaultConfigurationAsset : ScriptableObject
    {
        [Header("Vault Limits")]
        [Tooltip("Low profile vault arena limit in bytes.")]
        [SerializeField] private long lowArenaLimitBytes = GlobalDataVault.LowTierArenaLimitBytes;
        [Tooltip("High profile vault arena limit in bytes.")]
        [SerializeField] private long highArenaLimitBytes = GlobalDataVault.HighTierArenaLimitBytes;
        [Tooltip("GlobalDataVault buffer table capacity.")]
        [SerializeField, Range(128, 32768)] private int bufferCapacity = 512;

        [Header("Entity Streams")]
        [Tooltip("Maximum hot entity records.")]
        [SerializeField, Range(64, 1048576)] private int hotEntityCapacity = 1024;
        [Tooltip("Maximum cold entity records.")]
        [SerializeField, Range(64, 1048576)] private int coldEntityCapacity = 1024;
        [Tooltip("Simulation bucket count. Runtime clamps to 64.")]
        [SerializeField, Range(1, 64)] private int bucketCapacity = 64;

        /// <summary>Resolves the arena limit for the active scalability profile.</summary>
        public long ResolveArenaLimitBytes(byte scalabilityProfile)
        {
            long selected = scalabilityProfile == 0 ? lowArenaLimitBytes : highArenaLimitBytes;
            return selected > 0L ? selected : GlobalDataVault.ResolveArenaCapacityLimit(scalabilityProfile);
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
            return config;
        }
    }
}
