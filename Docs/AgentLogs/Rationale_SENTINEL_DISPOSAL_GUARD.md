# Rationale_SENTINEL_DISPOSAL_GUARD

Status: CORE COMPLETE / BUILD BLOCKED BY DEPENDENCY

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
Solution: Reran `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly`; current errors are duplicate `VehicleDockingModule` methods in Construction: `IsLowDockingMathTier`, `ResolveSystemStress01`, and `ResetDockingRuntimeCaches`. CORE/MEMORY touched files report no compiler errors.
Rejected Alternatives: Rejected editing Construction from the sentinel memory domain and rejected claiming green build while external duplicates remain.
Scalability potential: None in CORE/MEMORY; integrator or Construction owner must collapse duplicate docking methods before Unity/runtime profiling can prove transition timing.
Hardware Impact: None from this dependency block.
