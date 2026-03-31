using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldChunkStreamingProfile", menuName = "Hecton8/World/Chunk Streaming Profile")]
    public sealed class WorldChunkStreamingProfile : ScriptableObject
    {
        [System.Serializable]
        public struct LayerProfile
        {
            public WorldStreamingLayer layer;
            public bool useChunkResidency;
            public bool useVisualProxyLayer;
            public bool useFullSimulationNearPlayer;
            [Min(0f)] public float nearRadiusScale;
            [Min(0f)] public float midRadiusScale;
            [Min(0f)] public float farRadiusScale;
            [Min(0)] public int maxChunkLoadsPerTick;
            [Min(0)] public int maxChunkUnloadsPerTick;
            [Min(0)] public int maxActivationsPerTick;
        }

        [Header("World Scale")]
        [Min(1000f)] public float worldSizeMeters = 15000f;
        [Min(32f)] public float chunkSizeMeters = 192f;
        [Min(8f)] public float chunkCellSizeMeters = 64f;
        [Min(128f)] public float macroZoneSizeMeters = 768f;

        [Header("Observer Rings")]
        [Min(0f)] public float fullSimulationRadius = 180f;
        [Min(0f)] public float midSimulationRadius = 420f;
        [Min(0f)] public float visualResidencyRadius = 900f;
        [Min(0f)] public float dataResidencyRadius = 1800f;

        [Header("Layer Profiles")]
        public LayerProfile[] layers;

        public bool TryGetLayerProfile(WorldStreamingLayer layer, out LayerProfile profile)
        {
            if (layers != null)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i].layer == layer)
                    {
                        profile = layers[i];
                        return true;
                    }
                }
            }

            profile = default;
            return false;
        }

        public LayerProfile GetLayerProfileOrDefault(WorldStreamingLayer layer)
        {
            if (TryGetLayerProfile(layer, out LayerProfile profile))
                return profile;

            return CreateDefaultLayerProfile(layer);
        }

        public static LayerProfile CreateDefaultLayerProfile(WorldStreamingLayer layer)
        {
            LayerProfile profile = new LayerProfile
            {
                layer = layer,
                useChunkResidency = true,
                useVisualProxyLayer = false,
                useFullSimulationNearPlayer = false,
                nearRadiusScale = 1f,
                midRadiusScale = 1f,
                farRadiusScale = 1f,
                maxChunkLoadsPerTick = 2,
                maxChunkUnloadsPerTick = 2,
                maxActivationsPerTick = 8
            };

            switch (layer)
            {
                case WorldStreamingLayer.TerrainLod:
                    profile.useVisualProxyLayer = true;
                    profile.nearRadiusScale = 1.4f;
                    profile.midRadiusScale = 1.35f;
                    profile.farRadiusScale = 1.5f;
                    profile.maxChunkLoadsPerTick = 4;
                    profile.maxChunkUnloadsPerTick = 4;
                    profile.maxActivationsPerTick = 12;
                    break;

                case WorldStreamingLayer.Flora:
                    profile.useVisualProxyLayer = true;
                    profile.nearRadiusScale = 1.25f;
                    profile.midRadiusScale = 1.2f;
                    profile.farRadiusScale = 1.15f;
                    profile.maxChunkLoadsPerTick = 3;
                    profile.maxChunkUnloadsPerTick = 3;
                    profile.maxActivationsPerTick = 20;
                    break;

                case WorldStreamingLayer.Debris:
                    profile.useVisualProxyLayer = true;
                    profile.nearRadiusScale = 1.15f;
                    profile.midRadiusScale = 1.1f;
                    profile.farRadiusScale = 1f;
                    profile.maxChunkLoadsPerTick = 3;
                    profile.maxChunkUnloadsPerTick = 3;
                    profile.maxActivationsPerTick = 18;
                    break;

                case WorldStreamingLayer.Resources:
                    profile.nearRadiusScale = 1f;
                    profile.midRadiusScale = 0.95f;
                    profile.farRadiusScale = 0.9f;
                    profile.maxChunkLoadsPerTick = 3;
                    profile.maxChunkUnloadsPerTick = 3;
                    profile.maxActivationsPerTick = 24;
                    break;

                case WorldStreamingLayer.Fauna:
                    profile.useVisualProxyLayer = true;
                    profile.useFullSimulationNearPlayer = true;
                    profile.nearRadiusScale = 1f;
                    profile.midRadiusScale = 1f;
                    profile.farRadiusScale = 1.1f;
                    profile.maxChunkLoadsPerTick = 3;
                    profile.maxChunkUnloadsPerTick = 3;
                    profile.maxActivationsPerTick = 24;
                    break;

                case WorldStreamingLayer.Construction:
                    profile.nearRadiusScale = 1.2f;
                    profile.midRadiusScale = 1f;
                    profile.farRadiusScale = 0.9f;
                    profile.maxChunkLoadsPerTick = 2;
                    profile.maxChunkUnloadsPerTick = 2;
                    profile.maxActivationsPerTick = 8;
                    break;

                case WorldStreamingLayer.LargeThreats:
                    profile.useChunkResidency = false;
                    profile.useVisualProxyLayer = true;
                    profile.useFullSimulationNearPlayer = true;
                    profile.nearRadiusScale = 1.2f;
                    profile.midRadiusScale = 1.4f;
                    profile.farRadiusScale = 1.6f;
                    profile.maxChunkLoadsPerTick = 1;
                    profile.maxChunkUnloadsPerTick = 1;
                    profile.maxActivationsPerTick = 2;
                    break;
            }

            return profile;
        }

        private void OnValidate()
        {
            if (worldSizeMeters < 1000f) worldSizeMeters = 1000f;
            if (chunkSizeMeters < 32f) chunkSizeMeters = 32f;
            if (chunkCellSizeMeters < 8f) chunkCellSizeMeters = 8f;
            if (macroZoneSizeMeters < chunkSizeMeters) macroZoneSizeMeters = chunkSizeMeters;
            if (midSimulationRadius < fullSimulationRadius) midSimulationRadius = fullSimulationRadius;
            if (visualResidencyRadius < midSimulationRadius) visualResidencyRadius = midSimulationRadius;
            if (dataResidencyRadius < visualResidencyRadius) dataResidencyRadius = visualResidencyRadius;

            if (layers == null || layers.Length == 0)
            {
                layers = new[]
                {
                    CreateDefaultLayerProfile(WorldStreamingLayer.TerrainLod),
                    CreateDefaultLayerProfile(WorldStreamingLayer.Flora),
                    CreateDefaultLayerProfile(WorldStreamingLayer.Debris),
                    CreateDefaultLayerProfile(WorldStreamingLayer.Resources),
                    CreateDefaultLayerProfile(WorldStreamingLayer.Fauna),
                    CreateDefaultLayerProfile(WorldStreamingLayer.Construction),
                    CreateDefaultLayerProfile(WorldStreamingLayer.LargeThreats)
                };
            }
        }
    }
}
