---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: ja_JP
surface: external_site
source_voice: Website Public
spoiler_tier: 
title: "固有名ローカライズ・プロトコル"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
---

# 固有名ローカライズ・プロトコル

固有名ローカライズ・プロトコルは、15 言語で作業員の身元を守る方法を定義する。個人名、バッジ表示、短縮表示は locale ごとに作成して bake し、職名、部署、シフト役割、経路許可、スキャナー語彙は別に翻訳する。名前は証拠オブジェクトであり、runtime のライブ翻訳に依存してはならない。

## Scanner

NAME LOC // この名札は手作業で書かれたもので、ライブ翻訳ではない。インターフェースが即興をやめた時だけ、その人は画面上で生き残る。

## Terminal

NAME LOCALIZATION // 個人名、短い名札、バッジ片は locale ごとに bake する。職名、部署、経路許可、シフトメモはその周囲で翻訳する。RTL と CJK では短縮形、改行安全な名札、スキャナー、ロッカー UI、端末、外部 wiki でのライブ再合成禁止が必要。

## Audio

UIを壊す名前は敬意ではない。作業員を二度消すことだ。

## Field Note

死亡した作業員を runtime fallback に改名させてはならない。壊れた名前はもう一つの抹消だ。

<!-- External Site; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/ja_JP. -->
