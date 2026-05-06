Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 Runtime Perf Diagnostic Bundle — 2026-04-04

## Verified runtime history

| Phase | Tick peak | Scatter rebuild | sample | post | reconcile | spawn | Other verified offenders |
|---|---:|---:|---:|---:|---:|---:|---|
| Best confirmed run | n/a | 77.76ms | 53.42ms | 35.77ms | 13.59ms | 9.19ms | none recorded |
| Regressed run | 162.19ms | 105.39ms | 77.57ms | 44.25ms | 20.53ms | 9.00ms | `FaunaDirector=17.21ms` |
| Previous handoff baseline | 144.60ms | 79.24ms | 64.29ms | 30.14ms | 10.63ms | 4.53ms | `FaunaDirector=25.66ms`, `MapMagicBridge=2.52ms`, `ScavengePopulator=2.03ms` |
| Latest run from user logs / profiler | 104.71ms | 69.36ms | 51.37ms | 29.04ms | 10.49ms | 4.29ms | `WorldProceduralScatterDirector=84.77ms`, `FaunaDirector=11.52ms`, `ScavengePopulator=1.97ms`, `MapMagicBridge=0.90ms` |

## Latest confirmed runtime logs

```text
[WorldScatterProfiler] rebuild=69.36ms sample=51.37ms input=19.55ms wait=2.79ms post=29.04ms rescue=6.78ms restore=0.31ms reconcile=10.49ms cleanup=3.97ms spawn=4.29ms fauna=2.23ms diag=0.41ms removed=0 rebuilt=0 created=0 reused=0 cells=225 desired=8 active=0 reason=dirty

[TickProfiler] SlowTick spike total=104.71ms registered=20 top=WorldProceduralScatterDirector=84.77ms | FaunaDirector=11.52ms | ScavengePopulator=1.97ms | HectonSurvivalSystem=1.17ms | HectonUnderwaterVisuals=1.07ms | MapMagicBridge=0.90ms
```

## Latest run vs previous handoff baseline

- `TickProfiler total`: `144.60 -> 104.71ms` (`-39.89ms`, about `-27.6%`)
- `WorldProceduralScatterDirector`: `106.40 -> 84.77ms` (`-21.63ms`, about `-20.3%`)
- `FaunaDirector`: `25.66 -> 11.52ms` (`-14.14ms`, about `-55.1%`)
- `scatter rebuild`: `79.24 -> 69.36ms` (`-9.88ms`, about `-12.5%`)
- `sample`: `64.29 -> 51.37ms` (`-12.92ms`, about `-20.1%`)
- `input`: `25.95 -> 19.55ms` (`-6.40ms`, about `-24.7%`)
- `wait`: `8.20 -> 2.79ms` (`-5.41ms`, about `-66.0%`)
- `post`: `30.14 -> 29.04ms` (`-1.10ms`, about `-3.6%`)
- `reconcile`: `10.63 -> 10.49ms` (`-0.14ms`, about `-1.3%`)
- `spawn`: `4.53 -> 4.29ms` (`-0.24ms`, about `-5.3%`)
- `rescue`: `3.38 -> 6.78ms` (`+3.40ms`, regression)
- `desired`: `6 -> 8`
- `active`: `0 -> 0`

## Current objective conclusions

1. The latest verified runtime is materially better than the previous handoff baseline.
2. User-reported feel of "less freezing" is consistent with the numbers:
   - total slow-tick spike fell from `144.60ms` to `104.71ms`
   - top runtime offender fell from `106.40ms` to `84.77ms`
   - `FaunaDirector` fell from `25.66ms` to `11.52ms`
3. Runtime is still not normalized:
   - `dirty` scatter rebuild still costs `69.36ms`
   - `sample` still costs `51.37ms`
   - `post` still costs `29.04ms`
4. Main runtime stall is still `WorldProceduralScatterDirector`.
5. `active=0` with `desired=8` remains a bad signal:
   - the system is still spending expensive rebuild time without producing active placements
6. `rescue` got worse in the latest run:
   - `3.38 -> 6.78ms`
   - this is now an explicit investigation target
7. GC is still not the root cause of the runtime freeze:
   - older profiler screenshot values stayed in the `1.1-2.5 KB` range
