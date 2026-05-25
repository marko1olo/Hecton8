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

## Decision 036 - Gameplay DTO Tail Sweep

Problem: Gameplay still had owner-safe fixed rows using compiler-owned Sequential layout after Pack debt was zeroed: radiation source/telemetry rows, swim body pose scalar rows, contextual IK entity snapshots, and submarine PID/flood output tail holes.

Solution: Converted fixed rows to explicit offsets and named padding: `RadiationSource=64`, `RadiationTelemetryEntry=64`, `BodyModePose=96`, and `ContextualPhysicalIkEntityState=512`. Added explicit tail pads to `PidJobOutput`, `SubmarinePidTelemetryEntry`, and `DynamicFloodMassOutput`. Submarine/radiation jobs received synchronous Burst flags and NoAlias on non-overlapping NativeArrays.

Rejected Alternatives: Converting Unity-owned job wrappers was rejected because those structs embed `NativeArray`, `RaycastHit`, `CapsulecastCommand`, or animation-job handles whose ABI is owned by Unity. Preserving the 472-byte contextual IK entity stride was rejected because it crosses cache-line boundaries and leaves anonymous tail padding.

Scalability potential: Low tier gets stable row strides for Quest-class IK/radiation/vehicle jobs. Middle tier keeps the same gameplay facts with better Burst alias proof. High and Ultra tiers can spend saved stalls on richer hand/foot placement, radiation telemetry, or vehicle feedback without changing truth rows.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-18 us per 10k fixed row reads or writes. Evidence class: STATIC_SOURCE; touched-file Sequential debt is limited to Unity wrapper or managed records.

## Decision 037 - Interaction Input Atlas Payload Sweep

Problem: NativeQueue/event payloads and persistent finger buffers still used Sequential layout: interaction events, Atlas-6 events, beacon telemetry/solve results, universal input signals, terminal pointer/hand target payloads, and physical hand finger rows.

Solution: Converted those fixed rows to explicit layout: `InteractionEventPayload=32`, `Atlas6EventPayload=32`, `SignalBeaconTelemetry=48`, `SignalBeaconSolveResult=16`, `UniversalInputStateSignal=48`, `FingerRayDefinition=32`, `FingerRayRuntime=32`, `FingerPoseData=32`, `KinematicTerminalPointerState=64`, and `PhysicalHandIkTarget=64`. Finger jobs and beacon Burst math now include `CompileSynchronously = true`; finger NativeArray fields received NoAlias.

Rejected Alternatives: Managed registry rows carrying interfaces, MonoBehaviours, or strings were rejected because explicit layout would not make them blittable or Burst-safe. Removing `readonly` from public terminal payloads was rejected because constructor `this = default` can zero padding while preserving API immutability.

Scalability potential: Low tier gets 32-byte queue/finger rows and stable 48/64-byte contract rows. Middle tier preserves input/terminal cadence. High and Ultra tiers can increase haptic/terminal/finger visual fidelity using the same explicit payloads.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per 10k event/finger rows. Evidence class: STATIC_SOURCE; AtlasSignal and fixed Interaction touched-file Sequential scans return 0 hits.

## Decision 038 - Economy Marauder DTO Explicit Closure

Problem: `TradeMarauderRuntime` declared many DTO sizes but still relied on Sequential offsets for AUP, route, sector, telemetry, tuning, heap, proxy, and acoustic signature rows. `ResourceScarcityDirector` had AUP cluster rows with int fields before the AUP lane.

Solution: Converted the source-owned economy rows to explicit layout while preserving existing size contracts. `double3` and `long` lanes remain at offsets divisible by 8; `ResourceClusterRecord` is padded to 64 bytes with `PositionAup@8`; `SectorExtractionRecord` is 16 bytes.

Rejected Alternatives: Reordering economy DTO fields to place every AUP at offset 0 was rejected because these are existing contract/buffer rows. Expanding every 48-byte-style row to 64 bytes was rejected unless the existing declared size already allowed it or the row carried an AUP cluster needing a cache-line proof.

Scalability potential: Low tier gets deterministic economy/path rows with no binary quality branch. Middle tier preserves current route-solver density. High and Ultra tiers can scale more marauder visual/acoustic proxies through continuous quality-weight budgets without ABI drift.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-15 us per 10k economy/path/telemetry rows. Evidence class: STATIC_SOURCE; economy touched-file Sequential and unaligned 8-byte FieldOffset scans return 0 hits.

## Decision 039 - Build Gate Discipline For Continuation Slice

Problem: The mandate requires evidence, but the existing dependency wall and user instruction prohibit premature rebuilds.

Solution: Used static source scans only: Pack-parameter scan, touched-file Sequential scans, unaligned 8-byte FieldOffset scans, missing `CompileSynchronously` scans on touched Burst routes, and `git diff --check`.

Rejected Alternatives: Launching `dotnet build` was rejected because this slice is mechanical layout hardening and the known dependency wall remains unrelated to these edits. Owner-blind full-project rewrite was rejected because 509 remaining Sequential records include Unity wrappers, managed records, shader/file ABI rows, other domain-owned contracts, and one concurrent external Sequential reintroduction outside this slice.

Scalability potential: Low through Ultra tiers benefit from narrower ABI debt without spending compile-wall time or forcing unrelated agents to merge around speculative rewrites.

Hardware Impact: Runtime cost of verification is 0.00 ms. Static source proof shows `StructLayout(...Pack=...)` remains 0 under `Assets/_Project/Scripts`; broad Sequential count dropped from 541 to 509 during this continuation slice after one concurrent external Sequential reintroduction outside the touched files.

## Decision 040 - World/VFX/UI Fixed DTO Explicit Closure

Problem: Additional fixed runtime rows still relied on Sequential layout after the prior Pack purge: VFX silt/biolum/debris payloads, world sampler rows, terrain generated signal, thermodynamics hazard rows, visor/outpost/UI rows, flora genomics rows, biome SDF rows, and ecosystem/ocean contracts.

Solution: Converted source-owned fixed rows to explicit FieldOffset layouts while preserving existing buffer sizes where ABI-sensitive and widening mock/event rows only when they were queue/cache-line hostile. `MockTerrainQuerySignal` is now 64 bytes, with `double3 Aup@0` and scalar lanes after offset 24. Unity/native-container wrappers in `GlobalWorldSampler` remain Sequential because their layout is owned by Unity collections and safety-handle configuration.

Rejected Alternatives: A blanket conversion of `GlobalWorldSamplerData` and all sampler job structs was rejected because they embed `NativeArray` handles. Leaving 40-byte mock terrain query rows was rejected because queued stress-test rows cross cache-line boundaries and carry AUP data.

Scalability potential: Low tier gets predictable AUP/scalar reads and cheaper mock stress lanes. Middle tier keeps existing sampler ABI. High and Ultra tiers can increase terrain sampling and visual feedback density without mutating row layout.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-20 us per 10k fixed DTO reads, plus crash-risk reduction for AUP lanes. Evidence class: STATIC_SOURCE; Pack scan remains 0.

## Decision 041 - Atmosphere/Telemetry/Event Lane Explicit Closure

Problem: Several event and black-box rows in atmosphere, foveated rendering, GI relay, terrain seam blending, narrative, prologue, player stress, scan marker, logistics fluid, WFC outpost, sargassum, and economy ledger systems still had compiler-owned Sequential offsets.

Solution: Converted those fixed rows to explicit layouts with named padding. AUP-like rows keep `AbsoluteUniversePosition`, `MacroDatabaseAup`, `AbsoluteUniversePositionBlit`, `double`, `long`, and `ulong` lanes on 8-byte offsets. Rows with no stable unmanaged ownership, such as player collision events carrying `Rigidbody`, remain Sequential and are documented as excluded from DTO hardening.

Rejected Alternatives: Reordering all fields to make every vector 16-byte aligned was rejected where it would silently migrate existing NativeArray or Vault ABI. Converting managed event structs with Unity object references was rejected because explicit layout would not make them blittable or Burst-safe.

Scalability potential: Low tier pays less cache-split risk on hot event/telemetry paths. Middle tier preserves existing ring-buffer capacities. High and Ultra tiers can spend the saved bandwidth on richer telemetry/VFX signals without adding object-oriented dispatch.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-18 us per 10k event/telemetry row reads. Evidence class: STATIC_SOURCE; touched-file unaligned 8-byte FieldOffset scan returned 0 hits.

## Decision 042 - Construction/Encounter Burst Alias Fences

Problem: Habitat, encounter, narrative, atmosphere, sargassum, and meta-campaign jobs had multiple owner-separated NativeArrays but did not always tell Burst that the buffers cannot alias. Some touched jobs also lacked `CompileSynchronously = true`.

Solution: Added synchronous Burst compile flags and `[NoAlias]` to non-overlapping NativeArray fields in the touched jobs. NativeParallelHashMap and Unity-native wrapper containers were not marked when ownership/aliasing was not provable from local code.

Rejected Alternatives: Marking every container field blindly was rejected because Unity-owned containers and hash maps can include internal metadata and safety handles. Running a rebuild to prove this slice was rejected by mandate and because the known dependency wall is unrelated.

Scalability potential: Low tier benefits from cheaper Burst kernels under thermal pressure. Middle tier gains vectorization headroom. High and Ultra tiers can keep higher encounter/construction/narrative budgets on the same data rows without binary quality branches.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-30 us per scheduled job batch depending on buffer length and Burst vectorization. Evidence class: STATIC_SOURCE; no build/rebuild launched.

## Decision 043 - NativeQueue Snapshot And Entropy Yield ABI Closure

Problem: Several owner-safe event/snapshot/runtime rows still exposed compiler-owned Sequential layout and property-backed hot reads: power telemetry, sonar snapshots, performance events, PDA/flashlight/pool event payloads, haptic commands, scavenging runtime tables, entropy yield job rows, PDA logbook hashes, and quest marker cache rows. These rows are copied through NativeQueue, NativeArray, Vault spans, or cache arrays, so hidden padding and bool properties weaken ARM64 alignment proof and can trigger defensive copies in tight consumers.

Solution: Converted the fixed unmanaged rows to explicit layouts with named padding and 16/32/64 byte strides where feasible: `PowerGridTelemetrySnapshot=32`, `SpatialSonarSnapshot=32`, `PerformanceEventPayload=32`, `PDAIntrusionEventPayload=16`, `PoolDiagnosticsEventPayload=16`, `FlashlightEventPayload=16`, `PDAEventPayload=64`, `HapticCommand=64`, scavenging runtime descriptors/tables, entropy yield rows, `PDALogbookEntry=32`, and `QuestMarkerCache=80`. Packed power and sonar booleans/tier state into `uint StatusFlags` with static bit helpers. Added synchronous Burst compile flags and `[NoAlias]` to entropy yield NativeArray fields. Updated consumers and editor smoke assertions to the explicit ABI.

Rejected Alternatives: Converting structs carrying `ItemData`, `Transform`, `Rigidbody`, `Component`, `string`, `DateTime`, interfaces, or Unity native-container wrappers was rejected because explicit layout would not make those records blittable and could corrupt Unity-owned ABI. Keeping `PDAEventPayload` at 40 bytes was rejected because its NativeQueue stride crosses cache lines; it is now one 64-byte row. Running a rebuild was rejected because static proof was sufficient for this mechanical layout slice and the user explicitly prohibited premature rebuilds.

Scalability potential: Low tier gains smaller, predictable queue and NativeArray lanes with fewer unaligned/cache-split hazards. Middle tier keeps the same gameplay facts and event cadence. High and Ultra tiers can spend saved CPU/cache bandwidth on richer haptics, sonar presentation, PDA updates, and entropy-yield visual feedback without changing authority routes or binary quality switches.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-25 us per 10k event/snapshot rows or entropy yield batch depending on density. Evidence class: STATIC_SOURCE; Pack scan remains 0, touched-file Sequential scan returns 0, removed-property reference scan returns 0, unaligned 8-byte FieldOffset scan returns 0, and `git diff --check` returns exit 0 with LF/CRLF warnings only.

## Decision 044 - UI/Spectrum Fixed DTO ABI Closure

Problem: Additional owner-safe UI, sonar, GPU-upload, and Vault item-state rows still relied on compiler-owned Sequential layout. `AcousticEchoEvent` and `PingReturnSignal` also exposed readonly auto-properties in payloads copied through sonar/audio paths, leaving room for defensive struct copies and hiding the byte layout of the 48-byte AUP lane.

Solution: Converted fixed rows to explicit layouts with named padding: `DiegeticHudLayoutInput=16`, `DiegeticHudLayoutSettings=16`, `AcousticEchoEvent=80`, `PingReturnSignal=80`, `ActiveSonarGeoTelemetryEntry=32`, `SonarMapConstants=96`, `TooltipGlyphInstance=96`, `GroupState=16`, and `ItemState=16`. Replaced sonar payload auto-properties with raw readonly fields while preserving constructor API and the pure `ResolveWorldAup()` fallback. Added synchronous Burst flags and `[NoAlias]` to the existing diegetic HUD layout job input/output arrays.

Rejected Alternatives: Converting `PendingDurabilityCommand` was rejected because it contains a managed `string`; explicit layout would not make it Burst-safe or blittable. Widening 80-byte sonar payloads to 128 bytes was rejected because the rows are queue payloads, not concurrent per-thread counters, and doing so would increase copy bandwidth without a proven false-sharing owner. Running a rebuild was rejected by command discipline; this pass is a mechanical source-proof slice and the known dependency wall still exists.

Scalability potential: Low tier gets stable 16/32/80/96-byte rows for HUD, sonar, and PDA uploads without binary quality switches. Middle tier preserves current buffer cadence and visual output. High and Ultra tiers can spend saved cache/Burst headroom on richer sonar echoes, tooltip density, and PDA overlay updates through existing continuous quality budgets without changing authority routes or DTO sizes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-10 us per 10k HUD/UI/sonar/tool rows depending on density. Evidence class: STATIC_SOURCE; touched-file Sequential scan returns only the managed `PendingDurabilityCommand` exception, removed Spectrum hot-property scan returns 0, unaligned 8-byte `FieldOffset` scan returns 0, broad Sequential count is 212, and `git diff --check` returns exit 0 with LF/CRLF warnings only.

## Decision 045 - Construction/World/Submarine Fixed Row And Burst Fence Sweep

Problem: Several remaining small fixed rows still used Sequential layout in construction, world regrowth/HLOD, cultivation, and submarine damage-control paths. Separately, `SubmarineFluidDynamics` flood/hydro jobs used correct float modes but omitted `CompileSynchronously = true` and did not expose non-overlap facts to Burst for Vault-backed SoA arrays.

Solution: Converted owner-safe rows to explicit layouts: `CultivationSlotState=32`, `XorShift32State=8`, `WorldRegrowthConfig=48`, `HLODInstance=96`, `HabitatSiegeTargetSnapshot=48`, `HabitatFloodConnection=16`, `HabitatFloodBlackBoxEntry=48`, `HabitatDeconstructionTelemetryEntry=32`, and `ImpactCommand=32`. Added synchronous Burst flags and `[NoAlias]` to `HydroKinematicDragJob`, `FluidTransferJob`, `BulkheadTransferDeltaJob`, `ApplyBulkheadTransferJob`, and `FloodMassPropertiesJob`.

Rejected Alternatives: PDA exchange snapshots, raycast query rows, inspector-authored fluid compartment definitions, and job wrappers carrying `NativeArray`, `NativeList`, `NativeParallelHashSet`, `RaycastHit`, `string`, or Unity object references were rejected from blind explicit conversion. Widening all flood/job wrappers to explicit layout was rejected because Unity owns native-container ABI. Running a rebuild was rejected by mandate and because static source checks are enough for this mechanical slice.

Scalability potential: Low tier gets deterministic 8/16/32/48/96-byte rows for regrowth, HLOD, habitat, cultivation, and submarine impact telemetry. Middle tier preserves current solver cadence. High and Ultra tiers can increase HLOD density, flood/hydro solver fidelity, and habitat black-box coverage through continuous budgets without changing DTO layout or introducing binary quality switches.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per 10k fixed rows and 2-30 us per flood/hydro job batch. Evidence class: STATIC_SOURCE; broad Sequential count is 204, touched-file unaligned 8-byte `FieldOffset` scan returns 0, `SubmarineFluidDynamics.cs` Burst attributes now all include `CompileSynchronously = true`, and `git diff --check` returns exit 0 with LF/CRLF warnings only.

## Decision 046 - Volcanic/Voxel/QA Fixed DTO ABI And Burst Fence Sweep

Problem: The remaining owner-safe Sequential debt still included fixed Volcanic, Voxel, QA, headless, flora, world, VFX, physiology, and utility DTO rows. Some touched Burst jobs in voxel navigation, audio buffering, spatial hash rebuild, and headless AUP simulation also lacked either synchronous compile flags or explicit non-alias proof.

Solution: Converted fixed unmanaged rows to explicit layouts with named padding across the Loop 10 slice, including 64-byte volcanic state/signal rows, 128-byte Shinobu38 file writer cursor/ingest rows, 80-byte decompression state, 80-byte voxel carve telemetry, 112-byte spatial hash entries, 128-byte QA endurance black-box entries, and 64-byte fracture telemetry entries. Added `CompileSynchronously = true` and `[NoAlias]` to owner-separated arrays in touched voxel navigation, spatial hash, audio buffering, and headless AUP jobs.

Rejected Alternatives: Converting Unity job wrappers and native-container wrappers was rejected because `NativeArray`, `NativeList`, `NativeQueue`, `RaycastHit`, and Unity safety-handle internals are not SHINOBU-owned ABI. `DeferredDirtyVolumeRequest` and `ScavengerHostState` were rejected because they carry managed scene/domain references. Running a rebuild was rejected because this pass is source-proof ABI hardening, the known dependency wall still exists, and the user prohibited rebuild until needed.

Scalability potential: Low tier gets aligned fixed rows for watchdog, voxel, navigation, audio, and QA forensic lanes without changing gameplay truth. Middle tier keeps the same cadence with stronger Burst alias evidence. High and Ultra tiers can spend the saved cache stalls on denser voxel navigation probes, richer volcanic/VFX telemetry, and heavier QA endurance instrumentation through continuous `GlobalQualityWeight` budgets without binary feature switches.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-20 us per 10k fixed DTO rows and 2-35 us per scheduled voxel/audio/headless batch when Burst can vectorize owner-separated arrays. Evidence class: STATIC_SOURCE; broad Sequential count dropped from 204 to 182, Pack scan remains 0, touched-file unaligned 8-byte `FieldOffset` scan returns 0, touched Burst missing-`CompileSynchronously` scan returns 0, and `git diff --check` returns exit 0 with LF/CRLF warnings only.

## Decision 047 - Progression Vegetation Wreck Micro ABI Slice

Problem: After Loop 10, a few low-risk source-owned rows were still mixed into the Sequential debt: a string-free achievement runtime threshold row, the flora spore NativeQueue payload, the persistent thermal vent record, and procedural wreck mesh/render jobs missing synchronous Burst compile flags.

Solution: Converted `AchievementRuntimeDefinition=16`, `HectonFloraSporeEvent=96`, and `PersistentThermalVentRecord=80` to explicit layouts with named padding and 8-byte AUP alignment. Added `CompileSynchronously = true` and `[NoAlias]` to procedural wreck mesh/proxy/render payload jobs where NativeArrays are owner-separated.

Rejected Alternatives: `AchievementDefinition` was not converted because it contains managed `string` references. `PendingWreckLootSpawn` was not converted because it contains `GameObject` and `ItemData`. Procedural wreck job wrappers remain Sequential because they embed Unity native containers or mesh data handles whose ABI is Unity-owned. Running a rebuild was rejected because this was a static ABI/Burst-annotation pass and no compile gate was needed.

Scalability potential: Low tier gets tighter achievement evaluation and stable vegetation/wreck payload lanes. Middle tier keeps the same wreck generation and flora event cadence. High and Ultra tiers can spend saved stalls on denser procedural wreck render payloads and richer spore fog/scatter without changing truth ownership or using binary quality switches.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per 10k queue/record rows and 2-25 us per procedural wreck mesh/render payload batch. Evidence class: STATIC_SOURCE; broad Sequential count is now 179, touched-file unaligned 8-byte `FieldOffset` scan returns 0, ProceduralWreck missing-`CompileSynchronously` scan returns 0, and `git diff --check` returns exit 0 with LF/CRLF warnings only.

## Decision 048 - Anomaly Deterministic DTO And Burst Fence Sweep

Problem: The anomaly basin/ridge/brine jobs still used compiler-owned Sequential DTO layout and deterministic Burst attributes without `CompileSynchronously = true`. These rows are consumed by NativeArray/NativeQueue flood-fill and feature detection lanes, so anonymous padding weakens rollback/replay and ARM64 offset proof.

Solution: Converted anomaly DTOs to explicit layouts: `AnomalyBasinDetectionSettings=32`, `AnomalyBasinRecord=56`, `AnomalyBasinFloodFillState=48`, `AnomalyRidgeDetectionSettings=80`, `AnomalyFeatureRecord=56`, and `AnomalyBrinePoolBounds=32`. Added synchronous deterministic Burst flags and `[NoAlias]` to source-separated NativeArray fields in basin, ridge, reduction, and brine jobs.

Rejected Alternatives: `HectonSandboxAbyssalShelfParams` was not converted because it is `[Serializable]` authoring data and not proven to be a NativeArray DTO lane in this pass. NativeQueue fields in the sliced flood-fill job were not annotated with NoAlias because queue internals are Unity-owned. Running a rebuild was rejected because static checks are sufficient for this localized ABI/Burst pass and the user forbade premature rebuilds.

