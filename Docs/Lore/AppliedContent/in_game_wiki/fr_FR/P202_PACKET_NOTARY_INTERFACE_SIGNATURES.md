---
packet_id: P202_PACKET_NOTARY_INTERFACE_SIGNATURES
release_set_id: RS041_DEEP_REACH_LOWER_SIGNATURES
article_id: deep_reach.packet_notary_interface_signatures
unlock_id: unlock.packet_notary_interface_signatures
poi_tags: poi.packet_notary_seal;poi.relay_delay_stamp
biome_tags: biome.relay_spine;biome.claim_admin
locale: fr_FR
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Signatures de l'interface notariale de paquets"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Signatures de l'interface notariale de paquets

La bande Packet Notary récupérée est le premier dossier de bureau inférieur qui rend un message utilisable comme preuve plutôt que comme rumeur. Elle lie trois éléments : packet hash, heure de fenêtre relais et détenteur de garde qui a touché le dossier. Deep Reach pouvait enterrer un journal propre en bruit de transport non vérifié; l'interface notariale réduit cette marge seulement quand un second witness hash a survécu. Le sceau est un outil de chaîne de garde, pas un aveu. La signature de Som Varela certifie l'heure de route et l'état de garde. Elle ne prouve pas pourquoi le paquet a été retardé et ne nomme pas la personne qui a ordonné le retard.

## Scanner

Sceau de paquet récupéré : bande de hachage intacte, fenêtre relais 17-A, détenteur de garde non résolu. À traiter comme preuve seulement si la chaîne témoin concorde.

## Terminal

SIGNATURE SEED: Som Varela, Packet Notary Interface. Route : Relay Spine / witness hash strip. Action : sceller le packet hash, le délai relais local et le détenteur de garde. Exception : l'annexe du nom de travailleur manque; le paquet reste en file claim material. Escalade : public ledger après un second witness hash.

## Audio

Le sceau tient. L'horodatage a deux fenêtres de retard. Si le witness hash concorde, ils ne pourront plus parler de parasites.

## Field Note

Ne vends pas ça comme un journal. Vends-le comme une horloge avec témoin : heure relais, packet hash, détenteur de garde. Sans les trois champs, Deep Reach le classe en bruit de transport.

<!-- In-Game Wiki; generated from P202_PACKET_NOTARY_INTERFACE_SIGNATURES/fr_FR. -->
