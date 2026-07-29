---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: pl_PL
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Protokół natywnej lokalizacji imion"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Protokół natywnej lokalizacji imion

Natywna lokalizacja imion chroni warstwę dowodów przed wypadkiem interfejsu. Gracz nie powinien widzieć imion zgniecionych, odwróconych, półprzetłumaczonych przez fallback ani zastąpionych angielskim debugiem.

Zasada jest prosta: tożsamość pisze się per locale, a systemy wokół tłumaczy normalnie. Pasek imienia ma pozostać celowym artefaktem. Jeśli język potrzebuje krótkiej formy, powstaje ona wcześniej i jest baked.

## Scanner

NAME LOC // Ten pasek jest napisany, nie tłumaczony na żywo. Osoba przetrwa interfejs tylko wtedy, gdy interfejs przestanie improwizować.

## Terminal

LOKALIZACJA IMION // Imiona, krótkie paski i fragmenty identyfikatora są baked per locale. Stanowiska, działy, zgody tras i notatki zmian tłumaczą się wokół nich. RTL i CJK wymagają autorskich skrótów, bezpiecznych łamań i braku live recomposition w skanerze, UI szafek, terminalach i wiki.

## Audio

Imię łamiące UI nie jest szacunkiem. To kolonia usuwająca pracownika po raz drugi.

## Field Note

Nie pozwól, by runtime fallback przemianował martwego pracownika. Zepsute imię to kolejne wymazanie.

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/pl_PL. -->