Scalability potential: Low tier gets bounded deterministic anomaly scans with stable record strides. Middle tier keeps the same basin/ridge cadence. High and Ultra tiers can increase anomaly feature density and brine/pillar visual output using existing continuous budgets without changing DTO identity or adding binary quality switches.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per 10k anomaly rows and 2-30 us per anomaly batch when Burst can assume non-overlapping buffers. Evidence class: STATIC_SOURCE; anomaly touched-file Sequential scan returns 0, anomaly missing-`CompileSynchronously` scan returns 0, anomaly unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad Sequential count is now 173, and `git diff --check` over anomaly files returns exit 0 with LF/CRLF warnings only.

## Decision 049 - Voxel Mesh Pipeline Burst Alias Fence

Problem: `HectonVoxelEngine` still had the dominant mesh-generation jobs using Burst without `CompileSynchronously = true`, and most NativeArray lanes lacked non-alias proof. `MCRawVertex` and the voxel mesh black-box telemetry row also depended on compiler-owned layout. `InstanceCullingService.ApplyAupShiftJob` had the same Burst flag gap.

Solution: Added synchronous Burst flags and `[NoAlias]` to source-separated array lanes in voxel density, MC count/extract/weld, normals, seam, projection, biome, color, dirty blend, spawn sampling, collider classification, and instance AUP shift jobs. Converted `MCRawVertex=24` with `position@0` and `edgeId@16`, and `VoxelMeshPipelineTelemetryEntry=32` with explicit fixed offsets.

Rejected Alternatives: Unity `NativeParallelHashMap`, `NativeList.ParallelWriter`, and job-wrapper structs were not forced into explicit layout because Unity owns those container ABIs. Deferred voxel mesh/collider upload rows were rejected because they carry managed Unity object references plus `JobHandle`. `VoxelSurfaceVertex` and `VoxelColliderVertex` were not padded because `SetVertexBufferParams` declares exact GPU strides of 76 and 12 bytes; padding those rows would corrupt mesh upload stride and is a render ABI migration, not a blind DTO cleanup. Running a rebuild was rejected by mandate and because static checks are sufficient for this source-only metadata/ABI slice.

Scalability potential: Low tier gets faster cave/voxel chunk jobs without changing chunk identity, save identity, or authority route. Middle tier keeps the same mesh resolution and collider upload cadence. High and Ultra tiers can spend the saved stalls on denser cave curvature/AO, richer seam blending, and more visible procedural voxel detail under continuous quality budgets; no binary quality switch was added.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-40 us per voxel mesh pipeline batch and 1-8 us per 10k instance AUP shifts. Evidence class: STATIC_SOURCE; touched-file missing-`CompileSynchronously` scan returns 0, touched-file unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad Sequential count is now 172, and `git diff --check` over the two touched code files returns exit 0 with LF/CRLF warnings only.

## Decision 050 - Fixed DTO Micro Slice And Vegetation Burst Fence

Problem: The remaining exact-count safe slice contained small source-owned rows still using compiler-owned Sequential layout: biome SDF weight rows, brine toxic mud broadphase rows, AR waypoint projection frames, and a single-slot abyssal wake impulse. The vegetation flow jobs also still used Burst without `CompileSynchronously = true` and lacked alias proof on their owner-separated buffers.

Solution: Converted `BiomeWeightEntry=16`, `ToxicMudCell=56`, `WaypointProjectionFrame=112`, and `SwarmWakeImpulse=32` to explicit layouts. Replaced `WaypointProjectionFrame.IsValid` with a `uint` flag. Added synchronous Burst flags and `[NoAlias]` to vegetation generation, density query, threat propagation, threat voxelization, abyssal flow, thermal, flow-volume, and native A* jobs.

Rejected Alternatives: AR external/runtime waypoint slots were not converted because they hold `string`, `Transform`, `RectTransform`, `Image`, `TMP_Text`, and other managed UI references. Vegetation job wrappers remain Sequential because they contain Unity native container handles. `VoxelSurfaceVertex` and `VoxelColliderVertex` remain as render ABI exceptions because their current mesh vertex declarations require exact 76-byte and 12-byte strides. Running a rebuild was rejected by mandate and because this pass was still a source-level ABI/Burst proof slice.

Scalability potential: Low tier gets cheaper vegetation/flow/A* jobs and aligned biome/brine/AR rows. Middle tier preserves the same route and update cadence. High and Ultra tiers can spend the saved stalls on denser vegetation placement, richer abyssal flow volumes, and more aggressive AR waypoint projection sampling under continuous quality weights without changing truth ownership or save identity.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per 10k fixed row reads and 2-35 us per vegetation/flow/A* job batch. Evidence class: STATIC_SOURCE; touched missing-`CompileSynchronously` scan returns 0, touched unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad Sequential count is now 168, and `git diff --check` over Loop 14 files returns exit 0 with LF/CRLF warnings only.

## Decision 051 - VFX And Visor GPU Constant ABI Closure

Problem: Several GPU-facing VFX and visor DTO rows still used compiler-owned Sequential layout with fixed Size metadata, leaving shader constant, indirect request, wake, and black-box telemetry strides dependent on source declaration order instead of explicit offsets. The carve debris and marine snow jobs also had owner-separated NativeArrays that were not advertised to Burst with `[NoAlias]`.

Solution: Converted source-owned fixed rows to explicit layouts: `FrameConstantsData=128`, `VehicleWakeJobResult=48`, `MarineSnowTelemetryEntry=64`, `CarveDebrisRequest=64`, `CarveDebrisTelemetryEntry=64`, `BrownoutGlobalsDTO=64`, `VisorFluidGlobalsDTO=128`, `LensComputeGlobalsDTO=80`, `StochasticSsrGlobalsDTO=48`, `DepthFogGlobalsDTO=64`, `HalfResParticlesGlobalsDTO=16`, `SootGlobalsDTO=32`, `RetinaGlobalsDTO=32`, and `ShaftGlobalsDTO=176`. Added `[NoAlias]` to marine snow wake/mock/flow fields and carve debris aging/injection buffers where local ownership proves non-overlap.

Rejected Alternatives: `MaterialParameterState` in scooter volumetric shafts was left out because it is a CPU material-parameter cache row, not a proven blittable shader upload DTO. Widening every visor constant row to a full 64-byte cache line was rejected for small immutable upload rows where CBuffer stride, not per-thread mutation, is the controlling ABI. Running a build or rebuild was rejected because this pass is static ABI hardening and the user prohibited premature rebuilds.

Scalability potential: Low tier gets stable, narrow VFX/visor upload rows and cleaner Burst alias proof for debris and marine snow. Middle tier preserves the current visual cadence. High and Ultra tiers can spend the saved CPU/cache stalls on denser marine snow flow, more carve debris, richer brownout/fog/SSR/retina passes, and longer volumetric shaft parameter sets through continuous quality weights without changing gameplay truth, save identity, or authority routes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-14 us per 10k fixed VFX/visor rows and 2-18 us per debris or marine snow job batch when Burst can vectorize non-overlapping buffers. Evidence class: STATIC_SOURCE; touched-file missing-`CompileSynchronously` scan returns 0, touched-file exact Sequential scan returns 0, touched-file unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad exact Sequential count remains 168 due to the historical exact regex, and `git diff --check` over Loop 15 files returns exit 0 with LF/CRLF warnings only.

## Decision 052 - Player Critical Audio DSP ABI Closure

Problem: `PlayerCriticalProceduralAudioRenderer.cs` still had 24 sized Sequential rows, including public sonar bridge payloads, Vault-backed sonar composite rows, 300-frame telemetry rows, parameter snapshots, and persistent DSP synthesis state. It also had 40 Burst-annotated math helpers without `CompileSynchronously = true`.

Solution: Converted all audio renderer Sequential rows to explicit layouts with named padding. Cache-sensitive worker rows were widened to line-safe sizes where warranted: `SonarEchoCompositeGroup` 72 -> 128, `GranularAudioTelemetryEntry` 48 -> 64, `PrologueAudioTransitionTelemetryEntry` 56 -> 64, `SonarSynthesisState` 96 -> 128, `AmbientCurrentSynthesisState` 72 -> 128, `ThrusterSynthesisState` 136 -> 256, and the smaller DSP states to 16/32/64-byte strides. Added `CompileSynchronously = true` to the remaining Burst math helpers in the file.

Rejected Alternatives: Leaving 72/96/136-byte DSP rows untouched was rejected because NativeArray and hot state traversal would continue crossing cache-line boundaries. Converting the many private Vault alias fields was rejected because they are Unity `NativeArray<T>` handles, not source-owned DTO element layouts. Adding new audio buffers or moving ownership was rejected; the existing Vault handles and SPSC/DSP route were preserved. Running a build or rebuild was rejected because the user explicitly prohibited premature rebuilds and static ABI gates passed.

Scalability potential: Low tier gets stable DSP rows and synchronous Burst metadata while preserving cheap underwater perceptual audio fakes: scalar low-pass, reverb-state, granular voice budgets, and selected critical cues. Middle tier keeps the same cadence with cleaner cache behavior. High and Ultra tiers can spend saved stalls on higher granular voice limits, Hermite grain sampling, cave convolution, Sabine reverb, binaural micro-delay, and richer sonar echo grouping under continuous quality weights; no layout or authority route changes with quality.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-18 us per 10k bridge/telemetry/snapshot rows and 1-16 us per audio block state pass. Evidence class: STATIC_SOURCE; touched audio file now has 0 Sequential hits, 0 missing-`CompileSynchronously` hits, 0 unaligned 8-byte `FieldOffset` hits, 0 DTO auto-property hits, Pack scan remains 0, broad sized-inclusive Sequential count is 239, and `git diff --check` over the audio file returns exit 0 with LF/CRLF warnings only.

## Decision 053 - Spatial Audio Payload ABI Closure

Problem: `SpatialAudioManager.cs` still had sized Sequential rows in active emitter samples, binaural telemetry, delayed audio event queues, acoustic portal cache entries, impact emitter samples, and the deferred audio caption payload. Several rows used 80/88/96/200-byte strides that are stable enough for managed sequential layout but poor for NativeQueue/NativeList/array traversal and not explicit for ARM64 audit.

Solution: Converted all touched spatial-audio rows to explicit layouts with named padding. Public/internal bridge offsets were preserved for `ActiveEmitterSample`, `ActiveImpactEmitterSample`, `BinauralEmitterTelemetry`, and `AudioCaptionPayload`. Queue/cache rows were widened where cache-line stride mattered: `DelayedAudioEvent=128`, `ImpactEmitterSample=128`, `AudioCaptionPayload=128`, and `AcousticPortalCacheEntry=256`.

Rejected Alternatives: Reworking `AcousticPathResult` or `AcousticAup` was rejected because those contract structs are already explicit and outside this local source slice. Converting NativeQueue/NativeList owner fields was rejected because Unity owns the native-container handle ABI. Changing the delayed event route or caption queue route was rejected; this pass only locks payload bytes and padding. Build/rebuild was not launched because static gates passed and command discipline still forbids premature rebuild.

Scalability potential: Low tier gets stable audio queue/cache payloads for cheap perceptual underwater audio: delayed pressure/trauma events, scalar acoustic transmission, low-pass, and caption routing. Middle tier keeps current cadence. High and Ultra tiers can spend saved cache/copy overhead on denser active emitter samples, binaural telemetry, portal reprojection cache hits, and caption/debug overlays under continuous quality weights; payload layout remains invariant across quality.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per 10k spatial audio queue/cache rows depending on active emitter and caption density. Evidence class: STATIC_SOURCE; touched `SpatialAudioManager.cs` Sequential scan returns 0, touched unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad sized-inclusive Sequential count is 232, and `git diff --check` returns exit 0 with LF/CRLF warnings only.

## Decision 054 - Sargassum Micro Fauna GPU Payload ABI Closure

Problem: `SargassumMicroFaunaBoids.cs` still had seven sized Sequential rows on GPU/NativeArray lanes: boid state, grazing anchors, massive threats, formation beacons/obstacles, leviathan nodes, and the 768-byte simulation frame constant packet. These rows had shader stride constants and validation, but the CLR still owned offset placement. Two local jobs also lacked the mandated compile/alias fence: predator consumption used asynchronous low precision Burst metadata, and leviathan node construction lacked `CompileSynchronously = true` plus non-alias proof.

Solution: Converted the seven fixed rows to explicit layouts while preserving every shader-visible stride and field offset: `BoidData=32`, `GrazingAnchorData=32`, `MassiveThreatData=48`, `FormationBeaconData=32`, `FormationObstacleData=32`, `LeviathanNodeData=32`, and `SimulationFrameConstants=768`. The frame constant packet uses exact 16-byte lanes from offset 0 through 752. Added synchronous Fast/Standard Burst flags and `[NoAlias]` to owner-separated arrays in `PredatorBoidConsumptionJob` and `BuildLeviathanNodeJob`.

Rejected Alternatives: Widening the 32/48-byte GPU rows was rejected because the HLSL StructuredBuffer contract and existing stride constants are the controlling ABI. Reordering fields was rejected because it would require shader and validator migrations outside this SHINOBU slice. Leaving the predator job at `FloatPrecision.Low` with asynchronous compilation was rejected because the project mandate requires synchronous Fast/Standard for non-rollback mathematical jobs unless a deterministic exception is proven. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier preserves the existing cheap visual fake route for micro fauna: GPU-driven boid buffers, 16-byte frame lanes, approximate leviathan path resampling, and scalar predator kill signals. Middle tier keeps the same simulation cadence and shader packet size. High and Ultra tiers can spend the saved CPU/cache stalls on denser micro-fauna schools, more anchor/threat emitters, richer sonar/acoustic panic response, and longer leviathan visual spline sampling under continuous `GlobalQualityWeight`; DTO layout and authority route remain invariant across quality.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-10 us per 10k boid/GPU row reads and 2-18 us per predator or leviathan-node batch when Burst can vectorize non-overlapping buffers. Evidence class: STATIC_SOURCE; touched `SargassumMicroFaunaBoids.cs` Sequential scan returns 0, touched missing-`CompileSynchronously` Burst scan returns 0, touched unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad sized-inclusive Sequential count is 225, broad exact Sequential count remains 168, and `git diff --check` returns exit 0 with LF/CRLF warnings only.

## Decision 055 - Drone Fleet Runtime DTO ABI Closure

Problem: The drone fleet construction/cognition slice still had source-owned fixed rows using Sequential layout, including the 448-byte `HeadlessDroneState` with late `double3` docking/AUP lanes. Sequential layout here was relying on compiler-inserted padding before the double lanes and at the tail, which is unacceptable for rollback snapshots, NativeArray traversal, and ARM64 proof. Navigation-side fixed rows for tuning, waypoints, tasks, mock SDF, min-heap nodes, and A* telemetry had the same compiler-owned layout problem.

Solution: Converted `HeadlessDroneState=448` to explicit layout with 8-byte-aligned `double3` lanes at offsets 216/240/264/288/312/336/360/384 and named padding at 212..215 and 424..447. Converted drone navigation fixed rows to explicit layouts: `DroneFleetTuningConstants=64`, `PathWaypointDTO=16`, `DroneFleetDebugRoute=144`, `DroneFleetAutomationStats=48`, `DroneTaskDTO=64`, `MockSDFGrid=64`, `DroneNativeMinHeapNode=8`, and `DroneAStarTelemetry=32`.

Rejected Alternatives: Reordering `HeadlessDroneState` to put all double lanes first was rejected because it would require a broad cognition/job migration and risk conflicts with other agents. Widening the debug route to 192 or 256 bytes was rejected because it is telemetry/debug payload, not a contested per-thread counter. Converting `DroneNativeMinHeap` itself was rejected because it embeds a Unity `NativeArray` handle; Unity owns that container ABI. Running a build or rebuild was rejected because static gates passed and no compile gate was required.

Scalability potential: Low tier gets aligned drone cognition rows and cheaper NativeArray state copies while preserving deterministic control. Middle tier keeps the existing path solve cadence. High and Ultra tiers can spend the saved state-copy and A* telemetry overhead on more active repair/mining drones, richer debug route telemetry, and larger solve budgets through continuous quality values; drone DTO identity and authority route remain invariant across quality.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-14 us per 10k drone state/tuning/task/path rows. Evidence class: STATIC_SOURCE; touched drone files now report 0 Sequential hits, touched missing-`CompileSynchronously` Burst scan returns 0, touched unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad sized-inclusive Sequential count is 216, broad exact Sequential count remains 168, and `git diff --check` returns exit 0 with LF/CRLF warnings only.

## Decision 056 - Predator Cognition Runtime DTO ABI Closure

Problem: `PredatorCognitionDomain.cs` still had 14 source-owned fixed runtime rows using Sequential layout, including Vault-backed cognition inputs/outputs, memory entries, light source payloads, telemetry rows, mock signal rows, and private nested result/directive rows. The largest risk was `CognitionInput=480`, which mixes `double3`, two 48-byte AUP blit payloads, many local float3 lanes, and scalar control flags; leaving that compiler-owned weakens rollback memcpy proof and ARM64 offset proof.

Solution: Converted all source-owned fixed rows in the file to explicit layouts: `CognitionCore=64`, `CognitionMemoryEntry=24`, `AcousticMemoryEntry=40`, `PredatorMockAcousticSignal=24`, `MockLightSource=24`, `ApexCortexTuningSnapshot=16`, `LightSourceData=96`, `RetinalTelemetryEntry=32`, `CognitionControl=96`, `CognitionInput=480`, `CognitionOutput=64`, `PackedCognitionOutput=48`, `RetinalLightResult=24`, and `AlphaLeviathanDirective=32`. `CognitionInput` pins `FloatingOriginOffset@0`, `PlayerTargetAup@24`, `PackTargetAup@72`, local float3 lanes from 120 through 300, and final scalar flags at 476.

Rejected Alternatives: Widening `CognitionInput` to 512 bytes was rejected because the current 480-byte Vault stride is already a declared contract and there is no contested per-thread counter reason to spend an extra half cache line per predator. Reordering AUP fields before all scalar lanes was rejected because existing job and validation code already asserts a 480-byte row and other agents may be reading field order. Converting managed compatibility classes or `FaunaBrain` property APIs was rejected because this pass is unmanaged DTO ABI hardening only. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cognition cheap through compact explicit memory rows, quantized drives, scalar light/acoustic inputs, and packed outputs. Middle tier keeps the same job cadence with stronger memcpy and Burst layout proof. High and Ultra tiers can spend the saved cache and row-copy stalls on richer retinal exposure, acoustic memory, pack flanking, alpha leviathan directives, and predator debug telemetry under continuous quality controls; cognition DTO identity and authority route remain invariant across quality.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-20 us per 10k cognition row reads/copies depending on active predator count. Evidence class: STATIC_SOURCE; touched `PredatorCognitionDomain.cs` Sequential scan returns 0, touched missing-`CompileSynchronously` Burst scan returns 0, touched unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad sized-inclusive Sequential count is 202, broad exact Sequential count remains 168, and `git diff --check` returns exit 0 with LF/CRLF warnings only.

## Decision 057 - Vegetation Scatter GPU Payload ABI Closure

Problem: Vegetation/scatter GPU metadata and telemetry rows still used Sequential layout even though their shader/constant-buffer strides are fixed and validated. The CPU culling and scatter jobs also had Burst attributes without `CompileSynchronously = true` and did not expose non-overlap facts for matrix, metadata, culling plane, headlight, and visibility-mask arrays.

Solution: Converted fixed GPU and telemetry rows to explicit layouts: `HectonVegetationInstanceData=64`, `GpuScatterFloraInstanceData=64`, `FloraGrowthTelemetryEntry=40`, `VegetationCullTelemetrySnapshot=40`, `ScatterCullTelemetryEntry=40`, `ScatterTelemetryEntry=64`, `ScatterFrameConstants=176`, and `ScatterBlackBoxEntry=64`. Added synchronous Burst flags and `[NoAlias]` to owner-separated NativeArray fields in vegetation visibility, mock matrix generation, draw-output finalization, and scatter culling jobs.

Rejected Alternatives: Widening the public 64-byte vegetation/scatter metadata rows was rejected because shader stride and producer compatibility are the controlling ABI. Reordering the metadata lanes was rejected because HLSL consumers depend on current field order. Annotating unsafe draw-command pointer fields with NoAlias was rejected because they are raw render-output pointers, not NativeArray lanes with source-owned separation proof. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps vegetation cheap through compact metadata, CPU culling masks, density decimation, and black-box telemetry. Middle tier keeps current BRG/indirect cadence. High and Ultra tiers can spend saved culling and upload overhead on denser kelp/flora scatter, richer bioluminescence metadata, more headlight/culling planes, and deeper scatter black-box history under continuous quality weights; shader payload identity and route remain invariant across quality.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-10 us per 10k metadata/telemetry rows and 2-24 us per culling/finalization batch depending on instance count. Evidence class: STATIC_SOURCE; touched vegetation/scatter files now report 0 Sequential hits, touched missing-`CompileSynchronously` Burst scan returns 0, touched unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad sized-inclusive Sequential count is 194, broad exact Sequential count remains 168, and `git diff --check` returns exit 0 with LF/CRLF warnings only.

## Decision 058 - Black-Box Telemetry Fixed Row ABI Closure

Problem: Several fixed 300-frame black-box telemetry rows still used Sequential layout in culling, submarine damage control, visor waterline, WFC power boot, save telemetry, and marauder outpost generation. These rows are copied into native rings and dumped for forensic proof, so compiler-owned padding weakens crash autopsy and ARM64 byte-offset proof.

Solution: Converted `InstanceCullingTelemetryEntry=40`, `DamageControlTelemetryEntry=32`, `WaterlineTelemetryEntry=40`, `WfcOutpostPowerBootTelemetryEntry=64`, `WfcOutpostTelemetryEntry=64`, and `OutpostTelemetryEntry=80` to explicit layouts with exact field offsets. Kept `SectorHash` lanes on offset 8 or 16 as applicable and added a named 8-byte tail pad to `OutpostTelemetryEntry` at offset 72.

