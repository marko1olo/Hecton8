---
packet_id: P770_EXTERNAL_REVIEW_HOLD_TIMER
release_set_id: RS274_EXTERNAL_REVIEW_HOLD_TIMER
article_id: applied_lore.external_review_hold_timer
unlock_id: unlock.external_review_hold_timer
poi_tags: poi.external_review;poi.hold_timer
biome_tags: biome.public_archive;biome.black_keel_tender
locale: en_US
surface: in_game_wiki
source_voice: Hold Timer Scan
spoiler_tier: 2
title: "External Review Hold Timer"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: source_authority
localization_flags: 0
---

# External Review Hold Timer

The hold timer counts review delay in clean segments: intake, custody, medical, hazard, receiver. The labels look procedural until their overlap is mapped. Each segment can pause the next while claiming to wait for the previous.

## Scanner

HOLD TIMER // Five review segments overlap. Delay loop possible.

## Terminal

EXTERNAL REVIEW // Break loop by assigning one active owner before timer renewal.

## Audio

Timer tick: nobody is late if every office is waiting.

## Field Note

Delay becomes architecture when enough rooms share it.

<!-- In-Game Wiki; generated from P770_EXTERNAL_REVIEW_HOLD_TIMER/en_US. -->
