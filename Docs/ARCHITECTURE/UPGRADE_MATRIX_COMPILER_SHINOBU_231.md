# Upgrade Matrix Compiler SHINOBU_231

Owner: SHINOBU_231 / TOOL_UPGRADE_MATRIX_COMPILER.

Domain: branchless equipment upgrade stat compilation for tools, suits, and vehicle-adjacent stat publication. The route is a stateless Vault kernel; it does not poll `GlobalRegistry` in hot paths and does not own private persistent `NativeArray` fields.

Vault buffers:
- `71380` `UpgradeMasksBuffer`: `UpgradeMaskDTO[16]`, active `ulong` masks.
- `71381` `UpgradeBaseStatsBuffer`: `UpgradeStatVectorDTO[64]`, baseline stat lanes.
- `71382` `UpgradeCompiledStatsBuffer`: `UpgradeStatVectorDTO[64]`, compiled stat lanes.
- `71383` `UpgradeLutBuffer`: `UpgradeLutEntryDTO[128]`, precomputed multiplier/additive rows.
- `71384` `UpgradeRulesBuffer`: `UpgradeBitRuleDTO[32]`, cold boot CSV/rule source.
- `71385` `UpgradeTelemetryRingBuffer`: `UpgradeTelemetryEntry[64] x 300`.
- `71386` `UpgradeTelemetryCursorBuffer`: `int[1]`.
- `71387` `UpgradeInventorySlotsBuffer`: `InventoryUpgradeSlotDTO[16]`.
- `71388` `UpgradeItemMapBuffer`: `UpgradeItemMapDTO[16]`.
- `71389` `UpgradeVisualFlagsBuffer`: `UpgradeVisualStateDTO[64]`, VISUAL_SYNC shader flag, quality, glow, and extrusion lane.

Phase route:
- `PRE_SIMULATION`: `SyncUpgradeMasksJob` packs inventory item hashes into `UpgradeMaskDTO.ActiveUpgradesMask`.
- `SIMULATION`: `EvaluateUpgradeMasksJob` applies LUT rows and branchless environmental gates.
- `SIMULATION`: publication jobs write compiled scalars into active equipment and vehicle kinematic mirror DTOs via `UnsafeUtility.AsRef`.
- `POST_SIMULATION`: `RecordUpgradeTelemetryJob` writes the 300-frame blackbox ring.
- `VISUAL_SYNC`: `PublishUpgradeVisualStateJob` copies high mask bits into `UpgradeVisualStateDTO` and emits continuous `GlobalQualityWeight` scalars for shader-driven geometry lies, not runtime mesh instantiation.

Rules:
- `UpgradeMaskDTO` must remain exactly 16 bytes with `ActiveUpgradesMask` at offset 8.
- Runtime arrays are requested through `UpgradeMatrixVault.AcquireHandles(..., NativeArrayOptions.UninitializedMemory)`.
- Core stat truth is identical across hardware tiers. `GlobalQualityWeight` only affects `UpgradeVisualStateDTO` intensity/extrusion/glow, so weak hardware collapses to cheap shader flags while high/ultra can spend the saved CPU on visual overkill.
