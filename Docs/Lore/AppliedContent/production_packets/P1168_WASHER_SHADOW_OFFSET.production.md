# P1168_WASHER_SHADOW_OFFSET

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1168_WASHER_SHADOW_OFFSET |
| Article ID | article.fastener_repair.washer_shadow_offset |
| Loc namespace | lore.article.fastener_repair.washer_shadow_offset |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_fastener_repair |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; RS178_FIRST_FASTENER_REPAIR_ARTICLES.md |
| Speaker | Repair scanner, load trace note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first shelter repair |
| Location / route | Bracket foot, panel rail, shelter rack, or cargo clamp |
| Unlock context | Player scans a washer whose old dry shadow sits offset from the current washer edge |
| Evidence object | Washer, old shadow ring, bolt head, shifted paint edge |
| Connected packets | P1165_RIVET_HEAD_WHITE_RING; P1169_BRACKET_SLOT_ELONGATION; P1164_PALLET_PAD_COMPRESS_RING |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: teaches load movement evidence without physics-system claims |
| Content status | source_complete_unimported |

## Source Brief

The source knows the washer moved after original seating. It does not know the exact load, torque, or failure point.

Player use: supports early repair reading, cargo bracket believability, and cautious tightening language.

Forbidden facts: no torque number, no physics solver claim, no repair success claim, no inventory requirement, no runtime readiness.

## Surface Texts

### Scanner

WASHER SHADOW // Old ring offset from current washer. Fastener shifted under load.

### Codex

The old washer shadow is a dry ring left by pressure, paint wear, and dirt. When that ring no longer matches the current washer edge, the fastener has moved.

The part may be tight now. The offset says it was not always where it is now.

### PDA Log

Load note:

- Washer: seated.
- Old shadow: offset.
- Paint edge: dragged.
- Use class: load movement.

Inspect paired hardware before tightening.

### Environmental Label

WASHER SHADOW

OFFSET RING

CHECK PAIRED HARDWARE

## Future Integration Notes

- Use as early cargo, bracket, panel, or shelter rack article.
- Can support future repair UI or inspection hinting without claiming implementation.
- Keep torque, load magnitude, and mechanical safety unclaimed.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | WASHER SHADOW // Old ring offset from current washer. Fastener shifted under load. |
| ar_SA | draft_machine_or_llm | أثر وردة التثبيت // الحلقة القديمة منزاحة عن الوردة الحالية. المثبت تحرك تحت الحمل. |
| de_DE | draft_machine_or_llm | SCHEIBENABDRUCK // Alter Ring versetzt zur aktuellen Scheibe. Befestiger unter Last verschoben. |
| es_ES | draft_machine_or_llm | SOMBRA DE ARANDELA // Anillo viejo desplazado de la arandela actual. Fijacion movida bajo carga. |
| fr_FR | draft_machine_or_llm | TRACE DE RONDELLE // Ancien anneau decale de la rondelle actuelle. Fixation deplacee sous charge. |
| he_IL | draft_machine_or_llm | צל שייבה // טבעת ישנה מוסחת מהשייבה הנוכחית. המחבר זז תחת עומס. |
| id_ID | draft_machine_or_llm | JEJAK RING // Cincin lama bergeser dari ring saat ini. Pengikat bergeser saat menahan beban. |
| ja_JP | draft_machine_or_llm | ワッシャー跡 // 古い輪が現ワッシャーからずれている。荷重下で固定具が動いた。 |
| ko_KR | draft_machine_or_llm | 와셔 자국 // 오래된 고리가 현재 와셔와 어긋났다. 하중 아래 체결부가 밀렸다. |
| nl_NL | draft_machine_or_llm | RINGAFDRUK // Oude ring verschoven van huidige sluitring. Bevestiger onder last verschoven. |
| pl_PL | draft_machine_or_llm | SLAD PODKLADKI // Stary pierscien przesuniety wzgledem obecnej podkladki. Mocowanie przesunelo sie pod obciazeniem. |
| pt_BR | draft_machine_or_llm | MARCA DE ARRUELA // Anel antigo deslocado da arruela atual. Fixador moveu sob carga. |
| ru_RU | draft_machine_or_llm | СЛЕД ШАЙБЫ // Старое кольцо смещено от нынешней шайбы. Крепеж сдвинулся под нагрузкой. |
| uk_UA | draft_machine_or_llm | СЛІД ШАЙБИ // Старе кільце зміщене від нинішньої шайби. Кріплення зсунулося під навантаженням. |
| zh_CN | draft_machine_or_llm | 垫圈印 // 旧环偏离当前垫圈。紧固件在载荷下移位。 |
