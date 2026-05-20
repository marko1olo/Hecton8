# SHINOBU_113 Hydrodynamic KCC Route Card

Date: 2026-05-19
Owner: SHINOBU_113 / HYDRODYNAMIC_KINEMATICS_DIRECTOR
Status: YELLOW / STATIC SOURCE WIRED / COMPILE PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Boundary

`HydrodynamicKccRuntime` is the owner-local movement-vector authority for the new hydrodynamic KCC seam. It owns only KCC state, KCC movement commands, deferred capsule sweep commands/results, visual-local presentation packets, wake packets, rollback bytes, tuning, CSV profile rows, and black-box telemetry.

It does not own canonical device input, netcode rollback orchestration, water rendering, vehicle propulsion, fauna movement, or legacy Rigidbody presentation routes. Those domains must communicate through Vault buffers, `TryRegisterExternalInputWriter(JobHandle)`, `TryRunRollbackResimulation(...)`, or `SignalBus<WakeGeneratedSignal>`.

## Current Route Review Disposition

| Field | Value |
|---|---|
| Route ID | `SHINOBU_113_HYDRODYNAMIC_KCC` |
| Review disposition | YELLOW / STATIC_SOURCE_ONLY |
| Owner | SHINOBU_113 / `HydrodynamicKccRuntime` |
| Instrument | GlobalDataVault KCC buffers, external writer `JobHandle` handoff, `SignalBus<WakeGeneratedSignal>`, rollback byte lane, and black-box dump route |
| Producer phase | External input writer or mock input before simulation; KCC integration during simulation; wake/visual output during post phase |
| Consumer phase | Post-simulation rollback/state readback, visual sync, wake consumption, and editor diagnostics |
| Consumers | Input owner, rollback owner, visual sync, wake consumers, editor diagnostics |
| Cadence | Per simulation tick for KCC state; dirty-only for external input registration and wake packets |
| Capacity | Vault-backed KCC state/input/command/hit/output lanes sized by runtime capacity; telemetry ring fixed at 300 entries |
| Overflow/failure | Reject stale/non-finite input, reject external writer while mock input or batch-in-flight is active, drain scheduled batches on abort, write fault flags before telemetry dump |
| Shutdown/disposal | `AbortScheduledBatch()` completes owned handles before clearing batch flags; Vault/SignalBus owners retain buffer and queue disposal authority |
| Fault dump target | `Docs/AgentLogs/Dump_SHINOBU_113.bin` and `Docs/AgentLogs/Dump_KINEMATICS_SURGEON.bin` are generated only after a fault mask is observed; no existing artifact is implied unless linked with runtime trigger evidence |
| Proof required before GREEN | Fresh compile/import artifact, Play Mode rollback route, profiler/GC proof, Burst/job proof, and linked output path with command, timestamp, environment, and result |

## Vault Handles

Requested at boot or capacity growth through `GlobalDataVault`:

- `ShinobuHydroKccStates`
- `ShinobuHydroKccInputs`
- `ShinobuHydroKccProposedVelocities`
- `ShinobuHydroKccCollisionCommands`
- `ShinobuHydroKccCollisionHits`
- `ShinobuHydroKccResolvedHits`
- `ShinobuHydroKccPreviousAup`
- `ShinobuHydroKccVisualOutputs`
- `ShinobuHydroKccTelemetryRing`
- `ShinobuHydroKccTelemetryCursor`
- `ShinobuHydroKccTuning`
- `ShinobuHydroKccRollbackBytes`
- `ShinobuHydroKccFaultFlags`
- `ShinobuHydroKccWakePackets`
- `ShinobuHydroKccDebugOutputs`
- `ShinobuHydroKccFluidProfiles`
- `ShinobuHydroKccFluidProfileBuckets`

No runtime private `NativeArray`, `NativeList`, `NativeHashMap`, or managed array field owns persistent data. The editor graph reads Vault telemetry only when the runtime has no scheduled collision/post batch, and resolves one diagnostic Vault view per repaint instead of resolving the ring per graph point.

## Input Route

External writers must build `HydrodynamicKccInputDTO` through `HydrodynamicKccInputContract.BuildExternalInput(...)`, write `BufferID.ShinobuHydroKccInputs`, then call `TryRegisterExternalInputWriter(JobHandle)`. The runtime rejects external registration while mock input is active or while a batch is in flight.

The sanitizer rejects stale packets by frame, sequence, nonzero source hash, finite AUP/move/look values, local AUP range, and sector-generation stamp. If no external writer is armed, the KCC clears the input lane and the sanitizer writes deterministic zero movement.

## Job Graph

Simulation phase:

`externalInputWriter/mock/clear -> SanitizeKccInputBufferJob -> HydrodynamicIntegrationJob -> BuildCapsuleCastCommandsJob -> CapsulecastCommand.ScheduleBatch`

Post phase:

`CapsulecastCommand -> ExtractCapsuleCastHitsJob -> KinematicResolutionJob -> KinematicVisualSyncJob / KinematicRollbackFenceJob / EmitWakeSignalsJob / KinematicTelemetryAggregateJob`

Abort path:

`AbortScheduledBatch()` drains hit extraction, collision, command, integration, input, and external-input handles through `DispatcherJobSwap.TryComplete(..., true)` before clearing batch flags.

## Data Proof

Primary state DTO is explicit 64 bytes:

- `AUP_Position`: offset 0, `double3`, 24 bytes
- `Velocity`: offset 24, `float3`, 12 bytes
- `AngularVelocity`: offset 36, `float3`, 12 bytes
- `Mass`: offset 48, `float`, 4 bytes
- `DragCoefficient`: offset 52, `float`, 4 bytes
- padding: offsets 56..63, 8 bytes

Total: `24 + 12 + 12 + 4 + 4 + 8 = 64`. No `Pack=1`.

False-sharing lane: `HydrodynamicKccFaultFlagDTO` is explicit 64 bytes. Each parallel worker writes only its own fault slot.

Blackbox dump paths: `Docs/AgentLogs/Dump_SHINOBU_113.bin` is the project ID sweep path. `Docs/AgentLogs/Dump_KINEMATICS_SURGEON.bin` is the original XML task alias. The runtime writes the same native telemetry span to both paths only after a fault mask is observed.

All KCC speed and distance magnitudes in jobs route through `HydrodynamicKccMath.LengthSafe`, which uses squared length plus guarded `math.rsqrt` instead of `math.length`/sqrt. This applies to drag speed, wake speed, capsule sweep distance, collision displacement distance, telemetry aggregation, and visual-output speed.

## Dear Lie

The CPU does not solve fluid fields. Hydrodynamics are analytical drag, buoyancy, added mass, and a scalar turbulence/wake packet. Water visuals, caustics, silt, and camera/audio weight are downstream presentation consumers. This keeps the solver O(n) for n KCC entities instead of O(n * grid/particle samples).

## Verification

Static source wiring exists. Guarded compile, Unity import, Burst Inspector, profiler, GC capture, Play Mode rollback, and scene presentation proof are pending.


