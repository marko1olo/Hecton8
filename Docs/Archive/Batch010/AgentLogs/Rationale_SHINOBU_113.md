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

## 2026-05-19 - Collision Slide, AUP Hash, And Wake Route Proof

Problem: The resolver advanced to contact but then added a full-timestep projected velocity, which could double-count motion after a sweep hit and inject wall energy. The telemetry hash used only fractional AUP meters, so two positions separated by whole meters could collide in the forensic state hash. Wake emission also delegated absolute-position conversion to a foreign static helper from inside the Burst job.
Solution: `KinematicResolutionJob` now computes the consumed fraction from `allowedDistance / castDistance`, applies only the remaining timestep to the projected velocity, and keeps the scheduled hit stride separate from live quality iteration count. `HashState` folds low/high bits from millimeter-quantized AUP axes. `EmitWakeSignalsJob` calls owner-local `HydrodynamicKccMath.ToAup48`, which builds the `AbsoluteUniversePosition` payload directly from sanitized double3 meters before signal emission.
Rejected Alternatives: Hard-stopping all motion at first contact was rejected because it destroys kinematic slide. Leaving the full-timestep projected displacement was rejected because it can over-advance along walls. Hashing raw doubles was rejected because bitwise cross-platform double noise is poor rollback evidence. Calling the external AUP converter in the hot job was rejected because one fact must have one owner and one route.
Scalability potential: Low still resolves two hit records and gets the corrected remaining-time slide. Middle, high, and ultra spend the same continuous 2-8 hit budget with better corner energy behavior and richer telemetry proof. The wake route remains scalar metadata, not CPU fluid truth.
Hardware Impact: Adds a few scalar divisions/multiplications already guarded by `MinDenominator`; prevents repeated collision correction churn and improves black-box forensic selectivity with no new allocation.

## 2026-05-19 - Compile Guard Recheck 12

Problem: Collision slide/hash/wake proof now requires compiler validation, but the workstation still did not satisfy the build guard.
Solution: Rechecked `dotnet/csc` and CPU counters. No `dotnet` or `csc` process was active. Processor Time sampled `99.04, 35.70, 43.42, 31.41, 23.87`; Processor Utility sampled `80.57, 42.60, 49.44, 39.71, 31.68`. Build remains deferred because the first sample exceeded 50%.
Rejected Alternatives: Launching `dotnet build` after cherry-picking only the later low samples was rejected; the guard is intended to avoid compiling during active CPU spikes.
Scalability potential: Not runtime logic.
Hardware Impact: Prevents a compiler spike from stacking on concurrent-agent CPU load.

## 2026-05-19 - Compile Guard Recheck 13 And Asmdef Boundary Check

Problem: A second delayed guard check still blocked compilation. A separate KCC asmdef was also considered for compile-wall isolation, but current root-assembly scripts already reference `Hecton8.Physics.KCC`.
Solution: Rechecked `dotnet/csc` and CPU counters. No `dotnet` or `csc` process was active. Processor Time sampled `55.15, 54.42, 65.56, 88.11, 100.00`; Processor Utility sampled `56.90, 56.66, 58.74, 71.74, 77.22`. Build remains deferred. No new asmdef was created because `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` is in the root `Hecton8.Core` assembly and already imports `Hecton8.Physics.KCC`; moving KCC into an assembly that references Core would require a larger owner migration to avoid a cyclic assembly dependency.
Rejected Alternatives: Launching `dotnet build` under sustained CPU saturation was rejected. Creating a new KCC asmdef without moving root dependents was rejected because it would protect one folder by breaking existing root assembly references.
Scalability potential: Not runtime logic. The compile-wall risk is documented as a future owner migration, not hidden behind an unsafe partial move.
Hardware Impact: Avoids duplicate compiler load and avoids an avoidable assembly cycle.

## 2026-05-19 - Active Capsule Batch Window

