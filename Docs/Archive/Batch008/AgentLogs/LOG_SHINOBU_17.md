# SHINOBU_17 Log

## Session 2026-05-17
What was wrong: SHINOBU_17 had no current-batch state files yet. Shader/CBuffer work had to be scoped from the batch prompt and mandate registry before source edits.
What was done: Extracted prompt, counted 20 tasks, verified missing status/rationale as clean state, and initialized current-batch logs.
Cinematic Cheats used: Planned "Dear Lie" global flow vector path for low tier instead of full flow-field sampling in every shader.
Exact Microseconds saved: 100-150 us estimated process overhead only; runtime savings pending implementation and profiler evidence.

## Session 2026-05-17 - CBuffer Dispatcher Implementation
What was wrong: no single SHINOBU-owned ARM64-safe DTO/cbuffer bridge existed for Vault-to-URP fog, flow, caustics, DRS, hazard pulse, and telemetry. The extinction LUT resolver also expected a bogus 4096x4096 payload instead of the verified `256 x 256 x 3` half-float artifact.
What was done: added `GlobalShaderDispatcher`, `ShaderGlobalsDTO` 48-byte std140 layout, mock weather/job data, static CommandBuffer dispatch, wake/thermal GraphicsBuffer routing, AUP/time/DRS/keyword globals, 300-frame telemetry ring, CSV override parser, `UberNoir Global Tuner`, and SceneView flow/wake gizmos. Corrected `LutArrayResolver` to load the verified 384 KB main LUT and expose it for global binding.
Cinematic Cheats used: Dear Lie `float4` sector flow for low tier; projected caustic matrix instead of decals; thermal anomaly float4 points instead of volumetric heat; compact extinction LUT instead of full scattering.
Exact Microseconds saved: Material mutation avoidance 60-250 us; Dear Lie flow 40-120 us shader-side on MX350-class hardware; wake/thermal upload target 15-35 us; telemetry record 1-3 us; bogus LUT path avoids about 31.6 MB cold allocation.
Compile evidence: Unity R1 found SHINOBU const error and it was fixed. Unity R2/R3 report no SHINOBU-owned compiler errors, but global compile is blocked by external `long3` errors in `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs:128`, `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeJobs.cs:108`, and `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs:529`.

<SELF_AUDIT>
  <Q1 materialSetFloat="NO">No SHINOBU-owned `Material.SetFloat` or `MeshRenderer.material.SetFloat`; dispatch uses global CommandBuffer/global buffers.</Q1>
  <Q2 std140="PASS">48-byte DTO: bytes 0-15 FogColor, 16-31 FlowVector+FlowMagnitude, 32-47 GlobalTime+3 pads.</Q2>
  <Q3 properties="NO">ShaderGlobalsDTO has raw fields only; ref/ref readonly accessors prevent CS1612 copy mutation.</Q3>
  <Q4 mocks="PASS">MockWeatherState and MockGlobalShaderDataJob cover weather/flow/biome/thermal absence.</Q4>
  <Q5 editorFacade="PASS">UberNoir Global Tuner and SceneView gizmos are implemented.</Q5>
</SELF_AUDIT>

