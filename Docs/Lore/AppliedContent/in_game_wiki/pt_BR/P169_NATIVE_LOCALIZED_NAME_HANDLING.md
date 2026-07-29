---
packet_id: P169_NATIVE_LOCALIZED_NAME_HANDLING
release_set_id: RS034_WORKER_NAME_JOB_EVIDENCE_TABLE
article_id: worker_evidence.native_localized_name_handling
unlock_id: unlock.native_localized_name_handling
poi_tags: poi.localized_name_policy;poi.rtl_name_strip
biome_tags: biome.claim_admin;biome.shallow_annex
locale: pt_BR
surface: in_game_wiki
source_voice: Neutral Reference
spoiler_tier: 
title: "Protocolo de localização nativa de nomes"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
---

# Protocolo de localização nativa de nomes

A localização nativa de nomes protege a camada de evidência trabalhadora de acidentes de interface. O jogador não deve ver nome esmagado, invertido, meio traduzido por fallback nem substituído por resto debug inglês.

A regra é simples: identidade pessoal é escrita por locale, e os sistemas ao redor traduzem normalmente. Se um idioma precisa de forma curta para crachá, ela é escrita e baked, não inventada no runtime.

## Scanner

NAME LOC // Esta tira é autorada, não traduzida ao vivo. A pessoa sobrevive à interface só quando a interface para de improvisar.

## Terminal

LOCALIZAÇÃO DE NOMES // Nomes pessoais, tiras curtas e fragmentos de crachá são baked por locale. Cargos, departamentos, permissões de rota e notas de turno se localizam ao redor. RTL e CJK exigem formas curtas autoradas, quebras seguras e nenhuma recomposição live em scanner, UI de armário, terminais ou wiki externo.

## Audio

Um nome que quebra a UI não é respeito. É a colônia apagando o trabalhador duas vezes.

## Field Note

Nunca deixe um runtime fallback renomear um trabalhador morto. Nome quebrado é outro apagamento.

<!-- In-Game Wiki; generated from P169_NATIVE_LOCALIZED_NAME_HANDLING/pt_BR. -->
