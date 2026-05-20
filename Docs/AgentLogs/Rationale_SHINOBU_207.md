# Rationale SHINOBU_207

Status: PENDING VERIFICATION

Problem: Flat sorted arrays in MMF lookup (`while (low <= high)`) force midpoint jumps across the mapped file and waste cache locality during UI/PDA string scans.
Solution: Replace the hot lookup path with an offline-built, 64-byte cache-line B-Tree. Runtime traversal uses explicit unmanaged node fields, stack-local cursors, and raw offsets.
Rejected Alternatives: Keeping flat arrays plus OS/MMF prefetch was rejected because it cannot bound L1/L2 locality; managed dictionaries were rejected because they allocate, require hash-table memory, and are not MMF-native.
Scalability potential: Low uses the same B-Tree topology with prefetch throttled down; Middle/High/Ultra keep topology identical and spend saved CPU on richer PDA/text presentation outside search truth.
Hardware Impact: Expected low-end i3/MX350 gain is fewer random L2/DRAM misses during bulk string lookup; exact microseconds remain PENDING until benchmark/profiler evidence.

Problem: B-Tree node ABI can silently drift if it is expressed as sequential structs or managed arrays.
Solution: Define `BTreeNodeDTO` with `[StructLayout(LayoutKind.Explicit, Size = 64)]`, seven contiguous keys, eight contiguous child/value offsets, and one metadata word.
Rejected Alternatives: Variable-width nodes and packed records were rejected because they break ARM64 alignment and make child selection unpredictable.
Scalability potential: Low/Middle/High/Ultra all use identical ABI; quality affects prefetch aggressiveness, not correctness or tree shape.
Hardware Impact: One node read equals one cache line; no integer crosses a hardware word boundary.

Problem: Unity assemblies in this checkout expose no `UnsafeUtility.PrefetchMemory` or `Hint.Likely` symbol in static search, but Task 08 requires a prefetch control path.
Solution: Implemented deterministic cache-touch prefetch: traversal reads the first uint of the next candidate node when `GlobalQualityWeight` permits. The value is mixed into a local salt to prevent easy elimination while leaving lookup truth unchanged.
Rejected Alternatives: Reflection-based calls and speculative platform-specific intrinsics were rejected because missing API symbols would break compilation. Binary low/high quality switches were rejected because topology must remain continuous.
Scalability potential: Low uses stride 4 or disabled touch under thermal pressure; Middle lowers stride; High/Ultra touch every depth step and spend saved CPU on presentation outside lookup.
Hardware Impact: Expected i3/MX350 gain is reduced cold child-node latency when the memory controller is not stressed; exact Unity microseconds remain unverified because build/profiler were blocked by CPU guard.

Problem: H8LR header reserved field must stay zero, so there is no safe bit for a B-Tree-present flag without breaking the existing file contract.
Solution: H8LR now infers the B-Tree section from the 64-byte aligned gap between record table end and first payload offset. `Tools/LorePacker.py` writes that section and `PdaH8lrLoreStore` refuses old flat-only blobs.
Rejected Alternatives: Reusing header reserved bits was rejected because parser/test contracts require zero. Keeping binary-search fallback was rejected because Task 01 requires eradication, not a second path.
Scalability potential: Low/Middle/High/Ultra use identical H8LR topology; quality only affects traversal prefetch touch.
Hardware Impact: For the current two-entry H8LR blob, tree bytes are 64 and payload starts at 128. Gain is structural readiness; meaningful L2 savings appear when lore count grows.

Problem: Runtime `StaticDataStore` built a `NativeParallelHashMap` from MMF lookup rows, duplicating file-resident data and allocating persistent native memory at open.
Solution: Removed the runtime lookup map path and resolves record ordinals through the file-resident B-Tree, then reads the existing lookup row by ordinal.
Rejected Alternatives: Keeping the hash map as compatibility fallback was rejected because it hides missing B-Tree data and leaves a second lookup truth.
Scalability potential: Low devices avoid extra native-memory footprint; Ultra keeps identical truth and uses budget elsewhere.
Hardware Impact: Removes one persistent map allocation and hash-table indirection. Exact microseconds pending Unity build/profiler.

