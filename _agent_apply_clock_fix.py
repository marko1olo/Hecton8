# -*- coding: utf-8 -*-
"""Apply P0 post-ready unpause+dilation fix to HeadlessSimulationRunner.cs"""
import os
os.chdir(r"C:\hades\Hecton8")

path = r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs"
src = open(path, encoding="utf-8").read()
orig = src

# 1) constants after TimeDilationScalar
old1 = """        private const float TimeDilationScalar = 100f;
        private const float GhostSpeedMetersPerSecond = 85f;"""
new1 = """        private const float TimeDilationScalar = 100f;
        // Post-ready self-heal: re-assert unpause+dilation while zero days completed.
        // Not a mock — restores real dispatcher scalar after late pause signals.
        private const float PostReadyClockEnsureIntervalSeconds = 5f;
        private const float PostReadyDiagIntervalSeconds = 15f;
        private const float GhostSpeedMetersPerSecond = 85f;"""
assert old1 in src, "const block missing"
src = src.replace(old1, new1, 1)

# 2) fields after _ecologyWaitDiagBucket
old2 = """        private double _ecologyWaitStartRealtime;
        private int _ecologyWaitDiagBucket = -1;
        private float _daySeconds = DefaultDaySeconds;"""
new2 = """        private double _ecologyWaitStartRealtime;
        private int _ecologyWaitDiagBucket = -1;
        private int _postReadyDiagBucket = -1;
        private double _lastClockEnsureRealtime;
        private float _daySeconds = DefaultDaySeconds;"""
assert old2 in src, "field block missing"
src = src.replace(old2, new2, 1)

# 3) Replace entire Update method
old3 = """        private void Update()
        {
            if (_finished)
                return;

            // Wall-clock ecology timeout must NOT depend on ColdTick (ticks can starve).
            // CRITICAL: budget starts at GameReady/bootstrap-exit, NOT BeginStartup.
            // p0_gameready (2026-07-30): _startupTime armed at runner install; bootstrap
            // dependency chain burned ~180s; short-circuit PublishGameReady then immediate
            // BOOTSTRAP_TIMEOUT with zero post-GameReady FrostTick budget.
            if (_started && !_ecologyReady)
            {
                TryArmEcologyWaitClock();
                // Ready-mark is a gate, not a sim-tick substitute. FrostTick can starve while
                // FO bootstrap lock / frame lock / dilation=0 hold RunDispatcherUpdate off the
                // master sim path; ecoInit was true from t=0 in p0_fo_lock_drain while
                // TryMarkEcologyReady never ran (Frost-only). Same starvation-proof pattern as
                // moving the wait clock off ColdTick onto Update.
                TryMarkEcologyReady();
                if (_ecologyReady)
                    return;
                // Keep FO scene-rebase barrier draining while we wait — dispatcher early-returns
                // all Frost/LateFrame while IsOriginShiftBootstrapLocked holds.
                HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks();
                MaybeLogEcologyWaitProgress();
                if (_ecologyWaitStartRealtime > 0.0 &&
                    Time.realtimeSinceStartupAsDouble - _ecologyWaitStartRealtime > _startupTimeoutSeconds)
                {
                    LogEcologyBootstrapTimeoutDiagnostics();
                    FailAndQuit(1, TimeoutHash, "[BOOTSTRAP_TIMEOUT]");
                    return;
                }
            }

            if (!_awaitingDispatcher)
                return;

            TryCompleteDispatcherWait();
        }"""
new3 = """        private void Update()
        {
            if (_finished)
                return;

            // Wall-clock ecology timeout must NOT depend on ColdTick (ticks can starve).
            // CRITICAL: budget starts at GameReady/bootstrap-exit, NOT BeginStartup.
            // p0_gameready (2026-07-30): _startupTime armed at runner install; bootstrap
            // dependency chain burned ~180s; short-circuit PublishGameReady then immediate
            // BOOTSTRAP_TIMEOUT with zero post-GameReady FrostTick budget.
            if (_started && !_ecologyReady)
            {
                TryArmEcologyWaitClock();
                // Ready-mark is a gate, not a sim-tick substitute. FrostTick can starve while
                // FO bootstrap lock / frame lock / dilation=0 hold RunDispatcherUpdate off the
                // master sim path; ecoInit was true from t=0 in p0_fo_lock_drain while
                // TryMarkEcologyReady never ran (Frost-only). Same starvation-proof pattern as
                // moving the wait clock off ColdTick onto Update.
                TryMarkEcologyReady();
                if (!_ecologyReady)
                {
                    // Keep FO scene-rebase barrier draining while we wait — dispatcher early-returns
                    // all Frost/LateFrame while IsOriginShiftBootstrapLocked holds.
                    HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks();
                    MaybeLogEcologyWaitProgress();
                    if (_ecologyWaitStartRealtime > 0.0 &&
                        Time.realtimeSinceStartupAsDouble - _ecologyWaitStartRealtime > _startupTimeoutSeconds)
                    {
                        LogEcologyBootstrapTimeoutDiagnostics();
                        FailAndQuit(1, TimeoutHash, "[BOOTSTRAP_TIMEOUT]");
                        return;
                    }
                }
            }

            // Post-ready: keep FO draining, re-assert unpause+dilation against late pause, emit diag.
            // Smoke 80b2d9764: ready green then ~495s wall with 0 CSV rows — Fast/Frost got dt<=0.
            if (_started && _ecologyReady && !_finished)
            {
                // GameReady may land after ecoInit; arm wait clock + open Player LateFrame path.
                TryArmEcologyWaitClock();
                MaybeEnsureHeadlessSimulationClockSustain();
                MaybeLogPostReadyProgress();
                HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks();
            }

            if (!_awaitingDispatcher)
                return;

            TryCompleteDispatcherWait();
        }"""
