# Rationale_SHINOBU_213

Date: 2026-05-20
Agent: SHINOBU_213
Domain: OFFLINE_LOD_AND_COLLIDER_BAKER
Status: PENDING VERIFICATION / PRE-ENDIAN ROSLYN PROBE PASS / POST-ENDIAN BOUNDED-HULL-ASSET-BIND-SAFETY-INDEX-HOT-STRUCT PROBE GATED BY CPU=93.3 / UNITY IMPORT AND PROFILER PENDING

## Decision 001: Editor-only ownership boundary

Problem: LOD decimation and collider baking can become a runtime control surface if implemented as active MonoBehaviours.
Solution: Keep generation under Editor folders and emit static `Mesh`, `Prefab`, `LODGroup`, primitive collider, and report artifacts only.
Rejected Alternatives: Runtime LOD manager or `Update()`-based threshold controller was rejected because Global Authority law assigns runtime quality shift to external systems and active switching adds per-frame cost.
Scalability potential: Low uses earlier LOD/cull and primitive colliders; Middle keeps LOD1/LOD2 stable; High extends LOD residency; Ultra spends saved PhysX/GPU time on richer near-field visual meshes.
Hardware Impact: i3/MX350 avoids complex concave PhysX meshes and lowers render triangle pressure before scene load; expected runtime saving is asset-dependent and remains PENDING VERIFICATION.

## Decision 002: Primitive-first collider lie

Problem: Convex hulls are cheaper than concave mesh collision but still heavier than primitive colliders.
Solution: Fit sphere and box candidates first, then generate convex hull only when primitive error exceeds tolerance.
Rejected Alternatives: Always-convex MeshCollider was rejected because PhysX narrowphase still pays hull contact cost where a BoxCollider/SphereCollider would be sufficient.
Scalability potential: Low locks many props to primitive-only; Middle allows convex hulls for silhouettes; High/Ultra can keep precise visuals while colliders remain cheap.
Hardware Impact: i3/MX350 benefits through fewer contact manifolds and lower broad/narrow phase work; exact microseconds require profiler proof.

## Decision 003: Native scratch and ARM64 DTO layout

Problem: Geometry loops and DTOs can silently allocate or misalign on ARM64 if implemented with managed arrays/properties/packed structs.
Solution: Use unmanaged structs with public fields, explicit 16-byte `LodConfigurationDTO`, native scratch with `UninitializedMemory`, and editor layout validation.
Rejected Alternatives: `Vector3[]`, LINQ, auto-properties, and `[StructLayout(Pack=1)]` were rejected due to GC, defensive copies, and misaligned ARM64 loads.
Scalability potential: Low keeps bake time tolerable for large asset libraries; High/Ultra can generate denser LOD spectra without changing runtime ABI.
Hardware Impact: Editor bake avoids unnecessary zero-fill and heap churn; runtime DTO reads remain aligned. Evidence class is static until Unity/Burst validation runs.

## Decision 004: Route relevance

Problem: The project gate requires every product task to improve the first 20 minutes route or remove a blocker.
Solution: This work targets world/resource/structural asset performance by preventing high-poly render and collision assets from entering the Copper Wire route.
Rejected Alternatives: Broad art-pipeline polish unrelated to route assets was rejected.
Scalability potential: Low route scenes run with primitive colliders and earlier cull; Ultra keeps visual overkill in LOD0 while physics stays fake.
Hardware Impact: Reduces CPU/GPU pressure before player route profiling; measured proof absent.

## Decision 005: Existing Geology Forge collision correction

Problem: Existing editor Geology Forge generated `MeshCollider.convex = false` on LOD2 collision output and referenced a missing `GeologyVertexLayoutValidator.Layout` property.
Solution: Add the validator layout property and change generated geology collision to `convex = true` with faster cooking and mesh cleaning flags.
Rejected Alternatives: Leaving the existing generator untouched was rejected because it would keep producing concave MeshCollider output in the same offline-geometry domain and can block compile/static acceptance.
Scalability potential: Low devices avoid concave PhysX geometry from generated geology; Middle/High/Ultra can retain visual LOD richness while collision remains bounded.
Hardware Impact: i3/MX350 avoids concave collision cooking/narrowphase on generated geology prefabs. Exact runtime microseconds remain PENDING VERIFICATION.

## Decision 006: Compile gate deferred by host load

Problem: Local CPU load reported 100 percent while no `dotnet` or `csc` process was active.
Solution: Do not launch `dotnet build` until CPU load drops below the protocol threshold.
Rejected Alternatives: Running build under CPU saturation was rejected because project instructions explicitly forbid builds while CPU load is above 50 percent.
Scalability potential: Not a runtime design decision; preserves shared workstation stability during multi-agent execution.
Hardware Impact: Prevents false file-lock/time-slice build failures on the i5-class host.

## Decision 007: Primitive-first support hull over full Quickhull

