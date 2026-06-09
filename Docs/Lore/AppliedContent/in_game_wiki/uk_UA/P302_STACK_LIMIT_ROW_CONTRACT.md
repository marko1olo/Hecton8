---
packet_id: P302_STACK_LIMIT_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.stack_limit_row_contract
unlock_id: unlock.stack_limit_row_contract
poi_tags: poi.stack_limit_schema_card;poi.pressure_vessel_rack
biome_tags: biome.inventory;biome.resource_custody
locale: uk_UA
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "Межа даних ліміту стака"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Межа даних ліміту стака

Межа ліміту робить інвентар фізичним. Предмети складаються лише тоді, коли контейнер, тиск, маса і зараження можуть пережити той самий маршрут, не збрехавши save-файлу.

## Scanner

Рядок стака відкидає купи іконок: посудина, тиск, зараження і маса визначають кількість.

## Terminal

STACK CONTRACT: кількість у stack вимагає тип посудини, рейтинг тиску, стадію зараження, клас маси, рівень попередження і save-stable identity. Ящик не є напірною посудиною.

## Audio

Ящик не є напірною посудиною.

## Field Note

Ліміти stack лишаються table-owned і стабільними для save identity.

<!-- In-Game Wiki; generated from P302_STACK_LIMIT_ROW_CONTRACT/uk_UA. -->
