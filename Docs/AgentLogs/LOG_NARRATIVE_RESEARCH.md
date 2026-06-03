# LOG_NARRATIVE_RESEARCH

2026-06-02 Control Pass 81 - Apex verifier scope and scene-risk separation

What was wrong:
Fresh Unity Apex reduced the previous broad red state to code-verifier drift plus one real scene-placement fact. `ScanEvents.cs` existed but the verifier scope missed `GlobalSignals.RuntimeLifecycle.cs`; generic/static SignalBus calls were miscounted as missing; `Monitor.Enter` gate locks were miscounted as zero; world-domain scene markers were hard failures inside a Narrative/AppliedLore verifier. Current `02_HECTON_WORLD.unity` also has no scene-owned TerminalOS runtime placement.

What was done:
Updated `H8NarrativeApexVerifier` to include `GlobalSignals.RuntimeLifecycle.cs`, count full static/generic invocations through `MatchesInvocation`, count `Monitor.Enter` as a gate lock, and report `scene_world_dependency_warnings`/`terminal_os_scene_binding_warnings`. Updated `Tools/AppliedLoreRuntimeAudit.py` so MapMagic/Terrain/TerrainCollider absence is visible as `scene_world_dependency_warnings` instead of failing the AppliedLore content/data audit.

Cinematic Cheats used:
Static AST/source-route proof and content blob audit. No Play Mode probing, no runtime scene search, no `dotnet build`, no raw scene YAML repair.

Exact microseconds saved:
0 us/frame. All changes are editor/offline proof tools; runtime DTOs, SignalBus payloads, DataMonolith layout, and save identity unchanged.

Proof:
Local in-memory Roslyn parse over 7 linked C# files reports `ROSLYN_PARSE_CHECK files=7 errors=0`. AppliedLore source/full audits pass with `packets=55`, `locales=15`, `rows=825`, `blob_bytes=1804864`, `applied_records=825`, `applied_routes=49`, `route_cards=49`, `wiki_pages=825`, `site_pages=825`, and `index_pages=30`. Unity Apex run before the latest scene-warning compilation already reports code protocol green: `dependency_findings=1` only from `terminal_os_runtime_missing`, `phase_findings=0`, `zero_gc_findings=0`, `job_complete_findings=0`, `lock_findings=0`, `terminal_os_dump_gate_locks=1`, `terminal_os_dump_writes_after_lock=1`, `ui_rescale_layout_snapshot_reads=1`, `pda_ui_rescale_snapshot_reads=1`, `message_terminal_hash_cold_caches=5`, `build_invocations=0`, `analysis=RoslynAST_in_memory`. Current scene placement remains unclaimed: source/full audits report `scene_terminal_os_runtime_rows=0`, `scene_terminal_os_runtime_verified_slots=0`, `scene_bindings=0`, `scene_world_dependency_warnings=3`.

2026-06-02 Control Pass 82 - TerminalOS preview hash guard and scene binding proof

What was wrong:
Scene-owned TerminalOS placement was no longer absent, but 27 `terminalOsPreviewHash` overrides were wrong. They used AppliedLore packet/message hashes while `TerminalOsRuntime` resolves previews by `TerminalOsHash.HashIndex(index)`. This could leave the scene looking wired while preview routing fails.

What was done:
Corrected the 27 serialized `terminalOsPreviewHash` values in `Assets/_Project/Scenes/02_HECTON_WORLD.unity`. Added `H8NarrativeApexVerifier` checks for `terminal_os_preview_hash_pairs`, `terminal_os_preview_hash_mismatches`, and duplicate preview indices. Kept runtime code unchanged: no new hot path lookup, no scene search, no DTO layout or SignalBus payload change. Cross-domain Fauna verifier compile-wall fix remains limited to `TypeSyntax` vs `ArrayTypeSyntax` static parsing.

Cinematic Cheats used:
Static scene/data identity proof instead of runtime self-healing. Editor-only C# verifier catches drift before Play Mode. No physical/runtime simulation added.

Exact microseconds saved:
0 us/frame. Runtime remains unchanged. Avoided future per-frame fallback/search cost by fixing serialized identity.

Proof:
`python Tools/AppliedLoreRuntimeAudit.py --root .` passes: `packets=55`, `locales=15`, `rows=825`, `blob_bytes=1804864`, `applied_records=825`, `applied_routes=49`, `scene_terminal_preview_rows=27`, `scene_terminal_os_runtime_rows=1`, `scene_terminal_os_runtime_renderer_slots=27`, `scene_terminal_os_runtime_transform_slots=27`, `scene_terminal_os_runtime_verified_slots=27`, `scene_world_dependency_warnings=0`, `scene_bindings=7`, `prefab_bindings=43`, `wiki_pages=825`, `site_pages=825`. Source-only audit passes with the same scene binding counts. Unity `validate_script` on `H8NarrativeApexVerifier.cs` reports 0 errors/0 warnings. Raw scene structure check for `m_RootGameObject` succeeded. Unity Apex menu proof after one throttled script refresh/import reports `terminal_os_preview_hash_pairs=27`, `terminal_os_preview_hash_mismatches=0`, `terminal_os_preview_hash_duplicate_indices=0`, `dependency_findings=0`, `phase_findings=0`, `zero_gc_findings=0`, `job_complete_findings=0`, `lock_findings=0`, `fatal_findings=0`, `build_invocations=0`, `analysis=RoslynAST_in_memory`.

Process hygiene:
Stopped stale `rg.exe` PIDs 12544, 18908, 16748 from earlier agent searches. Repeat PID check returned no live process. Left Python servers, Unity, and Unity Roslyn/dotnet processes untouched due missing ownership proof.

Console noise check:
Unity Console initially held 19 stale missing-script messages. Static YAML sweep across scenes/prefabs/assets found 0 `m_Script: {fileID: 0}`, 0 empty GUID script refs, and 0 zero-GUID script refs. After clearing Console and rerunning Narrative Apex, only MCP command log plus green Apex verifier log returned; missing-script messages did not recur.

Control Pass 83: hardened MetaCampaign visual phase route. `MetaCampaignService` now queues visual state through primitive pending fields and flushes `Shader.SetGlobalFloat(_HectonOceanToxicity)` plus `IEcosystemDirectorService.ApplyCampaignToxicityPressure` only from `LateFrameTick`. No DTO, save identity, DataVault handle, or SignalBus payload changed. `H8NarrativeApexVerifier` now includes `MetaCampaignService.cs`, `CorporateOrderSystem.cs`, `LoreDatabaseManager.cs`, `ProceduralLoreDirector.cs`, and `AwaitableDropSequenceDirector.cs`; it also verifies DataVault write-lock transfer helpers and reports/fails MetaCampaign visual phase counters.

Verification: scoped hot dependency scan over Narrative/AppliedLore runtime files found no `GlobalRegistry.Get<T>()`, `GetComponent`, `TryGetComponent`, or `GetComponents`. AppliedLore runtime audit passes with `packets=55`, `rows=825`, `applied_records=825`, `applied_routes=49`, `scene_terminal_os_runtime_verified_slots=27`, `scene_bindings=7`, `prefab_bindings=43`, `wiki_pages=825`, `site_pages=825`. Local brace lexer reports `MetaCampaignService.cs brace=0 minBrace=0 paren=0 bracket=0` and `H8NarrativeApexVerifier.cs brace=0 minBrace=0 paren=0 bracket=0`. `git diff --check` reports only the CRLF normalization warning for `MetaCampaignService.cs`. Unity MCP `validate_script` disconnected once and timed out once; CPU returned to 100% with VBCSCompiler PID 26752 active, so `dotnet build`, Unity refresh, and Apex menu rerun were not launched in this pass.

Control Pass 84: closed MetaCampaign audio/cartography side-effect phase route. `PublishStateSideEffects` now queues audio and cartography through primitive dirty flags; `LateFrameTick` drains `FlushCachedVisualState`, `FlushCampaignBroadcast`, and `FlushCartographyState`. Direct `SignalBus<VocalWarningSignal>` and `SignalBus<NarrativePoiStateSignal>` publication remains only inside the flush-owned publish helpers. No DTO, save identity, DataVault handle, public API, or SignalBus payload changed.

Verification: source route scan shows `LateFrameTick` owns one call each to `FlushCampaignBroadcast` and `FlushCartographyState`; `PublishStateSideEffects` owns queue calls only; `PublishCampaignBroadcast` and `PublishCartographyState` are called only by their flush methods. Scoped hot dependency grep over active Narrative/AppliedLore runtime files found no runtime `GlobalRegistry.Get<T>()`, `GetComponent`, `TryGetComponent`, or `GetComponents`; the only `GlobalRegistry.Get<` hit is the verifier's forbidden-pattern scanner text. Write-lock inspection shows variable/rules locks release in `finally`; blackbox writes acquire their own lock after variable/rules writes are released. Whitespace scan reports no trailing whitespace. Unity `validate_script` on `H8NarrativeApexVerifier.cs` reports 0 errors/0 warnings. `MetaCampaignService.cs` validation disconnected once and timed out once; CPU returned to 90% with active `dotnet` PID 26752, so Unity refresh, Apex menu, and `dotnet build` were not launched.

Cinematic Cheats used: one-frame side-effect coalescing in `LateFrameTick` instead of immediate presentation/audio/cartography fan-out from state mutation. Exact microseconds saved: no measured runtime proof; theoretical normal-frame cost is two bool branches, 0 B GC, with side-effect work shifted to visual-sync timing.

Control Pass 85: locked latest user lore and propagated it through AppliedLore source.

What was wrong:
Canon docs still carried the old caution against an explicit ex-Deep-Reach protagonist, while the current user lock says the player is former Deep Reach and now Marauder. The Deep Reach crime also needed a cleaner hard-sci-fi split: Great Tide as real HECTON-8 physics, corporate guilt as evacuation/priority/cover-up failure. Discussion-level lore was not enough unless it entered packets, wiki/site pages, route cards and binding maps.

What was done:
Updated `Canon_Locks.md`, `Open_Questions.md`, `Lore_Bible.md`, and `Narrative_Crystallization.md`. Added AppliedLore `RS012_PLAYER_LIABILITY_ESCAPE`, `RS013_COLONY_ATLAS_MAINTENANCE`, and `RS014_COLONY_RETURN_WINDOWS` with packets `P056`-`P070`. Each packet has scanner, terminal, audio, field note, in-game wiki, external-site text, EN/RU localization, draft-filled other target locales, unlock tags, evidence graph row, route card, runtime binding map row, scene binding target backlog row, and image brief coverage. Regenerated `applied_lore_packets.csv`, `applied_lore_route_cards.csv`, `H8AppliedLoreHashes.cs`, localized wiki pages, external-site pages and indexes.

Cinematic Cheats used:
Static AppliedLore packet route and baked-string-pool source path. No runtime markdown parsing, no runtime translation, no Unity scene edit, no C# gameplay change.

Exact microseconds saved:
0 us/frame in this pass. Content goes through baked DataMonolith source rows and generated hash constants; no hot loop added.

Proof:
`AppliedLoreImporter.py --root .` reports `applied_lore_packets=70 localized_rows=1050`. `AppliedLorePageExporter.py --root .` reports `applied_lore_pages_written=450 skipped_existing=1650 index_pages_written=30`. `AppliedLoreRouteCardExporter.py --root .` reports `applied_lore_route_cards=64`. `AppliedLoreRuntimeAudit.py --root . --source-only` passes with `packets=70`, `rows=1050`, `binding_map_rows=70`, `target_backlog_rows=70`, `graph_rows=70`, `route_cards=64`, `route_source_rows=64`, `wiki_pages=1050`, `site_pages=1050`, `index_pages=30`, `scene_world_dependency_warnings=0`. `rg` found no stale `Do not start with explicit/open ex-Deep` guard in `Docs/Lore`. `git diff --check` over touched lore/data scope is clean except Git CRLF normalization warnings. Full baked `static_data.h8bin` proof was not claimed because no Unity/DataMonolith bake was run in this lore-only pass.

Control Pass 86: expanded AppliedLore from personal/corporate opening locks into wider setting infrastructure.

What was wrong:
Human domains, Aegir moons and HECTON-8 geology were still too easy to treat as internal background instead of player-facing evidence. The setting needed object-level signs for Sol/Centauri/Barnard/Tau/Luyten, route-functional moons around Aegir, and pressure geology that directly explains exploration and replayability.

