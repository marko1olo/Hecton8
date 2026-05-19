# Rationale_SHINOBU_105

Status: PENDING VERIFICATION
Batch: Docs/Tasks/CURRENT_BATCH.md

## Initial Technical Position

Problem: The task demands visual hydration of macro biomass into up to 100,000 fish without per-fish GameObjects, while also consuming Abyssal Flow 3D tensors and keeping deterministic lockstep behavior.
Solution: Build a data-only swarm route: explicit DTOs, double-buffered boid state, spatial hash built in Burst, flow sampling as read-only data, camera-relative matrix generation, GPU indirect rendering, and a fixed 300-frame telemetry ring.
Rejected Alternatives: Unity ParticleSystem, VFX Graph collision, per-fish MonoBehaviour `Update`, SkinnedMeshRenderer/Animator, Unity Physics broadphase, and raw absolute float world coordinates. These burn CPU, lose determinism, or violate AUP.
Scalability potential: Low uses sparse hydrated population, flow-follow only, low neighbor cap. Middle adds separation and sparse alignment. High adds full Reynolds steering with larger visible range. Ultra uses dense visual hydration and richer shader/VAT detail. No binary quality switch; all curves must consume `GlobalQualityWeight`.
Hardware Impact: On i3/MX350 the target is avoiding GameObject/Animator CPU cost and PCIe stalls; estimated savings are orders of magnitude against 100,000 GameObjects, but no profiler number exists yet. Status remains PENDING VERIFICATION.

## Pre-Code Mandate Decisions

Problem: Spatial neighbor search can dominate cost if implemented via managed lists or Physics queries.
Solution: Use fixed-capacity cell buckets or NativeParallelMultiHashMap built in PRE_SIMULATION, with bounded neighbor caps and squared distance checks.
Rejected Alternatives: Physics.OverlapSphere, managed neighbor lists, and unbounded per-boid neighbor scans.
Scalability potential: Low increases stride and reduces neighbor tests; Ultra lowers stride and widens neighbor caps.
Hardware Impact: Expected MX350/i3 gain comes from replacing O(N^2) or Physics broadphase with bounded O(N) grid build plus local sampling.

Problem: Large-world rendering can jitter if absolute world positions are cast to float.
Solution: Store authority as AUP sector/local data or double3-compatible local deltas, subtract camera/player AUP first, then cast to float for matrices.
Rejected Alternatives: `Transform.position` authority and shader-side global offset accumulation.
Scalability potential: Low only hashes key positions in telemetry; Ultra records dense AUP/matrix samples.
Hardware Impact: Prevents visual instability without expensive double math in shaders.

Problem: Flow-field dependency from another agent may not exist at compile time.
Solution: Provide deterministic emergency mock flow data and use owner-local/read-only seam until the actual GlobalDataVault flow route is available.
Rejected Alternatives: Direct concrete dependency on Agent 63/Weather implementation or blocking compile on missing assets.
Scalability potential: Low samples coarse flow cells; Ultra samples denser tensors and shader turbulence.
Hardware Impact: Keeps SHINOBU_105 compiling independently in a 20+ agent batch.

## Loop 1 Decisions - Tasks 01-05

Problem: Legacy fish simulation routes could hide per-fish GameObject or ParticleSystem attempts.
Solution: Scanned prefabs/scenes/scripts for `ParticleSystem`, VFX, `HectonBoidController`, `BoidController`, `FishAI`, `FishSpawner`, and the `HectonBoidController` GUID. Fish-specific prefab references to the old controller were absent; ParticleSystem hits were construction/world support/camera/silt effects, not fauna swarm authority.
Rejected Alternatives: Deleting broad ParticleSystem assets by keyword. That would damage unrelated construction, cave, silt, and UI systems outside SHINOBU_105 authority.
Scalability potential: Low through Ultra use one data-owned swarm route; unrelated one-shot particles remain outside fish population authority.
Hardware Impact: No hot-path gain from deleting unrelated assets; avoiding false deletion prevents cross-domain regressions.

Problem: Hot-path boid state needed explicit ARM64-safe layout.
Solution: Added `BoidStateDTO` at exactly 32 bytes: `double3 AUP` offset 0 size 24, `ushort SpeciesID` offset 24 size 2, `ushort PackIndex` offset 26 size 2, `float Speed` offset 28 size 4. Added `BoidMatrixDTO` as four `float4` columns at offsets 0/16/32/48, exactly 64 bytes.
Rejected Alternatives: `float4x4` as the Vault storage type and property-backed state DTOs. They hide layout intent and make byte-offset proof weaker.
Scalability potential: Low pays 32-byte state reads only; Ultra can stream contiguous 64-byte matrices to GPU without reading logic state.
Hardware Impact: On i3/MX350, separating state from matrix data avoids dragging 64B render payload into L1 during flocking.

Problem: Real Abyssal Flow tensor data may be absent during batch integration.
Solution: Added `GenerateEmergencyMockFlowJob` and `SampleEmergencyMockFlow`: deterministic triangle/curl-style flow fake seeded by sector/index and quality. This keeps Burst steering testable without Agent 63 assets.
Rejected Alternatives: Direct dependency on a concrete weather/fluid assembly or true Navier-Stokes. Both violate compile isolation and frame-time discipline.
Scalability potential: Low collapses to coarse flow-follow; Ultra increases strength/turbulence and keeps richer curl cues.
Hardware Impact: Expected low-end win is replacing missing/expensive fluid simulation with O(N) algebra and no texture dependency.

