# Rationale 1607 - Modular Station and Structure Architect

Status: PENDING VERIFICATION

## Decision 001 - Offline Editor-Only Station Fabrication

Problem: Runtime generation of abandoned stations would add CPU, hierarchy, and draw-call cost to gameplay.
Solution: Keep all WFC, mesh fusion, socket extraction, hidden-surface removal, dirt masking, and prefab serialization under `Assets/_Project/Editor/Generators/Structures/`.
Rejected Alternatives: Runtime modular spawning was rejected because it leaves hundreds of transforms alive and violates static batching/GRD discipline. Raw prefab YAML mutation was rejected because Unity API asset creation is safer for meshes and prefabs.
Scalability potential: Low uses few modules, baked masks, one structural mesh, and minimal shadows. Middle adds denser layouts and dirt variation. High increases module count and retained detail meshes. Ultra spends saved CPU on richer baked damage masks and longer HLOD residency without runtime solver cost.
Hardware Impact: Low-end i3/MX350 avoids per-frame station assembly and hierarchy traversal; expected gain is CPU-side submission and transform traversal avoidance, pending Unity Stats proof.

## Decision 002 - Socket Bitmasks Before Geometry Fusion

Problem: Mesh merging without a socket contract can hide invalid topology and create light leaks.
Solution: Extract socket data into unmanaged DTOs with connector masks and face directions before WFC or mesh fusion.
Rejected Alternatives: Name-string sockets and scene-object searches were rejected because they are brittle and allocate in tooling loops. Direct designer snapping was rejected because it cannot prove determinism.
Scalability potential: Low caps socket vocabulary and module count. Middle/High/Ultra increase allowed connector masks and station class complexity while preserving the same DTO route.
Hardware Impact: Offline validation removes runtime correction work on compact CPUs; expected runtime gain is zero station-generation CPU cost, pending prefab proof.

## Decision 003 - Burst-Compatible WFC and Mesh Fusion DTOs

Problem: Station assembly needs graph collapse, placement transforms, internal face culling, seam welding, and dirt masks without managed runtime ownership.
Solution: Added explicit-size DTOs and Editor-only Burst jobs in `DeepReachStationContracts.cs`: WFC collapse, transform/append fusion, spatial-hash vertex welding, and deterministic damage mask deformation.
Rejected Alternatives: ScriptableObject graph mutation and GameObject-per-module assembly were rejected because they leave hot hierarchy state and cannot prove deterministic station topology. Compute-only damage was deferred because current proof needs CPU-readable baked meshes and no GPU readback dependency.
Scalability potential: Low reduces placement cap and quality-weight damage spheres. Middle raises cap. High retains more source module detail. Ultra uses the same static output route with higher quality-weight deformation and larger station grids.
Hardware Impact: i3/MX350 runtime avoids WFC, transform loops, socket validation, and per-module renderer submission; estimated removed hot work is roughly 1.8 us per baked module plus 0.11 us per hidden triangle, pending generated prefab stats.

## Decision 004 - Socket-Gated Hidden Surface Removal

Problem: Blind mesh merging either leaves buried triangles or deletes visible exterior panels, creating light leaks.
Solution: The prefab analyzer maps `ModuleSocket` directions to station socket masks, marks only boundary triangles aligned with authored socket directions, and culls them only when WFC emits a matching connected-direction mask.
Rejected Alternatives: Bounds-only face deletion was rejected because detailed modules with protrusions would lose visible geometry. Renderer-level static batching was rejected because hidden triangles remain and draw payload stays inflated.
Scalability potential: Low culls only strict socket caps. Middle/High/Ultra can add richer socket vocabularies and more module types without changing the culling contract.
Hardware Impact: Cheap GPU fill/vertex cost is reduced by deleting seam caps before mesh upload; exact percentage remains in `StationBakeCountersDTO` after fabrication and is not written as a proof report.

## Decision 005 - CPU Wall Enforcement

Problem: Unity compile/fabrication would trigger more compiler work while the host is already saturated.
Solution: Used Unity MCP `validate_script` for all new scripts and withheld `dotnet build`, Unity compile refresh, and prefab fabrication while CPU is 100% and `dotnet:25280` is active.
Rejected Alternatives: Forcing compilation now was rejected because it violates the explicit coordinator CPU rule. Declaring prefab proof without execution was rejected as fake reporting.
Scalability potential: Low-end workstations keep cluster throughput by delaying heavy verification; high-end workstations can run the fabricator immediately after compiler contention clears.
Hardware Impact: Avoided launching a second compiler on saturated i3/MX350-class silicon; saved impact is host stability, not game-frame time.

