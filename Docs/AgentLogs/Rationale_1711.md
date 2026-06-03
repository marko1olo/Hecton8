# Rationale 1711 - Abyssal Flora Mesh Baker

Status evidence: static source only until Unity/import/profiler logs exist.

## Decision 01 - RB-002 isolation route
Problem: `HectonIndirectVegetationRenderer` still allowed `_generateMeshAtRuntime` and `_generateImpostorMeshAtRuntime` to compile into player paths, invoking strip/card mesh factories.
Solution: Convert those flags to disabled defaults and make procedural mesh construction editor-only; player runtime consumes serialized meshes or skips rendering with guarded diagnostics.
Rejected Alternatives: Keeping one-shot runtime generation as "cold" still creates `Mesh` and managed arrays during play. Moving it to startup does not remove GC/upload risk.
Scalability potential: Low uses static authored LOD/cell meshes. Middle uses richer baked cards. High and Ultra spend saved CPU on more dense authored flora fields and shader-driven vertex color motion.
Hardware Impact: Avoids player-time mesh allocation/upload spikes on i3/MX350; static estimate saves 1-5 ms chunk-load spikes and prevents GC debt from generated arrays.

## Decision 02 - RB-013 fallback removal
Problem: `SargassumGlobalDragManager` owns fallback `new Material`, fallback `new Mesh`, and nested prototype array creation when authored assets are missing.
Solution: Require serialized mesh/material/prototype assets; skip visual/scavenger rendering when missing while preserving drag field math.
Rejected Alternatives: Keep primitive fallback for resilience. It fragments SRP Batcher and hides asset authoring failures.
Scalability potential: Low uses one shared static mesh/material. Middle/High/Ultra add more authored prototype variety through serialized arrays, not runtime clones.
Hardware Impact: Removes material clone churn and mesh upload on compact lanes; expected savings 200-900 us on first activation plus avoided SRP batch fragmentation.

## Decision 03 - Silt trail route
Problem: `SargassumCollapseChunk` creates `new GameObject("SiltTrail")` and adds particle components when prefab wiring is absent.
Solution: Runtime requires authored child trail or pooled prefab path; editor-only context menu may still author missing trail before build.
Rejected Alternatives: Runtime fallback particle construction. It allocates GameObject, ParticleSystem, renderer, Gradient, AnimationCurve, and arrays.
Scalability potential: Low prewarms sparse silt trails. Middle/High/Ultra can author denser particle/VFX prefabs while pool capacity remains explicit.
Hardware Impact: Prevents runtime heap spikes and component construction on collapse; expected savings 400-1500 us per missing trail case on i3/MX350.

## Decision 04 - Offline topology studio scope
Problem: The task requires L-System, vertex colors, AO, UV packing, LODs, validation, and serialization without adding runtime cost.
Solution: Create `FloraTopologyStudio1711` under `Assets/_Project/Editor/Generators/Flora/` behind editor assembly rules. MeshData is allocated only in editor save routine; Burst jobs fill native staging buffers.
Rejected Alternatives: Extend runtime strip builder. That keeps procedural geometry available in player builds and violates 3D model offline permanence.
Scalability potential: GlobalQualityWeight controls recursive depth, radial segments, AO rays, and LOD budgets during bake only. Low, Middle, High, Ultra produce different static assets with identical runtime semantics.
Hardware Impact: Player cost becomes serialized mesh load only. Editor bake can spend CPU to buy richer Ultra geometry without touching compact runtime frame budgets.

## Decision 05 - BRG material ownership
Problem: Sargassum scavenger BRG cloned the authored material into a hidden runtime material to bind the matrix buffer.
Solution: Reuse the shared authored material directly and cache the buffer/material binding reference; missing material disables the render branch.
Rejected Alternatives: Keep a BRG-local material clone. It allocates, can fragment SRP batching, and hides missing authoring assets.
Scalability potential: Low and Middle reuse one static material. High and Ultra may provide richer authored material variants through serialized profiles, not runtime clones.
Hardware Impact: Removes one material allocation and shader variant warm path per manager activation; estimated 120-400 us saved on i3/MX350 plus lower batch churn.

