# SHINOBU_208 Rationale - OFFLINE_GEOLOGY_MESH_BAKER

Date: 2026-05-20
Status: CARVER_AUDIT_PATCHES_STATIC / UNITY PROJECT REGEN REQUIRED / RUNTIME ERADICATION VERDICT FALSE

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

## Decision 021 - Noise Quality Single Owner
Problem: Full bake still pre-scaled `Octaves` before assigning the job DTO, while `GenerateMockFractalNoiseJob` also consumed `GlobalQualityWeight` to derive fractional octave span. That split ownership could double-collapse low-quality profiles and make preview/bake quality behavior diverge.
Solution: `BakeSingle` now passes sanitized raw `profile.Octaves` into `GenerateMockFractalNoiseJob`. The job remains the single owner of SDF octave collapse through `GlobalQualityWeight`; the generator-level `qualityCurve` is retained only for UV scale, AO rays/steps/range, and LOD triangle/collapse budgets.
Rejected Alternatives: Keeping caller-side octave lerp rejected because it hides part of SDF quality policy outside the Burst kernel. Removing generator-level quality completely rejected because AO/UV/LOD budgets are separate authoring costs and not SDF noise math.
Scalability potential: Low weights still suppress high-frequency SDF detail through fractional job octaves, reduced frequency, reduced amplitude, lower ridged contribution, and suppressed Voronoi contribution. Middle/High/Ultra preserve richer authored octave spans while AO and LOD budgets continue to scale continuously.
Hardware Impact: Runtime cost remains 0 us. Editor low-quality bakes avoid accidental double reduction and keep predictable authoring output; exact bake milliseconds remain pending Unity editor execution.

## Decision 022 - UI Progress Callback
Problem: The UI Toolkit `ProgressBar` in `GeologyForgeWindow` only moved from 0 to 1 around a synchronous bake call, while the actual batch progress was visible only through `EditorUtility.DisplayCancelableProgressBar`.
Solution: `BakeProfiles` now accepts an optional cold `Action<float>` progress callback and reports by completed variation count. `GeologyForgeWindow` passes `SetBakeProgress`, clamps the value, marks the progress bar dirty, and repaints the window.
Rejected Alternatives: Adding runtime-visible progress state rejected because GeologyForge is Editor-only. A full async editor scheduler was rejected in this tail patch because it would be a broader control-flow rewrite without compiler proof available under the current CPU gate.
Scalability potential: Low through Ultra asset quality behavior is unchanged. Human operators get truthful progress when baking many profile variations, reducing canceled/restarted authoring runs.
Hardware Impact: Runtime cost remains 0 us. Editor UI now does O(1) progress updates per baked variation; exact editor responsiveness still requires Unity editor execution.

## Decision 023 - Editor Async Batch Runner
Problem: `BakeAll` still invoked a full synchronous batch call. The UI progress callback made state truthful, but the Editor call stack remained occupied until all variations finished.
Solution: Added `BakeProfilesAsync`, a static Editor-only runner driven by `EditorApplication.update`. It processes one profile variation per editor tick, reuses the existing private bake kernel, accumulates batch metrics and manifest records, writes the manifest/report once at finish or cancel, and keeps each 300-row TempJob telemetry ring local to a single tick.
Rejected Alternatives: `async/await` rejected because Unity editor async introduces managed state machines and cancellation/lifetime complexity. Persistent `NativeArray` telemetry fields rejected because the async runner spans editor frames; TempJob telemetry is created/disposed per variation instead. Runtime worker/GlobalRegistry integration rejected because SHINOBU_208 is strictly Editor-only.
Scalability potential: Low through Ultra generated asset quality is unchanged. Human operators can launch large profile libraries without one monolithic window call; strong machines still bake richer variants because the math budgets remain `GlobalQualityWeight` driven.
Hardware Impact: Runtime cost remains 0 us. Editor responsiveness improves between variations; exact responsiveness and bake timing require Unity editor execution.

## Decision 024 - Async Bake Cancel Guard
Problem: An editor async batch can span multiple editor updates while `AssetDatabase.StartAssetEditing` is open. Domain reload or operator abort must not leave the asset database editing scope open.
Solution: Added `CancelAsyncBake`, registered it with `AssemblyReloadEvents.beforeAssemblyReload`, and exposed a UI Toolkit `Cancel Bake` button. Cancel routes through the same finish path as user-canceled modal progress: unsubscribe update, clear progress UI, stop asset editing, write partial manifest/report, reset the UI progress scalar to 0, and clear static batch state.
Rejected Alternatives: Relying only on the modal `EditorUtility` cancel rejected because it does not cover assembly reload. Letting domain reload reset static fields without calling `StopAssetEditing` rejected because it risks editor asset database state leakage.
Scalability potential: Low through Ultra output math is unchanged. Large batch authoring becomes safer because abort/reload paths close the editor asset transaction deterministically.
Hardware Impact: Runtime cost remains 0 us. Editor safety improves; exact abort behavior requires Unity editor execution.

## Decision 025 - Packed Tetra Edge LUT
Problem: `SdfToMeshExtractionJob` still routed tetra cases through a chained `if/else` tree (`EmitOne`/`EmitPair`). It produced geometry, but the XML asks for LUT-driven edge intersections and the old shape was harder to audit for count/extract parity.
Solution: Added `GeologyTetraExtractionLut`, a Burst-safe static constant table encoded as packed 4-bit edge indices. `SdfCellVertexCountJob` and `SdfToMeshExtractionJob` now derive the same 0..15 tetra case index, read the same vertex count, and emit triangles through `EdgeSequence`/`EdgeAt` without managed arrays or runtime object state.
Rejected Alternatives: A managed `byte[]`/`int[]` lookup table was rejected because Burst static managed arrays are fragile and can create hidden initialization concerns. Keeping the branch tree rejected because it leaves the extraction proof dependent on control-flow reading instead of explicit case data.
Scalability potential: Low through Ultra output budgets remain controlled by `GlobalQualityWeight` in noise, AO, and LOD. The extraction kernel is deterministic for every tier; higher tiers buy more triangles through profile budgets instead of runtime generation.
Hardware Impact: Runtime cost remains 0 us. Editor extraction gets simpler branch topology and shared count/extract parity; exact Burst assembly/vectorization proof remains pending Unity/Burst import when CPU gate opens.

## Decision 026 - GeologyForge Editor Assembly Wall
Problem: The GeologyForge files lived under the broad `Hecton8.Editor` assembly, which references Core, graphics, Addressables, MapMagic, Crest, EasySave, test assemblies, and other unrelated editor surfaces. A small geology bake edit would unnecessarily pull a large compile/reload dependency surface.
Solution: Added `Assets/_Project/Scripts/Editor/GeologyForge/Hecton8.World.OfflineGeology.Editor.asmdef` with `includePlatforms: Editor`, `allowUnsafeCode: true`, and references limited to `Unity.Burst`, `Unity.Collections`, `Unity.Jobs`, and `Unity.Mathematics`. No sibling World/Environment/Runtime assembly reference is introduced.
Rejected Alternatives: Leaving the files in `Hecton8.Editor` rejected because it violates compile-wall discipline. Referencing `Hecton8.Core` or `Hecton8.World.*` rejected because this lane is an offline authoring producer, not a runtime owner.
Scalability potential: No runtime quality behavior changes. Low/Middle/High/Ultra assets are still controlled by continuous bake weights and manifest facts; this change protects authoring iteration speed.
Hardware Impact: Runtime cost remains 0 us. Editor compile blast radius should shrink after Unity reimports the asmdef; exact compile time delta requires Unity import/compiler log, currently blocked by CPU gate.

