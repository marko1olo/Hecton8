# LOG_SHINOBU_159

## 2026-05-19 Static Patch: Bioluminescent Material Pulse Coordinator

What was wrong:
- Flora pulse state was vulnerable to standard Unity material mutation patterns: material scalar writes, per-pass material keyword flips, and CPU-side thinking about individual plant glow.
- Global biolum state still had legacy per-instance color support, but no single authoritative 64-byte matrix DTO for the shader contract.
- Designers had no direct pulse tuner for phase/frequency/amplitude/spatial offset groups.
- The compile wall risk was high if the biolum system edited central memory IDs while other agents were mutating `H8Memory.cs`.

What was done:
- Added `BiolumPulseStateDTO` as `[StructLayout(LayoutKind.Explicit, Size = 64)]` with four `float4` rows at offsets 0/16/32/48.
- Added owner-local Vault buffer ID `(BufferID)70311` for the single pulse-state matrix without editing central `H8Memory.cs`.
- Added `InitializeBiolumPulseStateJob` and `AdvanceBiolumPhasesJob`. The advance job uses Burst deterministic float mode, clamps `DeltaTime`, wraps phase by `2*PI`, and applies darkness/predator panic scalars.
- Changed `Hecton_IndirectVegetation.shader` so `_GlobalBiolumDearLieGroups` rows are read as Phase/Frequency/Amplitude/SpatialOffset. Localized vertex/world-relative coordinates drive spatial waves; absolute double AUP never reaches the shader.
- Packed vegetation runtime LOD/draw scalars into two vectors and removed `Material.SetFloat`/direct material keyword calls from the targeted renderer path.
- Added `Abyssal Glow Tuner` controls and four live pulse boxes reading `BiolumPulseStateDTO`.
- Extended the cold CSV parser to ingest `biolum_pulse_profiles.csv` with legacy fallback.
- Telemetry now records a 300-frame blackbox for darkness, group0 phase, frequency multiplier, and compute time; NaN dumps to `Docs/AgentLogs/Dump_BIOLUM_DIRECTOR.bin`.
- Added authored vegetation pass materials under `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_HectonIndirectVegetation*.mat`.
- Removed runtime `HECTON_GPU_INDIRECT` keyword variants from forward/depth/shadow/motion vegetation shaders; indirect selection now uses `_HectonVegetationRuntimeDrawParams.w`.
- Routed GPU indirect pass buffers and runtime vectors through preallocated `MaterialPropertyBlock` instances in `RenderParams.matProps`. The renderer fail-closes GPU indirect if authored pass materials or visible-index buffers are absent instead of cloning materials.

Cinematic cheats used:
- The CPU does not solve light propagation and does not know which plant is glowing. It advances four phase rows. The shader fakes waves with local coordinate dot products and row-selected sine/interference math.
- Low quality collapses to a vertex pulse. Higher quality spends GPU ALU on fragment interference and filament shimmer. CPU cost remains O(4).

Exact microseconds saved:
- Measured profiler proof is absent because build/import was blocked by CPU gate. Static target: replacing 1,000 per-material float writes with one matrix upload typically saves tens to hundreds of microseconds on low-end desktop/mobile CPUs. For 100,000 plants, the asymptotic CPU change is O(N material/object mutation) to O(4 row mutation + one global matrix upload).

Compile/verification:
- `git diff --check` passed for the edited files; only CRLF normalization warnings were reported.
- Targeted scans show no `Material.SetFloat`, `material.SetFloat`, `sharedMaterial.SetFloat`, `EnableKeyword`, `DisableKeyword`, `SetKeyword`, `HECTON_GPU_INDIRECT`, or `new Material` inside `Assets/_Project/Scripts/VFX/Bioluminescence`, `HectonIndirectVegetationRenderer.cs`, or the edited vegetation shader family.
- `ComputeShader.SetFloat` remains in `HectonIndirectVegetationRenderer.cs`; this is not a Material API and does not instantiate materials.
- `dotnet build`/Unity compile was not launched. CPU gate read 97-100% load, with no `dotnet`/`csc` process active. AGENTS.md forbids launching build when CPU is above 50%.

