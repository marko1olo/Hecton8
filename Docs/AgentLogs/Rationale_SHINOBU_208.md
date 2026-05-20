# SHINOBU_208 Rationale - OFFLINE_GEOLOGY_MESH_BAKER

Date: 2026-05-20
Status: SCANNER_SCHEMA_V2_PATCHED / BUILD BLOCKED BY CPU GATE / RUNTIME ERADICATION VERDICT FALSE

## Decision 001 - Domain Boundary
Problem: Runtime mesh generation scan found broad mesh builders, including live cave voxel MC and vegetation/wreck/outpost helpers. Editing all of them would cross unrelated domains.
Solution: Build the requested Editor-only geology bake path and add a scanner/report for runtime topology sites. Only geology-specific runtime call sites will be removed if source evidence isolates them.
Rejected Alternatives: Blindly deleting `HectonVoxelEngine` MC would break dynamic cave/sonar/navgrid systems. Broad refactor loop rejected.
Scalability potential: Low uses precomputed LOD2/shorter LOD distances; Middle uses LOD1 residency; High uses LOD0 longer; Ultra uses denser baked profiles and richer vertex AO without runtime topology.
Hardware Impact: Avoiding static geology generation at runtime saves main-thread stalls and GC/native scratch pressure on i3/MX350; exact runtime delta requires profiler proof.

## Decision 002 - Vertex Layout
Problem: Unity default mesh APIs can produce variable stream layouts and hidden CPU-side arrays.
Solution: New baked geology meshes use explicit 32-byte interleaved vertex stream: Position Float32x3 (12), Normal Float32x3 (12), Color UNorm8x4 (4, AO in R), UV0 UNorm16x2 (4).
Rejected Alternatives: Tangent Float32x4 in the primary stream rejected because it expands the vertex to 48 bytes and contradicts the 32-byte ARM64 bandwidth target. Triplanar shader path does not require tangent-space UV unwrap.
Scalability potential: Low and Middle consume the same compact mesh. High/Ultra gain visual depth through denser LOD0 profiles and stronger baked AO, not runtime SSAO.
Hardware Impact: 32-byte vertex stride halves fetch pressure versus common 60+ byte authoring streams; expected gain on MX350 is GPU bandwidth headroom, microseconds pending Frame Debugger/profiler.

## Decision 003 - Offline AO as Dear Lie
Problem: Runtime SSAO and per-frame cavity ray work are too expensive for static geology.
Solution: `BakeVertexOcclusionJob` samples the baked SDF around each vertex and writes ambient occlusion into vertex `Color.r`.
Rejected Alternatives: URP SSAO rejected by project graphics mandate. Runtime mesh crevice raycasts rejected because static geology has no gameplay need for live self-shadow truth.
Scalability potential: Low uses baked AO only; Middle can add light SSDO elsewhere; High and Ultra get denser baked meshes and more AO rays during authoring.
Hardware Impact: Replaces recurring GPU/CPU AO work with 4 bytes per vertex already present in the 32-byte stream; expected runtime saving is frame-stable ambient depth, exact us pending profiler.

## Decision 004 - CSV Designer Bridge
Problem: Geology recipes must be tuneable without recompiling C# or editing binary payloads.
Solution: Added `Assets/_Project/Data/Geology/geology_generation_profiles.csv` and a byte parser that reads numeric columns without `string.Split`.
Rejected Alternatives: Hardcoded profile constants rejected because art direction will tune basalt/trench recipes repeatedly. Runtime CSV parsing rejected by data bridge mandate.
Scalability potential: Low profiles cap resolution/budgets; Middle raises LOD1; High/Ultra can add denser profiles and more AO rays while runtime remains immutable mesh loading.
Hardware Impact: Authoring cost moves to Editor; runtime cost is only static mesh rendering and LODGroup selection.

## Decision 005 - Runtime Scanner Verdict
Problem: Static scan found 34 runtime mesh/topology patterns after adding Marching Cubes and mesh upload patterns.
Solution: `RuntimeMeshGenerationScanner` writes `Docs/Reports/GEOMETRY_OPTIMIZATION_REPORT.json` and keeps the remaining sites explicit instead of deleting cross-domain systems.
Rejected Alternatives: Claiming eradication while `HectonVoxelEngine` and other runtime builders remain present rejected as false reporting.
Scalability potential: Low-tier route benefits only after owner-specific removals migrate static geology to baked assets; High/Ultra still need dynamic systems only where gameplay truth requires them.
Hardware Impact: Current code addition saves no measured runtime us until call sites consume baked assets. Static report prevents hidden future regression.

