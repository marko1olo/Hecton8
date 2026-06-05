# 1839 AppliedLore Remaining Meta-Language Cleanup

Date: 2026-06-04 06:44 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned the remaining real `Use for...` / `Use as...` production-language hits from visible AppliedLore packet text:

- `P031_PHOTIC_SHELF_LIFE`
- `P209_ANCHOR_WORKER_NAME_SET_B`
- `P300_NO_CLEAN_ENDING_DOSSIER_NOTE`
- `P301_RESOURCE_YIELD_ROW_CONTRACT`
- `P314_SITE_ATLAS_SPOILER_COMPOSITION_LOCK`
- `P315_SOCIAL_DEV_NOTE_COPY_LOCK`
- `P319_FINAL_DESCENT_PLACEMENT_PRIORITY_LOCK`
- `P330_BLACKOUT_WINDOW_SIGNAL_DECAY`
- `P334_SENSOR_TAGGED_FAUNA_FEEDBACK_LOOP`
- `P364_ASCENT_ENERGY_CHARGE_ASSEMBLY`
- `P365_QUARANTINE_LEGAL_HANDSHAKE_ASSEMBLY`
- `P366_FIELD_SYSTEMS_SPECIALIST_RECORD`
- `P369_DEBT_BLACKLIST_CONTRACT_PRESSURE`
- `P375_CLAIM_CONTINUITY_LOSS_CONVERSION_PROOF`

Primary sources:

- `Docs/Lore/AppliedContent/packets/RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE.packets.json`
- `Docs/Lore/AppliedContent/packets/RS042_COLONY_ROSTER_AUTHORING_POOL.packets.json`
- `Docs/Lore/AppliedContent/packets/RS060_FINAL_DESCENT_ROUTE_FRAGMENTS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS061_TABLE_VALUE_HANDOFF_CONTRACTS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS063_PUBLICATION_COMPOSITION_PROOF_PACK.packets.json`
- `Docs/Lore/AppliedContent/packets/RS064_UNITY_PLACEMENT_PRIORITY_BACKLOG.packets.json`
- `Docs/Lore/AppliedContent/packets/RS066_DEEP_REACH_PRESENT_COMMS_CHAIN.packets.json`
- `Docs/Lore/AppliedContent/packets/RS067_ATLAS_REPAIR_NETWORK_MECHANICS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS073_ESCAPE_ASCENT_ENGINEERING_COMPONENTS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS074_PLAYER_EX_DEEP_REACH_PROFESSIONAL_DOSSIER.packets.json`
- `Docs/Lore/AppliedContent/packets/RS075_DEEP_REACH_LIE_PHYSICAL_PROOF_CHAIN.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for the 14 packets under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

Converted visible service instructions into product-facing records:

- shallow shelf terminal copy now records a baseline sample instead of telling authors how to use it
- worker roster B now reads as an in-world deeper roster record; adjacent scanner/audio/site copy was also de-metafied
- ending, resource table, publication composition, placement, comms, fauna, escape assembly, player dossier, and proof-chain field notes now describe the actual record or route consequence

Packet IDs, route cards, hashes, scene bindings, and unlock routes were not changed.

Non-English locales for edited fields now use draft-native-review English fallback. No native-final claim was made.

## Verification

Target scan across all locales in edited packets:

```text
target_remaining_meta_hits 0
```

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5502
```

Targeted page generation:

```text
targeted_pages_written 420
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
remaining_en_us_use_for_as_place_hits 2
```

The two remaining broad hits were reviewed and left intact as normal English false positives:

- `P124_NORMAL_CITIZEN_AEGIR_MEMORY`: `Most people know the place as a line under insurance rates.`
- `P231_CONDUCTIVE_BIOFILM_CABLE_SKIN`: `Atlas can abuse as a repair surface.`

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- no DataMonolith binary bake or runtime UI/site proof
