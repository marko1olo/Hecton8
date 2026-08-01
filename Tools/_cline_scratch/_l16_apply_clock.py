# -*- coding: utf-8 -*-
"""L16: arm step-bounded simulation clock on H8_HeadlessPlayModeProbe (mirror HeadlessSimulationRunner)."""
from __future__ import annotations

import sys

sys.stdout.reconfigure(encoding="utf-8")

PROBE = r"C:\hades\Hecton8\Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs"

with open(PROBE, encoding="utf-8") as f:
    src = f.read()

if "EnsureProbeSimulationClock" in src:
    print("ALREADY_PATCHED")
    sys.exit(0)

# --- 1) Constants + fields after GraceHardTimeoutMarginSeconds ---
ANCHOR_FIELDS = """        private const double GraceHardTimeoutMarginSeconds = 20.0;

        private static int _worldDriverGraceTicks;"""

FIELDS_INSERT = """        private const double GraceHardTimeoutMarginSeconds = 20.0;

        // L16: batchmode WallClock often yields unscaledDeltaTime==0 so RunFixedStepAccumulator
        // early-outs and HPM.FixedTick never runs (hop2 ABSENT, movementIntent01max=0) despite the
        // world driver publishing hot overrides. Mirror HeadlessSimulationRunner.EnsureHeadlessSimulationClock:
        // unpause + headless dilation + EnableStepBoundedTime so the product dispatcher supplies a
        // real fixed unscaled dt per update. Probe is INPUT PRODUCER only via WorldDriver; this is
        // the simulation CLOCK arm, not a mock hop2 path.
        private const float ProbeTimeDilationScalar = 100f;
        private const float ProbeStepBoundedDeltaSeconds = 0.04f;
        private const float ProbeClockEnsureIntervalSeconds = 5f;
        private const uint ProbeSimClockHash = 0x48385043u; // 'H8PC'

        private static double _lastProbeClockEnsureRealtime;
        private static bool _probeSimClockArmed;

        private static int _worldDriverGraceTicks;"""

if ANCHOR_FIELDS not in src:
    print("FAIL: field anchor not found")
    sys.exit(2)
src = src.replace(ANCHOR_FIELDS, FIELDS_INSERT, 1)

# --- 2) GameplayWarmup: arm clock when window starts ---
ANCHOR_WINDOW = """                    if (_gameplayWindowStartedAt <= 0.0)
                    {
                        _gameplayWindowStartedAt = EditorApplication.timeSinceStartup;
                        double transitionTail = _gameplayWindowStartedAt - _phaseStartedAt;
                        if (transitionTail > 1.0)
                        {
                            Debug.Log(
                                $"{Marker} GAMEPLAY window clock starts here, {transitionTail:F3}s after " +
                                "the settle transition. Measured from the transition instead, that tail " +
                                "would have come straight out of the driver's schedule and truncated its " +
                                "last phase.");
                        }
                    }"""

WINDOW_INSERT = """                    if (_gameplayWindowStartedAt <= 0.0)
                    {
                        _gameplayWindowStartedAt = EditorApplication.timeSinceStartup;
                        double transitionTail = _gameplayWindowStartedAt - _phaseStartedAt;
                        if (transitionTail > 1.0)
                        {
                            Debug.Log(
                                $"{Marker} GAMEPLAY window clock starts here, {transitionTail:F3}s after " +
                                "the settle transition. Measured from the transition instead, that tail " +
                                "would have come straight out of the driver's schedule and truncated its " +
                                "last phase.");
                        }

                        // L16: arm product step-bounded clock before any WorldDriver.Begin so FixedTick
                        // can consume locomotion overrides (hop2 path) under batchmode WallClock dt=0.
                        EnsureProbeSimulationClock("gameplay-window-start");
                    }"""

if ANCHOR_WINDOW not in src:
    print("FAIL: gameplay window anchor not found")
    sys.exit(3)
src = src.replace(ANCHOR_WINDOW, WINDOW_INSERT, 1)

