# Rationale_SHINOBU_204

Status: PARTIAL PASS - PACK/GLOBALREGISTRY SEQUENTIAL ZEROED - BUILD BLOCKED BY DEPENDENCY WALL

## Decision 001 - Scope The Alignment Pass

Problem: The prompt demands project-wide layout hardening, but 20+ agents are modifying adjacent domains. Blindly rewriting every struct with StructLayout would mutate public APIs, break serialized structs, and create compile walls.

Solution: Use DOD source classification. Only structs that are runtime DTOs, Signal payloads, NativeArray elements, Burst job payloads, save/delta records, telemetry entries, or explicit Vault buffers are eligible for forced Explicit layout. Cold file-format records are documented exceptions and must copy into aligned runtime structs before hot use.

Rejected Alternatives: A project-wide regex rewrite from Sequential to Explicit was rejected because it cannot distinguish MonoBehaviour-local helper structs, serialized editor-only structs, and stable file ABI structs. Leaving Sequential untouched was rejected for DTO/native/signals because ARM64 alignment and save ABI are the assigned domain.

Scalability potential: Low tier keeps compact aligned payloads and avoids SIGBUS/cache split stalls. Middle tier gains deterministic layout validation. High tier can add richer debug telemetry by consuming padding or appending to explicit records. Ultra tier can visualize byte maps without increasing gameplay truth struct cost.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-20 us per 10k hot DTO reads when 8-byte fields avoid split-line loads; worst-case gain is crash prevention on ARM64. Evidence class: STATIC_SOURCE until Burst/Unity proof.

## Decision 002 - Validation Location

Problem: Layout validation needs reflection/metadata, but runtime reflection violates Zero-GC and Burst hot path rules.

Solution: Put reflection and byte-map generation inside Editor/build-gate assemblies only. Runtime receives only explicit unmanaged structs, static bit helpers, and Burst jobs.

Rejected Alternatives: Runtime Marshal.OffsetOf checks were rejected because they allocate/reflect and can run on player frames. Debug.Log-based runtime validation was rejected because string formatting allocates and cannot be Burst-safe.

Scalability potential: Low tier pays zero runtime validation cost. Middle/high/ultra tiers get editor-only inspection and CI gates with no gameplay CPU tax.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is preservation of 0 B/frame GC and 0.00 ms runtime validation overhead. Evidence class: STATIC_SOURCE.

## Decision 003 - Unity Job Struct Exception

Problem: HectonFluidEngine contained Pack=1 on Burst job structs that hold NativeArray<T> and NativeQueue<T>.ParallelWriter handles. Forcing LayoutKind.Explicit on those job structs would require hard-coding Unity safety-handle internals that can differ by Unity version, collection checks, and platform defines.

Solution: Remove Pack=1 from job structs, leave them Sequential, and place Explicit layout on the actual NativeArray element DTOs. Add [NoAlias] to job NativeArray fields to give Burst aliasing guarantees without claiming ownership of Unity's handle ABI.

Rejected Alternatives: Explicit offsets for NativeArray fields were rejected because they would freeze a Unity-owned managed wrapper layout and risk compile/runtime breakage. Leaving Pack=1 on job structs was rejected because it still perturbs handle packing and violates the ARM64 mandate.

Scalability potential: Low tier gets stable DTO strides without unsafe Unity handle assumptions. Middle tier benefits from Burst NoAlias vectorization. High and Ultra tiers can process more particles/floaters because alias fences and cache-line DTOs reduce worker stalls.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-25 us per scheduled fluid/physics validation batch when alias analysis improves; worst-case impact is avoiding native handle mispacking on ARM64. Evidence class: STATIC_SOURCE pending Burst compile.

## Decision 004 - Physics Event Payload Rebuild

Problem: PhysicsApplySystem used Pack=1 and readonly auto-properties on force/acoustic/pressure event payloads. Auto-properties hide backing fields and Pack=1 can put copied event data on unquantized 48/80-byte strides.

Solution: Convert the payloads to raw-field Explicit layouts. ForcePacket and acoustic events are 64 bytes; pressure and removed physics payloads are 128 bytes. Constructors start with this = default to deterministically zero padding.

Rejected Alternatives: Keeping readonly auto-properties was rejected because they obscure field offsets and can force defensive copies in in/ref call chains. Keeping 48/80-byte sizes was rejected because arrays of those records cross 64-byte boundaries and generate false sharing risk.

Scalability potential: Low tier pays slightly more memory for stable cache-line stride. Middle tier avoids queue false sharing. High and Ultra tiers can spend saved stalls on richer acoustic/pressure feedback without changing ABI.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 4-60 us per 10k queued/applied packets in contention-heavy frames; primary gain is deterministic ARM64-safe access. Evidence class: STATIC_SOURCE pending compile.

## Decision 005 - Editor X-Ray Instead Of Runtime Reflection

Problem: The task requires human-visible byte maps and strict CLI checks, but runtime reflection and Marshal.OffsetOf in gameplay would violate Zero-GC.

Solution: Add Memory Alignment X-Ray as an EditorWindow and command-line method. It scans ISignal, BinaryBlittableSafe, DTO, payload, and telemetry structs in editor only, uses UnsafeUtility.GetFieldOffset, writes Docs/Reports/ARM64_ALIGNMENT_XRAY_REPORT.txt, and throws BuildFailedException when run as strict CLI.

Rejected Alternatives: Runtime self-scanning was rejected for GC and Burst incompatibility. A silent report-only scanner was rejected because strict CI needs a hard failure mode. A broad InitializeOnLoad throw was rejected until remaining cross-domain Pack=1 debt is removed by owning agents.

Scalability potential: Low tier pays no player cost. Middle tier gets build evidence. High and Ultra tiers get visual byte maps to guide extra telemetry/padding decisions without mutating runtime memory.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is indirect: prevents reintroduction of misaligned 64-bit fields and cache-hostile strides. Runtime cost is 0.00 ms. Evidence class: EDITOR_TOOL.

## Decision 006 - Pack=1 Runtime Zero Pass

Problem: After the first pass, 91 runtime Pack=1 hits remained. The largest risk clusters were animation IK, procedural crab IK, gameplay hazard/mining/player-state DTOs, Sargassum density/boid payloads, wreck generation records, VFX telemetry, GPR telemetry, and persistent world save/delta records.

Solution: Convert every runtime `Pack = 1` hit under `Assets/_Project/Scripts` to either `LayoutKind.Explicit` with aligned offsets/padding or, for non-DTO structs carrying Unity/managed handles, `LayoutKind.Sequential` without Pack. Burst job structs that contain NativeArray/NativeSlice/NativeList/RaycastCommand handles were not explicit-laid out; only their element DTOs were. Added `[NoAlias]` and synchronous Burst flags to the touched jobs.

Rejected Alternatives: Leaving small 8/12/28-byte structs packed was rejected because NativeArray queues and hash maps still stride them and can alias cache lines. Forcing Explicit onto structs holding Unity safety handles or managed references was rejected because it would freeze non-owned ABI and create compile/runtime instability. Keeping `Pack=1` for byte savings was rejected because Quest 3 ARM64 alignment failures cost more than the padding.

Scalability potential: Low tier removes split-line 64-bit loads and false-sharing-prone strides. Middle tier gets more predictable Burst vectorization via NoAlias. High tier can spend the saved stalls on denser VFX/AI work. Ultra tier can enable editor byte-map/X-Ray inspection with no runtime reflection.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 6-120 us per 10k reads/writes in hot queues depending on prior stride; crash-prevention class for AUP/double fields. Evidence class: STATIC_SOURCE, `Pack1RuntimeCount=0`, compile pending due CPU gate.

## Decision 007 - Persistent Save ABI Re-quantization

