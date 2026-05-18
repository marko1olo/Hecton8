<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-17 Documentation Atlas And Counters R8 Local

Date: 2026-05-17
Status: LOCAL_ONLY STATIC_DOC / STATIC_SOURCE / PY_TOOL; ATLASCHECK FAIL; RUNTIME PROOF ABSENT

## R9 Supersession

R8 is historical within the same local documentation sequence. The current R9 correction is `Docs/Reports/2026-05-18_DOCUMENTATION_EVIDENCE_LANGUAGE_AND_COUNTERS_R9_LOCAL.md`.

The R8 tables below remain R8-time evidence. The live atlas was regenerated again on `2026-05-18 01:28:18`; current R9 static-source counters are `1729` project C# files, `1676` script C# files, `1712` non-test C# files, `1127320 / 1108505 / 1123322` physical lines, `267` interface declaration hits, `63` direct `GlobalRegistryContracts.cs` public interfaces, and `106` first-party asmdefs.

## Scope

R8 continued `DOC_GLOBAL_DOCS_REFRESH` after another user directive to update documentation interiors, not only indexes.

This pass corrected active documentation/tooling drift found after R7:

- stale atlas unit test still requiring the old `ATLAS VERIFIED` wording;
- atlas Markdown/JSON generated timestamps coming from separate `datetime.now()` calls;
- atlas cache invalidation trusting Git dirty state before file size/mtime checks;
- stale R5/R6 current source counters in active authority docs;
- active docs pointing at archived compile/H-Phi artifacts as if they still lived in `Docs/AgentLogs`;
- false unclaimed future-system ownership claims;
- stale tool metadata count in the Subnautica loop gap matrix;
- unsafe `VERIFIED` wording in glossary, FAQ, interconnect, and UI docs.

## Current Static Source Snapshot

Command class: `rg --files`, `rg -c '^'`, `Select-String`, and `Test-Path`.

Evidence class: `STATIC_SOURCE / FILESYSTEM`.

| Surface | R8 value |
|---|---:|
| `Assets/_Project/**/*.cs` | 1716 |
| `Assets/_Project/Scripts/**/*.cs` | 1663 |
| first-party non-test C# files | 1699 |
| project C# physical lines by `rg -c '^'` | 1116122 |
| script C# physical lines by `rg -c '^'` | 1097400 |
| non-test C# physical lines by `rg -c '^'` | 1112217 |
| interface declaration hits under `Assets/_Project` | 253 |
| direct public interfaces in `GlobalRegistryContracts.cs` | 63 |
| first-party asmdefs under `Assets/_Project` | 104 |

These numbers are volatile. The workspace is under active multi-agent edits, so source files can move during the same documentation pass.

## Atlas Tooling Fixes

Files updated:

- `Tools/BuildArchitectureAtlas.py`
- `Tools/test_architecture_atlas.py`
- `Docs/DEPENDENCY_GRAPH.md`
- `Docs/DEPENDENCY_GRAPH.json`
- `Docs/DEPENDENCY_GRAPH.cache.json`

Fixes:

- `Tools/test_architecture_atlas.py` now expects `Status: ATLAS GENERATED STATIC SOURCE / ATLASCHECK REQUIRED / RUNTIME PENDING`.
- The atlas generator captures one `generated_at` timestamp and passes it to both Markdown and JSON output.
- The atlas generator now checks file size/mtime before accepting cached source analysis. R7 cache reuse had stale line counts for hundreds of first-party files.
- The atlas generator no longer emits the false sentence that no root `.sln`/`.csproj` files exist.
- The generated atlas keeps compile/Unity/runtime proof outside its evidence class.

Latest regenerated atlas summary:

| Atlas field | Value |
|---|---:|
| generated | `2026-05-18 00:04:14` |
| C# source files scanned under `Assets/` and `Packages/` | 4981 |
| C# line count scanned under `Assets/` and `Packages/` | 1770865 |
| first-party source files under `Assets/_Project/Scripts/` | 1663 |
| first-party source line count by atlas cache method | 1098837 |
| asmdefs scanned | 164 |
| first-party asmdefs under `Assets/_Project` | 104 |
| signal union count | 225 |
| queue lane count | 56 |

Atlas line counts use the generator's byte newline count plus one. Stable authority docs use the older `rg -c '^'` method for continuity.

## Active Documentation Corrections

Updated active authority/current docs:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/PROJECT_STATE_STATIC_XRAY.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/INTERFACE_STRATEGY.md`

Updated compile/H-Phi boundary paths:

- `Docs/README.md`
- `Docs/ARCHITECTURE/README.md`
- `Docs/QUALITY_GATES.md`
- `Docs/SYSTEMS_CONTRACTS.md`

The May 15 CurrentDisk53 and BudgetGate22 artifacts now point to `Docs/Archive/Batch007/AgentLogs/...` and are labeled archived CLI/static evidence, not current compile proof.

Updated future-seam ownership docs:

- `Docs/ARCHITECTURE/UNCLAIMED_FUTURE_SYSTEM_SEAMS.md`
- `Docs/Modding/Future_Command_Kernel_Reservations.md`

R8 trail scan found Status/Rationale evidence for `SHINOBU_21`, `31`, `32`, `33`, `34`, `35`, `36`, `39`, and `40`. Only `SHINOBU_37` and `SHINOBU_38` still lack visible Status/LOG/Rationale trails.

Updated stale content and wording:

- `Docs/ARCHITECTURE/SUBNAUTICA2_PLAYER_LOOP_TO_HECTON8_GAP_MATRIX.md`: tool metadata count is `13`, with `ToolMetadata_LogicSpanner.asset` as the known extra/orphan metadata.
- `Docs/H8_GLOSSARY.md` and `Docs/TECHNICAL_FAQ.md`: status downgraded from `ENCYCLOPEDIA VERIFIED`.
- `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`: `Verified LateUpdate Flush Order` downgraded to static-source wording.
- `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md`: `Verified Search Targets` downgraded to static search wording.

## Verification

Commands and results:

```text
python Tools/BuildArchitectureAtlas.py
EXIT 0
WROTE Docs/DEPENDENCY_GRAPH.md
WROTE Docs/DEPENDENCY_GRAPH.json

python -m py_compile Tools/BuildArchitectureAtlas.py Tools/AtlasCheck.py Tools/test_architecture_atlas.py
EXIT 0

python Tools/test_architecture_atlas.py
EXIT 0
Ran 9 tests in 0.004s
OK

powershell -ExecutionPolicy Bypass -File Docs\Modding\Validate_Mod_API_Static.ps1
Status: PASS
SchemaRevision: 14
SourceSignals: 170
AllowedProjectedSignals: 2
DeniedByDefaultSignals: 168

python Tools/AtlasCheck.py
EXIT 1
ATLAS_CHECK_FAIL references=6429 missing=57
```

`AtlasCheck.py` still fails only on the same RealtimeCSG vendor icon/readme image family:

- `Assets/RealtimeCSG/RealtimeCSG/Icons/icon_pers_*`
- `Assets/RealtimeCSG/RealtimeCSG/Icons/icon_pro_*`
- `Assets/RealtimeCSG/RealtimeCSG/Readme/Images/house_view.png`

## Boundary

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform build, or visual proof was run.

Runtime microseconds saved: `0us`. This pass prevents false current documentation and false verification language.