## Decision 006 - No Ownership of External Compile Break

Problem: Unity console still reports compile errors after 1607 fixes, but the remaining errors are outside the station domain (`OrbitalSkyEphemerisDrift1601EditTests.cs`, then `DropPodSeatController.cs`).
Solution: Do not edit or revert other agents' orbital/drop-pod work. Record the dependency block and keep 1607 code validated independently.
Rejected Alternatives: Patching celestial or drop-pod types from the station agent was rejected as domain breach. Running full EditMode tests through an already broken project compile was rejected because it cannot isolate 1607 proof.
Scalability potential: Low/Middle/High/Ultra unaffected; station bake code remains isolated under Editor structures and can run once the external test assembly compiles.
Hardware Impact: Prevents additional failed compiler loops on saturated hardware; no runtime frame impact.

## Decision 007 - Power-of-Two Weld Bucket Contract

Problem: The seam welding spatial hash used `% bucketCount` in the probing path and did not explicitly reject direct job calls with non-power-of-two bucket buffers.
Solution: `StationVertexWeldingJob` now fails closed on missing arrays or non-power-of-two bucket count, then computes bucket and probe slots with `key & (bucketCount - 1)`.
Rejected Alternatives: Keeping modulo division was rejected because the fabricator already allocates power-of-two buckets and the hot weld loop should not pay integer division. Auto-resizing the bucket buffer inside the job was rejected because jobs must not allocate or mutate ownership.
Scalability potential: Low keeps the same weld correctness with cheaper probing. Middle/High/Ultra can increase source vertex capacity while retaining deterministic O(1) average hash lookup.
Hardware Impact: Removes two integer modulo operations from each weld probe on i3/MX350-class CPUs; exact gain depends on seam vertex count and probe depth.

## Decision 008 - Damage Normal Normalization

Problem: Imported mesh normals are assumed to be unit length, but malformed content could amplify offline crush displacement and open visual gaps at module seams.
Solution: `StationProceduralDamageJob` normalizes each finite source normal before displacement and before algae-mask bias, then stores the normalized normal back to the baked vertex.
Rejected Alternatives: Failing every non-unit normal was rejected because Unity-imported art can contain small normal-length drift and should be repaired by the offline bake. Trusting source normals was rejected because one bad prefab could poison a monolithic station mesh.
Scalability potential: Low gets bounded deformation even with rough content. Middle/High/Ultra keep stronger quality-weight damage without risking unbounded displacement from content import defects.
Hardware Impact: Adds one normalizesafe per baked vertex in the offline Editor job; runtime cost is zero and station hull integrity is more stable on all devices.

## Decision 009 - Structural Material Slot Preservation

Problem: The scanner stored triangle submesh IDs, but the fabricator collapsed all baked geometry into one mesh submesh and one material. That destroyed authored structural material separation and made mixed construction modules visually wrong.
Solution: Extract a filtered structural material vocabulary, reject transparent/leak/glass/ghost/scan materials as hull slots, carry `StationTriangleDTO.SubMesh` through fusion and welding in `NativeArray<ushort>` side buffers, then sort the final index buffer by active material slot before `Mesh.SetSubMesh`.
Rejected Alternatives: Multiple renderers per module were rejected because they reintroduce hierarchy and draw-call cost. One universal grime material was rejected because it erases authored material meaning. Runtime material remapping was rejected because the station prefab must already be final on disk.
Scalability potential: Low keeps one renderer and only active submeshes. Middle keeps structural material fidelity without more GameObjects. High/Ultra can spend richer authored station materials and BRG-compatible material slots while retaining a single baked mesh.
Hardware Impact: Keeps CPU submission flat on i3/MX350-class hardware by avoiding per-material child renderers; GPU cost is only extra mesh submesh ranges, not extra transforms or station assembly logic.

## Decision 010 - Reserved Fallback Material Slot

Problem: Rejected leak/glass/ghost/scan submeshes returned material slot 0, but slot 0 could be the first accepted structural material. That would render rejected visual-only geometry as hull plating instead of deterministic grime fallback.
Solution: Reserve `StationModuleLibrary.Materials[0]` as null fallback before prefab scan, keep rejected materials mapped to slot 0, and let `ResolveStationMaterials` replace null with `MAT_Station_BakedGrime`.
Rejected Alternatives: Keeping slot 0 as "first seen material" was rejected because it aliases rejected visual layers to arbitrary hull material. Creating a second renderer for rejected visuals was rejected because the station prefab must stay one static renderer path.
Scalability potential: Low receives one fallback grime slot with no extra renderer. Middle/High/Ultra can retain up to 15 authored structural slots plus fallback under the same BRG-compatible route.
Hardware Impact: No runtime cost added; prevents material alias artifacts while preserving one-renderer submission on i3/MX350-class hardware.

