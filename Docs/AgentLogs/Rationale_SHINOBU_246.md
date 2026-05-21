# Rationale_SHINOBU_246

Agent: SHINOBU_246
Domain: Echelon 2 World Generation / offline editor seam baking
Status: ACTIVE / SOURCE HARDENED / PENDING VERIFICATION

## Decision 00 - Scope Lock
Problem: The seam task touches voxel meshes, terrain meshes, editor tooling, and reports. Runtime deformation would violate the prompt and hot-path doctrine.
Solution: Keep stitching under editor/offline paths only, with Burst jobs operating on native scratch and baked `.mesh` outputs.
Rejected Alternatives: Runtime MonoBehaviour vertex alignment, scene `Update()` deformation, or material-only gap masking. They move static geometry correction into runtime CPU/GPU cost and do not mathematically seal the seam.
Scalability potential: Low uses smaller preview/query budgets and baked assets only; Middle increases editor batch size; High increases preview density; Ultra can bake richer normal/color transition metadata without changing gameplay truth.
Hardware Impact: MX350/i3 avoids all runtime seam solve cost. Expected runtime saving is the full avoided per-frame deformation/query path; exact microseconds remain PENDING VERIFICATION until static scan and profiler proof exist.

## Decision 01 - Mandate Set
Problem: Seam binding requires AUP precision, voxel pipeline compatibility, ARM64 layout discipline, and editor/designer facade control.
Solution: Read and apply VOX seam integration, voxel SDF/MC, AUP determinism, coordinate precision, ARM64 layout, native jobs, zero-GC, and designer CSV bridge mandates before coding.
Rejected Alternatives: Reading only the batch prompt or inventing a standalone baker contract. That would miss current source boundaries and global authority rules.
Scalability potential: Mandates define Low/Middle/High/Ultra behavior through continuous quality and LOD-safe pre-bakes instead of binary output.
Hardware Impact: Correct mandate alignment prevents runtime global polling, unsafe native allocations, and unaligned DTO fetches on low-end silicon.

## Decision 02 - Runtime Mutation Scan
Problem: Static seams must not be corrected by `Start()`/`Update()` mesh vertex loops in `Assets/_Project/Scripts/Environment/`.
Solution: Used text scans for `MeshFilter`, `.mesh.vertices`, `sharedMesh.vertices`, `GetVertices`, `SetVertices`, `Start`, `Update`, `Seam`, and `Skirt`. No proven runtime seam deformation dependency was found in the assigned Environment scripts.
Rejected Alternatives: Removing scripts by suspicion or adding a suppression layer. No matching code path existed, and fake deletion would be architectural vandalism.
Scalability potential: Low/Middle/High/Ultra tiers all keep 0 us runtime seam solve; any future correction must enter the offline forge.
Hardware Impact: MX350/i3 retains CPU for visible rendering. Exact saved microseconds are 0 for this scan because no active deformation loop existed to remove.

## Decision 03 - Skirt Purge Boundary
Problem: Hidden skirt meshes waste triangles and overdraw, but deleting assets requires proof.
Solution: Scanned `_Project` prefab, scene, and asset files for `Skirt/skirt`. No target asset was found, so purge result is documented as clean.
Rejected Alternatives: Broad deletion of unrelated cover geometry or name-inference cleanup. That risks destroying authored level content without a seam-specific proof trail.
Scalability potential: Low keeps no cover-up geometry; Middle/High/Ultra spend saved geometry budget on baked normal/color continuity instead of filler polygons.
Hardware Impact: No measured overdraw saving because no skirt artifact was present. Runtime cost remains 0 us added by SHINOBU_246.

