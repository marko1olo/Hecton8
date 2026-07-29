---
packet_id: P202_PACKET_NOTARY_INTERFACE_SIGNATURES
release_set_id: RS041_DEEP_REACH_LOWER_SIGNATURES
article_id: deep_reach.packet_notary_interface_signatures
unlock_id: unlock.packet_notary_interface_signatures
poi_tags: poi.packet_notary_seal;poi.relay_delay_stamp
biome_tags: biome.relay_spine;biome.claim_admin
locale: uk_UA
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Підписи Packet Notary Interface"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Підписи Packet Notary Interface

Відновлена стрічка Packet Notary — перший запис нижнього офісу, який робить повідомлення доказом, а не чуткою. Вона зв'язує три речі: packet hash, час ретрансляційного вікна та власника зберігання, який торкався запису. Deep Reach міг поховати чистий лог як неперевірений шум носія; нотаріальний інтерфейс заважає цьому тільки тоді, коли вцілів другий witness hash. Печатка є інструментом ланцюга зберігання, а не зізнанням. Підпис Som Varela засвідчує час маршруту і статус зберігання. Він не доводить, чому пакет затримали, і не називає того, хто наказав затримку.

## Scanner

Пакетну печатку відновлено: хеш-стрічка ціла, мітка ретрансляційного вікна 17-A, власник зберігання не визначений. Вважати доказом лише після збігу witness chain.

## Terminal

SIGNATURE SEED: Som Varela, Packet Notary Interface. Маршрут: Relay Spine / witness hash strip. Дія: засвідчити packet hash, локальну затримку ретрансляції та власника зберігання. Виняток: бракує додатка з іменем працівника, тому пакет лишається в черзі claim material. Ескалація: public ledger тільки після другого witness hash.

## Audio

Печатка ціла. Часова мітка запізнилася на два вікна. Якщо witness hash збіжиться, вони вже не назвуть це перешкодами.

## Field Note

Не продавай це як лог. Продавай як годинник із свідком: час ретрансляції, packet hash, власник зберігання. Без трьох полів Deep Reach спише запис на шум носія.

<!-- In-Game Wiki; generated from P202_PACKET_NOTARY_INTERFACE_SIGNATURES/uk_UA. -->
