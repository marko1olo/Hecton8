---
packet_id: P206_WORKER_ROSTER_SIZE_RULE
release_set_id: RS042_COLONY_ROSTER_AUTHORING_POOL
article_id: colony.worker_roster_size_rule
unlock_id: unlock.worker_roster_size_rule
poi_tags: poi.roster_size_sheet;poi.shift_board_frame
biome_tags: biome.worker_locker;biome.p63_shallows
locale: pl_PL
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 0
title: "Reguła rozmiaru listy pracowników"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Reguła rozmiaru listy pracowników

Reguła rozmiaru listy pracowników utrzymuje opuszczoną kolonię czytelną. HECTON-8 ma sprawiać wrażenie miejsca zamieszkanego przez brygady, nie obsypanego losowymi tabliczkami, dlatego lista jest celowo dość mała, by powtórzenia miały znaczenie. Pracownicy kotwice wracają w szafkach, znacznikach pomp, kartach triażu, zezwoleniach tras i uszkodzonych przedmiotach pracy. Pracownicy seed-role poszerzają bieg rozgrywki, ale nadal mają zawód, miejsce i ostatnie zadanie. Reguła powstrzymuje dwa błędy: anonimowe ruiny bez ludzkiego ciężaru oraz spam nazwisk, przez który każda śmierć wygląda tanio proceduralnie.

## Scanner

Siedemdziesiąt dwa nazwiska to nie tekst dla nastroju. To budżet pamięci kolonii: dość rąk, by miejsce wyglądało na przepracowane, i dość mało, by szafka, znacznik naprawy i ostatnia zmiana wskazywały tę samą osobę.

## Terminal

ROSTER RULE: aktywna lista niesie 72 tożsamości pracowników. Dwadzieścia cztery to nazwiska kotwice, które mogą wracać w szafkach, rejestrach, uszkodzonych narzędziach, stemplach zezwoleń i fragmentach audio. Czterdzieści osiem to nazwiska seed-role dla wariacji replay. Wygenerowane nazwisko może przesuwać kolejność dowodów; nie może stać się jednorazowym wypełniaczem.

## Audio

Nazwisko staje się dowodem, gdy pomieszczenie potrafi udowodnić pracę.

## Field Note

Jeśli nazwisko nie może później wrócić z przypisaną pracą, wytnij je z listy.

<!-- In-Game Wiki; generated from P206_WORKER_ROSTER_SIZE_RULE/pl_PL. -->
