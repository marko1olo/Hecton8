# Documentation R31 Architecture Current-Boundary Propagation

Date: 2026-05-19
Status: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / RUNTIME PROOF ABSENT

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Scope

R31 is a local-only root/architecture documentation pass. It continues R30 by propagating the current boundary into active architecture interiors and direct root entrypoints that still carried R28/R29/R30 drift after the R30 report existed.

R31 does not recapture source counters. The latest deliberate DOC_GLOBAL root/architecture source-counter snapshot remains R27:

- `Assets/_Project/**/*.cs`: `1818`
- `Assets/_Project/Scripts/**/*.cs`: `1761`
- first-party non-test C# excluding `Assets/_Project/Tests*`: `1797`
- physical source lines: `1204221 / 1184559 / 1199376`
- broad/direct interface orientation: `342 / 267`
- direct public interfaces in `GlobalRegistryContracts.cs`: `62`
- first-party asmdefs: `123`
- direct `GlobalSignals.CreateQueue(...)` slots: `73`
- typed `SignalBus<T>.EnsureInitialized()` lanes: `133`

## Corrections

- Promoted R31 as the latest root/architecture DOC_GLOBAL boundary. R30 is now the prior internal-currentness correction, R29 is the prior stale-gate/global-authority correction, R28 is the prior interior-boundary correction, and R27 remains the latest source-counter/index snapshot.
- Updated active architecture boundary notes that still pointed to R29 or R30 as current.
- Corrected six `GLOBAL_AUTHORITY_*` headings whose bodies already routed through a newer boundary.
- Corrected `DISPATCH_PIPELINE.md` from "global boundary is R29" to the current chain.
- Demoted direct-root May 3/May 11/May 17 "latest/current" lines to historical evidence.
- Corrected root and architecture path claims for performance probes, haptics/input sources, the missing `SaveCompressionDictionary.cs`, and the absent orphaned-script audit CSV.
- Demoted a Mod API static validator line from portable proof to schema/input-surface orientation unless a standalone artifact tuple is attached.
- Added missing R4 actuality boundaries to seven active architecture documents found by the R31 marker scan: `ABYSSAL_THERMODYNAMICS_SOLVER.md`, `FLORA_PROCEDURAL_SWAY_FIELD.md`, `HABITAT_FLUID_INCURSION.md`, `MACRO_ECOSYSTEM_MATHEMATICIAN.md`, `PROCEDURAL_WRECKAGE_ASSEMBLER_SHINOBU_121.md`, `SHINOBU_115_STRUCTURAL_INTEGRITY_CALCULATOR.md`, and `SHINOBU_125_SCAVENGING_LOOT_ORACLE_ROUTE_CARD.md`.

## Validation

- Targeted stale-current/proof scan over active root/architecture/report surfaces: `missing=0`.
- Markdown/txt R4 marker scan: `ScopeFiles=89`, `MissingCount=0`, `DuplicateCount=0`.
- Local markdown link scan: `ScopeFiles=88`, `MissingLinks=0`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- JSON parse spot check: `ok=5`, `bad=0`, `missing=0`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6549 missing=57`.
- Scoped root/architecture `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

This report is a static documentation/source boundary only; it is not runtime proof.

## Blockers

- `Tools/AtlasCheck.py` remains expected red on missing RealtimeCSG vendor icon/readme references unless a later run proves otherwise.
- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, mod runtime smoke, platform run, campaign telemetry, or visual-route proof was captured in R31.
- Mod API static validation may pass, but that is static tool output only and not mod runtime proof.
