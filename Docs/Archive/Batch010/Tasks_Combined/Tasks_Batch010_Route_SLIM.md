# Tasks_Batch010 Route SLIM
Scope: Batch010
Source: C:\hades\Hecton8\Docs\Archive\Batch010\Tasks
FileCount: 15
Separator: ===== FILE: name =====
SnapshotNote: Combined SLIM sections are archival snapshots. For SHINOBU_155 after 2026-05-21 Loop 79, use direct mirror Docs/Archive/Batch010/Tasks/Route_SHINOBU_155_Respawn.md.

===== FILE: Route_SHINOBU_145_Metabolism.md =====
Route_SHINOBU_145_Metabolism
Date: 2026-05-19
Status: PENDING VERIFICATION
Route ID: SHINOBU_145_METABOLISM_NATIVE_STATE
Owner: SHINOBU_145
Owner domain: ECHELON 5 COMBAT & SURVIVAL PHYSIOLOGY / DIET & METABOLISM
Owning file/system: `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`
Problem: Authoritative metabolism state must be job-visible, replayable, crash-dumpable, and shared with other E5/E1 consumers without managed collections.
Why owner-local data is insufficient: AI/creature spawners, thermal grids, combat routing, and UI/debug tools need stable native state route and not per-object state.
Why direct caller/owner interface is insufficient: Burst jobs need contiguous native buffers and signal writers, not MonoBehaviour references.
Instrument:
[x] GlobalRegistry cold service/interface
[x] SignalBus first-party broadcast
[ ] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase: SlowTick schedules Burst jobs; late-frame swap completes, publishes staged signals, and publishes presentation scalar. Cold boot initializes every resolved Vault row and staged signal slot through `InitInactiveMetabolismJob` before any mock rows are hydrated.
Consumer phase: Combat/physiology signal drains after simulation; editor/UI reads telemetry outside hot simulation.
Cadence: Continuous `math.lerp(0.5f, 3.0f, 1.0f - GlobalQualityWeight)`.
Expected max events/reads per frame: 5000 entity reads per scheduled SlowTick; worst-case starvation/dehydration/hypothermia signals stage into three fixed slots per row and are published after job completion through SignalBus `TryPush`; one toxic combat slot per row; one telemetry entry per completed tick.
GlobalQualityWeight behavior: No entity drops. Cadence stretches and dynamic `dt` preserves integrated totals.
External readback: chemical toxin samples consume SHINOBU_138 published Vault buffers `71152` published `float4` grid, `71161` tuning, `71162` telemetry ring, and `71163` telemetry cursor. Overlay buffer `71153` is sampled only when it can be locked and resolved; missing overlay does not disable published-grid toxin sampling. These are readback buffers only; SHINOBU_145 does not own or mutate chemical truth.
Payload/data shape: Explicit unmanaged DTOs. `MetabolicStateDTO` is 32 bytes with raw fields only. Staged signal buffers reuse existing 64-byte Core `PhysiologyStateSignal` and `CombatDamageSignal` DTOs.
Managed fields present: no
UnityEngine.Object fields present: no
Layout proof: editor validator checks `UnsafeUtility.SizeOf` and field offsets. Chemical mirror DTOs are explicit 64-byte readback mirrors of SHINOBU_138 tuning/telemetry layout.
Import hygiene: stable `.meta` GUIDs exist for all new SHINOBU_145 C# assets.
Capacity: 5000 default mock entities, configurable Vault capacity, 300 telemetry entries, 128 species rules, three staged physiology signal slots per entity, and one staged combat damage slot per entity. Rows with `EntityHashID=0` are inactive and skipped by hot integrator/telemetry.
Overflow/failure mode: SignalBus overflow follows existing lane policy during post-completion `TryPush`; staged signal slots are overwritten/cleared by next scheduled integrator pass; telemetry ring overwrites oldest; NaN sets telemetry flags and dumps binary ring.
Telemetry fields: frame, entity count, average core temperature, starvation count, dehydration count, toxicity count, job microseconds, flags.
Black-box fields: frame, aggregate state, event counts, quality, dt, NaN flags.
Profiler marker: `ShinobuMetabolism.Schedule`, `ShinobuMetabolism.Complete`.
GC proof required: Profiler/GCMonitor proof before GREEN. Static hot path contains no managed allocations by design; remaining `new` source hits are static/cold IO/cold graphics buffer/dump paths.
Shutdown/disposal rule: Vault handles are released by owner teardown; active jobs are reclaimed through Core `DispatcherJobFence.TryComplete`, with non-forced runtime completion only after `IsCompleted` and forced completion limited to teardown/editor/cold bootstrap.
Scene unload behavior: Runtime unregisters from tick dispatcher and does not spawn objects in teardown.
Stale-handle behavior: Vault handles are resolved per schedule/complete boundary and fail closed if invalid.
Rejected alternatives:
[x] owner-local field
[x] cached owner interface
[x] existing SignalBus lane
[x] existing Vault buffer
[x] cold HectonEventBus hook
[ ] no global route needed
Why this does not increase global monolith risk: It reuses existing physiology/combat signal lanes, stages job outputs in owner-local Vault buffers before publishing, queries thermodynamics only through `IThermodynamicsService`, and samples chemical toxin only through SHINOBU_138's documented Vault readback buffers. No Core enum, signal layout, new chemical service, or direct `ChemicalInfluenceGrid` reference is added.
H-Phi impact expected: None claimed.
Runtime proof required before acceptance: Unity import, compile, Play Mode SlowTick run, profiler/GC 0 B hot path, telemetry dump test.
Reviewer: Integrator
Status: PROPOSED
Review disposition: YELLOW
Reason: Static route card, inactive-slot vaccination, chemical readback, dispatcher-fence routing, optional overlay fallback, and import hygiene are complete enough to continue verification. Runtime/profiler proof is not attached, so GREEN is forbidden.
===== END FILE: Route_SHINOBU_145_Metabolism.md =====

===== FILE: Route_SHINOBU_154_EntityDeltaCompression.md =====
SHINOBU_154 Entity Delta Compression Route Card
Date: 2026-05-20
Status: YELLOW - STATIC SOURCE PRESENT, RUNTIME PROOF BLOCKED BY DEPENDENCY COMPILE WALL
Route ID: SAVE_ENTITY_DELTA_WAL_RLE_LZ4
Owner: SHINOBU_154 / Save Persistence
Owner domain: Echelon 1 Core & Memory Infrastructure / SaveSystem Data Archivist
Owning file/system: `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`
Problem
Dynamic entity truth for dropped items, fauna facts, base modules, and depleted resource nodes cannot be saved through JSON, object graph traversal, `BinaryFormatter`, or scene-wide `ISaveable` scans without autosave hitches.
Why Owner-Local Data Is Insufficient
entity delta stream must survive scene boundaries, async WAL handoff, replay, crash telemetry, and cross-domain producer ownership. Local scratch would not provide BufferID/SystemID evidence, stale-handle handling, or black-box proof for persistence failures.
Why Direct Caller/Owner Interface Is Insufficient
Save/load scheduling crosses producer domains and persistence pager. direct caller interface would still need persistent native buffers, checksum evidence, and async WAL staging. `IAsyncPersistenceService` remains narrow write/read facade; entity bytes live in Vault because they are job-visible, replay-visible, and crash-visible state.
Instrument
[ ] GlobalRegistry cold service/interface
[ ] SignalBus first-party broadcast
[ ] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase: save scheduling / worker job chain after authoritative entity producers publish flat records to Vault.
Consumer phase: async persistence pager WAL worker for writes; load/replay worker jobs for hydration.
Cadence: dirty autosave sector cadence; continuous `ResolveWriteHz(GlobalQualityWeight)` from low write Hz to high write Hz.
Expected max events/reads per frame: one sector payload enqueue per scheduled sector save; one read ticket/copy per requested sector replay.
GlobalQualityWeight behavior: compression effort uses smooth quality/pressure curves, active LZ4 hash slots, match length, probe step, and write cadence. No binary low/high switch.
Payload / Data Shape
Managed fields present: no in runtime DTOs/jobs.
UnityEngine.Object fields present: no in runtime DTOs/jobs.
Layout proof:
`EntityDeltaHeaderDTO`: explicit 32B, 8-byte fields at offsets 0 and 16.
`EntityDeltaRleStreamHeaderDTO`: explicit 16B.
`EntityDeltaDataRecordDTO`: explicit 80B, 8-byte sector/hash fields aligned.
`EntityDeltaBlockCounter64`: explicit 64B false-sharing-padded row.
`EntityCompressionTelemetryEntry`: explicit 64B telemetry ring row.
Capacity:
Vault buffers `70340..70357`, including current/baseline/delta records, dense/RLE/compressed/WAL byte buffers, counters, telemetry ring, tuning, stats, CSV scratch, and profiles.
`TelemetryRingFrames = 300`.
`HashTableSlots = 4096` on normal route; short test buffers fail/clamp safely.
Overflow/failure mode:
Delta block overflow sets `HeaderFlagFatal`.
Non-finite local AUP offsets set fatal and prevent corrupt WAL truth.
WAL envelope audit failure sets fatal and clears payload byte count.
Decode/RLE validation failure sets fatal and clears decode pass.
Native byte-range overlap or range-list capacity overflow before save or replay scheduling sets fatal through `EntityScheduleFailureJob`; `[NoAlias]` jobs are not scheduled over unverifiable Vault views.
`NativeDisableParallelForRestriction` is used only on partitioned mock/prune/extract writers and now has source-local three-paragraph safety proofs documenting exact per-index, per-block, and per-delta-range ownership invariants.
Short counters or missing Vault buffers fail closed.
Telemetry / Black Box
Telemetry fields:
sector hash
payload hash
frame
full snapshot bytes
dense delta bytes
RLE bytes
compressed bytes
flags
Burst time ms
disk write latency ms
global quality
compression effort
I/O pressure
delta entity count
Black-box fields:
same 300-frame telemetry ring
dump path: `Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin`
reason flag for disk latency spike
Profiler marker: `H8.Save.EntityDelta.ScheduleCompression` and `H8.Save.EntityDelta.ScheduleDecode` cover public scheduling facades. Worker job runtime timing still needs Unity/Burst profiler artifacts.
GC proof required: Unity Profiler or GCMonitor 0 B/frame for save scheduling, WAL enqueue, replay scheduling, and editor-disabled runtime path.
Shutdown / Stale Handles
Shutdown/disposal rule:
Compressor owns no private persistent native allocation.
Vault remains allocation/disposal owner.
Pager owns WAL stream lifetime; `H8BinaryWorldPager` opens WAL with `FileOptions.Asynchronous WriteThrough SequentialScan`.
Scene unload behavior:
Vault buffers must be released by `GlobalDataVault` owner.
Pager shutdown must drain/stop worker and close streams.
Stale-handle behavior:
`TryResolveVaultBuffers` resolves fresh handles before scheduling.
Public helper paths fail when buffers are missing, too short, or ticket payload type is not `EntityDeltaRle`.
Save/replay scheduling rejects overlapping native byte ranges before Burst jobs consume them.
Rejected Alternatives
[x] owner-local field: rejected because payload crosses save/replay/crash boundaries.
[x] cached owner interface: rejected for byte storage; retained only as `IAsyncPersistenceService` pager facade.
[x] existing SignalBus lane: rejected because persistence payload is not broadcast.
[x] existing Vault buffer: rejected because replay source must not alias RLE decode destination; added `SaveEntityDeltaWalPayloadBytes`.
[x] cold HectonEventBus hook: rejected because this is first-party persistence, not mod/API.
[ ] no global route needed.
Monolith Risk
This route does not add new registry slot, signal lane, event bus route, or sibling assembly reference. It adds owner-labeled SavePersistence Vault buffers and uses existing `IAsyncPersistenceService` interface. compile-wall debt remains existing root Core asmdef shape, not new SHINOBU_154 reference.
H-Phi impact expected:
Positive for persistence because cross-domain save/replay bytes are Vault-owned.
Not accepted as proof. Runtime acceptance still requires Unity import, Console, Play Mode, profiler/GC, WAL replay, and unload baseline artifacts.
Runtime proof required before acceptance:
Unity import includes SHINOBU_154 new C# assets. Current batchmode log confirms import saw assets but project-wide compile is blocked by non-SHINOBU domains.
Unity Console clean for SaveSystem/Core.
Burst compile succeeds for all 17 jobs.
Profiler/GCMonitor confirms 0 B hot-path scheduling/enqueue.
WAL write/read/replay sector test verifies checksum, RLE/LZ4 decode, endian marker, finite local offsets, pre-schedule alias rejection, and non-overlap on valid Vault buffers.
Telemetry ring dump is produced and can be read back.
Reviewer: pending integrator
Review result: YELLOW
Reason: route card complete as static documentation, but runtime-facing proof artifacts are blocked by compile errors in other owner domains.
===== END FILE: Route_SHINOBU_154_EntityDeltaCompression.md =====

