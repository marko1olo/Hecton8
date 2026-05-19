# Route_SHINOBU_145_Metabolism

Date: 2026-05-19
Status: PENDING VERIFICATION

Route ID: SHINOBU_145_METABOLISM_NATIVE_STATE
Owner: SHINOBU_145
Owner domain: ECHELON 5 COMBAT & SURVIVAL PHYSIOLOGY / DIET & METABOLISM
Owning file/system: `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`

Problem: Authoritative metabolism state must be job-visible, replayable, crash-dumpable, and shared with other E5/E1 consumers without managed collections.
Why owner-local data is insufficient: AI/creature spawners, thermal grids, combat routing, and UI/debug tools need a stable native state route and not per-object state.
Why direct caller/owner interface is insufficient: Burst jobs need contiguous native buffers and signal writers, not MonoBehaviour references.

Instrument:
  [ ] GlobalRegistry cold service/interface
  [x] SignalBus<T> first-party broadcast
  [ ] GlobalSignals bridge/direct queue
  [ ] HectonEventBus mod/API/cold event
  [x] GlobalDataVault / IDataVault
  [x] Black-box/telemetry route

Producer phase: SlowTick schedules Burst jobs; late-frame swap completes and publishes presentation scalar.
Consumer phase: Combat/physiology signal drains after simulation; editor/UI reads telemetry outside hot simulation.
Cadence: Continuous `math.lerp(0.5f, 3.0f, 1.0f - GlobalQualityWeight)`.
Expected max events/reads per frame: 5000 entity reads per scheduled SlowTick; worst-case starvation/dehydration signals capped by SignalBus capacity; one telemetry entry per completed tick.
GlobalQualityWeight behavior: No entity drops. Cadence stretches and dynamic `dt` preserves integrated totals.

Payload/data shape: Explicit unmanaged DTOs. `MetabolicStateDTO` is 32 bytes with raw fields only.
Managed fields present: no
UnityEngine.Object fields present: no
Layout proof: editor validator checks `UnsafeUtility.SizeOf` and field offsets.
Capacity: 5000 default entities, 300 telemetry entries, 128 species rules.
Overflow/failure mode: SignalBus overflow follows existing lane policy; telemetry ring overwrites oldest; NaN sets telemetry flags and dumps binary ring.

Telemetry fields: frame, entity count, average core temperature, starvation count, dehydration count, toxicity count, job microseconds, flags.
Black-box fields: frame, aggregate state, event counts, quality, dt, NaN flags.
Profiler marker: `ShinobuMetabolism.Schedule`, `ShinobuMetabolism.Complete`.
GC proof required: Profiler/GCMonitor proof before GREEN. Static hot path contains no managed allocations by design.

Shutdown/disposal rule: Vault handles are released by owner teardown; active jobs are disposed through deferred NativeArray handle rules when local allocations exist.
Scene unload behavior: Runtime unregisters from tick dispatcher and does not spawn objects in teardown.
Stale-handle behavior: Vault handles are resolved per schedule/complete boundary and fail closed if invalid.

Rejected alternatives:
  [x] owner-local field
  [x] cached owner interface
  [x] existing SignalBus lane
  [ ] existing Vault buffer
  [x] cold HectonEventBus hook
  [ ] no global route needed

Why this does not increase global monolith risk: It reuses existing physiology/combat signal lanes and adds only narrow Vault buffers owned by one E5 runtime.
H-Phi impact expected: None claimed.
Runtime proof required before acceptance: Unity import, compile, Play Mode SlowTick run, profiler/GC 0 B hot path, telemetry dump test.
Reviewer: Integrator
Status: PROPOSED

Review disposition: YELLOW
Reason: Static route card is complete enough to implement. Runtime/profiler proof is not attached, so GREEN is forbidden.
