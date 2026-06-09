---
packet_id: P301_RESOURCE_YIELD_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.resource_yield_row_contract
unlock_id: unlock.resource_yield_row_contract
poi_tags: poi.resource_yield_schema_card;poi.sample_pressure_label
biome_tags: biome.authoring;biome.resource
locale: ja_JP
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "資源産出データ境界"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# 資源産出データ境界

資源産出データ境界は、資源価格が緩い lore になるのを防ぐ。HECTON-8 では、鉱物はどの深度でも同じ価値を持つわけではない。圧力履歴、ルート保管、枯渇挙動が、それを通貨、証拠、汚染バラストのどれにするかを決める。

## Scanner

産出行は裸の価値を拒否する: 分類、圧力帯、保管、枯渇、hash が一致しなければならない。

## Terminal

RESOURCE YIELD CONTRACT: packet hash、資源分類、圧力帯、保管等級、希少度曲線、枯渇挙動なしに数値は受理されない。圧力履歴のないサンプルは価値ではなく証拠。

## Audio

圧力履歴のないサンプルに価値はない。

## Field Note

産出数値は、圧力帯、保管等級、枯渇挙動、packet hash が一致するまで暫定。

<!-- In-Game Wiki; generated from P301_RESOURCE_YIELD_ROW_CONTRACT/ja_JP. -->
