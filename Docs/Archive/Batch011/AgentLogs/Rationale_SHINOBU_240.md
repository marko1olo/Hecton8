# SHINOBU_240 Rationale

Agent: SHINOBU_240
Domain: ECHELON 2 - WORLD GENERATION & TERRAIN
Status: STATIC_SOURCE POLISH APPLIED, COMPILE BLOCKED BY CPU GUARD

## Decision 001 - Offline Heightmap Authority

Problem: Old terrain generation may depend on MapMagic graph evaluation or runtime terrain mutation, which conflicts with the 16.67 ms frame budget and world-streaming doctrine.

Solution: Keep macroscopic terrain generation as an editor/offline Burst pipeline that writes flat sector `.h8bin` arrays and reports. Runtime receives immutable binary height data only.

Rejected Alternatives: Runtime MapMagic re-evaluation and Unity Terrain height mutation are rejected because they allocate or stall, hide third-party graph cost, and create nondeterministic streaming load.

Scalability potential: Low uses the same baked data with shorter residency and cheaper mesh LOD. Middle keeps more sector tiles resident. High keeps richer proxy/topology previews. Ultra spends saved runtime CPU on visual overkill in renderer/streaming, not on live terrain generation.

Hardware Impact: Estimated low-end gain is removal of multi-millisecond terrain/noise spikes on i3/MX350; exact microseconds are PENDING VERIFICATION until compile and profiler artifacts exist.

## Decision 002 - AUP-Seeded Double Precision Sampling

Problem: Local-sector coordinates can create seams and precision drift across a 100km terrain grid.

Solution: Evaluate generation coordinates from absolute `double3` AUP sector origin plus local pixel spacing. Cast only final height to `float`.

Rejected Alternatives: Local-only sector noise and float-only world coordinates are rejected because border samples can diverge and distant sectors lose phase stability.

Scalability potential: All tiers consume identical deterministic source data. Runtime quality changes tessellation and residency only, not terrain truth.

Hardware Impact: Offline double coordinate math costs editor bake time only; runtime i3/MX350 path avoids recomputing fractal/domain warp math.

## Decision 003 - Explicit 32-Byte DTO Layout

Problem: Fractal parameter DTOs entering Burst jobs need stable, ARM64-safe layout and CS1612-free raw field access.

Solution: Use unmanaged structs with explicit 32-byte layout, raw public fields, and named padding fields. Add editor validation for offsets and sizes.

Rejected Alternatives: Auto-properties, classes, reflection-bound settings, and implicit sequential layout are rejected because they create defensive copies, managed references, or unverifiable binary layout.

Scalability potential: Same DTO contract feeds mock sectors, preview tiles, global bake, and macro bake. Ultra can increase octave counts through authoring data without changing layout.

Hardware Impact: Alignment avoids ARM64 traps and improves vectorized Burst access; exact gain is PENDING VERIFICATION.

## Decision 004 - CSV Biome Blending In The Burst Path

Problem: A CSV facade that only changes UI defaults would not satisfy the terrain-domain requirement for regional geological recipes across the 100km AUP grid.

Solution: Load `terrain_macro_biomes.csv` into unmanaged `TopographyBiomeRecipeDTO` cold authoring records, convert them to 128-byte `TopographyBiomeKernelDTO` rows, and pass `NativeArray<TopographyBiomeKernelDTO>` into domain-warp, ridge, and macro jobs. Each pixel resolves continuous radius weights in AUP space and blends frequency, lacunarity, persistence, octave count, warp strength, terrace intent, and seed ownership.

Rejected Alternatives: Per-sector recipe selection was rejected because hard borders would create biome seams. Managed dictionaries and string-token recipe lookups inside generation loops were rejected because they allocate and break Burst locality.

Scalability potential: Low uses the same baked payload with cheaper runtime tessellation. Middle keeps the same data and moderate residency. High increases visible tile residency. Ultra spends saved runtime CPU on visual overkill and distant terrain rendering while terrain truth remains unchanged.

Hardware Impact: Runtime i3/MX350 avoids all biome-noise interpolation; cost is paid once in editor Burst jobs. Exact microseconds remain PENDING COMPILE/PROFILE.

## Decision 005 - Runtime Terrain Generation Fence

Problem: Legacy MapMagic refresh and seam writeback paths could still schedule terrain jobs or call Unity Terrain height mutation during play.

Solution: Gate MapMagic terrain postprocess job scheduling and graph tile refresh out of play mode. Gate `WorldGenerativeGeologyTerrainSeamApplier` height writebacks out of play mode before `float[,]` patch allocation/writeback.

Rejected Alternatives: Deleting MapMagic assets and seam systems was rejected because other domains still reference them as legacy/editor tooling and direct deletion would be cross-domain sabotage. Leaving runtime graph refresh intact was rejected because it hides unbounded third-party generation cost.

Scalability potential: Low-end devices remove multi-ms terrain graph/writeback spikes. Middle devices use predictable streaming. High and Ultra tiers spend the budget on rendering and VFX, not terrain truth mutation.

Hardware Impact: Estimated i3/MX350 gain is avoiding Unity Terrain `SetHeightsDelayLOD` and MapMagic graph rebuild spikes in play; exact microseconds remain PENDING because CPU load blocked compile/profiler verification.

## Decision 006 - Async H8BIN Serialization With Black Box

Problem: A full 100km terrain bake produces many sector payloads, and failure during generation must leave a forensic artifact instead of an untraceable partial bake.

Solution: Serialize every sector as a 128-byte explicit header plus row-major raw `float` payload through Unity `Awaitable.BackgroundThreadAsync` and pooled chunked `FileStream.Write` calls, validate checksum before promotion, and retain the last 300 sector/macro terminal bake states in a fixed NativeArray black box dump.

Rejected Alternatives: Unity `TerrainData`, Texture2D heightmaps, managed `float[,]`, JSON heights, and DataMonolith insertion were rejected because they add layout ambiguity, runtime coupling, or rollback/static-data ownership ambiguity.

Scalability potential: Low reads compact static sectors. Middle streams more tiles. High/Ultra keep larger terrain views resident while the binary contract remains identical.

Hardware Impact: Runtime hardware avoids generation entirely. Editor serialization uses 1MB chunks to avoid giant managed payload copies; exact write microseconds are PENDING VERIFICATION.

## Decision 007 - Continuous Quality Without Terrain Truth Drift

Problem: HECTON-8 rejects binary quality switches, but terrain truth must not change with runtime quality.

Solution: `GlobalQualityWeight` is consumed continuously for editor scheduling granularity, progress cadence, preview/tool settings, and report metadata while high-fidelity terrain truth remains AUP/seed deterministic.

