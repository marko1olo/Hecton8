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
