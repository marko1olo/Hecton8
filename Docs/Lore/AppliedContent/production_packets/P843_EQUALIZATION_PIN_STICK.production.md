# P843_EQUALIZATION_PIN_STICK

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P843_EQUALIZATION_PIN_STICK |
| Article ID | article.pressure_access.equalization_pin_stick |
| Loc namespace | lore.article.pressure_access.equalization_pin_stick |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_pressure_access |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; HECTON8_Field_Atlas.md; CP01_Arrival_Shallow_Water.md; CP03_Drowned_Colony_Barnard_Hook.md |
| Speaker | Pressure access scanner, equalization check note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1/2 access handoff |
| Location / route | Early pressure door or hatch equalization port |
| Unlock context | Player scans a stuck equalization pin beside a pressure door |
| Evidence object | Equalization pin, salt collar, pressure witness mark, service notch |
| Connected packets | P840_PRESSURE_DOOR_MANUAL_WHEEL; P809_PRESSURE_DRIP_COUNTER; P821_COLD_SEALANT_CARTRIDGE_WEIGHT |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: gives pressure access a safe sequence before opening |
| Content status | source_complete_unimported |

## Source Brief

The source knows the equalization pin is stuck under a salt collar and the service notch remains reachable. It does not claim a full pressure model. The article should tell the player that pressure sequence matters before door force.

Player use: supports scanner hints and safe access ordering.

Forbidden facts: no instant depressurization, no safe passage proof, no full pressure simulation claim, no runtime readiness.

## Surface Texts

### Scanner

EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn.

### Codex

The pin is small and important. Salt built a collar around it, freezing the equalization path while the service notch stayed exposed. If the wheel turns before the pin releases, the door will argue with pressure and metal at the same time.

Small hardware decides big doors.

### PDA Log

Equalization check:

- Pin: stuck.
- Salt collar: present.
- Service notch: reachable.
- Wheel action: hold.

Release pin before manual wheel force.

### Environmental Label

EQUALIZATION PIN

SERVICE NOTCH REACHABLE

RELEASE BEFORE WHEEL

## Future Integration Notes

- Use beside pressure door wheel or hatch access.
- Can support authored state-machine sequence without implementing a pressure solver.
- Keep pressure information physical and local.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| ar_SA | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| de_DE | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| es_ES | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| fr_FR | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| he_IL | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| id_ID | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| ja_JP | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| ko_KR | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| nl_NL | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| pl_PL | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| pt_BR | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| ru_RU | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| uk_UA | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
| zh_CN | draft_machine_or_llm | EQUALIZATION PIN // Salt collar stuck. Service notch reachable; release before wheel turn. |
