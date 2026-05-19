# LOG_SHINOBU_65 - Toxic Outgassing Chemistry

## 2026-05-18 - Macro-grid toxic diffusion implementation

What was wrong:
- Poison gas was represented elsewhere in the project by trigger/physics-style hazard patterns. That is binary, cannot follow currents, cannot respect SDF cave walls, and burns Unity physics/callback overhead.
- The requested legacy `gas_toxicity_tables.h8bin` was not present under Batch005-007 archives. Current Dalton gas toxicity binaries exist, but the binary ledger marks them script-tool-only rather than runtime-wired.
- The implementation risk was per-frame array allocation and sibling-domain coupling into physiology/submarine/flora/shader code.

What was done:
- Added `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs`.
- Added `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs`.
- Added `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingTunerWindow.cs`.
- Created and updated `Docs/Tasks/Status_SHINOBU_65.md`.
- Created and updated `Docs/AgentLogs/Rationale_SHINOBU_65.md`.
- Implemented Vault-owned ping-pong density buffers, flow/world sample buffers, source/entity buffers, signal staging buffers, constants, CSV byte buffer, binary probe buffer, NaN flags, and 300-frame telemetry ring.
- Implemented Burst jobs for rebase, mock flow, mock SDF/flora sampler, diffusion/advection, entity exposure/corrosion, biolum signal harvest, and telemetry scan.
- Routed cross-domain effects through `SignalBus<ToxicityExposureSignal>`, `SignalBus<PhysiologyStateSignal>`, `SignalBus<CombatDamageSignal>`, `SignalBus<ToxicBioluminescenceSignal>`, and `HectonShaderGlobalDataVaultBridge`.
- Implemented the EditorWindow facade `Hecton8/Atmosphere/Toxic Outgassing Tuner` with sliders, CSV reload, emergency mock reset, and capped wire-cube plume visualization.

Cinematic Cheats used:
- The Dear Lie is a coarse 3D cellular automaton, not fluid truth.
- Acid readability is a shader scalar derived from density telemetry, not CPU volumetric lighting.
- Cave containment is direct SDF scalar math, not colliders/raycasts.
- Flow is a deterministic mock/curl vector field until a real flow provider is exposed through contracts.
- Flora absorption is a scalar sink with a `PurifierKelpHash`, not direct flora object interaction.

Exact Microseconds saved:
- Measured profiler data is not available in this session. I will not fabricate exact microseconds.
- Structural savings recorded:
  - Poison gas collider callbacks removed from the new route: expected physics broadphase/callback cost avoided; exact scene-dependent us pending profiler.
  - Cold binary probing: 0 us/frame.
  - Editor facade and wire visualizer: 0 us/player frame.
  - Origin rebase job: 0 us/frame unless an origin shift occurs.
  - Runtime heap arrays during evaluation loop: 0 B/frame by construction, because persistent data is held by `VaultBufferHandle<T>`.

