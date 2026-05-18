# Rationale_SHINOBU_04

Date: 2026-05-17
Agent: SHINOBU_04
Status: IMPLEMENTED / PENDING VERIFICATION / COMPILE BLOCKED BY EXISTING NON-SHINOBU DEPENDENCIES

## Decision 001: Authority Boundary

Problem: The sampler must weld MapMagic 2D height data and voxel SDF data without relying on scene colliders or Unity terrain APIs that allocate or require GameObjects.
Solution: Treat the sampler as a stateless Burst math kernel that receives unmanaged data views/pointers from the owning world data layer and converts `double3` AUP query positions to chunk-local `float3` before SIMD sampling.
Rejected Alternatives: Unity `Physics.Raycast`, `Terrain.SampleHeight`, `Terrain.GetHeights`, and MeshCollider fallback were rejected because they are not O(1), not Burst-callable, and violate the no-collider query directive.
Scalability potential: Low uses height-only/nearest-neighbor SDF and cheap normals; Middle uses trilinear SDF; High uses smooth-min seam and 4-tap normals; Ultra spends saved cycles on denser editor/debug visualization and higher cave detail around the player.
Hardware Impact: Expected low-end i3/MX350 gain is avoidance of BVH traversal and GameObject sync. Budget target is under 0.1 ms for the sampler batch path; measured proof is still absent.

## Decision 002: AUP Float-Jitter Guard

Problem: Raw float world positions lose centimeter/millimeter precision at 100 km and cause terrain/cave contact jitter.
Solution: Keep query authority in `double3`, subtract the active chunk origin in double precision, then cast only the small local delta to `float3` for Burst math.
Rejected Alternatives: Passing raw `float3 worldPosition` or reconstructing from `Transform.position` was rejected because float mantissa precision collapses at large distances.
Scalability potential: Low through Ultra share the same authority conversion; only downstream sample quality changes.
Hardware Impact: Double subtract happens once per query, after which all heavy math remains float SIMD. Estimated overhead is lower than collider/BVH instability correction.

## Decision 003: Missing OSHINO Binary Fallback

Problem: `Docs/Archive` does not contain the expected `mapmagic_heights_*.h8bin` or `sdf_base_noise_*.bin` files, so there is no authoritative legacy payload to decode.
Solution: Implement `MockTerrainGenerator(ref GlobalWorldSamplerData, sineFrequency, caveRadius, caveDepth)` that writes deterministic sine height samples, material bytes, all-cave sector masks, and a spherical encoded SDF into preallocated NativeArrays.
Rejected Alternatives: Blocking on absent artifacts, adding ad hoc binary readers for files that are not present, or storing mock state in managed arrays.
Scalability potential: Low uses the mock only for editor probing; Middle/High/Ultra replace the NativeArray aliases with DataVault/streaming payloads without changing sampler math.
Hardware Impact: Hot query path gains 0 allocations. Mock generation is cold/editor-side; low-end i3/MX350 pays no runtime cost unless a test tool explicitly rebuilds the data.

## Decision 004: DTO Layout And ARM64 Padding

Problem: Terrain hits and black-box entries must stay blittable and predictable across ARM64 without relying on byte packing that can punish memory access.
Solution: Use `[StructLayout(LayoutKind.Sequential, Size = 64)]` for `TerrainSampleResult` and `GlobalWorldSamplerTelemetryEntry`, with explicit spare fields and no `Pack = 1`.
Rejected Alternatives: `class` DTOs, nullable fields, managed strings, and `Pack = 1` were rejected because they either allocate or cause unsafe/alignment-sensitive reads.
Scalability potential: Low can stream compact 64-byte samples; Ultra can store richer telemetry in the same cache-line-sized payload without changing public ABI.
Hardware Impact: Fixed 64-byte rows are cache-predictable. Expected low-end gain is reduced false layout drift and cheaper telemetry copies.

## Decision 005: Seam Composition

Problem: MapMagic height distance and voxel SDF distance must meet without visible seam pops, while still letting cavern SDF dominate under terrain.
Solution: Compute terrain distance and trilinear/nearest SDF distance independently, use polynomial smooth-min for normal operation, and use direct SDF return when `sdfDistance < 0` below terrain.
Rejected Alternatives: Mesh boolean stitching, collider overlap queries, marching-cubes seam rebuild per query, and multi-octave physical erosion were rejected as too slow and too stateful.
Scalability potential: Low uses nearest SDF and can disable smooth-min; Middle uses trilinear; High enables smooth-min and tetra normals; Ultra spends saved cost on denser cave data.
Hardware Impact: On i3/MX350 the sector mask bypass prevents SDF sampling for empty areas. Top-tier devices can keep trilinear SDF and normals on dense batches.

