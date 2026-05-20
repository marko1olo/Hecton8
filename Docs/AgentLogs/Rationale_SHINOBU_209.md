# Rationale_SHINOBU_209

Status: STATIC IMPLEMENTATION / PROJECT COMPILE BLOCKED OUTSIDE DOMAIN

## 2026-05-20 Initial Architecture Decisions

Problem: The prompt targets offline destructive mesh baking, while the existing `World/ProceduralWreckage` code is a runtime/procedural WFC assembler with DataVault buffers and GPU matrix extraction.
Solution: Add a separate `Hecton8.World.OfflineWreckageBaker` assembly and Editor-only Forge tools. This avoids changing the WFC runtime authority surface and keeps destructive deformation out of gameplay.
Rejected Alternatives: Reusing `ProceduralWreckageVault` would create false dependency on another live agent's untracked runtime WFC work and would expand GlobalDataVault for editor scratch. Standard Unity runtime damage components were rejected because they reintroduce runtime mesh mutation and Rigidbody debris.
Scalability potential: Low uses static baked states and an 8-point support hull; Middle/High/Ultra increase baked vertex deformation richness, scorch vertex data, and preview density without adding runtime simulation.
Hardware Impact: MX350/i3 avoids per-frame deformation, dynamic mesh collider rebuilds, and Rigidbody fragment broadphase churn. Estimated runtime saving target is 300-2500 us on breach events, depending on previous fragment count.

Problem: Generated mesh-state metadata must be ARM64-safe and rollback-neutral.
Solution: Define `MeshDamageStateMappingDTO` as `[StructLayout(LayoutKind.Explicit, Size = 32)]` with four 32-bit mesh hashes and explicit 64-bit padding; runtime synchronizes only damage state index, not mesh geometry.
Rejected Alternatives: Managed dictionaries, ScriptableObject-only state maps, or implicit sequential structs were rejected because they hide layout and can drift under IL2CPP/ARM64.
Scalability potential: Low/Middle/High/Ultra all read the same 32-byte aligned mapping; visual tier changes affect which baked mesh is selected, not the network truth payload.
Hardware Impact: 32-byte aligned reads avoid misaligned runtime metadata loads on ARM64-class devices and keep cache use deterministic.

## 2026-05-20 Loop 1 Decisions

Problem: The prompt named `Assets/_Project/Scripts/Combat`, but the actual combat code is under `Assets/_Project/Scripts/Gameplay/Combat`; the named root is absent.
Solution: Scanner covers both the requested root and the actual root, plus `Assets/_Project/Scripts/Environment`. Missing roots are recorded as `MISSING`; real roots are scanned outside `Editor/` only.
Rejected Alternatives: Treating the missing `Combat` path as success was rejected because it would skip the live combat surface. Widening the scan to the whole project was rejected because unrelated agents own VFX and gameplay systems outside this domain.
Scalability potential: Low/Middle/High/Ultra all benefit from the same runtime fence: static mesh state selection only, no vertex writes or Rigidbody debris spawning in the hot path.
Hardware Impact: Static scan found zero forbidden runtime mesh deformation/debris-spawn findings in the active roots. Expected avoided cost remains event-dependent: 300-2500 us for mesh mutation bursts and 1000-8000 us for broadphase-heavy debris incidents that this pipeline prevents from being introduced.

Problem: Combat strings named `MicroFracture` and `BioformFractured` are damage semantics, not geometry fracture systems.
Solution: The scanner pattern was narrowed from generic `Fracture` to concrete geometry-pipeline terms (`FractureMesh`, `FractureShard`, `ProceduralFracture`, `Shatter(`). This avoids false architecture failures while still catching runtime mesh fragmentation code.
Rejected Alternatives: Keeping broad substring matching was rejected because it would report non-geometric combat status strings as illegal mesh destruction. Ignoring fracture terms entirely was rejected because actual procedural fracture code must stay banned.
Scalability potential: Low-tier devices avoid false cleanup churn; higher tiers can still run rich VFX because GPU particle effects are not banned by this geometry scanner.
Hardware Impact: No direct frame saving from the pattern fix; it preserves accurate enforcement and prevents time wasted on non-bottleneck combat nomenclature.

## 2026-05-20 Loop 2 Decisions

Problem: Exact torn mesh collision is visually attractive but runtime-hostile when used as a `MeshCollider`.
Solution: Generate an 8-point support-map convex wrapper from the deformed vertex bounds as the "Dear Lie" collision asset. The visual mesh carries torn metal; collision remains one simple convex hull under the 256-point budget.
Rejected Alternatives: Full QuickHull was rejected for this pass because it spends editor time to produce collision detail the player should not physically notice. Runtime `MeshCollider` on torn topology was rejected because it reintroduces PhysX cost.
Scalability potential: Low uses the same 8-point hull. Middle/High/Ultra spend extra quality weight on deformation, scorch, and torn visual vertices while collision stays cheap and predictable.
Hardware Impact: Estimated runtime saving is 200-1200 us per collision-heavy wreck contact compared with complex non-convex mesh collision, plus lower broadphase memory pressure on MX350/i3-class hardware.

