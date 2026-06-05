# Scanner String Bounds - 1773

Evidence class: STATIC_SOURCE / STATIC_DOC.

Scope: changed `en_US` scanner authority rows only. Non-English rows already exist in packet JSON but were not rewritten in this pass; they are stale relative to the changed English authority text and require native/UI review before synchronized bake.

## Locale Status For Changed Units

- `en_US`: `source_authority_updated`.
- `ru_RU`, `ja_JP`, `zh_CN`, `fr_FR`, `es_ES`, `de_DE`, `pl_PL`, `uk_UA`, `ar_SA`, `id_ID`, `ko_KR`, `he_IL`, `pt_BR`, `nl_NL`: `stale_pending_native_review_after_en_US_change`.
- RTL review required: `ar_SA`, `he_IL`.
- CJK wrap/font review required: `ja_JP`, `zh_CN`, `ko_KR`.
- Expansion review required: `de_DE`, `ru_RU`, `pl_PL`, `uk_UA`, `fr_FR`, `es_ES`, `pt_BR`, `nl_NL`.

## Changed Scanner Bounds

| Packet ID | Article ID | en_US chars | Expansion risk | Review note |
|---|---:|---:|---|---|
| P292_GLASS_GRAZER_CODEX_CARD | applied_lore.p292_glass_grazer_codex_card | 71 | Low | Short, action-bearing; all non-English rows stale. |
| P293_LANTERN_DRIFT_CODEX_CARD | applied_lore.p293_lantern_drift_codex_card | 81 | Low | Short, action-bearing; all non-English rows stale. |
| P294_BRINE_VANE_CODEX_CARD | applied_lore.p294_brine_vane_codex_card | 94 | Medium | Contains compound `false-floor`; DE/NL/PL/RTL review needed. |
| P295_SENSOR_TAGGED_FAUNA_CODEX_CARD | applied_lore.p295_sensor_tagged_fauna_codex_card | 71 | Low | Short, but Atlas terminology needs proper-noun consistency. |
| P351_DROWNED_CRUST_STRATA_GUIDE | applied_lore.p351_drowned_crust_strata_guide | 120 | Medium | Longest changed row; DE/RU/RTL/CJK short-form review required. |
| P352_BRINE_CANYON_DENSITY_LADDER_GUIDE | applied_lore.p352_brine_canyon_density_ladder_guide | 98 | Medium | Multiple hazard nouns; UI width review required. |
| P353_VENT_FORGE_FIELD_PROCESS_GUIDE | applied_lore.p353_vent_forge_field_process_guide | 101 | Medium | Technical process terms; CJK and DE wrap review required. |
| P354_BLUE_DEBT_PRESSURE_HISTORY_GUIDE | applied_lore.p354_blue_debt_pressure_history_guide | 99 | Medium | Proper noun `blue debt`; preserve sample/custody meaning. |
| P355_PRESSURE_GLASS_AND_SEALANT_GUIDE | applied_lore.p355_pressure_glass_and_sealant_guide | 101 | Medium | `Atlas` and `seal map` require consistent loc glossary. |
| P411_PREDATOR_SHADOW_ENCOUNTER_GRAMMAR | applied_lore.predator_shadow_encounter_grammar | 98 | Medium | Good action signal; review `light discipline` idiom. |
| P412_GLASS_GRAZER_CLEARING_ENCOUNTER_GRAMMAR | applied_lore.glass_grazer_clearing_encounter_grammar | 108 | Medium | Long but clear; expansion languages likely need shorter native forms. |
| P413_LANTERN_DRIFT_FALSE_SAFE_ENCOUNTER_GRAMMAR | applied_lore.lantern_drift_false_safe_encounter_grammar | 106 | Medium | Multi-meaning hazard; CJK/RTL line-break review required. |
| P414_BRINE_VANE_NAVIGATION_ENCOUNTER_GRAMMAR | applied_lore.brine_vane_navigation_encounter_grammar | 78 | Low | Short, route/action-bearing. |
| P415_SENSOR_TAGGED_FAUNA_PURSUIT_ENCOUNTER_GRAMMAR | applied_lore.sensor_tagged_fauna_pursuit_encounter_grammar | 97 | Medium | `repair-network echo` needs native review. |
| P426_BLUE_DEBT_CUSTODY_GRADE_RECEIPT | applied_lore.blue_debt_custody_grade_receipt | 108 | Medium | Custody/economy terms need legal/resource glossary consistency. |
| P427_PRESSURE_GLASS_FIELD_CERTIFICATE | applied_lore.pressure_glass_field_certificate | 106 | Medium | `escape proof` may expand; UI review required. |
| P428_BRINE_SALT_PROCESS_LOT_CARD | applied_lore.brine_salt_process_lot_card | 98 | Medium | Hazard/action clear; corrosion wording must fit scanner popup. |
| P429_ATLAS_LATTICE_CONTAMINATION_TAG | applied_lore.atlas_lattice_contamination_tag | 94 | Medium | `Atlas-lattice` must stay stable across locales. |
| P430_BLACK_KEEL_PAYOUT_MASS_LEDGER | applied_lore.black_keel_payout_mass_ledger | 98 | Medium | Legal/economy terms need compact native forms. |

## UI Risk

- No changed `en_US` scanner row exceeds 120 characters.
- Expected expansion can still break compact scanner popups in German, Russian, Polish, Ukrainian, Arabic, Hebrew, French, Spanish, Portuguese and Dutch.
- CJK locales need dedicated short forms rather than automatic truncation.
- RTL locales must preserve Latin IDs/proper nouns without manual reversal.