<SELF_AUDIT agent_id="SHINOBU_159" domain="Bioluminescence Sync">
  <TWENTY_TASK_RECONCILIATION>
    <TASK id="01" status="[PASS]">Material.SetFloat, direct material keyword mutation, runtime indirect keyword variants, and new Material clones removed from the biolum VFX domain and targeted indirect vegetation binding. Project-wide off-domain material users were scanned and left to their owners.</TASK>
    <TASK id="02" status="[PASS]">No per-plant/coral Update emission animation was added. Global tick schedules oscillator work; the shader evaluates the forest.</TASK>
    <TASK id="03" status="[PASS]">BiolumPulseStateDTO has public fields only. No DTO getter/setter properties exist in the pulse DTO.</TASK>
    <TASK id="04" status="[PASS]">BiolumPulseStateDTO is explicit 64 bytes and guarded with UnsafeUtility size/field-offset validation.</TASK>
    <TASK id="05" status="[PASS]">GenerateMockLightingState seeds deterministic weather/darkness and pulse rows through a cold Burst job.</TASK>
    <TASK id="06" status="[PASS]">AdvanceBiolumPhasesJob advances four phase rows with deterministic Burst and modulo 2*PI.</TASK>
    <TASK id="07" status="[PASS]">Shader reads _GlobalBiolumDearLieGroups as the Dear Lie global pulse matrix.</TASK>
    <TASK id="08" status="[PASS]">Spatial waves use localized position dot products multiplied by row SpatialOffset.</TASK>
    <TASK id="09" status="[PASS]">GlobalDarknessScalar is resolved from mock weather/profile threshold and multiplied into amplitude before GPU upload.</TASK>
    <TASK id="10" status="[PASS]">VISUAL_SYNC publishes one Shader.SetGlobalMatrix for _GlobalBiolumDearLieGroups per sync pass.</TASK>
    <TASK id="11" status="[PASS]">GlobalQualityWeight is passed as _GlobalBiolumParams.y and blends vertex pulse toward richer fragment evaluation continuously.</TASK>
    <TASK id="12" status="[PASS]">Predator proximity uses local mock Vault signal and panic speed/amplitude lerp with no AI assembly reference.</TASK>
    <TASK id="13" status="[PASS]">Shader consumes localized float coordinates; CPU pulse-origin logic subtracts AUP before float math in the existing sync pulse path.</TASK>
    <TASK id="14" status="[PASS]">Pulse state uses owner-local VFX BufferID 70311 and is excluded from Merkle/StateRingBuffer gameplay truth.</TASK>
    <TASK id="15" status="[PASS]">Pulse buffer length 1 is requested from Vault with NativeArrayOptions.UninitializedMemory and then Burst-seeded.</TASK>
    <TASK id="16" status="[PASS]">300-entry blackbox records critical pulse scalars and dumps Dump_BIOLUM_DIRECTOR.bin on NaN.</TASK>
    <TASK id="17" status="[PASS]">Abyssal Glow Tuner exposes base frequency, spatial offset multiplier, darkness threshold, and predator panic speed.</TASK>
    <TASK id="18" status="[PASS]">Cold byte parser reads biolum_pulse_profiles.csv and mutates profile/pulse Vault rows.</TASK>
    <TASK id="19" status="[PASS]">Editor window draws four live pulse boxes from sin(Phase)*Amplitude.</TASK>
    <TASK id="20" status="[PASS]">Static verification covers layout, modulo phase, shader matrix contract, forbidden targeted Material APIs, and zero private persistent NativeArray fields. Runtime compile/profiler proof remains blocked by CPU gate.</TASK>
  </TWENTY_TASK_RECONCILIATION>

  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="BiolumPulseStateDTO" layout="Explicit" size_bytes="64" alignment="16-byte rows / 64-byte total">
      <FIELD name="Group1_Params" offset="0" size="16">float4: Phase, Frequency, Amplitude, SpatialOffset</FIELD>
      <FIELD name="Group2_Params" offset="16" size="16">float4: Phase, Frequency, Amplitude, SpatialOffset</FIELD>
      <FIELD name="Group3_Params" offset="32" size="16">float4: Phase, Frequency, Amplitude, SpatialOffset</FIELD>
      <FIELD name="Group4_Params" offset="48" size="16">float4: Phase, Frequency, Amplitude, SpatialOffset</FIELD>
      <MATH>4 rows * 16 bytes = 64 bytes. 64 % 16 = 0. 64 % 8 = 0. No Pack=1. No atomic counters in this DTO, so false-sharing explicit counter padding is not applicable.</MATH>
    </DTO>
  </STRUCT_LAYOUT_VERIFICATION>

  <SCALABILITY_CURVE_EXPLANATION>
    When GlobalQualityWeight drops below 0.3, the shader keeps the vertex-computed cheap sine pulse as the dominant term and the update cadence stretches through ResolveUpdateCadenceSeconds. Fragment-level interference and filament shimmer are blended down by a smooth polynomial quality curve instead of a binary hardware branch. At high/ultra quality the same matrix rows feed pixel sine, secondary-row interference, and filament shimmer; CPU work remains four rows.
  </SCALABILITY_CURVE_EXPLANATION>

  <H_PHI_VAULT_STATUS>
    <PRIVATE_PERSISTENT_ARRAYS>Zero private persistent NativeArray/NativeList/NativeHashMap fields declared for pulse truth. Runtime stores VaultBufferHandle fields and resolves NativeArray views only while locks are held.</PRIVATE_PERSISTENT_ARRAYS>
    <REQUESTED_HANDLES>
      <BUFFER id="200" name="BiolumProfileFloats"/>
      <BUFFER id="202" name="BiolumBlackBox"/>
      <BUFFER id="70300" name="BiolumGlowStates"/>
      <BUFFER id="70301" name="BiolumGlowGpuColorFront"/>
      <BUFFER id="70302" name="BiolumGlowGpuColorBack"/>
      <BUFFER id="70303" name="BiolumGlowAupOrigins"/>
      <BUFFER id="70304" name="BiolumSyncPulses"/>
      <BUFFER id="70305" name="BiolumSyncPulseAges"/>
      <BUFFER id="70306" name="BiolumMockWeatherSignal"/>
      <BUFFER id="70307" name="BiolumMockPredatorSignal"/>
      <BUFFER id="70308" name="BiolumMockDamageSignal"/>
      <BUFFER id="70309" name="BiolumSpeciesTuning"/>
      <BUFFER id="70310" name="BiolumCsvScratch"/>
      <BUFFER id="70311" name="BiolumPulseStateBufferId"/>
    </REQUESTED_HANDLES>
  </H_PHI_VAULT_STATUS>

  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <ALIASING>InitializeBiolumPulseStateJob, AdvanceBiolumPhasesJob, and BiolumVisualSyncJob mark NativeArray fields with [NoAlias] where arrays are independent. Pulse mutation uses UnsafeUtility.AsRef on the single DTO buffer.</ALIASING>
    <JOB_GRAPH>Boot mock: InitializeBiolumPulseStateJob.Schedule().Complete() is cold-only. Frame path: AdvanceBiolumPhasesJob schedules first; BiolumVisualSyncJob depends on the phase job; _stateJobHandle is registered through H8Memory.RegisterActiveJob(SystemID.Vfx, _stateJobHandle). Upload occurs in VISUAL_SYNC after completion of that registered handle.</JOB_GRAPH>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>

  <COMPILE_GUARD>
    The VFX bioluminescence assembly path does not reference sibling domain namespaces such as AI, World, Gameplay, Environment, Physics, Audio, Ecosystem, Vehicles, Habitat, or Combat. Cross-domain facts route through contracts, GlobalRegistry/SignalBus patterns, and owner-local mock Vault signals. The world vegetation renderer edit added no new assembly reference.
  </COMPILE_GUARD>

  <DEAR_LIE_CONFIRMATION>
    Before: CPU-side per-material/per-object pulse mutation is O(N materials or visible plants) and creates batching/material-state risk. After: CPU advances four rows O(4), uploads one global matrix, and the GPU fakes spatial glow waves from localized coordinates. The optical trick is phase-row sine plus coordinate dot-products and quality-weighted fragment interference, not simulated light transport.
  </DEAR_LIE_CONFIRMATION>

  <RESIDUAL_RISK>
    Unity compile/import was not run because CPU_LOAD was 97-100%. Existing off-domain Material.SetFloat and new Material users remain outside SHINOBU_159 authority. The targeted vegetation pulse path now uses authored pass materials and preallocated MPBs; BRG fallback is released/fail-closed when the GPU indirect route cannot bind.
  </RESIDUAL_RISK>
