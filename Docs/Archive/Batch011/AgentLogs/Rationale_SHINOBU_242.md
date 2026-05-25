# Rationale_SHINOBU_242

Status: PENDING VERIFICATION

## Decision 00: Domain Fence
Problem: Hydraulic erosion simulation is heavy terrain math and can destroy runtime frame time if routed through scene objects or runtime terrain mutation.
Solution: Keep the baker editor-only and file-output-only. Runtime receives immutable baked height and silt data.
Rejected Alternatives: Runtime terrain modification, ParticleSystem rain, Unity Terrain GetHeights/SetHeights, managed droplet objects. These violate zero-GC, terrain streaming, and frame-time law.
Scalability potential: Low uses baked static height/silt only; Middle streams sector files; High keeps richer macro maps; Ultra consumes the same truth with denser render tessellation and shader detail.
Hardware Impact: On i3/MX350, moving erosion offline avoids multi-millisecond runtime terrain edits and removes GC spikes from managed droplet lists.

## Decision 01: Mandate Selection
Problem: The task crosses native data layout, Burst jobs, AUP determinism, chunk streaming, and designer-facing tooling.
Solution: Selected ARM64 layout, native memory/jobs, zero-GC, AUP determinism/precision, deterministic RNG, world chunking, and CSV/binary bridge mandates before coding.
Rejected Alternatives: Reading only generic AGENTS.md would miss struct alignment, TempJob ownership, and chunk seam constraints.
Scalability potential: Same baked files serve Low/Middle/High/Ultra; quality differences stay in render/stream consumption, not binary truth mutation.
Hardware Impact: Correct layout and native contiguous buffers reduce cache misses during editor bakes on low-end CPUs while preserving data for overkill rendering on high-end GPUs.

## Decision 02: Single-Writer Erosion Kernel
Problem: Parallel droplets that mutate the same height cell need float atomics or reduction. Unity does not provide cheap deterministic float atomics for arbitrary heightmap writes.
Solution: Implement `SimulateHydraulicErosionJob` as a Burst `IJob` with a single writer over raw pointers, and keep seam transfer in `NativeQueue<ErosionDropletDTO>`.
Rejected Alternatives: `IJobParallelFor` direct height writes with unsafe race risk; managed locks; per-droplet object queues. Reduction was kept for future expansion but not required for this single-writer kernel.
Scalability potential: Low uses the same baked result with no runtime cost; Middle bakes fewer sectors per editor pass; High and Ultra spend saved runtime budget on tessellation, silt shader blending, and longer terrain LOD residency.
Hardware Impact: On i3/MX350 runtime impact is zero because output is immutable `.h8bin`; editor bake is bounded by linear native arrays and no managed droplets.

## Decision 03: Editor-Only Async Serialization
Problem: Sector height and silt payloads must be written without blocking runtime and without routing through gameplay state.
Solution: Use editor-only `FileStream` with asynchronous file options and copy from `NativeArray<float>` via `UnmanagedMemoryStream` into `.h8bin` files carrying `PayloadFlagRollbackExcluded`.
Rejected Alternatives: JSON/CSV runtime payloads, `File.WriteAllBytes` staging arrays, or save-system deltas. Those add parser overhead or incorrectly classify terrain as mutable gameplay state.
Scalability potential: Low streams compact sector files; Middle uses macro map for distance; High keeps more resident sectors; Ultra spends visual budget on shader detail over the same immutable data.
Hardware Impact: On i3/MX350, no runtime serialization path exists; editor file writes happen outside frame-critical gameplay.

