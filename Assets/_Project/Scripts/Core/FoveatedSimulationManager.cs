using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using CameraFrustumSignal = Hecton8.Core.Contracts.Signals.CameraFrustumSignal;
using CameraPositionSignal = Hecton8.Core.Contracts.Signals.CameraPositionSignal;
using CombatDamageSignal = Hecton8.Core.Contracts.Signals.CombatDamageSignal;

namespace Hecton8.Core
{
    internal interface IFoveatedDispatcher : IDisposable
    {
        void InitializeRuntime();
        void RegisterTarget(IFoveatedSimulationTarget target);
        void UnregisterTarget(IFoveatedSimulationTarget target);
        void BeginDispatcherFrame(float frameDeltaTime);
        bool TryResolveTick(IUpdatable item, float frameDeltaTime, out float effectiveDeltaTime);
        void NotifyTickCompleted(IUpdatable item);
        void ScheduleFrameJobs();
        bool TryCompleteFrameJobs();
        void CompleteFrameJobs();
        void SetVoxelTeardownBackpressure(bool active, int pendingChunkCount);
        void ApplyHomeostasisPressureTier(byte pressureTier);
        void ResetRuntimeState();
    }

    internal interface IFoveatedSimulationTarget : IUpdatable
    {
        int FoveatedTargetIndex { get; set; }
        Transform SimulationTransform { get; }
        Transform VisualTransform { get; }
        AudioSource DopplerAudioSource { get; }
        uint FoveatedEntityHash { get; }
        ushort FoveatedEntityId { get; }
        void OnFoveatedCadenceResolved(FoveatedTickRate tickRate, float tickIntervalSeconds, float importanceScore, bool insideFrustum);
        void OnFoveatedTierResolved(FoveatedSimulationTier tier, float distanceMeters, bool tier0Locked);
        bool TryHandleFoveatedFrozenWrap(Vector3 cameraPosition, Vector3 cameraForward, float distanceMeters);
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
    internal sealed class FoveatedSimulationManager : IFoveatedDispatcher, IFoveatedSimulationDirector, IOriginShiftListener
    {
        private const double SlowJobCompleteWarningMilliseconds = 100.0;
        private const string SlowJobCompleteWarningMessage = "[SystemDispatcher] JobHandle.Complete slow in foveated simulation swap window.";

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct ImportanceScoringJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Positions;
            public NativeArray<float3> EntityAups;
            public NativeArray<float> ImportanceScores;
            public NativeArray<byte> TickRateCodes;
            public NativeArray<byte> InsideFrustumFlags;
            public NativeArray<byte> EntitySimTiers;
            public NativeArray<float> DistancesMeters;
            public float3 CameraPosition;
            public float3 CameraForward;
            public float3 CameraUp;
            public float ActiveDistanceMeters;
            public float FrozenDistanceMeters;
            public float FrustumForwardDotThreshold;