## Decision 027 - CSV Iso-Level Persistence
Problem: The UI Toolkit facade exposed `Iso-Level`, and the bake job consumed `profile.IsoLevel`, but `geology_generation_profiles.csv` had no persisted iso column and `GeologyProfileCsv` forced every loaded profile to `0f`. Designer threshold tuning would evaporate on reload unless entered manually in the window.
Solution: Added an `iso_level` column to `geology_generation_profiles.csv` and taught `GeologyProfileCsv` to scan the header bytes for that token before parsing. When present, `IsoLevel` is read and clamped to the slider-safe `[-0.5, 0.5]` range. When absent, old CSV layouts remain valid and quality is still read from the old position.
Rejected Alternatives: Slider-only iso tuning rejected because Task 17 requires human-readable recipe persistence. Blindly changing column order without a header guard rejected because existing local CSVs would misparse quality and LOD budgets.
Scalability potential: Low through Ultra can now tune density threshold per recipe without code changes; low-tier recipes can bias toward cheaper silhouettes, while high/ultra recipes can preserve denser crevice topology for stronger baked AO.
Hardware Impact: Runtime cost remains 0 us. Editor parse cost adds one first-line byte scan; it prevents repeated manual rebakes caused by lost threshold tuning.

## Decision 028 - Async Menu Path Purge
Problem: The Geology Forge window used the async runner, but the `Bake CSV Profiles` menu item still invoked the monolithic batch method. That preserved a synchronous operator path that could lock the Editor during a large 500-asset authoring run.
Solution: Removed the public monolithic `BakeProfiles` batch method and routed the menu through `BakeProfilesAsync`. Duplicate or empty requests now fail closed through the existing async guard instead of starting a second asset-editing scope.
Rejected Alternatives: Keeping the synchronous method for convenience rejected because Task 10 requires the single-button path to leave the editor unblocked. Adding another coroutine/async-await wrapper rejected because the existing `EditorApplication.update` runner already owns asset-editing lifetime and cancel behavior without managed state machines.
Scalability potential: Low through Ultra generated asset quality remains controlled by `GlobalQualityWeight`; the authoring control path now scales by yielding between variations instead of creating one blocking call stack.
Hardware Impact: Runtime cost remains 0 us. Editor responsiveness improves for large CSV batches; exact milliseconds remain pending Unity editor execution.

## Decision 029 - Editor Preview Hook Lifetime
Problem: `GeologyForgePreview` subscribed a static `SceneView.duringSceneGui` callback in its static constructor and `OnDisable` only cleared point count. A closed Forge window still left an idle SceneView delegate alive.
Solution: Added explicit preview hook lifetime: `Build` calls `EnsureSubscribed`, `OnDisable` calls `GeologyForgePreview.Shutdown`, and `Shutdown` removes the SceneView callback and clears the point count. Window bake buttons now route through `TryStartBake` so rejected async starts reset stale progress and emit a cold editor warning.
Rejected Alternatives: Keeping a permanent zero-point callback rejected because it is unnecessary editor global state. Adding a runtime preview owner rejected because this is strictly an Editor facade.
Scalability potential: Low through Ultra generated assets are unchanged; editor preview cost now exists only after an active preview request and is removed when the facade closes.
Hardware Impact: Runtime cost remains 0 us. Editor SceneView removes an idle delegate call after window close; exact editor repaint gain is negligible but the lifetime boundary is now explicit.

## Decision 030 - Mesh Bounds NaN Vaccination
Problem: `CalculateBounds` initialized min/max from `vertices[0].Position` before checking finiteness. A single poisoned first raw vertex could propagate NaN into `Mesh.bounds`, submesh bounds, and `GeologyMeshManifestRecord.BoundsCenter/BoundsExtents`, even when later vertices were valid.
Solution: Bounds now scan from index 0, ignore non-finite positions, initialize min/max only from the first finite position, and emit the existing 1m fallback only if every row is non-finite.
Rejected Alternatives: Trusting upstream pack jobs rejected because bounds are the final payload gate before Unity mesh metadata and `.h8geom` records. Throwing on first bad row rejected because the pack job already sanitizes vertex stream positions; bounds should quarantine poison instead of aborting a salvageable editor bake.
Scalability potential: Low/Middle/High/Ultra output quality is unchanged. The patch protects every quality tier from a metadata-only NaN escape while continuous bake math still controls detail budgets.
Hardware Impact: Runtime cost remains 0 us. Editor cost is one boolean and one branch per raw vertex during mesh object creation; expected cost is negligible compared with SDF extraction/AO, and it prevents invalid culling bounds from reaching runtime consumers.

## Decision 031 - Async Finish State Hardening
Problem: `FinishAsyncBake` reset `_asyncProfiles`, metrics, manifest records, counters, and callbacks only after manifest/report writes and progress callback invocation. If artifact IO or a UI callback threw, the asset edit scope was already closed but static state could remain non-null, blocking every later bake request.
Solution: Wrapped the finish artifact writes and progress callback in `try/finally`, with all static runner state cleared in the `finally` block.
Rejected Alternatives: Trusting manifest/report writes rejected because disk/import faults are exactly the path where recovery must be deterministic. Swallowing artifact exceptions rejected because operators still need the real failure surfaced in the Console.
Scalability potential: Low/Middle/High/Ultra output quality is unchanged. Large multi-profile bakes now recover their editor runner state after finish-path faults instead of requiring a domain reload.
Hardware Impact: Runtime cost remains 0 us. Editor cost is one `try/finally` frame per batch finish; it prevents wedged authoring sessions during high-volume geology library generation.

## Decision 032 - Burst Attribute Finite Guards
Problem: Several final Burst kernels assumed upstream rows were finite: triplanar UV generation read raw position/normal, AO nearest sampling rounded sample positions, LOD snap floored raw positions, and UV packing packed `math.frac(uv)`. Upstream generation already sanitizes most data, but final payload kernels should not depend on a single upstream proof.
Solution: Added local finite guards in `GenerateTriplanarUvsJob`, `BakeVertexOcclusionJob.SampleDensityNearest`, `GeologyLodDecimationJob.Snap`, and `GeologyPackVertexJob.PackUnorm16`.
Rejected Alternatives: Relying only on `SdfToMeshExtractionJob.WriteVertex` and `CalculateBounds` rejected because each Burst kernel is a payload boundary. Throwing on non-finite vectors rejected because the editor bake can safely quarantine poisoned rows into zero UV/position or empty AO samples.
Scalability potential: Low/Middle/High/Ultra output quality is unchanged for valid inputs. Invalid rows collapse to finite conservative visual data instead of corrupting packed mesh streams at any quality tier.
Hardware Impact: Runtime cost remains 0 us. Editor cost is a small finite predicate in final attribute/LOD/AO jobs; it buys deterministic payload safety under malformed profile/noise edge cases.

## Decision 033 - Artifact Failure Hardening
Problem: `CreateUnityMesh` allocated a Unity `Mesh` before validation/upload finished, so an exception between allocation and return could retain a transient native mesh object. `FinishAsyncBake` could also overwrite the previous manifest/report with empty artifacts when a batch was canceled before the first successful variation.
Solution: `CreateUnityMesh` now uses explicit ownership transfer: the local `Mesh` is destroyed in `finally` unless success sets the local to null before returning. `FinishAsyncBake` now computes `shouldWriteArtifacts` from cancel state plus actual metrics/manifest counts and skips manifest/report writes for zero-output cancels.
Rejected Alternatives: Trusting Unity mesh upload/validation never to throw was rejected because upload is the payload boundary. Writing empty manifests on zero-output cancel was rejected because it destroys the last good static geology handoff without producing a replacement.
Scalability potential: Low/Middle/High/Ultra output quality is unchanged. Large high/ultra authoring batches gain deterministic cleanup and preserve prior bake artifacts when the operator aborts before producing a replacement.
Hardware Impact: Runtime cost remains 0 us. Editor cost is one null check in mesh cleanup and three O(1) artifact guards at batch finish; it avoids native object retention and bad asset churn during failed/canceled authoring passes.