Problem: `CapsulecastCommand.ScheduleBatch` was passed the full Vault command/hit arrays. The normal requested size equals `_entityCapacity`, but Vault handles are allowed to satisfy later smaller active counts with a larger existing buffer. In that case, commands outside the active KCC count could contain stale or uninitialized command data and still be scheduled.
Solution: Before scheduling, the runtime now slices the command array to `commands.GetSubArray(0, capacity)` and hit storage to `hits.GetSubArray(0, capacity * maxHits)`. `BuildCapsuleCastCommandsJob` and `ExtractCapsuleCastHitsJob` still operate on the same active count; the PhysX command batch now has the same owner-local active window.
Rejected Alternatives: Clearing all unused command slots each frame was rejected because it defeats Task 15 zero-init bypass. Forcing Vault buffers to shrink exactly to active capacity was rejected because Vault ownership should allow reuse and growth without per-frame allocation churn.
Scalability potential: Low through Ultra keep continuous 2-8 hit budgets. The active command window scales by entity count and quality without scheduling stale memory.
Hardware Impact: `GetSubArray` is a bounds/view operation, not a new allocation. It prevents wasted PhysX commands and avoids undefined collision queries when capacity contracts after a growth.

## 2026-05-19 - Compile Guard Recheck 14

Problem: Active batch-window hardening needs compiler proof, but the build guard is now blocked by active `dotnet` processes and CPU spikes.
Solution: Rechecked `dotnet/csc` and CPU counters. Active `dotnet` processes were present: `2880, 15852, 42588, 46472, 49196, 54384, 63912`. Processor Time sampled `100.00, 69.17, 33.76, 33.03, 25.22`; Processor Utility sampled `85.83, 61.73, 39.85, 30.40, 26.98`. Build remains deferred.
Rejected Alternatives: Starting another `dotnet build` while seven `dotnet` processes are active was rejected by the explicit hardware guard.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids stacked compiler contention on an already busy workstation.

## 2026-05-19 - AUP48 Cast Clamp

Problem: Owner-local `ToAup48` sanitized NaN/Infinity but did not bound extreme finite double3 input before casting grid coordinates to `long`. Normal 100 km play space is far below the limit, but black-box/fault paths must survive corrupt finite inputs.
Solution: Added `MaxAupMagnitudeMeters` and clamps sanitized double3 meters before `math.floor` and `long` casts. Telemetry hash quantization now uses the same constant instead of a magic literal.
Rejected Alternatives: Trusting the 100 km world contract alone was rejected because fault handling must survive invalid upstream state. Clamping authoritative `KinematicStateDTO.AUP_Position` inside the wake converter was rejected; only the signal payload conversion is bounded.
Scalability potential: Low through Ultra share the same bounded conversion. No quality branch or visual pop is introduced.
Hardware Impact: Adds three scalar clamp operations on wake emission only; prevents undefined long casts in fault cases.

## 2026-05-19 - Compile Guard Recheck 15

Problem: AUP48 clamp patch requires compiler proof, but the guard is still blocked by active `dotnet` processes and CPU spikes.
Solution: Rechecked `dotnet/csc` and CPU counters. Active `dotnet` processes remained: `2880, 15852, 42588, 46472, 49196, 54384, 63912`. Processor Time sampled `58.81, 42.63, 89.52, 100.00, 100.00`; Processor Utility sampled `57.47, 46.28, 75.62, 84.56, 84.13`. Build remains deferred.
Rejected Alternatives: Launching a build while another compiler family process is active was rejected.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids parallel compiler contention.

## 2026-05-19 - Scheduled Entity Count Fence