===== FILE: Route_SHINOBU_154_EntityDeltaCompression__final46.md =====
SHINOBU_154 Entity Delta Compression Route Card
Date: 2026-05-20
Status: YELLOW - STATIC SOURCE PRESENT, RUNTIME PROOF BLOCKED BY DEPENDENCY COMPILE WALL
Route ID: SAVE_ENTITY_DELTA_WAL_RLE_LZ4
Owner: SHINOBU_154 / Save Persistence
Owner domain: Echelon 1 Core & Memory Infrastructure / SaveSystem Data Archivist
Owning file/system: `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`
Problem
Dynamic entity truth for dropped items, fauna facts, base modules, and depleted resource nodes cannot be saved through JSON, object graph traversal, `BinaryFormatter`, or scene-wide `ISaveable` scans without autosave hitches.
Why Owner-Local Data Is Insufficient
entity delta stream must survive scene boundaries, async WAL handoff, replay, crash telemetry, and cross-domain producer ownership. Local scratch would not provide BufferID/SystemID evidence, stale-handle handling, or black-box proof for persistence failures.
Why Direct Caller/Owner Interface Is Insufficient
Save/load scheduling crosses producer domains and persistence pager. direct caller interface would still need persistent native buffers, checksum evidence, and async WAL staging. `IAsyncPersistenceService` remains narrow write/read facade; entity bytes live in Vault because they are job-visible, replay-visible, and crash-visible state.
Instrument
[ ] GlobalRegistry cold service/interface
[ ] SignalBus first-party broadcast
[ ] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase: save scheduling / worker job chain after authoritative entity producers publish flat records to Vault.
Consumer phase: async persistence pager WAL worker for writes; load/replay worker jobs for hydration.
Cadence: dirty autosave sector cadence; continuous `ResolveWriteHz(GlobalQualityWeight)` from low write Hz to high write Hz.
Expected max events/reads per frame: one sector payload enqueue per scheduled sector save; one read ticket/copy per requested sector replay.
GlobalQualityWeight behavior: compression effort uses smooth quality/pressure curves, active LZ4 hash slots, match length, probe step, and write cadence. No binary low/high switch.
Payload / Data Shape
Managed fields present: no in runtime DTOs/jobs.
UnityEngine.Object fields present: no in runtime DTOs/jobs.
Layout proof:
`EntityDeltaHeaderDTO`: explicit 32B, 8-byte fields at offsets 0 and 16.
`EntityDeltaRleStreamHeaderDTO`: explicit 16B.
`EntityDeltaDataRecordDTO`: explicit 80B, 8-byte sector/hash fields aligned.
`EntityDeltaBlockCounter64`: explicit 64B false-sharing-padded row.
`EntityCompressionTelemetryEntry`: explicit 64B telemetry ring row.
Capacity:
Vault buffers `70340..70357`, including current/baseline/delta records, dense/RLE/compressed/WAL byte buffers, counters, telemetry ring, tuning, stats, CSV scratch, and profiles.
`TelemetryRingFrames = 300`.
`HashTableSlots = 4096` on normal route; short test buffers fail/clamp safely.
Overflow/failure mode:
Delta block overflow sets `HeaderFlagFatal`.
Non-finite local AUP offsets set fatal and prevent corrupt WAL truth.
WAL envelope audit failure sets fatal and clears payload byte count.
Decode/RLE validation failure sets fatal and clears decode pass.
Native byte-range overlap or range-list capacity overflow before save or replay scheduling sets fatal through `EntityScheduleFailureJob`; `[NoAlias]` jobs are not scheduled over unverifiable Vault views.
`NativeDisableParallelForRestriction` is used only on partitioned mock/prune/extract writers and now has source-local three-paragraph safety proofs documenting exact per-index, per-block, and per-delta-range ownership invariants.
Short counters or missing Vault buffers fail closed.
Telemetry / Black Box
Telemetry fields:
sector hash
payload hash
frame
full snapshot bytes
dense delta bytes
RLE bytes
compressed bytes
flags
Burst time ms
disk write latency ms
global quality
compression effort
I/O pressure
delta entity count
Black-box fields:
same 300-frame telemetry ring
dump path: `Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin`
reason flag for disk latency spike
Profiler marker: `H8.Save.EntityDelta.ScheduleCompression` and `H8.Save.EntityDelta.ScheduleDecode` cover public scheduling facades. Worker job runtime timing still needs Unity/Burst profiler artifacts.
GC proof required: Unity Profiler or GCMonitor 0 B/frame for save scheduling, WAL enqueue, replay scheduling, and editor-disabled runtime path.
Shutdown / Stale Handles
Shutdown/disposal rule:
Compressor owns no private persistent native allocation.
Vault remains allocation/disposal owner.
Pager owns WAL stream lifetime; `H8BinaryWorldPager` opens WAL with `FileOptions.Asynchronous WriteThrough SequentialScan`.
Scene unload behavior:
Vault buffers must be released by `GlobalDataVault` owner.
Pager shutdown must drain/stop worker and close streams.
Stale-handle behavior:
`TryResolveVaultBuffers` resolves fresh handles before scheduling.
Public helper paths fail when buffers are missing, too short, or ticket payload type is not `EntityDeltaRle`.
Save/replay scheduling rejects overlapping native byte ranges before Burst jobs consume them.
Rejected Alternatives
[x] owner-local field: rejected because payload crosses save/replay/crash boundaries.
[x] cached owner interface: rejected for byte storage; retained only as `IAsyncPersistenceService` pager facade.
[x] existing SignalBus lane: rejected because persistence payload is not broadcast.
[x] existing Vault buffer: rejected because replay source must not alias RLE decode destination; added `SaveEntityDeltaWalPayloadBytes`.
[x] cold HectonEventBus hook: rejected because this is first-party persistence, not mod/API.
[ ] no global route needed.
Monolith Risk
This route does not add new registry slot, signal lane, event bus route, or sibling assembly reference. It adds owner-labeled SavePersistence Vault buffers and uses existing `IAsyncPersistenceService` interface. compile-wall debt remains existing root Core asmdef shape, not new SHINOBU_154 reference.
H-Phi impact expected:
Positive for persistence because cross-domain save/replay bytes are Vault-owned.
Not accepted as proof. Runtime acceptance still requires Unity import, Console, Play Mode, profiler/GC, WAL replay, and unload baseline artifacts.
Runtime proof required before acceptance:
Unity import includes SHINOBU_154 new C# assets. Current batchmode log confirms import saw assets but project-wide compile is blocked by non-SHINOBU domains.
Unity Console clean for SaveSystem/Core.
Burst compile succeeds for all 17 jobs.
Profiler/GCMonitor confirms 0 B hot-path scheduling/enqueue.
WAL write/read/replay sector test verifies checksum, RLE/LZ4 decode, endian marker, finite local offsets, pre-schedule alias rejection, and non-overlap on valid Vault buffers.
Telemetry ring dump is produced and can be read back.
Reviewer: pending integrator
Review result: YELLOW
Reason: route card complete as static documentation, but runtime-facing proof artifacts are blocked by compile errors in other owner domains.
===== END FILE: Route_SHINOBU_154_EntityDeltaCompression__final46.md =====

===== FILE: Route_SHINOBU_154_EntityDeltaCompression__instant99.md =====
SHINOBU_154 Entity Delta Compression Route Card
Date: 2026-05-20
Status: YELLOW - STATIC SOURCE PRESENT, RUNTIME PROOF BLOCKED BY DEPENDENCY COMPILE WALL
Route ID: SAVE_ENTITY_DELTA_WAL_RLE_LZ4
Owner: SHINOBU_154 / Save Persistence
Owner domain: Echelon 1 Core & Memory Infrastructure / SaveSystem Data Archivist
Owning file/system: `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`
Problem
Dynamic entity truth for dropped items, fauna facts, base modules, and depleted resource nodes cannot be saved through JSON, object graph traversal, `BinaryFormatter`, or scene-wide `ISaveable` scans without autosave hitches.
Why Owner-Local Data Is Insufficient
entity delta stream must survive scene boundaries, async WAL handoff, replay, crash telemetry, and cross-domain producer ownership. Local scratch would not provide BufferID/SystemID evidence, stale-handle handling, or black-box proof for persistence failures.
Why Direct Caller/Owner Interface Is Insufficient
Save/load scheduling crosses producer domains and persistence pager. direct caller interface would still need persistent native buffers, checksum evidence, and async WAL staging. `IAsyncPersistenceService` remains narrow write/read facade; entity bytes live in Vault because they are job-visible, replay-visible, and crash-visible state.
Instrument
[ ] GlobalRegistry cold service/interface
[ ] SignalBus first-party broadcast
[ ] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase: save scheduling / worker job chain after authoritative entity producers publish flat records to Vault.
Consumer phase: async persistence pager WAL worker for writes; load/replay worker jobs for hydration.
Cadence: dirty autosave sector cadence; continuous `ResolveWriteHz(GlobalQualityWeight)` from low write Hz to high write Hz.
Expected max events/reads per frame: one sector payload enqueue per scheduled sector save; one read ticket/copy per requested sector replay.
GlobalQualityWeight behavior: compression effort uses smooth quality/pressure curves, active LZ4 hash slots, match length, probe step, and write cadence. No binary low/high switch.
Payload / Data Shape
Managed fields present: no in runtime DTOs/jobs.
UnityEngine.Object fields present: no in runtime DTOs/jobs.
Layout proof:
`EntityDeltaHeaderDTO`: explicit 32B, 8-byte fields at offsets 0 and 16.
`EntityDeltaRleStreamHeaderDTO`: explicit 16B.
`EntityDeltaDataRecordDTO`: explicit 80B, 8-byte sector/hash fields aligned.
`EntityDeltaBlockCounter64`: explicit 64B false-sharing-padded row.
`EntityCompressionTelemetryEntry`: explicit 64B telemetry ring row.
Capacity:
Vault buffers `70340..70357`, including current/baseline/delta records, dense/RLE/compressed/WAL byte buffers, counters, telemetry ring, tuning, stats, CSV scratch, and profiles.
`TelemetryRingFrames = 300`.
`HashTableSlots = 4096` on normal route; short test buffers fail/clamp safely.
Overflow/failure mode:
Delta block overflow sets `HeaderFlagFatal`.
Non-finite local AUP offsets set fatal and prevent corrupt WAL truth.
WAL envelope audit failure sets fatal and clears payload byte count.
Decode/RLE validation failure sets fatal and clears decode pass.
Native byte-range overlap or range-list capacity overflow before save or replay scheduling sets fatal through `EntityScheduleFailureJob`; `[NoAlias]` jobs are not scheduled over unverifiable Vault views.
`NativeDisableParallelForRestriction` is used only on partitioned mock/prune/extract writers and now has source-local three-paragraph safety proofs documenting exact per-index, per-block, and per-delta-range ownership invariants.
Short counters or missing Vault buffers fail closed.
Telemetry / Black Box
Telemetry fields:
sector hash
payload hash
frame
full snapshot bytes
dense delta bytes
RLE bytes
compressed bytes
flags
Burst time ms
disk write latency ms
global quality
compression effort
I/O pressure
delta entity count
Black-box fields:
same 300-frame telemetry ring
dump path: `Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin`
reason flag for disk latency spike
Profiler marker: `H8.Save.EntityDelta.ScheduleCompression` and `H8.Save.EntityDelta.ScheduleDecode` cover public scheduling facades. Worker job runtime timing still needs Unity/Burst profiler artifacts.
GC proof required: Unity Profiler or GCMonitor 0 B/frame for save scheduling, WAL enqueue, replay scheduling, and editor-disabled runtime path.
Shutdown / Stale Handles
Shutdown/disposal rule:
Compressor owns no private persistent native allocation.
Vault remains allocation/disposal owner.
Pager owns WAL stream lifetime; `H8BinaryWorldPager` opens WAL with `FileOptions.Asynchronous WriteThrough SequentialScan`.
Scene unload behavior:
Vault buffers must be released by `GlobalDataVault` owner.
Pager shutdown must drain/stop worker and close streams.
Stale-handle behavior:
`TryResolveVaultBuffers` resolves fresh handles before scheduling.
Public helper paths fail when buffers are missing, too short, or ticket payload type is not `EntityDeltaRle`.
Save/replay scheduling rejects overlapping native byte ranges before Burst jobs consume them.
Rejected Alternatives
[x] owner-local field: rejected because payload crosses save/replay/crash boundaries.
[x] cached owner interface: rejected for byte storage; retained only as `IAsyncPersistenceService` pager facade.
[x] existing SignalBus lane: rejected because persistence payload is not broadcast.
[x] existing Vault buffer: rejected because replay source must not alias RLE decode destination; added `SaveEntityDeltaWalPayloadBytes`.
[x] cold HectonEventBus hook: rejected because this is first-party persistence, not mod/API.
[ ] no global route needed.
Monolith Risk
This route does not add new registry slot, signal lane, event bus route, or sibling assembly reference. It adds owner-labeled SavePersistence Vault buffers and uses existing `IAsyncPersistenceService` interface. compile-wall debt remains existing root Core asmdef shape, not new SHINOBU_154 reference.
H-Phi impact expected:
Positive for persistence because cross-domain save/replay bytes are Vault-owned.
Not accepted as proof. Runtime acceptance still requires Unity import, Console, Play Mode, profiler/GC, WAL replay, and unload baseline artifacts.
Runtime proof required before acceptance:
Unity import includes SHINOBU_154 new C# assets. Current batchmode log confirms import saw assets but project-wide compile is blocked by non-SHINOBU domains.
Unity Console clean for SaveSystem/Core.
Burst compile succeeds for all 17 jobs.
Profiler/GCMonitor confirms 0 B hot-path scheduling/enqueue.
WAL write/read/replay sector test verifies checksum, RLE/LZ4 decode, endian marker, finite local offsets, pre-schedule alias rejection, and non-overlap on valid Vault buffers.
Telemetry ring dump is produced and can be read back.
Reviewer: pending integrator
Review result: YELLOW
Reason: route card complete as static documentation, but runtime-facing proof artifacts are blocked by compile errors in other owner domains.
===== END FILE: Route_SHINOBU_154_EntityDeltaCompression__instant99.md =====

===== FILE: Route_SHINOBU_154_EntityDeltaCompression__late21.md =====
SHINOBU_154 Entity Delta Compression Route Card
Date: 2026-05-20
Status: YELLOW - STATIC SOURCE PRESENT, RUNTIME PROOF BLOCKED BY DEPENDENCY COMPILE WALL
Route ID: SAVE_ENTITY_DELTA_WAL_RLE_LZ4
Owner: SHINOBU_154 / Save Persistence
Owner domain: Echelon 1 Core & Memory Infrastructure / SaveSystem Data Archivist
Owning file/system: `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`
Problem
Dynamic entity truth for dropped items, fauna facts, base modules, and depleted resource nodes cannot be saved through JSON, object graph traversal, `BinaryFormatter`, or scene-wide `ISaveable` scans without autosave hitches.
Why Owner-Local Data Is Insufficient
entity delta stream must survive scene boundaries, async WAL handoff, replay, crash telemetry, and cross-domain producer ownership. Local scratch would not provide BufferID/SystemID evidence, stale-handle handling, or black-box proof for persistence failures.
Why Direct Caller/Owner Interface Is Insufficient
Save/load scheduling crosses producer domains and persistence pager. direct caller interface would still need persistent native buffers, checksum evidence, and async WAL staging. `IAsyncPersistenceService` remains narrow write/read facade; entity bytes live in Vault because they are job-visible, replay-visible, and crash-visible state.
Instrument
[ ] GlobalRegistry cold service/interface
[ ] SignalBus first-party broadcast
[ ] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase: save scheduling / worker job chain after authoritative entity producers publish flat records to Vault.
Consumer phase: async persistence pager WAL worker for writes; load/replay worker jobs for hydration.
Cadence: dirty autosave sector cadence; continuous `ResolveWriteHz(GlobalQualityWeight)` from low write Hz to high write Hz.
Expected max events/reads per frame: one sector payload enqueue per scheduled sector save; one read ticket/copy per requested sector replay.
GlobalQualityWeight behavior: compression effort uses smooth quality/pressure curves, active LZ4 hash slots, match length, probe step, and write cadence. No binary low/high switch.
Payload / Data Shape
Managed fields present: no in runtime DTOs/jobs.
UnityEngine.Object fields present: no in runtime DTOs/jobs.
Layout proof:
`EntityDeltaHeaderDTO`: explicit 32B, 8-byte fields at offsets 0 and 16.
`EntityDeltaRleStreamHeaderDTO`: explicit 16B.
`EntityDeltaDataRecordDTO`: explicit 80B, 8-byte sector/hash fields aligned.
`EntityDeltaBlockCounter64`: explicit 64B false-sharing-padded row.
`EntityCompressionTelemetryEntry`: explicit 64B telemetry ring row.
Capacity:
Vault buffers `70340..70357`, including current/baseline/delta records, dense/RLE/compressed/WAL byte buffers, counters, telemetry ring, tuning, stats, CSV scratch, and profiles.
`TelemetryRingFrames = 300`.
`HashTableSlots = 4096` on normal route; short test buffers fail/clamp safely.
Overflow/failure mode:
Delta block overflow sets `HeaderFlagFatal`.
Non-finite local AUP offsets set fatal and prevent corrupt WAL truth.
WAL envelope audit failure sets fatal and clears payload byte count.
Decode/RLE validation failure sets fatal and clears decode pass.
Native byte-range overlap or range-list capacity overflow before save or replay scheduling sets fatal through `EntityScheduleFailureJob`; `[NoAlias]` jobs are not scheduled over unverifiable Vault views.
Short counters or missing Vault buffers fail closed.
Telemetry / Black Box
Telemetry fields:
sector hash
payload hash
frame
full snapshot bytes
dense delta bytes
RLE bytes
compressed bytes
flags
Burst time ms
disk write latency ms
global quality
compression effort
I/O pressure
delta entity count
Black-box fields:
same 300-frame telemetry ring
dump path: `Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin`
reason flag for disk latency spike
Profiler marker: `H8.Save.EntityDelta.ScheduleCompression` and `H8.Save.EntityDelta.ScheduleDecode` cover public scheduling facades. Worker job runtime timing still needs Unity/Burst profiler artifacts.
GC proof required: Unity Profiler or GCMonitor 0 B/frame for save scheduling, WAL enqueue, replay scheduling, and editor-disabled runtime path.
Shutdown / Stale Handles
Shutdown/disposal rule:
Compressor owns no private persistent native allocation.
Vault remains allocation/disposal owner.
Pager owns WAL stream lifetime; `H8BinaryWorldPager` opens WAL with `FileOptions.Asynchronous WriteThrough SequentialScan`.
Scene unload behavior:
Vault buffers must be released by `GlobalDataVault` owner.
Pager shutdown must drain/stop worker and close streams.
Stale-handle behavior:
`TryResolveVaultBuffers` resolves fresh handles before scheduling.
Public helper paths fail when buffers are missing, too short, or ticket payload type is not `EntityDeltaRle`.
Save/replay scheduling rejects overlapping native byte ranges before Burst jobs consume them.
Rejected Alternatives
[x] owner-local field: rejected because payload crosses save/replay/crash boundaries.
[x] cached owner interface: rejected for byte storage; retained only as `IAsyncPersistenceService` pager facade.
[x] existing SignalBus lane: rejected because persistence payload is not broadcast.
[x] existing Vault buffer: rejected because replay source must not alias RLE decode destination; added `SaveEntityDeltaWalPayloadBytes`.
[x] cold HectonEventBus hook: rejected because this is first-party persistence, not mod/API.
[ ] no global route needed.
Monolith Risk
This route does not add new registry slot, signal lane, event bus route, or sibling assembly reference. It adds owner-labeled SavePersistence Vault buffers and uses existing `IAsyncPersistenceService` interface. compile-wall debt remains existing root Core asmdef shape, not new SHINOBU_154 reference.
H-Phi impact expected:
Positive for persistence because cross-domain save/replay bytes are Vault-owned.
Not accepted as proof. Runtime acceptance still requires Unity import, Console, Play Mode, profiler/GC, WAL replay, and unload baseline artifacts.
Runtime proof required before acceptance:
Unity import includes SHINOBU_154 new C# assets. Current batchmode log confirms import saw assets but project-wide compile is blocked by non-SHINOBU domains.
Unity Console clean for SaveSystem/Core.
Burst compile succeeds for all 17 jobs.
Profiler/GCMonitor confirms 0 B hot-path scheduling/enqueue.
WAL write/read/replay sector test verifies checksum, RLE/LZ4 decode, endian marker, finite local offsets, pre-schedule alias rejection, and non-overlap on valid Vault buffers.
Telemetry ring dump is produced and can be read back.
Reviewer: pending integrator
Review result: YELLOW
Reason: route card complete as static documentation, but runtime-facing proof artifacts are blocked by compile errors in other owner domains.
===== END FILE: Route_SHINOBU_154_EntityDeltaCompression__late21.md =====

