# SHINOBU_154 Entity Delta Compression Route Card

Date: 2026-05-20
Status: YELLOW - STATIC SOURCE PRESENT, RUNTIME PROOF BLOCKED BY DEPENDENCY COMPILE WALL
Route ID: SAVE_ENTITY_DELTA_WAL_RLE_LZ4
Owner: SHINOBU_154 / Save Persistence
Owner domain: Echelon 1 Core & Memory Infrastructure / SaveSystem Data Archivist
Owning file/system: `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`

## Problem

Dynamic entity truth for dropped items, fauna facts, base modules, and depleted resource nodes cannot be saved through JSON, object graph traversal, `BinaryFormatter`, or scene-wide `ISaveable` scans without autosave hitches.

## Why Owner-Local Data Is Insufficient

The entity delta stream must survive scene boundaries, async WAL handoff, replay, crash telemetry, and cross-domain producer ownership. Local scratch would not provide BufferID/SystemID evidence, stale-handle handling, or black-box proof for persistence failures.

## Why Direct Caller/Owner Interface Is Insufficient

Save/load scheduling crosses producer domains and the persistence pager. A direct caller interface would still need persistent native buffers, checksum evidence, and async WAL staging. `IAsyncPersistenceService` remains the narrow write/read facade; entity bytes live in Vault because they are job-visible, replay-visible, and crash-visible state.

## Instrument

- [ ] GlobalRegistry cold service/interface
- [ ] SignalBus<T> first-party broadcast
- [ ] GlobalSignals bridge/direct queue
- [ ] HectonEventBus mod/API/cold event
- [x] GlobalDataVault / IDataVault
- [x] Black-box/telemetry route

Producer phase: save scheduling / worker job chain after authoritative entity producers publish flat records to Vault.
Consumer phase: async persistence pager WAL worker for writes; load/replay worker jobs for hydration.
Cadence: dirty autosave sector cadence; continuous `ResolveWriteHz(GlobalQualityWeight)` from low write Hz to high write Hz.
Expected max events/reads per frame: one sector payload enqueue per scheduled sector save; one read ticket/copy per requested sector replay.
GlobalQualityWeight behavior: compression effort uses smooth quality/pressure curves, active LZ4 hash slots, match length, probe step, and write cadence. No binary low/high switch.

## Payload / Data Shape

Managed fields present: no in runtime DTOs/jobs.
UnityEngine.Object fields present: no in runtime DTOs/jobs.
Layout proof:

- `EntityDeltaHeaderDTO`: explicit 32B, 8-byte fields at offsets 0 and 16.
- `EntityDeltaRleStreamHeaderDTO`: explicit 16B.
- `EntityDeltaDataRecordDTO`: explicit 80B, 8-byte sector/hash fields aligned.
- `EntityDeltaBlockCounter64`: explicit 64B false-sharing-padded row.
- `EntityCompressionTelemetryEntry`: explicit 64B telemetry ring row.

Capacity:

- Vault buffers `70340..70357`, including current/baseline/delta records, dense/RLE/compressed/WAL byte buffers, counters, telemetry ring, tuning, stats, CSV scratch, and profiles.
- `TelemetryRingFrames = 300`.
- `HashTableSlots = 4096` on the normal route; short test buffers fail/clamp safely.

Overflow/failure mode:

- Delta block overflow sets `HeaderFlagFatal`.
- Non-finite local AUP offsets set fatal and prevent corrupt WAL truth.
- WAL envelope audit failure sets fatal and clears payload byte count.
- Decode/RLE validation failure sets fatal and clears decode pass.
- Native byte-range overlap or range-list capacity overflow before save or replay scheduling sets fatal through `EntityScheduleFailureJob`; `[NoAlias]` jobs are not scheduled over unverifiable Vault views.
- Short counters or missing Vault buffers fail closed.

## Telemetry / Black Box

Telemetry fields:

- sector hash
- payload hash
- frame
- full snapshot bytes
- dense delta bytes
- RLE bytes
- compressed bytes
- flags
- Burst time ms
- disk write latency ms
- global quality
- compression effort
- I/O pressure
- delta entity count

Black-box fields:

- same 300-frame telemetry ring
- dump path: `Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin`
- reason flag for disk latency spike

Profiler marker: `H8.Save.EntityDelta.ScheduleCompression` and `H8.Save.EntityDelta.ScheduleDecode` cover the public scheduling facades. Worker job runtime timing still needs Unity/Burst profiler artifacts.
GC proof required: Unity Profiler or GCMonitor 0 B/frame for save scheduling, WAL enqueue, replay scheduling, and editor-disabled runtime path.

## Shutdown / Stale Handles

Shutdown/disposal rule:

- Compressor owns no private persistent native allocation.
- Vault remains allocation/disposal owner.
- Pager owns WAL stream lifetime; `H8BinaryWorldPager` opens WAL with `FileOptions.Asynchronous | WriteThrough | SequentialScan`.

Scene unload behavior:

- Vault buffers must be released by `GlobalDataVault` owner.
- Pager shutdown must drain/stop worker and close streams.

Stale-handle behavior:

- `TryResolveVaultBuffers` resolves fresh handles before scheduling.
- Public helper paths fail when buffers are missing, too short, or ticket payload type is not `EntityDeltaRle`.
- Save/replay scheduling rejects overlapping native byte ranges before Burst jobs consume them.

## Rejected Alternatives

- [x] owner-local field: rejected because payload crosses save/replay/crash boundaries.
- [x] cached owner interface: rejected for byte storage; retained only as `IAsyncPersistenceService` pager facade.
- [x] existing SignalBus lane: rejected because persistence payload is not broadcast.
- [x] existing Vault buffer: rejected because replay source must not alias RLE decode destination; added `SaveEntityDeltaWalPayloadBytes`.
- [x] cold HectonEventBus hook: rejected because this is first-party persistence, not mod/API.
- [ ] no global route needed.

## Monolith Risk

This route does not add a new registry slot, signal lane, event bus route, or sibling assembly reference. It adds owner-labeled SavePersistence Vault buffers and uses the existing `IAsyncPersistenceService` interface. The compile-wall debt remains the existing root Core asmdef shape, not a new SHINOBU_154 reference.

H-Phi impact expected:

- Positive for persistence because cross-domain save/replay bytes are Vault-owned.
- Not accepted as proof. Runtime acceptance still requires Unity import, Console, Play Mode, profiler/GC, WAL replay, and unload baseline artifacts.

Runtime proof required before acceptance:

- Unity import includes SHINOBU_154 new C# assets. Current batchmode log confirms import saw the assets but project-wide compile is blocked by non-SHINOBU domains.
- Unity Console clean for SaveSystem/Core.
- Burst compile succeeds for all 17 jobs.
- Profiler/GCMonitor confirms 0 B hot-path scheduling/enqueue.
- WAL write/read/replay sector test verifies checksum, RLE/LZ4 decode, endian marker, finite local offsets, pre-schedule alias rejection, and non-overlap on valid Vault buffers.
- Telemetry ring dump is produced and can be read back.

Reviewer: pending integrator
Review result: YELLOW
Reason: route card complete as static documentation, but runtime-facing proof artifacts are blocked by compile errors in other owner domains.