## Decision 006: Throughput Black Box

Problem: A hidden sampler spike or NaN cannot be debugged after the frame without a fixed-size local history.
Solution: Use a caller-owned `NativeArray<GlobalWorldSamplerTelemetryEntry>` ring, atomically increment `SampleCounter` via `Interlocked.Add`, emit warnings above 500k samples, request fatal dumps through a counter lane, and provide cold `TryFlushRequestedTelemetryDump` / `TryDumpTelemetryBuffer` hooks to write `Dump_SHINOBU_04.h8dump` outside Burst jobs.
Rejected Alternatives: `Debug.Log`, managed queues, dynamic List growth, and per-sample file writes were rejected because they allocate or destroy frame time.
Scalability potential: Low logs only warnings; Middle keeps 300 high-level states; High/Ultra can trigger external visual readers without changing Burst jobs.
Hardware Impact: The counter is one atomic per batch result or raymarch step. On low-end silicon this is cheaper than blind profiling after a freeze.

## Decision 007: Stateless DataVault Alias Packet

Problem: The sampler needs MapMagic height samples, splat-derived material bytes, encoded SDF, cave materials, and sector masks without owning their lifetime.
Solution: `GlobalWorldSamplerData` stores NativeArray aliases and hash metadata only; owners resolve DataVault handles or streaming payloads before scheduling jobs.
Rejected Alternatives: Static global sampler state, direct calls into MapMagic bridge, direct internal voxel volume dependency, and per-query data lookup.
Scalability potential: Low can pass sparse/no-cave sector masks; Middle streams normal chunks; High/Ultra can feed denser SDF/material payloads through the same packet.
Hardware Impact: Setup is handle copy only. Hot-path cost is pointer/NativeArray indexing, not service lookup; expected i3/MX350 gain is stable query latency under streaming churn.

## Decision 008: Editor Probe Without Colliders

Problem: Designers need to inspect the math seam, LOD, and mock cave shape without reintroducing collider authority.
Solution: Add `MathTerrainProbeWindow` with editor-only `GlobalDataVault` buffers, `VaultBufferHandle<T>` resolution, mock sculptor sliders, `Force MATH_LOD_LOW`, SceneView camera raymarch, and a Handles normal gizmo.
Rejected Alternatives: temporary MeshCollider generation, `Physics.Raycast`, and sampling a rendered depth buffer were rejected because they validate a different system than the Burst sampler.
Scalability potential: Low toggles nearest SDF to preview toaster behavior; Middle/High use trilinear and smoothing; Ultra can increase mock SDF density later without changing runtime sampler contracts.
Hardware Impact: Editor-only allocation and raymarching do not hit runtime. On low-end editor machines, force-low gives the cheapest inspectable path.

## Decision 009: Compile Gate Boundary

Problem: `dotnet build Hecton8.Core.csproj` fails on `GameBootstrapper` references to `VaultConfigurationAsset` and `VaultMemoryLayoutConfig`, while those symbols live under `Hecton8.Core.Memory`.
Solution: Do not edit Bootstrap domain from SHINOBU_04. Record the dependency wall and keep the sampler isolated; the new file was added to the local Core project file for real compiler participation.
Rejected Alternatives: Adding a blind `using` or moving memory contracts from another domain was rejected because it crosses the assigned world-terrain boundary without integrator approval.
Scalability potential: Low through Ultra sampler behavior is unaffected; the blocker is a compile visibility issue in bootstrap.
Hardware Impact: No runtime hardware impact from the blocker. Sampler performance claims remain analytical until the external compile gate is cleared.

## Decision 010: Editor H-Phi Eviction

Problem: The first editor probe held private Persistent `NativeArray` fields and used `.ToString()` in `OnGUI`, which is acceptable for a throwaway tool but violates the stricter H-Phi and zero-GC audit language.
Solution: Move probe arrays into a local editor-only `GlobalDataVault`, store only `VaultBufferHandle<T>` fields, resolve temporary NativeArray views on demand, and replace `.ToString()` labels with numeric editor fields.
Rejected Alternatives: Keeping private arrays because the tool is editor-only was rejected; it weakens the architecture proof and teaches the wrong pattern.
Scalability potential: The same facade now demonstrates the production ownership model: hot data lives in vault buffers, not in component fields. Low through Ultra differ only by sampler config and payload density.
Hardware Impact: Runtime path unchanged. Editor allocation is centralized in the vault and can be inspected for alignment/arena pressure.

## Decision 011: Literal DTO Initializer Purge