## Loop 2 Decisions - Tasks 06-10

Problem: Flocking must be deterministic under parallel scheduling and avoid Unity Physics.
Solution: Renamed the existing steering kernel to `BoidFlockingJob`; it reads `EntitySnapshot`/`AupSnapshot`, writes `Entities`/`Aups`, uses a custom bucket spatial hash, and marks NativeArray fields with `[NoAlias]`.
Rejected Alternatives: `Physics.OverlapSphere`, managed neighbor lists, and in-place update of current-frame state.
Scalability potential: Low caps neighbor samples at 4 and collapses alignment/cohesion; Middle/High/Ultra widen neighbor and chain budgets continuously through `GlobalQualityWeight`.
Hardware Impact: On i3/MX350 this preserves O(N) grid build plus bounded local scans instead of O(N^2) flocking.

Problem: Wall avoidance can become pathfinding or fluid simulation if overbuilt.
Solution: Implemented Dear Lie wall response: SDF sample selects a normal, then boids receive `cross(up, wallNormal)` vortex force plus a small normal push.
Rejected Alternatives: Navier-Stokes, mesh colliders, raycasts, or voxel pathfinding.
Scalability potential: Low uses the same cheap swirl at low strength; Ultra increases swirl strength for richer cave-school motion.
Hardware Impact: Replaces obstacle solve with a few vector ops per near-wall boid.

Problem: Matrix upload must not allocate managed arrays.
Solution: Added `BoidMatrixDTO` Vault payload and `TryUploadRenderMatricesToGpu()` using `GraphicsBuffer.LockBufferForWrite` plus `UnsafeUtility.MemCpy`. Added `TryUploadIndirectDrawArgs()` for active-count draw arguments.
Rejected Alternatives: `Matrix4x4[]`, `SetData` from managed arrays, or per-instance renderer submission.
Scalability potential: Low uploads 1k budget; Ultra streams 100k contiguous 64B matrices.
Hardware Impact: Avoids managed matrix array allocation and reduces PCIe/driver overhead by writing mapped buffer memory directly.

## Loop 3 Decisions - Tasks 11-15

Problem: Binary low/high switches would create population pops and thermal cliffs.
Solution: Active boid budget, neighbor sample cap, spatial chain cap, update stride, visible-cone threshold, flow strength, and Reynolds alignment/cohesion all consume `HomeostasisBrain.GlobalQualityWeight` through smooth polynomial curves.
Rejected Alternatives: `if lowEnd` branches or fixed 5k/100k tiers.
Scalability potential: Low simulates about 1k flow-following fish with sparse updates; Middle widens neighbor caps; High/Ultra reaches 100k and full Reynolds.
Hardware Impact: Low-end avoids most neighbor scans and updates; high-end spends saved cycles on dense school motion.

Problem: Predator reaction must not use colliders.
Solution: Existing signal route consumes fauna strike signals and `MockPredatorSignal`, converts predator AUP to camera-local space, and applies inverse steering inside `BoidFlockingJob`.
Rejected Alternatives: trigger colliders, raycasts, or per-predator GameObject queries.
Scalability potential: Low still applies the panic impulse; Ultra combines it with full flocking for bait-ball shatter.
Hardware Impact: Radius check is squared-distance math in Burst; no physics scene query.

Problem: Rollback deterministic flocking cannot read partially updated neighbor state.
Solution: `LocalShiftAndSpatialHashJob` writes `EntitySnapshot`/`AupSnapshot`; `BoidFlockingJob` reads snapshots and writes next `Entities`/`Aups`.
Rejected Alternatives: in-place current-frame neighbor reads.
Scalability potential: Same deterministic topology across all quality weights.
Hardware Impact: Extra snapshot storage buys order independence and avoids main-thread synchronization.

Problem: Indirect draw count must not be produced by CPU loops.
Solution: Added `BoidIndirectArgsDTO` and `WriteBoidIndirectArgsJob`; also provided `TryDrawProceduralIndirect()` as the render submission shim.
Rejected Alternatives: one draw call per fish, managed args arrays, or CPU-side visible list construction.
Scalability potential: Low writes 1k instances; Ultra writes 100k without changing submission logic.
Hardware Impact: Decouples visible population from CPU draw-call generation.

## Loop 4 Decisions - Tasks 16-20

Problem: Massive buffers were using zero-fill where first-frame jobs overwrite the payload.
Solution: Changed entity, AUP, render matrix, and render custom-data Vault allocations to `NativeArrayOptions.UninitializedMemory`.
Rejected Alternatives: ClearMemory for 100k matrices and state rows.
Scalability potential: Larger Ultra capacity no longer multiplies cold zero-fill cost.
Hardware Impact: Avoids clearing roughly 6.4MB of matrix data plus state payload on boot.

Problem: Designers need cold tuning without recompiling C#.
Solution: Existing CSV byte-scratch parser remains allocation-free for tuning; added `ParseSwarmSpeciesProfiles()` for `swarm_species_profiles.csv` into `SwarmSpeciesProfileDTO`.
Rejected Alternatives: `string.Split`, `List<T>`, managed dictionaries.
Scalability potential: Low/Ultra share the same profile table; quality only changes how many rows are hydrated/rendered.
Hardware Impact: Cold-only parser avoids gameplay GC and supports content iteration.

