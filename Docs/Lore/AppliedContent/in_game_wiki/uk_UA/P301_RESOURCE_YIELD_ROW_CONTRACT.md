---
packet_id: P301_RESOURCE_YIELD_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.resource_yield_row_contract
unlock_id: unlock.resource_yield_row_contract
poi_tags: poi.resource_yield_schema_card;poi.sample_pressure_label
biome_tags: biome.authoring;biome.resource
locale: uk_UA
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "Межа даних ресурсної віддачі"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Межа даних ресурсної віддачі

Межа даних не дає цінам ресурсів стати вільною lore-балаканиною. На HECTON-8 мінерал не має однакової вартості на кожній глибині: історія тиску, маршрутне зберігання і виснаження вирішують, чи є зразок валютою, доказом або зараженим баластом.

## Scanner

Рядок віддачі відкидає голу ціну: клас, тиск, зберігання, виснаження і hash мають збігтися.

## Terminal

RESOURCE YIELD CONTRACT: жодне число не приймається без packet hash, класу ресурсу, діапазону тиску, рангу зберігання, кривої рідкісності й поведінки виснаження. Зразок без історії тиску є доказом, а не цінністю.

## Audio

Зразок без історії тиску не має ціни.

## Field Note

Числа yield лишаються попередніми, доки pressure band, custody grade, depletion behavior і packet hash не узгоджені.

<!-- In-Game Wiki; generated from P301_RESOURCE_YIELD_ROW_CONTRACT/uk_UA. -->
