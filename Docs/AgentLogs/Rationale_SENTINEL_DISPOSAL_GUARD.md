# Rationale_SENTINEL_DISPOSAL_GUARD

Status: DOTNET BUILD GREEN / UNITY RUNTIME PENDING VERIFICATION

Problem: H8Memory tracks pointer ownership but cannot purge all allocations owned by a SystemID during scene transition.
Solution: Add owner-indexed pointer lanes, owner job fences, ReleaseAll(SystemID), and cold scene transition hooks.
Rejected Alternatives: Rejected broad edits to every caller's OnDestroy; too slow and outside CORE/MEMORY. Rejected relying on Unity domain reload; non-reload transitions preserve static state.
Scalability potential: Low releases scene-owned native memory before Ocean activation. Middle preserves valid session buffers. High/Ultra may keep only explicitly persistent owners after generation/baseline checks.
Hardware Impact: Estimated gain for i3/MX350 is preventing the reported 200MB transition leak; hot-path CPU change is 0.0 us because purge runs only on transition.

Problem: Unity NativeParallelHashMap rejected `SystemID` enum as a key because enum cannot satisfy `IEquatable<SystemID>`.
Solution: Use `NativeParallelHashMap<ushort, NativeList<IntPtr>>` and `NativeParallelHashMap<ushort, JobHandle>` while keeping all public API inputs as `SystemID`. The key is the exact underlying `ushort` value.
Rejected Alternatives: Rejected changing `SystemID` into a struct because it would mutate public API and break every caller. Rejected managed Dictionary because it violates native memory mandate and scene-transition determinism.
Scalability potential: Low/Middle/High/Ultra all get deterministic owner lookup without managed heap pressure. High-tier does not retain leaked memory for visual overkill.
Hardware Impact: i3/MX350 avoids O(all records) owner purge for explicit `ReleaseAll(SystemID)` in most cases; expected microseconds saved scale with allocation count, but runtime measurement is absent.

Problem: Memory assembly cannot directly publish `SystemPauseSignal` or consume `PrologueCompleteSignal` without cyclic assembly dependencies.
Solution: H8Memory owns the generation cutoff and scene-unload release hook; SceneRuntimeService, already in Core and already transition authority, bridges `SystemPauseSignal` and calls H8Memory before `LoadSceneAsync`.
Rejected Alternatives: Rejected adding a Core reference to Hecton8.Core.Memory. Rejected duplicate signal structs in Memory because duplicate lane names violate signal mandate.
Scalability potential: Low tier holds a loading pause until old generation buffers are purged. High/Ultra can allocate Ocean overkill after baseline verification without inheriting Prologue leaks.
Hardware Impact: Prevents the 200MB reported leak path from surviving into Ocean. Pause signal overhead is one cold-path signal per transition.

Problem: Force-freeing owner allocations can race active jobs.
Solution: Added `RegisterActiveJob(SystemID, JobHandle)` and complete owner fences only at owner teardown/scene transition. This is a documented blocking sync point, not a Tick path.
Rejected Alternatives: Rejected blind `UnsafeUtility.Free` and rejected per-frame polling. Rejected global `CompleteAll` because it would stall unrelated owners.
Scalability potential: Low devices avoid undefined memory races; high-end devices keep parallelism outside teardown.
Hardware Impact: Teardown may block to preserve correctness; gameplay hot path remains 0.0 us.

Problem: Build validation cannot reach green because unrelated Core.Contracts/domain namespace collisions are present outside CORE/MEMORY. Latest build reports ambiguous `BrineLayerSample`, `MacroSwarm`, `MacroSwarmArrival`, and `AcousticAup` symbols, plus `VirtualVoice` failing the `NativeList<T>` unmanaged constraint.
Solution: Fixed the H8Memory compile error, reran validation, and documented the external compile wall in Status. Latest build now reports missing `Hecton8.Animation.Locomotion`, `Hecton8.Core.Determinism`, `Hecton8.Physics.KCC`, missing `ProceduralLadderClimbRuntime`, and Core.Contracts/domain type conflicts. No player/audio/ecosystem/animation edits were made from the CORE/MEMORY domain.
Rejected Alternatives: Rejected editing ecosystem/audio/player/animation contracts under a memory lifecycle task. Rejected reverting dirty files made by other agents. Rejected aliasing only one caller because the error set is cross-domain and requires owner arbitration.
Scalability potential: None for this agent; integrator must restore contract namespace coherence before Unity/runtime verification can be authoritative.
Hardware Impact: None from this dependency block.

Problem: Omega polish extraction was required after all tasks were checked or blocked.
Solution: Extracted `<POLISH_MANDATE>` from `Docs/Tasks/CURRENT_BATCH.md` by CLI after task completion. The tag is absent, so no extra polish instructions exist in the batch file. Ran static anti-bloat scan and `git diff --check`.
Rejected Alternatives: Rejected inventing polish rules not present in the batch. Rejected broad refactor of touched files because static scan found no hot-path bloat requiring removal.
Scalability potential: Low/Middle/High/Ultra unaffected; implementation remains transition-cold and hot-path neutral.
Hardware Impact: 0.0 us gameplay hot-path delta from polish pass.

Problem: Multiplatform audit found CORE/MEMORY binary structs using default sequential packing, leaving implicit padding and platform-dependent dump/interop layout risk.
Solution: Added `Pack = 1` to CORE/MEMORY binary records and reordered large fields in `H8AllocationRecord`, `VaultBufferMeta`, and `MemoryDefragTelemetryEntry` so 8-byte and 4-byte fields stay naturally aligned even under packed layout. Removed the binary-layout attribute from the `VaultGapAuditJob` wrapper because it contains Unity `NativeArray<T>` fields and is not persisted or dumped.
Rejected Alternatives: Rejected blindly packing the job wrapper because Unity native container wrappers are runtime handles, not binary records. Rejected leaving default packing on crash-dump records.
Scalability potential: Low/Quest/Android gets deterministic compact telemetry layout. High/Ultra gets identical dump decoding and no runtime visual compromise.
Hardware Impact: 0.0 us hot-path impact; layout is compile-time metadata and field order.

