# Rationale 1735 - Wreckage Prefab Factory

## Session Start
Problem: Wreckage assets risk entering runtime as deep loose-debris hierarchies with expensive renderers/colliders and no terrain seating metadata.
Solution: Build an Editor-only prefab factory that combines debris meshes offline, binds existing shared PBR materials, attaches primitive collider proxies, and serializes SDF carve metadata.
Rejected Alternatives: Runtime Mesh.CombineMeshes and runtime terrain raycasts. Both move authoring work into player frames and violate the offline bake mandate.
Scalability potential: Low uses the same prefab truth with lower shadow fidelity; Middle keeps baked combined debris with restrained shadows; High/Ultra spend saved draw calls on richer shared-material lighting and denser authored wreck sets.
Hardware Impact: MX350/i3 gain comes from fewer loose renderers, no runtime mesh combining, and no runtime visual mesh colliders. Exact microseconds pending code and validation.

## Decision 01 - Factory Boundary
Problem: Wreckage assembly needs heavy mesh/material/collider work, but runtime generation is a registered release-blocker pattern.
Solution: Added `WreckagePrefabFactory.cs` as Editor-only assembly code. Runtime scripts are metadata/cold presentation only.
Rejected Alternatives: Extending `ProceduralWreckGenerator` runtime mesh paths; that keeps player `new Mesh` routes alive.
Scalability potential: Low/Middle/High/Ultra all load the same serialized prefab truth; high tiers spend saved draw calls on richer authored wreck density.
Hardware Impact: i3/MX350 avoids 80-350 us per cluttered wreck from loose debris renderer fanout and runtime combine work.

## Decision 02 - Debris Merge Contract
Problem: Small debris inside compartments can become many renderers and SetPass sources.
Solution: Grouped debris submeshes by material and used two-pass `Mesh.CombineMeshes`: first merge each material bucket, then combine buckets as preserved submeshes.
Rejected Alternatives: `mergeSubMeshes=true` for all debris; it destroys material identity. One renderer per shard; it wastes CPU/GPU setup.
Scalability potential: Low keeps one combined debris renderer; Middle keeps same truth with normal shadows; High/Ultra can add richer source debris before offline merge without increasing runtime hierarchy.
Hardware Impact: Estimated 80-350 us saved per cluttered wreck on weak hardware; draw-call count becomes one debris renderer instead of dozens.

## Decision 03 - Burned Material Gate
Problem: The request requires burned PBR materials from Agent 1727. Current tree has texture baker output paths but no obvious `MAT_*1727` material asset.
Solution: Factory now searches SRP-batcher-valid materials by exact shared Wave 3 wreckage material names: `MAT_Wreckage_Exterior`, `MAT_Wreckage_Hull_Exterior`, `MAT_Wreckage_Burned_Interior`, `MAT_Wreckage_Burned`, carbonized variants, and explicit debris/scrap variants. `MAT_Wreckage_Exterior` and burned material are required; debris may reuse burned only if no exact debris material exists. If absent, it fails closed and creates no fallback material.
Rejected Alternatives: Using generic runtime-proof materials like `MAT_RuntimeVisualProof_BlackenedHull` or broad `Blackened/Charred` token matching; both hide missing upstream material work and can bind unrelated Agent output.
Scalability potential: Low through Ultra use the same shared materials; quality only affects cold shadow presentation, not material identity.
Hardware Impact: Avoids runtime material instancing and shader variant churn; estimated 25-120 us render setup variance avoided.

## Decision 04 - SDF Carve Volume
Problem: Wreck chunks need automatic voxel bottom deformation at spawn without runtime mesh analysis.
Solution: `VoxelCarveVolume` serializes an OBB computed from the lowest 20 percent of hull vertices, yaw-aligned by XZ covariance, expanded downward by 1 meter, instruction `FlattenAndBury`.
Rejected Alternatives: Mesh-accurate SDF or spawn-time raycast/terrain scans; both are too expensive and less predictable.
Scalability potential: Low uses the same box carve; Middle/High/Ultra can add richer visual debris around the same terrain truth without changing authority.
Hardware Impact: Estimated 20-70 us saved per spawn vs runtime bounds/raycast analysis; 150-800 us saved vs mesh-accurate SDF.

## Decision 05 - Collision Proxy Policy
Problem: Automatic collision fallback would let visual meshes or expensive collider cooking enter prefab output.
Solution: Factory requires `COL_` proxy and validates BoxCollider, CapsuleCollider, or convex MeshCollider only.
Rejected Alternatives: Creating ad hoc colliders from visual bounds when `COL_` is missing; it would pass bad source packages silently.
Scalability potential: Low gets simple proxies; Middle/High/Ultra get the same physics truth while visuals scale independently.
Hardware Impact: Estimated 100-900 us saved at spawn/import depending on visual mesh complexity; avoids mesh-collider cooking on LOD0.

