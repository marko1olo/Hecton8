# HECTON-8 Persistence Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: save/load, binary state, deltas, checksums, migrations, black-box dumps, world persistence, and failure recovery.

## Prime Law

Persistence is not a convenience feature. It is the evidence trail of HECTON-8. A save must preserve player decisions, world scars, resource truth, damage state, voxel edits, mission evidence, and black-box records without turning runtime into a managed heap.

## Save Identity

Every persistent fact needs:

- stable owner;
- stable ID;
- schema version;
- authority route;
- serialization cadence;
- checksum or validation field;
- migration behavior;
- corruption fallback.

Object names, scene hierarchy order, generated random values, and transient instance IDs are not save identity.

## Truth Ownership

Persistence stores facts owned by gameplay domains. It does not invent, repair, or reinterpret those facts unless a migration explicitly says so.

Each saved section must name:

- owning domain;
- schema version;
- stable ID route;
- serialization cadence;
- migration route;
- corruption fallback;
- black-box or telemetry tie-in for failure.

Loading must restore owner-approved truth first. Presentation rebuilds after truth is restored.

## Binary Delta Law

Hot or large save domains use binary delta layouts, not reflective JSON dumps. Text formats are allowed for editor manifests, diagnostics, and authoring files, not for high-volume runtime state.

Required for large domains:

- fixed header;
- version;
- chunk or domain ID;
- entry count;
- payload length;
- checksum (`XXHash3` 64-bit validation);
- compressed block where justified;
- async write path (Unity 6 `Awaitable` background thread);
- `MainThreadSnapshotBudgetMs = 5L` (5 ms main thread snapshot budget ceiling);
- crash-safe temp file then atomic replace.

`SaveManager.cs` strictly enforces `ISaveable` registration with zero GC runtime allocations. Dynamic managed collections (e.g. `Dictionary<K,V>`) inside `SaveData.cs` root payloads are prohibited; serialization must use `ISerializationCallbackReceiver` with parallel flat arrays.

## 2026-06-05 H8BinaryWorldPager Source Anchor

Evidence class: STATIC_SOURCE / STATIC_DOC. Runtime proof is not implied.

Source: `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs`.

Owner and route:

- Owner: `Hecton8.Core.Persistence.Paging.H8BinaryWorldPager`.
- DataVault owner id: `SystemID.SavePersistence`.
- Storage route: `world_data.h8bin` with write-ahead log `h8_delta.wal`.
- File format markers visible in source: page magic `H8PG`, WAL magic `H8WL`, `PageVersion = 1`, `WalVersion = 1`.
- Static capacities: `SectorSizeBytes = 256 KB`, `SectorHeaderBytes = 64`, `MaxSectors = 8192`, `WriteSlotCount = 8`, `ReadSlotCount = 4`, `QueueCapacity = 64`, telemetry `Capacity = 300`.

Phase boundary:

- The class starts a background worker thread named `H8 Binary World Pager` for queued writes, reads, dump requests, and flush requests.
- Caller-side phase is not self-declared in this file. Any owner using `TryEnqueueWrite`, `TryRequestRead`, `TryCopyCompletedPage`, `TryRetireCompletedPage`, or direct read staging must document PRE_SIMULATION/SIMULATION/POST_SIMULATION/VISUAL_SYNC ownership before acceptance.
- `GlobalRegistry.DataVault` appears in allocation/setup and the class implements DataVault hot-swap cleanup; this is not a hot polling pattern.

DataVault and native lifetime:

- DataVault handles: `BufferID.SaveWorldPagerReadStaging` and `BufferID.SaveWorldPagerTelemetryRing`.
- DataVault write-lock helpers release locks in `finally`.
- Current source also contains local persistent `PagerNativeState` native arrays registered with `NativeMemorySentinel`. This is source reality for this pager only, not approval for other systems to bypass DataVault sovereignty.

Fault boundary:

- Telemetry record: `H8BinaryWorldPagerTelemetryEntry` stores sector hash, offset, UTC ticks, payload type, frame, request id, payload bytes, pending read/write counts, page faults, metrics, directory slot, flags, operation, and status.
- Declared dump files: `Dump_1312_VoxelPaging.bin`, `Dump_CRASH.bin`, `Dump_SAVE_SURGEON.h8dump`, and `Dump_CRASH.h8dump`.
- Static source now writes bounded black-box artifacts with a 64-byte `H8DP` header plus fixed telemetry-entry payload, temp-file write-through, critical flush/length validation, cached-read invalidation, and atomic promote. Crash-named mirrors are gated to pager fault/dispose paths. Runtime dump artifact generation remains PENDING VERIFICATION.

Hot-path prohibitions:

- No UI button, gameplay Tick, or presentation path may block on pager FileStream, WAL, worker join, direct page copy, or dump output.
- No caller may treat a missing page as silent world truth repair; status must route to the owning persistence/streaming failure policy.
- No runtime report may claim `0 GC`, save/load readiness, WAL safety, or corruption recovery from this source anchor alone.