Problem: Unity `Mesh.RecalculateNormals()` and managed vertex arrays would allocate and execute on the main thread during asset generation.
Solution: Burst jobs recalculate angle-weighted normals/tangents from raw unmanaged fields, then serialize interleaved vertices with `SetVertexBufferData` and `SetIndexBufferData` directly from `NativeArray` buffers.
Rejected Alternatives: Built-in normal recalculation and `mesh.vertices` assignment were rejected because they hide managed copies and violate the mandate to keep dense vertex math in Burst-owned memory.
Scalability potential: Low keeps low deformation density; Middle/High/Ultra can increase profile quality without changing runtime behavior.
Hardware Impact: Runtime cost is 0 us because all math is Editor-only. Editor bake cost remains bounded by Burst parallel jobs; expected saved developer iteration time versus managed mesh loops is 5000-30000 us per 50k-vertex state.

Problem: Cold source extraction originally used Unity `List<Vector3>` and hull fallback used `mesh.vertices`, which is legal Editor code but weak against this batch's zero-tolerance text scanner.
Solution: Replace extraction with `Mesh.AcquireReadOnlyMeshData`, byte-stream vertex reads in `ExtractBaseVerticesJob`, index-copy jobs, and buffer-based hull mesh construction.
Rejected Alternatives: Keeping cold managed extraction was rejected because the prompt explicitly bans `List<Vector3>` and managed vertex arrays in this baker. Using `Mesh.GetIndices` was rejected because it allocates managed arrays.
Scalability potential: Low/Middle/High/Ultra all now enter the same NativeArray pipeline from source import to final serialized mesh.
Hardware Impact: Editor extraction avoids managed array/list churn; estimated saved GC and copy overhead is 300-2500 us per source mesh, depending on vertex count.

## 2026-05-20 Loop 3 Decisions

Problem: Massive wreck set-pieces need blast origins in AUP space without float drift.
Solution: `LocalizeBlastEpicenter(double3 blastAup, double3 moduleAup)` subtracts in double precision and clamps before casting to local `float3` for Burst deformation.
Rejected Alternatives: Passing absolute world floats into radial blast math was rejected because large coordinates corrupt direction vectors and tear thresholds.
Scalability potential: Low/Middle/High/Ultra share the same localized math; higher tiers only change deformation amplitude and visual detail.
Hardware Impact: Avoids NaN/far-origin corrective work and prevents failed bakes on large coordinates; runtime impact is 0 us because localization is Editor-side.

Problem: Immutable wreck mesh geometry must not become rollback/Merkle state.
Solution: Architecture documentation states that runtime synchronizes only the integer damage state index and reads immutable baked mesh/collider references. `MeshDamageStateMappingDTO` stores hashes only.
Rejected Alternatives: Adding vertex payloads, collider points, or mutable geometry to rollback state was rejected as network bandwidth waste and desync risk.
Scalability potential: Low-tier and ultra-tier clients can choose visual mesh quality locally while authoritative state remains the same index.
Hardware Impact: Avoids network hash churn and mesh-state serialization; estimated bandwidth/CPU saving is 50-400 us per damage-state replication tick depending on old payload size.

Problem: Zero-fill of large temporary geometry buffers burns editor iteration time.
Solution: TempJob buffers for vertices, indices, tear weights, and hull points use `NativeArrayOptions.UninitializedMemory`, then all consumed slots are deterministically overwritten by extraction and bake jobs.
Rejected Alternatives: `ClearMemory` and `UnsafeUtility.MemClear` were rejected for high-volume bake buffers because the next job writes the required ranges anyway.
Scalability potential: Low bakes smaller meshes; Middle/High/Ultra can increase mesh density without paying unnecessary zero-fill for full scratch buffers.
Hardware Impact: Estimated editor saving is 200-1800 us per large buffer group on weak CPUs, with no runtime cost.

## 2026-05-20 Loop 4 Decisions

Problem: Technical artists need control without forcing final asset writes on every slider change.
Solution: `Wreckage Forge` exposes UI Toolkit controls, CSV profile loading, folder batch bake, runtime scan, and a preview path that runs the same Burst deformation into temporary buffers shown by an editor-only gizmo.
Rejected Alternatives: Single menu commands and runtime preview components were rejected because they either hide critical parameters or risk introducing gameplay-time deformation.
Scalability potential: Low/Middle/High/Ultra are represented by continuous `GlobalQualityWeight`; there is no binary intact/broken switch in the generator.
Hardware Impact: Runtime remains 0 us. Preview cost is Editor-only and bounded by temporary NativeArrays disposed in `finally`.

Problem: Critical geometry bake systems need a crash/NaN trail, not post-hoc guessing.
Solution: Added `OfflineWreckageBlackBox`, a 300-entry `NativeArray<OfflineWreckageTelemetryEntry>` circular buffer. On non-finite vertex detection, the baker writes `Docs/AgentLogs/Dump_SHINOBU_209.bin`.
Rejected Alternatives: Text-only reports were rejected because they do not preserve ordered binary state. Relying on Unity console logs was rejected because console history is not durable under crashes.
Scalability potential: Low/Middle/High/Ultra all emit identical 64-byte telemetry entries; higher visual settings only alter counts and timing fields.
Hardware Impact: Runtime impact is 0 us. Editor cost is one 64-byte telemetry write per baked state; negligible compared with deformation jobs.

