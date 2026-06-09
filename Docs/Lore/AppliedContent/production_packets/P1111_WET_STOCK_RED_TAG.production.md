# P1111_WET_STOCK_RED_TAG

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1111_WET_STOCK_RED_TAG |
| Article ID | article.inventory_sorting.wet_stock_red_tag |
| Loc namespace | lore.article.inventory_sorting.wet_stock_red_tag |
| Runtime layer | future_import_candidate |
| Surfaces | scanner, codex, pda_log, environmental_label |
| Spoiler level | arrival_inventory_sorting |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; RS165_FIRST_INVENTORY_SORTING_ARTICLES.md |
| Speaker | Inventory scanner, wet-stock sorting note |
| Audience | Player, later PDA/codex route |
| Date / era | Route 1, first inventory sorting |
| Location / route | Crash shelf, shelter shelf, or first repair-stock sorting area |
| Unlock context | Player scans a wet-stock red tag on a pouch or crate divider |
| Evidence object | Wet stock tag, red tab, drip corner, quarantine fold |
| Connected packets | P963_BRINE_SLICK_RAINBOW_EDGE; P930_CRATE_SEAL_CUSTODY_TAB; P903_ANTISEPTIC_AMPOULE_CLOUD |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: separates wet material from clean medical/repair stock |
| Content status | source_complete_unimported |

## Source Brief

The source knows wet stock needs separate handling and testing before use. It does not implement quarantine logic. The article should make wet-tag sorting a physical discipline.

Player use: supports future inventory wording and prevents wet salvage from reading as immediately clean.

Forbidden facts: no inventory-system claim, no automatic cleaning, no safe-use approval, no runtime readiness.

## Surface Texts

### Scanner

WET STOCK // Red tag is active. Keep separate until tested, dried, or discarded.

### Codex

The red tag is not accusation. It is triage for objects. Wet stock can still be useful, but it must not sit against clean pouches, med foam, battery contacts, or dry fabric until the water is understood.

Separation is the first repair.

### PDA Log

Wet-stock note:

- Red tag: active.
- Drip corner: present.
- Clean-stock contact: avoid.
- Use state: test, dry, or discard.

Do not mix with dry pouch.

### Environmental Label

WET STOCK

KEEP SEPARATE

TEST BEFORE USE

## Future Integration Notes

- Use as first sorting label or salvage handling article.
- Can support future inventory/inspection UI without runtime claims.
- Keep "wet" as a caution state, not a permanent classification.
- Importer/publication readiness remains false until localization, packet bundle, and Unity content proof exist.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | WET STOCK // Red tag is active. Keep separate until tested, dried, or discarded. |
| ar_SA | draft_machine_or_llm | مخزون مبلل // البطاقة الحمراء مفعلة. أبقه منفصلا حتى يتم اختباره أو تجفيفه أو التخلص منه. |
| de_DE | draft_machine_or_llm | NASSBESTAND // Rote Markierung ist aktiv. Getrennt halten, bis geprüft, getrocknet oder verworfen. |
| es_ES | draft_machine_or_llm | MATERIAL MOJADO // Etiqueta roja activa. Mantén separado hasta probar, secar o descartar. |
| fr_FR | draft_machine_or_llm | STOCK HUMIDE // Étiquette rouge active. Garder séparé jusqu'au test, séchage ou rebut. |
| he_IL | draft_machine_or_llm | מלאי רטוב // תג אדום פעיל. שמור בנפרד עד בדיקה, ייבוש או השלכה. |
| id_ID | draft_machine_or_llm | STOK BASAH // Tag merah aktif. Pisahkan sampai diuji, dikeringkan, atau dibuang. |
| ja_JP | draft_machine_or_llm | 濡れ在庫 // 赤タグ有効。検査、乾燥、廃棄まで分離。 |
| ko_KR | draft_machine_or_llm | 젖은 물품 // 붉은 태그가 활성이다. 검사, 건조, 폐기 전까지 분리하라. |
| nl_NL | draft_machine_or_llm | NATTE VOORRAAD // Rood label actief. Apart houden tot getest, gedroogd of weggegooid. |
| pl_PL | draft_machine_or_llm | MOKRY ZAPAS // Czerwona etykieta aktywna. Trzymaj osobno do testu, suszenia albo odrzutu. |
| pt_BR | draft_machine_or_llm | ESTOQUE MOLHADO // Etiqueta vermelha ativa. Mantenha separado até testar, secar ou descartar. |
| ru_RU | draft_machine_or_llm | МОКРЫЙ ЗАПАС // Красная бирка активна. Держи отдельно до проверки, сушки или списания. |
| uk_UA | draft_machine_or_llm | МОКРИЙ ЗАПАС // Червона бирка активна. Тримай окремо до перевірки, сушіння або списання. |
| zh_CN | draft_machine_or_llm | 湿物资 // 红色标签有效。测试、干燥或丢弃前保持隔离。 |
