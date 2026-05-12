# Rationale_ECO_BOIDS_COMPUTE

Status: VERIFIED MASTER GRADE
Prompt ID: ECO_BOIDS_COMPUTE
Domain: ECHELON 3: FLORA, FAUNA & BIOTA

## Decision 001 - Patch Generic GPU Boid Lane

Problem: The project already contains a Sargassum-specific micro-fauna compute path with GPU spatial grid ownership. Editing that system would risk cross-agent collision. The generic `BoidSimulation.compute` still used a tiled full-scan neighbor evaluation and was the correct target for this prompt.

Solution: Add `ClearSpatialGrid` and `BuildSpatialGrid` kernels to `Assets/_Project/Scripts/BoidSimulation.compute`, then bind persistent `GraphicsBuffer` grid storage from `HectonBoidController.cs`. The simulation remains GPU-owned and host code only dispatches kernels.

Rejected Alternatives: Rewriting Sargassum micro-fauna would duplicate existing work. CPU spatial hashing is disallowed by the prompt and by zero-GC/frame-budget mandates.

Scalability potential: Low uses bounded cell insertion and nearest-cell sampling to stay visually acceptable on i3/MX350. Middle keeps full separation/alignment/cohesion inside 27 cells. High increases boid count using the same grid. Ultra spends saved cycles on richer VAT, bubbles, SDF avoidance, and flow advection without moving simulation to CPU.

Hardware Impact: Expected low-end gain is removal of O(N^2) neighbor pressure from the generic boid kernel. Exact microseconds remain pending profiler evidence; no fake metric is recorded.

## Decision 002 - Atomic Prefix Grid Instead Of Full Bitonic Sort

Problem: The assignment permits sort or atomic-add prefix grouping. A full bitonic sort adds multiple global passes and more shader surface before the spatial hash is proven.

Solution: Use `RWByteAddressBuffer` cell counters plus `InterlockedAdd` to reserve a fixed slot in `RWStructuredBuffer<uint>` cell entries. Overflow is rolled back to keep counts bounded and deterministic enough for local-neighbor traversal.

Rejected Alternatives: Bitonic sorting by cell key is more complete for dense swarms but adds pass count, memory traffic, and validation burden. CPU-side prefix sums violate the no-readback and GPU-owned simulation requirements.

Scalability potential: Low/MX350 uses max 32 occupants per cell. Middle and High can raise boid count by resizing the grid while keeping lookup local. Ultra can replace the atomic grid with a sorted key/value path later if density proves pathological.

Hardware Impact: Atomic grid insertion is a cheap single pass relative to full neighbor scans. Estimated gain remains pending real shader profiler evidence.

## Decision 003 - Shared-Memory Local Cell Staging

Problem: The first spatial pass used local hash cells, but neighbor indices were read directly from the global structured buffer. The prompt explicitly required shared-memory localized lookups.

Solution: Stage each visited cell's fixed 32 indices into `groupshared uint sharedSpatialIndices[32]`, synchronize, then scan LDS for separation/alignment/cohesion. Low Math LOD masks social accumulators and keeps separation only.

Rejected Alternatives: Re-enabling the old tile path restores global O(N^2) behavior. Full cell sort would be heavier than required for this density until profiler data proves atomics insufficient.

Scalability potential: Low keeps separation to avoid obvious overlaps. Middle enables social motion. High raises count/grid density. Ultra can spend extra budget on denser cells and richer panic/flow behavior.

Hardware Impact: On i3/MX350 the expected gain is fewer global buffer reads during neighbor accumulation. Exact microseconds are pending GPU profiler evidence.

## Decision 004 - Use Existing Cave SDF And Abyssal Flow Contracts

Problem: The task asked for voxel SDF obstacle avoidance and abyssal flow advection. Inventing new texture ownership would collide with cave lighting/fluid systems and create duplicated data paths.

Solution: Bind `HectonCaveVoxelLightingVolume.TryGetPublishedGpuSdfPayload` for SDF repulsion and `HectonFluidEngine.TryGetGpuAbyssalFlowFieldBuffer` for flow advection. Fallback resources are cold-created once and disabled through active flags when authoritative publishers are absent.

Rejected Alternatives: Physics raycasts, CPU current volumes, or new per-boid flow textures. Those are slower, less deterministic, and harder to keep synchronized with existing systems.