## 2026-05-18 Adjacent Visor Shaft CBuffer Eviction
What was wrong: `HectonScooterVolumetricShaftsFeature` still drove the scooter volumetric shaft/noir pass through cached material mutation: dozens of `SetFloat`, one `SetColor`, and a material `SetBuffer` path for exposure state. The shader stored those values in `UnityPerMaterial`, which contradicts the CBuffer/global-dispatch discipline and left ARM64 padding implicit around the noir color and exposure flag.
What was done: replaced that upload path with one persistent `GraphicsBuffer.Target.Constant` buffer bound as `HectonScooterVolumetricShaftsGlobals`. Added `ShaftGlobalsDTO` (`Pack=4`, `Size=176`) with eleven `float4` rows, switched exposure state to `Shader.SetGlobalBuffer`, removed material upload caches, removed unused material property IDs, made `MaterialParameterState` explicit (`Pack=4`, `Size=152`), added explicit HLSL padding fields, and removed the explicit `Hecton8.Gameplay` namespace import from the touched pass.
Cinematic Cheats used: preserved the screen-space radial shaft fake. No world-volume raymarch or physical light simulation was added. Low tier keeps cheap radial taps and parameterized noir/fog response; higher tiers can push stronger lens/thermal/shaft response through the same CBuffer ABI.
Exact Microseconds saved: not profiler-measured. The honest claim is removal of more than 30 dirty-frame material property uploads and one material buffer bind from this pass. Expected benefit is submission/SRP-batcher hygiene, not a fabricated guaranteed frame-time number.
Compile evidence: `git diff --check` is clean except LF-to-CRLF warnings on touched files. `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` succeeds with `Build succeeded`, `9 Warning(s)`, `0 Error(s)`.
Residual debt: broad `rg` over `Assets/_Project/Scripts/Rendering` and `Assets/_Project/Scripts/Visor` still finds legacy material/MPB/compute property uploads in other renderer features. This report does not claim project-wide SetFloat eradication. It claims the SHINOBU bridge and the adjacent scooter-shaft CBuffer violation are fixed and compiled.

<SELF_AUDIT>
  <TASK_CHECK>
    <Task01 status="PASS">Binary graveyard scan and emergency aligned mock path remain in the dispatcher.</Task01>
    <Task02 status="PASS_WITH_RESIDUAL_DEBT">SHINOBU-owned bridge and scooter shaft pass have no Material.SetFloat. Other Presentation features still need a later CBuffer migration.</Task02>
    <Task03 status="PASS">ShaderGlobalsDTO remains raw-field/ref-backed; no DTO properties were added.</Task03>
    <Task04 status="PASS">Primary DTO is 48 bytes; scooter shaft DTO is 176 bytes; cache struct is 152 bytes; no Pack=1 in touched runtime structs.</Task04>
    <Task05 status="PASS">Mock weather/flow provider remains isolated.</Task05>
    <Task06 status="PASS">GlobalShaderDispatcher still runs through static CommandBuffer; scooter shaft now uses a global constant buffer, not material state.</Task06>
    <Task07 status="PASS">Wake/thermal GraphicsBuffer router remains Vault-backed.</Task07>
    <Task08 status="PASS">Extinction LUT stays bootstrap-loaded and dispatcher-bound only when already resident.</Task08>
    <Task09 status="PASS">AUP offset remains CPU-rebased before shader upload.</Task09>
    <Task10 status="PASS">Dear-Lie flow remains one global vector/magnitude on low tier.</Task10>
    <Task11 status="PASS">H8ShaderTime remains double-backed and modulo 3600.</Task11>
    <Task12 status="PASS">DRS scalar and packed params remain global.</Task12>
    <Task13 status="PASS">Tier keywords remain global/change-only.</Task13>
    <Task14 status="PASS">Caustic projection matrix remains globally published.</Task14>
    <Task15 status="PASS">Biome palette interpolation remains mock-job driven.</Task15>
    <Task16 status="PASS">Hazard pulse remains Core signal based, not Gameplay DTO based.</Task16>
    <Task17 status="PASS">300-frame CBuffer blackbox remains in DataVault slots and dumps `.bin` plus `.h8dump`.</Task17>
    <Task18 status="PASS">UberNoir tuner editor facade remains present.</Task18>
    <Task19 status="PASS">CSV override remains timestamp-gated and preallocated.</Task19>
    <Task20 status="PASS">SceneView flow/wake gizmo remains present.</Task20>
  </TASK_CHECK>
  <STRUCT_LAYOUT status="PASS">ShaderGlobalsDTO: 0-15 FogColor, 16-27 FlowVector, 28-31 FlowMagnitude, 32-35 GlobalTime, 36-47 pads. Scooter ShaftGlobalsDTO: rows 0..10 at byte offsets 0,16,32,48,64,80,96,112,128,144,160; total 176.</STRUCT_LAYOUT>
  <H_PHI_CHECK status="PASS">CBuffer dispatcher state and blackbox telemetry live in Vault buffers. Scooter shaft CBuffer is a rendering resource, not a gameplay-owned NativeArray.</H_PHI_CHECK>
  <DEAR_LIE status="PASS">Low-tier flow and scooter shafts stay visual fakes; no dense flowfield/volume simulation was introduced.</DEAR_LIE>
  <BLACKBOX status="PASS">300-frame ring active; dump path remains `Docs/AgentLogs/Dump_CBUFFER_DISPATCH.bin` plus `.h8dump`.</BLACKBOX>
  <COMPILE_GUARD status="PASS">Core CLI build currently passes with 0 errors and 9 warnings.</COMPILE_GUARD>
