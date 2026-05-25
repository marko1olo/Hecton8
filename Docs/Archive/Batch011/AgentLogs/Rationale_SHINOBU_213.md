# Rationale_SHINOBU_213

Date: 2026-05-20
Agent: SHINOBU_213
Domain: OFFLINE_LOD_AND_COLLIDER_BAKER
Status: PENDING VERIFICATION / PRE-ENDIAN ROSLYN PROBE PASS / POST-ENDIAN BOUNDED-HULL-ASSET-BIND-SAFETY-INDEX-HOT-STRUCT-STREAM-BOUNDS-HULL-FALLBACK-JOB-GUARDS-LAYOUT-LANES-HULL-CONTAINMENT-TRANSFORM-MESH-FIT-RANGE-BLACKBOX-FINITE-SOURCE-PER-LANE-AUDIT-FAILED-ATTEMPT-HULL-COUNTER-CLEAR-MIN8-PREFAB-SAVE-ASSET-PATH-CSV-ROOT-ATOMIC-WRITE-CSV-SCHEMA-MESH-TRANSFER-RENDERER-BRIDGE-LOD-ASSET-BIND-HULL-FAN-OVERFLOW-SENTINEL-CSV-SHORT-READ-SENTINEL-FAILFAST-LOD-MESH-OWNER-FADE-WIDTH-JSON-ESCAPE-CSV-ROW-STRICT-BLACKBOX-NO-TOSTRING-JOB-PROFILER-FIXEDSTRING-REPORT-HASH-GUARDS PROBE GATED BY CPU=99.8 / UNITY IMPORT AND PROFILER PENDING

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

## Decision 047: Decimator raw-stream and output-lane guards

Problem: The decimator already clamped corrupt index bases, but optional normal/UV pointers and output vertex lanes still relied on caller-correct flags and scheduling.
Solution: Add null/stride/offset guards to every raw source stream accessor and output length guards to zero-triangle and vertex writes in both UInt16 and UInt32 Burst decimation jobs.
Rejected Alternatives: Trusting Unity MeshData flags or the scheduled output length was rejected because imported assets and future caller edits are the fault boundary. Throwing from the job was rejected because malformed geometry should collapse to inert generated triangles and continue the folder bake.
Scalability potential: Low/Middle/High/Ultra keep the same continuous budgets and saliency windows; bad input collapses deterministically instead of changing method or adding a runtime repair route.
Hardware Impact: Runtime cost is 0us. Editor hot path adds a few integer branches around unsafe reads/writes and prevents undefined memory access on weak developer machines.

## Decision 048: Hull box fallback scratch bounds

Problem: `GenerateConvexHullJob.WriteBoxHull` wrote 8 support vertices and 36 indices directly, relying on the current fixed scratch allocation.
Solution: Guard hull vertex and index capacities before writing the conservative box fallback; invalid scratch state zeroes the hull counters and lets collider authoring fall back to `BoxCollider`.
Rejected Alternatives: Trusting the current 32/2048 scratch allocation was rejected because future caller edits could break Burst memory safety. Throwing was rejected because a malformed editor scratch state should produce no hull, not abort the whole folder bake.
Scalability potential: Low/Middle/High/Ultra keep the same primitive-first and bounded-hull policy. This hardens failure behavior without introducing a quality-tier branch.
Hardware Impact: Runtime cost is 0us. Editor fallback path adds two capacity checks and prevents out-of-bounds writes on weak developer hardware.

## Decision 049: Burst job denominator and collection guards

Problem: `GenerateMockHighPolyMeshJob.Execute` used modulo/division by `LongitudeSegments` before validating the segment count, and small write-only jobs still relied on caller-correct NativeArray creation and matching schedule lengths.
Solution: Reject invalid mock segment counts before modulo/division, guard mock writes, decimator output writes, pack writes, and index writes against default or mismatched NativeArray lanes.
Rejected Alternatives: Trusting menu constants and schedule lengths was rejected because the offline tool is a fault boundary for CI and future editor callers. Throwing was rejected because invalid editor setup should produce no generated rows, not a Burst exception path.
Scalability potential: Low/Middle/High/Ultra quality settings still control budgets continuously. These guards do not introduce tier branches; they only close malformed scheduling and denominator faults.
Hardware Impact: Runtime cost is 0us. Editor normal case adds fixed integer guards and removes divide-by-zero/out-of-bounds failure surfaces on weak developer hardware.

