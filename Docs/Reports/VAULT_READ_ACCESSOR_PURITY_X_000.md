# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# VAULT_READ_ACCESSOR_PURITY_X_000

Agent: X_000
Scope: Read-like methods in the X_000-migrated MonoBehaviour files.
Verdict: PASS after AudioLog read-path correction.

## Finding Corrected

Problem: `AudioLogSystem.TryReadVaultBuffer<T>` was named and used as a read helper, but the first DataVault migration still incremented vault-resolution counters and wrote telemetry rows on read failure.

Fix: The helper now only checks the cached `IDataVault` reference, validates the `VaultGenerationHandle<T>`, calls `TryReadOnlyHandle`, checks capacity, and returns `false` on failure. It no longer calls `RecordVaultTelemetry`, increments counters, allocates, grows buffers, releases buffers, acquires write locks, or polls `GlobalRegistry`.

Code route:
- `TryGetEncryptedFragmentBits` -> `TryReadEncryptedFragmentState` -> `TryReadVaultBuffer`
- Current behavior: read-only view is method-local and discarded before return.

## AudioLogSystem.cs

Pure read-like methods checked:
- `TryReadVaultBuffer<T>`: no allocation, no DataVault ensure, no write lock, no telemetry mutation, no registry search.
- `TryReadEncryptedFragmentState`: two read-only view resolutions only; no allocation or buffer regeneration.
- `TryGetEncryptedFragmentBits`: pure lookup over method-local read-only views and `_encryptedFragmentStateCount`; no allocation or mutation.

Non-read write routes intentionally remain mutating:
- `TryAcquireVaultWrite<T>`: writer-fence acquisition path; not a read accessor.
- `TryAcquireEncryptedFragmentState`: write-state acquisition path; not a read accessor.
- `RecordVaultTelemetry`: telemetry write path; not called by read helpers after this correction.
- `EnsureVaultBuffersCold`: cold ownership/bootstrap path; allocation is explicit and outside read accessors.

Defragmentation implication: stale or missing generation handles now fail closed in read helpers. No `TryGet*` or `TryRead*` method attempts lazy allocation or regeneration.

## AwaitableDropSequenceDirector.cs

Read-like DataVault use checked:
- `DumpBlackBox`: resolves one read-only view and writes a crash dump file in a cold fault/report path. It does not allocate or regenerate DataVault buffers.
- No private `TryGet*` or `Read*` method in this file creates or grows the DataVault black-box ring.

Non-read ownership route:
- `EnsureBlackBox`: cold setup path called from lifecycle/configuration. It can create the DataVault ring if missing and is intentionally not a read accessor.
- `CacheDataVaultCold`: cold bootstrap/cache path. It replaced the former `ResolveDataVaultCold` name because the method can mutate `_dataVault` by caching `GlobalRegistry.DataVault`.

Defragmentation implication: runtime record writes acquire a writer fence per row; dumps use a scoped read-only view only.

## QAEnduranceWatchdogBot.cs

Read-like DataVault use checked:
- `DumpBlackBox`: resolves one read-only view and writes a cold binary report. It does not allocate or regenerate DataVault buffers.
- No private `TryGet*` or `Read*` method in this file creates or grows the DataVault black-box ring.

Non-read ownership route:
- `EnsureBlackBox`: cold setup path called from `Awake`/configuration/startup paths. It can create the DataVault ring if missing and is intentionally not a read accessor.
- `CacheDataVaultCold`: cold bootstrap/cache path. It replaced the former `ResolveDataVaultCold` name because the method can mutate `_dataVault` by caching `GlobalRegistry.DataVault`.

Defragmentation implication: runtime black-box writes acquire a writer fence per row; dumps use a scoped read-only view only.

## OrbitalDropReentryVfxController.cs

Read-like DataVault use checked:
- `DumpBlackBoxOnce`: resolves one read-only telemetry view and writes a cold binary dump. It does not allocate or regenerate the DataVault telemetry ring.

Non-read ownership/write routes:
- `EnsureNativeTelemetry`: cold setup path. It can create the DataVault ring if missing and is intentionally not a read accessor.
- `WriteTelemetry`: acquires a DataVault writer fence, writes one fixed 48-byte row, and releases in `finally`.
- `OnDestroy`: releases the DataVault buffer descriptor through the vault owner route.

Defragmentation implication: the MonoBehaviour no longer stores `NativeArray<ReentryVfxTelemetryEntry>` between phases. The only native views are method-local writer/read-only views.

## SargassumCutManager.cs

Read-like DataVault use checked:
- No `TryGet*` or `Read*` method allocates, grows, or regenerates the new stamp-command buffers.
- Existing public `TryGetCutMask` only returns render texture and rect state, not DataVault buffers.

Non-read ownership/write routes:
- `EnsureVaultBuffer`: cold setup path used from resource creation. It can create/recreate the two DataVault command buffers if missing or undersized and is intentionally not a read accessor.
- `TryAcquireVaultBuffer`: writer-fence acquisition path used for command staging and graphics upload. It is not a read accessor.
- `ExecuteStampPass` and `QueueDamageVolumeStamp`: acquire a writer fence only long enough to write one fixed command row.
- `ProcessQueuedMaskUpdate` and `ProcessQueuedDamageVolumeUpdate`: acquire a writer fence only to obtain the mutable `NativeArray<T>` required by the existing `GraphicsBufferUploadUtility.UploadNativeArray` signature, then release immediately after upload.

