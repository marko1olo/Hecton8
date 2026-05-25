# Rationale_SHINOBU_241
Date: 2026-05-21
Agent: SHINOBU_241
State: PENDING VERIFICATION

## Initial Boundary
Problem: Hadal trench generation must create vertical rifts and basalt cliffs without runtime CSG cost.
Solution: Offline/editor Burst jobs emit flat `.h8bin` voxel data plus deterministic vent DTOs and reports.
Rejected Alternatives: Runtime MonoBehaviour carving, GameObject spline components, hand-modeled FBX trenches, heightmap-only fake depth.
Scalability potential: Low uses streamed compressed coarse cells; Middle keeps cliff-near fields longer; High keeps denser mesh residency; Ultra consumes the same baked truth with maximal meshing radius and visual dressing.
Hardware Impact: i3/MX350 avoids runtime trench CSG entirely; expected hot gameplay cost is loader/mesher cost already owned by streaming, not fault generation.

## Loop 1 Decisions
Problem: `Assets/_Project/Prefabs/Environment` is absent, so direct trench FBX deletion target does not exist.
Solution: Added `Manual_Trench_Scanner` with Environment-first scan and project fallback that avoids abyss/rift flora false positives; deletion exists only behind explicit Editor menu.
Rejected Alternatives: Blind deletion by substring across all prefabs; this would delete abyssal flora/support prefabs, not hand-sculpted terrain.
Scalability potential: Low/Middle/High/Ultra all avoid art bloat by proving no manual canyon mesh dependency before bake.
Hardware Impact: i3/MX350 avoids loading accidental massive FBX terrain; observed static fallback count for strict `.fbx/.prefab` trench/canyon/fault/chasm is 0.

Problem: Existing seismic gameplay path synthesized trench line payloads, terrain heightmap trench writeback, and voxel trench stamp loops at runtime.
Solution: Converted macroscopic seismic trench path to inert compatibility route: shockwave payloads no longer carry AUP line segments; `WorldGenerativeGeologyVoxelBridgeDirector` no-ops the trench handler; `RegisterSeismicTrench` and `TryApplySeismicTrench` return without terrain/voxel mutation.
Rejected Alternatives: Rewriting RandomEventSystem, VoxelDeltaProcessor, terrain seam applier, and voxel volumes into the new offline baker; too broad and violates domain ownership. Localized crater/collapse damage remains because it is gameplay state, not world-scale trench generation.
Scalability potential: Low devices avoid terrain `SetHeightsDelayLOD` and voxel line sampling spikes; Middle/High/Ultra consume immutable baked `.h8bin` trench truth and spend runtime budget on streaming/visuals.
Hardware Impact: i3/MX350 removes runtime line-sampled canyon stamping and heightmap sync from seismic events; expected saved spikes are above 0.1 ms because old path nested longitudinal/lateral samples plus terrain writeback.

Problem: Fault DTO must exactly match the 64-byte ARM64 contract.
Solution: `FaultLineParamsDTO` uses explicit offsets 0/24/48/52/56/60 and `HadalTrenchLayoutValidator` checks size/offsets with `UnsafeUtility`.
Rejected Alternatives: `StructLayout.Sequential`, properties, `Vector3`, or managed spline authoring records; these make persistent layout and Burst pointer traversal ambiguous.
Scalability potential: The same fault buffer feeds low preview, middle bake, high dense bake, and ultra visual dressing without DTO mutation.
Hardware Impact: i3/MX350 editor bake gets sequential 64-byte segment reads and avoids defensive CS1612 property copies inside voxel loops.

Problem: Need an isolated benchmark before full 100km world bake.
Solution: `GenerateMockTrenchJob` fills a solid NativeArray and subtracts a twisting fault at configurable 256^3 scale using AUP math.
Rejected Alternatives: Waiting for Agent 240 heightmap or runtime voxel engine integration; direct dependency would block the task and create runtime coupling.
Scalability potential: Low uses smaller resolution; Middle/High/Ultra increase voxel resolution and quality weight continuously.
Hardware Impact: i3/MX350 can validate math offline with a bounded chunk instead of full-map generation.

## Loop 2 Decisions
Problem: Voronoi tectonic graph must be deterministic and not object-spline based.
Solution: `GenerateTectonicNetworkJob` emits two Voronoi edge segments per macro cell into `NativeArray<FaultLineParamsDTO>` using deterministic hash jitter.
Rejected Alternatives: Unity spline components, managed graph objects, or scene GameObjects; all introduce Editor scene coupling and allocations.
Scalability potential: Cell size and grid count scale continuously from coarse survival to ultra-dense overkill.
Hardware Impact: i3/MX350 gets O(cells) offline fault generation and no gameplay cost.