Problem: Persistent world and mining records used compact packed layouts with 40/56/68/110/204-byte sizes. Those sizes are hostile to rollback hashing and save deltas because adjacent records cross 64-byte boundaries and padding holes are not named.

Solution: Rebuilt persistent records as explicit 16/64/128/256-byte layouts. AUP/long/double fields sit first on 8-byte offsets; `FixedString128Bytes` and hashes are aligned; `_padN` fields account for tail bytes. `PersistentWorldItemRecord` moved to 256 bytes so save/delta append space is explicit instead of anonymous.

Rejected Alternatives: Preserving old compact sizes was rejected because RLE/delta bandwidth savings do not justify unstable cache-line crossings and untracked padding. A binary-compatible no-op attribute removal was rejected for records with 8-byte fields after smaller fields. Full save migration code was not added in this pass because this agent owns ABI layout, not save-version policy.

Scalability potential: Low tier gains deterministic byte copies and avoids split loads. Middle tier gets stable RLE comparison lanes. High and Ultra tiers can consume reserved padding for new persistent facts without reordering old fields.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 0-15 us per large delta scan plus false-desync prevention. Evidence class: STATIC_SOURCE pending save migration owner review.

## Decision 008 - Zero Init Kernel

Problem: Task 15 requires padding to be zeroed while avoiding slow managed/default allocator clear paths for large uninitialized Vault buffers.

Solution: Added `InitializeAlignedBufferJob`, a Burst synchronous IJobParallelFor that clears one 64-byte cache line per index via eight `ulong` stores, with a bounded byte tail only on the final partial line. It takes a raw pointer and byte length so Vault callers can use `NativeArrayOptions.UninitializedMemory` and explicitly clear deterministic bytes after allocation.

Rejected Alternatives: `NativeArrayOptions.ClearMemory` everywhere was rejected for massive buffers because it hides OS/runtime clear cost. Runtime reflection-based clearing was rejected for GC and Burst incompatibility. Platform-specific intrinsics were rejected for this pass because Burst can lower aligned ulong stores to NEON/AVX without adding per-platform branches.

Scalability potential: Low tier clears only required cache lines during boot. Middle tier amortizes chunk initialization through jobs. High and Ultra tiers can bulk-init large diagnostic/visual buffers without main-thread stalls.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 10-150 us on large buffer boot clears, depending on buffer size and memory bandwidth. Evidence class: STATIC_SOURCE pending Burst compile/profiler proof.

## Decision 009 - Signal Payload Fence Re-quantization

Problem: The generic SignalBus copies payloads through NativeQueue lanes; 48/72/80/96/160-byte signal strides cross cache-line boundaries and create false sharing under MPSC writes. Several core and domain signals were explicit but not cache quantized, and two TerminalOS signals were still Sequential.

Solution: Re-quantized all source-visible ISignal payloads to Explicit 16/32/64/128-byte layouts, with TetherTensionSignal as a documented 192-byte three-cache-line exception because two 48-byte AUPs plus tension metadata cannot fit in 128 without deleting facts. Added explicit tail padding fields and a cold SignalBus<T> stride fence using UnsafeUtility.SizeOf<T>() only.

Rejected Alternatives: Shrinking dual-AUP signals by replacing the second AUP with a float delta was rejected because it would move authority from AUP to local approximation and break rollback/large-world correctness. Leaving 80-byte AUP signals was rejected because the second queued element starts inside the previous cache line. Runtime reflection validation was rejected; the SignalBus fence is size-only and reflection-free.

Scalability potential: Low tier avoids cache-line contention and mobile split loads. Middle tier gets stable queue flush bandwidth. High and Ultra tiers can push richer VFX/haptic signals without changing queue ABI because reserved padding is now explicit.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-40 us per heavy signal flush when contention is high; Quest 3 risk class reduced from unquantized queue stride to explicit cache-line stride. Evidence class: STATIC_SOURCE, textual signal parser BadSignalCount=0, compile pending CPU gate.

## Decision 010 - Explicit Pack Removal And Core/Physics CLI Fence

Problem: Even after Pack=1 removal, explicit layouts still had Pack=8 in global contracts and editor guard records, and Core/Physics needed a repeatable source-level gate so future DTOs do not reintroduce Pack or Sequential payloads.

Solution: Removed Pack from all Explicit StructLayout attributes under Assets. Converted GlobalContracts bootstrap/facade structs to explicit cache-quantized layouts and updated generated offsets. Added Arm64LayoutSourceFixer CLI/report for Core/Physics: safe automatic Pack removal on Explicit layouts; hard `[BLOCKED]` report for Sequential DTO candidates.

Rejected Alternatives: A regex tool that invents FieldOffset values for arbitrary Sequential structs was rejected because it cannot parse nested fixed buffers, conditional fields, generic function pointers, Unity collection wrappers, or owner-specific save ABI. Waiting for a full Roslyn dependency was rejected because the source gate can still remove safe Pack debt and fail risky cases today.

Scalability potential: Low tier pays zero runtime cost. Middle/high/ultra tiers preserve compile-wall contracts with stable offsets and avoid future ABI drift through CI artifacts.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is indirect: prevents recurrence of Pack-based unaligned loads in Core/Physics. Runtime cost 0.00 ms. Evidence class: STATIC_SOURCE, Core/Physics Pack scan 0, explicit Pack scan 0, compile pending CPU gate.

## Decision 011 - Total Pack Parameter Purge

Problem: After explicit Pack removal, 79 Sequential `StructLayout(...Pack=...)` parameters remained. Most were float/int-only GPU/intermediate structs where Pack=4/16 was redundant, but two records contained 8-byte fields and needed manual offsets before removing Pack safely.

Solution: Manually converted `VoxelChunkModifiedEvent` and `ResourceNodeTombstoneRecord` to Explicit layout because they contain `ulong` or AUP data. Then mechanically removed the remaining Pack parameters from float/int-only Sequential layouts. Source scan now reports zero Pack parameters under `Assets/_Project/Scripts`.

Rejected Alternatives: Blindly removing Pack from the two 8-byte records was rejected because default packing could move AUP/ulong offsets. Converting every remaining Sequential authoring/GPU struct to Explicit in one sweep was rejected because many are shader/Unity interop records that require owner stride checks.

Scalability potential: Low tier removes Pack-derived ARM64 ambiguity. Middle/high/ultra tiers retain shader interop strides while the explicit Pack ban prevents future cargo-cult Pack reintroduction.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 0-20 us per hot queue depending on prior 8-byte split risk; primary impact is ABI safety. Evidence class: STATIC_SOURCE, Pack1=0, ExplicitPack=0, AnyPack=0, compile pending CPU gate.

## Decision 012 - Core DTO Sequential Debt And Continuous Bucket Striding

Problem: Pack parameters were zeroed, but Core still had several high-confidence Sequential DTOs in active Vault/native routes: Bridge binary records, canonical input state/telemetry, black-box telemetry records, foveated simulation telemetry, and simulation-bucketer frame/result records. Task 10 also needed an actual GlobalQualityWeight traversal path, not a binary low/high tier gate.

Solution: Converted those Core DTOs to `LayoutKind.Explicit` while preserving their existing offsets and sizes. 8-byte fields remain at offsets divisible by 8: `H8PrefabMappingEntry.EstimatedVramBytes@16`, `H8FacadeMacroHeader.EstimatedVramBytes@40`, `InputTelemetryEntryDTO.InputSystemTimeSeconds@0`, `MockPlayerKinematicsSignal.AupLocalCell@0`, `TelemetryHeaderDTO.Timestamp@0`, `MockOriginShiftSignal.SectorX/Y/Z@0/8/16`, `BlackboxRingBufferDTO.Bytes@0`, and `BlackboxSourceSlot.SourcePtr@0`. `ModuloSimulationBucketer` now maps `GlobalQualityWeight` through SmoothStep and deterministic frame-phase dithering between 1/2/4 active slow-bucket groups, so average traversal bandwidth scales continuously while bucket math remains power-of-two and deterministic.

