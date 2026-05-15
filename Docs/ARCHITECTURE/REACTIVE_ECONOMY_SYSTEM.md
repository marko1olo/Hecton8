# Reactive Economy System
Date: 2026-05-07

Status: PENDING VERIFICATION
Verification: PENDING VERIFICATION

## 2026-05-11 Current-State Override

- Current data boundary: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Current manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current visual-realistic-fake doctrine: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`.
- May 14 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat that May 11 compile-success line as stale report text. R43 rechecked the current external root `Hecton8*.csproj` no-restore CLI compile surface at `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; full restore graphs still carry vendor/package warnings, and shared `Temp\obj` locks can create transient evidence noise. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical 2026-05-04 Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this economy map as current runtime truth.
- This document is an economy/fabrication/system contract, not proof that scarcity, pressure degradation, deconstruction, or thermodynamics are runtime-verified.
- Re-open inventory, fabrication, power, weather, and physics owners before surgery.

## Scope

This document covers the SOA inventory chemistry layer, market scarcity inflation, pressure degradation, deconstruction yield resolution, item-physics hooks, and Fabricator thermodynamics.

## Mandates Followed

- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `CORE_Weather_Abyssal_FlowField_Currents.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`

## Slot Adjacency Chemistry

`PlayerInventory` stores mutable per-anchor flags in `_itemStateFlags`. `ItemCatalog.BuildStateFlags` writes the shared `ItemRuntimeStateFlags` bits for radioactive, biological, degraded, rusted, crafting-locked, and flammable items.

Every `SlowTick`, `InventoryReactiveChemistryJob` scans the fixed SOA anchor range:

1. Skip empty anchors, zero stacks, and craft-locked anchors.
2. Check only the four orthogonal neighbors of each slot.
3. A reaction is valid when one side has `IS_RADIOACTIVE` and the other has `IS_FLAMMABLE`.
4. Valid adjacency increments `_thermalRunawayByAnchor[anchor]` by `ThermalRunawayPerSecond * SlowTickIntervalSeconds`.
5. Broken adjacency cools cached heat by `ThermalRunawayCooldownPerSecond * SlowTickIntervalSeconds`.
6. At `thermal >= 1.0`, the job writes the anchor pair to `_thermalRunawayPairs`.
7. Main thread consumes the fixed pair buffer, destroys both anchors, applies 50 suit damage per pair, and queues a muffled delayed DSP event through `SpatialAudioManager`.

The adjacency kernel uses only fixed native arrays, integer slot math, and scalar flags. No lists, LINQ, strings, managed delegates, or managed allocations execute inside the job.

## Market Scarcity Inflation

`Fabricator.GetAdjustedIngredientAmount` passes accessible item count into `ResourceScarcityDirector.ResolveInflatedIngredientAmount`.

For Titanium (`Data_TitaniumScrap`), accessible count above 500 units forces a `4.0x` ingredient multiplier. This is the requested `+300%` increase: final cost equals base cost plus 300%.

`HectonFabricatorUI.ApplyInflationLabel` formats the multiplier via `CharBufferPool.TryAcquire`, `float.TryFormat`, and `TMP_Text.SetCharArray`. If the shared pool is exhausted, it falls back to the fabricator UI's fixed private char buffer. The inflated label uses the configured red `inflationColor`.

## Pressure Crush

`PlayerInventory` applies pressure-crush degradation below 2000 m. Fragile items are:

- `AudioMaterialID == Glass`
- `ResourceFamily == ElectronicsMetal`
- `ResourceFamily == Power`

Durability damage is continuous per `SlowTick` and marks items degraded below the shared degraded threshold. At zero quality, the anchor is destroyed.

`PressurizedContainer` is the runtime protection bridge. Active modules call `PlayerInventory.AddPressurizedContainerProtection` on enable and release it on disable. `PlayerInventory.ResolveInventoryPressurizedContainerProtection` then makes pressure-crush degradation a zero-alloc count check instead of a scene search.

