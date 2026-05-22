# PROJECT_AUDIT Log

## 2026-05-21 - Audit Start

What was wrong: Direct user review request has no matching XML prompt in `Docs/Tasks/CURRENT_BATCH.md`; treating another prompt as authoritative would be contamination.
What was done: Established `PROJECT_AUDIT` status, rationale, and evidence boundary.
Cinematic Cheats used: None in code. Audit will judge systems against fake-first and continuous scalability doctrine.
Exact Microseconds saved: 0 us measured; no profiler claim.

## 2026-05-21 20:20:08 +04:00 - Whole Project Reality Audit

What was wrong: The project has a strong written doctrine, but source/content proof is behind the doctrine. Current disk scan found `2272` C# files under `Assets/_Project`, `172` asmdefs under `Assets/_Project`, `2762` data files, `1735` prefab files, and a `33,756,552` byte world scene, but the proof chain is volatile: `git status --short` reports `3438` entries (`1538 D`, `1262 M`, `638 ??`). AtlasCheck currently reports `missing=263`, worse than the R51 documented `missing=60`.
What was done: Audited root architecture docs, first-20-minutes route docs, mandate files, source topology, static gates, StreamingAssets, Addressables folder state, scene/prefab/data presence, Player prefab dev-loadout flags, and key route blockers. Compile state was deliberately excluded per user instruction.
Cinematic Cheats used: No code changed. Audit recommendation preserves the broad progressive-resolution strategy, but binds it to fake-first underwater presentation, continuous `GlobalQualityWeight`, and a hard Copper Wire route proof gate instead of additional broad abstraction.
Exact Microseconds saved: 0 us measured. Static estimates only: deleting generic breadth work from the immediate path should save review/integration time, not validated frame time. No profiler claim.

What was wrong: Authoritative runtime data readiness is not proven. `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent. `Data/Balance/Baked/H8StaticData.bin` exists but is not the mandated StreamingAssets payload. `Assets/AddressableAssetsData` exists but contains no content files in the current scan. PlatformPortabilityProofAudit reports no build artifacts, no serialized XR provider proof, no Quest URP wiring proof, no DataMonolith payload, and no Addressables content.
What was done: Classified this as project-readiness debt, not a local syntax issue. It blocks readiness/platform claims until route evidence exists.
Cinematic Cheats used: Route-first streaming proof should buy visual budget on high-end and survival budget on weak devices before adding more simulation.
Exact Microseconds saved: 0 us measured. Expected gain is avoided wasted runtime work, not measured frame savings.

What was wrong: Authority doctrine is good but not fully migrated. GlobalAuthorityGate reports `GlobalRegistry.` references `6156` across `746` files, `SignalBus` references `1515` across `226` files, `GlobalSignals.Publish` `239` across `79` files, `HectonEventBus` pub/sub `46` across `20` files, and `localNumericBufferCast=923`. DataVaultSovereigntyAudit fails closed with `runtimeForbidden=826` and missing baseline. Polish audit still finds `binaryHardwareSwitch=94` in `45` files.
What was done: Treated this as prioritized debt. Do not start a new cleanup campaign everywhere; remove or justify these only where they threaten the First 20 Minutes route, DataMonolith ownership, or platform proof.
Cinematic Cheats used: Replace binary platform forks with continuous quality scaling where route code touches presentation or cadence.
Exact Microseconds saved: 0 us measured. Future savings require profiler-backed route fixes, not text estimates.

What was wrong: The project contains high-risk monoliths: `H8LocHashes.cs` 12895 lines, `HectonPlayerMovement.cs` 12086, `WorldProceduralScatterDirector.cs` 10634, `PlayerCriticalProceduralAudioRenderer.cs` 10562, `GlobalSignals.cs` 9779, `SpatialAudioManager.cs` 9001, `HectonVoxelEngine.cs` 7939, `SaveBinaryStorage.cs` 7586, `HectonFluidEngine.cs` 7532, `PredatorCognitionDomain.cs` 7125. This is not a style complaint; it increases hidden ownership coupling and review failure probability.
What was done: Recommended route-bounded stabilization instead of broad refactor loops. Files should be split only when a route-proof or owner-boundary defect demands it.
Cinematic Cheats used: Favor cheap authored/faked player-facing route wins over deeper simulation expansion until proof artifacts exist.
Exact Microseconds saved: 0 us measured. Refactor avoidance prevents churn; frame savings remain unclaimed.

## 2026-05-21 - Ultra Polish DataMonolith Gate Pass

What was wrong: `Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs` still contained a player-build text CSV route for `visor_hud_profiles.csv` under `Application.streamingAssetsPath` and an `Assets/StreamingAssets/...csv` source path. `Tools/h8bin_validator.py` classified this as `RUNTIME_TEXT_STREAMINGASSETS_LOAD`. This bypassed the DataMonolith/static binary doctrine and created a cold IO/URI-risk route for Android/Quest-style StreamingAssets.
What was done: Restricted visor CSV profile hydration to editor/source-data ownership at `Assets/_SourceData/Visor/visor_hud_profiles.csv`. Player runtime no longer attempts to read visor profile text from `StreamingAssets`; runtime keeps deterministic default/baked profile DTOs until DataMonolith or a Visor-owned `.h8bin` carries the table. Updated `Docs/ARCHITECTURE/VISOR_AR_STENCIL_RENDERER.md` and `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with the boundary.
Cinematic Cheats used: Preserved the shader-side visor Dear Lie and avoided adding runtime parsing, Addressables text payloads, or extra simulation. This is a proof-route cleanup, not a visual feature expansion.
Exact Microseconds saved: 0 us measured. Static-only expected impact: removes one cold player-build text IO/stutter risk and one h8bin validator blocker. Sidecar validation command `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --no-require-static-data --report-json Docs\Reports\PROJECT_AUDIT_h8bin_validator_post_sidecar.json --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log` returned `PASS files=1 structs=32 mb=0.034424`. Required static-data validation still fails only with `STATIC_DATA_MISSING` for `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.

What was wrong: Static audit still reports global debt outside this narrow patch: `PolishMandateStaticAudit.py` remains `PASS_WITH_WARNINGS` with `binaryHardwareSwitch=94`, `privateNativeCollectionField=1315`, `structAutoProperties=3`, `unityRandom=5`; `DataVaultSovereigntyAudit.py --fail-on-runtime-regression` still fails closed due missing baseline and `runtimeForbidden=826`; `GlobalAuthorityGate.py` remains `PASS_WITH_WARNINGS`.
What was done: Did not mass-edit these surfaces. SHINOBU_200 signal-thread contention has an existing thread-local corridor but remains `STATIC_SOURCE_ONLY`; legacy `NativeQueue<T>.ParallelWriter` bridges require owner-by-owner route migration, not a blind global rewrite from PROJECT_AUDIT.
Cinematic Cheats used: None in code. The chosen intervention removes a binary-payload proof violation without expanding runtime work.
Exact Microseconds saved: 0 us measured. No profiler, Play Mode, Unity import, or player-build proof claimed.

## 2026-05-21 - Signal Cache-Line Telemetry Predicate Reconciliation

What was wrong: `SignalBus<T>.HasCacheLineCriticalStrideDebt()` treated 192-byte cache-line-critical payloads as clean because they are 64-byte multiples. Static SignalBus audit and `SHINOBU_200_SIGNAL_THREAD_CONTENTION_ROUTE_CARD.md` intentionally classify clean cache-line-critical payloads as exactly 64 or 128 bytes, leaving `ToolAcousticSignal` at 32 bytes and `TetherTensionSignal` at 192 bytes as explicit INFO debt rows. Runtime telemetry could therefore hide the tether lane debt.
What was done: Changed the runtime predicate in `Assets/_Project/Scripts/Core/GlobalSignals.cs` to mark cache-line-critical lanes clean only when `UnsafeUtility.SizeOf<T>()` is 64 or 128. No signal fields, offsets, queue types, writer routes, BufferIDs, save identity, rollback boundary, quality curve, or asmdef references changed. Added a route-card note documenting this as proof-surface reconciliation only.
Cinematic Cheats used: None. This is observability hardening, not simulation or presentation work.
Exact Microseconds saved: 0 us measured. Static proof: `Tools\SignalBusContractAudit.ps1 -Scope SignalCritical -OutputJson Docs\Reports\PROJECT_AUDIT_signalbus_contract_post.json -OutputMarkdown Docs\Reports\PROJECT_AUDIT_signalbus_contract_post.md` returned `errors=0 warnings=0 infos=19`, with `cacheLineCriticalStrideDebtHits=2` for `ToolAcousticSignal` and `TetherTensionSignal`. No rebuild, Unity import, profiler, GCMonitor, or player-build proof claimed.

## 2026-05-21 - H8BIN Symbolic Runtime Text Loader Gate

What was wrong: `Tools/h8bin_validator.py` reported no runtime text loader sites after the Visor cleanup because it required `.csv`, `.json`, or `.xml` to appear on the same line as `StreamingAssets`. Runtime code still had variable-based CSV routes: `ShinobuApexBrainVault.cs:907`, `PredatorCognitionDomain.cs:3129`, `PredatorCognitionDomain.cs:3341`, `StressDrivenSpawnDirector.cs:2234`, and `VolcanicUpdraftDirector.cs:1896`.
What was done: Added a source-symbol pass for const/static readonly text artifact names and linked those symbols to `StreamingAssets` loader lines. Updated `Docs/ARCHITECTURE/H8BIN_VALIDATOR_SHINOBU_258.md` so the previous "0 runtime loader sites" claim is no longer stale. Generated `Docs/Reports/PROJECT_AUDIT_h8bin_validator_symbol_post.json` and `Docs/Reports/PROJECT_AUDIT_h8bin_validator_symbol_required.json`.
Cinematic Cheats used: None in runtime. This is a CI gate correction that protects the binary/DataMonolith route from hidden cold text IO; domain migrations should use baked binary/Vault rows rather than player-build CSV parsing.
Exact Microseconds saved: 0 us measured. Static proof: `python -m py_compile Tools\h8bin_validator.py` passes; `python Tools\test_h8bin_validator.py` ran 53 tests OK after adding a symbol-loader regression case. Sidecar validation now fails with 5 `RUNTIME_TEXT_STREAMINGASSETS_LOAD` errors and one validated `H8VB` sidecar. Required validation additionally fails with `STATIC_DATA_MISSING` for `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`. No dotnet rebuild, Unity import, profiler, GCMonitor, Play Mode, or player-build proof claimed.

## 2026-05-21 - Runtime CSV StreamingAssets Route Cleanup

What was wrong: The symbolic validator findings were true runtime-route debt. `apex_predator_stats.csv`, `ai_behavior_overrides.csv`, `mesofauna_species_profiles.csv`, `director_spawn_rules.csv`, and `volcanic_vents.csv` could be resolved from player-runtime `StreamingAssets`, keeping human-readable tuning as a parallel truth route outside DataMonolith/domain `.h8bin`.
What was done: Removed those five `StreamingAssets` fallbacks from `ShinobuApexBrainVault`, `PredatorCognitionDomain`, `StressDrivenSpawnDirector`, and `VolcanicUpdraftDirector`. The cold CSV bridges now use only editor/development source-data paths and return `null` in production player builds. Updated `H8BIN_VALIDATOR_SHINOBU_258.md`, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, and `SHINOBU_61_APEX_COGNITION.md`.
Cinematic Cheats used: None in runtime. This preserves deterministic defaults/emergency mock data until the real binary payload exists instead of doing runtime text IO. It spends no new CPU/GPU budget and avoids inventing a fake DataMonolith artifact.
Exact Microseconds saved: 0 us measured. Static proof: focused `rg` finds no `Application.streamingAssetsPath` in the four touched files; remaining `StreamingAssets` hits in `VolcanicUpdraftDirector.cs` are binary `.h8bin/.bin` legacy reads, not text CSV. `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --no-require-static-data --report-json Docs\Reports\PROJECT_AUDIT_h8bin_validator_after_csv_routes.json --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log` returned `PASS files=1 structs=32`. Required mode still fails with only `STATIC_DATA_MISSING` plus `H8VB_SCHEMA_VALIDATED`. No dotnet rebuild, Unity import, profiler, GCMonitor, Play Mode, or player-build proof claimed.

What was wrong: A narrow binary-route fix can be mistaken for global polish. Broad static debt still exists outside the touched source.
What was done: Re-ran `python Tools\PolishMandateStaticAudit.py` after the cleanup. Result remains `PASS_WITH_WARNINGS`: `binaryHardwareSwitch=94 files=45`, `burstMissingCompileSynchronously=9 files=5`, `privateNativeCollectionField=1315 files=228`, `structAutoProperties=3 files=2`, `unityRandom=5 files=4`, and `unityTimeCritical=987 files=269`.
Cinematic Cheats used: None. This is evidence discipline.
Exact Microseconds saved: 0 us measured. This broad static audit is a guardrail only; no runtime/profiler/player-build performance claim is made.

## 2026-05-21 22:15:09 +04:00 - Struct Auto-Property Static Debt Removal

What was wrong: `PolishMandateStaticAudit.py` still flagged three struct auto-properties: `Assets/_Project/Scripts/BuildTools/BuildPlaytestEntry.cs:83`, `Assets/_Project/Scripts/SaveSidecarStorage.cs:412`, and `Assets/_Project/Scripts/SaveSidecarStorage.cs:512`. They were not hot NativeArray DTOs, but they still preserved hidden accessor methods inside structs.
What was done: Replaced those auto-properties with direct fields: `CreatedTimestamp`, `SidecarWriter.Error`, and `SidecarReader.Error`. No save sidecar serialization order, payload width, binary endianness handling, GlobalDataVault route, SignalBus route, job dependency, or quality scalar changed.
Cinematic Cheats used: None in presentation or simulation. This is a static-source debt removal, not a visual fake.
Exact Microseconds saved: 0 us measured. Static proof: `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_struct_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_struct_after.md` returned `structAutoProperties=0 files=0`. `git diff --check -- Assets/_Project/Scripts/BuildTools/BuildPlaytestEntry.cs Assets/_Project/Scripts/SaveSidecarStorage.cs` reported only LF-to-CRLF warnings. No dotnet rebuild, Unity import, Play Mode, profiler, GCMonitor, or player build was run.

## 2026-05-21 22:18:51 +04:00 - Unity Random Static Audit False-Positive Filter

What was wrong: The broad polish audit reported `unityRandom=5 files=4`, but all five examples were string literals in editor audit tools: audio smoke tests, scanner lore tuner hot-pattern list, and physiology OOP scanner forbidden-pattern list. Those lines are proof scaffolding, not `UnityEngine.Random` runtime calls.
What was done: Updated `Tools/PolishMandateStaticAudit.py` to strip C# string literals before line-pattern checks, and added `test_ignores_forbidden_tokens_inside_string_literals` to `Tools/test_polish_mandate_static_audit.py`. This keeps real code-token detection while removing proof-text noise.
Cinematic Cheats used: None. This is evidence hygiene.
Exact Microseconds saved: 0 us measured. Static proof: `python Tools\test_polish_mandate_static_audit.py` ran 3 tests OK. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_rng_filter_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_rng_filter_after.md` now reports `unityRandom=0 files=0`; broad status remains `PASS_WITH_WARNINGS` because other debt remains. `git diff --check -- Tools/PolishMandateStaticAudit.py Tools/test_polish_mandate_static_audit.py` reported only LF-to-CRLF warnings. No dotnet rebuild, Unity import, Play Mode, profiler, GCMonitor, or player build was run.

## 2026-05-21 22:23:35 +04:00 - Editor LINQ Static Debt Removal

What was wrong: The post-filter polish audit still reported `linqSurface=7 files=3`. The remaining hits were real LINQ usage in editor validators/scanners, not runtime gameplay, but they kept proof tooling outside the zero-GC/static-discipline contract.
What was done: Removed `System.Linq` and LINQ method use from `MainMenuValidator`, `SynchronousGpuReadbackScanner`, and `Wave_Math_Scanner`. Scene component lookup and Roslyn AST walks now use explicit loops/type checks. The two physics scanner files are untracked in the current worktree, so there is no git baseline diff for those files; focused source scan is the evidence.
Cinematic Cheats used: None. This is editor/proof hygiene.
Exact Microseconds saved: 0 us measured. Static proof: focused `rg` over the three touched files found no `System.Linq`, `.OfType<T>()`, `.FirstOrDefault()`, `.SelectMany()`, `.Where()`, `.Select()`, `.Any()`, or `.ToList()` matches. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_linq_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_linq_after.md` reports `linqSurface=0 files=0`. `git diff --check` reported only an LF-to-CRLF warning for the tracked `MainMenuValidator.cs`. No dotnet rebuild, Unity import, Play Mode, profiler, GCMonitor, or player build was run.

## 2026-05-21 22:28:01 +04:00 - Burst Directive Static Debt Removal

What was wrong: Burst audit debt mixed proof-string false positives with four real BioForge editor procedural jobs missing `CompileSynchronously = true`. The false positives came from smoke/scanner strings containing `[BurstCompile]`; the real attributes were in `BioForgeJobs.cs`.
What was done: Updated `PolishMandateStaticAudit.py` so Burst attribute collection also strips C# string literals before scanning. Added test coverage through the existing string-literal regression. Patched all four BioForge job attributes to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
Cinematic Cheats used: None. This is compile-directive hygiene for editor procedural jobs.
Exact Microseconds saved: 0 us measured. Static proof: `python Tools\test_polish_mandate_static_audit.py` ran 3 tests OK. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_burst_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_burst_after.md` reports `burstMissingCompileSynchronously=0 files=0`, `burstMissingFloatMode=0 files=0`, and `burstMissingFloatPrecision=0 files=0`. `rg -n "\[BurstCompile" Assets\_Project\Scripts\Editor\ProceduralGen\BioForgeJobs.cs` shows all four attributes now carry the full directive. No dotnet rebuild, Unity import, Play Mode, profiler, GCMonitor, or player build was run.

## 2026-05-21 22:32:51 +04:00 - Runtime Terrain Pager LateUpdate Removal

