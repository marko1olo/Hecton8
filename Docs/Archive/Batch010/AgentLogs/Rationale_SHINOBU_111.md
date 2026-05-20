# SHINOBU_111 Rationale

Status: PENDING VERIFICATION  
Evidence class: STATIC_SOURCE until compile/runtime/profiler artifacts exist.  

## Decision 00 - Domain And Mandate Selection

Problem: SHINOBU_111 must implement voxel delta compression without creating a second save authority or inventing absent voxel dependencies.

Solution: Bound work to voxel/save persistence surfaces. Read and apply these mandates before code: save binary delta/checksum, ARM64 DTO layout, Zero-GC, native memory/job discipline, crash telemetry, voxel carving persistence, voxel SDF pipeline, and LZ4 dictionary limits.

Rejected Alternatives: Broad rewrite of `SaveManager`, full DataVault migration, or runtime scene wiring. Those would cross owner boundaries and increase compile-wall risk.

Scalability potential: Low uses RLE-heavy fast path and prunes microscopic deltas; middle uses baseline LZ4; high/ultra spend saved I/O and CPU on richer visual fade/deformation presentation instead of serializing dead bytes.

Hardware Impact: Low-end i3/MX350/Steam Deck gain comes from avoiding full chunk writes and redundant zero-fill. Expected scheduling gain is in tens to hundreds of microseconds per dirty sector, pending measurement.

## Decision 01 - Binary Payload Reality

Problem: `voxel_save_schema.h8bin` may be absent; binary payload ledger confirms many payloads are static/script-only and runtime proof is pending.

Solution: Add an emergency deterministic unmanaged schema generator if no schema exists. This keeps the compression pipeline testable without blocking on StreamingAssets/DataMonolith ownership.

Rejected Alternatives: Throwing on missing schema, generating a managed `byte[]`, or adding a new authoritative generated payload by hand.

Scalability potential: Low and middle devices can run the fallback with minimal schema bytes. High/ultra can use the same deterministic schema to stress larger modified-sector profiles.

Hardware Impact: Avoids cold-start failure and avoids managed heap fallback. Estimated low-end gain: prevents multi-ms exception/log path and GC pressure during isolated tests.

## Decision 02 - Header Layout Contract

Problem: Voxel delta chunk headers must be safe for ARM64 and NativeArray/Burst mutation without CS1612 defensive copies.

Solution: Use `[StructLayout(LayoutKind.Explicit, Size = 32)]` with `ulong SectorHash` offset 0, `uint CompressedSize` offset 8, `uint UncompressedSize` offset 12, `ulong XXHash3Checksum` offset 16, and explicit padding at 24/28.

Rejected Alternatives: `Pack=1`, sequential layout with implicit padding, and C# properties. Pack=1 risks unaligned 64-bit reads; properties produce method calls and defensive copies.

Scalability potential: Same DTO is stable from weak to ultra hardware; ultra telemetry can add separate aligned records without bloating the hot header.

Hardware Impact: Preserves aligned 64-bit loads on ARM64/Steam Deck. Estimated low-end gain: avoids split cache-line reads and unaligned access penalties in sector hydration.

## Decision 03 - GlobalDataVault Route Card For Voxel Delta WAL

Problem: Voxel compression scratch, headers, counters, telemetry, and tuning need persistent native memory without private `NativeArray` ownership inside a leaf component.

Solution: Added `BufferID.SaveVoxelDeltaSchemaBytes` through `SaveVoxelDeltaSectorStats` in the save-persistence range 70284-70299. Owner is `SystemID.SavePersistence`. Capacity is requested through `VoxelDeltaCompressionArchitecture.TryResolveVaultBuffers`: 32^3 cell density/material/flag lanes, fixed 300 telemetry entries, one tuning DTO, aligned block counters, RLE bytes, compressed bytes, LZ4 hash table, header lane, and editor sector stats. Generation/stale-handle rule is the existing `GlobalDataVault` handle resolver; relocation/defrag is Vault-owned; disposal is owner release by `SystemID.SavePersistence`.

