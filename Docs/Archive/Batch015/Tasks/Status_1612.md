# Status 1612 - Narrative Integrator And POI Chronicler

Date: 2026-06-01
Status: PENDING VERIFICATION
Domain: Echelon 8 Presentation & UX - AUP Narrative Triggers, PDA Encyclopedia Streaming, Diegetic Terminals, scanner lore routing
Task Count: 20

## Mandates Selected

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt - hash-only narrative delivery, no string allocation in scan/display hot paths.
- ARCH_Signal_Lane_Segregation.txt - typed unmanaged signal lanes, no managed event names.
- DATA_Runtime_Struct_Layout_ARM64.txt - aligned signal/DTO layout.
- UI_Data_Streaming_ZeroGC_Optimization.txt - `TMP_Text.SetCharArray` and span/char-buffer text flow.
- UI_Diegetic_Physical_Interfaces.txt - terminal/PDA display must remain physical/world-space.
- PROG_Quest_State_Graph_Logic.txt - bit-packed/unmanaged evidence prerequisite logic.
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt - audio-log corruption must route as data, not managed playback strings.
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt - jobs/DataVault ownership, no hidden local native ownership.

## Loop 1 - Tasks 01-05

- [x] Task 01 EXHAUSTIVE_POI_TRIGGER_INQUISITION - Static scan complete. `Assets/_Project/Scripts/Environment/` has no target POI scripts; `Assets/_Project/Prefabs/POI/` is absent. Actual owners found: `NarrativeDiscovery`, `ScannableFragment`, `NarrativeSpatialTriggerAuthoring`, `MessageTerminal`. DOD practice: source archaeology before edits. Rejected: prefab YAML mutation. Estimate: 0 runtime us.
- [x] Task 02 SCANNER_TOOL_DEPENDENCY_MAPPING - Scanner path traced through `ScannerTool`, `ScannerDataMiningRouter`, `ScannableFragment`, `H8AppliedLoreRuntime`. Completion already routes hash payloads through unmanaged signal lanes. DOD practice: owner-route proof. Rejected: rewriting scanner router. Estimate: 0 runtime us added.
- [x] Task 03 ECOLOGICAL_IMPACT_ROUTE_DESIGN - Existing route found: `ScanCompleteSignal` -> `HectonNarrativeDirector_PoiTriggers` -> `H8AppliedLoreRuntime.TryRaiseScanCompleteWorldImpact` -> biome/audio signals. DOD practice: reuse first-party route. Rejected: new direct DataVault scatter writes without route card. Estimate: 0 runtime us added.
- [x] Task 04 EVIDENCE_GRAPH_STATE_MACHINE_PLANNING - Existing PDA knowledge gate uses packet-hash bit indexes plus `H8AppliedLoreRouteRecord` prerequisite checks. DOD practice: bitmask prerequisite read. Rejected: runtime managed graph. Estimate: 0 runtime us added.
- [x] Task 05 TELEMETRY_AND_REPORTING_ARCHITECTURE - JSON dump intentionally not generated per user directive. Proof path is ledgers plus final agent log. DOD practice: no fake metrics. Rejected: fabricated report schema. Estimate: 0 runtime us.
- [x] Loop 1 static verification - Mandates, batch prompt, AGENTS, domain files, and target source reviewed. No build.

## Loop 2 - Tasks 06-10

- [x] Task 06 STRING_BASED_POI_ANNIHILATION - Existing scripts already use applied lore hashes for runtime routes. Binding validation already exists in `H8AppliedLoreBindingCatalogWindow`; compiler route reference checks already existed. DOD practice: no unnecessary API churn. Rejected: deleting authoring strings used only cold/editor. Estimate: 0 runtime us added.
- [x] Task 07 ZERO_GC_SCANNER_COMPLETION_IMPLEMENTATION - Existing `ScannableFragment` raises applied-lore hash and AUP through `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt`. DOD practice: typed signal path, no UI/audio strings. Rejected: duplicate scanner bus. Estimate: 0 runtime us added.
- [ ] Task 08 KNOWLEDGE_STATE_VALIDATOR_JOB - Runtime Burst job not added. Existing validation lives in PDA/DataVault hash-bit route; adding a second authority would violate one-fact/one-route without a route card. Editor prerequisite cycle validator added instead. Status: PARTIAL, PENDING ROUTE CARD.
- [x] Task 09 PDA_TEXT_STREAMING_BRIDGE - Existing `ReadOnlySpan<byte>` + decoder + `TMP_Text.SetCharArray` route preserved. Added fail-closed corrupted-body writer using preallocated char span. DOD practice: bounded buffer writer. Rejected: string assignment. Estimate: no measured us.
- [x] Task 10 ACOUSTIC_GHOST_SIGNAL_ROUTING - Added 8-byte `AudioGlitchParametersDTO` inside existing audio-log event payload padding and derived depth/hash corruption parameters in `AudioLogSystem`. DOD practice: explicit-layout DTO, cinematic DSP fake. Rejected: clean clip-only playback. Estimate: no measured us.
- [x] Loop 2 static verification - `git diff --check` passed for touched source files; line-ending warnings only. No build.

## Loop 3 - Tasks 11-15

- [ ] Task 11 ECOLOGICAL_IMPACT_BURST_JOB - New Burst write job not added. Existing ecological impact bridge emits typed biome/acoustic signals; direct scatter/voxel buffer mutation needs route card and owner lock contract. Status: PARTIAL, PENDING ROUTE CARD.
- [x] Task 12 DIEGETIC_TERMINAL_DISPLAY_LOGIC - Existing `MessageTerminal` publishes applied-lore terminal preview; `TerminalOsRuntime` consumes hash preview and writes UTF8 via fixed char buffers. DOD practice: diegetic terminal surface. Rejected: floating UI overlay. Estimate: 0 runtime us added.
- [x] Task 13 FAIL_CLOSED_LORE_MISSING_SAFETY - Added PDA corrupted-record output for missing span and retained black-box/fault path. DOD practice: fail-closed visible text. Rejected: exception/freeze. Estimate: no measured us.
- [x] Task 14 DRY_RUN_VERIFICATION_EXECUTION - Mental trace recorded in rationale: scan -> hash signal -> prerequisite check -> encrypted/corrupted display path -> no hot strings.
- [x] Task 15 BATCHED_COMPILATION_AND_SYNTAX_ASSERTION - Build skipped by user order. Static verification only. DOD practice: host contention avoidance. Rejected: routine `dotnet build`.
- [x] Loop 3 static verification - Modified source reread around changed methods. No build.

## Loop 4 - Tasks 16-20

