# 1837 AppliedLore Campaign/POI/Contract Field Note Cleanup

Date: 2026-06-04 06:34 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned visible `Use for...` / `Use as...` field-note wording in campaign act, POI kit, and replay contract seed packets:

- `P381_CONTRACT_APPROACH_ACT_GATE`
- `P382_PHOTIC_SHELF_SURVIVAL_ACT`
- `P383_BRINE_CANYON_LIABILITY_ACT`
- `P384_ABYSSAL_MACHINE_FIELD_REPAIR_ACT`
- `P385_ATLAS_BASIN_PAYLOAD_ACT`
- `P386_SHALLOW_ANNEX_P63_POI_KIT`
- `P387_CABLE_REEF_RELAY_YARD_POI_KIT`
- `P388_BRINE_CANYON_PUMP_CATHEDRAL_POI_KIT`
- `P389_EVACUATION_QUEUE_TERMINAL_POI_KIT`
- `P390_ATLAS_SERVICE_BASIN_POI_KIT`
- `P391_QUIET_SALVAGE_CONTRACT_SEED`
- `P392_STORM_WINDOW_RUSH_CONTRACT_SEED`
- `P393_HIGH_CUSTODY_SAMPLE_CONTRACT_SEED`
- `P394_EVIDENCE_FIRST_CHARTER_CONTRACT_SEED`
- `P395_RECOVERY_COMPLIANCE_BAIT_CONTRACT_SEED`

Primary sources:

- `Docs/Lore/AppliedContent/packets/RS077_LONG_CAMPAIGN_ACT_SPINE.packets.json`
- `Docs/Lore/AppliedContent/packets/RS078_MAJOR_POI_EVIDENCE_KITS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS079_REPLAY_CONTRACT_SEED_FAMILIES.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for the 15 packets under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

Converted field notes from production instructions into campaign/POI/seed records:

- campaign acts now describe opening debt, bright photic shelf survival, brine liability, abyssal Atlas repair fields, and final payload decisions
- POI kits now describe evidence roles for P-63, Cable Reef Relay Yard, Brine Canyon Pump Cathedral, Evacuation Queue Terminal, and Atlas Service Basin
- replay contract seeds now describe quiet salvage, storm-window rush, high-custody samples, evidence-first charter, and recovery-compliance bait

Packet IDs, route cards, hashes, scene bindings, and unlock routes were not changed.

Non-English locales for edited packets now use draft-native-review English fallback. No native-final claim was made.

## Verification

Target scan:

```text
campaign_poi_contract_target_meta_hits 0
```

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5468
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
remaining_en_us_use_for_as_place_hits 31
```

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- 31 remaining `Use for` / `Use as` / `Place as` hits need review; several are false positives
