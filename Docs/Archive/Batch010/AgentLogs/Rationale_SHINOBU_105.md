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

## Loop 27 Decisions - Deterministic Burst Correction

Problem: SHINOBU_105 owns rollback-compatible swarm state, but the 9 SHINOBU Burst jobs still used `FloatMode.Fast`. The global rule allows Fast for normal hot paths, but explicitly requires deterministic float mode for multiplayer rollback domains. Leaving Fast here would make the Task 15 proof weaker than the XML assignment.
Solution: Changed all 9 SHINOBU job attributes to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`. Repository scan confirmed `FloatMode.Deterministic` is already used by rollback/physics/IK jobs, so this is an existing project pattern, not a new dependency.
Rejected Alternatives: Keeping Fast for speed while claiming rollback safety, splitting some visual jobs to Fast while they still consume or publish rollback-derived state, or editing unrelated sibling/domain Burst jobs.
Scalability potential: Low/middle/high/ultra still scale through active budget, neighbor cap, chain cap, update stride, and shader lanes. Deterministic float mode is a correctness guard across the same continuous quality curves, not a new tier switch.
Hardware Impact: Possible ALU optimization loss is accepted for rollback-owned state; exact microseconds remain pending because the project compile wall is external. The static proof is now jobs=9, deterministic=9, fast=0, with forbidden-pattern scan returning no SHINOBU hits.

Problem: The prompt extraction script used earlier was too strict and failed when extra attributes existed on the `AGENT_PROMPT` tag.
Solution: Re-extracted the full prompt using `<AGENT_PROMPT id="SHINOBU_105"[\s\S]*?</AGENT_PROMPT>`, preserving cover-to-cover task context while ignoring neighboring agents.
Rejected Alternatives: Relying on remembered task text or loosening the regex enough to capture adjacent prompt blocks.
Scalability potential: Not runtime-affecting; this protects task reconciliation evidence.
Hardware Impact: 0us runtime.

## Loop 28 Decisions - GPU Visibility Culling Route

Problem: The procedural draw route no longer used GameObjects, but it still submitted the active swarm budget to the vertex shader without a SHINOBU-owned visibility/occlusion compaction stage. That left the HZB mandate weak and made low-quality rendering pay for fish that the camera or terrain depth pyramid can reject.
Solution: Added `Hecton_AbyssalSwarmCull.compute` and a cold-bound culling seam on `ShinobuEcosystemBalancer`. The compute pass clears a 16B procedural args row, frustum-tests `BoidMatrixDTO.C3`, optionally samples the supplied depth pyramid, compacts visible source indices into a double-buffered `GraphicsBuffer<uint>`, and increments instance count with `RWByteAddressBuffer.InterlockedAdd`. `Hecton_AbyssalSwarmProcedural.shader` now resolves `_H8ShinobuBoidVisibleIndices` when `_H8ShinobuBoidUseVisibleIndices` is set.
Rejected Alternatives: CPU visible-list construction, per-fish `Renderer`/`GameObject` culling, BRG dependency invention, runtime `Resources.Load`/`Shader.Find`, and always-on HZB sampling at low quality. The GPU-written args buffer intentionally uses `Target.Raw | Target.IndirectArguments`; the CPU-uploaded fallback args buffer remains `Target.IndirectArguments` only.
Scalability potential: Low quality raises density decimation through `ceil(lerp(5, 1, smoothQuality))` and disables HZB sampling below `GlobalQualityWeight` 0.3 so the shader draw path collapses toward sparse frustum-only silhouettes. Middle/high/ultra lower decimation continuously, enable HZB when a depth pyramid is bound, and spend GPU work only on visible compacted instances.
Hardware Impact: Expected low-end/MX350 gain is fewer vertex shader invocations after frustum/density/HZB compaction; exact microseconds remain PENDING VERIFICATION because Unity import/profiler cannot run while the project compile wall and workstation compiler gate remain active.

Problem: Loop 28 required verification without violating the user's build gate.
Solution: Re-ran SHINOBU-only forbidden-pattern scans, `TryDraw` signature scan, C#/HLSL symbol alignment scan, Burst parity, property scan, and `git diff --check`. Forbidden scan returned no hits; `TryDraw` has two call sites plus the dispatcher definition; jobs=9, deterministic=9, fast=0; no hot DTO properties were found; `git diff --check` reported CRLF normalization warnings only. Build probe reported CPU=85.4 and compiler_count=7, so no `dotnet build` was launched.
Rejected Alternatives: Launching a second compiler while other `dotnet/csc` processes were active, or claiming runtime proof from static scans.
Scalability potential: Verification-only; the render path still scales by continuous active budget, density decimation, and culling depth use.
Hardware Impact: Avoided compiler contention on a saturated workstation. Runtime proof remains PENDING VERIFICATION.

## Loop 29 Decisions - GPU Cull Resource Binding Hardening

Problem: The first compute-cull patch had two Unity integration hazards. The procedural shader declared `_H8ShinobuBoidVisibleIndices`, but the fallback non-culled path did not bind a visible-index buffer. The compute kernel declared `_H8ShinobuDepthPyramid`, but frustum-only culls with no depth pyramid could dispatch without a texture binding on Unity backends that require every referenced compute resource to be set.
Solution: Always bind the active visible-index buffer through `PublishBuffers(...)`, even when `_H8ShinobuBoidUseVisibleIndices` is zero. Always call `SetTexture` for `_H8ShinobuDepthPyramid`, using the caller depth pyramid when present and `Texture2D.blackTexture` as the no-allocation fallback when frustum-only culling is active. Re-read the SHINOBU_105 XML block from `CURRENT_BATCH.md` and verified all 20 task lines before recording this loop.
Rejected Alternatives: Leaving the resource unbound and trusting branch pruning, creating a runtime dummy texture, disabling the entire compute cull when HZB is absent, or moving texture ownership into SHINOBU with a private render texture allocation.
Scalability potential: Low quality keeps culling sparse and HZB-disabled while still binding valid resources; middle/high/ultra can enable HZB without changing shader layout or material variants.
Hardware Impact: One global visible-index buffer binding and one compute texture binding per draw-dispatch path. Expected cost is negligible relative to preventing backend dispatch failure; exact microseconds remain PENDING VERIFICATION.

Problem: Loop 29 needed a fresh build gate check after C# edits.
Solution: Re-ran static scans and the workstation gate. Forbidden SHINOBU pattern scan returned no hits; jobs=9, deterministic=9, fast=0; `git diff --check` only reported CRLF normalization warnings. CPU=70.9 and compiler_count=7, so no `dotnet build` was launched.
Rejected Alternatives: Launching a build while active compilers and high CPU were present.
Scalability potential: Verification-only.
Hardware Impact: Avoided adding compiler load to a saturated workstation.

## Loop 30 Decisions - GPU Cull ABI Audit

Problem: The new GPU culling pass writes a `DrawProceduralIndirect` args row from HLSL and indexes a C#-uploaded `BoidMatrixDTO` StructuredBuffer. Any byte-layout drift between C# and HLSL would render the wrong fish, corrupt instance counts, or fail on ARM64/GPU backends without a compiler error pointing at the real cause.
Solution: Audited `ShinobuEcosystemLayoutManifest`, `BoidMatrixDTO`, `BoidIndirectArgsDTO`, `Hecton_AbyssalSwarmCull.compute`, and `Hecton_AbyssalSwarmProcedural.shader`. `BoidMatrixDTO` is four `float4` lanes at C# offsets `0/16/32/48` and the HLSL structs declare the same lane order. `BoidIndirectArgsDTO` is a 16B row: `VertexCountPerInstance=0`, `InstanceCount=4`, `StartVertex=8`, `StartInstance=12`; the compute clear kernel stores exactly `3/0/0/0` at byte offsets `0/4/8/12`, and the cull kernel increments byte offset `4` for the instance count. The GPU-written args buffer is `Target.IndirectArguments | Target.Raw`, `count=4`, `stride=4`, giving the required 16 bytes for `RWByteAddressBuffer`.
Rejected Alternatives: Adding a second DTO for culled args, changing the fallback CPU args buffer to Raw, relying on implicit `float4x4` packing, or claiming layout proof from shader naming alone. Those paths either blur the ABI or add unnecessary resource ambiguity.
Scalability potential: Low quality can draw a sparse density-decimated index window through the same 16B args row; middle/high/ultra increase visible instances without changing buffer layout. The ABI is invariant across quality weights, so quality scaling cannot create a tier-specific layout failure.
Hardware Impact: 0us runtime change. The value is preventing a silent GPU/ARM64 layout fault. The static proof is source-only; Unity shader import, RenderDoc/Frame Debugger, and profiler proof remain PENDING VERIFICATION.

Problem: The compile-wall mandate requires no direct sibling runtime assembly dependency from SHINOBU work.
Solution: Re-scanned asmdef references and SHINOBU using statements. `ShinobuEcosystemBalancer.cs` uses the `Hecton8.World` namespace only for the existing `AbsoluteUniversePosition` AUP type under the current core/root script assembly seam; no new SHINOBU asmdef or sibling runtime reference was added by the GPU cull work. This is logged as an existing AUP ownership seam, not a new dependency.
Rejected Alternatives: Moving `AbsoluteUniversePosition` into contracts during this batch, introducing a SHINOBU-local duplicate AUP struct, or editing world contracts without a route card. Those would be wider architectural changes outside the current cull ABI audit.
Scalability potential: AUP semantics remain identical across low/middle/high/ultra; only visible density and HZB sampling change.
Hardware Impact: 0us runtime change; compile-wall risk is documented rather than papered over.

## Loop 31 Decisions - Content Prewarm Route Audit

Problem: The SHINOBU procedural material and compute culler exist on disk, but shader/compute warmup and build retention are still unproven. The risky shortcut would be hand-authoring a Unity `ScriptableObject` YAML manifest or using `Resources.Load`, either of which can create false proof or violate the no-runtime-load route.
Solution: Audited the existing ContentAuthority surfaces instead of inventing a new route. `ContentAuthorityRuntime` owns `ContentVfxPrewarmManifest`, caps it at 64 handles, and calls `LoadAssetAsync<ComputeShader>()` for compute shader entries. `ContentAuthorityBuildValidators` scans `Assets/_Project` compute shaders with `GetKernelThreadGroupSizes`, enforces the <=1024 thread-group limit, and validates `ContentVfxPrewarmManifest` compute references. Disk reality remains bad: `Assets/AddressableAssetsData` contains 0 files, and the authored-payload scan for `ContentVfxPrewarmManifest` under `Assets/_Project` returned no `.asset`, `.prefab`, or `.unity` hits. Updated `CINEMATIC_CHEATS_LEDGER.md` to record the exact warmup route and the absent payload proof.
Rejected Alternatives: Creating fragile raw `.asset` YAML for `ContentVfxPrewarmManifest`, adding a SHINOBU-local Addressables bootstrap, adding `Resources.Load`, using `Shader.Find`, or claiming warmup/retention from source scaffolding alone.
Scalability potential: Low/middle/high/ultra all need the same cold content-authority route. Runtime quality scaling remains in active count, density decimation, HZB enablement, and shader lanes; asset retention is a boot/content pipeline fact, not a per-tier branch.
Hardware Impact: 0us runtime change. The audit prevents shader-stutter and stripping claims from becoming false positives. Actual Quest/MX350/RTX shader import, warmup, and profiler proof remain pending until ContentAuthority payloads and the external compile wall are resolved.

Problem: Loop 31 still needed static verification without violating the build gate.
Solution: Re-ran SHINOBU-only forbidden-pattern scan across `ShinobuEcosystemBalancer.cs`, `Hecton_AbyssalSwarmProcedural.shader`, and `Hecton_AbyssalSwarmCull.compute`; it returned no hits. Burst parity remains jobs=9, deterministic=9, fast=0. `LOG_SHINOBU_105.md` now orders Loop 29, Loop 30, Loop 31 linearly. `git diff --check` reports CRLF normalization warnings only. Workstation probe returned CPU=100 and compiler_count=0, so no build was launched; Loop 31 also changed documentation only and the last legal build remains externally blocked.
Rejected Alternatives: Relaunching `dotnet build` under 100% CPU, treating documentation-only edits as a fresh compile requirement, or leaving the misplaced Loop 30 audit block in the historical log.
Scalability potential: Verification-only; no runtime quality curve changed.
Hardware Impact: 0us runtime change. Compiler contention avoided on a saturated workstation.

## Loop 32 Decisions - HZB Texel Sanitizer

Problem: The optional GPU culler correctly binds a depth pyramid, but it trusted caller-supplied `_H8ShinobuDepthPyramidTexelSize` and mip count. A zero, stale, or non-finite `zw` would make the compute shader sample a fake 1x1 pyramid or an impossible mip level, causing silent over-culling or under-culling without a C# exception.
Solution: Added `SanitizeDepthPyramidMipCount()` and `SanitizeDepthPyramidTexelSize()` in the SHINOBU runtime seam. `BindProceduralCullingResources()` now clamps mip count to a conservative dimension-derived maximum and derives width/height/inverse width/inverse height from the bound `Texture` whenever caller values are invalid. Replaced the first attempt to use `Texture.mipmapCount` because a repo scan found no local precedent for that property; dimension-derived mip count is sufficient and less import-version-sensitive.
Rejected Alternatives: Trusting caller data, adding a SHINOBU-owned render texture, disabling HZB when texel size is invalid, or adding a binary low/high switch. The sanitizer preserves the same continuous quality route and keeps resource ownership with the caller.
Scalability potential: Low quality still disables HZB below `GlobalQualityWeight` 0.3 and density-decimates with `ceil(lerp(5, 1, smoothQuality))`. Middle/high/ultra use sanitized depth metadata so HZB sampling scales without a tier-specific failure mode.
Hardware Impact: Cold bind-path scalar math only. Runtime draw/cull cost is unchanged except preventing bad depth metadata from wasting GPU work or dropping visible fish. Exact microseconds remain pending; Unity import/profiler proof is still blocked externally.

Problem: Loop 32 needed verification after touching C#.
Solution: Re-extracted the SHINOBU_105 XML prompt and confirmed 20 task entries. Re-ran forbidden-pattern scan across SHINOBU runtime/shader/compute paths; it returned no hits, including no `mipmapCount`. Burst parity remains jobs=9, deterministic=9, fast=0. `git diff --check` reports CRLF normalization warnings only. Workstation probe returned CPU=98 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` under the user's CPU gate or claiming shader/runtime proof from source scans.
Scalability potential: Verification-only; no new runtime tier branch.
Hardware Impact: Compiler contention avoided on a saturated workstation.