Problem: Smooth V/U cuts look fake but secondary geometry passes are too expensive.
Solution: Ridged multifractal noise is evaluated inside `ExecuteTrenchSubtractionJob` using absolute sample AUP, then folded into lateral SDF distance.
Rejected Alternatives: Simulating erosion/rockfall particles or post-mesh displacement; too slow and less deterministic.
Scalability potential: Low lowers noise influence and adaptive density; Middle keeps readable basalt variation; High/Ultra uses more aggressive wall roughness.
Hardware Impact: i3/MX350 spends offline math only; runtime mesher receives already-rough SDF.

Problem: Thermal vents must be provided without spawning runtime objects.
Solution: `GenerateThermalVentNodesJob` writes 64-byte `ThermalVentSpawnDTO` records into the `.h8bin` secondary payload.
Rejected Alternatives: Registering directly into `VolcanicUpdraftVault`; BufferID ownership is separate and runtime hydration belongs to thermal/resource systems.
Scalability potential: Low streams fewer visual vent effects; High/Ultra can dress the same deterministic anchors.
Hardware Impact: i3/MX350 avoids discovery scans at runtime.

Problem: LZ4 dictionary bindings are not available to the new asmdef and existing save LZ4 is internal.
Solution: Implemented RLE first and a small LZ4 block encoder attempt for the RLE byte stream; if LZ4 expands, file header records RLE mode and warning flags.
Rejected Alternatives: Calling internal `SaveBinaryStorage` LZ4 or pretending dictionary LZ4 exists; both break compile or honesty.
Scalability potential: Low benefits from RLE uniform regions; High/Ultra still stream identical immutable payloads with richer meshing.
Hardware Impact: i3/MX350 reduces disk/memory bandwidth for solid/void runs; no runtime compression work is added.

## Loop 3 Decisions
Problem: 1m voxels everywhere across 100km is impossible.
Solution: `BuildTrenchAdaptiveBlocksJob` emits 32-byte uniform/error block summaries; block size is driven by continuous `GlobalQualityWeight`.
Rejected Alternatives: Binary low/high tiers or storing full dense volume globally.
Scalability potential: Low uses large collapsed blocks; Middle/High/Ultra tighten blocks near cliff detail.
Hardware Impact: i3/MX350 avoids reading uniform rock/water at full density.

Problem: Sector seams must not drift from float precision.
Solution: All sample positions are `SectorOriginAUP + voxel * voxelSize` in double, and only local deltas are cast to float inside SDF/noise.
Rejected Alternatives: Local sector float coordinates as noise seed; this would snap at extreme AUP edges.
Scalability potential: Same math works from weak devices to ultra meshing because truth ownership is coordinate-stable.
Hardware Impact: i3/MX350 avoids seam repair passes.

Problem: Static terrain payload must not enter rollback Merkle state.
Solution: `.h8bin` header flags and `HadalTrenchRollbackExclusionDTO` mark payload as rollback-excluded; no netcode descriptor is added.
Rejected Alternatives: Hashing gigabytes of voxel terrain or adding a hot DataVault route.
Scalability potential: Network state remains entity-only across all quality levels.
Hardware Impact: i3/MX350 avoids catastrophic network hash bandwidth.

## Loop 4 Decisions
Problem: Designers need control without a scene controller or runtime component.
Solution: `HadalTrenchForgeWindow` uses UI Toolkit controls for cell size, trench width, depth, noise, quality, preview, scan, and bake scheduling.
Rejected Alternatives: MonoBehaviour scene controller, inspector-only script, or GameObject spline editor; all violate offline authority or slow iteration.
Scalability potential: Low/Middle/High/Ultra are driven through numeric sliders and profile CSV, not binary switches.
Hardware Impact: i3/MX350 gets preview graph only before committing to dense voxel bake.

Problem: `tectonic_rift_profiles.csv` must not allocate through split/LINQ parsing.
Solution: `TectonicRiftProfileCsvParser` reads a NativeArray byte buffer from FileStream and parses fixed schema fields directly.
Rejected Alternatives: `File.ReadAllText`, `string.Split`, LINQ, or managed per-cell strings; they create avoidable editor GC during profile reload.
Scalability potential: Low profile can use shallow crevice, Middle default Mariana, High/Ultra basalt overkill without DTO layout change.
Hardware Impact: i3/MX350 avoids allocation spikes when designers iterate profiles.

Problem: Preview must be instant and not bake gigabytes.
Solution: `HadalTrenchPreviewStore` runs Voronoi and vent jobs only, then `HadalTrenchPreviewGizmo.OnDrawGizmos` draws red lines and blue spheres.
Rejected Alternatives: Full voxel generation for each slider change or mesh extraction preview.
Scalability potential: Low clamps preview fault count; Middle/High/Ultra can increase grid continuously.
Hardware Impact: i3/MX350 avoids dense SDF preview allocation.