            public void Execute(int index)
            {
                float3 position = Positions[index];
                EntityAups[index] = position;
                float3 safeForward = math.normalizesafe(CameraForward, new float3(0f, 0f, 1f));
                float3 toTarget = position - CameraPosition;
                float distanceSq = math.lengthsq(toTarget);
                float safeDistanceSq = math.max(distanceSq, MinimumDirectionLength);
                float inverseDistance = math.rsqrt(safeDistanceSq);
                float3 directionToTarget = math.select(safeForward, toTarget * inverseDistance, distanceSq > MinimumDirectionLength);
                float distanceMeters = math.select(0.0f, distanceSq * inverseDistance, distanceSq > MinimumDirectionLength);
                float forwardDot = math.clamp(math.dot(directionToTarget, safeForward), -1.0f, 1.0f);
                bool insideFrustum = forwardDot >= FrustumForwardDotThreshold;
                bool frozen = distanceMeters > FrozenDistanceMeters;
                bool peripheral = !frozen && (!insideFrustum || distanceMeters >= ActiveDistanceMeters);
                int tierCode = (int)FoveatedSimulationTier.Active;
                tierCode = math.select(tierCode, (int)FoveatedSimulationTier.Peripheral, peripheral);
                tierCode = math.select(tierCode, (int)FoveatedSimulationTier.Frozen, frozen);

                float distanceFactor = 1.0f / (1.0f + (distanceMeters * DistanceDecay));
                float importanceScore = math.select(1.0f, math.saturate(distanceFactor), peripheral);
                importanceScore = math.select(importanceScore, 0.0f, frozen);
                int tickRateCode = (int)FoveatedTickRate.Center60Hz;
                tickRateCode = math.select(tickRateCode, (int)FoveatedTickRate.Rear1Hz, peripheral);
                tickRateCode = math.select(tickRateCode, (int)FoveatedTickRate.CulledEcosystemOnly, frozen);

                ImportanceScores[index] = importanceScore;
                TickRateCodes[index] = (byte)tickRateCode;
                InsideFrustumFlags[index] = insideFrustum ? (byte)1 : (byte)0;
                EntitySimTiers[index] = (byte)tierCode;
                DistancesMeters[index] = distanceMeters;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
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

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct FoveatedSimulationTelemetryEntry
        {
            public int Frame;
            public int TargetCount;
            public int FrozenEntityCount;
            public int Tier0Count;
            public int Tier1Count;
            public int Tier2Count;
            public float3 CameraPosition;
            public float3 CameraForward;
            public uint Flags;
            public uint StateHash;
            public uint Reserved0;
            public uint Reserved1;
        }

        private const int ImportanceScoreBatchSize = 32;
        private const int MaxTargets = 512;
        private const int MaxDeferredRaycastCommandsPerTarget = 4;
        private const int MaxDeferredRaycastCommands = 256;
        private const int MaxDeferredRaycastCommandsPerFrame = 16;
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
        private const float DefaultActiveDistanceMeters = 100.0f;
        private const float DefaultFrozenDistanceMeters = 300.0f;
        private const float LowActiveDistanceMeters = 50.0f;
        private const float LowFrozenDistanceMeters = 150.0f;
        private const float FrozenWrapDistanceMeters = 600.0f;
        private const float FrozenWrapForwardDistanceMeters = 200.0f;
        private const float ImportanceEvaluationIntervalSeconds = 0.1f;
        private const float FrustumForwardDotThreshold = 0.34202015f;
        private const float Tier0CombatLockSeconds = 10.0f;
        private const int TelemetryCapacity = 300;
        private const uint TelemetryMagic = 0x46384C44u;
        private const float SoundSpeedWaterMetersPerSecond = 1480.0f;
        private const float MinimumPitch = 0.5f;
        private const float MaximumPitch = 2.0f;
        private const float VoxelTeardownBackpressureSwimSpeedMultiplier = 0.5f;
        private const float CenterVelocitySmoothingSharpness = 18.0f;
        private const float FocusVelocitySmoothingSharpness = 14.0f;
        private const float PeripheryVelocitySmoothingSharpness = 10.0f;
        private const float FarVelocitySmoothingSharpness = 7.0f;
        private const float RearVelocitySmoothingSharpness = 5.0f;
        private const float RearOneHertzVelocitySmoothingSharpness = 2.0f;
        private const float CulledEcosystemVelocitySmoothingSharpness = 1.0f;
        private const long PersistentNativeBudgetBytes = 393216L;
        private const string MemoryBudgetOwnerName = "FoveatedSimulationManager";
        private const string BlackBoxDumpFileName = "Dump_FOVEATED_SIMULATION_DIRECTOR.bin";

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
        private readonly FoveatedSimulationTier[] _simTiers = new FoveatedSimulationTier[MaxTargets];
        private readonly uint[] _entityHashes = new uint[MaxTargets];
        private readonly ushort[] _entityIds = new ushort[MaxTargets];
        private readonly float[] _tier0LockUntilTimes = new float[MaxTargets];
        private readonly int[] _framesSinceTickRateChange = new int[MaxTargets];
        // COLD ALLOC: int[512] — compact target-to-visual-transform mapping — owner: FoveatedSimulationManager
        private readonly int[] _visualTargetIndices = new int[MaxTargets];
        // COLD ALLOC: IFoveatedSimulationTarget[512] — deferred raycast owners for same-frame dispatch — owner: FoveatedSimulationManager
        private readonly IFoveatedSimulationTarget[] _deferredRaycastOwners = new IFoveatedSimulationTarget[MaxDeferredRaycastCommands];
        // COLD ALLOC: IFoveatedSimulationTarget[256] — pending deferred raycast owner refs immune to target index swap — owner: FoveatedSimulationManager
        private readonly IFoveatedSimulationTarget[] _pendingDeferredRaycastOwners = new IFoveatedSimulationTarget[MaxDeferredRaycastCommands];
        private readonly int[] _deferredRaycastCommandIndices = new int[MaxDeferredRaycastCommands];
        private readonly RaycastCommand[] _deferredRaycastScratchCommands = new RaycastCommand[MaxDeferredRaycastCommandsPerTarget];

        private TransformAccessArray _visualTransformAccessArray;
        private Transform[] _visualTransformArray = Array.Empty<Transform>();
        private NativeArray<float3> _jobScorePositions;
        private NativeArray<float3> _jobEntityAups;
        private NativeArray<float> _jobImportanceScores;
        private NativeArray<byte> _jobTickRateCodes;
        private NativeArray<byte> _jobInsideFrustumFlags;
        private NativeArray<byte> _jobEntitySimTiers;
        private NativeArray<float> _jobDistancesMeters;
        private NativeArray<float3> _jobFromPositions;
        private NativeArray<float3> _jobToPositions;
        private NativeArray<float> _jobAlphas;
        private NativeArray<FoveatedSimulationTelemetryEntry> _telemetryRing;
        private NativeArray<RaycastCommand> _pendingDeferredRaycastCommands;
        private NativeArray<int> _pendingDeferredRaycastCommandIndices;
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
        private bool _originShiftListenerRegistered;
        private bool _nativeMemorySentinelRegistered;
        private bool _nativeMemoryBudgetRegistered;
        private bool _voxelTeardownBackpressureActive;
        private bool _forceImmediateImportanceRefresh;
        private bool _hasSignalCameraPose;
        private bool _blackBoxDumped;
        private int _voxelTeardownBackpressurePendingCount;
        private int _queuedDeferredRaycastCount;
        private int _pendingDeferredRaycastHead;
        private int _pendingDeferredRaycastTail;
        private int _lastDeferredRaycastScheduleFrame = -1;
        private int _frozenEntityCount;
        private int _tier0Count;
        private int _tier1Count;
        private int _tier2Count;
        private byte _homeostasisPressureTier;
        private int _telemetryCursor;
        private float _importanceAccumulator;
        private float _activeDistanceMeters = DefaultActiveDistanceMeters;
        private float _frozenDistanceMeters = DefaultFrozenDistanceMeters;
        private float _thermalFrozenDistanceMeters = DefaultFrozenDistanceMeters;
        private Vector3 _signalCameraPosition;
        private Vector3 _signalCameraForward = Vector3.forward;
        private Vector3 _signalCameraUp = Vector3.up;
        private bool _thermalFreezeOverrideActive;

        public int FrozenEntityCount => _frozenEntityCount;

        public void ApplyHomeostasisPressureTier(byte pressureTier)
        {
            byte clampedTier = (byte)math.min(pressureTier, 3);
            if (_homeostasisPressureTier == clampedTier)
                return;

            _homeostasisPressureTier = clampedTier;
            _forceImmediateImportanceRefresh = true;
        }

        public void InitializeRuntime()
        {
            if (!_originShiftListenerRegistered)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _originShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
            }

            GlobalRegistry.RegisterFoveatedSimulationDirector(this);
        }

        public bool TryGetEntityTier(int targetIndex, out FoveatedSimulationTier tier)
        {
            if ((uint)targetIndex >= (uint)_targetCount)
            {
                tier = FoveatedSimulationTier.Active;
                return false;
            }

            tier = _simTiers[targetIndex];
            return true;
        }

        public FoveatedSimulationTier ResolveTierForPosition(Vector3 runtimePosition)
        {
            ResolveScalabilityThresholds(out float activeDistance, out float frozenDistance);
            if (!TryResolveScoringCamera(out Vector3 cameraPosition, out Vector3 cameraForward, out _))
                return FoveatedSimulationTier.Active;

            return ResolveTierForPosition(runtimePosition, cameraPosition, cameraForward, activeDistance, frozenDistance);
        }