What was wrong: The broad audit reported `unityUpdateMethod=12 files=12`. Only one hit was player runtime: `TerrainChunkPagerRuntime.LateUpdate()` polling `_deferredShutdown`; the other 11 hits are editor tuner/XRay windows.
What was done: Removed the runtime `LateUpdate()` method. Deferred shutdown cleanup now runs from the existing `VisualSyncPhaseSystem` dispatcher route. `Shutdown()` unregisters pre/post/frost phases immediately but keeps VisualSync registered when teardown is deferred; `TryReleaseDeferredShutdownState()` unregisters VisualSync after pending jobs and worker state are drained. The file is currently untracked in git, so focused source scan is the evidence surface.
Cinematic Cheats used: None. This is player-loop route cleanup.
Exact Microseconds saved: 0 us measured. Static proof: `rg -n "LateUpdate\(|UnregisterDispatcher\(|keepVisualSyncForDeferredShutdown|_deferredShutdown" Assets\_Project\Scripts\World\TerrainChunkPagerRuntime.cs` shows no `LateUpdate(` and shows the deferred VisualSync path. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_update_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_update_after.md` reports `unityUpdateMethod=11 files=11`; all remaining examples are editor windows. `git diff --check -- Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs` returned clean. No dotnet rebuild, Unity import, Play Mode, profiler, GCMonitor, or player build was run.

## 2026-05-21 22:35:15 +04:00 - Vocal Mock Bank Completion Fence Centralization

What was wrong: `VocalBankPlaybackRuntime.GenerateMockBankCold()` used a direct `handle.Complete()` after scheduling `GenerateMockVocalBankJob`. It is cold startup/fallback work, not a hot frame path, but it still bypassed the centralized dispatcher completion fence.
What was done: Replaced the direct completion with `DispatcherJobFence.TryComplete(ref handle, forceComplete: true)`. Mock bank byte layout, records, sample count, deterministic phrase hash, and audio route are unchanged.
Cinematic Cheats used: The existing deterministic mock vocal bank remains the fake; no new simulation was added.
Exact Microseconds saved: 0 us measured. Static proof: focused `rg` shows `DispatcherJobFence.TryComplete(ref handle, forceComplete: true)` and no `.Complete()` in `VocalBankPlaybackRuntime.cs`. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_complete_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_complete_after.md` drops `jobHandleComplete` from `129 files=41` to `128 files=40`. `git diff --check -- Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs` returned clean. No dotnet rebuild, Unity import, Play Mode, profiler, GCMonitor, or player build was run.

## 2026-05-21 22:48:42 +04:00 - Cold Completion Fence Sweep

What was wrong: Non-core executable direct `.Complete()` calls remained in MapMagic cold generator nodes, deterministic smoke testers, and `WalIntegrityFuzzerCore`. These are mostly cold/proof boundaries, not hot gameplay loops, but they still bypassed the central `DispatcherJobFence` policy and inflated broad `jobHandleComplete` pressure.

What was done: Replaced direct forced completion with `DispatcherJobFence.TryComplete(ref handle, forceComplete: true)` in `HectonTerrainSplatmapMapMagicNode`, `HectonAnomalyMapMagicNode`, `HectonBiomeMatrixMapMagicPostProcessNode`, `WalIntegrityFuzzerCore`, `SavePersistenceOmegaSmokeTester`, `ThermalMeltSmokeTester`, `VoxelDeformationSmokeTester`, `PlanetaryCanvasSmokeTester`, and `BiomeTransitionSmokeTester`. Added `Hecton8.Core` imports only where needed. Did not touch `DispatcherJobFence` direct completion internals or `.Complete(` audit string literals.

Cinematic Cheats used: None. This is job policy centralization, not simulation/rendering work.

Exact Microseconds saved: 0 us measured. Static debt reduction: `jobHandleComplete` dropped from `128 files=40` to `112 files=31`. Expected operational gain is enforceability: future job completion instrumentation/profiling can be centralized in one Core helper.

Evidence: Focused `rg` over changed files finds executable `.Complete()` only in `SavePersistenceOmegaSmokeTester` string literals. Project non-editor/non-QA `rg` finds executable `.Complete()` only in `Assets/_Project/Scripts/Core/DispatcherJobFence.cs`, plus the same audit strings. `git diff --check` reported no whitespace errors, only existing LF-to-CRLF warnings on touched files. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_complete_fence_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_complete_fence_after.md` returned `PASS_WITH_WARNINGS`. Remaining top debt includes `binaryHardwareSwitch=92 files=44`, `privateNativeCollectionField=1315 files=228`, `unityTimeCritical=964 files=261`, and `unityUpdateMethod=11 files=11`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-21 22:53:19 +04:00 - Acoustic Portal Continuous Quality Patch

What was wrong: `AcousticPortalPropagation` used `AcousticPathQuery.QualityTier` and `Query.QualityTier <= 2` to disable portal pathfinding. That was a real runtime binary quality switch in the audio propagation job.

What was done: Replaced the DTO field with `GlobalQualityWeight`, moved `DisablePortalPath` to offset 108, and kept the struct at 112 bytes. `AcousticPathJob` now resolves a continuous portal budget with `math.smoothstep(0.12f, 0.92f, qualityWeight)` and scales `MaxNodeExpansions` with `math.lerp(2f, requestedExpansions, portalBudget01)`. `SpatialAudioManager` now passes `ResolveVirtualVoiceQualityWeight()` into the query and uses the same continuous budget for the early portal-path gate.

Cinematic Cheats used: Existing cheap fallback is preserved as the low-weight audio fake: open-water delay/transmission is used instead of pathfinding when quality budget collapses. No new physical simulation was added.

Exact Microseconds saved: 0 us measured. Expected low-tier effect is avoided portal graph traversal when the continuous budget is effectively zero; middle/high/ultra get gradually larger node-expansion budgets instead of a tier snap.

Evidence: Focused `rg` confirms no `QualityTier` token remains in `AcousticPortalPropagation`. DTO layout math: 0-39 source `AcousticAup`, 40-79 listener `AcousticAup`, 80-91 `float3`, 92-103 three `int`s, 104-107 `float GlobalQualityWeight`, 108 `byte DisablePortalPath`, 109 one pad byte, 110-111 pad ushort, final size 112 bytes. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_acoustic_quality_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_acoustic_quality_after.md` returns `PASS_WITH_WARNINGS` and drops `binaryHardwareSwitch` from `92 files=44` to `88 files=42`. `git diff --check` reported only LF-to-CRLF warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-21 22:59:25 +04:00 - Binary Switch Audit Filter and Instance Culling Quality Patch

What was wrong: `binaryHardwareSwitch` mixed real decisions with DTO fields and serializers. After removing that noise, `InstanceCullingService` still had real runtime branch debt: `descriptor.QualityTier == InstanceCullingQualityTier.Low` controlled culling flags and fallback distance.

What was done: Hardened `PolishMandateStaticAudit.py` so binary switch findings require control-flow/control-expression context. Added `test_binary_hardware_switch_ignores_plain_dto_fields`. Added `GlobalQualityWeight` to `InstanceCullingDispatchDescriptor`, passed it from `HectonOctahedralImpostorRenderer`, and changed `InstanceCullingService` to derive low-tier distance pressure and fallback cull distance from `smoothstep`/`lerp` curves. `QualityTier` remains only as shader/telemetry label.

Cinematic Cheats used: Existing compute culling/indirect draw path remains the fake: distance/visibility pressure reduces draw work instead of simulating/rendering blocked instances. No new physics or CPU scene search was added.

Exact Microseconds saved: 0 us measured. Static debt reduction: audit filter reduced noisy `binaryHardwareSwitch` from `88 files=42` to `14 files=6`; instance culling patch then reduced real control-flow findings to `12 files=5`.

Evidence: `python Tools\test_polish_mandate_static_audit.py` ran 4 tests OK. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_binary_switch_filter_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_binary_switch_filter_after.md` returned `binaryHardwareSwitch=14 files=6`. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_instance_culling_quality_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_instance_culling_quality_after.md` returned `binaryHardwareSwitch=12 files=5`. Focused `rg` shows no `QualityTier ==` branch in the culling files. `git diff --check` reported only LF-to-CRLF warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-21 23:02:46 +04:00 - Volumetric VFX Continuous Quality Patch

What was wrong: `VFXEmissionProfile` and `VolumetricLightFeature` selected god-ray raymarch budgets through `HardwareTier.Low/High` branches. It was presentation-only, but still a visible fidelity snap instead of continuous `GlobalQualityWeight` scaling.

What was done: Added `GetVolumetricGodRaySteps(float globalQualityWeight)` to the emission profile and changed volumetric feature step resolution to use `HomeostasisBrain.GlobalQualityWeight` with smoothstep interpolation. `hardwareTier` remains as an authoring fallback only when the global scalar is invalid. Shader `_MATH_LOD_LOW/HIGH` keywords clamp the scalar endpoint instead of directly branching on hardware tier.

Cinematic Cheats used: The existing half-resolution raymarch/composite remains the fake. Low weights reduce steps rather than adding physical volumetric simulation; high weights buy visual richness by increasing steps.

Exact Microseconds saved: 0 us measured. Static debt reduction: `binaryHardwareSwitch` dropped from `12 files=5` to `8 files=3`.

Evidence: Focused `rg` finds no `HardwareTier.Low`, `HardwareTier.High`, `ShouldUseLowTierSteps`, or `ShouldUseHighTierSteps` control-flow in the touched files. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_vfx_quality_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_vfx_quality_after.md` returned `PASS_WITH_WARNINGS` with `binaryHardwareSwitch=8 files=3`. `git diff --check` reported only LF-to-CRLF warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-21 23:08:02 +04:00 - Binary Hardware Switch Audit Closed

What was wrong: After VFX cleanup, the audit still counted pure `QualityTier` read accessors and four real `GameBootstrapper` quality-tier switches. The accessor rows were evidence noise; the bootstrap rows were real boot-budget snaps for LOD, mip memory, async upload buffer, and async upload timeslice.

What was done: Refined `PolishMandateStaticAudit.py` to ignore pure tier accessors while preserving `switch/case` and direct tier-comparison findings. Added a pure-accessor regression test. Replaced the four `GameBootstrapper` `switch (hardwareProfile.QualityTier)` blocks with `ResolveBootQualityWeight01` and `ResolveBootQualityCurve`, using smoothstep interpolation and hardware score when available. `HectonHardwareProfile` binary layout was not changed.

Cinematic Cheats used: None directly. This is boot scalability policy. The practical effect is preserving lower LOD/mip/upload pressure on weak devices while letting high/ultra boot budgets rise smoothly.

Exact Microseconds saved: 0 us measured. Static debt reduction: `binaryHardwareSwitch` is now `0 files=0`.

Evidence: `python Tools\test_polish_mandate_static_audit.py` ran 5 tests OK. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_boot_quality_after.json --report-path Docs\Reports\PROJECT_AUDIT_polish_boot_quality_after.md` returned `PASS_WITH_WARNINGS` with `binaryHardwareSwitch=0 files=0`. Focused `rg` finds no `switch (hardwareProfile.QualityTier)` in `GameBootstrapper`. `git diff --check` for `GameBootstrapper.cs` returned clean. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-21 - Private Native Collection Risk Buckets

What was wrong: `privateNativeCollectionField=1316` was a blunt pressure map. It mixed real owner-local runtime native state, static signal/event bridge queues, static global native fields, Vault aliases/resolvers, editor/proof fields, and method-return signatures such as `private NativeArray<T> Resolve...`. Treating the raw number as one defect class would either hide real memory ownership debt or trigger destructive Vault churn.

What was done: Extended `Tools/PolishMandateStaticAudit.py` with additive private-native dimensions while preserving raw count. The tool now emits declaration kind, build surface, and primary risk bucket categories. Added unit tests for raw-total preservation, method returns, job-struct native views, editor-only surfaces, static queues, and Vault aliases. Wrote `Docs/Reports/PROJECT_AUDIT_private_native_collection_triage.md`.

Cinematic Cheats used: None. This is evidence/tooling work. The engineering cheat is rejecting a fake blanket migration: owner-local scratch can remain local when lifetime/fences are self-contained, while cross-domain/save/replay/blackbox/shared ownership must be route-carded.

Exact Microseconds saved: 0 us measured. Static outcome: raw `privateNativeCollectionField` remains `1316 files=229`. Primary risk buckets are `776` owner-local runtime native fields, `218` static signal/event bridge fields, `117` static global native fields, `131` method-returning native collection signatures, `29` Vault alias/resolver rows, and `45` editor/proof rows. Declaration, build-surface, and primary-risk sums each equal `1316`.

Evidence: `python Tools\test_polish_mandate_static_audit.py` ran 9 tests OK. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_private_native_risk_buckets.json --report-path Docs\Reports\PROJECT_AUDIT_polish_private_native_risk_buckets.md` returned `PASS_WITH_WARNINGS`. Top owner-local runtime native offenders are `DestructibleOrganicManager`, `PlayerInventory`, `LogisticsNetworkGraph`, `HectonFluidEngine`, `GasDynamicsSolver`, `SubmarineAtmosphereSystem`, `TetherInstance`, and `WorldChunkResidencyManager`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-21 - Mutable Native API Exposure Buckets

What was wrong: The previous private-native report did not catch the outward API problem: many public/internal/protected methods and properties expose mutable native collection views. These methods often look like read accessors, snapshots, editor helpers, or debug readbacks, but their signatures hand callers write-capable `NativeArray`, `NativeList`, `NativeHashMap`, or `NativeQueue` handles.

What was done: Extended `Tools/PolishMandateStaticAudit.py` with `nativeCollectionPublicMutableApiExposure` plus additive exposure-kind, build-surface, and primary-risk buckets. Added regression assertions to `Tools/test_polish_mandate_static_audit.py`. Wrote `Docs/Reports/PROJECT_AUDIT_native_api_exposure_triage.md` and updated the private-native triage with the companion report pointer.

Cinematic Cheats used: None. This is evidence/tooling work. The architectural cheat is refusing a fake mass signature rewrite: use read-only adapters and explicit writer-lock routes incrementally, with legacy wrappers until consumers are migrated.

Exact Microseconds saved: 0 us measured. Static outcome: `nativeCollectionPublicMutableApiExposure=274 files=97`; 260 are player-runtime surfaces, 5 editor-only, 9 QA/dev-proof. Exposure kind: 87 direct mutable returns/properties, 187 `out/ref` mutable views, 0 ambiguous. Primary risk: 21 core Vault/allocator surfaces, 14 editor/proof surfaces, 160 runtime `out/ref` mutable views, and 79 runtime mutable return/property views.

Evidence: `python Tools\test_polish_mandate_static_audit.py` ran 10 tests OK. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_native_api_exposure.json --report-path Docs\Reports\PROJECT_AUDIT_polish_native_api_exposure.md` returned `PASS_WITH_WARNINGS`. Top runtime mutable API offenders are `HectonMapMagicVegetationBridge`, `HabitatGraphManager`, `Shinobu19EconomyLedger`, `VoxelDynamicNavGridRuntime`, and `BuoyancyDisplacementRuntime`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-21 - Habitat Graph Read-Only Native Accessors

What was wrong: `HabitatGraphManager` exposed eight graph SoA buffers as internal mutable `NativeArray<T>` properties. Current external users in `ConstructionManager` and `SpatialAudioManager` only read those arrays, so the mutable signatures were unnecessary authority leakage.

What was done: Changed `Nodes`, `EdgeOffsets`, `EdgeDestinations`, `EdgeResistance`, `RoomWaterLevels`, `RoomVolumes`, `RoomFlags`, and `EdgeFlags` to `NativeArray<T>.ReadOnly`. Updated `ConstructionManager` save topology extraction and `SpatialAudioManager` acoustic portal extraction to use read-only views. Left `RoomConnections` unchanged because the hash-map read-only route needs separate consumer/API proof.

Cinematic Cheats used: None. This is authority-surface reduction. Existing acoustic portal graph is still a cheap topology sampling route, not a physical sound simulation.

Exact Microseconds saved: 0 us measured. Static outcome: mutable native API exposure dropped from `274` to `266`; direct mutable return/property exposure dropped from `87` to `79`; runtime mutable return/property risk dropped from `79` to `71`.

Evidence: `python Tools\test_polish_mandate_static_audit.py` ran 10 tests OK. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_native_api_exposure.json --report-path Docs\Reports\PROJECT_AUDIT_polish_native_api_exposure.md` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=266 files=97`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-21 - Diagnostic Mutable Native API Split

What was wrong: The mutable native API report mixed gameplay-looking read accessors with player-runtime methods that are named or typed as diagnostics: `ForEditor`, `Debug`, `Readback`, `Snapshot`, `Telemetry`, `Inspector`, or `Gizmo`. Those methods are not safe just because of their names, but they need a different migration queue.

What was done: Added `nativeApiRiskRuntimeDiagnosticNamedMutableView` to `Tools/PolishMandateStaticAudit.py` and updated the regression test so CamelCase names like `TryResolveTuningForEditor` are classified correctly. Updated native API triage reports with the new bucket.

Cinematic Cheats used: None. This is evidence/tooling work.

Exact Microseconds saved: 0 us measured. Static outcome: raw mutable API exposure is preserved at `268 files=97`; diagnostic/editor-named runtime mutable views are split into `61 files=36`, leaving `114` gameplay-looking runtime `out/ref` mutable views and `58` gameplay-looking runtime mutable return/property views.

