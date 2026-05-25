# SHINOBU_245 Rationale - TERRAIN_CHUNK_PAGING_SYSTEM

Status: ULTRA MANDATE STATIC REWORK APPLIED / LAPLACE-HUME STATIC DEFECTS INTEGRATED / UNITY VERIFICATION PENDING / BUILD BLOCKED BY CPU OR ACTIVE DOTNET POLICY

## Decision 001 - Scope Boundary
Problem: Existing world streaming includes Addressables residency, telemetry dumps, and offline editor bakers. Task requires terrain binary chunk paging, not a broad rewrite of all world disk writers.
Solution: Add a dedicated TerrainChunkPager runtime under World ownership and remove/replace only runtime synchronous archaeology that directly violates terrain streaming. Keep unrelated crash dump/export writers untouched unless scanner classifies them as runtime load paths.
Rejected Alternatives: Rewriting WorldChunkResidencyManager would mutate a large existing Addressables pipeline and risk cross-agent regressions. Deleting all FileStream usage would break black-box dumps and editor bake tools.
Scalability potential: Low uses narrow ring and mock/proxy fallback. Middle/High/Ultra widen ring and preserve more staged chunks without changing truth layout.
Hardware Impact: i3/MX350 avoids 3-second main-thread File.ReadAllBytes stalls; expected main-thread enqueue/evaluate cost stays in microseconds while disk latency moves to H8_Terrain_Pager.
First-20-minutes Route Impact: Removes terrain chunk hitch risk while the player leaves the starting area and crosses the first open-ocean/base-approach sector boundary.

## Decision 002 - Threading Primitive
Problem: Coroutine and Task.Run load paths allocate and tie I/O cadence to Unity frame flow.
Solution: One persistent System.Threading.Thread named H8_Terrain_Pager with AutoResetEvent sleep and preallocated request/result rings.
Rejected Alternatives: Task.Run per sector, async Task fan-out, Unity coroutine loader. All create managed scheduling churn and unpredictable GC.
Scalability potential: Low throttles queue depth and ring radius continuously under latency. Ultra keeps more requests in flight and uses saved main-thread time for farther terrain visibility.
Hardware Impact: MicroSD-class storage backpressure shrinks request radius before queue debt saturates memory. Main thread pays ring enqueue only.

## Decision 003 - Memory Ownership
Problem: Terrain buffers are long-lived cross-domain native data and cannot be local Persistent allocations unless DataVault is unavailable.
Solution: Request ChunkMetadataDTO, requests, results, telemetry, staging, compressed scratch, active bytes, tuning, and counters from GlobalDataVault using SystemID.WorldStreaming and UninitializedMemory for byte slabs.
Rejected Alternatives: Independent NativeArray allocation as primary path. It violates Data Sovereignty and prevents generation ownership audits.
Scalability potential: Weak devices reduce capacities through serialized config; high devices can raise capacities without changing DTO ABI.
Hardware Impact: Uninitialized large byte slabs avoid cold zero-fill cost on i3/MX350; exact gain depends on configured buffer megabytes.

## Decision 004 - Compression
Problem: Baked .h8bin payloads may be LZ4, but mandate file says dictionary APIs are not currently bound and dictionary claims require corpus proof.
Solution: Implement unmanaged block LZ4 safe decompression for plain LZ4 blocks and raw fallback. Do not implement dictionary LZ4.
Rejected Alternatives: Reusing SaveDeltaCompression internals from another namespace or claiming dictionary support without bindings.
Scalability potential: Low saves disk bandwidth with background CPU. Ultra can load larger far-ring chunks without main-thread stalls.
Hardware Impact: MicroSD reads reduce when compressed blocks exist; CPU cost is isolated to background worker.

## Decision 005 - AUP Hashing
Problem: Float absolute coordinates lose precision far from origin and break negative-sector requests.
Solution: Compute sector X/Z from double3 using math.floor before FNV-1a 64-bit hashing.
Rejected Alternatives: Transform.position, float3 casts before sector math, or modulo string paths.
Scalability potential: Same hash route from low to ultra; quality changes radius/cadence only.
Hardware Impact: No measurable hardware cost; prevents wrong-file churn that would waste disk and memory.

