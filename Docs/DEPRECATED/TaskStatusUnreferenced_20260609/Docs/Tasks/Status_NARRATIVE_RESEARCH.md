# Status_NARRATIVE_RESEARCH

## Control Pass 81 - Apex verifier scope and scene-risk separation

- [x] Restored active task file after Batch015 archive move.
  - DOD: active `Docs/Tasks/Status_NARRATIVE_RESEARCH.md` exists again; archive remains untouched.
  - Rejected: relying on archived status as live working memory.
  - Estimate: editor/docs only; runtime cost 0.
- [x] Fixed C# Apex source scope drift.
  - DOD: `H8NarrativeApexVerifier` now includes `GlobalSignals.RuntimeLifecycle.cs` in `RuntimeSourcePaths`; `ScanEvents.cs` remains included, so scanner/PDA/runtime lifecycle route sources are parsed together.
  - Rejected: suppressing `scanner_lore_fragment_sources_missing` or adding runtime coupling.
  - Estimate: editor-only AST proof; runtime cost 0.
- [x] Fixed verifier false phase failures for static/generic calls.
  - DOD: `CountInvocationInMethod` now uses `MatchesInvocation`, allowing exact simple-name matches and full static/generic invocation text matches. Existing UI/PDA `SignalBus<UIRescaleRequestSignal>.GetFrameSnapshot()` readers now count correctly.
  - Rejected: changing UI/PDA runtime code that already consumed immutable snapshots in `LateFrameTick`.
  - Estimate: editor-only AST proof; runtime cost 0.
- [x] Fixed dump gate-lock proof for `Monitor.Enter` + `try/finally`.
  - DOD: `CountLockStatementsInMethod` now counts `Monitor.Enter`/`System.Threading.Monitor.Enter` as gate lock scopes. Existing `TerminalOsRuntime.DecryptionBlackBoxDumpWriter.DrainPending` proves one gate lock plus write after release.
  - Rejected: rewriting valid runtime code to `lock {}` solely for verifier compatibility.
  - Estimate: editor-only AST proof; runtime cost 0.
- [x] Split world-scene diagnostics from Narrative/AppliedLore code failure.
  - DOD: MapMagic/Terrain/TerrainCollider absence in `02_HECTON_WORLD.unity` now increments `scene_world_dependency_warnings` instead of `dependency_findings`; C# and Python audits still print the counters.
  - Rejected: raw YAML scene repair in a shared 20-agent workspace; hard failing Narrative Apex on world-domain ownership.
  - Estimate: editor/offline only; runtime cost 0.
- [x] Revalidated source/data without build spam.
  - DOD: local in-memory Roslyn parse over 7 linked files reports `errors=0`; AppliedLore source/full audits pass with `packets=55`, `rows=825`, `applied_records=825`, `applied_routes=49`, and `blob_bytes=1804864`.
  - Rejected: `dotnet build`; active Unity-owned `dotnet.exe` exists.
  - Estimate: no gameplay truth, DTO layout, save identity, or SignalBus payload change.
- [x] Rechecked scene-owned AppliedLore terminal placement after shared scene restoration.
  - DOD: AppliedLore full/source audits now report `scene_terminal_os_runtime_rows=1`, `scene_terminal_os_runtime_renderer_slots=27`, `scene_terminal_os_runtime_transform_slots=27`, `scene_terminal_os_runtime_verified_slots=27`, `scene_terminal_preview_rows=27`, `scene_placement_covered_rows=34`, `scene_bindings=7`, `prefab_bindings=43`, `scene_world_dependency_warnings=0`.
  - Rejected: keeping the stale blocked status after the scene runtime object returned.
  - Estimate: serialized scene/data proof only; runtime cost 0.

## Control Pass 82 - TerminalOS preview hash drift guard

- [x] Corrected TerminalOS preview hash serialized values in `02_HECTON_WORLD.unity`.
  - DOD: 27 `terminalOsPreviewHash` values now match `TerminalOsHash.HashIndex(index)` for preview indices 0-26; AppliedLore full audit passes with 27 terminal preview rows and 27 verified TerminalOS slots.
  - Rejected: changing runtime resolver semantics or adding scene search/self-heal code.
  - Estimate: no per-frame cost; serialized data only.
- [x] Added C# Apex guard for future TerminalOS preview hash drift.
  - DOD: `H8NarrativeApexVerifier` now checks `terminal_os_preview_hash_pairs`, `terminal_os_preview_hash_mismatches`, and duplicate preview indices from scene YAML against the same hash formula used by TerminalOS runtime.
  - Rejected: relying only on Python audit; accepting C# Apex green while scene preview hashes can be wrong.
  - Estimate: editor-only AST/scene proof; runtime cost 0.
- [x] Revalidated without compile spam.
  - DOD: Unity `validate_script` on `H8NarrativeApexVerifier.cs` reports 0 errors/0 warnings; AppliedLore source and full audits pass.
  - Rejected: launching `dotnet build` or Unity compile while CPU is above 50% and Unity Roslyn/dotnet processes are active.
  - Estimate: no gameplay truth, DTO layout, save identity, or SignalBus payload change.
- [x] Unity Apex menu rerun after editor import.
  - DOD: Unity Apex reports `files_expected=22`, `files_parsed=22`, `terminal_os_expected=27`, `terminal_os_runtime_rows=1`, `terminal_os_verified_slots=27`, `terminal_os_preview_hash_pairs=27`, `terminal_os_preview_hash_mismatches=0`, `terminal_os_preview_hash_duplicate_indices=0`, `dependency_findings=0`, `phase_findings=0`, `zero_gc_findings=0`, `job_complete_findings=0`, `lock_findings=0`, `fatal_findings=0`, `build_invocations=0`, `analysis=RoslynAST_in_memory`.
  - Rejected: running `dotnet build`; Unity import/compile was requested once after CPU dropped and no `csc.exe` existed.
  - Estimate: editor-only proof; runtime cost 0.
- [x] Removed stale grep search processes from earlier agents.
  - DOD: stopped only old `rg.exe` PIDs 12544, 18908, 16748; repeat PID check returned no live processes.
  - Rejected: killing Python servers, Unity, or Unity Roslyn/dotnet processes without ownership proof.
  - Estimate: workstation hygiene only; runtime cost 0.
- [x] Separated stale Unity Console missing-script noise from current pass.
  - DOD: full scene/prefab/asset YAML scan found 0 `m_Script: {fileID: 0}` and 0 empty/zero script GUID references; after Console clear and Apex rerun, missing-script messages did not return.
  - Rejected: raw-deleting MonoBehaviour blocks without a matching serialized defect.
  - Estimate: editor/source proof only; runtime cost 0.

## Control Pass 83 - MetaCampaign visual phase hardening

- [x] Expanded Narrative Apex verifier scope to adjacent campaign/lore/prologue owners.
  - DOD: `H8NarrativeApexVerifier.RuntimeSourcePaths` now includes `MetaCampaignService.cs`, `CorporateOrderSystem.cs`, `LoreDatabaseManager.cs`, `ProceduralLoreDirector.cs`, and `AwaitableDropSequenceDirector.cs`.
  - Rejected: claiming APEX compliance from only TerminalOS/PDA/scanner files while campaign/prologue narrative owners remained outside the AST pass.
  - Estimate: editor-only AST scope; runtime cost 0.
- [x] Added transfer-helper proof for DataVault write locks.
  - DOD: verifier accepts direct `TryAcquireWriteLock` only with release in `finally`, and now also checks `TryAcquire*Write(out IDataVault lockedVault)` helpers plus caller-side `Release*Write` in `finally`.
  - Rejected: flagging safe helper-transfer patterns as false positives or allowing helper callers without a caller `finally`.
  - Estimate: editor-only proof; runtime cost 0.
- [x] Deferred MetaCampaign visual state writes to `LateFrameTick`.
  - DOD: `MetaCampaignService.PublishStateSideEffects` and `OnEnable` now call `QueueCachedVisualState`; `LateFrameTick` calls `FlushCachedVisualState`; only `FlushCachedVisualState` calls `PublishCachedVisualState`, which owns the `Shader.SetGlobalFloat` and ecosystem visual-pressure bridge.
  - Rejected: direct `Shader.SetGlobalFloat` reachable from cold APIs or simulation side-effect methods.
  - Estimate: 0 allocations; added primitive dirty flag and two primitive pending fields; one branch per `LateFrameTick`.
- [x] Added MetaCampaign visual phase guard to C# Apex.
  - DOD: verifier reports `meta_campaign_visual_queue_calls`, `meta_campaign_visual_lateframe_flushes`, `meta_campaign_visual_publish_calls`, and `meta_campaign_visual_shader_writes`, and fails if direct visual publish calls reappear.
  - Rejected: relying on prose or grep without a persistent C# tripwire.
  - Estimate: editor-only AST/source proof; runtime cost 0.
- [x] Revalidated source/data without build spam.
  - DOD: `python -B Tools/AppliedLoreRuntimeAudit.py --root Hecton8` passes with `packets=55`, `rows=825`, `applied_records=825`, `applied_routes=49`, `scene_terminal_os_runtime_verified_slots=27`, `scene_bindings=7`, `prefab_bindings=43`, `wiki_pages=825`, `site_pages=825`.
  - Rejected: `dotnet build`; CPU reached 100% and Unity `VBCSCompiler` PID 26752 stayed active.
  - Estimate: data/content proof only; runtime cost 0.
- [ ] Unity MCP validate/menu proof after compiler throttle clears.
  - DOD pending: `validate_script` for `MetaCampaignService.cs` and `H8NarrativeApexVerifier.cs`, then `Hecton8/Lore/Run Narrative Apex Verification`.
  - Current blocker: Unity MCP `validate_script` disconnected once and timed out once while Unity/VBCSCompiler was active; CPU later returned to 100%.
  - Estimate: no `dotnet build` invoked in this pass.

## Control Pass 84 - MetaCampaign side-effect phase closure

- [x] Deferred MetaCampaign audio and cartography side effects to `LateFrameTick`.
  - DOD: `PublishStateSideEffects` now queues `QueueCampaignBroadcast` and `QueueCartographyState`; only `LateFrameTick` calls `FlushCampaignBroadcast` and `FlushCartographyState`, and only those flush methods call `PublishCampaignBroadcast` / `PublishCartographyState`.
  - Rejected: direct VWS/cartography `SignalBus` publication from state mutation/cold setter paths; adding new lanes or managed queues.
  - Estimate: 0 allocations; two dirty bools plus two pending uint fields; two branches per `LateFrameTick`.
- [x] Expanded MetaCampaign Apex phase guard from visual-only to full side-effect route.
  - DOD: `H8NarrativeApexVerifier` now reports/fails `meta_campaign_audio_*` and `meta_campaign_cartography_*` counters alongside visual route counters.
  - Rejected: prose proof or loose grep that cannot fail the Unity Apex menu.
  - Estimate: editor-only AST proof; runtime cost 0.
- [x] Rechecked scoped hot dependency and lock shape without compile spam.
  - DOD: scoped source scan found no runtime `GlobalRegistry.Get<T>()`, `GetComponent`, `TryGetComponent`, or `GetComponents` in active Narrative/AppliedLore runtime files; `MetaCampaignService` write-lock call sites remain acquire -> `try/finally` release with blackbox writes outside variable/rules locks.
  - Rejected: `dotnet build`, Unity refresh, or Unity Apex menu while CPU is 100% and Unity/dotnet compiler PID 26752 is active.
  - Estimate: no gameplay truth, DTO layout, save identity, or SignalBus payload change.