Problem: Report artifacts must be on disk.
Solution: Added `WORLD_OPTIMIZATION_REPORT.json`, `SHINOBU_241_SELF_AUDIT.xml`, status, rationale, and final log path.
Rejected Alternatives: Chat-only assertion; CTO protocol rejects that.
Scalability potential: Review cost stays stable as bake size grows.
Hardware Impact: i3/MX350 not affected at runtime.

## Verification Blocker
Problem: Compile verification was required but CPU load was over the documented threshold.
Solution: Checked CPU and dotnet/csc twice; CPU returned 100%, no dotnet/csc processes were running, so dotnet build was not launched.
Rejected Alternatives: Violating the batch CPU rule or starting a competing build while machine is saturated.
Scalability potential: Build verification can run once load drops below 50%.
Hardware Impact: Avoided adding compile pressure to an already saturated workstation.

## Ultra Polish Decisions
Problem: Sub-agent payload audit proved the trench `.h8bin` is not a Data Monolith payload and would be rejected by `H8StaticDataArena`.
Solution: Added `Docs/ARCHITECTURE/HADAL_TRENCH_PAYLOAD_ROUTE_CARD.md`, appended the Binary Payload Integration Ledger, and labeled the route as a separate StreamingAssets payload pending runtime consumer proof.
Rejected Alternatives: Claiming `static_data.h8bin` readiness or adding a half-built `H8DataSectionId` without boot/import validation.
Scalability potential: Low/Middle/High/Ultra runtime streams the same immutable payload once a consumer exists; monolith integration can be planned without changing trench DTO layout.
Hardware Impact: i3/MX350 avoids a boot-time invalid payload parse and gets deterministic failure evidence instead of silent load corruption.

Problem: Header proof was too weak; BinaryWriter little-endian behavior was implicit and the old self-audit still reported 128 bytes.
Solution: Expanded `HadalTrenchChunkHeaderDTO` to 160 bytes with explicit endian marker, schema hash, uncompressed bytes, density prelude bytes, total file bytes, section alignment, checksum type, and padding. Updated layout validator and self-audit.
Rejected Alternatives: Keeping `_pad0` as unused bytes or documenting endian behavior only in prose.
Scalability potential: The same header supports small survival chunks and ultra-dense sector chunks with exact byte ranges.
Hardware Impact: i3/MX350 loader can reject corrupt bytes before allocating/decompressing sector data.

Problem: Payload correctness needed a proof artifact beyond report text.
Solution: Added `HadalTrenchPayloadValidator` to verify file existence, magic/version, endian, schema, offset alignment, byte counts, rollback flag, and FNV-1a payload hash after async write.
Rejected Alternatives: Runtime discovery failure or chat-only checksum assertions.
Scalability potential: Validation cost is editor/bake-only and scales linearly with compressed payload bytes.
Hardware Impact: Low-end devices avoid runtime validation cost; editor machines catch invalid payloads before shipping.

Problem: Preview gizmo cast absolute AUP doubles directly to Unity `Vector3`, and preview scheduling completed in the same method.
Solution: Preview now stores `PreviewOriginAUP`, localizes every fault/vent before float conversion, and pumps the scheduled job chain through `EditorApplication.update` before marking `HasPreview`.
Rejected Alternatives: Absolute float Scene View positions or full voxel bakes per slider change.
Scalability potential: Low keeps a clamped fault preview; Middle/High/Ultra can raise fault counts continuously without changing AUP math.
Hardware Impact: i3/MX350 avoids gigabyte preview bakes and avoids precision loss at 100km world edges.

Problem: Runtime carve fence still compiled suppressed unreachable bodies and obsolete bridge trench/debris helpers.
Solution: Removed dead macroscopic trench body code from terrain seam and voxel volume compatibility routes, removed the obsolete bridge private trench/debris helper path, and left explicit inert parameter consumption.
Rejected Alternatives: Leaving `#pragma warning disable CS0162` blocks or dormant private trench helpers that hide future compile errors.
Scalability potential: Static trench truth remains offline across all hardware tiers.
Hardware Impact: i3/MX350 avoids accidental terrain heightmap and voxel line-stamp spikes if seismic routes are touched later.

Problem: Original mock benchmark existed as a job but not as a direct TempJob benchmark facade.
Solution: Added `HadalTrenchMockBenchmark` menu path allocating a 256^3 `NativeArray<float>` with `Allocator.TempJob` and `UninitializedMemory`, then writing `TRENCH_MOCK_BENCHMARK_SHINOBU_241.json`.
Rejected Alternatives: Running the full async bake to validate the basic SDF subtract math.
Scalability potential: Designers can lower/raise benchmark resolution within the bounded path while preserving the same algorithm.
Hardware Impact: i3/MX350 can stress the carve kernel in isolation before committing to heavy sector output.

