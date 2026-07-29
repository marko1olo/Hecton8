---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: ar_SA
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "بروتوكول توطين الأسماء الأصلي"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: rtl
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
---

# بروتوكول توطين الأسماء الأصلي

يحمي توطين الأسماء الأصلي طبقة أدلة العمال من أن تصبح حادث واجهة. يجب ألا يرى اللاعب اسما مسحوقا، أو معكوسا بلا معنى، أو نصف مترجم بواسطة fallback، أو مستبدلا ببقايا debug إنجليزية.

القاعدة بسيطة: الهوية الشخصية تؤلف لكل لغة، والأنظمة حولها تترجم طبيعيا. إن احتاجت لغة صيغة قصيرة للشارة، تكتب وتخبز مسبقا، لا تخترع في runtime.

## Scanner

NAME LOC // هذا الشريط مؤلف، لا مترجم مباشرة. ينجو الشخص داخل الواجهة فقط عندما تتوقف الواجهة عن الارتجال.

## Terminal

توطين الأسماء // الأسماء الشخصية والشرائط القصيرة وأجزاء الشارة baked لكل locale. الوظائف والأقسام وتصاريح المسار وملاحظات الوردية تترجم حولها. تحتاج RTL وCJK إلى صيغ قصيرة مؤلفة، كسور أسطر آمنة، ومنع إعادة تركيب live في الماسح وواجهة الخزان وال terminals والويكي الخارجي.

## Audio

اسم يكسر الواجهة ليس احتراما. إنه حذف العامل مرة ثانية.

## Field Note

لا تسمح لـ runtime fallback بإعادة تسمية عامل ميت. الاسم المكسور شكل آخر من المحو.

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/ar_SA. -->
