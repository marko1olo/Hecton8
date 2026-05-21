# SHINOBU_208 Agent Log

## 2026-05-20 - OFFLINE_GEOLOGY_MESH_BAKER

What was wrong:
- Static geology had no dedicated Editor-only bake pipeline. Existing source still contains runtime mesh/topology generation patterns in world/environment/cave code.
- Runtime Marching Cubes cannot be honestly declared eradicated for the whole project: `Docs/Reports/GEOMETRY_OPTIMIZATION_REPORT.json` records 34 remaining findings. Cross-domain deletion was rejected.

What was done:
- Added Editor-only Geology Forge under `Assets/_Project/Scripts/Editor/GeologyForge`.
- Added SDF generation, deterministic extraction, smooth normal/tangent calculation, triplanar UV packing, vertex AO bake, LOD0/LOD1/LOD2 generation, 32-byte vertex layout validation, mesh/prefab asset serialization, CSV profile ingestion, SceneView preview, runtime mesh-generation scanner, bake report writer.
- Added `Assets/_Project/Data/Geology/geology_generation_profiles.csv`.
- Added architecture note `Docs/ARCHITECTURE/OFFLINE_GEOLOGY_MESH_BAKER.md`.
- Added reports `Docs/Reports/GEOLOGY_BAKE_REPORT.json` and `Docs/Reports/GEOMETRY_OPTIMIZATION_REPORT.json`.
- Updated Editor project include list in `Directory.Build.targets` for the new GeologyForge files and conditional Burst reference.

Cinematic cheats used:
- Vertex AO is baked into the red vertex color channel instead of runtime SSAO or per-frame cavity tracing.
- Static geology uses prebuilt LOD meshes instead of runtime Marching Cubes.
- Preview uses point sampling of SDF state instead of full mesh regeneration.

Exact microseconds saved:
- Measured runtime saving: 0 us in this pass. No Unity Play Mode/profiler run was executed.
- Authoring scan: 22.7 ms CLI static scan recorded in status.
- Estimated runtime removals pending consumer migration: static topology build, normal recalculation, AO sampling, and managed mesh upload are removed from runtime for GeologyForge-produced assets, but exact frame gain requires profiler proof.

Compile / verification:
- `dotnet build .\Hecton8.Editor.csproj --no-restore` was not launched because the legal preflight failed.
- First gated attempt: `CPU_AVERAGE=79`.
- Second gated wait samples: `100,100,100,97,38,58`.
- No `csc`/`dotnet` processes were visible before the gate, but CPU protocol blocked the build.

<SELF_AUDIT agent="SHINOBU_208" status="STATIC_IMPLEMENTATION_BUILD_BLOCKED">
  <vertex-layout stride-bytes="32" position="Float32x3" normal="Float32x3" color="UNorm8x4_AO_Red" uv0="UNorm16x2" />
  <lods generated="LOD0,LOD1,LOD2" runtime-marching-cubes="not-used-by-GeologyForge-assets" />
  <ao bake-location="Editor" storage="vertex Color.r" />
  <determinism seed="AUP double3 sector + profile seed FNV1a" />
  <zero-gc runtime="no runtime code added" editor-scratch="NativeArray UninitializedMemory where fully overwritten" />
  <reports bake="Docs/Reports/GEOLOGY_BAKE_REPORT.json" scanner="Docs/Reports/GEOMETRY_OPTIMIZATION_REPORT.json" />
  <limitations>
    <item>Unity import and compiler verification pending because CPU build gate stayed above 50 percent.</item>
    <item>Project-wide runtime mesh generation is not fully eradicated; scanner found 34 remaining cross-domain sites.</item>
    <item>No runtime microseconds are claimed as measured until profiler data exists.</item>
  </limitations>
</SELF_AUDIT>

## SHINOBU_208 - ASYNC FINISH EXCEPTION ISOLATION

What was wrong:
- Setup/update catch blocks called `FinishAsyncBake(true)` directly.
- If cleanup/report/progress callback code threw during that finish call, it could replace the original setup or bake exception.

What was done:
- Added `TryFinishAsyncBake(bool)`.
- Routed only exception-path finish calls through it.
- Left normal final finish and explicit cancel on direct `FinishAsyncBake` so their own failures remain visible.

Cinematic Cheats used:
- None added. This is editor exception-path evidence preservation.

Exact Microseconds saved:
- Runtime: 0 us.
- Editor success path: 0 us change.
- Editor failure path: one try/catch wrapper; preserves first causal exception under secondary IO/callback failure.

<SELF_AUDIT async_finish_exception_patch="static_source">
  <task-reconciliation>Task 10 and Task 20 failure containment strengthened. No runtime payload, DTO, or authority route changed.</task-reconciliation>
  <dependency-graph>No runtime `JobHandle` route changed.</dependency-graph>
  <compile-guard>No asmdef reference changed.</compile-guard>
</SELF_AUDIT>

## SHINOBU_208 - LEDGER SOURCE TRUTH CORRECTION

What was wrong:
- `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` still described asset editing as a current-variation save tranche rather than the actual `SaveMeshesAndManifest` LOD-write scope.
- The same row still implied zero-output cancel behavior instead of the current record-gated manifest write and metrics-only report behavior.

What was done:
- Corrected the SHINOBU_208 primary ledger row to match source.
- Corrected the offline geology architecture summary to avoid claiming one manifest is always written at finish/cancel.
- Corrected the vertex-layout ledger row to name `ApplyVertexBufferParams(Mesh,int)` instead of removed `GetLayout()`.

Cinematic Cheats used:
- None added. Documentation now preserves the existing Dear Lie: editor-time static geology mesh/AO bake instead of runtime topology simulation.

Exact Microseconds saved:
- Runtime: 0 us.
- Editor: documentation-only, but it protects the current source decision that keeps AssetDatabase editing out of SDF/extraction/AO/LOD job time.

<SELF_AUDIT ledger_truth_patch="static_docs">
  <compile-guard>No asmdef or source dependency changed.</compile-guard>
  <hphi>No Vault, runtime owner, or payload ABI changed.</hphi>
  <dear-lie>Offline bake boundary unchanged; runtime geology topology cost remains outside the simulation loop for this lane.</dear-lie>
</SELF_AUDIT>

## SHINOBU_208 - CURIE STATIC AUDIT PATCH SET

What was wrong:
- `TryReadProfile` consumed the `sector_z` row terminator and then called `SkipLine`, skipping the next authored profile row.
- Existing header-only CSV files fell back to `DefaultProfile`, hiding corrupt authoring data.
- Empty-surface bakes could write a zero-record `.h8geom` and overwrite a prior valid manifest.
- `DumpBlackBox` calls inside catch paths could mask the root exception if dump IO failed.
- Manifest audit accepted BRG-ready records with zero triangles or zero bounds extents.

What was done:
- Removed the redundant CSV `SkipLine` after `sector_z`.
- Added `CsvErrorNoProfiles=1009` for existing CSV files with no parsed rows.
- Gated manifest writes and `AssetDatabase.SaveAssets()` on positive manifest record count; metrics reports still write on metrics.
- Added `TryDumpBlackBox` and routed all telemetry dump call sites through it.
- Required positive LOD triangle counts and positive bounds extents in manifest audit.

Cinematic Cheats used:
- No new visual fake added in this patch. The existing Dear Lie remains static offline SDF/tetra extraction/AO baked into immutable mesh assets and `.h8geom` rather than runtime geology topology simulation.

Exact Microseconds saved:
- Runtime: 0 us; this lane remains Editor-only.
- Editor empty-surface path: skips one manifest write and one `AssetDatabase.SaveAssets()`.
- Editor CSV import: removes one redundant post-row scan per profile and prevents omitted-profile bake passes.

Static verification:
- `GeologyProfileCsv.cs BRACES=44/44`.
- `GeologyForgeGenerator.cs BRACES=140/140`.
- `GeologyForgeSelfAudit.cs BRACES=38/38`.
- Current CSV imports as 3 rows: Basalt_Pillar, Abyssal_Boulder, Trench_Wall_Chunk.
- `TRY_READ_PROFILE_HAS_SKIPLINE=False`.
- `CSV_NO_PROFILES_GATE=True`.
- `PUBLIC_MANIFEST_COUNT_GATE=True`.
- `ASYNC_MANIFEST_COUNT_GATE=True`.
- `MANIFEST_POSITIVE_TRI_GATE=True`.
- `MANIFEST_POSITIVE_EXTENTS_GATE=True`.
- `DIRECT_DUMP_CALL_SITES=none`.
- `SHOULD_WRITE_ARTIFACTS_PRESENT=False`.
- `WRITE_MANIFEST_EMPTY_GUARD=True`.
- `TRAILING_WS=none`.
- Targeted `git diff --check` reports LF-to-CRLF warnings only.
- Build preflight: `CPU_SAMPLES=100,100,100`, `CPU_AVERAGE=100`, `BUILD_PROCS=none`, `PROJECT_TARGET_HITS=none`, `BUILD_LAUNCHED=no`.

<SELF_AUDIT curie_static_patch="source_pass">
  <task-reconciliation>Tasks 03, 08, 10, 13, 14, 18, and 20 strengthened. No runtime ABI or ownership route migration claimed.</task-reconciliation>
  <struct-layout>No DTO layout changed. `GeologyVertex32` remains 32B, `GeologyRawVertex` 64B, telemetry 64B, manifest header 64B, manifest record 128B.</struct-layout>
  <hphi>No runtime Vault handles changed. Editor Temp/TempJob scratch remains bake-local and disposed.</hphi>
  <dependency-graph>No runtime dispatcher `JobHandle` route added. Existing editor jobs remain offline bake-local.</dependency-graph>
  <compile-guard>No asmdef reference changed.</compile-guard>
</SELF_AUDIT>

## SHINOBU_208 - ASSET EDIT SCOPE TRUTH PATCH

What was wrong:
- `TickAsyncBake` still opened `AssetDatabase.StartAssetEditing()` before `BakeSingle`, so CPU bake work ran under the AssetDatabase edit lock while docs/status claimed only the save tranche was locked.

What was done:
- Removed tick-level asset editing from `TickAsyncBake`.
- Moved edit-scope ownership into `SaveMeshesAndManifest`.
- `StartAssetEditing()` now wraps only the three LOD `SaveMeshAsset` calls.
- `StopAssetEditing()` runs in `finally` and clears `_asyncAssetEditing` even if the stop call faults.
- Architecture ledger and offline geology docs were corrected to match source truth.

Cinematic Cheats used:
- None added in this patch. Existing SHINOBU_208 Dear Lie remains offline static geology mesh/AO baking instead of runtime topology/physics generation.

Exact Microseconds saved:
- Runtime: 0 us; this is editor-only.
- Editor low-end: edit-scope wall time reduced from full variation bake to the asset-write tranche. Exact timing requires Unity Profiler; static proof removes the worst lock-window architecture.

Static verification:
- `GeologyForgeGenerator.cs BRACES=137/137`.
- `TICK_HAS_START_ASSET_EDITING=False`.
- `SAVE_HAS_START_ASSET_EDITING=True`.
- `SAVE_HAS_STOP_FINALLY=True`.
- `GUID_AFTER_STOP_SOURCE=True`.
- `TRAILING_WS=none`.
- Targeted `git diff --check` reports LF-to-CRLF warnings only.
- Build preflight: `CPU_SAMPLES=100,94,61`, `CPU_AVERAGE=85`, `BUILD_PROCS=none`, `PROJECT_TARGET_HITS=none`, `BUILD_LAUNCHED=no`.

<SELF_AUDIT asset_scope_patch="static">
  <task-reconciliation>Task 15/16 authoring scalability and crash containment strengthened; no ABI/runtime ownership change claimed.</task-reconciliation>
  <struct-layout>No DTO layout changed. `GeologyVertex32` remains 32B; manifest header 64B; manifest record 128B.</struct-layout>
  <hphi>No private runtime arrays or Vault handles changed; SHINOBU_208 remains Editor-only static payload generation.</hphi>
  <dependency-graph>No runtime `JobHandle` route added. Existing offline bake jobs still complete inside editor bake windows.</dependency-graph>
  <compile-guard>No asmdef reference changed. `Hecton8.World.OfflineGeology.Editor` remains Editor-only with Unity package references only.</compile-guard>
</SELF_AUDIT>

## 2026-05-20 - CSV Iso-Level Bridge Tail Report

What was wrong:
- The Forge UI exposed `Iso-Level`, and the SDF bake job consumed `profile.IsoLevel`, but CSV recipes did not persist that threshold. `GeologyProfileCsv` forced loaded profiles back to `0f`.

What was done:
- Added `iso_level` to `Assets/_Project/Data/Geology/geology_generation_profiles.csv`.
- Updated `GeologyProfileCsv` to detect the `iso_level` header token through a first-line byte scan.
- The parser reads and clamps `IsoLevel` only when the column exists; old CSV layouts still parse quality from the old position.
- Updated SHINOBU_208 status, rationale, and architecture note.

Cinematic cheats used:
- No new runtime fake was added in this tail patch. The persisted threshold feeds the existing offline SDF/vertex-AO bake route.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime path changed.
- Editor: avoids manual re-entry/rebake loops caused by lost threshold tuning. Parse adds one first-line byte scan.

Verification:
- Static scan: CSV header contains `iso_level`.
- Static scan: `HeaderHasIsoLevel` exists, `profile.IsoLevel` is read through the CSV parser, and no `File.ReadAllBytes`, `File.ReadAllLines`, `ReadByte`, or `.Split(` was introduced.
- Static CSV schema: header and all three authored rows contain 20 columns.
- Static brace scan: `GeologyProfileCsv.cs` 31/31.
- `.meta` presence check reports `MISSING_META_COUNT=0`.
- Build was not launched; CPU gate reported `CPU_AVERAGE=54`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="CSV_ISO_LEVEL_BRIDGE_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor CSV ingestion, data CSV, and SHINOBU_208 docs/logs.</runtime-boundary>
  <designer-bridge-proof>`iso_level` is now a persisted human-readable recipe value, not a volatile slider-only override.</designer-bridge-proof>
  <backward-compatibility-proof>Old CSV layouts without `iso_level` still route the next column to `GlobalQualityWeight`.</backward-compatibility-proof>
  <remaining-failures>
    <item>Compiler, Unity import, actual CSV reload, bake output, mesh inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - LUT Extraction And Compile-Wall Tail Report

What was wrong:
- `SdfToMeshExtractionJob` still encoded tetra topology through a chained `EmitOne`/`EmitPair` branch tree. It was deterministic, but it did not provide the LUT-shaped proof requested by Task 06.
- GeologyForge source still lived under the broad `Hecton8.Editor` assembly surface through folder inheritance, which pulls unrelated editor/package references into a geology-only edit.

What was done:
- Added `GeologyTetraExtractionLut`, a Burst-safe packed-nibble edge sequence table for the 16 tetra cases.
- `SdfCellVertexCountJob` and `SdfToMeshExtractionJob` now share the same case-index and vertex-count source, keeping count/extract parity explicit.
- Added `Hecton8.World.OfflineGeology.Editor.asmdef` with Editor-only include, unsafe enabled, and references limited to Unity Burst/Collections/Jobs/Mathematics.
- Updated SHINOBU_208 status, rationale, and architecture ledger entries.

Cinematic cheats used:
- No new runtime fake was added in this tail patch. The existing fake remains offline vertex AO in `Color.r` plus immutable static meshes instead of runtime SSAO or Marching Cubes.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime path changed.
- Editor: branch topology and compile-wall surface reduced, but exact Burst/compile deltas require Unity import and Burst Inspector proof.