Defragmentation implication: the MonoBehaviour no longer stores `NativeArray<StampCommand>` or `NativeArray<DamageVolumeStampCommand>` between phases. No lazy allocation is reachable through `TryGet*` or `Read*`; allocation is isolated to resource creation.

## DebrisManager.cs

Read-like DataVault use checked:
- `TryReadVaultBuffer<T>` only validates cached vault/handle state, resolves a method-local `NativeArray<T>` view, checks capacity, and returns `false` on miss.
- `TryReadOnlyVaultBuffer<T>` resolves a method-local read-only view for rendering and never creates or grows the vault buffers.
- No `TryGet*` or `Read*` route calls `EnsureVaultBuffer`, acquires a writer fence, polls the scene, publishes events, or completes jobs.

Non-read ownership/write routes:
- `EnsureRuntimeResources` / `EnsureVaultBuffer` are cold lifecycle setup paths. They can create the front/back DataVault rows and are not read accessors.
- `FlushPendingBursts`, `ResetActiveState`, `ProcessThermalPetrification`, and direct burst writes acquire scoped DataVault writer fences and release them in `finally`.
- `TryLockSimulationJobBuffers` locks the current front/back buffer IDs before scheduling `DebrisSimulationJob`; `UnlockSimulationJobBuffers` runs after completion in `LateFrameTick` or forced release.
- `OnOriginShift` no longer mutates state while the simulation job is scheduled. It accumulates `_pendingShiftOffset` and applies it only when no job owns the buffers.

Defragmentation implication: the MonoBehaviour no longer stores `_frontStates` or `_backStates` `NativeArray<DebrisChunkState>` fields. Native views are phase-local, and the previous write-while-job-read hazard is closed by deferring origin-shift and burst writes while the job buffers are locked.

## InternalFloodWaterlineRuntime.cs

Read-like DataVault use checked:
- No `TryGet*` or `Read*` method creates, grows, or regenerates the new waterline telemetry ring.
- `DumpBlackBoxOnce` resolves a scoped read-only view only in the cold fault dump path. It does not call `EnsureNativeTelemetry`, poll the scene, or mutate DataVault state.

Non-read ownership/write routes:
- `EnsureNativeTelemetry` is cold lifecycle setup and can create/recreate the fixed 300-row telemetry ring.
- `WriteTelemetry` acquires one DataVault writer fence, writes one fixed 40-byte row, and releases in `finally`.
- `Shutdown` releases the DataVault descriptor through the owner route.

Defragmentation implication: the MonoBehaviour no longer stores `NativeArray<WaterlineTelemetryEntry>` between phases. Runtime telemetry views are scoped to one method and writer fenced.

## DiegeticVisorHudMesh.cs

Read-like DataVault use checked:
- `DumpBlackBox` resolves a scoped read-only view only for the cold binary dump path. It does not allocate or regenerate the black-box ring.
- No `TryProject*`, `Resolve*`, or material state read path touches DataVault ownership.

Non-read ownership/write routes:
- `EnsureBlackBox` is cold setup and can create/recreate the fixed 300-row HUD telemetry ring.
- `RecordTelemetry` acquires a DataVault writer fence, writes one fixed 40-byte row, and releases in `finally`.
- `DisposeBlackBox` releases the descriptor through `IDataVault.ReleaseBuffer`.

Defragmentation implication: the MonoBehaviour no longer stores `NativeArray<DiegeticHudTelemetryEntry>` between phases. The field is a relocatable handle; mutable views are method-local.

## DiegeticTooltipSystem.cs

Read-like DataVault use checked:
- `DumpBlackBox` resolves a scoped read-only view only for cold crash/post-mortem export. It does not create, grow, or regenerate the tooltip black-box ring.
- Tooltip render/read helpers (`Resolve*`, `Refresh*`, `TryRegister*`) do not acquire DataVault writer fences or complete jobs.

Non-read ownership/write routes:
- `EnsureBlackBox` is cold setup and can create/recreate the fixed 300-row tooltip ring.
- `RecordBlackBox` acquires one DataVault writer fence, writes one fixed 32-byte row, and releases in `finally`.
- `ReleaseResources` releases the descriptor through the owner route.

Defragmentation implication: the MonoBehaviour no longer stores `NativeArray<TooltipBlackBoxEntry>` between phases. The black-box memory is DataVault-owned and resolved only for bounded write/read scopes.

## HUDNotification.cs

Read-like DataVault use checked:
- `ResolveQueueCapacity` reads scalar `_queueCapacity` and serialized capacity only; it does not call `EnsureQueue`, allocate/grow DataVault buffers, acquire a writer fence, poll the scene, or publish events.
- `TryGetActive` and `TryUseRegisteredNotification` only validate the static active instance and do not touch DataVault ownership.
- Display text read helpers (`TryWriteDisplayMessage`, `TryWriteFixedBufferMessage`, `TryWriteDisplaySpan`) operate on managed fixed char caches / caller buffers and do not create or regenerate the DataVault queue.

Non-read ownership/write routes:
- `EnsureQueue` is a cold setup path used from lifecycle/build paths and can create the fixed 8-row UI queue in DataVault.
- `TryAcquireQueueWrite`, `PushQueueBack`, `InsertQueueFront`, `PopQueueFront`, and `RemoveQueueFront` are mutation paths. They acquire a DataVault writer fence and release it in `finally`.
- `OnDestroy` and DataVault hot-swap release the descriptor through `IDataVault.ReleaseBuffer`.

