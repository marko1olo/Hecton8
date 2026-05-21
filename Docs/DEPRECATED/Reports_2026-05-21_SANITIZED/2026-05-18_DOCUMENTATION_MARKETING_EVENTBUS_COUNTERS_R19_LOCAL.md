<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-18 Documentation Marketing / EventBus / Counters R19 Local

Date: 2026-05-18
Status: LOCAL_ONLY STATIC_DOC / STATIC_SOURCE PASS; RUNTIME PROOF ABSENT

<!-- DOC_GLOBAL_DOCS_REFRESH:R19_REPORT_SNAPSHOT_BOUNDARY_START -->
## R19 Report Snapshot Boundary

This direct report file is a dated/static snapshot from the DOC_GLOBAL_DOCS_REFRESH sequence. It is evidence, not durable policy by itself.

Use stable authority files first: `AGENTS.md`, `.agents-skills`, `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, current source files, current official platform rules, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, Steam page, public demo, wishlist performance, creator outreach readiness, or visual-route proof is implied by this report.
<!-- DOC_GLOBAL_DOCS_REFRESH:R19_REPORT_SNAPSHOT_BOUNDARY_END -->

## Scope

R19 continued the local documentation refresh after R18. It focused on active internal claims rather than file sorting:

- Marketing KPI and platform-rule wording.
- Creator outreach verification state.
- Regional/localized pitch safety.
- EventBus / `GlobalSignals` lane-count drift.
- Volatile C# and asmdef counters after concurrent source churn.
- Active entrypoint/index read-order drift after R18.

## Current Static Snapshot

R19 source scan, capture-time only:

- `Assets/_Project/**/*.cs`: `1781`.
- `Assets/_Project/Scripts/**/*.cs`: `1726`.
- First-party non-test C# files: `1761`.
- Project C# physical lines: `1166702`.
- Script C# physical lines: `1147077`.
- Non-test C# physical lines: `1161984`.
- Direct public interfaces in `GlobalRegistryContracts.cs`: `63`.
- First-party asmdefs under `Assets/_Project`: `109`.

R19 `GlobalSignals.cs` static source scan:

- `InitializeAllQueues()` contains `73` direct `CreateQueue(...)` native queue slots.
- `InitializeCategorySignalLanes()` contains `132` `SignalBus<T>.EnsureInitialized()` typed lanes.
- `ConfigureDebugSignalLane()` initializes the `DebugSignal` lane.
- Modding static validator still reports `160 / 2 / 158` signal split.

These are static source counts, not compile, Unity, runtime, profiler, or player-build proof. Rerun before exact use because concurrent agents are mutating the workspace.

## Updates

- `Docs/Marketing/MARKETING_PREP_MASTER_PLAN.md`
  - Added an R19 KPI/forecast boundary.
  - Marked wishlist, clip, demo, and Next Fest targets as `INTERNAL_ASSUMPTION / PENDING_BENCHMARK_SOURCE`, not forecasts or public claims.
- `Docs/Marketing/KPI/MARKETING_DASHBOARD_SPEC.md`
  - Marked all dashboard target bands as provisional until replaced by Steam/UTM/outreach/demo telemetry.
- `Docs/Marketing/PREP_DIRECTIONS_NOW.md`
  - Added Steam Wishlist notification caveats: launch/Early Access/full-release, qualifying discount, and one-time public-demo notifications are subject to Steam email eligibility, cooldowns, user settings, and current rules.
- `Docs/Marketing/Press/PRESS_KIT_AND_MEDIA_PLAN.md`
  - Added `KEY_POLICY_PENDING` for pre-release press/influencer access and Release State Override/developer-comp/press/beta key constraints.
- `Docs/Marketing/CreatorOutreach/CREATOR_OUTREACH_DATABASE.md`
  - Added mandatory verification-state export boundary; rows without explicit state remain implicitly `VERIFY_BEFORE_CONTACT`.
- `Docs/Marketing/Regional/REGIONAL_OUTREACH_PLAN.md`
  - Marked localized pitch drafts as `LOCALIZATION_REVIEW_PENDING / DO_NOT_SEND`.
- `Docs/Marketing/README.md` and `Docs/Marketing/NO_COOP_PUBLIC_POSITIONING.md`
  - Added Subnautica 2 source-check caveats before using co-op/multiplayer positioning as competitor contrast.
- `Docs/ARCHITECTURE/BOOT_SEQUENCE_TOPOLOGY.md`, `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/01_EXECUTIVE_FORENSIC_SUMMARY.md`, and `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/DOC_CHRONOS_ARCHITECTURE_TRUTH_SYNC_2026-05-12.md`
  - Replaced stale `33 typed NativeQueue lanes` claims with R19 source-observed direct queue and `SignalBus<T>` lane counts.
- `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/Reports/README.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, and Archivarius indexes
  - Promoted R19 as the current DOC_GLOBAL boundary and demoted R18/R17/R16/R15 to subordinate correction layers.
  - Replaced stale R11/R18 counters with the R19 volatile static snapshot where the docs were active current-entry surfaces.
- `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`, `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`, `Docs/ARCHITECTURE/ORGANIC_ENTROPY_MATH.md`, and `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md`
  - Replaced unsupported `verified` / validator wording with static-observation or artifact-required language.

## Validation

Validation was run after edits and must be read as static documentation/source evidence only:

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs\DEPENDENCY_GRAPH.md`, `Docs\DEPENDENCY_GRAPH.json`, and cache.
- `python Tools\test_architecture_atlas.py`: exit `0`, `9` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File Docs\Modding\Validate_Mod_API_Static.ps1`: `PASS`, schema revision `14`, source signals `160`, allowed projected `2`, denied-by-default `158`.
- JSON parse spot check: `JSON_OK_COUNT=9` for dependency graph, mod signal schema, active documentation actuality manifest, Batch008 move manifests, and combined Batch008 manifests.
- R19 scoped R4 boundary scan: `R19_SCOPE_FILES=105`, `R19_SCOPE_MISSING_BOUNDARY=0`, `R19_SCOPE_DUPLICATE_R4=0`.
- Targeted stale counter scan: no active scoped hits for the superseded R19 draft numbers `1766 / 1712 / 1747 / 1157081 / 1137551 / 1152479`.
- Targeted proof/stale scan: no actionable stale `33 typed NativeQueue`, `Source verified`, or stale-current DOC_GLOBAL navigation hits in the R19 target scope; remaining hits are explicit current R19 read-order or historical R6 schema context.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6516 missing=57`; missing references remain RealtimeCSG vendor icon/readme image paths.
- `git diff --check -- Docs Tools ':!Docs/Tasks/CURRENT_BATCH.md'`: exit `0`, line-ending warnings only.

## Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, public Steam page, public demo, wishlist telemetry, creator outreach telemetry, or visual-route proof was run.
- `Tools/AtlasCheck.py` remains red in R19 on `57` missing RealtimeCSG vendor icon/readme image references.
- R19 source counters are capture-time static values only. Concurrent source churn can invalidate exact counts; rerun before any contractual use.
