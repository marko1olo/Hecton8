# HECTON-8 Applied Content

Status: production-facing content layer.
Purpose: store content that can be used by the game, in-game wiki, external website/wiki, localization, audio, and image/art tasks.

This folder is not an internal rationale archive.

## Folders

- `packets/`: structured content packets with all target surfaces and translations.
- `in_game_wiki/`: player-facing wiki/codex articles.
- `external_site/`: publication-ready website/wiki articles.
- `image_briefs/`: art prompts and image requirements for articles, codex, cards, and marketing.
- `release_sets/`: grouped publish/runtime batches.
- `binding_maps/`: scene-authoring maps for assigning baked packet hashes to concrete POIs, scan fragments, and terminals.
- `graphs/`: evidence-chain maps for packet prerequisites, next leads, depth bands, decision pressure, and primary display surfaces.
- `route_cards/`: gameplay route cards tying packet groups to phases, depth bounds, replay axes, and ending pressure.

## Current Release Sets

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
- Route-card export: `Tools/AppliedLoreRouteCardExporter.py` converts checked route cards into `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv` with stable route, phase, packet, prerequisite, surface, and ending-pressure hashes.
- CSV export: `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`.
- Constants export: `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs`.
- Bake targets: `H8DataSectionId.AppliedLorePackets` and `H8DataSectionId.AppliedLoreRoutes` in `static_data.h8bin`.
- Offline audit: `Tools/AppliedLoreRuntimeAudit.py --root .` checks CSV rows, generated constants, baked packet and route section layouts, sorted packet/locale records, every UTF-8 text slice against the blob, route-card fixed-record hashes/depths/prerequisites/slot padding, source-route symbols from importer through PDA/scanner/terminal/POI consumers, all `binding_maps/*_runtime_binding_map.csv`, all `graphs/*_evidence_graph.csv`, all `route_cards/*_route_cards.csv`, the route-card source-data export, publication page coverage for all baked packet/locale pairs, and localized publication indexes. Use `--source-only` when authoring has advanced but `static_data.h8bin` cannot be rebuilt yet.
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