Problem: Runtime debug visualization must not require a swarm GameObject.
Solution: Editor window now exposes an `Abyssal Swarm Tuner` UI Toolkit host, telemetry graph, hash-grid toggle, and SceneView vector drawing for flow and subset boid forward vectors.
Rejected Alternatives: runtime debug GameObject with `OnDrawGizmos`.
Scalability potential: Editor-only diagnostics; no player-frame overhead.
Hardware Impact: Zero runtime cost when editor facade is closed.

## Loop 5 Review - Compile Gate

Problem: The mandatory compile verification cannot run while total CPU is above 50%.
Solution: Checked `dotnet/csc` process list and total CPU repeatedly. No `dotnet`/`csc` was active, but total CPU stayed at ~98-100%, so no build was launched.
Rejected Alternatives: violating the user's CPU gate or killing unrelated high-CPU processes.
Scalability potential: Not applicable to runtime; this protects developer machine responsiveness during multi-agent batch execution.
Hardware Impact: Prevents adding build load on an already saturated workstation.

Problem: Source-only review needed to catch obvious rule breaks while compile is blocked.
Solution: Ran static scans for `Pack=1`, hot-path `foreach`, `Instantiate`, `new GameObject`, managed matrix arrays, old job names, and missing Burst directive patterns in the touched SHINOBU files.
Rejected Alternatives: waiting idle for CPU to drop without checking the patch.
Scalability potential: Ensures the changed files keep the Zero-GC/DataVault posture before compile.
Hardware Impact: No runtime impact; reduces chance of one compile wall iteration when build becomes legal.

## Loop 6 Decisions - Pointer Hardening

Problem: Defining aligned DTOs without enforcing pointer-level mutation in the hottest jobs leaves the CS1612 mandate only partially satisfied.
Solution: Converted `LocalShiftAndSpatialHashJob`, `BoidFlockingJob`, `QueryNeighbors`, and `BuildShinobuRenderPayloadJob` to unsafe pointer row access through `NativeArrayUnsafeUtility` plus `UnsafeUtility.AsRef`. The write rows are mutated by ref; read-only snapshots/buckets use read-only pointers where applicable.
Rejected Alternatives: relying on `NativeArray<T>[index]` value copies as the final steering contract, or adding properties around hot structs.
Scalability potential: Low-quality sparse updates still take the same direct row path; Ultra-quality 100k solves avoid hidden per-row property/method access and preserve Burst vectorization opportunities.
Hardware Impact: On i3/MX350-class CPUs this reduces defensive-copy risk in the steering/render path; exact microseconds remain pending because the CPU gate still blocks build/profiler runs.

Problem: The compile gate needed a non-WMI CPU check because `Get-CimInstance Win32_Processor` was denied.
Solution: Used `Get-Counter '\Processor(_Total)\% Processor Time'`, which returned 100%, and confirmed `dotnet`/`csc` were absent.
Rejected Alternatives: escalating WMI access or launching `dotnet build` while total CPU is saturated.
Scalability potential: Not runtime-affecting; protects multi-agent workstation throughput.
Hardware Impact: Prevents build load from competing with current 100% CPU work.

## Loop 7 Decisions - Determinism Hardening

Problem: The steering path still derived lane hashing, macro RNG frame, and telemetry frame from Unity `Time.frameCount`, and integration used the dispatcher `deltaTime` parameter. That is not acceptable for rollback-safe simulation authority.
Solution: Added a local `_simulationFrameCounter`, advanced only when the swarm schedule is accepted, and routed flocking lane hash, macro frame input, and telemetry frame through it. Replaced variable integration delta with a deterministic fixed tick curve: `1/60s` at full quality, smoothly expanding toward `12/60s` when quality collapses.
Rejected Alternatives: using Unity `Time.frameCount`, Unity `Time.deltaTime`, or unscaled frame time as critical simulation state.
Scalability potential: Low-quality devices still reduce work through active budget/update stride while remaining deterministic; Ultra receives the same 1/60s integration cadence with higher density and neighbor budgets.
Hardware Impact: Runtime cost is negligible; determinism prevents rollback divergence and replay-only defects that would otherwise be expensive to diagnose.

## Loop 8 Decisions - GPU Args Guard

Problem: `TryUploadIndirectDrawArgs()` could attempt `LockBufferForWrite` on a buffer with the wrong stride or zero count if an integrator passed the wrong GPU resource.
Solution: Added explicit `destination.count >= 1` and `destination.stride == GraphicsBuffer.IndirectDrawIndexedArgs.size` guards before mapping.
Rejected Alternatives: assuming the caller always creates the correct indirect-args buffer.
Scalability potential: Low through Ultra share the same draw-args ABI; the guard prevents a bad integration from becoming a runtime exception under 100k-instance load.
Hardware Impact: One cold branch before upload; avoids driver/API failure on invalid buffer wiring.

## Loop 9 Decisions - Species CSV Vault Route

Problem: `swarm_species_profiles.csv` parsing existed as a static parser, but the cold path was still resolving species CSV through the tuning CSV route and did not persist the profile lookup in the Vault.
Solution: Added `BufferID.ShinobuSwarmSpeciesProfiles` and `_swarmSpeciesProfileHandle`, allocated 64 unmanaged `SwarmSpeciesProfileDTO` rows through `GlobalDataVault`, split tuning CSV resolution from species CSV resolution, and wired a cold monitor to parse `swarm_species_profiles.csv` into the Vault lookup. Header-like rows are ignored and stale profile slots are cleared after shorter reloads.
Rejected Alternatives: using the tuning DTO as a species/profile carrier, parsing into managed dictionaries, or leaving the parser as an uncalled utility.
Scalability potential: Low devices hydrate fewer boids but still use the same profile table; Ultra can map dense biomass sectors to mesh/material hashes without C# recompiles.
Hardware Impact: Cold-path only. It removes any future temptation to create managed lookup tables during gameplay and keeps 0 B/frame species resolution as the contract.

