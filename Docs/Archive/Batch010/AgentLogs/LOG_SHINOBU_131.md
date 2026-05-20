# SHINOBU_131 Log

## 2026-05-19 Static Source Pass - Custom SH L2 Probe Grid

What was wrong:
- Unity ambient/probe authority was still conceptually split between built-in probe systems, managed `SphericalHarmonicsL2` routes, and custom interior lighting.
- `LightProbeGroup`/`LightProbeUsage` scene state is not compatible with a 100x100x10 km AUP streaming world.
- The XML DTO instruction contained a physical layout contradiction: `double3` at offset 0 plus 27 floats cannot fit in 128 bytes.
- Lighting shaft DTOs had ARM64-hostile `Pack=1` residue and direct World namespace use inside the Lighting assembly boundary.

What was done:
- Replaced the hot probe data path with `CustomLightProbeDTO`, explicit 128-byte SH L2 records, Vault-backed buffers, Burst generation/interpolation/occlusion/dynamic injection jobs, and double-buffered `GraphicsBuffer.LockBufferForWrite` upload to `_H8CustomLightProbeGrid`.
- Removed managed Unity ambient-probe writes from project presentation paths that would keep `SphericalHarmonicsL2` as a parallel authority.
- Added `AbyssalLightingTunerWindow` and `Docs/ambient_lighting_profiles.csv` for cold designer tuning without C# recompilation.
- Added byte-level ambient profile parsing into Vault rows with FNV-derived IDs and packed RGB10 biome tint consumption through `BiomeGradientSignal`.
- Converted touched Lighting shaft DTOs to explicit 64-byte layouts and rerouted AUP distance through Core double3 conversion.
- Removed the obsolete half-texture probe scratch after the direct `GraphicsBuffer<CustomLightProbeDTO>` upload path was established, eliminating one irrelevant per-probe write from propagation.
- Rewired `UpdateProbeOcclusionJob` into the active dependency chain after propagation and before telemetry; it now consumes `InteriorGIOcclusionCellDTO` directly and owns packed biome tint plus SDF darkening.
- Converted ambient profile CSV tokenization to a `ReadOnlySpan<byte>` parser backed by the Vault byte scratch.
- Added `Hecton_CustomLightProbeGrid.hlsl` so the GPU upload has a real shader-side 128-byte DTO consumer; direct project shader ambient now resolves from `_H8CustomLightProbeGrid` and no longer calls Unity `SampleSH`/`SampleSHPixel`.
- Added a fixed-buffer UI Toolkit telemetry graph for `SolverCompleteMs` in `AbyssalLightingTunerWindow`.

Cinematic cheats used:
- Dynamic bounce is nearest-8 directional SH injection, not radiosity.
- Mock probe grid uses deterministic depth/caustic gradient math, not terrain bake.
- SDF occlusion is scalar darkening/wall transfer, not rays.
- Low thermal quality collapses L2/trilinear cost into global/nearest terms.

Exact microseconds saved:
- Measured profiler proof: not available in this static pass. `dotnet build`, Unity import, Play Mode, Profiler, and Frame Debugger were not run.
- Static estimate: managed Unity probe sampling avoided at roughly 15-60 us per 1k moving entities; scene streaming probe-bake churn avoided at roughly 80-250 us per active streamed cluster. These are estimates only and must be replaced by Profiler markers after Unity runtime proof.
- Static extra saving after polish: removed up to 262,144 bytes of half-voxel scratch writes per 32^3 propagation pass at four iterations, plus one Vault buffer request. Exact frame impact pending profiler.

