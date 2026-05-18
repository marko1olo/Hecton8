# AWAITABLE MEMORY COMPACTION SURGERY LOG

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

Scope: safe subset of the OMEGA Awaitable Migration & Memory Compaction pass.
No Play Mode was launched.

## Mandates Followed

| Mandate | Applied rule |
|---|---|
| `PROJECT_LTS_Compatibility_Layer` | Asmdef purification must be staged through bridge assemblies, not blind reference deletion. |
| `OPT_Zero_GC_Policy_AllocFree_Mandate` | Coroutine removal must not introduce hot-path heap allocations. |
| `OPT_Native_Memory_Collections_JobSystem_Protocol` | Native telemetry buffers must keep explicit ownership and disposal. |
| `STRM_Asset_Lifecycle_Addressables_Loading_Memory` | Runtime object pool exhaustion must not instantiate fallback objects. |
| `DATA_Save_Persistence_Binary_Delta_Checksum` | Save binary storage already uses mapped native write/read paths. |
| `DBG_Telemetry_Crash_Reporting_PostMortem` | Crash exports must use blittable buffers and `UnsafeUtility.MemCpy`. |

## Completed

| Area | Result |
|---|---|
| Object pool lockdown | `ObjectPoolManager.Spawn` still returns `null` on missing/exhausted pools and never calls `Instantiate` on runtime spawn. Pool exhaustion now publishes a structured `PoolExhausted` telemetry event. |
| Telemetry prewarm | `ObjectPoolManager.InitializeService` calls `GlobalTelemetryBus.Initialize()` so the telemetry ring is cold-allocated during service init, not first pool-exhaustion failure. |
| Dead code excavation | Removed four orphan candidates with no current `Assets/` code refs and no YAML refs: `SaveSystemRuntimeSmokeTester`, `WeakToolsRuntimeSmokeTester`, `MantaAcousticRuntimeVerifier`, `PhysicalInteractionRuntimeVerifier`, plus `.meta` files. |
| Awaitable migration batch 1 | `FabricationRuntimeSmokeTester`, `ScanRuntimeSmokeTester`, `BarterRuntimeSmokeTester`, and `BuilderRuntimeSmokeTester` no longer use `StartCoroutine`, `IEnumerator`, or `yield return`. They now run through `async Awaitable` methods using `destroyCancellationToken`. |
| Awaitable migration batch 2 | `PauseSystemVerifier`, `SceneTransitionVerifier`, `UIRuntimeSmokeTester`, and `WorldGenerativeGeologyRuntimeSmokeTester` no longer use `StartCoroutine`, `IEnumerator`, or `yield return`. Public verifier entry points were preserved; waits now use `Awaitable.NextFrameAsync` plus realtime deadline checks. |
| Awaitable migration batch 3 | `StateRecoveryVerifier` no longer uses `StartCoroutine`, `IEnumerator`, `yield return`, or `WaitForSecondsRealtime`. Public verifier entry points are preserved; waits now use `Awaitable.NextFrameAsync` with `destroyCancellationToken`. |
| Awaitable migration batch 4 | `ToolRuntimeSmokeTester` no longer uses `StartCoroutine`, `IEnumerator`, `yield return`, or `WaitForSecondsRealtime`. Manual/context-menu smoke entry points are preserved; waits now use `Awaitable.NextFrameAsync` with `destroyCancellationToken`. |
| Awaitable migration batch 5 | `FieldToolRuntimeSmokeTester` no longer uses `StartCoroutine`, `IEnumerator`, `yield return`, or `WaitForSecondsRealtime`. Salvage/cutter smoke phases now return `Awaitable<bool>` results and keep probe cleanup in `finally`. |
| Awaitable migration batch 6 | `ToolTrialRangeRuntimeSmokeTester` no longer uses `StartCoroutine`, `IEnumerator`, `yield return`, or `WaitForSecondsRealtime`. Authored lane passes now return `Awaitable<bool>` results and preserve loadout/player-pose restore. |
| Awaitable migration batch 7 | `Dev/ShellVerificationRuntimeSmokeTester` no longer uses `StartCoroutine`, `IEnumerator`, `yield return`, or `WaitForSecondsRealtime`. Auto-start, resume, editor-stability, menu/world, pause, input, and load-slot waits now use `Awaitable` with `destroyCancellationToken`. |
| Coroutine grep hygiene | Removed false-positive or disabled coroutine tokens from `InteractionHighlighter`, `FaunaDirector`, and the dead `#if false` slow-tick stub in `GameTickManager`; no runtime slow-tick logic changed. |
| Compile hygiene | Fixed a stale editor compile error in `HectonComplianceValidator` by fully qualifying `global::System.Environment.GetEnvironmentVariable`. |
| Graveyard sync | Updated `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/DEAD_CODE_GRAVEYARD.md` with actual removal state and retained `WorldGenerativeGeologyRuntimeSmokeTester` because current editor code references it. |
| Crash telemetry MMF/cache check | `CrashTelemetryBuffer` already uses `NativeArray<byte>` export scratch, `UnsafeUtility.MemCpy`, and `AsyncWriteManager.WriteAll` over a native pointer. No managed `byte[]` export buffer was introduced. |

