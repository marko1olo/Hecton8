# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SHINOBU_200_SIGNAL_THREAD_CONTENTION_ROUTE_CARD.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_200 Signal Thread Contention Route Card

Date: 2026-05-20

Owner: SHINOBU_200 / THREAD_CONTENTION_SURGEON

Domain: Core Signals / SignalBus MPSC contention corridor

Status: STATIC SOURCE UPDATED - COMPILE BLOCKED BY EXTERNAL CORE DEPENDENCY WALL

## R48 Exact Route Field Normalization

Route ID: SHINOBU_200_SIGNAL_THREAD_CONTENTION_ROUTE_CARD

Owner: SHINOBU_200 / THREAD_CONTENTION_SURGEON

Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.

Producer/consumer phase: producer and consumer phases documented below; hot GlobalRegistry polling is forbidden.

Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.

Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.

Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.

Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.

Review disposition: YELLOW / STATIC_SOURCE_ONLY.

## Boundary

This lane owns only Core signal contention diagnostics and the mock high-pressure SignalBus producer path. It does not own gameplay damage truth, audio DSP, combat resolution, rollback state, or cross-domain producer scheduling.

No new asmdef or sibling-domain source reference was added. Existing `Hecton8.Core.asmdef` sibling references predate this route and are not claimed as SHINOBU_200 proof.

## Vault Buffers

- `73043` Front thread-local byte scratchpad, `byte[(64 * 16384) + 64]`, uninitialized.

- `73044` Back thread-local byte scratchpad, `byte[(64 * 16384) + 64]`, uninitialized.

- `73045` Front per-thread headers, `SignalThreadLocalHeader64[64]`, 64-byte rows.

- `73046` Back per-thread headers, `SignalThreadLocalHeader64[64]`, 64-byte rows.

- `73047` Committed mock damage snapshot, `SignalWardenMockDamageSignal[4096]`, 64-byte rows.

- `73048` Committed count, `int[1]`.

- `73049` 300-frame contention telemetry ring, `SignalThreadContentionTelemetryEntry[300]`, 64-byte rows.

- `73050` Telemetry cursor, `int[1]`.

- `73051` Live tuning row, `SignalThreadContentionTuning64[1]`, 64-byte row.

- `73052` Commit coalescence hash buckets, `int[8192]`, uninitialized and reset only over active bucket range.

- `73053` Bounded overflow signals, `SignalWardenMockDamageSignal[1024]`, 64-byte rows, uninitialized.

- `73054` Overflow header, `SignalThreadOverflowHeader64[1]`, 64-byte row.

- `73055` Contention CSV scratch, `byte[8192]`, uninitialized, cold parser only.

## Runtime Route

`GenerateSignalThreadContentionMockJob` writes 64-byte mock damage payloads into worker-exclusive byte slices by `[NativeSetThreadIndex]`. The hot path uses no `lock`, no `ConcurrentQueue<T>`, and no `Interlocked` insertion cursor. Capacity overflow is routed to Vault buffer `73053` through a 64-byte overflow header in buffer `73054`; the CAS cursor exists only on the rare saturated slow path.

Overflow is a sequence-tagged MPSC ring: `SignalThreadOverflowHeader64` carries monotonic `long WriteCursor` and `long ReadCursor`, and each overflow payload publishes `OverflowSequence = ticket + 1` only after the 64-byte row is copied. `SignalThreadLocalCommitJob` drains slices in deterministic worker order, clamps each read to the worker header's recorded active stride, hashes AUP by subtracting the supplied sector origin before local float quantization, drains only contiguous published overflow rows, and uses Vault-owned hash buckets for expected O(N) same-cell coalescence before writing the final snapshot. The legacy `ScheduleCommit(frame, dependency, out handle)` overload remains for compatibility; sector-aware callers should use `ScheduleCommit(frame, sectorOriginAup, dependency, out handle)`.

`SignalThreadLocalAupHash.ComputeCellHash(...)` is the final NaN vaccine for this route: it rejects non-finite AUP input, non-finite sector origin input, and overflowed local `float3` casts by returning deterministic sentinel hash `1u` instead of allowing NaN/Infinity to enter bucket indexing.