Problem: Hit stride was frozen, but the active command count was still recomputed from live `_entityCapacity` in `PostFixedTick`. If capacity changed after `FixedTick` scheduled the PhysX batch, the post phase could extract and resolve a different number of entity hit windows than the batch actually executed.
Solution: Added `_scheduledEntityCount` as a per-batch immutable fact. `FixedTick` stores the active count used for `BuildCapsuleCastCommandsJob` and `CapsulecastCommand.ScheduleBatch`. `PostFixedTick` uses that frozen count and validates every per-entity, rollback, wake, debug, raw-hit, and resolved-hit lane before scheduling extraction/resolution.
Rejected Alternatives: Recomputing capacity from live `_entityCapacity` was rejected because capacity is mutable editor/runtime control state. Clearing or resizing Vault buffers to force exact capacity was rejected because it fights the Vault's reuse model and Task 15 zero-init bypass.
Scalability potential: Low through Ultra keep continuous hit budgets and variable capacity, but each batch now has frozen count and frozen stride. Live quality/capacity changes affect the next batch, not the one already submitted to PhysX.
Hardware Impact: Adds one integer field and a small validation block in the post phase. Prevents wasted hit extraction and wrong-window collision resolution after capacity changes.

Follow-up clamp removal: `PostFixedTick` now treats `_scheduledEntityCount` as the exact batch fact and validates `states.Length >= scheduledCount` instead of clamping the scheduled count down to current state length. Silent shortening was rejected because it would hide a batch/window mismatch.

## 2026-05-19 - Compile Guard Recheck 16

Problem: Scheduled entity-count fence needs compiler proof, but the CPU guard still blocked the build.
Solution: Rechecked `dotnet/csc` and CPU counters. No active `dotnet` or `csc` process was returned. Processor Time sampled `37.67, 15.60, 97.87, 98.86, 39.88`; Processor Utility sampled `45.41, 20.41, 84.98, 83.64, 49.69`. Build remains deferred because two CPU samples exceeded 50%.
Rejected Alternatives: Launching the build because only some samples were below 50% was rejected; the guard exists to avoid compiling during spikes.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids compile contention during non-compiler CPU spikes.

## 2026-05-19 - Gizmo Diagnostics Decoupling

Problem: `LateFrameTick` refreshed gizmo debug cache only when `_applyVisualToTransform` was true and a cached transform existed. That made Task 19 diagnostic proof dependent on a presentation toggle.
Solution: The visual buffer is now read first, solver debug DTO values update `_lastGizmoCurrent`, `_lastGizmoPredicted`, and `_lastGizmoNormal` whenever visual output exists, and only the final transform write is gated by `_applyVisualToTransform`.
Rejected Alternatives: Leaving gizmos coupled to transform application was rejected because engineering diagnostics must remain visible when presentation writes are disabled for integration tests.
Scalability potential: Editor/debug surface only. Low through Ultra runtime math is unchanged.
Hardware Impact: No new jobs or allocations. One control-path branch moved after debug-cache update.

## 2026-05-19 - Compile Guard Recheck 17

Problem: Gizmo diagnostic decoupling needs compiler proof, but the CPU guard still blocked the build.
Solution: Rechecked `dotnet/csc` and CPU counters. No active `dotnet` or `csc` process was returned. Processor Time sampled `31.12, 78.12, 97.71, 100.00, 100.00`; Processor Utility sampled `36.16, 63.27, 76.05, 77.15, 75.29`. Build remains deferred.
Rejected Alternatives: Launching the build during sustained CPU spikes was rejected.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids compiler load during CPU saturation.

## 2026-05-19 - Legacy Physics Archaeology Refresh

Problem: Task 01/02 evidence needed a fresh scan after the KCC patches to prove no new synchronous sweep or CharacterController dependency entered the project-owned path.
Solution: Re-ran source/prefab/scene scans. `Assets/_Project` returned no `CharacterController` and no synchronous `Physics.CapsuleCast`/`Physics.SphereCast`; remaining project-owned hits are legacy `MovePosition` presentation/routes in docking, airlock, player motor/kinematics, origin shift, fauna, interaction, transport, station keeping, and vehicle motor. Third-party `Assets/Plugins`, Astar, DOTween, and Candice hits are outside KCC authority.
Rejected Alternatives: Deleting all legacy `MovePosition` calls in this pass was rejected because many belong to transport, origin shift, docking, fauna, or interaction owners. Replacing third-party package internals was rejected.
Scalability potential: The new KCC route remains the movement-authority path; legacy presentation routes require staged owner migration.
Hardware Impact: No runtime change from the scan. It prevents false completion claims and keeps the migration boundary explicit.

