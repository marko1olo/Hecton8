---
packet_id: P278_RTL_REVIEW_LOCK
release_set_id: RS056_NATIVE_LOCALIZATION_REVIEW_PACK
article_id: applied_lore.rtl_review_lock
unlock_id: unlock.rtl_review_lock
poi_tags: poi.rtl_review_card;poi.arabic_hebrew_sample
biome_tags: biome.localization;biome.rtl
locale: en_US
surface: external_site
source_voice: Website Public
spoiler_tier: 
title: "Right-to-Left Reading Contract"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: source_authority
localization_flags: 0
prereq_packet_ids: P256_PROPER_NOUN_TRANSLATION_LOCK_TABLE
---

# Right-to-Left Reading Contract

HECTON-8 treats RTL support as a real interface contract: the language must survive PDA panels, terminals, scanner labels, subtitles, and web pages before release.

## Scanner

Direction is part of the warning chain, not decoration.

## Terminal

RTL text stays logical in storage and visual in rendering: TMP handles shaping and order; no manual reversal; numbers remain readable.

## Audio

Wrong direction can bury a warning in noise.

## Field Note

Check bidirectional numerals, HECTON-8/Aegir/Atlas names, pressure units, carrier clauses, and subtitle source tags before release.

<!-- External Site; generated from P278_RTL_REVIEW_LOCK/en_US. -->