Compile evidence:
- First CLI compile: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` succeeded after initial SHINOBU files; 9 pre-existing warnings.
- Second CLI compile after later shared-workspace changes: failed on unrelated `Assets/_Project/Scripts/LocRegistry.cs` missing `IsCsvHeaderKey` and `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs` counter type errors. No SHINOBU file errors were reported.
- Third CLI compile: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` succeeded after dependency churn settled; 9 pre-existing warnings, 0 errors.
- Fourth CLI compile after ultra-polish edits timed out at 124s.
- Fifth CLI compile failed on unrelated `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` CS0120 calls to an instance method from static context. No SHINOBU file errors were reported.
- Sixth CLI compile failed on unrelated untracked `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs` CS0117 missing `VolcanicUpdraftVault.SafeNormalize`. No SHINOBU file errors were reported.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Archive and ledger checked. Exact gas_toxicity_tables.h8bin absent; Dalton aligned payload detected as script-tool-only. Emergency mock chemistry seeds runtime constants cold.</TASK>
    <TASK id="02" status="PASS">New toxic route has no SphereCollider, TriggerCollider, OnTrigger, or Physics.OverlapSphere path.</TASK>
    <TASK id="03" status="PASS">ToxicitySourceDTO is public fields only. No hot DTO get/set accessors.</TASK>
    <TASK id="04" status="PASS">Grid is contiguous float cells in Vault-owned NativeArray buffers.</TASK>
    <TASK id="05" status="PASS">partial MockFlowField plus Burst MockFlowFieldJob implemented; no concrete sibling flow runtime dependency.</TASK>
    <TASK id="06" status="PASS">Burst ToxicDiffusionJob implements ping-pong cellular diffusion/advection with mandated Burst flags.</TASK>
    <TASK id="07" status="PASS">Current advection uses dominant-axis upwind neighbor sampling biased by flow direction and speed.</TASK>
    <TASK id="08" status="PASS">High density publishes acid caustic scalar to the shader global bridge.</TASK>
    <TASK id="09" status="PASS">Tracked entity AUPs sample density by nearest/trilinear blend and emit toxemia signals.</TASK>
    <TASK id="10" status="PASS">Corrosion accumulates on a two-second cadence and emits CombatDamageSignal with toxic bit plus acid hash.</TASK>
    <TASK id="11" status="PASS">GlobalQualityWeight controls resolution, cadence, source budget, diffusion blend, advection weight, sampling blend, signal stride, and visual scalar.</TASK>
    <TASK id="12" status="PASS">MockWorldSampler SDF negative cells zero/block gas propagation.</TASK>
    <TASK id="13" status="PASS">Origin shifts are converted to integer cell offsets and applied by RebaseGridJob.</TASK>
    <TASK id="14" status="PASS">Purifier kelp zones subtract toxic density as scalar flora absorption.</TASK>
    <TASK id="15" status="PASS">Bioluminescent overlap emits capped ToxicBioluminescenceSignal packets.</TASK>
    <TASK id="16" status="PASS">All Vault allocations request UninitializedMemory; cold clearing uses UnsafeUtility.MemClear.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry ring and Dump_TOXIC_SURGEON.bin NaN dump path implemented.</TASK>
    <TASK id="18" status="PASS">Toxic Outgassing Tuner EditorWindow implemented.</TASK>
    <TASK id="19" status="PASS">CSV overrides parse bytes from a Vault buffer without Split/LINQ/managed row arrays.</TASK>
    <TASK id="20" status="PASS">Editor plume visualizer draws capped wire cubes from the density readback.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="ToxicitySourceDTO" size="48" alignment="16-byte size multiple">
      <FIELD name="AUP" offset="0" size="24" type="double3"/>
      <FIELD name="EmissionRate" offset="24" size="4" type="float"/>
      <FIELD name="Density" offset="28" size="4" type="float"/>
      <FIELD name="ChemicalHash" offset="32" size="4" type="uint"/>
      <FIELD name="_pad0" offset="36" size="4" type="uint"/>
      <FIELD name="_pad1" offset="40" size="8" type="ulong"/>
      <MATH>24 + 4 + 4 + 4 + 4 + 8 = 48. 48 % 16 = 0.</MATH>
    </DTO>
    <DTO name="ToxicOutgassingConstants" size="64" alignment="64-byte cache line">
      <MATH>15 scalar 4-byte fields plus uint pad = 64 bytes. No Pack=1.</MATH>
    </DTO>
    <DTO name="MockFlowField" size="32" alignment="16-byte size multiple">
      <MATH>float3 12 + float 4 + float3 12 + float 4 = 32.</MATH>
    </DTO>
    <DTO name="MockWorldSampler" size="32" alignment="16-byte size multiple">
      <MATH>float 4 + float 4 + float3 12 + uint 4 + uint 4 + uint 4 = 32.</MATH>
    </DTO>
    <DTO name="ToxicityGridTelemetryEntry" size="64" alignment="64-byte cache line">
      <MATH>double3 24 + 4 floats 16 + 2 uints 8 + 3 ushorts 6 + 2 bytes 2 + ulong pad 8 = 64.</MATH>
    </DTO>
    <FALSE_SHARING>Signal counters are isolated in a Vault buffer and written by serial jobs only. No parallel atomic counter writes share a cache line.</FALSE_SHARING>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below q 0.3, active math collapses toward radial source contribution, nearest entity sampling, low source budget, wider signal stride, and a 0.20s target tick interval. The 16^3 grid is selected below q 0.4 as required. From middle to ultra, Smooth01 curves increase diffusion/advection blend, flow turbulence, source budget, trilinear sampling, signal harvest density, and shader visual overkill. Resolution gate is discrete because the prompt explicitly required 32^3 to 16^3 reduction, but cadence and solver math breathe continuously.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <PRIVATE_NATIVE_ARRAY_FIELDS count="0"/>
    <VAULT_BUFFERS>70800 DensityFront, 70801 DensityBack, 70820 DensityMirrorTemp, 70821 GridHeader, 70822 CellStates, 70802 FlowField, 70803 WorldSampler, 70804 Sources, 70805 SourceIds, 70806 EntityAups, 70807 EntityIds, 70808 EntityCorrosionTimers, 70809 EntityExposureAccumulators, 70810 ExposureSignals, 70811 CombatSignals, 70812 BiolumSignals, 70813 SignalCounters, 70814 TelemetryRing, 70815 TelemetryScratch, 70816 Constants, 70817 CsvBytes, 70818 BinaryProbeBytes, 70819 NanFlags.</VAULT_BUFFERS>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NOALIAS>All NativeArray fields inside SHINOBU jobs use NoAlias and ReadOnly where applicable.</NOALIAS>
    <GRAPH>Optional RebaseGridJob into mirror temp -> CombineDependencies(MockFlowFieldJob, MockWorldSamplerJob) -> ToxicDiffusionJob writes density back plus ToxicityStateDTOs -> EntityExposureJob -> SignalHarvestJob -> ScanTelemetryJob -> LateFrame handle swap plus 64-byte grid header update.</GRAPH>
    <CONSUMES>Dispatcher Tick delta, GlobalQualityWeight, optional OriginShift event, Vault handles.</CONSUMES>
    <OUTPUTS>JobHandle registered through H8Memory.RegisterActiveJob, density front/mirror buffers, staged signals, telemetry ring.</OUTPUTS>
    <MAIN_THREAD_BLOCKING>Job completion is deferred to LateFrameTick/next schedule boundary; no arbitrary mid-kernel Complete.</MAIN_THREAD_BLOCKING>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new asmdef was created and no new sibling assembly reference was added by SHINOBU_65. Cross-domain runtime outputs are typed signals, Vault buffers, and the existing shader global bridge. Existing Hecton8.Core.asmdef references are inherited project state, not new SHINOBU coupling.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Heavy model rejected: Navier-Stokes or CPU particle plume. Implemented model: scalar cellular automaton with SDF walls, current-biased upwind reads, and shader caustic scalar. Before: O(particles * collisions) or O(cells * expensive fluid iteration count). After: O(active cells + tracked entities + capped signals) at 5-12Hz, with 4096 low cells or 32768 high cells.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Visor mutation barrier and job phase correction

What was wrong:
- Public visor APIs could mutate Vault buffers while `VisorCondensationJob` was scheduled against those same buffers.
- `Tick()` still owned a nonblocking job completion path, leaving a future stall regression point in the simulation phase.
- The editor tuner wrote through direct mutable refs into runtime Vault memory.
- The visor render feature still used direct registry resolves as its normal path and carried explicit pack metadata on the 128-byte CBuffer DTO.

What was done:
- Added primitive pending fields for mock physiology, pressure, environment, surface wash, wipe, and mock reset. Active jobs now keep ownership of Vault buffers until `LateFrameTick` commits them.
- Removed job completion from `Tick()`. `LateFrameTick()` is the nonblocking visual-sync commit/upload point; `OnDisable()` remains the forced shutdown drain.
- Added `TryWriteState()` and `TryWriteTuning()` and routed the editor tuner through those gates.
- Cached player/fluid service references in the render feature after first successful resolve and removed `Pack=4` from `VisorFluidGlobalsDTO`.
- Removed public ref-return access to visor state/tuning; remaining ref helpers are private and gated by the safe write APIs.
- Converted both visor scalar CBuffer paths to ping-pong `GraphicsBuffer` pairs. Render-feature buffers are prewarmed in `Create()`; render execution refuses hot allocation if buffers are unavailable.
- Updated status, rationale, and `SELF_AUDIT_SHINOBU_65.xml` to stop claiming `Tick()` commits jobs.

Cinematic Cheats used:
- Unchanged. The visor remains scalar CPU authority plus shader optical fake: no Canvas dirt, no particle droplets, no per-pixel CPU fog, no crack decals.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- Expected gain: avoids simulation-phase stalls and normal-case registry lookup churn. The actual microsecond delta requires Unity Profiler/Frame Debugger.
- Ping-pong CBuffers remove a CPU/GPU sync hazard; exact driver cost requires capture.
- Race prevention is correctness-first; steady-state cost is primitive branch checks and 0 B/frame.

Verification:
- `git diff --check` on touched visor/doc files reported CRLF normalization warnings only for shader/render feature files.
- Static grep found no `Pack=1`, `new NativeArray`, `SetData`, `SetFloat`, `MaterialPropertyBlock`, `UnityEngine.Random`, `Time.deltaTime`, LINQ, `Split`, `Canvas`, `Image`, `ParticleSystem`, `double`, `AbsoluteUniversePosition`, or runtime singleton instance in the touched visor path.
- No `dotnet build` launched: CPU samples were 100/100/100 and `Hecton8.Core.csproj` still omits the new visor runtime/types/editor files, so dotnet would not prove the full addition.

