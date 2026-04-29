using System;
using Hecton8.Bootstrap;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Hecton8.Core
{
    internal interface IFoveatedDispatcher : IDisposable
    {
        void RegisterTarget(IFoveatedSimulationTarget target);
        void UnregisterTarget(IFoveatedSimulationTarget target);
        void BeginDispatcherFrame(float frameDeltaTime);
        bool TryResolveTick(IUpdatable item, float frameDeltaTime, out float effectiveDeltaTime);
        void NotifyTickCompleted(IUpdatable item);
        void ScheduleFrameJobs();
        void CompleteFrameJobs();
        void ResetRuntimeState();
    }

    internal interface IFoveatedSimulationTarget : IUpdatable
    {
        int FoveatedTargetIndex { get; set; }
        Transform SimulationTransform { get; }
        Transform VisualTransform { get; }
        AudioSource DopplerAudioSource { get; }
        void OnFoveatedCadenceResolved(FoveatedTickRate tickRate, float tickIntervalSeconds, float importanceScore, bool insideFrustum);
        int BuildDeferredRaycastCommands(RaycastCommand[] commands);
        void ConsumeDeferredRaycastHit(int commandIndex, in RaycastHit hit);
    }

    internal enum FoveatedTickRate : byte
    {
        Center60Hz = 0,
        Focus30Hz = 1,
        Periphery20Hz = 2,
        Far10Hz = 3,
        Rear5Hz = 4,
        Rear1Hz = 5,
        CulledEcosystemOnly = 6,
    }

    /// <summary>
    /// Dispatcher-owned simulation foveation service. Computes importance scores,
    /// throttles opt-in targets, smooths low-frequency visual motion, and keeps
    /// audio/raycast side effects on an allocation-free path.
    /// </summary>
    internal sealed class FoveatedSimulationManager : IFoveatedDispatcher
    {
        private const double SlowJobCompleteWarningMilliseconds = 100.0;

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private struct ImportanceScoringJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Positions;
            public NativeArray<float> ImportanceScores;
            public NativeArray<byte> TickRateCodes;
            public NativeArray<byte> InsideFrustumFlags;
            public float3 CameraPosition;
            public float3 CameraForward;
            public float3 CameraUp;

            public void Execute(int index)
            {
                float3 toTarget = Positions[index] - CameraPosition;
                float distanceSq = math.lengthsq(toTarget);
                float safeDistanceSq = math.max(distanceSq, MinimumDirectionLength);
                float inverseDistance = math.rsqrt(safeDistanceSq);
                float3 directionToTarget = math.select(CameraForward, toTarget * inverseDistance, distanceSq > MinimumDirectionLength);
                float distanceMeters = math.select(0.0f, distanceSq * inverseDistance, distanceSq > MinimumDirectionLength);
                float forwardDot = math.clamp(math.dot(directionToTarget, CameraForward), -1.0f, 1.0f);
                bool behindCamera = forwardDot <= 0.0f;
                float frontHemisphereDot = math.saturate(forwardDot);
                float distanceFactor = 1.0f / (1.0f + (distanceMeters * DistanceDecay));
                float verticalDot = math.abs(math.dot(directionToTarget, CameraUp));
                float verticalPenalty = math.select(1.0f, VerticalPenaltyScale, verticalDot > VerticalPenaltyDotThreshold);
                float importanceScore = math.saturate(distanceFactor * frontHemisphereDot);
                importanceScore *= verticalPenalty;
                importanceScore = math.select(importanceScore, MinimumImportanceScore, behindCamera);

                bool rearOneHertz = behindCamera && distanceMeters > RearOneHertzDistanceMeters;
                bool ecosystemOnlyCull = behindCamera && distanceMeters > EcosystemOnlyCullDistanceMeters;
                int tickRateCode = (int)FoveatedTickRate.Rear5Hz;
                tickRateCode = math.select(tickRateCode, (int)FoveatedTickRate.Far10Hz, importanceScore >= LowImportanceThreshold);
                tickRateCode = math.select(tickRateCode, (int)FoveatedTickRate.Periphery20Hz, importanceScore >= MidImportanceThreshold);
                tickRateCode = math.select(tickRateCode, (int)FoveatedTickRate.Focus30Hz, importanceScore >= FocusImportanceThreshold);
                tickRateCode = math.select(tickRateCode, (int)FoveatedTickRate.Center60Hz, importanceScore >= HighImportanceThreshold);
                tickRateCode = math.select(tickRateCode, (int)FoveatedTickRate.Rear1Hz, rearOneHertz);
                tickRateCode = math.select(tickRateCode, (int)FoveatedTickRate.CulledEcosystemOnly, ecosystemOnlyCull);
                importanceScore = math.select(importanceScore, 0.0f, ecosystemOnlyCull);

                ImportanceScores[index] = importanceScore;
                TickRateCodes[index] = (byte)tickRateCode;
                InsideFrustumFlags[index] = behindCamera ? (byte)0 : (byte)1;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private struct VisualInterpolationJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<float3> FromPositions;
            [ReadOnly] public NativeArray<float3> ToPositions;
            [ReadOnly] public NativeArray<float> Alphas;

            public void Execute(int index, TransformAccess transform)
            {
                if (!transform.isValid)
                    return;

                float alpha = math.saturate(Alphas[index]);
                float smoothAlpha = alpha * alpha * (3.0f - (2.0f * alpha));
                transform.position = math.lerp(FromPositions[index], ToPositions[index], smoothAlpha);
            }
        }

        private const int ImportanceScoreBatchSize = 32;
        private const int MaxTargets = 512;
        private const int MaxDeferredRaycastCommandsPerTarget = 3;
        private const int MaxDeferredRaycastCommands = MaxTargets * MaxDeferredRaycastCommandsPerTarget;
        private const int MinimumCommandsPerJob = 1;
        private const float CenterTickIntervalSeconds = 1.0f / 60.0f;
        private const float FocusTickIntervalSeconds = 1.0f / 30.0f;
        private const float PeripheryTickIntervalSeconds = 1.0f / 20.0f;
        private const float FarTickIntervalSeconds = 1.0f / 10.0f;
        private const float RearTickIntervalSeconds = 1.0f / 5.0f;
        private const float RearOneHertzTickIntervalSeconds = 1.0f;
        private const float CulledEcosystemTickIntervalSeconds = 0.5f;
        private const float CameraResolveRetryInterval = 1.0f;
        private const float ListenerResolveRetryInterval = 1.0f;
        private const int CadenceHysteresisFrames = 30;
        private const float HighImportanceThreshold = 0.75f;
        private const float FocusImportanceThreshold = 0.50f;
        private const float MidImportanceThreshold = 0.30f;
        private const float LowImportanceThreshold = 0.15f;
        private const float MinimumImportanceScore = 0.0f;
        private const float DistanceDecay = 0.01f;
        private const float VerticalPenaltyDotThreshold = 0.7f;
        private const float VerticalPenaltyScale = 0.6f;
        private const float MinimumDirectionLength = 0.0001f;
        private const float MinimumVelocityDelta = 0.0001f;
        private const float MinimumDeferredRaycastImportanceScore = 0.2f;
        private const float RearOneHertzDistanceMeters = 100.0f;
        private const float EcosystemOnlyCullDistanceMeters = 300.0f;
        private const float SoundSpeedWaterMetersPerSecond = 1480.0f;
        private const float MinimumPitch = 0.5f;
        private const float MaximumPitch = 2.0f;
        private const float CenterVelocitySmoothingSharpness = 18.0f;
        private const float FocusVelocitySmoothingSharpness = 14.0f;
        private const float PeripheryVelocitySmoothingSharpness = 10.0f;
        private const float FarVelocitySmoothingSharpness = 7.0f;
        private const float RearVelocitySmoothingSharpness = 5.0f;
        private const float RearOneHertzVelocitySmoothingSharpness = 2.0f;
        private const float CulledEcosystemVelocitySmoothingSharpness = 1.0f;
        private const long PersistentNativeBudgetBytes = 262144L;
        private const string MemoryBudgetOwnerName = "FoveatedSimulationManager";

        // COLD ALLOC: IFoveatedSimulationTarget[512] — dispatcher-owned opt-in simulation targets — owner: FoveatedSimulationManager
        private readonly IFoveatedSimulationTarget[] _targets = new IFoveatedSimulationTarget[MaxTargets];
        // COLD ALLOC: Transform[512] — simulation transform cache for scoring and cadence — owner: FoveatedSimulationManager
        private readonly Transform[] _simulationTransforms = new Transform[MaxTargets];
        // COLD ALLOC: Transform[512] — visual transform cache for 5 Hz interpolation — owner: FoveatedSimulationManager
        private readonly Transform[] _visualTransforms = new Transform[MaxTargets];
        // COLD ALLOC: AudioSource[512] — Doppler-protected audio emitters — owner: FoveatedSimulationManager
        private readonly AudioSource[] _dopplerAudioSources = new AudioSource[MaxTargets];
        // COLD ALLOC: Vector3[512] — previous visual sample positions — owner: FoveatedSimulationManager
        private readonly Vector3[] _visualFromPositions = new Vector3[MaxTargets];
        // COLD ALLOC: Vector3[512] — latest visual sample positions — owner: FoveatedSimulationManager
        private readonly Vector3[] _visualToPositions = new Vector3[MaxTargets];
        // COLD ALLOC: Vector3[512] — smoothed visual velocities used for manual Doppler — owner: FoveatedSimulationManager
        private readonly Vector3[] _smoothedVelocities = new Vector3[MaxTargets];
        // COLD ALLOC: float[512] — importance score cache — owner: FoveatedSimulationManager
        private readonly float[] _importanceScores = new float[MaxTargets];
        // COLD ALLOC: float[512] — dispatcher accumulation cache — owner: FoveatedSimulationManager
        private readonly float[] _tickAccumulators = new float[MaxTargets];
        // COLD ALLOC: float[512] — active interval cache — owner: FoveatedSimulationManager
        private readonly float[] _tickIntervals = new float[MaxTargets];
        // COLD ALLOC: float[512] — last effective tick delta cache — owner: FoveatedSimulationManager
        private readonly float[] _lastTickDeltas = new float[MaxTargets];
        // COLD ALLOC: FoveatedTickRate[512] — current rate classification cache — owner: FoveatedSimulationManager
        private readonly FoveatedTickRate[] _tickRates = new FoveatedTickRate[MaxTargets];
        private readonly int[] _framesSinceTickRateChange = new int[MaxTargets];
        // COLD ALLOC: int[512] — compact target-to-visual-transform mapping — owner: FoveatedSimulationManager
        private readonly int[] _visualTargetIndices = new int[MaxTargets];
        // COLD ALLOC: IFoveatedSimulationTarget[512] — deferred raycast owners for same-frame dispatch — owner: FoveatedSimulationManager
        private readonly IFoveatedSimulationTarget[] _deferredRaycastOwners = new IFoveatedSimulationTarget[MaxDeferredRaycastCommands];
        private readonly int[] _deferredRaycastCommandIndices = new int[MaxDeferredRaycastCommands];
        private readonly RaycastCommand[] _deferredRaycastScratchCommands = new RaycastCommand[MaxDeferredRaycastCommandsPerTarget];

        private TransformAccessArray _visualTransformAccessArray;
        private Transform[] _visualTransformArray = Array.Empty<Transform>();
        private NativeArray<float3> _jobScorePositions;
        private NativeArray<float> _jobImportanceScores;
        private NativeArray<byte> _jobTickRateCodes;
        private NativeArray<byte> _jobInsideFrustumFlags;
        private NativeArray<float3> _jobFromPositions;
        private NativeArray<float3> _jobToPositions;
        private NativeArray<float> _jobAlphas;
        private NativeQueue<RaycastCommand> _pendingDeferredRaycastCommands;
        private NativeQueue<int> _pendingDeferredRaycastOwnerIndices;
        private NativeQueue<int> _pendingDeferredRaycastCommandIndices;
        private NativeList<RaycastCommand> _deferredRaycastCommands;
        private NativeArray<RaycastHit> _deferredRaycastResults;
        private JobHandle _importanceHandle;
        private JobHandle _interpolationHandle;
        private JobHandle _deferredRaycastHandle;

        private Camera _viewCamera;
        private Transform _cameraTransform;
        private Transform _listenerTransform;
        private Rigidbody _listenerRigidbody;
        private Vector3 _listenerVelocity;
        private Vector3 _lastListenerPosition;

        private int _targetCount;
        private int _visualTargetCount;
        private float _cameraResolveRetryTimer;
        private float _listenerResolveRetryTimer;
        private bool _visualTargetCacheDirty = true;
        private bool _importanceScheduled;
        private bool _interpolationScheduled;
        private bool _deferredRaycastScheduled;
        private bool _listenerStateInitialized;
        private int _queuedDeferredRaycastCount;
        private int _lastDeferredRaycastScheduleFrame = -1;

        public void RegisterTarget(IFoveatedSimulationTarget target)
        {
            if (target == null)
                return;

            if (target.FoveatedTargetIndex >= 0)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_targetCount >= MaxTargets)
            {
                throw new InvalidOperationException(
                    $"[FoveatedSimulationManager] Target capacity ({MaxTargets}) exceeded.");
            }
#endif

            int index = _targetCount;
            _targetCount++;
            _targets[index] = target;
            _simulationTransforms[index] = target.SimulationTransform;
            _visualTransforms[index] = target.VisualTransform;
            _dopplerAudioSources[index] = target.DopplerAudioSource;
            _tickIntervals[index] = CenterTickIntervalSeconds;
            _tickRates[index] = FoveatedTickRate.Center60Hz;
            _tickAccumulators[index] = CenterTickIntervalSeconds;
            _lastTickDeltas[index] = CenterTickIntervalSeconds;
            _importanceScores[index] = 1.0f;
            _framesSinceTickRateChange[index] = CadenceHysteresisFrames;
            target.FoveatedTargetIndex = index;

            Vector3 initialPosition = SampleVisualPosition(index);
            _visualFromPositions[index] = initialPosition;
            _visualToPositions[index] = initialPosition;
            _smoothedVelocities[index] = Vector3.zero;

            AudioSource audioSource = _dopplerAudioSources[index];
            if (audioSource != null)
                audioSource.dopplerLevel = 0.0f;

            target.OnFoveatedCadenceResolved(FoveatedTickRate.Center60Hz, CenterTickIntervalSeconds, 1.0f, true);
            _visualTargetCacheDirty = true;
            EnsureNativeBuffersAllocated();
        }

        public void UnregisterTarget(IFoveatedSimulationTarget target)
        {
            if (target == null)
                return;

            int removedIndex = target.FoveatedTargetIndex;
            if (removedIndex < 0 || removedIndex >= _targetCount)
                return;

            int lastIndex = _targetCount - 1;
            if (removedIndex != lastIndex)
            {
                IFoveatedSimulationTarget swappedTarget = _targets[lastIndex];
                _targets[removedIndex] = swappedTarget;
                _simulationTransforms[removedIndex] = _simulationTransforms[lastIndex];
                _visualTransforms[removedIndex] = _visualTransforms[lastIndex];
                _dopplerAudioSources[removedIndex] = _dopplerAudioSources[lastIndex];
                _visualFromPositions[removedIndex] = _visualFromPositions[lastIndex];
                _visualToPositions[removedIndex] = _visualToPositions[lastIndex];
                _smoothedVelocities[removedIndex] = _smoothedVelocities[lastIndex];
                _importanceScores[removedIndex] = _importanceScores[lastIndex];
                _tickAccumulators[removedIndex] = _tickAccumulators[lastIndex];
                _tickIntervals[removedIndex] = _tickIntervals[lastIndex];
                _lastTickDeltas[removedIndex] = _lastTickDeltas[lastIndex];
                _tickRates[removedIndex] = _tickRates[lastIndex];
                _framesSinceTickRateChange[removedIndex] = _framesSinceTickRateChange[lastIndex];

                if (swappedTarget != null)
                    swappedTarget.FoveatedTargetIndex = removedIndex;
            }

            ClearSlot(lastIndex);
            _targetCount = lastIndex;
            target.FoveatedTargetIndex = -1;
            _visualTargetCacheDirty = true;
        }

        public void BeginDispatcherFrame(float frameDeltaTime)
        {
            CompleteFrameJobsInternal(true);
            EnsureNativeBuffersAllocated();

            if (_deferredRaycastCommands.IsCreated)
                _deferredRaycastCommands.Clear();

            if (!TryResolveViewCamera(frameDeltaTime))
                return;

            TryResolveListener(frameDeltaTime);
            UpdateListenerVelocity(frameDeltaTime);
            UpdateDopplerProtection();
        }

        public bool TryResolveTick(IUpdatable item, float frameDeltaTime, out float effectiveDeltaTime)
        {
            if (!(item is IFoveatedSimulationTarget target))
            {
                effectiveDeltaTime = frameDeltaTime;
                return true;
            }

            int index = target.FoveatedTargetIndex;
            if (index < 0 || index >= _targetCount || !ReferenceEquals(_targets[index], target))
            {
                effectiveDeltaTime = frameDeltaTime;
                return true;
            }

            _tickAccumulators[index] += frameDeltaTime;
            float tickInterval = _tickIntervals[index];
            if (_tickAccumulators[index] + MinimumVelocityDelta < tickInterval)
            {
                effectiveDeltaTime = 0.0f;
                return false;
            }

            effectiveDeltaTime = _tickAccumulators[index];
            _lastTickDeltas[index] = effectiveDeltaTime;
            _tickAccumulators[index] = 0.0f;
            return true;
        }

        public void NotifyTickCompleted(IUpdatable item)
        {
            if (!(item is IFoveatedSimulationTarget target))
                return;

            int index = target.FoveatedTargetIndex;
            if (index < 0 || index >= _targetCount || !ReferenceEquals(_targets[index], target))
                return;

            Vector3 previousPosition = _visualToPositions[index];
            Vector3 currentPosition = SampleVisualPosition(index);
            _visualFromPositions[index] = previousPosition;
            _visualToPositions[index] = currentPosition;

            float deltaTime = math.max(_lastTickDeltas[index], MinimumVelocityDelta);
            Vector3 rawVelocity = Vector3.zero;
            if (TryResolveSafeReciprocal(deltaTime, out float inverseDeltaTime))
                rawVelocity = SanitizeFiniteVector((currentPosition - previousPosition) * inverseDeltaTime);
            float velocityBlend = 1.0f - math.exp(-ResolveVelocitySmoothingSharpness(_tickRates[index]) * deltaTime);
            float3 smoothedVelocity = math.lerp(
                new float3(_smoothedVelocities[index].x, _smoothedVelocities[index].y, _smoothedVelocities[index].z),
                new float3(rawVelocity.x, rawVelocity.y, rawVelocity.z),
                velocityBlend);
            _smoothedVelocities[index] = new Vector3(smoothedVelocity.x, smoothedVelocity.y, smoothedVelocity.z);

            if (_deferredRaycastCommands.IsCreated &&
                _pendingDeferredRaycastCommands.IsCreated &&
                _pendingDeferredRaycastOwnerIndices.IsCreated &&
                _pendingDeferredRaycastCommandIndices.IsCreated &&
                _queuedDeferredRaycastCount < MaxDeferredRaycastCommands &&
                _importanceScores[index] >= MinimumDeferredRaycastImportanceScore)
            {
                int commandCount = target.BuildDeferredRaycastCommands(_deferredRaycastScratchCommands);
                int safeCommandCount = math.clamp(commandCount, 0, MaxDeferredRaycastCommandsPerTarget);
                for (int commandIndex = 0;
                     commandIndex < safeCommandCount && _queuedDeferredRaycastCount < MaxDeferredRaycastCommands;
                     commandIndex++)
                {
                    _pendingDeferredRaycastCommands.Enqueue(_deferredRaycastScratchCommands[commandIndex]);
                    _pendingDeferredRaycastOwnerIndices.Enqueue(index);
                    _pendingDeferredRaycastCommandIndices.Enqueue(commandIndex);
                    _queuedDeferredRaycastCount++;
                }
            }
        }

        private static bool TryResolveSafeReciprocal(float value, out float reciprocal)
        {
            if (!float.IsFinite(value) || math.abs(value) <= MinimumVelocityDelta)
            {
                reciprocal = 0f;
                return false;
            }

            reciprocal = 1f / value;
            return float.IsFinite(reciprocal);
        }

        private static Vector3 SanitizeFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z)
                ? value
                : Vector3.zero;
        }

        public void ScheduleFrameJobs()
        {
            if (_visualTargetCacheDirty)
                RebuildVisualTargetCache();

            if (_visualTargetCount > 0)
                ScheduleInterpolationJob();

            DrainDeferredRaycastQueues();
            int currentFrame = Time.frameCount;
            if (_deferredRaycastCommands.IsCreated &&
                _deferredRaycastCommands.Length > 0 &&
                _lastDeferredRaycastScheduleFrame != currentFrame)
            {
                _deferredRaycastHandle = RaycastCommand.ScheduleBatch(
                    _deferredRaycastCommands.AsDeferredJobArray(),
                    _deferredRaycastResults,
                    MinimumCommandsPerJob,
                    default);
                _deferredRaycastScheduled = true;
                _lastDeferredRaycastScheduleFrame = currentFrame;
            }

            ScheduleImportanceScoringJob();
        }

        public void CompleteFrameJobs()
        {
            CompleteFrameJobsInternal(false);
        }

        private void CompleteFrameJobsInternal(bool includeDeferredRaycasts)
        {
            if (_interpolationScheduled)
            {
                CompleteJobWithWarning(ref _interpolationHandle, "FoveatedSimulationManager.Interpolation");
                _interpolationScheduled = false;
            }

            if (includeDeferredRaycasts && _deferredRaycastScheduled)
            {
                CompleteJobWithWarning(ref _deferredRaycastHandle, "FoveatedSimulationManager.DeferredRaycasts");
                int raycastCount = _deferredRaycastCommands.Length;
                for (int i = 0; i < raycastCount; i++)
                {
                    IFoveatedSimulationTarget owner = _deferredRaycastOwners[i];
                    if (owner != null)
                        owner.ConsumeDeferredRaycastHit(_deferredRaycastCommandIndices[i], _deferredRaycastResults[i]);

                    _deferredRaycastOwners[i] = null;
                    _deferredRaycastCommandIndices[i] = 0;
                }

                _deferredRaycastScheduled = false;
            }

            if (_importanceScheduled)
            {
                CompleteJobWithWarning(ref _importanceHandle, "FoveatedSimulationManager.ImportanceScoring");
                ApplyImportanceResults();
                _importanceScheduled = false;
            }
        }

        public void ResetRuntimeState()
        {
            CompleteFrameJobsInternal(true);
            DisposeVisualTransformAccessArray();
            DisposeNativeBuffers(JobHandle.CombineDependencies(_importanceHandle, JobHandle.CombineDependencies(_interpolationHandle, _deferredRaycastHandle)));
            _importanceHandle = default;
            _interpolationHandle = default;
            _deferredRaycastHandle = default;

            Array.Clear(_targets, 0, _targets.Length);
            Array.Clear(_simulationTransforms, 0, _simulationTransforms.Length);
            Array.Clear(_visualTransforms, 0, _visualTransforms.Length);
            Array.Clear(_dopplerAudioSources, 0, _dopplerAudioSources.Length);
            Array.Clear(_visualFromPositions, 0, _visualFromPositions.Length);
            Array.Clear(_visualToPositions, 0, _visualToPositions.Length);
            Array.Clear(_smoothedVelocities, 0, _smoothedVelocities.Length);
            Array.Clear(_importanceScores, 0, _importanceScores.Length);
            Array.Clear(_tickAccumulators, 0, _tickAccumulators.Length);
            Array.Clear(_tickIntervals, 0, _tickIntervals.Length);
            Array.Clear(_lastTickDeltas, 0, _lastTickDeltas.Length);
            Array.Clear(_tickRates, 0, _tickRates.Length);
            Array.Clear(_framesSinceTickRateChange, 0, _framesSinceTickRateChange.Length);
            Array.Clear(_visualTargetIndices, 0, _visualTargetIndices.Length);
            Array.Clear(_deferredRaycastOwners, 0, _deferredRaycastOwners.Length);
            Array.Clear(_deferredRaycastCommandIndices, 0, _deferredRaycastCommandIndices.Length);

            _viewCamera = null;
            _cameraTransform = null;
            _listenerTransform = null;
            _listenerRigidbody = null;
            _listenerVelocity = Vector3.zero;
            _lastListenerPosition = Vector3.zero;
            _targetCount = 0;
            _visualTargetCount = 0;
            _cameraResolveRetryTimer = 0.0f;
            _listenerResolveRetryTimer = 0.0f;
            _visualTargetCacheDirty = true;
            _importanceScheduled = false;
            _listenerStateInitialized = false;
            _interpolationScheduled = false;
            _deferredRaycastScheduled = false;
            _queuedDeferredRaycastCount = 0;
            _lastDeferredRaycastScheduleFrame = -1;
            _visualTransformArray = Array.Empty<Transform>();
        }

        public void Dispose()
        {
            ResetRuntimeState();
        }

        private static void CompleteJobWithWarning(ref JobHandle handle, string systemName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            handle.Complete();
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds > SlowJobCompleteWarningMilliseconds)
            {
                Debug.LogWarning(
                    $"[SystemDispatcher] JobHandle.Complete slow: {systemName} took {elapsedMilliseconds:F2}ms.");
            }