## Loop 7 Decisions
Problem: The bake pipeline used `async Task`-style serialization and then cloned the full sector payload through `MemoryStream.ToArray()`.
Solution: Replaced it with an explicit chunked `AsyncPayloadWriteSession` based on `FileStream.BeginWrite/EndWrite`, removed the unused retained payload field, and writes header/prelude/density/vent/adaptive buffers sequentially without a second whole-file managed clone.
Rejected Alternatives: Keeping compiler-generated Tasks, blocking `File.WriteAllBytes`, cloning the full `.h8bin` before disk write, or pretending editor-only async state machines are irrelevant.
Scalability potential: Low devices and saturated workstations keep editor responsiveness during sector writes; High/Ultra can push larger payloads through the same route without changing runtime truth ownership.
Hardware Impact: i3/MX350 avoids avoidable managed async allocation and warning-as-error churn; disk write remains editor-only.

Problem: Payload validation cloned the whole `.h8bin` with `File.ReadAllBytes`, which becomes pathological once sector chunks grow.
Solution: Validator now reads only the 160-byte header, checks offsets/sizes/alignment, and streams FNV-1a payload ranges through a fixed 128 KiB buffer.
Rejected Alternatives: Full managed payload clone or runtime validation at boot.
Scalability potential: Low/Middle editor machines validate huge chunks with bounded memory; High/Ultra payloads scale linearly in IO only.
Hardware Impact: i3/MX350 avoids large managed allocation spikes and GC pressure during validation.

Problem: The preview gizmo existed only as a `MonoBehaviour` entrypoint, so a designer would see nothing unless a scene object was manually added.
Solution: Added `SceneView.duringSceneGui` overlay that draws localized fault lines and vent anchors directly from editor scratch buffers; `OnDrawGizmos` remains a compatible manual draw path but is not required.
Rejected Alternatives: Injecting a GameObject/controller into scenes, generating preview meshes, or requiring a full voxel bake per slider edit.
Scalability potential: Low preview keeps clamped fault counts; Middle/High/Ultra can increase preview density continuously while staying editor-only.
Hardware Impact: i3/MX350 avoids scene pollution and milliseconds-to-seconds preview bake stalls.

Problem: Compile-wall proof needed a fresh direct-reference scan after async/preview changes.
Solution: Rechecked asmdefs and `using Hecton8` references; runtime contract asmdef references only `Unity.Mathematics`, editor asmdef references own contract plus Unity Burst/Collections/Jobs/Mathematics.
Rejected Alternatives: Direct calls into voxel/terrain siblings or adding a runtime boot consumer without route-card approval.
Scalability potential: Domain remains isolated while future runtime streaming can consume `.h8bin` through a documented route.
Hardware Impact: i3/MX350 not directly affected at runtime; developer workstation avoids sibling-domain recompile cascades.

## Loop 8 Decisions
Problem: The prompt demanded TempJob for massive bake arrays, but the actual forge is a multi-frame `EditorApplication.update` pipeline. `Allocator.TempJob` across that lifetime is invalid Unity allocator usage.
Solution: Reverted the multi-frame bake session scratch arrays/lists to `Allocator.Persistent` with `NativeArrayOptions.UninitializedMemory`, kept deterministic owner disposal on completion/failure/cancel/reload/quit, and kept the bounded 256^3 mock benchmark on `Allocator.TempJob`.
Rejected Alternatives: Keeping TempJob purely to satisfy wording while causing JobTempAlloc lifetime faults, or collapsing the pipeline into a blocking same-frame bake that defeats the responsive editor requirement.
Scalability potential: Low/Middle workstations keep safe editor-owned scratch for longer bakes; High/Ultra can run denser bakes without allocator lifetime warnings.
Hardware Impact: i3/MX350 avoids allocator diagnostic storms and partial job memory faults during slow sector bakes.

Problem: Direct `FileMode.Create` on the final `.h8bin` path could leave a truncated active payload if cancellation or editor shutdown occurred mid-write.
Solution: Async serialization now writes `hadal_trench_sector_0000.h8bin.tmp`, closes it, validates header/offset/hash on the temp file, then replaces or moves it into the final path. Uncommitted temp files are deleted on dispose; invalid temp files are preserved as `.tmp.invalid` for forensics.
Rejected Alternatives: Writing final path directly, relying on validator after corrupting the active file, or blocking the editor with synchronous write-all.
Scalability potential: Low/Middle machines can cancel without destroying the last valid payload; High/Ultra larger payloads keep the same atomic lifecycle.
Hardware Impact: i3/MX350 avoids broken runtime payload boot after interrupted editor writes.

Problem: Placing a separate payload under `StreamingAssets/Hecton8/DataMonolith/HadalTrenches` creates a false Data Monolith route signal.
Solution: Moved code and validator defaults to `Assets/StreamingAssets/Hecton8/HadalTrenches/hadal_trench_sector_0000.h8bin` and updated the route card, ledger, and pending report.
Rejected Alternatives: Keeping a non-monolith file inside the monolith subtree while documenting that it is not monolith-ready.
Scalability potential: Future runtime consumer can explicitly opt into the sidecar route without touching `H8StaticDataArena` section rules.
Hardware Impact: Low-end devices avoid boot probing the wrong arena path.