# --- 3) Before WorldDriver.Begin + sustain each tick ---
ANCHOR_DRIVER = """                    if (_worldDriverEnabled)
                    {
                        if (!_worldDriverStarted)
                        {
                            _worldDriverStarted = true;
                            H8_HeadlessWorldDriver.Begin();
                            Debug.Log(
                                $"{Marker} WORLDDRIVER begin - producing on SignalBus<PlayerInputSignal> " +
                                "(PLIN) and CoreDeterminismSignals input-override; budget " +
                                $"{H8_HeadlessWorldDriver.TotalBudgetSeconds:F0}s of the " +
                                $"{_gameplaySeconds:F0}s gameplay window");
                        }

                        H8_HeadlessWorldDriver.Tick();
                    }"""

DRIVER_INSERT = """                    if (_worldDriverEnabled)
                    {
                        if (!_worldDriverStarted)
                        {
                            _worldDriverStarted = true;
                            // L16: re-assert clock immediately before Begin in case dispatcher arrived late.
                            EnsureProbeSimulationClock("worlddriver-begin");
                            H8_HeadlessWorldDriver.Begin();
                            Debug.Log(
                                $"{Marker} WORLDDRIVER begin - producing on SignalBus<PlayerInputSignal> " +
                                "(PLIN) and CoreDeterminismSignals input-override; budget " +
                                $"{H8_HeadlessWorldDriver.TotalBudgetSeconds:F0}s of the " +
                                $"{_gameplaySeconds:F0}s gameplay window");
                        }

                        // L16: sustain against late pause / dilation collapse / step-bound drop.
                        MaybeEnsureProbeSimulationClockSustain();
                        H8_HeadlessWorldDriver.Tick();
                    }
                    else
                    {
                        // Undriven measurement still needs FixedTick for hop2/depth observability.
                        MaybeEnsureProbeSimulationClockSustain();
                    }"""

if ANCHOR_DRIVER not in src:
    print("FAIL: worlddriver anchor not found")
    sys.exit(4)
src = src.replace(ANCHOR_DRIVER, DRIVER_INSERT, 1)

# --- 4) ResetRunState latches ---
ANCHOR_RESET = """            _worldDriverStarted = false;
            _placementOwnersRepaired = false;
            _worldDriverGraceTicks = 0;
            _graceOpenedLogged = false;
            _graceClosedLogged = false;
            _gameplayWindowStartedAt = 0.0;
            H8_HeadlessWorldDriver.Reset();
        }"""

RESET_INSERT = """            _worldDriverStarted = false;
            _placementOwnersRepaired = false;
            _worldDriverGraceTicks = 0;
            _graceOpenedLogged = false;
            _graceClosedLogged = false;
            _gameplayWindowStartedAt = 0.0;
            _lastProbeClockEnsureRealtime = 0.0;
            _probeSimClockArmed = false;
            H8_HeadlessWorldDriver.Reset();
        }"""

if ANCHOR_RESET not in src:
    print("FAIL: reset anchor not found")
    sys.exit(5)
src = src.replace(ANCHOR_RESET, RESET_INSERT, 1)