## Deconstruction Yield

`CraftingSystem.TryBuildDeconstructionYieldBuffer` flattens crafted subcomponents before the Burst yield job:

- Bounded recursion cap: 64 recipe nodes.
- Fixed buffers: `_craftRecipeCosts`, `_deconstructionFlattenedCosts`, `_deconstructionRecipeOutputs`.
- Clean output: 80% reclaim.
- Degraded output: 30% reclaim.
- Quality below 20%: reclaim becomes scrap metal. The resolver prefers `Data_ScrapMetal`; if absent, it falls back to `Data_TitaniumScrap`.

This is a bounded reverse-topological expansion over recipe result hashes. Cycles or over-cap graphs collapse to the current item as raw cost instead of allocating or recursing unbounded.

## Physics Hooks

`HectonFluidEngine.BuoyancyJob` adds gyroscopic flow torque from the existing local current vector:

```text
torque = cross(up, flowAxis) * currentSpeed * sqrt(volume) * lightTumbleBias * massStabilizer * submersion * currentResponse
```

Heavy mass reduces tumble through `1 / max(1, mass)`. Light, high-volume items tumble harder.

Direct `Rigidbody.AddRelativeTorque` in gameplay code is rejected by the project physics mandate. The implementation routes equivalent torque through the existing deterministic force/torque application path owned by `PhysicsApplySystem`.

`GravityTetherTool` uses `Physics.SphereCastNonAlloc` with a fixed hit buffer and routes all pull impulses through `PhysicsForceRouter.QueueForce(..., ForceMode.VelocityChange)`. Close hits call `IInventoryPickupSource.TryHandleInventoryPickup`, keeping pickup routing on the existing zero-GC contract.

## Crafting Thermodynamics

`Fabricator` now has a configured `craftTemperatureDeltaCelsius` heat pulse and optional `thermalHostModule` reference.

On craft completion:

1. The fabricator resolves the heat delta before resetting `_activeCraftPowerMultiplier`.
2. It consumes craft power.
3. It calls `ApplyCraftingThermodynamics`.
4. The local `BaseModule` calls `TryInjectHostRoomTemperatureDeltaCelsius`.
5. `SubmarineAtmosphereSystem.InjectRoomTemperatureDeltaCelsius` applies the room temperature delta.

This keeps room ownership inside `BaseModule`/`SubmarineAtmosphereSystem`. The Fabricator does not mutate atmosphere SOA arrays directly.

## Encumbrance Enforcement

`HectonPlayerMovement` keeps an unsaturated `TotalMassKg / CarryCapacity` ratio. Existing `InventoryLoad01` remains saturated for UI and movement multipliers.

At `ratio >= 1.5`, the player is critically encumbered:

- Swim upward vertical input is zeroed.
- Exosuit jump jets are rejected.
- Gravity and existing buoyancy/drag remain active, so the player can sink.

## Regression Model

- CPU: adjacency and pressure work scale O(N) over fixed SOA anchors on `SlowTick`; deconstruction recursion is capped at 64 nodes.
- GC: hot paths use native/fixed buffers and `SetCharArray`; runtime GC proof still requires GCMonitor capture.
- Memory: new persistent buffers are bounded and owned by existing systems; no unbounded cache was introduced.
- Cadence: chemistry, pressure crush, and protection checks execute on `SlowTick`; craft heat executes once per craft completion.
- Correctness: pair destruction is main-thread after job output; pressure immunity is count-based; scarcity cost inflation uses accessible Titanium count.

## Failure Modes

- Missing `PressurizedContainer` means pressure crush is active below 2000 m.
- Missing local `BaseModule` means Fabricator heat is ignored instead of searching globally.
- Recipe cycles or recursion cap overflow collapse deconstruction to the current item as a raw cost.
- Exhausted `CharBufferPool` falls back to the UI-owned fixed char buffer.
