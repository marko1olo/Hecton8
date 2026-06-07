using Hecton8.Caves;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Central seam math contract for voxel-to-terrain stitching.
    /// Keeps AUP seam distance, overlap, and cave-mouth heuristics in one owner.
    /// </summary>
    public static class VoxelSeamDirector
    {
        public const float TerrainOverlapMeters = 0.10f;
        public const float SeamTransitionBandMeters = 3.5f;
        public const float CliffSlopeThresholdDegrees = 60f;

        private const float MinimumEntranceRadius = 2.4f;
        private const float MaximumEntranceRadius = 6.5f;
        private const float MinimumFunnelLength = 6f;
        private const float MaximumFunnelLength = 18f;

        /// <summary>
        /// Returns the in-volume distance to the nearest XZ boundary in the captured AUP frame.
        /// Zero means the vertex lies on or outside the seam edge.
        /// </summary>
        public static float ComputeBoundaryDistance(
            float2 absoluteWorldXZ,
            float3 absoluteVolumeOrigin,
            int ptsX,
            int ptsZ,
            float voxelStep)
        {
            float maxX = absoluteVolumeOrigin.x + math.max(0f, (ptsX - 1) * voxelStep);
            float maxZ = absoluteVolumeOrigin.z + math.max(0f, (ptsZ - 1) * voxelStep);

            float distanceMinX = absoluteWorldXZ.x - absoluteVolumeOrigin.x;
            float distanceMaxX = maxX - absoluteWorldXZ.x;
            float distanceMinZ = absoluteWorldXZ.y - absoluteVolumeOrigin.z;
            float distanceMaxZ = maxZ - absoluteWorldXZ.y;

            return math.max(0f, math.min(math.min(distanceMinX, distanceMaxX), math.min(distanceMinZ, distanceMaxZ)));
        }

        /// <summary>
        /// Returns a normalized seam blend where 1 = boundary lock and 0 = interior voxel surface.
        /// </summary>
        public static float ComputeBoundaryBlend01(float boundaryDistance, float transitionBand)
        {
            if (transitionBand <= 0f)
                return 0f;

            return 1f - math.saturate(boundaryDistance / transitionBand);
        }

        /// <summary>
        /// Returns signed terrain density. Zero is the exact MapMagic/voxel handoff plane.
        /// Positive values are solid terrain below the seafloor; negative values are open space above it.
        /// </summary>
        public static float ComputeTerrainDensity(float terrainHeight, float sampleHeight)
        {
            return math.clamp(terrainHeight - sampleHeight, -50f, 50f);
        }

        /// <summary>
        /// Returns the terrain-conforming seam target height. The overlap argument is kept for
        /// legacy call compatibility; density ownership snaps to zero at the terrain plane.
        /// </summary>
        public static float ComputeTargetSnapHeight(float terrainHeight, float overlapMeters = TerrainOverlapMeters)
        {
            return terrainHeight;
        }

        /// <summary>
        /// Samples the MapMagic-owned terrain normal for cave-mouth SDF blending.
        /// Falls back to up when terrain data is not resident.
        /// </summary>
        public static Vector3 ResolveTerrainNormalAtSeam(Vector3 absoluteUniversePosition, float seamBlendRadius)
        {
            if (!IsFinite(absoluteUniversePosition))
                return Vector3.up;

            MapMagicBridge mapMagicBridge = MapMagicBridge.Instance;
            if (mapMagicBridge == null)
                return Vector3.up;

            float safeSeamBlendRadius = ClampFinite(seamBlendRadius, MinimumFunnelLength, 0f, MaximumFunnelLength);
            float sampleDistance = Mathf.Clamp(safeSeamBlendRadius * 0.18f, 1f, 4f);
            return mapMagicBridge.TryGetNormalAUP(absoluteUniversePosition, sampleDistance, out Vector3 terrainNormal) &&
                   IsFinite(terrainNormal)
                ? terrainNormal
                : Vector3.up;
        }

        public static bool ResolveTerrainSplatColorAtSeam(Vector3 absoluteUniversePosition, out Color terrainColor, out float blend)
        {
            terrainColor = Color.clear;
            blend = 0f;
            if (!IsFinite(absoluteUniversePosition))
                return false;

            MapMagicBridge mapMagicBridge = MapMagicBridge.Instance;
            if (mapMagicBridge == null)
                return false;

            if (!mapMagicBridge.TryGetTerrainSplatColorAUP(absoluteUniversePosition, out terrainColor, out float confidence))
                return false;

            terrainColor = SanitizeColor(terrainColor, Color.clear);
            blend = SaturateFinite(confidence);
            return blend > 0.0001f;
        }

        public static float ResolveTerrainVoxelSnapStep(Vector3 voxelVolumeSize, float fallbackRadiusMeters)
        {
            Vector3 safeVoxelVolumeSize = IsFinite(voxelVolumeSize) ? voxelVolumeSize : Vector3.zero;
            float dominantSize = math.max(
                math.max(math.abs(safeVoxelVolumeSize.x), math.abs(safeVoxelVolumeSize.y)),
                math.abs(safeVoxelVolumeSize.z));
            float fallbackSize = math.max(1f, ClampFinite(fallbackRadiusMeters, MinimumEntranceRadius, 0.5f, MaximumEntranceRadius) * 2f);
            float size = dominantSize > 0.001f ? dominantSize : fallbackSize;
            return math.clamp(size / 64f, 0.125f, 2f);
        }

        public static double SnapAbsoluteHeightToVoxelLayer(
            double absoluteHeight,
            double absoluteVolumeOriginY,
            float voxelStepMeters)
        {
            double safeStep = math.max(0.0001f, voxelStepMeters);
            double normalized = (absoluteHeight - absoluteVolumeOriginY) / safeStep;
            return absoluteVolumeOriginY + System.Math.Round(normalized) * safeStep;
        }

        /// <summary>
        /// Cave mouths are reserved for hard cliff contacts that actually request a carved blend.
        /// </summary>
        public static bool ShouldCreateCaveMouth(
            bool hasTerrainSample,
            float slopeDegrees,
            WorldGenerativeGeologyProfile.CaveBlendMode caveBlendMode)
        {
            if (!hasTerrainSample || !math.isfinite(slopeDegrees) || slopeDegrees < CliffSlopeThresholdDegrees)
                return false;

            return caveBlendMode == WorldGenerativeGeologyProfile.CaveBlendMode.SdfBlend
                || caveBlendMode == WorldGenerativeGeologyProfile.CaveBlendMode.CarvePortal;
        }

        /// <summary>
        /// Builds a single deterministic cave-mouth funnel from the terrain contact into the voxel core.
        /// </summary>
        public static CaveEntrance BuildCaveEntrance(
            Vector3 runtimeSurfacePosition,
            Vector3 runtimeVolumeCenter,
            Vector3 voxelSize,
            float blendWeight,
            float seamBlendRadius,
            float suggestedTerrainCut,
            Vector3 terrainNormal = default,
            Vector3 absoluteTerrainContactPosition = default)
        {
            Vector3 safeSurfacePosition = IsFinite(runtimeSurfacePosition) ? runtimeSurfacePosition : Vector3.zero;
            Vector3 safeVolumeCenter = IsFinite(runtimeVolumeCenter)
                ? runtimeVolumeCenter
                : safeSurfacePosition + (Vector3.down * MinimumFunnelLength);
            float safeVoxelY = ClampFinite(math.abs(voxelSize.y), MinimumFunnelLength, 0.1f, 256f);
            float safeBlendWeight = SaturateFinite(blendWeight);
            float safeSeamBlendRadius = ClampFinite(seamBlendRadius, MinimumEntranceRadius, 0f, MaximumEntranceRadius * 4f);
            float safeTerrainCut = ClampFinite(suggestedTerrainCut, MinimumEntranceRadius, 0f, MaximumEntranceRadius * 4f);

            Vector3 inward = safeVolumeCenter - safeSurfacePosition;
            float inwardSq = IsFinite(inward) ? inward.sqrMagnitude : 0f;
            if (!math.isfinite(inwardSq) || inwardSq <= 0.0001f)
                inward = Vector3.down;

            if (inward.y > -0.18f)
                inward.y = -0.18f;

            inward.Normalize();

            float baseRadius = Mathf.Max(safeSeamBlendRadius * 0.24f, safeTerrainCut * 0.55f + 2f);
            float radius = Mathf.Clamp(baseRadius * Mathf.Lerp(0.94f, 1.12f, safeBlendWeight), MinimumEntranceRadius, MaximumEntranceRadius);
            float funnelLength = Mathf.Clamp(Mathf.Max(radius * 2.6f, safeVoxelY * 0.34f), MinimumFunnelLength, MaximumFunnelLength);
            float innerRadius = Mathf.Clamp(radius * 0.62f, 1.5f, radius * 0.92f);
            float terrainNormalSq = IsFinite(terrainNormal) ? terrainNormal.sqrMagnitude : 0f;
            if (!math.isfinite(terrainNormalSq))
                terrainNormalSq = 0f;
            Vector3 safeTerrainNormal = terrainNormalSq > 0.0001f ? terrainNormal.normalized : Vector3.up;
            float terrainNormalBlend = terrainNormalSq > 0.0001f ? safeBlendWeight : 0f;
            Color terrainSplatColor = Color.clear;
            float terrainSplatBlend = 0f;
            if (IsFinite(absoluteTerrainContactPosition) &&
                absoluteTerrainContactPosition.sqrMagnitude > 0.0001f &&
                ResolveTerrainSplatColorAtSeam(absoluteTerrainContactPosition, out Color sampledSplatColor, out float sampledBlend))
            {
                terrainSplatColor = SanitizeColor(sampledSplatColor, Color.clear);
                terrainSplatBlend = sampledBlend;
            }

            return new CaveEntrance
            {
                surfacePosition = safeSurfacePosition,
                inwardDirection = inward,
                radius = radius,
                funnelLength = funnelLength,
                innerRadius = innerRadius,
                terrainNormal = safeTerrainNormal,
                terrainNormalBlend = terrainNormalBlend,
                terrainSplatColor = new float4(
                    terrainSplatColor.r,
                    terrainSplatColor.g,
                    terrainSplatColor.b,
                    terrainSplatColor.a),
                terrainSplatBlend = terrainSplatBlend
            };
        }

        private static float ClampFinite(float value, float fallback, float minimum, float maximum)
        {
            float safeFallback = math.select(minimum, fallback, math.isfinite(fallback));
            float safeValue = math.select(safeFallback, value, math.isfinite(value));
            return math.clamp(safeValue, minimum, maximum);
        }

        private static float SaturateFinite(float value)
        {
            return math.saturate(math.select(0f, value, math.isfinite(value)));
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static Color SanitizeColor(Color value, Color fallback)
        {
            return math.isfinite(value.r) &&
                   math.isfinite(value.g) &&
                   math.isfinite(value.b) &&
                   math.isfinite(value.a)
                ? value
                : fallback;
        }
    }
}