## 2026-05-20 Ultra-Think Polish Decisions

Problem: The preview path scheduled Burst work and then read `counts[0]` without completing the returned `JobHandle`.
Solution: Complete the preview fence in the Editor-only button path before constructing the temporary preview mesh. Replace the preview persistent `NativeArray` store with a transient `Mesh` drawn by `Gizmos.DrawWireMesh`.
Rejected Alternatives: Keeping persistent preview NativeArrays was rejected because it created editor-owned retained buffers for a non-authoritative visualization. Reading counts before completion was rejected as a race even though it only affected editor preview.
Scalability potential: Low/Middle/High/Ultra preview now follows the exact same completed deformation chain before visualization; quality changes affect only the math payload, not unsafe read timing.
Hardware Impact: Runtime impact remains 0 us. Editor preview avoids retained NativeArray ownership and prevents undefined counter reads; expected saved debugging time is unbounded relative to chasing nondeterministic preview glitches.

Problem: Forge AUP entry used `Vector3Field`, downcasting absolute coordinates to float before localization.
Solution: Replace module/blast AUP UI with six `DoubleField` controls and perform `double3 blastAup - double3 moduleAup` before local `float3` cast.
Rejected Alternatives: Keeping `Vector3Field` was rejected because it violates the 100 km jitter rule and can corrupt blast vectors in set-piece bakes.
Scalability potential: Low/Middle/High/Ultra all share precise AUP localization; visual quality weight does not alter coordinate authority.
Hardware Impact: Runtime impact is 0 us. Editor bake avoids far-origin misbakes and repeat bake cost caused by precision failures.