## Decision 06 - Silt trail fallback boundary
Problem: Collapse chunks built a child particle GameObject and configured modules when prefab wiring was missing.
Solution: Runtime resolves existing child components or spawns an authored pooled prefab via cached `IObjectPoolService`; the construction helper is editor-only.
Rejected Alternatives: Keep runtime component creation as a recoverability path. That spends heap and component setup during collapse bursts.
Scalability potential: Low can prewarm sparse cheap trail prefabs. Middle, High, and Ultra can author denser VFX prefabs without changing chunk logic.
Hardware Impact: Avoids GameObject/AddComponent/Gradient/AnimationCurve allocations on collapse; estimated 400-1500 us saved per missing-trail activation on i3/MX350.

## Decision 07 - 1711 generator reuse boundary
Problem: Reimplementing the full 1604 mesh writer would duplicate proven MeshData code and create a second maintenance surface.
Solution: 1711 wraps the 1604 MeshData topology generator, then adds 1711 quality profiles, semantic validation, dry-run jobs, asset roots, and reporting.
Rejected Alternatives: Copy 1604 extrusion/serialization wholesale. Duplication increases drift risk without improving runtime purity.
Scalability potential: Low, Middle, High, and Ultra scale bake work through profile values while runtime remains serialized mesh draw only.
Hardware Impact: No player-frame cost; editor-only extra jobs buy better authored silhouettes on high-end workstations while low-tier devices consume static output.

## Decision 08 - Compile guard handling
Problem: Batch required compilation, but local guard checks showed overloaded CPU and a running Unity `dotnet` process.
Solution: Do not launch another build; record Task 23 as blocked by build guard and use `git diff --check` plus static allocation/fence scans as non-build evidence.
Rejected Alternatives: Start `dotnet build` anyway. AGENTS forbids this when CPU >50 or dotnet/csc is already running.
Scalability potential: Preserves shared workstation capacity for 20+ concurrent agents; avoids compounding compiler contention.
Hardware Impact: Prevents additional CPU saturation and compiler contention on shared low-end silicon; no runtime code impact.

## Decision 09 - Vertex color shader contract
Problem: 1711 baker remapped mesh colors to R=sway, G=bioluminescence, B=AO visibility, A=wear, but `Hecton_IndirectVegetation.shader` only consumed R and old A curvature semantics in normal play.
Solution: Keep R as sway/stiffness, treat A as wear/curvature driver, use G as a vertex-authored biolum intensity gate, and encode B into the fractional component of the existing sync-group interpolator so AO affects diffuse/transmission without adding a new TEXCOORD register. The editor-only strip builder now emits the same RGBA semantics for authoring previews.
Rejected Alternatives: Add a dedicated `TEXCOORD24` for AO/wear. That increases interpolator pressure on weak/mobile GPUs when the existing sync-group value can carry a stable fractional AO payload.
Scalability potential: Low tier keeps the same interpolator count and gets baked AO/wear cheaply; Middle/High/Ultra can author richer per-vertex emission and wear gradients without runtime geometry work.
Hardware Impact: Avoids extra varying register pressure while activating baked visual data; expected cost is a few scalar ops per vertex/fragment, with no heap or CPU-frame allocation.

## Decision 10 - GUID-stable flora bake persistence
Problem: 1711 mesh save path deleted existing mesh assets before creating replacements, and prefab save return was ignored.
Solution: Existing LOD mesh assets now update through `EditorUtility.CopySerialized`, preserving GUIDs; prefab save fails closed; seed-pack generation defers to one `AssetDatabase.SaveAssets()` flush.
Rejected Alternatives: Delete/create replacement. It can break references and can report success after partial asset write failure.
Scalability potential: Low, Middle, High, and Ultra bakes can be regenerated without destabilizing prefab references or multiplying editor I/O.
Hardware Impact: Editor-only; avoids redundant asset database flushes and prevents failed bake states from leaking into low-end player content.

## Decision 11 - Flora sediment VFX authoring boundary
Problem: `FloraInteractionManager` created a hidden particle GameObject and ParticleSystem at runtime for dense-grass sediment bursts.
Solution: Runtime now resolves only an authored ParticleSystem child/reference; an editor-only context menu can author the child before build.
Rejected Alternatives: Keep one-shot runtime particle construction. It allocates GameObject/component/module state and hides missing scene authoring.
Scalability potential: Weak devices can ship sparse authored sediment prefabs; high and ultra scenes can author richer burst systems without runtime construction logic.
Hardware Impact: Removes a cold runtime GameObject/AddComponent path, estimated 300-900 us avoided on i3/MX350 first activation.

