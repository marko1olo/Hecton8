using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Graphics.Culling
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct GenerateMockCullingDataJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ShadowCullInstanceDTO> Instances;
        [NoAlias] public NativeArray<ShadowCullStateDTO> States;
        [NoAlias] public NativeArray<float> IlluminationScalars;
        public double3 OriginAUP;
        public int Count;
        public uint Seed;

        public void Execute(int index)
        {
            int count = math.min(Count, math.min(Instances.Length, math.min(States.Length, IlluminationScalars.Length)));
            if ((uint)index >= (uint)count)
                return;

            uint hash = Hash32((uint)index ^ Seed ^ 0x9E3779B9u);
            int gridX = index % 250;
            int gridZ = (index / 250) % 200;
            float x = (gridX - 125) * 1.95f;
            float z = 4f + gridZ * 1.55f;
            float wave = HashToSigned01(hash) * 2.5f;
            float y = ((index / 50000) * 3f) + wave;
            float radius = math.lerp(0.18f, 3.2f, HashToUnit01(hash * 747796405u));
            float3 extents = new float3(radius, math.max(0.08f, radius * 0.62f), radius);
            float pocket = HashToUnit01(Hash32(hash ^ 0xB5297A4Du));
            float illumination = math.saturate(math.lerp(0.012f, 1f, pocket * pocket));

            ShadowCullInstanceDTO instance = default;
            instance.CenterAUP = OriginAUP + new double3(x, y, z);
            instance.Extents = extents;
            instance.BoundsRadius = radius;
            instance.InstanceHash = hash == 0u ? 1u : hash;
            instance.SourceFlags = AbyssalShadowSourceFlags.DirectionalLightShadow | AbyssalShadowSourceFlags.DynamicCaster;
            if ((hash & 7u) == 0u)
                instance.SourceFlags = AbyssalShadowSourceFlags.PointLightShadow | AbyssalShadowSourceFlags.DynamicCaster;
            instance.MaterialShadowScalar = math.lerp(0.45f, 1f, HashToUnit01(hash ^ 0x68E31DA4u));
            instance.OcclusionScalar = math.lerp(0.35f, 1f, HashToUnit01(hash ^ 0x1B56C4E9u));
            instance.ProfileHash = Hash32(hash ^ 0xC2B2AE35u);
            instance._pad0 = 0u;
            Instances[index] = instance;
            IlluminationScalars[index] = illumination;

            ShadowCullStateDTO state = default;
            state.InstanceHash = instance.InstanceHash;
            state.DistanceSq = 0f;
            state.CullFlags = AbyssalShadowCullFlags.MainVisible | AbyssalShadowCullFlags.CastShadows | AbyssalShadowCullFlags.RollbackExcluded;
            state.IlluminationScalar = illumination;
            States[index] = state;
        }

        private static uint Hash32(uint x)
        {
            x ^= x >> 16;
            x *= 2246822519u;
            x ^= x >> 13;
            x *= 3266489917u;
            x ^= x >> 16;
            return x;
        }

        private static float HashToUnit01(uint x)
        {
            return (Hash32(x) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float HashToSigned01(uint x)
        {
            return HashToUnit01(x) * 2f - 1f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct GenerateMockHzbTilesJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ShadowCullHzbTileDTO> HzbTiles;
        public float NearDepthMeters;
        public float FarDepthMeters;
        public float GlobalQualityWeight;
        public int TileCount;
        public int GridResolution;
        public uint Seed;

        public void Execute(int index)
        {
            int count = math.min(TileCount, HzbTiles.Length);
            if ((uint)index >= (uint)count)
                return;

            int resolution = math.max(1, GridResolution);
            int x = index % resolution;
            int y = index / resolution;
            float invResolution = math.rcp(math.max(1f, resolution));
            float2 uv = (new float2(x + 0.5f, y + 0.5f) * invResolution) * 2f - 1f;
            uint hash = Hash32((uint)index ^ Seed ^ 0x6A09E667u);
            float ridge = math.saturate(math.abs(uv.x * 0.72f + uv.y * 0.28f));
            float radialSq = math.saturate(math.dot(uv, uv));
            float noise = HashToUnit01(hash);
            float occluder = math.saturate(1f - radialSq * 0.85f + noise * 0.22f);
            float depth01 = math.saturate(ridge * 0.4f + (1f - occluder) * 0.6f);
            float qualityCurve = Smooth01(GlobalQualityWeight);

            ShadowCullHzbTileDTO tile = default;
            tile.DepthMeters = math.lerp(
                math.max(0.01f, NearDepthMeters),
                math.max(NearDepthMeters + 1f, FarDepthMeters),
                depth01);
            tile.OcclusionBiasMeters = math.lerp(0.35f, 3.5f, qualityCurve);
            tile.TileHash = hash == 0u ? 1u : hash;
            tile.Flags = occluder > math.lerp(0.22f, 0.55f, qualityCurve) ? 1u : 0u;
            HzbTiles[index] = tile;
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(math.isfinite(value) ? value : 1f);
            return t * t * (3f - 2f * t);
        }

        private static uint Hash32(uint x)
        {
            x ^= x >> 16;
            x *= 2246822519u;
            x ^= x >> 13;
            x *= 3266489917u;
            x ^= x >> 16;
            return x;
        }

        private static float HashToUnit01(uint x)
        {
            return (Hash32(x) & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EvaluateShadowCullingJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ShadowCullInstanceDTO> Instances;
        [ReadOnly, NoAlias] public NativeArray<float> IlluminationScalars;
        [ReadOnly, NoAlias] public NativeArray<float4> LocalFrustumPlanes;
        [ReadOnly, NoAlias] public NativeArray<ShadowCullProfileRuleDTO> ProfileRules;
        [ReadOnly, NoAlias] public NativeArray<ShadowCullHzbTileDTO> HzbTiles;
        [NoAlias] public NativeArray<ShadowCullStateDTO> States;

        public double3 CameraAUP;
        public float3 DirectionalLightDirection;
        public float GlobalQualityWeight;
        public float BaseShadowDistanceMeters;
        public float DarknessThreshold;
        public float DitherFadeBand01;
        public float MinCasterRadiusAtFullQuality;
        public float MaxCasterRadiusAtMinQuality;
        public float DirectionalShadowReachMeters;
        public float PointLightUltraThreshold;
        public float HzbWorldSpanMeters;
        public float3 HzbViewRight;
        public float3 HzbViewUp;
        public float3 HzbViewForward;
        public int InstanceCount;
        public int ProfileRuleCount;
        public int HzbTileCount;
        public int HzbGridResolution;

        public void Execute(int index)
        {
            int count = math.min(InstanceCount, math.min(Instances.Length, math.min(States.Length, IlluminationScalars.Length)));
            if ((uint)index >= (uint)count)
                return;

            ShadowCullInstanceDTO instance = Instances[index];
            ShadowCullStateDTO previousState = States[index];
            bool hasPreviousState = previousState.InstanceHash == instance.InstanceHash &&
                                    math.isfinite(previousState.DistanceSq) &&
                                    previousState.DistanceSq > 0f &&
                                    (previousState.CullFlags & AbyssalShadowCullFlags.NonFinite) == 0u;
            uint previousFlags = hasPreviousState ? previousState.CullFlags : 0u;
            bool previousMainVisible = (previousFlags & AbyssalShadowCullFlags.MainVisible) != 0u;
            bool previousShadowVisible = (previousFlags & (AbyssalShadowCullFlags.CastShadows | AbyssalShadowCullFlags.ShadowOnly)) != 0u;
            bool previousCastShadow = (previousFlags & AbyssalShadowCullFlags.CastShadows) != 0u;
            uint flags = AbyssalShadowCullFlags.RollbackExcluded;
            float distanceSq = float.MaxValue;
            float scalar = 0f;
            bool finite = math.all(math.isfinite(instance.CenterAUP)) &&
                          math.all(math.isfinite(instance.Extents)) &&
                          math.isfinite(instance.BoundsRadius);

            if (finite)
            {
                double3 deltaAUP = AupPrecisionMath.LocalDeltaDouble(instance.CenterAUP, CameraAUP);
                float3 center = AupPrecisionMath.DowncastLocalDelta(deltaAUP, float3.zero);
                float3 extents = math.max(instance.Extents, new float3(0.01f));
                double distanceSqDouble = math.lengthsq(deltaAUP);
                distanceSq = distanceSqDouble >= float.MaxValue ? float.MaxValue : (float)distanceSqDouble;
                finite = math.all(math.isfinite(center)) && math.isfinite(distanceSq);
                if (finite)
                {
                    float quality = math.saturate(GlobalQualityWeight);
                    ShadowCullProfileRuleDTO profile = ResolveProfileRule(instance.ProfileHash);
                    float profileDistanceScale = profile.Flags != 0u ? profile.ShadowDistanceScale : 1f;
                    float profileDarknessScale = profile.Flags != 0u ? profile.DarknessThresholdScale : 1f;
                    float profileMinRadius = profile.Flags != 0u ? profile.MinCasterRadiusMeters : 0f;
                    float profileFadeScale = profile.Flags != 0u ? profile.FadeBandScale : 1f;
                    float profilePointBudget = profile.Flags != 0u ? profile.PointLightBudget01 : 1f;
                    float maxBaseDistance = math.max(
                        AbyssalShadowCullingConstants.MinimumShadowDistanceMeters,
                        BaseShadowDistanceMeters * profileDistanceScale);
                    float maxShadowDistance = math.lerp(
                        AbyssalShadowCullingConstants.MinimumShadowDistanceMeters,
                        maxBaseDistance,
                        quality);
                    float distanceHysteresis = math.lerp(
                        AbyssalShadowCullingConstants.MaximumDistanceHysteresisMeters,
                        AbyssalShadowCullingConstants.MinimumDistanceHysteresisMeters,
                        quality);
                    float frustumHysteresis = math.lerp(
                        AbyssalShadowCullingConstants.MinimumFrustumHysteresisMeters,
                        AbyssalShadowCullingConstants.MaximumFrustumHysteresisMeters,
                        quality);
                    float distanceCullMeters = maxShadowDistance + (previousCastShadow ? distanceHysteresis : -distanceHysteresis);
                    distanceCullMeters = math.max(1f, distanceCullMeters);
                    float maxShadowDistanceSq = maxShadowDistance * maxShadowDistance;
                    float distanceCullSq = distanceCullMeters * distanceCullMeters;
                    float previousMainExpansion = previousMainVisible ? frustumHysteresis : 0f;
                    float previousShadowExpansion = previousShadowVisible ? frustumHysteresis : 0f;
                    bool mainVisible = IntersectsFrustum(center, extents, previousMainExpansion);
                    bool shadowVisible = mainVisible || IntersectsDirectionalShadowFrustum(center, extents, previousShadowExpansion);
                    if (mainVisible)
                        flags |= AbyssalShadowCullFlags.MainVisible;
                    else
                        flags |= AbyssalShadowCullFlags.MainFrustumCulled;

                    if (shadowVisible && !mainVisible)
                        flags |= AbyssalShadowCullFlags.ShadowOnly;

                    bool hzbOccluded = ResolveHzbOcclusion(center, extents, quality);
                    if (hzbOccluded)
                    {
                        mainVisible = false;
                        shadowVisible = false;
                        flags &= ~(AbyssalShadowCullFlags.MainVisible | AbyssalShadowCullFlags.ShadowOnly);
                        flags |= AbyssalShadowCullFlags.MainFrustumCulled | AbyssalShadowCullFlags.HzbOcclusionCulled;
                    }

                    bool castShadow = shadowVisible;
                    float illumination = math.saturate(IlluminationScalars[index] * instance.MaterialShadowScalar * instance.OcclusionScalar);
                    float safeDarkness = math.max(0f, DarknessThreshold * profileDarknessScale);
                    float darknessHysteresis = math.lerp(
                        AbyssalShadowCullingConstants.MaximumDarknessHysteresisScalar,
                        AbyssalShadowCullingConstants.MinimumDarknessHysteresisScalar,
                        quality);
                    float darknessGate = math.saturate(safeDarkness + (previousCastShadow ? -darknessHysteresis : darknessHysteresis));
                    if (illumination <= darknessGate)
                    {
                        castShadow = false;
                        flags |= AbyssalShadowCullFlags.DarknessCulled;
                    }

                    float sdfOcclusionGate = math.lerp(0.55f, 0.06f, quality);
                    float sdfHysteresis = math.lerp(
                        AbyssalShadowCullingConstants.MaximumSdfHysteresisScalar,
                        AbyssalShadowCullingConstants.MinimumSdfHysteresisScalar,
                        quality);
                    float effectiveSdfGate = math.saturate(sdfOcclusionGate + (previousCastShadow ? -sdfHysteresis : sdfHysteresis));
                    if (math.saturate(instance.OcclusionScalar) <= effectiveSdfGate)
                    {
                        castShadow = false;
                        flags |= AbyssalShadowCullFlags.SdfOcclusionCulled;
                    }

                    if (distanceSq > distanceCullSq)
                    {
                        castShadow = false;
                        flags |= AbyssalShadowCullFlags.DistanceShadowCulled;
                    }

                    float radius = math.max(0f, instance.BoundsRadius);
                    float minCasterRadius = math.lerp(MaxCasterRadiusAtMinQuality, MinCasterRadiusAtFullQuality, quality);
                    float distancePressure = math.saturate(distanceSq / math.max(1f, maxShadowDistanceSq));
                    float radiusGate = math.max(profileMinRadius, math.lerp(minCasterRadius * 0.35f, minCasterRadius, distancePressure));
                    float radiusHysteresis = math.lerp(
                        AbyssalShadowCullingConstants.MaximumRadiusHysteresisMeters,
                        AbyssalShadowCullingConstants.MinimumRadiusHysteresisMeters,
                        quality);
                    float effectiveRadiusGate = math.max(0f, radiusGate + (previousCastShadow ? -radiusHysteresis : radiusHysteresis));
                    if (radius < effectiveRadiusGate)
                    {
                        castShadow = false;
                        flags |= AbyssalShadowCullFlags.TooSmallCaster;
                    }

                    if ((instance.SourceFlags & AbyssalShadowSourceFlags.PointLightShadow) != 0u &&
                        (instance.SourceFlags & AbyssalShadowSourceFlags.DirectionalLightShadow) == 0u)
                    {
                        float pointThreshold = math.min(0.999f, math.saturate(PointLightUltraThreshold));
                        float allowance = math.smoothstep(pointThreshold, 1f, quality) * math.saturate(profilePointBudget);
                        float pointHysteresis = math.lerp(
                            AbyssalShadowCullingConstants.MaximumPointBudgetHysteresis01,
                            AbyssalShadowCullingConstants.MinimumPointBudgetHysteresis01,
                            quality);
                        float effectiveAllowance = math.saturate(allowance + (previousCastShadow ? pointHysteresis : -pointHysteresis));
                        uint pointHash = Hash32(instance.InstanceHash ^ 0xA53A9BCDu);
                        if (HashToUnit01(pointHash) > effectiveAllowance)
                        {
                            castShadow = false;
                            flags |= AbyssalShadowCullFlags.PointLightCulled;
                        }
                    }

                    float fadeBand01 = math.clamp(DitherFadeBand01 * profileFadeScale, 0.001f, 0.5f);
                    float fadeStartSq = distanceCullSq * math.max(0.001f, 1f - fadeBand01);
                    float fadeScalar = math.saturate((distanceCullSq - distanceSq) / math.max(1f, distanceCullSq - fadeStartSq));
                    if (fadeScalar < 0.999f)
                        flags |= AbyssalShadowCullFlags.DitherFadeActive;

                    scalar = math.saturate(illumination * fadeScalar);
                    if (castShadow)
                        flags |= AbyssalShadowCullFlags.CastShadows;
                    else if (shadowVisible)
                        flags |= AbyssalShadowCullFlags.DistanceShadowCulled;
                }
            }

            if (!finite)
            {
                flags |= AbyssalShadowCullFlags.NonFinite;
                distanceSq = float.MaxValue;
                scalar = 0f;
            }

            void* rawStates = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(States);
            byte* statePtr = (byte*)rawStates + index * AbyssalShadowCullingConstants.ShadowCullStateStrideBytes;
            ref ShadowCullStateDTO state = ref UnsafeUtility.AsRef<ShadowCullStateDTO>(statePtr);
            state.InstanceHash = instance.InstanceHash;
            state.DistanceSq = distanceSq;
            state.CullFlags = flags;
            state.IlluminationScalar = scalar;
            state._pad0 = 0;
            state._pad1 = 0;
            state._pad2 = 0;
            state._pad3 = 0;
            state._pad4 = 0;
            state._pad5 = 0;
            state._pad6 = 0;
            state._pad7 = 0;
            state._pad8 = 0;
            state._pad9 = 0;
            state._pad10 = 0;
            state._pad11 = 0;
            state._pad12 = 0;
            state._pad13 = 0;
            state._pad14 = 0;
            state._pad15 = 0;
        }

        private bool IntersectsFrustum(float3 center, float3 extents, float uniformExpansion)
        {
            if (!LocalFrustumPlanes.IsCreated || LocalFrustumPlanes.Length < AbyssalShadowCullingConstants.FrustumPlaneCount)
                return true;

            for (int i = 0; i < AbyssalShadowCullingConstants.FrustumPlaneCount; i++)
            {
                float4 plane = LocalFrustumPlanes[i];
                float3 normal = plane.xyz;
                float projectedRadius = math.dot(math.abs(normal), extents) + uniformExpansion;
                float signedDistance = math.dot(normal, center) + plane.w;
                if (!math.isfinite(signedDistance) || signedDistance + projectedRadius < 0f)
                    return false;
            }

            return true;
        }

        private bool ResolveHzbOcclusion(float3 center, float3 extents, float quality)
        {
            if (!HzbTiles.IsCreated || HzbTileCount <= 0 || HzbGridResolution <= 0)
                return false;

            int available = math.min(HzbTileCount, HzbTiles.Length);
            if (available <= 0)
                return false;

            int resolution = math.clamp(HzbGridResolution, 1, AbyssalShadowCullingConstants.HzbGridResolution);
            float span = math.max(1f, HzbWorldSpanMeters);
            float3 right = math.normalizesafe(HzbViewRight, new float3(1f, 0f, 0f));
            float3 up = math.normalizesafe(HzbViewUp, new float3(0f, 1f, 0f));
            float3 forward = math.normalizesafe(HzbViewForward, new float3(0f, 0f, 1f));
            float2 viewXY = new float2(math.dot(center, right), math.dot(center, up));
            float uvSpan = math.max(span, 0.0001f);
            float2 uv = math.saturate((viewXY / uvSpan) + 0.5f);
            int tileX = math.min(resolution - 1, (int)math.floor(uv.x * resolution));
            int tileY = math.min(resolution - 1, (int)math.floor(uv.y * resolution));
            int tileIndex = math.clamp(tileY * resolution + tileX, 0, available - 1);
            ShadowCullHzbTileDTO tile = HzbTiles[tileIndex];
            if ((tile.Flags & 1u) == 0u)
                return false;

            float viewDepth = math.dot(center, forward);
            float projectedDepthExtent = math.dot(math.abs(forward), extents);
            float frontDepth = math.max(0f, viewDepth - projectedDepthExtent);
            float bias = math.lerp(0.05f, math.max(0.05f, tile.OcclusionBiasMeters), quality);
            return math.isfinite(frontDepth) &&
                   math.isfinite(tile.DepthMeters) &&
                   frontDepth > tile.DepthMeters + bias;
        }

        private ShadowCullProfileRuleDTO ResolveProfileRule(uint profileHash)
        {
            if (!ProfileRules.IsCreated || ProfileRuleCount <= 0 || profileHash == 0u)
                return default;

            int count = math.min(ProfileRuleCount, ProfileRules.Length);
            for (int i = 0; i < count; i++)
            {
                ShadowCullProfileRuleDTO rule = ProfileRules[i];
                if (rule.ProfileHash == profileHash)
                    return rule;
            }

            return default;
        }

        private bool IntersectsDirectionalShadowFrustum(float3 center, float3 extents, float uniformExpansion)
        {
            if (!LocalFrustumPlanes.IsCreated || LocalFrustumPlanes.Length < AbyssalShadowCullingConstants.FrustumPlaneCount)
                return true;

            float3 lightDir = DirectionalLightDirection;
            if (!math.all(math.isfinite(lightDir)) || math.lengthsq(lightDir) < 0.000001f)
                lightDir = new float3(-0.35f, -0.72f, -0.25f);
            lightDir = math.normalizesafe(lightDir, new float3(-0.35f, -0.72f, -0.25f));
            float reach = math.max(0f, DirectionalShadowReachMeters);
            for (int i = 0; i < AbyssalShadowCullingConstants.FrustumPlaneCount; i++)
            {
                float4 plane = LocalFrustumPlanes[i];
                float3 normal = plane.xyz;
                float directionalExpansion = reach * math.abs(math.dot(normal, lightDir));
                float projectedRadius = math.dot(math.abs(normal), extents) + directionalExpansion + uniformExpansion;
                float signedDistance = math.dot(normal, center) + plane.w;
                if (!math.isfinite(signedDistance) || signedDistance + projectedRadius < 0f)
                    return false;
            }

            return true;
        }

        private static uint Hash32(uint x)
        {
            x ^= x >> 16;
            x *= 2246822519u;
            x ^= x >> 13;
            x *= 3266489917u;
            x ^= x >> 16;
            return x;
        }

        private static float HashToUnit01(uint x)
        {
            return (Hash32(x) & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct ReduceShadowCullTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ShadowCullStateDTO> States;
        [NoAlias] public NativeArray<ShadowCullCountersDTO> Counters;
        public int Count;
        public uint ProfileRuleCount;

        public void Execute()
        {
            if (!States.IsCreated || !Counters.IsCreated || Counters.Length <= 0)
                return;

            int count = math.min(Count, States.Length);
            ShadowCullCountersDTO counters = default;
            uint hash = 2166136261u;
            for (int i = 0; i < count; i++)
            {
                ShadowCullStateDTO state = States[i];
                uint flags = state.CullFlags;
                counters.EvaluatedCount++;
                if ((flags & AbyssalShadowCullFlags.MainVisible) == 0u)
                    counters.MainCulledCount++;
                if ((flags & AbyssalShadowCullFlags.CastShadows) == 0u)
                    counters.ShadowCulledCount++;
                if ((flags & AbyssalShadowCullFlags.DarknessCulled) != 0u)
                    counters.DarknessCulledCount++;
                if ((flags & AbyssalShadowCullFlags.PointLightCulled) != 0u)
                    counters.PointLightCulledCount++;
                if ((flags & AbyssalShadowCullFlags.ShadowOnly) != 0u)
                    counters.ShadowOnlyCount++;
                if ((flags & AbyssalShadowCullFlags.DitherFadeActive) != 0u)
                    counters.DitheredCount++;
                if ((flags & AbyssalShadowCullFlags.HzbOcclusionCulled) != 0u)
                    counters.HzbCulledCount++;
                if ((flags & AbyssalShadowCullFlags.SdfOcclusionCulled) != 0u)
                    counters.SdfCulledCount++;
                if ((flags & AbyssalShadowCullFlags.CastShadows) != 0u)
                    counters.VisibleShadowCount++;
                if ((flags & AbyssalShadowCullFlags.NonFinite) != 0u)
                    counters.Flags |= AbyssalShadowCullFlags.NonFinite;

                hash ^= state.InstanceHash;
                hash *= 16777619u;
                hash ^= flags;
                hash *= 16777619u;
            }

            if (hash == 0u)
                hash = 1u;
            counters.ProfileRuleCount = ProfileRuleCount;
            counters.StateHash = hash;
            Counters[0] = counters;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    public struct BuildShadowIndirectArgsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ShadowCullCountersDTO> Counters;
        [NoAlias] public NativeArray<ShadowCullIndirectArgsDTO> IndirectArgs;
        public uint VertexCountPerInstance;
        public uint StartVertex;
        public uint StartInstance;
        public uint StartIndex;

        public void Execute()
        {
            if (!Counters.IsCreated || !IndirectArgs.IsCreated || Counters.Length <= 0 || IndirectArgs.Length <= 0)
                return;

            ShadowCullCountersDTO counters = Counters[0];
            ShadowCullIndirectArgsDTO args = default;
            args.VertexCountPerInstance = math.max(1u, VertexCountPerInstance);
            args.InstanceCount = counters.VisibleShadowCount;
            args.StartVertex = StartVertex;
            args.StartInstance = StartInstance;
            args.StartIndex = StartIndex;
            args.Flags = counters.Flags;
            args._pad0 = 0u;
            args._pad1 = 0u;
            IndirectArgs[0] = args;
        }
    }
}
