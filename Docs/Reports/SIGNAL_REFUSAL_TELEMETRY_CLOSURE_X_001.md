# SIGNAL_REFUSAL_TELEMETRY_CLOSURE_X_001

Date: 2026-05-25  
Agent: X_001  
Domain: Echelon 1 Core Infrastructure / Typed SignalBus Corridor  
Status: SOURCE VERIFIED / BUILD BLOCKED BY CPU GUARD

## Scope

This pass closed producer-side refusal visibility for storm-adjacent `SignalBus<T>.TryEnqueueBounded(...)` paths that already had native pre-enqueue budgets but still discarded the returned bool at the call site.

Touched code files: 37.

## What Was Wrong

- Reactor, fluid, hull, submarine, exosuit, KCC, combat, fabrication, equipment, and inventory paths had bounded enqueue protection, but several owners did not surface refusal into their local state, counters, or telemetry.
- Statement-level `TryEnqueueBounded(...)` calls were still present in runtime source. Those calls used the native budget/drop counter inside `SignalBus<T>`, but the domain owner could not see the refused presentation/gameplay fact locally.
- Some physical-domain producers used `void` compatibility wrappers (`RaiseEntanglementStrain`, `QueuePhysicsWakeRequest`, `PhysicsApplySystem.Enqueue`) that made refusal invisible to callers.

## What Was Done

- Reactor and reactor heat injection jobs now mark reactor/base/power state flags when base-compromised, radiation, combat-damage, reactor-damage, or thermal signal emission is refused.
- Habitat fluid ingress, hull integrity, submarine structural/fluid/atmosphere, thermodynamics hazard, habitat fluid director, cavitation, exosuit, submarine dynamics, hydrodynamic KCC, and camera juice paths now record owner-local signal drop counters or flags.
- Base structural warnings, structural collapse, cable physics, ballast buoyancy, submarine dynamics, and vehicle damage jobs now convert bounded writer refusal into state/telemetry flags or dropped-result counters.
- Fabrication completion/tick, modular equipment depleted/overheat, inventory logistics transfer, ballistic combat damage, and combat deflect signals now handle `TryEnqueueBounded` refusal locally.
- Old `SargassumGlobalDragManager` `Raise*` wrappers remain as compile-time-banned compatibility wrappers while first-party callers use explicit `TryRaise*`.

## Deterministic Overflow Path

- Main-thread `TryPush` still rejects before enqueue at configured lane capacity.
- Job-side `TryEnqueueBounded` still claims a per-lane native budget before `NativeQueue<T>.ParallelWriter.Enqueue`.
- When budget is exhausted, no signal payload is enqueued and no managed allocation is performed.
- This pass makes the refusal visible to the owner route through existing unmanaged fields: bit flags, counters, telemetry ring flags, or transaction-result flags.

## Zero-GC / DTO Proof

- No new managed containers were added.
- No new signal DTO field carries `string`, `FixedString*`, `GameObject`, `Transform`, `NativeArray`, `NativeQueue`, `NativeList`, or `NativeHashMap`.
- New data is only `uint`, `byte`, or existing native counter/flag writes.

## Verification

- Runtime old-route scan: `GlobalSignals.Publish/Push/TryDequeue/*Writer`, `SignalBus<T>.Push`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, and `ThreadSafeCommandQueue.Enqueue` outside allowed zones: 0 hits.
- Core signal DTO banned-field scan: 0 hits.
- Runtime statement-level `TryEnqueueBounded(...)` scan outside `Core/Signals`, Editor, Tests, and ModdingAPI: `runtime_statement_tryenqueuebounded=0`.
- Touched-file brace delta scan: 0 deltas.
- Build not launched: latest guard reported `CPU=100 compiler_count=0`, above the `AGENTS.md` 50 percent CPU threshold.

## Microseconds

Runtime savings verified: 0us.  
Reason: no Unity Play Mode, profiler, GCMonitor, Memory Profiler, or player build was run in this pass.

Static expected effect on i3/MX350-class hardware: fewer hidden native queue accepts during reactor/fluid/combat/fabrication/equipment/inventory storms, with deterministic owner-visible drop state and no heap growth.
