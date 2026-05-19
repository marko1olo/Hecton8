# SHINOBU_65 Status - Diegetic Visor Lens

Agent: SHINOBU_65  
Prompt role: DIEGETIC_VISOR_AND_LENS_SIMULATOR  
Status date: 2026-05-19
Authority: `Docs/Tasks/CURRENT_BATCH.md` second `<AGENT_PROMPT id="SHINOBU_65">` block plus explicit user assignment `SHINOBU_DIEGETIC_VISOR_LENS`.

## [ANALYSIS] Duplicate-ID Override

`CURRENT_BATCH.md` contains two `SHINOBU_65` blocks. The user's explicit assignment names the visor/lens work, so this section is active for this turn. CLI extraction counted 20 visor tasks.
The stale wrong-domain duplicate block was removed from this active status file on 2026-05-19 so anti-amnesia reads cannot reselect the wrong assignment.

Relevant mandates selected before visor edits:

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: no managed allocations in Tick/LateFrame shader upload paths.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`: Vault-owned NativeArray state, scheduled Burst job, no arbitrary mid-frame blocking.
- `DATA_Runtime_Struct_Layout_ARM64.txt`: `VisorStateDTO` is 16 bytes, field-only, no `Pack=1`.
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`: scalar CBuffer authority plus declared RenderGraph compute mask, no `SetData` churn.
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`: reuse existing RenderGraph visor feature; load-shed refraction continuously.
- `UI_Diegetic_Physical_Interfaces.txt`: no Canvas Image dirt/fog; effect is shader/visor presentation.
- `REND_VR_Stencil_Masking.txt`: stay inside the existing visor/post feature boundary.
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`: 300-frame fixed telemetry and `Dump_VISOR_SURGEON.bin`.

## Loop V1 - Tasks 01-05

- [x] Task 01 - Binary graveyard reconnaissance.
  - DOD: CLI searched `Docs/Archive`/`StreamingAssets`; no `visor_materials_006.h8bin` found. Runtime fixed-probes Batch005-007/StreamingAssets and falls back to `GenerateEmergencyMockVisorData()`.
  - Rejected: recursive runtime archive scan or blocking boot on a missing archive payload.
  - Estimate: 0 us/frame; cold IO only.
- [x] Task 02 - Canvas overlay eradication pass.
  - DOD: new path adds no Canvas/Image/ParticleSystem and feeds existing URP `HectonVisorFluidDistortionFeature` plus shader CBuffer.
  - Rejected: `Canvas Image`, screen-space dirt UI, particle droplets.
  - Estimate: avoids UI rebuild/raycast surface; exact GPU us pending profiler.
- [x] Task 03 - CS1612 encapsulation purge.
  - DOD: `VisorStateDTO` has four public float fields; runtime mutation uses private unsafe helpers behind guarded `TryWriteState`/`TryWriteTuning` gates.
  - Rejected: DTO get/private-set properties and public ref-return escape hatches.
  - Estimate: no defensive struct property copies.
- [x] Task 04 - ARM64 padding reconstruction.
  - DOD: state is 16 bytes; GPU globals are four float4 lanes; telemetry is 64 bytes.
  - Rejected: `Pack=1`, bool-packed DTOs, unaligned mixed fields.
  - Estimate: cache-line friendly scalar upload.
- [x] Task 05 - Mock physiology signal and breathing spike job.
  - DOD: `MockPhysiologySignal` carries respiration/heart/core temp/breath spike; Burst job converts breath spikes into condensation.
  - Rejected: direct physiology runtime dependency.
  - Estimate: one 32-byte physiology buffer read/write per job.

## Loop V2 - Tasks 06-10

- [x] Task 06 - Burst condensation solver.
  - DOD: `VisorCondensationJob` increases condensation from respiration, heart rate, core temperature, cold water, and decays with `math.exp(-ClearingRate * dt)`.
  - Rejected: GPU/CPU fog particles or texture simulation.
  - Estimate: one scalar IJob; target below 0.02 ms pending profiler.