Verification:
- Static JSON parse: `Hecton8.World.OfflineGeology.Editor.asmdef` parses and reports name `Hecton8.World.OfflineGeology.Editor`.
- Static brace scan: `GeologyForgeJobs.cs` 74/74, `GeologyForgeGenerator.cs` 122/122, `GeologyForgeWindow.cs` 34/34.
- Static scan: `EmitOne` and `EmitPair` are absent; `GeologyTetraExtractionLut` is present and used by count/extract jobs.
- Static scan: owned GeologyForge source still has scanner string literals for forbidden runtime patterns, but no owned runtime path is introduced.
- Static hygiene: targeted `git diff --check` reports only LF-to-CRLF normalization warnings, trailing-whitespace scan reports `TRAILING_WS=none`, `.asmdef` has no `Hecton8*` references, and `.meta` presence reports `MISSING_META_COUNT=0`.
- Build was not launched; CPU gate reported `CPU_AVERAGE=98` then `CPU_AVERAGE=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="LUT_EXTRACTION_COMPILEWALL_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source, asmdef isolation, and SHINOBU_208 docs/logs.</runtime-boundary>
  <compile-guard>`Hecton8.World.OfflineGeology.Editor.asmdef` references only Unity Burst/Collections/Jobs/Mathematics and no sibling runtime domain.</compile-guard>
  <extraction-proof>`SdfCellVertexCountJob` and `SdfToMeshExtractionJob` share the same packed tetra case edge table; managed LUT arrays were not introduced.</extraction-proof>
  <remaining-failures>
    <item>Compiler, Unity import, Burst Inspector, actual bake, mesh inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Runtime Scanner Schema V2 Tail Report

What was wrong:
- Task 19 report still behaved like an enriched grep: it named path, line, and pattern, but not execution context or routing priority.
- Raw hits mixed comment-only archaeology with actionable runtime helper mesh construction.

What was done:
- Extended `RuntimeMeshGenerationScanner` with schema v2 fields: `kind`, `executionContext`, `method`, `runtimePhaseRisk`, and `commentOnly`.
- Hardened method-context parsing for multi-line method signatures and attribute lines so `UploadSurfaceMesh`/`UploadColliderMesh` are classified as runtime helpers instead of type-scope noise.
- Regenerated `Docs/Reports/GEOMETRY_OPTIMIZATION_REPORT.json` from a static CLI scan: `findingCount=34`, `actionableFindingCount=28`, `simulationPhaseFindingCount=0`, `bootstrapPhaseFindingCount=0`, `proceduralMaterialCloneFindingCount=0`.
- Updated status, rationale, and architecture boundary docs.

Cinematic cheats used:
- No runtime simulation was added. The scanner reinforces the offline-bake route by giving integrators a precise migration map from runtime helper mesh construction to baked assets/BRG-ready payloads.

Exact microseconds saved:
- Runtime: 0 us measured; this patch is Editor-only and diagnostic.
- Future runtime savings remain owner-dependent because 28 actionable non-owned call sites still need migration.

Verification:
- Static scans of owned GeologyForge source remain clean for LINQ, `foreach`, `IEnumerable`, `IEnumerator`, `yield return`, `.Split(`, `File.ReadAllBytes`, `ReadByte`, `Pack=`, `MonoBehaviour`, `GlobalRegistry`, and `SignalBus`.
- `git diff --check` passed for the touched scanner/report files.
- Build was not launched; CPU/build gate reported `CPU=100` then `CPU=97`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="RUNTIME_SCANNER_SCHEMA_V2_TAIL" status="STATIC_PATCH_BUILD_BLOCKED">
  <task-reconciliation>
    <task id="19" verdict="PASS_STATIC_BUILD_BLOCKED">Scanner now classifies forbidden runtime mesh/material patterns by kind, context, method, risk, and comment-only status.</task>
    <task id="20" verdict="PASS_STATIC_BUILD_BLOCKED">Status, rationale, architecture note, report JSON, and bottom LOG report updated.</task>
  </task-reconciliation>
  <report-counters findingCount="34" actionableFindingCount="28" simulationPhaseFindingCount="0" bootstrapPhaseFindingCount="0" proceduralMaterialCloneFindingCount="0" runtimeMeshAllocationsEradicated="false" />
  <compile-guard>No runtime assembly, sibling runtime reference, GlobalRegistry, SignalBus, MonoBehaviour, runtime loader, generated GameObject, LODGroup, or MeshCollider was added.</compile-guard>
  <remaining-failures>
    <item>Compiler, Unity import, scanner menu execution, actual bake, Burst Inspector, mesh inspector, and profiler proof remain pending behind CPU/build gate.</item>
    <item>Runtime-wide mesh generation eradication remains false due 28 actionable non-owned findings plus 6 comment/type-scope archaeology hits.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Angle-Weighted Normal Weld Pass

What was wrong:
- Task 07 still carried a deviation: the previous normal pass blended SDF gradients with each triangle's own face normal, but did not accumulate neighboring face normals for coincident triangle-soup vertices.
- That left a baked-faceting risk and made the "MATHEMATICAL_NORMAL_AND_TANGENT_SMOOTHING" proof too weak.

What was done:
- Added `BuildNormalBucketJob`, a Burst `IJobParallelFor` that writes quantized position buckets into a transient `NativeParallelMultiHashMap<ulong,int>`.
- Extended `CalculateSmoothNormalsJob` to scan 27 adjacent buckets, reject candidates outside voxel-relative tolerance, accumulate candidate triangle normals weighted by the candidate corner angle, align the result to the SDF gradient, and write the final normal/tangent through `UnsafeUtility.AsRef`.
- Replaced the remaining AO hemisphere `math.sqrt` with guarded `math.rsqrt` math.

Cinematic cheats used:
- No runtime smoothing or normal recalculation exists. The richer surface response is baked in the Editor and packed into the immutable mesh stream.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Theoretical runtime topology/normal cost remains `O(0)` for GeologyForge assets. Editor normal smoothing is now O(V + local bucket neighbors), not O(V^2).

Verification:
- Static source shows `JOB_COUNT=10`, `MANDATED_BURST_ATTRS=10`.
- Owned GeologyForge source is clean for `math.sqrt`, `Mathf.Sqrt`, `.normalized`, `math.length(`, LINQ, `foreach`, `IEnumerable`, `IEnumerator`, and `yield return`.
- Build was not launched; CPU gate reported `CPU=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="ANGLE_WEIGHTED_NORMAL_WELD" status="STATIC_PATCH_BUILD_BLOCKED">
  <task-reconciliation>
    <task id="07" verdict="PASS_STATIC_BUILD_BLOCKED">Angle-weighted neighboring face-normal accumulation exists through transient weld buckets; Unity/Burst proof pending.</task>
    <task id="20" verdict="PASS_STATIC_BUILD_BLOCKED">Status, rationale, architecture doc, and placeholder reports updated; compiler proof blocked by CPU gate.</task>
  </task-reconciliation>
  <dependency-graph>
    <handle order="1">`sdfHandle` completes before count.</handle>
    <handle order="2">`countHandle` completes before offset prefix sum.</handle>
    <handle order="3">`extractHandle` completes before normal buckets.</handle>
    <handle order="4">`bucketHandle` feeds `normalHandle`.</handle>
    <handle order="5">`normalHandle` feeds `uvHandle`.</handle>
    <handle order="6">`aoHandle` runs after UV/normal attributes complete.</handle>
  </dependency-graph>
  <aliasing>[NoAlias] remains on non-overlapping NativeArray fields. Bucket map and raw vertex pointer are separate native allocations.</aliasing>
  <runtime-boundary>No runtime assembly, GlobalRegistry, SignalBus, MonoBehaviour, generated GameObject, LODGroup, or MeshCollider route was added.</runtime-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, actual bake, Burst Inspector, and profiler proof remain pending behind CPU gate.</item>
    <item>Runtime-wide mesh generation eradication remains false due 34 non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Cold Allocation Comment Canon Pass

What was wrong:
- Owned GeologyForge files had `COLD ALLOC` comments, but several used hyphen separators instead of the exact mandated `Type[count] â€” reason â€” owner` form.

What was done:
- Normalized all `COLD ALLOC` comments in `GeologyForgeGenerator.cs` and `GeologyForgeWindow.cs` to the local canonical string.

Cinematic cheats used:
- None. This is auditability hygiene, not rendering or simulation work.

Exact microseconds saved:
- Runtime: 0 us. No logic changed.
- Editor: 0 us expected. Search/review determinism improved.

Verification:
- `rg "COLD ALLOC: .* - .* - owner" Assets/_Project/Scripts/Editor/GeologyForge` returned no matches.
- Build was not launched. CPU gate reported `CPU=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="COLD_ALLOC_COMMENT_CANON" status="STATIC_PATCH_BUILD_BLOCKED">
  <runtime-boundary>No runtime code changed.</runtime-boundary>
  <verification>Owned GeologyForge source has no stale hyphen-form `COLD ALLOC` comments.</verification>
  <remaining-failures>
    <item>Compiler, Unity import, and profiler proof remain pending behind CPU gate.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned scan findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Preview GC Polish Pass

What was wrong:
- `GeologyForgePreview.Build` allocated `List<Vector3>` and then `Vector3[]` through `ToArray()` every time the user requested a SceneView point-cloud preview.
- The allocation was Editor-only, but it contradicted the Task 18 intent: rapid preview edits should stay bounded and predictable.

What was done:
- Replaced the per-preview list and array copy with one cold `Vector3[2048]` buffer owned by `GeologyForgePreview`.
- Added `_pointCount` and converted SceneView draw to an index-based loop over active points.
- Left the SDF scratch as local TempJob memory disposed in `finally`; no persistent native owner or runtime Vault surface was added.

Cinematic cheats used:
- Preview remains a point-cloud SDF slice, not a full mesh bake. The artist sees the shape before committing to topology, normals, AO, and disk serialization.

Exact microseconds saved:
- Measured runtime saving: 0 us. This path is Editor-only and no Unity profiler run happened.
- Editor allocation removed per preview command: one `List<Vector3>` backing array path plus one `Vector3[]` copy from `ToArray()`.

Verification:
- Static scan of owned GeologyForge source is clean for `List<Vector3>` and `ToArray(` after the patch.
- Build was not launched. CPU gate reported `CPU=100`.

<SELF_AUDIT agent="SHINOBU_208" pass="PREVIEW_GC_POLISH" status="STATIC_PATCH_BUILD_BLOCKED">
  <task-reconciliation>
    <task id="18" verdict="PASS_STATIC_BUILD_BLOCKED">SceneView preview uses bounded cold point buffer and no per-preview managed point-list copy. Unity visual proof pending.</task>
    <task id="20" verdict="PASS_STATIC_BUILD_BLOCKED">Status, rationale, and LOG updated; compiler proof blocked by CPU gate.</task>
  </task-reconciliation>
  <runtime-boundary>Patch is inside `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeWindow.cs`; no runtime assembly, MonoBehaviour, GlobalRegistry, SignalBus, or scene object route added.</runtime-boundary>
  <allocation-proof>Owned source scan after patch finds no `List&lt;Vector3&gt;` or `ToArray(` in GeologyForge. Preview points are capped by `MaxPreviewPoints=2048`.</allocation-proof>
  <remaining-failures>
    <item>Project-wide runtime mesh generation eradication remains false: 34 non-owned findings.</item>
    <item>Unity import, actual bake, self-audit execution, and profiler proof remain pending behind CPU build gate.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Little-Endian Payload Guard

What was wrong:
- The `.h8geom` manifest and black-box dump are raw unmanaged binary payloads. They had layout proof but no explicit endian fail-fast.

What was done:
- Added `BitConverter.IsLittleEndian` guard before writing `geology_mesh_manifest.h8geom`.
- Added the same guard before writing `Dump_SHINOBU_208.bin`.
- Added manifest audit fail-closed reason `BIG_ENDIAN_HOST_UNSUPPORTED` for non-little-endian hosts.

Cinematic cheats used:
- None. This is payload integrity, not presentation.

Exact microseconds saved:
- Runtime saving: 0 us.
- Editor cost: one branch per binary write. The gain is corruption prevention, not frame time.

Compile / verification:
- Static scan confirms endian guards exist in `GeologyForgeGenerator` and `GeologyForgeSelfAudit`.
- Build was not launched: CPU gate remains above the legal threshold.

<SELF_AUDIT agent="SHINOBU_208" pass="LITTLE_ENDIAN_GUARD" status="STATIC_SOURCE_BUILD_BLOCKED">
  <payload path="Assets/_Project/BakedGeometry/Geology/geology_mesh_manifest.h8geom" endian="little" nonLittleEndianHost="fail-fast" />
  <payload path="Docs/AgentLogs/Dump_SHINOBU_208.bin" endian="little" nonLittleEndianHost="fail-fast" />
  <remaining-failures>
    <item>Unity bake/audit execution pending.</item>
    <item>Project-wide runtime mesh generation eradication remains false: 34 non-owned findings.</item>
    <item>Compile/import remains blocked by CPU gate.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Layout Self-Audit Tool Patch

What was wrong:
- The source validator checked struct/mesh layout during bake, but there was no repeatable operator command to audit already generated `.asset` meshes and the `.h8geom` manifest from disk.
- Task 20 therefore still relied too much on source review instead of a concrete report artifact.

What was done:
- Added `GeologyForgeSelfAudit` under the Editor-only GeologyForge lane.
- Added menu/window command `HECTON-8/Geology Forge/Run Layout Self Audit`.
- The audit validates every generated geology mesh via `GeologyVertexLayoutValidator.ValidateMesh`.
- The audit validates `geology_mesh_manifest.h8geom` with stack/Span reads, checking magic, version, header size, record size, stride, LOD count, file length, finite bounds, flags, triangle counts, and non-zero LOD GUIDs.
- Added `Docs/Reports/GEOLOGY_LAYOUT_AUDIT.json` as the report target.

Cinematic cheats used:
- No runtime loader or validator was added. Verification stays in Editor; runtime remains static mesh + manifest consumption by a separate owner.

Exact microseconds saved:
- Measured runtime saving: 0 us; no runtime path was added.
- Audit cost is O(mesh assets + manifest records), paid only when the editor operator runs the command.

Compile / verification:
- Static source scans show no owned GeologyForge `ReadByte`, `File.ReadAllBytes`, `File.ReadAllLines`, `string.Split`, `.Split(`, generated prefab, `LODGroup`, `new GameObject`, `AddComponent`, `Renderer[]`, `Mesh[]`, `MeshCollider`, `GlobalRegistry`, `SignalBus`, `HectonEventBus`, `MonoBehaviour`, DTO property, or `Pack=` path.
- `JOB_COUNT=9`, `MANDATED_BURST_ATTRS=9`.
- `git diff --check` returned no whitespace errors; Git warned that `Directory.Build.targets` and `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` will normalize LF to CRLF on next touch.
- Build was not launched: `CPU_AVERAGE=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="LAYOUT_SELF_AUDIT_TOOL" status="STATIC_SOURCE_BUILD_BLOCKED">
  <artifact path="Docs/Reports/GEOLOGY_LAYOUT_AUDIT.json" status="placeholder-until-unity-bake" />
  <tool menu="HECTON-8/Geology Forge/Run Layout Self Audit" />
  <checks>
    <check>Generated mesh vertex stream stride and attributes: Position Float32x3, Normal Float32x3, Color UNorm8x4, UV0 UNorm16x2.</check>
    <check>Manifest header: magic/version/header size/record size/vertex stride/LOD count/file length.</check>
    <check>Manifest records: finite bounds, non-negative triangle counts, BRG-ready flag, non-zero LOD GUIDs, 32B stride.</check>
  </checks>
  <remaining-failures>
    <item>Unity Editor bake/audit execution pending; current layout audit JSON is a placeholder.</item>
    <item>Project-wide runtime mesh generation eradication remains false: 34 non-owned findings.</item>
    <item>Compile/import remains blocked by CPU gate.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - BRG Manifest Patch

What was wrong:
- GeologyForge still emitted generated prefab/LODGroup/GameObject wrappers after mesh bake. That is editor-friendly but it preserves a standard Unity object route and does not match the BRG/static-file runtime handoff in the assignment.
- The fixed three-LOD handoff used managed `Mesh[]` staging even though LOD0/1/2 are structurally fixed.

What was done:
- Removed generated prefab output from the bake lane.
- Added `Assets/_Project/BakedGeometry/Geology/geology_mesh_manifest.h8geom` as the BRG-oriented static payload written during asset bakes.
- Added `GeologyMeshManifestHeader` as a 64B explicit DTO and `GeologyMeshManifestRecord` as a 128B explicit DTO.
- Manifest records store sector AUP, deterministic seed, profile hash, LOD triangle counts, 32B vertex stride, local bounds, LOD0/1/2 mesh GUID high/low words, BRG-ready flag, and variation.
- Replaced fixed-LOD managed `Mesh[]` staging with a private fixed-field `MeshLodSet`.
- Replaced CSV per-byte `ReadByte()` with unmanaged `Span<byte>` chunked stream reads and removed `string.Split` from the asset folder helper.
- Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with the SHINOBU_208 static payload boundary.

Cinematic cheats used:
- Runtime does not need generated scene objects for static geology. BRG/indirect consumers can load immutable mesh assets plus a compact manifest and apply continuous LOD policy from their own quality owner.

Exact microseconds saved:
- Measured runtime saving: 0 us; no Play Mode/profiler proof exists.
- Theoretical object-route removal: generated GameObject/LODGroup traversal for GeologyForge assets is replaced by one fixed manifest scan plus BRG/indirect draw submission by a future runtime owner.

Compile / verification:
- Static scans show no owned GeologyForge generated prefab, `LODGroup`, `new GameObject`, `AddComponent`, `Renderer[]`, `Mesh[]`, or `MeshCollider` path.
- Static scans show no owned GeologyForge `ReadByte`, `File.ReadAllBytes`, `File.ReadAllLines`, `string.Split`, or `.Split(` path.
- Manifest layout is guarded by `GeologyVertexLayoutValidator`; compile/import proof remains pending behind the CPU gate.
- BRG manifest patch recheck: `JOB_COUNT=9`, `MANDATED_BURST_ATTRS=9`, `MANIFEST_HITS=52`, `CPU_AVERAGE=100`, `BUILD_PROCS=none`.
- `git diff --check` returned no whitespace errors; Git warned that `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` will normalize LF to CRLF on next touch.

<SELF_AUDIT agent="SHINOBU_208" pass="BRG_MANIFEST_PATCH" status="STATIC_SOURCE_BUILD_BLOCKED">
  <struct-layout>
    <dto name="GeologyMeshManifestHeader" size="64">
      <field name="Magic" offset="0" size="4" />
      <field name="Version" offset="4" size="4" />
      <field name="RecordCount" offset="8" size="4" />
      <field name="RecordSize" offset="12" size="4" />
      <field name="HeaderSize" offset="16" size="4" />
      <field name="VertexStrideBytes" offset="20" size="4" />
      <field name="LodCount" offset="24" size="4" />
      <field name="Flags" offset="28" size="4" />
      <field name="Reserved0" offset="32" size="8" />
      <field name="Reserved1" offset="40" size="8" />
      <field name="Reserved2" offset="48" size="8" />
      <field name="Reserved3" offset="56" size="8" />
      <math>8 uint fields = 32 bytes; 4 ulong reserved fields = 32 bytes; total 64 bytes.</math>
    </dto>
    <dto name="GeologyMeshManifestRecord" size="128">
      <field name="SectorAup" offset="0" size="24" type="double3" />
      <field name="Seed" offset="24" size="4" />
      <field name="ProfileHash" offset="28" size="4" />
      <field name="Lod0Triangles" offset="32" size="4" />
      <field name="Lod1Triangles" offset="36" size="4" />
      <field name="Lod2Triangles" offset="40" size="4" />
      <field name="VertexStrideBytes" offset="44" size="4" />
      <field name="BoundsCenter" offset="48" size="12" type="float3" />
      <field name="BoundsExtents" offset="60" size="12" type="float3" />
      <field name="Lod0GuidHigh" offset="72" size="8" />
      <field name="Lod0GuidLow" offset="80" size="8" />
      <field name="Lod1GuidHigh" offset="88" size="8" />
      <field name="Lod1GuidLow" offset="96" size="8" />
      <field name="Lod2GuidHigh" offset="104" size="8" />
      <field name="Lod2GuidLow" offset="112" size="8" />
      <field name="Flags" offset="120" size="4" />
      <field name="Variation" offset="124" size="4" />
      <math>24+6*4+2*12+6*8+2*4=128 bytes.</math>
    </dto>
  </struct-layout>
  <compile-guard>Still Editor-only; no runtime assembly, no sibling runtime reference, no GlobalRegistry, no SignalBus, no generated GameObject route.</compile-guard>
  <remaining-failures>
    <item>Project-wide runtime mesh generation eradication remains false: 34 non-owned findings.</item>
    <item>Unity import, actual bake, manifest GUID proof, BRG consumption, Burst compile, and profiler proof remain pending behind CPU build gate.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Forensic Black Box Patch

What was wrong:
- The bake report covered successful batch metrics, but a failed bake had no fixed-size 300-entry forensic ring.
- The first black-box insertion had two compile-risk flaws: Unity profile risk from `double.IsFinite`, and non-finite warning flags did not propagate back into `GeologyBakeMetrics`.

What was done:
- Added `GeologyBakeTelemetryEntry` as an explicit 64B row and `GeologyBakeDumpHeader` as an explicit 32B binary header.
- `GeologyForgeGenerator` now records SDF, extraction/count, attribute, AO, and serialization stages into a 300-entry ring.
- Non-finite stage timings and bake exceptions dump `Docs/AgentLogs/Dump_SHINOBU_208.bin`.
- Replaced `double.IsFinite` with `IsNaN`/`IsInfinity` guards and returned warning flags from `RecordTelemetry` so the dump path is actually reachable.

Cinematic cheats used:
- No runtime forensic owner was added. The ring is editor-only diagnostic memory for an offline bake lane.

Exact microseconds saved:
- Measured runtime saving: 0 us; no runtime code path is added.
- Failure-analysis gain: the last 300 bake stages are written as 19,232 bytes of deterministic binary data instead of relying on managed exception text.

Compile / verification:
- Static scans show no owned GeologyForge hits for direct `GlobalRegistry`, `SignalBus`, `HectonEventBus`, `MonoBehaviour`, Unity lifecycle methods, DTO properties, `File.ReadAllBytes`, `File.ReadAllLines`, `Pack=`, `MeshCollider`, `double.IsFinite`, or stale unsafe read-only pointer usage.
- Expected scanner literal strings remain inside `RuntimeMeshGenerationScanner` only: `.SetVertices(`, `mesh.vertices`, `.material`.
- `git diff --check` passes for the owned GeologyForge and SHINOBU_208 doc/report files touched in this pass.
- Burst audit after residue fix: `JOB_COUNT=9`, `MANDATED_BURST_ATTRS=9`; every non-overlapping NativeArray and the raw pointer normal pass carry `[NoAlias]`.
- Build is still gated by CPU protocol and has not been launched after this patch. Recheck: `CPU_AVERAGE=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="BLACKBOX_PATCH" status="STATIC_SOURCE_BUILD_BLOCKED">
  <struct-layout>
    <dto name="GeologyBakeDumpHeader" size="32">
      <field name="Magic" offset="0" size="4" />
      <field name="EntryCount" offset="4" size="4" />
      <field name="EntrySize" offset="8" size="4" />
      <field name="Cursor" offset="12" size="4" />
      <field name="Reason" offset="16" size="4" />
      <field name="Reserved0" offset="20" size="4" />
      <field name="Reserved1" offset="24" size="8" />
      <math>4+4+4+4+4+4+8=32 bytes.</math>
    </dto>
    <dto name="GeologyBakeTelemetryEntry" size="64" false-sharing="one full cache line row">
      <field name="SectorAup" offset="0" size="24" type="double3" />
      <field name="Seed" offset="24" size="4" />
      <field name="Stage" offset="28" size="4" />
      <field name="StageMilliseconds" offset="32" size="4" />
      <field name="RawVertexCount" offset="36" size="4" />
      <field name="Lod0Triangles" offset="40" size="4" />
      <field name="Lod1Triangles" offset="44" size="4" />
      <field name="Lod2Triangles" offset="48" size="4" />
      <field name="WarningFlags" offset="52" size="4" />
      <field name="StateHash" offset="56" size="4" />
      <field name="DumpReason" offset="60" size="4" />
      <math>24+10*4=64 bytes; one row per cache line.</math>
    </dto>
  </struct-layout>
  <black-box path="Docs/AgentLogs/Dump_SHINOBU_208.bin" entries="300" row-bytes="64" header-bytes="32" total-bytes="19232" />
  <vault-status>Runtime VaultBufferHandle requests remain none; this is Editor-only TempJob diagnostic scratch, disposed after bake.</vault-status>
  <remaining-failures>
    <item>Project-wide runtime mesh generation eradication remains false: 34 non-owned findings.</item>
    <item>Unity import, Burst compile, bake execution, mesh inspector proof, and profiler proof remain pending behind the CPU build gate.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Ultra Polish Pass

What was wrong:
- `GeologyRawVertex` used sequential 56B layout. That was not illegal alignment, but it failed the stronger byte-offset proof and false-sharing demand.
- CSV ingest staged the whole file in managed `byte[]`.
- `GlobalQualityWeight` did not drive enough of the bake math.
- The generated prefab carried a `MeshCollider`, which contaminated a render-bake lane with runtime physics truth.
- Project-wide runtime mesh generation still is not eradicated. The refreshed scanner still reports 34 findings.

What was done:
- Converted `GeologyVertex32` to explicit 32B layout and `GeologyRawVertex` to explicit 64B layout.
- Added validator checks for exact size and field offsets.
- Reworked CSV profile ingest to stream into Temp `NativeArray<byte>` and parse through unmanaged byte pointers.
- Replaced the last owned UI lambda and full-file scanner line array.
- Routed `GlobalQualityWeight` through `math.smoothstep` into noise octaves, AO rays, AO steps, AO range, UV scale, LOD budgets, and collapse size.
- Removed `MeshCollider` generation from GeologyForge prefabs.
- Refreshed `Docs/Reports/GEOMETRY_OPTIMIZATION_REPORT.json`; result remains `findingCount=34`, `runtimeMeshAllocationsEradicated=false`.

Cinematic cheats used:
- Static topology is baked offline; runtime Marching Cubes cost for GeologyForge assets is 0.
- Crevice depth is baked into vertex red channel; runtime SSAO is not required for these rocks.
- Collision is not generated by this render-bake lane; runtime physics truth must come from a separate owner route, not render mesh colliders.

Exact microseconds saved:
- Measured runtime saving: 0 us. No Play Mode/profiler proof exists.
- Theoretical per-frame removal for GeologyForge assets: runtime MC `O(cells)` -> `O(0)`; runtime SSAO-like crevice evaluation `O(pixels * taps)` -> vertex color fetch `O(1)` per shaded vertex; runtime collider work from generated prefabs removed.
- CLI scanner runtime was not the product frame. It remains diagnostic only.

Compile / verification:
- Build not launched in polish pass. CPU recheck reported `Average=100`, which violates the build gate.

<SELF_AUDIT agent="SHINOBU_208" pass="ULTRA_POLISH" status="STATIC_POLISH_DONE_RUNTIME_ERADICATION_FALSE_BUILD_BLOCKED">
  <task-reconciliation>
    <task id="01" verdict="FAIL">Scanner and report exist, but project-wide runtime mesh generation is not eradicated; 34 non-owned runtime topology findings remain.</task>
    <task id="02" verdict="PASS">Owned GeologyForge path uses shared material references and vertex colors; scanner includes `.material` forbidden pattern.</task>
    <task id="03" verdict="PASS">Owned generation DTOs use raw public fields; owned source is clean for `get; set;`.</task>
    <task id="04" verdict="PASS">`GeologyVertex32` explicit 32B stream is validated by size and offsets.</task>
    <task id="05" verdict="PASS">`GenerateMockFractalNoiseJob` is Burst compiled and generates ridged/Voronoi SDF density.</task>
    <task id="06" verdict="PASS_WITH_DEVIATION">Extraction uses exact-count `NativeArray` triangle soup instead of growable `NativeList`; this avoids dynamic capacity mutation and zero-init cost.</task>
    <task id="07" verdict="PASS_WITH_DEVIATION">Normals use SDF gradient plus face fallback and tangent authoring. Shared-vertex angle accumulation is not used because the extraction output is triangle soup; runtime smoothing comes from SDF normals.</task>
    <task id="08" verdict="PASS">`BakeVertexOcclusionJob` writes AO scalar into red byte of packed vertex color.</task>
    <task id="09" verdict="PASS_WITH_DEVIATION">Deterministic LOD0/1/2 generation is implemented with budgeted triangle selection and snap collapse, not full QEM.</task>
    <task id="10" verdict="PASS_PENDING_UNITY">Mesh serialization code uses `SetVertexBufferData`, `SetIndexBufferData`, and `AssetDatabase`, but Unity import/bake execution is pending.</task>
    <task id="11" verdict="PASS">`GenerateTriplanarUvsJob` projects UVs by dominant normal axis and packs UV0 as UNorm16x2.</task>
    <task id="12" verdict="PASS">AUP sector double3 is hashed through FNV-1a before local float noise sampling.</task>
    <task id="13" verdict="PASS">Architecture doc excludes baked mesh buffers from rollback/Merkle/StateRingBuffer hashing.</task>
    <task id="14" verdict="PASS">Editor scratch buffers use `NativeArrayOptions.UninitializedMemory` where fully overwritten.</task>
    <task id="15" verdict="PASS_PENDING_UNITY">Bake report writer exists; current JSON is placeholder because no Unity bake ran.</task>
    <task id="16" verdict="PASS_PENDING_UNITY">UI Toolkit Geology Forge window exists; Unity menu/import proof pending.</task>
    <task id="17" verdict="PASS">CSV parser streams bytes into unmanaged Temp scratch and parses without managed token split.</task>
    <task id="18" verdict="PASS_PENDING_UNITY">SceneView preview samples low-res SDF points without full mesh bake; Unity visual proof pending.</task>
    <task id="19" verdict="FAIL_VERDICT_PASS_TOOL">Static scanner exists and refreshed the report, but its verdict is `runtimeMeshAllocationsEradicated=false`.</task>
    <task id="20" verdict="PASS_STATIC_BUILD_BLOCKED">Self-audit and logs exist; compile remains blocked by CPU gate.</task>
  </task-reconciliation>
  <struct-layout>
    <dto name="GeologyVertex32" size="32" false-sharing="not-contended-runtime-upload">
      <field name="Position" offset="0" size="12" type="float3" />
      <field name="Normal" offset="12" size="12" type="float3" />
      <field name="ColorRgba" offset="24" size="4" type="uint/UNorm8x4" note="AO in red byte" />
      <field name="Uv0Packed" offset="28" size="4" type="uint/UNorm16x2" />
      <math>12+12+4+4=32 bytes; stride is multiple of 4, 8, 16, and 32.</math>
    </dto>
    <dto name="GeologyRawVertex" size="64" false-sharing="one full cache line per worker-written row">
      <field name="Position" offset="0" size="12" type="float3" />
      <field name="Normal" offset="12" size="12" type="float3" />
      <field name="Tangent" offset="24" size="16" type="float4" />
      <field name="Uv" offset="40" size="8" type="float2" />
      <field name="AmbientOcclusion" offset="48" size="4" type="float" />
      <field name="Flags" offset="52" size="4" type="uint" />
      <field name="_pad0" offset="56" size="8" type="ulong" />
      <math>12+12+16+8+4+4+8=64 bytes; parallel row writes do not share a 64B cache line.</math>
    </dto>
  </struct-layout>
  <scalability-curve>
    `GlobalQualityWeight` is clamped and passed through `math.smoothstep(0,1,q)`.
    Below 0.3 the bake trends toward 2 noise octaves, 8 AO rays, 2 AO steps, 0.24 radius AO range, reduced LOD budgets, and coarse collapse sizes.
    Middle weights interpolate every parameter continuously.
    At 1.0 the bake approaches authored profile octaves/rays/budgets, 9 AO steps, 0.9 radius AO range, denser LOD0, and finer collapse.
  </scalability-curve>
  <h-phi-vault-status>
    Runtime VaultBufferHandle requests: none.
    Reason: this is an Editor-only offline asset compiler, not a persistent runtime owner. It declares no private persistent `NativeArray`, `NativeList`, or `NativeHashMap` fields.
    Scratch memory is local Temp/TempJob inside bake/preview methods and disposed in `finally`.
  </h-phi-vault-status>
  <pointer-aliasing-and-dependencies>
    Consumed handles: none from runtime dispatcher; Editor bake starts the chain.
    Output chain: `sdfHandle`, `countHandle`, `extractHandle`, `normalHandle`, `uvHandle`, `aoHandle`, per-LOD `decimateHandle`, `packHandle`, `indexHandle`.
    Blocking `Complete()` calls occur only in Editor bake/preview code, never in runtime frame loop.
    `[NoAlias]` is applied on non-overlapping NativeArray fields in Burst jobs; pointer normal pass uses `UnsafeUtility.AsRef`.
  </pointer-aliasing-and-dependencies>
  <compile-guard>
    Owned GeologyForge source has no direct sibling-domain `using Hecton8.*` imports, no `GlobalRegistry`, no `SignalBus`, no `HectonEventBus`, no `MonoBehaviour`, and no Unity runtime lifecycle methods.
    Editor project include additions reference Unity/Burst/Collections only for this lane.
  </compile-guard>
  <dear-lie>
    Heavy truth rejected: runtime Marching Cubes, runtime SSAO, render-mesh collider truth.
    Dear Lie route: offline static mesh bake, baked vertex red AO, no generated MeshCollider.
    Complexity before: runtime topology `O(cells)` per generated chunk plus per-frame SSAO `O(pixels*taps)`.
    Complexity after for GeologyForge assets: topology `O(0)` runtime, AO `O(1)` vertex color fetch, draw cost governed by static mesh LOD.
  </dear-lie>
  <failures>
    <item>Project-wide runtime mesh generation eradication is false: 34 findings remain in non-owned lanes.</item>
    <item>Unity import, actual bake, mesh inspector proof, Burst compile proof, and profiler proof are pending because CPU build gate reported 100 percent.</item>
  </failures>
</SELF_AUDIT>

## 2026-05-20 - Preview And Cold Allocation Tail Report

What was wrong:
- SceneView preview still allocated per-preview point containers: `List<Vector3>` and `ToArray()`.
- Owned cold-allocation comments were present but not all used the exact AGENTS.md canonical separator format.

What was done:
- Replaced preview point churn with one cold `Vector3[2048]` buffer and `_pointCount`.
- Normalized all owned GeologyForge `COLD ALLOC` comments to `Type[count] â€” reason â€” owner`.

Cinematic cheats used:
- The preview remains a low-cost SDF point cloud rather than a full mesh bake.

Exact microseconds saved:
- Runtime: 0 us; this is Editor-only.
- Editor allocation removed per preview command: one point list backing path and one `Vector3[]` copy. No profiler timing exists.

Verification:
- Owned source scan is clean for `List<Vector3>`, `ToArray(`, and stale hyphen-form `COLD ALLOC` comments.
- Build was not launched; CPU gate reported `CPU=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="PREVIEW_AND_COLD_ALLOC_TAIL" status="STATIC_PATCH_BUILD_BLOCKED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, bake execution, and profiler proof remain pending behind CPU gate.</item>
    <item>Runtime-wide mesh generation eradication remains false due 34 non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Angle-Weighted Normal Weld Tail Report

What was wrong:
- Task 07 still carried a real deviation in the latest bottom report: shared-edge triangle-soup vertices were not accumulating neighboring face normals.

What was done:
- Added a Burst bucket build pass and angle-weighted normal accumulation before triplanar UV generation.
- Updated `Docs/Tasks/Status_SHINOBU_208.md`, `Docs/AgentLogs/Rationale_SHINOBU_208.md`, `Docs/ARCHITECTURE/OFFLINE_GEOLOGY_MESH_BAKER.md`, `GEOLOGY_BAKE_REPORT.json`, and `GEOLOGY_LAYOUT_AUDIT.json` with the new boundary.

Cinematic cheats used:
- Normal smoothness is authored offline; runtime does not recalculate normals or run topology work.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime path changed.
- Theoretical runtime normal/topology cost remains `O(0)` for these baked assets.

Verification:
- Static scan: `JOB_COUNT=10`, `MANDATED_BURST_ATTRS=10`.
- Static scan: owned GeologyForge source is clean for `math.sqrt`, `Mathf.Sqrt`, `.normalized`, `math.length(`, LINQ, `foreach`, `IEnumerable`, `IEnumerator`, and `yield return`.
- Build was not launched; CPU gate reported `CPU=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="ANGLE_WEIGHTED_NORMAL_WELD_TAIL" status="STATIC_PATCH_BUILD_BLOCKED">
  <task-reconciliation>
    <task id="07" verdict="PASS_STATIC_BUILD_BLOCKED">Angle-weighted neighboring normal accumulation exists through transient weld buckets. Unity/Burst proof pending.</task>
    <task id="20" verdict="PASS_STATIC_BUILD_BLOCKED">Bottom LOG report, status, rationale, architecture doc, and placeholder reports updated.</task>
  </task-reconciliation>
  <dependency-graph>`extractHandle -> bucketHandle -> normalHandle -> uvHandle -> aoHandle -> LOD pack` in the Editor bake lane.</dependency-graph>
  <compile-guard>No runtime assembly, sibling runtime reference, GlobalRegistry, SignalBus, MonoBehaviour, generated GameObject, LODGroup, MeshCollider, or runtime loader was added.</compile-guard>
  <remaining-failures>
    <item>Compiler, Unity import, actual bake, Burst Inspector, mesh inspector, and profiler proof remain pending behind CPU gate.</item>
    <item>Runtime-wide mesh generation eradication remains false due 34 non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Direct Noise Quality Weight Tail Report

What was wrong:
- `GenerateMockFractalNoiseJob` did not directly receive `GlobalQualityWeight`; bake callers resolved part of the behavior outside the core SDF generator, and preview did not mirror the bake quality curve.

What was done:
- Added `GlobalQualityWeight` to the unmanaged noise job DTO.
- Applied `math.smoothstep` inside the job to scale SDF frequency, amplitude, ridged/Voronoi contribution, and fractional octave contribution.
- Passed the same weight from full bake and SceneView preview.

Cinematic cheats used:
- The quality curve sculpts cheaper authored SDF detail instead of adding any runtime topology or lighting work.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code path exists in this lane.
- Editor: lower-quality profiles run fewer active octave contributions and lower-frequency SDF detail. Exact bake milliseconds require Unity editor execution.

Verification:
- Static scan: `GlobalQualityWeight` is present in the noise job, generator assignment, and preview assignment.
- Static scan: owned GeologyForge source remains clean for `math.sqrt`, `Mathf.Sqrt`, `.normalized`, `math.length(`, LINQ, `foreach`, `IEnumerable`, `IEnumerator`, `yield return`, `File.ReadAllBytes`, `File.ReadAllLines`, `ReadByte`, `.Split(`, `Pack=`, `MeshCollider`, `MonoBehaviour`, `GlobalRegistry`, and `SignalBus`.
- Static scan: `JOB_COUNT=10`, `MANDATED_BURST_ATTRS=10`.
- Targeted `git diff --check` and trailing-whitespace scan passed for patched C# files.
- Build was not launched in this report segment.

<SELF_AUDIT agent="SHINOBU_208" pass="DIRECT_NOISE_QUALITY_WEIGHT_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <task-reconciliation>
    <task id="05" verdict="PASS_STATIC_BUILD_BLOCKED">Mock SDF generator now consumes continuous quality directly.</task>
    <task id="18" verdict="PASS_STATIC_BUILD_BLOCKED">Preview SDF now passes the same quality scalar as bake.</task>
    <task id="20" verdict="PASS_STATIC_BUILD_BLOCKED">Status, rationale, architecture doc, and bottom LOG report updated.</task>
  </task-reconciliation>
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, actual bake, Burst Inspector, mesh inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due 34 non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Unsaved Bake Mesh Lifetime Tail Report

What was wrong:
- `BakeSingle(profile, variation, saveAssets:false)` created LOD0/1/2 Unity `Mesh` objects and returned metrics without assigning asset ownership or destroying the transient meshes.

What was done:
- Added a `try/finally` around the LOD metric/save block.
- Added `DestroyTransientLods` and call it only when `saveAssets` is false.

Cinematic cheats used:
- None added in this tail patch; it is lifetime hygiene for the existing offline fake pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code path exists in this lane.
- Editor: prevents native mesh retention across report-only bake probes. Exact memory slope requires Unity editor profiling.

Verification:
- Static scan: unsaved path now reaches `DestroyTransientLods(lods)` in `finally`.
- Static scan: owned GeologyForge source remains clean for forbidden hot-path patterns and stale managed CSV reads.
- Static scan: `JOB_COUNT=10`, `MANDATED_BURST_ATTRS=10`.
- Targeted `git diff --check` and trailing-whitespace scan passed for patched C# files.
- Build was not launched in this report segment; CPU gate reported `CPU_AVERAGE=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="UNSAVED_BAKE_MESH_LIFETIME_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <lifetime-proof>`saveAssets:false` destroys transient LOD meshes; `saveAssets:true` keeps asset-owned meshes valid.</lifetime-proof>
  <remaining-failures>
    <item>Compiler, Unity import, actual bake, Burst Inspector, mesh inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due 34 non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Noise Quality Single Owner Tail Report

What was wrong:
- Full bake pre-scaled `Octaves` before assigning `GenerateMockFractalNoiseJob`, while the job also consumed `GlobalQualityWeight`. That created split ownership of SDF quality collapse.

What was done:
- `BakeSingle` now passes raw sanitized `profile.Octaves` to the Burst noise job.
- The job remains the only owner of octave-span collapse; generator-level `qualityCurve` remains only for UV, AO, and LOD authoring costs.

Cinematic cheats used:
- Low quality still authors cheaper SDF detail offline instead of adding runtime topology or SSAO work.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime path changed.
- Editor: prevents accidental double-collapse in low-quality bakes; exact bake milliseconds require Unity editor execution.

Verification:
- Static scan: no caller-side octave pre-collapse pattern remains in owned GeologyForge source.
- Static scan: owned GeologyForge source remains clean for forbidden hot-path patterns and direct runtime-coupling symbols.
- Static scan: `JOB_COUNT=10`, `MANDATED_BURST_ATTRS=10`.
- Targeted `git diff --check` and trailing-whitespace scan passed for patched C#/docs files; Git reported only LF-to-CRLF working-copy warnings.
- Build was not launched; CPU gate reported `CPU_AVERAGE=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="NOISE_QUALITY_SINGLE_OWNER_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <quality-proof>`GenerateMockFractalNoiseJob` owns SDF octave quality collapse through `GlobalQualityWeight`; caller no longer pre-collapses `Octaves`.</quality-proof>
  <remaining-failures>
    <item>Compiler, Unity import, actual bake, Burst Inspector, mesh inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - UI Progress Callback Tail Report

What was wrong:
- The Geology Forge UI progress bar reported only pre/post bake values; per-variation progress existed only in the modal EditorUtility progress UI.

What was done:
- Added an optional cold `Action<float>` progress callback to `BakeProfiles`.
- Updated `GeologyForgeWindow` to pass `SetBakeProgress`, clamp values, mark the progress bar dirty, and repaint per completed bake variation.

Cinematic cheats used:
- None added in this tail patch; it is human-control facade accuracy for the offline bake pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime path changed.
- Editor: O(1) UI update per completed variation; exact responsiveness requires Unity editor execution.

Verification:
- Static scan: `BakeProfiles(..., Action<float>)`, `CountTotalBakes`, and `SetBakeProgress` are present in owned source.
- Static scan: owned GeologyForge source remains clean for forbidden hot-path patterns and direct runtime-coupling symbols.
- Static scan: `JOB_COUNT=10`, `MANDATED_BURST_ATTRS=10`.
- Targeted `git diff --check` and trailing-whitespace scan passed for patched C#/docs files; Git reported only LF-to-CRLF working-copy warnings.
- Build was not launched; CPU gate reported `CPU_AVERAGE=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="UI_PROGRESS_CALLBACK_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <facade-proof>UI Toolkit progress now receives completed-variation progress from the bake generator instead of reporting only 0/1.</facade-proof>
  <remaining-failures>
    <item>Compiler, Unity import, actual bake, UI repaint proof, Burst Inspector, mesh inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Editor Async Batch Runner Tail Report

What was wrong:
- `BakeAll` still invoked a synchronous full-batch call. The progress bar was truthful after the previous patch, but the Editor call stack remained occupied until all variations finished.

What was done:
- Added `BakeProfilesAsync`, driven by `EditorApplication.update`.
- The async runner bakes one profile variation per editor tick, accumulates metrics and manifest records, writes the manifest/report once at finish or cancel, and keeps telemetry TempJob allocation local to each tick.
- Updated the Geology Forge window to call `BakeProfilesAsync` for BAKE SELECTED and BAKE ALL.

Cinematic cheats used:
- None added in this tail patch; it is Editor facade scheduling for the existing offline bake fake.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime path changed.
- Editor: avoids one monolithic UI call stack for large profile libraries; exact responsiveness requires Unity editor execution.

Verification:
- Static scan: `BakeProfilesAsync`, `EditorApplication.update`, per-tick `NativeArray<GeologyBakeTelemetryEntry>`, and window calls to `BakeProfilesAsync` are present.
- Static scan: owned GeologyForge source remains clean for forbidden hot-path patterns and direct runtime-coupling symbols.
- Static scan: `JOB_COUNT=10`, `MANDATED_BURST_ATTRS=10`.
- Static scan: placeholder bake/layout reports remain valid JSON after async-runner report updates.
- Targeted `git diff --check` and trailing-whitespace scan passed for patched C#/docs files; Git reported only LF-to-CRLF working-copy warnings.
- Build was not launched; CPU gate reported `CPU_AVERAGE=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="EDITOR_ASYNC_BATCH_RUNNER_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <facade-proof>`BakeProfilesAsync` advances one variation per `EditorApplication.update`; UI buttons no longer call the monolithic batch path.</facade-proof>
  <native-lifetime-proof>Async runner does not hold a NativeArray field across editor frames; each telemetry ring is TempJob-local to one tick and disposed in `finally`.</native-lifetime-proof>
  <remaining-failures>
    <item>Compiler, Unity import, actual bake, UI responsiveness proof, Burst Inspector, mesh inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Async Bake Cancel Guard Tail Report

What was wrong:
- The async editor batch runner can span multiple editor updates while `AssetDatabase.StartAssetEditing` is open. A domain reload or operator abort needed an explicit close path.

What was done:
- Added `CancelAsyncBake`.
- Registered cancel with `AssemblyReloadEvents.beforeAssemblyReload`.
- Added a UI Toolkit `Cancel Bake` button that routes through the same finish/cleanup path.

Cinematic cheats used:
- None added in this tail patch; it is editor lifetime hygiene for the offline bake pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime path changed.
- Editor: prevents asset-editing scope leakage on cancel/reload; exact abort behavior requires Unity editor execution.

Verification:
- Static scan: `CancelAsyncBake`, `AssemblyReloadEvents.beforeAssemblyReload`, `EditorApplication.update -= TickAsyncBake`, `AssetDatabase.StopAssetEditing`, and `Cancel Bake` button are present.
- Static scan: owned GeologyForge source remains clean for forbidden hot-path patterns and direct runtime-coupling symbols.
- Static scan: `JOB_COUNT=10`, `MANDATED_BURST_ATTRS=10`.
- Static scan: placeholder bake/layout reports remain valid JSON after cancel-guard report updates.
- Targeted `git diff --check` and trailing-whitespace scan passed for patched C#/docs files; Git reported only LF-to-CRLF working-copy warnings.
- Build was not launched; CPU gate reported `CPU_AVERAGE=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="ASYNC_BAKE_CANCEL_GUARD_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <lifetime-proof>`CancelAsyncBake` unsubscribes the update runner, clears progress UI, stops asset editing, writes partial artifacts, resets UI progress to 0, and clears static batch state.</lifetime-proof>
  <remaining-failures>
    <item>Compiler, Unity import, actual bake, cancel/reload proof, Burst Inspector, mesh inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Async Menu Sync Path Purge Tail Report

What was wrong:
- The Geology Forge window used `BakeProfilesAsync`, but `HECTON-8/Geology Forge/Bake CSV Profiles` still called the monolithic batch method.
- That left a blocking operator path for large CSV libraries even after the UI facade was made async.

What was done:
- Removed the public synchronous `BakeProfiles` batch method from `GeologyForgeGenerator`.
- Routed `BakeCsvProfilesMenu` through `BakeProfilesAsync`.
- Kept duplicate/empty menu requests fail-closed through the async guard.

Cinematic cheats used:
- None added in this tail patch; this preserves the existing offline bake fake and removes a synchronous editor-control path.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: prevents one monolithic menu call stack for CSV batches; exact responsiveness requires Unity editor execution.

Verification:
- Static scan: `SYNC_BAKEPROFILES_CALLS=none`.
- Static scan: `GeologyForgeGenerator.cs BRACES=112/112`.
- Static scan: menu and window entrypoints now call `BakeProfilesAsync`.
- Build was not launched; CPU gate reported `CPU_AVERAGE=94`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="ASYNC_MENU_SYNC_PATH_PURGE_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <facade-proof>`BakeCsvProfilesMenu`, `BakeSelected`, and `BakeAll` now route through `BakeProfilesAsync`; the public monolithic batch method is no longer present in owned source.</facade-proof>
  <remaining-failures>
    <item>Compiler, Unity import, actual bake, UI responsiveness proof, Burst Inspector, mesh inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Editor Preview Hook Lifetime Tail Report

What was wrong:
- `GeologyForgePreview` subscribed `SceneView.duringSceneGui` in its static constructor.
- Closing the Forge window only cleared preview points; the idle SceneView callback stayed registered.
- `BakeSelected` and `BakeAll` ignored a rejected async start, leaving stale progress possible when a batch was already active.

What was done:
- Added `GeologyForgePreview.Shutdown` and routed `OnDisable` through it.
- Added `EnsureSubscribed` so preview drawing subscribes only after a preview is built.
- Routed window bake buttons through `TryStartBake`; rejected starts reset progress and log a cold editor warning.

Cinematic cheats used:
- None added in this tail patch; this is editor facade lifetime hygiene for the SDF point-cloud preview.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: removes an idle SceneView delegate after the Forge window closes; exact repaint gain is negligible and requires Unity editor measurement.

Verification:
- Static scan: `GeologyForgeWindow.cs BRACES=38/38`.
- Static scan: `TryStartBake`, `GeologyForgePreview.Shutdown`, `EnsureSubscribed`, and paired `SceneView.duringSceneGui +=/-=` are present.
- Build was not launched; CPU gate reported `CPU_AVERAGE=71`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="EDITOR_PREVIEW_HOOK_LIFETIME_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <lifetime-proof>SceneView preview subscribes after an explicit preview build and unsubscribes when the Forge window disables.</lifetime-proof>
  <remaining-failures>
    <item>Compiler, Unity import, actual preview lifecycle proof, UI responsiveness proof, Burst Inspector, mesh inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Bounds NaN Vaccination Tail Report

What was wrong:
- `CalculateBounds` seeded min/max from `vertices[0].Position` before checking `math.isfinite`.
- If the first raw row was non-finite, mesh bounds and `.h8geom` manifest bounds could become NaN even when later rows were valid.

What was done:
- Reworked bounds accumulation to initialize from the first finite raw position.
- Non-finite rows are skipped.
- If every row is non-finite, the generator emits the existing finite 1m fallback bound.

Cinematic cheats used:
- None added in this tail patch. This is payload hygiene for the offline geometry fake already in place.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: adds one boolean branch per raw vertex during mesh creation; cost is negligible compared with SDF/AO jobs. The saved cost is avoided downstream invalid-culling/debug work, not a claimed frame-time delta.

Verification:
- Static scan: `GeologyForgeGenerator.cs BRACES=113/113`.
- Static scan: `CalculateBounds` contains `hasFinitePosition` and two finite fallback exits.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Build was not launched; CPU gate reported `CPU_AVERAGE=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="BOUNDS_NAN_VACCINATION_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <payload-proof>`CalculateBounds` now derives mesh/submesh/manifest bounds from finite source positions only and falls back to finite local bounds when no valid row exists.</payload-proof>
  <remaining-failures>
    <item>Compiler, Unity import, actual bake, manifest audit, mesh inspector, Burst Inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Async Finish State Hardening Tail Report

What was wrong:
- `FinishAsyncBake` cleared static async state only after manifest/report writes and progress callback invocation.
- A `.h8geom` write/import fault, report write fault, or UI callback fault could leave `_asyncProfiles` non-null and reject all later bake starts.

What was done:
- Wrapped async finish artifact work in `try/finally`.
- The `finally` block now clears profiles, metrics, manifest records, callback, counters, save flag, and asset-editing flag regardless of finish-path exceptions.

Cinematic cheats used:
- None added in this tail patch. This is editor-lifetime hardening for the offline bake lane.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: adds one `try/finally` at batch finish; prevents manual domain reload/editor restart after finish-path faults. Exact recovery time saved depends on operator session.

Verification:
- Static scan: `GeologyForgeGenerator.cs BRACES=115/115`.
- Static scan: `FinishAsyncBake` contains artifact writes inside `try` and state reset inside `finally`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Build was not launched; CPU gate reported `CPU_AVERAGE=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="ASYNC_FINISH_STATE_HARDENING_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <lifetime-proof>`FinishAsyncBake` now clears static runner ownership state in `finally`, so finish-path exceptions do not permanently block later bakes.</lifetime-proof>
  <remaining-failures>
    <item>Compiler, Unity import, exception-path editor proof, actual bake, manifest audit, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Burst Finite Guard Tail Report

What was wrong:
- Final Burst kernels still trusted upstream finite vectors in triplanar UVs, AO nearest sampling, LOD snapping, and UV packing.
- A malformed profile/noise edge case could push NaN lanes into UV or snapped position math before the final packed stream guard.

What was done:
- `GenerateTriplanarUvsJob` now substitutes finite normal and position fallbacks before projection.
- `BakeVertexOcclusionJob.SampleDensityNearest` treats non-finite sample positions as empty space.
- `GeologyLodDecimationJob.Snap` zeros poisoned positions before returning.
- `GeologyPackVertexJob.PackUnorm16` maps non-finite UVs to zero before UNorm16 packing.

Cinematic cheats used:
- None added in this tail patch. This is NaN vaccination for the existing offline visual-fake pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: adds small finite predicates in final jobs. The avoided cost is corrupted bake/manifest recovery, not a claimed frame-time improvement.

Verification:
- Static scan: `GeologyForgeJobs.cs BRACES=75/75`.
- Static scan: finite guards are present in triplanar UV input, AO nearest sampling, LOD snap, and UV packing.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Build was not launched; CPU gate reported `CPU_AVERAGE=100`, `BUILD_PROCS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="BURST_FINITE_GUARD_TAIL" status="STATIC_PATCH_BUILD_NOT_RUN">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge Burst jobs and SHINOBU_208 docs/logs.</runtime-boundary>
  <math-proof>Final UV/AO/LOD/pack jobs now sanitize non-finite vector lanes locally before writing raw or packed payload data.</math-proof>
  <remaining-failures>
    <item>Compiler, Unity import, Burst Inspector, actual bake, manifest audit, mesh inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Artifact Failure Hardening Tail Report

What was wrong:
- `CreateUnityMesh` could retain a transient Unity `Mesh` if upload, submesh assignment, validation, or upload-finalization threw after allocation.
- A zero-output async cancel could rewrite the previous `.h8geom` manifest and bake report with empty artifacts.

What was done:
- `CreateUnityMesh` now destroys the transient mesh in `finally` unless successful return transfers ownership.
- `FinishAsyncBake` now writes manifest/report artifacts on cancel only after at least one metrics row or manifest record exists.

Cinematic cheats used:
- None added in this tail patch. This is editor payload-lifetime hardening for the existing offline Dear Lie mesh bake path.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: adds O(1) null/artifact guards. It avoids native mesh retention and bad asset churn during failed or canceled authoring passes; exact editor recovery time depends on the failing batch.

Verification:
- Static scan: `GeologyForgeGenerator.cs BRACES=115/115`.
- Static scan: `CreateUnityMesh` has explicit mesh ownership transfer and `DestroyImmediate(mesh)` in the cleanup path.
- Static scan: `FinishAsyncBake` has `hasMetrics`, `hasManifestRecords`, and `shouldWriteArtifacts` guards around manifest/report writes.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- CPU samples reported `CPU_AVERAGE=38` and `CPU_AVERAGE=31`, `BUILD_PROCS=none`; build was still not launched because no generated `Hecton8.World.OfflineGeology.Editor.csproj` exists and current `.csproj`/`.sln` files do not reference GeologyForge.
- Post-documentation static gate reported balanced braces for the four owned C# files, `TRAILING_WS=none`, `PROJECT_TARGET_HITS=none`, and `CPU_AVERAGE=70` with active `csc,dotnet`; no build command was legal or useful.

<SELF_AUDIT agent="SHINOBU_208" pass="ARTIFACT_FAILURE_HARDENING_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <lifetime-proof>`CreateUnityMesh` destroys failed transient meshes; `FinishAsyncBake` preserves previous artifacts when a canceled batch produced zero output.</lifetime-proof>
  <remaining-failures>
    <item>Compiler, Unity import, exception-path editor proof, zero-output cancel proof, actual bake, manifest audit, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Atomic Payload And LUT Winding Tail Report

What was wrong:
- `.h8geom`, dump, bake report, layout audit, and scanner report writes could overwrite the previous proof artifact before the replacement write completed.
- Complement tetra LUT cases reused same-order edge sequences, risking inverted winding/backface holes.
- `GeologyVertexLayoutValidator.GetLayout()` exposed the mutable static descriptor array.

What was done:
- Binary payload writers now write `.tmp` files with `FileMode.CreateNew`, then replace final files through `File.Replace` with `.bak` preservation when a previous artifact exists.
- JSON report writers now use the same `.tmp` plus replace policy.
- Complement LUT cases now reverse triangle winding, and `ValidateComplementWinding()` runs from `ValidateStruct()`.
- `GetLayout()` now returns a fresh four-descriptor copy.

Cinematic cheats used:
- None added in this tail patch. This protects the offline visual-fake payload artifacts and triangle presentation contract.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: adds one temp write/replacement per artifact, a 14-case LUT check, and a four-descriptor copy per mesh upload. These are cold authoring costs that prevent corrupted evidence files and backface artifact rebakes.

Verification:
- Static scan: `GeologyForgeGenerator.cs BRACES=125/125`, `GeologyForgeJobs.cs BRACES=79/79`, `GeologyForgeSelfAudit.cs BRACES=37/37`, `GeologyVertexLayoutValidator.cs BRACES=35/35`.
- Static scan: `FileMode.CreateNew`, `File.Replace`, `.bak` preservation, `ValidateComplementWinding()`, and fresh `GetLayout()` copy are present.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- CPU gate reported `CPU_AVERAGE=11`, `BUILD_PROCS=none`; build was still not launched because project target scan reported `PROJECT_TARGET_HITS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="ATOMIC_PAYLOAD_LUT_WINDING_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge source and SHINOBU_208 docs/logs.</runtime-boundary>
  <payload-proof>Primary evidence payload writers now commit through temp files and keep prior artifacts as `.bak`; tetra complement winding is self-validated before layout checks.</payload-proof>
  <remaining-failures>
    <item>Compiler, Unity import, IO-fault proof, actual bake, manifest audit, mesh inspector, Burst Inspector, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - CSV Schema Fail-Closed Tail Report

What was wrong:
- `GeologyProfileCsv` detected `iso_level` but then trusted positional parsing for all other fields.
- Reordered or missing columns could silently map seed, quality, LOD, or AUP data into the wrong fields.

What was done:
- Added byte-level header validation for the exact supported SHINOBU_208 schema, with and without `iso_level`.
- Invalid headers now throw `InvalidDataException` before any row is parsed.

Cinematic cheats used:
- None added in this tail patch. This protects the human-readable designer bridge feeding the offline bake fake.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: adds one header-token pass over about 20 columns; prevents corrupt multi-variation bake batches caused by malformed CSV headers.

Verification:
- Static scan: `GeologyProfileCsv.cs BRACES=41/41`.
- Static scan: `HeaderMatchesExpectedSchema`, `ExpectedHeaderToken`, `TokenEquals`, `InvalidDataException`, and `ReadUInt` are present.
- CSV schema sanity: header and all three rows have 20 columns.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="CSV_SCHEMA_FAIL_CLOSED_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge CSV ingestion and SHINOBU_208 docs/logs.</runtime-boundary>
  <designer-bridge-proof>CSV headers are validated byte-for-byte before rows hydrate unmanaged bake profiles; malformed schema stops the editor bake instead of corrupting payload facts.</designer-bridge-proof>
  <remaining-failures>
    <item>Compiler, Unity import, malformed-header editor proof, actual bake, manifest audit, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Asset Edit Scope Tail Report

What was wrong:
- `BakeProfilesAsync` opened `AssetDatabase.StartAssetEditing()` before subscribing the editor update runner.
- That meant a multi-profile async batch could keep the asset database edit scope open across many editor updates until finish/cancel.

What was done:
- Removed the batch-wide edit scope.
- Each async tick opens `StartAssetEditing()` only around the current variation's saved mesh tranche and closes it on success or exception before telemetry/report handling continues.

Cinematic cheats used:
- None added in this tail patch. This is editor transaction hardening for the offline bake pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: may add small asset-edit transition overhead per variation; buys bounded lock scope and safer cancel/domain-reload behavior.

Verification:
- Static scan: `GeologyForgeGenerator.cs BRACES=128/128`.
- Static scan: batch setup no longer calls `StartAssetEditing`; local async tick save tranche has paired `StartAssetEditing()`/`StopAssetEditing()` success and exception paths.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- CPU gate reported `CPU_AVERAGE=13`, `BUILD_PROCS=none`; build was still not launched because project target scan reported `PROJECT_TARGET_HITS=none`.
- Final static gate after sub-agent response reported targeted `git diff --check` with only LF-to-CRLF warnings, `TRAILING_WS=none`, CPU `CPU_AVERAGE=20`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no dotnet build target exists for the new asmdef lane yet.

<SELF_AUDIT agent="SHINOBU_208" pass="ASSET_EDIT_SCOPE_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge async bake source and SHINOBU_208 docs/logs.</runtime-boundary>
  <lifetime-proof>Asset database edit scope is bounded to one variation save tranche instead of the whole multi-frame async batch.</lifetime-proof>
  <remaining-failures>
    <item>Compiler, Unity import, cancel/domain-reload editor proof, full staged job scheduler proof, actual bake, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Scanner Time-Slice Tail Report

What was wrong:
- `RuntimeMeshGenerationScanner.ScanAndWriteReport()` scanned the full World/Environment target set synchronously from the non-batch menu/window path.
- That could freeze the Unity editor during a source audit, even though the scanner is only an editor proof tool.

What was done:
- Non-batch scan requests now start an `EditorApplication.update` state machine.
- The async scanner processes files under `AsyncScanBudgetSeconds=0.004`, shows a cancelable progress bar, rejects duplicate starts, and removes the update hook on completion, cancel, restart, and fault.
- Batch mode keeps the synchronous report path so CI/static scripts still receive one immediate report.

Cinematic cheats used:
- None added in this tail patch. This is authoring-tool scheduling hardening for the static runtime topology audit.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: full scan cost is redistributed across editor ticks with an intended 4 ms per-update budget after file enumeration. Unity editor execution proof remains pending.

Verification:
- Static scan: `RuntimeMeshGenerationScanner.cs` contains `StartAsyncScan`, `TickAsyncScan`, `CancelAsyncScan`, `AsyncScanBudgetSeconds=0.004`, and paired `EditorApplication.update -= TickAsyncScan` cleanup paths.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- CPU gate reported `CPU_AVERAGE=11` but `BUILD_PROCS=dotnet`; no build was launched. Project target scan still reported `PROJECT_TARGET_HITS=none`.
- Final touched-file gate after docs reported `TRAILING_WS=none`, targeted `git diff --check` with only LF-to-CRLF warnings, scanner lifecycle hooks present, `CPU_AVERAGE=8`, active `BUILD_PROCS=dotnet`, and `PROJECT_TARGET_HITS=none`.
- Compile-risk alias check: `RuntimeMeshGenerationScanner.cs` now aliases `Debug = UnityEngine.Debug` while using `System.Diagnostics.Stopwatch`; targeted whitespace/diff gates stayed clean apart from LF-to-CRLF warnings. CPU remained `CPU_AVERAGE=8` with active `BUILD_PROCS=dotnet`, so no build was launched.

<SELF_AUDIT agent="SHINOBU_208" pass="SCANNER_TIME_SLICE_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge scanner source and SHINOBU_208 docs/logs.</runtime-boundary>
  <scanner-proof>Non-batch runtime mesh audits are time-sliced through editor update; batch-mode CI scans remain synchronous and deterministic.</scanner-proof>
  <remaining-failures>
    <item>Compiler, Unity import, editor progress/cancel execution proof, scanner report refresh through the new async path, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Scanner Discovery Slice Tail Report

What was wrong:
- The non-batch scanner was time-sliced after startup, but startup still called `CollectScanFiles()`.
- `CollectScanFiles()` uses recursive `Directory.GetFiles(... SearchOption.AllDirectories)`, so a large source tree could still freeze the editor before the first 4 ms update budget.

What was done:
- Non-batch scanning now seeds root directories and direct files only.
- `TickAsyncScan()` alternates source-file scans with one-directory expansions through `ExpandNextAsyncDirectory()`.
- Directory expansion uses `SearchOption.TopDirectoryOnly` and the progress bar now uses static `AsyncScanProgressMessage` instead of per-tick string concatenation.
- Batch mode keeps the synchronous `Scan()` path for CI/static report determinism.

Cinematic cheats used:
- None added in this tail patch. This is authoring-tool scheduling hardening for the static runtime topology audit.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: removes full recursive discovery from non-batch startup. Remaining scan/discovery work is distributed across editor ticks with the existing 4 ms budget; Unity editor execution proof remains pending.

Verification:
- Static scan: non-batch `StartAsyncScan()` no longer calls `CollectScanFiles()`; `TickAsyncScan()` calls `ExpandNextAsyncDirectory()` and `FinishAsyncScan()`.
- Static scan: recursive `SearchOption.AllDirectories` remains only in `CollectScanFiles()` for synchronous batch/CI `Scan()`.
- Static scan: progress bar uses `AsyncScanProgressMessage`; no per-tick `"Scanned " + ...` progress string remains.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- CPU gates reported `CPU_AVERAGE=62` then `CPU_AVERAGE=37`; no build was launched because `PROJECT_TARGET_HITS=none` still means dotnet would not verify this asmdef lane before Unity regeneration/import.
- Final scanner discovery gate reported `TRAILING_WS=none`, targeted `git diff --check` with only LF-to-CRLF warnings, `SearchOption.AllDirectories` confined to the synchronous batch `CollectScanFiles()` path, `PROJECT_TARGET_HITS=none`, `CPU_AVERAGE=53`, and `BUILD_PROCS=none`; no compiler command was launched.

<SELF_AUDIT agent="SHINOBU_208" pass="SCANNER_DISCOVERY_SLICE_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge scanner source and SHINOBU_208 docs/logs.</runtime-boundary>
  <scanner-proof>Non-batch runtime mesh audits now slice both discovery and file scanning through editor update; batch-mode CI scans remain synchronous and deterministic.</scanner-proof>
  <remaining-failures>
    <item>Compiler, Unity import, editor progress/cancel execution proof, async scanner report refresh, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Async Bake Static Progress Tail Report

What was wrong:
- `TickAsyncBake()` rebuilt the cancelable progress-bar message every editor update using profile-name `ToString()` and interpolation.
- This was editor-only, but it kept unnecessary managed formatting in the active async bake update hook.

What was done:
- Added static `AsyncBakeProgressTitle` and `AsyncBakeProgressMessage` constants.
- Routed `EditorUtility.DisplayCancelableProgressBar` through those constants while preserving numeric progress and cancel behavior.

Cinematic cheats used:
- None added in this tail patch. This is authoring-loop allocation hygiene for the offline bake facade.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: removes profile-name conversion and interpolated message allocation from each async bake update tick. Unity Profiler allocation proof remains pending.

Verification:
- Static scan: `GeologyForgeGenerator.cs` contains `AsyncBakeProgressTitle`, `AsyncBakeProgressMessage`, and `DisplayCancelableProgressBar(AsyncBakeProgressTitle, AsyncBakeProgressMessage, progress)`.
- Static scan: `GeologyForgeGenerator.cs BRACES=125/125`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_AVERAGE=69`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched.
- Compiler, Unity import, and Profiler allocation proof remain pending because the generated project target for `Hecton8.World.OfflineGeology.Editor.asmdef` is still absent and the CPU rule blocks build launch.

<SELF_AUDIT agent="SHINOBU_208" pass="ASYNC_BAKE_STATIC_PROGRESS_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge async bake source and SHINOBU_208 docs/logs.</runtime-boundary>
  <allocation-proof>Cancelable progress text is no longer rebuilt per editor update; the update hook now passes static strings and a float progress scalar.</allocation-proof>
  <remaining-failures>
    <item>Compiler, Unity import, editor progress/cancel execution proof, and Unity Profiler allocation proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Async Variation Count Saturation Tail Report

What was wrong:
- `_asyncTotalBakes` was computed from raw profile `Variations`.
- Actual execution sanitized the profile later, so malformed CSV/UI values could make progress denominators diverge or overflow before the clamp was applied.

What was done:
- Added `SanitizeVariationCount()`.
- Routed both `SanitizeProfile()` and `CountTotalBakes()` through the same 1..500 clamp.
- Added aggregate overflow saturation in `CountTotalBakes()` instead of allowing integer wrap.

Cinematic cheats used:
- None added in this tail patch. This is deterministic authoring-control math for the offline bake facade.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: one clamp per profile during async setup; prevents malformed authoring input from generating corrupt progress math or runaway bake totals.

Verification:
- Static scan: `SanitizeVariationCount()` is present and consumed by both `SanitizeProfile()` and `CountTotalBakes()`.
- Static scan: `GeologyForgeGenerator.cs BRACES=127/127`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_AVERAGE=31`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because dotnet still has no generated target for this asmdef lane.
- Compiler, Unity import, malformed-CSV execution proof, and editor progress proof remain pending.

<SELF_AUDIT agent="SHINOBU_208" pass="ASYNC_VARIATION_SATURATION_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge async bake source and SHINOBU_208 docs/logs.</runtime-boundary>
  <determinism-proof>Async setup and async execution now use one variation-count clamp route, and total progress count saturates before integer wrap.</determinism-proof>
  <remaining-failures>
    <item>Compiler, Unity import, malformed-CSV execution proof, and editor progress proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Preview Fake-Async Fence Tail Report

What was wrong:
- `GeologyForgePreview.Build()` called `Schedule(count, 64).Complete()` for the lightweight SceneView preview SDF job.
- The source file also lacked the explicit `Unity.Jobs` import for the job extension API.

What was done:
- Added `using Unity.Jobs`.
- Replaced the preview-only immediate schedule/fence with `Run(count)` over the bounded 24^3 preview grid.
- Kept preview scratch as a method-local `NativeArray<float>` disposed in `finally`; no long-lived private native buffer was introduced.

Cinematic cheats used:
- The preview remains the intended Dear Lie: a point-cloud SDF probe instead of full mesh extraction, LOD generation, mesh upload, or AO bake.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: removes job scheduler overhead and an immediate completion fence from the preview button path. Exact editor timing remains pending Unity execution.

Verification:
- Static scan: `GeologyForgeWindow.cs` contains `using Unity.Jobs` and `.Run(count)`.
- Static scan: no `Schedule(count, 64).Complete()` remains in `GeologyForgeWindow.cs`.
- Static scan: `GeologyForgeWindow.cs BRACES=38/38`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Compiler, Unity import, and SceneView preview timing proof remain pending.

<SELF_AUDIT agent="SHINOBU_208" pass="PREVIEW_FAKE_ASYNC_FENCE_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge window/preview source and SHINOBU_208 docs/logs.</runtime-boundary>
  <job-route-proof>Preview SDF generation uses a bounded cold `Run(count)` route instead of `Schedule().Complete()` fake async, and scratch memory remains method-local.</job-route-proof>
  <remaining-failures>
    <item>Compiler, Unity import, SceneView preview execution proof, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Shared Variation Ceiling Tail Report

What was wrong:
- The generator clamped variation counts to 500, but the UI Toolkit field only enforced a lower bound.
- Bad CSV/manual field values could display one count while the async runner executed a different sanitized count.

What was done:
- Added `GeologyForgeConstants.MaximumVariations=500`.
- Routed generator sanitization, async total-count math, UI field display, and UI field resolve through the same 1..500 range.

Cinematic cheats used:
- None added in this tail patch. This is human-control/facade consistency for the offline bake pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: one clamp on field display/resolve; prevents accidental oversized batch requests from malformed authoring input.

Verification:
- Static scan: `MaximumVariations` is present in `GeologyForgeConstants` and referenced by both `GeologyForgeGenerator` and `GeologyForgeWindow`.
- Static scan: `GeologyForgeTypes.cs BRACES=10/10`, `GeologyForgeGenerator.cs BRACES=127/127`, `GeologyForgeWindow.cs BRACES=39/39`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_AVERAGE=38`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because dotnet still has no generated target for this asmdef lane.
- Compiler, Unity import, and UI field execution proof remain pending.

<SELF_AUDIT agent="SHINOBU_208" pass="SHARED_VARIATION_CEILING_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge types/generator/window source and SHINOBU_208 docs/logs.</runtime-boundary>
  <facade-proof>The designer-facing variation field and generator execution now share one maximum variation constant and one effective clamp range.</facade-proof>
  <remaining-failures>
    <item>Compiler, Unity import, UI field execution proof, and profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Async Result Preallocation Tail Report

What was wrong:
- `_asyncMetrics` and `_asyncManifestRecords` still used `profiles.Count * 4` as initial capacity.
- With the shared variation ceiling now at 500, assignment-scale 500 to 5000-rock bakes could grow managed list backing arrays during the editor-update runner.

What was done:
- Moved `_asyncTotalBakes = CountTotalBakes(_asyncProfiles)` before result-list allocation.
- Added `GeologyForgeConstants.MaximumAsyncResultPreallocation=5000`.
- Added `ResolveAsyncResultCapacity()` and used it for async metrics and manifest record list capacities.
- Set `_asyncManifestRecords` to `null` for non-asset async probes so no unused manifest list is allocated.

Cinematic cheats used:
- None added in this tail patch. This is cold editor memory hygiene for the offline forge.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: removes expected `List<T>` growth/copy churn for assignment-scale async bakes up to 5000 results. Exact allocation and time proof remain pending Unity Profiler.

Verification:
- Static scan: `MaximumAsyncResultPreallocation`, `ResolveAsyncResultCapacity`, and sanitized `_asyncTotalBakes` preallocation are present.
- Static scan: no `profiles.Count * 4` remains in `GeologyForgeGenerator.cs`.
- Static scan: `GeologyForgeTypes.cs BRACES=10/10`, `GeologyForgeGenerator.cs BRACES=128/128`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_AVERAGE=16`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because dotnet still has no generated target for this asmdef lane.
- Compiler, Unity import, and Unity Profiler allocation proof remain pending because the generated project target for `Hecton8.World.OfflineGeology.Editor.asmdef` is still absent.

<SELF_AUDIT agent="SHINOBU_208" pass="ASYNC_RESULT_PREALLOCATION_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge types/generator source and SHINOBU_208 docs/logs.</runtime-boundary>
  <allocation-proof>Async result lists now preallocate from sanitized total bakes up to 5000 instead of profile count multiplied by a stale literal.</allocation-proof>
  <failure-boundary>Pathological malformed totals remain capped before initial allocation; execution still uses the existing 1..500 per-profile variation clamp.</failure-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, editor batch execution proof, and Unity Profiler allocation proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Editor Request Reuse And Preview Sampling Tail Report

What was wrong:
- The Geology Forge window allocated a fresh `List<GeologyBakeProfile>` for `BAKE SELECTED` and another for `BAKE ALL`.
- The SceneView point-cloud preview filled its fixed point buffer from the first near-surface SDF samples in linear grid order, which biased the visual fake toward one region.

What was done:
- Added one reusable `_bakeRequestProfiles` list to the Editor window facade.
- Routed selected/all bake button requests through that list; `BakeProfilesAsync` still copies the incoming profiles immediately before the facade can reuse the list.
- Changed the preview to count all near-surface candidates first, then deterministically stride through candidates into the fixed 2048-point buffer.

Cinematic cheats used:
- The preview remains a bounded SDF point cloud. It does not run full mesh extraction, LOD decimation, vertex AO, mesh upload, prefab generation, or runtime simulation.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: removes transient list allocations from bake button requests after initial window construction. Preview adds one bounded 13,824-sample count pass to buy representative visual coverage while staying far cheaper than a full bake. Exact allocation/timing proof remains pending Unity Profiler.

Verification:
- Static source patch is present in `GeologyForgeWindow.cs`.
- Static scan: `GeologyForgeWindow.cs BRACES=42/42`.
- Static scan: `var bakeList` no longer exists, and `new List<GeologyBakeProfile>` hits are only the persistent window fields.
- Static scan: touched-file trailing whitespace reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_AVERAGE=29`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because dotnet still has no generated target for this asmdef lane.
- Compiler, Unity import, SceneView execution, and Profiler allocation proof are still pending after this tail patch.

<SELF_AUDIT agent="SHINOBU_208" pass="EDITOR_REQUEST_REUSE_PREVIEW_SAMPLING_TAIL" status="STATIC_PATCH_VERIFICATION_PENDING">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge window/preview source and SHINOBU_208 docs/logs.</runtime-boundary>
  <allocation-proof>`BAKE SELECTED` and `BAKE ALL` reuse one editor-owned list instead of allocating request lists per click.</allocation-proof>
  <dear-lie-proof>Preview remains a point-cloud SDF fake and now samples the whole near-surface candidate set deterministically before truncating to 2048 points.</dear-lie-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, SceneView preview execution proof, and Unity Profiler allocation proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Caller-Owned CSV Profile Lists Tail Report

What was wrong:
- `GeologyProfileCsv.LoadProfiles()` returned a fresh `List<GeologyBakeProfile>`.
- `GeologyForgeWindow.ReloadProfiles()` immediately copied that fresh list into `_profiles`.
- The menu bake path created another short-lived profile list before the async runner copied profiles into `_asyncProfiles`.

What was done:
- Added `GeologyProfileCsv.LoadProfiles(List<GeologyBakeProfile>)`, which clears and fills caller-owned storage while preserving default-profile fallback behavior.
- Routed the UI reload path directly into `_profiles`.
- Added static `_menuProfiles` for the menu bake command and passed it to `BakeProfilesAsync`; the async runner still snapshots profiles before the menu list can be reused.

Cinematic cheats used:
- None added in this tail patch. This is editor facade allocation hygiene for the existing offline Dear Lie pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: removes one transient profile-list allocation and copy loop per CSV reload, and one transient menu-list allocation per menu bake after static initialization. Unity Profiler allocation proof remains pending.

Verification:
- Static scan: `GeologyProfileCsv.cs BRACES=42/42`, `GeologyForgeWindow.cs BRACES=40/40`, `GeologyForgeGenerator.cs BRACES=128/128`.
- Static scan: no `List<GeologyBakeProfile> loaded` remains in the window path.
- Static scan: menu/window routes call `GeologyProfileCsv.LoadProfiles(_menuProfiles)` and `GeologyProfileCsv.LoadProfiles(_profiles)`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_AVERAGE=52`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because CPU was above the local rule and dotnet still has no generated target for this asmdef lane.
- Compiler, Unity import, UI reload execution, menu bake execution, and Unity Profiler allocation proof remain pending.

<SELF_AUDIT agent="SHINOBU_208" pass="CALLER_OWNED_CSV_PROFILE_LISTS_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge CSV/window/generator source and SHINOBU_208 docs/logs.</runtime-boundary>
  <allocation-proof>CSV reload and menu bake now reuse caller-owned lists instead of allocating throwaway profile containers before the async snapshot.</allocation-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, UI reload/menu execution proof, and Unity Profiler allocation proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - CSV Variation Ceiling Constant Tail Report

What was wrong:
- CSV parsing still clamped `variations` with a literal `500`.
- Generator execution, async total math, and UI field resolution already used `GeologyForgeConstants.MaximumVariations`, so imported authoring truth had a drift point.

What was done:
- Replaced the CSV literal with `GeologyForgeConstants.MaximumVariations`.
- Updated SHINOBU_208 rationale and architecture notes to record that CSV, UI, async totals, and generator execution share one ceiling.

Cinematic cheats used:
- None added in this tail patch. This is authoring truth consolidation for the offline bake pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: 0 us expected; this removes split-authority maintenance risk, not a hot-path cost.

Verification:
- Static scan: `GeologyProfileCsv.cs BRACES=42/42`, `GeologyForgeWindow.cs BRACES=40/40`, `GeologyForgeGenerator.cs BRACES=128/128`, `GeologyForgeTypes.cs BRACES=10/10`.
- Static scan: no CSV `variations` clamp with literal `500` remains.
- Static scan: `MaximumVariations` is referenced by CSV parsing, UI field resolution, and generator sanitization.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_AVERAGE=100`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because CPU violated the local rule and dotnet still has no generated target for this asmdef lane.
- Compiler, Unity import, malformed-CSV execution proof, and Unity Profiler proof remain pending.

<SELF_AUDIT agent="SHINOBU_208" pass="CSV_VARIATION_CEILING_CONSTANT_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge CSV source and SHINOBU_208 docs/logs.</runtime-boundary>
  <truth-route-proof>CSV import, UI display/resolve, async total counting, and generator execution now consume one `MaximumVariations` constant.</truth-route-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, malformed-CSV execution proof, and Unity Profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Return-Allocated CSV Loader Removal Tail Report

What was wrong:
- After caller-owned CSV loading was introduced, the old internal `GeologyProfileCsv.LoadProfiles()` wrapper still existed.
- That wrapper allocated a fresh `List<GeologyBakeProfile>` and preserved a stale facade path even though no owned source used it.

What was done:
- Removed the return-value loader.
- `GeologyProfileCsv.LoadProfiles(List<GeologyBakeProfile>)` is now the only CSV ingestion API in the owned editor assembly.

Cinematic cheats used:
- None added in this tail patch. This is editor authoring API hardening for the offline bake lane.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: no active measured path changed after the previous caller-owned patch, but the stale API can no longer reintroduce one transient profile-list allocation and copy route per reload/menu bake.

Verification:
- Static scan shows no declaration or call of return-value `GeologyProfileCsv.LoadProfiles()`.
- Static scan shows only caller-owned `_profiles` and `_menuProfiles` CSV ingestion call sites.
- Static scan: `GeologyProfileCsv.cs BRACES=41/41`, `GeologyForgeWindow.cs BRACES=40/40`, `GeologyForgeGenerator.cs BRACES=128/128`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_AVERAGE=100`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because CPU violated the local rule and dotnet still has no generated target for this asmdef lane.

<SELF_AUDIT agent="SHINOBU_208" pass="RETURN_ALLOCATED_CSV_LOADER_REMOVAL_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge CSV source and SHINOBU_208 docs/logs.</runtime-boundary>
  <allocation-proof>The return-allocated CSV loader no longer exists; all owned CSV ingestion must supply caller-owned storage.</allocation-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, UI reload/menu execution proof, and Unity Profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Validator Layout Apply And Preview Hook Tail Report

What was wrong:
- `GeologyVertexLayoutValidator.GetLayout()` allocated a fresh four-element descriptor array for every mesh upload.
- `GeologyForgePreview.Build()` subscribed `SceneView.duringSceneGui` before preview density allocation, preview SDF execution, and point-buffer population were proven.

What was done:
- Replaced `GetLayout()` with `ApplyVertexBufferParams(Mesh,int)`, keeping the descriptor array private and applying it directly.
- Updated `CreateUnityMesh()` to call the validator-owned apply method.
- Moved preview hook subscription to the successful end of point generation after `_pointCount` is written.

Cinematic cheats used:
- The SceneView preview remains the same bounded SDF point-cloud fake. It still avoids full mesh extraction, AO, LOD decimation, mesh upload, or runtime simulation.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: removes one managed descriptor-array allocation per generated LOD mesh. Preview success path timing is unchanged; fault paths no longer retain a dead draw callback.

Verification:
- Static scan found no `GetLayout()` declaration/call and found `ApplyVertexBufferParams(mesh, vertexCount)` as the mesh upload route.
- Static scan found `EnsureSubscribed()` only after preview buffer fill.
- Static scan reported `GeologyVertexLayoutValidator.cs BRACES=34/34`, `GeologyForgeGenerator.cs BRACES=128/128`, `GeologyForgeWindow.cs BRACES=40/40`, and `GeologyProfileCsv.cs BRACES=41/41`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_AVERAGE=100`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because CPU violated the local rule and dotnet still has no generated target for this asmdef lane.

<SELF_AUDIT agent="SHINOBU_208" pass="VALIDATOR_LAYOUT_APPLY_PREVIEW_HOOK_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge validator/generator/window source and SHINOBU_208 docs/logs.</runtime-boundary>
  <allocation-proof>Mesh upload no longer requests a fresh descriptor-array copy for every generated LOD mesh.</allocation-proof>
  <hook-lifetime-proof>SceneView preview callback registration now follows successful point-buffer generation.</hook-lifetime-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, mesh upload execution proof, preview exception-path proof, and Unity Profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Runtime Scanner Reusable Async Buffers Tail Report

What was wrong:
- The non-batch `RuntimeMeshGenerationScanner` created fresh source-file, directory-stack, and finding lists on every scanner launch.
- Scan active state was encoded through nullable list fields, so lifecycle and allocation state were coupled.

What was done:
- Converted async scanner queues/findings to static readonly reusable lists.
- Added `_asyncScanActive` as the explicit scan sentinel.
- Routed start/cancel/finish cleanup through `ClearAsyncScanState()`.
- Preserved report writing before buffer clear and wrapped finish cleanup in `finally`.

Cinematic cheats used:
- None added in this patch. The scanner remains editor proof tooling; runtime geology still consumes static baked assets and the `.h8geom` manifest.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: removes three list-container allocations per non-batch scanner launch after static initialization. Unity Profiler allocation proof remains pending.

Verification:
- Static scan shows `_asyncFiles`, `_asyncDirectoryStack`, and `_asyncFindings` are static readonly reusable lists.
- Static scan shows no `_asyncFiles = null`, `_asyncFindings == null`, or `_asyncDirectoryStack == null` scanner lifecycle state.
- Static scan shows `FinishAsyncScan()` writes the report before `ClearAsyncScanState()` and clears in `finally`.
- Static scan reported `ASYNC_NULL_STATE=none`.
- Static scan reported scanner raw braces `66/65` because JSON report string literals contain braces; structural closing braces are present at the file tail.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_SAMPLES=100,100,100`, `CPU_AVERAGE=100`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because CPU violated the local rule and dotnet still has no generated target for this asmdef lane.

<SELF_AUDIT agent="SHINOBU_208" pass="RUNTIME_SCANNER_REUSABLE_ASYNC_BUFFERS_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge scanner source and SHINOBU_208 docs/logs.</runtime-boundary>
  <allocation-proof>Non-batch scanner startup no longer allocates three list containers per launch after static initialization.</allocation-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, scanner menu execution proof, and Unity Profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - CSV Row And Numeric Cell Fail-Closed Tail Report

What was wrong:
- CSV headers were fail-closed, but row values still routed through fallback-return numeric readers.
- Empty or malformed numeric cells could silently substitute defaults before generating mesh assets and `.h8geom` records.

What was done:
- Added per-row column-count validation before profile hydration.
- Replaced fallback-return numeric readers with strict byte-level `ReadInt`, `ReadUInt`, and `ReadFloat` readers.
- Added row/column/field `InvalidDataException` messages for malformed cells.
- Positive-only physical fields now throw instead of falling back to safe defaults.

Cinematic cheats used:
- None added in this patch. This protects the human-readable authoring bridge feeding the existing offline SDF/vertex-AO Dear Lie pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: no frame-time saving claimed. The gain is fail-fast authoring safety before expensive mesh/AO/manifest generation.

Verification:
- Static scan reported `GeologyProfileCsv.cs BRACES=43/43`.
- Static scan found `ValidateRowColumnCount`, `ThrowInvalidCell`, strict `ReadInt`/`ReadUInt`/`ReadFloat` calls, and no `SafePositive` or fallback-return numeric reader path.
- Static CSV schema check reported `CSV_ROW_0_COLS=20`, `CSV_ROW_1_COLS=20`, `CSV_ROW_2_COLS=20`, and `CSV_ROW_3_COLS=20`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_SAMPLES=100,100,100`, `CPU_AVERAGE=100`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because CPU violated the local rule and dotnet still has no generated target for this asmdef lane.

<SELF_AUDIT agent="SHINOBU_208" pass="CSV_ROW_CELL_FAIL_CLOSED_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge CSV source and SHINOBU_208 docs/logs.</runtime-boundary>
  <authoring-proof>Malformed CSV numeric cells now throw with row/column/field context instead of hydrating fallback values.</authoring-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, malformed-row execution proof, and Unity Profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - CSV Integer Overflow Fail-Closed Tail Report

What was wrong:
- `GeologyProfileCsv.ReadInt` and `ReadUInt` still saturated oversized numeric cells after the strict row/cell parser patch.
- A malformed overflow value could silently hydrate as a valid seed, resolution, variation count, AO ray count, or LOD budget before `.h8geom` generation.

What was done:
- Added explicit overflow tracking to signed and unsigned byte-digit accumulation.
- Routed overflow through the existing row/column/field `ThrowInvalidCell` path.
- Preserved the one valid signed minimum edge case: `-2147483648`.
- Removed final integer saturation returns; valid parsed values now return directly.

Cinematic cheats used:
- None added in this patch. This hardens the CSV authoring bridge feeding the existing offline SDF extraction, LOD, and baked vertex-AO Dear Lie pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: no frame-time saving claimed. The gain is fail-fast rejection before expensive SDF/AO/manifest generation from corrupt integer cells.

Verification:
- Static scan reported `GeologyProfileCsv.cs BRACES=43/43`.
- Static scan found `bool overflow = false` and `if (!hasDigit || overflow)` in both `ReadInt` and `ReadUInt`.
- Static scan found no `value = value <=` saturation assignment and no fallback-return numeric reader hit.
- Static CSV schema check reported `CSV_ROW_0_COLS=20`, `CSV_ROW_1_COLS=20`, `CSV_ROW_2_COLS=20`, and `CSV_ROW_3_COLS=20`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_SAMPLES=100,100,100`, `CPU_AVERAGE=100`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because CPU violated the local rule and dotnet still has no generated target for this asmdef lane.

<SELF_AUDIT agent="SHINOBU_208" pass="CSV_INTEGER_OVERFLOW_FAIL_CLOSED_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge CSV source and SHINOBU_208 docs/logs.</runtime-boundary>
  <authoring-proof>Signed and unsigned CSV integer overflow now throws with row/column/field context instead of saturating into plausible bake values.</authoring-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, malformed-overflow execution proof, and Unity Profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - CSV Numeric Error-Code Tail Report

What was wrong:
- CSV import failures reported row, column, and field context, but not stable numeric error codes.
- The designer facade mandate requires numeric codes so CI/editor gates can classify header, column-count, terminator, overflow, and value-domain failures without parsing prose.

What was done:
- Added `CsvErrorMalformedCell=1001`, `CsvErrorIntegerOverflow=1002`, `CsvErrorNonFiniteFloat=1003`, `CsvErrorNonPositiveValue=1004`, `CsvErrorInvalidTerminator=1005`, `CsvErrorColumnCount=1006`, and `CsvErrorHeaderSchema=1007`.
- Routed cell, integer overflow, float finite, positive-only, terminator, row-count, and header-schema failures through messages that include `Geology profile CSV error <code>`.
- Kept row/column/field context on cell errors.

Cinematic cheats used:
- None added in this patch. This is authoring-gate hardening for the offline mesh/vertex-AO fake pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: no success-path timing saving claimed. Failed imports now classify deterministically before expensive mesh/AO/manifest generation.

Verification:
- Static scan reported `GeologyProfileCsv.cs BRACES=43/43`.
- Static scan found CSV error constants `1001..1007` and `Geology profile CSV error` on header, row-count, and cell error paths.
- Static scan reported `CSV_CODE_GATE=pass`; no prose-only legacy invalid-value message, integer saturation assignment, or fallback parser token remains.
- Static CSV schema check reported `CSV_ROW_0_COLS=20`, `CSV_ROW_1_COLS=20`, `CSV_ROW_2_COLS=20`, and `CSV_ROW_3_COLS=20`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_SAMPLES=100,100,100`, `CPU_AVERAGE=100`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because CPU violated the local rule and dotnet still has no generated target for this asmdef lane.

<SELF_AUDIT agent="SHINOBU_208" pass="CSV_NUMERIC_ERROR_CODES_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge CSV source and SHINOBU_208 docs/logs.</runtime-boundary>
  <authoring-proof>CSV import failures now include stable numeric codes plus row/column/field context for cell failures.</authoring-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, malformed-row execution proof, and Unity Profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - CSV Header Schema Diagnostic Tail Report

What was wrong:
- Header validation used `HeaderMatchesExpectedSchema()` as a boolean gate and collapsed missing, reordered, or extra columns into one generic header mismatch.
- That failed the same evidence standard as row cells: malformed imports need exact row/column classification before the bake lane runs.

What was done:
- Replaced `HeaderMatchesExpectedSchema()` with `ValidateHeaderSchema()`.
- Added `ThrowHeaderMismatch()` for exact row-1/column-N schema token failures.
- Added a header column-count diagnostic that reports row 1, the first unexpected/missing column index, expected count, and observed count.

Cinematic cheats used:
- None added in this patch. This only hardens authoring validation before the existing offline geology mesh bake and vertex-AO visual fake.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: no success-path timing saving claimed. Bad headers now fail before SDF extraction, LOD generation, AO rays, mesh upload, and manifest writes.

Verification:
- Static scan reported `GeologyProfileCsv.cs BRACES=44/44`.
- Static scan reported `CSV_HEADER_GATE=pass`; no `HeaderMatchesExpectedSchema` boolean gate remains.
- Static CSV schema check reported `CSV_ROW_0_COLS=20`, `CSV_ROW_1_COLS=20`, `CSV_ROW_2_COLS=20`, and `CSV_ROW_3_COLS=20`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_SAMPLES=93,100,100`, `CPU_AVERAGE=98`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because CPU violated the local rule and dotnet still has no generated target for this asmdef lane.

<SELF_AUDIT agent="SHINOBU_208" pass="CSV_HEADER_SCHEMA_DIAGNOSTICS_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge CSV source and SHINOBU_208 docs/logs.</runtime-boundary>
  <authoring-proof>CSV header schema failures now report exact row-1/column diagnostics instead of a generic boolean mismatch.</authoring-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, malformed-header execution proof, and Unity Profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - CSV Existing File Size Fail-Closed Tail Report

What was wrong:
- Missing CSV files intentionally used `DefaultProfile()` for mock/CI bootstrap, but existing empty or oversized CSV files also used fallback behavior.
- That could bake fallback geology while hiding explicit corrupt source data.

What was done:
- Added `CsvErrorFileSize=1008`.
- Existing zero-byte or larger-than-`int.MaxValue` CSV files now throw `InvalidDataException`.
- The missing-file fallback remains intact for the emergency mock authoring route.

Cinematic cheats used:
- None added in this patch. This protects the human-readable CSV bridge that feeds the existing offline SDF/LOD/vertex-AO fake pipeline.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: failed corrupt-file imports avoid all downstream SDF density generation, extraction, normal smoothing, AO, mesh upload, and manifest writes.

Verification:
- Static scan reported `GeologyProfileCsv.cs BRACES=43/43`.
- Static scan reported `CSV_SIZE_GATE=pass`.
- Static CSV schema check reported `CSV_ROW_0_COLS=20`, `CSV_ROW_1_COLS=20`, `CSV_ROW_2_COLS=20`, and `CSV_ROW_3_COLS=20`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_SAMPLES=100,100,100`, `CPU_AVERAGE=100`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because CPU violated the local rule and dotnet still has no generated target for this asmdef lane.

<SELF_AUDIT agent="SHINOBU_208" pass="CSV_FILE_SIZE_FAIL_CLOSED_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge CSV source and SHINOBU_208 docs/logs.</runtime-boundary>
  <authoring-proof>Existing empty or oversized CSV files now fail closed with numeric error code 1008 instead of using fallback profile truth.</authoring-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, malformed-file execution proof, and Unity Profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - Profile Finite Vaccination Tail Report

What was wrong:
- CSV parsing rejected non-finite numbers, but non-CSV editor callers could still pass poisoned `GeologyBakeProfile` values directly to `BakeProfilesAsync` or `BakeSingle`.
- `SanitizeProfile` clamped ranges without first replacing NaN/Infinity, allowing non-finite radius, quality, iso, or AUP values to reach SDF, AUP seed, AO, or LOD math.

What was done:
- Added `FiniteOr(float,float)` and `FiniteOr(double,double)`.
- Routed radius, height, frequency, amplitude, ridged/voronoi weights, `IsoLevel`, `GlobalQualityWeight`, and all `SectorAup` lanes through finite fallbacks before clamp/hash/job setup.

Cinematic cheats used:
- None added in this patch. This protects the existing offline SDF extraction and baked vertex-AO Dear Lie from poisoned editor input.

Exact microseconds saved:
- Runtime: 0 us measured; no runtime code changed.
- Editor: no success-path timing saving claimed. Invalid profiles are neutralized before wasting SDF/AO/mesh serialization work or poisoning telemetry.

Verification:
- Static scan reported `GeologyForgeGenerator.cs BRACES=130/130`.
- Static scan reported `PROFILE_FINITE_GATE=pass`.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported only LF-to-CRLF working-copy warnings.
- CPU/build preflight reported `CPU_SAMPLES=100,100,100`, `CPU_AVERAGE=100`, `BUILD_PROCS=none`, and `PROJECT_TARGET_HITS=none`; no build command was launched because CPU violated the local rule and dotnet still has no generated target for this asmdef lane.

<SELF_AUDIT agent="SHINOBU_208" pass="PROFILE_FINITE_VACCINATION_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <runtime-boundary>No runtime code changed; patch is confined to Editor GeologyForge generator source and SHINOBU_208 docs/logs.</runtime-boundary>
  <math-proof>Non-finite editor profile scalar and AUP inputs are replaced before SDF, AUP seed, AO, LOD, mesh, or manifest math can observe them.</math-proof>
  <payload-boundary>No `.h8geom` header, record, vertex layout, BufferID, Vault route, runtime owner, or asmdef reference changed.</payload-boundary>
  <remaining-failures>
    <item>Compiler, Unity import, malformed-profile execution proof, and Unity Profiler proof remain pending.</item>
    <item>Runtime-wide mesh generation eradication remains false due non-owned findings.</item>
  </remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - CARVER AUDIT PATCHES STATIC TAIL REPORT

What was wrong:
- CSV import could accept a partial read from a concurrently modified profile file.
- CSV `sector_x/y/z` lanes were parsed through float math before storage in `double3`, and `-0` could diverge from `0` in AUP seed hashing.
- `BakeProfilesAsync` could assign `_asyncProfiles` before result-list setup finished, leaving a dead active latch on allocation/setup failure.
- LOD construction and asset-save failures could retain unsaved transient Unity `Mesh` objects.
- Manifest self-audit did not reject non-finite `SectorAup` lanes.
- Manifest self-audit opened `.h8geom` with `FileShare.ReadWrite`, weakening the audit's immutable-payload proof.

What was done:
- `GeologyProfileCsv` now opens existing CSV files with `FileShare.Read`, rejects short/length-changing reads with `CsvErrorFileSize=1008`, and uses `ReadDouble` for sector lanes.
- `SanitizeProfile` and `ResolveAupSeed` canonicalize finite AUP zero lanes before deterministic seed hashing.
- `BakeProfilesAsync` stages profile snapshots and result containers in locals before assigning static runner state.
- `BuildLods` constructs meshes sequentially with failure cleanup, and `SaveMeshesAndManifest` tracks per-LOD `AssetDatabase` ownership before destroying unsaved transients.
- `GeologyForgeSelfAudit.ValidateManifestRecord` rejects non-finite manifest `SectorAup`.
- `GeologyForgeSelfAudit.TryValidateManifest` now opens with `FileShare.Read` and rejects post-parse length drift as `UNSTABLE_FILE_LENGTH`.

Cinematic Cheats used:
- No runtime simulation added. Static authoring payload still buys runtime visuals through baked SDF mesh assets, vertex AO, and BRG-ready manifest records.

Exact Microseconds saved:
- Runtime: 0 us added, 0 us measured because no runtime code changed.
- Editor fault paths: avoids full SDF/AO/LOD/asset bake after CSV instability and avoids retained native mesh cleanup cost after failed LOD/save paths. Profiler proof remains pending.

Verification:
- Static brace scan: `GeologyProfileCsv.cs BRACES=44/44`, `GeologyForgeGenerator.cs BRACES=136/136`, `GeologyForgeSelfAudit.cs BRACES=38/38`.
- Static source gates: `ASYNC_ATOMIC_GATE=True`, `LOD_CLEANUP_GATE=True`, `AUP_CANON_GATE=True`, `CSV_STABLE_GATE=True`, `MANIFEST_AUP_GATE=True`, `MANIFEST_STABLE_GATE=True`.
- Targeted trailing-whitespace scan: `TRAILING_WS=none`.
- Targeted `git diff --check`: LF-to-CRLF working-copy warnings only.
- CPU/build preflight: `CPU_SAMPLES=100,100,100`, `CPU_AVERAGE=100`, `BUILD_PROCS=none`, `PROJECT_TARGET_HITS=none`, `BUILD_LAUNCHED=no`.

<SELF_AUDIT agent="SHINOBU_208" pass="CARVER_AUDIT_PATCHES_STATIC_TAIL" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <task-reconciliation>Tasks 01-20 remain static-source implemented for SHINOBU-owned Editor bake lane; project-wide runtime mesh eradication remains false due non-owned scanner findings.</task-reconciliation>
  <struct-layout>Primary runtime vertex DTO remains `GeologyVertex32` explicit 32B: position 0..11, normal 12..23, ColorRgba 24..27, Uv0Packed 28..31. Manifest header remains 64B; manifest record remains 128B. No ABI/schema migration claimed.</struct-layout>
  <scalability>GlobalQualityWeight still scales SDF noise, octave contribution, AO rays/steps/range, UV scale, and LOD budgets continuously; this patch only fixes import/state/ownership/audit failure paths.</scalability>
  <hphi>No runtime Vault buffers requested or changed. SHINOBU_208 remains Editor-only static payload generation; `.h8geom` is an immutable render handoff artifact, not global heap ownership.</hphi>
  <dependency-graph>No new runtime `JobHandle` route added. Existing editor jobs remain batch-local with explicit completion inside offline bake windows; no runtime dispatcher route claimed.</dependency-graph>
  <compile-guard>`Hecton8.World.OfflineGeology.Editor.asmdef` remains Editor-only and references only Unity Burst/Collections/Jobs/Mathematics. No sibling runtime assembly reference added.</compile-guard>
  <dear-lie>The domain still bakes mesh/AO offline and feeds static BRG-ready artifacts instead of runtime geology topology or physics. Runtime topology cost stays O(0) for this lane after bake; editor bake remains O(grid cells + emitted vertices).</dear-lie>
  <remaining-failures>Unity import, Burst compile, malformed CSV execution, manifest bake/audit execution, profiler proof, and runtime owner consumption proof remain pending. Full int64 AUP-sector ABI migration is not claimed.</remaining-failures>
</SELF_AUDIT>

## 2026-05-20 - MILL STATIC AUDIT HARDENING REPORT

What was wrong:
- `GEOLOGY_LAYOUT_AUDIT.json` could report `STATIC_LAYOUT_AUDIT_PASS` when no generated meshes and no manifest existed.
- Manifest GUID fields only proved nonzero integers; stale nonzero GUIDs could pass without resolving to mesh assets.
- `ResolveGuid128` converted missing or malformed GUID text into zero/truncated payload words.
- A failed three-LOD save tranche could leave newly created partial `.asset` files without a manifest record.
- CSV file-size validation only rejected files above `int.MaxValue`, allowing excessive Temp native allocation before parse failure.
- UTF-8 BOM headers from designer-exported CSVs failed exact header validation at column 1.
- `NativeDisableParallelForRestriction` and `NativeDisableUnsafePtrRestriction` fields in `GeologyForgeJobs.cs` lacked local safety invariants.

What was done:
- Layout audit pass now requires `meshCount > 0`, `manifestValid`, and `manifestRecords > 0`; reports include `noOutput`.
- Manifest audit resolves each LOD GUID pair back through `AssetDatabase.GUIDToAssetPath`, loads a `Mesh`, and validates its 32B vertex layout.
- Generator GUID hydration now throws on missing, non-32-character, or non-hex GUIDs before a record can be appended.
- `SaveMeshesAndManifest` deletes newly created partial LOD assets after `AssetDatabase.StopAssetEditing()` when save fails, then destroys unsaved transient meshes through a wrapper that logs cleanup faults without masking the original save exception.
- CSV import rejects existing files above `MaximumCsvBytes=4194304` before native scratch allocation.
- CSV import skips optional UTF-8 BOM bytes before header token detection, schema validation, and first data-row cursor setup.
- Unsafe Burst suppression fields now document disjoint ranges, physical non-aliasing, and returned-`JobHandle` dependency ownership.

Cinematic Cheats used:
- No runtime simulation added. This preserves the original Dear Lie: geology is baked offline into static mesh assets, vertex AO, and `.h8geom` manifest records instead of runtime terrain physics/topology.

Exact Microseconds saved:
- Runtime: 0 us added, 0 us measured; no runtime assembly or runtime route changed.
- Editor failure paths: avoids full SDF/AO/LOD bake after oversized CSV input, avoids false-positive empty audits, and removes orphan newly-created LOD assets after failed saves. Exact Unity Editor timings remain pending.

Verification:
- Static brace scan: `GeologyProfileCsv.cs BRACES=45/45`, `GeologyForgeGenerator.cs BRACES=148/148`, `GeologyForgeSelfAudit.cs BRACES=42/42`, `GeologyForgeJobs.cs BRACES=79/79`.
- Static source gates found `MaximumCsvBytes`, `Utf8BomOffset`, `TryCleanupFailedAssetSave`, `DeleteCreatedAssets`, strict GUID exceptions, `ValidateMeshGuid`, `noOutput`, and SAFETY comments on all GeologyForgeJobs unsafe suppressions.
- Targeted trailing-whitespace scan reported `TRAILING_WS=none`.
- Targeted `git diff --check` reported LF-to-CRLF working-copy warnings only.
- CPU/build preflight reported `CPU_SAMPLES=100,100,100`, `CPU_AVERAGE=100`, `BUILD_PROCS=none`, `PROJECT_TARGET_HITS=none`, `BUILD_LAUNCHED=no`; no build command was legal or useful.

<SELF_AUDIT agent="SHINOBU_208" pass="MILL_STATIC_AUDIT_HARDENING" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <task-reconciliation>Tasks 01-20 remain implemented for SHINOBU-owned Editor bake lane. This pass hardens Task 10 asset serialization, Task 15 telemetry/audit evidence, Task 17 CSV bridge, and Task 20 self-audit proof.</task-reconciliation>
  <struct-layout>Primary ABI unchanged: `GeologyVertex32` 32B, `GeologyRawVertex` 64B, manifest header 64B, manifest record 128B. No payload offset or save identity migration was introduced.</struct-layout>
  <scalability>`GlobalQualityWeight` continuous bake math is unchanged. The patch rejects invalid authoring/audit states before expensive high/ultra SDF, AO, LOD, mesh upload, or manifest work can be trusted.</scalability>
  <hphi>No runtime Vault buffers or private runtime arrays added. This remains Editor-only static payload generation; `.h8geom` is immutable render handoff evidence.</hphi>
  <dependency-graph>No new runtime `JobHandle` route added. Unsafe suppression comments document existing editor job dependencies and disjoint write ranges.</dependency-graph>
  <compile-guard>`Hecton8.World.OfflineGeology.Editor.asmdef` remains Editor-only with Unity Burst/Collections/Jobs/Mathematics references only; no sibling runtime assembly reference was added.</compile-guard>
  <dear-lie>Offline static mesh/AO baking remains the fake replacing runtime geology simulation. Runtime topology cost remains O(0) after bake; editor bake cost remains O(grid cells + emitted vertices).</dear-lie>
  <remaining-failures>Unity import/compile, actual AssetDatabase save-failure execution, manifest GUID audit execution, and Profiler allocation proof remain pending. Existing asset mutation rollback is not claimed because AssetDatabase has no verified atomic three-file transaction in this pass.</remaining-failures>
</SELF_AUDIT>

## 2026-05-21 - PAULI STATIC DEFECT INTEGRATION REPORT

What was wrong:
- Runtime mesh-generation scanner roots covered only `World`, `Environment`, and one special voxel file while the report verdict read like project-wide proof.
- Asset save cleanup stopped after the LOD save tranche; GUID resolution or manifest-record append failure could leave new or overwritten LOD assets without a manifest record.
- Layout self-audit accepted GUIDs resolving to any mesh path and did not reject extra top-level output meshes outside the manifest.
- `GeologyRawVertex` padding was named `Padding0`, and the manifest record had an implicit 4-byte hole at bytes 68..71.
- Editor `.Complete()` fences were visible in source but lacked local blocking-fence proof.

What was done:
- `RuntimeMeshGenerationScanner` now scans `Assets/_Project/Scripts` excluding `Editor` folders; the JSON report was refreshed with project-scope static evidence: `findingCount=137`, `actionableFindingCount=131`, `proceduralMaterialCloneFindingCount=66`, `runtimeMeshAllocationsEradicated=false`.
- `SaveMeshesAndManifest` now creates `_H8Backups` for existing LOD assets, restores them on save/GUID/manifest-record failure, removes manifest-tail records on failure, deletes newly created partial assets, and deletes backups only after manifest append succeeds.
- `GeologyForgeSelfAudit` now validates that manifest GUIDs resolve under the geology output folder, are unique, and match the top-level output mesh set exactly; foreign, duplicate, non-mesh, or unmanifested assets fail.
- `GeologyRawVertex` padding is `_pad0`; `GeologyMeshManifestRecord.BoundsExtents` owns bytes 60..71 and GUID lanes start aligned at byte 72, so no manifest pad field is present.
- Every SHINOBU-owned `.Complete()` fence now has a `BLOCKING_SYNC_POINT` reason.

Cinematic Cheats used:
- No runtime simulation added. The Dear Lie remains offline static mesh/AO baking plus BRG-ready manifest evidence; runtime geology topology cost remains outside the frame loop for this lane.

Exact Microseconds saved:
- Runtime: 0 us added, 0 us measured.
- Editor: backup/restore work only occurs on existing-asset overwrite paths. The larger gain is failure containment: bad high/ultra bakes no longer leave orphan/foreign static payloads that would later waste render/import investigation time.

Verification:
- `GeologyForgeGenerator.cs BRACES=158/158`, `GeologyForgeSelfAudit.cs BRACES=46/46`, `GeologyForgeTypes.cs BRACES=10/10`, `GeologyVertexLayoutValidator.cs BRACES=34/34`, `GeologyForgeJobs.cs BRACES=79/79`.
- `RuntimeMeshGenerationScanner.cs` structural lexer excluding string/char literals reports `62/62`.
- `TRAILING_WS=none`.
- Targeted `git diff --check` reports LF-to-CRLF working-copy warnings only.
- `GEOMETRY_OPTIMIZATION_REPORT.json` parses via `ConvertFrom-Json` and reports project-scope false eradication.
- Build not launched: `CPU_AVERAGE=100`, `BUILD_PROCS=none`, `PROJECT_TARGET_HITS=none`.

<SELF_AUDIT agent="SHINOBU_208" pass="PAULI_STATIC_DEFECT_INTEGRATION" status="STATIC_PATCH_PROJECT_REGEN_REQUIRED">
  <task-reconciliation>Tasks 01-20 remain static-source implemented for the SHINOBU-owned Editor bake lane. Task 19 evidence is now project runtime scope and remains false, which is the correct verdict until other owners migrate their runtime mesh/material clone sites.</task-reconciliation>
  <struct-layout>`GeologyVertex32` remains explicit 32B. `GeologyRawVertex` remains explicit 64B with `_pad0@56`. `GeologyMeshManifestHeader` remains 64B. `GeologyMeshManifestRecord` remains 128B with `BoundsExtents` occupying bytes 60..71 and GUID lanes starting at byte 72; no ABI size change.</struct-layout>
  <scalability>GlobalQualityWeight math was not changed. The patch hardens proof/asset failure paths while preserving continuous low/mid/high/ultra bake scaling.</scalability>
  <hphi>No runtime Vault buffers, GlobalRegistry polling, signal routes, or runtime owners were added. The `.h8geom` file remains immutable static render handoff data.</hphi>
  <dependency-graph>No runtime dispatcher route added. Editor-only job fences are documented as offline timing/readback/Unity Mesh API boundaries.</dependency-graph>
  <compile-guard>`Hecton8.World.OfflineGeology.Editor.asmdef` remains Editor-only and Unity-only; no sibling runtime assembly reference was added.</compile-guard>
  <dear-lie>Static rocks still use offline SDF extraction, triplanar UV, deterministic LODs, and baked vertex AO instead of runtime topology, runtime SSAO, or runtime physics.</dear-lie>
  <remaining-failures>Unity import/compile, actual bake execution, AssetDatabase backup/restore execution, layout audit execution, profiler proof, and runtime BRG consumer proof remain pending.</remaining-failures>
</SELF_AUDIT>

## 2026-05-21 - MANIFEST ORPHAN COUNT CORRECTION REPORT

What was wrong:
- Layout self-audit failed empty or missing manifests, but did not increment `unmanifestedMeshCount` for top-level mesh assets when the manifest GUID set was empty.

What was done:
- `ValidateGeneratedMeshes` now checks every top-level geology mesh path against the manifest GUID set directly. Empty set means every top-level mesh is reported as `UNMANIFESTED_MESH_ASSET`.
- Architecture notes and the binary payload ledger now state that orphan mesh accounting is unconditional against the manifest GUID set.

Cinematic Cheats used:
- No runtime route added. This preserves the offline mesh/AO/manifest Dear Lie and only hardens editor proof artifacts.

Exact Microseconds saved:
- Runtime: 0 us added, 0 us measured.
- Editor: unchanged asymptotic audit cost; one existing hash lookup per top-level mesh now also produces accurate orphan counts in missing-manifest cases.

Verification:
- `GeologyForgeSelfAudit.cs STRUCTURAL_BRACES=39/39`.
- `UNCONDITIONAL_ORPHAN_CHECK=True`; `OLD_ORPHAN_CHECK=False`.
- Architecture and binary payload ledger both document empty/missing-manifest orphan accounting.
- `TRAILING_WS=none`.
- Targeted `git diff --check` reports LF-to-CRLF working-copy warnings only.
- Build not launched: `CPU_SAMPLES=89,95,100`, `CPU_AVERAGE=95`, `BUILD_PROCS=none`, `PROJECT_TARGET_HITS=none`.

## 2026-05-21 - KEPLER LUT AND MANIFEST LAYOUT CORRECTION REPORT

What was wrong:
- `ValidateComplementWinding()` would reject tetra LUT pairs `1/14`, `2/13`, `4/11`, and `7/8` because cases `14`, `13`, `11`, and `8` did not reverse the inverse-case edge order.
- `GeologyMeshManifestRecord._pad0` at byte 68 overlapped `BoundsExtents.z`, corrupting bytes 68..71 while the validator accepted the overlap.

What was done:
- Reversed complement edge sequences: `14 -> Edge03,Edge02,Edge01`; `13 -> Edge12,Edge13,Edge01`; `11 -> Edge23,Edge12,Edge02`; `8 -> Edge23,Edge13,Edge03`.
- Removed `GeologyMeshManifestRecord._pad0` and removed its validator offset check.
- Updated architecture, ledger, status, and rationale to state the current manifest byte map: `BoundsExtents` owns bytes 60..71; GUID lanes start aligned at byte 72.

Cinematic Cheats used:
- No runtime route added. This keeps the offline SDF/tetra extraction plus baked AO fake intact and only corrects editor-time proof and payload layout.

Exact Microseconds saved:
- Runtime: 0 us added, 0 us measured.
- Editor: validation cost unchanged. The saved cost is avoided failed audits and avoided rebakes from inverted complement triangles or corrupt manifest bounds.

Verification:
- `LUT_COMPLEMENT_WINDING=PASS`.
- `MANIFEST_OVERLAP_RESIDUE=none`.
- `EXPLICIT_LAYOUT_OVERLAP_SCAN=PASS` across six explicit DTO structs.
- Structural braces: `GeologyForgeJobs.cs=79/79`, `GeologyForgeTypes.cs=10/10`, `GeologyVertexLayoutValidator.cs=9/9`, `GeologyForgeSelfAudit.cs=39/39`.
- `TRAILING_WS=none`.
- Targeted `git diff --check` reports LF-to-CRLF working-copy warnings only.
- Build not launched: `CPU_SAMPLES=100,100,100`, `CPU_AVERAGE=100`, `BUILD_PROCS=none`, `PROJECT_TARGET_HITS=none`.