## 2026-05-18 - Diegetic visor ultra polish loop

What was wrong:
- Runtime exposed `public static DiegeticVisorLensRuntime Instance`; that is singleton-shaped access and not needed for player execution.
- The 64-byte visor GPU globals buffer could be allocated during first `LateFrameTick`, causing a first-use render-frame allocation instead of cold boot allocation.
- The inherited visor feature still pushed a binary low-tier flag into the shader path.
- `VisorBreachSignal` was unmanaged and correctly sized, but not `partial` as the task requested.
- The cold file reader used a mixed int/long `math.min` expression that depended on Unity.Mathematics overload availability.
- Self-audit proof was present in chat/log text but not in a dedicated durable XML artifact.

What was done:
- Removed runtime `Instance`; the editor tuner now locates the runtime only inside the editor window through `UnityEngine.Object.FindFirstObjectByType`.
- Allocated and neutral-cleared the visor CBuffer during `EnsureNativeState()` and clear-publishes globals on disable. Dirty upload now repushes scalar vectors only when the 64-byte DTO changes.
- Converted RenderFeature `RuntimeState` from property accessors to readonly fields.
- Replaced low-tier boolean upload with `LowTierWeight01`, derived from `GlobalQualityWeight`, hardware fallback, stress, and lens refraction scale.
- Updated HLSL to use `dynamicVisorWeight` and `refractionWeight`: low quality skips procedural droplet noise and Snell refraction, while middle/high/ultra blend back into richer visor optics.
- Marked `VisorBreachSignal` partial and removed the mixed-overload file-length clamp.
- Added `Docs/AgentLogs/SELF_AUDIT_SHINOBU_65.xml` with 20-task reconciliation, DTO offsets, Vault IDs, dependency graph, compile guard, and Dear Lie complexity.

Cinematic Cheats used:
- Low quality now collapses into a static edge-weighted film plus chroma fallback instead of evaluating full droplet noise and then hiding it.
- Droplets remain a CPU scalar plus shader UV/noise illusion; no particles, physics droplets, Canvas dirt, reflection camera, or fog render texture.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- Expected low-tier GPU saving: avoids `ComputeDropletMask` value-noise work and Snell refraction sampling when `GlobalQualityWeight` collapses below the visor dynamic range.
- Expected CPU/frame saving: removes repeated unchanged global vector publication and moves the only visor `GraphicsBuffer` allocation out of first visible frame.
- Runtime singleton removal is architecture hygiene, not a measurable frame saving.

Verification:
- Static grep after polish found no `Instance`, DTO properties, `Pack=1`, `double`, `AbsoluteUniversePosition`, `Canvas`, `Image`, `ParticleSystem`, `SetData`, `SetFloat`, `MaterialPropertyBlock`, `UnityEngine.Random`, `Time.deltaTime`, `Split`, LINQ, or `new NativeArray` in the touched visor runtime/type/editor/feature files.
- `git diff --check` on touched files reported only CRLF normalization warnings for `Hecton_VisorFluidDistortion.shader` and `HectonVisorFluidDistortionFeature.cs`.
- `dotnet build` was not launched. User explicitly said not to launch until needed; static verification did not create a compile wall that justified it.

<SELF_AUDIT_REVISION id="SHINOBU_65_VISOR_ULTRA_POLISH">
  <TASK_RECONCILIATION count="20" status="PASS">Tasks 01-20 remain implemented. This loop hardens singleton removal, cold GPU allocation, continuous quality, partial breach signal, cold file parsing, and durable audit evidence.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>`VisorStateDTO`: offset 0 CondensationLevel float, offset 4 WaterDropletIntensity float, offset 8 CrackSeverity float, offset 12 DirtAccumulation float, total 16 bytes. `VisorLensTelemetryEntry` is 64 bytes and single-writer, so false-sharing atomic counter padding is not applicable.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below q ~0.3, CPU refractionScale and shader dynamicVisorWeight collapse toward zero. Shader uses static film/chroma and bypasses expensive droplet noise/refraction. Middle blends; high/ultra restore droplet flow, Snell, reflection, salt, and silt.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Private persistent NativeArray/List/HashMap fields remain zero. Boot Vault IDs are 71020-71029.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>`VisorCondensationJob` has NoAlias on State, Tuning, Physiology, Environment, GpuGlobals, and NanFlags. The job publishes `_scheduledHandle`; Tick/LateFrame nonblock unless complete; OnDisable force-completes.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No direct sibling runtime assembly dependency added. Physiology, audio, anomaly, and waterline are consumed as core signal lanes or local mock DTOs.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Rejected O(n droplets) physics and render texture fog. Current path is O(1) scalar state plus O(k) bounded signal snapshots and shader procedural masks.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT_REVISION>

## 2026-05-19 - Diegetic visor continuous CPU cadence

What was wrong:
- Low-quality shader math collapsed, but the CPU Burst solver still had no continuous cadence throttle. That left low-tier hardware paying steady-state job scheduling too often.

What was done:
- Added `_simulationAccumulator` and `ResolveSimulationInterval()`.
- `GlobalQualityWeight` now maps through a smooth polynomial and `math.lerp` to 5 Hz at q=0.1 and 60 Hz at q=1.0.
- `Tick()` accumulates dispatcher delta and schedules `VisorCondensationJob` only when the interval expires.
- Breath, splash, wipe, pressure, glitch, and mock injection paths set `_forceImmediateSimulation` for one immediate schedule.

Cinematic Cheats used:
- Low quality now fakes visor change as sparse scalar updates plus static film/chroma shader presentation. It does not run a high-frequency fluid sim and then hide it.

Exact Microseconds saved:
- No profiler data. No fabricated number.
- Expected schedule reduction at q=0.1: up to 60 solver schedules/sec down to 5 schedules/sec in steady state.
- Event frames still pay one immediate job to keep surface wash, pressure cracks, and breath fog responsive.

Verification:
- Static grep found no runtime singleton instance, DTO properties, `Pack=1`, `double`, AUP, Canvas/Image/ParticleSystem, `SetData`, `SetFloat`, MPB, `UnityEngine.Random`, `Time.deltaTime`, `Split`, LINQ, or `new NativeArray`.
- `git diff --check` reports only CRLF normalization warnings for the existing shader/RenderFeature files.
- No `dotnet build` launched; static evidence did not justify the compile wall.

