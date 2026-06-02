# HECTON-8 Lore Implementation Notes

Status: STATIC_LORE_CORPUS / APPLIED_LORE_SOURCE_PRESENT / SCENE_BINDINGS_PENDING
Evidence class: STATIC_DOC / STATIC_SOURCE / OFFLINE_AUDIT_TEXT
Purpose: what we are putting into project lore, docs, future data, and player-facing content.

## Current Source Reality

- AppliedContent is no longer only a future data target. Source route exists through `Tools/AppliedLoreImporter.py`, `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`, `H8AppliedLoreHashes`, `H8AppliedLoreRuntime`, `PDAEncyclopediaStreamer`, scanner targets, message terminals, and terminal OS preview handling.
- Current audit text for RS009 reports `45` packets, `15` locales, `675` AppliedLore records, `39` route records, and `scene_bindings=0`.
- Missing proof: non-zero scene/prefab `appliedLore...Hash` assignments, Unity import, Play Mode route, PDA/scanner/terminal visual route proof, and save/load continuity.
- Runtime content must consume baked DataMonolith records and stable hashes. Markdown, wiki, and external-site pages are authoring/publication output, not gameplay interpreters.

## Already Fixed In Docs

- `Lore_Bible.md`: 2190 present, non-Solar Aegir, no FTL, salvage-carrier arrival, damaged bathy-drop, Deep Reach proxy pressure, Atlas damaged repair logic, replay structure.
- `TASTE.md`: hard sci-fi / NASA-punk constraint for interstellar logistics and local orbital mechanics.
- `Narrative_Crystallization.md`: decision trail, source notes, real-system candidates, ship speed bands, human domains, Go2Starss propulsion source.

## We Are Implementing As Lore

- Aegir route as a no-FTL corporate claim system, probably anchored to Epsilon Eridani / Ran unless a later astronomy pass changes it.
- Aegir as one claim inside an already-expanded human sphere, not the first extrasolar destination.
- Human space by 2190 as layered domains: Sol Core, inner relay domains, independent or semi-independent habitats, corporate claim systems, dead/cold claims.
- Sparse mature frontier scale: enough civilization for law, debt, routes, claims, and salvage culture; not enough for quick rescue or clean oversight.
- HECTON-8 as a fictional ocean moon with pressure chemistry, tidal/geothermal support, layered geology, extractable industrial resources, and Deep Reach life support.
- Strategic resource layer: `Xenon-Omega` is locked as Deep Reach's corporate codename for a pressure-stable material/process family, not a literal simple isotope.
- Deep Reach as a legally present but physically distant power.
- Player as a Marauder salvage professional, economically trapped and technically competent.
- Carrier as local Aegir-system infrastructure, not player-owned freedom.
- Black Keel as public claim-pool infrastructure, legal debt/insurance custody, and hidden Deep Reach priority hooks.
- Aegir moon system as authored ephemeris/geology/reference data, not live N-body simulation.
- Blue debt as pressure-contained Xenon-Omega field slang with Atlas-compatible signal behavior and industrial/ecological contamination risk.
- False endings as real outcomes: material payout/truth failure and partial exit/same seed return.
- Player motive as earned progression: professional contract, recovered names, Barnard/frontier link, late contract trap.
- Escape as a chain:
  - repair or replace high-gain uplink;
  - recover ephemeris / carrier timing;
  - rebuild pressure-rated ascent package;
  - secure energy, buoyancy, thermal mass, or fuel equivalent;
  - wait for orbital/radiation/weather window;
  - decide what payload, evidence, or coordinates leave with the player.
- Return and ending pressure as game-facing content:
  - Black Keel contact is not rescue unless Aegir geometry, storm state, relay line, and ascent hardware line up;
  - early departure is a real partial ending after engineering work, not a fake fail screen;
  - material payout can be a valid success state while truth remains unresolved;
  - Deep Reach currently pushes resource custody, Atlas access, and proof deletion through proxy orders;
  - Atlas-6 final choices ask whether HECTON-8 can be owned, reset, preserved, severed, exposed, sold, or quarantined.
- Atlas-6 escalation by depth:
  - shallow: living ocean still beautiful;
  - mid-depth: drones, broken industrial modules, cable flora;
  - deep: fauna with industrial intrusion, stations as organs;
  - bottom: factory-ship temple, Deep Reach/Atlas/ocean fusion.