## Decision 006 - Compile Gate
Problem: The project requires compilation verification, but the local machine stayed above the explicit CPU threshold for build work.
Solution: Added static self-audit evidence and attempted a gated build only when preconditions looked legal. The in-command preflight then reported `CPU_AVERAGE=79`, and a six-sample wait produced `100,100,100,97,38,58`; build was not launched.
Rejected Alternatives: Running `dotnet build` while CPU was above 50% rejected because it violates the batch protocol. Reporting success without compiler output rejected as false evidence.
Scalability potential: No runtime scalability claim changes until Unity import/compiler/profiler data exists. Low/Middle/High/Ultra path remains baked mesh plus LODGroup and profile-driven density.
Hardware Impact: 0 measured microseconds saved in this pass because runtime was not profiled. Expected gain remains removal of static geology topology generation from runtime once consumers switch to baked assets.

## Decision 007 - Explicit Raw Layout Polish
Problem: `GeologyRawVertex` was sequential and 56 bytes. It was technically 8-byte aligned, but it left hot editor worker writes sharing cache lines and did not provide byte-offset proof.
Solution: Converted `GeologyVertex32` and `GeologyRawVertex` to explicit layout. `GeologyVertex32` is exactly 32 bytes for GPU upload; `GeologyRawVertex` is exactly 64 bytes with offset 56 padding to isolate parallel writes. `GeologyVertexLayoutValidator` now checks struct size and every field offset.
Rejected Alternatives: Keeping `Sequential` rejected because the audit required proof, not compiler trust. Packing tangent into the 32-byte runtime stream rejected because it would either exceed the stride or destroy normal/UV precision; tangent remains editor-authoring data and runtime triplanar material does not need tangent-space unwrap.
Scalability potential: Low/Middle/High/Ultra all consume the same 32-byte render stream; authoring scratch gets 64-byte rows for safer parallel writes while runtime memory stays compact.
Hardware Impact: Runtime vertex fetch remains 32B. Editor scratch memory rises from 56B to 64B per raw vertex, buying cache-line isolation in parallel jobs; runtime measured gain remains pending.

## Decision 008 - CSV Native Scratch And Continuous Quality
Problem: The CSV bridge used `File.ReadAllBytes`, and quality only partially affected bake math.
Solution: CSV now streams into a Temp `NativeArray<byte>` and parses via unmanaged byte pointers. `GlobalQualityWeight` is smoothed through `math.smoothstep` and continuously drives noise octaves, AO rays, AO steps, AO range, UV scale, LOD triangle budgets, and collapse cell size.
Rejected Alternatives: Keeping managed byte arrays rejected because the profile ingest task explicitly asked for allocation-free byte parsing. Binary low/high quality switches rejected by the scalability pillar.
Scalability potential: At low weight the bake collapses to cheap 2-octave noise, 8 AO rays, 2 AO steps, shorter AO distance, lower budgets, and coarse collapse. Middle weights interpolate. Ultra approaches authored profile limits with dense LOD0 and stronger baked AO.
Hardware Impact: Editor bake cost scales with quality instead of forcing one expensive path. Runtime cost is still static mesh rendering and LODGroup selection; no runtime microseconds measured.

## Decision 009 - Collider Rejection
Problem: The generated prefab previously carried an LOD2 `MeshCollider`, which risks turning a render-bake lane into runtime physics truth.
Solution: Removed collider generation from GeologyForge prefabs. This lane now outputs render meshes and LODGroup only. Collision or terrain queries must use an owner-specific collision/proxy/SDF route.
Rejected Alternatives: Convex `MeshCollider` rejected because it violates the Dear Lie boundary for static geology rendering and creates runtime physics work unrelated to visual rock rendering.
Scalability potential: Low through Ultra use the same no-collider render prefab; physics/collision scalability remains a separate owner lane.
Hardware Impact: Avoids adding runtime collider cooking/query burden to generated geology prefabs. Exact runtime saving pending scene/profiler proof.