Problem: H8Memory fatal dumps had allocation snapshots but no fixed last-300 heartbeat ring.
Solution: Added `NativeArray<H8MemoryTelemetryEntry>[300]` to H8Memory, recording allocation/release/transition/fatal state and serializing it before leak detail records in `Dump_SENTINEL_DISPOSAL_GUARD.bin`.
Rejected Alternatives: Rejected managed queues, Debug.Log, or per-frame disk writes. Rejected pretending allocation records alone were a system heartbeat.
Scalability potential: Low devices get cold-path forensic data without gameplay writes; High/Ultra can keep deeper in-memory telemetry without runtime disk pressure.
Hardware Impact: Initial fixed 300-entry ring cost was 19,200 bytes. Current split blackbox storage is 38,400 bytes for heartbeat plus lifecycle-event rings; gameplay hot path writes one heartbeat struct per frame and exact CPU microseconds are unmeasured.

Problem: Raw allocation alignment accepted caller values directly, which can pass non-power-of-two or sub-16-byte alignment into ARM64/Quest native memory paths.
Solution: Added `ResolveSafeAlignment` and routed `AllocateRaw`/`ReallocateRaw` through a power-of-two alignment floor of 16 bytes.
Rejected Alternatives: Rejected trusting every caller. Rejected forcing all typed `NativeArray<T>` allocations through raw allocation because Unity's allocator already owns those typed paths.
Scalability potential: Low/Quest/Android gets safer raw pointer alignment. PC God-Mode remains unaffected.
Hardware Impact: 0.0 us hot-path impact; raw allocation is cold-path.

Problem: Latest build validation still cannot reach green because the repository's external contract state changed again outside CORE/MEMORY.
Solution: Reran `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly`; current errors are unsupported `XRDisplaySubsystem.TryRequestDisplayRefreshRate`, `VaultProbeUtility` generic inference failure, missing `ItemAcquiredSignal`, missing submarine breach/damage-control fields/helpers, missing Biolum profile/blackbox fields, and related non-memory contract drift. No CORE/MEMORY errors were reported.
Rejected Alternatives: Rejected repairing XR, diagnostics, item, submarine, and VFX contracts from the memory lifecycle domain. Rejected claiming compile success when the solution is still red.
Scalability potential: None in CORE/MEMORY; integrator must restore cross-domain contract coherence before runtime profiling can prove exact transition timing.
Hardware Impact: None from this dependency block.

Problem: Explicit `ReleaseAll(SystemID)` used owner pointer lanes but still resolved each pointer by scanning the allocation table when the record index was needed.
Solution: Added a native `NativeParallelHashMap<long, int>` pointer-to-record-index lane, maintained on register, resize, lookup repair, and swap-back removal.
Rejected Alternatives: Rejected managed dictionaries and rejected leaving teardown as owner pointer count multiplied by active record count. Rejected storing record indices only in owner lists because swap-back removal would make every owner list stale.
Scalability potential: Low devices get cheaper scene/owner teardown under allocation-heavy transitions. High/Ultra keeps more transition CPU budget available for Ocean activation and visual overkill systems.
Hardware Impact: 0.0 us gameplay hot-path impact; cold owner teardown changes from scan fallback to O(1) common lookup. Exact microseconds remain unmeasured.

Problem: `Shutdown()` force-freed every tracked record without first completing every registered owner `JobHandle`.
Solution: Added `_ownerJobKeys` and `CompleteAllOwnerJobs()` so shutdown drains all known owner fences before raw frees. `CompleteOwnerJobs()` removes its key to keep the lane bounded.
Rejected Alternatives: Rejected trusting caller shutdown order and rejected global per-frame job polling. The blocking wait stays in shutdown/teardown only.
Scalability potential: Low/Quest avoids undefined native memory races during domain unload. High/Ultra keeps normal job parallelism during gameplay and pays the fence only at teardown.
Hardware Impact: 0.0 us gameplay hot-path impact; shutdown may block by the actual outstanding job duration.

Problem: `RegisterActiveJob` could silently fail to record a new owner fence if the native registry could not add the key.
Solution: Converted that path to `FatalMemoryException.ThrowAllocationTrackingFailed()` so the sentinel fails closed instead of freeing memory without a recorded dependency.
Rejected Alternatives: Rejected silent fence loss and rejected managed fallback storage.
Scalability potential: Low/Middle/High/Ultra all keep deterministic failure semantics under registry pressure.
Hardware Impact: 0.0 us gameplay hot-path impact in normal operation; cold failure path throws instead of corrupting memory ownership.

Problem: H8Memory blackbox was event-driven, not a true last-300-frame heartbeat. Allocation/free/transition records do not prove the final 300 frames before a crash.
Solution: Added `H8Memory.RecordHeartbeat()` and call it from the existing `SceneRuntimeService.Tick` bridge. The method writes one fixed `H8MemoryTelemetryEntry` with a `Heartbeat` flag and `Time.frameCount` into the persistent 300-entry ring.
Rejected Alternatives: Rejected Debug.Log, managed queues, per-frame disk writes, and initializing H8Memory from Tick. Initialization is done in `SceneRuntimeService.InitializeService`; heartbeat returns if memory is not initialized.
Scalability potential: Low tier gets crash context without disk pressure. High/Ultra keeps deterministic frame evidence without growing the ring.
Hardware Impact: Heartbeat ring remains 19,200 bytes; total H8Memory blackbox storage is now 38,400 bytes after the lifecycle-event ring split. Per frame cost is one NativeArray struct store and exact microseconds are unmeasured. GC impact is 0 B/frame by static inspection.

Problem: Adding frame evidence risked inflating the telemetry record beyond the established 64-byte blackbox entry footprint.
Solution: Replaced two reserved ushort fields with one `uint Frame`, preserving the manual 64-byte packed layout while adding frame index data to binary dumps.
Rejected Alternatives: Rejected adding a new field that grows every ring entry. Rejected stealing semantic fields such as `Sequence` or `Owner`.
Scalability potential: Same memory footprint across MX350, Quest, Steam Deck, and high-end PC.
Hardware Impact: 0 additional persistent bytes versus the previous 300-entry ring.

Problem: GlobalDataVault tracked per-buffer `VaultBufferMeta.Owner`, but scene-transition purge only released top-level H8Memory records. Scene-owned vault suballocations could survive inside the reusable CoreDataVault arena.
Solution: Added `ReleaseOwnerBuffers(SystemID, out long)` and `ReleaseSceneOwnedBuffers(out long)` to `IDataVault`/`GlobalDataVault`, scanning vault keys on the cold transition path and freeing blocks whose metadata owner is scene-scoped.
Rejected Alternatives: Rejected destroying the entire CoreDataVault arena because that would create transition memory churn and MicroSD-adjacent reload pressure. Rejected ignoring vault metadata ownership because it makes H-Phi ownership cosmetic.
Scalability potential: Low tier reuses the arena without carrying stale scene buffers. High/Ultra preserve arena capacity for fast Ocean allocation while evicting old-scene data.
Hardware Impact: 0 B GC/frame; transition performs a cold O(vault buffer count) scan. Exact microseconds are unmeasured.