<SELF_AUDIT agent_id="SHINOBU_131" domain="LIGHTING_PROBE_GRID_ARCHITECT">
  <TASK_RECONCILIATION>
    <TASK id="01" name="UNITY_LIGHT_PROBE_ERADICATION" result="PASS">Static scan under Assets/_Project returns zero LightProbeGroup, m_LightProbeUsage: 1, managed probe evaluation, or managed SH writes.</TASK>
    <TASK id="02" name="MANAGED_SH_EVALUATION_PURGE" result="PASS">LightProbes.GetInterpolatedProbe, SphericalHarmonicsL2, and RenderSettings.ambientProbe are absent from the static project scan.</TASK>
    <TASK id="03" name="CS1612_ENCAPSULATION_PURGE" result="PASS">Hot DTOs use public fields; no property-backed SH coefficients are used in NativeArray lanes.</TASK>
    <TASK id="04" name="ARM64_PADDING_RECONSTRUCTION" result="PASS">CustomLightProbeDTO is explicit 128 bytes with byte offsets listed below. The requested double3+27-float 128B layout was rejected as impossible.</TASK>
    <TASK id="05" name="EMERGENCY_MOCK_PROBE_DATA" result="PASS">GenerateMockProbeGridJob seeds deterministic blue-to-black SH gradients and a caustic fake.</TASK>
    <TASK id="06" name="BURST_SH_INTERPOLATION_KERNEL" result="PASS">EvaluateProbeLightingJob is Burst, NoAlias, IJobParallelFor, AUP-relative, and quality-scaled.</TASK>
    <TASK id="07" name="SDF_OCCLUSION_BAKING" result="PASS">UpdateProbeOcclusionJob is scheduled after propagation, consumes InteriorGIOcclusionCellDTO, and darkens/tints probes from scalar SDF/wall data without rays.</TASK>
    <TASK id="08" name="THE_DEAR_LIE_DYNAMIC_BOUNCE" result="PASS">InjectDynamicLightJob adds nearest-8 directional boosts. No radiosity simulation exists.</TASK>
    <TASK id="09" name="ASYNCHRONOUS_GPU_UPLOAD_DISPATCHER" result="PASS">CustomLightProbeGpuUploadJob copies into a mapped double-buffered GraphicsBuffer, binds `_H8CustomLightProbeGrid`, and direct project shader ambient consumes the matching HLSL StructuredBuffer instead of Unity SampleSH/SampleSHPixel ambient.</TASK>
    <TASK id="10" name="CONTINUOUS_SCALABILITY_PROBE_DENSITY" result="PASS">GlobalQualityWeight drives resolution, cadence, source samples, iterations, upload cadence, and L1/L2 weights.</TASK>
    <TASK id="11" name="GLOBAL_DIRECTIONAL_FALLBACK" result="PASS">EvaluateProbeLightingJob writes GlobalFallback directly when quality collapses.</TASK>
    <TASK id="12" name="BIOME_TINT_INTEGRATION" result="PASS">BiomeGradientSignal plus ambient profile Vault rows feed PackedBiomeTint. Direct CurrentAtmosphereDTO/World reference was rejected to preserve compile-wall routing.</TASK>
    <TASK id="13" name="AUP_PRECISION_GRID_MAPPING" result="PASS">Entity/source/light positions subtract RootAup before float3 cast; touched shaft code uses double3 AUP delta.</TASK>
    <TASK id="14" name="ROLLBACK_NETCODE_STATE_FENCE" result="PASS">Probe grid state is visual-only Vault/GPU data and is not registered in rollback Merkle state.</TASK>
    <TASK id="15" name="ZERO_INIT_OVERHEAD_BYPASS" result="PASS">Vault buffers use UninitializedMemory; InteriorGIClearStateJob owns boot clear. No standalone vault fallback remains.</TASK>
    <TASK id="16" name="TELEMETRY_LIGHTING_RECORDER" result="PASS">300-entry Vault ring records quality, active probes, sources, timing, luma, NaNs, hashes; fault dump path is Docs/AgentLogs/Dump_LIGHTING_SURGEON.bin.</TASK>
    <TASK id="17" name="PROBE_TUNER_EDITOR_WINDOW" result="PASS">Abyssal Lighting Tuner UI Toolkit facade provides layout validation, sliders, mock grid, CSV reloads, blackbox dump, fixed-buffer compute-time graph, and probe scan/disable controls.</TASK>
    <TASK id="18" name="CSV_AMBIENT_PROFILES_INGESTOR" result="PASS">AmbientLightingProfileCsvParser tokenizes ReadOnlySpan&lt;byte&gt; backed by Vault scratch into AmbientLightingProfileDTO rows with no string.Split, LINQ, foreach, or runtime dictionary.</TASK>
    <TASK id="19" name="LIVE_PROBE_DEBUG_GIZMO" result="PASS">Editor gizmos evaluate fixed forward-direction SH color and draw probe spheres from Vault readback.</TASK>
    <TASK id="20" name="SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION" result="PASS_STATIC">This log, Status_SHINOBU_131.md, Rationale_SHINOBU_131.md, and the binary ledger contain the static audit. Unity compile/runtime proof remains pending.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="CustomLightProbeDTO" size_bytes="128" alignment="8/16-safe two 64-byte cache lines">
      <FIELD name="SpatialHash64" offset="0" size="8"/>
      <FIELD name="PackedGridCoord" offset="8" size="4"/>
      <FIELD name="Flags" offset="12" size="4"/>
      <FIELD name="Lane0/R0-R3" offset="16" size="16"/>
      <FIELD name="Lane1/R4-R7" offset="32" size="16"/>
      <FIELD name="Lane2/R8,G0-G2" offset="48" size="16"/>
      <FIELD name="Lane3/G3-G6" offset="64" size="16"/>
      <FIELD name="Lane4/G7-G8,B0-B1" offset="80" size="16"/>
      <FIELD name="Lane5/B2-B5" offset="96" size="16"/>
      <FIELD name="Lane6/B6-B8,Spare0" offset="112" size="16"/>
      <FIELD name="B8" offset="120" size="4"/>
      <FIELD name="Spare0" offset="124" size="4"/>
      <MATH>16-byte header + 7 * 16-byte float4 lanes = 128 bytes. This equals two 64-byte cache lines and is divisible by 16.</MATH>
    </DTO>
    <DTO name="CustomDynamicProbeLightDTO" size_bytes="64" note="AUP source record: double3 0..23, color/intensity/radius/flags/direction 24..59, pad 60..63."/>
    <DTO name="InteriorGITelemetryEntry" size_bytes="64" note="One cache line per blackbox entry."/>
    <FALSE_SHARING>Parallel jobs write probe/output rows by index, not shared counters. Touched shaft telemetry/contribution records are explicit 64-byte structs. No atomic counter DTO was introduced in this lane.</FALSE_SHARING>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE_EXPLANATION>
    GlobalQualityWeight is consumed as a continuous scalar. Resolution moves from 12^3 toward 32^3 active cells; cadence lerps from slow thermal updates toward denser visual sync; source sample limit lerps from 4 toward MaxSourceCount; propagation iterations curve from 1 toward 4; L1 starts after 0.08 and L2 after 0.54 by math.step/smooth polynomial gates. Below roughly 0.3, EvaluateProbeLightingJob fades to GlobalFallback or nearest-like sampling by shrinking frac toward zero, L2Weight stays near zero, and the shader helper blends to fallback/nearest instead of 8-read trilinear. Ultra keeps trilinear L2, richer dynamic light SH injection, and per-pixel UberNoir grid ambient.
  </SCALABILITY_CURVE_EXPLANATION>
  <H_PHI_VAULT_STATUS private_persistent_arrays="0">
    <BUFFER id="0x630800" name="ProbeFront" type="CustomLightProbeDTO"/>
    <BUFFER id="0x630801" name="ProbeBack" type="CustomLightProbeDTO"/>
    <BUFFER id="0x630802" name="ProbeSources" type="InteriorGISourceDTO"/>
    <BUFFER id="0x630803" name="ProbeOcclusion" type="InteriorGIOcclusionCellDTO"/>
    <BUFFER id="0x630804" name="ProbeTuning" type="InteriorGITuningDTO"/>
    <BUFFER id="0x630805" name="ProbeTelemetryRing" type="InteriorGITelemetryEntry"/>
    <BUFFER id="0x630806" name="ProbeTelemetryScratch" type="InteriorGITelemetryEntry"/>
    <BUFFER id="0x630808" name="ProbeMockPower" type="MockPowerState"/>
    <BUFFER id="0x630809" name="ProbeFaults" type="int"/>
    <BUFFER id="0x63080A" name="ProbeCsvBytes" type="byte"/>
    <BUFFER id="0x63080B" name="ProbeAmbientProfiles" type="AmbientLightingProfileDTO"/>
    <BUFFER id="0x63080C" name="ProbeAmbientProfileCount" type="int"/>
    <RESERVED id="0x630807" name="RetiredHalfTextureScratch" reason="Removed after direct GraphicsBuffer SH DTO upload became the only visual route."/>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NO_ALIAS>InteriorGIClearStateJob, CustomLightProbeGpuUploadJob, GenerateMockProbeGridJob, EvaluateProbeLightingJob, UpdateProbeOcclusionJob, InjectDynamicLightJob, InteriorGIPropagationJob, and InteriorGITelemetryScanJob mark separate NativeArray fields with NoAlias where applicable.</NO_ALIAS>
    <DEPENDENCIES>Tick schedules InteriorGIMockPowerJob -> N InteriorGIPropagationJob iterations -> UpdateProbeOcclusionJob -> InteriorGITelemetryScanJob. LateFrameTick completes only when the scheduled handle is completed, then swaps front/back and schedules GPU upload when dirty. The upload job copies final probe records into mapped GraphicsBuffer memory before unlock/bind.</DEPENDENCIES>
    <SHADER_ROUTE>`Hecton_CustomLightProbeGrid.hlsl` reads `_H8CustomLightProbeGrid`, `_H8InteriorGIProbeParams`, `_H8InteriorGIProbeOrigin`, `_H8InteriorGIProbeRootAup`, and `_H8CustomLightProbeGridState`; direct project shader ambient calls `H8CustomLightProbeResolveAmbient` across UberNoir, terrain, wreck, flora, kelp, coral, fauna, sargassum, tools, debris, item highlight, archive ocean residue, and indirect-lit materials.</SHADER_ROUTE>
    <BLOCKING>There is still a controlled Complete during LateFrameTick/GPU unlock because Unity's LockBufferForWrite lifetime requires CPU completion before UnlockBufferAfterWrite. This is a graphics upload boundary, not arbitrary simulation blocking.</BLOCKING>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    Hecton8.Lighting.asmdef references Hecton8.Core, Hecton8.Core.Contracts, Hecton8.Core.Memory, Burst, Collections, Mathematics, and rendering packages only. Static scan of Lighting source finds zero direct sibling-domain using statements after the shaft residue patch.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    <BEFORE complexity="O(probes * rays) or managed Unity probe sampling">Traditional LightProbeGroup baking/streaming, per-object managed interpolation, or realtime bounce would couple scene state, ray work, and main-thread APIs.</BEFORE>
    <AFTER complexity="O(probes + sources_per_probe_limited + 8 * dynamic_lights)">The custom route uses scalar SDF/wall occlusion, synthetic caustic/depth gradients, nearest-8 directional boost, and shader-side consumption of packed probe records.</AFTER>
  </DEAR_LIE_CONFIRMATION>
  <VERIFICATION>
    <STATIC_SCAN name="unity_probe_residue" result="0 offenders"/>
    <STATIC_SCAN name="lighting_direct_sibling_using" result="0 offenders"/>
    <STATIC_SCAN name="lighting_gc_hotpath_smells" result="0 offenders"/>
    <STATIC_SCAN name="lighting_pack1" result="0 offenders"/>
    <STATIC_SCAN name="lighting_burst_flags" result="0 offenders"/>
    <STATIC_SCAN name="project_shader_sample_sh" result="0 SampleSH/SampleSHPixel/raw unity_SH references under Assets/_Project shader/HLSL files; custom probe HLSL include and resolve calls present"/>
    <DIFF_CHECK result="pass with line-ending warnings only"/>
    <BUILD result="not_run_by_user_instruction"/>
    <UNITY_RUNTIME result="pending"/>
  </VERIFICATION>