## We Are Implementing As Writing Infrastructure

- `Canon_Locks.md`: short truth source for stable decisions.
- `Open_Questions.md`: unresolved decisions that need user control.
- `Encyclopedia/`: player-facing or near-player-facing articles.
- `Encyclopedia/README.md`: rules for article tone and spoiler handling.
- `Lore_Content_System.md`: article/content packet rules for codex, terminal, website, and wiki use.
- `Lore_Localization_Model.md`: locale roster, LocID contract, and localization QA model.
- `Lore_Multilingual_Content_Architecture.md`: practical writing/export model for multilingual lore packets.
- `Gameable_World_Packets.md`: world-first packets that turn lore into scannable, lootable, explorable, replayable game material.
- `HECTON8_Field_Atlas.md`: depth-zone world atlas for biomes, POIs, resources, hazards, and truth hooks.
- `Aegir_System_Game_Texture.md`: Aegir sky, moons, windows, tides, and visible orbital pressure.
- `HECTON8_Resource_Gameplay_Catalog.md`: practical resource families, hazards, uses, and ending relevance.
- `Final_Payloads_Gameplay_Map.md`: physical/data payload families for endings and false exits.
- `Humanity_2190_Game_Texture.md`: how human domains appear through objects, tools, route labels, and worker culture.
- `Ships_Transit_Game_Texture.md`: how no-FTL travel, carriers, and ship classes appear as gameplay pressure.
- `ContentPacks/`: source packs for concrete player-facing world material.
- `AppliedContent/`: production-facing packets, in-game wiki articles, external site articles, image briefs, and release sets.
- `AppliedContent/graphs/`: evidence chains that connect baked packets to prerequisites, next leads, depth bands, decision pressure, and primary display surfaces.
- `Tools/AppliedLorePageExporter.py`: export bridge that creates localized in-game wiki and external-site markdown pages plus localized `INDEX.md` files from AppliedContent packet JSON. Existing hand-authored packet pages are preserved unless `--overwrite` is explicitly passed.
- `Tools/AppliedLoreRouteCardExporter.py`: export bridge that converts route-card authoring CSV into DataMonolith source data with stable route, phase, packet, prerequisite, surface, and ending-pressure hashes.
- Individual encyclopedia entries for:
  - Aegir system;
  - HECTON-8;
  - human domains;
  - relay spine;
  - corporate claims;
  - dead claims;
  - salvage economy;
  - Aegir route;
  - strategic pressure resources;
  - named human domains;
  - Aegir gas giant;
  - HECTON-8 geology and resources;
  - humanity overview;
  - technology overview;
  - Seed Program;
  - Deep Reach;
  - Marauders;
  - Atlas-6;
  - interstellar travel and ship classes.
  - Black Keel ownership/control;
  - Aegir astronomy reference and moon catalog;
  - blue debt field behavior;
  - false ending families;
  - player motive arc.

## Later Data Targets

These are still future content families, but the first runtime route is now implemented.

AppliedContent packets/routes are the current source-present runtime route. The targets below are additional content families unless they are already listed in `Runtime AppliedContent Route`.

- PDA encyclopedia records consume baked AppliedContent packet IDs from DataMonolith.
- Terminal articles and sealed corporate memos consume baked terminal/audio surfaces from DataMonolith.
- Scanner database entries can resolve baked AppliedContent titles/surfaces by packet hash.
- Contract dossier records.
- Marauder field notes.
- Old route telemetry / transit archives.
- Ending dossier summaries.
- Locale JSON entries for `ru_RU`, `ja_JP`, `zh_CN`, `fr_FR`, `es_ES`, `de_DE`, `pl_PL`, `uk_UA`, `ar_SA`, `id_ID`, `ko_KR`, `he_IL`, `pt_BR`, and `nl_NL`.

## Localization Target Roster

Production aliases are fixed as:

- `cn` means `zh_CN`.
- `ua` means `uk_UA`.
- `in` means `id_ID`.
- `kr` means `ko_KR`.
- `jewish` means `he_IL`.
- `portuguese` means `pt_BR` by default.

Open localization choices:

