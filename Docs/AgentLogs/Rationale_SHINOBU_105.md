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
