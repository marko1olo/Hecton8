# 1777 Top Localization Risks

Evidence class: STATIC_SOURCE / STATIC_DOC.

## Risk Summary
- draft_marker_packet: 34175
- english_fallback_packet: 17988
- english_fallback_page: 4986
- draft_marker_page: 2444
- mojibake_page: 2

## Top Fixable Or Documentable Risks
- Non-English fallback is concentrated in QA/brief/proof packets where translated rows carry English operational QA text; this must stay native-review-needed and should not be treated as shipped localization.
- Generated localized pages expose QA language such as "Localization QA", "QA gate", and "native review" in page bodies. These are acceptable only for internal QA/proof-card surfaces, not player/public release pages.
- Static text-bound risk is concentrated in long wiki/site bodies and title rows with compound technical IDs. Requires TMP/reader capture for runtime proof.
- RTL/CJK directories and frontmatter exist, but static files do not prove shaping, bidi isolation, glyph atlas coverage, or no-space wrapping.
- No native-final proof was found; every non-English row remains at native-review-needed/pending-native-review unless a human review artifact is supplied.

## Packet Source Scope
- Active packet rows counted: 460.
- Includes bundle files and legacy single-packet JSON files in `Docs/Lore/AppliedContent/packets/` to match publication/status totals.
