# Rationale 3247

Task: P495 string-pool custody stamp production packet.

Mandates followed:

- QA_Evidence_Text_Filter_Audit.txt
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- TOOL_Designer_Facades_CSV_Binary_Bridge.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt

Authority used:

- AGENTS.md
- VISION_LOCKS.md
- TASTE.md
- writing.md
- narrative.md
- localization.md
- data.md
- authoring.md
- quality.md
- Docs/Lore/Canon_Locks.md
- Docs/Lore/Lore_Bible.md
- Docs/Lore/Lore_Content_System.md
- Docs/Lore/Lore_Localization_Model.md
- Docs/Lore/Lore_Multilingual_Content_Architecture.md

Decisions:

- Wrote P495 as an in-world evidence/custody packet, not a developer guide.
- Used `### locale` headings only for the 15 localization rows to keep heading validation strict.
- Used actual Unicode draft rows for RTL, CJK, Cyrillic, Korean, Japanese, Arabic, Hebrew, and Chinese. Latin-language draft rows use ASCII transliteration where needed to avoid mojibake-marker false positives.
- Kept every runtime/import/binary/public claim negative or pending; the packet is authoring text only.
- Did not add route cards, source CSV rows, generated pages, localization source rows, runtime scripts, Unity assets, or h8bin artifacts.

Validation:

- UTF-8 strict read passed for all four written files.
- The packet uses exactly 15 exact locale headings and no bracketed locale/status headings.
- The packet has exactly one authority status row and fourteen draft status rows.
- U+FFFD, mojibake-marker, positive runtime/native/binary/public claim, and forbidden static-proof phrase scans returned zero hits.