Problem: Python benchmark over 16,384 synthetic records showed B-Tree theoretical cache-line savings but slower wall-clock in CPython due interpreter/struct unpack overhead.
Solution: Report records both numbers: `Docs/Reports/MEMORY_OPTIMIZATION_REPORT.json` keeps the negative Python ns delta and the topology-derived cache-line delta. This is static scanner evidence, not Burst profiler proof.
Rejected Alternatives: Faking a positive ns delta was rejected. Running Unity Burst benchmark was blocked by CPU guard.
Scalability potential: Low/Middle/High/Ultra benefit from fewer random table probes in compiled Burst; proof still requires Unity profiler once build is legal.
Hardware Impact: Theoretical saving in the scanner is 8 cache lines / 512 bytes per lookup on 16,384 records; CPython timing is not hardware proof.

Problem: B-Tree metrics were packed into generic static-data telemetry, which prevented direct cache-miss forensic replay and did not expose a POST_SIMULATION flush contract.
Solution: Added 64-byte `BTreeTelemetryAccumulatorDTO`, 64-byte `BTreeTelemetryEntry`, Vault IDs `72070`/`72071`/`72072`, `FlushBTreeTelemetryPostSimulationJob`, and `H8BTreeTelemetryDump.Write`. Static/Babel readers accumulate depth, processed keys, prefetch touches, slowest ns, and last lookup into the specialized lane.
Rejected Alternatives: Reusing only `StaticDataTelemetryRing` was rejected because it hides B-Tree-specific evidence. Adding a dependency on `SystemDispatcher` internals was rejected because this domain must not mutate core scheduling topology without compile proof.
Scalability potential: Low records the same fixed 19.2 KiB ring; Middle/High/Ultra retain identical telemetry but can use the X-Ray window to spend saved lookup time on richer UI/audio presentation.
Hardware Impact: Low-end i3/MX350 cost is one 64-byte accumulator and one 64-byte ring write per sample path; slow >0.5ms batches dump a 19.2 KiB ring plus header for postmortem.

Problem: Designers need hardware-specific prefetch limits without recompiling C#, but managed CSV parsing would introduce string/int parsing allocations.
Solution: Added `BTreeTuningCsvParser.TryParse(ReadOnlySpan<byte>, NativeArray<BTreeTuningProfileDTO>, ...)` and `Data/Balance/btree_tuning_profiles.csv`. Parser hashes profile names via FNV-1a byte spans, parses integer/decimal cells manually, and writes 64-byte DTOs into Vault buffer `72073`.
Rejected Alternatives: `string.Split`, `int.Parse`, `float.Parse`, and managed dictionaries were rejected because the cold bridge still feeds runtime native state and must stay deterministic.
Scalability potential: Low profile uses prefetch aggression 0.05 and batch 16; Middle uses 0.45 and batch 32; High/Ultra uses 1.00 and batch 64 without changing tree topology.
Hardware Impact: Low-end devices can suppress speculative touch pressure; high-end devices keep aggressive warm-cache behavior while preserving the same lookup truth.

Problem: The B-Tree topology was invisible to leads, making cache-locality claims hard to audit without raw memory tools.
Solution: Added `CacheBTreeTopologyXRayWindow` UI Toolkit facade. It loads static/Babel/H8LR payloads, resolves the tree section, draws explicit node topology, displays a telemetry waterfall from Vault, and runs `TraceBTreeTraversalJob` synchronously for a raw key.
Rejected Alternatives: Text-only reports and fake topology diagrams were rejected because they do not prove byte offsets or touched nodes. Runtime gizmos were rejected because this is editor diagnosis, not gameplay.
Scalability potential: Low/Middle/High/Ultra all draw the same physical tree; quality only changes prefetch behavior. X-Ray makes the continuous quality effect inspectable through telemetry, not hidden in code.
Hardware Impact: No player-frame cost. Editor-only cold allocations are bounded arrays; the runtime trace job remains unmanaged and bounded by 32 depths.

