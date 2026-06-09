---
packet_id: P302_STACK_LIMIT_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.stack_limit_row_contract
unlock_id: unlock.stack_limit_row_contract
poi_tags: poi.stack_limit_schema_card;poi.pressure_vessel_rack
biome_tags: biome.inventory;biome.resource_custody
locale: ru_RU
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "Граница данных лимита стака"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Граница данных лимита стака

Граница лимита делает инвентарь физическим. Предметы складываются только тогда, когда контейнер, давление, масса и заражение могут пережить один маршрут, не солгав save-файлу.

## Scanner

Строка стака отвергает кучи иконок: сосуд, давление, заражение и масса решают количество.

## Terminal

КОНТРАКТ СТАКА: количество требует тип сосуда, рейтинг давления, стадию заражения, класс массы, уровень предупреждения и save-stable identity. Ящик не является напорным сосудом.

## Audio

Ящик не является напорным сосудом.

## Field Note

Лимиты стака остаются table-owned и стабильными для save identity.

<!-- In-Game Wiki; generated from P302_STACK_LIMIT_ROW_CONTRACT/ru_RU. -->