- [x] Task 07 - Dear Lie droplet kinematics.
  - DOD: camera angular velocity is converted to local float2 droplet gravity, sent in `_HectonDiegeticVisorLensParams0.xy`, and consumed by `Hecton_DiegeticVisorLens.compute` for a downscaled lens mask before raster composite.
  - Rejected: individual droplet physics and one-thread scalar-copy compute.
  - Estimate: one quaternion delta, one dirty 64-byte upload, and a quality-gated downscaled compute dispatch; exact GPU us pending profiler.
- [x] Task 08 - Pressure crack routing.
  - DOD: mock pressure and `PlayerFatalPressureSignal` drive crack severity; shader thresholds cracks with procedural ridge/noise.
  - Rejected: crack mesh decals or GameObjects.
  - Estimate: scalar crack growth only.
- [x] Task 09 - Surface emergence wash.
  - DOD: `PlayerWaterSplashSignal` and explicit `NotifySurfaceEmergence()` spike droplets to 1.0 and drain over configured seconds.
  - Rejected: splash particles on visor glass.
  - Estimate: bounded signal snapshot scan.
- [x] Task 10 - Mud and silt accumulation with wipe reset.
  - DOD: silt/dirt accumulates from mock environment and water activity; `RequestWipeVisor()` decays dirt/condensation/droplets.
  - Rejected: runtime decal layers or texture writes.
  - Estimate: scalar dirt update only.

## Loop V3 - Tasks 11-15

- [x] Task 11 - Continuous scalability visor LOD.
  - DOD: `GlobalQualityWeight` drives CPU cadence, dynamic droplet blend, compute mask blend/render scale, and refraction scale; shader disables expensive refraction through continuous scale below cutoff.
  - Rejected: hard low/high boolean quality switch as the algorithmic authority.
  - Estimate: low quality collapses to static UV/chroma and zero compute blend; high/ultra uses richer refraction and downscaled compute masks.
- [x] Task 12 - Bioluminescent/internal reflection.
  - DOD: darkness/corruption drive reflection scalar in CBuffer and shader edge reflection tint.
  - Rejected: additional reflection camera.
  - Estimate: one half3 add in shader when active.
- [x] Task 13 - AUP precision ignore.
  - DOD: new visor runtime/types contain no `double`, `double3`, or `AbsoluteUniversePosition`; only local camera-space floats.
  - Rejected: reading AUP-heavy anomaly/droplet signals.
  - Estimate: no 64-bit math in visor jobs.
- [x] Task 14 - Anomaly glitch injection.
  - DOD: `SystemGlitchSignal` and mock corruption modulate condensation/cracks/glitch scalar.
  - Rejected: direct Anomaly Director dependency.
  - Estimate: bounded signal snapshot scan.
- [x] Task 15 - Audio breach signal.
  - DOD: local unmanaged `VisorBreachSignal` is prewarmed and emitted when crack severity exceeds 0.8 with cooldown.
  - Rejected: direct audio call.
  - Estimate: one SignalBus push per breach window.

## Loop V4 - Tasks 16-20

- [x] Task 16 - Zero-init NativeArray allocation.
  - DOD: all persistent state is Vault handles requested with `NativeArrayOptions.UninitializedMemory`, then cold-cleared through `UnsafeUtility.MemClear`.
  - Rejected: private persistent NativeArray owners and per-frame clear loops.
  - Estimate: boot-only clear.
- [x] Task 17 - 300-frame visor telemetry dump.
  - DOD: `VisorLensTelemetryEntry[300]` ring and cursor use Vault IDs 71025/71026; NaN dumps `Docs/AgentLogs/Dump_VISOR_SURGEON.bin`.
  - Rejected: console-only fault reports.
  - Estimate: one 64-byte telemetry write per committed job.
- [x] Task 18 - Diegetic Visor Tuner EditorWindow.
  - DOD: `Hecton8/Visor/Diegetic Visor Tuner` edits state/tuning through guarded runtime write gates.
  - Rejected: recompiling constants or runtime Canvas preview.
  - Estimate: editor-only.
- [x] Task 19 - CSV override ingestor.
  - DOD: cold parser reads `visor_properties.csv` into Vault byte scratch and parses without `Split`/LINQ.
  - Rejected: JSON/reflection/string row arrays.
  - Estimate: cold/editor only.
- [x] Task 20 - Live lens debug preview.
  - DOD: editor preview draws condensation, droplets, cracks, and dirt procedurally from DTO/scalar state.
  - Rejected: runtime debug GameObjects or Canvas.
  - Estimate: editor-only.

