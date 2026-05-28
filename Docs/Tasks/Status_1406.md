# Status 1406 - QUEST_URP_QUALITY_CONFIGURATOR_AND_VR_FOVEATION_DRIVER

Status: APEX_STATIC_REAUDIT_UPDATED_PENDING_UNITY_AND_DEVICE_VERIFICATION
Domain: Echelon 8 Rendering/XR URP Quest VR
Task Count: 19
Prompt Source: Docs/Tasks/CURRENT_BATCH.md `<AGENT_PROMPT id="1406">`
Last Successful Prompt Extraction From Saved State: 2026-05-28; Task count revalidated by `Task NN:` regex = 19
Current Prompt Recheck: 2026-05-28 FAILED; `Docs/Tasks/CURRENT_BATCH.md` now starts with `<AGENT_PROMPT id="1400">`, and exact `rg` for 1406/QUEST_URP returned exit code 1.

Relevant Mandates Read:
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_Foveated_Simulation_LOD.txt
- REND_VRS_MX350_Reality_Check.txt
- REND_VR_Stencil_Masking.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Execution_Phases.txt

## Loop 1 - Tasks 01-05
- [x] Task 01: EXHAUSTIVE_XR_SETTINGS_INQUISITION | DOD: raw YAML/static scan ledger at `Docs/Reports/QUEST_VR_STATIC_LEDGER_1406.json`, no Unity claims | Alternative rejected: Unity import/build first; too expensive and not needed for discovery | Estimate: 1800000 us static scan wall
- [x] Task 02: URP_PIPELINE_BLOAT_ANALYSIS | DOD: exact Quest URP bloat paths identified: MSAA, renderScale, lights, cascades, volume profile | Alternative rejected: blind profile duplication; YAML corruption risk | Estimate: 3200000 us static audit wall
- [x] Task 03: FOVEATION_API_AVAILABILITY_MAPPING | DOD: OpenXR route mapped to `XRDisplaySubsystem.foveatedRenderingLevel`; no `Unity.XR.Oculus` package/OVRManager route | Alternative rejected: hard dependency on Oculus API; breaks OpenXR/no-plugin cases | Estimate: 900000 us scan wall
- [x] Task 04: QUALITY_CONTROLLER_INTEGRATION_PLANNING | DOD: existing `FoveatedRenderCommander` and `HomeostasisBrain.GlobalQualityWeight` preserved as owner/injection route | Alternative rejected: per-frame registry polling and second writer | Estimate: 2400000 us source audit wall
- [x] Task 05: TELEMETRY_AND_REPORTING_PLANNING | DOD: final JSON path/schema fixed as `Docs/Reports/QUEST_VR_OPTIMIZATION_REPORT_1406.json` plus SHA-256 hashes | Alternative rejected: chat-only reporting | Estimate: 350000 us planning wall

## Loop 2 - Tasks 06-10
- [x] Task 06: URP_QUEST_PROFILE_MATERIALIZATION | DOD: isolated `URP_Quest_VR.asset` patched to MSAA2/renderScale0.85/1 cascade/no inherited global volume | Alternative rejected: edit PC URP assets | Estimate: 3500 us estimated Quest GPU frame savings from render scale/MSAA, unprofiled
- [x] Task 07: QUALITY_SETTINGS_ANDROID_BINDING | DOD: Android default quality index remains 3 and Quest quality uses GUID `d9c4cd6a763fec04a913c6a149663003` | Alternative rejected: global default downgrade | Estimate: 0 us runtime; binding correctness only
- [x] Task 08: XR_SINGLE_PASS_INSTANCED_ENFORCEMENT | DOD: Android OpenXR `m_renderMode: 1` verified and multiview region optimization enabled | Alternative rejected: multi-pass acceptance | Estimate: 1500 us estimated CPU/GPU draw overhead avoidance, unprofiled
- [x] Task 09: FOVEATION_DRIVER_IMPLEMENTATION | DOD: `Assets/_Project/Scripts/Visor/QuestFoveationDriver.cs` added, OpenXR/Unity XR only, no Oculus package dependency | Alternative rejected: unguarded plugin references and duplicate MonoBehaviour owner | Estimate: 1000-3000 us estimated GPU savings when runtime honors foveation
- [x] Task 10: DYNAMIC_RESOLUTION_SCALING_HOOKS | DOD: `FoveatedRenderCommander` now consumes driver continuous `GlobalQualityWeight` mapping with Android Quest 0.35-0.85 foveation range; PCVR keeps relief path | Alternative rejected: low/high dichotomy | Estimate: 0 us CPU target; visual pressure mapping only