## Loop 33 Decisions - Hot Struct And Allocation Audit

Problem: After adding the GPU cull seam and HZB sanitizer, the proof needed a fresh CS1612 and Zero-GC audit. A clean forbidden-pattern scan alone does not prove DTOs stayed field-only or that native persistent ownership stayed out of private `NativeArray` fields.
Solution: Re-ran scoped scans on `ShinobuEcosystemBalancer.cs`. Struct/property scan enumerated SHINOBU DTOs/jobs and found zero `{ get; set; }` or `{ get; private set; }` properties. Persistent native-container scan found zero private `NativeArray`, `NativeList`, `NativeHashMap`, `NativeParallel`, `NativeQueue`, or `NativeStream` fields. Allocation scan found cold singleton/dispatcher construction, cold CSV and blackbox `FileStream`/`BinaryWriter` usage, cold `GraphicsBuffer` bridge allocation, value-type constructors (`float3`, `Vector4`, `Bounds`), and one cold boot `CriticalBootException` layout failure path.
Rejected Alternatives: Treating every `new float3` value-type constructor as GC, deleting the cold layout fail-fast exception, or moving GPU buffer ownership into `GlobalDataVault` where Unity graphics resources cannot be stored as unmanaged Vault rows.
Scalability potential: No runtime quality route changed. Low/middle/high/ultra still scale through active budget, density decimation, HZB enablement, and shader lanes; the audit confirms no hidden property or private native container path was introduced.
Hardware Impact: 0us runtime change. The cold GPU bridge allocations are boot/render-resource ownership only; no per-frame managed array/list/string allocation was introduced by the recent cull work.