- [ ] Task 16 MOCK_10K_SCAN_SPAM_ASSERTION - Not executed. No editor test added because runtime authority/job route was not changed and build/profiler validation was forbidden for this pass.
- [ ] Task 17 ZERO_GC_TEXT_STREAMING_STRESS_TEST - Not executed. Existing span/SetCharArray path reviewed; no ProfilerRecorder proof collected.
- [x] Task 18 ZERO_COMPILATION_HOT_PATH_VERIFICATION - Static audit of modified hot presentation paths complete. No new reference-type allocation, string concat, or List use added in changed hot methods.
- [x] Task 19 EVIDENCE_GRAPH_DEADLOCK_AUDIT - Added cold compiler prerequisite graph cycle scanner for `H8AppliedLoreRouteRecord` route dependencies. Throws `FatalArchitectureException` text on cycle/self prerequisite.
- [x] Task 20 AUTOMATED_METRIC_VALIDATOR_REPORT - JSON report not generated per user directive. Final proof appended to `Docs/AgentLogs/LOG_1612.md`; no fabricated timings or hashes.
- [x] Loop 4 static verification - `rg` and `diff --check` performed. No build.

## Loop 5 - Final Self-Audit

- [x] Re-read CURRENT_BATCH prompt for 1612 - Extracted block with attributes-aware regex.
- [x] Re-read modified source - Reviewed changed symbol locations by `rg`.
- [x] Confirm no managed strings in hot signal payloads - `LoreFragmentScannedSignal` remains hash/AUP; audio DTO remains explicit-layout unmanaged.
- [x] Append final report to LOG_1612.md - Completed in final pass.

## APEX Continuation - Source Proof Pass

- [x] Hot dependency scan - Scoped scanner/PDA/terminal/audio/narrative files returned `NO_FORBIDDEN_HOT_DEPENDENCY_OR_TEXT_HITS_IN_AGENT_SCOPE` for `GlobalRegistry.Get<T>()`, component lookups, TMP string writes, `StringBuilder`, and managed collection creation inside `Tick/FastTick/SlowTick/FixedTick/LateFrameTick/Execute`. DOD practice: static method-body scan. Rejected: broad full-project rewrite. Estimate: 0 runtime us added.
- [x] Phase-safety proof - Audio playback presentation remains queued then flushed by `AudioLogSystem.LateFrameTick`; PDA corrupted record writes only inside `PDAEncyclopediaStreamer.LateFrameTick`; terminal applied-lore preview remains consumed through `TerminalOsRuntime.LateFrameTick`. DOD practice: presentation after simulation. Rejected: direct playback/text mutation during scanner completion.
- [x] Lock-flattening proof - Agent-scope runtime lock hit is `ScannerDataMiningRouter.TryWriteVaultSettings`, one `TryAcquireWriteLock` with one `finally` release. New audio/PDA/verifier changes add no DataVault write locks. DOD practice: no nested owner locks. Rejected: direct ecological scatter/voxel write locks without owner route card.
- [x] Audio scalability patch - `AudioLogSystem.ResolveAudioGlitchParameters` now consumes continuous `HomeostasisBrain.GlobalQualityWeight` to scale bitcrush/pitch/bandpass DTO values without changing gameplay truth. DOD practice: continuous quality scalar, no binary tier fork.
- [x] APEX verifier patch - `H8NarrativeApexVerifier` now checks PDA corrupted-record route and audio glitch DTO/phase route from C# source. DOD practice: C# source proof, no JSON report.
- [x] Compilation throttling - `dotnet build` not launched. Existing external `dotnet` process observed, so build gate remains blocked.
- [x] Final static proof checkpoint - Re-ran agent-scope hot-method scan after compaction; result remained `NO_FORBIDDEN_HOT_DEPENDENCY_OR_TEXT_HITS_IN_AGENT_SCOPE`. Rechecked `git diff --check`; only LF-to-CRLF warnings. Rechecked process gate; active `dotnet` processes remained present, so no build was launched.

## APEX Continuation - Finite DTO Hardening

- [x] Audio glitch DTO boundary hardening - Added `AudioGlitchParametersDTO.Sanitize` and enqueue-side sanitization so public producers cannot push out-of-range permille, pitch, or unknown flag bits. DOD practice: unmanaged payload clamp before queue. Rejected: managed validator/report artifact.
- [x] Audio playback finite guard - `AudioLogSystem` now sanitizes volume, `GlobalQualityWeight`, depth/radiation interference, permille conversion, and pending glitch transfer before `LateFrameTick` playback flush. DOD practice: finite payload before presentation lane. Rejected: exception path or clean audio fallback only.
- [x] Applied-lore world-impact finite guard - `H8AppliedLoreRuntime.TryRaiseScanCompleteWorldImpact` now sanitizes acoustic impact intensity and AUP-derived depth before publishing `ToolAcousticSignal`. DOD practice: finite signal payload. Rejected: trusting static data blindly.
- [x] Verifier drift guard - `H8NarrativeApexVerifier` now counts DTO sanitizers, enqueue sanitization, and audio finite guards in the source proof string. DOD practice: AST/source contract proof.
- [x] Static proof rerun - Hot-method scan returned `NO_FORBIDDEN_HOT_DEPENDENCY_OR_TEXT_HITS_IN_AGENT_SCOPE`; lock scan found only existing scanner settings write lock with `finally`; `git diff --check` showed no whitespace errors; active `dotnet` process kept build gate closed.

## APEX Continuation - Audio Playback Lifecycle Hardening

## APEX Continuation - Universal Accessibility Motion Service

- [x] UI motion defect scan - `UIScreenShake` had cold `TryGetComponent` only in `Awake`, but shake amplitude had no persisted player comfort scalar. DOD practice: source-owner inspection before edit. Rejected: global VFX/camera shake rewrite outside 1612 domain. Estimate: 0 runtime us measured.
- [x] Accessibility motion producer - Added finite `uiMotionScale` ownership to `AccessibilitySettings` and publish-only-in-`VisualSyncTick` transfer to `UIScreenShake.SetGlobalMotionScale`. DOD practice: phase-owned primitive state transfer. Rejected: managed event/service route. Estimate: one finite clamp on changed scalar.
- [x] Persisted settings route - Added `Hecton_UiMotionScale`, `SettingsManager.UiMotionScale`, reset/load/apply validation, and fallback direct UI-shake scalar when the accessibility runtime instance is absent. DOD practice: cold settings persistence, no gameplay truth mutation. Rejected: prefab/YAML edits. Estimate: 0 hot-frame us.
- [x] SettingsPanel control - Added auto-created `Row_UiMotionScale` slider with cached `UnityAction<float>` and cached percent char labels via `SetCharArray` path. DOD practice: cold controls, zero-GC value display. Rejected: live TMP string assignment. Estimate: cold row allocation only.
- [x] UI shake consumer hardening - `UIScreenShake` now sanitizes delta, duration, envelope, intensity, and global motion scale; all transform writes remain in `LateFrameTick`. DOD practice: finite scalar guard before presentation write. Rejected: disabling the component or changing gameplay input. Estimate: one scalar multiply in active shake frames.
- [x] Verifier route extension - `H8NarrativeApexVerifier` now includes `UIScreenShake.cs` and source-checks accessibility UI motion fields, persistence, zero-GC panel labels, and LateFrame-only shake writes. DOD practice: in-memory C# source proof, no JSON report.
- [x] Static verification - `git diff --check` found no whitespace errors, only existing LF/CRLF warnings. Hot-method scanner found zero forbidden dependency/text writes in changed hot methods. Robust brace lexer returned balance zero for changed C# files. Build skipped because active external `dotnet` processes were present and user forbids routine build.

