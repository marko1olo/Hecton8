# -*- coding: utf-8 -*-
"""Apply L17 FO-drain product fix to Probe + SystemDispatcher LateFrame."""
from pathlib import Path

ROOT = Path(r"C:\hades\Hecton8")
PROBE = ROOT / r"Assets\_Project\Scripts\Editor\Diagnostics\H8_HeadlessPlayModeProbe.cs"
SD = ROOT / r"Assets\_Project\Scripts\Core\SystemDispatcher.cs"


def must_replace(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise SystemExit(f"FAIL {label}: search block not found")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"FAIL {label}: expected 1 match, got {count}")
    return text.replace(old, new, 1)


# ---- Probe ----
probe = PROBE.read_text(encoding="utf-8")

old_fields = """        private static double _lastProbeClockEnsureRealtime;
        private static bool _probeSimClockArmed;

        private static int _worldDriverGraceTicks;"""

new_fields = """        private static double _lastProbeClockEnsureRealtime;
        private static bool _probeSimClockArmed;

        // L17: HSR drains FO scene-rebase every Update while bootstrap lock can starve FixedTick
        // (RunDispatcherUpdate returns after PreSim when IsOriginShiftBootstrapLocked and TryFlush
        // cannot clear; LateFrame hard-returns on the same lock without TryFlush). Probe never
        // called TryFlush — hop1/presim advanced while lateFrameTick/pumpFired froze and hop2
        // stayed ABSENT. Mirror HSR FO drain + throttled FODRAIN snapshot (not a hop2 mock).
        private const double ProbeFoDrainDiagIntervalSeconds = 5.0;
        private static double _lastProbeFoDrainDiagRealtime;
        private static int _probeFoDrainCalls;
        private static int _probeFoDrainCleanCount;

        private static int _worldDriverGraceTicks;"""
probe = must_replace(probe, old_fields, new_fields, "probe-fields")

