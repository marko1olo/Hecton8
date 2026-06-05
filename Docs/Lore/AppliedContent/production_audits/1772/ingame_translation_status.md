# In-Game Wiki Translation Status 1772

Evidence class: STATIC_SOURCE.

Changed English source packets:

| packet_id | article_id | unlock_id | changed units |
|---|---|---|---|
| `P046_PUMP_ROOM_HANDSHAKE` | `hecton8.pump_room_handshake` | `unlock.first_pump_room_handshake` | scanner, terminal, field_note, in_game_wiki |
| `P049_SONAR_RETURN_ROUTE` | `hecton8.sonar_return_route` | `unlock.first_sonar_return_route` | scanner, terminal, field_note, in_game_wiki |
| `P060_FIRST_HOUR_SPINE` | `hecton8.first_hour_spine` | `unlock.first_first_hour_spine` | scanner, terminal, field_note, in_game_wiki |
| `P061_MAINTENANCE_ECOLOGY` | `hecton8.maintenance_ecology` | `unlock.first_maintenance_ecology` | scanner, terminal, field_note, in_game_wiki |
| `P221_PHOTIC_MAT_BASELINE` | `ecology.photic_mat_baseline` | `unlock.photic_mat_baseline` | scanner, field_note, in_game_wiki |
| `P291_PHOTIC_MAT_CODEX_CARD` | `applied_lore.p291_photic_mat_codex_card` | `unlock.p291_photic_mat_codex_card` | terminal, field_note, in_game_wiki |

Stable translation units for every changed packet:

| unit | source field | readiness |
|---|---|---|
| title | `localized.<locale>.title` | unchanged in this pass |
| short summary / body | `localized.<locale>.in_game_wiki` | en_US updated; other locales stale |
| scanner bridge | `localized.<locale>.scanner` | en_US updated where listed above; other locales stale |
| terminal bridge | `localized.<locale>.terminal` | en_US updated where listed above; other locales stale |
| audio bark | `localized.<locale>.audio` | unchanged in this pass |
| field note | `localized.<locale>.field_note` | en_US updated; other locales stale |
| related links | none verified in packet schema or page body | no link invented |
| unlock note | `unlock.primary` and page frontmatter `unlock_id` | unchanged |
| external/public copy | `localized.<locale>.external_site` | unchanged; outside in-game wiki edit scope |

Locale readiness after the 1772 English source edits:

| locale | direction | selected packet rows | exported page rows | readiness |
|---|---:|---:|---:|---|
| en_US | ltr | 6/6 present | 6/6 present | `source_authority_updated` |
| ru_RU | ltr | 6/6 present | 6/6 present | `stale_against_updated_en_US; native_review_required` |
| ja_JP | ltr | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; native_review_required` |
| zh_CN | ltr | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; native_review_required` |
| fr_FR | ltr | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; native_review_required` |
| es_ES | ltr | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; native_review_required` |
| de_DE | ltr | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; native_review_required` |
| pl_PL | ltr | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; native_review_required` |
| uk_UA | ltr | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; native_review_required` |
| ar_SA | rtl | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; rtl_native_review_required` |
| id_ID | ltr | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; native_review_required` |
| ko_KR | ltr | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; native_review_required` |
| he_IL | rtl | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; rtl_native_review_required` |
| pt_BR | ltr | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; native_review_required` |
| nl_NL | ltr | 6/6 present | 6/6 present | `draft_translation_stale_against_updated_en_US; native_review_required` |

LocID / packet consistency:

- Existing packets use `packet_id`, `article_id`, `title_key`, `unlock.primary`, `poi_tags`, and `biome_tags`.
- This pass did not invent a new runtime LocID format.
- This pass did not change packet IDs, article IDs, title keys, unlock gates, POI tags, biome tags, surface names, locale names, or direction values.
- `title_key` remains in the existing `applied_lore...` style where present. It is not normalized here because the localization model forbids inventing a runtime key format during content polish.

Unlock and spoiler state:

| packet_id | unlock state | spoiler decision |
|---|---|---|
| `P046_PUMP_ROOM_HANDSHAKE` | after first pump-room handshake | teaches pump tradeoffs only; no late machinery reveal |
| `P049_SONAR_RETURN_ROUTE` | after first sonar return-route unlock | teaches stale return ping behavior only; no late route spoiler |
| `P060_FIRST_HOUR_SPINE` | after first-hour spine unlock | names only evidence already observed in first hour |
| `P061_MAINTENANCE_ECOLOGY` | after first maintenance-ecology unlock | keeps Atlas/life relationship operational, not mystical or final |
| `P221_PHOTIC_MAT_BASELINE` | after photic mat baseline scan | keeps shallow brightness useful and hazardous, not bleak |
| `P291_PHOTIC_MAT_CODEX_CARD` | after photic mat specimen scan | teaches sample handling; no deep ecology spoiler |

## Additional Pass - 2026-06-04

Changed English source rows:

- `P017_AEGIR_MOON_LADDER`: `localized.en_US.in_game_wiki`, `scanner`, `terminal`, `audio`, and `field_note` updated in `RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY`.
- `P018_HECTON8_DROWNED_GEOLOGY`: `localized.en_US.in_game_wiki`, `scanner`, `terminal`, `audio`, and `field_note` updated in `RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY`.
- `P020_HECTON8_ECOLOGY_REGISTRY`: `localized.en_US.in_game_wiki`, `scanner`, `terminal`, `audio`, and `field_note` updated in `RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY`.
- `P031_PHOTIC_SHELF_LIFE`: `localized.en_US.in_game_wiki`, `scanner`, `terminal`, `audio`, and `field_note` updated in `RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE`.
- `P032_PRESSURE_LADDER_DEPTH_BANDS`: `localized.en_US.in_game_wiki`, `scanner`, `terminal`, `audio`, and `field_note` updated in `RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE`.
- `P033_CABLE_REEF_SYMBIOSIS`: `localized.en_US.in_game_wiki`, `scanner`, `terminal`, `audio`, and `field_note` updated in `RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE`.

Locale readiness:

- `en_US`: source authority updated for all six additional entries; page mirrors updated.
- `ru_RU`, `ja_JP`, `zh_CN`, `fr_FR`, `es_ES`, `de_DE`, `pl_PL`, `uk_UA`, `id_ID`, `ko_KR`, `pt_BR`, `nl_NL`: packet rows and page rows remain present but are stale against the updated en_US source; native review required.
- `ar_SA`, `he_IL`: packet rows and page rows remain present but are stale against the updated en_US source; RTL native review required.

Known localization debt:

- Non-English `P017_AEGIR_MOON_LADDER` rows still contain old moon names from the obsolete ladder and must be refreshed before native-final publication.
- Non-English `P018`, `P020`, `P031`, `P032`, and `P033` rows may still preserve writer-facing framing from the prior English source.
- No locale key, direction, packet ID, article ID, title key, or unlock gate was changed in this pass.
