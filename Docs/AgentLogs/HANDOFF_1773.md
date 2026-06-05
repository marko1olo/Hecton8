# Handoff 1773 - Scanner / Field Notes / Specimen Cards

Evidence class: STATIC_SOURCE / STATIC_DOC.

## Edited Packet IDs

Scanner/field-note English authority rows changed:

- `P292_GLASS_GRAZER_CODEX_CARD`
- `P293_LANTERN_DRIFT_CODEX_CARD`
- `P294_BRINE_VANE_CODEX_CARD`
- `P295_SENSOR_TAGGED_FAUNA_CODEX_CARD`
- `P351_DROWNED_CRUST_STRATA_GUIDE`
- `P352_BRINE_CANYON_DENSITY_LADDER_GUIDE`
- `P353_VENT_FORGE_FIELD_PROCESS_GUIDE`
- `P354_BLUE_DEBT_PRESSURE_HISTORY_GUIDE`
- `P355_PRESSURE_GLASS_AND_SEALANT_GUIDE`
- `P411_PREDATOR_SHADOW_ENCOUNTER_GRAMMAR`
- `P412_GLASS_GRAZER_CLEARING_ENCOUNTER_GRAMMAR`
- `P413_LANTERN_DRIFT_FALSE_SAFE_ENCOUNTER_GRAMMAR`
- `P414_BRINE_VANE_NAVIGATION_ENCOUNTER_GRAMMAR`
- `P415_SENSOR_TAGGED_FAUNA_PURSUIT_ENCOUNTER_GRAMMAR`
- `P426_BLUE_DEBT_CUSTODY_GRADE_RECEIPT`
- `P427_PRESSURE_GLASS_FIELD_CERTIFICATE`
- `P428_BRINE_SALT_PROCESS_LOT_CARD`
- `P429_ATLAS_LATTICE_CONTAMINATION_TAG`
- `P430_BLACK_KEEL_PAYOUT_MASS_LEDGER`

## UI / Scanner Stage Wiring Needed

- `P292` to `P295`: scanner stage cards should unlock after physical scan or observed behavior, not as default codex omniscience. `P292` and `P293` need shallow scanner popup fit and in-game wiki unlock alignment.
- `P351` to `P355`: resource/geology scanner stages should bind to material/sample/container or route objects. These are good candidates for scanner 25/50/100 percent stage escalation because they now contain handling/action hooks.
- `P411` to `P415`: fauna encounter rows need scan-stage gating by observation state. Do not reveal species certainty before evidence: trace first, behavior second, codex confirmation last.
- `P426` to `P430`: resource economy artifacts should bind to sample receipts, field certificates, lot cards, contamination tags and payout ledger UI. These are not generic loot labels.

## Localization Handoff

- `en_US` rows are updated source authority.
- All non-English rows for edited packet IDs are stale relative to changed English text.
- Required locale review groups are recorded in `Docs/Lore/AppliedContent/production_audits/1773/scanner_string_bounds.md`.

## Validation Blockers

- Source-only AppliedLore audit failed on unrelated publication page frontmatter: `Docs/Lore/AppliedContent/external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md` missing `localization_status: source_ready`.
- Changed packet JSON parse passed after comma fix.