</SELF_AUDIT>

## 2026-05-19 Material Polish Addendum

What was wrong:
- The prior report still accepted cold BRG material clones and runtime keyword mutation as renderer debt. That conflicted with SHINOBU_159 Task 01 and the user mandate forbidding `new Material()` and keyword mutation in this pulse route.

What was done:
- Removed the runtime material-clone path from `HectonIndirectVegetationRenderer.cs`; `EnsureBrgMaterialClone` now only keeps authored material references and `Tick()` releases BRG fallback state if GPU indirect cannot bind.
- Added four authored material assets for forward/depth/shadow/motion vegetation passes so editor auto-assignment does not require runtime construction.
- Replaced `HECTON_GPU_INDIRECT` shader variants in forward/depth/shadow/motion vegetation shaders with the uniform `_HectonVegetationRuntimeDrawParams.w` branch.
- Added pass-local `MaterialPropertyBlock` objects for GPU indirect draw bindings, including visible-index buffers and the packed LOD/draw vectors.

Cinematic cheats used:
- No CPU light propagation and no material-per-plant pulse. Four phase rows feed shader-side waves; the draw path now uses one authored material contract and a runtime vector branch instead of variant churn.

Exact microseconds saved:
- Profiler proof still blocked by CPU gate. Static expected saving: one material clone allocation removed per pass owner on cold path; no per-frame material keyword mutation; one matrix upload remains the pulse CPU surface.