Problem: `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` still classified `Data/Lore/Encyclopedia.h8bin` as a 41920-byte script/tool-only H8LR blob with no dedicated runtime reader, while the current source and generated file now contain a 64-byte B-Tree gap and `PdaH8lrLoreStore` reader.
Solution: Updated only the H8LR ledger rows/backlog entry to `43536` bytes and `READER_PRESENT_PENDING_UNITY_PROOF`. The ledger now states the B-Tree node lives at offset 64 and that runtime acceptance still lacks Unity import, MMF map, GC, and profiler proof.
Rejected Alternatives: Leaving stale docs was rejected because user mandate requires `/Docs` actuality. Claiming shipped/runtime-ready was rejected because the CPU guard still prevents Unity compile/profiler evidence.
Scalability potential: Low/Middle/High/Ultra all consume the same immutable H8LR tree; the continuous prefetch/tuning path remains the only quality-weight variable.
Hardware Impact: No runtime code cost from the doc change. It removes integration ambiguity and prevents a second H8LR conversion task from being assigned against already-present source.

Problem: The explicit `X86.Sse2` scan path was flagged as a possible static compile risk because Burst intrinsic method definitions are not all present as text in `Library/PackageCache`.
Solution: Rechecked package/project usage. Unity Collections uses `X86.Avx2.IsAvx2Supported`, `X86.Avx.mm256_set1_epi32`, and `X86.Sse.SHUFFLE`; local project uses `X86.Sse.IsSseSupported`. Kept the explicit Sse2 path pending compiler proof rather than weakening Task 06 to scalar-only source.
Rejected Alternatives: Removing SIMD intrinsics preemptively was rejected because Task 06 explicitly requires a Burst node scanning kernel. Declaring it compiler-proven was rejected because Unity build is still blocked by CPU guard.
Scalability potential: Low devices fall through to the same bounded uint4/scalar-safe behavior when Sse2 is unavailable; High/Ultra keep direct vector compare/mask behavior where the backend supports it.
Hardware Impact: No measured microsecond claim. The intended compiled path compares seven keys in two 128-bit chunks; exact Burst codegen remains pending.

Problem: New B-Tree telemetry Vault buffers were valid for postmortem ownership but lacked a dedicated Global Authority route-card artifact.
Solution: Added `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_BTREE_TELEMETRY_SHINOBU_207.md` documenting BufferIDs `72070`..`72073`, producer/consumer phases, capacity, stale-handle behavior, shutdown rule, failure mode, and proof debt. Review result is `YELLOW`, not `GREEN`, because runtime proof is missing.
Rejected Alternatives: Treating BufferID constants as self-documenting was rejected by `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`. Marking the route `GREEN` was rejected because Unity compile/profiler/GC evidence is absent.
Scalability potential: Low uses the same fixed-size ring with no optional visual consumers; Middle/High/Ultra may read richer X-Ray/editor telemetry without changing runtime truth.
Hardware Impact: Runtime forensic memory is 19.2 KiB ring + 4 B cursor + 64 B accumulator; cold tuning profiles add 1024 B. All are Vault-owned. No new player-frame allocation.

Problem: Current `Data/Balance/Baked/H8StaticData.bin` and `Data/Balance/Baked/Babel_Dictionary.h8bin` were still old flat lookup payloads with flags `0x1`; the new runtime readers require `CacheBTreeFlag` and would fail closed before Unity could rebake.
Solution: Added `Tools/UpgradeStaticBTreePayloads.py` to insert the same 64-byte B-Tree topology into existing baked bytes, shift absolute offsets, recompute Hecton CRC32, sync the static header's Babel CRC, and refresh manifests through temp-file replace writes. The tool validates every B-Tree lookup for current static/Babel rows under `--check`.
Rejected Alternatives: Leaving current files stale was rejected because source-only implementation would break default readers. Hand-editing bytes was rejected; the upgrade is reproducible through a checked-in Python tool and the C# baker also emits B-Trees for future bakes.
Scalability potential: Low/Middle/High/Ultra use identical file topology; quality only adjusts prefetch/tuning behavior. On high hardware the saved lookup budget can go to richer PDA/diegetic presentation.
Hardware Impact: Static file grew from 896 to 1136 bytes; Babel grew from 1296 to 1616 bytes. Added B-Tree bytes are 192 and 320 respectively, buying cache-local lookup at negligible disk cost.