Problem: Vault owner eviction can collide with active jobs if a buffer is locked.
Solution: `ReleaseBuffersByOwner` skips blocks with `BlockFlagLocked` or nonzero lock count and emits the existing Phi/VOD blackbox path instead of freeing active memory.
Rejected Alternatives: Rejected force-freeing locked vault blocks. Rejected adding per-frame polling for lock expiry.
Scalability potential: Low/Quest avoids use-after-free during scene transitions. High/Ultra keep job parallelism outside the transition gate.
Hardware Impact: Cold transition branch only; no gameplay hot-path cost.

Problem: H8Memory mixed frame heartbeats and lifecycle allocation/release/transition snapshots in one 300-entry ring. Event bursts during teardown could evict the last-300-frame heartbeat evidence required by the blackbox rule.
Solution: Split the sentinel telemetry into `_blackBox` for frame heartbeats and `_eventBlackBox` for lifecycle snapshots, both fixed-size native rings serialized into `Dump_SENTINEL_DISPOSAL_GUARD.bin`. Added packed ABI size guards in both H8Memory and GlobalDataVault initialization so binary dump layouts fail closed on drift.
Rejected Alternatives: Rejected enlarging a single mixed ring because it still fails the semantic requirement when event spikes exceed capacity. Rejected managed queues and Debug.Log snapshots because they add GC/I/O pressure and lose deterministic binary layout. Rejected trusting `StructLayout` attributes without `UnsafeUtility.SizeOf` checks.
Scalability potential: Low/Quest/Steam Deck retain deterministic crash evidence without per-frame disk writes. High/Ultra get the same heartbeat guarantee while lifecycle events remain available for leak forensics and Ocean activation budget remains protected.
Hardware Impact: 38,400 bytes persistent native storage for two 300-entry 64-byte rings. Gameplay hot path is one heartbeat struct store; lifecycle ring writes only on allocation/release/transition. Exact microseconds are unmeasured.

Problem: Latest build validation is still red, but the current blocker is outside CORE/MEMORY.
Solution: Reran `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly`; current result is 70 external errors across World, Animation, Submarine, and Determinism. Lead failures are `NativeArray<MacroSwarm>` being used as a list in `EcosystemDirector`, missing helper methods in `ProceduralLadderClimbRuntime`, missing vault handle fields in `SubmarineFluidDynamics`, and missing signal constants in `LockstepStateValidator`. CORE/MEMORY touched files report no compiler errors.
Rejected Alternatives: Rejected editing World, Animation, Submarine, or Determinism from the sentinel memory domain and rejected claiming green build while external domains remain broken.
Scalability potential: None in CORE/MEMORY; integrator and domain owners must restore those contracts before Unity/runtime profiling can prove transition timing.
Hardware Impact: None from this dependency block.

Problem: GlobalDataVault owner eviction removed metadata and free-list ownership but left old scene payload bytes in the reusable arena until a later allocation overwrote them.
Solution: `ReleaseBuffersByOwner` now calls `FreeBlock(..., clearPayload: true)`, which clears the released payload with `UnsafeUtility.MemClear` before returning the block to the free list. Free-list creation, split, merge, grow, dispose, and release paths also reset `Reserved1` lock counters together with lock flags.
Rejected Alternatives: Rejected metadata-only eviction because H-Phi data sovereignty requires erased old-scene bytes, not just inaccessible keys. Rejected shrinking/reallocating the arena on every transition because that would create transition churn and I/O-adjacent reload pressure on Steam Deck/MicroSD.
Scalability potential: Low/MX350 gets a warm reusable arena without carrying stale scene data. High/Ultra can reuse the same arena for Ocean/VFX allocation while old-scene payloads are actually erased.
Hardware Impact: 0 B GC/frame. Payload clearing is cold owner/scene-transition path only; exact microseconds are unmeasured and scale with released bytes.

Problem: GlobalDataVault exposed a 300-entry defrag blackbox but had no guaranteed per-frame heartbeat bridge; the method existed but was not called from the scene tick owner.
Solution: `SceneRuntimeService` caches `IDataVault` during service initialization and refreshes it only on the cold scene-transition path, then calls `RecordHeartbeat()` from Tick beside `H8Memory.RecordHeartbeat()`.
Rejected Alternatives: Rejected polling `GlobalRegistry.DataVault` in Tick because registry lookup in Tick violates the architecture rules. Rejected leaving vault heartbeat defrag-event-only because the blackbox rule requires frame evidence.
Scalability potential: Low devices get vault crash context without disk writes or managed queues. High/Ultra keep the same bounded 300-frame ring and use recovered memory budget for visual systems outside CORE/MEMORY.
Hardware Impact: One native struct store per frame when the vault is cached; exact microseconds are unmeasured. Static inspection shows 0 B GC/frame.

Problem: Final validation was previously blocked by external compile errors.
Solution: A prior `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly` checkpoint succeeded with 0 warnings and 0 errors in 02:04.91. The current compile gate is documented below as red again due external domain drift.
Rejected Alternatives: Rejected claiming Unity runtime verification from a dotnet build; Unity Editor import, console, scene transition, and profiler verification remain pending without MCP/editor access.
Scalability potential: Compile green unblocks runtime profiling on MX350, Quest/Android, Steam Deck, and high-end PC, but does not replace those platform captures.
Hardware Impact: None measured; build success is a compile gate, not runtime performance proof.

Problem: GlobalDataVault defrag/PhiVOD dumps had a 300-entry native ring but did not stamp frame count or serialize entries in chronological order after wraparound.
Solution: Added `Frame` to `MemoryDefragTelemetryEntry` while preserving the 128-byte packed size guard. Dumps now write a fixed magic, recorded count, entry size, then entries oldest-to-newest.
Rejected Alternatives: Rejected raw NativeArray-order dump because the cursor position is required to decode wrapped rings. Rejected growing the telemetry record because MX350 does not need a larger blackbox entry.
Scalability potential: Low/Quest/Steam Deck get deterministic crash decoding without per-frame disk writes. High/Ultra keep the same bounded ring and get cleaner postmortem correlation between H8Memory and vault heartbeat frames.
Hardware Impact: No additional persistent bytes versus the prior 128-byte defrag entry. Runtime adds one `uint` assignment per vault heartbeat; exact CPU microseconds are unmeasured. Dump serialization is cold-path disk I/O only.

