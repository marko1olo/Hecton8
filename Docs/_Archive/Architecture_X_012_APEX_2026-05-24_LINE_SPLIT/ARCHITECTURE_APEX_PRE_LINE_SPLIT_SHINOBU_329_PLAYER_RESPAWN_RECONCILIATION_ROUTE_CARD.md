# [ARCHIVE] Pre-Line-Split Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SHINOBU_329_PLAYER_RESPAWN_RECONCILIATION_ROUTE_CARD.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_329 Player Respawn Reconciliation Route Card

Owner: SHINOBU_329 / PLAYER_RESPAWN_RECONCILIATION_RUNTIME
Domain: Echelon 5 Combat & Survival Physiology
Status: STATIC SOURCE VERIFIED, UNITY IMPORT/PLAYMODE PENDING

## Authority Route

- Death producers: `HectonSurvivalSystem` and `HectonPlayerHealth` resolve movement/context AUP first and call `PlayerDeathReconciliationBridge.RequestRespawn` with the player GameObject entity hash as `PlayerHash`; if AUP is unavailable or nonfinite, the bridge writes a sanitized fallback request with invalid flags instead of falling back to managed death events.
- Hot signal route: `SignalBus<PlayerRespawnSignal>` carries request/commit; no death-domain scene load is allowed.
- Fail-closed route: bridge push failure no longer invokes `PlayerDiedEvent`, `OnDeath`, `PublishPlayerDeath`, or component disable; local health/survival mirrors are restored while the authoritative path remains SignalBus/Vault.
- Truth owner: `ShinobuRespawnReconciliationRuntime` owns respawn request/state/fade/telemetry/medbay/penalty buffers in `GlobalDataVault`.
- Job route: `FindNearestMedicalBayJob` resolves nearest active powered `MedicalBayDTO` in Burst, then `ResetPlayerPhysiologyJob` resets physiology/metabolism/gas state, writes AUP-local player kinematics, emits inventory penalty command, and records telemetry.
- Inventory loot route: `ResetPlayerPhysiologyJob` emits `InventoryCommandSignal=32` and matching `InventoryRespawnDeathAupSignal=64` so the inventory command can resolve the authoritative death AUP on the next PreSimulation flush; `PlayerInventory` falls back to same-frame `PlayerRespawnSignal.Sequence` only when present and `PlayerHash` matches the inventory owner, removes SOA inventory rows, then publishes direct Core lane `InventoryDeathLootCacheSignal=128` with item genetics, quality, and state flags. `LootMagnetSystem` writes data-only `DataOnlyDeathCache` Vault rows without `PickupItem`, `PersistentWorldRegistry`, Rigidbody, or Instantiate. `LootMagnetJob` preserves the sideband while writing acquisition events, acquired data-only rows recover before inactive cleanup, and recovered rows restore through the inventory state-preserving add path. If the loot pull job is busy or the cache has no safe inactive slot, the snapshot is requeued without a forced `.Complete()` or lost removed-inventory truth.
- Inventory result route: after removal attempts, `PlayerInventory` publishes direct Core lane `InventoryRespawnPenaltyResultSignal=32` (configure/flush/clear/dispatch registered in `GlobalSignals`, AOT-preserved in `SignalWardenRuntime`); respawn owner accepts only matching sequence plus matching or unspecified inventory hash and writes the actual dropped count into telemetry.
- Presentation route: `RespawnFadeDTO` is primed to black before reset scheduling and feeds `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie`.

## ABI
- `MedicalBayDTO=32`: `BayAUP double3@0`, `AssociatedBaseHash uint@24`, `Flags uint@28`.
- `RespawnStateDTO=32`: `TargetAUP double3@0`, `MedicalBayHashID uint@24`, `Flags uint@28`.
- `RespawnTelemetryEntry=64`: death/respawn AUP, cause hash, frame, microseconds, flags.
- `InventoryRespawnDeathAupSignal=64`: `DeathAUP double3@0`, `InventoryHash uint@24`, `Frame uint@28`, `Sequence uint@32`, `Flags uint@36`, `SourceHash uint@40`, explicit tail padding `@44..63`.
- `InventoryDeathLootCacheSignal=128`: `PositionAup AbsoluteUniversePosition@0`, `GeneticsMask ulong@48`, `InventoryHash uint@56`, `ItemHash uint@60`, `Sequence uint@64`, `Frame uint@68`, `Quantity ushort@72`, `QualityMilli ushort@74`, `Flags uint@76`, `StateFlags ushort@80`, explicit tail padding `@82..127`.
- `LootMagnetSignalEvent=128`: data-only cache sideband stores `GeneticsMask ulong@80`, `QualityMilli ushort@88`, `StateFlags ushort@90`; size is unchanged.
- `InventoryRespawnPenaltyResultSignal=32`: Core `GlobalSignals` payload with inventory hash, frame, sequence, dropped count, flags, and explicit padding.
- No `Pack=1`. No managed fields in respawn DTOs.

## Buffers
- `71604` respawn state, `71605` medbays, `71606` Dear Lie fade, `71607` telemetry ring, `71608` telemetry cursor, `71609` tuning, `71610` penalty rules, `71611` penalty rule count, `71612` CSV scratch, `71613` request.
- Borrowed owner rows: physiology vitals, decompression, tissue compartments, physiology scalars, metabolism, gas physiology, player kinematics.

## Scanner Proof
- `Scene_Reload_Scanner` writes `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_329.json`.
- Shared report key: `shinobu329SceneReloadScanner`.
- Scanner scope covers Player/Core/Gameplay/Physiology/Combat; `SceneRuntimeService` boot/menu authority and `RuntimeWatchdog` fatal process exit are documented exclusions.
