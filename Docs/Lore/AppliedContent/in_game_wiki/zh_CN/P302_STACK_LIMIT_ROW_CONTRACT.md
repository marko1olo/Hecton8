---
packet_id: P302_STACK_LIMIT_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.stack_limit_row_contract
unlock_id: unlock.stack_limit_row_contract
poi_tags: poi.stack_limit_schema_card;poi.pressure_vessel_rack
biome_tags: biome.inventory;biome.resource_custody
locale: zh_CN
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "压力堆叠限制行"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P262_INVENTORY_STACK_AUTHORING_ROWS
---

# 压力堆叠限制行

堆叠限制是实物货运规则。两个工具可以共用一个箱；两个压力样本如果一个封条开裂，或一个标签带有Atlas兼容噪声，就不能共用同一段记录。Black Keel按穿过轨道窗口的质量收费，并拒绝会让清单在封存状态上说谎的堆叠。

## Scanner

堆叠请求被拒：容器类别、压力额定、污染阶段、质量窗口收费和清单身份不匹配。

## Terminal

堆叠接收模式 / Black Keel货运台：堆叠数量需要容器类型、额定压力、封条证书、污染阶段、质量类别、警告级别、留置质量窗口和稳定清单身份。箱标不等于封存认证。

## Audio

货运台：堆叠被拒。箱子额定用于吊装，不用于压力保管。

## Field Note

不要为了省空间堆叠未知样本。一个坏封条会让整堆变成检疫货物，承运方照样按质量收费。

<!-- In-Game Wiki; generated from P302_STACK_LIMIT_ROW_CONTRACT/zh_CN. -->