## Awaitable / Coroutine State

| Metric | Before this pass | After this pass |
|---|---:|---:|
| `StartCoroutine(` live call sites outside `Editor/**` | 37 baseline legacy sites | 0 remaining |
| `StartCoroutine(` strict lexical hits across `Assets/_Project/Scripts` | 37 baseline legacy sites | 0 current hits |
| `IEnumerator` / `yield return` lexical hits | Previous rough grep was not comparable | 0 current broad lexical hits outside `Editor/**` |

Remaining strict coroutine sites outside `Editor/**`: none by grep. Compile and Play Mode proof are still absent in the current session.

## Rejected / Blocked

### Global asmdef purification

Not executed.

Facts:
- `Hecton8.Core.asmdef` is still the assembly root for most first-party scripts.
- The previous dependency scan found `1,879` UI/TMP/URP/Crest/MapMagic/GPUInstancer/third-party hits below that root.
- Removing UI/URP package references before nested bridge asmdefs exist would create compile errors.

Required next step:
- Create staged bridge assemblies for UI, URP/Visor, Crest, MapMagic, GPUInstancer, and world rendering.
- Move owned files into those assemblies before deleting root references.

### WorldGenerativeGeologyRuntimeSmokeTester deletion

Rejected.

Facts:
- Current `Assets/_Project/Editor/HectonDevToolsMenu.cs` references and can add `WorldGenerativeGeologyRuntimeSmokeTester`.
- Deleting it without editor menu migration would break compilation.

## Verification

| Check | Result |
|---|---|
| Assets refs to removed four classes | Historical STATIC_SOURCE pass: no refs under `Assets/` in that pass. |
| `CrashTelemetryBuffer` export path | Historical STATIC_SOURCE code inspection: `NativeArray<byte>` + `UnsafeUtility.MemCpy` + native pointer write. |
| Runtime `Spawn` expansion | Historical STATIC_SOURCE code inspection: spawn path returns `null`; `InstantiatePooled` is only used from `Warmup`. |
| Migrated harness coroutine scan | Historical STATIC_SOURCE pass: migrated files had no `StartCoroutine`, `IEnumerator`, or `yield return` hits. |
| Unity MCP refresh/console | PASS for that surgery session only: after a forced script compile/domain reload, `read_console(types=["error"])` returned 0 entries. |
| Editor log tail fallback | Prior `HectonComplianceValidator` and transient `ConstructionManager` compile errors were stale after that domain reload; final MCP console read in that session reported 0 errors. |

STATUS: PENDING VERIFICATION - historical MCP console pass recorded; current session not rechecked; Play Mode / GC Profiler verification not run
