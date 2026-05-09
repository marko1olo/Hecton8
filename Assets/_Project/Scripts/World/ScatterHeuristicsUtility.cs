using UnityEngine;
using Unity.Mathematics;

namespace Hecton8.World
{
    internal static class ScatterHeuristicsUtility
    {
        public static float GetDepthDomainScale(
            float depthMeters,
            WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 1f;

            WorldPrefabFamilyProfile.ProceduralDomain domain = family.proceduralDomain;
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => EvaluateDepthBand(depthMeters, 25f, 90f, 180f, 1.14f, 0.82f, 0.34f, 0.18f),
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => EvaluateDepthBand(depthMeters, 35f, 110f, 220f, 1.08f, 0.78f, 0.36f, 0.18f),
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => EvaluateDepthBand(depthMeters, 50f, 180f, 420f, 1.06f, 0.94f, 0.68f, 0.42f),
                WorldPrefabFamilyProfile.ProceduralDomain.Egg => EvaluateDepthBand(depthMeters, 60f, 180f, 420f, 1.02f, 0.92f, 0.72f, 0.48f),
                WorldPrefabFamilyProfile.ProceduralDomain.Rock => EvaluateDepthBand(depthMeters, 40f, 180f, 600f, 0.92f, 1.02f, 1.14f, 1.18f),
                WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => EvaluateDepthBand(depthMeters, 40f, 180f, 700f, 0.94f, 1.04f, 1.16f, 1.22f),
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => EvaluateDepthBand(depthMeters, 60f, 220f, 800f, 0.96f, 1.02f, 1.12f, 1.16f),
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => EvaluateDepthBand(depthMeters, 50f, 220f, 800f, 0.98f, 1.04f, 1.12f, 1.16f),
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => EvaluateDepthBand(depthMeters, 60f, 220f, 900f, 0.96f, 1.02f, 1.10f, 1.14f),
                WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn
                    ? EvaluateSpawnDepthScale(depthMeters, family.primaryPattern)
                    : 1f,
                _ => 1f
            };
        }

        public static float GetDepthDomainScale(
            float depthMeters,
            in WorldProceduralScatterDirector.ScatterRuntimeRuleEntry runtimeRule)
        {
            WorldPrefabFamilyProfile.ProceduralDomain domain = runtimeRule.ProceduralDomain;
            return domain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => EvaluateDepthBand(depthMeters, 25f, 90f, 180f, 1.14f, 0.82f, 0.34f, 0.18f),
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => EvaluateDepthBand(depthMeters, 35f, 110f, 220f, 1.08f, 0.78f, 0.36f, 0.18f),
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => EvaluateDepthBand(depthMeters, 50f, 180f, 420f, 1.06f, 0.94f, 0.68f, 0.42f),
                WorldPrefabFamilyProfile.ProceduralDomain.Egg => EvaluateDepthBand(depthMeters, 60f, 180f, 420f, 1.02f, 0.92f, 0.72f, 0.48f),
                WorldPrefabFamilyProfile.ProceduralDomain.Rock => EvaluateDepthBand(depthMeters, 40f, 180f, 600f, 0.92f, 1.02f, 1.14f, 1.18f),
                WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => EvaluateDepthBand(depthMeters, 40f, 180f, 700f, 0.94f, 1.04f, 1.16f, 1.22f),
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => EvaluateDepthBand(depthMeters, 60f, 220f, 800f, 0.96f, 1.02f, 1.12f, 1.16f),
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => EvaluateDepthBand(depthMeters, 50f, 220f, 800f, 0.98f, 1.04f, 1.12f, 1.16f),
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => EvaluateDepthBand(depthMeters, 60f, 220f, 900f, 0.96f, 1.02f, 1.10f, 1.14f),
                WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => runtimeRule.ScatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn
                    ? EvaluateSpawnDepthScale(depthMeters, runtimeRule.PrimaryPattern)
                    : 1f,
                _ => 1f
            };
        }

        public static float EvaluateSpawnDepthScale(float depthMeters, WorldProceduralPattern primaryPattern)
        {
            return primaryPattern switch
            {
                WorldProceduralPattern.FertileShallows or WorldProceduralPattern.ReefNavigation
                    => EvaluateDepthBand(depthMeters, 35f, 120f, 280f, 1.08f, 0.94f, 0.66f, 0.4f),
                WorldProceduralPattern.BrineToxic
                    => EvaluateDepthBand(depthMeters, 50f, 180f, 650f, 0.78f, 0.92f, 1.04f, 1.08f),
                WorldProceduralPattern.VolcanicPressure
                    => EvaluateDepthBand(depthMeters, 60f, 200f, 700f, 0.74f, 0.94f, 1.08f, 1.14f),
                WorldProceduralPattern.RiftHazard or WorldProceduralPattern.IndustrialService
                    => EvaluateDepthBand(depthMeters, 60f, 180f, 600f, 0.86f, 1.0f, 1.12f, 1.18f),
                _ => EvaluateDepthBand(depthMeters, 40f, 160f, 420f, 1.0f, 0.96f, 0.88f, 0.72f)
            };
        }

        public static float EvaluateDepthBand(
            float depthMeters,
            float nearEnd,
            float midEnd,
            float deepEnd,
            float shallowScale,
            float midScale,
            float deepScale,
            float abyssScale)
        {
            if (depthMeters <= nearEnd)
                return shallowScale;

            if (depthMeters <= midEnd)
                return math.lerp(shallowScale, midScale, Mathf.InverseLerp(nearEnd, midEnd, depthMeters));

            if (depthMeters <= deepEnd)
                return math.lerp(midScale, deepScale, Mathf.InverseLerp(midEnd, deepEnd, depthMeters));

            return abyssScale;
        }
    }
}
