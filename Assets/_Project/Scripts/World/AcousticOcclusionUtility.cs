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
        private const int MaxQueuedEnclosureRequests = 8;
        private const int EnclosureProbeCount = 6;
        private const float OcclusionReuseDistanceMeters = 0.5f;
        private const float OcclusionReuseDistanceSqr = OcclusionReuseDistanceMeters * OcclusionReuseDistanceMeters;
        private const float EnclosureReuseDistanceMeters = 2f;
        private const float EnclosureReuseDistanceSqr = EnclosureReuseDistanceMeters * EnclosureReuseDistanceMeters;
        private const float MinimumProbeDistanceMeters = 0.001f;
        private const float MinimumEquivalentAbsorptionArea = 0.5f;
        private const float SabineConstant = 0.161f;
        private const float MinimumRt60Seconds = 0.12f;
        private const float MaximumRt60Seconds = 10f;

        private static readonly int PlayerLayer = LayerMask.NameToLayer("Player");
        private static readonly int TriggerZoneLayer = LayerMask.NameToLayer("TriggerZone");
        private static readonly int TransparentFxLayer = LayerMask.NameToLayer("TransparentFX");
        private static readonly int FirstPersonToolsLayer = LayerMask.NameToLayer("FirstPersonTools");
        private static readonly int VoxelCaveLayer = LayerMask.NameToLayer("VoxelCave");
        private static readonly int BaseModuleLayer = LayerMask.NameToLayer("BaseModule");
        private static readonly int VehicleLayer = LayerMask.NameToLayer("Vehicle");
        private static readonly int WaterLayer = LayerMask.NameToLayer("Water");

        private struct QueryKey
        {
            public Vector3 SourcePosition;
            public Vector3 ListenerPosition;
            public int LayerMask;
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

        private struct EnclosureKey
        {
            public Vector3 OriginPosition;
            public float ProbeDistance;
            public int LayerMask;
            public ulong IgnoreRootEntityId;
            public ulong IgnoreBodyEntityId;
        }

        private struct EnclosureFrameEntry
        {
            public EnclosureKey Key;
        }

        private struct CachedEnclosureEntry
        {
            public EnclosureKey Key;
            public AcousticEnclosureResult Result;
            public bool Valid;
        }

        private static int _runtimeOwnerCount;
        private static bool _queryBatchScheduled;
        private static bool _enclosureBatchScheduled;
        private static int _queuedFrame = -1;
        private static int _queuedCount;
        private static int _scheduledCount;
        private static int _nextCacheWriteIndex;
        private static int _queuedEnclosureCount;
        private static int _scheduledEnclosureCount;
        private static int _nextEnclosureCacheWriteIndex;
        private static JobHandle _pendingQueryHandle;
        private static JobHandle _pendingEnclosureHandle;
        // COLD ALLOC: QueryFrameEntry[48] - queued cross-frame acoustic occlusion requests - owner: AcousticOcclusionUtility
        private static readonly QueryFrameEntry[] _queuedEntries = new QueryFrameEntry[MaxQueuedRequests];
        // COLD ALLOC: QueryFrameEntry[48] - scheduled acoustic occlusion request snapshot - owner: AcousticOcclusionUtility
        private static readonly QueryFrameEntry[] _scheduledEntries = new QueryFrameEntry[MaxQueuedRequests];
        // COLD ALLOC: CachedQueryEntry[48] - last resolved acoustic occlusion cache - owner: AcousticOcclusionUtility
        private static readonly CachedQueryEntry[] _cachedEntries = new CachedQueryEntry[MaxQueuedRequests];
        // COLD ALLOC: EnclosureFrameEntry[8] - queued cross-frame enclosure probes - owner: AcousticOcclusionUtility
        private static readonly EnclosureFrameEntry[] _queuedEnclosureEntries = new EnclosureFrameEntry[MaxQueuedEnclosureRequests];
        // COLD ALLOC: EnclosureFrameEntry[8] - scheduled enclosure probe snapshot - owner: AcousticOcclusionUtility
        private static readonly EnclosureFrameEntry[] _scheduledEnclosureEntries = new EnclosureFrameEntry[MaxQueuedEnclosureRequests];
        // COLD ALLOC: CachedEnclosureEntry[8] - last resolved enclosure cache - owner: AcousticOcclusionUtility
        private static readonly CachedEnclosureEntry[] _cachedEnclosureEntries = new CachedEnclosureEntry[MaxQueuedEnclosureRequests];
        // COLD ALLOC: NativeList<RaycastCommand>[48] - deferred acoustic occlusion batch command buffer - owner: AcousticOcclusionUtility
        private static NativeList<RaycastCommand> _queryCommands;
        // COLD ALLOC: NativeArray<RaycastHit>[384] - deferred acoustic occlusion batch result buffer - owner: AcousticOcclusionUtility
        private static NativeArray<RaycastHit> _queryResults;
        // COLD ALLOC: NativeList<RaycastCommand>[48] - deferred enclosure probe command buffer - owner: AcousticOcclusionUtility
        private static NativeList<RaycastCommand> _enclosureCommands;
        // COLD ALLOC: NativeArray<RaycastHit>[48] - deferred enclosure probe result buffer - owner: AcousticOcclusionUtility
        private static NativeArray<RaycastHit> _enclosureResults;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            if (_queryBatchScheduled && _pendingQueryHandle.IsCompleted)
                _pendingQueryHandle.Complete();

            if (_enclosureBatchScheduled && _pendingEnclosureHandle.IsCompleted)
                _pendingEnclosureHandle.Complete();

            if (_queryCommands.IsCreated)
                _queryCommands.Dispose();

            if (_queryResults.IsCreated)
                _queryResults.Dispose();

            if (_enclosureCommands.IsCreated)
                _enclosureCommands.Dispose();

            if (_enclosureResults.IsCreated)
                _enclosureResults.Dispose();

            _runtimeOwnerCount = 0;
            _queryBatchScheduled = false;
            _enclosureBatchScheduled = false;
            _queuedFrame = -1;
            _queuedCount = 0;
            _scheduledCount = 0;
            _nextCacheWriteIndex = 0;
            _queuedEnclosureCount = 0;
            _scheduledEnclosureCount = 0;
            _nextEnclosureCacheWriteIndex = 0;
            _pendingQueryHandle = default;
            _pendingEnclosureHandle = default;
            _queryCommands = default;
            _queryResults = default;
            _enclosureCommands = default;
            _enclosureResults = default;

            for (int i = 0; i < MaxQueuedRequests; i++)
                _cachedEntries[i].Valid = false;

            for (int i = 0; i < MaxQueuedEnclosureRequests; i++)
                _cachedEnclosureEntries[i].Valid = false;
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
            _enclosureBatchScheduled = false;
            _queuedFrame = -1;
            _queuedCount = 0;
            _scheduledCount = 0;
            _nextCacheWriteIndex = 0;
            _queuedEnclosureCount = 0;
            _scheduledEnclosureCount = 0;
            _nextEnclosureCacheWriteIndex = 0;

            if (_queryCommands.IsCreated)
            {
                if (_pendingQueryHandle.Equals(default(JobHandle)))
                    _queryCommands.Dispose();
                else
                    _queryCommands.Dispose(_pendingQueryHandle);
            }

            if (_queryResults.IsCreated)
            {
                if (_pendingQueryHandle.Equals(default(JobHandle)))
                    _queryResults.Dispose();
                else
                    _queryResults.Dispose(_pendingQueryHandle);
            }

            if (_enclosureCommands.IsCreated)
            {
                if (_pendingEnclosureHandle.Equals(default(JobHandle)))
                    _enclosureCommands.Dispose();
                else
                    _enclosureCommands.Dispose(_pendingEnclosureHandle);
            }

            if (_enclosureResults.IsCreated)
            {
                if (_pendingEnclosureHandle.Equals(default(JobHandle)))
                    _enclosureResults.Dispose();
                else
                    _enclosureResults.Dispose(_pendingEnclosureHandle);
            }

            _pendingQueryHandle = default;
            _pendingEnclosureHandle = default;
            _queryCommands = default;
            _queryResults = default;
            _enclosureCommands = default;
            _enclosureResults = default;

            for (int i = 0; i < MaxQueuedRequests; i++)
                _cachedEntries[i].Valid = false;

            for (int i = 0; i < MaxQueuedEnclosureRequests; i++)
                _cachedEnclosureEntries[i].Valid = false;
        }

        public static int BuildSensoryMask()
        {
            int mask = UnityEngine.Physics.DefaultRaycastLayers;
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
            EnsureRuntimeBuffers();
            AdvanceFrameFence();
            if (!_queryCommands.IsCreated || !_queryResults.IsCreated)
                return;

            QueryKey queryKey = new QueryKey
            {
                SourcePosition = sourcePosition,
                ListenerPosition = listenerPosition,
                LayerMask = layerMask,
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

        public static void PrimeEnclosureSample(
            Vector3 originPosition,
            float probeDistance,
            int layerMask,
            Transform ignoreRoot)
        {
            EnsureRuntimeBuffers();
            AdvanceFrameFence();
            if (!_enclosureCommands.IsCreated || !_enclosureResults.IsCreated)
                return;

            float clampedProbeDistance = math.max(1f, probeDistance);
            EnclosureKey queryKey = new EnclosureKey
            {
                OriginPosition = originPosition,
                ProbeDistance = clampedProbeDistance,
                LayerMask = layerMask,
                IgnoreRootEntityId = ResolveEntityId(ignoreRoot),
                IgnoreBodyEntityId = ResolveAttachedBodyEntityId(ignoreRoot)
            };

            if (TryFindCachedEnclosureResult(queryKey, out _))
                return;

            for (int i = 0; i < _queuedEnclosureCount; i++)
            {
                if (EnclosureKeysMatch(_queuedEnclosureEntries[i].Key, queryKey))
                    return;
            }

            if (_queuedEnclosureCount >= MaxQueuedEnclosureRequests)
                return;

            _queuedEnclosureEntries[_queuedEnclosureCount].Key = queryKey;
            _queuedEnclosureCount++;
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

            QueryKey queryKey = new QueryKey
            {
                SourcePosition = sourcePosition,
                ListenerPosition = listenerPosition,
                LayerMask = layerMask,
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
            AdvanceFrameFence();
            EnclosureKey queryKey = new EnclosureKey
            {
                OriginPosition = originPosition,
                ProbeDistance = math.max(1f, probeDistance),
                LayerMask = layerMask,
                IgnoreRootEntityId = ResolveEntityId(ignoreRoot),
                IgnoreBodyEntityId = ResolveAttachedBodyEntityId(ignoreRoot)
            };

            if (TryFindCachedEnclosureResult(queryKey, out result))
                return true;

            result = BuildOpenWaterResult(queryKey.ProbeDistance);
            return false;
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
                _queryCommands = new NativeList<RaycastCommand>(MaxQueuedRequests, Allocator.Persistent);

            if (!_queryResults.IsCreated)
                _queryResults = new NativeArray<RaycastHit>(MaxQueuedRequests * MaxOcclusionHits, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            if (!_enclosureCommands.IsCreated)
                _enclosureCommands = new NativeList<RaycastCommand>(MaxQueuedEnclosureRequests * EnclosureProbeCount, Allocator.Persistent);

            if (!_enclosureResults.IsCreated)
            {
                _enclosureResults = new NativeArray<RaycastHit>(
                    MaxQueuedEnclosureRequests * EnclosureProbeCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
            }
        }

        private static void AdvanceFrameFence()
        {
            int currentFrame = Time.frameCount;
            if (_queuedFrame == currentFrame)
            {
                TryConsumeCompletedQuery();
                TryConsumeCompletedEnclosureQuery();
                return;
            }

            TryConsumeCompletedQuery();
            TryConsumeCompletedEnclosureQuery();
            if (!_queryBatchScheduled && _queuedCount > 0)
                ScheduleQueuedBatch();

            if (!_enclosureBatchScheduled && _queuedEnclosureCount > 0)
                ScheduleQueuedEnclosureBatch();

            _queuedFrame = currentFrame;
            _queuedCount = 0;
            _queuedEnclosureCount = 0;
        }

        private static void TryConsumeCompletedQuery()
        {
            if (!_queryBatchScheduled || !_pendingQueryHandle.IsCompleted)
                return;

            _pendingQueryHandle.Complete();
            for (int queryIndex = 0; queryIndex < _scheduledCount; queryIndex++)
            {
                QueryKey queryKey = _scheduledEntries[queryIndex].Key;
                AcousticOcclusionResult result = ResolveOcclusionResult(
                    queryIndex * MaxOcclusionHits,
                    queryKey.IgnoreOriginRootEntityId,
                    queryKey.IgnoreTargetRootEntityId,
                    queryKey.IgnoreOriginBodyEntityId,
                    queryKey.IgnoreTargetBodyEntityId);
                StoreCachedResult(queryKey, result);
            }

            _queryBatchScheduled = false;
            _scheduledCount = 0;
            _pendingQueryHandle = default;
        }

        private static void TryConsumeCompletedEnclosureQuery()
        {
            if (!_enclosureBatchScheduled || !_pendingEnclosureHandle.IsCompleted)
                return;

            _pendingEnclosureHandle.Complete();
            for (int queryIndex = 0; queryIndex < _scheduledEnclosureCount; queryIndex++)
            {
                EnclosureKey queryKey = _scheduledEnclosureEntries[queryIndex].Key;
                AcousticEnclosureResult result = ResolveEnclosureResult(
                    queryIndex * EnclosureProbeCount,
                    queryKey.ProbeDistance,
                    queryKey.IgnoreRootEntityId,
                    queryKey.IgnoreBodyEntityId);
                StoreCachedEnclosureResult(queryKey, result);
            }

            _enclosureBatchScheduled = false;
            _scheduledEnclosureCount = 0;
            _pendingEnclosureHandle = default;
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
                _queryCommands.Add(new RaycastCommand(queryKey.SourcePosition, delta / distance, parameters, distance));
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

        private static void ScheduleQueuedEnclosureBatch()
        {
            if (_queuedEnclosureCount <= 0)
                return;

            _enclosureCommands.Clear();
            _scheduledEnclosureCount = 0;
            for (int queryIndex = 0; queryIndex < _queuedEnclosureCount; queryIndex++)
            {
                EnclosureKey queryKey = _queuedEnclosureEntries[queryIndex].Key;
                QueryParameters parameters = new QueryParameters(queryKey.LayerMask, false, QueryTriggerInteraction.Ignore);
                for (int axisIndex = 0; axisIndex < EnclosureProbeCount; axisIndex++)
                {
                    _enclosureCommands.Add(new RaycastCommand(
                        queryKey.OriginPosition,
                        ResolveEnclosureProbeDirection(axisIndex),
                        parameters,
                        queryKey.ProbeDistance));
                }

                _scheduledEnclosureEntries[_scheduledEnclosureCount].Key = queryKey;
                _scheduledEnclosureCount++;
            }

            if (_scheduledEnclosureCount <= 0)
                return;

            int resultLength = math.min(_scheduledEnclosureCount * EnclosureProbeCount, _enclosureResults.Length);
            for (int i = 0; i < resultLength; i++)
                _enclosureResults[i] = default;

            _pendingEnclosureHandle = RaycastCommand.ScheduleBatch(
                _enclosureCommands.AsDeferredJobArray(),
                _enclosureResults,
                1,
                default);
            _enclosureBatchScheduled = true;
        }

        private static AcousticOcclusionResult ResolveOcclusionResult(
            int resultStartIndex,
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

            if (occludingHitCount <= 0)
                return new AcousticOcclusionResult(1f, OpenLowPassCutoffHertz, 0);

            float lowPassCutoffHz = math.max(
                MinimumLowPassCutoffHertz,
                OpenLowPassCutoffHertz / math.pow(2f, occludingHitCount));

            return new AcousticOcclusionResult(
                math.clamp(transmission01, 0f, 1f),
                math.clamp(lowPassCutoffHz, MinimumLowPassCutoffHertz, OpenLowPassCutoffHertz),
                occludingHitCount);
        }

        private static AcousticEnclosureResult ResolveEnclosureResult(
            int resultStartIndex,
            float probeDistance,
            ulong ignoreRootEntityId,
            ulong ignoreBodyEntityId)
        {
            float clampedProbeDistance = math.max(1f, probeDistance);
            float distanceUp = ResolveEnclosureDistance(resultStartIndex, 0, clampedProbeDistance, ignoreRootEntityId, ignoreBodyEntityId, out float absorptionUp, out bool hitUp);
            float distanceDown = ResolveEnclosureDistance(resultStartIndex, 1, clampedProbeDistance, ignoreRootEntityId, ignoreBodyEntityId, out float absorptionDown, out bool hitDown);
            float distanceLeft = ResolveEnclosureDistance(resultStartIndex, 2, clampedProbeDistance, ignoreRootEntityId, ignoreBodyEntityId, out float absorptionLeft, out bool hitLeft);
            float distanceRight = ResolveEnclosureDistance(resultStartIndex, 3, clampedProbeDistance, ignoreRootEntityId, ignoreBodyEntityId, out float absorptionRight, out bool hitRight);
            float distanceForward = ResolveEnclosureDistance(resultStartIndex, 4, clampedProbeDistance, ignoreRootEntityId, ignoreBodyEntityId, out float absorptionForward, out bool hitForward);
            float distanceBack = ResolveEnclosureDistance(resultStartIndex, 5, clampedProbeDistance, ignoreRootEntityId, ignoreBodyEntityId, out float absorptionBack, out bool hitBack);

            float spanVertical = math.max(MinimumProbeDistanceMeters, distanceUp + distanceDown);
            float spanHorizontal = math.max(MinimumProbeDistanceMeters, distanceLeft + distanceRight);
            float spanDepth = math.max(MinimumProbeDistanceMeters, distanceForward + distanceBack);
            float volumeCubicMeters = math.max(0.01f, spanVertical * spanHorizontal * spanDepth);

            float areaTop = spanHorizontal * spanDepth;
            float areaBottom = areaTop;
            float areaLeft = spanVertical * spanDepth;
            float areaRight = areaLeft;
            float areaForward = spanVertical * spanHorizontal;
            float areaBack = areaForward;
            float totalArea =
                areaTop + areaBottom +
                areaLeft + areaRight +
                areaForward + areaBack;

            float equivalentAbsorptionArea =
                (areaTop * absorptionUp) +
                (areaBottom * absorptionDown) +
                (areaLeft * absorptionLeft) +
                (areaRight * absorptionRight) +
                (areaForward * absorptionForward) +
                (areaBack * absorptionBack);

            equivalentAbsorptionArea = math.max(MinimumEquivalentAbsorptionArea, equivalentAbsorptionArea);
            float meanAbsorption01 = totalArea > 0.001f
                ? math.clamp(equivalentAbsorptionArea / totalArea, 0f, 1f)
                : WaterAbsorption01;

            float rt60Seconds = math.clamp(
                SabineConstant * volumeCubicMeters / equivalentAbsorptionArea,
                MinimumRt60Seconds,
                MaximumRt60Seconds);

            float normalizedRt60 = math.saturate((rt60Seconds - MinimumRt60Seconds) / (MaximumRt60Seconds - MinimumRt60Seconds));
            float openness01 = math.saturate(
                ((spanVertical + spanHorizontal + spanDepth) / 3f) /
                math.max(clampedProbeDistance * 2f, 0.001f));
            float wetMix01 = math.saturate(normalizedRt60 * (0.65f + 0.35f * openness01));

            int hitCount =
                (hitUp ? 1 : 0) +
                (hitDown ? 1 : 0) +
                (hitLeft ? 1 : 0) +
                (hitRight ? 1 : 0) +
                (hitForward ? 1 : 0) +
                (hitBack ? 1 : 0);

            return new AcousticEnclosureResult(
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

        private static bool TryFindCachedEnclosureResult(EnclosureKey queryKey, out AcousticEnclosureResult result)
        {
            for (int i = 0; i < MaxQueuedEnclosureRequests; i++)
            {
                if (!_cachedEnclosureEntries[i].Valid)
                    continue;

                if (!EnclosureKeysMatch(_cachedEnclosureEntries[i].Key, queryKey))
                    continue;

                result = _cachedEnclosureEntries[i].Result;
                return true;
            }

            result = BuildOpenWaterResult(queryKey.ProbeDistance);
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

        private static void StoreCachedEnclosureResult(EnclosureKey queryKey, AcousticEnclosureResult result)
        {
            for (int i = 0; i < MaxQueuedEnclosureRequests; i++)
            {
                if (_cachedEnclosureEntries[i].Valid && EnclosureKeysMatch(_cachedEnclosureEntries[i].Key, queryKey))
                {
                    _cachedEnclosureEntries[i].Result = result;
                    return;
                }
            }

            int writeIndex = _nextEnclosureCacheWriteIndex;
            _cachedEnclosureEntries[writeIndex].Key = queryKey;
            _cachedEnclosureEntries[writeIndex].Result = result;
            _cachedEnclosureEntries[writeIndex].Valid = true;
            _nextEnclosureCacheWriteIndex = (writeIndex + 1) % MaxQueuedEnclosureRequests;
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

        private static bool EnclosureKeysMatch(EnclosureKey cached, EnclosureKey current)
        {
            return cached.LayerMask == current.LayerMask &&
                   cached.IgnoreRootEntityId == current.IgnoreRootEntityId &&
                   cached.IgnoreBodyEntityId == current.IgnoreBodyEntityId &&
                   math.abs(cached.ProbeDistance - current.ProbeDistance) <= 0.01f &&
                   math.lengthsq((float3)(cached.OriginPosition - current.OriginPosition)) <= EnclosureReuseDistanceSqr;
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

        private static float ResolveEnclosureDistance(
            int resultStartIndex,
            int axisIndex,
            float fallbackDistance,
            ulong ignoreRootEntityId,
            ulong ignoreBodyEntityId,
            out float absorption01,
            out bool hitSurface)
        {
            int resultIndex = resultStartIndex + axisIndex;
            if ((uint)resultIndex >= (uint)_enclosureResults.Length)
            {
                absorption01 = WaterAbsorption01;
                hitSurface = false;
                return fallbackDistance;
            }

            RaycastHit hit = _enclosureResults[resultIndex];
            Collider collider = hit.collider;
            if (collider == null || ShouldIgnoreCollider(collider, ignoreRootEntityId, 0ul, ignoreBodyEntityId, 0ul))
            {
                absorption01 = WaterAbsorption01;
                hitSurface = false;
                return fallbackDistance;
            }

            absorption01 = ResolveAbsorption01(collider);
            hitSurface = true;
            return math.clamp(hit.distance, MinimumProbeDistanceMeters, fallbackDistance);
        }

        private static AcousticEnclosureResult BuildOpenWaterResult(float probeDistance)
        {
            float clampedProbeDistance = math.max(1f, probeDistance);
            float span = clampedProbeDistance * 2f;
            float volume = math.max(0.01f, span * span * span);
            float faceArea = span * span;
            float totalArea = faceArea * EnclosureProbeCount;
            float equivalentAbsorptionArea = math.max(MinimumEquivalentAbsorptionArea, totalArea * WaterAbsorption01);
            float rt60Seconds = math.clamp(
                SabineConstant * volume / equivalentAbsorptionArea,
                MinimumRt60Seconds,
                MaximumRt60Seconds);
            float normalizedRt60 = math.saturate((rt60Seconds - MinimumRt60Seconds) / (MaximumRt60Seconds - MinimumRt60Seconds));
            float wetMix01 = math.saturate(normalizedRt60);
            return new AcousticEnclosureResult(
                span,
                span,
                span,
                volume,
                WaterAbsorption01,
                equivalentAbsorptionArea,
                rt60Seconds,
                wetMix01,
                1f,
                0);
        }

        private static Vector3 ResolveEnclosureProbeDirection(int axisIndex)
        {
            switch (axisIndex)
            {
                case 0:
                    return Vector3.up;
                case 1:
                    return Vector3.down;
                case 2:
                    return Vector3.left;
                case 3:
                    return Vector3.right;
                case 4:
                    return Vector3.forward;
                default:
                    return Vector3.back;
            }
        }

        private static float ResolveAbsorption01(Collider collider)
        {
            if (collider == null)
                return DefaultAbsorption01;

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

        private static ulong ResolveEntityId(Transform target)
        {
            return target != null ? EntityId.ToULong(target.GetEntityId()) : 0ul;
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
