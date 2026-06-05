# 1840 AppliedLore Public/Protocol Meta-Language Cleanup

Date: 2026-06-04 06:52 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned visible product/authoring/protocol wording from 32 AppliedLore packets where exported public/wiki text was speaking about HECTON-8 as a product, controller task, authoring surface, website/wiki rule, placement pass, or development note instead of as believable records/articles.

Primary packets:

- P068, P092, P099, P108, P135, P164, P172, P180, P207, P216, P220, P251
- P261-P265, P281, P301-P305, P315-P320, P367, P425, P436

Primary sources:

- `Docs/Lore/AppliedContent/packets/RS014_COLONY_RETURN_WINDOWS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS019_HECTON8_PHYSICAL_ATLAS_DEPTH_BANDS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS020_ATLAS_ENDING_AGENCY_DOSSIER.packets.json`
- `Docs/Lore/AppliedContent/packets/RS022_DEEP_REACH_SIGNOFF_CHAIN.packets.json`
- `Docs/Lore/AppliedContent/packets/RS027_FALSE_EXIT_RETURN_PRESSURE.packets.json`
- `Docs/Lore/AppliedContent/packets/RS033_DOMAIN_EPHEMERIS_ROUTE_TABLE.packets.json`
- `Docs/Lore/AppliedContent/packets/RS035_RESOURCE_RECIPE_PRESSURE_RULES.packets.json`
- `Docs/Lore/AppliedContent/packets/RS036_DOSSIER_SAVE_PRESENTATION_RULES.packets.json`
- `Docs/Lore/AppliedContent/packets/RS042_COLONY_ROSTER_AUTHORING_POOL.packets.json`
- `Docs/Lore/AppliedContent/packets/RS044_PUBLICATION_SPOILER_LOCALIZATION_PROTOCOL.packets.json`
- `Docs/Lore/AppliedContent/packets/RS051_PUBLIC_SITE_PILLAR_ARTICLES.packets.json`
- `Docs/Lore/AppliedContent/packets/RS053_NUMERIC_AUTHORING_BRIDGE_SURFACES.packets.json`
- `Docs/Lore/AppliedContent/packets/RS057_PUBLIC_SITE_READY_ARTICLE_SECTIONS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS061_TABLE_VALUE_HANDOFF_CONTRACTS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS063_PUBLICATION_COMPOSITION_PROOF_PACK.packets.json`
- `Docs/Lore/AppliedContent/packets/RS064_UNITY_PLACEMENT_PRIORITY_BACKLOG.packets.json`
- `Docs/Lore/AppliedContent/packets/RS074_PLAYER_EX_DEEP_REACH_PROFESSIONAL_DOSSIER.packets.json`
- `Docs/Lore/AppliedContent/packets/RS085_CELESTIAL_EPHEMERIS_PUBLIC_BANDS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS088_AUDIO_TRANSCRIPT_ARTICLE_SEEDS.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for the 32 packets under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

- Replaced `gives HECTON-8...`, `website/wiki`, `publication-ready`, `authoring rows`, `placement priority`, `copy lock`, `handoff`, and similar visible service wording with in-world records, public article rules, table contracts, evidence placement rules, and data-boundary rules.
- Kept table/contract intent where it is part of the content model, but made the text read as a release artifact instead of an instruction to agents or writers.
- Preserved packet IDs, route cards, hashes, scene bindings, and unlock routes.
- Non-English locales for edited fields now use draft-native-review English fallback. No native-final claim was made.

## Verification

Target public/product meta scan:

```text
target_public_meta_hits 0
```

Target protocol/meta scan:

```text
target_protocol_meta_hits 1
```

The remaining target hit is an intentional in-world false positive:

```text
P436_BLACK_KEEL_APPROACH_TRANSCRIPT_SEED scanner: Recovered carrier audio confirms paid descent, conditional return, four-second lag and required proof packet.
```

Broad authoring/meta scan:

```text
broad_meta_hit_packets 1
```

The single broad hit is the same P436 `proof packet` false positive.

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5528
```

Targeted page generation:

```text
targeted_pages_written 960
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

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- no DataMonolith binary bake or runtime UI/site proof
- Dewey's read-only scan still identified harder internal QA/placement brief leaks in P196-P220 and P446-P455 that need the next cleanup batch