Problem: After the B-Tree resolved a static-data ordinal, the 48-byte balance payload records still started at only 16-byte aligned offsets. Current offsets such as 560, 608, 656 caused some resolved records to straddle 64-byte cache lines, partially negating the cache-conscious lookup topology.
Solution: Kept the 48-byte DTO ABI intact but changed the static-data baker/upgrader to align every payload record start to 64 bytes after the B-Tree section. `StaticDataStore` now rejects non-64-byte payload offsets. Current `H8StaticData.bin` was regenerated to 1328 bytes with payload CRC `0x598EF439`; every lookup entry offset is `offset % 64 == 0`.
Rejected Alternatives: Padding each static record DTO to 64 bytes was rejected as a broader ABI change. Leaving records at 16-byte offsets was rejected because the B-Tree would hand the CPU a cache-local ordinal and then immediately risk a split-line payload fetch.
Scalability potential: Low hardware avoids a second cache-line fetch on individual 48-byte payload reads; high/ultra keeps the same immutable file topology and can spend saved memory stalls on richer presentation outside data lookup.
Hardware Impact: Static file grew from 1136 to 1328 bytes, adding 192 bytes of inter-record padding for 13 records. Each 48-byte payload now fits within one 64-byte cache line from its recorded offset.

Problem: The assignment names `.h8loc`, but the current repository scan found no `.h8loc` payload files to upgrade on disk.
Solution: Kept the runtime B-Tree helper/file-loader semantics generic for `.h8bin` and `.h8loc`, and the X-Ray file picker accepts `.h8loc`. No absent payload was fabricated.
Rejected Alternatives: Creating a fake `.h8loc` just to satisfy wording was rejected because it would add disk ballast and false runtime evidence.
Scalability potential: Any future `.h8loc` owner can use the same 64-byte node ABI and continuous prefetch stride without a new lookup algorithm.
Hardware Impact: No current disk or runtime cost because no `.h8loc` file exists in `Data` or `Assets/_Project`.

Problem: Loop 7 XML re-read exposed editor-time managed hash residue in `H8DataBaker`: `HashSet<uint>` was still used as the duplicate record gate even though Task 02 rejects managed dictionary/hash-table staging in the B-Tree bake lane.
Solution: Removed `HashSet<uint>` and replaced it with an index-based scan over the already-owned pending record list before appending the next row. This keeps duplicate detection deterministic and removes the extra managed hash-table allocation from the baker path without adding a new dependency or cross-domain API.
Rejected Alternatives: Keeping `HashSet<uint>` because it is editor-only was rejected by the XML mandate. Adding a `NativeParallelHashMap` to the editor baker was rejected for this narrow dataset because it would introduce another native lifetime and disposal surface where the existing contiguous record list already contains all required facts.
Scalability potential: Low/Middle/High/Ultra runtime topology is unchanged; cold bake remains deterministic and produces identical B-Tree payloads. For larger future localization sets, the next escalation is an unmanaged radix/sort duplicate pass, not a managed hash table.
Hardware Impact: Removes one cold managed hash-table allocation and bucket walk. Runtime frame cost is unchanged; the gain is lower bake-time GC pressure and cleaner source compliance.