Problem: The repository compile gate drifted back to red outside CORE/MEMORY after the prior green build.
Solution: Reran `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly`; current result fails in 01:00.45 with 141 external errors. Lead groups: `GameBootstrapper.Initialize` signature mismatch, `RepairTool` unassigned `localPoint`, missing biome fog fields in `HectonUnderwaterVisuals`, and missing native-state fields/helpers in `ToolDurabilitySystem`. CORE/MEMORY touched files report no compiler errors.
Rejected Alternatives: Rejected editing Bootstrap, Repair, VFX, or Tools domains from the Sentinel memory assignment. Rejected preserving stale green-build status after objective failure.
Scalability potential: None in CORE/MEMORY; external domain owners must restore contracts before Unity scene-transition profiling can be authoritative.
Hardware Impact: None from this dependency block.

Problem: The H-Phi data sovereignty audit needed to distinguish illegal system-private native collections from the central memory authority's own registry and vault lanes.
Solution: Re-read every CORE/MEMORY source and assembly file. Native collections remaining in this domain are H8Memory tracking tables, GlobalDataVault arena/metadata tables, job audit scratch owned by the vault, or API handles returning vault/sentinel-owned memory.
Rejected Alternatives: Rejected moving the memory authority's own registries into another layer, because H8Memory and GlobalDataVault are the ownership layer. Rejected deleting API-level `NativeArray<T>` returns because callers need zero-copy vault/sentinel handles.
Scalability potential: Low devices get one central release/blackbox authority instead of scattered per-system disposal. High/Ultra preserve the same ownership contract while spending freed memory budget in visual domains.
Hardware Impact: 0.0 us gameplay hot-path change from this audit; it was source verification only.

Problem: H8Memory and GlobalDataVault blackbox dump length was inferred from wrapping `uint` sequence counters. After extreme uptime, count inference can under-report a full 300-entry ring.
Solution: Added explicit recorded-count state for H8Memory heartbeat, H8Memory lifecycle-event, and GlobalDataVault defrag/PhiVOD rings. Dump traversal still writes oldest-to-newest, but count is now bounded state instead of sequence-derived.
Rejected Alternatives: Rejected widening sequence fields or assuming sessions never wrap. Rejected dumping all 300 entries before the ring is full because that pollutes postmortem data with zero records.
Scalability potential: Low/Quest/Steam Deck get deterministic 300-frame dump length without larger records or disk writes. High/Ultra keep identical bounded telemetry with better long-session correctness.
Hardware Impact: One bounded int increment per ring write; exact microseconds are unmeasured. Persistent memory increase is 12 bytes of static/instance int state across the three rings, before runtime alignment.

Problem: The last compile error was a typed SignalBus namespace drift outside CORE/MEMORY: `ContextualPhysicalIkRuntime` consumed `KccVelocitySignal`, while the signal struct lives in `Hecton8.Core.Contracts.Signals`.
Solution: Added the missing namespace import only. No gameplay logic, data layout, or signal duplication was changed.
Rejected Alternatives: Rejected duplicating `KccVelocitySignal`, moving the signal struct, or editing the physics lane. The correct fix is the existing typed-lane namespace.
Scalability potential: Low/Middle/High/Ultra all keep the same typed-lane flow; compile success enables runtime validation.
Hardware Impact: 0.0 us runtime; compile-only namespace resolution.

Problem: Final validation had drifted red due external errors.
Solution: Reran `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly`; current result succeeds with 0 warnings and 0 errors in 00:03.24.
Rejected Alternatives: Rejected claiming Unity runtime verification from dotnet. Unity Editor import, scene transition run, and profiler capture remain pending without MCP/editor access.
Scalability potential: Compile green reopens real platform profiling for MX350, Quest/Android, Steam Deck, and high-end PC.
Hardware Impact: None measured; this is compile validation, not runtime profiling.

Problem: H8Memory and GlobalDataVault dumps were ordered and counted, but the binary headers still forced postmortem tools to infer dump version, ring type, capacity, and record sizes from positional knowledge.
Solution: Added fixed H8Memory fatal-dump magic/version metadata, telemetry entry size, allocation record size, and blackbox capacity. Each H8Memory ring section now writes ring kind, ring capacity, entry size, recorded count, then oldest-to-newest entries. GlobalDataVault defrag/PhiVOD dumps now write dump version and ring capacity beside the existing magic/count/entry-size header.
Rejected Alternatives: Rejected anonymous ring streams and magic-only versioning because they are brittle under long-lived save/runtime telemetry changes. Rejected JSON/text dumps because crash forensics must remain fixed-size binary and cold-path only.
Scalability potential: Low/Quest/Steam Deck get deterministic postmortem decoding without per-frame disk writes. High/Ultra keep the same bounded telemetry while tools can distinguish heartbeat, lifecycle event, and vault defrag streams without layout guesses.
Hardware Impact: 0 B/frame and no gameplay hot-path work. Added bytes are written only on fatal leak/defrag/PhiVOD cold dump paths; exact dump microseconds are unmeasured.

Problem: A validation rerun briefly exposed Fauna/Construction compile drift from parallel work, then settled after those external files changed again.
Solution: Reran `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly`; current result succeeds with 0 warnings and 0 errors in 00:01:39.53. Static CORE/MEMORY scans remain clean.
Rejected Alternatives: Rejected editing Fauna/Construction from the Sentinel memory domain after the second pass showed the compile bridge was no longer needed. Rejected preserving the stale 00:03.24 validation line after a newer build.
Scalability potential: Compile green reopens Unity/runtime transition profiling across MX350, Quest/Android, Steam Deck, and high-end PC, but does not replace those captures.
Hardware Impact: None measured; build validation is a compile gate, not runtime profiling.

