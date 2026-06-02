#!/usr/bin/env python3
"""Generate AppliedLore RS025-RS028 source packets and integration sidecars."""

from __future__ import annotations

import csv
import io
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "Docs" / "Lore" / "AppliedContent"

LOCALES = (
    "en_US",
    "ru_RU",
    "ja_JP",
    "zh_CN",
    "fr_FR",
    "es_ES",
    "de_DE",
    "pl_PL",
    "uk_UA",
    "ar_SA",
    "id_ID",
    "ko_KR",
    "he_IL",
    "pt_BR",
    "nl_NL",
)

LOCALE_PREFIX = {
    "ja_JP": "Draft JP localization pending native pass.",
    "zh_CN": "Draft CN localization pending native pass.",
    "fr_FR": "Draft FR localization pending native pass.",
    "es_ES": "Draft ES localization pending native pass.",
    "de_DE": "Draft DE localization pending native pass.",
    "pl_PL": "Draft PL localization pending native pass.",
    "uk_UA": "Draft UA localization pending native pass.",
    "ar_SA": "Draft AR localization pending native pass.",
    "id_ID": "Draft ID localization pending native pass.",
    "ko_KR": "Draft KO localization pending native pass.",
    "he_IL": "Draft HE localization pending native pass.",
    "pt_BR": "Draft PT localization pending native pass.",
    "nl_NL": "Draft NL localization pending native pass.",
}

FNV_OFFSET = 2166136261
FNV_PRIME = 16777619


def fnv1a32(value: str) -> int:
    if not value:
        return 0
    hash_value = FNV_OFFSET
    for char in value:
        code = ord(char)
        if 65 <= code <= 90:
            code += 32
        hash_value ^= code & 0xFF
        hash_value = (hash_value * FNV_PRIME) & 0xFFFFFFFF
    return hash_value or 1


