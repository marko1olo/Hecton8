# P1125_RATION_FOIL_PINHOLE

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1125_RATION_FOIL_PINHOLE |
| Article ID | article.ration_checks.ration_foil_pinhole |
| Loc namespace | lore.article.ration_checks.ration_foil_pinhole |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_ration_checks |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; RS173_FIRST_RATION_CHECK_ARTICLES.md |
| Speaker | Survival scanner, ration inspection note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first ration checking |
| Location / route | Emergency crate, first shelter shelf, or raft salvage pouch |
| Unlock context | Player scans a ration pouch with a pinhole in the foil layer |
| Evidence object | Ration pouch, foil pinhole, pressure crease, dry edge |
| Connected packets | P1110_DRY_POUCH_SALT_CHECK; P1111_WET_STOCK_RED_TAG; P1126_NUTRIENT_GEL_COLD_LAYER |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: teaches early ration inspection before the player treats sealed-looking food as clean |
| Content status | source_complete_unimported |

## Source Brief

The source knows the pouch is compromised by a small pinhole even if the outer shape looks intact. The article should make the player check foil, pressure crease, and dry edge before trusting the ration.

Player use: supports early supply sorting, survival tone, and crate inspection.

Forbidden facts: no food-system claim, no edibility verdict, no medical advice, no runtime readiness.

## Surface Texts

### Scanner

RATION POUCH // Foil pinhole detected. Quarantine from clean stock until inspected.

### Codex

The pouch still looks square, but the foil layer has a pinhole near the pressure crease. That is enough to move it out of clean stock. A ration can fail at a point smaller than the label.

Sealed-looking is not sealed.

### PDA Log

Ration note:

- Foil: pinhole near crease.
- Edge: dry.
- Outer pouch: intact shape.
- Use class: quarantine pending inspection.

Do not store against clean med stock.

### Environmental Label

RATION POUCH

FOIL PINHOLE

QUARANTINE FROM CLEAN STOCK

## Future Integration Notes

- Use as early ration, crate, or inventory sorting article.
- Can support future food/ration UI without claiming implementation.
- Keep condition language inspection-based, not a consumption rule.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | RATION POUCH // Foil pinhole detected. Quarantine from clean stock until inspected. |
| ar_SA | draft_machine_or_llm | كيس حصة // ثقب دقيق في الرقاقة. اعزله عن المخزون النظيف حتى يتم فحصه. |
| de_DE | draft_machine_or_llm | RATIONENBEUTEL // Nadelstich in der Folie erkannt. Bis zur Prüfung vom sauberen Vorrat trennen. |
| es_ES | draft_machine_or_llm | BOLSA DE RACIÓN // Detectado microagujero en la lámina. Aísla del material limpio hasta inspección. |
| fr_FR | draft_machine_or_llm | SACHET DE RATION // Microperforation détectée dans le film. Isoler du stock propre jusqu'à inspection. |
| he_IL | draft_machine_or_llm | שקית מנה // זוהה חור סיכה ביריעת האלומיניום. לבודד מהמלאי הנקי עד בדיקה. |
| id_ID | draft_machine_or_llm | KANTONG RANSUM // Lubang jarum di foil terdeteksi. Karantina dari stok bersih sampai diperiksa. |
| ja_JP | draft_machine_or_llm | レーション袋 // 箔にピンホール検出。検査まで清潔在庫から隔離。 |
| ko_KR | draft_machine_or_llm | 배급 파우치 // 포일에 핀홀 감지. 검사 전까지 깨끗한 재고에서 격리하라. |
| nl_NL | draft_machine_or_llm | RANTSOENZAK // Speldenprik in folie gevonden. Apart houden van schone voorraad tot inspectie. |
| pl_PL | draft_machine_or_llm | SASZETKA RACJI // Wykryto mikrootwór w folii. Odizoluj od czystego zapasu do kontroli. |
| pt_BR | draft_machine_or_llm | BOLSA DE RAÇÃO // Microfuro detectado na lâmina. Isole do estoque limpo até inspeção. |
| ru_RU | draft_machine_or_llm | ПАКЕТ ПАЙКА // В фольге найден прокол. Держи отдельно от чистого запаса до осмотра. |
| uk_UA | draft_machine_or_llm | ПАКЕТ ПАЙКА // У фользі знайдено прокол. Тримай окремо від чистого запасу до огляду. |
| zh_CN | draft_machine_or_llm | 口粮袋 // 箔层发现针孔。检查前与干净物资隔离。 |
