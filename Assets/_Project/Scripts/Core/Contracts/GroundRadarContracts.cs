using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VoxelSonarSdfRaycastHit
    {
        public const uint FlagHit = 1u << 0;

        [FieldOffset(0)] public float3 Point;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float Distance;
        [FieldOffset(28)] public float Density;
        [FieldOffset(32)] public float Density01;
        [FieldOffset(36)] public float SdfRange;
        [FieldOffset(40)] public int Version;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VoxelSdfPayloadDescriptorDTO
    {
        public const uint FlagValid = 1u << 0;

        [FieldOffset(0)] public float3 VolumeOrigin;
        [FieldOffset(12)] public int3 GridDimensions;
        [FieldOffset(24)] public float3 VoxelCellSize;
        [FieldOffset(36)] public float SdfRangeMeters;
        [FieldOffset(40)] public int ByteCount;
        [FieldOffset(44)] public uint BufferId;
        [FieldOffset(48)] public uint BufferGeneration;
        [FieldOffset(52)] public uint SdfVersion;
        [FieldOffset(56)] public uint OwnerSystemId;
        [FieldOffset(60)] public uint Flags;
    }

    /// <summary>
    /// Registry-facing voxel SDF read model for sonar/GPR consumers.
    /// Implementations own the voxel volume list and publish immutable SDF snapshots.
    /// </summary>
    public interface IVoxelSonarSdfReadModel
    {
        bool TryReadNearestSonarSdf(
            float3 runtimeOrigin,
            out NativeArray<byte> encodedSdf,
            out int3 gridDimensions,
            out float3 volumeOrigin,
            out float3 cellSize,
            out float sdfRange);

        bool TryRaymarchNearestSonarSdf(
            float3 runtimeOrigin,
            float3 runtimeDirection,
            float maxDistance,
            float stepMeters,
            out VoxelSonarSdfRaycastHit hit,
            out NativeArray<byte> encodedSdf,
            out int3 gridDimensions,
            out float3 volumeOrigin,
            out float3 cellSize,
            out float sdfRange);

        bool TrySampleNearestSonarSdf(
            float3 runtimePosition,
            out float density,
            out float density01);
    }

    /// <summary>
    /// Optional owner-local SDF sampler for spatial hits whose owner already exposes the exact voxel payload.
    /// </summary>
    public interface IVoxelSonarSdfSampleSource
    {
        bool TrySampleSonarSdf(
            float3 runtimePosition,
            out float density,
            out float density01);
    }

    /// <summary>
    /// Optional voxel-owner command surface for repair tools. This mutates voxel delta state and is not a read accessor.
    /// </summary>
    public interface IVoxelRepairWeldTarget
    {
        bool TryApplyRepairWeldDda(
            double3 absoluteHitPoint,
            Vector3 direction,
            float normalizedPower,
            float maxDistance);
    }

    /// <summary>
    /// Optional voxel-owner command surface for plasma cutters. This mutates voxel delta state and is not a read accessor.
    /// </summary>
    public interface IVoxelPlasmaCutTarget
    {
        bool TryApplyPlasmaCutDda(
            double3 absoluteHitPoint,
            Vector3 direction,
            float normalizedPower,
            float maxDistance);
    }
}

namespace Hecton8.World
{
    /// <summary>
    /// Registry-facing GPR read model. Runtime ownership stays in World; cockpit and tools consume this interface only.
    /// </summary>
    public interface IGroundRadarService
    {
        int ActiveGprPings { get; }
        int GprSequence { get; }
        int OreFilterType { get; }
        float3 LastProbeOrigin { get; }
        float ScanRadiusMeters { get; }
        NativeArray<float3>.ReadOnly GprHitsReadOnly { get; }
        NativeArray<float>.ReadOnly GprSignalStrengthReadOnly { get; }
        void SetOreFilterType(int oreType);
        bool TryGetGprPingBuffer(out GraphicsBuffer buffer, out int activeCount, out int sequence);
        bool TryCopyGprPings(NativeArray<float4> destination, out int copiedCount);
    }

    /// <summary>
    /// Registry-facing ore SoA read model owned by the world resource spawner.
    /// </summary>
    public interface IWorldResourceSpawnerReadModel
    {
        int ActiveOreCount { get; }
        int LocalTitaniumCount { get; }
        /// <summary>Returns the sparse immutable ore position lane plus the valid scan window length; zero-type slots inside the window are holes.</summary>
        bool TryGetOrePositionsReadOnly(out NativeArray<float3>.ReadOnly orePositions, out int scanCount);
        /// <summary>Returns the sparse immutable ore type lane plus the valid scan window length; zero means no live ore in that slot.</summary>
        bool TryGetOreTypesReadOnly(out NativeArray<int>.ReadOnly oreTypes, out int scanCount);
    }

    /// <summary>
    /// Optional zero-copy reader fence sink for jobs scheduled over the ore read model.
    /// </summary>
    public interface IWorldResourceSpawnerReadDependencySink
    {
        void RegisterOreReadDependency(JobHandle readDependency);
    }

    /// <summary>
    /// Registry-facing command lane for data-only procedural resource depletion.
    /// </summary>
    public interface IWorldResourceSpawnerCommandModel
    {
        /// <summary>
        /// Marks a sparse ore scan index as depleted, emits owner-local depletion side effects, and returns primitive data for interaction/VFX consumers.
        /// </summary>
        bool TryMarkOreDepleted(int oreIndex, out uint oreHash, out uint itemHash, out float3 depletedPosition);
    }

    /// <summary>
    /// Stable ore ids shared by ore authority, GPR filtering, HUD controls, and telemetry.
    /// </summary>
    public static class WorldOreTypeIds
    {
        public const int None = 0;
        public const int BasaltIron = 1;
        public const int Copper = 2;
        public const int Titanium = 3;
        public const int Silver = 4;
    }
}