## Decision 050: MeshData vertex lanes must validate offset plus width

Problem: `TryResolveVertexLayout` validated vertex attribute format and dimension, but raw pointer jobs also need proof that each lane fits inside the stream stride before `UnsafeUtility.AsRef<T>` reads `float3` or `float2`.
Solution: Add `IsStreamLaneValid(stride, offset, laneBytes)` and reject position streams whose `offset + 12` exceeds stride. Optional normal and UV0 streams are disabled if their lanes do not fit.
Rejected Alternatives: Trusting Unity importer metadata alone was rejected because malformed imported meshes are the fault boundary. Passing invalid optional streams into Burst and relying on read-time defaults was rejected because the pointer arithmetic would still have invalid stride/offset metadata.
Scalability potential: Low/Middle/High/Ultra keep identical continuous LOD budgets; invalid optional source lanes degrade to face normals or zero UVs without changing quality ownership.
Hardware Impact: Runtime cost is 0us. Editor cold layout resolution adds three integer checks and prevents unsafe lane reads on weak developer hardware.

## Decision 051: Support hulls must prove containment or become boxes

Problem: A fixed-direction support hull can under-enclose the source mesh when an extreme vertex lies between sampled directions, which would create a convex MeshCollider smaller than the visual geometry.
Solution: After plane-deduped face generation, validate every finite source vertex against every emitted outward hull plane. If any point is outside or the side test is non-finite, return zero hull indices so the existing conservative BoxCollider fallback is authored.
Rejected Alternatives: Trusting fixed support directions was rejected because it weakens collision truth. Expanding planes without regenerating mesh topology was rejected because it would desynchronize the authored vertices from the collider faces.
Scalability potential: Low/Middle/High/Ultra keep primitive-first collision and bounded hull limits. Complex shapes that cannot be safely enclosed by the support hull degrade to the box lie instead of a false precise collider.
Hardware Impact: Runtime cost is 0us. Editor hull generation adds a bounded `sourceVertexCount * emittedPlaneCount` containment pass only after primitive fitting rejects; it prevents under-sized collision meshes.

## Decision 052: Editor transform and mesh creation must fail closed

Problem: Relative transform copying could feed non-finite, zero-length, or near-parallel basis vectors into `Quaternion.LookRotation`, and a failed `CreateUnityMesh` call after decimation could leave a created raw vertex buffer for the caller to leak on the LOD0 path.
Solution: Sanitize position and scale lanes, orthogonalize the up vector against the forward vector, select a deterministic fallback axis before `Quaternion.LookRotation`, return null for invalid mesh/range lanes, and dispose created raw vertices immediately when mesh creation fails. Primitive fitting now also finite-guards inverse count, radius, tolerance, and emitted error.
Rejected Alternatives: Trusting imported transform matrices and Unity warning behavior was rejected because editor tools must survive malformed prefabs without hidden console errors. Throwing on mesh creation failure was rejected because a folder bake should skip the bad asset and continue.
Scalability potential: Low/Middle/High/Ultra outputs keep identical continuous LOD and collider policy. Malformed source transforms or mesh lanes collapse to skipped static output instead of introducing runtime repair state.
Hardware Impact: Runtime cost is 0us. Editor normal case adds fixed scalar checks; failure case avoids invalid prefab transforms, leaked native scratch, and NaN collider-fit metrics.

## Decision 053: Descriptor and blackbox proof rows must not hide faults

