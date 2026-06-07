# P823_BATTERY_SLAB_WAKE_TEST

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P823_BATTERY_SLAB_WAKE_TEST |
| Article ID | article.field_fabricator.battery_slab_wake_test |
| Loc namespace | lore.article.field_fabricator.battery_slab_wake_test |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_repair_fabricator |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; HECTON8_Resource_Gameplay_Catalog.md; CP01_Arrival_Shallow_Water.md |
| Speaker | Emergency crate scanner, battery wake-test note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first emergency-power decision |
| Location / route | Emergency float crate, shallow annex panel, or field fabricator wake tray |
| Unlock context | Player scans a battery slab used for a wake test |
| Evidence object | Battery slab, wake-test lead, low-load panel mark |
| Connected packets | P660_EMERGENCY_FLOAT_CRATE_INVENTORY; P625_SHALLOW_ANNEX_P63_PUMP_ROOM_ARTICLE; P820_FIELD_FABRICATOR_GASKET_QUEUE |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: makes first emergency power a measured wake test, not an open power source |
| Content status | source_complete_unimported |

## Source Brief

The source knows the battery slab can wake a low-load panel long enough to test authority or queue state. It does not imply general base power. The article should make emergency power narrow and measurable.

Player use: supports early repair sequencing: wake panel, read queue, choose repair, preserve remaining stock.

Forbidden facts: no base-wide power, no endless battery, no full fabricator unlock, no runtime readiness.

## Surface Texts

### Scanner

BATTERY SLAB // Stable for low-load wake test. Do not route to room power.

### Codex

The slab is stable enough to wake a panel, not a room. It can lift a dead terminal long enough to read authority, queue state, or fault code. Treat it as a test pulse, not a power plan.

The first light should answer a question.

### PDA Log

Wake-test note:

- Slab: stable.
- Load class: low.
- Use target: panel wake.
- Room power: rejected.

Read the queue before spending more stock.

### Environmental Label

BATTERY SLAB

LOW-LOAD WAKE TEST

PANEL ONLY

## Future Integration Notes

- Use as emergency battery article near float crate or first dead panel.
- Can support interaction copy for waking a fabricator/terminal without broad power claims.
- Future implementation should keep this as a narrow authored action unless power systems prove more.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| ar_SA | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| de_DE | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| es_ES | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| fr_FR | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| he_IL | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| id_ID | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| ja_JP | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| ko_KR | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| nl_NL | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| pl_PL | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| pt_BR | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| ru_RU | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| uk_UA | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
| zh_CN | draft_machine_or_llm | BATTERY SLAB // Stable for low-load wake test. Do not route to room power. |
