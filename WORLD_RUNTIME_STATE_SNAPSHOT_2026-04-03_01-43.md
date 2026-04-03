# WORLD RUNTIME STATE SNAPSHOT — 2026-04-03 01:43

## Trace

- `C:\Users\danat\AppData\LocalLow\Danat Games\Submerge\Diagnostics\Hecton8_runtime_2026-04-03_01-43-47.log`

## What Changed Before This Run

### Honest failed step before this

The previous `helper-only` pass moved primitive creation away from `CreatePrimitive + destroy collider`, but by itself it did not materially improve runtime behavior.

Observed bad run before the deeper fix:

- `Hecton8_runtime_2026-04-03_01-37-17.log`
- startup scatter:
  - `rebuild=304.88ms`
  - `sample=167.10ms`
- startup GC:
  - `window=1 gc=14293701B`
- repeated large GC windows:
  - `window=2 gc=4783598B`
  - `window=6 gc=4794967B`
  - `window=11 gc=4805902B`
  - `window=19 gc=4846383B`

Conclusion:

- that step alone was not enough
- in practice it did not solve the early runtime churn

### Real fix applied for this run

`WorldGenerativeGeologyService` was moved off the destructive `clear -> recreate all children` path for rebuilds on the same generated root.

Key behavior change:

- reuse existing `LOD` roots
- reuse existing primitive children inside each `LOD` root
- only reconfigure transforms / meshes / renderers for existing children
- disable unused children instead of destroying them every rebuild

This preserved the same world content semantics while reducing live rebuild churn.

## Current Measured State

### Startup

- scatter startup:
  - `rebuild=158.76ms`
  - `sample=91.14ms`
  - `reconcile=52.47ms`
  - `spawn=47.85ms`
- slowtick startup:
  - `total=195.02ms`
  - `WorldProceduralScatterDirector=168.91ms`
- startup GC:
  - `window=1 gc=7739509B`

### Early Runtime Windows

Large GC spikes still exist, but the cluster is now shorter:

- `window=3 gc=4807147B`
- `window=6 gc=4833274B`
- `window=10 gc=4793107B`

Then the run becomes much cleaner:

- `window=11 gc=133621B`
- `window=12 gc=105215B`
- `window=13 gc=133609B`
- `window=14 gc=350468B`
- `window=15 gc=132250B`
- `window=16 gc=130911B`
- `window=17 gc=104238B`

### Generated Geology / Cave Baseline

Baseline remained valid:

- `geoBindings=7..8`
- `geoRoots=7..8`
- `geoRenderers=114..131`
- `geoVoxels=0..1`
- `voxel.reconcile requests=7..8`

This was not a fake “optimization by deleting caves”.

## Honest Comparison

### Versus the bad 01:37 run

Improved materially:

- startup scatter:
  - from `304.88ms / 167.10ms`
  - to `158.76ms / 91.14ms`
- startup GC:
  - from `14293701B`
  - to `7739509B`
- large early GC spike count:
  - from four obvious ~`4.8MB` windows in the observed route
  - to three obvious ~`4.8MB` windows in the observed route

### Versus the earlier good 01:02 run

Roughly back to the earlier best startup GC band:

- `window=1 gc=7739509B`
- earlier best run:
  - `window=1 gc=7739489B`

But not solved yet:

- the repeated early `~4.8MB` spikes are still alive
- `zero GC` has not been reached

## Current Diagnosis

The last service-level reuse change was real and useful.

What remains looks narrower now:

- early scatter churn is still too heavy
- some early allocation source around scatter/pool/entering-active-window is still alive
- `generated geology clear/rebuild on same root` is no longer the most obvious live culprit

## Next Work

Next focus should stay on the remaining early burst:

1. trace the surviving early `~4.8MB` windows against scatter reconcile and pool expansion
2. inspect hot-family pool misses again, especially `family.coral.low`
3. reduce first-entry churn for active scatter window without changing world content semantics

## Verification

Verified in Unity / runtime:

- code compiled without new first-party errors
- new runtime trace captured
- caves / geology stayed alive
- numbers compared against the immediately previous failed run

Not yet fully verified:

- a longer route with more aggressive traversal after this snapshot
- whether the remaining early `~4.8MB` spikes are dominated more by scatter spawn or by pool expansion