Rejected Alternatives: Low/Ultra terrain datasets were rejected because they create multiple truths, seam risk, save identity ambiguity, and rollback verification ambiguity.

Scalability potential: Low, Middle, High, and Ultra share the same height data. Quality changes runtime tessellation/residency and optional tooling cadence only.

Hardware Impact: On weak devices, no fractal/domain-warp math runs at runtime. On top-tier devices, saved CPU is available for denser rendering of the same immutable terrain.

## Decision 008 - Kernel Recipe DTO And Squared Distance

Problem: The first biome blend path carried `FixedString64Bytes` authoring data near dense pixel jobs and used `sqrt` for radius/rift distance.

Solution: Keep `TopographyBiomeRecipeDTO` as a 192-byte cold authoring record, convert it to a 128-byte `TopographyBiomeKernelDTO`, and add `InvRadiusSqMeters`. Biome blending and rift carving now use squared-distance falloff in Burst.

Rejected Alternatives: Keeping `sqrt` was rejected because every sector pixel would pay transcendental cost. Passing the CSV recipe with name text into dense jobs was rejected because it wastes memory bandwidth.

Scalability potential: Low and middle devices benefit indirectly because runtime reads static payloads only. High and Ultra can spend saved runtime budget on denser rendering while editor bake cost remains bounded.

Hardware Impact: Removes two per-sample square roots from affected paths. Exact editor microseconds are PENDING until benchmark execution; runtime i3/MX350 path remains zero generation cost.

## Decision 009 - Terminal JobHandle Chain

Problem: The sector bake initially completed each stage immediately, creating four scheduler stalls per sector.

Solution: Schedule Domain Warp -> Ridge -> Terracing -> Rift as one dependency chain and call `Complete()` only once at terminal checksum/serialization readback. Report now records `pipeline_ms` and marks per-stage fields as legacy placeholders.

Rejected Alternatives: Retaining per-stage completes for convenient stopwatch numbers was rejected because the numbers were bought with artificial sync points.

Scalability potential: Low-end editor hardware gets fewer scheduler stalls. High-end CPUs keep more worker continuity across the whole sector pipeline.

Hardware Impact: Expected save is scheduler overhead per sector; exact microseconds are PENDING because compile/import/profiler are blocked.

## Decision 010 - Runtime MapMagic Generation Fence

Problem: Guarding only sandbox job scheduling and tile refresh overclaimed runtime purge while `MapMagicObject` could still be enabled in play mode.

Solution: `MapMagicRuntimeBridge` now disables `mapMagicObject.enabled` in play mode and skips terrain connectivity/repair mutation outside editor. Legacy read APIs remain as query surfaces until a dedicated `.h8bin` streaming owner exists.

Rejected Alternatives: Deleting MapMagic assets was rejected as third-party/domain sabotage. Leaving MapMagic enabled was rejected because it preserves hidden runtime graph generation authority.

Scalability potential: Low devices avoid live terrain generation stalls. Middle/High/Ultra use static terrain truth and spend runtime budget on rendering/VFX.

Hardware Impact: Estimated multi-ms spike avoidance during terrain graph/update events; exact proof requires Play Mode profiler.

## Decision 011 - Proof Artifact Honesty

Problem: Status/log language marked report-producing tasks as done while Unity menu execution had not produced JSON reports or `.h8bin` files.

Solution: Status now labels those tasks as STATIC_SOURCE with REPORT/ARTIFACT pending. `WORLD_OPTIMIZATION_REPORT_SHINOBU_240.json` is used to avoid overwriting another agent's report.

Rejected Alternatives: Keeping the original `WORLD_OPTIMIZATION_REPORT.json` target was rejected because the file currently belongs to adjacent work and would destroy another proof artifact.

Scalability potential: No runtime effect. It prevents integrators from routing around missing import/bake proof.

Hardware Impact: 0 runtime us. Production impact is avoiding false-green integration.

## Decision 012 - Binary Validation Tightening

Problem: Header/checksum validation alone could accept a payload containing NaN floats if the checksum matched the bad bytes.

Solution: Self-audit now streams payload chunks from pooled buffers, verifies checksum, rejects non-finite floats, rejects values outside header contract range, and checks expanded DTO offsets.

Rejected Alternatives: Trusting checksum-only validation was rejected because it detects transport corruption but not mathematically poisonous payloads.

Scalability potential: Low-to-Ultra runtime consumers receive cleaner static data; validation is editor/import cost only.

Hardware Impact: Runtime impact 0 us. Editor validation cost is sequential I/O over payload bytes; exact time pending generated files.

## Decision 013 - Cold Dump Scratch Pooling And Writer Path Guard

Problem: The black-box crash dump path allocated fresh managed `byte[]` buffers, and the h8bin writer trusted `Path.GetDirectoryName(path)` to return a non-empty string. Both are cold/editor paths, but they are still avoidable failure points during a long global bake.

Solution: `DumpBlackBox` now rents header and telemetry scratch from `ArrayPool<byte>` and writes only exact byte counts. `WriteHeightmapAsync` now checks the directory string before creating it.

Rejected Alternatives: Keeping fresh arrays was rejected because crash-forensics code should not add avoidable managed pressure during fault handling. Blind `Directory.CreateDirectory(Path.GetDirectoryName(path))` was rejected because a malformed or direct filename path can trip a null/empty path exception before the writer reaches its checksum validation.

Scalability potential: Low/Middle/High/Ultra terrain truth is unchanged. The editor bake path becomes less fragile under long-running generation and repeated fault dumps.

Hardware Impact: Runtime impact 0 us. Editor/crash path removes two managed array allocations per dump and one avoidable path exception branch; exact microseconds are pending Unity execution.

## Decision 014 - Unity Awaitable Bake Flow

Problem: The first editor bake flow used `System.Threading.Tasks` and `async Task`, which violates the current Unity 6 async rule even though the code is offline/editor-only.

Solution: Replace the SHINOBU_240 bake flow with Unity `Awaitable`, `Awaitable.NextFrameAsync`, `Awaitable.BackgroundThreadAsync`, and `Awaitable.MainThreadAsync`. The `.h8bin` writer now runs chunked FileStream writes on the background thread and returns to main thread before the bake pipeline continues.

Rejected Alternatives: Keeping `async Task` was rejected because adjacent SHINOBU_241 already proved this policy debt matters for static gates. Keeping hidden `FileStream.WriteAsync` Task awaits was rejected because it would evade grep while retaining Task machinery. Main-thread blocking writes were rejected because Task 10 requires responsive serialization.

Scalability potential: Low/Middle editor machines can yield between sectors and during file writes; High/Ultra can write larger sector payloads through the same path without changing terrain truth.