Rejected Alternatives: Private persistent arrays inside `VoxelDeltaProcessor`; a new save singleton; direct references from Caves to a sibling save writer. Those create stale-handle and compile-wall risk.

Scalability potential: Low uses the same route with smaller requested staging and more pruning. Middle increases write cadence. High/Ultra increase LZ4 effort and telemetry density without changing ownership.

Hardware Impact: Removes allocator churn from sector compression setup. Estimated low-end gain: 40-120 us avoided during dirty-sector boot/staging compared with ad hoc NativeArray allocation, pending profiler proof.

Route ID: SHINOBU_111_SAVE_VOXEL_DELTA_WAL_VAULT  
Instrument: GlobalDataVault  
BufferID: `SaveVoxelDeltaSchemaBytes`, `SaveVoxelDeltaRuntimeDensity`, `SaveVoxelDeltaBaselineDensity`, `SaveVoxelDeltaMaterialIds`, `SaveVoxelDeltaCellFlags`, `SaveVoxelDeltaRleRuns`, `SaveVoxelDeltaBlockCounters`, `SaveVoxelDeltaRleBytes`, `SaveVoxelDeltaCompressedBytes`, `SaveVoxelDeltaLz4HashTable`, `SaveVoxelDeltaHeaders`, `SaveVoxelDeltaCounters`, `SaveVoxelDeltaTelemetryRing`, `SaveVoxelDeltaTelemetryCursor`, `SaveVoxelDeltaTuning`, `SaveVoxelDeltaSectorStats`  
Why owner-local data is insufficient: WAL compression spans Burst jobs, async pager handoff, editor telemetry, and crash forensics; one owner and one Vault route prevents duplicate scratch allocators.  
Proof required before acceptance: compile, `BinaryLayoutManifest`, WAL enqueue smoke, GC 0 B profile, telemetry dump verification.  
Status: PROPOSED / STATIC SOURCE ONLY.

## Decision 04 - Block-Local RLE And Continuous LZ4 Effort

Problem: Full dense voxel chunks inflate saves; variable-length sector RLE is difficult to parallelize without atomics or nondeterministic ordering.

Solution: Encode block-local deterministic RLE in `VoxelRleEncoderJob` with fixed per-block slots and 64-byte block counters, then compact to byte staging in a single deterministic pack job. `VoxelLz4CompressionJob` consumes `GlobalQualityWeight` and I/O pressure to lerp active hash slots, match length, and probe stride.

Rejected Alternatives: One global `NativeList` append in parallel, managed streams, or binary slow/fast hardware tiers. NativeList append would need atomics and nondeterministic ordering; managed streams break Zero-GC; binary tiers violate the continuous quality law.

Scalability potential: Low collapses toward sparse RLE/raw bytes with coarse probes; middle runs moderate hash coverage; high/ultra use near-full hash coverage and shorter matches for better ratio.

Hardware Impact: Steam Deck/i3 path avoids full chunk writes and keeps counters on separate cache lines. Static estimate: 80-350 us saved per dirty 32^3 sector when changes are sparse, pending build/profile proof.

## Decision 05 - Existing Async Pager Owns WAL Durability

Problem: SHINOBU_111 needs async WAL writes but must not create a second save authority or duplicate file rotation logic.

Solution: Added `VoxelWalPayloadPackJob` to write a 32-byte little-endian header plus payload into native staging. `TryEnqueueVoxelDeltaWalWrite` then routes bytes through `IAsyncPersistenceService.TryEnqueueChunkPageWrite` with `H8WorldPagePayloadTypes.VoxelDeltaRle`, leaving pager internals owned by SavePersistence.

Rejected Alternatives: Direct `.sav` writes, `File.WriteAllBytes`, or a new worker thread. The pager already owns tmp/verify/rename/WAL worker behavior; duplicating it would fracture save authority.

Scalability potential: Low limits bytes per frame through tuning; high/ultra can raise byte budgets and compression effort while still using the same pager queue.