<SELF_AUDIT_REVISION id="SHINOBU_65_VISOR_CPU_CADENCE">
  <TASK_RECONCILIATION count="20" status="PASS">Task 11 now affects CPU cadence and shader ALU; Tasks 01-20 remain reconciled.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO size changed in this loop. `VisorStateDTO` remains 16 bytes; telemetry remains 64 bytes.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>q=0.1 maps to 5 Hz solver cadence; q=1.0 maps to 60 Hz; middle weights interpolate smoothly. Low shader path uses static film/chroma, high/ultra restore dynamic droplet/refraction.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs unchanged: 71020-71029. No private persistent NativeArray/List/HashMap fields added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>NoAlias fields unchanged. `_scheduledHandle` remains the output job handle; forced completion remains limited to shutdown.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling runtime assembly dependency added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Steady state is sparse scalar updates, not continuous fluid truth. Event frames force one scalar update only.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT_REVISION>

## 2026-05-19 - Diegetic visor telemetry completion

What was wrong:
- Task 17 required `ShaderUpdateComputeTimeNs`, but the 64-byte visor black-box entry did not expose that timing lane.
- Adding a separate compute dispatch just to satisfy the word "Compute" would spend GPU time without changing the visual result.

What was done:
- Replaced the lower-value telemetry `Darkness01` lane at offset 56 with `uint ShaderUpdateComputeTimeNs`.
- `UploadGpuGlobals()` now measures scalar publish / CBuffer bind time using integer `Stopwatch` ticks, converts it to nanoseconds without floating-point conversion, and patches the latest telemetry ring entry.
- Updated `SELF_AUDIT_SHINOBU_65.xml` with the exact telemetry offsets.

Cinematic Cheats used:
- Kept the Dear Lie intact: CPU emits scalar visor state, shader/RenderFeature consumes the CBuffer. No extra compute dispatch for a four-float transformation.

Exact Microseconds saved:
- No profiler data. No fabricated number.
- Avoided one standalone compute dispatch per visor update.
- Added cost: two timestamp reads plus one 64-byte ring read/write per shader upload.

Verification:
- Static grep found no runtime singleton instance, DTO properties, `Pack=1`, `double`, AUP, Canvas/Image/ParticleSystem, `SetData`, `SetFloat`, MPB, `UnityEngine.Random`, `Time.deltaTime`, `Split`, LINQ, or `new NativeArray`.
- `SELF_AUDIT_SHINOBU_65.xml` parses as XML.
- `git diff --check` reports only CRLF normalization warnings for the existing shader/RenderFeature files.
- No `dotnet build` launched; this did not justify a compile wall.

<SELF_AUDIT_REVISION id="SHINOBU_65_VISOR_TELEMETRY_COMPLETION">
  <TASK_RECONCILIATION count="20" status="PASS">Task 17 now includes the required `ShaderUpdateComputeTimeNs` black-box lane while Tasks 01-20 remain reconciled.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>`VisorLensTelemetryEntry` remains 64 bytes. Offset 56 is now `uint ShaderUpdateComputeTimeNs`; offset 60 remains `float Anomaly01`.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>The timing lane records the actual scalar GPU publish path across static-film low quality, blended middle quality, and high/ultra refraction.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs unchanged: 71020-71029. No private persistent NativeArray/List/HashMap fields added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No job graph change. Upload telemetry patch happens after `UploadGpuGlobals()` and writes the latest single-writer ring entry.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling runtime assembly dependency added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Rejected a standalone compute dispatch for four scalar lanes. Current algorithm remains O(1) CPU scalar state plus O(k) bounded signal snapshots and shader procedural masks.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT_REVISION>

## 2026-05-18 - Ultra-polish re-audit pass

What was wrong:
- Runtime had a recursive archive helper using `Directory.GetFiles`; that is managed array allocation and belongs in CLI evidence, not gameplay.
- Simulation frame metadata used Unity `Time.frameCount`; rollback-visible state needs a deterministic counter.
- Ping-pong buffers were copied back after completion; that is not a real swap and spends avoidable bandwidth.
- Binary probe checked only little-endian magic.

What was done:
- Removed runtime recursive archive scan. Boot probes fixed toxicity payload paths only.
- Moved binary probing out of `SlowTick`; it is now boot-only.
- Added `_simulationFrameCounter` and removed all `Time.*` references from SHINOBU runtime.
- Swapped front/back Vault buffer handles after diffusion completion; removed full-grid `NativeArray<float>.Copy`.
- Rebase now writes into the mirror temp buffer, then diffusion reads that temp and writes into back.
- Added field-only 32-byte `ToxicityStateDTO`.
- Added endian-defensive local `ReverseBytes(uint)` magic check.
- Fixed boot recursion by setting `_nativeReady` after Vault handles/MemClear and before public seed/load helpers.
- Added zero-copy `ToxicOutgassingGridHeaderDTO` so consumers resolve the current active ping-pong density buffer without copying.
- Added per-cell `ToxicityStateDTO` writes from `ToxicDiffusionJob`.

Cinematic Cheats used:
- Unchanged: cellular automaton plus SDF math plus shader scalar. No particles, no colliders, no raycasts.

Exact Microseconds saved:
- Measured profiler data is still unavailable.
- Bandwidth avoided by handle swap: one 16KB low-grid copy or 128KB high-grid copy per diffusion commit, plus the previous mirror copy.
- Recursive archive scan removed from gameplay: managed allocation burst removed; steady-state remains 0 B/frame for SHINOBU evaluation.
- Boot recursion fix: correctness-only; prevents stack overflow, no steady-state frame delta.
- Grid header: one 64-byte write replaces stable mirror density copies.
- Cell states: one 32-byte write per active cell at diffusion cadence; no heap allocation.

Verification:
- Grep after hardening found no `Time.*`, `Directory.GetFiles`, `NativeArray<float>.Copy`, LINQ, foreach, Random, Physics, SphereCollider, Trigger, `Pack=1`, or DTO property accessors in SHINOBU files.
- Latest compile is blocked by unrelated World domain error in `VolcanicUpdraftDirector.cs`; no SHINOBU compiler errors were emitted.

## 2026-05-18 - No-build static hardening pass

What was wrong:
- Source/entity mutation could race running Burst jobs.
- `ToxicityStateDTO` export was single-buffered, so readers could race the writer job.
- The editor tuner lived in the runtime source folder.
- Low quality still evaluated sine/cosine flow/SDF detail and trilinear sampling when the mathematical blend was zero.
- The code used `math.reversebytes`, but the installed Unity.Mathematics package only exposes `reversebits`.
- Previous dotnet build evidence was not final SHINOBU proof because current generated `.csproj` files do not list the new untracked SHINOBU files.

