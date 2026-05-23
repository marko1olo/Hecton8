# SHINOBU_330 Rationale

Status: STATIC VERIFIED / AIRLOCK_BUOYANCY_DRYZONE_ERADICATED / COMPILE BLOCKED BY ACTIVE DOTNET_CPU_GATE

## Preflight Decisions

Problem: Legacy flooding likely uses Unity physics triggers, managed room traversal, or component-driven water state. Those paths consume PhysX broadphase CPU and cannot scale to 5000-node flooding.
Solution: Start from source archaeology, then implement a flat unmanaged DTO/job layer that can be wired to Vault/CSR routes without direct dependencies on absent systems.
Rejected Alternatives: Keeping trigger volumes as compatibility fallback is rejected because it preserves the broadphase cost this task exists to remove. Direct concrete references to door, pump, buoyancy, or vehicle systems are rejected because 20+ agents are operating in parallel.
Scalability potential: Low uses reduced cadence and depth; Middle uses full CSR cadence; High increases telemetry/sample density; Ultra spends saved CPU on visual shader/detail lanes, not more gameplay truth.
Hardware Impact: i3/MX350 target is removal of managed trigger broadphase and pointer-chasing room graphs; expected savings are architecture-dependent and remain PENDING VERIFICATION until profiler/GCMonitor exists.

Problem: DTO layout in the XML prompt corrects itself from 32 bytes to 64 bytes due to `double3` size.
Solution: Use explicit 64-byte layout, place `double3` first, then scalar fields, then five uint padding fields, and add a validation method using `UnsafeUtility.SizeOf`/`OffsetOf`.
Rejected Alternatives: Sequential layout is rejected for this primary DTO because the task requires exact offsets. `Pack=1` is rejected by ARM64 mandate.
Scalability potential: Same truth layout across all tiers; higher tiers may add presentation-only payloads outside this DTO.
Hardware Impact: 64-byte cache-line DTO avoids unaligned 64-bit reads on ARM64 and reduces L1 miss penalties on low silicon.

Problem: Fluid flow must obey AUP precision and deterministic rollback constraints.
Solution: Subtract compartment `double3.y` values in double precision, cast only the delta to float, use deterministic Burst mode where available, and quantize transferred mass into integer milliliter units.
Rejected Alternatives: Float absolute world positions and pure float volume drift are rejected because 100 km map boundaries and closed loops amplify errors.
Scalability potential: Cadence and processing window scale with `GlobalQualityWeight`; authority route and DTO layout do not change across tiers.
Hardware Impact: Quantized integer deltas trade small ALU cost for deterministic conservation and fewer postmortem defects on low-end hardware.

## Implementation Decisions - 2026-05-22

Problem: `BaseModule` retained a managed `Dictionary<ulong, BuoyancyObject>` dry-zone authority path. Even after trigger callbacks stopped registering new water objects, state-resync methods still called `EnterDryZone`/`ExitDryZone` from flood state.
Solution: Delete the dictionary/list storage and make legacy tracked-object sync/release paths no-op diagnostics. Keep the interior trigger only for player life-support occupancy.
Rejected Alternatives: Leaving the dictionary as dormant compatibility is rejected because task 01 requires deletion of object-oriented water authority, not merely disabling new registrations.
Scalability potential: Low, middle, high, and ultra all use the same scalar flood truth. Presentation scales through shader buffers instead of component count.
Hardware Impact: Removes managed dictionary iteration and PhysX dry-zone side effects from module flood transitions; i3/MX350 gain is expected in breach/flood scenes, exact microseconds pending profiler.

Problem: Room-to-room water needed door/bulkhead sealing without collider checks.
Solution: Add Vault lane `ShinobuFluidEdgeConductivity` (`73330`) and multiply transfer by scalar conductance. Sealed edges resolve to zero.
Rejected Alternatives: Trigger/Collider blockers and direct door component lookup from solver are rejected. They would poll scene state and violate owner-route boundaries.
Scalability potential: Low quality uses smaller BFS budget and cadence; middle/high raise visit count and iterations; ultra spends saved CPU on visual waterline richness, not alternate truth.
Hardware Impact: Replaces broadphase/object dispatch with contiguous CSR reads; target is sub-0.1 ms for 5000 nodes pending runtime profiling.

