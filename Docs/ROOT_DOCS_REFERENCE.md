# Root Docs Reference

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_FILESYSTEM

## Root Policy

The repository root may contain only:

- `AGENTS.md`
- `MASTER_RELEASE_WORK_PLAN.md`
- `BUILD_PLAYTEST_ISSUES.md`

Filesystem scan result from `C:\hades\Hecton8`: no extra root `.md`, `.txt`, or log files were found.

`C:\hades` is not the repository root. Files in that parent directory are outside this policy unless the repo is explicitly moved.

## Active Entry Points

- `Docs/README.md` - active documentation map and source-reality snapshot.
- `Docs/DOC_GOVERNANCE.md` - documentation maintenance rules.
- `Docs/QUALITY_GATES.md` - evidence and acceptance gates.
- `Docs/SYSTEMS_CONTRACTS.md` - stable cross-system contracts.
- `Docs/ARCHITECTURE/README.md` - architecture contract index.
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` - documentation corpus changelog and current source constants.

## Report Boundary

`Docs/Reports` is evidence storage, not authority. Current local report boundary:

- `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`

Superseded reports were moved to:

- `Docs/DEPRECATED/Reports_2026-05-21_SANITIZED/`

## Archive Boundary

- `Docs/DEPRECATED`, `Docs/_Archive`, and `Docs/Archive` are historical storage.
- Archived files must not be loaded by new agents as active contracts.
- To promote archived facts, copy only the current technical fact into an active contract and cite the source path.