What was done:
- Added `TryOpenMutationWindow()` and applied it to source/entity upsert/remove.
- Split state export into `CellStateFrontBufferId` 70822 and `CellStateBackBufferId` 70823, swapped with density.
- Moved `ToxicOutgassingTunerWindow.cs` to `Assets/_Project/Scripts/Editor`.
- Added low-quality ALU collapse: no trig detail in flow/world jobs below threshold, no trilinear sample while blend is zero.
- Replaced nonexistent `math.reversebytes` with local byte-swap helper.
- Verification ledger now states that Unity/project regeneration is needed before dotnet can prove these new files.
- Wrapped optional CSV/binary cold loaders so mock chemistry remains active on IO failure.

Cinematic Cheats used:
- Still the same Dear Lie: coarse grid plus SDF scalar containment plus shader caustic scalar.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- Low-tier path now avoids per-cell trig in mock flow/world jobs and skips 8-tap trilinear sampling when q keeps the blend at zero.
- State double buffering costs bounded Vault memory but prevents reader/writer synchronization stalls.
- Cold loader guards are boot/editor only; 0 us/frame.

Verification:
- No `dotnet build` was launched in this pass per user order.
- Static grep remains clean for banned hot-path markers: `Time.*`, `Directory.GetFiles`, `NativeArray<float>.Copy`, LINQ, foreach, Random, Physics, SphereCollider, Trigger, `Pack=1`, DTO property accessors.

## 2026-05-18 - Dependency-chain correction and audit revision

What was wrong:
- The previous code contradicted its own dependency-chain claim: `Tick()` called `CompleteScheduledWork()` every frame, and that method unconditionally called `JobHandle.Complete()`.
- Custom toxic SignalBus lanes were not initialized until first push, risking a first-contact native queue allocation during gameplay.
- Built-in physiology/combat signals were bypassing `GlobalSignals.Publish`, losing the core latest-signal/sanitization path.
- The earlier XML audit did not list `CellStateBackBufferId` 70823 and still described the endian guard as `math.reversebytes` instead of the local `ReverseBytes(uint)` helper.

What was done:
- `CompleteScheduledWork(bool force = false)` now returns immediately while a job is unfinished. `Tick()` accumulates dispatcher delta, attempts a nonblocking commit, and returns if the diffusion graph is still running.
- `OnDisable` calls `CompleteScheduledWork(force: true)` so shutdown reclaims ownership without leaving registered stale listeners.
- `PrewarmSignalLanes()` configures and initializes custom toxic lanes and initializes built-in physiology/combat lanes during native boot.
- Physiology and combat outputs now publish through `GlobalSignals.Publish(in ...)`; custom toxic exposure/biolum outputs remain on typed `SignalBus<T>`.

Cinematic Cheats used:
- No change to the Dear Lie: the gameplay truth is still a coarse SDF-contained CA grid, while optical acid behavior is a shader scalar.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- Removed main-thread stall risk equal to any unfinished diffusion/advection graph time.
- Moved first toxic custom-signal native queue allocation from gameplay contact to boot.

Verification:
- No `dotnet build` launched in this correction pass.
- Static grep remains clean for the SHINOBU files: no `Time.*`, no `Directory.GetFiles`, no `NativeArray<float>.Copy`, no LINQ/`foreach`, no Random, no Physics/SphereCollider/Trigger, no `Pack=1`, no DTO properties, no `math.reversebytes`.
- Generated `.csproj` files still do not list the new untracked SHINOBU files; Unity/project regeneration is still required before dotnet can prove these files.

<SELF_AUDIT_REVISION id="SHINOBU_65_LOOP8">
  <TASK_RECONCILIATION count="20" status="PASS">Tasks 01-20 remain implemented as recorded above. Loop 8 corrects the dependency graph and signal-lane cold allocation evidence without changing task scope.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="ToxicityStateDTO" size="32">
      <FIELD name="Density" offset="0" size="4"/>
      <FIELD name="PreviousDensity" offset="4" size="4"/>
      <FIELD name="FlowBias" offset="8" size="4"/>
      <FIELD name="SdfDistance" offset="12" size="4"/>
      <FIELD name="ChemicalHash" offset="16" size="4"/>
      <FIELD name="CellHash" offset="20" size="4"/>
      <FIELD name="Frame" offset="24" size="4"/>
      <FIELD name="_pad0" offset="28" size="4"/>
      <MATH>8 fields * 4 bytes = 32; 32 % 16 = 0.</MATH>
    </DTO>
    <DTO name="ToxicOutgassingGridHeaderDTO" size="64">
      <FIELD name="GridOriginAUP" offset="0" size="24"/>
      <FIELD name="CellSizeMeters" offset="24" size="4"/>
      <FIELD name="GlobalQualityWeight" offset="28" size="4"/>
      <FIELD name="ActiveDensityBufferId" offset="32" size="4"/>
      <FIELD name="BackDensityBufferId" offset="36" size="4"/>
      <FIELD name="StateBufferId" offset="40" size="4"/>
      <FIELD name="DensityVersion" offset="44" size="4"/>
      <FIELD name="Resolution" offset="48" size="2"/>
      <FIELD name="ActiveSources" offset="50" size="2"/>
      <FIELD name="ActiveEntities" offset="52" size="2"/>
      <FIELD name="Flags" offset="54" size="1"/>
      <FIELD name="_pad0" offset="55" size="1"/>
      <FIELD name="_pad1" offset="56" size="8"/>
      <MATH>24 + 4 + 4 + 4 + 4 + 4 + 4 + 2 + 2 + 2 + 1 + 1 + 8 = 64.</MATH>
    </DTO>
    <DTO name="ToxicitySourceDTO" size="48">
      <MATH>double3 24 + float 4 + float 4 + uint 4 + uint pad 4 + ulong pad 8 = 48; 48 % 16 = 0.</MATH>
    </DTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below q 0.3, the grid is already on the 16^3 path, tick interval trends to the 0.20s survival cadence, source budget contracts, entity sampling stays nearest, mock flow/world jobs skip trig detail, and diffusion/advection blends collapse toward radial source math. Higher q restores 32^3, trilinear exposure, detailed mock curl/SDF ribs/flora, denser signal harvest, and stronger shader caustic scalar.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <PRIVATE_NATIVE_ARRAY_FIELDS count="0"/>
    <VAULT_BUFFERS>70800 DensityFront, 70801 DensityBack, 70820 DensityMirrorTemp, 70821 GridHeader, 70822 CellStateFront, 70823 CellStateBack, 70802 FlowField, 70803 WorldSampler, 70804 Sources, 70805 SourceIds, 70806 EntityAups, 70807 EntityIds, 70808 EntityCorrosionTimers, 70809 EntityExposureAccumulators, 70810 ExposureSignals, 70811 CombatSignals, 70812 BiolumSignals, 70813 SignalCounters, 70814 TelemetryRing, 70815 TelemetryScratch, 70816 Constants, 70817 CsvBytes, 70818 BinaryProbeBytes, 70819 NanFlags.</VAULT_BUFFERS>
    <SIGNAL_LANES>Custom toxic exposure and biolum lanes are boot-prewarmed; built-in physiology/combat lanes are boot-initialized.</SIGNAL_LANES>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NOALIAS>All job NativeArray fields use NoAlias, with ReadOnly on immutable inputs.</NOALIAS>
    <GRAPH>Optional RebaseGridJob -> CombineDependencies(MockFlowFieldJob, MockWorldSamplerJob) -> ToxicDiffusionJob -> EntityExposureJob -> SignalHarvestJob -> ScanTelemetryJob -> nonblocking LateFrame/Tick commit if IsCompleted.</GRAPH>
    <FORCED_COMPLETE>Only shutdown uses `CompleteScheduledWork(force: true)`.</FORCED_COMPLETE>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new sibling asmdef reference was added. New runtime code remains in Atmosphere and communicates outward through Vault buffers, GlobalSignals, typed SignalBus lanes, and the existing shader bridge.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: collider/particle/volumetric poison truth with O(particles * collision checks) or physics callback overhead. After: O(active cells + tracked entities + capped signals) CA update at quality-scaled cadence, with SDF scalar containment and shader scalar optical fraud.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT_REVISION>