## 2026-05-19 - Compile Guard Recheck 18

Problem: The latest static polish still needs compiler proof, but the build guard failed again.
Solution: Rechecked `dotnet/csc` and CPU counters. Active `dotnet` processes were present: `6624, 20496, 32920, 33996, 35560, 56072, 71692`. Processor Time sampled `40, 86, 83, 73, 17`. Build remains deferred because another compiler-family process is active and CPU has samples above 50%.
Rejected Alternatives: Starting `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` while seven `dotnet` processes are active was rejected by the explicit workstation guard.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids stacking compiler contention on concurrent project work.

## 2026-05-19 - Audit Hardening: Input Generation, Abort Drain, Telemetry Owner

Problem: Sub-agent audit found three hard risks: stale external inputs could be consumed without frame/sequence/source/sector proof, post-phase early aborts could leave `_collisionScheduled` set after a failed buffer resolve, and telemetry wrote from parallel workers with only entity-zero coverage. The black-box dump path also used the XML nickname instead of the required agent ID.
Solution: Added sector-generation packing into `HydrodynamicKccInputDTO.Flags`, `HydrodynamicKccInputContract.BuildExternalInput(...)`, and `SanitizeKccInputBufferJob` validation for frame, sequence, source hash, sector generation, finite vectors, and local AUP range. Added `AbortScheduledBatch()` to drain hit/collision/command/integration/input/external handles before clearing batch state. Moved black-box telemetry writes into one post-resolution `KinematicTelemetryAggregateJob` that folds all active entity state hashes and fault/collision flags into one ring entry. The editor graph now reads Vault telemetry only when `_collisionScheduled` and `_postScheduled` are both false, so no private editor array is retained. Dump file is now `Docs/AgentLogs/Dump_SHINOBU_113.bin`. Kept unsafe `UnsafeUtility.AsRef` mutation because the assignment explicitly requires that mutation route and `Hecton8.Core.asmdef` has `allowUnsafeCode=true`; replacing it with value-copy `NativeArray[index]` mutation was rejected for this batch.
Rejected Alternatives: Trusting external writers by convention was rejected. Completing only the collision handle on abort was rejected because earlier handles own the same Vault lanes. Entity-zero telemetry was rejected because it hides multi-body faults. A direct netcode/input sibling dependency was rejected; the route stays handle/DTO based.
Scalability potential: Low quality still sanitizes every packet but executes fewer collision hits; middle/high/ultra can widen resolver work without changing the input ABI. The telemetry aggregate remains O(n) with one ring write regardless of quality.
Hardware Impact: Adds one 64-byte input validation pass and one O(n) aggregate per active batch. It removes stale-input divergence and parallel telemetry contention, which is more valuable than the small extra ALU cost on i3/MX350-class hardware.

## 2026-05-19 - Route Card And Layout Report Padding

Problem: Vault ownership existed in code/docs but did not have a single route card for integrators. The editor-only layout report was 56 bytes: ARM64-safe by 8-byte multiple, but weaker than the project's 64-byte cache-line proof style.
Solution: Added `Docs/ARCHITECTURE/SHINOBU_113_HYDRODYNAMIC_KCC_ROUTE_CARD.md`, linked it from the kinematics architecture doc, and registered the lane in the binary payload ledger. Padded `HydrodynamicKccLayoutReport` to explicit 64 bytes.
Rejected Alternatives: Letting integrators infer the route from code was rejected because multiple agents are editing nearby domains. Leaving the 56-byte report was technically aligned but rejected for proof clarity.
Scalability potential: Documentation only. The route card states low/middle/high/ultra consumers must use the same Vault/SignalBus seams.
Hardware Impact: No runtime cost.

## 2026-05-19 - Compile Guard Recheck 19

