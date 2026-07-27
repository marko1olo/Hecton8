using System.Runtime.InteropServices;
using Hecton8.Caves;
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
        public byte HasHit;

        public AcousticForwardEchoResult(float hitDistanceMeters, float transmission01, float lowPassCutoffHz, bool hasHit)
        {
            HitDistanceMeters = hitDistanceMeters;
            Transmission01 = transmission01;
            LowPassCutoffHz = lowPassCutoffHz;
            HasHit = hasHit ? (byte)1 : (byte)0;
        }
    }

    internal readonly struct AcousticSurfaceResponse
    {
        public readonly float Absorption01;
        public readonly float Transmission01;
        public readonly float LowPassCutoffHz;

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

        private const float RockAbsorption01 = 0.98f;
        private const float WaterAbsorption01 = 0.05f;
        private const int MaxQueuedRequests = 64;
        private const int MaxQueuedRequestsMask = MaxQueuedRequests - 1;
        private const int EnclosurePresetFaceCount = 6;
        private const float OcclusionReuseDistanceMeters = 0.5f;
        private const float OcclusionReuseDistanceSqr = OcclusionReuseDistanceMeters * OcclusionReuseDistanceMeters;
        private const float MinimumPathDistanceMeters = 0.001f;
        private const float DistanceOcclusionStartMeters = 28f;
        private const float DistanceOcclusionFullMeters = 180f;
        private const float DistanceOcclusionTransmissionFloor = 0.32f;
        private const float DistanceOcclusionLowPassFloorHertz = 900f;
        private const float SdfOcclusionProbeMaxDistanceMeters = 200f;
        private const float SdfOcclusionProbeStepMeters = 5f;
        private const float SdfOcclusionTransmission01 = 0.18f;
        private const float SdfOcclusionLowPassHertz = 800f;
        private const float SdfEnclosureProbeDistanceMeters = 200f;
        private const float SdfEnclosureProbeStepMeters = 5f;
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
        private const int MaxQueuedRequestsPowerOfTwoGuard =
            1 / ((MaxQueuedRequests > 0 &&
                  (MaxQueuedRequests & (MaxQueuedRequests - 1)) == 0 &&
                  MaxQueuedRequestsMask == MaxQueuedRequests - 1) ? 1 : 0);

        private static int PlayerLayer = -1;
        private static int TriggerZoneLayer = -1;
        private static int TransparentFxLayer = -1;
        private static int FirstPersonToolsLayer = -1;
        private static int VoxelCaveLayer = -1;
        private static int BaseModuleLayer = -1;
        private static int VehicleLayer = -1;
        private static int WaterLayer = -1;
        private static bool _layerCacheInitialized;

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct QueryKey
        {
            [FieldOffset(0)]
            public Vector3 SourcePosition;
            [FieldOffset(12)]
            public int LayerMask;
            [FieldOffset(16)]
            public Vector3 ListenerPosition;
            [FieldOffset(28)]
            private int _padding0;
            [FieldOffset(32)]
            public ulong IgnoreOriginRootEntityId;
            [FieldOffset(40)]
            public ulong IgnoreTargetRootEntityId;
            [FieldOffset(48)]
            public ulong IgnoreOriginBodyEntityId;
            [FieldOffset(56)]
            public ulong IgnoreTargetBodyEntityId;
        }

        private struct CachedQueryEntry
        {
            public QueryKey Key;
            public AcousticOcclusionResult Result;
            public byte Valid;
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct ForwardEchoKey
        {
            [FieldOffset(0)]
            public Vector3 OriginPosition;
            [FieldOffset(12)]
            public float ProbeDistance;
            [FieldOffset(16)]
            public Vector3 ForwardDirection;
            [FieldOffset(28)]
            public int LayerMask;
            [FieldOffset(32)]
            public ulong IgnoreRootEntityId;
            [FieldOffset(40)]
            public ulong IgnoreBodyEntityId;
        }

        private struct CachedForwardEchoEntry
        {
            public ForwardEchoKey Key;
            public AcousticForwardEchoResult Result;
            public byte Valid;
        }

        private static int _runtimeOwnerCount;
        private static int _nextCacheWriteIndex;
        private static bool _triggerPresetActive;
        private static AcousticEnclosureResult _triggerPresetResult;
        private static CachedForwardEchoEntry _cachedForwardEchoEntry;
        // COLD ALLOC: CachedQueryEntry[64] - last resolved acoustic occlusion cache - owner: AcousticOcclusionUtility
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
            _cachedForwardEchoEntry.Valid = 0;

            for (int i = 0; i < MaxQueuedRequests; i++)
                _cachedEntries[i].Valid = 0;

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
            _cachedForwardEchoEntry.Valid = 0;

            for (int i = 0; i < MaxQueuedRequests; i++)
                _cachedEntries[i].Valid = 0;
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

            AcousticOcclusionResult fallbackResult = TryBuildSdfOcclusionResult(sourcePosition, listenerPosition, out AcousticOcclusionResult sdfResult)
                ? sdfResult
                : BuildDistanceOcclusionResult(sourcePosition, listenerPosition);

            StoreCachedResult(queryKey, fallbackResult);
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
            _cachedForwardEchoEntry.Valid = 1;
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

            result = TryBuildSdfOcclusionResult(sourcePosition, listenerPosition, out AcousticOcclusionResult sdfResult)
                ? sdfResult
                : BuildDistanceOcclusionResult(sourcePosition, listenerPosition);
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
            _ = layerMask;
            _ = ignoreRoot;
            if (_triggerPresetActive)
            {
                result = _triggerPresetResult;
                return true;
            }

            if (TryBuildSdfEnclosureResult(originPosition, math.max(probeDistance, SdfEnclosureProbeDistanceMeters), out result))
                return true;

            result = BuildOpenWaterResult(probeDistance);
            return true;
        }

        public static bool TryGetSdfEnclosureSample(
            Vector3 originPosition,
            float probeDistance,
            out AcousticEnclosureResult result)
        {
            if (_triggerPresetActive)
            {
                result = _triggerPresetResult;
                return true;
            }

            return TryBuildSdfEnclosureResult(
                originPosition,
                math.max(probeDistance, SdfEnclosureProbeDistanceMeters),
                out result);
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
            _cachedForwardEchoEntry.Valid = 1;
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

        private static bool TryBuildSdfOcclusionResult(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            out AcousticOcclusionResult result)
        {
            result = default;
            float3 delta = (float3)(listenerPosition - sourcePosition);
            float distanceSq = math.lengthsq(delta);
            if (distanceSq <= MinimumPathDistanceMeters * MinimumPathDistanceMeters)
                return false;

            if (TryBuildSdfMidpointOcclusionResult(sourcePosition, listenerPosition, distanceSq, out result))
                return true;

            float inverseDistance = math.rsqrt(math.max(distanceSq, MinimumPathDistanceMeters * MinimumPathDistanceMeters));
            float distanceMeters = distanceSq * inverseDistance;
            float probeDistance = math.min(distanceMeters, SdfOcclusionProbeMaxDistanceMeters);
            Vector3 direction = (Vector3)(delta * inverseDistance);
            if (!HectonVoxelVolume.TryRaymarchAnyPublishedSdf(
                    sourcePosition,
                    direction,
                    probeDistance,
                    SdfOcclusionProbeStepMeters,
                    out _,
                    out VoxelSdfRaycastHit hit) ||
                hit.Hit == 0)
            {
                return false;
            }

            float hitDepth01 = math.saturate(hit.Distance * math.rcp(math.max(probeDistance, MinimumPathDistanceMeters)));
            float transmission01 = math.lerp(SdfOcclusionTransmission01, SdfOcclusionTransmission01 * 0.55f, hitDepth01);
            float lowPassCutoffHz = math.lerp(SdfOcclusionLowPassHertz, MinimumLowPassCutoffHertz, hitDepth01 * 0.35f);
            result = new AcousticOcclusionResult(
                math.clamp(transmission01, 0f, 1f),
                math.clamp(lowPassCutoffHz, MinimumLowPassCutoffHertz, OpenLowPassCutoffHertz),
                2);
            return true;
        }

        private static bool TryBuildSdfMidpointOcclusionResult(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            float distanceSq,
            out AcousticOcclusionResult result)
        {
            result = default;
            Vector3 midpointRuntime = sourcePosition + (listenerPosition - sourcePosition) * 0.5f;
            if (!TryResolveAupFromRuntimeOrigin(midpointRuntime, out AbsoluteUniversePosition midpointPositionAup))
                return false;

            double3 midpointAup = midpointPositionAup.ToAbsoluteDouble3();
            if (!HectonVoxelVolume.GetSDFDensity(midpointAup, out float density) || !(density > 0f))
                return false;

            float density01 = math.saturate(density);
            float distance01 = math.saturate(distanceSq * math.rcp(SdfOcclusionProbeMaxDistanceMeters * SdfOcclusionProbeMaxDistanceMeters));
            float obstruction01 = math.saturate(math.max(density01, distance01 * 0.35f));
            float transmission01 = math.lerp(SdfOcclusionTransmission01, SdfOcclusionTransmission01 * 0.5f, obstruction01);

            float thicknessCm = obstruction01 * 100f;
            float materialDensity = density01;
            float lowPassCutoffHz = Hecton8.PureLogic.Systems.SoundObstructionLowpassCutoffCalculator.Compute(SdfOcclusionLowPassHertz, thicknessCm, materialDensity);

            result = new AcousticOcclusionResult(
                math.clamp(transmission01, 0f, 1f),
                math.clamp(lowPassCutoffHz, MinimumLowPassCutoffHertz, OpenLowPassCutoffHertz),
                2);
            return true;
        }

        private static bool TryBuildSdfEnclosureResult(
            Vector3 originPosition,
            float probeDistance,
            out AcousticEnclosureResult result)
        {
            result = default;
            float safeProbeDistance = math.clamp(probeDistance, 1f, SdfEnclosureProbeDistanceMeters);
            float up = ResolveSdfCardinalDistance(originPosition, new Vector3(0f, 1f, 0f), safeProbeDistance, out int upHit);
            float down = ResolveSdfCardinalDistance(originPosition, new Vector3(0f, -1f, 0f), safeProbeDistance, out int downHit);
            float left = ResolveSdfCardinalDistance(originPosition, new Vector3(-1f, 0f, 0f), safeProbeDistance, out int leftHit);
            float right = ResolveSdfCardinalDistance(originPosition, new Vector3(1f, 0f, 0f), safeProbeDistance, out int rightHit);
            float forward = ResolveSdfCardinalDistance(originPosition, new Vector3(0f, 0f, 1f), safeProbeDistance, out int forwardHit);
            float back = ResolveSdfCardinalDistance(originPosition, new Vector3(0f, 0f, -1f), safeProbeDistance, out int backHit);
            int hitCount = upHit + downHit + leftHit + rightHit + forwardHit + backHit;
            if (hitCount <= 0)
                return false;

            float spanVertical = math.max(MinimumPathDistanceMeters, up + down);
            float spanHorizontal = math.max(MinimumPathDistanceMeters, left + right);
            float spanDepth = math.max(MinimumPathDistanceMeters, forward + back);
            float volumeCubicMeters = math.max(MinimumPathDistanceMeters, spanVertical * spanHorizontal * spanDepth);
            float areaVerticalHorizontal = spanVertical * spanHorizontal;
            float areaVerticalDepth = spanVertical * spanDepth;
            float areaHorizontalDepth = spanHorizontal * spanDepth;
            float surfaceArea = math.max(
                MinimumEquivalentAbsorptionArea,
                2f * (areaVerticalHorizontal + areaVerticalDepth + areaHorizontalDepth));
            float closure01 = math.saturate(hitCount * 0.16666667f);
            float meanAbsorption01 = math.lerp(WaterAbsorption01, RockAbsorption01, closure01);
            float equivalentAbsorptionArea = math.max(MinimumEquivalentAbsorptionArea, surfaceArea * meanAbsorption01);
            float rt60Seconds = math.clamp(
                0.161f * volumeCubicMeters * math.rcp(equivalentAbsorptionArea),
                MinimumRt60Seconds,
                MaximumRt60Seconds);
            float wetMix01 = math.lerp(OpenWaterWetMix01, SmallRoomWetMix01, closure01);
            float openness01 = 1f - closure01;

            result = new AcousticEnclosureResult(
                spanVertical,
                spanHorizontal,
                spanDepth,
                volumeCubicMeters,
                meanAbsorption01,
                equivalentAbsorptionArea,
                rt60Seconds,
                wetMix01,
                openness01,
                hitCount);
            return true;
        }

        private static float ResolveSdfCardinalDistance(
            Vector3 originPosition,
            Vector3 direction,
            float probeDistance,
            out int hit)
        {
            hit = 0;
            if (HectonVoxelVolume.TryRaymarchAnyPublishedSdf(
                    originPosition,
                    direction,
                    probeDistance,
                    SdfEnclosureProbeStepMeters,
                    out _,
                    out VoxelSdfRaycastHit sdfHit) &&
                sdfHit.Hit != 0)
            {
                hit = 1;
                return math.max(MinimumPathDistanceMeters, sdfHit.Distance);
            }

            return probeDistance;
        }

        private static AcousticForwardEchoResult BuildDistanceForwardEchoResult(ForwardEchoKey queryKey)
        {
            float fakeDistanceMeters = math.clamp(
                queryKey.ProbeDistance * ForwardEchoFakeDistanceRatio,
                ForwardEchoMinimumFakeDistanceMeters,
                ForwardEchoMaximumFakeDistanceMeters);
            float shadow01 = math.saturate(fakeDistanceMeters * math.rcp(ForwardEchoMaximumFakeDistanceMeters));
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
                (distanceMeters - DistanceOcclusionStartMeters) *
                math.rcp(math.max(DistanceOcclusionFullMeters - DistanceOcclusionStartMeters, 0.001f)));
            return distance01 * distance01 * (3f - (2f * distance01));
        }

        private static float ResolveAupDistanceMeters(Vector3 sourcePosition, Vector3 listenerPosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(sourcePosition, out AbsoluteUniversePosition sourceAup) ||
                !TryResolveAupFromRuntimeOrigin(listenerPosition, out AbsoluteUniversePosition listenerAup))
            {
                return float.MaxValue;
            }

            double distanceSq = AbsoluteUniversePosition.DistanceSq(in sourceAup, in listenerAup);
            if (distanceSq <= 0d)
                return 0f;

            if (!math.isfinite(distanceSq))
                return float.MaxValue;

            double distanceMeters = distanceSq * math.rsqrt(distanceSq);
            return distanceMeters >= float.MaxValue ? float.MaxValue : (float)distanceMeters;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) || !math.isfinite(runtimePosition.y) || !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in positionAup);
        }

        private static void ApplyFloraScattering(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            ref float transmission01,
            ref float lowPassCutoffHz,
            ref int occludingHitCount)
        {
            Vector3 segment = listenerPosition - sourcePosition;
            if (math.lengthsq((float3)segment) < FloraScatteringMinimumSegmentMeters * FloraScatteringMinimumSegmentMeters)
                return;

            HectonMapMagicVegetationBridge vegetationBridge = null;
            if (!WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge))
                return;

            int floraIntersections = 0;
            float sampleTStep = math.rcp((float)(FloraScatteringSampleCount + 1));
            float scatteringRangeInv = math.rcp(math.max(0.0001f, 1f - FloraScatteringDensityThreshold));
            for (int sampleIndex = 1; sampleIndex <= FloraScatteringSampleCount; sampleIndex++)
            {
                float sampleT = sampleIndex * sampleTStep;
                Vector3 samplePosition = sourcePosition + segment * sampleT;
                float density = math.saturate(vegetationBridge.SampleBiomassDensityImmediate(samplePosition, KelpDensityTypeMask));
                if (density <= FloraScatteringDensityThreshold)
                    continue;

                float scattering01 = math.saturate((density - FloraScatteringDensityThreshold) * scatteringRangeInv);
                transmission01 *= math.lerp(0.9f, 0.6f, scattering01);
                lowPassCutoffHz = math.lerp(lowPassCutoffHz, FloraScatteringLowPassFloorHertz, scattering01 * 0.72f);
                floraIntersections++;
            }

            if (floraIntersections <= 0)
                return;

            transmission01 = math.max(FloraScatteringTransmissionFloor, transmission01);
            occludingHitCount += floraIntersections;
        }

        private static bool TryFindCachedResult(QueryKey queryKey, out AcousticOcclusionResult result)
        {
            for (int i = 0; i < MaxQueuedRequests; i++)
            {
                if (_cachedEntries[i].Valid == 0)
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
            if (_cachedForwardEchoEntry.Valid != 0 && ForwardEchoKeysMatch(_cachedForwardEchoEntry.Key, queryKey))
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
                if (_cachedEntries[i].Valid != 0 && KeysMatch(_cachedEntries[i].Key, queryKey))
                {
                    _cachedEntries[i].Result = result;
                    return;
                }
            }

            int writeIndex = _nextCacheWriteIndex;
            _cachedEntries[writeIndex].Key = queryKey;
            _cachedEntries[writeIndex].Result = result;
            _cachedEntries[writeIndex].Valid = 1;
            _nextCacheWriteIndex = (writeIndex + 1) & MaxQueuedRequestsMask;
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
