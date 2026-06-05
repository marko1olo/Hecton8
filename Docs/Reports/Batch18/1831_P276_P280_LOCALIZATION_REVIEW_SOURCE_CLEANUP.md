# 1831 P276-P280 Localization Review Source Cleanup

Date: 2026-06-04 06:13 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned visible player/public text in the native localization review packet:

- `P276_RU_NATIVE_REVIEW_LOCK`
- `P277_CJK_REVIEW_LOCK`
- `P278_RTL_REVIEW_LOCK`
- `P279_EUROPEAN_LANGUAGE_REVIEW_LOCK`
- `P280_SUBTITLE_AUDIO_REVIEW_LOCK`

Primary source:

- `Docs/Lore/AppliedContent/packets/RS056_NATIVE_LOCALIZATION_REVIEW_PACK.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for P276-P280 under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

Replaced visible `LOC HOLD`, `Review Gate`, `review gate`, `proof`, mixed-language RU, and mojibake copy with product-facing localization quality contracts:

- P276: Russian operational voice contract.
- P277: CJK font and width contract.
- P278: right-to-left reading contract.
- P279: European text expansion contract.
- P280: subtitle and audio timing contract.

Packet IDs, route cards, hashes, scene bindings, and unlock routes were not changed.

Non-English locales now use draft-native-review English fallback for these five packets. No native-final claim was made.

## Verification

Target residue scan over P276-P280 localized source fields:

```text
rs056_target_residue_hits 0
```

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5275
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

Publication frontmatter/index parity:

```text
publication_status_mismatches 0
```

AppliedLore source audit:

```powershell
python Tools\AppliedLoreRuntimeAudit.py --root . --source-only
```

Result:

```text
AppliedLore source audit OK: packets=460 locales=15 rows=6900 ... publication_frontmatter_pages=13800 publication_surface_rows=13800 publication_cluster_rows=150 ...
```

Current explicit service-residue scan found 14 remaining packets:

```text
P165, P203, P401-P405, P408, P421-P425, P435
```

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- no runtime interface fit proof for these localization contracts
- remaining explicit service-residue queue requires separate scoped cleanup