Evidence: `python Tools\test_polish_mandate_static_audit.py` ran 10 tests OK. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_native_api_exposure.json --report-path Docs\Reports\PROJECT_AUDIT_polish_native_api_exposure.md` returned `PASS_WITH_WARNINGS`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-21 - Unity Time Risk Buckets and Fixed-Delta Cleanup

What was wrong: `unityTimeCritical=964` was not actionable. It mixed `Time.frameCount` telemetry stamps, `Time.time` cooldowns, editor/proof rows, and true `Time.deltaTime/fixedDeltaTime` simulation reads.

What was done: Extended `Tools/PolishMandateStaticAudit.py` with Unity time kind/build/risk buckets and added unit coverage. Replaced `Time.fixedDeltaTime` in `FaunaBrain.TryResolvePredatorLungeCcdPosition()` with cached dispatcher `FixedTick(float fdt)`. Replaced `Time.fixedDeltaTime` in `SubmarineFluidDynamics.UpdateBrineHullBreachState()` with `_currentFixedDeltaTime`, already assigned from dispatcher `FixedTick(float fixedDeltaTime)`. Wrote `Docs/Reports/PROJECT_AUDIT_unity_time_triage.md`.

Cinematic Cheats used: Existing predator lunge CCD remains the Dear Lie: a sweep guard over a teleported lunge presentation instead of full continuous physics simulation. No new physics was added.

Exact Microseconds saved: 0 us measured. Static outcome: `unityTimeCritical` dropped from `964` to `962`; `unityTimeRiskGameplayDelta` dropped from `3` to `1`. Remaining buckets: `806` frame stamp/telemetry rows, `80` gameplay wall-clock rows, `38` cooldown/perf-log rows, `37` editor/proof rows, and one gameplay delta row in shoreline foam presentation.

Evidence: `python Tools\test_polish_mandate_static_audit.py` ran 11 tests OK. `python -X faulthandler Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_risk_buckets.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_risk_buckets.md` returned `PASS_WITH_WARNINGS` with `unityTimeRiskGameplayDelta=1 files=1`. Focused `rg -n "Time\.(deltaTime|fixedDeltaTime)"` over the three inspected files now leaves only `ShorelineFoamGraftContracts.cs:616`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Destructible Organic Owner Clock

What was wrong: `DestructibleOrganicManager` used `Time.time` as authority time for organic gameplay facts: corpse resource expiry, decomposition start, partial damage metadata, wilt suppression, untouched overgrowth, mature spore acoustic cadence, tool-hit touch time, regrowth finalization, and Dear Lie regeneration restore windows. This was the top wall-clock file in the time triage and not safe to dismiss as telemetry.

What was done: Added `_organicClockSeconds`, advanced only from dispatcher `Tick(float deltaTime)`, and routed all owner-state timing through `ResolveOrganicClockSeconds()`. `Time.frameCount` remains only for frame-stamp telemetry and scheduling windows. `Time.realtimeSinceStartupAsDouble` remains only for Dear Lie job microsecond telemetry, not gameplay state.

Cinematic Cheats used: Existing Dear Lie flora destruction remains intact: spatial hash + Burst lane resolution + visual regen queue instead of GameObject/physics destruction simulation. This patch makes that fake use owner time rather than Unity wall clock.

Exact Microseconds saved: 0 us measured. Static outcome: `unityTimeCritical` dropped from `962` to `940`; `unityTimeRiskGameplayWallClock` dropped from `80` to `60`.

Evidence: Focused `rg` finds no `Time.time`, `Time.deltaTime`, or `Time.fixedDeltaTime` in `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`. `python -X faulthandler Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_organic_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_organic_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=940`, `unityTimeWallClock=97`, and `unityTimeRiskGameplayDelta=1`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - MigrationDirector Fallback Timeline

What was wrong: `MigrationDirector` used `Time.time` as the fallback for game-time timeline math when `CelestialEngine` was unavailable. That fallback drove migration field seasonal phase, blood-cloud POI expiry, and statistical swarm state timestamps.

What was done: Added `_fallbackTimelineGameSeconds`, advanced by the existing bounded cold-tick delta. Changed migration timeline call sites to use `ResolveTimelineGameSeconds(0f)`, preserving `CelestialEngine.GameTime` as the primary authority and using the local fallback only when the celestial owner is absent.

Cinematic Cheats used: Existing statistical swarm population remains the fake: O(1) population cells and POI bias replace materialized boids. No new AI simulation was added.

Exact Microseconds saved: 0 us measured. Static outcome: `unityTimeCritical` dropped from `940` to `936`; `unityTimeRiskGameplayWallClock` dropped from `60` to `56`.

Evidence: Focused `rg -n "Time\.time" Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs` returns no rows. `python -X faulthandler Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_migration_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_migration_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=936`, `unityTimeWallClock=93`, and `unityTimeRiskGameplayWallClock=56`. `Time.unscaledTime` remains only as cold-tick cadence pending a dispatcher slow-tick delta route. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - FaunaBrain Owner-Time Cleanup

What was wrong: `FaunaBrain` still used `Time.time` for combat mobility duration, hibernation sleep-start records, dev slow-tick watchdog throttling, and corpse-bloat shader start time. The first three are owner/proof timing routes; the corpse-bloat row is a shader presentation bridge.

What was done: Combat mobility now compares against `_cognitionTimeSeconds`. Tier-2 hibernation state now writes sleep start from `SystemDispatcher.ActiveRuntimeInstance.DilatedTimeSeconds`, matching `FaunaDirector` restore/catch-up math. The dev watchdog now throttles by `Time.frameCount` instead of wall-clock seconds. The corpse-bloat shader start remains `Time.time` because `Hecton_LeviathanOrganic.shader` computes age from Unity `_Time.y`.

Cinematic Cheats used: Existing corpse bloat remains the fake: material time and shader deformation replace CPU corpse physiology simulation. Existing predator lunge/telegraph fakes are unchanged.

Exact Microseconds saved: 0 us measured. Static outcome: `unityTimeCritical` dropped from `936` to `932`; `unityTimeRiskGameplayWallClock` dropped from `56` to `53`.

Evidence: Focused `rg -n "Time\.time|Time\.deltaTime|Time\.fixedDeltaTime" Assets/_Project/Scripts/Fauna/FaunaBrain.cs` leaves only `ArmCorpseBloatShaderTimer()` feeding `_CorpseBloatStartTime`. `python -X faulthandler Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_fauna_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_fauna_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=932`, `unityTimeWallClock=88`, and `unityTimeRiskGameplayWallClock=53`. `git diff --check -- Assets/_Project/Scripts/Fauna/FaunaBrain.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Spectrum Sonar Shader Clock Alignment

What was wrong: `SpectrumSystem` used `Time.time` for active sonar pulse/reveal/echo timing, but those values are consumed by shaders that compute wave age from `_Time.y`. `HectonSonarPointCloudFeature` compared the same reveal global against `Time.unscaledTime`, and `HectonMarineSnowRenderer` compared it against `Time.time`, creating mixed visual clocks around one shader global.

What was done: Added `ResolveUnityShaderTimeSeconds()` in `SpectrumSystem` and routed pulse, echo, reveal, and active-sonar geo timing through `Time.timeSinceLevelLoad`. Updated point-cloud history and marine-snow glow lifetime checks to compare `_SonarRevealExpireTime` against `Time.timeSinceLevelLoad` as well.

Cinematic Cheats used: The existing sonar reveal remains a Dear Lie: shader wavefronts, point-cloud history, and marine-snow glow sell the ping instead of CPU fluid/acoustic propagation simulation. No physics, DTO, AUP, or SignalBus route changed.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=932` to `927` and from `unityTimeRiskGameplayWallClock=53` to `48`. This is not a claim that Unity time is gone from presentation; `Time.timeSinceLevelLoad` remains the shader-compatible clock and is not counted by the current `Time.time\b` detector.

Evidence: Focused `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/Visor/SpectrumSystem.cs Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs` returns no rows. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_spectrum_shader_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_spectrum_shader_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=927`, `unityTimeWallClock=83`, and `unityTimeRiskGameplayWallClock=48`. `git diff --check` on the touched sonar files reports only LF-to-CRLF warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - HectonBoidController Acoustic Owner Clock

What was wrong: `HectonBoidController` used `Time.time` to expire acoustic ping panic and timestamp acoustic signal consumption. This path is not a shader `_Time` bridge: `BoidSimulation.compute` consumes `_AcousticPingParams.w` as an active flag, and C# decides whether the shockwave is alive.

What was done: Added `_boidClockSeconds`, advanced from dispatcher `Tick(float deltaTime)` with finite guards. Acoustic ping registration and expiry now use `ResolveBoidClockSeconds()`. The boid GPU struct, ping-pong buffers, compute shader ABI, and acoustic signal DTO were not changed.

Cinematic Cheats used: Existing fish panic remains a GPU fake: one acoustic shockwave vector and scalar active flag drive flock scattering instead of CPU per-fish acoustic physics.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeRiskGameplayWallClock=48` to `45`, and from `unityTimeWallClock=83` to `80`. `unityTimeCritical` is now `926`.

Evidence: Focused `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/HectonBoidController.cs` returns no rows. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_boid_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_boid_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=926`, `unityTimeWallClock=80`, and `unityTimeRiskGameplayWallClock=45`. `git diff --check -- Assets/_Project/Scripts/HectonBoidController.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Topographical Sonar Owner Clock

What was wrong: `TopographicalSonarSynthesizer` used `Time.time` for ping cooldown, scan start timestamp, and shader `PingSignal.x` age. `Hecton_SonarPoint.shader` consumes `PingSignal.x` directly as ping age, so this was owner-time debt, not a Unity shader `_Time` bridge.

What was done: Added bounded `_sonarClockSeconds`, advanced from `Render(float deltaTime)` before render early returns. Late-frame ping cadence, `_lastPingTimeSeconds`, `_lastScheduledPingTimeSeconds`, and shader-global ping age now use `ResolveSonarClockSeconds()`.

Cinematic Cheats used: Existing topographical sonar remains a fake: ping-scheduled Burst SDF raymarch, compact hit buffer, point-cloud indirect draw, and GPU point fade replace continuous terrain physics or acoustic propagation.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=926` to `923`, from `unityTimeWallClock=80` to `77`, and from `unityTimeRiskGameplayWallClock=45` to `42`.

Evidence: Focused `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs` returns no rows. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_topographical_sonar_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_topographical_sonar_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=923`, `unityTimeWallClock=77`, and `unityTimeRiskGameplayWallClock=42`. `git diff --check -- Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - PlayerPDA Owner Clock

What was wrong: `PlayerPDA` used `Time.time` for open start, normal close duration, force-close duration, and debug open duration. That duration is UI state, but it is also emitted through `PDAEvents.RaiseClosed(duration)`, so it should not depend on Unity wall-clock seconds.

What was done: Added bounded `_pdaClockSeconds`, advanced from dispatcher `Tick(float deltaTime)`. `Open`, `Close`, `ForceClose`, and diagnostics now use `ResolvePdaClockSeconds()` / `ResolvePdaOpenDurationSeconds()`.

Cinematic Cheats used: Existing PDA remains a UI/RenderTexture fake with preallocated tab history and event payloads. No extra simulation, no event DTO mutation, no input-route mutation.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=923` to `918`, from `unityTimeWallClock=77` to `73`, and from `unityTimeRiskGameplayWallClock=42` to `39`.

Evidence: Focused `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/PlayerPDA.cs` returns no rows. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_player_pda_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_player_pda_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=918`, `unityTimeWallClock=73`, and `unityTimeRiskGameplayWallClock=39`. `git diff --check -- Assets/_Project/Scripts/PlayerPDA.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - HabitatGraphManager Owner Clock

What was wrong: `HabitatGraphManager` used `Time.time` for analytical low-tier stress feedback cooldown and the `timeSeconds` value feeding analytical breach-gate traversal. Both affect habitat stress/breach behavior and were not safe to leave on Unity wall clock.

What was done: Added bounded `_habitatClockSeconds`, advanced from `ApplyHydrodynamicStress(float deltaTime)`. Analytical feedback cooldown and breach-gate `timeSeconds` now use `ResolveHabitatClockSeconds()`.

Cinematic Cheats used: Existing habitat stress remains analytical: graph stress, module scalar upload, shader displacement, and low-tier audio/camera feedback replace full structural finite-element simulation.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=918` to `915`, from `unityTimeWallClock=73` to `71`, and from `unityTimeRiskGameplayWallClock=39` to `37`.

Evidence: Focused `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` returns no rows. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_habitat_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_habitat_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=915`, `unityTimeWallClock=71`, and `unityTimeRiskGameplayWallClock=37`. `git diff --check -- Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - FoveatedSimulationManager Clock Reconciliation

What was wrong: The working tree contained `Time.time` in `FoveatedSimulationManager.LockTier0` and `ApplyImportanceResults`, affecting tier0 combat lock expiry and cadence classification.

What was done: Reconciled those rows to the dispatcher-owned foveated clock route and added `_foveatedClockSeconds` reset in `ResetRuntimeState()`. The existing clock route was already present in `HEAD`; the net file diff to `HEAD` is the reset line.

Cinematic Cheats used: Existing foveated simulation remains a cadence fake: scoring jobs, tiered tick intervals, and visual interpolation avoid per-target 60 Hz simulation at distance.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=915` to `913`, from `unityTimeWallClock=71` to `69`, and from `unityTimeRiskGameplayWallClock=37` to `35`.

Evidence: Focused `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` returns no rows. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_foveated_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_foveated_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=913`, `unityTimeWallClock=69`, and `unityTimeRiskGameplayWallClock=35`. `git diff --check -- Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - PersistentWorldRegistry Fauna-State Clock

What was wrong: `PersistentWorldRegistry` used `Time.time` to decide when cached fauna eggs hatch and to stamp hibernation records created from hatched/equilibrium fauna. That path affects saved temporary entity state.

What was done: Added bounded `_worldClockSeconds`, advanced from dispatcher `Tick(float dt)`. Egg hatch comparison and hibernation sleep-start creation now use `ResolveWorldClockSeconds()`. `Time.unscaledTime` remains only in sector override/paging commit cadence and tombstone scheduling.

Cinematic Cheats used: Existing persistent ecology remains a record fake: hibernated/egg/equilibrium entity records stand in for fully simulated off-screen fauna.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=913` to `910`, from `unityTimeWallClock=69` to `66`, and from `unityTimeRiskGameplayWallClock=35` to `32`.

Evidence: Focused `rg -n "Time\.time\b" Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` returns no rows. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_persistent_world_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_persistent_world_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=910`, `unityTimeWallClock=66`, and `unityTimeRiskGameplayWallClock=32`. `git diff --check -- Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Sargassum Thermal Shader Clock

What was wrong: `SargassumCutManager` used `Time.time` for recent cut heat stamp registration and shader-global pruning. Those values are consumed by `Hecton_ScooterVolumetricShafts.shader`, which computes thermal haze age from Unity shader `_Time.y`.

What was done: Added `ResolveThermalShaderClockSeconds()` and routed the two heat-stamp time reads through `Time.timeSinceLevelLoad`, preserving shader `_Time` compatibility while removing direct `Time.time` usage from the manager.

Cinematic Cheats used: Existing thermal scar/haze remains a Dear Lie: 16 compact heat stamps and shader noise/displacement sell post-cut heat instead of CPU thermal diffusion or fluid simulation.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=910` to `906`, from `unityTimeWallClock=66` to `64`, and from `unityTimeRiskGameplayWallClock=32` to `30`.

Evidence: Focused `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/World/SargassumCutManager.cs` returns no direct wall-clock rows; it only shows `Time.timeSinceLevelLoad` inside `ResolveThermalShaderClockSeconds()`. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_sargassum_shader_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_sargassum_shader_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=906`, `unityTimeWallClock=64`, and `unityTimeRiskGameplayWallClock=30`. `git diff --check -- Assets/_Project/Scripts/World/SargassumCutManager.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Vegetation Flow-Field Owner Clock

What was wrong: `VegetationFlowFieldIntegrator` used `Time.time` for threat propagation elapsed time and `Time.unscaledTime` for swarm wake impulse lifetime. Those values feed simulation jobs, not diagnostics.

What was done: Added `_vegetationRuntimeSeconds` to `HectonMapMagicVegetationBridge`, advanced it from dispatcher `Tick(float dt)`, and routed threat propagation plus wake impulse expiry through `ResolveVegetationRuntimeSeconds()`. Tick delta is sanitized before clock advancement.

Cinematic Cheats used: Existing abyssal flow remains a field approximation: grid diffusion, flow vectors, and one external wake impulse slot replace per-fish/per-particle fluid simulation.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=906` to `902`, from `unityTimeWallClock=64` to `62`, and from `unityTimeRiskGameplayWallClock=30` to `28`.

Evidence: Focused `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` returns no rows. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_vegetation_flow_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_vegetation_flow_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=902`, `unityTimeWallClock=62`, and `unityTimeRiskGameplayWallClock=28`. `git diff --check -- Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` reports only LF-to-CRLF warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Chunk Residency HLOD Visual Clock

What was wrong: `WorldChunkResidencyManager` used `Time.time` to timestamp HLOD impostor spawn/fade and to cull expired fade-outs. The same file's `Time.unscaledTime` rows are memory-pressure purge timing and were intentionally left alone.

What was done: Added `_chunkResidencyRuntimeSeconds`, advanced from dispatcher `Tick(float deltaTime)`, and routed HLOD spawn/fade jobs through `ResolveChunkResidencyRuntimeSeconds()`.

Cinematic Cheats used: Existing HLOD impostors remain the streaming visual fake: lightweight impostor matrices and fade-outs stand in for fully resident chunk geometry.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=902` to `898`, from `unityTimeWallClock=62` to `60`, and from `unityTimeRiskGameplayWallClock=28` to `26`.

Evidence: Focused `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` leaves only the two adrenaline purge `Time.unscaledTime` rows. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_chunk_residency_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_chunk_residency_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=898`, `unityTimeWallClock=60`, and `unityTimeRiskGameplayWallClock=26`. `git diff --check -- Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - WorldCaveDirector Dispatcher Time

What was wrong: `WorldCaveDirector` throttled cave spawn evaluation with `Time.time`, affecting world-generation cadence.

What was done: Replaced the wall-clock throttle with `ResolveCaveEvaluationTimeSeconds()`, reading bounded dispatcher `DilatedTimeSeconds` when the runtime dispatcher exists.

Cinematic Cheats used: Existing cave spawn remains a strategic candidate fake: biome/zone rules and deterministic candidates feed cave generation instead of scanning every terrain point or running expensive continuous cave discovery.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=898` to `894`, from `unityTimeWallClock=60` to `58`, and from `unityTimeRiskGameplayWallClock=26` to `24`.

Evidence: Focused `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.fixedDeltaTime\b|Time\.deltaTime\b" Assets/_Project/Scripts/WorldCaveDirector.cs` returns no rows. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_world_cave_dispatcher_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_world_cave_dispatcher_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=894`, `unityTimeWallClock=58`, and `unityTimeRiskGameplayWallClock=24`. `git diff --check -- Assets/_Project/Scripts/WorldCaveDirector.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Surface/Biome/Pipe/Drone Time Route Cleanup

What was wrong: Four direct wall-clock rows remained in real runtime paths: surface rain splash impulse timestamp, biome seismic dust cooldown, flexible-pipe rupture reveal timestamp, and drone phantom-flow sampling time. Three were presentation shader `_Time` bridges; one was Burst job simulation input.

What was done: `SurfaceWeatherVfxRig` and `ConnectionSplineBatchRenderer` now use explicit shader-clock helpers backed by `Time.timeSinceLevelLoad`. `BiomeMatrixDirector` now reads bounded dispatcher `DilatedTimeSeconds` for seismic dust cooldown. `DroneFleetManager` now owns a bounded headless simulation clock advanced from sanitized `Tick(deltaTime)` and feeds it into `DroneCognitionJob.PhantomFlowTime`.

Cinematic Cheats used: Rain ripples and pipe rupture reveal remain shader fakes; drone phantom flow remains a sampled current field rather than per-particle fluid simulation; biome dust remains event-triggered decal/VFX, not terrain physics.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=894` to `890`, from `unityTimeWallClock=58` to `54`, and from `unityTimeRiskGameplayWallClock=24` to `20`.

Evidence: Focused `rg -n "Time\.time\b|Time\.unscaledTime\b|Time\.deltaTime\b|Time\.fixedDeltaTime\b|timeSinceLevelLoad|ResolveWeatherShaderClockSeconds|ResolveBiomeMatrixClockSeconds|ResolvePipeShaderClockSeconds|ResolveHeadlessSimulationClockSeconds|s_HeadlessSimulationClockSeconds" Assets/_Project/Scripts/Atmosphere/SurfaceWeatherVfxRig.cs Assets/_Project/Scripts/BiomeMatrixDirector.cs Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs Assets/_Project/Scripts/Construction/DroneFleetManager.cs` shows no direct `Time.time`, `Time.unscaledTime`, `Time.deltaTime`, or `Time.fixedDeltaTime`; only two `Time.timeSinceLevelLoad` shader-clock helper rows remain. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_surface_biome_pipe_drone_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_surface_biome_pipe_drone_clock.md` returned `PASS_WITH_WARNINGS`. `git diff --check --` on touched files reports only LF-to-CRLF warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Current/Atmosphere/Celestial/Decal Shader Time Route Cleanup