`SignalThreadLocalScratchpad` persists only `VaultGenerationHandle<T>` descriptors for buffers `73043..73055`. It resolves transient `NativeArray<T>` views through `IDataVault.TryResolveHandle(...)` inside the caller phase immediately before scheduling jobs, reading telemetry, mutating the tuning row, or returning a cold editor snapshot. Editor/consumer snapshot reads use `NativeArray<SignalWardenMockDamageSignal>.ReadOnly` through `TryGetCommittedSignalsReadOnly(...)`; the writable snapshot surface remains legacy/owner-local only. It does not retain private static `NativeArray<T>` aliases for SHINOBU-owned Vault memory. If a same-vault generation resolve fails, the initialized flag is cleared and the cold path reacquires fresh generation handles. Buffer resolution now fails at the first undersized or unresolved Vault handle instead of hiding every condition inside a chained boolean expression.

`SignalBusRegistry.FlushPreSimulation()` and `ClearPostSimulationSnapshots()` dispatch generated Core lanes through explicit generic direct calls. Sibling-owned/non-generated typed lanes cannot be added to the Core direct list without breaking the compile wall, so registration stores closed-generic flush/clear operations in `SignalLaneDispatch[]` and drains those fallback lanes through that operation table. The legacy `ISignalLane`/adapter registry has been removed; cold disposal is a cached `SignalLaneDisposeDelegate[]`, and telemetry copies plus `ReportSignalLaneTelemetry()` sampling use cached closed-generic delegates rather than per-lane interface calls. Exact pushed/corrupted counters are packed into the existing `SignalLaneTelemetry.Reserved2` 64-bit lane so the public 32-byte telemetry stride does not change. `SignalLaneTelemetry.Flags` bit `16` marks corrupted lanes, and corrupted-only lanes are treated as critical crash telemetry even when snapshot and dropped counts are zero.

Legacy `GlobalSignals.Publish(...)` overloads now route legacy payloads through `SignalBus<T>.Push(...)` only. The old `NativeQueue<T>` fields are compatibility handles copied from the same closed `SignalBus<T>` queue; they no longer receive direct `_...Signals.Enqueue(...)` writes that double-insert one fact into the same MPSC lane. Legacy `NativeQueue<T>.ParallelWriter` wrapper properties now delegate through `GlobalSignals.OpenSignalWriterForProducerPhase<TSignal>()`, so owned Core producer acquisition has an explicit open route. The compatibility `SignalBus<T>.ParallelWriter` property remains for sibling-domain producers and delegates to `OpenParallelWriter()`. The only remaining `.AsParallelWriter()` call in the owned route is inside the canonical closed `SignalBus<T>` implementation. The unused legacy `PrewarmQueue<T>(ref NativeQueue<T>, int)` helper was removed so the facade no longer carries a dead direct-enqueue pattern.

Rare external/cold interrupt producers may use `TryPushAsynchronousOverflow(...)`. High-frequency gameplay producers must not use that API; they must enter through thread-local slices.

`SignalThreadContentionLayoutGuard` validates SHINOBU-owned DTO sizes and byte offsets through `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset` during editor/development cold bootstrap. It is not run in initialized hot accessors.

Human tuning source: `Assets/_SourceData/Signals/signal_corridor_capacities.csv` is a checked-in ASCII CSV with platform, min stride, max stride, and output cap columns. `SignalThreadContentionCsvHotSwap` reads it only in editor/source-data paths through Vault buffer `73055` and `ReadOnlySpan<byte>`, rejects empty/oversized files, fails on short reads, lowercases platform bytes for FNV-1a hashing, applies only the exact detected platform row with `pc` fallback, and does not use `string.Split`, `int.Parse`, or managed dictionaries. Player/runtime builds must receive these rows through a baked binary/Vault route, not `StreamingAssets` text.

The editor `Signal Architecture X-Ray` tuner draws contention history through a UI Toolkit `VisualElement.generateVisualContent` waterfall graph. It reads the 300-frame telemetry ring through `TryGetTelemetryReadOnly(...)` and `Painter2D`; `OnInspectorUpdate` only marks the graph dirty. The previous per-refresh `Label.text` string concatenation path is removed from this file.

