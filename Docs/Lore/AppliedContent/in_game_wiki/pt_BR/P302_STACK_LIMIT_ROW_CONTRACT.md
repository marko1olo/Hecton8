---
packet_id: P302_STACK_LIMIT_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.stack_limit_row_contract
unlock_id: unlock.stack_limit_row_contract
poi_tags: poi.stack_limit_schema_card;poi.pressure_vessel_rack
biome_tags: biome.inventory;biome.resource_custody
locale: pt_BR
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "Limite de Dados de Stack"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Limite de Dados de Stack

O limite mantém o inventário físico. Itens só empilham quando contêiner, rating de pressão, massa e estado de contaminação podem sobreviver à mesma rota sem mentir para o arquivo de save.

## Scanner

A linha de stack rejeita pilhas de ícones: classe do vaso, pressão, contaminação e massa decidem a contagem.

## Terminal

STACK CONTRACT: contagem de stack exige tipo de vaso, rating de pressão, estágio de contaminação, classe de massa, tier de aviso e identidade save-stable. Uma caixa não é vaso de pressão.

## Audio

Uma caixa não é vaso de pressão.

## Field Note

Limites de stack ficam table-owned e estáveis para save identity.

<!-- In-Game Wiki; generated from P302_STACK_LIMIT_ROW_CONTRACT/pt_BR. -->
