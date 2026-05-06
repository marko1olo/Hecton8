# 26 Legacy Docset Actuality And Update Queue

Date: 2026-05-07
Status: PENDING VERIFICATION

## 2026-05-04 Supersession Note

This file is a May 1/May 2 docset actuality queue. Current global source/doc counts are superseded by `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`: `1118` first-party `.cs` files under `Assets/_Project`, `1078` under `Assets/_Project/Scripts`, `519952` static script lines, `325` scripts directly under `Assets/_Project/Scripts`, active `Docs/**/*.md` count `188`, and active/root markdown surface `214`.

Mandates followed:
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

Scope:
- Full machine inventory of non-meta files under `Docs`.
- Manual read of active documentation anchors and Archivarius index files.
- Targeted stale-counter scans for known project-scale counters.
- No Unity play-mode, profiler, Frame Debugger, or build run.

## 1. Current Doc Inventory

Historical snapshot date: 2026-05-01.

| Surface | Count |
|---|---:|
| all non-meta files under `Docs` after this report | 584 |
| markdown files | 375 |
| text files | 64 |
| patch files | 13 |
| csv files | 1 |
| diff files | 13 |
| log files | 92 |
| other non-meta files | 1 |

Archivarius active folder inventory:

| Folder | Current count |
|---|---:|
| `01_GENERAL_INFO` physical files | 24 |
| `01_GENERAL_INFO` markdown files | 24 |
| `02_ACTUAL_REPORTS` physical files | 56 |
| `02_ACTUAL_REPORTS` markdown files | 46 |
| `02_ACTUAL_REPORTS` patch artifacts | 9 |
| `02_ACTUAL_REPORTS` csv datasets | 1 |

Interpretation:
- `MASTER_INDEX.md` is correct for indexed docs/datasets at `71`.
- It previously under-communicated that patch artifacts physically live in `02_ACTUAL_REPORTS`.
- Patch files are evidence artifacts, not standalone current-state authority.

## 2. Active Files Updated In This Pass

Updated active anchors:

| File | Change |
|---|---|
| `Docs/README.md` | date moved to 2026-04-30; current forensic bundle added to active audit outputs |
| `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` | refreshed script counts, script lines, average, root-script count, and interface delta |
| `Docs/SYSTEMS_CONTRACTS.md` | added explicit `Status: PENDING VERIFICATION` metadata |
| `Docs/QUALITY_GATES.md` | split `Status: SECONDARY` from `Verification: PENDING VERIFICATION` |
| `Docs/PROCEDURAL_ASSET_PIPELINE.md` | added explicit status/verification metadata |
| `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md` | refreshed script counts, added May 1 deltas, and clarified patch artifacts in `02_ACTUAL_REPORTS` |
| `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/MASTER_INDEX.md` | clarified `71` indexed docs/datasets versus `80` physical files |
| `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/README.md` | added this report to bundle contents |
| `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/24_VERIFICATION_DOC_TRUST_AND_EVIDENCE_MODEL.md` | refreshed Docs count and active bundle count |
| `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/25_AUDIT_BUNDLE_FILE_ACTUALITY_RECHECK.md` | refreshed bundle count after adding this report |
| `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/28_STALE_ERROR_PURGE_AND_TRUST_SYNC.md` | added explicit stale-error purge and trust-sync addendum |
| `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` | added May 2 full markdown read-pass, source-count sync, link-integrity update, and build-evidence boundary |
| `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md` | refreshed May 2 source and doc counts |
| `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md` | refreshed May 2 source count and largest-file table |

Runtime code changed:
- none

## 3. Stale But Historical Files Not Updated

These files contain old project-scale counters such as `1010` first-party C# under `Assets/_Project` or `970` under `Assets/_Project/Scripts`.
They are dated audit snapshots, so rewriting them as current truth would destroy provenance.

