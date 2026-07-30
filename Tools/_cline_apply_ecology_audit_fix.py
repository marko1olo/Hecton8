# -*- coding: utf-8 -*-
"""Apply ECOLOGY_UNAVAILABLE job-fence fix to HeadlessSimulationRunner.cs.

Moves day-boundary biomass audit from FrostTick (same frame as SlowTick schedule,
before ecology LateFrame completes jobs) into LateFrameTick at PriorityLayer.Player
so ecology Environment LateFrame has already completed the solve fence.
"""
from __future__ import annotations

from pathlib import Path

PATH = Path(r"C:/hades/Hecton8/Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs")
text = PATH.read_text(encoding="utf-8")
original = text

# ---------------------------------------------------------------------------
# 1) Update the long fence comment to document the LateFrame sample move.
# ---------------------------------------------------------------------------
old_comment_tail = """        // Treating that identically to a dead ecology meant one day boundary landing inside a job fence
        // aborted the whole run and blamed the ecology. Tolerance is bounded, never unbounded: three
        // CONSECUTIVE unsampled days still fail, and FinishRunIfTargetReached refuses to report SUCCESS for a
        // run that never sampled once.
        private const int MaxConsecutiveEcologySampleFailures = 3;"""

new_comment_tail = """        // Treating that identically to a dead ecology meant one day boundary landing inside a job fence
        // aborted the whole run and blamed the ecology. Tolerance is bounded, never unbounded: three
        // CONSECUTIVE unsampled days still fail, and FinishRunIfTargetReached refuses to report SUCCESS for a
        // run that never sampled once.
        //
        // FIX 2026-07-30: day-boundary biomass sampling no longer runs in FrostTick. FrostTick only
        // accumulates completed-day debt (_pendingDayAudits). The sample itself runs in LateFrameTick,
        // AFTER ecology's Environment-lane LateFrameTick has called CompleteScheduledSimulation and
        // cleared HasPendingSimulationJob. Runner LateFrame is registered at PriorityLayer.Player so
        // the lane order Core -> Environment -> Player -> UI guarantees ecology completes first.
        // Streak only advances on a true dead/empty ecology (audit false with no pending job), never
        // on the deterministic same-frame fence that used to make every day "unsampled".
        private const int MaxConsecutiveEcologySampleFailures = 3;"""

if old_comment_tail not in text:
    raise SystemExit("FAIL: comment anchor not found")
text = text.replace(old_comment_tail, new_comment_tail, 1)

# ---------------------------------------------------------------------------
# 2) Add _pendingDayAudits field next to ecology counters.
# ---------------------------------------------------------------------------
old_fields = """        private int _ecologySampleFailureStreak;
        private int _ecologySampledDayCount;
        private int _ecologyUnsampledDayCount;"""

new_fields = """        private int _ecologySampleFailureStreak;
        private int _ecologySampledDayCount;
        private int _ecologyUnsampledDayCount;
        // Day boundaries detected in FrostTick; biomass sample deferred to LateFrameTick (post job fence).
        private int _pendingDayAudits;"""

if old_fields not in text:
    raise SystemExit("FAIL: field anchor not found")
text = text.replace(old_fields, new_fields, 1)

# ---------------------------------------------------------------------------
# 3) Replace LateFrameTick body to run deferred day audits after ghost commit.
# ---------------------------------------------------------------------------
old_late = """        public void LateFrameTick()
        {
            if (!_started || _finished || !_ghostStepPending)
                return;

            _ghostStepPending = false;
            if (!TryCommitPendingGhostState(out GhostState previous, out GhostState next))
            {
                FailAndQuit(1, DataVaultUnavailableHash, "[GHOST_BUFFER_WRITE_FAILED]");
                return;
            }

            HandleSyntheticAupShift(in previous, in next);
            if (!math.all(math.isfinite(next.AbsoluteMeters)) ||
                !math.isfinite(next.RuntimeMeters.x) ||
                !math.isfinite(next.RuntimeMeters.y) ||
                !math.isfinite(next.RuntimeMeters.z))
            {
                FailAndQuit(1, NaNHash, "[NAN_DETECTED]");
            }
        }"""