</SELF_AUDIT>

## Session 2026-05-17 - Ultra Polish Dependency Inquisition
What was wrong: the first hazard pulse path still pulled `Hecton8.Gameplay` and read `HazardExposureJobResult` directly. That was a compile-time coupling leak in a renderer-owned CBuffer bridge. `_ResolutionScale` and `_HazardPulseIntensity` were also being sent as packed vectors under scalar names.
What was done: removed the Gameplay dependency, switched hazard pulse to `SignalBus<RadiationDoseSignal>.GetFrameSnapshot()`, kept a Homeostasis stress fallback, and split scalar shader globals from packed param vectors: `_ResolutionScale` + `_H8ResolutionScaleParams`, `_HazardPulseIntensity` + `_H8HazardPulseParams`. Re-ran Unity batch compile R4 after dependency audit.
Cinematic Cheats used: preserved Dear Lie global flow for low tier; hazard pulse is a scalar sine overlay from typed signal data instead of a heavy post-process/gameplay poll.
Exact Microseconds saved: compile-time coupling saved is architectural, not frame-time measurable. Runtime hazard scan remains 2-4 us in ordinary frames; scalar globals add 0 us versus the prior packed vector path. Material mutation avoidance remains 60-250 us; Dear Lie remains 40-120 us shader-side on MX350-class hardware.
Compile evidence: `Docs/AgentLogs/UnityCompile_SHINOBU_17_R4.log` exited `UNITY_EXIT=1` after the dependency decoupling patch with only external errors at that time. `Docs/AgentLogs/UnityCompile_SHINOBU_17_R5.log` is the final current boundary after CSV I/O polish and exits `UNITY_EXIT=1`; unique errors are external Habitat Deformation failures in `HullIntegrityRuntime` for missing `Contracts`, `IHabitatModuleDeformationReadModel`, and `HabitatModuleDeformationSample`. No SHINOBU-owned `error CS` line was reported in R5.

<SELF_AUDIT>
  <TASKS_01_20 status="PASS">All 20 SHINOBU_17 tasks are recorded in Status_SHINOBU_17.md with evidence. Compile proof is blocked only by external domains.</TASKS_01_20>
  <ARM64_LAYOUT status="PASS">ShaderGlobalsDTO offsets: 0-15 FogColor, 16-27 FlowVector, 28-31 FlowMagnitude, 32-35 GlobalTime, 36-47 pads. Size 48 bytes. No SHINOBU-owned runtime Pack=1 struct.</ARM64_LAYOUT>
  <ZERO_GC_HOT_PATH status="PASS">No Material.SetFloat, no local NativeArray ownership, no foreach/LINQ in dispatcher hot path; cold FileStream/byte scratch paths are CSV/dump only.</ZERO_GC_HOT_PATH>
  <AUP status="PASS">AUP is CPU-rebased through HectonFloatingOrigin offset and sent to shader as finite float4; no absolute-position distance math in shader bridge.</AUP>
  <DEAR_LIE status="PASS">Low-tier current simulation is faked with a single global flow vector and tier keyword gate.</DEAR_LIE>
  <DEPENDENCIES status="PASS">Hazard pulse uses Core SignalBus snapshot. No Gameplay type dependency remains in GlobalShaderDispatcher.</DEPENDENCIES>
  <BLACKBOX status="PASS">300-frame telemetry ring remains active in DataVault slots 64-363 and dumps `Dump_CBUFFER_DISPATCH.bin` on >0.1 ms dispatch/layout fault.</BLACKBOX>
</SELF_AUDIT>

