---
packet_id: P301_RESOURCE_YIELD_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.resource_yield_row_contract
unlock_id: unlock.resource_yield_row_contract
poi_tags: poi.resource_yield_schema_card;poi.sample_pressure_label
biome_tags: biome.authoring;biome.resource
locale: pl_PL
surface: external_site
source_voice: Website Public
spoiler_tier: 0
title: "Granica danych wydajności zasobu"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Granica danych wydajności zasobu

Wartość zasobu w HECTON-8 to łańcuch, nie etykieta. Tabela posiada liczbę, ale fikcja posiada powód jej istnienia: kto wziął próbkę, pod jakim ciśnieniem, z jakim stemplem dozoru i ile tej żyły trasa może jeszcze bezpiecznie zerwać.

## Scanner

Wiersz wydajności odrzuca luźną wartość: klasa, pasmo ciśnienia, dozór, wyczerpanie i hash muszą się zgadzać.

## Terminal

RESOURCE YIELD CONTRACT: żadna liczba nie jest przyjęta bez packet hash, klasy zasobu, pasma ciśnienia, stopnia dozoru, krzywej rzadkości i zachowania wyczerpania. Próbka bez historii ciśnienia jest dowodem, nie wartością.

## Audio

Próbka bez historii ciśnienia nie jest wartością.

## Field Note

Liczby yield pozostają tymczasowe, aż pressure band, custody grade, depletion behavior i packet hash będą zgodne.

<!-- External Site; generated from P301_RESOURCE_YIELD_ROW_CONTRACT/pl_PL. -->