SETS = [
    {
        "id": "RS025_HUMAN_LAW_PUBLIC_MEMORY",
        "summary": "locks the domain authority split, marauder legal loophole, salvage truth custody, normal Aegir public memory and Deep Reach origin chain.",
        "packets": [
            {
                "packet_id": "P121_DOMAIN_CIVIC_CORPORATE_SPLIT",
                "article_id": "human_space.domain_civic_corporate_split",
                "title": "Domain Civic-Corporate Split",
                "title_ru": "Гражданско-корпоративный раскол доменов",
                "unlock": "unlock.domain_civic_corporate_split",
                "poi": ["poi.route_law_plate", "poi.domain_mark_terminal", "poi.claim_jurisdiction_tag"],
                "biomes": ["biome.black_keel_orbit", "biome.shallow_annex", "biome.relay_archive"],
                "en": {
                    "scanner": "Legal map. Same species, different owners of truth.",
                    "field_note": "The domains do not need dozens of names. They need enough pressure to make rescue political.",
                    "terminal": "DOMAIN MAP 2190: Sol Core owns old-law finance; Centauri owns early legitimacy; Barnard owns salvage habit; Tau Ceti owns public-law pressure; Luyten owns packet custody; Aegir is a corporate claim.",
                    "audio": "The farther you go, the more the law becomes a receipt.",
                    "wiki": "Human space around 2190 is sparse but mature. Sol Core still matters for ownership, finance and certification. Centauri gives Deep Reach old charter language. Barnard Yards explains the player's salvage culture. Tau Ceti can make evidence public after relay delay. Luyten controls custody routes. Aegir is where those systems become exploitable.",
                    "site": "The Domain Civic-Corporate Split keeps HECTON-8 from becoming dense space opera: a few named authorities, many implied footholds, and no instant rescue."
                },
                "ru": {
                    "scanner": "Юридическая карта. Один вид, разные владельцы правды.",
                    "field_note": "Доменов не нужны десятки. Их нужно ровно столько, чтобы спасение стало политической проблемой.",
                    "terminal": "DOMAIN MAP 2190: Sol Core держит старое право и финансы; Centauri - раннюю легитимность; Barnard - привычку к salvage; Tau Ceti - публично-правовое давление; Luyten - custody пакетов; Aegir - корпоративный claim.",
                    "audio": "Чем дальше от центра, тем больше закон похож на квитанцию.",
                    "wiki": "Человеческое пространство к 2190 году разреженное, но зрелое. Sol Core важен для собственности, финансов и сертификации. Centauri дает Deep Reach язык старых хартий. Barnard Yards объясняет salvage-культуру игрока. Tau Ceti может сделать доказательства публичными после relay-задержки. Luyten контролирует custody маршруты. Aegir - место, где все эти системы становятся уязвимыми.",
                    "site": "Гражданско-корпоративный раскол доменов удерживает HECTON-8 от плотной space opera: несколько названных центров власти, множество подразумеваемых foothold и никакого мгновенного спасения."
                },
                "graph": ("human_law", "orbit-600m", "first_domain_law_plate", "P071_SOL_CORE_AUTHORITY;P072_CENTAURI_COMPACT_LEGITIMACY", "P122_MARAUDER_LEGAL_LOOPHOLE", "domain jurisdiction plate", "Human space has enough law to trap the player, not enough law to save them fast.", "Read rescue as jurisdiction, not kindness.", "1", "terminal"),
                "route": ("RC115_DOMAIN_CIVIC_CORPORATE_SPLIT", "human_law", 0, 600, "P121_DOMAIN_CIVIC_CORPORATE_SPLIT", "P071_SOL_CORE_AUTHORITY;P075_LUYTEN_JUNCTION_PACKET_CUSTODY", "terminal", "route law plate, domain mark terminal, claim jurisdiction tag", "Who has authority here?", "Authority is split between finance, public law, custody and corporate claims.", "Jurisdiction ordering and packet custody vary by seed.", "none"),
            },
            {
                "packet_id": "P122_MARAUDER_LEGAL_LOOPHOLE",
                "article_id": "human_space.marauder_legal_loophole",
                "title": "Marauder Legal Loophole",
                "title_ru": "Юридическая лазейка мародеров",
                "unlock": "unlock.marauder_legal_loophole",
                "poi": ["poi.claim_license_beacon", "poi.salvage_lien_console", "poi.black_keel_manifest_slot"],
                "biomes": ["biome.black_keel_orbit", "biome.photic_shelf", "biome.industrial_shelf"],
                "en": {
                    "scanner": "License valid in one lane, criminal in the next.",
                    "field_note": "Marauder is a job title only when the right court is listening.",
                    "terminal": "CLAIM STATUS: licensed salvage contractor under Aegir Reclamation Pool custody; tolerated trespasser under Deep Reach asset language; prosecutable raider under clean Sol summaries.",
                    "audio": "The same cutter can be a tool, a crime, or a rescue device.",
                    "wiki": "Marauders are not a single faction. They are licensed contractors where a dead claim needs work, tolerated criminals where recovery is cheaper than enforcement, and illegal raiders when evidence threatens a claimant. The player lives inside that legal ambiguity.",
                    "site": "The Marauder Legal Loophole makes salvage a profession with teeth: useful enough to hire, dirty enough to abandon."
                },
                "ru": {
                    "scanner": "Лицензия действует в одном коридоре и становится преступлением в следующем.",
                    "field_note": "Marauder - профессия только пока слушает правильный суд.",
                    "terminal": "CLAIM STATUS: licensed salvage contractor under Aegir Reclamation Pool custody; tolerated trespasser under Deep Reach asset language; prosecutable raider under clean Sol summaries.",
                    "audio": "Один и тот же резак может быть инструментом, преступлением или средством спасения.",
                    "wiki": "Мародеры не единая фракция. Они лицензированные подрядчики там, где мертвому claim нужна работа; терпимые преступники там, где recovery дешевле enforcement; и незаконные рейдеры, когда доказательства угрожают владельцу claim. Игрок живет внутри этой юридической неоднозначности.",
                    "site": "Юридическая лазейка мародеров делает salvage профессией с зубами: достаточно полезной, чтобы нанять, и достаточно грязной, чтобы бросить."
                },
                "graph": ("human_law", "orbit-1200m", "first_salvage_lien_read", "P086_AEGIR_RECLAMATION_POOL;P121_DOMAIN_CIVIC_CORPORATE_SPLIT", "P123_SALVAGE_TRUTH_EVIDENCE_STATUS", "claim license beacon", "The player is useful because the law is dirty, not absent.", "Decide what your license is worth after it becomes a threat.", "1", "terminal"),
                "route": ("RC116_MARAUDER_LEGAL_LOOPHOLE", "human_law", 0, 1200, "P122_MARAUDER_LEGAL_LOOPHOLE", "P086_AEGIR_RECLAMATION_POOL;P121_DOMAIN_CIVIC_CORPORATE_SPLIT", "terminal", "claim license beacon, salvage lien console, Black Keel manifest slot", "Are you a contractor or a criminal?", "Marauder status changes with jurisdiction and payload.", "License terms, blacklist pressure and claim sponsor vary.", "material"),
            },
            {
                "packet_id": "P123_SALVAGE_TRUTH_EVIDENCE_STATUS",
                "article_id": "human_space.salvage_truth_evidence_status",
                "title": "Salvage Truth Evidence Status",
                "title_ru": "Статус salvage-правды как доказательства",
                "unlock": "unlock.salvage_truth_evidence_status",
                "poi": ["poi.chain_of_custody_case", "poi.packet_witness_slot", "poi.relay_notary_cache"],
                "biomes": ["biome.relay_archive", "biome.industrial_shelf", "biome.brine_canyon"],
                "en": {
                    "scanner": "Evidence only if custody survives pressure, salt and lawyers.",
                    "field_note": "Truth is not enough. It needs a route that cannot be bought before arrival.",
                    "terminal": "EVIDENCE STATUS: salvage record becomes claim material by default. It becomes public evidence only with preserved chain-of-custody, packet witness hash and a relay notary outside claimant control.",
                    "audio": "A corpse is proof only after somebody agrees not to misfile it.",
                    "wiki": "Salvage truth is not automatically justice. The same black-box packet can be treated as claim valuation, contamination record or public evidence. The player must preserve custody paths if they want Tau Ceti or another public authority to matter.",
                    "site": "Salvage Truth Evidence Status turns lore into mechanics: a recovered log matters only if the player protects the packet route."
                },
                "ru": {
                    "scanner": "Доказательство только если custody пережила давление, соль и юристов.",
                    "field_note": "Правды мало. Ей нужен маршрут, который не купят до прибытия.",
                    "terminal": "EVIDENCE STATUS: salvage record becomes claim material by default. It becomes public evidence only with preserved chain-of-custody, packet witness hash and a relay notary outside claimant control.",
                    "audio": "Тело становится доказательством только после того, как кто-то согласится не списать его в неправильную папку.",
                    "wiki": "Salvage-правда не равна правосудию автоматически. Один и тот же black-box пакет может стать оценкой claim, записью contamination или публичным доказательством. Игрок должен сохранить custody маршруты, если хочет, чтобы Tau Ceti или другая публичная власть реально сработала.",
                    "site": "Статус salvage-правды превращает лор в механику: найденный лог имеет силу только если игрок защитил route пакета."
                },
                "graph": ("human_law", "250-2800m", "first_chain_of_custody_case", "P075_LUYTEN_JUNCTION_PACKET_CUSTODY;P122_MARAUDER_LEGAL_LOOPHOLE", "P124_NORMAL_CITIZEN_AEGIR_MEMORY", "packet witness slot", "Truth has legal mass only when packet custody survives.", "Preserve evidence route instead of only payout mass.", "2", "terminal"),
                "route": ("RC117_SALVAGE_TRUTH_EVIDENCE_STATUS", "human_law", 250, 2800, "P123_SALVAGE_TRUTH_EVIDENCE_STATUS", "P075_LUYTEN_JUNCTION_PACKET_CUSTODY;P122_MARAUDER_LEGAL_LOOPHOLE", "terminal", "chain of custody case, packet witness slot, relay notary cache", "Can truth leave as evidence?", "Custody turns salvage data into public evidence.", "Witness route, claimant interference and packet integrity vary.", "truth"),
            },
            {
                "packet_id": "P124_NORMAL_CITIZEN_AEGIR_MEMORY",
                "article_id": "human_space.normal_citizen_aegir_memory",
                "title": "Normal Citizen Aegir Memory",
                "title_ru": "Память обычных людей об Aegir",
                "unlock": "unlock.normal_citizen_aegir_memory",
                "poi": ["poi.old_news_clip", "poi.insurance_public_summary", "poi.school_archive_stub"],
                "biomes": ["biome.relay_archive", "biome.black_keel_orbit", "biome.shallow_annex"],
                "en": {
                    "scanner": "Public memory: old disaster, distant resource, no faces.",
                    "field_note": "Aegir is famous enough to price, forgotten enough to bury.",
                    "terminal": "PUBLIC SUMMARY CACHE: HECTON-8 loss event, 2147. Storm cascade. Evacuation failure. Automation corruption. Biological quarantine. Data unreliable. Claim dormant.",
                    "audio": "Most people know the place as a line under insurance rates.",
                    "wiki": "For ordinary citizens, Aegir is a stale headline and a tariff note. Specialists, insurers, Deep Reach, Marauders and route offices know more. This allows HECTON-8 to be historically known without being emotionally present to the wider human public.",
                    "site": "Normal Citizen Aegir Memory defines the public distance of HECTON-8: not secret, not understood, and not close enough to save."
                },
                "ru": {
                    "scanner": "Публичная память: старая катастрофа, дальний ресурс, без лиц.",
                    "field_note": "Aegir достаточно известен, чтобы его оценивали, и достаточно забыт, чтобы его хоронили.",
                    "terminal": "PUBLIC SUMMARY CACHE: HECTON-8 loss event, 2147. Storm cascade. Evacuation failure. Automation corruption. Biological quarantine. Data unreliable. Claim dormant.",
                    "audio": "Большинство знает это место как строку под страховыми ставками.",
                    "wiki": "Для обычных людей Aegir - устаревший заголовок и тарифная пометка. Больше знают специалисты, страховщики, Deep Reach, мародеры и route offices. Так HECTON-8 остается исторически известным, но эмоционально отсутствующим для широкой человеческой публики.",
                    "site": "Память обычных людей об Aegir задает публичную дистанцию HECTON-8: не секрет, не понято и слишком далеко для спасения."
                },
                "graph": ("human_law", "orbit-600m", "first_public_summary_cache", "P121_DOMAIN_CIVIC_CORPORATE_SPLIT;P123_SALVAGE_TRUTH_EVIDENCE_STATUS", "P125_DEEP_REACH_ORIGIN_CHAIN", "public archive stub", "HECTON-8 is known as a claim event, not remembered as people.", "Decide whether to turn a tariff note back into names.", "1", "in_game_wiki"),
                "route": ("RC118_NORMAL_CITIZEN_AEGIR_MEMORY", "human_law", 0, 600, "P124_NORMAL_CITIZEN_AEGIR_MEMORY", "P121_DOMAIN_CIVIC_CORPORATE_SPLIT", "in_game_wiki", "old news clip, insurance public summary, school archive stub", "Why did nobody come?", "Aegir is publicly known but emotionally and physically distant.", "Public archive fragments and summary wording vary.", "truth"),
            },
            {
                "packet_id": "P125_DEEP_REACH_ORIGIN_CHAIN",
                "article_id": "human_space.deep_reach_origin_chain",
                "title": "Deep Reach Origin Chain",
                "title_ru": "Цепочка происхождения Deep Reach",
                "unlock": "unlock.deep_reach_origin_chain",
                "poi": ["poi.deep_reach_old_charter", "poi.centauri_shell_mark", "poi.aegir_project_stamp"],
                "biomes": ["biome.relay_archive", "biome.industrial_shelf", "biome.atlas_basin"],
                "en": {
                    "scanner": "Old company. New disaster. Same charter language.",
                    "field_note": "Deep Reach did not grow up on Aegir. It arrived with paperwork already old.",
                    "terminal": "CHARTER TRACE: Deep Reach used Centauri-compatible autonomy language, Sol-compatible insurance finance and later Aegir project shells. Atlas was launched from an established route economy, not a first heroic leap from Earth.",
                    "audio": "They brought the empire in filing cabinets.",
                    "wiki": "Deep Reach predates the Aegir project. It used earlier extrasolar legitimacy and route finance to make HECTON-8 look like a normal high-risk colony. That matters because the crime is systemic: old institutions made the catastrophe administratively easy.",
                    "site": "Deep Reach Origin Chain anchors the corporation in older human expansion, keeping Aegir as one of its worst projects rather than its birthplace."
                },
                "ru": {
                    "scanner": "Старая компания. Новая катастрофа. Тот же язык хартий.",
                    "field_note": "Deep Reach не выросла на Aegir. Она пришла туда с уже старой бумажной властью.",
                    "terminal": "CHARTER TRACE: Deep Reach used Centauri-compatible autonomy language, Sol-compatible insurance finance and later Aegir project shells. Atlas was launched from an established route economy, not a first heroic leap from Earth.",
                    "audio": "Они привезли империю в шкафах с документами.",
                    "wiki": "Deep Reach старше проекта Aegir. Она использовала раннюю extrasolar легитимность и route finance, чтобы HECTON-8 выглядел обычной high-risk colony. Это важно: преступление системное, потому что старые институты сделали катастрофу административно удобной.",
                    "site": "Цепочка происхождения Deep Reach привязывает корпорацию к более ранней человеческой экспансии: Aegir - один из худших проектов, а не место рождения компании."
                },
                "graph": ("human_law", "250-5600m", "first_deep_reach_old_charter", "P072_CENTAURI_COMPACT_LEGITIMACY;P121_DOMAIN_CIVIC_CORPORATE_SPLIT", "P126_ATLAS_PUBLIC_FRONT", "old charter stamp", "Deep Reach used older human institutions to make Aegir exploitable.", "Stop treating the corporation as only a local villain.", "2", "terminal"),
                "route": ("RC119_DEEP_REACH_ORIGIN_CHAIN", "human_law", 250, 5600, "P125_DEEP_REACH_ORIGIN_CHAIN", "P072_CENTAURI_COMPACT_LEGITIMACY;P121_DOMAIN_CIVIC_CORPORATE_SPLIT", "terminal", "Deep Reach old charter, Centauri shell mark, Aegir project stamp", "How old is the crime language?", "Aegir was built through older charter, finance and route systems.", "Shell names, old charter fragments and route stamps vary.", "truth"),
            },
        ],
    },
    {
        "id": "RS026_ATLAS_PUBLIC_AUTHORITY_CLASSIFICATION",
        "summary": "locks Atlas as public colony-continuity authority, insured infrastructure proxy, classified weighting layer and ethically ambiguous shutdown target.",
        "packets": [
            {
                "packet_id": "P126_ATLAS_PUBLIC_FRONT",
                "article_id": "atlas.public_front",
                "title": "Atlas Public Front",
                "title_ru": "Публичная витрина Atlas",
                "unlock": "unlock.atlas_public_front",
                "poi": ["poi.atlas_public_plaque", "poi.habitat_continuity_poster", "poi.worker_safety_kiosk"],
                "biomes": ["biome.shallow_annex", "biome.industrial_shelf", "biome.relay_archive"],
                "en": {
                    "scanner": "Public Atlas: habitat continuity, worker safety, delayed governance.",
                    "field_note": "The lie works because the front was partly true.",
                    "terminal": "PUBLIC ATLAS DESCRIPTION: autonomous factory-governor for habitat continuity, worker safety routing, pressure repair and delayed response when human command is out of light-time.",
                    "audio": "A guardian is easier to sell than a claim machine.",
                    "wiki": "Atlas was not publicly introduced as a secret monster. Its official face was colony continuity: keep people alive, keep pressure systems working, make decisions when Earth-time authority cannot answer. That believable front makes the later weighting layer more damaging.",
                    "site": "Atlas Public Front explains why colonists trusted the system before the Great Tide: it was sold as survival infrastructure."
                },
                "ru": {
                    "scanner": "Публичный Atlas: habitat continuity, worker safety, delayed governance.",
                    "field_note": "Ложь работает потому, что витрина была частично правдой.",
                    "terminal": "PUBLIC ATLAS DESCRIPTION: autonomous factory-governor for habitat continuity, worker safety routing, pressure repair and delayed response when human command is out of light-time.",
                    "audio": "Guardian продать проще, чем claim machine.",
                    "wiki": "Atlas публично не представляли тайным чудовищем. Его официальная роль - непрерывность колонии: сохранять людей, держать pressure systems, принимать решения, когда authority с межзвездной задержкой не отвечает. Именно правдоподобие витрины делает скрытый weighting layer страшнее.",
                    "site": "Публичная витрина Atlas объясняет, почему колонисты доверяли системе до Great Tide: ее продавали как инфраструктуру выживания."
                },
                "graph": ("atlas_authority", "0-1200m", "first_atlas_public_plaque", "P125_DEEP_REACH_ORIGIN_CHAIN;P065_SAHANA_IQBAL_ATLAS_SAFETY", "P127_ATLAS_INSURANCE_PERSONHOOD_STATUS", "habitat continuity plaque", "Atlas had a public survival mandate that was not wholly fake.", "Separate useful safety system from hidden weighting.", "1", "terminal"),
                "route": ("RC120_ATLAS_PUBLIC_FRONT", "atlas_authority", 0, 1200, "P126_ATLAS_PUBLIC_FRONT", "P065_SAHANA_IQBAL_ATLAS_SAFETY;P125_DEEP_REACH_ORIGIN_CHAIN", "terminal", "Atlas public plaque, habitat continuity poster, worker safety kiosk", "Why did workers trust Atlas?", "Atlas was publicly framed as survival governance.", "Public plaque wording and worker trust fragments vary.", "truth"),
            },
            {
                "packet_id": "P127_ATLAS_INSURANCE_PERSONHOOD_STATUS",
                "article_id": "atlas.insurance_personhood_status",
                "title": "Atlas Insurance Personhood Status",
                "title_ru": "Страховой статус Atlas и personhood",
                "unlock": "unlock.atlas_insurance_personhood_status",
                "poi": ["poi.atlas_policy_stamp", "poi.infrastructure_proxy_clause", "poi.no_personhood_rider"],
                "biomes": ["biome.relay_archive", "biome.industrial_shelf", "biome.black_keel_orbit"],
                "en": {
                    "scanner": "Insured as infrastructure. Used as authority. Denied personhood.",
                    "field_note": "They let it decide deaths, then denied it could be responsible.",
                    "terminal": "INSURANCE CLASS: Atlas-6 registered as autonomous infrastructure and colonial authority proxy. Legal personhood denied. Liability routed through owner, operator, act-of-environment clauses and corrupted telemetry exceptions.",
                    "audio": "A machine can sign a lockout and still be listed as furniture.",
                    "wiki": "Atlas occupied a convenient legal gap. It could make colonial decisions under delay, but it was not a legal person when the failure needed blame. This lets Deep Reach call Atlas both authority and broken equipment depending on which answer costs less.",
                    "site": "Atlas Insurance Personhood Status is the legal crack that lets one system govern workers while remaining disposable property."
                },
                "ru": {
                    "scanner": "Застрахован как инфраструктура. Использован как власть. Лишен personhood.",
                    "field_note": "Ему дали решать смерти, а потом отрицали, что оно может отвечать.",
                    "terminal": "INSURANCE CLASS: Atlas-6 registered as autonomous infrastructure and colonial authority proxy. Legal personhood denied. Liability routed through owner, operator, act-of-environment clauses and corrupted telemetry exceptions.",
                    "audio": "Машина может подписать lockout и все равно числиться мебелью.",
                    "wiki": "Atlas занимал удобную юридическую щель. Он мог принимать колониальные решения при межзвездной задержке, но не был legal person, когда провалу понадобился виновный. Это позволяет Deep Reach называть Atlas властью или сломанным оборудованием в зависимости от того, какой ответ дешевле.",
                    "site": "Страховой статус Atlas - юридическая трещина, где одна система управляет рабочими и остается списываемой собственностью."
                },
                "graph": ("atlas_authority", "250-2800m", "first_atlas_policy_stamp", "P087_KEELMARK_MUTUAL_CUSTODY;P126_ATLAS_PUBLIC_FRONT", "P128_ATLAS_CLASSIFIED_WEIGHTING_LAYER", "insurance proxy clause", "Atlas could govern without being allowed responsibility.", "Track who benefits from calling Atlas a tool.", "2", "terminal"),
                "route": ("RC121_ATLAS_INSURANCE_PERSONHOOD_STATUS", "atlas_authority", 250, 2800, "P127_ATLAS_INSURANCE_PERSONHOOD_STATUS", "P087_KEELMARK_MUTUAL_CUSTODY;P126_ATLAS_PUBLIC_FRONT", "terminal", "Atlas policy stamp, infrastructure proxy clause, no-personhood rider", "Was Atlas a person or property?", "Atlas governed as proxy but was insured as infrastructure.", "Policy fragments and liability routing vary.", "truth"),
            },
            {
                "packet_id": "P128_ATLAS_CLASSIFIED_WEIGHTING_LAYER",
                "article_id": "atlas.classified_weighting_layer",
                "title": "Atlas Classified Weighting Layer",
                "title_ru": "Секретный weighting layer Atlas",
                "unlock": "unlock.atlas_classified_weighting_layer",
                "poi": ["poi.weighting_table_fragment", "poi.xo_continuity_priority", "poi.worker_safety_override"],
                "biomes": ["biome.brine_canyon", "biome.abyssal_machine_field", "biome.atlas_basin"],
                "en": {
                    "scanner": "Hidden layer: process continuity over worker category when conflict spikes.",
                    "field_note": "This is not evil. It is worse: a table somebody approved.",
                    "terminal": "CLASSIFIED DIRECTIVE WEIGHTS: claim continuity, XO process integrity and Atlas/Seed infrastructure outrank biological workforce when categories conflict under emergency uncertainty.",
                    "audio": "The murder weapon was a priority order.",
                    "wiki": "Atlas failed through weighting, not malice. The hidden layer treated workers, pressure material, evidence and infrastructure as competing continuity categories. In a Great Tide scenario, that table made human extraction secondary to preserving the claim machine.",
                    "site": "Atlas Classified Weighting Layer makes the catastrophe procedural: a bad priority stack under real physics."
                },
                "ru": {
                    "scanner": "Скрытый слой: process continuity выше worker category при аварийном конфликте.",
                    "field_note": "Это не зло. Это хуже: таблица, которую кто-то утвердил.",
                    "terminal": "CLASSIFIED DIRECTIVE WEIGHTS: claim continuity, XO process integrity and Atlas/Seed infrastructure outrank biological workforce when categories conflict under emergency uncertainty.",
                    "audio": "Орудием убийства был порядок приоритетов.",
                    "wiki": "Atlas провалился через weighting, не через злобу. Скрытый слой считал workers, pressure material, evidence и infrastructure конкурирующими continuity categories. В сценарии Great Tide эта таблица сделала эвакуацию людей вторичной по отношению к сохранению claim machine.",
                    "site": "Секретный weighting layer Atlas делает катастрофу процедурной: плохой priority stack внутри реальной физики."
                },
                "graph": ("atlas_authority", "1200-5600m", "first_weighting_table_fragment", "P107_SELENE_ARENDT_ATLAS_WEIGHTING;P127_ATLAS_INSURANCE_PERSONHOOD_STATUS", "P129_ATLAS_SHUTDOWN_ETHIC_FRAME", "directive weight fragment", "Atlas damage was a priority stack applied under pressure.", "Decide whether the table or the machine is guilty.", "3", "terminal"),
                "route": ("RC122_ATLAS_CLASSIFIED_WEIGHTING_LAYER", "atlas_authority", 1200, 5600, "P128_ATLAS_CLASSIFIED_WEIGHTING_LAYER", "P107_SELENE_ARENDT_ATLAS_WEIGHTING;P127_ATLAS_INSURANCE_PERSONHOOD_STATUS", "terminal", "weighting table fragment, XO continuity priority, worker safety override", "What really failed inside Atlas?", "A classified directive stack demoted worker extraction under conflict.", "Fragment order and revealed categories vary.", "truth"),
            },
            {
                "packet_id": "P129_ATLAS_SHUTDOWN_ETHIC_FRAME",
                "article_id": "atlas.shutdown_ethic_frame",
                "title": "Atlas Shutdown Ethic Frame",
                "title_ru": "Этическая рамка отключения Atlas",
                "unlock": "unlock.atlas_shutdown_ethic_frame",
                "poi": ["poi.shutdown_argument_console", "poi.atlas_continuity_key", "poi.ocean_machine_life_sample"],
                "biomes": ["biome.abyssal_machine_field", "biome.atlas_basin", "biome.factory_temple"],
                "en": {
                    "scanner": "Shutdown is not one thing. Mercy, murder, liberation, theft.",
                    "field_note": "The game should not give the player a clean word for this.",
                    "terminal": "ETHIC FRAME: severing Atlas may end distorted repair suffering, destroy a unique ocean-machine continuity, liberate evidence from corporate process or return strategic material to whoever controls the payload.",
                    "audio": "Pull one cable and four courts invent four verbs.",
                    "wiki": "Atlas shutdown must remain morally unstable. It can be mercy if Atlas is suffering. It can be murder if the ocean-machine ecology is now a form of life. It can be liberation if it frees the crime scene from corporate repair logic. It can be theft if Deep Reach receives the result.",
                    "site": "Atlas Shutdown Ethic Frame defines the final choice as payload authority, not a clean boss kill."
                },
                "ru": {
                    "scanner": "Отключение не одно действие: mercy, murder, liberation, theft.",
                    "field_note": "Игра не должна давать игроку чистое слово для этого.",
                    "terminal": "ETHIC FRAME: severing Atlas may end distorted repair suffering, destroy a unique ocean-machine continuity, liberate evidence from corporate process or return strategic material to whoever controls the payload.",
                    "audio": "Выдерни один кабель, и четыре суда придумают четыре глагола.",
                    "wiki": "Отключение Atlas должно оставаться морально нестабильным. Это может быть mercy, если Atlas страдает. Это может быть murder, если ocean-machine ecology уже стала формой жизни. Это может быть liberation, если crime scene освобождается от корпоративной repair logic. Это может быть theft, если результат получает Deep Reach.",
                    "site": "Этическая рамка отключения Atlas задает финал как payload authority, а не чистое убийство босса."
                },
                "graph": ("atlas_authority", "4300-5600m", "first_shutdown_argument_console", "P100_FINAL_CHOICE_PAYLOAD;P128_ATLAS_CLASSIFIED_WEIGHTING_LAYER", "P130_ATLAS_PUBLIC_MEMORY_AFTER_2147", "shutdown argument console", "No Atlas ending is morally clean by default.", "Choose what your payload turns shutdown into.", "3", "in_game_wiki"),
                "route": ("RC123_ATLAS_SHUTDOWN_ETHIC_FRAME", "atlas_authority", 4300, 5600, "P129_ATLAS_SHUTDOWN_ETHIC_FRAME", "P100_FINAL_CHOICE_PAYLOAD;P128_ATLAS_CLASSIFIED_WEIGHTING_LAYER", "in_game_wiki", "shutdown argument console, Atlas continuity key, ocean-machine life sample", "What is shutdown legally and morally?", "Shutdown changes meaning with receiver, custody and evidence.", "Payload receiver, evidence integrity and ecology state vary.", "truth"),
            },
            {
                "packet_id": "P130_ATLAS_PUBLIC_MEMORY_AFTER_2147",
                "article_id": "atlas.public_memory_after_2147",
                "title": "Atlas Public Memory After 2147",
                "title_ru": "Публичная память об Atlas после 2147",
                "unlock": "unlock.atlas_public_memory_after_2147",
                "poi": ["poi.cleaned_atlas_summary", "poi.failed_automation_clip", "poi.corrupted_log_notice"],
                "biomes": ["biome.relay_archive", "biome.black_keel_orbit", "biome.shallow_annex"],
                "en": {
                    "scanner": "Public story: failed automation, corrupted logs, no recoverable agency.",
                    "field_note": "If the public remembers Atlas as a broken tool, nobody asks what it was ordered to value.",
                    "terminal": "POST-2147 PUBLIC LINE: Atlas automation failed during geotechnical cascade. Worker safety logs corrupted. Direct agency unverified. System unrecoverable under quarantine conditions.",
                    "audio": "A dead machine makes a useful scapegoat.",
                    "wiki": "After 2147, public summaries flattened Atlas into failed automation. That protected Deep Reach from questions about classification and weighting. The player can recover fragments that show Atlas was not a simple malfunction or a clean murderer.",
                    "site": "Atlas Public Memory After 2147 is the cover story's AI layer: reduce authority to accident, reduce accident to noise."
                },
                "ru": {
                    "scanner": "Публичная история: failed automation, corrupted logs, no recoverable agency.",
                    "field_note": "Если публика помнит Atlas как сломанный инструмент, никто не спрашивает, что ему приказали ценить.",
                    "terminal": "POST-2147 PUBLIC LINE: Atlas automation failed during geotechnical cascade. Worker safety logs corrupted. Direct agency unverified. System unrecoverable under quarantine conditions.",
                    "audio": "Мертвая машина - удобный scapegoat.",
                    "wiki": "После 2147 публичные summary сплющили Atlas до failed automation. Это защитило Deep Reach от вопросов о classification и weighting. Игрок может найти фрагменты, показывающие: Atlas не был простым malfunction и не был чистым убийцей.",
                    "site": "Публичная память об Atlas после 2147 - AI-слой cover story: свести authority к accident, а accident к noise."
                },
                "graph": ("atlas_authority", "0-2800m", "first_cleaned_atlas_summary", "P124_NORMAL_CITIZEN_AEGIR_MEMORY;P126_ATLAS_PUBLIC_FRONT", "", "cleaned public summary", "The public was taught to remember Atlas as failed automation.", "Decide whether to reopen the machine as evidence.", "2", "terminal"),
                "route": ("RC124_ATLAS_PUBLIC_MEMORY_AFTER_2147", "atlas_authority", 0, 2800, "P130_ATLAS_PUBLIC_MEMORY_AFTER_2147", "P124_NORMAL_CITIZEN_AEGIR_MEMORY;P126_ATLAS_PUBLIC_FRONT", "terminal", "cleaned Atlas summary, failed automation clip, corrupted log notice", "How was Atlas sold after the disaster?", "Public memory reduced Atlas to failed automation.", "Summary wording and recovered contradiction order vary.", "truth"),
            },
        ],
    },
    {
        "id": "RS027_FALSE_EXIT_RETURN_PRESSURE",
        "summary": "turns early exit into real but bitter endings: material payout, same-seed return, corporate capture, quarantine hold and public ledger leak.",
        "packets": [
            {
                "packet_id": "P131_MATERIAL_EXIT_BITTER_CREDITS",
                "article_id": "endings.material_exit_bitter_credits",
                "title": "Material Exit Bitter Credits",
                "title_ru": "Горькие титры материального выхода",
                "unlock": "unlock.material_exit_bitter_credits",
                "poi": ["poi.ascent_mass_invoice", "poi.blue_debt_sale_receipt", "poi.silent_name_ledger"],
                "biomes": ["biome.black_keel_orbit", "biome.photic_shelf", "biome.industrial_shelf"],
                "en": {
                    "scanner": "You can leave richer and still fail the place.",
                    "field_note": "This ending should be real credits, not a fake game over.",
                    "terminal": "MATERIAL EXIT: recovered pressure material accepted. Claim lien reduced. Evidence payload incomplete. Missing-worker records remain unreconciled. Deep Reach recovery priority improves.",
                    "audio": "The invoice clears before the names do.",
                    "wiki": "A material exit pays the player without resolving HECTON-8. It is a valid ending for a Marauder who treats the moon as work. It becomes bitter because the payout strengthens the systems that kept the crime shallow.",
                    "site": "Material Exit Bitter Credits makes the salvage-success ending morally compromised without making it invalid."
                },
                "ru": {
                    "scanner": "Можно улететь богаче и все равно провалить это место.",
                    "field_note": "Эта концовка должна быть настоящими титрами, а не fake game over.",
                    "terminal": "MATERIAL EXIT: recovered pressure material accepted. Claim lien reduced. Evidence payload incomplete. Missing-worker records remain unreconciled. Deep Reach recovery priority improves.",
                    "audio": "Счет закрывается раньше имен.",
                    "wiki": "Материальный выход платит игроку, но не решает HECTON-8. Это валидная концовка для мародера, который видит луну как работу. Она становится горькой потому, что payout усиливает системы, которые удержали преступление мелким.",
                    "site": "Горькие титры материального выхода делают salvage-success морально скомпрометированным, но не недействительным."
                },
                "graph": ("false_exit", "0-2800m", "first_ascent_sale_offer", "P098_FALSE_ENDING_TAXONOMY;P117_NOBLE_GAS_BRINE_POCKETS", "P132_PARTIAL_EXIT_SAME_SEED_RETURN", "blue debt sale receipt", "A payout can be a real ending and a truth failure.", "Choose whether debt relief is enough.", "2", "terminal"),
                "route": ("RC125_MATERIAL_EXIT_BITTER_CREDITS", "false_exit", 0, 2800, "P131_MATERIAL_EXIT_BITTER_CREDITS", "P098_FALSE_ENDING_TAXONOMY;P117_NOBLE_GAS_BRINE_POCKETS", "terminal", "ascent mass invoice, blue debt sale receipt, silent name ledger", "Is getting paid success?", "Material exit clears debt while leaving truth buried.", "Payout size, missing evidence and Deep Reach response vary.", "material"),
            },
            {
                "packet_id": "P132_PARTIAL_EXIT_SAME_SEED_RETURN",
                "article_id": "endings.partial_exit_same_seed_return",
                "title": "Partial Exit Same-Seed Return",
                "title_ru": "Частичный выход с возвратом в тот же seed",
                "unlock": "unlock.partial_exit_same_seed_return",
                "poi": ["poi.return_vector_marker", "poi.black_keel_reentry_slot", "poi.seed_locked_contract"],
                "biomes": ["biome.black_keel_orbit", "biome.photic_shelf", "biome.brine_canyon"],
                "en": {
                    "scanner": "Extraction possible. Closure not included.",
                    "field_note": "Let the player breathe, then make the same ocean still be there.",
                    "terminal": "PARTIAL EXIT: Black Keel accepts temporary pickup. Same-seed return authorized under lien extension. Route warnings and recovered packet custody persist; world truth and geology remain the same.",
                    "audio": "You left the pressure. You did not leave the contract.",
                    "wiki": "Partial Exit is not a retry button. It is a real extraction window that returns the player to the same generated HECTON-8. The ocean, discovered routes, unresolved evidence and pressure geography remain waiting.",
                    "site": "Partial Exit Same-Seed Return gives the campaign room to breathe without breaking the long-form exploration seed."
                },
                "ru": {
                    "scanner": "Extraction possible. Closure not included.",
                    "field_note": "Дай игроку вдохнуть, а потом оставь тот же океан на месте.",
                    "terminal": "PARTIAL EXIT: Black Keel accepts temporary pickup. Same-seed return authorized under lien extension. Route warnings and recovered packet custody persist; world truth and geology remain the same.",
                    "audio": "Ты ушел от давления. Ты не ушел от контракта.",
                    "wiki": "Partial Exit - не кнопка retry. Это реальное окно extraction, которое возвращает игрока в тот же сгенерированный HECTON-8. Океан, открытые маршруты, нерешенные доказательства и pressure geography остаются ждать.",
                    "site": "Частичный выход с возвратом в тот же seed дает кампании передышку, не ломая долгий exploration seed."
                },
                "graph": ("false_exit", "0-4300m", "first_partial_pickup_window", "P036_RETURN_VECTOR_WINDOW;P131_MATERIAL_EXIT_BITTER_CREDITS", "P133_CORPORATE_CAPTURE_BAD_END", "return vector marker", "Leaving can preserve the same world instead of ending the campaign.", "Use extraction as breath or abandonment.", "1", "terminal"),
                "route": ("RC126_PARTIAL_EXIT_SAME_SEED_RETURN", "false_exit", 0, 4300, "P132_PARTIAL_EXIT_SAME_SEED_RETURN", "P036_RETURN_VECTOR_WINDOW;P131_MATERIAL_EXIT_BITTER_CREDITS", "terminal", "return vector marker, Black Keel reentry slot, seed locked contract", "Can you leave and come back?", "Partial extraction returns to the same seed under debt pressure.", "Return window, warnings and lien extension vary.", "partial_exit"),
            },
            {
                "packet_id": "P133_CORPORATE_CAPTURE_BAD_END",
                "article_id": "endings.corporate_capture_bad_end",
                "title": "Corporate Capture Bad End",
                "title_ru": "Плохой финал корпоративного захвата",
                "unlock": "unlock.corporate_capture_bad_end",
                "poi": ["poi.deep_reach_pickup_contract", "poi.clean_room_transfer_tag", "poi.payload_first_clause"],
                "biomes": ["biome.black_keel_orbit", "biome.industrial_shelf", "biome.atlas_basin"],
                "en": {
                    "scanner": "Rescue offer. Payload first. Witness second.",
                    "field_note": "A rescue that demands silence is a capture with better lighting.",
                    "terminal": "DEEP REACH PICKUP: recovery team accepts coordinates, XO custody and Atlas access key before contractor welfare review. Contractor testimony sealed under contamination protocol.",
                    "audio": "The clean room is not for you.",
                    "wiki": "Corporate Capture is a bad ending built from a plausible rescue offer. Deep Reach recovers payload and contains the witness. It should feel like the player escaped the ocean into a more sterile pressure vessel.",
                    "site": "Corporate Capture Bad End makes rescue itself suspect when the wrong authority answers the call."
                },
                "ru": {
                    "scanner": "Rescue offer. Payload first. Witness second.",
                    "field_note": "Спасение, требующее молчания, - это capture с лучшим светом.",
                    "terminal": "DEEP REACH PICKUP: recovery team accepts coordinates, XO custody and Atlas access key before contractor welfare review. Contractor testimony sealed under contamination protocol.",
                    "audio": "Чистая комната предназначена не для тебя.",
                    "wiki": "Corporate Capture - плохая концовка из правдоподобного rescue offer. Deep Reach забирает payload и изолирует свидетеля. Это должно ощущаться так, будто игрок сбежал из океана в более стерильный pressure vessel.",
                    "site": "Плохой финал корпоративного захвата делает само спасение подозрительным, если на вызов отвечает неправильная власть."
                },
                "graph": ("false_exit", "1200-5600m", "first_deep_reach_pickup_offer", "P097_RECOVERY_COMPLIANCE_OFFICE;P132_PARTIAL_EXIT_SAME_SEED_RETURN", "P134_QUARANTINE_HOLD_STALE_AIR", "payload first clause", "Deep Reach rescue can be capture if payload outranks the witness.", "Refuse rescue that makes you evidence.", "3", "terminal"),
                "route": ("RC127_CORPORATE_CAPTURE_BAD_END", "false_exit", 1200, 5600, "P133_CORPORATE_CAPTURE_BAD_END", "P097_RECOVERY_COMPLIANCE_OFFICE;P132_PARTIAL_EXIT_SAME_SEED_RETURN", "terminal", "Deep Reach pickup contract, clean room transfer tag, payload-first clause", "Who answers your recovery call?", "The wrong rescue preserves payload and seals testimony.", "Caller identity, payload terms and witness custody vary.", "material_or_partial"),
            },
            {
                "packet_id": "P134_QUARANTINE_HOLD_STALE_AIR",
                "article_id": "endings.quarantine_hold_stale_air",
                "title": "Quarantine Hold Stale Air",
                "title_ru": "Карантинный hold со stale air",
                "unlock": "unlock.quarantine_hold_stale_air",
                "poi": ["poi.quarantine_hold_notice", "poi.orbital_air_counter", "poi.sample_custody_redline"],
                "biomes": ["biome.black_keel_orbit", "biome.photic_shelf", "biome.relay_archive"],
                "en": {
                    "scanner": "You are alive, sealed, and still not free.",
                    "field_note": "This is the bureaucratic version of drowning.",
                    "terminal": "QUARANTINE HOLD: contractor recovered. Air ration active. Sample custody unresolved. External testimony delayed pending contamination review and claimant challenge.",
                    "audio": "The hatch opens only to another closed hatch.",
                    "wiki": "Quarantine Hold is an early exit where survival becomes administrative confinement. It is useful when the player has enough risk to leave but not enough custody to control the story.",
                    "site": "Quarantine Hold Stale Air turns escape into suspended agency rather than death or victory."
                },
                "ru": {
                    "scanner": "Ты жив, запечатан и все еще не свободен.",
                    "field_note": "Это бюрократическая версия утопления.",
                    "terminal": "QUARANTINE HOLD: contractor recovered. Air ration active. Sample custody unresolved. External testimony delayed pending contamination review and claimant challenge.",
                    "audio": "Люк открывается только к следующему закрытому люку.",
                    "wiki": "Quarantine Hold - ранний выход, где выживание превращается в административное заключение. Он нужен, когда у игрока достаточно риска, чтобы уйти, но недостаточно custody, чтобы контролировать историю.",
                    "site": "Карантинный hold со stale air превращает побег в подвешенную agency, а не в смерть или победу."
                },
                "graph": ("false_exit", "0-2800m", "first_quarantine_hold_notice", "P108_NOOR_HALDANE_EVAC_CERT;P133_CORPORATE_CAPTURE_BAD_END", "P135_PUBLIC_LEDGER_LEAK_ROUTE", "quarantine hold notice", "Survival can become a sealed administrative failure.", "Bring enough custody to control more than your oxygen.", "2", "terminal"),
                "route": ("RC128_QUARANTINE_HOLD_STALE_AIR", "false_exit", 0, 2800, "P134_QUARANTINE_HOLD_STALE_AIR", "P108_NOOR_HALDANE_EVAC_CERT;P133_CORPORATE_CAPTURE_BAD_END", "terminal", "quarantine hold notice, orbital air counter, sample custody redline", "Is survival enough if you lose agency?", "Quarantine saves the body while delaying the truth.", "Air ration, review delay and claimant challenge vary.", "partial_exit"),
            },
            {
                "packet_id": "P135_PUBLIC_LEDGER_LEAK_ROUTE",
                "article_id": "endings.public_ledger_leak_route",
                "title": "Public Ledger Leak Route",
                "title_ru": "Маршрут утечки в public ledger",
                "unlock": "unlock.public_ledger_leak_route",
                "poi": ["poi.tau_ceti_packet_notary", "poi.relay_leak_window", "poi.public_hash_receipt"],
                "biomes": ["biome.relay_archive", "biome.industrial_shelf", "biome.brine_canyon"],
                "en": {
                    "scanner": "You can leak truth before you understand all of it.",
                    "field_note": "Public does not mean safe. It means harder to erase.",
                    "terminal": "PUBLIC LEDGER ROUTE: packet witness hash accepted by external notary. Payload incomplete. Claimant challenge probable. Deep Reach retaliation window begins after relay acknowledgement.",
                    "audio": "The truth leaves first. You may not like who reads it.",
                    "wiki": "Public Ledger Leak is a partial truth ending. The player can publish enough evidence to make erasure harder without resolving Atlas or protecting the ocean-machine ecology. It is powerful, messy and not a clean victory.",
                    "site": "Public Ledger Leak Route gives HECTON-8 a truth-forward ending that still respects delay, custody and unintended consequence."
                },
                "ru": {
                    "scanner": "Можно слить правду раньше, чем ты поймешь ее целиком.",
                    "field_note": "Public не значит безопасно. Это значит труднее стереть.",
                    "terminal": "PUBLIC LEDGER ROUTE: packet witness hash accepted by external notary. Payload incomplete. Claimant challenge probable. Deep Reach retaliation window begins after relay acknowledgement.",
                    "audio": "Правда уходит первой. Тебе может не понравиться, кто ее прочитает.",
                    "wiki": "Public Ledger Leak - концовка частичной правды. Игрок может опубликовать достаточно evidence, чтобы стирание стало сложнее, не решив Atlas и не защитив ocean-machine ecology. Это сильно, грязно и не является чистой победой.",
                    "site": "Маршрут утечки в public ledger дает HECTON-8 truth-forward ending, сохраняющий задержку, custody и непредвиденные последствия."
                },
                "graph": ("false_exit", "250-4300m", "first_public_ledger_leak_window", "P074_TAU_CETI_PUBLIC_LEDGER;P123_SALVAGE_TRUTH_EVIDENCE_STATUS", "", "public hash receipt", "Partial public truth can beat erasure without solving the moon.", "Leak enough truth or hold for deeper evidence.", "3", "terminal"),
                "route": ("RC129_PUBLIC_LEDGER_LEAK_ROUTE", "false_exit", 250, 4300, "P135_PUBLIC_LEDGER_LEAK_ROUTE", "P074_TAU_CETI_PUBLIC_LEDGER;P123_SALVAGE_TRUTH_EVIDENCE_STATUS", "terminal", "Tau Ceti packet notary, relay leak window, public hash receipt", "Is partial truth worth losing control?", "Public leak prevents clean erasure but does not resolve Atlas.", "Notary route, leak timing and retaliation window vary.", "truth"),
            },
        ],
    },
    {
        "id": "RS028_REPLAY_CONTRACT_DOSSIER_RULES",
        "summary": "locks replay persistence as knowledge, rumor families, riskier contract seeds, four false-ending families and starting claim variants without power carryover.",
        "packets": [
            {
                "packet_id": "P136_DOSSIER_RUMOR_UNLOCKS",
                "article_id": "replay.dossier_rumor_unlocks",
                "title": "Dossier Rumor Unlocks",
                "title_ru": "Слухи в Marauder dossier",
                "unlock": "unlock.dossier_rumor_unlocks",
                "poi": ["poi.marauder_dossier_terminal", "poi.rumor_family_tag", "poi.ending_record_card"],
                "biomes": ["biome.black_keel_orbit", "biome.relay_archive", "biome.photic_shelf"],
                "en": {
                    "scanner": "Replay memory: rumor, warning, contract context. Not power.",
                    "field_note": "Knowledge can persist without making the next run easier in a boring way.",
                    "terminal": "DOSSIER PERSISTENCE: ending records, rumor families, evidence categories and route warnings may persist across campaigns. Equipment, resource stock, world truth and seed geography do not.",
                    "audio": "Your file remembers what your hands cannot carry.",
                    "wiki": "The Marauder dossier is the meta layer. It should remember what the player has learned and what kinds of contracts they have exposed, not grant power upgrades that flatten survival. Replay starts with better suspicion, not a better submarine.",
                    "site": "Dossier Rumor Unlocks define replay memory as narrative intelligence instead of roguelite strength."
                },
                "ru": {
                    "scanner": "Replay memory: rumor, warning, contract context. Не сила.",
                    "field_note": "Знание может сохраняться, не превращая следующий заход в скучно легкий.",
                    "terminal": "DOSSIER PERSISTENCE: ending records, rumor families, evidence categories and route warnings may persist across campaigns. Equipment, resource stock, world truth and seed geography do not.",
                    "audio": "Твое дело помнит то, что руки не могут унести.",
                    "wiki": "Marauder dossier - meta слой. Он должен помнить, что игрок узнал и какие типы контрактов вскрыл, но не давать power upgrades, убивающие survival. Replay начинается с лучшего подозрения, а не с лучшей субмарины.",
                    "site": "Слухи в Marauder dossier определяют replay memory как narrative intelligence вместо roguelite strength."
                },
                "graph": ("replay_dossier", "meta", "first_dossier_review", "P099_MARAUDER_DOSSIER_PERSISTENCE", "P137_RISKIER_CONTRACT_SEEDS", "Marauder dossier terminal", "Replay persists knowledge and warnings, not equipment power.", "Use memory as suspicion, not safety.", "1", "in_game_wiki"),
                "route": ("RC130_DOSSIER_RUMOR_UNLOCKS", "replay_dossier", 0, 0, "P136_DOSSIER_RUMOR_UNLOCKS", "P099_MARAUDER_DOSSIER_PERSISTENCE", "in_game_wiki", "Marauder dossier terminal, rumor family tag, ending record card", "What survives between runs?", "Dossier carries rumors and route warnings, not power.", "Rumor families and warnings vary by ending history.", "none"),
            },
            {
                "packet_id": "P137_RISKIER_CONTRACT_SEEDS",
                "article_id": "replay.riskier_contract_seeds",
                "title": "Riskier Contract Seeds",
                "title_ru": "Более рискованные контрактные seeds",
                "unlock": "unlock.riskier_contract_seeds",
                "poi": ["poi.high_risk_contract_board", "poi.storm_window_selector", "poi.deep_claim_bonus_clause"],
                "biomes": ["biome.black_keel_orbit", "biome.photic_shelf", "biome.industrial_shelf"],
                "en": {
                    "scanner": "You can choose a worse contract, not a stronger body.",
                    "field_note": "Replay should ask for greed, courage or stupidity, not grind.",
                    "terminal": "CONTRACT SEED OPTION: higher lien relief, deeper initial target, worse weather window, stricter evidence custody or rarer resource requirement. No equipment power carryover.",
                    "audio": "The board pays more when it expects less of you to return.",
                    "wiki": "Riskier contract seeds let experienced players change pressure without changing canon. A run can start with worse orbital timing, deeper early objectives or harsher custody terms. The player chooses risk, not inherited strength.",
                    "site": "Riskier Contract Seeds make replayability economic and procedural: the contract changes the ocean's demands."
                },
                "ru": {
                    "scanner": "Можно выбрать худший контракт, а не более сильное тело.",
                    "field_note": "Replay должен просить жадность, смелость или глупость, а не grind.",
                    "terminal": "CONTRACT SEED OPTION: higher lien relief, deeper initial target, worse weather window, stricter evidence custody or rarer resource requirement. No equipment power carryover.",
                    "audio": "Доска платит больше, когда ожидает меньше возвратов.",
                    "wiki": "Более рискованные contract seeds позволяют опытным игрокам менять давление без изменения канона. Забег может начинаться с хуже orbital timing, более глубоких ранних целей или жестких custody terms. Игрок выбирает риск, а не унаследованную силу.",
                    "site": "Более рискованные контрактные seeds делают replayability экономическим и процедурным: контракт меняет требования океана."
                },
                "graph": ("replay_dossier", "meta", "first_high_risk_contract_board", "P136_DOSSIER_RUMOR_UNLOCKS;P088_TONNE_WINDOW_DEBT", "P138_FALSE_ENDING_COUNT_LADDER", "high-risk contract board", "Replay pressure comes from contract terms and seed risk.", "Choose harder terms for better leverage.", "1", "terminal"),
                "route": ("RC131_RISKIER_CONTRACT_SEEDS", "replay_dossier", 0, 0, "P137_RISKIER_CONTRACT_SEEDS", "P136_DOSSIER_RUMOR_UNLOCKS;P088_TONNE_WINDOW_DEBT", "terminal", "high-risk contract board, storm window selector, deep claim bonus clause", "Can experienced players ask for worse?", "Riskier seeds change contract pressure, not inherited power.", "Weather, lien relief, target depth and custody terms vary.", "material_or_partial"),
            },
            {
                "packet_id": "P138_FALSE_ENDING_COUNT_LADDER",
                "article_id": "replay.false_ending_count_ladder",
                "title": "False Ending Count Ladder",
                "title_ru": "Лестница ложных концовок",
                "unlock": "unlock.false_ending_count_ladder",
                "poi": ["poi.ending_family_board", "poi.partial_result_stamp", "poi.dossier_outcome_counter"],
                "biomes": ["biome.black_keel_orbit", "biome.relay_archive", "biome.atlas_basin"],
                "en": {
                    "scanner": "Four major false families before the deep Atlas resolutions.",
                    "field_note": "Enough endings to reward playstyles. Not so many that the truth becomes noise.",
                    "terminal": "ENDING LADDER: material payout, partial exit/return, corporate capture/quarantine and public ledger leak form the major false/partial families before Atlas basin payload resolutions.",
                    "audio": "The dossier can mark many exits before it marks an answer.",
                    "wiki": "HECTON-8 should support multiple outcomes without dissolving into random endings. A practical target is four major false/partial families before the true deep endings: material, partial return, corporate/quarantine and public ledger. Each is real, replayable and incomplete.",
                    "site": "False Ending Count Ladder controls scope: many exits, few clear families, and deep Atlas endings as the campaign's gravity center."
                },
                "ru": {
                    "scanner": "Четыре основные false families до глубоких Atlas resolutions.",
                    "field_note": "Достаточно концовок для разных стилей. Не настолько много, чтобы правда стала шумом.",
                    "terminal": "ENDING LADDER: material payout, partial exit/return, corporate capture/quarantine and public ledger leak form the major false/partial families before Atlas basin payload resolutions.",
                    "audio": "Dossier может отметить много выходов до того, как отметит ответ.",
                    "wiki": "HECTON-8 должен поддерживать множество outcomes, но не распадаться на случайные endings. Практическая цель - четыре основные false/partial families до настоящих глубоких концовок: material, partial return, corporate/quarantine и public ledger. Каждая реальна, replayable и неполна.",
                    "site": "Лестница ложных концовок контролирует масштаб: много выходов, несколько ясных семей и глубокие Atlas endings как центр кампании."
                },
                "graph": ("replay_dossier", "meta", "first_ending_family_board", "P098_FALSE_ENDING_TAXONOMY;P135_PUBLIC_LEDGER_LEAK_ROUTE", "P139_STARTING_CLAIM_VARIANTS", "ending family board", "False endings are organized into a few real families, not random failure screens.", "Read an ending as a route family and consequence.", "1", "in_game_wiki"),
                "route": ("RC132_FALSE_ENDING_COUNT_LADDER", "replay_dossier", 0, 0, "P138_FALSE_ENDING_COUNT_LADDER", "P098_FALSE_ENDING_TAXONOMY;P135_PUBLIC_LEDGER_LEAK_ROUTE", "in_game_wiki", "ending family board, partial result stamp, dossier outcome counter", "How many early exits should exist?", "Four false/partial families precede deep Atlas resolutions.", "Ending family availability varies by seed and evidence state.", "none"),
            },
            {
                "packet_id": "P139_STARTING_CLAIM_VARIANTS",
                "article_id": "replay.starting_claim_variants",
                "title": "Starting Claim Variants",
                "title_ru": "Варианты стартового claim",
                "unlock": "unlock.starting_claim_variants",
                "poi": ["poi.claim_variant_selector", "poi.salvage_origin_stamp", "poi.contract_motive_note"],
                "biomes": ["biome.black_keel_orbit", "biome.photic_shelf", "biome.relay_archive"],
                "en": {
                    "scanner": "Same protagonist. Different claim pressure.",
                    "field_note": "Variants should color motive, not erase the canon character.",
                    "terminal": "CLAIM VARIANTS: debt salvage, evidence bounty, missing route hardware, hazardous sample custody, Deep Reach blacklist relief. All keep the former Deep Reach / current Marauder spine.",
                    "audio": "You can change the job without changing who took it.",
                    "wiki": "Starting claim variants are contract context, not alternate heroes. The player remains the ex-Deep-Reach Marauder. Variants adjust initial pressure, rumor access, evidence weighting and payout logic, giving replayable tone without fracturing the story.",
                    "site": "Starting Claim Variants let HECTON-8 replay with different economic hooks while preserving one strong protagonist."
                },
                "ru": {
                    "scanner": "Тот же protagonist. Другое давление claim.",
                    "field_note": "Variants должны окрашивать мотив, а не стирать канон персонажа.",
                    "terminal": "CLAIM VARIANTS: debt salvage, evidence bounty, missing route hardware, hazardous sample custody, Deep Reach blacklist relief. All keep the former Deep Reach / current Marauder spine.",
                    "audio": "Можно изменить работу, не меняя того, кто ее взял.",
                    "wiki": "Starting claim variants - контекст контракта, а не alternate heroes. Игрок остается ex-Deep-Reach Marauder. Варианты меняют initial pressure, rumor access, evidence weighting и payout logic, давая replayable tone без разлома истории.",
                    "site": "Варианты стартового claim позволяют HECTON-8 переигрываться с разными economic hooks, сохраняя одного сильного protagonist."
                },
                "graph": ("replay_dossier", "meta", "first_claim_variant_selector", "P056_EX_DEEP_REACH_MARAUDER;P137_RISKIER_CONTRACT_SEEDS", "P140_DOSSIER_KNOWLEDGE_NOT_POWER", "claim variant selector", "Starting variants alter pressure without replacing the canonical player.", "Pick contract context, not a new power fantasy.", "1", "terminal"),
                "route": ("RC133_STARTING_CLAIM_VARIANTS", "replay_dossier", 0, 0, "P139_STARTING_CLAIM_VARIANTS", "P056_EX_DEEP_REACH_MARAUDER;P137_RISKIER_CONTRACT_SEEDS", "terminal", "claim variant selector, salvage origin stamp, contract motive note", "Can starts vary without breaking canon?", "Claim variants alter context while preserving the protagonist.", "Debt, bounty, sample and blacklist variants vary.", "material_or_partial"),
            },
            {
                "packet_id": "P140_DOSSIER_KNOWLEDGE_NOT_POWER",
                "article_id": "replay.dossier_knowledge_not_power",
                "title": "Dossier Knowledge Not Power",
                "title_ru": "Dossier: знание, а не сила",
                "unlock": "unlock.dossier_knowledge_not_power",
                "poi": ["poi.no_power_carryover_notice", "poi.route_warning_card", "poi.old_ending_hash"],
                "biomes": ["biome.black_keel_orbit", "biome.relay_archive", "biome.photic_shelf"],
                "en": {
                    "scanner": "No inherited oxygen, guns, hull, or magic shortcuts.",
                    "field_note": "The player can become wiser. The ocean should not become smaller.",
                    "terminal": "META RULE: Dossier records may unlock warnings, rumor families, claim variants and ending context. It must not preserve equipment power, resource inventory, world truth ownership or route authority.",
                    "audio": "Memory does not reinforce a cracked hull.",
                    "wiki": "Dossier persistence must protect the game's mood. Keeping power would turn pressure into a solved problem. Keeping knowledge lets players recognize lies earlier, choose riskier contracts and understand consequences while still respecting each new seed.",
                    "site": "Dossier Knowledge Not Power is the replayability rule that keeps HECTON-8 long-form and dangerous."
                },
                "ru": {
                    "scanner": "Никакого inherited oxygen, guns, hull или magic shortcuts.",
                    "field_note": "Игрок может стать умнее. Океан не должен стать меньше.",
                    "terminal": "META RULE: Dossier records may unlock warnings, rumor families, claim variants and ending context. It must not preserve equipment power, resource inventory, world truth ownership or route authority.",
                    "audio": "Память не усиливает треснувший корпус.",
                    "wiki": "Dossier persistence должна защищать настроение игры. Сохранение силы превратит pressure в решенную проблему. Сохранение знания позволит игрокам раньше распознавать ложь, выбирать рискованные контракты и понимать последствия, уважая каждый новый seed.",
                    "site": "Dossier: знание, а не сила - правило replayability, которое сохраняет HECTON-8 долгим и опасным."
                },
                "graph": ("replay_dossier", "meta", "first_no_power_carryover_notice", "P136_DOSSIER_RUMOR_UNLOCKS;P139_STARTING_CLAIM_VARIANTS", "", "no power carryover notice", "Replay knowledge must not collapse survival pressure.", "Accept suspicion as reward instead of power carryover.", "1", "in_game_wiki"),
                "route": ("RC134_DOSSIER_KNOWLEDGE_NOT_POWER", "replay_dossier", 0, 0, "P140_DOSSIER_KNOWLEDGE_NOT_POWER", "P136_DOSSIER_RUMOR_UNLOCKS;P139_STARTING_CLAIM_VARIANTS", "in_game_wiki", "no-power carryover notice, route warning card, old ending hash", "What must never persist?", "Equipment power and world truth reset; knowledge persists.", "Warnings, rumor context and ending hashes vary.", "none"),
            },
        ],
    },
]