## Decision 04 - Raw Seam DTOs
Problem: CS1612 property semantics and managed arrays would create copies or GC pressure inside dense vertex evaluation.
Solution: Added explicit unmanaged seam DTOs with raw public fields and Burst jobs using `NativeArray<T>`, pointers, and `UnsafeUtility.AsRef<T>`.
Rejected Alternatives: `Vector3[]`, classes, auto-properties, LINQ, or per-vertex managed helpers. Those are incompatible with zero-GC Burst loops over hundreds of thousands of vertices.
Scalability potential: Low can use sparse/LOD2 meshes; Middle uses LOD1; High and Ultra increase preview/sample density without changing runtime authority.
Hardware Impact: Low-end editor machines avoid managed allocation spikes during bakes. Exact microsecond saving is pending Unity profiler execution.

## Decision 05 - ARM64 Vertex Contract
Problem: Baked stitched meshes need a deterministic GPU fetch layout across desktop and mobile.
Solution: Fixed stitched vertex layout to 32 bytes: position float3, normal float3, color UNorm8x4, uv0 UNorm16x2. Added an editor validator for offsets, explicit DTO sizes, and stride.
Rejected Alternatives: Relying on default Unity mesh packing or storing auxiliary seam records in loosely packed managed structs. Default layouts are not a durable ARM64 contract.
Scalability potential: Low uses the same aligned layout with fewer vertices; Middle/High/Ultra increase mesh density or transition metadata while preserving 32-byte base fetches.
Hardware Impact: Prevents misaligned memory fetch hazards on i3/MX350-class and mobile-class hardware; runtime microsecond delta is structural and not profiler-measured.

## Decision 06 - Mock Seam Source
Problem: The real terrain/voxel generators may be unavailable while the seam algorithm still needs isolated stress input.
Solution: Added Burst mock generation for two 500x500 overlapping surfaces with deterministic sinusoidal/noise misalignment and matching plane indices.
Rejected Alternatives: Hand-authored test meshes or waiting on Agent 240/244 outputs. Those routes block independent verification and vary by scene state.
Scalability potential: Low can reduce grid size through quality weight; Middle/High/Ultra can drive denser editor stress tests without runtime cost.
Hardware Impact: Mock generation is editor-only. Runtime cost is 0 us; editor benchmark duration remains pending until Unity executes the job.

## Decision 07 - Spatial Hash Instead Of KD Tree
Problem: Terrain and voxel boundaries can be large enough that pairwise vertex search is dead on arrival.
Solution: Built a voxel-boundary `NativeParallelMultiHashMap<long, SeamBoundaryVertex64>` keyed by deterministic AUP cell hashes. Terrain vertices probe adjacent 27 cells only.
Rejected Alternatives: KD-tree objects, managed dictionaries, or full O(N*M) scans. KD-tree setup in managed memory adds complexity and GC; pairwise search is indefensible for 10 km terrain.
Scalability potential: Low uses larger cells and lower LOD meshes; Middle tightens epsilon; High/Ultra can increase boundary density and preview sample count while staying offline.
Hardware Impact: MX350/i3 avoids runaway editor bakes. Runtime microseconds added remain 0 because the hash is not shipped as hot gameplay logic.

## Decision 08 - Double3 AUP Snap Authority
Problem: Local terrain and voxel meshes can sit under different roots, and float-world math loses precision at map edges.
Solution: Terrain and voxel local positions are promoted to `double3` by adding their own AUP roots; snapping is decided in double precision; final mesh writes subtract the terrain root and cast to float3.
Rejected Alternatives: Unity `TransformPoint`, float world coordinates, or snapping in local mesh coordinates. Those paths break when roots differ or coordinates are large.
Scalability potential: Low/Middle/High/Ultra all share the same truth route; quality changes radius/cadence, not coordinate authority.
Hardware Impact: Slight editor arithmetic cost buys deterministic seams. Runtime cost is 0 us because resulting local float3 vertices are baked.

## Decision 09 - Dear Lie Normals And Alpha
Problem: Airtight positions still leave visible lighting and material cuts.
Solution: Averaged terrain and voxel normals at snap results with distance falloff, then wrote vertex alpha gradients near the seam for `UberNoir`-style material transition.
Rejected Alternatives: Additional decals, overlay strips, or runtime normal recalculation. Those increase overdraw/runtime work and do not guarantee exact geometry continuity.
Scalability potential: Low keeps simple averaged normals and coarse alpha falloff; Middle refines falloff; High/Ultra can spend saved runtime budget on richer shader interpretation of the same vertex alpha.
Hardware Impact: Low-end GPUs read one packed color alpha instead of drawing cover geometry. Runtime CPU added is 0 us.