===== FILE: Route_SHINOBU_154_EntityDeltaCompression__watch22.md =====
SHINOBU_154 Entity Delta Compression Route Card
Date: 2026-05-20
Status: YELLOW - STATIC SOURCE PRESENT, RUNTIME PROOF BLOCKED BY DEPENDENCY COMPILE WALL
Route ID: SAVE_ENTITY_DELTA_WAL_RLE_LZ4
Owner: SHINOBU_154 / Save Persistence
Owner domain: Echelon 1 Core & Memory Infrastructure / SaveSystem Data Archivist
Owning file/system: `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`
Problem
Dynamic entity truth for dropped items, fauna facts, base modules, and depleted resource nodes cannot be saved through JSON, object graph traversal, `BinaryFormatter`, or scene-wide `ISaveable` scans without autosave hitches.
Why Owner-Local Data Is Insufficient
entity delta stream must survive scene boundaries, async WAL handoff, replay, crash telemetry, and cross-domain producer ownership. Local scratch would not provide BufferID/SystemID evidence, stale-handle handling, or black-box proof for persistence failures.
Why Direct Caller/Owner Interface Is Insufficient
Save/load scheduling crosses producer domains and persistence pager. direct caller interface would still need persistent native buffers, checksum evidence, and async WAL staging. `IAsyncPersistenceService` remains narrow write/read facade; entity bytes live in Vault because they are job-visible, replay-visible, and crash-visible state.
Instrument
[ ] GlobalRegistry cold service/interface
[ ] SignalBus first-party broadcast
[ ] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase: save scheduling / worker job chain after authoritative entity producers publish flat records to Vault.
Consumer phase: async persistence pager WAL worker for writes; load/replay worker jobs for hydration.
Cadence: dirty autosave sector cadence; continuous `ResolveWriteHz(GlobalQualityWeight)` from low write Hz to high write Hz.
Expected max events/reads per frame: one sector payload enqueue per scheduled sector save; one read ticket/copy per requested sector replay.
GlobalQualityWeight behavior: compression effort uses smooth quality/pressure curves, active LZ4 hash slots, match length, probe step, and write cadence. No binary low/high switch.
Payload / Data Shape
Managed fields present: no in runtime DTOs/jobs.
UnityEngine.Object fields present: no in runtime DTOs/jobs.
Layout proof:
`EntityDeltaHeaderDTO`: explicit 32B, 8-byte fields at offsets 0 and 16.
`EntityDeltaRleStreamHeaderDTO`: explicit 16B.
`EntityDeltaDataRecordDTO`: explicit 80B, 8-byte sector/hash fields aligned.
`EntityDeltaBlockCounter64`: explicit 64B false-sharing-padded row.
`EntityCompressionTelemetryEntry`: explicit 64B telemetry ring row.
Capacity:
Vault buffers `70340..70357`, including current/baseline/delta records, dense/RLE/compressed/WAL byte buffers, counters, telemetry ring, tuning, stats, CSV scratch, and profiles.
`TelemetryRingFrames = 300`.
`HashTableSlots = 4096` on normal route; short test buffers fail/clamp safely.
Overflow/failure mode:
Delta block overflow sets `HeaderFlagFatal`.
Non-finite local AUP offsets set fatal and prevent corrupt WAL truth.
WAL envelope audit failure sets fatal and clears payload byte count.
Decode/RLE validation failure sets fatal and clears decode pass.
Native byte-range overlap or range-list capacity overflow before save or replay scheduling sets fatal through `EntityScheduleFailureJob`; `[NoAlias]` jobs are not scheduled over unverifiable Vault views.
Short counters or missing Vault buffers fail closed.
Telemetry / Black Box
Telemetry fields:
sector hash
payload hash
frame
full snapshot bytes
dense delta bytes
RLE bytes
compressed bytes
flags
Burst time ms
disk write latency ms
global quality
compression effort
I/O pressure
delta entity count
Black-box fields:
same 300-frame telemetry ring
dump path: `Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin`
reason flag for disk latency spike
Profiler marker: `H8.Save.EntityDelta.ScheduleCompression` and `H8.Save.EntityDelta.ScheduleDecode` cover public scheduling facades. Worker job runtime timing still needs Unity/Burst profiler artifacts.
GC proof required: Unity Profiler or GCMonitor 0 B/frame for save scheduling, WAL enqueue, replay scheduling, and editor-disabled runtime path.
Shutdown / Stale Handles
Shutdown/disposal rule:
Compressor owns no private persistent native allocation.
Vault remains allocation/disposal owner.
Pager owns WAL stream lifetime; `H8BinaryWorldPager` opens WAL with `FileOptions.Asynchronous WriteThrough SequentialScan`.
Scene unload behavior:
Vault buffers must be released by `GlobalDataVault` owner.
Pager shutdown must drain/stop worker and close streams.
Stale-handle behavior:
`TryResolveVaultBuffers` resolves fresh handles before scheduling.
Public helper paths fail when buffers are missing, too short, or ticket payload type is not `EntityDeltaRle`.
Save/replay scheduling rejects overlapping native byte ranges before Burst jobs consume them.
Rejected Alternatives
[x] owner-local field: rejected because payload crosses save/replay/crash boundaries.
[x] cached owner interface: rejected for byte storage; retained only as `IAsyncPersistenceService` pager facade.
[x] existing SignalBus lane: rejected because persistence payload is not broadcast.
[x] existing Vault buffer: rejected because replay source must not alias RLE decode destination; added `SaveEntityDeltaWalPayloadBytes`.
[x] cold HectonEventBus hook: rejected because this is first-party persistence, not mod/API.
[ ] no global route needed.
Monolith Risk
This route does not add new registry slot, signal lane, event bus route, or sibling assembly reference. It adds owner-labeled SavePersistence Vault buffers and uses existing `IAsyncPersistenceService` interface. compile-wall debt remains existing root Core asmdef shape, not new SHINOBU_154 reference.
H-Phi impact expected:
Positive for persistence because cross-domain save/replay bytes are Vault-owned.
Not accepted as proof. Runtime acceptance still requires Unity import, Console, Play Mode, profiler/GC, WAL replay, and unload baseline artifacts.
Runtime proof required before acceptance:
Unity import includes SHINOBU_154 new C# assets. Current batchmode log confirms import saw assets but project-wide compile is blocked by non-SHINOBU domains.
Unity Console clean for SaveSystem/Core.
Burst compile succeeds for all 17 jobs.
Profiler/GCMonitor confirms 0 B hot-path scheduling/enqueue.
WAL write/read/replay sector test verifies checksum, RLE/LZ4 decode, endian marker, finite local offsets, pre-schedule alias rejection, and non-overlap on valid Vault buffers.
Telemetry ring dump is produced and can be read back.
Reviewer: pending integrator
Review result: YELLOW
Reason: route card complete as static documentation, but runtime-facing proof artifacts are blocked by compile errors in other owner domains.
===== END FILE: Route_SHINOBU_154_EntityDeltaCompression__watch22.md =====

===== FILE: Route_SHINOBU_154_EntityDeltaCompression__watch30.md =====
SHINOBU_154 Entity Delta Compression Route Card
Date: 2026-05-20
Status: YELLOW - STATIC SOURCE PRESENT, RUNTIME PROOF BLOCKED BY DEPENDENCY COMPILE WALL
Route ID: SAVE_ENTITY_DELTA_WAL_RLE_LZ4
Owner: SHINOBU_154 / Save Persistence
Owner domain: Echelon 1 Core & Memory Infrastructure / SaveSystem Data Archivist
Owning file/system: `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`
Problem
Dynamic entity truth for dropped items, fauna facts, base modules, and depleted resource nodes cannot be saved through JSON, object graph traversal, `BinaryFormatter`, or scene-wide `ISaveable` scans without autosave hitches.
Why Owner-Local Data Is Insufficient
entity delta stream must survive scene boundaries, async WAL handoff, replay, crash telemetry, and cross-domain producer ownership. Local scratch would not provide BufferID/SystemID evidence, stale-handle handling, or black-box proof for persistence failures.
Why Direct Caller/Owner Interface Is Insufficient
Save/load scheduling crosses producer domains and persistence pager. direct caller interface would still need persistent native buffers, checksum evidence, and async WAL staging. `IAsyncPersistenceService` remains narrow write/read facade; entity bytes live in Vault because they are job-visible, replay-visible, and crash-visible state.
Instrument
[ ] GlobalRegistry cold service/interface
[ ] SignalBus first-party broadcast
[ ] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase: save scheduling / worker job chain after authoritative entity producers publish flat records to Vault.
Consumer phase: async persistence pager WAL worker for writes; load/replay worker jobs for hydration.
Cadence: dirty autosave sector cadence; continuous `ResolveWriteHz(GlobalQualityWeight)` from low write Hz to high write Hz.
Expected max events/reads per frame: one sector payload enqueue per scheduled sector save; one read ticket/copy per requested sector replay.
GlobalQualityWeight behavior: compression effort uses smooth quality/pressure curves, active LZ4 hash slots, match length, probe step, and write cadence. No binary low/high switch.
Payload / Data Shape
Managed fields present: no in runtime DTOs/jobs.
UnityEngine.Object fields present: no in runtime DTOs/jobs.
Layout proof:
`EntityDeltaHeaderDTO`: explicit 32B, 8-byte fields at offsets 0 and 16.
`EntityDeltaRleStreamHeaderDTO`: explicit 16B.
`EntityDeltaDataRecordDTO`: explicit 80B, 8-byte sector/hash fields aligned.
`EntityDeltaBlockCounter64`: explicit 64B false-sharing-padded row.
`EntityCompressionTelemetryEntry`: explicit 64B telemetry ring row.
Capacity:
Vault buffers `70340..70357`, including current/baseline/delta records, dense/RLE/compressed/WAL byte buffers, counters, telemetry ring, tuning, stats, CSV scratch, and profiles.
`TelemetryRingFrames = 300`.
`HashTableSlots = 4096` on normal route; short test buffers fail/clamp safely.
Overflow/failure mode:
Delta block overflow sets `HeaderFlagFatal`.
Non-finite local AUP offsets set fatal and prevent corrupt WAL truth.
WAL envelope audit failure sets fatal and clears payload byte count.
Decode/RLE validation failure sets fatal and clears decode pass.
Native byte-range overlap or range-list capacity overflow before save or replay scheduling sets fatal through `EntityScheduleFailureJob`; `[NoAlias]` jobs are not scheduled over unverifiable Vault views.
Short counters or missing Vault buffers fail closed.
Telemetry / Black Box
Telemetry fields:
sector hash
payload hash
frame
full snapshot bytes
dense delta bytes
RLE bytes
compressed bytes
flags
Burst time ms
disk write latency ms
global quality
compression effort
I/O pressure
delta entity count
Black-box fields:
same 300-frame telemetry ring
dump path: `Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin`
reason flag for disk latency spike
Profiler marker: `H8.Save.EntityDelta.ScheduleCompression` and `H8.Save.EntityDelta.ScheduleDecode` cover public scheduling facades. Worker job runtime timing still needs Unity/Burst profiler artifacts.
GC proof required: Unity Profiler or GCMonitor 0 B/frame for save scheduling, WAL enqueue, replay scheduling, and editor-disabled runtime path.
Shutdown / Stale Handles
Shutdown/disposal rule:
Compressor owns no private persistent native allocation.
Vault remains allocation/disposal owner.
Pager owns WAL stream lifetime; `H8BinaryWorldPager` opens WAL with `FileOptions.Asynchronous WriteThrough SequentialScan`.
Scene unload behavior:
Vault buffers must be released by `GlobalDataVault` owner.
Pager shutdown must drain/stop worker and close streams.
Stale-handle behavior:
`TryResolveVaultBuffers` resolves fresh handles before scheduling.
Public helper paths fail when buffers are missing, too short, or ticket payload type is not `EntityDeltaRle`.
Save/replay scheduling rejects overlapping native byte ranges before Burst jobs consume them.
Rejected Alternatives
[x] owner-local field: rejected because payload crosses save/replay/crash boundaries.
[x] cached owner interface: rejected for byte storage; retained only as `IAsyncPersistenceService` pager facade.
[x] existing SignalBus lane: rejected because persistence payload is not broadcast.
[x] existing Vault buffer: rejected because replay source must not alias RLE decode destination; added `SaveEntityDeltaWalPayloadBytes`.
[x] cold HectonEventBus hook: rejected because this is first-party persistence, not mod/API.
[ ] no global route needed.
Monolith Risk
This route does not add new registry slot, signal lane, event bus route, or sibling assembly reference. It adds owner-labeled SavePersistence Vault buffers and uses existing `IAsyncPersistenceService` interface. compile-wall debt remains existing root Core asmdef shape, not new SHINOBU_154 reference.
H-Phi impact expected:
Positive for persistence because cross-domain save/replay bytes are Vault-owned.
Not accepted as proof. Runtime acceptance still requires Unity import, Console, Play Mode, profiler/GC, WAL replay, and unload baseline artifacts.
Runtime proof required before acceptance:
Unity import includes SHINOBU_154 new C# assets. Current batchmode log confirms import saw assets but project-wide compile is blocked by non-SHINOBU domains.
Unity Console clean for SaveSystem/Core.
Burst compile succeeds for all 17 jobs.
Profiler/GCMonitor confirms 0 B hot-path scheduling/enqueue.
WAL write/read/replay sector test verifies checksum, RLE/LZ4 decode, endian marker, finite local offsets, pre-schedule alias rejection, and non-overlap on valid Vault buffers.
Telemetry ring dump is produced and can be read back.
Reviewer: pending integrator
Review result: YELLOW
Reason: route card complete as static documentation, but runtime-facing proof artifacts are blocked by compile errors in other owner domains.
===== END FILE: Route_SHINOBU_154_EntityDeltaCompression__watch30.md =====

