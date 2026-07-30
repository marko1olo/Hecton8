# P0 fix: ecology wait clock must start at GameReady (not runner install).
# Evidence p0_gameready.log: lanes @2514, short-circuit PublishGameReady @3616,
# BOOTSTRAP_TIMEOUT @3666. _startupTime set at BeginStartup; 180s burned during
# long bootstrap dependency chain BEFORE GameReady opened ticks. Fail fires
# immediately after short-circuit — zero post-GameReady ecology budget.
#
# Also: on BOOTSTRAP_TIMEOUT path, log eco/FO state for next diagnosis.
# Also: while waiting for ecology after lanes registered, call
# HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks so FO scene-rebase
# lock can clear without depending solely on dispatcher early-return path.
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")
RUNNER = ROOT / r"Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs"
text = RUNNER.read_text(encoding="utf-8")
orig = text

# 1) Add field _ecologyWaitStartRealtime after _startupTime
old_fields = """        private double _startupTime;
        private float _startupTimeoutSeconds = DefaultStartupTimeoutSeconds;"""
# Find actual field block more carefully
if "_ecologyWaitStartRealtime" not in text:
    needle = "        private double _startupTime;"
    if needle not in text:
        raise SystemExit("FAIL: _startupTime field missing")
    text = text.replace(
        needle,
        needle + "\n        // Wall clock for ecology-ready budget. Armed only after lanes are live AND\n"
        "        // BootstrapState.IsGameReady (or bootstrap presence cleared). Measuring from BeginStartup\n"
        "        // falsely burned the entire 180s budget during dependency init before GameReady opened\n"
        "        // dispatcher FrostTick — p0_gameready 2026-07-30 BOOTSTRAP_TIMEOUT at short-circuit.\n"
        "        private double _ecologyWaitStartRealtime;",
        1,
    )

# 2) Replace Update ecology timeout block
old_update = """            // Wall-clock ecology/bootstrap timeout must NOT depend on ColdTick.
            // ColdTick only fires after lanes are registered AND dispatcher cadence
            // is unlocked. p0_dispfix (2026-07-30) proved lanes registered then
            // BATCH_TIMEOUT with zero BOOTSTRAP_TIMEOUT — ticks were starved
            // (Player LateFrame gated on !IsGameReady; possible origin-shift lock).
            // Poll here so a stall always produces a named FailAndQuit instead of
            // letting the batch runner win with BATCH_TIMEOUT.
            if (_started &&
                !_ecologyReady &&
                Time.realtimeSinceStartupAsDouble - _startupTime > _startupTimeoutSeconds)
            {
                FailAndQuit(1, TimeoutHash, "[BOOTSTRAP_TIMEOUT]");
                return;
            }"""

new_update = """            // Wall-clock ecology timeout must NOT depend on ColdTick (ticks can starve).
            // CRITICAL: budget starts at GameReady/bootstrap-exit, NOT BeginStartup.
            // p0_gameready (2026-07-30): _startupTime armed at runner install; bootstrap
            // dependency chain burned ~180s; short-circuit PublishGameReady then immediate
            // BOOTSTRAP_TIMEOUT with zero post-GameReady FrostTick budget.
            if (_started && !_ecologyReady)
            {
                TryArmEcologyWaitClock();
                // Keep FO scene-rebase barrier draining while we wait — dispatcher early-returns
                // all Frost/LateFrame while IsOriginShiftBootstrapLocked holds.
                HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks();
                if (_ecologyWaitStartRealtime > 0.0 &&
                    Time.realtimeSinceStartupAsDouble - _ecologyWaitStartRealtime > _startupTimeoutSeconds)
                {
                    LogEcologyBootstrapTimeoutDiagnostics();
                    FailAndQuit(1, TimeoutHash, "[BOOTSTRAP_TIMEOUT]");
                    return;
                }
            }"""

if old_update not in text:
    raise SystemExit("FAIL: Update timeout block not found exactly")
text = text.replace(old_update, new_update, 1)

