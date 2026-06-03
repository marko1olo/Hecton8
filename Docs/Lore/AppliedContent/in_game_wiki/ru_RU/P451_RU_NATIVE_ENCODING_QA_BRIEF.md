---
packet_id: P451_RU_NATIVE_ENCODING_QA_BRIEF
release_set_id: RS091_NATIVE_LOCALIZATION_AND_ACCESSIBILITY_QA_BRIEFS
article_id: applied_lore.ru_native_encoding_qa_brief
unlock_id: unlock.ru_native_encoding_qa_brief
poi_tags: poi.ru_native_encoding_qa;poi.mojibake_guard
biome_tags: biome.localization;biome.qa
locale: ru_RU
surface: in_game_wiki
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: source_ready
localization_flags: 0
---

# RU native encoding QA brief

Русский source может нести рабочий тон проекта, но release rows требуют чистую native phrasing, стабильные units и отсутствие encoding damage.

## Scanner

Localization QA: русские rows требуют native pass, UTF-8 source proof и rejection mojibake до publication lock.

## Terminal

RU QA: reject broken encoding, mixed register drift, overlong terminal copy и untranslated gameplay units.

## Audio

Localization note: битый текст - производственный дефект, а не flavor corruption.

## Field Note

QA gate: сравнить packet JSON, exported CSV, wiki page и site page bytes как UTF-8. Если любая поверхность displays mojibake, block publication and fix source generation.

<!-- In-Game Wiki; generated from P451_RU_NATIVE_ENCODING_QA_BRIEF/ru_RU. -->