| File or group | Current handling |
|---|---|
| `Docs/DEPRECATED/2026-04-29_Audit_Bundles/2026-04-29_Codex_Autonomous_Audit/*` | moved to deprecated as historical 2026-04-29 snapshot |
| `Docs/DEPRECATED/2026-04-29_Audit_Bundles/2026-04-29_Codex_Project_Wide_Audit/*` | moved to deprecated as historical 2026-04-29 snapshot |
| `Docs/DEPRECATED/2026-04-29_Audit_Bundles/2026-04-29_Codex_Codebase_Reality_Audit/*` | moved to deprecated as historical 2026-04-29 snapshot |
| `Docs/DEPRECATED/2026-04-29_Audit_Bundles/2026-04-29_CODEX_MANDATE_AUDIT/2026-04-29_CODEX_MANDATE_COMPLIANCE_AUDIT.md` | moved to deprecated as historical mandate scan |
| `Docs/DEPRECATED/External_And_Log_Bundles/*` | moved to deprecated as external prompt/log/source-material bundles; not current-state authority |
| `Docs/DEPRECATED/Root_Redirect_Stubs_2026-05-01/*` | moved to deprecated as old flat redirect stubs; canonical Flora/Scatter bundle paths remain active |
| `Docs/DEPRECATED/Encoding_Damaged_2026-05-01/*` | moved to deprecated because active-surface text was encoding-damaged and not reliable current documentation |
| `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/*` | moved to deprecated as old repository-root docs and scan artifacts |
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_DEEP_FORENSIC_AUDIT.md` | keep as historical; superseded by later rechecks for counts |
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_SUPREME_AUDITOR_REPORT.md` | keep as historical; superseded by later rechecks for counts |
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION.md` | keep as dated recheck; superseded by `2026-04-30` layers for counts |
| `Docs/Legacy_Backlog/Ð§Ñ‚Ð¾_Ð¸_ÐºÐ°Ðº_Ð¸ÑÐ¿Ñ€Ð°Ð²Ð»ÑÐµÐ¼_â€”_Ð¶Ð¸Ð²Ð¾Ð¹_Ð¿Ð»Ð°Ð½.md` | legacy reference; stale embedded counts should not be used as current evidence |

Historical May 2 scale truth:

| Counter | Historical May 2 value |
|---|---:|
| first-party C# under `Assets/_Project` | 1087 |
| C# under `Assets/_Project/Scripts` | 1047 |
| script lines under `Assets/_Project/Scripts` | 571562 |
| scripts directly in `Assets/_Project/Scripts` root | 317 |

Current May 4 scale truth is in the supersession note above.

## 4. Status Metadata Findings

Active anchors before this pass had uneven status metadata.

Fixed now:
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/QUALITY_GATES.md`
- `Docs/ARCHITECTURE/*.md` reference files that lacked explicit `Status:`
- `Docs/AI_Fauna/*.md` reference files that lacked explicit `Status:`
- `Docs/Flora_Pipeline/FLORA_NEXT_DIALOG_PROMPT.md`
- `Docs/Flora_Pipeline/FLORA_TEXTURE_IMPORT_LOG.md`
- `Docs/Scatter_Runtime/SCATTER_REFACTORING_MANIFESTO_V2.md`
- `Docs/Legacy_World_Reference/TERRAIN_108_BIOMES_VISION.md`
- `Docs/Legacy_Backlog/*.md` legacy reference files that lacked explicit `Status:`

Still requires later policy decision:
- Some `ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS` files use statuses like `ETA SANITIZED`, `ETA LEAK_MAPPED`, `ETA SURGERY_PREPPED`, `POPULATED`, or historical check marks.
- These are not uniformly wrong, but they are not equivalent to runtime verification.
- Recommended next cleanup: normalize status vocabulary across active reports to `PENDING VERIFICATION`, `HISTORICAL`, `ARTIFACT`, `REFERENCE`, or `ARCHIVE`.

Additional long-lived bundle scan from the earlier pass:

| Area | Finding |
|---|---|
| `Docs/ARCHITECTURE` | fixed: missing long-lived reference statuses added |
| `Docs/Scatter_Runtime` | fixed: `SCATTER_REFACTORING_MANIFESTO_V2.md` marked `REFERENCE` |
| `Docs/Flora_Pipeline` | fixed: prompt/log files marked with explicit status and pending verification |
| `Docs/AI_Fauna` | fixed: two reference files marked `REFERENCE` |
| `Docs/Legacy_World_Reference` | fixed: `TERRAIN_108_BIOMES_VISION.md` marked `LEGACY REFERENCE` |
| `Docs/Legacy_Backlog` | fixed: two large legacy backlog files marked `LEGACY REFERENCE` |

