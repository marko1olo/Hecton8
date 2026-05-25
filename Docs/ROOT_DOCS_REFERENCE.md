# Root Docs Reference

Date: 2026-05-24
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

- `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log` - last local zero-warning CLI PASS for `Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`.
- `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup147_gi_despawn.log` - latest CLI compile attempt; fails before C# with `NETSDK1004` missing `Temp/obj/Hecton8.Editor/project.assets.json` and `MSB3491` Temp/obj access denied. Runtime proof remains pending.
- EXTERNAL_CODEX loop157 is source-gated with an environment build wall: non-editor raw `Debug.Log` is zero outside `H8Debug.cs`; targeted frost/render membership, dispatcher lane `Contains`, HectonVoxelVolume sonar DataVault poll, pure Environment/Ocean/PlayerSensory context getter, cadence/context Dispatcher/DataVault/service/player rebind, persistent-world Save/Player/Inventory owner-cache, UI/audio/construction Dispatcher rebind, and UI/Construction singleton runtime greps are clean in touched scopes. Latest guarded build fails before C# with missing project.assets and Temp/obj access denied; latest build guard after loop157 skipped compile at `cpu=100`, `compiler_count=2`; broad file-local no-hot-swap scans still include known split-line/static-driver/legacy-stub false positives.

Superseded reports were moved to:

- `Docs/DEPRECATED/Reports_2026-05-21_SANITIZED/`
- `Docs/_Archive/Reports_X_012_2026-05-23/`

## Archive Boundary

- `Docs/DEPRECATED`, `Docs/_Archive`, and `Docs/Archive` are historical storage.
- Archived files must not be loaded by new agents as active contracts.
- To promote archived facts, copy only the current technical fact into an active contract and cite the source path.