Defragmentation implication: the MonoBehaviour no longer stores `NativeArray<NotificationRequest>` between phases. The queue field is a relocatable `VaultGenerationHandle<NotificationRequest>`, and native views are method-local writer views only.

## HectonVoxelEngine.cs

Read-like DataVault use checked:
- `DumpVoxelMeshPipelineBlackBox` resolves a scoped read-only view only in the cold editor/development fault dump path. It does not call `EnsureVoxelMeshPipelineBlackBox`, allocate/grow DataVault buffers, acquire a writer fence, complete jobs, or regenerate the ring.
- No `TryGet*`, `Read*`, or `Resolve*` route was introduced for the voxel mesh black-box buffer.

Non-read ownership/write routes:
- `EnsureVoxelMeshPipelineBlackBox` is the owner setup path for the fixed 300-row voxel mesh telemetry ring. It can create the DataVault buffer and is not a read accessor.
- `CacheVoxelMeshPipelineBlackBoxVaultCold` handles cold DataVault cache/hot-swap only; it deliberately does not use a `Resolve*` name.
- `WriteVoxelMeshPipelineBlackBoxSample` acquires one DataVault writer fence, writes one fixed 32-byte row, and releases in `finally`.

Defragmentation implication: the `HectonVoxelEngine` MonoBehaviour no longer stores the static `NativeArray<VoxelMeshPipelineTelemetryEntry>` field. The file still contains 50 non-MonoBehaviour persistent candidates in `MCTables` and voxel scratch owner structs; those require separate owner route cards and were not claimed clean.

## LoreDatabaseManager.cs

Read-like DataVault use checked:
- `TryGetPackedUnlockWords` now only calls `TryReadUnlockWords`, which validates cached vault/handle state and returns a read-only view. It does not call `EnsureUnlockStorage`, create/grow buffers, acquire a writer fence, or cache `GlobalRegistry.DataVault`.
- `TryGetRecordIndex` no longer lazily rebuilds the hash dictionary. The lookup table is built in `Awake`/write paths through `BuildRecordLookupCold`.
- `IsUnlocked`, `UnlockedCount`, and save population read the unlock bitset through scoped read-only DataVault views only.

Non-read ownership/write routes:
- `EnsureUnlockStorage` is cold lifecycle/write setup and can create the fixed two-word DataVault buffer.
- `TryAcquireUnlockWordsWrite`, `UnlockByHashInternal`, `UnlockByPackedBits`, and `LoadFromSaveData` acquire a DataVault writer fence and release it in `finally`.
- `OnDestroy` and DataVault hot-swap release the descriptor through `IDataVault.ReleaseBuffer`.

Defragmentation implication: `LoreDatabaseManager` no longer stores `NativeArray<uint> _unlockedWords` between phases. The unlock mask is a relocatable `VaultGenerationHandle<uint>` over a two-word primitive payload; read routes are fail-closed when the vault/handle is unavailable.

## HeadlessStressFractureBot.cs

Read-like DataVault use checked:
- `TryReadBlackbox` only validates the cached vault/handle and resolves a scoped read-only view. It does not call `EnsureBlackboxCold`, create/grow the ring, acquire a writer fence, publish signals, or cache registry state.
- `TryReadRigidbodyAupBuffer` remains a pure external read of an existing physics DataVault handle; it returns false on compaction fence, missing handle, stale generation, or empty buffer.
- `DumpBlackbox` and `DumpBlackboxManifest` call `TryReadBlackbox` and write cold files only after a read-only view exists; they do not regenerate black-box storage.

Non-read ownership/write routes:
- `EnsureBlackboxCold` is the cold setup path for the fixed 300-row stress-fracture black-box ring.
- `AcquireScratchBlock` is an explicit QA stress mutation path called from `PulseScratchMemory`; it can create the DataVault scratch payload and immediately releases the writer fence after validation.
- `RecordBlackbox` acquires a DataVault writer fence, writes one fixed 64-byte row, and releases in `finally`.
- `ReleaseScratchBlock` and `ReleaseBlackbox` release descriptors through `IDataVault.ReleaseBuffer`.

Defragmentation implication: `HeadlessStressFractureBot` no longer stores `NativeArray<FractureTelemetryEntry>` or `NativeArray<byte>` fields between phases. The retained state is two generation handles plus scalar counters/flags; every native view is method-local.

## InstanceCullingService.cs

Read-like DataVault use checked:
- `TryReadIndirectArgsReadback` validates the cached vault and exact `BufferID.InstanceCullingIndirectArgsReadback` handle, then calls `TryReadOnlyHandle`. It does not call `EnsureResources`, allocate/grow the readback buffer, acquire a writer fence, publish, poll the scene, or complete jobs.
- `TryReadTelemetryRing` validates the cached vault and exact `BufferID.InstanceCullingTelemetryRing` handle, then calls `TryReadOnlyHandle`. It does not create/regenerate the telemetry ring.
- `TryConsumeTelemetry` reads the existing telemetry ring through `TryReadTelemetryRing`. It mutates only the consumer cursor/count that defines queue consumption semantics; it does not mutate DataVault storage or allocate.
- `DumpBlackBox` uses `TryReadTelemetryRing` and writes a cold binary dump only after a read-only view exists; it does not regenerate telemetry storage.

