---
packet_id: P6805_RECEIVER_COUNTERSTAMP_ROOM_NUMBER
release_set_id: RS287_RECEIVER_COUNTERSTAMP_ROOM_NUMBER
article_id: applied_lore.receiver_counterstamp_room_number
unlock_id: unlock.receiver_counterstamp_room_number
poi_tags: poi.receiver_counterstamp_room_number;poi.quarantine_room_number_plate
biome_tags: biome.carrier_link;biome.surface_relay
locale: fr_FR
surface: in_game_wiki
source_voice: Recovered Receiver Counterstamp Note
spoiler_tier: 1
title: "Numéro de salle du counterstamp receiver"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P086_AEGIR_RECLAMATION_POOL
---

# Numéro de salle du counterstamp receiver

Un receiver counterstamp est le moment où un blank berth devient named door. Il peut rendre recovery plus difficile à ignorer, mais il nomme aussi qui possède le corps après le lift. Vérifie room class, medical owner et receiver strip avant de croire le stamp.

## Scanner

LECTURE COUNTERSTAMP // Quarantine room number présent. Vérifie body custody class; sample-only rooms ne valident pas living recovery.

## Terminal

RECEIVER ROOM COUNTERSEAL // Blank berth remplacé par owned custody. Lift allocation seulement avec named room, owner et body class.

## Audio

Room number inscrit. Receiver possède la porte. Confirme body custody avant ascent burn.

## Field Note

Un room number est une poignée. Tire doucement; certaines portes sont des cages.

<!-- In-Game Wiki; generated from P6805_RECEIVER_COUNTERSTAMP_ROOM_NUMBER/fr_FR. -->
