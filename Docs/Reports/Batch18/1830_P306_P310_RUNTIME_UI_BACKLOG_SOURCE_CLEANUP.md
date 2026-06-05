# 1830 P306-P310 Runtime UI Backlog Source Cleanup

Date: 2026-06-04 06:08 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned visible player/public text in runtime UI backlog packets:

- `P306_PDA_CODEX_STATE_PROOF_CARD`
- `P307_SCANNER_STAGE_BINDING_PROOF_CARD`
- `P308_TERMINAL_SLOT_PROOF_CARD`
- `P309_DOSSIER_ENDING_RECORD_PROOF_CARD`
- `P310_LOCALIZED_OVERFLOW_PROOF_CARD`

Primary source:

- `Docs/Lore/AppliedContent/packets/RS062_RUNTIME_UI_PROOF_BACKLOG.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for P306-P310 under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

Replaced visible `Proof Card`, `UI PROOF`, `LOC PROOF`, and `runtime implementation still needs...` copy with player-facing/in-world records:

- P306: PDA evidence state and discovery-gated codex behavior.
- P307: scanner stage binding through measured contact and observed behavior.
- P308: terminal slot chain constrained to local operational records.
- P309: dossier ending memory without carrying gear/world truth between campaigns.
- P310: localization fit record for interface, fonts, RTL/CJK, overflow, and subtitle timing.

Non-English locales now use draft-native-review English fallback for these five packets. This intentionally removes the previous mixed/garbled RU strings and avoids native-final claims.

## Verification

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5205
```

Targeted page generation:

```text
targeted_pages_written 150
```

Exporter/index refresh:

```powershell
python Tools\AppliedLorePageExporter.py --root .
```

Result:

```text
applied_lore_pages_written=0 skipped_existing=13800 index_pages_written=30
```

Residue scan over P306-P310 localized source fields:

```text
target_field_residue_hits 0
```

AppliedLore source audit:

```powershell
python Tools\AppliedLoreRuntimeAudit.py --root . --source-only
```

Result:

```text
AppliedLore source audit OK: packets=460 locales=15 rows=6900 ... publication_frontmatter_pages=13800 publication_surface_rows=13800 publication_cluster_rows=150 ...
```

Current explicit en_US residue marker scan after this cleanup found one remaining packet:

```text
P278_RTL_REVIEW_LOCK proof gate
```

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- no runtime PDA/scanner/terminal/dossier interface proof
- `Docs/Reports/Batch18/1820_LORE_RELEASE_QUEUE.csv` is historical and stale for groups cleaned by 1828, 1829, and 1830
