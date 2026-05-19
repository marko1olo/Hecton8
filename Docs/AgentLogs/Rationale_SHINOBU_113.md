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

## 2026-05-19 - Fault Lane False-Sharing Repair

Problem: A single shared `int` fault flag was written from parallel integration/resolution jobs. Even when every writer stores the same value, that is a contested shared cache-line write and a weak proof path for endurance NaN forensics.
Solution: Added `HydrodynamicKccFaultFlagDTO` as a 64-byte explicit-layout per-entity fault slot and a Burst `ClearKccFaultFlagsJob`. Each worker writes only its own cache line; LateFrame ORs the masks after the post handle completes. The layout validator now checks wake, debug, telemetry, and fault DTO sizes.
Rejected Alternatives: Atomic counters were rejected because the current code only needs a sticky mask and atomics would introduce unnecessary contention. Keeping one scalar `int` was rejected as false-sharing debt.
Scalability potential: Low through Ultra share the same fault lane; low capacity pays one 64-byte slot, high capacity keeps deterministic per-entity forensic isolation.
Hardware Impact: On i3/MX350 this removes contested writes from the solver fault path; healthy-frame cost is one linear clear job over padded fault slots.

## 2026-05-19 - Wake Payload Packing

Problem: `WakeGeneratedSignal` in Core is fixed at AUP, velocity, and `SourceFlags`. The assignment demands wake magnitude and radius, but mutating the global Core DTO would violate the compile wall and affect unrelated wake producers. The first KCC pass also ORed the KCC source hash into low `SourceFlags`, which could confuse downstream source-kind decoding.
Solution: Kept the global DTO unchanged. KCC now carries `WakeRadius` and `WakeMagnitude` in its owner-local `HydrodynamicWakePacketDTO`, emits velocity with length equal to magnitude, and packs source kind in low 8 bits plus quantized magnitude/radius in high bits via `HydrodynamicKccMath.PackWakeSourceFlags`.
Rejected Alternatives: Changing `WakeGeneratedSignal` fields was rejected as cross-domain API mutation. Emitting wake GameObjects or fluid particles was rejected as allocation-heavy visual truth.
Scalability potential: Low uses scalar turbulence and compact packed metadata. Middle/High/Ultra can let downstream GPU/audio systems decode or ignore high-bit metadata without changing CPU solver cost.
Hardware Impact: Adds a few scalar ALU ops per emitted wake and preserves zero-GC signal routing.

## 2026-05-19 - Telemetry Compute Estimate

Problem: Task 16 asks for solver time in telemetry, but reading a real clock inside Burst jobs is not portable or deterministic. Managed `Stopwatch` around hot solver stages would add managed surface and would not represent per-entity job work.
Solution: Filled `KinematicTelemetryEntry.ComputeMicroseconds` with a deterministic compute-use estimate based on `GlobalQualityWeight`, speed, collision presence, and resolver iteration count. This is explicitly a profiler surrogate until Unity Profiler/Burst timing proof exists.
Rejected Alternatives: `Stopwatch`, `Time.realtimeSinceStartup`, and per-frame managed timing strings were rejected for GC/portability. Leaving the field at zero was rejected because it produced useless black-box data.
Scalability potential: Low quality records smaller expected compute because it executes fewer iterations; high/ultra records larger expected work as the resolver buys smoother collision polish.
Hardware Impact: Adds scalar math already adjacent to telemetry writes; no heap allocation and no main-thread stall.

## 2026-05-19 - Queue Mock Harness

Problem: The first pass had a queue mock job but no explicit public scheduling seam, so CI/profiling harnesses would need to know job construction internals.
Solution: Added `HydrodynamicKccMockInput.GenerateMockMovementInput(...)`, which schedules `GenerateMockMovementInputQueueJob` into a caller-owned `NativeQueue<HydrodynamicKccInputDTO>.ParallelWriter` and returns the dependency handle.
Rejected Alternatives: KCC-owned persistent `NativeQueue` was rejected by the Vault Law. Managed input callbacks and lambdas were rejected for hot-path GC and devirtualization risk.
Scalability potential: Harnesses can scale count continuously with `GlobalQualityWeight` outside this domain while the deterministic job remains unchanged.
Hardware Impact: No runtime cost unless a harness calls it; avoids managed mock-input overhead during isolated profiling.

