# 26 Legacy Docset Actuality And Update Queue

Status: PENDING VERIFICATION

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

Snapshot date: 2026-04-30.

| Surface | Count |
|---|---:|
| all non-meta files under `Docs` after this report | 529 |
| markdown files | 350 |
| text files | 61 |
| patch files | 8 |
| csv files | 1 |
| other non-meta files | 104 |

Archivarius active folder inventory:

| Folder | Current count |
|---|---:|
| `01_GENERAL_INFO` physical files | 22 |
| `01_GENERAL_INFO` markdown files | 22 |
| `02_ACTUAL_REPORTS` physical files | 55 |
| `02_ACTUAL_REPORTS` markdown files | 46 |
| `02_ACTUAL_REPORTS` patch artifacts | 8 |
| `02_ACTUAL_REPORTS` csv datasets | 1 |

Interpretation:
- `MASTER_INDEX.md` is correct for indexed docs/datasets at `69`.
- It previously under-communicated that patch artifacts physically live in `02_ACTUAL_REPORTS`.
- Patch files are evidence artifacts, not standalone current-state authority.

## 2. Active Files Updated In This Pass

Updated active anchors:

| File | Change |
|---|---|
| `Docs/README.md` | date moved to 2026-04-30; current forensic bundle added to active audit outputs |
| `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` | refreshed `1038/998` script counts, script lines, average, root-script count, and largest-owner line counts |
| `Docs/SYSTEMS_CONTRACTS.md` | added explicit `Status: PENDING VERIFICATION` metadata |
| `Docs/QUALITY_GATES.md` | split `Status: SECONDARY` from `Verification: PENDING VERIFICATION` |
| `Docs/PROCEDURAL_ASSET_PIPELINE.md` | added explicit status/verification metadata |
| `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md` | refreshed script counts and clarified patch artifacts in `02_ACTUAL_REPORTS` |
| `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/MASTER_INDEX.md` | clarified `69` indexed docs/datasets versus `75` physical files |
| `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/README.md` | added this report to bundle contents |
| `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/24_VERIFICATION_DOC_TRUST_AND_EVIDENCE_MODEL.md` | refreshed Docs count and active bundle count |
| `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/25_AUDIT_BUNDLE_FILE_ACTUALITY_RECHECK.md` | refreshed bundle count after adding this report |
| `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/28_STALE_ERROR_PURGE_AND_TRUST_SYNC.md` | added explicit stale-error purge and trust-sync addendum |

Runtime code changed:
- none

## 3. Stale But Historical Files Not Updated

These files contain old project-scale counters such as `1010` first-party C# under `Assets/_Project` or `970` under `Assets/_Project/Scripts`.
They are dated audit snapshots, so rewriting them as current truth would destroy provenance.

| File or group | Current handling |
|---|---|
| `Docs/2026-04-29_Codex_Project_Wide_Audit/*` | keep as historical 2026-04-29 snapshot |
| `Docs/2026-04-29_Codex_Codebase_Reality_Audit/*` | keep as historical 2026-04-29 snapshot |
| `Docs/2026-04-29_CODEX_MANDATE_AUDIT/2026-04-29_CODEX_MANDATE_COMPLIANCE_AUDIT.md` | keep as historical mandate scan |
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_DEEP_FORENSIC_AUDIT.md` | keep as historical; superseded by later rechecks for counts |
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_SUPREME_AUDITOR_REPORT.md` | keep as historical; superseded by later rechecks for counts |
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION.md` | keep as dated recheck; superseded by `2026-04-30` layers for counts |
| `Docs/Legacy_Backlog/Что_и_как_исправляем_—_живой_план.md` | legacy reference; stale embedded counts should not be used as current evidence |

Current scale truth:

| Counter | Current value |
|---|---:|
| first-party C# under `Assets/_Project` | 1038 |
| C# under `Assets/_Project/Scripts` | 998 |
| script lines under `Assets/_Project/Scripts` | 444135 |
| scripts directly in `Assets/_Project/Scripts` root | 314 |

## 4. Status Metadata Findings

Active anchors before this pass had uneven status metadata.

Fixed now:
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/QUALITY_GATES.md`