Hardware Impact: Runtime impact 0 us. Editor impact is reduced Task-policy debt and less warning triage; exact allocation delta remains pending Unity profiler/import proof.

## Decision 015 - Hadal Trench Boundary Discipline

Problem: Task 09 says the heightmap rift carve must interface with the Hadal Trench generator, but SHINOBU_241 currently owns a separate YELLOW static-source SDF `.h8bin` payload, not a GREEN fault-line sidecar API.

Solution: Keep `ApplyTectonicRiftsJob` on SHINOBU_240-owned `TectonicRiftSegmentDTO` rows and document the boundary. No direct asmdef reference to `Hecton8.World.OfflineHadalTrenchBaker` is introduced. A future integration should consume a GREEN fault-line sidecar contract, not reverse-engineer SHINOBU_241 voxel density bytes.

Rejected Alternatives: Directly referencing SHINOBU_241 editor/runtime assemblies was rejected as compile-wall coupling. Parsing the trench voxel `.h8bin` to derive heightmap splines was rejected because it inverts ownership and duplicates truth.

Scalability potential: Low-to-Ultra terrain rendering keeps one immutable height truth. Future trench SDF can enrich near-field cliffs through its own streaming owner without changing SHINOBU_240 heightmap ABI.

Hardware Impact: Runtime impact 0 us. Avoids introducing cross-domain import churn and prevents a low-end loader from parsing unrelated SDF density just to bake a heightfield.

## Decision 016 - H8BIN Endian Marker And Scanner Loop Hardening

Problem: The heightmap header was little-endian by writer/validator assumption but did not carry an explicit endian marker/schema hash, leaving future loaders with a weaker corruption gate. The Roslyn scanner also still used `foreach`, and the background-thread writer kept `FileOptions.Asynchronous` despite no longer using Task-backed async file APIs.

Solution: Use two reserved header fields as `EndianMarker@96 = 0x01020304` and `SchemaHash@100 = 0xA2400001`, write them during header construction, and reject mismatches in `TopographyForgeSelfAudit`. Replace scanner `foreach` loops with explicit enumerators and remove `FileOptions.Asynchronous` from the background-thread writer.

Rejected Alternatives: Leaving endian identity implicit was rejected because raw `.h8bin` payloads need defensive loader gates. Keeping `foreach` was rejected because the mandate forbids it even though this scanner is cold/editor-only. Keeping `FileOptions.Asynchronous` was rejected because the writer already executes on a background thread and the flag creates misleading Task/async-I/O proof language.

Scalability potential: Low-to-Ultra terrain truth is unchanged. Stronger header identity lets future streaming tiers validate and reject bad sidecars before touching runtime residency buffers.

Hardware Impact: Runtime impact remains 0 us until a streaming owner imports the payload. Future low-end loader impact is fail-fast validation before reading large payload bodies; exact microseconds require generated files and loader proof.

## Decision 017 - Awaitable Single-Consumer Fence And Blackbox Wording

Problem: `_activeBakeOperation` stored Unity `Awaitable` as if it were a reusable Task handle. Unity Awaitable values are pooled and single-consumer; retaining one in static state creates a stale-handle hazard. Rationale also overstated the blackbox as 300 frame states while current source records terminal sector/macro bake states.

Solution: Remove the cached `Awaitable` field and launch the editor bake with `_ = RunBakeAsync(...)`, relying on `_isBaking` and `_cancelRequested` for ownership state. Update proof wording to `sector/macro terminal bake states`.

Rejected Alternatives: Returning/caching the same `Awaitable` for external observation was rejected because it would invite double-await or stale pooled handle use. Expanding telemetry to per-frame recording was rejected for this pass because the offline baker has no runtime frame owner; terminal sector/macro state is the accurate proof currently implemented.

Scalability potential: Low/Middle editor machines avoid a stale async handle during long bakes. High/Ultra behavior is unchanged; bake throughput is still governed by Burst jobs and background writer.

Hardware Impact: Runtime impact 0 us. Editor impact is correctness hardening, not a measured speed gain.

## Decision 018 - Preview Pixel Buffer Native Upload

Problem: The live preview retained a static managed `Color32[]` scratch buffer. It was editor-only, but it contradicted the Forge's native-buffer discipline and left a managed array in the SHINOBU_240 surface.

Solution: Allocate a local `NativeArray<Color32>` for the preview pass, fill it with index loops, upload through `Texture2D.SetPixelData`, and dispose it in the same `finally` block as the height scratch arrays.

Rejected Alternatives: Keeping `Texture2D.SetPixels32(Color32[])` was rejected because it requires a managed array and weakens the static proof gate. A persistent native preview buffer was rejected because preview scratch is tiny and does not need lifetime beyond one build.

Scalability potential: Low preview quality still collapses to 64 px; High/Ultra preview stays 128 px. The upload route remains identical across tiers and does not change terrain truth.

Hardware Impact: Runtime impact 0 us. Editor preview avoids one retained managed pixel array; exact GC delta pending Unity profiler.

## Decision 019 - Native Run State And CSV Recipe Bridge

Problem: The editor baker still used a managed `BakeRunState` class and managed `List<TopographyBiomeRecipeDTO>` bridge containers. They were cold editor allocations, but they weakened the source-level proof that SHINOBU_240's generation state is explicit, blittable, and layout-audited.

Solution: Replace `BakeRunState` with `TopographyBakeRunStateDTO`, an explicit 192-byte unmanaged DTO containing the 128-byte metrics block and black-box cursor. Store it in a one-row local `NativeArray<TopographyBakeRunStateDTO>` for mock/global bake lifetimes and mutate it through `UnsafeUtility.AsRef`. Change the CSV parser and preview to use local `NativeList<TopographyBiomeRecipeDTO>` bridges before conversion into `NativeArray<TopographyBiomeKernelDTO>`. In the global async bake, the `NativeList` is scoped to a synchronous load-copy-dispose helper, so only the persistent kernel `NativeArray` crosses `await` boundaries.

Rejected Alternatives: Keeping the managed class was rejected because async editor state does not need object identity. Keeping managed `List<T>` was rejected because the parser already writes unmanaged recipe DTOs and can use NativeList without changing the CSV contract. A static native recipe cache was rejected because it would introduce cross-preview lifetime ownership that this offline tool does not need.

Scalability potential: Low preview still resolves at 64 px and discards scratch immediately. Middle/High/Ultra keep the same immutable terrain truth and can spend runtime budget on denser terrain presentation; the editor bridge changes do not alter payload format or runtime authority.

Hardware Impact: Runtime impact 0 us. Editor impact is removal of one managed bake-state object and two managed recipe-list surfaces per global/preview route, plus shorter recipe-bridge lifetime before sector awaits; exact GC and microsecond deltas remain PENDING UNITY PROFILER.

