---
packet_id: P363_GUIDANCE_TIMING_CORE_ASSEMBLY
release_set_id: RS073_ESCAPE_ASCENT_ENGINEERING_COMPONENTS
article_id: applied_lore.guidance_timing_core_assembly
unlock_id: unlock.guidance_timing_core_assembly
poi_tags: poi.guidance_timing_core;poi.orbit_window_chart
biome_tags: biome.brine_canyon;biome.abyssal_machine_field
locale: zh_CN
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "制导定时核心组件"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P093_ACCESSIBLE_SEAFLOOR_WINDOWS
---

# 制导定时核心组件

制导定时核心防止上升变成一次干净却无处抵达的燃烧。HECTON-8上方不是开放天空。Aegir改变回收几何，中继快门开合，风暴羽流弯折声学和无线交接，Black Keel只在特定时间查看特定航道。

核心不会让胶囊比海更聪明。它给胶囊一只时钟、一段星历、接收航道表，以及足够的漂移修正，使出水事件能被分类。过早的数据包会在carrier栈里变成捕获噪声。过晚的数据包会在航道滚走后抵达。好发动机配坏核心，可能完全按设计发射，却不留下可用的回收申索。

可修复的核心需要四样东西：能抵抗深水延迟的时钟、当前Aegir窗口、正确的Black Keel航道，以及与风暴上方中继链吻合的快门缓存。所以这个组件小而严苛。它不增加推力。它决定推力是否能变成抵达。

## Scanner

空白定时核心 // 无Aegir星历，无Keel航道表， relay shutter缓存为空。胶囊可以干净点火，却仍错过所有接收器。

## Terminal

GUIDANCE CORE: 安装Aegir星历切片、Black Keel接收航道表、月面中继快门缓存和羽流漂移修正。若本地时钟漂移超过custody stamp容差则拒绝。

## Audio

把胶囊指向窗口，不是天空。

## Field Note

从HECTON-8没有简单的向上。只有月影、风暴羽流、接收航道，以及一只必须被相信的时钟。

<!-- In-Game Wiki; generated from P363_GUIDANCE_TIMING_CORE_ASSEMBLY/zh_CN. -->