Verification:
- Targeted `rg` scan found no `Material.SetFloat`, `sharedMaterial.SetFloat`, `EnableKeyword`, `DisableKeyword`, `SetKeyword`, `HECTON_GPU_INDIRECT`, `_runtimeMaterial`, `ReleaseRuntimeMaterial`, `EnsureRuntimeMaterial`, or `new Material` in the biolum runtime/editor files, `HectonIndirectVegetationRenderer.cs`, and edited vegetation shader family.
- World-scope scan still finds off-domain `material.SetFloat` users such as `GroundPenetratingRadarRuntime`, `ResourceDistributionDirector`, and editor flora material authoring. These are not SHINOBU_159 plant pulse runtime ownership and were not edited.
- `git diff --check` passed with CRLF normalization warnings only.
- Build/import was not run: CPU_LOAD=100, compilers=none.

## 2026-05-19 Matrix-Only Polish Addendum

What was wrong:
- The previous patch removed material mutation but still left an old per-instance color synchronization route in the SHINOBU runtime. That route used `BiolumVisualSyncJob`, two Vault GPU color buffers, `GraphicsBuffer.LockBufferForWrite`, `Shader.SetGlobalBuffer`, and a shader `StructuredBuffer<uint> _BiolumGpuColorBuffer`.
- Even disabled by quality weight, that path violated the one `float4x4` pulse matrix contract and kept CPU O(N) code alive.

What was done:
- Removed `_gpuColorFrontHandle`, `_gpuColorBackHandle`, all `_BiolumGpuColorBuffer` references, the GPU color upload method, and the shader R10G10B10A2 decode/override path.
- Deleted `BiolumVisualSyncJob`; `ScheduleStateJob` now only schedules `AdvanceBiolumPhasesJob` over the single 64-byte pulse DTO.
- `_GlobalBiolumParams.w` and `_GlobalBiolumClock.w` are explicitly published as zero so the shader has no per-instance glow-weight gate to revive.
- Removed the dead scheduled GPU color count, individual glow weight, and active glow count fields from the runtime.
- Rewired the editor `Trigger Global Pulse` action to mutate one matrix row directly instead of writing to the old `SyncPulseDTO` visual job path.
- Preserved fixed-slot external `SyncPulseDTO` AUP events as constant-count row perturbations inside `AdvanceBiolumPhasesJob`; the job subtracts a local AUP reference before casting to `float3` and never loops over plants.

Cinematic cheats used:
- The system no longer computes individual plant emission on CPU. Authored flora color remains the base; spatial glow is faked in shader from `_GlobalBiolumDearLieGroups`, localized vegetation coordinates, group tint, and quality-weighted sine/interference.

Exact microseconds saved:
- Profiler proof still blocked by CPU gate. Static asymptotic saving: removed one optional `IJobParallelFor` over up to 50,000 glow records, one CPU-to-GPU color copy, and one global structured-buffer bind. Frame pulse truth is now O(4 rows + fixed pulse slots) plus one matrix upload.

Verification:
- Targeted scans returned no matches for `_BiolumGpuColorBuffer`, `BiolumGpuColor`, `_gpuColorBuffer*`, `_publishedGpuColorCount`, `TryUploadGpuColorBufferFromLockedVault`, `BiolumVisualSyncJob`, `Shader.SetGlobalBuffer`, `LockBufferForWrite`, `GraphicsBuffer`, `Material.SetFloat`, runtime keyword mutation, `HECTON_GPU_INDIRECT`, private persistent Native collections, `Pack=1`, `Time.deltaTime`, or `UnityEngine.Random` in the SHINOBU target files.
- `git diff --check` passed with CRLF normalization warnings only.
- Build/import was not run: CPU_LOAD=100, compilers=none.

