# SHINOBU_65 Status - Toxic Outgassing Chemistry

Agent: SHINOBU_65  
Prompt role: TOXIC_OUTGASSING_AND_CHEMISTRY_SOLVER  
Status date: 2026-05-18  
Authority: `Docs/Tasks/CURRENT_BATCH.md` first `<AGENT_PROMPT id="SHINOBU_65">` block

## [ANALYSIS] Pre-Code Lock

Task matrix has 20 tasks. The active prompt is toxic outgassing. `Docs/Actual Domains of Project.txt` lists slot 65 as Thermodynamics, which conflicts with the current batch assignment. I will keep the implementation in the atmosphere/environment data-only lane and route cross-domain effects through DataVault handles and typed SignalBus packets instead of concrete physiology, submarine, shader, flora, or voxel dependencies.

Relevant mandates selected before runtime edits:

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: no heap allocation in Tick or job scheduling hot paths.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`: Vault-owned persistent buffers, ping-pong front/back, deferred completion only in late-frame swap.
- `CORE_Weather_Abyssal_FlowField_Currents.txt`: flow-field math is presentation-first and low-cadence; no main-thread entity flow loops.
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`: SDF distances are direct scalar samples; negative values block propagation.
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`: 300-frame black box and binary dump on NaN.
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`: GlobalRegistry is cold dependency discovery only; hot paths use cached handles/scalars.
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`: grid concentration and shader caustic scalar replace particle/physics gas truth.

Compile-wall rule: no sibling-domain concrete references in hot paths; communicate by SignalBus, DataVault, and existing registry registration only.

## Loop 1 - Tasks 01-05

- [x] Task 01 - Binary graveyard reconnaissance.
  - DOD: searched Batch005-007 archives and `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`; exact `gas_toxicity_tables.h8bin` was absent, Dalton aligned payload exists as script-tool-only, so runtime calls `GenerateEmergencyMockChemistry()` and probes binaries cold only.
  - Rejected: blocking runtime on stale archive names or hydrating binary tables inside the hot path.
  - Estimate: 0 us/frame; cold probe only.
- [x] Task 02 - Poison collider eradication pass.
  - DOD: new toxic gas path contains no `SphereCollider`, trigger callback, overlap query, or Unity physics gas volume.
  - Rejected: `OnTriggerEnter`, `SphereCollider`, `Physics.OverlapSphere`.
  - Estimate: eliminates Unity physics broadphase/callback cost for poison gas; exact us saved requires profiler.
- [x] Task 03 - CS1612 encapsulation purge.
  - DOD: `ToxicitySourceDTO` is field-only, `[StructLayout(Size = 48)]`, no get/set accessors.
  - Rejected: properties on unmanaged hot structs.
  - Estimate: avoids defensive struct copies during source mutation; exact us depends on source count.
- [x] Task 04 - ARM64 grid padding/data layout.
  - DOD: concentration grid is contiguous `NativeArray<float>` via `VaultBufferHandle<float>`; DTOs are 16/32/48/64 byte sizes, no `Pack=1`.
  - Rejected: object cells, dictionaries, jagged arrays, sparse managed grids.
  - Estimate: 0 B/frame allocations; streaming grid access over 4096 or 32768 cells.
- [x] Task 05 - Mock flow field.
  - DOD: `partial struct MockFlowField` and `MockFlowFieldJob` produce deterministic arbitrary current vectors.
  - Rejected: direct dependency on a concrete sibling flow-field runtime.
  - Estimate: one parallel write over active cells at variable cadence; target under 0.1ms pending profiler.

## Loop 2 - Tasks 06-10

- [x] Task 06 - Burst chemical diffusion kernel.
  - DOD: `ToxicDiffusionJob` uses ping-pong front/back density buffers and Burst flags `CompileSynchronously`, `FloatMode.Fast`, `FloatPrecision.Standard`.
  - Rejected: Navier-Stokes, particles, managed plume arrays.
  - Estimate: O(active cells) at 5-12Hz; no per-frame heap allocations.
- [x] Task 07 - Current advection bias.
  - DOD: dominant-axis upwind sampling biases concentration by `MockFlowField.Direction` and `CurrentAdvectionMultiplier`.
  - Rejected: full vector semi-Lagrangian trace per cell on low quality.
  - Estimate: low quality bypasses neighbor work; high quality adds bounded neighbor reads.
- [x] Task 08 - Dear Lie acid caustics shader scalar.
  - DOD: telemetry max density publishes acid caustic intensity to `HectonShaderGlobalDataVaultBridge.PublishUberNoirRuntime`.
  - Rejected: CPU volumetric lighting or per-cell VFX GameObjects.
  - Estimate: one cold bridge call after job commit; GPU shader carries the visual.
- [x] Task 09 - Entity toxemia injection.
  - DOD: `EntityExposureJob` samples nearest/trilinear density by AUP and emits `ToxicityExposureSignal` plus `PhysiologyStateSignal`.
  - Rejected: trigger enter/exit toxemia.
  - Estimate: O(active tracked entities), capped at 128.
- [x] Task 10 - Corrosive hull damage signal.
  - DOD: acid corrosion accumulates for two seconds and emits `CombatDamageSignal` with toxic bit plus acid hash through the signal bus.
  - Rejected: direct submarine/hull component dependency.
  - Estimate: serial 128-entity cap; no concrete gameplay assembly call.

## Loop 3 - Tasks 11-15

- [x] Task 11 - Continuous scalability grid decimation through `GlobalQualityWeight`.
  - DOD: `GlobalQualityWeight` controls 16^3/32^3 resolution gate, tick interval, source budget, diffusion/advection blend, sampling blend, flow strength, and visual scalar.
  - Rejected: static low/high tier enum switch.
  - Estimate: at q < 0.3, neighbor diffusion collapses toward radial source approximation and nearest sampling.
- [x] Task 12 - SDF containment evaluator.
  - DOD: `MockWorldSamplerJob` writes analytic cave SDF; `ToxicDiffusionJob` zeros negative SDF cells and blocks neighbor reads.
  - Rejected: `MeshCollider`, `Physics.Raycast`, terrain object lookup.
  - Estimate: O(cells) scalar SDF, no physics queries.
- [x] Task 13 - AUP grid rebasing job.
  - DOD: `OnOriginShift` accumulates cell offsets; `RebaseGridJob` shifts density indices and drops out-of-grid gas.
  - Rejected: absolute float world positions and full grid reallocations.
  - Estimate: rebase only on origin shift; 0 us/frame normally.
- [x] Task 14 - Flora toxic absorption.
  - DOD: `MockWorldSampler` marks `PurifierKelpHash` zones and diffusion subtracts flora absorption continuously.
  - Rejected: direct flora component calls.
  - Estimate: one multiply/subtract per active cell when diffusion runs.
- [x] Task 15 - Bioluminescent reaction signal.
  - DOD: `SignalHarvestJob` emits capped `ToxicBioluminescenceSignal` where toxic density and purifier flora overlap.
  - Rejected: per-cell particle spawning.
  - Estimate: stride scales from 8 to 2 by quality; capped at 64 signals/frame.

## Loop 4 - Tasks 16-20

- [x] Task 16 - Uninitialized allocation plus `UnsafeUtility.MemClear`.
  - DOD: every Vault buffer requests `NativeArrayOptions.UninitializedMemory`; cold init and reset use `UnsafeUtility.MemClear`.
  - Rejected: `ClearMemory` allocation path assumptions and per-frame clearing loops.
  - Estimate: cold boot only; no gameplay heap arrays.
- [x] Task 17 - 300-frame telemetry ring and NaN dump.
  - DOD: `ToxicityGridTelemetryEntry[300]` ring records max density, volume, hash, resolution, source/entity counts; NaN triggers `Docs/AgentLogs/Dump_TOXIC_SURGEON.bin`.
  - Rejected: "cannot reproduce" crash reports.
  - Estimate: one scan over active cells per diffusion commit.
- [x] Task 18 - Toxic Outgassing Tuner EditorWindow.
  - DOD: `Hecton8/Atmosphere/Toxic Outgassing Tuner` edits constants through runtime ref access and reloads CSV/mock values.
  - Rejected: recompiling C# to tune base constants.
  - Estimate: editor-only; 0 us/player frame.
- [x] Task 19 - Zero-GC CSV override parser.
  - DOD: cold parser reads bytes into Vault buffer and hashes keys without `Split`, LINQ, or managed row arrays.
  - Rejected: `string.Split`, JSON, reflection config hydration.
  - Estimate: cold/editor path only.
- [x] Task 20 - Editor plume visualizer.
  - DOD: EditorWindow `OnDrawGizmos(SceneView)` draws capped wire cubes over density cells.
  - Rejected: runtime debug GameObjects or gizmo component spam.
  - Estimate: editor-only; max wire cells capped.

## Loop 5 - Strict Self-Audit

- [x] No SphereCollider or TriggerCollider gas route in the new SHINOBU files.
- [x] `ToxicitySourceDTO` layout is exactly 48 bytes by explicit size: `double3` 24 + floats/uints 16 + `ulong` pad 8.
- [x] No hot-path get/set properties on DTOs.
- [x] `GlobalQualityWeight` affects resolution, cadence, source budget, diffusion/advection math, sampling quality, flow strength, and visual scalar.
- [x] Editor facade exists and reads/writes runtime constants without recompiling.
- [x] Black-box telemetry and dump path exist.
- [x] CLI compile attempted and result recorded.

## Loop 6 - Ultra Polish Re-Audit

- [x] Removed recursive runtime archive scan.
  - DOD: `Directory.GetFiles` no longer exists in SHINOBU runtime. Archive archaeology stays as CLI/documentation evidence; runtime boot probes only fixed payload paths.
  - Rejected: managed recursive file scan from `SlowTick`.
  - Estimate: removes one cold gameplay allocation burst; 0 us/frame in steady state.
- [x] Removed Unity `Time.*` from simulation inputs.
  - DOD: gas jobs now use `_simulationFrameCounter` and `SimulationTickDelta`; completion telemetry uses `Stopwatch`.
  - Rejected: Unity `Time.frameCount` as deterministic state seed.
  - Estimate: determinism correction; performance delta negligible.
- [x] Converted ping-pong copy to ping-pong swap.
  - DOD: density front/back handles swap after completion; no `NativeArray<float>.Copy` full-grid bandwidth pass remains.
  - Rejected: copying 4096/32768 floats every diffusion commit.
  - Estimate: avoids roughly 16KB low-grid or 128KB high-grid memory copy per commit, plus the previous mirror copy.
- [x] Added `ToxicityStateDTO`.
  - DOD: field-only 32-byte aligned state DTO exists for future Vault/network copies.
  - Rejected: managed state class or property-backed state struct.
  - Estimate: no runtime cost until consumed.
- [x] Added endian-defensive binary magic probe.
  - DOD: probe checks little-endian magic and a local `ReverseBytes(uint)` big-endian fallback.
  - Rejected: assuming all future toxicity payloads are little-endian.
  - Estimate: boot-only; 0 us/frame.
- [x] Removed boot recursion hazard.
  - DOD: `_nativeReady` is set after Vault handles and MemClear, before public seed/load helpers call back into `EnsureNativeState()`.
  - Rejected: recursive initialization through `GenerateEmergencyMockChemistry()` or `TryReloadCsvOverrides()`.
  - Estimate: correctness fix; prevents boot stack overflow.
- [x] Added zero-copy Vault grid header.
  - DOD: `ToxicOutgassingGridHeaderDTO` records active ping-pong buffer id, back buffer id, state buffer id, resolution, counts, version, quality, and origin.
  - Rejected: copying front density into a stable mirror every commit.
  - Estimate: replaces 16KB/128KB density copies with one 64-byte header write.
- [x] Added per-cell state export.
  - DOD: `ToxicDiffusionJob` writes `ToxicityStateDTO` per active cell into Vault for visual/physiology consumers.
  - Rejected: managed cell snapshots or forcing other agents to infer state from private runtime fields.
  - Estimate: one 32-byte state write per active cell during diffusion cadence; no heap allocation.

## Verification Ledger

- Build attempt 1: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` succeeded after initial SHINOBU files; 9 pre-existing warnings.
- Build attempt 2: same command failed transiently on unrelated shared-workspace edits in `Assets/_Project/Scripts/LocRegistry.cs` and `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs`; no SHINOBU file errors reported.
- Build attempt 3: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` succeeded after dependency churn settled; 9 pre-existing warnings, 0 errors.
- Build attempt 4: same command timed out at 124s after the ultra-polish edits.
- Build attempt 5: same command failed on unrelated shared-workspace `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` CS0120 calls to an instance method from static context; no SHINOBU file errors were reported.
- Build attempt 6: same command failed on unrelated untracked `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs` CS0117 missing `VolcanicUpdraftVault.SafeNormalize`; no SHINOBU file errors were reported.
- Correction: current generated `.csproj` files do not yet list the new SHINOBU source files, so dotnet evidence is not sufficient for final SHINOBU compile proof until Unity/project regeneration includes them.
- Build attempt 7: intentionally not run. User explicitly ordered: do not launch dotnet build until needed. Current pass uses static source checks only.
- Unity import/Play Mode/profiler/GCMonitor: not available in this session; remains `PENDING VERIFICATION`.

## Loop 7 - No-Build Static Hardening

- [x] Added nonblocking mutation window.
  - DOD: source/entity upsert/remove returns false while a diffusion job is running and completes only if the scheduled job is already finished.
  - Rejected: mutating source/entity Vault buffers while Burst jobs read them; arbitrary main-thread blocking.
  - Estimate: correctness fix; no frame cost except a finished-job commit.
- [x] Double-buffered per-cell toxicity state.
  - DOD: `CellStateFrontBufferId`/`CellStateBackBufferId` swap with density front/back, so consumers never read the state buffer being written.
  - Rejected: single state buffer race.
  - Estimate: extra 32 bytes per active cell of Vault memory; no heap allocation.
- [x] Moved Editor facade out of runtime folder.
  - DOD: `ToxicOutgassingTunerWindow.cs` now lives under `Assets/_Project/Scripts/Editor`.
  - Rejected: editor churn owned by atmosphere runtime folder.
  - Estimate: compile-wall hygiene; no runtime cost.
- [x] Low-quality ALU collapse hardened.
  - DOD: q below detail threshold skips trig in mock flow/world jobs and skips trilinear exposure sampling.
  - Rejected: computing expensive sine/cosine/trilinear paths when their blend is zero.
  - Estimate: low-grid thermal path avoids per-cell trig and 8-tap sampling.
- [x] Endian fallback made compile-safe.
  - DOD: replaced nonexistent `math.reversebytes` call with local byte-swap helper after confirming this Unity.Mathematics package exposes `reversebits` only.
  - Rejected: relying on an API absent from the installed package.
  - Estimate: boot-only; 0 us/frame.
- [x] Cold IO fail-closed behavior.
  - DOD: CSV override and binary probe failures are caught; emergency mock chemistry remains active.
  - Rejected: boot failure from a locked/missing/malformed optional tuning payload.
  - Estimate: boot/editor only; 0 us/frame.

## Loop 8 - Dependency-Chain Correction

- [x] Removed accidental per-frame job blocking.
  - DOD: `Tick()` now accumulates dispatcher delta, attempts a nonblocking commit, and returns if the scheduled diffusion graph is still running. `JobHandle.Complete()` is only forced in `OnDisable`.
  - Rejected: calling `Complete()` from every frame before checking `IsCompleted`.
  - Estimate: prevents a main-thread stall equal to unfinished diffusion time; exact us requires profiler.
- [x] Prewarmed toxic signal lanes at boot.
  - DOD: `SignalBus<ToxicityExposureSignal>` and `SignalBus<ToxicBioluminescenceSignal>` are configured and initialized during native boot, alongside built-in physiology/combat lanes.
  - Rejected: first toxic exposure allocating a `NativeQueue<T>` during gameplay.
  - Estimate: moves SignalBus native allocation to boot; 0 B/frame steady state.
- [x] Restored built-in signal publishing semantics.
  - DOD: physiology and combat output now use `GlobalSignals.Publish(in ...)`, preserving the core sanitization/latest-signal path while custom toxic lanes stay on typed `SignalBus<T>`.
  - Rejected: bypassing core GlobalSignals for engine-owned payloads.
  - Estimate: correctness and integration hygiene; negligible CPU delta.
- [x] Self-audit correction required.
  - DOD: `LOG_SHINOBU_65.md` must now mention nonblocking job commit, SignalBus prewarm, both cell-state buffers, and the corrected byte-swap helper instead of stale `math.reversebytes` wording.
  - Rejected: leaving an audit report that describes a previous implementation.
  - Estimate: documentation integrity; 0 us/frame.

## Loop 9 - Amnesia Guard Cleanup

- [x] Removed duplicate-ID contamination from this status file.
  - DOD: `Status_SHINOBU_65.md` now contains only the toxic outgassing assignment selected by the user's active prompt.
  - Rejected: preserving a second unrelated `SHINOBU_65` task matrix in the same active status file.
  - Estimate: documentation integrity; 0 us/frame.

## Loop 10 - Telemetry and Resize Race Hardening

- [x] Corrected diffusion timing telemetry.
  - DOD: `_scheduledStartTicks` is captured before the simulation graph is scheduled; `DiffusionCompleteMs` now records schedule-to-commit latency instead of near-zero post-ready drain time.
  - Rejected: measuring only the `JobHandle.Complete()` call after `IsCompleted` is already true.
  - Estimate: documentation/black-box accuracy; runtime cost is two `Stopwatch.GetTimestamp()` calls per diffusion commit.
- [x] Guarded grid resize against active jobs.
  - DOD: resolution change now uses `TryResizeActiveGrid()` and returns without clearing Vault buffers if a scheduled graph is still active.
  - Rejected: MemClear of density/state/SDF buffers while Burst jobs may still read/write them.
  - Estimate: race prevention; no steady-state cell cost.
- [x] Re-ran no-build static hygiene scan.
  - DOD: banned hot-path markers remain absent from SHINOBU runtime/types/editor files.
  - Rejected: launching dotnet build despite the user's explicit no-build order and stale generated `.csproj` source list.
  - Estimate: verification-only; 0 us/frame.
- [x] Added Unity `.meta` assets for new scripts.
  - DOD: runtime/types/editor C# files now have stable MonoImporter GUIDs.
  - Rejected: letting Unity generate local-only GUID churn on import.
  - Estimate: asset-database integrity; 0 us/frame.

---

# SHINOBU_65 Status - Diegetic Visor Lens

Agent: SHINOBU_65  
Prompt role: DIEGETIC_VISOR_AND_LENS_SIMULATOR  
Status date: 2026-05-18  
Authority: `Docs/Tasks/CURRENT_BATCH.md` second `<AGENT_PROMPT id="SHINOBU_65">` block plus explicit user assignment `SHINOBU_DIEGETIC_VISOR_LENS`.

## [ANALYSIS] Duplicate-ID Override

`CURRENT_BATCH.md` contains two `SHINOBU_65` blocks. The user's explicit assignment names the visor/lens work, so this section is active for this turn. CLI extraction counted 20 visor tasks.

Relevant mandates selected before visor edits:

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: no managed allocations in Tick/LateFrame shader upload paths.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`: Vault-owned NativeArray state, scheduled Burst job, no arbitrary mid-frame blocking.
- `DATA_Runtime_Struct_Layout_ARM64.txt`: `VisorStateDTO` is 16 bytes, field-only, no `Pack=1`.
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`: one scalar CBuffer upload path, no `SetData` churn.
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
  - DOD: `VisorStateDTO` has four public float fields and runtime exposes `ref` accessors.
  - Rejected: DTO get/private-set properties.
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
  - DOD: camera angular velocity is converted to local float2 droplet gravity and sent in `_HectonDiegeticVisorLensParams0.xy`.
  - Rejected: individual droplet physics.
  - Estimate: one quaternion delta and float2 upload; no particle cost.
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
  - DOD: `GlobalQualityWeight` drives dynamic droplet blend and refraction scale; shader disables expensive refraction through continuous scale below cutoff.
  - Rejected: hard low/high boolean quality switch as the algorithmic authority.
  - Estimate: low quality collapses to static UV/chroma; high/ultra uses richer refraction.
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
  - DOD: `Hecton8/Visor/Diegetic Visor Tuner` edits state/tuning via runtime ref access.
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
- [ ] CLI compile blocked by guard: no dotnet/csc process was found, but CPU samples were 93.17%, 63.80%, and 86.43%, all above the 50% build threshold.

## Loop V6 - Ultra Polish Mandate

- [x] Re-read active visor XML block and binary payload ledger.
  - DOD: CLI extraction confirmed the second `SHINOBU_65` block is `DIEGETIC_VISOR_AND_LENS_SIMULATOR`; `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` contains no active visor payload, so fixed probes and mock fallback remain the correct route.
  - Rejected: trusting chat memory or the stale duplicate-ID toxic trail.
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
- [x] Preserved build guard.
  - DOD: `dotnet`/`csc` process scan found no compiler, but CPU samples were 100/100/100; `dotnet build` was not launched. `Hecton8.Core.csproj` still lists only `HectonVisorFluidDistortionFeature.cs`, so it would not prove the new runtime/types/editor files anyway.
  - Rejected: violating the explicit no-build-until-needed guard under 100% CPU.
  - Estimate: verification-only; 0 us/frame.
