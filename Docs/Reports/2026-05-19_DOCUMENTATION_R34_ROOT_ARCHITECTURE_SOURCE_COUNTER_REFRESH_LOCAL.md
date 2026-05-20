# DOC_GLOBAL R34 Root / Architecture Source-Counter Refresh

Date: 2026-05-19
Agent: DOC_GLOBAL_DOCS_REFRESH
Scope: `Docs` root entrypoints plus active `Docs/ARCHITECTURE` documents
Status: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC. Runtime proof remains PENDING VERIFICATION.

## Boundary

R34 supersedes R33 for root/architecture documentation currentness where the documents discuss source scale, atlas reference count, architecture source anchors, R4 actuality boundaries, and current static blocker wording.

R33 remains the prior R32-residue/source-anchor correction. R32 remains the prior R4/proof-wording correction. R31 remains the prior current-boundary propagation layer. R30 remains the prior internal-currentness layer. R29 remains the prior stale-gate/global-authority layer. R28 remains the prior interior-boundary layer. R27 is historical source-counter/index evidence superseded by this R34 source-counter refresh.

This report does not prove Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual route quality.

## Source Counters

R34 static source-count capture:

- `Assets/_Project/**/*.cs`: `1924`
- `Assets/_Project/Scripts/**/*.cs`: `1867`
- first-party non-test `Assets/_Project` C# files excluding `Assets/_Project/Tests*`: `1902`
- project physical lines: `1304459`
- script physical lines: `1284763`
- non-test physical lines: `1298736`
- broad `interface` token hits under `Assets/_Project`: `344`
- broad `interface` token hits under `Assets/_Project/Scripts`: `339`
- direct interface declaration lines under `Assets/_Project/Scripts`: `269`
- `GlobalRegistryContracts.cs` direct public interfaces: `62`
- first-party `.asmdef` files: `129`
- first-party non-test `.asmdef` files: `127`
- `GlobalSignals.CreateQueue(...)` direct queue slots: `73`
- typed `SignalBus<T>.EnsureInitialized()` lanes inside `GlobalSignals.cs`: `133`
- broader script-level `SignalBus<T>.EnsureInitialized()` matches: `254`
- average physical lines per script C# file: `688.14`

These counters are static source orientation only. They are not compile, runtime, profiler, GC, scene wiring, or player-build proof.

## Changes

- Promoted R34 through root/architecture entrypoints and demoted R27 physical-line/source-counter rows from current authority to historical evidence.
- Updated active architecture boundary notes from R33/R27 wording to R34 source-counter and physical-line refresh wording.
- Regenerated the architecture atlas after the R34 documentation/source-anchor edits.
- Updated the current AtlasCheck wording to `ATLAS_CHECK_FAIL references=6705 missing=57`; the missing set remains RealtimeCSG vendor icon/readme image references only.
- Added missing R4 actuality boundaries to `PLATFORM_PORTABILITY_PROOF_LADDER.md`, `SHINOBU_113_HYDRODYNAMIC_KCC_ROUTE_CARD.md`, `SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md`, and `Vehicle_Component_Damage_Router_SHINOBU_152.md`.
- Added or verified source-anchor sections for runtime-facing architecture docs, including input determinism, Data Monolith, Quest DAG, Reactive Economy, Seismic Geology, Submarine OS, and SHINOBU_156 cavitation.
- Separated expected fault dump paths from `Source Anchors` sections where they are future runtime artifacts, not present source files.
- Corrected compile-blocker wording where older docs implied `ChemicalInfluenceGrid.cs` was missing. Current filesystem reality: `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` exists; `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` is still referenced by `Hecton8.Core.csproj` but missing; `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs` are still referenced by `Assembly-CSharp.csproj` but missing.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6705 missing=57`; missing refs are RealtimeCSG vendor icon/readme image refs only.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`. Static-tool orientation only; not mod runtime proof.
- Active non-archive `Docs` JSON parse: `JsonFiles=132`, `Bad=0`.
- Root/architecture R4 marker scan: `ScopeFiles=104`, `Missing=0`, `Duplicate=0`.
- Architecture source-anchor filesystem scan: `SourceAnchorPathsChecked=257`, `Missing=0`.
- Local markdown link scan over root/architecture/report entrypoint scope: `MarkdownLinksChecked=62`, `Missing=0`.
- Targeted stale-current scan for R34-absent/restored wording, old AtlasCheck tuples, stale ChemicalInfluenceGrid missing wording, and R33-latest wording: no hits.
- Scoped `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof exists for R34.
- `Tools\AtlasCheck.py` remains red on `57` RealtimeCSG vendor icon/readme image references.
- Generated/project-file references still include missing source paths:
  - `Hecton8.Core.csproj` references `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, which is absent.
  - `Assembly-CSharp.csproj` references `Assets/_Project/_Archive/HectonWaterPhysics.cs`, which is absent.
  - `Assembly-CSharp.csproj` references `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`, which is absent.
- `ChemicalInfluenceGrid.cs` is present at `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs`; older missing-file wording is stale.