===== FILE: Route_SHINOBU_154_EntityDeltaCompression__watch33.md =====
SHINOBU_154 Entity Delta Compression Route Card
Date: 2026-05-20
Status: YELLOW - STATIC SOURCE PRESENT, RUNTIME PROOF BLOCKED BY DEPENDENCY COMPILE WALL
Route ID: SAVE_ENTITY_DELTA_WAL_RLE_LZ4
Owner: SHINOBU_154 / Save Persistence
Owner domain: Echelon 1 Core & Memory Infrastructure / SaveSystem Data Archivist
Owning file/system: `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`
Problem
Dynamic entity truth for dropped items, fauna facts, base modules, and depleted resource nodes cannot be saved through JSON, object graph traversal, `BinaryFormatter`, or scene-wide `ISaveable` scans without autosave hitches.
Why Owner-Local Data Is Insufficient
entity delta stream must survive scene boundaries, async WAL handoff, replay, crash telemetry, and cross-domain producer ownership. Local scratch would not provide BufferID/SystemID evidence, stale-handle handling, or black-box proof for persistence failures.
Why Direct Caller/Owner Interface Is Insufficient
Save/load scheduling crosses producer domains and persistence pager. direct caller interface would still need persistent native buffers, checksum evidence, and async WAL staging. `IAsyncPersistenceService` remains narrow write/read facade; entity bytes live in Vault because they are job-visible, replay-visible, and crash-visible state.
Instrument
[ ] GlobalRegistry cold service/interface
[ ] SignalBus first-party broadcast
[ ] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase: save scheduling / worker job chain after authoritative entity producers publish flat records to Vault.
Consumer phase: async persistence pager WAL worker for writes; load/replay worker jobs for hydration.
Cadence: dirty autosave sector cadence; continuous `ResolveWriteHz(GlobalQualityWeight)` from low write Hz to high write Hz.
Expected max events/reads per frame: one sector payload enqueue per scheduled sector save; one read ticket/copy per requested sector replay.
GlobalQualityWeight behavior: compression effort uses smooth quality/pressure curves, active LZ4 hash slots, match length, probe step, and write cadence. No binary low/high switch.
Payload / Data Shape
Managed fields present: no in runtime DTOs/jobs.
UnityEngine.Object fields present: no in runtime DTOs/jobs.
Layout proof:
`EntityDeltaHeaderDTO`: explicit 32B, 8-byte fields at offsets 0 and 16.
`EntityDeltaRleStreamHeaderDTO`: explicit 16B.
`EntityDeltaDataRecordDTO`: explicit 80B, 8-byte sector/hash fields aligned.
`EntityDeltaBlockCounter64`: explicit 64B false-sharing-padded row.
`EntityCompressionTelemetryEntry`: explicit 64B telemetry ring row.
Capacity:
Vault buffers `70340..70357`, including current/baseline/delta records, dense/RLE/compressed/WAL byte buffers, counters, telemetry ring, tuning, stats, CSV scratch, and profiles.
`TelemetryRingFrames = 300`.
`HashTableSlots = 4096` on normal route; short test buffers fail/clamp safely.
Overflow/failure mode:
Delta block overflow sets `HeaderFlagFatal`.
Non-finite local AUP offsets set fatal and prevent corrupt WAL truth.
WAL envelope audit failure sets fatal and clears payload byte count.
Decode/RLE validation failure sets fatal and clears decode pass.
Native byte-range overlap or range-list capacity overflow before save or replay scheduling sets fatal through `EntityScheduleFailureJob`; `[NoAlias]` jobs are not scheduled over unverifiable Vault views.
Short counters or missing Vault buffers fail closed.
Telemetry / Black Box
Telemetry fields:
sector hash
payload hash
frame
full snapshot bytes
dense delta bytes
RLE bytes
compressed bytes
flags
Burst time ms
disk write latency ms
global quality
compression effort
I/O pressure
delta entity count
Black-box fields:
same 300-frame telemetry ring
dump path: `Docs/AgentLogs/Dump_ENTITY_IO_SURGEON.bin`
reason flag for disk latency spike
Profiler marker: `H8.Save.EntityDelta.ScheduleCompression` and `H8.Save.EntityDelta.ScheduleDecode` cover public scheduling facades. Worker job runtime timing still needs Unity/Burst profiler artifacts.
GC proof required: Unity Profiler or GCMonitor 0 B/frame for save scheduling, WAL enqueue, replay scheduling, and editor-disabled runtime path.
Shutdown / Stale Handles
Shutdown/disposal rule:
Compressor owns no private persistent native allocation.
Vault remains allocation/disposal owner.
Pager owns WAL stream lifetime; `H8BinaryWorldPager` opens WAL with `FileOptions.Asynchronous WriteThrough SequentialScan`.
Scene unload behavior:
Vault buffers must be released by `GlobalDataVault` owner.
Pager shutdown must drain/stop worker and close streams.
Stale-handle behavior:
`TryResolveVaultBuffers` resolves fresh handles before scheduling.
Public helper paths fail when buffers are missing, too short, or ticket payload type is not `EntityDeltaRle`.
Save/replay scheduling rejects overlapping native byte ranges before Burst jobs consume them.
Rejected Alternatives
[x] owner-local field: rejected because payload crosses save/replay/crash boundaries.
[x] cached owner interface: rejected for byte storage; retained only as `IAsyncPersistenceService` pager facade.
[x] existing SignalBus lane: rejected because persistence payload is not broadcast.
[x] existing Vault buffer: rejected because replay source must not alias RLE decode destination; added `SaveEntityDeltaWalPayloadBytes`.
[x] cold HectonEventBus hook: rejected because this is first-party persistence, not mod/API.
[ ] no global route needed.
Monolith Risk
This route does not add new registry slot, signal lane, event bus route, or sibling assembly reference. It adds owner-labeled SavePersistence Vault buffers and uses existing `IAsyncPersistenceService` interface. compile-wall debt remains existing root Core asmdef shape, not new SHINOBU_154 reference.
H-Phi impact expected:
Positive for persistence because cross-domain save/replay bytes are Vault-owned.
Not accepted as proof. Runtime acceptance still requires Unity import, Console, Play Mode, profiler/GC, WAL replay, and unload baseline artifacts.
Runtime proof required before acceptance:
Unity import includes SHINOBU_154 new C# assets. Current batchmode log confirms import saw assets but project-wide compile is blocked by non-SHINOBU domains.
Unity Console clean for SaveSystem/Core.
Burst compile succeeds for all 17 jobs.
Profiler/GCMonitor confirms 0 B hot-path scheduling/enqueue.
WAL write/read/replay sector test verifies checksum, RLE/LZ4 decode, endian marker, finite local offsets, pre-schedule alias rejection, and non-overlap on valid Vault buffers.
Telemetry ring dump is produced and can be read back.
Reviewer: pending integrator
Review result: YELLOW
Reason: route card complete as static documentation, but runtime-facing proof artifacts are blocked by compile errors in other owner domains.
===== END FILE: Route_SHINOBU_154_EntityDeltaCompression__watch33.md =====

===== FILE: Route_SHINOBU_155_Respawn.md =====
Route_SHINOBU_155_Respawn
Date: 2026-05-19
Status: PENDING VERIFICATION - COMPILE BLOCKED BY EXTERNAL CONTRACT BRIDGE ERRORS
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Owner domain: ECHELON 5 - Combat & Survival Physiology
Owning file/system: `ShinobuRespawnReconciliationRuntime`
Problem:
Fatal player death must not reload scene, instantiate replacement player, or run managed cutscene. It must reconcile player AUP, physiology, metabolism, inventory penalty, shader fade, and AI target state through deterministic unmanaged routes.
Why owner-local data is insufficient:
death event originates in gameplay/survival, but authoritative mutation touches Physiology Vault rows, player kinematic AUP, rendering shader globals, inventory command traffic, and mesofauna target state. Keeping that state in one MonoBehaviour would create direct sibling dependencies and stale local ownership.
Why direct caller/owner interface is insufficient:
There are multiple consumers and they live behind different asmdef/domain boundaries. direct interface from Gameplay to Physiology plus Fauna plus Rendering would break Compile Wall and would not provide same-frame black-boxable contract payload.
Instrument:
[ ] GlobalRegistry cold service/interface
[x] SignalBus first-party broadcast
[x] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase:
Gameplay/survival fatal damage emits only `SignalBus ` when lethal state is detected. It does not write shader globals. Death-adjacent health/survival signal timestamps use `TimeSliceScheduler.CurrentFrameId`, not Unity `Time.frameCount`.
On successful reconciliation, legacy managed `GlobalTelemetryBus.PublishPlayerDeath`, `SurvivalVitalsChangedSignalFlags.Death`, human-readable `RecordDeathTelemetry`, `OnDeath`, and `PlayerDiedEvent` fallback side effects are skipped; they remain only for unreconciled failure. Health death AUP resolution finite-gates `CurrentAup` before bridge and falls back only to finite runtime-position-derived AUP.
Consumer phase:
`ShinobuRespawnReconciliationRuntime` consumes request in `PreSimulation`, resolves med-bay AUP, writes Vault request/state rows, and mutates current `PlayerRespawnSignal` snapshot in-place so same-frame consumers see resolved `RespawnAUP`, `MedicalBayHashID`, request/commit flags, and clamped collision-suspend count. `ResetPlayerPhysiologyJob` then consumes that staged `RespawnStateDTO` as primary med-bay truth; staged route flags are accepted only with that staged target, and job scans `MedicalBayRespawnPointDTO` rows only if staged target is missing, non-finite, or unresolved. It mutates Vault in `Simulation`, records fault/dump state in `PostSimulation`, and publishes shader scalars in `VisualSync` only after active job fence is already completed. Dispatcher phases consume cached `_dataVault` only and use `HasHotVaultState()` so they never request Vault buffers; allocation-capable `EnsureVaultState(...)` is restricted to Awake/Start/DataVault hot-swap/editor utility paths. Dear Lie shader publish uses `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie(IDataVault, Vector4)` with cached `_dataVault`, and local dirty latch publishes only while active plus one zero-clear frame after fade end. Simulation also refuses to schedule another writer over same Vault rows while prior active handle is incomplete; it returns combined dependency instead. Mesofauna consumes request/commit snapshot through existing predator cognition signal stage. `HydrodynamicKccRuntime` consumes requested or committed `SuspendCollision` packets and skips capsulecast/collision resolution for one snapshot generation.
Cadence:
Dirty-only on death requests. Fade update runs only while pending request or active fade exists. VisualSync shader publication is also dirty-only: active fade frames publish payload, first inactive frame after active fade publishes zero, and later idle frames return before bridge.
Expected max events/reads per frame:
Core `GlobalSignals` configures `PlayerRespawnSignal` for expected capacity 8, max frame signals 16, low-tier frame signals 4, stable hash `0x5253504E`, direct pre-simulation flush, post-simulation clear, finite guard, 96-byte layout validation, and AOT preservation. Normal expected traffic is 0 or 1 player death request per frame.
GlobalQualityWeight behavior:
`RespawnFadeDTO` consumes continuous `GlobalQualityWeight`: low weight accelerates fade decay and collapses shader detail to blackout/grain scalars; high weight preserves longer chromatic/grain cover for Visual Overkill without changing authoritative simulation.
Physics collision suspend:
`PlayerRespawnSignalFlags.SuspendCollision` does not call Physiology from Physics. KCC reads contract lane, accepts request or committed respawn packets, latches one bypass frame by `SignalBus .SnapshotGeneration`, skips `CapsulecastCommand.ScheduleBatch`, bypasses hit extraction, and marks `FlagRespawnCollisionBypass` in debug/telemetry flags. snapshot-generation latch prevents duplicate extension.
Payload/data shape:
`PlayerRespawnSignal` is unmanaged, explicit 96 bytes: death AUP, respawn AUP, hashes, frame, sequence, flags, phase bytes, and padding. Vault DTOs are explicit 16/32/64-byte unmanaged rows.
Payload managed fields present: no. Runtime managed fields are cold-only path strings, dispatcher adapter objects, and cached service handles; no persistent managed collection owns gameplay state.
UnityEngine.Object fields present: no serialized or hot-path object reference fields.
Hot-path allocation proof:
`ShinobuRespawnJobs.cs` contains no literal `new` after polish pass. `ShinobuRespawnReconciliationRuntime.ScheduleSimulation` and `VisualSyncTick` now build job structs and shader payloads through `default` field assignment. Hot dispatcher phases use `HasHotVaultState()` and cannot allocate/request Vault buffers; cold `EnsureVaultState(...)` remains boot/editor/hot-swap only. `HectonShaderGlobalDataVaultBridge.cs` now has no typed `new float4`/`new Vector4`; its vector constants, mask packing, conversion helpers, and reset payloads use explicit field assignment helpers. Remaining runtime `new` hits are cold host/dispatcher adapter creation, cold file IO for CSV/dump, and stack-only `Span ` constructor in cold CSV ingest. Remaining `JobHandle.Complete()` calls are cold mock-medbay boot generation and teardown/service-replacement fences, not per-frame VisualSync or Simulation stalls.
Death-vicinity hygiene:
Mutable `VitalWarningSignal`, `PhysiologyStateSignal`, and `SurvivalVitalsChangedSignal` publishers in health/survival vicinity use `default` field assignment, not object-initializer `new`. `SurvivalDatabaseItemRecord` no longer uses `Pack=1`; it is explicit 24 bytes with manual `uint _pad0` at offset `20`.
Legacy telemetry/log/event side effects are fallback-only after `PlayerDeathReconciliationBridge.RequestRespawn(...)` fails. Reconciled death does not enter `GlobalTelemetryBus.PublishPlayerDeath`, `SurvivalVitalsChangedSignalFlags.Death`, `RecordDeathTelemetry`, managed `OnDeath`, or `PlayerDiedEvent`.
Layout proof:
`RespawnStateDTO` 32 bytes, `RespawnRequestDTO` 64 bytes, `MedicalBayRespawnPointDTO` 64 bytes, `RespawnFadeDTO` 32 bytes, `RespawnTuningDTO` 64 bytes, `InventoryDeathPenaltyRuleDTO` 16 bytes, `RespawnTelemetryEntry` 64 bytes, `RespawnTelemetryCursor64` 64 bytes. `PlayerRespawnSignal` is 96 bytes because it carries two `double3` AUP values. `InventoryCommandSignal` remains 32 bytes and now uses offsets `14/16/20/24/28` for penalty payload metadata.
Capacity:
Vault IDs `71604..71613`: state[1], med bays[8], fade[1], telemetry[300], cursor[1], tuning[1], `InventoryDeathPenaltyRuleDTO` penalty rules[64], rule count[1], CSV scratch[32768], request[1]. Inventory receives rule table through `InventoryCommandSignal.Payload0=71610`, `Payload1=ruleCount`, `Payload2=capacity`, and `Payload3=SHINOBU_155 source hash`. XML NativeHashMap wording is implemented as fixed Vault row table to preserve Vault ownership, deterministic bounded lookup, and blittable rollback/memcpy behavior.
Overflow/failure mode:
If signal lane refuses request, health reconciliation is not applied. If medical bay validation fails, target falls back to deterministic lifepod AUP and flags `FallbackLifepod`/`InvalidTargetAup`. NaN/invalid AUP writes set black-box fault flags and trigger `Docs/AgentLogs/Dump_SHINOBU_155.bin` plus XML compatibility alias `Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin`.
NaN guard status:
`ResetPlayerPhysiologyJob.WriteKinematic()` guards `target / sectorSize` with `math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)`. Both SHINOBU local AUP conversion helpers now use `SafeAupClampMeters()` before clamping and casting AUP deltas to `float3`. `math.rsqrt` is guarded with `math.max(lengthSq, 0.0001f)`.
Telemetry fields:
Death AUP, respawn AUP, cause hash, frame, schedule/reconcile microseconds, flags.
Black-box fields:
300-frame `RespawnTelemetryEntry` ring plus `RespawnTelemetryCursor64` false-sharing padded cursor.
Profiler marker:
No explicit ProfilerMarker in this patch. Runtime proof must capture dispatcher/Burst job timing before acceptance.
GC proof required:
Unity Profiler/GCMonitor 0 B/frame during no-death steady state and during one death reconciliation frame.
Shutdown/disposal rule:
Vault owns native buffers. Runtime owns only handles and cold dispatcher adapters; handles are cleared on disable/hot-swap. Active job fence is registered with `H8Memory`.
Scene unload behavior:
No scene reload or unload is requested by this route. Runtime host is `DontSave`; scene unload must unregister dispatcher adapters and clear cached handles.
Stale-handle behavior:
On DataVault replacement, active work is fenced, handles are cleared, defaults are rehydrated from new Vault, and fault dump state is reset.
Rejected alternatives:
[x] owner-local field
[x] cached owner interface
[x] existing SignalBus lane
[x] existing Vault buffer
[x] cold HectonEventBus hook
[x] no global route needed
Why this does not increase global monolith risk:
only new contract payload is one unmanaged signal with narrow death-reconciliation purpose. Persistent data stays in owner-local Vault IDs and does not modify global `BufferID` enum or sibling asmdef references.
H-Phi impact expected:
Persistent death-reconciliation state moves out of scene objects and into Vault-owned unmanaged rows. Runtime logic remains stateless dispatcher owner around Burst jobs.
Runtime proof required before acceptance:
Unity import/Console, Burst compile, Play Mode death trigger, one-frame KCC collision-bypass proof, GCMonitor 0 B/frame, Profiler timing, shader fade visual capture, and black-box dump validation on injected invalid AUP.
Reviewer: SHINOBU_155 self-review
Status: YELLOW
Global authority review:
Result: YELLOW
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Instrument: `SignalBus ` + `GlobalDataVault`
Reason: Route is narrow and statically bounded, but runtime/Unity/profiler proof is absent.
Required fixes: Fresh Unity import/Console, profiler, and GC proof before `GREEN`.
Proof still missing: Unity import, Burst compile, Play Mode, KCC one-frame bypass capture, shader visual proof, GCMonitor, player-build proof.
Reviewer: SHINOBU_155 self-review
Date: 2026-05-19
Compile blocker:
Earlier guarded `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed before SHINOBU code on `CS2001` for deleted `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, then current architecture ledger recorded that `Directory.Build.targets` shields that stale generated include. follow-up guarded Core compile now advances to external missing contract/source bridge semantic errors outside SHINOBU_155 ownership. No cross-domain stubs or generated project edits were made by this lane.
===== END FILE: Route_SHINOBU_155_Respawn.md =====