8. Editor lifecycle errors are separate from the gameplay spike until proven otherwise:
   - `LifecycleManagement ... NullReferenceException`
   - `SerializedObjectNotCreatableException`
   - `MCP-FOR-UNITY [WebSocket] ... not initialised`

## Latest profiler screenshot breakdown

### Frame 704 / 1676
- CPU Active: `116.132ms`
- GPU: `6.194ms`
- Systems:
  - Scripts: `6.388ms`
  - Others: `5.774ms`
  - Rendering: `2.465ms`
- Top markers dominated by editor/profiler:
  - `RenderLoop 113.118ms`
  - `EditorLoop 2.803ms`
  - `Profiler.ParseThreadData 1.693ms`
  - `Profiler.FlushMemoryCounters 1.021ms`
- Verdict:
  - not a clean gameplay frame for attribution
  - editor/profiler overhead dominates

### Frame 593 / 1676
- CPU Active: `115.713ms`
- GPU: `6.612ms`
- Systems:
  - Others: `6.368ms`
  - Scripts: `5.643ms`
  - Rendering: `2.160ms`
- Top markers:
  - `RenderLoop 113.031ms`
  - `EditorLoop 3.719ms`
- Verdict:
  - same story: not a trustworthy gameplay-attribution frame

### Frame 196 / 1676
- CPU Active: `31.797ms`
- GPU: `14.111ms`
- Systems:
  - Others: `17.035ms`
  - Scripts: `11.000ms`
  - Rendering: `3.365ms`
- Top markers:
  - `RenderLoop 11.681ms`
  - `EditorLoop 10.812ms`
  - `Profiler.ParseThreadData 3.939ms`
  - `WaitForJobGroupID 2.990ms`
- Verdict:
  - still editor-polluted
  - useful only to show project is not GPU-bound in the worst gameplay hitch

## Memory snapshot currently available

Source: older profiler screenshot set, not the latest console run.

- Total Resident On Device: `6.73 GB`
- Total Allocated: `11.37 GB`
- Native: `4.65 GB`
- Managed: `3.55 GB`
- Untracked: `2.49 GB`
- Graphics (Estimated): `0.68 GB`
- Textures: `0.80 GB`
- Render Textures: `426.5 MB`
- GC Alloc In Frame snapshot: `2.5 KB`

Fresh memory verification for the new `104.71ms / 69.36ms` baseline has not been captured yet.

## Changes already integrated before next verification

1. `MapMagicBridge` scene wiring is now serialized and saved:
   - `mapMagicObject = Terrain`
   - `playerTransform = Player`
2. Editor wiring now also pushes those refs through live bridge setters.
3. `WorldProceduralScatterDirector.SetFaunaSpawnRegistry(...)` is idempotent on same-reference rebind.
4. `FaunaDirector.TryWarmupCreaturePools()` now dedupes repeated prefabs across biome datasets before warmup.
5. `CreaturePoolWarmupCount = 8`

## Open unresolved questions

1. Why are `dirty` scatter rebuilds still occurring in runtime after the registry/idempotence fixes?
2. Why does scatter still report `active=0` while `desired=8` and spend `69.36ms` rebuilding?
3. Why did `rescue` get worse (`3.38 -> 6.78ms`) while most other scatter stages improved?
4. Are the remaining `dirty` invalidations caused by:
   - bootstrap wiring order
   - procedural state registry events
   - fauna registry changes
   - some other control-path invalidation
5. Fresh memory numbers for the latest runtime are still missing:
   - current document only has older screenshot-derived memory data

## Status

`PENDING VERIFICATION`

## 2026-04-05 clean profiler baseline after clearing Profiler

Source: user-provided Unity Profiler screenshots captured on 2026-04-05 after clearing the Profiler buffer and re-running.

Important separation:
- `normal baseline` = the clean post-clear runtime/editor selection with longest inspected frame around `31-35.6 ms`
- `startup frame` = the first warmup frame around `8376 ms`; this is **not** valid as gameplay baseline

### Current normal baseline

- Selection frame time:
  - `max = 31.025 ms`
  - `median = 13.138 ms`
  - `min = 11.008 ms`