What was done:
Added `RS015_HUMAN_DOMAINS_ROUTE_ECONOMY`, `RS016_AEGIR_SYSTEM_MOON_LADDER`, and `RS017_HECTON8_GEOLOGY_RESOURCE_ECOLOGY` with packets `P071`-`P085`. Propagated them through packet bundles, manifests, release docs, evidence graphs, route cards, binding maps, scene binding target backlogs, image briefs, localized in-game wiki pages, external-site pages, indexes, `applied_lore_packets.csv`, `applied_lore_route_cards.csv`, and `H8AppliedLoreHashes.cs`. Synced `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, and `Narrative_Crystallization.md`.

Cinematic Cheats used:
Object-first lore and static route-card evidence instead of dense exposition or live simulation. Geology is readable through bands, props, scanner text, brine/vent presentation and POI order.

Exact microseconds saved:
0 us/frame. Authoring/export content only. No runtime parser, live translation, scene search, Unity compile or `dotnet build`.

Proof:
Importer reached `applied_lore_packets=85 localized_rows=1275`; page exporter wrote 450 new pages; route-card exporter reached `applied_lore_route_cards=79`; source-only audit passed with `packets=85`, `rows=1275`, `graph_rows=85`, `route_cards=79`, `wiki_pages=1275`, `site_pages=1275`, `binding_map_rows=85`.

Control Pass 87: locked carrier debt, physical atlas and ending agency into AppliedLore.

What was wrong:
Black Keel's exact legal shell, debt model, first voice, HECTON-8 physical origin/depth atlas, blue-debt containment, Atlas recognition boundary, present Deep Reach faction and endings were still too vague for production. These are not side lore; they drive opening UI, contract pressure, progression, replay meta and final choices.

What was done:
Added `RS018_CARRIER_DEBT_CLAIM_AUTHORITY`, `RS019_HECTON8_PHYSICAL_ATLAS_DEPTH_BANDS`, and `RS020_ATLAS_ENDING_AGENCY_DOSSIER` with packets `P086`-`P100`. Locked `Aegir Reclamation Pool`, `Keelmark Mutual`, `4.8 tonne-window` lien, clipped-audio/clean-text Black Keel first voice, Deep Reach priority hooks, Aegir-formed collision-fractured HECTON-8, five ocean depth bands, rare seafloor windows, seed invariants, pressure-containment stages 0-4, Atlas person-boundary, `Recovery Compliance Office`, false-ending taxonomy, Marauder dossier persistence and final payload choices. Propagated all new packets through the same AppliedLore surfaces and synchronized canon/open-question docs.

Cinematic Cheats used:
Contract pressure becomes mass-through-window text and payload scale, not expensive simulation. Depth bands and seafloor windows give procedural structure without simulating a whole planet. Atlas intimacy comes from precise classification text, not humanized AI cutscenes.

Exact microseconds saved:
0 us/frame in this pass. No gameplay runtime code added. Generated hash constants only through existing importer.

Proof:
`AppliedLoreImporter.py --root .` reports `applied_lore_packets=100 localized_rows=1500`. `AppliedLorePageExporter.py --root .` reports 450 new pages. `AppliedLoreRouteCardExporter.py --root .` reports `applied_lore_route_cards=94`. `AppliedLoreRuntimeAudit.py --root . --source-only` passes with `packets=100`, `locales=15`, `rows=1500`, `binding_map_rows=100`, `target_backlog_rows=100`, `graph_rows=100`, `route_cards=94`, `route_source_rows=94`, `wiki_pages=1500`, `site_pages=1500`, `index_pages=30`. `git diff --check` over touched lore/data scope is clean except Git CRLF normalization warnings. Full baked `static_data.h8bin` proof was not claimed because no Unity/DataMonolith bake was run.

Control Pass 88: locked transit doctrine, Deep Reach signoff chain, first tools and resource taxonomy into AppliedLore.

What was wrong:
Interstellar route logic was still too broad to prevent FTL/rescue drift. Deep Reach liability had procedural shape but not enough named signatures. First hour had story beats without exact tool-chain objects. Resource lore was still "blue debt plus other materials" rather than a clean gameplay/scanner taxonomy. `Player_Motive_Arc.md` also still carried a stale guard against an ex-Deep-Reach protagonist.

What was done:
Added `RS021_INTERSTELLAR_TRANSIT_ROUTE_HISTORY`, `RS022_DEEP_REACH_SIGNOFF_CHAIN`, `RS023_FIRST_TOOL_CHAIN_SURVIVAL_GATE`, and `RS024_RESOURCE_RECIPE_TAXONOMY` with packets `P101`-`P120`. Locked no-FTL route economy, beam-sail probe era, pellet-fusion freight doctrine, RAN-B:H8 catalog language, Black Keel in-system tender limits, Iliya Varnek, Selene Arendt, Noor Haldane, Marek Ibarra, Vera Sato-Ren, manual bilge pump kit, cold sealant patch gun, low-power induction cutter, acoustic pinger line, P-63 field fabricator, native sulfide/salt resources, noble-gas brine feedstock, Deep-Reach pressure ceramics, Atlas biofiber sealant and biometal sensor tags. Propagated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized wiki pages, external-site pages, indexes, source CSV rows and `H8AppliedLoreHashes.cs`. Synced `Canon_Locks.md`, `Open_Questions.md`, `Lore_Bible.md`, `Narrative_Crystallization.md`, `Aegir_Gas_Giant.md`, `Player_Motive_Arc.md`, and AppliedContent README.

Cinematic Cheats used:
Route windows and no-FTL pressure remain authored packet/window content, not live N-body truth. Tool and resource beats are object/evidence hooks first; visual overkill can layer later without changing packet IDs.

Exact microseconds saved:
0 us/frame in this pass. No runtime parser, live translation, scene search, Unity compile or `dotnet build`.

Proof:
`AppliedLoreImporter.py --root .` reports `applied_lore_packets=120 localized_rows=1800`. `AppliedLorePageExporter.py --root .` reports 600 new pages. `AppliedLoreRouteCardExporter.py --root .` reports `applied_lore_route_cards=114`. `AppliedLoreRuntimeAudit.py --root . --source-only` passes with `packets=120`, `locales=15`, `rows=1800`, `binding_map_rows=120`, `target_backlog_rows=120`, `graph_rows=120`, `route_cards=114`, `route_source_rows=114`, `wiki_pages=1800`, `site_pages=1800`, `index_pages=30`. Full baked `static_data.h8bin` proof was not claimed because no Unity/DataMonolith bake was run.

Control Pass 89: locked law, Atlas classification, false exits and dossier replay into AppliedLore.

What was wrong:
Human legal authority, public Aegir memory, Marauder legal status, salvage evidence rules, Atlas public/legal/classified status, shutdown ethics, false exit shape and replay persistence were still partly open. That would make future writing drift between hard-sci-fi legal pressure, generic corporate villainy, fake fail endings and power-roguelite persistence.

What was done:
Added `RS025_HUMAN_LAW_PUBLIC_MEMORY`, `RS026_ATLAS_PUBLIC_AUTHORITY_CLASSIFICATION`, `RS027_FALSE_EXIT_RETURN_PRESSURE`, and `RS028_REPLAY_CONTRACT_DOSSIER_RULES` with packets `P121`-`P140`. Locked the six-node authority split, Marauder jurisdiction loophole, chain-of-custody / witness hash / relay-notary evidence route, normal public Aegir memory, Deep Reach origin through existing route economics, Atlas public front, Atlas insurance/personhood gap, classified weighting layer, multi-valued shutdown ethics, material payout, same-seed partial return, corporate capture, quarantine hold, public ledger leak, riskier contracts, claim variants and dossier knowledge-not-power persistence. Propagated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized wiki pages, external-site pages, indexes, source CSV rows and `H8AppliedLoreHashes.cs`. Synced `Canon_Locks.md`, `Open_Questions.md`, `Lore_Bible.md`, `Narrative_Crystallization.md`, and AppliedContent README.

Cinematic Cheats used:
Law, replay and false exits are authored route/packet pressure, not expensive runtime simulation. Atlas shutdown ambiguity comes from payload route and evidence state, not a cinematic-only binary switch.

Exact microseconds saved:
0 us/frame in this pass. No runtime parser, live translation, scene search, Unity compile or `dotnet build`.

Proof:
`AppliedLoreImporter.py --root .` reports `applied_lore_packets=140 localized_rows=2100`. `AppliedLorePageExporter.py --root .` reports 600 new pages. `AppliedLoreRouteCardExporter.py --root .` reports `applied_lore_route_cards=134`. `AppliedLoreRuntimeAudit.py --root . --source-only` passes with `packets=140`, `locales=15`, `rows=2100`, `binding_map_rows=140`, `target_backlog_rows=140`, `manual_rows=54`, `manual_discovery_policy_rows=27`, `scene_terminal_os_runtime_verified_slots=27`, `graph_rows=140`, `route_cards=134`, `route_source_rows=134`, `wiki_pages=2100`, `site_pages=2100`, `index_pages=30`. Full baked `static_data.h8bin` proof was not claimed because no Unity/DataMonolith bake was run. TerminalOS scene expansion was deliberately avoided; new manual rows route through `NarrativeDiscovery` backlog until a Unity scene pass owns placement.

Control Pass 90: locked route time, Deep Reach shell org chart, first hour and colony human evidence into AppliedLore.

What was wrong:
Ran/Aegir distance and travel time were still broad enough to drift into soft rescue convenience or dry astronomy. Deep Reach had named signatures but no stable public/shell org hierarchy. The first hour had mood and tools but not enough playable sequence objects. Colony humanity could still slide into either anonymous ruins or family melodrama.

What was done:
Added `RS029_ROUTE_TIME_DISTANCE_MODEL`, `RS030_DEEP_REACH_SHELL_ORG_CHART`, `RS031_FIRST_HOUR_PLAYABLE_SPINE`, and `RS032_COLONY_HUMAN_EVIDENCE_LAYER` with packets `P141`-`P160`. Locked Ran/Aegir as a roughly 10.5 light-year class target, probe packet timing, heavy freight staging, human crew rotation delay, relay message lag, Deep Reach Extraterrestrial Development Combine, Aegir Continuity Holdings, Atlas Continuity Office, Keelmark Loss Desk, Recovery Compliance chain, Black Keel contract approach, drop capsule damage sequence, Shallow Annex P-63, first sanitized accident packet, first Atlas repair trace, shift crews, job cards, locker name protocol, triage ledger and Marauder correction layer. Propagated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized wiki pages, external-site pages, indexes, source CSV rows and `H8AppliedLoreHashes.cs`. Synced `Canon_Locks.md`, `Open_Questions.md`, `Lore_Bible.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README.

Cinematic Cheats used:
Distance is felt through packet timestamps, route plates, debt and custody instead of live orbital simulation. Corporate hierarchy is stamps/ledgers/routes, not exposition scenes. First-hour drama is built from physical prop states and repair tasks. Colony humanity comes from work evidence and correction notes, not costly bespoke family cinematics.

Exact microseconds saved:
0 us/frame in this pass. No runtime parser, live translation, scene search, Unity compile or `dotnet build`. The only generated C# touched is `H8AppliedLoreHashes.cs` through the existing importer.

Proof:
`AppliedLoreImporter.py --root .` reports `applied_lore_packets=160 localized_rows=2400`. `AppliedLoreRouteCardExporter.py --root .` reports `applied_lore_route_cards=154`. `AppliedLoreRuntimeAudit.py --root . --source-only` passes with `packets=160`, `locales=15`, `rows=2400`, `binding_map_rows=160`, `target_backlog_rows=160`, `manual_policy_rows=74`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=47`, `scene_terminal_os_runtime_verified_slots=27`, `graph_rows=160`, `route_cards=154`, `route_source_rows=154`, `wiki_pages=2400`, `site_pages=2400`, `index_pages=30`. Full baked `static_data.h8bin` proof was not claimed because no Unity/DataMonolith bake was run. TerminalOS expansion remains deliberately avoided; forty-seven non-terminal manual rows are routed through `NarrativeDiscovery` placement backlog.

Control Pass 91: locked domain route table, worker evidence protocol, pressure recipe grammar and dossier presentation into AppliedLore.

What was wrong:
Human expansion still had no stable public route names or lower Deep Reach office surfaces. Colony evidence still lacked a seed-safe worker-name/job/locker/localization protocol. Resource and escape crafting had categories but not pressure-band grammar or sample quality classes. Replay/dossier persistence was knowledge-not-power but needed UI/save/site presentation rules.

What was done:
Added `RS033_DOMAIN_EPHEMERIS_ROUTE_TABLE`, `RS034_WORKER_NAME_JOB_EVIDENCE_TABLE`, `RS035_RESOURCE_RECIPE_PRESSURE_RULES`, and `RS036_DOSSIER_SAVE_PRESENTATION_RULES` with packets `P161`-`P180`. Locked domain route bands, population/authority pressure scale, `Sol-Centauri Charter Spine`, `Barnard Breaker Run`, `Luyten Packet Ladder`, `Tau Public Ledger Lane`, `Ran Long Claim`, `Contract Continuity Desk`, `Packet Notary Interface`, `Quarantine Review Gate`, `Asset Silence Board`, `Return Action Queue`, worker-name/job/locker/native-localization protocol, shift crew story seeds, recipe pressure bands, pressure failure classes, blue debt sample quality classes, vent forge process, escape component route grammar, dossier selection UI rules, risk cards, ending record shape, save-profile knowledge flags and website/wiki spoiler tiers. Propagated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized wiki pages, external-site pages, indexes, source CSV rows and `H8AppliedLoreHashes.cs`. Synced `Canon_Locks.md`, `Open_Questions.md`, `Lore_Bible.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README.

Cinematic Cheats used:
Route scale is delivered through static route bands, timestamps, stamps and packet cards instead of live orbital simulation. Colony humanity is object evidence and seed-safe text, not bespoke family cinematics. Resource pressure uses staged containment classes and vent forge presentation, not material particle simulation. Replay knowledge is dossier text/cards and save flags, not runtime power mutation.

Exact microseconds saved:
0 us/frame in this pass. No runtime parser, live translation, scene search, Unity compile or `dotnet build`. The only generated C# touched is `H8AppliedLoreHashes.cs` through the existing importer.