## Decision 04: Seam Transfer as Native Queues
Problem: Droplets crossing a sector border must not die or pool at the edge, but a full 100km heightmap cannot be resident in one native array.
Solution: Preserve the 32-byte droplet state and wrap local coordinates into North/South/East/West `NativeQueue<ErosionDropletDTO>` lanes; neighbor sector import uses a bridge to seed its droplet buffer from the incoming queue.
Rejected Alternatives: global monolithic heightmap, border-clamped droplets, or managed transfer lists. Monolithic RAM is not acceptable, border clamp creates visible seams, managed lists create bake-scale GC pressure.
Scalability potential: Low bakes fewer sectors per editor pass; Middle pipelines sector queues; High/Ultra can increase droplet budgets and downstream shader richness without changing runtime truth.
Hardware Impact: On i3/MX350 this avoids runtime seam repair and keeps editor memory sector-local.

## Decision 05: Immutable Terrain Data and Rollback Fence
Problem: Baked height/silt arrays are large and static. If they enter rollback state, snapshot stride and Merkle hashing inflate for no gameplay authority benefit.
Solution: Write `PayloadFlagRollbackExcluded` into every erosion `.h8bin` header and document the route in `Docs/ARCHITECTURE/HYDRAULIC_EROSION_BAKER_SHINOBU_242.md`.
Rejected Alternatives: adding a terrain leaf to rollback descriptors or hashing terrain every frame. Both are false authority routes and create netcode bloat.
Scalability potential: Low keeps rollback leaf budget for dynamic entities; Middle/High/Ultra increase visual terrain detail without changing network truth.
Hardware Impact: On i3/MX350 this avoids copying/hashing large static terrain pages in rollback frame snapshots.

## Decision 06: Macro Map as Continuous LOD Input
Problem: Distant mountains need eroded silhouette continuity without streaming every high-resolution sector.
Solution: Generate `macro_erosion.h8bin` with `GenerateMacroErosionMapJob` from baked heights.
Rejected Alternatives: runtime downsample or shader-only fake macro relief. Runtime downsample wastes frame time; shader-only fake breaks route ownership because it diverges from baked height truth.
Scalability potential: Low uses macro for far terrain; Middle streams sector detail near player; High and Ultra keep richer sector residency and material blending.
Hardware Impact: On i3/MX350 this shifts far-terrain preparation to editor time and reduces runtime CPU and IO pressure.

## Decision 07: Designer Facade and CSV Bridge
Problem: Erosion weathering needs technical controls without hardcoding all tuning in C#.
Solution: Add UI Toolkit forge controls and a byte-parser for `terrain_weathering_profiles.csv`; missing CSV falls back to a deterministic basalt profile.
Rejected Alternatives: binary-only tuning or `string.Split` CSV parsing. Binary-only blocks designers; split parsing produces avoidable garbage and weaker schema checks.
Scalability potential: Low profiles can reduce bake scope; Middle/High/Ultra profiles can increase rain/capacity/detail while runtime remains immutable.
Hardware Impact: On i3/MX350, tuning is editor-only and does not add runtime parser or ScriptableObject mutation.

## Decision 08: Static Scanner as Repeated Gate
Problem: The codebase already contains runtime terrain mutation debt outside this agent's Environment scan boundary. Silent acceptance would let future runtime erosion creep back in.
Solution: Add `Terrain_Runtime_Scanner_Erosion` to write `WORLD_OPTIMIZATION_REPORT.json` with exact file/line/pattern hits.
Rejected Alternatives: deleting broad runtime geology files from outside the assigned prompt. That risks architectural sabotage and conflicts with other agents.
Scalability potential: Low/Middle/High/Ultra all benefit because terrain mutation debt is surfaced before it consumes runtime frame time.
Hardware Impact: On i3/MX350, every runtime SetHeights-style path flagged by the scanner is a candidate for removing frame spikes.