Adjacent Core signal buffers `73038..73042` were also moved off legacy pointer-bearing `VaultBufferHandle<T>` storage. `SignalTelemetryRingBuffer` and `SignalTuningTable` now persist `VaultGenerationHandle<T>` descriptors and resolve phase-local `NativeArray<T>` views through `IDataVault.TryResolveHandle(...)`; `SignalTuningTable` no longer stores private static `NativeArray<T>` aliases.

## Scalability

`GlobalQualityWeight` and Vault pressure drive active per-thread stride continuously through a smoothstep curve between the CSV/tuning min and max stride. Lower quality collapses memory traffic and increases overflow risk; higher quality preserves more lock-free event detail.

## R43 Review Disposition

| Field | Value |

|---|---|

| Route ID | `SHINOBU_200_SIGNAL_THREAD_CONTENTION` |

| Owner | SHINOBU_200 / THREAD_CONTENTION_SURGEON |

| Instrument | GlobalDataVault thread-local scratch/overflow/telemetry buffers `73043..73055`, SignalBusRegistry direct/fallback lane dispatch, closed-generic operation table, and black-box dump route |

| Producer phase | SIMULATION worker slice writes; rare cold overflow ingress only through `TryPushAsynchronousOverflow(...)` |

| Consumer phase | POST_SIMULATION deterministic commit, then read-only snapshot for downstream consumers |

| Cadence | frame-bounded commit; capacity governed by Vault buffers `73043..73055` |

| Capacity | Worker-local mock payload slices, bounded overflow lane `73053`, overflow header `73054`, published snapshot/hash lanes, and fixed 300-entry telemetry |

| Overflow/failure | bounded overflow lane `73053` plus header `73054`; high-frequency producers must stay on thread-local slices; non-finite or saturated states require telemetry, not silent route promotion |

| Shutdown/disposal | Owner completes/drains owned scheduled handles before clearing contention state; Vault/SignalBus owners retain buffer and queue disposal authority |

| Telemetry fields | Frame, committed count, overflow count, dropped/coalesced count, active worker count, slow-path flags, quality, state hash, and estimated commit time |

| Black-box fields | 300-entry `SignalThreadContentionTelemetryEntry` ring, telemetry cursor, overflow header, committed count, and fault flags |

| Fault dump target | `Docs/AgentLogs/Dump_SHINOBU_200.bin` is planned/generated on fault; no existing artifact is implied unless linked with runtime trigger evidence |

| Proof required before GREEN | Fresh compile/import artifact, contention Play Mode route, profiler/GCMonitor proof, player-build proof, and linked output path with command, timestamp, environment, and result |

| Review disposition | YELLOW / STATIC_SOURCE_ONLY until compile, Unity import, Play Mode, profiler, GCMonitor, and player-build artifacts exist |

## Verification

Static checks passed for brace balance, forbidden hot-path patterns, deterministic Burst attributes, layout guard wiring, legacy publish alias de-duplication, direct-list parity (`flush=135`, `clear=135`, `direct_policy=135`, drift `0`), and diff whitespace. Focused `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1` was attempted after CPU guard opened and failed with `76` external dependency-wall errors; no diagnostic named `GlobalSignals.cs` or `SignalWardenRuntime.cs`. Unity import, Burst Inspector, profiler, GCMonitor, and player proof remain pending.

## 2026-05-20 Pure Read / Explicit Consume Addendum

SignalBus snapshot inspection now follows the global read-accessor doctrine. `SnapshotCount`, `SnapshotGeneration`, `GetFrameSnapshot()`, and `GetFrameSnapshotArray()` use a pure cached-handle read path that does not refresh Vault handles, allocate/grow buffers, publish signals, complete jobs, or mutate counters. Owner mutation remains in the dispatcher flush route through `TryOpenFrameSnapshotForOwnerWrite(...)`, and destructive legacy iteration is named `TryConsumeFrame(...)`.

The legacy `GlobalSignals.TryDequeue*` bridge still consumes frame rows for existing consumers, but it now calls the explicit consume API instead of a read-looking `TryReadFrame` surface. Direct consumers in Core determinism, camera juice, terminal command, save chunk dehydration, and atmosphere base-transition handling were repointed for source-level clarity. No DTO layout, BufferID, SignalBus payload stride, save identity, or authority route changed.