## 2026-05-19 - Hit Edge Translator And Multi-Hit Resolution

Problem: The resolver read `RaycastHit` directly and used only one hit per command, so repeated quality iterations projected against the same normal. That was a weak answer for Task 11 and left Unity hit-field access inside the deterministic resolver.
Solution: Added `HydrodynamicKccCollisionHitDTO` and `ExtractCapsuleCastHitsJob`. Unity `RaycastHit` data is translated once into an owner-local 64-byte DTO, then `KinematicResolutionJob` reads only DTOs and processes up to `math.lerp(2, 8, GlobalQualityWeight)` hits per command. `CapsulecastCommand.ScheduleBatch` now uses the same continuous hit count.
Rejected Alternatives: Main-thread hit extraction was rejected because it would block the collision batch. Blindly trusting one hit was rejected because it made extra iterations cosmetic rather than functional.
Scalability potential: Low schedules and resolves two hits. Middle increases the hit budget continuously. High/Ultra uses up to eight hits for smoother corner response without a binary hardware branch.
Hardware Impact: Low quality avoids six extra hit records per command. High quality spends extra DTO reads only where the quality scalar allows it.

## 2026-05-19 - Vault Handle Refresh Throttle

Problem: `EnsureVaultBuffers()` reacquired every `VaultBufferHandle` whenever simulation/late paths called it. That is not managed allocation by itself, but it is unnecessary hot-path vault churn and could allocate if capacity changed unexpectedly.
Solution: Added `_resolvedBufferCapacity` and `AreVaultBuffersReady(...)`. Normal ticks now return after handle validation; full `GetBufferHandle` reacquisition happens only on initial boot, capacity growth, or DataVault hot-swap. Hot-swap drains outstanding jobs and resets handles before rebinding.
Rejected Alternatives: Removing safety checks from hot ticks was rejected because vault ownership can change during editor hot-swap. Keeping unconditional handle reacquisition was rejected as avoidable control-path work.
Scalability potential: Low through Ultra keep the same cold boot allocation behavior; higher entity capacity pays only when capacity actually changes.
Hardware Impact: Removes repeated dictionary/metadata lookups from healthy fixed/post/late ticks.

## 2026-05-19 - Blackbox Dump Byte Array Removal

Problem: `DumpTelemetry` copied the native telemetry ring into a managed `byte[]` before writing the dump file. It was fault-path only, but still contradicted the black-box memory discipline.
Solution: Replaced the managed array copy with `FileStream.Write(new ReadOnlySpan<byte>(nativePtr, bytes))`, matching existing project dump writers.
Rejected Alternatives: Leaving the managed array was rejected. Writing one entry at a time was rejected because it would add avoidable syscall overhead.
Scalability potential: No normal-frame impact. Large future telemetry rings can still dump as a contiguous native span.
Hardware Impact: Fault path avoids a 19.2 KB managed array allocation for the current 300-entry ring.

## 2026-05-19 - Telemetry Cursor Graph And CSV Ingest API

Problem: The editor graph read telemetry storage order, not ring chronological order, and CSV parsing had no runtime ingestion seam.
Solution: The UI Toolkit graph now reads `ShinobuHydroKccTelemetryCursor`, draws oldest-to-newest ring order, and throttles repaint to 20 Hz. Added `TryIngestFluidProfiles(ReadOnlySpan<byte>)` and `TryApplyFluidProfile(uint)` to hydrate and apply vault-backed CSV profile data without creating a private container.
Rejected Alternatives: Per-frame editor repaint and raw storage-order graphing were rejected as misleading. A private `NativeHashMap` was rejected by the Vault Law.
Scalability potential: Low can ingest sparse profile tables; high/ultra can use denser profiles without changing the solver ABI.
Hardware Impact: Editor-only graph work drops from every editor update to 20 Hz. Runtime CSV ingest remains cold/control-path only.

## 2026-05-19 - Hot Registry Fallback Removal And Editor-Only Layout Proof

