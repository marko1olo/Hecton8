---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: en_US
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Native Name Localization Protocol"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: source_authority
localization_flags: 0
prereq_packet_ids: P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
---

# Native Name Localization Protocol

Native name localization protects the worker evidence layer from becoming an interface accident. The player should never see a personal name crushed into a box, reversed into nonsense, half-translated by fallback code or replaced by an English debug remnant.

The rule is simple: personal identity is authored per locale, while the systems around it translate normally. Job titles, departments, route permissions and route notes may change language; the worker's name strip must remain a deliberate artifact. If a language needs a shorter form for a badge, that shorter form is written and baked, not invented at runtime.

This matters because HECTON-8 uses names as evidence. A locker, job board or medlock refusal can only carry human weight if the name is treated as a physical record. The UI must fit the record; the record must not be cut down to hide a UI failure.

## Scanner

NAME LOC // This strip is authored, not live-translated. The person survives the interface only if the interface stops improvising.

## Terminal

NAME LOCALIZATION // Personal names, short name strips and badge fragments are baked per locale. Job titles, departments, route permissions and shift notes localize around them. RTL and CJK builds require authored short forms, line-break-safe name strips and no live recomposition in the scanner, locker UI, terminals or external wiki exports.

## Audio

A name that breaks the UI is not respect. It is the colony deleting the worker twice.

## Field Note

Never let a runtime fallback rename a dead worker. A broken name is another form of erasure.

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/en_US. -->