Static proof for this addendum: touched-file brace counts are balanced; `SignalBus<...>.TryReadFrame`, `TryResolveFrameSnapshot`, `TryResolveFrameSnapshotVault`, and `GetQueueForLegacyGlobalSignals` scans over touched files returned no matches; `git diff --check` reports only LF-to-CRLF warnings. No build was launched for this addendum because the guard sampled `CPU=100`, `dotnet=0`, `csc=0`, and the previous focused build already established the external Core dependency wall.

## 2026-05-20 Scratchpad TryGet Purity Addendum

SHINOBU scratchpad read facades now also obey the global read-accessor doctrine. `TryGetLatestTelemetry(...)`, `TryGetTelemetryReadOnly(...)`, `TryGetTuning(...)`, `TryGetThreadHeader(...)`, and `TryGetCommittedSignalsReadOnly(...)` do not bootstrap the Vault or query `GlobalDataVault.TryGetLatestCreated()`. They read only if `_initialized != 0`, `_vault != null`, and cached generation handles resolve.

The mutable committed-snapshot opener was renamed to `TryOpenCommittedSignalsForOwner(...)`, and mutable CSV scratch access was renamed to `TryOpenCsvScratchForLoad(...)`. Explicit owner/writer/mutation routes still initialize when needed: `TryAcquireWriteContext(...)`, `ScheduleCommit(...)`, `TryPushAsynchronousOverflow(...)`, `RecordLastCommitMicroseconds(...)`, `MutateTuning(...)`, and `DumpToDisk()` retain owner-phase or crash-path initialization authority. This changes no BufferID, DTO layout, SignalBus payload stride, save identity, or authority route.

## 2026-05-20 Telemetry Ring Read/Open Split Addendum

`SignalTelemetryRingBuffer.CopyFrames(...)` is now a cached-handle diagnostic read. It calls `TryReadRing(...)`, which fails closed if the telemetry owner has not initialized and does not call `GlobalRegistry.DataVault`, `Initialize()`, publish signals, allocate/grow buffers, complete jobs, or mutate global state.

Telemetry owner write and crash dump routes remain explicit. `ReportFrame(...)` uses `TryOpenRingForOwnerWrite(...)`; `DumpToDisk()` uses `TryOpenRingForCrashDump(...)`. The cold CSV tuning bridges use `TryOpenCsvScratchForLoad(...)` for mutable scratch writes and then parse with `ReadOnlySpan<byte>`. This preserves existing BufferIDs, DTO layouts, payload strides, save identity, and authority route.

## 2026-05-20 Explicit Producer Writer Open Addendum

`SignalBus<T>.OpenParallelWriter()` and `GlobalSignals.OpenSignalWriterForProducerPhase<TSignal>()` are the maintained producer-acquisition APIs for Core signal code. These methods can open a mutable `NativeQueue<T>.ParallelWriter`, so their names are intentionally not `Get*`, `TryGet*`, `Resolve*`, or `Read*`. `OpenSignalWriterForProducerPhase<TSignal>()` does not call `GlobalSignals.InitializeAllQueues()`; it opens only the typed `SignalBus<TSignal>` legacy bridge lane, avoiding broad direct-queue prewarm during writer acquisition.

All SHINOBU/Core `GlobalSignals.*SignalWriter` facades call `OpenSignalWriterForProducerPhase<TSignal>()`. The already-touched `MemorySentinelRuntime` and `TerminalOsRuntime` bridge producers call `SignalBus<T>.OpenParallelWriter()`. The legacy `SignalBus<T>.ParallelWriter` property remains only as a compatibility facade for sibling domains that have not moved yet; removing it globally would expand the patch beyond this route and increase compile-wall risk. This changes no BufferID, DTO layout, SignalBus payload stride, save identity, or authority route.

2026-05-21 clarification: retained `NativeQueue<T>.ParallelWriter` access is a low-frequency legacy bridge only. Cache-line-critical producer storms use the SHINOBU thread-local corridor: Vault-backed per-worker scratch, explicit 64-byte payload rows, deterministic post-simulation commit, AUP-cell coalescence, and telemetry-visible overflow. `GLOBAL_SIGNAL_CORRIDOR.md` has been corrected to remove the old generic endorsement of `ParallelWriter` as the preferred MPSC path.

