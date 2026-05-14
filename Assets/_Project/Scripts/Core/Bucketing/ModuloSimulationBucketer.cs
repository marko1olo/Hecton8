using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Core.Bucketing
{
    /// <summary>
    /// Registry-owned modulo time-slicer using power-of-two masks instead of division/modulo in hot paths.
    /// </summary>
    public sealed class ModuloSimulationBucketer : ISimulationBucketer
    {
        private NativeArray<int> _entityBuckets;
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
        private byte _activeSlowBucketCount = SimulationBucketConstants.MinimumActiveSlowBucketCount;
        private float _lastActiveBucketLoadMs;
        private bool _aupBarrierActive;

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
        public bool AupBarrierActive => _aupBarrierActive;

        /// <inheritdoc />
        public void Initialize(int entityCapacity)
        {
            int capacity = SimulationBucketMath.RoundUpToPowerOfTwo(
                math.clamp(entityCapacity, 1, SimulationBucketConstants.MaxEntityCapacity));
            if (_entityBuckets.IsCreated)
            {
                if (_entityBuckets.Length == capacity)
                    return;

                H8Memory.Release(ref _entityBuckets, SystemID.SimulationBucketer);
            }

            _entityBuckets = H8Memory.Allocate<int>(
                capacity,
                SystemID.SimulationBucketer,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[capacity] - entity index to simulation bucket map - owner: ModuloSimulationBucketer
            if (!_entityBuckets.IsCreated)
            {
                _entityMask = 0;
                return;
            }

            _entityMask = capacity - 1;
            ClearEntityBuckets();
        }

        /// <inheritdoc />
        public void AdvanceFrame(byte scalabilityTierProfile, float unscaledDeltaTime, int criticalDebtFrames, bool aupBarrierActive)
        {
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
        }

        /// <inheritdoc />
        public void ReportActiveBucketLoadMs(float milliseconds)
        {
            _lastActiveBucketLoadMs = math.isfinite(milliseconds) ? math.max(0f, milliseconds) : 0f;
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

            _entityBuckets[entityIndex] = ResolveSlowBucket(stableHash);
            return true;
        }

        /// <inheritdoc />
        public bool TryUnregisterEntityBucket(int entityIndex)
        {
            if (!_entityBuckets.IsCreated || (uint)entityIndex >= (uint)_entityBuckets.Length)
                return false;

            _entityBuckets[entityIndex] = -1;
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
                ActiveBucketLoadMs = _lastActiveBucketLoadMs
            };
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_entityBuckets.IsCreated)
                H8Memory.Release(ref _entityBuckets, SystemID.SimulationBucketer);

            _entityMask = 0;
            _currentFrameCount = 0;
            _lastActiveBucketLoadMs = 0f;
            _criticalDebtFrames = 0;
            _aupBarrierActive = false;
            _activeSlowBucketCount = SimulationBucketConstants.MinimumActiveSlowBucketCount;
            _slowBucketGroupMask = SimulationBucketConstants.StandardSlowBucketMask;
            _activeSlowBucketGroup = 0;
            _activeSlowBucketShift = 0;
        }

        private void ClearEntityBuckets()
        {
            for (int i = 0; i < _entityBuckets.Length; i++)
                _entityBuckets[i] = -1;
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
    }
}
