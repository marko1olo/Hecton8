using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core.Bucketing
{
    /// <summary>
    /// Registry-owned modulo time-slicer. Persistent tables live in GlobalDataVault; this type keeps only handles and scalar frame state.
    /// </summary>
    public sealed class ModuloSimulationBucketer : ISimulationBucketer
    {
        private const int RebalanceResultLength = 1;
        private const int FrameStateLength = 1;
        private const int BlackBoxFrameCount = 300;
        private const int BlackBoxEntrySizeBytes = 64;
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR.bin";
        private const ulong BlackBoxDumpMagic = 0x00384E4F54434548ul; // HECTON8\0
        private const uint BlackBoxDumpVersion = 1u;
        private const float DefaultEntityCostMs = 0.025f;
        private const float CatastrophicCostClampMs = 1000f;
        private const float EwmaWeight = 0.10f;
        private const ulong RebalanceVaultMutationGuardMask =
            (1UL << 3) |
            (1UL << 4) |
            (1UL << 6) |
            (1UL << 8);
        private const ulong EntityStateVaultMutationGuardMask =
            (1UL << 2) |
            (1UL << 3) |
            (1UL << 4) |
            (1UL << 5) |
            (1UL << 6) |
            (1UL << 8);

        private VaultGenerationHandle<int> _entityBucketsHandle;
        private VaultGenerationHandle<int> _entityBucketsWorkHandle;
        private VaultGenerationHandle<float> _entityCostEwmaHandle;
        private VaultGenerationHandle<float> _bucketLoadEwmaHandle;
        private VaultGenerationHandle<float> _rebalanceBucketLoadsHandle;
        private VaultGenerationHandle<SimulationBucketRebalanceResult> _rebalanceResultHandle;
        private VaultGenerationHandle<SimulationBucketFrameState> _frameStateHandle;
        private VaultGenerationHandle<SimulationBucketBlackBoxEntry> _blackBoxHandle;
        private IDataVault _dataVault;
        private JobHandle _rebalanceHandle;
        private int _entityCapacity;
        private int _entityMask;
        private int _currentFrameCount;
        private int _slowBucketCount = SimulationBucketConstants.SurvivalSlowBucketCount;
        private int _slowBucketMask = SimulationBucketConstants.SurvivalSlowBucketMask;
        private int _slowBucketGroupMask = SimulationBucketConstants.SurvivalSlowBucketMask;
        private int _activeSlowBucketGroup;
        private int _activeSlowBucketShift;
        private int _activeFastBucket;
        private int _activeSlowBucket;
        private int _activeColdBucket;
        private int _criticalDebtFrames;
        private int _rebalanceCountdown = SimulationBucketConstants.RebalanceCadenceFrames;
        private int _mutationVersion;
        private int _rebalanceMutationVersion;
        private uint _rebalanceSequence;
        private uint _framePacingFlags;
        private int _blackBoxCursor;
        private int _lastBlackBoxFrame = -1;
        private byte _activeSlowBucketCount = SimulationBucketConstants.MinimumActiveSlowBucketCount;
        private float _lastActiveBucketLoadMs;
        private float _activeBucketLoadEwmaMs;
        private float _jitterVarianceMs;
        private float _expectedMaxBucketLoadMs;
        private float _expectedMeanBucketLoadMs;
        private float _preSimulationCostMs;
        private float _simulationBucketInterpolationAlpha;
        private float _qualityWeight01 = 1f;
        private float _survivalDistributionPressure01;
        private bool _aupBarrierActive;
        private bool _rebalancePending;
        private bool _nonFiniteCostObserved;
        private bool _pendingBlackBoxDump;
        private IDataVault _rebalanceVaultGuardVault;
        private bool _rebalanceVaultGuardHeld;

        public bool IsInitialized
        {
            get
            {
                NativeArray<int>.ReadOnly entityBuckets = ReadEntityBuckets();
                return _entityCapacity > 0 && entityBuckets.Length >= _entityCapacity;
            }
        }

        public NativeArray<int>.ReadOnly EntityBuckets
        {
            get
            {
                return ReadEntityBuckets();
            }
        }

        public int EntityCapacity => IsInitialized ? _entityCapacity : 0;

        public int CurrentFrameCount => _currentFrameCount;

        public int FastBucketCount => SimulationBucketConstants.FastBucketCount;

        public int SlowBucketCount => _slowBucketCount;

        public int ColdBucketCount => SimulationBucketConstants.ColdBucketCount;

        public int FastBucketMask => SimulationBucketConstants.FastBucketMask;

        public int SlowBucketMask => _slowBucketMask;

        public int ColdBucketMask => SimulationBucketConstants.ColdBucketMask;

        public int ActiveFastBucket => _activeFastBucket;

        public int ActiveSlowBucket => _activeSlowBucket;

        public int ActiveColdBucket => _activeColdBucket;

        public byte ActiveSlowBucketCount => _activeSlowBucketCount;

        public float LastActiveBucketLoadMs => _lastActiveBucketLoadMs;

        public float JitterVarianceMs => _jitterVarianceMs;

        public float ExpectedMaxBucketLoadMs => _expectedMaxBucketLoadMs;

        public float ExpectedMeanBucketLoadMs => _expectedMeanBucketLoadMs;

        public float SimulationBucketInterpolationAlpha => _simulationBucketInterpolationAlpha;

        public uint FramePacingFlags => _framePacingFlags;

        public bool AupBarrierActive => _aupBarrierActive;

        public void Initialize(int entityCapacity)
        {
            Initialize(entityCapacity, _dataVault);
        }

        /// <summary>
        /// Binds all persistent tables from the bootstrap-owned vault.
        /// </summary>
        public void Initialize(int entityCapacity, IDataVault dataVault)
        {
            int capacity = SimulationBucketMath.RoundUpToPowerOfTwo(
                math.clamp(entityCapacity, 1, SimulationBucketConstants.MaxEntityCapacity));

            if (dataVault == null)
            {
                ReleaseHandlesOnly();
                ResetStateAfterAllocationFailure();
                return;
            }

            if (ReferenceEquals(_dataVault, dataVault) && _entityCapacity == capacity && ReadEntityBuckets().IsCreated)
                return;

            if (dataVault.IsAllocationLocked || dataVault.IsCompactionFenceActive)
                return;

            ReleaseHandlesOnly();
            _dataVault = dataVault;
            _entityCapacity = capacity;
            _entityMask = capacity - 1;

            _entityBucketsHandle = dataVault.EnsureGenerationHandle<int>(
                BufferID.SimulationBucketEntityFront,
                capacity,
                SystemID.SimulationBucketer,
                NativeArrayOptions.UninitializedMemory);
            _entityBucketsWorkHandle = dataVault.EnsureGenerationHandle<int>(
                BufferID.SimulationBucketEntityWork,
                capacity,
                SystemID.SimulationBucketer,
                NativeArrayOptions.UninitializedMemory);
            _entityCostEwmaHandle = dataVault.EnsureGenerationHandle<float>(
                BufferID.SimulationBucketEntityCostEwma,
                capacity,
                SystemID.SimulationBucketer,
                NativeArrayOptions.ClearMemory);
            _bucketLoadEwmaHandle = dataVault.EnsureGenerationHandle<float>(
                BufferID.SimulationBucketLoadEwma,
                SimulationBucketConstants.SurvivalSlowBucketCount,
                SystemID.SimulationBucketer,
                NativeArrayOptions.ClearMemory);
            _rebalanceBucketLoadsHandle = dataVault.EnsureGenerationHandle<float>(
                BufferID.SimulationBucketRebalanceLoads,
                SimulationBucketConstants.SurvivalSlowBucketCount,
                SystemID.SimulationBucketer,
                NativeArrayOptions.ClearMemory);
            _rebalanceResultHandle = dataVault.EnsureGenerationHandle<SimulationBucketRebalanceResult>(
                BufferID.SimulationBucketRebalanceResult,
                RebalanceResultLength,
                SystemID.SimulationBucketer,
                NativeArrayOptions.ClearMemory);
            _frameStateHandle = dataVault.EnsureGenerationHandle<SimulationBucketFrameState>(
                BufferID.SimulationBucketFrameState,
                FrameStateLength,
                SystemID.SimulationBucketer,
                NativeArrayOptions.ClearMemory);
            _blackBoxHandle = dataVault.EnsureGenerationHandle<SimulationBucketBlackBoxEntry>(
                BufferID.SimulationBucketBlackBox,
                BlackBoxFrameCount,
                SystemID.SimulationBucketer,
                NativeArrayOptions.ClearMemory);

            if (!HasRequiredVaultBuffers())
            {
                ReleaseHandlesOnly();
                ResetStateAfterAllocationFailure();
                return;
            }

            _rebalanceCountdown = SimulationBucketConstants.RebalanceCadenceFrames;
            ClearEntityState();
            UpdateFrameStateBuffer();
        }

        public void AdvanceFrame(float globalQualityWeight01, float unscaledDeltaTime, int criticalDebtFrames, bool aupBarrierActive)
        {
            if (!IsInitialized)
                return;

            CompleteRebalanceIfReady();

            _currentFrameCount = _currentFrameCount == int.MaxValue ? 0 : _currentFrameCount + 1;
            _qualityWeight01 = SanitizeQualityWeight01(globalQualityWeight01);
            float precisionCurve01 = SmoothStep01(_qualityWeight01);
            _survivalDistributionPressure01 = 1f - precisionCurve01;
            _slowBucketCount = SimulationBucketConstants.SurvivalSlowBucketCount;
            _slowBucketMask = _slowBucketCount - 1;
            _activeFastBucket = _currentFrameCount & SimulationBucketConstants.FastBucketMask;
            _activeColdBucket = _currentFrameCount & SimulationBucketConstants.ColdBucketMask;
            _criticalDebtFrames = math.max(0, criticalDebtFrames);
            _aupBarrierActive = aupBarrierActive;
            _activeSlowBucketCount = ResolveActiveSlowBucketCount(_qualityWeight01, unscaledDeltaTime, _criticalDebtFrames, aupBarrierActive, _currentFrameCount);
            _activeSlowBucketShift = ResolveActiveSlowBucketShift(_activeSlowBucketCount);
            _slowBucketGroupMask = (_slowBucketCount >> _activeSlowBucketShift) - 1;
            _activeSlowBucketGroup = _currentFrameCount & _slowBucketGroupMask;
            _activeSlowBucket = (_activeSlowBucketGroup << _activeSlowBucketShift) & _slowBucketMask;
            _simulationBucketInterpolationAlpha = ResolveGlobalInterpolationAlpha();

            ScheduleRebalanceIfDue(ResolveRebalanceCadenceFrames(_qualityWeight01));

            UpdatePacingFlags();
            UpdateFrameStateBuffer();
            WriteBlackBoxEntry();
        }

        public void AdvanceFrame(byte globalQualityWeightByte, float unscaledDeltaTime, int criticalDebtFrames, bool aupBarrierActive)
        {
            AdvanceFrame(globalQualityWeightByte * 0.00392156862745f, unscaledDeltaTime, criticalDebtFrames, aupBarrierActive);
        }

        public void ReportActiveBucketLoadMs(float milliseconds)
        {
            float sanitized = SanitizeCost(milliseconds);
            _lastActiveBucketLoadMs = sanitized;
            float previous = _activeBucketLoadEwmaMs > 0f && math.isfinite(_activeBucketLoadEwmaMs)
                ? _activeBucketLoadEwmaMs
                : sanitized;
            _activeBucketLoadEwmaMs = math.lerp(previous, sanitized, EwmaWeight);
            float previousJitter = math.isfinite(_jitterVarianceMs) && _jitterVarianceMs >= 0f
                ? _jitterVarianceMs
                : 0f;
            _jitterVarianceMs = math.lerp(previousJitter, math.abs(sanitized - previous), EwmaWeight);

            bool bucketLoadsLocked = false;
            try
            {
                bucketLoadsLocked = TryAcquireWriteView(in _bucketLoadEwmaHandle, out NativeArray<float> bucketLoads);
                if (bucketLoadsLocked && bucketLoads.IsCreated && (uint)_activeSlowBucket < (uint)bucketLoads.Length)
                {
                    float current = bucketLoads[_activeSlowBucket];
                    float seed = current > 0f && math.isfinite(current) ? current : sanitized;
                    bucketLoads[_activeSlowBucket] = math.lerp(seed, sanitized, EwmaWeight);
                }
            }
            finally
            {
                if (bucketLoadsLocked)
                    ReleaseWriteView(in _bucketLoadEwmaHandle);
            }

            UpdatePacingFlags();
            UpdateFrameStateBuffer();
            WriteBlackBoxEntry();
        }

        public void ReportPreSimulationCostMs(float milliseconds)
        {
            _preSimulationCostMs = SanitizeCost(milliseconds);
            UpdatePacingFlags();
            UpdateFrameStateBuffer();
        }

        public bool TryReportEntityCostMs(int entityIndex, float measuredCostMs)
        {
            if (_rebalancePending)
                return false;

            bool costsLocked = false;
            try
            {
                costsLocked = TryAcquireWriteView(in _entityCostEwmaHandle, out NativeArray<float> costs);
                if (!costsLocked || !costs.IsCreated || (uint)entityIndex >= (uint)costs.Length)
                    return false;

                float sanitized = SanitizeCost(measuredCostMs);
                float current = costs[entityIndex];
                float seed = current > 0f && math.isfinite(current) ? current : sanitized;
                costs[entityIndex] = math.lerp(seed, sanitized, EwmaWeight);
                return true;
            }
            finally
            {
                if (costsLocked)
                    ReleaseWriteView(in _entityCostEwmaHandle);
            }
        }

        public int ResolveEntityIndex(uint stableHash)
        {
            return IsInitialized ? (int)(stableHash & unchecked((uint)_entityMask)) : -1;
        }

        public bool TryRegisterEntityBucket(int entityIndex, uint stableHash)
        {
            int bucket = ResolveSlowBucket(stableHash);
            bool entityBucketsLocked = TryAcquireWriteView(in _entityBucketsHandle, out NativeArray<int> entityBuckets);
            if (!entityBucketsLocked || !entityBuckets.IsCreated || (uint)entityIndex >= (uint)entityBuckets.Length)
            {
                if (entityBucketsLocked)
                    ReleaseWriteView(in _entityBucketsHandle);
                return false;
            }

            try
            {
                entityBuckets[entityIndex] = bucket;
            }
            finally
            {
                ReleaseWriteView(in _entityBucketsHandle);
            }

            if (!_rebalancePending)
            {
                bool workLocked = TryAcquireWriteView(in _entityBucketsWorkHandle, out NativeArray<int> work);
                try
                {
                    if (workLocked && work.IsCreated && (uint)entityIndex < (uint)work.Length)
                        work[entityIndex] = bucket;
                }
                finally
                {
                    if (workLocked)
                        ReleaseWriteView(in _entityBucketsWorkHandle);
                }

                bool costsLocked = TryAcquireWriteView(in _entityCostEwmaHandle, out NativeArray<float> costs);
                try
                {
                    if (costsLocked && costs.IsCreated && (uint)entityIndex < (uint)costs.Length)
                        costs[entityIndex] = DefaultEntityCostMs;
                }
                finally
                {
                    if (costsLocked)
                        ReleaseWriteView(in _entityCostEwmaHandle);
                }
            }

            _mutationVersion++;
            return true;
        }

        public bool TryUnregisterEntityBucket(int entityIndex)
        {
            bool entityBucketsLocked = TryAcquireWriteView(in _entityBucketsHandle, out NativeArray<int> entityBuckets);
            if (!entityBucketsLocked || !entityBuckets.IsCreated || (uint)entityIndex >= (uint)entityBuckets.Length)
            {
                if (entityBucketsLocked)
                    ReleaseWriteView(in _entityBucketsHandle);
                return false;
            }

            try
            {
                entityBuckets[entityIndex] = -1;
            }
            finally
            {
                ReleaseWriteView(in _entityBucketsHandle);
            }

            if (!_rebalancePending)
            {
                bool workLocked = TryAcquireWriteView(in _entityBucketsWorkHandle, out NativeArray<int> work);
                try
                {
                    if (workLocked && work.IsCreated && (uint)entityIndex < (uint)work.Length)
                        work[entityIndex] = -1;
                }
                finally
                {
                    if (workLocked)
                        ReleaseWriteView(in _entityBucketsWorkHandle);
                }

                bool costsLocked = TryAcquireWriteView(in _entityCostEwmaHandle, out NativeArray<float> costs);
                try
                {
                    if (costsLocked && costs.IsCreated && (uint)entityIndex < (uint)costs.Length)
                        costs[entityIndex] = 0f;
                }
                finally
                {
                    if (costsLocked)
                        ReleaseWriteView(in _entityCostEwmaHandle);
                }
            }

            _mutationVersion++;
            return true;
        }

        public int ResolveFastBucket(uint stableHash)
        {
            return SimulationBucketMath.ResolveBucket(stableHash, SimulationBucketConstants.FastBucketMask);
        }

        public int ResolveSlowBucket(uint stableHash)
        {
            return SimulationBucketMath.ResolveBucket(stableHash, _slowBucketMask);
        }

        public int ResolveColdBucket(uint stableHash)
        {
            return SimulationBucketMath.ResolveBucket(stableHash, SimulationBucketConstants.ColdBucketMask);
        }

        public bool IsFastBucketActive(int bucketId)
        {
            return (bucketId & SimulationBucketConstants.FastBucketMask) == _activeFastBucket;
        }

        public bool IsSlowBucketActive(int bucketId)
        {
            int bucketGroup = (bucketId & _slowBucketMask) >> _activeSlowBucketShift;
            return bucketGroup == _activeSlowBucketGroup;
        }

        public bool IsColdBucketActive(int bucketId)
        {
            return (bucketId & SimulationBucketConstants.ColdBucketMask) == _activeColdBucket;
        }

        public float ResolveSlowBucketInterpolationAlpha(int bucketId)
        {
            int bucketGroup = (bucketId & _slowBucketMask) >> _activeSlowBucketShift;
            int distance = SimulationBucketMath.ResolveWrappedDistance(_activeSlowBucketGroup, bucketGroup, _slowBucketGroupMask);
            return math.saturate(distance * math.rcp(math.max(1, _slowBucketGroupMask)));
        }

        public SimulationBucketFrameState CaptureFrameState()
        {
            return new SimulationBucketFrameState
            {
                CurrentFrameCount = _currentFrameCount,
                ActiveFastBucket = _activeFastBucket,
                ActiveSlowBucket = _activeSlowBucket,
                ActiveColdBucket = _activeColdBucket,
                SlowBucketCount = _slowBucketCount,
                SlowBucketMask = _slowBucketMask,
                ActiveSlowBucketCount = _activeSlowBucketCount,
                CriticalDebtFrames = _criticalDebtFrames,
                AupBarrierActive = _aupBarrierActive ? (byte)1 : (byte)0,
                ActiveBucketLoadMs = _lastActiveBucketLoadMs,
                JitterVarianceMs = _jitterVarianceMs,
                ExpectedMaxBucketLoadMs = _expectedMaxBucketLoadMs,
                ExpectedMeanBucketLoadMs = _expectedMeanBucketLoadMs,
                PreSimulationCostMs = _preSimulationCostMs,
                SimulationBucketInterpolationAlpha = _simulationBucketInterpolationAlpha,
                FramePacingFlags = _framePacingFlags,
                RebalanceSequence = _rebalanceSequence,
                ReservedPadding = 0
            };
        }

        public void Dispose()
        {
            ReleaseHandlesOnly();
            ResetStateAfterAllocationFailure();
        }

        private bool HasRequiredVaultBuffers()
        {
            if (_entityCapacity <= 0)
                return false;

            NativeArray<int> entityBuckets = OpenEntityBucketsForOwner();
            NativeArray<int> work = OpenEntityBucketsWorkForOwner();
            NativeArray<float> costs = OpenEntityCostEwmaForOwner();
            NativeArray<float> bucketLoads = OpenBucketLoadEwmaForOwner();
            NativeArray<float> rebalanceLoads = OpenRebalanceBucketLoadsForOwner();
            NativeArray<SimulationBucketRebalanceResult> result = OpenRebalanceResultForOwner();
            NativeArray<SimulationBucketFrameState> frameState = OpenFrameStateBufferForOwner();
            NativeArray<SimulationBucketBlackBoxEntry> blackBox = OpenBlackBoxBufferForOwner();

            return entityBuckets.IsCreated &&
                   entityBuckets.Length >= _entityCapacity &&
                   work.IsCreated &&
                   work.Length >= _entityCapacity &&
                   costs.IsCreated &&
                   costs.Length >= _entityCapacity &&
                   bucketLoads.IsCreated &&
                   bucketLoads.Length >= SimulationBucketConstants.SurvivalSlowBucketCount &&
                   rebalanceLoads.IsCreated &&
                   rebalanceLoads.Length >= SimulationBucketConstants.SurvivalSlowBucketCount &&
                   result.IsCreated &&
                   result.Length >= RebalanceResultLength &&
                   frameState.IsCreated &&
                   frameState.Length >= FrameStateLength &&
                   blackBox.IsCreated &&
                   blackBox.Length >= BlackBoxFrameCount;
        }

        private NativeArray<int> OpenEntityBucketsForOwner()
        {
            return TryOpenVaultBufferForOwner(in _entityBucketsHandle, out NativeArray<int> buffer) ? buffer : default;
        }

        private NativeArray<int>.ReadOnly ReadEntityBuckets()
        {
            return TryReadVaultBuffer(in _entityBucketsHandle, out NativeArray<int>.ReadOnly buffer) ? buffer : default;
        }

        private NativeArray<int> OpenEntityBucketsWorkForOwner()
        {
            return TryOpenVaultBufferForOwner(in _entityBucketsWorkHandle, out NativeArray<int> buffer) ? buffer : default;
        }

        private NativeArray<float> OpenEntityCostEwmaForOwner()
        {
            return TryOpenVaultBufferForOwner(in _entityCostEwmaHandle, out NativeArray<float> buffer) ? buffer : default;
        }

        private NativeArray<float> OpenBucketLoadEwmaForOwner()
        {
            return TryOpenVaultBufferForOwner(in _bucketLoadEwmaHandle, out NativeArray<float> buffer) ? buffer : default;
        }

        private NativeArray<float> OpenRebalanceBucketLoadsForOwner()
        {
            return TryOpenVaultBufferForOwner(in _rebalanceBucketLoadsHandle, out NativeArray<float> buffer) ? buffer : default;
        }

        private NativeArray<SimulationBucketRebalanceResult> OpenRebalanceResultForOwner()
        {
            return TryOpenVaultBufferForOwner(in _rebalanceResultHandle, out NativeArray<SimulationBucketRebalanceResult> buffer) ? buffer : default;
        }

        private NativeArray<SimulationBucketFrameState> OpenFrameStateBufferForOwner()
        {
            return TryOpenVaultBufferForOwner(in _frameStateHandle, out NativeArray<SimulationBucketFrameState> buffer) ? buffer : default;
        }

        private NativeArray<SimulationBucketBlackBoxEntry> OpenBlackBoxBufferForOwner()
        {
            return TryOpenVaultBufferForOwner(in _blackBoxHandle, out NativeArray<SimulationBucketBlackBoxEntry> buffer) ? buffer : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryOpenVaultBufferForOwner<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || handle.BufferID == 0u)
            {
                buffer = default;
                return false;
            }

            return vault.TryResolveHandle(in handle, out buffer) && buffer.IsCreated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadVaultBuffer<T>(in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || handle.BufferID == 0u)
            {
                buffer = default;
                return false;
            }

            return vault.TryReadOnlyHandle(in handle, out buffer) && buffer.Length > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryAcquireWriteView<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || handle.BufferID == 0u)
            {
                buffer = default;
                return false;
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.SimulationBucketer, out buffer))
                return false;

            bool handedOff = false;
            try
            {
                if (buffer.IsCreated)
                {
                    handedOff = true;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (!handedOff)
                    vault.ReleaseWriteLock(in handle, SystemID.SimulationBucketer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReleaseWriteView<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseWriteLock(in handle, SystemID.SimulationBucketer);
        }

        private void ReleaseHandlesOnly()
        {
            if (_rebalancePending)
            {
                CompleteRebalanceHandle(ref _rebalanceHandle);
                _rebalancePending = false;
            }
            ReleaseRebalanceVaultGuard();

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _entityBucketsHandle);
                ReleaseVaultHandle(vault, ref _entityBucketsWorkHandle);
                ReleaseVaultHandle(vault, ref _entityCostEwmaHandle);
                ReleaseVaultHandle(vault, ref _bucketLoadEwmaHandle);
                ReleaseVaultHandle(vault, ref _rebalanceBucketLoadsHandle);
                ReleaseVaultHandle(vault, ref _rebalanceResultHandle);
                ReleaseVaultHandle(vault, ref _frameStateHandle);
                ReleaseVaultHandle(vault, ref _blackBoxHandle);
            }

            _entityBucketsHandle = default;
            _entityBucketsWorkHandle = default;
            _entityCostEwmaHandle = default;
            _bucketLoadEwmaHandle = default;
            _rebalanceBucketLoadsHandle = default;
            _rebalanceResultHandle = default;
            _frameStateHandle = default;
            _blackBoxHandle = default;
            _dataVault = null;
            _entityCapacity = 0;
            _entityMask = 0;
            _blackBoxCursor = 0;
            _lastBlackBoxFrame = -1;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ClearEntityState()
        {
            if (!TryAcquireVaultMutationGuard(EntityStateVaultMutationGuardMask, out IDataVault guardVault))
                return;

            try
            {
                NativeArray<int> entityBuckets = OpenEntityBucketsForOwner();
                if (entityBuckets.IsCreated)
                {
                    for (int i = 0; i < entityBuckets.Length; i++)
                        entityBuckets[i] = -1;
                }

                NativeArray<int> work = OpenEntityBucketsWorkForOwner();
                if (work.IsCreated)
                {
                    for (int i = 0; i < work.Length; i++)
                        work[i] = -1;
                }

                NativeArray<float> costs = OpenEntityCostEwmaForOwner();
                if (costs.IsCreated)
                {
                    for (int i = 0; i < costs.Length; i++)
                        costs[i] = 0f;
                }

                NativeArray<float> bucketLoads = OpenBucketLoadEwmaForOwner();
                if (bucketLoads.IsCreated)
                {
                    for (int i = 0; i < bucketLoads.Length; i++)
                        bucketLoads[i] = 0f;
                }

                NativeArray<float> rebalanceLoads = OpenRebalanceBucketLoadsForOwner();
                if (rebalanceLoads.IsCreated)
                {
                    for (int i = 0; i < rebalanceLoads.Length; i++)
                        rebalanceLoads[i] = 0f;
                }

                NativeArray<SimulationBucketRebalanceResult> result = OpenRebalanceResultForOwner();
                if (result.IsCreated && result.Length > 0)
                    result[0] = default;

                NativeArray<SimulationBucketBlackBoxEntry> blackBox = OpenBlackBoxBufferForOwner();
                if (blackBox.IsCreated)
                {
                    for (int i = 0; i < blackBox.Length; i++)
                        blackBox[i] = default;
                }
            }
            finally
            {
                ReleaseVaultMutationGuard(guardVault, EntityStateVaultMutationGuardMask);
            }
        }

        private void ScheduleRebalanceIfDue(int cadenceFrames)
        {
            if (_rebalancePending)
                return;

            _rebalanceCountdown--;
            if (_rebalanceCountdown > 0)
                return;

            if (!TryAcquireRebalanceVaultGuard())
                return;

            NativeArray<float> costs = OpenEntityCostEwmaForOwner();
            NativeArray<int> work = OpenEntityBucketsWorkForOwner();
            NativeArray<float> bucketLoads = OpenRebalanceBucketLoadsForOwner();
            NativeArray<SimulationBucketRebalanceResult> result = OpenRebalanceResultForOwner();
            if (!costs.IsCreated ||
                costs.Length <= 0 ||
                !work.IsCreated ||
                work.Length <= 0 ||
                !bucketLoads.IsCreated ||
                bucketLoads.Length <= 0 ||
                !result.IsCreated ||
                result.Length < RebalanceResultLength)
            {
                ReleaseRebalanceVaultGuard();
                return;
            }

            _rebalanceCountdown = math.max(1, cadenceFrames);
            LoadBalancingJob job = new LoadBalancingJob
            {
                EntityCostsMs = costs,
                EntityBucketsWork = work,
                BucketLoadsMs = bucketLoads,
                Result = result,
                EntityCount = costs.Length,
                BucketCount = _slowBucketCount,
                DefaultCostMs = DefaultEntityCostMs,
                CostClampMs = CatastrophicCostClampMs,
                TargetFrameMs = SimulationBucketConstants.TargetFrameMilliseconds
            };

            bool scheduled = false;
            try
            {
                _rebalanceMutationVersion = _mutationVersion;
                _rebalanceHandle = job.Schedule();
                H8Memory.RegisterActiveJob(SystemID.SimulationBucketer, _rebalanceHandle);
                _rebalancePending = true;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseRebalanceVaultGuard();
            }
        }

        private void CompleteRebalanceIfReady()
        {
            if (!_rebalancePending || !_rebalanceHandle.IsCompleted)
                return;

            if (!TryFinalizeRebalanceHandle(ref _rebalanceHandle))
                return;

            _rebalancePending = false;

            try
            {
                if (_rebalanceMutationVersion == _mutationVersion)
                {
                    bool frontLocked = false;
                    try
                    {
                        frontLocked = TryAcquireWriteView(in _entityBucketsHandle, out NativeArray<int> front);
                        NativeArray<int> work = OpenEntityBucketsWorkForOwner();
                        if (frontLocked && front.IsCreated && work.IsCreated)
                        {
                            int count = math.min(front.Length, work.Length);
                            for (int i = 0; i < count; i++)
                                front[i] = work[i];
                        }
                    }
                    finally
                    {
                        if (frontLocked)
                            ReleaseWriteView(in _entityBucketsHandle);
                    }
                }

                NativeArray<SimulationBucketRebalanceResult> resultBuffer = OpenRebalanceResultForOwner();
                if (resultBuffer.IsCreated && resultBuffer.Length > 0)
                {
                    SimulationBucketRebalanceResult result = resultBuffer[0];
                    _expectedMaxBucketLoadMs = SanitizeCost(result.MaxBucketLoadMs);
                    _expectedMeanBucketLoadMs = SanitizeCost(result.MeanBucketLoadMs);
                    if (!math.isfinite(result.MaxBucketLoadMs) ||
                        !math.isfinite(result.MeanBucketLoadMs) ||
                        (result.FramePacingFlags & SimulationBucketPacingFlags.NonFiniteCost) != 0u)
                    {
                        _nonFiniteCostObserved = true;
                        _pendingBlackBoxDump = true;
                    }

                    _rebalanceSequence = _rebalanceSequence == uint.MaxValue ? 1u : _rebalanceSequence + 1u;
                }
            }
            finally
            {
                ReleaseRebalanceVaultGuard();
            }
        }

        private bool TryAcquireRebalanceVaultGuard()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            ReleaseRebalanceVaultGuard();
            _rebalanceVaultGuardHeld = vault.TryAcquireMutationGuard(RebalanceVaultMutationGuardMask);
            _rebalanceVaultGuardVault = _rebalanceVaultGuardHeld ? vault : null;
            return _rebalanceVaultGuardHeld;
        }

        private bool TryAcquireVaultMutationGuard(ulong mask, out IDataVault guardVault)
        {
            guardVault = null;
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(mask))
                return false;

            guardVault = vault;
            return true;
        }

        private static void ReleaseVaultMutationGuard(IDataVault vault, ulong mask)
        {
            if (vault != null)
                vault.ReleaseMutationGuard(mask);
        }

        private void ReleaseRebalanceVaultGuard()
        {
            if (!_rebalanceVaultGuardHeld)
            {
                _rebalanceVaultGuardVault = null;
                return;
            }

            IDataVault vault = _rebalanceVaultGuardVault;
            _rebalanceVaultGuardVault = null;
            _rebalanceVaultGuardHeld = false;
            if (vault == null)
            {
                _pendingBlackBoxDump = true;
                return;
            }

            vault.ReleaseMutationGuard(RebalanceVaultMutationGuardMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CompleteRebalanceHandle(ref JobHandle handle)
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryFinalizeRebalanceHandle(ref JobHandle handle)
        {
            return DispatcherJobFence.TryFinalizeCompleted(ref handle);
        }

        private void UpdatePacingFlags()
        {
            uint flags = 0u;
            if (_activeSlowBucketCount <= SimulationBucketConstants.MinimumActiveSlowBucketCount)
                flags |= SimulationBucketPacingFlags.SurvivalStaticDistribution;
            if (_rebalancePending)
                flags |= SimulationBucketPacingFlags.RebalancePending;
            if (_nonFiniteCostObserved)
                flags |= SimulationBucketPacingFlags.NonFiniteCost;
            if (_preSimulationCostMs > SimulationBucketConstants.PreSimulationBudgetMilliseconds)
                flags |= SimulationBucketPacingFlags.PreSimulationOverBudget;

            float expectedMaxBucketLoadMs = SanitizePacingCost(_expectedMaxBucketLoadMs, ref flags);
            float lastActiveBucketLoadMs = SanitizePacingCost(_lastActiveBucketLoadMs, ref flags);
            float preSimulationCostMs = SanitizePacingCost(_preSimulationCostMs, ref flags);
            float expectedFrameMs = math.max(expectedMaxBucketLoadMs, lastActiveBucketLoadMs) + preSimulationCostMs;
            if (!math.isfinite(expectedFrameMs))
            {
                flags |= SimulationBucketPacingFlags.NonFiniteCost;
                _nonFiniteCostObserved = true;
                _pendingBlackBoxDump = true;
                expectedFrameMs = CatastrophicCostClampMs;
            }

            float precisionCurve01 = SmoothStep01(_qualityWeight01);
            float visualHeadroomThresholdMs = math.lerp(
                SimulationBucketConstants.TargetFrameMilliseconds * 0.35f,
                SimulationBucketConstants.TargetFrameMilliseconds * 0.5f,
                precisionCurve01);
            if (expectedFrameMs > SimulationBucketConstants.TargetFrameMilliseconds)
            {
                flags |= SimulationBucketPacingFlags.Impossible60Fps;
                flags |= SimulationBucketPacingFlags.HomeostasisKillRequested;
            }
            else if (_activeSlowBucketCount > SimulationBucketConstants.MinimumActiveSlowBucketCount &&
                     !_rebalancePending &&
                     !_nonFiniteCostObserved &&
                     expectedFrameMs > 0f &&
                     expectedFrameMs <= visualHeadroomThresholdMs)
            {
                flags |= SimulationBucketPacingFlags.VisualOverkillBudgetAvailable;
            }

            _framePacingFlags = flags;
        }

        private void WriteBlackBoxEntry()
        {
            bool blackBoxLocked = false;
            try
            {
                blackBoxLocked = TryAcquireWriteView(in _blackBoxHandle, out NativeArray<SimulationBucketBlackBoxEntry> blackBox);
                if (!blackBoxLocked || !blackBox.IsCreated || blackBox.Length < BlackBoxFrameCount)
                    return;

                int writeIndex = _blackBoxCursor;
                bool overwriteCurrentFrame = _lastBlackBoxFrame == _currentFrameCount;
                if (overwriteCurrentFrame)
                    writeIndex = _blackBoxCursor == 0 ? BlackBoxFrameCount - 1 : _blackBoxCursor - 1;

                blackBox[writeIndex] = new SimulationBucketBlackBoxEntry
                {
                    CurrentFrameCount = _currentFrameCount,
                    ActiveFastBucket = _activeFastBucket,
                    ActiveSlowBucket = _activeSlowBucket,
                    ActiveColdBucket = _activeColdBucket,
                    SlowBucketCount = _slowBucketCount,
                    CriticalDebtFrames = _criticalDebtFrames,
                    FramePacingFlags = _framePacingFlags,
                    RebalanceSequence = _rebalanceSequence,
                    ActiveBucketLoadMs = _lastActiveBucketLoadMs,
                    JitterVarianceMs = _jitterVarianceMs,
                    ExpectedMaxBucketLoadMs = _expectedMaxBucketLoadMs,
                    ExpectedMeanBucketLoadMs = _expectedMeanBucketLoadMs,
                    PreSimulationCostMs = _preSimulationCostMs,
                    SimulationBucketInterpolationAlpha = _simulationBucketInterpolationAlpha,
                    ActiveSlowBucketCount = _activeSlowBucketCount,
                    AupBarrierActive = _aupBarrierActive ? (byte)1 : (byte)0,
                    ReservedPadding = 0,
                    StateHash = ComputeBlackBoxStateHash()
                };

                if (!overwriteCurrentFrame)
                {
                    writeIndex++;
                    if (writeIndex >= BlackBoxFrameCount)
                        writeIndex = 0;

                    _blackBoxCursor = writeIndex;
                    _lastBlackBoxFrame = _currentFrameCount;
                }

                TryDumpBlackBoxIfRequested(blackBox);
            }
            finally
            {
                if (blackBoxLocked)
                    ReleaseWriteView(in _blackBoxHandle);
            }
        }

        private uint ComputeBlackBoxStateHash()
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)_currentFrameCount) * 16777619u;
                hash = (hash ^ (uint)_activeFastBucket) * 16777619u;
                hash = (hash ^ (uint)_activeSlowBucket) * 16777619u;
                hash = (hash ^ (uint)_activeColdBucket) * 16777619u;
                hash = (hash ^ _framePacingFlags) * 16777619u;
                hash = (hash ^ math.asuint(_qualityWeight01)) * 16777619u;
                hash = (hash ^ math.asuint(_survivalDistributionPressure01)) * 16777619u;
                hash = (hash ^ math.asuint(_lastActiveBucketLoadMs)) * 16777619u;
                hash = (hash ^ math.asuint(_jitterVarianceMs)) * 16777619u;
                return hash;
            }
        }

        private void TryDumpBlackBoxIfRequested(NativeArray<SimulationBucketBlackBoxEntry> blackBox)
        {
            if (!_pendingBlackBoxDump)
                return;

            _pendingBlackBoxDump = false;
            try
            {
                string folder = Path.GetDirectoryName(BlackBoxDumpPath);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(BlackBoxDumpMagic);
                    writer.Write(BlackBoxDumpVersion);
                    writer.Write(BlackBoxFrameCount);
                    writer.Write(BlackBoxEntrySizeBytes);
                    writer.Write(_blackBoxCursor);
                    writer.Write(_currentFrameCount);
                    writer.Write(_rebalanceSequence);
                    for (int i = 0; i < BlackBoxFrameCount; i++)
                    {
                        int index = _blackBoxCursor + i;
                        if (index >= BlackBoxFrameCount)
                            index -= BlackBoxFrameCount;

                        SimulationBucketBlackBoxEntry entry = blackBox[index];
                        writer.Write(entry.CurrentFrameCount);
                        writer.Write(entry.ActiveFastBucket);
                        writer.Write(entry.ActiveSlowBucket);
                        writer.Write(entry.ActiveColdBucket);
                        writer.Write(entry.SlowBucketCount);
                        writer.Write(entry.CriticalDebtFrames);
                        writer.Write(entry.FramePacingFlags);
                        writer.Write(entry.RebalanceSequence);
                        writer.Write(entry.ActiveBucketLoadMs);
                        writer.Write(entry.JitterVarianceMs);
                        writer.Write(entry.ExpectedMaxBucketLoadMs);
                        writer.Write(entry.ExpectedMeanBucketLoadMs);
                        writer.Write(entry.PreSimulationCostMs);
                        writer.Write(entry.SimulationBucketInterpolationAlpha);
                        writer.Write(entry.ActiveSlowBucketCount);
                        writer.Write(entry.AupBarrierActive);
                        writer.Write(entry.ReservedPadding);
                        writer.Write(entry.StateHash);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void UpdateFrameStateBuffer()
        {
            bool frameStateLocked = false;
            try
            {
                frameStateLocked = TryAcquireWriteView(in _frameStateHandle, out NativeArray<SimulationBucketFrameState> frameState);
                if (frameStateLocked && frameState.IsCreated && frameState.Length > 0)
                    frameState[0] = CaptureFrameState();
            }
            finally
            {
                if (frameStateLocked)
                    ReleaseWriteView(in _frameStateHandle);
            }
        }

        private float ResolveGlobalInterpolationAlpha()
        {
            int groupCount = math.max(1, _slowBucketGroupMask + 1);
            return math.saturate((_activeSlowBucketGroup + 1) * math.rcp(groupCount));
        }

        private float SanitizePacingCost(float milliseconds, ref uint flags)
        {
            if (math.isfinite(milliseconds) && milliseconds >= 0f)
                return math.min(milliseconds, CatastrophicCostClampMs);

            flags |= SimulationBucketPacingFlags.NonFiniteCost;
            _nonFiniteCostObserved = true;
            _pendingBlackBoxDump = true;
            return 0f;
        }

        private float SanitizeCost(float milliseconds)
        {
            if (math.isfinite(milliseconds) && milliseconds >= 0f)
                return math.min(milliseconds, CatastrophicCostClampMs);

            _nonFiniteCostObserved = true;
            _pendingBlackBoxDump = true;
            return 0f;
        }

        private void ResetStateAfterAllocationFailure()
        {
            _currentFrameCount = 0;
            _lastActiveBucketLoadMs = 0f;
            _activeBucketLoadEwmaMs = 0f;
            _jitterVarianceMs = 0f;
            _expectedMaxBucketLoadMs = 0f;
            _expectedMeanBucketLoadMs = 0f;
            _preSimulationCostMs = 0f;
            _simulationBucketInterpolationAlpha = 0f;
            _criticalDebtFrames = 0;
            _aupBarrierActive = false;
            _framePacingFlags = 0u;
            _rebalanceSequence = 0u;
            _mutationVersion = 0;
            _rebalanceMutationVersion = 0;
            _blackBoxCursor = 0;
            _lastBlackBoxFrame = -1;
            _activeSlowBucketCount = SimulationBucketConstants.MinimumActiveSlowBucketCount;
            _slowBucketCount = SimulationBucketConstants.SurvivalSlowBucketCount;
            _slowBucketMask = SimulationBucketConstants.SurvivalSlowBucketMask;
            _slowBucketGroupMask = SimulationBucketConstants.SurvivalSlowBucketMask;
            _activeSlowBucketGroup = 0;
            _activeSlowBucketShift = 0;
            _activeFastBucket = 0;
            _activeSlowBucket = 0;
            _activeColdBucket = 0;
            _rebalanceCountdown = SimulationBucketConstants.RebalanceCadenceFrames;
            _qualityWeight01 = 1f;
            _survivalDistributionPressure01 = 0f;
            _nonFiniteCostObserved = false;
            _pendingBlackBoxDump = false;
        }

        private static byte ResolveActiveSlowBucketCount(float qualityWeight01, float unscaledDeltaTime, int criticalDebtFrames, bool aupBarrierActive, int frameCount)
        {
            if (aupBarrierActive || criticalDebtFrames > 0 || !math.isfinite(unscaledDeltaTime) || unscaledDeltaTime <= 0f)
                return SimulationBucketConstants.MinimumActiveSlowBucketCount;

            float curve = SmoothStep01(SanitizeQualityWeight01(qualityWeight01));
            float targetExponent = math.lerp(0f, 2f, curve);
            int lowerExponent = (int)math.floor(targetExponent);
            float fractional = targetExponent - lowerExponent;
            uint phase = DeterministicFramePhase01(frameCount);
            int exponent = lowerExponent + math.select(0, 1, phase < (uint)math.round(fractional * 65535f));
            int activeCount = 1 << math.clamp(exponent, 0, 2);
            return (byte)math.clamp(
                activeCount,
                (int)SimulationBucketConstants.MinimumActiveSlowBucketCount,
                (int)SimulationBucketConstants.PrecisionActiveSlowBucketCount);
        }

        private static int ResolveActiveSlowBucketShift(byte activeSlowBucketCount)
        {
            int clampedCount = math.clamp(
                (int)activeSlowBucketCount,
                (int)SimulationBucketConstants.MinimumActiveSlowBucketCount,
                (int)SimulationBucketConstants.PrecisionActiveSlowBucketCount);
            int shift = 0;
            int capacity = 1;
            while (capacity < clampedCount)
            {
                capacity <<= 1;
                shift++;
            }

            return shift;
        }

        private static int ResolveRebalanceCadenceFrames(float qualityWeight01)
        {
            float curve = SmoothStep01(SanitizeQualityWeight01(qualityWeight01));
            return math.max(
                1,
                (int)math.round(math.lerp(
                    SimulationBucketConstants.RebalanceCadenceFrames * 4f,
                    SimulationBucketConstants.RebalanceCadenceFrames,
                    curve)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint DeterministicFramePhase01(int frameCount)
        {
            unchecked
            {
                uint hash = (uint)frameCount;
                hash ^= 0x9E3779B9u;
                hash *= 0x85EBCA6Bu;
                hash ^= hash >> 16;
                return hash & 0xFFFFu;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeQualityWeight01(float qualityWeight01)
        {
            return math.isfinite(qualityWeight01) ? math.saturate(qualityWeight01) : 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        internal struct SimulationBucketRebalanceResult
        {
            [FieldOffset(0)] public float MaxBucketLoadMs;
            [FieldOffset(4)] public float MeanBucketLoadMs;
            [FieldOffset(8)] public float TotalLoadMs;
            [FieldOffset(12)] public uint FramePacingFlags;
            [FieldOffset(16)] public int ActiveEntityCount;
            [FieldOffset(20)] private uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = BlackBoxEntrySizeBytes)]
        internal struct SimulationBucketBlackBoxEntry
        {
            [FieldOffset(0)] public int CurrentFrameCount;
            [FieldOffset(4)] public int ActiveFastBucket;
            [FieldOffset(8)] public int ActiveSlowBucket;
            [FieldOffset(12)] public int ActiveColdBucket;
            [FieldOffset(16)] public int SlowBucketCount;
            [FieldOffset(20)] public int CriticalDebtFrames;
            [FieldOffset(24)] public uint FramePacingFlags;
            [FieldOffset(28)] public uint RebalanceSequence;
            [FieldOffset(32)] public float ActiveBucketLoadMs;
            [FieldOffset(36)] public float JitterVarianceMs;
            [FieldOffset(40)] public float ExpectedMaxBucketLoadMs;
            [FieldOffset(44)] public float ExpectedMeanBucketLoadMs;
            [FieldOffset(48)] public float PreSimulationCostMs;
            [FieldOffset(52)] public float SimulationBucketInterpolationAlpha;
            [FieldOffset(56)] public byte ActiveSlowBucketCount;
            [FieldOffset(57)] public byte AupBarrierActive;
            [FieldOffset(58)] public ushort ReservedPadding;
            [FieldOffset(60)] public uint StateHash;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct LoadBalancingJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<float> EntityCostsMs;
            [NoAlias] public NativeArray<int> EntityBucketsWork;
            [NoAlias] public NativeArray<float> BucketLoadsMs;
            [NoAlias] public NativeArray<SimulationBucketRebalanceResult> Result;
            public int EntityCount;
            public int BucketCount;
            public float DefaultCostMs;
            public float CostClampMs;
            public float TargetFrameMs;

            public void Execute()
            {
                uint flags = 0u;
                if (!EntityCostsMs.IsCreated || !EntityBucketsWork.IsCreated || !BucketLoadsMs.IsCreated)
                {
                    WriteResult(0f, 0f, 0f, SimulationBucketPacingFlags.NonFiniteCost, 0);
                    return;
                }

                int bucketCount = math.min(math.max(0, BucketCount), BucketLoadsMs.Length);
                int entityCount = math.max(0, math.min(EntityCount, math.min(EntityCostsMs.Length, EntityBucketsWork.Length)));
                if (bucketCount <= 0)
                {
                    for (int i = 0; i < EntityBucketsWork.Length; i++)
                        EntityBucketsWork[i] = -1;

                    WriteResult(0f, 0f, 0f, SimulationBucketPacingFlags.NonFiniteCost, 0);
                    return;
                }

                float defaultCostMs = math.isfinite(DefaultCostMs) && DefaultCostMs > 0f ? DefaultCostMs : 0.025f;
                float costClampMs = math.isfinite(CostClampMs) && CostClampMs > defaultCostMs ? CostClampMs : 1000f;
                float targetFrameMs = math.isfinite(TargetFrameMs) && TargetFrameMs > 0f
                    ? TargetFrameMs
                    : SimulationBucketConstants.TargetFrameMilliseconds;

                for (int bucket = 0; bucket < bucketCount; bucket++)
                    BucketLoadsMs[bucket] = 0f;

                for (int i = 0; i < EntityBucketsWork.Length; i++)
                    EntityBucketsWork[i] = -1;

                int activeEntityCount = 0;
                float totalLoadMs = 0f;
                for (int entityIndex = 0; entityIndex < entityCount; entityIndex++)
                {
                    float cost = EntityCostsMs[entityIndex];
                    if (!math.isfinite(cost) || cost < 0f)
                    {
                        flags |= SimulationBucketPacingFlags.NonFiniteCost;
                        cost = defaultCostMs;
                    }
                    else if (cost <= 0f)
                    {
                        continue;
                    }

                    int targetBucket = 0;
                    float targetLoad = BucketLoadsMs[0];
                    for (int bucket = 1; bucket < bucketCount; bucket++)
                    {
                        float load = BucketLoadsMs[bucket];
                        if (load >= targetLoad)
                            continue;

                        targetLoad = load;
                        targetBucket = bucket;
                    }

                    float sanitizedCost = math.min(math.max(defaultCostMs, cost), costClampMs);
                    float nextLoad = targetLoad + sanitizedCost;
                    if (!math.isfinite(nextLoad))
                    {
                        flags |= SimulationBucketPacingFlags.NonFiniteCost;
                        nextLoad = costClampMs;
                    }

                    EntityBucketsWork[entityIndex] = targetBucket;
                    BucketLoadsMs[targetBucket] = nextLoad;
                    totalLoadMs = math.min(totalLoadMs + sanitizedCost, costClampMs * bucketCount);
                    activeEntityCount++;
                }

                float maxLoadMs = 0f;
                for (int bucket = 0; bucket < bucketCount; bucket++)
                {
                    float load = BucketLoadsMs[bucket];
                    if (!math.isfinite(load))
                    {
                        flags |= SimulationBucketPacingFlags.NonFiniteCost;
                        load = 0f;
                    }

                    if (load > maxLoadMs)
                        maxLoadMs = load;
                }

                float meanLoadMs = totalLoadMs * math.rcp(bucketCount);
                if (maxLoadMs > targetFrameMs)
                    flags |= SimulationBucketPacingFlags.Impossible60Fps;

                WriteResult(maxLoadMs, meanLoadMs, totalLoadMs, flags, activeEntityCount);
            }

            private void WriteResult(float maxLoadMs, float meanLoadMs, float totalLoadMs, uint flags, int activeEntityCount)
            {
                if (Result.IsCreated && Result.Length > 0)
                {
                    Result[0] = new SimulationBucketRebalanceResult
                    {
                        MaxBucketLoadMs = maxLoadMs,
                        MeanBucketLoadMs = meanLoadMs,
                        TotalLoadMs = totalLoadMs,
                        FramePacingFlags = flags,
                        ActiveEntityCount = activeEntityCount
                    };
                }
            }
        }
    }
}