- [x] Full-playback bitcrush state fix - `PlayLogByHash` now stores the `QueuePlaybackVisualSync` return value in `_currentPlaybackBitCrushed`, matching encrypted-preview state transfer. DOD practice: source-state parity. Rejected: pretending full playback never routes bitcrush. Estimate: scalar branch only, no measured us.
- [x] Playback duration finite guard - Added `ResolvePlaybackDuration` and used it for full playback, encrypted preview, and atmospheric warning blockers. DOD practice: finite timer before event/presentation state. Rejected: trusting authored `AudioLogData.Duration`. Estimate: no steady per-frame work added.
- [x] Audio event duration clamp - `AudioLogEvents.Enqueue` now writes `SanitizeDurationSeconds(durationSeconds)` into the unmanaged payload. DOD practice: event boundary clamp. Rejected: listener-side defensive cleanup. Estimate: enqueue-only scalar comparisons.
- [x] StopPlayback pending-sync cancellation - `StopPlayback` now clears pending playback and unregisters late-frame delivery before raising the stop event, preventing stale `LateFrameTick` presentation. DOD practice: phase queue cancellation. Rejected: leaving a stopped clip queued for visual sync. Estimate: cold stop path only.
- [x] Verifier lifecycle drift guard - `H8NarrativeApexVerifier` now counts duration guards, stop cancellation, and full-playback state propagation. DOD practice: source contract proof, no report file.
- [x] Static proof rerun - Hot-method scan returned `NO_FORBIDDEN_HOT_DEPENDENCY_OR_TEXT_HITS_IN_AGENT_SCOPE`; scoped lock scan showed no new DataVault write locks; `git diff --check` showed no whitespace errors, only LF-to-CRLF warnings; active `dotnet` PIDs `25128` and `31232` kept build gate closed.

## APEX Continuation - MessageTerminal Phase Hygiene

- [x] Terminal tick finite guard - `MessageTerminal.Tick` now consumes `SanitizeDeltaTime(deltaTime)` before blink/playback timer mutation. DOD practice: finite scalar boundary. Rejected: trusting dispatcher delta blindly. Estimate: one scalar branch chain per tick.
- [x] Terminal playback duration clamp - `StartPlayback` now routes clip/authored duration through `ResolvePlaybackDuration`, clamping NaN/Infinity/non-positive values to 5s and long values to 86400s. DOD practice: finite timer before state transition. Rejected: direct `AudioClip.length` trust. Estimate: playback-start only.
- [x] Terminal pending event lifecycle clear - `OnDisable`, `OnDestroy`, and `FlushQueuedTerminalEvents` now use `ClearQueuedTerminalEvents`, dropping stale legacy UnityEvent string refs outside the delivery frame. DOD practice: phase queue cancellation. Rejected: retaining disabled-object event payloads. Estimate: lifecycle/LateFrame only.
- [x] Verifier terminal drift guard - `H8NarrativeApexVerifier` now counts `message_terminal_finite_time_guards=4` and `message_terminal_pending_event_clears=3` from C# source. DOD practice: source contract proof, no JSON.
- [x] Static proof rerun - Hot-method scan returned `NO_FORBIDDEN_HOT_DEPENDENCY_OR_TEXT_HITS_IN_AGENT_SCOPE`; code-brace lexer returned balanced source for `MessageTerminal.cs` and `H8NarrativeApexVerifier.cs`; `git diff --check` returned no whitespace errors, only LF-to-CRLF warning; active `dotnet` PID `31232` kept build gate closed.

## APEX Continuation - MessageTerminal Presentation Scalar Hardening

- [x] Blink interval finite clamp - `MessageTerminal.Tick` now compares `_blinkTimer` against `SanitizeBlinkInterval(blinkInterval)`, and `OnValidate` writes the same bounded interval. DOD practice: presentation scalar clamp. Rejected: relying on inspector `Range` metadata. Estimate: one scalar branch chain only during NewMessage tick.
- [x] Queued static audio volume clamp - `QueueStaticAudio` now stores `Sanitize01(volume)` before `LateFrameTick` playback flush. DOD practice: finite state-transfer payload. Rejected: listener-side volume cleanup. Estimate: interaction/new-message queue path only.
- [x] Authored clip duration sanitize - `OnValidate` now stores `SanitizePositiveDuration(entry.audioClip.length)` instead of raw clip length. DOD practice: cold author data normalization. Rejected: carrying bad serialized duration into runtime and clamping only at playback.
- [x] Verifier scalar drift guard - `H8NarrativeApexVerifier` now counts `message_terminal_finite_time_guards=6`, `message_terminal_presentation_scalar_guards=3`, and `message_terminal_pending_event_clears=3`.
- [x] Static proof rerun - Hot-method scan returned `NO_FORBIDDEN_HOT_DEPENDENCY_OR_TEXT_HITS_IN_AGENT_SCOPE`; terminal contract returned `MESSAGE_TERMINAL_CONTRACT finite_time_guards=6 presentation_scalar_guards=3 pending_event_clears=3`; brace lexer balanced both edited C# files; `git diff --check` returned no whitespace errors, only LF-to-CRLF warning; active `dotnet` PID `31232` kept build gate closed.

## APEX Continuation - TerminalOS Visual-Sync Rebuild Gate