Still requires later policy decision:
- Some `ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS` files use statuses like `ETA SANITIZED`, `ETA LEAK_MAPPED`, `ETA SURGERY_PREPPED`, `POPULATED`, or historical check marks.
- These are not uniformly wrong, but they are not equivalent to runtime verification.
- Recommended next cleanup: normalize status vocabulary across active reports to `PENDING VERIFICATION`, `HISTORICAL`, `ARTIFACT`, `REFERENCE`, or `ARCHIVE`.

Additional long-lived bundle scan:

| Area | Finding |
|---|---|
| `Docs/ARCHITECTURE` | 16 markdown files lack explicit `Status:` metadata |
| `Docs/Scatter_Runtime` | `SCATTER_REFACTORING_MANIFESTO_V2.md` lacks explicit status metadata |
| `Docs/Flora_Pipeline` | `FLORA_TEXTURE_IMPORT_LOG.md` lacks `PENDING VERIFICATION`; `FLORA_NEXT_DIALOG_PROMPT.md` contains pending language but no `Status:` key |
| `Docs/AI_Fauna` | two reference files lack explicit status metadata |
| `Docs/Legacy_World_Reference` | `TERRAIN_108_BIOMES_VISION.md` lacks explicit status metadata |
| `Docs/Legacy_Backlog` | two large legacy backlog files lack explicit status metadata |

Do not blindly patch all of these.
Some are reference/spec documents, not reports.
They need a separate metadata-normalization pass that preserves intended authority: `ACTIVE`, `REFERENCE`, `HISTORICAL`, `LEGACY`, or `PENDING VERIFICATION`.

## 5. Redirect Stub Findings

Root `Docs` contains redirect stubs for moved flora/scatter files.

Observed stubs:
- `AI_FLORA_EXECUTION_BRIEF.md`
- `ECS_DOTS_ADOPTION_PLAN.md`
- `FLORA_NEXT_DIALOG_PROMPT.md`
- `FLORA_SYSTEM_PLAN.md`
- `FLORA_TEXTURE_IMPORT_LOG.md`
- `SCATTER_DOTS_NARROW_SCOPE_SPEC.md`
- `SCATTER_PHASE1_BASELINE_CHECKLIST.md`
- `SCATTER_REFACTOR_EXECUTION_PLAN.md`
- `ПРАВИЛЬНОЕ ПРЕДЛОЖЕНИЕ.txt`

Verdict:
- Keep temporarily if external links still target them.
- They are compatibility mirrors, not authority.
- If no external link surface exists, archive or delete them in a dedicated cleanup pass.

## 6. Root And Legacy Noise

Observed:
- root repository markdown still contains large active anchors: `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md`, `AGENTS.md`, plus smaller root notes.
- `Docs/ROOT_DOCS_REFERENCE.md` contains mojibake path names inherited from earlier relocation.
- `Docs/какие то логи` contains 90 files and is high-noise unless a specific log is cited.
- `Docs/_Archive` is large and should remain historical unless a path is explicitly pulled forward.

Decision:
- Do not rewrite archives.
- Do not treat prompt dumps, old logs, patch artifacts, or external idea bundles as implementation truth.
- Use active anchors plus current source scans first.

## 7. Next Update Queue

P0 documentation updates:
- Normalize active report status vocabulary.
- Add a top-level "latest truth order" note to `Docs/README.md` once the forensic bundle stabilizes.
- Decide whether `Docs/ROOT_DOCS_REFERENCE.md` should be cleaned for mojibake paths or left as historical relocation evidence.
- Normalize missing status metadata in long-lived bundles without flattening all specs into the same authority class.

P1 documentation updates:
- Add per-folder `README.md` authority notes to high-noise idea folders.
- Mark old dated Codex audit folders as historical if they remain outside `_Archive`.
- Add a small `Docs/Reports` index entry for current forensic bundle or move future one-shot audit outputs under `Docs/Reports`.

P2 documentation updates:
- Decide whether redirect stubs can be removed.
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