Problem: Audit-hardening source changes now need compiler proof, but the workstation remains above the explicit build threshold.
Solution: Rechecked `dotnet/csc` and CPU counters. No active `dotnet` or `csc` process was returned. Processor Time sampled `100, 98.45, 93.64, 100, 83.19`; Processor Utility sampled `86.07, 84.24, 78.98, 84.22, 70.77`. Build remains deferred.
Rejected Alternatives: Starting `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` during sustained CPU saturation was rejected by the explicit AGENTS guard.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids compiler contention on an already saturated concurrent-agent workstation.

## 2026-05-19 - Editor Telemetry H-PHI Correction

Problem: The editor graph hardening briefly used a `KinematicTelemetryEntry[]` managed snapshot behind `#if UNITY_EDITOR`. It was not player-runtime data, but it weakened the zero-private-array proof.
Solution: Removed the managed snapshot. `TryReadEditorTelemetryVault(...)` now resolves the Vault telemetry ring only from the active runtime and returns false while `_collisionScheduled` or `_postScheduled` is true. No private runtime/editor array owns telemetry.
Rejected Alternatives: Keeping an editor-only array was rejected because the final H-PHI statement should not need a loophole. Completing jobs from the editor graph was rejected because diagnostics must not alter the scheduler.
Scalability potential: Editor-only. Runtime scalability is unchanged.
Hardware Impact: Removes one editor-domain managed array allocation; graph reads are disabled during active KCC batches.

## 2026-05-19 - Compile Guard Recheck 20

Problem: Editor telemetry H-PHI correction changed code after Recheck 19, so compile proof is again required, but the CPU guard remains red.
Solution: Rechecked `dotnet/csc` and CPU counters. No active `dotnet` or `csc` process was returned. Processor Time sampled `100, 100, 100, 100, 100`; Processor Utility sampled `85.66, 83.27, 83.56, 86.12, 83.12`. Build remains deferred.
Rejected Alternatives: Starting `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` during 100% CPU saturation was rejected.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids adding compiler load to a fully saturated workstation.

## 2026-05-19 - Compile Guard Recheck 21

Problem: The editor telemetry method rename touched code after Recheck 20, so compile proof remains due, but CPU still exceeds the build threshold.
Solution: Rechecked `dotnet/csc` and CPU counters. No active `dotnet` or `csc` process was returned. Processor Time sampled `100, 100, 100, 98.84, 79.79`; Processor Utility sampled `86.79, 80.31, 84.2, 80.39, 73.64`. Build remains deferred.
Rejected Alternatives: Starting `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` during sustained CPU saturation was rejected.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids adding compiler load to an overloaded workstation.

## 2026-05-19 - Vault Window Fail-Closed Pass

Problem: `FixedTick` trusted the ready-state helper for every active per-entity lane, and `PostFixedTick` did not explicitly require the telemetry cursor/ring and fault lane before scheduling the post chain. That was probably satisfied by `AreVaultBuffersReady(...)`, but it was not a local fail-closed proof at the actual scheduling point. `OnDrawGizmos` also had a fallback `GetComponent<CapsuleCollider>()` lookup that static hot-path scans flagged even though the gizmo route is editor diagnostic.
Solution: Added direct active-window length guards for input, proposed velocity, command, raw-hit, fault, wake, telemetry, and cursor lanes before scheduling jobs. The fixed-phase guard clears frozen scheduled facts before returning without a batch. Added `[DisallowMultipleComponent]` and `[RequireComponent(typeof(CapsuleCollider))]` to the KCC runtime and removed the gizmo `GetComponent` fallback; gizmos now use the cached capsule or deterministic default dimensions.
Rejected Alternatives: Relying on the earlier Vault readiness helper alone was rejected because buffer aliases can be invalidated or shortened by hot-swap mistakes and the job schedule site must fail closed. Keeping editor `GetComponent` fallback was rejected because it produced avoidable static scan noise and the component requirement is a cleaner contract.
Scalability potential: Low through Ultra use the same owner-local batch window; a missing lane drops the frame instead of scheduling undefined memory. No binary quality branch is introduced.
Hardware Impact: Adds integer length checks only on the control path before scheduling. Prevents wasted PhysX batches and avoids a per-gizmo component lookup in editor diagnostics.

