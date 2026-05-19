# Rationale SHINOBU_113

## 2026-05-19 - Preflight

Problem: Legacy player locomotion mixes Rigidbody presentation, synchronous kinematic application, and large cross-domain MonoBehaviour dependencies. Replacing every caller in one pass risks broad merge conflict and compile-wall damage.  
Solution: Add a narrow Burst-compatible hydrodynamic KCC kernel under the existing KCC physics namespace, backed by GlobalDataVault handles, then patch only directly relevant ARM64/Burst layout violations in current kinematics files.  
Rejected Alternatives: A full rewrite of `HectonPlayerMovement`/`HectonPlayerMotor` in one batch would touch inventory, interaction, audio, world, and vehicle routes and would not be safely verifiable under concurrent agents. Standard Unity `CharacterController`, `Rigidbody.AddForce`, and main-thread `Physics.CapsuleCast` are rejected for determinism and zero-GC control.  
Scalability potential: Low uses two solver iterations, analytical drag, and single scalar turbulence. Middle raises smoothing and collision precision continuously. High increases CCD refinement and richer wake scalars. Ultra spends saved CPU on visual/audio wake overkill without CPU fluid particles.  
Hardware Impact: i3/MX350 path avoids managed allocation and avoids per-frame force dispatch; expected gain is tens of microseconds per controlled body versus Rigidbody force + main-thread cast, with larger wins when many kinematic bodies share the same command batch.

## 2026-05-19 - Kinematic State Ownership

Problem: The existing player movement route still contains Rigidbody presentation calls and broad cross-domain dependencies. A direct hard swap would break concurrent agents and netcode-owned state buffers.  
Solution: Introduced `KinematicStateDTO` as a new 64-byte explicit-layout AUP authority under `Hecton8.Physics.KCC`, with dedicated `ShinobuHydroKcc*` Vault buffer IDs instead of reusing `PlayerKinematicState`. Jobs mutate via `UnsafeUtility.AsRef` to avoid defensive struct copies.  
Rejected Alternatives: Reusing `PlayerKinematicState` was rejected because it is already owned by lockstep/netcode. Editing fauna, docking, and transport `MovePosition` routes was rejected as outside this agent's immediate domain. Standard `CharacterController` and PhysX force integration remain rejected.  
Scalability potential: Low runs the same state layout with fewer resolver passes. Middle increases precision continuously. High/Ultra use the same data and spend extra passes on collision polish and richer wake scalars.  
Hardware Impact: i3/MX350 gains from predictable 64-byte cache-line state and no property-copy mutation; expected benefit is small per body but important when KCC count grows.

## 2026-05-19 - Async Collision Pipeline

Problem: Main-thread sweeps or immediate `JobHandle.Complete()` in simulation would serialize movement behind PhysX and defeat dispatcher parallelism.  
Solution: Split the route into input/integration, command build, deferred `CapsulecastCommand.ScheduleBatch`, post-simulation resolution, rollback copy, wake emission, and late-frame non-blocking swap-window completion.  
Rejected Alternatives: `Physics.CapsuleCast`, `Physics.SphereCast`, and same-tick completion were rejected because they force the caller to wait. Keeping old `Rigidbody.MovePosition` as the math owner was rejected; it remains only legacy presentation until integration handoff.  
Scalability potential: Low uses 2 projection passes and single-hit capsule batch. Middle/High/Ultra raise projection passes through `GlobalQualityWeight` without binary tiers.  
Hardware Impact: On i3/MX350 the main win is removal of sweep wait from the simulation lane; expected saving ranges from tens of microseconds in clear space to stall avoidance under dense collision.

## 2026-05-19 - Dear Lie Hydrodynamics

Problem: Real water displacement around character capsules is CPU-expensive and unnecessary for movement feel.  
Solution: Use analytical nonlinear drag plus a turbulence scalar derived from normalized speed. The scalar is routed into unmanaged wake packets and `SignalBus<WakeGeneratedSignal>.ParallelWriter`; downstream camera/audio/GPU water can sell the effect.  
Rejected Alternatives: Navier-Stokes, mesh-water friction, Rigidbody drag, and wake GameObject spawning were rejected as wrong-owner or allocation-heavy solutions.  
Scalability potential: Low keeps only scalar drag/turbulence. Middle increases smoothing. High/Ultra can consume the same scalar for richer GPU flow, camera shake, and audio without changing CPU simulation complexity.  
Hardware Impact: On i3/MX350 this avoids particle/fluid simulation entirely; expected savings are millisecond-scale if compared to a naive CPU fluid approximation.

## 2026-05-19 - CSV Profile Storage

Problem: The batch requested a `NativeHashMap` in the Vault, but current `IDataVault` exposes typed `NativeArray` buffers and slices, not persistent `NativeHashMap` ownership. A private persistent `NativeHashMap` would violate the Vault Law.  
Solution: Implemented cold `ReadOnlySpan<byte>` parsing into a vault-compatible flat profile array plus integer bucket array using FNV-1a and linked indices. This preserves zero-GC lookup compatibility without local persistent containers.  
Rejected Alternatives: `string.Split`, LINQ, managed dictionaries, and private persistent `NativeHashMap` fields were rejected.  
Scalability potential: Low can ingest fewer profiles and use nearest profile. Middle/High/Ultra can hydrate denser biome/depth profiles without changing runtime solver shape.  
Hardware Impact: On i3/MX350 cold-load GC spikes are avoided; runtime lookup remains cache-friendly.