Rejected Alternatives: Converting Burst job wrapper structs with `NativeArray`, `NativeList`, `RaycastCommand`, or `TransformAccess` fields to Explicit was rejected because Unity owns those wrapper layouts. Keeping the old bucketer low/high branch was rejected because it violated the no-binary-quality-switch law. Processing arbitrary 3 active buckets was rejected because current bucket grouping relies on power-of-two masks; deterministic temporal dither between powers of two preserves bitmask routing and achieves a continuous average.

Scalability potential: Low tier averages toward one active slow bucket and stretches rebalance cadence, freeing CPU for presentation smoothing and input stability in the first 20 minutes route. Middle tier temporally mixes one/two/four active groups with deterministic frame phase. High tier approaches four active groups with tighter rebalance. Ultra tier keeps the same ABI and spends the saved stable traversal on richer visual consumers, not larger gameplay truth structs.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 10-80 us per 100k bucketed slow-tick candidates under thermal pressure, plus 2-20 us per 10k reads for Core DTOs that no longer depend on compiler-inserted Sequential padding. Evidence class: STATIC_SOURCE. Compile/Burst proof remains blocked by CPU gate at 100.0%.

## Decision 013 - NativeQueue Payload Quantization And DTO Property Removal

Problem: After Pack purge, several unmanaged event payloads still used Sequential layout or explicit 24/40/48-byte strides while being stored in NativeQueue lanes or Vault/Core contracts. `ModuleStatusEventPayload` also exposed bool properties, creating hidden method calls on a copied DTO and leaving status bits in a 16-bit lane.

Solution: Converted the targeted NativeQueue/Core payload layer to Explicit layout and 16/32/64-byte strides: AudioLog, AtlasSignal, Bootstrap, GameBootstrapper, BiomeMatrix, Crafting, Weather, Localization, Narrative, Scan, ModuleStatus, Inventory, Core Registry, ThreadSafeCommandQueue, Bridge telemetry/mapping, duplicate deterministic input contracts, and BlackboxRingBufferDTO. `ModuleStatusEventPayload` is now 64 bytes with `uint StatusFlags`; bool property access moved to static `ModuleStatusEvents` bit helpers. Bridge verifier expected sizes were updated for the newly padded 64-byte entries.

Rejected Alternatives: Leaving 24/40/48-byte event strides was rejected because queue elements then straddle 64-byte cache lines under producer contention. Rewriting Unity-owned containers (`NativeArray`, `NativeQueue`, safety-handle wrappers) to Explicit was rejected because their internal layout is owned by Unity. Changing Core `InputStateDTO` from 24 to 32 bytes was rejected in this pass because rollback contracts hard-code offsets at 24/48/60; that migration needs a netcode-owner offset update, not an isolated ABI edit.

Scalability potential: Low tier gets fewer split cache-line queue reads and simpler 32-bit flag tests. Middle tier keeps deterministic NativeQueue flush behavior under listener fan-out. High and Ultra tiers can add richer event metadata into explicit padding without changing the public route or introducing managed sidecars.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 3-25 us per heavy late-frame queue drain depending on event pressure; ModuleStatus flag access saves an estimated 1-6 us per 10k payload reads by removing DTO bool property calls and using a single uint mask. Evidence class: STATIC_SOURCE; compile proof blocked by CPU gate.

## Decision 014 - CurrentMeta Weather ABI Re-quantization

Problem: `CurrentMeta` remained an Explicit Size=24 Core DTO after the queue-payload sweep. It is embedded in `WeatherRuntimeSnapshot` and `WeatherEventPayload`; changing it alone would overlap following `FieldOffset` fields and corrupt weather snapshot consumers.

Solution: Expand `CurrentMeta` to Explicit Size=32 with a named tail `ulong` pad. Move `WeatherRuntimeSnapshot` wave components to 64/96/128 and pad the snapshot to 192 bytes, exactly three cache lines. Expand `WeatherEventPayload` to 128 bytes with `CurrentMeta@32`, `EventType@64`, and named tail padding. Update `BinaryLayoutManifest` cold-boot assertions for `CurrentMeta` and `WeatherRuntimeSnapshot`.

Rejected Alternatives: Leaving `CurrentMeta` at 24 bytes was rejected because it kept a non-quantized Core DTO in a NativeQueue payload route. Shrinking the weather snapshot by deleting waves was rejected because those waves are the existing Dear Lie fallback spectrum for physics/VFX and would move work back toward heavier runtime sampling. Padding only `WeatherEventPayload` without changing `CurrentMeta` was rejected because the Core DTO itself would still violate the aligned stride contract.

Scalability potential: Low tier keeps weather current metadata on a 32-byte stride and queue weather events on 128-byte boundaries, reducing split-line reads during late-frame drains. Middle tier keeps the three analytic waves as the cheap visual/physics fake. High and Ultra tiers can consume the same stable snapshot for richer caustic/fluid presentation without changing ABI or adding per-device layout branches.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-10 us per heavy weather event drain or snapshot fan-out; primary gain is removing a remaining 24-byte Core DTO stride from the Quest 3 risk set. Evidence class: STATIC_SOURCE; compile proof blocked by CPU gate at 50.3%.

## Decision 015 - Compile Wall Triage And AnomalySignal Padding Fix

Problem: The first permitted scoped `Hecton8.Core.csproj` build failed with 117 compile errors. Most errors are unrelated dependency-wall failures from missing sibling/domain contracts (`Hecton8.Logistics.Grid`, `H8BinaryWorldPager`, `VaultGenerationHandle<>`, construction socket DTOs, docking/autopilot interfaces). One error was within SHINOBU's previous signal-fence edit: duplicated `_padTail0/_padTail1/_padTail2` fields in `AnomalySignal`.

Solution: Remove the duplicate `AnomalySignal` padding triplet and leave one explicit padding sequence at offsets 18/20/24. Do not attempt to repair unrelated missing domain references from this agent because that would cross ownership boundaries and likely create a larger compile wall. Stop orphan MSBuild nodes left by the failed scoped build.

Rejected Alternatives: Editing Logistics/Grid, construction socket, docking, and Vault generation contracts was rejected because those are not ARM64 DTO ABI ownership issues. Re-running build immediately after the padding fix was rejected because the post-cleanup CPU gate rose above 50%, and the project rule forbids compile launches under that load.

Scalability potential: Low tier gains no runtime feature from this triage; the value is compile-wall containment. Middle/high/ultra tiers preserve assembly ownership and avoid source churn in unrelated systems while keeping the signal DTO byte map explicit.

Hardware Impact: Direct runtime gain is 0 us. The duplicate-field fix removes a C# compile blocker inside the signal DTO layer; remaining build failure is dependency-wall, not an ARM64 layout error. Evidence class: COMPILE_ATTEMPT_FAILED_DEPENDENCY_WALL.

## Decision 016 - Owner-Safe Core Content And Dispatcher DTO Sweep

Problem: After weather and queue payload re-quantization, several Core-owned records still used non-Pack Sequential layout in Vault/native routes: ContentAuthority bundle refs, telemetry, pending load rows, visual budgets, lore block index rows, GlobalTelemetryBus events, and SystemDispatcher state/telemetry signals. These are small enough to look harmless, but their NativeArray/Vault usage makes compiler-inserted padding an uncontrolled ABI detail.

