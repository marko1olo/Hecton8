# WORLD_SCATTER_RUNTIME_HARDENING_2026-04-02

Date: 2026-04-02

## What was wrong

- `WorldProceduralScatterDirector` was waiting on `SceneBootstrap` even in scenes where no live bootstrap instance existed.
- After switching scatter creation to pooled reuse, startup filled the console with `Pool exhausted` warnings because the pool was not prewarmed to the real per-pass demand.
- The sampling hot path repeated the same pattern/context budget lookups several times per cell.

## What was changed

- `WorldProceduralScatterDirector` bootstrap gating now defers only when a real active `SceneBootstrap` is present.
- Removed the local scatter-side bootstrap timeout/warning path that was producing false waits and false noise.
- Scatter instance creation/destruction now reuses `ObjectPoolManager` when possible.
- Added pool prewarm preparation based on the actual placements needed for the current reconcile pass.
- Reduced repeated sampling work by resolving pattern/context budget data once per sampled cell and reusing it inside the cell loop.
- Added optional startup warmup spreading so the first scatter pass can stage distant placements across follow-up slow ticks instead of forcing all startup work into one spike.

## What this gives

- No more false `SceneBootstrap was not found...` warning from scatter startup.
- No more `Pool exhausted` spam from the scatter pass.
- Startup sampling work is materially lower.
- Later cell-to-cell scatter rebuilds remain much cheaper than the original startup burst.

## Verified in Unity

- Project compiles without new first-party `Error`.
- Long play run confirmed:
  - false bootstrap warning is gone
  - pool exhaustion warning spam is gone
  - startup scatter profile improved to:
    - `rebuild=310.97ms`
    - `sample=107.80ms`
    - `reconcile=185.98ms`
    - `spawn=181.23ms`
  - compared to the earlier measured bad path:
    - `rebuild=407.50ms`
    - `sample=224.35ms`
- Follow-up movement rebuilds stayed in the smaller range:
  - about `70-122ms` total
  - about `64-108ms` sampling
  - about `3-11ms` spawn on incremental passes

## Still open

- The first startup scatter burst is still too heavy.
- The next real bottleneck is no longer false waiting or pool noise; it is the startup `create/spawn` volume itself.
- Scene-level `missing script` warnings and some stale input/runtime noise appeared in a later long session and should be audited separately so profiling stays clean.