===== FILE: Route_SHINOBU_155_Respawn__instant99.md =====
Route_SHINOBU_155_Respawn
Date: 2026-05-19
Status: PENDING VERIFICATION - COMPILE BLOCKED BY EXTERNAL CONTRACT BRIDGE ERRORS
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Owner domain: ECHELON 5 - Combat & Survival Physiology
Owning file/system: `ShinobuRespawnReconciliationRuntime`
Problem:
Fatal player death must not reload scene, instantiate replacement player, or run managed cutscene. It must reconcile player AUP, physiology, metabolism, inventory penalty, shader fade, and AI target state through deterministic unmanaged routes.
Why owner-local data is insufficient:
death event originates in gameplay/survival, but authoritative mutation touches Physiology Vault rows, player kinematic AUP, rendering shader globals, inventory command traffic, and mesofauna target state. Keeping that state in one MonoBehaviour would create direct sibling dependencies and stale local ownership.
Why direct caller/owner interface is insufficient:
There are multiple consumers and they live behind different asmdef/domain boundaries. direct interface from Gameplay to Physiology plus Fauna plus Rendering would break Compile Wall and would not provide same-frame black-boxable contract payload.
Instrument:
[ ] GlobalRegistry cold service/interface
[x] SignalBus first-party broadcast
[x] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase:
Gameplay/survival fatal damage emits only `SignalBus ` when lethal state is detected. It does not write shader globals. Death-adjacent health/survival signal timestamps use `TimeSliceScheduler.CurrentFrameId`, not Unity `Time.frameCount`.
On successful reconciliation, legacy managed `GlobalTelemetryBus.PublishPlayerDeath`, human-readable `RecordDeathTelemetry`, `OnDeath`, and `PlayerDiedEvent` fallback side effects are skipped; they remain only for unreconciled failure. Health death AUP resolution finite-gates `CurrentAup` before bridge and falls back only to finite runtime-position-derived AUP.
Consumer phase:
`ShinobuRespawnReconciliationRuntime` consumes request in `PreSimulation`, resolves med-bay AUP, writes Vault request/state rows, and mutates current `PlayerRespawnSignal` snapshot in-place so same-frame consumers see resolved `RespawnAUP`, `MedicalBayHashID`, request/commit flags, and clamped collision-suspend count. `ResetPlayerPhysiologyJob` then consumes that staged `RespawnStateDTO` as primary med-bay truth; staged route flags are accepted only with that staged target, and job scans `MedicalBayRespawnPointDTO` rows only if staged target is missing, non-finite, or unresolved. It mutates Vault in `Simulation`, records fault/dump state in `PostSimulation`, and publishes shader scalars in `VisualSync` only after active job fence is already completed. Dispatcher phases consume cached `_dataVault` only and use `HasHotVaultState()` so they never request Vault buffers; allocation-capable `EnsureVaultState(...)` is restricted to Awake/Start/DataVault hot-swap/editor utility paths. Dear Lie shader publish uses `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie(IDataVault, Vector4)` with cached `_dataVault`, and local dirty latch publishes only while active plus one zero-clear frame after fade end. Simulation also refuses to schedule another writer over same Vault rows while prior active handle is incomplete; it returns combined dependency instead. Mesofauna consumes request/commit snapshot through existing predator cognition signal stage. `HydrodynamicKccRuntime` consumes requested or committed `SuspendCollision` packets and skips capsulecast/collision resolution for one snapshot generation.
Cadence:
Dirty-only on death requests. Fade update runs only while pending request or active fade exists. VisualSync shader publication is also dirty-only: active fade frames publish payload, first inactive frame after active fade publishes zero, and later idle frames return before bridge.
Expected max events/reads per frame:
Core `GlobalSignals` configures `PlayerRespawnSignal` for expected capacity 8, max frame signals 16, low-tier frame signals 4, stable hash `0x5253504E`, direct pre-simulation flush, post-simulation clear, finite guard, 96-byte layout validation, and AOT preservation. Normal expected traffic is 0 or 1 player death request per frame.
GlobalQualityWeight behavior:
`RespawnFadeDTO` consumes continuous `GlobalQualityWeight`: low weight accelerates fade decay and collapses shader detail to blackout/grain scalars; high weight preserves longer chromatic/grain cover for Visual Overkill without changing authoritative simulation.
Physics collision suspend:
`PlayerRespawnSignalFlags.SuspendCollision` does not call Physiology from Physics. KCC reads contract lane, accepts request or committed respawn packets, latches one bypass frame by `SignalBus .SnapshotGeneration`, skips `CapsulecastCommand.ScheduleBatch`, bypasses hit extraction, and marks `FlagRespawnCollisionBypass` in debug/telemetry flags. snapshot-generation latch prevents duplicate extension.
Payload/data shape:
`PlayerRespawnSignal` is unmanaged, explicit 96 bytes: death AUP, respawn AUP, hashes, frame, sequence, flags, phase bytes, and padding. Vault DTOs are explicit 16/32/64-byte unmanaged rows.
Payload managed fields present: no. Runtime managed fields are cold-only path strings, dispatcher adapter objects, and cached service handles; no persistent managed collection owns gameplay state.
UnityEngine.Object fields present: no serialized or hot-path object reference fields.
Hot-path allocation proof:
`ShinobuRespawnJobs.cs` contains no literal `new` after polish pass. `ShinobuRespawnReconciliationRuntime.ScheduleSimulation` and `VisualSyncTick` now build job structs and shader payloads through `default` field assignment. Hot dispatcher phases use `HasHotVaultState()` and cannot allocate/request Vault buffers; cold `EnsureVaultState(...)` remains boot/editor/hot-swap only. `HectonShaderGlobalDataVaultBridge.cs` now has no typed `new float4`/`new Vector4`; its vector constants, mask packing, conversion helpers, and reset payloads use explicit field assignment helpers. Remaining runtime `new` hits are cold host/dispatcher adapter creation, cold file IO for CSV/dump, and stack-only `Span ` constructor in cold CSV ingest. Remaining `JobHandle.Complete()` calls are cold mock-medbay boot generation and teardown/service-replacement fences, not per-frame VisualSync or Simulation stalls.
Death-vicinity hygiene:
Mutable `VitalWarningSignal`, `PhysiologyStateSignal`, and `SurvivalVitalsChangedSignal` publishers in health/survival vicinity use `default` field assignment, not object-initializer `new`. `SurvivalDatabaseItemRecord` no longer uses `Pack=1`; it is explicit 24 bytes with manual `uint _pad0` at offset `20`.
Legacy telemetry/log/event side effects are fallback-only after `PlayerDeathReconciliationBridge.RequestRespawn(...)` fails. Reconciled death does not enter `GlobalTelemetryBus.PublishPlayerDeath`, `RecordDeathTelemetry`, managed `OnDeath`, or `PlayerDiedEvent`.
Layout proof:
`RespawnStateDTO` 32 bytes, `RespawnRequestDTO` 64 bytes, `MedicalBayRespawnPointDTO` 64 bytes, `RespawnFadeDTO` 32 bytes, `RespawnTuningDTO` 64 bytes, `InventoryDeathPenaltyRuleDTO` 16 bytes, `RespawnTelemetryEntry` 64 bytes, `RespawnTelemetryCursor64` 64 bytes. `PlayerRespawnSignal` is 96 bytes because it carries two `double3` AUP values. `InventoryCommandSignal` remains 32 bytes and now uses offsets `14/16/20/24/28` for penalty payload metadata.
Capacity:
Vault IDs `71604..71613`: state[1], med bays[8], fade[1], telemetry[300], cursor[1], tuning[1], `InventoryDeathPenaltyRuleDTO` penalty rules[64], rule count[1], CSV scratch[32768], request[1]. Inventory receives rule table through `InventoryCommandSignal.Payload0=71610`, `Payload1=ruleCount`, `Payload2=capacity`, and `Payload3=SHINOBU_155 source hash`. XML NativeHashMap wording is implemented as fixed Vault row table to preserve Vault ownership, deterministic bounded lookup, and blittable rollback/memcpy behavior.
Overflow/failure mode:
If signal lane refuses request, health reconciliation is not applied. If medical bay validation fails, target falls back to deterministic lifepod AUP and flags `FallbackLifepod`/`InvalidTargetAup`. NaN/invalid AUP writes set black-box fault flags and trigger `Docs/AgentLogs/Dump_SHINOBU_155.bin` plus XML compatibility alias `Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin`.
NaN guard status:
`ResetPlayerPhysiologyJob.WriteKinematic()` guards `target / sectorSize` with `math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)`. Both SHINOBU local AUP conversion helpers now use `SafeAupClampMeters()` before clamping and casting AUP deltas to `float3`. `math.rsqrt` is guarded with `math.max(lengthSq, 0.0001f)`.
Telemetry fields:
Death AUP, respawn AUP, cause hash, frame, schedule/reconcile microseconds, flags.
Black-box fields:
300-frame `RespawnTelemetryEntry` ring plus `RespawnTelemetryCursor64` false-sharing padded cursor.
Profiler marker:
No explicit ProfilerMarker in this patch. Runtime proof must capture dispatcher/Burst job timing before acceptance.
GC proof required:
Unity Profiler/GCMonitor 0 B/frame during no-death steady state and during one death reconciliation frame.
Shutdown/disposal rule:
Vault owns native buffers. Runtime owns only handles and cold dispatcher adapters; handles are cleared on disable/hot-swap. Active job fence is registered with `H8Memory`.
Scene unload behavior:
No scene reload or unload is requested by this route. Runtime host is `DontSave`; scene unload must unregister dispatcher adapters and clear cached handles.
Stale-handle behavior:
On DataVault replacement, active work is fenced, handles are cleared, defaults are rehydrated from new Vault, and fault dump state is reset.
Rejected alternatives:
[x] owner-local field
[x] cached owner interface
[x] existing SignalBus lane
[x] existing Vault buffer
[x] cold HectonEventBus hook
[x] no global route needed
Why this does not increase global monolith risk:
only new contract payload is one unmanaged signal with narrow death-reconciliation purpose. Persistent data stays in owner-local Vault IDs and does not modify global `BufferID` enum or sibling asmdef references.
H-Phi impact expected:
Persistent death-reconciliation state moves out of scene objects and into Vault-owned unmanaged rows. Runtime logic remains stateless dispatcher owner around Burst jobs.
Runtime proof required before acceptance:
Unity import/Console, Burst compile, Play Mode death trigger, one-frame KCC collision-bypass proof, GCMonitor 0 B/frame, Profiler timing, shader fade visual capture, and black-box dump validation on injected invalid AUP.
Reviewer: SHINOBU_155 self-review
Status: YELLOW
Global authority review:
Result: YELLOW
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Instrument: `SignalBus ` + `GlobalDataVault`
Reason: Route is narrow and statically bounded, but runtime/Unity/profiler proof is absent.
Required fixes: Fresh Unity import/Console, profiler, and GC proof before `GREEN`.
Proof still missing: Unity import, Burst compile, Play Mode, KCC one-frame bypass capture, shader visual proof, GCMonitor, player-build proof.
Reviewer: SHINOBU_155 self-review
Date: 2026-05-19
Compile blocker:
Earlier guarded `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed before SHINOBU code on `CS2001` for deleted `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, then current architecture ledger recorded that `Directory.Build.targets` shields that stale generated include. follow-up guarded Core compile now advances to external missing contract/source bridge semantic errors outside SHINOBU_155 ownership. No cross-domain stubs or generated project edits were made by this lane.
===== END FILE: Route_SHINOBU_155_Respawn__instant99.md =====