## 2026-05-19 - Compile Guard Recheck 22

Problem: The Vault window fail-closed pass changed runtime source, so compile proof is required, but workstation conditions still violate the explicit build guard.
Solution: Rechecked `dotnet/csc` and CPU counters. No active `dotnet` or `csc` process was returned. Processor Time sampled `100, 97.5, 98.65, 100, 100`; Processor Utility sampled `74.86, 78.67, 77.77, 76.73, 81`. Build remains deferred.
Rejected Alternatives: Launching `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` during sustained CPU saturation was rejected.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids adding compiler load to an overloaded workstation.

## 2026-05-19 - Compile Guard Recheck 23 And Fresh Self-Audit Anchor

Problem: The log needed a new bottom-anchored self-audit after the Vault window fail-closed pass, but compile proof is still blocked by workstation load.
Solution: Re-ran the build guard and targeted KCC forbidden-pattern scan before appending a fresh `SELF_AUDIT` block. No active `dotnet` or `csc` process was returned. Processor Time sampled `100, 100, 100, 100, 100`; Processor Utility sampled `85.87, 81.22, 84.26, 84.13, 83.98`. Targeted KCC forbidden-pattern scan returned no matches. Build remains deferred.
Rejected Alternatives: Launching `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` at 100% CPU was rejected. Claiming runtime closure from static scans was rejected.
Scalability potential: Not runtime logic. The self-audit records the continuous quality path already implemented: low quality reduces hit/rollback/smoothing cost; high/ultra spends the same route on more collision samples and richer wake data.
Hardware Impact: Avoids adding compiler load to a saturated workstation while preserving an auditable proof trail for the integrator.

## 2026-05-19 - Dual Blackbox Dump Path Reconciliation

Problem: The project-level black-box protocol requires `Docs/AgentLogs/Dump_SHINOBU_113.bin`, while the original XML task 16 names `Docs/AgentLogs/Dump_KINEMATICS_SURGEON.bin`. Writing only one path leaves either the integrator's ID-based sweep or the XML task audit with a false negative.
Solution: `DumpTelemetry` now writes the same native `KinematicTelemetryEntry` ring span to both filenames through a small fault-path `WriteTelemetryDump(...)` helper. The implementation still avoids a managed `byte[]`; it opens two `FileStream` handles only after a nonzero fault mask is observed and only once per distinct fault mask.
Rejected Alternatives: Renaming the ID path back to the XML nickname was rejected because AGENTS requires `Dump_[ID].bin`. Writing a managed staging buffer once and then duplicating it was rejected because the native span can be streamed directly. Ignoring the XML alias was rejected because task 16 explicitly names it.
Scalability potential: No normal-frame scalability impact. Low through Ultra paths share the same bounded fault export behavior; healthy frames execute no file work.
Hardware Impact: No hot-path cost. Fault path writes two 19.2 KB files for the 300-entry ring, which is acceptable because the process is already in forensic mode.

## 2026-05-19 - Compile Guard Recheck 24 After Dual Dump Patch

Problem: The dual dump-path source patch requires compiler proof, but the explicit build guard is still red.
Solution: Re-ran targeted KCC forbidden-pattern scan and build guard. The scan returned no matches for `CharacterController`, synchronous casts, `Rigidbody.AddForce`, private Native containers, `Pack=1`, `UnityEngine.Random`, `foreach`, `.Complete(`, scene finders, `Camera.main`, or string formatting under `Assets/_Project/Scripts/Physics/KCC`. No active `dotnet` or `csc` process was returned. Processor Time sampled `100, 100, 100, 100, 100`; Processor Utility sampled `78.09, 82.48, 82.21, 80.75, 80.51`. Build remains deferred.
Rejected Alternatives: Launching `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` at sustained 100% CPU was rejected by the hardware guard.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids adding compiler load to saturated workstation CPU.

## 2026-05-19 - Editor Telemetry View Tightening

