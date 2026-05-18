# LOG_SHINOBU_37

## 2026-05-18 - Physics Culling And LOD Overseer

Status: PENDING UNITY RUNTIME VERIFICATION

What was wrong:
- Existing global physics culling did not have the mandated 40-byte DTO lane, spatial hash candidate window, changed-index queue, frozen velocity preservation, frustum rejection, editor tuner, CSV override, or task-specific blackbox dump.
- Far-body sleep used velocity dampening instead of the requested Dear Lie freeze/restore.
- Main-thread result handling still had broad scan behavior and old dump naming.

What was done:
- Added `PhysicsCullingDTO` explicit 40-byte layout and vault lanes for DTOs, frozen velocities, state ages, candidate indices, candidate masks, telemetry, tuning, mock seismic signals, and wake mirrors.
- Added `NativeParallelMultiHashMap<int,int>` 50m spatial hash and 9-cell camera candidate window.
- Added `PhysicsDistanceCullingJobShinobu37` Burst kernel: AUP-relative distance, frustum planes, hardware radius sq scale, hysteresis, exemption bit, kinematic/mesh commands, and changed-index queue.
- Added collider disable/restore for distance sleep and removed obsolete velocity dampening.
- Added targeted 16-byte wake payload, existing global wake signal routing, and mock seismic shockwave wake job.
- Added 300-frame `PhysicsCullingFrameTelemetry` and `Docs/AgentLogs/Dump_PHYSICS_CULLING.bin` dump on state-sync >1ms.
- Added `Physics Culling Tuner` EditorWindow, vault sliders, CSV byte-span parser, and SceneView gizmo X-Ray.

Cinematic cheats used:
- Offscreen falling bodies are frozen, colliders disabled, and solver work removed. On wake, stored velocity is restored.
- Low/MX350 radius sq scale is 0.25. It trades far-body physics truth for frame budget.
- Frustum culling uses dot-plane rejection and a 20m+ inner sphere instead of raycasts.

Struct layout:
- `PhysicsCullingDTO` total 40 bytes: 0 `double3 AUP`, 24 `int InstanceId`, 28 `float ActivationRadiusSq`, 32 `byte IsAsleep`, 33-35 padding, 36 `uint _pad3/CullingFlags`.
- `FrozenVelocityDTO` total 32 bytes: 0 `float3 LinearVelocity`, 12 `float3 AngularVelocity`, 24 `byte HasVelocity`, 25-27 padding, 28 `uint`.
- `PhysicsCullingTargetWakeRequestSignal` total 16 bytes: 0 `uint TargetInstanceId`, 4 `float3 ImpulseVector`.

Exact microseconds saved:
- Runtime profiler was not run; exact savings are not claimed.
- Expected hot-path savings are proportional to culled solver/broadphase bodies: 100-500 us/frame in dense debris from removing prefab/per-object distance sleep, 50-1000 us/frame during stable culling from changed-index sync instead of broad Unity API mutation, and 10-200 us/event from targeted wake instead of radius wake-all.

Verification:
- Full root build is externally blocked by missing RealtimeCSG source files/package Temp artifacts.
- Isolated `Assembly-CSharp.csproj` compile with staged `Library/ScriptAssemblies` metadata passed: 0 warnings, 0 errors.
- Isolated `Assembly-CSharp-Editor.csproj` compile passed: 0 warnings, 0 errors.
- `git diff --check` passed with line-ending warnings only.
- Static anti-bloat scan of touched files found no `FindObject*`, `Vector3.Distance`, `foreach`, `.ToString()`, `Pack=1`, private `NativeArray`, 1024 compute thread markers, or `Material.SetFloat`.

<SELF_AUDIT>
  <TASK_CHECK>01 PASS, 02 PASS, 03 PASS, 04 PASS with existing-signal compatibility note, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS via SoA hysteresis to preserve DTO stride, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 PASS.</TASK_CHECK>
  <ARM64_CHECK>Primary DTO is explicit 40 bytes and has no Pack=1. Runtime culling structs touched in `GlobalPhysicsStateManager` no longer use Pack=1.</ARM64_CHECK>
  <ZERO_GC_CHECK>Tick hot paths use indexed loops, vault arrays, native queues/hash maps, and byte-span parser. No hidden closures/LINQ/string split in culling tick path.</ZERO_GC_CHECK>
  <AUP_CHECK>Distance/frustum math subtracts camera double3 AUP before float3 conversion.</AUP_CHECK>
  <DEAR_LIE_CHECK>Falling rocks are suspended offscreen by freezing velocity, disabling colliders, and sleeping the Rigidbody.</DEAR_LIE_CHECK>
  <DEPENDENCY_CHECK>GlobalRegistry interface and SignalBus are used; no direct seismic/torpedo/submarine dependency added.</DEPENDENCY_CHECK>
</SELF_AUDIT>
