---
packet_id: P301_RESOURCE_YIELD_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.resource_yield_row_contract
unlock_id: unlock.resource_yield_row_contract
poi_tags: poi.resource_yield_schema_card;poi.sample_pressure_label
biome_tags: biome.authoring;biome.resource
locale: fr_FR
surface: external_site
source_voice: Website Public
spoiler_tier: 0
title: "Frontière de données du rendement de ressource"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Frontière de données du rendement de ressource

La valeur d'une ressource dans HECTON-8 est une chaîne, pas une étiquette. La table possède le nombre, mais la fiction possède sa raison : qui a pris l'échantillon, sous quelle pression, avec quel sceau de garde et combien de la veine la route peut encore arracher sans danger.

## Scanner

La ligne de rendement rejette la valeur libre : classe, bande de pression, garde, épuisement et hash doivent concorder.

## Terminal

RESOURCE YIELD CONTRACT : aucun nombre accepté sans packet hash, classe de ressource, bande de pression, grade de garde, courbe de rareté et comportement d'épuisement. Un échantillon sans historique de pression est une preuve, pas une valeur.

## Audio

Un échantillon sans historique de pression n'est pas une valeur.

## Field Note

Les nombres de rendement restent provisoires jusqu'à accord entre bande de pression, grade de garde, comportement d'épuisement et packet hash.

<!-- External Site; generated from P301_RESOURCE_YIELD_ROW_CONTRACT/fr_FR. -->