- [x] Graphics rebuild phase correction - `TerminalOsRuntime.SlowTick` no longer flushes pending graphics-resource rebuilds; `LateFrameTick` owns the flush after job finalization. DOD practice: VISUAL_SYNC presentation ownership. Rejected: SlowTick RenderTexture mutation. Estimate: no extra steady-frame work.
- [x] Scheduled-job rebuild guard - `FlushPendingGraphicsResourceRebuild` now returns `false` and keeps the rebuild pending while format, click resolve, terminal interaction, or decryption jobs are scheduled. DOD practice: no resource release while jobs may still own native buffers. Rejected: forced `.Complete()` in presentation path.
- [x] Verifier phase drift guard - `H8NarrativeApexVerifier` now counts `terminal_os_graphics_rebuild_lateframe_calls=1`, `terminal_os_graphics_rebuild_slowtick_calls=0`, and `terminal_os_graphics_rebuild_job_guards=5`.
- [x] Static proof rerun - TerminalOS rebuild contract returned `lateframe_calls=1 slowtick_calls=0 job_guards=5`; hot-method scan returned `NO_FORBIDDEN_HOT_DEPENDENCY_OR_TEXT_HITS_IN_AGENT_SCOPE`; brace lexer balanced `TerminalOsRuntime.cs` and `H8NarrativeApexVerifier.cs`; `git diff --check` returned no whitespace errors, only LF-to-CRLF warning on TerminalOS; active `dotnet` PID `4360` kept build gate closed.

## APEX Continuation - TerminalOS Continuous Quality Rebuild Route

- [x] Runtime quality rebuild unblock - Removed the `Application.isPlaying` texture-exists early return that prevented `RefreshScalabilityPolicy` from queuing resolution changes after the first RenderTexture array existed. DOD practice: continuous `GlobalQualityWeight` reaches presentation fidelity. Rejected: frozen runtime RT resolution.
- [x] Phase preservation - Resolution changes still only queue `_pendingGraphicsResourceRebuild`; actual RenderTexture mutation remains behind the `LateFrameTick` scheduled-job gate. DOD practice: quality scaling without wrong-phase resource churn.
- [x] Verifier quality drift guard - `H8NarrativeApexVerifier` now counts TerminalOS quality rebuild guards and asserts zero playing-texture blocks in `RefreshScalabilityPolicy`.
- [x] Static proof rerun - TerminalOS quality contract returned `rebuild_guards=7 playing_texture_blocks=0`; brace lexer balanced `TerminalOsRuntime.cs` and `H8NarrativeApexVerifier.cs`; `git diff --check` returned no whitespace errors, only LF-to-CRLF warning on TerminalOS.

## APEX Continuation - ScannableFragment Lifecycle Queue Hygiene

- [x] Scanner compatibility event cleanup - `ScannableFragment` now clears pending late-frame visual/audio/event fields in `OnDisable`, `OnDestroy`, and `ResetState`. DOD practice: phase queue cancellation. Rejected: retaining legacy `UnityEvent<string>` payload refs across lifecycle transitions.
- [x] Hash route preserved - `TryUnlockLoreStage` still publishes applied-lore unlocks through `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt` with packet hash/AUP/source ID. DOD practice: unmanaged first-party route, legacy string callback remains compatibility-only.
- [x] Verifier lifecycle drift guard - `H8NarrativeApexVerifier` now counts scannable hash unlock, late-frame event flush, lifecycle clears, and pending string clears.
- [x] Static proof rerun - Scannable contract returned `hash_unlocks=1 lateframe_flushes=1 lifecycle_clears=3 pending_string_clears=2`; brace lexer balanced `ScannableFragment.cs` and `H8NarrativeApexVerifier.cs`; `git diff --check` returned no whitespace errors, only LF-to-CRLF warning on Scannable.

## APEX Continuation - NarrativeDiscovery Cached Lore Hash Route

- [x] Interaction-time string hash removal - `NarrativeDiscovery.Interact` now sends `_cachedLoreHash` into `ILoreUnlockSink.TryUnlockByHash` instead of computing `LocHash.ComputeAscii(discoveryId)` during interaction. DOD practice: cold identity cache. Rejected: managed string hashing in runtime interaction path.
- [x] Spatial DTO hash cache - `TryGetSpatialTrigger` now writes `LoreHash = _cachedLoreHash`; `RefreshAupTriggerCache` computes `_cachedLoreHash` once alongside quest/biome/soundscape hashes. DOD practice: flat DTO identity. Rejected: repeated string hashing during trigger authoring reads.
- [x] Verifier hash-cache drift guard - `H8NarrativeApexVerifier` now counts `NarrativeDiscovery` lore-hash cache, cached unlock calls, and zero runtime `LocHash.ComputeAscii(discoveryId)` hits.
- [x] Static proof rerun - NarrativeDiscovery contract returned `caches=3 cached_calls=2 runtime_string_hashes=0`; brace lexer balanced `NarrativeDiscovery.cs` and `H8NarrativeApexVerifier.cs`; hot scan returned `NO_FORBIDDEN_NARRATIVE_DISCOVERY_HOT_HITS`; `git diff --check` returned no whitespace errors, only LF-to-CRLF warning on NarrativeDiscovery.

## APEX Continuation - HectonNarrativeDirector Cached POI Hash Route

- [x] Director POI selection hash cache - `GetNearestUndiscoveredPOI` now reads `poi.DiscoveryHash` instead of hashing `poi.DiscoveryId` during nearest-POI selection. DOD practice: cached identity read. Rejected: runtime managed-string FNV loop. Estimate: removes one FNV loop per candidate POI scan.
- [x] AUP solved-result dispatch hash cache - `RebuildNativePoiRegistry` now clears/fills `_poiDiscoveryHashes` in parallel with `_poiDiscoveryIds`; `DispatchAupNarrativePoiSolvedResult` reads `_poiDiscoveryHashes[poiIndex]` and falls back to `poiHash`. DOD practice: cold registry cache feeding unmanaged event dispatch. Rejected: `ComputeDiscoveryHash(discoveryId)` inside dispatch.
- [x] Verifier director drift guard - `H8NarrativeApexVerifier` now counts director POI hash caches, cached dispatches, and zero runtime string hashes in the solved-result route.
- [x] Static proof rerun - Director contract returned `HECTON_DIRECTOR_POI_HASH_CACHE_CONTRACT caches=4 cached_dispatches=1 runtime_string_hashes=0`; hot-method scan returned `NO_FORBIDDEN_HOT_METHOD_HITS_IN_AGENT_SCOPE`; brace lexer balanced `HectonNarrativeDirector.cs`, `HectonNarrativeDirector_PoiTriggers.cs`, and `H8NarrativeApexVerifier.cs`; scoped lock scan still found only existing `ScannerDataMiningRouter` write lock with `finally`; `git diff --check` returned no whitespace errors, only LF-to-CRLF warnings; active `dotnet` PIDs `14080` and `28892` kept build gate closed.

## APEX Continuation - Applied Lore World-Impact Phase Split