## 2026-05-18 - Amnesia cleanup, telemetry correction, no-build verification

What was wrong:
- Active SHINOBU long-term notes still contained a foreign duplicate-ID task label in cleanup explanations.
- `DiffusionCompleteMs` measured only the final `JobHandle.Complete()` drain after readiness, not the real schedule-to-commit latency.
- The grid resize helper was defensively weak: a future caller could clear Vault buffers while a scheduled graph was active.

What was done:
- Removed the foreign duplicate-ID task label from status/rationale language.
- Added `_scheduledStartTicks` and changed telemetry commit math to record end-to-end scheduled graph latency.
- Converted resize to `TryResizeActiveGrid()` and refused buffer clears while `_hasScheduledWork` remains true.
- Added stable MonoImporter `.meta` files for the new runtime, type, and editor scripts.
- Re-ran static no-build hygiene checks over the SHINOBU runtime/types/editor files.

Cinematic Cheats used:
- No change: poison remains an SDF-contained scalar CA with current bias and shader caustic scalar. No triggers, no particles, no Navier-Stokes.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- Timestamp telemetry costs two `Stopwatch.GetTimestamp()` calls per diffusion commit.
- Resize guard saves correctness, not steady-state time; it prevents a race that would have poisoned the black-box evidence.
- Unity `.meta` files are asset-database hygiene; 0 us/frame.
- Static verification remains 0 us/player frame.

Verification:
- No `dotnet build` launched per explicit user order.
- Static grep found no banned SHINOBU markers: `Time.*`, recursive archive scan, `NativeArray<float>.Copy`, LINQ/`foreach`, Random, Physics/SphereCollider/Trigger, `Pack=1`, DTO properties, or `math.reversebytes`.

<SELF_AUDIT_REVISION id="SHINOBU_65_LOOP10">
  <TASK_RECONCILIATION count="20" status="PASS">Tasks 01-20 remain implemented. Loop 10 corrects telemetry and defensive resize behavior without changing the original task surface.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No struct layout changed in this loop. `ToxicityStateDTO` remains 32 bytes; `ToxicOutgassingGridHeaderDTO` remains 64 bytes; `ToxicitySourceDTO` remains 48 bytes.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Resolution changes remain governed by continuous `GlobalQualityWeight`; the resize now waits for a safe mutation window instead of clearing active job buffers. Below q 0.3 the same 16^3 nearest/radial/trig-collapsed path is preserved.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No private NativeArray/List/HashMap fields added. Existing VaultBufferHandle IDs remain unchanged, including ping-pong density 70800/70801 and ping-pong state 70822/70823.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>`ScheduleSimulation()` now timestamps before job graph creation. `CompleteScheduledWork()` still commits only if complete unless forced by shutdown, then records schedule-to-commit latency.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No assembly reference or sibling-domain dependency added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Unchanged. The visual/physical fake remains O(active cells + tracked entities + capped signals), with shader scalar carrying acid caustic presentation.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT_REVISION>

## 2026-05-18 - Diegetic visor lens simulator

What was wrong:
- The live `SHINOBU_65` ID is duplicated in `CURRENT_BATCH.md`; the current user assignment points to the second visor block, not the toxic trail.
- Helmet glass effects had no centralized CPU scalar authority for condensation/cracks/dirt tied to physiology/head motion.
- Canvas/particle approaches are disallowed for this domain and would be the wrong rendering path.

What was done:
- Added `VisorStateDTO` (16 bytes, four public floats), tuning/mock/environment DTOs, 64-byte GPU globals, 64-byte telemetry entries, and unmanaged `VisorBreachSignal`.
- Added `DiegeticVisorLensRuntime`: Vault-owned buffers 71020-71029, Burst `VisorCondensationJob`, mock physiology/environment injection, signal ingestion, head angular velocity droplet gravity, CBuffer upload, CSV parser, archive probe fallback, breach signal, and 300-frame black box dump to `Docs/AgentLogs/Dump_VISOR_SURGEON.bin`.
- Extended `HectonVisorFluidDistortionFeature` to fold diegetic condensation/droplets/cracks/dirt into the existing RenderGraph visor pass instead of spawning a second pass.
- Extended `Hecton_VisorFluidDistortion.shader` with `HectonDiegeticVisorLensGlobals`, procedural condensation haze, crack ridge/noise, dirt/silt coupling, reflection tint, and continuous refraction load-shed.
- Added `Diegetic Visor Tuner` EditorWindow with live sliders, mock/reload/wipe controls, and 2D procedural mask preview.

Cinematic Cheats used:
- No droplet physics. CPU emits scalar state and droplet gravity; shader fakes droplet motion with UV/noise flow.
- No real vapor simulation. Breath/cold water feed a scalar; shader spatializes condensation with cheap value noise.
- No crack decals. Crack severity thresholds procedural ridge/noise in the visor shader.
- No Canvas Image. Existing URP visor RenderGraph pass carries the visual.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- Expected runtime cost is one scalar Burst IJob, one 64-byte GPU globals upload when dirty, one 64-byte telemetry write per commit, and bounded signal snapshot scans.
- Rejected particle droplets, Canvas overlays, reflection cameras, and render-texture fog accumulation because each would spend more CPU/GPU budget than the scalar Dear Lie.

