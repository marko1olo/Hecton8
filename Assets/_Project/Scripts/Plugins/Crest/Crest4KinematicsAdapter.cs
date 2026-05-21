using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Physics
{
    /// <summary>
    /// Crest 4-backed implementation of <see cref="IHectonOceanKinematics"/>.
    /// Keeps all Crest runtime calls and query-owner bookkeeping outside gameplay controllers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Crest 4 Kinematics Adapter")]
    public sealed class Crest4KinematicsAdapter : CrestBridge
    {
        private const int MaxBatchSampleCount = 5;
        private const int ProviderPriority = 400;
        private static readonly int _waveFoamStrengthId = Shader.PropertyToID("_WaveFoamStrength");
        private static readonly int _waveFoamCoverageId = Shader.PropertyToID("_WaveFoamCoverage");
        private static readonly int _foamScaleId = Shader.PropertyToID("_FoamScale");

        [Header("References")]
        [Tooltip("Explicit Crest ocean owner. Assign this directly or colocate the OceanRenderer on the same GameObject.")]
        [SerializeField] private Crest.OceanRenderer crestOceanRenderer;

        [Header("Burst Sampling")]
        [Tooltip("Depth below the resolved surface where Burst ocean kinematics returns a still-water result before trigonometry.")]
        [SerializeField, Range(0f, 200f)] private float burstDepthCullingThresholdMeters = OceanKinematicsConstants.DefaultDepthCullMeters;
        [Tooltip("Maximum Gerstner octaves available to the Burst analytical sampler. GlobalQualityWeight continuously resolves the active count.")]
        [SerializeField, Range(1, OceanKinematicsConstants.WaveCapacity)] private int burstMaxOctaveLimit = 6;
        [Tooltip("Continuous amplitude multiplier for the Burst analytical and emergency mock samplers.")]
        [SerializeField, Range(0f, 4f)] private float burstWaveAmplitudeMultiplier = OceanKinematicsConstants.DefaultAmplitudeMultiplier;

        private int _heightQueryOwnerHash;
        private int _waveQueryOwnerHash;
        private int _displacementQueryOwnerHash;
        private int _flowQueryOwnerHash;
        // COLD ALLOC: Vector3[5] - native-to-managed Crest position bridge scratch for Crest 4 runtime fallback - owner: Crest4KinematicsAdapter
        private readonly Vector3[] _samplePositionScratch = new Vector3[MaxBatchSampleCount];
        // COLD ALLOC: Vector3[5] - native-to-managed Crest flow bridge scratch for Crest 4 runtime fallback - owner: Crest4KinematicsAdapter
        private readonly Vector3[] _flowScratch = new Vector3[MaxBatchSampleCount];
        // COLD ALLOC: Vector3[5] - native-to-managed Crest wave-normal bridge scratch for Crest 4 runtime fallback - owner: Crest4KinematicsAdapter
        private readonly Vector3[] _waveNormalScratch = new Vector3[MaxBatchSampleCount];
        // COLD ALLOC: Vector3[5] - native-to-managed Crest velocity bridge scratch for Crest 4 runtime fallback - owner: Crest4KinematicsAdapter
        private readonly Vector3[] _surfaceVelocityScratch = new Vector3[MaxBatchSampleCount];
        // COLD ALLOC: Vector3[5] - native-to-managed Crest displacement bridge scratch for Crest 4 runtime fallback - owner: Crest4KinematicsAdapter
        private readonly Vector3[] _displacementScratch = new Vector3[MaxBatchSampleCount];
        private readonly float[] _heightScratch =
            new float[MaxBatchSampleCount]; // COLD ALLOC: float[5] - temporary Crest height scratch buffer for wave-only queries - owner: Crest4KinematicsAdapter

        /// <inheritdoc />
        public override int Priority => ProviderPriority;

        /// <inheritdoc />
        public override bool IsAvailable
        {
            get
            {
                Crest.OceanRenderer oceanRenderer = TryReadBoundOceanRenderer();
                return oceanRenderer != null && oceanRenderer.CollisionProvider != null;
            }
        }

        /// <inheritdoc />
        public override float SeaLevel => ResolveSeaLevel(TryReadBoundOceanRenderer());

        /// <inheritdoc />
        public override bool TryGetSurfaceWeatherState(out HectonOceanSurfaceWeatherState state)
        {
            state = default;
            Crest.OceanRenderer oceanRenderer = TryReadBoundOceanRenderer();
            if (oceanRenderer == null)
                return false;

            uint flags = (uint)HectonOceanSurfaceWeatherStateFlags.SupportsWindSpeed;
            state.WindSpeed = Mathf.Max(0f, oceanRenderer._globalWindSpeed);

            Material oceanMaterial = oceanRenderer.OceanMaterial;
            if (oceanMaterial != null)
            {
                if (oceanMaterial.HasProperty(_waveFoamStrengthId))
                {
                    flags |= (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamStrength;
                    state.FoamStrength = oceanMaterial.GetFloat(_waveFoamStrengthId);
                }

                if (oceanMaterial.HasProperty(_waveFoamCoverageId))
                {
                    flags |= (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamCoverage;
                    state.FoamCoverage = oceanMaterial.GetFloat(_waveFoamCoverageId);
                }

                if (oceanMaterial.HasProperty(_foamScaleId))
                {
                    flags |= (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamScale;
                    state.FoamScale = oceanMaterial.GetFloat(_foamScaleId);
                }
            }

            state.Flags = flags;
            return true;
        }

        /// <inheritdoc />
        public override bool ApplySurfaceWeatherState(in HectonOceanSurfaceWeatherState state)
        {
            Crest.OceanRenderer oceanRenderer = ResolveOceanRenderer();
            if (oceanRenderer == null)
                return false;

            uint flags = state.Flags;
            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsWindSpeed) != 0u)
                oceanRenderer._globalWindSpeed = Mathf.Max(0f, state.WindSpeed);

            Material oceanMaterial = oceanRenderer.OceanMaterial;
            if (oceanMaterial == null)
                return true;

            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamStrength) != 0u &&
                oceanMaterial.HasProperty(_waveFoamStrengthId))
            {
                oceanMaterial.SetFloat(_waveFoamStrengthId, state.FoamStrength);
            }

            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamCoverage) != 0u &&
                oceanMaterial.HasProperty(_waveFoamCoverageId))
            {
                oceanMaterial.SetFloat(_waveFoamCoverageId, state.FoamCoverage);
            }

            if ((flags & (uint)HectonOceanSurfaceWeatherStateFlags.SupportsFoamScale) != 0u &&
                oceanMaterial.HasProperty(_foamScaleId))
            {
                oceanMaterial.SetFloat(_foamScaleId, state.FoamScale);
            }

            return true;
        }

        /// <inheritdoc />
        public override bool TryAssignPrimaryLight(Light primaryLight)
        {
            Crest.OceanRenderer oceanRenderer = ResolveOceanRenderer();
            if (oceanRenderer == null || primaryLight == null)
                return false;

            if (!ReferenceEquals(oceanRenderer._primaryLight, primaryLight))
                oceanRenderer._primaryLight = primaryLight;

            return true;
        }

        /// <summary>
        /// Builds the per-frame unmanaged tuning row from a deterministic simulation clock.
        /// </summary>
        /// <param name="simulationTimeSeconds">Dispatcher-owned simulation time, not variable frame delta.</param>
        /// <param name="frameIndex">Dispatcher-owned rollback frame index.</param>
        /// <param name="tuning">Resolved tuning row.</param>
        /// <returns>True when a Crest ocean owner is bound and the row is finite-safe.</returns>
        public bool TryBuildBurstTuning(float simulationTimeSeconds, uint frameIndex, out OceanKinematicsTuningDTO tuning)
        {
            tuning = default;
            Crest.OceanRenderer oceanRenderer = TryReadBoundOceanRenderer();
            if (oceanRenderer == null)
                return false;

            tuning.OceanRootAUP = ResolveOceanRootAUP(oceanRenderer);
            tuning.OceanSurfaceY = ResolveSeaLevel(oceanRenderer);
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning.TimeSeconds = math.max(0f, math.select(0f, simulationTimeSeconds, math.isfinite(simulationTimeSeconds)));
            tuning.DepthCullingThresholdMeters = Mathf.Max(0f, burstDepthCullingThresholdMeters);
            tuning.MaxOctaveLimit = Mathf.Clamp(burstMaxOctaveLimit, 1, OceanKinematicsConstants.WaveCapacity);
            tuning.WaveAmplitudeMultiplier = Mathf.Max(0f, burstWaveAmplitudeMultiplier);
            tuning.FrameIndex = frameIndex;
            tuning.Flags = OceanKinematicsConstants.FlagActive | OceanKinematicsConstants.FlagAnalyticalWave;
            return true;
        }

        /// <summary>
        /// Publishes the O(1) macro ocean state using dispatcher-owned deterministic tuning.
        /// </summary>
        public bool TryPublishVaultMacroState(
            IDataVault vault,
            in OceanKinematicsTuningDTO tuning,
            NativeArray<GerstnerWaveDTO> waves,
            int waveCount,
            out OceanMacroStateDTO macroState)
        {
            OceanKinematicsTuningDTO sanitized = PrepareJobTuning(
                in tuning,
                requestCount: 0,
                flags: tuning.Flags | OceanKinematicsConstants.FlagActive | OceanKinematicsConstants.FlagAnalyticalWave);
            return OceanKinematicsVaultRuntime.TryPublishMacroState(vault, in sanitized, waves, waveCount, out macroState);
        }

        /// <summary>
        /// Records post-simulation ocean kinematics telemetry using dispatcher-owned deterministic tuning.
        /// </summary>
        public bool TryRecordVaultTelemetry(
            IDataVault vault,
            in OceanKinematicsTuningDTO tuning,
            NativeArray<int> queueCounters,
            NativeArray<FluidSampleResultDTO> results,
            int resultCount,
            float burstExecutionMicros,
            uint lastRequestHash)
        {
            OceanKinematicsTuningDTO sanitized = PrepareJobTuning(
                in tuning,
                math.max(0, resultCount),
                tuning.Flags | OceanKinematicsConstants.FlagActive);
            return OceanKinematicsVaultRuntime.TryRecordTelemetry(
                vault,
                in sanitized,
                queueCounters,
                results,
                resultCount,
                burstExecutionMicros,
                lastRequestHash);
        }

        /// <summary>
        /// Schedules Burst analytical wave sampling using dispatcher-owned deterministic tuning.
        /// </summary>
        public JobHandle ScheduleAnalyticalFluidSamples(
            NativeArray<OceanKinematicsSampleRequestDTO> requests,
            NativeArray<FluidSampleResultDTO> results,
            NativeArray<GerstnerWaveDTO> waves,
            int sampleCount,
            in OceanKinematicsTuningDTO tuning,
            JobHandle inputDeps)
        {
            if (!requests.IsCreated || !results.IsCreated || sampleCount <= 0)
                return inputDeps;

            int count = math.min(sampleCount, math.min(requests.Length, results.Length));
            if (count <= 0)
                return inputDeps;

            int waveCount = waves.IsCreated ? math.min(waves.Length, OceanKinematicsConstants.WaveCapacity) : 0;
            OceanKinematicsTuningDTO jobTuning = PrepareJobTuning(
                in tuning,
                count,
                tuning.Flags | OceanKinematicsConstants.FlagActive | OceanKinematicsConstants.FlagAnalyticalWave);
            EvaluateAnalyticalWavesJob job = new EvaluateAnalyticalWavesJob
            {
                Requests = requests,
                Results = results,
                Waves = waves,
                Tuning = jobTuning,
                RequestCount = count,
                WaveCount = waveCount
            };
            return job.ScheduleBatch(count, ResolveInnerLoopBatchCount(count), inputDeps);
        }

        /// <summary>
        /// Drains queued requests and schedules analytical sampling using dispatcher-owned deterministic tuning.
        /// </summary>
        public JobHandle ScheduleQueuedAnalyticalFluidSamples(
            NativeQueue<OceanKinematicsSampleRequestDTO> pendingRequests,
            NativeArray<OceanKinematicsSampleRequestDTO> packedRequests,
            NativeArray<FluidSampleResultDTO> results,
            NativeArray<GerstnerWaveDTO> waves,
            NativeArray<int> queueCounters,
            NativeParallelHashMap<uint, int> coalescingHashToIndex,
            int maxDrainCount,
            in OceanKinematicsTuningDTO tuning,
            JobHandle inputDeps)
        {
            if (!pendingRequests.IsCreated ||
                !packedRequests.IsCreated ||
                !results.IsCreated ||
                !queueCounters.IsCreated ||
                queueCounters.Length <= OceanKinematicsConstants.QueueCounterPacked)
            {
                return inputDeps;
            }

            int capacity = math.min(packedRequests.Length, results.Length);
            int drainBudget = math.max(0, maxDrainCount);
            if (capacity <= 0 || drainBudget <= 0)
                return inputDeps;
            int scheduleCount = math.min(capacity, drainBudget);

            JobHandle drainHandle = new DrainOceanSampleRequestQueueJob
            {
                PendingRequests = pendingRequests,
                PackedRequests = packedRequests,
                QueueCounters = queueCounters,
                CoalescingHashToIndex = coalescingHashToIndex,
                MaxDrainCount = drainBudget
            }.Schedule(inputDeps);

            int waveCount = waves.IsCreated ? math.min(waves.Length, OceanKinematicsConstants.WaveCapacity) : 0;
            OceanKinematicsTuningDTO jobTuning = PrepareJobTuning(
                in tuning,
                scheduleCount,
                tuning.Flags | OceanKinematicsConstants.FlagActive | OceanKinematicsConstants.FlagAnalyticalWave);
            EvaluateAnalyticalWavesJob evaluateJob = new EvaluateAnalyticalWavesJob
            {
                Requests = packedRequests,
                RequestCounter = queueCounters,
                Results = results,
                Waves = waves,
                Tuning = jobTuning,
                RequestCount = scheduleCount,
                WaveCount = waveCount
            };
            JobHandle evaluateHandle = evaluateJob.ScheduleBatch(scheduleCount, ResolveInnerLoopBatchCount(scheduleCount), drainHandle);
            return new CountOceanSampleDepthCullsJob
            {
                Requests = packedRequests,
                Results = results,
                QueueCounters = queueCounters,
                Tuning = jobTuning,
                RequestCount = scheduleCount,
                WaveCount = waveCount
            }.Schedule(evaluateHandle);
        }

        /// <summary>
        /// Exposes the multi-producer/single-consumer lane expected by KCC, submarine, and flora dispatchers.
        /// </summary>
        public static bool TryGetRequestParallelWriter(
            NativeQueue<OceanKinematicsSampleRequestDTO> pendingRequests,
            out NativeQueue<OceanKinematicsSampleRequestDTO>.ParallelWriter writer)
        {
            writer = default;
            if (!pendingRequests.IsCreated)
                return false;

            writer = pendingRequests.AsParallelWriter();
            return true;
        }

        /// <summary>
        /// Drains queued requests and resolves previous-frame cached water using dispatcher-owned deterministic tuning.
        /// </summary>
        public JobHandle ScheduleQueuedDearLieCachedFluidSamples(
            NativeQueue<OceanKinematicsSampleRequestDTO> pendingRequests,
            NativeArray<OceanKinematicsSampleRequestDTO> packedRequests,
            NativeArray<FluidSampleResultDTO> results,
            NativeArray<int> queueCounters,
            NativeParallelHashMap<uint, int> coalescingHashToIndex,
            NativeArray<OceanCachedFluidSampleDTO> cachedReadbackResults,
            int maxDrainCount,
            in OceanKinematicsTuningDTO tuning,
            JobHandle inputDeps)
        {
            if (!pendingRequests.IsCreated ||
                !packedRequests.IsCreated ||
                !results.IsCreated ||
                !queueCounters.IsCreated ||
                queueCounters.Length <= OceanKinematicsConstants.QueueCounterPacked)
            {
                return inputDeps;
            }

            int capacity = math.min(packedRequests.Length, results.Length);
            int drainBudget = math.max(0, maxDrainCount);
            if (capacity <= 0 || drainBudget <= 0)
                return inputDeps;
            int scheduleCount = math.min(capacity, drainBudget);

            JobHandle drainHandle = new DrainOceanSampleRequestQueueJob
            {
                PendingRequests = pendingRequests,
                PackedRequests = packedRequests,
                QueueCounters = queueCounters,
                CoalescingHashToIndex = coalescingHashToIndex,
                MaxDrainCount = drainBudget
            }.Schedule(inputDeps);

            OceanKinematicsTuningDTO jobTuning = PrepareJobTuning(
                in tuning,
                scheduleCount,
                tuning.Flags | OceanKinematicsConstants.FlagActive | OceanKinematicsConstants.FlagAsyncCached);
            ResolveDearLieCachedResultsJob cacheJob = new ResolveDearLieCachedResultsJob
            {
                Requests = packedRequests,
                RequestCounter = queueCounters,
                CachedResults = cachedReadbackResults,
                Results = results,
                Tuning = jobTuning,
                RequestCount = scheduleCount
            };
            JobHandle cacheHandle = cacheJob.ScheduleBatch(scheduleCount, ResolveInnerLoopBatchCount(scheduleCount), drainHandle);
            return new CountOceanSampleDepthCullsJob
            {
                Requests = packedRequests,
                Results = results,
                QueueCounters = queueCounters,
                Tuning = jobTuning,
                RequestCount = scheduleCount,
                WaveCount = OceanKinematicsConstants.WaveCapacity
            }.Schedule(cacheHandle);
        }

        /// <summary>
        /// Schedules deterministic emergency mock waves using dispatcher-owned deterministic tuning.
        /// </summary>
        public JobHandle ScheduleMockFluidSamples(
            NativeArray<OceanKinematicsSampleRequestDTO> requests,
            NativeArray<FluidSampleResultDTO> results,
            int sampleCount,
            in OceanKinematicsTuningDTO tuning,
            JobHandle inputDeps)
        {
            if (!requests.IsCreated || !results.IsCreated || sampleCount <= 0)
                return inputDeps;

            int count = math.min(sampleCount, math.min(requests.Length, results.Length));
            if (count <= 0)
                return inputDeps;

            OceanKinematicsTuningDTO jobTuning = PrepareJobTuning(
                in tuning,
                count,
                tuning.Flags | OceanKinematicsConstants.FlagActive | OceanKinematicsConstants.FlagMockWave);
            GenerateMockOceanWavesJob job = new GenerateMockOceanWavesJob
            {
                Requests = requests,
                Results = results,
                Tuning = jobTuning,
                RequestCount = count
            };
            return job.ScheduleBatch(count, ResolveInnerLoopBatchCount(count), inputDeps);
        }

        /// <summary>
        /// Schedules queued mock sampling using dispatcher-owned deterministic tuning.
        /// </summary>
        public JobHandle ScheduleQueuedMockFluidSamples(
            NativeQueue<OceanKinematicsSampleRequestDTO> pendingRequests,
            NativeArray<OceanKinematicsSampleRequestDTO> packedRequests,
            NativeArray<FluidSampleResultDTO> results,
            NativeArray<int> queueCounters,
            NativeParallelHashMap<uint, int> coalescingHashToIndex,
            int maxDrainCount,
            in OceanKinematicsTuningDTO tuning,
            JobHandle inputDeps)
        {
            if (!pendingRequests.IsCreated ||
                !packedRequests.IsCreated ||
                !results.IsCreated ||
                !queueCounters.IsCreated ||
                queueCounters.Length <= OceanKinematicsConstants.QueueCounterPacked)
            {
                return inputDeps;
            }

            int capacity = math.min(packedRequests.Length, results.Length);
            int drainBudget = math.max(0, maxDrainCount);
            if (capacity <= 0 || drainBudget <= 0)
                return inputDeps;
            int scheduleCount = math.min(capacity, drainBudget);

            JobHandle drainHandle = new DrainOceanSampleRequestQueueJob
            {
                PendingRequests = pendingRequests,
                PackedRequests = packedRequests,
                QueueCounters = queueCounters,
                CoalescingHashToIndex = coalescingHashToIndex,
                MaxDrainCount = drainBudget
            }.Schedule(inputDeps);

            OceanKinematicsTuningDTO jobTuning = PrepareJobTuning(
                in tuning,
                scheduleCount,
                tuning.Flags | OceanKinematicsConstants.FlagActive | OceanKinematicsConstants.FlagMockWave);
            GenerateMockOceanWavesJob job = new GenerateMockOceanWavesJob
            {
                Requests = packedRequests,
                RequestCounter = queueCounters,
                Results = results,
                Tuning = jobTuning,
                RequestCount = scheduleCount
            };
            JobHandle mockHandle = job.ScheduleBatch(scheduleCount, ResolveInnerLoopBatchCount(scheduleCount), drainHandle);
            return new CountOceanSampleDepthCullsJob
            {
                Requests = packedRequests,
                Results = results,
                QueueCounters = queueCounters,
                Tuning = jobTuning,
                RequestCount = scheduleCount,
                WaveCount = 4
            }.Schedule(mockHandle);
        }

        /// <summary>
        /// Schedules a completed GPU readback fold into the previous-frame Dear Lie cache from caller-owned staging memory.
        /// </summary>
        public static JobHandle ScheduleDearLieCacheUpdateFromStagedReadback(
            NativeArray<float4> stagedReadbackSamples,
            NativeArray<OceanKinematicsSampleRequestDTO> completedRequests,
            NativeArray<OceanCachedFluidSampleDTO> cachedReadbackResults,
            int completedCount,
            out int scheduledCount,
            JobHandle inputDeps)
        {
            scheduledCount = 0;
            if (!stagedReadbackSamples.IsCreated ||
                !completedRequests.IsCreated ||
                !cachedReadbackResults.IsCreated ||
                cachedReadbackResults.Length == 0 ||
                completedCount <= 0)
            {
                return inputDeps;
            }

            int count = math.min(completedCount, math.min(completedRequests.Length, stagedReadbackSamples.Length));
            if (count <= 0)
                return inputDeps;

            scheduledCount = count;
            UpdateDearLieCacheFromReadbackJob job = new UpdateDearLieCacheFromReadbackJob
            {
                CompletedRequests = completedRequests,
                ReadbackSamples = stagedReadbackSamples,
                CachedResults = cachedReadbackResults,
                CompletedCount = count
            };
            return job.Schedule(inputDeps);
        }

        private void Awake()
        {
            BindLocalOceanRendererIfMissing();
            int ownerHash = unchecked((int)EntityId.ToULong(GetEntityId()));
            _heightQueryOwnerHash = ownerHash;
            _waveQueryOwnerHash = ownerHash ^ 0x2F31;
            _displacementQueryOwnerHash = ownerHash ^ 0x53C9;
            _flowQueryOwnerHash = ownerHash ^ 0x7A4D;
        }

        private void OnEnable()
        {
            Hecton8.Core.OceanVisualBridgeRegistry.Register(this);
            Hecton8.Core.OceanKinematicsRuntimeService.RegisterProvider(this);
        }

        private void OnDisable()
        {
            Hecton8.Core.OceanKinematicsRuntimeService.UnregisterProvider(this);
            Hecton8.Core.OceanVisualBridgeRegistry.Unregister(this);
        }

        /// <inheritdoc />
        public override bool GetWaterHeight(Vector3[] samplePositions, int sampleCount, float minSpatialLength, float[] waterHeights)
        {
            if (!ValidateHeightRequest(samplePositions, sampleCount, waterHeights))
                return false;

            if (!TryReadCollisionProvider(out Crest.ICollProvider collisionProvider))
                return false;

            int queryStatus = collisionProvider.Query(
                _heightQueryOwnerHash,
                Mathf.Max(0.01f, minSpatialLength),
                samplePositions,
                waterHeights,
                null,
                null);
            return collisionProvider.RetrieveSucceeded(queryStatus);
        }

        /// <inheritdoc />
        public override bool GetWaterHeight(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<float> waterHeights)
        {
            if (!ValidateHeightRequest(samplePositions, sampleCount, waterHeights))
                return false;

            CopyNativePositions(samplePositions, sampleCount);
            bool succeeded = GetWaterHeight(_samplePositionScratch, sampleCount, minSpatialLength, _heightScratch);
            if (succeeded)
                CopyManagedHeightsToNative(_heightScratch, waterHeights, sampleCount);
            else
                FillNativeHeights(waterHeights, sampleCount, ResolveSeaLevel(TryReadBoundOceanRenderer()));
            return succeeded;
        }

        /// <inheritdoc />
        public override bool GetSurfaceFlow(Vector3[] samplePositions, int sampleCount, float minSpatialLength, Vector3[] surfaceFlows)
        {
            if (!ValidateVectorRequest(samplePositions, sampleCount, surfaceFlows))
                return false;

            Crest.OceanRenderer oceanRenderer = TryReadBoundOceanRenderer();
            if (oceanRenderer == null || oceanRenderer.FlowProvider == null)
                return false;

            Crest.IFlowProvider flowProvider = oceanRenderer.FlowProvider;
            int queryStatus = flowProvider.Query(
                _flowQueryOwnerHash,
                Mathf.Max(0.01f, minSpatialLength),
                samplePositions,
                surfaceFlows);
            return flowProvider.RetrieveSucceeded(queryStatus);
        }

        /// <inheritdoc />
        public override bool GetSurfaceFlow(NativeArray<Vector3> samplePositions, int sampleCount, float minSpatialLength, NativeArray<Vector3> surfaceFlows)
        {
            if (!ValidateVectorRequest(samplePositions, sampleCount, surfaceFlows))
                return false;

            CopyNativePositions(samplePositions, sampleCount);
            bool succeeded = GetSurfaceFlow(_samplePositionScratch, sampleCount, minSpatialLength, _flowScratch);
            if (succeeded)
                CopyManagedVectorsToNative(_flowScratch, surfaceFlows, sampleCount);
            else
                FillNativeVectors(surfaceFlows, sampleCount, Vector3.zero);
            return succeeded;
        }

        /// <inheritdoc />
        public override bool GetWaveNormal(
            Vector3[] samplePositions,
            int sampleCount,
            float minSpatialLength,
            Vector3[] waveNormals,
            Vector3[] surfaceVelocities,
            Vector3[] displacements)
        {
            if (!ValidateWaveRequest(samplePositions, sampleCount, waveNormals, surfaceVelocities, displacements))
                return false;

            if (!TryReadCollisionProvider(out Crest.ICollProvider collisionProvider))
                return false;

            float resolvedMinSpatialLength = Mathf.Max(0.01f, minSpatialLength);
            int waveStatus = collisionProvider.Query(
                _waveQueryOwnerHash,
                resolvedMinSpatialLength,
                samplePositions,
                _heightScratch,
                waveNormals,
                surfaceVelocities);
            bool waveSucceeded = collisionProvider.RetrieveSucceeded(waveStatus);
            if (!waveSucceeded)
                return false;

            int displacementStatus = collisionProvider.Query(
                _displacementQueryOwnerHash,
                resolvedMinSpatialLength,
                samplePositions,
                displacements,
                null,
                null);
            if (!collisionProvider.RetrieveSucceeded(displacementStatus))
            {
                for (int i = 0; i < sampleCount; i++)
                    displacements[i] = Vector3.zero;
            }

            return true;
        }

        /// <inheritdoc />
        public override bool GetWaveNormal(
            NativeArray<Vector3> samplePositions,
            int sampleCount,
            float minSpatialLength,
            NativeArray<Vector3> waveNormals,
            NativeArray<Vector3> surfaceVelocities,
            NativeArray<Vector3> displacements)
        {
            if (!ValidateWaveRequest(samplePositions, sampleCount, waveNormals, surfaceVelocities, displacements))
                return false;

            CopyNativePositions(samplePositions, sampleCount);
            bool succeeded = GetWaveNormal(
                _samplePositionScratch,
                sampleCount,
                minSpatialLength,
                _waveNormalScratch,
                _surfaceVelocityScratch,
                _displacementScratch);
            if (succeeded)
            {
                CopyManagedVectorsToNative(_waveNormalScratch, waveNormals, sampleCount);
                CopyManagedVectorsToNative(_surfaceVelocityScratch, surfaceVelocities, sampleCount);
                CopyManagedVectorsToNative(_displacementScratch, displacements, sampleCount);
            }
            else
            {
                FillNativeVectors(waveNormals, sampleCount, Vector3.up);
                FillNativeVectors(surfaceVelocities, sampleCount, Vector3.zero);
                FillNativeVectors(displacements, sampleCount, Vector3.zero);
            }

            return succeeded;
        }

        private bool TryReadCollisionProvider(out Crest.ICollProvider collisionProvider)
        {
            Crest.OceanRenderer oceanRenderer = TryReadBoundOceanRenderer();
            collisionProvider = oceanRenderer != null ? oceanRenderer.CollisionProvider : null;
            return collisionProvider != null;
        }

        private void BindLocalOceanRendererIfMissing()
        {
            if (crestOceanRenderer == null)
                TryGetComponent(out crestOceanRenderer);
        }

        protected override Crest.OceanRenderer ReadBoundOceanRenderer()
        {
            return crestOceanRenderer;
        }

        private Crest.OceanRenderer TryReadBoundOceanRenderer()
        {
            return ReadBoundOceanRenderer();
        }

        private Crest.OceanRenderer ResolveOceanRenderer()
        {
            return crestOceanRenderer;
        }

        private static double3 ResolveOceanRootAUP(Crest.OceanRenderer oceanRenderer)
        {
            double3 rootAup = Hecton8.Core.HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (oceanRenderer == null || oceanRenderer.Root == null)
                return math.select(double3.zero, rootAup, math.isfinite(rootAup));

            Vector3 rootPosition = oceanRenderer.Root.position;
            rootAup.x += rootPosition.x;
            rootAup.z += rootPosition.z;
            return math.select(double3.zero, rootAup, math.isfinite(rootAup));
        }

        private static float ResolveSeaLevel(Crest.OceanRenderer oceanRenderer)
        {
            if (oceanRenderer != null && oceanRenderer.Root != null)
                return oceanRenderer.Root.position.y;

            return 0f;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = Hecton8.Core.HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }

        private static int ResolveInnerLoopBatchCount(int count)
        {
            if (count >= 1024)
                return 64;

            if (count >= 128)
                return 32;

            return 16;
        }

        private static OceanKinematicsTuningDTO PrepareJobTuning(
            in OceanKinematicsTuningDTO source,
            int requestCount,
            uint flags)
        {
            OceanKinematicsTuningDTO tuning = source;
            tuning.OceanRootAUP = math.select(double3.zero, tuning.OceanRootAUP, math.isfinite(tuning.OceanRootAUP));
            tuning.OceanSurfaceY = math.select(0f, tuning.OceanSurfaceY, math.isfinite(tuning.OceanSurfaceY));
            tuning.GlobalQualityWeight = math.saturate(math.select(1f, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
            tuning.TimeSeconds = math.max(0f, math.select(0f, tuning.TimeSeconds, math.isfinite(tuning.TimeSeconds)));
            tuning.DepthCullingThresholdMeters = math.max(0f, math.select(OceanKinematicsConstants.DefaultDepthCullMeters, tuning.DepthCullingThresholdMeters, math.isfinite(tuning.DepthCullingThresholdMeters)));
            tuning.MaxOctaveLimit = math.clamp(tuning.MaxOctaveLimit, 1, OceanKinematicsConstants.WaveCapacity);
            tuning.WaveAmplitudeMultiplier = math.max(0f, math.select(OceanKinematicsConstants.DefaultAmplitudeMultiplier, tuning.WaveAmplitudeMultiplier, math.isfinite(tuning.WaveAmplitudeMultiplier)));
            tuning.RequestCount = math.max(0, requestCount);
            tuning.MaxPeakHeight = math.max(0f, math.select(0f, tuning.MaxPeakHeight, math.isfinite(tuning.MaxPeakHeight)));
            tuning.Flags = flags;
            return tuning;
        }

        private static bool ValidateHeightRequest(Vector3[] samplePositions, int sampleCount, float[] heights)
        {
            return samplePositions != null &&
                   heights != null &&
                   sampleCount > 0 &&
                   sampleCount <= MaxBatchSampleCount &&
                   samplePositions.Length >= sampleCount &&
                   heights.Length >= sampleCount;
        }

        private static bool ValidateHeightRequest(NativeArray<Vector3> samplePositions, int sampleCount, NativeArray<float> heights)
        {
            return samplePositions.IsCreated &&
                   heights.IsCreated &&
                   sampleCount > 0 &&
                   sampleCount <= MaxBatchSampleCount &&
                   samplePositions.Length >= sampleCount &&
                   heights.Length >= sampleCount;
        }

        private static bool ValidateVectorRequest(Vector3[] samplePositions, int sampleCount, Vector3[] vectors)
        {
            return samplePositions != null &&
                   vectors != null &&
                   sampleCount > 0 &&
                   sampleCount <= MaxBatchSampleCount &&
                   samplePositions.Length >= sampleCount &&
                   vectors.Length >= sampleCount;
        }

        private static bool ValidateVectorRequest(NativeArray<Vector3> samplePositions, int sampleCount, NativeArray<Vector3> vectors)
        {
            return samplePositions.IsCreated &&
                   vectors.IsCreated &&
                   sampleCount > 0 &&
                   sampleCount <= MaxBatchSampleCount &&
                   samplePositions.Length >= sampleCount &&
                   vectors.Length >= sampleCount;
        }

        private static bool ValidateWaveRequest(
            Vector3[] samplePositions,
            int sampleCount,
            Vector3[] waveNormals,
            Vector3[] surfaceVelocities,
            Vector3[] displacements)
        {
            return ValidateVectorRequest(samplePositions, sampleCount, waveNormals) &&
                   surfaceVelocities != null &&
                   displacements != null &&
                   surfaceVelocities.Length >= sampleCount &&
                   displacements.Length >= sampleCount;
        }

        private static bool ValidateWaveRequest(
            NativeArray<Vector3> samplePositions,
            int sampleCount,
            NativeArray<Vector3> waveNormals,
            NativeArray<Vector3> surfaceVelocities,
            NativeArray<Vector3> displacements)
        {
            return ValidateVectorRequest(samplePositions, sampleCount, waveNormals) &&
                   surfaceVelocities.IsCreated &&
                   displacements.IsCreated &&
                   surfaceVelocities.Length >= sampleCount &&
                   displacements.Length >= sampleCount;
        }

        private void CopyNativePositions(NativeArray<Vector3> samplePositions, int sampleCount)
        {
            for (int i = 0; i < sampleCount; i++)
                _samplePositionScratch[i] = samplePositions[i];
        }

        private static void CopyManagedHeightsToNative(float[] source, NativeArray<float> destination, int sampleCount)
        {
            for (int i = 0; i < sampleCount; i++)
                destination[i] = source[i];
        }

        private static void CopyManagedVectorsToNative(Vector3[] source, NativeArray<Vector3> destination, int sampleCount)
        {
            for (int i = 0; i < sampleCount; i++)
                destination[i] = source[i];
        }

        private static void FillNativeHeights(NativeArray<float> destination, int sampleCount, float value)
        {
            int count = math.min(sampleCount, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = value;
        }

        private static void FillNativeVectors(NativeArray<Vector3> destination, int sampleCount, Vector3 value)
        {
            int count = math.min(sampleCount, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = value;
        }
    }
}
