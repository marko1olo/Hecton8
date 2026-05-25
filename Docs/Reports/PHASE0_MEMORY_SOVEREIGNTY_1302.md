# Phase 0 Memory Sovereignty Report - Agent 1302

Date: 2026-05-25
Agent: 1302 / MEMORY_SOVEREIGN_PHYSICS_HYDRO_EXORCIST
Domain: `Assets/_Project/Scripts/Physics`, excluding `Tether`, `Tethers`, `Cable`, `Cable132`, and `HarpoonTension` lanes.
Prompt path requested by batch: `Assets/Project/Scripts/Physics`
Actual path on disk: `Assets/_Project/Scripts/Physics`

## Executive Finding

Scoped Phase 0 result: zero in-domain persistent `NativeArray`, `NativeList`, `NativeHashMap`, or `NativeQueue` field aliases require migration.

Raw physics scan found one forbidden persistent native alias in `Assets/_Project/Scripts/Physics/VerletCableDTOs.cs` (`VerletCableNodeBuffer.Nodes`). That file is cable/tether ownership and excluded from agent 1302 by the prompt boundary.

Primary machine artifacts:
- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1302_RAW.json`
- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1302.json`
- `Docs/Reports/VAULT_EXORCISM_REPORT_1302.json`

## Task 02 - Ownership Provenance And Lifecycle Mapping

Forbidden in-domain alias list: empty.

Existing scoped physics owners already store persistent state as `VaultGenerationHandle<T>` descriptors and resolve transient `NativeArray<T>` views at phase use sites:

| Owner | System owner | Persistent state pattern | Lifecycle / consumer notes |
| --- | --- | --- | --- |
| `BuoyancyDisplacementRuntime` | `SystemID.Physics` | `VaultGenerationHandle<T>` fields for states, force packets, flow samples, tuning, telemetry, sleep SDF, SIMD buffers | Fixed/post/late phase owner. Editor views use read handles; SIMD and sleep lanes already vault-backed. |
| `AsyncBuoyancyReadbackRuntime` | `SystemID.Physics` | request/result/tuning/telemetry handles | Async readback ring and resolved height data are vault-routed; editor views are read-only surfaces. |
| `AnalyticalGerstnerWaveRuntime` | `SystemID.Physics` | spectrum/request/result/macro grid/telemetry handles | Wave sampling is batch/job routed; no persistent native collection field found. |
| `HydrodynamicKccRuntime` | `SystemID.Physics` | KCC states, inputs, previous AUP, visual outputs, rollback bytes, hits, profiles, environment SDF/flow, telemetry handles | Fixed KCC producer. Consumers read DTO snapshots, `SignalBus<WakeGeneratedSignal>`, and editor read views. |
| `ExosuitKinematicsRuntime` | `SystemID.Physics` | state/input/tuning/mock SDF/flow/output/screen/telemetry/signal handles | Uses vault write acquisition helpers; no class-level native alias found. |
| `AbyssalCavitationRuntime` | `SystemID.Physics` | event/counter/entity/force/tuning/telemetry vault handles; graphics buffers for visuals | SignalBus lanes for acoustic/wake. GraphicsBuffer ownership is renderer-side, not GlobalDataVault persistent collection alias. |
| `SeaglideHydrodynamicsRuntime` | `SystemID.VehiclesPhysics` | state/request/force/flow/tuning/telemetry/counter/binding/visual/audio/cavitation handles | Vehicle hydrodynamic producer; signals bridge propulsion/audio/VFX. |
| `SubmarineDynamicsRuntime` and gyro partial | `SystemID.VehiclesPhysics` | kinematic/control/PID/mass/force/telemetry/added-mass/gyro handles | Vehicle physics owner. Jobs receive transient views and pointers derived from vault buffers inside phase windows. |
| `VehicleComponentDamageRuntime` | `SystemID.VehiclesPhysics` | grid/signal/state/tuning/telemetry/config handles | Damage grid owner; consumes combat signals and publishes hazard signals. |
| `HabitatFluidIncursionDirector` | `SystemID.Fluid` | compartment/integrity/graph/waterline/mass/tuning/telemetry/queue/remainder/summary handles | Fluid owner inside physics tree. Publishes flood/acoustic signals and writes waterline GraphicsBuffers for rendering. |

