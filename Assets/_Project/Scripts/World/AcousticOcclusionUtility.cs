using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    internal struct AcousticOcclusionResult
    {
        public float Transmission01;
        public float LowPassCutoffHz;
        public int HitCount;

        public AcousticOcclusionResult(float transmission01, float lowPassCutoffHz, int hitCount)
        {
            Transmission01 = transmission01;
            LowPassCutoffHz = lowPassCutoffHz;
            HitCount = hitCount;
        }
    }

    internal struct AcousticEnclosureResult
    {
        public float SpanVerticalMeters;
        public float SpanHorizontalMeters;
        public float SpanDepthMeters;
        public float VolumeCubicMeters;
        public float MeanAbsorption01;
        public float EquivalentAbsorptionArea;
        public float Rt60Seconds;
        public float WetMix01;
        public float Openness01;
        public int SurfaceHitCount;

        public AcousticEnclosureResult(
            float spanVerticalMeters,
            float spanHorizontalMeters,
            float spanDepthMeters,
            float volumeCubicMeters,
            float meanAbsorption01,
            float equivalentAbsorptionArea,
            float rt60Seconds,
            float wetMix01,
            float openness01,
            int surfaceHitCount)
        {
            SpanVerticalMeters = spanVerticalMeters;
            SpanHorizontalMeters = spanHorizontalMeters;
            SpanDepthMeters = spanDepthMeters;
            VolumeCubicMeters = volumeCubicMeters;
            MeanAbsorption01 = meanAbsorption01;
            EquivalentAbsorptionArea = equivalentAbsorptionArea;
            Rt60Seconds = rt60Seconds;
            WetMix01 = wetMix01;
            Openness01 = openness01;
            SurfaceHitCount = surfaceHitCount;
        }
    }

    internal struct AcousticForwardEchoResult
    {
        public float HitDistanceMeters;
        public float Transmission01;
        public float LowPassCutoffHz;
        public bool HasHit;

        public AcousticForwardEchoResult(float hitDistanceMeters, float transmission01, float lowPassCutoffHz, bool hasHit)
        {
            HitDistanceMeters = hitDistanceMeters;
            Transmission01 = transmission01;
            LowPassCutoffHz = lowPassCutoffHz;
            HasHit = hasHit;
        }
    }

    internal struct AcousticSurfaceResponse
    {
        public float Absorption01;
        public float Transmission01;
        public float LowPassCutoffHz;

        public AcousticSurfaceResponse(float absorption01, float transmission01, float lowPassCutoffHz)
        {
            Absorption01 = absorption01;
            Transmission01 = transmission01;
            LowPassCutoffHz = lowPassCutoffHz;
        }
    }

    internal enum AcousticReverbPresetKind : byte
    {
        OpenWater = 0,
        SmallRoom = 1,
        LargeRoom = 2
    }

    /// <summary>
    /// Shared zero-GC acoustic occlusion evaluation for sonar, hearing, and world-geometry filtering.
    /// </summary>
    internal static class AcousticOcclusionUtility
    {
        public const float OpenLowPassCutoffHertz = 22000f;
        public const float MinimumLowPassCutoffHertz = 80f;
        public const float DeepShadowTransmissionThreshold = 0.15f;

        private const float DefaultAbsorption01 = 0.50f;
        private const float RockAbsorption01 = 0.98f;
        private const float MetalAbsorption01 = 0.85f;
        private const float SedimentAbsorption01 = 0.60f;
        private const float WaterAbsorption01 = 0.05f;
        private const int MaxQueuedRequests = 48;
        private const int EnclosurePresetFaceCount = 6;
        private const float OcclusionReuseDistanceMeters = 0.5f;
        private const float OcclusionReuseDistanceSqr = OcclusionReuseDistanceMeters * OcclusionReuseDistanceMeters;
        private const float MinimumPathDistanceMeters = 0.001f;
        private const float DistanceOcclusionStartMeters = 28f;
        private const float DistanceOcclusionFullMeters = 180f;
        private const float DistanceOcclusionTransmissionFloor = 0.32f;
        private const float DistanceOcclusionLowPassFloorHertz = 900f;
        private const float MinimumEquivalentAbsorptionArea = 0.5f;
        private const float MinimumRt60Seconds = 0.12f;
        private const float MaximumRt60Seconds = 10f;
        private const float OpenWaterPresetSpanMeters = 96f;
        private const float OpenWaterRt60Seconds = 10f;
        private const float OpenWaterWetMix01 = 0.20f;
        private const float SmallRoomSpanMeters = 6f;
        private const float LargeRoomSpanMeters = 18f;
        private const float SmallRoomRt60Seconds = 0.48f;
        private const float LargeRoomRt60Seconds = 1.35f;
        private const float SmallRoomWetMix01 = 0.80f;
        private const float LargeRoomWetMix01 = 0.80f;
        private const float SmallRoomOpenness01 = 0.16f;
        private const float LargeRoomOpenness01 = 0.36f;
        private const float ForwardEchoFakeDistanceRatio = 0.12f;
        private const float ForwardEchoMinimumFakeDistanceMeters = 18f;
        private const float ForwardEchoMaximumFakeDistanceMeters = 140f;
        private const float ForwardEchoReuseDistanceMeters = 2f;
        private const float ForwardEchoReuseDistanceSqr = ForwardEchoReuseDistanceMeters * ForwardEchoReuseDistanceMeters;
        private const float ForwardEchoDirectionReuseDot = 0.9961947f;
        private const int KelpDensityTypeMask = 1 << 1;
        private const int FloraScatteringSampleCount = 5;
        private const float FloraScatteringMinimumSegmentMeters = 3f;
        private const float FloraScatteringDensityThreshold = 0.08f;
        private const float FloraScatteringTransmissionFloor = 0.18f;
        private const float FloraScatteringLowPassFloorHertz = 220f;

        private static int PlayerLayer = -1;
        private static int TriggerZoneLayer = -1;
        private static int TransparentFxLayer = -1;
        private static int FirstPersonToolsLayer = -1;
        private static int VoxelCaveLayer = -1;
        private static int BaseModuleLayer = -1;
        private static int VehicleLayer = -1;
        private static int WaterLayer = -1;
        private static bool _layerCacheInitialized;

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct QueryKey
        {
            public Vector3 SourcePosition;
            public int LayerMask;
            public Vector3 ListenerPosition;
            private int _padding0;
            public ulong IgnoreOriginRootEntityId;
            public ulong IgnoreTargetRootEntityId;
            public ulong IgnoreOriginBodyEntityId;
            public ulong IgnoreTargetBodyEntityId;
        }

        private struct CachedQueryEntry
        {
            public QueryKey Key;
            public AcousticOcclusionResult Result;
            public bool Valid;
        }

        [StructLayout(LayoutKind.Sequential, Size = 48)]
        private struct ForwardEchoKey
        {
            public Vector3 OriginPosition;
            public float ProbeDistance;
            public Vector3 ForwardDirection;
            public int LayerMask;
            public ulong IgnoreRootEntityId;
            public ulong IgnoreBodyEntityId;
        }

        private struct CachedForwardEchoEntry
        {
            public ForwardEchoKey Key;
            public AcousticForwardEchoResult Result;
            public bool Valid;
        }

        private static int _runtimeOwnerCount;
        private static int _nextCacheWriteIndex;
        private static bool _triggerPresetActive;
        private static AcousticEnclosureResult _triggerPresetResult;
        private static CachedForwardEchoEntry _cachedForwardEchoEntry;
        // COLD ALLOC: CachedQueryEntry[48] - last resolved acoustic occlusion cache - owner: AcousticOcclusionUtility
        private static readonly CachedQueryEntry[] _cachedEntries = new CachedQueryEntry[MaxQueuedRequests];

        private static void EnsureLayerCache()
        {
            if (_layerCacheInitialized)
                return;

            PlayerLayer = Hecton8.Core.HectonLayerMasks.Player;
            TriggerZoneLayer = Hecton8.Core.HectonLayerMasks.TriggerZone;
            TransparentFxLayer = Hecton8.Core.HectonLayerMasks.TransparentFX;
            FirstPersonToolsLayer = Hecton8.Core.HectonLayerMasks.FirstPersonTools;
            VoxelCaveLayer = Hecton8.Core.HectonLayerMasks.VoxelCave;
            BaseModuleLayer = Hecton8.Core.HectonLayerMasks.BaseModule;
            VehicleLayer = Hecton8.Core.HectonLayerMasks.Vehicle;
            WaterLayer = Hecton8.Core.HectonLayerMasks.Water;
            _layerCacheInitialized = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            PlayerLayer = -1;
            TriggerZoneLayer = -1;
            TransparentFxLayer = -1;
            FirstPersonToolsLayer = -1;
            VoxelCaveLayer = -1;
            BaseModuleLayer = -1;
            VehicleLayer = -1;
            WaterLayer = -1;
            _layerCacheInitialized = false;
            EnsureLayerCache();

            _runtimeOwnerCount = 0;
            _nextCacheWriteIndex = 0;
            _triggerPresetActive = false;
            _triggerPresetResult = BuildOpenWaterResult(1f);
            _cachedForwardEchoEntry.Valid = false;

            for (int i = 0; i < MaxQueuedRequests; i++)
                _cachedEntries[i].Valid = false;

        }

        public static void AcquireRuntime()
        {
            _runtimeOwnerCount++;
        }

        public static void ReleaseRuntime()
        {
            if (_runtimeOwnerCount <= 0)
                return;

            _runtimeOwnerCount--;
            if (_runtimeOwnerCount > 0)
                return;

            _nextCacheWriteIndex = 0;
            _cachedForwardEchoEntry.Valid = false;

            for (int i = 0; i < MaxQueuedRequests; i++)
                _cachedEntries[i].Valid = false;

        }

        public static int BuildSensoryMask()
        {
            EnsureLayerCache();
            int mask = HectonLayerMasks.SeamProbeLayerMask | HectonLayerMasks.CreatureLayerMask;
            mask &= ~LayerBit(PlayerLayer);
            mask &= ~LayerBit(TriggerZoneLayer);
            mask &= ~LayerBit(TransparentFxLayer);
            mask &= ~LayerBit(FirstPersonToolsLayer);
            return mask;
        }

        /// <summary>
        /// Kept as a dispatcher hook; acoustic purge now resolves occlusion through cacheable math only.
        /// </summary>
        public static void LateFrameTick()
        {
        }

        public static void PrimeOcclusionPath(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            int layerMask,
            Transform ignoreOriginRoot,
            Transform ignoreTargetRoot)
        {
            int resolvedLayerMask = ResolveSensoryLayerMask(layerMask);
            QueryKey queryKey = new QueryKey
            {
                SourcePosition = sourcePosition,
                ListenerPosition = listenerPosition,
                LayerMask = resolvedLayerMask,
                IgnoreOriginRootEntityId = ResolveEntityId(ignoreOriginRoot),
                IgnoreTargetRootEntityId = ResolveEntityId(ignoreTargetRoot),
                IgnoreOriginBodyEntityId = ResolveAttachedBodyEntityId(ignoreOriginRoot),
                IgnoreTargetBodyEntityId = ResolveAttachedBodyEntityId(ignoreTargetRoot)
            };

            if (TryFindCachedResult(queryKey, out _))
                return;

            StoreCachedResult(queryKey, BuildDistanceOcclusionResult(sourcePosition, listenerPosition));
        }

        public static void PrimeForwardEchoSample(
            Vector3 originPosition,
            Vector3 forwardDirection,
            float probeDistance,
            int layerMask,
            Transform ignoreRoot)
        {
            float directionLengthSq = forwardDirection.sqrMagnitude;
            if (directionLengthSq <= MinimumPathDistanceMeters * MinimumPathDistanceMeters)
                return;

            int resolvedLayerMask = ResolveSensoryLayerMask(layerMask);
            ForwardEchoKey queryKey = new ForwardEchoKey
            {
                OriginPosition = originPosition,
                ProbeDistance = math.max(1f, probeDistance),
                ForwardDirection = ResolveDominantForwardDirection(forwardDirection),
                LayerMask = resolvedLayerMask,
                IgnoreRootEntityId = ResolveEntityId(ignoreRoot),
                IgnoreBodyEntityId = ResolveAttachedBodyEntityId(ignoreRoot)
            };

            if (TryFindCachedForwardEchoResult(queryKey, out _))
                return;

            _cachedForwardEchoEntry.Key = queryKey;
            _cachedForwardEchoEntry.Result = BuildDistanceForwardEchoResult(queryKey);
            _cachedForwardEchoEntry.Valid = true;
        }

        public static bool TryGetCachedOcclusionPath(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            int layerMask,
            Transform ignoreOriginRoot,
            Transform ignoreTargetRoot,
            out AcousticOcclusionResult result)
        {
            int resolvedLayerMask = ResolveSensoryLayerMask(layerMask);
            QueryKey queryKey = new QueryKey
            {
                SourcePosition = sourcePosition,
                ListenerPosition = listenerPosition,
                LayerMask = resolvedLayerMask,
                IgnoreOriginRootEntityId = ResolveEntityId(ignoreOriginRoot),
                IgnoreTargetRootEntityId = ResolveEntityId(ignoreTargetRoot),
                IgnoreOriginBodyEntityId = ResolveAttachedBodyEntityId(ignoreOriginRoot),
                IgnoreTargetBodyEntityId = ResolveAttachedBodyEntityId(ignoreTargetRoot)
            };

            if (TryFindCachedResult(queryKey, out result))
            {
                return true;
            }

            result = BuildDistanceOcclusionResult(sourcePosition, listenerPosition);
            StoreCachedResult(queryKey, result);
            return true;
        }

        public static bool TryGetCachedEnclosureSample(
            Vector3 originPosition,
            float probeDistance,
            int layerMask,
            Transform ignoreRoot,
            out AcousticEnclosureResult result)
        {
            _ = originPosition;
            _ = layerMask;
            _ = ignoreRoot;
            result = _triggerPresetActive
                ? _triggerPresetResult
                : BuildOpenWaterResult(probeDistance);
            return true;
        }

        public static void SetTriggerReverbPreset(AcousticReverbPresetKind preset)
        {
            if (preset == AcousticReverbPresetKind.OpenWater)
            {
                ClearTriggerReverbPreset();
                return;
            }

            _triggerPresetResult = BuildTriggerPresetResult(preset);
            _triggerPresetActive = true;
        }

        public static void ClearTriggerReverbPreset()
        {
            _triggerPresetActive = false;
            _triggerPresetResult = BuildOpenWaterResult(1f);
        }

        public static bool TryGetCachedForwardEchoSample(
            Vector3 originPosition,
            Vector3 forwardDirection,
            float probeDistance,
            int layerMask,
            Transform ignoreRoot,
            out AcousticForwardEchoResult result)
        {
            float directionLengthSq = forwardDirection.sqrMagnitude;
            if (directionLengthSq <= MinimumPathDistanceMeters * MinimumPathDistanceMeters)
            {
                result = default;
                return false;
            }

            int resolvedLayerMask = ResolveSensoryLayerMask(layerMask);
            ForwardEchoKey queryKey = new ForwardEchoKey
            {
                OriginPosition = originPosition,
                ProbeDistance = math.max(1f, probeDistance),
                ForwardDirection = ResolveDominantForwardDirection(forwardDirection),
                LayerMask = resolvedLayerMask,
                IgnoreRootEntityId = ResolveEntityId(ignoreRoot),
                IgnoreBodyEntityId = ResolveAttachedBodyEntityId(ignoreRoot)
            };

            if (TryFindCachedForwardEchoResult(queryKey, out result))
                return true;

            result = BuildDistanceForwardEchoResult(queryKey);
            _cachedForwardEchoEntry.Key = queryKey;
            _cachedForwardEchoEntry.Result = result;
            _cachedForwardEchoEntry.Valid = true;
            return true;
        }

        private static Vector3 ResolveDominantForwardDirection(Vector3 direction)
        {
            float ax = math.abs(direction.x);
            float ay = math.abs(direction.y);
            float az = math.abs(direction.z);
            if (ax >= ay && ax >= az)
                return new Vector3(direction.x < 0f ? -1f : 1f, 0f, 0f);

            if (ay >= az)
                return new Vector3(0f, direction.y < 0f ? -1f : 1f, 0f);

            return new Vector3(0f, 0f, direction.z < 0f ? -1f : 1f);
        }

        private static float FastTransmissionDecay(float x)
        {
            float clamped = math.max(0f, x);
            float x2 = clamped * clamped;
            return math.saturate(1f / (1f + clamped + (0.48f * x2) + (0.235f * x2 * clamped)));
        }

        public static AcousticOcclusionResult EvaluateOcclusionPath(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            int layerMask,
            Transform ignoreOriginRoot,
            Transform ignoreTargetRoot)
        {
            if (TryGetCachedOcclusionPath(
                    sourcePosition,
                    listenerPosition,
                    layerMask,
                    ignoreOriginRoot,
                    ignoreTargetRoot,
                    out AcousticOcclusionResult result))
            {
                return result;
            }

            PrimeOcclusionPath(
                sourcePosition,
                listenerPosition,
                layerMask,
                ignoreOriginRoot,
                ignoreTargetRoot);
            return new AcousticOcclusionResult(1f, OpenLowPassCutoffHertz, 0);
        }

        private static AcousticOcclusionResult BuildDistanceOcclusionResult(
            Vector3 sourcePosition,
            Vector3 listenerPosition)
        {
            float distanceMeters = ResolveAupDistanceMeters(sourcePosition, listenerPosition);
            if (distanceMeters <= MinimumPathDistanceMeters)
                return new AcousticOcclusionResult(1f, OpenLowPassCutoffHertz, 0);

            float shadow01 = ResolveDistanceShadow01(distanceMeters);
            float transmission01 = math.lerp(1f, DistanceOcclusionTransmissionFloor, shadow01);
            float lowPassCutoffHz = math.lerp(OpenLowPassCutoffHertz, DistanceOcclusionLowPassFloorHertz, shadow01);
            int occludingHitCount = shadow01 > 0.04f ? 1 : 0;

            ApplyFloraScattering(
                sourcePosition,
                listenerPosition,
                ref transmission01,
                ref lowPassCutoffHz,
                ref occludingHitCount);

            if (occludingHitCount <= 0)
                return new AcousticOcclusionResult(1f, OpenLowPassCutoffHertz, 0);

            return new AcousticOcclusionResult(
                math.clamp(transmission01, 0f, 1f),
                math.clamp(lowPassCutoffHz, MinimumLowPassCutoffHertz, OpenLowPassCutoffHertz),
                occludingHitCount);
        }

        private static AcousticForwardEchoResult BuildDistanceForwardEchoResult(ForwardEchoKey queryKey)
        {
            float fakeDistanceMeters = math.clamp(
                queryKey.ProbeDistance * ForwardEchoFakeDistanceRatio,
                ForwardEchoMinimumFakeDistanceMeters,
                ForwardEchoMaximumFakeDistanceMeters);
            float shadow01 = math.saturate(fakeDistanceMeters / ForwardEchoMaximumFakeDistanceMeters);
            float transmission01 = math.lerp(0.86f, 0.35f, shadow01);
            float lowPassCutoffHz = math.lerp(12000f, 900f, shadow01);
            return new AcousticForwardEchoResult(
                fakeDistanceMeters,
                math.clamp(transmission01, 0f, 1f),
                math.clamp(lowPassCutoffHz, MinimumLowPassCutoffHertz, OpenLowPassCutoffHertz),
                true);
        }

        private static float ResolveDistanceShadow01(float distanceMeters)
        {
            float distance01 = math.saturate(
                (distanceMeters - DistanceOcclusionStartMeters) /
                math.max(DistanceOcclusionFullMeters - DistanceOcclusionStartMeters, 0.001f));
            return distance01 * distance01 * (3f - (2f * distance01));
        }

        private static float ResolveAupDistanceMeters(Vector3 sourcePosition, Vector3 listenerPosition)
        {
            Vector3 sourceAup = HectonFloatingOrigin.ToAbsoluteUniversePosition(sourcePosition);
            Vector3 listenerAup = HectonFloatingOrigin.ToAbsoluteUniversePosition(listenerPosition);
            return ApproximateMagnitude3D((float3)(listenerAup - sourceAup));
        }

        private static void ApplyFloraScattering(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            ref float transmission01,
            ref float lowPassCutoffHz,
            ref int occludingHitCount)
        {
            Vector3 segment = listenerPosition - sourcePosition;
            float segmentLength = ApproximateMagnitude3D((float3)segment);
            if (segmentLength < FloraScatteringMinimumSegmentMeters)
                return;

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge == null)
                return;

            Vector3 direction = segment / segmentLength;
            int floraIntersections = 0;
            for (int sampleIndex = 1; sampleIndex <= FloraScatteringSampleCount; sampleIndex++)
            {
                float sampleT = sampleIndex / (float)(FloraScatteringSampleCount + 1);
                Vector3 samplePosition = sourcePosition + direction * (segmentLength * sampleT);
                float density = math.saturate(vegetationBridge.SampleBiomassDensityImmediate(samplePosition, KelpDensityTypeMask));
                if (density <= FloraScatteringDensityThreshold)
                    continue;

                float scattering01 = math.saturate((density - FloraScatteringDensityThreshold) / math.max(0.0001f, 1f - FloraScatteringDensityThreshold));
                transmission01 *= math.lerp(0.9f, 0.6f, scattering01);
                lowPassCutoffHz = math.lerp(lowPassCutoffHz, FloraScatteringLowPassFloorHertz, scattering01 * 0.72f);
                floraIntersections++;
            }

            if (floraIntersections <= 0)
                return;

            transmission01 = math.max(FloraScatteringTransmissionFloor, transmission01);
            occludingHitCount += floraIntersections;
        }

        private static float ApproximateMagnitude3D(float3 value)
        {
            float3 absoluteValue = math.abs(value);
            float maxAxis = math.max(absoluteValue.x, math.max(absoluteValue.y, absoluteValue.z));
            float minAxis = math.min(absoluteValue.x, math.min(absoluteValue.y, absoluteValue.z));
            float midAxis = absoluteValue.x + absoluteValue.y + absoluteValue.z - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375f + minAxis * 0.125f;
        }

        private static bool TryFindCachedResult(QueryKey queryKey, out AcousticOcclusionResult result)
        {
            for (int i = 0; i < MaxQueuedRequests; i++)
            {
                if (!_cachedEntries[i].Valid)
                    continue;

                if (!KeysMatch(_cachedEntries[i].Key, queryKey))
                    continue;

                result = _cachedEntries[i].Result;
                return true;
            }

            result = new AcousticOcclusionResult(1f, OpenLowPassCutoffHertz, 0);
            return false;
        }

        private static bool TryFindCachedForwardEchoResult(ForwardEchoKey queryKey, out AcousticForwardEchoResult result)
        {
            if (_cachedForwardEchoEntry.Valid && ForwardEchoKeysMatch(_cachedForwardEchoEntry.Key, queryKey))
            {
                result = _cachedForwardEchoEntry.Result;
                return true;
            }

            result = default;
            return false;
        }

        private static void StoreCachedResult(QueryKey queryKey, AcousticOcclusionResult result)
        {
            for (int i = 0; i < MaxQueuedRequests; i++)
            {
                if (_cachedEntries[i].Valid && KeysMatch(_cachedEntries[i].Key, queryKey))
                {
                    _cachedEntries[i].Result = result;
                    return;
                }
            }

            int writeIndex = _nextCacheWriteIndex;
            _cachedEntries[writeIndex].Key = queryKey;
            _cachedEntries[writeIndex].Result = result;
            _cachedEntries[writeIndex].Valid = true;
            _nextCacheWriteIndex = (writeIndex + 1) % MaxQueuedRequests;
        }

        private static bool KeysMatch(QueryKey cached, QueryKey current)
        {
            return cached.LayerMask == current.LayerMask &&
                   cached.IgnoreOriginRootEntityId == current.IgnoreOriginRootEntityId &&
                   cached.IgnoreTargetRootEntityId == current.IgnoreTargetRootEntityId &&
                   cached.IgnoreOriginBodyEntityId == current.IgnoreOriginBodyEntityId &&
                   cached.IgnoreTargetBodyEntityId == current.IgnoreTargetBodyEntityId &&
                   math.lengthsq((float3)(cached.SourcePosition - current.SourcePosition)) <= OcclusionReuseDistanceSqr &&
                   math.lengthsq((float3)(cached.ListenerPosition - current.ListenerPosition)) <= OcclusionReuseDistanceSqr;
        }

        private static bool ForwardEchoKeysMatch(ForwardEchoKey cached, ForwardEchoKey current)
        {
            return cached.LayerMask == current.LayerMask &&
                   cached.IgnoreRootEntityId == current.IgnoreRootEntityId &&
                   cached.IgnoreBodyEntityId == current.IgnoreBodyEntityId &&
                   math.abs(cached.ProbeDistance - current.ProbeDistance) <= 0.01f &&
                   math.lengthsq((float3)(cached.OriginPosition - current.OriginPosition)) <= ForwardEchoReuseDistanceSqr &&
                   math.dot((float3)cached.ForwardDirection, (float3)current.ForwardDirection) >= ForwardEchoDirectionReuseDot;
        }

        private static AcousticEnclosureResult BuildOpenWaterResult(float probeDistance)
        {
            _ = probeDistance;
            float span = OpenWaterPresetSpanMeters;
            float volume = math.max(0.01f, span * span * span);
            float faceArea = span * span;
            float totalArea = faceArea * EnclosurePresetFaceCount;
            float equivalentAbsorptionArea = math.max(MinimumEquivalentAbsorptionArea, totalArea * WaterAbsorption01);
            return new AcousticEnclosureResult(
                span,
                span,
                span,
                volume,
                WaterAbsorption01,
                equivalentAbsorptionArea,
                OpenWaterRt60Seconds,
                OpenWaterWetMix01,
                1f,
                0);
        }

        private static AcousticEnclosureResult BuildTriggerPresetResult(AcousticReverbPresetKind preset)
        {
            float span = preset == AcousticReverbPresetKind.LargeRoom
                ? LargeRoomSpanMeters
                : SmallRoomSpanMeters;
            float volume = math.max(0.01f, span * span * span);
            float faceArea = span * span;
            float totalArea = faceArea * EnclosurePresetFaceCount;
            float meanAbsorption = preset == AcousticReverbPresetKind.LargeRoom ? 0.58f : 0.72f;
            float equivalentAbsorptionArea = math.max(MinimumEquivalentAbsorptionArea, totalArea * meanAbsorption);
            float rt60Seconds = preset == AcousticReverbPresetKind.LargeRoom
                ? LargeRoomRt60Seconds
                : SmallRoomRt60Seconds;
            float wetMix01 = preset == AcousticReverbPresetKind.LargeRoom
                ? LargeRoomWetMix01
                : SmallRoomWetMix01;
            float openness01 = preset == AcousticReverbPresetKind.LargeRoom
                ? LargeRoomOpenness01
                : SmallRoomOpenness01;
            return new AcousticEnclosureResult(
                span,
                span,
                span,
                volume,
                meanAbsorption,
                equivalentAbsorptionArea,
                math.clamp(rt60Seconds, MinimumRt60Seconds, MaximumRt60Seconds),
                wetMix01,
                openness01,
                EnclosurePresetFaceCount);
        }

        private static float ResolveAbsorption01(Collider collider)
        {
            if (collider == null)
                return DefaultAbsorption01;

            EnsureLayerCache();

            if (collider.CompareTag("MetalFloor") ||
                collider.CompareTag("Grate") ||
                collider.CompareTag("BaseModule") ||
                collider.CompareTag("Vehicle"))
            {
                return MetalAbsorption01;
            }

            if (collider.CompareTag("Sand") || collider.CompareTag("Wet"))
                return SedimentAbsorption01;

            if (collider.CompareTag("Rock"))
                return RockAbsorption01;

            int layer = collider.gameObject.layer;
            if (layer == WaterLayer)
                return WaterAbsorption01;

            if (layer == BaseModuleLayer || layer == VehicleLayer)
                return MetalAbsorption01;

            if (layer == VoxelCaveLayer)
                return RockAbsorption01;

            return DefaultAbsorption01;
        }

        internal static AcousticSurfaceResponse ResolveSurfaceResponse(Collider collider)
        {
            float absorption01 = math.clamp(ResolveAbsorption01(collider), 0f, 1f);
            float transmission01 = math.clamp(1f - absorption01, 0f, 1f);
            float lowPassCutoffHz = math.lerp(
                MinimumLowPassCutoffHertz,
                OpenLowPassCutoffHertz,
                transmission01);
            return new AcousticSurfaceResponse(absorption01, transmission01, lowPassCutoffHz);
        }

        private static ulong ResolveEntityId(Transform target)
        {
            return target != null ? EntityId.ToULong(target.GetEntityId()) : 0ul;
        }

        private static int ResolveSensoryLayerMask(int layerMask)
        {
            if (layerMask == 0)
                return 0;

            int sensoryMask = BuildSensoryMask();
            if (HectonLayerMasks.IsEverythingLayerMask(layerMask))
                return sensoryMask;

            return layerMask & sensoryMask;
        }

        private static ulong ResolveAttachedBodyEntityId(Transform target)
        {
            if (target == null || !target.TryGetComponent(out Rigidbody body))
                return 0ul;

            return EntityId.ToULong(body.GetEntityId());
        }

        private static int LayerBit(int layer)
        {
            return layer >= 0 ? 1 << layer : 0;
        }

    }
}
