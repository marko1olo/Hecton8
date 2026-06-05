# 1832 P401-P405 and P421-P425 LOC HOLD Source Cleanup

Date: 2026-06-04 06:16 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Normalized visible draft localization markers for:

- `P401_MARA_VENN_PUMP_CHIEF_DOSSIER`
- `P402_JUNO_KADE_RELAY_NOTARY_DOSSIER`
- `P403_REN_OKOYE_BRINE_CARTOGRAPHER_DOSSIER`
- `P404_SAHANA_IQBAL_TRIAGE_MECHANIC_DOSSIER`
- `P405_LIAN_TORRES_VENT_FORGE_OPERATOR_DOSSIER`
- `P421_RAN_AEGIR_PUBLIC_DISTANCE_BAND`
- `P422_AEGIR_LOCAL_WINDOW_BAND_TABLE`
- `P423_HECTON8_MOON_LADDER_PUBLIC_BAND`
- `P424_BLACK_KEEL_TRANSFER_ORBIT_BAND`
- `P425_PUBLIC_EPHEMERIS_TABLE_HANDOFF_RULE`

Primary sources:

- `Docs/Lore/AppliedContent/packets/RS081_COLONY_ANCHOR_WORKER_DOSSIERS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS085_CELESTIAL_EPHEMERIS_PUBLIC_BANDS.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for the 10 packets under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

Removed visible `XX LOC HOLD:` prefixes from non-English localized fields. For the target packets, non-English locales now use standard draft-native-review English fallback:

```text
Draft XX localization pending native pass.
```

Existing `en_US` source copy was preserved. Packet IDs, route cards, hashes, scene bindings, and unlock routes were not changed.

## Verification

Target residue scan:

```text
target_loc_hold_residue_hits 0
```

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5415
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

Current explicit service-residue scan found 4 remaining packets:

```text
P165_DEEP_REACH_SUBOFFICE_REGISTRY
P203_QUARANTINE_REVIEW_GATE_SIGNATURES
P408_HALDANE_QUARANTINE_RELEASE_HOLD_ARTIFACT
P435_LOCALIZED_OVERFLOW_PRESENTATION_RULE
```

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- 4 remaining explicit `review gate` wording packets need scoped review