## Decision 020 - H8BIN Header Fatalism Gate

Problem: `.h8bin` validation checked magic, version, stride, payload bytes, checksum, finite payload floats, and rollback flags, but it still allowed a hostile or corrupted header to reach payload-length arithmetic with extreme positive dimensions or invalid pixel/height ranges.

Solution: Add `MaximumHeightmapResolution = 4096` to the SHINOBU_240 heightmap contract and reject headers with dimensions outside `1..4096`, non-finite or non-positive pixel size, non-finite or inverted height contract, and observed min/max outside the contract before expected payload arithmetic and chunk scans.

Rejected Alternatives: Trusting generated files was rejected because the self-audit is the import gate for future runtime streaming owners. Expanding the limit above the sanitizer's max resolution was rejected because no current SHINOBU_240 path writes sectors or macro maps above 4096.

Scalability potential: Low-to-Ultra runtime presentation stays unchanged. Stronger static validation prevents weak hardware from touching malformed payloads or wasting I/O on impossible dimensions.

Hardware Impact: Runtime impact 0 us in SHINOBU_240. Future loader/import path fails before scanning large payloads; exact microseconds require generated corrupted/valid file benchmarks.

## Decision 021 - Macro Rift Curve Consistency

Problem: `GenerateMacroHeightmapJob` used squared-distance trench width but did not apply the same `FalloffPower` curve and `Config.RiftDepthMeters` fallback used by `ApplyTectonicRiftsJob`. That created a static-source risk that the permanently resident macro overview could show shallower or softer trench silhouettes than the high-resolution sector payloads.

Solution: Mirror the sector rift carve formula in the macro job: squared distance to segment, guarded width reciprocal, `math.pow(t, max(0.25, FalloffPower))`, `math.smoothstep`, and per-rift depth with config fallback.

Rejected Alternatives: Leaving macro as a cheaper curve was rejected because this is offline/editor bake math and macro topology must stay visually coherent with sector truth. Runtime downsampling from high-res sectors was rejected because Task 11 requires a dedicated macro payload and would force loading too much data for distant topology.

Scalability potential: Low devices stream the compact macro payload for distant mountains without sector residency. Middle keeps macro plus nearby sector meshes. High/Ultra can extend terrain view distance while macro and sector rift silhouettes remain coherent across LOD transitions.

Hardware Impact: Runtime impact remains 0 us/frame in SHINOBU_240. Editor macro bake pays one `math.pow` per rift contribution, but avoids runtime correction, distant topology mismatch, and any later mesh/terrain patch pass.

## Decision 022 - Kernel Ternary Pruning

Problem: The core noise helpers and rift width/depth selection still carried data-local ternaries where the input sanitizers already guarantee safe ranges. They were not correctness bugs, but they weakened the Task 20 branchless/vectorization proof for dense editor jobs.

Solution: Replace ridged and domain-warp normalization with guarded reciprocals, replace rift width/depth fallback with `math.select`, and replace macro normalized coordinate ternaries with guarded reciprocal denominators.

Rejected Alternatives: Removing all finite fallback branches was rejected because NaN vaccination must remain stronger than vectorization aesthetics. Changing recipe selection logic was rejected because biome seed ownership is data-dependent and must stay predictable.

Scalability potential: Low/Middle editor hardware gets slightly cleaner Burst IR for the common finite path. High/Ultra can increase macro resolution or visible terrain distance using the same immutable payload truth.

Hardware Impact: Runtime impact remains 0 us/frame. Editor impact is reduced branch pressure in the dense noise and rift loops; exact microseconds require Burst Inspector and mock-sector profiler artifacts.

## Decision 023 - Biome Mask Sidecar H8BIN

Problem: The SHINOBU_240 batch requires raw heightmap arrays and biome masks, but the current binary route only emitted height floats. CSV biome recipes affected generation math yet did not leave an immutable sidecar payload for runtime material/streaming consumers.

Solution: Add RGBA `float4` biome-mask sidecars beside each sector and macro heightmap. `BiomeMaskFileHeaderDTO=128` carries magic `0x4D423854`, schema hash `0xA2400002`, endian marker, sector AUP, pixel size, channel count, recipe count, payload bytes, stride, checksum, and rollback exclusion. `GenerateBiomeMaskJob` and `GenerateMacroBiomeMaskJob` resolve the first four biome recipe weights in AUP space, normalize them to sum 1, and write row-major `float4` payloads. Self-audit now routes by file magic and validates finite RGBA payloads, range, sum, checksum, and header identity.

Rejected Alternatives: Packing biome weights into Unity `Texture2D` assets was rejected because it creates importer/state ambiguity and weakens flat binary validation. Recomputing biome weights at runtime was rejected because it reintroduces terrain-generation math on the frame path. Storing biome weights inside the height payload was rejected because it would break the height ABI and force every height consumer to parse material data.

Scalability potential: Low devices can stream one compact immutable RGBA mask and use nearest or coarse material selection. Middle devices keep the same payload with moderate terrain residency. High and Ultra can spend saved runtime CPU on shader blending, caustics, silt, and richer material transitions from the same sidecar without changing terrain truth, DTO layout, save identity, or rollback boundary.

Hardware Impact: Runtime i3/MX350 path avoids per-frame biome falloff math and string/CSV lookup. Editor bake pays one additional independent Burst job per sector/macro plus one sidecar write. Exact microseconds remain pending Unity import, Burst Inspector, generated `.h8bin`, and profiler proof.

## Decision 024 - Biome Mask Channel Count Honesty

Problem: The RGBA biome-mask sidecar physically encodes four channels, but `RecipeCount` initially mirrored the full CSV recipe count. If designers authored more than four recipes, the file header could imply more encoded masks than the payload actually contains.

Solution: Clamp `BiomeMaskFileHeaderDTO.RecipeCount` to `0..4`, reject any file whose stored recipe count exceeds channel count, and emit `WarningBiomeMaskRecipeOverflow` when source recipes exceed the encoded RGBA capacity. The bake report now exposes `biome_mask_invalid`, `biome_mask_recipe_overflow`, and a critical warning that includes invalid masks.

Rejected Alternatives: Expanding the sidecar to variable channel counts was rejected because it would break fixed-stride runtime parsing and shader binding. Silently ignoring extra recipes was rejected because it hides authoring loss. Allocating a managed list of overflow recipe names in the report was rejected because this route only needs a deterministic warning flag and the CSV authoring surface already names the source file.

