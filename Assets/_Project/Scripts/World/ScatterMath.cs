using Hecton8.Environment;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Static scatter evaluation math shared by sampling, candidate acceptance, and envelope gates.
    /// </summary>
    internal static class ScatterMath
    {
        public static int ResolveHeightLayerIndex(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in WorldProceduralScatterDirector.ScatterRuntimeRuleEntry runtimeRule)
        {
            if (fieldSample.isSecondaryDomain != 0 || fieldSample.verticalDomainWeight > 0f)
                return math.max(0, fieldSample.verticalDomainIndex);

            return ResolveHeightLayerIndex(
                fieldSample.caveProximity,
                runtimeRule.Family,
                runtimeRule.StructureAccentRole);
        }

        public static int ResolveHeightLayerIndex(
            float caveProximity,
            WorldPrefabFamilyProfile family,
            WorldPrefabFamilyProfile.StructureAccentRole structureAccentRole)
        {
            if (family == null)
                return 0;

            const float caveHeightLayerThreshold = 0.68f;
            bool explicitCaveDomain = family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance
                                      || structureAccentRole == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead;
            bool caveQualified = caveProximity >= caveHeightLayerThreshold
                                 && (family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Structure
                                     || family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn
                                     || explicitCaveDomain);
            return explicitCaveDomain || caveQualified ? 1 : 0;
        }

        public static bool ShouldEvaluateScatterDomain(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            in WorldProceduralScatterDirector.ScatterRuntimeRuleEntry runtimeRule)
        {
            if (fieldSample.isSecondaryDomain == 0)
                return true;

            WorldPrefabFamilyProfile family = runtimeRule.Family;
            if (family == null)
                return false;

            if (runtimeRule.ScatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Structure ||
                runtimeRule.ScatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn)
            {
                return true;
            }

            return family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance ||
                   runtimeRule.StructureAccentRole == WorldPrefabFamilyProfile.StructureAccentRole.CaveRead;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public static float GetHorizontalDistanceSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return (dx * dx) + (dz * dz);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public static long ComposeScatterGridKey(int cellX, int cellZ)
        {
            return ((long)cellX << 32) | (uint)cellZ;
        }

        public static float ResolveRequiredDistance(
            WorldProceduralScatterDirector.ScatterPlacement candidate,
            WorldProceduralScatterDirector.ScatterPlacement existing)
        {
            WorldPrefabFamilyProfile.ScatterLayer candidateLayer = candidate.Family.scatterLayer;
            WorldPrefabFamilyProfile.ScatterLayer existingLayer = existing.Family.scatterLayer;
            float candidateSpacing = candidate.EffectiveSpacing;
            float existingSpacing = existing.EffectiveSpacing;
            float maxSpacing = math.max(candidateSpacing, existingSpacing);

            if (candidateLayer == existingLayer)
            {
                return candidateLayer switch
                {
                    WorldPrefabFamilyProfile.ScatterLayer.Ground => math.max(1.25f, maxSpacing * 0.52f),
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster => math.max(3f, maxSpacing * 0.92f),
                    WorldPrefabFamilyProfile.ScatterLayer.Structure => math.max(12f, maxSpacing),
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn => math.max(14f, maxSpacing * 1.08f),
                    _ => maxSpacing
                };
            }

            bool candidatePocket = IsPocket(candidate.Family.proceduralDomain);
            bool existingPocket = IsPocket(existing.Family.proceduralDomain);
            if (candidatePocket && existingPocket)
                return math.max(10f, maxSpacing * 1.35f);

            bool candidateStructure = IsStructure(candidate.Family.scatterLayer);
            bool existingStructure = IsStructure(existing.Family.scatterLayer);
            if (candidateStructure && existingStructure)
                return math.max(14f, maxSpacing * 0.88f);

            bool candidateSpawn = candidateLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn;
            bool existingSpawn = existingLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn;
            if ((candidateSpawn && existingStructure) || (candidateStructure && existingSpawn))
                return math.max(12f, math.max(candidateSpacing, existingSpacing) * 0.9f);

            return 0f;
        }

        public static float GetEffectiveSpacing(WorldPrefabFamilyProfile family, WorldProceduralPlacementRule rule)
        {
            if (rule != null && rule.minSpacingOverrideMeters > 0f)
                return math.max(0.5f, rule.minSpacingOverrideMeters);

            return family != null ? math.max(0.5f, family.minSpacingMeters) : 1f;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public static float EvaluateDepthLightProxy01(
            float depthMeters,
            float deepFloraMinDepthMeters,
            float deepFloraLightProxyFadeRangeMeters)
        {
            float minimumDepth = math.max(1f, deepFloraMinDepthMeters);
            float fadeRange = math.max(1f, deepFloraLightProxyFadeRangeMeters);
            float darkness01 = math.saturate((depthMeters - minimumDepth) / fadeRange);
            return 1f - darkness01;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public static float EvaluateClusterPatchMask01(
            float worldX,
            float worldZ,
            int chunkX,
            int chunkZ,
            float clusterNoiseScale,
            int ruleIdHash,
            int familyHash)
        {
            float safeScale = math.max(0.0001f, clusterNoiseScale);
            int chunkSalt = (chunkX * 73856093) ^ (chunkZ * 19349663);
            float2 chunkOffset = new float2(
                ((chunkX & 1023) * 0.173f) + ((chunkSalt & 255) * 0.00390625f),
                ((chunkZ & 1023) * 0.197f) + (((chunkSalt >> 8) & 255) * 0.00390625f));
            float2 p = new float2(
                (worldX * safeScale) + ((ruleIdHash & 255) * 0.03125f),
                (worldZ * safeScale) + ((familyHash & 255) * 0.03125f)) + chunkOffset;

            float octaveA = ValueNoise01(p, ruleIdHash ^ familyHash ^ chunkSalt ^ 0x51A3);
            float octaveB = ValueNoise01((p * 1.93f) + new float2(11.7f, -7.1f), ruleIdHash ^ chunkSalt ^ 0x2C1B);
            float octaveC = ValueNoise01((p * 0.57f) + new float2(-19.4f, 23.8f), familyHash ^ chunkSalt ^ 0x6D2F);
            return math.saturate((octaveA * 0.58f) + (octaveB * 0.28f) + (octaveC * 0.14f));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public static float ResolveDeterministicFloraYawDegrees(int stableHash, float3 absolutePosition)
        {
            uint hash = MixAupScatterHash(stableHash, absolutePosition, 0xA53A9D1Bu);
            return (hash & 0x00FFFFFFu) * (360f / 16777216f);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public static float ResolveDeterministicFloraScaleMultiplier(
            float minScale,
            float maxScale,
            int stableHash,
            float3 absolutePosition)
        {
            float min = math.max(0.1f, math.min(minScale, maxScale));
            float max = math.max(min, math.max(minScale, maxScale));
            if (math.abs(max - min) <= 0.0001f)
                return min;

            uint hash = MixAupScatterHash(stableHash, absolutePosition, 0x6C8E9CF5u);
            float t = (hash & 0x00FFFFFFu) * (1f / 16777215f);
            return math.lerp(min, max, t);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public static float ResolveDeterministicFloraSizeVariance(int stableHash, float3 absolutePosition)
        {
            uint hash = MixAupScatterHash(stableHash, absolutePosition, 0x4A6F7261u);
            return 0.8f + (hash % 50u) * 0.01f;
        }

        internal static byte ResolveFloraBudgetClassId(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return 0;

            switch (family.proceduralDomain)
            {
                case WorldPrefabFamilyProfile.ProceduralDomain.Kelp:
                case WorldPrefabFamilyProfile.ProceduralDomain.Plant:
                case WorldPrefabFamilyProfile.ProceduralDomain.Coral:
                    return family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Ground ? (byte)1 : (byte)2;
                default:
                    return 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private static float ValueNoise01(float2 p, int salt)
        {
            float2 cell = math.floor(p);
            float2 local = p - cell;
            float2 t = local * local * (3f - (2f * local));

            uint hashBase = unchecked((uint)(((int)cell.x * 73856093) ^ ((int)cell.y * 19349663) ^ salt));
            float a = Hash01(hashBase);
            float b = Hash01(hashBase ^ 0x9E3779B9u);
            float c = Hash01(hashBase ^ 0x85EBCA6Bu);
            float d = Hash01(hashBase ^ 0xC2B2AE35u);
            return math.lerp(math.lerp(a, b, t.x), math.lerp(c, d, t.x), t.y);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private static uint MixAupScatterHash(int stableHash, float3 absolutePosition, uint salt)
        {
            int ix = (int)math.floor(absolutePosition.x * 4f);
            int iy = (int)math.floor(absolutePosition.y * 4f);
            int iz = (int)math.floor(absolutePosition.z * 4f);
            uint hash = ((uint)stableHash ^ salt) + 0x9E3779B9u;
            hash ^= ((uint)ix + 0x85EBCA6Bu) * 0xC2B2AE35u;
            hash ^= ((uint)iy + 0x27D4EB2Fu) * 0x165667B1u;
            hash ^= ((uint)iz + 0xD3A2646Cu) * 0x9E3779B1u;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }

        private static bool IsPocket(WorldPrefabFamilyProfile.ProceduralDomain domain)
        {
            return domain == WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket
                || domain == WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket
                || domain == WorldPrefabFamilyProfile.ProceduralDomain.SafePocket;
        }

        private static bool IsStructure(WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            return layer == WorldPrefabFamilyProfile.ScatterLayer.Structure;
        }
    }
}