Solution: Convert the owner-safe records to `LayoutKind.Explicit`: `ContentBundleRefState=32`, `ContentAuthorityTelemetryEntry=64`, `ContentPendingLoadState=16`, `ContentVisualFeatureBudget=16`, `ContentLoreBlockIndex=16`, `TelemetryEvent=64`, `DispatcherStateDTO=32`, `DispatcherPipelineTelemetryEntry=32`, and `MockTimeDilationSignal=16`. `ContentBundleRefState.Bytes` moved to offset 0 and the record gained an explicit 8-byte tail pad so Vault refs no longer stride at 24 bytes. Existing semantic field names were preserved so object initializers and editor serialization keep source compatibility.

Rejected Alternatives: Rewriting lockstep replay/file-format records was rejected because those records have existing external ABI and need a netcode/save-owner migration plan. Rewriting Burst job wrapper structs was rejected because Unity owns NativeArray/NativeQueue handle layout. Leaving `ContentBundleRefState` at 24 bytes was rejected because it is a persistent Vault row and every fourth record starts on a different cache-line phase.

Scalability potential: Low tier gets aligned Vault and telemetry rows without branchy device checks. Middle tier reduces cache-line phase churn during content residency sweeps. High and Ultra tiers can use the named padding to expand content/debug telemetry later without changing the route or adding managed sidecars.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-15 us per 10k ContentAuthority bundle or telemetry row reads in residency-heavy frames; `TelemetryEvent` and dispatcher telemetry remain 64/32-byte stable for later black-box/export work. Evidence class: STATIC_SOURCE; build rerun deliberately skipped because the known dependency wall remains and no new compile proof would be meaningful.

## Decision 017 - Core Runtime Snapshot DTO Quantization

Problem: Core runtime snapshots for brine, power, UI, and player context still used Sequential layout. `UIValueSlot` used a 24-byte NativeArray stride and `PlayerSurvivalRuntimeState` used an 88-byte snapshot stride, both leaving cache-line phase behavior to the CLR instead of explicit ABI.

Solution: Convert these owner-safe snapshots to explicit layouts: `BrineLayerSample=32`, `BatteryRuntimeSnapshot=16`, `UIStateData=32`, `UIValueSlot=32`, `PlayerMovementRuntimeState=128`, `PlayerLookState=32`, `PlayerSurvivalRuntimeState=128`, and `PlayerInteractionRuntimeState=32`. `PlayerMovementRuntimeState.PredictedAup` remains 8-byte aligned at offset 24 and its embedded AUP spans 24..71. `UIValueSlot` gained named tail pads at 20/24/28 so its native UI buffer no longer strides at 24 bytes.

Rejected Alternatives: Changing `AcousticAup` from 40 to 64 was rejected in this slice because audio-side explicit parent structs currently assume the 40-byte contract and need coordinated audio-owner migration. Rewriting prologue/replay snapshots was rejected because those are persisted or replay-facing format records. Keeping `PlayerSurvivalRuntimeState` at 88 bytes was rejected because it is a core player truth snapshot copied by many systems and has no external binary contract found in this pass.

Scalability potential: Low tier gets stable UI/player snapshot reads without branch-based quality paths. Middle tier reduces phase churn in HUD value scans and player-context fan-out. High and Ultra tiers can use the named padding for more visual/diagnostic scalar outputs without adding new managed bridge objects.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per 10k UI value/player snapshot reads, with the larger direct value being deterministic ABI and fewer split-line copies in UI/native snapshot fan-out. Evidence class: STATIC_SOURCE; compile proof still blocked by known dependency wall.

## Decision 018 - Acoustic And Object Batch Explicit Layout Without ABI Resize

Problem: `AcousticAup`, `ObjectBatchInstance`, and `ObjectBatchChunk` still used Sequential layout. `AcousticAup` is embedded throughout audio propagation/virtualization parent DTOs, while object-batch payloads are serialized authoring/render records with existing validator sizes.

Solution: Convert all three to `LayoutKind.Explicit` while preserving current sizes: `AcousticAup=40`, `ObjectBatchInstance=80`, and `ObjectBatchChunk=40`. `AcousticAup` keeps long grid lanes at 0/8/16 and `float3 Local@24`. Object batch instance keeps the 64-byte matrix first, then four 4-byte lanes. Object batch chunk gets named byte tail padding at 37..39.

Rejected Alternatives: Expanding `AcousticAup` to 64 was rejected in this pass because audio-side parent DTOs currently assume the 40-byte embedded contract; resizing it without migrating those parents would corrupt their explicit offsets. Expanding object batch rows to 128/64 was rejected because the editor validator and serialized payloads currently assert 80/40 and no runtime cache-line contention proof was captured.

Scalability potential: Low tier gets deterministic object-batch and audio AUP offsets without touching render/audio owner contracts. Middle tier can keep current baked payload sizes. High and Ultra tiers can later migrate object-batch rows to cache-line/SoA render streams behind the same authoring boundary.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 0-8 us per 10k object-batch/audio AUP metadata reads. Main gain is source-level ABI determinism and removal of Sequential layout drift; no runtime profiler proof.

## Decision 019 - Core Intrinsic And Arena DTO Explicit Sweep

Problem: Core still contained small, high-confidence non-Pack Sequential DTOs in spatial culling, NativeBitmask, logistics spline generation, arena allocation metadata, and native-memory snapshot capture. These records are easy to dismiss because their natural layout was already mostly aligned, but their layout was still compiler-owned instead of source-owned, and `ArenaAllocation` had a 24-byte stride that changes cache-line phase every element.

Solution: Convert owner-safe records to explicit source-owned byte maps: `HectonAabb=32`, `HectonSphere=16`, `NativeBitmask256=32`, `SplineDescriptor=64`, `NativeAllocationSnapshotSource=32`, and `ArenaAllocation=32`. `ArenaAllocation` grew from 24 to 32 bytes with an 8-byte named tail pad. `NativeBitmask256.IsEmpty` and `NativeArenaSlice<T>.IsCreated` properties were changed to methods after source scan found no project call sites. `NativeArenaSlice<T>` was intentionally kept Sequential Size=32 because generic explicit-layout structs are a .NET/IL2CPP TypeLoad risk; it remains pointer-first, multiple-of-8, and named-padded.

Rejected Alternatives: Forcing `LayoutKind.Explicit` onto `NativeArenaSlice<T>` was rejected because generic explicit layout can fail type loading on managed runtimes and IL2CPP. Leaving `ArenaAllocation` at 24 bytes was rejected because arena metadata is a hot native allocator result and the third record starts on a different 64-byte phase. Reordering fields was rejected because existing constructor/object-initializer source compatibility is preserved by exact field names and offsets.

Scalability potential: Low tier gets stable culling primitives, bitmask masks, and allocator metadata without binary quality branches. Middle tier benefits from predictable arena metadata stride during first-20-minutes bootstrap and route setup. High and Ultra tiers can use the same explicit bitmask/spatial primitives to feed richer culling, diagnostics, and visual-overkill presentation lanes without growing gameplay truth structs.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per 10k spatial/bitmask/arena metadata reads. Main gain is ABI determinism, 24-byte arena stride removal, and Quest 3 split-load risk reduction. Evidence class: STATIC_SOURCE; build proof intentionally not rerun because the dependency wall remains and the user prohibited rebuild until needed.

## Decision 020 - Core Input And Global Registry DTO Explicit Closure

Problem: Core still exposed non-Pack Sequential records in input, scheduling black-box, GlobalSignals transform, and `GlobalRegistryContracts.cs`. Many were naturally aligned, but natural layout is compiler-owned and still leaves ABI proof outside the source. `GlobalRegistryContracts.cs` was especially risky because it is the cross-domain contract surface; a hidden padding drift there propagates into every consumer even when no Pack attribute exists.

