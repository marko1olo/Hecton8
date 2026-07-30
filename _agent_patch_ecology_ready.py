# -*- coding: utf-8 -*-
from pathlib import Path

p = Path(r"C:\hades\Hecton8\Assets\_Project\Scripts\QA\Headless\HeadlessSimulationRunner.cs")
t = p.read_text(encoding="utf-8")
orig = t

old = """            if (_started && !_ecologyReady)
            {
                TryArmEcologyWaitClock();
                // Keep FO scene-rebase barrier draining while we wait — dispatcher early-returns
                // all Frost/LateFrame while IsOriginShiftBootstrapLocked holds.
                HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks();
                MaybeLogEcologyWaitProgress();"""

new = """            if (_started && !_ecologyReady)
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
                MaybeLogEcologyWaitProgress();"""

if old not in t:
    raise SystemExit("UPDATE block not found")
t = t.replace(old, new, 1)

old2 = """            if (readyNow && !_ecologyReady)
            {
                _simulationStartRealtime = Time.realtimeSinceStartupAsDouble;

                // Muzzle Debug.Log HERE, not in ForceHeadlessRuntimePolicy. The filter exists so a 100-day"""

new2 = """            if (readyNow && !_ecologyReady)
            {
                _simulationStartRealtime = Time.realtimeSinceStartupAsDouble;
                LogRunnerLifecycle("ecology ready (ecosystem initialized)");

                // Muzzle Debug.Log HERE, not in ForceHeadlessRuntimePolicy. The filter exists so a 100-day"""

if old2 not in t:
    raise SystemExit("TryMark block not found")
t = t.replace(old2, new2, 1)

old3 = """            LogRunnerLifecycle(
                "ecology wait progress t=" + waited.ToString("0.0", CultureInfo.InvariantCulture) +
                "s ecoNull=" + (ecoNull ? "1" : "0") +
                " ecoInit=" + (ecoInit ? "1" : "0") +
                " foHasOrigin=" + (foHasOrigin ? "1" : "0") +
                " foShift=" + (foShift ? "1" : "0") +
                " foPhysicsPause=" + (foPhysicsPause ? "1" : "0") +
                " foLock=" + (foLock ? "1" : "0") +
                " foPendingScenes=" + foPendingScenes.ToString(CultureInfo.InvariantCulture) +
                " foTargetsDirty=" + (foTargetsDirty ? "1" : "0") +
                " foBarrier=" + (foBarrier ? "1" : "0") +
                " dispBootstrapLocked=" + (SystemDispatcher.IsOriginShiftBootstrapLocked ? "1" : "0"));"""

new3 = """            LogRunnerLifecycle(
                "ecology wait progress t=" + waited.ToString("0.0", CultureInfo.InvariantCulture) +
                "s ecoNull=" + (ecoNull ? "1" : "0") +
                " ecoInit=" + (ecoInit ? "1" : "0") +
                " frostReg=" + (_registeredFrost ? "1" : "0") +
                " foHasOrigin=" + (foHasOrigin ? "1" : "0") +
                " foShift=" + (foShift ? "1" : "0") +
                " foPhysicsPause=" + (foPhysicsPause ? "1" : "0") +
                " foLock=" + (foLock ? "1" : "0") +
                " foPendingScenes=" + foPendingScenes.ToString(CultureInfo.InvariantCulture) +
                " foTargetsDirty=" + (foTargetsDirty ? "1" : "0") +
                " foBarrier=" + (foBarrier ? "1" : "0") +
                " dispBootstrapLocked=" + (SystemDispatcher.IsOriginShiftBootstrapLocked ? "1" : "0") +
                " dispFrameLocked=" + (SystemDispatcher.IsOriginShiftFrameLockedForCurrentFrame ? "1" : "0"));"""

if old3 not in t:
    raise SystemExit("progress log block not found")
t = t.replace(old3, new3, 1)

if t == orig:
    raise SystemExit("no changes")

p.write_text(t, encoding="utf-8")
print("OK patched", p)
print("Ready-mark", "Ready-mark is a gate" in t)
print("lifecycle", "ecology ready (ecosystem initialized)" in t)
print("frostReg", "frostReg=" in t)
print("frameLocked", "dispFrameLocked=" in t)
