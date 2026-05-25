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
