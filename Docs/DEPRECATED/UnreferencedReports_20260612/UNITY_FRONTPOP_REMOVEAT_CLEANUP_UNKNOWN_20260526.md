# Unity Front-Pop RemoveAt Cleanup - UNKNOWN - 2026-05-26

Status: source fixed, compile reclosed.

## Verdict

The project-wide runtime scan for `RemoveAt(0)` now leaves only an editor UI
history window. Four clean owner files were patched without touching dirty
cross-agent files.

This is a bounded source-quality cleanup, not a measured performance claim.
Microsoft documents that `List<T>.RemoveAt(index)` shifts following elements
after removal, so repeated index-0 removal is the wrong shape for queues/LRU
front pops.

## Source Checked

- Microsoft `List<T>.RemoveAt`: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.removeat

## Changed Files

- `Assets/_Project/Scripts/BeaconNetworkSystem.cs`
- `Assets/_Project/Scripts/SaveThumbnailSystem.cs`
- `Assets/_Project/Scripts/Gameplay/BioReactor.cs`
- `Assets/_Project/Scripts/Core/Data/H8DataBaker.cs`

## What Changed

- `BeaconNetworkSystem`: old active-beacon cap trimming removed one oldest
  beacon at a time with `RemoveAt(0)`. It now despawns all excess oldest
  beacons, then performs one `RemoveRange(0, excessCount)`.
- `SaveThumbnailSystem`: old thumbnail LRU used `List<string>` and removed the
  oldest slot through `RemoveAt(0)`. It now uses a fixed `string[12]` order
  buffer plus explicit count and local shifting bounded by `MaxCachedTextures`.
- `BioReactor`: old fuel consumption removed one depleted front fuel item per
  loop. It now counts depleted front items and removes them once with
  `RemoveRange(0, depletedCount)` after consumption.
- `H8DataBaker`: old CSV parser removed the header row with `rows.RemoveAt(0)`.
  It now validates rows and builds the data-row list without front-shifting the
  parsed row list. This is cold bake/helper code, not a runtime hot path.

## Static Proof

Project runtime/script scan:

```powershell
rg -n "RemoveAt\(0\)|\.RemoveAt\(\s*0\s*\)" Assets/_Project/Scripts -g '*.cs'
```

Current result:

- `Assets/_Project/Scripts/Editor/LODStatisticsWindow.cs`

Residual is editor-only CPU history UI. It was not converted because it is not
runtime gameplay/architecture debt.

Scoped `git diff --check` passed for the four changed source files, with
working-copy LF/CRLF warnings only.

## Build

- guarded full-solution CLI build:
  `Docs/Reports/BUILD_UNKNOWN_REMOVEAT_FRONTPOP_RECHECK_20260526.log`;
- guard attempts `1-29` were blocked by high CPU and/or active compiler
  processes;
- final launch guard: CPU `48.6%`, compiler processes `0`;
- command:
  `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`;
- exit `0`;
- `Build succeeded.`;
- `0 Warning(s)`;
- `0 Error(s)`.

## Runtime Proof

Not claimed. No Unity Editor import, Console, PlayMode, player build, profiler,
GC allocation capture, scene wiring, visual, or platform gate was run.

## Documentation Gates

- `Tools/VerifyDocStructure.py`: `pass=true`, `activeDocCount=700`,
  `encodingWithoutUtf8Sig=0`;
- `Tools/OOP_Doc_Scanner.py`: `finalPass=true`, `activeFileCount=700`,
  `sourceSyncPass=true`.

## Hardware Impact

Measured microseconds saved: `0`.

Expected static effect:

- removes repeated front-shift list copies from runtime beacon trimming,
  thumbnail cache eviction, and bioreactor depletion;
- keeps bounded/cold list shifts where they are semantically correct;
- does not convert small managed owner state into native containers without a
  broader ownership proof.