- [ ] Unity MCP validate/menu proof after compiler throttle clears.
  - DOD pending: `validate_script` for `MetaCampaignService.cs` and `H8NarrativeApexVerifier.cs`, then `Hecton8/Lore/Run Narrative Apex Verification`.
  - Partial proof: Unity `validate_script` for `H8NarrativeApexVerifier.cs` reports 0 errors/0 warnings.
  - Current blocker: `MetaCampaignService.cs` validation disconnected once and then timed out once; CPU returned to 90% with active `dotnet` PID 26752; no build, Unity import, or Apex menu was launched.
  - Estimate: no `dotnet build` invoked in this pass.

## Control Pass 85 - Ex-Deep-Reach canon and AppliedLore RS012-RS014

- [x] Locked latest user-controlled story decisions in canon docs.
  - DOD: `Canon_Locks.md`, `Open_Questions.md`, `Lore_Bible.md`, and `Narrative_Crystallization.md` now lock the player as former Deep Reach / current Marauder, no family hook; Great Tide as real physics plus Deep Reach liability; concrete escape chain; Atlas maintenance ecology; and first-hour spine.
  - Rejected: keeping stale "do not start with ex-Deep-Reach" guard after user reopened and approved that identity.
  - Estimate: static lore only; runtime cost 0.
- [x] Added AppliedLore release sets `RS012_PLAYER_LIABILITY_ESCAPE`, `RS013_COLONY_ATLAS_MAINTENANCE`, and `RS014_COLONY_RETURN_WINDOWS`.
  - DOD: 15 new packets `P056`-`P070` with scanner, terminal, audio, in-game wiki, external-site, field-note, unlock tags, EN/RU text and draft-filled remaining locales.
  - Rejected: loose prose-only lore without packet IDs, route cards, binding maps, or publication pages.
  - Estimate: baked string-pool content path only; no hot runtime parser.
- [x] Propagated new lore through authoring/export surfaces.
  - DOD: added manifests, packet bundles, release markdown, evidence graphs, route cards, runtime binding maps, scene binding target backlog, image briefs, exported source CSV, generated hash constants, in-game wiki pages, external-site pages, and localized indexes.
  - Rejected: editing Unity scenes or C# gameplay systems during a lore/content pass.
  - Estimate: source-data rows only; runtime impact depends on later DataMonolith bake, no new per-frame code.
- [x] Verified source pipeline.
  - DOD: `AppliedLoreImporter.py` reports `applied_lore_packets=70 localized_rows=1050`; page exporter reports `applied_lore_pages_written=450`; route-card exporter reports `applied_lore_route_cards=64`; source-only audit passes with `packets=70`, `rows=1050`, `graph_rows=70`, `route_cards=64`, `wiki_pages=1050`, `site_pages=1050`, `binding_map_rows=70`.
  - Rejected: claiming full baked `static_data.h8bin` proof without running a Unity/DataMonolith bake.
  - Estimate: no `dotnet build`, no Unity compile, no runtime code touched.

## Control Pass 86 - Domains / Aegir moon ladder / HECTON-8 geology AppliedLore

- [x] Added AppliedLore release sets `RS015_HUMAN_DOMAINS_ROUTE_ECONOMY`, `RS016_AEGIR_SYSTEM_MOON_LADDER`, and `RS017_HECTON8_GEOLOGY_RESOURCE_ECOLOGY`.
  - DOD: 15 packets `P071`-`P085` now cover Sol/Centauri/Barnard/Tau/Luyten domain roles, Aegir/Ran light, moon ladder, relay hazards, HECTON-8 orbit/tide geometry, Great Tide physics, pressure glass, brine canyons, vent forges and wider resource stack.
  - Rejected: leaving human space, local astronomy and geology as loose internal prose with no packet IDs or route cards.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS015-RS017 through content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding target backlogs, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, or Unity scene edits in a lore pass.
  - Estimate: no per-frame code path added.
- [x] Synced canon memory.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, and `Narrative_Crystallization.md` now lock domain roles, moon ladder functions and geology/resource route pillars.
  - Rejected: keeping already-decided items as open questions.
  - Estimate: docs/content only.
- [x] Verified source pipeline.
  - DOD: importer reported `applied_lore_packets=85 localized_rows=1275`; page exporter wrote 450 new pages; route-card exporter reported `applied_lore_route_cards=79`; source-only audit passed with `packets=85`, `rows=1275`, `graph_rows=85`, `route_cards=79`, `wiki_pages=1275`, `site_pages=1275`, `binding_map_rows=85`.
  - Rejected: claiming baked `static_data.h8bin` proof without a DataMonolith bake.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched.

## Control Pass 87 - Carrier debt / physical atlas / ending agency AppliedLore

- [x] Added AppliedLore release sets `RS018_CARRIER_DEBT_CLAIM_AUTHORITY`, `RS019_HECTON8_PHYSICAL_ATLAS_DEPTH_BANDS`, and `RS020_ATLAS_ENDING_AGENCY_DOSSIER`.
  - DOD: 15 packets `P086`-`P100` now lock Aegir Reclamation Pool, Keelmark Mutual, 4.8 tonne-window debt, Black Keel first voice, Deep Reach priority hook, collision-fractured HECTON-8 origin, ocean depth bands, seafloor windows, seed invariants, pressure-containment stages, Atlas person-boundary, Recovery Compliance Office, false-ending taxonomy, Marauder dossier persistence and final payload choices.
  - Rejected: asking the user to decide already-solvable carrier/debt/geology/ending details without an applied proposal.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS018-RS020 through content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding target backlogs, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: leaving these as one-off markdown articles not connected to runtime authoring routes.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, and `Narrative_Crystallization.md` now mark these choices as locks and leave only actual tuning/name questions open.
  - Rejected: preserving stale unresolved prompts for exact claim-pool name, insurer shell, debt amount, first voice mode, HECTON-8 origin, depth bands, seafloor access, containment stages and Atlas person recognition.
  - Estimate: docs/content only.
- [x] Verified source pipeline after all edits.
  - DOD: importer reported `applied_lore_packets=100 localized_rows=1500`; page exporter wrote 450 new pages; route-card exporter reported `applied_lore_route_cards=94`; source-only audit passed with `packets=100`, `rows=1500`, `graph_rows=100`, `route_cards=94`, `route_source_rows=94`, `wiki_pages=1500`, `site_pages=1500`, `index_pages=30`, `binding_map_rows=100`, `target_backlog_rows=100`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 88 - Transit doctrine / signoff chain / first tools / resource taxonomy AppliedLore

- [x] Added AppliedLore release sets `RS021_INTERSTELLAR_TRANSIT_ROUTE_HISTORY`, `RS022_DEEP_REACH_SIGNOFF_CHAIN`, `RS023_FIRST_TOOL_CHAIN_SURVIVAL_GATE`, and `RS024_RESOURCE_RECIPE_TAXONOMY`.
  - DOD: 20 packets `P101`-`P120` now cover no-FTL route economy, beam-sail probe era, pellet-fusion freight, RAN-B:H8 catalog language, Black Keel in-system limits, named Deep Reach signoff chain, first-hour tools and resource category split.
  - Rejected: leaving hard-sci-fi transit, Deep Reach responsibility names, first tools and resource classes as open prose without packet IDs, route cards or binding maps.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS021-RS024 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search or Unity scene edits in this lore pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Removed stale canon conflicts.
  - DOD: `Player_Motive_Arc.md` no longer warns against an ex-Deep-Reach protagonist; `Aegir_Gas_Giant.md`, `Canon_Locks.md`, `Open_Questions.md`, `Lore_Bible.md`, `Narrative_Crystallization.md`, and AppliedContent README now reflect RS021-RS024.
  - Rejected: preserving stale unresolved prompts for drive family, public catalog label, senior names, first tool chain and resource class split.
  - Estimate: docs/content only.
- [x] Verified source pipeline after all edits.
  - DOD: importer reported `applied_lore_packets=120 localized_rows=1800`; page exporter wrote 600 new pages; route-card exporter reported `applied_lore_route_cards=114`; source-only audit passed with `packets=120`, `rows=1800`, `graph_rows=120`, `route_cards=114`, `route_source_rows=114`, `wiki_pages=1800`, `site_pages=1800`, `index_pages=30`, `binding_map_rows=120`, `target_backlog_rows=120`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 89 - Law / Atlas classification / false exits / dossier replay AppliedLore

- [x] Added AppliedLore release sets `RS025_HUMAN_LAW_PUBLIC_MEMORY`, `RS026_ATLAS_PUBLIC_AUTHORITY_CLASSIFICATION`, `RS027_FALSE_EXIT_RETURN_PRESSURE`, and `RS028_REPLAY_CONTRACT_DOSSIER_RULES`.
  - DOD: 20 packets `P121`-`P140` now cover authority split, Marauder legal loophole, salvage evidence custody, public Aegir memory, Deep Reach origin chain, Atlas public/insurance/classified status, shutdown ethics, false exits, public ledger leak, riskier contracts and dossier knowledge-not-power persistence.
  - Rejected: leaving these as open philosophical prompts or private notes without packet IDs, route cards, binding maps and publication surfaces.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS025-RS028 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, or Unity scene edits in this lore pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Avoided TerminalOS scene-slot conflict.
  - DOD: the audit initially exposed a TerminalOS renderer slot mismatch if all new manual rows were forced into `MessageTerminal`; new manual rows now route through `NarrativeDiscovery` placement backlog until a Unity scene pass expands slots deliberately.
  - Rejected: editing the active Unity scene while another Unity agent is working; pretending the mismatch was harmless; expanding terminal prefabs without scene capacity.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, and AppliedContent README now mark the new locks and leave only real table/UI/tone questions open.
  - Rejected: keeping stale unresolved prompts for domain roles, salvage evidence status, Atlas public/legal/classified status, shutdown framing, false exit shape and dossier persistence.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=140 localized_rows=2100`; page exporter wrote 600 new pages; route-card exporter reported `applied_lore_route_cards=134`; source-only audit passed with `packets=140`, `locales=15`, `rows=2100`, `graph_rows=140`, `route_cards=134`, `route_source_rows=134`, `wiki_pages=2100`, `site_pages=2100`, `index_pages=30`, `binding_map_rows=140`, `target_backlog_rows=140`, `manual_rows=54`, `manual_discovery_policy_rows=27`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 90 - Route time / Deep Reach shells / first hour / colony evidence AppliedLore

- [x] Added AppliedLore release sets `RS029_ROUTE_TIME_DISTANCE_MODEL`, `RS030_DEEP_REACH_SHELL_ORG_CHART`, `RS031_FIRST_HOUR_PLAYABLE_SPINE`, and `RS032_COLONY_HUMAN_EVIDENCE_LAYER`.
  - DOD: 20 packets `P141`-`P160` now cover Ran/Aegir route-time scale, probe/freight/crew/relay delays, Deep Reach public/shell org chain, first-hour contract/drop/P-63/lies/Atlas trace, and colony-as-worker-evidence without family melodrama.
  - Rejected: leaving route distance, Deep Reach org chart, opening hour and colony humanity as loose conversation without packet IDs, route cards, binding maps and publication surfaces.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS029-RS032 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, or Unity scene edits in this lore pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Flattened new manual placement into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=74`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=47`, `placement_plan_rows=74`, and `scene_terminal_os_runtime_verified_slots=27`; TerminalOS capacity was not expanded during a parallel Unity pass.
  - Rejected: forcing new content through terminal slots, raw-editing Unity scene/prefab YAML, or claiming runtime placement beyond proven serialized rows.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS029-RS032 locks and leave only real tuning/table/native-localization questions open.
  - Rejected: preserving stale unresolved prompts for route time, formal Deep Reach name/shell chain, first-hour sequence and colony human-evidence method.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=160 localized_rows=2400`; route-card exporter reported `applied_lore_route_cards=154`; source-only audit passed with `packets=160`, `locales=15`, `rows=2400`, `graph_rows=160`, `route_cards=154`, `route_source_rows=154`, `wiki_pages=2400`, `site_pages=2400`, `index_pages=30`, `binding_map_rows=160`, `target_backlog_rows=160`, `manual_policy_rows=74`, `manual_discovery_policy_rows=47`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 91 - Domain tables / worker evidence / pressure recipes / dossier presentation AppliedLore

