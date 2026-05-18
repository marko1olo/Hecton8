# Mod API Sandbox Quarantine

Runtime UGC command execution is binary-only. Managed mod entry points are disabled at load time; mods must serialize fixed 64-byte `FutureCommandEnvelope` packets and submit them through `HectonAPI.Commands.RequestFuture`.

Bulk ingress must avoid per-packet Vault resolution. Use `FutureCommandSandboxValidator.RequestRawEnvelopeStream(NativeArray<byte>, byteLength)` for flat little-endian streams, `RequestRawEnvelopeStream(..., sourceBigEndian: true)` for legacy/big-endian hydrated streams, or `RequestFromExternalQueue(ref NativeQueue<FutureCommandEnvelope>, maxEnvelopeCount)` when an external producer queue is the boundary. The validator does not own that external queue; it drains it into the Vault pending ring.

Scheduler integration should prefer `FutureCommandSandboxValidator.TrySchedulePreSimulation(dependsOn, out JobHandle validationHandle)` when the caller owns a dispatcher dependency graph. The legacy `DrainPreSimulation()` path remains for the current void `ModCommandDispatcher`, but the scheduled path lets the integrator chain validator work without an immediate main-thread fence. Use `TryFinalizeScheduledPreSimulation(forceComplete: false)` only after `IsCompleted`; force completion is reserved for teardown or scene-transition boundaries.

`FutureCommandEnvelope` layout:

- `uint OpcodeHash`
- `uint ModderSignature`
- `double3 TargetAUP`
- `float4 PayloadData`
- `ulong IntegrityHash`
- `ulong _pad0`

The validator runs from `ModCommandDispatcher.DrainPreSimulation`, checks opcode allowlist, XXHash3 integrity over bytes `0..47`, finite +/-50 km AUP bounds, CRC32-approved asset references, declared asset byte ceiling, per-signature flood budgets, CPU-overheat backlog shedding through `GlobalQualityWeight`, and rollback freeze state. Valid future seams that have no owning gameplay system are routed to DevNull instead of crashing.

All persistent runtime state is Vault-owned. The validator stores only `VaultBufferHandle<T>` fields and resolves short-lived `NativeArray<T>` views per phase. The pending ring, DevNull ring, staging buffer, opcode records, per-mod counters, memory leases, approved asset manifest, ring state, tuning, blackbox memory, and telemetry ring use `BufferID.ShinobuModSandbox*` IDs. The legacy `NativeHashSet`/`NativeHashMap` implementation was removed in favor of fixed-size open-address tables to avoid private allocator state.

The modder blackbox memory arena is Vault-backed at `BufferID.ShinobuModSandboxBlackboxMemory`. Core simulation ignores this memory; only explicit memory opcodes can read/write inside the mod's assigned chunk. The 300-frame quarantine ring writes to `BufferID.ShinobuModSandboxTelemetryRing` and dumps `Docs/AgentLogs/Dump_QUARANTINE_SURGEON.bin` on memory, NaN, or layout faults.

Rollback freeze is read as a local 64-byte Vault flag view at buffer `70752`, flag bit `1 << 4`. This avoids a direct `Hecton8.Networking` runtime assembly reference from the sandbox validator while preserving Agent 64 resimulation quarantine.

Human controls live in `HECTON-8/Mod API Sandbox Tuner`. The editor facade adjusts continuous command budget, max mod memory, asset ceiling, opcode gates, CSV opcode reload, self-audit injection, and incoming/rejected traffic histogram.