- Longest inspected live frame:
  - `CPU = 35.59 ms`
  - main thread `PlayerLoop = 11.12 ms`
  - main thread `RenderPlayModeViewCameras = 7.83 ms`
  - main thread `RenderPipelineManager.DoRenderLoop_Internal = 7.71 ms`
  - render thread `RenderLoop = 27.02 ms` with `23.56 ms` self
- Bottleneck split across selection:
  - `CPU over target = 44% of frames`
  - `GPU over target = 0% of frames`
  - `Others mean = 10.651 ms`
  - `Scripts mean = 3.896 ms`
  - `Rendering mean = 1.523 ms`
- GC across the clean selection:
  - `1377` allocation events across the selected range
  - worst frame allocation payload `4.9 KB`
  - `GC Collect` markers not found
- Render stats snapshot:
  - `SetPass Calls = 306`
  - `Triangles = 624.2k`
  - `Vertices = 341.9k`
  - `Used Textures = 77 / 37.2 MB`
  - `Render Textures = 694 / 0.60 GB`
  - `Render Texture Changes = 194`
  - `Shadow Casters = 64`

### Baseline verdict

1. This clean run is materially better than the earlier dirty-scatter / editor-polluted captures.
2. In the current clean selection, the worst frame is **not** dominated by `WorldProceduralScatterDirector`.
3. The current worst inspected frame is dominated by:
   - `EditorLoop`
   - render-loop / camera-stack work
   - normal render-thread waiting behavior
4. The clean selection does **not** show a steady-state GC stall problem:
   - peak frame payload is only `4.9 KB`
   - no GC collect marker was found in the selected range
5. The project is still CPU-limited in editor:
   - `44%` of frames over target
   - but this is no longer the earlier `100+ ms` spike class
6. Render texture residency is still heavy for the MX350 target:
   - `694 RT`
   - `0.60 GB` RT memory in this snapshot

### Screenshot-by-screenshot analysis

#### 1. Frame time + allocations summary panel

- Clean selection readback:
  - `max = 31.025 ms`
  - `median = 12.183 ms`
  - `min = 9.324 ms`
- Longest selected frame self markers:
  - `RenderLoop = 23.695 ms`
  - `EditorLoop = 23.370 ms`
  - `Profiler.ParseThreadData`
  - `Profiler.FlushMemoryCounters`
- Allocation summary:
  - total allocation-event count across selection = `1377`
  - worst allocation count frame = `93`
  - worst allocation payload frame = `4.9 KB`
- Verdict:
  - clean selection is no longer showing catastrophic runtime GC
  - top self time is editor/render-side, not gameplay script-side

#### 2. CPU graph after clearing Profiler

- Visual pattern:
  - frame band is mostly around `10-16 ms`
  - occasional spikes climb into the `20-30 ms` range
  - no repeated `100+ ms` class spikes visible in this run
- Verdict:
  - current run shape is much healthier than the previous captured regime
  - if gameplay still feels uneven, the problem is now in intermittent editor/render overhead, not the earlier giant stall class

#### 3. Bottlenecks + systems impact summary

- Bottleneck classification:
  - `CPU = 44% of frames over target`
  - `GPU = 0%`
- Mean system cost across selection:
  - `Others = 10.651 ms`
  - `Scripts = 3.896 ms`
  - `Rendering = 1.523 ms`
- Longest frame self markers still led by:
  - `RenderLoop = 23.695 ms`
  - `EditorLoop = 23.370 ms`
- Verdict:
  - this selection is CPU-limited in editor
  - script cost is present but not the dominant cost center in the captured clean range

#### 4. Timeline for the longest normal frame

- Captured live frame:
  - `CPU = 35.59 ms`
  - `GPU = not captured in this screenshot`
- Main thread:
  - `PlayerLoop = 11.12 ms`
  - `RenderPlayModeViewCameras = 7.83 ms`
  - `RenderPipelineManager.DoRenderLoop_Internal = 7.71 ms`
  - `EditorLoop = 17.48 ms`
- Render thread:
  - `RenderLoop = 19.62 ms`
- Other threads:
  - long `Semaphore.WaitForSignal` spans are visible
- Verdict:
  - the longest clean frame is a mixed editor + render frame
  - this is not a pure gameplay-script stall

#### 5. Render stats panel

