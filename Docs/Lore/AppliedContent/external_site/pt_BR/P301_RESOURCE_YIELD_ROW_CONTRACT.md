---
packet_id: P301_RESOURCE_YIELD_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.resource_yield_row_contract
unlock_id: unlock.resource_yield_row_contract
poi_tags: poi.resource_yield_schema_card;poi.sample_pressure_label
biome_tags: biome.authoring;biome.resource
locale: pt_BR
surface: external_site
source_voice: Website Public
spoiler_tier: 0
title: "Limite de Dados de Rendimento de Recurso"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Limite de Dados de Rendimento de Recurso

Valor de recurso em HECTON-8 é cadeia, não rótulo. A tabela possui o número, mas a ficção possui o motivo: quem pegou a amostra, sob qual pressão, com qual selo de custódia e quanto daquele veio a rota ainda pode retirar com segurança.

## Scanner

A linha de rendimento rejeita valor solto: classe, faixa de pressão, custódia, depleção e hash precisam concordar.

## Terminal

RESOURCE YIELD CONTRACT: nenhum número é aceito sem packet hash, classe de recurso, faixa de pressão, grau de custódia, curva de raridade e comportamento de depleção. Amostra sem histórico de pressão é prova, não valor.

## Audio

Amostra sem histórico de pressão não é valor.

## Field Note

Números de yield ficam provisórios até pressure band, custody grade, depletion behavior e packet hash concordarem.

<!-- External Site; generated from P301_RESOURCE_YIELD_ROW_CONTRACT/pt_BR. -->