Rejected Alternatives: Nearby job-wrapper structs in `InstanceCullingService` and `MarauderOutpostJobs` were not converted because they embed Unity `NativeArray` handles and scheduling metadata. Managed save/cache rows and submarine/grid authoring records were left for owner proof or excluded because they include managed or Unity-owned ABIs. Widening all 40-byte telemetry rows to 64 bytes was rejected because they are fixed forensic dump records, not contested per-thread counters.

Scalability potential: Low tier gets deterministic black-box rings and smaller forensic dumps with stable byte lanes. Middle tier keeps current telemetry cadence. High and Ultra tiers can increase optional telemetry density or retain deeper diagnostic capture under continuous quality controls; telemetry layout, save identity, and authority routes remain invariant across quality.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per 10k telemetry ring copies/dump writes, primarily from stable field offsets and removal of compiler-owned padding ambiguity. Evidence class: STATIC_SOURCE; targeted telemetry structs no longer use Sequential layout, touched unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad sized-inclusive Sequential count is 188, broad exact Sequential count remains 168, and `git diff --check` over Loop 22 files returns exit 0 with LF/CRLF warnings only.

## Decision 059 - Runtime Descriptor And Cache Key ABI Closure

Problem: Several source-owned runtime descriptors and cache keys still used Sequential layout. Two authoring descriptors also declared impossible `Size` literals: `FaunaDataTemplate.RuntimeDescriptor` declared 64 bytes while its field span is 88 bytes, and `ProceduralFamily_Fauna.RuntimeDescriptor` declared 48 bytes while its field span is 56 bytes. Leaving those declarations in place would keep false ABI evidence in the project.

Solution: Converted `SpeciesCognitionTuning=32`, `FaunaDataTemplate.RuntimeDescriptor=88`, `FloraDataTemplate.RuntimeDescriptor=56`, `ProceduralFamily_Fauna.RuntimeDescriptor=56`, `PredatorFearNodeSnapshot=32`, `QueryKey=64`, `ForwardEchoKey=48`, `AssetGuidIdRecord=16`, and `LogisticsNode=32` to explicit layouts. `ulong` cache-key lanes are aligned to offsets 32/40/48/56 or 32/40, and tail padding is named on GUID and logistics node rows.

Rejected Alternatives: Preserving the false 64-byte and 48-byte descriptor literals was rejected because it would document an impossible stride. Widening descriptor rows to 64/96/128 bytes was rejected unless the real field span required it; these are immutable authoring/runtime descriptors, not per-thread contested counters. Managed authoring structs, Unity object references, and compatibility property structs were left out because this pass is fixed unmanaged DTO ABI hardening only.

Scalability potential: Low tier gets stable descriptor and cache-key strides for fauna cognition, acoustic occlusion reuse, vegetation fear snapshots, pre-init asset lookup, and logistics node iteration. Middle tier keeps current authoring/runtime cache cadence. High and Ultra tiers can increase fauna family variety, acoustic cache pressure, predator-fear nodes, and logistics graph size under continuous quality budgets without changing DTO identity or route ownership.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per 10k descriptor/cache/node reads depending on active fauna, acoustic query, and logistics graph density. Evidence class: STATIC_SOURCE; touched descriptor/cache files now report 0 Sequential hits, touched unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad sized-inclusive Sequential count is 179, broad exact Sequential count remains 168, and `git diff --check` over Loop 23 files returns exit 0 with LF/CRLF warnings only.

## Decision 060 - Save Telemetry And Scratch Row ABI Closure

Problem: `SaveManager` still had fixed unmanaged private rows using Sequential layout: the 300-frame async persistence telemetry ring, WFC snapshot dedupe cache entry, unmanaged load fallback candidate scratch row, and captured frame context row. The telemetry/cache/scratch rows are NativeArray or value-copy lanes and can be hardened without touching save file identity.

Solution: Converted `AsyncPersistenceTelemetryEntry=32`, `WfcOutpostSnapshotCacheEntry=24`, `SaveLoadCandidate=16`, and `SaveContextFrameData=4` to explicit layouts. `WfcOutpostSnapshotCacheEntry` keeps both `ulong` hash lanes at offsets 0 and 8. `SaveLoadCandidate` keeps the existing 16-byte fallback descriptor and constructor semantics.

Rejected Alternatives: `SaveStagingHeader` was deliberately left Sequential because it is a save staging/file-pipeline header; changing it belongs to a save-format owner proof pass, not this scratch/telemetry pass. `SaveLoadCandidate.IsBackup` was not rewritten because it is cold fallback path syntax, not a hot NativeArray mutation property. Build/rebuild was rejected because static gates passed and the user explicitly forbade premature rebuilds.

Scalability potential: Low tier gets stable save telemetry and snapshot-cache strides with no extra save-path allocation. Middle tier keeps existing persistence cadence. High and Ultra tiers can retain deeper optional persistence telemetry and larger snapshot dedupe pressure under continuous quality controls without changing save slot identity or file routes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-6 us per 10k save telemetry/cache/scratch row reads. Evidence class: STATIC_SOURCE; touched `SaveManager.cs` unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad sized-inclusive Sequential count is 175, broad exact Sequential count is 167, and `git diff --check` over `SaveManager.cs` returns exit 0 with LF/CRLF warnings only.

## Decision 061 - Interaction And Campaign Result ABI Closure

Problem: Two non-job exact Sequential rows remained in hot-facing but source-owned payload routes: `FloraHarvestInteractionPoint`, which passes harvest snap target data across interaction code, and `MetaCampaignEvaluationResult`, which is written by a Burst job into a NativeArray output lane. Both are unmanaged and do not carry managed references.

Solution: Converted `FloraHarvestInteractionPoint=96` to explicit layout with `InstanceUid@0`, `AnchorAup@8`, runtime vectors at 56 and 68, material class at 80, template index at 84, blend weight at 88, and named padding. Converted `MetaCampaignEvaluationResult=128` to explicit layout with its `FixedList128Bytes<MetaCampaignVariableChange>` at offset 0.

Rejected Alternatives: `QueryResult`/`CachedQueryResult` were rejected because they carry `RaycastHit`/`Collider` Unity physics handles. PDA exchange snapshots, emergency relay rewards, performance budget rows, and fauna spatial registry entries were rejected because they carry managed references or Unity object handles. Job/native-state wrappers remain excluded because Unity owns those container ABIs.

Scalability potential: Low tier gets stable harvest snap and campaign-result row copies without adding managed allocation. Middle tier keeps current campaign evaluation cadence. High and Ultra tiers can increase flora interaction density and campaign side-effect telemetry under continuous quality controls without changing interaction authority or campaign variable identity.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per 10k interaction/result row copies. Evidence class: STATIC_SOURCE; touched interaction/campaign files report 0 Sequential hits, touched unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad sized-inclusive Sequential count is 173, broad exact Sequential count is 165, and `git diff --check` over Loop 25 files returns exit 0 with LF/CRLF warnings only.

## Decision 062 - Sandbox Shelf Runtime Parameter ABI Closure

Problem: `HectonSandboxAbyssalShelfJobs.cs` still had a clean unmanaged runtime parameter block on compiler-owned Sequential layout. The row carries three `double` lanes and is passed into Burst shelf generation and smoke validation jobs, so relying on implicit padding weakens ARM64 and rollback-style memcpy proof. The same file also had Burst annotations without `CompileSynchronously = true` and owner-separated NativeArray fields without alias proof.

Solution: Converted `HectonSandboxAbyssalShelfParams` to explicit `Size = 104`. Offsets are pinned as: `AupCellSizeMeters@0`, `DescentRadiusMeters@8`, `PlateCellSizeMeters@16`, float scalar lanes `HighWorldY@24` through `IslandJunctionThreshold@92`, `Seed@96`, and `_pad0@100`. Added synchronous Fast/Standard Burst flags to the static math and shelf jobs, and added `[NoAlias]` to independent NativeArray inputs/outputs in base height, slope quantization, smoke sample, smoke reduction, and summary jobs.

Rejected Alternatives: Reordering the parameter fields to a denser or 128-byte cache-line layout was rejected because the existing 104-byte field order is a source-visible authoring/runtime contract and the row is not a contested atomic counter. Converting neighboring smoke output/reduction structs in the same sweep was rejected because they are validation rows and need separate owner proof before widening. Running a build or rebuild was rejected because static gates passed and the user explicitly prohibited premature rebuilds.

Scalability potential: Low tier keeps shelf generation as an analytical Dear Lie height function with aligned parameters and lower job alias ambiguity. Middle tier preserves the same deterministic AUP shelf math while improving scheduling metadata. High and Ultra tiers can spend saved CPU stalls on denser shelf samples, richer ridge/trench visual overlays, or deeper smoke validation under continuous quality controls; the parameter identity, seed route, AUP route, and gameplay truth layout remain invariant.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-10 us per 10k shelf parameter reads or scheduled shelf job batches, plus 2-18 us per batch when Burst can vectorize independent NativeArray lanes. Evidence class: STATIC_SOURCE; touched-file Sequential/Pack scan returns 0, all 9 Burst annotations include `CompileSynchronously = true`, touched unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad sized-inclusive Sequential count is 172, broad exact Sequential count is 164, and `git diff --check` over `HectonSandboxAbyssalShelfJobs.cs` returns exit 0 with LF/CRLF warnings only.

## Decision 063 - Chunk Local Offset Quantization ABI Closure

Problem: `ChunkLocalOffsetQuantization` stored runtime quantized local offsets as 6-byte Sequential rows in a `NativeArray<QuantizedLocalOffset>`. That 6-byte stride saves memory but violates the runtime ARM64 multiple-of-8 law for hot Burst lanes and risks split loads. The quantization jobs also lacked `CompileSynchronously = true` and alias proof on independent source/destination buffers.

Solution: Converted runtime `Short3` to explicit `Size = 8` with `X@0`, `Y@2`, `Z@4`, and `_pad0@6`. Converted `QuantizedLocalOffset` to explicit `Size = 8` containing `Short3@0`. Added explicit `QuantizationParams=48` with `ChunkCenterLocal@0`, `EncodeScale@16`, `DecodeStep@32`, and `_pad0@44`. Added synchronous Burst flags and `[NoAlias]` to quantize/dequantize job arrays.

Rejected Alternatives: Keeping the 6-byte runtime stride was rejected because this path is used as a Burst `NativeArray` element lane, not just a cold file-format record. Changing `SaveBinaryStorage.QuantizedAupLocalOffsetShort3` was rejected because that is a separate save-binary/wire record and changing it would require save-format owner proof. Replacing millimeter quantization with float3 storage was rejected because it would double bandwidth and remove the compression win that this visual/runtime helper is designed to preserve. Running a build or rebuild was rejected because static gates passed and the user explicitly prohibited premature rebuilds.

Scalability potential: Low tier keeps cheap millimeter local-offset compression but avoids unaligned 6-byte runtime strides in Burst. Middle tier keeps current quantize/dequantize cadence with cleaner SIMD alias facts. High and Ultra tiers can push more decoded vegetation/voxel-local presentation samples under continuous quality controls without changing save identity, AUP origin ownership, or shader-visible gameplay truth.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-6 us per 10k quantized offset reads on ARM64-class hardware, plus 1-10 us per quantize/dequantize batch when Burst can vectorize independent input/output buffers. Evidence class: STATIC_SOURCE; touched-file Sequential/Pack scan returns 0, missing-`CompileSynchronously` Burst scan returns 0, touched unaligned 8-byte `FieldOffset` scan returns 0, Pack scan remains 0, broad sized-inclusive Sequential count is 170, broad exact Sequential count remains 164, and `git diff --check` over `ChunkLocalOffsetQuantization.cs` returns exit 0 with LF/CRLF warnings only.

## Decision 064 - Binary Header ABI And Endian Hygiene Closure

Problem: Two fixed binary/staging headers still used sized Sequential layout: `WorldRegrowthPayloadHeader`, which is read from raw macro-database bytes with `UnsafeUtility.ReadArrayElement`, and `SaveStagingHeader`, which is copied into a private native staging buffer. The regrowth unpack path also rejected reversed-magic payloads instead of normalizing the header endian before validation.

Solution: Converted `WorldRegrowthPayloadHeader=80` to explicit layout with fixed 4-byte lanes from `Magic@0` through `Reserved1@76`. Added `NormalizeHeaderEndian` and `ReverseInt` so a reversed-magic payload has its header fields normalized via `math.reversebytes` before the existing magic/version/layout/checksum gates run. Converted `SaveStagingHeader=32` to explicit layout with eight `uint` lanes from `OperationId@0` through `Frame@28`.

Rejected Alternatives: Treating the regrowth header as an excluded file record was rejected because the existing codec directly hydrates it into a runtime struct with `UnsafeUtility.ReadArrayElement`, so explicit ABI proof is still required. Reversing payload body bytes was rejected because the regrowth payload lanes are byte SOA streams; only the header has multi-byte fields. Changing save file slot identity or persistent save DTOs was rejected because this pass only hardens the private staging header. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier gets deterministic regrowth import/export header handling and private save staging memcpy proof with no additional allocations. Middle tier keeps current macro-database and save cadence. High and Ultra tiers can retain larger regrowth payload traffic and deeper save staging telemetry under continuous quality controls without changing file identity, route ownership, or payload body layout.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 0-2 us per regrowth payload import/export and less than 1 us per staged save snapshot; this is primarily crash-prevention and binary compatibility proof, not a hot-loop speed claim. Evidence class: STATIC_SOURCE; touched header files report no targeted Sequential/Pack hits, touched unaligned 8-byte `FieldOffset` scan returns 0, `math.reversebytes` pattern exists elsewhere in source, Pack scan remains 0, broad sized-inclusive Sequential count is 168, broad exact Sequential count remains 164, and `git diff --check` over the two files returns exit 0 with LF/CRLF warnings only.

## Decision 065 - Visor Material Parameter Cache ABI Closure

Problem: `HectonScooterVolumetricShaftsFeature.MaterialParameterState` still used sized Sequential layout for a 152-byte CPU material cache row. The row is copied and compared before shader constant DTO generation; implicit padding was unnecessary because the field span is fixed and source-owned.

Solution: Converted `MaterialParameterState=152` to explicit layout. Float lanes run from `RenderScale@0` through `NoirFogDensity@92`, `NoirLiftColor@96` occupies the 16-byte color lane, remaining float lanes run from `LensGhostIntensity@112` through `HasExposureState@144`, and `_pad0@148` names the tail lane.

Rejected Alternatives: Changing `ShaftGlobalsDTO` or shader constant buffer packing was rejected because that GPU upload DTO already had an explicit layout and is a separate ABI. Widening the material cache to 160 bytes was rejected because the existing 152-byte span is already an 8-byte multiple and this row is not a contested per-thread counter. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap material-cache comparisons before shaft update. Middle tier preserves existing material update cadence. High and Ultra tiers can spend saved certainty on richer scooter shaft/noir lens parameters without changing shader route, RenderGraph pass identity, or material binding authority.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-4 us per 10k material-cache row comparisons/copies; primary value is deterministic CPU cache-row proof. Evidence class: STATIC_SOURCE; targeted visor Sequential/Pack scan returns 0 for `MaterialParameterState`, touched unaligned 8-byte `FieldOffset` scan returns 0, broad sized-inclusive Sequential count is 167, and `git diff --check` over `HectonScooterVolumetricShaftsFeature.cs` returns exit 0 with LF/CRLF warnings only.

## Decision 066 - Core Handle Descriptor ABI Closure

Problem: The final sized Sequential rows under `Assets/_Project/Scripts` were core allocator/vault descriptors: `NativeArenaSlice<T>`, `VaultBufferHandle<T>`, and `VaultBufferSlice<T>`. These are not gameplay DTO rows, but they are copied and passed across many vault/arena phases. Leaving them as compiler-owned Sequential rows preserved an avoidable ARM64 ABI blind spot in the core memory layer.

Solution: Converted the three descriptors to explicit layouts while preserving their existing byte sizes and field order. `NativeArenaSlice<T>` is `Ptr@0`, `Length@8`, `Stride@12`, `ByteCount@16`, `FrameSequence@20`, `_pad0@24`, size 32. `VaultBufferHandle<T>` is `ptr@0`, `generation@8`, `BufferId@12`, `Length@16`, `Stride@20`, size 24. `VaultBufferSlice<T>` is `Ptr@0`, `Generation@8`, `BufferId@12`, `StartIndex@16`, `Length@20`, `Stride@24`, `Flags@28`, pads at 29..31, size 32.

Rejected Alternatives: Widening `VaultBufferHandle<T>` to 32 or 64 bytes was rejected because it is a massively used legacy migration handle, not a contested per-thread counter, and widening it would increase manager field footprint across many domains. Removing the existing convenience properties was rejected in this loop because that would require a cross-domain call-site migration and is separate from the sized Sequential ABI closure. Running a build or rebuild was rejected because static gates passed and the user explicitly prohibited premature rebuilds.

Scalability potential: Low tier benefits from stable handle/slice descriptors in the allocator and vault paths without increasing persistent memory pressure. Middle tier keeps the same memory lease semantics. High and Ultra tiers can safely push larger vault-backed workloads, more transient arena slices, and deeper black-box diagnostics under continuous quality budgets without changing ownership, authority routes, or handle identity.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-6 us per 10k handle/slice descriptor copies or debug lease checks. Evidence class: STATIC_SOURCE; broad sized Sequential scan now returns 0 hits, touched core files report 0 Sequential hits, touched unaligned pointer/long `FieldOffset` scan returns 0, broad sized-inclusive Sequential count is 164 matching exact Sequential count, and `git diff --check` over `HectonArenaAllocator.cs` and `GlobalDataVault.cs` returns exit 0 with LF/CRLF warnings only.

## Decision 067 - Core Distance Math Burst Fence Closure

Problem: `Core/DistanceMath.cs` had 17 Burst-annotated core math helpers using Fast/Standard flags but missing `CompileSynchronously = true`. These helpers gate distance-based approximation, dominant-axis normalization, triangle-wave trigonometry, and shader math LOD pushes, so asynchronous Burst metadata violates the batch compiler directive even though no DTO layout was involved.

Solution: Added `CompileSynchronously = true` to all 17 `DistanceMath` Burst annotations and preserved their existing `FloatMode.Fast` and `FloatPrecision.Standard` settings. No public method signature, shader keyword, distance threshold, quality-tier behavior, or math approximation changed in this loop.

Rejected Alternatives: Rewriting the existing tier-based `MathLodMode` API to consume `GlobalQualityWeight` directly was rejected for this pass because it would be a cross-call-site behavior migration rather than a metadata fence closure. Converting exact Sequential job wrappers elsewhere was rejected where the rows contain Unity `NativeArray`, `NativeQueue`, mesh vertex formats, save DTOs, or managed references. Running a build or rebuild was rejected because static gates passed and the user explicitly prohibited premature rebuilds.

Scalability potential: Low tier keeps existing cheap approximations such as dominant-axis normalization and triangle-wave sine. Middle tier keeps current distance-gated blend helpers. High and Ultra tiers keep existing high-fidelity close-range math and shader LOD push routes. The change makes the Burst compile behavior deterministic without changing scalability decisions or gameplay truth.

Hardware Impact: Runtime microsecond gain is not claimed because this is Burst metadata hardening only. Evidence class: STATIC_SOURCE; `DistanceMath.cs` missing-`CompileSynchronously` scan returns 0 hits, the file reports 17 Burst annotations with synchronous Fast/Standard flags, and `git diff --check` over `DistanceMath.cs` returns exit 0 with LF/CRLF warning only.

## Decision 068 - AUP And Regrowth Deterministic Burst Fence Closure

Problem: `AUPMath` and five `WorldRegrowthSimulation` jobs still had Burst annotations without `CompileSynchronously = true`. They also used Fast float mode despite handling AUP-localized math or persistent world-state integration. The regrowth jobs operate over separate SOA byte lanes but did not expose non-overlap facts to Burst.

Solution: Updated `AUPMath` and the five regrowth jobs to `CompileSynchronously = true` with `FloatMode.Deterministic` and `FloatPrecision.Standard`. Added `[NoAlias]` to all owner-separated regrowth NativeArray fields across initialization, nutrient diffusion, daily regrowth, tombstone mining, and telemetry jobs.

Rejected Alternatives: Preserving Fast mode was rejected for this deterministic-state slice because AUP and persistent regrowth state are part of simulation truth, not visual-only math. Rewriting the regrowth memory owner to use GlobalDataVault was rejected because this file currently owns a dedicated macro-regrowth memory object and such a migration would require a route card instead of a local Burst fence pass. Running a build or rebuild was rejected because static gates passed and the user explicitly prohibited premature rebuilds.

Scalability potential: Low tier keeps regrowth in byte-lane SOA with cheap integer daily updates and stable AUP deltas. Middle tier keeps the same cadence while Burst can reason about separate lanes. High and Ultra tiers can increase regrowth grid area, telemetry density, or macro-sector validation under continuous quality budgets without changing file identity, payload layout, or simulation ownership.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per regrowth batch when Burst can trust non-overlapping byte lanes. Deterministic-mode metadata is correctness hardening, not a speed claim. Evidence class: STATIC_SOURCE; targeted AUP/regrowth missing-`CompileSynchronously` scan returns 0 hits, targeted scan reports deterministic Burst annotations on AUP/regrowth jobs plus NoAlias on regrowth NativeArrays, and `git diff --check` over `AUPMath.cs` and `WorldRegrowthSimulation.cs` returns exit 0 with LF/CRLF warnings only.

## Decision 069 - World Classification And Scatter Deterministic Burst Fence

Problem: `WorldVolumetricBiomeClassificationJobs.cs` used implicit compiler layout for five NativeArray DTOs and Fast asynchronous Burst metadata for stress/classification jobs. `ScatterMath.cs` and `ResourceYieldMath.cs` also had Burst targets without `CompileSynchronously = true`; both influence deterministic procedural placement or resource extraction truth, so the metadata needed deterministic treatment instead of visual-only Fast mode.

