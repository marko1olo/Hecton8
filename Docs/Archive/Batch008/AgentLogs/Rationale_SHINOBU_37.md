# SHINOBU_37 Rationale - Physics Culling And LOD Overseer

## Baseline

Problem: Existing physics culling was centralized in `GlobalPhysicsStateManager`, but it still used linear evaluation/result scans and lacked the mandated DTO, spatial hash, frozen velocity storage, frustum rejection, editor control, CSV override, and task-specific dump.

Solution: Extend the existing registered manager through partial files, keep Unity API mutations on the main thread, move hot math into Burst over vault DTO lanes, and route cross-domain wakes through existing registry/signal boundaries.

Rejected Alternatives: A prefab `DistanceSleeper` component, `FindObjectsOfType`, or direct seismic/torpedo dependencies were rejected because they add per-object overhead or compile-wall coupling.

Scalability Potential: Low/MX350 shrinks activation radius sq to 25%; middle tier uses default 50m debris/200m vehicle fallback; high/ultra relax radii and spend saved solver cost on presentation systems.

Hardware Impact: Exact microseconds require Unity profiler proof. Expected gain is eliminating solver/broadphase work for offscreen/out-of-range debris and removing redundant Sleep/WakeUp API calls.

## Decision Journal

### D0 - Preserve Existing Registry Surface

Problem: `GlobalPhysicsStateManager` already implements `IPhysicsCullingOverseer` and is registered through `GlobalRegistry`.

Solution: Reuse this route and add SHINOBU_37 internals behind the same manager.

Rejected Alternatives: A new manager singleton would duplicate Rigidbody sleep authority.

Scalability potential: One registry endpoint works from toaster to ultra tier.

Hardware Impact: Avoids extra dispatch and compile-wall churn.

### D1 - DTO Layout vs. Flag/Hysteresis Conflict

Problem: Prompt demanded a 40-byte DTO and later requested new `CullingFlags` and `TimeSinceStateChange` fields.

Solution: Kept `PhysicsCullingDTO` explicit 40 bytes. `CullingFlags` overlays the mandated `_pad3` at offset 36. Hysteresis lives in a parallel vault SoA `NativeArray<float>` to avoid expanding DTO to 44 bytes.

Rejected Alternatives: Expanding DTO was rejected because it violates the ARM64/L1-cache assignment. Packing with `Pack=1` was rejected.

Scalability potential: Fixed 40-byte stride gives predictable cache behavior on ARM64, Steam Deck, and desktop.

Hardware Impact: Preserves 64-byte cache-line friendliness: one DTO plus part of next DTO per line rather than irregular padded growth.

### D2 - Existing Wake Signal Compatibility

Problem: The prompt asked for a 16-byte `WakeRequestSignal`, but the project already has a global 48-byte AUP-radius `WakeRequestSignal` in `Hecton8.Core.Contracts.Signals`.

Solution: Kept the global signal intact and added `PhysicsCullingTargetWakeRequestSignal` for the exact 16-byte target/impulse route. The existing global wake signal remains the radius-event corridor.

Rejected Alternatives: Redefining `WakeRequestSignal` would fragment the signal matrix and break existing `SignalBus<WakeRequestSignal>` users.

Scalability potential: Radius wakes handle shock/acoustic events; target wakes handle torpedo/raycast impacts without waking piles.

Hardware Impact: Targeted route avoids O(n) radius scans when the caller has an instance id.

### D3 - Spatial Hash Candidate Window

Problem: Evaluating 100k bodies linearly wastes Burst time and then forces broad result handling.

Solution: Added a `NativeParallelMultiHashMap<int,int>` rebuilt on the slow culling cadence. Bodies are hashed into 50m cells; the evaluator schedules only candidates in the 3x3 camera XZ window plus already-awake escape candidates that must be allowed to fall asleep.

Rejected Alternatives: Strictly evaluating only the 9 cells was rejected because initially-awake far bodies would never receive their first sleep transition.

Scalability potential: Low tier stays near O(visible+active); high tier can expand radii through tuning while preserving the same hash path.

Hardware Impact: Reduces steady-state debris culling from O(N) to near O(1) around the camera plus active leakage cleanup.

### D4 - Dear Lie Velocity Freeze

Problem: Distant falling rocks burn physics CPU, but deleting motion breaks continuity when the player returns.

Solution: Before sleep, store linear/angular velocity in `FrozenVelocityDTO`, zero the Rigidbody, call `Sleep()`, and disable cached colliders. On wake, restore colliders and velocity before `WakeUp()`.

Rejected Alternatives: Velocity dampening was removed because it destroys momentum and is not the requested cinematic fake.

Scalability potential: Low tier freezes aggressively; ultra tier can keep larger radii while retaining the same visual continuity.

Hardware Impact: Solver/broadphase cost is removed while the player cannot observe the body.

