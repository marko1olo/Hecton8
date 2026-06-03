# Publication Spoiler Localization Protocol

Status: production-facing draft pending native localization.

Lock public article tiers, in-game unlock tiers, transcript censorship, art-release gates and native localization backlog rules.

## Packets

- `P216_PUBLIC_SITE_ARTICLE_TIER_RULES` - Public Site Article Tier Rules: Public Site Article Tier Rules defines HECTON-8 publication boundaries for website and external wiki articles.
- `P217_IN_GAME_WIKI_UNLOCK_TIER_RULES` - In-Game Wiki Unlock Tier Rules: In-Game Wiki Unlock Tier Rules describes the evidence-first codex policy for HECTON-8.
- `P218_AUDIO_TRANSCRIPT_CENSOR_RULES` - Audio Transcript Censor Rules: Audio Transcript Censor Rules defines readable damaged-audio policy for HECTON-8.
- `P219_ART_BRIEF_RELEASE_GATE_RULES` - Art Brief Release Gate Rules: Art Brief Release Gate Rules defines publication-safe image requirements for HECTON-8.
- `P220_NATIVE_LANGUAGE_BACKLOG_RULES` - Native Language Backlog Rules: Native Language Backlog Rules defines the HECTON-8 localization backlog for website, wiki and in-game text.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