Problem: A full arbitrary-polyhedron Quickhull implementation increases editor bake complexity and can become fragile under malformed imported meshes while the runtime objective is to remove concave PhysX work.
Solution: Use primitive fitting first, then a deterministic conservative support hull generated from validated source vertices. The current implementation honors `ConvexHullVertexLimit` up to a fixed 32-vertex cap; see Decision 037 for the later bounded-hull correction that replaced the initial minimum fallback.
Rejected Alternatives: Full Quickhull was rejected for this pass because a bad hull topology generator can produce invalid MeshCollider assets; always using source mesh collision was rejected because it keeps high-poly collision in PhysX.
Scalability potential: Low uses sphere/box or the smallest bounded hull; Middle keeps cheap physics while LOD1/LOD2 shed render cost; High keeps near-field LOD0 visual overkill; Ultra spends saved physics cost on render density, not collision complexity.
Hardware Impact: i3/MX350 avoids concave mesh broadphase/narrowphase and keeps fallback convex contact sets bounded at <=32 vertices. Estimated saving is 15-250 microseconds per active high-poly collider cluster versus concave MeshCollider, pending profiler proof.

## Decision 008: Static reports as enforcement, not chat claims

Problem: Artist-prefab drift can reintroduce missing LODGroups, high-poly concave MeshColliders, or bad manual LOD material assignments after this tool lands.
Solution: Add `Unoptimized_Mesh_Scanner` and JSON report emission to `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`, plus bake telemetry to `Docs/Reports/LOD_OPTIMIZATION_REPORT.json`.
Rejected Alternatives: Manual inspection in the Unity hierarchy was rejected because it is non-repeatable and misses imported prefab changes.
Scalability potential: Low-tier builds block bad collision assets before they enter scenes; Middle/High/Ultra can deliberately keep expensive visuals only in generated LOD0 while static reports enforce cheap physics.
Hardware Impact: i3/MX350 gains are asset-dependent. The scanner itself is editor-only; runtime impact is zero.

## Decision 009: Local asmdef isolation without hijacking neighboring work

Problem: The shared `Assets/_Project/Scripts/Editor/OfflineGeometryBaker` directory also contains unrelated `InteriorClutterForgeJobs.cs`, so a new asmdef at that parent would capture another agent's code and mutate their compile boundary.
Solution: Move SHINOBU_213 editor files into `OfflineGeometryBaker/Shinobu213` and add `Hecton8.World.OfflineGeometry.Editor.asmdef` there. Add `Hecton8.World.OfflineGeometry.asmdef` for the runtime DTO only.
Rejected Alternatives: Editing the global `Hecton8.Editor.asmdef` or placing an asmdef over the parent folder was rejected because it would drag sibling editor code into this domain and increase merge conflict risk.
Scalability potential: Compile wall is narrower; SHINOBU_213 editor code depends only on runtime DTO, Burst, Collections, Jobs, and Mathematics.
Hardware Impact: No runtime impact. Editor iteration cost is reduced structurally by avoiding broad sibling assembly references.

## Decision 010: Editor black-box ring, not runtime Vault state

Problem: The baker is editor-only but still needs forensic proof for NaN/non-finite bake state and repeated asset processing failures.
Solution: Add a 300-row `NativeArray<OfflineGeometryBakeTelemetryEntry>` ring with explicit 64-byte rows and dump to `Docs/AgentLogs/Dump_SHINOBU_213.bin` on non-finite bake metrics.
Rejected Alternatives: Runtime `GlobalDataVault` reservation was rejected because the baker emits immutable assets and owns no persistent gameplay memory.
Scalability potential: Low/Middle/High/Ultra runtime paths are unaffected; editor diagnostics scale by fixed 300 rows only.
Hardware Impact: Runtime cost is 0us. Editor memory cost is 19,200 bytes plus NativeArray header.

## Decision 011: Native CSV staging

Problem: `File.ReadAllBytes` creates a managed `byte[]` staging allocation and weakens the CSV ingestion claim.
Solution: Read the CSV through `FileStream.Read(Span<byte>)` into a `NativeArray<byte>` allocated with `Allocator.Temp`, then parse by byte cursor into `FixedString64Bytes` and numeric fields.
Rejected Alternatives: `string.Split`, `File.ReadAllBytes`, and managed token arrays were rejected because they provide no useful authoring control and create avoidable managed garbage.
Scalability potential: Profile parsing remains cold/editor-only, but larger profile tables do not require managed staging arrays.
Hardware Impact: Runtime cost is 0us. Editor GC pressure from profile reads is reduced; exact bytes require Unity profiler proof.

## Decision 012: Asset reference reload after mesh serialization

