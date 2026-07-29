---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: ru_RU
surface: external_site
source_voice: Website Public
spoiler_tier: 
title: "Протокол локализации личных имен"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
---

# Протокол локализации личных имен

Протокол локализации личных имен задает, как HECTON-8 сохраняет личность рабочих на 15 языках игрока. Личные имена, полосы жетонов и компактные варианты отображения авторятся и bake-ятся по локалям. Окружающая лексика переводится отдельно: должности, отделы, роли смены, маршрутные допуски и подписи сканера.

Цель одновременно сценарная и техническая. Рабочего нельзя уважать на английском и калечить на арабском, иврите, японском, корейском, китайском, русском или польском. Поэтому модель локализации рассматривает имена как предметы-доказательства. Они должны помещаться в интерфейс, переживать экспорт в игровую wiki и сайт и не зависеть от live translation в runtime.

## Scanner

ЛОК ИМЕНИ // Эта полоса написана вручную, а не переведена на лету. Человек переживет интерфейс только если интерфейс перестанет импровизировать.

## Terminal

ЛОКАЛИЗАЦИЯ ИМЕН // Личные имена, короткие именные полосы и фрагменты жетонов bake-ятся для каждой локали. Должности, отделы, маршрутные допуски и заметки смены переводятся вокруг них. RTL и CJK сборки требуют авторских коротких форм, безопасных переносов и запрета live-рекомпозиции в сканере, UI шкафчиков, терминалах и внешнем wiki export.

## Audio

Имя, которое ломает UI, не уважение. Это колония удаляет рабочего второй раз.

## Field Note

Нельзя позволять runtime fallback переименовывать мертвого рабочего. Сломанное имя - еще одна форма стирания.

<!-- External Site; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/ru_RU. -->