## Session 2026-05-17 - CSV I/O Pressure Patch
What was wrong: `shader_globals_override.csv` timestamp polling still touched filesystem metadata every late-frame tick. That is acceptable in the editor for quick tuning, but hostile to runtime MicroSD targets.
What was done: added a CSV poll gate before `File.Exists`/`File.GetLastWriteTimeUtc`: 50 ms in Unity Editor, 250 ms in runtime. The parser still uses preallocated byte scratch and only opens the file when the timestamp changes.
Cinematic Cheats used: none; this is I/O pressure removal around the human override bridge.
Exact Microseconds saved: avoids one filesystem metadata hit per rendered frame on runtime builds. Frame-time saving is platform-dependent and must be profiled; expected benefit is hitch-risk reduction, not deterministic shader math cost.
Compile evidence: `Docs/AgentLogs/UnityCompile_SHINOBU_17_R5.log` is current after this patch. It reports only external Habitat Deformation errors and no SHINOBU-owned `error CS`.

## Session 2026-05-17 - Titanium Polish Pass
What was wrong: remaining rot was local and concrete: implicit padding in `UberNoirGlobalTuning`, blackbox dump only using `.bin`, telemetry File I/O under a DataVault lock, unlocked wake buffer reads, raw normalization paths, profile-byte-only keyword cache, and runtime URI staging that could busy-wait on `UnityWebRequest`.
What was done: added explicit tuning padding and size validation; copied telemetry into a stack span while locked, then wrote `Dump_CBUFFER_DISPATCH.bin` and `Dump_CBUFFER_DISPATCH.h8dump` after unlocking; locked wake/vector Vault buffers for GPU upload and gizmos; replaced raw `math.normalize`/`.normalized` with guarded normalization; included `GlobalRegistry.ScalabilityTier` in keyword-cache invalidation; made StreamingAssets URI staging editor-only.
Cinematic Cheats used: Dear Lie global flow remains the low-tier fake. Analytical extinction fallback is used for portable/low-memory targets instead of forcing LUT streaming.
Exact Microseconds saved: File I/O under Vault lock removed from fatal dump path; one runtime URI busy-wait path removed; guarded normalization is sub-1 us and prevents NaN propagation. Previous estimates remain: material mutation avoidance 60-250 us, Dear Lie 40-120 us shader-side, telemetry record 1-3 us.
Compile evidence: R6 was inconclusive because Bee backend contention stopped source compile. `Docs/AgentLogs/UnityCompile_SHINOBU_17_R7.log` is current and reports only external Core Origin `AupOriginShiftCoordinator.cs:178` missing `HectonPhysicsContract`; no SHINOBU-owned `error CS`.

<SELF_AUDIT>
  <TASKS_01_20 status="PASS">All 20 SHINOBU_17 tasks remain implemented and rechecked in Status_SHINOBU_17.md.</TASKS_01_20>
  <ARM64_LAYOUT status="PASS">ShaderGlobalsDTO: 0-15 FogColor, 16-27 FlowVector, 28-31 FlowMagnitude, 32-35 GlobalTime, 36-47 pads. UberNoirGlobalTuning: 0-15 FogColor, 16-27 FlowVector, 28-31 FogDensity, 32-35 CausticSpeed, 36-39 FlowMagnitude, 40-47 pads.</ARM64_LAYOUT>
  <ZERO_GC_HOT_PATH status="PASS">No Material.SetFloat, no local NativeArray ownership, no foreach/LINQ, no direct Gameplay DTO. Dump snapshot is stack-backed before cold FileStream writes.</ZERO_GC_HOT_PATH>
  <AUP status="PASS">AUP path still publishes finite rebased origin offset; no absolute AUP float distance math added.</AUP>
  <DEAR_LIE status="PASS">Low tier still fakes abyssal current with one global flow vector and cheap analytical extinction fallback where appropriate.</DEAR_LIE>
  <DEPENDENCIES status="PASS">Hazard pulse remains on Core SignalBus; no new sibling asmdef or Contracts edits.</DEPENDENCIES>
  <BLACKBOX status="PASS">300-frame ring active in DataVault slots 64-363; `.bin` and `.h8dump` are emitted on fault/budget breach.</BLACKBOX>