## Decision 09: Async Native Payload Lifetime
Problem: Async `.h8bin` serialization can cross editor frames. Holding `Allocator.TempJob` height/silt/macro arrays while `await WritePayloadAsync` is pending violates Unity native-container lifetime rules and can invalidate raw pointers passed to `UnmanagedMemoryStream`.
Solution: Keep same-frame droplet and preview scratch on `Allocator.TempJob + UninitializedMemory`, but allocate async-owned height/silt/macro payloads and black-box telemetry on `Allocator.Persistent + UninitializedMemory/ClearMemory`. Dispose all TempJob scratch before the first awaited file write. Add `SanitizeFloatPayloadJob` so raw payload bytes are finite before header checksum/min/max are computed.
Rejected Alternatives: Keeping TempJob arrays across await, staging every payload in managed `byte[]`, or only sanitizing header metadata. TempJob across await is unsafe, managed staging bloats memory, and header-only sanitization writes invalid terrain truth.
Scalability potential: Low/Middle/High/Ultra all receive the same finite immutable data; higher tiers can spend visual budget on tessellation and silt shader detail without depending on editor scratch lifetime.
Hardware Impact: On i3/MX350 this prevents editor bake crashes without adding runtime cost; Persistent allocation is editor-only and removes unsafe pointer lifetime risk during asynchronous disk IO.

## Decision 10: Seam Sidecar Capture
Problem: The directional `NativeQueue<ErosionDropletDTO>` lanes proved seam intent but the async writer consumed them before queue disposal, forcing `TempJob` data across an `await` boundary.
Solution: Drain all queues synchronously into a Persistent seam scratch buffer, record offsets/counts, dispose TempJob queues/droplets, then serialize `.h8seam` sidecars from the persistent scratch.
Rejected Alternatives: Direct async queue serialization, killing seam droplets, or baking a global monolithic heightmap. Async queues violate lifetime, killing droplets creates visible sector rivers, and monolithic RAM breaks sector streaming.
Scalability potential: Low can bake fewer sectors and still preserve handoff artifacts; Middle/High/Ultra can increase droplet counts while the same sidecar contract holds.
Hardware Impact: On i3/MX350 this removes editor native-container faults and prevents re-bakes caused by broken seams; runtime cost remains 0 us.

## Decision 11: Millimeter AUP Quantization
Problem: Hashing raw `double3` sector AUP bits makes deterministic rain paths sensitive to sub-millimeter numeric residue and different authoring math routes.
Solution: Quantize AUP components to integer millimeters before FNV hashing, payload headers, seam headers, and black-box telemetry; seed `Unity.Mathematics.Random` from that stable FNV value for droplet placement.
Rejected Alternatives: Raw double byte hashing, float-sector coordinates, or managed RNG. Raw doubles overreact to non-meaningful drift, floats lose precision in the 100 km world, and managed RNG violates deterministic job rules.
Scalability potential: Low/Middle/High/Ultra all rebuild identical riverbeds from the same sector identity; higher render fidelity does not change bake identity.
Hardware Impact: On i3/MX350 this avoids wasted re-bake/debug churn from tiny coordinate residue; runtime cost is none because this is editor-side identity math.

## Decision 12: Native Memory Sentinel Registration
Problem: The offline baker owns large native buffers for minutes during editor work; untracked allocations make leaks and lifetime abuse invisible.
Solution: Wrap every NativeArray and NativeQueue creation in SHINOBU_242 tracking helpers and register with `NativeMemorySentinel`; use `Session` for async Persistent payloads and `TempJob` for same-frame scratch.
Rejected Alternatives: Untracked native allocations or GlobalDataVault ownership. Untracked buffers fail forensic requirements; Vault handles would invent runtime authority for an editor-only sidecar baker.
Scalability potential: Low devices get better leak diagnosis during constrained editor bakes; high-end devices can push larger sectors with the same tracking proof.
Hardware Impact: On i3/MX350 this catches native leaks before memory pressure becomes a full editor restart; runtime cost remains 0 us.