## Loop 3 - Tasks 11-14
- [x] Task 11: POST_PROCESSING_CULLING_PASS | DOD: Quest URP no longer references shared Bloom-active `SampleSceneProfile`; SSDO/depth fog/volumetric shafts verified inactive | Alternative rejected: desktop PP inheritance | Estimate: 600-1600 us estimated Quest GPU savings, unprofiled
- [x] Task 12: COMPILE_WALL_AND_NAMESPACE_HYGIENE | DOD: driver uses `UnityEngine.XR`, `UnityEngine.Rendering`, `Unity.Mathematics`; no `Unity.XR.Oculus`; brace scan and diff check passed | Alternative rejected: broad XR package dependency | Estimate: 0 us runtime
- [x] Task 13: DRY_RUN_VERIFICATION_EXECUTION | DOD: Rationale decisions 003-007 record GUID/property/YAML simulation and validation results | Alternative rejected: blind text replacement | Estimate: 0 us runtime
- [BLOCKED_BY_CONTENTION] Task 14: BATCHED_COMPILATION_AND_EXECUTION_CHECK | DOD: CPU/csc gate executed; CPU 100.0%, csc 0, dotnet 1, build skipped | Alternative rejected: unthrottled dotnet build | Estimate: host CPU preserved

## Loop 4 - Tasks 15-17
- [x] Task 15: YAML_CORRUPTION_FUZZER_TEST | DOD: `QuestVrOptimizationValidator1406` added with AssetDatabase loads plus static YAML checks; PowerShell YAML header/tab scan passed | Alternative rejected: visual inspection only | Estimate: 1100000 us static validator wall
- [x] Task 16: FOVEATION_API_MOCK_TEST | DOD: editor validator covers unsupported caps abort, disabled level, survival high target, visual-overkill low floor | Alternative rejected: assume Oculus installed | Estimate: 0 us runtime; editor-only
- [x] Task 17: URP_FEATURE_STRIPPING_ASSERTION | DOD: validator and PowerShell scanner assert SSDO, NoirDepthFog, and VolumetricShafts inactive | Alternative rejected: manual checklist only | Estimate: 1100000 us static scan wall

## Loop 5 - Tasks 18-19
- [x] Task 18: ZERO_COMPILATION_HOT_PATH_VERIFICATION | DOD: driver hot path audited line-by-line: no `new`, LINQ, strings, coroutine, scene search, registry polling | Alternative rejected: profiler claim without access | Estimate: 0 B/frame target by static scan; measured proof absent
- [x] Task 19: AUTOMATED_METRIC_VALIDATOR_REPORT | DOD: `QUEST_VR_OPTIMIZATION_REPORT_1406.json` written with SHA-256 hashes and `LOG_1406.md` appended | Alternative rejected: fake runtime metrics | Estimate: 0 us exact; estimates in report are explicitly unprofiled

