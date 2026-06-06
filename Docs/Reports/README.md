# Reports Index

Date: 2026-06-05
Status: STATIC POLICY
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC

`Docs/Reports` stores machine-readable and command-output evidence snapshots. Reports do not override active contracts unless a current contract imports the fact.

## Use Rules

- Promote durable technical facts into `Docs/ARCHITECTURE` before treating them as doctrine.
- Keep new reports concise: problem, changed files, source constants, evidence class, unresolved gaps.
- Do not cite a report as `VERIFIED`, `COMPLETE`, or `PRODUCTION READY` without a current proof artifact.
- Do not turn this folder into a maintained knowledge base. It is evidence storage.
- Superseded chains, local telemetry, duplicate prompt extracts, and stale rerun/pass/loop reports are archive targets once no active contract cites them.

## Useful Active Families

- native-memory ownership ledgers
- build logs
- Unity API trap cleanup reports
- mesh/component cache cleanup reports
- render-feature shader lookup cleanup reports
- runtime shader reference catalog reports
- shader/mesh/CTS recheck reports
- runtime trap deeper pass reports
- runtime allocation route cleanup reports
- native ring copy cleanup reports
- late-frame registry hot-path cleanup reports
- read accessor purity cleanup reports
- signal contract cleanup reports
- signal queue diagnostics and CLI toolchain reports
- signal audit classifier and seam MPB reports
- signal contract rename rechecks
- signal safe-rename, job-carrier, executable-carrier classifier rechecks
- signal telemetry ownership classifier rechecks
- signal layout, cache-line stride, and native alias classifier rechecks
- signal residual contract cleanup reports
- core memory and SignalBus ownership deep-pass reports
- global route stability and registry hot-path reports
- global route cache and dispatcher dependency reports
- mod registry cache and DataVault binding reports
- DataVault rebind/release lifecycle reports
- SignalBus hot-path audits
- project metrics dashboards and chart bundles
- documentation structure scans

## Current 2026-06-05 Fronts

These folders are active evidence snapshots, not stable authority:

| Folder | Use |
|---|---|
| `Docs/Reports/DocumentationCompleteness_20260605/` | Documentation actuality, root-bible completeness, source-routing coverage, stable-doc patch queues, and proof-language audits. Start with `Docs/Reports/DocumentationCompleteness_20260605/README.md`. |
| `Docs/Reports/AssetSystem_20260605/` | Asset-system static inventories, source-pack maps, material/model/audio/texture risk matrices, and asset proof routing. Runtime/import/visual/audio acceptance remains separate. |
| `Docs/Reports/Batch32/` | Lore/content packet controller evidence and static source-admission ledgers. Source CSV, generated pages, h8bin, Unity, runtime, native localization, and publication claims remain proof-gated. |

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
- Superseded `Docs/_Archive/Reports_1334_2026-05-26/SignalBus1303Superseded/` payload was purged on 2026-06-06; use `Docs/Reports/SIGNALBUS_HOTPATH_AUDIT_1303_APEX_V16.md`.

Complete archived-file lists:

- `Docs/DEPRECATED/Reports_2026-05-21_SANITIZED/ARCHIVED_FILES_2026-05-21.csv`
- `Docs/DEPRECATED/Documentation_Bundles_2026-05-21_SANITIZED/ARCHIVED_BUNDLES_2026-05-21.csv`
- `Docs/DEPRECATED/Reports_2026-05-21_REVALIDATION_QUARANTINE/ARCHIVED_REPORTS_REVALIDATION_2026-05-21.csv`
- `Docs/DEPRECATED/Reports_2026-05-21_LOOP11_STALE_HANDOFF/README.md`

The obsolete payload files inside the three `Reports_2026-05-21_*` folders were purged on 2026-06-06. Only README/manifest provenance remains there; use current `Docs/Reports` artifacts for active evidence.

`DOCUMENTATION_CORPUS_INVENTORY_X_012.json` was purged on 2026-06-06 after cleanup made it a stale generated inventory with references to removed deprecated payloads.