## Decision 011 - Module-Local Material Hash and Tighter Socket Cap Culling

Problem: `StationMeshSliceDTO.MaterialHash` was derived from global material order, so later modules inherited the first module material hash. Hidden-surface culling also accepted normal dot `0.32`, broad enough to delete diagonal bevel/detail triangles near socket windows.
Solution: `ExtractReadableMeshes` now emits a module-local primary structural material hash, while socket cap removal uses named window constants and `SocketCapNormalDotThreshold = 0.72f`.
Rejected Alternatives: Global material hash was rejected as misleading data ownership. Broad cap culling was rejected because preserving exterior bevels is more important than deleting every borderline triangle.
Scalability potential: Low gets fewer light-gap risks on cheap devices. Middle keeps authored bevel silhouettes. High/Ultra can push denser construction detail without the culler eating diagonal facade triangles.
Hardware Impact: No runtime cost; slightly fewer offline deletions in ambiguous cases, trading minimal baked triangle count for stable seams and cleaner visuals.

## Decision 012 - APEX Test Without Shared Roslyn Dependency

Problem: The station APEX test used `Microsoft.CodeAnalysis`, but the shared `Hecton8.EditModeTests.asmdef` does not declare Roslyn precompiled references. That could turn a static proof test into a compile-time dependency break.
Solution: Replace Roslyn usage in the 1607 APEX test with local lexical scanning for balanced source blocks and method bodies, then run the same forbidden-token assertions against extracted hot methods.
Rejected Alternatives: Modifying the shared test asmdef was rejected because it would affect other agents' tests. Keeping Roslyn was rejected because proof infrastructure must not introduce a dependency violation.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; test infrastructure stays lighter and isolated.
Hardware Impact: Avoids adding Roslyn assembly resolution pressure to the already saturated editor compile path; no gameplay frame impact.

## Decision 013 - Horizontal Closed-Face WFC Compatibility

Problem: A module with a sealed horizontal wall side could force an uncollapsed interior neighbor into contradiction because `BuildCompatibleMask` only allowed Empty when `currentSocket == 0`. That made ordinary side-by-side sealed pressure vessels unstable unless every interior side had a socket.
Solution: Allow closed-face abutment only on horizontal directions when both the current face and candidate opposite face have no socket. Keep top/bottom closed faces forbidden for structural stacks, and keep `StructuralSocketsCompatible` socket-only so connectivity is still proven by real connectors.
Rejected Alternatives: Allowing all closed-face abutment was rejected because it would permit vertical closed-floor stacking. Requiring sockets on every interior side was rejected because it over-constrains station modules and causes false WFC contradictions.
Scalability potential: Low gets fewer false bake failures on small ruined bases. Middle/High/Ultra can use sealed side bays and denser habitat blocks without requiring fake doors on every adjacent wall.
Hardware Impact: Runtime cost is zero. Offline WFC avoids needless contradiction retries and failed bakes; expected save is fabrication stability, not frame time.

## Decision 014 - No Global AssetDatabase Refresh During Bake

Problem: `EnsureAssetFolder` created filesystem directories directly and then called `AssetDatabase.Refresh()`. That is a broad editor import sweep in the middle of a station bake and violates the CPU-throttle spirit.
Solution: Create missing output folders through `AssetDatabase.CreateFolder` one segment at a time and remove the explicit global refresh.
Rejected Alternatives: Keeping `Directory.CreateDirectory` plus refresh was rejected because it can trigger a project-wide import scan. Skipping folder creation was rejected because prefab/mesh serialization must be self-contained.
Scalability potential: Low-end workstations avoid an avoidable editor refresh. Middle/High/Ultra keep deterministic asset output without extra import churn.
Hardware Impact: Runtime cost is zero. Editor bake avoids a global AssetDatabase refresh; exact saved time depends on project import state and disk cache.

## Decision 015 - Triangle-Aligned Welding Output

