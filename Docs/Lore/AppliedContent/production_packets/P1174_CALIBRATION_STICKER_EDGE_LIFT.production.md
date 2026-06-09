# P1174_CALIBRATION_STICKER_EDGE_LIFT

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1174_CALIBRATION_STICKER_EDGE_LIFT |
| Article ID | article.sensor_surface.calibration_sticker_edge_lift |
| Loc namespace | lore.article.sensor_surface.calibration_sticker_edge_lift |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_sensor_surface |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; RS179_FIRST_SENSOR_SURFACE_ARTICLES.md |
| Speaker | Instrument scanner, calibration-label note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first instrument inspection |
| Location / route | Shelter service box, probe case, range kit, or wet console door |
| Unlock context | Player scans a calibration sticker lifting past its seat line |
| Evidence object | Calibration sticker, lifted edge, seat line, label glue |
| Connected packets | P1171_TEMP_PROBE_GREEN_DOT; P1173_RANGE_FIND_LENS_SCRATCH; P1152_STATUS_LENS_WATERLINE |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: teaches label provenance without calibration-system claims |
| Content status | source_complete_unimported |

## Source Brief

The source knows the calibration sticker has lifted and moved past its seat line. It does not know whether the current setup is calibrated.

Player use: supports instrument provenance, caution around labels, and believable maintenance language.

Forbidden facts: no calibration verdict, no date authority, no diagnostics implementation, no UI state, no runtime readiness.

## Surface Texts

### Scanner

CAL STICKER // Edge lifted past seat line. Label may not date current setup.

### Codex

The sticker still carries a calibration mark, but the lifted edge crosses the seat line. That means the label can no longer date the current arrangement by itself.

Loose labels shift. Seat marks show where this one used to sit.

### PDA Log

Label note:

- Sticker edge: lifted.
- Seat line: crossed.
- Glue: dry at corner.
- Use class: provenance warning.

Do not treat the label as current without another cue.

### Environmental Label

CAL STICKER

EDGE PAST SEAT LINE

VERIFY WITH OTHER CUE

## Future Integration Notes

- Use as early instrument, service box, range kit, or probe-case article.
- Can support future diagnostics, calibration text, or scan UI without claiming implementation.
- Keep calibration state and dates unclaimed.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | CAL STICKER // Edge lifted past seat line. Label may not date current setup. |
| ar_SA | draft_machine_or_llm | ملصق معايرة // الحافة مرفوعة بعد خط الجلوس. قد لا يؤرخ الملصق الإعداد الحالي. |
| de_DE | draft_machine_or_llm | KAL-AUFKLEBER // Kante ueber Sitzlinie geloest. Label datiert aktuellen Aufbau eventuell nicht. |
| es_ES | draft_machine_or_llm | PEGATINA DE CAL // Borde levantado mas alla de la linea de asiento. La etiqueta puede no fechar el montaje actual. |
| fr_FR | draft_machine_or_llm | ETIQUETTE CAL // Bord leve au-dela de la ligne d'assise. L'etiquette ne date peut-etre pas l'installation actuelle. |
| he_IL | draft_machine_or_llm | מדבקת כיול // הקצה התרומם מעבר לקו הישיבה. ייתכן שהתווית לא מתאימה להגדרה הנוכחית. |
| id_ID | draft_machine_or_llm | STIKER KAL // Tepi terangkat melewati garis duduk. Label mungkin bukan acuan tanggal setup saat ini. |
| ja_JP | draft_machine_or_llm | 校正シール // 端が座り線を越えて浮く。現在設定の日付根拠にはならない可能性。 |
| ko_KR | draft_machine_or_llm | 보정 스티커 // 가장자리가 자리선 너머로 들렸다. 현재 설정 날짜 근거가 아닐 수 있다. |
| nl_NL | draft_machine_or_llm | KAL-STICKER // Rand voorbij zitlijn los. Label dateert huidige opstelling mogelijk niet. |
| pl_PL | draft_machine_or_llm | NAKLEJKA KAL // Krawedz podniesiona poza linie osadzenia. Etykieta moze nie datowac obecnej konfiguracji. |
| pt_BR | draft_machine_or_llm | ETIQUETA CAL // Borda levantada alem da linha de assentamento. Etiqueta pode nao datar a configuracao atual. |
| ru_RU | draft_machine_or_llm | КАЛИБРОВОЧНАЯ НАКЛЕЙКА // Край поднят за посадочную линию. Наклейка может не датировать текущую сборку. |
| uk_UA | draft_machine_or_llm | КАЛІБРУВАЛЬНА НАКЛЕЙКА // Край піднятий за посадкову лінію. Наклейка може не датувати поточне налаштування. |
| zh_CN | draft_machine_or_llm | 校准贴 // 边缘翘过座线。标签未必能给当前设置定年。 |
