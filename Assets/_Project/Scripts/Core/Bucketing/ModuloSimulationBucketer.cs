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
        private const float DefaultEntityCostMs = 0.025f;
        private const float EwmaWeight = 0.10f;

        private VaultBufferHandle<int> _entityBucketsHandle;
        private VaultBufferHandle<int> _entityBucketsWorkHandle;
        private VaultBufferHandle<float> _entityCostEwmaHandle;
        private VaultBufferHandle<float> _bucketLoadEwmaHandle;
        private VaultBufferHandle<float> _rebalanceBucketLoadsHandle;
        private VaultBufferHandle<SimulationBucketRebalanceResult> _rebalanceResultHandle;
        private VaultBufferHandle<SimulationBucketFrameState> _frameStateHandle;
        private IDataVault _dataVault;
        private JobHandle _rebalanceHandle;
        private int _entityCapacity;
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
        private bool _rebalancePending;
        private bool _nonFiniteCostObserved;

        public bool IsInitialized => ResolveEntityBuckets().IsCreated;

        public NativeArray<int>.ReadOnly EntityBuckets
        {
            get
            {
                NativeArray<int> entityBuckets = ResolveEntityBuckets();
                return entityBuckets.IsCreated ? entityBuckets.AsReadOnly() : default;
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
            Initialize(entityCapacity, GlobalRegistry.DataVault);
        }

        /// <summary>
        /// Resolves all persistent tables from the bootstrap-owned vault.
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

            if (ReferenceEquals(_dataVault, dataVault) && _entityCapacity == capacity && ResolveEntityBuckets().IsCreated)
                return;

            ReleaseHandlesOnly();
            _dataVault = dataVault;
            _entityCapacity = capacity;
            _entityMask = capacity - 1;

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

        public void AdvanceFrame(byte scalabilityTierProfile, float unscaledDeltaTime, int criticalDebtFrames, bool aupBarrierActive)
        {
            if (!IsInitialized)
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
                _rebalanceCountdown = SimulationBucketConstants.RebalanceCadenceFrames;
            else
                ScheduleRebalanceIfDue();

            UpdatePacingFlags(lowTier);
            UpdateFrameStateBuffer();
        }

        public void ReportActiveBucketLoadMs(float milliseconds)
        {
            float sanitized = SanitizeCost(milliseconds);
            _lastActiveBucketLoadMs = sanitized;
            float previous = _activeBucketLoadEwmaMs > 0f ? _activeBucketLoadEwmaMs : sanitized;
            _activeBucketLoadEwmaMs = math.lerp(previous, sanitized, EwmaWeight);
            _jitterVarianceMs = math.lerp(_jitterVarianceMs, math.abs(sanitized - previous), EwmaWeight);

            NativeArray<float> bucketLoads = ResolveBucketLoadEwma();
            if (bucketLoads.IsCreated && (uint)_activeSlowBucket < (uint)bucketLoads.Length)
            {
                float current = bucketLoads[_activeSlowBucket];
                float seed = current > 0f && math.isfinite(current) ? current : sanitized;
                bucketLoads[_activeSlowBucket] = math.lerp(seed, sanitized, EwmaWeight);
            }

            UpdatePacingFlags(_slowBucketCount == SimulationBucketConstants.LowSlowBucketCount);
            UpdateFrameStateBuffer();
        }

        public void ReportPreSimulationCostMs(float milliseconds)
        {
            _preSimulationCostMs = SanitizeCost(milliseconds);
            UpdatePacingFlags(_slowBucketCount == SimulationBucketConstants.LowSlowBucketCount);
            UpdateFrameStateBuffer();
        }

        public bool TryReportEntityCostMs(int entityIndex, float measuredCostMs)
        {
            NativeArray<float> costs = ResolveEntityCostEwma();
            if (!costs.IsCreated || (uint)entityIndex >= (uint)costs.Length)
                return false;

            float sanitized = SanitizeCost(measuredCostMs);
            float current = costs[entityIndex];
            float seed = current > 0f && math.isfinite(current) ? current : sanitized;
            costs[entityIndex] = math.lerp(seed, sanitized, EwmaWeight);
            return true;
        }

        public int ResolveEntityIndex(uint stableHash)
        {
            return IsInitialized ? (int)(stableHash & unchecked((uint)_entityMask)) : -1;
        }

        public bool TryRegisterEntityBucket(int entityIndex, uint stableHash)
        {
            NativeArray<int> entityBuckets = ResolveEntityBuckets();
            if (!entityBuckets.IsCreated || (uint)entityIndex >= (uint)entityBuckets.Length)
                return false;

            int bucket = ResolveSlowBucket(stableHash);
            entityBuckets[entityIndex] = bucket;

            NativeArray<int> work = ResolveEntityBucketsWork();
            if (!_rebalancePending && work.IsCreated && (uint)entityIndex < (uint)work.Length)
                work[entityIndex] = bucket;

            NativeArray<float> costs = ResolveEntityCostEwma();
            if (costs.IsCreated && (uint)entityIndex < (uint)costs.Length)
                costs[entityIndex] = DefaultEntityCostMs;

            _mutationVersion++;
            return true;
        }

        public bool TryUnregisterEntityBucket(int entityIndex)
        {
            NativeArray<int> entityBuckets = ResolveEntityBuckets();
            if (!entityBuckets.IsCreated || (uint)entityIndex >= (uint)entityBuckets.Length)
                return false;

            entityBuckets[entityIndex] = -1;

            NativeArray<int> work = ResolveEntityBucketsWork();
            if (!_rebalancePending && work.IsCreated && (uint)entityIndex < (uint)work.Length)
                work[entityIndex] = -1;

            NativeArray<float> costs = ResolveEntityCostEwma();
            if (costs.IsCreated && (uint)entityIndex < (uint)costs.Length)
                costs[entityIndex] = 0f;

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
                RebalanceSequence = _rebalanceSequence
            };
        }

        public void Dispose()
        {
            ReleaseHandlesOnly();
            ResetStateAfterAllocationFailure();
        }

        private bool HasRequiredVaultBuffers()
        {
            return ResolveEntityBuckets().IsCreated &&
                   ResolveEntityBucketsWork().IsCreated &&
                   ResolveEntityCostEwma().IsCreated &&
                   ResolveBucketLoadEwma().IsCreated &&
                   ResolveRebalanceBucketLoads().IsCreated &&
                   ResolveRebalanceResult().IsCreated &&
                   ResolveFrameStateBuffer().IsCreated;
        }

        private NativeArray<int> ResolveEntityBuckets()
        {
            return _entityBucketsHandle.IsCreated && _dataVault != null ? _entityBucketsHandle.Resolve(_dataVault) : default;
        }

        private NativeArray<int> ResolveEntityBucketsWork()
        {
            return _entityBucketsWorkHandle.IsCreated && _dataVault != null ? _entityBucketsWorkHandle.Resolve(_dataVault) : default;
        }

        private NativeArray<float> ResolveEntityCostEwma()
        {
            return _entityCostEwmaHandle.IsCreated && _dataVault != null ? _entityCostEwmaHandle.Resolve(_dataVault) : default;
        }

        private NativeArray<float> ResolveBucketLoadEwma()
        {
            return _bucketLoadEwmaHandle.IsCreated && _dataVault != null ? _bucketLoadEwmaHandle.Resolve(_dataVault) : default;
        }

        private NativeArray<float> ResolveRebalanceBucketLoads()
        {
            return _rebalanceBucketLoadsHandle.IsCreated && _dataVault != null ? _rebalanceBucketLoadsHandle.Resolve(_dataVault) : default;
        }

        private NativeArray<SimulationBucketRebalanceResult> ResolveRebalanceResult()
        {
            return _rebalanceResultHandle.IsCreated && _dataVault != null ? _rebalanceResultHandle.Resolve(_dataVault) : default;
        }

        private NativeArray<SimulationBucketFrameState> ResolveFrameStateBuffer()
        {
            return _frameStateHandle.IsCreated && _dataVault != null ? _frameStateHandle.Resolve(_dataVault) : default;
        }

        private void ReleaseHandlesOnly()
        {
            if (_rebalancePending)
            {
                _rebalanceHandle.Complete();
                _rebalancePending = false;
                _rebalanceHandle = default;
            }

            _entityBucketsHandle = default;
            _entityBucketsWorkHandle = default;
            _entityCostEwmaHandle = default;
            _bucketLoadEwmaHandle = default;
            _rebalanceBucketLoadsHandle = default;
            _rebalanceResultHandle = default;
            _frameStateHandle = default;
            _dataVault = null;
            _entityCapacity = 0;
            _entityMask = 0;
        }

        private void ClearEntityState()
        {
            NativeArray<int> entityBuckets = ResolveEntityBuckets();
            NativeArray<int> work = ResolveEntityBucketsWork();
            NativeArray<float> costs = ResolveEntityCostEwma();
            int entityCount = math.min(entityBuckets.Length, math.min(work.Length, costs.Length));
            for (int i = 0; i < entityCount; i++)
            {
                entityBuckets[i] = -1;
                work[i] = -1;
                costs[i] = 0f;
            }

            NativeArray<float> bucketLoads = ResolveBucketLoadEwma();
            NativeArray<float> rebalanceLoads = ResolveRebalanceBucketLoads();
            int loadCount = math.min(bucketLoads.Length, rebalanceLoads.Length);
            for (int i = 0; i < loadCount; i++)
            {
                bucketLoads[i] = 0f;
                rebalanceLoads[i] = 0f;
            }

            NativeArray<SimulationBucketRebalanceResult> result = ResolveRebalanceResult();
            if (result.IsCreated && result.Length > 0)
                result[0] = default;
        }

        private void ScheduleRebalanceIfDue()
        {
            if (_rebalancePending)
                return;

            _rebalanceCountdown--;
            if (_rebalanceCountdown > 0)
                return;

            NativeArray<float> costs = ResolveEntityCostEwma();
            NativeArray<int> work = ResolveEntityBucketsWork();
            NativeArray<float> bucketLoads = ResolveRebalanceBucketLoads();
            NativeArray<SimulationBucketRebalanceResult> result = ResolveRebalanceResult();
            if (!costs.IsCreated || !work.IsCreated || !bucketLoads.IsCreated || !result.IsCreated)
                return;

            _rebalanceCountdown = SimulationBucketConstants.RebalanceCadenceFrames;
            LoadBalancingJob job = new LoadBalancingJob
            {
                EntityCostsMs = costs,
                EntityBucketsWork = work,
                BucketLoadsMs = bucketLoads,
                Result = result,
                EntityCount = costs.Length,
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

            if (_rebalanceMutationVersion == _mutationVersion)
            {
                NativeArray<int> front = ResolveEntityBuckets();
                NativeArray<int> work = ResolveEntityBucketsWork();
                int count = math.min(front.Length, work.Length);
                for (int i = 0; i < count; i++)
                    front[i] = work[i];
            }

            NativeArray<SimulationBucketRebalanceResult> resultBuffer = ResolveRebalanceResult();
            if (resultBuffer.IsCreated && resultBuffer.Length > 0)
            {
                SimulationBucketRebalanceResult result = resultBuffer[0];
                _expectedMaxBucketLoadMs = math.isfinite(result.MaxBucketLoadMs) ? math.max(0f, result.MaxBucketLoadMs) : 0f;
                _expectedMeanBucketLoadMs = math.isfinite(result.MeanBucketLoadMs) ? math.max(0f, result.MeanBucketLoadMs) : 0f;
                if (!math.isfinite(result.MaxBucketLoadMs) || !math.isfinite(result.MeanBucketLoadMs))
                    _nonFiniteCostObserved = true;

                _rebalanceSequence = _rebalanceSequence == uint.MaxValue ? 1u : _rebalanceSequence + 1u;
            }
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
            NativeArray<SimulationBucketFrameState> frameState = ResolveFrameStateBuffer();
            if (frameState.IsCreated && frameState.Length > 0)
                frameState[0] = CaptureFrameState();
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
            _slowBucketCount = SimulationBucketConstants.StandardSlowBucketCount;
            _slowBucketMask = SimulationBucketConstants.StandardSlowBucketMask;
            _slowBucketGroupMask = SimulationBucketConstants.StandardSlowBucketMask;
            _activeSlowBucketGroup = 0;
            _activeSlowBucketShift = 0;
            _activeFastBucket = 0;
            _activeSlowBucket = 0;
            _activeColdBucket = 0;
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

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
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
                        cost = DefaultEntityCostMs;
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

                    float sanitizedCost = math.max(DefaultCostMs, cost);
                    EntityBucketsWork[entityIndex] = targetBucket & BucketMask;
                    BucketLoadsMs[targetBucket] = targetLoad + sanitizedCost;
                    totalLoadMs += sanitizedCost;
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