## Loop 10 Decisions - Indirect Args Vault Route

Problem: Task 14 was only partially proven: `BoidIndirectArgsDTO`, a Burst writer job, and upload helpers existed, but the runtime frame chain did not own a dedicated Vault buffer for the draw-args row.
Solution: Added `BufferID.ShinobuBoidIndirectArgs`, `_indirectArgsHandle`, one-row `BoidIndirectArgsDTO` Vault allocation, job-buffer lock/unlock coverage, and a scheduled `WriteBoidIndirectArgsJob` after telemetry counting. Added a `TryUploadIndirectDrawArgs(GraphicsBuffer, NativeArray<BoidIndirectArgsDTO>)` bridge so the GPU-facing indirect buffer can be updated from the Vault row without managed arrays.
Rejected Alternatives: leaving indirect args as a helper-only utility, building managed `uint[]`/`GraphicsBuffer.IndirectDrawIndexedArgs[]` upload arrays, or letting render submission compute instance counts by CPU loops.
Scalability potential: Low writes 1,000 instances into the same row; middle/high/ultra write the continuously scaled budget up to 100,000 without changing submission topology.
Hardware Impact: One 32B row write in Burst replaces any future CPU-visible instance-list construction. Measured microseconds remain pending because the CPU gate still blocks build/profiler execution.

## Loop 11 Decisions - BoidState Vault Pointer Route

Problem: `BoidStateDTO` existed and was layout-asserted, but the first runtime path still used `AmbientEntityDTO`/`AmbientEntityAupDTO` as the only hot mutable state. That made the CS1612 mandate weaker than the XML assignment, which explicitly required `BoidStateDTO*` iteration.
Solution: Added `BufferID.ShinobuBoidStates` and `BufferID.ShinobuBoidStateSnapshot`, allocated both as 100,000-row unmanaged Vault buffers with `UninitializedMemory`, and routed `LocalShiftAndSpatialHashJob`, `BoidFlockingJob`, and `BuildShinobuRenderPayloadJob` through raw `BoidStateDTO*` rows. Local shift now snapshots `double3` AUP/speed/species; flocking reads the previous `BoidStateDTO` AUP, writes the next state, and render payload subtracts camera AUP from the `double3` state before downcasting to float.
Rejected Alternatives: keeping `BoidStateDTO` as documentation-only proof, storing only `AbsoluteUniversePosition` metadata, or wrapping hot state behind properties.
Scalability potential: Low quality still writes sparse direct state rows; ultra quality streams the full 100,000-row state/snapshot pair without changing code shape.
Hardware Impact: Adds 6.4MB total for state+snapshot at 100,000 boids (32B x 2 x 100,000) to buy explicit assignment compliance and cache-readable state. Exact speed impact is pending build/profiler; the old ambiguity is removed.

## Loop 12 Decisions - Center AUP Hot-Path Precompute

Problem: After routing render/flocking through `BoidStateDTO.AUP`, both jobs could recompute `ToAbsoluteDouble3(CenterAup)` per boid.
Solution: Compute camera absolute `double3` once in the scheduler and pass it into `BoidFlockingJob` and `BuildShinobuRenderPayloadJob` as `CenterAbsolute`.
Rejected Alternatives: leaving repeated double-sector reconstruction inside 100,000 parallel iterations.
Scalability potential: Low devices avoid unnecessary repeated double math in sparse updates; ultra avoids two redundant 100,000x center reconstructions when full density is active.
Hardware Impact: Saves a small but deterministic amount of ALU on every scheduled frame. Exact microseconds remain pending because CPU is still above the build/profiler threshold.

## Loop 13 Decisions - Build Gate Attempt

Problem: Compile verification was pending behind the user's CPU/dotnet gate.
Solution: Used a guarded PowerShell loop that sampled CPU and dotnet/csc processes before invoking `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal`. It ran only when CPU sampled at 35.5% and no dotnet/csc process was active.
Rejected Alternatives: launching build while CPU was above 50%, launching build while another compiler was active, or editing unrelated deleted files to satisfy the generated project.
Scalability potential: Not runtime-affecting; this preserves multi-agent workstation throughput.
Hardware Impact: Build failed before SHINOBU code verification with `CS2001` on missing external source `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`. The file is deleted in the current worktree and outside SHINOBU_105 ownership, so this is a dependency blocker, not a SHINOBU compile result.

## Loop 14 Decisions - GPU ABI And Job-Fence Polish