The individual `GlobalSignals.*SignalWriter` XML summaries now match the same rule: they are legacy bridge writers for low-frequency compatibility producers. The corridor consume vocabulary is also split: `TryConsumeFrame(...)` is the explicit destructive cursor path, retained `TryDequeue*` methods are bridge drains, and snapshot APIs remain read-only inspection surfaces.

Adjacent Core writer helpers now use the same source contract vocabulary. `ThreadSafeCommandQueue.OpenLegacyMpscWriter()` and `TryOpenParallelWriter(...)` own structural-command writer acquisition; the retained `TryGetParallelWriter(...)` compatibility method no longer initializes storage. `BurstCallbackQueue.OpenParallelWriter()` owns callback writer acquisition, with the old `ParallelWriter` property kept as a documented compatibility alias. Current static debt scan shows `24` raw `.AsParallelWriter()` hits repo-wide and `49` `SignalBus<T>.ParallelWriter` compatibility-property call sites; hits outside Core/SignalBus remain sibling-domain debt for their owners, not SHINOBU route approval.

`BurstCallbackQueue` no longer allocates its pending-count lane with a direct persistent `NativeArray<int>`. The one-int counter now opens and releases through `H8Memory` owner `SystemID.CoreDiagnostics`; if that allocation fails, the constructor unregisters and disposes the already-created native queue before returning fail-closed. This is a low-frequency Core callback-bridge cleanup and does not change the SHINOBU thread-local signal corridor, SignalBus payload ABI, queue authority, rollback exclusion, save identity, or quality curve.

Compile-wall hygiene: the touched Core signal/command files no longer carry dead sibling namespace imports. `GlobalSignals.cs` does not import `Hecton8.World`, `ThreadSafeCommandQueue.cs` does not import `Hecton8.Caves`, and `Hecton8.Core.asmdef` already has no World/Caves runtime assembly reference. Biome/cave signal symbols used in `GlobalSignals.cs` are Core contract/local DTO surfaces, not sibling runtime coupling.

Sidecar route closure: `ThreadSafeCommandQueue.ExecuteCommand(...)` no longer dispatches `UndoPDAState` through `Hecton8.UI.PDAEvents.RaiseUndoRequest(...)`. The structural command queue now calls Core-owned `UIStateStore.TryRollbackPDAState(command.IntValue <= 0 ? 1 : command.IntValue)` directly, preserving the existing UI event clamp semantics while removing concrete Core-to-UI coupling. This changes no SignalBus payload, BufferID, DTO layout, rollback exclusion, save identity, shader payload, or quality curve.

Residual boundary: a broader Core scan still finds direct sibling references in broad dispatcher/registry/context authority files such as `SystemDispatcher`, `GlobalRegistry`, `GlobalRegistryContracts`, runtime context services, diagnostics viewers, and player context managers. Those files are not SHINOBU-owned SignalBus/MPSC implementation surfaces and require a separate integrator/core-owner route-card migration. This route card only claims the focused touched surface: `ThreadSafeCommandQueue`, `GlobalSignals`, and `BurstCallback`.

`ThreadSafeCommandQueue.TryGetParallelWriter(...)` has been removed. Structural command writer acquisition is exposed only through open/producer vocabulary: `TryOpenParallelWriter(...)`, `OpenLegacyMpscWriter()`, and the retained legacy `AsParallelWriter()` alias. This closes the last read-looking writer accessor in the touched structural-command route without changing queue ownership, payload layout, rollback identity, save identity, or SignalBus authority.

## 2026-05-20 Owner Route Vault Fallback Containment Addendum

SHINOBU contention producer/scheduler/mutation routes do not use `GlobalDataVault.TryGetLatestCreated()`. `TryAcquireWriteContext(...)`, `ScheduleCommit(...)`, `TryPushAsynchronousOverflow(...)`, `ScheduleOrphanedLockAutopsy(...)`, `RecordLastCommitMicroseconds(...)`, and `MutateTuning(...)` call `EnsureInitializedForOwnerRoute()`, which uses only the cached `_vault` established by explicit owner initialization and fails closed when that proof is absent.