## 2026-05-19 - Compile Guard

Problem: Code changes now require compilation, but project law forbids launching a build while dotnet/csc is active or CPU load exceeds 50%.  
Solution: Checked dotnet/csc process list and CPU counters. No dotnet/csc process was active, but CPU sampled above the allowed threshold, so build is deferred.  
Rejected Alternatives: Ignoring the hardware guard and launching `dotnet build` under load was rejected.  
Scalability potential: Protects developer iteration hardware from avoidable thermal contention.  
Hardware Impact: Prevents a compile spike on already saturated silicon.

## 2026-05-19 - Gizmo Solver Evidence

Problem: The first gizmo pass drew current/predicted capsules but did not route the collision normal from the solver, leaving the red normal line as a placeholder.  
Solution: Added `HydrodynamicKccDebugOutputDTO` in a Vault buffer. `KinematicResolutionJob` writes current local position, predicted local position, hit distance, flags, and collision normal; `OnDrawGizmos` reads the latest debug DTO after visual sync.  
Rejected Alternatives: Guessing the normal from Transform delta or reading Physics state in `OnDrawGizmos` was rejected because the gizmo must show KCC solver evidence.  
Scalability potential: Low through Ultra use the same debug DTO; editor-only visualization never enters runtime solver cost when gizmos are off.  
Hardware Impact: Runtime cost is one 64-byte write per entity when the debug buffer is present; no gameplay allocation.

## 2026-05-19 - Static Compile-Risk Cleanup

Problem: Unity API call sites were brittle before compilation: `RaycastHit.normal` and capsule command endpoints relied on implicit UnityEngine/Mathematics conversions, and `QueryParameters` received a `LayerMask` instead of its explicit integer value. Fault dumps could also repeat every LateFrame after a persistent NaN flag.  
Solution: Converted `hit.normal` to `float3` explicitly, converted capsule command endpoints/direction to `Vector3` explicitly, passed `_collisionMask.value` into `QueryParameters`, and added a scalar `_dumpedFaultMask` so the black-box dump writes once per distinct fault mask.  
Rejected Alternatives: Waiting for compiler errors was rejected because these were deterministic static risks. Clearing the fault flag after dump was rejected because it would hide forensic state from live diagnostics.  
Scalability potential: Low through Ultra paths share the same safer API calls; the dump guard prevents repeated crash-path allocations from becoming a frame loop under persistent fault.  
Hardware Impact: No runtime cost in healthy frames; faulted frames avoid repeated managed byte-array dumps on saturated low-end hardware.

## 2026-05-19 - Teardown Job Ownership

Problem: `OnDisable` originally forced only the post-simulation or collision handle, leaving command/integration/input handles implicit through dependency chains. That is safe in normal order but brittle during editor domain reloads, component disable, or hot-swap while only part of the chain is scheduled.  
Solution: Added `DrainPendingJobsForTeardown()` to force-complete post, collision, command, integration, and input handles through `DispatcherJobSwap.TryComplete(forceComplete:true)` before unregistering lanes.  
Rejected Alternatives: Direct `JobHandle.Complete()` was rejected. Ignoring teardown was rejected because Vault aliases must not outlive the registered owner during disable.  
Scalability potential: Low through Ultra paths are unchanged during healthy simulation; teardown is deterministic and bounded.  
Hardware Impact: No per-frame cost; one-time disable/hot-swap drain avoids racey memory ownership failures.

## 2026-05-19 - Rollback Resimulation Seam

Problem: The first pass wrote a contiguous rollback memcpy fence but did not expose an owner-local fast-forward seam for rollback resimulation frames. Directly referencing `HectonRollbackNetcodeRuntime` from KCC would violate the compile-wall boundary.  
Solution: Added `TryRunRollbackResimulation(int requestedFrames, float fixedDeltaTime)`. It drains outstanding work, runs the existing fixed/post pipeline for a quality-budgeted number of frames, force-completes only inside this explicit rollback API, and sets visual bypass flags so presentation smoothing does not lie during resim.  
Rejected Alternatives: A direct netcode runtime dependency and hidden polling of rollback state were rejected. Running every rollback frame at the requested count without `GlobalQualityWeight` was rejected because thermal pressure still applies during resim.  
Scalability potential: Low quality allows one resim frame per call; middle/high/ultra lerp up to `_maxRollbackFastForwardFrames` through the same scalar.  
Hardware Impact: Normal frames remain async. Rollback frames pay bounded synchronous work only when the rollback owner explicitly calls the seam.

## 2026-05-19 - Unsafe Layout Offset Validator

Problem: Task 04 explicitly requested an UnsafeUtility-backed offset validator; the first implementation used `Marshal.OffsetOf`, which was structurally correct but not the requested proof path.  
Solution: Replaced the validator offset helper with `UnsafeUtility.GetFieldOffset(typeof(T).GetField(fieldName))`, returning `-1` on missing fields so layout validation fails closed.  
Rejected Alternatives: Keeping the Marshal helper was rejected because the assignment specified UnsafeUtility. Moving reflection into a Burst job was rejected; this remains cold/editor verification only.  
Scalability potential: No runtime scalability impact; it improves ARM64 layout proof fidelity.  
Hardware Impact: Zero hot-path cost.