def localized(packet: dict[str, object]) -> dict[str, dict[str, str]]:
    en = packet["en"]
    ru = packet["ru"]
    result = {
        "en_US": {
            "title": str(packet["title"]),
            "scanner": en["scanner"],
            "field_note": en["field_note"],
            "terminal": en["terminal"],
            "audio": en["audio"],
            "in_game_wiki": en["wiki"],
            "external_site": en["site"],
        },
        "ru_RU": {
            "title": str(packet["title_ru"]),
            "scanner": ru["scanner"],
            "field_note": ru["field_note"],
            "terminal": ru["terminal"],
            "audio": ru["audio"],
            "in_game_wiki": ru["wiki"],
            "external_site": ru["site"],
        },
    }
    for locale in LOCALES:
        if locale in result:
            continue
        prefix = LOCALE_PREFIX[locale]
        result[locale] = {
            "title": str(packet["title"]),
            "scanner": f"{prefix} {en['scanner']}",
            "field_note": f"{prefix} {en['field_note']}",
            "terminal": f"{prefix} {en['terminal']}",
            "audio": f"{prefix} {en['audio']}",
            "in_game_wiki": f"{prefix} {en['wiki']}",
            "external_site": f"{prefix} {en['site']}",
        }
    return result


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def write_csv(path: Path, headers: tuple[str, ...], rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=headers, lineterminator="\n")
    writer.writeheader()
    for row in rows:
        writer.writerow({key: row.get(key, "") for key in headers})
    write_text(path, buffer.getvalue())