Missing proof artifacts:

- compile/import result for the pager assembly.
- write/read/copy roundtrip for `world_data.h8bin`.
- WAL append/replay/truncation/corruption recovery artifact.
- DataVault stale-handle/relocation/hot-swap test artifact.
- actual 300-entry dump artifact for pager telemetry.
- GC/profiler capture for enqueue, worker drain, direct read staging, flush, and shutdown.
- player save/load proof on the target file-system path.

## World Scars

The save must preserve evidence:

- opened doors;
- repaired or failed machines;
- drained or flooded compartments;
- cut panels;
- harvested salvage;
- voxel edits;
- found bodies or logs;
- black-box records;
- discovered route marks;
- creature or hazard state where gameplay-relevant.

If the world forgets visible consequences, the game becomes fake.

Death/respawn persistence lock: ordinary player death respawns at the current valid base, safe anchor, or approved recovery point. Carried resources may drop into a recoverable container/world package similar to Minecraft/Subnautica loss pressure. Core tools remain with the player unless a specific authored event or system rule explicitly says otherwise. Save/load must preserve dropped-resource packages, death/recovery evidence, and tool ownership without turning death into either a free teleport or a full wipe.

## Voxel And Generated Asset Persistence

Voxel terrain stores deltas from deterministic seed state. Generated assets store seed, family, version, material set, collider/proof metadata, and any player-caused persistent damage or modification. Do not serialize full generated meshes unless the mesh cannot be reconstructed from deterministic source and delta data.

## Save Cadence

Save work is not a random frame event. It must be scheduled, bounded, and visible to the owner system.

Allowed:

- checkpoint writes;
- safe-room or dock writes;
- low-cadence autosaves after stable state;
- explicit black-box dump on crash or NaN;
- async domain chunk writes.

Forbidden:

- main-thread blocking save spikes;
- save writes from UI button code without persistence owner route;
- serializing entire scene graphs;
- saving presentation-only state as truth;
- silent corruption recovery.

Operational defaults:

- manual slots are `slot_0`, `slot_1`, and `slot_2` unless current source proves a migrated slot table;
- primary save writes use `.sav`, backup uses `.sav.bak`, and temp writes use `.sav.tmp` or the owner-specific temp sector/page extension documented by the save route;
- autosave cadence must not be faster than 30 seconds unless a route bible/source owner proves an emergency black-box or checkpoint exception;
- `SaveDataMigration` or its current source-backed successor owns schema migration; do not invent ad hoc load-time reinterpretation in callers.

Load application bands:

- `0-10`: Core/bootstrap/session identity;
- `11-50`: World, terrain, voxel, streaming, and global route state;
- `51-100`: Player, vehicle, tools, survival, and camera handoff state;
- `101-200`: Inventory, construction, crafting, power, logistics, and economy;
- `201+`: UI, presentation rebuild, optional diagnostics, and non-authoritative view state.

Two `ISaveable` owners with the same `LoadPriority` are rejected when a dependency exists between them. `LoadFromSaveData` must check key/section presence and apply a documented default for missing optional data; missing data must not throw as normal flow.

## Quality Scaling

`GlobalQualityWeight` may scale optional save diagnostics, checkpoint presentation, compression aggressiveness chosen during cold save windows, and black-box export verbosity. It must not change save identity, schema version, checksum meaning, gameplay truth, migration route, or whether a player decision is preserved.

## First-20 Route Hook

- First-20 moment: save/load must restore position, inventory/resource chain, opened/looted/scanned flags, tool/world changes, hazard state, death/drop recovery state, and route evidence for the selected opening route.
- Route blocker removed: persistence cannot claim route readiness from schema text, static source, or UI save buttons without a restored-state artifact.
- Proof class: save/load artifact, Play Mode/player capture for roundtrip, Profiler/GCMonitor for save/load runtime paths, and static-only schema review when no runtime path changed.

## Migration

Every schema change must define:

- from-version;
- to-version;
- field mapping;
- default values;
- rejected/removed fields;
- validation after migration;
- test asset or sample blob.

If migration cannot be written, the schema change is not ready.

## Rejection Gates

Reject:

- save identity based on GameObject names;
- reflection-heavy serialization on hot paths;
- missing checksum;
- no corruption fallback;
- no black-box dump path;
- persistence that changes gameplay truth during load without owner approval;
- save reports without file path, schema version, size, write time, and validation result.

## Proof Artifacts

Persistence work must provide:

- save file path and byte size;
- schema version and migration range;
- checksum/hash result;
- write/read/corruption/migration test artifact;
- domain section list;
- async/cadence route;
- black-box dump behavior for failure;
- player or Play Mode proof for gameplay-facing state restoration;
- explicit `PENDING VERIFICATION` if only static source was inspected.

## Acceptance Sentence

Persistence is accepted only when the save file restores the same physical consequences, survives corruption boundaries, preserves evidence, and costs predictable time and memory.
