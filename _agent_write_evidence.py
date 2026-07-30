# -*- coding: utf-8 -*-
from pathlib import Path

repo = Path(r"C:\hades\Hecton8")

ev = repo / "Docs" / "AgentLogs" / "p0_ecology_ready_frost_starve_20260731.md"
ev.parent.mkdir(parents=True, exist_ok=True)
ev.write_text(
    """# P0 ecology-ready Frost starve — 2026-07-31

## Prior state
- FO soft-deadlock fixed+pushed (see p0_fo_bootstrap_lock_drain_20260731).
- Live smoke proved foLock=0 dispBootstrapLocked=0 after GameReady.
- Progress lines: ecoInit=1 from first sample t=0.0s through t=480s+.
- Never flipped _ecologyReady → BOOTSTRAP_TIMEOUT budget burns with no sim advance.

## Root cause
TryMarkEcologyReady() lived only on IFrostTickable.FrostTick.
readyNow = ecosystem != null && ecosystem.IsInitialized was true the entire wait.
Mark path never invoked because Frost did not deliver (dispatcher gates / deltaTime<=0 / interval accumulator never fed).

Ready-mark is a **gate**, not a substitute for sim ticks. Same starvation-proof pattern as moving the ecology wait clock off ColdTick onto MonoBehaviour.Update (p0_gameready).

## Fix (product)
HeadlessSimulationRunner.Update wait block:
1. TryArmEcologyWaitClock()
2. TryMarkEcologyReady()  // NEW
3. early return if ready
4. FO flush + wait progress (pre-ready only)

On first ready transition: LogRunnerLifecycle("ecology ready (ecosystem initialized)") before log filter muzzles Log.

Wait progress diag adds frostReg + dispFrameLocked (InternalsVisibleTo).

## Still open after this fix
- Day-boundary debt still queued in FrostTick; if Frost remains starved post-ready, days will not advance.
- If live smoke shows ready + timeDilationDelivered==0 / zero days → fix dilation/pause root in SystemDispatcher path next.
- Real-game screenshots still DECLINED until interactive proof.

## DoD
status not in {ECOLOGY_UNAVAILABLE, BATCH_TIMEOUT, BOOTSTRAP_TIMEOUT};
ecologySampledDays>0; timeDilationDelivered>0; no error CS.
""",
    encoding="utf-8",
)
print("evidence", ev, ev.exists(), ev.stat().st_size)

bl_path = repo / "BACKLOG.md"
bl = bl_path.read_text(encoding="utf-8")
entry = """
## Open — P0 ecology-ready Frost starve (2026-07-31)
- **Symptom (live smoke after FO lock-drain):** foLock=0 ecoInit=1 from t=0 through t=480s+; `_ecologyReady` never set; BOOTSTRAP_TIMEOUT / BATCH_TIMEOUT.
- **Root:** `TryMarkEcologyReady` only invoked from `FrostTick`. Frost never delivered while wait clock ran (dispatcher master-sim path starved or deltaTime<=0). Ready predicate (`ecosystem.IsInitialized`) was true the entire wait.
- **Fix applied:** call `TryMarkEcologyReady` from runner `Update` wait path (starvation-proof gate, same pattern as wait-clock move off ColdTick). Lifecycle log on first ready. Wait-progress adds frostReg + dispFrameLocked.
- **Not a mock:** ready-mark is a harness gate; day audits still require Frost/LateFrame once ready. Frost starve root for day advance remains open if dilation/pause zeros master sim.
- Evidence: Docs/AgentLogs/p0_ecology_ready_frost_starve_20260731.md
"""
if "P0 ecology-ready Frost starve" not in bl:
    if bl.startswith("#"):
        # insert after first line
        nl = bl.find("\n")
        if nl > 0:
            bl = bl[: nl + 1] + entry + bl[nl + 1 :]
        else:
            bl = bl + entry
    else:
        bl = entry + bl
    bl_path.write_text(bl, encoding="utf-8")
    print("BACKLOG updated")
else:
    print("BACKLOG already has entry")

runner = (repo / "Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs").read_text(
    encoding="utf-8"
)
print("Ready-mark", "Ready-mark is a gate" in runner)
print("lifecycle", "ecology ready (ecosystem initialized)" in runner)
print("frostReg", "frostReg=" in runner)
print("frameLocked", "dispFrameLocked=" in runner)