Problem: Task 07 explicitly requires deterministic traversal, but the previous source pass left the B-Tree traversal jobs on `FloatMode.Fast`. This entry is superseded by the later Loop 11 source re-read, which found the earlier change had not actually landed in source.
Solution: Loop 11 corrected `TraverseBTreeJob`, `DispatchBulkBTreeSearchJob`, `TraceBTreeTraversalJob`, and `BabelBTreeSearchKernel` to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`. Scan-only, telemetry, mock, and byte-copy jobs keep `FloatMode.Fast`.
Rejected Alternatives: Documenting deterministic behavior without changing the attributes was rejected. Changing every unrelated job to deterministic was rejected because the mandate is traversal-specific and would dilute the global Burst directive discipline.
Scalability potential: The same B-Tree path runs on weak, middle, high, and ultra hardware. `GlobalQualityWeight` still only changes prefetch stride, not lookup truth.
Hardware Impact: Integer traversal dominates. Expected impact of deterministic float mode is negligible because the only float in the traversal kernel is the bounded quality-weight prefetch throttle; proof remains pending until Unity Burst compile/profiler is legal.

Problem: Task 12 required a spatial Morton B-Tree variant, but the prior source only exposed `HashAupDouble3ToMorton64`; it did not provide a 64-bit node ABI, offline compiler, or runtime range query route.
Solution: Added `MortonBTreeNodeDTO` as one 64-byte cache line with four `ulong` Morton separators, five child/value lanes, metadata, and explicit padding. Added `SpatialMortonBTreeRecordDTO`, `SpatialMortonLevelEntryDTO`, `SpatialMortonBTreeCompiler.TryBuild`, `TryFindMortonValue`, bounded non-recursive `TryFindMortonRangeFirstValue`, and deterministic `SpatialMortonRangeQueryJob`.
Rejected Alternatives: Reusing the 32-bit text B-Tree for spatial logs was rejected because it would fold 64-bit Morton locality into a collision-prone hash. Octree/KD-tree ownership was rejected because it creates pointer-heavy topology and worse MMF/cache locality. Managed editor arrays/dictionaries for spatial build state were rejected; the compiler consumes caller-owned `NativeArray` buffers.
Scalability potential: Low/Middle/High/Ultra use the same 64-byte Morton node topology. Low hardware can query coarse Morton ranges or fewer records upstream; high/ultra can use tighter ranges and richer spatial forensic overlays without changing tree truth.
Hardware Impact: Spatial node fetch is exactly one L1 cache line. Morton records/scratch rows are 16 bytes and 8-byte aligned. Runtime range query is bounded by a fixed stack of `MaxTraversalDepth * 5` offsets and has no recursion or heap allocation.

Problem: Task 05 requires a heavy synthetic B-Tree for Burst Inspector/profiling, but the previous `GenerateMockBTreeJob` only emitted eight leaves and one root.
Solution: Expanded the job to derive topology from caller-owned byte capacity. With enough buffer space it now emits 512 leaves, 64 level-1 internal nodes, 8 level-2 internal nodes, and one root, covering 3584 sequential hashes. Smaller buffers scale down while preserving a valid root-last topology.
Rejected Alternatives: Allocating a `NativeArray<byte>` inside the job was rejected; job allocation would violate the zero-GC/zero-private-ownership model. Keeping the tiny stub was rejected because it cannot stress cache traversal depth.
Scalability potential: Low test hardware can pass a smaller buffer and still get a valid topology; high/ultra profiling can allocate the full synthetic tree and hammer thousands of lookup keys without waiting for importer output.
Hardware Impact: Full mock tree is 585 nodes * 64 bytes = 37440 bytes, deliberately larger than L1 on many targets to expose traversal/cache behavior under pressure.

Problem: Task 04 asks for `UnsafeUtility.SizeOf` and `UnsafeUtility.AlignOf` proof, but the baker gate only checked byte size.
Solution: Added minimum natural alignment checks for `BTreeNodeDTO`, `MortonBTreeNodeDTO`, `SpatialMortonBTreeRecordDTO`, and `SpatialMortonLevelEntryDTO`. File section offsets remain forced to 64-byte boundaries, while `AlignOf` now catches ABI drift that would under-align wide Morton fields.
Rejected Alternatives: Treating `SizeOf == 64` as complete ARM64 proof was rejected because a 64-byte struct with weak natural alignment can still hide wide-field access hazards.
Scalability potential: Same ABI on low, middle, high, and ultra hardware. Alignment proof protects mobile ARM64 first and does not weaken desktop x86 behavior.
Hardware Impact: No runtime cost; cold baker fails before emitting malformed MMF payloads.

Problem: The status log claimed deterministic B-Tree traversal, but a source re-read showed `TraverseBTreeJob`, `DispatchBulkBTreeSearchJob`, `TraceBTreeTraversalJob`, and `BabelBTreeSearchKernel` still using `FloatMode.Fast`.
Solution: Changed only traversal/search jobs to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`. Kept scan-only, telemetry, mock, endian validation, XOR decrypt, and count jobs on `FloatMode.Fast` because they do not decide lookup path authority.
Rejected Alternatives: Changing documentation only was rejected as false evidence. Changing every Burst job in the files to deterministic was rejected because the assignment targets traversal determinism and unnecessary deterministic mode can tax unrelated cold/math-light jobs.
Scalability potential: Low, middle, high, and ultra hardware now execute the same deterministic lookup path; `GlobalQualityWeight` still changes only speculative prefetch stride, never topology or result selection.
Hardware Impact: Integer traversal dominates, so the expected cost of deterministic float mode is negligible. It closes a correctness gap before profiler evidence; exact microseconds remain pending behind compile-wall proof.

