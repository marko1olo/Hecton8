# P1202_RELAY_CASE_OZONE_STAIN

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1202_RELAY_CASE_OZONE_STAIN |
| Article ID | article.power_status.relay_case_ozone_stain |
| Loc namespace | lore.article.power_status.relay_case_ozone_stain |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_power_status |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; RS185_FIRST_POWER_STATUS_TRACE_ARTICLES.md |
| Speaker | Service scanner, relay case note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first power-panel inspection |
| Location / route | Relay tray, wet service panel, breaker cabinet, or emergency-light box |
| Unlock context | Player scans a relay case with a dark ozone stain near the vent slit |
| Evidence object | Relay case, ozone stain, vent slit, label curl |
| Connected packets | P1200_BREAKER_FLAG_HALF_TRAVEL; P1201_CONTACT_PAD_MATTE_PATCH; P1204_BUS_LABEL_HEAT_CURL |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: teaches relay stress evidence without electrical-state claims |
| Content status | source_complete_unimported |

## Source Brief

The source knows the relay case carries a dark stain near a vent slit. It does not know whether the relay is active or failed.

Player use: supports panel realism, caution around old hardware, and power-adjacent object reading.

Forbidden facts: no live relay state, no fault code, no shock event, no repair unlock, no runtime readiness.

## Surface Texts

### Scanner

RELAY CASE // Dark stain near vent slit. Treat as stress trace, not fault code.

### Codex

The stain sits by the vent slit, where heat, ozone, dust, or residue could leave a mark after stress. It gives the relay history but not a present state.

Stress evidence is not the same thing as a diagnostic code.

### PDA Log

Relay note:

- Vent slit: stained.
- Case edge: dry.
- Label: slight curl.
- Use class: stress trace.

Do not read the stain as a current fault code.

### Environmental Label

RELAY CASE

STAIN AT VENT SLIT

STRESS TRACE

## Future Integration Notes

- Use as early relay tray, service panel, breaker cabinet, or emergency-light box article.
- Can support future power or diagnostics copy without claiming implementation.
- Keep relay state, fault code, and repair behavior unclaimed.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | RELAY CASE // Dark stain near vent slit. Treat as stress trace, not fault code. |
| ru_RU | draft_machine_or_llm | КОРПУС РЕЛЕ // Темное пятно у вентиляционной щели. Считать следом нагрузки, не кодом ошибки. |
| ja_JP | draft_machine_or_llm | リレーケース // 通気スリット近くに黒い染み。ストレス痕として扱い、故障コードにはしない。 |
| zh_CN | draft_machine_or_llm | 继电器外壳 // 通风缝旁有暗斑。按受压痕迹处理，不作故障码。 |
| fr_FR | draft_machine_or_llm | BOITIER DE RELAIS // Tache sombre pres de la fente d'aeration. Trace de stress, pas code de panne. |
| es_ES | draft_machine_or_llm | CAJA DE RELE // Mancha oscura cerca de ranura de ventilacion. Tratar como rastro de esfuerzo, no codigo de falla. |
| de_DE | draft_machine_or_llm | RELAISGEHAEUSE // Dunkler Fleck nahe Lueftungsschlitz. Als Belastungsspur lesen, nicht als Fehlercode. |
| pl_PL | draft_machine_or_llm | OBUDOWA PRZEKAZNIKA // Ciemna plama przy szczelinie wentylacyjnej. To slad obciazenia, nie kod usterki. |
| uk_UA | draft_machine_or_llm | КОРПУС РЕЛЕ // Темна пляма біля вентиляційної щілини. Вважати слідом навантаження, не кодом збою. |
| ar_SA | draft_machine_or_llm | غلاف مرحل // بقعة داكنة قرب شق التهوية. عاملها كأثر إجهاد، لا كرمز عطل. |
| id_ID | draft_machine_or_llm | RUMAH RELAI // Noda gelap dekat celah ventilasi. Anggap jejak tekanan, bukan kode gangguan. |
| ko_KR | draft_machine_or_llm | 릴레이 케이스 // 통풍 틈 근처 어두운 얼룩. 고장 코드가 아니라 스트레스 흔적으로 본다. |
| he_IL | draft_machine_or_llm | מארז ממסר // כתם כהה ליד חריץ אוורור. להתייחס כסימן עומס, לא כקוד תקלה. |
| pt_BR | draft_machine_or_llm | CAIXA DO RELE // Mancha escura perto da fenda de ventilacao. Trate como marca de estresse, nao codigo de falha. |
| nl_NL | draft_machine_or_llm | RELAISKAST // Donkere vlek bij ventilatiesleuf. Lees als stressspoor, niet als foutcode. |
