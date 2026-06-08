# P1301_RAN_AEGIR_NAME_AUTHORITY_LEDGER

## Header Metadata

| Field | Value |
|---|---|
| Packet ID | P1301_RAN_AEGIR_NAME_AUTHORITY_LEDGER |
| Article ID | article.aegir.ran_aegir_name_authority_ledger |
| Loc namespace | lore.aegir.ran_aegir_name_authority_ledger |
| Runtime layer | Narrative authoring source |
| Surfaces | external_site, in_game_wiki, pda_codex, scanner, terminal_seed, marauder_note_seed |
| Spoiler level | 0 public system naming / 1 first carrier-catalog context |
| Canon sources | Lore_Bible.md; Canon_Locks.md; Lore_Content_System.md; Lore_Localization_Model.md; Aegir_System_Game_Texture.md; Encyclopedia/Aegir_Astronomy_Reference.md; Encyclopedia/Aegir_System.md; Encyclopedia/Aegir_Gas_Giant.md |
| Speaker | Public route archivist, carrier catalog resolver, Marauder annotation |
| Audience | Player reading route catalogs; public/wiki reader; localization reviewer |
| Date / era | 2190 current Aegir claim catalog |
| Location / route | Black Keel catalog cache, RAN-B:H8 route plate, Aegir system public archive |
| Unlock context | First orbital dossier, carrier ephemeris, or route plate scan that exposes Ran/Aegir/RAN-B:H8 labels |
| Evidence object | Scratched route plate with `RAN-B:H8`, Black Keel cache title `AEGIR`, and old public archive correction stamp |
| Connected packets | P076_RAN_AEGIR_ANCHOR; P104_RAN_B_H8_PUBLIC_CATALOG; P141_RAN_AEGIR_DISTANCE_MODEL; P421_RAN_AEGIR_PUBLIC_DISTANCE_BAND; P423_HECTON8_MOON_LADDER_PUBLIC_BAND |
| First-20 route moment | FIRST_20_ROUTE_BLOCKER_REMOVED: prevents early sky/orbit/codex text from confusing the star, gas giant, claim label and playable moon |
| Content status | source_complete_unimported |
| Proof boundary | Markdown authoring source only. No importer, route-card, DataMonolith, h8bin, Unity placement, runtime UI, site deployment, native localization or publication readiness is claimed. |

## Source Brief

Packet ID: P1301_RAN_AEGIR_NAME_AUTHORITY_LEDGER

Article ID: article.aegir.ran_aegir_name_authority_ledger

Loc namespace: lore.aegir.ran_aegir_name_authority_ledger

Runtime layer: Narrative

Surface targets: external site/wiki, in-game wiki, PDA codex, scanner short, terminal seed, Marauder note seed

Spoiler level: 0 public system naming / 1 first carrier-catalog context

Canon sources: Canon_Locks.md; Lore_Bible.md; Lore_Content_System.md; Lore_Localization_Model.md; Aegir_System_Game_Texture.md; Encyclopedia/Aegir_Astronomy_Reference.md; Encyclopedia/Aegir_System.md; Encyclopedia/Aegir_Gas_Giant.md

Speaker/source: route archivist for public wording, carrier catalog resolver for scanner/terminal wording, Marauder for field correction.

Audience: player, site/wiki reader, future localization reviewer.

Date/era: 2190.

Location/depth/route: Black Keel catalog cache, orbit dossier, route plate recovered near first uplink or later navigation archive.

Unlock context: first scan of Aegir/RAN-B:H8 labels.

Evidence object: physical route plate and catalog correction stamp.

What this source knows: Ran is the host star anchor; Aegir is the gas giant / claim-system label in player-facing shorthand; RAN-B:H8 is a dry catalog/insurance label; HECTON-8 is the playable ocean moon and normal in-world name.

What this source does not know: final ephemeris constants, final orbital table, ending routes, native localization proof, runtime placement.

What this source hides or gets wrong: public archives collapse names for readability; old ledgers may use Aegir as shorthand for star/system/giant when the exact body matters.

Player use: keeps sky, route plate, contract and codex labels readable without turning every object into an astronomy lecture.

Forbidden facts: no claim that Aegir is the playable moon, no claim that Aegir is darkness-first, no exact orbital constants, no runtime/publication readiness.

Required proper nouns/units: Ran, Aegir, RAN-B:H8, HECTON-8, Black Keel, 2190.

LocIDs:

- LORE_AEGIR_RAN_NAME_AUTHORITY_LEDGER_TITLE
- LORE_AEGIR_RAN_NAME_AUTHORITY_LEDGER_SCANNER_SHORT
- LORE_AEGIR_RAN_NAME_AUTHORITY_LEDGER_PUBLIC_BODY
- LORE_AEGIR_RAN_NAME_AUTHORITY_LEDGER_FIELD_NOTE

Localization status: en_US source_authority; all non-English rows draft_machine_or_llm and require native review plus surface layout proof.

## Surface Texts

### External Site / Wiki

Ran is the star. Aegir is the giant people argue over.

Public catalogs do not always keep that clean. Old route ledgers, insurance plates and Deep Reach summaries often use `Aegir` for the whole claim system because that is the name that gets paid, taxed and fought over. A carrier operator knows the difference. A contract lawyer may pretend not to. A worker on HECTON-8 usually does not have time to care unless the wrong label puts a storm window, relay shadow or recovery orbit in the wrong column.

Use the names by surface. `Ran` belongs in astronomy and long-distance route language. `Aegir` belongs to the gas giant, its moon system and the claim economy. `RAN-B:H8` belongs on dry catalogs, insurance plates, tariff tables and route ledgers. `HECTON-8` belongs in normal play text, worker speech, Marauder notes and anything that has salt on it.

