# Foundation Hardening Surgery Log

Date: 2026-05-07
Status: PENDING VERIFICATION

Requested MCP console proof of `0` errors failed. Unity returned two compile errors in `GameBootstrapper.cs` and three obsolete API warnings outside this patch scope.

## Mandates Followed

- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `OPT_Native_Memory_Collections_JobSystem_Protocol`
- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`
- `PROJECT_LTS_Compatibility_Layer`

## Changes Made

### Compliance validator scheduling

`HectonComplianceValidator` no longer executes the full editor compliance sweep in one post-reload callback during normal editor use.

- Normal editor reload: time-sliced through repeated `EditorApplication.delayCall`.
- Per-slice budget: `8 ms`.
- CI / hard-failure mode: synchronous validation still runs when `Application.isBatchMode` or `HECTON_COMPLIANCE_ENFORCE=1`.
- Static bypass guard: `ShouldEnforceAsBuildGate()` checks `HECTON_COMPLIANCE_ENFORCE` for exact value `1`.

### Pool exhaustion safety

`HectonVoxelEngine.SpawnVolume()` now checks the result of `ObjectPoolManager.Instance.Spawn(...)` before `PrepareVolumeForBuild(...)`.

If the pool returns `null`, the method falls through to the existing fallback volume construction path instead of dereferencing `null`.

`DebrisManager` was reviewed. It does not call `ObjectPoolManager.Spawn`; it uses fixed native/managed slot arrays and already checks `CountFreeSlots()` and `FindFreeSlot()` before writing slot state.

### TransformAccessArray dispose race

`ProceduralLeviathanSpineIK.DisposeRuntimeBuffers()` now calls `CompletePendingJob()` before `_vertebraAccessArray.Dispose()`.

This makes the disposal method safe even if future callers invoke it without a prior explicit complete.

### UnsafeUtility.MemCpy bounds checking

Added `Hecton8.Core.UnsafeMemoryCopyGuard`.

Core logic:

```csharp
return destination != null &&
       source != null &&
       sourceSizeBytes >= 0L &&
       destinationSizeBytes >= 0L &&
       sourceSizeBytes <= destinationSizeBytes;
```

Patched checked copy sites in:

- `SaveBinaryPayloadCodec`
- `SaveSidecarStorage`
- `SystemDispatcher`
- `ConnectionSplineBatchRenderer`
- `GlobalTelemetryBus`
- `CrashTelemetryBuffer`
- `PlayerInventory`
- `PlayerExplorationTracker`

Remaining debt:

- Static scan still finds raw `UnsafeUtility.MemCpy` calls in `VoxelDeltaProcessor`, `QuestStateManager`, `SaveManager`, `SaveDataMigration_AupV8`, and `SaveBinaryStorage`.
- Verdict: MemCpy mandate is partially enforced, not complete.

### Dead code meta sync

Filesystem scan:

- Missing `.cs.meta`: `0`
- Orphan `.cs.meta`: `0`
- Graveyard-deleted pairs confirmed absent for `WeakToolsRuntimeSmokeTester`, `MantaAcousticRuntimeVerifier`, and `PhysicalInteractionRuntimeVerifier`.

### Awaitable smoke tester GC scan

Static scan over `*SmokeTester*.cs` and `*Verifier*.cs` found:

- `StartCoroutine`: `0`
- `IEnumerator`: `0`
- `yield return new WaitForSeconds`: `0`
- `WaitForSeconds(...)`: `0`
- `Awaitable.WaitForSecondsAsync`: `0` after patching `FabricationRuntimeSmokeTester`

Delay pattern is now realtime loops over `Awaitable.NextFrameAsync`, matching the existing smoke tester pattern.

## MCP Console

`refresh_unity`:

- `success=true`
- `compile_requested=true`
- resulting state: `compiling`

`read_console` returned:

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs(348,13): error CS0103: The name 'ReleaseAddressableUIPrefabs' does not exist in the current context`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs(793,24): error CS0103: The name 'LoadAddressableUIPrefabsAsync' does not exist in the current context`
- `CraftingEvents.cs(452,35): warning CS0618: Object.GetInstanceID() is obsolete`
- `InteractionEvents.cs(354,35): warning CS0618: Object.GetInstanceID() is obsolete`
- `InteractionEvents.cs(361,35): warning CS0618: Object.GetInstanceID() is obsolete`

## 2026-05-01 Local Editor.log Delta

Later local verification used `C:\Users\danat\AppData\Local\Unity\Editor\Editor.log` because MCP was unavailable.

Recorded clean scan boundary during this pass:

- total log lines: `46679`
- recorded `Mono: successfully reloaded assembly`: line `46334`
- recorded compile marker: line `46540`
- scan start: line `46541`

Fresh post-marker counts:

- `error CS`: `0`
- `warning CS`: `0`
- `Exception`: `0`
- `Resource ID out of range in SetResource`: `0`
- `There are inconsistent line endings`: `0`
- TMP `m_AtlasTextures` unassigned exception: `0`

This local delta supersedes the stale compile-warning/error state above for the current reachable log only.
It does not convert this report into Play Mode, GCMonitor, profiler, or memory-retention proof.

Note: an intermediate import exposed additional Unity 6 obsolete API warnings in editor-only third-party tooling (`Dynamic Decals` and `DOTweenPro`). Those were patched by direct API replacement before the scan boundary above.

## Regression Model

CPU: validator normal mode now has an 8 ms editor slice budget instead of one large post-reload reflection/source scan.

GC: gameplay hot path changes are branch checks and pointer-size checks only. Smoke tester delay loops avoid coroutine wait objects.

Memory: one new script and `.meta`; no runtime containers or cache growth.

Cadence: TransformAccessArray disposal now blocks only during teardown/rebuild disposal, not per-frame scheduling.

Correctness: raw `MemCpy` debt remains in several save/voxel/quest paths. Full mandate compliance requires converting those remaining call sites to `UnsafeMemoryCopyGuard`.

## Verification Verdict

- Validator performance hardening: PATCHED, editor runtime not benchmarked.
- Pool null safety: PATCHED for confirmed voxel pool dereference; DebrisManager reviewed.
- TransformAccessArray race: PATCHED.
- MemCpy bounds: PARTIAL, raw call sites remain.
- Dead meta sync: PASS static filesystem scan.
- Awaitable smoke tester scan: PASS static token scan.
- CI hard-failure env guard: PASS static source inspection, not executed in batchmode.
- MCP clean console: FAIL.

## Diff Artifact

Full diff artifact:

`C:\hades\Hecton8\.codex-artifacts\2026-05-01_foundation_hardening.diff`

STATUS: PENDING VERIFICATION