Problem: `SaveOrReplaceMesh` can destroy the transient mesh when replacing an existing asset, which would leave generated prefab renderers pointing at a destroyed object.
Solution: Reload saved mesh assets via `AssetDatabase.LoadAssetAtPath<Mesh>` before assigning them to generated LOD renderers.
Rejected Alternatives: Passing transient mesh references directly after `CopySerialized` was rejected because it is a silent editor-time asset-reference hazard.
Scalability potential: All quality weights now bind stable mesh assets instead of transient editor objects.
Hardware Impact: Runtime cost is 0us. Correctness fix prevents invalid prefab references.

## Decision 013: Deterministic Unity source GUIDs

Problem: New Unity C# source files without `.meta` files receive editor-generated GUIDs on import, which creates avoidable churn and can destabilize serialized references if an editor tool is later referenced by menu tests or automation assets.
Solution: Add explicit MonoImporter `.meta` files for every SHINOBU_213 C# source file and DefaultImporter `.meta` files for owned folders while keeping asmdef GUIDs unchanged.
Rejected Alternatives: Letting Unity generate metas was rejected because it turns import order into a source-control fact and creates noise for 20+ concurrent agents.
Scalability potential: No runtime behavior change; import determinism keeps the compile boundary stable across weak developer machines and CI workers.
Hardware Impact: Runtime cost is 0us. Editor impact is reduced import churn and fewer GUID conflicts.

## Decision 014: Quality-weighted bake math, not threshold-only policy

Problem: Initial scalability evidence bent LOD thresholds but left LOD triangle ratios and primitive fitting tolerance mostly profile-static.
Solution: Add `ResolveLod1Ratio`, `ResolveLod2Ratio`, and `ResolvePrimitiveTolerance` so `GlobalQualityWeight` and depth continuously reduce generated LOD density and increase primitive acceptance under weak/obscured conditions.
Rejected Alternatives: Binary low/high hardware switches and threshold-only scaling were rejected because they leave triangle count and PhysX simplification disconnected from the global quality continuum.
Scalability potential: Low devices/deep sectors generate fewer LOD1/LOD2 triangles and accept more sphere/box lies; Middle holds moderate density; High/Ultra preserve richer visual LODs while collision stays primitive-first.
Hardware Impact: Runtime cost is 0us because this is an offline bake decision. i3/MX350 benefits from lower generated triangle residency and more primitive colliders; exact microseconds remain PENDING VERIFICATION.

## Decision 015: Derived hard budgets for LOD1/LOD2

Problem: Ratio-based LOD1/LOD2 generation could exceed the LOD0 hard cap when source meshes were far above budget.
Solution: Derive LOD1 and LOD2 max-triangle caps from `Lod0HardBudget * resolvedRatio`, with LOD2 additionally clamped below LOD1.
Rejected Alternatives: Pure ratio decimation was rejected because hard budget tasks must survive pathological source meshes.
Scalability potential: Low/deep bakes get aggressively smaller LOD1/LOD2 meshes; Middle keeps controlled budget growth; High/Ultra can increase ratios but never above derived hard caps.
Hardware Impact: Runtime cost is 0us. i3/MX350 avoids accidental LOD1 triangle spikes from oversized source assets; exact GPU microseconds are asset-dependent and pending profiler proof.

## Decision 016: Full telemetry row offset proof

Problem: The self-audit proved telemetry row size but did not list every field offset, weakening the 64-byte false-sharing claim.
Solution: Emit all `OfflineGeometryBakeTelemetryEntry` offsets in the generated XML audit and static artifact.
Rejected Alternatives: A single `Size=64` claim was rejected because it does not prove field ordering or row packing.
Scalability potential: No runtime gameplay impact; black-box evidence remains fixed at 300 rows while future worker-lane ownership can rely on a cache-line row contract.
Hardware Impact: Runtime cost is 0us. Editor audit work is cold and deterministic.

## Decision 017: Total material slot drift scan

Problem: Manual LOD material validation compared only the first non-null renderer in each LOD level and could miss multi-renderer slot drift.
Solution: Compare total `sharedMaterials` slot count across all renderers for each LOD level.
Rejected Alternatives: First-renderer checks were rejected because complex prefabs often split geometry across children.
Scalability potential: Prevents visually broken manual LODs from being accepted into any quality tier before deterministic bake replacement.
Hardware Impact: Runtime cost is 0us; scanner is editor-only. It reduces route-debug time by catching broken LOD authoring earlier.

## Decision 018: Quality-scaled local saliency decimation

Problem: Plain stride sampling met hard budgets but could drop visually important large triangles near the sample point.
Solution: Add `ResolveDecimationWindow` and per-triangle saliency scoring inside the Burst decimation jobs. Low quality uses a one-triangle window; high quality scans a bounded seven-triangle local window and chooses the strongest area-normalized candidate while preserving source UVs and normals.
Rejected Alternatives: Full global QEM was rejected for this pass because it requires heavier adjacency topology and can destabilize import if implemented hastily; plain stride-only sampling was rejected as too visually weak.
Scalability potential: Low devices get cheapest offline decimation and smaller LOD ratios; Middle gets a bounded local search; High/Ultra spend editor bake time to preserve stronger silhouettes without changing runtime cost.
Hardware Impact: Runtime cost is 0us. Editor bake cost rises by at most 7 candidate triangle evaluations per output triangle at high quality; generated GPU work is still bounded by hard budgets.

