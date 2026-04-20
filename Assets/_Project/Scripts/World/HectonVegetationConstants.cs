using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Canonical vegetation constants shared across the floating sargassum labyrinth runtime.
    /// This keeps gameplay queries and vegetation generation on the same Voronoi/warp contract.
    /// </summary>
    public static class HectonVegetationConstants
    {
        public const uint FloatingLabyrinthSamplingSeed = 0xC2B2AE35u;
        public const uint PrimaryVoronoiSalt = 0xB5297A4Du;
        public const uint SecondaryVoronoiSalt = 0x68E31DA4u;
        public const uint OccupancyVariationSalt = 0x4F1BBCDCu;
        public const uint WarpXSalt = 0xA511E9B3u;
        public const uint WarpZSalt = 0x63D83595u;
        public const uint SecondaryFeatureSalt = 0x9E3779B9u;
        public const uint PrimaryVariationSalt = 0x7F4A7C15u;

        public const float FloatingPatchNoiseScale = 0.012f;
        public const float FloatingPatchThreshold = 0.6f;
        public const float FloatingPrimaryCellSize = 18f;
        public const float FloatingSecondaryCellSize = 11f;
        public const float FloatingWallWidth = 2.4f;
        public const float FloatingWarpMeters = 6f;
        public const float FloatingFlowAnisotropy = 0.42f;

        public const float SargassumBiolumPhaseMultiplier = 0.92f;
        public const float BoidScooterPanicRadiusMultiplier = 3f;
        public const float BoidMassiveDisplacementPanicRadius = 56f;

        public static readonly Vector2 FloatingFlowDirection = new Vector2(0.92f, 0.38f);
    }
}