Scalability potential: Low devices still receive a fixed 16-byte-per-pixel RGBA payload. Middle, High, and Ultra tiers can bind the same mask consistently; richer biome presentation should be layered through additional versioned payloads, not by widening this ABI silently.

Hardware Impact: Runtime impact remains 0 us/frame. Editor impact is one integer clamp and one recipe-count branch per sector/macro bake, plus clearer report flags; exact time is below measurement noise and remains pending Unity profiler proof.

## Decision 025 - Explicit Editor Preprocessor Fence

Problem: SHINOBU_240 source files lived under an Editor folder, but the batch prompt explicitly requires pure Editor utilities. Relying only on folder placement leaves a weak static proof if files are moved or asmdef boundaries shift.

Solution: Wrap every `TopographyForge*.cs` source file in `#if UNITY_EDITOR` / `#endif` while keeping the existing Editor folder boundary. This creates a second compile fence around the offline baker, scanner, UI Toolkit facade, DTOs, jobs, CSV bridge, and self-audit.

Rejected Alternatives: Leaving the folder-only fence was rejected because the task demanded explicit editor utility confinement. Moving files into a new asmdef was rejected because it would increase compile-wall churn and risk conflicts with adjacent agents in the shared Editor assembly.

Scalability potential: No runtime quality route changes. Low/Middle/High/Ultra all retain zero runtime terrain-generation ownership from SHINOBU_240; the fence protects player builds from accidental import of editor-only bake code.

Hardware Impact: Runtime impact remains 0 us/frame. Editor compile impact is negligible source preprocessing; exact compile timing remains pending because CPU guard blocks rebuild.

## Decision 026 - Duplicate Attribute Compile Risk Removal

Problem: `TopographyBiomeBlendMath.ResolveWeight` carried two identical `[MethodImpl(MethodImplOptions.AggressiveInlining)]` attributes after kernel polish. This is a static compile risk because `MethodImplAttribute` is not a multi-instance marker.

Solution: Remove the duplicate attribute and keep a single aggressive-inline directive on the dense biome/rift falloff helper.

Rejected Alternatives: Leaving the duplicate until Unity import was rejected because it can fail script compilation before any Burst or bake validation runs. Removing the inline directive entirely was rejected because the helper sits in dense pixel/rift loops and should remain inlineable.

Scalability potential: Low/Middle/High/Ultra terrain truth and runtime presentation are unchanged. The fix preserves the compile route required before mock-sector and full-bake proof can be generated.

Hardware Impact: Runtime impact remains 0 us/frame. Editor compile impact is correctness, not a measurable speed gain. CPU guard still blocks rebuild, so compiler proof remains pending.

## Decision 027 - Biome Mask Semantic Tag Byte Order

Problem: `BiomeMaskSemanticsHash` used `0x52474241`, which stores ASCII `ABGR` on little-endian disk even though the payload contract is `float4` RGBA weights.

Solution: Change the semantic tag to `0x41424752`, which serializes as `RGBA` bytes on disk. The writer and validator share the same constant, so the static ABI remains self-consistent, and no file migration is required because generated `.h8bin` artifacts are still pending.

Rejected Alternatives: Leaving the tag as a private hash was rejected because the ledger and docs call it a semantic tag, not an opaque checksum. Reordering payload lanes was rejected because the Burst jobs and future shader consumers already treat `float4` as RGBA.

Scalability potential: Low/Middle/High/Ultra payload identity stays fixed-width and fixed-lane. Runtime material consumers can bind the same RGBA contract without guessing channel order.

Hardware Impact: Runtime impact remains 0 us/frame. Future loader impact is fail-fast semantic clarity before binding sidecar data; exact microseconds are not applicable.

## Decision 028 - Superseded Continuous Quality Math LOD Inside Burst Kernels

Problem: `GlobalQualityWeight` previously affected scheduler batch size and preview resolution, but the dense ridged multifractal/domain-warp/terrace math still used authored tap counts at every quality. That preserved a hidden high-cost path on weak editor hardware and weakened the continuous scalability proof.

Solution: This route was rejected after re-reading SHINOBU_240 constraint 5. `TopographyQualityMath` remains as a preview/input-collapse helper, but production sector, macro, and mock payload jobs do not consume quality LOD inside dense pixel loops. Decision 029 fixes terrain truth to full fidelity; Decision 030 removes per-pixel quality ALU from production jobs.

Rejected Alternatives: Keeping quality inside production dense jobs was rejected because final `.h8bin` terrain truth must not depend on machine state or an editor slider. Binary low/high switches were still rejected; preview quality remains continuous. Creating alternate DTOs or file schemas was rejected because quality must not change payload identity, rollback exclusion, or authority route.

Scalability potential: Low devices get cheaper preview feedback and runtime LOD from downstream streaming/rendering owners. Middle/High/Ultra consume the same full-fidelity payload truth and can spend runtime budget on presentation from the immutable dataset.

Hardware Impact: Runtime impact remains 0 us/frame because SHINOBU_240 is offline/editor-owned. Editor full-bake impact from Decision 028 is intentionally voided by Decisions 029 and 030; preview-only tap reduction remains unmeasured until Unity profiler proof exists.

## Decision 029 - Final Payload Truth Forces Full Fidelity

Problem: Applying `GlobalQualityWeight` directly to production sector and macro bake configs conflicted with the SHINOBU_240 primary prompt. The final `.h8bin` terrain files must be the maximum-fidelity immutable dataset; runtime Agent 245/streaming/rendering quality should decide how that data is displayed, not rewrite terrain truth based on an editor slider.

Solution: Force `TopographyBakeConfigDTO.GlobalQualityWeight = 1f` in `BuildSectorConfig`, which feeds sector, macro, and mock bakes. `TopographyQualityMath` now runs before preview jobs to collapse input parameters, not inside production dense pixel loops. Scheduler batch sizing can still consume the slider because batch size changes editor throughput only, not output bytes. The bake report now writes `payload_math_quality_weight=1.0` and `quality_weight_affects_payload_truth=false` so generated JSON cannot imply slider-dependent terrain truth.

Rejected Alternatives: Keeping low-quality final payload bakes was rejected because it makes terrain truth depend on machine or designer slider state. Removing `TopographyQualityMath` entirely was rejected because the preview still needs continuous low-cost feedback and the wider mandate rejects binary quality switches. Adding a second payload schema for low-quality bakes was rejected because it would widen ABI and confuse runtime authority.

Scalability potential: Low devices still benefit at runtime through the streaming/rendering owner consuming the same high-fidelity source with cheaper LOD/tessellation. Editor preview remains cheap at low quality. High and Ultra receive the same payload truth and can spend rendering budget on richer presentation.