#else
            handle.Complete();
#endif
        }

        private void ScheduleImportanceScoringJob()
        {
            if (_cameraTransform == null || _targetCount <= 0)
                return;

            EnsureNativeBuffersAllocated();

            for (int i = 0; i < _targetCount; i++)
            {
                Transform simulationTransform = _simulationTransforms[i];
                _jobScorePositions[i] = simulationTransform != null
                    ? (float3)simulationTransform.position
                    : float3.zero;
            }

            ImportanceScoringJob scoringJob = new ImportanceScoringJob
            {
                Positions = _jobScorePositions,
                ImportanceScores = _jobImportanceScores,
                TickRateCodes = _jobTickRateCodes,
                InsideFrustumFlags = _jobInsideFrustumFlags,
                CameraPosition = _cameraTransform.position,
                CameraForward = _cameraTransform.forward,
                CameraUp = _cameraTransform.up,
            };

            _importanceHandle = scoringJob.Schedule(_targetCount, ImportanceScoreBatchSize);
            _importanceScheduled = true;
        }

        private void ApplyImportanceResults()
        {
            for (int i = 0; i < _targetCount; i++)
            {
                IFoveatedSimulationTarget target = _targets[i];
                if (target == null)
                    continue;

                float importanceScore = _jobImportanceScores[i];
                FoveatedTickRate resolvedTickRate = (FoveatedTickRate)_jobTickRateCodes[i];
                FoveatedTickRate currentTickRate = _tickRates[i];
                bool immediateRearDemotion = _jobInsideFrustumFlags[i] == 0 &&
                                             (int)resolvedTickRate >= (int)FoveatedTickRate.Rear5Hz;
                if (!immediateRearDemotion &&
                    math.abs((int)resolvedTickRate - (int)currentTickRate) == 1 &&
                    _framesSinceTickRateChange[i] < CadenceHysteresisFrames)
                {
                    resolvedTickRate = currentTickRate;
                }

                if (resolvedTickRate != currentTickRate)
                {
                    _tickRates[i] = resolvedTickRate;
                    _framesSinceTickRateChange[i] = 0;
                }
                else if (_framesSinceTickRateChange[i] < int.MaxValue)
                {
                    _framesSinceTickRateChange[i]++;
                }

                _importanceScores[i] = importanceScore;
                _tickIntervals[i] = ResolveTickInterval(_tickRates[i]);
                _tickAccumulators[i] = math.min(_tickAccumulators[i], _tickIntervals[i]);
                target.OnFoveatedCadenceResolved(_tickRates[i], _tickIntervals[i], importanceScore, _jobInsideFrustumFlags[i] != 0);
            }
        }

        private void UpdateDopplerProtection()
        {
            if (_listenerTransform == null)
                return;

            float3 listenerPosition = _listenerTransform.position;
            float3 listenerVelocity = _listenerVelocity;

            for (int i = 0; i < _targetCount; i++)
            {
                AudioSource audioSource = _dopplerAudioSources[i];
                if (audioSource == null)
                    continue;

                audioSource.dopplerLevel = 0.0f;
                float3 toListener = listenerPosition - (float3)audioSource.transform.position;
                float distanceSq = math.lengthsq(toListener);
                if (distanceSq <= MinimumDirectionLength)
                {
                    audioSource.pitch = 1.0f;
                    continue;
                }

                float inverseDistance = math.rsqrt(distanceSq);
                float3 toListenerDir = toListener * inverseDistance;
                float3 relativeVelocity = (float3)_smoothedVelocities[i] - listenerVelocity;
                float approachSpeed = math.dot(relativeVelocity, toListenerDir);
                audioSource.pitch = math.clamp(
                    1.0f + (approachSpeed / SoundSpeedWaterMetersPerSecond),
                    MinimumPitch,
                    MaximumPitch);
            }
        }

        private void ScheduleInterpolationJob()
        {
            EnsureNativeBuffersAllocated();

            for (int compactIndex = 0; compactIndex < _visualTargetCount; compactIndex++)
            {
                int targetIndex = _visualTargetIndices[compactIndex];
                Vector3 currentPosition = _visualToPositions[targetIndex];

                if (_tickRates[targetIndex] != FoveatedTickRate.Center60Hz)
                {
                    _jobFromPositions[compactIndex] = _visualFromPositions[targetIndex];
                    _jobToPositions[compactIndex] = currentPosition;
                    _jobAlphas[compactIndex] = math.saturate(
                        _tickAccumulators[targetIndex] / math.max(_tickIntervals[targetIndex], MinimumVelocityDelta));
                }
                else
                {
                    _jobFromPositions[compactIndex] = currentPosition;
                    _jobToPositions[compactIndex] = currentPosition;
                    _jobAlphas[compactIndex] = 1.0f;
                }
            }

            VisualInterpolationJob interpolationJob = new VisualInterpolationJob
            {
                FromPositions = _jobFromPositions,
                ToPositions = _jobToPositions,
                Alphas = _jobAlphas,
            };

            _interpolationHandle = IJobParallelForTransformExtensions.ScheduleByRef(
                ref interpolationJob,
                _visualTransformAccessArray,
                default);
            _interpolationScheduled = true;
        }

        private void RebuildVisualTargetCache()
        {
            _visualTargetCount = 0;
            for (int i = 0; i < _targetCount; i++)
            {
                Transform visualTransform = _visualTransforms[i];
                if (visualTransform == null)
                    continue;

                _visualTargetIndices[_visualTargetCount] = i;
                _visualTargetCount++;
            }

            if (_visualTransformArray.Length != _visualTargetCount)
            {
                _visualTransformArray = _visualTargetCount == 0
                    ? Array.Empty<Transform>()
                    : new Transform[_visualTargetCount]; // COLD ALLOC: Transform[visualTargetCount] — compact interpolation cache for low-frequency visuals — owner: FoveatedSimulationManager
            }

            for (int i = 0; i < _visualTargetCount; i++)
            {
                int targetIndex = _visualTargetIndices[i];
                _visualTransformArray[i] = _visualTransforms[targetIndex];
            }

            DisposeVisualTransformAccessArray();
            if (_visualTargetCount > 0)
            {
                TransformAccessArray.Allocate(_visualTargetCount, -1, out _visualTransformAccessArray);
                _visualTransformAccessArray.SetTransforms(_visualTransformArray);
            }

            _visualTargetCacheDirty = false;
        }

        private void EnsureNativeBuffersAllocated()
        {
            if (!_jobScorePositions.IsCreated)
            {
                _jobScorePositions = new NativeArray<float3>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeArray<float3>[512] - simulation positions for Burst cadence scoring - owner: FoveatedSimulationManager
            }

            if (!_jobImportanceScores.IsCreated)
            {
                _jobImportanceScores = new NativeArray<float>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeArray<float>[512] - Burst importance score output buffer - owner: FoveatedSimulationManager
            }

            if (!_jobTickRateCodes.IsCreated)
            {
                _jobTickRateCodes = new NativeArray<byte>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeArray<byte>[512] - Burst raw cadence tier codes before hysteresis - owner: FoveatedSimulationManager
            }

            if (!_jobInsideFrustumFlags.IsCreated)
            {
                _jobInsideFrustumFlags = new NativeArray<byte>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeArray<byte>[512] - Burst front hemisphere visibility flags - owner: FoveatedSimulationManager
            }
            if (!_jobFromPositions.IsCreated)
            {
                _jobFromPositions = new NativeArray<float3>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeArray<float3>[512] — interpolation source positions — owner: FoveatedSimulationManager
            }

            if (!_jobToPositions.IsCreated)
            {
                _jobToPositions = new NativeArray<float3>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeArray<float3>[512] — interpolation target positions — owner: FoveatedSimulationManager
            }

            if (!_jobAlphas.IsCreated)
            {
                _jobAlphas = new NativeArray<float>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeArray<float>[512] — interpolation alpha payloads — owner: FoveatedSimulationManager
            }

            if (!_pendingDeferredRaycastCommands.IsCreated)
            {
                _pendingDeferredRaycastCommands = new NativeQueue<RaycastCommand>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RaycastCommand>[1536] - next-frame deferred fauna sight-line requests - owner: FoveatedSimulationManager
            }

            if (!_pendingDeferredRaycastOwnerIndices.IsCreated)
            {
                _pendingDeferredRaycastOwnerIndices = new NativeQueue<int>(Allocator.Persistent); // COLD ALLOC: NativeQueue<int>[1536] - deferred fauna sight-line owner indices aligned to queued commands - owner: FoveatedSimulationManager
            }

            if (!_pendingDeferredRaycastCommandIndices.IsCreated)
            {
                _pendingDeferredRaycastCommandIndices = new NativeQueue<int>(Allocator.Persistent); // COLD ALLOC: NativeQueue<int>[1536] - deferred fauna sight-line command slot indices aligned to queued commands - owner: FoveatedSimulationManager
            }

            if (!_deferredRaycastCommands.IsCreated)
            {
                _deferredRaycastCommands = new NativeList<RaycastCommand>(MaxDeferredRaycastCommands, Allocator.Persistent); // COLD ALLOC: NativeList<RaycastCommand>[512] — deferred throttled-entity physics commands — owner: FoveatedSimulationManager
            }

            if (!_deferredRaycastResults.IsCreated)
            {
                _deferredRaycastResults = new NativeArray<RaycastHit>(MaxDeferredRaycastCommands, Allocator.Persistent); // COLD ALLOC: NativeArray<RaycastHit>[512] — deferred throttled-entity raycast hits — owner: FoveatedSimulationManager
            }
            RegisterNativeMemoryBudget();
        }

        private void DisposeNativeBuffers(JobHandle dependency)
        {
            MemoryBudgetTracker.Unregister(MemoryBudgetOwnerName);
            DisposeNativeArray(ref _jobScorePositions, dependency);
            DisposeNativeArray(ref _jobImportanceScores, dependency);
            DisposeNativeArray(ref _jobTickRateCodes, dependency);
            DisposeNativeArray(ref _jobInsideFrustumFlags, dependency);
            DisposeNativeArray(ref _jobFromPositions, dependency);
            DisposeNativeArray(ref _jobToPositions, dependency);
            DisposeNativeArray(ref _jobAlphas, dependency);
            DisposeNativeQueue(ref _pendingDeferredRaycastCommands);
            DisposeNativeQueue(ref _pendingDeferredRaycastOwnerIndices);
            DisposeNativeQueue(ref _pendingDeferredRaycastCommandIndices);
            DisposeNativeList(ref _deferredRaycastCommands, dependency);
            DisposeNativeArray(ref _deferredRaycastResults, dependency);

            _jobScorePositions = default;
            _jobImportanceScores = default;
            _jobTickRateCodes = default;
            _jobInsideFrustumFlags = default;
            _jobFromPositions = default;
            _jobToPositions = default;
            _jobAlphas = default;
            _pendingDeferredRaycastCommands = default;
            _pendingDeferredRaycastOwnerIndices = default;
            _pendingDeferredRaycastCommandIndices = default;
            _deferredRaycastCommands = default;
            _deferredRaycastResults = default;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            array.Dispose(dependency);
            array = default;
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, JobHandle dependency) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            list.Dispose(dependency);
            list = default;
        }

        private static void DisposeNativeQueue<T>(ref NativeQueue<T> queue) where T : unmanaged
        {
            if (!queue.IsCreated)
                return;

            queue.Dispose();
            queue = default;
        }

        private void DrainDeferredRaycastQueues()
        {
            if (!_deferredRaycastCommands.IsCreated)
                return;

            _deferredRaycastCommands.Clear();
            Array.Clear(_deferredRaycastOwners, 0, _deferredRaycastOwners.Length);
            Array.Clear(_deferredRaycastCommandIndices, 0, _deferredRaycastCommandIndices.Length);

            int commandIndex = 0;
            while (commandIndex < MaxDeferredRaycastCommands &&
                   _pendingDeferredRaycastCommands.IsCreated &&
                   _pendingDeferredRaycastOwnerIndices.IsCreated &&
                   _pendingDeferredRaycastCommandIndices.IsCreated &&
                   _pendingDeferredRaycastCommands.TryDequeue(out RaycastCommand command) &&
                   _pendingDeferredRaycastOwnerIndices.TryDequeue(out int ownerIndex) &&
                   _pendingDeferredRaycastCommandIndices.TryDequeue(out int ownerCommandIndex))
            {
                _deferredRaycastCommands.Add(command);
                _deferredRaycastOwners[commandIndex] = ownerIndex >= 0 && ownerIndex < _targetCount ? _targets[ownerIndex] : null;
                _deferredRaycastCommandIndices[commandIndex] = ownerCommandIndex;
                commandIndex++;
            }

            if (_pendingDeferredRaycastCommands.IsCreated)
            {
                while (_pendingDeferredRaycastCommands.TryDequeue(out _))
                {
                }
            }

            if (_pendingDeferredRaycastOwnerIndices.IsCreated)
            {
                while (_pendingDeferredRaycastOwnerIndices.TryDequeue(out _))
                {
                }
            }

            if (_pendingDeferredRaycastCommandIndices.IsCreated)
            {
                while (_pendingDeferredRaycastCommandIndices.TryDequeue(out _))
                {
                }
            }

            _queuedDeferredRaycastCount = 0;
        }

        private void RegisterNativeMemoryBudget()
        {
            long totalBytes = GetNativeArrayBytes(_jobScorePositions) +
                              GetNativeArrayBytes(_jobImportanceScores) +
                              GetNativeArrayBytes(_jobTickRateCodes) +
                              GetNativeArrayBytes(_jobInsideFrustumFlags) +
                              GetNativeArrayBytes(_jobFromPositions) +
                              GetNativeArrayBytes(_jobToPositions) +
                              GetNativeArrayBytes(_jobAlphas) +
                              GetNativeArrayBytes(_deferredRaycastResults) +
                              GetNativeListBytes(_deferredRaycastCommands);
            MemoryBudgetTracker.Register(MemoryBudgetOwnerName, totalBytes, PersistentNativeBudgetBytes);
        }

        private static long GetNativeArrayBytes<T>(NativeArray<T> array) where T : struct
        {
            return array.IsCreated ? (long)array.Length * UnsafeUtility.SizeOf<T>() : 0L;
        }

        private static long GetNativeListBytes<T>(NativeList<T> list) where T : unmanaged
        {
            return list.IsCreated ? (long)list.Capacity * UnsafeUtility.SizeOf<T>() : 0L;
        }

        private void DisposeVisualTransformAccessArray()
        {
            if (_visualTransformAccessArray.isCreated)
                _visualTransformAccessArray.Dispose();
        }

        private bool TryResolveViewCamera(float frameDeltaTime)
        {
            if (_cameraTransform != null)
                return true;

            if (_cameraResolveRetryTimer > 0.0f)
            {
                _cameraResolveRetryTimer -= math.max(frameDeltaTime, 0.0f);
                return false;
            }

            _cameraResolveRetryTimer = CameraResolveRetryInterval;
            _viewCamera = null;
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (!playerTransform.TryGetComponent(out _viewCamera))
                    _viewCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
            }

            if (_viewCamera == null)
                return false;

            _cameraTransform = _viewCamera.transform;
            _cameraResolveRetryTimer = 0.0f;
            return true;
        }

        private void TryResolveListener(float frameDeltaTime)
        {
            if (_listenerTransform != null)
                return;

            if (_listenerResolveRetryTimer > 0.0f)
            {
                _listenerResolveRetryTimer -= math.max(frameDeltaTime, 0.0f);
                return;
            }

            _listenerResolveRetryTimer = ListenerResolveRetryInterval;
            _listenerTransform = _cameraTransform;
            _listenerRigidbody = null;

            GameObject playerObject = SceneBootstrap.CurrentPlayerObject;
            if (playerObject != null)
                playerObject.TryGetComponent(out _listenerRigidbody);

            if (_listenerTransform != null)
                _listenerResolveRetryTimer = 0.0f;
        }

        private void UpdateListenerVelocity(float frameDeltaTime)
        {
            if (_listenerTransform == null)
                return;

            if (_listenerRigidbody != null)
            {
                _listenerVelocity = _listenerRigidbody.linearVelocity;
                _lastListenerPosition = _listenerTransform.position;
                _listenerStateInitialized = true;
                return;
            }

            Vector3 currentListenerPosition = _listenerTransform.position;
            if (!_listenerStateInitialized || frameDeltaTime <= MinimumVelocityDelta)
            {
                _listenerVelocity = Vector3.zero;
                _lastListenerPosition = currentListenerPosition;
                _listenerStateInitialized = true;
                return;
            }

            _listenerVelocity = (currentListenerPosition - _lastListenerPosition) / frameDeltaTime;
            _lastListenerPosition = currentListenerPosition;
        }

        private Vector3 SampleVisualPosition(int targetIndex)
        {
            Transform visualTransform = _visualTransforms[targetIndex];
            if (visualTransform != null)
                return visualTransform.position;

            Transform simulationTransform = _simulationTransforms[targetIndex];
            return simulationTransform != null ? simulationTransform.position : Vector3.zero;
        }

        private static float ResolveTickInterval(FoveatedTickRate tickRate)
        {
            switch (tickRate)
            {
                case FoveatedTickRate.Center60Hz:
                    return CenterTickIntervalSeconds;
                case FoveatedTickRate.Focus30Hz:
                    return FocusTickIntervalSeconds;
                case FoveatedTickRate.Periphery20Hz:
                    return PeripheryTickIntervalSeconds;
                case FoveatedTickRate.Far10Hz:
                    return FarTickIntervalSeconds;
                case FoveatedTickRate.Rear5Hz:
                    return RearTickIntervalSeconds;
                case FoveatedTickRate.Rear1Hz:
                    return RearOneHertzTickIntervalSeconds;
                default:
                    return CulledEcosystemTickIntervalSeconds;
            }
        }

        private static float ResolveVelocitySmoothingSharpness(FoveatedTickRate tickRate)
        {
            switch (tickRate)
            {
                case FoveatedTickRate.Center60Hz:
                    return CenterVelocitySmoothingSharpness;
                case FoveatedTickRate.Focus30Hz:
                    return FocusVelocitySmoothingSharpness;
                case FoveatedTickRate.Periphery20Hz:
                    return PeripheryVelocitySmoothingSharpness;
                case FoveatedTickRate.Far10Hz:
                    return FarVelocitySmoothingSharpness;
                case FoveatedTickRate.Rear5Hz:
                    return RearVelocitySmoothingSharpness;
                case FoveatedTickRate.Rear1Hz:
                    return RearOneHertzVelocitySmoothingSharpness;
                default:
                    return CulledEcosystemVelocitySmoothingSharpness;
            }
        }

        private void ClearSlot(int index)
        {
            _targets[index] = null;
            _simulationTransforms[index] = null;
            _visualTransforms[index] = null;
            _dopplerAudioSources[index] = null;
            _visualFromPositions[index] = Vector3.zero;
            _visualToPositions[index] = Vector3.zero;
            _smoothedVelocities[index] = Vector3.zero;
            _importanceScores[index] = 0.0f;
            _tickAccumulators[index] = 0.0f;
            _tickIntervals[index] = 0.0f;
            _lastTickDeltas[index] = 0.0f;
            _tickRates[index] = FoveatedTickRate.Center60Hz;
            _framesSinceTickRateChange[index] = 0;
        }
    }
}