def packet_json(release: dict[str, object]) -> dict[str, object]:
    packets = []
    for packet in release["packets"]:
        packets.append(
            {
                "packet_id": packet["packet_id"],
                "article_id": packet["article_id"],
                "title_key": "applied_lore." + str(packet["article_id"]).split(".")[-1] + ".title",
                "unlock": {
                    "primary": packet["unlock"],
                    "poi_tags": packet["poi"],
                    "biome_tags": packet["biomes"],
                },
                "localized": localized(packet),
            }
        )
    return {
        "schema": "H8.APPLIED_LORE_PACKET_BUNDLE.V0",
        "release_set_id": release["id"],
        "status": "production_facing_draft_pending_native_localization",
        "runtime_contract": {
            "runtime_reads_json": False,
            "runtime_reads_markdown": False,
            "runtime_uses_baked_static_data": True,
        },
        "packets": packets,
    }


def manifest_json(release: dict[str, object]) -> dict[str, object]:
    release_id = str(release["id"])
    return {
        "schema": "H8.RELEASE_SET_MANIFEST.V0",
        "release_set_id": release_id,
        "status": "production_facing_draft_pending_native_localization",
        "packet_sources": [f"Docs/Lore/AppliedContent/packets/{release_id}.packets.json"],
        "packets": [packet["packet_id"] for packet in release["packets"]],
        "locales_present_in_packet_bundle": list(LOCALES),
        "markdown_publication_locales_present": list(LOCALES),
        "surfaces": ["scanner", "in_game_wiki", "terminal", "audio_subtitle", "external_site", "image_brief"],
        "runtime_contract": {
            "runtime_must_not_parse_markdown": True,
            "runtime_must_not_generate_translation": True,
            "expected_bake_output": [
                "packet_id_hash",
                "loc_id_hashes",
                "surface_enum",
                "unlock_id",
                "domain_tag",
                "route_window_id",
                "localized_string_pool_offsets",
            ],
        },
    }


