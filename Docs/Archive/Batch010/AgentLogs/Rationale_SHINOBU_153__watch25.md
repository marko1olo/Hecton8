# SHINOBU_153 Rationale

Date: 2026-05-20
Status: PENDING VERIFICATION

## Decision 01 - Active State Recovery

Problem: Active `Status_SHINOBU_153.md` and `Rationale_SHINOBU_153.md` were absent while SHINOBU_153 source and the binary payload ledger already contained a procedural geology lane.
Solution: Recreated active state files from current source, current batch prompt, and ledger evidence. Marked runtime proof pending instead of fabricating completion.
Rejected Alternatives: Trusting chat memory; reading archived combined status as current truth without active files.
Scalability potential: Restores deterministic tracking for low/middle/high/ultra verification without touching runtime code.
Hardware Impact: No runtime change.

## Decision 02 - Editor Facade Compile-Risk Fix

Problem: `ProceduralResourceTunerWindow` used `math.isfinite` without importing `Unity.Mathematics`, which would fail compilation in the Editor asmdef.
Solution: Added the missing `using Unity.Mathematics;` import only.
Rejected Alternatives: Replacing math calls with `float.IsFinite` would diverge from Unity.Mathematics style used in adjacent runtime code.
Scalability potential: Keeps designer tuning row validation aligned with runtime math.
Hardware Impact: Editor-only; runtime frame cost unchanged.

## Decision 03 - DrawProceduralIndirect Boundary

Problem: The XML asks for `Graphics.DrawProceduralIndirect`, while current source uses `Graphics.RenderMeshIndirect` with an ore mesh and indexed args.
Solution: Keep current mesh-indirect path pending shader audit. A correct procedural route requires a shader that expands vertices from `SV_VertexID` and consumes a 16-byte procedural args row. Renaming the API call without this shader contract would be false architecture.
Rejected Alternatives: Blind API swap to satisfy text; CPU-instantiated mesh proxies; `GraphicsBuffer.SetData`.
Scalability potential: Low tiers already benefit from continuous visual-cluster decimation; high/ultra can move to procedural vertex expansion after shader ABI is proven.
Hardware Impact: Current path avoids GameObject/proxy spikes; exact MX350 us pending Frame Debugger/profiler.