- [x] Added AppliedLore release sets `RS033_DOMAIN_EPHEMERIS_ROUTE_TABLE`, `RS034_WORKER_NAME_JOB_EVIDENCE_TABLE`, `RS035_RESOURCE_RECIPE_PRESSURE_RULES`, and `RS036_DOSSIER_SAVE_PRESENTATION_RULES`.
  - DOD: 20 packets `P161`-`P180` now cover route-band domain scale, population/authority scale, public route names, transit duration bands, lower Deep Reach office surfaces, worker-name/job/locker/native-localization evidence protocol, resource pressure bands, blue debt quality, vent forge process, escape component route grammar, dossier UI, risk cards, ending records, save knowledge flags and website/wiki spoiler tiers.
  - Rejected: leaving these as open tuning prose without packet IDs, route cards, binding maps and publication surfaces.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS033-RS036 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits or TerminalOS expansion during a parallel Unity pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Kept manual placement flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=94`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=67`, `placement_plan_rows=94`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing new domain/worker/resource/dossier rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS033-RS036 locks.
  - Rejected: preserving stale unresolved prompts for public route names, suboffice names, worker evidence protocol, pressure-failure classes, escape grammar, dossier UI shape and save/profile presentation.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=180 localized_rows=2700`; page exporter wrote 600 new pages; route-card exporter reported `applied_lore_route_cards=174`; source-only audit passed with `packets=180`, `locales=15`, `rows=2700`, `graph_rows=180`, `route_cards=174`, `route_source_rows=174`, `wiki_pages=2700`, `site_pages=2700`, `index_pages=30`, `binding_map_rows=180`, `target_backlog_rows=180`, `manual_policy_rows=94`, `manual_discovery_policy_rows=67`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 92 - Moon atlas / Deep Reach knowledge / final axis / tuning contracts AppliedLore

- [x] Added AppliedLore release sets `RS037_AEGIR_MOON_PUBLIC_ATLAS`, `RS038_DEEP_REACH_TRUE_CAUSE_KNOWLEDGE`, `RS039_FINAL_DECISION_EMOTIONAL_AXIS`, and `RS040_NUMERIC_TUNING_SOURCE_RULES`.
  - DOD: 20 packets `P181`-`P200` now lock moon-name mutability vs route function, HECTON-8 orbital hazard classes, moon role ledger, ephemeris table ownership, Deep Reach true-cause knowledge tiers, liability memo fragment chain, signoff/witness conflicts, public-report lie, final emotional trilemma, no-clean-best-ending rule, and table/native-localization ownership.
  - Rejected: loose prose-only lore, exact orbital/crafting/risk numbers without table owners, and pretending public wiki pages can expose Atlas-basin consequences without spoiler gates.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS037-RS040 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits or TerminalOS expansion during a parallel Unity pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Kept manual placement flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=114`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=87`, `placement_plan_rows=114`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing new moon/liability/final/tuning rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS037-RS040 locks.
  - Rejected: preserving stale unresolved prompts for who knew the true cause, final emotional question, moon name/function boundary, and numeric/localization ownership.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=200 localized_rows=3000`; page exporter wrote indexes with all pages covered; route-card exporter reported `applied_lore_route_cards=194`; source-only audit passed with `packets=200`, `locales=15`, `rows=3000`, `graph_rows=200`, `route_cards=194`, `route_source_rows=194`, `wiki_pages=3000`, `site_pages=3000`, `index_pages=30`, `binding_map_rows=200`, `target_backlog_rows=200`, `manual_policy_rows=114`, `manual_discovery_policy_rows=87`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 93 - Lower signatures / roster / prop evidence / publication protocol AppliedLore

- [x] Added AppliedLore release sets `RS041_DEEP_REACH_LOWER_SIGNATURES`, `RS042_COLONY_ROSTER_AUTHORING_POOL`, `RS043_WORKER_PROP_EVIDENCE_KIT`, and `RS044_PUBLICATION_SPOILER_LOCALIZATION_PROTOCOL`.
  - DOD: 20 packets `P201`-`P220` now lock lower Deep Reach office signature seeds, 72-worker colony roster rule, anchor name sets, seed-role name grammar, locker/triage/route/Marauder/audio prop rules, public article tiers, in-game wiki unlock tiers, audio transcript censorship, art release gates and native-language backlog rules.
  - Rejected: adding new masterminds below the senior Deep Reach chain; random name spam; decorative prop lore; public pages exposing Atlas-basin payload consequences; claiming native localization quality from draft rows.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS041-RS044 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards, runtime binding maps, scene binding targets, image briefs, localized wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits or TerminalOS expansion during a parallel Unity pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=134`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=107`, `placement_plan_rows=134`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing new lower-signature/roster/prop/publication rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS041-RS044 locks.
  - Rejected: preserving stale unresolved prompts for optional lower personnel names, exact roster size, concrete prop variants and publication tier policy.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=220 localized_rows=3300`; page exporter wrote 600 new pages and 30 indexes; route-card exporter reported `applied_lore_route_cards=214`; source-only audit passed with `packets=220`, `locales=15`, `rows=3300`, `graph_rows=220`, `route_cards=214`, `route_source_rows=214`, `wiki_pages=3300`, `site_pages=3300`, `index_pages=30`, `binding_map_rows=220`, `target_backlog_rows=220`, `manual_policy_rows=134`, `manual_discovery_policy_rows=107`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 94 - Ecology / Atlas repair network / hardware evidence AppliedLore

- [x] Added AppliedLore release sets `RS045_PHOTIC_SHELF_NATIVE_ECOLOGY`, `RS046_BRINE_CANYON_ABYSS_ECOLOGY`, `RS047_ATLAS_MAINTENANCE_BIOMECH_LAYER`, and `RS048_HARDWARE_AND_VEHICLE_EVIDENCE_STACK`.
  - DOD: 20 packets `P221`-`P240` now lock shallow native ecology, brine/abyss ecology, Atlas-as-repair-network biomechanics, Black Keel tender limits, drop capsule damage chain, P-63 fabricator authority, pressure suit grades and sonar pinger route beacons.
  - Rejected: aquarium-style fauna lists, a mystical speaking ocean, Atlas as simple villain, clean instant rescue hardware, and decorative equipment without gameplay/evidence purpose.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS045-RS048 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards `RC215`-`RC234`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits or TerminalOS expansion during a parallel Unity pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Corrected source-route drift found during verification.
  - DOD: replaced phantom/stale references with real packet IDs: `P084_VENT_FORGE_GEOTHERMAL_ENGINE`, `P086_AEGIR_RECLAMATION_POOL`, `P173_BLUE_DEBT_SAMPLE_QUALITY`, `P114_ACOUSTIC_PINGER_LINE`; normalized new route-card ending pressures to supported schema values; changed new scene binding candidates to real prefab paths.
  - Rejected: adding duplicate packets to satisfy wrong names, hiding route-card schema violations, or leaving `poi.*` pseudo-paths in prefab candidate columns.
  - Estimate: source determinism fix before bake; runtime cost 0.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=154`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=127`, `placement_plan_rows=154`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing new ecology/Atlas/hardware rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS045-RS048 locks and leave remaining work as numeric table/native-localization/Unity-placement execution, not unresolved core lore.
  - Rejected: preserving stale unresolved prompts for "what is the repair network", shallow/deep ecology, capsule escape parts, and whether hardware evidence belongs in wiki/game/site source.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=240 localized_rows=3600`; page exporter wrote 600 new pages and 30 indexes; route-card exporter reported `applied_lore_route_cards=234`; source-only audit passed with `packets=240`, `locales=15`, `rows=3600`, `graph_rows=240`, `route_cards=234`, `route_source_rows=234`, `wiki_pages=3600`, `site_pages=3600`, `index_pages=30`, `binding_map_rows=240`, `target_backlog_rows=240`, `manual_policy_rows=154`, `manual_discovery_policy_rows=127`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 95 - Contract replay / first-hour micro-script / public pillars / localization audio AppliedLore

- [x] Added AppliedLore release sets `RS049_CONTRACT_SEED_RISK_REWARD_SURFACES`, `RS050_FIRST_HOUR_MICRO_SCRIPT_SURFACES`, `RS051_PUBLIC_SITE_PILLAR_ARTICLES`, and `RS052_LOCALIZATION_GLOSSARY_AUDIO_STYLE`.
  - DOD: 20 packets `P241`-`P260` now lock contract seed pressure cards, first-hour audio/diagnostic/repair/lie/Atlas-trace surfaces, spoiler-tiered public pillar articles, and localization/audio style rules.
  - Rejected: abstract replay difficulty, companion chatter, public articles that spoil final payloads, live translation, and first-hour exposition without playable objects.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS049-RS052 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards `RC235`-`RC254`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits or TerminalOS expansion during a parallel Unity pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Corrected route/source drift during verification.
  - DOD: replaced phantom prereqs with real packet IDs including `P021_BLACK_KEEL_CUSTODY`, `P097_RECOVERY_COMPLIANCE_OFFICE`, `P203_QUARANTINE_REVIEW_GATE_SIGNATURES`, `P091_COLLISION_FRACTURED_MOON`, `P146_DEEP_REACH_PUBLIC_COMBINE`, `P147_AEGIR_CONTINUITY_HOLDINGS`, `P171_RECIPE_TIER_PRESSURE_BANDS`, `P216_PUBLIC_SITE_ARTICLE_TIER_RULES`, `P217_IN_GAME_WIKI_UNLOCK_TIER_RULES`, `P218_AUDIO_TRANSCRIPT_CENSOR_RULES`, `P190_FALSE_PUBLIC_REPORT_PACKET`, and `P036_RETURN_VECTOR_WINDOW`.
  - Rejected: creating duplicate packets for bad names or weakening route-card validation.
  - Estimate: source determinism fix before bake; runtime cost 0.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=174`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=147`, `placement_plan_rows=174`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing new contract/site/localization rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS049-RS052 locks and leave remaining work as numeric tables, Unity placement/UI and native localization/publication polish.
  - Rejected: pretending exact gameplay economy, native localization, final UI layout or scene placement are solved by prose packets.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=260 localized_rows=3900`; page exporter wrote 600 new pages and 30 indexes; route-card exporter reported `applied_lore_route_cards=254`; source-only audit passed with `packets=260`, `locales=15`, `rows=3900`, `graph_rows=260`, `route_cards=254`, `route_source_rows=254`, `wiki_pages=3900`, `site_pages=3900`, `index_pages=30`, `binding_map_rows=260`, `target_backlog_rows=260`, `manual_policy_rows=174`, `manual_discovery_policy_rows=147`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 96 - Numeric bridge / dossier UI / ending records / localization review AppliedLore

- [x] Added AppliedLore release sets `RS053_NUMERIC_AUTHORING_BRIDGE_SURFACES`, `RS054_DOSSIER_CONTRACT_UI_COPY_DECK`, `RS055_ENDING_PAYLOAD_RECORD_SURFACES`, and `RS056_NATIVE_LOCALIZATION_REVIEW_PACK`.
  - DOD: 20 packets `P261`-`P280` now lock table-bridge surfaces, dossier/contract UI copy, ending payload records and native localization review gates.
  - Rejected: solving balance numbers in prose, creating live translation/runtime parser work, or treating final UI layout as finished without Unity/device proof.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS053-RS056 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards `RC255`-`RC274`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits or TerminalOS expansion during a parallel Unity pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Corrected source-route drift during verification.
  - DOD: replaced phantom prereqs with real packet IDs: `P099_MARAUDER_DOSSIER_PERSISTENCE`, `P037_COWARD_EXIT_CHAIN`, `P132_PARTIAL_EXIT_SAME_SEED_RETURN`, and `P135_PUBLIC_LEDGER_LEAK_ROUTE`.
  - Rejected: adding duplicate packets to satisfy wrong names or weakening route-card validation.
  - Estimate: source determinism fix before bake; runtime cost 0.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=194`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=167`, `placement_plan_rows=194`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing new numeric/UI/ending/localization rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS053-RS056 locks and leave remaining work as numeric table values, Unity placement, UI implementation/device proof, native localization proof and publication polish.
  - Rejected: pretending exact gameplay economy, native localization, final UI layout or scene placement are solved by prose packets.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=280 localized_rows=4200`; page exporter reported `skipped_existing=8400 index_pages_written=30`; route-card exporter reported `applied_lore_route_cards=274`; source-only audit passed with `packets=280`, `locales=15`, `rows=4200`, `graph_rows=280`, `route_cards=274`, `route_source_rows=274`, `wiki_pages=4200`, `site_pages=4200`, `index_pages=30`, `binding_map_rows=280`, `target_backlog_rows=280`, `manual_policy_rows=194`, `manual_discovery_policy_rows=167`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 97 - Public articles / artifacts / ecology codex / final descent AppliedLore

