# Status: OCEAN_CHEMISTRY_ENGINEER

Domain: ENVIRONMENT_ENGINEER / Environment.Fluids  
Task Count: 19  
Prompt Source: Docs/Tasks/CURRENT_BATCH.md  
Current State: PENDING VERIFICATION  
Last Prompt Extract: 2026-05-13

## Mandates Loaded

- OPT_Zero_GC_Policy_AllocFree_Mandate: no managed allocation in hot paths; no LINQ/new strings inside ticks/jobs.
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First: render/audio brine as deterministic fakes before real simulation.
- OPT_Performance_Budgets_FrameTime_VRAM_Limits: MX350 target; any 0.1 ms addition is suspect.
- OPT_Native_Memory_Collections_JobSystem_Protocol: NativeArray ownership, disposal, no hidden job sync.
- MATH_Coordinate_Precision_AUP_FloatingOrigin: absolute brine heights; runtime evaluation subtracts floating-origin offset.
- ARCH_Global_Registry_ServiceLocator_DI_Init: GlobalRegistry/EventBus only for cross-domain coupling.
- DBG_Telemetry_Crash_Reporting_PostMortem: fixed black-box state, no "unknown crash" reports.
- REND_Shader_Noir_Aesthetics_Dithering_Fog: fog/depth/caustic lies over simulated volume.

## Task Loop 1: Tasks 1-5

- [x] 1. SINGLETON ERADICATION: Purge BrineManager.Instance.
  - DOD: `rg` scan found no `BrineManager` or `BrineManager.Instance` symbols under `Assets/_Project`; no singleton path remains in first-party brine code.
  - Rejected: creating a replacement manager; GlobalRegistry/typed signal path is the only cross-domain route.
  - Estimate: 0.00 us runtime delta.
- [x] 2. SIGNAL MIGRATION: Player entry into brine emits FluidDensityChangedSignal.
  - DOD: added fixed-size `FluidDensityChangedSignal` NativeQueue lane and player transition publish on brine enter/exit.
  - Rejected: `OnTriggerEnter` callbacks and string event ids; transition is math-plane state plus typed signal.
  - Estimate: 0.6 us on transition frames only, 0.00 us on steady frames.
- [x] 3. ASMDEF ISOLATION: Hecton8.Environment.Fluids -> Contracts.
  - DOD: added `Hecton8.Environment.Fluids.Contracts` and `Hecton8.Environment.Fluids`; Core references both.
  - Rejected: placing brine structs directly in Core; that would preserve architectural rot and circular ownership.
  - Estimate: 0.00 us runtime delta.
- [x] 4. DEAD CODE HUNT: Eradicate OnTriggerEnter from all Brine Pool prefabs.
  - DOD: `rg` found no `BrineTrigger` or brine prefab `OnTriggerEnter`; no YAML touched.
  - Rejected: blind prefab/YAML mutation; no matching brine prefab target existed.
  - Estimate: 0.00 us runtime delta.
- [x] 5. BRINE PLANE S.O.A.: Define NativeArray<float> BrineHeights mapped to 50x50m sectors.
  - DOD: `HectonFluidEngine` now owns `_brineHeights`, `_brineDensityMultipliers`, `_brineCartographySectors`, `_brineFlags`; samples store absolute height and 50m sector hash.
  - Rejected: per-object managed lookups inside Burst; gather phase writes SOA once, job reads scalar arrays.
  - Estimate: 0.04 us/object gather, <0.01 us/object Burst branch.
- Compile Check: Unity refresh requested; editor readiness timed out after 60s and MCP console session was unavailable. `dotnet build Hecton8.Core.csproj` failed on pre-existing/generated-project reference gaps plus new asmdef-not-yet-generated references; status remains PENDING VERIFICATION, not green.

## Task Loop 2: Tasks 6-10

- [x] 6. BUOYANCY OVERRIDE: HectonFluidEngine density multiplier 3.0 below brine height.
  - DOD: Burst job reads absolute `_brineHeights`, subtracts shift Y, multiplies resolved density by `3.0f` below plane, and clamps brine lift to 9g.
  - Rejected: unbounded Archimedes force; density multiplier without cap can produce infinite-feeling acceleration.
  - Estimate: <0.01 us/object Burst branch, paid only for lanes with valid brine flags.
