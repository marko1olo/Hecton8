# [DEPRECATED] 2026-05-21 Portability Code Improvement Backlog

Agent: HFI_AUDIT
Scope: improvements available before runtime/device proof. No build, Unity
import, player run, profiler, or device capture was launched.

## Verdict

Yes, there is useful work to do in code and settings before runtime proofs.
But the work must be ordered. The highest-value changes are not broad refactors;
they are narrow changes that reduce mobile/weak-PC risk without creating a Unity
import or compile wall.

## Do Now

1. Add platform-proof gates for known static gaps.
   - Extend `PlatformPortabilityProofAudit.py` to report Android sustained
     performance mode, Android graphics API mode, Quest URP asset wiring,
     shader warmup surface, and risky compute thread groups.
   - This prevents future reports from silently calling scaffold readiness.

2. Turn Android sustained-performance mode on through project settings or an
   editor-controlled settings patch.
   - Current evidence: `ProjectSettings/ProjectSettings.asset` has
     `AndroidEnableSustainedPerformanceMode: 0`.
   - This is low-risk but should still be done as a deliberate settings commit,
     not hidden inside unrelated code.

3. Add a Quest/standalone-VR render-pipeline selection route.
   - Current evidence: `URP_Quest_VR.asset` exists with depth/opaque/HDR off,
     but Android quality currently routes to `URP_Low (PC_RPAsset)`.
   - Do not hand-edit a new QualitySettings tier blindly. Use either a Unity
     editor script or a serialized build-profile step that wires Android/XR to
     the Quest asset and leaves PC minimum-quality path intact.

4. Add a compute portability audit.
   - Current evidence: `Hecton_SonarMap.compute` has `[numthreads(8,8,8)]`
     equals 512 threads, suspicious for Quest/TBDR; `Hecton_SonarRaymarch`
     uses target 5.0 and 128-thread kernels.
   - The first code step is a gate/report. The second step is per-kernel mobile
     variants or a dispatch limiter.

5. Add shader warmup coverage gates.
   - Current warmup evidence is weaker than the shader-feature surface.
   - Track shader variant collections and feature/target counts before changing
     content.

6. Reduce runtime-only DataVault regression or improve the audit classifier.
   - Current R28 runtime gross growth is `+38`.
   - First separate real persistent owner fields from job input `NativeArray`
     fields. Then burn down only true runtime ownership leaks.

7. Clean Burst flag drift in leaf jobs first.
   - Current static warning surface includes missing Burst flags.
   - Start with isolated job structs and math kernels, not giant domain files.

8. Replace or classify runtime `.Complete()` sites.
   - Teardown/editor completions can stay documented.
   - Frame-path completions should move behind dispatcher/fence patterns.

## Do Later / Needs Unity Import Proof

- Adding/removing QualitySettings tiers.
- Rewiring Android default quality index by hand.
- Changing URP asset internals beyond obvious settings.
- Replacing compute shader thread-group sizes without checking C# dispatch
  callers.
- Changing native plugin importer metadata without import/build validation.

## Practical First Slice

The safest next slice is tooling plus one settings change:

1. Extend `PlatformPortabilityProofAudit.py` with sustained-performance,
   Quest-URP-wired, shader-warmup, and compute-thread-risk fields.
2. Add tests for those fields.
3. Enable Android sustained-performance mode as a standalone settings commit.
4. Leave QualitySettings/URP wiring for a Unity-import-aware slice.

That improves discipline immediately without pretending the game is measured.

## R31/R32 Execution Update

Done now:

- `AndroidEnableSustainedPerformanceMode` is serialized on.
- Bootstrap now calls `ShaderVariantCollection.WarmUp()` for configured
  collections.
- `PlatformPortabilityProofAudit.py` is schema v3 and reports sustained
  performance, Vulkan-only serialization, Quest URP wiring, shader warmup,
  compute thread groups, and compute runtime reachability.