## Loop V5 - Self-Audit

- [x] No Canvas UI Image or particle droplet route in touched visor files.
- [x] `VisorStateDTO` is exactly 16 bytes by layout: 4 floats.
- [x] No `VisorStateDTO` properties or get/private-set accessors.
- [x] `GlobalQualityWeight` continuously attenuates dynamic droplet/refraction paths.
- [x] Editor facade exists.
- [x] Static scan found no `double`, AUP, `Canvas`, `Image`, `ParticleSystem`, `Pack=1`, `new NativeArray`, `SetData`, or DTO private setters in the new visor runtime/types/editor path.
- [ ] CLI compile blocked by guard: latest CPU samples were 100/100/99.42, all above the 50% build threshold.

## Loop V6 - Ultra Polish Mandate

- [x] Re-read active visor XML block and binary payload ledger.
  - DOD: CLI extraction confirmed the second `SHINOBU_65` block is `DIEGETIC_VISOR_AND_LENS_SIMULATOR`; `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` contains no active visor payload, so fixed probes and mock fallback remain the correct route.
  - Rejected: trusting chat memory or the stale wrong-domain duplicate trail.
  - Estimate: 0 us/frame.
- [x] Removed runtime singleton access.
  - DOD: `public static DiegeticVisorLensRuntime Instance` and editor dependency on it are gone; the editor-only tuner uses `UnityEngine.Object.FindFirstObjectByType` outside player hot paths.
  - Rejected: preserving a classic singleton for editor convenience.
  - Estimate: no runtime lookup/ownership surface; editor-only scene query.
- [x] Moved visor GPU buffer creation to cold boot.
  - DOD: `EnsureNativeState()` now allocates the 64-byte `GraphicsBuffer` and clears GPU globals before gameplay upload; dirty uploads only republish scalar vectors when values change.
  - Rejected: first-use CBuffer allocation inside `LateFrameTick`.
  - Estimate: removes one possible first-visual-frame allocation/stutter; exact us pending profiler.
- [x] Converted inherited low-tier scalar to continuous low-tier weight.
  - DOD: `HectonVisorFluidDistortionFeature` now carries `LowTierWeight01`, derives it from `GlobalQualityWeight`, hardware fallback, stress, and visor refraction scale; shader uses `dynamicVisorWeight` and `refractionWeight`.
  - Rejected: binary `lowTier ? 1f : 0f` as the primary visor algorithm gate.
  - Estimate: below q 0.3 shader skips dynamic droplet noise/refraction and uses a static film/chroma approximation.
- [x] Completed explicit partial signal and compile-risk cleanup.
  - DOD: `VisorBreachSignal` is `partial`; `FillByteBufferFromFile()` no longer depends on mixed int/long `math.min` overloads.
  - Rejected: relying on an API overload that may not exist in the installed Unity.Mathematics package.
  - Estimate: compile safety only; 0 us/frame.
- [x] Wrote `Docs/AgentLogs/SELF_AUDIT_SHINOBU_65.xml`.
  - DOD: XML includes tasks 01-20 reconciliation, DTO offsets, scalability curve, Vault IDs, dependency graph, compile-wall status, and Dear Lie Big-O.
  - Rejected: chat-only audit.
  - Estimate: documentation-only; 0 us/frame.
- [x] Static no-build verification rerun after polish.
  - DOD: grep over touched visor/runtime/editor/feature files found no `Instance`, DTO properties, `Pack=1`, AUP/double, Canvas/Image/ParticleSystem, `SetData`, `SetFloat`, MPB, `UnityEngine.Random`, `Time.deltaTime`, `Split`, LINQ, or `new NativeArray`.
  - Rejected: launching `dotnet build` without need under explicit user order.
  - Estimate: verification-only; player cost 0 us/frame.

## Loop V7 - Telemetry Completeness

- [x] Added shader update timing to the 300-frame black box.
  - DOD: `VisorLensTelemetryEntry` stays 64 bytes and now stores `ShaderUpdateComputeTimeNs` at offset 56; `UploadGpuGlobals()` measures the scalar GPU publish path with `Stopwatch.GetTimestamp()` and patches the latest ring entry.
  - Rejected: expanding telemetry to a second cache line or claiming shader upload cost without evidence.
  - Estimate: two timestamp reads and one 64-byte ring read/write per shader upload; exact ns now recorded.
