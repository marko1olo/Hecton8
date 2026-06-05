# Kinematics AUP Integration

Date: 2026-05-24
Owner domain: player/physics kinematics AUP integration
Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE / STATIC_DOC

Full historical map snapshot: `../_Archive/Architecture_X_012_APEX_2026-05-24_FILE_CAP/ARCHITECTURE_APEX_PRE_FILE_CAP_KINEMATICS_AUP_INTEGRATION.md`.

## Source Anchors

- `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs`
- `Assets/_Project/Scripts/Physics/CCD/KinematicCcdMath.cs`
- `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs`
- `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs`
- `Assets/_Project/Scripts/World/AUPMath.cs`

Path visibility only. It is not Play Mode, physics stability, save/load, profiler, GC, or player-build proof.

## Scope

| Area | Current authority |
|---|---|
| player locomotion | `Rigidbody + CapsuleCollider + HectonPlayerMovement + HectonPlayerMotor` |
| hydrodynamic KCC | `HydrodynamicKccRuntime.cs` and `SHINOBU_113_HYDRODYNAMIC_KCC_ROUTE_CARD.md` |
| authoritative KCC state | 64-byte `KinematicStateDTO` in `GlobalDataVault` under `ShinobuHydroKcc*` IDs |
| input lane | owner-local `HydrodynamicKccInputDTO`; canonical device/rollback input stays in Core `InputStateDTO` |
| visual output | final EWMA local float transform presentation only |
| wake output | existing `WakeGeneratedSignal` Core contract |

## AUP Rules

- Subtract active sector/origin `double3` before any `float3` cast.
- Clamp only transient post-subtraction local delta to `+/-131072m`.
- Authoritative AUP is not altered by local float seam clamps.
- Edge length and resistance calculations subtract `double3` AUPs first.
- Origin shift and teleport routes must clear/republish presentation state, not mutate authority through visuals.

## KCC Runtime Rules

- External producers build packets through `HydrodynamicKccInputContract.BuildExternalInput(...)`.
- External input writers call `TryRegisterExternalInputWriter(JobHandle)`.
- Mock input blocks external writer registration.
- No external writer plus no mock input schedules a Burst zero-input clear job.
- Sanitizer rejects stale frame, entity sequence, source hash, sector generation, non-finite fields, and out-of-range local AUP.
- Capsule batch freezes max-hit stride before `ScheduleBatch`.
- `PostFixedTick` resolves hits with the stored stride so `GlobalQualityWeight` cannot reinterpret buffer layout mid-frame.
- Vault readiness is fail-closed for per-entity, multi-hit, rollback, telemetry, cursor, tuning, and profile lanes.

## Telemetry

- `HydrodynamicKccFaultFlagDTO`: 64-byte row per entity.
- `KinematicTelemetryEntry`: 300-frame ring.
- Dumps: `Docs/AgentLogs/Dump_SHINOBU_113.bin` and `Docs/AgentLogs/Dump_KINEMATICS_SURGEON.bin`.
- Estimated compute-use is not profiler proof.

## Rejected Routes

- Unity `CharacterController` as locomotion authority.
- Direct calls to legacy movement MonoBehaviours from new integrations.
- Hot `GlobalRegistry.DataVault` polling in fixed/post/late chain.
- Runtime ownership of movement truth through presentation transforms.

## Proof Required

Unity import, Unity Console, Play Mode traversal, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual route proof remain pending.
