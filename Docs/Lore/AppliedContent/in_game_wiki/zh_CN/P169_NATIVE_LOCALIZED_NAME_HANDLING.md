---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: zh_CN
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "原生姓名本地化协议"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
---

# 原生姓名本地化协议

原生姓名本地化保护工人证据层不变成界面事故。玩家不应看到被挤扁的名字、方向错乱的名字、被 fallback 代码半翻译的名字，或残留的英文 debug 名。

规则很简单：个人身份按语言预写，周围系统正常翻译。岗位、部门、路线许可和班次备注可以换语言；工人的姓名条必须保持为有意制作的物证。若某种语言需要工牌短名，就提前写好并 bake，而不是 runtime 临时生成。

这很重要，因为 HECTON-8 把名字当证据。储物柜、岗位板或 medlock 拒绝单只有在姓名像物理记录时才有人味。UI 应该适配记录，而不是裁掉记录来掩盖 UI 失败。

## Scanner

姓名本地化 // 这条姓名不是实时翻译，而是预先编写。只有界面停止即兴，人才不会在界面里再次消失。

## Terminal

姓名本地化 // 个人名、短姓名条和工牌碎片按 locale bake。岗位、部门、路线许可和班次备注围绕它们本地化。RTL 与 CJK 构建需要预写短形式、安全换行姓名条，并禁止扫描器、储物柜 UI、终端和外部 wiki 在 runtime 重组姓名。

## Audio

一个会弄坏 UI 的名字不是尊重，而是殖民地第二次删除工人。

## Field Note

不要让 runtime fallback 给死去的工人改名。坏掉的名字也是一种抹除。

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/zh_CN. -->