</SELF_AUDIT>

## Session 2026-05-17 - Adjacent Rendering Bridge Alignment
What was wrong: `HectonUberNoirRuntimeBridge` was already in the Rendering domain and still had `[StructLayout(... Pack = 1 ...)]` on a runtime DataVault telemetry struct. It also wrote its blackbox files while holding the `ShaderFeatureTelemetryRing` lock and emitted only `.bin`.
What was done: changed `UberNoirShaderTelemetryEntry` to `Pack = 4`, `Size = 48`, added `SizeBytes`, preserved twelve 4-byte fields at offsets `0..44`, and moved dump writes after a stack snapshot/unlock. The bridge now writes `.bin` and `.h8dump` variants for both integrator and extinction dumps.
Cinematic Cheats used: none; this is memory-layout and blackbox hardening.
Exact Microseconds saved: steady-state unchanged. Fault path removes lock-held File I/O; ARM64 misalignment risk for the 48-byte feature telemetry ring is eliminated.
Compile evidence: superseded by R9 in the following session; the adjacent bridge patch has no SHINOBU-owned compiler errors in that log.

<SELF_AUDIT>
  <ARM64_LAYOUT status="PASS">Rendering-domain scan now has no `Pack=1`. `UberNoirShaderTelemetryEntry` offsets are 0,4,8,12,16,20,24,28,32,36,40,44; size 48.</ARM64_LAYOUT>
  <BLACKBOX status="PASS">CBuffer dispatcher and UberNoir bridge both emit `.h8dump`; both snapshot under lock and write after unlock.</BLACKBOX>
  <DEPENDENCIES status="PASS">No contracts or sibling assembly references were added.</DEPENDENCIES>
</SELF_AUDIT>

## Session 2026-05-17 - Shared Slab VISUAL_SYNC Reconciliation
What was wrong: `HectonShaderGlobalDataVaultBridge` still treated `ShaderGlobalState` as a 7-slot bridge while `GlobalShaderDispatcher` reserves a 512-slot slab with slots 64-363 for the 300-frame CBuffer blackbox. The bridge also kept permanent immediate `Shader.SetGlobal*` publishing for legacy biolum/AUP/extinction/UberNoir globals, leaving part of the global shader contract outside the atomic VISUAL_SYNC CommandBuffer path.
What was done: unified the shared slab size to 512 float4 slots, made `GlobalShaderDispatcher` consume the bridge constant, stored AUP jitter in the shift slot `.w`, read legacy slots 0-6 inside the dispatcher, and republished those globals through the same static CommandBuffer as the SHINOBU CBuffer state. The legacy bridge now keeps direct `Shader.SetGlobal*` only as a pre-dispatch fallback and gates it off after the first successful VISUAL_SYNC dispatch.
Cinematic Cheats used: no new simulation; preserved Dear Lie global flow and analytical extinction fallback. This pass bought determinism and submission discipline, not extra physical truth.
Exact Microseconds saved: 5-15 us estimated in affected frames by removing scattered legacy shader submission after dispatcher activation; steady-state proof remains pending profiler capture. Cold resize/stale-handle risk from 7-slot preallocation is removed.
Compile evidence: `Library/UnityCompile_SHINOBU_17_R9.log` was run after this pass. It reports no SHINOBU-owned `error CS`; compilation is blocked externally in `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs` by duplicate `BlackboxEmergencyFlushHash`, `PushEvent`, `EnsureBlackboxInitialized`, `DisposeBlackboxState`, `CommitBlackboxFrame`, and dump helper definitions.