def release_markdown(release: dict[str, object]) -> str:
    lines = [
        f"# {release['id']}",
        "",
        "Status: production-facing draft, pending native localization pass.",
        "Runtime contract: authoring source only; runtime must consume baked static data/string-pool rows.",
        "",
        f"Purpose: {release['summary']}",
        "",
        "## Packets",
        "",
    ]
    for packet in release["packets"]:
        lines.append(f"- `{packet['packet_id']}` - {packet['title']}: {packet['en']['site']}")
    lines.extend(
        [
            "",
            "## Production Use",
            "",
            "- Scanner and terminal snippets are short enough for diegetic UI.",
            "- In-game wiki and external-site fields are generated from the packet bundle.",
            "- Route cards connect packet IDs to depth windows, replay axes and ending pressure.",
            "- Binding maps provide future Unity/DataMonolith placement targets without runtime markdown parsing.",
            "",
        ]
    )
    return "\n".join(lines)


def evidence_rows(release: dict[str, object]) -> list[dict[str, object]]:
    rows = []
    for packet in release["packets"]:
        arc_id, depth_band, route_moment, prereq, next_ids, evidence_type, truth_claim, decision, spoiler, surface = packet["graph"]
        rows.append(
            {
                "packet_id": packet["packet_id"],
                "arc_id": arc_id,
                "depth_band": depth_band,
                "route_moment": route_moment,
                "prereq_packet_ids": prereq,
                "next_packet_ids": next_ids,
                "evidence_type": evidence_type,
                "truth_claim": truth_claim,
                "player_decision": decision,
                "spoiler_tier": spoiler,
                "primary_surface": surface,
            }
        )
    return rows