- Add `es_419` later or keep only `es_ES`.
- Add `pt_PT` later or keep `pt_BR` as the Portuguese default.
- Add `zh_TW` later or keep only `zh_CN`.
- Confirm `ar_SA` as the Modern Standard Arabic baseline or choose another Arabic default.

## Current Lore Growth Vector

Build outward in this order:

1. Human expansion model.
2. Route infrastructure and ship classes.
3. Deep Reach as interdomain corporate power.
4. Aegir as a late corporate claim.
5. HECTON-8 colony and catastrophe.
6. Atlas-6 and the HECTON-8 strategic-resource layer.
7. Marauder profession and player contract.
8. Replayable evidence ecology and endings.

## Immediate World Packet Priority

1. Broken bathy-drop and Black Keel first contact.
2. Bright shallow ecology as wonder/contrast. Source packet exists in RS007 as `P031_PHOTIC_SHELF_LIFE`.
3. Drowned worker spaces with named human texture.
4. Blue debt as handled, stabilized, sold, hidden, or carried.
5. Aegir sky windows as visible route pressure.
6. Barnard marks as the player's personal hook.
7. Atlas repair scars as the first non-random biomechanical horror. RS007 extends this into cable reefs, repair fauna, and factory-temple threshold packets.
8. Escape/ending pressure as real outcome hooks. RS008 adds return-vector windows, coward exit, material payout, Deep Reach cleanup order, and Atlas final argument packets.
9. Drowned worker spaces with named human texture. RS009 adds locker rows, pressure bunk routine, shift-board route holds, medical lock delays, and black-box name stacks.

These are the next practical units because they create player action, world texture, replay variation, and future localization surfaces.

## Active Content Packs

- `ContentPacks/CP01_Arrival_Shallow_Water.md`: first crash, first shelter, first beauty, first failed contact.
- `ContentPacks/CP02_Black_Keel_Aegir_Sky.md`: carrier calls, sky windows, orbital pressure.
- `ContentPacks/CP03_Drowned_Colony_Barnard_Hook.md`: worker rooms, names, Barnard marks, personal motive.
- `ContentPacks/CP04_Blue_Debt_Industrial_Descent.md`: pressure material, sample handling, contract temptation.
- `ContentPacks/CP05_Atlas_Repair_Scars.md`: biomechanical repair evidence and Atlas classification horror.
- `ContentPacks/CP06_Service_Canyons_Marauder_Caches.md`: mid-depth route salvage and prior Marauder traces.
- `ContentPacks/CP07_Brine_Thermal_Descent.md`: brine/thermal traversal and pressure chemistry.
- `ContentPacks/CP08_Deep_Abyss_Evacuation_Truth.md`: evacuation evidence and Deep Reach contradiction.
- `ContentPacks/CP09_Atlas_Bottom_Factory.md`: final zone factory-ship temple.
- `ContentPacks/CP10_Final_Payload_False_Exit.md`: ending payload and false exit source pack.
- `ContentPacks/CP_Source_Text_Bank.md`: first extractable text units.
- `ContentPacks/CP_POI_Unlock_Matrix.md`: tags and unlock routing for first content packs.

## Active Applied Release Sets