- [x] Added AppliedLore release sets `RS057_PUBLIC_SITE_READY_ARTICLE_SECTIONS`, `RS058_IN_GAME_ARTIFACT_AUDIO_SURFACES`, `RS059_ECOLOGY_CODEX_SPECIMEN_CARDS`, and `RS060_FINAL_DESCENT_ROUTE_FRAGMENTS`.
  - DOD: 20 packets `P281`-`P300` now lock public article sections, concrete in-game notes/audio, ecology codex cards and final descent route fragments.
  - Rejected: abstract article advice, generic bestiary entries, companion-style exposition, fantasy-portal final gates and clean-victory ending copy.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS057-RS060 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards `RC315`-`RC334`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits or TerminalOS expansion during a parallel Unity pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Corrected source-route drift during verification.
  - DOD: replaced stale `P083_BRINE_CANYON_LADDER` with real `P083_BRINE_CANYON_ROUTE_LADDER`; confirmed `P100_FINAL_CHOICE_PAYLOAD` exists before rerunning route export.
  - Rejected: adding duplicate packets to satisfy wrong names or weakening route-card validation.
  - Estimate: source determinism fix before bake; runtime cost 0.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=214`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=187`, `placement_plan_rows=214`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing new publication/artifact/codex/final-route rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS057-RS060 locks and leave remaining work as table values, Unity placement, UI implementation/device proof, native localization proof and final publication composition.
  - Rejected: pretending final site composition, image production, UI runtime proof or Unity placement are complete from source prose.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=300 localized_rows=4500`; page exporter reported `applied_lore_pages_written=600 skipped_existing=8400 index_pages_written=30`; route-card exporter reported `applied_lore_route_cards=294`; source-only audit passed with `packets=300`, `locales=15`, `rows=4500`, `graph_rows=300`, `route_cards=294`, `route_source_rows=294`, `wiki_pages=4500`, `site_pages=4500`, `index_pages=30`, `binding_map_rows=300`, `target_backlog_rows=300`, `manual_policy_rows=214`, `manual_discovery_policy_rows=187`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 98 - Table handoff / runtime UI proof / publication composition / placement priority AppliedLore

- [x] Added AppliedLore release sets `RS061_TABLE_VALUE_HANDOFF_CONTRACTS`, `RS062_RUNTIME_UI_PROOF_BACKLOG`, `RS063_PUBLICATION_COMPOSITION_PROOF_PACK`, and `RS064_UNITY_PLACEMENT_PRIORITY_BACKLOG`.
  - DOD: 20 packets `P301`-`P320` now lock table handoff contracts, runtime UI proof cards, public composition gates and Unity placement-priority triage.
  - Rejected: hardcoding balance values in lore, claiming Unity UI implementation, claiming public page assembly/image production, or raw-editing scene YAML.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS061-RS064 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards `RC335`-`RC354`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits or TerminalOS expansion during a parallel Unity pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=234`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=207`, `placement_plan_rows=234`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing new UI/proof/publication/placement-priority rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS061-RS064 locks and leave remaining work as numeric DataMonolith tables, Unity placement, UI implementation/device proof, native localization proof and final public assembly.
  - Rejected: pretending table contracts choose final numbers, UI proof cards implement UI, or publication composition locks produce final marketing pages.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=320 localized_rows=4800`; page exporter reported `applied_lore_pages_written=600 skipped_existing=9000 index_pages_written=30`; route-card exporter reported `applied_lore_route_cards=314`; source-only audit passed with `packets=320`, `locales=15`, `rows=4800`, `graph_rows=320`, `route_cards=314`, `route_source_rows=314`, `wiki_pages=4800`, `site_pages=4800`, `index_pages=30`, `binding_map_rows=320`, `target_backlog_rows=320`, `manual_policy_rows=234`, `manual_discovery_policy_rows=207`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 99 - Carrier ownership / Deep Reach comms / Atlas repair network / false-exit after-action AppliedLore

- [x] Added AppliedLore release sets `RS065_CARRIER_CONTRACT_OWNERSHIP_SURFACES`, `RS066_DEEP_REACH_PRESENT_COMMS_CHAIN`, `RS067_ATLAS_REPAIR_NETWORK_MECHANICS`, and `RS068_FALSE_EXIT_AFTER_ACTION_RECORDS`.
  - DOD: 20 packets `P321`-`P340` now lock claim-pool carrier ownership, present Deep Reach comms, Atlas repair-network mechanisms and false/partial exit after-action records.
  - Rejected: personal rescue ship, omnipotent Deep Reach radio, talking-ocean mysticism, fake fail screens and gear-power replay progression.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS065-RS068 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards `RC355`-`RC374`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits or TerminalOS expansion during a parallel Unity pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Corrected source-route drift during verification.
  - DOD: replaced stale prereqs with real packet IDs: `P021_BLACK_KEEL_CUSTODY`, `P016_AEGIR_HOST_STAR`, `P201_CONTRACT_CONTINUITY_DESK_SIGNATURES`, `P231_CONDUCTIVE_BIOFILM_CABLE_SKIN`, `P232_ACOUSTIC_FILTER_ORGAN_RELAY`, `P233_SHELL_SEALANT_FRACTURE_GROWTH`, `P234_SENSOR_TAGGED_FAUNA`, and `P235_VENT_MICRONODE_NESTS`.
  - Rejected: duplicate packets for wrong names, weaker route validation or hand-editing only generated route output.
  - Estimate: source determinism fix before bake; runtime cost 0.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=254`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=227`, `placement_plan_rows=254`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing new carrier/comms/repair/ending rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS065-RS068 locks and leave remaining work as numeric DataMonolith tables, Unity placement, UI implementation/device proof, native localization proof and final public assembly.
  - Rejected: pretending carrier/comms/repair/exit packets implement runtime UI, animations, scene placement or ending screens.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=340 localized_rows=5100`; page exporter reported `applied_lore_pages_written=600 skipped_existing=9600 index_pages_written=30` on first pass and `skipped_existing=10200 index_pages_written=30` after route corrections; route-card exporter reported `applied_lore_route_cards=334`; source-only audit passed with `packets=340`, `locales=15`, `rows=5100`, `graph_rows=340`, `route_cards=334`, `route_source_rows=334`, `wiki_pages=5100`, `site_pages=5100`, `index_pages=30`, `binding_map_rows=340`, `target_backlog_rows=340`, `manual_policy_rows=254`, `manual_discovery_policy_rows=227`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 100 - Ships / Aegir system / HECTON-8 geology / colony daily-life AppliedLore

- [x] Added AppliedLore release sets `RS069_SHIP_TECH_TRANSIT_ENCYCLOPEDIA`, `RS070_AEGIR_MOON_SYSTEM_ATLAS`, `RS071_HECTON8_GEOLOGY_RESOURCE_FIELDGUIDE`, and `RS072_COLONY_DAILY_LIFE_EVIDENCE_ATLAS`.
  - DOD: 20 packets `P341`-`P360` now lock no-FTL ship/transit encyclopedia, Aegir multi-moon atlas roles, HECTON-8 geology/resource fieldguide and colony daily-life evidence with explicit no-family-hook protagonist guardrail.
  - Rejected: FTL, brown-dwarf darkness, personal rescue yacht, magic ore, mystical speaking ocean, family-revenge motive and generic internal lore notes.
  - Estimate: static content source only; runtime cost 0 until bake/placement.
- [x] Propagated RS069-RS072 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards `RC375`-`RC394`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits or TerminalOS expansion during a parallel Unity pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=274`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=247`, `placement_plan_rows=274`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing new encyclopedia/atlas/geology/colony rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS069-RS072 locks and leave remaining work as numeric DataMonolith tables, celestial numeric tables, Unity placement, UI implementation/device proof, native localization proof and final public assembly.
  - Rejected: pretending encyclopedia packets implement runtime UI, orbital mechanics simulation, Unity placement, native localization or public site final art.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=360 localized_rows=5400`; page exporter reported `applied_lore_pages_written=600 skipped_existing=10200 index_pages_written=30`; route-card exporter reported `applied_lore_route_cards=354`; source-only audit passed with `packets=360`, `locales=15`, `rows=5400`, `graph_rows=360`, `route_cards=354`, `route_source_rows=354`, `wiki_pages=5400`, `site_pages=5400`, `index_pages=30`, `binding_map_rows=360`, `target_backlog_rows=360`, `manual_policy_rows=274`, `manual_discovery_policy_rows=247`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 101 - Escape components / player dossier / Deep Reach proof / final payload receivers AppliedLore

- [x] Added AppliedLore release sets `RS073_ESCAPE_ASCENT_ENGINEERING_COMPONENTS`, `RS074_PLAYER_EX_DEEP_REACH_PROFESSIONAL_DOSSIER`, `RS075_DEEP_REACH_LIE_PHYSICAL_PROOF_CHAIN`, and `RS076_ATLAS_FINAL_PAYLOAD_RECEIVER_PROTOCOLS`.
  - DOD: 20 packets `P361`-`P380` now lock concrete escape/ascent component surfaces, the ex-Deep-Reach professional player dossier, Deep Reach liability proof chain and final payload receiver protocols.
  - Rejected: convenient rescue, single generic repair kit, family-revenge protagonist, cartoon Deep Reach guilt, clean final endings and fake fail screens.
  - Estimate: static content source only; runtime cost 0 until bake/placement/UI work.
