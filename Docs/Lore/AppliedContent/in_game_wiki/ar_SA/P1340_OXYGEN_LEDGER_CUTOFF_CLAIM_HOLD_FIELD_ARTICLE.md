---
packet_id: P1340_OXYGEN_LEDGER_CUTOFF_CLAIM_HOLD_FIELD_ARTICLE
release_set_id: RS292_OXYGEN_LEDGER_CUTOFF_CLAIM_HOLD_FIELD_ARTICLE
article_id: applied_lore.oxygen_ledger_cutoff_claim_hold_field_article
unlock_id: unlock.oxygen_ledger_cutoff_claim_hold_field_article
poi_tags: poi.oxygen_ledger_cutoff;poi.scrubber_cutoff_row
biome_tags: biome.drowned_colony;biome.pressure_base
locale: ar_SA
surface: in_game_wiki
source_voice: PDA Forensic Object Article
spoiler_tier: 
title: "قطع سجل الأكسجين وتعليق المطالبة"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: rtl
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1339_MISSING_RETURN_MARK_CLAIM_CONVERSION_FIELD_ARTICLE
next_packet_ids: P1341_SUIT_RESERVE_DELTA_MISMATCH_FIELD_ARTICLE
---

# قطع سجل الأكسجين وتعليق المطالبة

سجل الأكسجين حساب دعم حياة. يسجل زمن خرطوشة المرشح، فرق احتياطي البدلة، تسليم مضخة الغرفة، تغذية الضاغط وسلطة القطع. عندما يتوقف ذلك الصف، تكون الغرفة قد توقفت عن دعم التنفس. لا يثبت ثانية الوفاة؛ يثبت آخر نقطة قبلت فيها الغرفة حمل هواء.

يصبح claim hold مشبوهاً عندما يعيش بعد صف الأكسجين. يمكن لمكتب المسار أن يترك العامل نشطاً، ويمكن لـ claim desk أن يمسك سلطة الاسترداد، لكن لا خانة منهما تضيف هواء إلى الغرفة. إذا توقف الأكسجين أولاً وبقيت علامة العودة فارغة، فحالة العامل النشطة تصبح ورقاً فوق مسار جسد مغلق.

التناقض المفيد يقع بين أربعة أشياء: قطع المرشح، وجود وسم البدلة، ختم إذن المسار، واستثناء claim لاحق. إذا بقي المسار مفتوحاً بعد قطع السجل، تستطيع Deep Reach احتساب التأخير، أو إبقاء سلطة salvage، أو إعادة تصنيف الحامل ككتلة قابلة للاسترداد مع تجنب إغلاق خسارة نظيف.

التعامل الميداني محدود. احفظ شريط السجل قبل شطف الملح. صور صف القطع، رقم خرطوشة المرشح، فرق احتياطي البدلة وأي ختم يدوي يعبر الملح. لا تدع حالة claim تمحو حد دعم الحياة.

## Scanner

قطع سجل الأكسجين // يتوقف صف المرشح قبل إغلاق عودة المسار. يبقي claim hold العامل نشطاً بعد أن توقفت الغرفة عن دعم التنفس.

## Terminal

فحص سجل الأكسجين // قارن قطع المرشح، فرق احتياطي البدلة، تسليم الضاغط، خانة العودة وclaim hold. توقف الأكسجين حد دعم حياة، وليس إغلاق مسار.

## Audio

صف المرشح ميت قبل إغلاق المسار. أبقى claim desk الاسم نشطاً.

## Field Note

إذا توقف الأكسجين أولاً، فالمسار المفتوح ورق لا هواء.

<!-- In-Game Wiki; generated from P1340_OXYGEN_LEDGER_CUTOFF_CLAIM_HOLD_FIELD_ARTICLE/ar_SA. -->