Verification:
- CLI extracted the second `SHINOBU_65` visor block and counted 20 tasks.
- Archive scan found no `visor_materials_006.h8bin`; runtime falls back to `GenerateEmergencyMockVisorData()`.
- Static grep found no `double`, `AbsoluteUniversePosition`, `Canvas`, `Image`, `ParticleSystem`, `Pack=1`, `new NativeArray`, `SetData`, or DTO private setters in new visor runtime/types/editor files.
- `git diff --check` on touched files reported line-ending warnings only for pre-existing CRLF normalization.
- No `dotnet build` launched: no dotnet/csc process was active, but CPU samples were 93.17%, 63.80%, and 86.43%, above the 50% guard.

<SELF_AUDIT id="SHINOBU_65_VISOR">
  <TASK_RECONCILIATION count="20" status="PASS">Visor Tasks 01-20 implemented or statically satisfied. Compile remains blocked by CPU guard, not claimed green.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>`VisorStateDTO` is exactly 16 bytes: CondensationLevel 0..3, WaterDropletIntensity 4..7, CrackSeverity 8..11, DirtAccumulation 12..15. No `Pack=1`.</STRUCT_LAYOUT_VERIFICATION>
  <NO_CANVAS_OR_PARTICLES>No Canvas Image, RawImage, or ParticleSystem path added. Existing URP RenderGraph visor shader is the visual surface.</NO_CANVAS_OR_PARTICLES>
  <SCALABILITY_CURVE>`GlobalQualityWeight` drives dynamic droplet blend and refraction scale. Low collapses to static/chroma; middle keeps fog/dirt/crack masks; high/ultra adds head-motion droplet flow and richer refraction/reflection.</SCALABILITY_CURVE>
  <AUP_STATUS>No `double`, `double3`, or `AbsoluteUniversePosition` in new visor runtime/types/jobs.</AUP_STATUS>
  <BLACK_BOX>Status: implemented. Ring is Vault-owned at IDs 71025/71026; dump path is `Docs/AgentLogs/Dump_VISOR_SURGEON.bin`.</BLACK_BOX>
</SELF_AUDIT>

## 2026-05-19 - Visor final static audit and memory cleanup

What was wrong:
- Active anti-amnesia files contained a wrong-domain duplicate block before the visor section.
- Status/rationale still described public ref accessors after the mutation barrier closed them.
- Build-guard wording was stale after `dotnet`/`csc` processes appeared and CPU remained pinned at 100%.

What was done:
- Trimmed `Docs/Tasks/Status_SHINOBU_65.md` and `Docs/AgentLogs/Rationale_SHINOBU_65.md` to the active visor XML assignment only.
- Updated Task 03/rationale to state guarded `TryWriteState`/`TryWriteTuning` writes and private unsafe helpers.
- Re-ran no-build static scans over the visor runtime/types/editor/RenderFeature/shader path.
- Updated `SELF_AUDIT_SHINOBU_65.xml` with current build-guard evidence.

Cinematic Cheats used:
- Unchanged: CPU emits scalar condensation/droplet/crack/dirt state; shader fakes spatial fog, droplet flow, cracks, dirt, chroma, and refraction.
- No Canvas Image, no particle droplets, no crack decals, no reflection camera, no separate compute dispatch for four scalar lanes.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- Status/rationale cleanup saves 0 us/frame.
- Ping-pong CBuffers remove a driver sync hazard; exact GPU/CPU us pending profiler.
- Low-quality q=0.1 cadence remains 5 Hz instead of 60 Hz, cutting steady-state solver schedules by 55/sec before event-forced one-shots.

Verification:
- `SELF_AUDIT_SHINOBU_65.xml` parses as XML.
- Banned-pattern scan returned empty for runtime singleton, DTO properties, `Pack=1`, `double`, AUP, Canvas/Image/ParticleSystem, `SetData`, `SetFloat`, MPB, `UnityEngine.Random`, `Time.deltaTime`, `Split`, LINQ, and `new NativeArray`.
- Completion ownership scan shows `CompleteScheduledWork(false)` only from `LateFrameTick`; forced completion remains in `OnDisable`.
- Active status/rationale/self-audit contain no wrong-domain `toxic` text.
- `git diff --check` returned CRLF normalization warnings only.
- `dotnet build` not launched: latest CPU samples were 100/100/99.42, above the 50% build guard.

## 2026-05-19 - Literal RenderGraph compute visor mask

What was wrong:
- The previous visor path was CPU/Burst scalar authority plus CBuffer/raster shader consumption. That is efficient, but it did not literally route the scalar visor state through a Compute Shader as the active prompt states.
- A fake compute dispatch that only copies constants would have added GPU cost without improving the lens.

What was done:
- Added `Assets/_Project/Art/Shaders/Hecton_DiegeticVisorLens.compute` with `ResolveDiegeticVisorLensMask`.
- Wired the compute shader through `HectonVisorFluidDistortionFeature` as a declared RenderGraph compute pass. It writes a transient downscaled RGBA mask: condensation/droplets, cracks, dirt/silt, anomaly glitch.
- Extended `Hecton_VisorFluidDistortion.shader` to sample `_HectonDiegeticVisorLensMaskTex` and blend the compute mask into condensation, crack, dirt, and glitch presentation.
- Kept the CPU/Burst solver as scalar authority; no Canvas Image, particles, crack decals, or runtime RTHandle ownership were added.

Cinematic Cheats used:
- No physical droplets. Head angular velocity becomes scalar droplet gravity; compute and raster shaders fake the glass flow with UV/noise.
- No vapor field. Breath/cold/heart/core-temperature become one condensation scalar plus downscaled mask.
- No crack geometry. Pressure becomes a scalar cutoff over procedural ridge/noise.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- The intended trade is one quality-gated downscaled compute dispatch replacing repeated full-resolution fragment noise for visor-local masks.
- Low quality resolves compute blend to zero and keeps static film/chroma; high/ultra spend the saved full-res fragment ALU on richer refraction/reflection/silt.

Verification:
- Unity import, shader compiler, Frame Debugger, profiler, and PlayMode proof remain pending.
- `dotnet build` still not launched under the explicit build guard.

## 2026-05-19 - XR-safe visor compute mask descriptor

What was wrong:
- The compute mask descriptor copied `activeColorTexture` layout. In XR, that can inherit texture-array slices or VR usage while the kernel writes `RWTexture2D<float4>`.
- The raster shader sampled the single-slice lens mask with the same stereo-transformed UV used for camera color/depth.

What was done:
- Changed `TryAddDiegeticLensMaskPass()` to create `_HectonDiegeticVisorLensMask` from explicit width/height with `xrReady: false`.
- Forced `slices = 1`, `dimension = Tex2D`, `VRTextureUsage.None`, clamp wrap, no dynamic scale, no mips, and UAV access.
- Split mask UV from camera UV: `_HectonDiegeticVisorLensMaskTex` now samples raw fullscreen `input.screenUV`; scene/depth/color sampling still uses `ResolveXRStereoScreenUV`.
- Updated status, rationale, and self-audit with the descriptor constraint.

