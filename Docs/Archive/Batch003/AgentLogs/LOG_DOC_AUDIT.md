# LOG_DOC_AUDIT

Date: 2026-05-13  
Status: PENDING VERIFICATION  
Evidence class: STATIC_SOURCE / STATIC_DOC  

## What Was Wrong

- Stable docs cited May 11 build artifacts that are absent from the current filesystem.
- Root/docs surface drifted: root gained `PROJECT_ATLAS.md`, `.log`, and `.json` evidence files; direct `Docs/` contained a stale Cyrillic-named batch prompt dump.
- Atlas counters were stale: older docs claimed `13`, `22`, or `23` first-party asmdefs; current static scan sees `24`.
- Source counters were stale and volatile under live multi-agent churn.
- `SYSTEMS_CONTRACTS.md` listed target file names that do not exist in current first-party source.
- `DOC_CHRONOS_ARCHITECTURE_TRUTH_SYNC_2026-05-12.md` claimed `VERIFIED MASTER GRADE` for a documentation audit; that status violated the current evidence ceiling after May 13 supersession.

## What Was Done

- Added `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`.
- Added May 13 override blocks to `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/Reports/README.md`, `Docs/ARCHITECTURE/README.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, and Archivarius indexes.
- Moved stale direct `Docs` batch dump to `Docs/DEPRECATED/Root_Stale_Batch_Prompt_Dumps_2026-05-13/` and added a README.
- Updated atlas counters to `1411` first-party C# files, `866103` project source lines, `204` interface declaration hits, and `24` first-party asmdefs.
- Verified `Docs/PROJECT_ATLAS.md` and root `PROJECT_ATLAS.md` now report current asmdef reality; updated `ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md` and stable authority files where they were stale.
- Added `SYSTEMS_CONTRACTS.md` source x-ray table: absent target labels are now explicitly separated from current source owners.
- Changed DOC_CHRONOS status to historical static audit / superseded counters / runtime pending.

## Cinematic Cheats Used

- None in runtime. Documentation-only pass.
- Visual-fake doctrine applied as reporting discipline: static source presence is not treated as runtime proof.

## Exact Microseconds Saved

- Runtime frame savings: `0 us/frame`.
- Build-time savings: `0 us` because no compile was run.
- Human/agent navigation savings: not measured; no fake number reported.

## Verification

- Confirmed stale prompt dump no longer exists at direct `Docs/` root.
- Confirmed moved dump and deprecated README exist.
- Confirmed active authority grep no longer reports `VERIFIED MASTER` in stable docs or active Archivarius indexes except historical/deprecated/archive/task prompt material.
- Confirmed final static counts: `24` first-party asmdefs, `1411` first-party C# files, `204` interface declaration hits.
- `git diff --check` on touched docs reported line-ending warnings only; no whitespace error diagnostics were emitted.

STATUS: PENDING VERIFICATION

---

Date: 2026-05-13 R2  
Status: PENDING VERIFICATION  
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM  

## What Was Wrong

- Active non-report docs still called the absent May 11 build artifact `Current compile-only evidence`.
- `Docs/README.md`, report indexes, and architecture atlas files had R1/R2 counters that were already stale under concurrent agent churn.
- A new direct root doc, `Docs/PROJECT_STATE_STATIC_XRAY.md`, appeared during the audit and needed classification in the documentation spine.
- `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md` still had a status line implying the missing May 11 Core build artifact was present/current.

## What Was Done

- Demoted the exact stale `Current compile-only evidence` line in 35 active non-report reference docs to a May 13 DOC_AUDIT override.
- Updated `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, `Docs/README.md`, `Docs/Reports/README.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`, `Docs/DOC_GOVERNANCE.md`, and `Docs/ROOT_DOCS_REFERENCE.md` with latest static counters and the new `PROJECT_STATE_STATIC_XRAY.md` classification.
- Added a supersession note to `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Verified `Docs/PROJECT_STATE_STATIC_XRAY.md` top inventory claims and corrected its dirty-worktree count to `376` entries: `259` modified-like, `116` untracked, `1` deleted.

## Cinematic Cheats Used

- None in runtime. Documentation-only pass.
- The only "cheap fake" was reporting discipline: static source scans are allowed to guide risk, but cannot be sold as runtime behavior.

## Exact Microseconds Saved

- Runtime frame savings: `0 us/frame`.
- Build-time savings: `0 us`; no compile or Unity run was executed.

## Current Static Snapshot

- First-party asmdefs: `24`.
- First-party C# files: `1411`.
- First-party script C# files: `1365`.
- Interface declaration hits: `215`.
- Project source lines: `868545`.
- Script source lines: `850990`.
- Docs markdown files: `916`.
- Active markdown files: `536`.
- Active non-report markdown files: `403`.

## R2 Verification

- Stale `Current compile-only evidence:` lines are removed from active non-report docs; remaining hits are DOC_AUDIT explanatory text only.
- May 11 summary/raw build artifacts are still absent.
- Direct `Docs/не откр.md` remains absent; deprecated moved copy remains present.
- `git diff --check -- Docs` returned exit code `0`; output was CRLF normalization warnings only.

STATUS: PENDING VERIFICATION

---

Date: 2026-05-13 R3  
Status: PENDING VERIFICATION  
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM  

## What Was Wrong

- `GlobalRegistryContracts.cs` currently exposes `51` direct public interfaces, but active Archivarius interface docs still used `41` as the current count.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO` referenced `INTERFACE_HEALTH_DASHBOARD.md` and `EVENT_FLOW_MAP.md` as local files; they are physically under `../02_ACTUAL_REPORTS/`.
- `PROJECT_ATLAS.md` still pointed MapMagic node paths at `Assets/_Project/Scripts/World/`, but current source has them under `Assets/_Project/Scripts/Plugins/MapMagic/`.
- `PROJECT_ATLAS.md` still listed a direct `Assets/_Project/UI` folder; that folder is absent.