## Verification Notes
- Unity runtime/profiler proof: PENDING VERIFICATION.
- dotnet build: BLOCKED_BY_CONTENTION on initial gate; CPU_TOTAL_PERCENT=100.0, CSC_COUNT=0, DOTNET_COUNT=1.
- dotnet build: BLOCKED_BY_CONTENTION on APEX gate; CPU_TOTAL_PERCENT=72.8, CSC_COUNT=0, DOTNET_COUNT=0.
- dotnet build: BLOCKED_BY_CONTENTION on post-APEX final gate; CPU_TOTAL_PERCENT=100.0, CSC_COUNT=0, DOTNET_COUNT=0.
- dotnet build: BLOCKED_BY_CONTENTION on final route/cache gate; CPU_TOTAL_PERCENT=100.0, CSC_COUNT=1, DOTNET_COUNT=1.
- dotnet build: BLOCKED_BY_CONTENTION on Shapes strip final gate; CPU_TOTAL_PERCENT=100.0, CSC_COUNT=0, DOTNET_COUNT=1.
- Current prompt source hygiene: FAILED for live `CURRENT_BATCH.md`; 1406 assignment survives only in this status/rationale/report evidence set.
- Current blocker: Unity import/build/device proof unavailable under contention.

## Loop 6 - APEX Final Verification
- [x] APEX 01: Unity 6 multiview enum correction | DOD: local package source proves `FinalPass=1`; Android OpenXR `m_multiviewRenderRegionsOptimizationMode: 1` at line 1157 and MetaQuest Android line 1229 | Alternative rejected: relying on obsolete bool migration | Estimate: 1000-3000 us Quest GPU potential, unprofiled
- [x] APEX 02: Symmetric projection prerequisite | DOD: package validation rule requires symmetric projection for multiview region benefit; Android line 1155 and MetaQuest line 1222 set to 1 | Alternative rejected: enabling MVPVV with no effect | Estimate: avoids wasted MVPVV setup cost
- [x] APEX 03: Validator false-marker fix | DOD: `QuestVrOptimizationValidator1406.ExtractNamedBlock` exact full-line matching at lines 127-171; avoids `AndroidMouseInteractionProfile Android` false match | Alternative rejected: substring marker search | Estimate: 0 us runtime, editor-only proof correctness
- [x] APEX 04: Forensic dump path correction | DOD: `FoveatedRenderCommander.cs:66` now writes `Dump_1406.bin`; project fallback path resolves to `Docs/AgentLogs/Dump_1406.bin` | Alternative rejected: retaining previous agent dump path | Estimate: 0 us normal runtime
- [x] APEX 05: Zero-GC static proof | DOD: modified hot slices scan all zero for `new`, `string.Format`, `.ToString()`, LINQ, `foreach`, coroutine, GetComponent, scene search | Alternative rejected: runtime claim without GCMonitor | Estimate: 0 B/frame target, measured proof absent
- [x] APEX 06: Final JSON proof artifact | DOD: `Docs/Reports/QUEST_VR_OPTIMIZATION_REPORT_1406.json` parses; latest SHA-256 after Loop 10 is `57416b401987e160e340c92ee708d4d2d475e66940c71a7b2d4c7709b6d5fd97` | Alternative rejected: chat-only APEX proof | Estimate: 0 us runtime

## Loop 7 - Post-APEX Domain Reaudit
- [x] REAUDIT 01: Quest prebuild configurator sabotage check | DOD: `QuestVulkanRenderPipelineConfigurator` no longer restores MSAA4/renderScale1/depth0; it now writes the same Quest URP invariants as `URP_Quest_VR.asset` | Alternative rejected: leaving Android preprocess to undo static YAML edits | Estimate: preserves 2600-5000 us estimated GPU savings from prior URP cuts, unprofiled
- [x] REAUDIT 02: Quest renderer strip persistence | DOD: prebuild strip list now includes `RetinaDistortion` and `VisorFluidDistortion`; renderer YAML already has both `m_Active: 0` | Alternative rejected: accepting fullscreen VR distortion passes on mobile survival path | Estimate: avoids unknown stereo fullscreen pass cost, device proof absent
- [x] REAUDIT 03: Runtime camera texture force bypass | DOD: `HectonUrpTextureRequirementsGuard` and `HectonUnderwaterVisuals` return after depth texture preservation under `QuestVrMobileSurvivalPolicy`, so they stop forcing opaque color texture and postprocess on Quest | Alternative rejected: clearing camera settings globally or editing shared PC volume profiles | Estimate: protects 600-1600 us estimated Bloom/global PP saving, unprofiled
- [x] REAUDIT 04: Expanded Zero-GC hot scan | DOD: added guard and underwater camera requirement slices to the scan; 7 hot slices all report 0 for reference `new`, `string.Format`, `.ToString()`, LINQ, `foreach`, coroutine, GetComponent, scene search | Alternative rejected: assuming non-driver hot guards were harmless | Estimate: 0 B/frame static target, measured proof absent

