<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# DOC_GLOBAL_DOCS_REFRESH R18 Local Report

Date: 2026-05-18
Agent: DOC_GLOBAL_DOCS_REFRESH
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT
Status: LOCAL STATIC DOC REFRESH COMPLETE / RUNTIME PROOF ABSENT

<!-- DOC_GLOBAL_DOCS_REFRESH:R13_REPORT_SNAPSHOT_BOUNDARY_START -->
## R13 Report-Snapshot Boundary

This file is a dated static documentation/source report. It is not Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, scene-wiring, frame-time, memory, or visual-quality proof.

Use it only where it agrees with `AGENTS.md`, `.agents-skills`, stable `Docs/*.md` authority files, current source files, and fresh verification artifacts.
<!-- DOC_GLOBAL_DOCS_REFRESH:R13_REPORT_SNAPSHOT_BOUNDARY_END -->

## Scope

R18 closed the long-tail active-documentation debt left after R17:

- active stable `.md` / `.txt` R4 boundary coverage across root docs, architecture docs, Archivarius indexes, `02_ACTUAL_REPORTS`, the April 30 forensic bundle, and newly active Marketing docs;
- stale current/proof wording in Archivarius actual reports and forensic bundle documents;
- report/index navigation that still made R17 the newest local boundary;
- volatile source-counter drift after the workspace moved again;
- Modding signal-schema drift after current source dropped from `170` to `160` `ISignal` structs.

Historical archives and dated machine-evidence payloads were not rewritten as if they were current truth.

## Concrete Corrections

- Added/normalized R4 actuality boundaries. Final active stable scan after concurrent Marketing doc churn: `252` active `.md` / `.txt` files, missing `0`, duplicate boundary markers `0`.
- Demoted stale live-looking statuses in `AUDIO_ROUTING_AUDIT.md` and `ITEM_ASSET_GUIDS.md` from `ETA SURGERY_PREPPED` to historical static/pending verification labels.
- Demoted April 28/29/30 Archivarius claims about script scale, interface count, VRAM budget, compute/rendergraph leak status, god-object status, Unity console readbacks, and handoff readiness to static/historical language.
- Updated forensic bundle stale counts and proof language: May 4 counts are historical, MCP/Unity console clean slices are historical unless a fresh raw artifact is linked, and formal-test/smoke/verifier/doc-count values are volatile static snapshots.
- Updated `PROJECT_ATLAS.md` and Archivarius README to record the R18 late static source snapshot: `1743` project C# files, `1690` script C# files, `1726` non-test C# files, `990528` project source lines, `974162` script source lines, `63` direct public interfaces, and `107` first-party asmdefs. These are capture-time values only.
- Synchronized Modding docs/schema to current source: `160` source `ISignal` structs, `2` projected mod signals, `158` denied-by-default signals.
- Added an R4 actuality boundary to `Docs/ARCHITECTURE/SHINOBU_41_Geological_Synthesis.md` and demoted its status to static source orientation / compile proof pending.
- Added R4 actuality boundaries to newly active Marketing preparation, Steam, creator outreach, community, press, KPI, content-shotlist, and source-ledger documents; added an R18 platform actuality note that Steamworks rules must be rechecked before money, keys, registration, or public launch language.

## R18 Static Snapshot

- `.agents-skills`: `80` `.txt` mandate files / `81` files total.
- `Assets/_Project/**/*.cs`: `1743`.
- `Assets/_Project/Scripts/**/*.cs`: `1690`.
- first-party non-test C# files: `1726`.
- project/script physical lines by PowerShell line count: `990528` / `974162`.
- direct public interfaces in `GlobalRegistryContracts.cs`: `63`.
- first-party asmdefs under `Assets/_Project`: `107`.
- test source files: `17` total, `13` Editor, `4` PlayMode.
- smoke tester source files: `56`.
- verifier source files: `4`.
- full `Docs` non-meta snapshot before final report writes: `4924`; `.md` / `.txt`: `2857`.
- non-archive active authority docs snapshot: `454` files; `.md` / `.txt`: `261`.
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO`: `26` direct markdown files.
- `ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS`: `58` direct files, `48` markdown files.

## Validation

- `python Tools/test_architecture_atlas.py`: PASS, `9` tests.
- `powershell -NoProfile -ExecutionPolicy Bypass -File Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, schema revision `14`, source signals `160`, projected signals `2`, denied-by-default signals `158`.
- JSON parse spot check: PASS, `9` JSON files.
- Active stable R4 boundary scan: `252` files, missing `0`, duplicate markers `0`.
- Targeted stale `170` / `168` Modding signal count scan: no remaining hits in `Docs/Modding`.

## Blockers

- `python Tools/AtlasCheck.py` still fails: `ATLAS_CHECK_FAIL references=6457 missing=57`, all observed missing refs are RealtimeCSG vendor icon/readme image paths.
- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, or visual-route proof was run.
- The workspace is under concurrent agent churn; exact counts above are capture-time static values and must be rerun before being used as current engineering proof.