## Decision 034 - CSV Seed Determinism
Problem: `GeologyProfileCsv.ReadInt` parsed every integer column through `ReadFloat`. Current 10-digit CSV seeds exceed exact 24-bit float integer precision, so deterministic sector/profile seeds could lose low bits before entering `ResolveAupSeed`.
Solution: Added direct byte-wise `ReadUInt` for profile seeds and rewrote `ReadInt` to parse signed decimal digits with saturation, without routing through float math.
Rejected Alternatives: Keeping float-backed integer parsing rejected because deterministic seeds are authority inputs, not visual approximations. Hex-only seeds rejected because the existing designer CSV already uses decimal seeds and must stay human-editable.
Scalability potential: Low/Middle/High/Ultra geometry quality is unchanged. Every tier now consumes the exact same deterministic CSV seed bits, so high/ultra variation density does not amplify seed drift from editor parsing.
Hardware Impact: Runtime cost remains 0 us. Editor parse cost stays O(bytes) and replaces float conversion/rounding with integer multiply-add; the important gain is deterministic payload identity, not frame time.

## Decision 035 - Atomic Evidence Payload Writes
Problem: `.h8geom`, black-box dump, bake report, layout audit, and scanner report writes used direct overwrite paths. A mid-write IO exception could erase the last valid artifact and leave only a truncated or empty file.
Solution: Binary payloads now write to `.tmp` files with `FileMode.CreateNew`, then replace the final path through `File.Replace` with `.bak` preservation when a previous artifact exists. JSON reports now use the same temp/replace policy.
Rejected Alternatives: Direct `FileMode.Create` and `File.WriteAllText` were rejected because they destroy the previous proof before the replacement proof exists. Keeping only zero-output cancel guards rejected because non-cancel IO faults can still happen after valid records exist.
Scalability potential: Low/Middle/High/Ultra generated geometry is unchanged. Artifact integrity now scales with large authoring batches: aborts and IO faults preserve the last known-good payload instead of forcing a full rebake to recover evidence.
Hardware Impact: Runtime cost remains 0 us. Editor finish cost adds one temp write and atomic replace per artifact; this is outside gameplay and prevents expensive human recovery from corrupted evidence files.

## Decision 036 - Tetra Winding And Layout Copy Guard
Problem: Complement tetra LUT cases reused the same edge order as their inverse cases, risking inverted triangle winding/backface holes. `GetLayout()` also returned the mutable static vertex descriptor array to assembly callers.
Solution: Complement cases now reverse triangle edge order, and `ValidateComplementWinding()` checks every 1..14 case pair before layout validation. `GetLayout()` returns a fresh four-descriptor copy rather than `_GeologyLayout`.
Rejected Alternatives: Trusting visual inspection of the LUT rejected because extraction count and winding must be machine-checkable. Returning the static descriptor array rejected because any same-assembly caller could mutate the upload contract.
Scalability potential: Low/Middle/High/Ultra topology budgets are unchanged. Correct winding prevents high/ultra dense bakes from amplifying backface artifacts, while the copied layout preserves the 32B stream contract for every tier.
Hardware Impact: Runtime cost remains 0 us. Editor validation adds a tiny 14-case loop and one four-element descriptor copy per mesh upload; both are negligible compared with SDF extraction and AO.

## Decision 037 - CSV Schema Fail-Closed Guard
Problem: The CSV parser detected `iso_level` but otherwise trusted positional columns. A reordered or missing header could silently map designer values into the wrong unmanaged profile fields.
Solution: Added byte-level header validation for the exact supported schema, with and without `iso_level`, before row parsing begins. Mismatches throw `InvalidDataException` instead of producing corrupt bake recipes.
Rejected Alternatives: Full arbitrary header-index remapping was deferred because it is a wider parser rewrite and needs Unity import/compiler proof. Continuing positional parsing without validation rejected because silent field corruption is worse than a cold editor import failure.
Scalability potential: Low/Middle/High/Ultra output quality is unchanged for valid CSVs. Invalid CSVs now stop before baking, so high/ultra batches cannot amplify a single header edit into hundreds of corrupt meshes.
Hardware Impact: Runtime cost remains 0 us. Editor parse cost adds a one-line header token pass over about 20 columns; the gain is deterministic authoring safety, not frame time.

## Decision 038 - Bounded Asset Editing Scope
Problem: `BakeProfilesAsync` opened `AssetDatabase.StartAssetEditing()` before subscribing the update runner, so the editing scope could span multiple editor updates and remain active until finish/cancel.
Solution: Removed the batch-wide edit scope. The async tick now opens `StartAssetEditing()` only around one variation's saved mesh tranche, closes it immediately after `BakeSingle`, and closes it on the local exception path before black-box dump/report handling continues.
Rejected Alternatives: Keeping the full-batch edit scope rejected because assembly reload/cancel faults should not inherit a multi-frame asset database lock. A full stage scheduler was deferred because it is a larger rewrite and still needs Unity import proof.
Scalability potential: Low/Middle/High/Ultra generated quality is unchanged. Large high/ultra batches now reduce editor lock duration from whole-batch to per-variation save windows.
Hardware Impact: Runtime cost remains 0 us. Editor overhead may increase slightly through more asset-edit transitions, but the lock scope is bounded and safer under operator cancel or domain reload.

## Decision 039 - Runtime Scanner Time-Slice
Problem: `RuntimeMeshGenerationScanner.ScanAndWriteReport()` scanned every target source file synchronously from the menu/window path. The report is editor-only, but a large World/Environment scan can still lock the Unity editor during an authoring session.
Solution: Non-batch scans now start an `EditorApplication.update` state machine with a 4 ms per-update budget, a cancelable progress bar, duplicate-start guard, and explicit update-hook cleanup on completion, cancel, or fault. Batch mode keeps the synchronous path so CI/report scripts still emit one deterministic report.
Rejected Alternatives: Moving the scanner to runtime rejected because it is an editor proof tool, not gameplay logic. Threaded file IO rejected because Unity editor progress/report calls and shared static state would need additional synchronization without measurable value for a static source scanner.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring scales by distributing source scanning across editor ticks instead of freezing the tool while high-volume geology batches and static audits are being prepared.
Hardware Impact: Runtime cost remains 0 us. Editor scan work is bounded to roughly 4 ms per update after initial file enumeration; exact wall-clock impact requires Unity editor import/execution proof.

## Decision 040 - Scanner Discovery Slice
Problem: The non-batch scanner time-sliced file scanning, but `StartAsyncScan()` still called `CollectScanFiles()`, which recursively enumerated every target file before the first editor-update budget began.
Solution: Non-batch scanning now seeds only root directories and direct files, then `TickAsyncScan()` alternates bounded source-file scans with one-directory expansions through `ExpandNextAsyncDirectory()`. Directory expansion uses `SearchOption.TopDirectoryOnly`, and the progress bar uses a static message literal instead of per-tick string concatenation.
Rejected Alternatives: Keeping recursive upfront discovery rejected because it preserves the editor-freeze defect behind a time-sliced scan facade. Moving the CI path to the incremental state machine rejected because batch/static scripts need one deterministic call that writes the report before returning.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring scalability improves for large source trees by distributing both discovery and scanning across editor ticks.
Hardware Impact: Runtime cost remains 0 us. Editor work is still file-system bound, but the non-batch path no longer performs full recursive discovery before yielding; exact wall-clock/editor-responsiveness proof requires Unity execution.

