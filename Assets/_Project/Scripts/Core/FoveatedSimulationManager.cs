using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
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
        bool TryAdvanceTick(IUpdatable item, float frameDeltaTime, out float effectiveDeltaTime);
        void NotifyTickCompleted(IUpdatable item);
        void ScheduleFrameJobs();
        void VisualSyncTick();
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
    /// audio/surface-probe side effects on an allocation-free path.
    /// </summary>
    internal sealed class FoveatedSimulationManager : IFoveatedDispatcher, IFoveatedSimulationDirector, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const double SlowJobCompleteWarningMilliseconds = 100.0;
        private const string SlowJobCompleteWarningMessage = "[SystemDispatcher] JobHandle.Complete slow in foveated simulation swap window.";

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ImportanceScoringJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float3> Positions;
            [NoAlias] public NativeArray<float3> EntityAups;
            [NoAlias] public NativeArray<float> ImportanceScores;
            [NoAlias] public NativeArray<byte> TickRateCodes;
            [NoAlias] public NativeArray<byte> InsideFrustumFlags;
            [NoAlias] public NativeArray<byte> EntitySimTiers;
            [NoAlias] public NativeArray<float> DistancesMeters;
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

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FoveatedSimulationTelemetryEntry
        {
            [FieldOffset(0)] public int Frame;
            [FieldOffset(4)] public int TargetCount;
            [FieldOffset(8)] public int FrozenEntityCount;
            [FieldOffset(12)] public int Tier0Count;
            [FieldOffset(16)] public int Tier1Count;
            [FieldOffset(20)] public int Tier2Count;
            [FieldOffset(24)] public float3 CameraPosition;
            [FieldOffset(36)] public float3 CameraForward;
            [FieldOffset(48)] public uint Flags;
            [FieldOffset(52)] public uint StateHash;
            [FieldOffset(56)] public uint Reserved0;
            [FieldOffset(60)] public uint Reserved1;
        }

        private struct FoveatedNativeBuffers
        {
            public NativeArray<float3> ScorePositions;
            public NativeArray<float3> EntityAups;
            public NativeArray<float> ImportanceScores;
            public NativeArray<byte> TickRateCodes;
            public NativeArray<byte> InsideFrustumFlags;
            public NativeArray<byte> EntitySimTiers;
            public NativeArray<float> DistancesMeters;
            public NativeArray<float3> FromPositions;
            public NativeArray<float3> ToPositions;
            public NativeArray<float> Alphas;
            public NativeArray<FoveatedSimulationTelemetryEntry> TelemetryRing;
        }

        private const int ImportanceScoreBatchSize = 32;
        private const int MaxTargets = 512;
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
        private const float RearOneHertzDistanceMeters = 100.0f;
        private const float EcosystemOnlyCullDistanceMeters = 300.0f;
        private const uint KccVelocityListenerMaxAgeFrames = 12u;
        private const float DefaultActiveDistanceMeters = 100.0f;
        private const float DefaultFrozenDistanceMeters = 300.0f;
        private const float SurvivalActiveDistanceMeters = 50.0f;
        private const float SurvivalFrozenDistanceMeters = 150.0f;
        private const float CriticalSurvivalActiveDistanceMeters = 25.0f;
        private const float CriticalSurvivalFrozenDistanceMeters = 75.0f;
        private const float FrozenWrapDistanceMeters = 600.0f;
        private const float FrozenWrapForwardDistanceMeters = 200.0f;
        private const float ImportanceEvaluationIntervalSeconds = 0.1f;
        private const float FrustumForwardDotThreshold = 0.34202015f;
        private const float Tier0CombatLockSeconds = 10.0f;
        private const float FoveatedClockMaxSeconds = 16777215f;
        private const int TelemetryCapacity = 300;
        private const uint TelemetryMagic = 0x46384C44u;
        private const float SoundSpeedWaterMetersPerSecond = HectonPhysicsContract.SoundSpeedWaterMetersPerSecondConst;
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
        private const SystemID VaultOwnerSystemId = SystemID.SystemDispatcher;
        private const BufferID FoveatedScorePositionsBufferId = (BufferID)73220;
        private const BufferID FoveatedEntityAupsBufferId = (BufferID)73221;
        private const BufferID FoveatedImportanceScoresBufferId = (BufferID)73222;
        private const BufferID FoveatedTickRateCodesBufferId = (BufferID)73223;
        private const BufferID FoveatedInsideFrustumFlagsBufferId = (BufferID)73224;
        private const BufferID FoveatedEntitySimTiersBufferId = (BufferID)73225;
        private const BufferID FoveatedDistancesMetersBufferId = (BufferID)73226;
        private const BufferID FoveatedFromPositionsBufferId = (BufferID)73227;
        private const BufferID FoveatedToPositionsBufferId = (BufferID)73228;
        private const BufferID FoveatedAlphasBufferId = (BufferID)73229;
        private const BufferID FoveatedTelemetryRingBufferId = (BufferID)73234;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong FoveatedMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 63);
        }

        private static readonly ulong ImportanceJobMutationGuardMask =
            FoveatedMutationGuardBit(FoveatedScorePositionsBufferId) |
            FoveatedMutationGuardBit(FoveatedEntityAupsBufferId) |
            FoveatedMutationGuardBit(FoveatedImportanceScoresBufferId) |
            FoveatedMutationGuardBit(FoveatedTickRateCodesBufferId) |
            FoveatedMutationGuardBit(FoveatedInsideFrustumFlagsBufferId) |
            FoveatedMutationGuardBit(FoveatedEntitySimTiersBufferId) |
            FoveatedMutationGuardBit(FoveatedDistancesMetersBufferId);

        // COLD ALLOC: object[512] — dispatcher-owned opt-in simulation target slots, object-backed to avoid interface arrays — owner: FoveatedSimulationManager
        private readonly object[] _targets = new object[MaxTargets];
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
        // COLD ALLOC: Transform[512] — compact interpolation cache rebuilt in VISUAL_SYNC without managed allocation — owner: FoveatedSimulationManager
        private readonly Transform[] _visualTransformArray = new Transform[MaxTargets];
        private TransformAccessArray _visualTransformAccessArray;
        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerContext;
        private VaultGenerationHandle<float3> _jobScorePositionsHandle;
        private VaultGenerationHandle<float3> _jobEntityAupsHandle;
        private VaultGenerationHandle<float> _jobImportanceScoresHandle;
        private VaultGenerationHandle<byte> _jobTickRateCodesHandle;
        private VaultGenerationHandle<byte> _jobInsideFrustumFlagsHandle;
        private VaultGenerationHandle<byte> _jobEntitySimTiersHandle;
        private VaultGenerationHandle<float> _jobDistancesMetersHandle;
        private VaultGenerationHandle<float3> _jobFromPositionsHandle;
        private VaultGenerationHandle<float3> _jobToPositionsHandle;
        private VaultGenerationHandle<float> _jobAlphasHandle;
        private VaultGenerationHandle<FoveatedSimulationTelemetryEntry> _telemetryRingHandle;
        private JobHandle _importanceHandle;
        private JobHandle _interpolationHandle;

        private Camera _viewCamera;
        private Transform _cameraTransform;
        private Transform _listenerTransform;
        private Vector3 _listenerVelocity;
        private Vector3 _lastListenerPosition;

        private int _targetCount;
        private int _visualTargetCount;
        private float _cameraResolveRetryTimer;
        private float _listenerResolveRetryTimer;
        private bool _visualTargetCacheDirty = true;
        private bool _importanceScheduled;
        private bool _interpolationScheduled;
        private bool _listenerStateInitialized;
        private bool _originShiftListenerRegistered;
        private bool _nativeMemoryBudgetRegistered;
        private bool _registeredHotSwapListener;
        private bool _voxelTeardownBackpressureActive;
        private bool _importanceJobBuffersLocked;
        private IDataVault _importanceJobGuardVault;
        private bool _forceImmediateImportanceRefresh;
        private bool _hasSignalCameraPose;
        private bool _blackBoxDumped;
        private int _voxelTeardownBackpressurePendingCount;
        private int _frozenEntityCount;
        private int _tier0Count;
        private int _tier1Count;
        private int _tier2Count;
        private byte _homeostasisPressureTier;
        private int _telemetryCursor;
        private float _foveatedClockSeconds;
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
            TryRegisterHotSwapListener();
            RebindDataVaultForOwnerRoute(_dataVault, GlobalRegistry.DataVault);
            _playerContext = GlobalRegistry.Player;

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
            float lockUntil = ResolveFoveatedClockSeconds() + math.max(seconds, Tier0CombatLockSeconds);
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
                Hecton8.Core.H8Debug.LogError(
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

            target.OnFoveatedCadenceResolved(FoveatedTickRate.Center60Hz, CenterTickIntervalSeconds, 1.0f, true);
            target.OnFoveatedTierResolved(FoveatedSimulationTier.Active, 0.0f, false);
            _forceImmediateImportanceRefresh = true;
            _visualTargetCacheDirty = true;
            OpenOrAcquireNativeBuffersForOwnerRoute();
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
                IFoveatedSimulationTarget swappedTarget = GetTargetAt(lastIndex);
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
            TryCompleteFrameJobsInternal(forceComplete: false);
            AdvanceFoveatedClock(frameDeltaTime);
            if (!TryResolveNativeBuffers(out _))
                return;

            ConsumeAupShiftSignals();
            ConsumeCameraSignals();
            _importanceAccumulator += math.max(frameDeltaTime, 0.0f);

            if (!RefreshViewCameraBinding(frameDeltaTime) && !_hasSignalCameraPose)
                return;

            RefreshListenerBinding(frameDeltaTime);
            UpdateListenerVelocity(frameDeltaTime);
        }

        private void AdvanceFoveatedClock(float frameDeltaTime)
        {
            if (!math.isfinite(frameDeltaTime) || frameDeltaTime <= 0f)
                return;

            _foveatedClockSeconds = math.min(FoveatedClockMaxSeconds, _foveatedClockSeconds + frameDeltaTime);
        }

        private float ResolveFoveatedClockSeconds()
        {
            return _foveatedClockSeconds;
        }

        public bool TryAdvanceTick(IUpdatable item, float frameDeltaTime, out float effectiveDeltaTime)
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
            TryCompleteFrameJobsInternal(forceComplete: false);

            ApplyCombatDamageSignals();
            bool shouldRefreshImportance = _forceImmediateImportanceRefresh ||
                                           _importanceAccumulator >= ImportanceEvaluationIntervalSeconds;
            if (!_importanceScheduled && shouldRefreshImportance)
                ScheduleImportanceScoringJob();
        }

        public void VisualSyncTick()
        {
            if (_visualTargetCacheDirty)
                RebuildVisualTargetCache();

            ApplyVisualInterpolationVisualSync();
            UpdateDopplerProtection();
        }

        public bool TryCompleteFrameJobs()
        {
            return TryCompleteFrameJobsInternal(forceComplete: false);
        }

        public void CompleteFrameJobs()
        {
            ForceCompleteFrameJobsInPostSimulationWindow();
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
            IPlayerRuntimeContext playerContext = _playerContext;
            HectonPlayerMovement movement = playerContext != null
                ? playerContext.PlayerMovement
                : null;
            if (movement == null)
                return;

            movement.SetRuntimeVoxelBackpressureSwimSpeedMultiplier(active ? VoxelTeardownBackpressureSwimSpeedMultiplier : 1f);
        }

        private bool TryCompleteFrameJobsInternal(bool forceComplete)
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

            if (_importanceScheduled)
            {
                if (!TryCompleteJob(ref _importanceHandle, "FoveatedSimulationManager.ImportanceScoring", forceComplete))
                {
                    completedAll = false;
                }
                else
                {
                    try
                    {
                        ApplyImportanceResults();
                    }
                    finally
                    {
                        ReleaseImportanceJobBufferLocks();
                        _importanceScheduled = false;
                    }
                }
            }

            return completedAll;
        }

        private bool ForceCompleteFrameJobsInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return TryCompleteFrameJobsInternal(forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        public void ResetRuntimeState()
        {
            if (_voxelTeardownBackpressureActive)
                SetVoxelTeardownBackpressure(false, 0);

            ForceCompleteFrameJobsInPostSimulationWindow();
            DisposeVisualTransformAccessArray();
            DisposeNativeBuffers(JobHandle.CombineDependencies(_importanceHandle, _interpolationHandle));
            _importanceHandle = default;
            _interpolationHandle = default;

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
            _viewCamera = null;
            _cameraTransform = null;
            _listenerTransform = null;
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
            _foveatedClockSeconds = 0.0f;
            _importanceAccumulator = 0.0f;
            _activeDistanceMeters = DefaultActiveDistanceMeters;
            _frozenDistanceMeters = DefaultFrozenDistanceMeters;
            _signalCameraPosition = Vector3.zero;
            _signalCameraForward = Vector3.forward;
            _signalCameraUp = Vector3.up;
            Array.Clear(_visualTransformArray, 0, _visualTransformArray.Length);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= MinimumVelocityDelta)
                return;

            ForceCompleteFrameJobsInPostSimulationWindow();
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
                ForceCompleteFrameJobsInPostSimulationWindow();
            }
        }

        public void Dispose()
        {
            GlobalRegistry.UnregisterFoveatedSimulationDirector(this);
            TryUnregisterHotSwapListener();
            if (_originShiftListenerRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftListenerRegistered = false;
            }

            ResetRuntimeState();
            _dataVault = null;
            _playerContext = null;
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindDataVaultForOwnerRoute(previousService as IDataVault, currentService as IDataVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _playerContext = currentService as IPlayerRuntimeContext;
        }

        private static bool TryCompleteJob(ref JobHandle handle, string systemName, bool forceComplete)
        {
            if (!forceComplete)
                return DispatcherJobFence.TryFinalizeCompleted(ref handle);

            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
                double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (elapsedMilliseconds > SlowJobCompleteWarningMilliseconds)
                {
                    Hecton8.Core.H8Debug.LogWarning(SlowJobCompleteWarningMessage);
                }
#else
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
#endif
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
            return true;
        }

        private void ScheduleImportanceScoringJob()
        {
            if (_targetCount <= 0 || !TryResolveScoringCamera(out Vector3 cameraPosition, out Vector3 cameraForward, out Vector3 cameraUp))
                return;

            if (!TryWriteScorePositionsForImportanceJob())
                return;

            if (!TryPinImportanceJobBuffers())
                return;

            if (!TryResolveNativeBuffers(out FoveatedNativeBuffers buffers))
            {
                ReleaseImportanceJobBufferLocks();
                return;
            }

            ResolveScalabilityThresholds(out _activeDistanceMeters, out _frozenDistanceMeters);

            ImportanceScoringJob scoringJob = new ImportanceScoringJob
            {
                Positions = buffers.ScorePositions,
                EntityAups = buffers.EntityAups,
                ImportanceScores = buffers.ImportanceScores,
                TickRateCodes = buffers.TickRateCodes,
                InsideFrustumFlags = buffers.InsideFrustumFlags,
                EntitySimTiers = buffers.EntitySimTiers,
                DistancesMeters = buffers.DistancesMeters,
                CameraPosition = cameraPosition,
                CameraForward = cameraForward,
                CameraUp = cameraUp,
                ActiveDistanceMeters = _activeDistanceMeters,
                FrozenDistanceMeters = _frozenDistanceMeters,
                FrustumForwardDotThreshold = FrustumForwardDotThreshold,
            };

            bool scheduled = false;
            try
            {
                _importanceHandle = scoringJob.Schedule(_targetCount, ImportanceScoreBatchSize);
                _importanceScheduled = true;
                _importanceAccumulator = 0.0f;
                _forceImmediateImportanceRefresh = false;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseImportanceJobBufferLocks();
            }
        }

        private void ApplyImportanceResults()
        {
            if (!TryResolveNativeBuffers(out FoveatedNativeBuffers buffers))
                return;

            _tier0Count = 0;
            _tier1Count = 0;
            _tier2Count = 0;
            _frozenEntityCount = 0;
            TryResolveScoringCamera(out Vector3 cameraPosition, out Vector3 cameraForward, out _);
            float now = ResolveFoveatedClockSeconds();
            for (int i = 0; i < _targetCount; i++)
            {
                IFoveatedSimulationTarget target = GetTargetAt(i);
                if (target == null)
                    continue;

                float importanceScore = buffers.ImportanceScores[i];
                FoveatedTickRate resolvedTickRate = (FoveatedTickRate)buffers.TickRateCodes[i];
                FoveatedSimulationTier resolvedTier = (FoveatedSimulationTier)buffers.EntitySimTiers[i];
                float distanceMeters = buffers.DistancesMeters[i];
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
                target.OnFoveatedCadenceResolved(_tickRates[i], _tickIntervals[i], importanceScore, buffers.InsideFrustumFlags[i] != 0);
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
                if ((signal.Flags & CombatDamageSignal.VisualOnlyFlag) != 0)
                    continue;

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
            float qualityWeight01 = ResolveGlobalQualityWeight01();
            float qualitySurvivalPressure01 = 1.0f - SmoothStep01(qualityWeight01);
            float homeostasisSurvivalPressure01 = SmoothStep01((float)_homeostasisPressureTier * (1.0f / 3.0f));
            float survivalPressure01 = math.saturate(math.max(qualitySurvivalPressure01, homeostasisSurvivalPressure01));
            float criticalPressure01 = SmoothStep01(homeostasisSurvivalPressure01);

            activeDistance = math.lerp(
                DefaultActiveDistanceMeters,
                math.lerp(SurvivalActiveDistanceMeters, CriticalSurvivalActiveDistanceMeters, criticalPressure01),
                survivalPressure01);
            frozenDistance = math.lerp(
                DefaultFrozenDistanceMeters,
                math.lerp(SurvivalFrozenDistanceMeters, CriticalSurvivalFrozenDistanceMeters, criticalPressure01),
                survivalPressure01);

            if (_thermalFreezeOverrideActive)
                frozenDistance = math.max(activeDistance, math.min(frozenDistance, _thermalFrozenDistanceMeters));
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 1.0f;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3.0f - (2.0f * t));
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
            if (!IsFinite(cameraPosition) || !IsFinite(cameraForward))
                DumpTelemetryBlackBoxOnce();

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsFoveatedVaultHandle(in _telemetryRingHandle, FoveatedTelemetryRingBufferId) ||
                !vault.TryAcquireWriteLock(in _telemetryRingHandle, VaultOwnerSystemId, out NativeArray<FoveatedSimulationTelemetryEntry> telemetryRing))
            {
                return;
            }

            try
            {
                if (!telemetryRing.IsCreated || telemetryRing.Length < TelemetryCapacity)
                    return;

                int cursor = _telemetryCursor;
                telemetryRing[cursor] = new FoveatedSimulationTelemetryEntry
                {
                    Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
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
            finally
            {
                vault.ReleaseWriteLock(in _telemetryRingHandle, VaultOwnerSystemId);
            }
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
            if (_blackBoxDumped || !TryResolveTelemetryRing(out NativeArray<FoveatedSimulationTelemetryEntry> telemetryRing))
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
                        FoveatedSimulationTelemetryEntry entry = telemetryRing[i];
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

        private void ApplyVisualInterpolationVisualSync()
        {
            for (int compactIndex = 0; compactIndex < _visualTargetCount; compactIndex++)
            {
                int targetIndex = _visualTargetIndices[compactIndex];
                Transform visualTransform = _visualTransformArray[compactIndex];
                if (visualTransform == null)
                    continue;

                Vector3 currentPosition = _visualToPositions[targetIndex];
                float3 originShiftedPresentationPosition;
                if (_tickRates[targetIndex] != FoveatedTickRate.Center60Hz)
                {
                    float alpha = math.saturate(_tickAccumulators[targetIndex] / math.max(_tickIntervals[targetIndex], MinimumVelocityDelta));
                    originShiftedPresentationPosition = ResolveOriginShiftedPresentationPosition(
                        _visualFromPositions[targetIndex],
                        currentPosition,
                        alpha);
                }
                else
                {
                    originShiftedPresentationPosition = currentPosition;
                }

                visualTransform.position = ToVector3(originShiftedPresentationPosition);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveOriginShiftedPresentationPosition(float3 fromPosition, float3 toPosition, float alpha)
        {
            float smoothAlpha = alpha * alpha * (3.0f - (2.0f * alpha));
            return math.lerp(fromPosition, toPosition, smoothAlpha);
        }

        private void RebuildVisualTargetCache()
        {
            int previousVisualTargetCount = _visualTargetCount;
            _visualTargetCount = 0;
            for (int i = 0; i < _targetCount; i++)
            {
                Transform visualTransform = _visualTransforms[i];
                if (visualTransform == null)
                    continue;

                _visualTargetIndices[_visualTargetCount] = i;
                _visualTargetCount++;
            }

            for (int i = 0; i < _visualTargetCount; i++)
            {
                int targetIndex = _visualTargetIndices[i];
                _visualTransformArray[i] = _visualTransforms[targetIndex];
            }

            for (int i = _visualTargetCount; i < previousVisualTargetCount; i++)
                _visualTransformArray[i] = null;

            _visualTargetCacheDirty = false;
        }

        private bool OpenOrAcquireNativeBuffersForOwnerRoute()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            FoveatedNativeBuffers buffers = default;
            bool resolved =
                OpenOrAcquireVaultArray(vault, ref _jobScorePositionsHandle, FoveatedScorePositionsBufferId, MaxTargets, NativeArrayOptions.UninitializedMemory, out buffers.ScorePositions) &&
                OpenOrAcquireVaultArray(vault, ref _jobEntityAupsHandle, FoveatedEntityAupsBufferId, MaxTargets, NativeArrayOptions.UninitializedMemory, out buffers.EntityAups) &&
                OpenOrAcquireVaultArray(vault, ref _jobImportanceScoresHandle, FoveatedImportanceScoresBufferId, MaxTargets, NativeArrayOptions.UninitializedMemory, out buffers.ImportanceScores) &&
                OpenOrAcquireVaultArray(vault, ref _jobTickRateCodesHandle, FoveatedTickRateCodesBufferId, MaxTargets, NativeArrayOptions.UninitializedMemory, out buffers.TickRateCodes) &&
                OpenOrAcquireVaultArray(vault, ref _jobInsideFrustumFlagsHandle, FoveatedInsideFrustumFlagsBufferId, MaxTargets, NativeArrayOptions.UninitializedMemory, out buffers.InsideFrustumFlags) &&
                OpenOrAcquireVaultArray(vault, ref _jobEntitySimTiersHandle, FoveatedEntitySimTiersBufferId, MaxTargets, NativeArrayOptions.UninitializedMemory, out buffers.EntitySimTiers) &&
                OpenOrAcquireVaultArray(vault, ref _jobDistancesMetersHandle, FoveatedDistancesMetersBufferId, MaxTargets, NativeArrayOptions.UninitializedMemory, out buffers.DistancesMeters) &&
                OpenOrAcquireVaultArray(vault, ref _jobFromPositionsHandle, FoveatedFromPositionsBufferId, MaxTargets, NativeArrayOptions.UninitializedMemory, out buffers.FromPositions) &&
                OpenOrAcquireVaultArray(vault, ref _jobToPositionsHandle, FoveatedToPositionsBufferId, MaxTargets, NativeArrayOptions.UninitializedMemory, out buffers.ToPositions) &&
                OpenOrAcquireVaultArray(vault, ref _jobAlphasHandle, FoveatedAlphasBufferId, MaxTargets, NativeArrayOptions.UninitializedMemory, out buffers.Alphas) &&
                OpenOrAcquireVaultArray(vault, ref _telemetryRingHandle, FoveatedTelemetryRingBufferId, TelemetryCapacity, NativeArrayOptions.ClearMemory, out buffers.TelemetryRing);

            if (!resolved)
                return false;

            if (!_nativeMemoryBudgetRegistered)
                RegisterNativeMemoryBudget(in buffers);

            return true;
        }

        private bool TryResolveNativeBuffers(out FoveatedNativeBuffers buffers)
        {
            buffers = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return TryResolveVaultArray(vault, FoveatedScorePositionsBufferId, in _jobScorePositionsHandle, MaxTargets, out buffers.ScorePositions) &&
                   TryResolveVaultArray(vault, FoveatedEntityAupsBufferId, in _jobEntityAupsHandle, MaxTargets, out buffers.EntityAups) &&
                   TryResolveVaultArray(vault, FoveatedImportanceScoresBufferId, in _jobImportanceScoresHandle, MaxTargets, out buffers.ImportanceScores) &&
                   TryResolveVaultArray(vault, FoveatedTickRateCodesBufferId, in _jobTickRateCodesHandle, MaxTargets, out buffers.TickRateCodes) &&
                   TryResolveVaultArray(vault, FoveatedInsideFrustumFlagsBufferId, in _jobInsideFrustumFlagsHandle, MaxTargets, out buffers.InsideFrustumFlags) &&
                   TryResolveVaultArray(vault, FoveatedEntitySimTiersBufferId, in _jobEntitySimTiersHandle, MaxTargets, out buffers.EntitySimTiers) &&
                   TryResolveVaultArray(vault, FoveatedDistancesMetersBufferId, in _jobDistancesMetersHandle, MaxTargets, out buffers.DistancesMeters) &&
                   TryResolveVaultArray(vault, FoveatedFromPositionsBufferId, in _jobFromPositionsHandle, MaxTargets, out buffers.FromPositions) &&
                   TryResolveVaultArray(vault, FoveatedToPositionsBufferId, in _jobToPositionsHandle, MaxTargets, out buffers.ToPositions) &&
                   TryResolveVaultArray(vault, FoveatedAlphasBufferId, in _jobAlphasHandle, MaxTargets, out buffers.Alphas) &&
                   TryResolveVaultArray(vault, FoveatedTelemetryRingBufferId, in _telemetryRingHandle, TelemetryCapacity, out buffers.TelemetryRing);
        }

        private bool TryResolveTelemetryRing(out NativeArray<FoveatedSimulationTelemetryEntry> telemetryRing)
        {
            IDataVault vault = _dataVault;
            return TryResolveVaultArray(vault, FoveatedTelemetryRingBufferId, in _telemetryRingHandle, TelemetryCapacity, out telemetryRing);
        }

        private bool TryWriteScorePositionsForImportanceJob()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsFoveatedVaultHandle(in _jobScorePositionsHandle, FoveatedScorePositionsBufferId) ||
                !vault.TryAcquireWriteLock(in _jobScorePositionsHandle, VaultOwnerSystemId, out NativeArray<float3> scorePositions))
            {
                return false;
            }

            try
            {
                if (!scorePositions.IsCreated || scorePositions.Length < MaxTargets)
                    return false;

                for (int i = 0; i < _targetCount; i++)
                {
                    Transform simulationTransform = _simulationTransforms[i];
                    scorePositions[i] = simulationTransform != null
                        ? (float3)simulationTransform.position
                        : float3.zero;
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _jobScorePositionsHandle, VaultOwnerSystemId);
            }
        }

        private bool TryPinImportanceJobBuffers()
        {
            if (_importanceJobBuffersLocked)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || !TryValidateImportanceJobBuffers(vault))
                return false;

            bool acquired = false;
            try
            {
                if (!vault.TryAcquireMutationGuard(ImportanceJobMutationGuardMask))
                    return false;

                acquired = true;
                if (!TryValidateImportanceJobBuffers(vault))
                    return false;

                _importanceJobGuardVault = vault;
                _importanceJobBuffersLocked = true;
                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseMutationGuard(ImportanceJobMutationGuardMask);
            }
        }

        private bool TryValidateImportanceJobBuffers(IDataVault vault)
        {
            return TryResolveVaultArray(vault, FoveatedScorePositionsBufferId, in _jobScorePositionsHandle, MaxTargets, out _) &&
                   TryResolveVaultArray(vault, FoveatedEntityAupsBufferId, in _jobEntityAupsHandle, MaxTargets, out _) &&
                   TryResolveVaultArray(vault, FoveatedImportanceScoresBufferId, in _jobImportanceScoresHandle, MaxTargets, out _) &&
                   TryResolveVaultArray(vault, FoveatedTickRateCodesBufferId, in _jobTickRateCodesHandle, MaxTargets, out _) &&
                   TryResolveVaultArray(vault, FoveatedInsideFrustumFlagsBufferId, in _jobInsideFrustumFlagsHandle, MaxTargets, out _) &&
                   TryResolveVaultArray(vault, FoveatedEntitySimTiersBufferId, in _jobEntitySimTiersHandle, MaxTargets, out _) &&
                   TryResolveVaultArray(vault, FoveatedDistancesMetersBufferId, in _jobDistancesMetersHandle, MaxTargets, out _);
        }

        private void ReleaseImportanceJobBufferLocks()
        {
            if (!_importanceJobBuffersLocked)
                return;

            IDataVault vault = _importanceJobGuardVault ?? _dataVault;
            if (vault != null)
                vault.ReleaseMutationGuard(ImportanceJobMutationGuardMask);

            _importanceJobGuardVault = null;
            _importanceJobBuffersLocked = false;
        }

        private static bool OpenOrAcquireVaultArray<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> array) where T : struct
        {
            if (!IsFoveatedVaultHandle(in handle, bufferId))
            {
                if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                {
                    array = default;
                    return false;
                }

                handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, VaultOwnerSystemId, options);
            }

            if (TryResolveVaultArray(vault, bufferId, in handle, requiredLength, out array))
            {
                return true;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
            {
                array = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, VaultOwnerSystemId, options);
            if (TryResolveVaultArray(vault, bufferId, in handle, requiredLength, out array))
                return true;

            ReleaseVaultHandle(vault, bufferId, ref handle);
            array = default;
            return false;
        }

        private static bool TryResolveVaultArray<T>(
            IDataVault vault,
            BufferID bufferId,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> array) where T : struct
        {
            array = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsFoveatedVaultHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out array) &&
                   array.IsCreated &&
                   array.Length >= requiredLength;
        }

        private static bool IsFoveatedVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void DisposeNativeBuffers(JobHandle dependency)
        {
            MemoryBudgetTracker.Unregister(MemoryBudgetOwnerName);
            JobHandle disposeHandle = dependency;
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
            ReleaseImportanceJobBufferLocks();

            ReleaseNativeVaultHandles(_dataVault);
            ClearNativeBufferAliases();
        }

        private void ClearNativeBufferAliases()
        {
            _nativeMemoryBudgetRegistered = false;
        }

        private void ReleaseNativeVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, FoveatedScorePositionsBufferId, ref _jobScorePositionsHandle);
            ReleaseVaultHandle(vault, FoveatedEntityAupsBufferId, ref _jobEntityAupsHandle);
            ReleaseVaultHandle(vault, FoveatedImportanceScoresBufferId, ref _jobImportanceScoresHandle);
            ReleaseVaultHandle(vault, FoveatedTickRateCodesBufferId, ref _jobTickRateCodesHandle);
            ReleaseVaultHandle(vault, FoveatedInsideFrustumFlagsBufferId, ref _jobInsideFrustumFlagsHandle);
            ReleaseVaultHandle(vault, FoveatedEntitySimTiersBufferId, ref _jobEntitySimTiersHandle);
            ReleaseVaultHandle(vault, FoveatedDistancesMetersBufferId, ref _jobDistancesMetersHandle);
            ReleaseVaultHandle(vault, FoveatedFromPositionsBufferId, ref _jobFromPositionsHandle);
            ReleaseVaultHandle(vault, FoveatedToPositionsBufferId, ref _jobToPositionsHandle);
            ReleaseVaultHandle(vault, FoveatedAlphasBufferId, ref _jobAlphasHandle);
            ReleaseVaultHandle(vault, FoveatedTelemetryRingBufferId, ref _telemetryRingHandle);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void RebindDataVaultForOwnerRoute(IDataVault previousVault, IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
            {
                if (_dataVault != null)
                    OpenOrAcquireNativeBuffersForOwnerRoute();
                return;
            }

            IDataVault releaseVault = _dataVault ?? previousVault;
            if (releaseVault != null)
            {
                ForceCompleteFrameJobsInPostSimulationWindow();
                MemoryBudgetTracker.Unregister(MemoryBudgetOwnerName);
                ReleaseNativeVaultHandles(releaseVault);
                ClearNativeBufferAliases();
            }

            _dataVault = nextVault;
            if (_dataVault != null)
                OpenOrAcquireNativeBuffersForOwnerRoute();
        }

        private static void ReleaseVaultHandle<T>(
            IDataVault vault,
            BufferID bufferId,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (IsFoveatedVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool IsActiveFoveatedTarget(IFoveatedSimulationTarget target)
        {
            if (target == null)
                return false;

            int index = target.FoveatedTargetIndex;
            return index >= 0 && index < _targetCount && ReferenceEquals(_targets[index], target);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IFoveatedSimulationTarget GetTargetAt(int index)
        {
            return _targets[index] as IFoveatedSimulationTarget;
        }

        private void RegisterNativeMemoryBudget(in FoveatedNativeBuffers buffers)
        {
            long totalBytes = GetNativeArrayBytes(buffers.ScorePositions) +
                              GetNativeArrayBytes(buffers.EntityAups) +
                              GetNativeArrayBytes(buffers.ImportanceScores) +
                              GetNativeArrayBytes(buffers.TickRateCodes) +
                              GetNativeArrayBytes(buffers.InsideFrustumFlags) +
                              GetNativeArrayBytes(buffers.EntitySimTiers) +
                              GetNativeArrayBytes(buffers.DistancesMeters) +
                              GetNativeArrayBytes(buffers.FromPositions) +
                              GetNativeArrayBytes(buffers.ToPositions) +
                              GetNativeArrayBytes(buffers.Alphas) +
                              GetNativeArrayBytes(buffers.TelemetryRing);
            MemoryBudgetTracker.Register(MemoryBudgetOwnerName, totalBytes, PersistentNativeBudgetBytes);
            _nativeMemoryBudgetRegistered = true;
        }

        private static long GetNativeArrayBytes<T>(NativeArray<T> array) where T : struct
        {
            return array.IsCreated ? (long)array.Length * UnsafeUtility.SizeOf<T>() : 0L;
        }

        private void DisposeVisualTransformAccessArray()
        {
            if (_visualTransformAccessArray.isCreated)
                _visualTransformAccessArray.Dispose();
        }

        private bool RefreshViewCameraBinding(float frameDeltaTime)
        {
            if (_cameraTransform != null)
                return true;

            if (_cameraResolveRetryTimer > 0.0f)
            {
                _cameraResolveRetryTimer -= math.max(frameDeltaTime, 0.0f);
                return false;
            }

            _cameraResolveRetryTimer = CameraResolveRetryInterval;
            IPlayerRuntimeContext playerContext = _playerContext;
            _viewCamera = playerContext != null ? playerContext.PlayerCamera : null;

            if (_viewCamera == null)
                return false;

            _cameraTransform = _viewCamera.transform;
            _cameraResolveRetryTimer = 0.0f;
            return true;
        }

        private void RefreshListenerBinding(float frameDeltaTime)
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

            if (_listenerTransform != null)
                _listenerResolveRetryTimer = 0.0f;
        }

        private void UpdateListenerVelocity(float frameDeltaTime)
        {
            if (_listenerTransform == null)
                return;

            if (CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityListenerMaxAgeFrames, out Vector3 kccVelocity))
            {
                _listenerVelocity = kccVelocity;
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


