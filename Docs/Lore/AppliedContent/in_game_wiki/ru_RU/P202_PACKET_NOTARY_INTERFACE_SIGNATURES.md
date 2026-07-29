---
packet_id: P202_PACKET_NOTARY_INTERFACE_SIGNATURES
release_set_id: RS041_DEEP_REACH_LOWER_SIGNATURES
article_id: deep_reach.packet_notary_interface_signatures
unlock_id: unlock.packet_notary_interface_signatures
poi_tags: poi.packet_notary_seal;poi.relay_delay_stamp
biome_tags: biome.relay_spine;biome.claim_admin
locale: ru_RU
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Подписи Packet Notary Interface"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Подписи Packet Notary Interface

Восстановленная лента Packet Notary — первая запись нижнего офиса, которая делает сообщение доказательством, а не слухом. Она связывает три поля: packet hash, время окна ретрансляции и владельца хранения, который держал запись. Deep Reach мог похоронить чистый лог как непроверенный шум канала; нотариальный интерфейс мешает этому только тогда, когда уцелел второй witness hash. Печать — инструмент цепочки хранения, а не признание. Подпись Som Varela заверяет время маршрута и статус хранения. Она не доказывает, почему пакет задержали, и не называет того, кто приказал задержку.

## Scanner

Пакетная печать восстановлена: хеш-лента цела, метка окна ретрансляции 17-A, владелец хранения не назначен. Считать доказательством только после совпадения witness chain.

## Terminal

SIGNATURE SEED: Som Varela, Packet Notary Interface. Маршрут: Relay Spine / witness hash strip. Действие: заверить packet hash, локальную задержку ретрансляции и владельца хранения. Исключение: отсутствие приложения с именем работника удерживает пакет в очереди claim material. Эскалация: public ledger только после второго witness hash.

## Audio

Печать цела. Метка времени опоздала на два окна. Если witness hash совпадет, они уже не назовут это помехами.

## Field Note

Не продавай это как лог. Продавай как часы плюс свидетель: время ретрансляции, packet hash, владелец хранения. Без трех полей Deep Reach спишет запись на шум канала.

<!-- In-Game Wiki; generated from P202_PACKET_NOTARY_INTERFACE_SIGNATURES/ru_RU. -->