Solution: Convert the owner-safe records to explicit source-owned byte maps while preserving existing public sizes: `BufferedActionEntry=16`, `JobAdmissionBlackboxEntry=32`, `CombatDamageSignalAupShiftTransformer=16`, `InputState=24`, `PlayerInputState=64`, `XRInputState=64`, and every `StructLayout` record inside `GlobalRegistryContracts.cs`. AUP-backed registry records use the confirmed `AbsoluteUniversePosition` layout (`Size=48`, long lanes at 0/8/16, float lanes at 24/28/32) and keep embedded AUP offsets divisible by 8. `XRInputState.HasActiveInput` and `EcosystemBiomassAuditSample.IsFinite` were changed from properties to methods; the two source-visible external call sites were updated.

Rejected Alternatives: Expanding every 80/88/96/120/144-byte registry record to 128/192 was rejected because the registry contracts may be consumed by owner domains that rely on the existing size and semantic field order; source-owning the current ABI is safer than silently changing cross-domain payload widths. Leaving `GlobalRegistryContracts.cs` Sequential was rejected because this file is the contract boundary and should not depend on CLR implicit offsets. Forcing generic/native-container wrappers to explicit remains rejected because Unity/IL2CPP own those layouts.

Scalability potential: Low tier gets deterministic contract reads and avoids split 64-bit AUP/double loads without adding device branches. Middle tier benefits from stable cross-domain payload copying through GlobalRegistry interfaces. High and Ultra tiers can route richer presentation systems through the same explicit contracts without adding managed sidecars or changing ownership routes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-18 us per 10k registry/input/black-box payload reads depending on fan-out. Main gain is ABI determinism at the contract surface and removal of all `LayoutKind.Sequential` records from `GlobalRegistryContracts.cs`. Evidence class: STATIC_SOURCE; build proof intentionally not rerun because the known dependency wall remains and the user prohibited rebuild until needed.

## Decision 021 - GlobalTelemetry Blackbox Job Layout Closure

Problem: `GlobalTelemetryBus.Blackbox.cs` still had Sequential job/editor frame records after prior DTO conversion. `NanSweeperJob` and `MockOriginShiftFireJob` are pointer-bearing Burst jobs, not Unity collection wrappers, so their 32-byte layout can be source-owned without freezing Unity safety-handle internals.

Solution: Convert `NanSweeperJob=32`, `MockOriginShiftFireJob=32`, and editor-only `BlackboxEditorFrame=32` to `LayoutKind.Explicit`. Pointer fields remain at 8-byte offsets 0/16/24, scalar lanes remain on 4-byte offsets, and `MockOriginShiftFireJob` gained a named tail pad at offset 28.

Rejected Alternatives: Leaving these jobs Sequential was rejected because the black-box crash route is the evidence path for NaN/alignment failures and should not depend on compiler-inferred padding. Converting NativeQueue/NativeArray wrapper structs in the same Core scan was rejected because Unity owns their internal layouts.

Scalability potential: Low tier keeps black-box sweeps and mock origin-shift fault probes on stable 32-byte job records. Middle/high/ultra tiers can expand forensic coverage around these jobs without changing hot gameplay DTOs.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 0-4 us per black-box sweep dispatch; primary value is deterministic crash-forensics ABI. Evidence class: STATIC_SOURCE; build proof intentionally not rerun.

## Decision 022 - Lockstep And MacroDatabase Explicit ABI Sweep

Problem: `LockstepStateValidator.cs`, `MacroDatabaseContracts.cs`, and `H8MacroDatabaseService.cs` still contained non-Pack Sequential records that represent rollback/replay hashes, static cache payload handles, hydration candidates, and dirty-sector private rows. These rows are copied, hashed, or stored in native/cache lanes, so source-unowned offsets are an avoidable ABI risk even when natural alignment currently matches.

Solution: Converted the owner-safe DTO rows to explicit byte maps while preserving the current public sizes where external ABI could exist: `LockstepPlayerKinematicState=96`, `LockstepReplayInputFrame=48`, `LockstepReplayBlockHeader=128`, `LockstepArrayHash=32`, `LockstepTelemetryEntry=64`, `LockstepMasterHashHistoryEntry=32`, `MacroDatabaseConfig=64`, `MacroDatabasePayloadHandle=40`, `MacroDatabaseNativeCacheStats=24`, `MacroDatabaseStats=80`, `MacroDatabaseCompactionSnapshot=48`, `SectorHydratedSignal=32`, `MacroDatabaseTelemetryEntry=72`, `SectorCoord64=24`, `HydrationCandidate=48`, `MacroDatabaseDirtyPayloadSlot=64`, and `MacroDatabaseSectorCoordSlot=64`. Long lanes remain on 8-byte offsets and private slot rows now have named 64-byte padding where they can be mutated by cache maintenance paths.

Rejected Alternatives: Expanding lockstep replay input/state records to larger cache-line multiples was rejected because replay/state-ring ABI may already be consumed by tools and netcode; source-owning the existing stride is safer than silent format migration. Converting `NativeArray` job wrapper structs in `LockstepStateValidator.cs` was rejected because Unity owns native-container field layout and Burst safety handles.

Scalability potential: Low tier gets deterministic cache/replay reads without binary quality branches. Middle tier keeps current replay file width and MacroDatabase file offsets stable. High and Ultra tiers can consume the same explicit telemetry/hash rows for deeper diagnostics without adding managed shadow state.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per 10k lockstep/cache-row reads. Primary gain is rollback/hash ABI determinism and 64-byte isolation of MacroDatabase dirty/coord slots. Evidence class: STATIC_SOURCE; build proof intentionally not rerun because the dependency wall and rebuild prohibition still apply.

## Decision 023 - H8StaticData File Contract Explicit Closure

Problem: `H8StaticDataContracts.cs` still used Sequential layout for static-data headers, lookup rows, Babel rows, the mock UI buffer, static balance records, black-box telemetry, and dump headers. These records are file/mmf contracts and Burst lookup inputs, so hidden CLR layout ownership is unacceptable even though the existing natural layout was aligned.

Solution: Converted the static-data file contracts to explicit offsets without changing sizes: `H8StaticDataHeader=64`, `H8StaticDataLookupEntry=16`, `H8BabelDictionaryHeader=32`, `H8BabelDictionaryEntry=16`, `BabelIndexDTO=16`, `BabelLookupResultDTO=16`, `MockUIBuffer=16`, `H8ItemStaticRecord=48`, `H8EconomyStaticRecord=48`, `H8PhysicsStaticRecord=48`, `H8FaunaStaticRecord=48`, `H8StaticDataTelemetryEntry=64`, and `H8StaticDataDumpHeader=32`. The 8-byte `long`/pointer lanes remain at offsets 0, 8, 32, or 40 as applicable; ushort lanes remain packed only inside 4-byte-aligned header/record words where file ABI already defines that packing.

Rejected Alternatives: Resizing 48-byte balance records to 64 bytes was rejected because the binary static-data format and record offsets are persisted by `H8StaticData.bin`; changing stride requires a schema/version migration, not a layout safety patch. Leaving the naturally aligned rows Sequential was rejected because file ABI must be expressed in source, not inferred from the runtime.

Scalability potential: Low tier gets stable lookup/static balance rows for MMF-backed reads with no runtime branch. Middle tier keeps existing file size and cache-BTree offsets. High and Ultra tiers can add richer static tuning records through an explicit schema migration later instead of relying on hidden padding.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 0-8 us per heavy static lookup batch; primary gain is byte-for-byte static file ABI proof and removal of all `LayoutKind.Sequential` hits from `H8StaticDataContracts.cs`. Evidence class: STATIC_SOURCE; `git diff --check` passed with CRLF warnings only.

## Decision 024 - SaveSystem Merkle And Delta ABI Explicit Closure

