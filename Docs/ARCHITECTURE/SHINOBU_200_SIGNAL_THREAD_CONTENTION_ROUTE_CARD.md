# SHINOBU_200 Signal Thread Contention Route Card

Date: 2026-05-20
Owner: SHINOBU_200 / THREAD_CONTENTION_SURGEON
Domain: Core Signals / SignalBus MPSC contention corridor
Status: STATIC SOURCE UPDATED - COMPILE BLOCKED BY CPU GUARD

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R45 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- Docs/README.md
- Docs/DOC_GOVERNANCE.md
- Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. R42 remains the prior counter/route-boundary/proof-label correction. R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R45): `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` is the latest local static root/architecture R43/R44 residue, proof-artifact wording, source-counter, and atlas-boundary correction. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->


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

`SignalBusRegistry.FlushPreSimulation()` and `ClearPostSimulationSnapshots()` dispatch generated Core lanes through explicit generic direct calls. Sibling-owned/non-generated typed lanes cannot be added to the Core direct list without breaking the compile wall, so registration stores closed-generic flush/clear operations in `SignalLaneDispatch[]` and drains those fallback lanes through that operation table. The legacy `ISignalLane[]` registry remains for cold identity/disposal only; telemetry copies also use cached closed-generic delegates rather than per-lane interface calls.

Rare external/cold interrupt producers may use `TryPushAsynchronousOverflow(...)`. High-frequency gameplay producers must not use that API; they must enter through thread-local slices.

`SignalThreadContentionLayoutGuard` validates SHINOBU-owned DTO sizes and byte offsets through `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset` during editor/development cold bootstrap. It is not run in initialized hot accessors.

Human tuning source: `Assets/StreamingAssets/signal_corridor_capacities.csv` is a checked-in ASCII CSV with platform, min stride, max stride, and output cap columns. `SignalThreadContentionCsvHotSwap` reads it through Vault buffer `73055` and `ReadOnlySpan<byte>`, rejects empty/oversized files, fails on short reads, lowercases platform bytes for FNV-1a hashing, applies only the exact detected platform row with `pc` fallback, and does not use `string.Split`, `int.Parse`, or managed dictionaries.

The editor `Signal Architecture X-Ray` tuner draws contention history through a UI Toolkit `VisualElement.generateVisualContent` waterfall graph. It reads the 300-frame telemetry ring through `TryGetTelemetryReadOnly(...)` and `Painter2D`; `OnInspectorUpdate` only marks the graph dirty. The previous per-refresh `Label.text` string concatenation path is removed from this file.

Adjacent Core signal buffers `73038..73042` were also moved off legacy pointer-bearing `VaultBufferHandle<T>` storage. `SignalTelemetryRingBuffer` and `SignalTuningTable` now persist `VaultGenerationHandle<T>` descriptors and resolve phase-local `NativeArray<T>` views through `IDataVault.TryResolveHandle(...)`; `SignalTuningTable` no longer stores private static `NativeArray<T>` aliases.

## Scalability

`GlobalQualityWeight` and Vault pressure drive active per-thread stride continuously through a smoothstep curve between the CSV/tuning min and max stride. Lower quality collapses memory traffic and increases overflow risk; higher quality preserves more lock-free event detail.


## R43 Review Disposition

| Field | Value |
|---|---|
| Route ID | `SHINOBU_200_SIGNAL_THREAD_CONTENTION` |
| Owner | SHINOBU_200 / THREAD_CONTENTION_SURGEON |
| Producer phase | SIMULATION worker slice writes; rare cold overflow ingress only through `TryPushAsynchronousOverflow(...)` |
| Consumer phase | POST_SIMULATION deterministic commit, then read-only snapshot for downstream consumers |
| Cadence | frame-bounded commit; capacity governed by Vault buffers `73043..73055` |
| Capacity | Worker-local mock payload slices, bounded overflow lane `73053`, overflow header `73054`, published snapshot/hash lanes, and fixed 300-entry telemetry |
| Overflow/failure | bounded overflow lane `73053` plus header `73054`; high-frequency producers must stay on thread-local slices; non-finite or saturated states require telemetry, not silent route promotion |
| Shutdown/disposal | Owner completes/drains owned scheduled handles before clearing contention state; Vault/SignalBus owners retain buffer and queue disposal authority |
| Proof required before GREEN | Fresh compile/import artifact, contention Play Mode route, profiler/GCMonitor proof, player-build proof, and linked output path with command, timestamp, environment, and result |
| Review disposition | YELLOW / STATIC_SOURCE_ONLY until compile, Unity import, Play Mode, profiler, GCMonitor, and player-build artifacts exist |
## Verification
Static checks passed for brace balance, forbidden hot-path patterns, deterministic Burst attributes, layout guard wiring, direct-list parity (`flush=135`, `clear=135`, `direct_policy=135`, drift `0`), and diff whitespace. Build, Unity import, Burst Inspector, profiler, GCMonitor, and player proof are pending because CPU guard stayed above 50 percent.
