# Status_SHINOBU_64_VOLCANIC_UPDRAFT

Agent: SHINOBU_64
Role: THERMAL_UPDRAFT_AND_VOLCANIC_DIRECTOR
Domain: Thermal Updrafts / Volcanic Geysers
Task Count: 20
Status: DISPATCHER FIXED-PIPELINE POLISH APPLIED; SHINOBU volcanic sources are statically clean. Fresh Core compile is currently deferred by CPU/build guard.

## Duplicate-ID Collision
- `Docs/Tasks/CURRENT_BATCH.md` contains two `SHINOBU_64` prompts: volcanic updrafts and rollback netcode.
- `Docs/Tasks/Status_SHINOBU_64.md` and `Docs/AgentLogs/Rationale_SHINOBU_64.md` are being overwritten by the rollback lane.
- This mirror preserves the volcanic audit without deleting concurrent netcode status.

## Checklist Delta
- [x] Tasks 01-20 remain implemented for the volcanic prompt: binary fallback/mock vents, no WindZone/ConstantForce, 64-byte DTOs, mock submarine proof, Burst cylinder solver, eruption oscillator, heat/blindness fake, debris chimney, leviathan thermal riding, GlobalQualityWeight debris culling, acoustic roar, AUP-local math, thermodynamics heat bridge, 0.1x vertical drag, uninitialized vent buffer, 300-frame telemetry, editor tuner, CSV parser, and gizmo cylinders.
- [x] Legacy `ThermalGeyser` Unity-physics path removed. It no longer contains `OverlapSphere`, `UnityEngine.Physics`, `PhysicsForceRouter`, `Rigidbody`, `ForceMode`, `WindZone`, or `ConstantForce`.
- [x] `ThermalGeyser` now submits authored cave geysers to `VolcanicUpdraftDirector.TryUpsertAuthoredVent()` with AUP, radius, thrust capacity, height, heat scalar, and phase.
- [x] `CurrentVolume` remains only as an authoring marker for cave geysers; vertical flow strength is stamped to `0f` to avoid duplicate physical truth.
- [x] `VolcanicUpdraftDirector` no longer reads `GlobalRegistry.ThermodynamicsService` from the `LateFrameTick` signal publication chain. Thermodynamics is cached during cold enable and rebound through `IGlobalRegistryHotSwapRefListener`.
- [x] `ThermalGeyser` no longer reads `VolcanicUpdraftDirector.ActiveRuntimeInstance` from its fixed-tick publish path. It caches the director in `Awake`, `OnEnable`, `Start`, and `Configure`, then fixed tick uses the cached pointer only.
- [x] Compile-wall audit rechecked `VolcanicUpdraftDirector` imports. The only live leviathan force path still consumes `Hecton8.AI.Cognition` DTOs because the owner buffer is registered with those exact types and the existing `Hecton8.Core.asmdef` already references `Hecton8.AI.Cognition`; no new asmdef edge was added by this polish pass.
- [x] Task 11 recheck applied: `ResolveDebrisLiftWeight()` and turbulence gating now use explicit `math.step(0.3f, q)` multiplied by the polynomial smooth curve. When debris lift weight is zero, the mock debris path skips the vent loop entirely instead of running cylinder/cone intersections and multiplying the result by zero.
- [x] Dispatcher recheck applied: `VolcanicUpdraftDirector` now registers as `IDispatcherFixedSystem`, returns the combined fixed simulation `JobHandle`, consumes submarine read handles through `JobHandle.CombineDependencies`, and leaves fixed-batch completion to the master dispatcher bridge. The only local `.Complete()` remains the cold `OnDisable()` teardown guard to avoid unlocking buffers while a job is still live.
- [x] SHINOBU static scans are clean for forbidden Unity force paths, hot LINQ/foreach/string.Format, `Pack=1`, hot `{ get; set; }`, and hot NativeArray allocation.
- [DEFERRED BY CPU GUARD] Fresh Core build was not launched after dispatcher polish on 2026-05-19 because guard sampled `CPU=100,100,99.2`; no compiler process was active, but CPU remains above the project threshold.

## Current Evidence
- `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs`: deterministic Vault/Burst updraft truth.
- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs`: direct submarine velocity/force hook.
- `Assets/_Project/Scripts/ThermalGeyser.cs`: legacy cave geyser bridge, no Unity physics force path.
- `Assets/_Project/Scripts/Editor/VolcanicUpdraftTunerWindow.cs`: human editor facade and gizmo cylinders.