## Decision 10 - Baked Mesh Asset Output
Problem: The runtime must not know the seam was procedurally repaired.
Solution: Pipeline creates new stitched terrain/voxel mesh assets with explicit 32-byte vertex buffers in `Assets/_Project/BakedGeometry/Stitched/` and leaves source meshes untouched.
Rejected Alternatives: Runtime MonoBehaviours, mutable correction buffers, or source asset mutation. Those either add hot-path cost or destroy rollback/debug traceability.
Scalability potential: LOD0/LOD1/LOD2 outputs are independent baked assets; GlobalQualityWeight can crossfade/select LODs without reopening seams.
Hardware Impact: Runtime seam alignment remains 0 us on i3/MX350. Editor serialization cost is pending measurement after Unity execution.

## Decision 11 - Compile Gate Obedience
Problem: The protocol forbids `dotnet`/`csc` builds while CPU is under load or compiler processes are running.
Solution: Checked processes and CPU. No `dotnet/csc` process was observed, but `Win32_Processor.LoadPercentage` returned 100, so compile was deferred.
Rejected Alternatives: Forcing a build to satisfy a checkbox. That violates the explicit integration rule and risks stealing CPU from other agents.
Scalability potential: Parallel-agent environment remains stable; verification resumes only when hardware load drops.
Hardware Impact: Avoided saturating an already loaded machine. Build microseconds are not measured because no build was launched.

## Decision 12 - Continuous LOD Seam Budget
Problem: LOD1/LOD2 topology differs and a binary quality branch would reopen seams during scalability transitions.
Solution: Every LOD is stitched independently. `ResolveLodProfile` continuously expands lower-LOD seam radius, normal blend, texture falloff, and cell size based on `GlobalQualityWeight` and `LodContinuityBias`.
Rejected Alternatives: LOD0-only baking, fixed epsilon for every LOD, or low/high boolean switches. Those either tear during LOD swaps or violate the continuous quality law.
Scalability potential: Low widens tolerance for coarse geometry; Middle moderates it; High and Ultra keep tighter math while spending visual budget on smoother normal/alpha curves.
Hardware Impact: Runtime cost remains 0 us. Low-end devices receive baked low-LOD seams without per-frame repair.

## Decision 13 - AUP Double Route
Problem: Seam candidates can be far from origin and live under distinct terrain/voxel roots.
Solution: Hashing and snapping use `double3 rootAup + localFloat3` for both sources. Only the accepted snapped AUP is converted back to local mesh space.
Rejected Alternatives: local-only snapping, Unity world float transforms, or float hash cells. Those fail at large coordinate magnitudes.
Scalability potential: Low through Ultra keep identical coordinate truth; quality only changes visual tolerance/falloff, not authority.
Hardware Impact: Runtime cost is 0 us. Editor spends double arithmetic once per bake to avoid runtime cracks.

## Decision 14 - Rollback Exclusion Fence
Problem: Static mesh vertex bytes are immutable environment data and must not enter frame-state hashing.
Solution: Wrote a little-endian sidecar rollback exclusion fence for generated stitched mesh assets with terrain/voxel/stitched hashes, `rollbackNetcodeExcluded=true`, `VTSF` magic, version, and endian marker.
Rejected Alternatives: StateRingBuffer/Merkle leaf inclusion, runtime registration, or mesh byte hashing. Static geometry belongs to asset loading, not rollback truth.
Scalability potential: Low/Middle/High/Ultra can swap baked LOD presentation assets while synchronizing gameplay entities through existing authority routes.
Hardware Impact: Avoids megabyte-scale static geometry hashing. Runtime microseconds added by SHINOBU_246 are 0.

