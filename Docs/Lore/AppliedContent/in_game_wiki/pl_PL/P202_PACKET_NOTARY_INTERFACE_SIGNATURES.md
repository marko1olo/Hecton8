---
packet_id: P202_PACKET_NOTARY_INTERFACE_SIGNATURES
release_set_id: RS041_DEEP_REACH_LOWER_SIGNATURES
article_id: deep_reach.packet_notary_interface_signatures
unlock_id: unlock.packet_notary_interface_signatures
poi_tags: poi.packet_notary_seal;poi.relay_delay_stamp
biome_tags: biome.relay_spine;biome.claim_admin
locale: pl_PL
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Podpisy Packet Notary Interface"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Podpisy Packet Notary Interface

Odzyskany pasek Packet Notary to pierwszy zapis niższego biura, który czyni wiadomość użytecznym dowodem zamiast pogłoską. Łączy trzy rzeczy: packet hash, czas okna przekaźnika i właściciela depozytu, który dotykał zapisu. Deep Reach mógł zakopać czysty log jako niesprawdzony szum nośnej; interfejs notarialny utrudnia to tylko wtedy, gdy przetrwa drugi witness hash. Pieczęć jest narzędziem łańcucha depozytu, nie przyznaniem się. Podpis Som Vareli certyfikuje czas trasy i status depozytu. Nie dowodzi, dlaczego pakiet opóźniono, ani nie wskazuje osoby, która to nakazała.

## Scanner

Odzyskano pieczęć pakietu: pasek hash nienaruszony, okno przekaźnika 17-A, właściciel depozytu nierozstrzygnięty. Traktować jako dowód dopiero po zgodności witness chain.

## Terminal

SIGNATURE SEED: Som Varela, Packet Notary Interface. Trasa: Relay Spine / witness hash strip. Działanie: opieczętować packet hash, lokalne opóźnienie przekaźnika i właściciela depozytu. Wyjątek: brak załącznika z nazwiskiem pracownika trzyma pakiet w kolejce claim material. Eskalacja: public ledger po drugim witness hash.

## Audio

Pieczęć jest cała. Znacznik czasu spóźnia się o dwa okna. Jeśli witness hash pasuje, nie nazwą tego zakłóceniami.

## Field Note

Nie sprzedawaj tego jako logu. Sprzedaj jako zegar ze świadkiem: czas przekaźnika, packet hash, właściciel depozytu. Bez tych trzech pól Deep Reach nazwie to luźnym szumem nośnej.

<!-- In-Game Wiki; generated from P202_PACKET_NOTARY_INTERFACE_SIGNATURES/pl_PL. -->
