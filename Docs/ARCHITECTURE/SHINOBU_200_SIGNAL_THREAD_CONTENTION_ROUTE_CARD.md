# SHINOBU_200 Signal Thread Contention Route Card

Date: 2026-05-24
Owner: SHINOBU_200 / THREAD_CONTENTION_SURGEON
Domain: Core Signals / SignalBus MPSC contention corridor
Status: YELLOW / STATIC_SOURCE_ONLY / COMPILE BLOCKED BY EXTERNAL CORE DEPENDENCY WALL
Evidence class: STATIC_SOURCE / STATIC_DOC

Full historical route-card snapshot: `../_Archive/Architecture_X_012_APEX_2026-05-24_FILE_CAP/ARCHITECTURE_APEX_PRE_FILE_CAP_SHINOBU_200_SIGNAL_THREAD_CONTENTION_ROUTE_CARD.md`.

## Boundary

Owns: Core signal contention diagnostics and mock high-pressure `SignalBus` producer path.

Does not own: gameplay damage truth, audio DSP, combat resolution, rollback state, cross-domain producer scheduling.

## Route

| Field | Value |
|---|---|
| Route ID | `SHINOBU_200_SIGNAL_THREAD_CONTENTION` |
| Instrument | `GlobalDataVault` buffers `73043..73055`, `SignalBusRegistry`, closed-generic dispatch table, black-box dump route |
| Producer phase | simulation worker slice writes; rare cold overflow via `TryPushAsynchronousOverflow(...)` only |
| Consumer phase | POST_SIMULATION deterministic commit, then read-only snapshot |
| Cadence | frame-bounded commit |
| Quality scaling | continuous `GlobalQualityWeight` maps active per-thread stride through smoothstep |
| Fault dump | `Docs/AgentLogs/Dump_SHINOBU_200.bin` generated on fault only |

## Vault Buffers

| Buffer | Payload | Capacity |
|---:|---|---:|
| `73043` | front thread-local byte scratchpad | `byte[(64*16384)+64]` |
| `73044` | back thread-local byte scratchpad | `byte[(64*16384)+64]` |
| `73045` | front per-thread headers | `SignalThreadLocalHeader64[64]` |
| `73046` | back per-thread headers | `SignalThreadLocalHeader64[64]` |
| `73047` | committed mock damage snapshot | `SignalWardenMockDamageSignal[4096]` |
| `73048` | committed count | `int[1]` |
| `73049` | contention telemetry ring | `SignalThreadContentionTelemetryEntry[300]` |
| `73050` | telemetry cursor | `int[1]` |
| `73051` | tuning row | `SignalThreadContentionTuning64[1]` |
| `73052` | coalescence hash buckets | `int[8192]` |
| `73053` | bounded overflow signals | `SignalWardenMockDamageSignal[1024]` |
| `73054` | overflow header | `SignalThreadOverflowHeader64[1]` |
| `73055` | CSV scratch | `byte[8192]` |

## Runtime Rules

- Worker writes use `[NativeSetThreadIndex]` byte slices.
- Hot path rejects `lock`, `ConcurrentQueue<T>`, interlocked insertion cursor, direct `_...Signals.Enqueue(...)` duplication.
- Overflow uses `73053` plus `73054`; CAS cursor is rare saturated slow path only.
- Commit drains worker slices in deterministic order and coalesces same-cell rows through Vault-owned buckets.
- `SignalThreadLocalAupHash.ComputeCellHash(...)` returns sentinel `1u` for non-finite or overflowed AUP input.
- Persistent state stores `VaultGenerationHandle<T>` descriptors only; phase-local `NativeArray<T>` views are resolved immediately before use.
- `SignalBusRegistry.FlushPreSimulation()` and `ClearPostSimulationSnapshots()` use generated direct calls plus closed-generic fallback operation tables.
- Legacy `GlobalSignals.Publish(...)` routes through `SignalBus<T>.Push(...)`; compatibility queue handles do not receive double writes.
- High-frequency producers must use thread-local slices, not `TryPushAsynchronousOverflow(...)`.

## Tuning And Editor

- CSV source: `Assets/_SourceData/Signals/signal_corridor_capacities.csv`.
- CSV path: editor/source-data only, via Vault scratch `73055` and `ReadOnlySpan<byte>`.
- Player/runtime tuning must arrive through baked binary/Vault route, not `StreamingAssets` text.
- X-Ray tuner reads the 300-frame ring through `TryGetTelemetryReadOnly(...)` and UI Toolkit `generateVisualContent`.

## Proof Required Before Green

- Fresh compile/import artifact.
- Contention Play Mode route.
- Profiler and GCMonitor proof.
- Player-build proof.
- Linked command, timestamp, environment, and output path.
