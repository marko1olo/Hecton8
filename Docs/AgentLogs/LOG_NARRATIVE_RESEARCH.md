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
