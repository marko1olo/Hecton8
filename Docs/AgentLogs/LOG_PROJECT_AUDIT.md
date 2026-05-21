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