Problem: SaveSystem Merkle, master-hash, and delta-compression DTOs still used Sequential layout for persisted WAL headers, Merkle nodes, sector entries, delta runs, AUP quantization rows, and strict save headers. These records are exactly the rows that blind binary reads, hash computation, and delta compression depend on; hidden CLR offsets are not acceptable proof.

Solution: Converted `SaveStateMerkleTree.cs`, `SaveMasterHashV10.cs`, and `SaveDeltaCompression.cs` binary DTOs to explicit offsets while preserving every existing size: Merkle/tree rows at 32/64/80/128 bytes, `SaveFileHeaderV10=72`, delta micro-runs at 8 bytes, `QuantizedAupSectorHalf3=24`, `SaveAupLocalOffset32=32`, `StrictSaveFileHeader64=64`, `SaveChunkHeader32=32`, and `SectorPayloadDTO=264`. All long/ulong/double lanes remain at offsets divisible by 8; ushort/byte lanes keep their file-defined compact positions.

Rejected Alternatives: Resizing `SaveFileHeaderV10` to 80 or `SectorPayloadDTO` to 320 was rejected because these are persisted schemas and must not change without a versioned migration. Rewriting save algorithms or compression paths was rejected because the task here is ABI ownership, not save-format redesign.

Scalability potential: Low tier gets stable file reads and delta compression rows without device branches. Middle tier preserves existing WAL/BTree payload size. High and Ultra tiers can extend save telemetry or compression lanes behind versioned explicit headers later instead of relying on implicit padding.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-10 us per 10k save/delta rows in hashing/compression batches. Primary gain is rollback/save ABI proof and prevention of unaligned 64-bit file reads on ARM64. Evidence class: STATIC_SOURCE; `rg` now reports 0 Sequential hits in the three touched SaveSystem files and `git diff --check` passed with CRLF warnings only.

## Decision 025 - SaveBinaryStorage Header Layout And Legacy V8 Hash Split

Problem: `SaveBinaryStorage.cs` still contained Sequential layouts for tokenized payload headers, indexed sector groups, compact entity state rows, protected block headers, override headers, current save headers, cloud metadata, delta cells, thermal RLE runs, and persistent-world compact rows. It also contained an already-explicit legacy V8 save header with `ulong` hash fields at offsets 36 and 44, which preserves wire ABI but risks unaligned 64-bit loads on ARM64 when read as a struct.

Solution: Converted every Sequential record in `SaveBinaryStorage.cs` to explicit offsets while preserving existing constants and sizes. Replaced `IndexedSaveFileHeaderV8.HashPayload64` and `.HashHeader64` with four aligned `uint` halves at offsets 36/40/44/48. Conversion helpers now compose/split the 64-bit values through `ComposeUInt64` and `SplitUInt64`, preserving the 52-byte legacy wire format without issuing unaligned `ulong` field loads.

Rejected Alternatives: Expanding the V8 header to 56 or 64 bytes was rejected because it would corrupt legacy save compatibility. Leaving the unaligned `ulong` fields was rejected because explicit layout alone is not ARM64-safe if a 64-bit lane starts at offset 36. Rewriting the entire save pipeline was rejected because this slice needed ABI safety, not format redesign.

Scalability potential: Low tier gets safer legacy-save reads and stable compact rows without runtime quality branches. Middle tier preserves indexed sector and tokenized payload compatibility. High and Ultra tiers can continue using the same save bridge while richer metadata stays versioned behind explicit headers.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-15 us per 10k SaveBinaryStorage header/compact-row reads in load/save bursts. Primary value is removing a real unaligned 64-bit field hazard from the legacy V8 bridge while preserving byte format. Evidence class: STATIC_SOURCE; `SaveBinaryStorage.cs` now reports 0 Sequential hits and `git diff --check` passed with CRLF warnings only.

## Decision 026 - H8BinaryWorldPager Command And Telemetry Explicit Closure

Problem: `H8BinaryWorldPager.cs` still had Sequential page command/result rows and 64-byte pager telemetry. These rows flow through paging queues and black-box dumps, so leaving their offsets compiler-owned creates unnecessary ABI drift and makes queue stride proof weaker.

Solution: Converted `PageWriteCommand=32`, `PageReadCommand=24`, `PageReadResult=32`, and `PagerTelemetryEntry=64` to explicit offsets. `SectorHash`, `Offset`, `TicksUtc`, and `Reserved` remain on 8-byte offsets; enum/byte status lanes are packed only in explicitly named byte/ushort fields at offsets 24/25/26 or 44/45/46.

Rejected Alternatives: Expanding `PageReadCommand` from 24 to 32 bytes was rejected in this pass because it is an internal queue command with no observed false-sharing proof and the current size already keeps the single 8-byte lane aligned. Leaving pager telemetry Sequential was rejected because it is a crash/IO forensic row and should be byte-verifiable.

Scalability potential: Low tier gets stable page command reads during chunk streaming without runtime quality branches. Middle tier keeps current paging queue density. High and Ultra tiers can add richer pager diagnostics through explicit telemetry padding or versioned rows instead of managed sidecars.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 0-8 us per 10k pager command/telemetry rows. Primary value is deterministic page IO ABI and clean static source proof. Evidence class: STATIC_SOURCE; `H8BinaryWorldPager.cs` now reports 0 Sequential hits and `git diff --check` passed with CRLF warnings only.

## Decision 027 - SaveData Blittable DTO Explicit Closure

Problem: `SaveData.cs` contains both managed compatibility DTOs and true `[BinaryBlittableSafe]` fixed-size records. The fixed records still used Sequential layout for player kinematics, inventory shadows, fauna hibernation, geology seam/cave data, construction mirrors, PDA advisory counters, environmental strain, and module graph edges.

Solution: Converted all `[BinaryBlittableSafe]` records in `SaveData.cs` to explicit offsets while preserving current sizes: `PlayerKinematicStateDTO=48`, `ExternalScavengerSiteDTO=32`, `InventoryShadowDTO=32`, `ProceduralFaunaStateDTO=16`, `HibernatedFaunaStateDTO=112`, `ProceduralGeologySeamStateDTO=64`, `ProceduralGeologyCaveEntranceDTO=48`, `HabitatFloodStateDTO=32`, `ModuleBlitDTO=64`, `PDAContextualAdvisoryDTO=48`, `EnvironmentalStrainDTO=16`, and `ModuleGraphEdgeDTO=16`. Embedded `AbsoluteUniversePositionBlit128` in `HibernatedFaunaStateDTO` starts at offset 16 and keeps its internal long lanes aligned.

Rejected Alternatives: Converting managed compatibility DTOs with `string`, arrays, or `bool` was rejected because those are not unmanaged payloads and explicit layout would not make them safe for blind memcpy. Reordering `HibernatedFaunaStateDTO` to put AUP at offset 0 was rejected because it would change the persisted 112-byte save ABI; the current AUP offset 16 is already 8-byte aligned.

Scalability potential: Low tier gets deterministic save mirror rows without runtime quality branches. Middle tier preserves existing save compatibility. High and Ultra tiers can add richer compatibility bridges later behind explicit fixed records instead of expanding managed DTO graphs.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per 10k fixed save DTO rows in restore/mirror passes. Primary gain is removal of source-unowned fixed save layout while avoiding unsafe edits to managed compatibility structs. Evidence class: STATIC_SOURCE; `rg -U` reports 0 `[BinaryBlittableSafe]` + Sequential records in `SaveData.cs`.

## Decision 028 - Core Prologue Inertial And DOD Replay Explicit Closure

Problem: Core contract/replay files still had Sequential DTOs for prologue snapshots, inertial navigation/compass state, and DOD replay sidecar records. These are contract, replay, or telemetry payloads where compiler-owned offsets weaken rollback/debug evidence, especially for `double3`, `double`, `long`, and `ulong` lanes.