Hardware Impact: Avoids synchronous file I/O on dirty terrain save. Estimated low-end gain: prevents multi-ms frame stalls during chunk unload; exact latency requires pager smoke proof.

## Decision 06 - Editor Facade And CSV Control Stay Cold

Problem: Designers need tuning and heatmap visibility without recompiling C# or adding runtime UI allocations.

Solution: Added `VoxelSaveTunerWindow` as editor-only UI Toolkit facade over Vault tuning, telemetry, and sector stats. Added `Assets/_Project/Data/World/voxel_save_profiles.csv` and `VoxelCompressionProfileCsvParseJob`, a byte-level parser with no `string.Split`, JSON, or managed text payload in runtime jobs.

Rejected Alternatives: Runtime UI, `TextAsset.text`, JSON config, or hidden constants. Those either allocate or remove designer control.

Scalability potential: Low/Middle/High/Ultra all use the same CSV fields; only continuous values change. No low/ultra boolean split was introduced.

Hardware Impact: Runtime hot path impact is 0 us until profile hydration is scheduled. Editor overhead is isolated to `UNITY_EDITOR`.

## Decision 07 - Black Box Dump Purge

Problem: The voxel black-box fault path still used `BinaryWriter`, leaving a managed serializer hit in the voxel domain static scan.

Solution: Replaced the writer with a 32-byte explicit header and raw unmanaged NativeArray dump from the 300-frame ring. The existing file path remains owner-local and development/editor guarded.

Rejected Alternatives: Leaving `BinaryWriter` because it is debug-only, or routing the carve black box through the new save WAL. Debug-only still pollutes the mandated scan; save WAL is the wrong authority for carve crash evidence.

Scalability potential: All tiers get identical forensic bytes. High-end does not gain more CPU work here; saved cycles belong to visuals, not crash serialization.

Hardware Impact: Fault-path only. Estimated low-end gain during dump: fewer per-entry virtual/write calls; not a frame-budget claim.

## Decision 08 - Legacy Managed DTO Boundary

Problem: `VoxelDeltaPersistenceDTO.EnsureCapacity` still contains managed arrays for the old save DTO path, and a blind rewrite would collide with existing `SaveManager`/ISaveable loading semantics.

Solution: Leave legacy DTO capacity management cold and document it as outside the new SHINOBU_111 Burst/WAL pipeline. The new compression path uses Vault buffers and `NativeArray<byte>` staging only; no `BinaryWriter`, `File.WriteAllBytes`, `System.Text.Json`, `MemoryStream`, or hot-path `byte[]` exists in the new chain.

Rejected Alternatives: Replacing the entire managed persistence DTO today. That would be a cross-domain save migration without load-path proof and would risk corrupting existing save compatibility.

Scalability potential: Low/Middle/High/Ultra all route new terrain delta persistence through the same Vault WAL path. Legacy DTOs remain compatibility/cold fallback only until a dedicated migration removes them.

Hardware Impact: New path avoids legacy DTO heap pressure during compression. Exact GC reduction requires runtime save-path smoke proof.

## Decision 09 - Generated Project File Compile Visibility

Problem: The checkout's `.csproj` files use explicit `Compile Include` entries. A compile proof is false if the new SHINOBU_111 runtime/editor files are not visible to the generated project surface.

Solution: Verified the current generated project surface includes `SaveStateMerkleTree.cs`, `H8BinaryWorldPager.cs`, and `VoxelDeltaCompressionArchitecture.cs` in `Hecton8.Core.csproj`, plus `VoxelSaveTunerWindow.cs` in `Hecton8.Editor.csproj`. No `.csproj` ownership is claimed in this pass because the active git diff contains no SHINOBU_111 project-file changes.

Rejected Alternatives: Relying on Unity project regeneration or accepting a false-positive build. Regeneration is not guaranteed in the current terminal workflow; false proof is worse than no proof.

Scalability potential: No runtime behavior change. This protects integration proof quality across all hardware tiers.

Hardware Impact: 0 runtime us. Editor/build metadata only.

## Decision 10 - Sub-Agent Static Review Corrections

