using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
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
        private const int BlackboxEntrySizeBytes = 32;
        private const string AdmissionBlackboxDumpPath = "Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission.bin";
        private const ulong AdmissionBlackboxDumpMagic = 0x00384E4F54434548ul; // HECTON8\0
        private const uint AdmissionBlackboxDumpVersion = 1u;
        private const int CriticalDebtKillFrames = 60;
        private const float DefaultEstimatedCostMs = 0.025f;
        private const float OverflowEstimatedCostMs = 0.20f;
        private const float AdmissionCostClampMs = 1000f;
        private const float SurvivalBudgetScalar = 0.60f;
        private const float MissedFrameRefillScalar = 0.50f;
        private const float TargetFrameMilliseconds = 16.667f;
        private const float TargetFrameMillisecondsRcp = 0.0599988f;
        private const float MinDeltaRefillScale = 0.5f;
        private const float MaxDeltaRefillScale = 2.0f;
        private const float LaneDebtFloorMs = -4.0f;
        private const uint KillSwitchDisableVfxMask = 1u << JobAdmissionLanes.Lane4VFX;

        private VaultGenerationHandle<float> _laneBudgetsMsHandle;
        private VaultGenerationHandle<float> _baseRefillMsHandle;
        private VaultGenerationHandle<uint> _jobHashesHandle;
        private VaultGenerationHandle<float> _ewmaCostsMsHandle;
        private VaultGenerationHandle<JobAdmissionBlackboxEntry> _blackboxHandle;
        private IDataVault _dataVault;
        private IJobAdmissionTelemetrySink _telemetrySink;
        private int _costSlotCount;
        private int _blackboxCursor;
        private int _criticalDebtFrameCount;
        private uint _refillFrameSequence;
        private uint _systemKillSwitchMask;
        private uint _lastFaultDumpFrameSequence;
        private float _overflowEwmaCostMs;
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
            Initialize(telemetrySink, null);
        }

        /// <summary>
        /// Initializes through a boxed vault reference for generated-project compatibility when asmdef and source identities diverge.
        /// </summary>
        /// <param name="telemetrySink">Telemetry sink for denied and non-finite admission state.</param>
        /// <param name="dataVault">Global data vault boxed by bootstrap.</param>
        public void Initialize(IJobAdmissionTelemetrySink telemetrySink, object dataVault)
        {
            Initialize(telemetrySink, dataVault as IDataVault);
        }

        /// <summary>
        /// Initializes the admission gate against bootstrap-owned vault buffers.
        /// </summary>
        /// <param name="telemetrySink">Telemetry sink for denied and non-finite admission state.</param>
        /// <param name="dataVault">Global data vault that owns all persistent admission buffers.</param>
        public void Initialize(IJobAdmissionTelemetrySink telemetrySink, IDataVault dataVault)
        {
            if (_initialized)
                return;

            _telemetrySink = telemetrySink;
            if (dataVault == null)
            {
                ResetRuntimeState(clearTelemetrySink: false);
                return;
            }

            _dataVault = dataVault;
            _laneBudgetsMsHandle = dataVault.EnsureGenerationHandle<float>(
                BufferID.JobAdmissionLaneBudgets,
                LaneCount,
                SystemID.JobAdmission,
                NativeArrayOptions.ClearMemory);
            _baseRefillMsHandle = dataVault.EnsureGenerationHandle<float>(
                BufferID.JobAdmissionBaseRefill,
                LaneCount,
                SystemID.JobAdmission,
                NativeArrayOptions.ClearMemory);
            _jobHashesHandle = dataVault.EnsureGenerationHandle<uint>(
                BufferID.JobAdmissionJobHashes,
                CostSlotCapacity,
                SystemID.JobAdmission,
                NativeArrayOptions.ClearMemory);
            _ewmaCostsMsHandle = dataVault.EnsureGenerationHandle<float>(
                BufferID.JobAdmissionEwmaCosts,
                CostSlotCapacity,
                SystemID.JobAdmission,
                NativeArrayOptions.ClearMemory);
            _blackboxHandle = dataVault.EnsureGenerationHandle<JobAdmissionBlackboxEntry>(
                BufferID.JobAdmissionBlackBox,
                BlackboxCapacity,
                SystemID.JobAdmission,
                NativeArrayOptions.ClearMemory);

            NativeArray<float> laneBudgetsMs = ResolveLaneBudgets();
            NativeArray<float> baseRefillMs = ResolveBaseRefill();
            NativeArray<uint> jobHashes = ResolveJobHashes();
            NativeArray<float> ewmaCostsMs = ResolveEwmaCosts();
            NativeArray<JobAdmissionBlackboxEntry> blackbox = ResolveBlackbox();
            if (!laneBudgetsMs.IsCreated ||
                laneBudgetsMs.Length < LaneCount ||
                !baseRefillMs.IsCreated ||
                baseRefillMs.Length < LaneCount ||
                !jobHashes.IsCreated ||
                jobHashes.Length < CostSlotCapacity ||
                !ewmaCostsMs.IsCreated ||
                ewmaCostsMs.Length < CostSlotCapacity ||
                !blackbox.IsCreated ||
                blackbox.Length < BlackboxCapacity)
            {
                ReleaseVaultHandlesOnly();
                ResetRuntimeState(clearTelemetrySink: false);
                return;
            }

            _overflowEwmaCostMs = OverflowEstimatedCostMs;
            _lastFaultDumpFrameSequence = uint.MaxValue;

            for (int i = 0; i < LaneCount; i++)
            {
                float refill = ResolveDefaultRefillBudgetMs(i);
                baseRefillMs[i] = refill;
                laneBudgetsMs[i] = refill;
            }

            for (int i = 0; i < CostSlotCapacity; i++)
            {
                jobHashes[i] = 0u;
                ewmaCostsMs[i] = 0f;
            }

            for (int i = 0; i < BlackboxCapacity; i++)
                blackbox[i] = default;

            _initialized = true;
        }

        /// <inheritdoc />
        public void Refill(float globalQualityWeight01, float deltaTimeSeconds, bool previousFrameMissedBudget)
        {
            if (!_initialized)
                return;

            NativeArray<float> laneBudgetsMs = ResolveLaneBudgets();
            NativeArray<float> baseRefillMs = ResolveBaseRefill();
            if (!laneBudgetsMs.IsCreated || laneBudgetsMs.Length < LaneCount || !baseRefillMs.IsCreated || baseRefillMs.Length < LaneCount)
                return;

            _refillFrameSequence = _refillFrameSequence == uint.MaxValue ? 1u : _refillFrameSequence + 1u;
            float deltaMilliseconds = deltaTimeSeconds * 1000f;
            if (!math.isfinite(deltaMilliseconds) || deltaMilliseconds <= 0f)
            {
                ReportNonFinite(JobAdmissionLane.Lane0_Critical, 0u, deltaMilliseconds);
                deltaMilliseconds = TargetFrameMilliseconds;
            }

            float deltaScale = math.clamp(deltaMilliseconds * TargetFrameMillisecondsRcp, MinDeltaRefillScale, MaxDeltaRefillScale);
            float qualityWeight01 = SanitizeQualityWeight01(globalQualityWeight01);
            float qualityCurve01 = SmoothStep01(qualityWeight01);
            float qualityScale = math.lerp(SurvivalBudgetScalar, 1f, qualityCurve01);
            float missScale = previousFrameMissedBudget ? MissedFrameRefillScalar : 1f;
            float refillScale = deltaScale * qualityScale * missScale;

            for (int lane = 0; lane < LaneCount; lane++)
            {
                float baseRefill = baseRefillMs[lane];
                if (!math.isfinite(baseRefill) || baseRefill < 0f)
                {
                    ReportNonFinite((JobAdmissionLane)lane, 0u, baseRefill);
                    baseRefill = 0f;
                    baseRefillMs[lane] = 0f;
                }
                else if (baseRefill > AdmissionCostClampMs)
                {
                    baseRefill = AdmissionCostClampMs;
                    baseRefillMs[lane] = baseRefill;
                }

                float refill = baseRefill * refillScale;
                if (!math.isfinite(refill))
                {
                    ReportNonFinite((JobAdmissionLane)lane, 0u, refill);
                    refill = 0f;
                }
                else if (refill > AdmissionCostClampMs)
                {
                    refill = AdmissionCostClampMs;
                }

                float current = laneBudgetsMs[lane];
                if (!math.isfinite(current))
                {
                    ReportNonFinite((JobAdmissionLane)lane, 0u, current);
                    current = 0f;
                }
                else
                {
                    current = ClampLaneBudgetMilliseconds(lane, current);
                    laneBudgetsMs[lane] = current;
                }

                float cap = baseRefill * MaxDeltaRefillScale * qualityScale;
                if (!math.isfinite(cap) || cap < 0f)
                {
                    ReportNonFinite((JobAdmissionLane)lane, 0u, cap);
                    cap = 0f;
                }
                else if (cap > AdmissionCostClampMs)
                {
                    cap = AdmissionCostClampMs;
                }

                float next = current + refill;
                if (!math.isfinite(next))
                {
                    ReportNonFinite((JobAdmissionLane)lane, 0u, next);
                    next = cap;
                }
                else
                {
                    next = ClampLaneBudgetMilliseconds(lane, next);
                }

                laneBudgetsMs[lane] = math.min(next, cap);
                _telemetrySink?.ReportLaneState((JobAdmissionLane)lane, laneBudgetsMs[lane], refill, _criticalDebtFrameCount, _systemKillSwitchMask);
            }

            if (laneBudgetsMs[JobAdmissionLanes.Lane0Critical] < 0f)
            {
                _criticalDebtFrameCount = math.min(_criticalDebtFrameCount + 1, int.MaxValue);
                if (_criticalDebtFrameCount >= CriticalDebtKillFrames)
                    _systemKillSwitchMask |= KillSwitchDisableVfxMask;
            }
            else
            {
                _criticalDebtFrameCount = 0;
                _systemKillSwitchMask &= ~KillSwitchDisableVfxMask;
            }
        }

        /// <inheritdoc />
        public bool TryAdmitJob(JobAdmissionLane lane, uint jobHash, out float estimatedCostMs)
        {
            estimatedCostMs = DefaultEstimatedCostMs;
            if (!_initialized)
                return true;

            NativeArray<float> laneBudgetsMs = ResolveLaneBudgets();
            if (!laneBudgetsMs.IsCreated || laneBudgetsMs.Length < LaneCount)
                return true;

            NativeArray<uint> jobHashes = ResolveJobHashes();
            NativeArray<float> ewmaCostsMs = ResolveEwmaCosts();
            int laneIndex = ClampLane(lane);
            JobAdmissionLane normalizedLane = (JobAdmissionLane)laneIndex;
            estimatedCostMs = ResolveEstimatedCostMs(jobHash, jobHashes, ewmaCostsMs);
            if (!math.isfinite(estimatedCostMs) || estimatedCostMs < 0f)
            {
                ReportNonFinite(normalizedLane, jobHash, estimatedCostMs);
                estimatedCostMs = DefaultEstimatedCostMs;
            }
            else
            {
                estimatedCostMs = math.min(estimatedCostMs, AdmissionCostClampMs);
            }

            float budget = laneBudgetsMs[laneIndex];
            if (!math.isfinite(budget))
            {
                ReportNonFinite(normalizedLane, jobHash, budget);
                budget = 0f;
                laneBudgetsMs[laneIndex] = 0f;
            }
            else
            {
                budget = ClampLaneBudgetMilliseconds(laneIndex, budget);
                laneBudgetsMs[laneIndex] = budget;
            }

            if (_aupBarrierActive && laneIndex != JobAdmissionLanes.Lane0Critical)
            {
                ReportDenied(normalizedLane, jobHash, estimatedCostMs, budget);
                return false;
            }

            if (laneIndex == JobAdmissionLanes.Lane4VFX && (_systemKillSwitchMask & KillSwitchDisableVfxMask) != 0u)
            {
                ReportDenied(normalizedLane, jobHash, estimatedCostMs, budget);
                return false;
            }

            if (laneIndex == JobAdmissionLanes.Lane0Critical)
            {
                float previousDebt = math.max(0f, -budget);
                float nextBudget = math.max(LaneDebtFloorMs, budget - estimatedCostMs);
                float nextDebt = math.max(0f, -nextBudget);
                laneBudgetsMs[laneIndex] = nextBudget;
                BorrowCriticalDebt(laneBudgetsMs, math.max(0f, nextDebt - previousDebt), jobHash);
                WriteBlackbox(normalizedLane, jobHash, estimatedCostMs, laneBudgetsMs[laneIndex], admitted: true);
                return true;
            }

            if (budget >= estimatedCostMs)
            {
                laneBudgetsMs[laneIndex] = budget - estimatedCostMs;
                WriteBlackbox(normalizedLane, jobHash, estimatedCostMs, laneBudgetsMs[laneIndex], admitted: true);
                return true;
            }

            ReportDenied(normalizedLane, jobHash, estimatedCostMs, budget);
            return false;
        }

        /// <inheritdoc />
        public void ReportJobCompleted(JobAdmissionLane lane, uint jobHash, float measuredCompleteMs)
        {
            if (!_initialized || jobHash == 0u)
                return;

            NativeArray<uint> jobHashes = ResolveJobHashes();
            NativeArray<float> ewmaCostsMs = ResolveEwmaCosts();
            if (!jobHashes.IsCreated || jobHashes.Length < CostSlotCapacity || !ewmaCostsMs.IsCreated || ewmaCostsMs.Length < CostSlotCapacity)
                return;

            JobAdmissionLane normalizedLane = (JobAdmissionLane)ClampLane(lane);
            if (!math.isfinite(measuredCompleteMs) || measuredCompleteMs < 0f)
            {
                ReportNonFinite(normalizedLane, jobHash, measuredCompleteMs);
                return;
            }

            measuredCompleteMs = math.min(measuredCompleteMs, AdmissionCostClampMs);

            int slot = FindOrAllocateCostSlot(jobHash, jobHashes, ewmaCostsMs);
            if (slot < 0)
            {
                float overflowSeed = ResolveOverflowEstimatedCostMs();
                _overflowEwmaCostMs = JobAdmissionMath.UpdateEwma(overflowSeed, measuredCompleteMs);
                return;
            }

            float previous = ewmaCostsMs[slot];
            float seed = previous > 0f && math.isfinite(previous) ? previous : measuredCompleteMs;
            ewmaCostsMs[slot] = JobAdmissionMath.UpdateEwma(seed, measuredCompleteMs);
        }

        /// <inheritdoc />
        public void SetAupBarrierActive(bool active)
        {
            _aupBarrierActive = active;
        }

        /// <inheritdoc />
        public float GetLaneBudgetMs(JobAdmissionLane lane)
        {
            NativeArray<float>.ReadOnly laneBudgetsMs = ReadLaneBudgets();
            if (!_initialized || laneBudgetsMs.Length < LaneCount)
                return 0f;

            int laneIndex = ClampLane(lane);
            return ClampLaneBudgetMilliseconds(laneIndex, laneBudgetsMs[laneIndex]);
        }

        /// <inheritdoc />
        public float GetEstimatedCostMs(uint jobHash)
        {
            if (!_initialized)
                return DefaultEstimatedCostMs;

            return ResolveEstimatedCostMsReadOnly(jobHash, ReadJobHashes(), ReadEwmaCosts());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ReleaseVaultHandlesOnly();
            ResetRuntimeState(clearTelemetrySink: true);
        }

        private NativeArray<float> ResolveLaneBudgets()
        {
            return _dataVault != null && _laneBudgetsMsHandle.BufferID != 0u && _dataVault.TryResolveHandle(in _laneBudgetsMsHandle, out NativeArray<float> buffer)
                ? buffer
                : default;
        }

        private NativeArray<float>.ReadOnly ReadLaneBudgets()
        {
            return _dataVault != null && _laneBudgetsMsHandle.BufferID != 0u && _dataVault.TryReadOnlyHandle(in _laneBudgetsMsHandle, out NativeArray<float>.ReadOnly buffer)
                ? buffer
                : default;
        }

        private NativeArray<float> ResolveBaseRefill()
        {
            return _dataVault != null && _baseRefillMsHandle.BufferID != 0u && _dataVault.TryResolveHandle(in _baseRefillMsHandle, out NativeArray<float> buffer)
                ? buffer
                : default;
        }

        private NativeArray<uint> ResolveJobHashes()
        {
            return _dataVault != null && _jobHashesHandle.BufferID != 0u && _dataVault.TryResolveHandle(in _jobHashesHandle, out NativeArray<uint> buffer)
                ? buffer
                : default;
        }

        private NativeArray<uint>.ReadOnly ReadJobHashes()
        {
            return _dataVault != null && _jobHashesHandle.BufferID != 0u && _dataVault.TryReadOnlyHandle(in _jobHashesHandle, out NativeArray<uint>.ReadOnly buffer)
                ? buffer
                : default;
        }

        private NativeArray<float> ResolveEwmaCosts()
        {
            return _dataVault != null && _ewmaCostsMsHandle.BufferID != 0u && _dataVault.TryResolveHandle(in _ewmaCostsMsHandle, out NativeArray<float> buffer)
                ? buffer
                : default;
        }

        private NativeArray<float>.ReadOnly ReadEwmaCosts()
        {
            return _dataVault != null && _ewmaCostsMsHandle.BufferID != 0u && _dataVault.TryReadOnlyHandle(in _ewmaCostsMsHandle, out NativeArray<float>.ReadOnly buffer)
                ? buffer
                : default;
        }

        private NativeArray<JobAdmissionBlackboxEntry> ResolveBlackbox()
        {
            return _dataVault != null && _blackboxHandle.BufferID != 0u && _dataVault.TryResolveHandle(in _blackboxHandle, out NativeArray<JobAdmissionBlackboxEntry> buffer)
                ? buffer
                : default;
        }

        private void ReleaseVaultHandlesOnly()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _laneBudgetsMsHandle);
                ReleaseVaultHandle(vault, ref _baseRefillMsHandle);
                ReleaseVaultHandle(vault, ref _jobHashesHandle);
                ReleaseVaultHandle(vault, ref _ewmaCostsMsHandle);
                ReleaseVaultHandle(vault, ref _blackboxHandle);
            }

            _laneBudgetsMsHandle = default;
            _baseRefillMsHandle = default;
            _jobHashesHandle = default;
            _ewmaCostsMsHandle = default;
            _blackboxHandle = default;
            _dataVault = null;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ResetRuntimeState(bool clearTelemetrySink)
        {
            if (clearTelemetrySink)
                _telemetrySink = null;

            _costSlotCount = 0;
            _blackboxCursor = 0;
            _criticalDebtFrameCount = 0;
            _refillFrameSequence = 0u;
            _systemKillSwitchMask = 0u;
            _lastFaultDumpFrameSequence = 0u;
            _overflowEwmaCostMs = 0f;
            _aupBarrierActive = false;
            _initialized = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClampLane(JobAdmissionLane lane)
        {
            int laneIndex = (int)lane;
            return (uint)laneIndex < LaneCount ? laneIndex : JobAdmissionLanes.Lane5IO;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveDefaultRefillBudgetMs(int lane)
        {
            switch (lane)
            {
                case JobAdmissionLanes.Lane0Critical:
                    return 1.20f;
                case JobAdmissionLanes.Lane1World:
                    return 0.90f;
                case JobAdmissionLanes.Lane2Voxel:
                    return 1.40f;
                case JobAdmissionLanes.Lane3AI:
                    return 0.80f;
                case JobAdmissionLanes.Lane4VFX:
                    return 0.45f;
                default:
                    return 0.60f;
            }
        }

        private float ResolveEstimatedCostMs(uint jobHash, NativeArray<uint> jobHashes, NativeArray<float> ewmaCostsMs)
        {
            if (jobHash == 0u)
                return DefaultEstimatedCostMs;

            if (!jobHashes.IsCreated || jobHashes.Length < CostSlotCapacity || !ewmaCostsMs.IsCreated || ewmaCostsMs.Length < CostSlotCapacity)
                return DefaultEstimatedCostMs;

            int costSlot = FindCostSlot(jobHash, jobHashes);
            if (costSlot < 0)
            {
                return _costSlotCount >= CostSlotCapacity
                    ? ResolveOverflowEstimatedCostMs()
                    : DefaultEstimatedCostMs;
            }

            float cached = ewmaCostsMs[costSlot];
            return cached > 0f && math.isfinite(cached)
                ? math.min(cached, AdmissionCostClampMs)
                : DefaultEstimatedCostMs;
        }

        private float ResolveEstimatedCostMsReadOnly(uint jobHash, NativeArray<uint>.ReadOnly jobHashes, NativeArray<float>.ReadOnly ewmaCostsMs)
        {
            if (jobHash == 0u)
                return DefaultEstimatedCostMs;

            if (jobHashes.Length < CostSlotCapacity || ewmaCostsMs.Length < CostSlotCapacity)
                return DefaultEstimatedCostMs;

            int costSlot = FindCostSlotReadOnly(jobHash, jobHashes);
            if (costSlot < 0)
            {
                return _costSlotCount >= CostSlotCapacity
                    ? ResolveOverflowEstimatedCostMs()
                    : DefaultEstimatedCostMs;
            }

            float cached = ewmaCostsMs[costSlot];
            return cached > 0f && math.isfinite(cached)
                ? math.min(cached, AdmissionCostClampMs)
                : DefaultEstimatedCostMs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveOverflowEstimatedCostMs()
        {
            float overflow = _overflowEwmaCostMs;
            return overflow > 0f && math.isfinite(overflow)
                ? math.min(math.max(overflow, DefaultEstimatedCostMs), AdmissionCostClampMs)
                : OverflowEstimatedCostMs;
        }

        private int FindOrAllocateCostSlot(uint jobHash, NativeArray<uint> jobHashes, NativeArray<float> ewmaCostsMs)
        {
            if (jobHash == 0u)
                jobHash = 1u;

            int existingSlot = FindCostSlot(jobHash, jobHashes);
            if (existingSlot >= 0)
                return existingSlot;

            if (_costSlotCount >= CostSlotCapacity)
                return -1;

            int slot = _costSlotCount++;
            jobHashes[slot] = jobHash;
            ewmaCostsMs[slot] = DefaultEstimatedCostMs;
            return slot;
        }

        private int FindCostSlot(uint jobHash, NativeArray<uint> jobHashes)
        {
            if (jobHash == 0u || !jobHashes.IsCreated)
                return -1;

            int slotCount = math.min(_costSlotCount, jobHashes.Length);
            for (int i = 0; i < slotCount; i++)
            {
                if (jobHashes[i] == jobHash)
                    return i;
            }

            return -1;
        }

        private int FindCostSlotReadOnly(uint jobHash, NativeArray<uint>.ReadOnly jobHashes)
        {
            if (jobHash == 0u)
                return -1;

            int slotCount = math.min(_costSlotCount, jobHashes.Length);
            for (int i = 0; i < slotCount; i++)
            {
                if (jobHashes[i] == jobHash)
                    return i;
            }

            return -1;
        }

        private void BorrowCriticalDebt(NativeArray<float> laneBudgetsMs, float debtMs, uint jobHash)
        {
            float remainingDebt = debtMs;
            if (remainingDebt <= 0f || !laneBudgetsMs.IsCreated || laneBudgetsMs.Length < LaneCount)
                return;

            for (int lane = JobAdmissionLanes.Lane5IO; lane >= JobAdmissionLanes.Lane1World; lane--)
            {
                float budget = laneBudgetsMs[lane];
                if (!math.isfinite(budget))
                {
                    ReportNonFinite((JobAdmissionLane)lane, jobHash, budget);
                    laneBudgetsMs[lane] = 0f;
                    continue;
                }
                else
                {
                    budget = ClampLaneBudgetMilliseconds(lane, budget);
                    laneBudgetsMs[lane] = budget;
                }

                if (budget <= 0f)
                    continue;

                float borrowed = math.min(budget, remainingDebt);
                laneBudgetsMs[lane] = budget - borrowed;
                remainingDebt -= borrowed;
                if (remainingDebt <= 0f)
                    return;
            }
        }

        private void ReportDenied(JobAdmissionLane lane, uint jobHash, float estimatedCostMs, float remainingBudgetMs)
        {
            float safeEstimatedCostMs = ClampCostTelemetryMilliseconds(estimatedCostMs);
            float safeRemainingBudgetMs = ClampLaneBudgetMilliseconds(lane, remainingBudgetMs);
            WriteBlackbox(lane, jobHash, safeEstimatedCostMs, safeRemainingBudgetMs, admitted: false);
            _telemetrySink?.ReportAdmissionDenied(lane, jobHash, safeEstimatedCostMs, safeRemainingBudgetMs, _criticalDebtFrameCount);
        }

        private void ReportNonFinite(JobAdmissionLane lane, uint jobHash, float value)
        {
            float safeValue = ClampCostTelemetryMilliseconds(value);
            WriteBlackbox(lane, jobHash, safeValue, 0f, admitted: false);
            DumpFaultStateToTelemetry();
            _telemetrySink?.ReportNonFiniteAdmissionState(lane, jobHash, safeValue);
        }

        private void DumpFaultStateToTelemetry()
        {
            if (_lastFaultDumpFrameSequence == _refillFrameSequence)
                return;

            _lastFaultDumpFrameSequence = _refillFrameSequence;
            if (_telemetrySink != null)
            {
                NativeArray<float> laneBudgetsMs = ResolveLaneBudgets();
                NativeArray<float> baseRefillMs = ResolveBaseRefill();
                for (int lane = 0; lane < LaneCount; lane++)
                {
                    float budget = laneBudgetsMs.IsCreated && laneBudgetsMs.Length > lane ? laneBudgetsMs[lane] : 0f;
                    float refill = baseRefillMs.IsCreated && baseRefillMs.Length > lane ? baseRefillMs[lane] : 0f;
                    _telemetrySink.ReportLaneState(
                        (JobAdmissionLane)lane,
                        ClampLaneBudgetMilliseconds(lane, budget),
                        ClampNonNegativeTelemetryMilliseconds(refill),
                        _criticalDebtFrameCount,
                        _systemKillSwitchMask);
                }

                int slotCount = math.min(_costSlotCount, CostSlotCapacity);
                float overflow = ResolveOverflowEstimatedCostMs();
                NativeArray<uint> jobHashes = ResolveJobHashes();
                NativeArray<float> ewmaCostsMs = ResolveEwmaCosts();
                for (int slot = 0; slot < slotCount; slot++)
                {
                    if (!jobHashes.IsCreated || !ewmaCostsMs.IsCreated || slot >= jobHashes.Length || slot >= ewmaCostsMs.Length)
                        break;

                    float cost = ewmaCostsMs[slot];
                    _telemetrySink.ReportCostState(
                        slot,
                        jobHashes[slot],
                        ClampCostTelemetryMilliseconds(cost),
                        slotCount,
                        overflow);
                }
            }

            DumpAdmissionBlackboxCold();
        }

        private void WriteBlackbox(JobAdmissionLane lane, uint jobHash, float estimatedCostMs, float remainingBudgetMs, bool admitted)
        {
            NativeArray<JobAdmissionBlackboxEntry> blackbox = ResolveBlackbox();
            if (!blackbox.IsCreated || blackbox.Length < BlackboxCapacity)
                return;

            int slot = _blackboxCursor++;
            if (_blackboxCursor >= BlackboxCapacity)
                _blackboxCursor = 0;

            int laneIndex = ClampLane(lane);
            blackbox[slot] = new JobAdmissionBlackboxEntry
            {
                FrameSequence = _refillFrameSequence,
                JobHash = jobHash,
                EstimatedCostMs = ClampCostTelemetryMilliseconds(estimatedCostMs),
                RemainingBudgetMs = ClampLaneBudgetMilliseconds(laneIndex, remainingBudgetMs),
                CriticalDebtFrames = _criticalDebtFrameCount,
                Lane = (byte)laneIndex,
                Flags = admitted ? (byte)1 : (byte)0,
                KillSwitchMask = _systemKillSwitchMask,
                Reserved = 0,
                StateHash = ComputeBlackboxHash(jobHash, estimatedCostMs, remainingBudgetMs, admitted)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ClampCostTelemetryMilliseconds(float milliseconds)
        {
            return math.isfinite(milliseconds)
                ? math.clamp(milliseconds, 0f, AdmissionCostClampMs)
                : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ClampNonNegativeTelemetryMilliseconds(float milliseconds)
        {
            return math.isfinite(milliseconds)
                ? math.clamp(milliseconds, 0f, AdmissionCostClampMs)
                : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ClampLaneBudgetMilliseconds(JobAdmissionLane lane, float milliseconds)
        {
            return ClampLaneBudgetMilliseconds(ClampLane(lane), milliseconds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ClampLaneBudgetMilliseconds(int laneIndex, float milliseconds)
        {
            if (!math.isfinite(milliseconds))
                return 0f;

            float floor = laneIndex == JobAdmissionLanes.Lane0Critical ? LaneDebtFloorMs : 0f;
            return math.clamp(milliseconds, floor, AdmissionCostClampMs);
        }

        private uint ComputeBlackboxHash(uint jobHash, float estimatedCostMs, float remainingBudgetMs, bool admitted)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ _refillFrameSequence) * 16777619u;
                hash = (hash ^ jobHash) * 16777619u;
                hash = (hash ^ math.asuint(math.isfinite(estimatedCostMs) ? estimatedCostMs : 0f)) * 16777619u;
                hash = (hash ^ math.asuint(math.isfinite(remainingBudgetMs) ? remainingBudgetMs : 0f)) * 16777619u;
                hash = (hash ^ (uint)_criticalDebtFrameCount) * 16777619u;
                hash = (hash ^ _systemKillSwitchMask) * 16777619u;
                hash = (hash ^ (admitted ? 1u : 0u)) * 16777619u;
                return hash;
            }
        }

        private void DumpAdmissionBlackboxCold()
        {
            NativeArray<JobAdmissionBlackboxEntry> blackbox = ResolveBlackbox();
            if (!blackbox.IsCreated || blackbox.Length < BlackboxCapacity)
                return;

            try
            {
                string folder = Path.GetDirectoryName(AdmissionBlackboxDumpPath);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                using (FileStream stream = new FileStream(AdmissionBlackboxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(AdmissionBlackboxDumpMagic);
                    writer.Write(AdmissionBlackboxDumpVersion);
                    writer.Write(BlackboxCapacity);
                    writer.Write(BlackboxEntrySizeBytes);
                    writer.Write(_blackboxCursor);
                    writer.Write(_refillFrameSequence);
                    for (int i = 0; i < BlackboxCapacity; i++)
                    {
                        int index = _blackboxCursor + i;
                        if (index >= BlackboxCapacity)
                            index -= BlackboxCapacity;

                        JobAdmissionBlackboxEntry entry = blackbox[index];
                        writer.Write(entry.FrameSequence);
                        writer.Write(entry.JobHash);
                        writer.Write(entry.EstimatedCostMs);
                        writer.Write(entry.RemainingBudgetMs);
                        writer.Write(entry.CriticalDebtFrames);
                        writer.Write(entry.KillSwitchMask);
                        writer.Write(entry.Lane);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Reserved);
                        writer.Write(entry.StateHash);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = BlackboxEntrySizeBytes)]
        private struct JobAdmissionBlackboxEntry
        {
            [FieldOffset(0)]
            public uint FrameSequence;

            [FieldOffset(4)]
            public uint JobHash;

            [FieldOffset(8)]
            public float EstimatedCostMs;

            [FieldOffset(12)]
            public float RemainingBudgetMs;

            [FieldOffset(16)]
            public int CriticalDebtFrames;

            [FieldOffset(20)]
            public uint KillSwitchMask;

            [FieldOffset(24)]
            public byte Lane;

            [FieldOffset(25)]
            public byte Flags;

            [FieldOffset(26)]
            public ushort Reserved;

            [FieldOffset(28)]
            public uint StateHash;
        }
    }

    /// <summary>
    /// Burst-visible EWMA math kernel. Kept separate so compiler can validate math without managed service fields.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public static class JobAdmissionMath
    {
        private const float DefaultCostMs = 0.025f;
        private const float EwmaWeight = 0.10f;
        private const float CostClampMs = 1000f;

        /// <summary>Computes a 10 percent EWMA update with finite guards.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float UpdateEwma(float previousMs, float measuredMs)
        {
            float measured = math.isfinite(measuredMs) && measuredMs > 0f ? measuredMs : DefaultCostMs;
            float previous = math.isfinite(previousMs) && previousMs > 0f ? previousMs : measured;
            previous = math.min(previous, CostClampMs);
            measured = math.min(measured, CostClampMs);
            return math.min(math.lerp(previous, measured, EwmaWeight), CostClampMs);
        }
    }
}