        public void LockTier0(uint entityHash, ushort entityId, float seconds)
        {
            float lockUntil = Time.time + math.max(seconds, Tier0CombatLockSeconds);
            for (int i = 0; i < _targetCount; i++)
            {
                bool hashMatch = entityHash != 0u && _entityHashes[i] == entityHash;
                bool idMatch = entityId != 0 && _entityIds[i] == entityId;
                if (!hashMatch && !idMatch)
                    continue;

                _tier0LockUntilTimes[i] = math.max(_tier0LockUntilTimes[i], lockUntil);
                _simTiers[i] = FoveatedSimulationTier.Active;
                _tickRates[i] = FoveatedTickRate.Center60Hz;
                _tickIntervals[i] = CenterTickIntervalSeconds;
                _tickAccumulators[i] = CenterTickIntervalSeconds;
                _forceImmediateImportanceRefresh = true;
            }
        }

        public void SetThermalFreezeDistanceOverride(bool active, float frozenDistanceMeters)
        {
            float sanitizedDistance = math.isfinite(frozenDistanceMeters)
                ? math.max(1f, frozenDistanceMeters)
                : DefaultFrozenDistanceMeters;

            if (_thermalFreezeOverrideActive == active &&
                math.abs(_thermalFrozenDistanceMeters - sanitizedDistance) <= 0.001f)
                return;

            _thermalFreezeOverrideActive = active;
            _thermalFrozenDistanceMeters = sanitizedDistance;
            _forceImmediateImportanceRefresh = true;
            _importanceAccumulator = ImportanceEvaluationIntervalSeconds;
        }

        public void RegisterTarget(IFoveatedSimulationTarget target)
        {
            if (target == null)
                return;

            if (target.FoveatedTargetIndex >= 0)
                return;

            if (_targetCount >= MaxTargets)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    $"[FoveatedSimulationManager] Target capacity ({MaxTargets}) exceeded.");
#endif
                return;
            }

            int index = _targetCount;
            _targetCount++;
            _targets[index] = target;
            _simulationTransforms[index] = target.SimulationTransform;
            _visualTransforms[index] = target.VisualTransform;
            _dopplerAudioSources[index] = target.DopplerAudioSource;
            _tickIntervals[index] = CenterTickIntervalSeconds;
            _tickRates[index] = FoveatedTickRate.Center60Hz;
            _simTiers[index] = FoveatedSimulationTier.Active;
            _entityHashes[index] = target.FoveatedEntityHash;
            _entityIds[index] = target.FoveatedEntityId;
            _tier0LockUntilTimes[index] = 0.0f;
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
            target.OnFoveatedTierResolved(FoveatedSimulationTier.Active, 0.0f, false);
            _forceImmediateImportanceRefresh = true;
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

            InvalidateDeferredRaycastOwner(target);

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
                _simTiers[removedIndex] = _simTiers[lastIndex];
                _entityHashes[removedIndex] = _entityHashes[lastIndex];
                _entityIds[removedIndex] = _entityIds[lastIndex];
                _tier0LockUntilTimes[removedIndex] = _tier0LockUntilTimes[lastIndex];
                _framesSinceTickRateChange[removedIndex] = _framesSinceTickRateChange[lastIndex];

                if (swappedTarget != null)
                    swappedTarget.FoveatedTargetIndex = removedIndex;
            }