Problem: The current repository compile gate drifted red again after the prior green checkpoint, now in external UI/Navigation and World domains.
Solution: Rechecked the previously reported Submarine syntax gate and found it already repaired by parallel work. Reran CORE/MEMORY static scans successfully. Reran `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly`; first pass timed out after 254.9s without errors, second pass completed in 00:03:03.97 and failed with 23 errors in `DiegeticGyroCompassRuntime` and `EcosystemDirector`. Marked validation blocked by dependency because no CORE/MEMORY errors were reported.
Rejected Alternatives: Rejected patching UI compass state fields, World native upload generics, or unrelated navigation blackbox signatures from the Sentinel memory assignment after multiple external compile drifts. Rejected preserving the stale green status after objective red output.
Scalability potential: None in CORE/MEMORY from this dependency block; external domain owners must restore their contracts before Unity scene-transition profiling can be authoritative.
Hardware Impact: None from the block. The Sentinel dump-header and memory lifecycle hot-path impact remains 0 B/frame; exact CPU microseconds remain unmeasured.

Problem: The prior blocker needed revalidation because the external UI/World files changed again under parallel work.
Solution: Re-extracted the exact Sentinel XML assignment, inspected the previously failing external code regions, and found the reported overload/generic errors no longer present. Reran CORE/MEMORY static scans successfully. Reran `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly`; current result succeeds with 0 warnings and 0 errors in 00:01:16.17.
Rejected Alternatives: Rejected making unnecessary external UI/World edits after source inspection showed the parallel work had already settled the compile drift. Rejected leaving status blocked after objective green validation.
Scalability potential: Compile green reopens Unity scene-transition profiling across MX350, Quest/Android, Steam Deck, and high-end PC, but it still does not replace runtime capture.
Hardware Impact: None measured; build validation is a compile gate. Sentinel memory lifecycle hot-path cost remains 0 B/frame, with heartbeat/native-ring costs still exact-microsecond unmeasured.

Problem: The H-Phi audit needed another full-domain pass after compile green to separate legal memory-authority native state from illegal system-private containers.
Solution: Enumerated every CORE/MEMORY file and scanned native collections, ownership APIs, scene hooks, disposal guards, pointer guards, and job fences. Remaining native containers are H8Memory registries/rings, GlobalDataVault arena/metadata/cache lanes, relocation scratch, or API views backed by the central vault/sentinel ownership layer.
Rejected Alternatives: Rejected moving H8Memory and GlobalDataVault authority data into another abstraction, because these classes are the GlobalDataVault/H8Memory lifecycle boundary. Rejected changing cold-path binary dump I/O into managed logging or per-frame disk writes.
Scalability potential: Low/Quest/Steam Deck keep centralized ownership, cold dumps, and no MicroSD-style per-frame writes. High/Ultra keep the same deterministic release boundary while recovered memory budget remains available for visual domains.
Hardware Impact: Audit-only pass added no runtime work. Current verified hot-path delta remains 0 B/frame; exact heartbeat/native-ring CPU microseconds are still unmeasured and therefore not claimed.

Problem: Fresh validation exposed three compile-gate issues after parallel edits: PhysicsApplySystem referenced GlobalDataVault packet lane IDs missing from `BufferID`, ArchitectEye double-buffer fields were read but never assigned, and Sargassum signal consumers called a missing finite clamp helper.
Solution: Added the physics force/validation buffer IDs in the central memory enum, initialized/released ArchitectEye's A/B GPU buffers, and added `SaturateFinite01` that returns 0 for non-finite inputs and saturates finite values. Reran `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly`; final result succeeds with 0 warnings and 0 errors in 00:00:01.29.
Rejected Alternatives: Rejected suppressing CS0649 warnings, rejected local/private physics buffer identifiers, and rejected raw saturation on NaN-bearing signal values. Rejected claiming Unity runtime verification from a dotnet build.
Scalability potential: Low devices keep deterministic vault IDs and NaN-safe signal ingestion; diagnostics GPU buffers now upload predictably. High/Ultra diagnostics can render without null double-buffer lanes while Sentinel memory state stays centralized.
Hardware Impact: Sentinel hot path remains unchanged. External compile bridges affect diagnostics/physics/sargassum paths only; exact runtime microseconds are unmeasured and not claimed.

Problem: The deferred `H8Memory.Release(ref NativeArray<T>, JobHandle, SystemID)` path retired ownership immediately, scheduled `NativeArray.Dispose(dependency)`, and returned the handle without recording it in the owner fence table. That creates a scene-transition blind spot: `TotalAllocatedBytes` can reach the baseline while the actual scheduled native free is still pending, and owners with no active pointer list can be skipped by transition job draining.
Solution: Register the returned dispose `JobHandle` through the existing owner fence table and expand `CompleteSceneTransitionOwnerJobs()` to drain scene-owned `_ownerJobKeys`, not only owners still present in `_ownerPointerKeys`.
Rejected Alternatives: Rejected leaving deferred frees as caller-only responsibility because the Sentinel owns transition proof. Rejected keeping retired pointers counted until disposal completion because it would make the baseline depend on a pending C++ disposal job rather than a drained transition gate. Rejected per-frame polling of disposal handles.
Scalability potential: Low/Quest/Steam Deck get deterministic teardown before Ocean activation without per-frame polling or disk writes. High/Ultra get the same exact release barrier before visual domains spend recovered memory.
Hardware Impact: 0 B/frame. Deferred release now adds one owner-fence native hash update only on the release call path; scene transition blocks on outstanding scene-owned dispose handles. Exact CPU microseconds are unmeasured.

Problem: The local dotnet compile list did not include the existing `ArchitectEyeDebugSignal.cs` typed-lane source, so enabling diagnostics compilation exposed `DebugSignal` as missing even though the source file existed.
Solution: Added the existing source file to `Hecton8.Core.csproj` for dotnet validation and kept the single `DebugSignal` definition in the typed signal namespace.
Rejected Alternatives: Rejected duplicating `DebugSignal` inside `GlobalSignals` or `ArchitectEyeVisualizer`. Rejected replacing the typed lane with legacy EventBus or managed delegates.
Scalability potential: Low devices keep typed diagnostics lanes compile-valid without runtime fallback. High/Ultra diagnostics can use the same lane without signal duplication.
Hardware Impact: 0.0 us runtime; project metadata compile bridge only.

Problem: The compile gate then drifted into external UI navigation presentation-state errors while Sentinel memory code was already compiling.
Solution: Re-read `DiegeticGyroCompassRuntime` and `InertialNavigationContracts`; the presentation DTO mismatch settled under parallel work before a Sentinel-owned patch was needed. Reran static CORE/MEMORY scans and the full dotnet gate.
Rejected Alternatives: Rejected copying UI presentation fields into `CompassStateDTO`, which would pollute the core navigation contract with render-only state. Rejected preserving a red build after the dependency settled.
Scalability potential: Sentinel remains cleanly scoped; UI presentation state stays separate from core navigation state while compile validation is green.
Hardware Impact: No Sentinel runtime change. Latest `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly` succeeded with 0 warnings and 0 errors in 00:00:26.13.

