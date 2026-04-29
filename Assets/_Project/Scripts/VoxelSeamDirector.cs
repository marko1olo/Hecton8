using Hecton8.Caves;
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
        /// Returns the terrain-conforming seam target height with a controlled voxel overlap.
        /// </summary>
        public static float ComputeTargetSnapHeight(float terrainHeight, float overlapMeters = TerrainOverlapMeters)
        {
            return terrainHeight + math.max(0f, overlapMeters);
        }

        /// <summary>
        /// Cave mouths are reserved for hard cliff contacts that actually request a carved blend.
        /// </summary>
        public static bool ShouldCreateCaveMouth(
            bool hasTerrainSample,
            float slopeDegrees,
            WorldGenerativeGeologyProfile.CaveBlendMode caveBlendMode)
        {
            if (!hasTerrainSample || slopeDegrees < CliffSlopeThresholdDegrees)
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
            float suggestedTerrainCut)
        {
            Vector3 inward = runtimeVolumeCenter - runtimeSurfacePosition;
            if (inward.sqrMagnitude <= 0.0001f)
                inward = Vector3.down;

            if (inward.y > -0.18f)
                inward.y = -0.18f;

            inward.Normalize();

            float baseRadius = Mathf.Max(seamBlendRadius * 0.24f, suggestedTerrainCut * 0.55f + 2f);
            float radius = Mathf.Clamp(baseRadius * Mathf.Lerp(0.94f, 1.12f, Mathf.Clamp01(blendWeight)), MinimumEntranceRadius, MaximumEntranceRadius);
            float funnelLength = Mathf.Clamp(Mathf.Max(radius * 2.6f, voxelSize.y * 0.34f), MinimumFunnelLength, MaximumFunnelLength);
            float innerRadius = Mathf.Clamp(radius * 0.62f, 1.5f, radius * 0.92f);

            return new CaveEntrance
            {
                surfacePosition = runtimeSurfacePosition,
                inwardDirection = inward,
                radius = radius,
                funnelLength = funnelLength,
                innerRadius = innerRadius
            };
        }
    }
}
