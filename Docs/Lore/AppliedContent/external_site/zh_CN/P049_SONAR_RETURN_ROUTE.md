---
packet_id: P049_SONAR_RETURN_ROUTE
release_set_id: RS010_PRESSURE_MACHINERY_RETURN_ROUTE
article_id: hecton8.sonar_return_route
unlock_id: unlock.first_sonar_return_route
poi_tags: poi.sonar_pylon;poi.return_beacon
biome_tags: biome.shallow_wreck;biome.service_canyon
locale: zh_CN
surface: external_site
source_voice: Website Public
spoiler_tier: 1
title: "声呐返程路线"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P045_BLACK_BOX_NAME_STACK
---

# 声呐返程路线

Sonar return routes让navigation成为持续任务：stale beacons可能指向right corridor，同时隐藏new silt、obstruction drift、fauna movement或pressure-door changes。

## Scanner

return beacon stale。old safe ping不再匹配corridor echo；cargo mass会拖慢retreat window。

## Terminal

RETURN ROUTE SONAR / BEACON R-09: last clean echo invalid。Black Keel ping received at low confidence。Obstruction drift、silt density和cargo mass exceed map tolerance。extraction前mark secondary line。

## Audio

route仍在那里。echo已经不同。

## Field Note

loading之前先ping。如果way home在hands empty时changed，它不会原谅full pack。

<!-- External Site; generated from P049_SONAR_RETURN_ROUTE/zh_CN. -->