What was wrong: Six remaining runtime time rows mixed real owner-state clocks and shader presentation clocks: `CurrentVolume` sampled currents from `Time.time`; `AbyssalFluidDecalManager` sampled decal current advection from `Time.time`; `HectonAtmosphereManager` and `HectonCelestialEngine` accumulated slow-tick timeline state from Unity wall clocks; `RandomEventSystem` and `VoxelDeltaProcessor` stamped shader effects with direct `Time.time`.

What was done: `CurrentVolume`, `HectonAtmosphereManager`, and `HectonCelestialEngine` now read bounded dispatcher `DilatedTimeSeconds`. `AbyssalFluidDecalManager` owns `_fluidDecalClockSeconds`, advanced from sanitized dispatcher tick delta. Meteor water impact and voxel laser heat stamps now use explicit `timeSinceLevelLoad` shader-clock helpers.

Cinematic Cheats used: Authored current pulses remain cheap noise/triangle-wave fields; abyssal fluid aftermath remains quad/decal drift instead of fluid simulation; meteor shock and laser heat remain scalar shader age fakes.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=890` to `883`, from `unityTimeWallClock=54` to `48`, and from `unityTimeRiskGameplayWallClock=20` to `14`.

Evidence: Focused scan over the six touched files shows no direct `Time.time`, `Time.unscaledTime`, `Time.deltaTime`, or `Time.fixedDeltaTime`; only `Time.timeSinceLevelLoad` remains in explicit shader-clock helpers for meteor impact and voxel cut heat. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_current_atmo_celestial_decal_shader_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_current_atmo_celestial_decal_shader_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=883`, `unityTimeWallClock=48`, and `unityTimeRiskGameplayWallClock=14`. `git diff --check --` on touched files reports only LF-to-CRLF warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Shader/Presentation Residual Time Cleanup

What was wrong: Eight-plus residual wall-clock rows were not one class of bug. Some were shader `_Time` bridge stamps, some were owner-state visual compute clocks, and one helper used `Time.realtimeSinceStartup` as a hidden fallback for flora simulation time.

What was done: Corpse bloat, micro-fauna hit flash, and observer-relative celestial realtime mode now use explicit presentation helpers backed by `Time.timeSinceLevelLoad`. Submarine leak plume compute now gets `_leakPlumeClockSeconds` advanced from sanitized fixed-step delta. HectonFluid fallback time reads bounded dispatcher `DilatedTimeSeconds`. Flora parasite pulse and wake-trail compute read `GetCurrentSimulationTimeSeconds()`, and that helper now falls back to dispatcher time instead of realtime.

Cinematic Cheats used: Corpse bloat and hit flash remain shader VAT/material fakes; leak plume remains one compute dispatch emitting four points per breach; parasite pulse and wake trail remain shader/compute scalar fields rather than per-leaf or per-fluid simulation.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=883` to `877`, from `unityTimeWallClock=48` to `42`, and from `unityTimeRiskGameplayWallClock=14` to `8`.

Evidence: Focused scan over the touched files shows no direct `Time.time`, `Time.unscaledTime`, `Time.deltaTime`, `Time.fixedDeltaTime`, or `Time.realtimeSinceStartup` in the patched paths; `Time.timeSinceLevelLoad` remains only in explicit presentation helpers. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_shader_presentation_owner_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_shader_presentation_owner_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeCritical=877`, `unityTimeWallClock=42`, and `unityTimeRiskGameplayWallClock=8`. `git diff --check --` on touched files reports only LF-to-CRLF warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Residual Gameplay Time Bucket Cleared

What was wrong: The broad static audit still showed `unityTimeRiskGameplayWallClock=8` and `unityTimeRiskGameplayDelta=1` after the shader/presentation pass. Residual rows covered player fixed/render interpolation, footstep cadence, LOD cleanup cadence, scatter candidate acceptance, shoreline foam delta, sargassum entanglement audio cooldown, and RenderTexture leak-age diagnostics.

What was done: Verified current source routes for player interpolation through `HectonFloatingOrigin.CurrentFixedInterpolationAlpha`, footstep/LOD/scatter through owner or sampling clocks, shoreline foam through `VisualSyncTick` `timing.FrameDelta`, and sargassum audio through fixed-step influence time. Patched the remaining net diff in `RenderTextureLifecycleTracker`: allocation timestamps and leak-age checks now use dispatcher-owned unscaled lifecycle seconds through `ResolveLifecycleClockSeconds()`.

Cinematic Cheats used: Shoreline foam stays a bounded 64-entry shader/compute fake; LOD cleanup and footstep audio stay tick-local cadence state; RenderTexture leak detection stays cold diagnostics, not gameplay simulation.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped from `unityTimeCritical=877` to `868`, from `unityTimeWallClock=42` to `34`, from `unityTimeRiskGameplayWallClock=8` to `0`, and from `unityTimeRiskGameplayDelta=1` to `0`.

Evidence: Focused scan over the residual files finds no direct `Time.time`, `Time.fixedTime`, `Time.deltaTime`, `Time.fixedDeltaTime`, or `Time.realtimeSinceStartup`. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_time_after_render_texture_lifecycle_clock.json --report-path Docs\Reports\PROJECT_AUDIT_polish_time_after_render_texture_lifecycle_clock.md` returned `PASS_WITH_WARNINGS` with `unityTimeRiskGameplayWallClock=0`, `unityTimeRiskGameplayDelta=0`, `unityTimeCritical=868`, and `unityTimeWallClock=34`. Remaining `unityTimeDelta=1` is `Assets/_Project/Scripts/Dev/CelestialTimeLapseDebugger.cs:30`. `git diff --check -- Assets/_Project/Scripts/Optimization/RenderTextureLifecycleTracker.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Quest DAG Teardown Fence Cleanup

What was wrong: `QuestDagResolverRuntime.Dispose()` directly called `.Complete()` on the scheduled resolver handle and the native disposal handle. This is a cold teardown path, but it still bypassed the Core-owned job fence policy.

What was done: Replaced both direct completions with `DispatcherJobFence.TryComplete(..., forceComplete: true)`. DAG scheduling, black-box telemetry, Vault buffer handles, and `Dispose(JobHandle)` dependency chaining were left unchanged.

Cinematic Cheats used: None added. The existing quest DAG remains bitmask state resolution and trigger spatial hashing, not per-quest object polling.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `jobHandleComplete` from `114 files=32` to `112 files=31`.

Evidence: Focused `rg -n "\.Complete\(\)|DispatcherJobFence\.TryComplete|Dispose\(" Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs` shows only the two `DispatcherJobFence.TryComplete` calls and no direct `.Complete()`. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_quest_dag_fence_cleanup.json --report-path Docs\Reports\PROJECT_AUDIT_polish_after_quest_dag_fence_cleanup.md` returned `PASS_WITH_WARNINGS` with `jobHandleComplete=112 files=31`. `git diff --check -- Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs` reports only LF-to-CRLF warning. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Vegetation Native Read-Only Payload Narrowing

What was wrong: `HectonMapMagicVegetationBridge` had public mutable native payload debt: owner-owned front-buffer snapshots for abyssal anchors, AUP anchors, ecosystem threat grids, compressed threat grids, and terrain-hole streaming were exposed as writable `NativeArray<T>` values to external readers.

What was done: Converted those five payload APIs to `NativeArray<T>.ReadOnly` and updated the five observed call-site files to read-only declarations. The call sites now use `Length` fail-closed guards instead of relying on mutable-array `IsCreated` checks.

Cinematic Cheats used: No new physical simulation was added. Existing terrain-hole, sonar anchor, HUD threat, and boid threat-grid paths remain zero-copy snapshot reads feeding presentation/proxy systems rather than duplicating data or simulating per-plant/per-cell truth outside the vegetation owner.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `268` at the start of the native-exposure pass to `236`; `nativeApiExposureOutRefMutable` dropped from `189` to `184`.

Evidence: Focused scans found no stale mutable call-site declarations for `TryGetActiveAbyssalAnchorPayload`, `TryGetActiveAbyssalAnchorAupPayload`, `TryGetEcosystemThreatGridPayload`, `TryGetCompressedEcosystemThreatGridPayload`, or `TryGetTerrainHoleStreamingPayload`, and no read-only `.IsCreated` leftovers in the touched call sites. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_vegetation_readonly_payloads.json --report-path Docs\Reports\PROJECT_AUDIT_polish_after_vegetation_readonly_payloads.md` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=236`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Vegetation No-Call-Site Snapshot Narrowing

What was wrong: The vegetation bridge still had public mutable native snapshot APIs with no first-party call sites in static search. These were unnecessary writable seams into owner-owned front buffers.

What was done: Converted no-call-site flow, abyssal nav-node, current-conduit, nav-graph node array, threat-echo, mega-wreck, canopy, and nav-node-type payload outputs to `NativeArray<T>.ReadOnly`. Left primary active surface/underwater payload methods and the nav-graph spatial hash unchanged because their write/mutation assumptions need separate call-site or container proof.

Cinematic Cheats used: None added. This is API surface hardening for existing vegetation snapshots and presentation/proxy consumers.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `236` to `225`; `nativeApiExposureOutRefMutable` dropped from `184` to `173`.

Evidence: Focused search for the converted method names found declarations only, with no first-party call sites. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_vegetation_readonly_nocallsite_payloads.json --report-path Docs\Reports\PROJECT_AUDIT_polish_after_vegetation_readonly_nocallsite_payloads.md` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=225`, `nativeApiExposureBuildPlayerRuntime=211`, and `nativeApiExposureOutRefMutable=173`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Voxel Passability Read-Only Snapshot Narrowing

What was wrong: `VoxelDynamicNavGridRuntime` returned mutable passability grid arrays through three read accessors. The observed consumer, vegetation path smoothing, already treats the grid as `[ReadOnly, NoAlias]` job input.

What was done: Converted `TryGetPassabilityPayload`, `TryGetContainingPassabilityPayload`, and `TryGetNearestPassabilityPayload` to `NativeArray<byte>.ReadOnly`. Updated `VegetationNavGridSynchronizer` and the `StringPullPathJob` field to carry the read-only view. Build-time passability buffers, pure-void scan buffers, and owner mutation paths were left mutable.

Cinematic Cheats used: No new physical simulation was added. The existing abyssal path string-pull/DDA route remains a cheap voxel/passability sampling fake instead of a physics or collider query path.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `225` to `222`; `nativeApiExposureOutRefMutable` dropped from `173` to `170`.

Evidence: Focused scans found no stale `out NativeArray<byte>` passability declarations. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_voxel_passability_readonly.json --report-path Docs\Reports\PROJECT_AUDIT_polish_after_voxel_passability_readonly.md` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=222`, `nativeApiExposureBuildPlayerRuntime=208`, `nativeApiExposureOutRefMutable=170`, and `nativeApiRiskRuntimeOutRefMutableView=95`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - PDA Read-Only Snapshot Narrowing

What was wrong: `PlayerExplorationTracker.TryGetExplorationMaskPayload` and `TryBuildCartographyRleRuns` exposed owner-owned native snapshots as writable arrays even though external mutation is not required.

What was done: Converted both APIs to `NativeArray<T>.ReadOnly`. The editor-only RLE call site discards the buffer and only reads the run count. Discovered-sector and packed-upload routes were left mutable because the current graphics upload utility still accepts `NativeArray<T>` and needs a separate proof pass.

Cinematic Cheats used: No new simulation was added. PDA cartography remains a packed/RLE data projection instead of per-cell GameObject or texture-object churn.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `222` to `220`; `nativeApiExposureOutRefMutable` dropped from `170` to `168`.

Evidence: Focused scans found no first-party mutable call-site declarations for the two narrowed APIs. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_pda_readonly_snapshots.json --report-path Docs\Reports\PROJECT_AUDIT_polish_after_pda_readonly_snapshots.md` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=220`, `nativeApiExposureBuildPlayerRuntime=206`, `nativeApiExposureOutRefMutable=168`, and `nativeApiRiskRuntimeOutRefMutableView=93`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Construction Occupancy Read Accessor Narrowing

What was wrong: `ModularBaseConstructionValidator.TryReadOccupancyHashTable` exposed the construction occupancy hash table as writable even though the method is a read accessor with no first-party call sites.

What was done: Converted that accessor to `NativeArray<BaseModuleOccupancyDTO>.ReadOnly`. Telemetry read/ensure and occupancy ensure/mutation helpers remain mutable because they feed writer paths.

Cinematic Cheats used: None added. Construction validation continues to use compact hash-table occupancy checks instead of scene object scans or physics queries.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `220` to `219`; `nativeApiExposureOutRefMutable` dropped from `168` to `167`.

Evidence: Focused scan found no first-party call sites for `TryReadOccupancyHashTable`. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_construction_readonly_occupancy.json --report-path Docs\Reports\PROJECT_AUDIT_polish_after_construction_readonly_occupancy.md` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=219`, `nativeApiExposureOutRefMutable=167`, `nativeApiExposureBuildQaDevProof=8`, and `nativeApiRiskEditorOrProofSurface=13`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Toxic Outgassing Readback Narrowing

What was wrong: `ToxicOutgassingChemistryRuntime.TryGetGridReadback` and `TryGetCellStates` exposed toxic chemistry owner buffers as mutable native arrays. The observed density reader is an editor gizmo; the state reader has no first-party call sites.

What was done: Converted both methods to `NativeArray<T>.ReadOnly`. Updated `ToxicOutgassingTunerWindow` to request a read-only density view and validate through `Length`.

Cinematic Cheats used: No new physical simulation was added. Toxic plume inspection remains a grid readback/visualization path over the existing chemistry field instead of per-cell scene objects or duplicated managed buffers.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `219` to `217`; `nativeApiExposureOutRefMutable` dropped from `167` to `165`.

Evidence: Focused scan found no stale mutable declarations for `TryGetGridReadback` or `TryGetCellStates`. `python Tools\PolishMandateStaticAudit.py --source-root Assets/_Project/Scripts --report-path Docs/Reports/PROJECT_AUDIT_polish_after_toxic_readonly_readbacks.md --json-path Docs/Reports/PROJECT_AUDIT_polish_after_toxic_readonly_readbacks.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=217`, `nativeApiExposureBuildPlayerRuntime=204`, `nativeApiExposureOutRefMutable=165`, and `nativeApiRiskRuntimeOutRefMutableView=92`. `git diff --check` on touched files and new audit artifacts produced no whitespace errors. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - HectonSeismicTideDirector Native API Exclusion

What was wrong: Static audit flags `HectonSeismicTideDirector` for mutable native out/ref exposure, but the hits are shared Vault acquisition/open helpers, pointer routes, and editor tuning writer views.

What was done: Read-only subagent triage classified `OpenOrAcquireVaultBuffer`, `TryOpenExistingVaultBuffer`, `TryOpenVaultBuffer`, `OpenVaultPointer`, and `TryResolveTuning`. No safe read-only narrowing was applied.

Cinematic Cheats used: None added. This was route protection, not simulation work.

Exact Microseconds saved: 0 us measured. Static debt unchanged by design.

Evidence: Subagent `019e4ce5-d823-70d2-99bf-5611a680fed2` reported no safe candidates: helper call sites include event slot, tuning, telemetry, output, CSV scratch, celestial buffer, commit/swap, and editor tuning writes. No source edit, Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Catalog Socket And Ecosystem Biomass Snapshot Narrowing

What was wrong: `BaseModuleCatalogRuntime.TryGetModuleSocketRangeFromVault` returned mutable catalog socket arrays to read-only construction graph/editor consumers. `EcosystemDirector.GetBiomassSaveSnapshotArray` returned a mutable biomass save snapshot despite having no first-party call sites.

What was done: Converted the base-module socket range route, `TryGetSocketRange`, and observed construction/editor socket range consumers to `NativeArray<SocketDefinitionDTO>.ReadOnly`. Converted only the biomass save snapshot accessor to `NativeArray<EcosystemBiomassSaveRun>.ReadOnly`.

Cinematic Cheats used: No new simulation. Construction still uses compact socket catalog indexing instead of scene scans, and ecosystem biomass save data stays a packed snapshot rather than object-level serialization churn.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `217` to `215`, `nativeApiExposureOutRefMutable` from `165` to `164`, and `nativeApiExposureMutableReturn` from `52` to `51`.

Evidence: Focused scans found read-only socket range declarations in `BaseModuleCatalogRuntime`, `HabitatConstructionManager`, `HabitatGraphManager`, and `BaseModuleCatalogEditorTools`, plus a read-only biomass accessor in `EcosystemDirector`. `python Tools\PolishMandateStaticAudit.py --source-root Assets/_Project/Scripts --report-path Docs/Reports/PROJECT_AUDIT_polish_after_catalog_ecosystem_readonly.md --json-path Docs/Reports/PROJECT_AUDIT_polish_after_catalog_ecosystem_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=215`, `nativeApiExposureBuildPlayerRuntime=202`, `nativeApiExposureOutRefMutable=164`, and `nativeApiExposureMutableReturn=51`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Data Monolith Resident Blob Read-Only Narrowing

What was wrong: `H8StaticDataArena.TryGetArena` and `TryGetResidentBlob` exposed resident static data bytes as mutable native arrays despite being documented as read-only blob accessors.

What was done: Converted both public accessors to `NativeArray<byte>.ReadOnly`. Left private `TryRefreshArenaView` mutable because boot load, validation, checksum, localization, and telemetry paths need owner-write or pointer access.

Cinematic Cheats used: None added. This protects static-data ownership; runtime consumers still use direct section spans/pointers instead of copied managed blobs.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `215` to `213`, and `nativeApiExposureOutRefMutable` from `164` to `162`.

Evidence: Focused scan shows only `TryGetArena(out NativeArray<byte>.ReadOnly ...)` and `TryGetResidentBlob(out NativeArray<byte>.ReadOnly ...)` as public resident blob accessors; remaining mutable arena views are private/internal owner paths. `python Tools\PolishMandateStaticAudit.py --source-root Assets/_Project/Scripts --report-path Docs/Reports/PROJECT_AUDIT_polish_after_datamonolith_readonly_blob.md --json-path Docs/Reports/PROJECT_AUDIT_polish_after_datamonolith_readonly_blob.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=213`, `nativeApiExposureBuildPlayerRuntime=200`, `nativeApiExposureOutRefMutable=162`, and `nativeApiRiskRuntimeOutRefMutableView=89`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Atmosphere Snapshot Read-Only Narrowing

What was wrong: `ShinobuOceanSurfaceAtmosphereRuntime.TryGetVaultSnapshot` and `TryGetReadbackDebugSnapshot` exposed atmosphere/wave/readback Vault snapshots as mutable native arrays to editor-side readers.

What was done: Converted both public snapshot APIs, the private existing-Vault read helper, the seed-ship quest mask reader, and `ShinobuAtmosphereWaveTunerWindow` consumers to `NativeArray<T>.ReadOnly`. Tuner write-lock, CSV hydration, wave/readback compute, and telemetry writer routes remain mutable.

Cinematic Cheats used: No simulation was added. The ocean surface debug path remains a compact Vault/readback snapshot and gizmo projection instead of copied managed arrays or per-sample scene objects.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `213` to `211`, and `nativeApiExposureOutRefMutable` from `162` to `160`.

Evidence: Focused scans found no stale mutable declarations for the two public atmosphere snapshot APIs. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_atmosphere_readonly_snapshots.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=211`, `nativeApiExposureBuildPlayerRuntime=198`, `nativeApiExposureOutRefMutable=160`, and `nativeApiRiskRuntimeOutRefMutableView=89`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Animation Matrix Editor-View Read-Only Narrowing