Problem: `CreateUnityMesh` counted any positive submesh target as valid, even if a malformed range row would produce an index span outside the generated vertex buffer. The mock benchmark also passed a null save path into asset loading if mesh creation failed, and blackbox rows sanitized non-finite metrics before dumping without marking the row itself.
Solution: Validate submesh range index spans before `SetSubMesh`, fail the mock benchmark before `AssetDatabase.LoadAssetAtPath` when mesh save returns no path, and OR warning bit `0x80000000` into blackbox telemetry before state-hash/dump when any metric lane is non-finite.
Rejected Alternatives: Trusting locally generated ranges was rejected because future editor callers can mutate this path. Silent blackbox sanitation was rejected because the forensic row must prove the fault, not erase it.
Scalability potential: Low/Middle/High/Ultra outputs keep the same continuous LOD policy; malformed editor data skips unsafe descriptors and records a proof bit instead of adding runtime corrective state.
Hardware Impact: Runtime cost is 0us. Editor normal case adds bounded integer checks; failure case avoids invalid submesh descriptors, null asset loads, and unflagged forensic dumps.

## Decision 054: Hull containment proof must require finite source evidence

Problem: `AllSourceVerticesInside` skipped non-finite source vertices and could return true when every source vertex was non-finite, allowing a support hull to survive containment with no valid source evidence.
Solution: Track `hasFiniteSourceVertex` during containment validation and reject the hull if no finite source vertex was tested against the emitted planes.
Rejected Alternatives: Trusting earlier source extraction and primitive fitting was rejected because containment is the last collision safety gate before convex MeshCollider authoring.
Scalability potential: Low/Middle/High/Ultra keep the same primitive-first and bounded-hull policy; invalid source data collapses to the conservative box lie without introducing a quality-tier branch or runtime repair path.
Hardware Impact: Runtime cost is 0us. Editor normal case adds one boolean write per finite source vertex; failure case prevents accepting a collider hull proven against an empty finite source set.

## Decision 055: Static proof must encode evidence class and fault lane

Problem: The self-audit used unconditional task `PASS` while compile/import/profiler proof is still gated, and black-box non-finite telemetry only set a generic high warning bit after sanitizing metric lanes.
Solution: Emit task reconciliation as `STATIC_SOURCE_PASS` with explicit pending compile/import/profiler verification, then encode black-box non-finite lanes as `0x40000000` extraction, `0x20000000` serialization, `0x10000000` LOD1 threshold, `0x08000000` LOD2 threshold, `0x04000000` quality, and `0x02000000` depth, with `0x80000000` as the aggregate bit. Fold raw double/float bits for faulted lanes into the telemetry `StateHash` before sanitized row serialization.
Rejected Alternatives: Leaving task `PASS` was rejected because it conflates source conformance with Unity import and profiler proof. A generic non-finite bit was rejected because it cannot identify the failing lane during dump autopsy.
Scalability potential: Low/Middle/High/Ultra runtime output remains unchanged; evidence quality improves without runtime owner state or tier branches.
Hardware Impact: Runtime cost is 0us. Editor black-box recording adds fixed branch/hash work only when metrics are recorded; non-finite rows become attributable without increasing dump row size.

## Decision 056: Architecture proof boundary must match current evidence class

Problem: The SHINOBU_213 architecture note still described the pending post-endian probe as ending at finite-source containment, while the current code/docs now include per-lane blackbox fault encoding and self-audit evidence-class correction.
Solution: Update the architecture note's compile-boundary paragraph to include blackbox per-lane fault encoding and self-audit evidence-class correction in the same pending probe scope.
Rejected Alternatives: Leaving the older wording was rejected because it would let future readers treat the architecture document as narrower than the actual modified source surface.
Scalability potential: No runtime quality-tier change; Low/Middle/High/Ultra output remains governed by the same continuous bake policy and static artifact ownership.
Hardware Impact: Runtime cost is 0us. Documentation correction prevents false readiness claims and does not widen compile dependencies.

## Decision 057: Failed bake attempts must enter the black-box ring

Problem: `BakeAsset` recorded telemetry only after successful prefab serialization, so missing source prefabs, invalid mesh lanes, failed asset binding, or other mid-bake exits could leave no 300-row forensic trace for the failing attempt.
Solution: Seed base metrics before source prefab load, record missing-source failures immediately, and record any unrecorded mid-bake failure in the finalizer with `WarningBakeAttemptFailed` while keeping failed attempts out of the manifest/report success list.
Rejected Alternatives: Adding failed attempts to `LOD_OPTIMIZATION_REPORT.json` and `.h8lod` was rejected because those artifacts describe generated immutable outputs, not failed work. Chat-only failure notes were rejected because the black-box ring is the required proof artifact.
Scalability potential: Low/Middle/High/Ultra output policy is unchanged; failure telemetry now captures the same continuous quality/depth settings that influenced the attempted bake.
Hardware Impact: Runtime cost is 0us. Editor failure paths add one fixed 64-byte ring write and no successful-asset manifest pollution.