## Decision 13: Sidecar, Not Data Monolith
Problem: The erosion files are static terrain-cache artifacts, but Data Monolith readiness requires `static_data.h8bin`, section IDs, import validation, and runtime owner proof that this task does not own.
Solution: Document SHINOBU_242 as `StreamingAssets/Hecton8/TerrainErosion` sidecar output with no Vault BufferID and no runtime authority. Future runtime import must be assigned to the terrain streaming owner.
Rejected Alternatives: Adding a Data Monolith section row or calling `GlobalDataVault.TryGetLatestCreated()` as a fallback. Both would create fake authority and violate the documented Data Monolith boundary.
Scalability potential: Low streams compact sidecars; Middle/High/Ultra can retain more sectors and render richer material detail after a proper runtime owner imports the data.
Hardware Impact: On i3/MX350 this avoids boot-time monolith coupling and keeps runtime memory ownership explicit for the future streaming loader.

## Decision 14: Pointerless Queue Label Split
Problem: `NativeMemorySentinel` coalesces pointerless native collections by `(owner,label)`. Preview and bake queues sharing labels could remove or resize the same forensic record.
Solution: Split queue labels into `Preview.*` and `Bake.*` lanes while keeping the same owner. Arrays remain pointer-tracked and do not need this split.
Rejected Alternatives: Shared queue labels or disabling preview during bake. Shared labels break tracking; disabling preview harms artist iteration without fixing the underlying proof route.
Scalability potential: Low/Middle/High/Ultra editor use can run preview and bake without losing queue telemetry identity.
Hardware Impact: On i3/MX350 this improves leak diagnosis during constrained editor sessions; runtime cost remains 0 us.

## Decision 15: Explicit Endian Marker and Zero-Count Seams
Problem: Native-endian headers and omitted empty seam files create silent failure modes: stale seam sidecars can be reused, and future importers cannot distinguish little-endian payloads from corrupted/reversed files.
Solution: Add `0x01020304` endian markers to height/silt/macro/seam headers, validate them in self-audit, use checked payload-byte arithmetic, and always rewrite all four `.h8seam` files even when count is zero.
Rejected Alternatives: Relying on host endianness, deleting empty sidecars, or leaving absent files as the no-transfer signal. Host endianness is not an explicit contract; absent files permit stale artifacts.
Scalability potential: Low streams clean compact sidecars; Middle/High/Ultra can pipeline sector bakes without stale cross-sector state.
Hardware Impact: On i3/MX350 this avoids failed long bakes caused by stale seam inputs and prevents future loader misreads; per-sector empty seam cost is four 160-byte editor writes.

## Decision 16: Designer-Controlled Quality Continuum
Problem: `GlobalQualityWeight` affected the erosion kernel, but the Forge window still used a hidden constant. That violates the human-control bridge because artists cannot test low/mid/high bake behavior without recompiling.
Solution: Add a `Global Quality Weight` UI Toolkit slider and feed it into both preview and full bake settings. Slider changes coalesce into a delayed preview refresh.
Rejected Alternatives: Fixed quality constant or immediate preview rebuild for every slider event. Fixed constants block tuning; immediate rebuilds spam Burst cold-sync preview work while dragging.
Scalability potential: Low can preview cheap nearest-biased erosion; Middle tests smooth ramps; High/Ultra tests richer erosion and silt detail from the same facade.
Hardware Impact: On i3/MX350 coalesced preview avoids repeated editor stalls during slider drag; runtime cost remains 0 us.

## Decision 17: Zero-Droplet Schedule Guard
Problem: The forge UI accepts a droplet budget of zero for baseline terrain generation and scanner diagnostics. Scheduling `InitializeErosionDropletsJob` with length zero relies on Unity scheduler edge-case behavior and creates a needless CI/import risk.
Solution: Clamp initialization count against the actual `NativeArray<ErosionDropletDTO>` length and skip the `IJobParallelFor` schedule when the count is zero. The simulation job still runs after height generation and immediately no-ops on zero droplets.
Rejected Alternatives: Forcing at least one synthetic droplet or relying on `Schedule(0, ...)`. A forced droplet mutates the baseline heightmap and lies to diagnostics; relying on zero-length scheduling is avoidable scheduler coupling.
Scalability potential: Low-quality and diagnostic passes can collapse to height/mock generation only; Middle/High/Ultra remain unchanged and still initialize full droplet budgets.
Hardware Impact: On i3/MX350 this saves one cold editor schedule call in zero-rain diagnostics and prevents a possible Unity-version-specific schedule fault; runtime cost remains 0 us.