Proof:
`AppliedLoreImporter.py --root .` reports `applied_lore_packets=180 localized_rows=2700`. `AppliedLorePageExporter.py --root .` wrote 600 pages. `AppliedLoreRouteCardExporter.py --root .` reports `applied_lore_route_cards=174`. `AppliedLoreRuntimeAudit.py --root . --source-only` passes with `packets=180`, `locales=15`, `rows=2700`, `binding_map_rows=180`, `target_backlog_rows=180`, `manual_policy_rows=94`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=67`, `scene_terminal_os_runtime_verified_slots=27`, `graph_rows=180`, `route_cards=174`, `route_source_rows=174`, `wiki_pages=2700`, `site_pages=2700`, `index_pages=30`. Full baked `static_data.h8bin` proof was not claimed because no Unity/DataMonolith bake was run. TerminalOS expansion remains deliberately avoided; sixty-seven non-terminal manual rows are routed through `NarrativeDiscovery` placement backlog.

Control Pass 92: locked moon atlas, Deep Reach true-cause knowledge, final emotional axis and tuning contracts into AppliedLore.

What was wrong:
Aegir moon labels and hazard roles were still loose. "Who knew the true cause?" remained an actual narrative hole. The final emotional decision was still a candidate list rather than a buildable axis. Numeric balancing and localization ownership were still vulnerable to bad prose numbers and runtime translation drift.

What was done:
Added `RS037_AEGIR_MOON_PUBLIC_ATLAS`, `RS038_DEEP_REACH_TRUE_CAUSE_KNOWLEDGE`, `RS039_FINAL_DECISION_EMOTIONAL_AXIS`, and `RS040_NUMERIC_TUNING_SOURCE_RULES` with packets `P181`-`P200`. Locked moon-name mutability vs route function, HECTON-8 orbital hazard classes, Aegir moon role ledger, ephemeris table ownership, public moon-article spoiler boundaries, Deep Reach knowledge tiers, liability memo fragment chain, signoff/witness conflict, seeded suboffice personnel, false public report packet, final trilemma, Atlas severance ethics, no-clean-best-ending rule, resource/recipe/risk/inventory table ownership and native localization pass requirements. Propagated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized wiki pages, external-site pages, indexes, source CSV rows and `H8AppliedLoreHashes.cs`. Synced `Canon_Locks.md`, `Open_Questions.md`, `Lore_Bible.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README.

Cinematic Cheats used:
Orbital pressure is authored as window/hazard/table evidence, not live N-body simulation. Deep Reach guilt is stamps, memos, witness hashes and damaged rooms, not villain monologues. Final choice pressure is payload/record/object state, not a clean dialogue binary. Numeric tuning stays in static tables rather than prose pretending to be balance.

Exact microseconds saved:
0 us/frame in this pass. No runtime parser, live translation, scene search, Unity compile or `dotnet build`. The only generated C# touched is `H8AppliedLoreHashes.cs` through the existing importer.

Proof:
Initial audit caught stale packet IDs and they were corrected against real packet JSON. Final source-only chain passed: `AppliedLoreImporter.py --root .` reports `applied_lore_packets=200 localized_rows=3000`; `AppliedLoreRouteCardExporter.py --root .` reports `applied_lore_route_cards=194`; `AppliedLoreRuntimeAudit.py --root . --source-only` passes with `packets=200`, `locales=15`, `rows=3000`, `binding_map_rows=200`, `target_backlog_rows=200`, `manual_policy_rows=114`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=87`, `scene_terminal_os_runtime_verified_slots=27`, `graph_rows=200`, `route_cards=194`, `route_source_rows=194`, `wiki_pages=3000`, `site_pages=3000`, `index_pages=30`. Full baked `static_data.h8bin` proof was not claimed because no Unity/DataMonolith bake was run. TerminalOS expansion remains deliberately avoided; eighty-seven non-terminal manual rows are routed through `NarrativeDiscovery` placement backlog.

Control Pass 93: locked lower Deep Reach signatures, colony roster scale, worker prop evidence and publication/localization protocol into AppliedLore.

What was wrong:
Lower Deep Reach personnel were still optional without concrete signatures. Colony humanity had method but not exact roster scale. Worker props could still drift into decorative clutter. Website/wiki/audio/image publication gates existed as broad policy but not packetized content usable by site/wiki/game systems.

What was done:
Added `RS041_DEEP_REACH_LOWER_SIGNATURES`, `RS042_COLONY_ROSTER_AUTHORING_POOL`, `RS043_WORKER_PROP_EVIDENCE_KIT`, and `RS044_PUBLICATION_SPOILER_LOCALIZATION_PROTOCOL` with packets `P201`-`P220`. Locked procedure-level lower signatures for Contract Continuity Desk, Packet Notary Interface, Quarantine Review Gate, Asset Silence Board and Return Action Queue; locked the colony roster at 72 worker identities, split into 24 anchor names and 48 seed-role identities; added anchor name sets A/B and seed-role grammar; locked locker, triage ledger, route stamp, Marauder correction and audio fragment prop rules; locked public site tiers, PDA unlock tiers, audio transcript censorship, art release gates and native-language backlog gates. Propagated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized wiki pages, external-site pages, indexes, source CSV rows and `H8AppliedLoreHashes.cs`. Synced `Canon_Locks.md`, `Open_Questions.md`, `Lore_Bible.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README.

Cinematic Cheats used:
Lower Deep Reach guilt is stamped procedure and route evidence, not villain scenes. Colony humanity comes from props, ledgers, stamps and audio tied to physical rooms, not family melodrama. Publication richness is handled as tiered static content and image briefs; high-end presentation can overdeliver visually without changing baked packet truth.

Exact microseconds saved:
0 us/frame in this pass. No runtime parser, live translation, scene search, Unity compile or `dotnet build`. The only generated C# touched is `H8AppliedLoreHashes.cs` through the existing importer.

Proof:
Initial route-card export caught stale packet IDs and they were corrected to real `P053_MARAUDER_GRAFFITI_MASKS` and `P176_DOSSIER_SELECTION_UI_RULE`. Final source-only chain passed: `AppliedLoreImporter.py --root .` reports `applied_lore_packets=220 localized_rows=3300`; `AppliedLorePageExporter.py --root .` reports 600 new pages and 30 indexes; `AppliedLoreRouteCardExporter.py --root .` reports `applied_lore_route_cards=214`; `AppliedLoreRuntimeAudit.py --root . --source-only` passes with `packets=220`, `locales=15`, `rows=3300`, `binding_map_rows=220`, `target_backlog_rows=220`, `manual_policy_rows=134`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=107`, `scene_terminal_os_runtime_verified_slots=27`, `graph_rows=220`, `route_cards=214`, `route_source_rows=214`, `wiki_pages=3300`, `site_pages=3300`, `index_pages=30`. Full baked `static_data.h8bin` proof was not claimed because no Unity/DataMonolith bake was run. TerminalOS expansion remains deliberately avoided; one hundred seven non-terminal manual rows are routed through `NarrativeDiscovery` placement backlog.

Control Pass 94: locked native ecology, Atlas repair network and hardware evidence chain into AppliedLore.

What was wrong:
The ocean still risked reading as a mood backdrop plus monsters. Atlas using biology as repair infrastructure was not concrete enough. The early escape lock still needed named, evidence-bearing hardware instead of generic "capsule broken" prose. Source routes also needed validation because stale packet IDs and pseudo-binding candidates can silently poison later bake passes.

What was done:
Added `RS045_PHOTIC_SHELF_NATIVE_ECOLOGY`, `RS046_BRINE_CANYON_ABYSS_ECOLOGY`, `RS047_ATLAS_MAINTENANCE_BIOMECH_LAYER`, and `RS048_HARDWARE_AND_VEHICLE_EVIDENCE_STACK` with packets `P221`-`P240`. Locked photic mat ecology, glass grazer schools, lantern drift blooms, shell clamp reefs, predator shadow telegraphs, brine vane forests, density skaters, vent anchor colonies, wide filter traces, silt ambusher telegraphs, conductive biofilm cable skin, acoustic filter-organ relays, shell sealant fracture growth, sensor-tagged fauna, vent micronode nests, Black Keel tender limits, drop capsule damage parts, P-63 fabricator authority limits, pressure suit grades and sonar pinger route beacons. Propagated packet bundles, manifests, release docs, evidence graphs, route cards `RC215`-`RC234`, runtime binding maps, scene binding targets, image briefs, localized wiki pages, external-site pages, indexes, source CSV rows and `H8AppliedLoreHashes.cs`. Synced `Canon_Locks.md`, `Open_Questions.md`, `Lore_Bible.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README.

Cinematic Cheats used:
Ecology is authored as scan evidence, silhouette behavior, route telegraphs and static packet truth instead of expensive full ecosystem simulation. Atlas repair is visible through growth masks, relay organs, shell seals and tagged fauna rather than a mystical speaking ocean. Escape pressure is delivered through damaged hardware, authority limits, suit grades and beacon routes rather than a costly rescue cinematic or arbitrary quest lock.

Exact microseconds saved:
0 us/frame in this pass. No runtime parser, live translation, scene search, Unity compile, DataMonolith bake or `dotnet build`. The only generated C# touched is `H8AppliedLoreHashes.cs` through the existing importer.

Proof:
Initial verification caught and corrected phantom/stale references to `P084_VENT_FORGE_ENGINE`, `P086_AEGIR_RECLAMATION_POOL_TENDER`, `P173_BLUE_DEBT_QUALITY_CLASSES`, and `P023_FIRST_TOOL_CHAIN_SURVIVAL_GATE`. New route-card ending pressures were normalized to supported schema values, and new scene binding candidates were changed from `poi.*` pseudo-paths to real prefab candidate paths. Final source-only chain passed: `AppliedLoreImporter.py --root .` reports `applied_lore_packets=240 localized_rows=3600`; `AppliedLorePageExporter.py --root .` reports 600 new pages and 30 indexes; `AppliedLoreRouteCardExporter.py --root .` reports `applied_lore_route_cards=234`; `AppliedLoreRuntimeAudit.py --root . --source-only` passes with `packets=240`, `locales=15`, `rows=3600`, `binding_map_rows=240`, `target_backlog_rows=240`, `manual_policy_rows=154`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=127`, `scene_terminal_os_runtime_verified_slots=27`, `graph_rows=240`, `route_cards=234`, `route_source_rows=234`, `wiki_pages=3600`, `site_pages=3600`, `index_pages=30`. Full baked `static_data.h8bin` proof was not claimed because no Unity/DataMonolith bake was run. TerminalOS expansion remains deliberately avoided; one hundred twenty-seven non-terminal manual rows are routed through `NarrativeDiscovery` placement backlog.

Control Pass 95: locked contract replay surfaces, first-hour micro-script, public pillar articles and localization/audio style into AppliedLore.

What was wrong:
Replay was still at risk of being "different random run" language instead of contract-visible pressure. The first hour had strong macro beats but needed terminal/scanner/audio-ready micro-surfaces. Website/wiki output needed public pillar articles rather than internal lore summaries. Localization and audio style needed hard locks before native review, otherwise names, units, terminal register and barks would drift between languages.

What was done:
Added `RS049_CONTRACT_SEED_RISK_REWARD_SURFACES`, `RS050_FIRST_HOUR_MICRO_SCRIPT_SURFACES`, `RS051_PUBLIC_SITE_PILLAR_ARTICLES`, and `RS052_LOCALIZATION_GLOSSARY_AUDIO_STYLE` with packets `P241`-`P260`. Locked lien severity, storm window, sample custody, evidence-order depth, Deep Reach clause weight, Black Keel approach audio, capsule diagnostic readout, P-63 first repair task, sanitized accident packet body, first Atlas repair trace scene, HECTON-8/Aegir/Deep Reach/Atlas-6/blue debt public pillar articles, proper-noun translation rules, unit/number style, terminal voice register, audio bark families and RTL/CJK font risk. Propagated packet bundles, manifests, release docs, evidence graphs, route cards `RC235`-`RC254`, runtime binding maps, scene binding targets, image briefs, localized wiki pages, external-site pages, indexes, source CSV rows and `H8AppliedLoreHashes.cs`. Synced `Canon_Locks.md`, `Open_Questions.md`, `Lore_Bible.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README.

Cinematic Cheats used:
Replay uses contract cards, clause weight and dossier order rather than inherited power simulation. First-hour drama is delivered through prop diagnostics, pump tasks and sparse voice instead of heavy cinematics. Public articles share packet truth with site/wiki surfaces instead of separate marketing lore. Audio uses sparse pressure barks instead of companion chatter.

Exact microseconds saved:
0 us/frame in this pass. No runtime parser, live translation, scene search, Unity compile, DataMonolith bake or `dotnet build`. The only generated C# touched is `H8AppliedLoreHashes.cs` through the existing importer.