<SELF_AUDIT_UPDATE agent_id="SHINOBU_159" domain="Bioluminescence Sync" pass="MatrixOnly">
  <CORRECTION>Prior log entries mentioning `BiolumGlowGpuColorFront`, `BiolumGlowGpuColorBack`, and `BiolumVisualSyncJob` are superseded by this addendum. Those are no longer requested or scheduled by the SHINOBU runtime.</CORRECTION>
  <TASK_RECONCILIATION_DELTA>
    <TASK id="07" status="[PASS]">Dear Lie shader evaluation now has no structured per-instance color buffer fallback.</TASK>
    <TASK id="10" status="[PASS]">VISUAL_SYNC has no `Shader.SetGlobalBuffer`; matrix upload is the only biolum pulse data upload.</TASK>
    <TASK id="20" status="[PASS]">Static scans prove the removed GPU color symbols are absent from the target runtime and shader.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="BiolumPulseStateDTO" layout="Explicit" size_bytes="64">
      <FIELD name="Group1_Params" offset="0" size="16"/>
      <FIELD name="Group2_Params" offset="16" size="16"/>
      <FIELD name="Group3_Params" offset="32" size="16"/>
      <FIELD name="Group4_Params" offset="48" size="16"/>
      <MATH>4 * 16 = 64; divisible by 16 and 8; no Pack=1.</MATH>
    </DTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <H_PHI_VAULT_STATUS>
    <PULSE_TRUTH>One owner-local pulse-state matrix buffer: `(BufferID)70311`, length 1, `BiolumPulseStateDTO`, 64 bytes.</PULSE_TRUTH>
    <REMOVED_BUFFERS>`BiolumGlowGpuColorFront` and `BiolumGlowGpuColorBack` are no longer requested by `BiolumPulseSyncRuntime`.</REMOVED_BUFFERS>
    <PRIVATE_PERSISTENT_ARRAYS>Zero private persistent NativeArray/NativeList/NativeHashMap fields.</PRIVATE_PERSISTENT_ARRAYS>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <ALIASING>`AdvanceBiolumPhasesJob` keeps [NoAlias] on PulseState, ProfileFloats, WeatherSignal, and PredatorSignal.</ALIASING>
    <JOB_GRAPH>Frame path consumes profile/weather/predator plus fixed-slot sync-pulse Vault buffers, schedules `AdvanceBiolumPhasesJob`, registers `_stateJobHandle`, completes in VISUAL_SYNC, copies four rows, then calls `Shader.SetGlobalMatrix`.</JOB_GRAPH>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling-domain assembly reference was added. No central `BufferID` enum edit was required for the owner-local pulse matrix.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: optional CPU O(N) individual color job plus GPU color buffer upload. After: CPU O(4 + fixed pulse slots) phase-row math and shader-side spatial wave fake from localized coordinates.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT_UPDATE>

## 2026-05-19 Fixed-Slot And Quality-Contract Addendum

What was wrong:
- `AdvanceBiolumPhasesJob` used SHINOBU's private `_activeSyncPulseCount` to clamp fixed `SyncPulseDTO` consumption. That made the fixed-slot Vault seam depend on a local producer counter and could ignore valid external pulse payloads.
- Clear-memory pulse-age slots could be counted as active telemetry even when the matching pulse payload had zero/non-finite `WaveSpeed`.
- Shared visible biolum shaders still contained stale `_GlobalBiolumParams.y` tier-index gates: `step(4.0, y)`. SHINOBU publishes a continuous 0..1 `GlobalQualityWeight`, so those High/Ultra overdrive paths were unreachable.

What was done:
- Removed `ActivePulseCount` from `AdvanceBiolumPhasesJob`.
- The matrix oscillator now scans the fixed 16 pulse slots, rejects non-finite or non-positive `WaveSpeed`, subtracts `OriginAUP - AupReference`, casts only the localized delta to `float3`, and perturbs matrix rows only.
- `AdvanceSyncPulseAges` now reads both pulse payload and age buffers, using the same pulses-then-ages lock order as existing writers, and counts only finite positive-speed live waves for blackbox telemetry.
- Replaced stale tier-index quality gates in `Hecton_CoralMaster`, `Hecton_CoralMaster_GPUI`, `Hecton_KelpMaster`, `Hecton_KelpMaster_GPUI`, `Hecton_SargassumMaster`, `Hecton_ProceduralBio`, `Hecton_LeviathanOrganic`, and `Hecton_LeviathanTentacleIndirect` with saturate plus polynomial `qualityCurve`. Overdrive, haze, and spark terms are multiplied by the continuous curve.