## Decision 019: Raw pointer stream reads through UnsafeUtility.AsRef

Problem: Vertex stream extraction used safe strided reads but did not literally satisfy the assignment's `UnsafeUtility.AsRef<T>` raw-pointer instruction.
Solution: Source position, normal, and UV accessors now compute the byte pointer from stream base, field offset, index, and stride, then return the value through `UnsafeUtility.AsRef<T>`.
Rejected Alternatives: `ReadArrayElementWithStride` was rejected for this mandate pass because the batch prompt explicitly requires raw pointer/as-ref iteration to avoid hidden property-style access.
Scalability potential: All quality tiers use the same direct source stream access; higher-quality local saliency windows now reuse the direct reads.
Hardware Impact: Runtime cost is 0us. Editor Burst kernels expose direct typed reads for LLVM/Burst optimization; exact bake microseconds remain pending.

## Decision 020: Partition-local saliency coverage

Problem: Overlapping saliency windows could allow neighboring output triangles to select the same high-scoring source triangle, leaving avoidable source coverage holes.
Solution: Map each output triangle to a deterministic non-overlapping source partition, then sample up to `SelectionWindow` candidates inside that partition and choose the strongest area-normalized triangle.
Rejected Alternatives: Centered overlapping windows were rejected because they preserve isolated large triangles but weaken coverage guarantees. Full topology-aware QEM remains rejected for this pass because it requires heavier adjacency construction.
Scalability potential: Low quality remains one candidate per partition; Middle samples a small bounded set; High/Ultra scan up to seven candidates per partition while preserving deterministic coverage under the same hard cap.
Hardware Impact: Runtime cost is 0us. Editor work remains bounded at seven candidate evaluations per output triangle and avoids duplicate-selection waste.

## Decision 021: Immutable flat LOD binary manifest

Problem: JSON reports are useful for humans but are the wrong ingestion surface for BRG or runtime LOD metadata consumers.
Solution: Emit `Assets/_Project/BakedGeometry/Optimized/offline_lod_manifest.h8lod` with a 64-byte header, little-endian tag, aggregate hashes, and 128-byte records containing mesh hashes, triangle counts, thresholds, ratios, primitive tolerance, quality, depth, and warning flags.
Rejected Alternatives: Parsing JSON at runtime was rejected because it is managed, slow, and not a flat binary payload. Reserving Vault buffers was rejected because this baker owns no runtime mutable fact.
Scalability potential: Low/Middle/High/Ultra consumers can read the same immutable records and apply their own continuous quality policy without coupling to editor classes.
Hardware Impact: Runtime hot-path cost is 0us until an owner imports the payload. Binary layout is fixed and ARM64-aligned for future bulk copy.

## Decision 022: Manifest finite sanitation and import visibility

Problem: A flat binary payload must not carry NaN floats into future runtime importers, and generated files under `Assets/` can remain invisible until an asset refresh/import.
Solution: Sanitize manifest float fields before write and call `AssetDatabase.ImportAsset` for the `.h8lod` path after the file stream closes.
Rejected Alternatives: Trusting upstream metrics was rejected because binary payloads need their own NaN boundary. Waiting for a later global refresh was rejected because the forge must produce immediately visible artifacts.
Scalability potential: All quality tiers read finite manifest values; import visibility is deterministic for CI/editor automation.
Hardware Impact: Runtime cost is 0us. Editor cost is one forced import of a small fixed-record payload.

## Decision 023: Source index clamp at raw pointer boundary

Problem: Decimation jobs trusted imported mesh indices before computing raw source stream pointers.
Solution: Pass `meshData.vertexCount` into UInt16/UInt32 decimation jobs and clamp every position/normal/UV source index before `UnsafeUtility.AsRef<T>`.
Rejected Alternatives: Trusting imported mesh validity was rejected because offline tooling must survive malformed or corrupt art assets without reading outside the stream.
Scalability potential: All quality tiers keep the same safety boundary; high-quality saliency scans cannot amplify a bad index into repeated unsafe reads.
Hardware Impact: Runtime cost is 0us. Editor Burst kernels pay one integer clamp per source read, acceptable for offline robustness.

## Decision 024: Manifest reserve lanes are 4-byte aligned

Problem: The manifest header/record reserves initially used `ulong` fields after 4-byte payload fields. The offsets were aligned, but the layout mixed late 8-byte lanes unnecessarily.
Solution: Convert manifest reserve lanes to explicit 4-byte `uint` fields while preserving 64-byte header and 128-byte record sizes.
Rejected Alternatives: Keeping `ulong` reserves was rejected because the payload does not need 8-byte reserves and the assignment demands paranoia around ARM64 field ordering.
Scalability potential: Future low/high tier consumers receive a simpler uniform 4-byte payload surface.
Hardware Impact: Runtime hot-path cost is 0us. Future importers can bulk-copy 4-byte aligned records without 8-byte reserve ambiguity.