Problem: A targeted C# build was finally allowed by the CPU guard but failed before SHINOBU_207 proof could be isolated.
Solution: Recorded the build as a foreign dependency wall instead of re-running blindly: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed after roughly 42.5 seconds with 188 errors rooted in missing non-SHINOBU_207 types/namespaces such as `Hecton8.Logistics.Grid`, `FaunaKinematicsRuntime`, `HectonFluidEngine`, `SoundEmissionSignal`, `H8BinaryWorldPager`, `VRAMMonitor`, and `AssetLifecycleGovernor`.
Rejected Alternatives: Reverting B-Tree changes was rejected because the first errors are outside this domain and reverting would destroy implemented requirements without fixing the foreign wall. Launching another build immediately was rejected because the latest guard reports CPU 100.
Scalability potential: No algorithmic change. The compile wall blocks proof only; the B-Tree path remains continuous across hardware tiers.
Hardware Impact: No runtime gain. Evidence quality improved: Task 20 is explicitly blocked on dependency wall plus CPU guard, not silently pending.

Problem: The first static benchmark compared packed-byte B-Tree traversal against a Python `list[tuple]` flat search, which undercounted the legacy MMF read cost and made the metric asymmetric.
Solution: Updated `Tools/Cache_Miss_Eradication_Scanner.py` so legacy binary search reads a packed 16-byte flat table through `struct.unpack_from`, matching the byte-blob access model used by the B-Tree scanner. The report now records `flatRecordBytes` and `flatTableBytes`.
Rejected Alternatives: Leaving the faster Python tuple path was rejected because it was not representative of MMF file access. Faking a positive nanosecond delta was rejected; the report still shows CPython B-Tree timing as slightly slower while preserving the topology-derived cache-line saving.
Scalability potential: No runtime topology change. Low through ultra hardware still uses the same B-Tree; the scanner now gives a cleaner static evidence artifact until Unity Burst profiling is unblocked.
Hardware Impact: Latest static scanner after Loop 15: packed-byte binary `16352.71 ns`, packed-byte B-Tree `18395.75 ns`, theoretical saving `8.00` cache lines / `512.06` bytes per lookup. This is still not Unity/Burst proof.

Problem: The B-Tree reader still had hostile-offset arithmetic surfaces: `TryResolveTree` cast an aligned `long` tree offset back to `uint`, and traversal guards used `offset + 64` comparisons. A malformed MMF header could wrap those additions before the fail-closed check.
Solution: Reworked section resolution to use `ulong` arithmetic and reject aligned offsets above `uint.MaxValue`. Added shared B-Tree range/node validators and patched normal, traced, and Morton traversal paths to compare `offset <= treeEndOffset - 64` after proving `treeEndOffset >= 64`.
Rejected Alternatives: Trusting caller-side file-length validation was rejected because MMF readers are a hostile binary boundary. Adding exceptions was rejected because Burst jobs need branch-return failure, not managed control flow.
Scalability potential: Low, middle, high, and ultra hardware use the same fail-closed topology. `GlobalQualityWeight` still controls only prefetch cadence; malformed data cannot select a different path.
Hardware Impact: No positive microsecond claim. It removes undefined/wrapped pointer math risk before profiler proof and keeps invalid child offsets from being counted as successful speculative cache touches.