# 3) Replace ColdTick timeout to use ecology wait clock
old_cold = """            if (!_ecologyReady && Time.realtimeSinceStartupAsDouble - _startupTime > _startupTimeoutSeconds)
            {
                FailAndQuit(1, TimeoutHash, "[BOOTSTRAP_TIMEOUT]");
            }"""
new_cold = """            if (!_ecologyReady)
            {
                TryArmEcologyWaitClock();
                if (_ecologyWaitStartRealtime > 0.0 &&
                    Time.realtimeSinceStartupAsDouble - _ecologyWaitStartRealtime > _startupTimeoutSeconds)
                {
                    LogEcologyBootstrapTimeoutDiagnostics();
                    FailAndQuit(1, TimeoutHash, "[BOOTSTRAP_TIMEOUT]");
                }
            }"""
if old_cold not in text:
    raise SystemExit("FAIL: ColdTick timeout block not found")
text = text.replace(old_cold, new_cold, 1)

# 4) Insert helper methods before TryMarkEcologyReady
helper = '''
        /// <summary>
        /// Arms the ecology-ready wall clock once bootstrap has opened gameplay ticks.
        /// Uses IsGameReady OR cleared bootstrap presence so headless short-circuit and
        /// full ActivatePlayer paths both qualify. Does not arm during dependency init.
        /// </summary>
        private void TryArmEcologyWaitClock()
        {
            if (_ecologyWaitStartRealtime > 0.0)
                return;

            // GameReady is the hard signal. HasActiveInstance==false alone is insufficient
            // during early boot before PublishBootstrapPresence(true).
            if (!BootstrapState.IsGameReady)
                return;

            _ecologyWaitStartRealtime = Time.realtimeSinceStartupAsDouble;
            LogRunnerLifecycle("ecology wait clock armed (GameReady)");
        }

        private void LogEcologyBootstrapTimeoutDiagnostics()
        {
            IEcosystemDirectorService ecosystem = GlobalRegistry.EcosystemDirector;
            bool ecoNull = ecosystem == null;
            bool ecoInit = !ecoNull && ecosystem.IsInitialized;
            bool foFlushClean = HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks();
            // FailAndQuit muzzles Log after ecologyReady; we are pre-ready so Log is fine.
            LogRunnerLifecycle(
                "BOOTSTRAP_TIMEOUT diag ecoNull=" + (ecoNull ? "1" : "0") +
                " ecoInit=" + (ecoInit ? "1" : "0") +
                " foFlushClean=" + (foFlushClean ? "1" : "0") +
                " gameReady=" + (BootstrapState.IsGameReady ? "1" : "0") +
                " hasBootstrap=" + (BootstrapState.HasActiveInstance ? "1" : "0"));
        }

'''

marker = "        private void TryMarkEcologyReady()"
if "TryArmEcologyWaitClock" not in text:
    if marker not in text:
        raise SystemExit("FAIL: TryMarkEcologyReady marker missing")
    text = text.replace(marker, helper + marker, 1)

# 5) Also arm clock immediately when FrostTick first runs after GameReady (belt)
# Already covered by TryArm in Update/ColdTick; FrostTick calls TryMarkEcologyReady.
# Add arm at top of FrostTick before TryMark:
old_frost = """        public void FrostTick()
        {
            if (!_started || _finished)
                return;

            TryMarkEcologyReady();"""
new_frost = """        public void FrostTick()
        {
            if (!_started || _finished)
                return;

            TryArmEcologyWaitClock();
            TryMarkEcologyReady();"""
if old_frost not in text:
    raise SystemExit("FAIL: FrostTick head not found")
text = text.replace(old_frost, new_frost, 1)

if text == orig:
    raise SystemExit("FAIL: no changes applied")

RUNNER.write_text(text, encoding="utf-8")
print("OK patched", RUNNER)
print("ecologyWaitStartRealtime field:", "_ecologyWaitStartRealtime" in text)
print("TryArmEcologyWaitClock:", "TryArmEcologyWaitClock" in text)
print("LogEcologyBootstrapTimeoutDiagnostics:", "LogEcologyBootstrapTimeoutDiagnostics" in text)
print("TryFlush in Update:", text.count("TryFlushInitialSceneRebaseBeforeTicks"))
