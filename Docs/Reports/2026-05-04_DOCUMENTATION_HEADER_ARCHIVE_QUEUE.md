# Documentation Header And Archive Queue

Date: `2026-05-04`
Status: `PENDING VERIFICATION`
Scope: active markdown header compliance, relocated root evidence logs, archive/deprecated move candidates

## Mandates Followed

- `.agents-skills/PROJECT_LTS_Compatibility_Layer.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## Purpose

This file is a cleanup queue, not runtime proof.
It records which documentation surfaces are still structurally dirty after the current authority-map pass.

Read first:

1. `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`
2. `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`
3. this file

## Inventory Snapshot

Current scan after the root-log relocation addendum:

| Surface | Count |
|---|---:|
| `Docs/**/*.md`, total | `406` |
| active `Docs/**/*.md`, excluding archive/deprecated/obsolete | `194` |
| `Docs/Reports/*.md` | `40` |
| root `.md` files | `4` |
| root `.txt` / `.log` files | `0` |
| relocated former root `.log` files in deprecated bundle | `7` |

Header scan rule:

- inspected first `30` lines of active markdown files
- ignored `Docs/_Archive`, `Docs/DEPRECATED`, `Docs/Reports/DEPRECATED`, and `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE`
- checked for literal `Date:` and `Status:` lines

Result:

| Finding | Count |
|---|---:|
| active markdown files missing `Date:` or `Status:` | `123` |
| active markdown files missing `Date:` | `122` |
| active markdown files missing `Status:` | `41` |

## Missing Header Breakdown

| Area | Files needing header normalization |
|---|---:|
| `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/` | `28` |
| `Docs/AI_Fauna/` | `2` |
| `Docs/ARCHITECTURE/` | `23` |
| `Docs/ARCHIVARIUS REPORTS/` | `37` |
| `Docs/Flora_Pipeline/` | `4` |
| `Docs/Legacy_Backlog/` | `2` |
| `Docs/Legacy_World_Reference/` | `1` |
| `Docs/Reports/` | `18` |
| `Docs/Scatter_Runtime/` | `4` |
| root active `Docs/*.md` contract files | `4` |

Root active `Docs/*.md` contract files needing a `Date:` line:

- `Docs/PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
- `Docs/QUALITY_GATES.md`
- `Docs/SYSTEMS_CONTRACTS.md`

These are active anchors. Normalize them before low-value historical reports.

## Relocated Root Evidence Logs

Repository root currently contains `0` `.log` files after the follow-up relocation.
Moved repository-root logs now live in:

- `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-04/README.md`

Moved files:

| File | Handling |
|---|---|
| `codex_playmode_launcher.log` | moved; raw evidence only; not documentation authority |
| `rebuild_full.log` | moved; raw evidence only; not documentation authority |
| `unity-batch-autonomous-foundation.log` | moved; raw evidence only; not documentation authority |
| `unity-batch-autonomous-registry-sweep-final.log` | moved; raw evidence only; not documentation authority |
| `unity-batch-autonomous-registry-sweep-rerun.log` | moved; raw evidence only; not documentation authority |
| `unity-batch-final.log` | moved; raw evidence only; not documentation authority |
| `unity-batch-smoke.log` | moved; raw evidence only; not documentation authority |

Do not cite these logs directly as current evidence. Create or use a dated report that states exact command, date, exit code, and summary.

## Archive Candidates

Do not move these during a dirty-tree documentation pass. Queue them for a separate move-only pass.

| Candidate | Reason | Proposed handling |
|---|---|---|
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/` April 28/29 audits | old evidence outputs, many missing headers, not first-read authority | move stale files to `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/` or `Docs/DEPRECATED/Archivarius_Actual_Reports_2026-05-04/`, except files explicitly linked by current indexes |
| `Docs/Reports/NAVGRID_LEAK_PURGE_SURGERY_LOG.md` | older narrow surgery report | keep only if cited by active report index; otherwise move to `Docs/Reports/DEPRECATED/` |
| `Docs/Reports/OMEGA_PURGE_SURGERY_LOG.md` | older narrow surgery report | same as above |
| `Docs/Reports/GC_SINGLETON_KILL_LIST.md` | useful debt ledger, not first-read authority | keep active only while current indexes cite it as a secondary ledger |
| `Docs/Reports/FOUNDATION_HARDENING_SURGERY_LOG_2026-05-01.md` | superseded by May 3/May 4 foundation reports | candidate for `Docs/Reports/DEPRECATED/` after link recheck |
| root `.log` files | completed in follow-up root-log relocation | no remaining root-log move item unless new root logs appear |
| `.codex-artifacts/**/*.log` and `CodexArtifacts/**/*.log` | raw evidence artifacts outside active docs | leave as artifact surface unless a later evidence-bundle cleanup explicitly moves them |

Keep `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/` intact for now.
It is historical, but it has current supersession notes and is still a coherent audit bundle.

## Header Normalization Order

1. Add `Date:` to active root `Docs/*.md` contract files.
2. Add `Date:` / `Status:` to current `Docs/ARCHITECTURE/` reference files that lack one.
3. Normalize current `Docs/Reports/*.md` files still missing headers.
4. Normalize active bundle READMEs before inner bundle files.
5. Normalize `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/` files that are still linked by current indexes.
6. Do not mass-edit deprecated/raw logs.

## What Was Changed In This Pass

- Created this queue report.
- Updated active indexes to link this report as the current header/archive cleanup queue.
- Follow-up relocation moved the seven tracked repository-root logs to `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-04/`.
- Did not move old reports because the worktree is already dirty and includes unrelated source, asset, artifact, report, and deprecated raw-log changes.

## Verification

Searches now return no active hits for stale May 2 current-state boundary headers, the old two-report read-first sentence, or stale current guard-fail wording when this queue report itself is excluded from literal-pattern searches.

Root text scan now reports `4` root `.md` files and `0` root `.txt` / `.log` files. The deprecated root-log bundle contains the `7` former repository-root logs plus its local README.

Scoped `git diff --check` excluding deprecated files passed.
Full `Docs` diff-check still reports pre-existing trailing whitespace in `Docs/DEPRECATED/External_And_Log_Bundles/2026-04-20_Deepseek_Ideas_Reality_Audit/логи.txt`; this pass did not modify or normalize that raw log.

## Do Not Claim

- Do not claim documentation is fully normalized. `123` active markdown files still need header cleanup.
- Do not claim archived/deprecated raw logs are clean.
- Do not claim relocated root logs are current proof.
- Do not claim Play Mode, GC, profiler, memory retention, player build, or scene/prefab proof from this documentation queue.

STATUS: PENDING VERIFICATION
