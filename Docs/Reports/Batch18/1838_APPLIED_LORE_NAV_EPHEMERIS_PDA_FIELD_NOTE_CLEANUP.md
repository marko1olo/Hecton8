# 1838 AppliedLore Navigation/Ephemeris/PDA Field Note Cleanup

Date: 2026-06-04 06:40 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned visible `Use for...` / `Use as...` field-note wording in colony worker, navigation cluster, ephemeris, and PDA presentation packets:

- `P401_MARA_VENN_PUMP_CHIEF_DOSSIER`
- `P403_REN_OKOYE_BRINE_CARTOGRAPHER_DOSSIER`
- `P417_NAV_SYSTEM_SHIP_CLUSTER`
- `P419_NAV_RESOURCE_ECOLOGY_CLUSTER`
- `P420_NAV_ENDING_CLUSTER`
- `P421_PUBLIC_AEGIR_DISTANCE_BAND`
- `P422_HECTON8_WINDOW_BAND`
- `P423_MOON_LADDER_PUBLIC_SCALE`
- `P424_BLACK_KEEL_ORBIT_BAND`
- `P425_CELESTIAL_ROUTE_TABLE_HANDOFF`
- `P431_PDA_EVIDENCE_TIER_LABEL_LOCK`
- `P432_SCANNER_STAGE_COPY_LOCK`
- `P433_TERMINAL_ENTRY_VOICE_LOCK`
- `P434_DOSSIER_PAGE_LAYOUT_LOCK`
- `P435_LOCALIZATION_LAYOUT_LOCK`

Primary sources:

- `Docs/Lore/AppliedContent/packets/RS081_COLONY_ANCHOR_WORKER_DOSSIERS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS084_SITE_WIKI_NAVIGATION_CLUSTERS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS085_CELESTIAL_EPHEMERIS_PUBLIC_BANDS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS087_PDA_CODEX_PRESENTATION_RULES.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for the 15 packets under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

Converted field notes from production instructions into readable records:

- worker dossiers now describe evidence clusters and brine approach records instead of internal placement purpose
- navigation clusters now describe how system, ship, resource, ecology, and ending pages are grouped
- ephemeris public bands now explain Aegir scale, transfer windows, moon ladder, Black Keel receive/retrieval gap, and table-owned exact constants
- PDA presentation rules now read as interface records for evidence tiers, scanner copy, terminal voice, dossier layout, and localization layout

Packet IDs, route cards, hashes, scene bindings, and unlock routes were not changed.

Non-English locales for edited packets now use draft-native-review English fallback. No native-final claim was made.

## Verification

Target scan:

```text
target_nav_ephemeris_pda_meta_hits 0
```

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5475
```

Targeted page generation:

```text
targeted_pages_written 450
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

Remaining broad scan:

```text
remaining_en_us_use_for_as_place_hits 16
```

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- 16 remaining `Use for` / `Use as` / `Place as` hits need review; two are likely ordinary English false positives