Problem: SceneView preview drew each fault with the `params Vector3[]` overload and dropped new preview configs when a previous preview job was pending.
Solution: Added a static two-point line scratch array for `Handles.DrawAAPolyLine` and queued the latest config while a preview job is active.
Rejected Alternatives: Allocating one managed array per fault line per repaint or silently ignoring designer slider changes.
Scalability potential: Low preview remains cheap; Middle/High/Ultra can raise preview density without avoidable SceneView GC.
Hardware Impact: i3/MX350 avoids editor repaint allocation spikes and stale previews.

Problem: `FileStream.Read(Span<byte>)` in the CSV parser risks Unity API-profile compile breaks.
Solution: Replaced it with byte-level reads into the NativeArray pointer; the file is capped at 4 MiB and this path is cold editor tooling.
Rejected Alternatives: Depending on .NET Standard 2.1 overloads or allocating managed CSV strings.
Scalability potential: All hardware tiers preserve the same byte parser behavior.
Hardware Impact: i3/MX350 avoids compile-profile churn; CSV load cost remains editor-only and bounded.

## Loop 9 Decisions
Problem: The static scanner still reports `.Complete()` in the SHINOBU_241 code slice.
Solution: Reconciled each hit by phase: bake `Update()` completes only after `JobHandle.IsCompleted`, cancel/dispose complete only to release editor-owned native memory, preview pump completes only after `IsCompleted`, and `HadalTrenchMockBenchmark` is an explicit manual blocking stress menu. No runtime gameplay path, Tick/Update MonoBehaviour, or hidden same-frame schedule/readback was added.
Rejected Alternatives: Removing disposal fences to satisfy a grep rule, or moving editor scratch into runtime dispatcher ownership before a boot consumer exists. Both would create leaks or cross-domain coupling.
Scalability potential: Low/Middle editor machines keep responsive multi-frame bake stages; High/Ultra can still run dense bakes without corrupting native memory on cancel/reload.
Hardware Impact: i3/MX350 avoids editor leak/fault triage while runtime trench CSG remains 0 us.

Problem: A literal prompt extraction regex failed because the batch tag contains additional attributes after `id`.
Solution: Re-ran extraction with an attribute-aware regex and verified `Task \d{2}:` count = 20.
Rejected Alternatives: Trusting chat memory or relying on exact tag text.
Scalability potential: Documentation truth stays stable across compression and future batch edits.
Hardware Impact: No runtime cost; prevents wrong-domain implementation churn.

Problem: Compile verification is still blocked by the workstation CPU gate.
Solution: Sampled CPU and compiler processes again: CPU returned 100%, `dotnet`/`csc` were absent, so build/rebuild remains forbidden by project protocol.
Rejected Alternatives: Launching a build while the machine is saturated or claiming compile proof from static scans.
Scalability potential: The compile gate can run when CPU drops below 50%; static-only artifacts remain clearly labeled.
Hardware Impact: Avoided additional IO/CPU pressure on an already saturated workstation.

## Loop 10 Decisions
Problem: The `.h8bin` header and validator required 8-byte section alignment, but the writer originally placed the vent section immediately after the compressed density payload. Any compressed density length not divisible by 8 would make `VentPayloadOffset` unaligned and cause the validator to reject a payload that the writer had just produced.
Solution: Insert explicit zero padding between density and vent sections and between vent and adaptive sections. Header offsets now use `AlignUp(end, 8)`, and the validator computes expected offsets with the same rule.
Rejected Alternatives: Removing validator alignment checks, lying in the header, or hashing padding as meaningful terrain data. Those options either break ARM64 read discipline or make payload identity depend on filler bytes.
Scalability potential: Low/Middle/High/Ultra sector sizes can vary compression lengths without invalidating the sidecar format; runtime readers get aligned DTO payload starts.
Hardware Impact: i3/MX350 avoids misaligned sequential reads and avoids rebaking/triaging payloads rejected by the validator.

Problem: Padding must not change content identity.
Solution: The FNV-1a hash remains over density payload, vent DTO bytes, and adaptive block bytes only; inter-section padding is skipped by both writer hash and validator range hash.
Rejected Alternatives: Including padding in identity or leaving hash ranges ambiguous.
Scalability potential: Payload identity stays stable even when padding length changes with compression ratio.
Hardware Impact: No runtime cost; prevents false cache invalidation on low-end streaming consumers.

