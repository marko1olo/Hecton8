# AWAITABLE MEMORY COMPACTION SURGERY LOG

Date: 2026-04-30
Status: PENDING VERIFICATION

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
| Coroutine grep hygiene | Removed a false-positive literal coroutine token from `InteractionHighlighter` comments; no runtime code changed there. |
| Compile hygiene | Fixed a stale editor compile error in `HectonComplianceValidator` by fully qualifying `global::System.Environment.GetEnvironmentVariable`. |
| Graveyard sync | Updated `DEAD_CODE_GRAVEYARD.md` with actual removal state and retained `WorldGenerativeGeologyRuntimeSmokeTester` because current editor code references it. |
| Crash telemetry MMF/cache check | `CrashTelemetryBuffer` already uses `NativeArray<byte>` export scratch, `UnsafeUtility.MemCpy`, and `AsyncWriteManager.WriteAll` over a native pointer. No managed `byte[]` export buffer was introduced. |

## Awaitable / Coroutine State

| Metric | Before this pass | After this pass |
|---|---:|---:|
| `StartCoroutine(` live call sites outside `Editor/**` | 37 baseline legacy sites | 15 remaining |
| `StartCoroutine(` strict lexical hits across `Assets/_Project/Scripts` | 37 baseline legacy sites | 15 current hits |
| `IEnumerator` / `yield` lexical hits | Previous rough grep was not comparable | 354 current broad lexical hits outside `Editor/**` |

Remaining coroutine sites are concentrated in runtime smoke/verifier infrastructure:

- `FieldToolRuntimeSmokeTester`
- `ToolRuntimeSmokeTester`
- `ToolTrialRangeRuntimeSmokeTester`
- `Dev/ShellVerificationRuntimeSmokeTester`
- `Tools/StateRecoveryVerifier`

These were not migrated in this pass because they are verification harnesses with nested callback/coroutine chains. Mechanical conversion to `async Awaitable` would be broad behavioral surgery and must be done one harness at a time with compile verification.

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
| Assets refs to removed four classes | PASS: no current refs under `Assets/`. |
| `CrashTelemetryBuffer` export path | PASS by code inspection: `NativeArray<byte>` + `UnsafeUtility.MemCpy` + native pointer write. |
| Runtime `Spawn` expansion | PASS by code inspection: spawn path returns `null`; `InstantiatePooled` is only used from `Warmup`. |
| Migrated harness coroutine scan | PASS: migrated files have no `StartCoroutine`, `IEnumerator`, or `yield return` hits. |
| Unity MCP refresh/console | PASS: after a forced script compile/domain reload, `read_console(types=["error"])` returned 0 entries. |
| Editor log tail fallback | Prior `HectonComplianceValidator` and transient `ConstructionManager` compile errors were stale after the latest domain reload; final MCP console read reported 0 errors. |

STATUS: MCP CONSOLE VERIFIED; PLAY MODE / GC PROFILER VERIFICATION NOT RUN