- [x] Propagated RS073-RS076 through source content surfaces.
  - DOD: generated packet bundles, manifests, release docs, evidence graphs, route cards `RC395`-`RC414`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits, ending UI implementation or TerminalOS expansion during this lore pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Corrected source-route drift during verification.
  - DOD: route-card exporter rejected stale `P070_RETURN_VECTOR_WINDOW`; replaced it with real `P036_RETURN_VECTOR_WINDOW`, regenerated RS073-RS076 and reran importer/route export.
  - Rejected: weakening route validation or duplicating a packet to satisfy a bad prereq.
  - Estimate: source determinism fix before bake; runtime cost 0.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=294`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=267`, `placement_plan_rows=294`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing escape/dossier/proof/final-payload rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS073-RS076 locks and leave remaining work as numeric DataMonolith tables, Unity placement, UI implementation/device proof, native localization proof, actual ending implementation/audio/cinematics and final public assembly.
  - Rejected: pretending source packets implement runtime UI, actual endings, native localization, audio, cinematics, scene placement or public web assembly.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=380 localized_rows=5700`; page exporter reported `applied_lore_pages_written=600 skipped_existing=10800 index_pages_written=30`; route-card exporter reported `applied_lore_route_cards=374`; source-only audit passed with `packets=380`, `locales=15`, `rows=5700`, `graph_rows=380`, `route_cards=374`, `route_source_rows=374`, `wiki_pages=5700`, `site_pages=5700`, `index_pages=30`, `binding_map_rows=380`, `target_backlog_rows=380`, `manual_policy_rows=294`, `manual_discovery_policy_rows=267`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 102 - Long campaign / POI kits / replay seed / public module AppliedLore

- [x] Added AppliedLore release sets `RS077_LONG_CAMPAIGN_ACT_SPINE`, `RS078_MAJOR_POI_EVIDENCE_KITS`, `RS079_REPLAY_CONTRACT_SEED_FAMILIES`, and `RS080_PUBLIC_WIKI_ARTICLE_MODULES`.
  - DOD: 20 packets `P381`-`P400` now lock long campaign act gates, major physical POI kits, replay contract seed families and spoiler-controlled public/wiki article modules.
  - Rejected: another abstract lore essay; inherited-power roguelite framing; public copy that spoils Atlas receiver endings; generic POI names without route/evidence function.
  - Estimate: static content source only; runtime cost 0 until bake/placement/UI/publication work.
- [x] Propagated RS077-RS080 through source content surfaces.
  - DOD: generated packet JSON, release manifests/docs, evidence graphs, route cards `RC415`-`RC434`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits, actual web assembly or ending UI implementation during this content pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Corrected source-route drift during verification.
  - DOD: route-card exporter rejected stale prereqs; replaced `P336_MATERIAL_RECEIPT_AUDIT_RECORD` with `P336_MATERIAL_EXIT_RECEIPT_AUDIT`, and `P342_BEAM_SAIL_PELLET_LANE_TRANSIT` with `P342_BEAM_SAIL_AND_PELLET_LANE`.
  - Rejected: duplicate packets for wrong names or weakening route-card validation.
  - Estimate: source determinism fix before bake; runtime cost 0.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=314`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=287`, `placement_plan_rows=314`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing campaign/POI/replay/public rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS077-RS080 locks and leave remaining work as numeric tables, Unity placement, runtime UI, native localization, public assembly, celestial numeric tables and actual endings.
  - Rejected: pretending campaign act packets implement missions, scene placement, final UI, images, audio or public website assembly.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=400 localized_rows=6000`; page exporter reported `applied_lore_pages_written=600 skipped_existing=11400 index_pages_written=30` on first pass and `applied_lore_pages_written=0 skipped_existing=12000 index_pages_written=30` after route corrections; route-card exporter reported `applied_lore_route_cards=394`; source-only audit passed with `packets=400`, `locales=15`, `rows=6000`, `graph_rows=400`, `route_cards=394`, `route_source_rows=394`, `wiki_pages=6000`, `site_pages=6000`, `index_pages=30`, `binding_map_rows=400`, `target_backlog_rows=400`, `manual_policy_rows=314`, `manual_discovery_policy_rows=287`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 103 - Worker dossiers / Deep Reach memos / fauna grammar / wiki navigation AppliedLore

- [x] Added AppliedLore release sets `RS081_COLONY_ANCHOR_WORKER_DOSSIERS`, `RS082_DEEP_REACH_ARTIFACT_MEMO_PACK`, `RS083_FAUNA_ENCOUNTER_GRAMMAR`, and `RS084_SITE_WIKI_NAVIGATION_CLUSTERS`.
  - DOD: 20 packets `P401`-`P420` now lock concrete worker dossiers, Deep Reach memo artifacts, fauna encounter grammar and site/wiki navigation clusters.
  - Rejected: family-revenge colony stakes, villain monologues, boss-list fauna, mystical speaking ocean and unordered public/wiki packet dumps.
  - Estimate: static content source only; runtime cost 0 until bake/placement/UI/publication work.
- [x] Propagated RS081-RS084 through source content surfaces.
  - DOD: generated packet JSON, release manifests/docs, evidence graphs, route cards `RC435`-`RC454`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits, actual web assembly, native localization claim or ending UI implementation during this content pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Corrected source-route drift during verification.
  - DOD: route-card exporter rejected stale `P352_BRINE_DENSITY_LADDER_ROUTES`; corrected it to `P352_BRINE_CANYON_DENSITY_LADDER_GUIDE`. Also corrected stale geology prereqs to `P351_DROWNED_CRUST_STRATA_GUIDE`, `P353_VENT_FORGE_FIELD_PROCESS_GUIDE`, and `P355_PRESSURE_GLASS_AND_SEALANT_GUIDE`. Runtime audit rejected unsupported `ending_pressure=spoiler`; changed RS084 route pressure to valid `truth`.
  - Rejected: weakening route/audit validation, duplicating old packet names or using unsupported ending-pressure categories.
  - Estimate: source determinism fix before bake; runtime cost 0.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=334`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=307`, `placement_plan_rows=334`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing worker/memo/fauna/wiki-navigation rows into terminal slots or raw-editing scene YAML.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS081-RS084 locks and leave remaining work as numeric tables, Unity placement, runtime UI, native localization, public assembly, celestial numeric tables and actual endings.
  - Rejected: pretending source packets implement runtime UI, final localization, scene placement, images, audio, ending cinematics or public website assembly.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=420 localized_rows=6300`; page exporter reported `applied_lore_pages_written=600 skipped_existing=12000 index_pages_written=30` on first pass and `applied_lore_pages_written=0 skipped_existing=12600 index_pages_written=30` after route corrections; route-card exporter reported `applied_lore_route_cards=414`; source-only audit passed with `packets=420`, `locales=15`, `rows=6300`, `graph_rows=420`, `route_cards=414`, `route_source_rows=414`, `wiki_pages=6300`, `site_pages=6300`, `index_pages=30`, `binding_map_rows=420`, `target_backlog_rows=420`, `manual_policy_rows=334`, `manual_discovery_policy_rows=307`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 104 - Ephemeris bands / resource artifacts / presentation rules / transcript seeds AppliedLore

- [x] Added AppliedLore release sets `RS085_CELESTIAL_EPHEMERIS_PUBLIC_BANDS`, `RS086_RESOURCE_ECONOMY_ARTIFACTS`, `RS087_PDA_CODEX_PRESENTATION_RULES`, and `RS088_AUDIO_TRANSCRIPT_ARTICLE_SEEDS`.
  - DOD: 20 packets `P421`-`P440` now lock public ephemeris bands, resource economy artifacts, PDA/scanner/terminal/dossier presentation rules and audio transcript/article seeds.
  - Rejected: exact orbital constants in prose, generic loot receipts, claiming runtime UI implementation from source strings, and performance/audio claims without routing work.
  - Estimate: static content source only; runtime cost 0 until bake/placement/UI/audio/publication work.
- [x] Propagated RS085-RS088 through source content surfaces.
  - DOD: generated packet JSON, release manifests/docs, evidence graphs, route cards `RC455`-`RC474`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits, actual UI/audio implementation, native localization claim or website assembly during this content pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Handled verification races without weakening validation.
  - DOD: importer completed before final route export; page exporter completed before final source audit. Earlier exporter/audit failures were caused by running tools in parallel against stale generated CSV/index files, then passed sequentially.
  - Rejected: weakening route validation, duplicating packet IDs, or pretending a tool race was a data defect.
  - Estimate: source determinism proof only; runtime cost 0.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=354`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=327`, `placement_plan_rows=354`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing ephemeris/resource/UI/transcript rows into TerminalOS slots or raw-editing scene YAML during a parallel Unity pass.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS085-RS088 locks and leave remaining work as numeric tables, exact ephemeris constants, Unity placement, runtime UI/audio, native localization, public assembly and actual endings.
  - Rejected: pretending source packets implement final celestial tables, runtime UI, audio routing, scene placement, ending cinematics or public website assembly.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=440 localized_rows=6600`; page exporter reported `applied_lore_pages_written=600 skipped_existing=12600 index_pages_written=30` on first pass and `applied_lore_pages_written=0 skipped_existing=13200 index_pages_written=30` after final export; route-card exporter reported `applied_lore_route_cards=434`; source-only audit passed with `packets=440`, `locales=15`, `rows=6600`, `graph_rows=440`, `route_cards=434`, `route_source_rows=434`, `wiki_pages=6600`, `site_pages=6600`, `index_pages=30`, `binding_map_rows=440`, `target_backlog_rows=440`, `manual_policy_rows=354`, `manual_discovery_policy_rows=327`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 105 - Table drafts / Unity placement briefs / localization QA / public longform AppliedLore

- [x] Added AppliedLore release sets `RS089_NUMERIC_GAMEPLAY_TABLE_VALUE_DRAFTS`, `RS090_UNITY_PLACEMENT_SCENE_BRIEFS`, `RS091_NATIVE_LOCALIZATION_AND_ACCESSIBILITY_QA_BRIEFS`, and `RS092_PUBLIC_SITE_LONGFORM_ARTICLE_BRIEFS`.
  - DOD: 20 packets `P441`-`P460` now lock table-facing value-band drafts, Unity scene placement briefs, native localization/accessibility QA briefs and public longform article briefs.
  - Rejected: choosing final numeric balance in lore prose, raw-editing Unity scenes, claiming native localization certification from draft rows, or writing public articles without spoiler/runtime-claim boundaries.
  - Estimate: static content source only; runtime cost 0 until bake/placement/UI/publication work.
- [x] Propagated RS089-RS092 through source content surfaces.
  - DOD: generated packet JSON, release manifests/docs, evidence graphs, route cards `RC475`-`RC494`, runtime binding maps, scene binding targets, image briefs, localized in-game wiki pages, external-site pages, localized indexes, source CSV rows and `H8AppliedLoreHashes.cs` constants.
  - Rejected: runtime markdown parsing, live translation, scene search, Unity scene edits, DataMonolith bake or website assembly during this content pass.
  - Estimate: no hot-path lookup, no runtime parser, no live localization.
- [x] Corrected stale source-route IDs during verification.
  - DOD: route-card exporter rejected stale prereqs; corrected `P385_ATLAS_PAYLOAD_RESOLUTION_ACT_GATE` to `P385_ATLAS_BASIN_PAYLOAD_ACT`, POI kit prereqs to `P388_BRINE_CANYON_PUMP_CATHEDRAL_POI_KIT`, `P389_EVACUATION_QUEUE_TERMINAL_POI_KIT`, `P390_ATLAS_SERVICE_BASIN_POI_KIT`, and RU review prereq to `P276_RU_NATIVE_REVIEW_LOCK`.
  - Rejected: weakening route-card validation, duplicating old packet names or hand-editing generated CSV only.
  - Estimate: source determinism fix before bake; runtime cost 0.
