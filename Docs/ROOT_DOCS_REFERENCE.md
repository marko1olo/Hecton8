# Root Docs Reference

Date: 2026-05-26
Status: PENDING VERIFICATION
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Evidence class: STATIC_DOC / STATIC_FILESYSTEM / CLI_COMPILE where artifact cited

## Root Policy

The repository root may contain only:

- `AGENTS.md`
- `MASTER_RELEASE_WORK_PLAN.md`
- `BUILD_PLAYTEST_ISSUES.md`

Filesystem scan result from `C:\hades\Hecton8`: no extra root `.md`, `.txt`, or log files were found.

`C:\hades` is not the repository root. Files in that parent directory are outside this policy unless the repo is explicitly moved.

Pre-X_012 verbose root copies:

- `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/MASTER_RELEASE_WORK_PLAN.md`
- `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/BUILD_PLAYTEST_ISSUES.md`

## Active Entry Points

- `Docs/README.md` - active documentation map and source-reality snapshot.
- `Docs/DOC_GOVERNANCE.md` - documentation maintenance rules.
- `Docs/QUALITY_GATES.md` - evidence and acceptance gates.
- `Docs/SYSTEMS_CONTRACTS.md` - stable cross-system contracts.
- `Docs/ARCHITECTURE/README.md` - architecture contract index.
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` - documentation corpus changelog and current source constants.

## Report Boundary

`Docs/Reports` is evidence storage, not authority. Current local report boundary:

- `Docs/_Archive/Reports_X_012_2026-05-23/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`
- `Docs/Reports/DOCUMENTATION_CORPUS_INVENTORY_X_012.json`
- `Docs/Reports/DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json`
- `Docs/Reports/DOC_STRUCTURE_VALIDATION_X_012.json`

Current local CLI compile slice:

- `Docs/Reports/BUILD_UNKNOWN_RECHECK_20260526_020504.log` - full `Hecton8.slnx` pass, exit `0`, `0 Warning(s)`, `0 Error(s)`.
- Command: `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`.
- Evidence class: CLI_COMPILE only. Runtime proof remains pending.
- Older EXTERNAL_CODEX build-wall entries are historical source-gate context, not the current compile boundary.

Superseded reports were moved to:

- `Docs/DEPRECATED/Reports_2026-05-21_SANITIZED/`
- `Docs/_Archive/Reports_X_012_2026-05-23/`

## Archive Boundary

- `Docs/DEPRECATED`, `Docs/_Archive`, and `Docs/Archive` are historical storage.
- Archived files must not be loaded by new agents as active contracts.
- To promote archived facts, copy only the current technical fact into an active contract and cite the source path.