- `AppliedContent/release_sets/RS001_FIRST_DESCENT.md`: first production-facing bundle.
- `AppliedContent/release_sets/RS001_manifest.json`: release set manifest.
- `AppliedContent/packets/P001_CRASH_SHELF.json` through `P005_REPAIR_SCAR.json`: multilingual structured packets for all target locales.
- `AppliedContent/release_sets/RS002_DEEPENING_DESCENT.md`: second production-facing bundle for mid/deep descent and ending payload structure.
- `AppliedContent/release_sets/RS002_manifest.json`: RS002 release set manifest.
- `AppliedContent/packets/RS002_DEEPENING_DESCENT.packets.json`: bundled P006 dead-claim cache packet.
- `AppliedContent/packets/P007_BRINE_STAIR.json` through `P010_PAYLOAD_WINDOW.json`: multilingual structured packets for all target locales.
- `AppliedContent/release_sets/RS003_HUMAN_SPACE_AEGIR_ROUTE.md`: third production-facing bundle for broad human-space and route logistics.
- `AppliedContent/release_sets/RS003_manifest.json`: RS003 release set manifest.
- `AppliedContent/packets/RS003_HUMAN_SPACE_AEGIR_ROUTE.packets.json`: bundled P011-P015 multilingual structured packets for all target locales.
- `AppliedContent/release_sets/RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY.md`: fourth production-facing bundle for Aegir host light, moon ladder, HECTON-8 geology, resources, and ecology contrast.
- `AppliedContent/release_sets/RS004_manifest.json`: RS004 release set manifest.
- `AppliedContent/packets/RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY.packets.json`: bundled P016-P020 multilingual structured packets for all target locales.
- `AppliedContent/release_sets/RS005_CARRIER_ESCAPE_DEEP_REACH_PRESSURE.md`: fifth production-facing bundle for Black Keel custody, damaged drop capsule, current Deep Reach return pressure, material partial exit, and professional-to-personal motive.
- `AppliedContent/release_sets/RS005_manifest.json`: RS005 release set manifest.
- `AppliedContent/packets/RS005_CARRIER_ESCAPE_DEEP_REACH_PRESSURE.packets.json`: bundled P021-P025 multilingual structured packets for all target locales.
- `AppliedContent/release_sets/RS006_HUMANITY_TRANSIT_SEED_LOGISTICS.md`: sixth production-facing bundle for six-domain context, Barnard Yards origin, no-FTL transit math, Seed Ship doctrine, and relay custody debt.
- `AppliedContent/release_sets/RS006_manifest.json`: RS006 release set manifest.
- `AppliedContent/packets/RS006_HUMANITY_TRANSIT_SEED_LOGISTICS.packets.json`: bundled P026-P030 multilingual structured packets for all target locales.
- `AppliedContent/release_sets/RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE.md`: seventh production-facing bundle for photic shelf contrast, pressure ladder, cable reef symbiosis, repair fauna, and bottom factory-temple threshold.
- `AppliedContent/release_sets/RS007_manifest.json`: RS007 release set manifest.
- `AppliedContent/packets/RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE.packets.json`: bundled P031-P035 multilingual structured packets for all target locales.
- `AppliedContent/release_sets/RS008_ESCAPE_ENDINGS_ATLAS_QUESTION.md`: eighth production-facing bundle for return-vector windows, coward exit chain, material payout ledger, Deep Reach cleanup order, and Atlas final argument.
- `AppliedContent/release_sets/RS008_manifest.json`: RS008 release set manifest.
- `AppliedContent/packets/RS008_ESCAPE_ENDINGS_ATLAS_QUESTION.packets.json`: bundled P036-P040 multilingual structured packets for all target locales.
- `AppliedContent/release_sets/RS009_COLONY_LAYOUT_WORKER_EVIDENCE.md`: ninth production-facing bundle for drowned worker lockers, pressure bunk routine, shift-board route holds, medical lock delay, and black-box name stacks.
- `AppliedContent/release_sets/RS009_manifest.json`: RS009 release set manifest.
- `AppliedContent/packets/RS009_COLONY_LAYOUT_WORKER_EVIDENCE.packets.json`: bundled P041-P045 multilingual structured packets for all target locales.
- `AppliedContent/in_game_wiki/en_US` and `AppliedContent/in_game_wiki/ru_RU`: first player-facing wiki pages.
- `AppliedContent/external_site/en_US` and `AppliedContent/external_site/ru_RU`: first external publication pages.
- `AppliedContent/in_game_wiki/*/INDEX.md` and `AppliedContent/external_site/*/INDEX.md`: localized publication indexes for all current target locales.
- `AppliedContent/image_briefs/RS001_FIRST_DESCENT.md`, `RS002_DEEPENING_DESCENT.md`, `RS003_HUMAN_SPACE_AEGIR_ROUTE.md`, `RS004_AEGIR_SYSTEM_HECTON8_ECOLOGY.md`, `RS005_CARRIER_ESCAPE_DEEP_REACH_PRESSURE.md`, `RS006_HUMANITY_TRANSIT_SEED_LOGISTICS.md`, `RS007_DEPTH_ECOLOGY_FACTORY_TEMPLE.md`, `RS008_ESCAPE_ENDINGS_ATLAS_QUESTION.md`, and `RS009_COLONY_LAYOUT_WORKER_EVIDENCE.md`: image/art direction for release sets.
- `AppliedContent/route_cards/*_route_cards.csv`: checked gameplay route-card layer for all current AppliedContent release sets.

