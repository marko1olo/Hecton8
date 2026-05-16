using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Core
{
    /// <summary>
    /// Power-of-two cadence constants for modulo-free simulation bucketing.
    /// </summary>
    public static class SimulationBucketConstants
    {
        /// <summary>Fast bucket count used by voxel and other frame-distributed work.</summary>
        public const int FastBucketCount = 4;

        /// <summary>Mask for fast buckets.</summary>
        public const int FastBucketMask = FastBucketCount - 1;

        /// <summary>Prompt target of 60 slow buckets, rounded up for bitmask bucket math.</summary>
        public const int StandardSlowBucketCount = 64;

        /// <summary>Mask for standard slow buckets.</summary>
        public const int StandardSlowBucketMask = StandardSlowBucketCount - 1;

        /// <summary>Low/MX350 slow bucket count, rounded up from 120 to preserve bitmask bucket math.</summary>
        public const int LowSlowBucketCount = 128;

        /// <summary>Mask for low-tier slow buckets.</summary>
        public const int LowSlowBucketMask = LowSlowBucketCount - 1;

        /// <summary>Prompt target of 600 cold buckets, rounded down to the nearest power-of-two cadence.</summary>
        public const int ColdBucketCount = 512;

        /// <summary>Mask for cold buckets.</summary>
        public const int ColdBucketMask = ColdBucketCount - 1;

        /// <summary>Default registry capacity. Must remain a power of two.</summary>
        public const int DefaultEntityCapacity = 8192;

        /// <summary>Hard cap for entity bucket storage; 1,048,576 entries costs 4 MiB.</summary>
        public const int MaxEntityCapacity = 1 << 20;

        /// <summary>Maximum active slow buckets per frame on high-tier hardware with no admission debt.</summary>
        public const byte HighTierActiveSlowBucketCount = 2;

        /// <summary>Minimum active slow bucket count used during debt, AUP barriers, and low tier.</summary>
        public const byte MinimumActiveSlowBucketCount = 1;

        /// <summary>Target 60 FPS frame duration in milliseconds.</summary>
        public const float TargetFrameMilliseconds = 16.667f;

        /// <summary>PRE_SIMULATION hard budget in milliseconds.</summary>
        public const float PreSimulationBudgetMilliseconds = 1.5f;

        /// <summary>High-tier dynamic rebalance cadence in dispatcher frames.</summary>
        public const int RebalanceCadenceFrames = 60;
    }

    /// <summary>
    /// Bit flags describing current simulation-bucket frame-pacing pressure.
    /// </summary>
    public static class SimulationBucketPacingFlags
    {
        /// <summary>Frame cost exceeds the 60 FPS mathematical budget after bucketing.</summary>
        public const uint Impossible60Fps = 1u << 0;

        /// <summary>PRE_SIMULATION exceeded its 1.5 ms phase budget.</summary>
        public const uint PreSimulationOverBudget = 1u << 1;

        /// <summary>A non-finite measured cost was sanitized.</summary>
        public const uint NonFiniteCost = 1u << 2;

        /// <summary>A background rebalance job is pending and the current frame uses the last stable table.</summary>
        public const uint RebalancePending = 1u << 3;

        /// <summary>Low-tier static distribution is active.</summary>
        public const uint LowTierStaticDistribution = 1u << 4;

        /// <summary>The dispatcher requested homeostasis load shedding for this frame.</summary>
        public const uint HomeostasisKillRequested = 1u << 5;

        /// <summary>High-tier frame budget has room for downstream visual overkill systems.</summary>
        public const uint VisualOverkillBudgetAvailable = 1u << 6;
    }

    /// <summary>
    /// Lightweight frame snapshot emitted by the simulation bucketer and black-box telemetry.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct SimulationBucketFrameState
    {
        /// <summary>Monotonic frame count owned by the bucketer, independent of Unity frame wrapping.</summary>
        public int CurrentFrameCount;

        /// <summary>Active fast bucket for this dispatcher frame.</summary>
        public int ActiveFastBucket;

        /// <summary>Active slow bucket for this dispatcher frame.</summary>
        public int ActiveSlowBucket;

        /// <summary>Active cold bucket for this dispatcher frame.</summary>
        public int ActiveColdBucket;

        /// <summary>Current slow bucket count selected by scalability tier.</summary>
        public int SlowBucketCount;

        /// <summary>Slow bucket mask selected by scalability tier.</summary>
        public int SlowBucketMask;

        /// <summary>Current job admission critical debt frame count.</summary>
        public int CriticalDebtFrames;

        /// <summary>Frame-pacing flags from <see cref="SimulationBucketPacingFlags"/>.</summary>
        public uint FramePacingFlags;

        /// <summary>Accepted dynamic rebalance sequence number.</summary>
        public uint RebalanceSequence;

        /// <summary>Last measured active bucket workload in milliseconds.</summary>
        public float ActiveBucketLoadMs;

        /// <summary>EWMA absolute jitter deviation in milliseconds.</summary>
        public float JitterVarianceMs;

        /// <summary>Expected maximum bucket load after the latest accepted rebalance.</summary>
        public float ExpectedMaxBucketLoadMs;

        /// <summary>Expected mean bucket load after the latest accepted rebalance.</summary>
        public float ExpectedMeanBucketLoadMs;

        /// <summary>Dispatcher-measured PRE_SIMULATION phase cost in milliseconds.</summary>
        public float PreSimulationCostMs;

        /// <summary>Globally broadcast interpolation alpha for bucketed presentation.</summary>
        public float SimulationBucketInterpolationAlpha;

        /// <summary>Number of slow buckets permitted this frame.</summary>
        public byte ActiveSlowBucketCount;

        /// <summary>Non-zero when an AUP shift barrier is active.</summary>
        public byte AupBarrierActive;

        /// <summary>Explicit tail pad. Keeps Pack=1 contract at 64 bytes with no implicit platform padding.</summary>
        public ushort ReservedPadding;
    }

    /// <summary>
    /// Registry-published service that assigns entities to stable simulation buckets and exposes the active frame mask.
    /// </summary>
    public interface ISimulationBucketer : IDisposable
    {
        /// <summary>True after persistent native storage has been allocated.</summary>
        bool IsInitialized { get; }

        /// <summary>Read-only entity index to slow-bucket mapping.</summary>
        NativeArray<int>.ReadOnly EntityBuckets { get; }

        /// <summary>Entity registry capacity.</summary>
        int EntityCapacity { get; }

        /// <summary>Monotonic frame count owned by the bucketer.</summary>
        int CurrentFrameCount { get; }

        /// <summary>Power-of-two fast bucket count.</summary>
        int FastBucketCount { get; }

        /// <summary>Current power-of-two slow bucket count.</summary>
        int SlowBucketCount { get; }

        /// <summary>Power-of-two cold bucket count.</summary>
        int ColdBucketCount { get; }

        /// <summary>Fast bucket mask.</summary>
        int FastBucketMask { get; }

        /// <summary>Current slow bucket mask.</summary>
        int SlowBucketMask { get; }

        /// <summary>Cold bucket mask.</summary>
        int ColdBucketMask { get; }

        /// <summary>Active fast bucket for this frame.</summary>
        int ActiveFastBucket { get; }

        /// <summary>Active slow bucket for this frame.</summary>
        int ActiveSlowBucket { get; }

        /// <summary>Active cold bucket for this frame.</summary>
        int ActiveColdBucket { get; }

        /// <summary>Number of slow buckets allowed this frame after load balancing.</summary>
        byte ActiveSlowBucketCount { get; }

        /// <summary>Last measured active bucket workload in milliseconds.</summary>
        float LastActiveBucketLoadMs { get; }

        /// <summary>EWMA absolute jitter deviation in milliseconds.</summary>
        float JitterVarianceMs { get; }

        /// <summary>Expected maximum bucket load from the latest accepted rebalance.</summary>
        float ExpectedMaxBucketLoadMs { get; }

        /// <summary>Expected mean bucket load from the latest accepted rebalance.</summary>
        float ExpectedMeanBucketLoadMs { get; }

        /// <summary>Globally synchronized interpolation alpha for bucketed presentation.</summary>
        float SimulationBucketInterpolationAlpha { get; }

        /// <summary>Current frame pacing flags.</summary>
        uint FramePacingFlags { get; }

        /// <summary>True when AUP shift safety is holding staggered simulation to one slow bucket.</summary>
        bool AupBarrierActive { get; }

        /// <summary>Allocates persistent native storage for entity-to-bucket mapping.</summary>
        /// <param name="entityCapacity">Requested entity capacity. Rounded up to a power of two.</param>
        void Initialize(int entityCapacity);

        /// <summary>Advances active bucket state once in the dispatcher simulation phase.</summary>
        /// <param name="scalabilityTierProfile">Profile byte: 0 = Low/MX350, 1 = High/RTX.</param>
        /// <param name="unscaledDeltaTime">Unscaled dispatcher delta for non-finite guards.</param>
        /// <param name="criticalDebtFrames">Lane0 critical job admission debt.</param>
        /// <param name="aupBarrierActive">True while an AUP shift barrier may tear interpolated presentation.</param>
        void AdvanceFrame(byte scalabilityTierProfile, float unscaledDeltaTime, int criticalDebtFrames, bool aupBarrierActive);

        /// <summary>Stores the measured active bucket load for black-box telemetry.</summary>
        /// <param name="milliseconds">Measured milliseconds. Non-finite values are clamped to zero.</param>
        void ReportActiveBucketLoadMs(float milliseconds);

        /// <summary>Stores the measured PRE_SIMULATION cost for phase-lock warnings.</summary>
        /// <param name="milliseconds">Measured milliseconds. Non-finite values are clamped to zero.</param>
        void ReportPreSimulationCostMs(float milliseconds);

        /// <summary>Feeds an entity's measured work cost into the rebalance EWMA table.</summary>
        /// <param name="entityIndex">Entity registry index.</param>
        /// <param name="measuredCostMs">Measured cost in milliseconds.</param>
        /// <returns>True when the cost was accepted.</returns>
        bool TryReportEntityCostMs(int entityIndex, float measuredCostMs);

        /// <summary>Resolves the registry index for a stable hash.</summary>
        /// <param name="stableHash">Entity stable hash.</param>
        /// <returns>Power-of-two masked entity registry index.</returns>
        int ResolveEntityIndex(uint stableHash);

        /// <summary>Registers a stable entity hash into the entity bucket table.</summary>
        /// <param name="entityIndex">Entity registry index.</param>
        /// <param name="stableHash">Stable entity hash.</param>
        /// <returns>True when the entry was written.</returns>
        bool TryRegisterEntityBucket(int entityIndex, uint stableHash);

        /// <summary>Clears an entity bucket table entry.</summary>
        /// <param name="entityIndex">Entity registry index.</param>
        /// <returns>True when the entry was cleared.</returns>
        bool TryUnregisterEntityBucket(int entityIndex);

        /// <summary>Resolves a stable hash to a fast bucket.</summary>
        int ResolveFastBucket(uint stableHash);

        /// <summary>Resolves a stable hash to the current slow bucket domain.</summary>
        int ResolveSlowBucket(uint stableHash);

        /// <summary>Resolves a stable hash to a cold bucket.</summary>
        int ResolveColdBucket(uint stableHash);

        /// <summary>Returns true when a fast bucket is active this frame.</summary>
        bool IsFastBucketActive(int bucketId);

        /// <summary>Returns true when a slow bucket is active this frame.</summary>
        bool IsSlowBucketActive(int bucketId);

        /// <summary>Returns true when a cold bucket is active this frame.</summary>
        bool IsColdBucketActive(int bucketId);

        /// <summary>Returns normalized visual interpolation alpha since the bucket last ran.</summary>
        float ResolveSlowBucketInterpolationAlpha(int bucketId);

        /// <summary>Captures a blittable frame snapshot for telemetry and diagnostics.</summary>
        SimulationBucketFrameState CaptureFrameState();
    }

    /// <summary>
    /// Optional contract for slow-tick owners that can be directly gated by the dispatcher.
    /// </summary>
    public interface IBucketedSlowTickable
    {
        /// <summary>Stable slow bucket id for dispatcher gating.</summary>
        int SimulationBucketId { get; }

        /// <summary>Called by the dispatcher on frames where this owner's slow bucket is active.</summary>
        void SlowTick();
    }

    /// <summary>
    /// Allocation-free bucket math helpers shared by managed code and Burst-compatible jobs.
    /// </summary>
    public static class SimulationBucketMath
    {
        /// <summary>Resolves a hash into a power-of-two bucket domain.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveBucket(uint stableHash, int bucketMask)
        {
            return (int)(stableHash & unchecked((uint)bucketMask));
        }

        /// <summary>Returns true when a value is a valid power-of-two count.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        /// <summary>Rounds a positive value up to a power of two with a minimum of one.</summary>
        public static int RoundUpToPowerOfTwo(int value)
        {
            if (value <= 1)
                return 1;

            if (value >= 0x40000000)
                return 0x40000000;

            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        /// <summary>Returns wrapped distance from a bucket to the active bucket.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveWrappedDistance(int activeBucket, int bucketId, int bucketMask)
        {
            return (activeBucket - bucketId) & bucketMask;
        }
    }
}