old_gameplay = """                        // L16: arm product step-bounded clock before any WorldDriver.Begin so FixedTick
                        // can consume locomotion overrides (hop2 path) under batchmode WallClock dt=0.
                        EnsureProbeSimulationClock("gameplay-window-start");
                    }

                    // Content before measurement. This runs BEFORE the driver starts and OUTSIDE the
                    // _worldDriverEnabled branch on purpose: a -h8SkipWorldDriver run is supposed to
                    // measure an UNDRIVEN world, not an EMPTY one, and those are different claims.
                    if (!_placementOwnersRepaired)
                    {
                        _placementOwnersRepaired = true;
                        EnableDisabledPlacementOwnersInMemory();
                    }

                    // SECOND determinism-owner observation: the world scene has arrived, so every
                    // LoadSceneMode.Single of the boot route has already happened. Comparing this against
                    // the BootWarmup sample brackets the window in which the owner disappeared, in ONE run.
                    // Latched by the sample's own Taken flag; the cold FindObjectsByType inside runs once.
                    if (!_determinismOwnerAtGameplayStart.Taken)
                    {
                        SampleDeterminismOwner(ref _determinismOwnerAtGameplayStart, "FirstGameplayTick");

                        // Order matters and is not cosmetic: the sample above is the EVIDENCE, and a revive
                        // that ran first would overwrite the one observation that proves the owner is
                        // missing in the shipped route.
                        if (_determinismReviveRequested)
                            TryReviveDeterminismOwner();
                    }

                    // The world driver rides THIS tick. It gets no Update, no coroutine and no timer of
                    // its own, so the schedule advances only while the probe is genuinely pumping the
                    // engine - the same discipline that stops "yield return null" hanging a batchmode run.
                    if (_worldDriverEnabled)
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

new_gameplay = """                        // L16: arm product step-bounded clock before any WorldDriver.Begin so FixedTick
                        // can consume locomotion overrides (hop2 path) under batchmode WallClock dt=0.
                        EnsureProbeSimulationClock("gameplay-window-start");
                        // L17: drain FO bootstrap lock before first driver tick (HSR parity).
                        DrainProbeFloatingOriginBootstrap("gameplay-window-start");
                    }

                    // Content before measurement. This runs BEFORE the driver starts and OUTSIDE the
                    // _worldDriverEnabled branch on purpose: a -h8SkipWorldDriver run is supposed to
                    // measure an UNDRIVEN world, not an EMPTY one, and those are different claims.
                    if (!_placementOwnersRepaired)
                    {
                        _placementOwnersRepaired = true;
                        EnableDisabledPlacementOwnersInMemory();
                    }

                    // SECOND determinism-owner observation: the world scene has arrived, so every
                    // LoadSceneMode.Single of the boot route has already happened. Comparing this against
                    // the BootWarmup sample brackets the window in which the owner disappeared, in ONE run.
                    // Latched by the sample's own Taken flag; the cold FindObjectsByType inside runs once.
                    if (!_determinismOwnerAtGameplayStart.Taken)
                    {
                        SampleDeterminismOwner(ref _determinismOwnerAtGameplayStart, "FirstGameplayTick");

                        // Order matters and is not cosmetic: the sample above is the EVIDENCE, and a revive
                        // that ran first would overwrite the one observation that proves the owner is
                        // missing in the shipped route.
                        if (_determinismReviveRequested)
                            TryReviveDeterminismOwner();
                    }

                    // The world driver rides THIS tick. It gets no Update, no coroutine and no timer of
                    // its own, so the schedule advances only while the probe is genuinely pumping the
                    // engine - the same discipline that stops "yield return null" hanging a batchmode run.
                    if (_worldDriverEnabled)
                    {
                        if (!_worldDriverStarted)
                        {
                            _worldDriverStarted = true;
                            // L16: re-assert clock immediately before Begin in case dispatcher arrived late.
                            EnsureProbeSimulationClock("worlddriver-begin");
                            // L17: FO drain before Begin so FixedTick path is not permanently early-out.
                            DrainProbeFloatingOriginBootstrap("worlddriver-begin");
                            H8_HeadlessWorldDriver.Begin();
                            Debug.Log(
                                $"{Marker} WORLDDRIVER begin - producing on SignalBus<PlayerInputSignal> " +
                                "(PLIN) and CoreDeterminismSignals input-override; budget " +
                                $"{H8_HeadlessWorldDriver.TotalBudgetSeconds:F0}s of the " +
                                $"{_gameplaySeconds:F0}s gameplay window");
                        }

                        // L16: sustain against late pause / dilation collapse / step-bound drop.
                        MaybeEnsureProbeSimulationClockSustain();
                        // L17: HSR-parity FO drain every gameplay tick (FixedTick starvation root).
                        DrainProbeFloatingOriginBootstrap("gameplay-tick");
                        H8_HeadlessWorldDriver.Tick();
                    }
                    else
                    {
                        // Undriven measurement still needs FixedTick for hop2/depth observability.
                        MaybeEnsureProbeSimulationClockSustain();
                        DrainProbeFloatingOriginBootstrap("gameplay-tick-undriven");
                    }"""
probe = must_replace(probe, old_gameplay, new_gameplay, "probe-gameplay")

old_clock_end = """            EnsureProbeSimulationClock("gameplay-sustain");
        }

        private static void ResetRunState()
        {"""

new_clock_end = """            EnsureProbeSimulationClock("gameplay-sustain");
        }

        /// <summary>
        /// L17 product FO drain for the playmode probe route.
        /// Mirrors <c>HeadlessSimulationRunner.Update</c> calling
        /// <c>HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks</c> every tick while
        /// <c>IsOriginShiftBootstrapLocked</c> can starve FixedTick after PreSim and freeze LateFrame.
        /// Probe is still INPUT PRODUCER only via WorldDriver; this is external FO drain (designed
        /// product path — FO.Tick itself is blocked by the same lock). Does not mock hop2.
        /// </summary>
        private static void DrainProbeFloatingOriginBootstrap(string reason)
        {
            bool flushClean = HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks();
            _probeFoDrainCalls++;
            if (flushClean)
                _probeFoDrainCleanCount++;

            double now = EditorApplication.timeSinceStartup;
            bool forceFirst = _lastProbeFoDrainDiagRealtime <= 0.0;
            bool intervalDue = forceFirst ||
                               (now - _lastProbeFoDrainDiagRealtime) >= ProbeFoDrainDiagIntervalSeconds;
            // Always emit when lock still held after a drain attempt so LIVE can prove residual.
            bool lockHeld = SystemDispatcher.IsOriginShiftBootstrapLocked;
            if (!intervalDue && !lockHeld)
                return;

            _lastProbeFoDrainDiagRealtime = now;

            HectonFloatingOrigin.CopyBootstrapDrainSnapshot(
                out bool foHasOrigin,
                out bool foShift,
                out bool foPhysicsPause,
                out bool foLock,
                out int foPendingScenes,
                out bool foTargetsDirty,
                out bool foBarrier);

            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            bool paused = dispatcher != null && dispatcher.SimulationPaused;
            float dil = dispatcher != null ? dispatcher.TimeDilationScalar : -1f;

            Debug.Log(
                $"{Marker} FODRAIN reason={reason}" +
                " flushClean=" + (flushClean ? "1" : "0") +
                " calls=" + _probeFoDrainCalls.ToString(CultureInfo.InvariantCulture) +
                " clean=" + _probeFoDrainCleanCount.ToString(CultureInfo.InvariantCulture) +
                " foHasOrigin=" + (foHasOrigin ? "1" : "0") +
                " foShift=" + (foShift ? "1" : "0") +
                " foPhysicsPause=" + (foPhysicsPause ? "1" : "0") +
                " foLock=" + (foLock ? "1" : "0") +
                " foPendingScenes=" + foPendingScenes.ToString(CultureInfo.InvariantCulture) +
                " foTargetsDirty=" + (foTargetsDirty ? "1" : "0") +
                " foBarrier=" + (foBarrier ? "1" : "0") +
                " dispBoot=" + (lockHeld ? "1" : "0") +
                " dispFrame=" + (SystemDispatcher.IsOriginShiftFrameLockedForCurrentFrame ? "1" : "0") +
                " paused=" + (paused ? "1" : "0") +
                " dil=" + dil.ToString("0.###", CultureInfo.InvariantCulture) +
                " stepBound=" + (SystemDispatcher.IsStepBoundedTimeActive ? "1" : "0") +
                " gameReady=" + (BootstrapState.IsGameReady ? "1" : "0"));
        }

        private static void ResetRunState()
        {"""
probe = must_replace(probe, old_clock_end, new_clock_end, "probe-drain-method")

old_reset = """            _lastProbeClockEnsureRealtime = 0.0;
            _probeSimClockArmed = false;
            H8_HeadlessWorldDriver.Reset();
        }"""

new_reset = """            _lastProbeClockEnsureRealtime = 0.0;
            _probeSimClockArmed = false;
            _lastProbeFoDrainDiagRealtime = 0.0;
            _probeFoDrainCalls = 0;
            _probeFoDrainCleanCount = 0;
            H8_HeadlessWorldDriver.Reset();
        }"""
probe = must_replace(probe, old_reset, new_reset, "probe-reset")

PROBE.write_text(probe, encoding="utf-8")
print("PROBE OK", flush=True)

# ---- SystemDispatcher LateFrame ----
sd = SD.read_text(encoding="utf-8")

old_late = """            if (IsOriginShiftBootstrapLocked)
                return;
            if (IsOriginShiftFrameLockedForCurrentFrame)
            {
                _dataVault?.UnlockAllocationsAfterAupShift(_aupPreShiftPauseSequence);
                return;
            }"""

new_late = """            // L17: parity with RunDispatcherUpdate — TryFlush before bootstrap-lock hard-return.
            // LateFrame previously returned without draining FO; when SceneRebaseTickLock stuck,
            // InputDispatcher lateFrameTick/pumpFired froze while PreSim still advanced (L16 LIVE:
            // lateFrameTick=49 pumpFired=1 sticky). External TryFlush is the designed drain path
            // (FO.Tick itself is on master lanes blocked by the same lock).
            if (IsOriginShiftBootstrapLocked)
            {
                if (!HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks())
                    return;

                if (IsOriginShiftBootstrapLocked)
                    return;
            }
            if (IsOriginShiftFrameLockedForCurrentFrame)
            {
                _dataVault?.UnlockAllocationsAfterAupShift(_aupPreShiftPauseSequence);
                return;
            }"""
sd = must_replace(sd, old_late, new_late, "sd-lateframe")

SD.write_text(sd, encoding="utf-8")
print("SD OK", flush=True)
print("L17 APPLY COMPLETE", flush=True)