Scalability potential: Low can disable SDF/flow weights or rely on broad fallback bounds. Middle uses one SDF normal and one flow sample. High can increase cave SDF quality. Ultra can layer visual overkill through stronger flow/VFX without CPU readback.

Hardware Impact: MX350 cost is bounded compute texture/buffer sampling. It trades CPU physics queries for GPU-local data access; exact microseconds are pending profiler evidence.

## Decision 005 - GPU Panic Metadata Instead Of CPU Events

Problem: Predator evasion, acoustic pings, and scatter/bubble VFX need shared state. Per-fish CPU events would violate the no-readback/no-fanout requirement and scale badly with boid count.

Solution: Extend `BoidData` padding into `panic` and `stateFlags`, matching `BoidFishInstanced.shader`. Compute writes panic and bit 0 from predator/ping/acceleration state; the shader/VFX lane can consume it without CPU object mutation.

Rejected Alternatives: Per-fish MonoBehaviour messages, CPU bubble spawn loops, or separate managed panic arrays. All add GC or CPU fan-out pressure.

Scalability potential: Low uses a single panic bit. Middle uses panic scalar for scatter. High can drive VAT/material reactions. Ultra can layer bubble/debris overkill while keeping boid simulation GPU-owned.

Hardware Impact: i3/MX350 avoids per-boid CPU event dispatch entirely. GPU cost is a few scalar writes and branchless falloff checks; exact microseconds pending profiler evidence.

## Decision 006 - EventBus Acoustic Ping Bridge

Problem: Ping dispersion must connect to the existing physics acoustic system without direct scene dependency or GameObject polling.

Solution: `HectonBoidController` implements `Hecton8.Physics.IAcousticPingEventListener`, registers with `PhysicsEventBus`, and converts one event into fixed compute uniforms for radial velocity kick and panic.

Rejected Alternatives: Searching for sonar/ping components each frame or broadcasting to individual boids. Both are direct dependencies and CPU fan-out.

Scalability potential: Low uses one short-lived radial kick. Middle keeps panic decay. High and Ultra can increase visual response through render/VFX consumers of the same panic state.

Hardware Impact: On low-end silicon the CPU cost is one listener callback per ping, not N boid callbacks. GPU cost is one radial distance check per boid while active.

## Decision 007 - Current Compile Block Is External To Generic Boid Lane

Problem: After fixing own errors, Unity console no longer reports `HectonBoidController.cs` or `BoidSimulation.compute` errors. Global compile remains blocked by `ProceduralCrabLegIKRuntime.cs` access/field errors, `CombatDamageRuntime.cs` missing helper methods, and `SaveBinaryStorage.cs` Burst unsupported catch/filter construction.

Solution: Mark Task 15 blocked by dependency. Do not claim compile success. Do not edit crab IK or save storage from the ECO_BOIDS_COMPUTE prompt without a separate directive, because those are separate ownership surfaces.

Rejected Alternatives: Faking a clean compile or expanding this pass into unrelated crab IK/save infrastructure. Both create false reporting and integration risk.

Scalability potential: Generic boid scalability work is isolated and can be profiled after external compile blockers are cleared. Low/Middle/High/Ultra behavior is now represented in code through Math LOD, SDF/flow weights, and panic metadata.

Hardware Impact: No runtime hardware claim is made until the project compiles and profiler data exists. Current block is compile infrastructure outside the edited generic boid files.

## Decision 008 - Indirect Draw With GPU Visible Index Compaction

Problem: The generic boid lane still submitted `Graphics.RenderMeshPrimitives` with a CPU-visible instance count and CPU AABB visibility. That did not satisfy the prompt's `Graphics.RenderMeshIndirect` and compute frustum culling requirements.

Solution: Add `ClearVisibleIndirectArgs` and `CullVisibleBoids` kernels. The cull pass resets only the raw indirect args instance-count field, tests boids against six uploaded camera planes, writes visible boid IDs into `_VisibleBoidIndices`, and atomically increments `GraphicsBuffer.IndirectDrawIndexedArgs.instanceCount`. `BoidFishInstanced.shader` maps `SV_InstanceID` through the visible-index buffer before reading `BoidData`.

Rejected Alternatives: BatchRendererGroup wrapper work was rejected for this generated data path because the prompt explicitly requested `Graphics.RenderMeshIndirect` with `GraphicsBuffer` args and the project already uses the same raw-args pattern in marine snow/scatter. CPU per-boid culling was rejected because it violates the GPU-owned simulation/render path.