Crash diagnostics remain a separate route. `DumpToDisk()` calls `EnsureInitializedForCrashDumpRoute()`, which may consult `GlobalRegistry.DataVault` and then `GlobalDataVault.TryGetLatestCreated()` so the 300-frame black-box can still be written during failure analysis. This is crash/diagnostic fallback only, not a producer bootstrap route.

## 2026-05-20 SignalBus Snapshot Bootstrap Tightening Addendum

Generic `SignalBus<T>` snapshot storage no longer falls back to `GlobalDataVault.TryGetLatestCreated()`. `TryFindFrameSnapshotVaultForBootstrap(...)` accepts only the owner-published `GlobalRegistry.DataVault` and otherwise fails closed. This prevents producer-reachable `SignalBus<T>.EnsureInitialized()` calls from binding runtime traffic to an arbitrary latest-created Vault.

`SignalTelemetryRingBuffer.ReportFrame(...)` also uses only cached ring ownership. `TryOpenRingForOwnerWrite(...)` requires `_vault` and `_initialized` from cold `SignalTelemetryRingBuffer.Initialize()` and does not poll `GlobalRegistry.DataVault` during frame telemetry writes.

## 2026-05-21 Adjacent Terminal Bridge Fallback Containment Addendum

The already-touched `TerminalOsRuntime` bridge no longer uses `GlobalDataVault.TryGetLatestCreated()` when opening its native UI buffers. `EnsureNativeResources()` now uses cached `_vault` or owner-published `GlobalRegistry.DataVault` and fails closed through the existing `FaultVaultUnavailable` path when the registry has not provided a Vault.

This is an adjacent containment patch, not a TerminalOS authority expansion by SHINOBU_200. It changes no TerminalOS BufferID, DTO layout, SignalBus payload stride, save identity, rollback exclusion, or UI truth owner. The purpose is to keep the SHINOBU producer-writer proof set free of normal runtime latest-created fallback routes outside the documented crash/diagnostic exception.

## 2026-05-21 Sidecar Read-Accessor Findings Response Addendum

The read-only sidecar audit found adjacent source-contract residue in the already-touched proof set. `MemorySentinelRuntime.TryGetTunerSnapshot(...)` now reads only pre-existing Vault handles and no longer calls owner setup or runtime-state default mutation. `MemorySentinelRuntime.OpenOrAcquireVaultBuffer(...)` and `TerminalOsRuntime.OpenNativeBufferForOwner(...)` name their allocation/open authority explicitly. `TerminalOsRuntime.OpenTerminalStateRefForOwner(...)` names mutable ref-return state access explicitly.

These changes do not expand SHINOBU_200 ownership into MemorySentinel or TerminalOS. They remove read-looking mutation/open surfaces from files already included in the Core SignalBus producer-route proof set. No BufferID, DTO layout, SignalBus payload stride, save identity, rollback exclusion, or gameplay/UI truth owner changed.

## 2026-05-21 Adjacent Mutating Resolve Name Eviction Addendum

The already-touched TerminalOS and MemorySentinel bridge files no longer keep private mutating/open/cold paths under stale `Resolve*` names where the method can write state, open owner pointers, discover compute kernels, cache camera references, capture input edge state, or probe the filesystem.

Renamed TerminalOS paths: `RefreshAttentionCameraForOwner(...)`, `TryCaptureCameraFrameForOwner(...)`, `EnsureComputeKernelForOwner(...)`, `CaptureGazeInputForOwner(...)`, `CaptureGazePoseForOwner(...)`, `SelectStateBuffer(...)`, and `OpenTerminalStatePointerForOwner(...)`. Renamed MemorySentinel paths: `OpenRuntimeStateForOwner(...)`, `RefreshTargetsForOwner(...)`, and `FindValidationRulesCsvPathCold(...)`.

This is source-contract hygiene in adjacent files already present in the SHINOBU proof set. It changes no BufferID, DTO layout, SignalBus payload stride, save identity, rollback exclusion, authority owner, or quality curve.

## 2026-05-21 Owner Snapshot Mutation / Editor Blocking Disclosure Addendum

