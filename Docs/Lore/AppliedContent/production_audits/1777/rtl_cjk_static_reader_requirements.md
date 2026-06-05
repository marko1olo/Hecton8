# RTL/CJK Static Reader Requirements - 1777

Target reader: `Docs/Lore/AppliedContent/reader.html`.

Evidence class: STATIC_DOC / STATIC_SOURCE.

## Requirements
- Use existing `reader.html`; do not require a new reader path or sibling-agent output.
- Read `Publication_Surface_Index.csv` `direction` and page frontmatter `direction`; apply `dir="rtl"` for `ar_SA` and `he_IL`, `dir="ltr"` for all other official locales.
- Do not manually reverse Arabic/Hebrew content. Keep logical source order so embedded IDs (`HECTON-8`, `Atlas-6`, packet IDs, hashes, meters, tonne-window labels) remain stable.
- Preserve Latin technical identifiers in RTL/CJK text and isolate mixed-direction runs with HTML bidi controls or CSS (`unicode-bidi: plaintext` or equivalent) at the text block level.
- CJK pages (`ja_JP`, `zh_CN`, `ko_KR`) require no-space wrapping support, static glyph coverage checks, and line-height that tolerates dense ideographs/Hangul without clipping.
- Titles, scanner rows, terminal headings, and cluster nav need per-locale overflow handling; static weighted-length candidates are listed in `text_expansion_risk.md`.
- Page body/status frontmatter may carry `draft_native_pass_pending`; body text must not expose authoring status as player/public copy unless the packet is an explicit QA/proof card excluded from release publication.

## Current Static Findings
- ar_SA: directory present on both surfaces; expected direction `rtl`. See `locale_directory_inventory.csv` and `localization_issue_candidates.csv`.
- he_IL: directory present on both surfaces; expected direction `rtl`. See `locale_directory_inventory.csv` and `localization_issue_candidates.csv`.
- ja_JP: directory present on both surfaces; expected direction `ltr`. See `locale_directory_inventory.csv` and `localization_issue_candidates.csv`.
- zh_CN: directory present on both surfaces; expected direction `ltr`. See `locale_directory_inventory.csv` and `localization_issue_candidates.csv`.
- ko_KR: directory present on both surfaces; expected direction `ltr`. See `locale_directory_inventory.csv` and `localization_issue_candidates.csv`.