# --- 5) Methods: insert before ResetRunState ---
METHOD_BLOCK = r'''
        /// <summary>
        /// L16 product clock arm for the playmode probe route.
        /// Unpause + re-request headless dilation + enable step-bounded dispatcher time.
        /// Mirrors <c>HeadlessSimulationRunner.EnsureHeadlessSimulationClock</c>.
        /// Batchmode WallClock often yields unscaledDeltaTime==0 so RunFixedStepAccumulator
        /// early-outs and HPM.FixedTick never runs; EnableStepBoundedTime supplies a real fixed
        /// unscaled dt per update. Does not mock hop2, does not call FixedTick/GetState from the probe.
        /// </summary>
        private static void EnsureProbeSimulationClock(string reason)
        {
            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            if (dispatcher == null)
            {
                Debug.Log($"{Marker} SIMCLOCK ensure reason={reason} dispatcher=null");
                return;
            }

            bool wasPaused = dispatcher.SimulationPaused;
            float dilBefore = dispatcher.TimeDilationScalar;
            bool stepBoundBefore = SystemDispatcher.IsStepBoundedTimeActive;

            // ConsumeFrameTimeDilationScalar returns 0 while _simulationPaused — unpause first.
            if (wasPaused)
                dispatcher.RequestSimulationPause(false, ProbeSimClockHash);

            dispatcher.RequestHeadlessTimeDilation(ProbeTimeDilationScalar, ProbeSimClockHash);

            // Real product headless time source (InternalsVisibleTo Hecton8.Editor).
            // Idempotent: EnableStepBoundedTime resets elapsed only when first arming; keep armed.
            bool stepBoundOk = stepBoundBefore;
            if (!stepBoundBefore)
                stepBoundOk = SystemDispatcher.EnableStepBoundedTime(ProbeStepBoundedDeltaSeconds);

            _lastProbeClockEnsureRealtime = EditorApplication.timeSinceStartup;
            _probeSimClockArmed = stepBoundOk || SystemDispatcher.IsStepBoundedTimeActive;

            bool pausedAfter = dispatcher.SimulationPaused;
            float dilAfter = dispatcher.TimeDilationScalar;
            bool stepBoundAfter = SystemDispatcher.IsStepBoundedTimeActive;
            Debug.Log(
                $"{Marker} SIMCLOCK ensure reason={reason}" +
                " pausedBefore=" + (wasPaused ? "1" : "0") +
                " dilBefore=" + dilBefore.ToString("0.###", CultureInfo.InvariantCulture) +
                " dilAfter=" + dilAfter.ToString("0.###", CultureInfo.InvariantCulture) +
                " pausedAfter=" + (pausedAfter ? "1" : "0") +
                " stepBoundBefore=" + (stepBoundBefore ? "1" : "0") +
                " stepBoundAfter=" + (stepBoundAfter ? "1" : "0") +
                " stepBoundOk=" + (stepBoundOk ? "1" : "0") +
                " stepDt=" + ProbeStepBoundedDeltaSeconds.ToString("0.###", CultureInfo.InvariantCulture) +
                " armed=" + (_probeSimClockArmed ? "1" : "0"));
        }

        /// <summary>
        /// During GameplayWarmup, periodically re-assert the real clock against late
        /// SimulationPauseSignal / pause-menu / desync paths that drop step-bound or dilation.
        /// </summary>
        private static void MaybeEnsureProbeSimulationClockSustain()
        {
            if (_lastProbeClockEnsureRealtime > 0.0 &&
                EditorApplication.timeSinceStartup - _lastProbeClockEnsureRealtime < ProbeClockEnsureIntervalSeconds)
            {
                // Cheap path between throttle windows: still force if step-bound dropped.
                if (SystemDispatcher.IsStepBoundedTimeActive)
                {
                    ITickDispatcher d = GlobalRegistry.TickDispatcher;
                    if (d != null &&
                        !d.SimulationPaused &&
                        d.TimeDilationScalar + 0.01f >= ProbeTimeDilationScalar)
                        return;
                }
            }

            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            if (dispatcher == null)
                return;

            bool needsRestore = dispatcher.SimulationPaused ||
                                dispatcher.TimeDilationScalar + 0.01f < ProbeTimeDilationScalar ||
                                !SystemDispatcher.IsStepBoundedTimeActive;
            if (!needsRestore)
            {
                _lastProbeClockEnsureRealtime = EditorApplication.timeSinceStartup;
                return;
            }

            EnsureProbeSimulationClock("gameplay-sustain");
        }

'''

# Insert methods immediately before private static void ResetRunState
RESET_SIG = "        private static void ResetRunState()"
if RESET_SIG not in src:
    # try without static ordering
    idx = src.find("void ResetRunState()")
    if idx < 0:
        print("FAIL: ResetRunState signature not found")
        sys.exit(6)
    # find line start
    line_start = src.rfind("\n", 0, idx) + 1
    src = src[:line_start] + METHOD_BLOCK + src[line_start:]
else:
    src = src.replace(RESET_SIG, METHOD_BLOCK + RESET_SIG, 1)

with open(PROBE, "w", encoding="utf-8", newline="\n") as f:
    f.write(src)

# verify
checks = [
    "EnsureProbeSimulationClock",
    "MaybeEnsureProbeSimulationClockSustain",
    "ProbeStepBoundedDeltaSeconds",
    "SIMCLOCK ensure",
    "_probeSimClockArmed = false",
    "gameplay-window-start",
    "worlddriver-begin",
]
with open(PROBE, encoding="utf-8") as f:
    final = f.read()
ok = all(c in final for c in checks)
print("VERIFY", "OK" if ok else "FAIL")
for c in checks:
    print(" ", c, "YES" if c in final else "MISSING")
print("lines", final.count("\n") + 1)
