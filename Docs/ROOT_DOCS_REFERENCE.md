# Root Docs Reference

Date: 2026-05-17
Status: PENDING VERIFICATION

Purpose: explain what still remains in repository root versus `Docs/` after the current documentation cleanup.

This file is navigation only.
It is not runtime proof.

## 2026-05-17 Check

Current root text scan:

| Class | Count | Handling |
|---|---:|---|
| root `.md` | 3 | active anchors only |
| root `.log` | 0 | none in root text scope |
| root `.json` | 0 | none in root text scope |
| root `.txt` | 0 | none in root text scope |

Current root markdown files:

- `AGENTS.md` - active operating contract.
- `MASTER_RELEASE_WORK_PLAN.md` - active production roadmap anchor.
- `BUILD_PLAYTEST_ISSUES.md` - active validation/build observation ledger.

Former root compute drift:

- `COMPUTE_AUDIT_BRIEF.md` moved to `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_AUDIT_BRIEF.md` during the 2026-05-17 R3 documentation integration pass. Do not treat it as a root authority anchor.

Latest broad documentation refresh:

- `Docs/Reports/2026-05-17_DOCUMENTATION_GLOBAL_REFRESH.md`

The May 15 three-anchor root state is again the current filesystem target after the R3 compute-brief move.

## 2026-05-15 Check

Current root text scan:

| Class | Count | Handling |
|---|---:|---|
| root `.md` | 3 | active anchors only |
| root `.log` | 0 | root logs moved to deprecated evidence bundles |
| root `.json` | 0 | root JSON evidence moved to deprecated evidence bundles |
| root `.txt` | 0 | no active root text authority |

Current root non-anchor files:

- None in the root documentation/evidence cleanup scope.
- `BROKEN_PREFABS.md` moved to `Docs/Reports/2026-05-13_BROKEN_PREFABS_STATIC_SNAPSHOT.md`.
- Root `COMPUTE_*.md` reports moved to `Docs/Reports/2026-05-15_COMPUTE_AUDIT/`.
- Root `PROJECT_ATLAS.md` and `TERRAIN_AND_BIOME_REALITY_MAP.md` moved to `Docs/DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/`.

Root noise moved:

- raw logs, JSON, XML, PNG, zip, and `clean_project.py` moved to `Docs/DEPRECATED/External_And_Log_Bundles/Root_Evidence_2026-05-15/`.

Docs-root cleanup:

- `Docs/?? ????.md` was a stale batch-prompt dump. It was moved to `Docs/DEPRECATED/Root_Stale_Batch_Prompt_Dumps_2026-05-13/`.

Proof-reference warning:

- May 11 docs cite `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt` and `.log`; both are absent in the current filesystem. Treat May 11 compile claims as report text only until artifacts are restored or replaced.

## Repository Root Text Files Seen In Current Scan

