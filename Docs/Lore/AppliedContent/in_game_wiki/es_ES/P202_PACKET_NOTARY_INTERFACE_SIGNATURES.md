---
packet_id: P202_PACKET_NOTARY_INTERFACE_SIGNATURES
release_set_id: RS041_DEEP_REACH_LOWER_SIGNATURES
article_id: deep_reach.packet_notary_interface_signatures
unlock_id: unlock.packet_notary_interface_signatures
poi_tags: poi.packet_notary_seal;poi.relay_delay_stamp
biome_tags: biome.relay_spine;biome.claim_admin
locale: es_ES
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Firmas de la interfaz notarial de paquetes"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
next_packet_ids: P471_LUYTEN_PACKET_CUSTODY_RELAY_BRIDGE
---

# Firmas de la interfaz notarial de paquetes

La tira Packet Notary recuperada es el primer registro de oficina baja que convierte un mensaje en prueba útil y no en rumor. Ata tres datos: packet hash, hora de ventana de relevo y custodio que tocó el registro. Deep Reach podía enterrar un registro limpio llamándolo ruido de portadora sin verificar; la interfaz notarial lo dificulta solo cuando sobrevive un segundo witness hash. El sello es una herramienta de cadena de custodia, no una confesión. La firma de Som Varela certifica tiempo de ruta y estado de custodia. No demuestra por qué se retrasó el paquete ni nombra a quien ordenó el retraso.

## Scanner

Sello de paquete recuperado: tira de hash intacta, ventana de relevo 17-A, custodio sin resolver. Tratar como prueba solo si coincide la cadena testigo.

## Terminal

SIGNATURE SEED: Som Varela, Packet Notary Interface. Ruta: Relay Spine / witness hash strip. Acción: sellar packet hash, retardo local de relevo y custodio. Excepción: falta el anexo con nombre de trabajador; el paquete queda en la cola claim material. Escalado: public ledger solo tras un segundo witness hash.

## Audio

El sello está intacto. La marca de tiempo llega dos ventanas tarde. Si el witness hash encaja, no podrán llamarlo estática.

## Field Note

No lo vendas como un registro. Véndelo como reloj con testigo: hora de relevo, packet hash, custodio. Sin los tres campos, Deep Reach lo archiva como ruido de portadora.

<!-- In-Game Wiki; generated from P202_PACKET_NOTARY_INTERFACE_SIGNATURES/es_ES. -->