- [x] Preserved no-build guard.
  - DOD: no `dotnet build` launched; static grep and XML parse were sufficient for this patch.
  - Rejected: burning a C# compile for a localized DTO/telemetry edit.
  - Estimate: 0 compile wall cost.

## Loop V8 - Continuous CPU Cadence

- [x] Collapsed low-quality CPU simulation frequency.
  - DOD: `ResolveSimulationInterval()` maps `GlobalQualityWeight` through a smooth polynomial into 5 Hz at q=0.1 and 60 Hz at q=1.0. `Tick()` accumulates deterministic dispatcher delta and schedules `VisorCondensationJob` only when the interval expires.
  - Rejected: running the CPU visor solver every frame and only reducing shader ALU.
  - Estimate: at q=0.1, steady-state solver schedules drop from up to 60/sec to 5/sec; exact us pending profiler.
- [x] Preserved event responsiveness.
  - DOD: breath, splash, wipe, pressure, glitch, and mock injection paths set `_forceImmediateSimulation` so critical visible changes do not wait for the low-tier cadence interval.
  - Rejected: a blind low-tier throttle that delays surface wash or crack pressure feedback.
  - Estimate: event-driven one-shot schedule only.

## Loop V9 - Mutation Barrier and Dependency Discipline

- [x] Removed `Tick()` job completion.
  - DOD: `Tick()` now returns while `VisorCondensationJob` is active; nonblocking completion and GPU publish happen in `LateFrameTick`, with forced completion only on disable.
  - Rejected: calling `JobHandle.Complete()` from the simulation tick even after `IsCompleted`.
  - Estimate: prevents accidental main-thread stalls in the simulation phase; exact us pending profiler.
- [x] Added pending scalar mutation barrier.
  - DOD: public mock/breath/pressure/silt/wipe/surface APIs stage primitive pending values while a job is active and apply them only before the next schedule when Vault buffers are not job-owned.
  - Rejected: writing State/Physiology/Environment/Tuning Vault buffers while Burst reads/writes them.
  - Estimate: race prevention; steady-state cost is primitive branch checks, 0 B/frame.
- [x] Hardened editor tuner writes.
  - DOD: `DiegeticVisorTunerWindow` now uses `TryWriteState` and `TryWriteTuning`; it no longer grabs mutable refs into Vault buffers during active jobs.
  - Rejected: editor convenience ref writes that can race the runtime job.
  - Estimate: editor-only; 0 player-frame cost.
- [x] Reduced render-feature registry and layout risk.
  - DOD: visor render feature caches player/fluid service references after first successful resolve and `VisorFluidGlobalsDTO` no longer declares explicit `Pack=4`.
  - Rejected: per-pass direct registry resolution as the normal path and explicit pack metadata on a CBuffer DTO.
  - Estimate: removes normal-case service lookup churn and a layout-audit warning; exact us pending profiler.
- [x] Closed public ref escape hatch.
  - DOD: state/tuning ref-return methods are private unsafe helpers; external/editor writes use `TryWriteState`/`TryWriteTuning` and fail closed while a job is active.
  - Rejected: preserving public ref access after adding a mutation barrier.
  - Estimate: correctness hardening; 0 B/frame.
- [x] Added ping-pong constant buffers.
  - DOD: diegetic visor globals and visor fluid RenderGraph globals each use two `GraphicsBuffer.Target.Constant` buffers and alternate writes; the render feature prewarms its buffers in `Create()` and refuses hot render allocation.
  - Rejected: writing into the same CBuffer that may still be bound for GPU reads; allocating from `AddRenderPasses`/`RecordRenderGraph`.
  - Estimate: prevents CPU/GPU sync hazard; exact GPU/driver us pending profiler.
- [x] Preserved build guard.
  - DOD: latest CPU samples were 100/100/99.42; `dotnet build` was not launched. `Hecton8.Core.csproj` still lists only `HectonVisorFluidDistortionFeature.cs`, so it would not prove the new runtime/types/editor files anyway.
  - Rejected: violating the explicit no-build-until-needed guard under 100% CPU.
  - Estimate: verification-only; 0 us/frame.

