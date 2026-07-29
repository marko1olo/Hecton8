---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: es_ES
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Protocolo de localización nativa de nombres"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
---

# Protocolo de localización nativa de nombres

La localización nativa de nombres evita que la capa de evidencia obrera se vuelva un accidente de interfaz. El jugador no debe ver nombres aplastados, invertidos sin sentido, medio traducidos por fallback ni restos debug en inglés.

La regla es simple: la identidad personal se escribe por locale y los sistemas alrededor se traducen normalmente. El nombre debe seguir siendo un artefacto deliberado. Si un idioma necesita forma corta para placa, se escribe y se hornea, no se improvisa en runtime.

## Scanner

NAME LOC // Esta tira está autorada, no traducida en vivo. La persona sobrevive a la interfaz solo si la interfaz deja de improvisar.

## Terminal

LOCALIZACIÓN DE NOMBRES // Nombres personales, tiras cortas y fragmentos de placa se hornean por locale. Cargos, departamentos, permisos de ruta y notas de turno se localizan alrededor. RTL y CJK requieren formas cortas autoradas, saltos seguros y sin recomposición live en escáner, UI de casillero, terminales o wiki externa.

## Audio

Un nombre que rompe la UI no es respeto. Es la colonia borrando al trabajador dos veces.

## Field Note

Nunca permitas que un fallback runtime renombre a un trabajador muerto. Un nombre roto es otra forma de borrado.

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/es_ES. -->
