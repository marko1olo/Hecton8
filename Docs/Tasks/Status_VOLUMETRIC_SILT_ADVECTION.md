# Status: VOLUMETRIC_SILT_ADVECTION

PROMPT IDENTIFIED: VOLUMETRIC_SILT_ADVECTION | DOMAIN: VFX/COMPUTE | TASK COUNT: 18

Mandates read before coding:
- REND_VFX_Fluid_Aesthetics_Compute_Particles.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- MATH_AUP_Determinism_Sync.txt
- ARCH_Execution_Phases.txt
- ARCH_Signal_Lane_Segregation.txt

Phase ownership record:
- Phase: PRE_SIMULATION for wake snapshot ingestion, VISUAL_SYNC for GPU buffer upload/dispatch/render.
- Owner assembly/domain: VFX/COMPUTE.
- DataVault/GPU buffers read: existing FluidEngine `_DynamicWakes` / `_DynamicWakeVectors` GPU ring via `TryGetDynamicWakeGpuPayload`.
- DataVault/GPU buffers written: `MarineSnowWakeJobResult` and `MarineSnowTelemetryRing` are GlobalDataVault-owned; vehicle wake enters existing `FluidImpulseSignal` -> FluidEngine ring path.
- SignalBus lanes consumed: `VehicleCommandSignal(Throttle)` through `VehicleCommandSignalBus`.
- SignalBus lanes published: `FluidImpulseSignal`; `ActiveSiltCount` telemetry via `GlobalTelemetryBus`.
- MX350 budget target: CPU <= 35 us; GPU <= 0.1 ms suspicious threshold until profiler proof.
- Load-shed fallback: low-tier 8,000 particles, no 3D noise, no SDF collision, dispatch cap under stress.

Checklist:
- [x] 1. PURGE_SINGLETONS | DOD: reused GlobalRegistry/SignalBus surfaces; no new singleton or direct vehicle singleton was introduced | Alternative rejected: direct scene lookup cache as authority | Estimate: 5 us saved from no per-frame discovery
- [x] 2. DEBT_CLEANUP | DOD: marine snow/silt owner and shader contain no Unity `ParticleSystem`; repo-wide ParticleSystem hits are unrelated bubbles/debris/world sediment outside this prompt | Alternative rejected: standard Unity ParticleSystem for marine snow silt | Estimate: 10 us CPU avoided per visible burst baseline
- [x] 3. DATA_EVICTION | DOD: `HectonFluidEngine.TryGetDynamicWakeGpuPayload` exposes existing `_DynamicWakes`/`_DynamicWakeVectors` ring to VFX compute | Alternative rejected: local duplicate wake buffer owned by renderer | Estimate: 20 us CPU/GPU sync avoided
- [x] 4. BURST_ALGORITHM | DOD: `BuildVehicleWakeSignalJob` stages throttle, AUP position, velocity, radius, lifetime; publishes `FluidImpulseSignal` through existing global signal lane | Alternative rejected: hard reference to submarine movement component | Estimate: 30 us CPU isolation cost avoided
- [x] 5. AUP_INTEGRITY | DOD: `_AupShiftOffset` is accumulated on origin shift and applied inside compute to live particles; allocation-time seeding moved to `InitializeParticles` GPU kernel | Alternative rejected: CPU position rebase/upload loop | Estimate: 8 us plus PCIe stall avoided
- [x] 6. DOD_SOA_LAYOUT | DOD: compute/render shader buffers are `StructuredBuffer<SiltParticle>` / `RWStructuredBuffer<SiltParticle>` with pos/life/vel packed in the 64B GPU struct | Alternative rejected: CPU GameObject/AoS particle transforms | Estimate: 12 us CPU transform churn avoided
- [x] 7. SIGNAL_FLOW | DOD: renderer implements `IVehicleCommandSignalListener` and registers with `VehicleCommandSignalBus` | Alternative rejected: string event name or direct vehicle poll | Estimate: 12 us dispatch/poll overhead avoided
- [x] 8. LOW_TIER_FAKE | DOD: MX350 budget is 8,000 marine-snow particles; low-tier wake path uses radial vector only and skips 3D flow/curl lookup | Alternative rejected: 100k particles plus 3D noise on MX350 | Estimate: 15 us GPU/CPU coordination avoided; larger GPU win pending capture
- [x] 9. HIGH_END_OVERKILL | DOD: high/ultra budget is 100,000 particles; shader reads `_AbyssalFlowFieldTexture` and adds curl-noise advection only on high-tier flow | Alternative rejected: always-on high workload | Estimate: 45 us saved on lower tiers by gating texture/curl path
- [x] 10. REACTIVE_VFX | DOD: shader samples global headlight cone/range and stores emission boost in `SiltParticle.Pad.y`; render/motion-vector passes consume it | Alternative rejected: CPU per-particle light cone tests | Estimate: 20 us CPU loop avoided
- [x] 11. STP_STABILIZATION | DOD: render path uses `Graphics.RenderMeshIndirect` quad draw and shader has URP `MotionVectors` pass | Alternative rejected: CPU mesh particle updates | Estimate: 25 us CPU avoided
- [x] 12. NAN_VACCINATION | DOD: compute clamps velocity through `ClampParticleVelocity` using `_MarineSnowVelocityParams.x` / `maxSiltSpeed`, with finite guard | Alternative rejected: blind normalize/divide | Estimate: 5 us avoided recovery cost per fault
- [x] 13. BLACKBOX_LOGGING | DOD: 300-frame `MarineSnowTelemetryEntry` circular buffer is leased from `GlobalDataVault` and writes `ActiveSiltCount`; non-finite state dumps to `Docs/AgentLogs/Dump_VOLUMETRIC_SILT_ADVECTION.bin` | Alternative rejected: `Debug.Log` hot-path spam and renderer-owned persistent `NativeArray` storage | Estimate: 10 us avoided managed logging cost
- [x] 14. TRIPLE_STRIKE_REPAIR | DOD: all particle kernels use `THREAD_GROUP_SIZE = 64` and `[numthreads(THREAD_GROUP_SIZE,1,1)]` | Alternative rejected: mismatched 8/32/128 particle groups | Estimate: 10 us scheduling waste avoided
- [x] 15. HOMEOSTASIS_ADAPTATION | DOD: `ResolveSystemStress01() > 0.8` forces low-tier active count and low scalability params | Alternative rejected: stable max dispatch during overload | Estimate: 5 us CPU plus major GPU dispatch reduction
- [x] 16. SDF_COLLISION_SKIP | DOD: marine snow/silt cannot enter SDF/depth collision path; only bubble/debris collision remains | Alternative rejected: floor/SDF collision for presentation-only silt | Estimate: saved SDF/depth ALU per silt particle
- [x] 17. WRAP_AROUND | DOD: particles beyond shell or 50m camera distance use `WrapParticleAroundCameraShell` instead of destroy/respawn | Alternative rejected: destroy/respawn churn and visible pop | Estimate: 8 us churn avoided
- [BLOCKED BY DEPENDENCY] 18. FINAL_VALIDATION | DOD attempted: Unity batchmode, DX12, and Vulkan runs were executed; all stop at pre-existing C# compile wall before shader/API validation, with no touched VFX file named in logs | Alternative rejected: fake pass claim | Estimate: 0 us

