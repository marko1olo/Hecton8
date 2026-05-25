# SIGNAL_PRODUCER_EDGE_TRYPUSH_REFUSAL_CLOSURE_X_001

Date: 2026-05-25
Agent: X_001
Domain: Echelon 1 Core Infrastructure / Typed SignalBus Corridor
Status: SOURCE_STATIC_VERIFIED / BUILD_PENDING_GUARD

## Problem

The previous closure added `SignalBus<T>.TryPushTracked(in T, ref int)`, but 245 remaining external runtime statement-level producers still called `SignalBus<T>.TryPush(...)` as a fire-and-forget statement. The generic lane still rejected overflow without heap growth, but the producer owner had no local evidence that presentation or gameplay-adjacent facts were refused during a 5000-signal burst.

That is not acceptable for black-box reconstruction. A base breach, physics impact, fauna panic, PDA/visor/audio feedback, or resource storm must leave an owner-local counter when the lane refuses traffic.

## Work Done

Patched 70 runtime code files in this pass. Converted selected simple one-line producer statements to `SignalBus<T>.TryPushTracked(..., ref ownerDropCounter)` and added owner-local `private static int s_x001...SignalPushDropCount` fields. Field names are file-qualified to avoid duplicate field conflicts across partial classes.

The pass targets:

- Core dispatcher, memory, input, haptics, determinism, job admission, bridge, hardware, and prefab telemetry producers.
- Physics, laser/tool, tether, submarine auto-level, fluid, thermal, impact, haptic, and acoustic producers.
- Atmosphere, toxicity, AI ambient spawn/debris, fauna/world resource, flora, and sargassum producers.
- Narrative, visor, UI, gyro compass, sealed door, interaction, base module, power, reactor, inventory, and survival producers.
- QA/headless/watchdog producers that stress the same signal corridor.

Manual fallout fixed:

- `SystemDispatcher` needed a local counter in the dispatcher class as well as the separate `GlobalRenderContext` class.
- `PhysicsEventBus` needed its own local counter for its top-level helper surface.
- `LaserCutterEvents` needed its own local counter for its top-level helper surface.

## Static Proof

- Code files patched this pass: 70.
- `SignalBus<T>.TryPushTracked(...)` total in project scripts: 346.
- `SignalBus<T>.TryPushTracked(...)` in this pass file set: 169.
- Owner-local `s_x001...SignalPushDropCount` fields in this pass file set: 73.
- Remaining external runtime statement-level direct `SignalBus<T>.TryPush(...)`: 75.
- Runtime legacy hot-route scan for `GlobalSignals.Publish/Push/TryDequeue/*Writer`, `SignalBus<T>.Push`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, and `ThreadSafeCommandQueue.Enqueue`: 0 hits outside allowed zones.
- Owner-counter containment scan: 0 missing local fields for `ref s_x001...SignalPushDropCount`.
- Changed-file brace delta regression scan: 0. `ScannerTool.cs` retains its pre-existing raw brace-count imbalance from `HEAD`; this pass did not change that delta.
- Signal DTO banned-field scan over `Core/Signals` and `Core/Contracts`: 0 `GameObject`, `Transform`, `string`, `FixedString*`, or native-container fields inside `ISignal` structs.
- `git diff --check` on targeted files: no whitespace errors; LF-to-CRLF warnings only.

## Overflow Behavior

`SignalBus<T>.TryPush(...)` remains the authority for lane-level zero-GC overflow:

- Ensures the lane is initialized.
- Rejects when the native queue is missing.
- Sheds non-critical VFX under system stress.
- Rejects at `_expectedCapacity` before native enqueue.
- Sanitizes non-finite payloads before enqueue.
- Increments lane-owned drop/load-shed/corruption counters.

`TryPushTracked(...)` adds only owner-local refusal accounting after `TryPush(...)` returns false. It does not allocate, does not create strings, does not use delegates, and does not modify DTO layout.

For a 5000-signal storm, the lane still caps at the configured unmanaged queue capacity. Excess producers now also increment owner-local integer counters, giving black-box and domain diagnostics producer-edge evidence of refusal.

## Remaining Work

75 external runtime statement-level direct `SignalBus<T>.TryPush(...)` calls remain. The remaining set includes multiline `new Signal { ... }` initializers and selected call sites that need manual conversion to avoid breaking object initializers or bool-handled route semantics.

Build was not launched in this pass because the guard reported CPU 100 percent and active compiler processes.