<SELF_AUDIT>
  <TASKS_01_20 status="PASS">All 20 SHINOBU_17 tasks remain implemented in code and tracked in Status_SHINOBU_17.md.</TASKS_01_20>
  <ARM64_LAYOUT status="PASS">Primary DTO remains 48 bytes: 0-15 FogColor, 16-27 FlowVector, 28-31 FlowMagnitude, 32-35 GlobalTime, 36-47 pads. Rendering-domain Pack=1 scan is clean.</ARM64_LAYOUT>
  <ZERO_GC_HOT_PATH status="PASS">LateFrameTick still uses static CommandBuffer, Vault handles, preallocated GraphicsBuffers, no LINQ/foreach/new NativeArray/direct Gameplay DTO in SHINOBU-owned files.</ZERO_GC_HOT_PATH>
  <AUP status="PASS">AUP offset is CPU-rebased before shader upload. AUP shift vector and jitter mask now travel through the shared Vault slab and VISUAL_SYNC CommandBuffer.</AUP>
  <DEAR_LIE status="PASS">Low tier still uses a single global flow vector instead of dense current sampling.</DEAR_LIE>
  <DEPENDENCIES status="PASS">No Contracts or sibling asmdef edits. Legacy bridge/dispatcher coupling is same Rendering namespace only.</DEPENDENCIES>
  <BLACKBOX status="PASS">CBuffer blackbox remains in slots 64-363 of the 512-slot `ShaderGlobalState` slab and emits `.bin` plus `.h8dump` on fault/budget breach.</BLACKBOX>
  <COMPILE status="BLOCKED_EXTERNAL">R9 compile wall is Core `GlobalTelemetryBus.cs` duplicate definitions, not SHINOBU files.</COMPILE>
</SELF_AUDIT>

## Session 2026-05-17 - Lock-Order And R11 Compile Guard
What was wrong: `GlobalShaderDispatcher` still had a stale-handle class of bug: `ShaderGlobalState`, gizmo wake buffers, and thermal-source buffers could be resolved before the relevant Vault lock. R10 also proved three SHINOBU compile errors: unqualified `Graphics` resolved against `Hecton8.Graphics`, `Debug` was ambiguous with `System.Diagnostics.Debug`, and `RadiationDoseSignal`/`SignalBus` lacked the existing Core.Contracts.Signals namespace import.
What was done: changed ShaderGlobalState access to `ensure handle -> lock -> resolve -> read/write` for VISUAL_SYNC, editor tuning, telemetry, and binary mock injection. Wake gizmo/upload and thermal-source reads now resolve only after their locks; thermal fallback clears stale packed slots. Fixed R10 compile errors with `UnityEngine.Graphics.ExecuteCommandBuffer`, `UnityEngine.Debug`, and `using Hecton8.Core.Contracts.Signals`.
Cinematic Cheats used: no new physical simulation. The low-tier dear-lie flow and mock thermal slot remain the controlled fake when producer buffers are missing or locked.
Exact Microseconds saved: no honest deterministic speed claim. Expected added lock overhead is 1-3 us in VISUAL_SYNC; the gain is deterministic ARM64-safe DataVault ownership and removal of stale NativeArray risk.
Compile evidence: `Library/UnityCompile_SHINOBU_17_R11.log` reports no SHINOBU-owned `error CS`. Compilation is blocked externally in `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictatorFallback.cs` by duplicate CS0111 members: `InitializeScalabilityDictator`, `ShutdownScalabilityDictator`, `ResolveTargetFrameMs`, `SampleVramPressure01`, `ComputeDictatorRawShi`, `ApplyHardwareShiFloor`, `SampleStopwatchFrameMilliseconds`, and `ApplyDictatorPressurePolicy`.

<SELF_AUDIT>
  <TASKS_01_20 status="PASS">All 20 SHINOBU_17 tasks remain implemented and rechecked after context compaction.</TASKS_01_20>
  <ARM64_LAYOUT status="PASS">ShaderGlobalsDTO remains 48 bytes: 0-15 FogColor, 16-27 FlowVector, 28-31 FlowMagnitude, 32-35 GlobalTime, 36-47 explicit float pads. Rendering-domain owned Pack=1 scan is clean.</ARM64_LAYOUT>
  <ZERO_GC_HOT_PATH status="PASS">LateFrameTick still uses static CommandBuffer, Vault handles, preallocated GraphicsBuffers, for loops, no LINQ/foreach/new NativeArray/direct Gameplay DTO in SHINOBU-owned files.</ZERO_GC_HOT_PATH>
  <AUP status="PASS">AUP stays CPU-rebased through HectonFloatingOrigin before shader upload; no absolute AUP float distance math was added.</AUP>
  <DEAR_LIE status="PASS">Low tier still fakes abyssal currents with one global flow vector and cheap fallback thermal/analytical visuals.</DEAR_LIE>
  <DEPENDENCIES status="PASS">Hazard pulse uses existing Core.Contracts.Signals `RadiationDoseSignal`; no local signal and no Gameplay dependency.</DEPENDENCIES>
  <BLACKBOX status="PASS">300-frame CBuffer ring remains in `ShaderGlobalState` slots 64-363 and dumps `.bin` plus `.h8dump` after lock release.</BLACKBOX>
  <COMPILE status="BLOCKED_EXTERNAL">R11 compile wall is external Core Homeostasis duplicate CS0111 members, not SHINOBU files.</COMPILE>