## Decision 058: Invalid support hulls must not become mesh boxes

Problem: `GenerateConvexHullJob` could synthesize an 8-vertex convex box mesh when support hull topology was underpopulated, under-contained, or sourced from all-nonfinite vertices, while current proof text claimed `BoxCollider` fallback.
Solution: Clear hull vertex/index counters for invalid support hulls and let `BuildConvexHullMesh` return null, which routes through the existing `AddFallbackBoxCollider` primitive path with warning flags.
Rejected Alternatives: Binding a convex box `MeshCollider` was rejected because it is still a mesh collision shape when the intended Dear Lie is an O(1) primitive collider. Keeping the documentation weaker was rejected because the code path is now cheaper and more explicit.
Scalability potential: Low devices avoid a needless convex mesh shape on malformed assets; Middle/High/Ultra still get bounded support hulls only when the topology proves finite-source containment.
Hardware Impact: Runtime cost is 0us for valid primitive/hull outputs. Malformed fallback removes MeshCollider cooking/contact overhead and keeps the path to a `BoxCollider` primitive.

## Decision 059: Support hull lower bound must match proof text

Problem: Static audit found that the source still accepted four-vertex support hulls while task and architecture proof text promised bounded 8..32 support hulls.
Solution: Add `MinHullVertexCount = 8` and enforce it in `GenerateConvexHullJob`, `BuildConvexHullMesh`, SceneView preview return, UI clamp, CSV clamp, and hull capacity resolution.
Rejected Alternatives: Downgrading proof text to `4..32` was rejected because tetrahedral hull acceptance weakens the collider-quality contract and makes malformed sparse support sets look like valid convex MeshCollider output.
Scalability potential: Low devices now skip sparse mesh-collider hulls and use the primitive BoxCollider lie; Middle/High/Ultra still receive bounded support hulls only after at least eight unique finite supports and containment proof.
Hardware Impact: Runtime cost is 0us. Failure path avoids convex MeshCollider cooking/contact overhead for sparse support sets; editor normal case adds a constant integer threshold check.

## Decision 060: Prefab save success must be authoritative

Problem: The baker recorded a successful metric row immediately after `PrefabUtility.SaveAsPrefabAsset` without checking the save success flag, so a failed prefab save could pollute reports and the `.h8lod` success manifest.
Solution: Use the Unity 6 `SaveAsPrefabAsset(..., out prefabSaved)` overload, return false on generated prefab save failure, set `WarningPrefabSaveFailed`, and let the existing failed-attempt blackbox finalizer record the forensic row. The source-prefab repair menu also checks `out saved` and returns zero repairs if the save fails.
Rejected Alternatives: Trusting the returned prefab object was rejected because asset editing windows can delay import and return null despite success; ignoring the result was rejected because manifest records must describe only generated immutable outputs.
Scalability potential: Low/Middle/High/Ultra output ownership remains unchanged; failed editor saves produce blackbox-only telemetry rather than runtime payload facts.
Hardware Impact: Runtime cost is 0us. Editor normal case adds one boolean check per save; failure case prevents a bad generated prefab path from entering manifest/report consumers and prevents a repair count from claiming unsaved source-prefab edits.

## Decision 061: Mesh save paths and CSV roots must fail closed