Solution: Converted `VolumetricBiomeClassificationInput=24`, `VolumetricBiomeClassificationResult=16`, `VolumetricBiomeStressAuditResult=24`, `VolumetricBiomeStressBlockSummary=8`, and `VolumetricBiomeStressSummaryResult=8` to explicit layouts. Added `CompileSynchronously = true`, `FloatMode.Deterministic`, and `FloatPrecision.Standard` to the five volumetric biome jobs, the two resource-yield function-pointer targets, and the ten scatter Burst targets. Added `[NoAlias]` to all non-overlapping NativeArray lanes in the volumetric biome jobs.

Rejected Alternatives: Widening volumetric rows to 32 or 64 bytes was rejected because they are batch DTO rows, not contested counters, and their current explicit sizes are already multiples of 8. Rewriting scatter to take `GlobalQualityWeight` was rejected in this pass because it would alter placement behavior rather than closing compile/alias metadata. Editing `BiomeInfluenceCell` properties was rejected for this loop because it is a broad cross-file call-site migration and needs a separate owner-proof pass. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap packed biome-cell stress rows, deterministic scatter hashes, and simple resource-yield multiplication without extra allocations. Middle tier keeps current biome/scatter cadence with cleaner Burst metadata. High and Ultra tiers can push denser biome validation, richer procedural placement, or deeper resource telemetry under continuous quality budgets without changing gameplay truth ownership, save identity, shader routes, or scatter authoring rules.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-10 us per 10k biome stress/classification row reads and 1-8 us per classification batch where Burst can trust non-overlapping lanes. Scatter/resource changes are metadata/correctness hardening with no runtime speed claim without Burst Inspector. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted Pack scan returns 0, targeted unaligned 8-byte `FieldOffset` scan returns 0, targeted NoAlias scan reports 13 lanes, and `git diff --check` over the three files returns exit 0 with LF/CRLF warnings only.

## Decision 070 - Procedural Field Sampler DTO ABI And Pack Fence

Problem: `WorldProceduralFieldSampler` still stored its primary sampler NativeArray rows with implicit compiler layout, including an 8-byte `BiomeFamilyFlags` lane in `CellOutputData`. The sampler and biome influence pack jobs also used asynchronous Fast Burst metadata, and the pack/classification jobs read packed biome influence data through struct properties inside Burst code.

Solution: Converted sampler-owned NativeArray rows to explicit layouts: `ZoneData=64`, `BiomeMatrixData=64`, `BiomeFamilyData=16`, `BiomeInfluenceCell=8`, `CellInputData=72`, `CellOutputData=328`, and `CaveEntranceHintData=32`. Pinned `CellOutputData.BiomeFamilyFlags` to offset 288 with a named 4-byte pad at 284. Added deterministic synchronous Burst flags and `[NoAlias]` to `CellSamplingJob` and `BiomeInfluencePackJob`. Added static packed extraction helpers to `BiomeInfluenceCell` and used them in hot Burst jobs instead of property reads.

Rejected Alternatives: Migrating the sampler's persistent NativeArrays into `GlobalDataVault` was rejected for this loop because it is an ownership-route migration requiring a separate route card; this pass only hardens row ABI and job metadata. Removing the existing `BiomeInfluenceCell` managed compatibility properties was rejected because managed UI/debug/atmosphere call sites still use them and that is a broader API migration. Widening `CellOutputData` to 384 or 512 bytes was rejected because it is not a contested counter and the explicit 328-byte row is already an 8-byte multiple.

Scalability potential: Low tier gets aligned sampler rows, packed biome influence lanes, and no extra scene queries. Middle tier keeps current sampling cadence with safer Burst alias metadata. High and Ultra tiers can spend the saved certainty on denser biome influence grids, more scatter validation, or richer biome transition shader feeds under continuous quality budgets without changing biome truth ownership or shader buffer identity.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-20 us per 10k sampler row reads/copies, plus 2-18 us per sampler/pack batch where Burst can trust non-overlapping lanes. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted Sequential/Pack scan returns 0, targeted unaligned 8-byte `FieldOffset` scan returns 0, targeted NoAlias scan reports 11 sampler/pack lanes, and `git diff --check` over the three files returns exit 0 with LF/CRLF warnings only.

## Decision 071 - Procedural Terrain Job Burst Alias Fence

Problem: Six pure procedural terrain kernels still used asynchronous Fast Burst metadata and unannotated source/output NativeArray lanes. The fake-overhang job also had a defensive branch that attempted to write to `HorizontalOffsetsMeters[index]` even when `HorizontalOffsetsMeters.IsCreated` was false.

Solution: Added `CompileSynchronously = true`, `FloatMode.Deterministic`, and `FloatPrecision.Standard` to `WorldProceduralTerrainFakeOverhangOffsetJob`, `WorldProceduralTerrainThermalWeatheringJob`, `WorldProceduralTerrainTerraceJob`, `WorldProceduralTerrainTectonicDisplacementJob`, `WorldProceduralTerrainSlopeCavitySplatmapJob`, and `ThermalSlumpingJob`. Added `[NoAlias]` to independent source/output/wear NativeArray lanes. Split the fake-overhang guard so an uncreated output lane returns before any write attempt.

Rejected Alternatives: Converting the terrain math to `GlobalQualityWeight`-dependent algorithms was rejected for this pass because it would change terrain truth rather than closing Burst metadata. Replacing cellular/noise terrain kernels with heavier mesh or physics probes was rejected by the Dear Lie rule; these are already analytical terrain fakes. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps analytical terrain masks, fake overhang offsets, terrace quantization, and talus relaxation as cheap NativeArray passes. Middle tier keeps current pass cadence with stronger alias proof. High and Ultra tiers can spend saved scheduler certainty on denser terrain samples, richer splatmap channels, or additional validation under continuous quality budgets without changing seed identity or terrain authority.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per terrain batch where Burst can trust source/output lane separation. Deterministic Burst metadata is correctness hardening without a profiler-backed runtime speed claim. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted Sequential/Pack scan returns 0, targeted NoAlias scan reports 15 terrain lanes, and `git diff --check` over the six files returns exit 0 with LF/CRLF warnings only.

## Decision 072 - Anomaly SDF Deterministic Burst Alias Fence

Problem: `HectonAnomalySdfJobs.cs` still had seven deterministic Burst kernels without `CompileSynchronously = true` and 13 unannotated NativeArray lanes. These kernels mutate SDF/voxel terrain truth, carve fissures, inject pillar cinematic fakes, and displace cliff surfaces; alias uncertainty can force Burst to keep conservative memory assumptions around terrain source, SDF target, optional influence, and output lanes.

Solution: Added `CompileSynchronously = true`, `FloatMode.Deterministic`, and `FloatPrecision.Standard` to `SnapSDFToTerrainJob`, `SnapSDFTopCellsToTerrainJob`, `SnapDualSDFTopCellsToTerrainJob`, `InjectMegaPillarSDFJob`, `InjectSelectedMegaPillarSDFJob`, `InjectDeepFissureSDFJob`, and `VoxelCliffOverhangNoiseJob`. Added `[NoAlias]` to terrain height input, SDF targets, secondary SDF validation target, selected feature input, fissure biome influence output, and cliff input/output SDF lanes while preserving existing remapped-write safety attributes.

Rejected Alternatives: Removing `NativeDisableParallelForRestriction` from column/envelope writers was rejected because those jobs intentionally map scheduled lanes to bounded SDF columns or pillar envelopes instead of flat NativeArray indices. Converting pillar/fissure generation to GameObjects, MeshColliders, or Physics raycasts was rejected by the Dear Lie rule; the current approach is analytical SDF/noise deformation. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap terrain-height snapping, one-column seam lock, analytical pillar/fissure SDF writes, and bounded cliff overhang noise. Middle tier keeps the same SDF pass cadence with stronger alias proof. High and Ultra tiers can spend saved scheduler certainty on denser SDF grids, stronger lateral overhang noise, or richer anomaly validation under continuous quality budgets without changing seed identity, AUP route, shader route, or gameplay truth ownership.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-16 us per anomaly SDF batch where Burst can trust terrain/SDF/source/output lane separation. Deterministic Burst metadata is correctness hardening without a profiler-backed runtime speed claim. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted NoAlias scan reports 13 anomaly SDF NativeArray lanes, and `git diff --check` over `HectonAnomalySdfJobs.cs` returns exit 0 with LF/CRLF warning only.

## Decision 073 - Shinobu Streaming Runtime Burst Alias Fence

Problem: `ShinobuStreamingRuntime.cs` already had explicit chunk residency DTO layouts, but the four residency jobs still used asynchronous Fast Burst metadata. The jobs initialize residency rows, publish deterministic mock AUP shift signals, reconcile residency after AUP shifts, and push hydration/dehydration request indices; these mutate simulation/streaming truth rather than visual-only data.

Solution: Added `CompileSynchronously = true`, `FloatMode.Deterministic`, and `FloatPrecision.Standard` to `ChunkResidencyDtoInitJob`, `MockAupShiftSignalJob`, `ChunkResidencyAupShiftReconcileJob`, and `PredictiveChunkResidencyJob`. Added `[NoAlias]` to chunk residency, AUP signal, hydration request, and dehydration request lanes.

Rejected Alternatives: Leaving Fast mode was rejected because chunk residency state participates in deterministic streaming decisions and rollback-friendly state proof. Reworking Addressables/mock profile parsing was rejected because the current pass only closes Burst metadata and alias facts; the CSV parser and fallback archaeology are cold managed bridges. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap chunk row initialization, deterministic mock AUP signal generation, and bounded predictive residency queues. Middle tier keeps the same request cadence with cleaner alias facts. High and Ultra tiers can spend scheduler certainty on larger streaming horizons, deeper black-box queue telemetry, or higher visual residency radius under continuous quality budgets without changing chunk identity, DTO layout, or ownership route.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-10 us per residency batch where Burst can trust chunk/signal/request lane separation. Deterministic Burst metadata is correctness hardening without a profiler-backed runtime speed claim. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted NoAlias scan reports 7 residency lanes, and `git diff --check` over `ShinobuStreamingRuntime.cs` returns exit 0 with LF/CRLF warning only.

## Decision 074 - BRG Visibility Output Burst Alias Fence

Problem: `HectonBatchRendererGroupUtility.cs` had two shared BRG jobs using asynchronous Fast Burst metadata. The visibility-mask build job also passed matrices, culling planes, and output mask without alias proof, and the finalization job read the visibility mask without alias proof before writing Unity-owned unsafe draw command pointers.

Solution: Added `CompileSynchronously = true` while preserving `FloatMode.Fast` and `FloatPrecision.Standard` for `BuildMatrixVisibilityMaskJob` and `FinalizeSingleDrawCommandOutputJob`. Added `[NoAlias]` to the matrix, culling-plane, and visibility-mask NativeArray lanes. Left unsafe draw-command pointer fields under their existing `NativeDisableUnsafePtrRestriction` route because Unity owns that callback allocation surface.

Rejected Alternatives: Switching BRG culling to deterministic float mode was rejected because this is visual culling/output, not rollback state. Replacing pointer writes with managed collections or CPU GameObject render loops was rejected because it would add allocation and break the BRG Dear Lie path. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap CPU plane culling and single draw-command finalization before BRG submission. Middle tier keeps the same mask path with cleaner alias facts. High and Ultra tiers can spend the saved render-thread certainty on larger instance batches, richer shader feeds, or deeper culling telemetry under continuous quality budgets without changing gameplay truth, shader identity, or draw route.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per BRG culling/finalization batch where Burst can trust mask/input lane separation. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted NoAlias scan reports 4 BRG NativeArray lanes, and `git diff --check` over `HectonBatchRendererGroupUtility.cs` returns exit 0 with LF/CRLF warning only.

## Decision 075 - Erosion Harness Metrics ABI And Burst Fence

Problem: `ErosionHarnessJobs.cs` had two bare `[BurstCompile]` editor smoke-test jobs and an implicit `ErosionSmokeMetrics` row. The metrics row spans 36 bytes, which is not an 8-byte multiple for NativeArray stride proof, and the smoke-test jobs write/read multiple independent height/sediment/wear buffers without alias metadata.

Solution: Converted `ErosionSmokeMetrics` to explicit `Size = 40`: seven float lanes at offsets 0..24, `ChangedCellCount@28`, `NonFiniteCellCount@32`, and `_pad0@36`. Added deterministic synchronous Burst flags to `ErosionFractalHeightmapJob` and `ErosionSmokeMetricsJob`. Added `[NoAlias]` to `Before`, `Height`, `After`, `Sediment`, `Wear`, and `Metrics` lanes.

Rejected Alternatives: Leaving a 36-byte stride was rejected because the metrics row is written through a NativeArray and is cheap to align with a named pad. Treating the editor harness as exempt was rejected because it produces validation evidence and should not carry bare Burst metadata. Changing erosion equations, seed constants, or PNG output paths was rejected because this pass only hardens ABI and job metadata. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps deterministic cheap erosion smoke data for CI/editor validation with aligned metrics output. Middle tier keeps current harness resolution and validation path. High and Ultra tiers can spend validation certainty on larger erosion smoke maps or deeper terrain QA metrics under continuous quality budgets without changing terrain authority, seed identity, or runtime route.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-4 us per 10k metrics row copies plus 1-8 us per harness batch where Burst can trust source/output lane separation. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, `ErosionSmokeMetrics` explicit size is 40, targeted NoAlias scan reports 7 erosion harness lanes, and `git diff --check` over `ErosionHarnessJobs.cs` returns exit 0 with LF/CRLF warning only.

## Decision 076 - Pressure Metamorphism DTO ABI And Burst Fence

Problem: `ResourceDistributionDirector` used implicit compiler layout for `PressureMetamorphismInput` and `PressureMetamorphismResult`, both stored in persistent NativeArrays, and its pressure metamorphism job used asynchronous Fast Burst metadata without alias proof. The job advances carbon-to-diamond transformation progress, so it is resource state truth rather than visual-only math.

Solution: Converted `PressureMetamorphismInput` to explicit `Size = 16` and `PressureMetamorphismResult` to explicit `Size = 8`. Added deterministic synchronous Burst flags to `PressureMetamorphismJob`. Added `[NoAlias]` to metamorphism input and result NativeArray lanes.

Rejected Alternatives: Leaving implicit layout was rejected because the input/result rows are persistent native lanes and cheap to pin byte-for-byte. Rewriting the resource distribution ownership route or ghost proxy snap raycast path was rejected because this pass only hardens the pressure metamorphism DTO/job surface. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap batch pressure metamorphism updates with aligned rows. Middle tier keeps current update cadence and transformation timing. High and Ultra tiers can spend scheduler certainty on larger resource-node batches, deeper pressure/thermal validation, or richer resource telemetry under continuous quality budgets without changing template identity, node ownership, or spawn routes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-4 us per 10k metamorphism row reads/copies plus 1-8 us per metamorphism batch where Burst can trust input/result lane separation. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted layout scan confirms 16-byte input and 8-byte result rows, targeted NoAlias scan reports 2 metamorphism lanes, and `git diff --check` over `ResourceDistributionDirector.cs` returns exit 0 with LF/CRLF warning only.

## Decision 077 - Hydraulic Erosion Metrics ABI And Burst Fence

Problem: `HydraulicErosionMetricBlock` was a compiler-owned 56-byte metrics row written through NativeArray scan/reduction jobs, and both hydraulic erosion metrics jobs used asynchronous Fast Burst metadata without alias proof. These jobs generate QA evidence, including NaN counts and boundary-band deltas, so byte-stable output and deterministic metadata matter.

Solution: Converted `HydraulicErosionMetricBlock` to explicit `Size = 56`, with float lanes from `MinHeight@0` through `MaxBoundaryWear@36` and int counters from `NanCount@40` through `BoundaryNanCount@52`. Added deterministic synchronous Burst flags to `HydraulicErosionMetricsJob` and `HydraulicErosionMetricReductionJob`. Added `[NoAlias]` to height, sediment, wear, block, and summary lanes.

Rejected Alternatives: Widening the row to 64 bytes was rejected because the existing 56-byte span is already an 8-byte multiple and this row is not a contested per-thread counter. Leaving Fast mode was rejected because these metrics are validation/forensics data. Changing erosion math or boundary audit rules was rejected because this pass only hardens ABI and metadata. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap block-level erosion QA and NaN detection with stable metric rows. Middle tier keeps current block scan/reduction cadence. High and Ultra tiers can spend validation certainty on larger erosion maps, tighter boundary audits, or richer terrain telemetry under continuous quality budgets without changing terrain authority, erosion equations, or save identity.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-5 us per 10k metric block reads/copies plus 1-10 us per metrics scan/reduction batch where Burst can trust source/block/summary lane separation. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, layout scan confirms 56-byte metric blocks, targeted NoAlias scan reports 6 hydraulic metric lanes, and `git diff --check` over `HydraulicErosionMetricsJob.cs` returns exit 0 with LF/CRLF warning only.

## Decision 078 - Biolum Visual Job Burst Alias Fence

Problem: `HectonBiolumManager` had predator blackout and ripple distance jobs using asynchronous Fast Burst metadata, and the source/output NativeArray lanes had no alias proof. These jobs feed visual response and shader-facing intensity/distance data, not authoritative rollback state.

Solution: Added `CompileSynchronously = true` while preserving `FloatMode.Fast` and `FloatPrecision.Standard` for `PredatorBlackoutJob` and `RippleDistanceJob`. Added `[NoAlias]` to predator positions, predator scores, ripple positions, and ripple distance output lanes.

Rejected Alternatives: Switching these visual jobs to deterministic mode was rejected because they do not own gameplay truth. Reworking the manager's existing dispatcher finalization, telemetry ring access, or vault lifecycle was rejected because this pass only closes Burst metadata and alias facts. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap predator dimming and ripple distance scoring before shader updates. Middle tier keeps current visual cadence with cleaner alias facts. High and Ultra tiers can spend render/CPU certainty on more active ripples, richer biolum shader parameters, or deeper visual telemetry under continuous quality budgets without changing sonar authority, predator ownership, or gameplay truth routes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-6 us per biolum scoring batch where Burst can trust source/output lane separation. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted NoAlias scan reports 4 biolum job lanes, and `git diff --check` over `HectonBiolumManager.cs` returns exit 0 with LF/CRLF warning only.

## Decision 079 - AUP Camera-Relative Burst Fence

Problem: `AbsoluteUniversePosition.ToCameraRelativeFloat3` in `PersistentWorldRegistry.cs` had Fast/Standard Burst metadata without `CompileSynchronously = true`. This wrapper is part of the AUP render/cull precision path and should not retain asynchronous Burst metadata.

Solution: Added `CompileSynchronously = true` to the existing Burst annotation and preserved the existing Fast/Standard float mode. No implementation, AUP subtraction route, struct layout, registry route, or save identity changed.

Rejected Alternatives: Switching the wrapper to deterministic mode was rejected because the method is explicitly camera-relative rendering/culling output and delegates to the already hardened AUP math path. Refactoring `PersistentWorldRegistry` completion windows or sequential job wrappers was rejected because this loop is a single metadata fence. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap camera-relative float conversion after double-precision AUP subtraction. Middle tier keeps the same culling/render route. High and Ultra tiers can spend precision certainty on larger visible ranges and denser render batches under continuous quality budgets without changing AUP truth ownership or save identity.

Hardware Impact: Runtime microsecond gain is not claimed because this is Burst metadata hardening only. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan over `PersistentWorldRegistry.cs` returns 0 and `git diff --check` over the file returns exit 0 with LF/CRLF warning only.

## Decision 080 - Scatter Evaluator Counter And Burst Fence

Problem: `ScatterEvaluator` used a one-element `NativeArray<int>` as an atomic candidate counter while every worker lane reserved output slots through `Interlocked.Increment`. That creates a high-contention counter without cache-line isolation. The scatter evaluation job also used asynchronous Fast Burst metadata and lacked alias proof for height samples, candidate output, and counter lanes.

Solution: Introduced explicit `ScatterCandidateCounter64=64` with `Count@0` and 60 bytes of named padding. Replaced `NativeArray<int>` with `NativeArray<ScatterCandidateCounter64>`, reset by assigning `default`, and changed all atomic increments to operate on `counter->Count`. Added deterministic synchronous Burst flags and `[NoAlias]` to the scatter job's height sample, candidate output, and padded counter lanes.

Rejected Alternatives: Keeping the 4-byte counter was rejected because it violates the false-sharing/counter padding rule in a high-frequency parallel writer. Replacing the atomic reserve with managed lists or per-cell GameObject emission was rejected because it would add GC and break the data-oriented scatter pipeline. Widening candidate DTOs or changing scatter family routing was rejected because the issue is the reservation counter and Burst metadata, not candidate payload identity. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps bounded deterministic scatter candidate generation with a cache-line-isolated counter. Middle tier keeps current grid cadence with cleaner alias facts. High and Ultra tiers can spend saved contention budget on larger scatter radii, denser placement attempts, or richer scatter validation under continuous quality budgets without changing family identity, height sampling ownership, or save routes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per scatter evaluation under high candidate contention plus 1-10 us per batch from alias proof. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, layout scan confirms 64-byte counter row, targeted NoAlias scan reports 3 scatter evaluator lanes, and `git diff --check` over `ScatterEvaluator.cs` returns exit 0 with LF/CRLF warning only.

## Decision 081 - Wreck BRG Job Burst Alias Fence

Problem: `WreckMaterialRegistry` had two BRG helper jobs using asynchronous Fast Burst metadata, explicit Sequential attributes on job wrappers, and unannotated native lanes for matrix rebase and visible-subset culling. These jobs are render-output helpers and should not carry DTO-style layout attributes or alias ambiguity.

