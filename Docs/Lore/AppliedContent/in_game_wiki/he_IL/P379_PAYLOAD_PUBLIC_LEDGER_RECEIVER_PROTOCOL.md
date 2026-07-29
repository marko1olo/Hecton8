---
packet_id: P379_PAYLOAD_PUBLIC_LEDGER_RECEIVER_PROTOCOL
release_set_id: RS076_ATLAS_FINAL_PAYLOAD_RECEIVER_PROTOCOLS
article_id: applied_lore.payload_public_ledger_receiver_protocol
unlock_id: unlock.payload_public_ledger_receiver_protocol
poi_tags: poi.public_ledger_uplink;poi.witness_hash_stack
biome_tags: biome.atlas_basin;biome.claim_route
locale: he_IL
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "פרוטוקול מקבל ספר ציבורי payload"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: rtl
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P092_GLOBAL_OCEAN_DEPTH_BANDS
---

# פרוטוקול מקבל ספר ציבורי payload

פרוטוקול הספר הציבורי שולח ראיות למקום שבו Deep Reach אינה יכולה להחזיק בהן בשקט. הוא משחיר קואורדינטות, חותם attestation hashes, מצרף משמורת מסלול ו-digest שקלול Atlas, ואז נכנס לממסר מושהה. השולח מאבד שליטה בקבלה. קשה יותר לקנות את החבילה, קשה יותר לקבור אותה ואיטי יותר להשתמש בה.

## Scanner

מסלול ספר ציבורי חמוש. קואורדינטות מושחרות לפני ממסר מושהה; ערימת attestation hash ושרשרת אחריות נחתמות בקבלה.

## Terminal

מקבל PAYLOAD // נתיב ספר ציבורי. מקבל: ספר ציבורי מבוזר, נתיב Tau מושהה. קבל חבילת אירוע עם קואורדינטות מושחרות, attestation hashes, משמורת מסלול, digest שקלול Atlas, חותמת זמן מקבל. דחה תשלום פרטי ותביעת שחזור ישירה. חלון משיכה נסגר בקבלת ממסר.

## Audio

הספר לקח את החבילה. הקואורדינטות מוסתרות. חלון המשיכה נסגר.

## Field Note

הערת בוזז: השחר את המסלול לפני שהממסר מקבל. אחרי הקבלה, החבילה כבר לא שלך.

<!-- In-Game Wiki; generated from P379_PAYLOAD_PUBLIC_LEDGER_RECEIVER_PROTOCOL/he_IL. -->