Non-read ownership/write routes:
- `EnsureResources` is the cold setup path and can create the fixed readback scratch payload and fixed 300-row telemetry ring in DataVault.
- `CacheDataVaultCold` caches `GlobalRegistry.DataVault` and is deliberately not named `Resolve*`.
- `TryRequestTelemetryReadback` is an explicit GPU readback request/mutation path, not a read accessor. It acquires the readback write lock, passes the method-local native view to `AsyncGPUReadback.RequestIntoNativeArray`, and releases the lock in the callback or teardown guard.
- `WriteTelemetry` acquires a DataVault writer fence, writes one fixed 64-byte telemetry row, and releases in `finally`.
- `CompletePendingReadbackBeforeRelease` is a documented teardown/configuration blocking sync point. It protects a vault buffer while `AsyncGPUReadback` may own the native pointer; it is not called by `TryGet*`, `Read*`, or `Resolve*` accessors.

Defragmentation implication: `InstanceCullingService` no longer stores `NativeArray<uint> _indirectArgsReadback` or `NativeArray<InstanceCullingTelemetryEntry> _telemetryRing` fields between phases. The file's only remaining native finding is `ApplyAupShiftJob.Matrices`, an allowed transient Burst job parameter.

## TraumaDispatcher.cs

Read-like DataVault use checked:
- `TryReadParasiteSporeLosHits` validates the cached vault and exact `BufferID.TraumaDispatcherParasiteSporeLosHits` handle, then calls `TryReadOnlyHandle`. It does not call `EnsureParasiteSporeLosBuffer`, allocate/grow buffers, acquire a writer fence, cache `GlobalRegistry.DataVault`, publish signals, or search the scene.
- `CompleteParasiteSporeLosQuery(false)` only completes the scheduled LOS job when `DispatcherJobSwap.TryComplete` says the job is already complete in the dispatcher late-frame swap window. It releases writer fences before consuming the read-only hit view.
- `LateFrameTick` no longer calls the ensure path. If the cold lifecycle setup did not create the DataVault handles, the LOS query fails closed for that frame instead of lazily allocating in a hot path.

Non-read ownership/write routes:
- `InitializeParasiteSporeLosBuffers` and `EnsureParasiteSporeLosBuffer` are cold lifecycle setup paths called from `Awake` / `OnEnable`. They can create the fixed one-row command and hit buffers.
- `TryAcquireParasiteSporeLosWriteBuffers` acquires writer fences for both LOS buffers immediately before `RaycastCommand.ScheduleBatch`.
- The writer fences remain held while the raycast job owns the native command/hit views and are released by `CompleteParasiteSporeLosQuery` or forced teardown in `DisposeParasiteSporeLosBuffers`.

Defragmentation implication: `TraumaDispatcher` no longer stores `NativeArray<RaycastCommand>` or `NativeArray<RaycastHit>` fields between phases. It stores only two `VaultGenerationHandle` descriptors and method-local native views used for the scheduled physics batch.

## Residual Project Truth

This report covers the files changed by X_000. The project-wide Roslyn ledger still reports 2248 forbidden persistent native alias candidates, including 682 inside MonoBehaviour classes across 68 files. The full residual map is in `Docs/Reports/VAULT_MONOBEHAVIOUR_NATIVE_FIELD_AUDIT_X_000.json`.

Additional name-purity correction: a project-wide search found no remaining `ResolveDataVaultCold` method names under `Assets/_Project/Scripts` after mechanical private renames to `CacheDataVaultCold` in the migrated prologue/QA files plus KineticCharacterAnimatorRuntime, VocalWarningSystem, SpectrumSystem, and TopographicalSonarSynthesizer. Broader `TryResolve*` mutable-view routes still exist and are outside this safe rename pass; they require owner-specific API migration, not a blind edit.

## RaycastBatchHelper.cs

Read-like DataVault use checked:
- `GetResult`, `QueryCount`, and `WasExecuted` read managed mirrors/scalars only. They do not allocate/grow DataVault buffers, complete jobs, cache `GlobalRegistry`, publish signals, or acquire writer fences.
- `HeartbeatState` calls `HasRaycastBuffers`, which uses `TryReadRaycastCommands` and `TryReadRaycastHits` read-only handle resolution only. It fails as Booting if the cold buffers are absent.
- `TryReadRaycastCommands` and `TryReadRaycastHits` validate the cached vault and exact BufferID handles, then call `TryReadOnlyHandle`. They do not call `EnsureRaycastBuffer` or mutate DataVault state.
- `OnDrawGizmos` reads the existing command buffer through `TryReadRaycastCommands`; it does not create or regenerate native memory.

Non-read ownership/write routes:
- `EnsureBuffersAllocated` and `EnsureRaycastBuffer` are cold setup paths called from lifecycle registration and can create/recreate the fixed 512-row command/hit buffers.
- `AddQuery` writes exactly one command through `TryWriteRaycastCommand`, a short writer-fenced mutation path.
- `ExecuteBatch` and `TryAcquireScheduledRaycastBuffers` acquire writer fences for command and hit buffers immediately before `RaycastCommand.ScheduleBatch`.
- `TryConsumeScheduledBatch` may complete the scheduled job only from the late-frame/forced teardown route. It releases writer fences before consuming the read-only hit view.
- `ReleaseBuffers` is teardown; it forces completion only to prevent Unity Physics from holding a pointer while DataVault handles are released.