</SELF_AUDIT>

## 2026-05-19 Polish Pass - Upload Fence And Grid Clear

What was wrong:
- The first GPU dispatcher implementation still scheduled `CustomLightProbeGpuUploadJob` and immediately completed, unlocked, and bound the mapped buffer in the same frame.
- That satisfied the shape of a double-buffered upload but failed the stricter Task 09 requirement: the GPU must consume a completed buffer on a subsequent frame, not a write buffer published immediately after a CPU fence.
- Resolution changes still routed through the cold boot clear method from `Tick`, so quality/resolution collapse could become a synchronous full-grid clear.

What was done:
- Replaced immediate upload publication with `_gpuUploadPending*` state. `TryStartGpuUploadIfDirty` now maps the write `GraphicsBuffer`, schedules one Burst `CustomLightProbeGpuUploadJob`, records constants and frame index, flips the write index, and returns.
- `TryPublishCompletedGpuUpload` now checks `IsCompleted`, completes only that finished upload handle, unlocks the mapped range, and binds `_H8CustomLightProbeGrid` only when `Time.frameCount` is greater than the upload frame.
- `CustomLightProbeGpuUploadJob` is now a single Burst `IJob` using `UnsafeUtility.MemCpy` over `CustomLightProbeDTO` records instead of an `IJobParallelFor` element-copy loop.
- Dynamic resolution clear now uses `InteriorGIProbeGridClearJob` through `ScheduleGridClear`, keeping the work under the normal simulation handle and `LateFrameTick` reclamation path.
- `Tick` now writes a fresh `InteriorGITuningDTO` before scheduling a resolution clear, so post-clear upload constants cannot lag behind `_activeResolution`.
- `LateFrameTick` now refuses to start a GPU copy while a simulation handle is still running; this blocks a real `_probeFront` read/write race when propagation iteration parity writes the final buffer back into front.
- Boot clear no longer calls `Complete()`. Initialization schedules `InteriorGIClearStateJob`, chains optional mock-grid generation, blocks readback while `_scheduledBootClear` is true, and publishes only after LateFrame handle reclamation.
- Editor CSV polling now exits during pending boot clear or active simulation, preventing the editor facade from writing Vault scratch/profile data while a clear job owns it.