Proof:
Initial route-card export and audit caught stale prereq IDs and they were corrected to real packet IDs: `P021_BLACK_KEEL_CUSTODY`, `P097_RECOVERY_COMPLIANCE_OFFICE`, `P203_QUARANTINE_REVIEW_GATE_SIGNATURES`, `P091_COLLISION_FRACTURED_MOON`, `P146_DEEP_REACH_PUBLIC_COMBINE`, `P147_AEGIR_CONTINUITY_HOLDINGS`, `P171_RECIPE_TIER_PRESSURE_BANDS`, `P216_PUBLIC_SITE_ARTICLE_TIER_RULES`, `P217_IN_GAME_WIKI_UNLOCK_TIER_RULES`, `P218_AUDIO_TRANSCRIPT_CENSOR_RULES`, `P190_FALSE_PUBLIC_REPORT_PACKET`, and `P036_RETURN_VECTOR_WINDOW`. Final source-only chain passed: `AppliedLoreImporter.py --root .` reports `applied_lore_packets=260 localized_rows=3900`; `AppliedLorePageExporter.py --root .` reports 600 new pages and 30 indexes; `AppliedLoreRouteCardExporter.py --root .` reports `applied_lore_route_cards=254`; `AppliedLoreRuntimeAudit.py --root . --source-only` passes with `packets=260`, `locales=15`, `rows=3900`, `binding_map_rows=260`, `target_backlog_rows=260`, `manual_policy_rows=174`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=147`, `scene_terminal_os_runtime_verified_slots=27`, `graph_rows=260`, `route_cards=254`, `route_source_rows=254`, `wiki_pages=3900`, `site_pages=3900`, `index_pages=30`. Full baked `static_data.h8bin` proof was not claimed because no Unity/DataMonolith bake was run. TerminalOS expansion remains deliberately avoided; one hundred forty-seven non-terminal manual rows are routed through `NarrativeDiscovery` placement backlog.
## NARRATIVE_RESEARCH Control Pass 96 - RS053-RS056 AppliedLore production bridge

What was wrong:

- Numeric economy questions were still open as prose-level "think about this" instead of source surfaces that table/gameplay owners can consume.
- Dossier/contract UI copy was not fully packetized for PDA, scanner, terminal, wiki and site reuse.
- Ending outcomes had thematic locks but lacked concrete record payload surfaces for material, partial, public, severance and preserve/quarantine outcomes.
- Localization review had glossary/style locks but not explicit production review gates for RU, CJK, RTL, European expansion and subtitle/audio timing.
- First route export caught stale prereq names from memory: `P028_RUMOR_FAMILY_UNLOCKS`, `P037_COWARD_EXIT`, `P132_PARTIAL_EXIT_RETURN`, and `P134_PUBLIC_LEDGER_RELEASE`.

What was done:

- Added `RS053_NUMERIC_AUTHORING_BRIDGE_SURFACES` with packets `P261`-`P265`.
- Added `RS054_DOSSIER_CONTRACT_UI_COPY_DECK` with packets `P266`-`P270`.
- Added `RS055_ENDING_PAYLOAD_RECORD_SURFACES` with packets `P271`-`P275`.
- Added `RS056_NATIVE_LOCALIZATION_REVIEW_PACK` with packets `P276`-`P280`.
- Generated packet JSON, release manifests/docs, evidence graphs, route cards, runtime binding maps, scene binding targets and image briefs.
- Updated shared manual binding policy and scene placement plan to include new manual rows without expanding TerminalOS scene slots.
- Ran `AppliedLoreImporter.py --root .`: `applied_lore_packets=280 localized_rows=4200`.
- Ran `AppliedLorePageExporter.py --root .`: `skipped_existing=8400 index_pages_written=30`.
- Ran `AppliedLoreRouteCardExporter.py --root .`: `applied_lore_route_cards=274`.
- Ran `AppliedLoreRuntimeAudit.py --root . --source-only`: source audit OK with `packets=280`, `locales=15`, `rows=4200`, `graph_rows=280`, `route_cards=274`, `route_source_rows=274`, `wiki_pages=4200`, `site_pages=4200`, `index_pages=30`, `binding_map_rows=280`, `target_backlog_rows=280`, `manual_policy_rows=194`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=167`, `scene_terminal_os_runtime_verified_slots=27`.
- Synced `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, `AppliedContent/README.md`, and `binding_maps/README.md`.

Cinematic cheats used:

- No physical systems were added. All work is source content, route cards, copy surfaces, image briefs and binding targets.
- UI richness is deferred to presentation owners. Low-end devices can show static baked text/cards; high-end devices can spend budget on richer PDA boards, ending dossiers, route-warning animation and audio/subtitle treatment around the same packet hashes.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- No runtime markdown parser: avoids hot-path parsing and allocation.
- No live translation: avoids runtime language transforms and font/layout churn.
- No scene search: all new manual rows stay as authored placement backlog.
- No `dotnet build`, no Unity compile, no DataMonolith bake in this pass.

## NARRATIVE_RESEARCH Control Pass 97 - RS057-RS060 AppliedLore publication/artifact/ecology/final-route layer

What was wrong:

- Public/site lore still risked becoming detached marketing prose instead of packet-owned release text that can feed website, wiki and in-game surfaces from the same source.
- In-game notes and audio needed concrete artifact payloads instead of generic "add more logs" guidance.
- Ecology needed specimen cards that teach route logic, scan value and Atlas repair-network evidence instead of decorative bestiary text.
- Final descent needed usable warning, gate, factory-temple, payload-authority and dossier fragments without turning Atlas into a boss or offering a clean victory.
- First route export caught stale prereq naming: `P083_BRINE_CANYON_LADDER`.

What was done:

- Added `RS057_PUBLIC_SITE_READY_ARTICLE_SECTIONS` with packets `P281`-`P285`.
- Added `RS058_IN_GAME_ARTIFACT_AUDIO_SURFACES` with packets `P286`-`P290`.
- Added `RS059_ECOLOGY_CODEX_SPECIMEN_CARDS` with packets `P291`-`P295`.
- Added `RS060_FINAL_DESCENT_ROUTE_FRAGMENTS` with packets `P296`-`P300`.
- Fixed the stale prereq to the existing `P083_BRINE_CANYON_ROUTE_LADDER` and confirmed `P100_FINAL_CHOICE_PAYLOAD` exists.
- Generated packet JSON, release manifests/docs, evidence graphs, route cards, runtime binding maps, scene binding targets and image briefs.
- Updated shared manual binding policy and scene placement plan. New non-terminal rows stay in `NarrativeDiscovery` backlog; TerminalOS scene slots were not expanded during this parallel Unity period.
- Ran `AppliedLoreImporter.py --root .`: `applied_lore_packets=300 localized_rows=4500`.
- Ran `AppliedLorePageExporter.py --root .`: `applied_lore_pages_written=600 skipped_existing=8400 index_pages_written=30`.
- Ran `AppliedLoreRouteCardExporter.py --root .`: `applied_lore_route_cards=294`.
- Ran `AppliedLoreRuntimeAudit.py --root . --source-only`: source audit OK with `packets=300`, `locales=15`, `rows=4500`, `graph_rows=300`, `route_cards=294`, `route_source_rows=294`, `wiki_pages=4500`, `site_pages=4500`, `index_pages=30`, `binding_map_rows=300`, `target_backlog_rows=300`, `manual_policy_rows=214`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=187`, `scene_terminal_os_runtime_verified_slots=27`.
- Synced `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, `AppliedContent/README.md`, and `binding_maps/README.md`.

Cinematic cheats used:

- Public articles reuse packet truth and spoiler gates instead of separate marketing lore.
- Artifact/audio payloads are diegetic text, captions and prop-ready surfaces; no companion narrator or expensive scripted exposition is required.
- Ecology teaches through scan cards, silhouettes, resource hints and route hazards; no full ecosystem simulation was added.
- Final descent pressure is conveyed through terminal/scanner warnings, pressure gates and dossier copy; no boss fight, portal, or clean ending branch was invented.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- No runtime markdown parser, no live translation, no scene search, no Unity scene edits.
- No `dotnet build`, no Unity compile, no DataMonolith bake and no `static_data.h8bin` claim in this pass.
- Low tier can consume static baked pages/cards/audio captions. Middle/High/Ultra can spend presentation budget on richer site cards, scan UI, final-threshold VFX, localized subtitle treatment and dossier animation around the same packet IDs.

## NARRATIVE_RESEARCH Control Pass 98 - RS061-RS064 AppliedLore handoff/UI/publication/placement layer

What was wrong:

- Exact numeric values were still unresolved, but writing numbers into lore would violate table ownership.
- PDA/scanner/terminal/dossier UI had copy surfaces, but future implementation still needed proof-shaped packet cards.
- Public pages could still be assembled as generic marketing despite packet-owned article text.
- The `NarrativeDiscovery` backlog was growing and needed priority language for the next Unity placement pass.

What was done:

- Added `RS061_TABLE_VALUE_HANDOFF_CONTRACTS` with packets `P301`-`P305`.
- Added `RS062_RUNTIME_UI_PROOF_BACKLOG` with packets `P306`-`P310`.
- Added `RS063_PUBLICATION_COMPOSITION_PROOF_PACK` with packets `P311`-`P315`.
- Added `RS064_UNITY_PLACEMENT_PRIORITY_BACKLOG` with packets `P316`-`P320`.
- Generated packet JSON, release manifests/docs, evidence graphs, route cards, runtime binding maps, scene binding targets and image briefs.
- Updated shared manual binding policy and scene placement plan. New non-terminal rows stay in `NarrativeDiscovery` backlog; TerminalOS scene slots were not expanded during this parallel Unity period.
- Ran `AppliedLoreImporter.py --root .`: `applied_lore_packets=320 localized_rows=4800`.
- Ran `AppliedLoreRouteCardExporter.py --root .`: `applied_lore_route_cards=314`.
- Ran `AppliedLorePageExporter.py --root .`: `applied_lore_pages_written=600 skipped_existing=9000 index_pages_written=30`.
- Ran `AppliedLoreRuntimeAudit.py --root . --source-only`: source audit OK with `packets=320`, `locales=15`, `rows=4800`, `graph_rows=320`, `route_cards=314`, `route_source_rows=314`, `wiki_pages=4800`, `site_pages=4800`, `index_pages=30`, `binding_map_rows=320`, `target_backlog_rows=320`, `manual_policy_rows=234`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=207`, `scene_terminal_os_runtime_verified_slots=27`.
- Synced `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, `AppliedContent/README.md`, and `binding_maps/README.md`.

Cinematic cheats used:

- Table values remain data handoff rows, not simulated economy logic.
- UI proof is represented as baked packet/string-pool contract cards, not runtime UI code.
- Public composition is constrained through static copy/image-brief gates, not expensive marketing-page production inside this pass.
- Placement priority is authored as backlog triage, not scene YAML mutation.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- No runtime markdown parser, no live translation, no scene search, no Unity scene edits.
- No `dotnet build`, no Unity compile, no DataMonolith bake and no `static_data.h8bin` claim in this pass.
- Low tier can consume static baked rows/cards/pages. Middle/High/Ultra can spend budget on richer UI presentation, scan overlays, publication cards and denser placed props after the controlled Unity/editor passes.

## NARRATIVE_RESEARCH Control Pass 99 - RS065-RS068 AppliedLore carrier/comms/repair/false-exit layer

What was wrong:

- Carrier ownership still risked being simplified into "the player's ship" instead of compromised claim-pool infrastructure.
- Deep Reach present-time pressure needed message routes, not constant radio or villain monologue.
- Atlas repair network needed mechanism-level packets, not only broad biomechanical mood.
- False/partial exits needed after-action records so they are real outcomes, not fake fail screens.

What was done:

- Added `RS065_CARRIER_CONTRACT_OWNERSHIP_SURFACES` with packets `P321`-`P325`.
- Added `RS066_DEEP_REACH_PRESENT_COMMS_CHAIN` with packets `P326`-`P330`.
- Added `RS067_ATLAS_REPAIR_NETWORK_MECHANICS` with packets `P331`-`P335`.
- Added `RS068_FALSE_EXIT_AFTER_ACTION_RECORDS` with packets `P336`-`P340`.
- Fixed stale prereqs to real packet IDs: `P021_BLACK_KEEL_CUSTODY`, `P016_AEGIR_HOST_STAR`, `P201_CONTRACT_CONTINUITY_DESK_SIGNATURES`, `P231_CONDUCTIVE_BIOFILM_CABLE_SKIN`, `P232_ACOUSTIC_FILTER_ORGAN_RELAY`, `P233_SHELL_SEALANT_FRACTURE_GROWTH`, `P234_SENSOR_TAGGED_FAUNA`, and `P235_VENT_MICRONODE_NESTS`.
- Generated packet JSON, release manifests/docs, evidence graphs, route cards, runtime binding maps, scene binding targets and image briefs.
- Updated shared manual binding policy and scene placement plan. New non-terminal rows stay in `NarrativeDiscovery` backlog; TerminalOS scene slots were not expanded during this parallel Unity period.
- Ran `AppliedLoreImporter.py --root .`: `applied_lore_packets=340 localized_rows=5100`.
- Ran `AppliedLorePageExporter.py --root .`: first pass wrote `applied_lore_pages_written=600 skipped_existing=9600 index_pages_written=30`; second pass after route corrections wrote `applied_lore_pages_written=0 skipped_existing=10200 index_pages_written=30`.
- Ran `AppliedLoreRouteCardExporter.py --root .`: `applied_lore_route_cards=334`.
- Ran `AppliedLoreRuntimeAudit.py --root . --source-only`: source audit OK with `packets=340`, `locales=15`, `rows=5100`, `graph_rows=340`, `route_cards=334`, `route_source_rows=334`, `wiki_pages=5100`, `site_pages=5100`, `index_pages=30`, `binding_map_rows=340`, `target_backlog_rows=340`, `manual_policy_rows=254`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=227`, `scene_terminal_os_runtime_verified_slots=27`.
- Synced `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, `AppliedContent/README.md`, and `binding_maps/README.md`.

Cinematic cheats used:

- Carrier rescue pressure is expressed through static window/status/contract cards; no ship simulation or live rescue dispatcher was added.
- Deep Reach comms use sparse legal packets, blackout windows and source-split messages; no always-on exposition channel.
- Atlas repair network uses visible mechanisms: biofilm, acoustic organs, shell sealant, tagged fauna and micronodes; no talking-ocean logic.
- False exits use dossier after-action records before bespoke cinematic branches.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- No runtime markdown parser, no live translation, no scene search, no Unity scene edits.
- No `dotnet build`, no Unity compile, no DataMonolith bake and no `static_data.h8bin` claim in this pass.
- Low tier can consume static baked rows/cards/pages. Middle/High/Ultra can spend budget on carrier UI animation, comm waveform treatment, repair-network VFX and richer ending dossier boards after controlled runtime/UI passes.

## NARRATIVE_RESEARCH Control Pass 100 - RS069-RS072 AppliedLore ships/Aegir/geology/colony layer

What was wrong:

- Ship and transit lore still needed usable encyclopedia/source packets, not vague "future spaceship" framing.
- Aegir needed a believable multi-moon system role model without changing the current playable scope away from HECTON-8.
- HECTON-8 geology/resource language needed fieldguide packets that can drive scanner/wiki/site copy and future table handoff.
- Colony humanity needed stronger daily-life evidence while preserving the locked ex-Deep-Reach/current-Marauder protagonist and rejecting family-revenge hooks.

What was done:

- Added `RS069_SHIP_TECH_TRANSIT_ENCYCLOPEDIA` with packets `P341`-`P345`.
- Added `RS070_AEGIR_MOON_SYSTEM_ATLAS` with packets `P346`-`P350`.
- Added `RS071_HECTON8_GEOLOGY_RESOURCE_FIELDGUIDE` with packets `P351`-`P355`.
- Added `RS072_COLONY_DAILY_LIFE_EVIDENCE_ATLAS` with packets `P356`-`P360`.
- Generated packet JSON, release manifests/docs, evidence graphs, route cards, runtime binding maps, scene binding targets and image briefs.
- Updated shared manual binding policy and scene placement plan. New non-terminal rows stay in `NarrativeDiscovery` backlog; TerminalOS scene slots were not expanded during this parallel Unity period.
- Ran `AppliedLoreImporter.py --root .`: `applied_lore_packets=360 localized_rows=5400`.
- Ran `AppliedLorePageExporter.py --root .`: `applied_lore_pages_written=600 skipped_existing=10200 index_pages_written=30`.
- Ran `AppliedLoreRouteCardExporter.py --root .`: `applied_lore_route_cards=354`.
- Ran `AppliedLoreRuntimeAudit.py --root . --source-only`: source audit OK with `packets=360`, `locales=15`, `rows=5400`, `graph_rows=360`, `route_cards=354`, `route_source_rows=354`, `wiki_pages=5400`, `site_pages=5400`, `index_pages=30`, `binding_map_rows=360`, `target_backlog_rows=360`, `manual_policy_rows=274`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=247`, `scene_terminal_os_runtime_verified_slots=27`.
- Synced `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, `AppliedContent/README.md`, and `binding_maps/README.md`.

Cinematic cheats used:

- Interstellar travel is explained through static route records, carrier specs and encyclopedia cards; no travel simulation was added.
- Aegir moon roles are source atlas labels and hazard grammar; no orbital mechanics runtime was claimed.
- Geology uses scanner/fieldguide records and POI labels; no expensive physical geology simulation was added.
- Colony humanity is delivered through props, ledgers, tool boards and audio captions; no companion or family melodrama branch was invented.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- No runtime markdown parser, no live translation, no scene search, no Unity scene edits.
- No `dotnet build`, no Unity compile, no DataMonolith bake and no `static_data.h8bin` claim in this pass.
- Low tier can consume static baked rows/cards/pages. Middle/High/Ultra can spend budget on orbital atlas animation, geology scan overlays, prop art, audio fragments and richer publication pages after controlled runtime/UI/publication passes.

## NARRATIVE_RESEARCH Control Pass 101 - RS073-RS076 AppliedLore escape/player/liability/final-payload layer

What was wrong:

- Escape/ascent was locked as grammar but still lacked enough concrete component cards for in-game/wiki/site use.
- The player identity needed usable dossier evidence, not only a one-line "ex-Deep-Reach Marauder" statement.
- Deep Reach guilt needed to preserve the user's correction: the flood was real physics, while corporate choices made it fatal and then sanitized it.
- Final choices needed receiver/custody protocols instead of abstract ending names.

What was done:

- Added `RS073_ESCAPE_ASCENT_ENGINEERING_COMPONENTS` with packets `P361`-`P365`.
- Added `RS074_PLAYER_EX_DEEP_REACH_PROFESSIONAL_DOSSIER` with packets `P366`-`P370`.
- Added `RS075_DEEP_REACH_LIE_PHYSICAL_PROOF_CHAIN` with packets `P371`-`P375`.
- Added `RS076_ATLAS_FINAL_PAYLOAD_RECEIVER_PROTOCOLS` with packets `P376`-`P380`.
- Generated packet JSON, release manifests/docs, evidence graphs, route cards, runtime binding maps, scene binding targets and image briefs.
- Updated shared manual binding policy and scene placement plan. New non-terminal rows stay in `NarrativeDiscovery` backlog; TerminalOS scene slots were not expanded during this parallel Unity period.
- Fixed stale prereq `P070_RETURN_VECTOR_WINDOW` to real `P036_RETURN_VECTOR_WINDOW` during route export verification.
- Ran `AppliedLoreImporter.py --root .`: `applied_lore_packets=380 localized_rows=5700`.
- Ran `AppliedLorePageExporter.py --root .`: `applied_lore_pages_written=600 skipped_existing=10800 index_pages_written=30`.
- Ran `AppliedLoreRouteCardExporter.py --root .`: `applied_lore_route_cards=374`.
- Ran `AppliedLoreRuntimeAudit.py --root . --source-only`: source audit OK with `packets=380`, `locales=15`, `rows=5700`, `graph_rows=380`, `route_cards=374`, `route_source_rows=374`, `wiki_pages=5700`, `site_pages=5700`, `index_pages=30`, `binding_map_rows=380`, `target_backlog_rows=380`, `manual_policy_rows=294`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=267`, `scene_terminal_os_runtime_verified_slots=27`.
- Synced `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, `AppliedContent/README.md`, and `binding_maps/README.md`.

Cinematic cheats used:

- Escape remains source cards and component evidence; no ascent simulation, rescue cutscene or live orbital mechanic was added.
- Player motive is expressed through dossier/access/procedure evidence; no family melodrama branch was invented.
- Deep Reach liability is delivered through sensor margins, clocks, audit branches and ledgers; no villain monologue needed.
- Final payload routes are receiver records first; bespoke ending cinematics/audio remain separate runtime/content work.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- No runtime markdown parser, no live translation, no scene search, no Unity scene edits.
- No `dotnet build`, no Unity compile, no DataMonolith bake and no `static_data.h8bin` claim in this pass.
- Low tier can consume static baked rows/cards/pages. Middle/High/Ultra can spend budget on final receiver UI, component meshes, access-denial overlays, proof-chain reconstructions, audio captions and spoiler-gated public pages after controlled runtime/UI/publication passes.

## NARRATIVE_RESEARCH Control Pass 102 - RS077-RS080 AppliedLore campaign/POI/replay/publication layer

What was wrong:

- The lore had many locked facts but still lacked a usable long-campaign act spine for placement and future mission passes.
- Major POIs needed object/evidence kits instead of atmospheric labels.
- Replayability needed concrete contract seed families without inherited power progression.
- Public/wiki copy needed spoiler-safe modules that do not leak final payload outcomes or make unsupported implementation claims.

What was done:

- Added `RS077_LONG_CAMPAIGN_ACT_SPINE` with packets `P381`-`P385`.
- Added `RS078_MAJOR_POI_EVIDENCE_KITS` with packets `P386`-`P390`.
- Added `RS079_REPLAY_CONTRACT_SEED_FAMILIES` with packets `P391`-`P395`.
- Added `RS080_PUBLIC_WIKI_ARTICLE_MODULES` with packets `P396`-`P400`.
- Generated packet JSON, release manifests/docs, evidence graphs, route cards, runtime binding maps, scene binding targets and image briefs.
- Updated shared manual binding policy and scene placement plan. New non-terminal rows stay in `NarrativeDiscovery` backlog; TerminalOS scene slots were not expanded during this parallel Unity period.
- Fixed stale prereqs to real packet IDs: `P336_MATERIAL_EXIT_RECEIPT_AUDIT` and `P342_BEAM_SAIL_AND_PELLET_LANE`.
- Ran `AppliedLoreImporter.py --root .`: `applied_lore_packets=400 localized_rows=6000`.
- Ran `AppliedLorePageExporter.py --root .`: first pass wrote `applied_lore_pages_written=600 skipped_existing=11400 index_pages_written=30`; final pass after route corrections wrote `applied_lore_pages_written=0 skipped_existing=12000 index_pages_written=30`.
- Ran `AppliedLoreRouteCardExporter.py --root .`: `applied_lore_route_cards=394`.
- Ran `AppliedLoreRuntimeAudit.py --root . --source-only`: source audit OK with `packets=400`, `locales=15`, `rows=6000`, `graph_rows=400`, `route_cards=394`, `route_source_rows=394`, `wiki_pages=6000`, `site_pages=6000`, `index_pages=30`, `binding_map_rows=400`, `target_backlog_rows=400`, `manual_policy_rows=314`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=287`, `scene_terminal_os_runtime_verified_slots=27`.
- Synced `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, `AppliedContent/README.md`, and `binding_maps/README.md`.

Cinematic cheats used:

- Campaign pacing is expressed through static act gates and route cards; no mission runtime was invented.
- Major POIs are physical kit definitions first; expensive bespoke set dressing remains optional per device tier and art pass.
- Replay pressure uses contract cards and dossier context, not new simulation systems or gear inheritance.
- Public modules use spoiler gates and static article blocks before bespoke website assembly.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- No runtime markdown parser, no live translation, no scene search, no Unity scene edits.
- No `dotnet build`, no Unity compile, no DataMonolith bake and no `static_data.h8bin` claim in this pass.
- Low tier can consume static baked rows/cards/pages. Middle/High/Ultra can spend budget on act transition UI, POI prop density, contract-board animation, public page art and spoiler-gate presentation after controlled runtime/UI/publication passes.

## NARRATIVE_RESEARCH Control Pass 103 - RS081-RS084 AppliedLore worker/memo/fauna/navigation layer

What was wrong:

- Colony humanity needed more named, object-ready workers without violating the no-family protagonist rule.
- Deep Reach liability still needed concrete memo artifacts that preserve real flood physics while proving corporate choices.
- Fauna needed encounter grammar for route/scanner/replay use, not a boss list.
- Public/wiki content needed navigation hubs so it can ship as usable wiki/site/codex structure instead of an unordered packet dump.

What was done:

- Added `RS081_COLONY_ANCHOR_WORKER_DOSSIERS` with packets `P401`-`P405`.
- Added `RS082_DEEP_REACH_ARTIFACT_MEMO_PACK` with packets `P406`-`P410`.
- Added `RS083_FAUNA_ENCOUNTER_GRAMMAR` with packets `P411`-`P415`.
- Added `RS084_SITE_WIKI_NAVIGATION_CLUSTERS` with packets `P416`-`P420`.
- Generated packet JSON, release manifests/docs, evidence graphs, route cards `RC435`-`RC454`, runtime binding maps, scene binding targets and image briefs.
- Updated shared manual binding policy and scene placement plan. New rows stay in `NarrativeDiscovery` backlog; TerminalOS scene slots were not expanded during this parallel Unity period.
- Fixed stale prereqs to real packet IDs: `P352_BRINE_CANYON_DENSITY_LADDER_GUIDE`, `P353_VENT_FORGE_FIELD_PROCESS_GUIDE`, `P355_PRESSURE_GLASS_AND_SEALANT_GUIDE`, and `P351_DROWNED_CRUST_STRATA_GUIDE`.
- Fixed route-card audit category drift by changing unsupported `ending_pressure=spoiler` to valid `truth`.
- Ran `AppliedLoreImporter.py --root .`: `applied_lore_packets=420 localized_rows=6300`.
- Ran `AppliedLorePageExporter.py --root .`: first pass wrote `applied_lore_pages_written=600 skipped_existing=12000 index_pages_written=30`; final pass after route corrections wrote `applied_lore_pages_written=0 skipped_existing=12600 index_pages_written=30`.
- Ran `AppliedLoreRouteCardExporter.py --root .`: `applied_lore_route_cards=414`.
- Ran `AppliedLoreRuntimeAudit.py --root . --source-only`: source audit OK with `packets=420`, `locales=15`, `rows=6300`, `graph_rows=420`, `route_cards=414`, `route_source_rows=414`, `wiki_pages=6300`, `site_pages=6300`, `index_pages=30`, `binding_map_rows=420`, `target_backlog_rows=420`, `manual_policy_rows=334`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=307`, `scene_terminal_os_runtime_verified_slots=27`.
- Synced `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, `AppliedContent/README.md`, and `binding_maps/README.md`.

Cinematic cheats used:

- Workers are delivered through prop-readable dossiers and audio/wiki copy, not bespoke family cutscenes.
- Deep Reach guilt is delivered through memos, stamps, ledgers and signal packets; no expensive villain performance or moral monologue required.
- Fauna is encounter grammar and scanner ambiguity first; heavy AI/spawn/runtime behavior remains a separate implementation pass.
- Site/wiki navigation uses static hub packets and spoiler gates before any custom website UI.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- No runtime markdown parser, no live translation, no scene search, no Unity scene edits.
- No `dotnet build`, no Unity compile, no DataMonolith bake and no `static_data.h8bin` claim in this pass.
- Low tier can consume static baked rows/cards/pages. Middle/High/Ultra can spend budget on worker prop density, memo-room overlays, fauna telegraphs, sonar distortion, animated wiki hubs and spoiler-gated ending presentation after controlled runtime/UI/publication passes.

## NARRATIVE_RESEARCH Control Pass 104 - RS085-RS088 AppliedLore ephemeris/resource/presentation/transcript layer

What was wrong:

- Public hard-sci-fi copy needed route/window bands without pretending exact celestial tables were already complete.
- Resource economy needed artifact-level evidence for custody, pressure history, contamination and payout mass.
- PDA/scanner/terminal/dossier lore needed presentation rules before runtime UI work.
- Audio/transcript material needed performance/article seeds without claiming routed audio implementation.

What was done:

- Added `RS085_CELESTIAL_EPHEMERIS_PUBLIC_BANDS` with packets `P421`-`P425`.
- Added `RS086_RESOURCE_ECONOMY_ARTIFACTS` with packets `P426`-`P430`.
- Added `RS087_PDA_CODEX_PRESENTATION_RULES` with packets `P431`-`P435`.
- Added `RS088_AUDIO_TRANSCRIPT_ARTICLE_SEEDS` with packets `P436`-`P440`.
- Generated packet JSON, release manifests/docs, evidence graphs, route cards `RC455`-`RC474`, runtime binding maps, scene binding targets and image briefs.
- Updated shared manual binding policy and scene placement plan. New rows stay in `NarrativeDiscovery` backlog; TerminalOS scene slots were not expanded during this parallel Unity period.
- Reran importer, route-card exporter, page exporter and source-only audit sequentially after parallel exporter/audit races against stale generated files.
- Ran `AppliedLoreImporter.py --root .`: `applied_lore_packets=440 localized_rows=6600`.
- Ran `AppliedLorePageExporter.py --root .`: first pass wrote `applied_lore_pages_written=600 skipped_existing=12600 index_pages_written=30`; final pass wrote `applied_lore_pages_written=0 skipped_existing=13200 index_pages_written=30`.
- Ran `AppliedLoreRouteCardExporter.py --root .`: `applied_lore_route_cards=434`.
- Ran `AppliedLoreRuntimeAudit.py --root . --source-only`: source audit OK with `packets=440`, `locales=15`, `rows=6600`, `graph_rows=440`, `route_cards=434`, `route_source_rows=434`, `wiki_pages=6600`, `site_pages=6600`, `index_pages=30`, `binding_map_rows=440`, `target_backlog_rows=440`, `manual_policy_rows=354`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=327`, `scene_terminal_os_runtime_verified_slots=27`.
- Synced `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, `AppliedContent/README.md`, and `binding_maps/README.md`.

Cinematic cheats used:

- Ephemeris uses route/window bands and table handoff, not runtime N-body or invented constants.
- Resource economy uses receipts, certificates and ledgers before any expensive economy UI.
- Presentation rules are source strings and proof cards; runtime UI can stay baked string-pool/DTO driven.
- Audio seeds can ship as transcript/article surfaces on low tier and become processed voice only when audio routing exists.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- No runtime markdown parser, no live translation, no scene search, no Unity scene edits.
- No `dotnet build`, no Unity compile, no DataMonolith bake and no `static_data.h8bin` claim in this pass.
- Low tier can consume static baked rows/cards/pages. Middle/High/Ultra can spend budget on orbital-map UI, receipt overlays, PDA/dossier transitions, subtitle treatment and audio playback after controlled runtime/UI/audio/publication passes.

## NARRATIVE_RESEARCH Control Pass 105 - RS089-RS092 AppliedLore table/placement/localization/longform layer

What was wrong:

- Numeric gameplay needs existed as handoff contracts but still lacked value-band draft rows that future tables can implement without reinterpreting lore.
- The `NarrativeDiscovery` backlog had hundreds of rows but needed stronger scene placement priorities for first-hour, mid-depth, ecology, final descent and terminal-slot promotion.
- Multilingual coverage risked being mistaken for release localization; native/RTL/CJK/subtitle proof needed explicit source gates.
- Public/site/wiki content needed longform article briefs that can guide real publication without unsupported runtime, release or spoiler claims.

What was done:

- Added `RS089_NUMERIC_GAMEPLAY_TABLE_VALUE_DRAFTS` with packets `P441`-`P445`.
- Added `RS090_UNITY_PLACEMENT_SCENE_BRIEFS` with packets `P446`-`P450`.
- Added `RS091_NATIVE_LOCALIZATION_AND_ACCESSIBILITY_QA_BRIEFS` with packets `P451`-`P455`.
- Added `RS092_PUBLIC_SITE_LONGFORM_ARTICLE_BRIEFS` with packets `P456`-`P460`.
- Generated packet JSON, release manifests/docs, evidence graphs, route cards `RC475`-`RC494`, runtime binding maps, scene binding targets and image briefs.
- Updated shared manual binding policy and scene placement plan. New rows stay in `NarrativeDiscovery` backlog; TerminalOS scene slots were not expanded during this parallel Unity period.
- Fixed stale prereqs to real packet IDs: `P385_ATLAS_BASIN_PAYLOAD_ACT`, `P388_BRINE_CANYON_PUMP_CATHEDRAL_POI_KIT`, `P389_EVACUATION_QUEUE_TERMINAL_POI_KIT`, `P390_ATLAS_SERVICE_BASIN_POI_KIT`, and `P276_RU_NATIVE_REVIEW_LOCK`.
- Ran `AppliedLoreImporter.py --root .`: `applied_lore_packets=460 localized_rows=6900`.
- Ran `AppliedLoreRouteCardExporter.py --root .`: `applied_lore_route_cards=454`.
- Ran `AppliedLorePageExporter.py --root .`: `applied_lore_pages_written=600 skipped_existing=13200 index_pages_written=30`.
- Ran `AppliedLoreRuntimeAudit.py --root . --source-only`: source audit OK with `packets=460`, `locales=15`, `rows=6900`, `graph_rows=460`, `route_cards=454`, `route_source_rows=454`, `wiki_pages=6900`, `site_pages=6900`, `index_pages=30`, `binding_map_rows=460`, `target_backlog_rows=460`, `manual_policy_rows=374`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=347`, `scene_terminal_os_runtime_verified_slots=27`.
- Synced `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, `AppliedContent/README.md`, and `binding_maps/README.md`.

Cinematic cheats used:

- Numeric design uses value bands and table handoff contracts before exact numbers, avoiding spreadsheet churn in lore prose.
- Unity placement uses brief packets and sparse proof anchors before expensive scene density.
- Localization proof is explicit QA source, so low tier can stay baked-string simple while higher tiers can spend on typography/subtitle polish after proof.
- Longform articles use static packet-backed spines and spoiler gates before custom web UI or art production.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- No runtime markdown parser, no live translation, no scene search, no Unity scene edits.
- No `dotnet build`, no Unity compile, no DataMonolith bake and no `static_data.h8bin` claim in this pass.
- Low tier can consume static baked rows/cards/pages. Middle/High/Ultra can spend budget on table UI, scene prop density, localized typography, subtitles, orbital maps and spoiler-gated article presentation after controlled table/runtime/UI/publication passes.

## NARRATIVE_RESEARCH Control Pass 106 - Apex verifier hardening

What was wrong:

- The existing Narrative Apex verifier did not include Unity `Update` and `LateUpdate` roots, so future hot-path drift could hide outside `Tick`/`LateFrameTick`.
- Hot string concatenation and `WaitForCompletion` were not explicitly caught.
- Project hygiene proof for orphan `.meta` / missing `.meta` on `.cs`, `.shader`, `.compute` lived outside the C# Apex gate.
- Runtime struct layout proof was present in other systems, but this domain's Apex report did not expose local `StructLayout` / 8-byte ABI counters.

What was done:

- Extended `Assets/_Project/Scripts/Editor/Narrative/H8NarrativeApexVerifier.cs` only; no new scanner class.
- Added `Update` and `LateUpdate` to `IsApexRoot`.
- Added `hot_string_concat` detection for string-literal `+` inside hot roots.
- Expanded hot managed allocation tokens with additional LINQ calls.
- Added `WaitForCompletion` to the synchronous completion gate.
- Added runtime struct layout scan: literal `Size` must be 8-byte aligned; `Pack=1` fails; `UnsafeUtility.SizeOf<` references are counted.
- Added project `.meta` integrity scan: orphan `.meta` files and missing `.meta` for source/shader files fail the Apex pass.

Cinematic cheats used:

- No runtime safety component was added. The prevention remains an editor-only proof gate that protects future visual/runtime work from hot-loop allocation and dependency drift.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- Static proof run: `hot_scope_findings=0`, `orphan_meta_count=0`, `missing_source_meta_count=0`.
- AppliedLore source audit remains green: `packets=460`, `rows=6900`, `route_cards=454`, `scene_terminal_os_runtime_verified_slots=27`.
- No `dotnet build`, no Unity compile, no DataMonolith bake, no scene edit.

## NARRATIVE_RESEARCH Control Pass 107 - AppliedLore layout proof and streamed Apex hygiene

What was wrong:

- `H8AppliedLoreWorldImpactRecord` had literal `Size=24` but no cold runtime `UnsafeUtility.SizeOf<T>()` proof in the existing AppliedLore facade.
- The Narrative Apex `.meta` scan allocated full path arrays with `Directory.GetFiles` before checking files.

What was done:

- Updated `Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs`: added `SizeBytes=24`, explicit padding at offsets 17/18/20, and cold `ValidateRuntimeLayout()`.
- Updated `Assets/_Project/Scripts/Editor/Narrative/H8NarrativeApexVerifier.cs`: Apex now requires the AppliedLore world-impact size constant, padding offsets and SizeOf proof.
- Replaced Apex project hygiene scans with `Directory.EnumerateFiles` streaming.

Cinematic cheats used:

- No runtime assertion or parser was added. The proof remains cold/editor-facing; runtime keeps the same 24-byte record and SignalBus routes.

Exact microseconds saved:

- Runtime frame cost in this pass: 0 us/frame.
- Verification: C# balance OK for changed files; `hot_scope_findings=0`; `.meta` scan `0/0`; DataVault write-lock proof OK; AppliedLore audit `packets=460`, `rows=6900`, `blob_bytes=8212352`, `route_cards=454`, `wiki_pages=6900`, `site_pages=6900`.
- No `dotnet build`, no Unity compile, no DataMonolith bake, no scene edit.

## NARRATIVE_RESEARCH Control Pass 108 - Central DataMonolith AppliedLore layout gate

What was wrong:

- The AppliedLore world-impact DTO had local SizeOf proof but was not included in the central `H8DataLayoutAudit.ValidateBlittableSizes()` route.

What was done:

- Added `H8AppliedLoreWorldImpactRecord` SizeOf and 8-byte alignment checks to `H8DataMonolithTypes.cs`.
- Added `H8DataMonolithTypes.cs` to Narrative Apex scope.
- Made `H8NarrativeApexVerifier` require central audit proof tokens for the AppliedLore world-impact route.

Cinematic cheats used:

- Kept the DTO 24 bytes. It is not a DataMonolith section record, so forcing 16-byte section alignment would spend bytes without a runtime owner need.

Exact microseconds saved:

- Runtime frame cost: 0 us/frame.
- Verification: tokenizer balance OK for three touched C# files; `hot_scope_findings=0`; `.meta` scan `0/0`; `lock_proof_bad=0`; AppliedLore audit `packets=460`, `rows=6900`, `blob_bytes=8212352`, `route_cards=454`.
- No `dotnet build`, no Unity compile, no DataMonolith bake, no scene edit.

## NARRATIVE_RESEARCH Control Pass 109 - AppliedLore UTF8 record-copy flattening

What was wrong:

- `H8AppliedLoreRuntime.TryGetUtf8` had an `in` public overload, but the private UTF8 surface selector could still take `H8AppliedLorePacketRecord` by value.

What was done:

- Routed both public `TryGetUtf8` overloads through `TryGetUtf8FromRecord(in record, ...)`.
- Changed the private selector to `in H8AppliedLorePacketRecord record`.
- Added `applied_lore_utf8_pass_by_ref_proofs` to `H8NarrativeApexVerifier`; the route now fails if the proof count drops below 4.

Cinematic cheats used:

- No runtime parser, UI component or helper subsystem was added. The existing baked string-pool route remains the owner.

Exact microseconds saved:

- Runtime frame cost: 0 us/frame.
- Avoided copy: one `H8AppliedLorePacketRecord` value pass removed from the facade lookup path.
- Verification: UTF8 proof `calls=2`, `in_params=4`, `total=6`; tokenizer balance OK for three touched C# files; touched runtime files `hot_roots=0`, `raw_forbidden_tokens=0`; DataVault helper proof acquire/release/finally with `heavy_tokens=0`; `git diff --check` clean; AppliedLore source audit OK with `packets=460`, `rows=6900`, `route_cards=454`.
- No `dotnet build`: active `dotnet`/`csc` processes were present, so throttling rules blocked build spam.

## NARRATIVE_RESEARCH Control Pass 110 - Unity asset meta hygiene closure

What was wrong:

- Four existing baker assets lacked Unity `.meta` companions: two compute shaders and two editor C# files under the `1722/1724` baker work.

What was done:

- Added `.meta` for `Assets/_Project/Art/Shaders/Include/GeologicalStrataBaker1724.compute`.
- Added `.meta` for `Assets/_Project/Art/Shaders/Include/HullCavitationBaker1722.compute`.
- Added `.meta` for `Assets/_Project/Editor/Bakers/GeologicalStrataBaker1724.cs`.
- Added `.meta` for `Assets/_Project/Editor/Bakers/HullCavitationBaker1722.cs`.
- Did not edit the source/baker files themselves.

Cinematic cheats used:

- No runtime component, scene edit or asset reimport was needed. This is pure identity stabilization.

Exact microseconds saved:

- Runtime frame cost: 0 us/frame.
- Hygiene proof: `orphan_meta_count=0`, `missing_source_meta_count=0`.
- GUID proof: each new GUID appears exactly once.
- AppliedLore source audit remains green: `packets=460`, `rows=6900`, `route_cards=454`.
- No `dotnet build`, no Unity import, no DataMonolith bake.

## NARRATIVE_RESEARCH Control Pass 111 - AppliedLore UTF8 single-owner route

What was wrong:

- `H8AppliedLoreRuntime` still had a private UTF8 surface selector after `H8StaticDataArena` already owned the localized UTF8 span route. That violated one fact -> one owner -> one route.

What was done:

- Removed the duplicated runtime facade selector.
- Routed both AppliedLore UTF8 overloads directly to `H8StaticDataArena.TryGetAppliedLoreUtf8(in record, surface, out utf8Bytes)`.
- Added `H8StaticDataArena.cs` to the Narrative Apex verifier scope.
- Made the Apex verifier require arena pass-by-ref proof and reject duplicate facade selectors.

Cinematic cheats used:

- No new runtime subsystem, parser, string pool, UI bridge or helper class was added. The existing baked arena route remains the single owner.

Exact microseconds saved:

- Runtime frame cost: 0 us/frame.
- Proof: `runtime_arena_utf8_calls=2`, `arena_methods=1`, `arena_in_record_params=1`, `total_pass_by_ref_proofs=4`, `facade_duplicate_selectors=0`.
- Hot-token proof: `H8AppliedLoreRuntime.cs` and `H8StaticDataArena.cs` report `hot_roots=0 raw_forbidden_tokens=0`.
- Static proof: tokenizer balance OK for four scoped C# files; `.meta` hygiene `orphan_meta_count=0 missing_source_meta_count=0`; DataVault helper proof acquire/release/finally with `heavy_tokens=0`; AppliedLore audit `packets=460`, `rows=6900`, `route_cards=454`.
- No `dotnet build`, no Unity compile/import, no DataMonolith bake, no scene edit.

## NARRATIVE_RESEARCH Control Pass 112 - Expanded Unity asset meta proof surface

What was wrong:

- The Apex `.meta` gate covered scripts and shaders only. Prefabs, materials, scenes, textures, models, audio, UI files and DataMonolith source text could still lose stable Unity identity without this verifier reporting it.

What was done:

- Extended `H8NarrativeApexVerifier` with one `SourceMetaRequiredExtensions` table.
- Replaced three hardcoded source-meta scan calls with one pass over the Assets tree filtered by 27 production Unity asset extensions.
- Kept the scan editor-only and streaming; no runtime route, no Unity import and no generated content rewrite.

Cinematic cheats used:

- No runtime validator was added. This stays a cold Apex gate and does not spend frame time.

Exact microseconds saved:

- Runtime frame cost: 0 us/frame.
- Static meta proof: `source_meta_extensions=27`, `source_meta_files_scanned=11908`, `missing_source_meta_files=0`, `meta_files_scanned=13887`, `orphan_meta_files=0`.
- C# proof: tokenizer balance OK for `H8NarrativeApexVerifier.cs`.
- AppliedLore source audit remains green: `packets=460`, `rows=6900`, `route_cards=454`, `wiki_pages=6900`, `site_pages=6900`.
- No `dotnet build`: active `dotnet` processes were present. No Unity compile/import, no DataMonolith bake, no scene edit.

## NARRATIVE_RESEARCH Control Pass 113 - Prologue black-box DataVault lock flattening

What was wrong:

- `AwaitableDropSequenceDirector.RecordStage` had correct `try/finally` release, but resolved runtime frame/orbital telemetry and cursor math while holding the DataVault black-box write lock.

What was done:

- Hoisted runtime frame, sequence, orbital speed, orbital distance and ring cursor calculation before `TryAcquireWriteLock`.
- Left only buffer validity, DTO primitive assignment, one `NativeArray` write and cursor store inside the locked `try`.
- Added `ScanPrologueBlackBoxDataVaultRoute` to `H8NarrativeApexVerifier`.
- Added Apex summary counters for `prologue_blackbox_write_locks`, `prologue_blackbox_release_finally`, `prologue_blackbox_hoisted_telemetry`, and `prologue_blackbox_heavy_inside_lock`.

Cinematic cheats used:

- No new telemetry object or runtime repair loop. Existing black-box truth stays compact; presentation can consume the same prologue route later.

Exact microseconds saved:

- Runtime frame cost: 0 us/frame.
- Critical-section content reduced to primitive assignment and one native slot write.
- Static proof: `prologue_blackbox_write_locks=1`, `prologue_blackbox_release_finally=1`, `prologue_blackbox_hoisted_telemetry=6`, `prologue_blackbox_heavy_inside_lock=0`.
- Token proof: no `GlobalRegistry.Get`, `GetComponent`, LINQ, `WaitForCompletion`, `new List`, or `new Dictionary` hits in the prologue file scan.
- Source proof: tokenizer balance OK for both changed C# files; `git diff --check` has no whitespace errors; AppliedLore source audit OK with `packets=460`, `rows=6900`, `route_cards=454`.
- No `dotnet build`: active `dotnet` PID 47240 blocked compilation by throttle. No Unity compile/import, no DataMonolith bake, no scene edit.

## NARRATIVE_RESEARCH Control Pass 115 - TerminalOS telemetry snapshot/write separation

What was wrong:

- `TerminalOsRuntime.RecordTelemetry` opened the terminal telemetry ring before computing layout hash, overlapping telemetry-ring access with a screen-command DataVault snapshot.
- `RecordDecryptionTelemetry` opened the decryption telemetry ring before puzzle/terminal snapshot reads and advanced the cursor from the raw cursor, not the clamped write index.

What was done:

- `RecordTelemetry` now computes `layoutHashSnapshot` before opening `_telemetryRingHandle`, guards ring length, writes through `telemetryIndex`, and advances cursor from that index.
- `RecordDecryptionTelemetry` now reads puzzle/terminal snapshots before opening `_decryptionTelemetryRingHandle`; decryption cursor advance uses `telemetryIndex`.
- `H8NarrativeApexVerifier` now includes `ScanTerminalOsTelemetryVaultRoute` and reports TerminalOS telemetry route-shape counters.

Cinematic cheats used:

- None. This is telemetry/route hygiene, not player-facing simulation.

Exact microseconds saved:

- Runtime frame cost added: 0 us/frame.
- Profiler measurement not run. Expected saving is one shortened/flattened DataVault buffer residency window per TerminalOS telemetry write frame.

Verification:

- Token balance OK for `TerminalOsRuntime.cs` and `H8NarrativeApexVerifier.cs`.
- Static counters: `terminal_layout_hoists=2`, `terminal_ring_after_snapshot_tokens=3`, `terminal_ring_length_guards=2`, `decryption_snapshot_before_ring=1`, `decryption_cursor_clamps=2`, `verifier_terminal_gate_tokens=9`.
- Hot-token scan for `LateFrameTick`, `RecordTelemetry`, `RecordDecryptionTelemetry`: `0`.
- AppliedLore source audit green: `packets=460`, `locales=15`, `rows=6900`, `route_cards=454`, `wiki_pages=6900`, `site_pages=6900`.
- `git diff --check`: no whitespace errors; CRLF warnings only.

Build/Unity proof:

- `dotnet build`: not run. This pass used source-only verification by design and did not require compiler spam.
- Unity import/playmode/DataMonolith bake/scene edit: not run.

Addendum - TerminalOS input telemetry partial:

What was wrong:

- `RecordTerminalInputTelemetry` in `TerminalOsRuntime_TerminalProjection.cs` composed `projectionFaults` inside the same local path that then opened `_terminalInputTelemetryRingHandle`.

What was done:

- `projectionFaults` is now fully composed before the terminal input telemetry ring is opened.
- The ring write path now guards zero length, writes through a clamped `telemetryIndex`, and advances `_terminalInputTelemetryCursor` from that clamped index.
- `H8NarrativeApexVerifier` now includes `TerminalOsRuntime_TerminalProjection.cs` in its TerminalOS telemetry route gate and requires input fault-before-ring and cursor clamp proofs.

Exact microseconds saved:

- Runtime frame cost added: 0 us/frame.
- Profiler measurement not run. Expected saving is a shorter native-buffer residency window for TerminalOS input black-box writes.
- Static counters after addendum: `input_faults_before_ring=1`, `input_cursor_clamps=2`, TerminalOS hot forbidden tokens `0`.

## NARRATIVE_RESEARCH Control Pass 114 - PDA telemetry redundant vault read removal

What was wrong:

- `PDAEncyclopediaStreamer.RecordTelemetry` performed a read-only DataVault lookup of `_telemetryHandle` just to check telemetry ring length, then immediately acquired a write lock for the same telemetry ring.

What was done:

- Removed the redundant `_telemetryHandle` read-only lookup from `RecordTelemetry`.
- Moved the telemetry ring capacity proof into the existing write-lock block with `telemetry.Length < TelemetryFrameCount`.
- Changed `WriteRuntimeState` to return `unlockedCountSnapshot` by `out uint`.
- Passed the streaming-frame unlocked-count snapshot from `LateFrameTick` into `RecordTelemetry`.
- Kept one runtime-state fallback read inside `RecordTelemetry` for locked/complete telemetry paths that do not write state first.
- Added `ScanPdaTelemetryVaultRoute` to `H8NarrativeApexVerifier`.
- Added Apex summary counters for `pda_telemetry_write_locks`, `pda_telemetry_release_finally`, `pda_telemetry_redundant_readonly`, `pda_telemetry_write_size_proofs`, `pda_telemetry_runtime_fallback_reads`, and `pda_telemetry_streaming_snapshot_passes`.

Cinematic cheats used:

- No new telemetry object or UI simulation. Existing PDA black-box route stays compact; presentation remains a LateFrame/VISUAL_SYNC concern.

Exact microseconds saved:

- Runtime frame cost: 0 us/frame added.
- Removed one read-only vault lookup from each visible PDA telemetry write frame.
- Removed one additional runtime-state vault read from the normal streaming PDA telemetry frame.
- Static proof: `lateframe_streaming_snapshot_write_calls=1`, `lateframe_streaming_snapshot_record_calls=1`, `write_runtime_state_out_params=1`, `record_telemetry_write_locks=2`, `record_telemetry_release_finally=2`, `record_telemetry_redundant_readonly=0`, `record_telemetry_runtime_fallback_reads=1`, `record_telemetry_size_proofs=1`, `pda_telemetry_hot_tokens=0`.
- Source proof: tokenizer balance OK for `PDAEncyclopediaStreamer.cs` and `H8NarrativeApexVerifier.cs`; `git diff --check` has no whitespace errors; AppliedLore source audit OK with `packets=460`, `rows=6900`, `route_cards=454`.
- No `dotnet build`: active `dotnet` PID 47240 blocked compilation by throttle. No Unity compile/import, no DataMonolith bake, no scene edit.

## NARRATIVE_RESEARCH Control Pass 116 - PDA black-box dump vault-read flattening

What was wrong:

- `PDAEncyclopediaStreamer.WriteBlackBoxDump` serialized the PDA black-box dump through `TryReadTelemetryDumpEntry`.
- `TryReadTelemetryDumpEntry` re-resolved `_telemetryHandle` via `TryReadVaultBuffer` for every telemetry row.
- The same method allocated its payload through raw `new NativeArray<byte>(...)` instead of the first-party `NativeFaultDumpWriter` transient payload route.

What was done:

- `WriteBlackBoxDump` now resolves `NativeArray<PdaEncyclopediaTelemetryEntry>.ReadOnly telemetrySnapshot` once before the fixed serialization loop.
- The 300-row copy loop reads from that snapshot directly and falls back to `default` entries if the snapshot is unavailable or shorter than expected.
- Removed `TryReadTelemetryDumpEntry`.
- Replaced raw Temp payload allocation with `NativeFaultDumpWriter.CreateTransientPayload(..., NativeArrayOptions.ClearMemory)` and `DisposeTransientPayload` in `finally`.
- Extended `H8NarrativeApexVerifier.ScanPdaTelemetryVaultRoute` with PDA dump tripwires for single snapshot, per-row reads, transient payload ownership, and raw payload allocation.

Cinematic cheats used:

- None. This is fault-path telemetry hygiene, not player-facing simulation.

Exact microseconds saved:

- Runtime frame cost added: 0 us/frame.
- Profiler measurement not run. Expected fault-path saving is up to 300 repeated DataVault read attempts removed per PDA dump.

Verification:

- Unity MCP `validate_script`: `PDAEncyclopediaStreamer.cs` 0 errors/0 warnings; `H8NarrativeApexVerifier.cs` 0 errors/0 warnings.
- AppliedLore source audit green: `packets=460`, `locales=15`, `rows=6900`, `route_cards=454`, `wiki_pages=6900`, `site_pages=6900`.
- Static tokens: `pda_try_read_dump_entry_refs=0`, `pda_raw_payload_allocs=0`, `pda_transient_payload_creates=1`, `pda_transient_payload_disposes=1`, `verifier_blackbox_gate_tokens=20`.
- `git diff --check`: CRLF warnings only; no whitespace errors.

Build/Unity proof:

- `dotnet build`: not run.
- Unity import/refresh/playmode/DataMonolith bake/scene edit: not run.

## Control Pass 123 - P418 Worker Encyclopedia Rewrite

What was wrong:

- The colony/workers hub still read too much like condensed design intent.
- An inline PowerShell JSON rewrite corrupted Cyrillic article prose into literal `?` characters.

What was done:

- Added dedicated longform source files:
  - `Docs/Lore/AppliedContent/articles/ru_RU/P418_SITE_WIKI_COLONY_AND_WORKERS_CLUSTER.md`
  - `Docs/Lore/AppliedContent/articles/en_US/P418_SITE_WIKI_COLONY_AND_WORKERS_CLUSTER.md`
- Reworked `AppliedLorePageExporter.py` so `external_site` pages prefer `external_site_article_path` before inline JSON article bodies.
- Linked `P418_SITE_WIKI_COLONY_AND_WORKERS_CLUSTER` to those files and cleared the corrupted inline longform fields.
- Re-exported 13,800 publication pages.

Cinematic Cheats used:

- None. Publication content only.

Exact microseconds saved:

- Runtime game frame cost added: 0 us/frame.
- C# change: none.
- DataMonolith DTO change: none.

Verification:

- `python -B Tools/AppliedLorePageExporter.py --root . --overwrite`: `applied_lore_pages_written=13800`, `skipped_existing=0`, `index_pages_written=30`.
- `python -B Tools/AppliedLoreRuntimeAudit.py --root . --source-only`: pass with `packets=460`, `site_pages=6900`, `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, `publication_cluster_rows=150`.
- Generated RU/EN `P418` external-site pages: literal `?=0`, replacement chars `0`, no `## Scanner`.
- Local HTTP `P418` RU page serves the updated article body.