===== FILE: Route_SHINOBU_155_Respawn__late1.md =====
Route_SHINOBU_155_Respawn
Date: 2026-05-19
Status: PENDING VERIFICATION - COMPILE BLOCKED BY EXTERNAL CONTRACT BRIDGE ERRORS
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Owner domain: ECHELON 5 - Combat & Survival Physiology
Owning file/system: `ShinobuRespawnReconciliationRuntime`
Problem:
Fatal player death must not reload scene, instantiate replacement player, or run managed cutscene. It must reconcile player AUP, physiology, metabolism, inventory penalty, shader fade, and AI target state through deterministic unmanaged routes.
Why owner-local data is insufficient:
death event originates in gameplay/survival, but authoritative mutation touches Physiology Vault rows, player kinematic AUP, rendering shader globals, inventory command traffic, and mesofauna target state. Keeping that state in one MonoBehaviour would create direct sibling dependencies and stale local ownership.
Why direct caller/owner interface is insufficient:
There are multiple consumers and they live behind different asmdef/domain boundaries. direct interface from Gameplay to Physiology plus Fauna plus Rendering would break Compile Wall and would not provide same-frame black-boxable contract payload.
Instrument:
[ ] GlobalRegistry cold service/interface
[x] SignalBus first-party broadcast
[x] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase:
Gameplay/survival fatal damage emits only `SignalBus ` when lethal state is detected. It does not write shader globals. Death-adjacent health/survival signal timestamps use `TimeSliceScheduler.CurrentFrameId`, not Unity `Time.frameCount`.
On successful reconciliation, legacy managed `OnDeath` and `PlayerDiedEvent` fallback side effects are skipped; they remain only for unreconciled failure.
Consumer phase:
`ShinobuRespawnReconciliationRuntime` consumes request in `PreSimulation`, resolves med-bay AUP, writes Vault request/state rows, and mutates current `PlayerRespawnSignal` snapshot in-place so same-frame consumers see resolved `RespawnAUP`, `MedicalBayHashID`, request/commit flags, and clamped collision-suspend count. `ResetPlayerPhysiologyJob` then consumes that staged `RespawnStateDTO` as primary med-bay truth; staged route flags are accepted only with that staged target, and job scans `MedicalBayRespawnPointDTO` rows only if staged target is missing, non-finite, or unresolved. It mutates Vault in `Simulation`, records fault/dump state in `PostSimulation`, and publishes shader scalars in `VisualSync` only after active job fence is already completed. Dispatcher phases consume cached `_dataVault` only; cold `GlobalRegistry.DataVault` fallback is restricted to Awake/Start/editor utility paths. Simulation also refuses to schedule another writer over same Vault rows while prior active handle is incomplete; it returns combined dependency instead. Mesofauna consumes request/commit snapshot through existing predator cognition signal stage. `HydrodynamicKccRuntime` consumes requested or committed `SuspendCollision` packets and skips capsulecast/collision resolution for one snapshot generation.
Cadence:
Dirty-only on death requests. Fade update runs only while pending request or active fade exists.
Expected max events/reads per frame:
Core `GlobalSignals` configures `PlayerRespawnSignal` for expected capacity 8, max frame signals 16, low-tier frame signals 4, stable hash `0x5253504E`, direct pre-simulation flush, post-simulation clear, finite guard, 96-byte layout validation, and AOT preservation. Normal expected traffic is 0 or 1 player death request per frame.
GlobalQualityWeight behavior:
`RespawnFadeDTO` consumes continuous `GlobalQualityWeight`: low weight accelerates fade decay and collapses shader detail to blackout/grain scalars; high weight preserves longer chromatic/grain cover for Visual Overkill without changing authoritative simulation.
Physics collision suspend:
`PlayerRespawnSignalFlags.SuspendCollision` does not call Physiology from Physics. KCC reads contract lane, accepts request or committed respawn packets, latches one bypass frame by `SignalBus .SnapshotGeneration`, skips `CapsulecastCommand.ScheduleBatch`, bypasses hit extraction, and marks `FlagRespawnCollisionBypass` in debug/telemetry flags. snapshot-generation latch prevents duplicate extension.
Payload/data shape:
`PlayerRespawnSignal` is unmanaged, explicit 96 bytes: death AUP, respawn AUP, hashes, frame, sequence, flags, phase bytes, and padding. Vault DTOs are explicit 16/32/64-byte unmanaged rows.
Payload managed fields present: no. Runtime managed fields are cold-only path strings, dispatcher adapter objects, and cached service handles; no persistent managed collection owns gameplay state.
UnityEngine.Object fields present: no serialized or hot-path object reference fields.
Hot-path allocation proof:
`ShinobuRespawnJobs.cs` contains no literal `new` after polish pass. `ShinobuRespawnReconciliationRuntime.ScheduleSimulation` and `VisualSyncTick` now build job structs and shader payloads through `default` field assignment. Remaining runtime `new` hits are cold host/dispatcher adapter creation, cold file IO for CSV/dump, and stack-only `Span ` constructor in cold CSV ingest. Remaining `JobHandle.Complete()` calls are cold mock-medbay boot generation and teardown/service-replacement fences, not per-frame VisualSync or Simulation stalls.
Death-vicinity hygiene:
Mutable `VitalWarningSignal`, `PhysiologyStateSignal`, and `SurvivalVitalsChangedSignal` publishers in health/survival vicinity use `default` field assignment, not object-initializer `new`. `SurvivalDatabaseItemRecord` no longer uses `Pack=1`; it is explicit 24 bytes with manual `uint _pad0` at offset `20`.
Layout proof:
`RespawnStateDTO` 32 bytes, `RespawnRequestDTO` 64 bytes, `MedicalBayRespawnPointDTO` 64 bytes, `RespawnFadeDTO` 32 bytes, `RespawnTuningDTO` 64 bytes, `InventoryDeathPenaltyRuleDTO` 16 bytes, `RespawnTelemetryEntry` 64 bytes, `RespawnTelemetryCursor64` 64 bytes. `PlayerRespawnSignal` is 96 bytes because it carries two `double3` AUP values. `InventoryCommandSignal` remains 32 bytes and now uses offsets `14/16/20/24/28` for penalty payload metadata.
Capacity:
Vault IDs `71604..71613`: state[1], med bays[8], fade[1], telemetry[300], cursor[1], tuning[1], `InventoryDeathPenaltyRuleDTO` penalty rules[64], rule count[1], CSV scratch[32768], request[1]. Inventory receives rule table through `InventoryCommandSignal.Payload0=71610`, `Payload1=ruleCount`, `Payload2=capacity`, and `Payload3=SHINOBU_155 source hash`. XML NativeHashMap wording is implemented as fixed Vault row table to preserve Vault ownership, deterministic bounded lookup, and blittable rollback/memcpy behavior.
Overflow/failure mode:
If signal lane refuses request, health reconciliation is not applied. If medical bay validation fails, target falls back to deterministic lifepod AUP and flags `FallbackLifepod`/`InvalidTargetAup`. NaN/invalid AUP writes set black-box fault flags and trigger `Docs/AgentLogs/Dump_SHINOBU_155.bin` plus XML compatibility alias `Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin`.
NaN guard status:
`ResetPlayerPhysiologyJob.WriteKinematic()` guards `target / sectorSize` with `math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)`. Both SHINOBU local AUP conversion helpers now use `SafeAupClampMeters()` before clamping and casting AUP deltas to `float3`. `math.rsqrt` is guarded with `math.max(lengthSq, 0.0001f)`.
Telemetry fields:
Death AUP, respawn AUP, cause hash, frame, schedule/reconcile microseconds, flags.
Black-box fields:
300-frame `RespawnTelemetryEntry` ring plus `RespawnTelemetryCursor64` false-sharing padded cursor.
Profiler marker:
No explicit ProfilerMarker in this patch. Runtime proof must capture dispatcher/Burst job timing before acceptance.
GC proof required:
Unity Profiler/GCMonitor 0 B/frame during no-death steady state and during one death reconciliation frame.
Shutdown/disposal rule:
Vault owns native buffers. Runtime owns only handles and cold dispatcher adapters; handles are cleared on disable/hot-swap. Active job fence is registered with `H8Memory`.
Scene unload behavior:
No scene reload or unload is requested by this route. Runtime host is `DontSave`; scene unload must unregister dispatcher adapters and clear cached handles.
Stale-handle behavior:
On DataVault replacement, active work is fenced, handles are cleared, defaults are rehydrated from new Vault, and fault dump state is reset.
Rejected alternatives:
[x] owner-local field
[x] cached owner interface
[x] existing SignalBus lane
[x] existing Vault buffer
[x] cold HectonEventBus hook
[x] no global route needed
Why this does not increase global monolith risk:
only new contract payload is one unmanaged signal with narrow death-reconciliation purpose. Persistent data stays in owner-local Vault IDs and does not modify global `BufferID` enum or sibling asmdef references.
H-Phi impact expected:
Persistent death-reconciliation state moves out of scene objects and into Vault-owned unmanaged rows. Runtime logic remains stateless dispatcher owner around Burst jobs.
Runtime proof required before acceptance:
Unity import/Console, Burst compile, Play Mode death trigger, one-frame KCC collision-bypass proof, GCMonitor 0 B/frame, Profiler timing, shader fade visual capture, and black-box dump validation on injected invalid AUP.
Reviewer: SHINOBU_155 self-review
Status: YELLOW
Global authority review:
Result: YELLOW
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Instrument: `SignalBus ` + `GlobalDataVault`
Reason: Route is narrow and statically bounded, but runtime/Unity/profiler proof is absent.
Required fixes: Fresh Unity import/Console, profiler, and GC proof before `GREEN`.
Proof still missing: Unity import, Burst compile, Play Mode, KCC one-frame bypass capture, shader visual proof, GCMonitor, player-build proof.
Reviewer: SHINOBU_155 self-review
Date: 2026-05-19
Compile blocker:
Earlier guarded `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed before SHINOBU code on `CS2001` for deleted `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, then current architecture ledger recorded that `Directory.Build.targets` shields that stale generated include. follow-up guarded Core compile now advances to external missing contract/source bridge semantic errors outside SHINOBU_155 ownership. No cross-domain stubs or generated project edits were made by this lane.
===== END FILE: Route_SHINOBU_155_Respawn__late1.md =====

===== FILE: Route_SHINOBU_155_Respawn__late11.md =====
Route_SHINOBU_155_Respawn
Date: 2026-05-19
Status: PENDING VERIFICATION - COMPILE BLOCKED BY EXTERNAL CONTRACT BRIDGE ERRORS
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Owner domain: ECHELON 5 - Combat & Survival Physiology
Owning file/system: `ShinobuRespawnReconciliationRuntime`
Problem:
Fatal player death must not reload scene, instantiate replacement player, or run managed cutscene. It must reconcile player AUP, physiology, metabolism, inventory penalty, shader fade, and AI target state through deterministic unmanaged routes.
Why owner-local data is insufficient:
death event originates in gameplay/survival, but authoritative mutation touches Physiology Vault rows, player kinematic AUP, rendering shader globals, inventory command traffic, and mesofauna target state. Keeping that state in one MonoBehaviour would create direct sibling dependencies and stale local ownership.
Why direct caller/owner interface is insufficient:
There are multiple consumers and they live behind different asmdef/domain boundaries. direct interface from Gameplay to Physiology plus Fauna plus Rendering would break Compile Wall and would not provide same-frame black-boxable contract payload.
Instrument:
[ ] GlobalRegistry cold service/interface
[x] SignalBus first-party broadcast
[x] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase:
Gameplay/survival fatal damage emits only `SignalBus ` when lethal state is detected. It does not write shader globals. Death-adjacent health/survival signal timestamps use `TimeSliceScheduler.CurrentFrameId`, not Unity `Time.frameCount`.
On successful reconciliation, legacy managed `OnDeath` and `PlayerDiedEvent` fallback side effects are skipped; they remain only for unreconciled failure.
Consumer phase:
`ShinobuRespawnReconciliationRuntime` consumes request in `PreSimulation`, resolves med-bay AUP, writes Vault request/state rows, and mutates current `PlayerRespawnSignal` snapshot in-place so same-frame consumers see resolved `RespawnAUP`, `MedicalBayHashID`, request/commit flags, and clamped collision-suspend count. `ResetPlayerPhysiologyJob` then consumes that staged `RespawnStateDTO` as primary med-bay truth; staged route flags are accepted only with that staged target, and job scans `MedicalBayRespawnPointDTO` rows only if staged target is missing, non-finite, or unresolved. It mutates Vault in `Simulation`, records fault/dump state in `PostSimulation`, and publishes shader scalars in `VisualSync` only after active job fence is already completed. Dispatcher phases consume cached `_dataVault` only; cold `GlobalRegistry.DataVault` fallback is restricted to Awake/Start/editor utility paths. Dear Lie shader publish uses `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie(IDataVault, Vector4)` with cached `_dataVault`, and local dirty latch publishes only while active plus one zero-clear frame after fade end. Simulation also refuses to schedule another writer over same Vault rows while prior active handle is incomplete; it returns combined dependency instead. Mesofauna consumes request/commit snapshot through existing predator cognition signal stage. `HydrodynamicKccRuntime` consumes requested or committed `SuspendCollision` packets and skips capsulecast/collision resolution for one snapshot generation.
Cadence:
Dirty-only on death requests. Fade update runs only while pending request or active fade exists. VisualSync shader publication is also dirty-only: active fade frames publish payload, first inactive frame after active fade publishes zero, and later idle frames return before bridge.
Expected max events/reads per frame:
Core `GlobalSignals` configures `PlayerRespawnSignal` for expected capacity 8, max frame signals 16, low-tier frame signals 4, stable hash `0x5253504E`, direct pre-simulation flush, post-simulation clear, finite guard, 96-byte layout validation, and AOT preservation. Normal expected traffic is 0 or 1 player death request per frame.
GlobalQualityWeight behavior:
`RespawnFadeDTO` consumes continuous `GlobalQualityWeight`: low weight accelerates fade decay and collapses shader detail to blackout/grain scalars; high weight preserves longer chromatic/grain cover for Visual Overkill without changing authoritative simulation.
Physics collision suspend:
`PlayerRespawnSignalFlags.SuspendCollision` does not call Physiology from Physics. KCC reads contract lane, accepts request or committed respawn packets, latches one bypass frame by `SignalBus .SnapshotGeneration`, skips `CapsulecastCommand.ScheduleBatch`, bypasses hit extraction, and marks `FlagRespawnCollisionBypass` in debug/telemetry flags. snapshot-generation latch prevents duplicate extension.
Payload/data shape:
`PlayerRespawnSignal` is unmanaged, explicit 96 bytes: death AUP, respawn AUP, hashes, frame, sequence, flags, phase bytes, and padding. Vault DTOs are explicit 16/32/64-byte unmanaged rows.
Payload managed fields present: no. Runtime managed fields are cold-only path strings, dispatcher adapter objects, and cached service handles; no persistent managed collection owns gameplay state.
UnityEngine.Object fields present: no serialized or hot-path object reference fields.
Hot-path allocation proof:
`ShinobuRespawnJobs.cs` contains no literal `new` after polish pass. `ShinobuRespawnReconciliationRuntime.ScheduleSimulation` and `VisualSyncTick` now build job structs and shader payloads through `default` field assignment. `HectonShaderGlobalDataVaultBridge.cs` now has no typed `new float4`/`new Vector4`; its vector constants, mask packing, conversion helpers, and reset payloads use explicit field assignment helpers. Remaining runtime `new` hits are cold host/dispatcher adapter creation, cold file IO for CSV/dump, and stack-only `Span ` constructor in cold CSV ingest. Remaining `JobHandle.Complete()` calls are cold mock-medbay boot generation and teardown/service-replacement fences, not per-frame VisualSync or Simulation stalls.
Death-vicinity hygiene:
Mutable `VitalWarningSignal`, `PhysiologyStateSignal`, and `SurvivalVitalsChangedSignal` publishers in health/survival vicinity use `default` field assignment, not object-initializer `new`. `SurvivalDatabaseItemRecord` no longer uses `Pack=1`; it is explicit 24 bytes with manual `uint _pad0` at offset `20`.
Layout proof:
`RespawnStateDTO` 32 bytes, `RespawnRequestDTO` 64 bytes, `MedicalBayRespawnPointDTO` 64 bytes, `RespawnFadeDTO` 32 bytes, `RespawnTuningDTO` 64 bytes, `InventoryDeathPenaltyRuleDTO` 16 bytes, `RespawnTelemetryEntry` 64 bytes, `RespawnTelemetryCursor64` 64 bytes. `PlayerRespawnSignal` is 96 bytes because it carries two `double3` AUP values. `InventoryCommandSignal` remains 32 bytes and now uses offsets `14/16/20/24/28` for penalty payload metadata.
Capacity:
Vault IDs `71604..71613`: state[1], med bays[8], fade[1], telemetry[300], cursor[1], tuning[1], `InventoryDeathPenaltyRuleDTO` penalty rules[64], rule count[1], CSV scratch[32768], request[1]. Inventory receives rule table through `InventoryCommandSignal.Payload0=71610`, `Payload1=ruleCount`, `Payload2=capacity`, and `Payload3=SHINOBU_155 source hash`. XML NativeHashMap wording is implemented as fixed Vault row table to preserve Vault ownership, deterministic bounded lookup, and blittable rollback/memcpy behavior.
Overflow/failure mode:
If signal lane refuses request, health reconciliation is not applied. If medical bay validation fails, target falls back to deterministic lifepod AUP and flags `FallbackLifepod`/`InvalidTargetAup`. NaN/invalid AUP writes set black-box fault flags and trigger `Docs/AgentLogs/Dump_SHINOBU_155.bin` plus XML compatibility alias `Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin`.
NaN guard status:
`ResetPlayerPhysiologyJob.WriteKinematic()` guards `target / sectorSize` with `math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)`. Both SHINOBU local AUP conversion helpers now use `SafeAupClampMeters()` before clamping and casting AUP deltas to `float3`. `math.rsqrt` is guarded with `math.max(lengthSq, 0.0001f)`.
Telemetry fields:
Death AUP, respawn AUP, cause hash, frame, schedule/reconcile microseconds, flags.
Black-box fields:
300-frame `RespawnTelemetryEntry` ring plus `RespawnTelemetryCursor64` false-sharing padded cursor.
Profiler marker:
No explicit ProfilerMarker in this patch. Runtime proof must capture dispatcher/Burst job timing before acceptance.
GC proof required:
Unity Profiler/GCMonitor 0 B/frame during no-death steady state and during one death reconciliation frame.
Shutdown/disposal rule:
Vault owns native buffers. Runtime owns only handles and cold dispatcher adapters; handles are cleared on disable/hot-swap. Active job fence is registered with `H8Memory`.
Scene unload behavior:
No scene reload or unload is requested by this route. Runtime host is `DontSave`; scene unload must unregister dispatcher adapters and clear cached handles.
Stale-handle behavior:
On DataVault replacement, active work is fenced, handles are cleared, defaults are rehydrated from new Vault, and fault dump state is reset.
Rejected alternatives:
[x] owner-local field
[x] cached owner interface
[x] existing SignalBus lane
[x] existing Vault buffer
[x] cold HectonEventBus hook
[x] no global route needed
Why this does not increase global monolith risk:
only new contract payload is one unmanaged signal with narrow death-reconciliation purpose. Persistent data stays in owner-local Vault IDs and does not modify global `BufferID` enum or sibling asmdef references.
H-Phi impact expected:
Persistent death-reconciliation state moves out of scene objects and into Vault-owned unmanaged rows. Runtime logic remains stateless dispatcher owner around Burst jobs.
Runtime proof required before acceptance:
Unity import/Console, Burst compile, Play Mode death trigger, one-frame KCC collision-bypass proof, GCMonitor 0 B/frame, Profiler timing, shader fade visual capture, and black-box dump validation on injected invalid AUP.
Reviewer: SHINOBU_155 self-review
Status: YELLOW
Global authority review:
Result: YELLOW
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Instrument: `SignalBus ` + `GlobalDataVault`
Reason: Route is narrow and statically bounded, but runtime/Unity/profiler proof is absent.
Required fixes: Fresh Unity import/Console, profiler, and GC proof before `GREEN`.
Proof still missing: Unity import, Burst compile, Play Mode, KCC one-frame bypass capture, shader visual proof, GCMonitor, player-build proof.
Reviewer: SHINOBU_155 self-review
Date: 2026-05-19
Compile blocker:
Earlier guarded `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed before SHINOBU code on `CS2001` for deleted `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, then current architecture ledger recorded that `Directory.Build.targets` shields that stale generated include. follow-up guarded Core compile now advances to external missing contract/source bridge semantic errors outside SHINOBU_155 ownership. No cross-domain stubs or generated project edits were made by this lane.
===== END FILE: Route_SHINOBU_155_Respawn__late11.md =====