Problem: Task 14 still carried an indexed-style 32B indirect row while the assignment specifically names `Graphics.DrawProceduralIndirect`, whose argument ABI is four uints. The render bridge also existed mostly as static helper seams, not as a cold-owned double-buffered GPU resource owner.
Solution: Rebuilt `BoidIndirectArgsDTO` as a 16B explicit layout: vertex count, instance count, start vertex, start instance. Added `ShinobuBoidGpuUploadDispatcher`, which owns double-buffered `GraphicsBuffer` resources for 64B matrices, float4 custom shader lanes, and 16B procedural args, then uploads from Vault with `LockBufferForWrite` and `UnsafeUtility.MemCpy`.
Rejected Alternatives: Keeping `GraphicsBuffer.IndirectDrawIndexedArgs` for a procedural draw, uploading managed argument arrays, or pretending a static helper was equivalent to runtime GPU ownership.
Scalability potential: Low writes and draws the same 16B ABI for about 1,000 instances; middle/high/ultra increase only `InstanceCount` and matrix/custom upload count up to 100,000. Shader/VAT richness can scale independently through the custom float4 lane and global quality weight.
Hardware Impact: The ABI cut reduces the indirect args row from 32B to 16B and removes future confusion between indexed mesh submission and procedural submission. Measured GPU/CPU impact remains pending; current project build remains blocked by the unrelated missing world source.

Problem: `LotkaVolterraMacroJob.Run()` executed synchronously in `ColdTick`, violating the job-fence policy even though it was not a per-frame `Tick`.
Solution: Schedule the macro job into `_activeJobHandle`, mark it as a macro pipeline, and let `LateFrameTick` finish it through `DispatcherJobSwap.TryComplete` under the existing swap-window discipline. Also moved DataVault lookup to the cold activation path so `Tick`/`ColdTick` call `EnsureVaultState()` against a cached `_dataVault` only.
Rejected Alternatives: Leaving the synchronous `.Run()` because it is cold, or calling `.Complete()` immediately after scheduling.
Scalability potential: Low through Ultra keep macro biomass hydration off the immediate caller stack. The active boid budget still scales continuously from sparse hydration to full-density visual overkill.
Hardware Impact: Avoids a cold-tick main-thread stall source. Exact microseconds are unmeasured because compile/profiler proof is still blocked.

Problem: The emergency flow mock was too easy to misread as a plain triangle-wave placeholder rather than the required deterministic Perlin-style current test field.
Solution: Kept the cheap triangle base for low quality, then blended in deterministic trilinear value-noise samples through `GlobalQualityWeight` for high-quality Perlin-style current richness. This remains a visual fake, not Navier-Stokes.
Rejected Alternatives: Real fluid simulation, runtime texture dependency on another agent, or shader-only smoke without CPU steering input.
Scalability potential: Low stays mostly coarse triangle/curl flow; middle/high/ultra add richer value-noise variation for schooling motion without changing ownership.
Hardware Impact: Extra ALU only buys visual current richness; no measured timing yet. The branchless blend keeps determinism stable across clients.

## Loop 15 Decisions - Static Verification And Build Gate

Problem: After Loop 14, compile verification was still required, but the previous missing-source blocker may have changed because `Hecton8.Core.csproj` no longer reports the stale world-file include. Launching a build at 100% CPU would violate the user gate.
Solution: Re-extracted the exact SHINOBU_105 XML block, re-ran static scans against the touched SHINOBU runtime/editor files, and guarded the build with CPU/compiler checks. The probe reported CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Running `dotnet build` under saturated CPU, treating an old missing-source failure as current proof without checking the generated project, or editing unrelated world-domain files.
Scalability potential: Not runtime-affecting. It preserves workstation responsiveness while keeping the source-level proof current.
Hardware Impact: Avoided adding compiler load to a saturated machine. Runtime microseconds remain unmeasured because the build/profiler gate is still closed.

## Loop 16 Decisions - Vault Boot Gate And Failure Path

Problem: `EnsureVaultState()` still executed `GlobalDataVault.GetBufferHandle` from `Tick` and `ColdTick` after the buffers were already created. Current Vault implementation sanitizes existing buffers on handle reacquisition, so a nominal readiness check could become a hidden O(N) scan over 100,000-row SHINOBU buffers.
Solution: Added `_vaultBuffersReady` and `AreVaultHandlesCreated()`. Runtime calls now short-circuit after validating handle creation and minimum lengths; `GetBufferHandle` remains a boot/hot-swap/recovery operation only. Reset and dispose clear the readiness flag.
Rejected Alternatives: Trusting repeated `GetBufferHandle` as cheap, or adding a local persistent NativeArray cache. The first hides hot-path work; the second violates DataVault ownership.
Scalability potential: Low devices avoid accidental per-frame sanitize scans while running sparse updates. Middle/high/ultra keep the same Vault-owned buffers and scale only through quality-weighted active count and math LOD.
Hardware Impact: Prevents a potential 100,000-row buffer sanitize route from executing during normal ticks. Exact microseconds remain unmeasured until build/profiler verification is legal.

Problem: The frame and macro schedule blocks caught `Exception`, unlocked job buffers, then rethrew. That protects locks but still converts a recoverable schedule failure into a gameplay crash with no SHINOBU telemetry marker.
Solution: Replaced the schedule-path rethrows with `GlobalTelemetryBus.PublishPerformanceWarning` using numeric SHINOBU hashes, then return naturally after buffer unlock.
Rejected Alternatives: Swallowing failure silently, throwing through gameplay, or allocating exception text/log strings.
Scalability potential: Same across low/middle/high/ultra; failure reporting is numeric and allocation-free.
Hardware Impact: No normal-frame cost beyond the existing try/catch boundary; failure path keeps the black-box route alive for postmortem analysis.

