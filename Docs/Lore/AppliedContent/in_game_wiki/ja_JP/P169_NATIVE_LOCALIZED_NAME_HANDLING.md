---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: ja_JP
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "固有名ローカライズ・プロトコル"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# 固有名ローカライズ・プロトコル

固有名ローカライズは、作業員証拠レイヤーを UI 事故から守る。プレイヤーは潰れた名前、逆順で意味不明になった名前、fallback コードで半端に訳された名前、英語 debug の残骸を見てはならない。

原則は明確だ。個人の身元は locale ごとに作成し、その周囲の職名、部署、経路許可、作業メモを通常どおり翻訳する。バッジ用に短い形が必要なら、runtime に作らせず、書いて bake する。

HECTON-8 では名前が証拠である。ロッカー、作業板、medlock 拒否は、名前が物理記録として扱われて初めて重みを持つ。UI が記録に合わせるべきで、UI の失敗を隠すために記録を切ってはならない。

## Scanner

NAME LOC // この名札は手作業で書かれたもので、ライブ翻訳ではない。インターフェースが即興をやめた時だけ、その人は画面上で生き残る。

## Terminal

NAME LOCALIZATION // 個人名、短い名札、バッジ片は locale ごとに bake する。職名、部署、経路許可、シフトメモはその周囲で翻訳する。RTL と CJK では短縮形、改行安全な名札、スキャナー、ロッカー UI、端末、外部 wiki でのライブ再合成禁止が必要。

## Audio

UIを壊す名前は敬意ではない。作業員を二度消すことだ。

## Field Note

死亡した作業員を runtime fallback に改名させてはならない。壊れた名前はもう一つの抹消だ。

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/ja_JP. -->
