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
