#if UNITY_EDITOR
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Editor.GeographySanity
{
    internal static unsafe class GeographySanitySampling
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToSectorLocal(double3 targetAup, GeographySectorDTO sector)
        {
            double3 delta = targetAup - sector.SectorOriginAup;
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleHeightBilinear(float2 localXZ, GeographySectorDTO sector, float* heights)
        {
            int width = math.max(2, sector.HeightResolution);
            float invSize = math.rcp(math.max(1f, sector.SectorSizeMeters));
            float sx = math.saturate(localXZ.x * invSize) * (width - 1);
            float sz = math.saturate(localXZ.y * invSize) * (width - 1);
            int x0 = (int)math.floor(sx);
            int z0 = (int)math.floor(sz);
            int x1 = math.min(x0 + 1, width - 1);
            int z1 = math.min(z0 + 1, width - 1);
            float tx = sx - x0;
            float tz = sz - z0;
            float h00 = heights[x0 + z0 * width];
            float h10 = heights[x1 + z0 * width];
            float h01 = heights[x0 + z1 * width];
            float h11 = heights[x1 + z1 * width];
            return math.lerp(math.lerp(h00, h10, tx), math.lerp(h01, h11, tx), tz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleHeightQuality(float2 localXZ, GeographySectorDTO sector, float* heights)
        {
            float blend = QualityInterpolationWeight(sector);
            float nearest = SampleHeightNearest(localXZ, sector, heights);
            if (blend <= 0.0001f)
                return nearest;

            float bilinear = SampleHeightBilinear(localXZ, sector, heights);
            if (blend >= 0.9999f)
                return bilinear;

            return math.lerp(nearest, bilinear, blend);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleSdfQuality(float3 local, GeographySectorDTO sector, float* sdf)
        {
            float blend = QualityInterpolationWeight(sector);
            float nearest = SampleSdfNearest(local, sector, sdf);
            if (blend <= 0.0001f)
                return nearest;

            float trilinear = SampleSdfTrilinear(local, sector, sdf);
            if (blend >= 0.9999f)
                return trilinear;

            return math.lerp(nearest, trilinear, blend);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleSdfTrilinear(float3 local, GeographySectorDTO sector, float* sdf)
        {
            int rx = math.max(2, sector.SdfResolutionX);
            int ry = math.max(2, sector.SdfResolutionY);
            int rz = math.max(2, sector.SdfResolutionZ);
            float invSize = math.rcp(math.max(1f, sector.SectorSizeMeters));
            float invY = math.rcp(math.max(1f, sector.SdfSizeYMeters));
            float sx = math.saturate(local.x * invSize) * (rx - 1);
            float sy = math.saturate((local.y - sector.SdfMinYLocalMeters) * invY) * (ry - 1);
            float sz = math.saturate(local.z * invSize) * (rz - 1);
            int x0 = (int)math.floor(sx);
            int y0 = (int)math.floor(sy);
            int z0 = (int)math.floor(sz);
            int x1 = math.min(x0 + 1, rx - 1);
            int y1 = math.min(y0 + 1, ry - 1);
            int z1 = math.min(z0 + 1, rz - 1);
            float tx = sx - x0;
            float ty = sy - y0;
            float tz = sz - z0;
            float c000 = SampleSdfNearest(x0, y0, z0, rx, ry, sdf);
            float c100 = SampleSdfNearest(x1, y0, z0, rx, ry, sdf);
            float c010 = SampleSdfNearest(x0, y1, z0, rx, ry, sdf);
            float c110 = SampleSdfNearest(x1, y1, z0, rx, ry, sdf);
            float c001 = SampleSdfNearest(x0, y0, z1, rx, ry, sdf);
            float c101 = SampleSdfNearest(x1, y0, z1, rx, ry, sdf);
            float c011 = SampleSdfNearest(x0, y1, z1, rx, ry, sdf);
            float c111 = SampleSdfNearest(x1, y1, z1, rx, ry, sdf);
            float x00 = math.lerp(c000, c100, tx);
            float x10 = math.lerp(c010, c110, tx);
            float x01 = math.lerp(c001, c101, tx);
            float x11 = math.lerp(c011, c111, tx);
            return math.lerp(math.lerp(x00, x10, ty), math.lerp(x01, x11, ty), tz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SampleSdfGradient(float3 local, GeographySectorDTO sector, float* sdf)
        {
            float h = math.max(0.05f, sector.SdfVoxelSizeMeters);
            float dx = SampleSdfQuality(local + new float3(h, 0f, 0f), sector, sdf) -
                       SampleSdfQuality(local - new float3(h, 0f, 0f), sector, sdf);
            float dy = SampleSdfQuality(local + new float3(0f, h, 0f), sector, sdf) -
                       SampleSdfQuality(local - new float3(0f, h, 0f), sector, sdf);
            float dz = SampleSdfQuality(local + new float3(0f, 0f, h), sector, sdf) -
                       SampleSdfQuality(local - new float3(0f, 0f, h), sector, sdf);
            return NormalizeOrFallback(new float3(dx, dy, dz), new float3(0f, 1f, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float value)
        {
            return math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double value)
        {
            return math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return IsFinite(value) && lenSq > 1e-12f ? value * math.rsqrt(lenSq) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleSdfNearest(int x, int y, int z, int rx, int ry, float* sdf)
        {
            return sdf[x + y * rx + z * rx * ry];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleHeightNearest(float2 localXZ, GeographySectorDTO sector, float* heights)
        {
            int width = math.max(2, sector.HeightResolution);
            float invSize = math.rcp(math.max(1f, sector.SectorSizeMeters));
            int x = (int)math.round(math.saturate(localXZ.x * invSize) * (width - 1));
            int z = (int)math.round(math.saturate(localXZ.y * invSize) * (width - 1));
            x = math.clamp(x, 0, width - 1);
            z = math.clamp(z, 0, width - 1);
            return heights[x + z * width];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleSdfNearest(float3 local, GeographySectorDTO sector, float* sdf)
        {
            int rx = math.max(2, sector.SdfResolutionX);
            int ry = math.max(2, sector.SdfResolutionY);
            int rz = math.max(2, sector.SdfResolutionZ);
            float invSize = math.rcp(math.max(1f, sector.SectorSizeMeters));
            float invY = math.rcp(math.max(1f, sector.SdfSizeYMeters));
            int x = (int)math.round(math.saturate(local.x * invSize) * (rx - 1));
            int y = (int)math.round(math.saturate((local.y - sector.SdfMinYLocalMeters) * invY) * (ry - 1));
            int z = (int)math.round(math.saturate(local.z * invSize) * (rz - 1));
            x = math.clamp(x, 0, rx - 1);
            y = math.clamp(y, 0, ry - 1);
            z = math.clamp(z, 0, rz - 1);
            return SampleSdfNearest(x, y, z, rx, ry, sdf);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float QualityInterpolationWeight(GeographySectorDTO sector)
        {
            float q = math.saturate(sector.GlobalQualityWeight);
            return math.smoothstep(0.25f, 0.85f, q);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ApplySanityProfilesJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialEntityDTO* Entities;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SanityProfileDTO* Profiles;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialAnomalyRuleDTO* Rules;

        public int EntityCount;
        public int ProfileCount;

        public void Execute(int index)
        {
            if (index >= EntityCount || ProfileCount <= 0)
                return;

            ref SpatialEntityDTO entity = ref UnsafeUtility.AsRef<SpatialEntityDTO>(Entities + index);
            if (entity.SourceFlags == 0u)
                return;

            for (int i = 0; i < ProfileCount; i++)
            {
                ref readonly SanityProfileDTO profile = ref UnsafeUtility.AsRef<SanityProfileDTO>(Profiles + i);
                if (profile.ObjectTypeHash != entity.ObjectTypeHash)
                    continue;

                entity.MaxFloatingDistance = profile.MaxFloatingDistance;
                entity.RequiredClearance = profile.RequiredClearance;
                entity.RecoverableEpsilon = profile.RecoverableEpsilon;
                entity.RuleFlags = profile.RuleFlags;
                ref SpatialAnomalyRuleDTO rule = ref UnsafeUtility.AsRef<SpatialAnomalyRuleDTO>(Rules + index);
                rule.RequiredClearance = profile.RequiredClearance;
                rule.RuleFlags = profile.RuleFlags;
                return;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct EvaluateFloatingAnomaliesJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialEntityDTO* Entities;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* HeightSamples;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* SdfSamples;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialAnomalyResultDTO* Results;

        public GeographySectorDTO Sector;
        public int EntityCount;

        public void Execute(int index)
        {
            if (index >= EntityCount)
                return;

            ref SpatialEntityDTO entity = ref UnsafeUtility.AsRef<SpatialEntityDTO>(Entities + index);
            if ((entity.RuleFlags & GeographySanityConstants.RuleCheckFloating) == 0u)
                return;

            ref SpatialAnomalyResultDTO result = ref UnsafeUtility.AsRef<SpatialAnomalyResultDTO>(Results + index);
            result.TargetAUP = entity.TargetAUP;
            result.EntityHash = entity.EntityHash;
            result.ObjectTypeHash = entity.ObjectTypeHash;
            result.HullMaterialHash = entity.HullMaterialHash;
            result.SectorX = Sector.SectorX;
            result.SectorZ = Sector.SectorZ;

            if (!GeographySanitySampling.IsFinite(entity.RadiusMeters) ||
                !GeographySanitySampling.IsFinite(entity.RequiredClearance) ||
                !GeographySanitySampling.IsFinite(entity.MaxFloatingDistance) ||
                !GeographySanitySampling.IsFinite(entity.RecoverableEpsilon) ||
                entity.RadiusMeters <= 0f ||
                entity.RequiredClearance < 0f ||
                entity.MaxFloatingDistance < 0f ||
                entity.RecoverableEpsilon < 0f)
            {
                result.ErrorFlags |= GeographySanityConstants.ResultFatalMath;
                return;
            }

            float3 local = GeographySanitySampling.ToSectorLocal(entity.TargetAUP, Sector);
            if (!GeographySanitySampling.IsFinite(local))
            {
                result.ErrorFlags |= GeographySanityConstants.ResultFatalMath;
                return;
            }

            float height = GeographySanitySampling.SampleHeightQuality(local.xz, Sector, HeightSamples);
            float sdfAtPoint = GeographySanitySampling.SampleSdfQuality(local, Sector, SdfSamples);
            if (!GeographySanitySampling.IsFinite(height) || !GeographySanitySampling.IsFinite(sdfAtPoint))
            {
                result.ErrorFlags |= GeographySanityConstants.ResultFatalMath;
                return;
            }

            float surfaceY = height;
            float probeStartY = local.y - math.max(0f, entity.RadiusMeters);
            float step = math.max(0.05f, Sector.VerticalProbeStepMeters);
            int steps = math.max(1, Sector.VerticalProbeSteps);
            for (int i = 0; i <= steps; i++)
            {
                float y = probeStartY - (i * step);
                if (y < Sector.SdfMinYLocalMeters)
                    break;

                float d = GeographySanitySampling.SampleSdfQuality(new float3(local.x, y, local.z), Sector, SdfSamples);
                if (!GeographySanitySampling.IsFinite(d))
                {
                    result.ErrorFlags |= GeographySanityConstants.ResultFatalMath;
                    return;
                }

                if (d <= entity.RequiredClearance)
                {
                    surfaceY = math.max(surfaceY, y);
                    break;
                }
            }

            float clearance = probeStartY - surfaceY - math.max(0f, entity.RequiredClearance);
            result.SdfMeters = sdfAtPoint;
            result.HeightMeters = height;
            result.ClearanceMeters = clearance;

            float allowed = math.max(entity.MaxFloatingDistance, Sector.MaxFloatingDistance);
            if (sdfAtPoint > -entity.RadiusMeters && clearance > allowed)
            {
                result.ErrorFlags |= GeographySanityConstants.ResultFloating;
                result.SuggestedCorrectionMeters = new float3(0f, -clearance, 0f);
                if (clearance <= entity.RecoverableEpsilon)
                    result.ErrorFlags |= GeographySanityConstants.ResultRecoverable;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct EvaluateBuriedAnomaliesJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialEntityDTO* Entities;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* SdfSamples;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialAnomalyResultDTO* Results;

        public GeographySectorDTO Sector;
        public int EntityCount;

        public void Execute(int index)
        {
            if (index >= EntityCount)
                return;

            ref SpatialEntityDTO entity = ref UnsafeUtility.AsRef<SpatialEntityDTO>(Entities + index);
            if ((entity.RuleFlags & GeographySanityConstants.RuleCheckBuried) == 0u)
                return;

            ref SpatialAnomalyResultDTO result = ref UnsafeUtility.AsRef<SpatialAnomalyResultDTO>(Results + index);
            result.TargetAUP = entity.TargetAUP;
            result.EntityHash = entity.EntityHash;
            result.ObjectTypeHash = entity.ObjectTypeHash;
            result.HullMaterialHash = entity.HullMaterialHash;
            result.SectorX = Sector.SectorX;
            result.SectorZ = Sector.SectorZ;

            if (!GeographySanitySampling.IsFinite(entity.RadiusMeters) ||
                !GeographySanitySampling.IsFinite(entity.RequiredClearance) ||
                !GeographySanitySampling.IsFinite(entity.RecoverableEpsilon) ||
                entity.RadiusMeters <= 0f ||
                entity.RequiredClearance < 0f ||
                entity.RecoverableEpsilon < 0f)
            {
                result.ErrorFlags |= GeographySanityConstants.ResultFatalMath;
                return;
            }

            float3 local = GeographySanitySampling.ToSectorLocal(entity.TargetAUP, Sector);
            if (!GeographySanitySampling.IsFinite(local))
            {
                result.ErrorFlags |= GeographySanityConstants.ResultFatalMath;
                return;
            }

            float sdf = GeographySanitySampling.SampleSdfQuality(local, Sector, SdfSamples);
            if (!GeographySanitySampling.IsFinite(sdf))
            {
                result.ErrorFlags |= GeographySanityConstants.ResultFatalMath;
                return;
            }

            result.SdfMeters = sdf;

            float radius = math.max(0.01f, entity.RadiusMeters);
            if (sdf < -radius)
            {
                float penetration = -sdf + radius + math.max(0f, entity.RequiredClearance);
                result.ErrorFlags |= GeographySanityConstants.ResultBuried;
                result.SuggestedCorrectionMeters = GeographySanitySampling.SampleSdfGradient(local, Sector, SdfSamples) * penetration;
                if (penetration <= entity.RecoverableEpsilon)
                    result.ErrorFlags |= GeographySanityConstants.ResultRecoverable;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ValidateCrushDepthLimitsJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialEntityDTO* Entities;
        [NoAlias, NativeDisableUnsafePtrRestriction] public CrushDepthMaterialDTO* Materials;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialAnomalyResultDTO* Results;

        public int EntityCount;
        public int MaterialCount;

        public void Execute(int index)
        {
            if (index >= EntityCount)
                return;

            ref SpatialEntityDTO entity = ref UnsafeUtility.AsRef<SpatialEntityDTO>(Entities + index);
            if ((entity.RuleFlags & GeographySanityConstants.RuleCheckCrushDepth) == 0u)
                return;

            ref SpatialAnomalyResultDTO result = ref UnsafeUtility.AsRef<SpatialAnomalyResultDTO>(Results + index);
            result.TargetAUP = entity.TargetAUP;
            result.EntityHash = entity.EntityHash;
            result.ObjectTypeHash = entity.ObjectTypeHash;
            result.HullMaterialHash = entity.HullMaterialHash;

            if (!GeographySanitySampling.IsFinite(entity.TargetAUP))
            {
                result.ErrorFlags |= GeographySanityConstants.ResultFatalMath;
                return;
            }

            float limit = ResolveCrushDepth(entity.HullMaterialHash);
            if (!GeographySanitySampling.IsFinite(limit))
            {
                result.ErrorFlags |= GeographySanityConstants.ResultFatalMath;
                return;
            }

            if (limit <= 0f)
                return;

            float depth = (float)math.max(0.0, -entity.TargetAUP.y);
            result.ActualDepthMeters = depth;
            result.CrushDepthLimitMeters = limit;
            if (depth > limit)
                result.ErrorFlags |= GeographySanityConstants.ResultCrushDepth;
        }

        private float ResolveCrushDepth(uint hash)
        {
            for (int i = 0; i < MaterialCount; i++)
            {
                ref CrushDepthMaterialDTO material = ref UnsafeUtility.AsRef<CrushDepthMaterialDTO>(Materials + i);
                if (material.HullMaterialHash == hash)
                    return material.CrushDepthMeters;
            }

            return 0f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct EvaluateNavigationalConnectivityJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public NavigationRequestDTO* Requests;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float* SdfSamples;
        [NoAlias, NativeDisableUnsafePtrRestriction] public int* Scratch;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SpatialAnomalyResultDTO* Results;

        public GeographySectorDTO Sector;
        public int RequestCount;
        public int GridResolution;
        public int ScratchIntsPerRequest;

        public void Execute(int index)
        {
            if (index >= RequestCount)
                return;

            int resolution = math.clamp(GridResolution, 4, GeographySanityConstants.MaximumConnectivityResolution);
            int cellCount = resolution * resolution * resolution;
            ref NavigationRequestDTO request = ref UnsafeUtility.AsRef<NavigationRequestDTO>(Requests + index);
            if ((request.RuleFlags & GeographySanityConstants.RuleCheckConnectivity) == 0u)
                return;

            int* queue = Scratch + index * ScratchIntsPerRequest;
            int* visited = queue + cellCount;
            for (int i = 0; i < cellCount; i++)
                visited[i] = 0;

            ref SpatialAnomalyResultDTO result = ref UnsafeUtility.AsRef<SpatialAnomalyResultDTO>(Results + index);
            result.TargetAUP = request.StartAUP;
            result.RequestHash = request.RequestHash;
            result.EntityHash = request.RequestHash;
            result.SectorX = Sector.SectorX;
            result.SectorZ = Sector.SectorZ;

            if (!GeographySanitySampling.IsFinite(request.VehicleRadiusMeters) ||
                !GeographySanitySampling.IsFinite(request.RequiredClearance) ||
                request.VehicleRadiusMeters <= 0f ||
                request.RequiredClearance < 0f)
            {
                result.ErrorFlags |= GeographySanityConstants.ResultFatalMath;
                return;
            }

            float3 startLocal = GeographySanitySampling.ToSectorLocal(request.StartAUP, Sector);
            float3 endLocal = GeographySanitySampling.ToSectorLocal(request.EndAUP, Sector);
            if (!GeographySanitySampling.IsFinite(startLocal) || !GeographySanitySampling.IsFinite(endLocal))
            {
                result.ErrorFlags |= GeographySanityConstants.ResultFatalMath;
                return;
            }

            int start = ToCell(startLocal, resolution);
            int goal = ToCell(endLocal, resolution);
            if (!IsOpen(start, resolution, request) || !IsOpen(goal, resolution, request))
            {
                result.ErrorFlags |= GeographySanityConstants.ResultNavigationTrap;
                return;
            }

            int head = 0;
            int tail = 0;
            queue[tail++] = start;
            visited[start] = 1;
            bool reached = false;
            while (head < tail)
            {
                int current = queue[head++];
                if (current == goal)
                {
                    reached = true;
                    break;
                }

                TryPushNeighbor(current, 1, 0, 0, resolution, request, queue, visited, ref tail);
                TryPushNeighbor(current, -1, 0, 0, resolution, request, queue, visited, ref tail);
                TryPushNeighbor(current, 0, 1, 0, resolution, request, queue, visited, ref tail);
                TryPushNeighbor(current, 0, -1, 0, resolution, request, queue, visited, ref tail);
                TryPushNeighbor(current, 0, 0, 1, resolution, request, queue, visited, ref tail);
                TryPushNeighbor(current, 0, 0, -1, resolution, request, queue, visited, ref tail);
            }

            if (!reached)
                result.ErrorFlags |= GeographySanityConstants.ResultNavigationTrap;
        }

        private void TryPushNeighbor(
            int current,
            int dx,
            int dy,
            int dz,
            int resolution,
            NavigationRequestDTO request,
            int* queue,
            int* visited,
            ref int tail)
        {
            int3 c = FromCell(current, resolution);
            int3 n = c + new int3(dx, dy, dz);
            if (math.any(n < int3.zero) || math.any(n >= new int3(resolution)))
                return;

            int next = n.x + n.y * resolution + n.z * resolution * resolution;
            if (visited[next] != 0 || !IsOpen(next, resolution, request))
                return;

            visited[next] = 1;
            queue[tail++] = next;
        }

        private bool IsOpen(int cell, int resolution, NavigationRequestDTO request)
        {
            float3 local = CellCenter(cell, resolution);
            float sdf = GeographySanitySampling.SampleSdfQuality(local, Sector, SdfSamples);
            float clearance = math.max(0.01f, request.VehicleRadiusMeters) + math.max(0f, request.RequiredClearance);
            return GeographySanitySampling.IsFinite(sdf) && sdf >= clearance;
        }

        private int ToCell(float3 local, int resolution)
        {
            float inv = math.rcp(math.max(1f, Sector.SectorSizeMeters));
            float invY = math.rcp(math.max(1f, Sector.SdfSizeYMeters));
            int x = (int)math.floor(math.saturate(local.x * inv) * resolution);
            int y = (int)math.floor(math.saturate((local.y - Sector.SdfMinYLocalMeters) * invY) * resolution);
            int z = (int)math.floor(math.saturate(local.z * inv) * resolution);
            x = math.clamp(x, 0, resolution - 1);
            y = math.clamp(y, 0, resolution - 1);
            z = math.clamp(z, 0, resolution - 1);
            return x + y * resolution + z * resolution * resolution;
        }

        private float3 CellCenter(int cell, int resolution)
        {
            int3 c = FromCell(cell, resolution);
            float stepXZ = Sector.SectorSizeMeters * math.rcp(resolution);
            float stepY = Sector.SdfSizeYMeters * math.rcp(resolution);
            return new float3(
                (c.x + 0.5f) * stepXZ,
                Sector.SdfMinYLocalMeters + (c.y + 0.5f) * stepY,
                (c.z + 0.5f) * stepXZ);
        }

        private static int3 FromCell(int cell, int resolution)
        {
            int plane = resolution * resolution;
            int z = cell / plane;
            int rem = cell - z * plane;
            int y = rem / resolution;
            int x = rem - y * resolution;
            return new int3(x, y, z);
        }
    }
}
#endif
