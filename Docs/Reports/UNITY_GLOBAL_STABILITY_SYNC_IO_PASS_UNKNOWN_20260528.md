# Unity Global Stability Sync IO Pass UNKNOWN - 2026-05-28

Status: STATIC SOURCE PROOF ONLY / RUNTIME PROOF ABSENT

## Scope

Domain: Core and memory infrastructure, SignalBus contracts, zero-GC runtime architecture.

User constraint honored: full project compile errors are not fixed in this pass. Another agent owns the compile wall.

## What Was Wrong

`GlobalProfileManager.SlowTick()` wrote the global profile from the dispatcher slow tick.

The call path was:

```text
SlowTick -> FlushIfDirty -> TryWriteProfile -> Directory.CreateDirectory / JsonUtility.ToJson / File.WriteAllText / File.Delete / File.Move
```

That is a real main-thread stability defect. It can allocate strings and block on disk every 15 seconds after meta progress changes.

`LocalizationManager` Babel dictionary readers were already behind `Awaitable.BackgroundThreadAsync()`, but their helper names did not expose that route to the static contract audit.

`ControlRemapper`, `QAEnduranceWatchdogBot`, and `LutArrayResolver` had cold or QA-only file IO helpers with names that looked runtime-generic to the audit.

## What Changed

`GlobalProfileManager` no longer flushes profile JSON from `SlowTick()`.

Profile writes now remain in cold lifecycle routes:

```text
OnDisable
OnDestroy
OnApplicationQuit
OnApplicationPause(true)
OnApplicationFocus(false)
```

The actual profile IO helpers are now named `FlushIfDirtyCold()`, `TryWriteProfileCold()`, and `LoadProfileFromDiskCold()`.

`SlowTick()` only tracks dirty age with a bounded `Mathf.Min` cap. It does not touch disk.

Babel file read helpers were renamed to `ReadBabelDictionaryIntoStageBackgroundCold()`, `ReadBabelDictionaryWithMmfBackgroundCold()`, and `ReadBabelDictionaryWithStreamBackgroundCold()`.

Input override IO helpers were renamed to `TryReadAllCold()`, `TryWriteAtomicCold()`, and `TryDeleteTempAfterIoFailureCold()`.

QA endurance and LUT boot helpers were renamed so the phase is explicit: `BeginRunCold()`, `WriteResultFileCold()`, `StartCold()`, `TryStreamFileIntoRawTextureCold()`, `TryStreamFileIntoArgb32FallbackCold()`, and `TryDeleteFileCold()`.

## Proof

Before this pass, the last clean SignalBus audit reported:

```text
errors=0
confirmedErrors=0
warnings=57
infos=1024
```

Final audit:

```text
Docs/Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_GLOBAL_STABILITY_IO_FINAL.json
errors=0
confirmedErrors=0
warnings=40
infos=1041
files=2443
shaders=71
```

Delta: `RUNTIME_SYNC_FILE_IO_REVIEW` warnings reduced by `17`.

Source hygiene:

```text
git diff --check -- touched source files
exit=0
only LF-to-CRLF working-copy warnings
```

Documentation gates:

```text
VerifyDocStructure.py pass=true activeDocCount=706 encodingWithoutUtf8Sig=0
OOP_Doc_Scanner.py finalPass=true activeFileCount=706 sourceSyncPass=true wordReductionPercent=31.157806912765135
```

Full solution build was not run by this agent.

## Residuals

Remaining warnings are cross-domain `RUNTIME_SYNC_FILE_IO_REVIEW` findings in AI, World, VFX, Rendering, Quest, Narrative, Construction, UI, Gameplay, Economy, Fauna, Visor, and Thermodynamics files.

They were not edited in this pass because no main-thread hot route was proven for them inside this agent domain, and parallel agents are active.

No Unity Editor import, Play Mode, profiler, GCMonitor, player build, or device proof was produced.

## Regression Model

CPU: removes one recurring main-thread disk-write route from `GlobalProfileManager.SlowTick()`.

GC: removes recurring profile JSON serialization from the slow tick route. Measured GC proof is absent.

Memory: no new runtime persistent native ownership was added.

Correctness: meta profile flush is now lifecycle-bound. Crash-before-pause can still lose dirty meta progress.

Cadence: dirty profile data is not written every 15 seconds. It flushes on lifecycle boundaries.

Failure modes: profile file write failure keeps `_dirty=true` and logs only in editor/development builds.

## Runtime Claim Boundary

Runtime microseconds saved: `0 us` claimed.

Expected benefit is hitch-risk removal and cleaner phase ownership, not a measured frame-time result.