## Decision 041 - Async Bake Static Progress Text
Problem: `TickAsyncBake()` formatted the cancelable progress message every editor update through profile-name `ToString()` and interpolation. This is editor-only, but it preserved managed churn inside the active update hook during long multi-profile geology bakes.
Solution: Replaced the per-update formatted title/message with static `AsyncBakeProgressTitle` and `AsyncBakeProgressMessage` constants while keeping the progress scalar and cancel button path intact.
Rejected Alternatives: Keeping dynamic profile/variation text rejected because the UI Toolkit progress bar already exposes progress and the Console/report artifacts carry exact profile facts. Adding a pooled string builder rejected because Unity's progress API still consumes a managed string and the useful fix is to avoid rebuilding it per tick.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring responsiveness improves during large high/ultra bake batches by removing avoidable managed formatting from the update loop.
Hardware Impact: Runtime cost remains 0 us. Editor gain is small per tick but deterministic: no profile-name string conversion or interpolated message allocation occurs from the cancelable progress path. Unity Profiler allocation proof remains pending.

## Decision 042 - Async Variation Count Saturation
Problem: `_asyncTotalBakes` was computed from raw profile `Variations`, while the actual async execution sanitized each profile later. A malformed CSV value could overflow the total counter or make progress math diverge from the executed 1..500 variation clamp.
Solution: Added `SanitizeVariationCount()` and routed both `SanitizeProfile()` and `CountTotalBakes()` through it. `CountTotalBakes()` now saturates on integer overflow instead of wrapping.
Rejected Alternatives: Trusting CSV/UI validation rejected because imported profile data is an external authoring payload. Keeping `math.max(1, rawVariations)` rejected because it does not apply the upper bound used by execution and can overflow aggregate progress totals.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring batches now have the same bounded variation count for progress math and execution across every generated quality tier.
Hardware Impact: Runtime cost remains 0 us. Editor cost is one clamp per profile during async setup; it prevents corrupt progress denominators and runaway bake counts from malformed CSV input.

## Decision 043 - Preview Fake-Async Fence Removal
Problem: `GeologyForgePreview.Build()` scheduled the lightweight preview SDF job and immediately called `.Complete()` from the button path. It also lacked an explicit `Unity.Jobs` import for the job extension API in the file that owns the preview call.
Solution: Added the explicit `Unity.Jobs` import and changed the preview-only `GenerateMockFractalNoiseJob` invocation to `Run(count)`. The preview remains a bounded cold editor action over 24^3 samples and no longer pretends to be asynchronous.
Rejected Alternatives: Keeping `Schedule(...).Complete()` rejected because it is a fake async fence and violates the project's job-route readability standard. A full persistent async preview state machine was rejected here because it would require a long-lived private `NativeArray<float>` in the editor facade, which is worse for H-Phi evidence than a bounded cold `Run`.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Preview quality still consumes `GlobalQualityWeight` through `GenerateMockFractalNoiseJob`; low weights collapse expensive noise terms while high weights show richer SDF detail before the full bake.
Hardware Impact: Runtime cost remains 0 us. Editor preview avoids scheduler overhead plus immediate main-thread fence around a 13,824-sample cold preview job; exact editor timing remains pending Unity execution.

## Decision 044 - Shared Variation Ceiling Facade
Problem: The generator clamped variation counts to 500, but the UI field displayed and forwarded raw values with only a lower bound. Malformed CSV or manual entry could show one count while the async runner executed another.
Solution: Added `GeologyForgeConstants.MaximumVariations` and routed both the generator clamp and the UI facade through that shared ceiling. The dropdown display and field resolution now clamp to the same 1..500 range used by async progress math and execution.
Rejected Alternatives: Leaving the UI permissive rejected because human-readable tuning bridges should show the facts the bake will execute. Duplicating the literal `500` in more places rejected because it creates drift between the designer facade and payload generator.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring batches now expose the same variation cap across every quality tier, avoiding accidental high/ultra batch explosions from bad CSV values.
Hardware Impact: Runtime cost remains 0 us. Editor cost is one clamp on field display/resolve; it prevents accidental 500+ variation authoring runs from weak development machines.

## Decision 045 - Async Result Preallocation From Sanitized Total
Problem: `BakeProfilesAsync` still allocated `_asyncMetrics` and `_asyncManifestRecords` with `profiles.Count * 4`, even after execution was clamped to as many as 500 variations per profile. A 500 to 5000 variation authoring batch could therefore grow managed `List<T>` backing arrays mid-bake inside the active editor-update runner.
Solution: Moved `_asyncTotalBakes = CountTotalBakes(_asyncProfiles)` ahead of result-list allocation, added `GeologyForgeConstants.MaximumAsyncResultPreallocation = 5000`, and introduced `ResolveAsyncResultCapacity()` so normal SHINOBU assignment-scale batches preallocate from the sanitized total while pathological totals are capped before they can request impossible memory.
Rejected Alternatives: Keeping `profiles.Count * 4` rejected because it silently underestimates the mandated 500/5000-variation forge path. Preallocating `int.MaxValue` after saturated total math rejected because malformed input must fail bounded, not OOM the editor.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring scales from small preview batches to the 5000-rock library target with stable result storage; larger malformed batches still execute through the existing 1..500 per-profile clamp without front-loading unbounded memory.
Hardware Impact: Runtime cost remains 0 us. Editor gain is removal of avoidable `List<T>` backing-array growth and copy churn during assignment-scale async bakes; exact allocation proof remains pending Unity Profiler.

## Decision 046 - Reused Bake Requests And Even Preview Sampling
Problem: `BakeSelected` and `BakeAll` created fresh `List<GeologyBakeProfile>` objects on every button click, and the SceneView preview filled its 2048-point budget with the first near-surface grid hits. That preserved avoidable editor facade allocations and biased the Dear Lie preview toward one scan-order region of the SDF.
Solution: Added one reusable `_bakeRequestProfiles` list owned by the window facade and reused it for selected/all bake requests. `BakeProfilesAsync` copies the incoming list synchronously, so reuse after dispatch does not alias the active runner. The preview now performs a bounded two-pass scan: count all near-surface candidates, then sample candidates with a deterministic stride into the fixed point buffer.
Rejected Alternatives: Keeping per-click lists rejected because the button path is the operator-facing high-volume forge route. Full mesh preview rejected because Task 18 explicitly requires a cheap SDF point-cloud fake. Random candidate sampling rejected because deterministic editor previews should not depend on hidden RNG state.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Low authoring previews still cap at 2048 points and 24^3 SDF samples; middle/high/ultra profiles get a more representative point cloud before full bake without escalating to mesh extraction, AO, or upload.
Hardware Impact: Runtime cost remains 0 us. Editor bake buttons remove two transient list allocations per request path after initial window construction. Preview adds one extra 13,824-sample pass, still bounded and cheaper than full mesh/AO generation; exact editor timing and allocation proof remain pending Unity Profiler.

