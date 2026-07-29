---
packet_id: P202_PACKET_NOTARY_INTERFACE_SIGNATURES
release_set_id: RS041_DEEP_REACH_LOWER_SIGNATURES
article_id: deep_reach.packet_notary_interface_signatures
unlock_id: unlock.packet_notary_interface_signatures
poi_tags: poi.packet_notary_seal;poi.relay_delay_stamp
biome_tags: biome.relay_spine;biome.claim_admin
locale: ar_SA
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "توقيعات واجهة توثيق الحزم"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: rtl
localization_status: draft_machine_or_llm
localization_flags: 1
next_packet_ids: P471_LUYTEN_PACKET_CUSTODY_RELAY_BRIDGE
---

# توقيعات واجهة توثيق الحزم

شريط Packet Notary المستعاد هو أول سجل من مكتب سفلي يجعل الرسالة صالحة كدليل بدلا من شائعة. يربط ثلاثة عناصر: packet hash، وقت نافذة الترحيل، ومالك الحفظ الذي لمس السجل. كان بوسع Deep Reach دفن سجل نظيف بوصفه ضجيج ناقل غير موثق؛ واجهة التوثيق تجعل ذلك أصعب فقط عندما يبقى witness hash ثان. الختم أداة لسلسلة الحفظ، لا اعتراف. توقيع Som Varela يصدق وقت المسار وحالة الحفظ. لا يثبت سبب تأخير الحزمة ولا يسمي الشخص الذي أمر بالتأخير.

## Scanner

تمت استعادة ختم الحزمة: شريط التجزئة سليم، نافذة الترحيل 17-A، ومالك الحفظ غير محسوم. يعامل كدليل فقط بعد تطابق witness chain.

## Terminal

SIGNATURE SEED: Som Varela, Packet Notary Interface. Route: Relay Spine / witness hash strip. Action: seal packet hash, local relay delay, custody owner. Exception: missing worker-name attachment keeps packet in claim-material queue. Escalation: public ledger only after second witness hash.

## Audio

الختم سليم. الطابع الزمني متأخر نافذتين. إذا طابق witness hash فلن يستطيعوا تسميته تشويشا.

## Field Note

لا تبعه كسجل. بعه كساعة ومعها شاهد: وقت الترحيل، packet hash، ومالك الحفظ. من دون الحقول الثلاثة ستسميه Deep Reach ضجيج ناقل مفكوك.

<!-- In-Game Wiki; generated from P202_PACKET_NOTARY_INTERFACE_SIGNATURES/ar_SA. -->
