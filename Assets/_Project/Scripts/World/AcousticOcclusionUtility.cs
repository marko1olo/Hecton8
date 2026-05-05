using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using Unity.Jobs;
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

    internal struct AcousticVoxelOcclusionResult
    {
        public float AccumulatedDensity;
        public float Transmission01;
        public float LowPassCutoffHz;
        public int SampledVoxelCount;

        public AcousticVoxelOcclusionResult(float accumulatedDensity, float transmission01, float lowPassCutoffHz, int sampledVoxelCount)
        {
            AccumulatedDensity = accumulatedDensity;
            Transmission01 = transmission01;
            LowPassCutoffHz = lowPassCutoffHz;
            SampledVoxelCount = sampledVoxelCount;
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
        private const int MaxOcclusionHits = 8;
        private const int MaxQueuedRequests = 48;
        private const int EnclosureProbeCount = 6;
        private const float OcclusionReuseDistanceMeters = 0.5f;
        private const float OcclusionReuseDistanceSqr = OcclusionReuseDistanceMeters * OcclusionReuseDistanceMeters;
        private const float MinimumProbeDistanceMeters = 0.001f;
        private const float MinimumEquivalentAbsorptionArea = 0.5f;
        private const float MinimumRt60Seconds = 0.12f;
        private const float MaximumRt60Seconds = 10f;
        private const float OpenWaterPresetSpanMeters = 96f;
        private const float OpenWaterRt60Seconds = 10f;
        private const float OpenWaterWetMix01 = 0.85f;
        private const float SmallRoomSpanMeters = 6f;
        private const float LargeRoomSpanMeters = 18f;
        private const float SmallRoomRt60Seconds = 0.48f;
        private const float LargeRoomRt60Seconds = 1.35f;
        private const float SmallRoomWetMix01 = 0.28f;
        private const float LargeRoomWetMix01 = 0.48f;
        private const float SmallRoomOpenness01 = 0.16f;
        private const float LargeRoomOpenness01 = 0.36f;
        private const float ForwardEchoReuseDistanceMeters = 2f;
        private const float ForwardEchoReuseDistanceSqr = ForwardEchoReuseDistanceMeters * ForwardEchoReuseDistanceMeters;
        private const float ForwardEchoDirectionReuseDot = 0.9961947f;
        private const int KelpDensityTypeMask = 1 << 1;
        private const int FloraScatteringSampleCount = 5;
        private const float FloraScatteringMinimumSegmentMeters = 3f;
        private const float FloraScatteringDensityThreshold = 0.08f;
        private const float FloraScatteringTransmissionFloor = 0.18f;
        private const float FloraScatteringLowPassFloorHertz = 220f;
        private const byte CaveSignedDistanceSolidThreshold = 128;
        private const float CaveVoxelDdaEpsilon = 0.000001f;
        private const float VoxelDensityTransmissionScale = 1.65f;
        private const float VoxelDensityLowPassScale = 7.5f;
        private const float VoxelDensityHeavyOcclusionThreshold01 = 0.78f;
        private const float VoxelDensityHeavyLowPassStartHertz = 650f;
        private const float VoxelDensityHardLowPassCutoffHertz = 300f;
        private const int VoxelDensityMaximumDdaSteps = 4096;

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

        private struct QueryFrameEntry
        {
            public QueryKey Key;
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
        private static bool _queryBatchScheduled;
        private static int _queuedFrame = -1;
        private static int _queuedCount;
        private static int _scheduledCount;
        private static int _nextCacheWriteIndex;
        private static JobHandle _pendingQueryHandle;
        private static JobHandle _pendingForwardEchoHandle;
        private static bool _forwardEchoQueryScheduled;
        private static bool _queuedForwardEchoValid;
        private static bool _scheduledForwardEchoValid;
        private static bool _triggerPresetActive;
        private static AcousticEnclosureResult _triggerPresetResult;
        private static ForwardEchoKey _queuedForwardEchoKey;
        private static ForwardEchoKey _scheduledForwardEchoKey;
        private static CachedForwardEchoEntry _cachedForwardEchoEntry;
        // COLD ALLOC: QueryFrameEntry[48] - queued cross-frame acoustic occlusion requests - owner: AcousticOcclusionUtility
        private static readonly QueryFrameEntry[] _queuedEntries = new QueryFrameEntry[MaxQueuedRequests];
        // COLD ALLOC: QueryFrameEntry[48] - scheduled acoustic occlusion request snapshot - owner: AcousticOcclusionUtility
        private static readonly QueryFrameEntry[] _scheduledEntries = new QueryFrameEntry[MaxQueuedRequests];
        // COLD ALLOC: CachedQueryEntry[48] - last resolved acoustic occlusion cache - owner: AcousticOcclusionUtility
        private static readonly CachedQueryEntry[] _cachedEntries = new CachedQueryEntry[MaxQueuedRequests];
        // COLD ALLOC: NativeList<RaycastCommand>[48] - deferred acoustic occlusion batch command buffer - owner: AcousticOcclusionUtility
        private static NativeList<RaycastCommand> _queryCommands;
        // COLD ALLOC: NativeArray<RaycastHit>[384] - deferred acoustic occlusion batch result buffer - owner: AcousticOcclusionUtility
        private static NativeArray<RaycastHit> _queryResults;
        // COLD ALLOC: NativeArray<RaycastCommand>[1] - transient forward-echo acoustic probe command buffer - owner: AcousticOcclusionUtility
        private static NativeArray<RaycastCommand> _forwardEchoCommands;
        // COLD ALLOC: NativeArray<RaycastHit>[1] - transient forward-echo acoustic probe result buffer - owner: AcousticOcclusionUtility
        private static NativeArray<RaycastHit> _forwardEchoResults;

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

            JobHandle teardownDependency = CancelPendingHandlesForTeardown();
            DisposeRuntimeBuffers(teardownDependency);

            _runtimeOwnerCount = 0;
            _queryBatchScheduled = false;
            _queuedFrame = -1;
            _queuedCount = 0;
            _scheduledCount = 0;
            _nextCacheWriteIndex = 0;
            _pendingQueryHandle = default;
            _pendingForwardEchoHandle = default;
            _queryCommands = default;
            _queryResults = default;
            _forwardEchoCommands = default;
            _forwardEchoResults = default;
            _forwardEchoQueryScheduled = false;
            _queuedForwardEchoValid = false;
            _scheduledForwardEchoValid = false;
            _triggerPresetActive = false;
            _triggerPresetResult = BuildOpenWaterResult(1f);
            _cachedForwardEchoEntry.Valid = false;

            for (int i = 0; i < MaxQueuedRequests; i++)
                _cachedEntries[i].Valid = false;

        }

        public static void AcquireRuntime()
        {
            _runtimeOwnerCount++;
            EnsureRuntimeBuffers();
        }

        public static void ReleaseRuntime()
        {
            if (_runtimeOwnerCount <= 0)
                return;

            _runtimeOwnerCount--;
            if (_runtimeOwnerCount > 0)
                return;

            _queryBatchScheduled = false;
            _queuedFrame = -1;
            _queuedCount = 0;
            _scheduledCount = 0;
            _nextCacheWriteIndex = 0;

            JobHandle teardownDependency = CancelPendingHandlesForTeardown();
            DisposeRuntimeBuffers(teardownDependency);
            _forwardEchoQueryScheduled = false;
            _queuedForwardEchoValid = false;
            _scheduledForwardEchoValid = false;
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
        /// Recovers and schedules acoustic raycast jobs in the dispatcher-owned late-frame swap window.
        /// </summary>
        public static void LateFrameTick()
        {
            if (_runtimeOwnerCount <= 0)
                return;

            EnsureRuntimeBuffers();
            TryConsumeCompletedQuery();
            TryConsumeCompletedForwardEchoQuery();

            if (!_queryBatchScheduled && _queuedCount > 0)
                ScheduleQueuedBatch();

            if (!_forwardEchoQueryScheduled && _queuedForwardEchoValid)
                ScheduleQueuedForwardEchoBatch();

            _queuedFrame = Time.frameCount;
            _queuedCount = 0;
        }

        public static void PrimeOcclusionPath(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            int layerMask,
            Transform ignoreOriginRoot,
            Transform ignoreTargetRoot)
        {
            EnsureRuntimeBuffers();
            AdvanceFrameFence();
            if (!_queryCommands.IsCreated || !_queryResults.IsCreated)
                return;

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

            Vector3 delta = listenerPosition - sourcePosition;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
            {
                StoreCachedResult(queryKey, new AcousticOcclusionResult(1f, OpenLowPassCutoffHertz, 0));
                return;
            }

            if (TryFindCachedResult(queryKey, out _))
                return;

            for (int i = 0; i < _queuedCount; i++)
            {
                if (KeysMatch(_queuedEntries[i].Key, queryKey))
                    return;
            }

            if (_queuedCount >= MaxQueuedRequests)
                return;

            _queuedEntries[_queuedCount].Key = queryKey;
            _queuedCount++;
        }

        public static void PrimeForwardEchoSample(
            Vector3 originPosition,
            Vector3 forwardDirection,
            float probeDistance,
            int layerMask,
            Transform ignoreRoot)
        {
            EnsureRuntimeBuffers();
            AdvanceFrameFence();
            if (!_forwardEchoCommands.IsCreated || !_forwardEchoResults.IsCreated)
                return;

            float directionLengthSq = forwardDirection.sqrMagnitude;
            if (directionLengthSq <= MinimumProbeDistanceMeters * MinimumProbeDistanceMeters)
                return;

            int resolvedLayerMask = ResolveSensoryLayerMask(layerMask);
            ForwardEchoKey queryKey = new ForwardEchoKey
            {
                OriginPosition = originPosition,
                ProbeDistance = math.max(1f, probeDistance),
                ForwardDirection = forwardDirection / math.sqrt(directionLengthSq),
                LayerMask = resolvedLayerMask,
                IgnoreRootEntityId = ResolveEntityId(ignoreRoot),
                IgnoreBodyEntityId = ResolveAttachedBodyEntityId(ignoreRoot)
            };

            if (TryFindCachedForwardEchoResult(queryKey, out _))
                return;

            _queuedForwardEchoKey = queryKey;
            _queuedForwardEchoValid = true;
        }

        public static bool TryGetCachedOcclusionPath(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            int layerMask,
            Transform ignoreOriginRoot,
            Transform ignoreTargetRoot,
            out AcousticOcclusionResult result)
        {
            AdvanceFrameFence();

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

            result = new AcousticOcclusionResult(1f, OpenLowPassCutoffHertz, 0);
            return false;
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
            AdvanceFrameFence();
            float directionLengthSq = forwardDirection.sqrMagnitude;
            if (directionLengthSq <= MinimumProbeDistanceMeters * MinimumProbeDistanceMeters)
            {
                result = default;
                return false;
            }

            int resolvedLayerMask = ResolveSensoryLayerMask(layerMask);
            ForwardEchoKey queryKey = new ForwardEchoKey
            {
                OriginPosition = originPosition,
                ProbeDistance = math.max(1f, probeDistance),
                ForwardDirection = forwardDirection / math.sqrt(directionLengthSq),
                LayerMask = resolvedLayerMask,
                IgnoreRootEntityId = ResolveEntityId(ignoreRoot),
                IgnoreBodyEntityId = ResolveAttachedBodyEntityId(ignoreRoot)
            };

            return TryFindCachedForwardEchoResult(queryKey, out result);
        }

        public static bool TryTraceVoxelDensityOcclusion(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            out AcousticVoxelOcclusionResult result)
        {
            result = new AcousticVoxelOcclusionResult(0f, 1f, OpenLowPassCutoffHertz, 0);

            HectonCaveVoxelLightingVolume volume = HectonCaveVoxelLightingVolume.ActiveRuntimeInstance;
            if (volume == null ||
                !volume.TryGetPublishedSignedDistanceVoxelPayload(
                    out NativeArray<byte> signedDistanceVoxels,
                    out Vector3Int gridDimensions,
                    out Vector3 gridOrigin,
                    out Vector3 voxelCellSize))
            {
                return false;
            }

            int3 dimensions = new int3(gridDimensions.x, gridDimensions.y, gridDimensions.z);
            float3 origin = new float3(gridOrigin.x, gridOrigin.y, gridOrigin.z);
            float3 cellSize = new float3(voxelCellSize.x, voxelCellSize.y, voxelCellSize.z);
            float3 start = new float3(sourcePosition.x, sourcePosition.y, sourcePosition.z);
            float3 end = new float3(listenerPosition.x, listenerPosition.y, listenerPosition.z);
            if (!TryWorldToCaveVoxel(start, origin, cellSize, dimensions, out int3 startVoxel) ||
                !TryWorldToCaveVoxel(end, origin, cellSize, dimensions, out int3 endVoxel))
            {
                return false;
            }

            float3 delta = end - start;
            float distanceSq = math.lengthsq(delta);
            if (distanceSq <= CaveVoxelDdaEpsilon)
            {
                float density = ResolveCaveVoxelDensity01(SampleCaveVoxel(signedDistanceVoxels, startVoxel, dimensions));
                result = new AcousticVoxelOcclusionResult(
                    density,
                    math.exp(-density * VoxelDensityTransmissionScale),
                    ResolveVoxelDensityLowPassCutoff(density),
                    1);
                return true;
            }

            float3 rayDirection = delta * math.rsqrt(distanceSq);
            bool3 positiveMask = rayDirection >= 0f;
            bool3 activeAxisMask = math.abs(rayDirection) > CaveVoxelDdaEpsilon;
            int3 step = math.select(new int3(-1, -1, -1), new int3(1, 1, 1), positiveMask);
            float3 cellMin = origin + (new float3(startVoxel.x, startVoxel.y, startVoxel.z) * cellSize);
            float3 voxelBoundary = cellMin + math.select(float3.zero, cellSize, positiveMask);
            float3 safeAbsDirection = math.max(math.abs(rayDirection), new float3(CaveVoxelDdaEpsilon, CaveVoxelDdaEpsilon, CaveVoxelDdaEpsilon));
            float3 rayDirectionInverse = 1f / safeAbsDirection;
            float3 tMax = math.abs((voxelBoundary - start) * rayDirectionInverse);
            float3 tDelta = cellSize * rayDirectionInverse;
            float3 sentinel = new float3(1000000f, 1000000f, 1000000f);
            tMax = math.select(sentinel, tMax, activeAxisMask);
            tDelta = math.select(sentinel, tDelta, activeAxisMask);

            int3 currentVoxel = startVoxel;
            int maxSteps = math.min(
                VoxelDensityMaximumDdaSteps,
                math.max(1, dimensions.x + dimensions.y + dimensions.z));
            float accumulatedDensity = 0f;
            int sampledVoxelCount = 0;

            for (int i = 0; i < maxSteps; i++)
            {
                accumulatedDensity += ResolveCaveVoxelDensity01(SampleCaveVoxel(signedDistanceVoxels, currentVoxel, dimensions));
                sampledVoxelCount++;

                if (math.all(currentVoxel == endVoxel))
                    break;

                bool3 axisMask = (tMax <= tMax.yzx) & (tMax <= tMax.zxy);
                tMax += math.select(float3.zero, tDelta, axisMask);
                currentVoxel += math.select(int3.zero, step, axisMask);
                if (!IsCaveVoxelInside(currentVoxel, dimensions))
                    break;
            }

            float normalizedDensity = sampledVoxelCount > 0
                ? accumulatedDensity / sampledVoxelCount
                : 0f;
            float transmission01 = math.clamp(
                math.exp(-normalizedDensity * VoxelDensityTransmissionScale),
                0.02f,
                1f);
            result = new AcousticVoxelOcclusionResult(
                accumulatedDensity,
                transmission01,
                ResolveVoxelDensityLowPassCutoff(normalizedDensity),
                sampledVoxelCount);
            return sampledVoxelCount > 0;
        }

        public static AcousticOcclusionResult EvaluateOcclusionPath(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            int layerMask,
            RaycastHit[] hitBuffer,
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

        private static void EnsureRuntimeBuffers()
        {
            if (!_queryCommands.IsCreated)
            {
                _queryCommands = new NativeList<RaycastCommand>(MaxQueuedRequests, Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeList(
                    _queryCommands,
                    nameof(AcousticOcclusionUtility),
                    nameof(_queryCommands),
                    NativeAllocationLifetime.Session);
            }

            if (!_queryResults.IsCreated)
            {
                _queryResults = new NativeArray<RaycastHit>(MaxQueuedRequests * MaxOcclusionHits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _queryResults,
                    nameof(AcousticOcclusionUtility),
                    nameof(_queryResults),
                    NativeAllocationLifetime.Session);
            }

            if (!_forwardEchoCommands.IsCreated)
            {
                _forwardEchoCommands = new NativeArray<RaycastCommand>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _forwardEchoCommands,
                    nameof(AcousticOcclusionUtility),
                    nameof(_forwardEchoCommands),
                    NativeAllocationLifetime.Session);
            }

            if (!_forwardEchoResults.IsCreated)
            {
                _forwardEchoResults = new NativeArray<RaycastHit>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _forwardEchoResults,
                    nameof(AcousticOcclusionUtility),
                    nameof(_forwardEchoResults),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void AdvanceFrameFence()
        {
            int currentFrame = Time.frameCount;
            if (_queuedFrame != currentFrame)
                _queuedFrame = currentFrame;
        }

        private static void TryConsumeCompletedQuery()
        {
            if (!_queryBatchScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _pendingQueryHandle, forceComplete: false))
                return;

            for (int queryIndex = 0; queryIndex < _scheduledCount; queryIndex++)
            {
                QueryKey queryKey = _scheduledEntries[queryIndex].Key;
                AcousticOcclusionResult result = ResolveOcclusionResult(
                    queryIndex * MaxOcclusionHits,
                    queryKey.SourcePosition,
                    queryKey.ListenerPosition,
                    queryKey.IgnoreOriginRootEntityId,
                    queryKey.IgnoreTargetRootEntityId,
                    queryKey.IgnoreOriginBodyEntityId,
                    queryKey.IgnoreTargetBodyEntityId);
                StoreCachedResult(queryKey, result);
            }

            _queryBatchScheduled = false;
            _scheduledCount = 0;
        }

        private static void TryConsumeCompletedForwardEchoQuery()
        {
            if (!_forwardEchoQueryScheduled || !_scheduledForwardEchoValid)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _pendingForwardEchoHandle, forceComplete: false))
                return;

            RaycastHit hit = _forwardEchoResults[0];
            Collider collider = hit.collider;
            AcousticForwardEchoResult result;
            if (collider == null ||
                ShouldIgnoreCollider(
                    collider,
                    _scheduledForwardEchoKey.IgnoreRootEntityId,
                    0ul,
                    _scheduledForwardEchoKey.IgnoreBodyEntityId,
                    0ul))
            {
                result = new AcousticForwardEchoResult(
                    _scheduledForwardEchoKey.ProbeDistance,
                    1f,
                    OpenLowPassCutoffHertz,
                    false);
            }
            else
            {
                float absorption01 = ResolveAbsorption01(collider);
                float transmission01 = math.clamp(1f - absorption01, 0f, 1f);
                float lowPassCutoffHz = math.lerp(
                    MinimumLowPassCutoffHertz,
                    OpenLowPassCutoffHertz,
                    transmission01);
                result = new AcousticForwardEchoResult(
                    math.max(MinimumProbeDistanceMeters, hit.distance),
                    transmission01,
                    lowPassCutoffHz,
                    true);
            }

            _cachedForwardEchoEntry.Key = _scheduledForwardEchoKey;
            _cachedForwardEchoEntry.Result = result;
            _cachedForwardEchoEntry.Valid = true;
            _scheduledForwardEchoValid = false;
            _forwardEchoQueryScheduled = false;
        }

        private static JobHandle CancelPendingHandlesForTeardown()
        {
            JobHandle dependency = JobHandle.CombineDependencies(_pendingQueryHandle, _pendingForwardEchoHandle);

            _pendingQueryHandle = default;
            _pendingForwardEchoHandle = default;
            _queryBatchScheduled = false;
            _forwardEchoQueryScheduled = false;
            return dependency;
        }

        private static void DisposeRuntimeBuffers(JobHandle dependency)
        {
            JobHandle disposeHandle = dependency;

            if (_queryCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(AcousticOcclusionUtility), nameof(_queryCommands));
                disposeHandle = _queryCommands.Dispose(disposeHandle);
                _queryCommands = default;
            }

            if (_queryResults.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_queryResults);
                disposeHandle = _queryResults.Dispose(disposeHandle);
                _queryResults = default;
            }

            if (_forwardEchoCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_forwardEchoCommands);
                disposeHandle = _forwardEchoCommands.Dispose(disposeHandle);
                _forwardEchoCommands = default;
            }

            if (_forwardEchoResults.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_forwardEchoResults);
                disposeHandle = _forwardEchoResults.Dispose(disposeHandle);
                _forwardEchoResults = default;
            }

            JobHandle.ScheduleBatchedJobs();
        }

        private static void ScheduleQueuedBatch()
        {
            if (_queuedCount <= 0)
                return;

            _queryCommands.Clear();
            _scheduledCount = 0;
            QueryParameters parameters;
            for (int queryIndex = 0; queryIndex < _queuedCount; queryIndex++)
            {
                QueryKey queryKey = _queuedEntries[queryIndex].Key;
                Vector3 delta = queryKey.ListenerPosition - queryKey.SourcePosition;
                float distance = delta.magnitude;
                if (distance <= 0.001f)
                {
                    StoreCachedResult(queryKey, new AcousticOcclusionResult(1f, OpenLowPassCutoffHertz, 0));
                    continue;
                }

                parameters = new QueryParameters(queryKey.LayerMask, false, QueryTriggerInteraction.Ignore);
                _queryCommands.AddNoResize(new RaycastCommand(queryKey.SourcePosition, delta / distance, parameters, distance));
                _scheduledEntries[_scheduledCount].Key = queryKey;
                _scheduledCount++;
            }

            if (_scheduledCount <= 0)
                return;

            int resultLength = math.min(_scheduledCount * MaxOcclusionHits, _queryResults.Length);
            for (int i = 0; i < resultLength; i++)
                _queryResults[i] = default;

            _pendingQueryHandle = RaycastCommand.ScheduleBatch(
                _queryCommands.AsDeferredJobArray(),
                _queryResults,
                1,
                MaxOcclusionHits,
                default);
            _queryBatchScheduled = true;
        }

        private static void ScheduleQueuedForwardEchoBatch()
        {
            if (!_queuedForwardEchoValid || !_forwardEchoCommands.IsCreated || !_forwardEchoResults.IsCreated)
                return;

            QueryParameters parameters = new QueryParameters(_queuedForwardEchoKey.LayerMask, false, QueryTriggerInteraction.Ignore);
            _forwardEchoCommands[0] = new RaycastCommand(
                _queuedForwardEchoKey.OriginPosition,
                _queuedForwardEchoKey.ForwardDirection,
                parameters,
                _queuedForwardEchoKey.ProbeDistance);
            _forwardEchoResults[0] = default;
            _scheduledForwardEchoKey = _queuedForwardEchoKey;
            _scheduledForwardEchoValid = true;
            _queuedForwardEchoValid = false;
            _pendingForwardEchoHandle = RaycastCommand.ScheduleBatch(_forwardEchoCommands, _forwardEchoResults, 1, default);
            _forwardEchoQueryScheduled = true;
        }

        private static AcousticOcclusionResult ResolveOcclusionResult(
            int resultStartIndex,
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            ulong ignoreOriginRootEntityId,
            ulong ignoreTargetRootEntityId,
            ulong ignoreOriginBodyEntityId,
            ulong ignoreTargetBodyEntityId)
        {
            float transmission01 = 1f;
            int occludingHitCount = 0;
            int resultEndIndex = math.min(resultStartIndex + MaxOcclusionHits, _queryResults.Length);
            for (int i = resultStartIndex; i < resultEndIndex; i++)
            {
                Collider collider = _queryResults[i].collider;
                if (collider == null)
                    break;

                if (ShouldIgnoreCollider(
                        collider,
                        ignoreOriginRootEntityId,
                        ignoreTargetRootEntityId,
                        ignoreOriginBodyEntityId,
                        ignoreTargetBodyEntityId))
                    continue;

                float absorption01 = ResolveAbsorption01(collider);
                transmission01 *= 1f - math.clamp(absorption01, 0f, 1f);
                occludingHitCount++;
            }

            float lowPassCutoffHz = occludingHitCount > 0
                ? math.max(
                    MinimumLowPassCutoffHertz,
                    OpenLowPassCutoffHertz / math.pow(2f, occludingHitCount))
                : OpenLowPassCutoffHertz;

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

        private static void ApplyFloraScattering(
            Vector3 sourcePosition,
            Vector3 listenerPosition,
            ref float transmission01,
            ref float lowPassCutoffHz,
            ref int occludingHitCount)
        {
            Vector3 segment = listenerPosition - sourcePosition;
            float segmentLength = segment.magnitude;
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

        private static bool TryWorldToCaveVoxel(float3 worldPosition, float3 gridOrigin, float3 voxelCellSize, int3 dimensions, out int3 voxel)
        {
            float3 local = worldPosition - gridOrigin;
            if (local.x < 0f || local.y < 0f || local.z < 0f)
            {
                voxel = int3.zero;
                return false;
            }

            int3 candidate = new int3(
                (int)math.floor(local.x / math.max(voxelCellSize.x, CaveVoxelDdaEpsilon)),
                (int)math.floor(local.y / math.max(voxelCellSize.y, CaveVoxelDdaEpsilon)),
                (int)math.floor(local.z / math.max(voxelCellSize.z, CaveVoxelDdaEpsilon)));
            if (!IsCaveVoxelInside(candidate, dimensions))
            {
                voxel = int3.zero;
                return false;
            }

            voxel = candidate;
            return true;
        }

        private static bool IsCaveVoxelInside(int3 voxel, int3 dimensions)
        {
            return voxel.x >= 0 &&
                   voxel.y >= 0 &&
                   voxel.z >= 0 &&
                   voxel.x < dimensions.x &&
                   voxel.y < dimensions.y &&
                   voxel.z < dimensions.z;
        }

        private static byte SampleCaveVoxel(NativeArray<byte> signedDistanceVoxels, int3 voxel, int3 dimensions)
        {
            int flatIndex = voxel.x + (voxel.y * dimensions.x) + (voxel.z * dimensions.x * dimensions.y);
            if (flatIndex < 0 || flatIndex >= signedDistanceVoxels.Length)
                return 255;

            return signedDistanceVoxels[flatIndex];
        }

        private static float ResolveCaveVoxelDensity01(byte encodedSignedDistance)
        {
            if (encodedSignedDistance >= CaveSignedDistanceSolidThreshold)
                return 0f;

            return math.saturate((CaveSignedDistanceSolidThreshold - encodedSignedDistance) / (float)CaveSignedDistanceSolidThreshold);
        }

        private static float ResolveVoxelDensityLowPassCutoff(float density01)
        {
            float density = math.saturate(density01);
            if (density >= VoxelDensityHeavyOcclusionThreshold01)
            {
                float hardOcclusionT = math.saturate(
                    (density - VoxelDensityHeavyOcclusionThreshold01) /
                    math.max(1f - VoxelDensityHeavyOcclusionThreshold01, 0.0001f));
                return math.clamp(
                    math.lerp(VoxelDensityHeavyLowPassStartHertz, VoxelDensityHardLowPassCutoffHertz, hardOcclusionT),
                    MinimumLowPassCutoffHertz,
                    OpenLowPassCutoffHertz);
            }

            return math.clamp(
                OpenLowPassCutoffHertz / (1f + (density * VoxelDensityLowPassScale)),
                MinimumLowPassCutoffHertz,
                OpenLowPassCutoffHertz);
        }

        private static bool ShouldIgnoreCollider(
            Collider collider,
            ulong ignoreOriginRootEntityId,
            ulong ignoreTargetRootEntityId,
            ulong ignoreOriginBodyEntityId,
            ulong ignoreTargetBodyEntityId)
        {
            if (collider == null)
                return true;

            Rigidbody attachedBody = collider.attachedRigidbody;
            if (attachedBody != null)
            {
                ulong bodyEntityId = EntityId.ToULong(attachedBody.GetEntityId());
                if ((ignoreOriginBodyEntityId != 0ul && bodyEntityId == ignoreOriginBodyEntityId) ||
                    (ignoreTargetBodyEntityId != 0ul && bodyEntityId == ignoreTargetBodyEntityId))
                {
                    return true;
                }
            }

            ulong colliderEntityId = EntityId.ToULong(collider.GetEntityId());
            if ((ignoreOriginRootEntityId != 0ul && colliderEntityId == ignoreOriginRootEntityId) ||
                (ignoreTargetRootEntityId != 0ul && colliderEntityId == ignoreTargetRootEntityId))
            {
                return true;
            }

            Transform colliderTransform = collider.transform;
            if (colliderTransform == null)
                return false;

            ulong transformEntityId = EntityId.ToULong(colliderTransform.GetEntityId());
            return (ignoreOriginRootEntityId != 0ul && transformEntityId == ignoreOriginRootEntityId) ||
                   (ignoreTargetRootEntityId != 0ul && transformEntityId == ignoreTargetRootEntityId);
        }

        private static AcousticEnclosureResult BuildOpenWaterResult(float probeDistance)
        {
            _ = probeDistance;
            float span = OpenWaterPresetSpanMeters;
            float volume = math.max(0.01f, span * span * span);
            float faceArea = span * span;
            float totalArea = faceArea * EnclosureProbeCount;
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
            float totalArea = faceArea * EnclosureProbeCount;
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
                EnclosureProbeCount);
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