Defragmentation implication: `RaycastBatchHelper` no longer stores `NativeArray<RaycastCommand>` or `NativeArray<RaycastHit>` fields between phases. It stores two `VaultGenerationHandle` descriptors, scalar state, and managed mirrors for the public API; every native view is method-local and tied to a writer/read-only fence scope.

## Residual Project Truth - After RaycastBatchHelper

Current Roslyn ledger: 2390 files, 0 parse failures, 7665 native field declarations, 2173 forbidden persistent native alias candidates, 680 MonoBehaviour candidates across 65 files, proof hash `baf4734bc9c976e6a331deb5e1b7b3d5ea1bad3d696bc62992b1713aa15bb345`.

## PhysicalToolGripOffsets.cs

Read-like/native ownership routes checked:
- `TryReadGripOffset` reads one of two cached `float4x4` value fields after optional value-cache refresh from serialized `Matrix4x4` data. It does not allocate native memory, grow a buffer, acquire a DataVault writer fence, complete a job, publish signals, or retain a native view.
- `TryApplyGripOffset` is an explicit Transform mutation path, not a read accessor. It calls `TryReadGripOffset` and then applies the unmanaged value to the supplied Transform.

Non-read ownership/write routes:
- `CacheAuthoredOffsets` sanitizes serialized authored matrices into two unmanaged value fields.
- `OnValidate`, `Awake`, and `OnEnable` refresh the value cache only; no `NativeArray` exists after this cleanup.

Defragmentation implication: `PhysicalToolGripOffsets` no longer stores `NativeArray<float4x4>` between phases. DataVault was deliberately not used because this is per-instance prefab-authored state; one global `BufferID` would merge independent tool instances.

## DiegeticHudManualLayout.cs

Read-like/native ownership routes checked:
- No `TryGet*`, `Read*`, or `Resolve*` method now creates, resizes, or regenerates a native buffer in this file.
- `ComputeLayoutPosition` is a pure static value function over `DiegeticHudLayoutInput` and `DiegeticHudLayoutSettings`.

Non-read ownership/write routes:
- `RebuildLayout` is an explicit UI layout mutation path. It builds stack/value DTOs and writes `Transform.localPosition`; it does not allocate `NativeArray` state, call DataVault, schedule a job, or retain a native view.
- `FlushGlobalRescaleRequests` consumes UI rescale signals and invokes the explicit rebuild path.
- `DiegeticHudLayoutJob` remains a transient job contract type with `NativeArray` fields, but the MonoBehaviour no longer owns or schedules persistent `_inputs` / `_outputs` arrays.

Defragmentation implication: `DiegeticHudManualLayout` no longer stores `NativeArray<DiegeticHudLayoutInput>` or `NativeArray<float3>` fields between phases. Layout calculation is now per-target value math with no persistent native memory.

## Residual Project Truth - After PhysicalToolGripOffsets And DiegeticHudManualLayout

Current Roslyn ledger: 2390 files, 0 parse failures, 7721 native field declarations, 2233 forbidden persistent native alias candidates, 677 MonoBehaviour candidates across 63 files, proof hash `c7156675265f9b616938d414b92e95419fb09ebfc6199c06b12d9a1a9eb5e76d`. The global count changed under concurrent workspace edits; the targeted X_000 cleanup for this slice removed three persistent MonoBehaviour native aliases and both scoped files now report 0 forbidden persistent findings.

## FontStreamingManager.cs

Read-like DataVault use checked:
- `TryReadVisibleHashPrefetch` validates cached vault/handle identity and resolves a read-only view only. It does not allocate/grow buffers, acquire a writer fence, complete a job, publish signals, or search the scene.
- `TryReadVisibleSlicePrefetch` follows the same read-only handle path for the prefetch result buffer.
- `TryCompleteVisibleHashPrefetch` is an explicit job-completion route, not a read accessor. It completes the scheduled prefetch job, releases writer locks, then reads the result through `TryReadVisibleSlicePrefetch`.

Non-read ownership/write routes:
- `EnsurePrefetchBuffer<T>` is cold/setup capacity management and can create or replace the DataVault buffer descriptors.
- `TryAcquireVisibleHashPrefetchWriteBuffer` and `TryAcquireVisiblePrefetchJobBuffers` acquire writer fences only for explicit mutation/job ownership windows.
- `ReleaseVisiblePrefetchJobBufferLocks` releases writer locks after the job ownership window closes; no raw `NativeArray` field is retained by the MonoBehaviour.

Defragmentation implication: `FontStreamingManager` no longer stores `NativeArray<uint> _visibleHashPrefetch` or `NativeArray<int2> _visibleSlicePrefetch` between phases. It stores DataVault generation handles and scalar capacity/lock state only; every native view is method-local.

## Residual Project Truth - After FontStreamingManager

Current Roslyn ledger: 2398 files, 0 parse failures, 7740 native field declarations, 2234 forbidden persistent native alias candidates, 671 MonoBehaviour candidates across 62 files, proof hash `b2a2d0e9af041616dbcba8004e5e81476593942c8417c9c8d69b6943956eb99d`. `FontStreamingManager.cs` reports 0 forbidden persistent native alias findings.