assert old3 in src, "Update method missing"
src = src.replace(old3, new3, 1)

# 4) TryArmEcologyWaitClock — ensure clock on GameReady arm
old4 = """            _ecologyWaitStartRealtime = Time.realtimeSinceStartupAsDouble;
            LogRunnerLifecycle("ecology wait clock armed (GameReady)");
        }"""
new4 = """            _ecologyWaitStartRealtime = Time.realtimeSinceStartupAsDouble;
            LogRunnerLifecycle("ecology wait clock armed (GameReady)");
            // GameReady opens Player LateFrame (day-audit drain). Re-assert clock here so a pause
            // taken during bootstrap cannot leave dilation at 0/pre-pause after short-circuit.
            EnsureHeadlessSimulationClock("game-ready");
        }"""
assert old4 in src, "TryArmEcologyWaitClock tail missing"
src = src.replace(old4, new4, 1)

# 5) TryMarkEcologyReady first-transition — ensure clock + insert helpers after method
old5 = """                // Lifecycle lines survive either way: LogRunnerLifecycle uses LogWarning precisely because
                // this filter once ate the harness's own verdict (`[HEADLESS] fail` appeared zero times in
                // 27,107 lines while the result JSON sat on disk).
                Debug.unityLogger.filterLogType = LogType.Warning;
            }

            _ecologyReady = readyNow;
        }

        /// <summary>
        /// Simulated seconds advanced per real second, measured rather than assumed. Zero until simulation
        /// starts advancing.
        /// </summary>
        private double DeliveredTimeDilation"""