## Decision 12 - Sargassum micro-fauna presentation purity
Problem: `SargassumMicroFaunaBoids` cloned `boidMaterial` and used `AsyncGPUReadbackRequest.WaitForCompletion()` during teardown.
Solution: Rendering now uses the authored material directly and no `MaterialPropertyBlock`; pending readback teardown is deferred to a cached callback instead of blocking.
Rejected Alternatives: Keep owner-local material clone, MPB on instanced geometry, or synchronous readback wait. The clone fragments material ownership; MPB violates the URP/SRP Batcher mandate; the wait can stall the main thread.
Scalability potential: Low tier avoids material churn and teardown stalls. Middle/High/Ultra still drive VAT and parasite visual uniforms from the authored material in `LateFrameTick`.
Hardware Impact: Removes one material allocation/destruction path and one GPU readback stall vector; estimated 120-400 us activation savings plus unbounded teardown stall avoidance.

## Decision 13 - Scatter readback teardown ownership
Problem: `GPUScatterDirector` still used `AsyncGPUReadbackRequest.WaitForCompletion()` on release, and the first deferred fix could expose a stale args buffer during rapid disable/enable.
Solution: Teardown now marks pending visible-count readback for callback disposal, stores the old args buffer in a dedicated held field, clears the live `_argsBuffer`, and blocks new readbacks until the callback releases the NativeArray and held buffer.
Rejected Alternatives: Keep synchronous wait, or keep the old buffer in the live field. The first can stall the main thread; the second can double-use a buffer that callback later releases.
Scalability potential: Low and middle hardware avoid teardown stalls; high/ultra can keep inspector readback telemetry enabled without poisoning runtime shutdown.
Hardware Impact: Removes an unbounded GPU readback wait path from scatter release and avoids stale-buffer reuse on lifecycle churn.

## Decision 14 - Flora readback global-wait purge
Problem: `HectonIndirectVegetationRenderer` and vegetation tile cache disposal still used global GPU readback waits during teardown, so one diagnostics path could stall unrelated GPU work.
Solution: Cull telemetry now owns release through a cached callback and held counter buffer. Tile height readback disposal uses the existing fixed deferred-disposal array, stores the owning `TileRuntimeState`, and guards that state from reusing its `NativeArray` until request completion.
Rejected Alternatives: `AsyncGPUReadback.WaitAllRequests()` or `WaitForCompletion()` on shutdown. Both serialize the main thread against all GPU readbacks, not just the local flora request.
Scalability potential: Low and Middle avoid teardown hitches. High and Ultra can keep flora diagnostics and terrain cache readbacks active without turning shutdown/reconfigure into a global sync point.
Hardware Impact: Removes two unbounded main-thread stall vectors; expected gain is lifecycle stability rather than steady-state microseconds.

## Decision 15 - Sargassum visual texture workload scaling
Problem: `SargassumGlobalDragManager` rebuilt CPU density/sink textures at the serialized resolution regardless of device quality, so low-end hardware could still pay 256x256 sampling/upload work for visual-only texture data.
Solution: Keep drag truth and density cell math unchanged, but resolve visual texture resolution and refresh cadence from continuous `HomeostasisBrain.GlobalQualityWeight`; the authored inspector value remains the upper cap.
Rejected Alternatives: Binary low/high texture toggle, or changing drag density sampling itself. The first violates quality doctrine; the second changes gameplay authority.
Scalability potential: Low runs smaller, slower-updating visual textures. Middle keeps moderate resolution. High and Ultra can use the authored cap and near-immediate refresh for richer fauna/ocean response.
Hardware Impact: Minimum-quality texture bake drops to 64x64 and can defer refresh up to 18 frames, reducing visual texture texel work by up to 4x versus 128 and 16x versus 256.