- `Player.prefab` now points PDA sonar map compute at `Hecton_MapMesh.compute`,
  matching `PDAMapTab`'s `CSBuildMapPoints` contract.
- `HectonHudFogLuminance.compute` is reduced from 256 lanes to 64 lanes, and
  `HectonUnderwaterVisuals` disables the optional path if the platform/kernel
  is unsupported or above the 64-lane budget.

Current static blockers:

- XR provider serialized proof is absent.
- Quest URP exists but Android default quality still does not route to it.
- Addressables data and Data Monolith payload are absent.
- Build/device/profiler artifacts are absent.

The runtime-referenced high-risk compute gate now passes; remaining risky
runtime asset groups are dormant or editor/test-only by current route evidence.

## R31 Execution Update

Done in the safe-now lane:

- `PlatformPortabilityProofAudit.py` now reports Android sustained performance,
  Android Vulkan-only serialization, Quest URP wiring, shader warmup, shader
  feature/target counts, and risky compute groups split by execution surface.
- Android sustained-performance mode is now enabled in project settings.
- `GameBootstrapper` now calls `ShaderVariantCollection.WarmUp()` during boot
  warmup instead of treating `isWarmedUp` as proof.
- DataVault v3 classification now separates persistent owner native collection
  fields from job-input native collections.

Current static blockers still visible:

- XR provider serialized proof is absent.
- Quest URP asset exists but Android default quality does not use it.
- Runtime compute risk remains: 4 runtime kernels exceed 64 threads per group.
- Addressables content and Data Monolith payload are absent.
- DataVault v3 no-regression gate currently fails on Construction/Habitat
  forbidden declarations `1719 -> 1721`.

Still do later with Unity/import/build awareness:

- Wire Quest URP through the Android/XR quality route.
- Rewrite Sonar/HUD compute kernels and matching C# dispatchers together.
- Produce runtime proof artifacts for Quest, Deck/Linux, Windows, and Mac/Metal.

## R33 Execution Update

Done in the safe-now lane:

- `QuestVulkanRenderPipelineConfigurator` now writes an Android Quality/Quest
  URP route audit: Quest GUID, quality row count, Android default quality
  index/name, Android render-pipeline GUID, and PASS/BLOCKED.
- `PlatformPortabilityProofAudit.py` is schema v4 and reports whether the Quest
  configurator contains that quality-route audit.
- The platform audit test suite asserts route-audit detection.

Current static blockers remain:

- Quest URP still is not wired to Android default quality.
- XR provider serialized proof is still absent.
- Addressables content, Data Monolith payload, and build/device/profiler
  artifacts are still absent.

Deliberately not done in R33:

- No manual `QualitySettings.asset` tier rewrite. That remains a Unity
  import-aware fix because the current Android default row is `Abyss (Low)`,
  not a dedicated Quest quality row.

## R34 Execution Update

Done in the safe-now lane:

- `QuestVulkanRenderPipelineConfigurator` now contains
  `WireQuestAndroidQualityRouteForCi()`.
- The fixer creates or updates a dedicated `Quest (VR)` quality row through
  Unity's `QualitySettings` serialized object, assigns the Quest URP asset,
  includes Android only on that row, excludes Android from other rows, and sets
  Android's per-platform default quality index.
- `PlatformPortabilityProofAudit.py` is schema v5 and reports
  `questConfiguratorQualityRouteFixerPresent`.
- The platform audit test suite asserts that the Unity-side route fixer is
  present.

Current static blockers remain:

- The fixer has not been executed inside Unity, so serialized
  `questUrpWiredToAndroidQuality` remains `false`.
- XR provider serialized proof is still absent.
- Addressables content, Data Monolith payload, build/device/profiler artifacts,
  and real headset/deck/desktop captures are still absent.

Deliberately not done in R34:

- No manual `QualitySettings.asset` rewrite.
- No dotnet build, Unity import, player build, profiler, or device run.