Problem: Burst jobs were marked `FloatMode.Deterministic`, but this baker generates immutable editor assets and is not an authoritative rollback simulation.
Solution: Convert all owned mathematical Burst jobs to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]` per polish mandate.
Rejected Alternatives: Deterministic mode was rejected for this non-authoritative offline tool because it spends compiler latitude without protecting runtime rollback state. Unsafe/non-finite math remains explicitly guarded.
Scalability potential: Fast mode gives the compiler more room to vectorize deformation, extraction, tear, normal, color, and hull jobs across mobile NEON and desktop SIMD.
Hardware Impact: Expected editor bake gain is 5-40 percent for math-heavy states depending on Burst backend and mesh density. Runtime remains 0 us.

Problem: CSV and mapping writers still allocated managed `byte[]` buffers in cold editor paths.
Solution: Convert profile ingestion to bounded `stackalloc Span<byte>` plus `FileStream.Read(Span<byte>)`; convert mapping serialization to `stackalloc Span<byte>` plus `FileStream.Write(ReadOnlySpan<byte>)`.
Rejected Alternatives: `File.ReadAllBytes` and `File.WriteAllBytes` were rejected because the task explicitly requested allocation-conscious profile/mapping tooling.
Scalability potential: CSV size is capped at 32768 bytes; low/high devices follow the same parser and profile rows hydrate into unmanaged DTOs.
Hardware Impact: Editor GC pressure reduced to 0 bytes for profile file bytes and mapping bytes; expected savings are small per file (10-200 us) but remove cumulative garbage during repeated bakes.

Problem: Canonical `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` is shared by multiple agents and was overwritten by SHINOBU_210.
Solution: Patch `Runtime_Destruction_Scanner` to preserve any existing report when writing, regenerate the canonical SHINOBU_209 report, and add `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_209.json` sidecar for stable ownership.
Rejected Alternatives: Blind overwrite was rejected because concurrent agents are active. Leaving SHINOBU_210 as top-level was rejected because Task 19 requires SHINOBU_209 proof at the canonical path.
Scalability potential: Report preservation is documentation/runtime-proof hygiene only.
Hardware Impact: Runtime impact is 0 us; avoids coordination loss and report churn during parallel agent work.

Problem: The Ultra-Think mandate demanded a hard H-Phi/Vault statement, but this domain is an Editor-only offline baker with no runtime execution surface.
Solution: Record the split explicitly in `LOG_SHINOBU_209.md`: runtime has zero persistent NativeArray ownership and requests no VaultBufferHandle because it performs no deformation work. The Forge profile cache now uses a fixed 16-slot value cache instead of a persistent native table; only the 300-entry black-box ring remains as retained native editor telemetry. Transient TempJob buffers are disposed in `finally`.
Rejected Alternatives: Falsely claiming zero editor-private native allocations was rejected because the black-box ring is a mandated real editor buffer. Moving editor scratch into GlobalDataVault was rejected because it would couple offline authoring tools to runtime rollback memory ownership.
Scalability potential: Runtime remains constant-cost across Low/Middle/High/Ultra; editor quality weight scales deformation detail without changing runtime memory authority.
Hardware Impact: Runtime 0 us. Editor retained native memory is bounded and explicit: 300 * 64 bytes telemetry.

Problem: Small `NativeArray<int>` count buffers (`Counts` and `HullCounts`) gave correct results but did not prove cache-line ownership or false-sharing discipline.
Solution: Replace them with one `OfflineWreckageBakeCounters64` row: explicit 64 bytes, active vertices/torn vertices/degenerate triangles/hull vertices/warnings in the first 20 bytes, explicit padding through byte 63. Build, normal, color, and hull jobs now pass the same cache-line counter row through the dependency chain.
Rejected Alternatives: Keeping three-int and one-int arrays was rejected because they were cheaper to type but weaker as a hardware proof. Separate counter arrays were rejected because they expanded TempJob ownership and added no scheduling value.
Scalability potential: Low/Middle/High/Ultra share the same counter contract; higher visual quality increases counts, not counter memory topology.
Hardware Impact: Runtime 0 us. Editor jobs avoid adjacent tiny counter rows and the layout validator now checks the 64-byte counter DTO.

Problem: Damage-state `.bytes` maps and JSON reports were written directly to their final paths, so an Editor interruption or filesystem fault could leave half-written proof artifacts.
Solution: Route mapping bytes, Forge bake report, and scanner reports through same-volume `.tmp` files with `FileMode.CreateNew`, exclusive write sharing, and final `File.Move`. Scanner now writes both canonical `PHYSICS_OPTIMIZATION_REPORT.json` and SHINOBU-owned sidecar from the same generated payload.
Rejected Alternatives: Direct `File.WriteAllText`/final-path `FileStream.Create` were rejected because they are simpler but not failure-atomic. A larger manifest schema change was rejected because Task 04 requires the map payload itself to remain exactly 32 bytes.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; this protects authoring artifacts that feed all tiers.
Hardware Impact: Runtime 0 us. Editor IO adds one rename per artifact and removes torn-file failure mode.

Problem: The black-box dump used `BinaryWriter`, a managed object facade that writes each field one by one and weakens the byte-layout proof for a 64-byte telemetry DTO.
Solution: Replace it with a fixed 32-byte little-endian header plus raw 64-byte row writes copied through `UnsafeUtility.CopyStructureToPtr` into a stack span. The dump itself writes through `.tmp` and publishes with `File.Replace` when the final dump already exists.
Rejected Alternatives: Keeping `BinaryWriter` was rejected because field-wise managed serialization hides the actual row layout. Writing JSON was rejected because the crash trail must remain compact binary.
Scalability potential: Low/Middle/High/Ultra all share identical 64-byte forensic rows; higher visual quality only changes row counters/timing.
Hardware Impact: Runtime 0 us. Editor crash dump path removes per-field writer overhead and makes dump size fixed: 32 + retainedRows * 64 bytes.

Problem: The dense mock deformation kernel existed, but automated CI/editor users had no single entrypoint that exercised the full deformation pipeline without source art assets.
Solution: Add `OfflineWreckageMockBenchmark`, an Editor-only static menu/entrypoint that generates a 48x48x6 vertex grid, surface triangle indices, and runs mock deformation, structural shear, radial blast, tear duplication, normal recalculation, damage color baking, and convex hull generation through the same Burst chain. It writes `Docs/Reports/WRECKAGE_MOCK_BENCHMARK_SHINOBU_209.json` atomically.
Rejected Alternatives: Keeping only `GenerateMockStructuralDeformationJob` was rejected because it proves one kernel but not the chained bake contract. Creating GameObjects or temporary assets for CI was rejected because the benchmark should not touch scene/runtime authority.
Scalability potential: Low/Middle/High/Ultra can change grid resolution and `GlobalQualityWeight` while preserving the same no-runtime-deformation contract. CI can run a weak-device profile with lower resolution and an ultra profile with denser grids without changing runtime code.
Hardware Impact: Runtime 0 us. Editor/CI now has deterministic coverage for the expensive math path without waiting on art assets.

Problem: Several Burst job fields were output-only but only annotated with `[NoAlias]`, leaving the compiler less proof than available.
Solution: Add `[WriteOnly]` to output-only vertex/index/counter/hull/tear fields where the job never reads element data. Read/write fields remain unmarked where mutation uses existing values.
Rejected Alternatives: Blanket `[WriteOnly]` on all mutable fields was rejected because shear/blast/color/normal jobs legitimately read and write vertex rows. Leaving output lanes unqualified was rejected because it undersells memory isolation to Burst.
Scalability potential: Continuous quality behavior is unchanged; the compiler receives stronger alias/write intent for all tiers.
Hardware Impact: Runtime 0 us. Editor Burst kernels gain stricter memory-access proof for NEON/AVX vectorization opportunities.

Problem: Newly added SHINOBU_209 C# and asmdef files had no committed `.meta` files, so Unity would mint GUIDs on first import and create non-deterministic local identity across parallel agents.
Solution: Add explicit `.meta` files for every owned `.cs` and `.asmdef` under `Assets/_Project/Scripts/World/OfflineWreckageBaker`, using standard script and AssemblyDefinitionImporter meta shapes. A duplicate GUID scan over the domain returned no duplicates.
Rejected Alternatives: Waiting for Unity to generate metas was rejected because it makes first import machine-local and can create merge churn. Editing unrelated global asset metadata was rejected as outside domain.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; import determinism protects all tiers from GUID drift.
Hardware Impact: Runtime 0 us. Editor/import stability improved; no frame-time claim.

Problem: Forge and mock benchmark reports wrote floating microsecond values with current-culture `ToString("0.000")`, which can emit comma decimals on non-US editor machines and produce invalid JSON numbers.
Solution: Add `System.Globalization.CultureInfo.InvariantCulture` to the two owned editor JSON writers and format `burstMicroseconds`/`microseconds` with invariant decimal separators.
Rejected Alternatives: Leaving locale-sensitive proof artifacts was rejected because CI/report consumers need deterministic machine-readable JSON across developer regions. Replacing the whole report writer with a serializer was rejected because this editor lane intentionally keeps reports simple, atomic, and scoped.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; report determinism improves evidence quality for every quality profile and benchmark resolution.
Hardware Impact: Runtime 0 us. Editor cost is unchanged in meaningful terms; this is correctness hardening, not a measured speed claim.

Problem: The previous `.tmp` publication helper deleted the final artifact before moving the temp file into place, leaving a short interval where other tooling could observe the target as missing.
Solution: Use `File.Replace(tempPath, finalPath, null)` whenever the final artifact already exists, and reserve `File.Move` for first creation only. Applied to damage-state map bytes, Forge report JSON, runtime scanner reports, mock benchmark report, and black-box binary dump.
Rejected Alternatives: Keeping delete-then-move was rejected because it is not a true atomic replacement contract. Introducing a shared helper class was deferred because the current duplication is small and a new abstraction would touch more lines without changing the artifact contract.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; editor proof artifacts are safer under concurrent readers and interrupted authoring tools.
Hardware Impact: Runtime 0 us. Editor IO cost is effectively unchanged; correctness improves by removing the missing-target window.

Problem: The CI mock benchmark generated a dense 3D vertex lattice but only emitted one XY surface worth of indices, so normals, tear duplication, and convex hull coverage were weaker than the Task 05 cube stress requirement.
Solution: Expand `GenerateMockGridSurfaceIndicesJob` to emit all six boundary surfaces: XY min/max, XZ min/max, and YZ min/max. The 48x48x6 benchmark now allocates 5358 surface quads and 32148 indices.
Rejected Alternatives: Keeping the single-surface benchmark was rejected because it can pass while missing face-orientation and boundary hull failures. Generating interior cube cell triangles was rejected because it wastes editor stress time on invisible internal faces that the real visual mesh would not render.
Scalability potential: Low CI can lower resolution while preserving six-face topology; High/Ultra CI can increase resolution and quality weight without changing runtime behavior.
Hardware Impact: Runtime 0 us. Editor benchmark work increases intentionally to cover the full math path; no measured speed claim.

Problem: The editor preview store owned a temporary Mesh that was disposed on replacement, but not explicitly on assembly reload or editor quit. The black-box telemetry ring likewise had a manual dispose method but no editor lifecycle hook.
Solution: Add an editor-only `InitializeOnLoad` lifecycle hook in the owned preview file. It unsubscribes/resubscribes idempotently to `AssemblyReloadEvents.beforeAssemblyReload` and `EditorApplication.quitting`, then disposes the preview mesh and black-box ring. Preview meshes are marked `HideFlags.HideAndDontSave` when stored, keeping them out of scene/asset persistence.
Rejected Alternatives: Relying on Unity domain teardown was rejected because retained UnityEngine.Objects and persistent NativeArrays need explicit owner shutdown. Moving preview ownership into a ScriptableObject asset was rejected because the preview is transient authoring state, not project data.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; editor preview can be used repeatedly across quality weights without accumulating temp meshes.
Hardware Impact: Runtime 0 us. Editor memory leak surface reduced by one retained Mesh and one 300-entry native telemetry ring per domain lifetime.

Problem: Owned Burst jobs used `NativeDisableParallelForRestriction` correctly for per-index writes, but the source did not state the exact invariant beside the suppression.
Solution: Add concise safety comments before every owned `NativeDisableParallelForRestriction` field, naming the exclusive index write or disjoint-buffer relationship. This is a proof/readability change only.
Rejected Alternatives: Removing the suppression was rejected because these jobs intentionally write NativeArray rows through unsafe pointers or output-only lanes for Burst-friendly direct mutation. Adding a broad file-level comment was rejected because suppressions should carry local invariants.
Scalability potential: Low/Middle/High/Ultra math behavior unchanged; code review and Burst-safety audit risk is lower for every profile.
Hardware Impact: Runtime 0 us. Editor code generation unchanged in meaningful terms; this is safety proof hardening.

Problem: Atomic writers still reused fixed `path + ".tmp"` names and deleted that temp before writing. That is not a final-target delete, but it is weak under concurrent Editor tools using the same output path.
Solution: Add `OfflineWreckageAtomicFile`, an Editor-only helper that creates unique same-directory temp paths with process id plus monotonic ordinal, writes with `FileMode.CreateNew`, publishes existing files through `File.Replace`, and only cleans up the owned unique temp on failure.
Rejected Alternatives: Keeping duplicated fixed-temp writers was rejected because a second tool could collide with stale/shared temp state. Using `Guid.NewGuid()` was rejected because a process-local ordinal is enough for this single Unity Editor process and is easier to audit in filesystem traces.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; every tier consumes safer immutable baked assets and reports.
Hardware Impact: Runtime 0 us. Editor IO cost is unchanged in meaningful terms; corruption risk during interrupted/concurrent authoring is reduced.

Problem: `AssetDatabase.GenerateUniqueAssetPath` minted new numbered mesh/map assets on every rebake, preserving data but destroying output identity stability and leaving stale orphaned assets.
Solution: Build deterministic output names from sanitized source name plus source-path hash. First bake creates the asset; later bakes copy the newly generated Mesh data into the existing asset with `EditorUtility.CopySerialized`, preserving the `.meta` GUID. `.bytes` maps use the same deterministic name and already publish atomically.
Rejected Alternatives: Deleting old assets before `CreateAsset` was rejected because it changes GUIDs and breaks references. Keeping unique numbered assets was rejected because designers repeatedly tuning a profile would pollute the output folder and make runtime references unstable.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; all tiers keep stable immutable asset references while visual richness changes by profile and source density.
Hardware Impact: Runtime 0 us. Editor/import churn drops because repeat bakes update existing assets instead of creating extra GUIDs and import records.

Problem: Stack-allocated binary payload buffers wrote only active fields and left padding/reserved bytes dependent on prior stack contents. That violates the explicit-padding proof even if field offsets are correct.
Solution: Clear the 32-byte mapping payload span before writing the four little-endian hashes, and clear the 32-byte black-box header before writing fixed header fields. DTO rows copied at 64 bytes remain fully overwritten by the DTO copy.
Rejected Alternatives: Trusting stackalloc bytes was rejected because uninitialized padding would make byte-for-byte artifacts non-deterministic and could pollute binary diff evidence. Switching to managed zeroed arrays was rejected because a span clear on 32 bytes is cheaper and keeps the zero-GC editor bridge.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; all tiers receive deterministic metadata and forensic headers.
Hardware Impact: Runtime 0 us. Editor cost is two 32-byte clears in cold/fault writer paths, below measurement relevance and necessary for binary correctness.

## 2026-05-20 Ultra-Think Polish Pass 11 Decisions

Problem: Source extraction only copied submesh 0 and ignored `baseVertex`, then the first corrected path used per-index range scans.
Solution: Build explicit 16-byte index-copy tiles from every triangle submesh; each tile preserves source index start, destination start, count, and `baseVertex`. Burst copy jobs now schedule per tile and loop contiguous indices into disjoint output windows.
Rejected Alternatives: `Mesh.GetIndices` managed arrays, submesh0-only extraction, per-index `ResolveRange` scan, and retaining Unity submeshes in the output were rejected because runtime consumes one immutable triangle stream.
Scalability potential: Low/Middle/High/Ultra all get complete baked visual sections. Higher tiers can feed denser multi-material meshes without submesh omissions; runtime remains one state-index swap.
Hardware Impact: Runtime 0 us. Editor index extraction changes from O(indexCount * submeshCount) lookup to O(indexCount) copy and avoids wrong hull/normal bounds from dropped submeshes.

Problem: Compile validation was needed after job changes, but the project build is blocked by unrelated Core missing-type errors.
Solution: Ran one single-core `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` only after CPU measured below 50 percent and no dotnet/csc process was active; stopped after the first compile wall and recorded blockers.
Rejected Alternatives: Repeated build attempts or touching Power/Core/Save/Fauna/Construction dependencies outside SHINOBU_209 ownership were rejected.
Scalability potential: None; verification hygiene only.
Hardware Impact: Runtime 0 us. Avoids wasting IO/CPU on repeated compile-wall attempts.

## 2026-05-20 Ultra-Think Polish Pass 12 Decisions

Problem: The 32-bit index copy path guarded `baseVertex` addition through a 64-bit temporary and int clamp, but the 16-bit path still used direct `ushort + baseVertex` arithmetic.
Solution: Apply the same long-add and clamp discipline to `CopyIndex16RangesJob` so malformed or extreme submesh `baseVertex` data cannot wrap before `BuildTornTrianglesJob` validates indices.
Rejected Alternatives: Assuming Unity import metadata is always sane was rejected because this baker is an offline content compiler and must fail soft on corrupt art input. Throwing inside the Burst copy job was rejected because invalid indices can be converted into degenerate triangles downstream without aborting the whole batch.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. Higher-tier dense meshes gain safer importer tolerance without changing the immutable mesh-swap contract.
Hardware Impact: Runtime 0 us. Editor cost is one 64-bit add and two comparisons per copied index; acceptable cold-path correctness cost versus a wrapped index poisoning normals or hull bounds.

Problem: `BuildTriangleSubMeshRanges` trusted `SubMeshDescriptor.indexStart/indexCount` to fit the underlying source index buffer.
Solution: Resolve source index capacity once from the typed MeshData index view, clamp `indexStart` to that capacity, cap available count, and truncate to whole triangles before tile emission.
Rejected Alternatives: Blind descriptor trust was rejected because a single corrupt submesh range can schedule an out-of-range read inside Burst. Managed `Mesh.GetIndices` validation was rejected because it allocates arrays and violates the extraction design.
Scalability potential: Low/Middle/High/Ultra all preserve the same single output stream; corrupt tail triangles are dropped deterministically instead of risking undefined reads.
Hardware Impact: Runtime 0 us. Editor adds O(submeshCount) scalar validation and prevents catastrophic batch aborts on bad imported meshes.

## 2026-05-20 Ultra-Think Polish Pass 13 Decisions

Problem: The black-box telemetry ring is an Editor-owned `Allocator.Persistent` `NativeArray`, but it was only disposed by lifecycle hooks and not visible to the native allocation tracking plane.
Solution: Reference `Hecton8.Core.Contracts` from the Editor-only asmdef and register/unregister the ring through `NativeMemoryTrackingBridge`. This bridge records bytes by owner/label when installed and no-ops safely when the Core sentinel is not installed.
Rejected Alternatives: Directly referencing `Hecton8.Core` was rejected because the contracts bridge exists specifically to avoid compile-wall coupling. Leaving the persistent ring untracked was rejected because the mandate requires native allocation visibility even for bounded editor diagnostics.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. Editor leak evidence improves while the runtime mesh-swap contract remains independent of the baker.
Hardware Impact: Runtime 0 us. Editor cost is a cold allocation registration/unregistration around a 300 * 64 byte ring.

## 2026-05-20 Ultra-Think Polish Pass 14 Decisions

Problem: The support-hull Dear Lie used a generic `[-0.5, 0.5]` cube whenever any measured deformed bounds axis was flat. Flat bulkheads and hull plates are legitimate wreckage inputs, so that fallback discarded real extents and produced an arbitrary collision proxy.
Solution: Preserve measured min/max on valid axes and expand only degenerate axes to a 0.01 m half-extent. Invalid/non-finite bounds still fall back to the unit cube and mark the existing non-finite warning.
Rejected Alternatives: Keeping the unit-cube fallback was rejected because it is fast but physically misleading for thin structural assets. Generating detailed runtime MeshCollider topology was rejected because the entire domain exists to avoid runtime collision truth.
Scalability potential: Low/Middle/High/Ultra all retain the same O(n) offline support hull scan and O(1) runtime mesh/collider swap. Higher-detail source meshes keep their authored extents without increasing runtime cost.
Hardware Impact: Runtime 0 us. Editor cost is three max comparisons and one optional warning bit in the hull job after the existing bounds scan.

Problem: Hull proxy expansion was not surfaced through the report or black-box warning path.
Solution: Add `WarningHullBoundsExpanded` and OR `OfflineWreckageBakeCounters64.WarningFlags` into the state warning flags after the hull job completes.
Rejected Alternatives: Silent collision proxy thickening was rejected because QA needs to distinguish a clean 3D support hull from a thin-axis-expanded proxy.
Scalability potential: Warning propagation is tier-independent and does not alter visual quality curves.
Hardware Impact: Runtime 0 us. Editor cost is one scalar OR per baked state.

## 2026-05-20 Ultra-Think Polish Pass 15 Decisions

Problem: `Runtime_Destruction_Scanner` preserved the previous shared canonical report by embedding its full JSON as an escaped string inside the new canonical report. Re-running the scanner can recursively grow `PHYSICS_OPTIMIZATION_REPORT.json`.
Solution: Replace the recursive blob with bounded provenance fields: previous report byte count, hash, and agent. The exact previous JSON is still written once to `PHYSICS_OPTIMIZATION_REPORT_PREVIOUS_SHINOBU_209.json` before the canonical report is replaced.
Rejected Alternatives: Keeping the embedded report was rejected because the report size grows with every run and pollutes machine-readable evidence. Dropping previous-report preservation entirely was rejected because multiple agents share the canonical report.
Scalability potential: Runtime behavior unchanged across all tiers. Editor scanner output remains bounded even under repeated CI/menu runs.
Hardware Impact: Runtime 0 us. Editor avoids recursive JSON growth; canonical report stays O(current findings) instead of O(previous report chain).

## 2026-05-20 Ultra-Think Polish Pass 16 Decisions

Problem: Pass 15 labeled `previousReport.Length` as `previousReportBytes`, but `string.Length` is UTF-16 code units, not the UTF-8 byte count actually written by `OfflineWreckageAtomicFile.WriteTextUtf8`.
Solution: Add a no-allocation UTF-8 measurement/hash walk in `Runtime_Destruction_Scanner`. The scanner now counts ASCII, two-byte, three-byte, and surrogate-pair four-byte UTF-8 emissions and hashes the same byte stream used for the bounded previous-report provenance.
Rejected Alternatives: Renaming the field to `previousReportChars` was rejected because the report contract says bytes. Calling `Encoding.UTF8.GetBytes` was rejected because it allocates a managed byte array for a cold artifact that can be measured by a scalar walk.
Scalability potential: Runtime behavior unchanged across Low/Middle/High/Ultra; static scanner evidence remains bounded and byte-accurate under repeated validation runs.
Hardware Impact: Runtime 0 us. Editor adds one scalar pass over the previous report string and removes misleading provenance metadata.

## 2026-05-20 Ultra-Think Polish Pass 17 Decisions

Problem: Pass 16 counted UTF-8 bytes correctly, but the hash path still called `OfflineWreckageBakeMath.HashBytes`, which lowercases ASCII and skips selected whitespace for stable asset/profile-name hashes. That is not a raw report byte-stream hash.
Solution: Add `HashRawByte` inside `Runtime_Destruction_Scanner` and use it for every emitted UTF-8 byte in `HashUtf8Scalar`. The final avalanche remains `OfflineWreckageBakeMath.Hash(uint)` because it does not normalize individual bytes.
Rejected Alternatives: Reusing the name-hash helper was rejected because two distinct prior reports can collide more easily if case and whitespace are intentionally ignored. Allocating an encoded byte array for standard hashing was rejected because the scanner already has a no-allocation scalar encoder walk.
Scalability potential: Runtime behavior unchanged across Low/Middle/High/Ultra; scanner provenance now reflects byte-for-byte report differences without growing the canonical report.
Hardware Impact: Runtime 0 us. Editor cost is unchanged in meaningful terms: one XOR and multiply per UTF-8 byte already counted in the cold scanner path.

## 2026-05-20 Ultra-Think Polish Pass 18 Decisions

Problem: Scanner JSON string escaping only handled quote and backslash, and previous-agent extraction treated any quote preceded by a backslash as escaped without checking whether the backslash itself was escaped.
Solution: Extend `AppendEscaped` to emit valid JSON escapes for backspace, form feed, newline, carriage return, tab, and generic control bytes as `\u00XX`. Add `IsEscaped` with backslash-run parity so extracted string termination respects even/odd slash counts.
Rejected Alternatives: Leaving report fields dependent on path/pattern cleanliness was rejected because the scanner also preserves other-agent canonical report metadata. Pulling in a JSON serializer was rejected because this editor scanner intentionally keeps a tiny bounded writer and avoids broad dependency churn.
Scalability potential: Runtime behavior unchanged across Low/Middle/High/Ultra; static scanner artifacts remain machine-readable under repeated validation and cross-agent report preservation.
Hardware Impact: Runtime 0 us. Editor cost is a few branches per emitted report character in a cold menu/CI scanner path.

## 2026-05-20 Ultra-Think Polish Pass 19 Decisions

Problem: `ExtractJsonStringValue` located the first quote after the colon, so a prior canonical report containing a non-string `agent` value could cause the scanner to capture the next quoted property name/value as the previous report agent.
Solution: After the colon, skip only JSON whitespace and require the next non-whitespace character to be a string quote. Non-string, missing, or malformed values now fail closed to `UNKNOWN`.
Rejected Alternatives: Continuing to tolerate loose extraction was rejected because previous-report provenance is cross-agent evidence. Using a full JSON parser was rejected because this bounded editor scanner only needs one string field and avoids adding broad dependencies.
Scalability potential: Runtime behavior unchanged across Low/Middle/High/Ultra; scanner provenance remains bounded and fails closed under malformed shared canonical reports.
Hardware Impact: Runtime 0 us. Editor adds a short whitespace loop in a cold scanner path.

## 2026-05-20 Ultra-Think Polish Pass 20 Decisions

Problem: `OfflineWreckageAtomicFile.Publish` used one `File.Exists(finalPath)` snapshot before choosing `File.Replace` or `File.Move`. In a parallel Editor environment, another tool can create or remove the final file between that check and publication.
Solution: Wrap the first observed-state publish in a narrow retry path. If `FileNotFoundException` or `IOException` occurs and the owned temp file still exists, re-observe final-path existence and retry the appropriate `File.Replace`/`File.Move` once.
Rejected Alternatives: A global named mutex was rejected because it would serialize unrelated artifact writers and create a new lock-order surface. Blind repeated retries were rejected because locked files should fail visibly instead of spinning.
Scalability potential: Runtime behavior unchanged across Low/Middle/High/Ultra; editor output artifacts remain stable under concurrent scan/bake/report tools.
Hardware Impact: Runtime 0 us. Editor normal path unchanged; race path adds one filesystem recheck and one retry.

## 2026-05-20 Ultra-Think Polish Pass 21 Decisions

Problem: `RecalculateDeformedNormalsJob.Angle` used `math.rsqrt(la * lb)` after clamping only with `math.max`. If a corrupt imported/deformed edge produced non-finite dot products, the angle weight could become non-finite and poison normal accumulation.
Solution: Check `la` and `lb` for finiteness and minimum length before reciprocal square root. The helper now returns zero angle weight for non-finite, zero-length, or non-finite dot results.
Rejected Alternatives: Relying exclusively on upstream vertex sanitization was rejected because normal recomputation is a last defensive wall before baked mesh serialization. Throwing from the job was rejected because bad triangles should be skipped/zero-weighted, not abort an entire batch.
Scalability potential: Runtime behavior unchanged across Low/Middle/High/Ultra; higher-tier dense source meshes gain stronger corrupt-import tolerance without changing the O(1) runtime mesh swap.
Hardware Impact: Runtime 0 us. Editor adds finite checks inside the normal bake job; this is cold-path correctness cost.
