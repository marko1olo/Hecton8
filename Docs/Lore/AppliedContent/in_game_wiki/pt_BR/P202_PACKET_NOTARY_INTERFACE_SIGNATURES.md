---
packet_id: P202_PACKET_NOTARY_INTERFACE_SIGNATURES
release_set_id: RS041_DEEP_REACH_LOWER_SIGNATURES
article_id: deep_reach.packet_notary_interface_signatures
unlock_id: unlock.packet_notary_interface_signatures
poi_tags: poi.packet_notary_seal;poi.relay_delay_stamp
biome_tags: biome.relay_spine;biome.claim_admin
locale: pt_BR
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Assinaturas da Packet Notary Interface"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
next_packet_ids: P471_LUYTEN_PACKET_CUSTODY_RELAY_BRIDGE
---

# Assinaturas da Packet Notary Interface

A faixa Packet Notary recuperada é o primeiro registro de escritório inferior que torna uma mensagem útil como prova em vez de rumor. Ela amarra três coisas: packet hash, tempo da janela de retransmissão e dono da custódia que tocou o registro. A Deep Reach podia enterrar um log limpo chamando-o de ruído de portadora não verificado; a interface notarial dificulta isso apenas quando um segundo witness hash sobrevive. O selo é ferramenta de cadeia de custódia, não confissão. A assinatura de Som Varela certifica tempo de rota e status de custódia. Ela não prova por que o pacote foi atrasado nem nomeia quem ordenou o atraso.

## Scanner

Selo de pacote recuperado: faixa de hash intacta, janela de retransmissão 17-A, dono da custódia não resolvido. Tratar como prova só depois que a witness chain coincidir.

## Terminal

SIGNATURE SEED: Som Varela, Packet Notary Interface. Rota: Relay Spine / witness hash strip. Ação: selar packet hash, atraso local de retransmissão e dono da custódia. Exceção: anexo com nome de trabalhador ausente mantém o pacote na fila claim material. Escalação: public ledger só após segundo witness hash.

## Audio

O selo está inteiro. A marca de tempo atrasou duas janelas. Se o witness hash bater, eles não podem chamar de estática.

## Field Note

Não venda isso como log. Venda como relógio com testemunha: tempo de retransmissão, packet hash, dono da custódia. Sem os três campos, a Deep Reach chama de ruído solto de portadora.

<!-- In-Game Wiki; generated from P202_PACKET_NOTARY_INTERFACE_SIGNATURES/pt_BR. -->
