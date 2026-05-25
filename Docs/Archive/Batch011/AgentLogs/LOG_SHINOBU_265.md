# LOG_SHINOBU_265

## 2026-05-21 - Water Optics Graft Static Pass

<SELF_AUDIT agent_id="SHINOBU_265" domain="Echelon 7 Graphics and Rendering / Water Optics" evidence_class="STATIC_SOURCE_STATIC_DOC">
  <TWENTY_TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Crest package HLSL inspected because `Assets/~Quarantine_Crest5` is absent. Extracted Beer-Lambert extinction, scatter ratio, and phase-scatter concepts without taking a Crest include/runtime dependency.</TASK>
    <TASK id="02" status="FAIL_BLOCKED">Scanner/report exists and static scan found `0` `m_Fog: 1` tokens, but `3` generic Volume/Profile tokens remain in scene/profile assets. Blind YAML deletion rejected; needs scene/profile owner review.</TASK>
    <TASK id="03" status="PASS">Water optics DTO/profile/tuning/telemetry rows use explicit raw public fields. Targeted scan found no hot DTO `{ get; set; }`/`{ get; private set; }` residue.</TASK>
    <TASK id="04" status="PASS_STATIC">`WaterOpticsLayoutValidator` validates 64-byte DTO/profile/tuning/telemetry sizes and `WaterOpticsDTO` offsets `0/16/32/48`.</TASK>
    <TASK id="05" status="PASS_STATIC">`GenerateMockWaterOpticsJob` writes fallback abyssal absorption/scattering/light params from Vault-backed tuning into `WaterOpticsDTO`.</TASK>
    <TASK id="06" status="PASS_STATIC">UberNoir now calls `H8WaterOpticsApplyBeerLambert` after main lighting, so extinction affects lit surface color rather than material albedo.</TASK>
    <TASK id="07" status="PASS_STATIC">Directional in-scatter uses view vector, main light direction, Schlick/HG-style phase, scattering/extinction ratio, and `_GlobalWaterOptics` light color.</TASK>
    <TASK id="08" status="PASS_STATIC">Dear Lie waterline tint is a screen-space shallow-water gradient, not a CPU water-surface physics simulation.</TASK>
    <TASK id="09" status="FAIL_BLOCKED">Biome-specific live routing is blocked. Existing biome route owns absorption/hash in World DTOs and legacy shader globals; no approved Core-contract signal/DataVault lane carries scattering plus water-optics profile hash to Rendering/WaterOptics.</TASK>
    <TASK id="10" status="PASS_STATIC">Shader ALU scales continuously by `GlobalQualityWeight`; low quality collapses toward scalar mono transmittance, higher quality restores spectral correction and stronger in-scatter.</TASK>
    <TASK id="11" status="PASS_STATIC">Upload path cold-acquires double-buffered `GraphicsBuffer.Target.Constant`, uses `LockBufferForWrite`, direct 64-byte `UnsafeUtility.MemCpy`, and `Shader.SetGlobalConstantBuffer`; no water-optics `Shader.SetGlobalVector` route and no VisualSync buffer allocation repair.</TASK>
    <TASK id="12" status="PASS_STATIC">Surface Y is converted to camera-local AUP delta before packing as float into `QualityAndDepthLimits.y`.</TASK>
    <TASK id="13" status="PASS_DOC">Route card and ledger mark optics DTO/profile/telemetry as presentation/proof-only, excluded from rollback/save/Merkle truth.</TASK>
    <TASK id="14" status="PASS_STATIC">DataVault buffers are acquired with `NativeArrayOptions.UninitializedMemory`; frame writes overwrite rows instead of clearing every update.</TASK>
    <TASK id="15" status="PARTIAL_STATIC_MARKER">300-frame Vault telemetry ring, dump path, URP RenderGraph `CommandBuffer` marker route, and renderer-feature installer/build guard exist. Exact Unity profiler/GPU timestamp proof is still not captured; current microseconds are estimated fields.</TASK>
    <TASK id="16" status="PASS_STATIC">UI Toolkit Abyssal Optics Tuner exposes coefficients, light, surface Y, max distance, signed quality bias, active flag, runtime telemetry graph, and pushes runtime tuning through the water-optics owner.</TASK>
    <TASK id="17" status="PASS_STATIC">Editor/development cold/reload CSV parser uses `ReadOnlySpan<byte>` and writes `WaterOpticsProfileDTO` including signed quality bias; no `string.Split` row-object path and no player `StreamingAssets` text load.</TASK>
    <TASK id="18" status="PASS_STATIC">Tuner includes a 64-swatch Beer-Lambert extinction preview.</TASK>
    <TASK id="19" status="PASS_STATIC_WITH_FINDINGS">`PostProcess_Fog_Scanner` exists and report was written; report is not green because static findings remain.</TASK>
    <TASK id="20" status="PASS_STATIC_WITH_LIMITS">This self-audit was appended. Unity import, shader import, Frame Debugger, profiler/GCMonitor, GPU timestamp, and player build remain pending proof artifacts.</TASK>
  </TWENTY_TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION primary="WaterOpticsDTO">
    Field0: `AbsorptionCoefficientsRGB float4` offset 0, size 16.
    Field1: `ScatteringCoefficientsRGB float4` offset 16, size 16.
    Field2: `DirectionalLightColorAndIntensity float4` offset 32, size 16.
    Field3: `QualityAndDepthLimits float4` offset 48, size 16.
    Math: 16 + 16 + 16 + 16 = 64 bytes. Explicit layout size = 64. This is exactly one 64-byte L1 cache line and a multiple of 8/16/32/64. No `Pack=1`; no managed references; not an atomic counter row, but still cache-line aligned.
    Dump header: `WaterOpticsDumpHeader` is explicit 32 bytes and validates separately; it is not shader, rollback, save, or hot-array truth.
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    `QualityAndDepthLimits.x` carries continuous `GlobalQualityWeight`. Below roughly 0.3 the shader path collapses toward one scalar extinction evaluation and a cheap rational spectral correction; volumetric fog/Dear Lie reduce toward scalar tint and shallow-water proxy. Middle weights restore partial RGB separation and moderate in-scatter. High/ultra weights increase spectral correction and directional scattering without changing DTO layout, authority, save identity, or rollback state. There are no water-optics shader keywords or binary low/high hardware switches.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Runtime declares zero private `NativeArray`, `NativeList`, or `NativeHashMap` persistent fields. Persistent native state is requested from GlobalDataVault via generation handles: `71129 ShinobuWaterOpticsTuning`, `71135 ShinobuWaterOpticsParams`, `71136 ShinobuWaterOpticsProfiles`, `71137 ShinobuWaterOpticsTelemetryRing`, `71138 ShinobuWaterOpticsTelemetryCursor`, `71139 ShinobuWaterOpticsCsvScratch`. GraphicsBuffer fields are GPU upload resources, cold allocated/released, not gameplay truth or rollback memory.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    `GenerateMockWaterOpticsJob` consumes dispatcher `dependsOn` and returns `job.Schedule(dependsOn)` from `ScheduleSimulation`. Its `Output` and `Tuning` arrays are marked `[NoAlias]`; `Tuning` is `[ReadOnly]`. The mapped-buffer upload is not a mathematical job and now uses direct `UnsafeUtility.MemCpy` for the single 64-byte row inside `LockBufferForWrite`/`UnlockBufferAfterWrite`. No `.Complete()` call exists in the water-optics route.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    `Hecton8.Rendering.WaterOptics.asmdef` references `Hecton8.Core`, `Hecton8.Core.Memory`, Unity Burst/Collections/Jobs/Mathematics, and Unity RenderPipelines Core/Universal runtime for the scoped telemetry feature. It does not reference `Hecton8.World`, Biomes runtime assemblies, VFX sibling runtime assemblies, or concrete biome manager code. Editor assembly references runtime only for tooling.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Heavy route rejected: per-pixel or CPU waterline/surface simulation and camera-wide generic fog volume layering. Implemented route: UberNoir surface Beer-Lambert in existing material path, compute-fog tint, and DearLie screen-space waterline gradient. Complexity stays O(visible pixels) inside existing passes; no CPU Navier-Stokes, no extra object loop, no new draw-call fanout. Low quality saves roughly two channel-exp equivalents per covered opaque pixel by using scalar extinction plus continuous spectral correction.
  </DEAR_LIE_CONFIRMATION>
  <WHAT_WAS_WRONG>Objects underwater were not receiving per-surface Beer-Lambert attenuation from water depth/travel distance. Existing generic fog/post-process paths were camera-distance approximations and biome globals were not a safe rendering contract for water optics.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added 64-byte `_GlobalWaterOptics` DTO and DataVault lanes; added scheduled Burst generation job; bound a global constant buffer in VisualSync through direct 64-byte memcpy; grafted extinction/scattering into UberNoir, volumetric fog compute, and DearLie waterline; added layout validator, tuner, CSV parser, preview, fog scanner, route card, and ledger row.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>Scalar mono extinction at low quality, rational spectral correction instead of three full RGB exponentials, and screen-space waterline tint instead of water-surface physics.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>CPU: target upload route is one 64-byte copy and one constant-buffer bind, estimated under 10 us on i3/MX350 absent driver variance. GPU: low-quality shader path saves approximately two channel-exp equivalents per affected opaque pixel versus exact RGB Beer-Lambert. Exact GPU timestamp is pending.</MICROSECONDS_SAVED>
  <OPEN_BLOCKERS>Task 02 scene/profile fog token removal needs owner review. Task 09 needs a Core-contract biome water-optics payload. Task 15 needs Unity profiler/GPU timestamp proof. Unity compile/import/profiler proof was not run.</OPEN_BLOCKERS>
