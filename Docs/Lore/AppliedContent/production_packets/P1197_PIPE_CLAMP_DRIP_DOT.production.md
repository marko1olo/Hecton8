# P1197_PIPE_CLAMP_DRIP_DOT

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1197_PIPE_CLAMP_DRIP_DOT |
| Article ID | article.cooling_loop.pipe_clamp_drip_dot |
| Loc namespace | lore.article.cooling_loop.pipe_clamp_drip_dot |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_cooling_loop |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; RS184_FIRST_COOLING_LOOP_TRACE_ARTICLES.md |
| Speaker | Service scanner, pipe clamp note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first service-corner inspection |
| Location / route | Pump corner, shelter service line, exchanger cabinet, or wet utility rack |
| Unlock context | Player scans a single dried drip dot below a pipe clamp screw |
| Evidence object | Pipe clamp, drip dot, screw head, pipe underside |
| Connected packets | P1195_COOLANT_STAIN_BLUE_RIM; P1198_PUMP_LABEL_WET_EDGE; P1199_EXCHANGER_FIN_SALT_BRIDGE |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: teaches leak-trace geometry without fluid-system claims |
| Content status | source_complete_unimported |

## Source Brief

The source knows a drip dried below a clamp screw. It does not know current flow, pressure, or leak state.

Player use: supports pipe route believability, maintenance inspection, and cautious service-corner reading.

Forbidden facts: no live leak claim, no pressure value, no flow simulation, no repair requirement, no runtime readiness.

## Surface Texts

### Scanner

PIPE CLAMP // Dried drip dot below screw. Mark as past leak path, not current flow.

### Codex

The dot sits directly below the screw instead of along the whole pipe. That gives a likely path for a past drip, but not a verdict on the line today.

Small stains can point. They cannot certify.

### PDA Log

Pipe note:

- Clamp screw: above dot.
- Drip mark: dry.
- Pipe underside: dull.
- Use class: past leak path.

Check nearby hardware before calling it active.

### Environmental Label

PIPE CLAMP

DRY DRIP BELOW SCREW

PAST PATH ONLY

## Future Integration Notes

- Use as early pump corner, service line, exchanger cabinet, or wet utility rack article.
- Can support future maintenance or scan copy without claiming implementation.
- Keep current flow, pressure, and repair state unclaimed.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | PIPE CLAMP // Dried drip dot below screw. Mark as past leak path, not current flow. |
| ru_RU | draft_machine_or_llm | ХОМУТ ТРУБЫ // Высохшая капля под винтом. Помечать как старый путь течи, не текущий поток. |
| ja_JP | draft_machine_or_llm | パイプクランプ // ネジ下に乾いた滴点。過去の漏れ経路として記録し、現在の流れとはしない。 |
| zh_CN | draft_machine_or_llm | 管夹 // 螺钉下方有干滴点。标为过去泄漏路径，不是当前流动。 |
| fr_FR | draft_machine_or_llm | COLLIER DE TUYAU // Point de goutte sec sous la vis. Ancien trajet de fuite, pas debit actuel. |
| es_ES | draft_machine_or_llm | ABRAZADERA DE TUBO // Punto de goteo seco bajo el tornillo. Marcar como fuga pasada, no flujo actual. |
| de_DE | draft_machine_or_llm | ROHRSCHELLE // Getrockneter Tropfpunkt unter Schraube. Als alten Leckpfad markieren, nicht als aktuellen Fluss. |
| pl_PL | draft_machine_or_llm | OBEJMA RURY // Wyschnieta kropla pod sruba. Oznacz jako dawny tor przecieku, nie obecny przeplyw. |
| uk_UA | draft_machine_or_llm | ХОМУТ ТРУБИ // Висохла крапля під гвинтом. Позначати як старий шлях витоку, не поточний потік. |
| ar_SA | draft_machine_or_llm | مشبك أنبوب // نقطة تقطر جافة تحت البرغي. سجلها كمسار تسرب سابق، لا كتدفق حالي. |
| id_ID | draft_machine_or_llm | KLEM PIPA // Titik tetes kering di bawah sekrup. Tandai sebagai jalur bocor lama, bukan aliran sekarang. |
| ko_KR | draft_machine_or_llm | 파이프 클램프 // 나사 아래 마른 물방울 점. 과거 누수 경로로 표시하고 현재 흐름으로 보지 않는다. |
| he_IL | draft_machine_or_llm | מהדק צינור // נקודת טפטוף יבשה מתחת לבורג. לסמן כנתיב דליפה ישן, לא כזרימה נוכחית. |
| pt_BR | draft_machine_or_llm | ABRACADEIRA DE TUBO // Ponto de goteira seco sob parafuso. Marque como caminho antigo de vazamento, nao fluxo atual. |
| nl_NL | draft_machine_or_llm | PIJPKLEM // Droge druppelstip onder schroef. Markeer als oud lekpad, niet als huidige stroming. |
