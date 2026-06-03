# Manual Review Pass 13 - Persistence, Streaming, DataVault, H8Memory, Scatter Working Memory

Status: STATIC METHOD REVIEW - NO UNITY/PROFILER/MEMORY PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`
- `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs`
- `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs`
- `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs`
- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
- `Assets/_Project/Scripts/WorldProceduralScatterDirectorCandidateAcceptance.cs`
- `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`
- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`

## Mandates Checked

- `STRM_World_Streaming_Residency_Chunk_Management.txt`
- `STRM_Persistent_Object_Registry.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Findings

### Persistent world registry

- `PersistentWorldRegistry.Awake()` preallocates proxy slots, lookup dictionaries, hydration scratch lists, sector commit buffers, active thermal vent buffers, and a 300-entry world telemetry dump snapshot (`PersistentWorldRegistry.cs:3178-3225`). This is a stronger shape than ad hoc scene scans, but the code still uses managed dictionaries/lists for registry and sector state. It is acceptable only as owner-local residency state with bootstrap capacity proof.
- `LateFrameTick()` drains deferred prefab releases, dehydrates slots, starts indexed sector paging, runs pending hydration, applies tombstone decay, and captures queued black-box snapshots (`PersistentWorldRegistry.cs:3550-3558`). `SlowTick()` quantizes player sector/chunk, schedules indexed paging, schedules sector override commits, and runs hydration rescan (`PersistentWorldRegistry.cs:3561-3595`). This matches owner-phase streaming better than raw `Update`, but release proof must show these calls are bounded under chunk boundary movement.
- `TryLoadIndexedSectorRecordsSnapshot()` allocates a TempJob desired-sector hash array and TempJob record list, then materializes a managed `PersistentWorldDeltaRecord[]` for loaded records (`PersistentWorldRegistry.cs:5319-5351`). `TryWriteResidentSectorOverrideSnapshots()` and `TryWriteResidentSectorEntityStateSnapshot()` allocate TempJob arrays for sector and entity-state snapshots (`PersistentWorldRegistry.cs:6489`, `:6567`). `TryWriteSectorEntityStateOverrideAsync()` allocates an `Allocator.Temp` entity-state array for async temp writes (`PersistentWorldRegistry.cs:8045`).
- `RunSectorOverrideCommitAsync()` copies bounded commit work into `_sectorOverrideCommitWorkBuffer`, switches to a background thread for file commit, then returns to main thread for state cleanup (`PersistentWorldRegistry.cs:7000-7085`). The async split is structurally correct, but file IO cadence, allocation windows, and frame impact are not proven.

Classification: `YELLOW_PERSISTENT_WORLD_IO_WINDOW_PROOF_REQUIRED`.

### Content authority and Addressables prewarm

- `ContentAuthorityRuntime` preallocates pending-load renderers, hologram proxy arrays, fixed Addressables handle ledgers, and DataVault-backed pending-load/telemetry buffers (`ContentRuntimeServices.cs:691-696`, `:733-745`, `:2446-2475`).
- `StartVfxPrewarm()` dispatches Addressables `LoadAssetAsync` calls for particle systems and compute shaders into a 64-entry fixed prewarm ledger (`ContentRuntimeServices.cs:1039-1081`). `TickVfxPrewarm()` polls completion in `ColdTick()` and moves successful handles to the resident ledger or stages releases through the asset lifecycle governor (`ContentRuntimeServices.cs:1373-1401`).
- `BuildHologramPool()` creates `GEN_ContentHologramProxy` GameObjects in `Awake()` when `hologramProxyMesh` and `hologramMaterial` are assigned (`ContentRuntimeServices.cs:1095-1117`). This is a bounded bootstrap pool, not a hot path in the reviewed method, but production still needs proof the mesh/material are assigned and the pool does not hide missing authored UI/content assets.

Classification: `YELLOW_CONTENT_AUTHORITY_LEDGER_AND_POOL_PROOF_REQUIRED`.

### Voxel streaming fade materials

- `HectonVoxelStreamingBridge` uses dispatcher phases: `Tick()` launches bounded async cave requests, `LateFrameTick()` flushes despawns and fade updates, and `SlowTick()` rebuilds desired cave requests (`HectonVoxelStreamingBridge.cs:172-219`).
- The fade path prewarms up to 32 cloned materials from the voxel volume prefab source material (`HectonVoxelStreamingBridge.cs:603-627`) and drives `_ChunkDissolveFade` through pooled runtime materials during `LateFrameTick()` (`HectonVoxelStreamingBridge.cs:550-573`).
- On `VoxelEngineRuntime` hot-swap it releases and may recreate the material pool (`HectonVoxelStreamingBridge.cs:805-809`). This is acceptable only if hot-swap is rare/debug/boot, the source material is production-authored, and SRP/material instance counts are measured.

Classification: `YELLOW_VOXEL_STREAMING_FADE_MATERIAL_POOL_PROOF_REQUIRED`.

### Scatter working memory

- `ScatterWorkingMemory` owns persistent native scatter arrays, multi-hash maps, candidate scratch buffers, and many fixed-size managed dictionaries/lists/stacks (`WorldProceduralScatterWorkingMemory.cs:26-170`). It registers native storage with `NativeMemorySentinel`.
- `PrepareScatterCellCandidateAcceptanceBatch()` grows native lists and multi-hash maps by setting `Capacity` when candidate count exceeds current capacity (`WorldProceduralScatterWorkingMemory.cs:240-302`). `EnsureGridPlacementSpatialCapacity()` grows spatial metadata and bucket maps (`WorldProceduralScatterWorkingMemory.cs:345-370`). `EnsureCapacity()` disposes and recreates sampling `NativeArray<T>` to `NextPowerOfTwo(requiredCapacity)` (`WorldProceduralScatterWorkingMemory.cs:571-581`).
- These growth routes are called from runtime scatter paths: `WorldProceduralScatterDirector.Tick()` queues visual sync every 0.25 seconds when needed (`WorldProceduralScatterDirector.cs:676-705`), `SlowTick()` can queue scatter visual sync and migratory Sargassum work (`WorldProceduralScatterDirector.cs:983-1074`), and candidate acceptance calls `PrepareScatterCellCandidateAcceptanceBatch()` during placement evaluation (`WorldProceduralScatterDirectorCandidateAcceptance.cs:1375`, `:1515`).

Classification: `YELLOW_SCATTER_WORKING_MEMORY_RUNTIME_GROWTH_PROOF_REQUIRED`.

### GlobalDataVault and H8Memory growth

- `GlobalDataVault.Initialize()` creates UnsafeHashMaps, NativeLists, H8Memory-owned native arrays, macro payload cache maps, and a raw arena (`GlobalDataVault.cs:698-825`). That is the right core-owner pattern for cross-domain native state, not a casual global dictionary.
- `EnsureGenerationHandle()` can call `TryEnsureVaultBuffer()`, and `TryEnsureVaultBuffer()` can request arena growth when required bytes exceed current arena bytes (`GlobalDataVault.cs:1135-1201`). `TryGrowArenaForBytes()` / `TryGrowArena()` can relocate the arena when no active locks/pinned views block relocation, and deferred growth can be processed later (`GlobalDataVault.cs:5167-5361`).
- `TryReserveMacroDatabaseCache()` can increase macro payload hash/list capacities, and `TryStoreMacroDatabasePayload()` allocates raw persistent payload memory (`GlobalDataVault.cs:3543-3566`, `:3607-3613`).
- `H8Memory.Allocate()` calls `EnsureTrackingCapacity()` before creating tracked `NativeArray<T>` (`H8Memory.cs:2756-2787`). `EnsureTrackingCapacity()` doubles the tracking record array and hash maps when exhausted (`H8Memory.cs:4374-4420`).

Classification: `YELLOW_CORE_NATIVE_GROWTH_COUNTER_PROOF_REQUIRED`.

## Blocker Updates

- Strengthen `RB-101`: DataVault is structurally owner-owned, but arena relocation/deferred growth and macro payload cache growth must be proven absent from gameplay hot paths or moved into boot/import windows.
- Strengthen `RB-105`: Content authority needs Addressables handle-ledger proof, resident VFX prewarm proof, hologram pool assignment proof, and memory-pressure release proof.
- Strengthen `RB-109`: Voxel fade material pool is fixed-count, but material clone count, hot-swap recreation count, and SRP compatibility remain unproven.
- Strengthen `RB-115`: Scatter working memory has multiple capacity-growth routes reachable from runtime scatter evaluation. NativeMemorySentinel registration is not enough; the proof must show no growth after gameplay begins.
- Strengthen `RB-118`: Persistent world indexed sector read/write windows are explicit IO routes, but Temp/TempJob/managed-array allocation windows need stress proof with frame-time and allocation counters.
- Strengthen `RB-120`: H8Memory tracking arrays/maps can reallocate on record-capacity exhaustion; the target scene capacity must be prewarmed or proven flat in a long soak.

## Required Next Proof

- 300-frame and 10-minute soak for DataVault/H8Memory allocation count, arena bytes, deferred growth bytes, macro payload count, tracking capacity, and block descriptor capacity.
- Save/stream stress crossing multiple sector boundaries while writing overrides; capture frame time, Temp/TempJob allocation count, managed loaded-record array count, and async commit backlog.
- Addressables VFX prewarm proof: handle ledger counts, failed handle releases, resident handle count, pending releases, and Memory Profiler snapshot after prewarm and after scene unload.
- Scatter stress proof: candidate batch capacity, grid placement capacity, sampling array length, NativeMemorySentinel active bytes, and no capacity growth after bootstrap.
- Voxel streaming proof: fade material clone count exactly bounded, no recreation during normal gameplay, hot-swap count, SRP Batcher/material instance proof, and compact/high cave transition capture.

## Non-Closure

This pass does not close persistence, streaming, DataVault, scatter, or voxel streaming for release. It narrows the gates. Runtime correctness still requires Unity/player/profiler/memory artifacts.