## VehicleSubOsCockpitRuntime.cs

Read-like DataVault use checked:
- `TryReadButtonStates`, `TryReadButtonTargets`, `TryReadButtonProgress`, `TryReadButtonOffsets`, `TryReadButtonBaseLocalPositions`, `TryReadButtonMatrices`, and `TryReadTelemetryRing` validate cached vault/handle identity and resolve scoped read-only views only.
- `TryReadCockpitVaultBuffer` does not call `EnsureCockpitVaultBuffer`, allocate/grow a buffer, acquire a writer fence, complete a job, publish signals, or search scene state.
- `DumpBlackbox` and `WriteDamageHolographerMirrorDump` read the telemetry ring through `NativeArray<CockpitTelemetryEntry>.ReadOnly` only.

Non-read ownership/write routes:
- `EnsureNativeResources` and `EnsureCockpitVaultBuffer<T>` are cold/setup capacity paths. They create or replace buffers only outside read accessors.
- `ScheduleButtonJob` acquires method-local writer views through `TryAcquireButtonJobBuffers`; `LateFrameTick`, forced teardown, and `ReleaseButtonJobBufferLocks` release those writer fences after the scheduled button job window closes.
- `PressCockpitButton` is an explicit command/mutation path and writes states/targets through a short DataVault writer fence.
- `RecordTelemetry` is an explicit black-box write path and releases the telemetry writer fence before any dump route.

Defragmentation implication: `VehicleSubOsCockpitRuntime` no longer stores seven persistent `NativeArray` fields. It stores `VaultGenerationHandle` descriptors plus scalar lock state; all native views are method-local and tied to read-only or writer-fence scopes.

## FakeRadarBlipController.cs

Read-like/native ownership routes checked:
- No `TryGet*`, `Read*`, or `Resolve*` method in the file creates, resizes, completes, or regenerates native buffers after the cleanup.
- `Render` reads `_visibleBlipMatrixCount` and the fixed managed `Matrix4x4[64]` draw buffer only.
- `LateFrameTick` consumes the already computed scalar visible-count handoff. It does not complete a job, allocate memory, or touch DataVault.

Non-read ownership/write routes:
- `ScheduleBlipCull` now performs bounded value culling directly over `SpatialQueryHit[64]` and writes `Matrix4x4` values into the existing fixed managed draw buffer.
- The previous `RadarBlip2DCullJob`, `_radarCullCandidates`, `_radarCullResults`, and `_visibleBlipMatrices` native handoff list were removed.
- `EnsureRuntimeResources` now only creates Unity mesh/material resources; it does not allocate native collections.

Defragmentation implication: `FakeRadarBlipController` no longer stores `NativeArray<RadarCullCandidate>`, `NativeArray<RadarCullResult>`, or `NativeList<Matrix4x4>` between phases. The old tiny job/same-frame completion route is gone, so there is no native pointer retention to protect across simulation phases.

## Residual Project Truth - After VehicleSubOs And FakeRadar

Current Roslyn ledger: 2398 files, 0 parse failures, 7736 native field declarations, 2224 forbidden persistent native alias candidates, 661 MonoBehaviour candidates across 60 files, proof hash `aeca727724ffa3d082660c7c28726bd038689766a6e25f42ab9fc5e9d335638e`. `VehicleSubOsCockpitRuntime.cs` and `FakeRadarBlipController.cs` both report 0 forbidden persistent native alias findings.

## EcosystemDirector.cs

Read-like/native ownership routes checked:
- `VaultNativeArray<T>.IsCreated`, `Length`, indexer, `GetSubArray`, `Resolve`, and implicit conversion resolve through `IDataVault.TryResolveHandle` only.
- The wrapper no longer stores a `NativeArray<T>` field, so it cannot retain a stale raw native view across simulation phases.
- These wrapper reads do not allocate/grow DataVault buffers, complete jobs, publish signals, search scene state, or mutate global state.

Non-read ownership/write routes:
- Existing ecosystem setup paths still create generation handles through DataVault. This slice only removed the hidden cached native view inside the wrapper descriptor.
- Writes through the wrapper indexer still resolve a method-local view and write the addressed row; the view is not retained after the setter returns.

Defragmentation implication: `EcosystemDirector` still has many vault-backed descriptors, but the descriptor type itself is now relocation-safe. The previous hidden `_array` field was a persistent native alias carrier; it is gone.

## WorldProceduralFieldSampler.cs

Read-like DataVault use checked:
- `TryGetZoneData`, `TryGetBiomeMatrixData`, and `TryGetBiomeFamilyData` validate the cached vault/handle and call `TryReadOnlyHandle` through `TryReadVaultBuffer`. They do not call `PrepareBurstData`, `TryEnsureVaultBufferCapacity`, `EnsureNoiseLookupTable`, acquire writer fences, complete jobs, or publish signals.
- `ResolveSecondaryBiomeFamily` falls back to `TryReadVaultBuffer` for biome matrix rows only after scalar/profile checks fail. It does not regenerate sampler buffers.
- `TryReadVaultBuffer` itself only validates handle identity and returns a read-only view. It fails closed on missing vault, missing handle, stale generation, or missing native storage.