## R35 Execution Update

Done in the safe-now lane:

- `XrPlatformReadinessValidator` now contains
  `WireAndroidOpenXrProviderRouteForCi()`.
- The fixer creates/uses Android XR Management settings, assigns
  `UnityEngine.XR.OpenXR.OpenXRLoader` through
  `XRPackageMetadataStore.AssignLoader`, and sets Android OpenXR render mode to
  `SinglePassInstanced`.
- XR validation now checks `XRManagerSettings.activeLoaders` for OpenXR and
  treats empty legacy `m_BuildTargetVRSettings` as fatal only when XR
  Management lacks a provider route.
- `Hecton8.Editor.asmdef` now explicitly references the XR package assemblies
  used by the editor validator.
- `PlatformPortabilityProofAudit.py` is schema v6 and reports
  `xrProviderRouteFixerPresent` and `xrProviderRouteValidatorPresent`.

Current static blockers remain:

- The Android OpenXR route fixer has not been executed inside Unity, so
  serialized `xrProviderSerializedProof` remains `false`.
- The Quest quality fixer has not been executed inside Unity, so serialized
  `questUrpWiredToAndroidQuality` remains `false`.
- Addressables content, Data Monolith payload, build/device/profiler artifacts,
  and real headset/deck/desktop captures are still absent.

Deliberately not done in R35:

- No manual XR ProjectSettings or `.asset` YAML rewrite.
- No dotnet build, Unity import, player build, profiler, or device run.

## R36 Execution Update

Done in the safe-now lane:

- Added `PlatformPortabilityRouteRepairer.WireAndroidQuestXrRoutesForCi()` as
  the one-call Unity editor route repair entrypoint.
- The orchestrator calls Quest asset configuration, Quest Android quality
  routing, Android OpenXR provider routing, and hard Android XR validation in a
  fixed order.
- Added a stable `.meta` file for the new editor script.
- Exposed `XrPlatformReadinessValidator.ValidateAndroidXrReadinessForCi()` as a
  hard CI validation route.
- `PlatformPortabilityProofAudit.py` is schema v7 and reports
  `androidQuestXrRouteRepairerPresent`.

Current static blockers remain:

- The orchestrator has not been executed inside Unity, so serialized
  `xrProviderSerializedProof` and `questUrpWiredToAndroidQuality` remain false.
- Addressables content, Data Monolith payload, build/device/profiler artifacts,
  and real headset/deck/desktop captures are still absent.

Deliberately not done in R36:

- No manual settings YAML rewrite.
- No dotnet build, Unity import, player build, profiler, or device run.

## R37 Execution Update

Done in the safe-now lane:

- `PlatformPortabilityProofAudit.py` is schema v8.
- Added `artifacts.dataMonolithBakeRoute` so the audit reports Data Monolith
  compiler presence, CLI bake route, prebuild gate, output validation, atomic
  temp-write/replace, little-endian guard, production coverage gate, external
  `.h8bin` validator, source folder, and balance folder facts.
- Added `dataMonolithBakeRoutePresent` and
  `dataMonolithValidationRoutePresent` readiness flags.