## What Was Done

- Updated `ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/INTERFACE_HEALTH_DASHBOARD.md` with the `51`-interface R3 override and current interface-name list.
- Updated `ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`, `DOCSET_COVERAGE_MATRIX.md`, `INTERFACE_STRATEGY.md`, `MASTER_INDEX.md`, `EVENT_BUS_MAP.md`, `CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`, `NARRATIVE_DISCOVERY_PROGRESSION_SYSTEM_MAP.md`, and `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md` for current interface/path reality.
- Updated the May 13 X-Ray, Docs index, Reports index, and global architecture map with R3 interface-count drift.
- Updated `Docs/PROJECT_STATE_STATIC_XRAY.md` dirty-worktree count to the R3 observed `378` entries.

## Cinematic Cheats Used

- None in runtime. Documentation-only pass.
- No fake implementor coverage was claimed; only the contract count and path locations were corrected.

## Exact Microseconds Saved

- Runtime frame savings: `0 us/frame`.
- Build-time savings: `0 us`; no compile or Unity run was executed.

## R3 Verification

- `GlobalRegistryContracts.cs` direct public interface count: `51`.
- Broad first-party interface declaration hits: `215`.
- First-party asmdefs: `24`.
- Verified current MapMagic node paths exist under `Assets/_Project/Scripts/Plugins/MapMagic/`.
- Verified `../02_ACTUAL_REPORTS/INTERFACE_HEALTH_DASHBOARD.md` and `../02_ACTUAL_REPORTS/EVENT_FLOW_MAP.md` exist.
- `git diff --check -- Docs` returned exit code `0`; output was CRLF normalization warnings only.

STATUS: PENDING VERIFICATION

---

Date: 2026-05-13 R4  
Status: PENDING VERIFICATION  
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM  

## What Was Wrong

- R4 readback found live counter drift after R3: docs/source line totals changed again under concurrent workspace activity.
- The previous R4 active-doc wording still preserved stale `919/262/129/16` counters after fresh scans showed `918/283/203/10`.
- Active Archivarius system maps still carried source-path drift: stale scene runtime label, short editor debugger path, player movement naming drift, MapMagic world-path assumptions, and an absent direct `Assets/_Project/UI` folder.
- `HECTON8_GLOBAL_ARCHITECTURE_MAP.md` still had an `R2 correction` heading in an R4 override section.

## What Was Done

- Updated current authority docs with the R4 static snapshot: `918` Docs markdown files, `283` active markdown files, `203` active non-`Docs/Reports` markdown files, `80` active direct report markdown files, and `10` docs JSON files.
- Updated source counters to `1411` first-party C# files, `1365` script C# files, `1401` non-test C# files, `869871` project physical lines, `852315` script physical lines, `867132` non-test physical lines, `215` interface declaration hits, `51` direct public `GlobalRegistryContracts.cs` interfaces, and `24` first-party asmdefs.
- Patched `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, `Docs/README.md`, `Docs/Reports/README.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, and `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`.
- Confirmed current path reality for `SceneRuntimeService.cs`, `KinematicGhostDebugger.cs`, `HectonPlayerMovement.cs`, MapMagic plugin nodes, `../02_ACTUAL_REPORTS/*`, and `Assets/_Project/Scripts/UI`.

## Cinematic Cheats Used

- None in runtime. Documentation-only pass.
- The only "cheap fake" allowed here was an explicit evidence ceiling: static filesystem truth is useful, but it is not Unity runtime proof.

## Exact Microseconds Saved

- Runtime frame savings: `0 us/frame`.
- Build-time savings: `0 us`; no compile or Unity run was executed.

## R4 Verification

- Stale `SceneBootstrap.cs`, stale MapMagic world-path refs, and `HectonHectonPlayerMovement` no longer appear in active Archivarius 01 maps.
- Verified existing paths: `Assets/_Project/Scripts/Core/SceneRuntimeService.cs`, `Assets/_Project/Scripts/Editor/KinematicGhostDebugger.cs`, `Assets/_Project/Scripts/HectonPlayerMovement.cs`, all three MapMagic plugin node files, and `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/INTERFACE_HEALTH_DASHBOARD.md` / `EVENT_FLOW_MAP.md`.
- Verified `Assets/_Project/UI` is absent and `Assets/_Project/Scripts/UI` is present.
- `git diff --check -- Docs` returned exit code `0`; output was CRLF normalization warnings only.

STATUS: PENDING VERIFICATION