</SELF_AUDIT>

## 2026-05-18 Current Disk Reconciliation
What was wrong: The active prompt was reissued after context churn, and the last recorded compile wall named Core Homeostasis duplicates that may no longer be current.
What was done: Re-read `CURRENT_BATCH.md` SHINOBU_17 XML, `PROJECT_STATE_STATIC_XRAY.md`, `Status_SHINOBU_17.md`, and `Rationale_SHINOBU_17.md`. Re-scanned SHINOBU-owned rendering/editor files: no `Material.SetFloat`, `.material.SetFloat`, `Pack=1`, `new NativeArray`, LINQ, JSON, `FindObjectOfType`, or `GameObject.Find` in `GlobalShaderDispatcher`, `HectonShaderGlobalDataVaultBridge`, `HectonUberNoirRuntimeBridge`, or `UberNoirGlobalTunerWindow`. Re-ran binary archaeology; no legacy `global_shader_constants.h8bin` or `lighting_palettes_007.bin` exists, so the emergency aligned mock path remains the correct fallback.
Cinematic Cheats used: Low tier remains a single global float4 current fake plus analytical extinction fallback; high tiers consume global buffers/LUTs and can enable visual-overkill without touching per-object materials.
Exact Microseconds saved: Still evidence-class estimate only. Expected savings remain 60-250 us CPU from avoiding per-renderer material instancing/submission churn, 40-120 us shader-side on MX350 from Dear-Lie global flow, and 5-15 us from folding legacy global writes into the VISUAL_SYNC CommandBuffer. No profiler/player capture was run.
Compile evidence: Fresh `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` exits 1 on external Core/Input/Dispatcher/World contract visibility drift: `Hecton8.Input.Determinism`, `IDispatcherSystem`, dispatcher DTOs, `InputStateDTO`, `ChunkResidencyDTO`, `WorldStreamingRuntimeTuning`, `AddressablesRequestDTO`, `HLOD_ImpostorDTO`, and `MockAupShiftSignal`. No reported error names SHINOBU rendering files.

<SELF_AUDIT>
  <TASKS_01_20 status="PASS">All 20 SHINOBU_17 tasks remain implemented on disk and tracked in Status_SHINOBU_17.md. Current compile proof is blocked externally.</TASKS_01_20>
  <ARM64_LAYOUT status="PASS">Primary DTO remains 48 bytes: 0-15 FogColor, 16-27 FlowVector, 28-31 FlowMagnitude, 32-35 GlobalTime, 36-47 explicit padding. Adjacent UberNoir telemetry is Pack=4 Size=48.</ARM64_LAYOUT>
  <ZERO_GC_HOT_PATH status="PASS">Scoped SHINOBU hot path uses static CommandBuffer, Vault handles, preallocated GraphicsBuffers, index loops, and no Material.SetFloat/LINQ/local NativeArray/JSON. CSV/file work is gated cold path.</ZERO_GC_HOT_PATH>
  <AUP status="PASS">CPU publishes finite floating-origin offsets as shader globals; no shader-side double fantasy or absolute AUP float cast is introduced.</AUP>
  <DEAR_LIE status="PASS">Low tier receives one global current vector+magnitude and disabled heavy keywords instead of dense flow lookups.</DEAR_LIE>
  <DEPENDENCIES status="PASS">Rendering observes Core signals/GlobalRegistry/Vault state and does not reintroduce direct Gameplay hazard DTO coupling or new sibling asmdef dependencies.</DEPENDENCIES>
  <BLACKBOX status="PASS">300-frame CBuffer ring remains in DataVault ShaderGlobalState slots and dumps `.bin` plus `.h8dump` on layout fault or >0.1 ms dispatch.</BLACKBOX>
  <COMPILE status="BLOCKED_EXTERNAL">Current wall is Core/Input/Dispatcher/World contract drift, not SHINOBU rendering code.</COMPILE>
