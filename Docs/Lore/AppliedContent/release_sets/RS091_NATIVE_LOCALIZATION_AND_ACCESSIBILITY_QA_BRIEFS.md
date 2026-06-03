# Rs091 Native Localization And Accessibility Qa Briefs

Status: production-facing draft pending native localization and runtime placement.
Runtime rule: source content only; no runtime JSON, markdown or live translation.

Purpose: native localization, encoding, RTL/CJK/font, subtitle and audio review briefs.

## Packets

- `P451_RU_NATIVE_ENCODING_QA_BRIEF` - RU Native Encoding QA Brief.
- `P452_CJK_FONT_WRAP_QA_BRIEF` - CJK Font Wrap QA Brief.
- `P453_RTL_BIDI_NUMERIC_QA_BRIEF` - RTL Bidi Numeric QA Brief.
- `P454_EUROPEAN_EXPANSION_FIT_QA_BRIEF` - European Expansion Fit QA Brief.
- `P455_SUBTITLE_AUDIO_TIMING_QA_BRIEF` - Subtitle Audio Timing QA Brief.

## Use

- In-game: scanner, terminal, PDA/codex, dossier or audio transcript source rows after DataMonolith bake.
- Site/wiki: external article modules generated from the same packet IDs.
- Authoring: route cards, evidence graph, binding maps, image briefs and placement backlog.

## Boundary

This release set does not claim Unity scene placement, runtime UI/audio implementation, final native localization, final numeric balancing or `static_data.h8bin` bake.
