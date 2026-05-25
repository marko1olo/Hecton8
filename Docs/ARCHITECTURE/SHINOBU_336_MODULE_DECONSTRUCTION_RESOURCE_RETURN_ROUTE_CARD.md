# SHINOBU_336 Module Deconstruction Resource Return

Owner: SHINOBU_336 / MODULE_DECONSTRUCTION_RESOURCE_RETURN
Domain: Echelon 6 Habitat & Vehicles / module deconstruction
Status: STATIC_SOURCE / STATIC_DOC, runtime proof pending

## Authority Route

`ConstructionManager` owns the teardown request. It builds one `DeconstructionTransactionDTO`, resolves unmanaged module-cost rows, executes `ExecuteModuleTeardownJob`, applies refund commands through `PlayerInventory` authority, and publishes overflow through `SignalBus<InventoryDeathLootCacheSignal>`.

`HabitatGraphManager` owns topology. Transaction job zeros target CSR edge strength; owner marks matching edges ruptured and invalidates CSR destinations. Teardown never calls `Destroy()`.

## ABI

- `DeconstructionTransactionDTO=32`: `TargetModuleHash@0`, `InitiatorEntityHash@4`, `OriginalAUP double3@8`.
- `RefundCommandDTO=32`.
- `LootCacheDTO=64`.
- `TeardownTelemetryEntry=64`.
- `RefundProfileDTO=32`.

## BufferIDs

- `72016 Shinobu336TeardownTransactions`
- `72017 Shinobu336RefundCommands`
- `72018 Shinobu336LootCaches`
- `72019 Shinobu336TelemetryRing`
- `72020 Shinobu336TelemetryCursor`
- `72021 Shinobu336RefundProfiles`
- `72022 Shinobu336CsvScratch`
- `72023 Shinobu336Counters`

Owner is `SystemID.Construction`. These are runtime/proof lanes only and do not alter save identity, rollback identity, inventory truth identity, or logistics graph owner identity.

## Refund Rule

Refund quantity is `originalQuantity >> 1`, equivalent to floor 50 percent for positive integer counts. Missing or zero-cost rows produce no refund rather than fabricating resources.

- Preferred cost source: `BaseModuleCatalogRuntime` DataVault rows.
- If DataVault module-cost lanes are absent, cold compatibility bridge converts `BuildableData.buildCost`.
- Conversion target: one-row `ModuleCostDTO` before Burst transaction.
- Final `static_data.h8bin` readiness is not claimed.

## Overflow Route

Inventory success publishes `ItemAcquiredSignal`. Inventory failure or refund command buffer overflow writes `LootCacheDTO` at exact AUP plus a deterministic local offset and publishes `SignalBus<InventoryDeathLootCacheSignal>`.

The offset radius scales continuously from `0.35m` to `0.95m` by `GlobalQualityWeight`; refund scalar does not scale with quality.

## Scalability

`GlobalQualityWeight` maps admitted teardown transactions per frame from `5` to `50`.

Low quality spreads teardown work across more frames. Middle, high, and ultra admit more transactions and wider visual cache scattering.

Quality does not change DTO layout, resource truth, graph authority, or save/rollback ownership.

## Telemetry

`TeardownTelemetryEntry[300]` records frame, target/initiator/state hashes, processed modules, refunded resources, overflow count, severed edges, Burst us, quality, flags, target node, AUP magnitude.

Fault dump: `Docs/AgentLogs/Dump_SHINOBU_336.bin`.

Scanner report: `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_336.json`.

## Verification Status

Static runtime scan currently reports `0` direct `Destroy(` calls, legacy `DespawnOrDestroyModuleInstance` refs, legacy `InventoryDeathLootCacheSignal` `GlobalSignals.Publish` calls, and `CanAcceptItemQuantityBatch` preflight calls in the touched route.

Compile was not launched: CPU sampled at 100 percent with active `dotnet/csc`, violating batch build guard.