- [x] Kept new manual rows flattened into `NarrativeDiscovery` backlog.
  - DOD: source audit reports `manual_policy_rows=374`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=347`, `placement_plan_rows=374`, and `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: forcing table/placement/localization/article rows into TerminalOS slots or raw-editing scene YAML during a parallel Unity pass.
  - Estimate: content source remains bake-ready; scene runtime cost 0 in this pass.
- [x] Synced canon memory and open-question list.
  - DOD: `Canon_Locks.md`, `Lore_Bible.md`, `Open_Questions.md`, `Narrative_Crystallization.md`, AppliedContent README and binding-map README now reflect RS089-RS092 locks and leave remaining work as exact numeric tables, Unity placement, runtime UI/audio, native localization certification, public web assembly, exact ephemeris constants and actual endings.
  - Rejected: pretending source briefs implement runtime UI, actual scene placement, native localization, audio, ending cinematics or public website assembly.
  - Estimate: docs/content only.
- [x] Verified source pipeline after content generation.
  - DOD: importer reported `applied_lore_packets=460 localized_rows=6900`; page exporter reported `applied_lore_pages_written=600 skipped_existing=13200 index_pages_written=30`; route-card exporter reported `applied_lore_route_cards=454`; source-only audit passed with `packets=460`, `locales=15`, `rows=6900`, `graph_rows=460`, `route_cards=454`, `route_source_rows=454`, `wiki_pages=6900`, `site_pages=6900`, `index_pages=30`, `binding_map_rows=460`, `target_backlog_rows=460`, `manual_policy_rows=374`, `manual_discovery_policy_rows=347`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: Unity/DataMonolith bake claim; no `static_data.h8bin` rebuild was run in this lore-only pass.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched except regenerated hash constants from the existing importer.

## Control Pass 106 - Narrative Apex verifier hardening pass

- [x] Hardened `H8NarrativeApexVerifier` without adding a parallel utility.
  - DOD: existing verifier now includes `Update`/`LateUpdate` as hot roots, catches hot string concatenation, catches `WaitForCompletion`, expands LINQ hot-call coverage, scans runtime struct layout literal sizes for 8-byte ABI drift, and scans project `.meta` integrity for orphan/missing source meta files.
  - Rejected: new standalone scanner class, runtime checks, broad gameplay refactor, or another markdown-only report.
  - Estimate: editor-only proof surface; runtime cost 0.
- [x] Rechecked hot dependency shape in scoped Narrative/AppliedLore owners.
  - DOD: scoped static scan reports `hot_scope_findings=0` for `GlobalRegistry.Get<T>()`, `GetComponent`/`TryGetComponent`/`GetComponents`, sync completion, LINQ/string allocation tokens inside hot roots.
  - Rejected: changing the two cold `TryGetComponent` call sites in `MessageTerminal` and `PDAEncyclopediaStreamer`; they are outside hot roots.
  - Estimate: no gameplay truth, DTO layout, save identity or SignalBus payload change.
- [x] Rechecked DataVault write-lock shape in `MetaCampaignService`.
  - DOD: direct write lock acquire helpers each contain one `TryAcquireWriteLock`, one failure `ReleaseWriteLock`, and one `finally`; caller-side writes release via `finally`; no asset/resource/pathfinding calls found in those locked write scopes.
  - Rejected: replacing existing helper-transfer pattern that the Apex verifier already supports.
  - Estimate: no frame cost; lock proof only.
- [x] Verified without build spam.
  - DOD: local source balance check reports `syntax_balance_ok stack=0 lines=4280`; `git diff --check` reports only CRLF normalization warning; `.meta` scan reports `orphan_meta_count=0` and `missing_source_meta_count=0`; source-only AppliedLore audit passes with `packets=460`, `rows=6900`, `route_cards=454`, `scene_terminal_os_runtime_verified_slots=27`.
  - Rejected: `dotnet build`, Unity refresh, DataMonolith bake, scene edits, runtime markdown parser, live translation.
  - Estimate: no `dotnet build`, no Unity compile, no runtime gameplay code touched.

## Control Pass 107 - AppliedLore layout proof and streamed Apex hygiene

- [x] Fortified `H8AppliedLoreWorldImpactRecord` layout contract in the existing runtime facade.
  - DOD: record now exposes `SizeBytes=24`, explicit tail padding at offsets 17/18/20, and cold `H8AppliedLoreRuntime.ValidateRuntimeLayout()` using `UnsafeUtility.SizeOf<H8AppliedLoreWorldImpactRecord>()` with 8-byte multiple proof.
  - Rejected: new DTO wrapper, new helper utility, runtime parser, changing SignalBus payloads, changing gameplay truth, or moving the record to a parallel subsystem.
  - Estimate: hot-path cost 0; validation method is cold/static.
- [x] Bound the new layout proof into `H8NarrativeApexVerifier`.
  - DOD: Apex verifier now requires world-impact `SizeBytes`, padding offsets and `UnsafeUtility.SizeOf<T>()` proof, and streams project `.meta` scans with `Directory.EnumerateFiles` instead of allocating full path arrays.
  - Rejected: editor report-only change without C# gate, `dotnet build`, Unity refresh, scene mutation, or broad refactor.
  - Estimate: runtime cost 0; editor verifier peak path-array allocation reduced.
- [x] Reverified scoped source safety.
  - DOD: balance check reports `syntax_balance_ok` for `H8AppliedLoreRuntime.cs` and `H8NarrativeApexVerifier.cs`; hot-scope scan reports `hot_scope_findings=0`; `.meta` scan reports `orphan_meta_count=0 missing_source_meta_count=0`; DataVault proof reports all three `MetaCampaignService` write-lock helpers acquire/release/finally with `heavy_tokens=0`; AppliedLore audit passes with `packets=460`, `rows=6900`, `blob_bytes=8212352`, `route_cards=454`, `wiki_pages=6900`, `site_pages=6900`.
  - Rejected: build spam, Unity compile, DataMonolith bake, native localization certification or public-site assembly claims.
  - Estimate: no `dotnet build`; source-only verification.

## Control Pass 108 - Central DataMonolith AppliedLore layout gate

- [x] Connected the AppliedLore world-impact DTO to the central `H8DataLayoutAudit.ValidateBlittableSizes()` route.
  - DOD: `H8DataMonolithTypes.cs` now checks `UnsafeUtility.SizeOf<H8AppliedLoreWorldImpactRecord>() == H8AppliedLoreWorldImpactRecord.SizeBytes` and 8-byte alignment.
  - Rejected: forcing the 24-byte non-section DTO into the 16-byte DataMonolith section alignment rule, adding a wrapper, or changing SignalBus/runtime truth layout.
  - Estimate: hot-path cost 0; cold audit only.
- [x] Extended `H8NarrativeApexVerifier` scope to prove the central audit route.
  - DOD: Apex scope now includes `H8DataMonolithTypes.cs`; world-impact route requires central audit proofs in addition to local padding and `ValidateRuntimeLayout()`.
  - Rejected: separate scanner or markdown-only assertion.
  - Estimate: editor-only proof gate.
- [x] Reverified after scope expansion.
  - DOD: tokenizer balance OK for `H8AppliedLoreRuntime.cs`, `H8DataMonolithTypes.cs`, `H8NarrativeApexVerifier.cs`; hot-scope scan reports `hot_scope_findings=0`; `.meta` scan `orphan_meta_count=0 missing_source_meta_count=0`; DataVault helper proof `lock_proof_bad=0`; AppliedLore audit passes with `packets=460`, `rows=6900`, `blob_bytes=8212352`, `route_cards=454`.
  - Rejected: `dotnet build`, Unity compile, DataMonolith bake, scene edit.
  - Estimate: no build spam; source-only verification.

## Control Pass 109 - AppliedLore UTF8 record-copy flattening

- [x] Flattened AppliedLore UTF8 surface lookup to pass packet records by readonly reference.
  - DOD: `H8AppliedLoreRuntime.TryGetUtf8` now routes both packet-hash and record overloads through `TryGetUtf8FromRecord(in H8AppliedLorePacketRecord record, ...)`; proof count reports `utf8_pass_by_ref_calls=2`, `in_params=4`, `total=6`.
  - Rejected: new helper class, new DTO wrapper, reverting another agent's localized-span route, or touching gameplay/UI scene code.
  - Estimate: removes one avoidable packet-record copy per UTF8 lookup; no heap allocation and no hot root added.
- [x] Bound the pass-by-ref requirement into the existing Apex verifier.
  - DOD: `H8NarrativeApexVerifier` now reports and gates `applied_lore_utf8_pass_by_ref_proofs`; route fails if proof count drops below 4.
  - Rejected: markdown-only assertion or standalone scanner.
  - Estimate: editor-only proof gate; runtime cost 0.
- [x] Reverified source-only after the C# polish.
  - DOD: tokenizer balance OK for `H8AppliedLoreRuntime.cs`, `H8DataMonolithTypes.cs`, `H8NarrativeApexVerifier.cs`; touched runtime files report `hot_roots=0` and `raw_forbidden_tokens=0`; `MetaCampaignService` write helpers report acquire/release/finally with `heavy_tokens=0`; `git diff --check` clean; AppliedLore source audit passes with `packets=460`, `rows=6900`, `route_cards=454`.
  - Rejected: `dotnet build` because existing `dotnet`/`csc` processes were already active; Unity compile, DataMonolith bake, scene edit.
  - Estimate: source-only validation; no new process spam.

## Control Pass 110 - Unity asset meta hygiene closure

- [x] Added missing Unity `.meta` companions for four existing baker assets.
  - DOD: added `.meta` files for `GeologicalStrataBaker1724.compute`, `HullCavitationBaker1722.compute`, `GeologicalStrataBaker1724.cs`, and `HullCavitationBaker1722.cs`; source files themselves were not edited.
  - Rejected: editing the untracked baker sources, deleting files, letting Unity generate unstable GUIDs on import, or touching scene YAML.
  - Estimate: runtime cost 0; asset identity drift prevented.
- [x] Reverified hygiene and source state after the meta pass.
  - DOD: `.meta` scan reports `orphan_meta_count=0 missing_source_meta_count=0`; GUID search reports each new GUID once; `git diff --check` clean for the new meta files; AppliedLore source audit still passes with `packets=460`, `rows=6900`, `route_cards=454`; tokenizer balance still OK for the three touched C# files.
  - Rejected: `dotnet build` because no C# source changed in this pass and build proof is not required for meta-only identity repair.
  - Estimate: no compile, no Unity import, no DataMonolith bake.

## Control Pass 111 - AppliedLore UTF8 single-owner route

- [x] Removed the duplicated UTF8 surface selector from the AppliedLore runtime facade.
  - DOD: `H8AppliedLoreRuntime` now delegates both UTF8 lookup overloads to the existing owner route `H8StaticDataArena.TryGetAppliedLoreUtf8(in record, surface, out utf8Bytes)`; facade duplicate selector proof reports `facade_duplicate_selectors=0`.
  - Rejected: new helper class, new DTO wrapper, editing another agent's `H8StaticDataArena` content, or keeping a parallel switch in the facade.
  - Estimate: one local selector branch chain removed from the facade path; hot roots remain 0.
- [x] Bound single-owner route proof into the Apex verifier.
  - DOD: `H8NarrativeApexVerifier` now includes `H8StaticDataArena.cs` in scope, requires central arena pass-by-ref UTF8 proof, and fails the world-impact route if facade duplicate selectors return.
  - Rejected: markdown-only proof, standalone scanner, or widening runtime dependencies.
  - Estimate: editor-only proof gate; runtime cost 0.