## Decision 010 - Editor Bake Black Box
Problem: The bake lane had JSON metrics but no 300-frame forensic ring. A failed SDF extraction, non-finite timing value, or exception would leave no compact binary trail for the endurance bot.
Solution: Added explicit 64B `GeologyBakeTelemetryEntry` rows and a 32B `GeologyBakeDumpHeader`. `GeologyForgeGenerator` records SDF, count/extract, attribute, AO, and serialization stages into a 300-entry TempJob ring and writes `Docs/AgentLogs/Dump_SHINOBU_208.bin` on non-finite timing or exception.
Rejected Alternatives: Chat-only diagnostics and managed exception strings rejected. Persistent runtime Vault ownership rejected because this lane is Editor-only and must not create a gameplay owner or rollback state.
Scalability potential: Low through Ultra all use the same tiny fixed forensic ring; bake complexity still scales continuously through `GlobalQualityWeight`, while the telemetry footprint remains capped at 19,232 bytes plus file metadata.
Hardware Impact: Runtime cost remains 0 us because no runtime code is added. Editor failure analysis gains deterministic binary context without Play Mode allocation or runtime service coupling.

## Decision 011 - BRG Manifest Instead Of Generated Prefabs
Problem: The forge emitted `.prefab` wrappers with `LODGroup` and child renderers. That is acceptable for editor inspection, but it is the wrong runtime handoff for 10,000 static geology instances because it encourages GameObject placement instead of BRG/indirect mesh submission.
Solution: Removed prefab/LODGroup/GameObject output from GeologyForge. The bake now writes only LOD mesh assets plus `Assets/_Project/BakedGeometry/Geology/geology_mesh_manifest.h8geom`, a fixed binary manifest with 64B header and 128B records carrying LOD GUIDs, AUP seed, bounds, triangle counts, variation, BRG-ready flag, and vertex stride.
Rejected Alternatives: Keeping generated prefab wrappers rejected because it preserves a standard Unity object route. Adding runtime loader code rejected because SHINOBU_208 is Editor-only and must not create a new runtime owner or assembly dependency.
Scalability potential: Low can read the manifest and submit only LOD2 ranges; Middle can admit LOD1 by distance; High/Ultra can admit longer LOD0 distances and richer instance scalars. The manifest contains facts; the runtime quality dictator owns continuous transition policy.
Hardware Impact: Avoids generated scene-object count and LODGroup CPU management for this lane. Expected runtime saving is object traversal/culling overhead removal once BRG consumer imports the manifest; measured runtime gain remains pending.

## Decision 012 - Byte Ingest Hygiene
Problem: CSV ingest had already moved off managed `File.ReadAllBytes`, but the replacement still used per-byte `ReadByte()` IO. The asset-folder helper also used `Split('/')`, leaving tokenization residue in the owned source.
Solution: CSV loads now read into the Temp `NativeArray<byte>` through an unmanaged `Span<byte>` and chunked `FileStream.Read`. Folder creation now walks slash indices without `string.Split`.
Rejected Alternatives: Keeping per-byte IO rejected because 5000-asset bake batches should not pay avoidable editor-file overhead. Managed token splitting rejected because the assignment explicitly calls out allocation-free profile ingest.
Scalability potential: Low through Ultra profiles still parse the same deterministic bytes; authoring throughput improves without changing runtime.
Hardware Impact: Runtime cost remains 0 us. Editor IO overhead drops from O(bytes) virtual calls to chunked stream reads; exact ms pending Unity editor bake run.

## Decision 013 - Explicit Layout Self-Audit Command
Problem: The validator existed but there was no operator-facing audit command that inspected generated `.asset` meshes and the BRG manifest after a bake. That left Task 20 dependent on manual inspection or source review.
Solution: Added `GeologyForgeSelfAudit` with menu/window access. It validates every generated mesh in the geology output folder through `GeologyVertexLayoutValidator.ValidateMesh`, validates the `.h8geom` manifest header/records from disk using stack/Span reads, and writes `Docs/Reports/GEOLOGY_LAYOUT_AUDIT.json`.
Rejected Alternatives: Manual Unity Inspector checks rejected as non-repeatable. Runtime validation rejected because the geology bake lane must stay Editor-only and must not introduce runtime loaders.
Scalability potential: Low through Ultra all consume the same verified 32B vertex stream and manifest facts; the runtime quality owner can vary LOD distance without changing asset layout.
Hardware Impact: Runtime cost remains 0 us. Editor audit cost is O(mesh assets + manifest records) and runs on demand after bake, not during gameplay.

