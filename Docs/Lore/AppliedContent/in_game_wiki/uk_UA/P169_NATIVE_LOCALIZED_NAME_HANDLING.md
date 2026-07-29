---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: uk_UA
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Протокол нативної локалізації імен"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
---

# Протокол нативної локалізації імен

Нативна локалізація імен захищає шар робочих доказів від аварії інтерфейсу. Гравець не має бачити стиснуте ім'я, перевернуте безглуздя, напівпереклад fallback-кодом або англійський debug-залишок.

Правило просте: особистість авториться для локалі, а системи навколо перекладаються нормально. Іменна смуга лишається навмисним артефактом. Якщо мові потрібна коротка форма для бейджа, її пишуть і bake-ять заздалегідь.

## Scanner

ЛОК ІМЕНІ // Ця смуга написана вручну, а не перекладена наживо. Людина переживе інтерфейс лише якщо інтерфейс перестане імпровізувати.

## Terminal

ЛОКАЛІЗАЦІЯ ІМЕН // Особові імена, короткі смуги й фрагменти бейджів bake-яться для кожної локалі. Посади, відділи, маршрутні дозволи й нотатки зміни перекладаються довкола них. RTL і CJK потребують авторських коротких форм, безпечних переносів і заборони live-рекомпозиції в сканері, UI шафок, терміналах і зовнішній wiki.

## Audio

Ім'я, що ламає UI, не є повагою. Це колонія видаляє працівника вдруге.

## Field Note

Не дозволяй runtime fallback перейменовувати мертвого працівника. Зламане ім'я - ще одна форма стирання.

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/uk_UA. -->