What was wrong: Procedural bone and kinetic character runtime editor matrix resolvers returned mutable matrix/parent native arrays to gizmo-only readers.

What was done: Converted both `TryResolveMatricesForEditor` APIs and their runtime/editor gizmo consumers to `NativeArray<T>.ReadOnly`. Tuning editor resolvers and runtime solve/upload buffers remain mutable because they write DTOs or feed owner jobs/GPU upload.

Cinematic Cheats used: No simulation was added. Animation debugging remains a cheap matrix-line gizmo projection instead of copied managed skeleton data or scene object probes.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `211` to `209`, and `nativeApiExposureOutRefMutable` from `160` to `158`.

Evidence: Focused scans found no stale mutable call-site declarations for `TryResolveMatricesForEditor`. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_animation_matrix_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=209`, `nativeApiExposureBuildPlayerRuntime=196`, `nativeApiExposureOutRefMutable=158`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=51`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Lighting Readback Read-Only Narrowing

What was wrong: Dynamic point-light and interior GI readback APIs exposed diagnostic/probe/light native views as mutable arrays to editor and gizmo consumers.

What was done: Converted six lighting readback APIs and observed consumers to `NativeArray<T>.ReadOnly`: dynamic point-light telemetry, states/sources, fake-bounce lights, interior GI probe grid, occlusion, and telemetry. Owner write lanes remain mutable.

Cinematic Cheats used: No simulation was added. The lighting debug path remains compact readback/gizmo projection; fake-bounce probe lights stay an owner-local scalar stream rather than a cross-owner job or scene-object light simulation.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `209` to `203`, and `nativeApiExposureOutRefMutable` from `158` to `152`.

Evidence: Focused scans found only read-only signatures/call sites for the narrowed lighting readbacks. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_lighting_readonly_readbacks.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=203`, `nativeApiExposureBuildPlayerRuntime=190`, `nativeApiExposureOutRefMutable=152`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=45`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Rollback Snapshot Read-Only Narrowing

What was wrong: Rollback visual state/history and telemetry snapshot APIs returned mutable native arrays to editor-only diagnostics.

What was done: Converted `TryGetVisualStates`, `TryGetVisualHistory`, `TryGetTelemetry`, and `TryGetInputPredictionTelemetry` to read-only views. Updated `RollbackNetcodeTunerWindow` to use read-only telemetry/state arrays and length checks.

Cinematic Cheats used: No simulation was added. Rollback diagnostics remain an editor/gizmo projection over existing ring buffers, not copied managed history or scene-object markers.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `203` to `199`, and `nativeApiExposureOutRefMutable` from `152` to `148`.

Evidence: Focused scans found only read-only rollback snapshot signatures/call sites. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_rollback_readonly_snapshots.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=199`, `nativeApiExposureBuildPlayerRuntime=186`, `nativeApiExposureOutRefMutable=148`, and `nativeApiRiskRuntimeOutRefMutableView=87`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Physics Debug Readback Read-Only Narrowing

What was wrong: Ballistics debug buffers, ballistics impact VFX staging, habitat fluid active compartment snapshots, and hydrodynamic KCC editor telemetry exposed owner-owned native arrays as mutable views to debug/editor readers.

What was done: Converted the selected physics/debug accessors and their observed consumers to `NativeArray<T>.ReadOnly`. Owner mutation, topology/source installation, runtime job buffers, and Vault open helpers remain mutable where they own writes.

Cinematic Cheats used: No simulation was added. Ballistics, compartment, and KCC diagnostics stay as cheap readback/gizmo overlays over existing native state instead of copied managed records or scene-object probes.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `199` to `194`, `nativeApiExposureOutRefMutable` from `148` to `143`, and `nativeApiExposureBuildPlayerRuntime` from `186` to `181`.

Evidence: Focused scans found only read-only signatures/call sites for the narrowed routes. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_physics_debug_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=194`, `nativeApiExposureBuildPlayerRuntime=181`, `nativeApiExposureOutRefMutable=143`, `nativeApiRiskRuntimeDiagnosticNamedMutableView=39`, and `nativeApiRiskRuntimeOutRefMutableView=86`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Diagnostic Readback Batch Narrowing

What was wrong: Several diagnostic/readback routes exposed mutable native arrays to read-only consumers: submarine thermal grid public readback, thermodynamics front/Vault grid readbacks, trade marauder editor state/route view, and habitat siege target snapshot.

What was done: Converted those selected APIs and editor/gizmo consumers to `NativeArray<T>.ReadOnly`. Preserved submarine thermal grid's private mutable helper for owner-side `GraphicsBuffer.SetData`, and left seaglide editor views mutable because tuning is edited through that path.

Cinematic Cheats used: No simulation was added. These systems stay as grid/gizmo/readback projections over existing native state rather than copied managed debug objects or scene proxies.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `194` to `189`, `nativeApiExposureOutRefMutable` from `143` to `138`, and `nativeApiExposureBuildPlayerRuntime` from `181` to `176`.

Evidence: Focused scans found only read-only selected signatures/call sites. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_diagnostic_readback_batch.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=189`, `nativeApiExposureBuildPlayerRuntime=176`, `nativeApiExposureOutRefMutable=138`, `nativeApiRiskRuntimeDiagnosticNamedMutableView=35`, and `nativeApiRiskRuntimeOutRefMutableView=85`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Ocean Debug Telemetry Read-Only Narrowing

What was wrong: Ocean single-pass telemetry and shoreline foam debug/telemetry readers returned mutable native arrays to diagnostic consumers.

What was done: Converted selected ocean/foam read accessors to `NativeArray<T>.ReadOnly` and updated the shoreline foam gizmo consumer. Runtime foam upload and telemetry writer paths remain mutable inside their owners.

Cinematic Cheats used: No simulation was added. Foam diagnostics remain a compact gizmo projection over GPU-oriented foam parameters, not copied managed debug state.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `189` to `186`, `nativeApiExposureOutRefMutable` from `138` to `135`, and `nativeApiExposureBuildPlayerRuntime` from `176` to `173`.

Evidence: Focused scans found no stale ocean/foam mutable call sites. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_ocean_debug_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=186`, `nativeApiExposureBuildPlayerRuntime=173`, `nativeApiExposureOutRefMutable=135`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=32`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Plasma Editor Mesh Snapshot Read-Only Narrowing

What was wrong: Plasma beam editor mesh snapshot exposed the runtime beam vertex buffer as a mutable native array to an editor gizmo reader.

What was done: Converted `ShinobuPlasmaBeamRuntime.TryGetEditorMeshSnapshot` and `PlasmaBeamTunerWindow` to `NativeArray<BeamVertexDTO>.ReadOnly`. Runtime vertex generation and upload ownership remain unchanged.

Cinematic Cheats used: No simulation was added. The editor mesh overlay remains a bounded triangle-wire visualization over existing VFX vertices, not copied managed mesh state.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `186` to `185`, `nativeApiExposureOutRefMutable` from `135` to `134`, and `nativeApiExposureBuildPlayerRuntime` from `173` to `172`.

Evidence: Focused scans found only read-only editor mesh snapshot call sites. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_plasma_editor_snapshot_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=185`, `nativeApiExposureBuildPlayerRuntime=172`, `nativeApiExposureOutRefMutable=134`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=31`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Pure Snapshot Read-Only Narrowing

What was wrong: Lore unlock words, HLOD registry entries, and visor AUP discovery grid exposed mutable native arrays through read-style APIs.

What was done: Converted the selected APIs to `NativeArray<T>.ReadOnly` and updated the PDA lore consumer. Internal writer routes for HLOD culling and visor discovery marking remain mutable.

Cinematic Cheats used: No simulation was added. HLOD and visor surfaces remain compact snapshot/projection data instead of managed debug copies.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `185` to `182`, `nativeApiExposureOutRefMutable` from `134` to `131`, and `nativeApiExposureBuildPlayerRuntime` from `172` to `169`.

Evidence: Focused scans found no stale mutable call sites for the selected APIs. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_pure_snapshot_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=182`, `nativeApiExposureBuildPlayerRuntime=169`, `nativeApiExposureOutRefMutable=131`, and `nativeApiRiskRuntimeOutRefMutableView=82`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Streaming Impostor Read-Only Contract Narrowing

What was wrong: The core streaming backpressure contract exposed active HLOD impostor arrays as mutable native arrays to cross-domain consumers.

What was done: Converted active impostor matrices/types and active impostor cartography points to `NativeArray<T>.ReadOnly` in the interface, owner implementation, and PDA map consumer. Owner buffers remain mutable only inside `WorldChunkResidencyManager`.

Cinematic Cheats used: No simulation was added. PDA/HLOD rendering still uses compact impostor point snapshots instead of scene-object scans.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `182` to `180`, `nativeApiExposureOutRefMutable` from `131` to `129`, and `nativeApiExposureBuildPlayerRuntime` from `169` to `167`.

Evidence: Focused scans found only read-only streaming impostor signatures/call sites. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_streaming_impostor_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=180`, `nativeApiExposureBuildPlayerRuntime=167`, `nativeApiExposureOutRefMutable=129`, and `nativeApiRiskRuntimeOutRefMutableView=80`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Fluid Property Read-Only Native Return Narrowing

What was wrong: `HectonFluidEngine.FloaterPositions` and `BuoyancyResults` exposed owner buffers as mutable public native-return properties.

What was done: Converted both properties to `NativeArray<T>.ReadOnly` aliases with default returns for uncreated owner arrays. Fluid engine internal mutation and GPU upload ownership remain unchanged.

Cinematic Cheats used: No simulation was added. The route remains a zero-copy owner-buffer view instead of creating debug copies or scene-object scans.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `180` to `178`, `nativeApiExposureBuildPlayerRuntime` from `167` to `165`, `nativeApiExposureMutableReturn` from `51` to `49`, and `nativeApiRiskRuntimeReturnMutableView` from `35` to `33`.

Evidence: Focused scan found no first-party call sites for those property names beyond the declarations and shader ID text. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_fluid_readonly_properties.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=178`, `nativeApiExposureBuildPlayerRuntime=165`, `nativeApiExposureMutableReturn=49`, and `nativeApiRiskRuntimeReturnMutableView=33`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Crab IK Property Read-Only Native Return Narrowing

What was wrong: `ProceduralCrabLegIKRuntime.FootPositions` and `TargetFootPositions` exposed Vault-backed IK buffers as mutable internal native-return properties.

What was done: Converted both property aliases to `NativeArray<float3>.ReadOnly`. Runtime-owned job buffers and writer phases remain mutable inside `ProceduralCrabLegIKRuntime`.

Cinematic Cheats used: No physics simulation was added. The crab leg route remains a bounded IK/pose visual fake over Vault buffers, not GameObject leg physics.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `178` to `176`, `nativeApiExposureBuildPlayerRuntime` from `165` to `163`, `nativeApiExposureMutableReturn` from `49` to `47`, and `nativeApiRiskRuntimeReturnMutableView` from `33` to `31`.

Evidence: Focused scan found no external property consumers; remaining hits are owner/job buffer fields and writes. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_crab_ik_readonly_properties.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=176`, `nativeApiExposureBuildPlayerRuntime=163`, `nativeApiExposureMutableReturn=47`, and `nativeApiRiskRuntimeReturnMutableView=31`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Scatter Backend Input Read-Only Seam Narrowing

What was wrong: `ScatterBackendBindingState.HeightSamples` and `CellStates` returned mutable binding buffers to the backend scheduling seam.

What was done: Converted the binding-state properties and backend schedule signatures to `NativeArray<T>.ReadOnly`. `ScatterEvaluator` now copies the read-only height input into its owner-local `_heightSamples` buffer by index before scheduling the existing Burst job.

Cinematic Cheats used: No placement simulation was added. The scatter backend still evaluates compact pre-sampled terrain/cell snapshots, not scene-object terrain queries.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `176` to `174`, `nativeApiExposureBuildPlayerRuntime` from `163` to `161`, `nativeApiExposureMutableReturn` from `47` to `45`, and `nativeApiRiskRuntimeReturnMutableView` from `31` to `29`.

Evidence: Focused scan found read-only scatter schedule signatures and no stale mutable binding properties. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_scatter_backend_readonly_inputs.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=174`, `nativeApiExposureBuildPlayerRuntime=161`, `nativeApiExposureMutableReturn=45`, and `nativeApiRiskRuntimeReturnMutableView=29`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Marching-Cubes Table Read-Only Native Return Narrowing

What was wrong: `MCTables.EdgeTable` and `TriTable` returned mutable static lookup tables.

What was done: Converted the static table properties and the two marching-cubes job table fields to `NativeArray<int>.ReadOnly`. Table initialization, sentinel registration, and disposal still own the underlying mutable arrays.

Cinematic Cheats used: No terrain physics was added. The voxel pipeline continues to use direct SDF/marching-cubes lookup tables instead of collider or scene queries.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `174` to `172`, `nativeApiExposureBuildPlayerRuntime` from `161` to `159`, `nativeApiExposureMutableReturn` from `45` to `43`, and `nativeApiRiskRuntimeReturnMutableView` from `29` to `27`.

Evidence: Focused scan found read-only static table properties/job fields and unchanged table lifecycle. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_mctables_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=172`, `nativeApiExposureBuildPlayerRuntime=159`, `nativeApiExposureMutableReturn=43`, and `nativeApiRiskRuntimeReturnMutableView=27`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Abyssal Flow Payload Read-Only Narrowing

What was wrong: Abyssal thermal and flow-volume payload APIs exposed bridge-owned simulation grids as mutable native arrays.

What was done: Converted `TryGetAbyssalThermalGridPayload` and both `TryGetAbyssalFlowVolumePayload` overloads to `NativeArray<T>.ReadOnly`. The drone manager and `DroneCognitionJob` now consume the abyssal flow volume as a read-only job input.

Cinematic Cheats used: No fluid simulation was added. The route remains a compact precomputed current volume sampled by drones instead of per-drone scene physics or fluid queries.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `172` to `169`, `nativeApiExposureOutRefMutable` from `129` to `126`, and `nativeApiExposureBuildPlayerRuntime` from `159` to `156`.

Evidence: Focused scan found read-only abyssal payload signatures/job field and unchanged owner writer buffers. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_abyssal_flow_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=169`, `nativeApiExposureBuildPlayerRuntime=156`, `nativeApiExposureOutRefMutable=126`, and `nativeApiRiskRuntimeOutRefMutableView=77`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Acoustic Radar Grid Read-Only Contract Narrowing

What was wrong: `IAudioService.TryGetAcousticRadarGridPayload` exposed the 8x4 audio-owned radar grid as a mutable native array to the PDA map.

What was done: Converted the grid payload contract, bootstrap stub, spatial audio implementation, and PDA map consumer to `NativeArray<float>.ReadOnly`. The 360-bin radar ring route remains mutable because the HUD texture upload still requires that path.

Cinematic Cheats used: No sonar simulation was added. The PDA map continues to use a compact 8x4 acoustic grid as a visual threat proxy instead of dense scene/audio ray queries.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `169` to `167`, `nativeApiExposureOutRefMutable` from `126` to `124`, and `nativeApiExposureBuildPlayerRuntime` from `156` to `154`.

Evidence: Focused scan found read-only grid signatures/consumer and unchanged mutable radar ring. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_acoustic_grid_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=167`, `nativeApiExposureBuildPlayerRuntime=154`, `nativeApiExposureOutRefMutable=124`, and `nativeApiRiskRuntimeOutRefMutableView=75`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Vegetation Semantic Payload Read-Only Reconciliation

What was wrong: Vegetation semantic payloads are classification facts owned by `HectonMapMagicVegetationBridge`; exposing them as mutable native arrays would let downstream flora/AI/nav-grid consumers modify owner truth.

What was done: Reconciled the current tree and verified `TryGetActiveSurfaceSemanticPayload` and `TryGetActiveUnderwaterSemanticPayload` return `NativeArray<int>.ReadOnly` and `NativeArray<byte>.ReadOnly`. Current consumers in destructible organic, flora regrowth, flora interaction, Sargassum boids, and dynamic nav-grid code consume semantic views read-only. No new source edit was needed in this continuation because the code was already in the intended state.

Cinematic Cheats used: No simulation was added. Downstream systems continue to use compact semantic classification snapshots instead of scanning scene flora objects or performing per-instance terrain/biome queries.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `167` to `165`, `nativeApiExposureOutRefMutable` from `124` to `122`, and `nativeApiExposureBuildPlayerRuntime` from `154` to `152`.

Evidence: Focused scan found no stale mutable semantic payload declarations. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_vegetation_semantics_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=165`, `nativeApiExposureBuildPlayerRuntime=152`, `nativeApiExposureOutRefMutable=122`, and `nativeApiRiskRuntimeOutRefMutableView=73`. `git diff --check` on the inspected vegetation files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Ecosystem Threat Voxel Read-Only Payload Narrowing

What was wrong: `TryGetEcosystemThreatVoxelPayload` returned the vegetation bridge's 3D threat voxel front buffer as a mutable `NativeArray<byte>` even though observed consumers only sample it for fauna line-of-sight, crevice, and obstacle-pressure decisions.

What was done: Converted the bridge accessor to `NativeArray<byte>.ReadOnly`. `PredatorCognitionDomain` now caches the borrowed grid as a read-only native alias, converts the cave SDF fallback to read-only at the borrow boundary, and feeds read-only grid fields into predator and mesofauna jobs.

Cinematic Cheats used: No collision or scene query simulation was added. Fauna still uses compact byte voxel threat snapshots for DDA/gradient heuristics instead of physics raycasts or scene-object obstacle scans.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `165` to `164`, `nativeApiExposureOutRefMutable` from `122` to `121`, and `nativeApiExposureBuildPlayerRuntime` from `152` to `151`. Raw private native field count rose from `1317` to `1318` because the cached borrow is now explicitly typed as a read-only native alias.

