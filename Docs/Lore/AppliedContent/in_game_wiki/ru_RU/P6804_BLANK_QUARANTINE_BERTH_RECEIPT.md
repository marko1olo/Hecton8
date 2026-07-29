---
packet_id: P6804_BLANK_QUARANTINE_BERTH_RECEIPT
release_set_id: RS286_BLANK_QUARANTINE_BERTH_RECEIPT
article_id: applied_lore.blank_quarantine_berth_receipt
unlock_id: unlock.blank_quarantine_berth_receipt
poi_tags: poi.blank_quarantine_berth_receipt;poi.receiver_berth_null_stamp
biome_tags: biome.carrier_link;biome.surface_relay
locale: ru_RU
surface: in_game_wiki
source_voice: Recovered Quarantine Receipt Note
spoiler_tier: 1
title: "Пустая квитанция карантинного берта"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P6803_TONNE_WINDOW_BODY_LEDGER
---

# Пустая квитанция карантинного берта

Пустая квитанция quarantine berth показывает разницу между тем, что тебя услышали, и тем, что тебя приняли. У Black Keel есть живая body line и оцененное tonne-window, но receiver не назвал карантинную комнату, medical owner или custody door. Читай receipt как evidence: маршрут активен, отказ процедурный, а отсутствующая комната и есть блокер.

## Scanner

ЧТЕНИЕ BERTH FIELD // Body line принята. Quarantine berth пуст. Receiver custody отсутствует; lift allocation остается заблокированным.

## Terminal

NULL BERTH RECEIPT // Packet принят, body оценено, quarantine volume не назван. Не принимай carrier acknowledgement за rescue.

## Audio

Receiver line есть. Berth пуст. Держи ascent sleeve закрытым, пока комната не примет тело.

## Field Note

Пустой berth не пустая комната. Это дверь, которую никто не согласился владеть.

<!-- In-Game Wiki; generated from P6804_BLANK_QUARANTINE_BERTH_RECEIPT/ru_RU. -->