`SignalBus<T>.TransformSnapshot(...)` and `SignalBus<T>.FilterSnapshot(...)` now open the current frame snapshot through `TryOpenFrameSnapshotForOwnerWrite(...)` before mutating rows. Pure snapshot inspection remains on `TryReadFrameSnapshot(...)`; destructive legacy cursor iteration remains named `TryConsumeFrame(...)`.

The UI Toolkit contention tuner benchmark is explicitly named `RunMockContentionEditorBlocking(...)`. It remains inside `#if UNITY_EDITOR` and force-completes scheduled handles only for manual editor stress timing. Runtime producer/commit routes still hand `JobHandle`s back to the dispatcher path and are not represented by this blocking editor button.

This changes no BufferID, DTO layout, SignalBus payload stride, save identity, rollback exclusion, authority owner, or quality curve.

## 2026-05-21 SPSC ARM Memory Barrier Addendum

`SpscSignalRingBuffer<T>` now publishes `_head` and `_tail` mutations through `Interlocked.Exchange(...)`; reads remain `Volatile.Read(...)`. The wrapper is an SPSC escape hatch and currently has no first-party C# callsite, but the latent fallback now matches the native-memory mandate for weak ARM memory ordering.

This changes no BufferID, queue type, SignalBus payload stride, save identity, rollback exclusion, authority owner, or quality curve.

## 2026-05-21 Cache-Line-Critical Audit Sidecar Addendum

The SignalBus static audit tools now report cache-line-critical lane stride debt

when `ConfigureCacheLineCritical(...)` is used on a payload whose declared

`StructLayout(Size = N)` is not 64 or 128 bytes. Current SignalCritical evidence

has two INFO rows: `ToolAcousticSignal` at 32 bytes and `TetherTensionSignal` at

192 bytes. This is audit visibility only; the payload ABI, queue route, legacy

MPSC writer bridge, DataVault ownership, and telemetry ring authority are

unchanged.

## 2026-05-21 Cache-Line-Critical Debt Action Ledger

Current57 keeps both cache-line-critical debt rows open as explicit migration

items. No payload field, offset, size, writer route, or producer callsite changes

in this ledger.

| Lane | Current Size | Struct Anchor | Boot Anchor | Size Guard | Telemetry Proof | Owner Risk | Current Action | Future Migration Gate |

|---|---:|---|---|---|---|---|---|---|

| `ToolAcousticSignal` | 32 bytes | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9713` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7967` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6212` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2569`, `:2604` | Broad tool/audio producer spread; padding now would touch sibling producers. | Keep `SignalLaneTelemetry.Flags` bit `32` and static INFO audit row active. | Owner-approved 64-byte gameplay payload or a separate visual/acoustic sidecar route with producer inventory. |

