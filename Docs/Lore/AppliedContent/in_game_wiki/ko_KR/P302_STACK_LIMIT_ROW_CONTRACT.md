---
packet_id: P302_STACK_LIMIT_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.stack_limit_row_contract
unlock_id: unlock.stack_limit_row_contract
poi_tags: poi.stack_limit_schema_card;poi.pressure_vessel_rack
biome_tags: biome.inventory;biome.resource_custody
locale: ko_KR
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "스택 제한 데이터 경계"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# 스택 제한 데이터 경계

이 경계는 인벤토리를 물리적으로 유지한다. 컨테이너, 압력 등급, 질량, 오염 상태가 같은 경로를 견디고 저장 파일에 거짓말하지 않을 때만 아이템은 스택된다.

## Scanner

스택 행은 아이콘 더미를 거부한다. 용기 등급, 압력 등급, 오염, 질량이 수량을 결정한다.

## Terminal

STACK CONTRACT: 스택 수는 용기 유형, 압력 등급, 오염 단계, 질량 등급, 경고 티어, save-stable identity를 요구한다. 상자는 압력 용기가 아니다.

## Audio

상자는 압력 용기가 아니다.

## Field Note

스택 제한은 table-owned로 유지되고 save identity에 안정적이어야 한다.

<!-- In-Game Wiki; generated from P302_STACK_LIMIT_ROW_CONTRACT/ko_KR. -->