Problem: Value-type object initializers do not allocate managed heap memory, but the SHINOBU prompt explicitly treats even apparent `new struct` construction as audit poison in the sampler path.
Solution: Replace DTO object initializers for query, data packet, sample result, hardfloor result, and telemetry entry with direct field-by-field value writes.
Rejected Alternatives: Keeping the initializers and explaining IL semantics was rejected because project review is grep-driven under concurrent agent churn.
Scalability potential: Low through Ultra behavior is unchanged; the benefit is a harder zero-GC proof surface and less ambiguity for static auditors.
Hardware Impact: No measured runtime gain claimed. The practical impact is audit safety and unchanged stack/value-copy behavior on i3/MX350 and ARM64.

## Decision 012: NaN Vaccine And Hot-Path Dump Eviction

Problem: The first invalid-input branch wrote non-finite local coordinates into the hardfloor result and invoked managed dump I/O through a `[BurstDiscard]` helper on the sample call stack.
Solution: Sanitize invalid local positions to `float3.zero`, guard query/origin/height/SDF/sector math with finite and overflow checks, request dumps through a vault counter, and expose `TryFlushRequestedTelemetryDump()` for cold/editor/system-phase emission.
Rejected Alternatives: Keeping direct file emission in the sampler was rejected because Steam Deck MicroSD and main-thread hitch risk are not acceptable even on fatal-adjacent paths.
Scalability potential: Low gets deterministic hardfloor instead of NaN spread; Middle/High/Ultra can flush richer `.h8dump` artifacts from a controlled debug/export phase without changing gameplay math.
Hardware Impact: Hot path gains a few scalar guards but removes cold I/O risk from the sample stack. No measured microsecond delta claimed.

## Decision 013: Editor Facade UI Toolkit And CSV Bridge

Problem: The editor tool still used IMGUI `OnGUI` and had no file-based bridge for designers to tweak mock seam parameters without touching C#.
Solution: Replace `OnGUI` with UI Toolkit `CreateGUI`, add a CSV mock profile at `Docs/Tasks/SHINOBU_04_MathTerrainProbe.csv`, and add an editor-only hot-reload toggle that updates DataVault-backed mock arrays.
Rejected Alternatives: Runtime CSV polling was rejected because it would add file I/O pressure to gameplay. IMGUI was rejected because the project checklist flags `OnGUI` as a deletion target.
Scalability potential: Low designers can force nearest SDF and tune cheap mock fields; High/Ultra can keep the same gameplay truth while authoring richer SDF payloads later.
Hardware Impact: Runtime path unchanged. Editor-only file I/O never enters Burst jobs or gameplay tick cadence.

## Decision 014: ARM64 Re-Audit And Job Tail Padding

Problem: The Ultra mandate requires every SHINOBU struct to be defensible under ARM64 review, and the batch job structs ended with a lone `byte` after a `uint` field.
Solution: Keep primary DTOs fixed at 64/64/32 bytes, document the 184-byte scalar payload inside `GlobalWorldSamplerData`, and add explicit `byte` + `ushort` tail padding to `BatchSamplerJob` and `BatchLocalSamplerJob`.
Rejected Alternatives: Relying on runtime-added implicit padding was rejected because review must be source-visible. Forcing `[StructLayout(Pack=1)]` was rejected because it is explicitly forbidden for runtime memory.
Scalability potential: Low/ARM64 avoids ambiguity around job packet layout; High/Ultra receive the same Burst job packet without a separate code path.
Hardware Impact: No measured microsecond gain claimed. The hardware benefit is reduced risk of unaligned tail reads or accidental future byte-field packing drift on Quest/ARM64-class CPUs.

## Decision 015: Runtime Constructor Token Purge

Problem: The SHINOBU prompt is grep-hostile: even value-type `new float3(...)` tokens in Burst math can be misread as allocation even though they lower to value construction.
Solution: Add `Float3`, `Float2`, `Double3`, and `Int3` helpers that write public fields directly and replace runtime sampler/job vector constructors with those helpers. The editor probe also stopped using `UnityEngine.Ray`; it now passes explicit origin/direction vectors into the math trace.
Rejected Alternatives: Explaining value-type IL semantics was rejected because the project is under concurrent agent review and audit scans need an unambiguous surface.
Scalability potential: Low through Ultra behavior is unchanged; the benefit is audit hardening and lower risk of accidental managed patterns entering hot sampler code later.
Hardware Impact: No measured microsecond gain claimed. The practical hardware impact is preserving a zero-GC proof surface for the 100k-query/frame path.

## Decision 016: Telemetry Alias Locality