Solution: Converted `PrologueOrbitalSnapshot=48`, `PrologueAtmosphericReentrySnapshot=16`, `PrologueCompleteSnapshot=16`, `CompassStateDTO=176`, `InertialNavigationSnapshot=120`, and the remaining `DodReplayRecorder.cs` replay sidecars to explicit layout. `CompassStateDTO` keeps four `double3` blocks at offsets 0/24/48/72; `InertialNavigationSnapshot` keeps AUP blocks at 0/24/48; replay hash/drift records keep 8-byte lanes on offsets divisible by 8.

Rejected Alternatives: Reordering inertial snapshots to place floats before all AUP blocks was rejected because the registry contract size/order may already be consumed by UI and cockpit readers. Changing DOD replay record sizes to cache-line multiples was rejected where records are sidecar file ABI; source-owning the current schema is safer than silent replay format migration.

Scalability potential: Low tier gets stable prologue/navigation/replay reads without binary quality branches. Middle tier preserves existing recorder payload sizes. High and Ultra tiers can attach richer diagnostic sidecars to the explicit replay format instead of expanding managed debug objects.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-10 us per 10k contract/replay rows. Primary value is aligned `double3`/hash-lane proof and deterministic replay sidecar ABI. Evidence class: STATIC_SOURCE; the three touched Core files now report 0 Sequential hits and `git diff --check` passed with CRLF warnings only.

## Decision 029 - ArchitectEye GPU And Blackbox Row Explicit Closure

Problem: `ArchitectEyeVisualizer.cs` still used Sequential layout for GPU instance rows, 300-frame black-box entries, and runtime state. These rows are not Unity native-container wrappers; they are source-owned payloads written to Vault/GPU buffers and dumped for diagnostics. Empty assembly marker structs in Core Contracts and Persistence also still showed up as Sequential noise in the Core scan.

Solution: Converted `ArchitectEyeQuadInstance=80`, `ArchitectEyeBlackBoxEntry=64`, and `ArchitectEyeRuntimeState=64` to explicit offsets. Float4 lanes sit at 16-byte boundaries in the GPU instance row, black-box entries remain one 64-byte cache line, and runtime state remains one 64-byte row. Converted empty assembly markers to explicit Size=1. Unity-owned job/native-container wrappers were intentionally left alone because explicit offsets over `NativeArray`, `NativeQueue`, `TransformAccess`, or generic NativeContainer internals would be owner-blind and runtime-risky.

Rejected Alternatives: Expanding `ArchitectEyeQuadInstance` to 96 bytes was rejected because the shader/GraphicsBuffer stride is already validated as 80 bytes and widening it would require shader ABI migration. Converting Foveated/Lockstep/NativeQuery job wrappers was rejected because Unity owns the embedded native-container layout under safety-handle variants. Deleting marker structs was rejected because assembly-boundary references may use their type identity.

Scalability potential: Low tier keeps forensic visualization rows stable when diagnostics are enabled without branchy tier logic. Middle tier keeps the current 80-byte indirect quad stride. High and Ultra tiers can push denser ArchitectEye overlays through the same explicit rows while the continuous quality budget controls sample counts elsewhere.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 0-6 us per 10k ArchitectEye row reads or uploads when diagnostics run. Primary gain is GPU/black-box ABI proof and removal of owner-safe Sequential records from the Core diagnostics scan. Evidence class: STATIC_SOURCE; touched-file Sequential scan and unaligned 8-byte FieldOffset scan returned 0 hits; `git diff --check` passed with CRLF warnings only.

## Decision 030 - BurstCallback Function Pointer Wrapper Explicit Closure

Problem: `BurstCallback.cs` had three Sequential records. Only one of them, `BurstCallback`, is a source-owned DTO-like wrapper with a single Burst `FunctionPointer`. The other two records embed Unity `NativeQueue<int>` and `NativeQueue<int>.ParallelWriter`, so their internal layout can vary with Unity collection safety configuration.

Solution: Converted `BurstCallback` to explicit Size=8 with the function pointer at offset 0. Left `BurstCallbackQueue` and `ParallelEventWriter` Sequential because they are native-container wrappers, not blind-copy DTOs. This reduces owner-safe Sequential debt without freezing Unity queue internals.

Rejected Alternatives: Converting `BurstCallbackQueue` to explicit layout was rejected because it would require hardcoding `NativeQueue<T>` and `NativeArray<T>` field sizes across safety-handle modes. Removing StructLayout attributes to hide the scan was rejected because it would not create a source-owned ABI. Widening `BurstCallback` to 16 bytes was rejected because the wrapper only carries one native function pointer and no false-sharing counter.

Scalability potential: Low tier keeps Burst callback handles compact and stable. Middle/high/ultra tiers can queue more callback events through the existing Unity queue path without changing callback-handle ABI.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 0-2 us per 10k callback wrapper reads. Primary gain is static ABI proof for the only source-owned record in `BurstCallback.cs`. Evidence class: STATIC_SOURCE; `BurstCallback.cs` unaligned 8-byte FieldOffset scan returned 0 hits and `git diff --check` passed with CRLF warnings only.

## Decision 031 - Crash Telemetry And Toxic Chemistry Explicit Closure

Problem: `CrashTelemetryBuffer.cs` still used Sequential layout for binary crash export and live telemetry headers. `ToxicOutgassingChemistryTypes.cs` still used Sequential layout for toxicity state, grid headers, source rows, constants, mock samplers, combat-damage transfer rows, and grid telemetry. These rows feed dump files, forensic telemetry, or Burst chemistry grids where source-owned offsets matter more than CLR convenience.

Solution: Converted crash export/live telemetry headers and all non-signal Sequential rows in `ToxicOutgassingChemistryTypes.cs` to explicit layouts while preserving existing sizes. Toxic grid/source/telemetry records keep `double3` AUP lanes at offset 0 and all 8-byte pads at offsets divisible by 8. Existing `ToxicityExposureSignal` and `ToxicBioluminescenceSignal` were already explicit and were left unchanged.

Rejected Alternatives: Expanding 48-byte `ToxicitySourceDTO` to 64 bytes was rejected because the existing chemistry buffer stride may already be consumed by jobs and tooling; source-owning the current size avoids silent format migration. Replacing mock flow/sampler rows with managed test objects was rejected because CI fallback data must remain blittable. Changing the overlapping union fields in `TelemetryEntry` was rejected because that is existing explicit crash telemetry ABI and outside this Sequential purge.

Scalability potential: Low tier keeps toxic plume and crash telemetry DTOs compact and deterministic. Middle tier preserves current chemistry buffer widths. High and Ultra tiers can run richer toxic-grid diagnostics over the same explicit rows while visual density remains controlled by continuous `GlobalQualityWeight` in the existing constants.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per 10k toxic chemistry rows and 0-4 us per crash export batch. Primary gain is ARM64-safe 8-byte AUP/pad proof and dump ABI determinism. Evidence class: STATIC_SOURCE; touched-file Sequential and unaligned 8-byte FieldOffset scans returned 0 hits; `git diff --check` passed with CRLF warnings only.

## Decision 032 - Material Response And TBDR Culling DTO Explicit Closure

Problem: `ShinobuMaterialResponseRuntime.cs` and `TBDRPipelineSurgeonTypes.cs` still had Sequential layouts on shader/material/culling DTO rows. These records are copied into Vault, GPU buffers, telemetry rings, or indirect draw arguments. Hidden CLR offsets weaken shader ABI proof and can hide unaligned lanes in culling payloads.