Problem: `StationVertexWeldingJob` copied index streams before independently proving that `SourceIndexCount` was divisible by three. A malformed upstream buffer could survive welding with a non-triangle-aligned count and fail later during mesh serialization instead of at the topology boundary.
Solution: Add an early `sourceIndexCount % 3 != 0` guard in the welding job, set `FaultInvalidTopology`, zero welded counters, and include `FaultInvalidTopology` in the welding fatal mask. Added an EditMode test for the malformed stream.
Rejected Alternatives: Letting `CreateMeshAsset` catch alignment was rejected because welding owns index remap topology and should not emit an invalid triangle stream. Treating it as `FaultCapacity` was rejected because capacity is not the failure; the source topology contract is broken.
Scalability potential: Low avoids editor crashes from malformed lightweight module meshes. Middle/High/Ultra can process larger fused stations while the same fail-closed topology boundary prevents bad output from reaching asset serialization.
Hardware Impact: Adds one integer remainder check per weld job, not per vertex. Runtime cost is zero; editor failure becomes deterministic and early on i3/MX350-class machines.

## Decision 016 - No Authored Material Mutation in Resolver

Problem: `ResolveStationMaterials` forced `enableInstancing = true` on every authored source material returned by scanned modules. That dirties or mutates content owned by environment/art pipelines even though the baked station is a monolithic renderer and does not need per-source material mutation.
Solution: Return authored materials unchanged from `ResolveStationMaterials`; keep `enableInstancing` only on `MAT_Station_BakedGrime`, the generated fallback material owned by the station bake output.
Rejected Alternatives: Cloning every source material was rejected because it multiplies asset count and breaks authored material identity. Keeping source mutation was rejected because offline generation must not silently alter shared art assets.
Scalability potential: Low keeps one renderer without material side effects. Middle/High/Ultra can keep richer authored structural materials without station bake rewriting their import/asset flags.
Hardware Impact: Runtime cost remains zero. Editor avoids unnecessary material dirtying and asset churn; exact saved import time depends on asset database state.

## Decision 017 - Full-Range Station Seed UI

Problem: `DeepReachStationArchitectWindow` exposed a `uint` station seed through `IntegerField` and clamped values above `int.MaxValue`. That silently removed half of the deterministic 32-bit seed space from the station bake UI.
Solution: Replace the seed field with a text-backed non-zero `uint` parser using invariant culture, then normalize the displayed value after sync.
Rejected Alternatives: Keeping `IntegerField` was rejected because it cannot represent full `uint` seed authority. Using a signed long field was rejected because the source truth is already `uint` and text parsing avoids UI type-version uncertainty.
Scalability potential: Low/Middle/High/Ultra all get the same deterministic seed address space; larger station families can be sampled without artificial UI truncation.
Hardware Impact: Runtime cost is zero. Editor parse cost is cold and only occurs on bake button sync.

## Decision 018 - Asset Folder Segment Character Gate

Problem: `SanitizeAssetFolder` enforced `Assets/` ownership and dot-segment rejection, but it did not reject filesystem-invalid characters before calling `AssetDatabase.CreateFolder`.
Solution: Validate every folder segment against `Path.GetInvalidFileNameChars()` during sanitization and fail closed before native fabrication allocations.
Rejected Alternatives: Letting `AssetDatabase.CreateFolder` fail later was rejected because the error would occur after more editor work and would be less precise. Replacing invalid characters automatically was rejected because folder ownership paths should be explicit, not silently rewritten.
Scalability potential: Low-end editor machines avoid wasted bake setup on invalid paths. Middle/High/Ultra keep the same deterministic output route and cleaner failure mode.
Hardware Impact: Runtime cost is zero. Editor avoids unnecessary allocation/job setup when a user enters an impossible asset path.

## Decision 019 - Box Surrogate Material Slot Preservation

Problem: Non-readable structural module meshes fall back to box surrogate geometry, but surrogate triangles inherited `SubMesh = 0`, aliasing them to fallback grime even when the prefab had a valid structural renderer material.
Solution: Resolve the first accepted structural renderer material for surrogate modules, write that slot into every generated box triangle, and preserve slot 0 only for rejected transparent/leak/ghost/scan materials.
Rejected Alternatives: Keeping grime-only surrogates was rejected because it erases authored hull identity on non-readable modules. Forcing mesh readability was rejected because third-party/import settings may own that decision and the station bake already has a deterministic surrogate route.
Scalability potential: Low keeps one renderer and correct broad hull color on cheap devices. Middle keeps material vocabulary stable across readable and surrogate modules. High/Ultra can use denser authored structural materials without non-readable modules visually collapsing into one fallback material.
Hardware Impact: Runtime cost is zero. Editor pays a cold renderer-material scan only for surrogate modules; no additional GameObjects, renderers, or runtime material remap are introduced.
