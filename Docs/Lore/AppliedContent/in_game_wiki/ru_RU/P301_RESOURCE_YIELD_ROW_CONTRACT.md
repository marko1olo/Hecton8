---
packet_id: P301_RESOURCE_YIELD_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.resource_yield_row_contract
unlock_id: unlock.resource_yield_row_contract
poi_tags: poi.resource_yield_schema_card;poi.sample_pressure_label
biome_tags: biome.authoring;biome.resource
locale: ru_RU
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "Граница данных ресурсной отдачи"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Граница данных ресурсной отдачи

Граница данных не дает ценам ресурсов стать свободной болтовней лора. На HECTON-8 минерал не стоит одинаково на любой глубине: история давления, маршрутное хранение и истощение решают, является ли образец валютой, доказательством или зараженным балластом.

## Scanner

Строка отдачи отвергает голую цену: класс, давление, хранение, истощение и hash должны совпасть.

## Terminal

КОНТРАКТ РЕСУРСНОЙ ОТДАЧИ: число не принимается без packet hash, класса ресурса, диапазона давления, ранга хранения, кривой редкости и поведения истощения. Образец без истории давления — доказательство, а не ценность.

## Audio

Образец без истории давления не имеет цены.

## Field Note

Числа yield остаются предварительными, пока pressure band, custody grade, depletion behavior и packet hash не согласованы.

<!-- In-Game Wiki; generated from P301_RESOURCE_YIELD_ROW_CONTRACT/ru_RU. -->