Problem: Static review found four deterministic hazards before compile: signed Morton bias overflow, RLE job trusting caller-clamped cell counts, exact-capacity RLE false fatal, and empty sectors being marked as pruned deltas.

Solution: Moved Morton bias math to `long` before clamp, clamped RLE block `end` to `RuntimeDensity.Length`, added an explicit overflow boolean only when a run cannot be written, and only sets `HeaderFlagPruned` when the prune counter is non-zero.

Rejected Alternatives: Treating the scheduler clamp as sufficient or relying on compile/Burst to expose logic defects. These are correctness faults, not syntax faults.

Scalability potential: Low tier gets fewer false corruption retries on sparse sectors; middle/high/ultra keep deterministic sector identity across huge maps.

Hardware Impact: Avoids fault-path retries and bad WAL flags. Static estimate: 2-8 us avoided per affected sector plus prevention of sector-hash corruption.

## Decision 11 - Version-Safe Burst Surface Hardening

Problem: Three expressions were syntactically compact but brittle across Unity.Mathematics/Burst versions: `math.clamp(long, long, long)` in Morton encoding, `math.max(uint, uint)` in Dear Lie fade duration, and an implicit `byte|uint` promotion in the LZ4 sequence read.

Solution: Replaced the signed Morton clamp with explicit long branches, replaced the unsigned duration denominator with a direct zero guard, and cast the first LZ4 byte to `uint` before bitwise OR.

Rejected Alternatives: Waiting for `dotnet build` or Burst to reject version-specific overloads. That would waste a compile attempt while the CPU guard is already blocking proof.

Scalability potential: No tier-specific behavior change. This protects the same deterministic byte stream from weak devices through ultra hardware.

Hardware Impact: 0 measurable runtime change; removes avoidable compile/Burst compatibility risk before the next verification pass.

## Decision 12 - WAL Handoff Through Contracts

Problem: The staged WAL helper accepted the concrete `H8BinaryWorldPager`, which would couple SHINOBU_111 code to pager implementation details instead of the save authority route.

Solution: Replaced the helper signature with `IAsyncPersistenceService` and call `TryEnqueueChunkPageWrite` using `H8WorldPagePayloadTypes.VoxelDeltaRle`. The pager remains owned by SavePersistence; voxel compression only submits a native payload through the contract.

Rejected Alternatives: Adding `using Hecton8.Core.Persistence.Paging` and binding directly to `H8BinaryWorldPager`. That would work syntactically but violates unidirectional contract routing.

Scalability potential: All hardware tiers use the same route; throttling remains in continuous compression/write-Hz math, not in direct pager access.

Hardware Impact: 0 runtime us; prevents compile-wall dependency spread and keeps future pager changes isolated from voxel compression.

## Decision 13 - Unity Import Stability

Problem: Newly added C# script meta files only contained GUIDs. Unity can tolerate or rewrite incomplete metadata, but that creates noisy import churn and risks GUID instability during multi-agent work.

Solution: Added standard `MonoImporter` blocks for `VoxelDeltaCompressionArchitecture.cs.meta` and `VoxelSaveTunerWindow.cs.meta`.

Rejected Alternatives: Letting Unity regenerate the script metadata during the next editor import. That would hide a deterministic asset hygiene issue behind editor side effects.

Scalability potential: No runtime behavior. Stable imports protect all tiers by keeping build artifacts deterministic.

Hardware Impact: 0 runtime us; reduces editor import churn during verification.

## Decision 14 - Runtime Vault Resolve Via Handles

Problem: The boot resolver requested Vault buffers directly as `NativeArray` views while the architectural contract requires `VaultBufferHandle`-based routing for generation safety.

Solution: Replaced the runtime SHINOBU_111 buffer resolver with `GetBufferHandle<T>(...).Resolve(vault)` through a single `ResolveVaultBuffer<T>` helper. The helper does not store handles as private persistent fields; it only resolves generation-checked views for the scheduled job chain. The editor tuning writes now also resolve through `VaultBufferHandle` for consistency.

