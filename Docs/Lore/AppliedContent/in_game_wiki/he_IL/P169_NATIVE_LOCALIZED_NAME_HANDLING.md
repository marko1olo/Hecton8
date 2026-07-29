---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: he_IL
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "פרוטוקול לוקליזציה טבעית של שמות"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: rtl
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
---

# פרוטוקול לוקליזציה טבעית של שמות

לוקליזציה טבעית של שמות מגינה על שכבת ראיות העובדים מתאונת ממשק. השחקן לא צריך לראות שם מעוך, הפוך לשטות, חצי מתורגם על ידי fallback או מוחלף בשארית debug אנגלית.

הכלל פשוט: זהות אישית נכתבת לכל locale, והמערכות סביב מתורגמות רגיל. אם שפה צריכה צורה קצרה לתג, היא נכתבת ו-baked מראש, לא מומצאת בזמן runtime.

## Scanner

NAME LOC // הרצועה הזאת נכתבה, לא תורגמה בזמן אמת. האדם שורד בממשק רק אם הממשק מפסיק לאלתר.

## Terminal

לוקליזציית שמות // שמות אישיים, רצועות קצרות ושברי תג baked לכל locale. תפקידים, מחלקות, היתרי מסלול והערות משמרת מתורגמים סביבם. RTL ו-CJK דורשים קיצורים כתובים, שבירות שורה בטוחות, וללא recomposition live בסורק, UI ארוניות, מסופים או wiki חיצוני.

## Audio

שם ששובר UI אינו כבוד. זו המושבה שמוחקת את העובד בפעם השנייה.

## Field Note

אל תיתן ל-runtime fallback לשנות שם של עובד מת. שם שבור הוא עוד מחיקה.

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/he_IL. -->