Cinematic cheats used:
- No new physical simulation was added. The route still spends CPU only on SH payload maintenance; the visual richness remains a shader-side fake from a packed probe grid.
- Low quality keeps the old published buffer longer and sheds upload cadence; high quality can publish denser L2 records without reintroducing Unity probe sampling.

Exact microseconds saved:
- Measured profiler proof: still unavailable. `dotnet build`, Unity import, Play Mode, Profiler, and Frame Debugger were not run by explicit instruction.
- Static saving: removes an avoidable same-frame CPU upload fence and avoids one blocking full-grid clear during quality-driven resolution changes. Exact frame-time delta must be replaced by Unity Profiler/Frame Debugger data.

<SELF_AUDIT_DELTA agent_id="SHINOBU_131" pass="upload_fence_grid_clear">
  <TASK id="09" result="PASS_STATIC_TIGHTENED">Mapped GPU upload now completes/publishes only after the scheduled copy job reports completion and at least one frame has elapsed. Same-frame bind was rejected.</TASK>
  <TASK id="10" result="PASS_STATIC_TIGHTENED">Resolution changes now request a scheduled grid clear instead of executing a blocking boot clear from Tick.</TASK>
  <TASK id="10b" result="PASS_STATIC_TIGHTENED">Resolution-change tuning is written before the clear job is scheduled, preventing stale shader grid constants after a cleared upload.</TASK>
  <DEPENDENCY_GRAPH>Boot schedules clear/mock generation; Tick can schedule propagation or grid clear. LateFrameTick never starts an upload while a simulation handle is active and incomplete; it reclaims completed simulation handles, marks the visual buffer dirty, and schedules a pending GPU upload. A later LateFrameTick publishes the completed GPU buffer.</DEPENDENCY_GRAPH>
  <NO_ALIAS>InteriorGIProbeGridClearJob and CustomLightProbeGpuUploadJob use NoAlias on independent NativeArray fields.</NO_ALIAS>
  <BLOCKING_CLASSIFICATION>Remaining Complete calls are editor/manual mock generation, teardown drains, completed simulation reclamation, and post-IsCompleted GPU unlock/publish. The cold boot clear fence was removed in this pass.</BLOCKING_CLASSIFICATION>
</SELF_AUDIT_DELTA>