Solution: Converted all fixed-size material response DTOs to explicit offsets: `InstanceMaterialDTO=16`, `MaterialPowerDTO=16`, `MaterialVisibleDTO=32`, `GlobalShaderConstantsDTO=48`, `MockBiomassDensitySignal=16`, `MaterialRuntimeScalarsDTO=16`, `TextureSetMappingDTO=16`, `WearRateDTO=16`, and `MaterialResponseTelemetryEntry=64`. Converted fixed TBDR rows to explicit offsets: budget/warning/mock quality rows, `PoiTransformDTO=112`, `MockCameraMatrix=128`, `AupGpuLocalizationInput=48`, streaming/telemetry/tuner/shader globals, and indirect draw args. `MockScatterBuffer` remains Sequential because it is a `NativeArray` wrapper aggregator, not a DTO.

Rejected Alternatives: Converting `MockScatterBuffer` to explicit layout was rejected because `NativeArray<T>` size and safety fields are Unity-owned. Widening `PoiTransformDTO=112` or `GlobalShaderConstantsDTO=48` to cache-line multiples was rejected because these are shader/GraphicsBuffer strides; widening requires shader ABI migration, not a layout safety patch. Replacing culling with CPU GameObject instantiation was rejected by DOD and render-pipeline bypass rules.

Scalability potential: Low tier keeps material and culling payload rows compact for Quest/mobile bandwidth. Middle tier preserves shader buffer ABI. High and Ultra tiers can spend saved CPU bandwidth on richer material response and HZB/indirect culling while staying on the same explicit DTO rows driven by continuous quality scalars.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-14 us per 10k material/culling row reads or uploads. Primary gain is shader/Vault stride proof and aligned long lanes in AUP GPU localization. Evidence class: STATIC_SOURCE; touched-file unaligned 8-byte scan returned 0 hits and `git diff --check` passed with CRLF warnings only.

## Decision 033 - Audio Virtualization Contract Explicit Closure

Problem: `AudioVirtualizationContracts.cs` still used Sequential layout for virtual voice ingress/state/selection/statistics/telemetry/tuning/mock rows. These are contract and Vault/DSP payloads, not Unity container wrappers. The editor smoke test also hard-coded old Sequential source needles, which would turn the new layout policy into a false failure.

Solution: Converted every Sequential record in `AudioVirtualizationContracts.cs` to explicit offsets while preserving existing sizes: `VirtualVoiceDTO=48`, `VirtualVoiceRequest=128`, `VirtualVoice=160`, `VirtualVoiceSortKey=16`, `VirtualVoiceSelection=144`, `VirtualVoiceStatistics=64`, `AcousticTelemetryEntry=64`, `VirtualVoiceTelemetryEntry=64`, `VirtualVoiceTuningSnapshot=32`, `AudioProfileCsvRow=32`, `AcousticEchoTap=144`, `MockAcousticEmitterSignal=96`, `MockPlayerInsideSubSignal=32`, `MockSDFSampler=64`, and `MockTerrainSampler=96`. Embedded `AcousticAup` lanes remain at 8-byte aligned offsets 0, 40, and 80. Updated `ShinobuAcousticDspSmokeTester` to assert Explicit source markers for the 48-byte voice DTO and 16-byte sort key.

Rejected Alternatives: Expanding `VirtualVoice=160` or `VirtualVoiceSelection=144` to larger cache-line multiples was rejected because the sorting path intentionally swaps 16-byte keys instead of full voice rows, and widening contract payloads would require a coordinated audio ABI migration. Leaving smoke tests on Sequential strings was rejected because it would preserve an obsolete policy assertion. Converting runtime audio jobs in the same pass was rejected because this patch is the contract surface, not job wrapper layout.

Scalability potential: Low tier keeps compact virtual voice rows and 16-byte sort keys for Quest-class audio virtualization. Middle tier preserves current physical/virtual voice buffer widths. High and Ultra tiers can scale richer DSP and acoustic telemetry over the same explicit rows while continuous `GlobalQualityWeight` drives budget decisions in existing resolver math.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-16 us per 10k virtual voice/sort/telemetry rows. Primary gain is contract ABI proof, aligned `AcousticAup` lanes, and removal of all source-owned Sequential layouts from the virtualization contract file. Evidence class: STATIC_SOURCE; Sequential and unaligned 8-byte scans returned 0 hits; `git diff --check` passed with CRLF warnings only.

## Decision 034 - Audio DSP Propagation Kernel DTO Explicit Closure

Problem: Several fixed audio DSP and propagation DTO/state rows still used Sequential layout: adaptive stem state/commands, echolocation ray hits, acoustic portal graph/query/result/telemetry rows, and depth-stress granular synthesis state. These records are kernel-facing or telemetry-facing payloads and do not embed Unity native containers.

Solution: Converted fixed rows in `AdaptiveStemAudioMixer.cs`, `AcousticEcholocationRaymarch.cs`, `AcousticPortalPropagation.cs`, and `DepthStressGranularSynthesisKernel.cs` to explicit offsets while preserving sizes. `AcousticAup` lanes in portal rows remain aligned at offsets 0/40/80. The sine oscillator keeps `double Phase` at offset 0 and 4-byte fields after it. Existing Burst jobs and NativeArray fields were not converted in this patch.

Rejected Alternatives: Converting `PlayerCriticalBufferJobs` job structs in the same pass was rejected because those are job wrappers with Unity collections and require a separate owner-aware review. Widening 56-byte echolocation hits or 104-byte portal results was rejected because these are existing buffer strides and widening would require consumer migration. Replacing the portal/SDF approximations with physical wave simulation was rejected by the Dear Lie rule.

Scalability potential: Low tier keeps compact DSP and portal rows for cheap acoustic feedback. Middle tier preserves current ray-hit and portal-result buffer widths. High and Ultra tiers can increase ray counts or portal expansion budgets using the same explicit DTO rows without changing binary layout.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-10 us per 10k audio DSP/propagation rows. Primary gain is static ABI proof, aligned `AcousticAup` and `double` lanes, and removal of Sequential layout from these fixed kernel DTO files. Evidence class: STATIC_SOURCE; touched-file Sequential and unaligned 8-byte scans returned 0 hits; `git diff --check` passed with CRLF warnings only.

## Decision 035 - Scanner Data Mining Route Explicit Closure

Problem: `ScannerDataMiningRouter.cs` still used Sequential layout for scan result, spatial entity, active scan state, mock input/tool, SDF occlusion, query stats, telemetry, and settings DTOs. These rows carry AUP, sector hash, depletion masks, progress, and telemetry fields; hidden offsets weaken scanner rollback/hash and ARM64 proof.

Solution: Converted every Sequential row in `ScannerDataMiningRouter.cs` to explicit offsets while preserving existing sizes from 16 to 128 bytes. `double3` AUP lanes stay at offsets 0 or 24, `long`/`ulong` sector and depletion lanes stay 8-byte aligned, and settings/query rows remain compact 4-byte scalar lanes.

Rejected Alternatives: Expanding `ScanResultDTO=48` or `ScannerSpatialEntityDTO=64` was rejected because scanner buffers likely use existing strides. Replacing scanner occlusion with Unity physics queries was rejected; the route remains on the cheap SDF/mock DTO path. Converting unrelated gameplay job wrappers in this pass was rejected because this slice is fixed scanner payload rows.

Scalability potential: Low tier keeps scanner route rows compact and deterministic. Middle tier preserves current candidate/query widths. High and Ultra tiers can spend additional budget on more candidate cells or VFX bias through existing settings rows without changing ABI.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per 10k scanner DTO rows. Primary gain is aligned AUP/depletion lanes and removal of all source-owned Sequential layout from the scanner route file. Evidence class: STATIC_SOURCE; scanner Sequential and unaligned 8-byte scans returned 0 hits; `git diff --check` passed with CRLF warnings only.