- [x] Reverified source/static safety after route flattening.
  - DOD: pass-by-ref proof reports `runtime_arena_utf8_calls=2`, `arena_methods=1`, `arena_in_record_params=1`, `total_pass_by_ref_proofs=4`; touched runtime files report `hot_roots=0 raw_forbidden_tokens=0`; tokenizer balance OK for four scoped C# files; `.meta` hygiene reports `orphan_meta_count=0 missing_source_meta_count=0`; DataVault helper proof remains acquire/release/finally with `heavy_tokens=0`; AppliedLore audit passes with `packets=460`, `rows=6900`, `route_cards=454`.
  - Rejected: `dotnet build`, Unity compile, DataMonolith bake, Unity import, scene edit.
  - Estimate: source-only verification; no compiler spam.

## Control Pass 112 - Expanded Unity asset meta proof surface

- [x] Extended the existing Narrative Apex `.meta` gate beyond scripts and shaders.
  - DOD: `H8NarrativeApexVerifier` now uses one in-class `SourceMetaRequiredExtensions` table covering C# asmdefs, shaders, compute, prefabs, assets, materials, scenes, animation assets, UI assets, textures, models, audio and DataMonolith source text/CSV/JSON/bytes files.
  - Rejected: new scanner class, Unity import refresh, deleting assets, leaving prefab/material/texture identity drift outside the verifier, or scanning the Assets tree once per extension.
  - Estimate: runtime cost 0; editor-only single-pass streaming scan.
- [x] Verified expanded source/meta hygiene without compiler spam.
  - DOD: static scan reports `source_meta_extensions=27`, `source_meta_files_scanned=11908`, `missing_source_meta_files=0`, `meta_files_scanned=13887`, `orphan_meta_files=0`; tokenizer balance remains OK for `H8NarrativeApexVerifier.cs`; AppliedLore source audit remains green with `packets=460`, `rows=6900`, `route_cards=454`, `wiki_pages=6900`, `site_pages=6900`.
  - Rejected: `dotnet build` because active `dotnet` processes were present; no Unity compile, DataMonolith bake, scene edit or generated content rewrite.
  - Estimate: no runtime frame change; identity proof only.

## Control Pass 113 - Prologue black-box DataVault lock flattening

- [x] Hoisted prologue black-box telemetry snapshots out of the DataVault write lock.
  - DOD: `AwaitableDropSequenceDirector.RecordStage` now resolves `CurrentFrame`, sequence, orbital speed, orbital distance, and ring-buffer cursor before `TryAcquireWriteLock`; the locked block only validates the native buffer, fills a local DTO, writes one slot, advances cursor, and releases in `finally`.
  - Rejected: new telemetry subsystem, helper wrapper, extra DataVault handle, moving black-box ownership, or changing prologue gameplay truth.
  - Estimate: 0 allocations; write-lock hold time reduced to primitive assignment/native-array slot write only.
- [x] Added a permanent Apex gate for the prologue lock shape.
  - DOD: `H8NarrativeApexVerifier` now runs `ScanPrologueBlackBoxDataVaultRoute` and reports `prologue_blackbox_write_locks`, `prologue_blackbox_release_finally`, `prologue_blackbox_hoisted_telemetry`, and `prologue_blackbox_heavy_inside_lock`; the route fails as a lock finding if heavy telemetry returns inside the write-lock `try`.
  - Rejected: one-off grep proof, markdown-only assertion, separate scanner file, or broad lock-system refactor.
  - Estimate: runtime cost 0; editor-only AST/source gate.
- [x] Reverified source safety without build spam.
  - DOD: tokenizer balance OK for `AwaitableDropSequenceDirector.cs` and `H8NarrativeApexVerifier.cs`; static proof reports `prologue_blackbox_write_locks=1`, `prologue_blackbox_release_finally=1`, `prologue_blackbox_hoisted_telemetry=6`, `prologue_blackbox_heavy_inside_lock=0`; hot-token scan in the prologue file found no `GlobalRegistry.Get`, `GetComponent`, LINQ, `WaitForCompletion`, or managed container allocation; `git diff --check` has no whitespace errors; AppliedLore source audit passes with `packets=460`, `rows=6900`, `route_cards=454`.
  - Rejected: `dotnet build` because active `dotnet` PID 47240 was present; no Unity compile/import, DataMonolith bake, scene edit, generated lore rewrite, or process kill.
  - Estimate: source-only verification; no compiler process launched.

## Control Pass 114 - PDA telemetry redundant vault read removal

- [x] Removed the redundant PDA telemetry ring read from the visual-sync telemetry write path.
  - DOD: `PDAEncyclopediaStreamer.RecordTelemetry` no longer calls `TryReadVaultBuffer(in _telemetryHandle, ...)` before acquiring the same telemetry ring for write; the locked block validates `telemetry.Length < TelemetryFrameCount` before writing.
  - Rejected: new telemetry manager, new DataVault handle, deleting black-box telemetry, changing PDA stream truth, or broad UI refactor.
  - Estimate: removes one read-only vault lookup per visible PDA telemetry frame; no heap allocation and no gameplay truth change.
- [x] Removed the streaming-frame runtime-state reread after `WriteRuntimeState`.
  - DOD: `WriteRuntimeState` now returns `unlockedCountSnapshot` through an `out uint`; `LateFrameTick` passes it into `RecordTelemetry`; `RecordTelemetry` keeps one fallback runtime-state read only for locked/complete paths that do not write state first.
  - Rejected: caching unlocked count globally, adding a telemetry DTO wrapper, removing locked/complete telemetry, or trusting stale UI state.
  - Estimate: removes one extra runtime-state vault read from the normal streaming PDA telemetry frame.
- [x] Added an Apex verifier gate for the PDA telemetry route.
  - DOD: `H8NarrativeApexVerifier` now runs `ScanPdaTelemetryVaultRoute` and reports `pda_telemetry_write_locks`, `pda_telemetry_release_finally`, `pda_telemetry_redundant_readonly`, `pda_telemetry_write_size_proofs`, `pda_telemetry_runtime_fallback_reads`, and `pda_telemetry_streaming_snapshot_passes`.
  - Rejected: one-off grep, markdown-only assertion, standalone scanner file, or widening runtime dependencies.
  - Estimate: runtime cost 0; editor-only source gate.
- [x] Reverified source safety without compiler spam.
  - DOD: tokenizer balance OK for `PDAEncyclopediaStreamer.cs` and `H8NarrativeApexVerifier.cs`; static proof reports `lateframe_streaming_snapshot_write_calls=1`, `lateframe_streaming_snapshot_record_calls=1`, `write_runtime_state_out_params=1`, `record_telemetry_write_locks=2`, `record_telemetry_release_finally=2`, `record_telemetry_redundant_readonly=0`, `record_telemetry_runtime_fallback_reads=1`, `record_telemetry_size_proofs=1`, `pda_telemetry_hot_tokens=0`; `git diff --check` has no whitespace errors; AppliedLore source audit passes with `packets=460`, `rows=6900`, `route_cards=454`.
  - Rejected: `dotnet build` because active `dotnet` PID 47240 was present; no Unity compile/import, DataMonolith bake, generated content rewrite, scene edit, or process kill.
  - Estimate: source-only verification; no compiler process launched.

## Control Pass 115 - TerminalOS telemetry snapshot/write separation

- [x] Hoisted TerminalOS read snapshots before telemetry ring writes.
  - DOD: `TerminalOsRuntime.RecordTelemetry` now computes `layoutHashSnapshot` before opening `_telemetryRingHandle`; the telemetry ring path guards `telemetryRing.Length == 0`, writes through a clamped `telemetryIndex`, and advances cursor from that clamped index.
  - Rejected: new telemetry manager, new DTO, changing DataVault ownership, or touching terminal presentation semantics.
  - Estimate: removes nested/overlapped vault buffer resolution during terminal black-box slot writes; 0 heap bytes.
- [x] Flattened decryption telemetry write ordering.
  - DOD: `RecordDecryptionTelemetry` now reads puzzle/terminal snapshots first, then opens `_decryptionTelemetryRingHandle` only for the final write; decryption cursor advance uses the clamped write index.
  - Rejected: deleting decryption telemetry, changing puzzle truth, or adding a managed queue.
  - Estimate: shorter telemetry-ring residency; safer cursor drift handling.
- [x] Flattened TerminalOS input telemetry fault/write ordering.
  - DOD: `RecordTerminalInputTelemetry` in `TerminalOsRuntime_TerminalProjection.cs` now computes `projectionFaults` before opening `_terminalInputTelemetryRingHandle`; the ring path guards zero length, writes through a clamped `telemetryIndex`, and advances cursor from that index.
  - Rejected: new input telemetry manager, managed staging queue, changing terminal command ownership, or deleting black-box input telemetry.
  - Estimate: fault composition is outside the DataVault ring write window; 0 heap bytes.
- [x] Added an Apex verifier gate for TerminalOS telemetry route shape.
  - DOD: `H8NarrativeApexVerifier` now checks layout-hash hoist, telemetry ring open-after-snapshot proof, ring length guards, decryption snapshot-before-ring order, input fault-before-ring order, and cursor clamp usage.
  - Rejected: standalone scanner, markdown-only proof, or Unity/runtime bake claim.
  - Estimate: runtime cost 0; editor/source proof only.
- [x] Reverified source safety without compiler spam.
  - DOD: token balance OK for `TerminalOsRuntime.cs`, `TerminalOsRuntime_TerminalProjection.cs`, and `H8NarrativeApexVerifier.cs`; static proof reports `terminal_layout_hoists=2`, `terminal_ring_after_snapshot_tokens=3`, `terminal_ring_length_guards=2`, `decryption_snapshot_before_ring=1`, `decryption_cursor_clamps=2`, `input_faults_before_ring=1`, `input_cursor_clamps=2`, `verifier_terminal_gate_tokens=9`, and hot forbidden tokens `0`; AppliedLore source audit remains green with `packets=460`, `rows=6900`, `route_cards=454`; `git diff --check` has no whitespace errors.
  - Rejected: `dotnet build`, Unity compile/import, DataMonolith bake, scene edit, generated content rewrite, or process kill.
  - Estimate: source-only verification; no compiler process launched.

## Control Pass 116 - PDA black-box dump vault-read flattening

- [x] Flattened PDA black-box dump telemetry reads.
  - DOD: `PDAEncyclopediaStreamer.WriteBlackBoxDump` now snapshots `_telemetryHandle` once before serializing the 300-frame dump; the removed `TryReadTelemetryDumpEntry` helper can no longer re-resolve the DataVault telemetry ring per row.
  - Rejected: new dump manager, managed staging list, deleting PDA black-box telemetry, or changing the PDA runtime truth route.
  - Estimate: fault-path cost only; removes up to 300 repeated vault read attempts per PDA dump; 0 us/frame steady state.
- [x] Routed PDA dump payload through the native fault dump owner API.
  - DOD: raw `new NativeArray<byte>(...)` in `WriteBlackBoxDump` is replaced by `NativeFaultDumpWriter.CreateTransientPayload(..., NativeArrayOptions.ClearMemory)` plus `DisposeTransientPayload` in `finally`.
  - Rejected: untracked temp allocation ownership, relying on manual `Dispose`, or leaving the header padding uninitialized.
  - Estimate: no hot-path cost; fault payload remains Temp and explicitly tracked.
- [x] Added Apex verifier coverage for the PDA dump route.
  - DOD: `H8NarrativeApexVerifier.ScanPdaTelemetryVaultRoute` now reports `pda_blackbox_dump_single_snapshots`, `pda_blackbox_dump_per_row_reads`, `pda_blackbox_dump_transient_payloads`, and `pda_blackbox_dump_raw_payload_allocs`; route fails unless the dump has one snapshot route, zero per-row vault reads, transient create/dispose, and zero raw payload allocs.
  - Rejected: one-off grep, markdown-only proof, standalone scanner, or runtime assertion.
  - Estimate: editor/source-only proof; runtime cost 0.