## Decision 025: Editor progress without runtime components

Problem: The UI Toolkit window previously set progress only before and after a long bake, which gave weak feedback during large folders.
Solution: Add `EditorUtility.DisplayProgressBar` calls inside selection and folder bake loops, clearing the progress bar in `finally`.
Rejected Alternatives: Adding a runtime progress MonoBehaviour or active prefab script was rejected because generated assets must remain static and editor-only.
Scalability potential: Large low/high tier asset libraries expose current asset progress during offline baking without changing generated runtime behavior.
Hardware Impact: Runtime cost is 0us. Editor UI overhead is cold and bounded to one progress update per source asset.

## Decision 026: Preserve all source submesh ranges

Problem: LOD range generation capped processed submeshes at 64, which could silently drop material/submesh ranges from complex artist assets.
Solution: Allocate the range table for the full `meshData.subMeshCount` and remove the dead cap constant.
Rejected Alternatives: Keeping the cap was rejected because it protects editor memory at the cost of correctness and material preservation.
Scalability potential: Low through Ultra tiers retain material/submesh coverage while triangle budgets still enforce generated density.
Hardware Impact: Runtime cost is 0us. Editor memory scales by one small range struct per source submesh.

## Decision 027: Hard budget beats minimum-one submesh allocation

Problem: Preserving every submesh with a minimum of one triangle can violate strict LOD budgets when source submesh count exceeds target triangle count.
Solution: Allocate submesh target triangles with floor-proportional distribution, fill remaining budget deterministically, and reduce from the tail when necessary; zero-triangle submeshes are allowed only when the hard budget cannot represent every range.
Rejected Alternatives: Keeping one triangle per submesh was rejected because the assignment requires hard polygon budgets, not best-effort caps.
Scalability potential: Low quality/deep assets can collapse aggressively without budget leakage; High/Ultra can preserve more submeshes by raising continuous ratios while still respecting caps.
Hardware Impact: Runtime cost is 0us. Editor allocation remains one range row per submesh; generated GPU triangle count obeys the cap.

## Decision 028: No empty Unity submesh descriptors

Problem: Hard-budget allocation can produce zero-output submesh ranges when the target triangle budget is lower than source submesh count.
Solution: Keep zero-output ranges in the budget calculation but skip them when calling `Mesh.SetSubMesh`, so Unity receives only positive index-count descriptors.
Rejected Alternatives: Serializing zero-count submeshes was rejected because importer/runtime handling is ambiguous and extra material slots do not justify empty geometry descriptors.
Scalability potential: Low quality can drop unrepresentable ranges cleanly; High/Ultra preserve more ranges through higher continuous ratios.
Hardware Impact: Runtime cost is 0us. Generated meshes avoid empty submesh metadata.

## Decision 029: No implicit clear for black-box ring

Problem: The editor black-box ring used `NativeArrayOptions.ClearMemory`, which is a safe small allocation but still weakens the zero-init audit surface.
Solution: Allocate the 300-row telemetry ring with `UninitializedMemory` and explicitly write a deterministic sentinel row once at allocation.
Rejected Alternatives: Keeping implicit clear was rejected because static gates should not need domain-specific exceptions for SHINOBU_213 memory ownership.
Scalability potential: All quality tiers keep fixed 300-row forensic coverage; low-tier editor machines avoid implicit allocator clearing markers.
Hardware Impact: Runtime cost is 0us. Editor allocation writes 300 cache-line rows once, replacing allocator-side zero-fill with explicit deterministic initialization.

## Decision 030: Probe against actual Unity 6000 API surface

Problem: Generated csproj files for the new asmdefs do not exist until Unity imports the new assemblies, and `dotnet build --no-restore` stopped before compile with a missing `project.assets.json`.
Solution: Build a local Roslyn response-file probe using Unity 6000 reference assemblies, Burst, Collections, Mathematics, the runtime DTO output DLL, and the SHINOBU_213 editor files. Fix the probe findings directly: import `UnityEditor.UIElements` for `ObjectField`, and use `MeshData.GetVertexAttributeStream/Format/Dimension/Offset` instead of the unsupported `GetVertexAttribute` helper.
Rejected Alternatives: Treating stale generated csproj output as compile proof was rejected because it does not include the new asmdefs. Launching a full Unity batch import was rejected for this pass because it is heavier than the scoped compiler proof and the user explicitly warned against rebuild spam.
Scalability potential: Compile-wall isolation remains intact; the fix uses existing Unity API primitives already present in adjacent project code and does not add sibling-domain references.
Hardware Impact: Runtime cost is 0us. Editor import risk is reduced because the API mismatch is caught before Unity opens the new assembly.