## Decision 006 - Cold CSV Without Managed Byte Arrays
Problem: `streaming_hardware_profiles.csv` must configure byte limits and radius caps, but `File.ReadAllBytes` violates the zero-GC mandate and is detectable by the SHINOBU_245 scanner.
Solution: Allocate `CsvScratchBufferId` in the Vault, read the file into native scratch during cold boot with `FileStream`, then parse through `ReadOnlySpan<byte>` and write unmanaged tuning/profile DTOs.
Rejected Alternatives: `File.ReadAllBytes`, `string.Split`, LINQ, or a managed CSV library. Standard Unity text assets would allocate and hide import timing.
Scalability potential: Low uses MicroSD caps from CSV to shrink ring/queue/bytes. Middle raises queue and chunk cap. High/Ultra widen radius and commit budget without DTO layout changes.
Hardware Impact: i3/MX350 avoids one managed byte[] and string tokenization burst at boot; hot frame impact remains 0 us.

## Decision 007 - Result Ring Backpressure
Problem: If the background result ring fills, dropping a worker result leaves metadata stuck in `Loading` and leaks a chunk slot.
Solution: `PublishWorkerResult` waits on the background thread with short `Thread.Yield` then `Thread.Sleep(1)` until the main thread drains space or shutdown begins.
Rejected Alternatives: Silently dropping results, growing the ring, or publishing managed callbacks. All either corrupt residency or allocate.
Scalability potential: Low storage can stall safely without losing slot state. Ultra can raise queue capacity and still preserve deterministic completion semantics.
Hardware Impact: Backpressure cost is paid only by `H8_Terrain_Pager`; main thread remains at enqueue/drain microseconds.

## Decision 008 - Truthful Scanner Boundary
Problem: `Assets/_Project/Scripts/World` contains unrelated synchronous runtime readers owned by volcanic, voxel, coral, seed ship, and resource systems.
Solution: `Synchronous_IO_Scanner` reports pager-owned findings separately from `EXTERNAL_WORLD_DEBT`; SHINOBU_245 claims eradication only for `TerrainChunkPagerRuntime`.
Rejected Alternatives: Editing foreign domains without integrator order, hiding external findings, or claiming global World purity.
Scalability potential: Scanner allows lead programmers to prioritize remaining debt by platform impact while the pager itself remains clean.
Hardware Impact: External debt still risks low-end stalls; SHINOBU_245 terrain paging no longer contributes blocking terrain chunk reads.

## Decision 009 - Editor Waterfall Instead Of Runtime UI
Problem: Designers need live I/O feedback, but runtime UI would add presentation allocations and coupling.
Solution: UI Toolkit editor window with fixed prebuilt bar elements for latency/active-count waterfall and sliders mutating the Vault-backed tuning DTO.
Rejected Alternatives: Runtime Canvas, spawned debug objects, or IMGUI-only facade. These either couple gameplay to tooling or miss the UI Toolkit requirement.
Scalability potential: Low/Middle/High/Ultra profiles can be stress-tested live by moving continuous radius, latency, queue, and commit controls.
Hardware Impact: Editor-only cost; no player runtime frame cost.

## Decision 010 - Vault-Only Fail-Closed Allocation
Problem: The first pager draft fell back to local `H8Memory.Allocate` when `GlobalDataVault` could not provide a buffer. That violates Data Sovereignty and creates an unowned native memory route.
Solution: Bootstrap now acquires `VaultGenerationHandle<T>` descriptors for BufferIDs `71740..71757`, resolves NativeArray views from the cached Vault, locks every held buffer while worker/job aliases exist, releases generation handles on confirmed shutdown, and fails closed with `TelemetryFaultVaultUnavailable` if any required buffer is missing.
Rejected Alternatives: Silent local Persistent fallback and duplicate NativeMemorySentinel registration of Vault aliases. Both hide ownership and generation drift.
Scalability potential: Low/Middle/High/Ultra resize only through cold serialized capacities or cold profile selection. Live quality still scales radius, queue, and commit cadence without changing allocation ownership.
Hardware Impact: i3/MX350 avoids allocator fragmentation and duplicate owner accounting; high-end devices can raise cold capacities without changing DTO ABI.

