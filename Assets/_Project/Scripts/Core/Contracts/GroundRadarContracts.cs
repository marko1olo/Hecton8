using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts
{
    internal static class GroundRadarLayout
    {
        internal const int VoxelSonarSdfRaycastHitStrideBytes = 64;
        internal const int VoxelSdfPayloadDescriptorStrideBytes = 80;
        internal const int VoxelSonarSdfReadLeaseStrideBytes = 24;
    }

    [StructLayout(LayoutKind.Explicit, Size = GroundRadarLayout.VoxelSonarSdfRaycastHitStrideBytes)]
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

    [StructLayout(LayoutKind.Explicit, Size = GroundRadarLayout.VoxelSdfPayloadDescriptorStrideBytes)]
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
        [FieldOffset(64)] public int AudioMaterialByteCount;
        [FieldOffset(68)] public uint AudioMaterialBufferId;
        [FieldOffset(72)] public uint AudioMaterialBufferGeneration;
        [FieldOffset(76)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = GroundRadarLayout.VoxelSonarSdfReadLeaseStrideBytes)]
    public struct VoxelSonarSdfReadLease
    {
        public const uint FlagValid = 1u << 0;

        [FieldOffset(0)] public uint SdfGeneration;
        [FieldOffset(4)] public uint AudioMaterialGeneration;
        [FieldOffset(8)] public int Version;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] private ulong _pad0;

        public bool IsValid => (Flags & FlagValid) != 0u && SdfGeneration != 0u && Version > 0;
    }

    /// <summary>
    /// Registry-facing voxel SDF read model for sonar/GPR consumers.
    /// Implementations own the voxel volume list and publish immutable SDF snapshots.
    /// </summary>
    public interface IVoxelSonarSdfReadModel
    {
        bool TryReadNearestSonarSdf(
            float3 runtimeOrigin,
            out NativeArray<byte>.ReadOnly encodedSdf,
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
            out NativeArray<byte>.ReadOnly encodedSdf,
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
    /// Optional paired lifetime contract for consumers that schedule work over voxel SDF views.
    /// Callers must release the lease after the last scheduled reader completes.
    /// </summary>
    public interface IVoxelSonarSdfReadLeaseModel
    {
        bool TryAcquireNearestSonarSdfReadLease(
            float3 runtimeOrigin,
            out NativeArray<byte>.ReadOnly encodedSdf,
            out int3 gridDimensions,
            out float3 volumeOrigin,
            out float3 cellSize,
            out float sdfRange,
            out VoxelSonarSdfReadLease lease);

        void ReleaseNearestSonarSdfReadLease(in VoxelSonarSdfReadLease lease);
    }

    /// <summary>
    /// Optional owner-local resolver for read models that own several SDF payloads.
    /// This keeps ray-directed volume selection in the volume owner instead of forcing consumers
    /// to accept a nearest-origin payload when the ray crosses another volume first.
    /// </summary>
    public interface IVoxelSonarSdfSurfaceResolver
    {
        bool TryResolveNearestSonarSdfSurface(
            float3 runtimeOrigin,
            float3 runtimeDirection,
            float maxDistance,
            float stepMeters,
            out VoxelSonarSdfRaycastHit hit,
            out NativeArray<byte>.ReadOnly encodedSdf,
            out int3 gridDimensions,
            out float3 volumeOrigin,
            out float3 cellSize,
            out float sdfRange);
    }

    /// <summary>
    /// Pure encoded-SDF surface sampling shared by scanner/KCC-adjacent callers.
    /// The resolver is finite by construction: all raymarch loops are clamped by distance and MaxEncodedRaymarchSteps.
    /// </summary>
    public static class VoxelSonarSdfMath
    {
        public const int MaxEncodedRaymarchSteps = 2048;
        public const float MinimumRaymarchStepMeters = 0.025f;
        private const float NormalProbeScale = 0.5f;
        private const float Epsilon = 0.0001f;
        private const float InvByteMax = 1f / 255f;

        public static bool TryResolveNearestSdfSurface(
            IVoxelSonarSdfReadModel readModel,
            float3 runtimeOrigin,
            float3 runtimeDirection,
            float maxDistance,
            float stepMeters,
            out VoxelSonarSdfRaycastHit hit)
        {
            return TryResolveNearestSdfSurface(
                readModel,
                runtimeOrigin,
                runtimeDirection,
                maxDistance,
                stepMeters,
                out hit,
                out NativeArray<byte>.ReadOnly _,
                out int3 _,
                out float3 _,
                out float3 _,
                out float _);
        }

        public static bool TryResolveNearestSdfSurface(
            IVoxelSonarSdfReadModel readModel,
            float3 runtimeOrigin,
            float3 runtimeDirection,
            float maxDistance,
            float stepMeters,
            out VoxelSonarSdfRaycastHit hit,
            out NativeArray<byte>.ReadOnly encodedSdf,
            out int3 gridDimensions,
            out float3 volumeOrigin,
            out float3 cellSize,
            out float sdfRange)
        {
            hit = default;
            encodedSdf = default;
            gridDimensions = default;
            volumeOrigin = default;
            cellSize = default;
            sdfRange = 0f;

            if (readModel == null)
                return false;

            if (readModel is IVoxelSonarSdfSurfaceResolver surfaceResolver)
            {
                return surfaceResolver.TryResolveNearestSonarSdfSurface(
                    runtimeOrigin,
                    runtimeDirection,
                    maxDistance,
                    stepMeters,
                    out hit,
                    out encodedSdf,
                    out gridDimensions,
                    out volumeOrigin,
                    out cellSize,
                    out sdfRange);
            }

            if (readModel is not IVoxelSonarSdfReadLeaseModel leaseModel)
                return false;

            VoxelSonarSdfReadLease lease = default;
            bool leaseLocked = false;
            try
            {
                if (!leaseModel.TryAcquireNearestSonarSdfReadLease(
                        runtimeOrigin,
                        out encodedSdf,
                        out gridDimensions,
                        out volumeOrigin,
                        out cellSize,
                        out sdfRange,
                        out lease))
                {
                    return false;
                }

                leaseLocked = true;
                return TryRaymarchEncodedSdf(
                    encodedSdf,
                    gridDimensions,
                    volumeOrigin,
                    cellSize,
                    sdfRange,
                    runtimeOrigin,
                    runtimeDirection,
                    maxDistance,
                    stepMeters,
                    out hit);
            }
            finally
            {
                if (leaseLocked && lease.IsValid)
                    leaseModel.ReleaseNearestSonarSdfReadLease(in lease);
            }
        }

        public static bool TryRaymarchEncodedSdf(
            NativeArray<byte>.ReadOnly encodedSdf,
            int3 gridDimensions,
            float3 volumeOrigin,
            float3 cellSize,
            float sdfRange,
            float3 runtimeOrigin,
            float3 runtimeDirection,
            float maxDistance,
            float stepMeters,
            out VoxelSonarSdfRaycastHit hit)
        {
            hit = default;
            if (!encodedSdf.IsCreated ||
                !math.all(gridDimensions > 1) ||
                !math.all(math.isfinite(volumeOrigin)) ||
                !math.all(math.isfinite(cellSize)) ||
                !math.all(math.isfinite(runtimeOrigin)) ||
                !math.all(math.isfinite(runtimeDirection)) ||
                !math.isfinite(sdfRange) ||
                !math.isfinite(maxDistance) ||
                sdfRange <= Epsilon ||
                maxDistance <= 0f)
            {
                return false;
            }

            if (!TryResolveExpectedVoxelCount(gridDimensions, out long expectedLength) ||
                encodedSdf.Length < expectedLength)
                return false;

            float directionLengthSq = math.lengthsq(runtimeDirection);
            if (!math.isfinite(directionLengthSq) || directionLengthSq <= Epsilon)
                return false;

            float3 direction = runtimeDirection * math.rsqrt(math.max(directionLengthSq, Epsilon));
            if (!TryResolveRaymarchInterval(
                    maxDistance,
                    runtimeOrigin,
                    direction,
                    volumeOrigin,
                    cellSize,
                    gridDimensions,
                    out float startDistance,
                    out float endDistance))
            {
                return false;
            }

            float segmentDistance = math.max(0f, endDistance - startDistance);
            float requestedStep = math.max(MinimumRaymarchStepMeters, math.isfinite(stepMeters) ? stepMeters : MinimumRaymarchStepMeters);
            float step = ResolveBoundedRaymarchStep(segmentDistance, requestedStep);
            int stepCount = ResolveBoundedRaymarchStepCount(segmentDistance, step);
            float previousDensity = 0f;
            float previousDistance = 0f;
            float3 previousPosition = runtimeOrigin;
            bool hasPrevious = false;

            for (int i = 0; i <= stepCount; i++)
            {
                float distance = math.min(endDistance, startDistance + i * step);
                float3 position = runtimeOrigin + direction * distance;
                if (!TrySampleEncodedSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, position, out float density))
                    return false;

                bool nearSurface = math.abs(density) <= Epsilon;
                bool crossedSurface =
                    hasPrevious &&
                    ((previousDensity < -Epsilon && density >= Epsilon) ||
                     (previousDensity > Epsilon && density <= -Epsilon));

                if (nearSurface || crossedSurface)
                {
                    float resolvedDistance = distance;
                    float3 resolvedPoint = position;
                    if (crossedSurface)
                    {
                        float previousAbsDensity = math.abs(previousDensity);
                        float currentAbsDensity = math.abs(density);
                        float t = math.saturate(previousAbsDensity * math.rcp(math.max(Epsilon, previousAbsDensity + currentAbsDensity)));
                        resolvedDistance = math.lerp(previousDistance, distance, t);
                        resolvedPoint = math.lerp(previousPosition, position, t);
                    }

                    float3 normal = EstimateEncodedSdfNormal(
                        encodedSdf,
                        gridDimensions,
                        volumeOrigin,
                        cellSize,
                        sdfRange,
                        resolvedPoint,
                        -direction);

                    hit.Point = resolvedPoint;
                    hit.Normal = normal;
                    hit.Distance = math.max(0f, resolvedDistance);
                    hit.Density = 0f;
                    hit.Density01 = 0f;
                    hit.SdfRange = sdfRange;
                    hit.Version = 0;
                    hit.Flags = VoxelSonarSdfRaycastHit.FlagHit;
                    return true;
                }

                if (distance >= endDistance)
                    break;

                previousDensity = density;
                previousDistance = distance;
                previousPosition = position;
                hasPrevious = true;
            }

            return false;
        }

        public static bool TrySampleEncodedSdfTrilinear(
            NativeArray<byte>.ReadOnly encodedSdf,
            int3 gridDimensions,
            float3 volumeOrigin,
            float3 cellSize,
            float sdfRange,
            float3 runtimePosition,
            out float density)
        {
            density = 0f;
            if (!encodedSdf.IsCreated ||
                !math.all(gridDimensions > 1) ||
                !math.all(math.isfinite(volumeOrigin)) ||
                !math.all(math.isfinite(cellSize)) ||
                !math.all(math.isfinite(runtimePosition)) ||
                !math.isfinite(sdfRange) ||
                sdfRange <= Epsilon)
            {
                return false;
            }

            if (!TryResolveExpectedVoxelCount(gridDimensions, out long expectedLength) ||
                encodedSdf.Length < expectedLength)
                return false;

            float3 safeCell = math.max(math.abs(cellSize), new float3(Epsilon));
            float3 sample = (runtimePosition - volumeOrigin) / safeCell;
            sample = math.clamp(sample, float3.zero, new float3(gridDimensions.x - 1.001f, gridDimensions.y - 1.001f, gridDimensions.z - 1.001f));
            int3 p0 = (int3)math.floor(sample);
            int3 p1 = math.min(p0 + new int3(1), gridDimensions - new int3(1));
            float3 t = sample - p0;

            float c000 = DecodeSdfAt(encodedSdf, gridDimensions, p0.x, p0.y, p0.z, sdfRange);
            float c100 = DecodeSdfAt(encodedSdf, gridDimensions, p1.x, p0.y, p0.z, sdfRange);
            float c010 = DecodeSdfAt(encodedSdf, gridDimensions, p0.x, p1.y, p0.z, sdfRange);
            float c110 = DecodeSdfAt(encodedSdf, gridDimensions, p1.x, p1.y, p0.z, sdfRange);
            float c001 = DecodeSdfAt(encodedSdf, gridDimensions, p0.x, p0.y, p1.z, sdfRange);
            float c101 = DecodeSdfAt(encodedSdf, gridDimensions, p1.x, p0.y, p1.z, sdfRange);
            float c011 = DecodeSdfAt(encodedSdf, gridDimensions, p0.x, p1.y, p1.z, sdfRange);
            float c111 = DecodeSdfAt(encodedSdf, gridDimensions, p1.x, p1.y, p1.z, sdfRange);
            float c00 = math.lerp(c000, c100, t.x);
            float c10 = math.lerp(c010, c110, t.x);
            float c01 = math.lerp(c001, c101, t.x);
            float c11 = math.lerp(c011, c111, t.x);
            density = math.lerp(math.lerp(c00, c10, t.y), math.lerp(c01, c11, t.y), t.z);
            return math.isfinite(density);
        }

        private static float3 EstimateEncodedSdfNormal(
            NativeArray<byte>.ReadOnly encodedSdf,
            int3 gridDimensions,
            float3 volumeOrigin,
            float3 cellSize,
            float sdfRange,
            float3 runtimePosition,
            float3 fallback)
        {
            float3 step = math.max(math.abs(cellSize) * NormalProbeScale, new float3(MinimumRaymarchStepMeters));
            TrySampleEncodedSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, runtimePosition + new float3(step.x, 0f, 0f), out float px);
            TrySampleEncodedSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, runtimePosition - new float3(step.x, 0f, 0f), out float nx);
            TrySampleEncodedSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, runtimePosition + new float3(0f, step.y, 0f), out float py);
            TrySampleEncodedSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, runtimePosition - new float3(0f, step.y, 0f), out float ny);
            TrySampleEncodedSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, runtimePosition + new float3(0f, 0f, step.z), out float pz);
            TrySampleEncodedSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, runtimePosition - new float3(0f, 0f, step.z), out float nz);
            float3 gradient = new float3(px - nx, py - ny, pz - nz);
            float lengthSq = math.lengthsq(gradient);
            if (math.isfinite(lengthSq) && lengthSq > Epsilon)
                return gradient * math.rsqrt(math.max(lengthSq, Epsilon));

            float fallbackLengthSq = math.lengthsq(fallback);
            return math.isfinite(fallbackLengthSq) && fallbackLengthSq > Epsilon
                ? fallback * math.rsqrt(math.max(fallbackLengthSq, Epsilon))
                : new float3(0f, 1f, 0f);
        }

        private static float DecodeSdfAt(
            NativeArray<byte>.ReadOnly encodedSdf,
            int3 gridDimensions,
            int x,
            int y,
            int z,
            float sdfRange)
        {
            int index = x + gridDimensions.x * (y + gridDimensions.y * z);
            if ((uint)index >= (uint)encodedSdf.Length)
                return 0f;

            return (((float)encodedSdf[index] * InvByteMax) * 2f - 1f) * sdfRange;
        }

        private static bool TryResolveRaymarchInterval(
            float maxDistance,
            float3 runtimeOrigin,
            float3 direction,
            float3 volumeOrigin,
            float3 cellSize,
            int3 gridDimensions,
            out float startDistance,
            out float endDistance)
        {
            startDistance = 0f;
            endDistance = 0f;
            if (!math.isfinite(maxDistance) || maxDistance <= 0f || !math.all(math.isfinite(direction)))
                return false;

            float3 safeCell = math.max(math.abs(cellSize), new float3(Epsilon));
            float3 gridSpan = safeCell * math.max((float3)(gridDimensions - new int3(1)), new float3(1f));
            float3 boundsMin = volumeOrigin;
            float3 boundsMax = volumeOrigin + gridSpan;
            float tMin = 0f;
            float tMax = maxDistance;
            if (!TryUpdateRaymarchAxisInterval(runtimeOrigin.x, direction.x, boundsMin.x, boundsMax.x, ref tMin, ref tMax) ||
                !TryUpdateRaymarchAxisInterval(runtimeOrigin.y, direction.y, boundsMin.y, boundsMax.y, ref tMin, ref tMax) ||
                !TryUpdateRaymarchAxisInterval(runtimeOrigin.z, direction.z, boundsMin.z, boundsMax.z, ref tMin, ref tMax))
            {
                return false;
            }

            startDistance = math.max(0f, tMin);
            endDistance = math.min(maxDistance, tMax);
            return math.isfinite(startDistance) &&
                   math.isfinite(endDistance) &&
                   endDistance >= startDistance;
        }

        private static bool TryUpdateRaymarchAxisInterval(
            float origin,
            float direction,
            float boundsMin,
            float boundsMax,
            ref float tMin,
            ref float tMax)
        {
            if (!math.isfinite(origin) ||
                !math.isfinite(direction) ||
                !math.isfinite(boundsMin) ||
                !math.isfinite(boundsMax))
            {
                return false;
            }

            float min = math.min(boundsMin, boundsMax);
            float max = math.max(boundsMin, boundsMax);
            if (math.abs(direction) <= Epsilon)
                return origin >= min && origin <= max;

            float inverseDirection = math.rcp(direction);
            float t0 = (min - origin) * inverseDirection;
            float t1 = (max - origin) * inverseDirection;
            float axisMin = math.min(t0, t1);
            float axisMax = math.max(t0, t1);
            tMin = math.max(tMin, axisMin);
            tMax = math.min(tMax, axisMax);
            return tMax >= tMin && tMax >= 0f;
        }

        private static float ResolveBoundedRaymarchStep(float maxDistance, float requestedStep)
        {
            float capStep = maxDistance * math.rcp(MaxEncodedRaymarchSteps);
            return math.max(requestedStep, math.isfinite(capStep) ? capStep : requestedStep);
        }

        private static int ResolveBoundedRaymarchStepCount(float maxDistance, float stepMeters)
        {
            float rawStepCount = math.ceil(maxDistance * math.rcp(math.max(MinimumRaymarchStepMeters, stepMeters))) + 1f;
            if (!math.isfinite(rawStepCount) || rawStepCount >= MaxEncodedRaymarchSteps)
                return MaxEncodedRaymarchSteps;

            if (rawStepCount <= 1f)
                return 1;

            return (int)rawStepCount;
        }

        private static bool TryResolveExpectedVoxelCount(int3 gridDimensions, out long expectedLength)
        {
            expectedLength = 0L;
            if (!math.all(gridDimensions > 1))
                return false;

            long x = gridDimensions.x;
            long y = gridDimensions.y;
            long z = gridDimensions.z;
            expectedLength = x * y * z;
            return expectedLength > 0L && expectedLength <= int.MaxValue;
        }
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

        /// <summary>
        /// Marks a promoted pooled ore proxy as depleted through its stable proxy id without exposing the concrete spawner type to interaction nodes.
        /// </summary>
        bool TryDepletePromotedProxy(string uniqueId, out uint oreHash, out uint itemHash, out float3 depletedPosition);

        /// <summary>
        /// Reports a completed scanner sweep to the unmanaged player ecosystem telemetry row.
        /// </summary>
        void ReportScannerSweepResult(int detectedOreCount, float sweptDistanceMeters, uint frame);
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