Rejected Alternatives: Keeping direct `GetBuffer` calls because they are short, or caching `NativeArray` fields in a component. Direct calls weaken the stated proof; cached fields create stale-handle risk after Vault defrag.

Scalability potential: Same buffers serve low through ultra tiers. Larger staging or telemetry capacity can be requested by the save owner without changing the compression jobs.

Hardware Impact: Runtime overhead is cold boot/resolve only; hot compression jobs still receive raw contiguous `NativeArray` views.

## Decision 15 - Contract Namespace Compile Fix

Problem: Filtered Core compile reached SHINOBU_111 and reported `IAsyncPersistenceService` missing. The interface lives in namespace `Hecton8.Core` inside `GlobalRegistryContracts.cs`, while the voxel compression file only imported `Hecton8.Core.Contracts`.

Solution: Added `using Hecton8.Core;` and kept `Hecton8.Core.Contracts` for `H8WorldPagePayloadTypes`.

Rejected Alternatives: Reverting to concrete `H8BinaryWorldPager`, duplicating the interface, or moving contract definitions. Those would violate compile-wall routing or touch global contract ownership.

Scalability potential: No runtime behavior change. This preserves the contract route for all hardware tiers.

Hardware Impact: 0 runtime us; compile-surface correction only.

## Decision 16 - Compile Wall Boundary

Problem: The first unfiltered Core compile failed before SHINOBU_111 on a tracked World source file deleted from the working tree while still referenced by `Hecton8.Core.csproj`. A filtered diagnostic compile then reached SHINOBU_111 and found the namespace issue fixed in Decision 15, but the same compile surface remains blocked by foreign Visor, Optimization, Networking, and Power missing-type errors.

Solution: Do not restore or recreate the deleted World file and do not patch foreign Visor/Power/Networking/Optimization systems from the voxel save lane. Use the filtered compile only as evidence that the SHINOBU_111 namespace error no longer appears after the fix.

Rejected Alternatives: Reverting another agent's deleted World source, adding stubs for foreign DTOs/contracts, or globally editing generated project files to hide non-SHINOBU errors. Those would cross domain boundaries and corrupt ownership evidence.

Scalability potential: No runtime behavior change. This preserves owner-local first routing and prevents SHINOBU_111 from becoming a catch-all compile janitor.

Hardware Impact: 0 runtime us. Compile proof remains blocked by dependency; static source proof and filtered diagnostic proof are the current evidence ceiling.

## Decision 17 - Tuning DTO Must Drive Runtime Math

Problem: The editor facade and CSV parser produced `VoxelDeltaCompressionTuningDTO`, but the compression scheduler still used hardcoded prune and LZ4 effort defaults. That would pass a superficial UI task while leaving designers with a dead control surface.

Solution: Added `ResolveRuntimeTuning(NativeArray<VoxelDeltaCompressionTuningDTO>)`, sanitizing prune threshold, effort range, write Hz, I/O pressure bias, write latency, and byte budget without managed allocations. `ScheduleCompressionPipeline` now feeds tuning-derived `PruneThreshold01` and continuous LZ4 effort into the Burst jobs.

Rejected Alternatives: Keeping constants in the scheduler, or adding binary low/high disk modes. Constants make the tuner fake; binary modes violate the continuous quality mandate.

Scalability potential: Low devices slide toward low effort, larger probe stride, and pruning through the same curve. Middle devices stay near CSV defaults. High/Ultra raise hash coverage and match search density without changing code paths.

Hardware Impact: Runtime extra cost is a single DTO read and scalar sanitation before scheduling. Low-end benefit comes from real pressure-driven compression downgrade instead of a hardcoded effort floor; exact microseconds require profiler proof.

## Decision 18 - Telemetry And Heatmap Need Real Baseline Semantics

Problem: `CounterRawBytes` tracked RLE bytes, not dense voxel payload bytes, and `VoxelDeltaSectorStatsDTO.CompressedBytes` was never updated after LZ4. That made the 99% compression self-audit and editor heatmap mathematically misleading.