new_late = """        public void LateFrameTick()
        {
            if (!_started || _finished)
                return;

            // Ghost commit first (existing path). Day audits run after so a failed ghost write still
            // aborts before we charge ecology for a day the harness could not advance.
            if (_ghostStepPending)
            {
                _ghostStepPending = false;
                if (!TryCommitPendingGhostState(out GhostState previous, out GhostState next))
                {
                    FailAndQuit(1, DataVaultUnavailableHash, "[GHOST_BUFFER_WRITE_FAILED]");
                    return;
                }

                HandleSyntheticAupShift(in previous, in next);
                if (!math.all(math.isfinite(next.AbsoluteMeters)) ||
                    !math.isfinite(next.RuntimeMeters.x) ||
                    !math.isfinite(next.RuntimeMeters.y) ||
                    !math.isfinite(next.RuntimeMeters.z))
                {
                    FailAndQuit(1, NaNHash, "[NAN_DETECTED]");
                    return;
                }
            }

            // Biomass sample after ecology LateFrame (Environment lane) completed scheduled jobs.
            // See MaxConsecutiveEcologySampleFailures comment block for the fence chronology.
            if (_ecologyReady && _pendingDayAudits > 0)
                DrainPendingDayAudits();
        }"""

if old_late not in text:
    raise SystemExit("FAIL: LateFrameTick anchor not found")
text = text.replace(old_late, new_late, 1)

# ---------------------------------------------------------------------------
# 4) FrostTick: queue day debt instead of ExecuteDailyAudit.
# ---------------------------------------------------------------------------
old_frost = """        public void FrostTick()
        {
            if (!_started || _finished)
                return;

            TryMarkEcologyReady();
            if (!_ecologyReady)
                return;

            if (!AuditGasPressureFinite())
            {
                FailAndQuit(1, GasInvalidHash, "[GAS_INVALID]");
                return;
            }

            int auditsThisTick = 0;
            while (_dayAccumulatorSeconds >= _daySeconds &&
                   _completedDays < _targetDays &&
                   auditsThisTick < MaxDailyAuditsPerFrostTick &&
                   !_finished)
            {
                _dayAccumulatorSeconds -= _daySeconds;
                ExecuteDailyAudit();
                auditsThisTick++;
            }
        }"""

new_frost = """        public void FrostTick()
        {
            if (!_started || _finished)
                return;

            TryMarkEcologyReady();
            if (!_ecologyReady)
                return;

            if (!AuditGasPressureFinite())
            {
                FailAndQuit(1, GasInvalidHash, "[GAS_INVALID]");
                return;
            }

            // Do NOT sample biomass here. SlowTick (ecology) schedules the sector solve earlier in this
            // same dispatcher update, and the job fence stays up until ecology's LateFrameTick. Queue
            // day debt only; LateFrameTick drains it after the fence clears.
            int auditsThisTick = 0;
            int remainingDays = _targetDays - _completedDays - _pendingDayAudits;
            while (_dayAccumulatorSeconds >= _daySeconds &&
                   remainingDays > 0 &&
                   auditsThisTick < MaxDailyAuditsPerFrostTick &&
                   !_finished)
            {
                _dayAccumulatorSeconds -= _daySeconds;
                _pendingDayAudits++;
                remainingDays--;
                auditsThisTick++;
            }
        }"""

if old_frost not in text:
    raise SystemExit("FAIL: FrostTick anchor not found")
text = text.replace(old_frost, new_frost, 1)

# ---------------------------------------------------------------------------
# 5) Register LateFrame at Player priority (after ecology Environment).
# ---------------------------------------------------------------------------
old_reg = """            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);"""
new_reg = """            // Player lane runs after Environment: ecology LateFrame completes jobs before this runner samples.
            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);"""
if old_reg not in text:
    raise SystemExit("FAIL: late register anchor not found")
text = text.replace(old_reg, new_reg, 1)

old_unreg = """                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);"""
new_unreg = """                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);"""
if old_unreg not in text:
    raise SystemExit("FAIL: late unregister anchor not found")
text = text.replace(old_unreg, new_unreg, 1)

