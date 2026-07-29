---
packet_id: P1343_COMPRESSOR_HANDOFF_BACKFILL_FIELD_ARTICLE
release_set_id: RS295_COMPRESSOR_HANDOFF_BACKFILL_FIELD_ARTICLE
article_id: applied_lore.compressor_handoff_backfill_field_article
unlock_id: unlock.compressor_handoff_backfill_field_article
poi_tags: poi.compressor_handoff_backfill;poi.pump_counter
biome_tags: biome.drowned_colony;biome.pressure_base
locale: fr_FR
surface: in_game_wiki
source_voice: PDA Forensic Object Article
spoiler_tier: 
title: "Rattrapage de passation compresseur"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1342_SCRUBBER_CARTRIDGE_SERIAL_MISMATCH_FIELD_ARTICLE
next_packet_ids: P1344_SUPPORT_LOAD_CARRY_FORWARD_EXCEPTION_FIELD_ARTICLE
---

# Rattrapage de passation compresseur

Une ligne de passation compresseur devrait se placer entre le mouvement mecanique de l'air et le registre de la piece. Le compresseur deplace la pression. Le relais marque le changement d'etat. Le compteur de pompe compte les cycles. Le clapet empeche un cote du circuit de tirer de la saumure dans l'autre. Quand ces quatre marques concordent, la ligne est banale. Elle dit seulement que la piece a transmis une charge au compte suivant.

Le rattrapage commence quand la ligne reste lisse apres que la machine est devenue irreguliere. Un compresseur noye ne tombe pas en panne comme ecrit un employe. Il cale, tousse dans les blancs du relais, laisse du sel d'un seul cote du clapet et cesse de compter des cycles propres. Une ligne ajoutee plus tard peut encore paraitre complete. transfer accepted, compressor handoff pending, support load carried forward. Aucune de ces formules ne pousse de l'air dans une pompe arretee.

Ce registre se lit apres la coupure du registre oxygene, le delta de reserve de combinaison et la discordance de serie du scrubber. Le registre donne la limite de la piece. La combinaison donne ce qui est reste avec le corps. La plaque du scrubber donne quelle cartouche occupait le train d'air. La passation compresseur dit si la piece a vraiment pousse cette charge plus loin. Si le compteur meurt d'abord et la ligne arrive ensuite, la chaine a quitte la mecanique pour entrer dans la langue de garde.

Sur le terrain, il faut garder la laideur du compresseur. Photographier la face du compteur avant nettoyage. Noter la position du relais, l'anneau de sel du clapet, la tache de saumure dans la coupelle de purge et la ligne handoff exacte qui declare le transfert. Ne pas accepter une ligne lisse sans marques machine concordantes. La ligne peut dire qui a porte la responsabilite plus loin. Elle ne peut pas rendre de l'air a la piece.

## Scanner

RATTRAPAGE COMPRESSEUR // Le compteur de pompe s'arrete avant la ligne de passation. L'anneau de sel du clapet et le cran du relais ne soutiennent pas le transfert propre ajoute plus tard.

## Terminal

QA PASSATION COMPRESSEUR // Comparer compteur de pompe, cran de relais, anneau de sel du clapet, coupure du registre oxygene, delta de reserve de combinaison, serie de scrubber discordante et claim hold. Une ligne handoff ne prouve pas que l'air a bouge.

## Audio

Le compteur s'est arrete. Le relais aussi. La ligne a continue parce que quelqu'un avait besoin d'une passation propre.

## Field Note

Si la pompe s'est arretee avant la ligne, le handoff est une entree de desk, pas de l'air.

<!-- In-Game Wiki; generated from P1343_COMPRESSOR_HANDOFF_BACKFILL_FIELD_ARTICLE/fr_FR. -->