## Decision 047 - Caller-Owned CSV Profile Lists
Problem: `GeologyProfileCsv.LoadProfiles()` returned a new `List<GeologyBakeProfile>`, and the UI reload path immediately copied that list into the window-owned `_profiles` list. The menu bake path also created a short-lived profile list before the async runner copied it again.
Solution: Added a caller-owned `LoadProfiles(List<GeologyBakeProfile>)` overload that clears and fills an existing list while preserving the default-profile fallback. `GeologyForgeWindow.ReloadProfiles()` now loads directly into `_profiles`, and `BakeCsvProfilesMenu()` reuses a static `_menuProfiles` list before `BakeProfilesAsync` performs its existing synchronous copy into runner-owned state.
Rejected Alternatives: Keeping the return-only loader rejected because it forced an avoidable list container and backing array on every reload/menu bake. Returning an enumerable or iterator rejected because it introduces managed iterator state. Moving profiles into a persistent NativeArray rejected because this is cold editor authoring data and the async runner already owns the copied execution snapshot.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring scales better for repeated CSV reloads and high-volume library bake launches because the facade reuses containers while the continuous `GlobalQualityWeight` profile values remain untouched.
Hardware Impact: Runtime cost remains 0 us. Editor path removes one transient `List<GeologyBakeProfile>` allocation plus profile-copy loop from UI reload and removes one transient list allocation from the menu bake path after the static list is initialized; exact allocation proof remains pending Unity Profiler.

## Decision 048 - CSV Variation Ceiling Constant
Problem: The generator and UI facade shared `GeologyForgeConstants.MaximumVariations`, but CSV parsing still clamped `variations` with a literal `500`. That created a drift risk between imported profile truth, UI display, async progress totals, and actual execution.
Solution: Replaced the CSV literal with `GeologyForgeConstants.MaximumVariations`, making CSV ingestion, UI field resolution, async total counting, and generator sanitization consume the same ceiling.
Rejected Alternatives: Leaving the literal rejected because it silently reintroduces split-authority tuning. Adding a separate CSV-specific ceiling rejected because profile import is not a separate gameplay truth owner.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring tiers now clamp imported variation counts through the same continuous-quality bake lane and the same high-volume batch ceiling.
Hardware Impact: Runtime cost remains 0 us. Editor cost is unchanged; this removes a maintenance drift risk rather than a measurable hot-path cost.

## Decision 049 - Remove Return-Allocated CSV Loader
Problem: After adding the caller-owned CSV loader, the old internal `LoadProfiles()` method still existed and allocated a fresh `List<GeologyBakeProfile>` before delegating. No owned source called it, but leaving it in the editor assembly preserved an allocation-shaped escape hatch for future menu/window code.
Solution: Removed the return-value loader and made `LoadProfiles(List<GeologyBakeProfile>)` the only CSV ingestion API inside `GeologyProfileCsv`.
Rejected Alternatives: Keeping the wrapper for convenience rejected because the whole facade direction is caller-owned storage. Marking it obsolete rejected because this is an internal editor-only class and no compatibility boundary requires the method.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring remains deterministic and caller-owned; future reload/menu paths cannot accidentally revive the transient-list copy route.
Hardware Impact: Runtime cost remains 0 us. Editor cost removes no active measured path after the previous patch, but it deletes the stale API that could reintroduce one `List<T>` allocation plus copy loop per reload or bake launch.

## Decision 050 - Validator-Owned Vertex Layout Application
Problem: `GeologyVertexLayoutValidator.GetLayout()` returned a fresh four-element `VertexAttributeDescriptor[]` copy for every mesh upload. The copy protected the static layout from external mutation, but it still allocated a managed array inside the active async bake path for every LOD mesh.
Solution: Replaced `GetLayout()` with `ApplyVertexBufferParams(Mesh,int)`, keeping `_GeologyLayout` private and letting the validator apply the static descriptor array directly to the mesh.
Rejected Alternatives: Returning the static array rejected because same-assembly callers could mutate the vertex layout contract. Keeping per-upload copies rejected because the async bake loop creates LOD0/1/2 meshes per variation and should not allocate descriptor arrays on that path.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring batches at high/ultra variation counts avoid one managed descriptor-array allocation per generated LOD mesh while preserving the 32-byte stream contract.
Hardware Impact: Runtime cost remains 0 us. Editor path removes a small but deterministic managed allocation from each mesh upload call; exact Unity Profiler allocation proof remains pending.

## Decision 051 - Deferred Preview Hook Subscription
Problem: `GeologyForgePreview.Build()` subscribed to `SceneView.duringSceneGui` before allocating preview density, running the preview SDF kernel, and filling the point buffer. A fault before point population could leave the SceneView callback registered until window disable.
Solution: Moved `EnsureSubscribed()` to the successful end of point generation, after `_pointCount` has been written from a populated fixed buffer.
Rejected Alternatives: Keeping early subscription rejected because callback lifetime should follow valid preview state. Adding a broad catch-only unsubscribe rejected because the simpler ownership rule is to subscribe only after success.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Editor preview still uses the same bounded 24^3 SDF point-cloud fake and continuous `GlobalQualityWeight`; callback lifetime now matches valid preview data.
Hardware Impact: Runtime cost remains 0 us. Editor cost is unchanged for successful previews; fault paths no longer retain a dead SceneView callback after failed preview generation.

## Decision 052 - Reusable Runtime Scanner Async Buffers
Problem: `RuntimeMeshGenerationScanner` still allocated fresh `List<string>` and `List<Finding>` containers every time a non-batch scan started, then used null state as the scan-active sentinel. That preserved allocation churn inside an editor proof tool that can be invoked repeatedly during geology authoring.
Solution: Converted the non-batch scanner queues/findings to static readonly reusable lists, added `_asyncScanActive` as the explicit lifecycle sentinel, and centralized cancel/finish/start reset through `ClearAsyncScanState()`. `FinishAsyncScan()` now writes the report before clearing buffers and clears state in `finally`.
Rejected Alternatives: Keeping nullable lists rejected because ownership/lifecycle was encoded through allocation state. Moving scanner results into a persistent native container rejected because this is cold Editor source inspection, not runtime data truth or rollback state.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring responsiveness improves across repeated scan runs while the scanner continues to time-slice directory discovery and file scanning under the 4 ms editor-update budget.
Hardware Impact: Runtime cost remains 0 us. Editor path removes three list-container allocations per non-batch scanner launch after static initialization; exact Unity Profiler allocation proof remains pending.

## Decision 053 - CSV Row And Numeric Cell Fail-Closed Parsing
Problem: `GeologyProfileCsv` validated the header, but row cells still used fallback-return numeric readers. A malformed value such as an empty seed, truncated float, or stray character could silently substitute a default and bake the wrong static payload.
Solution: Added row column-count validation and strict byte-level `ReadInt`, `ReadUInt`, and `ReadFloat` paths that throw `InvalidDataException` with row, column, and field context on malformed input. Positive-only physical fields now fail closed instead of reverting to fallback defaults.
Rejected Alternatives: Keeping fallback defaults rejected because it hides authoring corruption. Full arbitrary header remapping was still rejected because SHINOBU_208 owns one exact CSV schema with optional `iso_level`, and widening the parser would create more route ambiguity.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged for valid CSVs. Invalid high/ultra bake recipes now stop before they can generate hundreds of corrupt mesh records or misleading BRG manifest entries.
Hardware Impact: Runtime cost remains 0 us. Editor parse cost adds one row comma-count scan and strict terminator checks per numeric cell; this is negligible compared with SDF extraction and prevents expensive rebakes from poisoned authoring data.