Problem: `EnsureVaultBuffers()` is called from `FixedTick`/`PostFixedTick`; even a null-only fallback to `GlobalRegistry.DataVault` inside that helper violates the hot-path service cache law. The layout validator also used reflection for `UnsafeUtility.GetFieldOffset`, which is acceptable for the requested editor proof but not as player runtime surface.
Solution: Removed the `GlobalRegistry.DataVault` fallback from `EnsureVaultBuffers()`. DataVault is now cached only from `OnEnable` or `OnGlobalRegistryServiceReplaced`. Wrapped `HydrodynamicKccLayoutReport` and `HydrodynamicKccLayoutValidator` in `#if UNITY_EDITOR`, keeping the byte-offset proof as an editor-time contract only.
Rejected Alternatives: Polling `GlobalRegistry.DataVault` until the service appears was rejected as a hidden live configuration bus. Keeping reflection in player runtime was rejected because the validator is not needed for simulation.
Scalability potential: Low through Ultra simulation cadence no longer pays even a conditional registry path in the KCC buffer guard; editor validation remains available without player build reflection surface.
Hardware Impact: Small control-path saving only, but removes an architecture violation that could become a compile-wall/service-order defect on low-end hardware during hot swaps.

## 2026-05-19 - Vault Capacity Proof Tightening

Problem: `AreVaultBuffersReady()` checked multi-hit buffer length but only tested `IsCreated` for several per-entity lanes. A partial Vault relocation or bad capacity return could still schedule jobs that index past inputs, proposed velocities, commands, visual outputs, wake packets, fault flags, or rollback bytes.
Solution: Added explicit length proofs for every SHINOBU KCC Vault handle: per-entity lanes must be at least entity capacity, collision lanes must be `capacity * 8`, rollback bytes must be `capacity * sizeof(KinematicStateDTO)`, telemetry ring must be 300, and tuning/cursor/profile tables must meet their fixed capacities.
Rejected Alternatives: Relying on `VaultBufferHandle.IsCreated` was rejected because it only proves non-zero memory, not that this system's requested scheduling window is safe.
Scalability potential: Low through Ultra now fail closed before scheduling if a buffer cannot satisfy its continuous quality hit budget or entity capacity.
Hardware Impact: Adds a few integer comparisons on the buffer guard path and prevents out-of-range safety exceptions or native memory corruption under hot-swap/capacity drift.

## 2026-05-19 - Collision Hit Stride Freezing

Problem: `CapsulecastCommand.ScheduleBatch` lays out the raw hit buffer using the `maxHits` value supplied during `FixedTick`. `PostFixedTick` recomputed `maxHits` from the current quality scalar; if scalability changed between phases, command N would read hit slots using the wrong stride.
Solution: Added `_scheduledMaxHitsPerCommand` as a per-batch immutable stride. `FixedTick` stores the exact hit budget used for `ScheduleBatch`; `PostFixedTick` uses that stored stride for hit extraction and deterministic resolution, then clears it after scheduling the post chain.
Rejected Alternatives: Recomputing from tuning was rejected because `GlobalQualityWeight` is intentionally live and can change between simulation phases. Forcing quality to be static globally was rejected because thermal load-shed must remain continuous.
Scalability potential: Low through Ultra keep continuous 2-8 hit budgets, but each scheduled batch now has one fact, one owner, and one proof for its buffer stride.
Hardware Impact: One integer field write/read per batch; prevents wrong-hit collision response and rollback divergence under live quality changes.

## 2026-05-19 - Uninitialized State Slot Seeding

Problem: SHINOBU KCC Vault buffers request `NativeArrayOptions.UninitializedMemory`. The first valid transform seed must therefore prove every active `KinematicStateDTO` slot, not just entity zero, before Burst jobs read state.
Solution: `FixedTick` passes active capacity into `SeedInitialStateIfNeeded`. The seeder scans each active slot, accepts only finite AUP/velocity/angular velocity plus positive mass and finite drag, and otherwise writes a deterministic millimeter-quantized AUP derived from the sector origin, cached local transform, and a small capsule-spaced index offset. Integration also writes sanitized angular velocity, mass, and drag back to the state.
Rejected Alternatives: Clearing the whole Vault buffer was rejected because Task 15 explicitly wants uninitialized command/hit-style buffers and because O(n) memset at boot/hot-swap hides state proof instead of making it explicit. Seeding only entity zero was rejected because capacity can be raised by scalability, CI harnesses, or future multiplayer bodies.
Scalability potential: Low through Ultra use the same deterministic seed path; higher entity counts get stable initial spacing without adding runtime dependencies or direct spawn ownership.
Hardware Impact: Adds one cold/guard scan over active capacity before scheduling. It prevents NaN propagation and black-box fault dumps caused by uninitialized cache-line state on i3/MX350-class hardware.

