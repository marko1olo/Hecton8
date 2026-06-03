# Localization Glossary Audio Style

Status: production-facing draft pending native localization.

Lock terminology, units, voice registers and transcript style for localization and audio surfaces.

## Packets

- `P256_PROPER_NOUN_TRANSLATION_LOCK_TABLE` - Proper Noun Translation Lock Table: Proper Noun Translation Lock Table defines which HECTON-8 terms remain stable across localization. Names, IDs and claim brands preserve identity; surrounding explanations carry the local language.
- `P257_UNIT_NUMBER_STYLE_CARD` - Unit And Number Style Card: Unit And Number Style Card defines how HECTON-8 handles depth, transit, pressure and debt values in localized articles and UI surfaces.
- `P258_TERMINAL_VOICE_REGISTER_RULE` - Terminal Voice Register Rule: Terminal Voice Register Rule separates HECTON-8's text surfaces: corporate packets are cold, Marauder notes are practical, Atlas traces are category errors.
- `P259_AUDIO_BARK_FAMILY_RULES` - Audio Bark Family Rules: Audio Bark Family Rules lock the voice strategy for HECTON-8: clipped carrier messages, sanitized corporate packets, practical Marauder corrections and Atlas maintenance traces that misname living context.
- `P260_RTL_CJK_FONT_RISK_CARD` - RTL And CJK Font Risk Card: RTL And CJK Font Risk Card defines the localization proof needed before HECTON-8's multilingual pages and in-game wiki can be called release-ready.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