- [x] World-impact signal drain moved out of visual sync - `ConsumeAppliedLoreWorldImpactSignals` is now called from `Tick`, not `LateFrameTick`, so `BiomeChangedSignal` and `ToolAcousticSignal` are published from the simulation/update lane instead of the visual presentation phase. DOD practice: phase-owned world signal publication. Rejected: publishing world-impact signals from `LateFrameTick`.
- [x] Audio presentation transfer deferred - Applied-lore acoustic interference is queued as `float` + `bool` fields and flushed to `ISpatialAudioNarrativeRadioSink.SetNarrativeRadioInterference` in `LateFrameTick`. DOD practice: zero-GC value transfer between phases. Rejected: direct audio sink call during signal drain.
- [x] Lifecycle pending-state clear - `OnDisable` and `OnDestroy` now call `ClearAppliedLoreWorldImpactState`, clearing pending acoustic state and the last impact biome hash. DOD practice: no stale visual/audio state after lifecycle exit.
- [x] Verifier phase drift guard - `H8NarrativeApexVerifier` now counts applied-lore world-impact Tick drains, LateFrame drains, queued audio transfers, lifecycle clears, and signal publishes.
- [x] Static proof rerun - Applied-lore phase contract returned `tick_drains=1 lateframe_drains=0`; hot-method scan returned `NO_FORBIDDEN_HOT_METHOD_HITS_IN_AGENT_SCOPE`; brace lexer balanced `HectonNarrativeDirector.cs`, `HectonNarrativeDirector_PoiTriggers.cs`, `H8NarrativeApexVerifier.cs`, and `H8AppliedLoreRuntime.cs`; scoped lock scan still found only existing `ScannerDataMiningRouter` write lock with `finally`; `git diff --check` returned no whitespace errors, only LF-to-CRLF warnings; active `dotnet` PID `28892` kept build gate closed.

## APEX Continuation - PDA Universal Quality Guard

- [x] PDA finite quality resolver - `PDAEncyclopediaStreamer` now resolves `HomeostasisBrain.GlobalQualityWeight` through `ResolveGlobalQualityWeight01`, clamping finite values and falling back to `0.5f` on NaN/Infinity. DOD practice: continuous scalar guard before decode/typewriter/text-token budgets. Rejected: DataVault fallback from the presentation hot path. Estimate: two scalar comparisons in active PDA frames.
- [x] PDA raw quality route removal - `LateFrameTick`, `UnlockEntry`, and `QUALITY` token formatting no longer call `math.saturate(HomeostasisBrain.GlobalQualityWeight)` directly. DOD practice: one scalar owner route. Rejected: scattered quality clamps. Estimate: 0 allocations, no bus layout change.
- [x] Verifier quality drift guard - `H8NarrativeApexVerifier` now counts PDA finite quality resolver/calls/raw saturates/guards in the source proof string.
- [x] Static proof rerun - PDA contract returned `resolver=1 calls=4 raw_saturates=0 raw_quality_reads=1 tmp_body_writes=0`; brace lexer balanced `PDAEncyclopediaStreamer.cs` and `H8NarrativeApexVerifier.cs`; `git diff --check` showed no whitespace errors, only LF-to-CRLF warning; active `dotnet` PID `28892` kept build gate closed.

## APEX Continuation - Applied Lore World-Impact Idempotence

- [x] ScanComplete world-impact dedupe - `HectonNarrativeDirector_PoiTriggers` now caches the last processed applied-lore `EntryHash`, `ScanId`, and `SourceId`, skipping duplicate snapshot observations before publishing biome/audio impact. DOD practice: reveal impact idempotence. Rejected: second managed queue or direct snapshot mutation. Estimate: three uint comparisons per scan-complete signal.
- [x] Lifecycle dedupe clear - `ClearAppliedLoreWorldImpactState` now clears cached entry/scan/source identity along with biome/audio state. DOD practice: no stale impact suppression after disable/destroy.
- [x] Verifier dedupe drift guard - `H8NarrativeApexVerifier` now counts world-impact dedupe fields, duplicate/cache calls, and lifecycle clear paths.
- [x] Static proof rerun - World-impact contract returned `tick_drains=1 lateframe_drains=0 dedup_guards>=8`; brace lexer balanced `HectonNarrativeDirector_PoiTriggers.cs`, `PDAEncyclopediaStreamer.cs`, and `H8NarrativeApexVerifier.cs`; `git diff --check` showed no whitespace errors, only LF-to-CRLF warnings; build remained skipped due active `dotnet` PID `28892`.

## APEX Continuation - PDA Accessibility Reveal Route

- [x] Instant reveal request lane - Added `RequestInstantReveal()` and `allowInstantRevealRequests` so UI/input can request fast text reveal without writing TMP immediately. DOD practice: public request queues scalar state only. Rejected: direct string assignment or immediate off-phase text mutation.
- [x] Visual-sync reveal application - `LateFrameTick` now calls `ForceRevealDecodedTextIfRequested`, moving `_visibleLength` to decoded buffer length and clearing `_charAccumulator`. DOD practice: cursor-only `SetCharArray` path preserved. Estimate: one bool branch per visible PDA frame.
- [x] Lifecycle and entry reset - Pending reveal requests clear on `OnDisable` and `BeginEntry`, preventing stale accessibility input from applying to a new lore article.
- [x] Verifier reveal drift guard - `H8NarrativeApexVerifier` now counts public request, LateFrame flush, cursor write, accumulator clear, and lifecycle clears.
- [x] Static proof rerun - Reveal contract returned `request_methods=1 lateframe_force_calls=1 visible_cursor_writes=1 accumulator_clears=1 lifecycle_clears=3`; brace lexer balanced touched files; hot dependency scan found only cold `TryGetComponent` in `Awake`; active `dotnet` PID `28892` kept build gate closed.

## APEX Continuation - PDA UI Rescale Accessibility Route

- [x] PDA rescale signal cold init - `PDAEncyclopediaStreamer.OnEnable` now initializes `SignalBus<UIRescaleRequestSignal>` and captures TMP baseline font sizes cold. DOD practice: cold dependency lane setup. Rejected: polling global UI services or scene lookups in `LateFrameTick`. Estimate: 0 hot allocations.
- [x] Visual-sync text scale consumer - `LateFrameTick` now consumes `UIRescaleRequestSignal` snapshots through `ReadOnlySpan<UIRescaleRequestSignal>` and applies only primitive `FontScale` state. DOD practice: immutable signal snapshot read. Rejected: destructive `TryConsumeFrame`, managed UI event callbacks, or string route. Estimate: one snapshot length check and bounded loop only when active.
- [x] Finite accessible scale guard - `ResolvePdaTextScale` clamps invalid/non-positive `FontScale`, `minimumTextScale`, and `maximumTextScale` before touching TMP font scalar. DOD practice: finite scalar boundary. Rejected: trusting inspector ranges and producer purity.
- [x] Verifier rescale drift guard - `H8NarrativeApexVerifier` now counts PDA rescale cold init, LateFrame call, snapshot read, finite guards, and font scalar writes.
- [x] Static proof rerun - `git diff --check` showed no whitespace errors, only LF-to-CRLF warning on PDA; brace lexer balanced `PDAEncyclopediaStreamer.cs` and `H8NarrativeApexVerifier.cs`; hot dependency scan found only existing cold `TryGetComponent` in `Awake`; active `dotnet` PID `28892` kept build gate closed.

