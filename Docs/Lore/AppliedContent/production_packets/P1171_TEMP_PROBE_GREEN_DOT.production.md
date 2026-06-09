# P1171_TEMP_PROBE_GREEN_DOT

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1171_TEMP_PROBE_GREEN_DOT |
| Article ID | article.sensor_surface.temp_probe_green_dot |
| Loc namespace | lore.article.sensor_surface.temp_probe_green_dot |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_sensor_surface |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; RS179_FIRST_SENSOR_SURFACE_ARTICLES.md |
| Speaker | Instrument scanner, probe corrosion note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first instrument inspection |
| Location / route | Shelter thermal probe, wet pump panel, or field kit sensor |
| Unlock context | Player scans a temperature probe with a green corrosion dot on the collar |
| Evidence object | Temperature probe, collar, green corrosion dot, probe stem |
| Connected packets | P1153_FUSE_BAND_HEAT_FADE; P1170_SENSOR_WINDOW_BIOFILM_SMEAR; P1174_CALIBRATION_STICKER_EDGE_LIFT |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: teaches corrosion context before instrument reading trust |
| Content status | source_complete_unimported |

## Source Brief

The source knows the probe collar shows green corrosion. It does not know whether the temperature reading is accurate.

Player use: supports early instrument inspection, material aging, and wet shelter maintenance tone.

Forbidden facts: no temperature value, no thermal-system claim, no live diagnostic verdict, no repair unlock, no runtime readiness.

## Surface Texts

### Scanner

TEMP PROBE // Green dot on collar. Treat corrosion before temperature verdict.

### Codex

The green dot belongs to the collar, not the probe tip. That matters. A corroded collar can show moisture and age without proving the temperature channel failed.

The surface gives context before the number gives confidence.

### PDA Log

Probe note:

- Collar: green dot.
- Stem: visible.
- Tip: not judged.
- Use class: corrosion context.

Do not treat the mark as a temperature verdict.

### Environmental Label

TEMP PROBE

GREEN COLLAR DOT

READ AS CORROSION

## Future Integration Notes

- Use as early probe, pump panel, field kit, or shelter instrument article.
- Can support future thermal, diagnostics, or maintenance UI without claiming implementation.
- Keep sensor accuracy and repair behavior unclaimed.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | TEMP PROBE // Green dot on collar. Treat corrosion before temperature verdict. |
| ar_SA | draft_machine_or_llm | مسبار حرارة // نقطة خضراء على الطوق. اقرأ التآكل قبل حكم الحرارة. |
| de_DE | draft_machine_or_llm | TEMP-SONDE // Gruener Punkt am Kragen. Korrosion vor Temperatururteil beachten. |
| es_ES | draft_machine_or_llm | SONDA TERMICA // Punto verde en el collar. Trata la corrosion antes del veredicto de temperatura. |
| fr_FR | draft_machine_or_llm | SONDE THERMIQUE // Point vert sur la bague. Traiter la corrosion avant tout verdict de temperature. |
| he_IL | draft_machine_or_llm | גשוש טמפרטורה // נקודה ירוקה על הצווארון. התייחס לקורוזיה לפני קביעת טמפרטורה. |
| id_ID | draft_machine_or_llm | PROBE SUHU // Titik hijau di kerah. Periksa korosi sebelum menilai suhu. |
| ja_JP | draft_machine_or_llm | 温度プローブ // カラーに緑点。温度判断の前に腐食として扱う。 |
| ko_KR | draft_machine_or_llm | 온도 프로브 // 칼라에 녹색 점. 온도 판정 전에 부식을 먼저 본다. |
| nl_NL | draft_machine_or_llm | TEMPERATUURSONDE // Groene stip op kraag. Behandel corrosie voor temperatuuroordeel. |
| pl_PL | draft_machine_or_llm | SONDA TEMPERATURY // Zielona kropka na kolnierzu. Najpierw potraktuj to jako korozje, potem oceniaj temperature. |
| pt_BR | draft_machine_or_llm | SONDA TERMICA // Ponto verde no colar. Trate a corrosao antes do veredito de temperatura. |
| ru_RU | draft_machine_or_llm | ТЕРМОЗОНД // Зеленая точка на воротнике. Сначала учитывай коррозию, потом суди о температуре. |
| uk_UA | draft_machine_or_llm | ТЕРМОЗОНД // Зелена точка на комірі. Спершу врахуй корозію, потім суди про температуру. |
| zh_CN | draft_machine_or_llm | 温度探头 // 颈圈有绿点。先按腐蚀处理，再判断温度。 |