## Decision 014 - Little-Endian Binary Guard
Problem: `.h8geom` and black-box dump writers serialize unmanaged structs directly. Without an endian contract, a future non-little-endian authoring host could silently produce unreadable payloads.
Solution: Added explicit `BitConverter.IsLittleEndian` guards before writing manifest and dump payloads. The manifest self-audit fails closed with `BIG_ENDIAN_HOST_UNSUPPORTED` when the host cannot safely validate little-endian raw records.
Rejected Alternatives: Runtime byte-swapping in SHINOBU_208 rejected because this lane has no runtime reader and must stay Editor-only. Silent native-endian output rejected because it makes payload corruption non-local and hard to diagnose.
Scalability potential: No quality-tier behavior changes. Low through Ultra all consume the same byte-stable static payload format once a runtime owner imports it.
Hardware Impact: Runtime cost remains 0 us. Editor cost is a single branch before payload IO; it prevents cross-platform binary corruption.

## Decision 015 - Preview Buffer Hygiene
Problem: The SceneView preview path created `List<Vector3>` and then `ToArray()` on every preview bake. It is editor-only, but Task 18 asks for a fast live voxel preview and repeated slider edits would produce avoidable managed churn.
Solution: Replaced per-preview point-list allocation with one bounded cold `Vector3[2048]` buffer plus `_pointCount`. The preview SDF still uses local TempJob scratch and disposes it in `finally`; draw uses an index-based `for` loop over the active count.
Rejected Alternatives: Persistent `NativeArray<Vector3>` rejected because this is an Editor facade and would create unnecessary native lifetime ownership. Full mesh preview rejected because it defeats the point-cloud fast path.
Scalability potential: Low preview remains capped at 2048 points. Middle/High/Ultra can raise SDF authoring profile quality for final bake without changing this lightweight preview guard.
Hardware Impact: Runtime cost remains 0 us. Editor preview removes one `List` allocation and one `Vector3[]` allocation per preview command; exact ms/GC pending Unity editor profiler.

## Decision 016 - Cold Allocation Comment Canon
Problem: Owned GeologyForge scratch allocations had `COLD ALLOC` comments, but several used hyphen separators instead of the exact AGENTS.md canonical form. That weakens reviewer searchability and compliance evidence.
Solution: Normalized all owned GeologyForge `COLD ALLOC` comments to `Type[count] — reason — owner`.
Rejected Alternatives: Leaving comments non-canonical rejected because the project mandates exact evidence strings for cold allocations.
Scalability potential: No behavior change. Low through Ultra bake quality remains controlled by `GlobalQualityWeight`; this patch improves auditability only.
Hardware Impact: Runtime cost remains 0 us. Editor/runtime behavior unchanged; compliance search now has deterministic text.

## Decision 017 - Angle-Weighted Normal Weld Buckets
Problem: The normal pass previously blended per-triangle face normals with SDF gradients, but triangle-soup vertices emitted on shared edges did not accumulate neighboring face normals. Task 07 explicitly requires angle-weighted smoothing, and the previous deviation left visible faceting risk in baked rocks.
Solution: Added `BuildNormalBucketJob` and extended `CalculateSmoothNormalsJob`. The new path builds deterministic `NativeParallelMultiHashMap<ulong,int>` buckets from quantized vertex positions, searches the 27 adjacent buckets per vertex, rejects candidates outside a voxel-relative tolerance, accumulates each candidate triangle's face normal weighted by its corner angle, aligns the result to the SDF gradient, then writes normal and tangent through `UnsafeUtility.AsRef`.
Rejected Alternatives: `Mesh.RecalculateNormals` rejected because it uses Unity managed mesh-side processing and hides layout/control. O(N^2) all-vertex smoothing rejected because authoring batches can generate tens of thousands of vertices per asset.
Scalability potential: Low quality keeps cheaper geometry budgets and fewer raw vertices, reducing bucket load. Middle/High/Ultra retain the same smoothing path while buying denser LOD0 and stronger baked AO through `GlobalQualityWeight`.
Hardware Impact: Runtime cost remains 0 us. Editor normal smoothing changes from per-triangle local only to O(V + local bucket neighbors) with bounded 27-bucket lookup; exact editor milliseconds pending Unity bake/profiler.