Problem: `SaveOrReplaceMesh` trusted caller-provided asset paths before deriving the asset folder, and CSV profile loading derived the project root by blindly trimming `/Assets` from `Application.dataPath`.
Solution: Mesh save now rejects null, empty, folderless, or non-`Assets/` target folders, destroys the transient mesh, and returns null so the existing failed-attempt route owns telemetry. CSV profile loading now verifies the `/Assets` suffix before deriving the project root and falls back to the editor working directory plus default settings if the profile file is absent.
Rejected Alternatives: Trusting current constants was rejected because editor tooling is a fault boundary for CI and future menu callers. Throwing on bad paths was rejected because a folder bake should fail the current asset without breaking unrelated assets.
Scalability potential: Low/Middle/High/Ultra output policy is unchanged; invalid authoring paths do not create runtime payload state, dangling mesh references, or shadow ownership.
Hardware Impact: Runtime cost is 0us. Editor normal case adds a few string/folder checks; failure case prevents native mesh object leaks and invalid manifest/report publication.

## Decision 062: CSV schema must be explicit, not positional faith

Problem: Existing CSV profile files were parsed positionally after skipping the first line, so reordered or malformed headers could silently corrupt LOD ratios, tolerance, hull limits, quality, and depth settings.
Solution: Add a 1 MiB CSV ceiling, optional UTF-8 BOM skip, and exact ASCII header validation before row parsing. Bad or oversized existing CSV files fail closed to the deterministic default profile.
Rejected Alternatives: Continuing positional parsing was rejected because authoring CSVs are a binary-configuration source boundary. Throwing from the menu path was rejected because missing/bad tuning should not destroy a folder bake session when a deterministic default profile exists.
Scalability potential: Low/Middle/High/Ultra profile math remains continuous; malformed tuning cannot silently push weak-device bakes toward accidental visual overkill or bad collision hull limits.
Hardware Impact: Runtime cost is 0us. Editor normal case adds one bounded header compare; failure case prevents bad generated LOD/collider payloads.

## Decision 063: Generated artifacts must be replaced atomically

Problem: `.h8lod`, JSON/XML reports, and black-box dumps were written directly to final paths, so a crash, domain reload, or disk failure could leave a zero-byte or torn proof/payload artifact.
Solution: Write to same-volume `.tmp`, flush, validate fixed byte counts for `.h8lod` and the 300-row black-box dump, then replace final files with `.bak` preservation. Import happens only after the manifest replacement succeeds.
Rejected Alternatives: `FileMode.Create` on the final artifact was rejected because the final path is a proof boundary. Skipping `.bak` preservation was rejected because the previous proof can aid forensic comparison after a failed editor write.
Scalability potential: Runtime quality tiers are unchanged; artifact integrity is stable for Low/Middle/High/Ultra generated payload consumers.
Hardware Impact: Runtime cost is 0us. Editor writes add one temp file and one same-volume replace; failure case preserves the last good artifact instead of publishing torn bytes.

## Decision 064: Transient mesh ownership must be explicit

Problem: `CreateUnityMesh` and `BuildConvexHullMesh` created Unity `Mesh` objects before upload/layout/submesh validation completed. Exceptions in those calls could leave transient native mesh objects alive without caller or AssetDatabase ownership.
Solution: Add `transferred` guards around main LOD mesh and hull mesh construction. The transient mesh is destroyed in `finally` unless the function returns it successfully. `SaveOrReplaceMesh` now also destroys transient meshes after copy-serialize replacement or failed asset creation.
Rejected Alternatives: Trusting Unity upload calls was rejected because malformed source geometry and import surfaces are the editor fault boundary. Relying on GC/finalizers was rejected because Unity native objects are not normal managed memory.
Scalability potential: Low/Middle/High/Ultra output policy is unchanged; malformed assets fail closed without accumulating editor-native mesh memory.
Hardware Impact: Runtime cost is 0us. Editor normal case adds one boolean guard; failure case prevents native mesh memory leaks on weak developer hardware and CI.

## Decision 065: Renderer array bridge must be explicit

Problem: Static scan found three `List<Renderer>.ToArray()` calls in the cold prefab assembly bridge to Unity `LODGroup`, contradicting the zero-LINQ/no-`ToArray` source proof even though the calls were editor-only.
Solution: Replace the calls with `CopyRenderers`, an explicit indexed copy into the `Renderer[]` array Unity requires for `LOD` construction.
Rejected Alternatives: Leaving `ToArray()` was rejected because the project proof surface treats that pattern as forbidden. Inventing a runtime LOD wrapper was rejected because generated prefabs must stay static and script-free.
Scalability potential: Low/Middle/High/Ultra output policy is unchanged; this is a cold editor bridge that preserves the static `LODGroup` artifact route.
Hardware Impact: Runtime cost is 0us. Editor allocation count remains one required `Renderer[]` per LOD level, but the hidden helper call is removed and the static proof now matches the source.

