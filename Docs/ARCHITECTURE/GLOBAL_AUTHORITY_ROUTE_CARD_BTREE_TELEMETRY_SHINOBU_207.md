# Global Authority Route Card: B-Tree MMF Telemetry

Date: 2026-05-20

Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING

Owner: SHINOBU_207

Owner domain: Echelon 1 Core & Memory Infrastructure / MMF cache optimizer

Evidence class: STATIC_SOURCE + STATIC_DOC + PY_TOOL. Unity runtime proof absent; latest targeted C# proof is blocked by a foreign dependency wall plus CPU guard.

## Route Card

Route ID: CORE_DATA_BTREE_MMF_TELEMETRY

Owning file/system: `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs`

Problem: MMF B-Tree lookups need a 300-frame forensic trail for depth, key scans, prefetch touches, slow lookup samples, and dump triggers.

- Owner-local data is insufficient.
- Static-data, Babel, and H8LR readers share the same B-Tree ABI and failure mode.
- A reader-local ring would split one fact into multiple buffers.
- Unified postmortem evidence would be lost.

Why direct caller/owner interface is insufficient: Telemetry is consumed by crash dump and editor X-Ray tooling, not a private caller response.

Instrument:

- GlobalDataVault / IDataVault

- Black-box/telemetry route

Route fields:

Producer/consumer phase: lookup readers accumulate during source read paths; `FlushBTreeTelemetryPostSimulationJob` flushes in `POST_SIMULATION`; VISUAL_SYNC/editor tooling reads snapshots only.

Cadence/capacity: dirty/sample based; at most one 64-byte ring entry per frame; 300-entry retention.

Overflow/failure: ring wraps modulo 300; missing Vault fails closed; slow-sample dump target is planned/generated-on-fault only.

Shutdown/disposal: Vault owns native lifetime; readers cache generation handles and re-resolve after Vault changes.

Proof required before GREEN: Unity import, C# compile, Play Mode lookup smoke, GC 0 B/frame proof, profiler sample, and timestamped dump readback.

Review disposition: `YELLOW / STATIC_SOURCE_ONLY`.

Producer phase: lookup readers accumulate during source read paths; `FlushBTreeTelemetryPostSimulationJob` is intended for POST_SIMULATION.

Consumer phase: POST_SIMULATION writes the ring; VISUAL_SYNC/editor tooling may read snapshots.

Cadence: dirty/sample based. Lookup paths update a one-entry accumulator; POST_SIMULATION flush writes at most one 64-byte ring entry per frame.

Expected max events/reads per frame: 1 ring write per frame, bounded 300-entry retention. X-Ray reads are editor-only.

GlobalQualityWeight behavior: weight is recorded in telemetry and controls lookup prefetch stride; it does not change ownership, node shape, or lookup truth.

Payload/data shape:

- `BTreeTelemetryEntry`: explicit 64 bytes, unmanaged.

- `BTreeTelemetryAccumulatorDTO`: explicit 64 bytes, unmanaged.

- `BTreeTuningProfileDTO`: explicit 64 bytes, unmanaged cold tuning profiles.

Managed fields present: no.

UnityEngine.Object fields present: no.

Layout proof: 16 4-byte lanes per telemetry entry; exact offsets documented in `Docs/AgentLogs/LOG_SHINOBU_207.md`.

Capacity:

- BufferID `72070`: 300 `BTreeTelemetryEntry` records.

- BufferID `72071`: 1 cursor `int`.

- BufferID `72072`: 1 `BTreeTelemetryAccumulatorDTO`.

- BufferID `72073`: 16 `BTreeTuningProfileDTO` records.

Overflow/failure: ring wraps by cursor modulo 300. Missing Vault fails closed; lookup still runs without telemetry. Slow samples over 0.5 ms request dump to `Docs/AgentLogs/Dump_SHINOBU_207.bin`.

Telemetry fields: frame, search count, average depth Q8, keys processed, slowest ns, last hash, result offset, node count, root offset, flags, quality weight, prefetch touches, error hash.

Black-box fields: same as telemetry fields plus dump request flag in reserved lane.

Profiler marker: not added in this pass; Unity compile/profiler proof remains blocked by a 188-error foreign dependency wall and current CPU guard.

GC proof required: Unity Profiler or GCMonitor over 300 frames after compile/import.

Shutdown/disposal: buffers are Vault-owned and released by `GlobalDataVault` lifecycle. Readers cache generation handles and re-resolve after Vault changes.

Scene unload behavior: Vault owner releases buffers; no private persistent NativeArray owner exists in reader classes.

Stale-handle behavior: reader `BindDataVault` clears generation handles; `TryResolveHandle` failure reacquires through `GetGenerationHandle`.

Rejected alternatives:

- owner-local field

- cached owner interface

- existing SignalBus lane

- existing Vault buffer

- cold HectonEventBus hook

- no global route needed

Why this does not increase global monolith risk: route exposes fixed-size Core/Data forensic data, not gameplay truth or request/response state. Missing route disables telemetry only.

H-Phi impact expected: neutral to slightly positive by removing private ring pressure and documenting BufferID ownership. H-Phi is not acceptance proof.

Proof required before GREEN:

- Unity import and C# compile.
- Play Mode lookup smoke.
- GC 0 B/frame proof.
- Profiler sample for POST_SIMULATION flush.
- Readback of generated `Dump_SHINOBU_207.bin` after timestamped fault/slow-sample trigger.

Reviewer: pending Integrator.

Review disposition: `YELLOW / STATIC_SOURCE_ONLY`. Static route card is complete, runtime-facing proof is missing.

## Review Note

Global authority review:

Result: YELLOW

Route ID: CORE_DATA_BTREE_MMF_TELEMETRY

Owner: SHINOBU_207

Instrument: GlobalDataVault + black-box telemetry

Reason: route is narrow and fixed-size, but runtime compile/profiler/GC proof is absent.

Required fixes: clear the foreign missing-type dependency wall, then run Unity compile/import and profiler once CPU guard permits.

Proof still missing: clean C# compile, Unity Console, Play Mode, GCMonitor, profiler, dump readback.

Reviewer: pending Integrator

Date: 2026-05-20