- [x] 7. KCC MOVEMENT PENALTY: density multiplier reduces swim speed by 40%.
  - DOD: player brine state applies `0.6f` swim speed multiplier and feeds density multiplier into the existing Burst drag scalar.
  - Rejected: changing rigidbody mass or suit data; runtime multiplier preserves authored control curves.
  - Estimate: <0.02 us/fixed tick.
- [x] 8. DEPTH PLANE SHADER: global _BrineHeightY and _BrineColor, no physical mesh render dependency.
  - DOD: player publishes `_BrineHeightY`, `_BrineColor`, `_BrineFogHardClip`; brine pool generator no longer adds MeshFilter/MeshRenderer/fog mesh.
  - Rejected: rendering generated pool meshes; shader plane is cheaper and matches prompt.
  - Estimate: removes one or more brine mesh draw calls per generated pool; CPU savings scene-dependent.
- [x] 9. POST-PROCESS FOG: HectonVisorUberPost applies green/yellow brine fog below plane.
  - DOD: visor post reconstructs world position from scene depth and applies brine color below `_BrineHeightY`.
  - Rejected: CPU matrix upload or physical fog volumes; URP depth reconstruction already exists on GPU.
  - Estimate: one depth sample plus matrix reconstruction per pixel in visor post; no CPU allocation.
- [x] 10. CAUSTICS ABSORPTION: caustics disabled below brine plane.
  - DOD: CoreLit projected caustics return zero when `_BrineColor.a` is active and `positionWS.y < _BrineHeightY`.
  - Rejected: per-light state or extra caustic masks; one global plane branch is deterministic.
  - Estimate: saves procedural caustic math under brine; branch cost negligible.
- Compile Check: PENDING.

## Task Loop 3: Tasks 11-14

- [x] 11. AUDIO MUFFLE: camera brine submersion applies heavy low-pass.
  - DOD: `PlayerCriticalProceduralAudioRenderer` reads `HectonPlayerMovement.IsInsideBrineLayer` and raises the existing abyssal low-pass target to heavy brine mix.
  - Rejected: adding or allocating a new `AudioLowPassFilter`; existing DSP low-pass lane is already stable and allocation-free.
  - Estimate: <0.01 us/audio tick, one scalar max against existing depth low-pass.
- [x] 12. AUP SHIFT SAFETY: brine heights absolute; runtime checks subtract ShiftOffset.y.
  - DOD: `BrineLayerMath.ResolveRuntimeHeightY` subtracts `HectonFloatingOrigin.CurrentTotalOffset.y`; player and heavy-brine sink checks use `IsRuntimeBelowAbsolutePlane`.
  - Rejected: storing runtime-height sectors; runtime values corrupt under floating-origin shifts.
  - Estimate: one scalar subtract per sample, <0.01 us/check.
- [x] 13. TOXICITY LINK: submerged brine injects +10 CO2 equivalent pressure into GasDynamicsSolver local room.
  - DOD: player brine submersion calls `IGasDynamicsSolver.TryApplyPlayerRoomCarbonDioxideEquivalentPressure`, which floors the active room CO2 at standard +10 kPa without stacking per tick.
  - Rejected: additive per-frame CO2 injection; it would explode room pressure and make exposure frame-rate dependent.
  - Estimate: <0.04 us/fixed tick while submerged.
- [x] 14. MATH LOD: Low Tier post fog uses hard clipping plane, not soft fade.
  - DOD: low tier publishes `_BrineFogHardClip = 1`; `HectonVisorUberPost` switches to a hard plane fog path.
  - Rejected: low-tier soft depth fade; it spends GPU ALU on a visual the mandate says must be a hard clip.
  - Estimate: shader branch removes soft fade multiply/saturate relevance on low tier; CPU delta 0 us.