- Kept `dataMonolithPresent` strictly tied to the active runtime payload:
  `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- Updated unit tests to prove the route/artifact split.

Current static blockers remain:

- `static_data.h8bin` is still absent, so Data Monolith runtime payload proof is
  still red.
- The Unity Quest/XR route repairer has not been executed/imported, so
  serialized `xrProviderSerializedProof` and `questUrpWiredToAndroidQuality`
  remain false.
- Addressables content, build/device/profiler artifacts, and real
  headset/deck/desktop captures are still absent.

Deliberately not done in R37:

- No dummy `.h8bin` generation.
- No Unity bake/import, dotnet build, player build, profiler, or device run.

## R38 Execution Update

Done in the safe-now lane:

- `PlatformPortabilityProofAudit.py` is schema v9.
- Added Addressables package manifest/lock reporting.
- Added `artifacts.addressablesRoute` so the audit reports ContentAuthority
  validation, prebuild gating, Core/High_Res/Overkill tier gate, content hash
  route, bootstrap dependency prewarm, AssetLifecycleGovernor async load route,
  blind-frame release route, telemetry dump route, and texture-tier authoring
  route facts.
- Added `addressablesPackagePresent`, `addressablesContentRoutePresent`, and
  `addressablesRuntimeLifecycleRoutePresent` readiness flags.
- Kept `addressablesContentPresent` strictly tied to real files under
  `Assets/AddressableAssetsData`.
- Updated unit tests to prove the Addressables route/artifact split.

Current static blockers remain:

- `Assets/AddressableAssetsData` contains `0` files, so Addressables content
  proof is still red.
- `static_data.h8bin` is still absent.
- The Unity Quest/XR route repairer has not been executed/imported, so
  serialized `xrProviderSerializedProof` and `questUrpWiredToAndroidQuality`
  remain false.
- Build/device/profiler artifacts and real headset/deck/desktop captures are
  still absent.

Deliberately not done in R38:

- No manual Addressables `.asset`/catalog generation.
- No Unity Addressables build/import, dotnet build, player build, profiler, or
  device run.

## R39 Execution Update

Done in the safe-now lane:

- Added `Tools/JobCompletionAudit.py`.
- Added `Tools/test_job_completion_audit.py`.
- Updated `Docs/QUALITY_GATES.md` so frame-path raw/forced completion has its
  own gate: `python Tools\JobCompletionAudit.py --fail-on-frame-path`.
- Generated `Docs/AgentLogs/JobCompletionAudit_HFI_AUDIT.md/json`.

Current static result:

- `.Complete()` findings: `531`.
- Frame-path raw/forced blockers: `0`.
- Raw runtime blockers requiring owner review: `6`.
- Raw runtime queue: two `Core/DispatcherJobFence.cs` canonical helper sites
  and four MapMagic cold sync generator sites.

Deliberately not done in R39:

- No blind MapMagic generator rewrite.
- No Unity import, dotnet build, player build, profiler, or device run.

## R40 Execution Update

Done in the safe-now lane:

- Added explicit Burst flags to 27 attributes across 15 small/attr-only files.
- Reduced `PolishMandateStaticAudit.py` Burst debt:
  `burstMissingCompileSynchronously 94 -> 67`,
  `burstMissingFloatMode 33 -> 24`,
  `burstMissingFloatPrecision 35 -> 26`.

Current static blockers remain:

- Large owner domains still need Burst flag passes:
  `CombatDamageRuntime.cs`, `Inventory/Shinobu19EconomyLedger.cs`, plus
  remaining dev/editor false-positive review surfaces.
- Quest URP/XR serialized proof remains false.
- Addressables content artifact, Data Monolith payload, build artifacts, and
  device/profiler captures remain absent.

Deliberately not done in R40:

- No broad Combat/ledger rewrite.
- No dotnet build, Unity import, player build, profiler, or device run.

## R41 Execution Update

Current DataVault red-state after recheck:

- Default `--fail-on-regression` fails closed because no active baseline is
  configured.
- Candidate v2 comparison fails: constructors `1149 -> 1233`, plus schema
  mismatch.
- Candidate v3 comparison fails: constructors `1141 -> 1233`, field
  declarations `1719 -> 1739`.
- Current classification still separates persistent declarations `1053` from
  job-input declarations `3952`.

Next DataVault work should not reset the baseline. First cut is owner triage:

- Editor/offline baker constructor growth: classify as bake-only or move to
  cold allocator/sentinel route.
- Runtime field growth: `HabitatConstructionManager`, `MapMagicBridge`,
  `ModularEquipmentEngine`, `GlobalShaderDispatcher`, `ScannerTool`.

## R42 Execution Update

Safe static-gate work completed:

- `DataVaultSovereigntyAudit.py` strips comments/string literals before direct
  constructor matching.
- DataVault reports current forbidden constructor totals by execution surface.
- Added `--fail-on-runtime-regression` for runtime-only owner-domain burn-down.
- `JobCompletionAudit.py` now reports Core `DispatcherJobFence` internal raw
  completes as `DispatcherFenceInternalRawComplete` instead of counting them as
  owner-domain raw runtime blockers.

Current static results:

- JobCompletion frame-path blockers: `0`.
- JobCompletion raw runtime blockers: `4`.
- DataVault total forbidden constructors: `1232`.
- DataVault runtime forbidden constructors: `800`.
- DataVault editor/offline forbidden constructors: `402`.
- DataVault plugin forbidden constructors: `30`.
- DataVault runtime-only regression: five field-declaration file deltas.

Next owner-domain queue:

- Runtime: `Construction/HabitatConstructionManager.cs`,
  `ModularEquipmentEngine.cs`, `MapMagicBridge.cs`,
  `Rendering/GlobalShaderDispatcher.cs`, `ScannerTool.cs`.
- Raw runtime completion review: four MapMagic plugin generator sites remain;
  do not rewrite without caller/lifecycle review.

Deliberately not done in R42:

- No baseline reset.
- No blind MapMagic generator rewrite.
- No Unity import, dotnet build, player build, profiler, or device run.

## R43 Execution Update

Runtime DataVault regression burned down:

- Added `nativeViewStruct` classification for non-owning native view/payload
  structs.
- Removed `ScannerTool._scannerBlackBoxRing` as a persistent `NativeArray`
  class field.
- Preserved scanner black-box telemetry through the existing Vault generation
  handle and local resolved views.

Current gate results:

- Runtime-only DataVault regression gate: PASS.
- Global DataVault candidate gate: FAIL expected on editor/offline bake debt.
- Forbidden declarations reduced to `1305`.
- Persistent declarations reduced to `1052`.

Remaining DataVault burn-down:

- Editor/offline direct constructors in `GeographySanity`, `TopographyForge`,
  `HydraulicErosionForge`, `InteriorClutterForgeJobs`, `BiomeWeightMapBaker`,
  `OfflineHadalTrenchBaker`, `StaticCaveSdfBaker`, and
  `VoxelTerrainSeamBinder`.
- Editor/offline declaration deltas in
  `World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs` and
  `World/OfflineHadalTrenchBaker/Editor/HadalTrenchForgeWindow.cs`.

Still not proven:

- Unity compile/import.
- Player/runtime memory behavior.
- Quest/Android thermal or headset proof.

## R46 Execution Update

Hadal Trench editor preview ownership reduced:

- `HadalTrenchForgeWindow` static preview arrays now allocate with
  `H8Memory.Allocate<T>(..., SystemID.ContentAuthority, Allocator.Persistent)`
  and release with `H8Memory.Release`.
- Added `H8MEMORY_TRACKED_EDITOR_PREVIEW` marker and DataVault audit handling
  for tracked editor preview caches.
- Added `Hecton8.Core.Memory` to the editor-only Hadal Trench asmdef.
- Added unit coverage for tracked editor preview cache declarations.

Current static result:

- Runtime-only DataVault regression gate: PASS.
- Global DataVault no-regression gate: FAIL expected.
- Direct constructors: `1236`.
- Forbidden constructors: `1230`.
- Runtime forbidden constructors: `800`.
- Editor/offline forbidden constructors: `400`.
- Editor/offline allocator split: `Persistent=28`, `Temp=31`,
  `TempJob=317`, `Unknown=24`.
- Forbidden declarations: `1277`.
- Persistent declarations: `1022`.
- Editor/offline persistent preview declarations: `4`, with the Hadal Trench
  preview fields now tracked/allowed by the audit.
- Assembly dependency audit: PASS_WITH_WARNINGS, cycles `0`.

Still not proven:

- Unity compile/import.
- Player/runtime memory behavior.
- Quest/Android thermal or headset proof.

## R44 Execution Update

Job completion classification tightened:

- `JobCompletionAudit.py` now separates MapMagic plugin graph generation
  barriers as `PluginSynchronousGeneratorRawComplete`.
- Added optional review gate:
  `python Tools\JobCompletionAudit.py --fail-on-plugin-sync-complete`.
- Added unit coverage proving MapMagic plugin sync completes remain visible but
  no longer count as generic raw runtime owner blockers.

Current static result:

- JobCompletion findings: `529`.
- Frame-path blockers: `0`.
- Raw runtime blockers: `0`.
- Plugin synchronous generator review sites: `4`.

Deliberately not done in R44:

- No blind MapMagic generator rewrite.
- No Unity import, dotnet build, player build, profiler, or device run.

## R45 Execution Update

DataVault editor/offline classification tightened:

- Constructor findings now include allocator classes.
- Report now exposes global forbidden constructor allocator split and
  editor/offline allocator split.
- Editor/offline multi-frame bake session native fields are classified as
  `editorOfflineSessionScratchField`.
- Static editor preview cache fields are classified as
  `editorOfflinePersistentPreviewField` and remain gate-relevant.

Current static result:

- Runtime-only DataVault regression gate: PASS.
- Global DataVault no-regression gate: FAIL expected.
- Direct constructors: `1238`.
- Forbidden constructors: `1232`.
- Runtime forbidden constructors: `800`.
- Editor/offline forbidden constructors: `402`.
- Editor/offline allocator split: `Persistent=30`, `Temp=31`,
  `TempJob=317`, `Unknown=24`.
- Forbidden declarations: `1279`.
- Persistent declarations: `1022`.
- Editor/offline session scratch declarations: `22`.
- Editor/offline persistent preview declarations: `4`.

Remaining DataVault burn-down:

- Do not migrate local `TempJob` bake buffers to `GlobalDataVault`.
- Review editor/offline `Allocator.Persistent` direct constructors by owner.
- `HadalTrenchForgeWindow` static preview cache remains true editor ownership
  debt unless converted to a tracked editor preview scratch route or explicitly
  approved with reload/quit disposal proof.

Still not proven:

- Unity compile/import.
- Player/runtime memory behavior.
- Quest/Android thermal or headset proof.

## R47/R48 Execution Update

DataVault no-regression recovered and platform compute gate tightened:

- `GeographySanityPipeline.cs` and `TopographyForgeGenerator.cs` now route
  persistent editor/offline `NativeArray<T>` allocations through
  `H8Memory.Allocate<T>` / `H8Memory.Release` with
  `SystemID.ContentAuthority`.
- Disposable editor `TempJob` scratch remains local and classified as
  transient scratch.
- Runtime DataVault no-regression gate: PASS.
- Full DataVault no-regression gate: PASS.
- `PlatformPortabilityProofAudit.py` is schema v10.
- Added `--fail-on-runtime-asset-high-risk-compute` for dormant runtime compute
  assets with risky numeric thread groups.
- Android sustained-performance mode is serialized on.
- Shader warmup and bootstrap explicit warmup proof are present.

Current static result:

- DataVault direct constructors: `1215`.
- DataVault forbidden constructors: `850`.
- DataVault runtime forbidden constructors: `800`.
- DataVault editor/offline forbidden constructors: `20`.
- Runtime asset risky compute groups: `3`.
- Runtime-referenced risky compute groups: `0`.
- `Hecton_SonarMap.compute:59` is flagged as `[numthreads(8,8,8)]` =
  `512` threads and remains unreviewed for mobile/TBDR.
- Quest URP asset exists, but Android default quality still does not resolve
  to `URP_Quest_VR`.
- XR provider serialized proof remains absent.

Still not done:

- Do not manually edit `QualitySettings.asset`; run the existing Unity
  import-aware route fixer when Unity/dotnet are idle.
- Do not change compute thread groups until the dispatch caller/mobile variant
  has been reviewed.
- Unity compile/import, player build, Quest headset run, profiler, GC, memory,
  thermal, Deck, macOS, Linux, PICO, and console proof are still absent.

## R49 Execution Update

Platform compute dispatch proof was tightened:

- `PlatformPortabilityProofAudit.py` schema is now
  `hecton8.platform_portability_proof_audit.v11`.
- Added C# dispatch caller scan for `.Dispatch` / `.DispatchCompute`.
- Added runtime hard gate:
  `python Tools\PlatformPortabilityProofAudit.py --fail-on-runtime-compute-dispatch-without-threadgroup-query`.
- Current counts: compute dispatch calls `115`, runtime `111`, dispatch calls
  without file-level `GetKernelThreadGroupSizes` `69`, runtime `65`, caller
  files without query `25`, runtime `23`.

Quest route attempt:

- Unity batchmode was launched only after CPU/process preflight allowed it.
- The correct Editor API method was requested:
  `Hecton8.Editor.Build.QuestVulkanRenderPipelineConfigurator.WireQuestAndroidQualityRouteForCi`.
- It did not execute because Unity import/compile failed first.
- Removed concrete Unity 6000 editor compile blockers:
  nonexistent `MeshUpdateFlags.DontRecalculateNormals` in
  `WreckageForgeWindow.cs`, `VoxelTerrainSeamPreviewGizmo.cs`, and
  `VoxelTerrainSeamBinderPipeline.cs`; missing `UnityEditor.UIElements` for
  `ObjectField`; and removed `Mesh.MeshData.GetVertexAttribute` calls in
  Habitat/Interior offline bake paths.
- Unity was not rerun after these patches because CPU preflight reported
  `81%`, above the project gate.
- Remaining Unity compile wall may include a Burst ILPP exception in
  `Hecton8.MockDomain.Runtime` if it reproduces after the API blockers are
  cleared.

Still not done:

- Quest URP is still unwired to Android default quality.
- Runtime compute dispatch caller debt remains; do not patch dispatch group
  constants blindly.
- No player/device/profiler proof exists.

## R50 Execution Update

`.Complete()` classification was recaptured rather than rewritten blindly:

- `JobCompletionAudit.py` reports findings `534`, frame-path blockers `0`,
  raw runtime blockers `0`, plugin synchronous generator completions `4`.
- `python -B Tools/test_job_completion_audit.py`: PASS, 4 tests.
- Plugin generator barriers remain review-only; owner-domain code was not
  mutated.
- Unity/Quest route was not rerun because CPU preflight reported `100%`.

## R51 Execution Update

MockDomain Burst ILPP trigger was reduced:

- `MockContractImplementation.cs` no longer compiles an empty no-op callback
  through `BurstCompiler.CompileFunctionPointer` in a static initializer.
- The mock still returns `PhysicsFacade` with a default no-op function pointer
  and the supplied buffer handle.
- `python Tools/AssemblyDependencyAudit.py`: PASS_WITH_WARNINGS, cycles `0`.
- Unity import proof is still absent; CPU preflight remained `100%`, so the
  Quest route was not rerun.

## R52 Execution Update

Leaf Burst flag burn-down:

- Added `CompileSynchronously = true` to four `ErosionTestHarness` editor bake
  jobs and ten `VFX/Debris/ShinobuDeltaCrusherJobs.cs` jobs.
- `python Tools/PolishMandateStaticAudit.py`: PASS_WITH_WARNINGS,
  `burstMissingCompileSynchronously` `67 -> 53`.
- Remaining Burst drift still needs owner-domain slicing; no bulk rewrite was
  performed.