## APEX Continuation - UI Rescale Broadcast Preservation

- [x] Layout rescale destructive read removed - `DiegeticHudManualLayout.FlushGlobalRescaleRequests` now reads `SignalBus<UIRescaleRequestSignal>.GetFrameSnapshot()` instead of advancing `TryConsumeFrame`. DOD practice: broadcast lane preservation. Rejected: legacy destructive queue drain. Estimate: no allocations, bounded signal loop only.
- [x] Layout duplicate suppression - Added static frame/source/reason/font-scale-bit cache and reset it in `SubsystemRegistration`, preventing repeated rebuilds from the same snapshot. DOD practice: primitive idempotence. Rejected: managed set/list or timestamp object.
- [x] Verifier broadcast drift guard - `H8NarrativeApexVerifier` now includes `DiegeticHudManualLayout.cs` and counts snapshot reads, zero legacy consumes, dedupe fields, reset clears, and rebuild calls.
- [x] Static proof rerun - `git diff --check` showed no whitespace errors, only LF-to-CRLF warnings; brace lexer balanced `DiegeticHudManualLayout.cs`, `PDAEncyclopediaStreamer.cs`, and `H8NarrativeApexVerifier.cs`; rescale flush scan returned `DIEGETIC_LAYOUT_FLUSH_ALLOCATION_AND_LEGACY_SCAN=clean`; active `dotnet` PIDs `24444` and `28892` kept build gate closed.

## APEX Continuation - Accessibility Text Scale Producer

- [x] Accessibility service text-scale source - `AccessibilitySettings` now owns a finite continuous `textScale` and public `SetTextScale(float)` request path. DOD practice: existing UX service owns player accessibility input. Rejected: new global manager or managed UI callback chain. Estimate: VisualSync-only scalar check.
- [x] Rescale producer API - `FontStreamingManager.RequestAccessibilityTextScale` now publishes sanitized `UIRescaleRequestSignal` payloads with reason `2`, reusing the existing unmanaged broadcast lane. DOD practice: route reuse, no new EventID. Rejected: direct PDA/layout references from settings panels. Estimate: one signal enqueue per changed scale.
- [x] Immediate diegetic layout apply - `DiegeticHudManualLayout.ApplyGlobalRescaleRequest` accepts the just-published unmanaged payload directly, while `FlushGlobalRescaleRequests` remains snapshot-based for broadcast consumers. DOD practice: phase-safe primitive transfer. Rejected: reading a stale snapshot immediately after publish.
- [x] Verifier producer drift guard - `H8NarrativeApexVerifier` now scans `AccessibilitySettings.cs` and `FontStreamingManager.cs`, treats `VisualSyncTick` as a presentation root, and counts producer/accessibility finite guards.
- [x] Static proof rerun - `git diff --check` showed no whitespace errors, only LF-to-CRLF warnings; brace lexer balanced `FontStreamingManager.cs`, `DiegeticHudManualLayout.cs`, `AccessibilitySettings.cs`, and `H8NarrativeApexVerifier.cs`; UI text-scale hot scan returned `UI_TEXT_SCALE_HOT_SCAN=clean`; lock scan found no DataVault write locks; active `dotnet` PID `28892` kept build gate closed.

## APEX Continuation - Persisted Accessibility Text Scale Service

- [x] Settings persistence route - `SettingsManager` now owns `Hecton_TextScale`, loads it from `options.h8cfg`, resets it with defaults, validates finite bounds, and applies through `AccessibilitySettings.ActiveRuntimeInstance` or `FontStreamingManager.RequestAccessibilityTextScale`. DOD practice: existing options owner, no new global service. Rejected: direct PDA/layout references from settings. Estimate: apply-only scalar clamp.
- [x] Player-facing settings UI - `SettingsPanel` now has a text-scale slider, cached `UnityAction<float>`, cold optional row creation, and prebuilt `78%..135%` labels written through `SetCharArray`. DOD practice: zero-GC label cache. Rejected: prefab YAML edit and TMP `.text` assignment. Estimate: no hot-frame work; slider callback only.
- [x] Rescale lane bootstrap hardening - `FontStreamingManager.PublishRescaleRequest` now calls `SignalBus<UIRescaleRequestSignal>.EnsureInitialized()` before pushing, so accessibility scale does not depend on PDA initialization order. DOD practice: producer-owned lane readiness. Rejected: consumer-order coupling.
- [x] Verifier route guard - `H8NarrativeApexVerifier` now includes `SettingsManager.cs` and `SettingsPanel.cs`, counting persisted text-scale route, cached slider bindings, finite guards, and zero text string writes.
- [x] Static proof rerun - `git diff --check` returned no whitespace errors, only LF-to-CRLF warnings; hot-method scan returned `HOT_METHOD_SCAN=clean`; brace lexer balanced all touched C# files; text-scale string-write scan returned `string_writes=0` for `SettingsManager`, `SettingsPanel`, and `FontStreamingManager`; active `dotnet` PIDs `3756` and `20008` kept build gate closed.

## APEX Continuation - Latest Universal Accessibility Motion Checkpoint

- [x] Latest checkpoint - Persisted UI motion comfort route completed in `AccessibilitySettings`, `SettingsManager`, `SettingsPanel`, `UIScreenShake`, and `H8NarrativeApexVerifier`. Static proof: `git diff --check` whitespace-clean except LF/CRLF warnings; changed hot-method scan returned zero forbidden dependency/text writes; robust brace lexer returned zero balance for changed C# files; build skipped because active external `dotnet` PID `3756` remained present.

## APEX Continuation - Audio-log Subtitle Phase Bridge