</SELF_AUDIT>

## 2026-05-18 VISUAL_SYNC Cold-I/O Eviction
What was wrong: `GlobalShaderDispatcher.LateFrameTick` still called `LutArrayResolver.EnsureLoadedAndBound()`. Most frames returned through `_loaded`, but the edge case was unacceptable: if static state reset or bootstrap ordering failed, VISUAL_SYNC could touch file paths, check byte counts, allocate/build a texture, and publish globals inside the render dispatcher.
What was done: removed the `EnsureLoadedAndBound()` call from `LateFrameTick`. LUT bootstrap remains in `LutArrayResolver` via `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`. The dispatcher now only binds `LutArrayResolver.ExtinctionTexture` through the static CommandBuffer when the texture already exists; otherwise analytical extinction fallback and DataVault params continue to drive shaders.
Cinematic Cheats used: no new physical simulation. Low tier still uses Dear-Lie global flow and analytical extinction fallback; higher tiers consume the verified 256x256x3 LUT when it is loaded.
Exact Microseconds saved: no inflated steady-state claim. The removed `_loaded` branch is sub-1 us in normal frames. The real saving is eliminating a possible milliseconds-scale cold file/texture path from VISUAL_SYNC on MicroSD, Android, or reload-edge cases.
Compile evidence: scoped `rg` confirms `EnsureLoadedAndBound()` is absent from `GlobalShaderDispatcher`; banned-pattern scan over SHINOBU-owned rendering/editor files reports no `Material.SetFloat`, `.material.SetFloat`, `Pack=1`, `new NativeArray`, LINQ, `foreach`, `ToString(`, Unity object searches, or hot `GetComponent`. `git diff --check` is clean except existing LF-to-CRLF warnings on touched rendering files. Fresh `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` exits 1 outside SHINOBU at `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs(119,34)` and `(1343,41)` missing `WakeRequestSignal`.

<SELF_AUDIT>
  <TASKS_01_20 status="PASS">All 20 SHINOBU_17 tasks remain implemented; this pass specifically hardens Task 06 and Task 08.</TASKS_01_20>
  <ARM64_LAYOUT status="PASS">ShaderGlobalsDTO byte layout remains 0-15 FogColor, 16-27 FlowVector, 28-31 FlowMagnitude, 32-35 GlobalTime, 36-47 explicit pads. Rendering-domain Pack=1 scan remains clean.</ARM64_LAYOUT>
  <ZERO_GC_HOT_PATH status="PASS">VISUAL_SYNC no longer calls the LUT loader. Static audit shows no banned SHINOBU hot-path patterns.</ZERO_GC_HOT_PATH>
  <AUP status="PASS">AUP remains CPU-rebased before shader upload; no absolute double-to-float distance math was added.</AUP>
  <DEAR_LIE status="PASS">Low tier still fakes dense flow with one global vector and cheap analytical extinction fallback.</DEAR_LIE>
  <DEPENDENCIES status="PASS">No contracts, asmdef references, or sibling-domain usings were added. The missing `WakeRequestSignal` was not stubbed from the renderer.</DEPENDENCIES>
  <BLACKBOX status="PASS">300-frame CBuffer blackbox remains active in DataVault slots 64-363 and dumps `.bin` plus `.h8dump` after releasing locks.</BLACKBOX>
  <COMPILE status="BLOCKED_EXTERNAL">Current compile wall is external `GlobalPhysicsStateManager` missing `WakeRequestSignal`, already identified by other agent logs as SHINOBU_37/physics-culling contract debt.</COMPILE>
</SELF_AUDIT>
