---
packet_id: P301_RESOURCE_YIELD_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.resource_yield_row_contract
unlock_id: unlock.resource_yield_row_contract
poi_tags: poi.resource_yield_schema_card;poi.sample_pressure_label
biome_tags: biome.authoring;biome.resource
locale: es_ES
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "Límite de datos de rendimiento de recurso"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Límite de datos de rendimiento de recurso

El límite impide que los precios de recursos sean lore suelto. En HECTON-8, un mineral no vale lo mismo a cualquier profundidad: historial de presión, custodia de ruta y agotamiento deciden si la muestra es moneda, prueba o lastre contaminado.

## Scanner

La fila de rendimiento rechaza valor suelto: clase, banda de presión, custodia, agotamiento y hash deben coincidir.

## Terminal

RESOURCE YIELD CONTRACT: ningún número se acepta sin packet hash, clase de recurso, banda de presión, grado de custodia, curva de rareza y comportamiento de agotamiento. Una muestra sin historial de presión es evidencia, no valor.

## Audio

Una muestra sin historial de presión no tiene valor.

## Field Note

Los números de yield siguen provisionales hasta que pressure band, custody grade, depletion behavior y packet hash coinciden.

<!-- In-Game Wiki; generated from P301_RESOURCE_YIELD_ROW_CONTRACT/es_ES. -->