## 2026-05-19 - Compile Guard Recheck 7

Problem: Static verification is clean enough to justify a build, but the hardware guard still blocks it.
Solution: Rechecked `dotnet/csc` and CPU counters after the state-slot audit. No `dotnet` or `csc` process was active, but Processor Time sampled `99.42, 70.67, 47.62, 44.08, 82.82` and Processor Utility sampled `85.02, 65.04, 49.15, 41.80, 71.44`, so `dotnet build` remains deferred.
Rejected Alternatives: Launching compilation under CPU spikes was rejected by project law. Marking Task 20 done without compiler evidence was rejected.
Scalability potential: Not a runtime algorithm; preserves iteration hardware under concurrent-agent load.
Hardware Impact: Prevents a build spike while the workstation is already above the allowed 50% CPU threshold.

## 2026-05-19 - Compile Guard Recheck 8

Problem: A delayed retry still did not provide a safe compilation window.
Solution: Waited 15 seconds, rechecked `dotnet/csc`, then sampled CPU again. No `dotnet` or `csc` process was active, but Processor Time sampled `67.98, 59.13, 65.65, 61.95, 94.14` and Processor Utility sampled `62.48, 60.51, 58.45, 61.57, 78.83`, so build remains blocked.
Rejected Alternatives: Running `dotnet build` during sustained CPU load was rejected. Silent completion without a compiler pass was rejected.
Scalability potential: Not a runtime path.
Hardware Impact: Avoids compounding CPU saturation during concurrent project work.

## 2026-05-19 - Resolver Scheduled Stride Proof

Problem: `FixedTick` correctly froze `_scheduledMaxHitsPerCommand`, and `ExtractCapsuleCastHitsJob` translated raw PhysX hits with that stride. `KinematicResolutionJob` still recomputed its `hitBase` from `min(scheduledStride, currentQualityIterations)`. If `GlobalQualityWeight` dropped between FixedTick and PostFixedTick, entity N could read entity N's hits using the wrong stride.
Solution: Split resolver math into two facts: `scheduledHitStride` owns buffer addressing and remains equal to the frozen batch stride; `executedIterations` owns compute budget and clamps current quality iterations to that stride. Telemetry now records executed iterations, not the theoretical quality budget.
Rejected Alternatives: Freezing global quality across the whole frame was rejected because thermal load-shed is intentionally live. Recomputing every stride from current tuning was rejected because `CapsulecastCommand.ScheduleBatch` already fixed the raw hit layout.
Scalability potential: Low quality can still execute two or three resolver passes after an ultra-quality batch, but it reads them from the correct per-entity hit window. High/Ultra keep the wider stride and can consume more hits when the live quality scalar permits.
Hardware Impact: No new memory. One integer multiply now uses the immutable scheduled stride; this prevents wrong-hit wall response and rollback divergence under live quality changes.

## 2026-05-19 - Compile Guard Recheck 9

Problem: Resolver stride repair requires compiler proof, but the CPU guard still has samples above the allowed threshold.
Solution: Rechecked `dotnet/csc` and CPU after the patch. No `dotnet` or `csc` process was active. Processor Time sampled `70.57, 41.94, 42.13, 68.23, 31.21`; Processor Utility sampled `68.08, 48.49, 42.20, 64.75, 34.48`. Build remains deferred because not all samples are below 50%.
Rejected Alternatives: Launching the build on the low samples only was rejected because the guard requires the workstation not to be under load, not a cherry-picked interval.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids adding compiler saturation on a machine with active CPU spikes.

## 2026-05-19 - KCC Input Contract Polish