## Locked Current Answers

- Named domain count: 6 major nodes, smaller systems implied.
- Player origin: Barnard Yards / connected frontier salvage belt, not Earth/Sol.
- Deep Reach age: older than Aegir; Aegir is one of its dirtiest later projects.
- Aegir public profile: known to specialists, insurers, Marauders, and corporations; ordinary citizens know it only as a distant old accident if at all.
- Xenon-Omega: Deep Reach codename for pressure-grown xenon-rich clathrate/defect lattices and associated processing, used for extreme computation, high-energy containment, and Atlas-compatible pressure infrastructure.
- Black Keel ownership: public Aegir claim-pool tender; legal debt/insurance custody; hidden Deep Reach priority hooks.
- Blue debt behavior: pressure containment, weak Atlas-compatible pressure-harmonic signal, industrial/ecological contamination.
- False endings: Material Ending and Partial Exit are the minimum families.
- Player motive: professional interest becomes personal through recovered names and Barnard/frontier link; the contract trap arrives later.

## Runtime Constraint

Lore delivery must stay data-driven and event-triggered.

- No hot scene search.
- No runtime markdown/prose interpreter.
- No runtime procedural text generation for core truth.
- No allocation-heavy lore routing.
- Long articles belong in static data or compiled localization/content blobs.
- Variable replay should alter discovery order, context, and presentation, not the underlying canon truth.
- Build-time tools may parse writer docs and export static records; gameplay systems should consume only IDs, enums, offsets, flags, and localized string hashes.

## Runtime AppliedContent Route

Status: implemented for the first release packets.