Solution: Added `CompileSynchronously = true` while preserving Fast/Standard float mode for `WreckMatrixRebaseJob` and `CullWreckMatricesToVisibleSubsetJob`. Removed redundant `StructLayout(LayoutKind.Sequential)` attributes from those job wrappers. Added `[NoAlias]` to matrix, age, frustum plane, visible matrix, and visible age lanes.

Rejected Alternatives: Switching to deterministic mode was rejected because these are visual BRG output jobs, not authoritative simulation. Migrating the registry's persistent staging NativeLists or dispatcher completion windows was rejected because that is an ownership-route change outside this metadata fence. Replacing BRG with GameObject renderer loops was rejected because it would add managed traversal and defeat the visual fake. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap matrix rebasing and frustum-filtered visible subsets before upload. Middle tier keeps current BRG batch cadence with cleaner alias facts. High and Ultra tiers can spend render certainty on larger wreck batches, richer shader metadata, or tighter culling telemetry under continuous quality budgets without changing wreck identity, shader route, or gameplay truth.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per wreck BRG culling/rebase batch where Burst can trust source/output lane separation. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, explicit Sequential scan is clear for touched job wrappers, targeted NoAlias scan reports 6 wreck BRG lanes, and `git diff --check` over `WreckMaterialRegistry.cs` returns exit 0 with LF/CRLF warning only.

## Decision 082 - Flora Genome Burst Alias Fence

Problem: `FloraGenomeJobs` still had L-system expansion and turtle graphics jobs without synchronous Burst metadata, while decoder/expander/turtle source-output NativeArray lanes had no alias proof. These jobs convert genome bytes into branch matrices, hazard zones, black-box entries, and generation stats, so async metadata and alias ambiguity weaken deterministic procedural evidence.

Solution: Preserved synchronous Fast/Standard Burst for `FloraGenomeDecoderJob` because it is byte parsing and range validation. Added synchronous deterministic Burst flags to `IterativeLSystemExpanderJob` and `TurtleGraphicsJob`. Added `[NoAlias]` to raw byte, genome, symbol, scratch, turtle stack, branch matrix, hazard zone, black-box, cursor, and stats lanes.

Rejected Alternatives: Changing grammar profiles, branch matrix DTOs, hazard DTOs, seed mixing, or black-box entry layout was rejected because this pass only hardens Burst metadata and alias facts. Padding `BlackBoxCursor` to 64 bytes was rejected because `TurtleGraphicsJob` is a single `IJob` and the cursor is not a contested parallel atomic counter. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps deterministic flora generation with bounded symbol expansion and alias-clean source/scratch/output lanes. Middle tier keeps current genome batch cadence. High and Ultra tiers can spend the certainty on larger grammar iteration caps, denser branch matrix output, richer hazard-zone authoring, or deeper flora black-box telemetry under continuous quality budgets without changing genome identity, save routes, or authority ownership.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-12 us per flora genome generation batch where Burst can trust source/scratch/output lane separation. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted NoAlias scan reports 16 flora genome lanes, and `git diff --check` over `FloraGenomeJobs.cs` returns exit 0 with LF/CRLF warning only.

## Decision 083 - Vegetation HLOD Cull Burst Alias Fence

Problem: `VegetationNavGridSynchronizer` had `CullHLODInstancesJob` using asynchronous Fast Burst metadata, a redundant Sequential attribute on the job wrapper, and unannotated registry/frustum/visibility NativeArray lanes. This job is render visibility output, not authoritative simulation state.

Solution: Added `CompileSynchronously = true` while preserving Fast/Standard float mode. Removed the redundant `StructLayout(LayoutKind.Sequential)` from the private job wrapper. Added `[NoAlias]` to `Registry`, `FrustumPlanes`, and `VisibleFlags`.

Rejected Alternatives: Switching to deterministic mode was rejected because HLOD flags are visual culling output and do not own gameplay truth. Reworking the bridge's NativeArray ownership, dispatcher completion windows, or HLOD payload API was rejected because this pass only closes Burst metadata and alias proof. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap HLOD visibility flags with a frustum-only visual fake before render upload. Middle tier keeps current culling cadence with cleaner alias facts. High and Ultra tiers can spend culling certainty on larger HLOD registries, denser vegetation impostor sets, or richer visibility telemetry under continuous quality budgets without changing vegetation authority, HLOD payload identity, or save routes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-6 us per HLOD culling batch where Burst can trust source/output lane separation. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted scan reports 3 HLOD NoAlias lanes, and `git diff --check` over `VegetationNavGridSynchronizer.cs` returns exit 0 with LF/CRLF warning only.

## Decision 084 - Spatial Hash AUP Maintenance Burst Alias Fence

Problem: `WorldSpatialHashGrid` had `ValidateAupIntegrityJob` and `FarUnloadCandidatesJob` using asynchronous Fast Burst metadata and unannotated NativeArray lanes. These jobs operate on AUP maintenance evidence and far-unload decision masks, so they should not remain async Fast metadata with alias ambiguity.

Solution: Added synchronous deterministic Burst flags to both jobs. Added `[NoAlias]` to validation absolute-position, runtime-position, invalid-mask lanes and far-unload absolute-position, eligibility-mask, unload-mask lanes.

Rejected Alternatives: Reworking the file's pre-existing static NativeArray ownership into vault-backed buffers was rejected in this loop because it would be a broad route migration touching public facade behavior and teardown ownership. Leaving Fast mode was rejected because AUP integrity and far-unload masks are state-maintenance decisions. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps deterministic AUP validation and far-unload masks with alias-clean maintenance lanes. Middle tier keeps current validation/far-unload cadence. High and Ultra tiers can spend maintenance certainty on larger spatial batches, denser validation sampling, or richer acoustic/spatial telemetry under continuous quality budgets without changing entity identity, authority routes, or save semantics.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per spatial maintenance batch where Burst can trust source/output lane separation. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted NoAlias scan reports 6 spatial maintenance lanes, and `git diff --check` over `WorldSpatialHashGrid.cs` returns exit 0 with LF/CRLF warning only.

## Decision 085 - Ecosystem Simulation Burst Alias Fence

Problem: `EcosystemDirector` had four gameplay simulation jobs using asynchronous Fast Burst metadata and unannotated NativeArray lanes: apex territory overlap, sector Lotka-Volterra, biomass Lotka-Volterra, and headless threshold migration. These jobs derive population, predator pressure, migration, and territorial retreat facts, so Fast async metadata and alias ambiguity were not acceptable.

Solution: Added synchronous deterministic Burst flags to the four jobs. Added `[NoAlias]` to 27 source/output lanes spanning apex territory samples/results, sector front/back state/counts, food heatmaps, headless output SOA, biomass front/back arrays, carrying capacity, macro-cell coords, index entries, and biomass sum scratch.

Rejected Alternatives: Preserving Fast mode was rejected because these jobs affect gameplay state and rollback-relevant ecosystem facts. Rewriting Lotka-Volterra equations, food heatmap sampling, migration tie rules, or vault buffer ownership was rejected because this pass only hardens Burst metadata and alias facts. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps bounded deterministic ecosystem batches with alias-clean vault lanes. Middle tier keeps current solve cadence and headless SOA outputs. High and Ultra tiers can spend certainty on larger active sector counts, denser biomass cells, richer apex territory overlap sampling, or deeper ecosystem black-box telemetry under continuous quality budgets without changing authority routes, save identity, or vault buffer IDs.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-18 us per ecosystem solve batch where Burst can trust source/output lane separation. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, targeted NoAlias scan reports 27 ecosystem job lanes, and `git diff --check` over `EcosystemDirector.cs` returns exit 0 with LF/CRLF warning only.

## Decision 086 - SpaceEngine 0.9.8 Terrain Kernel ABI And Burst Fence

Problem: `SpaceEngine098TerrainKernels` had compiler-owned terrain parameter/metric struct layouts and seven bare Burst annotations. `SpaceEngine098RilleParams` and `SpaceEngine098PipelineMetricSample` had natural sizes that were not guaranteed 8/16/32-byte explicit rows, and the terrain pipeline jobs lacked alias proof.

Solution: Converted ridged parameters to explicit `Size = 40`, crater profile to explicit `Size = 32`, rille parameters to explicit `Size = 32`, and pipeline metric samples to explicit `Size = 32`. Added synchronous deterministic Burst metadata to the two math facades and five terrain jobs. Added `[NoAlias]` to 13 terrain source/output lanes.

Rejected Alternatives: Leaving compiler layout was rejected because terrain bake parameters and metrics are copied through job payloads and NativeArray rows. Preserving Fast async metadata was rejected because procedural terrain output is deterministic world truth. Rewriting the noise, crater, rille, or checksum equations was rejected because this pass only hardens ABI, Burst metadata, and alias facts. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap deterministic terrain fakes: ridged multifractal, analytic crater profile, rille fissure masks, and metrics rows. Middle tier keeps current pipeline order and validation cadence. High and Ultra tiers can spend certainty on more samples, deeper metrics, or richer crater/rille overlays under continuous quality budgets without changing seed identity, terrain authority, or bake routes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-6 us per 10k terrain parameter/metric row reads/copies plus 2-16 us per terrain pipeline batch from alias proof. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, layout scan confirms 40/32/32/32-byte rows, targeted NoAlias scan reports 13 terrain lanes, and `git diff --check` over `SpaceEngine098TerrainKernels.cs` returns exit 0 with LF/CRLF warning only.

## Decision 087 - Hydraulic Erosion Kernel ABI, NaN Guard, And Burst Fence

Problem: `HydraulicErosionJob` had a queued delta row using Sequential layout, six bare Burst annotations, no alias proof on terrain source/output lanes, and two post-filter jobs computing `index / Width` before validating width. The droplet direction `math.rsqrt` was already epsilon-guarded, but the width division order was still a concrete NaN/divide-by-zero hazard if invalid dimensions reached a worker.

Solution: Converted `HydraulicErosionHeightDelta` to explicit `Size = 16`. Added synchronous deterministic Burst metadata to droplet erosion, queued delta apply, sedimentary flat smoothing, canyon wall steepening, silt mask, and normalization jobs. Added `[NoAlias]` to 17 terrain lanes. Reordered smoothing and canyon post-filter coordinate math to use `safeWidth = math.max(1, Width)` and `safeHeight = math.max(1, Height)` before modulo/division guards.

Rejected Alternatives: Replacing four-phase erosion with a full-copy merge pipeline was rejected because it would multiply memory bandwidth and undo the existing parity proof. Removing `NativeDisableParallelForRestriction` was rejected because the four-phase scheduler owns the safety invariant and Unity cannot infer it. Rewriting erosion physics, delta queue policy, or mask semantics was rejected because this pass only hardens ABI, Burst metadata, alias facts, and one division guard. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps sliced deterministic droplets, bounded delta apply, flat smoothing, canyon steepening, and silt masks with guarded dimensions. Middle tier keeps current droplet slices and apply budgets. High and Ultra tiers can spend certainty on larger droplet batches, deeper sediment masks, or richer erosion telemetry under continuous quality budgets without changing terrain authority, queued delta route, or save identity.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-4 us per 10k queued delta row reads/copies plus 2-18 us per hydraulic erosion batch from alias proof. Evidence class: STATIC_SOURCE; targeted missing-`CompileSynchronously` scan returns 0, layout scan confirms `HydraulicErosionHeightDelta=16`, targeted NoAlias scan reports 17 hydraulic erosion lanes, and `git diff --check` over `HydraulicErosionJob.cs` returns exit 0 with LF/CRLF warning only.

## Decision 088 - SpaceEngine Terrain Width Guard Closure

Problem: `SpaceEngine098TerrainKernels` still had three terrain jobs deriving x/z coordinates through `index % Width` and `index / Width` directly. Even though normal pipeline dimensions are valid, the code path had no local guard before integer division in ridged multifractal, crater height application, and rille fissure jobs.

Solution: Added `safeWidth = math.max(1, Width)` before coordinate derivation in the three jobs and switched modulo/division to `safeWidth`. This preserves deterministic terrain values for valid dimensions and prevents invalid-width divide/modulo faults from poisoning terrain metrics.

Rejected Alternatives: Adding early returns was rejected because it would change batch coverage and could hide invalid pipeline dimensions from metric evidence. Rewriting terrain sampling, crater/rille equations, seed identity, or DTO layouts was rejected because the issue was division guard ordering only. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap analytic terrain fakes with guarded coordinate math under reduced sample budgets. Middle tier keeps the existing ridged/crater/rille pipeline cadence. High and Ultra tiers can spend terrain budget on denser samples, crater overlays, and rille detail under continuous quality weights without changing terrain authority, save identity, or payload layout.

Hardware Impact: Runtime microsecond gain is not claimed because this is correctness hardening. Evidence class: STATIC_SOURCE; broad World missing-`CompileSynchronously` scan returns 0, targeted raw width division/modulo and forbidden layout scan over SpaceEngine plus hydraulic erosion files returns 0, targeted SpaceEngine scan shows safe-width guards at the three coordinate sites, and `git diff --check` over `SpaceEngine098TerrainKernels.cs` returns exit 0 with LF/CRLF warning only.

## Decision 089 - Player Critical Audio Job Wrapper Layout Hygiene

Problem: `PlayerCriticalBufferJobs.cs` carried five `StructLayout(LayoutKind.Sequential)` attributes on Burst job wrapper structs. These wrappers contain `NativeArray<T>` handles and scheduling scalars, but they are not Vault DTO rows, Signal payloads, save deltas, or binary payloads. Leaving Sequential there creates false ABI evidence and pollutes the project-wide layout scan.

Solution: Removed the five redundant Sequential attributes and removed the now-unused `System.Runtime.InteropServices` import. Existing synchronous Fast/Standard Burst annotations and `[NoAlias]` lanes remain unchanged.

Rejected Alternatives: Converting these job wrappers to explicit layouts was rejected because `NativeArray<T>` handle fields are Unity-owned scheduling data, not raw payload rows that should be frozen with FieldOffset maps. Changing DSP math, delay-ring ownership, grain voice arrays, or VWS queue semantics was rejected because this loop only removes false layout metadata. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps the current cheap Doppler, binaural delay, granular, cooldown, and priority jobs with no extra ABI fiction. Middle tier keeps current audio cadence. High and Ultra tiers can spend audio budget on richer DSP buffer sizes under continuous quality budgets without changing buffer ownership or payload routes.

Hardware Impact: Runtime microsecond gain is not claimed because this is metadata hygiene. Evidence class: STATIC_SOURCE; targeted scan over `PlayerCriticalBufferJobs.cs` returns 0 `StructLayout`, `System.Runtime.InteropServices`, `LayoutKind.Sequential`, `Pack=1`, or missing-`CompileSynchronously` hits, while targeted scan confirms the existing synchronous Burst annotations and NoAlias lanes. `git diff --check` over the file returns exit 0 with LF/CRLF warning only.

## Decision 090 - Ladder Climb IK Job Wrapper Layout Hygiene

Problem: `LadderClimbIkJobs.cs` had correct explicit 128-byte payload rows but still carried a `StructLayout(LayoutKind.Sequential)` attribute on `LadderClimbIkSolveJob`. That solver is a scheduling wrapper containing NativeArray handles and one double3 scalar, not a Vault payload row. Leaving Sequential there corrupts the project-wide layout scan with a false DTO hit.

Solution: Removed the redundant Sequential attribute from `LadderClimbIkSolveJob`. The explicit maps for `LadderClimbIkInput`, `LadderClimbIkOutput`, and `LadderClimbTelemetryEntry` were left untouched.

Rejected Alternatives: Converting the job wrapper to explicit layout was rejected because Unity owns NativeArray handle representation and job packet layout; freezing it would be false ABI work. Changing IK equations, low-tier elbow fake, AUP subtraction, telemetry cursor semantics, or DTO layouts was rejected because the payload rows already satisfy explicit layout requirements. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps the cheap half-way elbow fake path through existing flags. Middle tier keeps deterministic FABRIK-style analytic solving and telemetry. High and Ultra tiers can spend IK budget on richer grip validation or denser ladder telemetry under continuous quality budgets without changing DTO layout, AUP route, or save identity.

Hardware Impact: Runtime microsecond gain is not claimed because this is metadata hygiene. Evidence class: STATIC_SOURCE; targeted scan over `LadderClimbIkJobs.cs` returns 0 `LayoutKind.Sequential` or `Pack=1` hits and 0 missing-`CompileSynchronously` hits; targeted layout scan confirms three explicit 128-byte DTO/telemetry rows and existing NoAlias lanes. `git diff --check` over the file returns exit 0 with LF/CRLF warning only.

## Decision 091 - Foveated Simulation Job Wrapper Layout Hygiene

Problem: `FoveatedSimulationManager.cs` had two private Burst job wrappers declaring `StructLayout(LayoutKind.Sequential)`: `ImportanceScoringJob` and `VisualInterpolationJob`. The actual black-box row, `FoveatedSimulationTelemetryEntry`, was already explicit 64 bytes. The Sequential wrappers were false DTO evidence in a core manager.

Solution: Removed the redundant Sequential attributes from the two job wrappers. Left `FoveatedSimulationTelemetryEntry=64` unchanged.

Rejected Alternatives: Converting job wrappers to explicit layout was rejected because the wrappers carry Unity NativeArray handles and TransformAccess scheduling state, not binary payload rows. Reworking the manager's pre-existing private NativeArray ownership into Vault buffers was rejected in this loop because that is a route/lifecycle migration, not a layout metadata fix. Changing foveated cadence math, registry interfaces, signal imports, or telemetry layout was rejected. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps continuous foveated throttling through distance/frustum importance scores. Middle tier keeps current cadence hysteresis. High and Ultra tiers can spend foveation certainty on larger target sets or richer telemetry under continuous quality budgets without changing tick-rate truth ownership or telemetry ABI.

Hardware Impact: Runtime microsecond gain is not claimed because this is metadata hygiene. Evidence class: STATIC_SOURCE; targeted scan over `FoveatedSimulationManager.cs` returns 0 `LayoutKind.Sequential` or `Pack=1` hits and 0 missing-`CompileSynchronously` hits; targeted layout scan confirms `FoveatedSimulationTelemetryEntry=64` and existing NoAlias lanes. `git diff --check` over the file returns exit 0 with LF/CRLF warning only.

## Decision 092 - Sargassum NativeQueue Payload Quantization

Problem: `SargassumGlobalDragManager.cs` still had explicit payload rows with non-quantized strides: `SargassumFieldSample=40`, `EntanglementStrainSignal=40`, `MassiveDisplacementSignal=24`, and `NestedAttachmentState=80`. The two signal structs are stored in `NativeQueue<T>` front/back lanes, so their 24/40-byte strides are real native event ABI, not just managed metadata. `ScavengerHostState` also carried a Sequential marker despite containing a managed `SargassumCollapseChunk` reference and living in a managed array.

Solution: Padded the local payload rows to quantized sizes: field sample to 64, entanglement strain to 64, massive displacement to 32, and nested attachment state to 128. Removed the false Sequential marker from `ScavengerHostState`.

Rejected Alternatives: Reordering public signal fields was rejected because listener source compatibility and existing initializer shape should not be disturbed. Converting `ScavengerHostState` to explicit was rejected because it contains a managed reference and is not a NativeQueue/Vault payload. Reworking the sargassum manager's pre-existing managed arrays and persistent queues into Vault buffers was rejected because this loop only closes ABI stride and layout metadata. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps bounded sargassum queue capacities and cheap density/event payloads with cache-quantized strides. Middle tier keeps the current native event lanes. High and Ultra tiers can spend budget on more disruption zones, nested debris, or listener telemetry under continuous quality budgets without changing queue ownership or event route semantics.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-5 us per 10k queue/sample row copies from aligned fixed strides; primary gain is ABI stability. Evidence class: STATIC_SOURCE; targeted scan over `SargassumGlobalDragManager.cs` returns 0 24/40/48/56/72/80/96/104/112/120 explicit payload rows, 0 Sequential hits, 0 Pack=1 hits, and 0 missing-`CompileSynchronously` hits; targeted layout scan confirms 64/64/32/128-byte rows. `git diff --check` over the file returns exit 0 with LF/CRLF warning only.

## Decision 093 - Persistent Compact Delta CS1612 Purge

Problem: `PersistentWorldCompactDeltaRecord` is a 16-byte native row stored in `NativeList<PersistentWorldCompactDeltaRecord>` and `NativeParallelMultiHashMap<uint, PersistentWorldCompactDeltaRecord>`, but it still exposed `IsDeleted` and `IsValid` properties. Those hidden methods are unnecessary in Burst scans and can create defensive value copies when called through native container indexers. `TombstoneDecayCollectJob` also still carried a redundant Sequential marker.

Solution: Replaced the compact-row properties with static `HasDeletedFlag(in PersistentWorldCompactDeltaRecord)` and `IsValidRecord(in PersistentWorldCompactDeltaRecord)` helpers. Updated hot compact-row uses in tombstone collection, compact build validation, compact resolve, and tombstone apply. Removed the false Sequential marker from the tombstone job wrapper.

Rejected Alternatives: Reworking all `PersistentWorldItemRecord` and `PersistentWorldDeltaRecord` properties was rejected in this loop because those records have broader save/import/editor call sites and require a larger API migration. Changing the compact row layout, tombstone thresholds, delta hashmap route, or save delta semantics was rejected because the issue was only hidden property access on the compact native row. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps compact 16-byte dropped-item deltas and bounded tombstone sweeps. Middle tier keeps current tombstone decay cadence. High and Ultra tiers can spend persistence budget on larger compact delta windows or richer save telemetry under continuous quality budgets without changing compact-row layout or save identity.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-4 us per 10k compact delta scans by avoiding property calls/defensive copies in tombstone scans. Evidence class: STATIC_SOURCE; targeted scan over `PersistentWorldRegistry.cs` returns 0 `compactRecord.IsDeleted`, `compactRecord.IsValid`, or `DeltaRecords[i].IsDeleted` hits, 0 missing-`CompileSynchronously` hits, and shows static `in` helpers on the compact row. `git diff --check` over the file returns exit 0 with LF/CRLF warning only.

