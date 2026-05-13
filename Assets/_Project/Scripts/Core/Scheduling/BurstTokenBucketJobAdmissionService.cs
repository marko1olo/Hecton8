using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Core.Scheduling
{
    /// <summary>
    /// Fixed-size token bucket gate for Burst job admission. No dictionaries, no string keys, no per-frame allocation.
    /// </summary>
    public sealed class BurstTokenBucketJobAdmissionService : IJobAdmissionService
    {
        private const int LaneCount = JobAdmissionLanes.Count;
        private const int CostSlotCapacity = 256;
        private const int BlackboxCapacity = 300;
        private const int CriticalDebtKillFrames = 60;
        private const float DefaultEstimatedCostMs = 0.025f;
        private const float EwmaWeight = 0.10f;
        private const float LowTierBudgetScalar = 0.60f;
        private const float MissedFrameRefillScalar = 0.50f;
        private const float TargetFrameMilliseconds = 16.667f;
        private const float TargetFrameMillisecondsRcp = 0.0599988f;
        private const float MinDeltaRefillScale = 0.5f;
        private const float MaxDeltaRefillScale = 2.0f;
        private const float LaneDebtFloorMs = -4.0f;
        private const uint KillSwitchDisableVfxMask = 1u << JobAdmissionLanes.Lane4VFX;

        private static readonly float[] _defaultRefillBudgetsMs =
        {
            1.20f,
            0.90f,
            1.40f,
            0.80f,
            0.45f,
            0.60f
        };

        private NativeArray<float> _laneBudgetsMs;
        private NativeArray<float> _baseRefillMs;
        private NativeArray<uint> _jobHashes;
        private NativeArray<float> _ewmaCostsMs;
        private NativeArray<JobAdmissionBlackboxEntry> _blackbox;
        private IJobAdmissionTelemetrySink _telemetrySink;
        private int _costSlotCount;
        private int _blackboxCursor;
        private int _criticalDebtFrameCount;
        private uint _systemKillSwitchMask;
        private bool _initialized;
        private bool _aupBarrierActive;

        /// <inheritdoc />
        public bool IsInitialized => _initialized;

        /// <inheritdoc />
        public uint SystemKillSwitchMask => _systemKillSwitchMask;

        /// <inheritdoc />
        public int CriticalDebtFrameCount => _criticalDebtFrameCount;

        /// <inheritdoc />
        public bool AupBarrierActive => _aupBarrierActive;

        /// <inheritdoc />
        public void Initialize(IJobAdmissionTelemetrySink telemetrySink)
        {
            if (_initialized)
                return;

            _telemetrySink = telemetrySink;
            _laneBudgetsMs = new NativeArray<float>(LaneCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[6] - lane token buckets - owner: BurstTokenBucketJobAdmissionService
            _baseRefillMs = new NativeArray<float>(LaneCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float>[6] - lane base refills - owner: BurstTokenBucketJobAdmissionService
            _jobHashes = new NativeArray<uint>(CostSlotCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[256] - fixed EWMA hash table - owner: BurstTokenBucketJobAdmissionService
            _ewmaCostsMs = new NativeArray<float>(CostSlotCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[256] - fixed EWMA cost table - owner: BurstTokenBucketJobAdmissionService
            _blackbox = new NativeArray<JobAdmissionBlackboxEntry>(BlackboxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<JobAdmissionBlackboxEntry>[300] - admission blackbox ring - owner: BurstTokenBucketJobAdmissionService

            for (int i = 0; i < LaneCount; i++)
            {
                float refill = _defaultRefillBudgetsMs[i];
                _baseRefillMs[i] = refill;
                _laneBudgetsMs[i] = refill;
            }

            _initialized = true;
        }

        /// <inheritdoc />
        public void Refill(byte scalabilityTierProfile, float deltaTimeSeconds, bool previousFrameMissedBudget)
        {
            if (!_initialized)
                return;

            float deltaMilliseconds = deltaTimeSeconds * 1000f;
            if (!math.isfinite(deltaMilliseconds) || deltaMilliseconds <= 0f)
            {
                ReportNonFinite(JobAdmissionLane.Lane0_Critical, 0u, deltaMilliseconds);
                deltaMilliseconds = TargetFrameMilliseconds;
            }

            float deltaScale = math.clamp(deltaMilliseconds * TargetFrameMillisecondsRcp, MinDeltaRefillScale, MaxDeltaRefillScale);
            float tierScale = scalabilityTierProfile == 0 ? LowTierBudgetScalar : 1f;
            float missScale = previousFrameMissedBudget ? MissedFrameRefillScalar : 1f;
            float refillScale = deltaScale * tierScale * missScale;

            for (int lane = 0; lane < LaneCount; lane++)
            {
                float refill = _baseRefillMs[lane] * refillScale;
                if (!math.isfinite(refill))
                {
                    ReportNonFinite((JobAdmissionLane)lane, 0u, refill);
                    refill = 0f;
                }

                float current = _laneBudgetsMs[lane];
                float next = current + refill;
                float cap = _baseRefillMs[lane] * MaxDeltaRefillScale;
                _laneBudgetsMs[lane] = math.min(next, cap);
                _telemetrySink?.ReportLaneState((JobAdmissionLane)lane, _laneBudgetsMs[lane], refill, _criticalDebtFrameCount, _systemKillSwitchMask);
            }

            if (_laneBudgetsMs[JobAdmissionLanes.Lane0Critical] < 0f)
            {
                _criticalDebtFrameCount = math.min(_criticalDebtFrameCount + 1, int.MaxValue);
                if (_criticalDebtFrameCount >= CriticalDebtKillFrames)
                    _systemKillSwitchMask |= KillSwitchDisableVfxMask;
            }
            else
            {
                _criticalDebtFrameCount = 0;
            }
        }

        /// <inheritdoc />
        public bool TryAdmitJob(JobAdmissionLane lane, uint jobHash, out float estimatedCostMs)
        {
            estimatedCostMs = DefaultEstimatedCostMs;
            if (!_initialized)
                return true;

            int laneIndex = ClampLane(lane);
            if (_aupBarrierActive && laneIndex != JobAdmissionLanes.Lane0Critical)
            {
                ReportDenied(lane, jobHash, estimatedCostMs, _laneBudgetsMs[laneIndex]);
                return false;
            }

            if (laneIndex == JobAdmissionLanes.Lane4VFX && (_systemKillSwitchMask & KillSwitchDisableVfxMask) != 0u)
            {
                ReportDenied(lane, jobHash, estimatedCostMs, _laneBudgetsMs[laneIndex]);
                return false;
            }

            int costSlot = FindOrAllocateCostSlot(jobHash);
            if (costSlot >= 0)
            {
                float cached = _ewmaCostsMs[costSlot];
                estimatedCostMs = cached > 0f && math.isfinite(cached) ? cached : DefaultEstimatedCostMs;
            }

            if (!math.isfinite(estimatedCostMs) || estimatedCostMs < 0f)
            {
                ReportNonFinite(lane, jobHash, estimatedCostMs);
                estimatedCostMs = DefaultEstimatedCostMs;
            }

            float budget = _laneBudgetsMs[laneIndex];
            if (laneIndex == JobAdmissionLanes.Lane0Critical)
            {
                _laneBudgetsMs[laneIndex] = math.max(LaneDebtFloorMs, budget - estimatedCostMs);
                BorrowCriticalDebt(estimatedCostMs);
                WriteBlackbox(lane, jobHash, estimatedCostMs, _laneBudgetsMs[laneIndex], admitted: true);
                return true;
            }

            if (budget >= estimatedCostMs)
            {
                _laneBudgetsMs[laneIndex] = budget - estimatedCostMs;
                WriteBlackbox(lane, jobHash, estimatedCostMs, _laneBudgetsMs[laneIndex], admitted: true);
                return true;
            }

            ReportDenied(lane, jobHash, estimatedCostMs, budget);
            return false;
        }

        /// <inheritdoc />
        public void ReportJobCompleted(JobAdmissionLane lane, uint jobHash, float measuredCompleteMs)
        {
            if (!_initialized || jobHash == 0u)
                return;

            if (!math.isfinite(measuredCompleteMs) || measuredCompleteMs < 0f)
            {
                ReportNonFinite(lane, jobHash, measuredCompleteMs);
                return;
            }

            int slot = FindOrAllocateCostSlot(jobHash);
            if (slot < 0)
                return;

            float previous = _ewmaCostsMs[slot];
            float seed = previous > 0f && math.isfinite(previous) ? previous : measuredCompleteMs;
            _ewmaCostsMs[slot] = JobAdmissionMath.UpdateEwma(seed, measuredCompleteMs);
        }

        /// <inheritdoc />
        public void SetAupBarrierActive(bool active)
        {
            _aupBarrierActive = active;
        }

        /// <inheritdoc />
        public float GetLaneBudgetMs(JobAdmissionLane lane)
        {
            return _initialized ? _laneBudgetsMs[ClampLane(lane)] : 0f;
        }

        /// <inheritdoc />
        public float GetEstimatedCostMs(uint jobHash)
        {
            if (!_initialized || jobHash == 0u)
                return DefaultEstimatedCostMs;

            for (int i = 0; i < _costSlotCount; i++)
            {
                if (_jobHashes[i] != jobHash)
                    continue;

                float cost = _ewmaCostsMs[i];
                return cost > 0f && math.isfinite(cost) ? cost : DefaultEstimatedCostMs;
            }

            return DefaultEstimatedCostMs;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_laneBudgetsMs.IsCreated)
                _laneBudgetsMs.Dispose();
            if (_baseRefillMs.IsCreated)
                _baseRefillMs.Dispose();
            if (_jobHashes.IsCreated)
                _jobHashes.Dispose();
            if (_ewmaCostsMs.IsCreated)
                _ewmaCostsMs.Dispose();
            if (_blackbox.IsCreated)
                _blackbox.Dispose();

            _laneBudgetsMs = default;
            _baseRefillMs = default;
            _jobHashes = default;
            _ewmaCostsMs = default;
            _blackbox = default;
            _telemetrySink = null;
            _costSlotCount = 0;
            _blackboxCursor = 0;
            _criticalDebtFrameCount = 0;
            _systemKillSwitchMask = 0u;
            _aupBarrierActive = false;
            _initialized = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClampLane(JobAdmissionLane lane)
        {
            int laneIndex = (int)lane;
            return (uint)laneIndex < LaneCount ? laneIndex : JobAdmissionLanes.Lane5IO;
        }

        private int FindOrAllocateCostSlot(uint jobHash)
        {
            if (jobHash == 0u)
                jobHash = 1u;

            for (int i = 0; i < _costSlotCount; i++)
            {
                if (_jobHashes[i] == jobHash)
                    return i;
            }

            if (_costSlotCount >= CostSlotCapacity)
                return -1;

            int slot = _costSlotCount++;
            _jobHashes[slot] = jobHash;
            _ewmaCostsMs[slot] = DefaultEstimatedCostMs;
            return slot;
        }

        private void BorrowCriticalDebt(float estimatedCostMs)
        {
            float remainingDebt = estimatedCostMs;
            for (int lane = JobAdmissionLanes.Lane5IO; lane >= JobAdmissionLanes.Lane1World; lane--)
            {
                float budget = _laneBudgetsMs[lane];
                if (budget <= 0f)
                    continue;

                float borrowed = math.min(budget, remainingDebt);
                _laneBudgetsMs[lane] = budget - borrowed;
                remainingDebt -= borrowed;
                if (remainingDebt <= 0f)
                    return;
            }
        }

        private void ReportDenied(JobAdmissionLane lane, uint jobHash, float estimatedCostMs, float remainingBudgetMs)
        {
            WriteBlackbox(lane, jobHash, estimatedCostMs, remainingBudgetMs, admitted: false);
            _telemetrySink?.ReportAdmissionDenied(lane, jobHash, estimatedCostMs, remainingBudgetMs, _criticalDebtFrameCount);
        }

        private void ReportNonFinite(JobAdmissionLane lane, uint jobHash, float value)
        {
            _telemetrySink?.ReportNonFiniteAdmissionState(lane, jobHash, value);
            WriteBlackbox(lane, jobHash, value, 0f, admitted: false);
        }

        private void WriteBlackbox(JobAdmissionLane lane, uint jobHash, float estimatedCostMs, float remainingBudgetMs, bool admitted)
        {
            if (!_blackbox.IsCreated)
                return;

            int slot = _blackboxCursor++;
            if (_blackboxCursor >= BlackboxCapacity)
                _blackboxCursor = 0;

            _blackbox[slot] = new JobAdmissionBlackboxEntry
            {
                JobHash = jobHash,
                EstimatedCostMs = math.isfinite(estimatedCostMs) ? estimatedCostMs : 0f,
                RemainingBudgetMs = math.isfinite(remainingBudgetMs) ? remainingBudgetMs : 0f,
                CriticalDebtFrames = _criticalDebtFrameCount,
                Lane = (byte)ClampLane(lane),
                Flags = admitted ? (byte)1 : (byte)0,
                KillSwitchMask = _systemKillSwitchMask
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobAdmissionBlackboxEntry
        {
            public uint JobHash;
            public float EstimatedCostMs;
            public float RemainingBudgetMs;
            public int CriticalDebtFrames;
            public uint KillSwitchMask;
            public byte Lane;
            public byte Flags;
        }
    }

    /// <summary>
    /// Burst-visible EWMA math kernel. Kept separate so compiler can validate math without managed service fields.
    /// </summary>
    [BurstCompile]
    public static class JobAdmissionMath
    {
        /// <summary>Computes a 10 percent EWMA update with finite guards.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float UpdateEwma(float previousMs, float measuredMs)
        {
            float previous = math.isfinite(previousMs) && previousMs > 0f ? previousMs : measuredMs;
            float measured = math.isfinite(measuredMs) && measuredMs > 0f ? measuredMs : previous;
            return math.lerp(previous, measured, 0.10f);
        }
    }
}