## Decision 15 - Uninitialized TempJob Buffers
Problem: Clearing large editor geometry buffers wastes iteration time before jobs overwrite them.
Solution: All large seam buffers are allocated with `Allocator.TempJob` and `NativeArrayOptions.UninitializedMemory`, then deterministically filled by extraction, index, mock, or mask jobs.
Rejected Alternatives: `UnsafeUtility.MemClear`, managed `Vector3[]`, or persistent runtime NativeArrays. They add cost or violate ownership.
Scalability potential: Low uses smaller buffers through selected LODs; Middle/High/Ultra can bake denser meshes without runtime allocation.
Hardware Impact: Editor-only improvement. Exact microsecond savings are pending profiler execution; runtime cost is 0 us.

## Decision 16 - File Reports And Black Box
Problem: Chat output is not a proof artifact, and seam failures need last-state diagnostics.
Solution: Added `SEAM_STITCH_REPORT.json`, `WORLD_OPTIMIZATION_REPORT.json`, and a 300-entry binary telemetry dump path on failure.
Rejected Alternatives: Console-only logs or exceptions without state. Those fail the black-box requirement.
Scalability potential: Low reports basic counters; Middle/High/Ultra can record richer visual diagnostics without changing runtime truth.
Hardware Impact: Report I/O is editor-only. Runtime cost remains 0 us.

## Decision 17 - Editor Forge Facade
Problem: Technical artists need control over meshes, AUP roots, epsilon, normal blend, and texture falloff without entering runtime code.
Solution: Added a UI Toolkit window with mesh object fields for LOD0/1/2 pairs, double AUP fields, continuous sliders, CSV profile selection, stitch action, mock benchmark, preview clear, and runtime scanner action.
Rejected Alternatives: Scene MonoBehaviour controller, inspector-only settings, or command-line-only bake. Those create runtime attachment risk or block artist iteration.
Scalability potential: Low selects coarse LODs and wider tolerance; Middle/High/Ultra adjust the same continuous profile fields without binary quality switches.
Hardware Impact: Editor-only UI. Runtime cost is 0 us.

## Decision 18 - Span CSV Profiles
Problem: Biome seam recipes need designer data without per-field managed string churn during parse.
Solution: Added `seam_binding_profiles.csv` and a byte-span parser using short-lived native scratch, ASCII hash keys, manual float parsing, and fixed profile cache capacity.
Rejected Alternatives: `string.Split`, LINQ, reflection, or ScriptableObject-only profiles. They allocate or hide exact data format.
Scalability potential: Low/Middle/High/Ultra consume the same profile schema; values control radius, blend, falloff, hash cell, LOD bias, and quality continuously.
Hardware Impact: Editor-only parser. Runtime cost is 0 us.

## Decision 19 - Preview Without Mesh Mutation
Problem: Artists need visual seam pull feedback before saving assets, but preview must not alter source meshes.
Solution: Snap results are uploaded into a hidden editor Mesh as terrain-local thick red pull ribbons through short-lived Temp native buffers; no source mesh or scene object is mutated.
Rejected Alternatives: Temporary mesh writes, runtime preview actors, or permanent scene components. They create hidden state and risk runtime leakage.
Scalability potential: Low uses fewer visible lines; Middle/High/Ultra can raise preview density later without touching baked truth.
Hardware Impact: Editor SceneView only. Runtime cost remains 0 us.

## Decision 20 - Runtime Mutation Scanner
Problem: The project needs a repeatable proof that terrain/voxel seam alignment is not occurring in runtime loops.
Solution: Added `Dynamic_Vertex_Scanner` under Editor to scan non-Editor scripts for seam-context `.mesh.vertices`, `GetVertices`, `SetVertices`, and `RecalculateNormals` patterns near runtime loop methods.
Rejected Alternatives: Trusting manual grep or adding a runtime detector. Manual-only proof is not repeatable; runtime detectors add the very hot-path cost being removed.
Scalability potential: Low through Ultra share the same proof route; no gameplay or render quality changes.
Hardware Impact: Scanner is editor-only. Runtime microseconds added are 0.

