# P1172_PRESSURE_PORT_SALT_NEEDLE

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1172_PRESSURE_PORT_SALT_NEEDLE |
| Article ID | article.sensor_surface.pressure_port_salt_needle |
| Loc namespace | lore.article.sensor_surface.pressure_port_salt_needle |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_sensor_surface |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; RS179_FIRST_SENSOR_SURFACE_ARTICLES.md |
| Speaker | Instrument scanner, pressure-port note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first instrument inspection |
| Location / route | Shelter pressure port, wet pump box, hatch equalizer plate, or salvage gauge |
| Unlock context | Player scans a pressure port with a salt needle inside the rim |
| Evidence object | Pressure port, salt needle, rim, vent hole |
| Connected packets | P1157_DRAIN_TRAP_SALT_CAP; P1170_SENSOR_WINDOW_BIOFILM_SMEAR; P1168_WASHER_SHADOW_OFFSET |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: teaches blocked-port evidence without pressure-system claims |
| Content status | source_complete_unimported |

## Source Brief

The source knows salt crystallized inside the pressure-port rim. It does not know the actual pressure state or the instrument reading.

Player use: supports hatch, pump, and shelter maintenance believability while keeping readings evidence-bound.

Forbidden facts: no pressure value, no live sensor verdict, no hatch system claim, no cleaning interaction claim, no runtime readiness.

## Surface Texts

### Scanner

PRESSURE PORT // Salt needle inside rim. Flag reading; rim is partly blocked.

### Codex

The salt needle sits in the rim where the port needs open contact. If the port is blocked, the display can be old, delayed, or simply wrong.

The port condition comes before the reading.

### PDA Log

Port note:

- Rim: salt needle.
- Vent hole: partly shadowed.
- Faceplate: dry streaks.
- Use class: blocked reading risk.

Flag the port before trusting pressure.

### Environmental Label

PRESSURE PORT

SALT NEEDLE IN RIM

FLAG BEFORE READING

## Future Integration Notes

- Use as early hatch, pump, pressure, or salvage gauge article.
- Can support future diagnostics, scan, or maintenance UI without claiming implementation.
- Keep pressure state, hatch behavior, and clearing behavior unclaimed.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | PRESSURE PORT // Salt needle inside rim. Flag reading; rim is partly blocked. |
| ar_SA | draft_machine_or_llm | منفذ ضغط // إبرة ملح داخل الحافة. علّم القراءة؛ الحافة مسدودة جزئيا. |
| de_DE | draft_machine_or_llm | DRUCKPORT // Salznadel im Rand. Messwert markieren; Rand teilweise blockiert. |
| es_ES | draft_machine_or_llm | PUERTO DE PRESION // Aguja de sal dentro del borde. Marca la lectura; el borde esta parcialmente bloqueado. |
| fr_FR | draft_machine_or_llm | ORIFICE DE PRESSION // Aiguille de sel dans le bord. Signaler la lecture; bord partiellement bloque. |
| he_IL | draft_machine_or_llm | פתח לחץ // מחט מלח בתוך השפה. סמן את הקריאה; השפה חסומה חלקית. |
| id_ID | draft_machine_or_llm | PORT TEKANAN // Jarum garam di dalam tepi. Tandai bacaan; tepi sebagian tersumbat. |
| ja_JP | draft_machine_or_llm | 圧力ポート // 縁内に塩針。読値を注意扱い；縁が一部詰まり。 |
| ko_KR | draft_machine_or_llm | 압력 포트 // 가장자리 안에 소금 바늘. 판독값 표시; 가장자리가 일부 막혔다. |
| nl_NL | draft_machine_or_llm | DRUKPOORT // Zoutnaald in rand. Markeer uitlezing; rand deels geblokkeerd. |
| pl_PL | draft_machine_or_llm | PORT CISNIENIA // Igla soli w obreczy. Oznacz odczyt; obrecz czesciowo zablokowana. |
| pt_BR | draft_machine_or_llm | PORTA DE PRESSAO // Agulha de sal dentro da borda. Marque a leitura; borda parcialmente bloqueada. |
| ru_RU | draft_machine_or_llm | ПОРТ ДАВЛЕНИЯ // Соляная игла внутри кромки. Пометь показание; кромка частично забита. |
| uk_UA | draft_machine_or_llm | ПОРТ ТИСКУ // Соляна голка всередині крайки. Познач показник; крайка частково забита. |
| zh_CN | draft_machine_or_llm | 压力口 // 边缘内有盐针。标记读数；边缘部分堵塞。 |
