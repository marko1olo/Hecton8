# 1811 P456 Public Source Repair

## Verdict

COMPLETE for static P456 public-source repair.

No Unity, PlayMode, profiler, or DataMonolith bake was run. No native-final translation or runtime proof is claimed.

## Source Owner

Authoritative source:

- `Docs/Lore/AppliedContent/packets/RS092_PUBLIC_SITE_LONGFORM_ARTICLE_BRIEFS.packets.json`

Generated mirrors updated from that source:

- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- `Docs/Lore/AppliedContent/external_site/*/P456_SITE_HOME_LONGFORM_BRIEF.md`
- `Docs/Lore/AppliedContent/in_game_wiki/*/P456_SITE_HOME_LONGFORM_BRIEF.md`
- `Docs/Lore/AppliedContent/Publication_Surface_Index.csv`
- `Docs/Lore/AppliedContent/Localization_Status_Index.md`
- P456 lines in localized `INDEX.md` pages

Identity preserved:

- packet_id: `P456_SITE_HOME_LONGFORM_BRIEF`
- release_set_id: `RS092_PUBLIC_SITE_LONGFORM_ARTICLE_BRIEFS`
- article_id: `applied_lore.site_home_longform_brief`
- unlock_id: `unlock.site_home_longform_brief`
- surface_mask: `127`
- CSV header unchanged

## Before / After

Before P456 carried production-instruction copy:

- title: `Site Home Longform Brief`
- scanner: `Public brief: the home article opens with contract...`
- terminal: `SITE HOME: show the player verb...`
- external_site: `Longform spine: HECTON-8 is a hard-sci-fi salvage descent...`
- field_note: `Assemble for website: first viewport = pressure machinery...`

After P456 is public/player-facing:

- title: `HECTON-8: Pressure Claim`
- scanner: `Marauder intake record. Initial route links Black Keel, a damaged bathydrop, and P-63 pressure repairs. Public accident record is incomplete.`
- terminal: `PUBLIC ARCHIVE NODE // H8 CLAIM SUMMARY. Contract window: Aegir relay open. Required actions: repair pressure seals, scan the shelf, recover cargo, return with evidence.`
- external_site opens with: `HECTON-8 begins as a salvage job, not a rescue story.`
- field_note: `Marauder note: daylight on the shelf does not make it safe. Count air, fix seals before chasing cargo, and distrust any record that cannot match the dents.`

## Locale Status

| locale | CSV flags | publication status | title |
|---|---:|---|---|
| en_US | 0 | source_ready | HECTON-8: Pressure Claim |
| ru_RU | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| ja_JP | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| zh_CN | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| fr_FR | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| es_ES | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| de_DE | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| pl_PL | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| uk_UA | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| ar_SA | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| id_ID | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| ko_KR | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| he_IL | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| pt_BR | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |
| nl_NL | 1 | draft_native_pass_pending | HECTON-8: Pressure Claim |

Non-English rows intentionally use clean English fallback text behind `Draft XX localization pending native pass.` source prefixes. Import/export strips the prefix from visible copy and preserves `flags=1`; this is not native localization.

## P456 Proof

Static route commands:

```text
python Tools\AppliedLoreImporter.py --root .
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5191
```

P456 page render:

```text
p456_pages_written 30 indexes_refreshed 30
```

Targeted verification:

```text
JSON_IDENTITY P456_SITE_HOME_LONGFORM_BRIEF RS092_PUBLIC_SITE_LONGFORM_ARTICLE_BRIEFS applied_lore.site_home_longform_brief unlock.site_home_longform_brief
CSV_ROWS 15
PAGES_CHECKED 30 bad_pages none
PUBLICATION_INDEX_ROWS 30
```

Scans over P456 JSON, P456 CSV rows, and 30 P456 generated pages found:

- banned P456 residue terms: none
- mojibake markers checked: none
- `en_US` status: `source_ready`, flags `0`
- non-English status: `draft_native_pass_pending`, flags `1`

## Non-P456 Residue Candidates

All-source AppliedLore packet scan found P456 clean and listed these non-P456 candidates for later scoped work. No bulk edits were made:

- `P010_PAYLOAD_WINDOW`: `TODO`
- `P167_PRESSURE_JOB_TITLE_TABLE`: `should explain`
- `P196_RESOURCE_TABLE_PLACEHOLDER_CONTRACT`: `placeholder`
- `P217_IN_GAME_WIKI_UNLOCK_TIER_RULES`: `should explain`
- `P252_AEGIR_SYSTEM_PUBLIC_PRIMER_ARTICLE`: `should explain`
- `P311_SITE_HOME_PAGE_COMPOSITION_LOCK`: `SITE HOME`
- `P396_PUBLIC_STARTING_PREMISE_ARTICLE_MODULE`: `Use for website`
- `P445_ENDING_PAYOUT_VALUE_DRAFT_ROWS`: `should explain`
- `P457_AEGIR_HARD_SCIFI_LONGFORM_BRIEF`: `Longform spine`, `Public brief`, article-instruction terms
- `P458_DEEP_REACH_LIABILITY_LONGFORM_BRIEF`: `Longform spine`, `Public brief`, article-instruction terms
- `P459_ATLAS_SPOILER_LONGFORM_BRIEF`: `Longform spine`, `Public brief`
- `P460_BLUE_DEBT_RESOURCE_LONGFORM_BRIEF`: `Longform spine`, `Public brief`, article-instruction terms

## Audit Result

Static source-only audit was run:

```text
python Tools\AppliedLoreRuntimeAudit.py --root . --source-only
AppliedLore audit FAILED: Publication page C:\hades\Hecton8\Docs\Lore\AppliedContent\in_game_wiki\ru_RU\P151_BLACK_KEEL_CONTRACT_APPROACH.md missing frontmatter line: localization_status: draft_native_pass_pending
```

This is outside P456. The user explicitly said P151/exporter drift is serialized after this task, so 1811 did not repair P151 or launch 1812.

## Re-Export Notes

Current safe static refresh route:

```text
python Tools\AppliedLoreImporter.py --root .
```

Full page overwrite is not recommended until P151/exporter drift is owned:

```text
python Tools\AppliedLorePageExporter.py --root . --overwrite
```

1811 used P456-only page rendering from the exporter functions to avoid broad page overwrite.