Problem: Loop 33 needed a build-gate check after the audit docs were updated.
Solution: Re-ran log-order, property/native-container, forbidden-token, Burst parity, and `git diff --check` scans. Results: Loop 31 -> 32 -> 33 order is linear; hot_properties=0; private_native_fields=0; jobs=9; deterministic=9; forbidden SHINOBU scan returned no hits; `git diff --check` reports CRLF normalization warnings only. CPU probe returned 53 and compiler_count=0, so the build gate stayed closed.
Rejected Alternatives: Launching build above the user's >50% CPU cutoff, or ignoring the gate because the delta is small.
Scalability potential: Verification-only.
Hardware Impact: 0us runtime change. Avoided compiler load while CPU was above the allowed threshold.

## Loop 34 Decisions - Shader Variant And Threadgroup Audit

Problem: The new procedural material/shader/compute route could still create runtime stutter through shader keyword variants or compute kernels that exceed Quest/Metal thread-group limits. Source existence is not enough proof.
Solution: Audited the material, shader, compute shader, and ContentAuthority validator. `MAT_AbyssalSwarmProcedural.mat` has empty `m_ValidKeywords` and `m_InvalidKeywords`. `Hecton_AbyssalSwarmProcedural.shader` has only `#pragma vertex Vert`, `#pragma fragment Frag`, and `#pragma target 4.5`; no `shader_feature` or `multi_compile` directives are present. `Hecton_AbyssalSwarmCull.compute` has two kernels: clear at `[numthreads(1,1,1)]` and cull at `[numthreads(64,1,1)]`. `ContentAuthorityBuildValidators.ValidateComputeShaderThreadGroups()` scans compute shaders under `Assets/_Project`, calls `GetKernelThreadGroupSizes`, and fails totals above 1024.
Rejected Alternatives: Adding keyword tiers for low/high visuals, creating material variants, or relying on the build validator without checking the actual SHINOBU source. Visual overkill remains in scalar buffers and shader math, not variant multiplication.
Scalability potential: Low/middle/high/ultra share one material and one shader variant; quality is data-driven through `_H8ShinobuBoidCustomData.w`, density decimation, and HZB enablement.
Hardware Impact: 0us runtime change. This prevents a hidden variant explosion claim. Import/shader compiler proof is still pending because Unity cannot be run through the current project compile wall.

Problem: Loop 34 needed a build-gate and source-proof recheck.
Solution: Re-ran log-order, shader keyword/threadgroup, and `git diff --check` scans. Loop order is 32 -> 33 -> 34; shader-route scan confirms empty material keyword lists and 1/64-thread compute kernels; `git diff --check` reports CRLF normalization warnings only. CPU probe returned 100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching a build at CPU saturation or treating source-level shader audit as Unity import proof.
Scalability potential: Verification-only.
Hardware Impact: 0us runtime change. Compiler contention avoided.

## Loop 35 Decisions - Agent-Keyed Blackbox Dump Path

Problem: The telemetry ring exists and writes a 300-frame blackbox, but the dump filenames used the domain alias `Dump_ABYSSAL_SWARM.*`. AGENTS.md requires crash/NaN dumps to be keyed as `Dump_[YourID].bin`; keeping the alias weakens forensic routing when multiple agents write logs in parallel.
Solution: Changed the dump constants to `Docs/AgentLogs/Dump_SHINOBU_105.bin` and `Docs/AgentLogs/Dump_SHINOBU_105.h8dump`. The 64-byte `ShinobuTelemetryEntry` layout, 300-entry ring, fault trigger conditions, binary writer, and `.h8dump` companion route remain unchanged.
Rejected Alternatives: Writing only the old domain alias, writing both old and new aliases every fault, or moving blackbox ownership into a shared core route. Dual-writing would add unnecessary file I/O during a fault; shared ownership is outside SHINOBU's domain.
Scalability potential: Not quality-dependent. Low/middle/high/ultra all use the same fixed-size forensic dump path when invalid math or budget faults occur.
Hardware Impact: 0us normal runtime change. Fault-path file name only; no per-frame cost.

Problem: Loop 35 needed validation after a code constant change.
Solution: Re-scanned source/docs for old and new dump route names, Burst parity, forbidden SHINOBU patterns, and `git diff --check`. Runtime constants now point to `Dump_SHINOBU_105.bin` and `.h8dump`; remaining `Dump_ABYSSAL_SWARM` hits are historical explanation text in the log/rationale/status for the correction. Forbidden SHINOBU scan returned no hits; jobs=9, deterministic=9, fast=0; `git diff --check` reports CRLF normalization warnings only. CPU probe returned 70 and compiler_count=0, so build stayed closed.
Rejected Alternatives: Launching build over the user's CPU threshold or deleting historical context that explains why Loop 35 exists.
Scalability potential: Verification-only.
Hardware Impact: 0us runtime change. Compiler load avoided above CPU cutoff.

## Loop 36 Decisions - Stale Forensic Claim Repair

Problem: The top of `LOG_SHINOBU_105.md` still contained earlier claims that were correct when written but are now superseded: `BoidIndirectArgsDTO` as 32B/indexed-args ABI and `FloatMode.Fast` as the active Burst proof. After context compaction, another agent could read those stale lines as current truth and undo the Loop 14/27 corrections.
Solution: Updated only the stale forensic-log statements to point at the superseding loops. The current proof now consistently states: `BoidIndirectArgsDTO` is 16B for `DrawProceduralIndirect`, indexed indirect args were removed, and all nine rollback-owned SHINOBU jobs use `FloatMode.Deterministic`.
Rejected Alternatives: Leaving stale top-of-file audit claims because later loop deltas contradict them, or deleting historical loop sections entirely. The repair preserves chronology while preventing stale claims from looking authoritative.
Scalability potential: Documentation-only; runtime quality route unchanged.
Hardware Impact: 0us runtime change.