## Loop V10 - Literal RenderGraph Compute Mask

- [x] Added a real diegetic visor compute kernel.
  - DOD: `Assets/_Project/Art/Shaders/Hecton_DiegeticVisorLens.compute` contains `ResolveDiegeticVisorLensMask`, consuming CPU/Burst lens scalars and writing RGBA condensation/crack/dirt/glitch mask lanes.
  - Rejected: a one-thread compute pass that only rewrites scalar constants.
  - Estimate: shifts repeated full-res fragment mask noise to a quality-gated downscaled dispatch; exact GPU us pending profiler.
- [x] Wired compute through the existing URP RenderGraph visor feature.
  - DOD: `HectonVisorFluidDistortionFeature` auto-loads the compute shader in editor, declares a transient random-write mask texture, dispatches compute with declared write access, then declares read access in the raster pass.
  - Rejected: hidden global UAV writes, persistent RT allocation inside render, and a second fullscreen composite feature.
  - Estimate: no persistent hot-path managed collection added; RenderGraph owns the transient texture.
- [x] Preserved continuous scalability.
  - DOD: compute blend and internal mask render scale are derived from `GlobalQualityWeight`, `LowTierWeight01`, and `VisualOverkill01`; q below roughly 0.3 resolves to no compute mask and static film/chroma.
  - Rejected: binary `IsLowEndHardware` dispatch switch as the core algorithm.
  - Estimate: low device keeps raster fallback; middle/high/ultra buy richer lens masks with downscaled compute.

## Loop V11 - XR-Safe Compute Mask Descriptor

- [x] Removed inherited camera texture layout from the compute mask.
  - DOD: `TryAddDiegeticLensMaskPass()` now creates `_HectonDiegeticVisorLensMask` with `new TextureDesc(maskWidth, maskHeight, dynamicResolution: false, xrReady: false)`, then explicitly sets `slices = 1`, `dimension = Tex2D`, `vrUsage = None`, clamp wrap, no dynamic scale, no mips, and UAV write access.
  - Rejected: copying `TextureDesc(sourceDesc)` from `activeColorTexture`, because XR camera targets can carry texture-array dimension/slices that do not match `RWTexture2D<float4>`.
  - Estimate: correctness hardening; no extra frame cost.
- [x] Isolated visor mask UV from XR scene sampling UV.
  - DOD: the raster shader samples `_HectonDiegeticVisorLensMaskTex` with raw fullscreen `input.screenUV` while scene/depth/color sampling continues to use `ResolveXRStereoScreenUV`.
  - Rejected: sampling a non-XR 2D transient mask with stereo-transformed camera UVs.
  - Estimate: avoids per-eye mask distortion in single-pass paths; exact XR visual proof pending Unity/Frame Debugger.
- [x] Re-ran static descriptor and forbidden-pattern checks.
  - DOD: XML parse OK, banned-pattern scan OK, descriptor check OK, and `git diff --check` reports CRLF normalization warnings only.
  - Rejected: claiming Unity/Frame Debugger proof from static checks.
  - Estimate: verification-only.

## Loop V12 - Targeted Compile Attempt

- [ ] Compile verification - blocked by unrelated project errors.
  - DOD: build guard allowed one targeted `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` after samples 18.05, 14.19, and 24.21 with no dotnet/csc process. The build failed with 26 errors outside the SHINOBU_65 visor lens patch surface: `math.reversebytes` in flora/fauna, unassigned `sanitizedWeight` in Homeostasis, missing `IndustrialLoreBitMask`, missing `AssetRecord` AUP fields in AssetLifecycleGovernor, and missing `HectonDrsRenderFeatureGate` in other visor features.
  - Rejected: fixing unrelated domain errors or running repeated build attempts into the compile wall.
  - Estimate: compile proof remains blocked; no error was reported against `HectonVisorFluidDistortionFeature.cs` in this attempt.

## Loop V13 - Scalability Event Mutation Barrier

- [x] Closed the last direct tuning Vault write during scheduled work.
  - DOD: `OnScalabilityChanged()` no longer increments `VisorLensTuningDTO.Version` directly when `VisorCondensationJob` owns the tuning buffer. It stages `_pendingTuningVersionIncrement`, forces one immediate simulation, and `Tick()` applies the version increment only after scheduled work has committed and before the next job is scheduled.
  - Rejected: calling `JobHandle.Complete()` from the scalability event or leaving a direct Vault write because it is only a version counter.
  - Estimate: steady-state cost is one boolean check before signal ingestion; removes a data-race path, no measured runtime speed claim.

