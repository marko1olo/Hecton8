# RUNTIME_OWNER_06_THERMAL_DRS_COROUTINE_REPAIR_PACKET

Status: `SOURCE PATCHED / PENDING COMPILE AND UNITY PROOF`
Evidence class: `STATIC_SOURCE_PATCH + EDITOR_LOG_TAIL_READ`
Owner: future graphics scalability/runtime owner

Packet authoring did not mutate Unity assets. The controller later patched the target C# file under a clean process gate; Unity then started its own import pass. No Play Mode, player build, profiler, scene save, prefab save, material save, or project-setting edit was performed.

## Objective

Remove the runtime coroutine repair path from `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs` without weakening dispatcher registration recovery, dynamic-resolution ownership, or continuous `GlobalQualityWeight` behavior.

First-20 route blocker removed: runtime forbidden coroutine contamination in a visual/performance governor that can affect surface, sky, ocean, HUD, and route readability proof.

## Evidence Basis

- `Docs/Reports/RuntimeSystem_20260605/FORBIDDEN_RUNTIME_API_STATIC_SCAN_20260605.md/.csv`
- `Docs/Reports/RuntimeSystem_20260605/FORBIDDEN_RUNTIME_API_ROUTE_TRIAGE_20260605.md/.csv`
- `Docs/Reports/RuntimeSystem_20260605/THERMAL_DRS_STATIC_DEFECT_ANCHORS_20260605.md/.csv`
- `taskslocal/runtime_system_20260605/RUNTIME_OWNER_07_THERMAL_DRS_BLACKBOX_DUMP_ROUTE_PACKET.md`
- `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs`

Original static source facts:

- Class implements `ILateFrameTickable`, `ISlowTickable`, `IGlobalRegistryHotSwapListener`, `IGlobalRegistryHotSwapRefListener`, and `IResolutionScalerService`.
- `RequestDispatcherPhaseRegistrationRepair()` calls `StartCoroutine(RepairDispatcherPhaseRegistrationCold())` at line `1560`.
- `RepairDispatcherPhaseRegistrationCold()` yields `null` for up to `DispatcherRegistrationRepairMaxFrames`.
- `OnDisable()` calls `StopAllCoroutines()`.
- Dispatcher rebind/replacement callbacks, scene-loaded repair, `OnEnable`, and `Start` already call `TryRegister()` and `RequestDispatcherPhaseRegistrationRepair()`.
- Same file also has a DRS black-box dump route defect tracked by packet 07: fixed historical `Dump_13KRA.bin` and the no-file-write black-box path must not survive the compile/profiler pass.

Current patched source facts:

- `StartCoroutine`, `StopAllCoroutines`, `IEnumerator`, and `Dump_13KRA` scan count in `ThermalDynamicResolutionAdapter.cs`: zero.
- `_dispatcherRegistrationRepairRunning` now has bounded `_dispatcherRegistrationRepairFramesRemaining` state.
- `RequestDispatcherPhaseRegistrationRepair()` sets bounded repair state and runs one immediate registration pass.
- `AdvanceDispatcherRegistrationRepair()` is pumped from `LateFrameTick()` and `SlowTick()`, with exhaustion and success clearing the repair state.
- Packet 07 was executed in the same source edit: the DRS dump route now uses the owner/system filename prefix `Dump_THERMAL_DRS_`.
- `git diff --check` on the touched C# file returned only a CRLF normalization warning.
- Editor.log tail scan after Unity import showed no `error CS`, no `Compilation failed`, and no `ThermalDynamicResolutionAdapter` compile error, but this is log-tail evidence only, not full compile proof.

## Authorities And Mandates

Read before execution:

- `AGENTS.md`
- `systems.md`
- `performance.md`
- `rendering.md`
- `quality.md`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Analysis Gate For Future Owner

[ANALYSIS]
Target: replace runtime coroutine dispatcher repair with zero-GC owner-phase retry.
Affected systems: dynamic resolution, URP render scale, `GlobalQualityWeight`, dispatcher registration, hot-swap listener, scene-loaded repair, shader globals, DRS telemetry ring.
Zero GC proof: no `StartCoroutine`, no `IEnumerator`, no `yield`, no `StopAllCoroutines`, no new managed allocation in late-frame/slow-tick paths. Required proof is fresh compile plus Play Mode profiler/GCMonitor for DRS owner under registration churn.
State check: no duplicate adapter owner, no lost late-frame/slow-tick registration, no stuck `_lateFrameRegistrationRequested`, no hot `GlobalRegistry` polling loop, no render-scale commit starvation after dispatcher replacement.
Rule quote: runtime systems use dispatcher phases, not MonoBehaviour coroutine schedulers. `GlobalQualityWeight` remains continuous and presentation-only.

