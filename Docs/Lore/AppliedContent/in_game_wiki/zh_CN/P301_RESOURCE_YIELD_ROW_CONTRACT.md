---
packet_id: P301_RESOURCE_YIELD_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.resource_yield_row_contract
unlock_id: unlock.resource_yield_row_contract
poi_tags: poi.resource_yield_schema_card;poi.sample_pressure_label
biome_tags: biome.authoring;biome.resource
locale: zh_CN
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "资源产出数据边界"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# 资源产出数据边界

资源产出数据边界防止资源价格变成松散的设定话。 在 HECTON-8，矿物在每个深度并不等值：压力历史、路线监管和枯竭行为决定样本是货币、证据，还是受污染压舱物。

## Scanner

产出行拒绝松散价值：类别、压力带、监管、枯竭和 hash 必须一致。

## Terminal

RESOURCE YIELD CONTRACT：没有 packet hash、资源类别、压力带、监管等级、稀有度曲线和枯竭行为，任何数字都不被接受。没有压力历史的样本是证据，不是价值。

## Audio

没有压力历史的样本没有价值。

## Field Note

在 pressure band、custody grade、depletion behavior 和 packet hash 一致前，yield 数字保持临时状态。

<!-- In-Game Wiki; generated from P301_RESOURCE_YIELD_ROW_CONTRACT/zh_CN. -->