Current active-bundle metadata scan:
- root `Docs/*.md`: no missing `Status:` rows found
- `Docs/ARCHITECTURE/*.md`: no missing `Status:` rows found
- `Docs/AI_Fauna`, `Docs/Flora_Pipeline`, `Docs/Scatter_Runtime`, `Docs/Legacy_World_Reference`, `Docs/Legacy_Backlog`: no missing `Status:` rows found

## 5. Redirect Stub Findings

Root `Docs` previously contained redirect stubs for moved flora/scatter files.
They were moved to `Docs/DEPRECATED/Root_Redirect_Stubs_2026-05-01/`.

Moved stubs:
- `AI_FLORA_EXECUTION_BRIEF.md`
- `ECS_DOTS_ADOPTION_PLAN.md`
- `FLORA_NEXT_DIALOG_PROMPT.md`
- `FLORA_SYSTEM_PLAN.md`
- `FLORA_TEXTURE_IMPORT_LOG.md`
- `SCATTER_DOTS_NARROW_SCOPE_SPEC.md`
- `SCATTER_PHASE1_BASELINE_CHECKLIST.md`
- `SCATTER_REFACTOR_EXECUTION_PLAN.md`
- Cyrillic `.txt` redirect stub for the scatter manifesto

Verdict:
- They are compatibility mirrors, not authority.
- They no longer live in active root `Docs`.
- Current canonical bundle entry points remain `Docs/Flora_Pipeline/README.md` and `Docs/Scatter_Runtime/README.md`.

## 6. Root And Legacy Noise

Observed:
- root repository markdown still contains large active anchors: `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md`, `AGENTS.md`, plus smaller root notes and scan artifacts.
- `Docs/ROOT_DOCS_REFERENCE.md` was cleaned on 2026-05-01 and now lists current root text files without mojibake relocation rows.
- the deprecated external/log bundle contains 90 raw log files and is high-noise unless a specific log is cited.
- `Docs/_Archive` is large and should remain historical unless a path is explicitly pulled forward.

Decision:
- Do not rewrite archives.
- Do not treat prompt dumps, old logs, patch artifacts, or external idea bundles as implementation truth.
- Use active anchors plus current source scans first.

## 7. Next Update Queue

P0 documentation updates:
- Normalize active report status vocabulary.
- Add a top-level "latest truth order" note to `Docs/README.md` once the forensic bundle stabilizes.
- Decide whether remaining non-anchor root text docs should stay in root, move to `Docs/Reports`, or move to `Docs/DEPRECATED`.
- Keep active-bundle status metadata synced when new docs are added.

P1 documentation updates:
- Add per-folder `README.md` authority notes to high-noise idea folders.
- Mark old dated Codex audit folders as historical if they remain outside `_Archive`.
- Add a small `Docs/Reports` index entry for current forensic bundle or move future one-shot audit outputs under `Docs/Reports`.

P2 documentation updates:
- Decide whether the deprecated redirect-stub bundle can be deleted after any external link surface is checked.
- Generate a machine-readable doc authority manifest if this audit style continues.

## 8. Regression Model

CPU:
- No runtime code changed.

GC:
- No runtime code changed.

Memory:
- No runtime code changed.

Cadence:
- Documentation cadence improved because active anchors now carry current counters and status metadata.

Correctness:
- Current-source counters are refreshed.
- Historical snapshots remain preserved instead of being rewritten into false current-state documents.

## 9. Hard Conclusion

The old documentation set is not uniformly current.

Current active anchors are now less stale after this pass, but the broader doc corpus remains mixed:
- active source-backed anchors
- dated audit snapshots
- patch artifacts
- archive material
- prompt/idea dumps
- legacy relocation stubs

Rule for future reads:
- Trust `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/README.md` and current `ARCHIVARIUS REPORTS/01_GENERAL_INFO` anchors first.
- Treat 2026-04-28 and 2026-04-29 audit folders as historical unless a later file explicitly refreshes them.
- Treat every performance or readiness claim as `PENDING VERIFICATION` unless it cites profiler/build/play-mode evidence.
