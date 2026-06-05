# 1828 P457-P460 Public Longform Source Cleanup

Date: 2026-06-04 05:58 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned production-residue text in the RS092 public longform packets:

- `P457_AEGIR_HARD_SCIFI_LONGFORM_BRIEF`
- `P458_DEEP_REACH_LIABILITY_LONGFORM_BRIEF`
- `P459_ATLAS_SPOILER_LONGFORM_BRIEF`
- `P460_BLUE_DEBT_RESOURCE_LONGFORM_BRIEF`

Primary source:

- `Docs/Lore/AppliedContent/packets/RS092_PUBLIC_SITE_LONGFORM_ARTICLE_BRIEFS.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for P457-P460 under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

Replaced visible editorial/task wording with in-world article, wiki, scanner, terminal, audio, and field-note copy.

Removed visible residue from P457-P460 localized field values:

- `Public brief`
- `Longform spine`
- `should explain`
- `article module`
- `proof card`
- `QA brief`
- generic `brief`, `spine`, `article`, `copy`, `set dressing`, and `spoiler` wording inside player/public field values

The immutable packet IDs still contain `_LONGFORM_BRIEF` / `_SPOILER_`; IDs were preserved.

Non-English locales were not claimed as native-final. Each non-English field now uses a draft-native-review prefix plus the cleaned English fallback text.

## Verification

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5195
```

Targeted page generation:

```text
targeted_pages_written 120
targeted_pages_written 45
```

The second pass polished P457/P459 visible copy after the first rewrite.

Exporter/index refresh:

```powershell
python Tools\AppliedLorePageExporter.py --root .
```

Result:

```text
applied_lore_pages_written=0 skipped_existing=13800 index_pages_written=30
```

Residue scan over P457-P460 localized source fields:

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

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- no public-site integration proof
- no spoiler-gate runtime proof for P459

`Docs/Reports/Batch18/1820_LORE_RELEASE_QUEUE.csv` is a historical triage artifact and is now stale for P457-P460. Use this 1828 report plus current source/audit results for those four packets.

The AppliedLore working tree contains broad pre-existing dirty generated-page changes from earlier source/exporter work. This report claims only the P457-P460 source cleanup and the verification commands above.