- [x] Callback presentation removed - `SubtitleManager.OnAudioLogEvent` now queues `PlaybackStarted`, `PlaybackStopped`, and `PlaybackCompleted` into a fixed value-only `PendingAudioLogSubtitleEvent[8]` ring. DOD practice: callback writes primitive transfer state only. Rejected: direct `HandleAudioLogPlaybackStarted`/`HandleAudioLogPlaybackEnded`, TMP update, cue notification, sensory pulse emission, or tickable registration from the callback. Estimate: three primitive stores per audio-log event.
- [x] Visual-sync drain installed - `SubtitleManager.LateFrameTick` now calls `DrainPendingAudioLogEventsVisualSync` before `AdvanceSubtitlePresentation`, so audio-log subtitle preparation and cue pulse routing execute only from the subtitle visual owner phase. DOD practice: phase-owned presentation. Rejected: changing global dispatcher artery order.
- [x] Duration and lifecycle guards - Pending audio-log duration is finite-clamped by `SanitizeAudioLogEventDuration`; `OnDisable` and `OnDestroy` clear both pending ring state and timed audio-log subtitle state. DOD practice: no stale subtitle payload after lifecycle exit.
- [x] Verifier drift guard - `H8NarrativeApexVerifier` now includes `SubtitleManager.cs` and counts pending ring definitions, callback queue calls, direct callback presentation calls, LateFrame drains, visual dispatches, lifecycle clears, and finite duration guards.
- [x] Static proof rerun - `STRUCTURAL_AUDIOLOG_CONTRACT_FINAL` returned `on_audio_queues=2 on_audio_direct_present=0 lateframe_drains=1 drain_dispatches=2 queue_registers=0 queue_array_writes=4 clear_array_writes=1 duration_finite_guards=2`; changed hot-method scan returned `forbidden_deps=0` and `new_tokens=0`; brace lexer balanced `SubtitleManager.cs` and `H8NarrativeApexVerifier.cs`; trailing whitespace scan returned zero; `git diff --check` returned no whitespace errors, only LF-to-CRLF warning; build skipped because active `dotnet` PID `18584` kept compilation gate closed.

## APEX Continuation - MessageTerminal Cached Hash Events

- [x] Runtime read identity cache - `MessageEntry` now has a baked `messageHash`, with `_messageHashes` and `_readMessageHashes` filled by cold cache rebuilds in `Awake`, `OnValidate`, `AddMessage`, and read-set rebuilds. DOD practice: cached identity route. Rejected: string `HashSet.Contains` during pending-message scans. Estimate: one cached uint comparison per message candidate.
- [x] Playback event hash lane - `StartPlayback`, `CompletePlayback`, and new-message insertion queue both stable hash and legacy string payloads; hash `UnityEvent<uint>` fires from `LateFrameTick` before the old string event. DOD practice: numeric first-party payload, legacy serialized event remains compatibility-only. Rejected: deleting string events and breaking prefabs.
- [x] Pending scan string route removed - `UpdatePendingMessage` now reads `GetCachedMessageHashNoAlloc(i)` and `IsReadMessageHash(messageHash)`; hot scan found zero `HashSet.Contains`, `new`, resize, component lookup, or registry lookup in `Tick`, `LateFrameTick`, `StartPlayback`, `CompletePlayback`, `UpdatePendingMessage`, and `FlushQueuedTerminalEvents`.
- [x] Verifier hash drift guard - `H8NarrativeApexVerifier` now counts terminal message hash fields, cold cache sites, hash event queue/flush/clear routes, hash pending reads, and zero legacy pending contains.
- [x] Static proof rerun - Terminal hash contract returned `hash_fields=3 cold_caches=7 hash_event_queues=3 hash_event_flushes=3 hash_event_clears=3 update_pending_hash_reads=1 update_pending_legacy_contains=0`; brace lexer balanced `MessageTerminal.cs` and `H8NarrativeApexVerifier.cs`; `git diff --check` returned no whitespace errors, only LF-to-CRLF warning; Unity script validator was unavailable (`no_unity_session`); build skipped because CPU was `87%` and active external `dotnet` PID `18584` kept compilation gate closed.

## APEX Continuation - Scanner LoreFragment AUP Payload

- [x] Scanner completion signal route - `ScannerDataMiningRouter.RouteCompletionIfNeeded` now publishes `LoreFragmentScannedSignal` directly after `ScanCompleteSignal`, carrying hash, frame, scanner source hash, AUP, and paired-complete flags. DOD practice: typed unmanaged signal lane. Rejected: managed `ScanEvents` as first-party route. Estimate: one 64-byte signal enqueue per completed scan.
- [x] Lore fragment DTO AUP contract - `LoreFragmentScannedSignal` is now explicit-layout 64 bytes with `AbsoluteUniversePosition` at offset 0 and hash/frame/source/flags at offsets 48/52/56/60; lifecycle validation now checks size 64. DOD practice: ARM64-aligned SignalBus payload. Rejected: PDA fallback to last-discovery AUP as primary route.
- [x] PDA AUP consumption - `PDAEncyclopediaStreamer.ConsumeScanSignals` now reads `signal.PositionAup` when `FlagHasAup` is set and only falls back to last-discovery AUP for hash-only legacy producers. DOD practice: immutable snapshot read in visual-sync owner. Rejected: destructive `TryConsumeFrame` or scene lookup.
- [x] Applied-lore AUP propagation - `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt` now routes finite AUP into the lore-fragment signal before publishing the paired `ScanCompleteSignal`. DOD practice: one source hash route, two typed views for different consumers.
- [x] Verifier route guard - `H8NarrativeApexVerifier` now allows exactly the scanner completion publish site and verifies scanner/PDA/applied-lore AUP fields, layout size, snapshot read, and absence of non-legacy direct dequeue consumers.
- [x] Static proof rerun - Brace lexer balanced all touched C# files; hot-method scan returned `HOT_FORBIDDEN_HITS=0`; scoped DataVault lock scan found only existing scanner settings write lock with `finally`; `git diff --check` returned no whitespace errors, only LF-to-CRLF warnings.

## APEX Continuation - Scanner LoreFragment AUP Integrity Pass

- [x] Hash-only flag hardening - `H8AppliedLoreRuntime.TryRaisePacketUnlocked` now masks out `FlagHasAup`; `TryRaisePacketUnlockedAt` clears it when AUP is non-finite. DOD practice: payload truth flag cannot lie. Rejected: trusting caller-supplied flags. Estimate: two bitwise ops on unlock publication only.
- [x] PDA precise-AUP state commit - `PDAEncyclopediaStreamer.ConsumeScanSignals` now passes `hasSignalAup` into `UnlockEntry`, so finite lore-fragment AUP updates PDA runtime state instead of acting as a decorative local variable. DOD practice: signal data reaches state owner. Rejected: last-discovery fallback as primary route. Estimate: one bool branch per lore signal.
- [x] Exact verifier proof - `H8NarrativeApexVerifier` now counts `LoreFragmentScannedSignal` layout fields inside the target struct body and verifies hash-only flag stripping. DOD practice: source-contract proof scoped to one DTO. Rejected: whole-file substring overcount. Estimate: editor-only.
- [x] Architecture notes synchronized - `PDA_ENCYCLOPEDIA_STREAMER.md`, `SHINOBU_226_SCANNER_LORE_DATABASE_SYNC.md`, and `Implementation_Notes.md` now state the hash/AUP contract and legacy fallback rule. DOD practice: no stale active docs.
- [x] Static proof rerun - Struct contract checks passed; PDA AUP contract checks passed; touched hot-method scan returned `TOUCHED_HOT_FORBIDDEN_HITS=0`; brace counts balanced; trailing whitespace scan returned zero hits; `git diff --check` returned no whitespace errors; build skipped because CPU sampled `91%`.

