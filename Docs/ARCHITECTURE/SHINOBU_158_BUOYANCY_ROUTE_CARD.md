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
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
## R48 Exact Route Field Normalization

Route ID: SHINOBU_158_BUOYANCY_ROUTE_CARD
Owner: SHINOBU_158
Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.
Producer/consumer phase: producer and consumer phases documented below; hot GlobalRegistry polling is forbidden.
Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.
Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.
Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.
Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.
Review disposition: YELLOW / STATIC_SOURCE_ONLY.

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