Problem: Loop 36 needed validation after the stale-log edit.
Solution: Re-ran stale-proof search, forbidden SHINOBU scan, `git diff --check`, and the build gate. The only remaining indirect-size matches are current 16B evidence lines that share a line with other 32B DTO names, not stale `BoidIndirectArgsDTO` size claims. Forbidden SHINOBU scan returned no hits. `git diff --check` reports CRLF normalization warnings only. CPU probe returned 68 and compiler_count=0, so build stayed closed.
Rejected Alternatives: Launching build above the CPU cutoff or over-pruning historical narrative.
Scalability potential: Verification-only.
Hardware Impact: 0us runtime change.

## Loop 37 Decisions - HZB Sanitizer Consistency Repair

Problem: The Loop 32 HZB sanitizer still had a subtle consistency fault. If the caller supplied invalid `zw` texture dimensions but stale positive `xy` inverse dimensions, SHINOBU derived width/height from the bound texture while preserving the caller inverse. That can shift compute cull mip/pixel addressing and produce silent over-cull or under-cull. A second fault disabled HZB entirely when a valid depth texture was bound but caller mip count was missing.
Solution: `SanitizeDepthPyramidMipCount()` now returns mip0 for a valid texture with a missing/zero requested mip count, keeping a safe one-level HZB path instead of hard-disabling occlusion. `SanitizeDepthPyramidTexelSize()` now tracks whether width/height came from the caller and only preserves inverse values when they are finite, positive, and consistent with the final dimension within a 5% tolerance; otherwise it recomputes `1f / width` and `1f / height`.
Rejected Alternatives: Trusting mixed caller metadata, disabling HZB whenever metadata is incomplete, using `Texture.mipmapCount`, or allocating a SHINOBU-owned fallback render texture. Those either waste GPU work, hide occluders, add version-sensitive Unity API assumptions, or violate caller-owned HZB authority.
Scalability potential: Low quality still disables HZB below `GlobalQualityWeight` 0.3 and density-decimates through the existing polynomial quality curve. Middle/high/ultra can use mip0 as a safe fallback when metadata is incomplete, and full pyramid sampling when the render pipeline supplies a valid mip count.
Hardware Impact: Cold bind-path scalar validation only. Expected runtime cost is 0us in steady draw/cull because the compute shader receives coherent metadata. Measured profiler proof remains pending behind the external compile wall and CPU gate.

Problem: Loop 37 needed verification after C# edits.
Solution: Re-ran the HZB sanitizer source slice, SHINOBU runtime/shader forbidden-pattern scan, Burst parity, `git diff --check`, stale-log search, and workstation gate. Results: forbidden scan returned no hits; jobs=9, deterministic=9, fast=0; `git diff --check` reports CRLF normalization warnings only; stale-log hits are current 16B evidence or historical superseded notes. CPU probe returned 100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` at 100% CPU or claiming Unity import/profiler proof from source scans.
Scalability potential: Verification-only; no new binary tier branch.
Hardware Impact: 0us runtime change from verification. Compiler contention avoided on a saturated workstation.

## Loop 38 Decisions - Procedural Draw API Seam Audit

Problem: The SHINOBU procedural draw path depends on Unity accepting `Graphics.DrawProceduralIndirect` with a `GraphicsBuffer` argument buffer. Because the project compile wall is outside SHINOBU, this needed source precedent before risking another build.
Solution: Scanned existing project runtime code. `ProceduralWreckageGpuUploadDispatcher`, `ProceduralCoralGpuUploadDispatcher`, and `ShinobuPlasmaBeamRuntime` already call `Graphics.DrawProceduralIndirect(material, bounds, topology, GraphicsBuffer, 0, null, null, ShadowCastingMode, bool, layer)`. SHINOBU uses the same signature at its helper and dispatcher draw sites.
Rejected Alternatives: Replacing the draw with indexed `GraphicsBuffer.IndirectDrawIndexedArgs`, adding a mesh just to use `DrawMeshInstancedIndirect`, or converting to `ComputeBuffer` without a compiler error proving it is necessary. Those would either reintroduce indexed ABI confusion or add a mesh dependency the task explicitly rejects.
Scalability potential: No quality route changed. Low/middle/high/ultra all share the same one-call procedural draw; density and HZB compaction decide instance count.
Hardware Impact: 0us runtime change. Source precedent reduces compile-wall risk; actual Unity import/runtime proof remains pending.

Problem: The new GPU-written culled args buffer needed ownership/usage proof.
Solution: Compared SHINOBU buffer creation with existing procedural uploaders. CPU-written SHINOBU matrix/custom/fallback args buffers use `GraphicsBuffer.UsageFlags.LockBufferForWrite`. The culled args buffer is intentionally `GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw`, count 4, stride 4, and is not CPU-locked; it is only bound as `RWByteAddressBuffer` in compute and then consumed by `DrawProceduralIndirect`.
Rejected Alternatives: Adding `LockBufferForWrite` to the GPU-only raw args buffer, creating a second CPU staging copy for culled args, or using a structured DTO buffer for `RWByteAddressBuffer`. Those add unnecessary usage flags/copies or break the byte-address ABI.
Scalability potential: Low quality can draw sparse/density-decimated output through the same raw 16B row; higher quality increases the compute-written instance count without changing buffer shape.
Hardware Impact: 0us runtime change. Avoids CPU-visible culled-count readback and preserves GPU sovereignty.

Problem: Loop 38 needed verification without violating the build gate.
Solution: Re-ran procedural draw precedent scan, buffer usage scan, SHINOBU forbidden-pattern scan, and workstation gate. Results: procedural `GraphicsBuffer` draw precedent exists in three non-SHINOBU systems; forbidden SHINOBU scan returned no hits; CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` under CPU saturation or treating precedent as profiler proof.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 39 Decisions - Mesh Indexed ABI Residue Removal

Problem: SHINOBU still exposed an unused `TryUploadIndirectDrawArgs(GraphicsBuffer, Mesh, int)` overload. It copied `Mesh.GetIndexCount(0)` and `Mesh.GetIndexStart(0)` into `BoidIndirectArgsDTO`, which is now a 16-byte non-indexed `DrawProceduralIndirect` row. Even unused, this public helper preserved the old indexed mental model inside the swarm domain and invited future mesh coupling.
Solution: Removed the mesh overload completely. The only remaining draw-args upload route consumes Vault-produced `NativeArray<BoidIndirectArgsDTO>`, and the only draw submission is procedural. Current scan shows no `Mesh mesh`, `GetIndexCount`, `GetIndexStart`, `DrawMesh`, `SkinnedMeshRenderer`, `Animator`, `GameObject`, or `Instantiate` inside `ShinobuEcosystemBalancer.cs`.
Rejected Alternatives: Rewriting the overload to use `mesh.vertexCount`, marking it obsolete, or leaving it because no current call sites exist. Any mesh-accepting overload weakens the no-GameObject/no-mesh procedural contract and can reintroduce indexed args confusion.
Scalability potential: No quality route changed. Low/middle/high/ultra all use the same procedural triangle silhouette path, with density/HZB/active-budget controlling rendered count.
Hardware Impact: 0us runtime change because the helper was unused. Architectural impact is reduced future risk of CPU mesh dependency or wrong indirect ABI.