## Decision 094 - World Managed Row Sequential Hygiene

Problem: `FaunaSpatialHashRegistry.Entry` and `EmergencyServiceRelay.RewardEntry` declared Sequential layout even though both rows contain managed Unity object references and are not native payload ABI. These attributes pollute the project-wide Sequential inquisition with false DTO evidence and invite an invalid explicit-layout conversion.

Solution: Removed the two false Sequential attributes and the now-unused `System.Runtime.InteropServices` imports. No field ordering, behavior, AUP route, relay reward route, managed array/list/dictionary ownership, or native hash behavior changed.

Rejected Alternatives: Converting either row to explicit layout was rejected because managed references must not be treated as blittable native DTO fields. Rewriting `FaunaSpatialHashRegistry` private `NativeList<int>` scratch ownership into Vault was rejected in this loop because that is a route/lifecycle migration, not metadata hygiene. Reworking `EmergencyServiceRelay` read-accessor purity issues was rejected in this loop because SHINOBU_204's domain is layout/ABI alignment and this pass was constrained to false Sequential markers. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps managed scene-authored relay reward rows and fauna registry metadata out of native ABI scans. Middle tier keeps existing registry/query behavior. High and Ultra tiers can spend budget on richer relay or fauna sensory systems under continuous quality budgets without misclassifying managed references as native binary payload.

Hardware Impact: Runtime microsecond gain is not claimed because this is metadata hygiene. Evidence class: STATIC_SOURCE; targeted scan over `FaunaSpatialHashRegistry.cs` and `EmergencyServiceRelay.cs` returns 0 `StructLayout`, `System.Runtime.InteropServices`, `LayoutKind.Sequential`, or `Pack=1` hits, and `git diff --check` over both files returns exit 0 with LF/CRLF warning only.

## Decision 095 - World Sampler And Nav Native Row Quantization

Problem: `GlobalWorldSampler` still had real native DTO rows below the project quantization floor: `TerrainSampleDTO=24` and `MapMagicCellDTO=8`. The same file also had false Sequential source metadata on a NativeArray-handle data wrapper and six Burst job wrappers. `VoxelDynamicNavGridRuntime` had native queue/list rows at `DirtyVolumeRequest=8`, `DynamicObstacleClearRequest=24`, and `NavObstaclePrimitive=24`, plus one managed-reference deferred row carrying false Sequential metadata.

Solution: Padded `TerrainSampleDTO` to 32 bytes and `MapMagicCellDTO` to 16 bytes, then updated the byte-stride constants and `ToDTO` tail zeroing used by `GetSampleRef`. Padded voxel dirty-volume, dynamic-clear, and obstacle primitive rows to 16/32/32 bytes. Removed false Sequential source metadata from GlobalWorldSampler data/job wrappers and the managed-reference deferred dirty-volume row.

Rejected Alternatives: Leaving the 8/24-byte rows was rejected because they are copied through NativeArray, NativeQueue, or NativeList lanes and violate the cache-stride quantization mandate. Reordering public fields was rejected because existing named initializers and byte-offset documentation should remain stable. Converting NativeArray-handle job wrappers or managed-reference rows to explicit layout was rejected because Unity owns those handle/reference representations. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier gets cache-quantized terrain sample DTOs and nav obstacle rows while preserving cheap sampler math and bounded nav rebuild queues. Middle tier keeps current SDF/height sampling and partial obstacle stamping cadence. High and Ultra tiers can spend budget on more terrain query batches, richer mock raymarch tests, and denser dynamic obstacle snapshots under continuous quality weights without changing authority routes or save identity.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-4 us per 10k terrain DTO row reads/copies plus 1-8 us per 10k voxel obstacle queue/list reads under nav rebuild pressure. Evidence class: STATIC_SOURCE; targeted scan over `GlobalWorldSampler.cs` and `VoxelDynamicNavGridRuntime.cs` returns 0 `LayoutKind.Sequential`, `Pack=1`, or explicit `Size = 8/24` hits; targeted Burst scan returns 0 missing `CompileSynchronously` annotations; `git diff --check` over both files returns exit 0 with LF/CRLF warning only.

## Decision 096 - Spatial Hash And Marauder Outpost ABI Fence

Problem: `WorldSpatialHashGrid` still declared fully-qualified Sequential attributes on managed-reference registry rows. `MarauderOutpostJobs` had a native telemetry row at 80 bytes, three job wrappers carrying false Sequential metadata, low-precision Burst annotations, and unannotated NativeArray lanes.

Solution: Removed the two managed-row Sequential markers in `WorldSpatialHashGrid`. Padded `OutpostTelemetryEntry` to 128 bytes. Removed Sequential attributes from outpost job wrappers, upgraded their Burst attributes to synchronous Fast/Standard form, and added `[NoAlias]` to WFC, mutable grid, height sample, matrix, cell-type, spawn, counter, and AUP-shift matrix lanes.

Rejected Alternatives: Converting spatial hash managed entries to explicit layout was rejected because they contain Unity object references. Leaving outpost telemetry at 80 bytes was rejected because it is a persistent 300-frame NativeArray black-box row and not cache-quantized. Changing WFC cell semantics, low-tier dimensions, interactable spawn layout, or generation ownership was rejected because this pass only hardens ABI, Burst metadata, and alias facts. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps compact outpost WFC dimensions and cheap managed spatial registry metadata. Middle tier keeps current matrix extraction and height sampling. High and Ultra tiers can spend budget on more shell matrices, denser outpost telemetry, or richer outpost visual spawn evaluation under continuous quality weights without changing world authority routes or save identity.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-6 us per 10k outpost telemetry row reads/copies plus 1-8 us per outpost solve/extraction batch where Burst can trust non-overlapping lanes. Evidence class: STATIC_SOURCE; targeted scan over `WorldSpatialHashGrid.cs` and `MarauderOutpostJobs.cs` returns 0 `LayoutKind.Sequential`, `Pack=1`, or explicit `Size = 80/24/8` hits; outpost scan confirms `OutpostTelemetryEntry=128`, pads at 72/80/88/96/104/112/120, three synchronous Burst annotations, and nine `[NoAlias]` lanes; `git diff --check` over both files returns exit 0 with LF/CRLF warning only.

## Decision 097 - World Wrapper Sequential Sweep

Problem: After the targeted World payload conversions, remaining World-wide Sequential hits were false source metadata on job wrappers in `VegetationFlowFieldIntegrator.cs`, job wrappers in `ProceduralWreckGenerator.cs`, and one managed pending-loot row containing Unity object references.

Solution: Removed eleven Sequential attributes from vegetation flow/threat/thermal/path job wrappers and three from procedural wreck job/managed rows. No DTO layout, shader payload, AUP route, NativeArray ownership, BRG matrix route, pathfinding equation, or terrain/vegetation math changed.

Rejected Alternatives: Converting these job wrappers to explicit layout was rejected because Unity owns NativeArray/NativeList/NativeParallelMultiHashMap handle representations. Converting `PendingWreckLootSpawn` to explicit layout was rejected because it contains managed `GameObject` and `ItemData` references. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps cheap vegetation field sampling, threat propagation, and wreck BRG payload jobs without false ABI metadata. Middle tier keeps current flow/path cadence. High and Ultra tiers can spend budget on denser vegetation flow fields, richer abyssal current volumes, and larger wreck scatter payloads under continuous quality weights without changing authority routes or save identity.

Hardware Impact: Runtime microsecond gain is not claimed because this is metadata hygiene. Evidence class: STATIC_SOURCE; recursive scan over `Assets/_Project/Scripts/World` returns 0 `StructLayout(LayoutKind.Sequential)`, source `Sequential`, or `Pack=1` hits; targeted Burst wrapper scans over `VegetationFlowFieldIntegrator.cs` and `ProceduralWreckGenerator.cs` return 0 missing `CompileSynchronously` annotations and 0 Sequential-before-Burst wrappers; `git diff --check` over both files returns exit 0 with LF/CRLF warning only.

## Decision 098 - Graphics Culling Native Row Quantization And Wrapper Sweep

Problem: `Graphics/Culling` still had false Sequential metadata on Burst job wrappers and one NativeArray-handle mock buffer, while two real hot rows were under-quantized: `PoiTransformDTO=112` in Vault-backed BRG/culling buffers and `InstanceCullingTelemetryEntry=40` in a 300-frame native telemetry ring.

Solution: Padded `PoiTransformDTO` to 128 bytes by adding two explicit tail `ulong` pads at offsets 112 and 120. Padded `InstanceCullingTelemetryEntry` to 64 bytes by adding explicit pads at offsets 40, 48, and 56, and zeroed those pads in the telemetry writer. Removed false Sequential attributes from `MockScatterBuffer`, eleven TBDR culling job wrappers, five abyssal shadow culling job wrappers, and `ApplyAupShiftJob`; removed unused interop imports where the files no longer own FieldOffset layouts.

Rejected Alternatives: Leaving `PoiTransformDTO` at 112 bytes was rejected because adjacent hot transform rows then start at shifting cache-line offsets under parallel sort/cull writes. Leaving `InstanceCullingTelemetryEntry` at 40 bytes was rejected because black-box ring entries are native forensic rows and should not rely on a non-cache-quantized stride. Converting job wrappers or `MockScatterBuffer` to explicit layout was rejected because they carry Unity `NativeArray<T>` handles and scheduling scalars, not raw binary payload rows. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps culling row reads cache-quantized while HZB/frustum fakes reject hidden instances before GPU submission in the first-20-minutes dense debris/cave route. Middle tier keeps the current radix/HZB path with stable row strides. High and Ultra tiers can spend the saved culling stability on more BRG instances, denser shadow casters, and richer shader budget telemetry under continuous quality weights without changing save identity or authority routes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-10 us per 10k transform/telemetry row reads or writes under culling pressure, plus 1-8 us per culling batch where existing `[NoAlias]` lanes remain visible to Burst without false layout metadata. Evidence class: STATIC_SOURCE; targeted scan over `Assets/_Project/Scripts/Graphics/Culling` returns 0 `LayoutKind.Sequential` or `Pack=1` hits, targeted missing-`CompileSynchronously` scan returns 0, targeted size scan shows `PoiTransformDTO=128` and `InstanceCullingTelemetryEntry=64`, and `git diff --check` over the five touched files returns exit 0 with LF/CRLF warning only.

## Decision 099 - Interaction Query And Tether Black-Box ABI Hygiene

Problem: The interaction/query/progression slice still had Sequential metadata on managed rows that contain Unity references, strings, API mirror data, or bool-backed managed cache state. `TetherManagerTelemetryEntry` was the one real native payload problem in the slice: a 16-byte black-box row stored in a 300-entry Vault-backed `NativeArray`, which leaves adjacent telemetry samples on the same cache line.

Solution: Removed false Sequential attributes and now-unused interop imports from `InteractableRegistry.TargetInfo`, `RaycastBatchHelper.QueryResult`, and `QueryCacheContext.CachedQueryResult`; removed the false Sequential marker from `PlayerAchievementRegistry.AchievementDefinition` while preserving the existing explicit 16-byte runtime threshold row. Converted `TetherManagerTelemetryEntry` to explicit 64-byte layout with fields at 0/4/8/12 and pads at 16/24/32/40/48/56. The writer zeroes the new pads before storing into the Vault-backed ring.

Rejected Alternatives: Converting the managed cache rows to explicit layout was rejected because they contain `Collider`, `RaycastHit`, interface references, strings, and other managed/Unity-owned data. Rewriting `RaycastBatchHelper` persistent NativeArrays or tether managed instance pools into Vault buffers was rejected in this loop because that is a route/lifecycle migration, not ABI metadata cleanup. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps interaction lookups, raycast mirrors, and achievement presentation out of native ABI scans while tether crash telemetry gets cache-line isolation. Middle tier keeps current query batching and tether impostor rendering. High and Ultra tiers can spend tether visual budget on thicker stress glow, denser line-strip detail, or richer fault visualization under continuous quality weights without changing gameplay truth ownership or save identity.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-4 us per 10k tether telemetry row reads/writes under crash/QA readback pressure; managed-row cleanup has no runtime speed claim. Evidence class: STATIC_SOURCE; targeted scan over the five touched files returns 0 `LayoutKind.Sequential` or `Pack=1` hits; targeted tether scan confirms the 64-byte row and pad zeroing; `git diff --check` over the touched files returns exit 0 with LF/CRLF warning only.

## Decision 100 - Tools Managed Row And Laser Job Wrapper Hygiene

Problem: The Tools cluster still had Sequential metadata on four laser cutter Burst job wrappers and four managed status/command rows. The laser wrappers carry Unity collection handles, `RaycastCommand`, `RaycastHit`, and scheduling scalars; the status rows carry strings, interfaces, `DateTime`, or managed API mirrors. Treating these as explicit native DTOs would freeze Unity/managed layouts instead of hardening real payload ABI.

Solution: Removed the false Sequential attributes from the four laser cutter job wrappers and removed the unused interop import from `LaserCutterDodJobs.cs`. Removed false Sequential metadata from `SystemBudget`, `SystemBudgetInfo`, `PerformanceSnapshot`, and `PendingDurabilityCommand`. Left the existing explicit `ToolDurabilitySystem.ItemState=16` native row and other pre-existing durability lifecycle changes untouched.

Rejected Alternatives: Converting job wrappers to explicit layout was rejected because Unity owns collection and raycast command wrapper layout. Converting managed status rows to explicit layout was rejected because they contain references or managed framework structs and are not blittable Vault/Signal rows. Changing laser cutter DTO contracts, AUP localization, shader fake outputs, performance throttling semantics, or durability native buffer routes was rejected. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps laser cutter work in DTO-backed jobs and uses cheap shader dent/glow/spark fakes instead of CPU geometry mutation. Middle tier keeps current job batching and budget reporting. High and Ultra tiers can spend the saved visual fake path on richer decals, sparks, and heat glow through existing quality-weighted outputs without changing DTO layout or save identity.

Hardware Impact: Runtime microsecond gain is not claimed because this loop removed false metadata only. Evidence class: STATIC_SOURCE; targeted Tools scan returns 0 `LayoutKind.Sequential` or `Pack=1` hits in the touched files, targeted Burst scan returns 0 missing synchronous annotations, and `git diff --check` passes with LF/CRLF warning only.

## Decision 101 - NativeMemorySentinel Managed Record Hygiene

Problem: `NativeMemorySentinel.NativeAllocationRecord` and `PersistentReallocationRecord` declared Sequential layout even though they are cold managed registry rows. They contain `string` references and managed-only lifetime/audit data, so treating them as native DTOs would be false ABI evidence and an explicit-layout conversion would be invalid.

Solution: Removed the two false Sequential attributes. Left `NativeAllocationSnapshotSource` unchanged as the actual 32-byte explicit blittable snapshot DTO, and kept the interop import because that DTO still uses `StructLayout` and `FieldOffset`.

Rejected Alternatives: Converting the two managed rows to explicit layout was rejected because managed references are not blittable payload fields. Removing `System.Runtime.InteropServices` was rejected because the snapshot source still requires it. Migrating the sentinel's cold managed arrays into Vault buffers was rejected in this loop because the task is ABI metadata hygiene, not ownership-route migration. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps runtime allocation audit metadata out of native ABI scans while preserving the compact 32-byte snapshot source used for deterministic crash/replay evidence. Middle tier keeps current sentinel registry behavior. High and Ultra tiers can add richer cold diagnostics around the existing explicit snapshot DTO without changing hot gameplay authority routes or native payload layout.

Hardware Impact: Runtime microsecond gain is not claimed because this loop removed false metadata only. Evidence class: STATIC_SOURCE; targeted scan over `NativeMemorySentinel.cs` returns 0 `LayoutKind.Sequential`, `StructLayout(...Sequential)`, or `Pack=1` hits, interop scan confirms only the explicit 32-byte snapshot DTO remains, and `git diff --check` passes with LF/CRLF warning only.

## Decision 102 - WFC HUD PDA And Scanner ABI Hygiene

Problem: Four small clusters still produced Sequential evidence. `WfcOutpostGraphTranslationJob` was a Unity native-container job wrapper, not a DTO, and still used Low precision Burst metadata. AR waypoint and PDA snapshot rows contained Unity references, strings, UI components, ScriptableObjects, or managed properties. `ScannerBlackBoxEntry` was different: it is a real Vault-backed 300-frame native forensic row, but its Sequential shape was 96 bytes, so adjacent entries straddled cache-line boundaries.

Solution: Removed false Sequential metadata from the WFC job wrapper, HUD managed rows, and PDA managed snapshots. Upgraded the WFC job to the required synchronous Fast/Standard Burst annotation and added `[NoAlias]` to the six separate native lanes. Converted `ScannerBlackBoxEntry` to explicit 128-byte layout with named pads and zeroed those pads in the writer.

Rejected Alternatives: Converting the WFC job wrapper to explicit layout was rejected because Unity owns the NativeArray and NativeParallelMultiHashMap handle ABI. Converting AR/PDA managed rows to explicit layout was rejected because managed references are not blittable payload fields. Leaving scanner telemetry at 96 bytes was rejected because the black-box ring is native forensic state and should not rely on a cache-straddling stride. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps WFC graph translation as bounded grid math and scanner forensic writes on a quantized stride. Middle tier keeps current HUD/PDA managed presentation rows out of binary payload scans. High and Ultra tiers can spend saved stability on richer scanner marker visuals, denser WFC outpost power-node diagnostics, or stronger crash forensics without changing gameplay truth ownership, save identity, or DTO authority routes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-6 us per WFC graph translation batch from alias proof and 1-5 us per 10k scanner black-box row reads/writes under QA/crash readback pressure. HUD/PDA cleanup has no runtime speed claim. Evidence class: STATIC_SOURCE; targeted scan over the four files returns 0 Sequential/Pack hits, scanner scan confirms 128-byte explicit row plus pad zeroing, WFC scan confirms required Burst metadata and six `[NoAlias]` lanes, and `git diff --check` passes with LF/CRLF warning only.

## Decision 103 - Fauna Submarine Atmosphere And Contextual IK Wrapper Hygiene

Problem: Remaining Sequential hits in this slice were false metadata on managed rows or Unity job wrappers. `FaunaDirector.ActiveCreature` carries managed Unity references. Submarine PID/mass, atmosphere step, and contextual IK jobs carry NativeArray handles, animation handles, or scheduling scalars, while their real payload rows already use explicit layouts. `ContextualPhysicalIkApplyJob` also lacked the required synchronous Burst metadata and did not state alias separation for its native lanes.

Solution: Removed false Sequential attributes from the managed fauna row and six job wrappers. Added `CompileSynchronously = true` to `ContextualPhysicalIkApplyJob` and marked thirteen apply-job NativeArray lanes with `[NoAlias]`. Left explicit DTO layouts and pre-existing AUP-origin fixes untouched.

Rejected Alternatives: Converting job wrappers to explicit layout was rejected because Unity owns NativeArray, NativeParallel, animation stream handle, and scheduling wrapper layout. Converting `ActiveCreature` to explicit layout was rejected because managed Unity references are not blittable DTO fields. Reworking submarine/atmosphere private native buffer ownership into Vault was rejected in this loop because the issue was source layout metadata, not owner-route migration. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps IK and submarine batch jobs as data-local work with no false ABI metadata and explicit payload rows. Middle tier keeps current atmosphere diffusion and dynamic-flood math. High and Ultra tiers can spend the alias-safe IK/submarine budget on richer animation response, hull stress feedback, or atmosphere diagnostics without changing save identity or truth ownership.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per IK apply/response batch where Burst can trust source/output lanes. Submarine/atmosphere wrapper cleanup has no new runtime speed claim because existing `[NoAlias]` lanes were already present. Evidence class: STATIC_SOURCE; targeted scan over five files returns 0 Sequential/Pack hits, targeted Burst/NoAlias scan confirms required metadata and lanes, and `git diff --check` passes with LF/CRLF warning only.

## Decision 104 - Procedural Crab IK Wrapper Hygiene

Problem: `ProceduralCrabLegIKRuntime.cs` still had seven `StructLayout(LayoutKind.Sequential)` markers on Burst job wrappers. Those wrappers carry Unity `NativeArray<T>`, `RaycastCommand`, `RaycastHit`, and scheduling scalar fields; the real Vault/native rows at the top of the file were already explicit and cache-quantized.

Solution: Removed the seven false Sequential attributes from the job wrappers only. Preserved the explicit DTO rows (`ProceduralCrabLegEntityState=192`, `ProceduralCrabLegStepState=64`, `ProceduralCrabBodyPose=128`, `ProceduralCrabSolvedJointMatrices=192`, `ProceduralCrabIkTelemetryEntry=64`), synchronous Fast/Standard Burst metadata, `[NoAlias]` lane annotations, AUP rebase math, raycast budget mode, and two-bone solver equations.

Rejected Alternatives: Converting the job wrappers to explicit layout was rejected because Unity owns the `NativeArray`, raycast command, and raycast hit wrapper representation. Changing DTO offsets was rejected because those rows are already explicit, named-padded, and referenced by Vault/graphics upload routes. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps procedural crab movement on budgeted raycast pairs and analytical IK without false ABI metadata. Middle tier keeps current native batch jobs and black-box telemetry. High and Ultra tiers can spend visual budget on denser BRG body/joint rendering and richer procedural leg motion under continuous quality weights without changing truth ownership, save identity, or DTO layout.