new5 = """                // Lifecycle lines survive either way: LogRunnerLifecycle uses LogWarning precisely because
                // this filter once ate the harness's own verdict (`[HEADLESS] fail` appeared zero times in
                // 27,107 lines while the result JSON sat on disk).
                Debug.unityLogger.filterLogType = LogType.Warning;

                // Day machine needs dilated Fast/Frost deltaTime > 0. Dilation is requested once at
                // lane register; any later pause zeros scalar and unpause restores pre-pause (often 1).
                // Re-assert real dispatcher clock at the ready gate — not fake day rows.
                EnsureHeadlessSimulationClock("ecology-ready");
            }

            _ecologyReady = readyNow;
        }

        /// <summary>
        /// Unpause + re-request headless dilation on the live dispatcher.
        /// Product clock restore only — never writes CSV/day counters.
        /// </summary>
        private void EnsureHeadlessSimulationClock(string reason)
        {
            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            if (dispatcher == null)
            {
                LogRunnerLifecycle("sim clock ensure reason=" + reason + " dispatcher=null");
                return;
            }

            bool wasPaused = dispatcher.SimulationPaused;
            float dilBefore = dispatcher.TimeDilationScalar;

            // ConsumeFrameTimeDilationScalar returns 0 while _simulationPaused — unpause first.
            if (wasPaused)
                dispatcher.RequestSimulationPause(false, RunnerHash);

            dispatcher.RequestHeadlessTimeDilation(TimeDilationScalar, RunnerHash);
            _lastClockEnsureRealtime = Time.realtimeSinceStartupAsDouble;

            bool pausedAfter = dispatcher.SimulationPaused;
            float dilAfter = dispatcher.TimeDilationScalar;
            LogRunnerLifecycle(
                "sim clock ensure reason=" + reason +
                " pausedBefore=" + (wasPaused ? "1" : "0") +
                " dilBefore=" + dilBefore.ToString("0.###", CultureInfo.InvariantCulture) +
                " dilAfter=" + dilAfter.ToString("0.###", CultureInfo.InvariantCulture) +
                " pausedAfter=" + (pausedAfter ? "1" : "0") +
                " gameReady=" + (BootstrapState.IsGameReady ? "1" : "0"));
        }

        /// <summary>
        /// While ecology is ready and no day has completed, periodically re-assert the real clock
        /// against late SimulationPauseSignal / pause-menu / desync paths. Stops once days advance.
        /// </summary>
        private void MaybeEnsureHeadlessSimulationClockSustain()
        {
            if (!_ecologyReady || _finished || _completedDays > 0)
                return;

            if (_lastClockEnsureRealtime > 0.0 &&
                Time.realtimeSinceStartupAsDouble - _lastClockEnsureRealtime < PostReadyClockEnsureIntervalSeconds)
                return;

            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            if (dispatcher == null)
                return;

            // Cheap path: only force when paused or dilation collapsed below headless target.
            bool needsRestore = dispatcher.SimulationPaused ||
                                dispatcher.TimeDilationScalar + 0.01f < TimeDilationScalar;
            if (!needsRestore)
            {
                _lastClockEnsureRealtime = Time.realtimeSinceStartupAsDouble;
                return;
            }

            EnsureHeadlessSimulationClock("post-ready-sustain");
        }

        /// <summary>
        /// Throttled post-ready Warning diag so BATCH_TIMEOUT still leaves pause/dilation/dayAcc on disk.
        /// </summary>
        private void MaybeLogPostReadyProgress()
        {
            if (!_ecologyReady || _finished || _simulationStartRealtime <= 0.0)
                return;

            double waited = Time.realtimeSinceStartupAsDouble - _simulationStartRealtime;
            int bucket = (int)(waited / PostReadyDiagIntervalSeconds);
            if (bucket <= _postReadyDiagBucket)
                return;

            _postReadyDiagBucket = bucket;

            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            bool paused = dispatcher != null && dispatcher.SimulationPaused;
            float dil = dispatcher != null ? dispatcher.TimeDilationScalar : -1f;
            HectonFloatingOrigin.CopyBootstrapDrainSnapshot(
                out bool foHasOrigin,
                out bool foShift,
                out bool foPhysicsPause,
                out bool foLock,
                out int foPendingScenes,
                out bool foTargetsDirty,
                out bool foBarrier);

            LogRunnerLifecycle(
                "post-ready t=" + waited.ToString("0.0", CultureInfo.InvariantCulture) +
                "s paused=" + (paused ? "1" : "0") +
                " dil=" + dil.ToString("0.###", CultureInfo.InvariantCulture) +
                " dayAcc=" + _dayAccumulatorSeconds.ToString("0.###", CultureInfo.InvariantCulture) +
                " pending=" + _pendingDayAudits.ToString(CultureInfo.InvariantCulture) +
                " days=" + _completedDays.ToString(CultureInfo.InvariantCulture) +
                " simS=" + _simulatedSeconds.ToString("0.###", CultureInfo.InvariantCulture) +
                " gameReady=" + (BootstrapState.IsGameReady ? "1" : "0") +
                " frostReg=" + (_registeredFrost ? "1" : "0") +
                " lateReg=" + (_registeredLate ? "1" : "0") +
                " foHasOrigin=" + (foHasOrigin ? "1" : "0") +
                " foShift=" + (foShift ? "1" : "0") +
                " foPhysicsPause=" + (foPhysicsPause ? "1" : "0") +
                " foLock=" + (foLock ? "1" : "0") +
                " foPendingScenes=" + foPendingScenes.ToString(CultureInfo.InvariantCulture) +
                " foTargetsDirty=" + (foTargetsDirty ? "1" : "0") +
                " foBarrier=" + (foBarrier ? "1" : "0") +
                " dispBoot=" + (SystemDispatcher.IsOriginShiftBootstrapLocked ? "1" : "0") +
                " dispFrame=" + (SystemDispatcher.IsOriginShiftFrameLockedForCurrentFrame ? "1" : "0"));
        }

        /// <summary>
        /// Simulated seconds advanced per real second, measured rather than assumed. Zero until simulation
        /// starts advancing.
        /// </summary>
        private double DeliveredTimeDilation"""
assert old5 in src, "TryMarkEcologyReady / DeliveredTimeDilation join missing"
src = src.replace(old5, new5, 1)

# Also upgrade initial dilation request to go through Ensure after lanes (optional keep Request + ensure)
old6 = """                GlobalRegistry.TickDispatcher?.RequestHeadlessTimeDilation(TimeDilationScalar, RunnerHash);
                if (!_started)
                    FailAndQuit(1, TimeoutHash, "[RUNNER_REGISTRATION_FAILED]");
                else
                    LogRunnerLifecycle("runtime lanes registered; dilation requested");"""
new6 = """                // Unpause+dilation at register. Re-asserted again at ecology-ready / GameReady.
                EnsureHeadlessSimulationClock("lanes-registered");
                if (!_started)
                    FailAndQuit(1, TimeoutHash, "[RUNNER_REGISTRATION_FAILED]");
                else
                    LogRunnerLifecycle("runtime lanes registered; dilation requested");"""
assert old6 in src, "lane register dilation missing"
src = src.replace(old6, new6, 1)

if src == orig:
    raise SystemExit("NO CHANGES")

open(path, "w", encoding="utf-8", newline="\n").write(src)
print("OK patched", path)
print("delta_bytes", len(src) - len(orig))
# verify markers
for m in (
    "EnsureHeadlessSimulationClock",
    "MaybeEnsureHeadlessSimulationClockSustain",
    "MaybeLogPostReadyProgress",
    "post-ready-sustain",
    "ecology-ready",
    "PostReadyClockEnsureIntervalSeconds",
):
    print(m, src.count(m))