Scalability potential: Low/MX350 uses one compacted indirect draw and no CPU boid visibility list. Middle keeps the same path with higher boid count. High and Ultra can spend the saved CPU submission budget on richer materials, panic VFX, and denser swarms.

Hardware Impact: Expected low-end gain is reduced CPU draw submission and no CPU per-boid culling. Exact microseconds remain pending a clean compile and profiler capture.

## Decision 009 - Current Compile Block Updated After Latest Import

Problem: After the indirect/culling pass, `HectonBoidController.cs` validates with 0 errors and Unity console no longer reports boid compute/shader errors. Current hard compile blockers moved to `VoxelDeltaProcessor.cs`: missing `VoxelChunkModifiedEvent` and `VoxelChunkModifiedEvents`.

Solution: Keep Task 15 blocked by dependency and record the current external errors. Do not edit voxel deformation ownership from the ECO boids prompt.

Rejected Alternatives: Claiming a clean compile or fixing voxel deformation events from a boid rendering task. Both would violate domain boundaries and hide real integration state.

Scalability potential: The indirect/culling path is isolated from the voxel blocker and can be profiled as soon as global compile is restored.

Hardware Impact: No runtime microsecond claim is made. Current blocker is unrelated compile infrastructure.

## Decision 010 - OMEGA POLISH CHANGES

Problem: Omega audit found post-upgrade bloat in the generic boid lane: stale comments still documented `Graphics.RenderMeshPrimitives`, and the old CPU `CheckFrustumVisibility()` method survived after GPU visible-index culling took ownership. Leaving that dead path would confuse future profiling and could invite a CPU culling regression.

Solution: Removed the dead CPU AABB frustum method and updated the render documentation to `Graphics.RenderMeshIndirect`. Re-ran scoped scans against `HectonBoidController.cs`, `BoidSimulation.compute`, and `BoidFishInstanced.shader`: no `RenderMeshPrimitives`, `CheckFrustumVisibility`, `GetData`, `AsyncGPUReadback`, or readback references remain in those edited boid files. Diff-added-line audit found no new `foreach`, `string.Format`, interpolated strings, `.ToString()`, readback calls, `Vector3.Distance`, `Mathf.Sqrt`, `math.sqrt`, `.normalized`, or `Random`.

Rejected Alternatives: Keeping the CPU AABB cull as a fallback was rejected because the compute kernel already has a camera-missing fallback flag and renders all boids when no frustum is uploaded. Exact mesh frustum tests were rejected for this pass; sphere-radius culling is the cinematic cheat and is conservative enough for small fish silhouettes.

Scalability potential: Low/MX350 gets the cheapest path: one cull dispatch, six plane tests, compacted visible indices, one indirect draw. Middle/High keep the same dispatch path with more boids. Ultra can spend saved CPU submission budget on richer VAT/material response and scatter VFX, not on CPU visibility bookkeeping.

Hardware Impact: Expected low-end gain remains CPU-side and qualitative until profiler capture: no CPU per-boid cull list, no CPU-visible instance count, no readback. Exact microseconds are intentionally not claimed.

Omega audit evidence:
- `validate_script Assets/_Project/Scripts/HectonBoidController.cs`: 0 errors, 1 generic static warning about string concatenation in Update.
- Unity console after refresh: no boid-controller, boid-compute, or boid-shader errors. Current external blocker is `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs(2534,13)`: missing `WritePowerBlackBoxSample`.
- `dotnet build .\Assembly-CSharp.csproj`: executed as required. Multiprocess build failed with MSB4166 child-node crashes; single-process/no-restore variants exited 1 without C# diagnostics after restore/start banners, so Unity console remains the actionable compile evidence.
- `git diff --check` on edited boid files: no whitespace errors; only existing LF-to-CRLF warnings from Git.

Final Git Diff:
```text
Assets/_Project/Scripts/BoidFishInstanced.shader |   5 +-
Assets/_Project/Scripts/BoidSimulation.compute   | 437 +++++++++++++++++-
Assets/_Project/Scripts/HectonBoidController.cs  | 543 +++++++++++++++++++++--
3 files changed, 939 insertions(+), 46 deletions(-)

Numstat:
4       1       Assets/_Project/Scripts/BoidFishInstanced.shader
425     12      Assets/_Project/Scripts/BoidSimulation.compute
510     33      Assets/_Project/Scripts/HectonBoidController.cs
```
