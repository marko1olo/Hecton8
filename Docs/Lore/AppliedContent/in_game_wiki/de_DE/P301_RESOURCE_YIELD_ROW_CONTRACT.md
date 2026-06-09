---
packet_id: P301_RESOURCE_YIELD_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.resource_yield_row_contract
unlock_id: unlock.resource_yield_row_contract
poi_tags: poi.resource_yield_schema_card;poi.sample_pressure_label
biome_tags: biome.authoring;biome.resource
locale: de_DE
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "Datengrenze des Ressourcenertrags"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Datengrenze des Ressourcenertrags

Die Datengrenze verhindert, dass Ressourcenpreise lose Lore werden. Auf HECTON-8 ist ein Mineral nicht in jeder Tiefe gleich viel wert: Druckgeschichte, Routenverwahrung und Erschöpfung entscheiden, ob eine Probe Währung, Beweis oder kontaminierter Ballast ist.

## Scanner

Ertragszeile weist losen Wert zurück: Klasse, Druckband, Verwahrung, Erschöpfung und Hash müssen stimmen.

## Terminal

RESOURCE YIELD CONTRACT: Keine Zahl wird ohne packet hash, Ressourcenklasse, Druckband, Verwahrungsgrad, Seltenheitskurve und Erschöpfungsverhalten akzeptiert. Eine Probe ohne Druckgeschichte ist Beweis, kein Wert.

## Audio

Eine Probe ohne Druckgeschichte ist kein Wert.

## Field Note

Ertragszahlen bleiben vorläufig, bis Druckband, Verwahrungsgrad, Erschöpfungsverhalten und packet hash übereinstimmen.

<!-- In-Game Wiki; generated from P301_RESOURCE_YIELD_ROW_CONTRACT/de_DE. -->