## Decision 16 - Sargassum petrification timer queue flattening
Problem: Settled debris petrification used a fixed timer array but dequeued by shifting every remaining entry, creating O(n) struct-copy work in the slow-tick drain path.
Solution: Replace tail shifting with head/tail ring indices, use a 128-slot power-of-two mask, and reject duplicate `Rigidbody` timer entries before enqueue.
Rejected Alternatives: Keep the shift because the array is capped at 128. Collapse bursts can enqueue many bodies, and the avoidable copy loop competes with visual sync work.
Scalability potential: Low and Middle avoid slow-tick spikes during dense canopy collapse. High and Ultra can retain more authored collapse/scavenger dressing without timer compaction cost.
Hardware Impact: Removes up to 127 `DebrisTimer` struct copies per dequeue and prevents repeated physics petrification commands for the same body on i3/MX350-class CPUs.

## Decision 17 - Sargassum texture dirty split
Problem: Density and buoyancy sink textures shared one dirty flag, so clearing an already-empty field could repeatedly clear CPU staging arrays, reload both textures, and apply both uploads.
Solution: Track density and sink upload dirtiness independently and keep clear-state guards so empty-field refreshes only upload a zero map after actual content or texture recreation.
Rejected Alternatives: Keep aggregate dirty state. It is simple but wastes CPU/GPU upload work on no-field or sink-only cases.
Scalability potential: Low reduces visual map churn when sargassum fields are absent or sparse. High and Ultra still get full authored visual refresh when field data exists.
Hardware Impact: Avoids redundant `Array.Clear`, `LoadRawTextureData`, and `Texture2D.Apply` calls on empty fields; compact hardware saves visual-sync bandwidth without changing drag truth.

## Decision 18 - Sargassum scavenger density quality scaling
Problem: Bottom-scavenger presentation generated the authored maximum matrix count regardless of device quality, while the visual is non-authoritative dressing.
Solution: Resolve active scavengers per host from continuous `GlobalQualityWeight`; keep buffers sized to authored max so quality increases do not allocate.
Rejected Alternatives: Binary disable/enable tiers, or resizing buffers with quality. The first causes popping; the second risks runtime allocation when quality changes.
Scalability potential: Low keeps sparse but readable feeding motion. Middle restores more visible activity. High and Ultra use the full authored swarm density.
Hardware Impact: Minimum quality emits roughly 35% of authored scavenger matrices per host, reducing CPU matrix fill and BRG instance count without touching collapse gameplay truth.

## Decision 19 - Scavenger BRG lock flattening
Problem: Scavenger BRG registration acquired a DataVault metadata write lock and called Unity BRG registration plus GraphicsBuffer creation while the lock was held.
Solution: Remove the DataVault metadata handle for this presentation-only placeholder and register the BRG batch from a one-slot cold `Allocator.Temp` metadata array outside any vault lock.
Rejected Alternatives: Keep the lock because registration is cold. The DataVault rule is stricter: lock scope must be copy/direct assignment only, not Unity API calls.
Scalability potential: Low and Middle avoid lock contention during visual resource repair. High and Ultra can rebuild richer presentation resources without blocking DataVault compaction ownership.
Hardware Impact: Removes one lock-held Unity API path and releases failed batch handle buffers immediately; exact gain is lifecycle stability, not steady-state frame time.

## Decision 20 - Dormant density job scaffold purge
Problem: `SargassumGlobalDragManager` retained private density-build source handles and `forceComplete:true` release paths even though current code never schedules that job route.
Solution: Remove the unused source DTO, handle, pending bounds, completion method, and release-time force-complete branches; density rebuild remains the single CPU route used by current runtime.
Rejected Alternatives: Keep the scaffold for future async work. Dead private scheduling state creates false dependency proof and preserves a synchronous completion vector.
Scalability potential: Low avoids hidden release stalls. Middle/High/Ultra can still scale visual texture cadence separately without pretending the density field uses an async route.
Hardware Impact: Removes two unreachable synchronous completion sites and one unused unmanaged DTO; runtime behavior stays on the existing deterministic CPU density path.