Build/Unity proof:

- `dotnet build`: not run.
- Unity import/refresh/playmode/DataMonolith bake/scene edit: not run.

## NARRATIVE_RESEARCH Control Pass 122 - AppliedLore article voice decontamination

What was wrong:

- The first "longform" pass still sounded like instructions to agents and writers.
- The colony/workers article explicitly discussed player framing, family-hook avoidance and evidence-layer intent instead of staying inside the world.

What was done:

- Rewrote `P416`-`P420` `external_site_article` bodies in `en_US` and `ru_RU` as player-facing encyclopedia/archive prose.
- Removed visible meta-guidance tone from the public hub bodies.
- Re-exported generated pages.

Cinematic cheats used:

- None. This is editorial/publication content.

Exact microseconds saved:

- Runtime game frame cost added: 0 us/frame.
- C# change: none.
- DataMonolith DTO change: none.

Verification:

- `python -B Tools/AppliedLorePageExporter.py --root . --overwrite`: `applied_lore_pages_written=13800`, `skipped_existing=0`, `index_pages_written=30`.
- `python -B Tools/AppliedLoreRuntimeAudit.py --root . --source-only`: pass with `packets=460`, `site_pages=6900`, `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, `publication_cluster_rows=150`.
- Smell scan across 10 EN/RU public hub bodies: no `player/game/should/do not/use as/site nav/publication hub/игрок/игра/долж/нужно/использовать как/публикационный хаб`.
- Same scan: 0 `## Scanner` headings and 0 corrupted `????` payloads.