- Readback:
  - `SetPass Calls = 306`
  - `Triangles = 624.2k`
  - `Vertices = 341.9k`
  - `Used Textures = 77 / 37.2 MB`
  - `Render Textures = 694 / 0.60 GB`
  - `Render Texture Changes = 194`
  - `Vertex Buffer Upload In Frame = 37 / 0.8 MB`
  - `Index Buffer Upload In Frame = 37 / 3.0 KB`
  - `Shadow Casters = 64`
- Verdict:
  - geometry and SetPass are not exploding
  - RT count and RT memory are still heavy for target hardware

#### 6. Memory usage panel from the startup frame capture

- Readback:
  - `Total Resident On Device = 5.99 GB`
  - `Total Allocated = 12.22 GB`
  - `Native = 4.73 GB`
  - `Managed = 3.63 GB`
  - `Untracked = 3.02 GB`
  - `Graphics (Estimated) = 0.84 GB`
  - `Textures = 1.01 GB`
  - `Render Textures = 0.60 GB`
  - `Total GC Alloc In Frame = 6.7 MB`
- Verdict:
  - this capture is useful as a memory red flag
  - it is **not** a valid steady-state gameplay baseline because it came from the startup/warmup frame group

#### 7. Physics stats panel

- Readback:
  - `Physics Used Memory = 7.9 MB`
  - `Dynamic Bodies = 12`
  - `Static Colliders = 235`
  - `Physics Queries = 5`
  - `Total Overlaps = 0`
  - `Broadphase Adds/Removes = 0`
  - `Narrowphase Touches = 0`
- Verdict:
  - physics is not the active culprit in the provided captures

#### 8. Raw Hierarchy, Main Thread, startup frame

- Captured frame:
  - `CPU = 8376.08 ms`
- Dominant markers:
  - `EditorLoop = 5639.98 ms self`
  - another `EditorLoop = 3725.56 ms`
  - another `EditorLoop = 1662.48 ms`
  - `PlayerLoop = 2386.00 ms`
  - `UpdateScene = 326.84 ms`, `1.5 MB` GC
- Verdict:
  - startup frame is massively editor-dominated
  - not valid for gameplay attribution

#### 9. Raw Hierarchy, Render Thread, startup frame

- Dominant markers:
  - `RenderLoop = 3724.29 ms self`
  - multiple `Gfx.WaitForGfxCommandsFromMainThread` spans:
    - `651.53 ms`
    - `553.97 ms`
    - `406.02 ms`
    - `355.75 ms`
    - `248.74 ms`
- Verdict:
  - render thread spent startup blocked on main-thread/render warmup sequencing
  - this is startup synchronization cost, not normal runtime shape

#### 10. Inverted Hierarchy, Render Thread, startup frame

- Dominant paths:
  - `RenderLoop = 5672.20 ms total`, `5662.06 ms self`
  - `Gfx.WaitForGfxCommandsFromMainThread = 2703.96 ms`
  - `Semaphore.WaitForSignal = 2703.86 ms`
- Small real render work exists:
  - `ExecuteRenderGraph = 4.90 ms`
  - `UniversalRenderPipeline.RenderSingleCameraInternal = 4.20 ms`
- Verdict:
  - startup render-thread cost is overwhelmingly wait/synchronization, not sustained render workload

#### 11. Inverted Hierarchy, Main Thread, startup frame

- Dominant startup costs:
  - `EditorLoop = 5639.98 ms self`
  - `PlayerLoop = 2734.79 ms`, `6.3 MB` GC
  - `Mono.JIT = 2569.20 ms`, `2.4 MB` GC
  - `RenderPlayModeViewCameras = 2386.44 ms`, `4.8 MB` GC
  - `RenderPipelineManager.DoRenderLoop_Internal = 2385.07 ms`, `4.8 MB` GC
  - `VolumeManager.EvaluateVolumeDefaultState = 113.85 ms`, `35.7 KB` GC
  - `PostLateUpdate.PlayerUpdateCanvases = 60.48 ms`, about `0.9 MB` GC
- Verdict:
  - startup frame is dominated by JIT, render-graph warmup, volume setup, and editor overhead
  - again: not a gameplay baseline

#### 12. Batches Count view, Main Thread, startup frame