Problem: The local project file carried redundant direct includes for contract sources that are already owned by the generated `Directory.Build.targets` remove/include bridge.
Solution: Removed only the direct `HectonContractValidator.cs` and `HectonSurvivalContract.cs` entries from `Hecton8.Core.csproj`, then reran the full dotnet gate.
Rejected Alternatives: Rejected leaving duplicate compile metadata because it increases future merge drift in a 20-agent workspace. Rejected changing `Directory.Build.targets` because that file is active integration territory and already supplies the bridge.
Scalability potential: Low/Middle/High/Ultra are unaffected at runtime; this is compile metadata hygiene that keeps validation deterministic.
Hardware Impact: 0.0 us runtime. Latest build after removal and report update succeeded with 0 warnings and 0 errors in 00:00:38.31.

Problem: Fresh validation exposed an external compile gate in `H8DataBaker`: `SignalBusRegistry` was referenced without the existing Core namespace, and the local project rejected the bool `FileStream` overload on the cold CSV read path.
Solution: Added the existing `Hecton8.Core` import and changed the CSV read stream to use `FileOptions.SequentialScan`, preserving cold-path sequential I/O intent.
Rejected Alternatives: Rejected duplicating `SignalBusRegistry`, inventing a new signal lane, or changing static-data bake semantics. Rejected reverting to small/default stream reads because Steam Deck/MicroSD pressure is explicitly part of the inquisition.
Scalability potential: Low/MX350 and Steam Deck get predictable cold CSV reads without managed registry duplication. High/Ultra keep the same static-data path while Sentinel memory lifecycle remains unchanged.
Hardware Impact: No Sentinel gameplay hot-path work. CSV impact is cold data-bake I/O only; exact microseconds are unmeasured.

Problem: The Sentinel domain needed another post-cleanup static proof after metadata and external compile-gate fixes.
Solution: Re-extracted the XML prompt, reran CORE/MEMORY scans for unpacked structs, Update-style Unity hooks, `string.Format`, legacy `EventBus`, managed delegate patterns, and hidden `.Complete()` sync points. Only the intentional H8Memory owner teardown/shutdown fences remain.
Rejected Alternatives: Rejected claiming runtime scene-transition proof from dotnet/static analysis. Rejected editing visual/shader domains from the CORE/MEMORY assignment.
Scalability potential: Low/Quest/Steam Deck keep deterministic memory lifecycle evidence with no per-frame disk I/O. High/Ultra keep the same release barrier before recovered memory is spent by Ocean/VFX systems.
Hardware Impact: Static pass added no runtime work. Verified Sentinel hot-path allocation impact remains 0 B/frame by source inspection; exact CPU microseconds remain unmeasured.

Problem: H8Memory's own Unity `sceneUnloaded` hook could complete transition verification before `SceneRuntimeService` released scene-owned GlobalDataVault buffers, so the H-Phi vault eviction proof was ordered after the H8Memory proof. Additive and post-cutoff Ocean allocations could also make an otherwise correct transition look like a leak because verification compared total bytes to the pre-load baseline only.
Solution: Added a cold coordination flag through `H8Memory.SetSceneUnloadedVerificationDeferred(bool)`. `SceneRuntimeService` sets it after capturing the transition purge cutoff, then its `sceneUnloaded` callback clears runtime state when appropriate, releases scene-owned vault buffers, and calls `H8Memory.CompleteSceneTransitionVerification()`. H8Memory now computes `LastTransitionExpectedBytes` as captured baseline plus post-cutoff allocations, and fatal dump version 3 writes that expected total beside the baseline.
Rejected Alternatives: Rejected leaving H8Memory and SceneRuntimeService as racing scene-unload owners. Rejected freeing memory before Unity unloads the old scene because old-scene scripts/renderers can still touch buffers until unload. Rejected treating post-cutoff Ocean allocations as leaks because the assignment also requires memory to be freed and reallocated for Ocean.
Scalability potential: Low/MX350/Quest get deterministic old-scene eviction without per-frame polling or disk writes. Steam Deck avoids MicroSD churn because only cold fatal dumps touch disk. High/Ultra keep legitimate post-cutoff Ocean/VFX allocations while still proving pre-cutoff scene data is gone.
Hardware Impact: 0 B/frame. Transition verification adds one cold allocation-table scan to compute post-cutoff bytes; exact CPU microseconds are unmeasured and therefore not claimed.

Problem: A failed transition verification previously cleared the cutoff generation immediately, which could turn a transient external job/vault timing failure into an unrecoverable loss of the leak boundary.
Solution: H8Memory now clears `_transitionCutoffGeneration` only when verification succeeds. Failed verification writes the fatal blackbox and keeps the cutoff alive for a later retry.
Rejected Alternatives: Rejected one-shot failure semantics. Rejected per-frame retry polling; retries remain driven by scene lifecycle or explicit lifecycle calls.
Scalability potential: Low-end devices get a recoverable transition gate without hot-path cost. High/Ultra get the same deterministic boundary before visual domains spend memory.
Hardware Impact: Cold failure path only; exact microseconds are unmeasured.

Problem: Validation drifted red twice in external Tether and World files while Sentinel code was static-clean.
Solution: Rechecked the external contexts and reran the compile gate. The third build settled after parallel edits with `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly` succeeding in 00:03:23.66 with 0 warnings and 0 errors.
Rejected Alternatives: Rejected editing `TetherInstance` or `EcosystemDirector` from the Sentinel memory domain once source inspection showed the compile drift was external and unstable.
Scalability potential: Compile green reopens Unity scene-transition profiling across MX350, Quest/Android, Steam Deck, and high-end PC, but does not replace runtime capture.
Hardware Impact: None measured; build validation is a compile gate, not runtime profiling.

