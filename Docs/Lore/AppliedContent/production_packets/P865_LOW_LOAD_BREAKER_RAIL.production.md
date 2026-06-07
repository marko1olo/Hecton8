# P865_LOW_LOAD_BREAKER_RAIL

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P865_LOW_LOAD_BREAKER_RAIL |
| Article ID | article.first_shelter_power.low_load_breaker_rail |
| Loc namespace | lore.article.first_shelter_power.low_load_breaker_rail |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_first_shelter_power |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; HECTON8_Resource_Gameplay_Catalog.md; CP01_Arrival_Shallow_Water.md |
| Speaker | Shelter power scanner, breaker inspection note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first shelter power check |
| Location / route | First shelter panel, P-63 annex, or repair bench power rail |
| Unlock context | Player scans a low-load breaker rail after panel wake |
| Evidence object | Breaker rail, low-load label, salt-clean contact, disabled high-load branch |
| Connected packets | P823_BATTERY_SLAB_WAKE_TEST; P864_SHELTER_STATUS_TALLY_BOARD; P869_PANEL_LABEL_HALF_POWER |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: makes first shelter power a low-load local check, not a broad unlock |
| Content status | source_complete_unimported |

## Source Brief

The source knows the breaker rail can accept low-load routing and the high-load branch is disabled. It does not claim the room has full power. The article should make local power confidence inspectable and limited.

Player use: supports scanner hints for safe panel wake and early support systems.

Forbidden facts: no base-wide power, no full circuit restoration, no unlimited battery transfer, no runtime readiness.

## Surface Texts

### Scanner

BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only.

### Codex

The rail is honest about its limits. Low-load contacts cleaned up enough for scrubber test, panel read, or dim light. The high-load branch is still disabled. Pushing room power through it would turn a useful rail into evidence of impatience.

Power starts local.

### PDA Log

Breaker inspection:

- Low-load contact: clean.
- High-load branch: disabled.
- Shelter support: allowed.
- Room power: rejected.

Route only what the rail can carry.

### Environmental Label

BREAKER RAIL

LOW-LOAD ONLY

HIGH LOAD DISABLED

## Future Integration Notes

- Use near first shelter panel or repair bench breaker rail.
- Can support future UI copy for limited power without claiming a runtime power graph.
- Keep all claims local until power systems have proof.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| ar_SA | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| de_DE | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| es_ES | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| fr_FR | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| he_IL | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| id_ID | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| ja_JP | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| ko_KR | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| nl_NL | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| pl_PL | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| pt_BR | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| ru_RU | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| uk_UA | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
| zh_CN | draft_machine_or_llm | BREAKER RAIL // Low-load branch clean. High-load branch disabled; shelter support only. |
