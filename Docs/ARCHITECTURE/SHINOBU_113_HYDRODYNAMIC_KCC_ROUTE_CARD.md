# SHINOBU_113 Hydrodynamic KCC Route Card

Date: 2026-05-19

Owner: SHINOBU_113 / HYDRODYNAMIC_KINEMATICS_DIRECTOR

Status: YELLOW / STATIC SOURCE WIRED / COMPILE PENDING

## Route Field Contract

Route ID: SHINOBU_113_HYDRODYNAMIC_KCC_ROUTE_CARD

Owner: SHINOBU_113 / HYDRODYNAMIC_KINEMATICS_DIRECTOR

Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.

Producer/consumer phase: producer and consumer phases documented below; hot GlobalRegistry polling is forbidden.

Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.

Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.

Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.

Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.

Review disposition: YELLOW / STATIC_SOURCE_ONLY.

## Boundary

`HydrodynamicKccRuntime` is owner-local movement-vector authority for the hydrodynamic KCC seam.

It owns only KCC state, movement commands, deferred capsule sweep commands/results, visual-local packets, wake packets, rollback bytes, tuning, CSV profile rows, and telemetry.

- It does not own canonical device input, netcode rollback orchestration, water rendering, vehicle propulsion, fauna movement, or legacy Rigidbody presentation.
- Other domains communicate through Vault buffers.
- Accepted bridges: `TryRegisterExternalInputWriter(JobHandle)`, `TryRunRollbackResimulation(...)`, `SignalBus<WakeGeneratedSignal>`.

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

| Producer phase | External input writer or mock input before simulation; KCC integration during simulation; wake/visual output during post phase |
| Consumer phase | Post-simulation rollback/state readback, visual sync, wake consumption, editor diagnostics |

| Cadence/capacity | Per simulation tick for KCC state; dirty-only for external input/wake packets; Vault-backed KCC lanes sized by runtime capacity and fixed 300-entry telemetry ring |

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

No runtime private `NativeArray`, `NativeList`, `NativeHashMap`, or managed array field owns persistent data.

The editor graph reads Vault telemetry only when runtime has no scheduled collision/post batch. It resolves one diagnostic Vault view per repaint, not per graph point.

## Input Route

External writers build `HydrodynamicKccInputDTO` through `HydrodynamicKccInputContract.BuildExternalInput(...)`, write `BufferID.ShinobuHydroKccInputs`, then call `TryRegisterExternalInputWriter(JobHandle)`.

Runtime rejects external registration while mock input is active or batch is in flight.

Sanitizer rejects stale packets by frame, sequence, source hash, finite AUP/move/look values, local AUP range, and sector-generation stamp.

If no external writer is armed, KCC clears input lane and sanitizer writes deterministic zero movement.

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

Blackbox dump paths:

- `Docs/AgentLogs/Dump_SHINOBU_113.bin`: project ID sweep path.
- `Docs/AgentLogs/Dump_KINEMATICS_SURGEON.bin`: original XML task alias.
- Runtime writes the same native telemetry span to both paths only after fault mask.

All KCC speed and distance magnitudes in jobs route through `HydrodynamicKccMath.LengthSafe`.

- Math: squared length plus guarded `math.rsqrt`, not `math.length`/sqrt.
- Applies to drag, wake, capsule sweep, collision displacement, telemetry aggregation, and visual-output speed.

## Dear Lie

CPU does not solve fluid fields.

Hydrodynamics are analytical drag, buoyancy, added mass, and scalar turbulence/wake packet.

Water visuals, caustics, silt, and camera/audio weight are downstream presentation consumers. Solver cost stays `O(n)` for KCC entities.

## Verification

Static source wiring exists. Guarded compile, Unity import, Burst Inspector, profiler, GC capture, Play Mode rollback, and scene presentation proof are pending.
