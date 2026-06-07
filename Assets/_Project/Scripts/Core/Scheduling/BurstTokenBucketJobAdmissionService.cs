using System;
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
        private const int BlackboxEntrySizeBytes = 64;
        private const string AdmissionBlackboxDumpPath = "Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission.bin";
        private const ulong AdmissionBlackboxDumpMagic = 0x00384E4F54434548ul; // HECTON8\0
        private const uint AdmissionBlackboxDumpVersion = 2u;
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
            _telemetrySink = telemetrySink;
            if (_initialized && ReferenceEquals(_dataVault, dataVault))
                return;

            if (_initialized)
            {
                ReleaseVaultHandlesOnly();
                ResetRuntimeState(clearTelemetrySink: false);
            }

            if (dataVault == null)
            {
                ReleaseVaultHandlesOnly();
                ResetRuntimeState(clearTelemetrySink: false);
                return;
            }

            if (dataVault.IsAllocationLocked || dataVault.IsCompactionFenceActive)
            {
                ReleaseVaultHandlesOnly();
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

            _overflowEwmaCostMs = OverflowEstimatedCostMs;
            _lastFaultDumpFrameSequence = uint.MaxValue;

            bool initFailed = !InitializeBaseRefillBudgets() ||
                              !InitializeLaneBudgets() ||
                              !ClearJobHashes() ||
                              !ClearEwmaCosts() ||
                              !ClearBlackbox();

            if (initFailed)
            {
                ReleaseVaultHandlesOnly();
                ResetRuntimeState(clearTelemetrySink: false);
                return;
            }

            _initialized = true;
        }

        /// <inheritdoc />
        public void Refill(float globalQualityWeight01, float deltaTimeSeconds, bool previousFrameMissedBudget)
        {
            if (!_initialized)
                return;

            NativeArray<float>.ReadOnly baseRefillMs = ReadBaseRefill();
            bool laneBudgetsLocked = TryAcquireWriteView(in _laneBudgetsMsHandle, out NativeArray<float> laneBudgetsMs);
            if (!laneBudgetsLocked || laneBudgetsMs.Length < LaneCount || baseRefillMs.Length < LaneCount)
            {
                if (laneBudgetsLocked)
                    ReleaseWriteView(in _laneBudgetsMsHandle);
                return;
            }

            bool pendingNonFinite = false;
            JobAdmissionLane pendingNonFiniteLane = JobAdmissionLane.Lane0_Critical;
            uint pendingNonFiniteJobHash = 0u;
            float pendingNonFiniteValue = 0f;
            float telemetryRefillScale = 0f;
            try
            {
                _refillFrameSequence = _refillFrameSequence == uint.MaxValue ? 1u : _refillFrameSequence + 1u;
                float deltaMilliseconds = deltaTimeSeconds * 1000f;
                if (!math.isfinite(deltaMilliseconds) || deltaMilliseconds <= 0f)
                {
                    CaptureFirstNonFinite(
                        ref pendingNonFinite,
                        ref pendingNonFiniteLane,
                        ref pendingNonFiniteJobHash,
                        ref pendingNonFiniteValue,
                        JobAdmissionLane.Lane0_Critical,
                        0u,
                        deltaMilliseconds);
                    deltaMilliseconds = TargetFrameMilliseconds;
                }

                float deltaScale = math.clamp(deltaMilliseconds * TargetFrameMillisecondsRcp, MinDeltaRefillScale, MaxDeltaRefillScale);
                float qualityWeight01 = SanitizeQualityWeight01(globalQualityWeight01);
                float qualityCurve01 = SmoothStep01(qualityWeight01);
                float qualityScale = math.lerp(SurvivalBudgetScalar, 1f, qualityCurve01);
                float missScale = previousFrameMissedBudget ? MissedFrameRefillScalar : 1f;
                float refillScale = deltaScale * qualityScale * missScale;
                telemetryRefillScale = refillScale;

                for (int lane = 0; lane < LaneCount; lane++)
                {
                    float baseRefill = baseRefillMs[lane];
                    if (!math.isfinite(baseRefill) || baseRefill < 0f)
                    {
                        CaptureFirstNonFinite(
                            ref pendingNonFinite,
                            ref pendingNonFiniteLane,
                            ref pendingNonFiniteJobHash,
                            ref pendingNonFiniteValue,
                            (JobAdmissionLane)lane,
                            0u,
                            baseRefill);
                        baseRefill = 0f;
                    }
                    else if (baseRefill > AdmissionCostClampMs)
                    {
                        baseRefill = AdmissionCostClampMs;
                    }

                    float refill = baseRefill * refillScale;
                    if (!math.isfinite(refill))
                    {
                        CaptureFirstNonFinite(
                            ref pendingNonFinite,
                            ref pendingNonFiniteLane,
                            ref pendingNonFiniteJobHash,
                            ref pendingNonFiniteValue,
                            (JobAdmissionLane)lane,
                            0u,
                            refill);
                        refill = 0f;
                    }
                    else if (refill > AdmissionCostClampMs)
                    {
                        refill = AdmissionCostClampMs;
                    }

                    float current = laneBudgetsMs[lane];
                    if (!math.isfinite(current))
                    {
                        CaptureFirstNonFinite(
                            ref pendingNonFinite,
                            ref pendingNonFiniteLane,
                            ref pendingNonFiniteJobHash,
                            ref pendingNonFiniteValue,
                            (JobAdmissionLane)lane,
                            0u,
                            current);
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
                        CaptureFirstNonFinite(
                            ref pendingNonFinite,
                            ref pendingNonFiniteLane,
                            ref pendingNonFiniteJobHash,
                            ref pendingNonFiniteValue,
                            (JobAdmissionLane)lane,
                            0u,
                            cap);
                        cap = 0f;
                    }
                    else if (cap > AdmissionCostClampMs)
                    {
                        cap = AdmissionCostClampMs;
                    }

                    float next = current + refill;
                    if (!math.isfinite(next))
                    {
                        CaptureFirstNonFinite(
                            ref pendingNonFinite,
                            ref pendingNonFiniteLane,
                            ref pendingNonFiniteJobHash,
                            ref pendingNonFiniteValue,
                            (JobAdmissionLane)lane,
                            0u,
                            next);
                        next = cap;
                    }
                    else
                    {
                        next = ClampLaneBudgetMilliseconds(lane, next);
                    }

                    laneBudgetsMs[lane] = math.min(next, cap);
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
            finally
            {
                ReleaseWriteView(in _laneBudgetsMsHandle);
            }

            if (_telemetrySink != null)
                ReportLaneStatesReadOnly(telemetryRefillScale);

            if (pendingNonFinite)
                ReportNonFinite(pendingNonFiniteLane, pendingNonFiniteJobHash, pendingNonFiniteValue);
        }

        /// <inheritdoc />
        public bool TryAdmitJob(JobAdmissionLane lane, uint jobHash, out float estimatedCostMs)
        {
            estimatedCostMs = DefaultEstimatedCostMs;
            if (!_initialized)
                return true;

            bool laneBudgetsLocked = TryAcquireWriteView(in _laneBudgetsMsHandle, out NativeArray<float> laneBudgetsMs);
            if (!laneBudgetsLocked || laneBudgetsMs.Length < LaneCount)
            {
                if (laneBudgetsLocked)
                    ReleaseWriteView(in _laneBudgetsMsHandle);
                return true;
            }

            bool admitted = true;
            bool pendingBlackbox = false;
            byte pendingBlackboxFlags = 0;
            JobAdmissionLane pendingBlackboxLane = JobAdmissionLane.Lane0_Critical;
            uint pendingBlackboxJobHash = 0u;
            float pendingBlackboxEstimatedCostMs = 0f;
            float pendingBlackboxRemainingBudgetMs = 0f;
            bool pendingDenied = false;
            bool pendingNonFinite = false;
            JobAdmissionLane pendingNonFiniteLane = JobAdmissionLane.Lane0_Critical;
            uint pendingNonFiniteJobHash = 0u;
            float pendingNonFiniteValue = 0f;
            try
            {
                NativeArray<uint>.ReadOnly jobHashes = ReadJobHashes();
                NativeArray<float>.ReadOnly ewmaCostsMs = ReadEwmaCosts();
                int laneIndex = ClampLane(lane);
                JobAdmissionLane normalizedLane = (JobAdmissionLane)laneIndex;
                estimatedCostMs = ResolveEstimatedCostMsReadOnly(jobHash, jobHashes, ewmaCostsMs);
                if (!math.isfinite(estimatedCostMs) || estimatedCostMs < 0f)
                {
                    CaptureFirstNonFinite(
                        ref pendingNonFinite,
                        ref pendingNonFiniteLane,
                        ref pendingNonFiniteJobHash,
                        ref pendingNonFiniteValue,
                        normalizedLane,
                        jobHash,
                        estimatedCostMs);
                    estimatedCostMs = DefaultEstimatedCostMs;
                }
                else
                {
                    estimatedCostMs = math.min(estimatedCostMs, AdmissionCostClampMs);
                }

                float budget = laneBudgetsMs[laneIndex];
                if (!math.isfinite(budget))
                {
                    CaptureFirstNonFinite(
                        ref pendingNonFinite,
                        ref pendingNonFiniteLane,
                        ref pendingNonFiniteJobHash,
                        ref pendingNonFiniteValue,
                        normalizedLane,
                        jobHash,
                        budget);
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
                    admitted = false;
                    pendingDenied = true;
                    pendingBlackbox = true;
                    pendingBlackboxFlags = (byte)(JobAdmissionTelemetryFlags.Denied | JobAdmissionTelemetryFlags.AupBarrier);
                    pendingBlackboxLane = normalizedLane;
                    pendingBlackboxJobHash = jobHash;
                    pendingBlackboxEstimatedCostMs = estimatedCostMs;
                    pendingBlackboxRemainingBudgetMs = budget;
                }
                else if (laneIndex == JobAdmissionLanes.Lane4VFX && (_systemKillSwitchMask & KillSwitchDisableVfxMask) != 0u)
                {
                    admitted = false;
                    pendingDenied = true;
                    pendingBlackbox = true;
                    pendingBlackboxFlags = (byte)(JobAdmissionTelemetryFlags.Denied | JobAdmissionTelemetryFlags.KillSwitch);
                    pendingBlackboxLane = normalizedLane;
                    pendingBlackboxJobHash = jobHash;
                    pendingBlackboxEstimatedCostMs = estimatedCostMs;
                    pendingBlackboxRemainingBudgetMs = budget;
                }
                else if (laneIndex == JobAdmissionLanes.Lane0Critical)
                {
                    float previousDebt = math.max(0f, -budget);
                    float nextBudget = math.max(LaneDebtFloorMs, budget - estimatedCostMs);
                    float nextDebt = math.max(0f, -nextBudget);
                    laneBudgetsMs[laneIndex] = nextBudget;
                    BorrowCriticalDebt(
                        laneBudgetsMs,
                        math.max(0f, nextDebt - previousDebt),
                        jobHash,
                        ref pendingNonFinite,
                        ref pendingNonFiniteLane,
                        ref pendingNonFiniteJobHash,
                        ref pendingNonFiniteValue);
                    pendingBlackbox = true;
                    pendingBlackboxFlags = JobAdmissionTelemetryFlags.Admitted;
                    pendingBlackboxLane = normalizedLane;
                    pendingBlackboxJobHash = jobHash;
                    pendingBlackboxEstimatedCostMs = estimatedCostMs;
                    pendingBlackboxRemainingBudgetMs = laneBudgetsMs[laneIndex];
                }
                else if (budget >= estimatedCostMs)
                {
                    laneBudgetsMs[laneIndex] = budget - estimatedCostMs;
                    pendingBlackbox = true;
                    pendingBlackboxFlags = JobAdmissionTelemetryFlags.Admitted;
                    pendingBlackboxLane = normalizedLane;
                    pendingBlackboxJobHash = jobHash;
                    pendingBlackboxEstimatedCostMs = estimatedCostMs;
                    pendingBlackboxRemainingBudgetMs = laneBudgetsMs[laneIndex];
                }
                else
                {
                    admitted = false;
                    pendingDenied = true;
                    pendingBlackbox = true;
                    pendingBlackboxFlags = (byte)(JobAdmissionTelemetryFlags.Denied | JobAdmissionTelemetryFlags.InsufficientBudget);
                    pendingBlackboxLane = normalizedLane;
                    pendingBlackboxJobHash = jobHash;
                    pendingBlackboxEstimatedCostMs = estimatedCostMs;
                    pendingBlackboxRemainingBudgetMs = budget;
                }
            }
            finally
            {
                ReleaseWriteView(in _laneBudgetsMsHandle);
            }

            if (pendingDenied)
            {
                ReportDenied(
                    pendingBlackboxLane,
                    pendingBlackboxJobHash,
                    pendingBlackboxEstimatedCostMs,
                    pendingBlackboxRemainingBudgetMs,
                    pendingBlackboxFlags);
            }
            else if (pendingBlackbox)
            {
                WriteBlackbox(
                    pendingBlackboxLane,
                    pendingBlackboxJobHash,
                    pendingBlackboxEstimatedCostMs,
                    pendingBlackboxRemainingBudgetMs,
                    pendingBlackboxFlags);
            }

            if (pendingNonFinite)
                ReportNonFinite(pendingNonFiniteLane, pendingNonFiniteJobHash, pendingNonFiniteValue);

            return admitted;
        }

        /// <inheritdoc />
        public void ReportJobCompleted(JobAdmissionLane lane, uint jobHash, float measuredCompleteMs)
        {
            if (!_initialized || jobHash == 0u)
                return;

            JobAdmissionLane normalizedLane = (JobAdmissionLane)ClampLane(lane);
            if (!math.isfinite(measuredCompleteMs) || measuredCompleteMs < 0f)
            {
                ReportNonFinite(normalizedLane, jobHash, measuredCompleteMs);
                return;
            }

            measuredCompleteMs = math.min(measuredCompleteMs, AdmissionCostClampMs);
            bool jobHashesLocked = TryAcquireWriteView(in _jobHashesHandle, out NativeArray<uint> jobHashes);
            if (!jobHashesLocked || jobHashes.Length < CostSlotCapacity)
            {
                if (jobHashesLocked)
                    ReleaseWriteView(in _jobHashesHandle);
                return;
            }

            int slot;
            try
            {
                slot = FindOrAllocateCostSlot(jobHash, jobHashes);
            }
            finally
            {
                ReleaseWriteView(in _jobHashesHandle);
            }

            if (slot < 0)
            {
                float overflowSeed = ResolveOverflowEstimatedCostMs();
                _overflowEwmaCostMs = JobAdmissionMath.UpdateEwma(overflowSeed, measuredCompleteMs);
                return;
            }

            bool ewmaCostsLocked = TryAcquireWriteView(in _ewmaCostsMsHandle, out NativeArray<float> ewmaCostsMs);
            if (!ewmaCostsLocked || ewmaCostsMs.Length <= slot)
            {
                if (ewmaCostsLocked)
                    ReleaseWriteView(in _ewmaCostsMsHandle);
                return;
            }

            try
            {
                float previous = ewmaCostsMs[slot];
                float seed = previous > 0f && math.isfinite(previous) ? previous : measuredCompleteMs;
                ewmaCostsMs[slot] = JobAdmissionMath.UpdateEwma(seed, measuredCompleteMs);
            }
            finally
            {
                ReleaseWriteView(in _ewmaCostsMsHandle);
            }
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

        private NativeArray<float>.ReadOnly ReadLaneBudgets()
        {
            return _dataVault != null && _laneBudgetsMsHandle.BufferID != 0u && _dataVault.TryReadOnlyHandle(in _laneBudgetsMsHandle, out NativeArray<float>.ReadOnly buffer)
                ? buffer
                : default;
        }

        private NativeArray<float>.ReadOnly ReadBaseRefill()
        {
            return _dataVault != null && _baseRefillMsHandle.BufferID != 0u && _dataVault.TryReadOnlyHandle(in _baseRefillMsHandle, out NativeArray<float>.ReadOnly buffer)
                ? buffer
                : default;
        }

        private bool TryAcquireWriteView<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || handle.BufferID == 0u)
            {
                buffer = default;
                return false;
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.JobAdmission, out buffer))
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
                    vault.ReleaseWriteLock(in handle, SystemID.JobAdmission);
            }
        }

        private void ReleaseWriteView<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseWriteLock(in handle, SystemID.JobAdmission);
        }

        private NativeArray<uint>.ReadOnly ReadJobHashes()
        {
            return _dataVault != null && _jobHashesHandle.BufferID != 0u && _dataVault.TryReadOnlyHandle(in _jobHashesHandle, out NativeArray<uint>.ReadOnly buffer)
                ? buffer
                : default;
        }

        private NativeArray<float>.ReadOnly ReadEwmaCosts()
        {
            return _dataVault != null && _ewmaCostsMsHandle.BufferID != 0u && _dataVault.TryReadOnlyHandle(in _ewmaCostsMsHandle, out NativeArray<float>.ReadOnly buffer)
                ? buffer
                : default;
        }

        private bool InitializeBaseRefillBudgets()
        {
            bool locked = TryAcquireWriteView(in _baseRefillMsHandle, out NativeArray<float> baseRefillMs);
            if (!locked || baseRefillMs.Length < LaneCount)
            {
                if (locked)
                    ReleaseWriteView(in _baseRefillMsHandle);
                return false;
            }

            try
            {
                for (int i = 0; i < LaneCount; i++)
                    baseRefillMs[i] = ResolveDefaultRefillBudgetMs(i);

                return true;
            }
            finally
            {
                ReleaseWriteView(in _baseRefillMsHandle);
            }
        }

        private bool InitializeLaneBudgets()
        {
            bool locked = TryAcquireWriteView(in _laneBudgetsMsHandle, out NativeArray<float> laneBudgetsMs);
            if (!locked || laneBudgetsMs.Length < LaneCount)
            {
                if (locked)
                    ReleaseWriteView(in _laneBudgetsMsHandle);
                return false;
            }

            try
            {
                for (int i = 0; i < LaneCount; i++)
                    laneBudgetsMs[i] = ResolveDefaultRefillBudgetMs(i);

                return true;
            }
            finally
            {
                ReleaseWriteView(in _laneBudgetsMsHandle);
            }
        }

        private bool ClearJobHashes()
        {
            bool locked = TryAcquireWriteView(in _jobHashesHandle, out NativeArray<uint> jobHashes);
            if (!locked || jobHashes.Length < CostSlotCapacity)
            {
                if (locked)
                    ReleaseWriteView(in _jobHashesHandle);
                return false;
            }

            try
            {
                for (int i = 0; i < CostSlotCapacity; i++)
                    jobHashes[i] = 0u;

                return true;
            }
            finally
            {
                ReleaseWriteView(in _jobHashesHandle);
            }
        }

        private bool ClearEwmaCosts()
        {
            bool locked = TryAcquireWriteView(in _ewmaCostsMsHandle, out NativeArray<float> ewmaCostsMs);
            if (!locked || ewmaCostsMs.Length < CostSlotCapacity)
            {
                if (locked)
                    ReleaseWriteView(in _ewmaCostsMsHandle);
                return false;
            }

            try
            {
                for (int i = 0; i < CostSlotCapacity; i++)
                    ewmaCostsMs[i] = 0f;

                return true;
            }
            finally
            {
                ReleaseWriteView(in _ewmaCostsMsHandle);
            }
        }

        private bool ClearBlackbox()
        {
            bool locked = TryAcquireWriteView(in _blackboxHandle, out NativeArray<JobAdmissionBlackboxEntry> blackbox);
            if (!locked || blackbox.Length < BlackboxCapacity)
            {
                if (locked)
                    ReleaseWriteView(in _blackboxHandle);
                return false;
            }

            try
            {
                for (int i = 0; i < BlackboxCapacity; i++)
                    blackbox[i] = default;

                return true;
            }
            finally
            {
                ReleaseWriteView(in _blackboxHandle);
            }
        }

        private static void CaptureFirstNonFinite(
            ref bool pendingNonFinite,
            ref JobAdmissionLane pendingLane,
            ref uint pendingJobHash,
            ref float pendingValue,
            JobAdmissionLane lane,
            uint jobHash,
            float value)
        {
            if (pendingNonFinite)
                return;

            pendingNonFinite = true;
            pendingLane = lane;
            pendingJobHash = jobHash;
            pendingValue = value;
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

        private int FindOrAllocateCostSlot(uint jobHash, NativeArray<uint> jobHashes)
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

        private void BorrowCriticalDebt(
            NativeArray<float> laneBudgetsMs,
            float debtMs,
            uint jobHash,
            ref bool pendingNonFinite,
            ref JobAdmissionLane pendingNonFiniteLane,
            ref uint pendingNonFiniteJobHash,
            ref float pendingNonFiniteValue)
        {
            float remainingDebt = debtMs;
            if (remainingDebt <= 0f || !laneBudgetsMs.IsCreated || laneBudgetsMs.Length < LaneCount)
                return;

            for (int lane = JobAdmissionLanes.Lane5IO; lane >= JobAdmissionLanes.Lane1World; lane--)
            {
                float budget = laneBudgetsMs[lane];
                if (!math.isfinite(budget))
                {
                    CaptureFirstNonFinite(
                        ref pendingNonFinite,
                        ref pendingNonFiniteLane,
                        ref pendingNonFiniteJobHash,
                        ref pendingNonFiniteValue,
                        (JobAdmissionLane)lane,
                        jobHash,
                        budget);
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

        private void ReportDenied(JobAdmissionLane lane, uint jobHash, float estimatedCostMs, float remainingBudgetMs, byte flags)
        {
            float safeEstimatedCostMs = ClampCostTelemetryMilliseconds(estimatedCostMs);
            float safeRemainingBudgetMs = ClampLaneBudgetMilliseconds(lane, remainingBudgetMs);
            byte safeFlags = (byte)(flags | JobAdmissionTelemetryFlags.Denied);
            WriteBlackbox(lane, jobHash, safeEstimatedCostMs, safeRemainingBudgetMs, safeFlags);
            _telemetrySink?.ReportAdmissionDenied(lane, jobHash, safeEstimatedCostMs, safeRemainingBudgetMs, _criticalDebtFrameCount, safeFlags);
        }

        private void ReportNonFinite(JobAdmissionLane lane, uint jobHash, float value)
        {
            float safeValue = ClampCostTelemetryMilliseconds(value);
            WriteBlackbox(lane, jobHash, safeValue, 0f, (byte)(JobAdmissionTelemetryFlags.Denied | JobAdmissionTelemetryFlags.NonFinite));
            DumpFaultStateToTelemetry();
            _telemetrySink?.ReportNonFiniteAdmissionState(lane, jobHash, safeValue, _criticalDebtFrameCount);
        }

        private void DumpFaultStateToTelemetry()
        {
            if (_lastFaultDumpFrameSequence == _refillFrameSequence)
                return;

            _lastFaultDumpFrameSequence = _refillFrameSequence;
            if (_telemetrySink != null)
            {
                ReportLaneStatesReadOnly(1f);

                int slotCount = math.min(_costSlotCount, CostSlotCapacity);
                float overflow = ResolveOverflowEstimatedCostMs();
                NativeArray<uint>.ReadOnly jobHashes = ReadJobHashes();
                NativeArray<float>.ReadOnly ewmaCostsMs = ReadEwmaCosts();
                for (int slot = 0; slot < slotCount; slot++)
                {
                    if (slot >= jobHashes.Length || slot >= ewmaCostsMs.Length)
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

        private void ReportLaneStatesReadOnly(float refillScale)
        {
            NativeArray<float>.ReadOnly laneBudgetsMs = ReadLaneBudgets();
            NativeArray<float>.ReadOnly baseRefillMs = ReadBaseRefill();
            float safeRefillScale = math.isfinite(refillScale) ? math.max(0f, refillScale) : 0f;
            for (int lane = 0; lane < LaneCount; lane++)
            {
                float budget = laneBudgetsMs.IsCreated && laneBudgetsMs.Length > lane ? laneBudgetsMs[lane] : 0f;
                float baseRefill = baseRefillMs.IsCreated && baseRefillMs.Length > lane ? baseRefillMs[lane] : 0f;
                float refill = baseRefill * safeRefillScale;
                _telemetrySink.ReportLaneState(
                    (JobAdmissionLane)lane,
                    ClampLaneBudgetMilliseconds(lane, budget),
                    ClampNonNegativeTelemetryMilliseconds(refill),
                    _criticalDebtFrameCount,
                    _systemKillSwitchMask);
            }
        }

        private void WriteBlackbox(JobAdmissionLane lane, uint jobHash, float estimatedCostMs, float remainingBudgetMs, byte flags)
        {
            IDataVault vault = _dataVault;
            if (vault == null || _blackboxHandle.BufferID == 0u)
                return;

            if (!vault.TryAcquireWriteLock(in _blackboxHandle, SystemID.JobAdmission, out NativeArray<JobAdmissionBlackboxEntry> blackbox))
                return;

            try
            {
                if (!blackbox.IsCreated || blackbox.Length < BlackboxCapacity)
                    return;

                int slot = _blackboxCursor++;
                if (_blackboxCursor >= BlackboxCapacity)
                    _blackboxCursor = 0;

                int laneIndex = ClampLane(lane);
                JobAdmissionBlackboxEntry entry = default;
                entry.FrameSequence = _refillFrameSequence;
                entry.JobHash = jobHash;
                entry.EstimatedCostMs = ClampCostTelemetryMilliseconds(estimatedCostMs);
                entry.RemainingBudgetMs = ClampLaneBudgetMilliseconds(laneIndex, remainingBudgetMs);
                entry.CriticalDebtFrames = _criticalDebtFrameCount;
                entry.Lane = (byte)laneIndex;
                entry.Flags = flags;
                entry.KillSwitchMask = _systemKillSwitchMask;
                entry.Reserved = 0;
                entry.StateHash = ComputeBlackboxHash(jobHash, entry.EstimatedCostMs, entry.RemainingBudgetMs, flags);
                blackbox[slot] = entry;
            }
            finally
            {
                vault.ReleaseWriteLock(in _blackboxHandle, SystemID.JobAdmission);
            }
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

        private uint ComputeBlackboxHash(uint jobHash, float estimatedCostMs, float remainingBudgetMs, byte flags)
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
                hash = (hash ^ (uint)flags) * 16777619u;
                return hash;
            }
        }

        private void DumpAdmissionBlackboxCold()
        {
            if (!TryReadOnlyBlackbox(out NativeArray<JobAdmissionBlackboxEntry>.ReadOnly blackbox))
                return;

            if (!blackbox.IsCreated || blackbox.Length < BlackboxCapacity)
                return;

            NativeArray<byte> payload = default;
            try
            {
                int byteCount = 32 + (BlackboxCapacity * BlackboxEntrySizeBytes);
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(BurstTokenBucketJobAdmissionService),
                    "jobAdmissionBlackboxDumpPayload");
                int writeCursor = 0;

                WriteUInt64LittleEndian(payload, ref writeCursor, AdmissionBlackboxDumpMagic);
                WriteUInt32LittleEndian(payload, ref writeCursor, AdmissionBlackboxDumpVersion);
                WriteInt32LittleEndian(payload, ref writeCursor, BlackboxCapacity);
                WriteInt32LittleEndian(payload, ref writeCursor, BlackboxEntrySizeBytes);
                WriteInt32LittleEndian(payload, ref writeCursor, _blackboxCursor);
                WriteUInt32LittleEndian(payload, ref writeCursor, _refillFrameSequence);
                WriteUInt32LittleEndian(payload, ref writeCursor, 0u);

                for (int i = 0; i < BlackboxCapacity; i++)
                {
                    int index = _blackboxCursor + i;
                    if (index >= BlackboxCapacity)
                        index -= BlackboxCapacity;

                    JobAdmissionBlackboxEntry entry = blackbox[index];
                    WriteBlackboxEntry(payload, ref writeCursor, in entry);
                }

                Hecton8.Core.NativeFaultDumpWriter.TryWriteAll(AdmissionBlackboxDumpPath, payload, writeCursor);
            }
            catch (Exception)
            {
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(BurstTokenBucketJobAdmissionService),
                    "jobAdmissionBlackboxDumpPayload");
            }
        }

        private bool TryReadOnlyBlackbox(out NativeArray<JobAdmissionBlackboxEntry>.ReadOnly blackbox)
        {
            IDataVault vault = _dataVault;
            if (vault != null &&
                _blackboxHandle.BufferID != 0u &&
                vault.TryReadOnlyHandle(in _blackboxHandle, out blackbox))
            {
                return true;
            }

            blackbox = default;
            return false;
        }

        private static void WriteBlackboxEntry(NativeArray<byte> destination, ref int cursor, in JobAdmissionBlackboxEntry entry)
        {
            int entryStart = cursor;
            WriteUInt32LittleEndian(destination, ref cursor, entry.FrameSequence);
            WriteUInt32LittleEndian(destination, ref cursor, entry.JobHash);
            WriteSingleLittleEndian(destination, ref cursor, entry.EstimatedCostMs);
            WriteSingleLittleEndian(destination, ref cursor, entry.RemainingBudgetMs);
            WriteInt32LittleEndian(destination, ref cursor, entry.CriticalDebtFrames);
            WriteUInt32LittleEndian(destination, ref cursor, entry.KillSwitchMask);
            destination[cursor++] = entry.Lane;
            destination[cursor++] = entry.Flags;
            WriteUInt16LittleEndian(destination, ref cursor, entry.Reserved);
            WriteUInt32LittleEndian(destination, ref cursor, entry.StateHash);
            while (cursor - entryStart < BlackboxEntrySizeBytes)
                destination[cursor++] = 0;
        }

        private static void WriteSingleLittleEndian(NativeArray<byte> destination, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> destination, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)value));
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> destination, ref int cursor, ulong value)
        {
            destination[cursor++] = (byte)value;
            destination[cursor++] = (byte)(value >> 8);
            destination[cursor++] = (byte)(value >> 16);
            destination[cursor++] = (byte)(value >> 24);
            destination[cursor++] = (byte)(value >> 32);
            destination[cursor++] = (byte)(value >> 40);
            destination[cursor++] = (byte)(value >> 48);
            destination[cursor++] = (byte)(value >> 56);
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, ref int cursor, uint value)
        {
            destination[cursor++] = (byte)value;
            destination[cursor++] = (byte)(value >> 8);
            destination[cursor++] = (byte)(value >> 16);
            destination[cursor++] = (byte)(value >> 24);
        }

        private static void WriteUInt16LittleEndian(NativeArray<byte> destination, ref int cursor, ushort value)
        {
            destination[cursor++] = (byte)value;
            destination[cursor++] = (byte)(value >> 8);
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

            [FieldOffset(32)]
            private byte _pad0;

            [FieldOffset(33)]
            private byte _pad1;

            [FieldOffset(34)]
            private byte _pad2;

            [FieldOffset(35)]
            private byte _pad3;

            [FieldOffset(36)]
            private byte _pad4;

            [FieldOffset(37)]
            private byte _pad5;

            [FieldOffset(38)]
            private byte _pad6;

            [FieldOffset(39)]
            private byte _pad7;

            [FieldOffset(40)]
            private byte _pad8;

            [FieldOffset(41)]
            private byte _pad9;

            [FieldOffset(42)]
            private byte _pad10;

            [FieldOffset(43)]
            private byte _pad11;

            [FieldOffset(44)]
            private byte _pad12;

            [FieldOffset(45)]
            private byte _pad13;

            [FieldOffset(46)]
            private byte _pad14;

            [FieldOffset(47)]
            private byte _pad15;

            [FieldOffset(48)]
            private byte _pad16;

            [FieldOffset(49)]
            private byte _pad17;

            [FieldOffset(50)]
            private byte _pad18;

            [FieldOffset(51)]
            private byte _pad19;

            [FieldOffset(52)]
            private byte _pad20;

            [FieldOffset(53)]
            private byte _pad21;

            [FieldOffset(54)]
            private byte _pad22;

            [FieldOffset(55)]
            private byte _pad23;

            [FieldOffset(56)]
            private byte _pad24;

            [FieldOffset(57)]
            private byte _pad25;

            [FieldOffset(58)]
            private byte _pad26;

            [FieldOffset(59)]
            private byte _pad27;

            [FieldOffset(60)]
            private byte _pad28;

            [FieldOffset(61)]
            private byte _pad29;

            [FieldOffset(62)]
            private byte _pad30;

            [FieldOffset(63)]
            private byte _pad31;
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