===== FILE: Route_SHINOBU_155_Respawn__late2.md =====
Route_SHINOBU_155_Respawn
Date: 2026-05-19
Status: PENDING VERIFICATION - COMPILE BLOCKED BY EXTERNAL CONTRACT BRIDGE ERRORS
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Owner domain: ECHELON 5 - Combat & Survival Physiology
Owning file/system: `ShinobuRespawnReconciliationRuntime`
Problem:
Fatal player death must not reload scene, instantiate replacement player, or run managed cutscene. It must reconcile player AUP, physiology, metabolism, inventory penalty, shader fade, and AI target state through deterministic unmanaged routes.
Why owner-local data is insufficient:
death event originates in gameplay/survival, but authoritative mutation touches Physiology Vault rows, player kinematic AUP, rendering shader globals, inventory command traffic, and mesofauna target state. Keeping that state in one MonoBehaviour would create direct sibling dependencies and stale local ownership.
Why direct caller/owner interface is insufficient:
There are multiple consumers and they live behind different asmdef/domain boundaries. direct interface from Gameplay to Physiology plus Fauna plus Rendering would break Compile Wall and would not provide same-frame black-boxable contract payload.
Instrument:
[ ] GlobalRegistry cold service/interface
[x] SignalBus first-party broadcast
[x] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase:
Gameplay/survival fatal damage emits only `SignalBus ` when lethal state is detected. It does not write shader globals. Death-adjacent health/survival signal timestamps use `TimeSliceScheduler.CurrentFrameId`, not Unity `Time.frameCount`.
On successful reconciliation, legacy managed `OnDeath` and `PlayerDiedEvent` fallback side effects are skipped; they remain only for unreconciled failure.
Consumer phase:
`ShinobuRespawnReconciliationRuntime` consumes request in `PreSimulation`, resolves med-bay AUP, writes Vault request/state rows, and mutates current `PlayerRespawnSignal` snapshot in-place so same-frame consumers see resolved `RespawnAUP`, `MedicalBayHashID`, request/commit flags, and clamped collision-suspend count. `ResetPlayerPhysiologyJob` then consumes that staged `RespawnStateDTO` as primary med-bay truth; staged route flags are accepted only with that staged target, and job scans `MedicalBayRespawnPointDTO` rows only if staged target is missing, non-finite, or unresolved. It mutates Vault in `Simulation`, records fault/dump state in `PostSimulation`, and publishes shader scalars in `VisualSync` only after active job fence is already completed. Dispatcher phases consume cached `_dataVault` only; cold `GlobalRegistry.DataVault` fallback is restricted to Awake/Start/editor utility paths. Simulation also refuses to schedule another writer over same Vault rows while prior active handle is incomplete; it returns combined dependency instead. Mesofauna consumes request/commit snapshot through existing predator cognition signal stage. `HydrodynamicKccRuntime` consumes requested or committed `SuspendCollision` packets and skips capsulecast/collision resolution for one snapshot generation.
Cadence:
Dirty-only on death requests. Fade update runs only while pending request or active fade exists.
Expected max events/reads per frame:
Core `GlobalSignals` configures `PlayerRespawnSignal` for expected capacity 8, max frame signals 16, low-tier frame signals 4, stable hash `0x5253504E`, direct pre-simulation flush, post-simulation clear, finite guard, 96-byte layout validation, and AOT preservation. Normal expected traffic is 0 or 1 player death request per frame.
GlobalQualityWeight behavior:
`RespawnFadeDTO` consumes continuous `GlobalQualityWeight`: low weight accelerates fade decay and collapses shader detail to blackout/grain scalars; high weight preserves longer chromatic/grain cover for Visual Overkill without changing authoritative simulation.
Physics collision suspend:
`PlayerRespawnSignalFlags.SuspendCollision` does not call Physiology from Physics. KCC reads contract lane, accepts request or committed respawn packets, latches one bypass frame by `SignalBus .SnapshotGeneration`, skips `CapsulecastCommand.ScheduleBatch`, bypasses hit extraction, and marks `FlagRespawnCollisionBypass` in debug/telemetry flags. snapshot-generation latch prevents duplicate extension.
Payload/data shape:
`PlayerRespawnSignal` is unmanaged, explicit 96 bytes: death AUP, respawn AUP, hashes, frame, sequence, flags, phase bytes, and padding. Vault DTOs are explicit 16/32/64-byte unmanaged rows.
Payload managed fields present: no. Runtime managed fields are cold-only path strings, dispatcher adapter objects, and cached service handles; no persistent managed collection owns gameplay state.
UnityEngine.Object fields present: no serialized or hot-path object reference fields.
Hot-path allocation proof:
`ShinobuRespawnJobs.cs` contains no literal `new` after polish pass. `ShinobuRespawnReconciliationRuntime.ScheduleSimulation` and `VisualSyncTick` now build job structs and shader payloads through `default` field assignment. Remaining runtime `new` hits are cold host/dispatcher adapter creation, cold file IO for CSV/dump, and stack-only `Span ` constructor in cold CSV ingest. Remaining `JobHandle.Complete()` calls are cold mock-medbay boot generation and teardown/service-replacement fences, not per-frame VisualSync or Simulation stalls.
Death-vicinity hygiene:
Mutable `VitalWarningSignal`, `PhysiologyStateSignal`, and `SurvivalVitalsChangedSignal` publishers in health/survival vicinity use `default` field assignment, not object-initializer `new`. `SurvivalDatabaseItemRecord` no longer uses `Pack=1`; it is explicit 24 bytes with manual `uint _pad0` at offset `20`.
Layout proof:
`RespawnStateDTO` 32 bytes, `RespawnRequestDTO` 64 bytes, `MedicalBayRespawnPointDTO` 64 bytes, `RespawnFadeDTO` 32 bytes, `RespawnTuningDTO` 64 bytes, `InventoryDeathPenaltyRuleDTO` 16 bytes, `RespawnTelemetryEntry` 64 bytes, `RespawnTelemetryCursor64` 64 bytes. `PlayerRespawnSignal` is 96 bytes because it carries two `double3` AUP values. `InventoryCommandSignal` remains 32 bytes and now uses offsets `14/16/20/24/28` for penalty payload metadata.
Capacity:
Vault IDs `71604..71613`: state[1], med bays[8], fade[1], telemetry[300], cursor[1], tuning[1], `InventoryDeathPenaltyRuleDTO` penalty rules[64], rule count[1], CSV scratch[32768], request[1]. Inventory receives rule table through `InventoryCommandSignal.Payload0=71610`, `Payload1=ruleCount`, `Payload2=capacity`, and `Payload3=SHINOBU_155 source hash`. XML NativeHashMap wording is implemented as fixed Vault row table to preserve Vault ownership, deterministic bounded lookup, and blittable rollback/memcpy behavior.
Overflow/failure mode:
If signal lane refuses request, health reconciliation is not applied. If medical bay validation fails, target falls back to deterministic lifepod AUP and flags `FallbackLifepod`/`InvalidTargetAup`. NaN/invalid AUP writes set black-box fault flags and trigger `Docs/AgentLogs/Dump_SHINOBU_155.bin` plus XML compatibility alias `Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin`.
NaN guard status:
`ResetPlayerPhysiologyJob.WriteKinematic()` guards `target / sectorSize` with `math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)`. Both SHINOBU local AUP conversion helpers now use `SafeAupClampMeters()` before clamping and casting AUP deltas to `float3`. `math.rsqrt` is guarded with `math.max(lengthSq, 0.0001f)`.
Telemetry fields:
Death AUP, respawn AUP, cause hash, frame, schedule/reconcile microseconds, flags.
Black-box fields:
300-frame `RespawnTelemetryEntry` ring plus `RespawnTelemetryCursor64` false-sharing padded cursor.
Profiler marker:
No explicit ProfilerMarker in this patch. Runtime proof must capture dispatcher/Burst job timing before acceptance.
GC proof required:
Unity Profiler/GCMonitor 0 B/frame during no-death steady state and during one death reconciliation frame.
Shutdown/disposal rule:
Vault owns native buffers. Runtime owns only handles and cold dispatcher adapters; handles are cleared on disable/hot-swap. Active job fence is registered with `H8Memory`.
Scene unload behavior:
No scene reload or unload is requested by this route. Runtime host is `DontSave`; scene unload must unregister dispatcher adapters and clear cached handles.
Stale-handle behavior:
On DataVault replacement, active work is fenced, handles are cleared, defaults are rehydrated from new Vault, and fault dump state is reset.
Rejected alternatives:
[x] owner-local field
[x] cached owner interface
[x] existing SignalBus lane
[x] existing Vault buffer
[x] cold HectonEventBus hook
[x] no global route needed
Why this does not increase global monolith risk:
only new contract payload is one unmanaged signal with narrow death-reconciliation purpose. Persistent data stays in owner-local Vault IDs and does not modify global `BufferID` enum or sibling asmdef references.
H-Phi impact expected:
Persistent death-reconciliation state moves out of scene objects and into Vault-owned unmanaged rows. Runtime logic remains stateless dispatcher owner around Burst jobs.
Runtime proof required before acceptance:
Unity import/Console, Burst compile, Play Mode death trigger, one-frame KCC collision-bypass proof, GCMonitor 0 B/frame, Profiler timing, shader fade visual capture, and black-box dump validation on injected invalid AUP.
Reviewer: SHINOBU_155 self-review
Status: YELLOW
Global authority review:
Result: YELLOW
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Instrument: `SignalBus ` + `GlobalDataVault`
Reason: Route is narrow and statically bounded, but runtime/Unity/profiler proof is absent.
Required fixes: Fresh Unity import/Console, profiler, and GC proof before `GREEN`.
Proof still missing: Unity import, Burst compile, Play Mode, KCC one-frame bypass capture, shader visual proof, GCMonitor, player-build proof.
Reviewer: SHINOBU_155 self-review
Date: 2026-05-19
Compile blocker:
Earlier guarded `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed before SHINOBU code on `CS2001` for deleted `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, then current architecture ledger recorded that `Directory.Build.targets` shields that stale generated include. follow-up guarded Core compile now advances to external missing contract/source bridge semantic errors outside SHINOBU_155 ownership. No cross-domain stubs or generated project edits were made by this lane.
===== END FILE: Route_SHINOBU_155_Respawn__late2.md =====

===== FILE: Route_SHINOBU_155_Respawn__late9.md =====
Route_SHINOBU_155_Respawn
Date: 2026-05-19
Status: PENDING VERIFICATION - COMPILE BLOCKED BY EXTERNAL CONTRACT BRIDGE ERRORS
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Owner domain: ECHELON 5 - Combat & Survival Physiology
Owning file/system: `ShinobuRespawnReconciliationRuntime`
Problem:
Fatal player death must not reload scene, instantiate replacement player, or run managed cutscene. It must reconcile player AUP, physiology, metabolism, inventory penalty, shader fade, and AI target state through deterministic unmanaged routes.
Why owner-local data is insufficient:
death event originates in gameplay/survival, but authoritative mutation touches Physiology Vault rows, player kinematic AUP, rendering shader globals, inventory command traffic, and mesofauna target state. Keeping that state in one MonoBehaviour would create direct sibling dependencies and stale local ownership.
Why direct caller/owner interface is insufficient:
There are multiple consumers and they live behind different asmdef/domain boundaries. direct interface from Gameplay to Physiology plus Fauna plus Rendering would break Compile Wall and would not provide same-frame black-boxable contract payload.
Instrument:
[ ] GlobalRegistry cold service/interface
[x] SignalBus first-party broadcast
[x] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase:
Gameplay/survival fatal damage emits only `SignalBus ` when lethal state is detected. It does not write shader globals. Death-adjacent health/survival signal timestamps use `TimeSliceScheduler.CurrentFrameId`, not Unity `Time.frameCount`.
On successful reconciliation, legacy managed `OnDeath` and `PlayerDiedEvent` fallback side effects are skipped; they remain only for unreconciled failure.
Consumer phase:
`ShinobuRespawnReconciliationRuntime` consumes request in `PreSimulation`, resolves med-bay AUP, writes Vault request/state rows, and mutates current `PlayerRespawnSignal` snapshot in-place so same-frame consumers see resolved `RespawnAUP`, `MedicalBayHashID`, request/commit flags, and clamped collision-suspend count. `ResetPlayerPhysiologyJob` then consumes that staged `RespawnStateDTO` as primary med-bay truth; staged route flags are accepted only with that staged target, and job scans `MedicalBayRespawnPointDTO` rows only if staged target is missing, non-finite, or unresolved. It mutates Vault in `Simulation`, records fault/dump state in `PostSimulation`, and publishes shader scalars in `VisualSync` only after active job fence is already completed. Dispatcher phases consume cached `_dataVault` only; cold `GlobalRegistry.DataVault` fallback is restricted to Awake/Start/editor utility paths. Dear Lie shader publish uses `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie(IDataVault, Vector4)` with cached `_dataVault`, and local dirty latch publishes only while active plus one zero-clear frame after fade end. Simulation also refuses to schedule another writer over same Vault rows while prior active handle is incomplete; it returns combined dependency instead. Mesofauna consumes request/commit snapshot through existing predator cognition signal stage. `HydrodynamicKccRuntime` consumes requested or committed `SuspendCollision` packets and skips capsulecast/collision resolution for one snapshot generation.
Cadence:
Dirty-only on death requests. Fade update runs only while pending request or active fade exists. VisualSync shader publication is also dirty-only: active fade frames publish payload, first inactive frame after active fade publishes zero, and later idle frames return before bridge.
Expected max events/reads per frame:
Core `GlobalSignals` configures `PlayerRespawnSignal` for expected capacity 8, max frame signals 16, low-tier frame signals 4, stable hash `0x5253504E`, direct pre-simulation flush, post-simulation clear, finite guard, 96-byte layout validation, and AOT preservation. Normal expected traffic is 0 or 1 player death request per frame.
GlobalQualityWeight behavior:
`RespawnFadeDTO` consumes continuous `GlobalQualityWeight`: low weight accelerates fade decay and collapses shader detail to blackout/grain scalars; high weight preserves longer chromatic/grain cover for Visual Overkill without changing authoritative simulation.
Physics collision suspend:
`PlayerRespawnSignalFlags.SuspendCollision` does not call Physiology from Physics. KCC reads contract lane, accepts request or committed respawn packets, latches one bypass frame by `SignalBus .SnapshotGeneration`, skips `CapsulecastCommand.ScheduleBatch`, bypasses hit extraction, and marks `FlagRespawnCollisionBypass` in debug/telemetry flags. snapshot-generation latch prevents duplicate extension.
Payload/data shape:
`PlayerRespawnSignal` is unmanaged, explicit 96 bytes: death AUP, respawn AUP, hashes, frame, sequence, flags, phase bytes, and padding. Vault DTOs are explicit 16/32/64-byte unmanaged rows.
Payload managed fields present: no. Runtime managed fields are cold-only path strings, dispatcher adapter objects, and cached service handles; no persistent managed collection owns gameplay state.
UnityEngine.Object fields present: no serialized or hot-path object reference fields.
Hot-path allocation proof:
`ShinobuRespawnJobs.cs` contains no literal `new` after polish pass. `ShinobuRespawnReconciliationRuntime.ScheduleSimulation` and `VisualSyncTick` now build job structs and shader payloads through `default` field assignment. Core shader bridge's respawn conversion helpers also use `default` field assignment instead of typed `new float4`/`new Vector4`. Remaining runtime `new` hits are cold host/dispatcher adapter creation, cold file IO for CSV/dump, and stack-only `Span ` constructor in cold CSV ingest. Remaining `JobHandle.Complete()` calls are cold mock-medbay boot generation and teardown/service-replacement fences, not per-frame VisualSync or Simulation stalls.
Death-vicinity hygiene:
Mutable `VitalWarningSignal`, `PhysiologyStateSignal`, and `SurvivalVitalsChangedSignal` publishers in health/survival vicinity use `default` field assignment, not object-initializer `new`. `SurvivalDatabaseItemRecord` no longer uses `Pack=1`; it is explicit 24 bytes with manual `uint _pad0` at offset `20`.
Layout proof:
`RespawnStateDTO` 32 bytes, `RespawnRequestDTO` 64 bytes, `MedicalBayRespawnPointDTO` 64 bytes, `RespawnFadeDTO` 32 bytes, `RespawnTuningDTO` 64 bytes, `InventoryDeathPenaltyRuleDTO` 16 bytes, `RespawnTelemetryEntry` 64 bytes, `RespawnTelemetryCursor64` 64 bytes. `PlayerRespawnSignal` is 96 bytes because it carries two `double3` AUP values. `InventoryCommandSignal` remains 32 bytes and now uses offsets `14/16/20/24/28` for penalty payload metadata.
Capacity:
Vault IDs `71604..71613`: state[1], med bays[8], fade[1], telemetry[300], cursor[1], tuning[1], `InventoryDeathPenaltyRuleDTO` penalty rules[64], rule count[1], CSV scratch[32768], request[1]. Inventory receives rule table through `InventoryCommandSignal.Payload0=71610`, `Payload1=ruleCount`, `Payload2=capacity`, and `Payload3=SHINOBU_155 source hash`. XML NativeHashMap wording is implemented as fixed Vault row table to preserve Vault ownership, deterministic bounded lookup, and blittable rollback/memcpy behavior.
Overflow/failure mode:
If signal lane refuses request, health reconciliation is not applied. If medical bay validation fails, target falls back to deterministic lifepod AUP and flags `FallbackLifepod`/`InvalidTargetAup`. NaN/invalid AUP writes set black-box fault flags and trigger `Docs/AgentLogs/Dump_SHINOBU_155.bin` plus XML compatibility alias `Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin`.
NaN guard status:
`ResetPlayerPhysiologyJob.WriteKinematic()` guards `target / sectorSize` with `math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)`. Both SHINOBU local AUP conversion helpers now use `SafeAupClampMeters()` before clamping and casting AUP deltas to `float3`. `math.rsqrt` is guarded with `math.max(lengthSq, 0.0001f)`.
Telemetry fields:
Death AUP, respawn AUP, cause hash, frame, schedule/reconcile microseconds, flags.
Black-box fields:
300-frame `RespawnTelemetryEntry` ring plus `RespawnTelemetryCursor64` false-sharing padded cursor.
Profiler marker:
No explicit ProfilerMarker in this patch. Runtime proof must capture dispatcher/Burst job timing before acceptance.
GC proof required:
Unity Profiler/GCMonitor 0 B/frame during no-death steady state and during one death reconciliation frame.
Shutdown/disposal rule:
Vault owns native buffers. Runtime owns only handles and cold dispatcher adapters; handles are cleared on disable/hot-swap. Active job fence is registered with `H8Memory`.
Scene unload behavior:
No scene reload or unload is requested by this route. Runtime host is `DontSave`; scene unload must unregister dispatcher adapters and clear cached handles.
Stale-handle behavior:
On DataVault replacement, active work is fenced, handles are cleared, defaults are rehydrated from new Vault, and fault dump state is reset.
Rejected alternatives:
[x] owner-local field
[x] cached owner interface
[x] existing SignalBus lane
[x] existing Vault buffer
[x] cold HectonEventBus hook
[x] no global route needed
Why this does not increase global monolith risk:
only new contract payload is one unmanaged signal with narrow death-reconciliation purpose. Persistent data stays in owner-local Vault IDs and does not modify global `BufferID` enum or sibling asmdef references.
H-Phi impact expected:
Persistent death-reconciliation state moves out of scene objects and into Vault-owned unmanaged rows. Runtime logic remains stateless dispatcher owner around Burst jobs.
Runtime proof required before acceptance:
Unity import/Console, Burst compile, Play Mode death trigger, one-frame KCC collision-bypass proof, GCMonitor 0 B/frame, Profiler timing, shader fade visual capture, and black-box dump validation on injected invalid AUP.
Reviewer: SHINOBU_155 self-review
Status: YELLOW
Global authority review:
Result: YELLOW
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Instrument: `SignalBus ` + `GlobalDataVault`
Reason: Route is narrow and statically bounded, but runtime/Unity/profiler proof is absent.
Required fixes: Fresh Unity import/Console, profiler, and GC proof before `GREEN`.
Proof still missing: Unity import, Burst compile, Play Mode, KCC one-frame bypass capture, shader visual proof, GCMonitor, player-build proof.
Reviewer: SHINOBU_155 self-review
Date: 2026-05-19
Compile blocker:
Earlier guarded `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed before SHINOBU code on `CS2001` for deleted `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, then current architecture ledger recorded that `Directory.Build.targets` shields that stale generated include. follow-up guarded Core compile now advances to external missing contract/source bridge semantic errors outside SHINOBU_155 ownership. No cross-domain stubs or generated project edits were made by this lane.
===== END FILE: Route_SHINOBU_155_Respawn__late9.md =====

