---
packet_id: P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
release_set_id: RS288_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE
article_id: applied_lore.transcript_damage_band_offset_field_article
unlock_id: unlock.transcript_damage_band_offset_field_article
poi_tags: poi.transcript_damage_band;poi.hydrophone_log_strip
biome_tags: biome.drowned_colony;biome.pressure_base
locale: ru_RU
surface: in_game_wiki
source_voice: PDA Forensic Object Article
spoiler_tier: 
title: "Сдвиг полосы повреждения расшифровки"
source: AppliedContent packet JSON
runtime_reads_markdown: false
direction: ltr
localization_status: draft_machine_or_llm
localization_flags: 1
prereq_packet_ids: P1330_HYDROPHONE_LOG_STRIP_FIELD_ARTICLE;P1333_PRESSURE_INK_WHEEL_DRYOUT_FIELD_ARTICLE;P1331_PRESSURE_ROOM_RECORDER_BAY_FIELD_ARTICLE;P1335_RECORDER_SERVICE_SEAL_TEAR_DIRECTION_FIELD_ARTICLE
next_packet_ids: P1337_PACKET_NOTARY_MASK_EDGE_FIELD_ARTICLE;P712_HANDOVER_SECTION_ELEVEN;P712_TAPE_SEAL_G77;P162_DOMAIN_POPULATION_AUTHORITY_SCALE;P164_TRANSIT_DURATION_BANDS;P169_NATIVE_LOCALIZED_NAME_HANDLING;P180_WEBSITE_WIKI_SPOILER_TIERING;P183_AEGIR_MOON_LEDGER_ROLE_TABLE;P186_TRUE_CAUSE_KNOWLEDGE_TIERS;P188_SIGNOFF_WITNESS_CONFLICT;P189_SUBOFFICE_PERSONNEL_SEEDS;P196_RESOURCE_TABLE_PLACEHOLDER_CONTRACT;P201_CONTRACT_CONTINUITY_DESK_SIGNATURES;P437_DEEP_REACH_SANITIZED_PACKET_TRANSCRIPT_SEED;P442_STACK_LIMIT_VALUE_DRAFT_ROWS
---

# Сдвиг полосы повреждения расшифровки

Полоса повреждения расшифровки должна честно показывать место, где звук потерян. Нормальная полоса стоит там, где регистратор, край кассеты или packet notary оставили связанную метку. Плохая полоса плавает. Она закрывает чистый слог, промахивается мимо царапины на носителе или ложится между двумя делениями часовой лестницы, где ее не держит ни один предмет. На экране это мелочь. В цепочке хранения это грязное место.

В цепи регистратора напорной комнаты полоса должна совпасть не только со звуком. На гидрофонной полосе может быть белая царапина от протяжки головки. Зубцы кассеты могут пропустить шаг рядом с тем же скачком давления. Чернильное колесо может оборваться сухо до провала или расплыться мокрым следом после него. Packet notary может закрыть имя или координату, но не может сдвинуть царапину на носителе. Если черная полоса запаздывает на 1,8 секунды, чистую расшифровку делали из более грязного предмета.

В формулировках Deep Reach такую проблему удобно прятать под "деградацией аудио". Это дешевая фраза. У настоящего сдвига есть измеримый порядок: событие в комнате, повреждение носителя, проход копирования, маска notary, опубликованная расшифровка. Если опубликованная полоса следует за маской, но не за полосой носителя, источник трогали после того, как запись комнаты уже существовала. Если полоса закрывает route permission stamp, а механические метки остаются читаемыми, пропуск процедурный, а не случайный.

Практическое правило простое: сравни полосу до того, как доверять словам. Если полоса совпадает с царапиной кассеты, оставь расшифровку в цепи доказательств. Если она идет позже царапины, пометь расшифровку как позднюю копию. Если она закрывает единственную строку, связывающую маршрутный hold с человеком, ищи физический носитель до того, как принимать чистый текст.

## Scanner

СДВИГ ПОЛОСЫ РАСШИФРОВКИ // Черная полоса повреждения стоит на 1,8 секунды позже царапины кассеты и перед маской packet notary. Метка не совпадает с физическим повреждением полосы.

## Terminal

ЗАМЕТКА QA ПО РАСШИФРОВКЕ // Сверить полосу повреждения с царапиной кассеты, часовой лестницей, провалом гидрофона и штампом packet notary. Если полоса плавает между метками, не считать расшифровку главным доказательством.

## Audio

Сначала идет царапина. Затем сдвигается затемнение.

## Field Note

Если черная полоса отстает от царапины носителя, сначала упакуй носитель, потом цитируй текст.

<!-- In-Game Wiki; generated from P1336_TRANSCRIPT_DAMAGE_BAND_OFFSET_FIELD_ARTICLE/ru_RU. -->