Solution: `VoxelDeltaRleFinalizeJob` now records dense baseline bytes as `cellCount * 3` for density/material/flags telemetry while preserving `VoxelDeltaHeaderDTO.UncompressedSize` as RLE source bytes for LZ4 decode. `VoxelLz4CompressionJob` and checksum now update sector compressed bytes, ratio, and flags. The scheduler now chains `VoxelDeltaTelemetryRecordJob` after WAL pack.

Rejected Alternatives: Reporting RLE bytes as raw bytes, or leaving telemetry recording to an unspecified future caller. Both would create false forensic data.

Scalability potential: Low/Middle/High/Ultra use identical telemetry layout. Higher tiers can show richer compression ratios from more aggressive LZ4; lower tiers show intentional disk-size tradeoff under pressure.

Hardware Impact: One extra 64-byte telemetry write and one 64-byte sector stats write per compressed sector. No managed allocation and no main-thread synchronization. Estimated overhead is below 2 us per sector on low-end silicon, pending profiler proof.

## Decision 19 - Post-Polish Compile Probe Boundary

Problem: The polish patch touched scheduler/job signatures and needed a syntax proof, but unfiltered Core compile is still blocked by foreign working-tree errors.

Solution: Guarded CPU/dotnet first, then used a temporary MSBuild filter only for the already-deleted foreign World source. The filtered compile reported 17 foreign errors and no SHINOBU_111 errors. The temp target was deleted after the probe.

Rejected Alternatives: Running unguarded build, restoring another agent's deleted file, or adding stubs for Visor/Optimization/Networking types.

Scalability potential: No runtime behavior change. This protects the compile wall and keeps SHINOBU_111 scoped to voxel save compression.

Hardware Impact: 0 runtime us. Verification ceiling remains filtered compile plus static scans until foreign blockers are fixed.

## Decision 20 - Mock Generator Must Be Opt-In

Problem: The deterministic `MockVoxelDeformationGeneratorJob` satisfied isolated stress testing, but the scheduler always ran it before RLE. In a production route that would overwrite real voxel density/material/flag buffers and save artificial noise as if the player modified terrain.

Solution: Added `injectMockDeformation=false` to `ScheduleCompressionPipeline`. The default path now encodes the supplied Vault buffers unchanged. Tests and profiling can explicitly opt in to the mock generator.

Rejected Alternatives: Keeping the mock always-on because no live voxel provider is wired yet, or deleting the mock job. Always-on corrupts production data; deleting the job violates Task 05 isolated throughput testing.

Scalability potential: Low/Middle/High/Ultra production paths now preserve real delta semantics. Mock stress can still scale modification probability by `GlobalQualityWeight` for profiling.

Hardware Impact: Default production path removes one unnecessary parallel write pass over the full voxel chunk. Static saving estimate for non-test saves: one 32^3 cell write sweep avoided, roughly tens of microseconds on low-end CPUs, pending profiler proof.

Verification: The first post-patch compile probe was correctly blocked by active `dotnet` PID 35356. A later guarded probe at CPU 27.1% with no active compiler produced 17 foreign errors and no SHINOBU_111 errors; temporary MSBuild filter was removed.

## Decision 21 - Mock Baseline Cannot Read Uninitialized Vault Bytes

Problem: `TryResolveVaultBuffers` correctly requests voxel staging with `NativeArrayOptions.UninitializedMemory`, but the opt-in mock generator previously read `BaselineDensity[index]` before any guarantee that the buffer had been populated. That makes isolated mock compression nondeterministic.

Solution: The mock generator now derives a deterministic baseline from the sector/frame/index seed, writes it into `BaselineDensity`, then writes the mutated runtime density. Production remains unaffected because mock injection is opt-in.

Rejected Alternatives: Clearing the baseline Vault buffer, or requiring tests to pre-fill it. Clearing wastes memory bandwidth; external pre-fill makes the mock less isolated.

Scalability potential: Mock stress remains continuous: quality weight changes mutation probability and delta amplitude while baseline stays deterministic across weak, middle, high, and ultra tiers.