- Focused path:
  - `RenderPlayModeViewCameras -> RenderPipelineManager.DoRenderLoop_Internal`
- Startup allocations inside the render path:
  - `4.8 MB` on the selected render loop branch
  - `Mono.JIT = 430.85 ms self`
  - `Inl_On Record Render Graph = 98.70 ms`, `476 KB` GC
  - `SharedObjectPool<T>..cctor() = 87.48 ms`, `14.4 KB` GC
- Verdict:
  - this confirms startup warmup and render-graph bootstrapping
  - not a valid steady-state problem frame

#### 13. Hierarchy, Main Thread, normal frame

- Clean longest normal frame:
  - `EditorLoop = 22.97 ms self`
  - `PlayerLoop = 11.12 ms`, `1.1 KB` GC
  - `RenderPlayModeViewCameras = 7.83 ms`, `1.0 KB` GC
  - `RenderPipelineManager.DoRenderLoop_Internal = 7.71 ms`
  - `UniversalRenderTotal = 7.67 ms`
  - `RenderCameraStack = 7.62 ms`
  - `ScriptableRenderContext.Submit = 3.65 ms`, `368 B` GC
  - `UpdateScene = 3.28 ms`, `84 B` GC
  - `Profiler.FlushCounters = 1.38 ms`
  - `Profiler.FlushMemoryCounters = 1.34 ms self`
- Verdict:
  - this is a reasonable current editor baseline
  - gameplay script cost is not the lead offender in this frame

#### 14. Hierarchy, Render Thread, normal frame

- Readback:
  - `RenderLoop = 27.02 ms total`, `23.56 ms self`
  - `UniversalRenderPipeline.RenderSingleCameraInternal = 2.72 ms`
  - `ExecuteRenderGraph = 2.70 ms`
  - `Gfx.WaitForGfxCommandsFromMainThread = 8.63 ms`
  - `Semaphore.WaitForSignal = 8.62 ms`
- Verdict:
  - render thread has some real render work
  - a large part of the frame is still wait/scheduling behavior, not extreme GPU pressure

#### 15. Timeline, longest normal frame

- Confirms frame shape seen in hierarchy:
  - main thread has `~11 ms` of `PlayerLoop`
  - render thread has `~19.6 ms` of `RenderLoop`
  - a long wait span is visible on signal/semaphore lanes
  - editor work still occupies a large portion of the frame
- Verdict:
  - this screenshot matches the hierarchy reading and does not contradict it

#### 16. Inverted Hierarchy, Main Thread, normal frame

- Dominant clean-frame paths:
  - `EditorLoop = 22.97 ms self`
  - `PlayerLoop = 11.12 ms`, `1.1 KB` GC
  - `RenderPlayModeViewCameras = 7.83 ms`, `1.0 KB` GC
  - `RenderPipelineManager.DoRenderLoop_Internal = 7.71 ms`, `1.0 KB` GC
  - `RenderLoop.ScheduleDraw = 1.67 ms`
  - `DrawOpaqueObjects = 1.23 ms`
  - `Profiler.FlushCounters = 1.38 ms`
  - `Profiler.FlushMemoryCounters = 1.34 ms`
- Verdict:
  - the current clean baseline is mostly editor + camera/render-loop work
  - no evidence of the previous giant scatter spike in this particular frame set

### What this baseline means right now

1. Clearing the Profiler materially changed the observed frame shape.
2. The earlier extremely bad captures were at least partly contaminated by Profiler/editor startup overhead.
3. The new current normal baseline is:
   - roughly `11-13 ms` median
   - worst clean selected frame around `31-35.6 ms`
   - script mean around `3.9 ms`
4. The first startup frame with `~8376 ms` must remain excluded from gameplay performance judgment.
5. The remaining real concerns in the clean baseline are:
   - editor-side CPU overhead
   - camera/render-loop cost
   - heavy RT residency (`694` RT, `0.60 GB`)

## Updated baseline status

- Current normal baseline source of truth:
  - the 2026-04-05 post-clear clean Profiler screenshots above
- Startup/warmup source of truth:
  - useful only for initialization cost analysis
  - forbidden as steady-state gameplay baseline

Status remains `PENDING VERIFICATION`.
