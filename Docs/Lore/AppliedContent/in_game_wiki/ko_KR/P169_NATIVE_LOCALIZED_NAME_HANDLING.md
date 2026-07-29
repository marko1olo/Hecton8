---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: ko_KR
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "고유 이름 현지화 프로토콜"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
---

# 고유 이름 현지화 프로토콜

고유 이름 현지화는 작업자 증거 레이어가 인터페이스 사고가 되지 않게 한다. 플레이어는 눌린 이름, 방향이 뒤틀린 이름, fallback 코드가 반만 번역한 이름, 영어 debug 잔재를 보아서는 안 된다.

원칙은 단순하다. 개인 신원은 locale별로 작성하고 주변 시스템은 정상 번역한다. 배지용 짧은 형태가 필요하면 runtime이 만들지 않고 미리 작성해 bake한다.

## Scanner

NAME LOC // 이 스트립은 작성된 것이지 실시간 번역이 아니다. 인터페이스가 즉흥을 멈출 때만 사람은 화면에서 살아남는다.

## Terminal

이름 현지화 // 개인명, 짧은 이름 스트립, 배지 조각은 locale별로 bake한다. 직함, 부서, 경로 허가, 교대 메모는 그 주위에서 번역한다. RTL과 CJK는 작성된 축약형, 안전한 줄바꿈, 스캐너, 사물함 UI, 터미널, 외부 wiki에서 live 재조합 금지가 필요하다.

## Audio

UI를 망가뜨리는 이름은 존중이 아니다. 식민지가 작업자를 두 번 지우는 것이다.

## Field Note

runtime fallback이 죽은 작업자의 이름을 바꾸게 하지 마라. 깨진 이름은 또 다른 삭제다.

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/ko_KR. -->