Problem: The UI Toolkit tuner no longer retained a managed telemetry snapshot, but the velocity graph still called `TryReadEditorTelemetryVault(...)` once for cursor discovery and twice per telemetry point. Each call resolved the same Vault handles again. This is editor-only, but it weakens the facade proof and hides avoidable work in the human tuning surface.
Solution: Added `TryGetEditorTelemetryVaultView(...)` as an editor-only diagnostic seam that resolves the telemetry ring and cursor once when no KCC collision/post batch is scheduled. The graph now walks that single `NativeArray<KinematicTelemetryEntry>` view for max-speed and line generation. The existing per-index read method remains for compatibility and delegates through the view seam.
Rejected Alternatives: Keeping per-point Vault resolves was rejected because the graph can paint up to 300 entries and would repeat the same handle resolution hundreds of times. A managed snapshot array was rejected because it violates the H-PHI proof. Completing jobs from the editor facade was rejected because diagnostics must not perturb scheduler ownership.
Scalability potential: Low through Ultra runtime behavior is unchanged. Editor graph cost scales linearly over the fixed 300-entry ring with one Vault resolve pair per repaint; denser future telemetry can reuse the same view seam without adding private arrays.
Hardware Impact: Editor-only. For the current 300-frame graph, the patch removes up to 598 repeated telemetry/cursor handle resolves per repaint while preserving zero runtime cost when the tuner is closed.

## 2026-05-19 - Compile Guard Recheck 25 After Editor View Patch

Problem: The editor telemetry view patch changed runtime/editor source, so compiler proof is required, but the explicit CPU guard remains red.
Solution: Re-ran the targeted KCC forbidden-pattern scan and `git diff --check`; the scan returned no matches, and diff check reported only CRLF normalization warnings. No active `dotnet` or `csc` process was returned. Processor Time sampled `100, 100, 100, 97.28, 100`; Processor Utility sampled `81.2, 82.4, 79.39, 74.96, 75.27`. Build remains deferred.
Rejected Alternatives: Launching `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` while CPU samples exceed 50% was rejected by AGENTS hardware guard.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids adding compiler load to saturated workstation CPU.

## 2026-05-19 - Compile Guard Recheck 26 After Documentation Update

Problem: Logs and architecture docs were updated after the editor telemetry view patch. Source still requires compiler proof, but the build guard must be rechecked before any compile attempt.
Solution: Re-ran targeted KCC forbidden-pattern scan and the build guard. The scan returned no matches. No active `dotnet` or `csc` process was returned. Processor Time sampled `100, 100, 100, 100, 100`; Processor Utility sampled `80.78, 79.16, 84.72, 81.9, 80.45`. Build remains deferred.
Rejected Alternatives: Launching `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` because compiler-family processes were absent was rejected; CPU still exceeds the explicit 50% threshold.
Scalability potential: Not runtime logic.
Hardware Impact: Avoids adding compiler load to saturated workstation CPU.

## 2026-05-19 - SIMD Magnitude Guard Pass

Problem: A post-compaction mandate re-read found remaining `math.length(...)` calls in KCC authority-adjacent jobs and a `.normalized` editor gizmo path. Those are guarded against NaN by surrounding logic, but they still violate the i3/ARM rsqrt law's default magnitude shape and leave scalar sqrt selection to the compiler.
Solution: Added `HydrodynamicKccMath.LengthSafe(float3)` using `lenSq * math.rsqrt(math.max(lenSq, 0.000001f))` with finite/zero guards. Replaced KCC drag speed, wake speed, capsule cast distance, resolver displacement length, telemetry aggregate speed, visual output speed, wake magnitude fallback, and the gizmo normal display path with the safe helper or `NormalizeSafe`.
Rejected Alternatives: Keeping `math.length` was rejected because the local mandate explicitly prefers rsqrt form. Using `math.sqrt(math.max(lenSq, epsilon))` was rejected because it preserves the scalar sqrt path. Replacing gameplay speed with a dominant-axis visual approximation was rejected because drag/resolution authority needs stable magnitude, not a cheap visual-only proxy.
Scalability potential: Low/i3 and mobile use the same deterministic magnitude helper and avoid unguarded sqrt; middle/high/ultra can spend the saved ALU budget on the existing continuous hit budget and wake metadata without a binary branch.
Hardware Impact: Expected microsecond gain is small per entity, but the helper removes repeated scalar length paths from integration, collision build, resolution, telemetry, visual sync, and wake emission. Profiler/Burst Inspector proof is still pending because build/import cannot run under the CPU guard.