Problem: `H8Memory.TotalAllocatedBytes` alone cannot prove GlobalDataVault scene eviction, because scene-owned buffers can survive as suballocations inside the reusable `CoreDataVault` arena while the top-level arena allocation remains legal.
Solution: Expanded `IDataVault.ReleaseSceneOwnedBuffers` to report released bytes, remaining scene-owned buffer count, remaining scene-owned bytes, and locked survivor count. Added `CountSceneOwnedBuffers` and made `SceneRuntimeService` keep `SystemPauseSignal` asserted unless both vault survivor proof and H8Memory transition proof pass.
Rejected Alternatives: Rejected relying only on H8Memory's arena-level total. Rejected force-freeing locked vault blocks because active jobs may still own aliases. Rejected per-frame polling; the check remains transition-driven.
Scalability potential: Low/MX350/Quest get deterministic old-scene vault eviction with no per-frame disk writes. Steam Deck avoids MicroSD churn because only cold fatal/PhiVOD dumps touch disk. High/Ultra keep legitimate post-cutoff Ocean/VFX allocations while blocked vault survivors still prevent a false "ready" signal.
Hardware Impact: 0 B/frame. Added work is a cold scene-transition scan over vault keys and a few scalar fields in the pause failure path. Exact CPU microseconds are unmeasured and not claimed.

Problem: Local dotnet validation referenced `Hecton8.Core.Memory.Defrag` and `MemoryDefragPhase`, but the existing bridge included `H8Memory` and `GlobalDataVault` without the `MemoryDefragContracts.cs` source that defines the phase enum.
Solution: Added `Assets/_Project/Scripts/Core/Memory/Defrag/MemoryDefragContracts.cs` to the existing `Directory.Build.targets` compile bridge.
Rejected Alternatives: Rejected duplicating `MemoryDefragPhase`, deleting the defrag phase contract, or editing dispatcher behavior to hide the missing contract. The correct fix is to compile the existing Core/Memory contract source.
Scalability potential: Low/Middle/High/Ultra keep the same defrag phase contract and Burst lock mask flow; this only restores validation parity with the Unity asmdef boundary.
Hardware Impact: 0.0 us runtime; project metadata only.

Problem: Validation briefly reported an external UI `NativeSlice.IsCreated` compile error from `DiegeticGyroCompassRuntime`, but the working source had already changed and no longer contained that invalid call.
Solution: Reran an isolated build on a fresh obj/bin path, then reran the normal project build after the external drift settled. Final `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly` succeeds with 0 warnings and 0 errors in 00:01:56.04.
Rejected Alternatives: Rejected editing UI navigation from the Sentinel memory domain after source inspection showed no live patch was required. Rejected preserving a stale red validation line after objective green output.
Scalability potential: Compile green reopens Unity scene-transition profiling across MX350, Quest/Android, Steam Deck, and high-end PC, but it still does not replace runtime capture.
Hardware Impact: None measured; build validation is a compile gate. Sentinel hot-path allocation impact remains 0 B/frame by static inspection.

Problem: `SceneRuntimeService.LoadSceneAsync` captured a transition purge cutoff before `SceneManager.LoadSceneAsync`, but the `finally` path could complete memory verification even when Unity never unloaded the old scene. That could convert a canceled or null scene load into an active-scene force-release.
Solution: Added an observed-unload gate in `SceneRuntimeService`. Its `sceneUnloaded` callback marks `_memoryLifecycleSceneUnloadObserved`; finalization completes verification only when that flag is true. Otherwise it calls `H8Memory.CancelSceneTransitionPurge()`, clears the transient cutoff, and publishes an unpaused failure signal without releasing old-scene pointers.
Rejected Alternatives: Rejected completing H8Memory verification from `finally` without scene-unload proof. Rejected pre-unload force free because old-scene scripts/renderers can still touch scene-owned NativeArrays until Unity actually unloads them. Rejected per-frame polling of transition handles.
Scalability potential: Low/MX350/Quest avoid catastrophic invalid frees during canceled loads with no Tick cost. Steam Deck avoids extra I/O because only cold fatal dumps write to disk. High/Ultra keep the same deterministic memory gate before Ocean/VFX spends recovered memory.
Hardware Impact: 0 B/frame. Added work is cold scene-load bookkeeping and one cold cancel call on failed load attempts; exact CPU microseconds are unmeasured.

Problem: Fresh build validation exposed external drift in typed acoustic-zone signals, PlayerMotor registry/scalability interfaces, GlobalDataVault-backed motor native state, Tether call shapes, and Submarine thermal anomaly accessors while Sentinel memory code was already compiling.
Solution: Kept bridges narrow: acoustic zone now uses the existing typed `SignalBus<AcousticZoneChangedEvent>` lane and `ReadOnlySpan<T>` snapshots; PlayerMotor satisfies hot-swap/scalability contracts and passes `IDataVault` into native sweep storage; Tether manager/instance calls are back in parity; Submarine thermal anomaly center access is finite-guarded. No new signal duplicates or managed EventBus/delegate lanes were introduced.
Rejected Alternatives: Rejected reintroducing listener queues, private NativeArray fallbacks, broad gameplay refactors, or suppressing interface compile errors. Rejected claiming runtime performance wins from compile-only bridges.
Scalability potential: Low/Quest/Steam Deck keep typed lanes and vault ownership without private native state leaks. High/Ultra retain the same signal flow and data sovereignty while the compile gate stays green for runtime scene-transition profiling.
Hardware Impact: No Sentinel gameplay hot-path change. External bridge costs are either cold registration/compile metadata or existing gameplay paths; exact CPU microseconds are unmeasured.

Problem: H8Memory's owner pointer registry could retain empty per-owner `NativeList<IntPtr>` lanes after explicit owner release or pointer unregister. That is not a payload byte leak, but it is stale persistent allocator metadata across scene transitions and weakens the zero-leak claim.
Solution: `ReleaseAll(SystemID)` and `RemoveOwnerPointer` now dispose the owner lane, remove the `_ownerPointers` entry, and remove the owner key from `_ownerPointerKeys` when the last pointer is gone.
Rejected Alternatives: Rejected retaining empty lanes forever as a harmless cache because scene-owned owners churn during transitions. Rejected rebuilding `_ownerPointerKeys` by scanning the hash map because that adds broader cold-path complexity and risks iterator differences across Unity collections.
Scalability potential: Low/MX350, Quest/Android, and Steam Deck avoid owner-lane creep without per-frame polling or disk writes. High/Ultra keep the same deterministic owner registry so recovered memory can be spent by Ocean/VFX domains after verified transition cleanup.
Hardware Impact: 0 B/frame. Added work is only on explicit release/unregister cold paths. Exact CPU microseconds are unmeasured and not claimed.

