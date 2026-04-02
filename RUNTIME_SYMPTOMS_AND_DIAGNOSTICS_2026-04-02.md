# Runtime Symptoms And Diagnostics — 2026-04-02

Status: active diagnostics snapshot for the next dialog.

## Purpose

This document fixes the current performance picture in writing.

It exists so the next dialog does not re-invent the diagnosis, does not
downplay the numbers, and does not lose the difference between:

- confirmed facts from live logs
- code-backed interpretation
- still-unproven hypotheses

---

## What Is Actually Wrong Right Now

The project is no longer mainly blocked by compile errors.

The real live problems are now:

1. `WorldProceduralScatterDirector` still causes heavy runtime spikes.
2. `WorldGenerativeGeologyVoxelBridgeDirector` causes a separate very large
   spike when voxel volume generation happens.
3. Runtime GC still has bad-frame allocation spikes around `~5 MB`.
4. Batch count sometimes rises above `1000`, and in some windows above `2000`.
5. Editor/system memory in the profiled run is still very high
   (`~3.48 GB` to `~3.64 GB`).

---

## Live Log Facts

These numbers came from the latest user-provided runtime log and should be
treated as the current truth until disproven by a newer run.

### Scatter startup spike

`WorldProceduralScatterDirector` startup rebuild:

- `rebuild=310.98ms`
- `sample=113.10ms`
- `rescue=16.59ms`
- `restore=0.43ms`
- `reconcile=177.02ms`
- `cleanup=2.71ms`
- `spawn=170.67ms`
- `fauna=3.63ms`
- `diag=3.84ms`
- `created=153 reused=0`

`TickProfiler` at the same moment:

- `SlowTick total=356.48ms`
- top culprit: `WorldProceduralScatterDirector=323.50ms`

### Scatter movement spikes

While moving across cells, scatter still produces repeated heavy rebuilds:

- around `66ms` to `136ms` total rebuild time
- sample phase often around `63ms` to `132ms`
- spawn phase often around `1ms` to `7ms`

This means startup scatter is still bad, but normal movement scatter is not
free either.

### Voxel bridge spike

Later in the same run:

- `WorldGenerativeGeologyVoxelBridgeDirector=498.27ms`
- same moment logs:
  - `[HectonVoxel] Data volume generated ...`
- corresponding runtime window:
  - `frame=526.52ms`
  - `main=526.48ms`
  - `gc=759697B`

This is a confirmed large CPU stall in the voxel/geology path.

### GC peaks

The runtime log shows repeated bad-frame GC peaks around `~5 MB`:

- `5122401B`
- `5089864B`
- `5067874B`
- `5088715B`
- `5044110B`
- `5082169B`
- `5066553B`
- `5021127B`

There is also an enormous startup peak:

- `142162776B`

That startup value should not be mixed with steady-state runtime, but it still
shows a very heavy initialization frame.

### Batch / SetPass facts

The same run contains these renderer-side peaks:

- `setPass` usually around `63` to `89`
- `batches` sometimes around `800` to `1100`
- worst logged peaks include:
  - `1102`
  - `1901`
  - `2066`
  - `2096`

This is important:

- batch count is exploding
- set pass count is not exploding at the same scale

So this does **not** currently look like a pure “too many unique materials”
problem. It looks more like too many visible renderers / meshes / instances
coming alive together.

---

## What The Runtime Profiler Numbers Really Mean

The current profiler output in `RuntimePerformanceProfiler.cs` logs peak values
inside the sample window, not averages and not totals over the whole window.

Code-backed proof:

- `SampleRecorders()` stores current readings
- then updates:
  - `_peakGcAllocBytes = Mathf.Max(...)`
  - `_peakSetPassCalls = Mathf.Max(...)`
  - `_peakBatches = Mathf.Max(...)`
- `FlushSampleWindow()` prints those peak fields

So:

- `gc=5122401B` means:
  “inside this sample window there was at least one frame with about 5 MB of
  GC allocation”
- it does **not** mean:
  “5 MB accumulated across the whole sample window”

The same logic applies to:

- `frame=`
- `main=`
- `setPass=`
- `batches=`

---

## Honest Interpretation

### What is confirmed

Confirmed by the log:

- scatter is still a major runtime CPU problem
- voxel bridge is a separate major runtime CPU problem
- recurring `~5 MB` GC bad-frames are real
- `1000+` batches are real

### What is likely, but not yet fully proven

Likely from the combined evidence:

- the recurring GC spikes are tied to world streaming / scatter / geology
  activity
- the very high batch peaks are caused by large amounts of world content
  becoming visible together, not just by material chaos

### What is not yet proven

Not yet honestly proven:

- the exact single method responsible for the `~5 MB` GC spikes
- the exact renderer family responsible for the `1500-2000+` batch peaks
- whether memory pressure is dominated by first-party runtime content or by the
  Unity Editor context in this profiling run

---

## Current Priority Order

The next dialog should work in this order:

1. Attack `WorldGenerativeGeologyVoxelBridgeDirector` first.
   Reason:
   it has a confirmed `~498ms` spike and is now the worst single stall.

2. Then isolate the recurring `~5 MB` GC bad-frame source.
   Reason:
   this is now a live runtime quality problem, not theory.

3. Then investigate the `1000+` / `2000+` batch spikes with renderer-level
   visibility analysis.
   Reason:
   batch inflation is clearly real, but attribution is still too broad.

4. Keep improving scatter only after the voxel bridge pass unless a new run
   proves scatter became the top offender again.

---

## Files Most Relevant To The Next Pass

- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`
- `Assets/_Project/Scripts/RuntimePerformanceProfiler.cs`
- `CURRENT_SESSION_HANDOFF.md`

---

## Rule For The Next Dialog

Do not talk around the numbers.

Use the log as the source of truth, distinguish facts from guesses, and only
claim a root cause when code or live repro actually supports it.