## Decision 031: Explicit little-endian manifest writer

Problem: The `.h8lod` manifest header/record structs were aligned, but raw span writes still depended on host-endian memory representation.
Solution: Serialize every 4-byte manifest field into stack spans with explicit little-endian byte order. Float lanes still use `math.asuint`; byte reversal uses a local `ReverseBytes(uint)` because the installed Unity.Mathematics package does not expose `math.reversebytes`.
Rejected Alternatives: Raw struct dumping was rejected because it is not a defensible binary contract. Keeping a call to nonexistent `math.reversebytes` was rejected after Roslyn probe failure proved the current package surface.
Scalability potential: All quality tiers and future BRG/LOD importers can bulk-read one stable binary payload format instead of parsing JSON or guessing host endianness.
Hardware Impact: Runtime cost is 0us in this domain. Future importers avoid silent byte-order corruption; editor write cost is bounded to 64 bytes plus 128 bytes per baked record.

## Decision 032: Explicit little-endian black-box dump

Problem: `Dump_SHINOBU_213.bin` still wrote the 300-row black-box ring as raw NativeArray memory, inheriting host-endian representation.
Solution: Serialize each 64-byte telemetry row field-by-field into stack spans, oldest-to-newest by ring cursor, with the same explicit little-endian writer used for manifest payloads.
Rejected Alternatives: Keeping raw forensic dumps was rejected because binary autopsy files should be stable across machines and tools, even if they are editor-only.
Scalability potential: All quality tiers keep fixed forensic coverage without runtime memory ownership; QA dump parsers can consume one deterministic byte order.
Hardware Impact: Runtime cost is 0us. Dump cost is cold and bounded at 300 * 64 = 19,200 bytes.

## Decision 033: Proof wording cannot outrun compiler evidence

Problem: The XML self-audit and architecture note still reported a plain Roslyn pass after endian writer edits that had not yet received a post-fallback probe because the host CPU gate was closed.
Solution: Downgrade wording to `PRE_ENDIAN_PASS_RECHECK_PENDING` until a scoped Roslyn probe runs against the current explicit-endian source.
Rejected Alternatives: Leaving the older pass unqualified was rejected because it would make the proof artifact stronger than the current evidence.
Scalability potential: No runtime behavior change; evidence discipline protects the multi-agent compile wall and avoids false readiness claims.
Hardware Impact: Runtime cost is 0us. Developer-machine impact is reduced wasted rebuild pressure while CPU remains saturated.

## Decision 034: Self-audit generator owns the proof text

Problem: The XML artifact was corrected, but `OfflineGeometrySelfAudit.cs` would regenerate the stale plain `PASS` Roslyn probe status on the next editor report write.
Solution: Patch the generator to emit `PRE_ENDIAN_PASS_RECHECK_PENDING` until the current explicit-endian source receives a scoped probe.
Rejected Alternatives: Treating the XML artifact as authoritative was rejected because the menu command rewrites it from C#.
Scalability potential: No runtime behavior change; report generation remains deterministic across weak and high-end editor machines.
Hardware Impact: Runtime cost is 0us. Editor impact is one string literal change.

## Decision 035: Untracked files need an explicit whitespace scan

Problem: `git diff --check` returned clean, but the SHINOBU_213 source/meta/docs are still untracked, so that command did not prove trailing-whitespace cleanliness for new files.
Solution: Run a direct owned-file line scan for trailing whitespace and conflict markers, trim only SHINOBU_213 `.meta` trailing spaces, and re-run the scan clean.
Rejected Alternatives: Treating empty `git diff --check` output as sufficient was rejected because it ignores untracked files.
Scalability potential: No runtime behavior change; Unity meta determinism remains intact without whitespace churn.
Hardware Impact: Runtime cost is 0us. Developer impact is lower import/source-control noise.

## Decision 036: Telemetry timings must classify work correctly

Problem: `serializationMs` included LOD1/LOD2 decimation because the stopwatch started before `BuildLodMesh` and was read after mesh asset save/load.
Solution: Stop and accumulate LOD1/LOD2 `BuildLodMesh` durations into `ExtractionMilliseconds`, then restart the stopwatch only around `SaveOrReplaceMesh` and `AssetDatabase.LoadAssetAtPath`.
Rejected Alternatives: Leaving the metric mislabeled was rejected because Task 15 uses the report as proof, and false timing categories corrupt optimization decisions.
Scalability potential: No runtime behavior change; designers get truthful offline bake cost distribution across Low/Middle/High/Ultra profiles.
Hardware Impact: Runtime cost is 0us. Editor report fidelity improves; exact microseconds depend on asset library and pending profiler/import proof.

## Decision 037: Bounded support hull limit is real

