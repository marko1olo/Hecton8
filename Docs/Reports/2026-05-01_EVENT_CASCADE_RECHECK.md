# Event Cascade Recheck

Date: `2026-05-01`
Status: `PENDING VERIFICATION`
Scope: static source recheck of event cascade/depth guard claims

## Mandates Followed

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Verification Boundary

This is source evidence only. No Play Mode event-loop stress test was run.
No GCMonitor, profiler, or runtime telemetry capture was collected.

MCP was not used. Local `Editor.log` remained clean after the prior console-stabilization pass, but this report is not a runtime certification.

## Corrected Finding

The older same-day audit claim that `HectonEventBus` tracks dispatch depth but has no max-depth cap is stale.

Current source evidence:

- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:136` defines `MaxDispatchDepth = 4`.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:321` enters `TryEnterDispatch()`.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:323` rejects dispatch when `_dispatchDepth >= MaxDispatchDepth`.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:332` reports cascade telemetry before dropping the payload.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:340` routes the warning through `CrashTelemetryBuffer.ReportEventCascadeWarning()`.
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:462`, `:616`, and `:778` call the global dispatch-depth guard before unmanaged, native-byte, and managed event dispatch.

Dispatcher-side source evidence:

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:32` defines `MaxLateFrameEventsPerFrame = 1000`.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:470` starts the late-frame event budget.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:501` exposes `TryConsumeLateFrameEventDispatch()`.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:529` reports circuit-breaker trips through `CrashTelemetryBuffer.ReportEventCascadeWarning()`.

## Remaining Risk

The hard depth cap is present. The remaining event risk is not "unbounded recursion with no cap."

The remaining risk is same-frame generation processing in NativeQueue-backed lanes:

- `InteractionEvents.FlushPending()` drains while `_pendingEvents` is non-empty.
- `CraftingEvents.FlushPending()` uses the same active-queue drain model.
- If a listener publishes another event into the same lane during dispatch, the new payload can be consumed in the same `LateUpdate` until the global budget trips.

The upper bound is currently:

```text
MaxLateFrameEventsPerFrame * handler_cost
```

With the current source value:

```text
1000 * handler_cost
```

That is bounded, but still capable of burning a full late-frame budget every frame if a logic cycle keeps producing work.

## Required Future Fix

Do not add another depth cap to `HectonEventBus`; one already exists.

The next correct fix is a generation split for NativeQueue-backed lanes:

- current generation drains from a front queue
- publishes during listener dispatch write into a back queue
- front/back swap happens once per dispatcher late-frame phase
- payloads created by handlers are processed next frame unless a lane explicitly opts into same-frame reentrancy

This is a behavior change. It needs a Play Mode test because some systems may currently rely on same-frame event propagation.

## Regression Model

CPU: current source is bounded by the dispatcher budget and mod bus depth cap. Generation split would reduce same-frame spikes but can add one-frame latency.

GC: no runtime code changed in this recheck. A future generation split must use pre-created NativeQueues or fixed native buffers.

Memory: no runtime memory changed in this recheck. A future double-buffered lane costs one additional queue/buffer per event lane.

Cadence: current cadence permits same-frame propagation until budget exhaustion. Generation split would make event cadence more deterministic.

Correctness: static finding corrected. Runtime behavior remains unverified.

## Status Change

- `HectonEventBus` max-depth cap: SOURCE-PRESENT.
- `SystemDispatcher` late-frame budget breaker: SOURCE-PRESENT.
- NativeQueue generation split: NOT PRESENT / PENDING DESIGN.
- Runtime stress proof: ABSENT.

STATUS: PENDING VERIFICATION