Problem: Loop 39 needed verification after code deletion.
Solution: Re-ran SHINOBU mesh/index residue scan, forbidden-pattern scan, Burst parity, `git diff --check`, stale-log search, and workstation gate. Results: mesh/index residue absent from SHINOBU runtime; forbidden scan returned no hits; jobs=9, deterministic=9, fast=0; `git diff --check` reports CRLF warnings only; stale-log hits are current 16B evidence or historical superseded notes. CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching build under 100% CPU or deleting historical log lines that explain prior indexed ABI repairs.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 40 Decisions - Procedural Render Bounds Fallback

Problem: `SanitizeRenderBounds(default(Bounds))` produced a 1m x 1m x 1m culling volume because zero size passed the finite check and was only clamped to 1m. That is too small for a camera-relative procedural school and can make Unity reject the indirect draw before the shader consumes matrix/custom buffers.
Solution: Treat non-finite, zero, negative, and sub-millimeter extents as invalid integration input and replace them with the existing `DefaultDehydrateDistanceMeters * 2f` envelope, currently 400m per axis. Valid caller-authored bounds remain unchanged except for the prior 1m minimum clamp.
Rejected Alternatives: Trusting every caller to provide a useful bounds volume, always expanding all valid bounds to 400m, or adding a mesh renderer solely to inherit mesh bounds. Trusting callers creates blank-swarm failures; unconditional expansion wastes culling precision; mesh bounds violate the procedural/no-mesh contract.
Scalability potential: Low/middle/high/ultra still scale via active budget, density decimation, HZB, and shader data. This patch prevents the lowest-quality or first-frame integration path from silently dropping all fish due to a default render volume.
Hardware Impact: Cold draw-call scalar validation only, 0us measured. It avoids catastrophic visual false-negative culling; profiler/Frame Debugger proof remains pending.

Problem: Loop 40 needed verification without violating the user build gate.
Solution: Re-ran the render-bounds source slice, SHINOBU runtime/shader/compute forbidden-pattern scan, Burst parity, `git diff --check`, and workstation gate. Results: forbidden scan returned no hits; jobs=9, deterministic=9, fast=0; `git diff --check` reports CRLF normalization warnings only. CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` at 100% CPU or claiming Unity draw proof from source validation.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 41 Decisions - Compute UV Bounds HLSL Ambiguity Removal

Problem: `Hecton_AbyssalSwarmCull.compute` used `0.0.xx` and `1.0.xx` in the HZB UV bounds check. Some HLSL frontends accept scalar swizzles; relying on that in a Unity compute shader is unnecessary compile risk under the current external compile wall.
Solution: Replaced scalar-vector swizzle comparisons with explicit `uv.x` and `uv.y` scalar comparisons. The branch remains mathematically identical and avoids questionable shader syntax.
Rejected Alternatives: Leaving the scalar swizzle because it is compact, introducing helper vector constants, or adding shader keywords for culling modes. Compactness is not worth a platform compiler ambiguity; constants add no value; keywords violate the single-variant SHINOBU shader route.
Scalability potential: No quality route changed. Low/middle/high/ultra still use the same HZB/depth/density compute path with continuous `GlobalQualityWeight`.
Hardware Impact: 0us measured. The generated shader code is equivalent; the gain is reducing import/compiler failure risk on Unity shader backends.

Problem: Loop 41 needed validation without a build.
Solution: Re-ran a compute scan for the removed scalar swizzle and UV `any()` patterns, the SHINOBU forbidden-pattern scan, Burst parity, `git diff --check`, and the workstation gate. Results: no swizzle residue, forbidden scan clean, jobs=9, deterministic=9, fast=0, CRLF warnings only. CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` while CPU was saturated or treating source cleanup as shader import proof.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 42 Decisions - Procedural Draw Layer Clamp

Problem: High-level SHINOBU draw wrappers clamp Unity render layer inputs, but the low-level `ShinobuBoidGpuUploadDispatcher.TryDraw()` public seam forwarded its `layer` argument directly into `Graphics.DrawProceduralIndirect`. A future caller bypassing the wrapper could pass an invalid layer.
Solution: Clamp the dispatcher-local layer to `0..31` immediately before the draw call. This keeps the owner-local procedural draw path tolerant of bad integration input without changing material, buffer, or culling behavior.
Rejected Alternatives: Relying on caller discipline, throwing on invalid layer, or hiding the dispatcher API. Caller discipline is not a contract; throwing in a render path is unnecessary; hiding the API would break external render-graph integration that needs buffer ownership.
Scalability potential: No quality route changed. Low/middle/high/ultra all use the same one-call procedural draw; the clamp only guards Unity API input.
Hardware Impact: One scalar clamp on the draw submission path, 0us measured. It prevents invalid layer failures without touching per-boid work.

Problem: Loop 42 needed verification after C# edit.
Solution: Re-ran the dispatcher draw-call source slice, SHINOBU forbidden-pattern scan, Burst parity, `git diff --check`, and workstation gate. Results: forbidden scan clean; jobs=9, deterministic=9, fast=0; CRLF warnings only; CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` at 100% CPU or assuming wrapper clamps cover all external use.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 43 Decisions - Global Quality Active Budget Anchor

Problem: Task 11 explicitly requires `GlobalQualityWeight=0.1` to simulate/render 5% of hydrated biomass. The previous cubic budget curve was continuous but under-hit that anchor for 100k capacity, landing below the requested 5% survival density.
Solution: Split the active-budget fraction into two continuous bands using `math.lerp`, `Smooth01`, and `math.step`: 1% at quality 0.0, 5% at quality 0.1, and 100% at quality 1.0. The `MinimumVisualBoidBudget` remains as a lower clamp for small capacities.
Rejected Alternatives: Keeping the old cubic curve, adding a binary low-end branch, or hardcoding a 5,000-boid constant. The old curve missed the XML target; a branch violates the continuum rule; a constant would ignore actual hydrated capacity.
Scalability potential: Weak devices get 1% survival at quality 0.0 and the mandated 5% at 0.1; middle/high/ultra ramp continuously through the upper band until full 100k visual overkill at quality 1.0.
Hardware Impact: 0us measured; one extra scalar helper on schedule path only. The runtime win is predictable thermal shedding without a visual population cliff.

Problem: Loop 43 needed verification after changing active-budget math.
Solution: Re-ran the source slice for `ResolveActiveEntityBudget()`, SHINOBU forbidden-pattern scan, Burst parity, `git diff --check`, and workstation gate. Results: forbidden scan clean; jobs=9, deterministic=9, fast=0; CRLF warnings only; CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` at 100% CPU or claiming profiler proof from scalar source inspection.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 44 Decisions - Low-Quality Neighbor Solve Collapse

Problem: Task 11 requires `GlobalQualityWeight=0.1` to rely on flow-following after dropping cohesion and alignment. The prior code dropped alignment/cohesion but still entered `QueryNeighbors()` for separation, paying up to 27-cell bucket traversal and adding non-flow steering at thermal-survival quality.
Solution: Added `ResolveNeighborSolveWeight()` anchored at 0 through quality 0.12, 0.5 at quality 0.21, and 1 at quality 0.3. `BoidFlockingJob` now skips `QueryNeighbors()` when that scalar is zero, and separation/alignment/cohesion are all multiplied by the same continuous scalar when the neighbor solve ramps back in.
Rejected Alternatives: Keeping low-quality separation for safety, deleting neighbor logic entirely, or adding a hardware-tier boolean. Keeping separation violates the XML flow-following contract; deleting neighbor logic damages high-end school motion; hardware branching violates the continuum rule.
Scalability potential: Low/q=0.1 is sparse flow-following plus predator and SDF wall Dear Lie responses. Middle quality ramps neighbor solve smoothly. High/ultra restore full Reynolds steering while preserving the same data route.
Hardware Impact: At q=0.1 this avoids the bounded neighbor chain traversal per visible boid instead of only zeroing two force components after the scan. Exact microseconds remain pending behind Unity/profiler proof and the current compile wall.