## Decision 011 - Immutable Chunk Capacity After Allocation
Problem: CSV/tuner writes could change `ChunkByteCapacity` after byte slabs were allocated, while worker and commit offsets still used the mutable field. That could write past `_stagingBytes`, `_activeBytes`, or `_compressedScratchBytes`.
Solution: `_allocatedChunkByteCapacity` is frozen immediately before Vault allocation. Runtime tuning, CSV ingest, request DTOs, worker offsets, and commit offsets are clamped back to this immutable value until a cold reallocation path exists.
Rejected Alternatives: Letting UI sliders mutate live slab size or reallocating while worker/job aliases exist. Both create UAF/OOB risk.
Scalability potential: Low devices choose smaller cold slabs; high/ultra choose larger cold slabs. Runtime quality remains continuous through radius and cadence, not live slab mutation.
Hardware Impact: Prevents native OOB corruption on all devices; no frame-time cost beyond scalar clamps.

## Decision 012 - Strict Sidecar Header And CRC
Problem: A generic raw fallback could accept a corrupt or wrong `.h8bin` file and hydrate random bytes into active terrain buffers. Header `uint` fields were cast before validation.
Solution: Real files now require `TerrainChunkFileHeaderDTO=32`, magic `H8CB`, endian normalization, unsigned size/offset validation before casts, supported compression kind, and CRC32 verification after raw read or LZ4 decode. Headerless data is only allowed through deterministic mock generation.
Rejected Alternatives: Raw fallback for real files and trusting `StoredBytes`/`UncompressedBytes` before bounds checks. Both can silently corrupt terrain state.
Scalability potential: Low storage gets early rejection instead of wasting decompression; ultra can stream larger valid chunks with the same ABI.
Hardware Impact: CRC cost is background-thread only. Low-end main thread remains unaffected; corruption becomes a telemetry fault instead of undefined native state.

## Decision 013 - Worker Race Removal
Problem: The background worker read the 80-byte tuning DTO while editor/main code could write it, causing torn reads.
Solution: Worker-needed mock delay values are copied into the 64-byte request DTO at enqueue time. The worker no longer reads `_tuning[0]` during mock delay simulation.
Rejected Alternatives: Locking around tuning reads or using managed callbacks. Locks add blocking; callbacks allocate and couple worker to UI/runtime.
Scalability potential: Queue requests carry the quality/latency snapshot they were created under. Later quality changes affect new requests continuously.
Hardware Impact: Removes a data race with no measurable runtime cost; two `int` fields reuse DTO padding.

## Decision 014 - Shutdown Memory Safety
Problem: Releasing Vault buffers after a 500 ms worker join could let a slow FileStream/decompress path write freed memory.
Solution: Shutdown now releases native/Vault buffers only after worker termination is confirmed. If the worker does not stop inside the shutdown fence, the runtime unregisters from dispatch and keeps buffers locked instead of freeing memory under a live thread.
Rejected Alternatives: Forced abort, immediate release, or indefinite main-thread join. Abort is unsafe in managed I/O; immediate release is UAF; indefinite join can freeze editor/player shutdown.
Scalability potential: Slow disks fail closed without corrupting memory. Fast NVMe exits normally and releases handles.
Hardware Impact: Low-end slow storage may retain memory on abnormal shutdown, but avoids native corruption. Normal frame path unchanged.

## Decision 015 - Descriptor-Only Vault State
Problem: The runtime still stored `NativeArray<T>` views as private fields. They were Vault aliases, but static policy treats persistent native views as ownership ambiguity and relocation risk.
Solution: Runtime now stores `VaultGenerationHandle<T>` descriptors, raw pointers captured only after required Vault locks, explicit lengths, and method-local `NativeArray<T>` views resolved only for Burst scheduling/CSV parsing. Release clears descriptors and raw aliases.
Rejected Alternatives: Keeping private `NativeArray<T>` fields with comments. That does not prove relocation safety or Data Sovereignty.
Scalability potential: Low/Middle/High/Ultra capacities still scale through cold Vault descriptor lengths; live quality changes radius/queue/cadence only.
Hardware Impact: Removes hidden native view ownership and reduces teardown/relocation ambiguity on i3/MX350 without adding frame allocations.