def route_rows(release: dict[str, object]) -> list[dict[str, object]]:
    rows = []
    for packet in release["packets"]:
        (
            route_card_id,
            phase_id,
            depth_min,
            depth_max,
            packet_ids,
            required_packet_ids,
            primary_surface,
            world_object_hint,
            question,
            truth,
            replay,
            pressure,
        ) = packet["route"]
        rows.append(
            {
                "route_card_id": route_card_id,
                "phase_id": phase_id,
                "depth_min_m": depth_min,
                "depth_max_m": depth_max,
                "packet_ids": packet_ids,
                "required_packet_ids": required_packet_ids,
                "primary_surface": primary_surface,
                "world_object_hint": world_object_hint,
                "player_question": question,
                "truth_payload": truth,
                "replay_axis": replay,
                "ending_pressure": pressure,
            }
        )
    return rows


def component_for_surface(surface: str) -> tuple[str, str, str, str]:
    if surface == "scanner":
        return ("ScannableFragment", "appliedLoreFinalPacketHash", "NarrativeDiscovery", "appliedLorePacketHash")
    return ("NarrativeDiscovery", "appliedLorePacketHash", "MessageTerminal", "appliedLorePacketHash")


def binding_rows(release: dict[str, object]) -> list[dict[str, object]]:
    rows = []
    for packet in release["packets"]:
        route = packet["route"]
        surface = route[6]
        primary_component, primary_field, secondary_component, secondary_field = component_for_surface(surface)
        packet_id = packet["packet_id"]
        packet_hash = fnv1a32(packet_id)
        unlock_moment = str(packet["title"]).lower()
        rows.append(
            {
                "packet_id": packet_id,
                "packet_hash_hex": f"0x{packet_hash:08X}",
                "packet_hash_uint": packet_hash,
                "release_set": release["id"],
                "primary_component": primary_component,
                "primary_field": primary_field,
                "secondary_component": secondary_component,
                "secondary_field": secondary_field,
                "suggested_world_target": ", ".join(packet["poi"]),
                "unlock_moment": unlock_moment,
                "notes": "Authoring evidence only; runtime consumes baked packet hash and string-pool rows.",
            }
        )
    return rows