## Decision 18: Seam Queue Prewarm
Problem: `NativeMemorySentinel` tracked expected queue bytes, but `NativeQueue` itself was not physically prewarmed. Heavy seam crossing could therefore trigger queue block growth during the Burst erosion job, hiding allocator work inside the boundary-transfer path.
Solution: Prewarm each seam queue in cold editor setup by enqueuing and draining `expectedCapacity` default elements before registering the queue with `NativeMemorySentinel`.
Rejected Alternatives: Trusting dynamic queue growth or replacing the queue contract with a monolithic global heightmap. Dynamic growth is unpredictable during bake kernels; a monolithic map violates chunk streaming and memory locality.
Scalability potential: Low preview queues prewarm only small caps; Middle/High/Ultra can raise droplet budgets while seam queue growth remains explicit cold setup work.
Hardware Impact: On i3/MX350 this moves native queue allocation spikes out of the Burst seam-transfer phase and makes long editor bakes less prone to allocator stalls; runtime cost remains 0 us.

## Decision 19: UI Zero Baseline
Problem: The code path supported a zero-droplet diagnostic bake, but the Forge window slider still enforced a minimum of 1000 droplets. That contradicted the human-control facade and made the zero-rain baseline unreachable without calling the baker API directly.
Solution: Lower the `Droplet Count` slider minimum to 0 while keeping the default at one million.
Rejected Alternatives: Keeping a hidden API-only diagnostic or forcing a single droplet. Hidden diagnostics fail the facade requirement; one forced droplet mutates the baseline terrain and poisons silt/height comparisons.
Scalability potential: Low and diagnostic passes can verify mock height/payload/seam headers with no droplet loop; Middle/High/Ultra still use full designer-requested budgets.
Hardware Impact: On i3/MX350 zero-baseline preview/bake skips droplet initialization and simulation loops for serializer/scanner diagnostics; runtime cost remains 0 us.

## Decision 20: Numeric Slider Input Fields
Problem: The Forge window exposed the correct sliders but did not expose numeric input fields. Slider-only tuning makes it difficult to reproduce exact CSV/profile values and invites accidental bake variance.
Solution: Enable `showInputField` on every technical slider: droplet count, rain rate, evaporation speed, sediment capacity, erosion aggressiveness, and `GlobalQualityWeight`.
Rejected Alternatives: Leaving designers to approximate values by dragging or editing C# constants. Drag-only tuning is not reproducible; C# edits violate the hot-reloadable human-control bridge.
Scalability potential: Low/Middle/High/Ultra profile values can be entered exactly and compared without recompilation.
Hardware Impact: On i3/MX350 this reduces unnecessary rebakes caused by imprecise slider tuning; runtime cost remains 0 us.

## Decision 21: Static Compile-Risk Reduction While Build Is Blocked
Problem: CPU policy continues to block dotnet/Unity compile proof, but compile-risk work cannot stop at a claim. The remaining actionable path is targeted static comparison against local Unity idioms.
Solution: Re-scan Burst attributes for exact mandatory flags and compare SHINOBU_242 usage of Awaitable, Span file IO, UI Toolkit numeric fields, NativeQueue prewarm, and NoAlias attributes against existing project code.
Rejected Alternatives: Launching build under CPU 100 or declaring compile readiness from prose. Build under policy breach is forbidden; prose is not evidence.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; static correctness protects the editor baker that produces all tier data.
Hardware Impact: On i3/MX350 no runtime cost; reduces risk of wasting a constrained compile/import cycle on avoidable API mistakes.