| File | Current handling |
|---|---|
| `AGENTS.md` | active operating contract; keep in root |
| `MASTER_RELEASE_WORK_PLAN.md` | active production roadmap anchor; keep in root unless a later roadmap migration is approved |
| `BUILD_PLAYTEST_ISSUES.md` | active validation/build observation ledger; keep in root unless a later QA migration is approved |
| `BROKEN_PREFABS.md` | moved to `Docs/Reports/2026-05-13_BROKEN_PREFABS_STATIC_SNAPSHOT.md` |
| `PROJECT_ATLAS.md` | moved to `Docs/DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/`; canonical detailed copy is `Docs/PROJECT_ATLAS.md` |
| `TERRAIN_AND_BIOME_REALITY_MAP.md` | moved to `Docs/DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/`; canonical current report is `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md` |
| `DOCS_GAMEPLAY_API.md` | moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/` |
| `THIRD_PARTY_POISON.md` | moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/`; current ACL reference is `Docs/ARCHITECTURE/THIRD_PARTY_POISON.md` |
| `NAMING_VIOLATIONS.md` | moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/` |
| `cyrillic_violations.txt` | moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/` |
| `OUR PRINCIPLES - Copy.txt` | moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/`; current doctrine is `AGENTS.md` plus `.agents-skills/` |
| root `*.log` files from the May 4 pre-move scan | moved to `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-04/README.md`; raw evidence only |

## Root `Docs/` Surface After This Cleanup

Flat redirect stubs for flora and scatter documents were moved out of root `Docs/`.
The current root `Docs/` folder is reduced to broad active anchors and indexes:

- `Docs/Actual Domains of Project.txt`
- `Docs/ARCHITECT_HANDBOOK.md`
- `Docs/DEPENDENCY_GRAPH.cache.json`
- `Docs/DEPENDENCY_GRAPH.json`
- `Docs/DEPENDENCY_GRAPH.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/H8_GLOSSARY.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `Docs/PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
- `Docs/PROJECT_ATLAS.md`
- `Docs/PROJECT_STATE_STATIC_XRAY.md`
- `Docs/QUALITY_GATES.md`
- `Docs/README.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/TECH_ART_PBR_SURFACE_DOCTRINE.md`
- `Docs/TECHNICAL_FAQ.md`

## Deprecated Root Redirect Stubs

The old flat redirect stubs now live in:

- `Docs/DEPRECATED/Root_Redirect_Stubs_2026-05-01/README.md`

The encoding-damaged geology production plan now lives in:

- `Docs/DEPRECATED/Encoding_Damaged_2026-05-01/README.md`

The old repository-root legacy/scanner artifacts now live in:

- `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/README.md`

Current canonical bundle entry points:

- `Docs/Flora_Pipeline/README.md`
- `Docs/Scatter_Runtime/README.md`

## Future Cleanup Candidate

The repository root text surface is currently three markdown files: `AGENTS.md`,
`MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`. If new root text files
appear, classify them before treating them as current authority.

## 2026-05-13 Check

Historical state before the May 15 cleanup:

- root markdown contained active anchors plus `BROKEN_PREFABS.md`, `PROJECT_ATLAS.md`, and `TERRAIN_AND_BIOME_REALITY_MAP.md`.
- root logs and JSON files existed as raw evidence/noise.
- May 15 cleanup moved those non-anchor files to the paths listed in the 2026-05-15 check above.

## 2026-05-07 Check

Root documentation anchors remain `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`.
Root `TERRAIN_AND_BIOME_REALITY_MAP.md` is not active authority; use `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`.
Root `BROKEN_PREFABS.md` is not active authority; it is a generated snapshot and must be summarized in a dated `Docs/Reports/` file before citation.
Root `.log` files were moved to dated bundles under `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_*/`.
`.codex-artifacts/**` remains evidence artifact storage, not documentation authority.
Latest documentation synchronization pass: `Docs/Reports/2026-05-07_MAIN_DOCUMENTATION_CURRENT_STATE_REFRESH.md`.
Latest final-inquisition proof boundary: `Docs/Reports/2026-05-07_FINAL_INQUISITION_NATIVE_SCANNER.md`.
Latest Project Atlas/source-count synchronization pass: `Docs/Reports/2026-05-07_PROJECT_ATLAS_SYNCHRONIZATION_PASS.md`.
Latest documentation sweep: `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`.
Latest documentation sorting map: `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`.
Latest header/archive queue: `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md`.
Current root text scan saw `5` root `.md` files and `0` root `.txt`/`.log` files; only the three anchors listed above are active documentation authority.

## 2026-05-11 Check

Root documentation anchors remain `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`.
Root `TERRAIN_AND_BIOME_REALITY_MAP.md` is still not active authority; use `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`.
Root `BROKEN_PREFABS.md` is still not active authority; it is a generated snapshot and must be summarized in a dated `Docs/Reports/` file before citation.
Current root text scan saw `5` root `.md` files and `0` root `.txt`/`.log` files.
Latest documentation/data continuation: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
Latest `.agents-skills` doctrine update: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`.
Latest active documentation manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
May 11 report text claimed completed Core build evidence at `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`, but DOC_AUDIT did not find that summary or raw log. Treat it as stale report text. Current May 14/R43 evidence is the external root `Hecton8*.csproj` no-restore CLI sweep at `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; it is not Unity Console, Play Mode, profiler, GCMonitor, player-build, frame-time, memory, import, scene-wiring, or visual-quality proof.
Current Unity MCP proof was not run in the May 11 continuation; older MCP editor-console readbacks are historical only.

## 2026-05-13 R2 Check

Latest documentation counter/missing-artifact override: `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`.
The May 11 active manifest remains the latest machine-readable manifest, but its numeric counters are historical where May 13 R2 conflicts.
Current direct `Docs/` root has `11` markdown files plus `Actual Domains of Project.txt`.
Current root text scan sees `6` root `.md`, `3` root `.log`, `3` root `.json`, and `0` root `.txt`.

## 2026-05-13 R9 Check

Latest root-governance override remains `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`; R9 adds mirror/atlas boundary cleanup to that same report.

Current root scan:

- `6` root markdown files: `AGENTS.md`, `BROKEN_PREFABS.md`, `BUILD_PLAYTEST_ISSUES.md`, `MASTER_RELEASE_WORK_PLAN.md`, `PROJECT_ATLAS.md`, `TERRAIN_AND_BIOME_REALITY_MAP.md`.
- `3` root log files: raw evidence/noise, not documentation authority.
- `3` root json files: raw evidence/noise, not documentation authority.
- `0` root txt files.

Current active root authority remains exactly:

- `AGENTS.md`
- `MASTER_RELEASE_WORK_PLAN.md`
- `BUILD_PLAYTEST_ISSUES.md`

Root non-authority handling:

- `BROKEN_PREFABS.md` is a generated snapshot and currently only says missing scripts = `0`; it is not a prefab-health certificate without fresh Unity import/Console proof.
- `PROJECT_ATLAS.md` is a short compatibility mirror. Use `Docs/PROJECT_ATLAS.md` for the detailed asmdef graph.
- `TERRAIN_AND_BIOME_REALITY_MAP.md` is a root compatibility mirror / stale legacy surface. Use `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md` first.

Atlas boundary:

- `PROJECT_ATLAS.md` / `Docs/PROJECT_ATLAS.md` are static first-party asmdef graph snapshots only.
- They are not package/config/runtime authority and do not override `AGENTS.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, or fresh Unity/runtime proof.