Hardware Impact: Runtime impact remains 0 us/frame in SHINOBU_240. Editor full bake no longer reduces dense tap counts at low slider values; that is intentional to preserve immutable terrain truth. Preview retains the low-quality tap reduction path. Exact microseconds remain pending Unity import/profiler proof.

## Decision 030 - Evict Quality ALU From Production Pixel Loops

Problem: After forcing production bake quality to `1f`, the dense jobs still called `TopographyQualityMath` per pixel. The branchless math returned authored values but still cost ALU across sector and macro payload generation.

Solution: Move quality reduction to preview input construction. The preview converts CSV recipes, fallback ridge/warp, and terrace settings through `TopographyQualityMath` before running the existing jobs. Production sector, macro, and mock bakes pass full-fidelity parameters and the job file now has zero `TopographyQualityMath.` call sites inside job execution code.

Rejected Alternatives: Keeping the per-pixel helper was rejected because full bakes should not pay quality-sanitizer ALU when terrain truth is fixed at full fidelity. Adding separate preview-only job structs was rejected because it duplicates the terrain math and weakens preview/full parity.

Scalability potential: Low editor preview still gets continuous cheap feedback. Production payload generation stays deterministic full fidelity. Middle/High/Ultra keep the same payload and can spend runtime quality budget in streaming/rendering.

Hardware Impact: Runtime impact remains 0 us/frame. Editor full-bake impact removes several scalar lerp/clamp operations per dense pixel path; exact microseconds remain pending Burst Inspector/profiler proof.

## Decision 031 - Deterministic Black Box Ring Initialization

Problem: The 300-entry telemetry ring was allocated with `NativeArrayOptions.UninitializedMemory`. If the global bake failed before all 300 entries were recorded, `DumpBlackBox` could write uninitialized forensic slots, polluting the crash artifact.

Solution: Keep `UninitializedMemory` allocation to avoid hidden allocator clear semantics, then explicitly write a default `TopographyBakeTelemetryEntry` through a fixed index loop immediately after allocation. The ring is only 300 * 64 bytes, so this is deterministic forensic hygiene, not a massive heightmap memset.

Rejected Alternatives: Using `NativeArrayOptions.ClearMemory` was rejected because the SHINOBU static gate intentionally scans for implicit clear paths. Leaving uninitialized entries was rejected because a black-box dump must be interpretable even on early failure. Expanding the dump header to variable count was rejected because the black-box contract is a fixed 300-entry ring.

Scalability potential: Low/Middle/High/Ultra runtime terrain truth is unchanged. Editor crash forensics become stable across early and late bake failures.

Hardware Impact: Runtime impact remains 0 us/frame. Editor bake pays 300 sequential 64-byte stores once per global bake, approximately 19.2 KB of memory traffic; this is below measurement noise and buys deterministic diagnostics.

## Decision 032 - CSV Scientific Notation Without Managed Parsing

Problem: Terrain recipe frequencies are naturally authored as scientific notation (`3.2e-4`, `1E-3`). The CSV bridge only accepted plain decimal notation, which would force designers either to rewrite values manually or hit a cold import error despite the values being valid terrain constants.

Solution: Extend the byte-level numeric parser, now named `ParseDoubleCell`, with an explicit exponent branch for `e`/`E`, optional exponent sign, digit validation, and finite-result validation. The parser still operates on the native byte buffer and does not use `float.Parse`, `double.Parse`, substrings, culture state, LINQ, or managed token allocation.

Rejected Alternatives: Using `float.Parse`/`double.Parse` on substrings was rejected because it allocates and inherits culture ambiguity. Requiring fixed decimal notation was rejected because it weakens the human tuning bridge. Allowing unchecked exponents was rejected because malformed or extreme values must fail before entering DTOs.

Scalability potential: Low/Middle/High/Ultra payload truth is unchanged. Designers can author compact frequency values without C# recompiles or hidden managed parsing costs.

Hardware Impact: Runtime impact remains 0 us/frame. Editor CSV import adds a small branch only when a numeric cell contains `e`/`E`; exact microseconds are below measurement relevance and remain pending Unity profiler proof.

## Decision 033 - Offline Editor Island Memory Boundary

Problem: `TopographyForgeGenerator.cs` imported `Hecton8.Core.Memory` and used `H8Memory`, but `Hecton8.World.OfflineGeology.Editor.asmdef` intentionally references only Unity Burst/Collections/Jobs/Mathematics. That source/asmdef mismatch is a direct compile risk and would force the offline SHINOBU_240 editor island to depend on core runtime memory infrastructure.

Solution: Remove the accidental `Hecton8.Core.Memory` import, `SystemID` constant, and `H8Memory` allocation wrapper. SHINOBU_240 scratch remains local editor-only `NativeArray<T>` memory, allocated inside bake/preview scopes and disposed through `finally` with terminal job completion before readback or release.

Rejected Alternatives: Adding `Hecton8.Core.Memory` to the editor asmdef was rejected because it widens the compile wall and pulls runtime memory infrastructure into an offline terrain authoring island. Moving this local scratch into `GlobalDataVault` was rejected because these buffers do not cross runtime, scene, save, rollback, relocation, or domain ownership boundaries. Keeping the mismatch was rejected because Unity import would fail before bake/audit execution.

Scalability potential: Low/Middle/High/Ultra runtime terrain truth is unchanged. Editor full-bake memory remains bounded by sector-local scratch and is released after each sector/macro/mock route; runtime presentation scalability stays owned by Agent 245/streaming/rendering.

Hardware Impact: Runtime impact remains 0 us/frame. Editor impact is compile-wall reduction and one fewer runtime-assembly dependency during script import; exact compile-time delta remains pending Unity import proof.

## Decision 034 - Explicit Roslyn Scanner Route And Unsafe Lane Proof

Problem: `TopographyForgeScanners.cs` uses Roslyn AST APIs, but the SHINOBU_240 editor asmdef did not explicitly reference the Roslyn precompiled assemblies. The Burst jobs also disabled parallel-for restrictions on output arrays without a local written invariant explaining why the unsafe pointer writes are non-overlapping.

Solution: Set `Hecton8.World.OfflineGeology.Editor.asmdef` to explicit precompiled-reference mode and list `Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.CSharp.dll`, `System.Collections.Immutable.dll`, and `System.Reflection.Metadata.dll`, matching the existing voxel seam scanner route. Add a local invariant before each unsafe output lane: `Execute(index)` writes exactly its own index, while `[NoAlias]` declares independent native buffers and `UnsafeUtility.AsRef` avoids indexer-copy mutation.

