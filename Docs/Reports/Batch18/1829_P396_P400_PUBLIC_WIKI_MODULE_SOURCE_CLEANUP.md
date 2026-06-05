# 1829 P396-P400 Public Wiki Module Source Cleanup

Date: 2026-06-04 06:00 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned production-residue text in public/wiki module packets:

- `P396_PUBLIC_STARTING_PREMISE_ARTICLE_MODULE`
- `P397_PUBLIC_NO_FTL_ROUTE_ARTICLE_MODULE`
- `P398_PUBLIC_AEGIR_MOON_MAP_ARTICLE_MODULE`
- `P399_PUBLIC_DEEP_REACH_LIABILITY_ARTICLE_MODULE`
- `P400_PUBLIC_ATLAS_SPOILER_GATE_ARTICLE_MODULE`

Primary source:

- `Docs/Lore/AppliedContent/packets/RS080_PUBLIC_WIKI_ARTICLE_MODULES.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for P396-P400 under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

Replaced visible publication-module/editorial wording with in-world source copy:

- P396: professional Marauder starting claim.
- P397: no-FTL route delay and physical rescue limits.
- P398: Aegir moon route map as route pressure, not decoration.
- P399: Deep Reach liability evidence with real flood physics.
- P400: Atlas access boundary without visible spoiler/editor meta wording.

Non-English locales remain draft-native-review English fallback. No native-final claim was made.

## Verification

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5200
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

Residue scan over P396-P400 localized source fields:

```text
field_residue_hits 0
```

AppliedLore source audit:

```powershell
python Tools\AppliedLoreRuntimeAudit.py --root . --source-only
```

Result:

```text
AppliedLore source audit OK: packets=460 locales=15 rows=6900 ... publication_frontmatter_pages=13800 publication_surface_rows=13800 publication_cluster_rows=150 ...
```

Residual en_US residue queue after 1828 and 1829:

```text
en_us_packets_with_residue 60
```

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- no public-site integration proof
- no runtime access-gate proof for P400

`Docs/Reports/Batch18/1820_LORE_RELEASE_QUEUE.csv` is historical and stale for P396-P400 after this cleanup.
