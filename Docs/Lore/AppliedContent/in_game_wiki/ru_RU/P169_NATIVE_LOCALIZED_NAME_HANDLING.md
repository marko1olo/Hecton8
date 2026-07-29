---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: ru_RU
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Протокол локализации личных имен"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Протокол локализации личных имен

Нативная локализация имен защищает слой рабочих улик от превращения в ошибку интерфейса. Игрок не должен видеть личное имя, сплющенное в коробке, перевернутое в бессмыслицу, наполовину переведенное fallback-кодом или замененное английским debug-остатком.

Правило простое: личность авторится для каждой локали, а системы вокруг нее переводятся обычным образом. Должности, отделы, маршрутные допуски и служебные заметки могут менять язык; именная полоса рабочего должна оставаться намеренным артефактом. Если языку нужна короткая форма для жетона, эта форма пишется и bake-ится заранее, а не рождается в runtime.

Это важно, потому что HECTON-8 использует имена как доказательства. Шкафчик, доска работ или отказ medlock несут человеческий вес только если имя выглядит как физическая запись. UI должен подогнаться под запись; запись нельзя резать, чтобы спрятать провал UI.

## Scanner

ЛОК ИМЕНИ // Эта полоса написана вручную, а не переведена на лету. Человек переживет интерфейс только если интерфейс перестанет импровизировать.

## Terminal

ЛОКАЛИЗАЦИЯ ИМЕН // Личные имена, короткие именные полосы и фрагменты жетонов bake-ятся для каждой локали. Должности, отделы, маршрутные допуски и заметки смены переводятся вокруг них. RTL и CJK сборки требуют авторских коротких форм, безопасных переносов и запрета live-рекомпозиции в сканере, UI шкафчиков, терминалах и внешнем wiki export.

## Audio

Имя, которое ломает UI, не уважение. Это колония удаляет рабочего второй раз.

## Field Note

Нельзя позволять runtime fallback переименовывать мертвого рабочего. Сломанное имя - еще одна форма стирания.

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/ru_RU. -->
