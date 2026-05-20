# SHINOBU_215 Rationale

Date: 2026-05-20
Status: POLISH PASS 7 DOCUMENTED / UNITY COMPILE BLOCKED BY CPU GATE

## Initial Technical Boundary

Problem: Hadal arches require overhangs, lava tubes, caves, and welded static geometry that heightmaps cannot represent. Runtime CSG/voxel carving would spend gameplay frame time on editor-authoring truth.
Solution: Keep SDF, CSG, cavity occlusion, Marching Cubes, LOD decimation, and mesh serialization inside Editor-only tooling. Runtime receives immutable mesh assets and optional prefab LOD/collider shells.
Rejected Alternatives: Runtime ProBuilder/CSG, dynamic voxel MonoBehaviours, and intersecting rock-prefab clusters are rejected because they spend CPU/GPU budget on geometry that can be baked once.
Scalability potential: Low uses coarser bake resolutions and shorter LOD distances; Middle uses moderate bake resolution and LOD1 collision where needed; High uses denser normals/cavity rays; Ultra uses richer surface noise and heavier LOD0 geometry while keeping runtime static.
Hardware Impact: Expected low-end i3/MX350 gain is lower draw calls and fewer internal triangles; exact microseconds require Unity profiler evidence and remain PENDING VERIFICATION.

Problem: Native shape records and vertex records must survive Burst/ARM64 without hidden managed copies or unaligned loads.
Solution: Use explicit unmanaged DTOs, raw public fields, fixed byte-size assertions, and Mesh vertex buffer layout declarations.
Rejected Alternatives: C# properties and managed shape classes are rejected because dense voxel loops need direct value access and Burst-friendly layout.
Scalability potential: Same DTO drives all bake tiers; quality changes are numeric weights/resolution/ray count, not binary switches.
Hardware Impact: Alignment reduces ARM64 trap risk and improves predictable SIMD/cache behavior; exact timing is PENDING VERIFICATION.

## SDF Boolean And Extraction Kernel

Problem: Intersecting rock prefabs create overdraw, z-fighting, wasted internal triangles, and multiple renderers for one geological silhouette.
Solution: Represent the target structure as one dense SDF volume, compose shapes with union/subtract/intersection/smooth union, displace only the signed distance band, and extract triangles only where the final scalar field crosses zero.
Rejected Alternatives: ProBuilder booleans, runtime CSG, runtime voxel carving, and artist-stacked prefab arches were rejected because they preserve or create interior overlap cost.
Scalability potential: Low uses 16-48 resolution, short cavity rays, and aggressive LOD2; Middle uses 64 resolution and moderate cavity sampling; High uses 96-128 resolution and richer displacement; Ultra can push higher resolution/ray counts while runtime remains static geometry.
Hardware Impact: Low-end i3/MX350 avoids repeated renderer bounds, overdraw, and CSG CPU cost; expected frame saving is in draw-call and hidden-triangle elimination, exact microseconds require Unity profiler capture.

Problem: A textbook Marching Cubes table would require large managed static tables unless stored as native data, increasing compile and maintenance risk in this parallel batch.
Solution: Use fixed cube tetrahedral decomposition encoded as switch-based constant lookup inside Burst. This still processes cube cells and extracts sign-crossing shell triangles from the unified SDF without managed LUT allocations.
Rejected Alternatives: Managed 256-case triangle tables, third-party mesh booleans, and Surface Nets reuse were rejected. Managed tables violate the zero-GC/hot-loop posture; Surface Nets would change the requested shell character and require cross-domain dependency.
Scalability potential: Tetra decomposition scales by resolution and LOD ratios; weak devices bake coarser monoliths, high-tier machines can spend offline time on denser LOD0.
Hardware Impact: Switch-encoded tetra cases reduce memory pressure and avoid ARM64 table alignment risk; exact extraction timing is recorded by `HADAL_BAKE_REPORT.json` after Unity execution.

Problem: Deep lava tubes need dark crevice response without runtime raymarching or SSAO dependency.
Solution: `BakeCavityOcclusionJob` samples deterministic local SDF rays and writes visibility into vertex color red. Runtime shader can multiply ambient by the packed red channel.
Rejected Alternatives: Runtime AO rays, dynamic shadow probes inside caves, or per-pixel procedural cavity tracing were rejected because static geology can pay this cost once in Editor.
Scalability potential: Low uses 1-4 rays/short distances; Middle uses 6-8 rays; High uses 8-10; Ultra uses up to 12 rays and larger cavity distance. All are continuous through `GlobalQualityWeight` and numeric controls.
Hardware Impact: Expected low-end gain is shader and post-process budget saved in cave interiors; exact microseconds are PENDING UNITY PROFILER.

## Editor Tooling And Data Sovereignty