# ---------------------------------------------------------------------------
# 6) Insert DrainPendingDayAudits + refine ExecuteDailyAudit fence handling.
# ---------------------------------------------------------------------------
old_exec_start = """        private void ExecuteDailyAudit()
        {
            _completedDays++;
            long nativeBytes = GlobalRegistry.NativeTrackedBytes;
            long h8Bytes = H8Memory.TotalBytes;
            int nativeAllocations = GlobalRegistry.NativeAllocationCount;
            int h8Allocations = H8Memory.ActiveAllocationCount;
            _lastMemoryBytes = nativeBytes;
            _lastH8MemoryBytes = h8Bytes;

            if (DetectTenDayMemoryGrowth(nativeBytes, h8Bytes, h8Allocations, out bool memoryWindowUnavailable))
            {
                // A leak verdict is worthless without the owner-level allocation table, so dump it before quitting.
                TryDumpH8MemoryTable();
                FailAndQuit(1, LeakHash, "[LEAK_DETECTED]");
                return;
            }

            // A vault write refusal means the memory window could not be sampled, NOT that memory leaked.
            // Transient refusals (compaction fence, mutation guard, generation bump) are tolerated for a
            // bounded number of consecutive days so one defrag tick cannot abort a 100-day run.
            if (memoryWindowUnavailable)
            {
                _memoryWindowFailureStreak++;
                if (_memoryWindowFailureStreak >= MaxConsecutiveMemoryWindowFailures)
                {
                    FailAndQuit(1, DataVaultUnavailableHash, "[MEMORY_WINDOW_UNAVAILABLE]");
                    return;
                }
            }
            else
            {
                _memoryWindowFailureStreak = 0;
            }

            IEcosystemDirectorService ecosystem = GlobalRegistry.EcosystemDirector;
            if (ecosystem == null || !ecosystem.TryGetGlobalBiomassAudit(out EcosystemBiomassAuditSample biomass))
            {
                // The CSV row goes out FIRST and unconditionally, for every unsampled day, tolerated or not.
                // The day counter has already advanced (:594) so a skipped row would leave a hole in the
                // series and a reader could not tell a tolerated fence from a missing measurement.
                _ecologyUnsampledDayCount++;
                if (!TryWriteDailyCsv(default, nativeBytes, h8Bytes, nativeAllocations, h8Allocations, CsvFlagEcologySampleUnavailable))
                    return;

                _ecologySampleFailureStreak++;
                if (_ecologySampleFailureStreak >= MaxConsecutiveEcologySampleFailures)
                {
                    // Bounded, so the tolerance cannot hide the defect this harness currently exists to
                    // surface: with -h8headless there is no player, so _activeBiomassCellCount stays 0 and
                    // EVERY day is unsampled - that run still dies here, on day 3, with the same verdict.
                    FailAndQuit(1, EcologyCollapseHash, "[ECOLOGY_UNAVAILABLE]");
                    return;
                }

                // Must still run on this path. The day loop in FrostTick stops once _completedDays reaches
                // _targetDays, so a tolerated unsampled FINAL day would otherwise never reach any terminal
                // state: no completion, no failure, and the batch runner's watchdog left to notice hours
                // later. This is the only exit for that case.
                FinishRunIfTargetReached();
                return;
            }"""

