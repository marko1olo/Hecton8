---
packet_id: P202_PACKET_NOTARY_INTERFACE_SIGNATURES
release_set_id: RS041_DEEP_REACH_LOWER_SIGNATURES
article_id: deep_reach.packet_notary_interface_signatures
unlock_id: unlock.packet_notary_interface_signatures
poi_tags: poi.packet_notary_seal;poi.relay_delay_stamp
biome_tags: biome.relay_spine;biome.claim_admin
locale: ko_KR
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Packet Notary Interface 서명"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
next_packet_ids: P471_LUYTEN_PACKET_CUSTODY_RELAY_BRIDGE
---

# Packet Notary Interface 서명

회수된 Packet Notary 스트립은 메시지를 소문이 아니라 증거로 쓸 수 있게 만드는 첫 하위 사무 기록이다. 그것은 packet hash, 릴레이 창 시간, 기록을 만진 보관 소유자 세 가지를 묶는다. Deep Reach는 깨끗한 로그도 검증되지 않은 반송파 잡음으로 묻을 수 있었다. notary interface는 두 번째 witness hash가 남아 있을 때만 그 처리를 어렵게 만든다. 봉인은 보관 연쇄 도구이지 자백이 아니다. Som Varela의 서명은 경로 시간과 보관 상태를 인증한다. 패킷이 왜 지연됐는지, 누가 지연을 명령했는지는 증명하지 않는다.

## Scanner

패킷 봉인 회수됨: 해시 스트립 정상, 릴레이 창 표식 17-A, 보관 소유자 미해결. witness chain 일치 후에만 증거로 처리.

## Terminal

SIGNATURE SEED: Som Varela, Packet Notary Interface. Route: Relay Spine / witness hash strip. Action: packet hash, local relay delay, custody owner 봉인. Exception: 작업자 이름 첨부 누락으로 패킷은 claim-material queue에 남음. Escalation: 두 번째 witness hash 이후 public ledger.

## Audio

봉인은 멀쩡하다. 시간 표식은 두 창 늦었다. witness hash가 맞으면 정전기라고 부를 수 없다.

## Field Note

이걸 로그로 팔지 마라. 시계와 증인으로 팔아라. 릴레이 시간, packet hash, 보관 소유자. 셋이 없으면 Deep Reach는 느슨한 반송파 잡음으로 처리한다.

<!-- In-Game Wiki; generated from P202_PACKET_NOTARY_INTERFACE_SIGNATURES/ko_KR. -->