Problem: The KCC had a 64-byte owner-local movement packet named `InputStateDTO` while Core already owns the canonical 24-byte `Hecton8.Core.InputStateDTO`. The layouts intentionally differ, but the shared simple name creates API ambiguity and makes `BufferID.ShinobuHydroKccInputs` look like a Core input lane. The mock-disabled path also allowed the uninitialized KCC input buffer to be consumed when no explicit external writer was armed.
Solution: Renamed the KCC packet to `HydrodynamicKccInputDTO`, added it to the editor layout validator, introduced `TryRegisterExternalInputWriter(JobHandle)`, and introduced `ClearKccInputBufferJob`. `FixedTick` now has three explicit routes: deterministic mock writer, armed external writer dependency, or deterministic zero-input clearing before integration.
Rejected Alternatives: Changing the Core input DTO was rejected as outside KCC ownership. Reusing `BufferID.ShinobuInputCurrentDto` was rejected because KCC needs target AUP and 3D movement command data. A bare `_consumeExternalInputBuffer` flag without a JobHandle seam was rejected because it creates a race with external producers. Silently trusting uninitialized Vault memory when mock input is disabled was rejected because it can generate nondeterministic thrust.
Scalability potential: Low through Ultra keep the same input ABI. External producers can write the owner-local lane only when `_consumeExternalInputBuffer` is enabled and they arm a dependency for the frame; isolated profiling keeps deterministic mock input.
Hardware Impact: The zero-input fallback costs one 64-byte write per active entity only when mock input is disabled without an armed external writer. It prevents NaN/wrong-thrust cascades on low-end hardware and removes a compile-wall naming hazard.

Follow-up clamp: The mock-input branch now clears any stale external-input latch, and `TryRegisterExternalInputWriter` rejects registration while `_runMockInput` is enabled. This prevents an external writer handle from being armed in mock mode and consumed later after a mode flip.

## 2026-05-19 - Compile Guard Recheck 10

Problem: Input contract polish requires compiler proof, but the workstation remains above the forbidden CPU threshold.
Solution: Rechecked `dotnet/csc` and CPU counters after the input-handoff patch. No `dotnet` or `csc` process was active. Processor Time sampled `92.47, 50.45, 96.91, 100.00, 100.00`; Processor Utility sampled `73.34, 51.69, 78.11, 83.57, 84.29`, so build remains deferred.
Rejected Alternatives: Launching `dotnet build` during sustained CPU saturation was rejected by the AGENTS guard.
Scalability potential: Not runtime logic.
Hardware Impact: Prevents compiler load from compounding already saturated concurrent-agent CPU usage.

## 2026-05-19 - Compile Guard Recheck 11

Problem: Build remains required, but the guard now fails on both active compiler process and CPU saturation.
Solution: Rechecked process list and CPU counters. `dotnet` process `44020` was active with CPU time `16.609375`. Processor Time sampled `87.30, 75.72, 99.63, 71.60, 84.71`; Processor Utility sampled `66.31, 61.35, 75.12, 59.40, 68.40`. Build remains deferred.
Rejected Alternatives: Starting a second build while another `dotnet` process is active was rejected by the explicit AGENTS prohibition.
Scalability potential: Not runtime logic.
Hardware Impact: Prevents duplicate compiler load on an already saturated workstation.

## 2026-05-19 - AUP Local Float Overflow Clamp

Problem: `ResolveLocalFloat3` correctly subtracted sector AUP before casting, but a finite wrong-sector delta could still exceed safe local float range and poison capsule command endpoints or visual output.
Solution: Added `MaxLocalFloatMagnitude = 131072f` and clamp only the transient local delta after `double3` subtraction and before `float3` construction. The 131.072 km bound exceeds the 100 km map requirement, so normal local motion is unchanged while origin mismatch stays finite.
Rejected Alternatives: Clamping authoritative `KinematicStateDTO.AUP_Position` was rejected because it would corrupt truth. Trusting downstream sanitize after float construction was rejected because overflow can already create invalid command endpoints.
Scalability potential: Low through Ultra share the same bounded AUP-to-float seam; no quality-tier branch is introduced.
Hardware Impact: Adds a small number of scalar comparisons in local conversion and prevents NaN/Infinity propagation into PhysX command data on all hardware.