Build/Unity proof:

- `dotnet build`: not run.
- Unity import/refresh/playmode/DataMonolith bake/scene edit: not run.

## NARRATIVE_RESEARCH Control Pass 119 - AppliedLore hard-sci-fi encyclopedia cluster manifest

What was wrong:

- The AppliedLore encyclopedia had generated pages and a 13,800-row publication surface manifest, but site/wiki consumers still lacked a compact hub map for the hard-sci-fi world encyclopedia.
- The five RS084 navigation clusters existed as packets and evidence graph rows, but there was no deterministic CSV bridge exposing them across all locales and both surfaces.

What was done:

- `Tools/AppliedLorePageExporter.py` now generates `Docs/Lore/AppliedContent/Publication_Cluster_Index.csv`.
- The new manifest is generated from `Docs/Lore/AppliedContent/graphs/RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv` plus packet JSON.
- Each row carries surface, locale, direction, cluster id/order, cluster packet id, release set, article id, unlock id, spoiler tier, primary surface, prerequisites, next cluster, localization status/flags, tags, page path, localized title, truth payload and route question.
- `Tools/AppliedLoreRuntimeAudit.py` now validates the cluster manifest against RS084 graph, source CSV rows and generated page paths.
- `Docs/Lore/AppliedContent/README.md` documents the new publication cluster index as the site/wiki ingestion bridge.

