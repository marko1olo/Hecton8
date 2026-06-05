# 1842 AppliedLore Exported Wording Layer Cleanup

Date: 2026-06-04 07:02 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Cleaned the next exported wording layer found after 1841 in English wiki/site pages:

- `P166_WORKER_NAME_POOL_PROTOCOL`
- `P184_RAN_AEGIR_EPHEMERIS_TUNING_RULE`
- `P301_RESOURCE_YIELD_ROW_CONTRACT`
- `P302_STACK_LIMIT_ROW_CONTRACT`
- `P303_ESCAPE_RECIPE_COST_ROW_CONTRACT`
- `P304_CONTRACT_RISK_REWARD_ROW_CONTRACT`
- `P305_ENDING_PAYOUT_ROW_CONTRACT`
- `P308_TERMINAL_SLOT_PROOF_CARD`
- `P311_SITE_HOME_PAGE_COMPOSITION_LOCK`
- `P312_SITE_AEGIR_SYSTEM_ART_COMPOSITION_LOCK`
- `P313_SITE_DEEP_REACH_EVIDENCE_COMPOSITION_LOCK`
- `P314_SITE_ATLAS_SPOILER_COMPOSITION_LOCK`
- `P435_LOCALIZED_OVERFLOW_PRESENTATION_RULE`

Primary sources:

- `Docs/Lore/AppliedContent/packets/RS034_WORKER_NAME_JOB_EVIDENCE_TABLE.packets.json`
- `Docs/Lore/AppliedContent/packets/RS037_AEGIR_MOON_PUBLIC_ATLAS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS061_TABLE_VALUE_HANDOFF_CONTRACTS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS062_RUNTIME_UI_PROOF_BACKLOG.packets.json`
- `Docs/Lore/AppliedContent/packets/RS063_PUBLICATION_COMPOSITION_PROOF_PACK.packets.json`
- `Docs/Lore/AppliedContent/packets/RS087_PDA_CODEX_PRESENTATION_RULES.packets.json`

Derived artifacts refreshed:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- generated markdown pages for the 13 packets under `Docs/Lore/AppliedContent/in_game_wiki/*/` and `Docs/Lore/AppliedContent/external_site/*/`
- publication/index files through `Tools/AppliedLorePageExporter.py --root .`

No Unity Editor, DataMonolith binary bake, PlayMode, profiler, or site runtime proof was run.

## What Changed

- Renamed visible titles away from process labels such as `Tuning Rule`, `Row Contract`, and `Composition Lock` into data boundaries, art rules, evidence rules, and presentation records.
- Removed visible `runtime`, `baked string-pool`, `source packet`, `row contract`, `tuning rule`, and `composition lock` wording from exported page bodies.
- Preserved packet IDs, route cards, hashes, scene bindings, and unlock routes.
- Non-English locales for edited fields now use draft-native-review English fallback. No native-final claim was made.

## Verification

Target exported layer scan:

```text
target_export_layer_hits 0
```

Importer:

```powershell
python Tools\AppliedLoreImporter.py --root .
```

Result:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5561
```

Targeted page generation:

```text
targeted_pages_written 390
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

Hard process export grep:

```powershell
rg -n -i --glob '*.md' "\b(authoring rows?|runtime|Unity pass|DataMonolith|string-pool|LocID|proof card|placement (priority|brief)|composition lock|copy lock|review lock|source packet|table handoff|row contract|tuning rule|QA brief|TerminalOS capacity|vertical slice)\b" Docs\Lore\AppliedContent\external_site\en_US Docs\Lore\AppliedContent\in_game_wiki\en_US
```

Result:

```text
no matches
```

## Residual Risk

This is static source/publication cleanup only.

Remaining gates:

- no native localization proof for non-English locales
- no Unity string-pool / DataMonolith runtime binding proof
- no DataMonolith binary bake or runtime UI/site proof
- broader prose quality still needs human/native editorial review; this pass only removed clear exported process/meta wording