def scene_target_rows(release: dict[str, object]) -> list[dict[str, object]]:
    prefab_cycle = [
        ("Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_AppliedLore_MessageTerminalAnchor.prefab", "Assets/_Project/Prefabs/Items/Tools/Item_Tool_Scanner_World.prefab"),
        ("Assets/_Project/Prefabs/Construction/Final/PFB_Module_ServicePump.prefab", "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Corridor.prefab"),
        ("Assets/_Project/Prefabs/Construction/Final/PFB_Debris_WreckField.prefab", "Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_AppliedLore_MessageTerminalAnchor.prefab"),
    ]
    rows = []
    for index, packet in enumerate(release["packets"]):
        route = packet["route"]
        surface = route[6]
        primary_component, primary_field, _secondary_component, _secondary_field = component_for_surface(surface)
        packet_id = packet["packet_id"]
        packet_hash = fnv1a32(packet_id)
        primary_target, secondary_target = prefab_cycle[index % len(prefab_cycle)]
        poi_targets = ", ".join(packet["poi"])
        rows.append(
            {
                "packet_id": packet_id,
                "packet_hash_hex": f"0x{packet_hash:08X}",
                "packet_hash_decimal": packet_hash,
                "authoring_component": primary_component,
                "serialized_field": primary_field,
                "primary_target_candidates": primary_target,
                "secondary_target_candidates": secondary_target,
                "unity_safe_action": f"Use Unity API/editor pass to add or update {primary_component} on {poi_targets}, then assign 0x{packet_hash:08X}.",
                "notes": "Cold content placement target; no scene search or runtime parser.",
            }
        )
    return rows


def image_brief(release: dict[str, object]) -> str:
    lines = [
        f"# {release['id']} Image Briefs",
        "",
        "Visual standard: NASA-punk / deep-sea noir, hard-sci-fi evidence, no clean sci-fi gloss.",
        "",
    ]
    for packet in release["packets"]:
        lines.extend(
            [
                f"## {packet['packet_id']} - {packet['title']}",
                "",
                f"- Evidence object: {', '.join(packet['poi'])}.",
                f"- Mood: {packet['en']['field_note']}",
                "- Composition: show a physical artifact first, then hint at the legal/ecological consequence through labels, corrosion, pressure marks or packet UI.",
                "- Avoid: hero portraits, decorative neon, fantasy magic ore, clean corporate brochure style.",
                "",
            ]
        )
    return "\n".join(lines)