| `TetherTensionSignal` | 192 bytes | `Assets/_Project/Scripts/Core/GlobalSignals.cs:668` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8026` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6304` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2569`, `:2604` | Heavy physics/tool payload; direct split crosses tether truth and visual presentation ownership. | Keep telemetry debt visible; do not raise cadence based on this lane without budget proof. | Route-carded split into compact gameplay truth plus visual sidecar, or 128-byte bounded payload redesign with producer/consumer migration proof. |

The audit parser now scans a bounded forward statement for

`SignalBus<T>.ConfigureCacheLineCritical(...)`, so future multiline formatting

does not hide either debt row. This is static-source evidence only.

## 2026-05-21 Current58 Read Contract / Audit Summary Addendum

`SignalThreadLocalScratchpad` now reserves `TryOpen*` naming for the private

mutable committed-buffer opener and keeps `TryGetCommittedSignalsReadOnly(...)`

as the consumer-facing read-only snapshot route. This changes no BufferID,

payload DTO, committed count storage, queue type, producer callsite, or

dispatcher handle.

The PowerShell audit summary now reports `localNativeSignalQueues`, matching the

C# CLI summary. Current SignalCritical proof reports `0` local native signal

queue hits, so the change is summary drift repair rather than new runtime debt.

The debt ledger above now includes static anchors for the two cache-line-critical

payloads and their boot/telemetry proof without padding or splitting them.

## 2026-05-21 Current59 Audit CLI SDK Floor Addendum

`Tools/SignalBusContractAuditCli/SignalBusContractAuditCli.csproj` now targets

`net8.0` with explicit `LangVersion 12.0`. The scanner source still uses its

existing C# 12 shape, but the project no longer requires a .NET 10 SDK before

source compilation starts. This is toolchain portability only; no runtime Core

assembly, SignalBus route, DTO layout, queue, or generated audit rule changed.

## 2026-05-21 Current60 Tuning Read / Bootstrap DI Addendum

`SignalTuningTable.TryGetProfile(...)` now consumes a read-only profile view and

copied count. Mutable table rows are opened only by `TryOpenBuffersForOwner(...)`

for boot/default profile upsert or editor/source-data ingestion. The thread

scratch readiness helper is named `AreVaultBuffersReady(...)`, not

`ResolveBuffers(...)`, because it validates already-owned Vault handles.

`GlobalSignals.InitializeAllQueues()` now reads `GlobalRegistry.DataVault` once

in the bootstrap owner phase and passes that cached dependency into tuning and

thread-contention initialization. This is cold DI tightening only; no hot path,

BufferID, SignalBus payload, writer route, or quality curve changed.

## 2026-05-21 Sidecar Purity Closure Addendum

The Gauss sidecar audit found no actionable SHINOBU-scope defects after the

read-looking writer alias removal. Audited read accessors are pure inspection

paths, writer conversion is limited to explicit open/compatibility methods,

Burst jobs retain deterministic synchronous flags with `[NoAlias]` on relevant

`NativeArray` fields, and normal runtime does not use

`GlobalDataVault.TryGetLatestCreated()`.

The two cache-line-critical payload debt rows remain unchanged:

`ToolAcousticSignal` is still 32 bytes and `TetherTensionSignal` is still 192

bytes. They stay visible in the debt ledger above until their producers and

consumers can be migrated under owner-approved route cards.

## 2026-05-21 Combat Damage Codec Core Boundary Addendum

`CombatDamageSignalCodec` no longer reconstructs runtime damage AUP through the

concrete `Hecton8.World.AbsoluteUniversePosition` type. The Core codec now

projects runtime points by adding the local runtime delta to

`HectonFloatingOrigin.CurrentTotalOffsetDouble` in double precision, then applies

finite guards before returning the unchanged `double3` AUP payload.

This changes no `CombatDamageSignal` field, struct size, BufferID, SignalBus

lane, save identity, shader payload, rollback exclusion, or quality authority.

It closes the touched Core signal surface against the direct World AUP reference

reported by the sidecar while avoiding a broad call-site migration across combat,

fauna, vehicle, construction, and habitat owners.

## 2026-05-21 Legacy AUP Alias Compile Fence Addendum

`GlobalSignals.cs` still carries legacy signal DTOs that use

`AbsoluteUniversePosition` and `AbsoluteUniversePositionBlit`. Those DTOs were

not migrated in this SHINOBU pass because that would be a global signal ABI

migration across many owners. The file now imports only those two AUP DTO names

through explicit aliases instead of a broad `using Hecton8.World;`.

This is not a new authority route. It is a compile fence around existing payload
types while the combat damage codec itself stays on Core floating-origin
`double3` math and avoids World AUP construction helpers.

## 2026-05-21 PROJECT_AUDIT Cache-Line Telemetry Predicate Reconciliation

`SignalBus<T>.HasCacheLineCriticalStrideDebt()` now matches the static
SignalBus audit rule: cache-line-critical lanes are clean only at 64 or 128
bytes. The previous runtime predicate allowed any 64-byte multiple up to 192,
which meant `TetherTensionSignal` could avoid telemetry flag bit `32` even
though the static audit and this route card deliberately keep it open as stride
debt.

This changes no `ToolAcousticSignal` or `TetherTensionSignal` field, struct
size, SignalBus queue route, writer facade, BufferID, save identity, rollback
boundary, or quality curve. It is proof-surface reconciliation only:
`ToolAcousticSignal` at 32 bytes and `TetherTensionSignal` at 192 bytes both
remain INFO debt rows until owner-approved 64/128-byte payload migration or
truth/visual sidecar split.