Hardware Impact: Test-only path adds one baseline write per cell but removes nondeterministic input. Production cost is 0 because the mock is disabled by default.

Verification: Guarded filtered compile at CPU 9.4% with no active compiler produced the same 17 foreign errors and no SHINOBU_111 errors. Temporary MSBuild filter was removed.

Correction: After making the mock write `BaselineDensity`, removed the stale `[ReadOnly]` attribute from that NativeArray field. Guarded filtered compile at CPU 10.7% again reports the same 17 foreign errors and no SHINOBU_111 errors. This is a Unity safety-handle fix, not a syntax-only fix.

## Decision 22 - Async Latency Is A Completion Fact

Problem: `VoxelDeltaTelemetryRecordJob` could record bytes and compression ratio at schedule time, but real disk latency is only known after the async pager completes the write. Writing `0 ms` into the ring made Task 16 look implemented while hiding the only I/O spike number designers need.

Solution: Added `ScheduleDiskLatencyTelemetryPatch`, a Burst job that scans the fixed 300-entry telemetry ring for `SectorHash` and `SimulationFrame`, patches `DiskWriteLatencyMs`, and marks either `TelemetryFlagDiskLatencyPatched` or `TelemetryFlagDiskLatencySpike` against the tuning threshold. The method consumes caller-provided async completion latency and does not reference a concrete pager.

Rejected Alternatives: Polling `H8BinaryWorldPager` directly, storing a managed callback list, or leaving schedule-time `0 ms`. Concrete polling breaks the compile wall; managed callback state violates Zero-GC; fake zero latency destroys black-box value.

Scalability potential: Low and middle devices get honest write-spike detection for throttling and QA dumps. High/Ultra can raise byte budgets while still correlating disk latency to compressed payload size.

Hardware Impact: No hot compression cost. The patch job is a bounded 300-entry scan only after write completion, expected below 5 us on low-end CPUs, pending profiler proof.

Verification: Guarded filtered compile at CPU 40% with no active compiler produced 21 foreign errors and no SHINOBU_111 errors. Temporary MSBuild filter was removed.

## Decision 23 - CSV Profiles Need Domain Meaning, Not Only Knobs

Problem: The CSV parser accepted scalar compression knobs but not the requested biome/depth profile identity. That left designers unable to bind a compression profile to a world context without recompiling code or adding managed parsing.

Solution: Extended `VoxelDeltaCompressionTuningDTO` to explicit 64B with `DepthMinMeters` at offset 52, `DepthMaxMeters` at offset 56, and `_pad0` at offset 60. The zero-GC byte parser now accepts `biome,<ascii>` and hashes the lowercase biome token into `ProfileHash`, plus `depth_min_m` and `depth_max_m` scalar keys. `BinaryLayoutManifest` asserts the new offsets.

Rejected Alternatives: Using managed strings, keeping biome only in comments, or adding a separate variable-size profile record. Managed strings allocate; comments are not data; a variable record would complicate Burst hydration and WAL audit proof.

Scalability potential: Low/Middle/High/Ultra all consume the same continuous numeric profile. Biome/depth ranges provide designer routing without binary device tiers.

Hardware Impact: Profile hydration remains cold and allocation-free. Runtime compression cost is unchanged except for reading an already-resolved 64B tuning DTO.

Verification: Static scans confirm no `string.Split`, JSON, or managed text parser in the SHINOBU path. Guarded filtered compile reports no SHINOBU_111 errors; global build remains foreign-blocked.

## Decision 24 - Blackbox Dump Must Be Decodable

Problem: The 300-frame telemetry dump wrote only raw 64-byte entries. Without a magic, version, stride, cursor, reason flags, or ordered emission, the dump could not reliably reconstruct the last 300 frames after a disk-latency failure.

Solution: Added `VoxelDeltaTelemetryDumpHeaderDTO`, explicit 64B. `TryDumpTelemetryRing` now writes the header and then emits telemetry entries oldest-to-newest based on the ring cursor. Added cursor-aware latency spike dump helpers and spike-flag dump detection.