## Decision 21 - Micro-fauna formation lock flattening
Problem: `SargassumMicroFaunaBoids.BuildFormationData`, static obstacle refresh, and obstacle harvest performed beacon service reads, MapMagic payload reads, AUP distance checks, and abyssal-flow sampling while holding DataVault write locks.
Solution: Add prewarmed formation/static-obstacle staging arrays and move all bridge/math work before lock acquisition; publish helpers hold one write lock at a time and only copy DTOs plus update counts.
Rejected Alternatives: Keep the current lock scope because formation capacity is small. The issue is not loop length; it is holding relocatable DataVault memory while calling external systems and heavy math.
Scalability potential: Low avoids lock stalls during sparse formation updates. Middle/High/Ultra can afford richer beacon/obstacle fields because DataVault locks remain short copy windows.
Hardware Impact: Removes lock-held MapMagic/AUP/flow-sampling work from formation refresh on i3/MX350 lanes; exact frame gain requires Unity profiler.

## Decision 22 - Micro-fauna MPB purge
Problem: The first micro-fauna material-clone purge still relied on `MaterialPropertyBlock` and `RenderParams.matProps`, which AGENTS and `REND_URP_Graphics_HotPath_Optimization_HLOD` forbid for standard/instanced geometry.
Solution: Remove MPB state completely; `RenderCurrentBuffer()` binds the authored `boidMaterial` and issues `Graphics.RenderMeshIndirect` with direct material state from `LateFrameTick`.
Rejected Alternatives: Restore `new Material(source)`, keep MPB as "zero alloc", or add a parallel rendering wrapper. Clone allocation and MPB both violate current render mandates; a wrapper would duplicate ownership.
Scalability potential: Low/Middle keep SRP-batcher material purity and one indirect draw. High/Ultra retain VAT textures, parasite mode, interpolation, and hit-flash uniforms without runtime material cloning.
Hardware Impact: Removes one managed MPB object and `RenderParams.matProps` binding from the indirect fauna path; exact render-thread impact requires Frame Debugger/Profiler.

## Decision 23 - GPU scatter MPB purge
Problem: `GPUScatterDirector` used one `MaterialPropertyBlock` plus `RenderParams.matProps` for a single scatter indirect pass, despite the URP hot-path mandate forbidding MPB on instanced geometry.
Solution: Bind scatter buffers, visible indices, density bins, biome textures, and draw uniforms directly into the existing authored `scatterMaterial` immediately before the single `Graphics.RenderMeshIndirect` call.
Rejected Alternatives: Keep the MPB because it was allocated cold, or clone `scatterMaterial` to isolate state. Cold allocation does not satisfy the SRP-batcher rule; cloning would restore the material-ownership failure.
Scalability potential: Low/Middle preserve one indirect scatter pass without MPB state. High/Ultra keep biome color/ground texture richness through authored material bindings and unchanged compute culling.
Hardware Impact: Removes one managed MPB object and one per-frame `matProps` binding path from scatter presentation; exact render-thread gain requires Frame Debugger/Profiler.

## Decision 24 - Octahedral impostor MPB purge
Problem: `HectonOctahedralImpostorRenderer` rendered HLOD impostors through one indirect draw but still carried a draw-local `MaterialPropertyBlock` and `RenderParams.matProps`.
Solution: Remove MPB ownership and write atlas textures, matrix stream selection, floating offset, quality, time, and the active instance buffer into the authored material immediately before the single `Graphics.RenderMeshIndirect` call.
Rejected Alternatives: Apply the same blind direct-material conversion to `HectonIndirectVegetationRenderer`. That renderer has near/far/depth/shadow/motion passes that can share material sources; direct writes could collapse pass state to the final writer without a shader/pass buffer redesign.
Scalability potential: Low/Middle keep one cheap HLOD impostor pass with no MPB state. High/Ultra keep atlas depth, normal/depth blending, culling-stream fallback, and continuous quality weight.
Hardware Impact: Removes one managed MPB object and `matProps` binding from the HLOD impostor path; exact render-thread gain requires Frame Debugger/Profiler.