## Loop V14 - Binary Low-Tier Gate Removal

- [x] Removed the last binary hardware low-tier gate in the visor RenderFeature.
  - DOD: `ResolveLowTier()` and `lowTier ? 1f : 0f` are gone. Hardware pressure is now `ResolveHardwareLowPressure01()`, a continuous curve from `SystemInfo.graphicsMemorySize` against the configured VRAM threshold. Visual overkill now multiplies continuous `GlobalQualityWeight`, thermal headroom, and designer strength only; the tier enum is telemetry, not algorithm authority.
  - Rejected: `HectonQualityTier.Low/Mx350` as an immediate shader-algorithm switch.
  - Estimate: no measured frame-time claim; prevents quality popping and keeps compute/refraction/salt/silt load shedding continuous.

## Loop V15 - Compute CBuffer and Motion Ramp

- [x] Replaced compute scalar vector-param spam with a declared CBuffer.
  - DOD: `Hecton_DiegeticVisorLens.compute` now reads `HectonDiegeticVisorLensComputeGlobals` as five float4 lanes, backed by a cold-prewarmed 80-byte ping-pong `GraphicsBuffer.Target.Constant`. The RenderGraph compute pass imports that buffer and declares `UseBuffer(..., AccessFlags.Read)` before `SetComputeConstantBufferParam`.
  - Rejected: five `SetComputeVectorParam` calls per active mask dispatch and hidden undeclared compute constants.
  - Estimate: driver/API call reduction only; exact GPU/CPU us pending Unity profiler.
- [x] Removed binary thermal motion cull.
  - DOD: the visor render state now maps local velocity squared through a smooth 12-15 m/s ramp with `Smooth01`, instead of `localVelocitySq > threshold ? 1f : 0f`.
  - Rejected: abrupt high-speed cutoff that can pop distortion on sprint/scooter transitions.
  - Estimate: negligible CPU math change; visual stability improvement, no fabricated us.
- [x] Static verification after CBuffer/motion patch.
  - DOD: XML parse OK, banned-pattern scan OK, compute CBuffer declaration/import/UseBuffer scan OK, and no binary render gate tokens remain in the touched RenderFeature.
  - Rejected: claiming Unity import or shader compiler proof from static checks.
  - Estimate: verification-only.
- [ ] Compile verification after CBuffer/motion patch blocked by guard.
  - DOD: no dotnet/csc process, but CPU samples were 99.22, 90.95, and 100 percent, above the 50 percent threshold.
  - Rejected: launching targeted `dotnet build` while the machine is saturated.
  - Estimate: no compile wall load added.

## Loop V16 - Unity API Source and Guard Recheck

- [x] Verified Unity 6000 RenderGraph/compute CBuffer API surface from local package source.
  - DOD: local `Library/PackageCache/com.unity.render-pipelines.core` source contains `RenderGraph.ImportBuffer(GraphicsBuffer)` and `CommandBuffer.SetComputeConstantBufferParam(ComputeShader, int, GraphicsBuffer, int, int)`, matching the visor compute pass calls.
  - Rejected: treating web docs or memory as API proof when package source is available locally.
  - Estimate: verification-only; player cost 0 us/frame.
- [x] Re-ran static visor verification after API-source check.
  - DOD: `SELF_AUDIT_SHINOBU_65.xml` parses, banned-pattern scan OK, continuous CBuffer/render-gate scan OK, compute CBuffer declaration/import/UseBuffer scan OK, and `git diff --check` reports CRLF normalization warnings only.
  - Rejected: claiming Unity shader import, PlayMode, Frame Debugger, or profiler proof from static scans.
  - Estimate: verification-only.
- [ ] Fresh compile verification still blocked by guard.
  - DOD: no dotnet/csc process. Guard samples first blocked at 30.89, 43.80, and 53.37 percent, then blocked again at 100.00, 66.22, and 47.27 percent.
  - Rejected: launching `dotnet build` under the explicit CPU guard.
  - Estimate: no compile wall load added.