## Decision 066: LOD asset reload must not accept null paths

Problem: `SaveOrReplaceMesh` correctly fails closed and returns null for invalid asset paths, but the main LOD0/LOD1/LOD2 bake path still sent those paths directly into `AssetDatabase.LoadAssetAtPath`.
Solution: Require all three saved LOD asset paths to be non-empty before any asset reload, and set `WarningLodAssetBindFailed` if a path or reloaded asset is missing so the failed-attempt blackbox route owns the proof.
Rejected Alternatives: Trusting constants and Unity's null-path behavior was rejected because the baker is an editor fault boundary and failed mesh saves must not become hidden Unity asset-load warnings.
Scalability potential: Low/Middle/High/Ultra generated output policy is unchanged; failed authoring paths produce blackbox-only telemetry instead of partial prefab state or manifest success rows.
Hardware Impact: Runtime cost is 0us. Editor normal case adds three null/empty string checks and one warning-bit branch; failure case prevents invalid asset-load calls and partial generated prefab assembly.

## Decision 067: Hull face fan overflow must fail closed

Problem: Subagent static audit found `BuildConvexFaces` returned the partial `indexCount` when `AppendFaceFan` ran out of `HullIndices` capacity, allowing a truncated convex hull to bypass containment and survive as a MeshCollider.
Solution: Return zero from the face builder on fan overflow so `GenerateConvexHullJob.Execute` clears hull counters and authoring routes to the primitive BoxCollider fallback.
Rejected Alternatives: Keeping a partial face fan was rejected because a bounded but open hull is worse than the deliberate collision lie; expanding the index buffer was rejected because the current overflow is a topology validity failure, not a quality target.
Scalability potential: Low devices get the cheap primitive fallback under overflow; Middle/High/Ultra still receive bounded convex hulls only when the whole face set fits and containment is proven.
Hardware Impact: Runtime cost is 0us for valid hulls. Overflow failure path avoids invalid MeshCollider cooking/contact behavior and falls back to O(1) BoxCollider.

## Decision 068: NativeMemorySentinel bridge without compile-wall widening

Problem: Subagent static audit found the editor black-box persistent `NativeArray` was not registered with `NativeMemorySentinel`, but directly referencing `Hecton8.Core` from `Hecton8.World.OfflineGeometry.Editor` would widen this domain's assembly references.
Solution: Register and unregister the 300-row persistent ring through a cold reflection bridge that resolves `Hecton8.Core.NativeMemorySentinel` and `NativeAllocationLifetime` only when the sentinel assembly is already loaded.
Rejected Alternatives: Adding a direct `Hecton8.Core` asmdef reference was rejected because the compile guard requires no sibling/core assembly coupling from this editor island. Leaving only a waiver was rejected because the persistent allocation can be registered without a hard dependency.
Scalability potential: Runtime output policy is unchanged. Editor diagnostics gain first-party memory accounting when the sentinel is available while keeping Low/Middle/High/Ultra generated assets identical.
Hardware Impact: Runtime cost is 0us. Editor allocation path adds one cold assembly/type lookup and reflection invocation at ring allocation/disposal; no per-record hot cost.

## Decision 069: CSV reads fail closed and sentinel is mandatory

Problem: The CSV reader could treat a short `FileStream.Read` as a complete profile file, and a missing or rejected `NativeMemorySentinel` registration would leave the persistent black-box ring outside first-party native allocation accounting.
Solution: Require the CSV byte read to match the expected stream length before schema parsing, otherwise fall back to the deterministic default profile. The sentinel reflection bridge remains cold and asmdef-decoupled, but registration failure now disposes the ring and throws rather than publishing an untracked persistent allocation.
Rejected Alternatives: Parsing partial CSV bytes was rejected because authoring profiles are a configuration boundary. Keeping sentinel registration best-effort was rejected after the native-memory mandate was re-read; a persistent allocation without accounting is worse than failing the editor bake setup.
Scalability potential: Low/Middle/High/Ultra bake policy remains continuous; malformed tuning cannot silently bias weak-device geometry toward heavier settings, and missing Core sentinel availability fails before output assets are presented as verified.
Hardware Impact: Runtime cost is 0us. Editor normal case adds one integer equality check after file read and one cold reflection registration; failure paths avoid corrupt profile ingestion and untracked native memory.