## Decision 054 - CSV Integer Overflow Fail-Closed Parsing
Problem: Strict CSV integer parsing still saturated oversized signed and unsigned cells to `int.MaxValue`, `int.MinValue`, or `uint.MaxValue`. That preserved a corruption path where an authored overflow could silently become a valid seed, LOD budget, resolution, or variation count.
Solution: `ReadInt` and `ReadUInt` now track overflow during byte-digit accumulation and throw the same row/column/field `InvalidDataException` used by malformed cells. The exact `-2147483648` signed minimum remains valid; values outside the target integer domain are rejected.
Rejected Alternatives: Keeping saturation rejected because fail-closed CSV validation must not turn invalid authoring data into plausible bake truth. Post-clamp detection at profile hydration rejected because it would lose the field-level parser boundary and blur malformed input with legitimate clamp rules.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged for valid CSVs. Invalid high/ultra authoring rows stop before they can trigger runaway bake counts or produce wrong deterministic seeds.
Hardware Impact: Runtime cost remains 0 us. Editor parse cost adds one boolean overflow check per integer digit; this is negligible compared with SDF extraction and prevents expensive rebakes from poisoned integer cells.

## Decision 055 - CSV Numeric Error Codes
Problem: The CSV bridge reported row, column, and field names, but did not include numeric error codes. `TOOL_Designer_Facades_CSV_Binary_Bridge` requires import errors to carry row, column, field id, and numeric error code so CI/editor tools can classify failures without parsing prose.
Solution: Added stable CSV error codes for malformed cells, integer overflow, non-finite floats, non-positive physical values, invalid terminators, row column-count mismatch, and header schema mismatch. Cell errors now include `Geology profile CSV error <code>` while preserving row/column/field context.
Rejected Alternatives: Leaving prose-only exceptions rejected because automated import gates need stable numeric classification. Creating managed exception subclasses rejected because this editor-only parser already throws `InvalidDataException`, and subclass proliferation adds no useful payload without Unity execution proof.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring scales better because large CSV bake batches can be rejected and grouped by deterministic error code before any SDF/AO mesh work starts.
Hardware Impact: Runtime cost remains 0 us. Editor cost is one integer constant embedded in exceptional strings only on failure paths; successful parsing is unaffected except for constant definitions.

## Decision 056 - CSV Header Schema Diagnostics
Problem: Header validation still returned a boolean and collapsed reordered, missing, or extra columns into one generic header mismatch. That weakened the row/column/field evidence required by the designer bridge mandate.
Solution: Replaced `HeaderMatchesExpectedSchema` with `ValidateHeaderSchema`, which throws at the exact row-1 column that mismatches expected schema tokens and throws a header column-count diagnostic when column totals differ.
Rejected Alternatives: Keeping the boolean and adding a generic error code rejected because it still forces operators to inspect the CSV manually. Building a dynamic header map rejected because SHINOBU_208 owns one exact schema with optional `iso_level`, and arbitrary reordering would expand the route surface.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring failures in large profile batches now stop with precise schema facts before expensive high/ultra mesh generation starts.
Hardware Impact: Runtime cost remains 0 us. Editor successful-parse cost is unchanged in class; failure paths produce a more precise exception string only when the header is corrupt.

## Decision 057 - CSV Existing File Size Fail-Closed
Problem: Missing CSV files intentionally fall back to a default mock profile, but existing empty or oversized CSV files also fell back to `DefaultProfile()`. That hid corrupt authoring payloads and could bake fallback rocks when a designer expected authored profiles.
Solution: Added `CsvErrorFileSize=1008` and made existing zero-byte or larger-than-`int.MaxValue` CSV files throw `InvalidDataException`. Missing files still use the mock default profile route for CI/editor bootstrap.
Rejected Alternatives: Removing the missing-file fallback rejected because SHINOBU_208 still needs an emergency mock authoring route when the CSV is absent. Keeping fallback for empty files rejected because an existing empty file is explicit corrupt source data, not an absent optional bridge.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring failure now stops before high/ultra batch generation can produce a library from unintended fallback values.
Hardware Impact: Runtime cost remains 0 us. Editor cost is one file-length branch before native scratch allocation; failed imports avoid all downstream SDF, LOD, AO, mesh upload, and manifest work.

## Decision 058 - Profile Scalar Finite Vaccination
Problem: CSV parsing rejects non-finite numeric cells, but `BakeProfilesAsync` and direct `BakeSingle` can still receive profiles from UI/editor code. `SanitizeProfile` clamped ranges without first replacing NaN/Infinity, so poisoned radius, quality, iso, AUP, or weight fields could propagate into SDF, AUP hash, AO, or LOD math.
Solution: Added `FiniteOr` helpers and routed scalar shape/noise/quality fields, `IsoLevel`, and every `SectorAup` lane through finite fallbacks before clamp/hash/job setup.
Rejected Alternatives: Trusting CSV validation rejected because profile DTOs can enter through non-CSV editor paths. Letting downstream Burst kernels absorb NaNs rejected because topology extraction and deterministic AUP seeding must be vaccinated before job scheduling.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged for valid profiles. Invalid editor profiles collapse to safe finite authoring defaults instead of consuming high/ultra bake time on poisoned math.
Hardware Impact: Runtime cost remains 0 us. Editor cost is a fixed set of `math.isfinite` checks per profile before bake; this prevents non-finite profile data from wasting SDF/AO/mesh serialization work or poisoning black-box telemetry.

## Decision 059 - CSV Stable Read And AUP Lane Canonicalization
Problem: CSV import used `FileShare.ReadWrite` and parsed the byte count actually read without proving it matched the initial file length. Sector lanes also parsed through `ReadFloat`, storing float-derived values in `double3`, and `-0` could hash differently from `0`.
Solution: CSV import now uses `FileShare.Read`, rejects short/unstable reads with `CsvErrorFileSize=1008`, parses sector lanes through a strict `ReadDouble`, and canonicalizes zero before profile storage and AUP seed hashing.
Rejected Alternatives: Full manifest conversion to `int64x3` sector truth was rejected for this pass because it would break the existing 128-byte `.h8geom` ABI and require runtime importer ownership proof outside SHINOBU_208. Keeping float-derived sector lanes rejected because deterministic seed identity must not depend on float truncation.
Scalability potential: Runtime Low/Middle/High/Ultra behavior and payload layout remain unchanged. Authoring imports now fail before any quality-tier SDF/AO/LOD work if the CSV source mutates mid-read, while valid high/ultra sector values keep double precision in editor bake identity.
Hardware Impact: Runtime cost remains 0 us. Editor cost is one length equality check and double parser path for three sector cells; failed unstable imports avoid all downstream mesh extraction and asset serialization work.

## Decision 060 - Atomic Async Runner State Assignment
Problem: `BakeProfilesAsync` assigned `_asyncProfiles` before profile copy/list allocation and result-list setup were fully guarded. An allocation or setup fault could leave `_asyncProfiles` non-null without an update runner, blocking later bakes.
Solution: Profile snapshot, total-count calculation, metric-list allocation, and manifest-list allocation now happen in locals first. Static runner fields are assigned only inside the guarded start block; any fault after assignment routes through `FinishAsyncBake(true)`.
Rejected Alternatives: Keeping partially assigned static state rejected because lifecycle ownership was split across allocation progress. Adding a second boolean state flag rejected because the existing `_asyncProfiles != null` active sentinel is sufficient once assignment is atomic.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Large editor batches either start with a coherent state snapshot or fail without latching the authoring tool.
Hardware Impact: Runtime cost remains 0 us. Editor success cost is unchanged; fault paths avoid a manual domain reload or editor restart after an interrupted batch setup.