Cinematic cheats used:

- None. This is publication transport, not simulation or rendering.

Exact microseconds saved:

- Runtime frame cost added: 0 us/frame.
- Runtime DTO layout change: none.
- Site/wiki assembly can use one 150-row CSV instead of inferring hub routes from page crawls; no profiler timing claimed.

Verification:

- `python -B -c "import ast,pathlib; ..."`: `ast_ok=2` for `Tools/AppliedLorePageExporter.py` and `Tools/AppliedLoreRuntimeAudit.py`.
- `python -B Tools/AppliedLorePageExporter.py --root . --overwrite`: `applied_lore_pages_written=13800`, `skipped_existing=0`, `index_pages_written=30`.
- `python -B Tools/AppliedLoreRuntimeAudit.py --root . --source-only`: pass with `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, `publication_cluster_rows=150`, `wiki_pages=6900`, `site_pages=6900`.
- `Publication_Cluster_Index.csv` row count: 150.
- `git diff --check` for touched files: no whitespace errors; CRLF conversion warnings only.

Build/Unity proof:

- `dotnet build`: not run.
- Unity import/refresh/playmode/DataMonolith bake/scene edit: not run.
- `python -B -m py_compile ...`: not used as final proof because it attempted to write `.pyc` under `Tools/__pycache__` and hit a permission error.

## NARRATIVE_RESEARCH Control Pass 118 - AppliedLore publication metadata bridge

What was wrong:

- Generated AppliedLore pages were readable but not enough for robust site/wiki ingestion.
- Page frontmatter did not carry release-set, unlock and tag metadata.
- A website builder would need to scan 13,800 Markdown pages or parse packet JSON to build navigation.

What was done:

- `Tools/AppliedLorePageExporter.py` now emits `release_set_id`, `unlock_id`, `poi_tags` and `biome_tags` in every generated page frontmatter.
- Added `Docs/Lore/AppliedContent/Publication_Surface_Index.csv` with one row per generated surface/locale/packet page.
- `Tools/AppliedLoreRuntimeAudit.py` now validates page frontmatter and the publication surface index against CSV source rows.
- `Docs/Lore/AppliedContent/README.md` documents the publication surface index route.

Cinematic cheats used:

- None. This is content transport and publication infrastructure.

Exact microseconds saved:

- Runtime frame cost added: 0 us/frame.
- Runtime DTO layout change: none.
- Publication assembly no longer needs to crawl page bodies to discover route metadata; this is an offline build-time saving, not profiler-measured.

Verification:

- `python -B -m py_compile Tools/AppliedLorePageExporter.py Tools/AppliedLoreRuntimeAudit.py`: pass.
- `python -B Tools/AppliedLorePageExporter.py --root . --overwrite`: `applied_lore_pages_written=13800`, `index_pages_written=30`.
- `python -B Tools/AppliedLoreRuntimeAudit.py --root . --source-only`: pass with `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, `wiki_pages=6900`, `site_pages=6900`.
- `Publication_Surface_Index.csv` row count: 13,800.
- Exact forbidden localization marker scan across CSV/wiki/site: 0 matches.