Rejected Alternatives: Keeping the raw blob because entries are fixed-size, or writing managed JSON metadata. A raw blob is forensic guesswork after wraparound; JSON violates the binary/zero-GC direction.

Scalability potential: Low devices get actionable MicroSD latency forensic dumps; high/ultra devices can correlate NVMe write latency with compression effort and payload size using the same ring layout.

Hardware Impact: Fault path only. Hot compression path cost is 0 us; dump path writes one 64B header plus bounded ring bytes.

## Decision 25 - LZ4 Tail Rules Are Compatibility, Not Decoration

Problem: The custom LZ4-compatible encoder produced standard-looking sequences but did not enforce the LZ4 block end constraints: final 5 bytes must remain literals and a match must not start in the final 12-byte region. That makes native `LZ4_decompress_safe` compatibility unproven.

Solution: Added `Lz4LastLiterals=5` and `Lz4MfLimit=12`. Match search now ends before the forbidden tail region, and match extension stops before the final literal tail. If compression cannot beat raw bytes, the path stores raw RLE bytes with the raw flag.

Rejected Alternatives: Renaming the codec to custom-only or claiming compatibility without a tail-rule proof. Custom-only would break Task 07 intent; false compatibility would create corrupt WAL loads.

Scalability potential: Low pressure still stores raw/RLE when compression is not worth CPU. High/Ultra keep denser LZ4 search while respecting decode compatibility.

Hardware Impact: Possible tiny compression-ratio loss from reserved tail literals. Benefit is avoiding bad decode/retry paths and sector fallback stalls.

## Decision 26 - Legacy Voxel Processor Hot Registry Cache

Problem: The legacy `VoxelDeltaProcessor.Tick()` path called save registration and queued-carve helpers that could reach `GlobalRegistry.Save`, `GlobalRegistry.ScalabilityTier`, and `GlobalRegistry.SimulationBucketer` every frame.

Solution: Cache `IDataVault`, `ISimulationBucketer`, `ISaveService`, and `HectonQualityTier` in `OnEnable`. Runtime drain/register helpers now consume cached references only. This removes hot-path service discovery without changing save/compression ownership.

Rejected Alternatives: Leaving the old path because the new WAL compressor is clean, or adding a broad service-rebind framework from the voxel lane. Ignoring it leaves technical rot visible; broad rebind infrastructure is not SHINOBU_111 ownership.

Scalability potential: All tiers avoid per-frame registry lookups. Tier value remains cached until a proper typed rebound signal is wired by the global-authority owner.

Hardware Impact: Static microsecond estimate is small per frame, but it removes branch/cache noise from the legacy carve Tick lane.

Residual Debt: `ChunkDeltaState` still owns old per-chunk persistent NativeArrays. Migrating that state to Vault handles safely requires a dedicated legacy carve-state migration and save/load replay proof; it was not rewritten blindly in this WAL compression pass.

## Decision 27 - Designer CSV And Editor Facade Must Survive Real Edits

Problem: The CSV parser accepted only the clean sample format, and the editor telemetry reads mixed direct `TryGetBuffer` with handle-based tuning writes.

Solution: CSV parsing now strips UTF-8 BOM on the first key, accepts optional `+`, supports exponent notation through deterministic multiply loops, and cuts inline comments before float parsing. The editor facade resolves telemetry/cursor/sector stats through `TryGetBufferHandle(...).Resolve(vault)`.

Rejected Alternatives: Requiring perfectly formatted CSV files, or leaving editor reads on direct buffer views. Designer files drift; mixed access weakens generation/stale-handle evidence.

Scalability potential: Biome/depth/tuning profiles remain continuous and editable across low, middle, high, and ultra device targets.

Hardware Impact: CSV and editor work are cold/editor paths. Hot compression path remains allocation-free.

Verification: Guarded filtered compile after these patches reports 22 foreign errors and no SHINOBU_111 errors. Foreign blockers are Visor reconstruction contracts and Somatic VR comfort DTOs; the temporary build filter was removed.