- Compile Check: Unity refresh requested after Loop 3; editor readiness timed out after 60s and MCP console returned `no_unity_session`. Local structural reads verified patched player/audio/gas call sites; status remains PENDING VERIFICATION.

## Task Loop 4: Tasks 15-18

- [x] 15. ZERO-GC: height checks mathematically evaluated in Burst; 0 bytes allocated.
  - DOD: brine height truth is scalar math in `BrineLayerMath`; `HectonFluidEngine` jobs read NativeArray SOA lanes, player/submarine/fauna checks use structs and existing services only.
  - Rejected: LINQ, managed event lists, trigger volumes, and per-frame component lookups beyond GlobalRegistry scalar service access.
  - Estimate: 0 bytes allocated in hot brine checks; <0.01 us/check in Burst lanes.
- [x] 16. TELEMETRY: write BrineSubmersionTime to Blackbox.
  - DOD: `SubmarineFluidDynamics` tracks `_brineSubmersionTime`, writes `BrineSubmersionTime` into `HydroBlackBoxEntry`, hashes it, and mirrors dumps to `Docs/AgentLogs/Dump_OCEAN_CHEMISTRY_ENGINEER.bin`.
  - Rejected: a separate managed telemetry list; existing 300-frame fixed NativeArray blackbox is the mandated crash source.
  - Estimate: one float write per blackbox sample; cold dump-only file IO.
- [x] 17. EVENT BUS: emit AcousticPingSignal(ThickFluid) when hull breaches brine layer.
  - DOD: submarine center-of-mass brine transition publishes `AcousticPingSignal` with `AcousticThickFluidChannel`.
  - Rejected: direct audio coupling or per-frame ping spam; only state transitions emit.
  - Estimate: 0 us steady state, <0.6 us on transition enqueue.
- [x] 18. CROSS-DOMAIN AUDIT: Fauna pathfinding treats Brine sectors as high-cost nodes.
  - DOD: `FaunaSensorSuite.TrySampleClosedNavGridCell` treats runtime positions below a sampled brine plane as closed/high-cost before voxel navgrid sampling.
  - Rejected: editing fauna pathfinder internals or inventing a new cost map; brine plane is a cross-domain avoidance input.
  - Estimate: one scalar brine sample/check per existing nav obstacle probe.
- Compile Check: Unity refresh requested after Loop 4; editor readiness timed out after 60s and MCP console returned `no_unity_session`. Status remains PENDING VERIFICATION.

## Task Loop 5: Task 19 + Re-Verification

- [x] 19. OMEGA COMPILE CHECK: verify shader uses world-space Y correctly without allocating matrices.
  - DOD: `HectonVisorUberPost` reconstructs world position with `ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP)` and compares `worldPosition.y` against `_BrineHeightY`; CPU only publishes scalar/vector globals.
  - Rejected: CPU-side matrix allocation/upload for brine fog; URP already exposes inverse VP to the shader.
  - Estimate: 0 bytes CPU allocation; one GPU depth sample/world reconstruction in the existing post pass.
- [x] Re-read prompt and re-check buoyancy math for infinite acceleration.
  - DOD: prompt re-extracted; `HectonFluidEngine` brine branch clamps buoyancy to `mass * gravity * 9f`, and submarine exterior buoyancy remains bounded by its existing force clamp multiplied by brine density.
- [x] Polish Mandate parsed only after every task is checked or blocked.
  - DOD: `<POLISH_MANDATE id="OMEGA_POLISH">` parsed after Tasks 1-19 were checked; scoped static purge found no `foreach`, string formatting, `.ToString()`, `math.sqrt`, `math.normalize`, or CPU matrix uploads in touched brine files. One bloat item was fixed: brine color global writes are now alpha-cached instead of pushed every fixed tick.
- Final Compile Check: PENDING VERIFICATION. Unity refresh timed out after 60s and console returned `no_unity_session`. `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal -p:UseSharedCompilation=false` failed with 113 generated-project reference errors, including unresolved sibling asmdefs (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Physics.CCD`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `Hecton8.Inventory.*`). Scoped static polish audit passed after the color-cache edit.