</SELF_AUDIT>

## 2026-05-21 - Task 15 Hardening Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics RenderGraph Telemetry">
  <WHAT_WAS_WRONG>The telemetry lane had a Vault ring and dump path, but the render-pass proof was still weak. The first marker draft used `AddUnsafePass`, stored a runtime reference in pass data, and the estimated GPU budget flag was lost before the dump gate.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added `Unity.RenderPipelines.Core.Runtime` to the water-optics asmdef, switched to `IRasterRenderGraphBuilder.AddRasterRenderPass`, moved runtime notification outside `SetRenderFunc`, kept the render func to marker begin/end only, returned final telemetry flags from `RecordTelemetry`, and guarded editor telemetry graph initialization.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>The pass is marker-only and no-draw. The opaque cost remains an estimated scalar derived from continuous spectral weight until Unity profiler/GPU timestamp proof is available.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Rejected unsafe render-func runtime calls and no draw call is introduced. Expected CPU impact remains one fixed telemetry row plus marker submission; exact microseconds are pending Unity profiler capture.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Brace/preprocessor counts are balanced for runtime, feature, and tuner. Targeted scan found no `AddUnsafePass`, `Pack=1`, DTO properties, `Shader.SetGlobalVector`, `.Complete()`, `TryGetLatestCreated`, `UnityEngine.Random`, `string.Format`, or `foreach` in the water-optics scope.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import/build, shader import, Frame Debugger binding proof, and measured GPU timestamp proof have not been run because the mandate forbids premature rebuilds.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - LUT Atlas And RenderGraph Mutation Closure Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Shader Bounds And Telemetry Ownership">
  <WHAT_WAS_WRONG>Static audit found the extinction shader still addressed a fictional 256^3 LUT through a 4096-wide atlas while the actual binary payload is a 768x256 RHalf depth/turbidity/rgb matrix. The RenderGraph marker route still mutated `WaterOpticsRuntime` through a static marker counter, and fault dumps could be requested directly from `VISUAL_SYNC` file IO. UberNoir and custom light-probe buffer reads also lacked explicit capacity proof.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Replaced the LUT address math with `x=turbidityIndex*3+rgbChannel`, `y=depthIndex`, added `_ExtinctionLUT_TexelSize` shape guards, removed `TryMarkRenderGraphTelemetrySubmitted` and the sticky marker flag/counter, left `HectonWaterOpticsTelemetryFeature` as a marker-only pass with no runtime owner call, added `_UberNoirInstanceCapacity` bounds checks, republished custom light-probe state `z` as active capacity/count, and deferred fault dump writes to `PostSimulationTick`/shutdown with retry when Vault rows are unavailable.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>The LUT remains a Beer-Lambert visual fake rather than live spectral transport. Low quality still avoids the texture fetch through continuous `GlobalQualityWeight`; high quality can use the matrix safely after dimensions prove valid.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Prevents undefined GPU reads and moves fault-path file IO out of VisualSync. Normal-frame CPU delta is one pending-dump bool check in post-simulation; no draw/pass was added.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Prompt task count revalidated as 20. Targeted scans are clean for `TryMarkRenderGraphTelemetrySubmitted`, `NotifyRenderGraphTelemetrySubmitted`, `_opaqueProfilingMarkerCount`, `TelemetryFlagCommandBufferMarker`, `H8_WATER_EXTINCTION_PACK_WIDTH 4096u`, `flatIndex &amp;`, `_MATH_LOD_LOW`, `SHADER_API_MOBILE`, and `IsLowEndHardware` in the scoped water-optics surface. `git diff --check` on touched source reported only existing LF-to-CRLF warnings. CPU guard sampled 100%; no rebuild was launched.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import, C# compile, shader import, Frame Debugger CBUFFER proof, profiler GC proof, runtime renderer-feature asset placement, authored runtime owner placement, and measured GPU marker timestamps remain pending.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Runtime Owner Explicit Authoring Route Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Bootstrap Authoring">
  <WHAT_WAS_WRONG>After runtime self-spawn removal, the build guard correctly failed on missing authored owner placement, but the codebase had no scoped WaterOptics-owned editor route for the scene owner to serialize that component without hand-editing YAML.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added `WaterOpticsRuntimeOwnerInstaller` under the WaterOptics editor assembly. It provides `Hecton8/Rendering/Water Optics/Install Runtime Owner In Bootstrap Scene`, opens `Assets/_Project/Scenes/00_BOOTSTRAP.unity` through Unity editor APIs, resolves the existing `[BOOTSTRAPPER]` root by name, and attaches `WaterOpticsRuntime` through `Undo.AddComponent` only on explicit menu invocation. The renderer/build guard failure now points at this route.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No new visual simulation. This preserves the existing Beer-Lambert/Dear Lie shader fake while removing the need for hidden runtime ownership tricks.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>0 us/frame directly. The route preserves the previous removal of runtime `GameObject` self-spawn and one-row scheduled mock job; runtime cost appears only after an authored scene owner exists.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Scoped runtime self-spawn scan over `WaterOpticsRuntime.cs` and `HectonWaterOpticsTelemetryFeature.cs` returns no matches for runtime-load hooks, scene-load callbacks, `new GameObject`, mock-job symbols, Burst attributes, or NoAlias. New installer brace/preprocessor counts are balanced and `git diff --check` is clean for the touched editor files.</STATIC_VERIFICATION>
  <REMAINING_RISK>The menu has not been executed because no Unity Editor MCP endpoint is exposed and shell scene YAML mutation is rejected. Static GUID scan still finds no serialized owner placement. Unity import, C# compile, shader import, Frame Debugger proof, profiler GC proof, and measured GPU timing remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - RenderGraph Mutable Owner Leak Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics RenderGraph Marker Ownership">
  <WHAT_WAS_WRONG>`HectonWaterOpticsTelemetryFeature.RecordRenderGraph` pulled a mutable `WaterOpticsRuntime` reference through public `TryGetRuntimeInstance` just to mark telemetry submission. The render func stayed clean, but the player render feature still had an unnecessary owner-reference read surface.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Changed the runtime feature to call `WaterOpticsRuntime.TryMarkRenderGraphTelemetrySubmitted()` before pass registration. The method gates marker submission and increments only the existing owner marker counter when a scene-local owner exists. Public `TryGetRuntimeInstance` is now compiled only for `UNITY_EDITOR`, preserving the Abyssal Optics Tuner facade while removing the player mutable-owner getter.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No visual simulation changed. The same marker-only RenderGraph proof and Beer-Lambert/Dear Lie shader fake remain active.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Expected frame-time delta is negligible; the gain is architectural: no runtime renderer feature pulls a mutable owner object just to record a marker. No draw, texture, buffer, or shader variant was added.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Focused source scan now shows runtime `TryGetRuntimeInstance` only under the editor tuner route and `HectonWaterOpticsTelemetryFeature` using `TryMarkRenderGraphTelemetrySubmitted`. Shared report, route card, status, and binary ledger were updated. Unity import/runtime proof remains pending.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import, C# compile, RenderGraph execution, Frame Debugger marker proof, GC/profiler proof, and GPU timing remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Unity Meta Identity Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Unity Asset Identity">
  <WHAT_WAS_WRONG>New WaterOptics runtime/editor folders, asmdefs, and C# source assets had no `.meta` files. Leaving that to Unity import would create machine-local GUIDs and make asmdef/tooling identity unstable under concurrent agent work.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added deterministic `.meta` files for the WaterOptics folder, Editor folder, runtime asmdef, editor asmdef, `WaterOpticsRuntime`, `HectonWaterOpticsTelemetryFeature`, `AbyssalOpticsTunerWindow`, `PostProcess_Fog_Scanner`, `WaterOpticsLayoutValidator`, `WaterOpticsRendererFeatureInstaller`, the Dear Lie shader, and the UberNoir warmup collection.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No visual cheat was added in this pass; this is deterministic import hygiene.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>0 us/frame. It prevents editor/import churn and broken assembly identity rather than changing runtime cost.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Focused missing-meta scanner now targets all new WaterOptics source and shader artifacts. Full Unity import and asset database validation remain pending under the no-premature-build guard.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity has not imported the meta files, so accepted GUID proof and renderer-feature subasset serialization proof remain pending.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Renderer Feature Binding Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Renderer Feature Binding">
  <WHAT_WAS_WRONG>The RenderGraph marker feature existed as source, but renderer assets would not necessarily contain the feature. That leaves Task 15 dependent on manual Unity editor setup.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added `WaterOpticsRendererFeatureInstaller` and `WaterOpticsRendererFeatureBuildGuard`. The installer follows the existing project pattern for renderer-feature sub-assets, normalizes duplicates, rebuilds `m_RendererFeatureMap`, verifies `AfterRenderingOpaques`, and leaves actual asset serialization to Unity APIs.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No render work was added here. This is a source-level binding guard for the marker-only pass.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Editor/build-only. It avoids runtime scene search or hot registry polling to discover render features. Runtime cost remains bounded to the marker-only pass plus one 64-byte telemetry row.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Installer braces `44/44`, preprocessor `#if/#endif 1/1`. Targeted scan still reports no `AddUnsafePass`, `Pack=1`, `Shader.SetGlobalVector`, `.Complete()`, `TryGetLatestCreated`, `UnityEngine.Random`, `string.Format`, or `foreach` in water-optics scope.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity has not imported the new installer, so renderer asset sub-asset IDs and feature map serialization are not yet runtime proof.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Tuner Quality Bias And Raw Dump Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Human Control And Black Box">
  <WHAT_WAS_WRONG>The tuner had coefficient/light/surface controls but no explicit quality scalar bias even though the tuning DTO reserved a lane for continuous quality modulation. The black-box dump wrote each field through `BinaryWriter`, which made the 64-byte row proof less direct.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added a signed `Quality Bias` slider, pushed it through `ApplyEditorTuning`, stored it in `WaterOpticsTuningDTO.MaxDistanceQualityFlagsProfile.y`, applied it as `saturate(GlobalQualityWeight + bias)`, and loaded it from CSV profiles. Replaced the telemetry dump with a 32-byte unmanaged header plus raw 64-byte `WaterOpticsTelemetryEntry` rows written oldest-to-newest through `ReadOnlySpan<byte>`. Extended the layout validator to check the 32-byte dump header.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>The quality bias only moves the existing continuous ALU compression curve; it does not create a new shader keyword, render variant, or binary quality branch.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Runtime upload cost unchanged: one 64-byte DTO copy. Fault dump cost is crash-only and now writes contiguous native rows instead of per-field serialization.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>DTO ABI remains 64 bytes for `WaterOpticsDTO`, tuning/profile/telemetry rows. Dump header is 32 bytes and not part of rollback/save authority.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import/build and actual dump file validation remain pending under the no-premature-build guard.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Cold Vault Rebind Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics DataVault Lifecycle">
  <WHAT_WAS_WRONG>`WaterOpticsRuntime` originally made one cold Vault acquisition in `OnEnable`. If `GlobalRegistry.DataVault` was published after this owner enabled, the dispatcher route could be registered but remain unable to resolve its Vault lanes.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added `TryColdBootstrapVault`, cold retry points in `Awake`, `OnEnable`, and `Start`, and `IGlobalRegistryHotSwapListener` handling for `GlobalRegistryServiceSlot.DataVault`. On replacement, old owned handles are released and the new Vault gets the same fixed generation handles. Dispatcher phases still read only cached `_vault`.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No visual cheat changed in this pass. The existing Dear Lie and scalar extinction curve remain untouched.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Hot path avoids registry lookup entirely. Added work is cold lifecycle/service-rebind only: six fixed Vault handles plus one optional editor cold CSV load attempt.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Runtime braces `126/126`, preprocessor `0/0`. Targeted scan remains clean for `Pack=1`, `Shader.SetGlobalVector`, `.Complete()`, `TryGetLatestCreated`, `UnityEngine.Random`, `string.Format`, `foreach`, `AddUnsafePass`, `IUnsafeRenderGraphBuilder`, `BinaryWriter`, sibling `World/Biome/Atmosphere` usings, and DTO property setters.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import/build was not run. Runtime proof still needs dispatcher registration, Vault replacement smoke, constant-buffer binding, GC/profiler, and GPU marker capture.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - CSV Data Monolith Hygiene Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics CSV Bridge">
  <WHAT_WAS_WRONG>A validation artifact flagged the water-optics runtime for player text profile loading from `StreamingAssets`, which conflicts with the Data Monolith/static-payload doctrine and creates a fragile runtime IO route.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Removed the `Application.streamingAssetsPath` CSV path from `WaterOpticsRuntime`. The `ReadOnlySpan<byte>` CSV parser remains, but file-backed `water_optics_profiles.csv` ingestion is now `UNITY_EDITOR`/development bridge from `Docs` only, with an explicit Abyssal Optics Tuner reload action; player runtime keeps defaults/mock profile data until a Data Monolith/Vault payload contract exists.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No shader cheat changed. The low-quality scalar extinction and Dear Lie waterline remain the active visual fakes.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Removes player runtime text-file existence checks/read path. Editor cold/reload bridge cost remains outside frame hot paths.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`Application.streamingAssetsPath` no longer appears in `WaterOpticsRuntime`. CSV parser still uses `ReadOnlySpan<byte>` and fixed Vault scratch buffer.</STATIC_VERIFICATION>
  <REMAINING_RISK>Production biome/profile payload still needs a Core/Data Monolith contract; Task 09 remains blocked.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Editor Managed Surface Polish Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Editor Tooling">
  <WHAT_WAS_WRONG>The renderer feature installer and generic fog scanner kept static `string[]` route tables, and build/scanner diagnostics used `+` path concatenation. This was editor-only, but avoidable managed surface still weakens the audit.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Replaced static route arrays with const path fields plus index selectors. `PostProcess_Fog_Scanner` still creates a transient `string[]` only when `AssetDatabase.FindAssets` is invoked by explicit editor scan because Unity requires that API shape. Build-guard and scanner diagnostics now use `string.Concat`.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No visual cheat changed. Existing scalar extinction and Dear Lie waterline remain unchanged.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>0 us/runtime. Editor domain reload avoids two static route arrays; explicit editor scan still performs unavoidable AssetDatabase/string IO work.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Targeted scan is clean for `private static readonly string[]`, `+ rendererAssetPath`, forbidden runtime tokens, and source-level water-optics `Application.streamingAssetsPath`.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import/build and renderer asset serialization proof remain pending.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Dispatcher Frame Provenance Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Telemetry Frame Source">
  <WHAT_WAS_WRONG>Owner-phase telemetry in `PreSimulationTick` and `VisualSyncTick` stamped rows with `Time.frameCount` even though dispatcher timing already carries `FrameId`. The RenderGraph marker also carried a Unity frame stamp despite only needing a marker-present bit.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Replaced owner-phase frame stamps with `DispatcherTimingDTO.FrameId`. Changed `NotifyRenderGraphTelemetrySubmitted` to a no-argument, saturating marker counter so the renderer feature does not read Unity frame state.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No visual cheat changed. The current cheat stack remains scalar Beer-Lambert ALU collapse plus screen-space waterline Dear Lie.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Sub-microsecond CPU hygiene: avoids repeated `Time.frameCount` property reads and improves forensic frame alignment. Shader/GPU cost unchanged.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`Time.frameCount` no longer appears in the WaterOptics source scope. WaterOptics owner telemetry now receives the dispatcher frame id directly.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import/build, Frame Debugger constant-buffer proof, and measured GPU marker timestamps remain pending under the no-premature-rebuild guard.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Direct DTO Memory Mutation Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Burst DTO Mutation">
  <WHAT_WAS_WRONG>`GenerateMockWaterOpticsJob` satisfied the raw-field DTO layout but wrote the final row through `Output[0] = dto`, leaving a weak point against the direct memory mutation mandate.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Converted `GenerateMockWaterOpticsJob` to `unsafe`, resolved the tuning row with `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr`, and wrote `WaterOpticsDTO` through `UnsafeUtility.AsRef<T>` on the output pointer.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No shader cheat changed. This preserves the existing mock deep-ocean optical fake and continuous ALU collapse curve.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Sub-microsecond on the current one-row mock lane, but removes indexer mutation overhead and hidden-copy suspicion before profile blending expands the data width.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`GenerateMockWaterOpticsJob` uses Burst fast/standard attributes and direct unsafe pointer routes. The mapped-buffer upload is direct `UnsafeUtility.MemCpy` for one cache-line row. DTO fields remain raw public fields.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity Burst compile/import proof remains pending under the no-premature-rebuild guard.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Shader Continuum Gate Removal Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Extinction Shader Scalability">
  <WHAT_WAS_WRONG>`Hecton_WaterExtinction.hlsl` still had a legacy `_MATH_LOD_LOW` / `SHADER_API_MOBILE` compile-time split around the extinction LUT. That is a binary platform decision inside the water optics surface.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Removed the platform macro split, declared `_ExtinctionLUT` unconditionally, and added `H8WaterExtinctionLutBlendWeight(active)` so LUT influence is admitted by a smooth `GlobalQualityWeight` polynomial. Low quality returns analytical extinction before sampling; high quality blends toward LUT output.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>The low-quality path is the analytical Beer-Lambert fake instead of a texture lookup. High quality buys richer extinction bias through the existing LUT without a shader keyword.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Low quality avoids the legacy LUT texture fetch by uniform quality branch. High quality cost is unchanged except for a small blend. No shader variant or material split added.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`Hecton_WaterExtinction.hlsl` scan is clean for `_MATH_LOD_LOW`, `SHADER_API_MOBILE`, and `H8_WATER_EXTINCTION_LUT_ENABLED`. Braces `32/32`, preprocessor `1/1` include guard only.</STATIC_VERIFICATION>
  <REMAINING_RISK>Shader import/runtime GPU proof remains pending under the no-premature-rebuild guard.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Dirty Tuning And Owner Row Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Hot Owner Rows">
  <WHAT_WAS_WRONG>`PreSimulationTick` wrote tuning every frame even if no authoring/profile state changed, and several one-row owner paths still used `NativeArray` indexer reads/writes.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added `_tuningDirty`, editor `OnValidate`, forced writes on bootstrap/profile/editor changes, and direct `UnsafeUtility.AsRef<T>` helpers for params, tuning, telemetry cursor, and telemetry rows.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No visual cheat changed. This preserves the current scalar Beer-Lambert ALU collapse and screen-space waterline Dear Lie.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Suppresses unchanged per-frame tuning writes and removes indexer mutation from the owner telemetry path. Current expected gain is sub-microsecond; value is mainly structural before profile blending widens.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Targeted scan returns clean for `parameters[0]`, `tuning[0]`, `cursorArray[0]`, `ring[cursor]`, `Output[0]`, and `Tuning[0]` inside `WaterOpticsRuntime.cs`. Runtime braces `133/133`, preprocessor `3/3`.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity C# import/Burst proof remains pending; no `dotnet build` or Unity rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - CSV Authoring Artifact Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Human Tuning Source">
  <WHAT_WAS_WRONG>The zero-GC CSV parser and tuner reload path existed, but `Docs/water_optics_profiles.csv` was absent, so Task 17 had no concrete authoring input artifact in the checkout.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added `Docs/water_optics_profiles.csv` with four bounded profiles: abyssal noir, red silt, glacial blue, and sulfur vent. Route card, binary ledger, status, and rationale now state that this file is editor/development input only; player production payload authority remains Data Monolith/Vault pending a core contract.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No visual cheat changed. The profiles feed the existing scalar Beer-Lambert ALU collapse, spectral correction curve, directional scatter, and screen-space waterline Dear Lie.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>0 us/player runtime. Editor cold bootstrap or explicit tuner reload reads bounded CSV bytes into Vault scratch; no frame hot-path IO is introduced.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`Docs/water_optics_profiles.csv` exists and uses the parser's header/order: profile, absorption RGB, extinction multiplier, scattering RGB, anisotropy, directional light RGB/intensity, max distance, quality bias.</STATIC_VERIFICATION>
  <REMAINING_RISK>Data Monolith/Vault production profile payload and live biome water-optics contract remain pending; no Unity import/build was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Artifact Root Resolver Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Artifact Paths">
  <WHAT_WAS_WRONG>CSV reload and telemetry dump assumed `Directory.GetCurrentDirectory()` was already the Unity project root. Shell/automation in this workspace can run from `C:\hades`, which would miss `Docs/water_optics_profiles.csv` and write dumps to the wrong tree.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added `ResolveProjectRoot()` with an `Assets` + `ProjectSettings` proof and a `Hecton8` child fallback. CSV reload and black-box dump now share that resolver.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No visual cheat changed. This preserves the existing scalar extinction collapse, spectral correction curve, directional scatter, and Dear Lie waterline.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>0 us/frame. Resolver runs only during editor CSV load/cold attempt or fault dump, not in shader upload or dispatcher hot math.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`WaterOpticsRuntime.cs` braces `135/135`, preprocessor `3/3`; scan clean for `Application.streamingAssetsPath`, `TryGetLatestCreated`, `.Complete()`, and `BinaryWriter`.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import/build and actual filesystem dump/reload replay remain pending; no rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Spectral Admission ALU Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Shader ALU Collapse">
  <WHAT_WAS_WRONG>The compressed extinction output visually leaned toward mono at low quality, but still computed spectral delta and vector reciprocal correction before lerping it out.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added a shared spectral admission curve in opaque HLSL, volumetric compute, and telemetry: `smooth01(saturate((quality - 0.28) * 1.3888889))`. Below the admission floor, the shaders return mono transmittance immediately after the scalar exponential-equivalent path.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>Low quality is now an explicit monochrome Beer-Lambert optical fake; middle/high/ultra gradually buy back spectral correction, LUT bias, and directional in-scatter.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Low-quality pixels skip spectral delta, vector reciprocal correction, and spectral blend after mono transmittance. Exact GPU microseconds remain pending profiler capture.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`Hecton_WaterExtinction.hlsl` braces `33/33`, `Hecton_VolumetricFog.compute` braces `47/47`, `WaterOpticsRuntime.cs` braces `136/136`; no shader keyword/platform split was added.</STATIC_VERIFICATION>
  <REMAINING_RISK>Shader import and measured GPU timing remain pending; no rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Legacy LUT Fallback Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Extinction LUT Compatibility">
  <WHAT_WAS_WRONG>The legacy LUT admission could be suppressed when `_GlobalWaterOptics` was inactive or unbound, because the new quality reader would see zero during editor/import preview.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>`H8WaterExtinctionLutBlendWeight` now uses `lerp(1.0, H8WaterOpticsQualityWeight(), H8WaterOpticsActive())`, preserving legacy LUT admission unless the runtime water-optics CBUFFER is actively driving quality.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No new simulation. This preserves the existing LUT optical fake for non-runtime preview and the continuous water-optics runtime curve in play.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>No direct saving; one scalar lerp prevents a preview/runtime fallback regression without adding variants or CPU state writes.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`_MATH_LOD_LOW`, `SHADER_API_MOBILE`, and `H8_WATER_EXTINCTION_LUT_ENABLED` remain absent from `Hecton_WaterExtinction.hlsl`.</STATIC_VERIFICATION>
  <REMAINING_RISK>Shader import proof remains pending; no rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Shader Water Gate And CSharp Audit Response Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Static Audit Response">
  <WHAT_WAS_WRONG>Static sub-agent audits found three real risks: the low-quality volumetric proxy and Dear Lie pass could tint dry pixels when `_GlobalWaterOptics` was active, the RenderGraph marker attachment was write-only despite no draw, public read accessors used mutable Vault resolves, and the VisualSync copy used a tiny `IJob.Run()` wrapper for one 64-byte row. The renderer installer also mutated renderer assets on domain reload.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added `WaterOpticsCameraUnderwaterGate` to the compute proxy, gated Dear Lie tint by waterline/camera-underwater visibility, changed the marker attachment to `AccessFlags.ReadWrite`, added `TryReadHandle`-based public read routes, replaced the one-row copy job wrapper with direct `UnsafeUtility.MemCpy`, and removed reload-time renderer feature auto-install.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>The Dear Lie remains a screen-space waterline fake, but now it cannot bleed into dry pixels; low quality remains a scalar proxy instead of a full raymarch.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Removes one per-frame job wrapper around a 64-byte copy. Shader cost adds one scalar gate to avoid false underwater tint; no texture fetch or pass added. Exact GPU/CPU timings remain pending Unity proof.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`WaterOpticsRuntime.cs` braces `134/134`, telemetry feature braces `15/15`, renderer installer braces `44/44`, compute braces `48/48`, DearLie braces `32/32`. Targeted scan is clean for `.Complete()`, `SetGlobalVector`, `UnityEngine.Random`, `string.Format`, `foreach`, `TryGetLatestCreated`, `Pack=`, `Time.frameCount`, `Application.streamingAssetsPath`, and `BinaryWriter` in WaterOptics scope. `git diff --check` reports only an existing CRLF normalization warning for `Hecton_VolumetricFog.compute`.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity C# import, shader import, Frame Debugger CBUFFER proof, profiler GC proof, and measured GPU marker timestamps remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Legacy Vertex Extinction ALU Closure Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="UberNoir Legacy Extinction Fallback">
  <WHAT_WAS_WRONG>Task 10 was not airtight: the new water-optics fragment and volumetric paths collapsed to mono at low quality, but UberNoir still generated `input.extinctionColor` in the vertex path with the old RGB analytical extinction. Low quality could therefore keep spectral color shift through the legacy fog tint lane.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>`H8WaterExtinctionAnalyticalRgbByDepthMeters` now computes mono extinction first and admits RGB spectral extinction only through the same `smooth01(saturate((quality - 0.28) * 1.3888889))` curve used by `_GlobalWaterOptics`. When water optics is inactive/unbound, the resolver falls back to quality 1.0 so legacy preview/import visuals keep their previous richness.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>Low quality is now consistently a monochrome Beer-Lambert fake across the new fragment path, volumetric proxy, and legacy vertex/fog tint lane; high quality buys back spectral RGB and LUT bias continuously.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Low-quality vertex/fog fallback avoids RGB spectral exponent work below the admission floor. This is smaller than fragment/compute savings but closes the last static ALU leak found in the scoped shader route.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`Hecton_WaterExtinction.hlsl` braces `34/34`; targeted scan remains clean for `_MATH_LOD_LOW`, `SHADER_API_MOBILE`, `shader_feature`, and `multi_compile` in the scoped water-extinction file.</STATIC_VERIFICATION>
  <REMAINING_RISK>Shader import and measured GPU timing remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Dispatcher Reachability And Vault Allocation Fence Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Dispatcher Phase Wiring">
  <WHAT_WAS_WRONG>The runtime owner reported `PRE_SIMULATION`, so `GenerateMockWaterOpticsJob` was not guaranteed reachable by a dispatcher that only calls `ScheduleSimulation` on simulation-phase systems. `PreSimulationTick` and `ScheduleSimulation` also used `EnsureVaultBuffers(clearExisting:false)`, leaving a grow-capable `GetGenerationHandle` repair path inside frame phases.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added a cold-allocated `SimulationMockSystem` child registered at `DispatcherPhase.Simulation` and delegated scheduling to the owner. The owner remains the `PRE_SIMULATION` tuning publisher. Dispatcher hot paths now fail closed on cached Vault readiness and direct handle resolves; `EnsureVaultBuffers` remains cold bootstrap/editor/hot-swap only.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No new physical simulation. The emergency optics DTO remains a cheap presentation fake: one scheduled row writes Beer-Lambert absorption/scattering parameters for the shader to sell underwater depth instead of CPU-side light transport.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Restores the intended one-row Burst job and removes surprise Vault generation-handle acquisition from frame phases. Exact CPU microseconds remain pending profiler proof; source risk is reduced to fixed handle resolves and one scheduled math row.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`WaterOpticsRuntime.cs` now contains `SimulationMockSystem.GetDispatcherPhase() => DispatcherPhase.Simulation`; scoped scan shows `EnsureVaultBuffers(vault, clearExisting: false)` only in editor CSV reload, while `GetGenerationHandle` calls remain inside `EnsureVaultBuffers`.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity C# import, dispatcher runtime proof, Burst scheduling proof, and profiler allocation proof remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - UberNoir Variant Continuum Closure Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="UberNoir Shader Variant Surface">
  <WHAT_WAS_WRONG>`_MATH_LOD_LOW` still existed in three UberNoir passes and selected a compile-time nearest-only light-probe path. `H8_UBERNOIR_SCREEN_REFRACTION` also created a local forward-pass shader feature around code that already had runtime material/quality gates.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Removed `_MATH_LOD_LOW` multi_compile lines, removed the stale UberNoir warmup `_MATH_LOD_LOW` variant, removed the light-probe preprocessor branch, removed the local screen-refraction shader feature, and made refraction rely on `_UberNoirRefractionParams`, cavitation state, `H8UberNoirHighCostAllowed()`, and `GlobalQualityWeight` for early return and continuous admission.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>Low quality keeps nearest probe sampling and no screen refraction through runtime gates; high quality buys back trilinear probe interpolation and screen-space Snell/cavitation refraction. No CPU light transport or extra draw was introduced.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Reduces shader variant surface by removing `_MATH_LOD_LOW` from ForwardLit/MotionVectors/ShadowCaster, one stale warmup variant, and one local screen-refraction feature. Runtime low-quality path still returns before trilinear probe sampling and opaque-texture refraction sampling.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Targeted scan is clean for `_MATH_LOD_LOW`, `H8_UBERNOIR_SCREEN_REFRACTION`, and `shader_feature_local` in `Hecton8_UberNoir.shader`, `Hecton8_UberNoir.hlsl`, `Hecton_CustomLightProbeGrid.hlsl`, and `Hecton8_UberNoir_RadiationWarmup.shadervariants`. Braces: UberNoir.shader `18/18`, UberNoir.hlsl `130/130`, LightProbeGrid `14/14`.</STATIC_VERIFICATION>
  <REMAINING_RISK>Shader import and GPU timing proof remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Shared Rendering Report Restoration Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Rendering Optimization Report">
  <WHAT_WAS_WRONG>The shared `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` no longer contained SHINOBU_265 evidence after neighboring agent report upserts, leaving Task 19 proof split across status/log files only.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added a scoped `shinobu_265_water_optics` JSON object preserving existing report entries. The object records runtime route, shader route, Vault BufferIDs, DTO layout sizes, fog scan result, binary variant patch state, telemetry route, rollback exclusion, and pending Unity proof.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No runtime visual change. This preserves evidence for the Beer-Lambert shader fake, screen-space Dear Lie waterline, and continuous quality gates.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>0 us/frame; documentation evidence restoration only.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` parses with `ConvertFrom-Json`, and `shinobu_265_water_optics` expands to the expected object.</STATIC_VERIFICATION>
  <REMAINING_RISK>Runtime scanner execution inside Unity remains pending; the report object is static-source evidence only.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Build Guard Boundary Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Compile Wall Discipline">
  <WHAT_WAS_WRONG>Compile verification is required eventually, but the generated solution does not yet include the new WaterOptics asmdefs. A `dotnet build` now would not prove the new assembly and would still traverse the large dirty workspace.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Checked for existing compiler processes, CPU load, slnx entries, and generated WaterOptics csproj files. No compiler processes were running and CPU was 29%, but `Hecton8.Rendering.WaterOptics*.csproj` and slnx entries were absent, so compile checks remain pending without launching build.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No visual change.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Avoided a broad solution build with no coverage of the new asmdef surface.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`Select-String Hecton8.slnx` returned no WaterOptics entry; `Test-Path Hecton8.Rendering.WaterOptics*.csproj` returned false/false.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import/project generation, C# compile, Burst compile, and shader import proof remain pending.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Fog Scanner Shared Report Upsert Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Metric Scanner Persistence">
  <WHAT_WAS_WRONG>`PostProcess_Fog_Scanner` still overwrote `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` with a single scanner object. A normal menu scan would delete neighboring report sections and the richer SHINOBU_265 runtime/shader/Vault proof.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Changed the scanner to upsert the `shinobu_265_water_optics` section into the shared JSON. The emitted section includes runtime route, shader route, Vault BufferIDs, DTO sizes, fog scan counts, continuous-quality proof, binary variant patch state, telemetry route, rollback exclusion, and pending Unity proof. The upsert preserves a consumed trailing comma when replacing a middle section and uses the project-root resolver before writing.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No runtime visual change. This preserves evidence for the Beer-Lambert shader fake, screen-space Dear Lie waterline, and continuous GlobalQualityWeight gates after scanner execution.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>0 us/frame. Editor scanner persistence only; no runtime state, draw call, or shader variant was added.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`git diff --check` on `PostProcess_Fog_Scanner.cs` is clean. Targeted scan confirms `File.WriteAllText(reportPath, json` is absent and `UpsertReportSection(reportPath, section.ToString())` is present. Current shared report still parses with `ConvertFrom-Json` and expands `shinobu_265_water_optics`.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity menu execution of the scanner remains pending; no `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - VisualSync Shader Buffer Allocation Fence Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics VisualSync Upload Lifecycle">
  <WHAT_WAS_WRONG>`VisualSyncTick` could call `EnsureShaderParamsBuffers()` and allocate the double-buffered `_GlobalWaterOptics` `GraphicsBuffer.Target.Constant` pair during the render upload phase if buffers were missing or invalid. That violated the zero-GC/hot allocation doctrine even though the common path was steady-state.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Moved constant-buffer acquisition into cold lifecycle calls (`Awake`, `OnEnable`, `Start`) via `TryColdBootstrapShaderParamsBuffers()`. The hot upload phase now checks `HasValidShaderParamsBuffers()` and records `TelemetryFlagUploadSkipped` if buffers are unavailable instead of repairing them in-frame.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No visual change. The same 64-byte shader payload continues to drive the Beer-Lambert/Dear Lie shader fake; lifecycle ownership was tightened.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Removes a worst-case two-`GraphicsBuffer` allocation from `VISUAL_SYNC`. Steady state remains one mapped 64-byte memcpy plus one constant-buffer bind; measured CPU/GPU cost remains pending Unity profiler proof.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`git diff --check` on `WaterOpticsRuntime.cs` is clean. Targeted scan shows `new GraphicsBuffer` only inside `TryColdBootstrapShaderParamsBuffers`, called from `Awake`, `OnEnable`, and `Start`; `VisualSyncTick` uses `HasValidShaderParamsBuffers` and no longer invokes buffer allocation.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity C# import, graphics-device-lost behavior, and profiler allocation proof remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Forbidden Token Source-Scan Hygiene Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Static Verification Hygiene">
  <WHAT_WAS_WRONG>The scoped forbidden-token scan still matched `PostProcess_Fog_Scanner.cs` and the current shared report because explanatory prose embedded the removed shader keyword names. The shader variants were gone, but automated scans still needed manual interpretation.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Reworded the scanner output and `shinobu_265_water_optics.continuousQuality` report text to avoid embedding removed keyword literals while preserving the same proof meaning: water optics uses runtime quality/material gates, not local binary variants.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No visual change. This protects static proof for the existing Beer-Lambert/Dear Lie shader fake.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>0 us/frame; static verification hygiene only.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Scoped forbidden-token scan now returns no matches across WaterOptics source plus the touched UberNoir/water/fog shader files. `git diff --check` remains clean for the scanner source.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import, shader import, and runtime profiler proof remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - VisualSync Bandwidth Discipline Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Constant Buffer Bandwidth">
  <WHAT_WAS_WRONG>`VisualSyncTick` still copied and rebound `_GlobalWaterOptics` every frame after the cold allocation fence. The payload is only 64 bytes, but project law forbids unchanged GPU uploads because driver/buffer traffic is not free on MX350/Quest-class hardware.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added `_lastUploadedDto` and `_hasUploadedDto` owner value state. `VisualSyncTick` now validates the DTO, compares all four `float4` lanes against the last uploaded row, records `TelemetryFlagUploadUnchanged`, and returns before `LockBufferForWrite`/`Shader.SetGlobalConstantBuffer` when the payload is unchanged. Invalid numeric DTOs now dump the black box and fail closed before poisoning the GPU constant buffer.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No new simulation. The same Beer-Lambert/Dear Lie shader fake remains active; the patch removes redundant upload work so high quality can spend budget on spectral scattering and refraction gates.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Unchanged frames avoid one mapped 64-byte write, one unlock, and one global constant-buffer bind. Exact driver microseconds remain pending Unity profiler proof.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`git diff --check` is clean for `WaterOpticsRuntime.cs`, `Status_SHINOBU_265.md`, and `Rationale_SHINOBU_265.md`. Targeted scan confirms no `.Complete()`, `.Run()`, `Shader.SetGlobalVector`, `Time.frameCount`, `UnityEngine.Random`, `Pack=1`, `TryGetLatestCreated`, or `BinaryWriter` in WaterOptics scope. `TelemetryFlagUploadUnchanged`, `WaterOpticsDtoEquals`, and `BuildVisualSyncTelemetryFlags` are present in the runtime.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import, C# compile, shader import, Frame Debugger constant-buffer proof, profiler GC proof, and measured GPU timing remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Shader CBUFFER ABI Validator Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Shader ABI Verification">
  <WHAT_WAS_WRONG>`WaterOpticsLayoutValidator` proved the C# DTO size/offsets and shader graft token presence, but did not prove that `_GlobalWaterOptics` shader CBUFFER declarations preserved the same four-lane ABI order as `WaterOpticsDTO`.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Added explicit editor/static order validation for the water extinction include, volumetric fog compute, and Dear Lie shader. The validator now requires `AbsorptionCoefficientsRGB`, `ScatteringCoefficientsRGB`, `DirectionalLightColorAndIntensity`, and `QualityAndDepthLimits` in that order inside the `_GlobalWaterOptics` CBUFFER.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No runtime simulation. This protects the existing Beer-Lambert shader fake and screen-space Dear Lie waterline from silent shader ABI drift.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>0 us/frame. Static/editor validation only; expected savings are avoided debugging and no accidental extra shader globals.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>`WaterOpticsLayoutValidator.cs` now contains `HasGlobalWaterOpticsCBufferLayout` and checks all direct shader consumers. `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` records the CBUFFER ABI proof field.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity editor menu execution, shader import, Frame Debugger constant-buffer proof, and measured GPU timing remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Profile Row Direct Memory Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics CSV Profile Lane">
  <WHAT_WAS_WRONG>Cold CSV profile ingestion and profile hash application still used `NativeArray` profile row indexers. The current route is editor/cold, but the same pattern would become a hidden-copy and CS1612 audit risk when Task 09 biome profile blending gets an approved contract route.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Changed profile ingestion to take the profile buffer base pointer once and write each `WaterOpticsProfileDTO` row through `UnsafeUtility.AsRef<T>` with a fixed 64-byte stride. Added `ReadProfileAt` so profile hash application reads rows through the same direct-memory helper instead of `profiles[i]`.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No new simulation. This preserves the Beer-Lambert shader fake while keeping the profile lane ready for future continuous biome-driven color blending.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>0 us/frame in the current route. Editor/cold profile reload avoids row-indexer ambiguity; future profile blending can reuse direct-memory row access instead of adding managed DTO copies.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Targeted scan for `profiles[`, `parameters[`, `tuning[`, `ring[`, `cursorArray[`, `Output[`, and `Tuning[` in `WaterOpticsRuntime.cs` returns no matches. The runtime now contains `ReadProfileAt` and pointer writes using `WaterOpticsNativeLayout.ProfileSizeBytes`; the shared rendering report records the same `profileLane` proof.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import, C# compile, shader import, and runtime profiler proof remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Meta Identity Documentation Reconciliation Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Architecture Proof Hygiene">
  <WHAT_WAS_WRONG>The SHINOBU route card and binary payload ledger still described the Dear Lie shader meta as an older retained asset. Filesystem state shows the Dear Lie shader and `.meta` are part of this deterministic WaterOptics asset identity set, so the proof text was stale.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>Updated both architecture docs to name deterministic `.meta` assets for the WaterOptics folders, asmdefs, C# sources, `Hecton_VolumetricFog_DearLie.shader`, and the UberNoir warmup variant collection. Kept Unity import proof explicitly pending.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>No runtime visual change. This protects the evidence trail for the screen-space Dear Lie waterline shader fake.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>0 us/frame. Documentation/proof hygiene only.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Focused scan for old retained Dear Lie meta wording now returns no matches in the SHINOBU route card or binary payload ledger.</STATIC_VERIFICATION>
  <REMAINING_RISK>Unity import, shader import, and renderer asset proof remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>

## 2026-05-21 - Runtime Owner Tiny Job Shader Bounds Pass

<POLISH_PASS agent_id="SHINOBU_265" scope="Water Optics Owner Bootstrap And Shader Safety">
  <WHAT_WAS_WRONG>Ampere static audit found hidden runtime owner installation through runtime-load/scene-load hooks and a scheduled Burst job that wrote one 64-byte optics DTO. Carver static audit found Dear Lie waterline tint/opacity could affect dry screens and custom light-probe grid reads trusted stale/non-finite probe globals.</WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>`WaterOpticsRuntime` no longer contains runtime-load self-spawn, scene-load callback ownership, `EnsureRuntimeInstance`, or a `new GameObject` installation path. The renderer-feature build guard now fails if no authored `WaterOpticsRuntime` owner is serialized in `_Project` scenes/prefabs; the current static GUID scan finds no owner placement, so scene/bootstrap authoring is blocked rather than hidden. The fallback/mock optics row is written directly in `PRE_SIMULATION` through `UnsafeUtility.AsRef<T>` over the Vault row; `ScheduleSimulation` is now a dependency pass-through. `Hecton_VolumetricFog_DearLie.shader` gates waterline tint/opacity by underwater state. `Hecton_CustomLightProbeGrid.hlsl` finite-checks probe scalars/origin/position, clamps resolution to 128, clamps active count, and fail-closes when published probe count cannot cover `resolution^3`.</WHAT_WAS_DONE>
  <CINEMATIC_CHEATS_USED>The waterline remains a screen-space Dear Lie, not a water-surface physics solve. Light-probe richness remains nearest-to-trilinear quality admission, not a binary variant.</CINEMATIC_CHEATS_USED>
  <MICROSECONDS_SAVED>Removes one per-frame scheduled job for a single cache-line DTO write and removes the hidden cold GameObject owner allocation path. Exact CPU/GPU microseconds remain pending Unity profiler proof.</MICROSECONDS_SAVED>
  <STATIC_VERIFICATION>Source scans must show no owner self-spawn or mock-job symbols in `WaterOpticsRuntime.cs`; build guard source must contain `VerifyRuntimeOwnerAuthored`; shader balance and JSON parse checks are required after this append.</STATIC_VERIFICATION>
  <REMAINING_RISK>Scene/bootstrap owner placement is currently blocked by owner review; no manual scene YAML mutation was performed. C# import, shader import, Frame Debugger constant-buffer proof, profiler GC proof, and measured GPU timing remain pending. No `dotnet build`, Unity import, or rebuild was launched.</REMAINING_RISK>
</POLISH_PASS>