Problem: Build verification became eligible to probe after code changes, but the explicit workstation rule forbids build execution above 50% CPU or while another compiler is running.
Solution: Ran the guarded build probe. It reported CPU=64.3 and compiler_count=0, so no `dotnet build` was launched.
Rejected Alternatives: Violating the CPU gate to get a compile result, or treating static scans as a compiler substitute.
Scalability potential: Not runtime-affecting; it protects multi-agent workstation throughput.
Hardware Impact: Avoided adding compiler load while the machine was above the allowed CPU threshold.

## Loop 17 Decisions - Procedural Args Target Hardening

Problem: The double-buffered indirect args resource was created with `GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw` while using a 16-byte `BoidIndirectArgsDTO` stride. Some Unity backend validation treats Raw buffers as 4-byte lanes, so the extra Raw flag could create platform/version-specific failure even though the procedural ABI row itself is correct.
Solution: Removed `GraphicsBuffer.Target.Raw` from `CreateIndirectArgsBuffer()`. The buffer is now `Target.IndirectArguments` with one 16B `BoidIndirectArgsDTO` element, matching `DrawProceduralIndirect`.
Rejected Alternatives: Returning to `GraphicsBuffer.IndirectDrawIndexedArgs`, packing the args as four independent uint lanes, or allocating a managed upload array before draw submission.
Scalability potential: Low/middle/high/ultra all write the same 16B args row; only `InstanceCount` changes with `GlobalQualityWeight`.
Hardware Impact: Prevents a driver/API validation branch from killing the GPU path. Runtime microseconds are not claimed; this is ABI correctness.

Problem: Build verification was attempted again after the target correction, but the CPU gate closed.
Solution: Guarded probe reported CPU=98.9 and compiler_count=0, so `dotnet build` was not launched.
Rejected Alternatives: Launching the compiler under saturated CPU.
Scalability potential: Not runtime-affecting.
Hardware Impact: Avoided adding compiler pressure to an already saturated workstation.

## Loop 18 Decisions - Schedule Failure Ownership

Problem: The frame and macro schedule catch blocks were safer than rethrowing, but still had a hidden corruption edge. If any `Schedule()` succeeded and a later scheduling/register step threw before `_jobScheduled` and `_jobLocksHeld` were published, the catch could unlock Vault buffers while Burst jobs were still using them.
Solution: Added local `scheduledHandle` and `scheduledWork` tracking in both frame and macro scheduling paths. After each successful schedule, the latest handle is retained. On exception, scheduled work is preserved through `_activeJobHandle`, `_jobScheduled`, and `_jobLocksHeld` so late-frame recovery owns completion and unlock. Only pre-schedule failures unlock immediately.
Rejected Alternatives: Assuming schedule/register exceptions never happen, or forcing `Complete()` in the catch. The first is architectural denial; the second violates dispatcher swap-window ownership.
Scalability potential: Same across low/middle/high/ultra; this is memory ownership correctness around the same quality-scaled job graph.
Hardware Impact: No normal-frame allocation or collection added. Failure-path behavior prevents Vault reuse races and keeps recovery in the dispatcher lane.

Problem: Loop 18 needed verification after the ownership patch.
Solution: Re-ran critical static scans, Burst directive count, `git diff --check`, and the guarded build probe. Static scans found no critical forbidden SHINOBU pattern; 9 job types still have 9 required Burst directives. Build probe reported CPU=100 and compiler_count=0, so no compiler was launched.
Rejected Alternatives: Launching build under saturated CPU or claiming profiler/compile proof from static scans.
Scalability potential: Not runtime-affecting.
Hardware Impact: Avoided compiler load while CPU was saturated; runtime microseconds remain unmeasured.

## Loop 19 Decisions - GPU Upload Route Wiring

Problem: The double-buffered GPU dispatcher existed, but `ShinobuEcosystemBalancer` did not own or invoke it. That left the render matrices and indirect args as Vault output only, making the actual GPU publication route incomplete.
Solution: Added one cold-owned `ShinobuBoidGpuUploadDispatcher` instance, prewarmed it from `EnsureVaultState()` outside batch/headless mode, and uploaded matrix/custom/indirect payloads from Vault after `DispatcherJobSwap` completed the frame job. The upload publishes `_H8ShinobuBoidMatrices`, `_H8ShinobuBoidCustomData`, and `_H8ShinobuBoidActiveCount`, and writes measured upload milliseconds into telemetry.
Rejected Alternatives: Inventing a material/draw owner inside SHINOBU, building managed matrix arrays, or allocating the GPU buffers lazily on the first dense gameplay frame.
Scalability potential: Low uploads the quality-capped active count; middle/high/ultra increase instance count continuously up to the prewarmed capacity. Shader/VAT richness remains a renderer/material concern through the published buffers.
Hardware Impact: This turns the existing 6.4MB matrix lane into an actual mapped GPU upload route without per-frame managed arrays. Runtime timing is now recorded, but not measured here because build/profiler remains gated.

Problem: Loop 19 needed verification.
Solution: Re-ran critical static scans, Burst directive count, `git diff --check`, and the guarded build probe. Static scan found no critical forbidden SHINOBU pattern; 9 job types still have 9 required Burst directives. Build probe reported CPU=96.3 and compiler_count=0, so no compiler was launched.
Rejected Alternatives: Launching build above the CPU gate.
Scalability potential: Not runtime-affecting.
Hardware Impact: Avoided compiler pressure under high CPU load.

## Loop 20 Decisions - GPU Buffer Lock Safety