Non-read ownership/write routes:
- `PrepareBurstData` is explicit sampler preparation and can create/grow the six DataVault payloads: zone rows, biome matrix rows, matrix-index lookup, biome family rows, cave entrance hints, and noise LUT.
- `EnsureNoiseLookupTable` is a private data preparation helper. It can create/fill the fixed 512x512 ushort LUT, but it is not called from any `TryGet*`, `Read*`, or public read accessor in this slice.
- `ScheduleCellSamplingJob` acquires writer fences for all six job input buffers immediately before scheduling `CellSamplingJob`. The fences remain held until `CompletePendingSamplingJobForBarrier`, teardown, or the schedule-failure catch path releases them.
- `DisposeBurstData` completes the pending sampling job before releasing DataVault handles.

Defragmentation implication: `WorldProceduralFieldSampler` no longer stores six persistent native arrays in the MonoBehaviour. It stores `VaultGenerationHandle` descriptors plus scalar counts/lock state; all native views are method-local and tied to preparation, read-only, or scheduled-job ownership windows.

## Residual Project Truth - After Ecosystem Wrapper And WorldProceduralFieldSampler

Scoped regex for direct private native collection fields in `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs` and `Assets/_Project/Scripts/World/EcosystemDirector.cs` returns 0 findings. Full Roslyn ledger and clean compile proof are pending the next allowed build window because current CPU-gate sampling is above the project threshold.

## RadiationHazardGrid.cs

Read-like/native ownership routes checked:
- `TrySampleRadiationIntensity01` reads scalar state through `SampleGridNearest` and `SampleInverseSquare`. It does not call `EnsureNativeBuffers`, allocate/grow DataVault buffers, complete jobs, publish signals, or search scene state.
- `SampleGridNearest` and `SampleInverseSquare` use `VaultNativeArray<T>` descriptor wrappers. The wrappers resolve method-local native views through DataVault and do not retain `NativeArray` fields in the MonoBehaviour.
- `EncodeSparseRle`, `RecordTelemetry`, and dump routes still operate on explicit save/export paths, not read accessors.

Non-read ownership/write routes:
- `EnsureNativeBuffers` and `EnsureVaultState` remain cold/setup capacity paths and can create the existing radiation DataVault payloads.
- Simulation scheduling, emergency mock source generation, load/save application, source registration, telemetry writes, and RLE decode are explicit mutation paths.
- The latest slice removed twelve cached raw native view fields and kept the existing DataVault handles as the single native ownership route.

Defragmentation implication: `RadiationHazardGrid` no longer stores `NativeArray<float>`, `NativeArray<RadiationStateDTO>`, `NativeArray<RadiationSource>`, `NativeArray<RadiationTelemetryEntry>`, or other raw collection views between phases. It stores descriptors plus scalar state; method-local views are resolved only where existing mutation/job/read code needs them.

## Residual Project Truth - After Radiation Wrapper

Scoped regex for direct private native collection fields in `RadiationHazardGrid.cs`, `WorldProceduralFieldSampler.cs`, and `World/EcosystemDirector.cs` returns 0 findings. Clean compile proof is pending because Unity/Bee currently holds active compiler processes.

## WorldProceduralScatterDirectorMigratorySargassum.cs

Read-like/native ownership routes checked:
- `TryEvaluateMigratorySargassumShade` delegates to the raycast-up canopy sampler. It does not allocate/grow DataVault buffers, complete jobs, or publish signals.
- `TryGetNearestMigratorySargassumIsland` reads current descriptor-backed island rows and returns value data only. It does not call `EnsureMigratorySargassumLane`, create/grow buffers, acquire writer locks, or complete the drift job.
- `MigratoryVaultArray<T>.IsCreated`, `Length`, and indexer resolve a method-local native view through an existing generation handle. The wrapper stores no `NativeArray<T>` field and cannot retain a raw native alias across phases.

Non-read ownership/write routes:
- `EnsureMigratorySargassumLane` is an owner setup path. It can create the six fixed DataVault buffers under `SystemID.WorldSargassum`.
- `ScheduleMigratorySargassumJob` is an explicit mutation/job path. It acquires writer locks for island and flow-sample buffers before sampling flow and scheduling `UpdateMigratorySargassumIslandsJob`.
- `CompleteMigratorySargassumJobIfReady`, forced teardown, schedule failure, and DataVault replacement release the writer locks. No read helper completes the job.
- DataVault replacement calls `OnMigratorySargassumDataVaultReplaced`, completes any active migratory job, releases old vault handles, and resets scalar island count before rebinding.

Defragmentation implication: `WorldProceduralScatterDirectorMigratorySargassum.cs` no longer stores six persistent `NativeArray` fields in the MonoBehaviour. It stores descriptor wrappers plus scalar lock state. Native views are method-local and the two buffers used by Burst are writer-locked for the full scheduled-job ownership window.

## Residual Project Truth - After Migratory Sargassum

Scoped regex for direct private native collection fields in `WorldProceduralScatterDirectorMigratorySargassum.cs` returns 0 findings. Clean build and full Roslyn ledger are pending because the latest gate sample had CPU 50.56% and active `dotnet exec ... VBCSCompiler.dll` process `52216`.

## MarauderOutpostGenerationService.cs

