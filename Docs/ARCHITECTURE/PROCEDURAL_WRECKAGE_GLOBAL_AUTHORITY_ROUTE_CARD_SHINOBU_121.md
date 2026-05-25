# Procedural Wreckage Global Authority Route Card - SHINOBU_121

Date: 2026-05-19

Status: YELLOW / PENDING VERIFICATION

Evidence class: STATIC_SOURCE / UNITY IMPORT PENDING

## Source Anchors

Evidence: STATIC_SOURCE / FILESYSTEM.

Scope: cited local paths exist at capture time. No compile/import/Play/profiler/GC/player/save/platform/visual proof.

- `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs`

- `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs`

- `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageGpuUploadDispatcher.cs`

- `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageContracts.cs`

- `Assets/_Project/Scripts/World/ProceduralWreckage/Editor/ProceduralWreckageLayoutValidator.cs`

## Route Card

Route ID: PROCEDURAL_WRECKAGE_VAULT_ROUTE_SHINOBU_121

Owner: SHINOBU_121

Owner domain: Echelon 2 World Generation / Procedural Wreckage Assembler

Owning file/system: `Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs`

Problem: WFC wreck generation produces cross-domain native state for save, render, and crash forensics. Owner-local arrays would hide renderer, save, rollback, collision, loot, and dump state.

Why owner-local data is insufficient: nodes, matrices, indirect args, collision proxies, loot requests, tuning, and telemetry cross scheduler phases and need stable handles.

Why direct caller/owner interface is insufficient: the data is not a one-caller request/response. Multiple consumers need immutable snapshots or staged DTOs, while Burst jobs need unmanaged buffers.

Instrument:

- [ ] GlobalRegistry cold service/interface

- [ ] SignalBus<T> first-party broadcast

- [ ] GlobalSignals bridge/direct queue

- [ ] HectonEventBus mod/API/cold event

- [x] GlobalDataVault / IDataVault

- [x] Black-box/telemetry route

Producer/consumer phase: owner-local boot resolves Vault handles; generation jobs run in the world simulation dependency chain.

Renderer, physics, inventory, save/rollback, and QA consumers read staged snapshots in owned phases.

Cadence/capacity: generation/event driven, editor CSV poll 1 Hz editor-only, GPU upload dirty-output only; Vault lanes for nodes, matrices, args, loot requests, collision proxies, and 300 telemetry rows.

Producer phase:

- Owner-local boot resolves Vault handles.
- Generation jobs run in the World simulation dependency chain.
- GPU upload waits for caller visual-sync completion of the returned `JobHandle`.

Consumer phase: renderer upload reads `RenderMatrices`, `IndirectArgs`, `GpuScalars`; physics apply reads `CollisionProxies`; inventory/scavenging owner reads `LootRequests`; save/rollback owner reads `Nodes` and sector hash; QA reads `TelemetryRing`.

Cadence: generation/event driven, not per-frame by default. Editor CSV poll is 1 Hz editor-only. GPU upload is dirty generation output only.

Expected max events/reads per frame: one active wreck generation batch per sector trigger; `MaxRenderMatrices = 5120`; `MaxCollisionProxies = 1024`; `MaxLootRequests = 512`.

GlobalQualityWeight: continuous quality drives WFC target node count, debris count, visibility distance, culling probability, and shader scalar richness. No low/high binary switch exists in the generation jobs.

Payload/data shape: explicit unmanaged DTOs only. Primary node DTO is 128 bytes; counters are 64 bytes. No managed fields. No UnityEngine.Object fields.

Managed fields present: no.

UnityEngine.Object fields present: no.

Layout proof: `ProceduralWreckageLayoutValidator` checks sizes and `WreckageNodeDTO` field offsets with `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset`.

Capacity:

- Rules 16

- Grid 1024

- Nodes 1024

- DebrisNodes 4096

- RenderMatrices 5120

- IndirectArgs 1

- SectorTriggers 1

- LootRequests 512

- CollisionProxies 1024

- TelemetryRing 300

- Tuning 1

- CsvScratch 32768 bytes

- Counters 1

- DebugCells 1024

- GpuScalars 1

- SelfAudit 1

- HzbTiles 4096

Overflow/failure:

- Jobs clamp writes to capacity.
- Fault flags are set.
- Fallback mock rules are preserved.
- Planned dumps: `Dump_WRECKAGE_ASSEMBLER.bin`, `Dump_SHINOBU_121.bin`.
- No dump implied without timestamped trigger and output.

Telemetry fields: root AUP, frame, sector hash, collapsed modules, backtrack iterations, estimated compute ms, quality, state hash, fault flags, rendered modules, debris count.

Black-box fields: 300-frame `WreckageGenerationTelemetryEntry` ring plus cursor.

Profiler marker: not added yet; static source only.

GC proof required: Unity Profiler/GCMonitor capture proving 0 B/frame for generation, extraction, and upload orchestration.

Shutdown/disposal: Vault owns native buffer lifetime; `ProceduralWreckageGpuUploadDispatcher.Dispose()` releases managed GPU resources. No persistent private NativeArray/List/HashMap exists in the SHINOBU path.

Scene unload behavior: Vault shutdown/release remains Core Memory owner responsibility; this route does not create an extra registry slot or scene singleton.

Stale-handle behavior: `TryResolveExisting` refuses missing handles after Vault allocation lock; consumers fail closed if `TryResolveViews` cannot resolve all buffers.

Rejected alternatives:

- [x] owner-local field: rejected because state crosses render/save/collision/loot/telemetry boundaries.

- [x] cached owner interface: rejected because Burst jobs and multiple consumers need native snapshots, not live calls.

- [x] existing SignalBus lane: rejected because bulk arrays/matrices are not event payloads.

- [x] existing Vault buffer: no procedural wreckage BufferID lane existed.

- [x] cold HectonEventBus hook: rejected because first-party hot/native data must not use managed event transport.

- [ ] no global route needed.

Global monolith risk is unchanged.

- Route adds no `GlobalRegistry` slot.
- Adds no signal lane or EventBus traffic.
- IDs remain local constants in the SHINOBU asmdef until integrator promotion.

H-Phi impact expected: improves DataSovereignty for wreckage generation by replacing local persistent containers with Vault handles. This is not used as readiness proof.

Proof required before GREEN: Unity import, Burst compile, layout validator menu pass, GCMonitor generation run, Frame Debugger confirmation for `DrawProceduralIndirect`, and black-box dump smoke test.

Reviewer: SHINOBU_121 self-review only

Review disposition: YELLOW

Reason: route-card fields are populated for `STATIC_SOURCE` orientation; compile/Unity/profiler proof is absent under the CPU build gate.

Required fixes before GREEN: run Unity import/compile and profiler evidence when CPU guard allows; attach artifacts.