Evidence: Focused scan found the read-only threat voxel accessor, read-only fauna cache, and read-only predator/mesofauna job fields. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_threat_voxel_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=164`, `nativeApiExposureBuildPlayerRuntime=151`, `nativeApiExposureOutRefMutable=121`, and `nativeApiRiskRuntimeOutRefMutableView=72`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Private Nested Native API Filter And Ecosystem Save Snapshot Narrowing

What was wrong: The static native API exposure audit counted owner-internal `public` members of explicitly private nested helper types as external mutable native API surfaces. Separately, `EcosystemDirector.GetSaveSnapshotArray()` handed `SaveManager` a mutable ecosystem-sector snapshot even though persistence only serializes records.

What was done: `PolishMandateStaticAudit.py` now tracks private containing types and moves those internal helper hits to `nativeApiExposurePrivateNestedSuppressed`. `EcosystemDirector.GetSaveSnapshotArray()` now returns `NativeArray<EcosystemSectorSaveRecord>.ReadOnly`; `SaveManager` and `SaveBinaryStorage` carry that read-only view through the save path, and the cold writer copies rows by value into the binary payload.

Cinematic Cheats used: No simulation was added. This preserves the existing snapshot/binary serialization path instead of creating a second ecosystem save-state owner or managed mirror.

Exact Microseconds saved: 0 us measured. Static outcome: after the private-nested filter and ecosystem save snapshot narrowing, broad audit reports `nativeCollectionPublicMutableApiExposure=154`, down from `164`; `nativeApiExposureBuildPlayerRuntime=141`, down from `151`; `nativeApiExposureMutableReturn=35`; `nativeApiExposurePrivateNestedSuppressed=9`; and `nativeApiRiskRuntimeDiagnosticNamedMutableView=30`.

Evidence: `python Tools\test_polish_mandate_static_audit.py` ran 12 tests OK. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_private_nested_api_filter.json` returned `PASS_WITH_WARNINGS` with the suppressed bucket. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_ecosystem_save_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=154`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Contextual IK Target Frame Read-Only Handoff

What was wrong: `ContextualPhysicalIkRuntime.CurrentTargetFrames` returned a mutable target-frame buffer to rigs, even though the rig only forwards that buffer into the read-only animation apply job.

What was done: Converted the runtime property, rig cache, assign/swap methods, and `ContextualPhysicalIkApplyJob.TargetFrames` to `NativeArray<ContextualPhysicalIkTargetFrame>.ReadOnly`. Owner front/back buffers remain mutable in the runtime only.

Cinematic Cheats used: No animation physics was added. The route remains precomputed contextual IK target frames feeding an animation job, not per-rig scene queries or physics constraints.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `154` to `153`, `nativeApiExposureBuildPlayerRuntime` from `141` to `140`, `nativeApiExposureMutableReturn` from `35` to `34`, and `nativeApiRiskRuntimeReturnMutableView` from `20` to `19`.

Evidence: Focused scan found read-only contextual IK target-frame property/method/job signatures. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_contextual_ik_targetframes_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=153`, `nativeApiExposureBuildPlayerRuntime=140`, `nativeApiExposureMutableReturn=34`, and `nativeApiRiskRuntimeReturnMutableView=19`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Biomimetic POI Existing Placement Read-Only Resolver

What was wrong: `ShinobuPoiVault.TryResolveExistingPlacementBuffers()` returned mutable POI transform, narrative rule, and telemetry buffers from an existing-buffer read resolver.

What was done: Converted those resolver outputs to `NativeArray<T>.ReadOnly` while keeping the mutable `Acquire*` POI writer routes unchanged.

Cinematic Cheats used: No POI placement simulation was added. Existing placement snapshots remain compact Vault rows; there is no scene scan or GameObject placement pass.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `153` to `152`, `nativeApiExposureBuildPlayerRuntime` from `140` to `139`, `nativeApiExposureOutRefMutable` from `119` to `118`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView` from `30` to `29`.

Evidence: Focused search found no first-party call sites for `TryResolveExistingPlacementBuffers` and confirmed `AcquirePoiTransformBuffer`, `AcquireRouteBuffer`, and `AcquireTelemetryRing` remain mutable. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_biomimetic_poi_existing_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=152`, `nativeApiExposureBuildPlayerRuntime=139`, `nativeApiExposureOutRefMutable=118`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=29`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Flora Age Public Property Read-Only Narrowing

What was wrong: `HectonIndirectVegetationRenderer.FloraAges01` exposed the renderer-owned growth SoA as a mutable public native return. Focused search found no first-party direct mutation consumers, and the renderer already provides explicit setter/copy writer routes.

What was done: Converted `FloraAges01` to `NativeArray<float>.ReadOnly`. `TrySetFloraAge01`, `TryCopyFloraAges01`, GPU upload state, and culling compute bindings remain unchanged.

Cinematic Cheats used: No flora simulation was added. Growth/harvest state remains a compact shader-uploaded SoA visual lane instead of per-flora GameObject state or physics.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `152` to `151`, `nativeApiExposureBuildPlayerRuntime` from `139` to `138`, `nativeApiExposureMutableReturn` from `34` to `33`, and `nativeApiRiskRuntimeReturnMutableView` from `19` to `18`.

Evidence: Focused search found the read-only `FloraAges01` property and no first-party raw property mutation consumers. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_flora_age_readonly_property.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=151`, `nativeApiExposureBuildPlayerRuntime=138`, `nativeApiExposureMutableReturn=33`, and `nativeApiRiskRuntimeReturnMutableView=18`. `git diff --check` on the touched file reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Prefab Registry Native Map Read-Only Return

What was wrong: `PrefabRegistry.GetNativeMap()` returned a mutable `NativeHashMap<int,int>` despite its own contract saying the map is for read-only Burst access. The static audit also treated `NativeHashMap<int,int>.ReadOnly` as mutable because the read-only suppression only covered `NativeArray<T>.ReadOnly`.

What was done: Converted `GetNativeMap()` to `NativeHashMap<int,int>.ReadOnly` and returns `_nativeMap.AsReadOnly()` when the map is created. Updated `PolishMandateStaticAudit.py` and its regression test so native collection `.ReadOnly` wrapper returns are not counted as mutable exposure.

Cinematic Cheats used: No prefab or scene instantiation path was added. The registry remains a warmed native lookup map instead of a scene scan or managed lookup inside Burst-facing code.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `151` to `150`, `nativeApiExposureBuildPlayerRuntime` from `138` to `137`, `nativeApiExposureMutableReturn` from `33` to `32`, and `nativeApiRiskRuntimeReturnMutableView` from `18` to `17`.

Evidence: Focused scan found the read-only native map return and no first-party callers of `GetNativeMap()`. `python Tools\test_polish_mandate_static_audit.py` ran 12 tests OK. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_readonly_native_hashmap_filter.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=150`, `nativeApiExposureBuildPlayerRuntime=137`, `nativeApiExposureMutableReturn=32`, and `nativeApiRiskRuntimeReturnMutableView=17`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Chemical Snapshot Read-Only Handoff

What was wrong: `ChemicalInfluenceGrid` returned published chemical front/overlay grids and breadcrumbs as mutable native arrays to AI/flora consumers that only sample them in read-only jobs.

What was done: Converted `TryGetPublishedSnapshot`, `TryGetActivePublishedSnapshot`, and `TryGetPublishedBreadcrumbs` to `NativeArray<T>.ReadOnly` outputs. Predator cognition, mesofauna behavior, and flora parasite growth now consume those aliases as read-only job inputs.

Cinematic Cheats used: No chemical fluid simulation was added. The system still uses compact grid and breadcrumb proxy data for scent/toxin behavior instead of scene queries or per-particle diffusion truth.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `150` to `147`, `nativeApiExposureBuildPlayerRuntime` from `137` to `134`, `nativeApiExposureOutRefMutable` from `118` to `115`, `nativeApiRiskRuntimeDiagnosticNamedMutableView` from `29` to `27`, and `nativeApiRiskRuntimeOutRefMutableView` from `70` to `69`. Raw private native field count rose from `1318` to `1321` because cached chemical borrows are now explicit read-only native aliases.

Evidence: Focused scan found read-only snapshot signatures and read-only AI/flora job fields. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_chemical_snapshot_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=147`, `nativeApiExposureBuildPlayerRuntime=134`, `nativeApiExposureOutRefMutable=115`, `nativeApiRiskRuntimeDiagnosticNamedMutableView=27`, and `nativeApiRiskRuntimeOutRefMutableView=69`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Thermal Readback Read-Only Contract

What was wrong: The thermodynamics registry contract returned thermal map/grid readbacks as mutable native arrays even though the documented route is read-only, and first-party consumers only sample the grid through read-only pointers.

What was done: Converted `IThermodynamicsService.TryGetThermalMapReadback` and `TryGetThermalGridReadback` to `NativeArray<float>.ReadOnly`. `AbyssalThermalManager` now exports read-only aliases, while `ModularEquipmentEngine` and `ShinobuMetabolismRuntime` carry read-only grid views into unsafe pointer sampling.

Cinematic Cheats used: No thermal fluid simulation was added. The route remains a compact owner-generated Celsius grid proxy for equipment/metabolism response instead of per-object thermal physics or scene queries.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `147` to `145`, `nativeApiExposureBuildPlayerRuntime` from `134` to `132`, `nativeApiExposureOutRefMutable` from `115` to `113`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView` from `27` to `25`.

Evidence: Focused scan found read-only thermal readback signatures and no stale mutable thermal readback declarations in the touched route. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_thermal_readback_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=145`, `nativeApiExposureBuildPlayerRuntime=132`, `nativeApiExposureOutRefMutable=113`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=25`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Whirlpool Flow Read-Only Handoff

What was wrong: `HectonFluidEngine.TryGetActiveWhirlpoolFlows()` exported the fluid-owned whirlpool flow rows as a mutable native array to player kinematics and submarine ballast jobs, even though those jobs only sample vortex velocity.

What was done: Converted the active whirlpool flow accessor, `HectonAnalyticalFlowField.SampleWhirlpoolVelocity` overload, `PlayerKinematicsBodyJob.ActiveMaelstroms`, and `SubmarineAutoLevelPidJob.ActiveMaelstroms` to `NativeArray<WhirlpoolFlow>.ReadOnly`. Fluid owner mutation remains inside `HectonFluidEngine`.

Cinematic Cheats used: No vortex physics solver was added. The route remains compact analytical whirlpool rows sampled as a velocity proxy instead of per-object fluid simulation.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `145` to `144`, `nativeApiExposureBuildPlayerRuntime` from `132` to `131`, `nativeApiExposureOutRefMutable` from `113` to `112`, and `nativeApiRiskRuntimeOutRefMutableView` from `69` to `68`.

Evidence: Focused scan found read-only whirlpool flow signatures in the fluid accessor, sampler, player job, and submarine job. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_whirlpool_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=144`, `nativeApiExposureBuildPlayerRuntime=131`, `nativeApiExposureOutRefMutable=112`, and `nativeApiRiskRuntimeOutRefMutableView=68`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Cave Signed-Distance Payload Read-Only Handoff

What was wrong: `HectonCaveVoxelLightingVolume.TryGetPublishedSignedDistanceVoxelPayload()` returned the cave SDF volume as mutable native memory, while predator cognition only needed a read-only threat voxel source.

What was done: Converted the cave signed-distance payload output to `NativeArray<byte>.ReadOnly` and updated predator cognition to cache the read-only alias directly.

Cinematic Cheats used: No cave physics or collider query path was added. Predator cognition still samples an encoded SDF proxy instead of scene raycasts or mesh colliders.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `144` to `143`, `nativeApiExposureBuildPlayerRuntime` from `131` to `130`, `nativeApiExposureOutRefMutable` from `112` to `111`, and `nativeApiRiskRuntimeOutRefMutableView` from `68` to `67`.

Evidence: Focused scan found the read-only cave SDF payload signature and predator caller. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_cave_sdf_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=143`, `nativeApiExposureBuildPlayerRuntime=130`, `nativeApiExposureOutRefMutable=111`, and `nativeApiRiskRuntimeOutRefMutableView=67`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Persistent World Save Snapshot Read-Only Handoff

What was wrong: `PersistentWorldRegistry.GetSaveSnapshotArray()` returned a mutable native view of the persistent-world save snapshot. Current consumers in save serialization, PDA exploration reveal, and recovery smoke writing only read or serialize rows.

What was done: Converted the registry snapshot return and save/PDA/binary writer pipeline to `NativeArray<PersistentWorldDeltaRecord>.ReadOnly`. The binary writer still copies records by value into the indexed save payload; sector override writer APIs remain mutable because they own temporary write staging arrays.

Cinematic Cheats used: No scene scan, collider query, or managed save mirror was added. Persistent world state still flows as compact delta records instead of object graph serialization.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `143` to `142`, `nativeApiExposureBuildPlayerRuntime` from `130` to `129`, `nativeApiExposureMutableReturn` from `32` to `31`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView` from `25` to `24`.

Evidence: Focused scan found read-only snapshot signatures and consumers. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_persistent_world_save_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=142`, `nativeApiExposureBuildPlayerRuntime=129`, `nativeApiExposureMutableReturn=31`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=24`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Economy Telemetry Read-Only Dump Route

What was wrong: `Shinobu19EconomyLedger.TryResolveTelemetry` and economy dump helpers exposed the economy black-box telemetry ring as mutable native memory even though the selected route only reads entries for diagnostics and fault dumps.

What was done: Converted `TryResolveTelemetry`, `DumpTelemetryRing`, `DumpTelemetryRingH8Dump`, `DumpTelemetryRingOrdered`, and `TryDumpTelemetryOnFault` to `NativeArray<EconomyTelemetryEntry>.ReadOnly`. The telemetry writer job and `RecordTelemetry` remain mutable and explicit.

Cinematic Cheats used: No inventory replay, managed telemetry mirror, or per-item diagnostic object graph was added. The route remains a fixed 64-byte entry ring that can be dumped directly.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `142` to `141`, `nativeApiExposureBuildPlayerRuntime` from `129` to `128`, `nativeApiExposureOutRefMutable` from `111` to `110`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView` from `24` to `23`.

Evidence: Focused scan found read-only economy telemetry resolver/dump signatures and no first-party external callers requiring mutable views. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_economy_telemetry_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=141`, `nativeApiExposureBuildPlayerRuntime=128`, `nativeApiExposureOutRefMutable=110`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=23`. `git diff --check` on `Shinobu19EconomyLedger.cs` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - IK Black-Box and Async Buoyancy X-Ray Read-Only Views

What was wrong: IK black-box dump helpers accepted mutable telemetry arrays, and `AsyncBuoyancyReadbackRuntime.TryOpenEditorViews` exposed async readback X-ray buffers as mutable arrays even though the X-ray window only reads them.

What was done: Converted Leviathan terrain IK and VR physical hand-presence black-box dump/fault-dump inputs to read-only native aliases. Converted async buoyancy editor/X-ray view outputs and waterfall graph input to `NativeArray<T>.ReadOnly`; tuning edits still go through `ApplyEditorTuning`.

Cinematic Cheats used: No new physics, GPU readback, or managed mirror was added. Diagnostics continue to read fixed native rings and scalar counters instead of scene or object graph probes.

Exact Microseconds saved: 0 us measured. Static outcome: the final async audit dropped `nativeCollectionPublicMutableApiExposure` from `141` to `140`, `nativeApiExposureBuildPlayerRuntime` from `128` to `127`, `nativeApiExposureOutRefMutable` from `110` to `109`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView` from `23` to `22`. The IK dump-only hardening did not change counters because the remaining IK findings are the mutable resolver/job writer surfaces.

Evidence: Focused scans found read-only async editor view outputs and read-only IK black-box dump signatures. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_async_buoyancy_editor_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=140`, `nativeApiExposureBuildPlayerRuntime=127`, `nativeApiExposureOutRefMutable=109`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=22`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Analytical Wave Editor View Read-Only Handoff

What was wrong: `AnalyticalGerstnerWaveRuntime.TryOpenEditorViews` exposed analytical wave Vault buffers as mutable native arrays despite no first-party callers and separate editor write tooling for wave tuning.

What was done: Converted the editor view outputs for tuning, telemetry, cursor, requests, and results to `NativeArray<T>.ReadOnly`, resolving owner buffers locally and publishing immutable aliases only.

Cinematic Cheats used: No additional wave solver, scene probe, or managed mirror was added. The editor path still reads compact Gerstner request/result and telemetry DTO rows.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `140` to `139`, `nativeApiExposureBuildPlayerRuntime` from `127` to `126`, `nativeApiExposureOutRefMutable` from `109` to `108`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView` from `22` to `21`.

Evidence: Focused scan found only the read-only analytical wave editor view declaration and no first-party external caller. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_analytical_wave_editor_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=139`, `nativeApiExposureBuildPlayerRuntime=126`, `nativeApiExposureOutRefMutable=108`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=21`. `git diff --check` on `AnalyticalGerstnerWaveRuntime.cs` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Buoyancy SIMD X-Ray Read-Only View

What was wrong: `BuoyancyDisplacementRuntime.TryOpenSimdEditorViews` returned mutable SIMD telemetry, cursor, and tolerance arrays to the Burst Vectorization X-Ray window, although the window only reads telemetry/cursor and tolerance loading is routed separately.

What was done: Converted `TryOpenSimdEditorViews` outputs to `NativeArray<T>.ReadOnly` and updated the X-Ray window caller. `TryOpenSimdTuningEditorView` remains mutable because the scalar fallback slider writes through that explicit route.

Cinematic Cheats used: No runtime solver or extra telemetry generation was added. The editor reads the existing SIMD black-box and tolerance DTO rows directly.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `139` to `138`, `nativeApiExposureBuildPlayerRuntime` from `126` to `125`, `nativeApiExposureOutRefMutable` from `108` to `107`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView` from `21` to `20`.

Evidence: Focused scan found the read-only SIMD editor view and caller. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_buoyancy_simd_editor_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=138`, `nativeApiExposureBuildPlayerRuntime=125`, `nativeApiExposureOutRefMutable=107`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=20`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Inventory No-Call Resolver Read-Only Handoff

What was wrong: `Shinobu19EconomyLedger.TryResolveCarryTotals` and `TryResolveHotbarRoutes` exposed carry total and hotbar route Vault buffers as mutable native arrays from read-accessor-shaped methods. Focused search found no first-party callers that require mutation authority.

What was done: Converted both outputs to `NativeArray<T>.ReadOnly` aliases. The ledger still resolves the owner Vault buffers internally through mutable locals, then publishes immutable views only.

Cinematic Cheats used: No inventory mirror, managed hotbar cache, or per-slot object graph was added. The route remains compact native SoA data exposed as zero-copy read aliases.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `138` to `136`, `nativeApiExposureBuildPlayerRuntime` from `125` to `123`, `nativeApiExposureOutRefMutable` from `107` to `105`, and `nativeApiRiskRuntimeOutRefMutableView` from `67` to `65`.