Problem: The forge exposed `ConvexHullVertexLimit`, but the fallback hull path still behaved as a minimum box-like support hull, making the UI control and Task 07 proof too weak.
Solution: Generate deterministic support points from a fixed direction table, honor the requested hull limit up to 32 vertices, enumerate outward hull triangle indices offline, and use those indices for SceneView preview. Primitive sphere/box remains the first path.
Rejected Alternatives: Full Quickhull remains rejected because malformed artist meshes can produce fragile topology and the runtime goal is to escape concave PhysX, not chase perfect offline collision. Always-box fallback was rejected because elongated or diagonal silhouettes lose too much contact shape when the primitive fit has already failed.
Scalability potential: Low quality still accepts more sphere/box lies through tolerance; Middle/High/Ultra can permit bounded convex fallback up to 32 points for better authored collision while keeping runtime contact complexity fixed and tiny.
Hardware Impact: Runtime convex fallback remains capped at 32 support vertices instead of source-triangle complexity. i3/MX350 avoids concave MeshCollider cost; offline face enumeration is bounded by 32^3 candidate triples and does not enter gameplay.

## Decision 038: Coplanar hull faces need one fan, not every triple

Problem: The bounded hull face pass accepted every supporting triple. On coplanar faces this can emit redundant triangle combinations instead of one deterministic triangulated face surface.
Solution: Track emitted supporting planes in a local fixed `FixedList4096Bytes<float4>`, collect coplanar support vertices into `FixedList512Bytes<int>`, angular-sort them around the face center, and emit one outward fan per plane.
Rejected Alternatives: Leaving all coplanar triples was rejected because it bloats collider/preview index streams and weakens topology proof. Managed lists or full Quickhull remain rejected because this job must stay Burst-friendly and fixed-bound.
Scalability potential: Low still collapses to sphere/box more often; Middle/High/Ultra receive cleaner bounded convex fallback when primitives fail, without allowing unbounded source-triangle collision.
Hardware Impact: Runtime remains capped at <=32 support vertices. Offline hull face discovery is still fixed-bound O(32^3), but emitted indices are reduced to one fan per supporting plane, cutting redundant MeshCollider import work and preview draw lines.

## Decision 039: Hull failure must collapse to Box, not garbage mesh

Problem: If the bounded hull job ever returned too few vertices or indices, the mesh builder could clamp counters upward and serialize uninitialized vertex/index data.
Solution: Treat hull output with fewer than 4 vertices or 12 triangle indices as invalid, return no hull mesh, and author a conservative BoxCollider from the primitive fit bounds with a warning flag.
Rejected Alternatives: Forcing minimum counters was rejected because it hides corrupt topology and can create invalid collider assets. Throwing an exception was rejected because a single malformed artist mesh should not abort a whole folder bake.
Scalability potential: Low already prefers primitives; Middle/High/Ultra get bounded hulls when valid, and malformed inputs collapse to cheap physics rather than blocking the bake.
Hardware Impact: Runtime avoids invalid convex MeshCollider assets and uses BoxCollider O(1) fallback on bad hulls. Editor cost is a small branch after the hull job.

## Decision 040: Hull asset binding failure must not create null MeshCollider

Problem: A generated hull mesh can still fail to reload from `AssetDatabase.LoadAssetAtPath` after serialization, leaving a MeshCollider with a null shared mesh if the path/import step fails.
Solution: Reload the saved hull asset before adding `MeshCollider`; if the asset is absent, author the same conservative BoxCollider fallback and set a distinct warning flag.
Rejected Alternatives: Trusting `SaveOrReplaceMesh` blindly was rejected because editor asset database failures should degrade to primitive collision, not invalid prefab state.
Scalability potential: Low through Ultra tiers retain static primitive-first collision. A failed editor asset bind never forces runtime collision complexity or folder-bake aborts.
Hardware Impact: Runtime avoids null MeshCollider state. Editor overhead is one cold null check and no measurable gameplay cost.

## Decision 041: Hull support vertices are read-write job state

Problem: `GenerateConvexHullJob.HullVertices` was annotated `[WriteOnly]` while duplicate support elimination, face discovery, and fan triangulation read previously written support points.
Solution: Remove `[WriteOnly]` from `HullVertices` and keep `[NoAlias]`; the buffer is now explicitly read-write job-local output state.
Rejected Alternatives: Splitting support points into separate read/write buffers was rejected because it doubles editor scratch memory for no runtime gain. Leaving `[WriteOnly]` was rejected because Burst safety can reject or misrepresent legal reads.
Scalability potential: All quality tiers keep the same bounded <=32 support vertex cap without extra scratch bandwidth.
Hardware Impact: Runtime cost is 0us. Editor Burst safety metadata now matches memory behavior and preserves vectorization through `[NoAlias]`.

## Decision 042: Hull face tolerance and rsqrt guards must be distance-stable