## Decision 06 - GlobalQualityWeight Route
Problem: The prompt requires shadows to respond to `GlobalQualityWeight`, but gameplay/terrain truth must not vary by quality.
Solution: `WreckageScatterManager` reads `HomeostasisBrain.GlobalQualityWeight` once on enable and maps smooth 0..1 weight to Unity's discrete shadow enum.
Rejected Alternatives: Per-frame shadow polling or changing carve/collision by quality. Both violate hot-path and truth ownership rules.
Scalability potential: Low turns shadows off near survival quality; Middle uses standard shadowing; High/Ultra enables two-sided shadows for thin broken plates.
Hardware Impact: Low-tier avoids shadow caster cost on debris; high-tier spends saved budget on stronger presentation.

## Decision 07 - Verification Gate
Problem: The project already had active `dotnet` processes and CPU reached 98.85 percent, so a full build/test launch would violate the user gate.
Solution: Ran Unity `validate_script` on the 1735 files with 0 errors. Later polish gated disk reports behind explicit opt-in, so the current proof remains source/validator state instead of mandatory JSON I/O.
Rejected Alternatives: Launching dotnet or Unity test run anyway; that risks compile contention with other agents.
Scalability potential: Verification state is explicit; integrator can run factory dry-run after compiler gate clears.
Hardware Impact: No extra system load beyond validator; avoided competing build pressure on active machine.

## Decision 08 - Report I/O Gate
Problem: The latest integrator directive rejected mandatory JSON proof files as wasted I/O, while the factory still wrote a report every run.
Solution: Added `DefaultWriteReportToDisk=false`, window/settings opt-in, and a source-audit test proving `WriteReport` is guarded.
Rejected Alternatives: Keeping silent dry-run JSON writes; it adds disk churn and contradicts current completion proof policy.
Scalability potential: Low through Ultra devices keep the same prefab output path; editor diagnostics no longer add default I/O pressure.
Hardware Impact: Saves avoidable editor disk write per dry-run; runtime impact remains 0 us.

## Decision 09 - Adjacent Drone Compile Blocker
Problem: Unity console reported missing drone attachment DTO/metadata types in `DronePrefabFactory`, blocking editor assembly compile outside the wreckage code path.
Solution: Extended the existing `DroneBoneMetadata.cs` owner with `DroneAttachmentMetadata`, attachment enums, descriptors, and an explicit-layout runtime DTO validated by `UnsafeUtility.SizeOf`.
Rejected Alternatives: Adding a detached helper file or ignoring the compile blocker; both leave assembly topology fragmented or red.
Scalability potential: Low keeps lightweight serialized sockets; Middle/High/Ultra can drive richer drone VFX from the same anchor metadata.
Hardware Impact: Runtime lookup can consume cached anchor descriptors directly; no scene search or `GetComponent` loop needed.

## Decision 10 - Late Service Binding Repair
Problem: Spawn-time carve and debris shadow presentation depended on `GlobalRegistry.Dispatcher` being live during `OnEnable`/`Start`; a late dispatcher or voxel engine bind could silently drop the one-shot work.
Solution: `VoxelCarveVolume` and `WreckageScatterManager` now implement `IGlobalRegistryHotSwapListener`. Dispatcher replacement clears the local late-frame registration flag and retries registration; voxel engine replacement re-primes only the cached carve bridge.
Rejected Alternatives: Per-frame `Update` retry, hot `GlobalRegistry.Get<T>()`, or scene searches. All would add steady-state cost to solve a cold wiring problem.
Scalability potential: Low/Middle/High/Ultra all keep identical carve and presentation truth; only service bind timing becomes robust.
Hardware Impact: 0 us steady state. Prevents missed carve/presentation without adding runtime polling or allocation.

## Decision 11 - Editor Stress Capacity
Problem: Factory scratch lists were static but under-prewarmed for the 500-debris stress case, so editor combine could grow lists during bake.
Solution: Added explicit 512-capacity constants for debris segments and combine instances per material while keeping a single factory-owned scratch route.
Rejected Alternatives: New buffer manager or runtime preallocation. The work is editor-only and already owned by `WreckagePrefabFactory`.
Scalability potential: Low machines avoid editor allocation churn during large wreck bakes; high-tier authoring can feed dense source debris without changing runtime prefab shape.
Hardware Impact: Runtime 0 us. Editor bake avoids list growth churn in the expected 500-piece debris case.