Rejected Alternatives: Relying on implicit Roslyn auto-reference was rejected because adjacent scanner asmdefs already prove explicit references are required in this project. Removing Roslyn was rejected because the task needs AST-level scanner proof, not brittle string grep. Removing `NativeDisableParallelForRestriction` and returning to indexer writes was rejected because the current proof route intentionally uses raw pointer stores for CS1612-free mutation.

Scalability potential: Low/Middle/High/Ultra runtime terrain truth is unchanged. Editor scanner import becomes deterministic, and dense bake jobs preserve data-local one-index writes across all hardware tiers without adding sibling runtime assembly dependencies.

Hardware Impact: Runtime impact remains 0 us/frame. Editor compile/import impact is correctness and route determinism, not a measured speed claim. Dense job runtime within the editor keeps the existing pointer-store path; exact microseconds remain pending Burst Inspector/profiler proof.

## Decision 035 - Black Box Exception Reason Preservation

Problem: The outer async bake catch wrote every exception dump with `WarningNaNHeight`, even when the failure came from async serialization, file validation, or biome-mask validation. That could overwrite an earlier sector-specific dump header with a misleading reason.

Solution: Build the catch dump reason from `WarningAsyncWriteFailed` plus any already-recorded fatal metric bits (`WarningNaNHeight`, `WarningInvalidBiomeMask`, `WarningBiomeMaskRecipeOverflow`). Sector and macro generation still dump immediately with the exact fatal warning bits before serialization attempts.

Rejected Alternatives: Leaving the blanket NaN reason was rejected because black-box forensics must distinguish math poison from I/O/validation failure. Skipping the outer catch dump was rejected because exceptions can occur before a sector-specific fatal dump exists. Adding managed exception text to the binary dump was rejected because the fixed dump ABI is intentionally tiny and blittable.

Scalability potential: Low/Middle/High/Ultra runtime terrain truth is unchanged. Editor failure diagnosis is sharper during long global bakes, reducing re-run waste without touching payload ABI.

Hardware Impact: Runtime impact remains 0 us/frame. Editor impact is a few integer OR operations only on failure paths; exact microseconds are not relevant.

## Decision 036 - Sector-Level NaN Accounting And Sub-Meter Sector Guard

Problem: `AnalyzeHeights` incremented `NaNSectors` once per non-finite sample, so a single poisoned sector could report thousands or millions of bad sectors. `SanitizeSettings` also defaulted sector counts by dividing by `(int)SectorSizeMeters`, which can become zero if a caller supplies a positive sub-meter sector size.

Solution: Track a local `sectorContainsNaN` flag while scanning the height payload and increment `NaNSectors` once after the scan. Clamp sanitized `SectorSizeMeters` to at least `1f` before any integer cast or default sector-count division.

Rejected Alternatives: Leaving per-sample NaN counts was rejected because the metric name and report semantics are sector-level. Renaming the field to `NaNSamples` was rejected because it would change DTO/report semantics without improving the black-box route. Only rejecting non-positive sector sizes was rejected because positive sub-meter input still creates an integer zero divisor.

Scalability potential: Low/Middle/High/Ultra payload truth is unchanged. Failure reports remain proportional to sector ownership instead of sample count, and pathological editor inputs fail safe before bake scheduling.

Hardware Impact: Runtime impact remains 0 us/frame. Editor hot analysis removes repeated metric mutations during poisoned payload scans and adds one boolean branch; exact microseconds are pending Unity profiler proof.

## Decision 037 - Atomic Backup Retention, Chronological Black Box, And Roslyn Fence Tightening

Problem: A read-only subagent audit found three forensic weaknesses: `File.Replace` created `.bak` files that were immediately deleted, `DumpBlackBox` serialized the circular ring in physical array order, and the runtime terrain scanner could mark `if (!Application.isPlaying) return;` as a safe play-mode fence because it used text search instead of AST control shape.

Solution: Keep the current `.bak` after `File.Replace`; only prune `.bak.prev` after the new artifact validates. Serialize the black-box body oldest-to-newest from `cursor % count`, while preserving the cursor in the header. Record sector and macro start rows before allocations/job fences so a failure during scheduling or terminal completion names the in-flight route. Replace the scanner text search with Roslyn statement analysis that accepts only a preceding positive `Application.isPlaying` return guard in the same or enclosing block chain.

Rejected Alternatives: Immediate backup deletion was rejected because it destroys recovery after a corrupt replacement. Dump-reader-only reinterpretation was rejected because the binary body should be useful without hidden physical-ring knowledge. Keeping text search was rejected because inverse guards are common and the scanner already pays for Roslyn AST parsing.

Scalability potential: Low/Middle/High/Ultra payload truth is unchanged. Editor failure recovery is stronger for long bakes, and scanner reports become less noisy before runtime owners spend time chasing false safety flags.

Hardware Impact: Runtime impact remains 0 us/frame. Editor write path retains one backup file and may keep one `.bak.prev` until validation completes; black-box dump adds 300 fixed 64-byte ordered copies only on crash/fatal paths; scanner cost remains cold report generation.

## Decision 038 - Post-Promotion Validation Recovery

Problem: The writer validated the temp `.h8bin` before promotion and then validated the promoted artifact after `File.Replace`, but a post-promotion validation failure could leave the invalid promoted file at the active path while the previous good artifact sat in `.bak`.

Solution: On promoted heightmap or biome-mask validation failure, restore the previous `.bak` into the active path when it exists, restore `.bak.prev` back to `.bak` when present, and retain the rejected promoted file as `.failed`. If no backup exists, move the invalid promoted file to `.failed` so the active path does not advertise corrupt terrain truth.

Rejected Alternatives: Leaving the invalid promoted artifact active was rejected because runtime/import tooling may consume the active path before a human notices the exception. Deleting the invalid promoted bytes outright was rejected because a `.failed` artifact is useful forensic evidence when a validator or storage layer misbehaves after temp validation.

Scalability potential: Low/Middle/High/Ultra payload truth is unchanged. The editor bake route now fails closed instead of exposing a corrupt active artifact to streaming/import owners.

Hardware Impact: Runtime impact remains 0 us/frame. Editor failure path adds one file replace/move operation only after an already-exceptional post-promotion validation failure; no steady-state bake cost is added.

## Decision 039 - Failed Artifact Rotation And Restore Priority

Problem: The post-promotion recovery path deleted any previous `.failed` artifact before capturing a newly rejected promoted file. If that stale `.failed` file could not be removed, recovery could depend on a blocked forensic path instead of prioritizing restoration of the previous valid `.bak`.

Solution: Rotate an existing `.failed` artifact to `.failed.prev` before recovery. If the `.failed` path is still blocked, prioritize restoring the active file from `.bak` by deleting the rejected active artifact and moving `.bak` into place, dropping the new rejected bytes rather than leaving corrupt terrain truth active.

