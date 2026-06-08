---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: ru_RU
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "Нативная обработка локализованных имен"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Нативная обработка локализованных имен

Нативная обработка имен держит worker-evidence layer совместимым с многоязычной wiki, сайтом, шкафчиками и терминалами.

## Scanner

Именам рабочих нужна политика локализации до того, как они станут UI bugs.

## Terminal

NAME LOC: личные имена остаются authored/baked per locale. Должности, отделы и route permissions локализуются. RTL/CJK layouts требуют заранее baked short forms и fallback-safe name strips.

## Audio

Имя, ломающее UI, - не уважение. Это еще одно стирание.

## Field Note

Никакого live translation имен. Личность сохраняется через baked strings.

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/ru_RU. -->