## Loop 8 - Build Route And Camera Lookup Reaudit
- [x] REAUDIT 05: Android prebuild ordering and route closure | DOD: `QuestVulkanRenderPipelineConfigurator.callbackOrder` is now `-4700`, before `GraphicsApiMatrixValidator -4650` and `XrPlatformReadinessValidator -4610`; `OnPreprocessBuild` now enforces Quest quality row and Android Vulkan itself | Alternative rejected: depending on optional CI/menu repair helpers | Estimate: 0 us runtime, prevents build-route configuration drift
- [x] REAUDIT 06: BeginCameraRendering component lookup cache | DOD: `HectonUrpTextureRequirementsGuard` now uses a fixed 32-slot camera-data cache; exact scan shows 0 `GetComponent(` calls in the per-frame cache-hit path, with `TryGetComponent` retained only for cache misses | Alternative rejected: dictionary cache or scene-wide camera search; both add managed/container risk | Estimate: avoids repeated native component lookup per camera per frame; exact us not profiled
- [x] REAUDIT 07: Final prompt/build/proof refresh | DOD: robust prompt extraction found `START=545 END=629 TASK_COUNT=19`; final JSON parses; latest build gate CPU_TOTAL_PERCENT=100.0, CSC_COUNT=0, DOTNET_COUNT=0, build skipped | Alternative rejected: launching `dotnet build` with host CPU >50% | Estimate: host CPU preserved

## Loop 9 - APEX Reaudit Corrections After Self-Audit
- [x] REAUDIT 08: Quest camera color-force order proof | DOD: `HectonUrpTextureRequirementsGuard.cs:104` and `HectonUnderwaterVisuals.cs:1267` return on Quest before `requiresColorOption` mutation; validator asserts token order | Alternative rejected: forcing opaque texture and relying on URP asset flag to win | Estimate: protects unprofiled stereo opaque-copy/postprocess cost
- [x] REAUDIT 09: Biome fog DataVault one-sample job removal | DOD: `HectonUnderwaterVisuals.ApplyBiomeFogBlend` lines 3623-3667 replaces the scheduled job; `BufferID.UnderwaterBiomeFog*`, `_biomeFogVault`, and `BiomeTransitionFogBlendJob job` are absent from `HectonUnderwaterVisuals.cs` | Alternative rejected: six DataVault write locks for one visual sample | Estimate: avoids tiny-job/same-frame readback overhead; exact us not profiled
- [x] REAUDIT 10: Hot token cleanup and runtime camera fallback trim | DOD: `FoveatedRenderCommander.WriteTelemetry` lines 957-980 uses `default` plus field assignments, not `new`; runtime `ResolveMainCamera` root string search fallback removed | Alternative rejected: explaining struct `new` away in proof while leaving scanner ambiguity | Estimate: 0 B/frame static target preserved
- [x] REAUDIT 11: Final proof refresh | DOD: hot-slice scan covers 10 slices and reports 0 for `new`, `string.Format`, `.ToString`, LINQ, `foreach`, coroutine, non-Try `GetComponent`, and scene search; report SHA-256 `da701b6d617d75a77e5f28411c82ce4c416f44aff71c5ed137df0e6a0e2c5217`; CPU gate `CPU_TOTAL_PERCENT=100; CSC_COUNT=0; DOTNET_COUNT=0; VBCS_COUNT=0` so build skipped | Alternative rejected: launching `dotnet build` under saturated CPU | Estimate: host CPU preserved