Cinematic Cheats used:
- The mask remains a single 2D visor-local optical fake, not per-eye droplet simulation and not physical water.
- CPU remains scalar authority; compute only resolves downscaled condensation/crack/dirt/glitch mask lanes when continuous quality and activity justify it.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- Descriptor hardening saves correctness, not measured time.
- Low quality still resolves compute blend to zero; middle/high/ultra use the stable downscaled mask without XR resource-shape mismatch.

Verification:
- XML parse OK, banned-pattern scan OK, explicit descriptor scan OK.
- `git diff --check` returned CRLF normalization warnings only.
- Pending: Unity import, shader compiler, XR single-pass visual proof, Frame Debugger, profiler.
- Guard later allowed one targeted `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: no dotnet/csc process, CPU samples 18.05, 14.19, and 24.21.
- Build failed with 26 unrelated project errors: `math.reversebytes`, unassigned `sanitizedWeight`, missing `IndustrialLoreBitMask`, missing `AssetRecord` AUP fields, and missing `HectonDrsRenderFeatureGate` in other visor features. No error was reported for `HectonVisorFluidDistortionFeature.cs`.

## 2026-05-19 - Scalability event mutation barrier

What was wrong:
- `OnScalabilityChanged()` still wrote `VisorLensTuningDTO.Version` directly through the Vault.
- That write can race `VisorCondensationJob`, which reads the same tuning buffer after scheduling.

What was done:
- Added `_pendingTuningVersionIncrement`.
- If scalability changes while work is active, the callback stages the version increment and forces one immediate simulation.
- `Tick()` now applies the pending tuning version only after scheduled work has committed and before the next job is scheduled.

Cinematic Cheats used:
- Unchanged. CPU stays scalar-authority; compute/raster shaders fake condensation, droplets, dirt, cracks, and glitch from compact lanes.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- Added cost: one boolean check in the pre-schedule path.
- Saved failure mode: tuning buffer data race during quality changes.

Verification:
- Static source audit confirms `OnScalabilityChanged()` no longer calls `GetElementAsRef` directly.
- Compile remains dependency-blocked by unrelated project errors listed above.

## 2026-05-19 - Binary low-tier gate removal

What was wrong:
- The RenderFeature still used `ResolveLowTier()` and `lowTier ? 1f : 0f` as part of the shader load-shed signal.
- That violates the continuous `GlobalQualityWeight` law even if later math packs the result into a float.

What was done:
- Removed the boolean low-tier resolver from the visor RenderFeature.
- Added continuous `ResolveHardwareLowPressure01()` based on VRAM headroom against `lowTierVideoMemoryMb`.
- Changed visual overkill to depend on `GlobalQualityWeight`, thermal headroom, and designer strength only; quality tier stays telemetry-only for this path.

Cinematic Cheats used:
- Same Dear Lie: CPU sends scalar state; compute/raster shaders fake the spatial glass.
- Low pressure fades out compute/refraction/salt/silt proportionally instead of flipping.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- Runtime math change is negligible.
- The saved failure mode is quality popping and abrupt shader ALU cliffs.

Verification:
- Static grep confirms `ResolveLowTier` is gone from `HectonVisorFluidDistortionFeature.cs`.
- Compile remains dependency-blocked by unrelated project errors.

## 2026-05-19 - Compute CBuffer and motion ramp

What was wrong:
- The diegetic lens compute pass still bound five scalar lanes through separate `SetComputeVectorParam` calls.
- Thermal motion cull still used a hard velocity threshold, which can pop the visor distortion when the player crosses the speed boundary.

What was done:
- Added an 80-byte `LensComputeGlobalsDTO` with five `Vector4` lanes.
- Added cold-prewarmed ping-pong `GraphicsBuffer.Target.Constant` buffers for the compute payload.
- Imported the active compute CBuffer into RenderGraph and declared `UseBuffer(..., AccessFlags.Read)`.
- Replaced the compute shader loose uniforms with `CBUFFER_START(HectonDiegeticVisorLensComputeGlobals)`.
- Replaced the velocity hard cutoff with a 12-15 m/s `Smooth01` ramp over local speed squared.

Cinematic Cheats used:
- Still no real droplets, vapor, or crack physics. CPU sends scalar glass truth; compute/raster shaders fake the spatial mask and optical response.
- Motion suppression now blends the fake out instead of abruptly deleting it.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- Expected benefit is fewer render-thread compute parameter calls on active mask frames and no visual pop at the motion threshold.

Verification:
- Static source now has `SetComputeConstantBufferParam` and no `SetComputeVectorParam` in the touched compute pass.
- XML parse OK; banned-pattern scan OK; compute CBuffer declaration/import/UseBuffer scan OK; no binary render gate tokens remain in the touched RenderFeature.
- `git diff --check` returned CRLF normalization warnings only.
- Build guard blocked compile: no dotnet/csc process, but CPU samples were 99.22, 90.95, and 100 percent.
- Unity import, shader compiler, Frame Debugger, profiler, and PlayMode proof remain pending.

## 2026-05-19 - Unity API source and guard recheck

What was wrong:
- Fresh compile is still guarded, so the compute CBuffer patch needed another non-build API proof pass.

What was done:
- Checked local Unity 6000.4 package source. `RenderGraph.ImportBuffer(GraphicsBuffer)` and `CommandBuffer.SetComputeConstantBufferParam(ComputeShader, int, GraphicsBuffer, int, int)` exist in the installed render-pipelines core source.
- Re-ran XML parse, banned-pattern scan, continuous CBuffer/render-gate scan, compute CBuffer declaration/import/UseBuffer scan, and `git diff --check`.
- Re-ran build guard.

Cinematic Cheats used:
- No change. CPU remains scalar authority; compute/raster shaders fake the spatial glass mask and optics.

Exact Microseconds saved:
- No profiler data. No fabricated numbers.
- API source audit and docs cost 0 us/frame.

Verification:
- XML parse OK.
- Banned-pattern scan OK.
- Continuous CBuffer/render-gate scan OK.
- Compute CBuffer declaration/import/UseBuffer scan OK.
- `git diff --check` returned CRLF normalization warnings only.
- Build guard blocked compile: no dotnet/csc process, but CPU samples were 30.89, 43.80, and 53.37 percent.
- Second guard recheck also blocked compile: no dotnet/csc process, but CPU samples were 100.00, 66.22, and 47.27 percent.
- Unity import, shader compiler, Frame Debugger, profiler, and PlayMode proof remain pending.