## Decision 016 - Lock And Result Fences
Problem: Vault `TryLockBuffer` results were ignored, and worker results were accepted by slot index alone. A failed lock or stale worker result could mutate invalid/reused memory.
Solution: Lock acquisition is all-or-fail with a lock mask and partial unlock. The metadata padding remains explicit bytes per the XML contract; while a slot is `Loading`, `FileOffset@12` temporarily stores the request sequence, and result drain verifies sector hash, that sequence, and `Loading` before mutation.
Rejected Alternatives: Slot-only validation and best-effort locks. Both are standard Unity convenience patterns and unsafe under background I/O.
Scalability potential: Slow storage can finish old requests after eviction/reuse without corrupting current slots. Ultra can raise queue pressure without weakening correctness.
Hardware Impact: Adds two uint compares per result. Cost is below measurable frame budget and prevents native corruption on weak storage.

## Decision 017 - Worker-Only Blackbox Dump
Problem: Fault telemetry previously opened and wrote `FileStream` from `WriteTelemetry()`, a dispatcher phase.
Solution: Telemetry now raises a one-bit dump request only on new fault masks. The persistent pager worker wakes, snapshots the 300-entry ring into preallocated compressed-scratch bytes, and writes `Dump_SHINOBU_245.bin` off the main thread.
Rejected Alternatives: Synchronous dispatcher dump or per-fault managed task. The first stalls; the second allocates and hides scheduling.
Scalability potential: Low devices pay no disk dump cost on the main thread during faults. High devices still get immediate forensic data.
Hardware Impact: Main thread saves worst-case milliseconds during disk fault dump; worker pays file write after current request drain.

## Decision 018 - Bounded LZ4 Length Arithmetic
Problem: Malformed LZ4 extension bytes could overflow `int` length accumulation and feed negative values into bounds checks.
Solution: Length extension accumulation now uses `long`, rejects above remaining output capacity and `int.MaxValue`, and checks the match base before adding the mandatory four bytes.
Rejected Alternatives: Trusting offline bakers or relying on later `MemCpy` bounds. Corrupt sidecars must fail before native copy.
Scalability potential: Same codec route across all tiers; low storage avoids wasting CPU on corrupt payloads.
Hardware Impact: A few scalar checks per LZ4 sequence on background thread; prevents undefined native reads/writes.

## Decision 019 - Statement-Scoped I/O Scanner
Problem: The scanner used an 8-line context window, so a nearby allow marker could accidentally whitelist a forbidden `FileStream`.
Solution: `Synchronous_IO_Scanner` now extracts only the current `new FileStream(...)` statement span and requires the allow marker or `FileOptions.Asynchronous` inside that statement.
Rejected Alternatives: Wide-context grep. It is fast but can produce false green reports.
Scalability potential: Maintains honest I/O debt visibility while SHINOBU_245 remains clean.
Hardware Impact: Editor-only validation cost; prevents hidden main-thread disk stalls from returning unnoticed.

## Decision 020 - Layout Guard And Worker Heartbeat
Problem: The pager layout guard still used `Marshal.OffsetOf`, and telemetry could report queue/fault counters while a worker thread had silently died or stopped heartbeating during a long pending load.
Solution: `ChunkMetadataLayoutGuard` now reads field offsets through `UnsafeUtility.GetFieldOffset` over explicit `[FieldOffset]` metadata. The worker publishes a volatile heartbeat timestamp on start, wake, and request processing boundaries; `WriteTelemetry()` flags `TelemetryFaultIo` when pending/loading work exists and the worker is inactive or stale beyond `max(5000ms, CriticalLatencyMs*8)`. The dump header fault field now always writes the actual fault mask.
Rejected Alternatives: Leaving layout proof on Marshal reflection, per-frame managed worker watchdog tasks, or blocking `Thread.Join`/`Complete` probes from the dispatcher. Those either weaken the ARM64 proof path or create hot managed scheduling/blocking.
Scalability potential: Low/Middle storage can still spend seconds inside one background read before being classified as faulted; Ultra keeps the same correctness path while wider rings create more telemetry evidence under load.
Hardware Impact: Adds one volatile timestamp read and one scalar stopwatch comparison only when requests/loading exist; expected cost below 1 us on i3/MX350 while catching worker death before a long swim hides missing terrain.

