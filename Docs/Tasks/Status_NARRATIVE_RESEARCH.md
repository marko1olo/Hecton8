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
