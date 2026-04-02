# WORLD RUNTIME REFERENCE HARDENING — 2026-04-02

## What was wrong

Several world/runtime directors were each trying to find the player and their neighboring directors on their own.

In practice that meant:

- repeated `FindWithTag("Player")` / `GameObject.Find("Player")` attempts during bootstrap
- duplicated `FindAnyObjectByType(...)` lookups across world systems
- extra startup churn exactly while `SceneBootstrap` intentionally kept the player disabled

The hot symptom was not one giant per-frame spike, but a messy stack of repeated scene lookups across:

- `WorldInterestDirector`
- `WorldContentDirector`
- `WorldPopulationDirector`
- `WorldProceduralFillDirector`
- `WorldZoneDirector`
- `WorldSliceDirector`
- `ScatterBudgetController`
- `WorldGenerativeGeologyIntegrationDirector`
- `WorldGenerativeGeologySeamExecutionDirector`

## What was done

- Added a bootstrap-owned fast player reference in `SceneBootstrap`:
  - `HasActiveInstance`
  - `CurrentPlayerObject`
  - `CurrentPlayerTransform`
  - `TryGetCurrentPlayerTransform(...)`
- Added `WorldRuntimeReferenceUtility` as a shared helper for:
  - bootstrap-aware player resolution
  - scene-object fallback resolution
  - `MapMagicBridge` / `ScavengePopulator` singleton-aware resolution
- Updated the listed world/runtime directors to use the shared utility instead of hand-rolled duplicated lookup code.
- Added throttled auto-resolve to directors that previously retried too aggressively:
  - `WorldInterestDirector`
  - `WorldSliceDirector`
  - `ScatterBudgetController`
  - `WorldGenerativeGeologyIntegrationDirector`
  - `WorldGenerativeGeologySeamExecutionDirector`

## What this means in simple terms

The world stack now stops blindly searching for the player while bootstrap is still loading the scene.

Instead:

- bootstrap publishes who the player is
- world systems grab that reference quickly
- slow scene-wide fallback search only happens when it is actually needed

So the startup/runtime path became calmer and less wasteful.

## What this gives

- less duplicated scene lookup work during bootstrap and early runtime
- fewer pointless player searches while the player is intentionally inactive
- more consistent player resolution across world directors
- cleaner foundation for the next profiling pass

## What was verified

- Unity console stayed empty after compile/import and after a short `play -> stop` smoke.
- `WorldRuntimeReferenceUtility.cs` validates cleanly.
- The project already contains `RuntimePerformanceProfiler.cs`, but there is currently no active scene object using that component.

## What remains open

- This pass improves reference churn and bootstrap noise, but it is not yet a full measured profiler pass.
- `RuntimePerformanceProfiler` should be placed in the dev scene or spawned by tooling if we want the next optimization round to be numbers-driven instead of code-driven.
