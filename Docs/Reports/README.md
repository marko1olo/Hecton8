# Reports

Date: `2026-05-03`
Status: `PENDING VERIFICATION`

Purpose: canonical drop zone for new reports, audits, and validation writeups that are still active.

## Naming

- single-file report: `YYYY-MM-DD_TaskName.md`
- multi-file report bundle: `YYYY-MM-DD_TaskName/`

## Rule

- do not create new report files in repo root
- do not drop one-shot reports loose in `Docs/`
- when a report is older than `5` days and no longer drives current work, archive it

## Current High-Authority Reports

- `2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`
- `2026-05-01_CURRENT_PROJECT_STATE.md`
- `2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`
- `2026-05-01_EDITOR_LOG_CONSOLE_STABILIZATION.md`
- `2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md`
- `2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md`
- `2026-05-01_EVENT_CASCADE_RECHECK.md`
- `2026-05-01_ZERO_GC_JOBS_CONTINUATION_DELTA.md`
- `TOTAL_CODEBASE_AUDIT_V2.md`
- `OMEGA_CORE_ENFORCEMENT_2026-05-01.md`
- `AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md`
- `DOOMSDAY_FLAW_REPORT.md`

`2026-05-01_CURRENT_PROJECT_STATE.md` is the current conceptual entry point.
It keeps its stable path but now includes May 2 evidence. It does not replace source files or runtime verification logs; it defines which active reports should be read first.

`2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` is the latest documentation-sweep addendum.
It records the active-doc inventory boundary, dirty-worktree risk, conflicting old logs, fresh local `dotnet build .\Hecton8.Core.csproj` result: `0 Error(s)`, `136 Warning(s)`, elapsed `00:01:24.05`, and latest post-restore `--no-restore` rerun result: `0 Error(s)`, `73 Warning(s)`, elapsed `00:00:23.95`.

`2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md` is the current blunt project-level verdict.
It is source/doc-backed, but still not Play Mode proof.

`2026-05-01_EDITOR_LOG_CONSOLE_STABILIZATION.md` is the current local `Editor.log` evidence for console-spam mitigation.
It supersedes older same-day statements that the editor console had known C# warnings or `SetResource` spam in the latest reachable local log, but it is not Play Mode or profiler proof.

`2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md` records the current compile-clean source migration around listener-backed Sargassum/Emergency relay events, the Burst spatial-hash `in` argument fix, and the latest MCP console zero-entry check.

`2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md` supersedes the older compile-evidence line numbers after the `VegetationJobRecovery.cs.meta` restoration. It records the Bee file-lock/internal-error recovery, current `Editor.log` compile/reload success, and final MCP console zero-entry check.

`2026-05-01_EVENT_CASCADE_RECHECK.md` corrects stale event-bus audit claims.
It confirms the source-present `HectonEventBus` depth cap and keeps NativeQueue generation split as the remaining event-cascade risk. As of 2026-05-03, `BootstrapEvents`, `InteractionEvents`, `CraftingEvents`, `ScanEvents`, `SaveEvents`, `InventoryEvents`, `WeatherEvents`, `QuestEvents`, `PowerGridTelemetryEvents`, `NarrativeEvents`, `NotificationEvents`, `FirstHourEvents`, `EndingEvents`, `AtmosphereEvents`, `EclipseGameplayEvents`, `AcousticZoneEvents`, `CelestialEvents`, `MapMagicBiomeEvents`, and `BiomeMatrixEvents` have source-level generation split; remaining lanes still require review and runtime proof.

## Active Secondary Reports

These are useful, but not first-read project-state authority:

- `CI_VALIDATION_HOOKS_SURGERY_LOG.md`
- `NAVGRID_LEAK_PURGE_SURGERY_LOG.md`
- `OMEGA_PURGE_SURGERY_LOG.md`
- `GC_SINGLETON_KILL_LIST.md`

## Evidence Artifacts

Patch files are evidence artifacts, not narrative authority:

- `2026-04-29_Habitat_Logistics_Graph_Diff.patch`
- `NAVGRID_LEAK_PURGE_DIFF.patch`

For full doc importance sorting, read:

- `../ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md`

## Deprecated

Historical static snapshots moved out of the active report root:

- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/ILLEGAL_SINGLETONS.md`
- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/GC_HOTPATH_VIOLATIONS.md`
- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/BOOT_ORDER_VIOLATIONS.md`
- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/NATIVE_ALLOCATION_AUDIT.md`