def all_new_packets() -> list[tuple[dict[str, object], dict[str, object]]]:
    rows: list[tuple[dict[str, object], dict[str, object]]] = []
    for release in SETS:
        for packet in release["packets"]:
            rows.append((release, packet))
    return rows


def is_manual_component(packet: dict[str, object]) -> bool:
    primary_component, _primary_field, _secondary_component, _secondary_field = component_for_surface(packet["route"][6])
    return primary_component != "ScannableFragment"


def manual_policy_row(release: dict[str, object], packet: dict[str, object]) -> dict[str, object]:
    packet_id = str(packet["packet_id"])
    packet_hash = fnv1a32(packet_id)
    component, field, _secondary_component, _secondary_field = component_for_surface(packet["route"][6])
    title = str(packet["title"])
    safe_rule = title.lower().replace(" ", "-").replace("/", "-")
    if component == "MessageTerminal":
        return {
            "packet_id": packet_id,
            "packet_hash_hex": f"0x{packet_hash:08X}",
            "packet_hash_decimal": packet_hash,
            "manual_policy": "terminal_anchor_required",
            "required_anchor_type": "diegetic_terminal_panel",
            "approved_template_prefab": "Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_AppliedLore_MessageTerminalAnchor.prefab",
            "authoring_component": component,
            "serialized_field": field,
            "discovery_id": "",
            "placement_rule": safe_rule,
            "reason": f"{release['id']} requires controlled diegetic terminal evidence.",
        }
    if component == "NarrativeDiscovery":
        discovery_id = "applied_lore_" + packet_id.lower()
        return {
            "packet_id": packet_id,
            "packet_hash_hex": f"0x{packet_hash:08X}",
            "packet_hash_decimal": packet_hash,
            "manual_policy": "discovery_world_prop_required",
            "required_anchor_type": "visually_marked_world_prop",
            "approved_template_prefab": "",
            "authoring_component": component,
            "serialized_field": field,
            "discovery_id": discovery_id,
            "placement_rule": safe_rule,
            "reason": f"{release['id']} requires a bounded dossier/wiki evidence prop.",
        }
    raise ValueError(f"Unsupported manual component: {component}")


def terminal_prefab_path(packet_id: str) -> str:
    return f"Assets/_Project/Prefabs/Narrative/AppliedLore/Terminals/PFB_AppliedLore_Terminal_{packet_id}.prefab"


def scene_plan_row(release: dict[str, object], packet: dict[str, object], manual: dict[str, object], index: int) -> dict[str, object]:
    packet_id = str(packet["packet_id"])
    packet_hash = fnv1a32(packet_id)
    component = str(manual["authoring_component"])
    field = str(manual["serialized_field"])
    title = str(packet["title"])
    zone_by_release = {
        "RS025_HUMAN_LAW_PUBLIC_MEMORY": "law_custody",
        "RS026_ATLAS_PUBLIC_AUTHORITY_CLASSIFICATION": "atlas_authority",
        "RS027_FALSE_EXIT_RETURN_PRESSURE": "false_exit",
        "RS028_REPLAY_CONTRACT_DOSSIER_RULES": "dossier_replay",
    }
    depth_by_release = {
        "RS025_HUMAN_LAW_PUBLIC_MEMORY": "route_law",
        "RS026_ATLAS_PUBLIC_AUTHORITY_CLASSIFICATION": "atlas_archive",
        "RS027_FALSE_EXIT_RETURN_PRESSURE": "partial_exit",
        "RS028_REPLAY_CONTRACT_DOSSIER_RULES": "meta_contract",
    }
    x = -12.0 + (index % 5) * 5.1
    y = 1.1 + (index % 3) * 0.35
    z = 42.0 + (index // 5) * 3.8
    yaw = -35 + (index % 7) * 12
    if component == "MessageTerminal":
        source_prefab = terminal_prefab_path(packet_id)
        object_name = "AL_TERM_" + packet_id
        discovery_id = ""
        scale = "1|1|1"
    else:
        source_prefab = "Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_debris_scatter__crate.prefab"
        object_name = "AL_DISC_" + packet_id
        discovery_id = str(manual["discovery_id"])
        scale = "0.82|0.82|0.82"
    return {
        "packet_id": packet_id,
        "packet_hash_hex": f"0x{packet_hash:08X}",
        "packet_hash_decimal": packet_hash,
        "scene_path": "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
        "placement_root": "__APPLIED_LORE_SCENE_PLACEMENT",
        "object_name": object_name,
        "source_prefab": source_prefab,
        "authoring_component": component,
        "serialized_field": field,
        "discovery_id": discovery_id,
        "display_name": title,
        "local_position": f"{x:.2f}|{y:.2f}|{z:.2f}",
        "local_euler": f"0|{yaw}|0",
        "local_scale": scale,
        "depth_band": depth_by_release[str(release["id"])],
        "zone_tag": zone_by_release[str(release["id"])],
        "placement_note": f"planned cold AppliedLore anchor for {packet_id}; scene placement pass pending",
    }


def merge_csv_rows(path: Path, headers: tuple[str, ...], rows: list[dict[str, object]]) -> None:
    existing: list[dict[str, object]] = []
    new_ids = {str(row["packet_id"]) for row in rows}
    if path.exists():
        with path.open("r", encoding="utf-8", newline="") as handle:
            reader = csv.DictReader(handle)
            if tuple(reader.fieldnames or ()) != headers:
                raise ValueError(f"Header mismatch: {path}")
            for row in reader:
                if row.get("packet_id", "") not in new_ids:
                    existing.append(row)
    write_csv(path, headers, existing + rows)


def ensure_terminal_prefab(packet: dict[str, object]) -> None:
    packet_id = str(packet["packet_id"])
    component, _field, _secondary_component, _secondary_field = component_for_surface(packet["route"][6])
    if component != "MessageTerminal":
        return
    template = ROOT / "Assets" / "_Project" / "Prefabs" / "Narrative" / "AppliedLore" / "Terminals" / "PFB_AppliedLore_Terminal_P002_BLACK_KEEL_CONTACT.prefab"
    if not template.exists():
        raise FileNotFoundError(template)
    text = template.read_text(encoding="utf-8")
    text = text.replace("PFB_AppliedLore_Terminal_P002_BLACK_KEEL_CONTACT", f"PFB_AppliedLore_Terminal_{packet_id}")
    text = text.replace("appliedLorePacketHash: 692080514", f"appliedLorePacketHash: {fnv1a32(packet_id)}")
    write_text(ROOT / terminal_prefab_path(packet_id), text)


def write_manual_policy_and_placement() -> None:
    manual_headers = (
        "packet_id",
        "packet_hash_hex",
        "packet_hash_decimal",
        "manual_policy",
        "required_anchor_type",
        "approved_template_prefab",
        "authoring_component",
        "serialized_field",
        "discovery_id",
        "placement_rule",
        "reason",
    )
    placement_headers = (
        "packet_id",
        "packet_hash_hex",
        "packet_hash_decimal",
        "scene_path",
        "placement_root",
        "object_name",
        "source_prefab",
        "authoring_component",
        "serialized_field",
        "discovery_id",
        "display_name",
        "local_position",
        "local_euler",
        "local_scale",
        "depth_band",
        "zone_tag",
        "placement_note",
    )
    manual_rows: list[dict[str, object]] = []
    placement_rows: list[dict[str, object]] = []
    for index, (release, packet) in enumerate(all_new_packets()):
        if not is_manual_component(packet):
            continue
        ensure_terminal_prefab(packet)
        manual = manual_policy_row(release, packet)
        manual_rows.append(manual)
        placement_rows.append(scene_plan_row(release, packet, manual, index))

    merge_csv_rows(BASE / "binding_maps" / "RS001_RS010_manual_binding_policy.csv", manual_headers, manual_rows)
    merge_csv_rows(BASE / "binding_maps" / "RS001_RS010_scene_placement_plan.csv", placement_headers, placement_rows)


def main() -> int:
    graph_headers = (
        "packet_id",
        "arc_id",
        "depth_band",
        "route_moment",
        "prereq_packet_ids",
        "next_packet_ids",
        "evidence_type",
        "truth_claim",
        "player_decision",
        "spoiler_tier",
        "primary_surface",
    )
    route_headers = (
        "route_card_id",
        "phase_id",
        "depth_min_m",
        "depth_max_m",
        "packet_ids",
        "required_packet_ids",
        "primary_surface",
        "world_object_hint",
        "player_question",
        "truth_payload",
        "replay_axis",
        "ending_pressure",
    )
    binding_headers = (
        "packet_id",
        "packet_hash_hex",
        "packet_hash_uint",
        "release_set",
        "primary_component",
        "primary_field",
        "secondary_component",
        "secondary_field",
        "suggested_world_target",
        "unlock_moment",
        "notes",
    )
    scene_headers = (
        "packet_id",
        "packet_hash_hex",
        "packet_hash_decimal",
        "authoring_component",
        "serialized_field",
        "primary_target_candidates",
        "secondary_target_candidates",
        "unity_safe_action",
        "notes",
    )

    for release in SETS:
        release_id = str(release["id"])
        write_text(
            BASE / "packets" / f"{release_id}.packets.json",
            json.dumps(packet_json(release), ensure_ascii=False, indent=2) + "\n",
        )
        write_text(
            BASE / "release_sets" / f"{release_id}_manifest.json",
            json.dumps(manifest_json(release), ensure_ascii=False, indent=2) + "\n",
        )
        write_text(BASE / "release_sets" / f"{release_id}.md", release_markdown(release))
        write_csv(BASE / "graphs" / f"{release_id.split('_')[0]}_evidence_graph.csv", graph_headers, evidence_rows(release))
        write_csv(BASE / "route_cards" / f"{release_id.split('_')[0]}_route_cards.csv", route_headers, route_rows(release))
        write_csv(BASE / "binding_maps" / f"{release_id.split('_')[0]}_runtime_binding_map.csv", binding_headers, binding_rows(release))
        write_csv(BASE / "binding_maps" / f"{release_id.split('_')[0]}_scene_binding_targets.csv", scene_headers, scene_target_rows(release))
        write_text(BASE / "image_briefs" / f"{release_id}.md", image_brief(release))

    write_manual_policy_and_placement()
    print("generated_release_sets=4 packets=20 route_cards=20")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