Cinematic cheats used:
- No CPU light propagation and no per-instance color buffer. Pulse events remain a 16-slot spatial impulse fake that bends four matrix rows; shaders fake richer detail from coordinate waves and continuous quality weight.

Exact microseconds saved:
- Static correctness/perf target remains CPU O(4 + 16 fixed pulse slots) and one matrix upload. No managed allocation added. The shader repair makes High/Ultra detail reachable without adding CPU buffers. Exact profiler proof remains blocked.

Verification:
- `rg` found no `ActivePulseCount`, `_BiolumGpuColorBuffer`, `BiolumVisualSyncJob`, `Shader.SetGlobalBuffer`, `LockBufferForWrite`, or `GraphicsBuffer` in the SHINOBU runtime/matrix shader targets.
- `rg` found no stale `step(4.0, _GlobalBiolumParams.y)` or `highTier` gate in the edited shared biolum shader set; remaining `highTier` names are unrelated fog/visor shader variables.
- Targeted material mutation scan still finds no `Material.SetFloat`, runtime keyword mutation, `HECTON_GPU_INDIRECT`, `new Material`, or biolum GPU color symbols in the SHINOBU/vegetation matrix route.
- `git diff --check` passed for the touched runtime and shader files with CRLF normalization warnings only.
- Build/import was not run: CPU_LOAD=100 and active compiler processes were `dotnet` x7.

<SELF_AUDIT_UPDATE agent_id="SHINOBU_159" domain="Bioluminescence Sync" pass="FixedSlotQuality">
  <TASK_RECONCILIATION_DELTA>
    <TASK id="11" status="[PASS]">Continuous quality now reaches shared biolum visible shaders; stale tier-index `step(4.0, y)` gates were removed from the edited contract consumers.</TASK>
    <TASK id="13" status="[PASS]">Fixed pulse slots no longer depend on a private active counter and still localize AUP before float math.</TASK>
    <TASK id="16" status="[PASS]">Wave pulse telemetry counts valid finite positive-speed payloads, not clear-memory slots.</TASK>
    <TASK id="20" status="[PASS]">Static verification added stale quality-gate scan and fixed-slot counter scan.</TASK>
  </TASK_RECONCILIATION_DELTA>
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="BiolumPulseStateDTO" layout="Explicit" size_bytes="64">
      <FIELD name="Group1_Params" offset="0" size="16"/>
      <FIELD name="Group2_Params" offset="16" size="16"/>
      <FIELD name="Group3_Params" offset="32" size="16"/>
      <FIELD name="Group4_Params" offset="48" size="16"/>
      <MATH>4 * 16 = 64; divisible by 16 and 8; no Pack=1.</MATH>
    </DTO>
    <DTO name="SyncPulseDTO" layout="Explicit" size_bytes="32">
      <FIELD name="OriginAUP" offset="0" size="24"/>
      <FIELD name="WaveSpeed" offset="24" size="4"/>
      <FIELD name="ColorOverride" offset="28" size="4"/>
      <MATH>24 + 4 + 4 = 32; divisible by 16 and 8; no Pack=1.</MATH>
    </DTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE_EXPLANATION>Low quality suppresses overdrive/haze/spark through `qualityCurve = q*q*(3-2*q)`. Middle ramps secondary interference. High/Ultra uses the same four matrix rows and scalar quality, not a separate shader variant or CPU buffer.</SCALABILITY_CURVE_EXPLANATION>
  <H_PHI_VAULT_STATUS>Pulse truth remains one owner-local `(BufferID)70311` `BiolumPulseStateDTO[1]`; fixed pulse events use `BiolumSyncPulses` and `BiolumSyncPulseAges`; zero private persistent NativeArray/NativeList/NativeHashMap fields.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>`AdvanceBiolumPhasesJob` consumes profile/weather/predator/sync-pulse buffers with [NoAlias] NativeArray fields and outputs the single pulse matrix job handle registered to `SystemID.Vfx`; no arbitrary frame-path `Complete()` was added.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling assembly dependency was added. Shader edits are contract consumers of the global biolum matrix and scalar quality.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: external pulse validity could imply a hidden producer counter and visible shader detail was blocked by a fake tier integer. After: fixed-slot impulse fake plus continuous quality-weighted shader waves from one matrix.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT_UPDATE>
