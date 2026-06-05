# 1834 AppliedLore Meta Wording Cleanup Batch A

Date: 2026-06-04 06:25 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned visible `en_US` meta wording from 17 AppliedLore packets where player/site/wiki surfaces still referenced writers, site/wiki mechanics, mission text, or site articles directly.

Target packets:

- `P104_RAN_B_H8_PUBLIC_CATALOG`
- `P141_RAN_AEGIR_DISTANCE_MODEL`
- `P146_DEEP_REACH_PUBLIC_COMBINE`
- `P148_ATLAS_CONTINUITY_OFFICE`
- `P158_LOCKER_NAME_PROTOCOL`
- `P162_DOMAIN_POPULATION_AUTHORITY_SCALE`
- `P163_PUBLIC_ROUTE_NAMES`
- `P164_TRANSIT_DURATION_BANDS`
- `P166_WORKER_NAME_POOL_PROTOCOL`
- `P167_PRESSURE_JOB_TITLE_TABLE`
- `P171_RECIPE_TIER_PRESSURE_BANDS`
- `P173_BLUE_DEBT_SAMPLE_QUALITY`
- `P174_VENT_FORGE_PROCESS_STEPS`
- `P201_CONTRACT_CONTINUITY_DESK_SIGNATURES`
- `P308_TERMINAL_SLOT_PROOF_CARD`
- `P402_JUNO_KADE_RELAY_NOTARY_DOSSIER`
- `P416_SITE_WIKI_START_HERE_CLUSTER`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for the 17 packets under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

Replaced visible authoring phrases such as:

- `gives writers`
- `site/wiki`
- `site articles`
- `website articles`
- `mission text`

with product-facing/in-world copy that preserves canon meaning without exposing production instructions to the player or public pages.

Packet IDs, route cards, hashes, scene bindings, and unlock routes were not changed.

For edited packets, non-English locales were regenerated as draft-native-review English fallback. No native-final claim was made.

## Verification

Target scan:

```text
target_meta_writer_hits 0
```

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5433
```

Targeted page generation:

```text
targeted_pages_written 510
```

Exporter/index refresh:

```powershell
python Tools\AppliedLorePageExporter.py --root .
```

Result:

```text
applied_lore_pages_written=0 skipped_existing=13800 index_pages_written=30
```

Remaining scan for the same marker class:

```text
remaining_en_us_writer_site_meta_hits 0
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

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- broader `Use for` / `Use as` / `Place as` field-note cleanup remains open
