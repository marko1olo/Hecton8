# OMEGA Build Restoration and Unsafe Purge - 2026-05-01
Date: 2026-05-07
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R13 Report Snapshot Boundary

This report file is a snapshot/provenance document. It is active only where it agrees with:

- `Docs/README.md`
- `Docs/Reports/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Historical `PASS`, `VERIFIED`, `current`, `latest`, counter, compile, runtime, 0-GC, frame-time, cost, and performance statements inside this report are not current proof unless the exact claim links a fresh artifact path, command/tool, timestamp, evidence class, and unresolved-error list. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied by this file alone.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Mandates Followed

- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Verdict

Status: PENDING VERIFICATION


Verification sequence:

- MCP `read_console` after the first successful compile returned 0 errors and 0 warnings.
- A later MCP `read_console` surfaced stale compiler entries while Unity was recompiling changed files.
- Forced script refresh completed with Unity Editor log `ExitCode: 0` and `Begin MonoManager ReloadAssembly`; no `error CS` or `warning CS` entries appear after the final successful compile block.
- Final MCP direct console readback became unavailable because the MCP WebSocket bridge stopped answering ping after the reload. The only active warning in the editor log for that phase is MCP transport closure from `com.coplaydev.unity-mcp`, not first-party code.

Scope note: this is compile/editor-log verification plus earlier MCP console proof. No playmode GCMonitor capture was run in this pass, so runtime 0 B/frame remains pending runtime profiling.

## What Was Wrong

- Bootstrap compile state was red from stale UI lifecycle linkage and stale Bee source lists.
- First-party code still contained raw `UnsafeUtility.MemCpy` callsites outside the approved bounds guard.
- `SystemDispatcher.Update()` contained hot-path string interpolation in the AUP NaN diagnostic.
- The editor compliance validator did not enforce the new unsafe-copy gate.
- The stale `Input/RebindingManager.cs` asset path could block compilation while the real implementation lived under Core.

## What Changed

- `UnsafeMemoryCopyGuard.SafeCopy` is now the only first-party wrapper allowed to call `UnsafeUtility.MemCpy`.
- `SafeCopy` rejects null pointers and `sourceSizeBytes > destinationSizeBytes`, then records native-copy throughput in `GlobalTelemetryBus`.
- `GlobalTelemetryBus` now tracks native copy bytes and operation count for memory heatmap telemetry.
- `SystemDispatcher` hot-path warning text now uses a cached constant, not interpolation or concatenation.
- `HectonComplianceValidator` now includes a time-sliced unsafe `MemCpy` phase and CI validation for raw `UnsafeUtility.MemCpy` usage outside the guard.
- `GlobalRegistry` service-rebound dispatch now uses the NativeQueue lane with sidecar reference slots instead of the removed managed pending array.
- `Input/RebindingManager.cs` is a tombstone for stale Unity/Bee source lists; the real implementation exists at `Assets/_Project/Scripts/Core/RebindingManager.cs`.

## Unsafe Copy Surgery Log

User report referenced 43 sites. Active source scan found 44 unguarded first-party callsites outside `UnsafeMemoryCopyGuard`; all 44 are now routed through `UnsafeMemoryCopyGuard.SafeCopy`.

1. `QuestStateManager.cs` - packed global prerequisites snapshot write.
2. `QuestStateManager.cs` - packed global prerequisites restore read.
3. `QuestStateManager.cs` - transition history restore read.
4. `QuestStateManager.cs` - transition history append write.
5. `VoxelDeltaProcessor.cs` - dirty mask to native snapshot.
6. `VoxelDeltaProcessor.cs` - SDF bits to native snapshot.
7. `VoxelDeltaProcessor.cs` - material IDs to native snapshot.
8. `VoxelDeltaProcessor.cs` - cell flags to native snapshot.
9. `VoxelDeltaProcessor.cs` - native snapshot to dirty mask.
10. `VoxelDeltaProcessor.cs` - native snapshot to SDF bits.
11. `VoxelDeltaProcessor.cs` - native snapshot to material IDs.
12. `VoxelDeltaProcessor.cs` - native snapshot to cell flags.
13. `SaveManager.cs` - integrity payload mirror copy.
14. `SaveDataMigration_AupV8.cs` - v7 prefix read.
15. `SaveDataMigration_AupV8.cs` - v8 prefix read.
16. `SaveDataMigration_AupV8.cs` - migration source prefix read.
17. `SaveBinaryStorage.cs` - async write first segment.
18. `SaveBinaryStorage.cs` - async write second segment.
19. `SaveBinaryStorage.cs` - mapped read payload.
20. `SaveBinaryStorage.cs` - metadata packed quest write.
21. `SaveBinaryStorage.cs` - metadata voxel snapshot write.
22. `SaveBinaryStorage.cs` - indexed block compressed payload staging.
23. `SaveBinaryStorage.cs` - indexed packed quest read.
24. `SaveBinaryStorage.cs` - indexed ecosystem section read.
25. `SaveBinaryStorage.cs` - indexed voxel snapshot read.
26. `SaveBinaryStorage.cs` - entity-state override read.
27. `SaveBinaryStorage.cs` - mod payload write.
28. `SaveBinaryStorage.cs` - mod payload read.
29. `SaveBinaryStorage.cs` - sector override staging copy.
30. `SaveBinaryStorage.cs` - sector override commit copy.
31. `SaveBinaryStorage.cs` - protected dictionary block prepend.
32. `SaveBinaryStorage.cs` - packed quest-state section read.
33. `SaveBinaryStorage.cs` - persistent-world delta read.
34. `SaveBinaryStorage.cs` - voxel delta payload read.
35. `SaveBinaryStorage.cs` - persistent-world chunk table write.
36. `SaveBinaryStorage.cs` - persistent-world item table write.
37. `SaveBinaryStorage.cs` - ecosystem section write.
38. `SaveBinaryStorage.cs` - ecosystem section read.
39. `SaveBinaryStorage.cs` - dictionary compression winning payload copy.
40. `SaveBinaryStorage.cs` - token compression winning payload copy.
41. `SaveBinaryStorage.cs` - token plus dictionary compression winning payload copy.
42. `SaveBinaryStorage.cs` - UTF16 metadata string write.
43. `SaveBinaryStorage.cs` - LZ4 static dictionary staging.
44. `SaveBinaryStorage.cs` - static dictionary decompression payload copy.

Final raw unsafe scan:

```text
Assets/_Project/Scripts/Core/UnsafeMemoryCopyGuard.cs:30: UnsafeUtility.MemCpy(destination, source, sourceSizeBytes);
```

## Static Native State

Core static native holders inspected for subsystem-registration reset coverage:

- `GlobalRegistry`
- `GlobalTelemetryBus`
- `SystemDispatcher`
- `ThreadSafeCommandQueue`
- `UIStateStore`
- `NativeArenaAllocator`

Each has a `RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)` reset path. The changed native-copy telemetry counters are reset with `GlobalTelemetryBus.DisposeStaticState`.

## Additional Sweeps

- `Fauna/` `GameObject.Find` and Unity Find APIs: 0 hits.
- First-party `GetInstanceID()` calls: 0 hits.
- `SystemDispatcher.cs` string interpolation or concatenation scan: 0 hits.
- Raw first-party `UnsafeUtility.MemCpy` outside the guard: 0 hits.

## Regression Model

- CPU: `SafeCopy` adds scalar bounds checks and telemetry increments around byte-copy operations. Cost is proportional to existing copy count, not byte length beyond the original copy.
- GC: no managed allocations added to hot paths; dispatcher diagnostic string allocation removed.
- Memory: no new persistent native containers added. Telemetry uses existing static counters.
- Cadence: validator unsafe scan is time-sliced through the existing deferred validation path; CI path remains hard-fail capable.
- Correctness: copy sizes now fail closed when source exceeds destination. Risk is early rejection of previously unsafe over-copy behavior.

## Evidence Artifacts

- Diff: `.codex-artifacts/2026-05-01_build_restoration_unsafe_purge.diff`