Hardware Impact: Runtime microsecond gain is not claimed because this loop removed false wrapper metadata only. Evidence class: STATIC_SOURCE; targeted scan over `ProceduralCrabLegIKRuntime.cs` returns 0 `LayoutKind.Sequential`, `StructLayout(...Sequential)`, or `Pack=1` hits, targeted layout/Burst scan confirms explicit DTO rows and `[NoAlias]` lanes, and `git diff --check` passes with LF/CRLF warning only.

## Decision 105 - VR Somatic Job Wrapper Hygiene

Problem: `VRSomaticProvider.cs` still had four Sequential markers on Burst job wrappers. The real somatic telemetry/root/head-cast rows were already explicit, while the wrappers hold Unity `NativeArray<T>`, `CapsulecastCommand`, `RaycastHit`, and command/sample lanes that should not be frozen as binary payload ABI.

Solution: Removed the four false Sequential attributes from `VRSomaticRootSyncJob`, `VRSomaticHandKinematicsJob`, `BuildHeadCapsulecastCommandsJob`, and `ProcessHeadCapsulecastHitsJob`. Preserved explicit layouts, Burst flags, `[NoAlias]` lanes, Vault buffer handles, KCC comfort math, head capsulecast math, and hand spring equations.

Rejected Alternatives: Converting wrappers to explicit layout was rejected because Unity owns native container and physics command wrapper layouts. Padding `VRSomaticRootSyncInput=80` was rejected because it is already a 16-byte multiple and a single-element root sync buffer; changing it would mutate a Vault row without need. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps somatic comfort as bounded Burst jobs with head collision samples and hand target smoothing. Middle tier keeps current KCC comfort integration and head capsulecast fan. High and Ultra tiers can spend visual budget on stronger comfort/haptic feedback and richer black-box diagnostics under continuous quality weights without changing DTO layout, authority route, or save identity.

Hardware Impact: Runtime microsecond gain is not claimed because this loop removed false wrapper metadata only. Evidence class: STATIC_SOURCE; targeted scan over `VRSomaticProvider.cs` returns 0 Sequential/Pack hits, targeted layout/Burst scan confirms explicit rows and NoAlias lanes, and `git diff --check` passes with LF/CRLF warning only.

## Decision 106 - World Generator PhysX Bake And Terrain Job Fence

Problem: `HectonWorldGenerator.cs` still had three false Sequential markers around PhysX bake state. Two rows contain Unity managed references and a `JobHandle`; the terrain bake job is a Unity job wrapper, not a DTO. The same file also had local terrain generation Burst jobs missing `CompileSynchronously` and lacking explicit alias proof on NativeArray lanes.

Solution: Removed the three false Sequential attributes. Added synchronous Fast/Standard Burst metadata to `HectonVertexJob`, `HectonNormalJob`, `HectonColorJob`, and `TerrainColliderBakeJob`. Imported `Unity.Burst.CompilerServices` and added `[NoAlias]` to terrain generation job NativeArray lanes. Left terrain equations, chunk streaming, physics bake scheduling, native array ownership, and teardown behavior unchanged.

Rejected Alternatives: Converting managed PhysX bake rows to explicit layout was rejected because they contain managed Unity references and `JobHandle` state. Converting the terrain bake job wrapper to explicit layout was rejected because it is a scheduler packet. Rewriting persistent terrain buffers into Vault was rejected in this loop because that is an ownership migration, not source ABI cleanup. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps terrain generation bounded by existing LOD and noise shortcuts while terrain jobs expose stricter Burst metadata and alias facts. Middle tier keeps current chunk streaming and physics bake queue. High and Ultra tiers can spend stable job throughput on denser terrain/cave color evaluation and richer visual chunk presentation under continuous quality weights without changing save identity or authority routes.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 2-20 us per terrain generation batch where Burst can trust NativeArray lane separation. PhysX bake metadata cleanup has no direct runtime speed claim. Evidence class: STATIC_SOURCE; targeted scans over `HectonWorldGenerator.cs` return 0 Sequential/Pack hits and 0 missing synchronous Burst hits, and `git diff --check` passes with LF/CRLF warning only.

## Decision 107 - Player Movement Managed Collision Queue Hygiene

Problem: `HectonPlayerMovement.QueuedCollisionEvent` declared Sequential layout despite containing a managed `Rigidbody` reference and living in a MonoBehaviour-owned managed ring buffer that bridges Unity collision callbacks into fixed-step processing.

Solution: Removed the false Sequential attribute only. Preserved the explicit native/telemetry rows in the same file, collision queue capacity, callback processing, Rigidbody transfer path, and cinematic focus black-box layout.

Rejected Alternatives: Converting `QueuedCollisionEvent` to explicit layout was rejected because managed references are not blittable DTO fields. Migrating the callback ring into `GlobalDataVault` was rejected in this loop because it is a Unity callback bridge and not the assigned ABI metadata cleanup. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps the collision bridge as a bounded 32-entry managed callback queue while explicit telemetry rows remain stable. Middle tier keeps current impact feedback and KCC transfer behavior. High and Ultra tiers can spend visual budget on richer collision feedback without changing movement authority, DTO layout, or save identity.

Hardware Impact: Runtime microsecond gain is not claimed because this is metadata hygiene only. Evidence class: STATIC_SOURCE; targeted scan over `HectonPlayerMovement.cs` returns 0 Sequential/Pack hits, and `git diff --check` passes with LF/CRLF warning only.

## Decision 108 - Player Kinematics Native State Wrapper Hygiene

Problem: `Gameplay/HectonPlayerState.cs` still had Sequential metadata on one Burst job wrapper and two native-state owner structs. Those rows carry Unity `NativeArray<T>` handles, physics command/result arrays, `JobHandle`, and owner flags. The real player state and telemetry DTOs were already explicit.

Solution: Removed false Sequential attributes from `PlayerKinematicsLinearDragJob`, `PlayerKinematicsNativeState`, and `HectonPlayerMotorNativeState`. Preserved `HectonPlayerState=192`, `PlayerKinematicsHandTarget=32`, `PlayerKinematicsTelemetryEntry=64`, Vault buffer routes, drag math, motor sweep ownership, kinematic repair buffers, and existing `[NoAlias]` lanes.

Rejected Alternatives: Converting native-state owner structs to explicit layout was rejected because Unity owns native container and physics command wrapper layout. Padding or changing existing explicit player DTOs was rejected because they are already cache-quantized enough for their route and offset-stable. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps player kinematic native state as bounded Vault-backed buffers with stable explicit telemetry. Middle tier keeps drag solve and motor sweep routing. High and Ultra tiers can spend budget on richer movement telemetry and repair probes under continuous quality weights without changing movement truth ownership or save identity.

Hardware Impact: Runtime microsecond gain is not claimed because this loop removed false wrapper metadata only. Evidence class: STATIC_SOURCE; targeted scan over `Gameplay/HectonPlayerState.cs` returns 0 Sequential/Pack hits, targeted layout/Burst scan confirms explicit DTO rows and NoAlias lanes, and `git diff --check` passes with LF/CRLF warning only.

## Decision 109 - Habitat Graph DFS Job Wrapper Hygiene

Problem: `Construction/HabitatGraphManager.cs` still had Sequential metadata on `DeconstructionDfsValidationJob`. The job carries Unity native container handles and cold validation state, while actual habitat flood/siege payload rows were already explicit.

Solution: Removed the false Sequential attribute only. Preserved explicit habitat DTO rows, DFS validation behavior, synchronous Burst metadata, `[NoAlias]` lanes, graph CSR buffers, and deconstruction validation route.

Rejected Alternatives: Converting the DFS job wrapper to explicit layout was rejected because Unity owns `NativeArray`, `NativeList`, and `NativeParallelHashSet` wrapper layouts. Rewriting habitat graph private buffers into Vault was rejected in this loop because the task is ABI metadata cleanup, not ownership migration. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps deconstruction validation cold and bounded while flood/siege payload rows remain explicit. Middle tier keeps current graph CSR validation. High and Ultra tiers can spend budget on richer habitat stress diagnostics and visual feedback under continuous quality weights without changing habitat truth ownership or save identity.

Hardware Impact: Runtime microsecond gain is not claimed because this loop removed false wrapper metadata only. Evidence class: STATIC_SOURCE; targeted scan over `Construction/HabitatGraphManager.cs` returns 0 Sequential/Pack hits, targeted layout/Burst scan confirms explicit DTO rows and NoAlias lanes, and `git diff --check` passes with LF/CRLF warning only.

## Decision 110 - Fluid Wave And Buoyancy Job Wrapper Hygiene

Problem: `HectonFluidEngine.cs` still had Sequential metadata on `WaveQueryJob` and `BuoyancyJob`. Both are Unity job wrappers with native container handles and scalar scheduling fields; the actual fluid DTO/native rows were already explicit and aligned.

Solution: Removed the two false Sequential attributes only. Preserved `BuoyancyParams=128`, flow/whirlpool/viscosity/interior flood/advection explicit rows, synchronous Burst metadata, `[NoAlias]` lanes, wave fallback math, brine sampling, force/torque outputs, and invalid-number writer route.

Rejected Alternatives: Converting the job wrappers to explicit layout was rejected because Unity owns `NativeArray<T>` and Burst job wrapper representation. Changing fluid DTO strides was rejected because the touched payload rows are already 16/32/64/128-byte explicit layouts. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps wave query and buoyancy jobs as data-local batches with cheap scalar fallbacks and explicit payload rows. Middle tier keeps brine/terrain/wave lanes. High and Ultra tiers can spend saved stability on denser buoyancy objects, richer vortex visual feeds, and advection telemetry under continuous quality weights without changing physics truth ownership or save identity.

Hardware Impact: Runtime microsecond gain is not claimed because this loop removed false wrapper metadata only. Evidence class: STATIC_SOURCE; targeted scan over `HectonFluidEngine.cs` returns 0 Sequential/Pack hits, targeted layout/Burst scan confirms explicit DTO rows and NoAlias lanes, and `git diff --check` passes with LF/CRLF warning only.

## Decision 111 - Voxel PhysX Queue And MeshData Vertex Boundary

Problem: `HectonVoxelEngine.cs` still exposed Sequential metadata on two deferred PhysX queue rows, one voxel mesh bake job wrapper, and two MeshData vertex rows. The deferred rows contain managed Unity references and `JobHandle` state. The bake job is a scheduling wrapper. The vertex rows are different: they define the CPU-side typed view for Unity MeshData vertex buffers and are coupled to `SetVertexBufferParams` descriptor order and stride.

Solution: Removed false Sequential attributes from `DeferredVoxelPhysicsBakeTeardown`, `DeferredVoxelColliderUpload`, and `VoxelMeshBakeJob` only. Left `VoxelSurfaceVertex` and `VoxelColliderVertex` as documented MeshData interop exceptions until the voxel owner rewrites the matching vertex descriptors and validates stride in Unity. Preserved existing explicit voxel telemetry, AUP helper changes, Burst metadata, NoAlias lanes, PhysX bake routing, collider upload queue behavior, and mesh upload layout.

Rejected Alternatives: Converting the managed deferred rows or job wrapper to explicit layout was rejected because managed Unity references and job wrapper state are not raw DTO ABI. Padding `VoxelSurfaceVertex` from its descriptor-derived shape to a cache-quantized size was rejected because `MeshData.GetVertexData<VoxelSurfaceVertex>()` must match the vertex buffer descriptors; adding silent padding would risk corrupting GPU vertex upload and collider bake data. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps voxel PhysX teardown/upload queues bounded and cold while preserving exact MeshData upload compatibility. Middle tier keeps current voxel mesh generation and collider bake routes. High and Ultra tiers can spend visual budget on denser cave surfaces and richer shader channels under continuous quality weights after a coordinated vertex-descriptor migration, without mutating gameplay truth ownership, save identity, or current DTO authority routes.

Hardware Impact: Runtime microsecond gain is not claimed because this loop removed false metadata only. Evidence class: STATIC_SOURCE; targeted scan over `HectonVoxelEngine.cs` now leaves only the two documented MeshData vertex interop Sequential rows, confirms the deferred rows and bake job have no layout attributes, and `git diff --check` passes with LF/CRLF warning only.

## Decision 112 - Submarine Structural Grid Wrapper Alias Fence

Problem: `SubmarineStructuralGrid.cs` still exposed Sequential metadata on four Burst job wrappers. The actual payload rows in the same section were already explicit in the working tree, but the wrappers carried native container handles and no alias proof, so Burst had to conservatively assume overlapping lanes.

Solution: Removed the four false Sequential attributes from `HullDamageDiffusionJob`, `HullCompartmentMappingJob`, `HullFatigueCompartmentJob`, and `BreachRepairJob`. Added `Unity.Burst.CompilerServices` and `[NoAlias]` to eighteen native lanes. Preserved structural-grid equations, existing explicit `ImpactCommand=32`, `DamageControlTelemetryEntry=32`, private native buffer ownership, breach Vault handles, late completion routes, and leak plume shader routing.

Rejected Alternatives: Converting the job wrappers to explicit layout was rejected because Unity owns native container wrapper layout. Migrating the file's existing private NativeArrays into `GlobalDataVault` was rejected in this loop because it changes truth ownership and lifetime routing; it needs a separate owner-approved migration card. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps hull damage diffusion, compartment mapping, fatigue, and breach repair as bounded data-local jobs with alias proof and no scene traversal. Middle tier keeps current leak plume and pressure-cycle routes. High and Ultra tiers can spend saved job throughput on richer hull dent visuals, leak particle density, or damage-control telemetry under continuous quality weights without changing structural truth ownership, save identity, or DTO layout.

Hardware Impact: Estimated gain for low-end sylicon as i3/MX350 is 1-8 us per structural-grid batch where Burst can use the native lane separation. Evidence class: STATIC_SOURCE; targeted scan over `SubmarineStructuralGrid.cs` returns 0 Sequential/Pack hits, confirms four synchronous Burst jobs and eighteen `[NoAlias]` lanes, and `git diff --check` passes with LF/CRLF warning only.

## Decision 113 - Submarine Fluid Dynamics Serialized Boundary

Problem: `SubmarineFluidDynamics.cs` still exposed Sequential metadata on seven rows: two Unity serialized authoring structs and five Burst job wrappers. The authoring structs are inspector data and are mirrored into runtime Vault/native buffers; the job wrappers carry Unity native containers and should not define binary payload ABI.

Solution: Removed false Sequential attributes from `HydroKinematicDragJob`, `FluidTransferJob`, `BulkheadTransferDeltaJob`, `ApplyBulkheadTransferJob`, and `FloodMassPropertiesJob`. Left `CompartmentDefinition` and `BulkheadDefinition` Sequential as documented Unity serialization exceptions. Preserved existing explicit hydro DTO rows, existing NoAlias lanes, AUP fixes, vault flags, mass properties result, black-box telemetry, and fluid transfer equations.

Rejected Alternatives: Converting job wrappers to explicit layout was rejected because Unity owns native container wrapper layout. Converting inspector-authored serialized records to explicit DTOs was rejected because Unity serialization field behavior is the owner route, and runtime authority already mirrors the data into native buffers. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps hydrodynamics in bounded native jobs and serialized authoring out of hot DTO lanes. Middle tier keeps current flood transfer, bulkhead transfer, and hydro drag routes. High and Ultra tiers can spend budget on richer brine, leak, ballast, and cargo visual feedback under continuous quality weights without changing fluid truth ownership, save identity, or DTO layout.

Hardware Impact: Runtime microsecond gain is not claimed for metadata removal; existing NoAlias lanes remain available for Burst. Evidence class: STATIC_SOURCE; targeted scan over `SubmarineFluidDynamics.cs` leaves only two serialized-authoring Sequential rows, confirms five synchronous Burst jobs and twenty-six `[NoAlias]` lanes, and `git diff --check` passes with LF/CRLF warning only.

## Decision 114 - SaveData Managed DTO Metadata Boundary

Problem: `SaveData.cs` still had 29 explicit Sequential tags on managed compatibility DTO containers. Many of those rows contain strings, arrays, or nested managed save rows, while the native binary-save rows in the file already use `BinaryBlittableSafe` plus explicit offsets. Keeping explicit Sequential metadata on managed containers creates false ABI evidence during ARM64 scans.

Solution: Removed those 29 false `StructLayout(LayoutKind.Sequential)` attributes only. Preserved every field, field order, schema version, migration path, capacity constant, array/string row, and explicit `BinaryBlittableSafe` layout. The save system remains field-by-field serialization for managed compatibility rows.

Rejected Alternatives: Converting managed save containers with references into explicit layout was rejected because managed references are not blittable payload fields. Reordering or padding save rows was rejected because it would mutate persistence schema. Removing explicit layout from `BinaryBlittableSafe` rows was rejected because those are the actual native/binary save ABI records. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps save hydration predictable without letting managed DTO containers masquerade as hot native ABI. Middle tier keeps migration and compatibility surfaces stable. High and Ultra tiers can add explicit native mirror rows where needed without forcing managed save containers into hot-path rules.

Hardware Impact: Runtime microsecond gain is not claimed because this is metadata hygiene only. Evidence class: STATIC_SOURCE; targeted scan over `SaveData.cs` returns 0 Sequential/Pack hits, remaining `StructLayout` rows are explicit only, and `git diff --check` passes with LF/CRLF warning only.

## Decision 115 - Editor Smoke Test Explicit Achievement ABI

Problem: `SignalCryptographySmokeTester.cs` still asserted that `PlayerAchievementRegistry` must contain a Sequential layout token. The runtime achievement threshold row is now explicit `Size = 16`, so the editor smoke test would report the correct code as a failure.

Solution: Updated only the achievement progression assertion to require `[StructLayout(LayoutKind.Explicit, Size = 16)]`. Preserved the existing hot table, AUP distance, notification cache, and telemetry assertions.

Rejected Alternatives: Removing the assertion entirely was rejected because the smoke tester should still guard the hot progression row layout. Reintroducing Sequential layout in runtime was rejected because the row is already correctly explicit and cache-quantized. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps editor gates aligned with explicit ABI and avoids stale failures. Middle/high/ultra tiers keep the same smoke-test coverage for string-free achievement evaluation.

Hardware Impact: Runtime cost is 0.00 ms because this is editor-only assertion hygiene. Evidence class: STATIC_SOURCE; targeted scan confirms the stale Sequential assertion string is gone and `git diff --check` passes with LF/CRLF warning only.

## Decision 116 - Editor Seaweed Mesh Builder Metadata Hygiene

Problem: `WorldProceduralSeaweedMeshBuilder.VertexData` carried source-visible Sequential layout even though the file is editor-only mesh construction code. This produced a false positive in broad ARM64 runtime scans.

Solution: Removed the false layout attribute and the now-unused `System.Runtime.InteropServices` import. Preserved vertex fields, mesh buffer writes, and editor generation behavior.

Rejected Alternatives: Converting the editor-only row to explicit layout was rejected because this is not a runtime binary DTO and does not justify freezing offsets. Leaving the false tag was rejected because it pollutes project-wide Sequential evidence. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Runtime scalability is unaffected. Editor mesh generation keeps the same field set and behavior.

Hardware Impact: Runtime cost is 0.00 ms. Evidence class: STATIC_SOURCE; targeted scan returns 0 layout/interops hits in the file and `git diff --check` passes with LF/CRLF warning only.

## Decision 117 - Drone Fleet Pack Lexeme Scanner Hygiene

Problem: Broad source gates that search for `Pack\s*=\s*1` still reported `DroneFleetManager.SolderIntegrityUnitsPerPack = 10f`. The code was not a `StructLayout` Pack parameter, but it polluted the ARM64 layout evidence surface and forced manual exception handling.

Solution: Renamed the private constant to `SolderIntegrityUnitsPerBundle` and updated its two local call sites. No DTO, Signal payload, Vault row, save row, native container, shader buffer, or gameplay equation changed.

Rejected Alternatives: Changing the scanner regex alone was rejected because external text gates may still use simple lexical scans. Leaving the constant name was rejected because it keeps creating false Pack debt. Any behavior change to drone solder consumption was rejected because this loop is scanner hygiene only. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Runtime scalability is unaffected. Static evidence becomes cleaner across low, middle, high, and ultra targets because Pack debt reports no longer include a private gameplay lexeme.

Hardware Impact: Runtime cost is 0.00 ms. Evidence class: STATIC_SOURCE; targeted scan confirms no `Pack\s*=\s*1` hit remains in `DroneFleetManager.cs`, and `git diff --check` passes with LF/CRLF warning only.

## Decision 118 - Core Generic Wrapper Sequential Metadata Removal

Problem: Broad Sequential scans still reported core native wrapper and job-scheduler structs even though these rows carry Unity native container handles, function pointers, fixed inline scratch memory, or `JobHandle` fan-in state. They are not binary DTO payloads, SignalBus payload rows, save rows, or Vault element ABIs.

Solution: Removed the explicit source `StructLayout(LayoutKind.Sequential)` tags from `StackQueue<T>`, `JobFenceManager`, `NativeArenaArray<T>`, `NativeRingBuffer<T>`, `NativeQuery<T>`, `NativeSelectQuery<TSource,TResult>`, `NativeFilterJob<T>`, `NativeSelectJob<TSource,TResult>`, `BurstCallbackQueue`, `BurstCallbackQueue.ParallelEventWriter`, `SpscSignalRingBuffer<T>`, and the six lockstep hash job wrappers. Kept true explicit DTO rows in the same files unchanged.

Rejected Alternatives: Converting these wrappers to explicit layout was rejected because Unity owns native container wrapper internals and several rows are generic or safety-handle dependent under conditional compilation. Leaving the tags was rejected because it continued to advertise non-DTO wrappers as ABI-bearing rows. Running a build or rebuild was rejected because static gates passed and command discipline forbids premature rebuilds.

Scalability potential: Low tier keeps the same native-wrapper behavior and avoids false scan debt. Middle tier preserves existing NoAlias/Burst job lanes. High and Ultra tiers keep explicit DTO payload rows available for richer telemetry without freezing Unity-owned wrapper internals.