## Decision 25 - Procedural coral MPB purge
Problem: `ProceduralCoralGpuUploadDispatcher` had a dormant but valid flora presentation path that allocated one `MaterialPropertyBlock` and passed it into `Graphics.DrawProceduralIndirect`.
Solution: Bind `_H8CoralMatrices` plus the three sway vectors directly on the supplied authored material immediately before the procedural draw and pass `null` for the property block argument.
Rejected Alternatives: Keep the MPB because the dispatcher has no current repo-local caller. Dormant code still compiles into player assemblies and preserves a forbidden render-state path.
Scalability potential: Low/Middle retain one procedural coral draw without MPB state. High/Ultra retain richer sway density/fault vectors and maximum render-matrix capacity.
Hardware Impact: Removes one managed MPB object and property-block draw argument from the coral dispatcher; exact render-thread gain requires profiler once the path is wired.

## Decision 26 - Scatter backend legacy MPB API purge
Problem: After `GPUScatterDirector` stopped using MPB, `ScatterGPUIBackend` still exposed an internal `BindInstanceBuffer(MaterialPropertyBlock, ...)` method and summary text documenting MPB as the draw binding route.
Solution: Delete the unused MPB method and update the backend contract to material-bound draw payloads.
Rejected Alternatives: Keep the unused method for possible future callers. It would preserve a forbidden API surface inside the same assembly after the actual scatter renderer was purified.
Scalability potential: Low/Middle avoid accidental reintroduction of MPB scatter payloads. High/Ultra keep the same double-buffered GPU instance upload and indirect draw route.
Hardware Impact: No runtime microsecond claim because the method had no repo-local caller; removes one dormant MPB route from compile surface.

## Decision 27 - BRG culling mask allocation purge
Problem: `HectonIndirectVegetationRenderer.OnPerformCulling` allocated a TempJob `NativeArray<byte>` visibility mask for every CPU-culling callback, then scheduled a disposal dependency after finalizing BRG draw commands.
Solution: Use the already allocated `BatchCullingOutputDrawCommands.visibleInstances` memory as deterministic scratch: the parallel visibility job writes near/far/shadow slots at fixed offsets, and the final job counts and compacts those slots in-place before publishing draw ranges.
Rejected Alternatives: A persistent scratch ring would need overlap tracking against BRG callback lifetimes; a serial finalizer that recomputes visibility would avoid allocation but spend more culling math on dense kelp fields.
Scalability potential: Low and Middle remove one allocator/disposal path per flora cull. High and Ultra keep the same parallel culling math and can spend saved allocator pressure on denser authored vegetation.
Hardware Impact: Removes one TempJob allocation plus one disposal job chain per CPU-culling callback on i3/MX350-class lanes; exact frame-time gain requires Unity profiler.

## Decision 28 - Dirty-page state read lock purge
Problem: `TryResolveDirtyPageUploadState<T>` acquired a DataVault write lock only to check whether uploaded data dirty pages were set and to compute first dirty-page byte cost.
Solution: Resolve the existing dirty-page handle through `TryReadOnlyHandle` and scan the read-only view; write-lock code remains only for paths that mutate page flags or clear uploaded pages.
Rejected Alternatives: Keep write lock because page count is bounded. The operation is read-only, and the DataVault rule requires write locks to stay mutation-only.
Scalability potential: Low/Middle remove one lock handoff from native upload state resolution. High/Ultra can keep larger upload page capacities without broadening write-lock scope.
Hardware Impact: Removes one write-lock acquisition/release and keeps only read-only bounded loops in the upload-state path; exact frame-time gain requires Unity profiler.

## Decision 29 - Indirect pass MPB selective prewarm
Problem: `HectonIndirectVegetationRenderer` previously created seven `MaterialPropertyBlock` objects for GPU indirect rendering even when far, depth, shadow, or motion passes were disabled or unavailable.
Solution: Prewarm only the pass payloads required by current authored configuration, mark the attempt complete, and let missing optional pass blocks disable GPU-indirect fallback instead of allocating in `SlowTick()`.
Rejected Alternatives: Direct authored-material binding for this renderer. The near/far/depth/shadow/motion passes can share material assets with distinct payloads, so direct writes would collapse pass state without a shader-side pass buffer redesign.
Scalability potential: Low and Middle scenes skip unused optional pass state. High and Ultra can enable depth, shadow, motion, and far passes with explicitly prewarmed payloads and no per-tick allocation.
Hardware Impact: Skips up to six cold managed MPB objects when optional indirect passes are off and removes the late allocation vector from `SlowTick()`; exact render-thread gain requires Frame Debugger/Profiler.
