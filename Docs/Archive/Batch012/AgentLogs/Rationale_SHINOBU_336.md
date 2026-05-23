# Rationale_SHINOBU_336

Agent: SHINOBU_336
Domain: ECHELON 6 HABITAT & VEHICLES / MODULE DECONSTRUCTION
Status: STATIC VERIFIED / BUILD BLOCKED BY CPU+ACTIVE CSC POLICY

## Decision 0 - Mandate Selection

Problem: Module deconstruction crosses inventory refund, logistics graph isolation, fluid/room connection invalidation, native job memory, signal lanes, and crash telemetry.
Solution: Read the eight closest registry mandates before code: SOA inventory, CSR logistics graph, fluid interior, ARM64 layout, zero-GC, native jobs, blackbox telemetry, typed signal lanes.
Rejected Alternatives: Reading only AGENTS.md would miss the inventory/CSR-specific edge cases; reading every mandate would waste context and increase neighboring-domain contamination risk.
Scalability potential: Low uses bounded teardown queue and no physics objects. Middle uses the same kernel with larger per-frame budget. High/Ultra can spend saved cycles on visual proxy richness, not gameplay truth expansion.
Hardware Impact: Expected low-end i3/MX350 gain is removal of Unity Destroy hierarchy teardown and managed recipe traversal from the deconstruction path; measurement pending.

## Decision 1 - Authority Integration

Problem: Deconstruction already has an authority route in `ConstructionManager`; inventing a separate runtime would split ownership and race other agents.
Solution: Keep `ConstructionManager` as request owner and `HabitatGraphManager` as topology owner; add only `HabitatDeconstructionTransactionKernel` for unmanaged math and use existing `IHabitatDeconstructionSystem` registration.
Rejected Alternatives: A standalone `ModuleDeconstructionRuntime` would duplicate the registry, poll modules, and create direct dependencies on systems that are actively changing.
Scalability potential: Low devices execute one bounded owner path. Middle/high/ultra increase admitted transactions per frame without creating more owner routes.
Hardware Impact: i3/MX350 benefits from one cache-local transaction path and no scene-wide ownership search during teardown.

## Decision 2 - Destroy Removal

Problem: The legacy retirement helper called `Destroy(module)` for runtime proxy or pool-missing cases, violating teardown safety and causing hierarchy churn.
Solution: Replace the helper with `RetireModuleInstanceWithoutDestroy`: use pool despawn only when `CanDespawnWithoutDestroy` passes; otherwise detach and deactivate without destroying.
Rejected Alternatives: Keeping `Destroy()` for proxy fallback was standard Unity cleanup but invalid for this route; forcing all modules through pool without the safety predicate could still hit pool-side destroy fallback.
Scalability potential: Low devices avoid expensive destruction spikes. Middle/high/ultra keep saved CPU for richer deconstruction VFX through existing signal lanes.
Hardware Impact: Estimated i3/MX350 gain is spike avoidance; no exact profiler sample is claimed without runtime import.

## Decision 3 - Refund Cost Source

Problem: `BuildableData.buildCost` is a managed authoring list, but the Data Monolith binary is absent in this workspace.
Solution: Prefer `BaseModuleCatalogRuntime` DataVault `ModuleCostDTO` rows; retain a cold fallback that converts `buildCost` into one unmanaged `ModuleCostDTO` before the Burst transaction.
Rejected Alternatives: Purging the fallback outright would make deconstruction refund zero when the monolith is absent; reading the managed list inside the refund loop would keep the hot OOP path.
Scalability potential: Low/middle/high/ultra all consume identical cost DTO layout. Quality does not scale refund truth.
Hardware Impact: i3/MX350 avoids repeated managed list traversal once module costs are in Vault; fallback cost is one cold conversion on missing data.

## Decision 4 - Inventory Deposit Boundary

Problem: The existing SoA inventory lane is a query/mirror route; mutating it directly would fabricate presentation data without placing items in the authoritative 2D grid.
Solution: The Burst kernel emits `RefundCommandDTO`; owner completion applies commands through `PlayerInventory.TryAddItem`, which refreshes the SoA mirror through the inventory owner phase. Failed placements become `LootCacheDTO`.
Rejected Alternatives: Calling `SoaInventoryQueryEngine.TryApplyMutationOwnerPhase` from construction would mutate the mirror and bypass grid/save/mass authority.
Scalability potential: Low devices get no fake extra inventory scans. Middle/high/ultra can use richer overflow presentation while inventory truth remains one owner.
Hardware Impact: i3/MX350 avoids a duplicate inventory authority and associated reconciliation work.

## Decision 5 - CSR Severing

Problem: Removing a room must cut logistics/flood graph links without destroying GameObjects or rebuilding the whole graph as the first action.
Solution: `ExecuteModuleTeardownJob` zeros target incoming/outgoing CSR strength and flags edges; `HabitatGraphManager.MarkDeconstructionEdgesSevered` then ruptures edge records and invalidates CSR destinations.
Rejected Alternatives: Scene destruction or immediate full graph rebuild is heavier and less deterministic; only marking managed edge records would not give the transaction kernel a graph proof.
Scalability potential: Low quality severs bounded edges with direct arrays. Middle/high/ultra can admit more teardown requests per frame but keep the same CSR layout.
Hardware Impact: i3/MX350 gets O(E_target + E_incoming) array writes instead of broad scene teardown.

## Decision 6 - Overflow Cache Lie

Problem: Inventory full must not delete refund resources or spawn pickup GameObjects during teardown.
Solution: Store overflow in `LootCacheDTO` with exact `double3` AUP plus deterministic local offset and publish existing `InventoryDeathLootCacheSignal` through typed `SignalBus`.
Rejected Alternatives: `Instantiate`/pickup prefab spawn would create allocation and scene churn; dropping the resource would violate transaction conservation.
Scalability potential: Low uses small offset/no extra object authority. Middle/high/ultra can make VFX around the same signal richer.
Hardware Impact: i3/MX350 avoids prefab allocation and physics registration.

## Decision 7 - Black Box

Problem: A teardown NaN or over-budget spike must leave a fixed artifact, not a chat explanation.
Solution: `TeardownTelemetryEntry[300]` plus cursor live in DataVault and dump to `Docs/AgentLogs/Dump_SHINOBU_336.bin` on NaN or >500 us.
Rejected Alternatives: `Debug.Log` or editor-only counters are insufficient for postmortem analysis.
Scalability potential: Low keeps a fixed 300-row ring. Middle/high/ultra do not grow the telemetry footprint.
Hardware Impact: i3/MX350 pays fixed memory only; disk dump is fault path.

## Decision 8 - Verification Boundary

Problem: Compile proof is required, but the batch forbids dotnet build under CPU load or active compiler processes.
Solution: Ran static route scans and `git diff --check`; suppressed build when CPU sampled 100 and 9 dotnet/csc processes were active.
Rejected Alternatives: Launching another build would violate the explicit guard and contaminate other agents' compiler work.
Scalability potential: Verification does not affect runtime quality tiers.
Hardware Impact: Avoided adding compiler contention on already saturated machine.