## Decision 070: Caller-owned LOD mesh lifetime must survive exceptions

Problem: `CreateUnityMesh` and `SaveOrReplaceMesh` had transfer guards, but `BakeAsset` could own LOD0 or LOD1 transient meshes across later LOD builds; an exception before asset transfer could leak Unity native mesh objects.
Solution: Hoist LOD mesh locals and ownership flags across the multi-mesh bake window. Destroy any caller-owned LOD0/LOD1/LOD2 mesh in `finally` unless `SaveOrReplaceMesh` has already transferred or destroyed it.
Rejected Alternatives: Trusting the happy-path null cleanup was rejected because malformed source meshes and Unity upload/import faults are the editor fault boundary. Relying on Unity object finalization was rejected because native mesh memory is not a normal managed object.
Scalability potential: Low/Middle/High/Ultra output policy is unchanged. Long folder bakes on weak editor hardware no longer accumulate native mesh objects after mid-bake exceptions.
Hardware Impact: Runtime cost is 0us. Editor normal path adds three booleans and null checks; failure path prevents native mesh memory growth during batch bakes.

## Decision 071: Fade width must be continuous if proof claims it

Problem: The self-audit claimed `GlobalQualityWeight` shifted fade widths, but the source used fixed `fadeTransitionWidth` constants.
Solution: Add `ResolveFadeTransitionWidth` using `math.smoothstep`, `math.lerp`, quality, and depth. Low quality/deep sectors use shorter fades to reduce crossfade overdraw; high quality keeps wider fades for smoother visual transitions.
Rejected Alternatives: Removing the proof text was rejected because Task 11 explicitly asks for runtime-ready continuous threshold/fade control. Binary low/high fade widths were rejected because quality must be continuous.
Scalability potential: Low reduces crossfade overdraw and accepts more visible swaps hidden by darkness/thermal pressure; Middle holds moderate fade widths; High/Ultra buys smoother LOD transitions without adding runtime scripts.
Hardware Impact: Runtime cost is 0us beyond Unity's static LODGroup evaluation. Editor cost is a few scalar math operations per generated prefab.

## Decision 072: JSON report strings must escape control bytes

Problem: Report JSON escaping only covered quote and backslash, so control characters in asset paths or detail fields could corrupt `LOD_OPTIMIZATION_REPORT.json` or `PHYSICS_OPTIMIZATION_REPORT.json`.
Solution: Extend `Escape` to encode quote, backslash, newline, carriage return, tab, backspace, form-feed, and any character below `0x20` as JSON-safe escapes.
Rejected Alternatives: Trusting Unity asset paths was rejected because scanner detail strings and future source names are fault inputs. Switching to a managed JSON serializer was rejected to keep the report writer explicit and predictable.
Scalability potential: All quality tiers keep identical generated assets; proof artifacts remain parseable under malformed authoring strings.
Hardware Impact: Runtime cost is 0us. Editor normal report writes only allocate a `StringBuilder` when escaping is required; malformed strings no longer poison downstream tooling.

## Decision 073: Static validation before compile-gated probe

Problem: The latest ownership/fade/json/sentinel edits need evidence, but the protocol forbids build/probe work while CPU is above 50 percent.
Solution: Run scoped `rg`, asmdef, conflict-marker, and `git diff --check` scans against owned SHINOBU_213 files only, then leave Roslyn probe queued behind the CPU/dotnet gate.
Rejected Alternatives: Launching Roslyn or `dotnet build` at `CPU=100.0` was rejected because it violates the command discipline gate and risks false build failures on the shared workstation.
Scalability potential: Runtime output is unchanged; this protects the proof surface without widening dependencies or consuming thermal/IO headroom.
Hardware Impact: Runtime cost is 0us. Editor verification cost is bounded static file scanning; no compiler worker was spawned.