Evidence: Focused scan found only read-only declarations for the selected inventory resolver methods. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_inventory_no_call_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=136`, `nativeApiExposureBuildPlayerRuntime=123`, `nativeApiExposureOutRefMutable=105`, and `nativeApiRiskRuntimeOutRefMutableView=65`. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Seaglide Editor Read-Only Views and Scalar Tuning Writer

What was wrong: `SeaglideHydrodynamicsRuntime.TryResolveEditorViews` exposed live seaglide tuning and telemetry buffers as mutable arrays to the X-Ray window. The slider path used `GetUnsafePtr()` on the tuning view, so a diagnostic read route doubled as a write authority lane. `TryResolveForcePacketEditorView` also gave a gizmo reader mutable force-packet access.

What was done: Converted editor tuning, counter, telemetry, cursor, audio, cavitation, and force-packet outputs to `NativeArray<T>.ReadOnly`. Added `TryApplyEditorTuning` so sliders submit finite scalar values into the owner runtime instead of writing through a borrowed native pointer.

Cinematic Cheats used: No extra propulsion simulation or managed diagnostic mirror was added. The X-Ray window still reads compact Vault rows and the gizmo samples the latest force packet as a visual proxy.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `136` to `134`, `nativeApiExposureBuildPlayerRuntime` from `123` to `121`, `nativeApiExposureOutRefMutable` from `105` to `103`, `nativeApiRiskRuntimeDiagnosticNamedMutableView` from `20` to `19`, and `nativeApiRiskRuntimeOutRefMutableView` from `65` to `64`.

Evidence: Focused scan found read-only seaglide editor resolver signatures and no `GetUnsafePtr()` in the seaglide editor path. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_seaglide_editor_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=134`, `nativeApiExposureBuildPlayerRuntime=121`, `nativeApiExposureOutRefMutable=103`, `nativeApiRiskRuntimeDiagnosticNamedMutableView=19`, and `nativeApiRiskRuntimeOutRefMutableView=64`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Animation Tuning Read-Only Editor Views

What was wrong: Procedural bone and kinetic character public tuning editor APIs returned mutable Vault-backed native arrays. Editor windows used those mutable aliases to write tuning rows directly, mixing diagnostic read views with mutation authority.

What was done: Converted both public tuning accessors to `NativeArray<T>.ReadOnly`. Added `TryApplyEditorTuning` owner methods and private mutable tuning resolvers for CSV/import paths inside each runtime.

Cinematic Cheats used: No extra animation solve, managed mirror, or GPU upload path was added. Editors still read compact tuning DTO rows and submit a single owner-side tuning row update.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `134` to `132`, `nativeApiExposureBuildPlayerRuntime` from `121` to `119`, `nativeApiExposureOutRefMutable` from `103` to `101`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView` from `19` to `17`.

Evidence: Focused scan found read-only public tuning views, private mutable CSV resolvers, and editor calls routed through `TryApplyEditorTuning`. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_animation_tuning_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=132`, `nativeApiExposureBuildPlayerRuntime=119`, `nativeApiExposureOutRefMutable=101`, and `nativeApiRiskRuntimeDiagnosticNamedMutableView=17`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Buoyancy Editor Read-Only Views and Owner Apply Routes

What was wrong: `BuoyancyDisplacementRuntime` editor APIs exposed main tuning, counters, telemetry, sleep telemetry/config, and SIMD scalar fallback tuning as mutable native arrays to editor/X-Ray windows. Designer sliders and sleep-state tuning were writing through borrowed views instead of explicit owner mutation routes.

What was done: Converted public buoyancy editor outputs to `NativeArray<T>.ReadOnly`. Added `TryApplyEditorTuning`, `TryApplySleepTelemetryEditorTuning`, and `TryApplySimdScalarFallbackEditorTuning` so editor UI writes bounded scalar/DTO values through the owner runtime.

Cinematic Cheats used: No extra buoyancy simulation, managed editor cache, or physics probe was added. The editor stays on compact Vault rows and X-Ray telemetry as a diagnostic proxy.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `132` to `128`, `nativeApiExposureBuildPlayerRuntime` from `119` to `115`, `nativeApiExposureOutRefMutable` from `101` to `97`, `nativeApiRiskRuntimeDiagnosticNamedMutableView` from `17` to `14`, and `nativeApiRiskRuntimeOutRefMutableView` from `64` to `63`.

Evidence: Focused scan found read-only buoyancy editor route signatures, owner apply methods, and no direct editor native writes. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_buoyancy_editor_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=128`, `nativeApiExposureBuildPlayerRuntime=115`, `nativeApiExposureOutRefMutable=97`, `nativeApiRiskRuntimeDiagnosticNamedMutableView=14`, and `nativeApiRiskRuntimeOutRefMutableView=63`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Construction Telemetry Read Accessor Split

What was wrong: `ModularBaseConstructionValidator.TryReadTelemetryRing` returned a mutable native telemetry ring from a read-accessor-shaped API. `PlayerBuilder` wrote telemetry through that route, so the name and authority contract disagreed.

What was done: Converted `TryReadTelemetryRing` to `NativeArray<ConstructionTelemetryEntry>.ReadOnly`. Updated `PlayerBuilder` to open the explicit `EnsureTelemetryRing` writer/acquire route before `WriteTelemetry`.

Cinematic Cheats used: No construction validation simulation or managed telemetry copy was added. The system still writes one fixed-size black-box ring row as a diagnostic proxy.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `128` to `127`, `nativeApiExposureOutRefMutable` from `97` to `96`, `nativeApiExposureBuildQaDevProof` from `8` to `7`, and `nativeApiRiskEditorOrProofSurface` from `13` to `12`.

Evidence: Focused scan found read-only `TryReadTelemetryRing` and `PlayerBuilder` routed through `EnsureTelemetryRing` before `WriteTelemetry`. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_construction_telemetry_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=127`, `nativeApiExposureOutRefMutable=96`, and `nativeApiExposureBuildQaDevProof=7`. `git diff --check` on touched files reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Seismic Vault Helper Scope Narrowing

What was wrong: `HectonSeismicTideDirector` exposed generic Vault implementation helpers as `internal static`, creating unnecessary mutable native API surface for same-file implementation details.

What was done: Made the implementation helpers private while preserving the two same-file editor/proof entry methods that are called by top-level editor classes in the file.

Cinematic Cheats used: No simulation changed. This is a compile-wall/authority surface reduction; seismic visual fakes and shader/scalar outputs remain on the existing paths.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `127` to `125`, `nativeApiExposureBuildPlayerRuntime` from `115` to `113`, `nativeApiExposureOutRefMutable` from `96` to `94`, and `nativeApiRiskRuntimeOutRefMutableView` from `63` to `61`.

Evidence: Focused scan found no external references to the private seismic helpers. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_seismic_helper_scope.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=125`, `nativeApiExposureBuildPlayerRuntime=113`, `nativeApiExposureOutRefMutable=94`, and `nativeApiRiskRuntimeOutRefMutableView=61`. `git diff --check` on the touched file reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Base Module Catalog Byte Hydration Read-Only Outputs

What was wrong: `BaseModuleCatalogRuntime.TryLoadCatalogBytes` and `TryStartCatalogByteLoad` exposed the raw catalog byte lane as mutable native memory, even though these APIs are cold hydration/read handoffs and no first-party callers require mutation authority.

What was done: Returned `NativeArray<byte>.ReadOnly` from both byte-load APIs. The loader still writes into an owner-local mutable `targetBytes` buffer for file reads, then publishes only a read-only alias.

Cinematic Cheats used: No catalog object graph or managed byte mirror was added. Hydration remains raw binary bytes feeding a Burst/table pipeline.

Exact Microseconds saved: 0 us measured. Static outcome: broad audit dropped `nativeCollectionPublicMutableApiExposure` from `125` to `123`, `nativeApiExposureBuildPlayerRuntime` from `113` to `111`, `nativeApiExposureOutRefMutable` from `94` to `92`, and `nativeApiRiskRuntimeOutRefMutableView` from `61` to `59`.

Evidence: Focused scan found no first-party call sites for the changed byte-load APIs. `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_base_module_catalog_bytes_readonly.json` returned `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=123`, `nativeApiExposureBuildPlayerRuntime=111`, `nativeApiExposureOutRefMutable=92`, and `nativeApiRiskRuntimeOutRefMutableView=59`. `git diff --check` on the touched file reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Construction Socket Editor Read-Only Pass

What was wrong: `ConstructionSocketEditorVaultReads.TryRead<T>` returned mutable native aliases from an editor-only read helper. Current consumers were UI/Gizmo read paths, not owner mutation paths.

What was done: Converted the helper output to `NativeArray<T>.ReadOnly` and updated counters, telemetry, socket state, and socket AUP editor consumers to read-only aliases. Runtime construction writer/acquire routes were not touched.

Cinematic Cheats used: None added. This is an authority/read-surface cleanup for editor diagnostics.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 123 -> 122 and `nativeApiExposureOutRefMutable` 92 -> 91 in `Docs/Reports/PROJECT_AUDIT_polish_after_construction_socket_editor_readonly.json`.

Verification: Focused scan found only read-only construction socket editor consumers. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Hadal Preview Read-Only Pass

What was wrong: `HadalTrenchPreviewStore.TryReadPreview` exported mutable editor preview fault and thermal vent arrays to the SceneView drawer even though the drawer only reads rows for preview visualization.

What was done: Converted the preview fault and vent outputs to `NativeArray<T>.ReadOnly` and updated the drawer call site. Preview store allocations, H8Memory release, and generation jobs remain the only writer path.

Cinematic Cheats used: Existing preview remains a SceneView optical proxy, drawing fault lines and vent caps instead of baking or simulating terrain during inspection.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 122 -> 121, `nativeApiExposureBuildEditorOnly` 4 -> 3, and `nativeApiExposureOutRefMutable` 91 -> 90 in `Docs/Reports/PROJECT_AUDIT_polish_after_hadal_preview_readonly.json`.

Verification: Focused scan found only read-only Hadal preview outputs. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Lore Entity Reader Read-Only Pass

What was wrong: `ScannableTarget.TryReadLoreEntityBuffers` returned mutable AUP/hash arrays from a public read accessor to `ScannerTool`, even though the scanner only reads them for scientific lore candidate scoring.

What was done: Converted the public lore entity outputs and scalar evaluator inputs to `NativeArray<T>.ReadOnly`. Private owner-side Vault helpers remain mutable for publish and clear operations.

Cinematic Cheats used: Existing scanner candidate resolution remains a cheap scalar cone/distance test over AUP-localized snapshots instead of physics queries over scene objects.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 121 -> 120, `nativeApiExposureBuildPlayerRuntime` 111 -> 110, and `nativeApiExposureOutRefMutable` 90 -> 89 in `Docs/Reports/PROJECT_AUDIT_polish_after_lore_entity_readonly.json`.

Verification: Focused scan found read-only public lore reader signatures and unchanged private owner writer helpers. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Residency DTO Read-Only Pass

What was wrong: `WorldChunkResidencyManager.TryGetChunkResidencyDtos` returned mutable chunk residency DTOs through a public readback route used by the editor SceneView tuner.

What was done: Converted the public readback route and editor consumer to `NativeArray<ChunkResidencyDTO>.ReadOnly`. Private owner resolver and streaming jobs remain mutable where they write state.

Cinematic Cheats used: Existing SceneView residency grid remains a lightweight visualization proxy over DTO rows instead of scene-object scans or streaming object traversal.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 120 -> 119, `nativeApiExposureBuildPlayerRuntime` 110 -> 109, and `nativeApiExposureOutRefMutable` 89 -> 88 in `Docs/Reports/PROJECT_AUDIT_polish_after_residency_dtos_readonly.json`.

Verification: Focused scan found read-only public residency DTO accessor and unchanged private writer resolver. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Abyssal Path Payload Read-Only Pass

What was wrong: `TryGetLatestAbyssalPathPayload` returned a mutable native path snapshot to the sargassum boid system, although the consumer only copied the rows into its own scratch buffer.

What was done: Converted the public path snapshot output and `ScheduleLeviathanNodeBuild` input to `NativeArray<Vector3>.ReadOnly`. Vegetation bridge remains the only owner of the mutable snapshot.

Cinematic Cheats used: Existing leviathan guidance remains a cheap path-snapshot copy into fixed scratch before node construction, avoiding scene path object traversal.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 119 -> 118, `nativeApiExposureBuildPlayerRuntime` 109 -> 108, and `nativeApiExposureOutRefMutable` 88 -> 87 in `Docs/Reports/PROJECT_AUDIT_polish_after_abyssal_path_readonly.json`.

Verification: Focused scan found read-only public path payload and consumer input. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Visible HLOD Payload Read-Only Pass

What was wrong: `TryGetVisibleHLODPayload` exposed visible HLOD snapshot memory as a mutable public native output despite having no current first-party consumers.

What was done: Converted the output to `NativeArray<HLODData>.ReadOnly`. HLOD culling and snapshot write ownership remain unchanged inside the vegetation bridge.

Cinematic Cheats used: Existing HLOD visibility remains a culled snapshot proxy for distant rendering rather than per-consumer scene traversal.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 118 -> 117, `nativeApiExposureBuildPlayerRuntime` 108 -> 107, and `nativeApiExposureOutRefMutable` 87 -> 86 in `Docs/Reports/PROJECT_AUDIT_polish_after_visible_hlod_readonly.json`.

Verification: Focused scan found only read-only HLOD payload declarations. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Dynamic Decal Read-Lock Read-Only Pass

What was wrong: `TryAcquireDecalBufferRead` used read-lock semantics but returned mutable decal instance memory to editor Gizmo/tuner consumers.

What was done: Converted the read-lock output and two editor consumers to `NativeArray<VisorDecalDTO>.ReadOnly`. Mutable Vault access remains internal; `ReleaseDecalBufferRead` lifecycle is unchanged.

Cinematic Cheats used: Existing decal editor visualization remains a Gizmo proxy over DTO rows instead of inspecting scene decals or runtime renderer internals.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 117 -> 116, `nativeApiExposureBuildPlayerRuntime` 107 -> 106, and `nativeApiExposureOutRefMutable` 86 -> 85 in `Docs/Reports/PROJECT_AUDIT_polish_after_dynamic_decal_readonly.json`.

Verification: Focused scan found only read-only decal editor consumers. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Scavenging Self-Audit Counter Read-Only Pass

What was wrong: `TryRunDistributionSelfAudit` returned a mutable distribution-audit counter buffer to an editor button that only reads four counter cells for display.

What was done: Converted the self-audit output and editor consumer to `NativeArray<uint>.ReadOnly`. The Vault-owned audit counter buffer remains mutable only for the self-audit job.

Cinematic Cheats used: Existing loot validation remains a counter histogram over deterministic rolls instead of spawning loot previews or scene objects.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 116 -> 115, `nativeApiExposureBuildPlayerRuntime` 106 -> 105, and `nativeApiExposureOutRefMutable` 85 -> 84 in `Docs/Reports/PROJECT_AUDIT_polish_after_scavenging_audit_readonly.json`.

Verification: Focused scan found only read-only self-audit counter signatures. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Wrist HUD Quad Resolver Scope Pass

What was wrong: `TryResolveQuadBuffer` exposed mutable wrist HUD quad DTO memory as a public method despite having only same-file owner call sites.

What was done: Narrowed the resolver to `private`. Upload and draw-matrix fill still read the completed quad buffer inside the owner runtime.

Cinematic Cheats used: Existing wrist HUD remains a DTO-to-quad GPU upload path instead of scene UI object generation.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 115 -> 114, `nativeApiExposureBuildPlayerRuntime` 105 -> 104, and `nativeApiExposureOutRefMutable` 84 -> 83 in `Docs/Reports/PROJECT_AUDIT_polish_after_wrist_quad_scope.json`.

Verification: Focused scan found only private same-file uses. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Base Atmosphere Front Read-Only Pass

What was wrong: Private `TryReadFront` returned mutable front-buffer aliases to read-only count/state/black-box code.

What was done: Converted `TryReadFront` and its read call sites to `NativeArray<CompartmentState>.ReadOnly`. Owner mutation remains on `TryOpenCompartmentViews`.

Cinematic Cheats used: Atmosphere reads remain scalar DTO sampling and Dalton-pressure approximation, not per-room physics scene queries.

Exact Microseconds saved: 0 us measured. Static public counters did not move in `Docs/Reports/PROJECT_AUDIT_polish_after_atmosphere_front_readonly.json`; this is a private read-helper hardening.

Verification: Focused scan found only read-only `TryReadFront` uses. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Count Accessor Scope Cleanup

What was wrong: `ToolHapticsRuntime.FrontCount` and `SubmarineOsThermalGridRuntime.NodeCount`/`EdgeCount` were public scalar properties whose expression bodies declared mutable `NativeArray` locals. This created focused public mutable API noise without granting callers actual native access.

What was done: Moved the mutable native resolution into private scalar helpers and kept public properties scalar-only. Haptic buffer and thermal counter writer routes remain owner-local/private; no DTO, Vault handle, solver, or unsafe span path changed.

Cinematic Cheats used: Scope narrowing only. No new simulation; no visual fake added in this micro-pass.

Exact Microseconds saved: 0 us measured. Static proof: focused native accessor scan no longer reports the haptic/thermal count property lines; `python Tools\test_polish_mandate_static_audit.py` ran 12 tests OK; `python Tools\PolishMandateStaticAudit.py --json-path Docs\Reports\PROJECT_AUDIT_polish_after_count_accessors_scope.json --report-path Docs\Reports\PROJECT_AUDIT_polish_after_count_accessors_scope.md` returned `PASS_WITH_WARNINGS` with unchanged broad counters: `nativeCollectionPublicMutableApiExposure=114`, `nativeApiExposureBuildPlayerRuntime=104`, `nativeApiExposureOutRefMutable=83`, and `nativeApiRiskRuntimeOutRefMutableView=52`. `git diff --check` reported only LF-to-CRLF warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Cable Editor Write API Split

What was wrong: `CablePhysicsSolver132` exposed public mutable native tuning/material views that were only needed by one editor tuner.

What was done: Added scalar/span owner APIs for tuning sample, tuning write, and material CSV apply. Made the raw native view openers private and updated `Shinobu132CablePhysicsTunerWindow` to use DTO/span routes.

Cinematic Cheats used: Existing cable tuning stays a scalar DTO and material hash-table CSV lane; no scene cable objects or physics previews are spawned for editor tuning.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 114 -> 112, `nativeApiExposureBuildPlayerRuntime` 104 -> 102, `nativeApiExposureOutRefMutable` 83 -> 81, and `nativeApiRiskRuntimeOutRefMutableView` 52 -> 50 in `Docs/Reports/PROJECT_AUDIT_polish_after_cable_editor_write_api.json`.

Verification: Focused scan no longer reports cable public native view APIs. `python Tools\test_polish_mandate_static_audit.py` passed 12 tests. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - SignalWarden Mutable API Scope Split

What was wrong: `SignalWardenRuntime` exposed public mutable CSV scratch `NativeArray<byte>` routes and a public mutable committed-signal opener even though focused source inventory showed same-file editor parser use or no external caller.

What was done: Added `TryReadCsvBytesForLoad(string, out ReadOnlySpan<byte>)` owner bridges, made the CSV scratch openers private, routed both hot-swap parsers through spans, and narrowed the unused committed-signal mutable opener to private while keeping the read-only committed-signal accessor.

Cinematic Cheats used: Scope/authority cleanup only. Runtime signal coalescence remains the existing scalar/SoA coalescence path, not scene object or managed event expansion.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 112 -> 109, `nativeApiExposureBuildPlayerRuntime` 102 -> 99, `nativeApiExposureOutRefMutable` 81 -> 78, and `nativeApiRiskRuntimeOutRefMutableView` 50 -> 47 in `Docs/Reports/PROJECT_AUDIT_polish_after_signal_warden_scope.json`.

Verification: Focused public/internal `out NativeArray` scan no longer reports `SignalWardenRuntime`. `python Tools\test_polish_mandate_static_audit.py` passed 12 tests. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - VRSomatic Private Wrapper Scope Pass

What was wrong: Private nested `VaultNativeArray<T>` in `VRSomaticProvider` exposed `TryResolve` and `TryRead` as public methods even though focused search found only same-struct callers.

What was done: Changed both native resolver methods to private. The wrapper indexer, `AsNativeArray`, implicit operator, release behavior, Vault handles, and owner-local mutable path are unchanged.

Cinematic Cheats used: Scope cleanup only. Existing VR somatic comfort remains shader scalar/vignette/haptic presentation plus owner-local collision samples; no new physical simulation was added.

Exact Microseconds saved: 0 us measured. Static hygiene gain: focused public/internal `out NativeArray` scan no longer reports `VRSomaticProvider`; `nativeApiExposurePrivateNestedSuppressed` 9 -> 7 in `Docs/Reports/PROJECT_AUDIT_polish_after_vrsomatic_scope.json`.

Verification: `python Tools\test_polish_mandate_static_audit.py` passed 12 tests. Static audit status remained `PASS_WITH_WARNINGS` with `nativeCollectionPublicMutableApiExposure=109`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Biomimetic POI Acquire Scope Pass

What was wrong: `ShinobuPoiVaultBridge` still exported three public mutable POI Vault acquire methods, and current whole-repo source search found no first-party caller for any of them.

What was done: Narrowed `AcquirePoiTransformBuffer`, `AcquireRouteBuffer`, and `AcquireTelemetryRing` to private. The existing public read-only placement resolver, BufferIDs, DTO layouts, and private `AcquireWorldStreamingBuffer<T>` behavior are unchanged.

Cinematic Cheats used: Existing POI architecture remains matrix/DTO placement and HZB/cull proxy data instead of scene prefab instantiation from consumers.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 109 -> 106, `nativeApiExposureBuildPlayerRuntime` 99 -> 96, `nativeApiExposureMutableReturn` 31 -> 28, and `nativeApiRiskRuntimeReturnMutableView` 17 -> 15 in `Docs/Reports/PROJECT_AUDIT_polish_after_biomimetic_acquire_scope.json`.

Verification: Focused search found no public POI acquire methods and no dependent caller. `python Tools\test_polish_mandate_static_audit.py` passed 12 tests. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Animation IK Resolver Scope Pass

What was wrong: `LeviathanTerrainIkVault.TryResolveBuffers` and `VRPhysicalHandPresenceVault.TryResolveBuffers` exported public mutable native Vault lanes, but active source search found no qualified caller. References exist only in archived batch logs.

What was done: Narrowed both resolver methods to private. The layout sentinels, BufferIDs, telemetry DTOs, and lane resolution code are unchanged.

Cinematic Cheats used: Existing IK route remains procedural math/Vault lane resolution rather than PhysX joints or scene query ownership. No new simulation was added.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 106 -> 104, `nativeApiExposureBuildPlayerRuntime` 96 -> 94, `nativeApiExposureOutRefMutable` 78 -> 76, and `nativeApiRiskRuntimeDiagnosticNamedMutableView` 13 -> 11 in `Docs/Reports/PROJECT_AUDIT_polish_after_animation_ik_scope.json`.

Verification: Focused search found no public animation IK resolver declarations in touched files and no qualified active caller. `python Tools\test_polish_mandate_static_audit.py` passed 12 tests. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Private Wrapper AsNative Scope Pass

What was wrong: `VRSomaticProvider.VaultNativeArray<T>.AsNativeArray` and `GlobalPhysicsStateManager.VaultBufferBinding<T>.AsNativeArray` were public native-return methods inside private wrapper structs. Focused search showed both are only used inside their own wrapper bodies.

What was done: Narrowed both `AsNativeArray` methods to private. The required public implicit conversion operators, indexers, Vault handles, DTO layout, and owner-local mutable paths are unchanged.

Cinematic Cheats used: Scope cleanup only. Existing VR/physics paths stay on owner-local Vault lanes and shader/DTO presentation; no new CPU physics or scene-search simulation was introduced.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeApiExposurePrivateNestedSuppressed` 7 -> 5 in `Docs/Reports/PROJECT_AUDIT_polish_after_private_wrapper_asnative_scope.json`; main mutable public counters remained `nativeCollectionPublicMutableApiExposure=104`, `nativeApiExposureBuildPlayerRuntime=94`, and `nativeApiExposureOutRefMutable=76`.