## Decision 21 - Final Audit Boundary
Problem: Task 20 requires self-audit, but compile execution is forbidden while CPU is saturated.
Solution: Wrote file-backed self-audit and architecture notes with explicit PASS_SOURCE_COMPILE_PENDING status. Marked compile as blocked by CPU gate rather than inventing success.
Rejected Alternatives: Fake compiler report, fake Burst timing, or manual overwrite of another agent's `WORLD_OPTIMIZATION_REPORT.json`.
Scalability potential: Low/Middle/High/Ultra contracts are documented and enforce editor-only seam baking.
Hardware Impact: Runtime cost remains 0 us. Verification will need a later Unity compile when CPU load is below 50 percent and no dotnet/csc process is active.

## Decision 22 - Compile Wall Assembly Isolation
Problem: The new domain source would otherwise fall into broad Assembly-CSharp zones and increase compile-wall blast radius.
Solution: Added `Hecton8.World.VoxelTerrainSeamBinder.asmdef` for DTO/math contracts and `Hecton8.World.VoxelTerrainSeamBinder.Editor.asmdef` for the Forge, jobs, scanner, preview, and AssetDatabase writer. Both are `autoReferenced:false`; runtime references only `Unity.Mathematics`, editor references only owned seam assembly plus Unity Burst/Collections/Jobs/Mathematics.
Rejected Alternatives: Leaving files unscoped, referencing terrain/voxel sibling runtimes, or importing global editor assemblies for convenience. Those routes widen recompiles and create cross-domain coupling.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged because seam solving remains baked. Editor iteration impact is constrained to the owned domain assembly.
Hardware Impact: i3/MX350-class developer machines avoid unnecessary project-wide recompiles when seam tooling changes. Runtime microseconds remain 0 us.

## Decision 23 - AST Scanner Upgrade
Problem: Task 19 demands AST proof; lexical scans can misclassify comments or unrelated text.
Solution: Replaced the primary scanner with a Roslyn `CSharpSyntaxWalker` that inspects invocation/member-access nodes inside runtime methods and seam-context files, with lexical fallback only if parsing fails. The report now records parser mode and parser failure count.
Rejected Alternatives: Keeping string-only grep or adding a runtime mutation detector. String-only proof is weak; runtime detectors add hot-path cost and violate the offline authority boundary.
Scalability potential: The scanner is editor-only and does not change quality output. It preserves Low/Middle/High/Ultra runtime budgets by proving that no seam repair was moved into gameplay loops.
Hardware Impact: Runtime cost remains 0 us. Editor scan cost is cold diagnostic work and must be measured later inside Unity if required.

## Decision 24 - Non-Saving Preview Route
Problem: Previewing seam pull lines by running the save pipeline would mutate generated assets before the artist commits.
Solution: Added `PreviewLod0`, which builds native copies of LOD0 terrain/voxel buffers, runs the Burst spatial-hash/snap path with `publishPreview=true`, and disposes scratch without creating or overwriting mesh assets.
Rejected Alternatives: Temporary source mesh mutation, scene GameObject preview actors, or saving meshes to get preview lines. Those create hidden state or runtime leakage.
Scalability potential: Low uses LOD0 preview with capped 4096 lines; Middle/High/Ultra can raise preview density later through editor-only caps without changing baked runtime truth.
Hardware Impact: Runtime cost remains 0 us. Low-end editor machines pay preview cost only on command, not per gameplay frame.

## Decision 25 - Black-Box Ring Initialization
Problem: A failure before the first completed stitch could dump uninitialized telemetry bytes.
Solution: The 300-entry ring is explicitly initialized on creation and all stage records write through a cursor with frame, stage, warning flags, vertex counts, snapped count, max error, and root AUP.
Rejected Alternatives: Dumping uninitialized `NativeArray` memory or using `UnsafeUtility.MemClear` on all geometry scratch. The former corrupts forensic proof; the latter violates the zero-init overhead requirement for large buffers.
Scalability potential: Telemetry capacity is fixed and quality-independent; richer Ultra diagnostics can add report fields later without changing runtime DTO authority.
Hardware Impact: Runtime cost remains 0 us. Editor initialization cost is 300 fixed 64-byte writes, bounded at 19.2 KiB, not geometry-scale clearing.