new_exec_start = """        /// <summary>
        /// Drains day-boundary audits deferred from FrostTick. Invoked only from LateFrameTick so the
        /// ecology job fence from the same frame's SlowTick has already been completed.
        /// </summary>
        private void DrainPendingDayAudits()
        {
            int auditsThisTick = 0;
            while (_pendingDayAudits > 0 &&
                   _completedDays < _targetDays &&
                   auditsThisTick < MaxDailyAuditsPerFrostTick &&
                   !_finished)
            {
                _pendingDayAudits--;
                ExecuteDailyAudit();
                auditsThisTick++;
            }
        }

        private void ExecuteDailyAudit()
        {
            _completedDays++;
            long nativeBytes = GlobalRegistry.NativeTrackedBytes;
            long h8Bytes = H8Memory.TotalBytes;
            int nativeAllocations = GlobalRegistry.NativeAllocationCount;
            int h8Allocations = H8Memory.ActiveAllocationCount;
            _lastMemoryBytes = nativeBytes;
            _lastH8MemoryBytes = h8Bytes;

            if (DetectTenDayMemoryGrowth(nativeBytes, h8Bytes, h8Allocations, out bool memoryWindowUnavailable))
            {
                // A leak verdict is worthless without the owner-level allocation table, so dump it before quitting.
                TryDumpH8MemoryTable();
                FailAndQuit(1, LeakHash, "[LEAK_DETECTED]");
                return;
            }

            // A vault write refusal means the memory window could not be sampled, NOT that memory leaked.
            // Transient refusals (compaction fence, mutation guard, generation bump) are tolerated for a
            // bounded number of consecutive days so one defrag tick cannot abort a 100-day run.
            if (memoryWindowUnavailable)
            {
                _memoryWindowFailureStreak++;
                if (_memoryWindowFailureStreak >= MaxConsecutiveMemoryWindowFailures)
                {
                    FailAndQuit(1, DataVaultUnavailableHash, "[MEMORY_WINDOW_UNAVAILABLE]");
                    return;
                }
            }
            else
            {
                _memoryWindowFailureStreak = 0;
            }

            IEcosystemDirectorService ecosystem = GlobalRegistry.EcosystemDirector;
            if (ecosystem == null || !ecosystem.TryGetGlobalBiomassAudit(out EcosystemBiomassAuditSample biomass))
            {
                // The CSV row goes out FIRST and unconditionally, for every unsampled day, tolerated or not.
                // The day counter has already advanced so a skipped row would leave a hole in the
                // series and a reader could not tell a tolerated miss from a missing measurement.
                _ecologyUnsampledDayCount++;
                if (!TryWriteDailyCsv(default, nativeBytes, h8Bytes, nativeAllocations, h8Allocations, CsvFlagEcologySampleUnavailable))
                    return;

                // After the LateFrame move, a false audit is no longer the deterministic same-frame job
                // fence (that fence is down by the time we sample). Streak still bounds true empty
                // ecology: headless with zero seeded biomass cells still fails by day 3.
                _ecologySampleFailureStreak++;
                if (_ecologySampleFailureStreak >= MaxConsecutiveEcologySampleFailures)
                {
                    // Bounded, so the tolerance cannot hide a permanently empty biomass table:
                    // with -h8headless there is no player; if EnsurePlayerSectorRegistered never seeds,
                    // _activeBiomassCellCount stays 0 and EVERY day is unsampled - that run still dies
                    // here, on day 3, with the same verdict.
                    FailAndQuit(1, EcologyCollapseHash, "[ECOLOGY_UNAVAILABLE]");
                    return;
                }

                // Must still run on this path. The day loop stops once _completedDays reaches
                // _targetDays, so a tolerated unsampled FINAL day would otherwise never reach any terminal
                // state: no completion, no failure, and the batch runner's watchdog left to notice hours
                // later. This is the only exit for that case.
                FinishRunIfTargetReached();
                return;
            }"""

if old_exec_start not in text:
    raise SystemExit("FAIL: ExecuteDailyAudit anchor not found")
text = text.replace(old_exec_start, new_exec_start, 1)

# ---------------------------------------------------------------------------
# 7) Update FinishRunIfTargetReached remarks (FrostTick -> LateFrame).
# ---------------------------------------------------------------------------
old_remarks = """        /// Tolerating transient job fences is only safe while this asymmetry holds. Without it, the tolerance
        /// added for MaxConsecutiveEcologySampleFailures would turn the 2026-07-29 failure into a green run:
        /// with -h8headlessDays 1 the single day is unsampled, the streak is 1 of 3 and therefore tolerated,
        /// and the target day count is already met - so the previous "if (_completedDays >= _targetDays)
        /// CompleteAndQuit()" would have written status SUCCESS for a run that never once measured the
        /// ecology. A harness that can report success without evidence is worse than one that over-reports
        /// failure, so the sample count gates the verdict, not the day count alone."""

new_remarks = """        /// Sampling now runs in LateFrameTick after the ecology job fence clears, so the 2026-07-29
        /// same-frame unavailability path should not fire. The sample-count gate remains: without it, a
        /// permanently empty biomass table (seed never ran) could still reach target days with zero real
        /// samples if streak tolerance alone were trusted. A harness that can report success without
        /// evidence is worse than one that over-reports failure, so the sample count gates the verdict,
        /// not the day count alone."""

if old_remarks not in text:
    raise SystemExit("FAIL: FinishRun remarks anchor not found")
text = text.replace(old_remarks, new_remarks, 1)

if text == original:
    raise SystemExit("FAIL: no changes applied")

PATH.write_text(text, encoding="utf-8", newline="\n")
print("OK wrote", PATH)
print("bytes", len(text.encode("utf-8")))

# sanity checks
checks = [
    "_pendingDayAudits",
    "DrainPendingDayAudits",
    "PriorityLayer.Player",
    "FIX 2026-07-30",
]
for c in checks:
    print("check", c, text.count(c))
