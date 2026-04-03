# World Runtime State Snapshot — 2026-04-03 01:02

## Baseline

- Trace: `C:/Users/danat/AppData/LocalLow/Danat Games/Submerge/Diagnostics/Hecton8_runtime_2026-04-03_01-02-36.log`
- Caves / generated geology are present:
  - `geoBindings=7..8`
  - `geoRoots=7..8`
  - `geoRenderers=114..131`
  - `geoVoxels=0..2`

## Verified Improvement

- A real runtime warmup bug was fixed in `WorldProceduralScatterDirector`.
- Before the fix, proactive scatter pool warmup could exceed its own global per-rebuild budget.
- Normal gameplay warmup for the same prefab could also be re-issued too frequently.
- After the fix:
  - runtime warmup now always respects `maxPoolWarmupPerRebuild`
  - the same prefab cannot be proactively warmed again until `runtimeWarmupCooldownSeconds` elapses

## Measured Result

### Before the fix

- Startup scatter:
  - `rebuild=274.93ms`
  - `sample=164.27ms`
- Startup GC:
  - `window=1 gc=14,290,895B`
- Repeating movement GC spikes:
  - `window=3 gc=4,779,118B`
  - `window=9 gc=4,765,211B`
  - `window=15 gc=4,782,078B`
  - `window=19 gc=4,729,165B`
  - `window=21 gc=4,773,465B`
  - `window=22 gc=4,761,216B`
  - `window=23 gc=4,675,857B`

### After the fix

- Startup scatter:
  - `rebuild=211.67ms`
  - `sample=130.67ms`
- Startup GC:
  - `window=1 gc=7,739,489B`
- Early movement GC spikes still exist, but far fewer:
  - `window=3 gc=4,777,561B`
  - `window=5 gc=4,812,386B`
  - `window=10 gc=4,791,126B`
- After `window=10`, GC settles down sharply:
  - `window=11..28` mostly `~102KB..135KB`
  - one moderate bump: `window=13 gc=341,255B`

## Honest Read

- This fix helped materially.
- The previous repeating `~4.7MB` churn across the whole route is no longer dominating the later half of the run.
- The game still has expensive early-world startup and early movement bursts.
- The next priority remains:
  - reduce startup scatter sampling cost
  - remove the remaining early `~4.8MB` warm path spikes
  - lower steady-state surface batch pressure further
