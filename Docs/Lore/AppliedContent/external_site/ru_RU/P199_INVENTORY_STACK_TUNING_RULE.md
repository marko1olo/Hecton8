---
packet_id: P199_INVENTORY_STACK_TUNING_RULE
release_set_id: RS040_NUMERIC_TUNING_SOURCE_RULES
article_id: tuning.inventory_stack_tuning_rule
unlock_id: unlock.inventory_stack_tuning_rule
poi_tags: poi.stack_policy_card;poi.pressure_container_label
biome_tags: biome.fabricator_room;biome.claim_admin
locale: ru_RU
surface: external_site
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: source_ready
localization_flags: 0
---

# Правило настройки stack inventory

Правило настройки stack inventory задает future stack-size logic для ресурсов HECTON-8.

## Scanner

Inventory stacks должны выражать physical containment и cargo debt, а не arbitrary UI convenience.

## Terminal

STACK RULE: stack by containment vessel, pressure class, contamination stage, carrier lien mass, certification state и sample quality. Generic loot stacks rejected для critical resources.

## Audio

Если это может сломать claim, оно не должно stack как scrap.

## Field Note

Blue debt, seals, samples и payload parts должны переносить pressure и custody logic в inventory.

<!-- External Site; generated from P199_INVENTORY_STACK_TUNING_RULE/ru_RU. -->