Problem: `GenerateMockBTreeJob` cleared every byte in the caller-provided output buffer before writing nodes, contradicting the zero-init bypass requirement for a job whose emitted nodes are already written as full 64-byte structs.
Solution: Removed the full `OutputBytes` clear. Each node is created from `default` and assigned as a complete `BTreeNodeDTO`, while metadata lanes are explicitly initialized when the metadata buffer exists.
Rejected Alternatives: Keeping the clear as harmless test hygiene was rejected because the job can receive a large shared profiling buffer and only owns the emitted tree region.
Scalability potential: Low devices can run smaller mock buffers without paying a whole-buffer memset; high/ultra profiling still emits the full 585-node synthetic tree.
Hardware Impact: Saves one cold linear write over `OutputBytes.Length` per mock-generation run. Runtime lookup cost is unchanged; this is profiling/test infrastructure cleanup.

Problem: A second source re-read found that the status/log claim about deterministic traversal had drifted again: key B-Tree traversal jobs and `BabelBTreeSearchKernel` still had `FloatMode.Fast` in actual source.
Solution: Patched `TraverseBTreeJob`, `DispatchBulkBTreeSearchJob`, `TraceBTreeTraversalJob`, `SpatialMortonRangeQueryJob`, and `BabelBTreeSearchKernel` to `FloatMode.Deterministic`. Left non-authoritative scan, telemetry, mock generation, endian/decrypt, and counting jobs on `FloatMode.Fast`.
Rejected Alternatives: Trusting the prior status entry was rejected because the code is the only proof artifact that matters. Flipping all Burst jobs to deterministic was rejected because the mandate is traversal/search determinism, and unrelated cold jobs should keep the standard Fast directive.
Scalability potential: The deterministic lookup result is identical across low, middle, high, and ultra hardware. Quality weight still modulates only prefetch cadence, not result selection.
Hardware Impact: Expected runtime delta is negligible because traversal is integer-heavy; this is correctness/rollback-adjacent determinism hardening. Unity/Burst proof remains blocked; latest CPU guard returned 67 with no `dotnet`/`csc`.

Problem: Loop 16 proved that the Loop 14/15 deterministic entries were still stale: actual C# had `FloatMode.Fast` on every B-Tree search/traversal job and `BabelBTreeSearchKernel`. This made the status file stronger than the code.
Solution: Patched the actual source again and expanded the boundary to include `ScanBTreeNodeJob` because it is the core search kernel. Added a source-contract gate to `Tools/Cache_Miss_Eradication_Scanner.py`; it now fails if any named B-Tree search job lacks `FloatMode.Deterministic` or if `BTreeNodeDTO` loses explicit 64-byte layout. The JSON report now records those booleans under `sourceContracts`.
Rejected Alternatives: Leaving this as a manual `rg` checklist was rejected because the same drift already happened twice. Changing endianness/decrypt/mock count jobs to deterministic was rejected because they are not lookup path authorities and the global Burst directive remains Fast for non-authoritative jobs.
Scalability potential: Low, middle, high, and ultra hardware now execute identical deterministic search code paths. The only continuous quality variable remains prefetch touch cadence; topology and result selection do not scale or branch by tier.
Hardware Impact: Latest scanner after source-contract gate and full verifier rerun: packed-byte binary `18239.09 ns`, packed-byte B-Tree `20238.74 ns`, theoretical saving `8.00` cache lines / `512.06` bytes per lookup. This is static evidence; Unity/Burst profiler proof is still blocked by CPU guard / foreign dependency wall.