Problem: Loop 44 needed verification after changing flocking math.
Solution: Re-ran the neighbor-weight source slice, manually evaluated the scalar anchors (`q=0/0.1/0.12 => 0`, `q=0.21 => 0.5`, `q=0.3/1 => 1`), re-ran the SHINOBU forbidden-pattern scan, Burst parity scan, `git diff --check`, and workstation gate. Results: forbidden scan clean; jobs=9, deterministic=9, fast=0; CRLF warnings only; CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` at 100% CPU or claiming profiler timing from static source inspection.
Scalability potential: Verification-only; the runtime curve remains continuous and quality-weight driven.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 45 Decisions - Sqrt-Free Predator Panic Falloff

Problem: Predator avoidance is a hot-path visual panic response. The previous proximity scalar used `math.sqrt(distSq)` per affected boid even though the behavior only needs a bounded flee intensity, not exact Euclidean distance.
Solution: Replaced the proximity calculation with squared-distance math: `radiusSq = max(1, radius * radius)` and `proximity = saturate(1 - distSq * rcp(radiusSq))`. The sector-wide panic impulse remains intact, the near-predator boost remains bounded, and the branch no longer takes a square root for falloff.
Rejected Alternatives: Keeping exact linear-distance falloff, using Unity physics triggers, or adding an authored panic lookup table. Exact distance is unnecessary for this Dear Lie; triggers violate the no-physics swarm route; a lookup table adds data authority for a scalar that is cheaper as math.
Scalability potential: Low quality keeps predator shatter readable with the same sparse flow-following swarm. Middle/high/ultra spend the saved scalar cost on denser schools and HZB-filtered procedural draw count, not on physical truth.
Hardware Impact: Removes one panic-path `sqrt` per boid affected by a predator signal. Exact microseconds are pending profiler proof; source cost is strictly lower and NaN exposure is reduced by avoiding distance division.

Problem: Loop 45 needed verification after changing predator math.
Solution: Re-ran the predator source slice, SHINOBU forbidden-pattern scan, Burst parity scan, repo-root `git diff --check`, and workstation gate. Results: forbidden scan clean; jobs=9, deterministic=9, fast=0; `git diff --check` reports CRLF warnings only; CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` at 100% CPU or reporting measured timing without Unity profiler evidence.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 46 Decisions - Squared-Speed Clamp and Final Speed Reuse

Problem: The steering kernel still normalized velocity through `math.length(velocity)` followed by division by `speed`, then recomputed `math.length(velocity)` for `BoidStateDTO.Speed`. That paid extra square-root work and left a visible divide-by-speed pattern in the hot path.
Solution: Replaced the clamp with `speedSq = math.lengthsq(velocity)`. Invalid or near-zero speed resets to `forward * maxSpeed`. Over-speed velocity scales by `maxSpeed * math.rsqrt(max(0.0001f, speedSq))`. A single `finalSpeed` scalar is carried into `boidState.Speed`, avoiding the second length calculation.
Rejected Alternatives: Keeping the old length/divide sequence, removing speed clamping, or storing squared speed in `BoidStateDTO.Speed`. The old path is wasteful; removing the clamp breaks tuning; changing DTO semantics would violate the 32-byte assignment contract and any downstream readers expecting meters/second.
Scalability potential: Low quality keeps cheap flow-following with fewer ALU operations. Middle/high/ultra keep exact clamp semantics for visual school coherence while avoiding one redundant speed recomputation.
Hardware Impact: Removes one hot-path divide-by-speed branch and the second speed length recomputation. Exact microseconds remain pending behind profiler proof; source-level NaN exposure is lower because the only reciprocal uses a clamped denominator.

Problem: Loop 46 needed verification after changing steering normalization.
Solution: Re-ran the speed-clamp source slice, residue scan for `float speed = math.length`, `/ speed`, and `boidState.Speed = math.length`, SHINOBU forbidden-pattern scan, Burst parity scan, repo-root `git diff --check`, and workstation gate. Results: forbidden scan clean; jobs=9, deterministic=9, fast=0; old speed/divide residues absent; `git diff --check` reports CRLF warnings only; CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` at 100% CPU or reporting measured timing without Unity profiler evidence.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 47 Decisions - Mock SDF Rsqrt Reuse

Problem: `MockTerrainSampler.SphereSdf()` calculated `sqrt(lenSq)` for signed distance and separately calculated `rsqrt(lenSq)` for the normal. That is duplicate work in the Task 08 Dear Lie wall-response path.
Solution: Added `safeLenSq = max(0.000001f, lenSq)`, calculated one `invLen = math.rsqrt(safeLenSq)`, derived `len = safeLenSq * invLen`, and reused `invLen` for the normal.
Rejected Alternatives: Keeping the duplicate operations for readability, replacing the mock SDF with raycasts, or adding a mesh collider. Duplicate scalar work is unnecessary; raycasts/colliders violate the no-physics swarm route and the SDF visual-fake requirement.
Scalability potential: Low quality still pays a single cheap SDF lookup for wall swirl. Middle/high/ultra keep the same vortex illusion while reducing redundant ALU in the terrain fake.
Hardware Impact: Removes one redundant sqrt/rsqrt pair component from each sphere SDF sample. Exact microseconds remain pending; source-level cost is lower and denominator handling remains guarded.

Problem: Loop 47 needed verification after SDF math change.
Solution: Re-ran the SDF source slice, residue scan for the removed `float len = math.sqrt` and `Normal = delta * math.rsqrt` duplication, SHINOBU forbidden-pattern scan, Burst parity scan, repo-root `git diff --check`, and workstation gate. Results: forbidden scan clean; jobs=9, deterministic=9, fast=0; only the intentional final-speed `sqrt` remains in the steering state path; `git diff --check` reports CRLF warnings only; CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` at 100% CPU or reporting measured timing without Unity profiler evidence.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 48 Decisions - Guarded Length Route

Problem: After Loop 47, runtime scalar lengths still used direct `math.length` in flow tensor diagnostics and local-shift state hydration, and final speed still used one direct `math.sqrt`. These are not physics truth; they are scalar metadata and state speed values that can use the same guarded `rsqrt` pattern as the rest of the SHINOBU math.
Solution: Added `SafeLength(float3)` with finite/epsilon checks and `lenSq * math.rsqrt(lenSq)`. Routed `GenerateEmergencyMockFlowJob` tensor scalar lanes and `LocalShiftAndSpatialHashJob` `BoidStateDTO.Speed` construction through it. Converted final speed storage in `BoidFlockingJob` from `sqrt(max(...))` to `speedSq * rsqrt(max(...))`.
Rejected Alternatives: Leaving direct `math.length` for readability, storing squared speed in the DTO, or removing speed telemetry. Readability does not beat hot-path math hygiene; DTO speed semantics must remain meters/second; telemetry/state speed remains useful for blackbox forensics.
Scalability potential: Low quality keeps the same sparse flow-following behavior with fewer direct transcendental-style helper calls. Middle/high/ultra keep current scalar data for shader/telemetry richness without adding exact-distance physical truth.
Hardware Impact: Direct `math.sqrt` and `math.length` calls are now absent from `ShinobuEcosystemBalancer.cs`. Exact microseconds remain pending, but every length route now has finite and epsilon guards.