Loop log:
- Loop 0: Prompt extracted, domain checked, mandates read. Code not touched yet.
- Loop 1: Tasks 1-5 implemented and verified. Filtered `dotnet build Hecton8.Core.csproj --no-dependencies` returned no errors mentioning touched C# files; `git diff --check` clean except line-ending warnings; marine snow path contains no `ParticleSystem`.
- Loop 2: Tasks 6-10 implemented and statically verified. `rg` confirmed `SiltParticle`, `VehicleCommandSignal`, 8,000/100,000 tier caps, 3D texture/curl high path, and headlight boost. Compile attempt timed out and spawned dotnet workers; workers were killed. Strict compile verification remains open for final validation.
- Loop 3: Tasks 11-17 implemented and read back. Static checks confirmed indirect rendering, motion vector pass, speed clamp, 300-entry blackbox, 64-thread particle kernels, stress gate, silt collision skip, and 50m wrap.
- Loop 4: Final validation attempted with Unity default, DX12, and Vulkan batchmode. All failed before shader/API compile due unrelated project errors in Audio/Physics/Editor assemblies; no errors reference touched VFX files.
- Loop 5: Omega anti-bloat pass executed. `rg` found no `GameObject.Find`/`FindObjectOfType` in touched files, no `distance()` in marine-snow compute/render shaders, and no new direct construction dependency. Build-green status remains blocked by unrelated project compile errors.
- Loop 6: Multiplatform/H-Phi inquisition executed after user escalation. Renderer-owned wake/telemetry `NativeArray` storage was evicted to `GlobalDataVault` BufferIDs 213/214; renderer now holds vault handles only and invalidates on compaction. ARM/Quest ABI was tightened with `Pack = 1`/explicit `Size` on marine-snow CPU/GPU structs and `VfxComputeParticleBudget`. Static scans found no persistent local native arrays, no `Update`/`LateUpdate`/`FixedUpdate`, no `string.Format`, and no scene discovery in the touched marine-snow files. Shader group sizes remain 64/64/1 and no compute/render `distance()` calls exist. `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies` still fails on unrelated Lockstep/SubmarineFluid/Ecosystem errors; filtered log contains no `HectonMarineSnowRenderer.cs`, `VfxComputeParticleBudgetCatalog.cs`, or `H8Memory.cs` errors.
