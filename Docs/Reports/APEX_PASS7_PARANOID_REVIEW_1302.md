# APEX_PASS7_PARANOID_REVIEW_1302

- Agent: 1302
- Prompt: `Docs/Reports/PROMPT_1302_REEXTRACTED_PASS7.txt`
- Task count: 20 (`Docs/Reports/PROMPT_1302_TASK_HEADERS_PASS7.txt`)
- Domain: `Assets/_Project/Scripts/Physics`, excluding Tether/Cable/Harpoon lanes
- Build: not launched; user ordered rare builds and this pass is static/source-only

## Source Change

- `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:3462-3464`: removed added `new float2` from `PlanarSpeedSq`; scalar result is `(vx * vx) + (vz * vz)`.
- No gameplay authority change; same velocity components, same squared magnitude.

## Added-Line Managed Token Scan

- Report: `Docs/Reports/PATCH_ADDED_LINES_TOKEN_SCAN_1302_PASS7.json`
- Scanned touched files: 18
- Forbidden added tokens: 0
- Safe Unity.Mathematics false positives: 2 (`math.select`, `math.all`; not LINQ)
- Patterns: `new`, `string.Format`, `.ToString()`, `System.Linq`, LINQ member calls, interpolation, string concat, `foreach`.

## Fault Writer Scan

- Report: `Docs/Reports/STRICT_PHYSICS_FAULT_ROUTE_SCAN_1302_PASS7.json`
- Touched local fault-writer hits: 0
- Core blackbox bridge hits: 97
- Cold read/editor/data IO hits remaining: 67
- Broad non-editor/non-tether residual: 1
- Residual: `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1415` accepts a `BinaryWriter` supplied by root `GlobalPhysicsStateManager`; no local `FileMode.Create` in patched nodes.

## ARM64 DTO Map

- Report: `Docs/Reports/DTO_OFFSET_MAP_1302_PASS7_TARGETS.json`
- DTOs found: 17 / 17
- Layout violations: 0

| DTO | Source | Size | MaxFieldEnd | Fields | MultipleOf8 |
|---|---|---:|---:|---:|---|
| FluidIncursionTelemetryEntry | `Assets/_Project/Scripts/Core/Contracts/Physics/HabitatFluidIncursionContracts.cs:129` | 64 | 64 | 16 | True |
| VehicleDamageStateDTO | `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageContracts.cs:119` | 128 | 128 | 24 | True |
| VehicleDamageTelemetryEntry | `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageContracts.cs:173` | 128 | 128 | 20 | True |
| SubmarineKinematicState | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:92` | 192 | 192 | 24 | True |
| SubmarineHydrodynamicsTelemetry | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:266` | 128 | 128 | 19 | True |
| GyroTelemetryEntry | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:377` | 64 | 64 | 14 | True |
| AutopilotTelemetryEntry | `Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs:162` | 64 | 64 | 9 | True |
| ShockwaveTelemetryEntry | `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationContracts.cs:232` | 80 | 80 | 14 | True |
| ExosuitTelemetryEntry | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:107` | 64 | 64 | 9 | True |
| KinematicTelemetryEntry | `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:108` | 64 | 64 | 9 | True |
| KccEnvironmentTelemetryEntry | `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:206` | 64 | 64 | 9 | True |
| SeaglideTelemetryEntry | `Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsContracts.cs:158` | 64 | 64 | 15 | True |
| WaveMathTelemetryEntry | `Assets/_Project/Scripts/Physics/Buoyancy/AnalyticalGerstnerWaveContracts.cs:161` | 64 | 64 | 16 | True |
| ReadbackTelemetryEntry | `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackContracts.cs:111` | 64 | 64 | 16 | True |
| SleepStateTelemetryEntry | `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementContracts.cs:155` | 64 | 64 | 16 | True |
| BuoyancyTelemetryEntry | `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementContracts.cs:275` | 64 | 64 | 14 | True |
| SimdTelemetryEntry | `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancySimdVectorization.cs:38` | 64 | 64 | 14 | True |

Full per-field `[FieldOffset]` maps are in the JSON artifact. All listed sizes are multiples of 8; no `FieldOffset` bool violation is reported by the static parser.

## AUP Determinism

- Vehicle depth formula: `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs:1037` uses `double depthMeters = seaLevelAupY - rootAup.y;`, then clamps/casts after double subtraction.
- Vehicle finite guard: `VehicleComponentDamageRuntime.cs:1033` rejects non-finite `rootAup` before depth calculation.
- KCC signal path: `HydrodynamicKccRuntime.cs:3458-3464` carries `BodyAup` as `double3`; added `PlanarSpeedSq` uses sanitized local velocity, not absolute AUP float casts.

## Dependency Isolation

- Report: `Docs/Reports/DEPENDENCY_USING_AUDIT_1302_PASS7.json`
- Added using directives: 2
- Forbidden domain/System.Linq using hits: 0
- Physics asmdefs scanned: 8

| Added using source | Directive |
|---|---|
| `Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs:5` | `using Hecton8.Core.Contracts.Physics;` |
| `Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs:4` | `using Hecton8.Core.Contracts.Physics;` |

Only new references are Core contract lanes. No `.asmdef` file was modified in this pass.

## Fail-Closed Behavior

- Patched fault paths return without local dump IO unless `_coreBlackboxWarmed == true` and `GlobalTelemetryBus.BlackboxActiveFrameCount > 0`.
- Fault routing publishes fixed integer event hashes and calls Core blackbox dump; if Core is not initialized, Physics fails closed by skipping the dump path.
- NaN/AUP guards remain explicit at the spatial authority boundaries; no managed exception route was added.

## Overengineering Check

- No new solver or high-iteration simulation was added.
- KCC squared planar speed now avoids helper vector construction and uses two scalar multiplies.
- Local subsystem binary serializers were removed instead of replaced with duplicated native writers; remaining native-only dump gap belongs to Core blackbox ownership.