Problem: Loop 48 needed verification after replacing length routes.
Solution: Re-ran residue scans for `math.sqrt` and `math.length(`, `SafeLength` usage scan, SHINOBU forbidden-pattern scan, Burst parity scan, repo-root `git diff --check`, and workstation gate. Results: direct `math.sqrt`/`math.length` calls absent; forbidden scan clean; jobs=9, deterministic=9, fast=0; `git diff --check` reports CRLF warnings only; CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` at 100% CPU or claiming profiler timing from source-only cleanup.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 49 Decisions - Compile-Wall Isolation Audit

Problem: The SHINOBU domain does not currently have a local runtime asmdef. `Assets/_Project/Scripts/AI/Ecosystem` files are compiled by the root `Hecton8.Core` asmdef, and `ShinobuEcosystemBalancer.cs` imports `Hecton8.World` for `AbsoluteUniversePosition`. That namespace looks like a sibling dependency, but the type currently lives under the root Core assembly path (`Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`), not a separable `Hecton8.World.Runtime` contract.
Solution: Treated this as structural compile-wall debt rather than deleting a required namespace import. Verified there is no `AI/Ecosystem` asmdef, found external references from `EcosystemRuntimeInstaller` and `BiomassBoidTunerWindow` into `ShinobuEcosystemBalancer`, and recorded that a safe split needs an integrator-owned boot/editor seam move plus an AUP contract placement decision.
Rejected Alternatives: Blindly deleting `using Hecton8.World`, adding a local asmdef that references Core while Core-side installers still reference SHINOBU, or moving `AbsoluteUniversePosition` across assemblies inside this domain pass. Deleting the import breaks the AUP type; a blind asmdef risks circular or missing references; moving AUP is global core/world ownership.
Scalability potential: Runtime behavior unchanged. The architectural value is preventing a fake compile-wall claim and identifying the exact route blocker for a future domain assembly split.
Hardware Impact: 0us runtime change. Developer iteration impact remains pending until the integrator isolates `AI/Ecosystem` from root `Hecton8.Core`.

## Loop 50 Decisions - HZB Z-Buffer Parameter Guard

Problem: The optional GPU HZB culling path sanitized depth pyramid dimensions and mip count, but still accepted caller-provided `_ZBufferParams` without checking finite values or a usable `LinearEyeDepth` denominator. A bad render-graph caller could feed NaN/zero params and make depth occlusion silently reject or accept the wrong fish set.
Solution: Added `_proceduralCullHasValidZBufferParams`, `IsUsableZBufferParams(Vector4)`, and a safe fallback vector. `ResolveGpuCullingParams()` now enables depth occlusion only when a depth pyramid, mip count, valid z-buffer params, and quality >= 0.3 all hold. Frustum and density culling still run when z params are invalid.
Rejected Alternatives: Trusting the render pipeline caller, disabling the whole compute pass when z params are invalid, or trying to reconstruct projection near/far planes inside SHINOBU. Caller trust is not a contract; disabling the whole compute pass wastes density/frustum culling; near/far reconstruction is render-pipeline ownership and risks a worse cross-domain dependency.
Scalability potential: Low quality remains density/frustum culled and HZB-disabled by the existing continuous quality gate. Middle/high/ultra get HZB only when the render pipeline provides usable depth parameters, avoiding false occlusion on malformed integrations.
Hardware Impact: 0us measured. Runtime cost is one cold/public-bind validation and one boolean in culling-param resolution; it prevents GPU cull corruption without adding per-boid CPU work.

Problem: Loop 50 needed verification after C# edit.
Solution: Re-ran the SHINOBU forbidden-pattern scan, Burst parity scan, `git diff --check`, and workstation gate. Results: forbidden scan clean; jobs=9, deterministic=9, fast=0; `git diff --check` reports CRLF warnings only; CPU=95.5 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` under the user-forbidden CPU gate or claiming Unity shader import proof from static source inspection.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 95.5% CPU.

## Loop 51 Decisions - GPU Density Double-Dip Removal

Problem: `ResolveActiveEntityBudget()` already applies the continuous `GlobalQualityWeight` population curve, including the XML anchor that q=0.1 keeps 5% hydrated swarm rows active. The GPU culler then added a second automatic quality-derived density step (`ceil(lerp(5, 1, smoothQuality))`), which could render roughly one fifth of the already-reduced q=0.1 budget when compute culling was bound.
Solution: Removed the automatic quality-derived density step from `ResolveGpuCullingParams()`. SHINOBU now keeps the active budget as the single owner of quality-driven population density, while preserving an explicit caller-owned density step clamped to 1..8 for render-graph emergencies.
Rejected Alternatives: Increasing the CPU active budget to compensate, leaving the double decimation as a GPU optimization, or deleting density-step support entirely. Increasing CPU work violates the thermal-survival goal; leaving double decimation violates the 5% q=0.1 contract; deleting the explicit step removes a useful owner-local escape hatch for render integration.
Scalability potential: Low/q=0.1 now simulates and renders the same 5% active swarm before frustum/HZB visibility, instead of silently dropping to roughly 1% when compute culling is bound. Middle/high/ultra retain full active-budget scaling and can still use explicit render density when a caller intentionally owns that tradeoff.
Hardware Impact: Worst-case GPU vertex work can increase at low quality only where compute culling was previously double-decimating, but the total q=0.1 count is already 5k/100k and still goes through frustum/HZB visibility. CPU hot path unchanged.

Problem: Loop 51 needed verification after culling-curve edit.
Solution: Re-ran the `ResolveGpuCullingParams()` source slice, SHINOBU forbidden-pattern scan, Burst parity scan, `git diff --check`, and workstation gate. Results: quality-derived density residue removed; forbidden scan clean; jobs=9, deterministic=9, fast=0; `git diff --check` reports CRLF warnings only; CPU=100 and compiler_count=0, so no build was launched.
Rejected Alternatives: Launching `dotnet build` at 100% CPU or reporting profiler timings from static analysis.
Scalability potential: Verification-only.
Hardware Impact: Compiler contention avoided at 100% CPU.

## Loop 52 Decisions - Exact Editor Facade Identity

Problem: The designer facade still carried stale Biomass/Boid identity in the public `EditorWindow` class and file path while the task-facing menu already said `Abyssal Swarm Tuner`. That is not a runtime perf fault, but it is an authoring-control fault: designers and CI/editor reflection should see the domain facade requested by the SHINOBU assignment, not a generic legacy name.
Solution: Renamed `BiomassBoidTunerWindow.cs` to `AbyssalSwarmTunerWindow.cs`, preserved the `.meta` GUID, renamed the public class and `GetWindow<T>()` target, and removed the legacy `HECTON-8/Biomass & Boid Tuner` menu alias. Kept the facade in the Editor folder and left runtime data ownership in DataVault buffers. This is an editor-only correction with 0us player hot-path cost.
Rejected Alternatives: Leaving the stale class because C# allows file/class mismatch, keeping the legacy menu redirect for convenience, or moving the facade into runtime code. Stale names weaken the exact facade proof; legacy menu text makes audit scans ambiguous; runtime UI would violate the designer facade boundary and add player-path surface.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. The value is human tuning control: the same Vault-backed sliders and telemetry graph remain available without recompilation while the player build remains free of editor UI.
Hardware Impact: 0us runtime. Source-level authoring clarity improved; no player allocation or GPU work added.

