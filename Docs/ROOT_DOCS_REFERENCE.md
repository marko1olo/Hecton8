# Root Docs Reference

Date: 2026-05-28
Status: STATIC POLICY
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_DOC / STATIC_FILESYSTEM

## Root Policy

The repository root may contain only these active text anchors:

- `AGENTS.md`
- `TASTE.md`
- `MASTER_RELEASE_WORK_PLAN.md`
- `BUILD_PLAYTEST_ISSUES.md`

Generated Unity/project files such as `.csproj`, `.slnx`, CSV, package, and build config files are not active documentation. Do not move them during documentation cleanup.

`C:\hades` is not the repository root. Files in that parent directory are outside this policy unless the repo is explicitly moved.

Pre-cleanup verbose root copies are historical only:

- `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/MASTER_RELEASE_WORK_PLAN.md`
- `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/BUILD_PLAYTEST_ISSUES.md`

## Active Entry Points

- `Docs/README.md` - active documentation map.
- `Docs/PROJECT_BASELINE.md` - stable project baseline and documentation boundary.
- `TASTE.md` - taste authority for gameplay, design, presentation, screenshot, audio, UI, creature, base, and marketing review.
- `Docs/DOC_GOVERNANCE.md` - documentation maintenance rules.
- `Docs/QUALITY_GATES.md` - evidence and acceptance gates.
- `Docs/SYSTEMS_CONTRACTS.md` - stable cross-system contracts.
- `Docs/PROJECT_ATLAS.md`, `Docs/DEPENDENCY_GRAPH.md`, `Docs/ARCHITECT_HANDBOOK.md` - short tool-entry contracts only.
- `Docs/Generated` - generated artifact storage, not root doctrine.
- `Docs/Data/Profiles` - static authoring/tuning profiles, not root doctrine.
- `Docs/ARCHITECTURE/README.md` - architecture contract index.
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` - source constants, concise proof snapshots, and documentation-change register.
- `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` - current static scene spine, package envelope, and source owner map.
- `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` - 85-domain roster mapped to active architecture docs and source areas by echelon.

## Evidence Placement

- `Docs/Reports` is evidence storage, not authority.
- Current build logs, scanner JSON, report chains, prompt extracts, status files, and local telemetry must not be promoted into root docs as prose.
- Durable facts are promoted into `Docs/ARCHITECTURE` or another stable contract after source/proof review.
- A scoped green report is not a global green build.

## Archive Boundary

- `Docs/DEPRECATED`, `Docs/_Archive`, and `Docs/Archive` are historical storage.
- Archived files must not be loaded as active contracts.
- To promote archived facts, copy only the current technical fact into an active contract and cite the source path.