## Required Repair Shape

1. Do not edit while CPU is over 50 percent or Unity/import/compiler/shader/package/dotnet/csc work is active.
2. Coordinate with `RUNTIME_OWNER_07_THERMAL_DRS_BLACKBOX_DUMP_ROUTE_PACKET.md` before source mutation. Both defects touch `ThermalDynamicResolutionAdapter.cs`; prefer one edit, one compile, one Play Mode/profiler pass.
3. Remove `StartCoroutine(RepairDispatcherPhaseRegistrationCold())`.
4. Remove `RepairDispatcherPhaseRegistrationCold()` and its `IEnumerator` return type.
5. Remove `StopAllCoroutines()` from `OnDisable()`.
6. Replace `_dispatcherRegistrationRepairRunning` with explicit retry state, for example:
   - `_dispatcherRegistrationRepairRequested`
   - `_dispatcherRegistrationRepairFramesRemaining`
7. `RequestDispatcherPhaseRegistrationRepair()` must:
   - return when not playing;
   - call `TryRegisterHotSwap()` and `TryRegister()` once;
   - set retry state only while `_registeredLateFrame` or `_registeredSlowTick` is still false;
   - never allocate and never start a coroutine.
8. Add a private no-alloc pump method called from owned/cold entry points only:
   - `LateFrameTick()`
   - `SlowTick()`
   - `OnGlobalRegistryServiceRebound(...)`
   - `OnGlobalRegistryServiceReplaced(...)`
   - `OnSceneLoadedRepairCold(...)`
   - `OnEnable()`
   - `Start()`
9. The pump must try one registration pass per call, decrement a bounded retry counter, and clear request state after success or exhaustion.
10. If retry exhausts, write deterministic telemetry/state flag through the existing DRS telemetry path. Do not spam `Debug.Log`.
11. Keep all `GlobalRegistry` access cold or in existing hot-swap/cold repair callbacks. Do not add hot polling.
12. If packet 07 is executed in the same pass, replace the stale dump filename with a deterministic owner/system route, implement an actual bounded binary artifact write, and preserve the 300-record ring plus fixed 64-byte telemetry rows.

## Rejection Gates

- Any remaining `StartCoroutine`, `IEnumerator` coroutine repair, `yield return`, or `StopAllCoroutines` in `ThermalDynamicResolutionAdapter`.
- Same-frame schedule/complete or hidden managed allocation added to DRS paths.
- Hot `GlobalRegistry.Get<T>()`, scene search, `Find*`, or string logging added.
- `GlobalQualityWeight` converted to a binary quality branch.
- Render-scale commit, shader globals, or telemetry starves after dispatcher replacement.
- Compile not proven clean after code edit.
- No profiler/GC proof after code edit.

## Proof Packet Requirements

Write proof outside `Assets`:

- process gate snapshot;
- source diff summary;
- compile result;
- static scan proving no coroutine symbols remain in `ThermalDynamicResolutionAdapter`;
- static scan proving `Dump_13KRA.bin` does not remain and a deterministic binary file write route exists if packet 07 is executed in the same pass;
- Play Mode DRS registration churn test;
- profiler/GCMonitor result;
- DRS telemetry ring status;
- compact/high screenshots only if visual scale changed.

## Low / Middle / High / Ultra Consequences

Low/compact:

- DRS repair remains deterministic and no-alloc. Render scale may reduce smoothly, but surface/ocean/sky readability cannot collapse into flat color or muddy darkness.

Middle:

- Expected lane keeps stable late-frame visual sync, shader globals, and HUD/route readability under normal frame pressure.

High:

- Saved coroutine overhead is not the goal. Budget must buy richer visual response only after compact proof passes.

Ultra:

- Visual overkill can increase material/detail response through existing continuous weights, not by changing DRS authority or DTO layout.

## Regression Model

- CPU: expected no coroutine scheduler work; must be measured.
- GC: expected removal of coroutine allocation path; must prove `0 B/frame` under registration churn.
- Memory: no new persistent managed collections or unbounded native state.
- Cadence: retry moves to owned callbacks/dispatcher phases; no hidden MonoBehaviour scheduler.
- Correctness: DRS registration, render-scale commits, shader globals, and telemetry must still recover after dispatcher replacement.

Final status: `SOURCE PATCHED / PENDING FULL COMPILE, UNITY CONSOLE, PLAY MODE, GC, AND PROFILER PROOF`.