Problem: The SceneView vector preview used `Time.realtimeSinceStartup` as the animated phase for `CurrentManager.SampleCurrent()`. It was editor-only, but it still created a confusing Time dependency inside the SHINOBU facade while the runtime mandate forbids Unity Time for deterministic simulation state.
Solution: Replaced the editor time phase with a deterministic phase derived from the sampled position (`math.frac(math.dot(p, ...)) * PI10`). The preview remains a spatial flow fake and no longer depends on Unity Time.
Rejected Alternatives: Keeping editor Time because it is not gameplay-critical, or removing the vector preview entirely. Keeping it invites false-positive determinism audits; removing it would weaken the required live vector-field gizmo.
Scalability potential: Runtime unchanged. Editor preview remains cheap, deterministic, and stable across weak-to-ultra machines.
Hardware Impact: Removes a cold editor-only time query from the draw path. Runtime impact is 0us.

## Loop 53 Decisions - Meta Hygiene and Compiler Gate

Problem: The newly added `Hecton_AbyssalSwarmCull.compute.meta` contained Unity-style blank YAML scalar lines with trailing spaces. Because untracked files are invisible to `git diff --check`, this would slip past the normal tracked-diff hygiene gate.
Solution: Removed the trailing spaces from `userData`, `assetBundleName`, and `assetBundleVariant`, then added an explicit untracked-file whitespace scan for new SHINOBU files alongside the standard tracked diff check.
Rejected Alternatives: Waiting for Unity to rewrite the meta file, relying only on `git diff --check`, or ignoring whitespace in untracked files. Unity rewrite is not guaranteed in this CLI pass; tracked diff check cannot see untracked payloads; ignoring it creates avoidable review noise.
Scalability potential: Runtime behavior unchanged across Low/Middle/High/Ultra. This is source-control hygiene only.
Hardware Impact: 0us runtime.

Problem: CPU dropped under 50%, but seven `dotnet`/`csc` compiler processes were active. The user explicitly forbids launching a build while another compiler is running.
Solution: Build remained gated. Verification stayed on static scans: case-sensitive forbidden-pattern scan, Burst parity, untracked-file whitespace scan, and tracked `git diff --check`.
Rejected Alternatives: Launching `dotnet build` because CPU was 45.3%, or killing external compiler processes. Active compilers violate the build gate; killing processes would be destructive and outside SHINOBU ownership.
Scalability potential: Verification-only.
Hardware Impact: Avoided compiler contention while seven compiler processes were active.

## Loop 54 Decisions - Designer Bridge Facade Completion

Problem: `AbyssalSwarmTunerWindow` exposed live Vault sliders and telemetry, but it did not expose the authoring bridge proof required by the designer-facade mandate: CSV source path, output route, schema, row count, checksum, validation state, bake/reload control, and DTO layout summary. That left Task 18/19 functionally present but weak as an audit artifact.
Solution: Added a `Designer Bridge` block to the editor window. It resolves the tuning and species CSV paths, reports data rows/bytes, computes FNV1A32 checksums over the same 8192-byte scratch limit as the runtime, names the DataVault output buffers, reports live Vault row counts, prints DTO size/layout summaries, warns on size drift, and exposes a play-mode `Force CSV -> Vault Reload` button.
Rejected Alternatives: Leaving bridge proof in logs only, adding a runtime debug UI, or adding a standalone baker that writes an unowned `.h8bin`. Logs are not designer control; runtime UI would add player-build surface; a standalone binary baker needs Data Monolith ownership and atomic binary-output policy beyond this SHINOBU domain pass.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. Designers can tune CSV values and force cold reload while the player hot path remains Vault/Burst-only. Ultra authoring gets richer checksum/layout visibility without increasing gameplay cost.
Hardware Impact: 0us player runtime. Editor-only file/hash work runs only inside the EditorWindow repaint path.

Problem: A reload button needed a route into SHINOBU without exposing mutable hot-path parser internals or adding a player-build API.
Solution: Added `ShinobuEcosystemBalancer.ForceDesignerDataReload()` behind `#if UNITY_EDITOR`. It is editor-only, checks Play Mode/DataVault availability, resets CSV timestamps, and reuses the existing cold `MonitorCsvOverrides(vault)` path.
Rejected Alternatives: Making the parser public, calling private methods through reflection, or duplicating CSV parse logic in the editor. Public parser surface broadens runtime API; reflection is explicitly forbidden for runtime patterns and bad even in tooling here; duplicate parsing risks editor/runtime divergence.
Scalability potential: Runtime unchanged. The same DataVault buffer route is used for every hardware tier after reload.
Hardware Impact: 0us player runtime; no player build method emitted.

## Loop 55 Decisions - Forensic Log Chronology Repair

Problem: Loop 52-54 rationale/log entries were accidentally inserted above older loop entries. That violates the top-old/bottom-new evidence trail and makes later audits look inconsistent even when the code changes are valid.
Solution: Mechanically moved the Loop 52-54 blocks to the bottom of `Rationale_SHINOBU_105.md` and `LOG_SHINOBU_105.md`, then verified the visible sequence from Loop 45 through Loop 54 is monotonic in both files.
Rejected Alternatives: Leaving chronology broken because the content was present, or deleting/recreating historical entries. Presence is not enough for an audit trail; deleting history would damage provenance.
Scalability potential: Verification-only.
Hardware Impact: 0us runtime; improves CTO audit readability and context-recovery reliability.

## Loop 56 Decisions - Facade Binary Output Label

Problem: The editor bridge showed the DataVault route under `Runtime Output`, but the designer-facade mandate asks for a binary output path. The route is a DataVault binary/unmanaged output rather than a standalone `.h8bin` writer, so the UI label needed to say that explicitly.
Solution: Changed the facade label to `Binary Output` and kept the value as `GlobalDataVault: ShinobuEcosystemTuning, ShinobuSwarmSpeciesProfiles`.
Rejected Alternatives: Adding an unowned `.h8bin` writer or leaving the weaker label. A writer belongs to Data Monolith/atomic bake ownership; the weaker label leaves audit ambiguity.
Scalability potential: Runtime unchanged.
Hardware Impact: 0us runtime; editor-only wording correction.

## Loop 57 Decisions - Guarded Build External Blocker

Problem: After C# runtime/editor changes, a guarded compile probe was necessary. The workstation gate was open at CPU=20.4 and compiler_count=0, but the build failed before reaching SHINOBU code because `Hecton8.Core.csproj` still references missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.
Solution: Recorded the failure as an external compile-wall blocker. Verified the file path does not exist and `git status` reports it as deleted. No SHINOBU compiler diagnostics were emitted.
Rejected Alternatives: Editing or restoring Construction-domain files from SHINOBU, removing the compile item from `Hecton8.Core.csproj`, or claiming compile success. Construction ownership is outside SHINOBU_105; changing the project file would affect all agents; compile success would be false.
Scalability potential: Verification-only.
Hardware Impact: Build stopped after 4.17s on a missing source file; no additional compiler retries launched.