- [x] Reverified source safety without compiler spam.
  - DOD: Unity MCP `validate_script` reports 0 errors/0 warnings for `PDAEncyclopediaStreamer.cs` and `H8NarrativeApexVerifier.cs`; AppliedLore source audit passes with `packets=460`, `rows=6900`, `route_cards=454`; local tokens report `pda_try_read_dump_entry_refs=0`, `pda_raw_payload_allocs=0`, `pda_transient_payload_creates=1`, `pda_transient_payload_disposes=1`, `verifier_blackbox_gate_tokens=20`; `git diff --check` reports CRLF warnings only.
  - Rejected: `dotnet build`, Unity import/refresh, DataMonolith bake, scene edit, generated content rewrite, or process kill.
  - Estimate: Unity script validation only; no project build process launched.

## Control Pass 117 - AppliedLore multilingual encyclopedia status route

- [x] Removed authoring-only draft/native-review text from player-visible AppliedLore exports.
  - DOD: `AppliedLoreImporter` now strips known draft/native-review prefixes before CSV generation while preserving state in `flags`; regenerated DataMonolith CSV reports `rows=6900`, `locales=15`, `draft_flagged=5095`.
  - Rejected: showing draft markers in game/wiki/site prose, changing packet IDs, changing DTO layout, adding a parallel localization table, or hiding incomplete locales.
  - Estimate: runtime cost 0; route-state uses existing `H8AppliedLorePacketRecord.Flags`.
- [x] Added publication-facing localization status without prose pollution.
  - DOD: `AppliedLorePageExporter` now writes `direction`, `localization_status`, and `localization_flags` frontmatter plus `Docs/Lore/AppliedContent/Localization_Status_Index.md`; RTL locales are marked for `ar_SA` and `he_IL`.
  - Rejected: markdown-only manual notes, JSON report files, binary telemetry dumps, or native-review text embedded in player-visible lore.
  - Estimate: offline export only; no frame cost.
- [x] Added a permanent AppliedLore audit gate for visible localization marker leaks.
  - DOD: `AppliedLoreRuntimeAudit` now scans CSV player-visible fields and generated `in_game_wiki`/`external_site` Markdown for forbidden draft/native-review markers before route/page validation.
  - Rejected: one-off grep proof, manual QA-only check, adding a second publication validator, or accepting leakage because flags exist.
  - Estimate: source-only audit cost; runtime cost 0.
- [x] Reverified source route and generated publication output.
  - DOD: `py_compile` passes for importer/exporter/auditor; AppliedLore source audit green with `packets=460`, `locales=15`, `rows=6900`, `visible_marker_csv_fields=48300`, `visible_marker_pages=13830`, `wiki_pages=6900`, `site_pages=6900`, `index_pages=30`; exact forbidden-marker scan across CSV/wiki/site returns 0 matches.
  - Rejected: `dotnet build`, Unity import/refresh, DataMonolith bake, scene edit, process kill, or broad runtime refactor.
  - Estimate: no compiler process launched; no runtime frame change.

## Control Pass 118 - AppliedLore publication metadata bridge

- [x] Added route metadata to every generated encyclopedia page.
  - DOD: `AppliedLorePageExporter` now emits `release_set_id`, `unlock_id`, `poi_tags`, and `biome_tags` frontmatter on both `in_game_wiki` and `external_site` pages while preserving runtime markdown isolation.
  - Rejected: forcing site/wiki consumers to parse packet JSON, adding runtime markdown reads, expanding DataMonolith DTOs, or using a separate unmanaged route table for publication-only navigation.
  - Estimate: offline export only; runtime cost 0.
- [x] Added a deterministic publication surface manifest.
  - DOD: `Publication_Surface_Index.csv` now has 13,800 rows, one per generated surface/locale/packet page, with surface, locale, direction, packet id, release set, article id, unlock id, localization status, tags, relative page path and title.
  - Rejected: JSON report, hand-maintained navigation docs, scanning 13,800 Markdown files during website assembly, or mixing spoiler/navigation state into visible article prose.
  - Estimate: site/wiki ingestion bridge; no game frame cost.
- [x] Added audit coverage for page metadata and publication manifest.
  - DOD: `AppliedLoreRuntimeAudit` now checks 13,800 page frontmatter records and 13,800 publication surface rows against CSV source truth; source audit green with `publication_frontmatter_pages=13800` and `publication_surface_rows=13800`.
  - Rejected: one-off manifest count, unchecked CSV generation, or accepting frontmatter drift from the DataMonolith source route.
  - Estimate: source-only audit cost; no compiler process launched.

## Control Pass 119 - AppliedLore hard-sci-fi encyclopedia cluster manifest

- [x] Added deterministic multilingual cluster manifest generation.
  - DOD: `AppliedLorePageExporter` now writes `Docs/Lore/AppliedContent/Publication_Cluster_Index.csv` from `RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv`; the manifest maps five encyclopedia hubs across 15 locales and both publication surfaces with cluster id, order, spoiler tier, source page path, route question and truth payload.
  - Rejected: hand-maintained wiki map, JSON report file, runtime markdown parsing, or a new parallel taxonomy outside the existing RS084 evidence graph.
  - Estimate: offline publication bridge only; runtime frame cost 0.
- [x] Added source audit coverage for the cluster manifest.
  - DOD: `AppliedLoreRuntimeAudit` validates `Publication_Cluster_Index.csv` headers, row uniqueness, page existence, direction, localization flags, release set, unlock id, tags, spoiler tier, prerequisites, next cluster and truth/question fields against source CSV plus RS084 graph.
  - Rejected: row-count-only proof, trusting exporter output, or forcing site/wiki tooling to crawl 13,800 pages for navigation state.
  - Estimate: source-only audit cost; no runtime parser, no DTO change.
- [x] Reverified source-only publication route.
  - DOD: in-memory Python AST parse reports `ast_ok=2`; page exporter rewrote `applied_lore_pages_written=13800` and `index_pages_written=30`; AppliedLore source audit passes with `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, `publication_cluster_rows=150`, `wiki_pages=6900`, `site_pages=6900`.
  - Rejected: `dotnet build`, Unity import/refresh, DataMonolith bake, scene edit, or claiming runtime proof from source-only publication tooling.
  - Estimate: no compiler process launched; runtime game cost 0.

## Control Pass 120 - AppliedLore local human reader

- [x] Added a minimal local HTML reader for the multilingual encyclopedia.
  - DOD: `Docs/Lore/AppliedContent/reader.html` reads existing `Publication_Surface_Index.csv`, `Publication_Cluster_Index.csv`, and generated Markdown pages; it supports surface, locale, localization status, search, cluster hubs, RTL article direction, and direct article loading.
  - Rejected: new lore generator, JSON report, binary telemetry dump, web framework, package install, runtime markdown dependency, or C# DTO change.
  - Estimate: static publication reader only; runtime game cost 0.
- [x] Added one discoverability line to the AppliedContent README.
  - DOD: README now gives the local HTTP server command and `reader.html` URL.
  - Rejected: bloated documentation pass or another status-only artifact.
  - Estimate: docs only; runtime cost 0.
- [x] Validated the reader and launched a local server.
  - DOD: extracted JavaScript passes `node --check`; Python HTML parser sees 1 script and 1 style; publication input check reports `surface_rows=13800`, `cluster_rows=150`, default Russian start page exists; HTTP `HEAD` returns 200 for `reader.html`, `Publication_Surface_Index.csv`, and the default Russian start article.
  - Rejected: Playwright install, `dotnet build`, Unity import/refresh, DataMonolith bake, scene edit, or killing unrelated existing Python processes.
  - Estimate: local reader server only; runtime game cost 0.

## Control Pass 121 - AppliedLore public article quality repair

- [x] Replaced the five RS084 public cluster stubs with real EN/RU site articles.
  - DOD: `P416`-`P420` now have `external_site_article` longform bodies for `en_US` and `ru_RU`; Russian titles/snippets were restored from corrupted placeholder text.
  - Rejected: pretending Scanner/Terminal/Audio snippets are website articles, hiding the problem by defaulting to English, or hand-editing generated Markdown only.
  - Estimate: content/source only; runtime game cost 0.
- [x] Made the page exporter honor public longform bodies.
  - DOD: `AppliedLorePageExporter` uses `external_site_article` for `external_site` pages and suppresses service `Scanner`/`Terminal`/`Audio` sections only when a public article body exists; in-game wiki compact output remains unchanged.
  - Rejected: web framework, new generator, runtime markdown parsing, DTO expansion, or changing non-public surfaces.
  - Estimate: offline export only; runtime cost 0.
- [x] Re-exported and audited the publication layer.
  - DOD: exporter rewrote 13,800 pages; source audit passes with `packets=460`, `site_pages=6900`, `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, `publication_cluster_rows=150`; five EN public hubs have 5-6 paragraphs and 0 `## Scanner` headings; five RU public hubs have restored UTF-8 titles and no corrupted `????` payload.
  - Rejected: `dotnet build`, Unity import/refresh, scene edit, DataMonolith bake, or claiming all 460 packets are now prose-polished.
  - Estimate: no runtime frame change.

## Control Pass 122 - AppliedLore article voice decontamination

- [x] Rewrote the five RS084 public articles from agent-guidance prose into player-facing encyclopedia prose.
  - DOD: `P416`-`P420` EN/RU `external_site_article` bodies now read as lore/archive articles, not implementation guidance; the colony/workers article no longer mentions player framing, family-rule commentary, or "how to write" instructions.
  - Rejected: leaving thesis/meta text in public articles, hiding it behind the reader, or treating service-card quality as acceptable because the audit was green.
  - Estimate: content/source only; runtime cost 0.
- [x] Re-exported and validated the cleaned publication surface.
  - DOD: exporter rewrote 13,800 pages; source audit passes with `packets=460`, `site_pages=6900`, `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, `publication_cluster_rows=150`; smell scan across the 10 EN/RU public hub bodies found no `player/game/should/do not/use as/site nav/publication hub/игрок/игра/долж/нужно/использовать как/публикационный хаб`, no `## Scanner`, and no corrupted `????`.
  - Rejected: `dotnet build`, Unity import/refresh, scene edit, DataMonolith bake, or claiming every non-hub packet is already editorially fixed.
  - Estimate: no runtime frame change.

## Control Pass 123 - P418 Worker Encyclopedia Rewrite

- [x] Rebuilt the colony/workers article as player-facing lore instead of agent guidance.
  - DOD: `P418_SITE_WIKI_COLONY_AND_WORKERS_CLUSTER` now uses dedicated EN/RU article source files, with a 650-word RU article and 790-word EN article focused on worker evidence, named staff, Deep Reach liability and professional proximity; no family-hook framing, no author instructions, no service sections.
  - Rejected: keeping long public prose inside JSON after a PowerShell encoding pass produced literal `?`, editing only generated markdown, or hiding broken text behind the reader UI.
  - Estimate: content/source only; runtime cost 0.
- [x] Re-exported and validated the publication surface after the article-source split.
  - DOD: exporter rewrote 13,800 pages; source audit passes with `packets=460`, `site_pages=6900`, `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, `publication_cluster_rows=150`; generated RU/EN `P418` pages have `?=0`, replacement chars `0`, and no `## Scanner`.
  - Rejected: `dotnet build`, Unity import/refresh, scene edit, or claiming the remaining 459 packets have the same editorial depth.
  - Estimate: no runtime frame change.
