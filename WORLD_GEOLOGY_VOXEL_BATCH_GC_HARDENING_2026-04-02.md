# World Geology / Voxel / Batch / GC Hardening — 2026-04-02

Status: compile-verified code pass after the latest runtime logs.

## What Was Actually Wrong

The current logs confirmed three different problems and they should not be mixed together:

1. `WorldGenerativeGeologyVoxelBridgeDirector` still causes a separate heavy stall.
   Confirmed by log:
   - `WorldGenerativeGeologyVoxelBridgeDirector=498.27ms`
   - same moment: `[HectonVoxel] Data volume generated ... grid=50 voxel=0.75`
   - same runtime window: `frame=526.52ms main=526.48ms gc=759697B`

2. The recurring `~5 MB` GC bad-frames are real, but in the latest log they align much more closely with scatter/world refresh windows than with the voxel bridge spike.
   Confirmed examples:
   - `window=11 gc=5122401B`
   - `window=16 gc=5089864B`
   - `window=17 gc=5067874B`
   - `window=35 gc=5088715B`
   - `window=38 gc=5044110B`
   - `window=42 gc=5082169B`
   - `window=43 gc=5066553B`
   - `window=53 gc=5021127B`

3. Batch spikes are real and large.
   Confirmed examples:
   - `window=12 batches=1102`
   - `window=13 batches=2066`
   - `window=14 batches=2096`
   - `window=16 batches=1901`

Important nuance from the same logs:
- `WorldGenerativeGeologySeamExecutionDirector` is usually only `~1.4ms` to `~2.9ms`
- so the problem is not “seam tick CPU is huge”
- the more likely problem is the amount of generated world/geology visuals that stay alive after scatter/world rebuilds

## What Was Changed

### 1. `WorldGenerativeGeologyVoxelBridgeDirector`

Goal: make voxel requests cheaper and stop paying full-quality cost for every request tier.

Changes:
- request signature now includes runtime quality tier
  - target voxel resolution
  - collider-needed state
- bridge now scales resolution down for:
  - medium/far distance requests
  - lower-priority requests
- hard cap added for runtime voxel grid dimension
- collider generation is now distance-gated
  - far geology voxel volumes can stay visual-only
- managed `CaveStructure[]` allocations were removed from the request build path
  - bridge now fills `NativeArray<CaveStructure>` directly
- runtime debug state now tracks active voxel colliders separately from active voxel volumes

Why this matters:
- reduces unnecessary rebuild quality for far/weak requests
- removes a repeat managed allocation from every bridge generation request
- avoids paying collider cost for distant geology volumes that do not need immediate physics presence

### 2. `HectonVoxelEngine`

Goal: let the bridge decide when a generated volume really needs a collider.

Changes:
- `GenerateVolumeFromDataAsync(...)` now accepts `buildCollider`
- mesh build path now enables or skips `MeshCollider` based on that flag
- legacy full-generation path keeps collider creation enabled explicitly

Why this matters:
- reduces main-thread finishing cost in the bridge-controlled geology path
- avoids changing the older full voxel generation behavior

### 3. `WorldGenerativeGeologyService`

Goal: reduce batch/GC pressure from non-final scatter geology visuals and make their cost measurable.

Changes:
- proxy / non-final generated geology now uses a cheaper path:
  - composition forced to `SingleFeature`
  - LOD count capped lower
  - debris disabled
- active generated geology roots/renderers are now counted globally
- active geology bindings are now exposed as a live count

Why this matters:
- latest logs show batch spikes living near scatter rebuild phases
- scatter repeatedly touches generated geology for spawned/reused instances
- cutting proxy-only geology detail reduces visual object count where the player is not yet seeing final-grade content anyway

### 4. `RuntimePerformanceProfiler`

Goal: make the next runtime log attribute geology pressure instead of only saying “batches are high”.

Changes:
- runtime profiler report now appends:
  - `geoBindings`
  - `geoRoots`
  - `geoRenderers`
  - `geoVoxels`
  - `geoVoxelColliders`

Why this matters:
- the old log proved there were batch spikes
- it did not prove which renderer family owned them
- the next run should show whether generated geology / voxel volumes are rising together with the bad windows

### 5. `WorldGenerativeGeologySeamExecutionDirector`

Goal: remove an unnecessary whole-scene scan from the seam cleanup path.

Changes:
- seam runtimes now maintain a live registry
- cleanup now reuses that registry instead of `FindObjectsByType(...)`

Why this matters:
- this was not the top offender in the logs
- but it was still needless recurring allocation/work inside the geology runtime path

## What This Gives In Game

- Far geology voxel blends should stop paying the same runtime price as near ones.
- Some geology voxel volumes no longer cook colliders when the player is too far for that cost to be justified.
- Proxy/generated geology attached to scatter content should create fewer visual pieces, which should help the worst batch windows.
- The next profiler run should stop being blind: it will show whether geology roots/renderers/voxel colliders rise with the bad-frame windows.

## Second Pass After The Fresh Runtime Log

The next profiled run made two things much clearer:

1. The large batch spikes are now directly correlated with generated geology renderer counts.
   Confirmed examples:
   - early stable windows: `geoRenderers=117`, batches often settle around `239-242`
   - after scatter/geology growth: `geoRenderers=181`, batches rise to `989 / 1072 / 1529 / 1175`

2. The recurring `~5 MB` GC spikes still happen around scatter rebuild phases, not around voxel volume growth.
   Confirmed examples:
   - `window=25 gc=5067342B`
   - `window=27 gc=5088067B`
   - `window=31 gc=5137809B`
   - same run: `geoVoxels=0 geoVoxelColliders=0`

