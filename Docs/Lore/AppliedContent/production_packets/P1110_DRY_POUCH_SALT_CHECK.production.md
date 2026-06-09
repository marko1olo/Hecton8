# P1110_DRY_POUCH_SALT_CHECK

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1110_DRY_POUCH_SALT_CHECK |
| Article ID | article.inventory_sorting.dry_pouch_salt_check |
| Loc namespace | lore.article.inventory_sorting.dry_pouch_salt_check |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_inventory_sorting |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; RS165_FIRST_INVENTORY_SORTING_ARTICLES.md |
| Speaker | Inventory scanner, dry-stock sorting note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first inventory sorting |
| Location / route | Emergency crate, first shelter shelf, or repair stock corner |
| Unlock context | Player scans a dry pouch with a salt check strip |
| Evidence object | Dry pouch, salt check strip, clean fold, sealed corner |
| Connected packets | P930_CRATE_SEAL_CUSTODY_TAB; P861_CONDENSER_CUP_FIRST_DRIP; P900_MED_FOAM_PRESSURE_PATCH |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: lets early stock separate dry usable material from wet evidence |
| Content status | source_complete_unimported |

## Source Brief

The source knows the pouch stayed dry enough for clean stock if the salt strip remains pale. It does not claim inventory categories are implemented. The article should make "dry" a checked condition, not an assumption.

Player use: supports first inventory sorting, med stock protection, and repair-stock credibility.

Forbidden facts: no inventory-system claim, no sterile guarantee, no full stock approval, no runtime readiness.

## Surface Texts

### Scanner

DRY POUCH // Salt strip is pale. Keep clean stock here; recheck after wet handling.

### Codex

The pouch earns its name by evidence, not hope. The salt strip is still pale, the fold stayed clean, and one sealed corner held. That makes it useful for clean stock, provided the player does not ruin it by mixing wet salvage into the same space.

Dry is a maintained state.

### PDA Log

Sorting note:

- Salt strip: pale.
- Fold: clean.
- Sealed corner: holding.
- Use class: clean stock only.

Recheck after handling wet material.

### Environmental Label

DRY POUCH

SALT STRIP PALE

CLEAN STOCK ONLY

## Future Integration Notes

- Use as early inventory sorting or crate-stock article.
- Can support future UI categories without claiming implementation.
- Keep clean-stock language conditional and inspected.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | DRY POUCH // Salt strip is pale. Keep clean stock here; recheck after wet handling. |
| ar_SA | draft_machine_or_llm | حقيبة جافة // شريط الملح باهت. ضع المخزون النظيف هنا، وأعد الفحص بعد التعامل مع مواد مبللة. |
| de_DE | draft_machine_or_llm | TROCKENBEUTEL // Salzstreifen ist blass. Sauberen Vorrat hier lagern; nach nasser Handhabung erneut prüfen. |
| es_ES | draft_machine_or_llm | BOLSA SECA // La tira de sal está pálida. Guarda aquí material limpio; revisa tras manipular piezas mojadas. |
| fr_FR | draft_machine_or_llm | POCHE SÈCHE // La bande de sel est pâle. Garder le stock propre ici; revérifier après manipulation humide. |
| he_IL | draft_machine_or_llm | שקית יבשה // פס המלח חיוור. שמור כאן ציוד נקי ובדוק שוב אחרי טיפול רטוב. |
| id_ID | draft_machine_or_llm | KANTONG KERING // Strip garam pucat. Simpan stok bersih di sini; cek ulang setelah menangani barang basah. |
| ja_JP | draft_machine_or_llm | ドライポーチ // 塩分ストリップは淡色。清潔な備品をここに保管し、濡れ物を扱った後に再確認。 |
| ko_KR | draft_machine_or_llm | 건조 파우치 // 염분 스트립이 옅다. 깨끗한 물품은 여기에 보관하고 젖은 물품을 다룬 뒤 다시 확인하라. |
| nl_NL | draft_machine_or_llm | DROGE POUCH // Zoutstrip is bleek. Bewaar schone voorraad hier; controleer opnieuw na natte hantering. |
| pl_PL | draft_machine_or_llm | SUCHA SASZETKA // Pasek solny jest blady. Trzymaj tu czysty zapas; sprawdź ponownie po pracy z mokrym materiałem. |
| pt_BR | draft_machine_or_llm | BOLSA SECA // A tira de sal está pálida. Guarde estoque limpo aqui; confira após lidar com material molhado. |
| ru_RU | draft_machine_or_llm | СУХОЙ ПАКЕТ // Солевая полоска бледная. Чистый запас держи здесь; после мокрой работы проверь снова. |
| uk_UA | draft_machine_or_llm | СУХИЙ ПАКЕТ // Соляна смужка бліда. Чистий запас тримай тут; після мокрої роботи перевір знову. |
| zh_CN | draft_machine_or_llm | 干袋 // 盐检条颜色很淡。干净物资放这里；接触湿物后重新检查。 |
