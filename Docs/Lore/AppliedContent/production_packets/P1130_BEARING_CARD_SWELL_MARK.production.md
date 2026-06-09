# P1130_BEARING_CARD_SWELL_MARK

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1130_BEARING_CARD_SWELL_MARK |
| Article ID | article.navigation_marks.bearing_card_swell_mark |
| Loc namespace | lore.article.navigation_marks.bearing_card_swell_mark |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_navigation_marks |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; RS169_FIRST_NAVIGATION_MARK_ARTICLES.md |
| Speaker | Field scanner, local bearing note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first navigation marks |
| Location / route | Service post, shoreline crate lid, or first shelter route board |
| Unlock context | Player scans a bearing card with a swollen corner and intact ink notch |
| Evidence object | Bearing card, swollen corner, ink notch, brass pin |
| Connected packets | P1134_SIGNAL_MAST_SHADOW_MARK; P905_ANCHOR_PAINT_CURRENT_SIDE; P1132_TETHER_KNOT_WET_SET |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: gives the player a local bearing check without claiming map or compass readiness |
| Content status | source_complete_unimported |

## Source Brief

The source knows the card was soaked, but one ink notch still provides a local bearing reference. The article should keep the mark useful while warning that the swollen corner can distort card alignment.

Player use: supports route reading, salvage navigation, and shoreline object credibility.

Forbidden facts: no map-system claim, no automatic waypoint, no full compass correction, no runtime readiness.

## Surface Texts

### Scanner

BEARING CARD // Corner swollen. Use ink notch, not card edge, for local bearing.

### Codex

The card edge has swollen enough to lie. The ink notch near the brass pin is the better reference because it stayed anchored after the corner took water. Local bearings survive when the player reads the mark that resisted drift.

Pin and notch stayed fixed; the soaked corner did not.

### PDA Log

Bearing note:

- Corner: swollen.
- Ink notch: intact.
- Brass pin: fixed.
- Use class: local reference only.

Do not align a route by the warped edge.

### Environmental Label

BEARING CARD

CORNER SWOLLEN

USE INK NOTCH

## Future Integration Notes

- Use as early route, marker, or shelter-board article.
- Can support future navigation UI without claiming implementation.
- Keep bearing language local and evidence-based.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | BEARING CARD // Corner swollen. Use ink notch, not card edge, for local bearing. |
| ar_SA | draft_machine_or_llm | بطاقة اتجاه // الزاوية متورمة. استخدم شق الحبر لا حافة البطاقة للاتجاه المحلي. |
| de_DE | draft_machine_or_llm | PEILKARTE // Ecke aufgequollen. Fuer lokale Peilung Tintenkerbe nutzen, nicht Kartenkante. |
| es_ES | draft_machine_or_llm | TARJETA DE RUMBO // Esquina hinchada. Usa la muesca de tinta, no el borde, para el rumbo local. |
| fr_FR | draft_machine_or_llm | CARTE DE RELEVEMENT // Coin gonfle. Pour le relevement local, utiliser l'encoche d'encre, pas le bord. |
| he_IL | draft_machine_or_llm | כרטיס כיוון // הפינה תפוחה. השתמש בחריץ הדיו, לא בשפת הכרטיס, לכיוון מקומי. |
| id_ID | draft_machine_or_llm | KARTU ARAH // Sudut mengembang. Pakai takik tinta, bukan tepi kartu, untuk arah lokal. |
| ja_JP | draft_machine_or_llm | 方位カード // 角が膨潤。局所方位はカード端ではなくインク刻みを使う。 |
| ko_KR | draft_machine_or_llm | 방위 카드 // 모서리가 부풀었다. 지역 방위는 카드 가장자리 말고 잉크 홈을 쓴다. |
| nl_NL | draft_machine_or_llm | PEILKAART // Hoek is opgezwollen. Gebruik de inktkerf, niet de kaartrand, voor lokale peiling. |
| pl_PL | draft_machine_or_llm | KARTA NAMIERZANIA // Rog spuchniety. Do lokalnego namiaru uzyj naciecia tuszu, nie krawedzi karty. |
| pt_BR | draft_machine_or_llm | CARTAO DE RUMO // Canto inchado. Use o entalhe de tinta, nao a borda, para rumo local. |
| ru_RU | draft_machine_or_llm | КАРТА ПЕЛЕНГА // Угол разбух. Для местного пеленга используй чернильную засечку, не край карты. |
| uk_UA | draft_machine_or_llm | КАРТКА ПЕЛЕНГА // Кут набух. Для місцевого пеленга використовуй чорнильну засічку, не край картки. |
| zh_CN | draft_machine_or_llm | 方位卡 // 角部胀起。按墨迹刻口取本地方位，不要按卡边。 |