Problem: `GraphicsBuffer.LockBufferForWrite` calls in the static upload helpers and dispatcher upload path did not use `finally`. If a driver call, pointer resolution, or unsafe copy threw after a successful lock, a GPU buffer could remain mapped.
Solution: Wrapped every SHINOBU `LockBufferForWrite` upload block with a lock flag and `finally`-guarded `UnlockBufferAfterWrite`. This covers render matrices, custom shader data, and indirect args.
Rejected Alternatives: Assuming `UnsafeUtility.MemCpy` and graphics-driver mapping cannot fail, or only hardening the newly wired dispatcher while leaving public helper seams unsafe.
Scalability potential: Same across quality weights; this is resource ownership safety for all active counts.
Hardware Impact: No allocation change. Failure-path safety improves; normal upload path adds only structured `finally` control flow around existing mapped writes.

Problem: Loop 20 needed verification and build-gate check.
Solution: Re-ran critical static scans, Burst directive count, `git diff --check`, and guarded build probe. Static scan found no critical forbidden SHINOBU pattern; 9 job types still have 9 required Burst directives. Build probe reported CPU=56.6 and compiler_count=0, so no compiler was launched because CPU remained above the <=50 threshold.
Rejected Alternatives: Launching build at 56.6% CPU.
Scalability potential: Not runtime-affecting.
Hardware Impact: Avoided violating the workstation CPU gate.

## Loop 21 Decisions - Procedural Shader Consumer

Problem: The runtime now publishes GPU buffers, but no SHINOBU-owned shader consumed `_H8ShinobuBoidMatrices` and `_H8ShinobuBoidCustomData`. That left visualization dependent on an undocumented external material.
Solution: Added `Assets/_Project/Art/Shaders/Hecton_AbyssalSwarmProcedural.shader`. It is a single-pass procedural URP shader using `SV_VertexID` and `SV_InstanceID`; it reconstructs column-major matrices from `BoidMatrixDTO`, draws one triangle silhouette per fish, and shades with custom species/quality lanes.
Rejected Alternatives: Adapting the older `BoidFishInstanced.shader` path that consumes `_BoidsBuffer`, adding shader keywords, adding a mesh dependency, or creating GameObjects/material ownership inside the runtime.
Scalability potential: Low/middle/high/ultra all consume the same global matrix/custom buffers; active count comes from the continuously scaled indirect args route.
Hardware Impact: Keeps vertex work to three vertices per fish for the Dear Lie silhouette path. No profiler number is claimed until Unity import/frame proof exists.

Problem: Loop 21 needed static verification and build-gate check.
Solution: Verified shader/global-buffer symbol alignment and absence of shader keyword variants in the new shader. Critical static scan found no forbidden SHINOBU pattern. Build probe reported CPU=100 and compiler_count=0, so no compiler was launched.
Rejected Alternatives: Claiming shader import proof without Unity import or launching build under saturated CPU.
Scalability potential: Not runtime-affecting.
Hardware Impact: Avoided compiler load at CPU saturation.

## Loop 22 Decisions - Procedural Material Asset

Problem: The procedural shader existed, but without a stable material asset the render owner would either guess the shader binding or create a material at runtime. Runtime material creation violates the cold asset/shader stutter rules and creates a managed-object path around the no-GameObject swarm route.
Solution: Added `Assets/_Project/Art/Materials/MAT_AbyssalSwarmProcedural.mat` plus `.meta`, bound to the SHINOBU shader GUID `7b6d4f2c9a2f4b94a2a9f7b9e8a10511`. The material has no keywords and uses the existing procedural shader properties, giving boot/warmup systems and render ownership a deterministic asset handle instead of a runtime allocation.
Rejected Alternatives: `new Material(shader)` inside the swarm runtime, `Resources.Load`, depending on an undocumented external material, or mutating a third-party/shared material.
Scalability potential: Low/middle/high/ultra share the same material asset while active count and shader lanes scale through `GlobalQualityWeight` and the `_H8ShinobuBoid*` buffers. Top-tier visual overkill stays in shader/VAT lanes without bloating CPU state.
Hardware Impact: No measured runtime savings claimed. The change blocks a future managed material allocation and shader first-use ambiguity; Unity import and warmup proof remain PENDING VERIFICATION.

Problem: Loop 22 needed verification and build-gate check.
Solution: Re-scanned shader/material/runtime buffer symbols, forbidden SHINOBU patterns, and `git diff --check`. The symbol scan confirmed material-to-shader GUID binding and `_H8ShinobuBoid*` usage; forbidden scan returned no hits; `git diff --check` reported CRLF normalization warnings only. Guarded build probe reported CPU=100 and compiler_count=0, so no compiler was launched.
Rejected Alternatives: Claiming Unity material import proof without Unity Editor import, or launching build under saturated CPU.
Scalability potential: Not runtime-affecting.
Hardware Impact: Avoided compiler pressure while total CPU was saturated.

## Loop 23 Decisions - Render Dispatch Seam

