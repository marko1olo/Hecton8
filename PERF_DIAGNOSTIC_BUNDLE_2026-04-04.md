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