## Decision 061 - Transient LOD Mesh Ownership Cleanup
Problem: LOD construction used an object initializer, so a failure after LOD0 creation could orphan earlier transient meshes. Save-path failures could also leave unsaved transient meshes because normal cleanup only ran for non-asset probes.
Solution: LOD meshes are now built sequentially into a local `MeshLodSet` and destroyed on construction failure. Asset save now tracks per-LOD ownership transfer; failed save paths destroy only meshes that have not been handed to `AssetDatabase`.
Rejected Alternatives: Relying on Unity editor cleanup rejected because native mesh objects can survive failed upload/save paths. Destroying all LOD references on failure rejected because some references may already be persistent assets loaded from `AssetDatabase`.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. High/ultra authoring batches no longer leak native mesh objects when one LOD or asset path fails.
Hardware Impact: Runtime cost remains 0 us. Editor failure paths reclaim up to three transient mesh native objects per failed variation; success path adds only three boolean ownership flags.

## Decision 062 - Manifest AUP Audit Guard
Problem: The self-audit manifest validator checked bounds and mesh GUIDs but allowed non-finite `SectorAup` lanes in `.h8geom` records.
Solution: `ValidateManifestRecord` now rejects records where `math.isfinite(record.SectorAup)` is not true for all three lanes.
Rejected Alternatives: Trusting generator-side finite vaccination rejected because layout audit must catch stale or externally corrupted payloads. Rewriting manifest schema to integer sectors rejected for this pass because the ABI/importer route is not owned here.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Corrupt static payloads fail the editor audit before any runtime BRG consumer can classify them as immutable truth.
Hardware Impact: Runtime cost remains 0 us. Audit cost is one `double3` finite check per manifest record; failed payloads avoid runtime importer investigation work.

## Decision 063 - Manifest Audit Stable Read
Problem: The manifest layout self-audit still opened `.h8geom` with `FileShare.ReadWrite`, so an external writer could theoretically mutate the payload while the audit was validating it.
Solution: The self-audit now opens the manifest with `FileShare.Read` and rejects a length change after record validation with `UNSTABLE_FILE_LENGTH`.
Rejected Alternatives: Relying only on generator-side `.tmp` replacement rejected because the audit is the trust boundary for stale or externally modified payloads. Loading the full manifest into a managed byte array rejected because the current stack-span exact reader is sufficient and smaller.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. High-volume authoring audits now validate a stable static payload view before any runtime consumer can accept it as BRG-ready.
Hardware Impact: Runtime cost remains 0 us. Audit cost is one final file-length check; failed unstable reads avoid false-positive manifest proof.

## Decision 064 - Asset Editing Scope Truth Patch
Problem: The documentation claimed asset editing was scoped to the save tranche, but `TickAsyncBake` still opened `AssetDatabase.StartAssetEditing()` before `BakeSingle`, so SDF generation, extraction, AO, LOD, and mesh packing ran while the AssetDatabase edit scope was open.
Solution: Removed AssetDatabase edit-scope ownership from `TickAsyncBake`. `SaveMeshesAndManifest` now opens `StartAssetEditing()` immediately before the three `SaveMeshAsset` calls, closes it in `finally`, then reads GUIDs and appends the manifest record after the edit scope is closed.
Rejected Alternatives: Keeping the tick-level scope rejected because it made static proof false and widened the failure window across CPU jobs. Wrapping only each individual LOD save rejected because three imports belong to one authoring transaction and batching them reduces editor import churn.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring on low-end CPUs avoids holding the asset database lock across heavy SDF/AO work; high/ultra batches still amortize three LOD imports per variation.
Hardware Impact: Runtime cost remains 0 us. Editor save path adds one local `finally`; failure containment improves by reducing edit-scope wall time from full variation bake to only three asset writes.

## Decision 065 - CSV Row Terminator Ownership
Problem: `TryReadProfile` called `SkipLine` after parsing `sector_z`, but `ReadDouble` already consumes the column terminator or row terminator. The extra skip advanced over the next authored profile row.
Solution: Removed the post-`sector_z` `SkipLine`. Each cell parser owns exactly one terminator, and the caller loop advances by the cursor state returned from `ReadDouble`.
Rejected Alternatives: Re-parsing rows through managed `String.Split` rejected due to allocation and weaker malformed-cell diagnostics. Leaving the skip and compensating `rowIndex` rejected because it still discards authoring data.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring batches now bake all intended low/mid/high/ultra geology profiles instead of silently dropping every next row.
Hardware Impact: Runtime cost remains 0 us. Editor import removes one redundant line scan per profile and prevents a full missing-profile bake/report mismatch.

## Decision 066 - Existing CSV Empty File Fail-Closed
Problem: A missing CSV legitimately needs fallback mock data, but an existing header-only or blank data file is corrupt authoring truth and previously fell back to `DefaultProfile`.
Solution: Kept fallback only in the `!File.Exists` branch. Existing files with zero parsed rows now throw `CsvErrorNoProfiles=1009`.
Rejected Alternatives: Silent default fallback rejected because it hides broken source data in CI and can bake the wrong static payload. Auto-creating a profile row rejected because designers own authoring truth.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. CI now fails before wasting SDF/AO/asset bake time on unintended fallback geometry.
Hardware Impact: Runtime cost remains 0 us. Editor failure path saves a full bake batch when the profile file is empty.

## Decision 067 - Empty Manifest Write Guard
Problem: Empty-surface bakes produce metrics but no manifest records; writing `.h8geom` in that state can replace a prior valid manifest with a zero-record file.
Solution: Public single-bake and async finish paths now write `.h8geom` and call `AssetDatabase.SaveAssets()` only when manifest records exist. Metrics-only reports still write when metrics exist.
Rejected Alternatives: Writing zero-record manifests rejected because it erases the BRG handoff artifact. Suppressing metrics rejected because empty-surface evidence is needed for authoring diagnostics.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Bad low-quality or malformed profiles now preserve last valid geometry while still reporting the failure.
Hardware Impact: Runtime cost remains 0 us. Editor empty-surface path skips one manifest file write and one `SaveAssets` call.

## Decision 068 - Black-Box Dump Exception Isolation
Problem: `DumpBlackBox` was called inside failure paths directly; dump IO failure could replace the original bake exception and destroy root-cause evidence.
Solution: Added `TryDumpBlackBox`, which logs dump exceptions through `Debug.LogException` and never masks the bake failure. Non-finite warning dump paths also use the wrapper.
Rejected Alternatives: Swallowing dump exceptions silently rejected because dump IO failure is still diagnostic evidence. Letting dump exceptions propagate rejected because the original bake failure owns the causal route.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Endurance-style editor failures now preserve the first exception even if the dump path is locked or unwritable.
Hardware Impact: Runtime cost remains 0 us. Success path has no additional cost; warning/failure path adds one try/catch boundary.

## Decision 069 - BRG Manifest Positive Geometry Audit
Problem: Manifest audit rejected negative triangle counts but accepted zero-triangle BRG-ready records and finite-but-zero bounds extents.
Solution: `ValidateManifestRecord` now requires all LOD triangle counts to be positive and all bounds extents to be finite and greater than zero.
Rejected Alternatives: Accepting zero as a legal empty mesh rejected because BRG-ready records must represent draw-capable geometry. Deferring to runtime consumers rejected because this editor audit is the payload trust boundary.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Invalid static artifacts fail before any runtime render owner imports them.
Hardware Impact: Runtime cost remains 0 us. Audit cost adds three integer comparisons and one `float3` positive check per record.

