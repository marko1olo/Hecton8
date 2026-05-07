# HECTON-8 OMEGA Build Restoration / Unsafe Purge V2

Date: 2026-05-07
Auditor: Codex
Status: PENDING VERIFICATION

## Mandates Followed

- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## What Was Wrong

- `NoOpAudioService` did not satisfy the current `IAudioService` contract during bootstrap compile passes.
- Bee temporarily failed with artifact lock/internal build-system errors after earlier imports; those were not source diagnostics.
- `VegetationJobRecovery.cs` and `.meta` were deleted during concurrent work. Final disk state has both files deleted together, so there is no orphaned `.meta` GUID mismatch.
- The old request referenced 43 naked `UnsafeUtility.MemCpy` callsites. Current source truth is different: first-party raw `UnsafeUtility.MemCpy` is present only inside `UnsafeMemoryCopyGuard`.

## What Changed

- `GameBootstrapper.NoOpAudioService` now implements `TryEmitModAcousticPing(Vector3 runtimePosition, float intensity01)` and returns `false` as the null-object fallback.
- `PlayerStressVFX` and `SuitHUDV4CanvasOverlay` now resolve against the `PlayerSignalEvents.Register/Unregister` listener API in current source. Stale event-field diagnostics were cleared by the final successful compile.
- No bootstrap UI ownership rewrite was needed. `GameBootstrapper` already owns `_uiPrefabInstanceHandles`, `LoadAddressableUIPrefabsAsync`, `InstantiateAsync(transform)`, and `ReleaseAddressableUIPrefabs` via `Addressables.ReleaseInstance`.

## Unsafe Copy State

Raw copy sweep:

```text
rg -n "UnsafeUtility\.MemCpy" Assets/_Project/Scripts -g '*.cs'
Assets/_Project/Scripts\Core\UnsafeMemoryCopyGuard.cs:66
```

Guarded copy counts in requested domains:

```text
SaveBinaryStorage.cs: 28
VoxelDeltaProcessor.cs: 8
Quest/QuestStateManager.cs: 4
```

Adjacent guarded copy counts:

```text
SaveBinaryPayloadCodec.cs: 5
HectonSubmarineOsDisplay.cs: 1
SystemDispatcher.cs: 2
GlobalTelemetryBus.cs: 1
SaveManager.cs: 1
SaveDataMigration_AupV8.cs: 3
SaveSidecarStorage.cs: 1
```

Guarded site ledger:

```text
SaveBinaryStorage.cs:71
SaveBinaryStorage.cs:81
SaveBinaryStorage.cs:135
SaveBinaryStorage.cs:1070
SaveBinaryStorage.cs:1086
SaveBinaryStorage.cs:1660
SaveBinaryStorage.cs:1940
SaveBinaryStorage.cs:1983
SaveBinaryStorage.cs:1997
SaveBinaryStorage.cs:2762
SaveBinaryStorage.cs:2846
SaveBinaryStorage.cs:3006
SaveBinaryStorage.cs:3155
SaveBinaryStorage.cs:3216
SaveBinaryStorage.cs:3798
SaveBinaryStorage.cs:4123
SaveBinaryStorage.cs:4181
SaveBinaryStorage.cs:4233
SaveBinaryStorage.cs:4342
SaveBinaryStorage.cs:4355
SaveBinaryStorage.cs:4557
SaveBinaryStorage.cs:4605
SaveBinaryStorage.cs:4698
SaveBinaryStorage.cs:4712
SaveBinaryStorage.cs:4724
SaveBinaryStorage.cs:5276
SaveBinaryStorage.cs:5391
SaveBinaryStorage.cs:5435
VoxelDeltaProcessor.cs:737
VoxelDeltaProcessor.cs:743
VoxelDeltaProcessor.cs:749
VoxelDeltaProcessor.cs:755
VoxelDeltaProcessor.cs:867
VoxelDeltaProcessor.cs:875
VoxelDeltaProcessor.cs:883
VoxelDeltaProcessor.cs:893
Quest/QuestStateManager.cs:749
Quest/QuestStateManager.cs:796
Quest/QuestStateManager.cs:851
Quest/QuestStateManager.cs:1204
SaveBinaryPayloadCodec.cs:51
SaveBinaryPayloadCodec.cs:1957
SaveBinaryPayloadCodec.cs:1994
SaveBinaryPayloadCodec.cs:2071
SaveBinaryPayloadCodec.cs:2166
HectonSubmarineOsDisplay.cs:631
SystemDispatcher.cs:1401
SystemDispatcher.cs:1423
GlobalTelemetryBus.cs:514
SaveManager.cs:404
SaveDataMigration_AupV8.cs:121
SaveDataMigration_AupV8.cs:168
SaveDataMigration_AupV8.cs:225
SaveSidecarStorage.cs:350
```

