---
packet_id: P301_RESOURCE_YIELD_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.resource_yield_row_contract
unlock_id: unlock.resource_yield_row_contract
poi_tags: poi.resource_yield_schema_card;poi.sample_pressure_label
biome_tags: biome.authoring;biome.resource
locale: ko_KR
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "자원 산출 데이터 경계"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# 자원 산출 데이터 경계

이 경계는 자원 가격이 느슨한 lore가 되는 것을 막는다. HECTON-8에서 광물은 모든 깊이에서 같은 가치가 아니다. 압력 이력, 경로 보관, 고갈 동작이 샘플을 화폐, 증거, 오염된 밸러스트 중 무엇으로 만들지 결정한다.

## Scanner

산출 행은 느슨한 가치를 거부한다. 등급, 압력대, 보관, 고갈, hash가 맞아야 한다.

## Terminal

RESOURCE YIELD CONTRACT: packet hash, 자원 등급, 압력대, 보관 등급, 희귀도 곡선, 고갈 동작 없이는 어떤 숫자도 수락되지 않는다. 압력 이력이 없는 샘플은 가치가 아니라 증거다.

## Audio

압력 이력 없는 샘플은 가치가 아니다.

## Field Note

압력대, 보관 등급, 고갈 동작, packet hash가 일치할 때까지 산출 숫자는 임시다.

<!-- In-Game Wiki; generated from P301_RESOURCE_YIELD_ROW_CONTRACT/ko_KR. -->