===== FILE: Route_SHINOBU_155_Respawn__watch22.md =====
Route_SHINOBU_155_Respawn
Date: 2026-05-19
Status: PENDING VERIFICATION - COMPILE BLOCKED BY EXTERNAL CONTRACT BRIDGE ERRORS
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Owner domain: ECHELON 5 - Combat & Survival Physiology
Owning file/system: `ShinobuRespawnReconciliationRuntime`
Problem:
Fatal player death must not reload scene, instantiate replacement player, or run managed cutscene. It must reconcile player AUP, physiology, metabolism, inventory penalty, shader fade, and AI target state through deterministic unmanaged routes.
Why owner-local data is insufficient:
death event originates in gameplay/survival, but authoritative mutation touches Physiology Vault rows, player kinematic AUP, rendering shader globals, inventory command traffic, and mesofauna target state. Keeping that state in one MonoBehaviour would create direct sibling dependencies and stale local ownership.
Why direct caller/owner interface is insufficient:
There are multiple consumers and they live behind different asmdef/domain boundaries. direct interface from Gameplay to Physiology plus Fauna plus Rendering would break Compile Wall and would not provide same-frame black-boxable contract payload.
Instrument:
[ ] GlobalRegistry cold service/interface
[x] SignalBus first-party broadcast
[x] GlobalSignals bridge/direct queue
[ ] HectonEventBus mod/API/cold event
[x] GlobalDataVault / IDataVault
[x] Black-box/telemetry route
Producer phase:
Gameplay/survival fatal damage emits only `SignalBus ` when lethal state is detected. It does not write shader globals. Death-adjacent health/survival signal timestamps use `TimeSliceScheduler.CurrentFrameId`, not Unity `Time.frameCount`.
On successful reconciliation, legacy managed `OnDeath` and `PlayerDiedEvent` fallback side effects are skipped; they remain only for unreconciled failure.
Consumer phase:
`ShinobuRespawnReconciliationRuntime` consumes request in `PreSimulation`, resolves med-bay AUP, writes Vault request/state rows, and mutates current `PlayerRespawnSignal` snapshot in-place so same-frame consumers see resolved `RespawnAUP`, `MedicalBayHashID`, request/commit flags, and clamped collision-suspend count. `ResetPlayerPhysiologyJob` then consumes that staged `RespawnStateDTO` as primary med-bay truth; staged route flags are accepted only with that staged target, and job scans `MedicalBayRespawnPointDTO` rows only if staged target is missing, non-finite, or unresolved. It mutates Vault in `Simulation`, records fault/dump state in `PostSimulation`, and publishes shader scalars in `VisualSync` only after active job fence is already completed. Dispatcher phases consume cached `_dataVault` only and use `HasHotVaultState()` so they never request Vault buffers; allocation-capable `EnsureVaultState(...)` is restricted to Awake/Start/DataVault hot-swap/editor utility paths. Dear Lie shader publish uses `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie(IDataVault, Vector4)` with cached `_dataVault`, and local dirty latch publishes only while active plus one zero-clear frame after fade end. Simulation also refuses to schedule another writer over same Vault rows while prior active handle is incomplete; it returns combined dependency instead. Mesofauna consumes request/commit snapshot through existing predator cognition signal stage. `HydrodynamicKccRuntime` consumes requested or committed `SuspendCollision` packets and skips capsulecast/collision resolution for one snapshot generation.
Cadence:
Dirty-only on death requests. Fade update runs only while pending request or active fade exists. VisualSync shader publication is also dirty-only: active fade frames publish payload, first inactive frame after active fade publishes zero, and later idle frames return before bridge.
Expected max events/reads per frame:
Core `GlobalSignals` configures `PlayerRespawnSignal` for expected capacity 8, max frame signals 16, low-tier frame signals 4, stable hash `0x5253504E`, direct pre-simulation flush, post-simulation clear, finite guard, 96-byte layout validation, and AOT preservation. Normal expected traffic is 0 or 1 player death request per frame.
GlobalQualityWeight behavior:
`RespawnFadeDTO` consumes continuous `GlobalQualityWeight`: low weight accelerates fade decay and collapses shader detail to blackout/grain scalars; high weight preserves longer chromatic/grain cover for Visual Overkill without changing authoritative simulation.
Physics collision suspend:
`PlayerRespawnSignalFlags.SuspendCollision` does not call Physiology from Physics. KCC reads contract lane, accepts request or committed respawn packets, latches one bypass frame by `SignalBus .SnapshotGeneration`, skips `CapsulecastCommand.ScheduleBatch`, bypasses hit extraction, and marks `FlagRespawnCollisionBypass` in debug/telemetry flags. snapshot-generation latch prevents duplicate extension.
Payload/data shape:
`PlayerRespawnSignal` is unmanaged, explicit 96 bytes: death AUP, respawn AUP, hashes, frame, sequence, flags, phase bytes, and padding. Vault DTOs are explicit 16/32/64-byte unmanaged rows.
Payload managed fields present: no. Runtime managed fields are cold-only path strings, dispatcher adapter objects, and cached service handles; no persistent managed collection owns gameplay state.
UnityEngine.Object fields present: no serialized or hot-path object reference fields.
Hot-path allocation proof:
`ShinobuRespawnJobs.cs` contains no literal `new` after polish pass. `ShinobuRespawnReconciliationRuntime.ScheduleSimulation` and `VisualSyncTick` now build job structs and shader payloads through `default` field assignment. Hot dispatcher phases use `HasHotVaultState()` and cannot allocate/request Vault buffers; cold `EnsureVaultState(...)` remains boot/editor/hot-swap only. `HectonShaderGlobalDataVaultBridge.cs` now has no typed `new float4`/`new Vector4`; its vector constants, mask packing, conversion helpers, and reset payloads use explicit field assignment helpers. Remaining runtime `new` hits are cold host/dispatcher adapter creation, cold file IO for CSV/dump, and stack-only `Span ` constructor in cold CSV ingest. Remaining `JobHandle.Complete()` calls are cold mock-medbay boot generation and teardown/service-replacement fences, not per-frame VisualSync or Simulation stalls.
Death-vicinity hygiene:
Mutable `VitalWarningSignal`, `PhysiologyStateSignal`, and `SurvivalVitalsChangedSignal` publishers in health/survival vicinity use `default` field assignment, not object-initializer `new`. `SurvivalDatabaseItemRecord` no longer uses `Pack=1`; it is explicit 24 bytes with manual `uint _pad0` at offset `20`.
Layout proof:
`RespawnStateDTO` 32 bytes, `RespawnRequestDTO` 64 bytes, `MedicalBayRespawnPointDTO` 64 bytes, `RespawnFadeDTO` 32 bytes, `RespawnTuningDTO` 64 bytes, `InventoryDeathPenaltyRuleDTO` 16 bytes, `RespawnTelemetryEntry` 64 bytes, `RespawnTelemetryCursor64` 64 bytes. `PlayerRespawnSignal` is 96 bytes because it carries two `double3` AUP values. `InventoryCommandSignal` remains 32 bytes and now uses offsets `14/16/20/24/28` for penalty payload metadata.
Capacity:
Vault IDs `71604..71613`: state[1], med bays[8], fade[1], telemetry[300], cursor[1], tuning[1], `InventoryDeathPenaltyRuleDTO` penalty rules[64], rule count[1], CSV scratch[32768], request[1]. Inventory receives rule table through `InventoryCommandSignal.Payload0=71610`, `Payload1=ruleCount`, `Payload2=capacity`, and `Payload3=SHINOBU_155 source hash`. XML NativeHashMap wording is implemented as fixed Vault row table to preserve Vault ownership, deterministic bounded lookup, and blittable rollback/memcpy behavior.
Overflow/failure mode:
If signal lane refuses request, health reconciliation is not applied. If medical bay validation fails, target falls back to deterministic lifepod AUP and flags `FallbackLifepod`/`InvalidTargetAup`. NaN/invalid AUP writes set black-box fault flags and trigger `Docs/AgentLogs/Dump_SHINOBU_155.bin` plus XML compatibility alias `Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin`.
NaN guard status:
`ResetPlayerPhysiologyJob.WriteKinematic()` guards `target / sectorSize` with `math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)`. Both SHINOBU local AUP conversion helpers now use `SafeAupClampMeters()` before clamping and casting AUP deltas to `float3`. `math.rsqrt` is guarded with `math.max(lengthSq, 0.0001f)`.
Telemetry fields:
Death AUP, respawn AUP, cause hash, frame, schedule/reconcile microseconds, flags.
Black-box fields:
300-frame `RespawnTelemetryEntry` ring plus `RespawnTelemetryCursor64` false-sharing padded cursor.
Profiler marker:
No explicit ProfilerMarker in this patch. Runtime proof must capture dispatcher/Burst job timing before acceptance.
GC proof required:
Unity Profiler/GCMonitor 0 B/frame during no-death steady state and during one death reconciliation frame.
Shutdown/disposal rule:
Vault owns native buffers. Runtime owns only handles and cold dispatcher adapters; handles are cleared on disable/hot-swap. Active job fence is registered with `H8Memory`.
Scene unload behavior:
No scene reload or unload is requested by this route. Runtime host is `DontSave`; scene unload must unregister dispatcher adapters and clear cached handles.
Stale-handle behavior:
On DataVault replacement, active work is fenced, handles are cleared, defaults are rehydrated from new Vault, and fault dump state is reset.
Rejected alternatives:
[x] owner-local field
[x] cached owner interface
[x] existing SignalBus lane
[x] existing Vault buffer
[x] cold HectonEventBus hook
[x] no global route needed
Why this does not increase global monolith risk:
only new contract payload is one unmanaged signal with narrow death-reconciliation purpose. Persistent data stays in owner-local Vault IDs and does not modify global `BufferID` enum or sibling asmdef references.
H-Phi impact expected:
Persistent death-reconciliation state moves out of scene objects and into Vault-owned unmanaged rows. Runtime logic remains stateless dispatcher owner around Burst jobs.
Runtime proof required before acceptance:
Unity import/Console, Burst compile, Play Mode death trigger, one-frame KCC collision-bypass proof, GCMonitor 0 B/frame, Profiler timing, shader fade visual capture, and black-box dump validation on injected invalid AUP.
Reviewer: SHINOBU_155 self-review
Status: YELLOW
Global authority review:
Result: YELLOW
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Instrument: `SignalBus ` + `GlobalDataVault`
Reason: Route is narrow and statically bounded, but runtime/Unity/profiler proof is absent.
Required fixes: Fresh Unity import/Console, profiler, and GC proof before `GREEN`.
Proof still missing: Unity import, Burst compile, Play Mode, KCC one-frame bypass capture, shader visual proof, GCMonitor, player-build proof.
Reviewer: SHINOBU_155 self-review
Date: 2026-05-19
Compile blocker:
Earlier guarded `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed before SHINOBU code on `CS2001` for deleted `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, then current architecture ledger recorded that `Directory.Build.targets` shields that stale generated include. follow-up guarded Core compile now advances to external missing contract/source bridge semantic errors outside SHINOBU_155 ownership. No cross-domain stubs or generated project edits were made by this lane.
===== END FILE: Route_SHINOBU_155_Respawn__watch22.md =====