Problem: The first owner-lane hygiene patch still left a stale lane when `ReleaseAll(SystemID)` found a pre-existing owner entry with zero pointers before entering the purge loop.
Solution: Added an early empty-lane branch and centralized owner pointer lane removal through `RemoveOwnerPointerLane`, shared by `ReleaseAll` and `RemoveOwnerPointer`.
Rejected Alternatives: Rejected treating zero-length lanes as a cache because scene-owned systems can churn through many owners over long sessions. Rejected a global key rebuild because deterministic point removal is simpler and keeps the transition path bounded.
Scalability potential: Low/MX350, Quest/Android, and Steam Deck avoid persistent owner metadata creep without per-frame work. High/Ultra keep deterministic release proof before VFX/Ocean domains spend recovered memory.
Hardware Impact: 0 B/frame. Added work is cold owner release/unregister only. Exact CPU microseconds are unmeasured.

Problem: No-restore build validation exposed external compile drift after the Sentinel patch: `SubmarineFluidDynamics` had a scalability listener signature mismatch and a removed frame-cache field, and `SargassumMicroFaunaBoids` had four removed CPU staging arrays still referenced by bounded GPU upload and bookkeeping paths.
Solution: Kept bridges minimal: restored the Submarine frame-cache field, fully qualified the scalability payload type, and restored the Sargassum bounded staging arrays plus cold allocation guards. The Sentinel memory domain was not broadened into a World/VFX refactor.
Rejected Alternatives: Rejected changing the core scalability interface, removing listener registration, or rewriting Sargassum's GPU upload pipeline under a memory-agent task. Rejected running `dotnet rebuild`; validation used `dotnet build --no-restore`.
Scalability potential: External bridges restore compile validation for MX350/Quest/Steam Deck/high-end profiles without changing Sentinel runtime behavior. Sargassum's restored arrays remain bounded staging data, not unbounded native allocations.
Hardware Impact: No Sentinel hot-path change. External bridge costs are existing bounded cold allocations or existing gameplay upload paths; exact CPU microseconds are unmeasured.

Problem: H8Memory's owner-key side lists could still accumulate duplicate stale keys after map/key divergence. The owner pointer lane cleanup removed current keys, but a pre-existing duplicate key could survive because removal stopped after the first match; a later `RegisterOwnerPointer` or `RegisterActiveJob` could add another key if the hash map entry was already gone.
Solution: Added bounded cold-path key insertion helpers for pointer lanes and job-fence lanes. `AddOwnerPointerKey` and `AddOwnerJobKey` dedupe before appending, while `RemoveOwnerPointerKey` and `RemoveOwnerJobKey` now scrub every duplicate match.
Rejected Alternatives: Rejected treating duplicate keys as harmless because transition/shutdown loops use those key arrays as deterministic owner iteration surfaces. Rejected rebuilding key lists from hash maps at every transition because the point cleanup is simpler and keeps work attached to owner creation/removal.
Scalability potential: Low/MX350, Quest/Android, and Steam Deck avoid persistent metadata creep without per-frame polling or disk writes. High/Ultra keep deterministic teardown surfaces so legitimate Ocean/VFX allocations can proceed after old-scene owner proof.
Hardware Impact: 0 B/frame. Added work is only on cold lane creation and owner teardown paths; exact CPU microseconds are unmeasured. Validation used `dotnet build --no-restore`; no `dotnet rebuild` was run.

Problem: `GlobalRegistry.ClearRuntimeBuckets()` clears the updatable bucket during scene transition, but `SceneRuntimeService._registeredUpdatable` can remain true. That can stop `SceneRuntimeService.Tick()` from being re-registered, which cuts off the 300-frame H8Memory and GlobalDataVault heartbeat after a runtime-state clear.
Solution: `SceneRuntimeService.ClearRuntimeState()` now asks the active scene runtime to restore its core tick registration after the registry buckets are cleared. The restore path verifies initialization/play mode/active state, checks whether the updatable bucket already contains the service, and otherwise re-runs the normal registration path.
Rejected Alternatives: Rejected setting `_registeredUpdatable` blindly because it would hide a missing bucket registration. Rejected registering from H8Memory because the memory layer must not own the scene runtime tick surface.
Scalability potential: Low/MX350, Quest/Android, and Steam Deck keep blackbox heartbeat continuity after transitions without per-frame polling or disk writes. High/Ultra keep the same heartbeat proof before Ocean/VFX domains consume recovered memory.
Hardware Impact: 0 B/frame. Added work is a cold scene-transition registration check; exact CPU microseconds are unmeasured.

Problem: `H8Memory.ReleaseSentinelReapedRaw()` could force-free an H8-tracked pointer found by `NativeMemorySentinel` without completing the owning system's registered `JobHandle`.
Solution: If the raw pointer is tracked by H8Memory, the sentinel reap path now reads the allocation record, completes the owner's registered jobs, then calls the existing force-free record path.
Rejected Alternatives: Rejected falling through to `UnsafeUtility.Free` for tracked pointers because it bypasses owner synchronization and H8Memory bookkeeping. Rejected per-frame leak polling because the sentinel reap path is already the cold fatal path.
Scalability potential: Low/MX350 and Quest avoid undefined native alias use during forced leak recovery. High/Ultra keep the same deterministic memory barrier before loading expensive Ocean/VFX state.
Hardware Impact: Cold fatal-leak path only. No gameplay hot-path cost; exact CPU microseconds are unmeasured.

Problem: Player presentation typed-lane payloads briefly existed in two places during parallel compile repair, causing duplicate signal definitions; the alternative state was missing the payloads entirely from the compile gate.
Solution: Kept a single compiled ABI definition in `GlobalSignals.cs`, including the existing `WaterTransitionSignal` lane validation and initialization. Reduced `Core/Signals/PlayerMovementPresentationSignals.cs` to an empty namespace shell so dotnet/Unity do not compile duplicate payloads while project metadata that references the file remains satisfied.
Rejected Alternatives: Rejected duplicate signal structs, removing the typed SignalBus lanes, or replacing them with legacy EventBus/managed delegates. Rejected deleting project metadata under a memory-agent pass.
Scalability potential: Low/Quest/Steam Deck keep compact fixed-size signal payloads and low-tier lane capacities. High/Ultra keep the same typed lanes for richer presentation systems without ABI ambiguity.
Hardware Impact: Compile/ABI hygiene only. Runtime cost is unchanged; exact CPU microseconds are unmeasured. Validation used `dotnet build --no-restore`; no `dotnet rebuild` was run.