The distinction matters because the sky is not decoration. Aegir's moons can block signal, shift tide models and change carrier windows. Ran's light keeps the upper ocean readable. HECTON-8 is the pressure world where those labels stop being navigation and start being survival.

### In-Game Codex

Catalog resolver: Ran is the host star. Aegir is the gas giant and claim-system shorthand. RAN-B:H8 is the dry catalog label for HECTON-8. Use HECTON-8 in field notes unless a route plate or insurance record requires the catalog form.

### Scanner Short

Route plate resolved. Ran: host star. Aegir: giant / claim label. RAN-B:H8: HECTON-8 catalog key.

### Terminal / Document Seed

CATALOG NORMALIZATION NOTICE: AEGIR shorthand accepted for claim-system index. Use RAN-B:H8 for insured moon record, HECTON-8 for field-operational surface.

### Marauder Field Note

If a lawyer says Aegir when the plate says RAN-B:H8, check what column got cheaper.

## Future Integration Notes

- Importer admission should route this to Aegir/system/ships navigation clusters and glossary surfaces.
- Future route plates and contracts can use this packet to resolve label confusion without adding exposition to every terminal.
- Scanner short is the first runtime candidate; public body is safer for site/wiki or PDA after an orbit dossier unlock.
- Native localization must preserve Ran, Aegir, RAN-B:H8 and HECTON-8 as stable identity strings unless a locale style guide later says otherwise.

## Locale Rows

| Locale | Status | Text |
|---|---|---|
| en_US | source_authority | Route plate resolved. Ran is the host star; Aegir is the giant and claim label; RAN-B:H8 is the HECTON-8 catalog key. |
| ar_SA | draft_machine_or_llm | تم حل لوحة المسار. Ran هي النجم المضيف؛ Aegir هو العملاق ووسم المطالبة؛ RAN-B:H8 هو مفتاح كتالوج HECTON-8. |
| de_DE | draft_machine_or_llm | Routenplatte aufgelöst. Ran ist der Wirtsstern; Aegir ist der Riese und Anspruchsname; RAN-B:H8 ist der HECTON-8-Katalogschlüssel. |
| es_ES | draft_machine_or_llm | Placa de ruta resuelta. Ran es la estrella anfitriona; Aegir es el gigante y la etiqueta de reclamación; RAN-B:H8 es la clave de catálogo de HECTON-8. |
| fr_FR | draft_machine_or_llm | Plaque de route résolue. Ran est l'étoile hôte ; Aegir est le géant et le libellé de revendication ; RAN-B:H8 est la clé catalogue de HECTON-8. |
| he_IL | draft_machine_or_llm | לוח נתיב פוענח. Ran הוא הכוכב המארח; Aegir הוא הענק ותווית התביעה; RAN-B:H8 הוא מפתח הקטלוג של HECTON-8. |
| id_ID | draft_machine_or_llm | Pelat rute terurai. Ran adalah bintang induk; Aegir adalah raksasa dan label klaim; RAN-B:H8 adalah kunci katalog HECTON-8. |
| ja_JP | draft_machine_or_llm | 航路プレートを解決。Ranは主星、Aegirは巨大惑星兼請求名、RAN-B:H8はHECTON-8のカタログキー。 |
| ko_KR | draft_machine_or_llm | 항로 판독 완료. Ran은 주성, Aegir는 거대 행성과 청구권 표지, RAN-B:H8은 HECTON-8 카탈로그 키. |
| nl_NL | draft_machine_or_llm | Routeplaat opgelost. Ran is de gastster; Aegir is de reus en claimnaam; RAN-B:H8 is de catalogussleutel voor HECTON-8. |
| pl_PL | draft_machine_or_llm | Płyta trasy rozpoznana. Ran to gwiazda macierzysta; Aegir to olbrzym i etykieta roszczenia; RAN-B:H8 to klucz katalogowy HECTON-8. |
| pt_BR | draft_machine_or_llm | Placa de rota resolvida. Ran é a estrela hospedeira; Aegir é o gigante e rótulo de reivindicação; RAN-B:H8 é a chave de catálogo de HECTON-8. |
| ru_RU | draft_machine_or_llm | Маршрутная табличка распознана. Ran - звезда-хозяин; Aegir - гигант и метка претензии; RAN-B:H8 - каталожный ключ HECTON-8. |
| uk_UA | draft_machine_or_llm | Маршрутну табличку розпізнано. Ran - зоря-господар; Aegir - гігант і мітка претензії; RAN-B:H8 - каталожний ключ HECTON-8. |
| zh_CN | draft_machine_or_llm | 航线牌已解析。Ran 是主恒星；Aegir 是巨行星和主张标签；RAN-B:H8 是 HECTON-8 的目录键。 |

## QA

Forbidden facts avoided:

- No claim that Aegir is the host star.
- No claim that HECTON-8 is the only moon or that Aegir is decorative.
- No exact ephemeris constants or runtime placement claim.
- No native-reviewed, runtime-ready or publication-ready claim.

Surface fit:

- External site/wiki: public name authority and spoiler-safe route explanation.
- In-game codex: compact operational resolver.
- Scanner: one label-resolution output.
- Terminal: catalog normalization notice.
- Marauder note: practical suspicion, not omniscience.

Length risks:

- Locale table row is compact; terminal/document seed still needs separate RTL/CJK/wrap proof.

Native-review status:

- en_US is source authority.
- Non-English rows are draft_machine_or_llm only.

Open blockers:

- Exact ephemeris constants remain future celestial-data work.
- No importer/source CSV, page export, route-card or Unity placement touched.
