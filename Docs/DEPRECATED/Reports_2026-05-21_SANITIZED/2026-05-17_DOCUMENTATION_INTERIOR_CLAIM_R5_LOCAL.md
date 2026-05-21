<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# Documentation Interior Claim R5 Local
Date: 2026-05-17
Status: LOCAL_ONLY_STATIC_DOC_CONTENT_PASS / RUNTIME PENDING
Agent: DOC_GLOBAL_DOCS_REFRESH
Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / GIT_WORKTREE
GitHub: intentionally not used in R5 per user order

## Purpose

R5 is a content pass, not a sorting pass. It checks internal claims in active documentation against current source/config facts and patches documents that were still carrying stale counters or incomplete runtime-tool boundaries.

## Source Facts Rechecked

Commands used local source/config only. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, save/load route, or visual proof was run.

- Unity project pin: `6000.4.1f1`.
- BuildSettings scenes: `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD`.
- Package pins: URP `17.4.0`, Addressables `2.7.6`, Input System `1.19.0`, AI Navigation `2.0.11`.
- First-party C# files under `Assets/_Project`: `1653`.
- First-party C# files under `Assets/_Project/Scripts`: `1602`.
- First-party non-test C# files: `1638`.
- First-party C# physical lines under `Assets/_Project`: `1065255`.
- First-party C# physical lines under `Assets/_Project/Scripts`: `1047015`.
- First-party non-test physical lines: `1061832`.
- Interface declaration hits under `Assets/_Project`: `288`.
- Direct public interfaces in `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: `63`.
- First-party asmdefs under `Assets/_Project`: `95`.
- Repository root markdown files: `3` (`AGENTS.md`, `BUILD_PLAYTEST_ISSUES.md`, `MASTER_RELEASE_WORK_PLAN.md`).
- `ModLoader.CurrentAPIVersion = 2`.
- `ModLoader` rejects missing/non-positive `RequiredAPIVersion` and consumes `ModPriority`.
- `ModBuilderWindow.ModManifestData` currently emits `7` fields and omits `RequiredAPIVersion` / `ModPriority`.

## Documents Corrected

- `Docs/DOC_GOVERNANCE.md`: added R5 source-scale facts and corrected asmdef count from `24` to `95`.
- `Docs/README.md`: corrected current source-scale counters, asmdef count, and evidence snapshot pointer.
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`: corrected source table, scope counts, interface count, asmdef count, and stale `41` direct-interface line.
- `Docs/PROJECT_STATE_STATIC_XRAY.md`: added R5 static inventory facts and corrected first-party C# counts.
- `Docs/ROOT_DOCS_REFERENCE.md`: converted old "current root scan" lines inside May 7/11/13 historical sections into dated observations and added the current R5 root scan.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`: corrected source table, source drift note, direct-interface count, asmdef count, and marked the older asmdef list as a partial sample.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`: corrected asmdef and source-scale counters.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/INTERFACE_STRATEGY.md`: corrected current `GlobalRegistryContracts.cs` direct public interface count to `63`.
- `Docs/Modding/README.md`: added the SDK builder manifest gap.
- `Docs/Modding/Mod_API_Specification.md`: added the SDK builder/runtime manifest mismatch.
- `Docs/Modding/Loader_Save_Audit_Matrix.md`: added the loader/builder mismatch boundary.

## Remaining Boundaries

- Historical dated reports were not rewritten.
- Batch prompt evidence in `Docs/Tasks/CURRENT_BATCH.md` was not normalized.
- Source/shader dirty files outside documentation scope remain owned by their code agents.
- R5 does not claim compile or runtime validity. It only repairs documentation claims against static source/config facts.