Problem: Closed-loop float diffusion can create or destroy volume after long sessions.
Solution: Quantize each transfer to signed milliliters and retain fractional residue in `ShinobuFluidTransferRemainders` (`73331`).
Rejected Alternatives: Raw float deltas are rejected due rollback drift and long-session conservation failure.
Scalability potential: Same exact units across all tiers. Quality changes cadence/budget, not volume identity.
Hardware Impact: Adds one scalar remainder read/write per traversed edge; cheaper than post-fact correction or desync recovery on low-end silicon.

Problem: XML task 09 requested direct `AddedMassProfileDTO` mutation, but vehicle hydrodynamics already owns AddedMass calculation.
Solution: Route mass through existing `SubmarineFloodStateSignal` and `PhysicsEventPayload`; `SubmarineDynamicsRuntime` consumes the mass into `SubmarineMassProperties`, and Agent 251's AddedMass tensor job applies the physical roll/heel response.
Rejected Alternatives: Directly writing `AddedMassProfileDTO` from HFI is rejected as cross-domain mutation. Writing `Rigidbody.mass`, `centerOfMass`, or `inertiaTensor` from flood code is rejected outright.
Scalability potential: Authority route is stable across low/middle/high/ultra. Presentation/telemetry cadence can scale independently.
Hardware Impact: Avoids Rigidbody broadphase/inertia mutation churn; exact saved microseconds pending profiler, but removes a known main-thread physics synchronization point from flood updates.

Problem: Existing `HabitatIntegrityManager` owned local flood accumulation and `BaseModule.ApplyFloodExposure`.
Solution: Convert it to an ingress producer by publishing `FluidIncursionSignal` with AUP, compartment id, flood scalar, and pressure-derived flow. The HFI director consumes the signal into Vault compartments.
Rejected Alternatives: Maintaining local `_floodLevel` as truth is rejected because it creates shadow state. Direct HFI method calls are rejected due compile-wall coupling.
Scalability potential: Signal cadence and HFI solver cadence scale continuously; breach authority stays structural/gameplay side.
Hardware Impact: Removes per-module flood-rate stepping as water truth; cost shifts to bounded SignalBus and data-local solver.

Problem: Static proof was needed for OOP water trigger eradication.
Solution: Add `Tools/OOP_Water_Trigger_Scanner_SHINOBU_330.py`, dedicated JSON report, and shared physics report entry. The scanner scopes task-mandated Habitat/Vehicles plus HFI/BaseModule/SubmarineFluidDynamics.
Rejected Alternatives: A prose claim in status/log is rejected; it is not machine-checkable.
Scalability potential: Tooling cost is offline only; runtime tiers unaffected.
Hardware Impact: No runtime cost. It prevents regression into high-cost PhysX trigger water paths.

Problem: `BaseAirlock` and `BuoyancyObject` still preserved a dry-zone component path after BaseModule stopped owning compartment water triggers. That left a callable managed ref-count separate from the CSR/Vault flood truth.
Solution: Remove `BaseAirlock` `BuoyancyObject` lookup/cache/call sites and delete `BuoyancyObject` dry-zone ref-count plus `EnterDryZone`/`ExitDryZone`. Keep only a false `IsInDryZone` compatibility read so unrelated consumers compile until they migrate to `PlayerBaseEnterSignal`/`PlayerBaseExitSignal`.
Rejected Alternatives: Leaving the methods dormant is rejected because public callable dry-zone authority would invite regression. Deleting the compatibility read in this pass is rejected because player/audio/weather consumers are outside SHINOBU_330 ownership and need a separate signal-consumer migration.
Scalability potential: Low through ultra tiers now share one compartment flood route. Dry interior presentation/control should be driven by base-transition signals, not per-object buoyancy suppression.
Hardware Impact: Removes per-object dry-zone ref-count writes and `TryGetComponent<BuoyancyObject>` from airlock transitions; exact microseconds pending profiler, but managed component coupling is gone from compartment water flow.