## Decision 074: CSV row schema must fail closed

Problem: Header validation was strict, but individual CSV rows could still fall through with missing or malformed cells and partially defaulted profile fields.
Solution: Replace fallback cell readers with `TryReadProfileRow`, exact eight-cell parsing, strict integer/float token checks, and whole-file default fallback on any bad row.
Rejected Alternatives: Per-field fallback defaults were rejected because a malformed profile can silently change generated triangle budgets, primitive tolerance, hull limits, quality, or depth behavior.
Scalability potential: Low/Middle/High/Ultra quality recipes remain designer-controlled only when the row is structurally valid; malformed rows cannot accidentally push low-tier bakes into visual-overkill density.
Hardware Impact: Runtime cost is 0us. Editor normal path adds bounded byte-token validation; failure path prevents bad generated mesh/collider payloads.

## Decision 075: Black-box hash and sentinel paths must not allocate or drift

Problem: Black-box telemetry hashed `FixedString` paths through managed `ToString`, and the torn-dump exception also formatted numbers via `ToString`; the sentinel bridge also needed fail-fast proof without widening the asmdef.
Solution: Hash `FixedString128Bytes` by byte index, remove numeric `ToString` from the torn-dump exception, and keep mandatory `NativeMemorySentinel` register/unregister behind cold reflection with disposal-before-throw on failure.
Rejected Alternatives: Keeping `ToString` in editor-only black-box code was rejected because the proof surface explicitly claims fixed, unmanaged telemetry rows. Adding a direct `Hecton8.Core` reference was rejected because it widens the compile wall.
Scalability potential: Runtime output policy is unchanged; Low/Middle/High/Ultra generated assets keep the same immutable payloads while black-box telemetry remains fixed-size and allocation-disciplined.
Hardware Impact: Runtime cost is 0us. Editor record path avoids managed path-string allocation during telemetry hashing; failure path stops rather than leaking an unregistered persistent ring.

## Decision 076: Same-frame editor fences need profiler evidence hooks

Problem: The baker intentionally schedules and completes editor-only jobs in the same call stack, but those blocking fences had no named profiler markers for later proof.
Solution: Wrap mock generation, preview fit/hull, decimation, mesh packing, collider primitive fit, and collider hull fences in static `ProfilerMarker` scopes.
Rejected Alternatives: Removing the same-frame fences was rejected because Unity mesh/prefab authoring needs immediate results in an editor command. Leaving anonymous fences was rejected because the native-jobs mandate requires evidence for blocking sync points.
Scalability potential: Runtime output policy is unchanged; profiler markers let low-tier editor hardware identify which offline stage must be further reduced or moved to a longer bake window.
Hardware Impact: Runtime cost is 0us. Editor cost is marker begin/end around already blocking authoring fences; actual fence duration remains profiler-pending.

## Decision 077: Metric path strings should not be materialized for manifest/report hot loops

Problem: After black-box hashing was fixed, the batch report and `.h8lod` manifest path hash still materialized `OfflineBakeMetrics.SourcePath` and `OutputPath` with `FixedString128Bytes.ToString()` inside per-metric loops.
Solution: Add `StableHash(in FixedString128Bytes)` for manifest hashes and `AppendEscapedFixedString` for JSON item path emission. ASCII asset paths now hash/append by byte index; non-ASCII paths still fall back to the existing escaped string path because report text must preserve authored path meaning.
Rejected Alternatives: Claiming editor-only status and leaving per-metric `ToString()` was rejected because this path runs across every baked asset and feeds proof/payload artifacts. Replacing all Unity UI/report string materialization was rejected because `DropdownField`, final file writes, debug logs, and exception text are cold managed editor API boundaries.
Scalability potential: Low/Middle/High/Ultra generated assets are unchanged; large folder bakes avoid avoidable per-metric path-string materialization in the proof and binary-manifest path.
Hardware Impact: Runtime cost is 0us. Editor saving is bounded to two fixed-string hash conversions and two JSON path conversions per successful metric row; exact microseconds remain profiler-pending.