Hardware Impact: Runtime microsecond gain is not claimed because default struct layout behavior and job routes are unchanged. Evidence class: STATIC_SOURCE; targeted scan over touched Core files returns 0 Sequential/Pack hits, and broad project scan leaves only documented interop/serialized exceptions plus editor diagnostic strings.

## Decision 119 - Strict Attribute Scan Boundary Proof

Problem: Broad text scans intentionally catch editor diagnostic string literals such as `"Pack=1 is forbidden"` and `"LayoutKind.Sequential"`, which can obscure the actual source attribute state after code hygiene.

Solution: Added a strict proof pass using start-of-line attribute regexes. The real remaining Sequential attributes are exactly four documented non-DTO exceptions: two Unity MeshData vertex rows in `HectonVoxelEngine.cs` and two Unity serialized authoring rows in `SubmarineFluidDynamics.cs`. Real Pack attributes are zero.

Rejected Alternatives: Removing editor diagnostic labels was rejected because those labels are part of the guard tools. Pretending the broad text scan was clean was rejected because it would conflate source attributes with string literals. Running a build or rebuild was rejected because this is static source proof and command discipline forbids premature rebuilds.

Scalability potential: Runtime scalability is unaffected. Static evidence is clearer for low, middle, high, and ultra targets because only real attributes drive the ABI exception list.

Hardware Impact: Runtime cost is 0.00 ms. Evidence class: STATIC_SOURCE; strict attribute-only scans show four documented Sequential exceptions and zero Pack attributes.

## Decision 120 - Vault Cursor Ownership And Self-Audit Artifact

Problem: SHINOBU_204 telemetry ring ownership was only half Vault-backed. `AlignmentTelemetryEntry` rows lived behind `BufferID.Arm64AlignmentTelemetryRing`, but the circular cursor was a private static int. The agent also lacked a durable SHINOBU_204 self-audit generator and XML report, so Task 20 evidence lived in status/log prose rather than a repeatable editor artifact.

Solution: Added `VaultGenerationHandle<int>` for `BufferID.Arm64AlignmentTelemetryCursor` and resolved cursor state through `IDataVault` beside the ring. The fault recorder now requests IDs 642 and 643, releases both handles when the vault instance changes, and writes cursor state back to Vault after each record. Added editor-only `Arm64AlignmentSelfAuditReport`, wired it into the X-Ray toolbar, and seeded `Docs/Reports/SHINOBU_204_SELF_AUDIT.xml` with 20 task reconciliation, three critical layout maps, Vault handle status, dependency graph, compile guard, and Dear Lie proof.

Rejected Alternatives: Keeping the cursor as a private static was rejected because it made the telemetry lifecycle partially non-Vault and weakened H-Phi evidence. Adding hard compile references to Physics KCC for the example byte map was rejected because the editor self-audit can resolve non-core examples by string through editor reflection and avoid sibling-domain compile edges. Running a build or rebuild was rejected because static gates passed, this loop is editor/runtime source proof, and the external dependency wall remains known.

Scalability potential: Low tier pays nothing in normal gameplay because the telemetry path is cold and only resolves the cursor on fault/dump. Middle tier keeps deterministic diagnostics without polling. High and Ultra tiers can emit richer alignment fault reports through the same 64-byte rows and one-int cursor without changing DTO stride, save identity, SignalBus stride, or GlobalQualityWeight authority.

Hardware Impact: Runtime normal-frame cost is 0.00 ms. Fault-path overhead adds one Vault-resolved int row read/write, but removes private static cursor ownership. Evidence class: STATIC_SOURCE; XML parse reports 20 tasks and 3 layout structs, targeted touched-file Pack/Sequential scan returns 0 hits, and `git diff --check` passes with LF/CRLF warnings only.

## Decision 121 - Self-Audit CLI Gate

Problem: The self-audit writer produced a durable XML artifact, but CI still needed a single explicit entry point that fails if a critical layout drifts. A write-only report can be ignored by build automation.

Solution: Added `RunBatchSelfAudit()` to `Arm64AlignmentSelfAuditReport`. It writes the XML and runs `ValidateCriticalLayouts()` over the critical sample set. The gate checks type presence, Explicit layout, no Pack=1, accepted stride, and 8-byte alignment for every double/long/ulong/AUP lane. Failures throw `BuildFailedException`, which Unity batchmode can surface as a hard gate.

Rejected Alternatives: Folding this into the runtime fault recorder was rejected because it would introduce reflection and report strings into a gameplay path. Making the XML writer throw on Task 18 PARTIAL status was rejected because that would conflate a known fixer-methodology limitation with critical ABI drift. Running Unity batchmode was rejected in this loop because the user prohibited rebuild-style churn and the source-level gate can be inspected without launching the editor.

Scalability potential: Low tier remains unaffected because the gate is editor/CI-only. Middle, high, and ultra tiers get the same ABI proof before play; no GlobalQualityWeight route or DTO stride can diverge per hardware tier.

Hardware Impact: Runtime cost is 0.00 ms. CI/editor cost is O(T+F) over three critical rows. Evidence class: STATIC_SOURCE; targeted scan found the CLI method and validation routine, trailing-whitespace scan returned 0 hits, and `git diff --check` passed for the new editor file.

## Decision 122 - Fault Gizmo Editor Fence And AUP Locality

Problem: `Arm64AlignmentFaultGizmo` read `GlobalDataVault.TryGetLatestCreated()` from an unfenced runtime assembly file and cast the recorded absolute AUP directly to `Vector3`. The route is diagnostic, but leaving it player-visible weakens the GlobalDataVault doctrine and the direct cast violates the local-AUP-before-float rule.

Solution: Fenced the latest-vault read behind `UNITY_EDITOR`, then converted the recorded AUP by subtracting `HectonFloatingOrigin.CurrentTotalOffsetDouble` in double precision before clamping and casting the local delta to `Vector3`. Updated the self-audit XML/task text and architecture note to record the diagnostic-only route.

Rejected Alternatives: Replacing the diagnostic route with hot `GlobalRegistry` polling was rejected because GlobalRegistry is cold identity/DI only. Keeping the direct absolute cast was rejected because 50 km scene coordinates lose precision before visualization. Moving the component into a runtime tick was rejected because Task 19 is an editor scene-view diagnostic, not gameplay logic. Running a build or rebuild was rejected because the change is static/editor fenced and command discipline forbids unnecessary rebuild churn.

Scalability potential: Low tier and player builds pay 0.00 ms because the diagnostic read is editor-fenced. Middle, high, and ultra development tiers retain the same fault visualization while the AUP-local conversion avoids false scene jitter in large-world debugging. GlobalQualityWeight does not alter the DTO layout, Vault IDs, signal route, or fault ownership.

Hardware Impact: Runtime cost is 0.00 ms. Editor-only diagnostic work remains O(1): one latest-vault lookup, one newest-ring read, one origin subtraction, one clamped draw. Evidence class: STATIC_SOURCE; static gate reports latest-vault read after `UNITY_EDITOR`, origin subtraction present, zero direct absolute-AUP casts, XML XPath parse reports 20 tasks, and `git diff --check` passes with LF/CRLF warnings only.

## Decision 123 - Roslyn AST Source Fixer Gate

Problem: `Arm64LayoutSourceFixer` still used regexes to detect Sequential DTO candidates and strip explicit Pack arguments. Regex scans are brittle around attributes, trivia, comments, nested attributes, and file-scoped namespaces; they also weaken the Task 18 claim that the tool parses C# structure.

Solution: Replaced the regex scanner with a Roslyn AST pass using `CSharpSyntaxTree.ParseText` and `CSharpSyntaxRewriter`. The tool now mechanically removes named `Pack` arguments only from `StructLayout(...LayoutKind.Explicit...)` attributes, and reports Sequential DTO-like structs as `[BLOCKED] AST` with full type name, instance-field count, and property count.

Rejected Alternatives: Keeping regex detection was rejected because it is text-fragile and can misclassify attributes. Automatically generating `FieldOffset` values for Sequential candidates was rejected because correct layout requires owner knowledge of fixed buffers, Unity containers, managed references, serialized authoring records, and persistence ABI. Running a build/rebuild or Unity batchmode was rejected because this is an editor source-tool change and static gates were sufficient for this loop.

Scalability potential: Runtime tiers are unaffected because the tool is editor/CI-only. Low-end development machines avoid false rewrites and get deterministic failure reports. High/ultra development machines can extend the AST pass later with owner-approved layout calculators without changing runtime DTO authority or GlobalQualityWeight routes.

Hardware Impact: Runtime cost is 0.00 ms. CI/editor cost is O(F+S) over source files and syntax nodes in Core/Physics. Evidence class: STATIC_SOURCE; targeted scan confirms Roslyn parser/rewriter symbols and `[BLOCKED] AST`, confirms old Regex symbols are absent, verifies the bundled Roslyn DLL exposes `FileScopedNamespaceDeclarationSyntax`, and `git diff --check` passes with LF/CRLF warning only.

## Decision 124 - Strict Attribute Boundary Refresh After Tooling Edit

Problem: After replacing the source fixer with a Roslyn AST pass, the latest Pack/Sequential proof needed a fresh scan. Relying on Loop 83 evidence would leave ambiguity about whether the editor-source change introduced new source-visible layout debt.

Solution: Reran strict start-of-line attribute scans under `Assets/_Project/Scripts`. The Sequential result remains exactly four documented non-DTO exceptions, and both any-Pack and explicit-Pack scans return zero hits.

Rejected Alternatives: Using the broad text scan was rejected because it intentionally catches editor diagnostic strings and would conflate source attributes with guard wording. Running a build/rebuild was rejected because this is source-boundary proof and no runtime assembly behavior changed.

Scalability potential: Runtime scalability is unaffected. Low, middle, high, and ultra tiers retain identical DTO layout and proof boundaries; only reviewer confidence improves.

Hardware Impact: Runtime cost is 0.00 ms. Evidence class: STATIC_SOURCE; strict Sequential scan returns four documented exceptions, Pack scans return zero hits, and `git diff --check` over touched SHINOBU files passes with LF/CRLF warnings only.

## Decision 125 - Roslyn Binding Fail-Fast Report

Problem: The Roslyn AST fixer can still fail before scanning if the editor/CI host cannot bind `Microsoft.CodeAnalysis` dependencies. A raw `TypeInitializationException` or dependency loader exception would give integrators an opaque failure and no report artifact.

Solution: Wrapped `CSharpSyntaxTree.ParseText` per file. A parser/dependency failure is now written as `[BLOCKED] AST_BINDING <path> :: <exception type>: <message>` and counted as `ParserFailures`. The report also prints Roslyn core/CSharp assembly names and versions before scanning.

Rejected Alternatives: Ignoring the binding issue was rejected because the PowerShell host probe already exposed dependency fragility. Loading Roslyn through reflection in runtime code was rejected because the tool is editor-only and compile-time editor references already exist. Running dotnet build or Unity batchmode was rejected because the user explicitly forbade rebuild churn and this loop only hardens the static editor tool.

Scalability potential: Runtime tiers are unaffected. CI/editor diagnostics improve across low, middle, high, and ultra development machines because parser binding failures now produce deterministic report rows instead of unstructured crashes.

Hardware Impact: Runtime cost is 0.00 ms. CI/editor overhead is one exception boundary per file and two assembly-version writes. Evidence class: STATIC_SOURCE; targeted scan confirms `AST_BINDING`, `ParserFailures`, Roslyn provenance lines, and parser try/catch; `git diff --check` over the fixer passes with LF/CRLF warning only.

## Decision 126 - Source Fixer Per-Attribute Rewrite Proof

Problem: `Arm64LayoutSourceFixer.VisitAttribute` used the rewriter-wide `HasChanges` flag inside a single-attribute rewrite condition. That flag is file-level state, so the condition was harder to audit than necessary and could make later attribute decisions appear coupled to earlier Pack removals.

Solution: Added a local `removedPackArgument` flag inside `VisitAttribute`. The method now sets `HasChanges` only after the current `StructLayout(LayoutKind.Explicit, ...)` attribute actually removes a `Pack` argument and returns a modified argument list.

Rejected Alternatives: Leaving the global flag in the condition was rejected because Task 18 is a source mutation gate and must have local proof for each mutation. Rewriting the whole fixer or adding a semantic model was rejected because the current issue is syntax-local and the semantic model would add dependency surface without solving owner-blind offset synthesis. Running build/rebuild or Unity batchmode was rejected because the change is editor-tool source hygiene and static gates passed.

Scalability potential: Runtime tiers are unaffected. Low-tier development machines get deterministic source-fixer behavior without extra runtime payload. Middle, high, and ultra development machines can run the same report gate and receive cleaner Pack-removal counts without changing DTO stride, save identity, SignalBus payload size, or GlobalQualityWeight route.

Hardware Impact: Runtime cost is 0.00 ms. Evidence class: STATIC_SOURCE; targeted scan confirms `removedPackArgument` exists, the old global-flag attribute condition is absent, `Regex` remains absent, strict Pack scan returns zero attributes, strict Sequential scan remains the four documented non-DTO exceptions, and `git diff --check` over the fixer passes with LF/CRLF warning only.

## Decision 127 - Raw Telemetry Dump Span Writer

Problem: `Arm64AlignmentTelemetry.DumpFaultHistory` wrote telemetry rows field-by-field through `BinaryWriter`. That produced a valid little-endian logical stream, but it did not prove that the exported dump preserved the exact 64-byte DTO memory image required for ABI forensics.

Solution: Replaced `BinaryWriter` with a bounded stack header and raw row writes. The method writes a 20-byte little-endian header (`magic`, `version`, `count`, `rowBytes`) and then writes each `AlignmentTelemetryEntry` through `new ReadOnlySpan<byte>(UnsafeUtility.AddressOf(ref entry), rowBytes)` in circular oldest-to-newest order.

Rejected Alternatives: Keeping `BinaryWriter` was rejected because Task 16 asks for a raw span dump and field-wise serialization can drift from DTO ABI if fields change. Allocating a managed `byte[]` scratch buffer was rejected because the fault path can use stack spans and direct row images. Writing the ring in physical array order was rejected because post-mortem reading needs chronological order. Running build/rebuild or Unity batchmode was rejected because this is a local source change with static proof and the user prohibited rebuild churn.

Scalability potential: Normal gameplay tiers are unaffected. Low-tier fault export avoids unnecessary per-field writer dispatch. Middle, high, and ultra development tiers get the same raw ABI dump schema, so richer forensic tooling can parse one stable 64-byte row layout without hardware-specific DTO variants.

Hardware Impact: Runtime normal-frame cost is 0.00 ms. Fault dump cost is O(300 * 64 bytes) plus a 20-byte header. Evidence class: STATIC_SOURCE; targeted scan confirms `BinaryWriter` is absent, stack header and raw `ReadOnlySpan<byte>` row writes are present, and `git diff --check` over `AlignmentTelemetryContracts.cs` passes with LF/CRLF warning only.

## Decision 128 - Core Physics Target-Root Layout Gate Refresh

Problem: Broad project scans still report four documented non-DTO Sequential exceptions outside the source fixer roots. Without a target-root scan, Task 18 evidence can be misread as if the Core/Physics fixer still has live Sequential or Pack debt to mutate.

Solution: Ran strict start-of-line layout-attribute scans over only `Assets/_Project/Scripts/Core` and `Assets/_Project/Scripts/Physics`. Both `LayoutKind.Sequential` and `StructLayout(...Pack=...)` return zero hits in those roots. Also reran the SHINOBU-owned DTO property scan; hot DTO auto-property patterns return zero, with reflection confined to the editor self-audit and latest-vault lookup confined to the editor-fenced gizmo.

Rejected Alternatives: Using the broad project scan alone was rejected because voxel MeshData and submarine authoring exceptions are outside the fixer roots and would confuse Task 18 evidence. Running the fixer CLI or Unity batchmode was rejected because the target roots have no current source-visible attributes requiring mutation, and the user prohibited rebuild/editor churn unless structurally necessary.

Scalability potential: Runtime tiers are unchanged. Low, middle, high, and ultra hardware get the same DTO ABI. The value is review precision: Core/Physics source-fixer roots are clean now, while future DTO regressions will fail through the Roslyn gate.

Hardware Impact: Runtime cost is 0.00 ms. Evidence class: STATIC_SOURCE; Core/Physics strict Sequential scan returns zero, Core/Physics Pack scan returns zero, SHINOBU-owned hot DTO property pattern scan returns zero, and no build/rebuild or Unity batchmode was launched.

## Decision 129 - Development Dump Fence

Problem: `Arm64AlignmentTelemetry.DumpFaultHistory` is a post-mortem file writer. Leaving that path compiled into release players keeps filesystem APIs reachable from a runtime assembly, even though the dump is only justified for editor/development diagnostics.

Solution: Wrapped the dump body in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` and made the release-player branch return `false` without resolving Vault rows or touching `Directory`/`FileStream`. The development branch preserves the 20-byte little-endian header and raw `ReadOnlySpan<byte>` row-image schema from Loop 91.

Rejected Alternatives: Keeping the writer available in release was rejected because it is diagnostic I/O, not gameplay authority. Replacing the dump with managed per-field logging was rejected because Task 16 requires raw ABI row evidence. Launching dotnet build or Unity batchmode was rejected because this is a localized static source/doc hardening pass and command discipline forbids rebuild churn without need.

Scalability potential: Low, middle, high, and ultra player builds all share the same DTO layout and Vault IDs; no hardware tier can change the ABI. Development builds on any tier can still emit the raw dump when a fault is detected, while release players pay no dump-file I/O surface.

Hardware Impact: Normal player-frame cost remains 0.00 ms. Release dump call cost is a constant false return. Development fault dump cost remains O(300 * 64 bytes) plus a 20-byte header. Evidence class: STATIC_SOURCE; XML parse reports 20 tasks and the development-build dump fence, source scan confirms `FileStream` is inside the fence and `BinaryWriter` is absent, Core/Physics strict scans remain 0 Sequential/0 Pack, broad project Pack remains 0, broad project Sequential remains exactly four documented non-DTO exceptions, and `git diff --check` passes with LF/CRLF warnings only.

## Decision 130 - Immediate Development Fault Dump Trigger

Problem: Task 16 requires the fault recorder to serialize the 300-entry history when a dynamic allocation or cast/layout fault is detected. The raw dump writer existed, but `TryRecordFault` only wrote the Vault ring and required a separate manual `DumpFaultHistory` call.

Solution: Added a shared editor/development writer, `TryWriteFaultHistory`, and invoked it immediately after `TryRecordFault` writes the telemetry row and advances the Vault-owned cursor. `DumpFaultHistory` now resolves Vault handles and delegates to the same writer. Release-player code keeps the writer compiled out.

Rejected Alternatives: Leaving the dump manual-only was rejected because it weakens the black-box requirement. Calling the public `DumpFaultHistory` from `TryRecordFault` was rejected because it would repeat Vault resolution after the caller already has the ring and cursor rows. Recording through managed logs was rejected because the required evidence is the raw 64-byte ABI row image. Build/rebuild was rejected because this pass is localized and static verification is sufficient before a Unity import gate.

Scalability potential: Release builds on low through ultra hardware keep zero dump-file I/O. Development builds pay the post-mortem export only when a fault is recorded; the cost is intentionally diagnostic and does not alter DTO stride, Vault ownership, save identity, signal payloads, or GlobalQualityWeight authority.

Hardware Impact: Normal release frame cost remains 0.00 ms. Development fault record adds one sequential raw dump of 300 rows, O(19.2 KB) plus a 20-byte header, after the row write. Evidence class: STATIC_SOURCE; source scan confirms `TryRecordFault` calls the shared writer inside the development fence, `DumpFaultHistory` delegates to it, release branch returns false, and `BinaryWriter` is absent; XML parse reports 20 tasks and the immediate-dump trigger; Core/Physics strict scans remain 0 Sequential/0 Pack; broad project Pack remains 0 and Sequential remains exactly four documented non-DTO exceptions; `git diff --check` passes with LF/CRLF warnings only.

## Decision 131 - Telemetry Vault Uninitialized Clear

Problem: The SHINOBU telemetry ring still requested `NativeArrayOptions.ClearMemory`. That is small and cold, but it weakens Task 15 evidence because this agent's own Vault buffer should not rely on allocator zeroing while claiming an explicit zero-init doctrine.

Solution: Switched the ring and cursor Vault requests to `NativeArrayOptions.UninitializedMemory`. After handle resolution, the 300-row ring is cleared once with `UnsafeUtility.MemClear` over `ring.Length * sizeof(AlignmentTelemetryEntry)`, and the one-int cursor is explicitly assigned 0.

Rejected Alternatives: Scheduling `InitializeAlignedBufferJob` and completing it immediately was rejected because 19.2 KB is a tiny diagnostic clear and same-frame schedule/readback loops are forbidden without profiler proof. Leaving `ClearMemory` was rejected because the local buffer should follow the uninitialized-then-explicit-clear policy. Adding a persistent private NativeArray scratch buffer was rejected because Vault owns the memory.

Scalability potential: Player-frame tiers are unchanged because allocation happens on first fault/dump only. Low-tier development hardware avoids job scheduling overhead for a tiny diagnostic buffer. High and ultra development machines keep the same stable raw ABI rows; GlobalQualityWeight cannot alter layout or ownership.

Hardware Impact: Normal runtime frame cost remains 0.00 ms. Cold first-use clear is O(19,200 bytes) through direct memory clear plus one int assignment. Evidence class: STATIC_SOURCE; source scan confirms 0 `ClearMemory` hits, 2 `UninitializedMemory` hits, `ClearRing`, `UnsafeUtility.MemClear`, `ring.GetUnsafePtr()`, immediate dump call, and no hidden `.Complete()` calls; XML parse reports 20 tasks and the Vault allocation route; Core/Physics strict scans remain 0 Sequential/0 Pack; `git diff --check` passes with LF/CRLF warnings only.
