# Persistent World Registry Migration Plan 1325

Status: archaeological ledger for tasks 02-05. Runtime proof pending source migration and compile.

## Task 02 Ownership And Lifecycle

Formal Roslyn hit list: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1325_WORLD_BEFORE.json`.

Primary target owner: `PersistentWorldRegistry`.

Vault owner system: `SystemID.WorldStreaming`. `SystemID.WorldRegistry` does not exist in `H8Memory.cs`; adding it now would mutate shared core identity. The registry already belongs to the world-streaming domain and publishes through `GlobalRegistry.PersistentWorldRegistry`.

Cold allocation site: `PersistentWorldRegistry.Awake()`, lines 1129-1170.

Lifecycle release site: `PersistentWorldRegistry.OnDisable()`, lines 1366-1438.

Sentinel route: `RegisterNativeMemorySentinelAllocations()`, lines 5948-5972; unregister lines 5989-6012.

Data groups:
- Dropped-item residency: `_records`, `_recordsByChunk`.
- Compact save deltas: `_deltaRecords`, `_deltaRecordIndexByEntityId`, `_deltaChunkIndexByChunkId`, `_deltaChunkIds`, `_deltaItemIndexByHash`, `_deltaItemHashes`, `_deltaRecordsByChunk`, `_tombstoneDecayExpiredIndices`, `_saveSnapshotDeltas`.
- Hydration state: `_poolSlotData`, `_guidToPoolIndex`, `_dehydrateQueue`, `_pendingHydrationRecords`.
- Entity persistence state: `_entityStateByInstanceUid`, `_floraSpawnStateByInstanceUid`, `_spawnImpulseByInstanceUid`, `_spawnVelocityChangeByInstanceUid`.
- Adjacent sovereignty fields not counted by the formal 19-field scanner: `_deletedInstanceUids`, `_resourceNodeTombstoneIds`, `_resourceNodeMetamorphosedIds`.

Planned buffer ID range: explicit local constants `(BufferID)74450` through `(BufferID)74518`, currently unused in `H8Memory.cs` scan. This avoids core enum churn while producing stable descriptors.

## Task 03 Dependency Graph

External writers through registry API:
- Dropped items: `BaseModule`, `LogisticsPipeNode`, `AutonomousExtractorSystem`, `Fabricator`, `HarvestableOutcrop`, `PlayerInventory`, `PlayerToolManager`, `ThermalGeyser`, `DestructibleOrganicManager`.
- Flora/resource deltas: `DestructibleOrganicManager`, `FloraRegrowthDirector`, `ResourceDistributionDirector`, `ResourceNode`.
- Fauna state: `FaunaDirector`, `FaunaBrain`, `EcosystemDirector`.
- Save/load: `SaveManager`.

External readers:
- `SaveManager` reads `GetSaveSnapshotArray()` before binary save.
- `PlayerExplorationTracker` reads `GetSaveSnapshotArray()` for PDA exploration data.
- `DestructibleOrganicManager` and `FloraRegrowthDirector` consume `CopyDestroyedFloraDeltas`, `CopyFloraStateOverrideDeltas`, and `CopyPendingFloraSeedDeltas`.
- `EcosystemDirector` consumes tombstone/state APIs.

Public API mutation plan:
- Keep public/internal method signatures stable except `GetSaveSnapshotArray()` if unavoidable. A read accessor returning a `NativeArray<T>.ReadOnly` alias is not pure after DataVault migration; preferred replacement is a copy-to-caller method or a one-phase callback that resolves read-only and releases before return.
- `Copy*Deltas(NativeList<...> destination)` can remain because destination is caller-owned phase-local memory. Registry reads must use `TryReadOnlyHandle` internally.
- Register/cache writer methods must acquire DataVault write locks only inside the method and release in `finally` before returning.

Race plan:
- No lock may cross frame boundaries or dispatcher phases.
- Tombstone decay job cannot keep a persistent `NativeList<int>` output. It must either write into a resolved DataVault array with an explicit count buffer in the same phase or degrade to a bounded main-thread sweep.
- Failed lock or stale handle must write telemetry and skip the mutation. No managed exception in hot paths.

## Task 04 DTO Layout Verification

Target structs already use explicit layout and 8-byte-multiple sizes:
- `AbsoluteUniversePosition`: 48 bytes, explicit.
- `AbsoluteUniversePositionBlit128`: 48 bytes, explicit.
- `PoolSlotData`: 64 bytes, explicit.
- `EntityDataRecord`: 64 bytes, explicit.
- `PersistentWorldItemRecord`: 256 bytes, explicit.
- `PersistentWorldDeltaRecord`: 64 bytes, explicit.
- `PersistentWorldCompactDeltaRecord`: 16 bytes, explicit.

No DTO refactor is required for those target buffers. Required validator work: add an editor static guard for the listed sizes and critical offsets. Reject `Pack=1` and any sequential replacement.

Cache footprint:
- `PersistentWorldItemRecord`: 4 cache lines at 64-byte line size.
- `PersistentWorldDeltaRecord`: 1 cache line.
- `PersistentWorldCompactDeltaRecord`: 4 records per 64-byte line.
- `PoolSlotData` and `EntityDataRecord`: 1 cache line each.

## Task 05 Telemetry Ring Plan

Telemetry buffers:
- `(BufferID)74517` -> `WorldTelemetryEntry[300]`.
- `(BufferID)74518` -> `int[1]` write cursor.

Planned `WorldTelemetryEntry` layout: explicit 64 bytes.
- Offset 0: `ulong FrameTickOrSequence`.
- Offset 8: `uint BufferId`.
- Offset 12: `uint Generation`.
- Offset 16: `uint SystemId`.
- Offset 20: `uint EventCode`.
- Offset 24: `int ActualLength`.
- Offset 28: `int ExpectedLength`.
- Offset 32: `int3 ChunkId`.
- Offset 44: `float Microseconds`.
- Offset 48: `uint InstanceUid`.
- Offset 52: `uint Flags`.
- Offset 56: `uint Reserved0`.
- Offset 60: `uint Reserved1`.

Event codes:
- 1: resolve success.
- 2: read failure.
- 3: write lock contention.
- 4: stale generation.
- 5: capacity mismatch.
- 6: NaN/AUP invalid.
- 7: tombstone decay skipped.

Dump route: `Docs/AgentLogs/Dump_1325_WorldRegistry.bin`. The runtime write path must copy raw structs; the cold dump path may run outside the hot frame after a catastrophic latch.
