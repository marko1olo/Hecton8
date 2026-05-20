# SHINOBU_158 Buoyancy Route Card

Date: 2026-05-19
Status: PENDING VERIFICATION
Review disposition: YELLOW

Evidence class: STATIC_SOURCE / STATIC_DOC. Unity import, Unity Console, Play Mode, Burst Inspector, profiler, GCMonitor, player build, and scene wiring proof are still absent.

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM. These anchors prove only that the cited local paths exist at this capture time; they are not compile, Unity import, Play Mode, profiler, GC, player-build, save/load, platform, or visual proof.

- `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs`
- `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs`
- `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementContracts.cs`
- `Assets/_Project/Scripts/Physics/Buoyancy/PhysicsApplySystem.BuoyancyQueue.cs`
- `Assets/_Project/Scripts/Physics/Buoyancy/GlobalPhysicsStateManager.BuoyancyBridge.cs`
- `Assets/_Project/Scripts/PhysicsApplySystem.cs`
- `Assets/_Project/Scripts/Editor/Physics/HydrodynamicBuoyancyTunerWindow.cs`

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary
This route card is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the R47 root/architecture authority-spine/runtime-wording/counter-drift correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`) (R46 prior interior-authority/route-field/proof-language correction; R45 prior R43/R44 residue/proof-artifact/source-counter correction); R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R37 remains the prior artifact-path/proof-wording/source-counter correction; R36 remains the prior authority-spine/domain-map correction; R35 remains the prior R4/counter-residue correction, and R34 remains the prior source-counter and physical-line refresh, R33 remains the prior R32-residue/source-anchor correction, R32 remains the prior R4/proof-wording correction, R31 remains the prior current-boundary propagation correction, R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, and R28 remains the prior interior-boundary correction. Current static gates: AtlasCheck fails `ATLAS_CHECK_FAIL references=6781 missing=61` (one Dynamic Decals vendor asset ref, RealtimeCSG vendor icon/readme image refs, missing HectonMaskChannelPacker/HectonMaterialChannelPackValidator editor source refs, and missing HabitatDamageBakePipeline source ref in the current atlas); Mod API static validation passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only.

No Unity import, Unity Console, Play Mode, Burst Inspector, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, 1000-object soak, save/load route, scene wiring, or visual proof is implied unless this route card links a fresh evidence artifact. `YELLOW` remains the only valid disposition until runtime evidence is attached.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Route Card

Route ID: SHINOBU_158_BUOYANCY_DISPLACEMENT_SOLVER
Owner: SHINOBU_158
Owner domain: Echelon 4 Player/Kinematics/Tools - Hydrodynamic Drag & Buoyancy
Owning file/system: `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs`

First 20 Minutes moment: Swim / Hazard
Route impact: dropped route objects can be kept stable in water without per-object `FixedUpdate`, direct `Rigidbody.AddForce`, or runtime mesh volume calculation.
Proof required before GREEN: Unity import, Console, 1000-object Play Mode soak, Profiler/GC 0 B hot-path capture, Burst compile inspection, and force packet drain telemetry.

Problem: loose underwater objects need deterministic buoyancy, drag, flow drift, and sleep behavior without per-object scripts.
Why owner-local data is insufficient: force packets must cross into `PhysicsApplySystem`, and buoyancy state must be Vault-visible for replay/blackbox/editor tuning.
Why direct caller/owner interface is insufficient: Burst jobs cannot call Unity `Rigidbody` APIs; main-thread physics application is owned by `PhysicsApplySystem`.

Instrument:
  [ ] GlobalRegistry cold service/interface
  [ ] SignalBus<T> first-party broadcast
  [ ] GlobalSignals bridge/direct queue
  [ ] HectonEventBus mod/API/cold event
  [x] GlobalDataVault / IDataVault
  [x] Black-box/telemetry route
  [x] Vault force-packet window drained by PhysicsApplySystem

Producer phase: SIMULATION (`IFixedTickable.FixedTick`) schedules Burst force generation over the current strided subset, not the full active count when quality is low.
Consumer phase: POST_SIMULATION (`IPostFixedTickable.PostFixedTick`) completes the owned solver fence only when ready, then drains to `PhysicsApplySystem`. If the job completes later in `LateFrameTick`, `_forcePacketsReadyToDrain` blocks the next fixed scheduling pass until the following post-fixed drain consumes the packet window.
Cadence: fixed tick, continuously load-shed by `GlobalQualityWeight` through evaluation stride `12 -> 1`; `EvaluationOffset = SimulationFrame % stride` rotates ownership across fixed frames.
Expected max events/reads per frame: 4096 state reads/writes at stride 1, roughly `ceil(active/stride)` evaluated state rows at lower quality, up to 8192 force packet rows, drain budget 8192.
GlobalQualityWeight behavior: smooth curve drives evaluation stride, drag blend from linear to quadratic, surface snap depth, and current amplitude. Authored `GlobalQualityWeight` remains a designer cap; runtime writes `ResolvedQualityWeight` separately so thermal recovery is not sticky.

Payload/data shape:
Managed fields present: no in DTO/job payloads.
UnityEngine.Object fields present: no in DTO/job payloads.
Layout proof: `BuoyancyStateDTO` explicit 64 bytes; `BuoyancyCounterDTO` explicit 64 bytes; all runtime DTOs are multiples of 8.
Mutation proof: authoritative `BuoyancyStateDTO` writes in solver/mock jobs use `UnsafeUtility.AsRef<BuoyancyStateDTO>` over raw `NativeArray` pointers; no direct `States[index]` setter remains.
Parallel writer proof: solver `States` and `DebugForces` use `[NativeDisableParallelForRestriction]` because the scheduled work item writes `(workIndex * EvaluationStride) + EvaluationOffset`, not `workIndex`. For fixed stride and offset, the mapping is injective, so parallel writers do not collide. Mock `States` uses the same annotation for raw pointer seeding.
Precision proof: depth uses `OceanSurfaceAUP - CurrentAUP`; scheduler stamps `SectorAUP` from `HectonFloatingOrigin.CurrentTotalOffsetDouble`; fallback current uses `CurrentAUP - SectorAUP` before `float3` conversion.
Capacity: states 4096, force packets 8192, flow samples 4096, material volumes 2048, telemetry 300.
Overflow/failure: force-packet window drain is capped; overflow sets `FlagForcePacketOverflow`; unresolved packets are counted; non-finite forces are dropped and set `FlagNonFinite`. Seafloor contact plus low velocity sleeps without force-equilibrium proof; surface sleep still requires force balance.

Telemetry fields: evaluated objects, sleeping objects, packets, non-finite count, total buoyancy, drag, compute micros, quality, depth, last hash, and sanitized current-frame last net force. Evaluated/force totals are current-frame only; force packet count comes from the false-sharing-padded counter, not stale debug flags.
Black-box fields: 300-entry `BuoyancyTelemetryEntry` ring requests planned/generated-on-fault dumps to both `Docs/AgentLogs/Dump_FLUID_DYNAMICS.bin` and `Docs/AgentLogs/Dump_SHINOBU_158.bin` on NaN/non-finite detection. No existing artifact is implied unless a timestamped runtime trigger and output are linked.
Profiler marker: pending; static implementation records compute micros through `Stopwatch` only.
GC proof required: Profiler/GCMonitor 300-frame runtime capture.

Shutdown/disposal: `BuoyancyDisplacementRuntime` completes pending solver before unregister/teardown; `PhysicsApplySystem` does not own native memory for buoyancy transfer and only drains Vault-owned force-packet rows.
Scene unload behavior: runtime unregisters fixed/post-fixed/late-frame listeners and completes pending jobs before clearing Vault handles.
Stale-handle behavior: hot-swap listener completes pending work, clears handles, reacquires Vault handles, and runs one idempotent cold boot. If post-fixed cannot resolve the packet route after a completed solver, stale drain readiness is cleared to prevent permanent fixed-tick starvation. Emergency mock seeding is a fail-open fallback only when the tuning row reports zero active state rows; producer-owned active buffers are not overwritten by mock data.

Vault buffers requested at boot:
- `ShinobuBuoyancyStates = 71620`
- `ShinobuBuoyancyForcePackets = 71621`
- `ShinobuBuoyancyFlowSamples = 71622`
- `ShinobuBuoyancyTuning = 71623`
- `ShinobuBuoyancyTelemetryRing = 71624`
- `ShinobuBuoyancyTelemetryCursor = 71625`
- `ShinobuBuoyancyMaterialVolumes = 71626`
- `ShinobuBuoyancyCsvScratch = 71627`
- `ShinobuBuoyancyDebugForces = 71629`
- `ShinobuBuoyancyCounters = 71630`
- `ShinobuBuoyancyBodyBindings = 71631`

Rejected alternatives:
  [x] owner-local field: rejected because replay/editor/blackbox need Vault-visible state.
  [x] cached owner interface: rejected for force generation because Burst cannot call managed Unity physics.
  [x] existing SignalBus lane: rejected because the payload is a high-volume fixed-step force stream, not fan-out notification.
  [x] owner-local NativeQueue bridge: rejected after SHINOBU_100 audit because force packets cross a rollback-adjacent owner boundary and already have a Vault buffer contract.
  [x] existing Vault buffer: rejected because no existing buffer owns SHINOBU_158 DTO layout/capacity.
  [x] cold HectonEventBus hook: rejected because first-party hot gameplay traffic cannot use managed events.
  [ ] no global route needed

Why this does not increase global monolith risk: state remains in SHINOBU_158-owned Vault buffers; force application crosses one existing owner boundary through `PhysicsApplySystem`; no new registry slot, event bus, or sibling assembly dependency was added.
H-Phi impact expected: bounded DataVault increase, no new managed hot bus, no new GlobalRegistry service slot.
Proof required before GREEN: Unity import, Burst compile, Play Mode 1000-object soak, GC/profiler capture, force packet acceptance/unresolved counters, and shutdown leak check.
Reviewer: pending integrator
Status: PROPOSED / YELLOW
