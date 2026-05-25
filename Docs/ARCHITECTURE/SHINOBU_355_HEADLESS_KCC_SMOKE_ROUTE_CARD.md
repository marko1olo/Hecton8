# SHINOBU_355 Headless KCC Smoke Route Card

Date: 2026-05-23
Owner: SHINOBU_355 / HEADLESS_KCC_SMOKE_TESTER
Status: YELLOW / STATIC_SOURCE_WIRED / COMPILE_BLOCKED_BY_EXTERNAL_DEPENDENCY

## Boundary

SHINOBU_355 owns only offline QA verification for `HydrodynamicKccRuntime`.

It does not own production KCC authority, runtime input, rollback, voxel generation, or presentation. Smoke tester writes temporary Vault buffers, executes Burst jobs, and emits artifacts.

## Route

| Field | Value |
|---|---|
| Route ID | `SHINOBU_355_HEADLESS_KCC_SMOKE` |
| Owner | `HydrodynamicKccRuntime` partial smoke-test lane plus `Hecton8.Physics.KCC.Editor` facade |
| Producer phase | Cold editor/test bootstrap allocates Vault buffers; Burst jobs generate SDF, initialize phantoms, simulate, verify escape, analyze drift |
| Consumer phase | Thin NUnit caller, KCC editor scheduled runner, JSON report writer, CSV failure writer, black-box dump writer, editor telemetry graph/gizmo reading a retained Vault read-only handle |
| Cadence | Offline on demand, scheduled editor window run, or CI test run |
| Capacity | 100 phantom states, 10,000 frames, 1,000,000 `double3` history slots, 300 telemetry slots, 512 failure records |
| Hot communication | NativeArray DTOs in `GlobalDataVault`; no hot `GlobalRegistry` polling, no Unity Physics scene, no GameObject test actor |
| Failure artifacts | `Docs/Reports/QA_OPTIMIZATION_REPORT.json`, `Docs/Reports/QA_OPTIMIZATION_OOP_REPORT.json`, `Docs/Reports/HEADLESS_KCC_FAILURES.csv`, `Docs/AgentLogs/Dump_SHINOBU_355.bin` |

## Vault Buffers

The runner uses existing KCC buffers for core KCC lanes:

- `BufferID.ShinobuHydroKccStates`
- `BufferID.ShinobuHydroKccInputs`
- `BufferID.ShinobuHydroKccProposedVelocities`
- `BufferID.ShinobuHydroKccFaultFlags`
- `BufferID.ShinobuKccEnvironmentSdf`

Smoke-only temporary lanes use numeric IDs `71810..71818`:

- smoke states
- position history
- rollback ring
- result DTO
- failure records
- telemetry ring
- drift probe
- mock `DesyncDetectedSignal`
- CSV profiles

These IDs are editor/offline lanes; they are not production ownership routes.

- The editor graph does not own a private persistent `NativeArray`.
- After a run, the runner copies the 300-entry telemetry ring into a dedicated 1 MiB telemetry-only `GlobalDataVault` and disposes the bulk 128 MiB smoke Vault.
- The retained graph reads `NativeArray<KccSmokeTelemetryEntry>.ReadOnly` through the telemetry generation handle.
- Assembly reload and editor quit dispose that retained telemetry Vault.

- The editor window path uses `StartScheduledRun()` instead of the blocking NUnit runner.
- It schedules SDF generation, phantom initialization, the 10,000-frame KCC loop, escape verification, and drift analysis as a chained `JobHandle` graph.
- `HeadlessKccSmokeTesterWindow` polls the final handle from `EditorApplication.update` and calls `Complete()` only after the handle reports `IsCompleted`; editor teardown drains the handle before disposing the Vault.

`ScheduledRun` finalization is single-attempt. If report, telemetry snapshot, or dump IO throws, teardown drains/disposes the Vault without re-entering the failed finalizer.

Report evidence is route-specific.

Synchronous NUnit route writes `UNITY_EDITOR_JOB_RUN_PENDING_EXTERNAL_COMPILE_WALL` and treats managed allocation deltas as smoke failures.

Scheduled editor route writes `UNITY_EDITOR_SCHEDULED_JOB_PENDING_IMPORT_PROOF`; its managed allocation delta is informational, not hot-path GC proof.

Scheduled editor timing is labeled `editor_wall_clock_not_ci_budget`.

It includes scheduling and editor polling time and does not set the 100 us CI failure flag. Synchronous NUnit remains CI budget route after import/build unblock.

## Assembly Route

- Runtime math lives in `HectonKccRuntime_SmokeTest.cs` under the existing `HydrodynamicKccRuntime` owner.
- Human tooling and cold runner live in `Assets/_Project/Scripts/Physics/KCC/Editor/Shinobu355KccSmokeEditorFacade.cs` under `Hecton8.Physics.KCC.Editor`.
- NUnit files are thin callers only; they no longer own `EditorWindow`, `SceneView` gizmo, telemetry cache, or runner implementation.
- `Hecton8.Physics.KCC.Editor.asmdef` allows unsafe code only for the raw black-box `ReadOnlySpan<byte>` dump path and references `Unity.Jobs` for cold editor scheduling.

## DTO Proof

`KccSmokeTestStateDTO` is explicit 32 bytes:

- offset 0: `double3 TestPlayerAUP`, 24 bytes
- offset 24: `uint CurrentFrameCount`, 4 bytes
- offset 28: `uint MismatchFlags`, 4 bytes

Total: 32 bytes, 8-byte alignment, no `Pack=1`, no properties.

## Hardening Notes

- SDF layout is validated with positive dimensions, finite cell size, overflow-safe cell count, and `requiredCount <= sdf.Length`.
- Out-of-volume or invalid SDF samples return `KccSmokeInvalidSdfMeters` and are flagged as `KccSmokeFailureEscape | KccSmokeFailureSdfInvalid`; they are not treated as safe open water.
- Rollback replay now compares AUP, velocity bits, and flags against the current authoritative state, while the mutated replay A/B branch verifies deterministic correction behavior.
- Smoke tuning is sanitized once at job entry. `GlobalQualityWeight` continuously lerps the stress drive from 220 to 620 without changing DTO layout or authority route.
- Resolved `NativeArray` lengths are asserted before scheduling phantom jobs. The runner passes the validated safe phantom count into initialization, simulation, and escape verification jobs.
- CSV profile ingestion rejects integer overflow in the span parser and rejects out-of-range AUP, velocity, and input-bias fields before writing profile DTOs.
- Black-box dumps use `H8KCC355` v1 binary header: version, entry count, entry struct size, oldest frame, newest frame, source hash. Telemetry rows are written oldest-to-newest after rotating the 300-frame circular ring.
- SceneView failure gizmo keeps absolute `double3` AUP in cold state.
- It renders only `AUP - previousAUP` local deltas through `HydrodynamicKccMath.ResolveLocalFloat3`.
- No absolute 100 km coordinate casts directly into debug `Vector3`.

## Verification State

Static source wiring and polish hardening are complete at source level.

Guarded `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false` did not reach SHINOBU_355 diagnostics because construction files fail on missing `Hecton8.Habitat`. Import/Burst/profiler proof pending.