Problem: Technical designers need repeatable recipes without touching runtime terrain systems or introducing designer-authored CSG MonoBehaviours.
Solution: Provide `Hadal Structure Forge`, a cold Editor window with primitive graph controls, CSV recipe ingestion, preview raymarch, and a single bake entry point that serializes static mesh assets/prefabs.
Rejected Alternatives: Runtime authoring components, per-scene procedural generators, and manual prefab cluster cleanup were rejected because they shift offline asset work into gameplay frames.
Scalability potential: Low/Middle/High/Ultra are represented by continuous resolution, voxel size, cavity rays, noise amplitude, and LOD ratios; no binary quality asset split is introduced.
Hardware Impact: Runtime receives SRP-friendly mesh buffers and LODGroup data. Low-end gain is reduced renderer count and collision complexity; high-end can use heavier LOD0 and longer crossfade ranges.

Problem: The live SDF preview uses `Allocator.Persistent` scratch buffers so Scene View gizmos can draw after the raymarch job exits; those buffers must not survive assembly reload or Editor shutdown.
Solution: `HadalSdfPreviewStore` now disposes on Forge window disable, `AssemblyReloadEvents.beforeAssemblyReload`, and `EditorApplication.quitting`.
Rejected Alternatives: `Allocator.TempJob` preview buffers were rejected because gizmo drawing occurs after the job scope; unmanaged global buffers without reload hooks were rejected as Editor leak risk.
Scalability potential: Low/Middle/High/Ultra preview density can remain numeric while memory ownership remains explicit.
Hardware Impact: Prevents stale persistent NativeArray allocations across reload iterations; runtime impact is zero because this store is Editor-only.

Problem: The Forge button previously called the synchronous `Bake()` path, so long SDF/extraction jobs could still stall the Unity Editor despite being scheduled as Burst jobs.
Solution: Add `HadalArchBakePipeline.BakeAsync`, a single active Editor session that schedules each phase and advances through `EditorApplication.update` only when `JobHandle.IsCompleted` is true. The Forge button now uses this async path and receives completion/failure callbacks.
Rejected Alternatives: Fire-and-forget jobs were rejected because native buffers need owned lifetime and black-box dumps on failure. Blocking `Complete()` immediately after each schedule was rejected for the UI entry point because Task 10/16 require editor responsiveness.
Scalability potential: Low settings finish in fewer updates; High/Ultra settings can run longer without freezing the Forge window. Runtime quality remains controlled by static LOD meshes and continuous numeric bake settings.
Hardware Impact: Runtime frame cost is unchanged. Editor iteration avoids main-thread stalls during SDF, cavity, extraction, and LOD computation; final AssetDatabase serialization still runs on the main thread by Unity design.

Problem: CSV preset parsing can silently allocate if implemented with `Split`, dictionaries, or culture-dependent parsing.
Solution: Parse one cold-loaded text buffer with `ReadOnlySpan<char>`, manual cell slicing, deterministic FNV schema hashing, and a custom numeric parser.
Rejected Alternatives: `string.Split`, LINQ, managed dictionaries, and `float.TryParse(ReadOnlySpan<char>)` were rejected. The span overload can vary by Unity API profile; custom parser is predictable.
Scalability potential: Same CSV schema can author toaster-safe coarse shapes or ultra-dense hero structures by changing numeric recipe values.
Hardware Impact: Cold Editor allocation is limited to the file buffer and UI lists; bake hot loops remain native. Runtime impact is zero because CSV is not loaded in gameplay.

Problem: Static geometry must not enter rollback state or dynamic global authority systems.
Solution: Keep the baker under `Assets/_Project/Scripts/World/OfflineHadalArchBaker`, output mesh assets/prefabs, mark reports as `rollbackExcluded`, and define a rollback exclusion DTO for catalog metadata.
Rejected Alternatives: Adding generated arches to `StateRingBuffer`, polling `GlobalRegistry` in jobs, or installing runtime SDF components were rejected as boundary violations.
Scalability potential: Static baked assets are cheap on weak devices and can be visually excessive on high-end devices via denser offline geometry and baked vertex colors.
Hardware Impact: Excluding mesh data from rollback avoids hashing megabytes of immutable geometry every frame. Exact network CPU saving requires rollback profiler evidence.

Problem: Offline topology must regenerate consistently from the same AUP and CSV graph while also obeying the latest Burst directive for non-rollback domains.
Solution: Keep deterministic AUP seed derivation with `HashFnv1a(double3)` and initialize `Unity.Mathematics.Random` from that seed for noise jitter. Keep Burst jobs on `FloatMode.Fast` because generated geology is static offline asset data and never enters rollback state.
Rejected Alternatives: `UnityEngine.Random`, Unity transform float hashing, and absolute-world float noise were rejected. Full `FloatMode.Deterministic` was rejected in the polish pass because the current mandate restricts it to rollback/kinematics/authoritative state domains.
Scalability potential: Low/Middle/High/Ultra bake settings remain numeric and reproducible by recipe/seed; quality changes do not introduce binary path splits.
Hardware Impact: `FloatMode.Fast` keeps offline bake throughput higher on ARM64 and x86 developer machines. Runtime cost remains unchanged because the mesh is static.