## Decision 070 - Ledger Source Truth Correction
Problem: `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` still described the old async-tick asset-edit scope and zero-output manifest behavior after source patches moved asset editing into `SaveMeshesAndManifest` and gated manifest writes on record count.
Solution: Updated the SHINOBU_208 ledger row to state the current source truth: asset editing wraps only LOD asset writes, `.h8geom`/`SaveAssets` require manifest records, metrics-only reports remain allowed, `CsvErrorNoProfiles=1009` owns existing-empty CSV failure, manifest self-audit rejects zero geometry, and layout upload uses `ApplyVertexBufferParams` rather than the removed `GetLayout` copy accessor.
Rejected Alternatives: Leaving stale prose rejected because architecture docs are used as objective memory after context compression. Adding a second contradictory addendum rejected because the primary SHINOBU_208 row must be readable without conflict.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. The correction prevents future agents from widening editor asset-lock windows or reintroducing zero-record manifest writes based on stale documentation.
Hardware Impact: Runtime cost remains 0 us. Documentation-only patch; indirect editor risk reduction comes from preserving the smaller asset-edit lock and fail-closed manifest behavior.

## Decision 071 - Async Finish Exception Isolation
Problem: `BakeProfilesAsync` setup catch and `TickAsyncBake` update catch called `FinishAsyncBake(true)` directly. If cleanup, report IO, or a progress callback threw during that finish call, the finish exception could replace the original setup/bake failure.
Solution: Added `TryFinishAsyncBake(bool)` for exception paths only. It logs finish failures via `Debug.LogException` and lets the original exception continue through the existing throw/log route. Normal successful finish and explicit cancel still call `FinishAsyncBake` directly so their own failures remain visible.
Rejected Alternatives: Swallowing all finish failures rejected because report/cleanup failure is diagnostic evidence. Wrapping normal finish rejected because a direct user cancel or final artifact write failure should still surface as the active operation failure.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Large high/ultra editor batches now preserve the first causal exception even when secondary cleanup/report work fails under IO pressure.
Hardware Impact: Runtime cost remains 0 us. Editor failure paths add one try/catch wrapper; success paths are unchanged.

## Decision 072 - Mill Static Audit Hardening
Problem: The layout audit could pass with no output, manifest GUID pairs were not proven against live mesh assets, partial newly created LOD assets could remain after a save-tranche failure, CSV size validation allowed huge files up to `int.MaxValue`, UTF-8 BOM headers failed exact schema validation, and unsafe Burst suppression fields lacked local invariants.
Solution: Layout pass now requires `meshCount > 0`, `manifestValid`, and `manifestRecords > 0`; manifest records resolve all three GUID pairs back to `Mesh` assets and validate their 32B layout; `ResolveGuid128` throws on missing, non-32-char, or non-hex GUIDs; `SaveMeshesAndManifest` deletes newly created partial assets after the edit scope closes on failure and logs cleanup faults without replacing the original save exception; CSV import rejects files above `MaximumCsvBytes=4194304` and skips an optional UTF-8 BOM before header validation; all GeologyForgeJobs unsafe suppression fields now carry explicit disjoint-write/dependency invariants.
Rejected Alternatives: Keeping no-output audits as pass rejected because an empty checkout is not BRG-ready proof. Accepting nonzero GUID integers rejected because stale GUID payloads must fail before runtime import. Full transactional replacement of existing Unity assets was rejected for this static pass because AssetDatabase does not provide an atomic three-file transaction and generated asset references must not be rewritten without Unity execution proof; the implemented cleanup covers newly created partial files without claiming rollback of pre-existing asset mutation. Raising CSV size to `int.MaxValue` rejected because it can allocate pathological Temp memory before schema failure. Stripping BOM in managed strings rejected because the parser remains byte-pointer based.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Authoring now fails before high/ultra bakes spend SDF/AO/LOD time on oversized CSVs, stale manifest GUIDs, or empty output proof; continuous `GlobalQualityWeight` math remains untouched.
Hardware Impact: Runtime cost remains 0 us. Editor audit adds three GUID-to-mesh resolves per manifest record; CSV success path adds one BOM branch and a 4 MiB file-size cap before native allocation; failure paths avoid partial new assets and impossible audit passes.

## Decision 073 - Pauli Static Defect Integration
Problem: Subagent audit found the scanner still scoped the eradication verdict to World/Environment roots, asset save cleanup stopped before GUID/manifest append, self-audit accepted foreign/orphan output meshes, and raw padding names did not match the project padding convention.
Solution: `RuntimeMeshGenerationScanner` now scans `Assets/_Project/Scripts` excluding `Editor` folders and the JSON artifact was refreshed project-wide. `SaveMeshesAndManifest` backs up pre-existing LOD assets under `_H8Backups`, restores them on save/GUID/manifest-record failure, deletes newly created partial assets on the same outer failure path, and removes any appended manifest tail on failure. `GeologyForgeSelfAudit` now requires manifest GUIDs to resolve under the geology output folder, rejects duplicate GUIDs, rejects top-level output meshes not referenced by the manifest, and reports exact mesh-set proof. `GeologyRawVertex.Padding0` was renamed `_pad0`; manifest record padding is not named because `BoundsExtents` owns bytes 60..71 and GUID lanes already start aligned at byte 72.
Rejected Alternatives: Leaving the scanner root-scoped was rejected because Task 19 is project-runtime proof, not just Task 01 archaeology. Claiming full AssetDatabase transactionality was rejected; the implemented backup/restore route is explicit editor failure containment without pretending Unity provides atomic multi-asset replacement. Accepting any GUID-resolved Mesh was rejected because manifest truth must point to this lane's output folder, not a foreign mesh.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Project-wide report routing gives other owners exact remaining topology/material-clone sites while this lane keeps runtime geology cost at static mesh/manifest import only.
Hardware Impact: Runtime cost remains 0 us. Editor failure paths add backup copy/delete work only when overwriting existing baked assets; in return they avoid corrupting the static payload set after a failed high/ultra bake.

## Decision 074 - Manifest Orphan Count Correction
Problem: The layout self-audit already failed missing or invalid manifests, but top-level mesh assets were counted as unmanifested only after at least one manifest GUID had been collected. An empty or missing manifest could therefore fail while reporting `unmanifestedMeshCount=0`, weakening the forensic artifact.
Solution: `ValidateGeneratedMeshes` now checks every top-level mesh path directly against the manifest GUID set. If the set is empty, every top-level mesh is counted and reported as `UNMANIFESTED_MESH_ASSET`.
Rejected Alternatives: Relying only on `manifestValid=false` was rejected because the audit report must explain the orphan mesh population, not just fail generally. Deleting orphan meshes during audit was rejected because the self-audit is proof tooling and must not mutate the payload folder.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. The correction prevents invalid editor output from being accepted or under-reported before any runtime BRG owner imports static geology.
Hardware Impact: Runtime cost remains 0 us. Editor audit cost is one existing `HashSet<string>.Contains` lookup per top-level mesh, even in missing-manifest cases.

## Decision 075 - Kepler LUT And Manifest Layout Correction
Problem: Subagent static audit proved two hard self-audit blockers: complement tetra cases `14`, `13`, `11`, and `8` did not reverse their inverse-case edge order, and the manifest `_pad0` field at byte 68 overlapped `BoundsExtents.z` bytes 68..71.
Solution: Reversed the four complement edge sequences so `ValidateComplementWinding()` passes for every 1..14 pair. Removed `GeologyMeshManifestRecord._pad0` and its offset validation; `BoundsExtents` now explicitly owns bytes 60..71 and `Lod0GuidHigh` starts aligned at byte 72.
Rejected Alternatives: Keeping the pad name for audit aesthetics was rejected because explicit-layout overlap corrupts payload truth. Removing `ValidateComplementWinding()` was rejected because the LUT must fail closed before bake/audit work.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. The correction preserves the editor-only Dear Lie extraction route while preventing backface/inverted tetra output and corrupted manifest bounds from entering static payload proof.
Hardware Impact: Runtime cost remains 0 us. Editor validation cost is unchanged: the existing 14-case LUT check and manifest offset checks now validate correct data rather than rejecting or blessing corruption.
