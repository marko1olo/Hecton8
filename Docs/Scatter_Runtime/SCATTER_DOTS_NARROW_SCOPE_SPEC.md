# Scatter DOTS Narrow Scope Spec

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`
Date: `2026-05-02`

This document defines the only valid DOTS scope for scatter in HECTON-8.

2026-05-04 current-state boundary:

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this scope as current project truth.
- DOTS remains placeholder/seam-only unless package, define, profiler parity, and runtime validation prove otherwise.
- Current source/package check: `com.unity.entities` is absent from `Packages/manifest.json`.
- `Assets/_Project/Scripts/World/Dots` exists as a gated optional assembly lane, but the current files are placeholder/fallback scaffolding.
- Current strict source grep found `0` active `Unity.Entities`, `IComponentData`, `SystemBase`, or `ISystem` references under `Assets/_Project/Scripts`.
- This document constrains future DOTS work; it is not proof that DOTS packages, jobs, or live scatter ownership are production-ready.

## 1. Owner Rule

`WorldProceduralScatterDirector` remains the only runtime owner.

DOTS is allowed to own data-oriented bookkeeping only.

DOTS is not allowed to:

- spawn prefabs
- touch `ObjectPoolManager`
- own save state
- submit GPUI buffers directly
- become a second placement owner

## 2. Allowed DOTS Data Path

The valid data path is:

- `cell state`
- `eligibility`
- `quotas`
- `suppression state`
- `candidate buffers`
- `dirty flags`

This means the backend may simulate and return candidate output, but scene application still belongs to the classic owner path.

## 3. Required Runtime State Model

### 3.1 Cell State

Each cell must carry:

- stable `CellKey`
- local cell coordinates
- sampled height
- height source
- eligibility mask
- suppression state
- dirty flags

### 3.2 Eligibility

Eligibility is per-cell and layer-aware.

Minimum required mask:

- Ground
- Cluster
- Structure
- Spawn

### 3.3 Quotas

Quota state must be explicit per layer:

- placements per cell
- cell stride
- representative family index

### 3.4 Suppression

Suppression must be explicit state, not inferred from candidate count deltas.

Minimum required states:

- None
- Suppressed
- Retained

### 3.5 Dirty Flags

Dirty flags are the gate for recomputation.

Minimum required flags:

- Heights
- Eligibility
- Quotas
- Suppression
- Candidates
- FullRebuild

## 4. Current Code Stance

The current Entities backend is not a live backend.

Current source-backed facts:

- neutral scatter contracts exist in `Assets/_Project/Scripts/World/Contracts`
- `ScatterRuntimeBackendFacade` and `ScatterBackendRuntimeHost` exist as a classic/backend seam under `WorldProceduralScatterDirector`
- `ScatterClassicSimulationBackend` is the only real schedule-capable backend seen in current source
- `ScatterEntitiesSimulationBackend` is a placeholder: `IsInitialized == false`, `TrySchedule(...) == false`, and `TryComplete(...) == false`
- `ScatterEntitiesBackendRegistration` intentionally registers no provider because Unity Entities is not available in this project state
- requesting `EntitiesDots` falls back to `ClassicJobs` when no provider is registered
- live placement ownership remains blocked; `ReservedLiveOwnership` resolves back to shadow mode

What is improved now:

- the contract model now explicitly carries narrow DOTS scope state
- owner-side backend request/config shaping now fills that scope
- owner sampling loop now exports owner-derived per-cell `eligibility`, `suppression`, and `dirty` state instead of relying on default-only masks
- shadow parity now compares per-layer counts plus candidate checksum, not only total candidate delta
- shadow parity now emits an explicit verdict (`Match` / concrete mismatch label) instead of leaving interpretation to inspector math

What is still not finished:

- no live ownership
- no installed `com.unity.entities` package in current `Packages/manifest.json`
- no real Entities package-backed scheduler in current source
- no validated parity beyond shadow/prototype scope
- no profiler proof
- no Burst-proof or frame-level perf proof that this beats classic path
- result publish/compaction still completes on the main thread after the job finishes

## 5. Production Rollout Gate

DOTS may only move beyond prototype if all of the following are true:

1. profiler proves scatter bookkeeping is still a major CPU offender after hybrid cleanup
2. parity proves more than candidate count
3. main-thread gain is real at frame level
4. no second runtime owner is introduced

If those gates fail, DOTS remains seam-only or shadow-only.
