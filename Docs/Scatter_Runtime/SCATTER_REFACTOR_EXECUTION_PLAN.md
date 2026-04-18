# Scatter Refactor Execution Plan

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`
Owner: `Codex`
Scope: `WorldProceduralScatterDirector` follow-up against `Docs/Scatter_Runtime/SCATTER_REFACTORING_MANIFESTO_V2.md`

---

## Objective

Close real scatter architecture gaps without fake completion claims.

Current truth:

- async sampling state machine exists
- `FastCandidateMap` exists
- `ScatterWorkingMemory` exists
- multi-floor placement keys exist
- teardown order already calls `Complete()` before `Dispose()`

Still missing or incomplete:

- monolith still huge (`WorldProceduralScatterDirector.cs` remains main owner)
- `SamplingSnapshot` not separate plain file and not full snapshot contract
- diagnostics still live inside director
- spawning/reconcile still mostly inside director
- alternate scatter path files exist but are not integrated cleanly
- no runtime profiler/log proof in this pass
- `ScatterHeuristicsUtility` file absent
- `ScatterSpawningService` file absent
- `ScatterRescueContext` file absent
- `GetGridPlacements()` accessor absent
- `ScatterWorkingMemory` still uses many managed containers, not manifesto-grade data buffers

---

## Execution Order

### Phase 1 â€” Low-Risk Structural Fixes

Status: `IN PROGRESS`

- [x] create execution ledger
- [x] extract `SamplingSnapshot` into standalone file
- [x] expand snapshot payload:
  - `CenterCellX`
  - `CenterCellZ`
  - `PlayerPosition`
  - `CaptureTime`
- [x] remove nested snapshot type from pipeline partial
- [x] wire pipeline to use snapshot data only, not implicit future transform reads

### Phase 2 â€” Diagnostics Extraction

Status: `PENDING`

- [x] identify dev-only diagnostics/report helpers safe to move first
- [x] create `ScatterDiagnosticsTracker` for rebuild report/log path
- [ ] keep serialized debug inspector state in director until safe migration path exists
- [ ] do not break inspector references

### Phase 3 â€” Spawning/Reconcile Extraction

Status: `PENDING`

- [ ] isolate budget/cadence logic from director reconcile loop
- [ ] extract stateless batch helper where logic is actually separable
- [ ] avoid fake service wrapper with no ownership gain
- [ ] preserve GPUI path and generated geology path

### Phase 4 â€” Heuristics Extraction

Status: `PENDING`

- [x] create `ScatterHeuristicsUtility`
- [x] move pure heat/depth math into helper
- [ ] move remaining pure pattern/domain scoring into helper
- [ ] keep zero-GC static methods only
- [ ] leave stateful/runtime owner logic in director

### Phase 5 â€” Dead Branch Cleanup

Status: `PENDING`

- [x] audit `ScatterEvaluator.cs` and removed backend-side reconcile stack
- [x] decide:
  - integrate fully
  - quarantine
  - delete
- [ ] do not keep two competing scatter architectures alive

### Phase 6 â€” Verification

Status: `PENDING`

- [ ] compile check in Unity
- [ ] console check
- [ ] runtime smoke in scene
- [ ] profiler numbers:
  - sampling wait
  - rebuild total
  - GC alloc
  - active placement count

### Phase 7 â€” API / Ownership Gaps

Status: `PENDING`

- [x] add read-only `GetGridPlacements()` access path safely
- [ ] decide final public/internal contract for `ScatterPlacement`
- [ ] decide whether current grid structure stays bucketed or needs manifesto-shape rewrite

---

## Hard Constraints

- no edits to `ComposeKey`, `ComputeRuleIdHash`, `ComputeStableHash`, `StableRandom01`
- no broken serialization
- no new MonoBehaviour scatter components
- no optimistic completion claims without logs

---

## Risks

- `WARNING: Regression risk in scatter determinism/save compatibility` if hash/key path touched wrong
- `WARNING: Regression risk in inspector wiring` if serialized ownership moves too early
- `WARNING: Regression risk in GPUI flora path` if spawn extraction ignores existing runtime split

---

## Current Pass

Work started with Phase 1 and first low-risk Phase 2 extraction.
Goal: make architecture more truthful with low blast radius, not pretend full manifesto complete in one jump.