Problem: Loop 17 found a remaining fallback lookup residue in `PDAEncyclopediaStreamer.ExtractLoreSpanJob`. The path was mock-only, but it still performed a flat binary-search style walk over the mock `Index`, contradicting the SHINOBU_207 "no flat MMF lookup in contour" rule.
Solution: Changed the mock fallback contract to deterministic ordinal decode: `ordinal = EntryHash - MockBaseHash`, then bounds-check against `MockEntryCount` and `Index.Length`, then verify `row.StringHash == EntryHash` before returning the slice. Added `sourceContracts.pdaMockFlatIndexScanRemoved` to the cache scanner so this residue cannot silently return.
Rejected Alternatives: Keeping binary search because the data is mock-only was rejected because fallback code becomes production emergency code under importer failure. Building a second B-Tree for the deterministic mock lane was rejected for this small sequential hash generator because ordinal decode is the exact mathematical inverse of mock key generation and creates no new Vault lane.
Scalability potential: Low hardware pays O(1) for emergency PDA mock lookup; middle/high/ultra retain the same immutable real B-Tree path and can use saved stalls for richer diegetic PDA presentation. No binary low/high quality branch was introduced.
Hardware Impact: Removes O(log N) or O(N) fallback mock probing from a one-row UI extraction path and replaces it with one subtraction, two bounds checks, one row read, and one hash equality. Latest scanner after the gate: packed-byte binary `22043.97 ns`, packed-byte B-Tree `17573.45 ns`, theoretical saving `8.00` cache lines / `512.06` bytes per lookup; this remains static Python evidence, not Unity/Burst profiler proof.

Problem: `Tools/UpgradeStaticBTreePayloads.py --check` rewrote byte-identical manifests on every validation pass. On this workstation the byte-identical `Babel_Dictionary.manifest.json.tmp` could be created but not deleted/replaced inside the sandbox ACL, producing a false verifier failure unrelated to payload topology.
Solution: Changed `atomic_write_bytes` to compare target size and bytes before writing or replacing. If the target already matches, the function returns without touching the temp path. The stale byte-identical temp was removed with explicit escalation after normal sandbox deletion was denied.
Rejected Alternatives: Ignoring the failed verifier was rejected because the batch protocol requires evidence, not excuses. Rewriting manifests through a new filename was rejected because it would create more generated churn instead of making validation idempotent.
Scalability potential: No runtime effect. Cold tooling now avoids redundant disk writes on weak laptops and CI agents while preserving the same static/Babel B-Tree validation on high-end machines.
Hardware Impact: Saves one cold JSON temp write and replace attempt per unchanged manifest check. Runtime microseconds unchanged; the gain is deterministic tool repeatability under file ACL pressure.

Problem: The cache scanner still left several SHINOBU_207 source regressions to manual grep: a flat binary-search loop, `.BinarySearch` API use, managed lookup containers, `Pack=1`, wrapped `offset + 64`, or the old full mock output clear could return without making `Docs/Reports/MEMORY_OPTIMIZATION_REPORT.json` fail.
Solution: Added `SOURCE_CONTRACT_FILES`, named `RESIDUE_PATTERNS`, and `validate_no_source_residue()` to `Tools/Cache_Miss_Eradication_Scanner.py`. The scanner now reads the six B-Tree contour source files and fails with file/line/snippet evidence if any forbidden pattern returns. The report records `sourceContracts.sourceResidueClean` booleans for the six gates.
Rejected Alternatives: Keeping this as an operator-run `rg` checklist was rejected because source drift already happened in earlier deterministic-attribute loops. A broad whole-project ban was rejected because known lifecycle/editor code still contains legitimate `GlobalRegistry`, `Time.frameCount`, and cold `UnsafeUtility.MemClear` noise outside the hot MMF lookup authority.
Scalability potential: Low devices keep the same B-Tree lookup truth and the proof gate prevents fallback code from regressing to random MMF probes. Middle/high/ultra can spend lookup savings on richer PDA/lore presentation without introducing a second lookup route.
Hardware Impact: Latest scanner after the gate: packed-byte binary `26167.87 ns`, packed-byte B-Tree `26089.42 ns`, static Python delta `78.45 ns saved`, theoretical saving `8.00` cache lines / `512.06` bytes per lookup. This is still source/static proof, not Unity Burst profiler proof.