## Decision 26 - Verification Gate Discipline
Problem: The polish pass needed proof without violating the explicit build prohibition under high CPU load.
Solution: Ran source-only checks: XML parse for self-audit, JSON parse for asmdefs, precise static scans for DTO properties/LINQ/foreach/MemClear/Persistent allocators/random/time usage, Burst attribute scan, asmdef reference review, and `git diff --check`. Rechecked dotnet/csc processes and CPU before any build decision.
Rejected Alternatives: Launching `dotnet build`, Unity batchmode, or csc while CPU reports 100 percent. That violates the batch protocol and risks collisions with other agents.
Scalability potential: Verification stays reproducible without stealing machine budget. Runtime quality remains unaffected because no runtime code path is introduced.
Hardware Impact: Build was skipped; no additional CPU saturation was caused. Compile/profiler microseconds remain pending.

## Decision 27 - Preview Mesh Instead Of Private Line Arrays
Problem: The initial SceneView preview route stored managed line arrays, which was editor-only but still weakened the zero-private-array claim.
Solution: Replaced preview line arrays with a hidden `Mesh` plus hidden internal-colored `Material`; snap results are uploaded through local Temp `NativeArray<Vector3>` and `NativeArray<int>` buffers, then disposed immediately.
Rejected Alternatives: Keeping static arrays, mutating source meshes for preview, or spawning scene preview actors. Static arrays create stale proof debt; source mutation and actors risk hidden runtime state.
Scalability potential: Low keeps the 4096-line cap; Middle/High/Ultra can raise the editor-only cap later without touching baked runtime truth or DTO layout.
Hardware Impact: Runtime remains 0 us. Editor preview memory is bounded and disposed after upload; no long-lived managed vertex/index array remains.

## Decision 28 - Fixed Profile Index UI
Problem: `DropdownField` and managed option lists add unnecessary managed UI state for a cold Forge control that only needs fixed profile slots.
Solution: Replaced the dropdown/list route with an `IntegerField` profile index and a compact hash/count label. The profile cache remains a fixed struct with 16 explicit slots.
Rejected Alternatives: `List<string>` options, reflection-driven profile UI, or ScriptableObject-only tuning. Those allocate extra editor objects and obscure the CSV profile hash route.
Scalability potential: Low through Ultra keep identical profile schema; designers select index and tune continuous quality/radius/blend sliders without C# recompile.
Hardware Impact: Runtime 0 us. Editor managed churn is reduced during Forge creation and profile reload.

## Decision 29 - Private Array And String Hygiene Scan
Problem: Source-only proof still contained private array declarations and minor editor string concatenation, making H-PHI reporting weaker than the actual runtime boundary.
Solution: Replaced scanner static arrays with switch-based catalogs, replaced `Directory.GetFiles` array allocation with `Directory.EnumerateFiles` enumerator use, inlined mesh layout params instead of a private layout array, and built temp paths/status strings with `StringBuilder`.
Rejected Alternatives: Relying on "editor-only" caveats for easy-to-remove private arrays or forcing a compile while CPU remained above the gate.
Scalability potential: The changes do not alter runtime quality; they keep the offline baker simpler to audit across Low/Middle/High/Ultra asset bake profiles.
Hardware Impact: Runtime 0 us. Editor cold allocation profile is reduced structurally; CPU gate still blocks compile/profiler proof.

