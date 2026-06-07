# P824_REPAIR_AUTHORITY_STAMP

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P824_REPAIR_AUTHORITY_STAMP |
| Article ID | article.field_fabricator.repair_authority_stamp |
| Loc namespace | lore.article.field_fabricator.repair_authority_stamp |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_repair_fabricator |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; HECTON8_Resource_Gameplay_Catalog.md; CP01_Arrival_Shallow_Water.md; CP03_Drowned_Colony_Barnard_Hook.md |
| Speaker | Field fabricator terminal, authority stamp scanner |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1/2 repair authority handoff |
| Location / route | P-63 field fabricator, drowned colony repair locker, or pump-spine service panel |
| Unlock context | Player scans a repair authority stamp tying output to local route state |
| Evidence object | Authority stamp, local route code, accepted repair class |
| Connected packets | P820_FIELD_FABRICATOR_GASKET_QUEUE; P821_COLD_SEALANT_CARTRIDGE_WEIGHT; P822_CONTACT_CUTTER_SPOOL_LIMIT |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: explains why early fabrication is route-owned and repair-scoped |
| Content status | source_complete_unimported |

## Source Brief

The source knows local repair authority permits narrow output after a route condition is proven. It does not define global permissions or future economy. The article should make authority an object the player can read, not invisible game logic.

Player use: supports future UI/tooltips that explain why some outputs are allowed and others are locked.

Forbidden facts: no global unlock, no external admin approval, no broad economy claim, no runtime readiness.

## Surface Texts

### Scanner

AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof.

### Codex

The stamp is not decoration. It tells the fabricator what kind of work this room can prove: gasket, clamp, contact, wake test. If the route cannot prove a repair need, the machine does not owe the player a broader queue.

Authority is local until the room earns more.

### PDA Log

Authority stamp read:

- Class: local repair.
- Accepted output: narrow.
- Route proof: required.
- Global authority: absent.

Use the room to prove the job.

### Environmental Label

REPAIR AUTHORITY

LOCAL CLASS

ROUTE PROOF REQUIRED

## Future Integration Notes

- Use as a scanner/terminal explanation for route-scoped fabrication.
- Can connect content, UI, and crafting constraints without changing runtime systems yet.
- If imported later, bind this to source-owned crafting/fabricator contracts, not ad hoc string gates.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| ar_SA | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| de_DE | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| es_ES | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| fr_FR | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| he_IL | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| id_ID | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| ja_JP | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| ko_KR | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| nl_NL | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| pl_PL | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| pt_BR | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| ru_RU | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| uk_UA | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
| zh_CN | draft_machine_or_llm | AUTHORITY STAMP // Local repair class accepted. Output remains tied to route proof. |