Build/Unity proof:

- `dotnet build`: not run.
- Unity import/refresh/playmode/DataMonolith bake/scene edit: not run.

## NARRATIVE_RESEARCH Control Pass 117 - AppliedLore multilingual encyclopedia status route

What was wrong:

- AppliedLore source packets carried draft/native-review prefixes inside localized player-visible prose.
- Those prefixes could leak into baked CSV rows and generated wiki/site Markdown.
- There was no durable source audit gate proving that review-state text stayed out of player-facing publication surfaces.

What was done:

- `Tools/AppliedLoreImporter.py` now strips known authoring-only localization prefixes from localized text before CSV export.
- Existing `flags` now carry draft localization state; generated `H8AppliedLoreHashes.cs` exposes `RowFlagDraftLocalization`.
- `Tools/AppliedLorePageExporter.py` now writes page frontmatter for `direction`, `localization_status`, and `localization_flags`.
- Added `Docs/Lore/AppliedContent/Localization_Status_Index.md` from source packet JSON.
- `Tools/AppliedLoreRuntimeAudit.py` now fails if forbidden draft/native-review markers appear in CSV player-visible fields or generated `in_game_wiki`/`external_site` pages.

Cinematic cheats used:

- None. This is encyclopedia/publication routing, not simulation.

Exact microseconds saved:

- Runtime frame cost added: 0 us/frame.
- DTO layout change: none. `H8AppliedLorePacketRecord` remains 128 bytes and uses the existing `Flags` field.
- Publication payload is cleaner: review state is metadata, not visible article prose.

Verification:

- `python -B -m py_compile Tools/AppliedLoreImporter.py Tools/AppliedLorePageExporter.py Tools/AppliedLoreRuntimeAudit.py`: pass.
- `python -B Tools/AppliedLoreRuntimeAudit.py --root . --source-only`: pass with `packets=460`, `locales=15`, `rows=6900`, `visible_marker_csv_fields=48300`, `visible_marker_pages=13830`, `wiki_pages=6900`, `site_pages=6900`, `index_pages=30`.
- CSV count: `rows=6900`, `locales=15`, `draft_flagged=5095`.
- Exact forbidden-marker scan across CSV, in-game wiki, and external site pages: 0 matches.

Build/Unity proof:

- `dotnet build`: not run.
- Unity import/refresh/playmode/DataMonolith bake/scene edit: not run.

## NARRATIVE_RESEARCH Control Pass 120 - AppliedLore local human reader

What was wrong:

- Generated lore pages existed, but reading them as a coherent encyclopedia required manual folder/CSV navigation.
- The publication bridge had machine indexes, but no minimal human browsing surface.

What was done:

- Added `Docs/Lore/AppliedContent/reader.html`.
- Reader uses existing `Publication_Surface_Index.csv`, `Publication_Cluster_Index.csv`, and generated Markdown pages.
- Reader supports surface, locale, localization status, search, RS084 cluster hubs, article list, article metadata, and RTL article direction.
- Added one README line with the local server command and URL.
- Started local server at `http://127.0.0.1:8788/reader.html` with Python PID `32960`.

Cinematic cheats used:

- None. This is static publication UI, not simulation.

Exact microseconds saved:

- Runtime game frame cost added: 0 us/frame.
- DataMonolith DTO change: none.
- C# change: none.

Verification:

- Extracted JavaScript from `reader.html`: `node --check` pass.
- Python HTML parser: `scripts=1`, `styles=1`.
- Publication input check: `surface_rows=13800`, `cluster_rows=150`, default Russian start article exists.
- HTTP `HEAD`: `reader.html=200`, `Publication_Surface_Index.csv=200`, default Russian start article=200`.

Build/Unity proof:

- `dotnet build`: not run.
- Unity import/refresh/playmode/DataMonolith bake/scene edit: not run.
- Playwright install: not run; dependency missing and not needed for static reader proof.

## NARRATIVE_RESEARCH Control Pass 121 - AppliedLore public article quality repair

What was wrong:

- The local reader exposed the actual weakness: RS084 public hub pages were service cards, not articles.
- The generated site output displayed `Scanner`, `Terminal`, `Audio`, and `Field Note` blocks as if they were website prose.
- Russian RS084 source text for the same hubs was corrupted in prior generation history and needed restoration.

What was done:

- Added `external_site_article` longform content for `P416`-`P420` in `en_US` and `ru_RU`.
- Restored Russian titles/snippets for those five packets.
- Updated `Tools/AppliedLorePageExporter.py` so `external_site` uses the longform article body when present and suppresses service-section headings for those pages.
- Re-exported all localized pages.

Cinematic cheats used:

- None. This is publication/content routing, not simulation.

Exact microseconds saved:

- Runtime game frame cost added: 0 us/frame.
- C# change: none.
- DataMonolith DTO change: none.

Verification:

- `AppliedLorePageExporter.py` AST parse: pass.
- `external_site_article_rows=10`.
- `python -B Tools/AppliedLorePageExporter.py --root . --overwrite`: `applied_lore_pages_written=13800`, `skipped_existing=0`, `index_pages_written=30`.
- `python -B Tools/AppliedLoreRuntimeAudit.py --root . --source-only`: pass with `packets=460`, `site_pages=6900`, `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, `publication_cluster_rows=150`.
- Five EN public hub pages now have 5-6 paragraphs and 0 `## Scanner` headings.
- Five RU public hub pages have restored UTF-8 titles and no corrupted `????` payload.

Build/Unity proof:

- `dotnet build`: not run.
- Unity import/refresh/playmode/DataMonolith bake/scene edit: not run.
