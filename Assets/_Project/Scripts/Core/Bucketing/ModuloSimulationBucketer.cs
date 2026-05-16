using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core.Bucketing
{
    /// <summary>
    /// Registry-owned master modulo time-slicer. SystemDispatcher owns frame advancement; this class only owns bucket state.
    /// </summary>
    public sealed class ModuloSimulationBucketer : ISimulationBucketer
    {
        private const int RebalanceResultLength = 1;
        private const int FrameStateLength = 1;
        private const float DefaultEntityCostMs = 0.025f;
        private const float EwmaWeight = 0.10f;

        private NativeArray<int> _entityBuckets;
        private NativeArray<int> _entityBucketsWork;
        private NativeArray<float> _entityCostEwmaMs;
        private NativeArray<float> _bucketLoadEwmaMs;
        private NativeArray<float> _rebalanceBucketLoadsMs;
        private NativeArray<SimulationBucketRebalanceResult> _rebalanceResult;
        private NativeArray<SimulationBucketFrameState> _frameStateBuffer;
        private VaultBufferHandle<int> _entityBucketsHandle;
        private VaultBufferHandle<int> _entityBucketsWorkHandle;
        private VaultBufferHandle<float> _entityCostEwmaHandle;
        private VaultBufferHandle<float> _bucketLoadEwmaHandle;
        private VaultBufferHandle<float> _rebalanceBucketLoadsHandle;
        private VaultBufferHandle<SimulationBucketRebalanceResult> _rebalanceResultHandle;
        private VaultBufferHandle<SimulationBucketFrameState> _frameStateHandle;
        private IDataVault _dataVault;
        private JobHandle _rebalanceHandle;
        private int _entityMask;
        private int _currentFrameCount;
        private int _slowBucketCount = SimulationBucketConstants.StandardSlowBucketCount;
        private int _slowBucketMask = SimulationBucketConstants.StandardSlowBucketMask;
        private int _slowBucketGroupMask = SimulationBucketConstants.StandardSlowBucketMask;
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
        private byte _activeSlowBucketCount = SimulationBucketConstants.MinimumActiveSlowBucketCount;
        private float _lastActiveBucketLoadMs;
        private float _activeBucketLoadEwmaMs;
        private float _jitterVarianceMs;
        private float _expectedMaxBucketLoadMs;
        private float _expectedMeanBucketLoadMs;
        private float _preSimulationCostMs;
        private float _simulationBucketInterpolationAlpha;
        private bool _aupBarrierActive;
        private bool _vaultOwned;
        private bool _rebalancePending;
        private bool _nonFiniteCostObserved;

        /// <inheritdoc />
        public bool IsInitialized => _entityBuckets.IsCreated;

        /// <inheritdoc />
        public NativeArray<int>.ReadOnly EntityBuckets => _entityBuckets.IsCreated ? _entityBuckets.AsReadOnly() : default;

        /// <inheritdoc />
        public int EntityCapacity => _entityBuckets.IsCreated ? _entityBuckets.Length : 0;

        /// <inheritdoc />
        public int CurrentFrameCount => _currentFrameCount;

        /// <inheritdoc />
        public int FastBucketCount => SimulationBucketConstants.FastBucketCount;

        /// <inheritdoc />
        public int SlowBucketCount => _slowBucketCount;

        /// <inheritdoc />
        public int ColdBucketCount => SimulationBucketConstants.ColdBucketCount;

        /// <inheritdoc />
        public int FastBucketMask => SimulationBucketConstants.FastBucketMask;

        /// <inheritdoc />
        public int SlowBucketMask => _slowBucketMask;

        /// <inheritdoc />
        public int ColdBucketMask => SimulationBucketConstants.ColdBucketMask;

        /// <inheritdoc />
        public int ActiveFastBucket => _activeFastBucket;

        /// <inheritdoc />
        public int ActiveSlowBucket => _activeSlowBucket;

        /// <inheritdoc />
        public int ActiveColdBucket => _activeColdBucket;

        /// <inheritdoc />
        public byte ActiveSlowBucketCount => _activeSlowBucketCount;

        /// <inheritdoc />
        public float LastActiveBucketLoadMs => _lastActiveBucketLoadMs;

        /// <inheritdoc />
        public float JitterVarianceMs => _jitterVarianceMs;

        /// <inheritdoc />
        public float ExpectedMaxBucketLoadMs => _expectedMaxBucketLoadMs;

        /// <inheritdoc />
        public float ExpectedMeanBucketLoadMs => _expectedMeanBucketLoadMs;

        /// <inheritdoc />
        public float SimulationBucketInterpolationAlpha => _simulationBucketInterpolationAlpha;

        /// <inheritdoc />
        public uint FramePacingFlags => _framePacingFlags;

        /// <inheritdoc />
        public bool AupBarrierActive => _aupBarrierActive;

        /// <inheritdoc />
        public void Initialize(int entityCapacity)
        {
            Initialize(entityCapacity, null);
        }

        /// <summary>
        /// Allocates or resolves bucket storage through the bootstrap-owned data vault.
        /// </summary>
        /// <param name="entityCapacity">Requested entity capacity. Rounded up to a power of two.</param>
        /// <param name="dataVault">Optional GlobalDataVault owner. Null uses H8Memory fallback storage.</param>
        public void Initialize(int entityCapacity, IDataVault dataVault)
        {
            int capacity = SimulationBucketMath.RoundUpToPowerOfTwo(
                math.clamp(entityCapacity, 1, SimulationBucketConstants.MaxEntityCapacity));
            bool sameVault = ReferenceEquals(_dataVault, dataVault);
            if (_entityBuckets.IsCreated && _entityBuckets.Length == capacity && sameVault)
                return;

            ReleaseBuffers();
            _dataVault = dataVault;

            if (dataVault != null && TryResolveVaultBuffers(dataVault, capacity))
            {
                _vaultOwned = true;
            }
            else
            {
                AllocateFallbackBuffers(capacity);
                _vaultOwned = false;
            }

            if (!_entityBuckets.IsCreated)
            {
                ResetStateAfterAllocationFailure();
                return;
            }

            _entityMask = capacity - 1;
            _rebalanceCountdown = SimulationBucketConstants.RebalanceCadenceFrames;
            ClearEntityState();
            UpdateFrameStateBuffer();
        }

        /// <inheritdoc />
        public void AdvanceFrame(byte scalabilityTierProfile, float unscaledDeltaTime, int criticalDebtFrames, bool aupBarrierActive)
        {
            if (!_entityBuckets.IsCreated)
                return;

            CompleteRebalanceIfReady();

            _currentFrameCount = _currentFrameCount == int.MaxValue ? 0 : _currentFrameCount + 1;
            bool lowTier = scalabilityTierProfile == 0;
            _slowBucketCount = lowTier
                ? SimulationBucketConstants.LowSlowBucketCount
                : SimulationBucketConstants.StandardSlowBucketCount;
            _slowBucketMask = _slowBucketCount - 1;
            _activeFastBucket = _currentFrameCount & SimulationBucketConstants.FastBucketMask;
            _activeColdBucket = _currentFrameCount & SimulationBucketConstants.ColdBucketMask;
            _criticalDebtFrames = math.max(0, criticalDebtFrames);
            _aupBarrierActive = aupBarrierActive;
            _activeSlowBucketCount = ResolveActiveSlowBucketCount(lowTier, unscaledDeltaTime, _criticalDebtFrames, aupBarrierActive);
            _activeSlowBucketShift = ResolveActiveSlowBucketShift(_activeSlowBucketCount);
            _slowBucketGroupMask = (_slowBucketCount >> _activeSlowBucketShift) - 1;
            _activeSlowBucketGroup = _currentFrameCount & _slowBucketGroupMask;
            _activeSlowBucket = (_activeSlowBucketGroup << _activeSlowBucketShift) & _slowBucketMask;
            _simulationBucketInterpolationAlpha = ResolveGlobalInterpolationAlpha();
            if (lowTier)
            {
                _rebalanceCountdown = SimulationBucketConstants.RebalanceCadenceFrames;
            }
            else
            {
                ScheduleRebalanceIfDue();
            }

            UpdatePacingFlags(lowTier);
            UpdateFrameStateBuffer();
        }

        /// <inheritdoc />
        public void ReportActiveBucketLoadMs(float milliseconds)
        {
            float sanitized = SanitizeCost(milliseconds);
            _lastActiveBucketLoadMs = sanitized;
            float previous = _activeBucketLoadEwmaMs > 0f ? _activeBucketLoadEwmaMs : sanitized;
            _activeBucketLoadEwmaMs = math.lerp(previous, sanitized, EwmaWeight);
            _jitterVarianceMs = math.lerp(_jitterVarianceMs, math.abs(sanitized - previous), EwmaWeight);

            if (_bucketLoadEwmaMs.IsCreated && (uint)_activeSlowBucket < (uint)_bucketLoadEwmaMs.Length)
            {
                float current = _bucketLoadEwmaMs[_activeSlowBucket];
                float seed = current > 0f && math.isfinite(current) ? current : sanitized;
                _bucketLoadEwmaMs[_activeSlowBucket] = math.lerp(seed, sanitized, EwmaWeight);
            }

            UpdatePacingFlags(_slowBucketCount == SimulationBucketConstants.LowSlowBucketCount);
            UpdateFrameStateBuffer();
        }

        /// <inheritdoc />
        public void ReportPreSimulationCostMs(float milliseconds)
        {
            _preSimulationCostMs = SanitizeCost(milliseconds);
            UpdatePacingFlags(_slowBucketCount == SimulationBucketConstants.LowSlowBucketCount);
            UpdateFrameStateBuffer();
        }

        /// <inheritdoc />
        public bool TryReportEntityCostMs(int entityIndex, float measuredCostMs)
        {
            if (!_entityCostEwmaMs.IsCreated || (uint)entityIndex >= (uint)_entityCostEwmaMs.Length)
                return false;

            float sanitized = SanitizeCost(measuredCostMs);
            float current = _entityCostEwmaMs[entityIndex];
            float seed = current > 0f && math.isfinite(current) ? current : sanitized;
            _entityCostEwmaMs[entityIndex] = math.lerp(seed, sanitized, EwmaWeight);
            return true;
        }

        /// <inheritdoc />
        public int ResolveEntityIndex(uint stableHash)
        {
            if (!_entityBuckets.IsCreated)
                return -1;

            return (int)(stableHash & unchecked((uint)_entityMask));
        }

        /// <inheritdoc />
        public bool TryRegisterEntityBucket(int entityIndex, uint stableHash)
        {
            if (!_entityBuckets.IsCreated || (uint)entityIndex >= (uint)_entityBuckets.Length)
                return false;

            int bucket = ResolveSlowBucket(stableHash);
            _entityBuckets[entityIndex] = bucket;
            if (!_rebalancePending && _entityBucketsWork.IsCreated)
                _entityBucketsWork[entityIndex] = bucket;
            if (_entityCostEwmaMs.IsCreated && (uint)entityIndex < (uint)_entityCostEwmaMs.Length)
                _entityCostEwmaMs[entityIndex] = DefaultEntityCostMs;

            _mutationVersion++;
            return true;
        }

        /// <inheritdoc />
        public bool TryUnregisterEntityBucket(int entityIndex)
        {
            if (!_entityBuckets.IsCreated || (uint)entityIndex >= (uint)_entityBuckets.Length)
                return false;

            _entityBuckets[entityIndex] = -1;
            if (!_rebalancePending && _entityBucketsWork.IsCreated)
                _entityBucketsWork[entityIndex] = -1;
            if (_entityCostEwmaMs.IsCreated && (uint)entityIndex < (uint)_entityCostEwmaMs.Length)
                _entityCostEwmaMs[entityIndex] = 0f;

            _mutationVersion++;
            return true;
        }

        /// <inheritdoc />
        public int ResolveFastBucket(uint stableHash)
        {
            return SimulationBucketMath.ResolveBucket(stableHash, SimulationBucketConstants.FastBucketMask);
        }

        /// <inheritdoc />
        public int ResolveSlowBucket(uint stableHash)
        {
            return SimulationBucketMath.ResolveBucket(stableHash, _slowBucketMask);
        }

        /// <inheritdoc />
        public int ResolveColdBucket(uint stableHash)
        {
            return SimulationBucketMath.ResolveBucket(stableHash, SimulationBucketConstants.ColdBucketMask);
        }

        /// <inheritdoc />
        public bool IsFastBucketActive(int bucketId)
        {
            return (bucketId & SimulationBucketConstants.FastBucketMask) == _activeFastBucket;
        }

        /// <inheritdoc />
        public bool IsSlowBucketActive(int bucketId)
        {
            int bucketGroup = (bucketId & _slowBucketMask) >> _activeSlowBucketShift;
            return bucketGroup == _activeSlowBucketGroup;
        }

        /// <inheritdoc />
        public bool IsColdBucketActive(int bucketId)
        {
            return (bucketId & SimulationBucketConstants.ColdBucketMask) == _activeColdBucket;
        }

        /// <inheritdoc />
        public float ResolveSlowBucketInterpolationAlpha(int bucketId)
        {
            int bucketGroup = (bucketId & _slowBucketMask) >> _activeSlowBucketShift;
            int distance = SimulationBucketMath.ResolveWrappedDistance(_activeSlowBucketGroup, bucketGroup, _slowBucketGroupMask);
            return math.saturate(distance * math.rcp(math.max(1, _slowBucketGroupMask)));
        }

        /// <inheritdoc />
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
                RebalanceSequence = _rebalanceSequence
            };
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ReleaseBuffers();
            ResetStateAfterAllocationFailure();
        }

        private bool TryResolveVaultBuffers(IDataVault dataVault, int capacity)
        {
            _entityBucketsHandle = dataVault.GetBufferHandle<int>(
                BufferID.SimulationBucketEntityFront,
                capacity,
                SystemID.SimulationBucketer,
                NativeArrayOptions.UninitializedMemory);
            _entityBucketsWorkHandle = dataVault.GetBufferHandle<int>(
                BufferID.SimulationBucketEntityWork,
                capacity,
                SystemID.SimulationBucketer,
                NativeArrayOptions.UninitializedMemory);
            _entityCostEwmaHandle = dataVault.GetBufferHandle<float>(
                BufferID.SimulationBucketEntityCostEwma,
                capacity,
                SystemID.SimulationBucketer,
                NativeArrayOptions.ClearMemory);
            _bucketLoadEwmaHandle = dataVault.GetBufferHandle<float>(
                BufferID.SimulationBucketLoadEwma,
                SimulationBucketConstants.LowSlowBucketCount,
                SystemID.SimulationBucketer,
                NativeArrayOptions.ClearMemory);
            _rebalanceBucketLoadsHandle = dataVault.GetBufferHandle<float>(
                BufferID.SimulationBucketRebalanceLoads,
                SimulationBucketConstants.LowSlowBucketCount,
                SystemID.SimulationBucketer,
                NativeArrayOptions.ClearMemory);
            _rebalanceResultHandle = dataVault.GetBufferHandle<SimulationBucketRebalanceResult>(
                BufferID.SimulationBucketRebalanceResult,
                RebalanceResultLength,
                SystemID.SimulationBucketer,
                NativeArrayOptions.ClearMemory);
            _frameStateHandle = dataVault.GetBufferHandle<SimulationBucketFrameState>(
                BufferID.SimulationBucketFrameState,
                FrameStateLength,
                SystemID.SimulationBucketer,
                NativeArrayOptions.ClearMemory);

            _entityBuckets = _entityBucketsHandle.Resolve(dataVault);
            _entityBucketsWork = _entityBucketsWorkHandle.Resolve(dataVault);
            _entityCostEwmaMs = _entityCostEwmaHandle.Resolve(dataVault);
            _bucketLoadEwmaMs = _bucketLoadEwmaHandle.Resolve(dataVault);
            _rebalanceBucketLoadsMs = _rebalanceBucketLoadsHandle.Resolve(dataVault);
            _rebalanceResult = _rebalanceResultHandle.Resolve(dataVault);
            _frameStateBuffer = _frameStateHandle.Resolve(dataVault);

            return _entityBuckets.IsCreated &&
                   _entityBucketsWork.IsCreated &&
                   _entityCostEwmaMs.IsCreated &&
                   _bucketLoadEwmaMs.IsCreated &&
                   _rebalanceBucketLoadsMs.IsCreated &&
                   _rebalanceResult.IsCreated &&
                   _frameStateBuffer.IsCreated;
        }

        private void AllocateFallbackBuffers(int capacity)
        {
            _entityBuckets = H8Memory.Allocate<int>(
                capacity,
                SystemID.SimulationBucketer,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[capacity] - front entity bucket map when DataVault is unavailable - owner: ModuloSimulationBucketer
            _entityBucketsWork = H8Memory.Allocate<int>(
                capacity,
                SystemID.SimulationBucketer,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[capacity] - work entity bucket map for rebalance job - owner: ModuloSimulationBucketer
            _entityCostEwmaMs = H8Memory.Allocate<float>(
                capacity,
                SystemID.SimulationBucketer,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[capacity] - entity cost EWMA table - owner: ModuloSimulationBucketer
            _bucketLoadEwmaMs = H8Memory.Allocate<float>(
                SimulationBucketConstants.LowSlowBucketCount,
                SystemID.SimulationBucketer,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[128] - bucket load EWMA table - owner: ModuloSimulationBucketer
            _rebalanceBucketLoadsMs = H8Memory.Allocate<float>(
                SimulationBucketConstants.LowSlowBucketCount,
                SystemID.SimulationBucketer,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[128] - rebalance scratch bucket loads - owner: ModuloSimulationBucketer
            _rebalanceResult = H8Memory.Allocate<SimulationBucketRebalanceResult>(
                RebalanceResultLength,
                SystemID.SimulationBucketer,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SimulationBucketRebalanceResult>[1] - rebalance scalar output - owner: ModuloSimulationBucketer
            _frameStateBuffer = H8Memory.Allocate<SimulationBucketFrameState>(
                FrameStateLength,
                SystemID.SimulationBucketer,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SimulationBucketFrameState>[1] - DataVault-compatible frame state fallback - owner: ModuloSimulationBucketer
        }

        private void ReleaseBuffers()
        {
            if (_rebalancePending)
            {
                _rebalanceHandle.Complete();
                _rebalancePending = false;
                _rebalanceHandle = default;
            }

            if (!_vaultOwned)
            {
                if (_entityBuckets.IsCreated)
                    H8Memory.Release(ref _entityBuckets, SystemID.SimulationBucketer);
                if (_entityBucketsWork.IsCreated)
                    H8Memory.Release(ref _entityBucketsWork, SystemID.SimulationBucketer);
                if (_entityCostEwmaMs.IsCreated)
                    H8Memory.Release(ref _entityCostEwmaMs, SystemID.SimulationBucketer);
                if (_bucketLoadEwmaMs.IsCreated)
                    H8Memory.Release(ref _bucketLoadEwmaMs, SystemID.SimulationBucketer);
                if (_rebalanceBucketLoadsMs.IsCreated)
                    H8Memory.Release(ref _rebalanceBucketLoadsMs, SystemID.SimulationBucketer);
                if (_rebalanceResult.IsCreated)
                    H8Memory.Release(ref _rebalanceResult, SystemID.SimulationBucketer);
                if (_frameStateBuffer.IsCreated)
                    H8Memory.Release(ref _frameStateBuffer, SystemID.SimulationBucketer);
            }

            _entityBuckets = default;
            _entityBucketsWork = default;
            _entityCostEwmaMs = default;
            _bucketLoadEwmaMs = default;
            _rebalanceBucketLoadsMs = default;
            _rebalanceResult = default;
            _frameStateBuffer = default;
            _entityBucketsHandle = default;
            _entityBucketsWorkHandle = default;
            _entityCostEwmaHandle = default;
            _bucketLoadEwmaHandle = default;
            _rebalanceBucketLoadsHandle = default;
            _rebalanceResultHandle = default;
            _frameStateHandle = default;
            _dataVault = null;
            _vaultOwned = false;
        }

        private void ClearEntityState()
        {
            for (int i = 0; i < _entityBuckets.Length; i++)
            {
                _entityBuckets[i] = -1;
                _entityBucketsWork[i] = -1;
                _entityCostEwmaMs[i] = 0f;
            }

            for (int i = 0; i < _bucketLoadEwmaMs.Length; i++)
            {
                _bucketLoadEwmaMs[i] = 0f;
                _rebalanceBucketLoadsMs[i] = 0f;
            }

            _rebalanceResult[0] = default;
        }

        private void ScheduleRebalanceIfDue()
        {
            if (_rebalancePending || !_entityBucketsWork.IsCreated)
                return;

            _rebalanceCountdown--;
            if (_rebalanceCountdown > 0)
                return;

            _rebalanceCountdown = SimulationBucketConstants.RebalanceCadenceFrames;
            LoadBalancingJob job = new LoadBalancingJob
            {
                EntityCostsMs = _entityCostEwmaMs,
                EntityBucketsWork = _entityBucketsWork,
                BucketLoadsMs = _rebalanceBucketLoadsMs,
                Result = _rebalanceResult,
                EntityCount = _entityBuckets.Length,
                BucketCount = _slowBucketCount,
                BucketMask = _slowBucketMask,
                DefaultCostMs = DefaultEntityCostMs,
                TargetFrameMs = SimulationBucketConstants.TargetFrameMilliseconds
            };

            _rebalanceMutationVersion = _mutationVersion;
            _rebalanceHandle = job.Schedule();
            H8Memory.RegisterActiveJob(SystemID.SimulationBucketer, _rebalanceHandle);
            _rebalancePending = true;
        }

        private void CompleteRebalanceIfReady()
        {
            if (!_rebalancePending || !_rebalanceHandle.IsCompleted)
                return;

            _rebalanceHandle.Complete();
            _rebalanceHandle = default;
            _rebalancePending = false;

            if (_rebalanceMutationVersion != _mutationVersion)
                return;

            NativeArray<int> previousFront = _entityBuckets;
            _entityBuckets = _entityBucketsWork;
            _entityBucketsWork = previousFront;

            SimulationBucketRebalanceResult result = _rebalanceResult[0];
            _expectedMaxBucketLoadMs = math.isfinite(result.MaxBucketLoadMs) ? math.max(0f, result.MaxBucketLoadMs) : 0f;
            _expectedMeanBucketLoadMs = math.isfinite(result.MeanBucketLoadMs) ? math.max(0f, result.MeanBucketLoadMs) : 0f;
            if (!math.isfinite(result.MaxBucketLoadMs) || !math.isfinite(result.MeanBucketLoadMs))
                _nonFiniteCostObserved = true;

            _rebalanceSequence = _rebalanceSequence == uint.MaxValue ? 1u : _rebalanceSequence + 1u;
        }

        private void UpdatePacingFlags(bool lowTier)
        {
            uint flags = 0u;
            if (lowTier)
                flags |= SimulationBucketPacingFlags.LowTierStaticDistribution;
            if (_rebalancePending)
                flags |= SimulationBucketPacingFlags.RebalancePending;
            if (_nonFiniteCostObserved)
                flags |= SimulationBucketPacingFlags.NonFiniteCost;
            if (_preSimulationCostMs > SimulationBucketConstants.PreSimulationBudgetMilliseconds)
                flags |= SimulationBucketPacingFlags.PreSimulationOverBudget;

            float expectedFrameMs = math.max(_expectedMaxBucketLoadMs, _lastActiveBucketLoadMs) + _preSimulationCostMs;
            if (expectedFrameMs > SimulationBucketConstants.TargetFrameMilliseconds)
            {
                flags |= SimulationBucketPacingFlags.Impossible60Fps;
                flags |= SimulationBucketPacingFlags.HomeostasisKillRequested;
            }

            _framePacingFlags = flags;
        }

        private void UpdateFrameStateBuffer()
        {
            if (!_frameStateBuffer.IsCreated)
                return;

            _frameStateBuffer[0] = CaptureFrameState();
        }

        private float ResolveGlobalInterpolationAlpha()
        {
            int groupCount = math.max(1, _slowBucketGroupMask + 1);
            return math.saturate((_activeSlowBucketGroup + 1) * math.rcp(groupCount));
        }

        private float SanitizeCost(float milliseconds)
        {
            if (math.isfinite(milliseconds) && milliseconds >= 0f)
                return milliseconds;

            _nonFiniteCostObserved = true;
            return 0f;
        }

        private void ResetStateAfterAllocationFailure()
        {
            _entityMask = 0;
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
            _activeSlowBucketCount = SimulationBucketConstants.MinimumActiveSlowBucketCount;
            _slowBucketGroupMask = SimulationBucketConstants.StandardSlowBucketMask;
            _activeSlowBucketGroup = 0;
            _activeSlowBucketShift = 0;
            _rebalanceCountdown = SimulationBucketConstants.RebalanceCadenceFrames;
            _nonFiniteCostObserved = false;
        }

        private static byte ResolveActiveSlowBucketCount(bool lowTier, float unscaledDeltaTime, int criticalDebtFrames, bool aupBarrierActive)
        {
            if (lowTier || aupBarrierActive || criticalDebtFrames > 0 || !math.isfinite(unscaledDeltaTime))
                return SimulationBucketConstants.MinimumActiveSlowBucketCount;

            return SimulationBucketConstants.HighTierActiveSlowBucketCount;
        }

        private static int ResolveActiveSlowBucketShift(byte activeSlowBucketCount)
        {
            return activeSlowBucketCount >= SimulationBucketConstants.HighTierActiveSlowBucketCount ? 1 : 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SimulationBucketRebalanceResult
        {
            public float MaxBucketLoadMs;
            public float MeanBucketLoadMs;
            public float TotalLoadMs;
            public uint FramePacingFlags;
            public int ActiveEntityCount;
        }

        [BurstCompile]
        internal struct LoadBalancingJob : IJob
        {
            [ReadOnly] public NativeArray<float> EntityCostsMs;
            public NativeArray<int> EntityBucketsWork;
            public NativeArray<float> BucketLoadsMs;
            public NativeArray<SimulationBucketRebalanceResult> Result;
            public int EntityCount;
            public int BucketCount;
            public int BucketMask;
            public float DefaultCostMs;
            public float TargetFrameMs;

            public void Execute()
            {
                int bucketCount = math.max(1, math.min(BucketCount, BucketLoadsMs.Length));
                int entityCount = math.max(0, math.min(EntityCount, EntityCostsMs.Length));
                for (int bucket = 0; bucket < bucketCount; bucket++)
                    BucketLoadsMs[bucket] = 0f;

                for (int i = 0; i < EntityBucketsWork.Length; i++)
                    EntityBucketsWork[i] = -1;

                int activeEntityCount = 0;
                float totalLoadMs = 0f;
                uint flags = 0u;
                for (int entityIndex = 0; entityIndex < entityCount; entityIndex++)
                {
                    float cost = EntityCostsMs[entityIndex];
                    if (cost <= 0f)
                        continue;

                    if (!math.isfinite(cost))
                    {
                        flags |= SimulationBucketPacingFlags.NonFiniteCost;
                        cost = DefaultCostMs;
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

                    EntityBucketsWork[entityIndex] = targetBucket & BucketMask;
                    BucketLoadsMs[targetBucket] = targetLoad + math.max(DefaultCostMs, cost);
                    totalLoadMs += math.max(DefaultCostMs, cost);
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

                float meanLoadMs = totalLoadMs * math.rcp(math.max(1, bucketCount));
                if (maxLoadMs > TargetFrameMs)
                    flags |= SimulationBucketPacingFlags.Impossible60Fps;

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