No 1302-owned migration target was identified, so no new `BufferID` assignments are required in Phase 0.

## Task 03 - Dependency Graph Impact Analysis

Migration blast radius is zero for the scoped hit list because there are no in-domain persistent native aliases.

Existing dependency surfaces observed:
- Editor/read APIs return DTO copies or read-only vault views.
- Hot broadcast uses `SignalBus<T>` lanes for wake, propulsion, acoustic, flood, hazard, and damage signals.
- Renderer upload paths use GraphicsBuffer staging where present; they do not expose mutable physics native arrays as public persistent fields.
- Job signatures already pass resolved `NativeArray<T>` views or unsafe pointers as transient execution data, not stored class fields.

Rejected migration action: changing public read APIs or lock windows without an offender would create race risk and source churn with no memory-safety gain.

## Task 04 - DTO Layout Extraction And Verification

DTOs stored in forbidden in-domain arrays: none.

Representative scoped DTO files already use explicit layout and 8-byte-compatible sizes:
- `Buoyancy/AnalyticalGerstnerWaveContracts.cs`
- `Buoyancy/BuoyancyDisplacementContracts.cs`
- `Buoyancy/AsyncReadback/AsyncBuoyancyReadbackContracts.cs`
- `Buoyancy/BuoyancySimdVectorization.cs`
- `KCC/HydrodynamicKccRuntime.cs`
- `KCC/HectonKccRuntime_SmokeTest.cs`
- `Exosuit/ExosuitKinematicsContracts.cs`
- `Cavitation/AbyssalCavitationContracts.cs`
- `Seaglide/SeaglideHydrodynamicsContracts.cs`
- `Vehicles/SubmarineDynamicsContracts.cs`
- `Vehicles/SubmarineBallastBuoyancyContracts.cs`
- `Vehicles/VehicleComponentDamageContracts.cs`
- `GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`

No `LayoutKind.Sequential` conversion plan is attached because the Phase 0 offender set is empty. Rewriting unrelated DTOs would violate the domain evidence rule.

## Task 05 - Telemetry Ring Integration Planning

No new runtime telemetry ring was implemented in Phase 0 because no source migration occurred.

If a future in-domain offender appears, the telemetry entry must be explicit, unmanaged, 64 bytes, and written through the owning system's existing telemetry route:

```csharp
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct PhysicsMemoryTelemetryEntry
{
    [FieldOffset(0)]  public double3 AupPosition;
    [FieldOffset(24)] public uint Frame;
    [FieldOffset(28)] public uint BufferId;
    [FieldOffset(32)] public uint SystemId;
    [FieldOffset(36)] public uint Generation;
    [FieldOffset(40)] public uint EventFlags;
    [FieldOffset(44)] public uint ExpectedCapacity;
    [FieldOffset(48)] public uint ActualCapacity;
    [FieldOffset(52)] public float ComputeMicroseconds;
    [FieldOffset(56)] public uint StateHash;
    [FieldOffset(60)] public uint FailureCode;
}
```

Ring capacity: 300 entries.
Dump path if migration code is introduced later: `Docs/AgentLogs/Dump_1302_Physics.bin`.

Exact BufferID policy: do not mint a global `BufferID` for a non-existent migration. For concrete subsystem migrations, use that subsystem's existing telemetry ring owner and `SystemID` route. If a cross-subsystem memory auditor is later required, reserve a first-party `BufferID` through a route card before implementation.

## Self Audit

Loop 1: Parsed assignment and mandate set. Rejected chat-only task inference.
Loop 2: Verified actual project path and domain exclusions. Rejected creating missing `Assets/Project`.
Loop 3: Ran Roslyn AST scan and generated raw/scoped ledgers. Rejected regex-only evidence.
Loop 4: Reviewed owner handles, public read surfaces, signal lanes, and graphics upload paths. Rejected no-op source rewrites.
Loop 5: Reviewed DTO explicit layout coverage and telemetry plan. Rejected adding unused global BufferIDs.

Build status: not launched. No C# source files were changed in Phase 0, and project policy forbids unnecessary `dotnet` rebuilds under active system load.