## Loop 11 Decisions
Problem: Adaptive blocks were generated with a continuous integer block size from `round(lerp(16,4,GlobalQualityWeight))`, but `HadalTrenchAdaptiveBlockDTO` stored only `Log2Size`. Non-power-of-two sizes such as 10 would be serialized as 3, causing a future loader to reconstruct an 8-voxel edge and misread the adaptive section.
Solution: Replace the offset-12 field with `BlockSizeVoxels`, preserving the exact byte offset and 32-byte DTO size while storing the actual block edge length. The bake report now exposes `adaptiveBlockSizeVoxels`.
Rejected Alternatives: Quantizing all block sizes to powers of two, weakening the continuous quality curve, or leaving a reader-critical mismatch hidden behind the schema hash.
Scalability potential: Low can still collapse toward larger blocks, Middle keeps moderate blocks, High/Ultra preserve denser near-cliff data; the payload now records the exact chosen size.
Hardware Impact: i3/MX350 avoids incorrect block expansion and seam/debug work during future runtime hydration.

Problem: Changing adaptive DTO semantics under the existing schema hash would make stale readers indistinguishable from current readers.
Solution: Bump `PayloadSchemaHash` from `0xA2410001` to `0xA2410002` and update the route card, binary ledger, and static self-audit.
Rejected Alternatives: Reusing the old schema id or relying on comments to signal a binary format change.
Scalability potential: All quality weights and sector sizes share one explicit payload version.
Hardware Impact: Low-end boot consumers can fail closed on schema mismatch before allocating/decompressing terrain data.

## Loop 12 Decisions
Problem: The payload contains an 8-byte density prelude duplicating uncompressed and compressed byte counts, but validation trusted only the header. A damaged prelude could pass offset/hash validation while a future loader that reads the prelude would allocate or decompress with different sizes.
Solution: Add `HadalTrenchPayloadValidationFlags.PreludeMismatch`; validator now seeks to `HeaderBytes`, reads the prelude, and compares both counts to the header before payload hash validation.
Rejected Alternatives: Removing the prelude, trusting only the header, or waiting for the runtime consumer to discover the inconsistency.
Scalability potential: Low/Middle/High/Ultra payload sizes can vary without creating two conflicting byte-count authorities.
Hardware Impact: i3/MX350 can reject corrupt terrain bytes before expensive allocation/decompression.

## Loop 13 Decisions
Problem: The Forge loaded CSV tuning values into visible UI fields but dropped profile-only identity fields (`Seed`, `SectorOriginAUP`). A designer selecting a profile could unknowingly bake every profile with the default seed and origin.
Solution: Store the active `TectonicRiftProfileDTO` in the window and apply it during `BuildConfig()` before applying live UI overrides for exposed tuning values.
Rejected Alternatives: Adding more UI controls before compile proof, or accepting profile ingestion that only copies float slider values.
Scalability potential: Low/Middle/High/Ultra profiles now keep deterministic seed/origin identity while still using continuous UI sliders for quality and geometry intensity.
Hardware Impact: i3/MX350 avoids rebaking/streaming a sector generated from the wrong origin or fault seed.

## Loop 14 Decisions
Problem: After changing the adaptive DTO field from `Log2Size` to `BlockSizeVoxels`, the layout validator still checked only the struct size. A field could drift while the 32-byte total size stayed unchanged.
Solution: Add explicit offset checks for all `HadalTrenchAdaptiveBlockDTO` fields, including `BlockSizeVoxels` at offset 12 and `_pad0` at offset 28.
Rejected Alternatives: Relying on self-audit prose or `UnsafeUtility.SizeOf<T>()` alone.
Scalability potential: All quality-driven adaptive block sizes preserve a stable binary row layout.
Hardware Impact: i3/MX350 avoids misaligned adaptive row reads in the future runtime consumer.

## Loop 15 Decisions
Problem: After adding `PreludeMismatch`, `WriteReport()` still emitted `densityPreludeValidated: true` unconditionally. A failed validation report could therefore contain a false success field.
Solution: Compute `densityPreludeValidated` from `PayloadValidationFlags & PreludeMismatch`.
Rejected Alternatives: Leaving validation truth only in a bitmask or treating the report as success-only.
Scalability potential: All payload sizes report prelude validity without changing binary truth.
Hardware Impact: Low-end consumers and integrators get a fail-closed artifact instead of chasing contradictory report fields.

## Loop 16 Decisions
Problem: `TectonicRiftProfileDTO` is stored in a `NativeList` but used default sequential layout. Even though it is editor-only, it is still an unmanaged native row crossing the CSV authoring bridge.
Solution: Make `TectonicRiftProfileDTO` explicit 128 bytes: `SectorOriginAUP` at 0, `FixedString64Bytes Name` at 24, scalar fields at 88-112, explicit padding at 116/120. Add layout validator and self-audit offsets.
Rejected Alternatives: Treating editor-only NativeList data as exempt from ARM64 layout discipline or relying on field order.
Scalability potential: Profile rows remain stable as designers add Low/Middle/High/Ultra recipe variants.
Hardware Impact: i3/MX350 avoids future native row misalignment if the profile bridge is reused by a worker import/bake path.

