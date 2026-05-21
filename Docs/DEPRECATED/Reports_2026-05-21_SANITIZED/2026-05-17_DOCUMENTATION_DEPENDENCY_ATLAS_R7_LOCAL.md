<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-17 Documentation Dependency Atlas R7 Local

Date: 2026-05-17
Status: LOCAL_ONLY STATIC_DOC / PY_TOOL PASS WITH ATLASCHECK FAIL; RUNTIME PROOF ABSENT

## R8/R9 Supersession

R7 is historical within the same local documentation sequence. R8 atlas/cache/counter correction is `Docs/Reports/2026-05-17_DOCUMENTATION_ATLAS_AND_COUNTERS_R8_LOCAL.md`; R9 evidence-language/source-counter correction is `Docs/Reports/2026-05-18_DOCUMENTATION_EVIDENCE_LANGUAGE_AND_COUNTERS_R9_LOCAL.md`.

R8 fixed atlas cache invalidation, aligned Markdown/JSON timestamps, updated the atlas unit test, and superseded the R7 source/atlas counters. R9 regenerated the live atlas again at `2026-05-18 01:28:18`; keep the R7 numbers below as R7-time evidence only.

## Scope

R7 continued the local-only documentation actuality pass after R6. The focus was `Docs/DEPENDENCY_GRAPH.md` and its generator, because the active atlas had been generated on `2026-05-15` and contained stale current counters.

## Findings

- `Docs/DEPENDENCY_GRAPH.md` was stale as an active current atlas. It recorded:
  - generated time `2026-05-15 22:35:28`
  - first-party script C# files `1505`
  - first-party script lines `960494`
  - assembly definitions `153`
  - first-party assembly definitions `92`
- The R7-time generated atlas after rerun recorded:
  - generated time `2026-05-17 20:49` (historical R7 snapshot)
  - first-party script C# files `1619`
  - first-party script lines `1042343`
  - assembly definitions `158`
  - first-party assembly definitions `98`
- `Tools/BuildArchitectureAtlas.py` was itself overclaiming with `Status: ATLAS VERIFIED PENDING RUNTIME VERIFICATION` even though verification depends on a separate `Tools/AtlasCheck.py` run.

## Updates

- Ran `python Tools/BuildArchitectureAtlas.py`.
- Regenerated:
  - `Docs/DEPENDENCY_GRAPH.md`
  - `Docs/DEPENDENCY_GRAPH.json`
- Patched `Tools/BuildArchitectureAtlas.py` so future generated atlases:
  - include `Date:`
  - include the active R4 interior actuality boundary
  - use `Status: ATLAS GENERATED STATIC SOURCE / ATLASCHECK REQUIRED / RUNTIME PENDING`
  - emit matching JSON status
  - state that the atlas is not verified unless `Tools/AtlasCheck.py` exits `0`
- Added an R7 verification note inside `Docs/DEPENDENCY_GRAPH.md`.

## Verification

Commands:

```text
python Tools/BuildArchitectureAtlas.py
python -m py_compile Tools/BuildArchitectureAtlas.py Tools/AtlasCheck.py
python Tools/AtlasCheck.py
```

Results:

```text
BuildArchitectureAtlas.py: EXIT 0
py_compile: EXIT 0
AtlasCheck.py: EXIT 1
ATLAS_CHECK_FAIL references=6380 missing=57
```

R9 current live readback after later documentation edits is `ATLAS_CHECK_FAIL references=6444 missing=57`; the missing family remains RealtimeCSG vendor icon/readme images.

The `57` missing references are RealtimeCSG vendor icon/readme image paths:

- `Assets/RealtimeCSG/RealtimeCSG/Icons/icon_*`
- `Assets/RealtimeCSG/RealtimeCSG/Readme/Images/house_view.png`

This means the atlas is freshly generated, but not atlas-check verified.

## Boundary

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual proof was run.

No GitHub operation was run in R7. This is local-only documentation/tool evidence.
