# 1841 AppliedLore Internal QA/Placement Leak Cleanup

Date: 2026-06-04 06:58 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned the hard exported internal/process leak cluster identified by the read-only content scan:

- P196-P220
- P446-P455

Primary sources:

- `Docs/Lore/AppliedContent/packets/RS040_NUMERIC_TUNING_SOURCE_RULES.packets.json`
- `Docs/Lore/AppliedContent/packets/RS041_DEEP_REACH_LOWER_SIGNATURES.packets.json`
- `Docs/Lore/AppliedContent/packets/RS042_COLONY_ROSTER_AUTHORING_POOL.packets.json`
- `Docs/Lore/AppliedContent/packets/RS043_WORKER_PROP_EVIDENCE_KIT.packets.json`
- `Docs/Lore/AppliedContent/packets/RS044_PUBLICATION_SPOILER_LOCALIZATION_PROTOCOL.packets.json`
- `Docs/Lore/AppliedContent/packets/RS090_UNITY_PLACEMENT_SCENE_BRIEFS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS091_NATIVE_LOCALIZATION_AND_ACCESSIBILITY_QA_BRIEFS.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for the 35 packets under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

- Rewrote visible `DataMonolith`, `runtime`, `Unity pass`, `TerminalOS capacity`, `QA brief`, `vertical slice`, `source packet`, `table handoff`, `row contract`, `tuning rule`, and similar exported process wording into stable public/in-game records.
- P196-P200 now read as resource/value/text release boundaries instead of internal numeric/localization contracts.
- P201-P220 now read as signatures, roster, prop evidence, article tiers, and text release rules without visible production phrasing.
- P446-P450 now read as evidence anchor briefs instead of Unity placement instructions.
- P451-P455 now read as native text review records instead of QA cards.
- Removed remaining broad anti-prose hits in the target cluster (`Balance Bands`, `turns`, `explains`) after the first pass.
- Preserved packet IDs, route cards, hashes, scene bindings, and unlock routes.
- Non-English locales for edited fields now use draft-native-review English fallback. No native-final claim was made.

## Verification

Target internal/process scan after cleanup:

```text
target_internal_process_hits 0
```

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5557
```

Targeted page generation:

```text
targeted_pages_written 1050
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

Export grep for harder process words no longer reports the cleaned P196-P220/P446-P455 cluster. It still exposes a separate next layer in titles/pages outside this batch:

- P184
- P301-P305
- P308
- P311-P314
- P435
- P166

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- no DataMonolith binary bake or runtime UI/site proof
- next visible wording layer remains in exported title/index/page text outside this batch
