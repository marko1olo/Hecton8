# 1835 AppliedLore Ship/Aegir Field Note Cleanup

Date: 2026-06-04 06:29 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned visible `Use for...` field-note wording in the ship technology and Aegir moon atlas release sets:

- `P341_NEEDLEPROBE_PRECURSOR_PROGRAM`
- `P342_BEAM_SAIL_AND_PELLET_LANE`
- `P343_SEED_SHIP_BRAKING_ARCHITECTURE`
- `P344_SYSTEM_CARRIER_TUG_STACK`
- `P345_BLACK_KEEL_BATHYDROP_INTERFACE`
- `P346_AEGIR_PRIMARY_LIGHT_AND_RADIATION`
- `P347_INNER_RELAY_MOON_TRAFFIC_ROLE`
- `P348_ICE_SCATTER_MOON_HAZARD_ROLE`
- `P349_HECTON8_MID_ORBIT_TIDE_ROLE`
- `P350_OUTER_DEAD_BEACON_MOON_ROLE`

Primary sources:

- `Docs/Lore/AppliedContent/packets/RS069_SHIP_TECH_TRANSIT_ENCYCLOPEDIA.packets.json`
- `Docs/Lore/AppliedContent/packets/RS070_AEGIR_MOON_SYSTEM_ATLAS.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for the 10 packets under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

Replaced field notes that read like production instructions with product-facing/in-world records:

- probe archives and route evidence
- transit lanes and missed-window cost
- seed cargo and braking architecture
- Black Keel as custody hardware, not a personal rescue ship
- damaged bathydrop return chain
- readable Aegir sky from warm dwarf light
- moon ladder roles, relay hazards, ice-scatter pressure, HECTON-8 tide identity, and dead-beacon comm windows

This supports the locked vision: Aegir/HECTON-8 surface and system context are readable, bright, technical, and beautiful, not a dark void used to hide weak art.

Packet IDs, route cards, hashes, scene bindings, and unlock routes were not changed.

Non-English locales for edited packets now use draft-native-review English fallback. No native-final claim was made.

## Verification

Target scan:

```text
rs069_rs070_target_meta_hits 0
```

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5443
```

Targeted page generation:

```text
targeted_pages_written 300
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
remaining_en_us_use_for_as_place_hits 56
```

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- 56 remaining `Use for` / `Use as` / `Place as` hits need cluster-by-cluster review; not all are service residue