## Loop 17 Decisions
Problem: The 300-entry bake telemetry ring was allocated with `NativeArrayOptions.UninitializedMemory`, but only a few stage rows were written. An early failure could dump stale native bytes, and stage IDs as indices were not a real circular buffer.
Solution: Initialize all 300 entries immediately after allocation with deterministic baseline telemetry, add a `_telemetryCursor`, and write later stage snapshots to `cursor % 300`.
Rejected Alternatives: Calling `UnsafeUtility.MemClear`, writing only `_telemetry[0]`, or treating stage IDs as ring indices. MemClear wastes the exact zero-init saving; one-row/stage-slot dumps do not satisfy forensic retention.
Scalability potential: Low/Middle/High/Ultra bake sizes keep the same fixed 64-byte x 300 telemetry cost while dense ultra bakes produce ordered stage snapshots for postmortem.
Hardware Impact: i3/MX350 runtime cost remains 0 us because this is editor bake memory only; crash forensics avoid unknown/stale rows.

## Loop 18 Decisions
Problem: `AsyncPayloadWriteSession.Dispose()` could be reached during cancel, editor quit, or domain reload while a `BeginWrite` operation was still pending. Closing the stream or deleting the temp file could throw inside cleanup.
Solution: `WaitAndDispose()` now records a timeout if the write does not finish within the disposal window, and stream/file cleanup catches exceptions into the session `Exception` field instead of throwing from the callback.
Rejected Alternatives: Blocking indefinitely, leaving temp files unmanaged, or swallowing cleanup failures without evidence.
Scalability potential: Low/Middle saturated workstations can cancel or reload during long writes without destroying the editor callback chain; High/Ultra large payloads retain the same atomic temp/replace route.
Hardware Impact: i3/MX350 runtime cost remains 0 us; editor-side failure handling avoids corrupted final payloads and preserves diagnostics.

## Loop 19 Decisions
Problem: CSV/UI values could still carry NaN or extreme finite floats into noise lattice math. `ValueNoise3` casts floored double lattice coordinates to int, so a bad profile could corrupt the bake before the non-finite density flag caught it.
Solution: Add explicit finite fallback clamps for bake and preview float parameters, cap noise frequency/intensity, clamp sector origin AUP during bake sanitize, and fail-fast CSV sector origins outside +/-100000m.
Rejected Alternatives: Relying on `math.clamp` alone, because NaN can propagate through clamp-like math; allowing impossible AUP values and hoping report validation catches downstream damage.
Scalability potential: Low/Middle/High/Ultra profiles stay within one stable numeric envelope while quality weight continues to drive continuous detail, not truth ownership.
Hardware Impact: i3/MX350 runtime cost remains 0 us; editor bake avoids wasted minutes on mathematically invalid profile data.

## Loop 20 Decisions
Problem: The SceneView preview store exposed `NativeArray<FaultLineParamsDTO>` and `NativeArray<ThermalVentSpawnDTO>` as public static mutable fields. Even though this is editor-only, it created an unnecessary write surface and weakened the "read accessors are pure" doctrine.
Solution: Make `HadalTrenchPreviewStore` internal to the editor assembly, keep preview native arrays/private counts/private origin fields hidden, and expose pure `TryReadPreview` / `TryGetCounts` accessors that allocate nothing, publish nothing, and do not complete jobs.
Rejected Alternatives: Leaving public mutable native arrays because the current drawer is trusted, or creating managed snapshots for read safety. Public arrays invite accidental mutation; managed snapshots would add allocation and stale-state risk.
Scalability potential: Low preview stays cheap and bounded; Middle/High/Ultra can raise preview density without opening preview cache ownership to other editor tools.
Hardware Impact: i3/MX350 runtime cost remains 0 us; editor preview avoids future corruption/debug churn while keeping the same native buffers.

Problem: Bake success/failure callbacks were invoked directly inside the async payload lifecycle. A UI callback exception could bubble through a successful payload write path and create a false failure or leave misleading temp/native state.
Solution: Wrap completion and failure callbacks with exception guards that log through `UnityEngine.Debug.LogException` while keeping writer commit/disposal state unchanged.
Rejected Alternatives: Letting EditorWindow label/status code throw through serialization, or swallowing callback exceptions silently. Both produce bad forensic evidence.
Scalability potential: Larger High/Ultra payload bakes preserve deterministic writer lifecycle even if editor presentation callbacks fail.
Hardware Impact: i3/MX350 runtime cost remains 0 us; saturated editor machines avoid broken temp-file forensics after callback faults.