Verification: Focused scan reports only five remaining private-wrapper public native methods with owner call sites. `python Tools\test_polish_mandate_static_audit.py` passed 12 tests. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. Batch re-check after Tasks 335-337 still found no `<AGENT_PROMPT id="PROJECT_AUDIT">` block. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - TBDR Locked Matrix Scope Pass

What was wrong: `TBDRUmaRawBufferWriter.SchedulePopulateLockedMatrices` publicly returned a locked mutable `NativeArray<float4x4>` view from a GPU buffer, but source search found no active caller.

What was done: Narrowed the no-call scheduler to private. The populate job, raw buffer factory, unlock helper, DTO source, and lock/unlock behavior are unchanged.

Cinematic Cheats used: Existing TBDR path remains GPU raw-buffer matrix upload and tile-aware culling infrastructure instead of GameObject/Renderer instantiation. No new CPU-side render simulation was added.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 104 -> 103, `nativeApiExposureBuildPlayerRuntime` 94 -> 93, `nativeApiExposureOutRefMutable` 76 -> 75, and `nativeApiRiskRuntimeOutRefMutableView` 47 -> 46 in `Docs/Reports/PROJECT_AUDIT_polish_after_tbdr_locked_matrix_scope.json`.

Verification: Focused search found only the private declaration. `python Tools\test_polish_mandate_static_audit.py` passed 12 tests. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. Batch re-check after Tasks 338-340 still found no `<AGENT_PROMPT id="PROJECT_AUDIT">` block. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Geography Profile CSV Store Split

What was wrong: `GeographySanityProfileCsv.LoadProfiles` returned a mutable `NativeList<SanityProfileDTO>` from an editor CSV bridge. The window and pipeline consumers only needed deterministic ownership, row count, disposal, and a read pointer for profile application; the native-list return was an unnecessary public mutable collection API.

What was done: Added `GeographySanityProfileStore` as a disposable editor-only owner wrapper, changed `LoadProfiles` to return the wrapper, updated `WorldSanityCheckerWindow` and `GeographySanityPipeline` consumers, and moved profile pointer acquisition inside the existing unsafe `RunSector` owner phase. `ApplySanityProfilesJob` now reads profile rows through `ref readonly` while retaining the existing pointer-local Burst path.

Cinematic Cheats used: Existing world sanity validation still patches anomaly thresholds from compact CSV profile rows before sector jobs instead of building managed authoring objects or scene-side validators. No physical simulation was added.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 103 -> 102, `nativeApiExposureBuildEditorOnly` 3 -> 2, `nativeApiExposureMutableReturn` 28 -> 27, and `nativeApiRiskEditorOrProofSurface` 10 -> 9 in `Docs/Reports/PROJECT_AUDIT_polish_after_geography_profile_store.json`.

Verification: Focused public/internal native signature scan found no `GeographySanityProfileCsv` public native collection API. `python Tools\test_polish_mandate_static_audit.py` passed 12 tests. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Topography Recipe CSV Store Split

What was wrong: `TopographyBiomeCsv.LoadRecipes` and `AppendDefaultRecipes` exported editor CSV recipe loading through public `ref NativeList<TopographyBiomeRecipeDTO>` signatures. The topography preview and bake generator only need count plus indexed recipe reads before copying to kernel DTOs.

What was done: Added `TopographyBiomeRecipeStore` as a disposable editor-only owner wrapper, changed `LoadRecipes` to return that wrapper, made default recipe append/factory helpers private, updated preview and bake generator consumers, and added exception cleanup so parse failures dispose the newly created NativeList before rethrow.

Cinematic Cheats used: Existing topography preview remains a compact recipe-to-kernel path feeding preview jobs, not scene mesh/GameObject terrain instantiation. No physical simulation was added.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeCollectionPublicMutableApiExposure` 102 -> 100, `nativeApiExposureBuildEditorOnly` 2 -> 0, `nativeApiExposureOutRefMutable` 75 -> 73, and `nativeApiRiskEditorOrProofSurface` 9 -> 7 in `Docs/Reports/PROJECT_AUDIT_polish_after_topography_recipe_store.json`.

Verification: Focused scan found no public/internal native collection signatures in `TopographyBiomeCsv`. `python Tools\test_polish_mandate_static_audit.py` passed 12 tests. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` on touched Topography files passed with no output. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Private Wrapper Tail Scope Pass

What was wrong: Five native-return helper methods inside private implementation wrappers still used `public`: `PredatorCognitionDomain.VaultArray<T>.Open`, `PlayerKinematicsRuntime.VaultBufferBinding<T>.GetSubArray`, `SubmarineFluidDynamics.VaultNativeBuffer<T>.OpenView`, and `EcosystemDirector.VaultNativeArray<T>.GetSubArray/Resolve`.

What was done: Narrowed those methods to private. Owner code still reaches them through containing-type access and existing implicit operators/indexers; Vault handles, DTOs, and job memory routes are unchanged.

Cinematic Cheats used: Scope cleanup only. Existing systems keep their DataVault-backed SoA lanes and shader/DTO presentation tricks; no new simulation was added.

Exact Microseconds saved: 0 us measured. Static hygiene gain: `nativeApiExposurePrivateNestedSuppressed` 5 -> 0 in `Docs/Reports/PROJECT_AUDIT_polish_after_private_wrapper_tail_scope.json`. Public mutable counters remained `nativeCollectionPublicMutableApiExposure=100`, `nativeApiExposureBuildPlayerRuntime=93`, and `nativeApiExposureOutRefMutable=73`.

Verification: Focused scan found no public/internal native wrapper methods matching the selected names. `python Tools\test_polish_mandate_static_audit.py` passed 12 tests. Static audit status remained `PASS_WITH_WARNINGS`. `git diff --check` reported only LF/CRLF normalization warnings. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Construction Proof Owner Write Split

What was wrong: `ModularBaseConstructionValidator` still exposed three public QA/proof mutable native buffer openers. `PlayerBuilder` consumed the telemetry opener only to write one row, and bounds/occupancy ensure routes were same-class owner helpers.

What was done: Added `TryWriteTelemetryToVault` as the public owner-write method, changed `PlayerBuilder` to call it directly, and narrowed `EnsureTelemetryRing`, `EnsureBoundsOverrideBuffer`, and `EnsureOccupancyHashTable` to private.

Cinematic Cheats used: No new simulation. This is route tightening only; construction still uses deterministic grid/AABB proof data and existing telemetry instead of scene scans.

Exact Microseconds saved: 0 us measured. Static API risk moved `nativeCollectionPublicMutableApiExposure=100->97`, `nativeApiExposureOutRefMutable=73->70`, `nativeApiExposureBuildQaDevProof=7->4`, and `nativeApiRiskEditorOrProofSurface=7->4`.

Evidence: `Docs/Reports/PROJECT_AUDIT_polish_after_construction_proof_owner_write.json`; `python Tools\test_polish_mandate_static_audit.py` passed 12 tests; focused scan found no public construction `Ensure*` mutable buffer openers and no external `ModularBaseConstructionValidator.Ensure*` callers; targeted `git diff --check` produced no output. Batch re-check found no `<AGENT_PROMPT id="PROJECT_AUDIT">` block. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - DropBuffer Owner Schedule Split

What was wrong: `World.DropBuffer.AsParallelWriter()` returned a mutable `NativeQueue<ItemDropData>.ParallelWriter` from a public method. Focused source search found only `DestructibleOrganicManager`, which immediately used the writer to schedule `EntropyYieldJob`.

What was done: Replaced the writer-return route with `DropBuffer.ScheduleEntropyYieldJob`, which opens the queue writer inside the buffer owner and returns only the scheduled `JobHandle`. `DestructibleOrganicManager` now passes native input views and batch size to the owner wrapper.

Cinematic Cheats used: No new simulation. Existing organic yield remains a deterministic Burst loot approximation, not GameObject spawning or physics debris simulation.

Exact Microseconds saved: 0 us measured. Static API risk moved `nativeCollectionPublicMutableApiExposure=97->96`, `nativeApiExposureBuildPlayerRuntime=93->92`, `nativeApiExposureMutableReturn=27->26`, and `nativeApiRiskRuntimeReturnMutableView=15->14`.

Evidence: `Docs/Reports/PROJECT_AUDIT_polish_after_dropbuffer_owner_schedule.json`; `python Tools\test_polish_mandate_static_audit.py` passed 12 tests; focused scan found no `DropBuffer.AsParallelWriter` API and only owner-local `_queue.AsParallelWriter()` inside `DropBuffer.ScheduleEntropyYieldJob`; targeted `git diff --check` reported only LF/CRLF normalization warnings. Batch re-check found no `<AGENT_PROMPT id="PROJECT_AUDIT">` block. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Cable Mock Owner Schedule Split

What was wrong: `TetherManager` opened the cable mock Vault buffers and SignalBus physics-event writer directly before scheduling the mock job. The caller was acting as a buffer broker instead of a schedule requester.

What was done: Added `CablePhysicsSolver132.TryHasMockBuffers` and `TryScheduleMockFromVault`, made `TryResolveMockBuffers` and `AcquirePhysicsEventWriter` private, and routed `TetherManager` through the owner scheduler while preserving dispatcher completion and black-box timing.

Cinematic Cheats used: No new physical simulation. The existing cable mock remains a cheap owner-scheduled presentation/telemetry path, and the edit removes raw mutable buffer transit rather than adding CPU physics.

Exact Microseconds saved: 0 us measured. Static API risk moved `nativeCollectionPublicMutableApiExposure=96->94`, `nativeApiExposureBuildPlayerRuntime=92->90`, `nativeApiExposureMutableReturn=26->25`, `nativeApiExposureOutRefMutable=70->69`, `nativeApiRiskRuntimeDiagnosticNamedMutableView=11->10`, and `nativeApiRiskRuntimeReturnMutableView=14->13`.

Evidence: `Docs/Reports/PROJECT_AUDIT_polish_after_cable_mock_owner_schedule.json`; `python Tools\test_polish_mandate_static_audit.py` passed 12 tests; focused scan found no external `CablePhysicsSolver132.TryResolveMockBuffers`, `AcquirePhysicsEventWriter`, or direct `ScheduleMock` call; targeted `git diff --check` reported only LF/CRLF normalization warnings. Batch re-check found no `<AGENT_PROMPT id="PROJECT_AUDIT">` block. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Inventory Ledger Writer Route Scope Pass

What was wrong: `Shinobu19EconomyLedger.TryResolveVaultLedger` publicly exposed mutable inventory hash, quantity, and durability SoA buffers, but source search found no active first-party caller.

What was done: Narrowed `TryResolveVaultLedger` to private and left active editor recipe/ingredient/physical-constants routes unchanged for a separate owner-facade pass.

Cinematic Cheats used: No new simulation. This is route sovereignty cleanup; inventory still uses rollback-compatible SoA buffers and deterministic transaction math.

Exact Microseconds saved: 0 us measured. Static API risk moved `nativeCollectionPublicMutableApiExposure=94->93`, `nativeApiExposureBuildPlayerRuntime=90->89`, `nativeApiExposureOutRefMutable=69->68`, and `nativeApiRiskRuntimeOutRefMutableView=46->45`.

Evidence: `Docs/Reports/PROJECT_AUDIT_polish_after_inventory_ledger_writer_scope.json`; `python Tools\test_polish_mandate_static_audit.py` passed 12 tests; focused search found only the private `TryResolveVaultLedger` declaration; targeted `git diff --check` reported only LF/CRLF normalization warnings. Batch re-check found no `<AGENT_PROMPT id="PROJECT_AUDIT">` block. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.

## 2026-05-22 - Drone Fleet Vault Helper Scope Pass

What was wrong: `DroneFleetManager.ResolveDroneVaultBuffer` and `ReleaseDroneVaultBuffer` were internal generic mutable native helper APIs. Same-file `HectonDroneFleetEvents` needed two payload lanes, but not the whole generic fleet helper surface.

What was done: Added private snapshot-event resolve/release helpers inside `HectonDroneFleetEvents`, then narrowed the generic DroneFleetManager helpers to private. Vault handle validation, H8Memory fallback, Sentinel registration, and listener dispatch behavior are unchanged.

Cinematic Cheats used: No new simulation. This preserves the existing drone fleet DataVault/BRG/telemetry architecture and only closes the generic mutable helper route.

Exact Microseconds saved: 0 us measured. Static API risk moved `nativeCollectionPublicMutableApiExposure=93->91`, `nativeApiExposureBuildPlayerRuntime=89->87`, `nativeApiExposureMutableReturn=25->24`, `nativeApiExposureOutRefMutable=68->67`, `nativeApiRiskRuntimeReturnMutableView=13->12`, and `nativeApiRiskRuntimeOutRefMutableView=45->44`.

Evidence: `Docs/Reports/PROJECT_AUDIT_polish_after_drone_vault_helper_scope.json`; `python Tools\test_polish_mandate_static_audit.py` passed 12 tests; focused scan found no external `DroneFleetManager.ResolveDroneVaultBuffer` or `ReleaseDroneVaultBuffer` calls and no public/internal declarations; targeted `git diff --check` reported only LF/CRLF normalization warnings. Batch re-check found no `<AGENT_PROMPT id="PROJECT_AUDIT">` block. No Unity import, Play Mode, profiler, GCMonitor, player build, dotnet build, or dotnet rebuild was run.