## 2026-05-19 - Compile Guard Recheck 27 After SIMD Magnitude Patch

Problem: The SIMD magnitude source patch requires compiler proof, but the explicit build guard must pass before invoking `dotnet`.
Solution: Re-ran the targeted KCC forbidden-pattern scan, `git diff --check`, and the build guard. The scan returned no matches for synchronous casts, legacy controller/rigidbody force routes, private Native container construction, `Pack=1`, random sources, hot foreach/completion, scene finders, string formatting, `math.length`, `.normalized`, or sqrt calls under `Assets/_Project/Scripts/Physics/KCC`. `git diff --check` reported only CRLF normalization warnings. No active `dotnet` or `csc` process was returned. Processor Time sampled `100, 100, 100, 100, 100`; Processor Utility sampled `82.63, 80.32, 75.62, 76.2, 77.37`.
Rejected Alternatives: Launching `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` during sustained CPU saturation was rejected by AGENTS. Marking compile closure from static scans was rejected.
Scalability potential: Not runtime logic. It protects the workstation while preserving the proof trail.
Hardware Impact: Avoids compiler contention on a saturated machine; runtime patch impact remains pending Burst/player proof.

## 2026-05-19 - Recheck 27 Audit Reanchor

Problem: The final agent log needed a bottom audit after the SIMD magnitude pass. The first prompt extraction command used a regex that matched only a bare `id` tag and missed the live tag because `CURRENT_BATCH.md` includes `role` and `chat_name` attributes.
Solution: Re-extracted the SHINOBU_113 assignment from `CURRENT_BATCH.md:747-783` with attribute-tolerant matching and appended a new bottom `SELF_AUDIT` block to `Docs/AgentLogs/LOG_SHINOBU_113.md` with Recheck 27 static evidence, struct layout math, Vault handles, dependency graph, compile guard status, and Dear Lie complexity.
Rejected Alternatives: Leaving the previous editor-view audit as the bottom entry was rejected because it predates the rsqrt source change. Treating the strict-regex miss as a missing prompt was rejected after `rg` proved the live tag at line 747.
Scalability potential: Documentation only. The audit records the existing continuous quality curve: two to eight hit records, smoothing/wake/rollback load lerps by `GlobalQualityWeight`, and no binary device switch.
Hardware Impact: No runtime change. It preserves the integrator's proof trail while compile remains blocked by CPU guard.

## 2026-05-19 - True Bottom Recheck 27 Anchor

Problem: The first Recheck 27 log append matched an earlier compile-guard section, so the true file bottom still showed Recheck 26. That violates the project reporting protocol even if the audit content existed above.
Solution: Appended `PASS_STATIC_PENDING_COMPILE_RSQRT_BOTTOM_RECHECK27` after the final `Bottom Compile Guard Recheck 26` section. Re-ran targeted KCC scans; both returned no matches. Re-ran the build guard: no active `dotnet/csc`; Processor Time `100, 100, 100, 100, 100`; Processor Utility `84.32, 82.31, 78.46, 80.66, 83.59`. Build remains deferred.
Rejected Alternatives: Leaving the newer audit above older tail content was rejected because the CTO reads the bottom of this log. Deleting historical duplicate audit blocks was rejected in this pass because removing old evidence is higher risk than appending a correct bottom anchor.
Scalability potential: Documentation only; runtime scaling remains the continuous 2..8 hit budget, rsqrt magnitude path, scalar turbulence, and quality-lerped visual smoothing/rollback budget.
Hardware Impact: No runtime impact. It avoids an invalid reporting state without launching a build on a saturated machine.