## Loop 21 Decisions
Problem: The allocation-free CSV bridge still used `profiles.Add(...)`. `NativeList.Add` may grow capacity internally when designers add more profiles, hiding an allocation/copy event behind an authoring read path.
Solution: Add a 256-profile hard cap, explicitly raise `NativeList<TectonicRiftProfileDTO>.Capacity` when required, and insert through `AddNoResize` only after capacity is proven.
Rejected Alternatives: Leaving implicit growth because the parser is editor-only, or using a managed list before copying into native memory. Both weaken the zero-GC authoring bridge and make profile import failures harder to reproduce.
Scalability potential: Low/Middle/High/Ultra profile recipes remain bounded and deterministic; high-end visual-overkill profiles can be added without converting the parser into an unbounded heap path.
Hardware Impact: i3/MX350 runtime cost remains 0 us; editor import avoids surprise native buffer copy spikes and keeps failure at a clear row-count gate.

Problem: CSV parse diagnostics started `seed` at column 1 even though column 1 is `name`. Invalid profile evidence therefore pointed at the wrong schema column.
Solution: Start numeric parse diagnostics at column 2 immediately after consuming the name token, preserving 1-based CSV schema reporting.
Rejected Alternatives: Keeping row-only error reporting or relying on field names alone. Designers need exact row/column evidence for a hot-reloadable tuning file.
Scalability potential: More region profiles can be tuned across the quality curve without ambiguous authoring errors.
Hardware Impact: i3/MX350 runtime cost remains 0 us; editor/debug time is reduced by eliminating off-by-one import diagnostics.

## Loop 22 Decisions
Problem: Task 10 proof exposed `rleRuns` and validation flags but did not carry the actual compression mode, density byte counts, or payload hash through `HadalTrenchBakeResult` into the JSON report and self-audit.
Solution: Add compression mode, uncompressed density bytes, compressed density bytes, and FNV payload hash to the bake result immediately after payload construction, then write those values into both `TRENCH_BAKE_REPORT.json` and `SHINOBU_241_SELF_AUDIT.xml`.
Rejected Alternatives: Inferring compression evidence from warning flags, or requiring designers to inspect binary headers manually. Both slow payload triage and weaken proof that RLE/LZ4 fallback selected the intended representation.
Scalability potential: Low/Middle/High/Ultra payloads can be compared by actual byte ratio and mode without changing immutable terrain truth.
Hardware Impact: i3/MX350 runtime cost remains 0 us; editor and loader debugging avoid rereading large payloads just to prove compression identity.

## Loop 23 Decisions
Problem: `ExecuteTrenchSubtractionJob` evaluated four-octave ridged multifractal noise for every voxel/fault pair, including far faults that could not affect the current density after `math.max(result, -voidSdf)`.
Solution: Add `EvaluateTrenchOutsideLowerBound`, a cheap SDF lower-bound pass that includes vertical bounds, width shaping, and the maximum possible roughness/pulse displacement. The carve loop skips the expensive Dear-Lie noise only when this lower bound is greater than `-result`, proving that the exact result would not change.
Rejected Alternatives: Distance-only culling, quality-threshold culling, or reducing octave count globally. Distance-only culling can erase protrusions; quality-threshold culling is a binary fidelity switch; global octave reduction weakens high-end visual overkill.
Scalability potential: Low/Middle avoid most far-fault noise work; High/Ultra keep full ridged noise where the fault can actually touch the voxel. The quality curve remains continuous because this is spatial proof, not hardware class branching.
Hardware Impact: i3/MX350 saves the dominant offline ALU path on sparse influence regions; runtime cost remains 0 us because the baked payload is still immutable.

## Loop 24 Decisions
Problem: The LZ4 block compression attempt allocated a 65,536-entry managed `int[]` match table every time a payload was built. That is editor-only, but it is still avoidable GC pressure in the largest authoring path.
Solution: Replace the managed table with a scoped `NativeArray<int>` using `Allocator.Temp` and `NativeArrayOptions.UninitializedMemory`; initialize it manually and dispose in `finally` so all early fallback returns release native memory.
Rejected Alternatives: Leaving the managed array because compression is not gameplay, or making the table persistent static editor state. Managed allocation violates the authoring bridge discipline; static ownership creates stale state and reload cleanup risk.
Scalability potential: Low/Middle/High/Ultra payload compression keeps the same binary output semantics while removing one large managed allocation per bake.
Hardware Impact: i3/MX350 runtime cost remains 0 us; editor bakes avoid a 256KB managed table allocation plus GC accounting on every compression attempt.

Problem: The current self-audit XML evidence used raw `NativeArray<int>` inside an attribute after documenting the change.
Solution: Escape the attribute as `NativeArray&lt;int&gt;` and validate the XML parses.
Rejected Alternatives: Leaving a visually readable but invalid XML proof artifact.
Scalability potential: Tooling can ingest the same audit for all quality tiers.
Hardware Impact: No runtime impact; prevents audit pipeline failure.