Problem: GPU upload and shader/material assets existed, but the active draw route was still private to `ShinobuBoidGpuUploadDispatcher`. External render ownership could not safely consume the uploaded matrix/custom/args buffers without either duplicating the dispatcher or guessing private state.
Solution: Added a cold render seam on `ShinobuEcosystemBalancer`: `BindProceduralRenderMaterial(Material, Bounds, int)` caches a caller-owned material asset and registers the service with `GlobalRegistry.Renderables`; `Render(float)` submits exactly one procedural indirect draw through the existing double-buffered dispatcher; `TryDrawUploadedSwarm()` and `TryGetUploadedSwarmBuffers()` expose explicit non-alloc integration seams for render graph or owner-local callers.
Rejected Alternatives: Runtime `new Material`, `Shader.Find`, `Resources.Load`, adding a swarm GameObject/MonoBehaviour render proxy, exposing the private dispatcher object, or moving render ownership into a sibling-domain concrete reference.
Scalability potential: Low/middle/high/ultra still scale through active count, update stride, and shader custom lanes. The render seam adds no quality tier branch; it consumes the same uploaded buffers and indirect args row at every quality weight.
Hardware Impact: No measured microseconds claimed. This removes the helper-only gap and prevents future per-frame/per-scene material allocation or duplicate GPU buffer ownership. On MX350 the submission remains one procedural indirect draw; on RTX the same route can carry 100,000 instances.

Problem: Loop 23 needed verification and build-gate check.
Solution: Re-scanned the new render methods, forbidden SHINOBU patterns, Burst directive parity, and `git diff --check`. No forbidden pattern hit; job/Burst parity remained 9/9; `git diff --check` reported CRLF normalization warnings only. Guarded build probe reported CPU=100 and compiler_count=1, and a follow-up process check showed active `dotnet` and `csc`, so no compiler was launched by SHINOBU.
Rejected Alternatives: Running a second build while another compiler was active, or treating static scans as Unity import/profiler proof.
Scalability potential: Not runtime-affecting.
Hardware Impact: Avoided compiler contention on a saturated workstation.

## Loop 24 Decisions - Compile Gate Attempt And External Blocker

Problem: After Loop 23, compile verification was still required. The workstation gate opened with CPU=40.2 and compiler_count=0, so a guarded `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal` was legal.
Solution: Ran the build. It failed on non-SHINOBU missing types and symbols in Visor, Equipment, DeferredDecal, GlobalRegistryContracts, and Somatic editor code: `UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `ReconstructionTelemetryEntry`, `UberNoirReconstructionVaultIds`, `DynamicDecalFrameStats`, `ActiveEquipmentDTO`, `EquipmentGridLoadRequest`, `EquipmentTelemetryEntry`, `EquipmentIntegrationCounters`, `EquipmentOverheatSignal`, `VrComfortProfileDTO`, and `ComfortTelemetryEntry`. No compiler error referenced `ShinobuEcosystemBalancer.cs`, the new shader, the new material, or `H8Memory.cs`.
Rejected Alternatives: Editing Visor/Equipment/Comfort DTO ownership outside SHINOBU_105, reverting SHINOBU render seam without a compiler error pointing at it, or relaunching builds repeatedly against the same external dependency wall.
Scalability potential: Not runtime-affecting.
Hardware Impact: Build verification remains PENDING VERIFICATION for SHINOBU because the project compile wall is outside this domain. Runtime microseconds remain unmeasured.

## Loop 25 Decisions - Cinematic Cheat Ledger And Asset Retention Reality

Problem: A shader and material asset alone do not prove asset retention or shader warmup. If no scene, Addressables group, content hash map, or VFX prewarm manifest references the material, Unity import/build may strip or fail to preload it. Pretending otherwise would be fake readiness.
Solution: Scanned for `ShaderVariantCollection`, `ContentVfxPrewarmManifest`, Addressables data, and SHINOBU material/shader references. The project has source classes/docs for content authority, but no populated `Assets/AddressableAssetsData` files and no authored `ContentVfxPrewarmManifest` assets. Updated `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md` with a SHINOBU_105 procedural silhouette entry that marks material binding, shader warmup/retention, Unity import, Frame Debugger, GCMonitor, and profiler proof as pending.
Rejected Alternatives: Adding `Resources.Load`, creating an unverified Addressables setup, hand-authoring fragile ScriptableObject YAML for a content manifest without Unity import proof, or claiming the material is retained because it exists on disk.
Scalability potential: Low/middle/high/ultra keep the same Dear Lie silhouette route; content retention must be solved by ContentAuthority/boot ownership, not by per-tier runtime loads.
Hardware Impact: Documentation-only 0us. It prevents a future stutter/strip claim from being treated as proven.

## Loop 26 Decisions - Static Validation Forensics

Problem: The broad forbidden-pattern scan across every touched path produced false positives: existing core `H8Memory.cs` DataVault `new NativeArray` owners, existing `H8Memory.cs` `Time.frameCount` telemetry, and unrelated historical text in the cinematic cheat ledger. Reporting that as a SHINOBU violation would be inaccurate; ignoring it would hide validation ambiguity.
Solution: Re-ran narrower scans against SHINOBU runtime, shader, and material paths, and separately scanned the zero-context diff. The SHINOBU runtime/shader/material scan returned no forbidden hits. The diff scan found only removed lines for the previous `throw;`, cold `job.Run()`, and `GraphicsBuffer.IndirectDrawIndexedArgs` route. Job/Burst parity remained 9 job structs with 9 required Burst directives.
Rejected Alternatives: Editing `H8Memory.cs` core allocation internals outside SHINOBU ownership, deleting unrelated ledger history, or claiming a clean whole-file scan when the evidence contains known non-domain matches.
Scalability potential: Not runtime-affecting. It preserves the proof chain for low/middle/high/ultra behavior by separating SHINOBU's actual hot path from core memory-owner code.
Hardware Impact: 0us runtime. Verification only. `git diff --check` still reports CRLF normalization warnings only; compile remains externally blocked by Visor/Equipment/Comfort DTO gaps.
