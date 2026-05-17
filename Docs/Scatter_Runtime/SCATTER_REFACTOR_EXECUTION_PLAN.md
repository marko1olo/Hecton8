# Scatter Refactor Execution Plan

Date: 2026-05-07
Status: PENDING VERIFICATION
Verification: `PENDING VERIFICATION`
Owner: `Codex`
Scope: `WorldProceduralScatterDirector` follow-up against `Docs/Scatter_Runtime/SCATTER_REFACTORING_MANIFESTO_V2.md`

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- May 14 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat that May 11 compile-success line as stale report text. R43 rechecked the current external root `Hecton8*.csproj` no-restore CLI compile surface at `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; full restore graphs still carry vendor/package warnings, and shared `Temp\obj` locks can create transient evidence noise. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.

## 2026-05-14 DOC_AUDIT R46 Source Reality Recheck

Evidence class: STATIC_SOURCE / FILESYSTEM. No Unity scene load, inspector readback, Play Mode, profiler, Memory Profiler, GCMonitor, player build, or visual validation was run.

Current source inventory:

- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`: `539165` bytes / `11907` lines. The director is still the main runtime owner and is larger than the older planning-language inventory.
- `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs`: `42878` bytes / `590` lines. `ScatterWorkingMemory` exists, owns NativeCollections and many managed dictionaries/lists/stacks that are intended as cold/prewarmed working memory, not manifesto-grade pure native SOA.
- `Assets/_Project/Scripts/World/SamplingSnapshot.cs`: `733` bytes / `28` lines. `SamplingSnapshot` is now a standalone readonly struct with runtime/absolute center, center cell, and capture time.
- `Assets/_Project/Scripts/World/ScatterHeuristicsUtility.cs`: `5698` bytes / `95` lines. The helper exists and currently covers depth/domain scale and spawn depth scale math; remaining pattern/domain scoring is not fully extracted.
- `Assets/_Project/Scripts/World/ScatterDiagnosticsTracker.cs`: `6263` bytes / `138` lines. Rebuild report/snapshot emission was extracted into a static helper, but serialized inspector debug fields still intentionally live in `WorldProceduralScatterDirector`.
- `Assets/_Project/Scripts/WorldProceduralScatterDirectorRescueContexts.cs`: `8619` bytes / `157` lines. `ScatterRescueContext` exists as a private readonly struct inside a director partial; it is not the standalone `ref struct` shape described by the manifesto.
- `WorldProceduralScatterDirector.GetGridPlacements()` exists at source level and returns `IReadOnlyDictionary<long, List<ScatterPlacement>>`, not the manifesto's flat `IReadOnlyDictionary<long, ScatterPlacement>` shape.
- A `ScatterSpawningService` source file/class was not found in the scoped source search. Spawn/reconcile batching still lives in the director path around `ReconcileInstances`, `ContinuePendingScatterReconcile`, and `ReconcileDesiredPlacements`.
- DOTS remains optional/disabled: `Packages/manifest.json` does not declare `com.unity.entities`; `Hecton8.World.Dots.asmdef` is auto-reference disabled and gated by `HECTON8_ENABLE_ENTITIES_DOTS`, `HECTON8_HAS_ENTITIES_PACKAGE`, and `HECTON8_ENABLE_OPTIONAL_ASSEMBLIES`. Source scan found no `Unity.Entities`, `IComponentData`, or `SystemBase` use in active `.cs` files; bare `ISystem` hits are the first-party `Hecton8.Core.ISystem`, not ECS proof.

R46 correction to this plan:

- Treat earlier "file absent" bullets as stale where the files now exist.
- Treat checked extraction items as source-present only, not runtime-validated.
- Keep Phase 6 fully pending until artifact-backed Unity/scene/profiler captures exist.
## 2026-05-04 Current-State Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this plan as current runtime truth.
- This is an execution plan for the scatter owner cleanup, not proof that scatter is profiler-clean, runtime-correct, or fully refactored.
- Native memory teardown notes in older scatter docs must be rechecked against current `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`; do not blindly preserve local `.Complete()` barriers.
- Source files and fresh Unity/profiler evidence win over this plan.

---

## Objective

Close real scatter architecture gaps without fake completion claims.

Current truth:

- async sampling state machine exists
- `FastCandidateMap` exists
- `ScatterWorkingMemory` exists
- standalone `SamplingSnapshot` exists
- `ScatterHeuristicsUtility` exists for depth/domain scale math
- `ScatterDiagnosticsTracker` exists for rebuild report/snapshot emission
- private director-partial `ScatterRescueContext` exists
- read-only `GetGridPlacements()` accessor exists, but returns bucketed grid placement lists
- multi-floor placement keys exist
- teardown order had already been reviewed as `Complete()` before `Dispose()` in the older path; current work must re-check this against the active deferred-disposal mandate instead of copying local barriers blindly

Still missing or incomplete:

- monolith still huge (`WorldProceduralScatterDirector.cs` remains main owner at `11907` lines in R46 static scan)
- `SamplingSnapshot` is source-present, but runtime correctness still lacks scene/profiler proof
- diagnostics are split: rebuild report logic is in `ScatterDiagnosticsTracker`, while serialized debug inspector state still lives inside director by design
- spawning/reconcile still mostly inside director
- alternate scatter path files exist but are not integrated cleanly
- no runtime profiler/log proof in this pass
- `ScatterSpawningService` file absent
- `ScatterRescueContext` is not a standalone `ref struct`; it is a private readonly struct in a director partial and still references managed arrays/dictionaries
- `GetGridPlacements()` is present but exposes bucketed `List<ScatterPlacement>` values, so final API shape remains undecided
- `ScatterWorkingMemory` still uses many managed containers, not manifesto-grade data buffers

---

## Execution Order

### Phase 1 Ã¢â‚¬â€ Low-Risk Structural Fixes

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

### Phase 2 Ã¢â‚¬â€ Diagnostics Extraction

Status: `PENDING`

- [x] identify dev-only diagnostics/report helpers safe to move first
- [x] create `ScatterDiagnosticsTracker` for rebuild report/log path
- [ ] keep serialized debug inspector state in director until safe migration path exists
- [ ] do not break inspector references

### Phase 3 Ã¢â‚¬â€ Spawning/Reconcile Extraction

Status: `PENDING`

- [ ] isolate budget/cadence logic from director reconcile loop
- [ ] extract stateless batch helper where logic is actually separable
- [ ] avoid fake service wrapper with no ownership gain
- [ ] preserve GPUI path and generated geology path

### Phase 4 Ã¢â‚¬â€ Heuristics Extraction

Status: `PENDING`

- [x] create `ScatterHeuristicsUtility`
- [x] move pure heat/depth math into helper
- [ ] move remaining pure pattern/domain scoring into helper
- [ ] keep zero-GC static methods only
- [ ] leave stateful/runtime owner logic in director

### Phase 5 Ã¢â‚¬â€ Dead Branch Cleanup

Status: `PENDING`

- [x] audit `ScatterEvaluator.cs` and removed backend-side reconcile stack
- [x] decide:
  - integrate fully
  - quarantine
  - delete
- [ ] do not keep two competing scatter architectures alive

### Phase 6 Ã¢â‚¬â€ Verification

Status: `PENDING`

- [ ] compile check in Unity
- [ ] console check
- [ ] runtime smoke in scene
- [ ] profiler numbers:
  - sampling wait
  - rebuild total
  - GC alloc
  - active placement count

### Phase 7 Ã¢â‚¬â€ API / Ownership Gaps

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