Guard behavior verified by source:

- `UnsafeMemoryCopyGuard.SafeCopy` checks negative sizes, null pointers, zero-length copies, and `sourceSizeBytes <= destinationSizeBytes`.
- Editor/development invalid copies throw `FatalMemoryCorruptionException`.
- Release overflow copies clamp to destination capacity and return `false`.
- Successful copies increment `GlobalTelemetryBus.RecordNativeCopy(copySizeBytes)`.
- HUD exposure exists through `GlobalTelemetryBus.NativeCopyMegabyteCount`.

## Linkage / Sweep Results

UI linkage:

```text
GameBootstrapper.cs:195 _uiPrefabInstanceHandles
GameBootstrapper.cs:803 LoadAddressableUIPrefabsAsync call
GameBootstrapper.cs:810 LoadAddressableUIPrefabsAsync
GameBootstrapper.cs:868 ReleaseAddressableUIPrefabs
GameBootstrapper.cs:878 Addressables.ReleaseInstance(handle)
```

Compliance validator:

```text
HectonComplianceValidator.cs:29 UnsafeMemCpyNeedle
HectonComplianceValidator.cs:97 ValidateUnsafeMemCpyUsage(report)
HectonComplianceValidator.cs:423 ValidateUnsafeMemCpyUsage
```

Final source sweeps:

```text
Fauna/World Find APIs: 0 matches
GetInstanceID in first-party scripts: 0 matches
SystemDispatcher string interpolation/concat/ToString scan: 0 matches
```

Broad first-party `Find*` still exists in bootstrap cold-path discovery. That is outside the requested Fauna/World replacement scope and should not be refactored without a separate bootstrap architecture task.

## Verification

Compile:

```text
Editor-prev.log latest successful compiler marker after Unity shutdown: line 126901
errorCSAfter=0
warningCSAfter=0
```

MCP console:

```text
Earlier post-compile read_console(types=["error","warning"]) => Retrieved 0 log entries.
Final read_console unavailable: Unity stopped MCP local HTTP server on port 8088 during editor shutdown cleanup after the final successful compile.
```

Third-party warnings observed earlier are from RealtimeCSG, Crest, and MapMagic, before the final clean compiler marker. They are excluded by mandate.

## Regression Model

- CPU: SafeCopy adds scalar branch checks and telemetry counter increments around native copies. This is safer but may be measurable on bulk-copy hot loops; no runtime profiler sample was run.
- GC: The edited bootstrap fallback adds no per-frame allocation. Runtime GC proof is absent because no play-mode GCMonitor capture was executed.
- Memory: Static pinned LZ4 dictionary path is reset-safe through subsystem registration. No new persistent native allocation was added in this pass.
- Cadence: No Tick/Update loop was added. Bootstrap UI load remains cold async `Awaitable`.
- Correctness: Invalid native copy bounds fail fast in editor/development and clamp in release. This prevents overwrite but can truncate corrupted payloads; callers already treat `false` as failure.
