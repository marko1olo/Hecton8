# Root Docs Reference

Date: `2026-05-04`
Status: `PENDING VERIFICATION`

Purpose: explain what still remains in repository root versus `Docs/` after the current documentation cleanup.

This file is navigation only.
It is not runtime proof.

## Repository Root Text Files Seen In Current Scan

| File | Current handling |
|---|---|
| `AGENTS.md` | active operating contract; keep in root |
| `MASTER_RELEASE_WORK_PLAN.md` | active production roadmap anchor; keep in root unless a later roadmap migration is approved |
| `BUILD_PLAYTEST_ISSUES.md` | active validation/build observation ledger; keep in root unless a later QA migration is approved |
| `TERRAIN_AND_BIOME_REALITY_MAP.md` | root compatibility mirror / stale legacy surface; canonical current report is `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md` |
| `DOCS_GAMEPLAY_API.md` | moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/` |
| `THIRD_PARTY_POISON.md` | moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/`; current ACL reference is `Docs/ARCHITECTURE/THIRD_PARTY_POISON.md` |
| `NAMING_VIOLATIONS.md` | moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/` |
| `cyrillic_violations.txt` | moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/` |
| `OUR PRINCIPLES - Copy.txt` | moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/`; current doctrine is `AGENTS.md` plus `.agents-skills/` |
| root `*.log` files from the May 4 pre-move scan | moved to `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-04/README.md`; raw evidence only |

## Root `Docs/` Surface After This Cleanup

Flat redirect stubs for flora and scatter documents were moved out of root `Docs/`.
The current root `Docs/` folder is reduced to broad active anchors and indexes:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `Docs/PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
- `Docs/QUALITY_GATES.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/ROOT_DOCS_REFERENCE.md`

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

The repository root text surface is reduced to three active anchors plus `TERRAIN_AND_BIOME_REALITY_MAP.md`.
Treat it as a compatibility mirror only.
If new root text files appear, classify them before treating them as current authority.

## 2026-05-04 Check

Root documentation anchors remain `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`.
Root `TERRAIN_AND_BIOME_REALITY_MAP.md` is not active authority; use `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`.
Root `.log` files were moved to `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-04/README.md`.
`.codex-artifacts/**` remains evidence artifact storage, not documentation authority.
Latest documentation sweep: `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`.
Latest documentation sorting map: `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`.
Latest header/archive queue: `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md`.
Current root text scan after the log move saw `4` root `.md` files and `0` root `.txt`/`.log` files; only the three anchors listed above are active documentation authority.
