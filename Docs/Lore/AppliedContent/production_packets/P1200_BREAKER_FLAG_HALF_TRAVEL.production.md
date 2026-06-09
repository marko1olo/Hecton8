# P1200_BREAKER_FLAG_HALF_TRAVEL

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1200_BREAKER_FLAG_HALF_TRAVEL |
| Article ID | article.power_status.breaker_flag_half_travel |
| Loc namespace | lore.article.power_status.breaker_flag_half_travel |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_power_status |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; RS185_FIRST_POWER_STATUS_TRACE_ARTICLES.md |
| Speaker | Service scanner, breaker flag note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first power-panel inspection |
| Location / route | Shelter breaker row, wet relay box, service cabinet, or emergency panel |
| Unlock context | Player scans a breaker flag stopped between two printed positions |
| Evidence object | Breaker flag, half-travel notch, printed positions, panel dust |
| Connected packets | P1201_CONTACT_PAD_MATTE_PATCH; P1202_RELAY_CASE_OZONE_STAIN; P1203_EMERGENCY_LIGHT_DUST_HALO |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: teaches ambiguous hardware state without power-system claims |
| Content status | source_complete_unimported |

## Source Brief

The source knows the flag sits between printed positions. It does not know whether the circuit is live, tripped, or safe.

Player use: supports cautious power-panel reading and discourages trusting labels as live-state proof.

Forbidden facts: no live power verdict, no shock event, no repair instruction, no UI state, no runtime readiness.

## Surface Texts

### Scanner

BREAKER FLAG // Stopped between marks. Treat print as position clue, not live state.

### Codex

The flag sits between the two printed marks, which makes the panel harder to read at a glance. The flag position is useful, but it is not a live circuit proof.

Ambiguous hardware needs corroboration before confidence.

### PDA Log

Breaker note:

- Flag: half-travel.
- Printed marks: readable.
- Panel dust: disturbed near slot.
- Use class: ambiguous position.

Do not treat the mark as a live verdict.

### Environmental Label

BREAKER FLAG

BETWEEN PRINTED MARKS

POSITION, NOT POWER

## Future Integration Notes

- Use as early breaker row, relay box, service cabinet, or emergency panel article.
- Can support future power or diagnostics copy without claiming implementation.
- Keep live state and repair behavior unclaimed.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | BREAKER FLAG // Stopped between marks. Treat print as position clue, not live state. |
| ru_RU | draft_machine_or_llm | ФЛАЖОК АВТОМАТА // Застрял между метками. Печатная метка - подсказка положения, не статус под напряжением. |
| ja_JP | draft_machine_or_llm | ブレーカーフラグ // 目盛りの間で停止。印字は位置の手掛かり、通電状態ではない。 |
| zh_CN | draft_machine_or_llm | 断路器标旗 // 停在标记之间。印字只是位置线索，不是带电状态。 |
| fr_FR | draft_machine_or_llm | DRAPEAU DISJONCTEUR // Arrete entre les marques. L'impression donne une position, pas un etat sous tension. |
| es_ES | draft_machine_or_llm | INDICADOR DE DISYUNTOR // Detenido entre marcas. La impresion orienta posicion, no estado energizado. |
| de_DE | draft_machine_or_llm | SCHALTERFLAGGE // Zwischen Markierungen stehen geblieben. Druck ist Positionshinweis, kein Live-Zustand. |
| pl_PL | draft_machine_or_llm | ZNACZNIK WYLACZNIKA // Zatrzymany miedzy znakami. Nadruk to wskazowka pozycji, nie stan pod napieciem. |
| uk_UA | draft_machine_or_llm | ПРАПОРЕЦЬ ВИМИКАЧА // Застряг між мітками. Друк дає підказку положення, не стан під напругою. |
| ar_SA | draft_machine_or_llm | علامة قاطع // توقفت بين العلامات. الطباعة دليل موضع، لا حالة كهرباء حية. |
| id_ID | draft_machine_or_llm | BENDERA PEMUTUS // Berhenti di antara tanda. Cetak hanya petunjuk posisi, bukan status bertegangan. |
| ko_KR | draft_machine_or_llm | 차단기 플래그 // 표시 사이에 멈춤. 인쇄는 위치 단서이지 통전 상태가 아니다. |
| he_IL | draft_machine_or_llm | דגל מפסק // נעצר בין הסימנים. ההדפס הוא רמז מיקום, לא מצב חי. |
| pt_BR | draft_machine_or_llm | INDICADOR DO DISJUNTOR // Parado entre marcas. A impressao indica posicao, nao estado energizado. |
| nl_NL | draft_machine_or_llm | SCHAKELAARVLAG // Gestopt tussen markeringen. Opdruk is positiehint, geen spanningstoestand. |
