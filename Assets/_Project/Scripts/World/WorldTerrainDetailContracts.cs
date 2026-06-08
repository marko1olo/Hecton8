using System.Runtime.CompilerServices;

namespace Hecton8.World
{
    public enum WorldTerrainDetailTier : byte
    {
        NearPlayable = 0,
        MidTraversal = 1,
        FarSilhouette = 2,
        DistantHlod = 3
    }

    public readonly struct WorldTerrainDetailTierInfo
    {
        public readonly WorldTerrainDetailTier Tier;
        public readonly int HeightResolution;
        public readonly float SamplePitchMeters;
        public readonly float MaxDistanceMeters;
        public readonly uint EnabledControlMaps;

        public WorldTerrainDetailTierInfo(
            WorldTerrainDetailTier tier,
            int heightResolution,
            float samplePitchMeters,
            float maxDistanceMeters,
            uint enabledControlMaps)
        {
            Tier = tier;
            HeightResolution = heightResolution;
            SamplePitchMeters = samplePitchMeters;
            MaxDistanceMeters = maxDistanceMeters;
            EnabledControlMaps = enabledControlMaps;
        }
    }

    public static class WorldTerrainControlMapFlags
    {
        public const uint None = 0u;
        public const uint MacroHeight = 1u << 0;
        public const uint Slope = 1u << 1;
        public const uint Curvature = 1u << 2;
        public const uint ErosionFlow = 1u << 3;
        public const uint Terrace = 1u << 4;
        public const uint Slump = 1u << 5;
        public const uint Tributary = 1u << 6;
        public const uint Sediment = 1u << 7;
        public const uint HardRock = 1u << 8;
        public const uint Nodule = 1u << 9;
        public const uint ReefEligibility = 1u << 10;
        public const uint VoxelSeam = 1u << 11;
        public const uint MaterialRegion = 1u << 12;
        public const uint All =
            MacroHeight |
            Slope |
            Curvature |
            ErosionFlow |
            Terrace |
            Slump |
            Tributary |
            Sediment |
            HardRock |
            Nodule |
            ReefEligibility |
            VoxelSeam |
            MaterialRegion;
    }

    public static class WorldTerrainDetailContracts
    {
        public const uint ContractVersion = 1u;
        public const float AuthoredProofExtentMeters = WorldMacroGeologyFields.MinimumWorldExtentMeters;
        public const float RuntimeChunkSizeMeters = WorldMacroGeologyFields.DefaultChunkSizeMeters;
        public const float SpawnProofExtentMeters = 10_000f;
        public const float MesoMesoProofExtentMeters = 1_000f;
        public const float MesoProofExtentMeters = 512f;
        public const float MicroProofExtentMeters = 100f;
        public const float PrimaryShallowDepthMeters = 50f;
        public const float TransitionShallowDepthMeters = 100f;
        public const float UpperPlayableDepthMeters = 500f;
        public const float TargetMaxSingleIslandSquareKilometers = 1f;
        public const float HardMaxSingleIslandSquareKilometers = 2f;

        public static WorldTerrainDetailTierInfo NearPlayable => new WorldTerrainDetailTierInfo(
            WorldTerrainDetailTier.NearPlayable,
            513,
            1f,
            768f,
            WorldTerrainControlMapFlags.All);

        public static WorldTerrainDetailTierInfo MidTraversal => new WorldTerrainDetailTierInfo(
            WorldTerrainDetailTier.MidTraversal,
            257,
            2f,
            2_048f,
            WorldTerrainControlMapFlags.MacroHeight |
            WorldTerrainControlMapFlags.Slope |
            WorldTerrainControlMapFlags.Curvature |
            WorldTerrainControlMapFlags.ErosionFlow |
            WorldTerrainControlMapFlags.Terrace |
            WorldTerrainControlMapFlags.Slump |
            WorldTerrainControlMapFlags.Tributary |
            WorldTerrainControlMapFlags.MaterialRegion |
            WorldTerrainControlMapFlags.VoxelSeam);

        public static WorldTerrainDetailTierInfo FarSilhouette => new WorldTerrainDetailTierInfo(
            WorldTerrainDetailTier.FarSilhouette,
            129,
            4f,
            6_144f,
            WorldTerrainControlMapFlags.MacroHeight |
            WorldTerrainControlMapFlags.Slope |
            WorldTerrainControlMapFlags.MaterialRegion |
            WorldTerrainControlMapFlags.VoxelSeam);

        public static WorldTerrainDetailTierInfo DistantHlod => new WorldTerrainDetailTierInfo(
            WorldTerrainDetailTier.DistantHlod,
            65,
            8f,
            24_576f,
            WorldTerrainControlMapFlags.MacroHeight |
            WorldTerrainControlMapFlags.MaterialRegion);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldTerrainDetailTier ResolveTier(float distanceMeters)
        {
            if (distanceMeters <= NearPlayable.MaxDistanceMeters)
                return WorldTerrainDetailTier.NearPlayable;
            if (distanceMeters <= MidTraversal.MaxDistanceMeters)
                return WorldTerrainDetailTier.MidTraversal;
            if (distanceMeters <= FarSilhouette.MaxDistanceMeters)
                return WorldTerrainDetailTier.FarSilhouette;
            return WorldTerrainDetailTier.DistantHlod;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldTerrainDetailTierInfo ResolveTierInfo(WorldTerrainDetailTier tier)
        {
            switch (tier)
            {
                case WorldTerrainDetailTier.NearPlayable:
                    return NearPlayable;
                case WorldTerrainDetailTier.MidTraversal:
                    return MidTraversal;
                case WorldTerrainDetailTier.FarSilhouette:
                    return FarSilhouette;
                default:
                    return DistantHlod;
            }
        }
    }
}