## Decision 018 - Scanner Schema V2 Context Classification
Problem: Task 19 evidence previously reported raw string hits only. `findingCount=34` was a useful stop signal, but it did not distinguish actionable runtime helpers from comments, type-scope declarations, or material clone hazards.
Solution: Extended `RuntimeMeshGenerationScanner` to classify each hit by forbidden kind, execution context, owning method, risk label, and comment-only status. The method parser handles multi-line signatures such as `static void UploadSurfaceMesh(` and ignores attributes such as `[BurstCompile(...)]` so report context is not polluted by decorators. The eradication boolean is tied to actionable findings, not comment-only archaeology. The refreshed CLI report now records `findingCount=34`, `actionableFindingCount=28`, `simulationPhaseFindingCount=0`, `bootstrapPhaseFindingCount=0`, and `proceduralMaterialCloneFindingCount=0`.
Rejected Alternatives: Roslyn package dependency rejected because the Editor tool must stay self-contained and avoid a new compile-wall dependency. Blind cross-domain deletion rejected because the 28 actionable findings belong to voxel, wreckage, vegetation, outpost, brine, radar, and sargassum owners.
Scalability potential: Low/Middle/High/Ultra runtime paths are unchanged by the scanner itself. The report improves migration order: owners can move runtime helper mesh construction to baked assets or BRG/GPU Resident Drawer lanes without SHINOBU_208 inventing new runtime dependencies.
Hardware Impact: Runtime cost remains 0 us. Editor static scan cost observed at 34-44 s in CLI context refresh on the loaded machine; Unity menu execution is pending import/compile proof.

## Decision 019 - Direct GlobalQualityWeight Noise Consumption
Problem: Preview and mock SDF generation relied on caller-resolved scalar parameters. The final mesh bake used `GlobalQualityWeight` indirectly, but `GenerateMockFractalNoiseJob` itself did not consume the continuous quality scalar, and the SceneView preview could drift from bake quality behavior.
Solution: Added `GlobalQualityWeight` to the unmanaged noise job DTO. The job now smoothsteps the weight and uses it to scale safe frequency, displacement amplitude, ridged contribution, Voronoi frequency/contribution, and fractional octave contribution. Both full bake and SceneView preview pass the same profile weight.
Rejected Alternatives: Keeping quality only in the caller rejected because it leaves a hidden binary/discrete boundary in the core SDF generator. Adding runtime quality variants rejected because this lane is Editor-only and must output immutable assets.
Scalability potential: Low reduces high-frequency shape noise and suppresses Voronoi scars while retaining believable silhouette. Middle interpolates stable ridged detail. High and Ultra preserve the authored octave span and richer cavity detail for stronger baked AO and LOD0 silhouettes.
Hardware Impact: Runtime cost remains 0 us. Editor ALU scales down at low weights through fewer active octaves and lower-frequency SDF detail; exact bake milliseconds remain pending Unity editor execution.

## Decision 020 - Unsaved Bake Mesh Lifetime
Problem: The public `BakeSingle(profile, variation, saveAssets:false)` path created LOD0/1/2 `Mesh` objects and returned metrics without asset ownership. Current UI callers use `saveAssets:true`, but CI probes or editor diagnostics can call the false path and retain native mesh objects.
Solution: Wrapped the LOD metric/save block in a `try/finally` and added `DestroyTransientLods` for the unsaved path. Saved paths still retain asset-owned meshes; unsaved paths release transient UnityEngine mesh objects deterministically.
Rejected Alternatives: Removing the public non-save overload rejected because it is useful for smoke probes and report-only validation. Destroying meshes after asset save rejected because saved/existing asset references must remain valid.
Scalability potential: Low through Ultra behavior is unchanged. The patch protects repeated non-asset bake probes from editor memory slope while the same continuous quality controls remain active.
Hardware Impact: Runtime cost remains 0 us. Editor/native memory retention risk drops for report-only bake loops; exact memory slope requires Unity editor profiling.