Problem: Hull face side tests used an unnormalized triangle normal, making coplanar epsilon scale with triangle area. Several normalization helpers also relied on implicit finite behavior before `math.rsqrt`.
Solution: Normalize the candidate plane normal before classifying side distances and guard every `math.rsqrt` path with finite length and `math.max(lenSq, 1e-12f)`.
Rejected Alternatives: Keeping area-scaled side tests was rejected because large triangles can classify nearly coplanar support points inconsistently. Relying on NaN comparison fallthrough was rejected because the code should state the safety invariant directly.
Scalability potential: Low through Ultra tiers get stable bounded hull topology; higher hull limits do not amplify NaN or coplanar tolerance drift.
Hardware Impact: Runtime cost is 0us. Editor adds a fixed rsqrt per candidate plane but reduces invalid hull fallout and keeps malformed assets in the fail-closed primitive path.

## Decision 043: Decimator index streams fail closed before unsafe reads

Problem: Source vertex indices were clamped, but a corrupt submesh descriptor could still produce an index-buffer base outside the imported index stream before the raw pointer vertex read boundary.
Solution: Clamp every selected index-buffer base against `Indices.Length - 3` in both UInt16 and UInt32 decimation jobs, and emit a deterministic zero/up-normal triangle if the index stream, range table, source vertex count, or position pointer is invalid.
Rejected Alternatives: Trusting Unity importer descriptors was rejected because this offline forge must survive malformed art inputs without unsafe memory access. Throwing inside the Burst job was rejected because one bad mesh should not abort a folder bake when a visible degenerate fallback can be flagged by metrics.
Scalability potential: Low through Ultra tiers keep the same continuous LOD budgets; corrupt source data collapses to inert geometry instead of causing editor instability.
Hardware Impact: Runtime cost is 0us. Editor cost is a few integer clamps per selected triangle, bounded and cheaper than a failed bake or undefined unsafe read on weak developer hardware.

## Decision 044: Mock benchmark asset reload must be authoritative

Problem: The mock benchmark mesh path reused the same `SaveOrReplaceMesh` helper as production LOD output. On replacement, that helper destroys the transient mesh after `CopySerialized`, so returning the transient mesh when `AssetDatabase.LoadAssetAtPath` fails can hand callers a destroyed reference.
Solution: Return only the reloaded mock mesh asset. If the editor asset database cannot bind the generated asset, the benchmark returns null instead of a destroyed transient object.
Rejected Alternatives: Returning the transient fallback was rejected because it is valid only on create, not replace. Special-casing replacement state was rejected as unnecessary because the menu caller does not consume the mesh reference.
Scalability potential: No runtime tier impact. It keeps the offline stress-test artifact path deterministic across repeated Low/Middle/High/Ultra benchmark runs.
Hardware Impact: Runtime cost is 0us. Editor correctness improves; no measurable normal-case bake cost.

## Decision 045: `.h8lod` manifest needs a binary-ledger boundary

Problem: SHINOBU_213 emits `offline_lod_manifest.h8lod`, but the binary payload ledger did not name the owner, layout, endian contract, or non-runtime authority boundary for that payload.
Solution: Add a SHINOBU_213 entry to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` documenting the 64-byte header, 128-byte records, explicit little-endian writer, immutable editor-output ownership, no Vault buffer, and no rollback truth.
Rejected Alternatives: Leaving the architecture note as the only documentation was rejected because the binary ledger is the cross-domain payload inventory. Adding a Vault buffer reservation was rejected because this baker owns no runtime mutable fact.
Scalability potential: Low/Middle/High/Ultra runtime consumers can import one flat manifest through their own owner lane without coupling to the editor baker or parsing JSON.
Hardware Impact: Runtime cost is 0us until another owner imports the payload. Future readers avoid byte-order ambiguity and false ownership.

## Decision 046: Hot geometry DTO layouts must be explicit

Problem: The latest safety pass hardened `LodConfigurationDTO`, telemetry, and manifest rows, but several editor hot/job geometry structs still relied on default sequential layout.
Solution: Convert `OfflineGeometryRawVertex`, `OfflineGeometryVertex32`, `OfflineSubMeshRange`, and `OfflinePrimitiveFitResult` to explicit layouts and validate their exact sizes in `OfflineGeometryVertexLayoutValidator.ValidateStructs`.
Rejected Alternatives: Relying on default sequential layout was rejected because the audit mandate requires byte-for-byte ARM64 proof and future field edits could silently alter stride or padding. `[StructLayout(Pack=1)]` remains rejected because it risks unaligned loads on ARM64.
Scalability potential: Low/Middle/High/Ultra bakes now use the same fixed source vertex, output vertex, submesh range, and primitive-fit rows. The quality curve still changes budgets and saliency work, not ABI layout.
Hardware Impact: Runtime cost is 0us because this is editor-only output generation. Editor Burst kernels get stable 32-byte vertex rows, 16-byte range rows, and a 40-byte primitive fit row aligned to an 8-byte multiple.
