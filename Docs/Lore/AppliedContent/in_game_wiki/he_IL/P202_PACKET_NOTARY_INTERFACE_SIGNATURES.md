---
packet_id: P202_PACKET_NOTARY_INTERFACE_SIGNATURES
release_set_id: RS041_DEEP_REACH_LOWER_SIGNATURES
article_id: deep_reach.packet_notary_interface_signatures
unlock_id: unlock.packet_notary_interface_signatures
poi_tags: poi.packet_notary_seal;poi.relay_delay_stamp
biome_tags: biome.relay_spine;biome.claim_admin
locale: he_IL
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "חתימות Packet Notary Interface"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: rtl
localization_status: draft_machine_or_llm
localization_flags: 1
---

# חתימות Packet Notary Interface

רצועת Packet Notary ששוחזרה היא רשומת המשרד התחתון הראשונה שהופכת הודעה לראיה שימושית ולא לשמועה. היא קושרת שלושה דברים: packet hash, זמן חלון ממסר ובעל המשמורת שנגע ברשומה. Deep Reach יכלה לקבור יומן נקי כרעש נשא לא מאומת; ממשק התיעוד מקשה על כך רק כשנשאר witness hash שני. החותם הוא כלי של שרשרת משמורת, לא הודאה. החתימה של Som Varela מאשרת זמן מסלול ומצב משמורת. היא לא מוכיחה למה החבילה עוכבה ולא נותנת שם למי שהורה על העיכוב.

## Scanner

חותם חבילה שוחזר: רצועת hash שלמה, חותמת חלון ממסר 17-A, בעל משמורת לא פתור. לטפל כראיה רק אחרי התאמת witness chain.

## Terminal

SIGNATURE SEED: Som Varela, Packet Notary Interface. Route: Relay Spine / witness hash strip. Action: seal packet hash, local relay delay, custody owner. Exception: missing worker-name attachment keeps packet in claim-material queue. Escalation: public ledger only after second witness hash.

## Audio

החותם שלם. חותמת הזמן מאחרת בשני חלונות. אם witness hash יתאים, הם לא יוכלו לקרוא לזה סטטי.

## Field Note

אל תמכור את זה כיומן. תמכור את זה כשעון עם עד: זמן ממסר, packet hash, בעל משמורת. בלי שלושת השדות Deep Reach תקרא לזה רעש נשא רופף.

<!-- In-Game Wiki; generated from P202_PACKET_NOTARY_INTERFACE_SIGNATURES/he_IL. -->