## Decision 021 - Player-Safe Layout Guard And Ledger Route
Problem: The replacement layout guard still left reflection and `UnsafeUtility.GetFieldOffset` callable from the general validation path. That is acceptable for editor proof but unnecessary risk for player/AOT runtime and not a clean ARM64 cold-boot invariant.
Solution: Split the guard. Player/runtime validation now checks explicit offset constants plus `UnsafeUtility.SizeOf<ChunkMetadataDTO>() == 32`; `System.Reflection` and `UnsafeUtility.GetFieldOffset` run only under `UNITY_EDITOR` to verify field metadata during authoring. The binary payload ledger now records SHINOBU_245 BufferIDs `71740..71757`, DTO anchors, endian route, rollback exclusion, heartbeat fault route, and the managed worker I/O boundary.
Rejected Alternatives: Runtime reflection proof, `Marshal.OffsetOf`, or no ledger ownership entry. Runtime reflection can allocate or fail under player stripping; Marshal violates the requested proof route; missing ledger ownership makes BufferID and ABI claims unverifiable.
Scalability potential: Low/Middle/High/Ultra all share the same 32-byte metadata ABI. Quality and latency scale residency radius and cadence only; the proof route does not change with hardware tier.
Hardware Impact: Removes reflection metadata access from player validation. Expected cold-boot win is small but deterministic; the real gain is eliminating an AOT/reflection failure mode on ARM64 mobile targets.

## Decision 022 - Overflow And AUP Determinism Hardening
Problem: Sector distance math subtracted `long` coordinates before widening to `double`, desired-sector offsets used unchecked `long + int`, CSV integer parsing accepted `-` as `0`, and live commit-budget products could overflow before clamping.
Solution: Widen sector coordinates before subtraction, use saturating small-offset addition for desired sectors, parse CSV ints through bounded `long` with at least one digit, and route commit byte budgets through a float/int-safe helper. AUP residency and eviction jobs now use `FloatMode.Deterministic` because their output controls local terrain authority and must not drift across rollback-adjacent environmental decisions.
Rejected Alternatives: Trusting world bounds to avoid `long` extremes, leaving parser overflow to wrap, or keeping `FloatMode.Fast` for all math because the first mandate requested it. The AUP sector boundary is a deterministic authority edge, not a decorative shader approximation.
Scalability potential: Low/Middle/High/Ultra all preserve the same sector identity; quality still scales radius and cadence only, so weak devices shed work without changing file identity or DTO layout.
Hardware Impact: Adds only scalar clamps and casts in cold/small loops. On i3/MX350 the measurable frame cost is below 1 us while preventing overflow-triggered wrong chunk requests and native OOB copy budgets.

## Decision 023 - LZ4 Bound, Native Path Open, And Dump Snapshot
Problem: LZ4 `StoredBytes` was incorrectly bounded by uncompressed chunk capacity, valid CRC32 `0` was rejected, worker path building allocated a new `string` per sector load, and blackbox dump copied live telemetry while the dispatcher could write it.
Solution: Bootstrap now proves active/staging slab bytes, compressed scratch bytes using `chunk + chunk/255 + 16`, and a dedicated `71758` telemetry dump snapshot before Vault acquisition. Real file open builds a fixed char/UTF-8 path and uses native handles before wrapping them in worker `FileStream`, eliminating per-load sector path strings and the `File.Exists` probe. Fault dumps copy telemetry to `71758` on the dispatcher once per new fault and publish frame/fault data with a packed interlocked value for worker file I/O.
Rejected Alternatives: Reusing compressed scratch for dumps, dropping valid zero CRCs, enforcing baker-only LZ4 stored-size assumptions, or accepting managed sector path allocation because it occurs on the background thread. These hide races or GC under streaming pressure.
Scalability potential: Weak storage uses the same ABI but gets accurate compressed bounds and latency-driven radius shrink; high/ultra devices can raise chunk capacity cold without changing authority or rollback exclusion.
Hardware Impact: LZ4 compressed scratch grows by roughly 0.4% plus 16 bytes per slot versus uncompressed capacity. The cost is cold Vault memory only; it prevents false LZ4 rejection and removes repeated path string allocation during streaming bursts.
