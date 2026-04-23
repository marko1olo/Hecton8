using System;
using Hecton8.Bootstrap;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Hecton8.Core
{
    internal interface IFoveatedSimulationTarget : IUpdatable
    {
        int FoveatedTargetIndex { get; set; }
        Transform SimulationTransform { get; }
        Transform VisualTransform { get; }
        AudioSource DopplerAudioSource { get; }
        void OnFoveatedCadenceResolved(FoveatedTickRate tickRate, float tickIntervalSeconds, float importanceScore, bool insideFrustum);
        bool TryBuildDeferredRaycastCommand(out RaycastCommand command);
        void ConsumeDeferredRaycastHit(in RaycastHit hit);
    }

    internal enum FoveatedTickRate : byte
    {
        Center60Hz = 0,
        Periphery20Hz = 1,
        Rear5Hz = 2,
    }

    /// <summary>
    /// Dispatcher-owned simulation foveation service. Computes importance scores,
    /// throttles opt-in targets, smooths low-frequency visual motion, and keeps
    /// audio/raycast side effects on an allocation-free path.
    /// </summary>
    internal sealed class FoveatedSimulationManager : IDisposable
    {
        [BurstCompile]
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

        private const int MaxTargets = 512;
        private const int MinimumCommandsPerJob = 1;
        private const float CenterTickIntervalSeconds = 1.0f / 60.0f;
        private const float PeripheryTickIntervalSeconds = 1.0f / 20.0f;
        private const float RearTickIntervalSeconds = 1.0f / 5.0f;
        private const float CameraResolveRetryInterval = 1.0f;
        private const float ListenerResolveRetryInterval = 1.0f;
        private const float MaximumScoreDistanceMeters = 180.0f;
        private const float CenterDotThreshold = 0.72f;
        private const float BehindCameraDotThreshold = -0.1f;
        private const float HighImportanceThreshold = 0.67f;
        private const float DistanceWeight = 0.58f;
        private const float FrustumWeight = 0.42f;
        private const float MinimumDirectionLength = 0.0001f;
        private const float MinimumVelocityDelta = 0.0001f;
        private const float MinimumDeferredRaycastImportanceScore = 0.2f;
        private const float SoundSpeedWaterMetersPerSecond = 1480.0f;
        private const float MinimumPitch = 0.5f;
        private const float MaximumPitch = 2.0f;
        private const float CenterVelocitySmoothingSharpness = 18.0f;
        private const float PeripheryVelocitySmoothingSharpness = 10.0f;
        private const float RearVelocitySmoothingSharpness = 5.0f;

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
        // COLD ALLOC: int[512] — compact target-to-visual-transform mapping — owner: FoveatedSimulationManager
        private readonly int[] _visualTargetIndices = new int[MaxTargets];
        // COLD ALLOC: IFoveatedSimulationTarget[512] — deferred raycast owners for same-frame dispatch — owner: FoveatedSimulationManager
        private readonly IFoveatedSimulationTarget[] _deferredRaycastOwners = new IFoveatedSimulationTarget[MaxTargets];

        private TransformAccessArray _visualTransformAccessArray;
        private Transform[] _visualTransformArray = Array.Empty<Transform>();
        private NativeArray<float3> _jobFromPositions;
        private NativeArray<float3> _jobToPositions;
        private NativeArray<float> _jobAlphas;
        private NativeList<RaycastCommand> _deferredRaycastCommands;
        private NativeArray<RaycastHit> _deferredRaycastResults;
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
        private bool _interpolationScheduled;
        private bool _deferredRaycastScheduled;
        private bool _listenerStateInitialized;

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
            CompleteFrameJobs();

            if (_deferredRaycastCommands.IsCreated)
                _deferredRaycastCommands.Clear();

            if (!TryResolveViewCamera(frameDeltaTime))
                return;

            TryResolveListener(frameDeltaTime);
            UpdateListenerVelocity(frameDeltaTime);
            RecalculateImportanceScores();
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
            Vector3 rawVelocity = (currentPosition - previousPosition) / deltaTime;
            float velocityBlend = 1.0f - math.exp(-ResolveVelocitySmoothingSharpness(_tickRates[index]) * deltaTime);
            _smoothedVelocities[index] = Vector3.Lerp(_smoothedVelocities[index], rawVelocity, velocityBlend);

            if (_deferredRaycastCommands.IsCreated &&
                _deferredRaycastCommands.Length < MaxTargets &&
                _importanceScores[index] >= MinimumDeferredRaycastImportanceScore &&
                target.TryBuildDeferredRaycastCommand(out RaycastCommand raycastCommand))
            {
                int commandIndex = _deferredRaycastCommands.Length;
                _deferredRaycastOwners[commandIndex] = target;
                _deferredRaycastCommands.Add(raycastCommand);
            }
        }

        public void ScheduleFrameJobs()
        {
            if (_visualTargetCacheDirty)
                RebuildVisualTargetCache();

            if (_visualTargetCount > 0)
                ScheduleInterpolationJob();

            if (_deferredRaycastCommands.IsCreated && _deferredRaycastCommands.Length > 0)
            {
                _deferredRaycastHandle = RaycastCommand.ScheduleBatch(
                    _deferredRaycastCommands.AsDeferredJobArray(),
                    _deferredRaycastResults,
                    MinimumCommandsPerJob,
                    default);
                _deferredRaycastScheduled = true;
            }
        }

        public void CompleteFrameJobs()
        {
            if (_interpolationScheduled)
            {
                _interpolationHandle.Complete();
                _interpolationScheduled = false;
            }

            if (_deferredRaycastScheduled)
            {
                _deferredRaycastHandle.Complete();
                int raycastCount = _deferredRaycastCommands.Length;
                for (int i = 0; i < raycastCount; i++)
                {
                    IFoveatedSimulationTarget owner = _deferredRaycastOwners[i];
                    if (owner != null)
                        owner.ConsumeDeferredRaycastHit(_deferredRaycastResults[i]);

                    _deferredRaycastOwners[i] = null;
                }

                _deferredRaycastScheduled = false;
            }
        }

        public void ResetRuntimeState()
        {
            CompleteFrameJobs();
            DisposeVisualTransformAccessArray();
            DisposeNativeBuffers();

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
            Array.Clear(_visualTargetIndices, 0, _visualTargetIndices.Length);
            Array.Clear(_deferredRaycastOwners, 0, _deferredRaycastOwners.Length);

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
            _listenerStateInitialized = false;
            _interpolationScheduled = false;
            _deferredRaycastScheduled = false;
            _visualTransformArray = Array.Empty<Transform>();
        }

        public void Dispose()
        {
            ResetRuntimeState();
        }

        private void RecalculateImportanceScores()
        {
            if (_cameraTransform == null)
                return;

            float3 cameraPosition = _cameraTransform.position;
            float3 cameraForward = _cameraTransform.forward;

            for (int i = 0; i < _targetCount; i++)
            {
                IFoveatedSimulationTarget target = _targets[i];
                Transform simulationTransform = _simulationTransforms[i];
                if (simulationTransform == null || target == null)
                    continue;

                float3 targetPosition = simulationTransform.position;
                float3 toTarget = targetPosition - cameraPosition;
                float distanceSq = math.lengthsq(toTarget);
                float inverseDistance = math.rsqrt(math.max(distanceSq, MinimumDirectionLength));
                float distanceMeters = distanceSq > MinimumDirectionLength
                    ? distanceSq * inverseDistance
                    : 0.0f;
                float3 directionToTarget = distanceSq > MinimumDirectionLength
                    ? toTarget * inverseDistance
                    : cameraForward;
                float dotToFrustum = math.dot(cameraForward, directionToTarget);
                float distanceFactor = 1.0f - math.saturate(distanceMeters / MaximumScoreDistanceMeters);
                float frustumFactor = math.saturate((dotToFrustum - BehindCameraDotThreshold) / (1.0f - BehindCameraDotThreshold));
                float importanceScore = math.saturate((distanceFactor * DistanceWeight) + (frustumFactor * FrustumWeight));
                bool insideFrustum = dotToFrustum > BehindCameraDotThreshold;

                _importanceScores[i] = importanceScore;
                _tickRates[i] = ResolveTickRate(importanceScore, dotToFrustum);
                _tickIntervals[i] = ResolveTickInterval(_tickRates[i]);
                target.OnFoveatedCadenceResolved(_tickRates[i], _tickIntervals[i], importanceScore, insideFrustum);
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

                if (_tickRates[targetIndex] == FoveatedTickRate.Rear5Hz)
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

            if (!_deferredRaycastCommands.IsCreated)
            {
                _deferredRaycastCommands = new NativeList<RaycastCommand>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeList<RaycastCommand>[512] — deferred throttled-entity physics commands — owner: FoveatedSimulationManager
            }

            if (!_deferredRaycastResults.IsCreated)
            {
                _deferredRaycastResults = new NativeArray<RaycastHit>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeArray<RaycastHit>[512] — deferred throttled-entity raycast hits — owner: FoveatedSimulationManager
            }
        }

        private void DisposeNativeBuffers()
        {
            if (_jobFromPositions.IsCreated)
                _jobFromPositions.Dispose();

            if (_jobToPositions.IsCreated)
                _jobToPositions.Dispose();

            if (_jobAlphas.IsCreated)
                _jobAlphas.Dispose();

            if (_deferredRaycastCommands.IsCreated)
                _deferredRaycastCommands.Dispose();

            if (_deferredRaycastResults.IsCreated)
                _deferredRaycastResults.Dispose();

            _jobFromPositions = default;
            _jobToPositions = default;
            _jobAlphas = default;
            _deferredRaycastCommands = default;
            _deferredRaycastResults = default;
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
                    _viewCamera = playerTransform.GetComponentInChildren<Camera>(true);
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

        private static FoveatedTickRate ResolveTickRate(float importanceScore, float dotToFrustum)
        {
            if (dotToFrustum <= BehindCameraDotThreshold)
                return FoveatedTickRate.Rear5Hz;

            if (importanceScore >= HighImportanceThreshold && dotToFrustum >= CenterDotThreshold)
                return FoveatedTickRate.Center60Hz;

            return FoveatedTickRate.Periphery20Hz;
        }

        private static float ResolveTickInterval(FoveatedTickRate tickRate)
        {
            switch (tickRate)
            {
                case FoveatedTickRate.Center60Hz:
                    return CenterTickIntervalSeconds;
                case FoveatedTickRate.Periphery20Hz:
                    return PeripheryTickIntervalSeconds;
                default:
                    return RearTickIntervalSeconds;
            }
        }

        private static float ResolveVelocitySmoothingSharpness(FoveatedTickRate tickRate)
        {
            switch (tickRate)
            {
                case FoveatedTickRate.Center60Hz:
                    return CenterVelocitySmoothingSharpness;
                case FoveatedTickRate.Periphery20Hz:
                    return PeripheryVelocitySmoothingSharpness;
                default:
                    return RearVelocitySmoothingSharpness;
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
        }
    }
}
