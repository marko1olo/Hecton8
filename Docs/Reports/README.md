# Reports Index

Date: 2026-05-24
Status: PENDING VERIFICATION
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Evidence class: STATIC_DOC / CLI_COMPILE where artifact cited

`Docs/Reports` stores current machine-readable evidence snapshots. Historical markdown reports are archived; a report does not override active contracts unless a current contract imports the fact.

## Current Boundary

- Archived root/architecture report set: `Docs/_Archive/Reports_X_012_2026-05-23/`
- Latest local zero-warning CLI compile slice: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log` (`Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`, 0 `: warning ` / 0 `: error ` text matches, CLI_COMPILE only).
- Latest CLI compile attempt: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup147_gi_despawn.log`; fails before C# with `NETSDK1004` missing project.assets and `MSB3491` Temp/obj access denied. Runtime proof remains pending.
- Latest source-gated cleanup: EXTERNAL_CODEX loop157 removes UI/Construction runtime singleton tails after loop154-156 Dispatcher unregister/re-register or lane-reset rebinds and cached Player ownership for `PDADeathMemoryDump`; targeted hot-swap and owner-cache greps pass; compile retry remains blocked by CPU/build environment guard and the same Temp/obj/project.assets wall before C# diagnostics.
- Archived sanitization report: `Docs/_Archive/Reports_X_012_2026-05-23/2026-05-21_DOCUMENTATION_SANITIZATION_REPORT.md`
- Current X_012 documentation scan artifacts: `DOCUMENTATION_CORPUS_INVENTORY_X_012.json`, `DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json`, `DOC_STRUCTURE_VALIDATION_X_012.json`
- Current X_012 binary-payload concision artifact: `BINARY_PAYLOAD_LEDGER_CONCISION_X_012.json`
- Current X_012 strict architecture paragraph artifact: `ARCHITECTURE_CONCISION_AUDIT_X_012.json`
- Current X_012 strict architecture line artifact: `ARCHITECTURE_LINE_CONCISION_AUDIT_X_012.json`
- Current X_012 architecture file-cap artifact: `ARCHITECTURE_FILE_CAP_AUDIT_X_012.json`
- Current X_012 residual prose/diff artifact: `ARCHITECTURE_RESIDUAL_PROSE_AUDIT_X_012.json`
- Current X_012 manual prose artifact: `ARCHITECTURE_MANUAL_PROSE_AUDIT_X_012.json`
- Current X_012 manual density artifact: `ARCHITECTURE_MANUAL_DENSITY_AUDIT_X_012.json`
- Current X_012 micro-density artifact: `ARCHITECTURE_MICRO_DENSITY_AUDIT_X_012.json`
- Current X_012 ultra-density artifact: `ARCHITECTURE_ULTRA_DENSITY_AUDIT_X_012.json`
- Current X_012 45-word density artifact: `ARCHITECTURE_45WORD_DENSITY_AUDIT_X_012.json`
- Current X_012 40-word density artifact: `ARCHITECTURE_40WORD_DENSITY_AUDIT_X_012.json`
- Current X_012 35-word density artifact: `ARCHITECTURE_35WORD_DENSITY_AUDIT_X_012.json`
- Current X_012 34-word density artifact: `ARCHITECTURE_34WORD_DENSITY_AUDIT_X_012.json`
- Current X_012 33-word density artifact: `ARCHITECTURE_33WORD_DENSITY_AUDIT_X_012.json`
- Current X_012 32-word density artifact: `ARCHITECTURE_32WORD_DENSITY_AUDIT_X_012.json`
- Archived terrain report compatibility stub: `Docs/_Archive/Reports_X_012_2026-05-23/TERRAIN_AND_BIOME_REALITY_MAP.md`; canonical terrain contract is `Docs/ARCHITECTURE/FLOODED_TERRESTRIAL_GEOGRAPHY.md`

## Archived Reports

Superseded dated documentation layers, old patch diffs, generated atlas copies, duplicate metric scans, and external research notes were moved to:

- `Docs/DEPRECATED/Reports_2026-05-21_SANITIZED/`
- `Docs/DEPRECATED/Documentation_Bundles_2026-05-21_SANITIZED/`
- `Docs/DEPRECATED/Reports_2026-05-21_REVALIDATION_QUARANTINE/`
- `Docs/DEPRECATED/Reports_2026-05-21_LOOP11_STALE_HANDOFF/`
- `Docs/DEPRECATED/X_012_Stale_DataMonolith_Reports_2026-05-23/`
- `Docs/_Archive/Reports_X_012_2026-05-23/`
- `Docs/_Archive/Architecture_X_012_APEX_2026-05-23/`
- `Docs/_Archive/Architecture_X_012_APEX_2026-05-24/`
- `Docs/_Archive/Architecture_X_012_APEX_2026-05-24_LINE_SPLIT/`
- `Docs/_Archive/Architecture_X_012_APEX_2026-05-24_FILE_CAP/`
- `Docs/_Archive/Architecture_X_012_APEX_2026-05-24_RESIDUAL_PROSE/`

Complete archived-file list:

- `Docs/DEPRECATED/Reports_2026-05-21_SANITIZED/ARCHIVED_FILES_2026-05-21.csv`
- `Docs/DEPRECATED/Documentation_Bundles_2026-05-21_SANITIZED/ARCHIVED_BUNDLES_2026-05-21.csv`
- `Docs/DEPRECATED/Reports_2026-05-21_REVALIDATION_QUARANTINE/ARCHIVED_REPORTS_REVALIDATION_2026-05-21.csv`
- `Docs/DEPRECATED/Reports_2026-05-21_LOOP11_STALE_HANDOFF/README.md`

## Use Rules

- Do not cite a report as `VERIFIED`, `COMPLETE`, or `PRODUCTION READY` without a current proof artifact.
- Promote durable technical facts into `Docs/ARCHITECTURE` before treating them as doctrine.
- Keep new reports concise: problem, changed files, source constants, evidence class, unresolved gaps.