Problem: The telemetry helper read `TelemetryRing` and `SampleCounter` through the sampler data packet multiple times, which creates unnecessary NativeArray handle copies and weakens the CS1612/cache-line audit story.
Solution: Keep the data packet parameter as `in`, copy the two NativeArray handles into locals once, and write the ring through the local alias.
Rejected Alternatives: Passing the entire `GlobalWorldSamplerData` by value was rejected because the alias packet is larger than the two handles the telemetry path actually needs.
Scalability potential: Low devices avoid extra alias-packet traffic in warning/heartbeat paths; High/Ultra keep the same telemetry ABI.
Hardware Impact: No measured microsecond gain claimed. This is L1 hygiene: less repeated field access and less mutable-struct ambiguity on ARM64.

## Decision 017: Double-Precision Sector Bypass

Problem: `HasCaveSector` still cast terrain-local double coordinates to float before sector `floor`, leaving a precision leak in the branch that decides whether SDF sampling is skipped.
Solution: Keep sector coordinate math in double precision through `floor`, validate the double sector coordinate against `int` bounds, and only then convert to `int`.
Rejected Alternatives: Leaving float sector math because the main sampler was already local was rejected; the bypass controls whether caves are sampled and must not flicker at large AUP coordinates.
Scalability potential: Low tier benefits most because sector bypass is its main SDF cost saver; High/Ultra get stable cave/no-cave decisions without changing sample quality.
Hardware Impact: No measured microsecond gain claimed. The change spends two double floors to prevent wrong-sector jitter and accidental SDF work.

## Decision 018: Warning-Free Finite Guard

Problem: A current `dotnet build Hecton8.Core.csproj --no-restore` reached `GlobalWorldSampler.cs` and reported CS1718 on the `double` finite guard because the NaN check used `value == value`.
Solution: Replace the self-comparison with a bounded range check: `value > -doubleMax && value < doubleMax`. NaN and infinity both fail the comparisons, and the expression stays simple for Burst/AOT.
Rejected Alternatives: `double.IsNaN` was rejected because the sampler should keep the finite guard as a primitive comparison path instead of depending on managed BCL helper support inside Burst-callable code.
Scalability potential: Low through Ultra behavior is unchanged; this is compile hygiene and NaN-vaccine clarity.
Hardware Impact: No measured microsecond gain claimed. The hardware effect is preserving a warning-free, branch-cheap finite guard in the sampler math path.

## Decision 019: Ref/Out Query Construction Surface

Problem: The hot jobs used the return-value `Query(...)` helper to construct `GlobalWorldSamplerQuery`. This is stack-only value construction, but the XML mandate explicitly asks for `ref`/`in`/`out` style and the review process is grep-hostile.
Solution: Add `BuildQuery(..., out GlobalWorldSamplerQuery query)` and move batch/raymarch/editor trace call sites to the out-parameter path. Keep `Query(...)` as a compatibility wrapper for cold/manual callers.
Rejected Alternatives: Deleting `Query(...)` was rejected because it is a harmless public convenience wrapper and removing it would be unnecessary API churn during a multi-agent batch.
Scalability potential: Low through Ultra behavior is unchanged. The benefit is a harder audit surface and less 32-byte query return-copy ambiguity in hot jobs.
Hardware Impact: No measured microsecond gain claimed. The hardware effect is L1/register-pressure hygiene, not a proven runtime speedup.

## Decision 020: Layout Probe API

Problem: The report listed byte offsets, but there was no code-level probe for current compiled sizes.
Solution: Add `GetStructLayoutBytes(out terrainResultBytes, out telemetryEntryBytes, out queryBytes, out dataBytes)` using `UnsafeUtility.SizeOf<T>()`.
Rejected Alternatives: Reflection-based layout inspection was rejected because runtime reflection is forbidden and unnecessary.
Scalability potential: Low through Ultra get the same ABI; the probe gives integrators a cheap static/runtime sanity check before wiring the sampler into scheduler phases.
Hardware Impact: No hot-path impact when not called. It validates the cache-line contract: 64-byte result, 64-byte telemetry row, 32-byte query row.

## Decision 021: Named ABI Constants

Problem: Byte layout proof still depended on comments and report text. A later refactor could change field order while leaving documentation stale.
Solution: Add named public constants for the expected byte sizes and byte offsets of `TerrainSampleResult`, `GlobalWorldSamplerTelemetryEntry`, and `GlobalWorldSamplerQuery`, plus `DataScalarPayloadBytes`. Add `ValidateStructLayout()` to compare compiled sizes against the constants.
Rejected Alternatives: `Marshal.OffsetOf` or reflection-based field inspection was rejected because runtime reflection is forbidden and unnecessary for the hot ABI contract.
Scalability potential: Low through Ultra keep the same cache-line ABI. Integration tests and editor probes can assert the constants before scheduling large sampler batches.
Hardware Impact: No hot-path cost unless explicitly called. The value is ARM64/L1 regression prevention, not measured frame-time savings.