That means:
- the new counters did their job
- batch inflation is currently much more tied to scatter-attached generated geology renderers than to voxel volumes
- recurring bad-frame GC is still consistent with scatter rebuild churn

## Additional Changes After The Fresh Log

### 6. `WorldProceduralScatterDirector`

Goal: stop scatter from front-loading too much instantiation work into a single rebuild and stop far proxy geology from inflating renderer counts.

Changes:
- scatter pool warmup is now budget-limited per rebuild
- scatter pool warmup is also capped per prefab per rebuild
- proxy/non-final variant resolution now reuses the placement's cached proxy variant instead of re-resolving it every time
- generated geology is now distance-gated for proxy scatter instances
  - final variants still keep geology
  - proxy geology only remains inside a reduced fraction of the near radius
  - far proxy geology is cleared instead of staying alive as extra renderer baggage

Why this matters:
- `PrepareScatterPoolWarmup()` previously had permission to call `pool.Warmup(prefab, missingCount)` with the full deficit in one rebuild
- `ObjectPoolManager.Warmup()` instantiates immediately in a loop, which is a credible source of the recurring bad-frame allocation spikes
- the fresh profiler data showed `geoRenderers` growing from `117` to `181` together with `1000+` batch windows, so far proxy geology needed to be cut back harder than the first pass

## Third Pass After Pool Expansion Logs

The next runtime log showed a new concrete problem:

- scatter pool warmup had become too conservative
- startup and early travel still triggered many fallback pool expansions
- the same prefabs were expanding repeatedly by `1`
  - `PFB_family_coral_low_Placeholder`
  - `PFB_family_coral_low__plate`
  - `PFB_family_pocket_safe_Placeholder`
  - other similar scatter prefabs

That means the previous fix removed some front-loaded warmup, but left too much work to the worst fallback path.

## Additional Changes After The Pool Log

### 7. `WorldProceduralScatterDirector`

Changes:
- startup warmup now has its own larger budget
- startup warmup now only prewarms placements that the initial pass will actually create
- far deferred startup placements are no longer counted into the same warmup pass

Why this matters:
- the earlier warmup pass was still counting more desired placements than the initial pass would spawn
- at the same time, the active warmup budget had become too small for the near startup set
- that mismatch pushed scatter into repeated `ExpandPool()` fallback work during `CreateScatterInstance`

## Fourth Pass After The "Expanding By 4" Log

The next run showed that batching fallback expansion helped, but did not solve the actual startup mismatch:

- startup scatter became cheaper
  - `reconcile=73.61ms`
  - `spawn=67.35ms`
- recurring steady-state GC in the shown windows stayed small
  - mostly `~146 KB` to `~180 KB`
- but startup still repeatedly hit:
  - `PFB_family_coral_low_Placeholder: Pool exhausted, expanding by 4`

That means:
- the fallback path is healthier now
- but startup warmup is still underestimating the exact near-start set for high-frequency prefabs

## Additional Change After The "Expanding By 4" Log

### 9. `WorldProceduralScatterDirector`

Changes:
- the very first startup warmup now uses exact warmup for the startup subset instead of a capped budget
- runtime/travel rebuilds still keep the guarded capped warmup path

Why this matters:
- startup is a one-time cost and should not keep falling into repeated fallback expansion for the same mass prefab
- the guarded limits are still useful for normal travel rebuilds, where recurring bad frames matter more than a single initial preload cost

### 8. `ObjectPoolManager`

Changes:
- fallback expansion is now batched instead of growing by exactly one object every time the pool runs dry

Why this matters:
- repeated `ExpandPool(... by 1)` is the worst possible shape during scatter burst spawning
- a small fallback batch reduces warning spam, reduces repeated `Instantiate` bookkeeping, and gives the pool a chance to stabilize faster after one miss

## What Was Verified

Verified in Unity:
- asset refresh requested successfully
- project compiled after the pass
- Unity console no longer shows any new first-party compile `Error` from these changes
- remaining console entries in the check were warnings from unrelated third-party/editor code

Not yet verified in Unity live play:
- whether the `498ms` voxel bridge stall dropped in a fresh authored run
- how much the proxy geology simplification reduces `1000+ / 2000+` batch windows
- whether the new geology counters line up with the recurring `~5 MB` GC windows

## Honest Current Interpretation After The New Logs

What now looks more likely:
- the `498ms` stall is still a dedicated voxel finishing problem
- the recurring `~5 MB` GC spikes are more likely tied to scatter/world-content churn than to the single voxel bridge spike
- the batch spikes are now strongly supported as visible generated world content growth
  - especially scatter-attached geology renderers
  - not voxel volumes, because the fresh log showed `geoVoxels=0`

What is still not honestly proven:
- the single exact method that owns the `~5 MB` GC windows
- the exact split between pool warmup cost and on-demand spawn/geometry rebuild cost inside the GC windows

## Best Next Verification

Run the same world profiling pass again and compare:

- old format:
  - `batches=2066`
- new format should also show:
  - `geoBindings=... geoRoots=... geoRenderers=... geoVoxels=... geoVoxelColliders=...`

The concrete expectation after this second pass is:

- `geoRenderers` should stop climbing as aggressively in the same travel path
- `batches` should stop sitting in the `1000+ / 1500+` band for as long
- recurring `~5 MB` GC windows should become less frequent if the pool warmup bursts were a real contributor

If `~5 MB` windows still remain unchanged after this pass, the next place to inspect is the per-rebuild generated geology application path itself, not the voxel bridge.