Read-like DataVault use checked:
- `TryGetWfcGrid` checks `_generated`, then reads through `TryReadWfcGrid`. It does not call `EnsurePersistentState`, allocate/grow DataVault buffers, complete jobs, publish signals, or search the scene.
- `TryGetShellMatrices` checks `_generated` and `_jobPhase`, then reads through `TryReadShellMatrices`. It does not schedule or complete work.
- `TryReadWfcGrid`, `TryReadShellMatrices`, `TryReadShellCellTypes`, `TryReadInteractableSpawns`, `TryReadCounters`, and `TryReadTelemetryRing` all route through `TryReadVaultBuffer`.
- `TryReadVaultBuffer` validates cached `IDataVault`, exact `VaultGenerationHandle<T>`, and `TryReadOnlyHandle`. It fails closed on missing/stale buffers and does not call `EnsureVaultBuffer`, `TryAcquireWriteLock`, `ReleaseWriteLock`, `Complete`, or any signal publication route.
- `ComputeGridHash`, `SpawnInteractableProxies`, `ProcessDoorPowerSignals`, `DumpBlackBox`, and `UploadMatricesAndArgs` consume read-only DataVault views only.

Non-read ownership/write routes:
- `EnsurePersistentState` and `EnsureVaultBuffer<T>` are cold/setup capacity routes. They can create/grow DataVault buffers, but no `TryGet*` or `TryRead*` accessor calls them.
- `TryRequestGeneration` acquires the WFC grid writer lock through `TryAcquireSolveJobBuffer` immediately before scheduling `MarauderOutpostSolveJob`; late-frame/teardown/schedule-failure paths release the lock.
- `ScheduleMatrixExtraction` acquires writer locks for WFC grid, mutable grid, shell matrices, cell types, interactable spawns, and counters before `MarauderOutpostMatrixExtractionJob`; completion, teardown, and catch paths release all locks.
- `ApplyAupShift` acquires a shell-matrix writer lock only for `MarauderOutpostAupShiftJob` and releases it after the scheduled shift completes or fails.
- `TryPublishGeneratedSignal`, `RestoreWfcMutableState`, `ApplyPendingShiftToExtractedData`, and `WriteTelemetry` are explicit mutation/publication routes. They acquire writer locks in local scopes and release in `finally`.
- `OnDataVaultReplaced` completes any active outpost job, releases all writer locks and old handles, then cold-rebinds only if the component is active.

Defragmentation implication: `MarauderOutpostGenerationService` no longer stores `NativeArray<byte>`, `NativeArray<float4x4>`, `NativeArray<uint>`, `NativeArray<int>`, or `NativeArray<OutpostTelemetryEntry>` fields. It stores DataVault handles and scalar lock state only. Native views are method-local and protected by read-only handle validation or writer locks for the full Burst job ownership window.

## Residual Project Truth - After Marauder Outpost

Scoped regex for direct persistent native collection fields in `MarauderOutpostGenerationService.cs` returns 0 findings. Clean build completed with 0 warnings and 0 errors after the CPU/process gate cleared. Full Roslyn ledger now reports 2406 files, 0 parse failures, 2138 forbidden persistent candidates, and 581 MonoBehaviour candidates across 58 files, proof hash `1a2db4092081840dfc0366bb82ed12aaa304e226b1fc5b3b1ee858e37456c58a`.

## CrashTelemetryBuffer.cs

Read-like/native ownership routes checked:
- `IsInitialized` checks `_ringBuffer.IsCreated`, which resolves the existing DataVault handle through the descriptor and does not allocate/grow a buffer, complete jobs, publish signals, or search scene state.
- Static telemetry entrypoints first check `instance == null || !instance._ringBuffer.IsCreated`. They fail closed if the descriptor cannot resolve and do not create fallback native storage.
- `VaultArray<T>.IsCreated`, `Length`, indexer, and implicit conversion resolve method-local native views through `IDataVault.TryResolveHandle`. The wrapper stores no `NativeArray<T>` field and cannot retain a raw native alias across phases.

Non-read ownership/write routes:
- `InitializeBuffers` is the explicit owner setup route. It creates the fixed `CrashTelemetryRing`, `CrashTelemetryExportSnapshot`, and `CrashTelemetryExportScratch` DataVault payloads under `SystemID.CoreDiagnostics`.
- Runtime telemetry writes assign one `TelemetryEntry` row into the ring through the descriptor indexer. This is a black-box write path, not a `TryGet*` or `Read*` accessor.
- `TryExportSnapshot` and `TryExportSnapshotFromUnhandledException` are explicit export mutation paths. They copy recent ring rows into the snapshot, build the scratch payload on the owner thread, mirror bytes into `_crashExportFileScratch`, then signal the background worker.
- `WritePreparedExportToDisk` writes `_crashExportFileScratch` only. The worker thread does not resolve `_exportScratch`, `_exportSnapshot`, or any DataVault handle.
- DataVault replacement calls `DisposeBuffers`, releases the old handles, caches the replacement vault, then reinitializes through the cold setup route.

Defragmentation implication: `CrashTelemetryBuffer` no longer stores `NativeArray<TelemetryEntry>` or `NativeArray<byte>` fields in the MonoBehaviour. The three native payloads are DataVault-owned, and every native view is method-local. The background export path has no DataVault view lifetime, so relocation/defrag cannot race a worker-thread handle resolve.

## Residual Project Truth - After CrashTelemetryBuffer Static Proof

Scoped regex for direct persistent native collection fields in `CrashTelemetryBuffer.cs` returns 0 findings. Clean compile and full Roslyn ledger refresh are still pending because the current build gate reports CPU 100% with active `dotnet`/`csc` processes.
