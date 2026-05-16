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
Hardware Impact: 300 fixed 64-byte entries = 19,200 bytes persistent memory; 0.0 us gameplay hot path, event-only writes on allocation/free/transition.

Problem: Raw allocation alignment accepted caller values directly, which can pass non-power-of-two or sub-16-byte alignment into ARM64/Quest native memory paths.
Solution: Added `ResolveSafeAlignment` and routed `AllocateRaw`/`ReallocateRaw` through a power-of-two alignment floor of 16 bytes.
Rejected Alternatives: Rejected trusting every caller. Rejected forcing all typed `NativeArray<T>` allocations through raw allocation because Unity's allocator already owns those typed paths.
Scalability potential: Low/Quest/Android gets safer raw pointer alignment. PC God-Mode remains unaffected.
Hardware Impact: 0.0 us hot-path impact; raw allocation is cold-path.

Problem: Latest build validation still cannot reach green because the repository's external contract state changed again outside CORE/MEMORY.
Solution: Reran `dotnet build Hecton8.Core.csproj --no-restore --nologo /clp:ErrorsOnly`; current errors are missing `Hecton8.VFX.Wakes`, `LightShaftContribution`, `ScreenSpaceLightShaftSource`, `WakeSource`, `WakeTelemetryEntry`, and `EcosystemDirector` interface drift. No CORE/MEMORY errors were reported.
Rejected Alternatives: Rejected repairing wake, lighting, and ecosystem contracts from the memory lifecycle domain. Rejected claiming compile success when the solution is still red.
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