            ClearSlot(lastIndex);
            _targetCount = lastIndex;
            target.FoveatedTargetIndex = -1;
            _forceImmediateImportanceRefresh = true;
            _visualTargetCacheDirty = true;
        }

        public void BeginDispatcherFrame(float frameDeltaTime)
        {
            TryCompleteFrameJobsInternal(true, forceComplete: false);
            EnsureNativeBuffersAllocated();
            ConsumeAupShiftSignals();
            ConsumeCameraSignals();
            _importanceAccumulator += math.max(frameDeltaTime, 0.0f);

            if (!_deferredRaycastScheduled && _deferredRaycastCommands.IsCreated)
                _deferredRaycastCommands.Clear();

            if (!TryResolveViewCamera(frameDeltaTime) && !_hasSignalCameraPose)
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

            if (_simTiers[index] == FoveatedSimulationTier.Frozen)
            {
                effectiveDeltaTime = 0.0f;
                return false;
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
            float velocityBlend = ApproximateOneMinusExpNeg(ResolveVelocitySmoothingSharpness(_tickRates[index]) * deltaTime);
            float3 smoothedVelocity = math.lerp(
                new float3(_smoothedVelocities[index].x, _smoothedVelocities[index].y, _smoothedVelocities[index].z),
                new float3(rawVelocity.x, rawVelocity.y, rawVelocity.z),
                velocityBlend);
            _smoothedVelocities[index] = new Vector3(smoothedVelocity.x, smoothedVelocity.y, smoothedVelocity.z);

            if (_deferredRaycastCommands.IsCreated &&
                _pendingDeferredRaycastCommands.IsCreated &&
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
                    TryEnqueueDeferredRaycastCommand(
                        target,
                        _deferredRaycastScratchCommands[commandIndex],
                        commandIndex);
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

        private static float ApproximateOneMinusExpNeg(float value)
        {
            float x = math.clamp(value, 0.0f, 3.0f);
            float expApprox = math.rcp(1.0f + x + (0.5f * x * x));
            return math.saturate(1.0f - expApprox);
        }

        public void ScheduleFrameJobs()
        {
            TryCompleteFrameJobsInternal(true, forceComplete: false);

            if (_visualTargetCacheDirty && !_interpolationScheduled)
                RebuildVisualTargetCache();

            if (_visualTargetCount > 0 && !_interpolationScheduled)
                ScheduleInterpolationJob();

            if (!_deferredRaycastScheduled)
            {
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
            }

            ApplyCombatDamageSignals();
            bool shouldRefreshImportance = _forceImmediateImportanceRefresh ||
                                           _importanceAccumulator >= ImportanceEvaluationIntervalSeconds;
            if (!_importanceScheduled && shouldRefreshImportance)
                ScheduleImportanceScoringJob();
        }

        public bool TryCompleteFrameJobs()
        {
            return TryCompleteFrameJobsInternal(true, forceComplete: false);
        }

        public void CompleteFrameJobs()
        {
            TryCompleteFrameJobsInternal(true, forceComplete: true);
        }

        public void SetVoxelTeardownBackpressure(bool active, int pendingChunkCount)
        {
            if (_voxelTeardownBackpressureActive == active &&
                _voxelTeardownBackpressurePendingCount == pendingChunkCount)
            {
                return;
            }

            _voxelTeardownBackpressureActive = active;
            _voxelTeardownBackpressurePendingCount = math.max(0, pendingChunkCount);
            HectonPlayerMovement movement = GlobalRegistry.Player != null
                ? GlobalRegistry.Player.PlayerMovement
                : null;
            if (movement == null)
                return;

            movement.SetRuntimeVoxelBackpressureSwimSpeedMultiplier(active ? VoxelTeardownBackpressureSwimSpeedMultiplier : 1f);
        }

        private bool TryCompleteFrameJobsInternal(bool includeDeferredRaycasts, bool forceComplete)
        {
            bool completedAll = true;
            if (_interpolationScheduled)
            {
                if (!TryCompleteJob(ref _interpolationHandle, "FoveatedSimulationManager.Interpolation", forceComplete))
                {
                    completedAll = false;
                }
                else
                {
                    _interpolationScheduled = false;
                }
            }

            if (includeDeferredRaycasts && _deferredRaycastScheduled)
            {
                if (!TryCompleteJob(ref _deferredRaycastHandle, "FoveatedSimulationManager.DeferredRaycasts", forceComplete))
                {
                    completedAll = false;
                }
                else
                {
                    int raycastCount = _deferredRaycastCommands.Length;
                    for (int i = 0; i < raycastCount; i++)
                    {
                        IFoveatedSimulationTarget owner = _deferredRaycastOwners[i];
                        if (IsActiveFoveatedTarget(owner))
                            owner.ConsumeDeferredRaycastHit(_deferredRaycastCommandIndices[i], _deferredRaycastResults[i]);

                        _deferredRaycastOwners[i] = null;
                        _deferredRaycastCommandIndices[i] = 0;
                    }

                    _deferredRaycastScheduled = false;
                }
            }

            if (_importanceScheduled)
            {
                if (!TryCompleteJob(ref _importanceHandle, "FoveatedSimulationManager.ImportanceScoring", forceComplete))
                {
                    completedAll = false;
                }
                else
                {
                    ApplyImportanceResults();
                    _importanceScheduled = false;
                }
            }

            return completedAll;
        }

        public void ResetRuntimeState()
        {
            if (_voxelTeardownBackpressureActive)
                SetVoxelTeardownBackpressure(false, 0);

            TryCompleteFrameJobsInternal(true, forceComplete: true);
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
            Array.Clear(_simTiers, 0, _simTiers.Length);
            Array.Clear(_entityHashes, 0, _entityHashes.Length);
            Array.Clear(_entityIds, 0, _entityIds.Length);
            Array.Clear(_tier0LockUntilTimes, 0, _tier0LockUntilTimes.Length);
            Array.Clear(_framesSinceTickRateChange, 0, _framesSinceTickRateChange.Length);
            Array.Clear(_visualTargetIndices, 0, _visualTargetIndices.Length);
            Array.Clear(_deferredRaycastOwners, 0, _deferredRaycastOwners.Length);
            Array.Clear(_deferredRaycastCommandIndices, 0, _deferredRaycastCommandIndices.Length);
            DrainDeferredRaycastQueueResidue();

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
            _lastDeferredRaycastScheduleFrame = -1;
            _voxelTeardownBackpressureActive = false;
            _voxelTeardownBackpressurePendingCount = 0;
            _forceImmediateImportanceRefresh = false;
            _hasSignalCameraPose = false;
            _blackBoxDumped = false;
            _frozenEntityCount = 0;
            _tier0Count = 0;
            _tier1Count = 0;
            _tier2Count = 0;
            _homeostasisPressureTier = 0;
            _telemetryCursor = 0;
            _importanceAccumulator = 0.0f;
            _activeDistanceMeters = DefaultActiveDistanceMeters;
            _frozenDistanceMeters = DefaultFrozenDistanceMeters;
            _signalCameraPosition = Vector3.zero;
            _signalCameraForward = Vector3.forward;
            _signalCameraUp = Vector3.up;
            _visualTransformArray = Array.Empty<Transform>();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= MinimumVelocityDelta)
                return;

            TryCompleteFrameJobsInternal(true, forceComplete: true);
            for (int i = 0; i < _targetCount; i++)
            {
                _visualFromPositions[i] -= shiftOffset;
                _visualToPositions[i] -= shiftOffset;
            }

            if (_listenerStateInitialized)
                _lastListenerPosition -= shiftOffset;

            _visualTargetCacheDirty = true;
            _forceImmediateImportanceRefresh = true;
            _importanceAccumulator = ImportanceEvaluationIntervalSeconds;
            if (_targetCount > 0 && !_importanceScheduled && TryResolveScoringCamera(out _, out _, out _))
            {
                ScheduleImportanceScoringJob();
                TryCompleteFrameJobsInternal(false, forceComplete: true);
            }
        }

        public void Dispose()
        {
            GlobalRegistry.UnregisterFoveatedSimulationDirector(this);
            if (_originShiftListenerRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftListenerRegistered = false;
            }

            ResetRuntimeState();
        }

        private static bool TryCompleteJob(ref JobHandle handle, string systemName, bool forceComplete)
        {
            if (!forceComplete)
                return DispatcherJobSwap.TryFinalizeCompleted(ref handle);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds > SlowJobCompleteWarningMilliseconds)
            {
                Debug.LogWarning(SlowJobCompleteWarningMessage);
            }
#else
            DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
#endif
            return true;
        }

        private void ScheduleImportanceScoringJob()
        {
            if (_targetCount <= 0 || !TryResolveScoringCamera(out Vector3 cameraPosition, out Vector3 cameraForward, out Vector3 cameraUp))
                return;

            EnsureNativeBuffersAllocated();
            ResolveScalabilityThresholds(out _activeDistanceMeters, out _frozenDistanceMeters);

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
                EntityAups = _jobEntityAups,
                ImportanceScores = _jobImportanceScores,
                TickRateCodes = _jobTickRateCodes,
                InsideFrustumFlags = _jobInsideFrustumFlags,
                EntitySimTiers = _jobEntitySimTiers,
                DistancesMeters = _jobDistancesMeters,
                CameraPosition = cameraPosition,
                CameraForward = cameraForward,
                CameraUp = cameraUp,
                ActiveDistanceMeters = _activeDistanceMeters,
                FrozenDistanceMeters = _frozenDistanceMeters,
                FrustumForwardDotThreshold = FrustumForwardDotThreshold,
            };

            _importanceHandle = scoringJob.Schedule(_targetCount, ImportanceScoreBatchSize);
            _importanceScheduled = true;
            _importanceAccumulator = 0.0f;
            _forceImmediateImportanceRefresh = false;
        }

        private void ApplyImportanceResults()
        {
            _tier0Count = 0;
            _tier1Count = 0;
            _tier2Count = 0;
            _frozenEntityCount = 0;
            TryResolveScoringCamera(out Vector3 cameraPosition, out Vector3 cameraForward, out _);
            float now = Time.time;
            for (int i = 0; i < _targetCount; i++)
            {
                IFoveatedSimulationTarget target = _targets[i];
                if (target == null)
                    continue;

                float importanceScore = _jobImportanceScores[i];
                FoveatedTickRate resolvedTickRate = (FoveatedTickRate)_jobTickRateCodes[i];
                FoveatedSimulationTier resolvedTier = (FoveatedSimulationTier)_jobEntitySimTiers[i];
                float distanceMeters = _jobDistancesMeters[i];
                bool tier0Locked = _tier0LockUntilTimes[i] > now;
                if (tier0Locked)
                {
                    resolvedTier = FoveatedSimulationTier.Active;
                    resolvedTickRate = FoveatedTickRate.Center60Hz;
                    importanceScore = 1.0f;
                }
                else if (_homeostasisPressureTier >= 3 && resolvedTickRate < FoveatedTickRate.Far10Hz)
                {
                    resolvedTickRate = FoveatedTickRate.Far10Hz;
                    importanceScore = math.min(importanceScore, 0.2f);
                    if (resolvedTier == FoveatedSimulationTier.Active)
                        resolvedTier = FoveatedSimulationTier.Peripheral;
                }

                FoveatedTickRate currentTickRate = _tickRates[i];
                bool freezeTransition = resolvedTier == FoveatedSimulationTier.Frozen ||
                                        _simTiers[i] == FoveatedSimulationTier.Frozen;
                if (!tier0Locked &&
                    !freezeTransition &&
                    math.abs((int)resolvedTickRate - (int)currentTickRate) == 1 &&
                    _framesSinceTickRateChange[i] < CadenceHysteresisFrames)
                {
                    resolvedTickRate = currentTickRate;
                    resolvedTier = _simTiers[i];
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

                _simTiers[i] = resolvedTier;
                _importanceScores[i] = importanceScore;
                _tickIntervals[i] = ResolveTickInterval(_tickRates[i]);
                _tickAccumulators[i] = math.min(_tickAccumulators[i], _tickIntervals[i]);
                target.OnFoveatedCadenceResolved(_tickRates[i], _tickIntervals[i], importanceScore, _jobInsideFrustumFlags[i] != 0);
                target.OnFoveatedTierResolved(resolvedTier, distanceMeters, tier0Locked);
                AccumulateTierCount(resolvedTier);
                if (resolvedTier == FoveatedSimulationTier.Frozen &&
                    distanceMeters > FrozenWrapDistanceMeters &&
                    target.TryHandleFoveatedFrozenWrap(cameraPosition, cameraForward, distanceMeters))
                {
                    _forceImmediateImportanceRefresh = true;
                }
            }

            WriteTelemetryFrame(cameraPosition, cameraForward);
        }

        private void AccumulateTierCount(FoveatedSimulationTier tier)
        {
            switch (tier)
            {
                case FoveatedSimulationTier.Active:
                    _tier0Count++;
                    return;
                case FoveatedSimulationTier.Peripheral:
                    _tier1Count++;
                    return;
                default:
                    _tier2Count++;
                    _frozenEntityCount++;
                    return;
            }
        }

        private void ApplyCombatDamageSignals()
        {
            ReadOnlySpan<CombatDamageSignal> damageSignals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            for (int signalIndex = 0; signalIndex < damageSignals.Length; signalIndex++)
            {
                CombatDamageSignal signal = damageSignals[signalIndex];
                if (signal.TargetHash == 0u && signal.TargetId == 0)
                    continue;

                LockTier0(signal.TargetHash, signal.TargetId, Tier0CombatLockSeconds);
            }
        }

        private void ConsumeCameraSignals()
        {
            ReadOnlySpan<CameraPositionSignal> positionSignals = SignalBus<CameraPositionSignal>.GetFrameSnapshot();
            for (int i = 0; i < positionSignals.Length; i++)
            {
                CameraPositionSignal signal = positionSignals[i];
                if (!IsFinite(signal.Position))
                    continue;

                _signalCameraPosition = ToVector3(signal.Position);
                if (IsFinite(signal.Forward) && math.lengthsq(signal.Forward) > MinimumDirectionLength)
                    _signalCameraForward = ToVector3(math.normalizesafe(signal.Forward, new float3(0f, 0f, 1f)));
                _hasSignalCameraPose = true;
            }

            ReadOnlySpan<CameraFrustumSignal> frustumSignals = SignalBus<CameraFrustumSignal>.GetFrameSnapshot();
            for (int i = 0; i < frustumSignals.Length; i++)
            {
                CameraFrustumSignal signal = frustumSignals[i];
                if (!IsFinite(signal.Position))
                    continue;

                _signalCameraPosition = ToVector3(signal.Position);
                if (IsFinite(signal.Forward) && math.lengthsq(signal.Forward) > MinimumDirectionLength)
                    _signalCameraForward = ToVector3(math.normalizesafe(signal.Forward, new float3(0f, 0f, 1f)));
                if (IsFinite(signal.Up) && math.lengthsq(signal.Up) > MinimumDirectionLength)
                    _signalCameraUp = ToVector3(math.normalizesafe(signal.Up, new float3(0f, 1f, 0f)));
                _hasSignalCameraPose = true;
            }
        }

        private void ConsumeAupShiftSignals()
        {
            ReadOnlySpan<AupShiftSignal> shiftSignals = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shiftSignals.Length; i++)
            {
                AupShiftSignal signal = shiftSignals[i];
                if (!IsFinite(signal.ShiftMeters))
                    continue;

                _forceImmediateImportanceRefresh = true;
                _importanceAccumulator = ImportanceEvaluationIntervalSeconds;
            }
        }

        private bool TryResolveScoringCamera(out Vector3 cameraPosition, out Vector3 cameraForward, out Vector3 cameraUp)
        {
            if (_hasSignalCameraPose)
            {
                cameraPosition = _signalCameraPosition;
                cameraForward = _signalCameraForward.sqrMagnitude > MinimumDirectionLength ? _signalCameraForward : Vector3.forward;
                cameraUp = _signalCameraUp.sqrMagnitude > MinimumDirectionLength ? _signalCameraUp : Vector3.up;
                return true;
            }

            if (_cameraTransform == null)
            {
                cameraPosition = Vector3.zero;
                cameraForward = Vector3.forward;
                cameraUp = Vector3.up;
                return false;
            }

            cameraPosition = _cameraTransform.position;
            cameraForward = _cameraTransform.forward;
            cameraUp = _cameraTransform.up;
            return true;
        }

        private void ResolveScalabilityThresholds(out float activeDistance, out float frozenDistance)
        {
            if (_homeostasisPressureTier >= 3)
            {
                activeDistance = LowActiveDistanceMeters * 0.5f;
                frozenDistance = LowFrozenDistanceMeters * 0.5f;
                return;
            }

            HectonQualityTier qualityTier = GlobalRegistry.ScalabilityTier;
            if (qualityTier == HectonQualityTier.Low || qualityTier == HectonQualityTier.Mx350)
            {
                activeDistance = LowActiveDistanceMeters;
                frozenDistance = LowFrozenDistanceMeters;
            }
            else
            {
                activeDistance = DefaultActiveDistanceMeters;
                frozenDistance = DefaultFrozenDistanceMeters;
            }

            if (_thermalFreezeOverrideActive)
                frozenDistance = math.max(activeDistance, math.min(frozenDistance, _thermalFrozenDistanceMeters));
        }

        private static FoveatedSimulationTier ResolveTierForPosition(
            Vector3 runtimePosition,
            Vector3 cameraPosition,
            Vector3 cameraForward,
            float activeDistance,
            float frozenDistance)
        {
            Vector3 toTarget = runtimePosition - cameraPosition;
            float distanceSq = toTarget.sqrMagnitude;
            if (distanceSq > frozenDistance * frozenDistance)
                return FoveatedSimulationTier.Frozen;

            if (distanceSq <= MinimumDirectionLength)
                return FoveatedSimulationTier.Active;

            float inverseDistance = math.rsqrt(math.max(distanceSq, MinimumDirectionLength));
            Vector3 direction = toTarget * inverseDistance;
            float forwardLengthSq = cameraForward.sqrMagnitude;
            Vector3 forward = forwardLengthSq > MinimumDirectionLength
                ? cameraForward * math.rsqrt(forwardLengthSq)
                : Vector3.forward;
            bool insideFrustum = Vector3.Dot(direction, forward) >= FrustumForwardDotThreshold;
            if (!insideFrustum || distanceSq >= activeDistance * activeDistance)
                return FoveatedSimulationTier.Peripheral;

            return FoveatedSimulationTier.Active;
        }

        private void WriteTelemetryFrame(Vector3 cameraPosition, Vector3 cameraForward)
        {
            if (!_telemetryRing.IsCreated)
                return;

            if (!IsFinite(cameraPosition) || !IsFinite(cameraForward))
                DumpTelemetryBlackBoxOnce();

            int cursor = _telemetryCursor;
            _telemetryRing[cursor] = new FoveatedSimulationTelemetryEntry
            {
                Frame = Time.frameCount,
                TargetCount = _targetCount,
                FrozenEntityCount = _frozenEntityCount,
                Tier0Count = _tier0Count,
                Tier1Count = _tier1Count,
                Tier2Count = _tier2Count,
                CameraPosition = cameraPosition,
                CameraForward = cameraForward,
                Flags = _forceImmediateImportanceRefresh ? 1u : 0u,
                StateHash = ComputeStateHash()
            };

            _telemetryCursor = cursor + 1 >= TelemetryCapacity ? 0 : cursor + 1;
        }

        private uint ComputeStateHash()
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)_targetCount) * 16777619u;
            hash = (hash ^ (uint)_frozenEntityCount) * 16777619u;
            hash = (hash ^ (uint)_tier0Count) * 16777619u;
            hash = (hash ^ (uint)_tier1Count) * 16777619u;
            hash = (hash ^ (uint)_tier2Count) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private void DumpTelemetryBlackBoxOnce()
        {
            if (_blackBoxDumped || !_telemetryRing.IsCreated)
                return;

            _blackBoxDumped = true;
            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string directory = Path.Combine(root, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, BlackBoxDumpFileName);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(TelemetryMagic);
                    writer.Write(TelemetryCapacity);
                    writer.Write(_telemetryCursor);
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        FoveatedSimulationTelemetryEntry entry = _telemetryRing[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.TargetCount);
                        writer.Write(entry.FrozenEntityCount);
                        writer.Write(entry.Tier0Count);
                        writer.Write(entry.Tier1Count);
                        writer.Write(entry.Tier2Count);
                        writer.Write(entry.CameraPosition.x);
                        writer.Write(entry.CameraPosition.y);
                        writer.Write(entry.CameraPosition.z);
                        writer.Write(entry.CameraForward.x);
                        writer.Write(entry.CameraForward.y);
                        writer.Write(entry.CameraForward.z);
                        writer.Write(entry.Flags);
                        writer.Write(entry.StateHash);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
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

            if (!_jobEntityAups.IsCreated)
            {
                _jobEntityAups = new NativeArray<float3>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeArray<float3>[512] - entity AUP/runtime positions for foveated tiering - owner: FoveatedSimulationManager
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

            if (!_jobEntitySimTiers.IsCreated)
            {
                _jobEntitySimTiers = new NativeArray<byte>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeArray<byte>[512] - Burst foveated tier output buffer - owner: FoveatedSimulationManager
            }

            if (!_jobDistancesMeters.IsCreated)
            {
                _jobDistancesMeters = new NativeArray<float>(MaxTargets, Allocator.Persistent); // COLD ALLOC: NativeArray<float>[512] - distance output buffer for wrap policy - owner: FoveatedSimulationManager
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
                _pendingDeferredRaycastCommands = new NativeArray<RaycastCommand>(MaxDeferredRaycastCommands, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<RaycastCommand>[256] — fixed ring buffer for next-frame deferred fauna sight-line requests — owner: FoveatedSimulationManager
            }

            if (!_pendingDeferredRaycastCommandIndices.IsCreated)
            {
                _pendingDeferredRaycastCommandIndices = new NativeArray<int>(MaxDeferredRaycastCommands, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[256] — fixed ring buffer for deferred fauna sight-line command slot indices — owner: FoveatedSimulationManager
            }

            if (!_deferredRaycastCommands.IsCreated)
            {
                _deferredRaycastCommands = new NativeList<RaycastCommand>(MaxDeferredRaycastCommands, Allocator.Persistent); // COLD ALLOC: NativeList<RaycastCommand>[256] — deferred throttled-entity physics commands — owner: FoveatedSimulationManager
            }

            if (!_deferredRaycastResults.IsCreated)
            {
                _deferredRaycastResults = new NativeArray<RaycastHit>(MaxDeferredRaycastCommands, Allocator.Persistent); // COLD ALLOC: NativeArray<RaycastHit>[256] — deferred throttled-entity raycast hits — owner: FoveatedSimulationManager
            }

            if (!_telemetryRing.IsCreated)
            {
                _telemetryRing = new NativeArray<FoveatedSimulationTelemetryEntry>(TelemetryCapacity, Allocator.Persistent); // COLD ALLOC: NativeArray<FoveatedSimulationTelemetryEntry>[300] - fixed black-box tier telemetry - owner: FoveatedSimulationManager
            }

            if (!_nativeMemorySentinelRegistered)
                RegisterNativeMemorySentinel();

            if (!_nativeMemoryBudgetRegistered)
                RegisterNativeMemoryBudget();
        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_jobScorePositions, nameof(FoveatedSimulationManager), nameof(_jobScorePositions), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_jobEntityAups, nameof(FoveatedSimulationManager), nameof(_jobEntityAups), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_jobImportanceScores, nameof(FoveatedSimulationManager), nameof(_jobImportanceScores), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_jobTickRateCodes, nameof(FoveatedSimulationManager), nameof(_jobTickRateCodes), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_jobInsideFrustumFlags, nameof(FoveatedSimulationManager), nameof(_jobInsideFrustumFlags), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_jobEntitySimTiers, nameof(FoveatedSimulationManager), nameof(_jobEntitySimTiers), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_jobDistancesMeters, nameof(FoveatedSimulationManager), nameof(_jobDistancesMeters), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_jobFromPositions, nameof(FoveatedSimulationManager), nameof(_jobFromPositions), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_jobToPositions, nameof(FoveatedSimulationManager), nameof(_jobToPositions), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_jobAlphas, nameof(FoveatedSimulationManager), nameof(_jobAlphas), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_pendingDeferredRaycastCommands, nameof(FoveatedSimulationManager), nameof(_pendingDeferredRaycastCommands), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_pendingDeferredRaycastCommandIndices, nameof(FoveatedSimulationManager), nameof(_pendingDeferredRaycastCommandIndices), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeList(_deferredRaycastCommands, nameof(FoveatedSimulationManager), nameof(_deferredRaycastCommands), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_deferredRaycastResults, nameof(FoveatedSimulationManager), nameof(_deferredRaycastResults), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_telemetryRing, nameof(FoveatedSimulationManager), nameof(_telemetryRing), NativeAllocationLifetime.Session);
            _nativeMemorySentinelRegistered = true;
        }

        private void DisposeNativeBuffers(JobHandle dependency)
        {
            MemoryBudgetTracker.Unregister(MemoryBudgetOwnerName);
            JobHandle disposeHandle = dependency;
            DisposeNativeArray(ref _jobScorePositions, ref disposeHandle);
            DisposeNativeArray(ref _jobEntityAups, ref disposeHandle);
            DisposeNativeArray(ref _jobImportanceScores, ref disposeHandle);
            DisposeNativeArray(ref _jobTickRateCodes, ref disposeHandle);
            DisposeNativeArray(ref _jobInsideFrustumFlags, ref disposeHandle);
            DisposeNativeArray(ref _jobEntitySimTiers, ref disposeHandle);
            DisposeNativeArray(ref _jobDistancesMeters, ref disposeHandle);
            DisposeNativeArray(ref _jobFromPositions, ref disposeHandle);
            DisposeNativeArray(ref _jobToPositions, ref disposeHandle);
            DisposeNativeArray(ref _jobAlphas, ref disposeHandle);
            NativeMemorySentinel.UnregisterNativeList(nameof(FoveatedSimulationManager), nameof(_deferredRaycastCommands));
            DisposeNativeArray(ref _pendingDeferredRaycastCommands, ref disposeHandle);
            DisposeNativeArray(ref _pendingDeferredRaycastCommandIndices, ref disposeHandle);
            DisposeNativeList(ref _deferredRaycastCommands, ref disposeHandle);
            DisposeNativeArray(ref _deferredRaycastResults, ref disposeHandle);
            DisposeNativeArray(ref _telemetryRing, ref disposeHandle);
            DispatcherJobSwap.TryComplete(ref disposeHandle, forceComplete: true);

            _jobScorePositions = default;
            _jobEntityAups = default;
            _jobImportanceScores = default;
            _jobTickRateCodes = default;
            _jobInsideFrustumFlags = default;
            _jobEntitySimTiers = default;
            _jobDistancesMeters = default;
            _jobFromPositions = default;
            _jobToPositions = default;
            _jobAlphas = default;
            _pendingDeferredRaycastCommands = default;
            _pendingDeferredRaycastCommandIndices = default;
            _deferredRaycastCommands = default;
            _deferredRaycastResults = default;
            _telemetryRing = default;
            DrainDeferredRaycastQueueResidue();
            _nativeMemorySentinelRegistered = false;
            _nativeMemoryBudgetRegistered = false;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, ref JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            dependency = array.Dispose(dependency);
            array = default;
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, ref JobHandle dependency) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            dependency = list.Dispose(dependency);
            list = default;
        }

        private void DrainDeferredRaycastQueues()
        {
            if (!_deferredRaycastCommands.IsCreated)
                return;

            _deferredRaycastCommands.Clear();

            int commandIndex = 0;
            while (commandIndex < MaxDeferredRaycastCommandsPerFrame &&
                   TryDequeueDeferredRaycastCommand(
                       out RaycastCommand command,
                       out IFoveatedSimulationTarget owner,
                       out int ownerCommandIndex))
            {
                if (!IsActiveFoveatedTarget(owner))
                    continue;

                _deferredRaycastCommands.AddNoResize(command);
                _deferredRaycastOwners[commandIndex] = owner;
                _deferredRaycastCommandIndices[commandIndex] = ownerCommandIndex;
                commandIndex++;
            }
        }

        private bool TryDequeueDeferredRaycastCommand(
            out RaycastCommand command,
            out IFoveatedSimulationTarget owner,
            out int ownerCommandIndex)
        {
            command = default;
            owner = null;
            ownerCommandIndex = 0;

            if (!_pendingDeferredRaycastCommands.IsCreated ||
                !_pendingDeferredRaycastCommandIndices.IsCreated ||
                _queuedDeferredRaycastCount <= 0)
            {
                return false;
            }

            int head = _pendingDeferredRaycastHead;
            command = _pendingDeferredRaycastCommands[head];
            owner = _pendingDeferredRaycastOwners[head];
            ownerCommandIndex = _pendingDeferredRaycastCommandIndices[head];
            _pendingDeferredRaycastOwners[head] = null;
            _pendingDeferredRaycastCommandIndices[head] = 0;
            _pendingDeferredRaycastHead = IncrementDeferredRaycastRingIndex(head);
            _queuedDeferredRaycastCount--;
            return true;
        }

        private bool TryEnqueueDeferredRaycastCommand(
            IFoveatedSimulationTarget owner,
            in RaycastCommand command,
            int ownerCommandIndex)
        {
            if (!_pendingDeferredRaycastCommands.IsCreated ||
                !_pendingDeferredRaycastCommandIndices.IsCreated ||
                _queuedDeferredRaycastCount >= MaxDeferredRaycastCommands)
                return false;

            int tail = _pendingDeferredRaycastTail;
            _pendingDeferredRaycastCommands[tail] = command;
            _pendingDeferredRaycastOwners[tail] = owner;
            _pendingDeferredRaycastCommandIndices[tail] = ownerCommandIndex;
            _pendingDeferredRaycastTail = IncrementDeferredRaycastRingIndex(tail);
            _queuedDeferredRaycastCount++;
            return true;
        }

        private void DrainDeferredRaycastQueueResidue()
        {
            Array.Clear(_pendingDeferredRaycastOwners, 0, _pendingDeferredRaycastOwners.Length);
            _pendingDeferredRaycastHead = 0;
            _pendingDeferredRaycastTail = 0;
            _queuedDeferredRaycastCount = 0;
        }

        private static int IncrementDeferredRaycastRingIndex(int index)
        {
            index++;
            return index >= MaxDeferredRaycastCommands ? 0 : index;
        }

        private bool IsActiveFoveatedTarget(IFoveatedSimulationTarget target)
        {
            if (target == null)
                return false;

            int index = target.FoveatedTargetIndex;
            return index >= 0 && index < _targetCount && ReferenceEquals(_targets[index], target);
        }

        private void InvalidateDeferredRaycastOwner(IFoveatedSimulationTarget target)
        {
            for (int i = 0; i < _pendingDeferredRaycastOwners.Length; i++)
            {
                if (ReferenceEquals(_pendingDeferredRaycastOwners[i], target))
                    _pendingDeferredRaycastOwners[i] = null;
            }

            for (int i = 0; i < _deferredRaycastOwners.Length; i++)
            {
                if (ReferenceEquals(_deferredRaycastOwners[i], target))
                    _deferredRaycastOwners[i] = null;
            }
        }

        private void RegisterNativeMemoryBudget()
        {
            long totalBytes = GetNativeArrayBytes(_jobScorePositions) +
                              GetNativeArrayBytes(_jobEntityAups) +
                              GetNativeArrayBytes(_jobImportanceScores) +
                              GetNativeArrayBytes(_jobTickRateCodes) +
                              GetNativeArrayBytes(_jobInsideFrustumFlags) +
                              GetNativeArrayBytes(_jobEntitySimTiers) +
                              GetNativeArrayBytes(_jobDistancesMeters) +
                              GetNativeArrayBytes(_jobFromPositions) +
                              GetNativeArrayBytes(_jobToPositions) +
                              GetNativeArrayBytes(_jobAlphas) +
                              GetNativeArrayBytes(_pendingDeferredRaycastCommands) +
                              GetNativeArrayBytes(_pendingDeferredRaycastCommandIndices) +
                              GetNativeArrayBytes(_deferredRaycastResults) +
                              GetNativeArrayBytes(_telemetryRing) +
                              GetNativeListBytes(_deferredRaycastCommands);
            MemoryBudgetTracker.Register(MemoryBudgetOwnerName, totalBytes, PersistentNativeBudgetBytes);
            _nativeMemoryBudgetRegistered = true;
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
            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (!playerTransform.TryGetComponent(out _viewCamera))
                {
                    IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
                    _viewCamera = playerContext != null ? playerContext.PlayerCamera : null;
                }
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

            GameObject playerObject = GameBootstrapper.CurrentPlayerObject;
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
            _simTiers[index] = FoveatedSimulationTier.Active;
            _entityHashes[index] = 0u;
            _entityIds[index] = 0;
            _tier0LockUntilTimes[index] = 0.0f;
            _framesSinceTickRateChange[index] = 0;
        }
    }
}