### D5 - Main-Thread Sync Bound

Problem: Burst jobs cannot call Unity APIs; scanning all bodies for state mutation wastes C++ boundary calls.

Solution: The job writes `NativeQueue<int> StateChangedIndices`. Post-fixed/late completion drains only changed indices for `Sleep/WakeUp` and `Collider.enabled`.

Rejected Alternatives: Full result scans with API calls were rejected. Counting scan remains API-free telemetry only.

Scalability potential: State churn stays proportional to transitions, not tracked bodies.

Hardware Impact: Prevents Sleep/WakeUp spikes when thousands of bodies remain stable.

### D6 - Human Control Bridge

Problem: Designers need to tune radii without C# recompiles.

Solution: Added `Physics Culling Tuner` EditorWindow and a byte-span CSV parser for `physics_culling_profiles.csv`.

Rejected Alternatives: Managed CSV libraries and ScriptableObject-only tuning were rejected because they allocate or require asset workflows.

Scalability potential: Designers can lower MX350 radii and raise ultra radii using the same vault data.

Hardware Impact: Editor/dev-only parsing; player hot path unchanged.

### D7 - Compile Guard

Problem: Full root build is polluted by unrelated missing RealtimeCSG/generated package artifacts.

Solution: Verified the touched runtime/editor assemblies with `BuildProjectReferences=false` and staged current `Library/ScriptAssemblies` metadata into `Temp/bin/Debug`, including placeholders for two missing references.

Rejected Alternatives: Editing package projects or RealtimeCSG was rejected as outside domain. Repeated full rebuild spam was rejected.

Scalability potential: Compile verification remains local to this patch.

Hardware Impact: Avoided repeated multi-minute full graph rebuilds after external dependency failure.

### D8 - Polish Inquisition

Problem: The final mandate required L1/layout proof, anti-bloat scan, and no fake completion claim.

Solution: Ran static searches over touched files for `FindObject*`, `Vector3.Distance`, `foreach`, `.ToString()`, `Pack=1`, private `NativeArray`, 1024-thread compute markers, and material instance mutation. No findings remained in touched physics/editor files. `git diff --check` passed with line-ending warnings only. Runtime/editor isolated compiles passed.

Rejected Alternatives: Claiming Unity runtime proof was rejected. No Play Mode, profiler, GCMonitor, or MX350 frame-time capture was run.

Scalability potential: Low/MX350 uses smaller radius sq and 9-cell hash; middle/high/ultra can increase radii through vault tuning or CSV without changing code.

Hardware Impact: The main unmeasured gain is solver/broadphase removal for sleeping offscreen debris. Exact microseconds remain PENDING UNITY PROFILER VERIFICATION.

<SELF_AUDIT>
  <TASK_CHECK>Tasks 01-20 PASS. Task 04 is implemented as a compatible 16-byte `PhysicsCullingTargetWakeRequestSignal` because the project already owns a 48-byte global `WakeRequestSignal`. Task 14 is implemented as vault SoA to preserve the mandated 40-byte DTO.</TASK_CHECK>
  <STRUCT_LAYOUT>PhysicsCullingDTO: 0 double3 AUP[24], 24 int InstanceId[4], 28 float ActivationRadiusSq[4], 32 byte IsAsleep[1], 33-35 pad[3], 36 uint _pad3/CullingFlags[4], total 40.</STRUCT_LAYOUT>
  <H_PHI_CHECK>DTO, frozen velocities, state ages, candidates, candidate mask, frame telemetry, tuning, mock seismic signals, and wake mirror are all `VaultBufferBinding<T>` from `GlobalDataVault`. Required NativeQueue/NativeParallelMultiHashMap collections are registered with `NativeMemorySentinel`.</H_PHI_CHECK>
  <ZERO_GC_CHECK>No `FindObject*`, `Vector3.Distance`, `foreach`, `.ToString()`, or private `NativeArray` fields remain in touched files. Parser uses byte spans.</ZERO_GC_CHECK>
  <AUP_CHECK>Absolute body/camera positions remain double3; only camera-relative delta becomes float3.</AUP_CHECK>
  <DEAR_LIE_CHECK>Offscreen rock gravity is faked by frozen velocity + disabled collider + Rigidbody.Sleep.</DEAR_LIE_CHECK>
  <BLACKBOX_CHECK>300-frame `PhysicsCullingFrameTelemetry` ring dumps to `Docs/AgentLogs/Dump_PHYSICS_CULLING.bin` on >1ms sync.</BLACKBOX_CHECK>
  <COMPILE_GUARD>Full build blocked by unrelated RealtimeCSG/package Temp graph; isolated Assembly-CSharp and Assembly-CSharp-Editor compiles passed 0/0.</COMPILE_GUARD>
</SELF_AUDIT>
