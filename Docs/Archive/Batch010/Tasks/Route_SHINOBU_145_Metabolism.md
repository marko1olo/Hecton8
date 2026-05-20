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
  [x] GlobalRegistry cold service/interface
  [x] SignalBus<T> first-party broadcast
  [ ] GlobalSignals bridge/direct queue
  [ ] HectonEventBus mod/API/cold event
  [x] GlobalDataVault / IDataVault
  [x] Black-box/telemetry route

Producer phase: SlowTick schedules Burst jobs; late-frame swap completes, publishes staged signals, and publishes presentation scalar. Cold boot initializes every resolved Vault row and staged signal slot through `InitInactiveMetabolismJob` before any mock rows are hydrated.
Consumer phase: Combat/physiology signal drains after simulation; editor/UI reads telemetry outside hot simulation.
Cadence: Continuous `math.lerp(0.5f, 3.0f, 1.0f - GlobalQualityWeight)`.
Expected max events/reads per frame: 5000 entity reads per scheduled SlowTick; worst-case starvation/dehydration/hypothermia signals stage into three fixed slots per row and are published after job completion through SignalBus `TryPush`; one toxic combat slot per row; one telemetry entry per completed tick.
GlobalQualityWeight behavior: No entity drops. Cadence stretches and dynamic `dt` preserves integrated totals.
External readback: chemical toxin samples consume SHINOBU_138 published Vault buffers `71152` published `float4` grid, `71161` tuning, `71162` telemetry ring, and `71163` telemetry cursor. Overlay buffer `71153` is sampled only when it can be locked and resolved; missing overlay does not disable published-grid toxin sampling. These are readback buffers only; SHINOBU_145 does not own or mutate chemical truth.

Payload/data shape: Explicit unmanaged DTOs. `MetabolicStateDTO` is 32 bytes with raw fields only. Staged signal buffers reuse existing 64-byte Core `PhysiologyStateSignal` and `CombatDamageSignal` DTOs.
Managed fields present: no
UnityEngine.Object fields present: no
Layout proof: editor validator checks `UnsafeUtility.SizeOf` and field offsets. Chemical mirror DTOs are explicit 64-byte readback mirrors of SHINOBU_138 tuning/telemetry layout.
Import hygiene: stable `.meta` GUIDs exist for all new SHINOBU_145 C# assets.
Capacity: 5000 default mock entities, configurable Vault capacity, 300 telemetry entries, 128 species rules, three staged physiology signal slots per entity, and one staged combat damage slot per entity. Rows with `EntityHashID=0` are inactive and skipped by the hot integrator/telemetry.
Overflow/failure mode: SignalBus overflow follows existing lane policy during post-completion `TryPush`; staged signal slots are overwritten/cleared by the next scheduled integrator pass; telemetry ring overwrites oldest; NaN sets telemetry flags and dumps binary ring.

Telemetry fields: frame, entity count, average core temperature, starvation count, dehydration count, toxicity count, job microseconds, flags.
Black-box fields: frame, aggregate state, event counts, quality, dt, NaN flags.
Profiler marker: `ShinobuMetabolism.Schedule`, `ShinobuMetabolism.Complete`.
GC proof required: Profiler/GCMonitor proof before GREEN. Static hot path contains no managed allocations by design; remaining `new` source hits are static/cold IO/cold graphics buffer/dump paths.

Shutdown/disposal rule: Vault handles are released by owner teardown; active jobs are reclaimed through Core `DispatcherJobFence.TryComplete`, with non-forced runtime completion only after `IsCompleted` and forced completion limited to teardown/editor/cold bootstrap.
Scene unload behavior: Runtime unregisters from tick dispatcher and does not spawn objects in teardown.
Stale-handle behavior: Vault handles are resolved per schedule/complete boundary and fail closed if invalid.

Rejected alternatives:
  [x] owner-local field
  [x] cached owner interface
  [x] existing SignalBus lane
  [x] existing Vault buffer
  [x] cold HectonEventBus hook
  [ ] no global route needed

Why this does not increase global monolith risk: It reuses existing physiology/combat signal lanes, stages job outputs in owner-local Vault buffers before publishing, queries thermodynamics only through `IThermodynamicsService`, and samples chemical toxin only through SHINOBU_138's documented Vault readback buffers. No Core enum, signal layout, new chemical service, or direct `ChemicalInfluenceGrid` reference is added.
H-Phi impact expected: None claimed.
Runtime proof required before acceptance: Unity import, compile, Play Mode SlowTick run, profiler/GC 0 B hot path, telemetry dump test.
Reviewer: Integrator
Status: PROPOSED

Review disposition: YELLOW
Reason: Static route card, inactive-slot vaccination, chemical readback, dispatcher-fence routing, optional overlay fallback, and import hygiene are complete enough to continue verification. Runtime/profiler proof is not attached, so GREEN is forbidden.