## Decision 30 - AUP-Local Debounced Preview
Problem: SceneView preview previously reconstructed AUP then subtracted it back, and the Forge preview was button-biased rather than slider-reactive.
Solution: Preview now consumes `SeamSnapResult64` local positions directly and uploads terrain-local ribbons. Forge slider, AUP, and LOD0 mesh changes enqueue one `EditorApplication.delayCall` preview refresh, removed on window disable.
Rejected Alternatives: Casting absolute AUP to float3 for SceneView, running preview on every slider event synchronously, or mutating source meshes for feedback. Absolute casts violate precision discipline; every-event execution can stall the editor; source mutation creates hidden state.
Scalability potential: Low keeps a 4096-ribbon cap and one queued refresh; Middle/High/Ultra can raise preview density later without changing baked assets or runtime truth.
Hardware Impact: Runtime 0 us. Editor preview avoids large-coordinate float jitter and coalesces rapid UI changes into a single command.

## Decision 31 - Explicit Roslyn Reference Fence
Problem: The scanner uses Roslyn AST types, but the owned editor asmdef did not explicitly bind the Roslyn plugin DLLs.
Solution: Set the editor asmdef `overrideReferences` to true and listed `Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.CSharp.dll`, `System.Collections.Immutable.dll`, and `System.Reflection.Metadata.dll`.
Rejected Alternatives: Trusting implicit plugin auto-reference or dropping back to lexical-only scanning. Implicit reference is not proof; lexical-only scanning weakens Task 19.
Scalability potential: Runtime quality is unchanged. Editor scan dependency is now explicit and compile-wall scoped to the owned editor assembly.
Hardware Impact: Runtime 0 us. Compile/import risk is reduced structurally; Unity import still pending CPU gate.

## Decision 32 - CSV Native Scratch Instead Of Large Stackalloc
Problem: `stackalloc byte[byteCount]` allowed up to 32 KiB on the editor stack.
Solution: CSV loading now allocates a short-lived `NativeArray<byte>` with `Allocator.Temp` and `UninitializedMemory`, reads through an unsafe `Span<byte>` over the native buffer, parses via `ReadOnlySpan<byte>`, and disposes in `finally`.
Rejected Alternatives: Managed `byte[]`, `string.Split`, pooled managed buffers, or keeping large stackalloc. Managed arrays violate the zero-GC direction; string splitting allocates; large stack frames are fragile.
Scalability potential: Low/Middle/High/Ultra profile schemas stay fixed; larger profile files remain bounded by `MaxProfileCsvBytes` without stack pressure.
Hardware Impact: Runtime 0 us. Editor stack pressure removed; scratch is native, bounded, and cold.

## Decision 33 - Compile-Risk Audit Response
Problem: A delegated read-only audit reported a possible `hasColor` truncation at pipeline line 504, Roslyn asmdef risk, and CSV stack risk.
Solution: Verified current `hasColor` source is intact, fixed the two live risks, re-ran source scans, XML/asmdef parse, Burst attribute audit, untracked-file whitespace scan, process check, and CPU gate.
Rejected Alternatives: Ignoring subagent findings or forcing a build while CPU reported 100 percent.
Scalability potential: Verification quality improves without touching runtime presentation or authority.
Hardware Impact: No build CPU was consumed. Runtime 0 us; compile/profiler proof remains blocked by CPU gate.

## Decision 34 - Rollback Fence Self-Description
Problem: The rollback exclusion sidecar occupied 32 bytes but bytes 16-31 were inert padding, leaving no binary magic/version/endian proof for downstream diagnostic readers.
Solution: Kept `SeamMeshRollbackFenceDTO` at exactly 32 bytes and repurposed offsets 16, 20, 24, and 28 as `VTSF` magic, version, little-endian marker, and reserved word. The writer fills all eight uint lanes explicitly with manual little-endian byte order; the layout validator now asserts each field offset.
Rejected Alternatives: Growing the DTO, adding a managed JSON sidecar, or relying on file names as binary proof. Growth changes the payload contract; JSON adds a second truth; file names are not sufficient for binary hydration.
Scalability potential: Low/Middle/High/Ultra runtime remains unchanged. The sidecar improves asset-pipeline diagnostics without altering geometry, LOD policy, or StateRingBuffer authority.
Hardware Impact: Runtime 0 us. Editor writes four additional uints per generated LOD sidecar; the byte order is deterministic and avoids ambiguous downstream endian reads.