### Route Card - SCANNER_LORE_FRAGMENT_AUP_1612

Route ID: `SCANNER_LORE_FRAGMENT_AUP_1612`
Date: 2026-06-01
Owner: `ScannerDataMiningRouter` for scanner completion; `H8AppliedLoreRuntime` for applied-lore facade publication; `PDAEncyclopediaStreamer` for visual-sync consumption.
Owner domain: Echelon 8 Presentation & UX / scanner lore routing.
Owning file/system: `SignalBus<LoreFragmentScannedSignal>`.
Problem: PDA needed hash and precise discovery AUP in the same first-party lore-fragment lane.
Why owner-local data is insufficient: scanner, PDA, applied-lore runtime, and world-impact consumers need decoupled fan-out from one scan completion fact.
Why direct caller/owner interface is insufficient: scanner must not depend on concrete PDA/audio/UI objects.
Instrument: `SignalBus<T>` first-party broadcast.
Producer phase: scanner completion / applied-lore unlock publication.
Consumer phase: PDA `LateFrameTick` snapshot read / VISUAL_SYNC.
Cadence/capacity: dirty only, existing `LoreFragmentScannedSignalCapacity = 128`, max bounded by completed scans per frame.
Expected max events/reads per frame: normal 0..1, stress bounded by lane capacity and drop counter.
GlobalQualityWeight behavior: none; quality may alter PDA reveal cadence, not hash/AUP truth.
Accessor purity: no `Get*`, `TryGet*`, `Resolve*`, or `Read*` API publishes this signal.
Payload/data shape: unmanaged 64-byte explicit-layout struct; `AbsoluteUniversePosition` plus hash/frame/source/flags.
Managed fields present: no.
UnityEngine.Object fields present: no.
Layout proof: `ValidateSignalSize<LoreFragmentScannedSignal>(64)` plus struct-body verifier checks.
Overflow/failure: `TryPushTracked` increments existing drop counter; hash-only legacy fallback clears `FlagHasAup`.
Telemetry fields: existing signal drop counters and PDA black-box state.
Black-box fields: PDA runtime state records last hash/frame/source/AUP.
GC proof required: hot-method source scan and Unity profiler proof before GREEN.
Shutdown/disposal: SignalBus lifecycle remains owned by GlobalSignals runtime lifecycle.
Scene unload behavior: consumers read immutable snapshots only; no scene reference in payload.
Stale-handle behavior: no native handle in payload.
Rejected alternatives: direct PDA call, managed event, expanding `ScanCompleteSignal` alone, last-discovery AUP as primary path.
Why this does not increase global monolith risk: single existing lane changed to explicit hash/AUP payload with fixed capacity and verifier guard.
Proof required before GREEN: Unity import/compile, Play Mode scan completion, GC 0 B sample, drop-counter clean under scanner stress.
Review disposition: `YELLOW / STATIC_SOURCE_ONLY`.
Status: `ACCEPTED_STATIC_SOURCE`.

## APEX Continuation - Scanner Paired Signal Dedup

- [x] PDA paired-signal duplicate suppression - `PDAEncyclopediaStreamer.ConsumeScanSignals` now skips `LoreFragmentScannedSignal` unlocks with `FlagPairedScanComplete` when the same snapshot already contains a matching `ScanCompleteSignal` by hash/source. DOD practice: immutable snapshot read with primitive compare. Rejected: mutating SignalBus cursors or adding a managed set. Estimate: bounded scan-complete loop only on lore signals.
- [x] Applied-lore paired flag truth - `H8AppliedLoreRuntime.TryRaisePacketUnlockedAt` now sets `FlagPairedScanComplete` only with finite AUP and paired `ScanCompleteSignal`; hash-only/non-finite routes clear both AUP and paired flags. DOD practice: metadata flag cannot lie. Rejected: trusting caller flags. Estimate: one extra bitwise OR/mask per unlock publication.
- [x] Verifier dedupe drift guard - `H8NarrativeApexVerifier` now counts PDA paired dedupe and applied-lore paired flag contracts in the scanner lore route proof.
- [x] Docs synchronized - PDA architecture, Shinobu route sync, and lore implementation notes now document paired-signal duplicate suppression.
- [x] Static proof rerun - Paired contract counts returned `PDA_DEDUPE_CALLS=1`, hash/source matches `1/1`, runtime paired flag refs `3`, clear-both masks `2`, verifier dedupe refs `10`; brace lexer balanced all touched C# files; trailing whitespace scan returned zero hits; `git diff --check` had no whitespace errors; build skipped because CPU sampled `97%` and active `dotnet` PIDs `10780,17844` kept the compilation gate closed.

## APEX Continuation - Scanner ScanEvents Cold Prewarm

- [x] Legacy scan-event queue prewarm - `ScanEvents.EnsureInitializedCold` exposes the existing native queue initialization as a named cold contract. DOD practice: cold owner initializes persistent queues before runtime publication. Rejected: allowing `TryRaiseEntryDiscovered` to be the first initializer from scanner completion. Estimate: 0 hot allocations after `OnEnable`.
- [x] Scanner OnEnable route hardening - `ScannerDataMiningRouter.OnEnable` now prewarms the legacy `ScanEvents` bridge after registry cache setup and before runtime scan completion can publish. DOD practice: hot `LateFrameTick` only enqueues into already-created native queues. Rejected: removing the legacy event bridge and breaking listeners.
- [x] Verifier cold-prewarm guard - `H8NarrativeApexVerifier` now counts the public cold method and the scanner `OnEnable` invocation in the scanner lore route proof.
- [x] Static proof rerun - Cold-prewarm counts returned `SCAN_EVENTS_COLD_METHODS=1`, `SCANNER_ONENABLE_PREWARM_CALLS=1`, verifier refs `9`; hot-method scan returned zero forbidden `GlobalRegistry.Get<T>`/`GetComponent` hits in touched runtime files; scoped settings write lock has one acquire/release inside one `try/finally`; touched-file whitespace check passed; scoped `git diff --check` returned no whitespace errors, only LF/CRLF warnings; build skipped because CPU sampled `100%` and active compiler PIDs `23284,27484` were present.
