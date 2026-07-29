---
packet_id: P305_ENDING_PAYOUT_ROW_CONTRACT
release_set_id: RS061_TABLE_VALUE_HANDOFF_CONTRACTS
article_id: applied_lore.ending_payout_row_contract
unlock_id: unlock.ending_payout_row_contract
poi_tags: poi.ending_payout_schema_card;poi.receiver_warning_row
biome_tags: biome.ending;biome.dossier
locale: de_DE
surface: external_site
source_voice: Website Public
spoiler_tier: 
title: "Endauszahlungs-Datensatzzeile"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P244_EVIDENCE_ORDER_DEPTH_CARD
---

# Endauszahlungs-Datensatzzeile

HECTON-8-Enden werden nach Empfänger und Verwahrung erfasst. Der Datensatz nennt, was den Ozean verlassen hat, wer es empfing, welche Beweise überlebten, was bezahlt wurde und was beschränkt bleibt. Eine Auszahlung kann eine Buchungszeile schließen, während Anspruch, Quarantäne oder Ökologie offen bleiben.

## Scanner

Ausgangszeile offen: Empfänger, Nutzlastroute, Beweisstatus, Auszahlung, Quarantäneverzug und ungelöste Folge sind nicht geschlossen.

## Terminal

ENDAUSZAHLUNGS-SCHEMA / Dossier-Empfängerprüfung: keinen Datensatz schließen, bis Nutzlastroute, Empfänger, Beweisstatus, Materialauszahlung, Pfandanpassung, Quarantäneverzug, ökologische Folge und ungelöste Kosten geschrieben sind. Credits werden nach Verwahrung freigegeben, nicht davor.

## Audio

Dossier-Relais: Empfänger hat Verwahrung akzeptiert. Quarantäneuhr startet, bevor der Kredit frei ist.

## Field Note

Nenn ein Ende nicht bezahlt, bevor der Empfänger genannt ist. Falscher Empfänger, falsche Zukunft; derselbe Ozean, andere Rechnung.

<!-- External Site; generated from P305_ENDING_PAYOUT_ROW_CONTRACT/de_DE. -->