Rejected Alternatives: Deleting every previous `.failed` artifact was rejected because it loses forensic evidence across repeated storage/validator failures. Letting `File.Replace` fail because `.failed` is locked was rejected because recovery must prefer a valid active terrain artifact over preserving the newest rejected bytes.

Scalability potential: Low/Middle/High/Ultra payload truth is unchanged. Editor bake recovery now preserves one prior failed artifact and still fails closed when the failed-artifact path itself is unavailable.

Hardware Impact: Runtime impact remains 0 us/frame. Editor successful writes pay 0 us. Exceptional recovery may add one file move for `.failed.prev`, or one delete/move fallback when failed-byte capture is unavailable.

## Decision 040 - Restore Failure Surfacing And AUP Header Validation

Problem: Read-only subagent audit found that post-promotion restore failures were still swallowed after best-effort recovery, and `.h8bin` validators accepted finite payloads even when `SectorAup` metadata contained NaN or Infinity.

Solution: Propagate restore IO/permission failures through `InvalidDataException` with the original validation error preserved as the outer message. Add explicit finite checks for `HeightmapFileHeaderDTO.SectorAup` and `BiomeMaskFileHeaderDTO.SectorAup` before payload-length and checksum validation.

Rejected Alternatives: Swallowing restore failures was rejected because an invalid active artifact could remain in place without a visible recovery failure. Relying on payload finite checks was rejected because AUP metadata is part of terrain authority and can poison downstream origin-relative math even when payload floats are valid.

Scalability potential: Low/Middle/High/Ultra payload truth is unchanged. Every tier receives the same fail-fast static-data contract, and corrupt origin metadata is rejected before streaming/rendering owners can consume it.

Hardware Impact: Runtime impact remains 0 us/frame. Editor validation adds six scalar double finite checks per file; this is below measurement relevance and prevents expensive downstream forensic churn.

## Decision 041 - Preview Texture Reload Lifecycle

Problem: Static-source scan found the editor preview's only remaining persistent managed/rendering surface: `TopographyForgePreview._texture`. `OnDisable` already destroyed it, but assembly reload or editor quit paths can bypass ordinary window disable ordering and leave `HideAndDontSave` preview texture lifetime dependent on Unity cleanup.

Solution: Register `TopographyForgePreview.Shutdown` with `AssemblyReloadEvents.beforeAssemblyReload` and `EditorApplication.quitting`. The preview texture remains editor-only, hidden, and single-owned by the preview facade, but its lifetime is now explicitly terminated on the two cold editor lifecycle routes that matter.

Rejected Alternatives: Keeping only `OnDisable` was rejected because static preview state should not depend on a UI window callback during reload/quit. Moving preview pixels into a persistent native vault was rejected because this is not runtime, rollback, cross-domain, or streaming memory. Rebuilding the texture every preview was rejected because it adds avoidable editor allocation churn.

Scalability potential: Low preview still collapses to 64 px and high/ultra preview remains capped at 128 px. The lifecycle patch does not change terrain truth, payload ABI, runtime memory, or quality semantics.

Hardware Impact: Runtime impact remains 0 us/frame. Editor steady-state impact is 0 us after static event registration. Reload/quit paths release one preview `Texture2D` deterministically instead of relying on Unity orphan cleanup.

## Decision 042 - OfflineGeology Assembly Co-Tenancy Boundary

Problem: A full folder scan showed `Hecton8.World.OfflineGeology.Editor.asmdef` also contains the older `GeologyForge*` and `RuntimeMeshGenerationScanner` files. `CURRENT_BATCH.md` identifies that mesh-baker surface as `SHINOBU_208 OFFLINE_GEOLOGY_MESH_BAKER`, not `SHINOBU_240 TERRESTRIAL_HEIGHTMAP_REFORMATTER`. Those files still contain managed `List<T>` editor state and `NativeArrayOptions.ClearMemory` sites, so claiming the whole asmdef as SHINOBU_240-zero-GC would be false.

Solution: Keep SHINOBU_240 code changes confined to `TopographyForge*` plus SHINOBU_240 docs. Treat the shared asmdef as an import boundary and proof caveat: topography files are statically hardened, but Unity import can still be affected by co-tenant SHINOBU_208 files until that owner splits or hardens the assembly.

Rejected Alternatives: Refactoring or moving SHINOBU_208 files was rejected because it is outside the current domain and would create merge risk with another owner's prompt. Declaring the full `OfflineGeology` asmdef clean was rejected because the scan shows objective managed editor surfaces outside SHINOBU_240. Creating direct dependencies to isolate at runtime was rejected because this is an editor-only assembly and compile-wall preservation matters more than cosmetic namespace purity.

Scalability potential: Low/Middle/High/Ultra terrain payload truth is unchanged. The boundary note prevents integration from treating SHINOBU_208 mesh-baker debt as SHINOBU_240 terrain truth debt.

Hardware Impact: Runtime impact remains 0 us/frame. Editor compile/import risk is clearer, not faster; exact import cost remains pending Unity compiler proof.

## Decision 043 - Read Accessor Purity Naming Fence

Problem: The global doctrine reserves `Read*`, `TryGet*`, `Get*`, and `Resolve*` names for pure accessors. SHINOBU_240 still had mutating CSV cursor helpers named `ReadInt`/`ReadDouble`, file-stream validators named `TryReadHeader`/`ReadFull`, and local state snapshots named `ReadMetrics`/`ReadBlackBoxCursor`. The code behavior was static/editor-only, but the names could pollute source scanner evidence and hide cursor mutation or file IO behind a pure-accessor verb.

Solution: Rename mutating CSV helpers to `TryParseRecipe`, `ConsumeFixedStringCell`, `ParseIntCell`, `ParseUIntCell`, `ParseFloatCell`, and `ParseDoubleCell`. Rename file-stream consumers to `TryLoadHeightmapHeader`, `TryLoadBiomeMaskHeader`, and `FillBufferFromStream`. Rename pure local state copies to `SnapshotMetrics` and `SnapshotBlackBoxCursor` so they stay honest without occupying the reserved doctrine verb.

Rejected Alternatives: Leaving the names was rejected because scanner output would keep showing false `Read*` debt. Adding comments was rejected because comments do not affect automated evidence. Rewriting the parser around managed strings was rejected because it would violate the zero-GC CSV bridge.

Scalability potential: Low/Middle/High/Ultra payload truth is unchanged. The patch only tightens evidence quality for editor authoring and validation routes; runtime topology streaming remains owned by downstream systems.

Hardware Impact: Runtime impact remains 0 us/frame. Editor impact is symbol-only after compilation; no algorithm, allocation, DTO layout, file ABI, or job dependency changed.
