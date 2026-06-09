# HECTON-8 Applied Content

Status: source/export content layer. Static docs here do not prove publication, runtime readiness, Unity placement, or DataMonolith bake state.
Purpose: store content sources and export candidates for the game, in-game wiki, external website/wiki, localization, audio, and image/art tasks.

This folder is not an internal rationale archive.

Local reader: run `python -m http.server 8788 --bind 127.0.0.1 --directory Docs/Lore/AppliedContent` from the project root, then open `http://127.0.0.1:8788/reader.html`.

Simple pre-wiki reader: open `http://127.0.0.1:8788/prewiki.html` after the same local server is running. It is the lighter article/language/surface preview for reading generated wiki/site pages without controller panels.

## Freshness Boundary

Local counts and release-set lists in this README are static documentation snapshots unless a timestamped command output or audit artifact says otherwise. Use `Publication_Surface_Index.csv`, `Publication_Cluster_Index.csv`, release-set manifests, and the scoped graph/route/binding CSV inventories as the current source/export inventory. None of those files is runtime, publication, localization-review, or placement proof by itself.

## Folders

- `packets/`: structured content packets with all target surfaces and translations.
- `in_game_wiki/`: player-facing wiki/codex articles.
- `external_site/`: website/wiki source/export candidate articles. Public release approval requires separate publication and localization proof.
- `image_briefs/`: art prompts and image requirements for articles, codex, cards, and marketing.
- `release_sets/`: grouped source/export batches. Runtime use requires importer, route-card export, string-pool bake, h8bin/DataMonolith validation, and Unity placement proof where applicable.
- `binding_maps/`: scene-authoring maps for assigning baked packet hashes to concrete POIs, scan fragments, and terminals.
- `graphs/`: evidence-chain maps for packet prerequisites, next leads, depth bands, decision pressure, and primary display surfaces.
- `route_cards/`: gameplay route cards tying packet groups to phases, depth bounds, replay axes, and ending pressure.

## Release-Set Snapshot

This local list is a static snapshot, not a freshness proof. The folder inventory currently extends beyond the older RS001-RS092 list; visible newer release-set manifests include `RS093_LORE_SYSTEM_INTEGRATION_BRIDGE`, `RS094_PUBLIC_AUTHORITY_BRIDGE_EXPANSION`, and `RS095_CORPORATE_PRESSURE_CHAIN_BRIDGE`.

- `RS001_FIRST_DESCENT`: first five content packets for crash shelf, Black Keel contact, Barnard mark, blue debt, and repair scar.
- `RS002_DEEPENING_DESCENT`: second five content packets for dead claim caches, brine traversal, evacuation truth, bottom factory, and payload-window endings.
- `RS003_HUMAN_SPACE_AEGIR_ROUTE`: third five content packets for human domains, relay infrastructure, nearlight ships, Aegir route windows, and Black Keel ledger logic.
- `RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY`: fourth five content packets for Aegir host light, moon ladder, HECTON-8 geology, resources, and ecology contrast.
- `RS005_CARRIER_ESCAPE_DEEP_REACH_PRESSURE`: fifth five content packets for Black Keel custody, drop-capsule stranding, current Deep Reach return pressure, material partial exit, and the player's professional-to-personal motive.
- `RS006_HUMANITY_TRANSIT_SEED_LOGISTICS`: sixth five content packets for the six-domain ledger, Barnard Yards origin, no-FTL transit math, Seed Ship doctrine, and relay custody debt.
- `RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE`: seventh five content packets for photic shelf contrast, pressure ladder depth bands, cable reef symbiosis, abyssal repair fauna, and the bottom factory-temple threshold.
- `RS008_ESCAPE_ENDINGS_ATLAS_QUESTION`: eighth five content packets for return-vector windows, the coward exit, material payout, Deep Reach cleanup pressure, and the final Atlas ethical question.
- `RS009_COLONY_LAYOUT_WORKER_EVIDENCE`: ninth five content packets for drowned worker lockers, pressure bunk routine, shift-board route holds, medical lock delay, and black-box name payloads.
- `RS010_PRESSURE_MACHINERY_RETURN_ROUTE`: tenth five content packets for pump-room pressure tradeoffs, hatch seal history, cable splice scars, sonar return-route decay, and salvage tool custody.
- `RS011_COMM_TARIFF_GRAFFITI_MASKS`: eleventh five content packets for no-ansible communication delay, Black Keel tariff queues, marauder graffiti masks, stale relay instructions, and corporate response ledgers.
- `RS012_PLAYER_LIABILITY_ESCAPE`: twelfth five content packets for ex-Deep-Reach player canon, Great Tide liability, Black Keel claim hooks, escape-chain assembly, and first-hour structure.
- `RS013_COLONY_ATLAS_MAINTENANCE`: thirteenth five content packets for Atlas maintenance ecology and first named colony workers: Mara Venn, Juno Kade, Ren Okoye, and Sahana Iqbal.
- `RS014_COLONY_RETURN_WINDOWS`: fourteenth five content packets for Lian Torres, Oskar Neumann, Aya Morita, Pavel Sorn, and present-tense Deep Reach communication windows.