- Authoring source: `Docs/Lore/AppliedContent/release_sets/*_manifest.json` and referenced packet JSON.
- Importer: `Tools/AppliedLoreImporter.py`.
- Publication exporter: `Tools/AppliedLorePageExporter.py`.
- Generated source data: `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`.
- Generated hash constants: `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs`.
- Baked blob section: `H8DataSectionId.AppliedLorePackets` in `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- Runtime DTO: `H8AppliedLorePacketRecord`, 128 bytes, sorted by `PacketHash` then `LocaleHash`.
- Runtime text pool: offsets and byte lengths point into the existing DataMonolith UTF-8 localization pool.
- Runtime facade: `H8AppliedLoreRuntime.TryGetUtf8` and span-copy UTF-16 helpers.
- PDA route: `PDAEncyclopediaStreamer` seeds baked packet metadata and streams `InGameWiki` text.
- Scanner route: `ScannableTarget.TryWriteLoreEntityTitle` can resolve baked packet titles.
- Terminal route: `MessageTerminal` exposes terminal/audio/title span-copy methods and publishes `AppliedLoreTerminalPreviewSignal`; `TerminalOsRuntime` consumes that signal in `LateFrameTick` and applies a private bounded preview writer to diegetic terminal screens.
- Unlock route: `H8AppliedLoreRuntime.TryRaisePacketUnlocked` pushes hash-only `LoreFragmentScannedSignal` and clears `FlagHasAup`/`FlagPairedScanComplete`. `TryRaisePacketUnlockedAt` and scanner completion push the same lane with finite AUP, `FlagHasAup`, and `FlagPairedScanComplete` when they also publish `ScanCompleteSignal`. PDA consumes the lore-fragment lane and unlocks/selects the baked packet without a markdown/JSON interpreter, but skips paired lore-fragment duplicates when the matching scan-complete payload is already in the same snapshot.
- Scanner legacy bridge: `ScannerDataMiningRouter.OnEnable` prewarms `ScanEvents` native queues through `EnsureInitializedCold`, so `TryRaiseEntryDiscovered` cannot create the bridge queues during scan completion.
- Scene authoring hooks:
  - `NarrativeDiscovery.appliedLorePacketHash` unlocks an AppliedContent packet on direct interaction and through AUP POI trigger bake.
  - `ScannableFragment.appliedLoreQuarterPacketHash`, `appliedLoreHalfPacketHash`, and `appliedLoreFinalPacketHash` unlock staged PDA/wiki packets at 25%, 50%, and completion.
  - `MessageTerminal.appliedLorePacketHash` unlocks the terminal's packet when accessed.
  - `NarrativeSpatialTriggerAuthoring.AppliedLoreHash` carries POI packet identity into native AUP trigger presentation data without changing DTO size.
- Evidence authoring graph: `AppliedContent/graphs/*_evidence_graph.csv` connects current packets to arcs, depth bands, prerequisites, next leads, evidence types, player decisions, spoiler tiers, and primary display surfaces.
- Route-card authoring: `AppliedContent/route_cards/*_route_cards.csv` groups current packets into gameplay phases with depth bounds, required packet references, primary surfaces, world-object hints, player questions, replay axes, and ending-pressure categories.
- Route-card source data: `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv` carries the same route cards as stable hash/flag rows.
- Route-card baked section: `H8DataSectionId.AppliedLoreRoutes`, section `28`, uses `H8AppliedLoreRouteRecord`, 128 bytes, sorted by `RouteCardHash`.
- Route-card runtime fields: route-card hash, phase hash, depth min/max, primary surface mask, ending-pressure hash, up to 8 packet hashes, up to 4 prerequisite packet hashes, flags, and record index.
- Route-card runtime facade: `H8AppliedLoreRuntime.TryFindRoute`, `TryGetRouteAt`, and `TryFindRouteForPacket` route UI/event consumers to baked records without CSV parsing.
- Publication pages: all current packet IDs have localized `in_game_wiki` and `external_site` markdown files plus localized indexes for all 15 target locales; runtime still consumes baked DataMonolith strings rather than these markdown files.

Current full-bake proof before RS004/RS005 re-bake: AppliedLore packet section `27`, record size `128`, record count `225`; AppliedLore route section `28`, record size `128`, record count `9`; schema `0x33313332`.

Current RS009 bake proof: AppliedLore packet section `27` contains `675` records for 45 packets x 15 locales; AppliedLore route section `28` contains `39` route records in the active `static_data.h8bin`.

Offline audit proof:

- Tool: `Tools/AppliedLoreRuntimeAudit.py --root .`.
- Source-only mode: `Tools/AppliedLoreRuntimeAudit.py --root . --source-only` validates authoring/export/page coverage when CPU/compiler contention blocks a fresh DataMonolith bake.
- Checks: authoring CSV header and packet/locale matrix, generated packet/locale/surface constants, DataMonolith header/directory, section table, AppliedLore packet section count/record size, AppliedLore route section count/record size, strict packet+locale sort order, packet record hashes, route record hashes, route record indices, depth bounds, packet/prerequisite hash slots, reserved route padding, surface masks, localization bounds, NUL terminators, exact UTF-8 byte equality for each baked surface, source-route symbols for importer, DTO, compiler, runtime facade, PDA, scanner, terminal, TerminalOS, direct discovery, staged scans, AUP POI triggers, the runtime binding map, the evidence graph, route cards, route-card source-data export, localized wiki/site page coverage, and localized publication indexes.
- Last fully baked result before RS004/RS005 authoring: `AppliedLore audit OK: packets=15 locales=15 rows=225 blob_bytes=1291840 localization_bytes=198328 applied_records=225 applied_routes=9 source_route=ok binding_map_rows=15 graph_rows=15 route_cards=9 route_source_rows=9 wiki_pages=225 site_pages=225 index_pages=30 scene_bindings=0`.
- Current source-only result after RS009 authoring: `AppliedLore source audit OK: packets=45 locales=15 rows=675 source_route=ok binding_map_rows=45 graph_rows=45 route_cards=39 route_source_rows=39 wiki_pages=675 site_pages=675 index_pages=30 scene_bindings=0`.
- Current full audit result after RS009 authoring: `AppliedLore audit OK: packets=45 locales=15 rows=675 blob_bytes=1652736 localization_bytes=497790 applied_records=675 applied_routes=39 source_route=ok binding_map_rows=45 graph_rows=45 route_cards=39 route_source_rows=39 wiki_pages=675 site_pages=675 index_pages=30 scene_bindings=0`.
- Current authoring gap: scene/prefab YAML does not yet contain non-zero `appliedLore...Hash` assignments. Runtime route exists; content must still be assigned to concrete scene POIs, scan fragments, or terminals when Unity ownership is clear.
- Runtime impact: 0 us/frame; offline verification only.