Problem: The noise pass previously constructed `Unity.Mathematics.Random` inside every voxel execution to derive the same seed jitter repeatedly.
Solution: Store `NoiseSeedJitter` in `HadalArchBakeConfigDTO` at offset 108 and build it once during `SanitizeConfig` after resolving the FNV AUP seed. `ApplySdfNoiseDisplacementJob` now reads the raw `float3` from config.
Rejected Alternatives: Per-voxel RNG setup, a mutable static RNG, and a managed cached service were rejected. The jitter vector is bake configuration, so it belongs in the immutable DTO copied into the Burst job.
Scalability potential: Low grids save repeated setup on fewer voxels; High/Ultra grids avoid a multiplicative RNG cost across dense volumes while preserving the same continuous noise controls.
Hardware Impact: Removes one deterministic RNG construction per voxel from the noise pass. On 64^3 this avoids 262,144 identical setup operations; on 128^3 it avoids 2,097,152 setup operations before Simplex evaluation.

Problem: Blunt CS1612 scans for `set;` can false-positive on field names ending in `Offset;`, hiding real property findings in noise.
Solution: Rename the config field/local route to `NoiseSeedJitter`/`seedJitter` without moving the 108-byte field offset.
Rejected Alternatives: Explaining the false positive in reports was rejected because automated audit should be silent when source is clean.
Scalability potential: No runtime or bake algorithm change; this is static-audit hygiene.
Hardware Impact: Zero execution impact. It prevents audit time being wasted on a known false positive.

Problem: A solid shape reaching the SDF volume edge can create an open sliced mesh even when the boolean graph itself is valid.
Solution: Add `SealSdfBoundaryShellJob` after noise displacement. It forces all six density-volume faces to positive distance before cavity and extraction, creating a closed outer cap instead of an open grid-edge wound.
Rejected Alternatives: Trusting artists to keep every shape inside bounds, postprocess hole filling on managed mesh data, or expanding every bake volume blindly were rejected. Bounds discipline belongs in SDF math, not manual scene cleanup.
Scalability potential: Boundary sealing cost scales as one extra parallel pass and is independent of hardware tier; low-tier bakes use smaller grids, high/ultra bakes can afford denser sealed volumes.
Hardware Impact: Prevents malformed collision/render meshes that would waste QA time or create physics leaks. Exact microseconds saved are not a frame metric; it is an asset integrity gate.

Problem: The extraction pass emitted three fresh vertex records per triangle, which is valid for rendering but wasteful for a clean static shell mesh because adjacent triangles carry duplicate positions along shared SDF edges.
Solution: Add `WeldArchMeshJob` after extraction and before LOD generation. It quantizes local positions by a small voxel-relative tolerance, stores the first canonical vertex in a native hash map, and rewrites the index stream to shared vertex rows.
Rejected Alternatives: Unity `Mesh.Optimize`, managed dictionary welding, and post-import mesh processing were rejected because they move topology cleanup into managed/editor black boxes. The weld must stay in Burst-native data before serialization.
Scalability potential: Low bakes reduce redundant vertex bandwidth on small meshes; High/Ultra bakes avoid multiplying per-triangle duplicate payload on dense basalt surfaces while preserving the same continuous quality controls.
Hardware Impact: Reduces serialized vertex rows and downstream LOD input size. Exact savings depend on topology and require Unity bake report/profiler capture; no runtime CSG or runtime weld work is introduced.

Problem: Root `AGENTS.md` now declares R43 as the current root/architecture documentation boundary, while the SHINOBU_215 architecture note still pointed at R42.
Solution: Update only `Docs/ARCHITECTURE/OFFLINE_HADAL_ARCH_BAKER_SHINOBU_215.md` to cite R43 and demote R42 to prior evidence.
Rejected Alternatives: Editing global domain maps or leaving the stale local boundary were rejected. The drift was local to this agent's architecture note.
Scalability potential: No runtime effect; documentation now points at the current proof hierarchy for future low/mid/high/ultra verification passes.
Hardware Impact: Zero execution impact. Prevents audit routing to an older static boundary report.

## Verification Boundary

Problem: Project rules require compile verification, but they also forbid `dotnet build` when CPU load exceeds 50%.
Solution: Checked `Get-Counter '\Processor(_Total)\% Processor Time'` multiple times and observed 100, 100, 100 in the latest pass. No `dotnet`/`csc` process was running, but build was not launched due the explicit CPU gate. Static scans were used instead.
Rejected Alternatives: Launching a build anyway or claiming compile pass without evidence were rejected.
Scalability potential: Verification can resume when CPU falls below the threshold; no code path depends on unverified runtime execution.
Hardware Impact: Avoided adding compile load to an already saturated development machine; Unity compile status remains PENDING.