## Loop 10 - Final Artifact Synchronization
- [x] REAUDIT 12: Final report/hash/CPU synchronization | DOD: report JSON parses after stale `Quest_VR_Renderer.asset` hash and final CPU process counts were corrected; latest report SHA-256 `57416b401987e160e340c92ee708d4d2d475e66940c71a7b2d4c7709b6d5fd97`; final gate `CPU_TOTAL_PERCENT=100; CSC_COUNT=1; DOTNET_COUNT=1; VBCS_COUNT=0` so build skipped | Alternative rejected: leaving stale evidence in the proof artifact | Estimate: host CPU preserved

## Loop 11 - Quest Renderer Feature Reaudit
- [x] REAUDIT 13: Third-party Shapes immediate-mode strip | DOD: `Quest_VR_Renderer.asset:291-293` now has `ShapesRenderFeature m_Active: 0`; vendor source evidence is `Assets/Shapes/Scripts/Runtime/Immediate Mode/ShapesRenderFeature.cs:27-29` using `DrawCommand.cBuffersRendering`, `foreach`, and `ObjectPool<ShapesRenderPass>.Alloc`; prebuild strip assertion is `QuestVulkanRenderPipelineConfigurator.cs:188`; validator assertion is `QuestVrOptimizationValidator1406.cs:104` | Alternative rejected: editing vendor Shapes runtime or leaving an unowned third-party per-camera enqueue path in Quest survival renderer | Estimate: unknown us; headset profiler proof absent
- [x] REAUDIT 14: Live prompt source drift recorded | DOD: exact `rg -n "<AGENT_PROMPT id=\"1406\"|QUEST_URP|1406" Docs/Tasks/CURRENT_BATCH.md` returned exit code 1; report JSON now marks `promptExtraction=FAIL_CURRENT_BATCH_1406_NOT_FOUND`; current report SHA-256 `d27f3ec3b16c63324560876ac0e938b593e0114916d8f294780000e442232af9` | Alternative rejected: preserving stale `promptExtraction: PASS` claim | Estimate: 0 us runtime; evidence integrity only

## Loop 12 - Renderer Enqueue And Proof Reaudit
- [x] REAUDIT 15: Quest renderer feature map integrity | DOD: decoded `m_RendererFeatureMap` as 13 little-endian int64 entries and matched all 13 `m_RendererFeatures` fileIDs in order; behavioral renderer diff remains only `ShapesRenderFeature m_Active: 1 -> 0` | Alternative rejected: leaving YAML list/map drift as unproved coincidence | Estimate: 0 us runtime; import-risk reduction only
- [x] REAUDIT 16: Fluid advection owner-null enqueue guard | DOD: `HectonFluidAdvectionRenderFeature.cs:125-127` copies `_cachedFluidEngine` to local owner and returns before `_pass.Setup`/`EnqueuePass` when null; hot slice 120-135 scan reports `new=0`, `string.Format=0`, `.ToString=0`, LINQ=0, `foreach=0`, coroutine=0, `GetComponent=0`, `TryGetComponent=0`, scene search=0 | Alternative rejected: enqueue pass with null owner and rely on `RecordRenderGraph` to fail later | Estimate: unknown us; avoids no-owner no-op render pass setup
- [x] REAUDIT 17: PDA projector non-game camera enqueue guard | DOD: `WristPdaScreenProjectorFeature.cs:187-189` returns before setup/enqueue for Preview, Reflection, and SceneView cameras; validator asserts order at `QuestVrOptimizationValidator1406.cs:179-185`; hot slice 182-192 scan reports all audited allocation/search tokens at 0 | Alternative rejected: let AddRenderPasses enqueue and depend on RecordRenderGraph to reject camera type later | Estimate: unknown us; avoids no-op enqueue for editor/reflection cameras
- [x] REAUDIT 18: Final evidence sync after Loop 12 | DOD: report JSON parses; final report SHA-256 `91952cf3de133ef7c5e82356108cd0ec199b9b91d36e4575708649ec16412231`; final build gate `CPU_TOTAL_PERCENT=100; CSC_COUNT=0; DOTNET_COUNT=0; VBCS_COUNT=0`, so `dotnet build` was not launched | Alternative rejected: running compilation under CPU >50% | Estimate: host CPU preserved