## Runtime Rule

Packets are authoring/export sources. The game consumes baked records generated from them: IDs, enums, flags, offsets, LocID hashes, and string pools.

No runtime markdown parsing. No runtime translation. No scene search for content.

## Runtime Route

- Import: `Tools/AppliedLoreImporter.py`.
- Page export: `Tools/AppliedLorePageExporter.py` fills localized markdown pages and `INDEX.md` files for in-game wiki and external site surfaces from the same packet JSON without overwriting hand-authored packet pages by default.
- Localization status: authoring-only draft/native-review prefixes are stripped before CSV/page export; `flags & H8AppliedLoreHashes.RowFlagDraftLocalization` and page frontmatter carry `draft_native_pass_pending` without leaking tool notes into player-visible prose.
- Localization status index: `Localization_Status_Index.md` is regenerated from release-set manifests and packet JSON sources, then reports source-authority, draft/localization-review-pending, and explicitly reviewed rows per locale.
- Publication surface index: `Publication_Surface_Index.csv` is regenerated from packet JSON and lists every generated page by surface, locale, release set, unlock id, localization status, tags and relative page path for site/wiki ingestion.
- Publication cluster index: `Publication_Cluster_Index.csv` is regenerated from `RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv` and maps the hard-sci-fi encyclopedia hubs to every locale/surface with spoiler tier, page path, route question and truth payload.
- Route-card export: `Tools/AppliedLoreRouteCardExporter.py` converts route-card source CSVs into `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv` with stable route, phase, packet, prerequisite, surface, and ending-pressure hashes when a scoped export/audit proves the handoff.
- CSV export: `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`.
- Constants export: `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs`.
- Bake targets: `H8DataSectionId.AppliedLorePackets` and `H8DataSectionId.AppliedLoreRoutes` in `static_data.h8bin`.
- Offline audit: `Tools/AppliedLoreRuntimeAudit.py --root .` checks CSV rows, generated constants, baked packet and route section layouts, sorted packet/locale records, every UTF-8 text slice against the blob, route-card fixed-record hashes/depths/prerequisites/slot padding, source-route symbols from importer through PDA/scanner/terminal/POI consumers, all `binding_maps/*_runtime_binding_map.csv`, all `graphs/*_evidence_graph.csv`, all `route_cards/*_route_cards.csv`, the route-card source-data export, publication page coverage for all baked packet/locale pairs, and localized publication indexes. Use `--source-only` when authoring has advanced but `static_data.h8bin` cannot be rebuilt yet.
- 1770 sorting audit: `production_audits/1770/` maps packet inventory, release-set scope, surface ownership, locale coverage, publication index drift, route/binding coverage, blockers, and handoff notes.
- Runtime read: `H8AppliedLoreRuntime` and `H8StaticDataArena`.
- Consumers: PDA encyclopedia, scanner title route, `MessageTerminal`, and TerminalOS preview line.
- Unlock route: `H8AppliedLoreRuntime.TryRaisePacketUnlocked` publishes `LoreFragmentScannedSignal`; PDA consumes the signal and unlocks/selects the baked packet.
- Authoring hooks: `NarrativeDiscovery.appliedLorePacketHash`, `ScannableFragment` 25/50/100% stage packet hashes, `MessageTerminal.appliedLorePacketHash`, and AUP POI triggers via `NarrativeSpatialTriggerAuthoring.AppliedLoreHash`.
- `RS015_HUMAN_DOMAINS_ROUTE_ECONOMY`: turn the six-domain human sphere into usable object lore, site/wiki articles, and route evidence without making the setting dense space opera.
- `RS016_AEGIR_SYSTEM_MOON_LADDER`: make Aegir and its moons useful for navigation, site lore, route pressure, and hard-sci-fi constraints around HECTON-8.
- `RS017_HECTON8_GEOLOGY_RESOURCE_ECOLOGY`: make HECTON-8's pressure geology, brine routes, vent forge, blue debt formation and wider resource economy usable as gameplay evidence and site/wiki foundation.
- `RS018_CARRIER_DEBT_CLAIM_AUTHORITY`: make Black Keel's claim-pool ownership, insurance custody, tonne-window debt, first voice and Deep Reach priority hooks usable as gameplay evidence and site/wiki foundation.
- `RS019_HECTON8_PHYSICAL_ATLAS_DEPTH_BANDS`: lock HECTON-8's moon origin, ocean bands, seafloor windows, seed invariants and pressure-containment failure stages.
- `RS020_ATLAS_ENDING_AGENCY_DOSSIER`: lock Atlas recognition limits, present Deep Reach faction pressure, false-ending taxonomy, dossier persistence and final payload choices.
- `RS021_INTERSTELLAR_TRANSIT_ROUTE_HISTORY`: lock no-FTL route economy, beam-sail probe era, pellet-fusion freight doctrine, RAN-B:H8 catalog language and Black Keel in-system tender limits.
- `RS022_DEEP_REACH_SIGNOFF_CHAIN`: name the 2147/2190 Deep Reach liability chain through risk, Atlas weighting, evacuation certification, insurance conversion and Recovery Compliance signatures.
- `RS023_FIRST_TOOL_CHAIN_SURVIVAL_GATE`: lock the first-hour tool chain around manual pumping, cold sealant, low-power cutting, acoustic return lines and the P-63 field fabricator.
- `RS024_RESOURCE_RECIPE_TAXONOMY`: split HECTON-8 resources into native geology, natural process feedstock, Deep-Reach-amplified materials and Atlas-altered biomechanical resources.
- `RS025_HUMAN_LAW_PUBLIC_MEMORY`: lock the civic/corporate authority split, Marauder legal loophole, salvage-truth evidence custody, public Aegir memory and Deep Reach origin chain.
- `RS026_ATLAS_PUBLIC_AUTHORITY_CLASSIFICATION`: lock Atlas public front, insurance/personhood gap, classified weighting layer, shutdown ethics and public memory after 2147.
- `RS027_FALSE_EXIT_RETURN_PRESSURE`: make early exits real but bitter through material payout, same-seed return, corporate capture, quarantine hold and public ledger leak.
- `RS028_REPLAY_CONTRACT_DOSSIER_RULES`: lock replay as dossier knowledge, riskier contract seeds, false-ending ladder, starting claim variants and knowledge-not-power persistence.
- `RS029_ROUTE_TIME_DISTANCE_MODEL`: lock Ran/Aegir distance as playable hard-sci-fi pressure through probe packet, heavy freight, crew rotation and relay-lag timing.
- `RS030_DEEP_REACH_SHELL_ORG_CHART`: lock Deep Reach public name and the Aegir shell chain through Continuity Holdings, Atlas office, Keelmark loss desk and Recovery Compliance.
- `RS031_FIRST_HOUR_PLAYABLE_SPINE`: lock first-hour contract, drop damage, Shallow Annex P-63, first sanitized Deep Reach lie and first Atlas repair trace.
- `RS032_COLONY_HUMAN_EVIDENCE_LAYER`: lock colony humanity through shift crews, job cards, locker names, triage ledgers and Marauder correction notes.
- `RS033_DOMAIN_EPHEMERIS_ROUTE_TABLE`: lock domain route-band scale, population/authority pressure scale, public lane names, transit duration bands and lower Deep Reach office surfaces.
- `RS034_WORKER_NAME_JOB_EVIDENCE_TABLE`: lock seeded worker-name protocol, pressure job titles, locker prop variants, native-localized name handling and replay-safe shift crew story seeds.
- `RS035_RESOURCE_RECIPE_PRESSURE_RULES`: lock recipe pressure bands, containment failure thresholds, blue debt quality classes, vent forge process steps and escape component tuning grammar.
- `RS036_DOSSIER_SAVE_PRESENTATION_RULES`: lock dossier selection UI rules, risk-weight contract cards, ending records, save-profile knowledge flags and website/wiki spoiler tiering.
- `RS037_AEGIR_MOON_PUBLIC_ATLAS`: lock moon-name mutability boundaries, HECTON-8 orbital hazard classes, Aegir moon role ledger, ephemeris table ownership and public moon-article spoiler gates.
- `RS038_DEEP_REACH_TRUE_CAUSE_KNOWLEDGE`: lock Deep Reach true-cause knowledge tiers, liability memo fragment chain, signoff/witness conflicts, suboffice personnel seed rules and the false public report packet.
- `RS039_FINAL_DECISION_EMOTIONAL_AXIS`: lock the emotional final trilemma around crime-scene sale, broken guardian preservation, public truth without control, Atlas severance and no clean best ending.
- `RS040_NUMERIC_TUNING_SOURCE_RULES`: lock table ownership for resource yields, escape recipe bands, risk/reward bands, inventory stack policy and native localization pass gates.
- `RS041_DEEP_REACH_LOWER_SIGNATURES`: Lock lower Deep Reach office signatures as evidence stamps, not new masterminds.
- `RS042_COLONY_ROSTER_AUTHORING_POOL`: Lock the colony roster scale, crew archetypes and reusable identity pool for prop, wiki and mission writing.
- `RS043_WORKER_PROP_EVIDENCE_KIT`: Lock prop-level evidence variants for lockers, ledgers, route stamps, Marauder corrections and audio fragments.
- `RS044_PUBLICATION_SPOILER_LOCALIZATION_PROTOCOL`: Lock public article tiers, in-game unlock tiers, transcript censorship, art-release gates and native localization backlog rules.
- `RS045_PHOTIC_SHELF_NATIVE_ECOLOGY`: Lock shallow ecology as vivid but pressure-governed evidence: photic mats, glass grazers, lantern blooms, shell clamp reefs and predator shadow telegraphs.
- `RS046_BRINE_CANYON_ABYSS_ECOLOGY`: Lock mid/deep ecology as route pressure through brine vane forests, density skaters, vent anchors, wide filter bodies and silt ambusher telegraphs.
- `RS047_ATLAS_MAINTENANCE_BIOMECH_LAYER`: Lock Atlas' repair ecology through conductive biofilm, acoustic filter organs, shell sealant growth, sensor-tagged fauna and vent micronode nests.
- `RS048_HARDWARE_AND_VEHICLE_EVIDENCE_STACK`: Lock Black Keel limits, drop-capsule failures, P-63 authority, pressure suit grades and sonar pinger beacons as gameplay evidence objects.
- `RS049_CONTRACT_SEED_RISK_REWARD_SURFACES`: Lock replay contract cards for lien severity, storm windows, sample custody, evidence order and Deep Reach clause pressure.
- `RS050_FIRST_HOUR_MICRO_SCRIPT_SURFACES`: Lock first-hour terminal/scanner/audio surfaces for Black Keel approach, capsule diagnostics, P-63 first repair, first Deep Reach lie and first Atlas repair trace.
- `RS051_PUBLIC_SITE_PILLAR_ARTICLES`: Lock spoiler-tiered public/site pillar articles for HECTON-8, Aegir, Deep Reach, Atlas-6 and blue debt.
- `RS052_LOCALIZATION_GLOSSARY_AUDIO_STYLE`: Lock proper-noun translation policy, unit/number style, terminal voice register, audio bark families and RTL/CJK font risk notes.
- `RS053_NUMERIC_AUTHORING_BRIDGE_SURFACES`: Lock gameplay-table bridge surfaces for resource yields, inventory stacks, escape recipes, contract risk/reward and ending payouts.
- `RS054_DOSSIER_CONTRACT_UI_COPY_DECK`: Lock PDA/dossier/contract UI copy surfaces for start screen, contract fields, rumor families, route warnings and ending records.
- `RS055_ENDING_PAYLOAD_RECORD_SURFACES`: Lock concrete ending record packets for material payout, partial return, public ledger, Atlas severance and preserve/quarantine outcomes.
- `RS056_NATIVE_LOCALIZATION_REVIEW_PACK`: Lock native review packets for RU, CJK, RTL, European-language and subtitle/audio QA gates.
- `RS057_PUBLIC_SITE_READY_ARTICLE_SECTIONS`: Lock public-site source/export candidate sections for HECTON-8, Aegir, Deep Reach, Atlas-6 spoiler gates and blue debt. The release-set name is historical; publication readiness still needs separate proof.
- `RS058_IN_GAME_ARTIFACT_AUDIO_SURFACES`: Lock concrete in-game note/audio/object fragments for capsule, P-63, worker lockers, Marauder corrections and quarantine relay pressure.
- `RS059_ECOLOGY_CODEX_SPECIMEN_CARDS`: Lock scanner/codex specimen cards for photic mats, glass grazers, lantern drifts, brine vanes and sensor-tagged fauna.
- `RS060_FINAL_DESCENT_ROUTE_FRAGMENTS`: Lock late-game route fragments for abyssal machine-field warning, Atlas basin gate, factory-temple entry, payload authority and no-clean-ending dossier tone.
- `RS061_TABLE_VALUE_HANDOFF_CONTRACTS`: Lock table handoff contracts for resource yield, stack limit, escape recipe cost, contract risk/reward and ending payout rows.
- `RS062_RUNTIME_UI_PROOF_BACKLOG`: Lock PDA, scanner, terminal, dossier and localized-overflow proof cards for future runtime UI implementation.
- `RS063_PUBLICATION_COMPOSITION_PROOF_PACK`: Lock site composition, Aegir art composition, Deep Reach evidence composition, Atlas spoiler composition and social/dev-note copy boundaries.
- `RS064_UNITY_PLACEMENT_PRIORITY_BACKLOG`: Lock Unity placement priority triage for first hour, mid-depth routes, ecology scans, final descent and terminal backlog rows.
- `RS065_CARRIER_CONTRACT_OWNERSHIP_SURFACES`: Lock Black Keel as claim-pool carrier infrastructure with masked Deep Reach beneficiary pressure, orbital recovery windows, autonomy limits and player lien start card.
- `RS066_DEEP_REACH_PRESENT_COMMS_CHAIN`: Lock present-tense Deep Reach communications as rare windows, legal/insurance pings, coordinate demands, faction split messages and physical signal decay.
- `RS067_ATLAS_REPAIR_NETWORK_MECHANICS`: Lock Atlas repair-network mechanisms as concrete biology-assisted maintenance: conductive biofilm, acoustic organs, shell sealant, sensor-tagged fauna and vent micronodes.
- `RS068_FALSE_EXIT_AFTER_ACTION_RECORDS`: Lock material, partial-return, quarantine, corporate-coordinate and public-ledger after-action records for false/partial endings.
- `RS069_SHIP_TECH_TRANSIT_ENCYCLOPEDIA`: Lock no-FTL probe, beam/pellet, seed-ship, carrier-tug and bathydrop-interface technology as player/site/wiki encyclopedia packets.
- `RS070_AEGIR_MOON_SYSTEM_ATLAS`: Lock Aegir as a readable warm-dwarf/gas-giant multi-moon system with relay, ice-scatter, HECTON-8 tide and dead-beacon moon roles.
- `RS071_HECTON8_GEOLOGY_RESOURCE_FIELDGUIDE`: Lock drowned-crust strata, brine density ladders, vent forge process, blue debt pressure history and pressure-glass/sealant fieldguide packets.
- `RS072_COLONY_DAILY_LIFE_EVIDENCE_ATLAS`: Lock colony humanity through shift routine, water ledger, tool certification, no-family-hook player guardrail and last-normal-day evidence.
- `RS073_ESCAPE_ASCENT_ENGINEERING_COMPONENTS`: Lock the concrete escape chain as acoustic relay, pressure seal, guidance timing, ascent energy and quarantine/legal handshake component packets.
- `RS074_PLAYER_EX_DEEP_REACH_PROFESSIONAL_DOSSIER`: Lock the player as former Deep Reach field-systems specialist and current debt-bound Marauder whose professional recognition becomes personal responsibility without family plot.
- `RS075_DEEP_REACH_LIE_PHYSICAL_PROOF_CHAIN`: Lock Deep Reach culpability as physical proof chain: real Great Tide physics plus accepted margins, evacuation delay, Atlas weighting, quarantine delay and claim-loss conversion.
- `RS076_ATLAS_FINAL_PAYLOAD_RECEIVER_PROTOCOLS`: Lock final payload receiver protocols for coordinate sale, Atlas severance, preserve/quarantine, public ledger and blind withholding refusal.
- `RS077_LONG_CAMPAIGN_ACT_SPINE`: Lock long campaign act gates for contract approach, photic survival, brine liability, abyssal repair and Atlas payload resolution.
- `RS078_MAJOR_POI_EVIDENCE_KITS`: Lock concrete major POI kits for P-63, cable reef relay yard, brine pump cathedral, evacuation queue terminal and Atlas service basin.
- `RS079_REPLAY_CONTRACT_SEED_FAMILIES`: Lock replay contract seed families for quiet salvage, storm-window rush, high-custody samples, evidence-first charter and Recovery Compliance bait.
- `RS080_PUBLIC_WIKI_ARTICLE_MODULES`: Lock public/wiki article modules for starting premise, no-FTL route, Aegir moon map, Deep Reach liability and Atlas spoiler gates.
- `RS081_COLONY_ANCHOR_WORKER_DOSSIERS`: Lock concrete worker dossiers for Mara Venn, Juno Kade, Ren Okoye, Sahana Iqbal and Lian Torres as job-evidence anchors, not family hooks.
- `RS082_DEEP_REACH_ARTIFACT_MEMO_PACK`: Lock concrete Deep Reach memo artifacts for accepted margins, Atlas weighting, quarantine hold, claim-loss conversion and present return pressure.
- `RS083_FAUNA_ENCOUNTER_GRAMMAR`: Lock fauna encounters as readable route grammar: predator shadow, glass grazer clearing, lantern drift ambiguity, brine vane navigation and sensor-tagged pursuit.
- `RS084_SITE_WIKI_NAVIGATION_CLUSTERS`: Lock site/wiki navigation clusters for start-here, system/ships, colony/workers, resources/ecology and spoiler-gated endings hubs.
- `RS085_CELESTIAL_EPHEMERIS_PUBLIC_BANDS`: Lock public distance bands, Aegir local window bands, HECTON-8 moon-ladder banding, Black Keel transfer-orbit bands and ephemeris table handoff rules.
- `RS086_RESOURCE_ECONOMY_ARTIFACTS`: Lock resource economy artifacts for blue debt custody, pressure glass certificates, brine process lots, Atlas lattice contamination and Black Keel payout mass ledgers.
- `RS087_PDA_CODEX_PRESENTATION_RULES`: Lock PDA, scanner, terminal, dossier and localized-overflow presentation rules as source strings and proof cards without claiming runtime UI implementation.
- `RS088_AUDIO_TRANSCRIPT_ARTICLE_SEEDS`: Lock transcript/article seeds for Black Keel approach, sanitized Deep Reach packets, worker dossiers, Atlas repair traces and ending records.
- `RS089_NUMERIC_GAMEPLAY_TABLE_VALUE_DRAFTS`: Lock value-band draft rows for resource yields, stack limits, escape recipes, contract risk/reward and ending payouts without choosing final numeric balance.
- `RS090_UNITY_PLACEMENT_SCENE_BRIEFS`: Lock Unity placement briefs for first-hour anchors, mid-depth route objects, ecology scan anchors, final descent anchors and terminal-slot promotion.
- `RS091_NATIVE_LOCALIZATION_AND_ACCESSIBILITY_QA_BRIEFS`: Lock native localization and accessibility QA briefs for RU encoding, CJK wrapping, RTL numeric direction, European expansion fit and subtitle/audio timing.
- `RS092_PUBLIC_SITE_LONGFORM_ARTICLE_BRIEFS`: Lock longform public/site article briefs for home, Aegir hard-sci-fi, Deep Reach liability, Atlas spoiler layers and blue debt resources.
- `RS093_LORE_SYSTEM_INTEGRATION_BRIDGE`: source bridge packets for future site/wiki/in-game surfaces and static-data bake contracts; no runtime proof.
- `RS094_PUBLIC_AUTHORITY_BRIDGE_EXPANSION`: source candidate bridge packets for public authority surfaces; runtime, source-CSV wiring, h8bin bake, native localization, Unity placement, and publication remain pending unless separately proven.
- `RS095_CORPORATE_PRESSURE_CHAIN_BRIDGE`: static-source candidate for corporate pressure-chain bridge packets; runtime, importer, h8bin, native localization, Unity placement, and publication readiness are false/pending verification in the release-set file.
